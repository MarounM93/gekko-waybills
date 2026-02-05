namespace Gekko.Waybills.Domain;

public sealed class ImportAudit : ITenantOwned
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public Guid ImportJobId { get; set; }

    public int TotalRows { get; set; }

    public int InsertedCount { get; set; }

    public int UpdatedCount { get; set; }

    public int RejectedCount { get; set; }

    public DateTime ReceivedAtUtc { get; set; }
}
