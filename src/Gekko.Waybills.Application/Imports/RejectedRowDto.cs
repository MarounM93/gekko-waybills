namespace Gekko.Waybills.Application.Imports;

/// <summary>Represents a rejected CSV row and its errors.</summary>
public sealed class RejectedRowDto
{
    /// <summary>Row number in the CSV file.</summary>
    public int RowNumber { get; set; }

    /// <summary>Error codes for the row.</summary>
    public List<string> Errors { get; set; } = [];
}
