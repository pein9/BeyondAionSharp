using Aion.Bots.Dashboard;
using Aion.Bots.Navigation;
using Aion.Bots.Movement;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	[SkippableFact]
	public async Task NativeCampaignZoneReplansAfterThreeOrdinaryGroundDisplacements()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		using var policy = NewPolicy("RC11ZoneProgress", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
		CancellationToken token = timeout.Token;
		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "rc11-zone-progress";
		string path = Path.Combine(RealStaticData.RepoRoot(), "run", $"{run}.zone-progress.trace.jsonl");
		using var trace = BotActionTraceWriter.Open(path, run, "b01", "sim-player-255",
			virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 255, "Asimzoneprog", Race.ASMODIANS, trace, path);
		var dashboard = new LiveBotDashboardState();
		await using var host = new LiveBotDashboardHost(run, ["RC-11"], dashboard,
			int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT") ?? "17880"));
		session.Dashboard = dashboard;
		if (host.Enabled) Console.WriteLine($"RC-11 zone progress probe dashboard: {host.Url}");
		var probe = new DestinyProbe(this, fixture, session, token);
		await probe.InitializeAsync();
		await probe.SetupAtFortressAsync();
		BotPosition start = new(1720.87f, 2169.27f, 256.63812f, 80);
		BotPosition[] partials = [new(1916.1058f,2266.4214f,302.25f,0), new(2033.3772f,2546.1863f,344.50787f,0), new(1922.0612f,2391.2627f,305.97858f,0)];
		NaturalAltgardZoneStep zone = Assert.Single(NaturalAltgardContract.LoadLeg("l10").ZoneStepList);
		BotPosition center = new(zone.Anchor![0], zone.Anchor[1], zone.Anchor[2], 0);
		foreach (var npc in probe.Server.GetWorldMapInstance().GetNpcs().Where(n => !n.IsDead() &&
			NaturalHostility.IsAggressive(n.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
			partials.Append(start).Append(center).Any(at => NaturalFlightPolicy.Distance(at,new(n.GetX(),n.GetY(),n.GetZ(),0)) < 120)).ToArray())
			fixture.World.Despawn(npc);
		QuestState quest = probe.Server.GetQuestStateList().GetQuestState(24015);
		quest.SetStatus(QuestStatus.START); quest.SetQuestVar(1);
		PacketSendUtility.SendPacket(probe.Server,new SM_QUEST_ACTION(SM_QUEST_ACTION.ActionType.ADD,quest));
		session.Api.World.BeginWorldReload();
		await TeleportForSetupAsync(session,probe.Server,220030000,start.X,start.Y,start.Z,token);
		session.AcceptTeleportPosition();
		await session.SynchronizeAsync(token);
		Console.WriteLine("RC-11 free probe 255: labelled START/1 setup and cleared course; three recorded displacement positions reached through ordinary checked walking, no further setup teleports.");
		BotNavigationGeometry geometry = probe.Runtime.CreateGeometry();
		int displacements = 0;
		Assert.True(await new NaturalIshalgenJourney(session,probe.Runtime,new()).RunObservedZoneApproachAsync(center,[],token,
			async ct =>
			{
				if (displacements == partials.Length) return;
				BotPosition next = partials[displacements++];
				IReadOnlyList<BotPosition> walk = geometry.FindJourneyPathAvoiding(220030000,session.CurrentPosition,next,[new(center,15)]);
				Assert.NotEmpty(walk);
				await session.ExecuteMovementAsync(new BotMover(session.Api.World,session.Api.Timing)
					.CreateGroundPlan(walk,session.CurrentPosition,session.Api.World.MovementSpeed!.Value),ct);
				await session.SynchronizeAsync(ct);
				Assert.Equal(1,quest.GetQuestVarById(0));
				Console.WriteLine($"RC-11 ordinary displacement {displacements}: {session.CurrentPosition}; quest remains START/1.");
			}));
		Assert.Equal(3,displacements);
		Assert.Equal(2,quest.GetQuestVarById(0));
		Assert.Equal(2,session.Api.World.Quests[24015].StepAndFlags);
		Assert.True(NaturalFlightPolicy.Distance(center,session.CurrentPosition) < zone.Radius);
		Assert.False(probe.Server.IsDead());
		Assert.DoesNotContain(session.PacketHistory,p => p.PacketType == typeof(SM_DIE));
		Console.WriteLine("RC-11 native zone entry succeeds after three partial ground approaches and stale-route replans; zero deaths.");
		policy.AssertClean();
	}

	[SkippableFact]
	public async Task NativeCampaignZoneRouteAvoidsTheRecordedDeathSpot()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		using var policy = NewPolicy("RC11Zone", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
		CancellationToken token = timeout.Token;
		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "rc11-zone";
		string path = Path.Combine(RealStaticData.RepoRoot(), "run", $"{run}.zone.trace.jsonl");
		using var trace = BotActionTraceWriter.Open(path, run, "b01", "sim-player-254",
			virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 254, "Asimzonepath", Race.ASMODIANS, trace, path);
		var dashboard = new LiveBotDashboardState();
		await using var host = new LiveBotDashboardHost(run, ["RC-11"], dashboard,
			int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT") ?? "17880"));
		session.Dashboard = dashboard;
		if (host.Enabled) Console.WriteLine($"RC-11 campaign-zone probe dashboard: {host.Url}");
		var probe = new DestinyProbe(this, fixture, session, token);
		await probe.InitializeAsync();
		await probe.SetupAtFortressAsync();
		BotPosition start = new(1925.9523f, 2351.807f, 301.69742f, 107);
		BotPosition death = new(1945.9756f, 2354.0061f, 308.7473f, 0);
		NaturalAltgardZoneStep zone = Assert.Single(NaturalAltgardContract.LoadLeg("l10").ZoneStepList);
		BotPosition center = new(zone.Anchor![0], zone.Anchor[1], zone.Anchor[2], 0);
		foreach (var npc in probe.Server.GetWorldMapInstance().GetNpcs().Where(n => !n.IsDead() &&
			NaturalHostility.IsAggressive(n.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
			NaturalFlightPolicy.Distance(start, new(n.GetX(), n.GetY(), n.GetZ(), 0)) < 120).ToArray())
			fixture.World.Despawn(npc);
		Console.WriteLine("RC-11 free probe 254: controlled Q24015 START/1, recorded position and remembered death spot; cleared live hostiles; ordinary movement proves zone entry.");
		QuestState quest = probe.Server.GetQuestStateList().GetQuestState(24015);
		quest.SetStatus(QuestStatus.START); quest.SetQuestVar(1);
		PacketSendUtility.SendPacket(probe.Server, new SM_QUEST_ACTION(SM_QUEST_ACTION.ActionType.ADD, quest));
		session.Api.World.BeginWorldReload();
		await TeleportForSetupAsync(session, probe.Server, 220030000, start.X, start.Y, start.Z, token);
		session.AcceptTeleportPosition();
		await session.SynchronizeAsync(token);
		Assert.Equal(1, quest.GetQuestVarById(0));
		BotNavigationGeometry geometry = probe.Runtime.CreateGeometry();
		BotPosition goal = geometry.GroundAround(220030000, center, [3f, 5f, 8f, 12f]).First();
		IReadOnlyList<BotPosition> oldRoad = geometry.FindJourneyPath(220030000, start, goal);
		Assert.NotEmpty(oldRoad);
		Assert.False(BotNavigationGeometry.AvoidsHazards(start, oldRoad, [new(death, 20)]));
		int packetStart = session.PacketHistory.Count;
		Assert.True(await new NaturalIshalgenJourney(session, probe.Runtime, new())
			.RunObservedZoneApproachAsync(center, [death], token));
		Assert.Equal(2, quest.GetQuestVarById(0));
		Assert.Equal((byte)3, session.Api.World.Quests[24015].Status);
		Assert.Equal(2, session.Api.World.Quests[24015].StepAndFlags);
		Assert.True(NaturalFlightPolicy.Distance(center, session.CurrentPosition) < zone.Radius);
		Assert.False(probe.Server.IsDead());
		Assert.DoesNotContain(session.PacketHistory.Skip(packetStart), p => p.PacketType == typeof(SM_DIE));
		Assert.Contains(session.PacketHistory.Skip(packetStart), p => p.PacketType == typeof(SM_QUEST_ACTION) && p.Get<int>("questId") == 24015);
		Console.WriteLine($"RC-11 old road crossed the remembered death spot; shared checked detour enters the native sphere and advances Q24015 1->2 at {session.CurrentPosition}.");
		policy.AssertClean();
	}
}
