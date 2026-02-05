using Gekko.Waybills.Domain;
using Xunit;

namespace Gekko.Waybills.Tests;

public sealed class WaybillStatusTransitionsTests
{
    [Fact]
    public void PendingToDelivered_IsAllowed()
    {
        var result = WaybillStatusTransitions.IsValidTransition(WaybillStatus.PENDING, WaybillStatus.DELIVERED);

        Assert.True(result);
    }

    [Fact]
    public void DeliveredToPending_IsRejected()
    {
        var result = WaybillStatusTransitions.IsValidTransition(WaybillStatus.DELIVERED, WaybillStatus.PENDING);

        Assert.False(result);
    }

    [Fact]
    public void CancelledToDelivered_IsRejected()
    {
        var result = WaybillStatusTransitions.IsValidTransition(WaybillStatus.CANCELLED, WaybillStatus.DELIVERED);

        Assert.False(result);
    }
}
