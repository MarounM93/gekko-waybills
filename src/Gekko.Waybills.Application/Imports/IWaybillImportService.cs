namespace Gekko.Waybills.Application.Imports;

/// <summary>Handles CSV import of waybills.</summary>
public interface IWaybillImportService
{
    /// <summary>Imports waybills from a CSV stream.</summary>
    /// <param name="csvStream">CSV data stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<WaybillImportResultDto> ImportAsync(Stream csvStream, CancellationToken cancellationToken);

    /// <summary>Imports waybills from a CSV stream with a known job identifier.</summary>
    /// <param name="csvStream">CSV data stream.</param>
    /// <param name="importJobId">Import job identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<WaybillImportResultDto> ImportAsync(Stream csvStream, Guid importJobId, CancellationToken cancellationToken);
}
