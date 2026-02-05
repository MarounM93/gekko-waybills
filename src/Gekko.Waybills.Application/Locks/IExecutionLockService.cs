namespace Gekko.Waybills.Application.Locks;

/// <summary>Service for acquiring and releasing execution locks.</summary>
public interface IExecutionLockService
{
    /// <summary>Attempts to acquire the named lock for the current tenant.</summary>
    Task<bool> TryAcquireAsync(string lockName, TimeSpan duration, CancellationToken cancellationToken);

    /// <summary>Releases the named lock for the current tenant.</summary>
    Task ReleaseAsync(string lockName, CancellationToken cancellationToken);
}
