namespace Gekko.Waybills.Application.Events;

/// <summary>Publishes integration events.</summary>
public interface IEventPublisher
{
    /// <summary>Publishes the waybills imported event.</summary>
    Task PublishWaybillsImportedAsync(WaybillsImportedEvent payload, CancellationToken cancellationToken);
}
