namespace Gekko.Waybills.Domain;

public class Waybill : TenantAuditableEntity
{
    public Guid Id { get; set; }

    public string WaybillNumber { get; set; } = string.Empty;

    public Guid ProjectId { get; set; }

    public Guid SupplierId { get; set; }

    public DateOnly WaybillDate { get; set; }

    public DateOnly DeliveryDate { get; set; }

    public string ProductCode { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal TotalAmount { get; set; }

    public WaybillStatus Status { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
