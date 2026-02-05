namespace Gekko.Waybills.Domain;

public sealed class ImportJob : ITenantOwned
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public ImportJobStatus Status { get; set; }

    public int? TotalRows { get; set; }

    public int? InsertedCount { get; set; }

    public int? UpdatedCount { get; set; }

    public int? RejectedCount { get; set; }

    public int? ProgressPercent { get; set; }

    public string? Error { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
