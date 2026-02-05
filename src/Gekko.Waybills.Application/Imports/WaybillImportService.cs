using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Gekko.Waybills.Application.Abstractions;
using Gekko.Waybills.Application.Events;
using Gekko.Waybills.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace Gekko.Waybills.Application.Imports;

/// <summary>CSV import service for waybills.</summary>
public sealed class WaybillImportService : IWaybillImportService
{
    private const decimal PriceTolerance = 0.01m;
    private readonly IAppDbContext _dbContext;
    private readonly IEventPublisher _eventPublisher;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<WaybillImportService> _logger;

    public WaybillImportService(
        IAppDbContext dbContext,
        ITenantContext tenantContext,
        IEventPublisher eventPublisher,
        ILogger<WaybillImportService> logger)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public Task<WaybillImportResultDto> ImportAsync(Stream csvStream, CancellationToken cancellationToken)
    {
        return ImportInternalAsync(csvStream, Guid.NewGuid(), cancellationToken);
    }

    public Task<WaybillImportResultDto> ImportAsync(Stream csvStream, Guid importJobId, CancellationToken cancellationToken)
    {
        return ImportInternalAsync(csvStream, importJobId, cancellationToken);
    }

    private async Task<WaybillImportResultDto> ImportInternalAsync(
        Stream csvStream,
        Guid importJobId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_tenantContext.TenantId))
        {
            throw new InvalidOperationException("TenantId is not set for the current request.");
        }

        var result = new WaybillImportResultDto();
        var validRows = new List<ParsedWaybillRow>();
        var seenWaybillNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null,
            BadDataFound = null,
            PrepareHeaderForMatch = args => args.Header?.Trim()
        };

        using var reader = new StreamReader(csvStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        using var csv = new CsvReader(reader, config);
        csv.Context.RegisterClassMap<ImportRowDtoMap>();

        if (!await csv.ReadAsync())
        {
            return result;
        }

        csv.ReadHeader();

        while (await csv.ReadAsync())
        {
            result.TotalRows++;
            var rowNumber = csv.Context?.Parser?.Row ?? 0;
            ImportRowDto row;
            try
            {
                row = csv.GetRecord<ImportRowDto>();
            }
            catch (Exception)
            {
                result.RejectedCount++;
                result.RejectedRows.Add(new RejectedRowDto
                {
                    RowNumber = rowNumber,
                    Errors = ["INVALID_ROW"]
                });
                continue;
            }

            var errors = new List<string>();
            var warnings = new List<string>();

            var tenantId = row.TenantId?.Trim();
            if (!string.IsNullOrWhiteSpace(tenantId) &&
                !string.Equals(tenantId, _tenantContext.TenantId, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("TENANT_MISMATCH");
            }

            var waybillNumberValid = TryNormalizeRequired(row.WaybillNumber, "WAYBILL_NUMBER_REQUIRED", errors, out var waybillNumber);
            var projectNameValid = TryNormalizeRequired(row.ProjectName, "PROJECT_NAME_REQUIRED", errors, out var projectName);
            var supplierNameValid = TryNormalizeRequired(row.SupplierName, "SUPPLIER_NAME_REQUIRED", errors, out var supplierName);
            var productCodeValid = TryNormalizeRequired(row.ProductCode, "PRODUCT_CODE_REQUIRED", errors, out var productCode);

            var waybillDateParsed = TryParseDate(row.WaybillDate, out var waybillDate);
            if (!waybillDateParsed)
            {
                errors.Add("INVALID_WAYBILL_DATE");
            }

            var deliveryDateParsed = TryParseDate(row.DeliveryDate, out var deliveryDate);
            if (!deliveryDateParsed)
            {
                errors.Add("INVALID_DELIVERY_DATE");
            }

            if (waybillDateParsed && deliveryDateParsed && deliveryDate < waybillDate)
            {
                errors.Add("DELIVERY_BEFORE_WAYBILL");
            }

            var quantityParsed = TryParseDecimal(row.Quantity, out var quantity);
            if (!quantityParsed)
            {
                errors.Add("INVALID_QUANTITY");
            }
            else if (quantity < 0.5m || quantity > 50m)
            {
                errors.Add("QUANTITY_OUT_OF_RANGE");
            }

            var unitPriceParsed = TryParseDecimal(row.UnitPrice, out var unitPrice);
            if (!unitPriceParsed)
            {
                errors.Add("INVALID_UNIT_PRICE");
            }

            var totalParsed = TryParseDecimal(row.TotalAmount, out var totalAmount);
            if (!totalParsed)
            {
                errors.Add("INVALID_TOTAL_AMOUNT");
            }

            if (!Enum.TryParse<WaybillStatus>(row.Status?.Trim(), true, out var status))
            {
                errors.Add("INVALID_STATUS");
            }

            if (quantityParsed && unitPriceParsed && totalParsed)
            {
                var expectedTotal = quantity * unitPrice;
                if (Math.Abs(expectedTotal - totalAmount) > PriceTolerance)
                {
                    warnings.Add("PRICE_DISCREPANCY");
                }
            }

            if (errors.Count > 0)
            {
                result.RejectedCount++;
                result.RejectedRows.Add(new RejectedRowDto
                {
                    RowNumber = rowNumber,
                    Errors = errors
                });
                _logger.LogWarning(
                    "Import validation failed Tenant={TenantId} Row={RowNumber} Errors={Errors}",
                    _tenantContext.TenantId,
                    rowNumber,
                    string.Join(",", errors));
                continue;
            }

            if (warnings.Count > 0)
            {
                result.Warnings.Add(new WarningRowDto
                {
                    RowNumber = rowNumber,
                    Warnings = warnings
                });
                if (warnings.Contains("PRICE_DISCREPANCY"))
                {
                    _logger.LogWarning(
                        "Price discrepancy Tenant={TenantId} Row={RowNumber}",
                        _tenantContext.TenantId,
                        rowNumber);
                }
            }

            if (!waybillNumberValid || !projectNameValid || !supplierNameValid || !productCodeValid)
            {
                continue;
            }

            if (!seenWaybillNumbers.Add(waybillNumber))
            {
                _logger.LogWarning(
                    "Duplicate waybill number in CSV Tenant={TenantId} Row={RowNumber} WaybillNumber={WaybillNumber}",
                    _tenantContext.TenantId,
                    rowNumber,
                    waybillNumber);
            }

            validRows.Add(new ParsedWaybillRow
            {
                RowNumber = rowNumber,
                WaybillNumber = waybillNumber,
                ProjectName = projectName,
                SupplierName = supplierName,
                WaybillDate = waybillDate,
                DeliveryDate = deliveryDate,
                ProductCode = productCode,
                Quantity = quantity,
                UnitPrice = unitPrice,
                TotalAmount = totalAmount,
                Status = status
            });
        }

        if (validRows.Count > 0)
        {
            var projectNames = validRows.Select(r => r.ProjectName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
            var supplierNames = validRows.Select(r => r.SupplierName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
            var waybillNumbers = validRows.Select(r => r.WaybillNumber)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

            var projects = await _dbContext.Projects
            .Where(p => projectNames.Contains(p.Name))
            .ToListAsync(cancellationToken);
            var suppliers = await _dbContext.Suppliers
            .Where(s => supplierNames.Contains(s.Name))
            .ToListAsync(cancellationToken);
            var existingWaybills = await _dbContext.Waybills
            .Where(w => waybillNumbers.Contains(w.WaybillNumber))
            .ToListAsync(cancellationToken);

            var projectLookup = projects.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
            var supplierLookup = suppliers.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
            var waybillLookup = existingWaybills.ToDictionary(w => w.WaybillNumber, StringComparer.OrdinalIgnoreCase);

            foreach (var row in validRows)
            {
                if (!projectLookup.TryGetValue(row.ProjectName, out var project))
                {
                    project = new Project
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _tenantContext.TenantId,
                        Name = row.ProjectName
                    };
                    _dbContext.Projects.Add(project);
                    projectLookup[row.ProjectName] = project;
                }

                if (!supplierLookup.TryGetValue(row.SupplierName, out var supplier))
                {
                    supplier = new Supplier
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _tenantContext.TenantId,
                        Name = row.SupplierName
                    };
                    _dbContext.Suppliers.Add(supplier);
                    supplierLookup[row.SupplierName] = supplier;
                }

                if (waybillLookup.TryGetValue(row.WaybillNumber, out var waybill))
                {
                    _logger.LogWarning(
                        "Duplicate waybill detected in DB Tenant={TenantId} WaybillNumber={WaybillNumber}",
                        _tenantContext.TenantId,
                        row.WaybillNumber);
                    waybill.ProjectId = project.Id;
                    waybill.SupplierId = supplier.Id;
                    waybill.WaybillDate = row.WaybillDate;
                    waybill.DeliveryDate = row.DeliveryDate;
                    waybill.ProductCode = row.ProductCode;
                    waybill.Quantity = row.Quantity;
                    waybill.UnitPrice = row.UnitPrice;
                    waybill.TotalAmount = row.TotalAmount;
                    waybill.Status = row.Status;
                    result.UpdatedCount++;
                }
                else
                {
                    waybill = new Waybill
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _tenantContext.TenantId,
                        WaybillNumber = row.WaybillNumber,
                        ProjectId = project.Id,
                        SupplierId = supplier.Id,
                        WaybillDate = row.WaybillDate,
                        DeliveryDate = row.DeliveryDate,
                        ProductCode = row.ProductCode,
                        Quantity = row.Quantity,
                        UnitPrice = row.UnitPrice,
                        TotalAmount = row.TotalAmount,
                        Status = row.Status
                    };
                    _dbContext.Waybills.Add(waybill);
                    waybillLookup[row.WaybillNumber] = waybill;
                    result.InsertedCount++;
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var message = new WaybillsImportedEvent
        {
            TenantId = _tenantContext.TenantId,
            ImportJobId = importJobId,
            TotalRows = result.TotalRows,
            InsertedCount = result.InsertedCount,
            UpdatedCount = result.UpdatedCount,
            RejectedCount = result.RejectedCount,
            OccurredAtUtc = DateTime.UtcNow
        };
        await _eventPublisher.PublishWaybillsImportedAsync(message, cancellationToken);

        _logger.LogInformation(
            "CSV import completed Tenant={TenantId} Total={Total} Inserted={Inserted} Updated={Updated} Rejected={Rejected}",
            _tenantContext.TenantId,
            result.TotalRows,
            result.InsertedCount,
            result.UpdatedCount,
            result.RejectedCount);
        return result;
    }

    private static bool TryNormalizeRequired(string? value, string errorCode, List<string> errors, out string normalized)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(errorCode);
            normalized = string.Empty;
            return false;
        }

        normalized = value.Trim();
        return true;
    }

    private static bool TryParseDecimal(string? value, out decimal result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = default;
            return false;
        }

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result)
               || decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out result);
    }

    private static bool TryParseDate(string? value, out DateOnly result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = default;
            return false;
        }

        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
        {
            return true;
        }

        if (DateOnly.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out result))
        {
            return true;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
        {
            result = DateOnly.FromDateTime(dateTime);
            return true;
        }

        if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out dateTime))
        {
            result = DateOnly.FromDateTime(dateTime);
            return true;
        }

        result = default;
        return false;
    }

    private sealed class ParsedWaybillRow
    {
        public int RowNumber { get; init; }
        public string WaybillNumber { get; init; } = string.Empty;
        public string ProjectName { get; init; } = string.Empty;
        public string SupplierName { get; init; } = string.Empty;
        public DateOnly WaybillDate { get; init; }
        public DateOnly DeliveryDate { get; init; }
        public string ProductCode { get; init; } = string.Empty;
        public decimal Quantity { get; init; }
        public decimal UnitPrice { get; init; }
        public decimal TotalAmount { get; init; }
        public WaybillStatus Status { get; init; }
    }
}
