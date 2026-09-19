using Aion.GameServer.Model.Account;
using Aion.GameServer.Utils.Time;

namespace Aion.GameServer.Tests;

public sealed class PassportAttendanceDayTests
{
	[Theory]
	[InlineData(0, 0, 0, 15)]
	[InlineData(8, 59, 59, 15)]
	[InlineData(9, 0, 0, 16)]
	[InlineData(23, 59, 59, 16)]
	public void DuplicateDetectionUsesTheNineOClockAttendanceDay(int hour, int minute, int second, int attendDay)
	{
		var timestamp = ServerTime.Of(new DateTime(2020, 12, 16, hour, minute, second)).UtcDateTime;
		var list = new PassportsList(); list.AddPassport(new Passport(346, true, timestamp));
		Assert.True(list.HasPassportForDay(346, new DateOnly(2020, 12, attendDay)));
		Assert.False(list.HasPassportForDay(346, new DateOnly(2020, 12, attendDay == 15 ? 16 : 15)));
		Assert.False(list.HasPassportForDay(345, new DateOnly(2020, 12, attendDay)));
	}
}
