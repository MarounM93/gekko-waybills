namespace Gekko.Waybills.Application.Imports;

/// <summary>Import summary response for waybill CSV uploads.</summary>
public sealed class WaybillImportResultDto
{
    /// <summary>Total number of data rows parsed.</summary>
    public int TotalRows { get; set; }

    /// <summary>Number of inserted waybills.</summary>
    public int InsertedCount { get; set; }

    /// <summary>Number of updated waybills.</summary>
    public int UpdatedCount { get; set; }

    /// <summary>Number of rejected rows.</summary>
    public int RejectedCount { get; set; }

    /// <summary>Rejected rows with errors.</summary>
    public List<RejectedRowDto> RejectedRows { get; set; } = [];

    /// <summary>Warnings for rows that were processed successfully.</summary>
    public List<WarningRowDto> Warnings { get; set; } = [];
}
