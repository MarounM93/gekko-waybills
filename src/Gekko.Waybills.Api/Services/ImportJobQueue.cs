using System.Threading.Channels;

namespace Gekko.Waybills.Api.Services;

public interface IImportJobQueue
{
    ValueTask QueueAsync(ImportJobWorkItem item, CancellationToken cancellationToken);
    IAsyncEnumerable<ImportJobWorkItem> DequeueAsync(CancellationToken cancellationToken);
}

public sealed class ImportJobQueue : IImportJobQueue
{
    private readonly Channel<ImportJobWorkItem> _channel;

    public ImportJobQueue(Channel<ImportJobWorkItem> channel)
    {
        _channel = channel;
    }

    public ValueTask QueueAsync(ImportJobWorkItem item, CancellationToken cancellationToken)
    {
        return _channel.Writer.WriteAsync(item, cancellationToken);
    }

    public IAsyncEnumerable<ImportJobWorkItem> DequeueAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}

public sealed class ImportJobWorkItem
{
    public Guid JobId { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public byte[] CsvData { get; init; } = Array.Empty<byte>();
}
