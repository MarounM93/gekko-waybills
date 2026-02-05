using Gekko.Waybills.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gekko.Waybills.Application.Abstractions;

/// <summary>Abstraction over the EF Core context for queries and commands.</summary>
public interface IAppDbContext
{
    DbSet<Project> Projects { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<Waybill> Waybills { get; }
    DbSet<ImportAudit> ImportAudits { get; }
    DbSet<ImportJob> ImportJobs { get; }
    DbSet<ExecutionLock> ExecutionLocks { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
