using Gekko.Waybills.Domain;

namespace Gekko.Waybills.Api.Middleware;

public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var tenantId = !string.IsNullOrWhiteSpace(tenantContext.TenantId)
                ? tenantContext.TenantId
                : context.Request.Headers["X-Tenant-ID"].ToString();

            _logger.LogInformation(
                "HTTP {Method} {Path} Tenant={TenantId} Status={StatusCode} ElapsedMs={ElapsedMs}",
                context.Request.Method,
                context.Request.Path.Value,
                string.IsNullOrWhiteSpace(tenantId) ? "UNKNOWN" : tenantId,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }
}
