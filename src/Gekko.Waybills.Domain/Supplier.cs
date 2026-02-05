namespace Gekko.Waybills.Domain;

public class Supplier : TenantAuditableEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
