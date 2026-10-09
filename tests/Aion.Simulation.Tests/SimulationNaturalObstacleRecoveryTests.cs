using Aion.Bots.Dashboard;
using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	[SkippableFact]
	public async Task NativeObstacleRecoveryDetoursAroundUnsafeCloseInGround()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		using var policy = NewPolicy("RC11Obstacle", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
		CancellationToken token = timeout.Token;
		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "rc11-obstacle";
		string path = Path.Combine(RealStaticData.RepoRoot(), "run", $"{run}.obstacle.trace.jsonl");
		using var trace = BotActionTraceWriter.Open(path, run, "b01", "sim-player-253",
			virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 253, "Asimobstacle", Race.ASMODIANS, trace, path);
		var dashboard = new LiveBotDashboardState();
		await using var host = new LiveBotDashboardHost(run, ["RC-11"], dashboard,
			int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT") ?? "17880"));
		session.Dashboard = dashboard;
		if (host.Enabled) Console.WriteLine($"RC-11 obstacle probe dashboard: {host.Url}");
		var probe = new DestinyProbe(this, fixture, session, token);
		await probe.InitializeAsync(); // Labelled disposable class/skills/prerequisites; no natural subject.
		await probe.SetupAtFortressAsync();
		BotPosition start = new(2414.4233f, 2185.2192f, 268.68567f, 72);
		foreach (var npc in probe.Server.GetWorldMapInstance().GetNpcs().Where(n => !n.IsDead() &&
			NaturalHostility.IsAggressive(n.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
			NaturalFlightPolicy.Distance(start, new(n.GetX(), n.GetY(), n.GetZ(), 0)) < 120).ToArray())
			fixture.World.Despawn(npc);
		Npc caster = Spawn(210538, new(2400.88f, 2171.88f, 270.328f, 2));
		Npc blocker = Spawn(210535, new(2406.3f, 2176.8298f, 270.08313f, 0));
		Console.WriteLine("RC-11 free probe 253: cleared shipped camp, own full-HP warlock and an observed hostile on the direct close-in step; setup teleport only.");
		session.Api.World.BeginWorldReload();
		await TeleportForSetupAsync(session, probe.Server, 220030000, start.X, start.Y, start.Z, token);
		session.AcceptTeleportPosition();
		await session.SynchronizeAsync(token);
		int packetStart = session.PacketHistory.Count;
		// The Priest's ranks of Smite, as before NR-18 took the hand-typed table away: the catalog is the Priest's.
		var smite = NaturalPriestSkills.Best("smite", session.Api.World.Level, session.Api.World.Skills,
			Aion.Bots.Scenarios.Classes.NaturalClassProfiles.For(PlayerClass.PRIEST.GetClassId(),
				Aion.Bots.Scenarios.Classes.NaturalClassLine.Default, fixture.DataManager.StaticData).Skills)!;
		await session.SendPacketAsync(session.Api.Target(caster.GetObjectId()), token);
		await session.SendPacketAsync(session.Api.Cast(probe.Runtime.CreateSpellCast(session.Api.World,
			session.CurrentPosition, smite.Id, checked((byte)session.Api.World.Skills[smite.Id].Level), caster.GetObjectId())), token);
		var refused = await BotCastProtocol.WaitForStartAsync((predicate, waitToken) => session.WaitForPacketAsync(
			p => predicate(p) || BotCastProtocol.IsStartRejection(p), waitToken), session.CharacterId, smite.Id, token);
		Assert.Equal(typeof(SM_SYSTEM_MESSAGE), refused.PacketType);
		Assert.Equal("STR_SKILL_OBSTACLE", refused.Get<object>("name"));
		session.Api.Timing.RecordCastCancelled();
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(351), token);
		var completed = session.Api.World.CompletedQuestCounts.OrderBy(p => p.Key).ToArray();
		var result = await new NaturalIshalgenJourney(session, probe.Runtime, new()).RunObservedCombatAsync(
			_ => Task.FromResult(caster.GetObjectId()), token, avoidHostileAggro: true);
		Assert.True(result.Killed);
		Assert.True(caster.IsDead());
		Assert.Equal(0, result.Deaths);
		Assert.False(blocker.IsDead());
		Assert.True(NaturalFlightPolicy.Distance(start, session.CurrentPosition) > 2);
		Assert.True(session.PacketHistory.Skip(packetStart).Count(p => p.PacketType == typeof(SM_SYSTEM_MESSAGE) &&
			p.Get<object>("name") is "STR_SKILL_OBSTACLE") >= 2, "The shared combat path did not receive its own native obstacle refusal.");
		Assert.Contains(session.PacketHistory.Skip(packetStart), p => p.PacketType == typeof(SM_CASTSPELL_RESULT) &&
			p.Get<int>("effectorId") == session.CharacterId && p.Get<int>("targetId") == caster.GetObjectId());
		Assert.Equal(completed, session.Api.World.CompletedQuestCounts.OrderBy(p => p.Key).ToArray());
		Console.WriteLine($"RC-11 native obstacle refused, checked detour moved, warlock killed normally; blocker remains alive, {result.Deaths} deaths, journal retained.");
		policy.AssertClean();

		Npc Spawn(int template, BotPosition position) => Assert.IsType<Npc>(Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(
			Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(220030000, template, position.X, position.Y, position.Z, position.Heading),
			probe.Server.GetInstanceId()), exactMatch: false);
	}
}
