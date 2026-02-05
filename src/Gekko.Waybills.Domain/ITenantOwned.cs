namespace Gekko.Waybills.Domain;

public interface ITenantOwned
{
    string TenantId { get; set; }
}
