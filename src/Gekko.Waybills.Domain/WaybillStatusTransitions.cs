namespace Gekko.Waybills.Domain;

public static class WaybillStatusTransitions
{
    public static bool IsValidTransition(WaybillStatus from, WaybillStatus to)
    {
        if (from == to)
        {
            return true;
        }

        return from switch
        {
            WaybillStatus.PENDING => to is WaybillStatus.DELIVERED or WaybillStatus.CANCELLED,
            WaybillStatus.DELIVERED => to is WaybillStatus.DISPUTED,
            WaybillStatus.CANCELLED => false,
            WaybillStatus.DISPUTED => false,
            _ => false
        };
    }
}
