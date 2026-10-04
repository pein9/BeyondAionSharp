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
	/// <summary>PC-05: proper Veldina/Balder/Kvasir progression and the actual Convent entry/return.</summary>
	[SkippableFact]
	public async Task CapitalVeldinasCallPaysThroughAngulofAndReturnsWithTheBindUnchanged()
	{
		await RunCapitalProbeAsync("PC05", 244, "Asimcapconvent", async (probe, session, token) =>
		{
			long xp = probe.Server.GetCommonData().GetExp(), kinah = session.Api.World.Kinah;
			foreach (NaturalAltgardStep step in NaturalCapitalSteps.Convent.Take(3)) await probe.TalkAsync(step);
			Assert.True(NaturalAltgardQuestSteps.State(session.Api.World, 29004) is (4, 2));
			NaturalCapitalPortal entry = probe.Contract.Portals.Single(portal => portal.MapId == 120010000);
			int statue = await probe.WalkNpcAsync(entry.NpcId);
			await NaturalCapitalSteps.PortalAsync(session, entry, statue, token);
			await probe.TalkAsync(NaturalCapitalSteps.Convent[3]);
			NaturalCapitalPortal exit = probe.Contract.Portals.Single(portal => portal.MapId == 120020000);
			statue = await probe.WalkNpcAsync(exit.NpcId);
			await NaturalCapitalSteps.PortalAsync(session, exit, statue, token);
			Assert.Contains(29004, session.Api.World.CompletedQuestIds);
			Assert.Equal(3000, probe.Server.GetCommonData().GetExp() - xp);
			Assert.Equal(9830, session.Api.World.Kinah - kinah);
			Assert.Equal(120010000, session.Api.World.MapId);
			Assert.Equal(probe.IncomingBind, session.Api.World.ObeliskBindPoint);
			Console.WriteLine("PC-05: real statue entry/return, Angulof reward, XP +3000, Kinah +9830, incoming bind retained.");
		});
	}

	/// <summary>PC-04: the actual group-0 branch, every Ribbon contact and Lost Love return.</summary>
	[SkippableFact]
	public async Task CapitalBlessingUnlocksOnlyTheSelectedRibbonBranchAndPaysLostLoveRewards()
	{
		await RunCapitalProbeAsync("PC04", 243, "Asimcapribbon", async (probe, session, token) =>
		{
			long xp = probe.Server.GetCommonData().GetExp(), kinah = session.Api.World.Kinah;
			foreach (NaturalAltgardStep step in NaturalCapitalSteps.Blessing)
			{
				await probe.TalkAsync(step);
				if (step.Key == "q2911-ribbon-branch")
				{
					Assert.Equal(0, probe.Server.GetQuestStateList().GetQuestState(2911).GetRewardGroup());
					Assert.True(QuestService.CheckStartConditions(probe.Server, 2912, false));
					Assert.False(QuestService.CheckStartConditions(probe.Server, 2913, false));
				}
			}
			Assert.True(session.Api.World.CompletedQuestIds.IsSupersetOf(new[] { 2911, 2912, 2914 }));
			Assert.DoesNotContain(2913, session.Api.World.CompletedQuestIds);
			Assert.DoesNotContain(2915, session.Api.World.CompletedQuestIds);
			Assert.Equal(15465, probe.Server.GetCommonData().GetExp() - xp);
			Assert.Equal(kinah, session.Api.World.Kinah);
			Assert.Contains(session.Api.World.Inventory.Values, item => item.ItemId == 122000870);
			Assert.Equal(2, session.Api.World.Inventory.Values.Where(item => item.ItemId == 164000074).Sum(item => item.Count));
			NaturalGearInfo? Describe(int id)
			{
				var template = fixture.DataManager.StaticData.ItemDataDh.GetItemTemplate(id);
				return template.GetItemSlot() == 0 ? null : new(template.GetItemSlot(),
					template.GetRequiredLevel(PlayerClass.CLERIC), template.GetLevel(),
					template.GetRace() is Race.PC_ALL or Race.ASMODIANS);
			}
			NaturalGearUpgrade ring = Assert.Single(NaturalGearPolicy.SelectUpgrades(session.Api.World.Inventory.Values,
				session.Api.World.Level, Describe, (long)Aion.GameServer.Model.Items.ItemSlot.MAIN_OFF_OR_SUB_OFF),
				upgrade => upgrade.ItemId == 122000870);
			await session.SendPacketAsync(session.Api.Equip(0, ring.Slot, ring.ObjectId), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(ring.Slot, session.Api.World.Inventory[ring.ObjectId].EquipmentSlot);
			Console.WriteLine("PC-04: group 0, all Ribbon/Lost Love visits, XP +15465, ring equipped by ordinary gear policy and two scrolls; group-1 branch excluded.");
		});
	}

	/// <summary>PC-03: complete the introductions with no bought, activated or summoned pet.</summary>
	[SkippableFact]
	public async Task CapitalPetIntroductionsPayAllFourItemsWithoutActivatingTheEgg()
	{
		await RunCapitalProbeAsync("PC03", 242, "Asimcappets", async (probe, session, token) =>
		{
			long xp = probe.Server.GetCommonData().GetExp(), kinah = session.Api.World.Kinah;
			Assert.Empty(probe.Server.GetPetList().GetPets());
			foreach (NaturalAltgardStep step in NaturalCapitalSteps.Pets) await probe.TalkAsync(step);
			Assert.True(session.Api.World.CompletedQuestIds.IsSupersetOf(new[] { 29040, 29044, 29045 }));
			foreach (int id in new[] { 169600066, 169600084, 169600085, 190000055 })
				Assert.Equal(1, session.Api.World.Inventory.Values.Where(item => item.ItemId == id).Sum(item => item.Count));
			Assert.Empty(probe.Server.GetPetList().GetPets());
			Assert.Equal(33711, probe.Server.GetCommonData().GetExp() - xp);
			Assert.Equal(kinah, session.Api.World.Kinah);
			Console.WriteLine("PC-03: three introductions, XP +33711, four reward items retained; no owned or summoned pet.");
		});
	}

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
			// The real ceremony chooses the Cleric's Karmic Staff (reward group 2).
			Server.GetQuestStateList().GetQuestState(2009).SetRewardGroup(2);
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
