using System.Linq.Expressions;
using Gekko.Waybills.Application.Abstractions;
using Gekko.Waybills.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gekko.Waybills.Infrastructure;

public sealed class AppDbContext : DbContext, IAppDbContext
{
    private readonly ITenantContext _tenantContext;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<Waybill> Waybills => Set<Waybill>();

    public DbSet<ImportAudit> ImportAudits => Set<ImportAudit>();

    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();

    public DbSet<ExecutionLock> ExecutionLocks => Set<ExecutionLock>();

    public string TenantId => _tenantContext.TenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Waybill>(entity =>
        {
            var rowVersion = entity.Property(w => w.RowVersion)
                .IsConcurrencyToken()
                .ValueGeneratedOnAddOrUpdate();

            if (Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
            {
                rowVersion.HasDefaultValueSql("randomblob(8)");
            }
            else
            {
                rowVersion.IsRowVersion();
            }

            entity.HasIndex(w => new { w.TenantId, w.WaybillNumber })
                .IsUnique();

            entity.HasIndex(w => new { w.TenantId, w.DeliveryDate });
            entity.HasIndex(w => new { w.TenantId, w.Status });
            entity.HasIndex(w => new { w.TenantId, w.ProjectId });
            entity.HasIndex(w => new { w.TenantId, w.SupplierId });
        });

        modelBuilder.Entity<ExecutionLock>(entity =>
        {
            entity.HasKey(e => new { e.TenantId, e.LockName });
            entity.HasIndex(e => new { e.TenantId, e.LockName })
                .IsUnique();
        });

        ApplyGlobalQueryFilters(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges()
    {
        ApplyAuditInfo();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInfo();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditInfo()
    {
        var utcNow = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added && entry.Entity is ITenantOwned tenantOwned)
            {
                if (string.IsNullOrWhiteSpace(tenantOwned.TenantId))
                {
                    if (string.IsNullOrWhiteSpace(_tenantContext.TenantId))
                    {
                        throw new InvalidOperationException("TenantId is not set for the current request.");
                    }

                    tenantOwned.TenantId = _tenantContext.TenantId;
                }
            }

            if (entry.Entity is AuditableEntity auditable)
            {
                if (entry.State == EntityState.Added)
                {
                    auditable.CreatedAt = utcNow;
                    auditable.UpdatedAt = utcNow;
                }
                else if (entry.State == EntityState.Modified)
                {
                    auditable.UpdatedAt = utcNow;
                }
            }
        }
    }

    private void ApplyGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            Expression? filter = null;
            var parameter = Expression.Parameter(clrType, "e");

            if (typeof(ITenantOwned).IsAssignableFrom(clrType))
            {
                var tenantProperty = Expression.Property(parameter, nameof(ITenantOwned.TenantId));
                var tenantValue = Expression.Property(Expression.Constant(this), nameof(TenantId));
                filter = Expression.Equal(tenantProperty, tenantValue);
            }

            if (typeof(AuditableEntity).IsAssignableFrom(clrType))
            {
                var isDeletedProperty = Expression.Property(parameter, nameof(AuditableEntity.IsDeleted));
                var notDeleted = Expression.Equal(isDeletedProperty, Expression.Constant(false));
                filter = filter == null ? notDeleted : Expression.AndAlso(filter, notDeleted);
            }

            if (filter != null)
            {
                var lambda = Expression.Lambda(filter, parameter);
                modelBuilder.Entity(clrType).HasQueryFilter(lambda);
            }
        }
    }
}
