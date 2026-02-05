namespace Gekko.Waybills.Domain;

public class Project : TenantAuditableEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
