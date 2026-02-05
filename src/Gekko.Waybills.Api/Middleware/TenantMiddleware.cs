using Gekko.Waybills.Domain;

namespace Gekko.Waybills.Api.Middleware;

public sealed class TenantMiddleware
{
    private const string TenantHeaderName = "X-Tenant-ID";
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        if (context.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(TenantHeaderName, out var tenantHeader) ||
            string.IsNullOrWhiteSpace(tenantHeader))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                error = $"{TenantHeaderName} header is required."
            });
            return;
        }

        tenantContext.TenantId = tenantHeader.ToString().Trim();
        await _next(context);
    }
}
