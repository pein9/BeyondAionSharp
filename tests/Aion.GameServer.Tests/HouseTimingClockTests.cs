using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Time;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class HouseTimingClockTests
{
    [Fact]
    public void DefaultAuctionClockFollowsVirtualTimeAcrossSundayNoon()
    {
        long now = ServerTime.Of(new DateTime(2026, 5, 24, 11, 59, 0)).ToUnixTimeMilliseconds();
        SystemClock.UseSource(() => now);
        try
        {
            var service = new HouseAuctionTimingService();
            Assert.True(service.IsBiddingTime(1));
            Assert.Equal(60, service.GetRemainingAuctionSeconds(1));
            now += 60_000;
            Assert.False(service.IsBiddingTime(1));
        }
        finally { SystemClock.UseSystemClock(); }
    }

    [Fact]
    public void DefaultMaintenanceClockFollowsVirtualTimeForOverdueStages()
    {
        long now = ServerTime.Of(new DateTime(2026, 1, 1, 0, 0, 0)).ToUnixTimeMilliseconds();
        var impoundDate = new DateTime(2026, 1, 15, 0, 0, 0);
        SystemClock.UseSource(() => now);
        try
        {
            var service = new HouseMaintenanceTimingService();
            Assert.Equal(HouseMaintenanceTimingService.FirstOverdueMailSender, service.GetMaintenanceMailSenderName(impoundDate));
            now += (long)TimeSpan.FromDays(7).TotalMilliseconds;
            Assert.Equal(HouseMaintenanceTimingService.SecondOverdueMailSender, service.GetMaintenanceMailSenderName(impoundDate));
            now += (long)TimeSpan.FromDays(7).TotalMilliseconds;
            Assert.Equal(HouseMaintenanceTimingService.FinalOverdueMailSender, service.GetMaintenanceMailSenderName(impoundDate));
        }
        finally { SystemClock.UseSystemClock(); }
    }
}
