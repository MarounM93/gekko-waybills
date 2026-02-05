namespace Gekko.Waybills.Domain;

public abstract class TenantAuditableEntity : AuditableEntity, ITenantOwned
{
    public string TenantId { get; set; } = string.Empty;
}
