using Aion.Bots.Dashboard;
using Aion.Bots.Navigation;
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
	public async Task NaturalCombatWaitsForNativeReadinessRefusalThenKillsTheObservedTarget()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		using var policy = NewPolicy("RC11CastReadiness", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		CancellationToken token = timeout.Token;
		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "rc11-cast-readiness";
		string path = Path.Combine(RealStaticData.RepoRoot(), "run", $"{run}.cast-readiness.trace.jsonl");
		using var trace = BotActionTraceWriter.Open(path, run, "b01", "sim-player-250",
			virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 250, "Asimcastwait", Race.ASMODIANS, trace, path);
		var dashboard = new LiveBotDashboardState();
		await using var host = new LiveBotDashboardHost(run, ["RC-11"], dashboard,
			int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT") ?? "17880"));
		session.Dashboard = dashboard;
		if (host.Enabled) Console.WriteLine($"RC-11 cast-readiness probe dashboard: {host.Url}");
		var probe = new DestinyProbe(this, fixture, session, token);
		await probe.InitializeAsync(); // Labelled disposable-probe class/skills/prerequisites only.
		await probe.SetupAtFortressAsync();
		// Earlier Fast probes clear neighbours in this shared map. Give this probe
		// its own full-HP native source at a shipped spot, without changing server data.
		var group = fixture.DataManager.StaticData.SpawnsDh.GetSpawnsByWorldId(220030000).First(g => g.GetNpcId() == 210527);
		var shipped = group.GetSpawnTemplates().First();
		var spawn = Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(220030000, 210527,
			shipped.GetX(), shipped.GetY(), shipped.GetZ(), shipped.GetHeading());
		var target = Assert.IsType<Npc>(Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(spawn, probe.Server.GetInstanceId()), exactMatch: false);
		Console.WriteLine($"RC-11 labelled probe-only source {target.GetObjectId()} at its shipped spot, native HP {target.GetLifeStats().GetCurrentHp()}.");
		BotPosition position = new(target.GetX(), target.GetY(), target.GetZ(), 0);
		var geometry = BotNavigationGeometry.ForServerWorld(probe.Server.GetInstanceId(), Race.ASMODIANS);
		BotPosition spot = geometry.GroundAround(220030000, position, [15f]).First();
		foreach (var npc in probe.Server.GetWorldMapInstance().GetNpcs().Where(n => n != target && !n.IsDead() &&
			NaturalHostility.IsAggressive(n.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
			NaturalFlightPolicy.Distance(spot, new(n.GetX(), n.GetY(), n.GetZ(), 0)) < 100).ToArray())
			fixture.World.Despawn(npc);
		Console.WriteLine("RC-11 labelled setup clears neighbours, teleports free probe 250 and sets its native animation gate; no natural progress changes.");
		session.Api.World.BeginWorldReload();
		await TeleportForSetupAsync(session, probe.Server, 220030000, spot.X, spot.Y, spot.Z, token);
		session.AcceptTeleportPosition();
		await session.SynchronizeAsync(token);
		Assert.Contains(target.GetObjectId(), session.Api.World.Objects.Keys);
		int packetStart = session.PacketHistory.Count;
		NaturalCombatDiagnosticResult result = await new NaturalIshalgenJourney(session, probe.Runtime, new()).RunObservedCombatAsync(_ =>
		{
			session.BeginStep("probe-native-readiness", "labelled-native-animation-gate-before-ordinary-combat");
			packetStart = session.PacketHistory.Count;
			probe.Server.SetNextSkillUse(fixture.Epoch.AddMilliseconds(fixture.Clock.NowMillis + 900).ToUnixTimeMilliseconds());
			return Task.FromResult(target.GetObjectId());
		}, token);
		var packets = session.PacketHistory.Skip(packetStart).ToArray();
		Assert.Contains(packets, packet => packet.PacketType == typeof(SM_SYSTEM_MESSAGE) &&
			packet.Get<object>("name") is "STR_SKILL_NOT_READY");
		Assert.Contains(packets, packet => packet.PacketType == typeof(SM_CASTSPELL_RESULT) &&
			packet.Get<int>("effectorId") == session.CharacterId);
		Assert.True(result.Killed);
		Assert.True(target.IsDead());
		Assert.Equal(0, result.Deaths);
		Console.WriteLine($"RC-11 native not-ready recovery: {result}; actual target {target.GetObjectId()} is dead.");
		policy.AssertClean();
	}
}
