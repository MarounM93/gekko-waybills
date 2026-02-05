using Gekko.Waybills.Domain;

namespace Gekko.Waybills.Application.Queries;

/// <summary>Detailed waybill response.</summary>
public sealed class WaybillDetailDto
{
    /// <summary>Waybill identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Waybill number.</summary>
    public string WaybillNumber { get; set; } = string.Empty;

    /// <summary>Project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>Project name.</summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>Supplier identifier.</summary>
    public Guid SupplierId { get; set; }

    /// <summary>Supplier name.</summary>
    public string SupplierName { get; set; } = string.Empty;

    /// <summary>Waybill date.</summary>
    public DateOnly WaybillDate { get; set; }

    /// <summary>Delivery date.</summary>
    public DateOnly DeliveryDate { get; set; }

    /// <summary>Product code.</summary>
    public string ProductCode { get; set; } = string.Empty;

    /// <summary>Quantity.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Unit price.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Total amount.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Waybill status.</summary>
    public WaybillStatus Status { get; set; }

    /// <summary>Row version encoded as base64.</summary>
    public string RowVersionBase64 { get; set; } = string.Empty;
}
