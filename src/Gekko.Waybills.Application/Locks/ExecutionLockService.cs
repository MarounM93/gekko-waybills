using Gekko.Waybills.Application.Abstractions;
using Gekko.Waybills.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gekko.Waybills.Application.Locks;

/// <summary>DB-backed execution lock service.</summary>
public sealed class ExecutionLockService : IExecutionLockService
{
    private readonly IAppDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public ExecutionLockService(IAppDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<bool> TryAcquireAsync(string lockName, TimeSpan duration, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new InvalidOperationException("TenantId is not set for the current request.");
        }

        var now = DateTime.UtcNow;
        var expiresAt = now.Add(duration);

        var existing = await _dbContext.ExecutionLocks
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.LockName == lockName, cancellationToken);

        if (existing is null)
        {
            _dbContext.ExecutionLocks.Add(new ExecutionLock
            {
                TenantId = tenantId,
                LockName = lockName,
                AcquiredAtUtc = now,
                ExpiresAtUtc = expiresAt
            });

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateException)
            {
                return false;
            }
        }

        if (existing.ExpiresAtUtc < now)
        {
            existing.AcquiredAtUtc = now;
            existing.ExpiresAtUtc = expiresAt;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        return false;
    }

    public async Task ReleaseAsync(string lockName, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return;
        }

        var existing = await _dbContext.ExecutionLocks
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.LockName == lockName, cancellationToken);

        if (existing is null)
        {
            return;
        }

        _dbContext.ExecutionLocks.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
