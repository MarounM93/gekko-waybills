namespace Gekko.Waybills.Domain;

public sealed class ExecutionLock : ITenantOwned
{
    public string TenantId { get; set; } = string.Empty;

    public string LockName { get; set; } = string.Empty;

    public DateTime AcquiredAtUtc { get; set; }

    public string? AcquiredBy { get; set; }

    public DateTime ExpiresAtUtc { get; set; }
}
