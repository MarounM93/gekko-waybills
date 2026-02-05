using Microsoft.Extensions.Caching.Memory;

namespace Gekko.Waybills.Api.Services;

public interface ISummaryCacheVersionProvider
{
    long GetVersion(string tenantId);
    void Increment(string tenantId, string reason);
}

public sealed class SummaryCacheVersionProvider : ISummaryCacheVersionProvider
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<SummaryCacheVersionProvider> _logger;

    public SummaryCacheVersionProvider(IMemoryCache cache, ILogger<SummaryCacheVersionProvider> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public long GetVersion(string tenantId)
    {
        return _cache.GetOrCreate(GetKey(tenantId), entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromHours(6);
            return 1L;
        });
    }

    public void Increment(string tenantId, string reason)
    {
        var key = GetKey(tenantId);
        var current = GetVersion(tenantId);
        _cache.Set(key, current + 1, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromHours(6)
        });
        _logger.LogInformation("Summary cache invalidated Tenant={TenantId} Reason={Reason}", tenantId, reason);
    }

    private static string GetKey(string tenantId) => $"summary-version:{tenantId}";
}
