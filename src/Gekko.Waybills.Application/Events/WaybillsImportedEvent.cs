namespace Gekko.Waybills.Application.Events;

/// <summary>Event payload for waybill import completion.</summary>
public sealed class WaybillsImportedEvent
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Import job identifier.</summary>
    public Guid ImportJobId { get; set; }

    /// <summary>Total rows in the import.</summary>
    public int TotalRows { get; set; }

    /// <summary>Inserted row count.</summary>
    public int InsertedCount { get; set; }

    /// <summary>Updated row count.</summary>
    public int UpdatedCount { get; set; }

    /// <summary>Rejected row count.</summary>
    public int RejectedCount { get; set; }

    /// <summary>UTC timestamp when import completed.</summary>
    public DateTime OccurredAtUtc { get; set; }
}
