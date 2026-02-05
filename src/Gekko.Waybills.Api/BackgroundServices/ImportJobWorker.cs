using Gekko.Waybills.Api.Services;
using Gekko.Waybills.Application.Abstractions;
using Gekko.Waybills.Application.Imports;
using Gekko.Waybills.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gekko.Waybills.Api.BackgroundServices;

public sealed class ImportJobWorker : BackgroundService
{
    private readonly IImportJobQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ImportJobWorker> _logger;
    private readonly ISummaryCacheVersionProvider _summaryCacheVersionProvider;

    public ImportJobWorker(
        IImportJobQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<ImportJobWorker> logger,
        ISummaryCacheVersionProvider summaryCacheVersionProvider)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _summaryCacheVersionProvider = summaryCacheVersionProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in _queue.DequeueAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
                var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
                tenantContext.TenantId = item.TenantId;

                var job = await dbContext.ImportJobs
                    .FirstOrDefaultAsync(j => j.Id == item.JobId, stoppingToken);

                if (job is null)
                {
                    continue;
                }

                job.Status = ImportJobStatus.RUNNING;
                job.ProgressPercent = 10;
                job.UpdatedAtUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("Import job started JobId={JobId} Tenant={TenantId}", item.JobId, item.TenantId);
                _logger.LogInformation("Import job progress JobId={JobId} Tenant={TenantId} Progress={Progress}",
                    item.JobId, item.TenantId, job.ProgressPercent);

                var importService = scope.ServiceProvider.GetRequiredService<IWaybillImportService>();
                using var stream = new MemoryStream(item.CsvData);
                var result = await importService.ImportAsync(stream, item.JobId, stoppingToken);

                job.Status = ImportJobStatus.SUCCEEDED;
                job.TotalRows = result.TotalRows;
                job.InsertedCount = result.InsertedCount;
                job.UpdatedCount = result.UpdatedCount;
                job.RejectedCount = result.RejectedCount;
                job.ProgressPercent = 100;
                job.Error = null;
                job.UpdatedAtUtc = DateTime.UtcNow;

                await dbContext.SaveChangesAsync(stoppingToken);

                _summaryCacheVersionProvider.Increment(item.TenantId, "import-async");
                _logger.LogInformation(
                    "Import job succeeded JobId={JobId} Tenant={TenantId} Total={Total} Inserted={Inserted} Updated={Updated} Rejected={Rejected}",
                    item.JobId,
                    item.TenantId,
                    result.TotalRows,
                    result.InsertedCount,
                    result.UpdatedCount,
                    result.RejectedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process import job {JobId}.", item.JobId);
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
                    var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
                    tenantContext.TenantId = item.TenantId;

                    var job = await dbContext.ImportJobs
                        .FirstOrDefaultAsync(j => j.Id == item.JobId, stoppingToken);

                    if (job is not null)
                    {
                        job.Status = ImportJobStatus.FAILED;
                        job.ProgressPercent = 100;
                        job.Error = ex.Message;
                        job.UpdatedAtUtc = DateTime.UtcNow;
                        await dbContext.SaveChangesAsync(stoppingToken);
                        _logger.LogError(ex, "Import job failed JobId={JobId} Tenant={TenantId}", item.JobId, item.TenantId);
                    }
                }
                catch (Exception updateEx)
                {
                    _logger.LogError(updateEx, "Failed to mark import job {JobId} as failed.", item.JobId);
                }
            }
        }
    }
}
