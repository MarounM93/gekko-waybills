namespace Gekko.Waybills.Application.Imports;

/// <summary>Represents warnings for a CSV row.</summary>
public sealed class WarningRowDto
{
    /// <summary>Row number in the CSV file.</summary>
    public int RowNumber { get; set; }

    /// <summary>Warning codes for the row.</summary>
    public List<string> Warnings { get; set; } = [];
}
