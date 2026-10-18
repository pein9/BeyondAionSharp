using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Time;

namespace Aion.GameServer.Tests;

/// <summary>
/// InstanceCooltimeData.calculateInstanceEntranceCooltime's daily and weekly entry resets in a zone with daylight saving.
/// Java resolves the reset's local time with ZonedDateTime.with(LocalTime) and plusDays, which apply the zone's rules at the
/// reset; the port kept the offset of the moment of entry, an hour off when a change falls between the two.
/// </summary>
[Collection("GoldenDataManager")]
public sealed class InstanceCooltimeParityTests
{
	private const int DailyWorld = 300080000, WeeklyWorld = 300590000, GapWorld = 1, OverlapWorld = 2;

	[Fact]
	public void DailyResetAfterTheClocksGoBackKeepsItsLocalTimeAsJavaZonedDateTimeDoes()
	{
		// Saturday 2026-10-31 10:00 EDT; the clocks go back at 02:00 on Sunday. Java: Sunday 09:00 EST, 14:00 UTC.
		Assert.Equal(new DateTimeOffset(2026, 11, 1, 14, 0, 0, TimeSpan.Zero), Reset(DailyWorld, new DateTimeOffset(2026, 10, 31, 14, 0, 0, TimeSpan.Zero)));
		// Saturday 2026-03-07 10:00 EST; the clocks go forward at 02:00 on Sunday. Java: Sunday 09:00 EDT, 13:00 UTC.
		Assert.Equal(new DateTimeOffset(2026, 3, 8, 13, 0, 0, TimeSpan.Zero), Reset(DailyWorld, new DateTimeOffset(2026, 3, 7, 15, 0, 0, TimeSpan.Zero)));
		// Without a change between them nothing moves: Wednesday 08:00 EDT resets at 09:00 EDT the same day.
		Assert.Equal(new DateTimeOffset(2026, 10, 28, 13, 0, 0, TimeSpan.Zero), Reset(DailyWorld, new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero)));
	}

	[Fact]
	public void WeeklyResetAcrossTheChangeKeepsItsLocalTimeAsJavaZonedDateTimeDoes()
	{
		// Wednesday 2026-10-28 10:00 EDT: tomorrow 09:00, then on to Wednesday 2026-11-04 09:00 EST, 14:00 UTC.
		Assert.Equal(new DateTimeOffset(2026, 11, 4, 14, 0, 0, TimeSpan.Zero), Reset(WeeklyWorld, new DateTimeOffset(2026, 10, 28, 14, 0, 0, TimeSpan.Zero)));
	}

	[Fact]
	public void ResetsInsideAGapOrAnOverlapResolveAsJavaZonedDateTimeOfLocalDoes()
	{
		// 02:30 does not exist on 2026-03-08: Java moves it later by the gap's length, 03:30 EDT, 07:30 UTC.
		Assert.Equal(new DateTimeOffset(2026, 3, 8, 7, 30, 0, TimeSpan.Zero), Reset(GapWorld, new DateTimeOffset(2026, 3, 7, 15, 0, 0, TimeSpan.Zero)));
		// 01:30 happens twice on 2026-11-01: Java keeps the preferred offset of the day before (EDT), 05:30 UTC.
		Assert.Equal(new DateTimeOffset(2026, 11, 1, 5, 30, 0, TimeSpan.Zero), Reset(OverlapWorld, new DateTimeOffset(2026, 10, 31, 14, 0, 0, TimeSpan.Zero)));
	}

	private static DateTimeOffset Reset(int worldId, DateTimeOffset now)
	{
		var data = new InstanceCooltimeData
		{
			instanceCooltime =
			[
				Cooltime(DailyWorld, InstanceCoolTimeType.DAILY, null, 900),
				Cooltime(WeeklyWorld, InstanceCoolTimeType.WEEKLY, "Wed", 900),
				Cooltime(GapWorld, InstanceCoolTimeType.DAILY, null, 230),
				Cooltime(OverlapWorld, InstanceCoolTimeType.DAILY, null, 130),
			],
		};
		data.AfterUnmarshal(null!);
		ServerTime.Initialize(CreateEasternTestZone());
		SystemClock.UseSource(() => now.ToUnixTimeMilliseconds());
		try
		{
			return DateTimeOffset.FromUnixTimeMilliseconds(data.CalculateInstanceEntranceCooltime(Player(), worldId));
		}
		finally
		{
			SystemClock.UseSystemClock();
			ServerTime.Initialize(TimeZoneInfo.Local);
		}
	}

	private static InstanceCooltime Cooltime(int worldId, InstanceCoolTimeType type, string? typeValue, int entCoolTime) => new()
	{
		worldId = worldId, syncId = worldId, coolTimeType = type, typevalue = typeValue!, entCoolTime = entCoolTime, maxCount = 1,
		maxMemberLight = 6, maxMemberDark = 6,
	};

	private static Player Player()
	{
		var common = new PlayerCommonData(1);
		common.SetName("Cooltimer");
		common.SetRace(Race.ASMODIANS);
		common.SetPlayerClass(PlayerClass.GLADIATOR);
		var account = new Account(10_001);
		account.SetName("cooltimer");
		return new Player(new PlayerAccountData(common, new PlayerAppearance()), account);
	}

	/// <summary>US Eastern rules for 2020 to 2030: -5, daylight saving from the second Sunday of March to the first Sunday of
	/// November at 02:00.</summary>
	private static TimeZoneInfo CreateEasternTestZone()
	{
		TimeZoneInfo.TransitionTime starts = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 2, DayOfWeek.Sunday);
		TimeZoneInfo.TransitionTime ends = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 11, 1, DayOfWeek.Sunday);
		TimeZoneInfo.AdjustmentRule rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2020, 1, 1), new DateTime(2030, 12, 31),
			TimeSpan.FromHours(1), starts, ends);
		return TimeZoneInfo.CreateCustomTimeZone("P11-01-Eastern", TimeSpan.FromHours(-5), "P11-01 Eastern", "P11-01 Eastern Standard",
			"P11-01 Eastern Daylight", [rule]);
	}
}
