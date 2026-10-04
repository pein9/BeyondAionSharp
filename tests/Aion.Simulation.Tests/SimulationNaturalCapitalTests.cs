using Aion.Bots.Dashboard;
using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.Scenarios;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.TestKit;
using Aion.GameServer.Utils;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>PC-02: supplied request, real manual use and D32 artisan report; no crafting setup.</summary>
	[SkippableFact]
	public async Task CapitalSupplyManualAndArtisansConsumeTheirActualQuestItemsAndPayRewards()
	{
		await RunCapitalProbeAsync("PC02", 241, "Asimcapitems", async (probe, session, token) =>
		{
			long xp = probe.Server.GetCommonData().GetExp(), kinah = session.Api.World.Kinah;
			foreach (NaturalAltgardStep step in NaturalCapitalSteps.Supply) await probe.TalkAsync(step);
			Assert.DoesNotContain(session.Api.World.Inventory.Values, item => item.ItemId == 182207039);
			await probe.TalkAsync(NaturalCapitalSteps.BookOffer);
			await NaturalCapitalSteps.ReadBookAsync(session, fixture.DataManager.StaticData.ItemDataDh.GetItemTemplate(182212217), token);
			Assert.Contains(session.Api.World.Inventory.Values, item => item.ItemId == 182212217);
			await probe.TalkAsync(NaturalCapitalSteps.BookReward);
			Assert.DoesNotContain(session.Api.World.Inventory.Values, item => item.ItemId == 182212217);
			Assert.Contains(session.Api.World.Inventory.Values, item => item.ItemId == 188508000);
			foreach (NaturalAltgardStep step in NaturalCapitalSteps.Artisans) await probe.TalkAsync(step);
			Assert.True(session.Api.World.CompletedQuestIds.IsSupersetOf(new[] { 2953, 29048, 2929 }));
			Assert.Equal(12885, probe.Server.GetCommonData().GetExp() - xp);
			Assert.Equal(4340, session.Api.World.Kinah - kinah);
			Assert.False(session.Api.World.CompletedQuestIds.Contains(29049));
			Console.WriteLine("PC-02: three completions, XP +12885, Kinah +4340, supplied items consumed, optional motion item retained.");
		});
	}

	private async Task RunCapitalProbeAsync(string item, int account, string name,
		Func<CapitalProbe, SimulationL0Session, CancellationToken, Task> runProbe)
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		using var policy = NewPolicy(item, includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(12));
		CancellationToken token = timeout.Token;
		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? item.ToLowerInvariant();
		string path = Path.Combine(RealStaticData.RepoRoot(), "run", "capital-pass", run + "-" + item.ToLowerInvariant() + ".trace.jsonl");
		using var trace = BotActionTraceWriter.Open(path, run, "b01", $"sim-player-{account}", virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		await using var session = new SimulationL0Session(fixture, policy, "b01", account, name, Race.ASMODIANS, trace, path);
		var dashboard = new LiveBotDashboardState();
		await using var monitor = new LiveBotDashboardHost(run, [item], dashboard,
			int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT") ?? "17880"));
		session.Dashboard = dashboard;
		var probe = new CapitalProbe(this, fixture, session, token);
		await probe.InitializeAsync();
		await runProbe(probe, session, token);
		policy.AssertClean();
	}

	/// <summary>PC-01: free account 240, checked city circuit and actual Convent statues; all setup stays in this probe.</summary>
	[SkippableFact]
	public async Task CapitalPassWalksEveryCityAreaAndReturnsThroughConventStatues()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		using var policy = NewPolicy("PC01", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(12));
		CancellationToken token = timeout.Token;
		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "pc01-travel";
		string path = Path.Combine(RealStaticData.RepoRoot(), "run", "capital-pass", run + ".trace.jsonl");
		using var trace = BotActionTraceWriter.Open(path, run, "b01", "sim-player-240", virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 240, "Asimcapital", Race.ASMODIANS, trace, path);
		var dashboard = new LiveBotDashboardState();
		await using var monitor = new LiveBotDashboardHost(run, ["PC-01"], dashboard,
			int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT") ?? "17880"));
		session.Dashboard = dashboard;
		var probe = new CapitalProbe(this, fixture, session, token);
		await probe.InitializeAsync();
		foreach (int npc in NaturalCapitalContract.RouteNpcIds)
		{
			int target = await probe.WalkNpcAsync(npc);
			if (probe.Contract.Portals.FirstOrDefault(portal => portal.NpcId == npc) is { } portal)
				await NaturalCapitalSteps.PortalAsync(session, portal, target, token);
		}
		Assert.Equal(120010000, session.Api.World.MapId);
		Assert.Equal(probe.IncomingBind, session.Api.World.ObeliskBindPoint);
		Assert.Equal(10, session.Api.World.Level);
		Console.WriteLine($"PC-01: {probe.Walked} checked NPC approaches, two real statue trips, bind retained.");
		policy.AssertClean();
	}

	private sealed class CapitalProbe(SimulationFastScenarioTests owner, SimulationWorldFixture fixture,
		SimulationL0Session session, CancellationToken token)
	{
		public NaturalCapitalContract Contract { get; } = NaturalCapitalContract.LoadDefault();
		public Player Server => fixture.World.GetPlayer(session.CharacterId);
		public BotBindPoint? IncomingBind { get; private set; }
		public int Walked { get; private set; }
		private readonly NaturalCapitalTravel travel = new(session, RealStaticData.RepoRoot(), () => fixture.Clock.NowMillis);
		public async Task InitializeAsync()
		{
			session.BeginStep("pc-setup", "controlled-level-10-cleric-setup-on-free-account");
			await session.LoginAndAuthenticateAsync(token);
			await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
			await session.EnterWorldAsync(token);
			await session.SynchronizeAsync(token);
			Assert.True(ClassChangeService.SetClass(Server, PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
			Server.GetCommonData().SetLevel(10);
			SkillLearnService.LearnNewSkills(Server, 1, 10);
			Server.GetInventory().IncreaseKinah(10000);
			foreach (int id in Contract.RequiredCompletedQuestIds)
			{
				QuestState? state = Server.GetQuestStateList().GetQuestState(id);
				if (state == null) Assert.True(Server.GetQuestStateList().AddQuest(id, new QuestState(id, QuestStatus.COMPLETE)));
				else state.SetStatus(QuestStatus.COMPLETE);
			}
			QuestState? dispatch = Server.GetQuestStateList().GetQuestState(Contract.DispatchQuestId);
			if (dispatch == null) Assert.True(Server.GetQuestStateList().AddQuest(Contract.DispatchQuestId, new QuestState(Contract.DispatchQuestId, QuestStatus.START)));
			else { dispatch.SetStatus(QuestStatus.START); dispatch.SetQuestVar(0); }
			PacketSendUtility.SendPacket(Server, new SM_QUEST_COMPLETED_LIST(0,
				Contract.RequiredCompletedQuestIds.Select(id => Server.GetQuestStateList().GetQuestState(id)).ToList()));
			await SetupNearAsync(Contract.MapId, 204079);
			IncomingBind = session.Api.World.ObeliskBindPoint;
		}

		public async Task SetupNearAsync(int map, int npcId)
		{
			var instance = fixture.World.GetWorldMap(map).GetMainWorldMapInstance();
			var npc = instance.GetNpcs(npcId).First(n => !n.IsDead());
			BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(instance.GetInstanceId(), Race.ASMODIANS);
			BotPosition at = geometry.GroundAround(map, new(npc.GetX(), npc.GetY(), npc.GetZ(), 0), [2f, 3f, 5f]).First(point => point != default);
			ClearHostiles(instance.GetNpcs(), [at]);
			session.Api.World.BeginWorldReload();
			await owner.TeleportForSetupAsync(session, Server, map, at.X, at.Y, at.Z, token);
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(token);
		}

		public async Task TalkAsync(NaturalAltgardStep step)
		{
			await SetupNearAsync(step.MapId ?? Contract.MapId, step.NpcId);
			int npc = await WalkNpcAsync(step.NpcId);
			session.BeginStep(step.Key, "capital-quest-dialog");
			Console.WriteLine("PC " + await NaturalAltgardQuestSteps.TalkAsync(session, step, npc, token));
		}

		public async Task<int> WalkNpcAsync(int npcId)
		{
			session.BeginStep($"pc-walk-{++Walked:00}", $"walk-to-{npcId}");
			var npc = Server.GetWorldMapInstance().GetNpcs(npcId).Where(n => !n.IsDead()).OrderBy(n =>
				NaturalFlightPolicy.Distance(session.CurrentPosition, new(n.GetX(), n.GetY(), n.GetZ(), 0))).First();
			int map = Server.GetWorldId();
			BotPosition from = session.CurrentPosition, to = new(npc.GetX(), npc.GetY(), npc.GetZ(), 0);
			float range = Math.Min(5, npc.GetObjectTemplate().GetTalkDistance());
			BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(Server.GetInstanceId(), Race.ASMODIANS);
			IReadOnlyList<BotPosition> route = [];
			foreach (BotPosition at in geometry.GroundAround(map, to, [Math.Max(1, range - 1), 2f, 3f])
				.Where(at => NaturalFlightPolicy.Distance(at, to) <= range - 0.5f).OrderBy(at => NaturalFlightPolicy.Distance(from, at)))
			{
				route = geometry.FindJourneyPath(map, from, at);
				if (route.Count > 0) break;
			}
			if (route.Count == 0 && await travel.ConnectColiseumAsync(geometry, to, token))
			{
				from = session.CurrentPosition;
				route = geometry.FindInteractionPath(map, from, to);
			}
			Assert.True(route.Count > 0 || NaturalFlightPolicy.Distance(from, to) <= range,
				$"NPC {npcId}: no checked route ({BotNavMeshRouter.LastOutcome}) {from} -> {to}");
			ClearHostiles(Server.GetWorldMapInstance().GetNpcs(), route.Append(from).Append(to));
			if (route.Count > 0) await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing)
				.CreateGroundPlan(route, from, session.Api.World.MovementSpeed!.Value), token);
			await session.SynchronizeAsync(token);
			float miss = NaturalFlightPolicy.Distance(new(Server.GetX(), Server.GetY(), Server.GetZ(), 0), to);
			Assert.True(miss <= range, $"NPC {npcId}: missed ordinary talk range by {miss:F1} m");
			Console.WriteLine($"PC route {npcId}: map {map}, {route.Count} points, miss {miss:F1} m");
			return await session.WaitForNpcAsync(npcId, token);
		}

		private void ClearHostiles(IEnumerable<Aion.GameServer.Model.GameObjects.Npc> npcs, IEnumerable<BotPosition> points)
		{
			BotPosition[] spots = points.ToArray();
			foreach (var npc in npcs.Where(n => !n.IsDead() && NaturalHostility.IsAggressive(n.GetObjectTemplate(),
				fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) && spots.Any(at =>
					MathF.Pow(n.GetX() - at.X, 2) + MathF.Pow(n.GetY() - at.Y, 2) <= 900)).ToArray())
				fixture.World.Despawn(npc);
		}
	}
}
