using Gekko.Waybills.Domain;

namespace Gekko.Waybills.Infrastructure;

public sealed class TenantContext : ITenantContext
{
    public string TenantId { get; set; } = string.Empty;
}
