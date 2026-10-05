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
using Aion.GameServer.Services.Items;
using Aion.GameServer.TestKit;
using Aion.GameServer.Utils;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	[SkippableFact]
	public async Task LaterCapitalRobeOfferInterceptsDeylaAfterNativeTooFarRefusal()
	{
		await RunCapitalProbeAsync("RC11Walker", 252, "Asimdeylawalk", async (probe, session, token) =>
		{
			probe.Server.GetCommonData().SetLevel(20);
			SkillLearnService.LearnNewSkills(probe.Server, 10, 20);
			await probe.SetupNearAsync(120010000, 204141);
			var deyla = probe.Server.GetWorldMapInstance().GetNpcs(204141).First(n => !n.IsDead());
			int npc = deyla.GetObjectId();
			// Remain at the original observed position while the native walker passes it.
			// Server position is read only to select this disposable probe's failure case.
			bool staleStart = false;
			for (int tick = 0; tick < 120; tick++)
			{
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(500), token);
				await session.SynchronizeAsync(token);
				BotKnownObject seen = session.Api.World.Objects[npc];
				if (seen.MoveTarget != null && NaturalFlightPolicy.Distance(session.CurrentPosition, seen.Position) < 5 &&
					NaturalFlightPolicy.Distance(session.CurrentPosition, new(deyla.GetX(), deyla.GetY(), deyla.GetZ(), 0)) > 7)
				{
					staleStart = true;
					break;
				}
			}
			Assert.True(staleStart, "Probe never observed Deyla pass her cached move start.");
			NaturalAltgardStep offer = NaturalLaterCapitalSteps.RobePreparation[0];
			session.BeginStep("probe-deyla-stale-start", "native-dialog-refusal-at-cached-move-start");
			await Assert.ThrowsAsync<NaturalDialogTooFarException>(() => NaturalAltgardQuestSteps.TalkAsync(session, offer, npc, token));
			var geometry = BotNavigationGeometry.ForServerWorld(probe.Server.GetInstanceId(), Race.ASMODIANS);
			int refusals = 1;
			for (int attempt = 0; ; attempt++)
			{
				BotPosition from = session.CurrentPosition,
					to = NaturalLaterCapitalSteps.DialogReapproachPosition(204141, session.Api.World.Objects[npc]);
				IReadOnlyList<BotPosition> route = geometry.FindJourneyPath(120010000, from, to);
				Assert.True(route.Count > 0 || NaturalFlightPolicy.Distance(from, to) <= 1, "Deyla interception had no checked route.");
				if (route.Count > 0)
					await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing)
						.CreateGroundPlan(route, from, session.Api.World.MovementSpeed!.Value), token);
				await session.SynchronizeAsync(token);
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(250), token);
				try
				{
					await NaturalAltgardQuestSteps.TalkAsync(session, offer, npc, token);
					break;
				}
				catch (NaturalDialogTooFarException) when (attempt < NaturalLaterCapitalSteps.DialogRetryLimit(204141))
				{
					refusals++;
				}
			}
			Assert.True(NaturalAltgardQuestSteps.State(session.Api.World, 2916) is (3, 0));
			Assert.False(session.Api.World.CompletedQuestIds.Contains(2916));
			await session.QuitAsync(token);
			await session.WaitForReentryAsync(token);
			await session.ReloginExistingCharacterAsync(token);
			await session.EnterWorldAsync(token);
			await session.SynchronizeAsync(token);
			Assert.True(NaturalAltgardQuestSteps.State(session.Api.World, 2916) is (3, 0));
			Assert.False(session.Api.World.CompletedQuestIds.Contains(2916));
			Console.WriteLine($"RC-11 Deyla: {refusals} native too-far refusals, checked waypoint interception, ordinary Q2916 START/0 retained through relog; no Q2916 state writes.");
		});
	}

	/// <summary>RC-11: real Neusa/book/reward packets consume only the required counts, including native surplus.</summary>
	[SkippableFact]
	public async Task LaterCapitalBookFinishConsumesRequiredCountsAndRetainsSurplusThroughRelog()
	{
		await RunCapitalProbeAsync("RC11", 248, "Asimbooksurplus", async (probe, session, token) =>
		{
			probe.Server.GetCommonData().SetLevel(22);
			SkillLearnService.LearnNewSkills(probe.Server, 10, 22);
			await probe.SetupNearAsync(120010000, 204206);
			async Task WalkedTalkAsync(NaturalAltgardStep step)
			{
				int npc = await probe.WalkNpcAsync(step.NpcId);
				for (int attempt = 0; ; attempt++)
				{
					session.BeginStep(step.Key, "walked-capital-quest-dialog");
					try
					{
						if (step.NpcId == 700212) await NaturalLaterCapitalSteps.ReadQuestBookAsync(session, step, npc, token);
						else await NaturalAltgardQuestSteps.TalkAsync(session, step, npc, token);
						return;
					}
					catch (NaturalDialogTooFarException) when (attempt < NaturalLaterCapitalSteps.DialogRetryLimit(step.NpcId))
					{
						BotPosition from = session.CurrentPosition,
							to = NaturalLaterCapitalSteps.DialogReapproachPosition(step.NpcId, session.Api.World.Objects[npc]);
						BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(probe.Server.GetInstanceId(), Race.ASMODIANS);
						IReadOnlyList<BotPosition> route = geometry.FindJourneyPath(step.MapId ?? 120010000, from, to);
						// Match the natural driver's waypoint interception: when already
						// there, wait for the walker rather than demanding a nonempty move.
						Assert.True(route.Count > 0 || NaturalFlightPolicy.Distance(from, to) <= 1,
							$"Walker {step.NpcId}: no checked reapproach {from} -> {to}");
						Console.WriteLine($"PC reapproach {step.NpcId}: {route.Count} checked points to {to}.");
						if (session.Api.OpenDialogTargetId is int dialogTarget)
							await session.SendPacketAsync(session.Api.CloseDialog(dialogTarget), token);
						if (route.Count > 0)
							await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing)
								.CreateGroundPlan(route, from, session.Api.World.MovementSpeed!.Value), token);
						await session.SynchronizeAsync(token);
						await session.AdvanceAsync(TimeSpan.FromMilliseconds(250), token);
					}
				}
			}
			Assert.True(await NaturalLaterCapitalSteps.PrepareBookAsync(session, WalkedTalkAsync));
			// Labelled probe supplies reproduce the retained full run's 3/2/2 incoming stacks.
			foreach (var (item, count) in new[] { (182207010, 3), (182207011, 2), (182207012, 2) })
				Assert.Equal(0, ItemService.AddItem(probe.Server, item, count, allowInventoryOverflow: true));
			await session.SynchronizeAsync(token);
			int sapObject = session.Api.World.Inventory.Values.Single(i => i.ItemId == 182207012).ObjectId;
			await NaturalLaterCapitalSteps.CompleteLeg7CityAsync(session, WalkedTalkAsync);
			Assert.True(session.Api.World.CompletedQuestIds.IsSupersetOf(new[] { 2919, 2959, 2984, 2954 }));
			Assert.True(NaturalAltgardQuestSteps.State(session.Api.World, 2938) is (3, 0));
			Assert.Equal(1, session.Api.World.CompletedQuestCounts[2919]);
			Assert.Equal(1, session.Api.World.Inventory[sapObject].Count);
			Assert.DoesNotContain(session.Api.World.Inventory.Values, i => i.ItemId is 182207010 or 182207011 or 182207013);
			await session.QuitAsync(token);
			await session.WaitForReentryAsync(token);
			await session.ReloginExistingCharacterAsync(token);
			await session.EnterWorldAsync(token);
			await session.SynchronizeAsync(token);
			Assert.Equal(1, session.Api.World.CompletedQuestCounts[2919]);
			Assert.Equal(1, session.Api.World.Inventory[sapObject].Count);
			await NaturalLaterCapitalSteps.CompleteLeg7CityAsync(session,
				_ => throw new InvalidOperationException("Completed book and juice must not repeat."));
			Console.WriteLine("RC-11 walked city batch: library pickup before family/dye, native 3/2/1 consumption, surplus and exact completions retained through relog.");
		});
	}

	/// <summary>RC-04: the carried heritage, actual box acceptance, D28's Annju contact and exactly one juice delivery.</summary>
	[SkippableFact]
	public async Task LaterCapitalLeg5PreparationUsesItsAwardedBoxAndRetainsTheRobeAndSingleJuiceCompletion()
	{
		await RunCapitalProbeAsync("RC04", 247, "Asimcapfive", async (probe, session, token) =>
		{
			probe.Server.GetCommonData().SetLevel(19);
			SkillLearnService.LearnNewSkills(probe.Server, 10, 19);
			await probe.SetupNearAsync(220030000, 203574);
			await NaturalLaterCapitalSteps.PickUpHeritageAsync(session, probe.TalkAsync);
			await probe.SetupNearAsync(120010000, 204108);
			await NaturalLaterCapitalSteps.PrepareLeg5CityAsync(session, probe.TalkAsync,
				fixture.DataManager.StaticData.ItemDataDh.GetItemTemplate(182207009), token);
			Assert.Contains(2917, session.Api.World.CompletedQuestIds);
			Assert.True(NaturalAltgardQuestSteps.State(session.Api.World, 2918) is (3, 0));
			Assert.True(NaturalAltgardQuestSteps.State(session.Api.World, 2916) is (3, 3));
			Assert.Contains(2954, session.Api.World.CompletedQuestIds);
			Assert.DoesNotContain(session.Api.World.Inventory.Values, i => i.ItemId is 182207008 or 182207040);
			await NaturalLaterCapitalSteps.PrepareLeg5CityAsync(session,
				_ => throw new InvalidOperationException("Prepared city contacts must not repeat."),
				fixture.DataManager.StaticData.ItemDataDh.GetItemTemplate(182207009), token);
			await probe.SetupNearAsync(220030000, 203574);
			await NaturalLaterCapitalSteps.CompleteMaternalReturnAsync(session, probe.TalkAsync);
			await session.QuitAsync(token);
			await session.WaitForReentryAsync(token);
			await session.ReloginExistingCharacterAsync(token);
			await session.EnterWorldAsync(token);
			await session.SynchronizeAsync(token);
			Assert.True(session.Api.World.CompletedQuestIds.IsSupersetOf(new[] { 2917, 2918, 2954 }));
			Assert.True(NaturalAltgardQuestSteps.State(session.Api.World, 2916) is (3, 3));
			Assert.DoesNotContain(session.Api.World.Inventory.Values, i => i.ItemId is 182207008 or 182207009 or 182207040);
			Assert.Equal(1, probe.Server.GetQuestStateList().GetQuestState(2954).GetCompleteCount());
			await NaturalLaterCapitalSteps.CompleteJuiceOnceAsync(session,
				_ => throw new InvalidOperationException("Completed repeatable juice must not start again."));
			Console.WriteLine("RC-04: correct heritage finish, actual awarded-box acceptance/hand-in, robe START/3 and one juice completion retained through relog.");
		});
	}

	/// <summary>RC-03: level gate, proper preparation, native Ampha drops, paid travel and ordinary relog.</summary>
	[SkippableFact]
	public async Task LaterCapitalBookPreparationCollectsTwoAmphaTailsAfterAcceptanceAndRetainsThemThroughRelog()
	{
		await RunCapitalProbeAsync("RC03", 246, "Asimbooktails", async (probe, session, token) =>
		{
			Assert.False(await NaturalLaterCapitalSteps.PrepareBookAsync(session,
				_ => throw new InvalidOperationException("The level gate must defer preparation.")));
			Assert.Null(NaturalAltgardQuestSteps.State(session.Api.World, 2919));
			probe.Server.GetCommonData().SetLevel(13);
			SkillLearnService.LearnNewSkills(probe.Server, 10, 13);
			Assert.Equal(0, ItemService.AddItem(probe.Server, 101500498, 1, allowInventoryOverflow: true));
			var staff = probe.Server.GetInventory().GetItems().Last(item => item.GetItemId() == 101500498);
			Assert.NotNull(probe.Server.GetEquipment().EquipItem(staff.GetObjectId(), 3));
			await probe.SetupNearAsync(120010000, 204206);
			Assert.True(await NaturalLaterCapitalSteps.PrepareBookAsync(session, probe.TalkAsync));
			Assert.True(NaturalAltgardQuestSteps.State(session.Api.World, 2919) is (3, 4));
			Assert.DoesNotContain(session.Api.World.Inventory.Values, item => item.ItemId == 182207011);
			var journey = new NaturalIshalgenJourney(session, probe.Runtime!, new());
			await new NaturalLaterCapitalBookTravel(session, probe.Runtime!, id => probe.WalkNpcAsync(id), () => { },
				async (id, objective) =>
				{
					int target = 0;
					NaturalCombatDiagnosticResult fight = await journey.RunObservedCombatAsync(async _ =>
					{
						target = await probe.WalkNpcAsync(id, NaturalPullPlanner.SpellRange);
						var actual = probe.Server.GetWorldMapInstance().GetNpcs(id).Single(n => n.GetObjectId() == target);
						Assert.Equal(actual.GetLifeStats().GetMaxHp(), actual.GetLifeStats().GetCurrentHp());
						return target;
					}, token);
					Console.WriteLine($"RC-03 native Ampha fight: {fight}");
					return fight.Killed ? target : 0;
				}, async source =>
				{
					BotPosition corpse = session.Api.World.Objects[source].Position;
					BotNavigationGeometry geometry = probe.Runtime!.CreateGeometry();
					IReadOnlyList<BotPosition> route = geometry.FindInteractionPath(220010000, session.CurrentPosition, corpse);
					Assert.NotEmpty(route);
					await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing)
						.CreateGroundPlan(route, session.CurrentPosition, session.Api.World.MovementSpeed!.Value), token);
					await NaturalAltgardQuestSteps.LootItemAsync(session, source, 182207011, token);
				}).CollectAmphaAndReturnAsync(token);
			int tailObject = session.Api.World.Inventory.Values.Single(item => item.ItemId == 182207011).ObjectId;
			await session.QuitAsync(token);
			await session.WaitForReentryAsync(token);
			await session.ReloginExistingCharacterAsync(token);
			await session.EnterWorldAsync(token);
			await session.SynchronizeAsync(token);
			Assert.Equal(120010000, session.Api.World.MapId);
			Assert.True(NaturalAltgardQuestSteps.State(session.Api.World, 2919) is (3, 4));
			Assert.Equal(2, session.Api.World.Inventory[tailObject].Count);
			Assert.DoesNotContain(2919, session.Api.World.CompletedQuestIds);
			await NaturalLaterCapitalSteps.PrepareBookAsync(session,
				_ => throw new InvalidOperationException("A resumed prepared book must not repeat dialogs."));
			Console.WriteLine("RC-03: normal preparation, two native Ampha Tails, paid transport return, START/4 and same item retained through relog.");
		});
	}

	/// <summary>RC-02: accept and carry Q2917 through its proper Altgard contacts, then relog.</summary>
	[SkippableFact]
	public async Task LaterCapitalHeritagePickupRetainsItsSuppliedItemAndFirstStepThroughRelog()
	{
		await RunCapitalProbeAsync("RC02", 245, "Asimheritage", async (probe, session, token) =>
		{
			await probe.SetupNearAsync(220030000, 203574);
			Assert.Null(NaturalAltgardQuestSteps.State(session.Api.World, 2917));
			await NaturalLaterCapitalSteps.PickUpHeritageAsync(session, probe.TalkAsync);
			int itemObject = session.Api.World.Inventory.Values.Single(item => item.ItemId == 182207008).ObjectId;
			await session.QuitAsync(token);
			await session.WaitForReentryAsync(token);
			await session.ReloginExistingCharacterAsync(token);
			await session.EnterWorldAsync(token);
			await session.SynchronizeAsync(token);
			Assert.True(NaturalAltgardQuestSteps.State(session.Api.World, 2917) is (3, 1));
			Assert.Equal(1, session.Api.World.Inventory[itemObject].Count);
			Assert.DoesNotContain(2917, session.Api.World.CompletedQuestIds);
			Assert.DoesNotContain(session.Api.World.Inventory.Values, item => item.ItemId == 182207009);
			await NaturalLaterCapitalSteps.PickUpHeritageAsync(session, _ => throw new InvalidOperationException("A resumed pickup must not repeat dialogs."));
			Console.WriteLine("RC-02: Arekedil -> Chauminerk, START/1, original supplied item retained through relog; no early reward or repeat.");
		});
	}

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
		probe.Runtime = new NaturalJourneyRuntime(RealStaticData.RepoRoot(), "SIM-" + item.ToLowerInvariant(), fixture.Seed,
			fixture.DataManager.StaticData, () => fixture.Clock.NowMillis, fixture.Epoch,
			() => BotNavigationGeometry.ForServerWorld(probe.Server.GetInstanceId(), Race.ASMODIANS),
			_ => Task.FromResult(false), policy.AssertClean, () => policy.SnapshotProblems(), trace, dashboard);
		if (monitor.Enabled) Console.WriteLine($"{item} dashboard: {monitor.Url}");
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
		public NaturalJourneyRuntime? Runtime { get; set; }
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
			// Java uses the Priest-born reward group 3; REWARD2 selects its staff within that group.
			Server.GetQuestStateList().GetQuestState(2009).SetRewardGroup(3);
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
			if (step.NpcId == 700212) await NaturalLaterCapitalSteps.ReadQuestBookAsync(session, step, npc, token);
			else Console.WriteLine("PC " + await NaturalAltgardQuestSteps.TalkAsync(session, step, npc, token));
		}

		public async Task<int> WalkNpcAsync(int npcId, float? combatRange = null)
		{
			session.BeginStep($"pc-walk-{++Walked:00}", $"walk-to-{npcId}");
			var npc = Server.GetWorldMapInstance().GetNpcs(npcId).Where(n => !n.IsDead()).OrderBy(n =>
				NaturalFlightPolicy.Distance(session.CurrentPosition, new(n.GetX(), n.GetY(), n.GetZ(), 0))).First();
			int map = Server.GetWorldId();
			BotPosition from = session.CurrentPosition, to = new(npc.GetX(), npc.GetY(), npc.GetZ(), 0);
			float range = combatRange ?? Math.Min(5, npc.GetObjectTemplate().GetTalkDistance());
			BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(Server.GetInstanceId(), Race.ASMODIANS);
			IReadOnlyList<BotPosition> route = NaturalCapitalTravel.FindGroundApproachPath(geometry, map, from, to, range);
			if (route.Count == 0 && await travel.ConnectColiseumAsync(geometry, to, token))
			{
				from = session.CurrentPosition;
				route = geometry.FindInteractionPath(map, from, to);
			}
			Assert.True(route.Count > 0 || NaturalFlightPolicy.Distance(from, to) <= range,
				$"NPC {npcId}: no checked route ({BotNavMeshRouter.LastOutcome}) {from} -> {to}");
			ClearHostiles(Server.GetWorldMapInstance().GetNpcs().Where(n => combatRange == null || n.GetNpcId() != npcId), route.Append(from).Append(to));
			if (route.Count > 0) await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing)
				.CreateGroundPlan(route, from, session.Api.World.MovementSpeed!.Value), token);
			await session.SynchronizeAsync(token);
			float miss = NaturalFlightPolicy.Distance(new(Server.GetX(), Server.GetY(), Server.GetZ(), 0), to);
			Assert.True(miss <= range, $"NPC {npcId}: missed ordinary talk range by {miss:F1} m");
			Console.WriteLine($"PC route {npcId}: map {map}, {route.Count} points, miss {miss:F1} m");
			Assert.Contains(npc.GetObjectId(), session.Api.World.Objects.Keys);
			return npc.GetObjectId(); // Another corpse of this template can still be visible after the previous pull.
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
