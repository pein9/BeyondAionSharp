using Aion.Bots.Navigation;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Services;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>
	/// AK-00: the game clock in SIM and in the bot. The client learns the game time from SM_GAME_TIME (Java: minutes since
	/// 01.01.0000, sent on entering the world and on GameTimeService's broadcast), and the SIM's virtual clock drives the
	/// game clock, whose hour change runs TemporarySpawnEngine. Q2292's ring carriers live by the hour: MuMu Lu (210599)
	/// from 05:00 to 22:00, MuMu Zoo (210621) from 22:00 to 05:00 (Leg 5, AK-Q3). The probe watches both across a night and
	/// a morning. GM setup on the probe only: a setup teleport to MuMu Village.
	/// </summary>
	[SkippableFact]
	public async Task GameClockDrivesTheHourlyRingCarriers()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000, lu = 210599, zoo = 210621;
		using var policy = NewPolicy("AK00", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(fixture, policy, "b01", 72, "Asimclock", Race.ASMODIANS);
		session.BeginStep("s00", "login-create-enter");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		var clock = GameTimeService.GetInstance().GetGameTime();
		// The client's clock on entering the world is the server's, to the minute the packet was written.
		int? entered = session.Api.World.GameMinutes;
		Assert.NotNull(entered);
		Assert.InRange(clock.GetTime() - entered!.Value, 0, 1);
		Assert.Equal(clock.GetHour(), session.Api.World.GameHour);

		var instance = fixture.World.GetWorldMap(altgard).GetMainWorldMapInstance();
		bool Alive(int npcId) => instance.GetNpcs().Any(npc => npc.GetNpcId() == npcId && !npc.IsDead());
		bool Day(int hour) => hour >= 5 && hour < 22;
		// Advance the virtual clock (one game minute per 5 s) to the next given game hour, and let the hour change land.
		async Task AdvanceToHourAsync(int hour)
		{
			int minutes = ((hour * 60 - clock.GetTime() % 1440) + 1440) % 1440;
			if (minutes == 0) minutes = 1440;
			// In 30 s slices: one long advance can run past the virtual pool's timer-tick budget in a busy shared world.
			for (int seconds = minutes * 5 + 10; seconds > 0; seconds -= 30)
				await session.AdvanceAsync(TimeSpan.FromSeconds(Math.Min(30, seconds)), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(hour, clock.GetHour());
		}

		var seen = new List<string>();
		foreach (int hour in new[] { 22, 5, 22 })
		{
			session.BeginStep($"s-hour-{hour}", "advance-the-game-clock");
			await AdvanceToHourAsync(hour);
			bool luAlive = Alive(lu), zooAlive = Alive(zoo);
			seen.Add($"{hour:00}:00 Lu {luAlive} Zoo {zooAlive}");
			Assert.Equal(Day(hour), luAlive);
			Assert.Equal(!Day(hour), zooAlive);
		}
		// The client sees what the hour brought: teleport into the village and look.
		session.BeginStep("s-look", "the-client-sees-the-night-carrier");
		var zooNpc = instance.GetNpcs().First(npc => npc.GetNpcId() == zoo && !npc.IsDead());
		BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(instance.GetInstanceId(), Race.ASMODIANS);
		BotPosition near = geometry.GroundAround(altgard, new BotPosition(zooNpc.GetX(), zooNpc.GetY(), zooNpc.GetZ(), 0), [15f, 20f, 25f]).First();
		await TeleportForSetupAsync(session, fixture.World.GetPlayer(session.CharacterId), altgard, near.X, near.Y, near.Z, token);
		session.AcceptTeleportPosition();
		await session.SynchronizeAsync(token);
		Assert.Contains(session.Api.World.Objects.Values, known => known.TemplateId == zoo);
		Assert.DoesNotContain(session.Api.World.Objects.Values, known => known.TemplateId == lu);
		Assert.True(session.Api.World.GameTimeUpdates >= 1);
		Console.WriteLine($"AK-00 entered at game hour {entered / 60 % 24}; {string.Join("; ", seen)}; client updates {session.Api.World.GameTimeUpdates}, " +
			$"client hour {session.Api.World.GameHour}, server hour {clock.GetHour()}");
		policy.AssertClean();
	}
}
