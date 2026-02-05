using Gekko.Waybills.Application.Abstractions;
using Gekko.Waybills.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gekko.Waybills.Application.Queries;

/// <summary>EF Core query service for waybills.</summary>
public sealed class WaybillQueryService : IWaybillQueryService
{
    private const int MaxPageSize = 200;
    private readonly IAppDbContext _dbContext;

    public WaybillQueryService(IAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResultDto<WaybillListItemDto>> GetWaybillsAsync(
        WaybillQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        var page = parameters.Page <= 0 ? 1 : parameters.Page;
        var pageSize = parameters.PageSize <= 0 ? 20 : Math.Min(parameters.PageSize, MaxPageSize);

        var query = from w in _dbContext.Waybills
                    join p in _dbContext.Projects on w.ProjectId equals p.Id
                    join s in _dbContext.Suppliers on w.SupplierId equals s.Id
                    select new { w, p, s };

        if (parameters.WaybillDateFrom.HasValue)
        {
            var from = parameters.WaybillDateFrom.Value;
            query = query.Where(x => x.w.WaybillDate >= from);
        }

        if (parameters.WaybillDateTo.HasValue)
        {
            var to = parameters.WaybillDateTo.Value;
            query = query.Where(x => x.w.WaybillDate <= to);
        }

        if (parameters.DeliveryDateFrom.HasValue)
        {
            var from = parameters.DeliveryDateFrom.Value;
            query = query.Where(x => x.w.DeliveryDate >= from);
        }

        if (parameters.DeliveryDateTo.HasValue)
        {
            var to = parameters.DeliveryDateTo.Value;
            query = query.Where(x => x.w.DeliveryDate <= to);
        }

        if (parameters.Status.HasValue)
        {
            var status = parameters.Status.Value;
            query = query.Where(x => x.w.Status == status);
        }

        if (parameters.ProjectId.HasValue)
        {
            var projectId = parameters.ProjectId.Value;
            query = query.Where(x => x.w.ProjectId == projectId);
        }

        if (parameters.SupplierId.HasValue)
        {
            var supplierId = parameters.SupplierId.Value;
            query = query.Where(x => x.w.SupplierId == supplierId);
        }

        if (!string.IsNullOrWhiteSpace(parameters.ProductCode))
        {
            var productCode = parameters.ProductCode.Trim();
            query = query.Where(x => x.w.ProductCode == productCode);
        }

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var term = parameters.Search.Trim();
            query = query.Where(x => x.p.Name.Contains(term) || x.s.Name.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var itemsData = await query
            .OrderByDescending(x => x.w.DeliveryDate)
            .ThenByDescending(x => x.w.WaybillDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                Id = x.w.Id,
                WaybillNumber = x.w.WaybillNumber,
                ProjectId = x.w.ProjectId,
                ProjectName = x.p.Name,
                SupplierId = x.w.SupplierId,
                SupplierName = x.s.Name,
                WaybillDate = x.w.WaybillDate,
                DeliveryDate = x.w.DeliveryDate,
                ProductCode = x.w.ProductCode,
                Quantity = x.w.Quantity,
                UnitPrice = x.w.UnitPrice,
                TotalAmount = x.w.TotalAmount,
                Status = x.w.Status,
                RowVersion = x.w.RowVersion
            })
            .ToListAsync(cancellationToken);

        var items = itemsData.Select(x => new WaybillListItemDto
        {
            Id = x.Id,
            WaybillNumber = x.WaybillNumber,
            ProjectId = x.ProjectId,
            ProjectName = x.ProjectName,
            SupplierId = x.SupplierId,
            SupplierName = x.SupplierName,
            WaybillDate = x.WaybillDate,
            DeliveryDate = x.DeliveryDate,
            ProductCode = x.ProductCode,
            Quantity = x.Quantity,
            UnitPrice = x.UnitPrice,
            TotalAmount = x.TotalAmount,
            Status = x.Status,
            RowVersionBase64 = Convert.ToBase64String(x.RowVersion ?? Array.Empty<byte>())
        }).ToList();

        return new PagedResultDto<WaybillListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<WaybillDetailDto?> GetWaybillByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var data = await (from w in _dbContext.Waybills
                          join p in _dbContext.Projects on w.ProjectId equals p.Id
                          join s in _dbContext.Suppliers on w.SupplierId equals s.Id
                          where w.Id == id
                          select new
                          {
                              w.Id,
                              w.WaybillNumber,
                              w.ProjectId,
                              ProjectName = p.Name,
                              w.SupplierId,
                              SupplierName = s.Name,
                              w.WaybillDate,
                              w.DeliveryDate,
                              w.ProductCode,
                              w.Quantity,
                              w.UnitPrice,
                              w.TotalAmount,
                              w.Status,
                              w.RowVersion
                          }).FirstOrDefaultAsync(cancellationToken);

        if (data is null)
        {
            return null;
        }

        return new WaybillDetailDto
        {
            Id = data.Id,
            WaybillNumber = data.WaybillNumber,
            ProjectId = data.ProjectId,
            ProjectName = data.ProjectName,
            SupplierId = data.SupplierId,
            SupplierName = data.SupplierName,
            WaybillDate = data.WaybillDate,
            DeliveryDate = data.DeliveryDate,
            ProductCode = data.ProductCode,
            Quantity = data.Quantity,
            UnitPrice = data.UnitPrice,
            TotalAmount = data.TotalAmount,
            Status = data.Status,
            RowVersionBase64 = Convert.ToBase64String(data.RowVersion ?? Array.Empty<byte>())
        };
    }

    public async Task<List<WaybillListItemDto>> GetWaybillsByProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var itemsData = await (from w in _dbContext.Waybills
                               join p in _dbContext.Projects on w.ProjectId equals p.Id
                               join s in _dbContext.Suppliers on w.SupplierId equals s.Id
                               where w.ProjectId == projectId
                               orderby w.DeliveryDate descending
                               select new
                               {
                                   w.Id,
                                   w.WaybillNumber,
                                   w.ProjectId,
                                   ProjectName = p.Name,
                                   w.SupplierId,
                                   SupplierName = s.Name,
                                   w.WaybillDate,
                                   w.DeliveryDate,
                                   w.ProductCode,
                                   w.Quantity,
                                   w.UnitPrice,
                                   w.TotalAmount,
                                   w.Status,
                                   w.RowVersion
                               }).ToListAsync(cancellationToken);

        return itemsData.Select(x => new WaybillListItemDto
        {
            Id = x.Id,
            WaybillNumber = x.WaybillNumber,
            ProjectId = x.ProjectId,
            ProjectName = x.ProjectName,
            SupplierId = x.SupplierId,
            SupplierName = x.SupplierName,
            WaybillDate = x.WaybillDate,
            DeliveryDate = x.DeliveryDate,
            ProductCode = x.ProductCode,
            Quantity = x.Quantity,
            UnitPrice = x.UnitPrice,
            TotalAmount = x.TotalAmount,
            Status = x.Status,
            RowVersionBase64 = Convert.ToBase64String(x.RowVersion ?? Array.Empty<byte>())
        }).ToList();
    }

    public async Task<SupplierSummaryDto?> GetSupplierSummaryAsync(Guid supplierId, CancellationToken cancellationToken)
    {
        var totals = await _dbContext.Waybills
            .Where(w => w.SupplierId == supplierId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalQuantity = g.Sum(x => x.Quantity),
                TotalAmount = g.Sum(x => x.TotalAmount)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (totals is null)
        {
            return null;
        }

        var breakdown = await _dbContext.Waybills
            .Where(w => w.SupplierId == supplierId)
            .GroupBy(w => w.Status)
            .Select(g => new StatusTotalsDto
            {
                Status = g.Key,
                TotalQuantity = g.Sum(x => x.Quantity),
                TotalAmount = g.Sum(x => x.TotalAmount)
            })
            .ToListAsync(cancellationToken);

        return new SupplierSummaryDto
        {
            SupplierId = supplierId,
            TotalQuantity = totals.TotalQuantity,
            TotalAmount = totals.TotalAmount,
            BreakdownByStatus = breakdown
        };
    }

    public async Task<WaybillSummaryDto> GetWaybillSummaryAsync(CancellationToken cancellationToken)
    {
        var statusTotals = await _dbContext.Waybills
            .GroupBy(w => w.Status)
            .Select(g => new StatusTotalsDto
            {
                Status = g.Key,
                TotalQuantity = g.Sum(x => x.Quantity),
                TotalAmount = g.Sum(x => x.TotalAmount)
            })
            .ToListAsync(cancellationToken);

        var monthlyTotals = await _dbContext.Waybills
            .GroupBy(w => new { w.DeliveryDate.Year, w.DeliveryDate.Month })
            .Select(g => new MonthlyTotalsDto
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                TotalQuantity = g.Sum(x => x.Quantity),
                TotalAmount = g.Sum(x => x.TotalAmount)
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToListAsync(cancellationToken);

        var topSuppliers = await (from w in _dbContext.Waybills
                                  join s in _dbContext.Suppliers on w.SupplierId equals s.Id
                                  group new { w, s } by new { w.SupplierId, s.Name } into g
                                  orderby g.Sum(x => x.w.Quantity) descending
                                  select new TopSupplierDto
                                  {
                                      SupplierId = g.Key.SupplierId,
                                      SupplierName = g.Key.Name,
                                      TotalQuantity = g.Sum(x => x.w.Quantity)
                                  })
            .Take(5)
            .ToListAsync(cancellationToken);

        var projectTotals = await (from w in _dbContext.Waybills
                                   join p in _dbContext.Projects on w.ProjectId equals p.Id
                                   group new { w, p } by new { w.ProjectId, p.Name } into g
                                   orderby g.Sum(x => x.w.TotalAmount) descending
                                   select new ProjectTotalsDto
                                   {
                                       ProjectId = g.Key.ProjectId,
                                       ProjectName = g.Key.Name,
                                       TotalQuantity = g.Sum(x => x.w.Quantity),
                                       TotalAmount = g.Sum(x => x.w.TotalAmount)
                                   })
            .ToListAsync(cancellationToken);

        return new WaybillSummaryDto
        {
            StatusTotals = statusTotals,
            MonthlyTotals = monthlyTotals,
            TopSuppliersByQuantity = topSuppliers,
            ProjectTotals = projectTotals
        };
    }
}
