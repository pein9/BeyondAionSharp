using Aion.Bots.Navigation;
using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.Movement;
using Aion.Bots.Protocol;
using Aion.Bots.Reflexes;
using Aion.Bots.Scenarios;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private sealed class NaturalCombatApproachBlockedException(string message) : IOException(message);
	private sealed class NaturalGuardedObjectiveRevivedException(string message) : IOException(message);

	private static bool StopNi07OnDeath =>
		Environment.GetEnvironmentVariable("NI07_STOP_ON_DEATH") == "1";

	/// <summary>
	/// One fresh Priest follows the frozen NI-07 quest contract. The explicitly
	/// selected short scope is a diagnostic checkpoint, not acceptance evidence.
	/// </summary>
	[SkippableFact]
	public async Task NaturalIshalgenPriestCompletesFrozenJourneyWithoutSetup()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		bool stopAfterQ2004 = Environment.GetEnvironmentVariable("NI07_STOP_AFTER_Q2004") == "1";
		bool stopAfterQ2005 = Environment.GetEnvironmentVariable("NI07_STOP_AFTER_Q2005") == "1";
		bool stopAfterQ2006 = Environment.GetEnvironmentVariable("NI07_STOP_AFTER_Q2006") == "1";
		bool stopAfterQ2007 = Environment.GetEnvironmentVariable("NI07_STOP_AFTER_Q2007") == "1";
		bool fullJourney = Environment.GetEnvironmentVariable("NI07_FULL_JOURNEY") == "1";
		// Set by TryFightThroughAsync: its last plan found a route to the objective that no observed monster
		// blocks (for example after a retreat dragged the pack away), so the caller should simply walk again.
		bool fightRouteOpen = false;
		BotPosition? fightRouteOpenAt = null;
		Skip.IfNot(stopAfterQ2004 || stopAfterQ2005 || stopAfterQ2006 || stopAfterQ2007 || fullJourney,
			"Set NI07_STOP_AFTER_Q2004=1, NI07_STOP_AFTER_Q2005=1, NI07_STOP_AFTER_Q2006=1 " +
			"or NI07_STOP_AFTER_Q2007=1 " +
			"for a focused checkpoint, or NI07_FULL_JOURNEY=1 for 41-quest acceptance.");
		string combatTracePath = Path.Combine(
			Environment.GetEnvironmentVariable("AION_NI07_COMBAT_DIR") ??
				Path.Combine(Aion.GameServer.TestKit.RealStaticData.RepoRoot(), "run", "ni07-combat"),
			$"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}.trace.jsonl");
		using var combatTrace = BotActionTraceWriter.Open(combatTracePath,
			Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "ni07", "b01", "sim-player-41",
			virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		Console.WriteLine($"NI-07 combat trace: {combatTracePath}");
		using var policy = NewPolicy("NI07", includeHistory: true);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(
			stopAfterQ2004 ? 6 : stopAfterQ2005 ? 8 : stopAfterQ2006 ? 16 : stopAfterQ2007 ? 20 : 45));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(
			fixture, policy, "b01", accountId: 41, "Asimnjour", Race.ASMODIANS,
			combatTrace, combatTracePath);
		session.BeginStep("ni07-create", "create-natural-asmodian-priest");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
		await session.SynchronizeAsync(token);
		Assert.Equal(5, session.Api.World.Quests[2000].Status);
		var player = fixture.World.GetPlayer(session.CharacterId);
		var created = session.PacketHistory.Single(packet => packet.PacketType == typeof(SM_CREATE_CHARACTER))
			.Get<IReadOnlyDictionary<string, object?>>("character");
		Assert.Equal((int)PlayerClass.PRIEST, Get<int>(created, "playerClass"));
		Assert.Equal(PlayerClass.PRIEST, player.GetPlayerClass()); // Read-only test oracle, not a bot decision.

		NaturalIshalgenContract contract = NaturalIshalgenContract.LoadDefault();
		IReadOnlyDictionary<int, QuestRunPlan> templatePlans = QuestRunPlan.LoadDirectory(
			Path.Combine(Aion.GameServer.TestKit.RealStaticData.RepoRoot(), "parity-artifacts", "e2e",
				"natural-ishalgen-plans")).ToDictionary(plan => plan.Id);
		Assert.Equal(26, templatePlans.Count);
		Assert.All(templatePlans.Keys, id => Assert.Contains(id, contract.Quests.Select(quest => quest.Id)));
		NaturalDecision decision = NaturalIshalgenDecisionEngine.Decide(contract,
			ObserveNaturalJourney(session), 1);
		Assert.Equal("find-quest-starter", decision.SelectedAction);
		Assert.Equal(2101, decision.SelectedQuestId);
		Assert.NotEqual("complete", decision.Outcome);

		BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(player.GetInstanceId(), player.GetRace());
		BotNavigationGraph graph = BotNavigationGraphFactory.Build(fixture.DataManager.StaticData,
			[203500, 203504, 203501, 203502, 203516, 203518,
			203519, 203534, 790002, 210377, 210378, 700045, 203538,
			203539, 210592, 700047, 203550, 210402, 210403, 203530, 203535, 203551, 203547,
			203540, 210395, 210396, 210750, 700095,
			203552, 203554, 700085, 700086, 700087, 203517,
			203533, 210734, 203514, 203543, 203532, 203531, 700128,
			210363, 210367, 210369, 700124, 700093],
			geometry);
		var navigator = new NaturalSimulationNavigator(session, graph, geometry)
		{
			// Level-aware roads-and-branches planning for long legs; null (grid/navmesh chain only)
			// when the map has no baked navmesh or travel graph.
			Planner = BotTravelPlanner.For(NaturalIshalgenRoads.MapId, geometry, fixture.DataManager.StaticData),
		};
		BotPosition? easternRoadIngressStart = null;
		BotPosition[] easternRoadIngress = [];
		NaturalSimulationCombat? navigationDefense = null;
		navigator.DefendOnAttackAsync = async (attackers, refuge, packetStart, defendToken) =>
		{
			if (navigationDefense?.InCombat != false) return;
			foreach (int attacker in attackers)
			{
				if (!session.Api.World.Objects.TryGetValue(attacker, out BotKnownObject? npc) ||
					npc.Kind != BotKnownObjectKind.Npc ||
					Distance(session.CurrentPosition, npc.Position) > 30) continue;
				session.TraceDiagnostic("defend-navigation", new Dictionary<string, object?>
				{
					["attacker"] = attacker,
					["npcId"] = npc.TemplateId,
					["hp"] = session.Api.World.CurrentHp,
					["position"] = session.CurrentPosition,
					["retreatAnchor"] = refuge,
				});
				if (await navigationDefense.TryKillAsync(attacker, defendToken, refuge, packetStart))
					navigator.UnavailableObjects.Add(attacker);
				else return; // Re-observe after a retreat or lost target before another pull.
			}
		};
		int observedAsak = await session.WaitForNpcAsync(203500, token);
		session.BeginStep("ni07-q2101-start", "walk-to-asak-and-accept");
		NaturalNavigationResult asakApproach = await NaturalIshalgenNavigator.ApproachNpcAsync(
			contract.MapId, 203500, session.Api.World.Objects[observedAsak].Position, navigator, token);
		Assert.True(asakApproach.Arrived, asakApproach.Reason);
		int asak = Assert.IsType<int>(asakApproach.TargetObjectId);
		await session.StartQuestAsync(asak, 2101, token);
		Assert.Equal(3, session.Api.World.Quests[2101].Status);

		session.BeginStep("ni07-q2101-finish", "walk-to-vandar-and-report");
		NaturalNavigationResult vandarApproach = await NaturalIshalgenNavigator.ApproachNpcAsync(
			contract.MapId, 203504, new BotPosition(526.99f, 2775.67f, 295.751f, 0), navigator, token);
		Assert.True(vandarApproach.Arrived, vandarApproach.Reason);
		int vandar = Assert.IsType<int>(vandarApproach.TargetObjectId);
		await session.FinishQuestAsync(vandar, 2101, token);
		Assert.Equal(5, session.Api.World.Quests[2101].Status);

		session.BeginStep("ni07-q2102-start", "accept-bloody-task-at-vandar");
		await session.StartQuestAsync(vandar, 2102, token);
		Assert.Equal(3, session.Api.World.Quests[2102].Status);
		var combat = new NaturalSimulationCombat(session, navigator, fixture, geometry);
		navigationDefense = combat;
		bool maintainingInventory = false;
		long failedRestockKinah = -1;
		var refusedGear = new HashSet<int>();

		// Wear the best gear in the bag (the recorded human put on four unused quest rewards at Nalto).
		// The client knows each item's slots, level and class/race limits from its tooltip; the server
		// still checks every equip, and an item it refuses is never asked for again.
		async Task EquipUpgradesAsync(CancellationToken gearToken)
		{
			BotWorldModel world = session.Api.World;
			if (world.IsDead) return;
			var playerClass = player.GetPlayerClass();
			var race = player.GetRace();
			var gender = player.GetGender();
			NaturalGearInfo? Describe(int itemId)
			{
				var template = Aion.GameServer.Dataholders.DataManager.ITEM_DATA.GetItemTemplate(itemId);
				if (template == null || template.GetItemSlot() == 0) return null;
				var genderLimit = template.GetUseLimits()?.GetGenderPermitted();
				return new NaturalGearInfo(template.GetItemSlot(), template.GetRequiredLevel(playerClass), template.GetLevel(),
					(template.GetRace() == Aion.GameServer.Model.Race.PC_ALL || template.GetRace() == race) &&
					(genderLimit == null || genderLimit == gender));
			}
			foreach (NaturalGearUpgrade upgrade in NaturalGearPolicy.SelectUpgrades(world.Inventory.Values, world.Level,
				Describe, (long)Aion.GameServer.Model.Items.ItemSlot.MAIN_OFF_OR_SUB_OFF, refusedGear))
			{
				await session.SendPacketAsync(session.Api.Equip(0, upgrade.Slot, upgrade.ObjectId), gearToken);
				await session.SynchronizeAsync(gearToken);
				bool worn = world.Inventory.TryGetValue(upgrade.ObjectId, out BotInventoryItem? after) &&
					(after.Details.EquippedSlot ?? 0) > 0;
				if (!worn) refusedGear.Add(upgrade.ObjectId);
				session.TraceDiagnostic("gear-equip", new Dictionary<string, object?>
				{
					["itemId"] = upgrade.ItemId,
					["objectId"] = upgrade.ObjectId,
					["slot"] = upgrade.Slot,
					["itemLevel"] = upgrade.ItemLevel,
					["replacesItemLevel"] = upgrade.ReplacesItemLevel,
					["worn"] = worn,
				});
			}
		}
		combat.MaintainInventoryAsync = MaintainInventoryAsync;
		NaturalHostility.PlayerTribe = NaturalHostility.TribeOf(player.GetRace());
		// Every aggressive spawn spot, plus every step of a patrol's route (a walker stands anywhere on it).
		var hostileSpawns = graph.GetMap(contract.MapId)!.Waypoints
			.Select(waypoint => (waypoint, template: waypoint.TemplateId is int id
				? Aion.GameServer.Dataholders.DataManager.NPC_DATA.GetNpcTemplate(id) : null))
			.Where(entry => NaturalHostility.IsAggressive(entry.template))
			.Select(entry => new BotNavigationHazard(entry.waypoint.Position, entry.template!.GetAggroRange())).ToList();
		foreach (var group in Aion.GameServer.Dataholders.DataManager.SPAWNS_DATA.GetSpawnsByWorldId(contract.MapId))
		{
			var template = Aion.GameServer.Dataholders.DataManager.NPC_DATA.GetNpcTemplate(group.GetNpcId());
			if (!NaturalHostility.IsAggressive(template)) continue;
			foreach (var spot in group.GetSpawnTemplates())
			{
				if (spot.GetWalkerId() is not { Length: > 0 } routeId) continue;
				var route = Aion.GameServer.Dataholders.DataManager.WALKER_DATA.GetWalkerTemplate(routeId);
				if (route == null) continue;
				foreach (var step in route.GetRouteSteps())
					hostileSpawns.Add(new BotNavigationHazard(new BotPosition(step.GetX(), step.GetY(), step.GetZ(), 0),
						template!.GetAggroRange()));
			}
		}
		combat.HostileSpawns = hostileSpawns;
		async Task MaintainInventoryAsync(CancellationToken maintenanceToken)
		{
			await EquipUpgradesAsync(maintenanceToken);
			BotWorldModel world = session.Api.World;
			if (maintainingInventory || !NaturalIshalgenPotionPolicy.NeedsRestock(world.Inventory.Values) ||
				world.Kinah == failedRestockKinah) return;
			maintainingInventory = true;
			try
			{
				long stock = NaturalIshalgenPotionPolicy.Count(world.Inventory.Values,
					NaturalIshalgenPotionPolicy.VendorLifeElixirId);
				long totalStock = NaturalIshalgenPotionPolicy.TotalHealingCount(world.Inventory.Values);
				string root = Aion.GameServer.TestKit.RealStaticData.RepoRoot();
				var inventoryPolicy = NaturalIshalgenInventoryPolicy.Load(root,
					world.Inventory.Values.Select(item => item.ItemId));
				NaturalInventoryPlan plan = inventoryPolicy.Decide(world);
				long basePrice = Aion.GameServer.Dataholders.DataManager.ITEM_DATA
					.GetItemTemplate(NaturalIshalgenPotionPolicy.VendorLifeElixirId).GetPrice();
				if (world.Kinah < basePrice && plan.Sales.Count == 0)
				{
					failedRestockKinah = world.Kinah;
					return;
				}
				foreach (var candidate in NaturalIshalgenPotionPolicy.Vendors
					.OrderBy(vendor => Distance(session.CurrentPosition, vendor.Position)))
				{
					NaturalNavigationResult approach = await NaturalIshalgenNavigator.ApproachNpcAsync(
						contract.MapId, candidate.NpcId, candidate.Position, navigator, maintenanceToken);
					if (!approach.Arrived) continue;
					int vendor = Assert.IsType<int>(approach.TargetObjectId);
					await session.SendPacketAsync(session.Api.TalkTo(vendor), maintenanceToken);
					await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), maintenanceToken);
					if (plan.Sales.Count > 0)
					{
						await session.SendPacketAsync(session.Api.SelectDialog(vendor, 3), maintenanceToken);
						await session.WaitForPacketAsync(typeof(SM_SELL_ITEM), maintenanceToken);
						foreach (NaturalInventoryDecision sale in plan.Sales)
						{
							await session.SendPacketAsync(session.Api.Sell(vendor,
								[(sale.ObjectId, sale.Count)]), maintenanceToken);
							await session.SynchronizeAsync(maintenanceToken);
						}
					}
					await session.SendPacketAsync(session.Api.SelectDialog(vendor, 2), maintenanceToken);
					await session.WaitForPacketAsync(typeof(SM_TRADELIST), maintenanceToken);
					BotTradeWindow trade = world.Trade ?? throw new InvalidDataException("Vendor sent no trade window.");
					if (trade.TargetObjectId != vendor || !trade.Tabs.Contains(721))
						throw new InvalidDataException($"Ishalgen vendor {candidate.NpcId} did not offer the shipped elixir list.");
					BotVendorPrices prices = world.VendorPrices ??
						throw new InvalidDataException("No client-observed vendor prices.");
					long unitPrice = prices.BuyPrice(basePrice, trade.BuyPriceModifier);
					long count = NaturalIshalgenPotionPolicy.AffordablePurchaseCount(totalStock, world.Kinah, unitPrice);
					if (count > 0)
					{
						await session.SendPacketAsync(session.Api.Buy(vendor,
							[(NaturalIshalgenPotionPolicy.VendorLifeElixirId, count)]), maintenanceToken);
						await session.SynchronizeAsync(maintenanceToken);
						long observed = NaturalIshalgenPotionPolicy.Count(world.Inventory.Values,
							NaturalIshalgenPotionPolicy.VendorLifeElixirId);
						if (observed != stock + count)
							throw new InvalidDataException($"Elixir purchase not observed: {stock}+{count}, got {observed}.");
						if (totalStock + count <= NaturalIshalgenPotionPolicy.RestockAtOrBelow)
							failedRestockKinah = world.Kinah;
					}
					else failedRestockKinah = world.Kinah;
					await session.SendPacketAsync(session.Api.CloseDialog(vendor), maintenanceToken);
					session.TraceDiagnostic("inventory-maintenance", new Dictionary<string, object?>
					{
						["vendorId"] = candidate.NpcId, ["sold"] = plan.Sales.Count,
						["elixirBefore"] = stock, ["elixirsBought"] = count,
						["totalHealingBefore"] = totalStock,
						["elixirAfter"] = NaturalIshalgenPotionPolicy.Count(world.Inventory.Values,
							NaturalIshalgenPotionPolicy.VendorLifeElixirId), ["unitPrice"] = unitPrice,
						["kinah"] = world.Kinah,
					});
					return;
				}
				// Deep in a camp, every checked way to both vendors can be closed. A player keeps going on heals and
				// shops later: record it and retry once the purse changes (the same back-off as an empty purse).
				failedRestockKinah = world.Kinah;
				session.TraceDiagnostic("restock-unreachable", new Dictionary<string, object?>
				{
					["totalHealing"] = totalStock,
					["kinah"] = world.Kinah,
					["position"] = session.CurrentPosition,
				});
			}
			finally { maintainingInventory = false; }
		}
		for (int kill = 0; kill < 4; kill++)
		{
			session.BeginStep($"ni07-q2102-kill-{kill + 1}", "approach-and-fight-sprigg-worker");
			BotPosition anchor = graph.GetMap(contract.MapId)!.Waypoints
				.Where(waypoint => waypoint.TemplateId == 210363)
				.OrderBy(waypoint => Distance(session.CurrentPosition, waypoint.Position))
				.First().Position;
			NaturalNavigationResult approach = await NaturalIshalgenNavigator.ApproachNpcAsync(
				contract.MapId, 210363, anchor, navigator, token);
			Assert.True(approach.Arrived, approach.Reason);
			int target = Assert.IsType<int>(approach.TargetObjectId);
			await combat.KillAsync(target, token);
			navigator.UnavailableObjects.Add(target);
			await combat.RestAsync(token);
		}
		Assert.Equal(4, session.Api.World.Quests[2102].StepAndFlags);
		session.BeginStep("ni07-q2102-finish", "return-to-vandar-and-claim");
		vandarApproach = await NaturalIshalgenNavigator.ApproachNpcAsync(contract.MapId, 203504,
			new BotPosition(526.99f, 2775.67f, 295.751f, 0), navigator, token);
		Assert.True(vandarApproach.Arrived, vandarApproach.Reason);
		await session.FinishQuestAsync(Assert.IsType<int>(vandarApproach.TargetObjectId), 2102, token);
		Assert.Equal(5, session.Api.World.Quests[2102].Status);

		decision = NaturalIshalgenDecisionEngine.Decide(contract, ObserveNaturalJourney(session), 2);
		Assert.Equal(2103, decision.SelectedQuestId);
		vandar = Assert.IsType<int>(vandarApproach.TargetObjectId);
		session.BeginStep("ni07-q2103-start", "accept-sprigg-report-at-vandar");
		await session.StartQuestAsync(vandar, 2103, token);
		session.BeginStep("ni07-q2103-finish", "walk-to-guheitun-and-report");
		NaturalNavigationResult guheitunApproach = await NaturalIshalgenNavigator.ApproachNpcAsync(contract.MapId,
			203501, new BotPosition(223.975f, 2679.86f, 295.25f, 0), navigator, token);
		Assert.True(guheitunApproach.Arrived, guheitunApproach.Reason);
		await session.FinishQuestAsync(Assert.IsType<int>(guheitunApproach.TargetObjectId), 2103, token);
		Assert.Equal(5, session.Api.World.Quests[2103].Status);

		decision = NaturalIshalgenDecisionEngine.Decide(contract, ObserveNaturalJourney(session), 3);
		Assert.Equal(2104, decision.SelectedQuestId);
		session.BeginStep("ni07-q2104-start", "walk-to-vanar-and-accept");
		int vanar = await ApproachAsync(203502, new BotPosition(220.15f, 2678.81f, 295.25f, 0));
		await session.StartQuestAsync(vanar, 2104, token);
		for (int basket = 0; basket < 3; basket++)
		{
			session.BeginStep($"ni07-q2104-basket-{basket + 1}", "walk-and-loot-shipped-basket");
			int objectId = await UseAndLootQuestObjectAsync(700124, 182203104);
			navigator.UnavailableObjects.Add(objectId);
		}
		Assert.Equal(3, ItemCount(session.Api.World, 182203104));
		session.BeginStep("ni07-q2104-finish", "return-to-vanar-and-turn-in");
		vanar = await ApproachAsync(203502, new BotPosition(220.15f, 2678.81f, 295.25f, 0));
		await FinishItemQuestAsync(session, vanar, 2104, token);
		Assert.Equal(5, session.Api.World.Quests[2104].Status);
		Assert.Equal(0, ItemCount(session.Api.World, 182203104));

		decision = NaturalIshalgenDecisionEngine.Decide(contract, ObserveNaturalJourney(session), 4);
		if (decision.SelectedQuestId == 2100)
		{
			await FinishCaptainOrderAsync();
			decision = NaturalIshalgenDecisionEngine.Decide(contract, ObserveNaturalJourney(session), 5);
		}
		if (decision.SelectedQuestId == 2001)
		{
			await CompleteThinkingAheadAsync();
			decision = NaturalIshalgenDecisionEngine.Decide(contract, ObserveNaturalJourney(session), 6);
		}
		if (decision.SelectedQuestId == 2105)
		{
			await CompleteSparkleAndShineAsync();
		}
		else
			Assert.Equal(2002, decision.SelectedQuestId); // The next automatically started campaign precedes Q2105.
		if (decision.SelectedQuestId == 2002)
		{
			await AdvanceWheresRaeAsync();
			decision = NaturalIshalgenDecisionEngine.Decide(contract, ObserveNaturalJourney(session), 8);
		}
		if (decision.SelectedQuestId == 2132)
		{
			await CompleteNewSkillAsync();
			decision = NaturalIshalgenDecisionEngine.Decide(contract, ObserveNaturalJourney(session), 9);
		}
		Assert.Equal(2003, decision.SelectedQuestId);
		if (decision.SelectedQuestId == 2003)
		{
			await CompleteTreasureOfTheDeceasedAsync();
			decision = NaturalIshalgenDecisionEngine.Decide(contract, ObserveNaturalJourney(session), 10);
		}
		Assert.Equal(2004, decision.SelectedQuestId);
		await CompleteCharmedCubeAsync();
		if (stopAfterQ2004)
		{
			Assert.Contains(2004, session.Api.World.CompletedQuestIds);
			await session.QuitAsync(token);
			policy.AssertClean();
			return;
		}
		decision = NaturalIshalgenDecisionEngine.Decide(contract, ObserveNaturalJourney(session), 11);
		Assert.Equal(2005, decision.SelectedQuestId);
		await CompleteTeachingALessonAsync();
		if (stopAfterQ2005)
		{
			Assert.Contains(2005, session.Api.World.CompletedQuestIds);
			await session.QuitAsync(token);
			policy.AssertClean();
			return;
		}
		decision = NaturalIshalgenDecisionEngine.Decide(contract, ObserveNaturalJourney(session), 12);
		Assert.Equal(2006, decision.SelectedQuestId);
		await CompleteHitThemWhereItHurtsAsync();
		if (stopAfterQ2006)
		{
			Assert.Contains(2006, session.Api.World.CompletedQuestIds);
			await session.QuitAsync(token);
			policy.AssertClean();
			return;
		}
		decision = NaturalIshalgenDecisionEngine.Decide(contract, ObserveNaturalJourney(session), 13);
		Assert.Equal(2007, decision.SelectedQuestId);
		await CompleteWheresRaeThisTimeAsync();
		if (stopAfterQ2007)
		{
			Assert.Contains(2007, session.Api.World.CompletedQuestIds);
			await session.QuitAsync(token);
			policy.AssertClean();
			return;
		}
		if (!session.Api.World.CompletedQuestIds.Contains(2105))
		{
			decision = NaturalIshalgenDecisionEngine.Decide(contract, ObserveNaturalJourney(session), 14);
			Assert.Equal(2105, decision.SelectedQuestId);
			await CompleteSparkleAndShineAsync();
		}
		decision = NaturalIshalgenDecisionEngine.Decide(contract, ObserveNaturalJourney(session), 15);
		Assert.Equal(2106, decision.SelectedQuestId);
		await CompleteVanarsFlatteryAsync();
		for (int sequence = 16; sequence < 57; sequence++)
		{
			decision = NaturalIshalgenDecisionEngine.Decide(contract, ObserveNaturalJourney(session), sequence);
			if (decision.SelectedQuestId is not int questId) break;
			if (templatePlans.TryGetValue(questId, out QuestRunPlan? plan))
			{
				await CompleteTemplateQuestAsync(plan);
				continue;
			}
			switch (questId)
			{
				case 2114: await CompleteInsectProblemAsync(); break;
				case 2123: await CompleteImprisonedGourmetAsync(); break;
				case 2125: await CompleteRobberyPlotAsync(); break;
				case 2135: await CompleteForLoveOfNegiAsync(); break;
				default: throw new InvalidDataException($"Natural Ishalgen Q{questId} has no journey actions.");
			}
		}

		Assert.Equal(41, contract.Quests.Length);
		Assert.All(contract.Quests, quest => Assert.Contains(quest.Id, session.Api.World.CompletedQuestIds));
		Assert.Equal((ushort)9, session.Api.World.Level);
		Assert.DoesNotContain(contract.AscensionQuestId, session.Api.World.CompletedQuestIds);
		Assert.Equal(3, session.Api.World.Quests[contract.AscensionQuestId].Status);
		Assert.Equal(0, session.Api.World.Quests[contract.AscensionQuestId].StepAndFlags);
		session.BeginStep("ni07-munin-stop", "stand-at-munin-without-ascension-dialogue");
		await ApproachShippedSpawnAsync(contract.AscensionNpcId);
		decision = NaturalIshalgenDecisionEngine.Decide(contract, ObserveNaturalJourney(session), 57);
		Assert.Equal("journey-complete", decision.SelectedAction);
		Assert.Equal("complete", decision.Outcome);
		await session.QuitAsync(token);
		policy.AssertClean();

		async Task<int> ApproachAsync(int templateId, BotPosition anchor)
		{
			NaturalNavigationResult result = await NaturalIshalgenNavigator.ApproachNpcAsync(
				contract.MapId, templateId, anchor, navigator, token);
			Assert.True(result.Arrived, result.Reason);
			return Assert.IsType<int>(result.TargetObjectId);
		}

		bool SpawnsOnMap(int templateId) =>
			graph.GetMap(contract.MapId)!.Waypoints.Any(waypoint => waypoint.TemplateId == templateId);

		async Task<int> ApproachShippedSpawnAsync(int templateId, bool skipBlockedTarget = false)
		{
			BotWaypoint[] anchors = graph.GetMap(contract.MapId)!.Waypoints
				.Where(waypoint => waypoint.TemplateId == templateId)
				.OrderBy(waypoint => Distance(session.CurrentPosition, waypoint.Position)).ToArray();
			if (anchors.Length == 0) throw new InvalidDataException($"Shipped spawn graph has no NPC {templateId}.");
			var reasons = new List<string>();
			foreach (BotWaypoint anchor in anchors.Take(12))
			{
				for (int guardClears = 0; guardClears <= 8; guardClears++)
				{
					NaturalNavigationResult result = await NaturalIshalgenNavigator.ApproachNpcAsync(
						contract.MapId, templateId, anchor.Position, navigator, token);
					if (result.Arrived && result.TargetObjectId is int objectId) return objectId;
					// Monsters on the way are not a wall: fight through to the objective (observed or its
					// shipped anchor) one pull at a time before giving up on this spawn hint.
					if (guardClears < 8 && result.Reason is "No collision-checked route to the current destination." or
							"New client-observed hazards exceeded the bounded replan budget.")
					{
						NaturalNavigationObject? seen = result.TargetObjectId is int blockedObjectId
							? navigator.Observe().Npcs.FirstOrDefault(npc => npc.ObjectId == blockedObjectId) : null;
						int revivesBefore = combat.ReviveCount;
						if (await TryClearObservedBlockerAsync(seen?.Position ?? anchor.Position, seen?.ObjectId))
							continue;
						if (combat.ReviveCount > revivesBefore) break;
					}
					if (skipBlockedTarget && result.TargetObjectId is int unavailableObjectId &&
						result.Reason == "No collision-checked route to the current destination.")
					{
						// A guarded object remains unreachable after bounded ordinary
						// fights. Try another shipped spawn; do not fabricate access.
						navigator.UnavailableObjects.Add(unavailableObjectId);
						session.TraceDiagnostic("skip-blocked-quest-object", new Dictionary<string, object?>
						{
							["templateId"] = templateId,
							["objectId"] = unavailableObjectId,
							["anchor"] = anchor.Position,
							["position"] = session.CurrentPosition,
							["route"] = navigator.LastRouteDiagnostic,
						});
					}
					reasons.Add(result.Reason);
					break;
				}
			}
			throw new InvalidDataException($"No client-observed NPC {templateId} at twelve shipped spawn hints " +
				$"from {session.CurrentPosition}: {string.Join(" | ", reasons)}");
		}

		async Task<int> ApproachShippedCombatSpawnAsync(int templateId)
		{
			BotWaypoint[] anchors = graph.GetMap(contract.MapId)!.Waypoints
				.Where(waypoint => waypoint.TemplateId == templateId)
				.OrderBy(waypoint => Distance(session.CurrentPosition, waypoint.Position)).Take(12).ToArray();
			if (anchors.Length == 0) throw new InvalidDataException($"Shipped spawn graph has no combat NPC {templateId}.");
			var reasons = new List<string>();
			foreach (BotWaypoint anchor in anchors)
			{
				NaturalNavigationResult approach = await NaturalIshalgenNavigator.ExploreWithinRangeAsync(
					contract.MapId, templateId, anchor.Position, 23, navigator,
					"priest-spell-range-target", token);
				if (!approach.Arrived)
				{
					reasons.Add(approach.Reason);
					continue;
				}
				// Prefer the target and spot that pull it alone (fewest helpers), not merely the nearest.
				NaturalNavigationObject[] visible = navigator.Observe().Npcs
					.Where(npc => npc.TemplateId == templateId && Distance(session.CurrentPosition, npc.Position) <= 30)
					.OrderBy(npc => Distance(session.CurrentPosition, npc.Position)).ToArray();
				if (visible.Length > 0 && await MoveToPullSpotAsync(visible, [], $"quest-kill-{templateId}") is { Helpers.Count: 0 } pull)
				{
					session.TraceDiagnostic("combat-standoff-selected", new Dictionary<string, object?>
					{
						["templateId"] = templateId,
						["objectId"] = pull.Target.Npc.ObjectId,
						["distance"] = Distance(session.CurrentPosition, pull.Target.Npc.Position),
						["position"] = session.CurrentPosition,
						["cleanPull"] = true,
					});
					return pull.Target.Npc.ObjectId;
				}
				for (int scan = 0; scan < 2; scan++)
				{
					NaturalNavigationObject? observed = navigator.Observe().Npcs
						.Where(npc => npc.TemplateId == templateId &&
							Distance(session.CurrentPosition, npc.Position) <= 23)
						.OrderBy(npc => Distance(session.CurrentPosition, npc.Position))
						.FirstOrDefault();
					if (observed != null)
					{
						session.TraceDiagnostic("combat-standoff-selected", new Dictionary<string, object?>
						{
							["templateId"] = templateId,
							["objectId"] = observed.ObjectId,
							["distance"] = Distance(session.CurrentPosition, observed.Position),
							["position"] = session.CurrentPosition,
						});
						return observed.ObjectId;
					}
					await session.SynchronizeAsync(token);
				}
				reasons.Add($"No client-observed {templateId} inside 23 m of {anchor.Position}.");
			}
			throw new InvalidDataException($"No spell-range client-observed NPC {templateId}: " +
				string.Join(" | ", reasons));
		}

		async Task<int> ApproachShippedNpcThroughObservedGuardsAsync(int templateId,
			int maximumGuardClears)
		{
			BotWaypoint[] anchors = graph.GetMap(contract.MapId)!.Waypoints
				.Where(waypoint => waypoint.TemplateId == templateId)
				.OrderBy(waypoint => Distance(session.CurrentPosition, waypoint.Position)).Take(12).ToArray();
			if (anchors.Length == 0)
				throw new InvalidDataException($"Shipped spawn graph has no NPC {templateId}.");
			var reasons = new List<string>();
			foreach (BotWaypoint anchor in anchors)
			{
				for (int cleared = 0; cleared <= maximumGuardClears; cleared++)
				{
					NaturalNavigationResult result = await NaturalIshalgenNavigator.ApproachNpcAsync(
						contract.MapId, templateId, anchor.Position, navigator, token);
					if (result.Arrived && result.TargetObjectId is int objectId) return objectId;
					reasons.Add(result.Reason);
					if (result.Reason is not ("No collision-checked route to the current destination." or
						"New client-observed hazards exceeded the bounded replan budget.") ||
						cleared == maximumGuardClears) break;
					int revivesBefore = combat.ReviveCount;
					bool guardKilled = await TryClearObservedBlockerAsync(anchor.Position);
					if (combat.ReviveCount > revivesBefore)
						throw new NaturalGuardedObjectiveRevivedException(
							$"Priest revived while clearing guarded NPC {templateId} from {anchor.Position}.");
					if (!guardKilled) break;
					session.TraceDiagnostic("return-corridor-guard-cleared", new Dictionary<string, object?>
					{
						["npcTemplateId"] = templateId,
						["clearedCount"] = cleared + 1,
						["position"] = session.CurrentPosition,
					});
				}
			}
			throw new InvalidDataException($"No checked guarded approach to NPC {templateId} " +
				$"from {session.CurrentPosition}: {string.Join(" | ", reasons)}; " +
				$"last route: {navigator.LastRouteDiagnostic}");
		}

		// Observed monsters as the pull planner sees them (Java aggro range and tribe).
		NaturalPullMonster[] ObservedPullMonsters(int? except = null) => navigator.Observe().Npcs
			.Where(npc => npc.ObjectId != except)
			.Select(npc => (npc, template: Aion.GameServer.Dataholders.DataManager.NPC_DATA.GetNpcTemplate(npc.TemplateId)))
			.Where(entry => NaturalHostility.IsAggressive(entry.template))
			.Select(entry => new NaturalPullMonster(entry.npc, entry.template!.GetAggroRange(), entry.template.GetTribe().ToString()))
			.ToArray();
		static bool CanSupport(string helper, string asking) =>
			Enum.TryParse(helper, out Aion.GameServer.Model.TribeClass h) && Enum.TryParse(asking, out Aion.GameServer.Model.TribeClass a) &&
			Aion.GameServer.Dataholders.DataManager.TRIBE_RELATIONS_DATA.CanSupport(h, a);

		// Monsters already on the bot (attacked it in the recent packet window and still within 30 m) and
		// pursuers still closing in (over 10 m from where they were first seen, and nearer the bot than that
		// spot). A player deals with these before pulling anything new.
		(int[] Attackers, int[] Pursuers) Engaged()
		{
			var observed = navigator.Observe().Npcs.ToDictionary(npc => npc.ObjectId);
			int[] attackers = session.PacketHistory.TakeLast(400)
				.Where(packet => packet.PacketType == typeof(SM_ATTACK) && packet.Get<int>("targetObjId") == session.CharacterId)
				.Select(packet => packet.Get<int>("attackerObjId")).Distinct()
				.Where(id => observed.TryGetValue(id, out var npc) && Distance(session.CurrentPosition, npc.Position) < 30)
				.ToArray();
			int[] pursuers = ObservedPullMonsters()
				.Where(m => !attackers.Contains(m.Npc.ObjectId) && Distance(session.CurrentPosition, m.Npc.Position) < 45)
				.Where(m => session.PacketHistory.FirstOrDefault(packet => packet.PacketType == typeof(SM_NPC_INFO) &&
					packet.Get<int>("objectId") == m.Npc.ObjectId) is { } info &&
					new BotPosition(info.Get<float>("x"), info.Get<float>("y"), info.Get<float>("z"), 0) is var home &&
					Distance(home, m.Npc.Position) > 10 &&
					Distance(session.CurrentPosition, m.Npc.Position) < Distance(session.CurrentPosition, home))
				.Select(m => m.Npc.ObjectId).ToArray();
			return (attackers, pursuers);
		}

		// Use a quest object and loot its item like a player: wait out the use bar the server starts
		// (SM_USE_OBJECT), and if a monster interrupts it (the closing SM_USE_OBJECT has duration 0), fight
		// what is on the Priest and walk back to use it again. Returns the object that was looted.
		async Task<int> UseAndLootQuestObjectAsync(int templateId, int itemId, bool skipBlockedTarget = false)
		{
			for (int attempt = 1; attempt <= 10; attempt++)
			{
				int objectId = await ApproachShippedSpawnAsync(templateId, skipBlockedTarget);
				long before = ItemCount(session.Api.World, itemId);
				int start = session.PacketHistory.Count;
				await session.SendPacketAsync(session.Api.TalkTo(objectId), token);
				await session.SynchronizeAsync(token);
				DecodedBotServerPacket? started = session.PacketHistory.Skip(start)
					.LastOrDefault(packet => packet.PacketType == typeof(SM_USE_OBJECT) &&
						packet.Get<int>("targetObjectId") == objectId && packet.Get<byte>("actionType") != 2);
				int durationMs = started?.Get<int>("durationMs") ?? 3000;
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(Math.Max(durationMs, 1) + 1), token);
				await session.SynchronizeAsync(token);
				DecodedBotServerPacket? finish = session.PacketHistory.Skip(start)
					.LastOrDefault(packet => packet.PacketType == typeof(SM_USE_OBJECT) &&
						packet.Get<int>("targetObjectId") == objectId && packet.Get<byte>("actionType") == 2);
				bool completed = finish == null || finish.Get<int>("durationMs") > 0;
				if (completed && (await TryLootCorpseItemAsync(session, objectId, itemId, token) ||
					ItemCount(session.Api.World, itemId) > before))
					return objectId;
				session.TraceDiagnostic(completed ? "quest-object-without-item" : "quest-object-use-interrupted",
					new Dictionary<string, object?>
				{
					["templateId"] = templateId,
					["objectId"] = objectId,
					["itemId"] = itemId,
					["attempt"] = attempt,
					["durationMs"] = finish?.Get<int>("durationMs"),
					["position"] = session.CurrentPosition,
				});
				if (completed) navigator.UnavailableObjects.Add(objectId); // used up without the item: try another
				else if (!await DefendAgainstEngagedAsync($"quest-object-{templateId}"))
					await combat.RestAsync(token);
			}
			throw new InvalidDataException($"Quest object {templateId} did not yield item {itemId} after ten uses.");
		}

		// Pull, one at a time, every observed monster whose aggro circle (plus the 2 m assist offset) comes
		// within 20 m of the objective, then walk back to it. Bounded only as a stall guard.
		async Task ClearAroundObjectiveAsync(int objectiveObjectId, string purpose) =>
			await ClearAroundSpotAsync(navigator.Observe().Npcs.FirstOrDefault(npc => npc.ObjectId == objectiveObjectId)?.Position,
				objectiveObjectId, purpose);

		// The same, around a position (a shipped spawn hint) before walking onto it: the recorded human pulled the
		// Stalker beside the blue generator from 20 m out first, instead of stepping onto the generator and being
		// jumped there. With an object id, walks back to it afterwards.
		async Task ClearAroundSpotAsync(BotPosition? objective, int? objectiveObjectId, string purpose)
		{
			if (objective is not BotPosition spot) return;
			bool cleared = false;
			for (int pull = 0; pull < 20 && !session.Api.World.IsDead; pull++)
			{
				NaturalNavigationObject[] near = ObservedPullMonsters(objectiveObjectId)
					.Where(m => Distance(m.Npc.Position, spot) <= m.AggroRadius + NaturalPullPlanner.SupportRangeOffset + 20)
					.OrderBy(m => Distance(m.Npc.Position, session.CurrentPosition))
					.Select(m => m.Npc).ToArray();
				if (near.Length == 0) break;
				session.TraceDiagnostic("objective-clear", new Dictionary<string, object?>
				{
					["purpose"] = purpose,
					["objective"] = spot,
					["monsters"] = near.Select(npc => $"{npc.TemplateId}/{npc.ObjectId}").ToArray(),
				});
				NaturalPullPlan? plan = await MoveToPullSpotAsync(near, [], purpose);
				if (plan == null || session.Api.World.IsDead) break;
				int revivesBefore = combat.ReviveCount;
				bool killed;
				try { killed = await combat.TryKillAsync(plan.Target.Npc.ObjectId, token, session.CurrentPosition); }
				catch (NaturalCombatApproachBlockedException) { killed = false; }
				if (combat.ReviveCount > revivesBefore) return;
				if (killed) navigator.UnavailableObjects.Add(plan.Target.Npc.ObjectId);
				cleared = true;
				// Respawns take 180 s: rest only when it is needed, so the object gets used inside that window.
				if (session.Api.World.CurrentHp * 100 < session.Api.World.MaxHp * 60 ||
					session.Api.World.CurrentMp * 100 < session.Api.World.MaxMp * 40)
					await combat.RestAsync(token);
				else
					await combat.MaintainBuffsAsync(token);
			}
			if (cleared && objectiveObjectId != null && !session.Api.World.IsDead)
				await NaturalIshalgenNavigator.ApproachNpcAsync(contract.MapId,
					navigator.Observe().Npcs.FirstOrDefault(npc => npc.ObjectId == objectiveObjectId)?.TemplateId ?? -1,
					spot, navigator, token);
		}

		// Fight what is already on the bot, one at a time, before any new pull. False if the bot died.
		async Task<bool> DefendAgainstEngagedAsync(string purpose)
		{
			// No cap on how many attackers are fought: chain aggro and respawns can keep them coming. Only the
			// wait for pursuers that never arrive is bounded (they gave up and walked home).
			int pursuerWaits = 0;
			for (int round = 0; round < NaturalSimulationCombat.MaximumCombatActions; round++)
			{
				var (attackers, pursuers) = Engaged();
				if (attackers.Length == 0 && pursuers.Length == 0) return true;
				if (attackers.Length == 0)
				{
					// Let a chaser arrive and commit rather than walking into a fresh pull with it behind us.
					await session.AdvanceAsync(TimeSpan.FromSeconds(2), token);
					await session.SynchronizeAsync(token);
					if (++pursuerWaits >= 3) return true; // it turned back (leash/give-up); carry on
					continue;
				}
				pursuerWaits = 0;
				int attacker = attackers.OrderBy(id => Distance(session.CurrentPosition,
					navigator.Observe().Npcs.First(npc => npc.ObjectId == id).Position)).First();
				session.TraceDiagnostic("defend-before-pull", new Dictionary<string, object?>
				{
					["purpose"] = purpose,
					["attacker"] = attacker,
					["attackers"] = attackers,
					["pursuers"] = pursuers,
					["hp"] = session.Api.World.CurrentHp,
					["position"] = session.CurrentPosition,
				});
				int revivesBefore = combat.ReviveCount;
				bool killed = await combat.TryKillAsync(attacker, token, session.CurrentPosition);
				if (combat.ReviveCount > revivesBefore || session.Api.World.IsDead) return false;
				if (killed) navigator.UnavailableObjects.Add(attacker);
				else return true; // combat decided otherwise (retreat); let the caller re-plan
			}
			await combat.RestAsync(token);
			return true;
		}

		// Pull like a player: choose, among the given targets (earlier ones preferred on ties), the one and the
		// spot that bring the fewest helpers (Java assist rule), at spell range off to the side of the pack,
		// outside every circle; if helpers would still come, wait briefly for patrols to move. Then walk there
		// on a checked, hostile-free route. Returns the target to fight, or null when no spot is reachable.
		// A preference, not a wall: the spot where the Priest died is avoided when any other firing spot
		// works, but when it is the only way in (Rae's camp) the Priest still goes.
		const float DeathSpotAvoidance = 12f;
		async Task<NaturalPullPlan?> MoveToPullSpotAsync(IReadOnlyList<NaturalNavigationObject> targets,
			IReadOnlyList<BotPosition> stagingPoints, string purpose)
		{
			if (!await DefendAgainstEngagedAsync(purpose)) return null;
			if (session.Api.World.CurrentHp * 100 < session.Api.World.MaxHp * 80) await combat.RestAsync(token);
			await combat.MaintainBuffsAsync(token);
			NaturalPullPlan? plan = null;
			for (int wait = 0; wait <= 3; wait++)
			{
				NaturalPullMonster[] monsters = ObservedPullMonsters();
				NaturalPullMonster[] pullTargets = targets
					.Select(t => monsters.FirstOrDefault(m => m.Npc.ObjectId == t.ObjectId)).OfType<NaturalPullMonster>().ToArray();
				if (pullTargets.Length == 0) return null;
				BotNavigationHazard[] hazards = monsters.Select(m => new BotNavigationHazard(m.Npc.Position, m.AggroRadius)).ToArray();
				var reachable = new Dictionary<(int, int), bool>();
				bool avoidDeaths = combat.DeathSpots.Count > 0;
				NaturalPullPlan? PlanOnce() => NaturalPullPlanner.Plan(session.CurrentPosition, pullTargets, monsters, stagingPoints, CanSupport,
					(a, b) => geometry.HasLineOfSight(contract.MapId, a, b),
					point => geometry.SnapToGround(contract.MapId, point),
					spot =>
					{
						var key = ((int)MathF.Round(spot.X), (int)MathF.Round(spot.Y));
						// Navmesh-connected only: a grid shortcut can climb onto a rock top the navmesh keeps
						// as a separate island, stranding the bot for every later route.
						if (avoidDeaths && combat.DeathSpots.Any(death => Distance(death, spot) < DeathSpotAvoidance)) return false;
						if (!reachable.TryGetValue(key, out bool ok))
							reachable[key] = ok = Distance(session.CurrentPosition, spot) < 1 ||
								(geometry.NavMesh is { } router && router.NavMeshes.Get(contract.MapId) is { } mesh
									? mesh.IslandOf(spot) >= 0 && mesh.IslandOf(spot) == mesh.IslandOf(session.CurrentPosition) &&
										router.FindPath(contract.MapId, session.CurrentPosition, spot,
											BotNavQuery.Default with { Hazards = hazards }).Count > 0
									: geometry.FindJourneyPathAvoiding(contract.MapId, session.CurrentPosition, spot, hazards).Count > 0);
						return ok;
					});
				plan = PlanOnce();
				if (plan == null && avoidDeaths)
				{
					avoidDeaths = false; // the only way in passes where the Priest died: go anyway
					plan = PlanOnce();
				}
				session.TraceDiagnostic("pull-plan", new Dictionary<string, object?>
				{
					["purpose"] = purpose,
					["wait"] = wait,
					["position"] = session.CurrentPosition,
					["candidates"] = pullTargets.Select(t => $"{t.Npc.TemplateId}/{t.Npc.ObjectId}").ToArray(),
					["target"] = plan == null ? null : $"{plan.Target.Npc.TemplateId}/{plan.Target.Npc.ObjectId}",
					["firingPosition"] = plan?.FiringPosition,
					["expectedHelpers"] = plan?.Helpers.Select(h => $"{h.Npc.TemplateId}/{h.Npc.ObjectId}").ToArray(),
					["clearance"] = plan?.ClearanceFromOthers,
				});
				if (plan == null || plan.Helpers.Count == 0 || wait == 3) break;
				// A helper stands in range: patrols move, so give it a few seconds before accepting a chain pull.
				await session.AdvanceAsync(TimeSpan.FromSeconds(3), token);
				await session.SynchronizeAsync(token);
			}
			if (plan == null) return null;
			if (Distance(session.CurrentPosition, plan.FiringPosition) > 1.5f)
			{
				BotNavigationHazard[] hazards = ObservedPullMonsters()
					.Select(m => new BotNavigationHazard(m.Npc.Position, m.AggroRadius)).ToArray();
				IReadOnlyList<BotPosition> approach = geometry.FindJourneyPathAvoiding(contract.MapId,
					session.CurrentPosition, plan.FiringPosition, hazards);
				if (approach.Count == 0) return null;
				foreach (BotPosition[] chunk in approach.Chunk(8))
				{
					if (!navigator.IsSegmentSafe(chunk, plan.Target.Npc.ObjectId)) return null; // new hostile: re-plan
					await navigator.MoveAsync(chunk, token);
					await navigator.SynchronizeAsync(token);
					if (session.Api.World.IsDead) return null;
				}
			}
			return plan;
		}

		// Fight your way in: when observed monsters close every hostile-free route, take the route that fights
		// the least, walk its hostile-free prefix to a firing point just outside the first circle it enters,
		// and pull that one monster with ordinary combat. True after a kill (the caller re-observes and
		// re-plans); false when there is no terrain route or no safe pull.
		async Task<bool> TryFightThroughAsync(BotPosition objective, int? objectiveObjectId, HashSet<int> rejected)
		{
			static float AggroRadius(int templateId)
			{
				var template = Aion.GameServer.Dataholders.DataManager.NPC_DATA.GetNpcTemplate(templateId);
				return NaturalHostility.IsAggressive(template) ? template.GetAggroRange() + 1f : 0f;
			}
			NaturalObservedMonster[] monsters = navigator.Observe().Npcs
				.Where(npc => npc.ObjectId != objectiveObjectId)
				.Select(npc => new NaturalObservedMonster(npc, AggroRadius(npc.TemplateId)))
				.Where(monster => monster.Radius > 0).ToArray();
			IReadOnlyList<BotPosition> fightRoute = geometry.FindFightThroughPath(contract.MapId, session.CurrentPosition,
				objective, monsters.Select(m => new BotNavigationHazard(m.Npc.Position, m.Radius)).ToArray());
			NaturalFightThroughBlocker? next = fightRoute.Count == 0 ? null
				: NaturalFightThrough.SelectNext(session.CurrentPosition, fightRoute, monsters, rejected);
			session.TraceDiagnostic("fight-through-plan", new Dictionary<string, object?>
			{
				["objective"] = objective,
				["position"] = session.CurrentPosition,
				["routePoints"] = fightRoute.Count,
				["blockers"] = NaturalFightThrough.BlockersInOrder(session.CurrentPosition, fightRoute, monsters)
					.Select(m => $"{m.Npc.TemplateId}/{m.Npc.ObjectId}").ToArray(),
				["next"] = next == null ? null : $"{next.Monster.Npc.TemplateId}/{next.Monster.Npc.ObjectId}",
				["firingPosition"] = next?.FiringPosition,
			});
			fightRouteOpen = fightRoute.Count > 0 &&
				NaturalFightThrough.BlockersInOrder(session.CurrentPosition, fightRoute, monsters).Count == 0;
			if (next == null) return false;
			// The first few monsters the route meets, pulled in the order and from the spot that chain least.
			NaturalNavigationObject[] blockers = NaturalFightThrough.BlockersInOrder(session.CurrentPosition, fightRoute, monsters)
				.Where(m => !rejected.Contains(m.Npc.ObjectId)).Take(3).Select(m => m.Npc).ToArray();
			NaturalPullPlan? pull = await MoveToPullSpotAsync(blockers, next.Staging, "fight-through");
			if (session.Api.World.IsDead) return false;
			if (pull == null)
			{
				rejected.Add(next.Monster.Npc.ObjectId);
				return true; // re-plan: nothing reachable to pull from here now
			}
			NaturalNavigationObject? target = navigator.Observe().Npcs.FirstOrDefault(npc => npc.ObjectId == pull.Target.Npc.ObjectId);
			bool OutOfReach(NaturalNavigationObject npc) =>
				Distance(session.CurrentPosition, npc.Position) > NaturalPullPlanner.SpellRange + 3 ||
				!geometry.HasLineOfSight(contract.MapId, session.CurrentPosition, npc.Position);
			// A patrolling monster walks on while the bot walks to its firing spot: follow it and plan the
			// pull again, as a player does, rather than giving up on it.
			for (int chase = 1; target != null && chase <= 3 && OutOfReach(target) && !session.Api.World.IsDead; chase++)
			{
				session.TraceDiagnostic("pull-target-moved", new Dictionary<string, object?>
				{
					["target"] = $"{target.TemplateId}/{target.ObjectId}",
					["chase"] = chase,
					["distance"] = Distance(session.CurrentPosition, target.Position),
					["position"] = session.CurrentPosition,
				});
				NaturalPullPlan? again = await MoveToPullSpotAsync([target], [], "fight-through-chase");
				if (again == null) break;
				target = navigator.Observe().Npcs.FirstOrDefault(npc => npc.ObjectId == again.Target.Npc.ObjectId);
			}
			if (target == null) return true; // it moved away or despawned: re-plan from here
			if (OutOfReach(target))
			{
				rejected.Add(target.ObjectId);
				return true;
			}
			int revivesBefore = combat.ReviveCount;
			try
			{
				bool killed = await combat.TryKillAsync(target.ObjectId, token, session.CurrentPosition);
				if (combat.ReviveCount > revivesBefore) return false;
				if (!killed) { rejected.Add(target.ObjectId); return true; }
				navigator.UnavailableObjects.Add(target.ObjectId);
				session.TraceDiagnostic("fight-through-cleared", new Dictionary<string, object?>
				{
					["objective"] = objective,
					["cleared"] = $"{target.TemplateId}/{target.ObjectId}",
					["hp"] = session.Api.World.CurrentHp,
					["position"] = session.CurrentPosition,
				});
				await combat.RestAsync(token);
				return true;
			}
			catch (NaturalCombatApproachBlockedException exception)
			{
				session.TraceDiagnostic("fight-through-pull-blocked", new Dictionary<string, object?>
				{
					["blocker"] = target.ObjectId,
					["reason"] = exception.Message,
				});
				rejected.Add(target.ObjectId);
				return combat.ReviveCount == revivesBefore;
			}
		}

		async Task<bool> TryClearObservedBlockerAsync(BotPosition objective, int? objectiveObjectId = null)
		{
			var rejected = new HashSet<int>();
			// First the route that fights the least, pulled in order; then the older corridor heuristics.
			for (int pull = 0; pull < 3; pull++)
			{
				int killsBefore = navigator.UnavailableObjects.Count;
				if (!await TryFightThroughAsync(objective, objectiveObjectId, rejected))
				{
					// Nothing blocks now: walk again. Only once per spot, though; if the walk fails again from
					// the same place, the navigator sees something the fight-through plan does not.
					if (fightRouteOpen && !session.Api.World.IsDead &&
						(fightRouteOpenAt is not BotPosition last || Distance(last, session.CurrentPosition) > 2))
					{
						fightRouteOpenAt = session.CurrentPosition;
						return true;
					}
					break;
				}
				if (navigator.UnavailableObjects.Count > killsBefore) return true;
			}
			// A pack can block every checked detour even when a side guard's own
			// circle misses the straight objective line. Try the direct corridor
			// first, then a bounded twenty-metre shoulder of observed monsters.
			for (int corridorPass = 0; corridorPass < 3; corridorPass++)
			for (int attempt = 0; attempt < 4; attempt++)
			{
				float AggroRadius(int templateId)
				{
					var template = Aion.GameServer.Dataholders.DataManager.NPC_DATA.GetNpcTemplate(templateId);
					return NaturalHostility.IsAggressive(template)
						? template.GetAggroRange() + 1f : 0f;
				}
				IReadOnlyList<NaturalNavigationObject> observed = navigator.Observe().Npcs;
				float corridorMargin = corridorPass == 0 ? 0 : 20;
				NaturalNavigationObject? blocker = corridorPass == 2
					? NaturalGuardedObjectivePolicy.SelectBlockerOnRoute(session.CurrentPosition,
						geometry.FindInteractionPath(contract.MapId, session.CurrentPosition, objective),
						observed, AggroRadius, objectiveObjectId, rejected)
					: NaturalGuardedObjectivePolicy.SelectBlocker(session.CurrentPosition,
						objective, observed, AggroRadius, objectiveObjectId, rejected, corridorMargin);
				if (blocker == null) break;
				session.TraceDiagnostic("guarded-objective-clear", new Dictionary<string, object?>
				{
					["objective"] = objective,
					["blockerTemplateId"] = blocker.TemplateId,
					["blockerObjectId"] = blocker.ObjectId,
					["corridorSelection"] = corridorPass == 2 ? "checked-route" : "straight",
					["corridorMargin"] = corridorPass == 2 ? null : corridorMargin,
					["hp"] = session.Api.World.CurrentHp,
					["position"] = session.CurrentPosition,
				});
				BotPosition refuge = session.CurrentPosition;
				int revivesBefore = combat.ReviveCount;
				try
				{
					bool killed = await combat.TryKillAsync(blocker.ObjectId, token, refuge);
					if (combat.ReviveCount > revivesBefore) return false;
					if (killed)
					{
						navigator.UnavailableObjects.Add(blocker.ObjectId);
						return true;
					}
				}
				catch (NaturalCombatApproachBlockedException exception)
				{
					// The guard is real, but a newly observed pack may make its
					// checked spell-range approach unsafe. Try another observed guard
					// or a different shipped objective; never cross the hazard circle.
					session.TraceDiagnostic("guard-approach-blocked", new Dictionary<string, object?>
					{
						["blockerObjectId"] = blocker.ObjectId,
						["position"] = session.CurrentPosition,
						["reason"] = exception.Message,
					});
				}
				if (combat.ReviveCount > revivesBefore) return false;
				rejected.Add(blocker.ObjectId);
			}
			return false;
		}

		async Task WalkEasternRoadToDerotAsync()
		{
			BotPosition roadStart = session.CurrentPosition;
			int historyStart = navigator.Events.Count;
			if (Distance(session.CurrentPosition, new BotPosition(571.0388f, 2787.342f, 299.875f, 0)) < 80)
			{
				// Ordinary Return/revive lands at the bind point, not at the road's
				// Nobekk entrance. Rejoin through NPCs the Priest already visited;
				// each interaction leg is collision-checked against current hazards.
				await ApproachShippedSpawnAsync(203504); // Vandar
				await ApproachShippedSpawnAsync(203501); // Guheitun
				await ApproachShippedSpawnAsync(203502); // Vanar
				await ApproachShippedSpawnAsync(203516); // Ulgorn
				await ApproachShippedSpawnAsync(203518); // Boromer
			}
			await ApproachShippedSpawnAsync(203519);
			await ApproachShippedSpawnAsync(203534);
			await ApproachShippedSpawnAsync(790002);
			await ApproachShippedSpawnAsync(203535);
			await ApproachShippedSpawnAsync(203539);
			if (easternRoadIngressStart == null)
			{
				// This route was actually walked from Ulgorn's side during Q2003.
				// Save its client-estimated progress, not the merely planned A* path,
				// so later returns can check each reverse leg against current terrain
				// and observed hostiles instead of taking the dead-end valley shortcut.
				easternRoadIngressStart = roadStart;
				easternRoadIngress = NaturalIshalgenNavigator.SelectRetraceCheckpoints(
					navigator.Events.Skip(historyStart));
				session.TraceDiagnostic("eastern-road-learned", new Dictionary<string, object?>
				{
					["origin"] = roadStart,
					["checkpoints"] = easternRoadIngress.Length,
					["position"] = session.CurrentPosition,
				});
			}
		}

		async Task UseLearnedReturnToBindAsync()
		{
			// Java ce54b7931 ReturnEffect moves the player to the character's
			// bind location. Skill 243 is auto-learned at level 1 for all classes.
			// This is an ordinary player cast, not a setup or GM teleport.
			const int returnSkillId = 243;
			Assert.True(session.Api.World.Skills.TryGetValue(returnSkillId, out BotSkill? learned),
				"The Priest did not observe the auto-learned Return skill.");
			BotPosition origin = session.CurrentPosition;
			session.BeginStep("ni07-natural-return", "cast-learned-return-after-checked-route-blocked");
			int packetStart = session.PacketHistory.Count;
			await session.SendPacketAsync(session.Api.Target(session.CharacterId), token);
			await session.SendPacketAsync(session.Api.Cast(new SpellCastData(returnSkillId,
				checked((byte)learned!.Level), 0) { TargetObjectId = session.CharacterId }), token);
			DecodedBotServerPacket started = await BotCastProtocol.WaitForStartAsync(
				(predicate, waitToken) => session.WaitForPacketAsync(packet => predicate(packet) ||
					BotCastProtocol.IsStartRejection(packet), waitToken),
				session.CharacterId, returnSkillId, token);
			Assert.Equal(typeof(SM_CASTSPELL), started.PacketType);
			session.Api.World.BeginWorldReload();
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(started.Get<ushort>("castDuration") + 1), token);
			DecodedBotServerPacket result = await BotCastProtocol.WaitForCompletionAsync(
				session.WaitForPacketAsync, session.CharacterId, returnSkillId, token);
			Assert.Equal(typeof(SM_CASTSPELL_RESULT), result.PacketType);
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(result.Get<ushort>("hitTime") + 1), token);
			await session.SynchronizeAsync(token);
			Assert.Contains(session.PacketHistory.Skip(packetStart), packet =>
				packet.PacketType == typeof(SM_CHANNEL_INFO));
			Assert.Contains(session.PacketHistory.Skip(packetStart), packet =>
				packet.PacketType == typeof(SM_PLAYER_INFO) &&
				packet.Get<int>("objectId") == session.CharacterId);
			session.AcceptTeleportPosition();
			Assert.Equal(contract.MapId, session.Api.World.MapId);
			Assert.True(Distance(origin, session.CurrentPosition) > 30,
				"Return completed but did not move the Priest out of the checked-route pocket.");
			session.TraceDiagnostic("natural-return-completed", new Dictionary<string, object?>
			{
				["skillId"] = returnSkillId,
				["origin"] = origin,
				["bindPosition"] = session.CurrentPosition,
			});
		}

		async Task FinishCaptainOrderAsync()
		{
			await WaitForQuestStatusAsync(session, 2100, 3, token);
			session.BeginStep("ni07-q2100-finish", "walk-to-ulgorn-and-claim-natural-reward");
			int ulgorn = await ApproachShippedSpawnAsync(203516);
			string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
			var inventory = NaturalIshalgenInventoryPolicy.Load(root,
				session.Api.World.Inventory.Values.Select(item => item.ItemId));
			int rewardIndex = inventory.ChooseReward(2100, session.Api.World.Level,
				session.Api.World.Inventory.Values);
			Assert.True(rewardIndex >= 0, "Q2100 should present a selectable reward to the natural Priest.");
			await FinishStandardQuestAsync(session, ulgorn, 2100, token,
				DialogAction.SELECTED_QUEST_REWARD1 + rewardIndex);
			Assert.Equal(5, session.Api.World.Quests[2100].Status);
		}

		async Task CompleteThinkingAheadAsync()
		{
			await WaitForQuestStatusAsync(session, 2001, 3, token);
			int boromer = await ApproachShippedSpawnAsync(203518);
			session.BeginStep("ni07-q2001-boromer-start", "speak-to-boromer-and-watch-campaign-movie");
			await OpenQuestDialogAsync(boromer, 2001);
			await session.SendPacketAsync(session.Api.SelectDialog(boromer, DialogAction.SELECT1_1, questId: 2001), token);
			await session.SynchronizeAsync(token);
			Assert.Contains(session.PacketHistory, packet => packet.PacketType == typeof(SM_PLAY_MOVIE) &&
				packet.Get<int>("cutsceneId") == 51);
			await session.SendPacketAsync(session.Api.SelectDialog(boromer, DialogAction.SETPRO1, questId: 2001), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(1, session.Api.World.Quests[2001].StepAndFlags);

			for (int sack = 0; sack < 3; sack++)
			{
				session.BeginStep($"ni07-q2001-sack-{sack + 1}", "walk-and-loot-sprigg-grain-sack");
				int objectId = await UseAndLootQuestObjectAsync(700093, 182203002);
				navigator.UnavailableObjects.Add(objectId);
			}
			Assert.Equal(3, ItemCount(session.Api.World, 182203002));
			boromer = await ApproachShippedSpawnAsync(203518);
			session.BeginStep("ni07-q2001-boromer-check", "present-grain-and-advance-mission");
			await OpenQuestDialogAsync(boromer, 2001);
			await session.SendPacketAsync(session.Api.SelectDialog(boromer,
				DialogAction.CHECK_USER_HAS_QUEST_ITEM, questId: 2001), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(2, session.Api.World.Quests[2001].StepAndFlags);
			await session.SendPacketAsync(session.Api.SelectDialog(boromer, DialogAction.SETPRO3, questId: 2001), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(3, session.Api.World.Quests[2001].StepAndFlags);

			for (int kill = 0; kill < 6; kill++)
			{
				session.BeginStep($"ni07-q2001-kill-{kill + 1}", "fight-sprigg-gatherer-for-campaign");
				int target = await ApproachShippedSpawnAsync(210369);
				await combat.KillAsync(target, token);
				navigator.UnavailableObjects.Add(target);
				await combat.RestAsync(token);
			}
			Assert.Equal(4, session.Api.World.Quests[2001].Status);
			boromer = await ApproachShippedSpawnAsync(203518);
			session.BeginStep("ni07-q2001-finish", "claim-priest-appropriate-campaign-reward");
			string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
			var inventory = NaturalIshalgenInventoryPolicy.Load(root,
				session.Api.World.Inventory.Values.Select(item => item.ItemId));
			int rewardIndex = inventory.ChooseReward(2001, session.Api.World.Level,
				session.Api.World.Inventory.Values);
			Assert.True(rewardIndex >= 0);
			await FinishStandardQuestAsync(session, boromer, 2001, token,
				DialogAction.SELECTED_QUEST_REWARD1 + rewardIndex);
			Assert.Equal(5, session.Api.World.Quests[2001].Status);
		}

		async Task AdvanceWheresRaeAsync()
		{
			await WaitForQuestStatusAsync(session, 2002, 3, token);
			session.BeginStep("ni07-q2002-nobekk", "walk-to-nobekk-and-ask-about-rae");
			int nobekk = await ApproachShippedSpawnAsync(203519);
			await OpenQuestDialogAsync(nobekk, 2002);
			await session.SendPacketAsync(session.Api.SelectDialog(nobekk, DialogAction.SETPRO1, questId: 2002), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(1, session.Api.World.Quests[2002].StepAndFlags);

			session.BeginStep("ni07-q2002-dabi", "walk-to-dabi-and-ask-about-verdandi");
			int dabi = await ApproachShippedSpawnAsync(203534);
			await OpenQuestDialogAsync(dabi, 2002);
			await session.SendPacketAsync(session.Api.SelectDialog(dabi, DialogAction.SELECT2_1, questId: 2002), token);
			await session.SynchronizeAsync(token);
			Assert.Contains(session.PacketHistory, packet => packet.PacketType == typeof(SM_PLAY_MOVIE) &&
				packet.Get<int>("cutsceneId") == 52);
			await session.SendPacketAsync(session.Api.SelectDialog(dabi, DialogAction.SETPRO2, questId: 2002), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(2, session.Api.World.Quests[2002].StepAndFlags);

			session.BeginStep("ni07-q2002-verdandi", "walk-to-verdandi-and-accept-sprigg-task");
			int verdandi = await ApproachShippedSpawnAsync(790002);
			await OpenQuestDialogAsync(verdandi, 2002);
			await session.SendPacketAsync(session.Api.SelectDialog(verdandi, DialogAction.SETPRO3, questId: 2002), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(3, session.Api.World.Quests[2002].StepAndFlags);
			BotPosition spriggRefuge = session.CurrentPosition; // Client-observed, already visited quest-giver ground.
			var rejectedSpriggs = new HashSet<int>();
			BotPosition[] pairedGuardHints = graph.GetMap(contract.MapId)!.Waypoints
				.Where(waypoint => waypoint.TemplateId == 210378)
				.Select(waypoint => waypoint.Position).ToArray();
			navigator.AvoidHostileAggro = true;
			for (int kill = 0, attempt = 0; kill < 7 && attempt < 21; attempt++)
			{
				session.BeginStep($"ni07-q2002-kill-{kill + 1}-try-{attempt + 1}",
					"pull-client-observed-sprigg-from-priest-spell-range");
				await combat.RestAsync(token);
				await session.SynchronizeAsync(token);
				NaturalNavigationObject[] observed = navigator.Observe().Npcs.ToArray();
				NaturalNavigationObject[] candidates = observed
					.Where(npc => npc.TemplateId == 210377 && !rejectedSpriggs.Contains(npc.ObjectId) &&
						pairedGuardHints.All(guard => Distance(guard, npc.Position) >= 10))
					.OrderBy(npc => observed.Count(other => other.ObjectId != npc.ObjectId &&
						NaturalHostility.IsAggressive(
							Aion.GameServer.Dataholders.DataManager.NPC_DATA.GetNpcTemplate(other.TemplateId)) &&
						Distance(other.Position, npc.Position) < 10))
					.ThenBy(npc => Distance(session.CurrentPosition, npc.Position)).ToArray();
				int? selected = null;
				foreach (NaturalNavigationObject candidate in candidates)
				{
					if (Distance(session.CurrentPosition, candidate.Position) > 25)
					{
						IReadOnlyList<BotPosition> route = await navigator.FindRouteAsync(
							session.CurrentPosition, candidate.Position, token);
						if (route.Count == 0) continue;
						BotPosition standoff = route.LastOrDefault(point =>
							Distance(point, candidate.Position) >= 22);
						if (standoff == default && Distance(session.CurrentPosition, candidate.Position) >= 22)
							standoff = session.CurrentPosition;
						if (standoff == default) continue;
						NaturalNavigationResult staging = await NaturalIshalgenNavigator.ExploreAnchorAsync(
							contract.MapId, -1, standoff, navigator, "sprigg-spell-range-standoff", token);
						if (!staging.Arrived) continue;
					}
					if (navigator.Observe().Npcs.Any(npc => npc.ObjectId == candidate.ObjectId) &&
						Distance(session.CurrentPosition, candidate.Position) <= 25)
					{
						selected = candidate.ObjectId;
						break;
					}
				}
				if (selected is not int target)
				{
					await session.AdvanceAsync(TimeSpan.FromSeconds(25), token); // Ordinary respawn/patrol wait.
					continue;
				}
				if (await combat.TryKillAsync(target, token, spriggRefuge))
				{
					navigator.UnavailableObjects.Add(target);
					kill++;
				}
				else rejectedSpriggs.Add(target);
			}
			navigator.AvoidHostileAggro = false;
			Assert.Equal(10, session.Api.World.Quests[2002].StepAndFlags);
			session.BeginStep("ni07-q2002-verdandi-report", "report-sprigg-kills-to-verdandi");
			verdandi = await ApproachShippedSpawnAsync(790002);
			await OpenQuestDialogAsync(verdandi, 2002);
			await session.SendPacketAsync(session.Api.SelectDialog(verdandi, DialogAction.SETPRO3, questId: 2002), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(11, session.Api.World.Quests[2002].StepAndFlags);
			session.BeginStep("ni07-q2002-mushroom", "collect-sticky-mushroom-for-verdandi");
			await UseAndLootQuestObjectAsync(700045, 182203003);
			Assert.Equal(1, ItemCount(session.Api.World, 182203003));
			session.BeginStep("ni07-q2002-mushroom-report", "present-collected-mushroom-to-verdandi");
			verdandi = await ApproachShippedSpawnAsync(790002);
			await OpenQuestDialogAsync(verdandi, 2002);
			await session.SendPacketAsync(session.Api.SelectDialog(verdandi,
				DialogAction.CHECK_USER_HAS_QUEST_ITEM, questId: 2002), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(12, session.Api.World.Quests[2002].StepAndFlags);
			session.BeginStep("ni07-q2002-ataxiar-enter", "take-verdandis-quest-teleport-to-ataxiar");
			session.Api.World.BeginWorldReload();
			await session.SendPacketAsync(session.Api.SelectDialog(verdandi, DialogAction.SETPRO5, questId: 2002), token);
			await session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token,
				packet => packet.Get<int>("worldId") == 320010000);
			await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token,
				packet => packet.Get<int>("objectId") == session.CharacterId);
			session.AcceptTeleportPosition();
			Assert.Equal(320010000, session.Api.World.MapId);
			Assert.Equal(99, session.Api.World.Quests[2002].StepAndFlags);
			NaturalDecision inInstance = NaturalIshalgenDecisionEngine.Decide(contract,
				ObserveNaturalJourney(session), 7);
			Assert.Equal(2002, inInstance.SelectedQuestId);
			Assert.Equal("continue-quest", inInstance.SelectedAction);
			Assert.Contains(inInstance.GlobalChecks, check => check.Rule == "quest-transport" && check.Verdict == "pass");
			session.BeginStep("ni07-q2002-hagen", "walk-to-hagen-and-take-quest-return-flight");
			var instanceGeometry = BotNavigationGeometry.ForServerWorld(player.GetInstanceId(), player.GetRace());
			BotNavigationGraph instanceGraph = BotNavigationGraphFactory.Build(fixture.DataManager.StaticData,
				[205020], instanceGeometry);
			var instanceNavigator = new NaturalSimulationNavigator(session, instanceGraph, instanceGeometry);
			NaturalNavigationResult hagenApproach = await NaturalIshalgenNavigator.ApproachNpcAsync(
				320010000, 205020, new BotPosition(434.75f, 399.5f, 235f, 25), instanceNavigator, token);
			Assert.True(hagenApproach.Arrived, hagenApproach.Reason);
			int hagen = Assert.IsType<int>(hagenApproach.TargetObjectId);
			await session.SendPacketAsync(session.Api.TalkTo(hagen), token);
			await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
				packet => packet.Get<int>("targetObjectId") == hagen);
			await session.SendPacketAsync(session.Api.SelectDialog(hagen, DialogAction.QUEST_SELECT, questId: 2002), token);
			await session.WaitForPacketAsync(typeof(SM_EMOTION), token,
				packet => packet.Get<int>("senderObjectId") == session.CharacterId &&
				packet.Get<byte>("emotionType") == (byte)EmotionType.START_FLYTELEPORT);
			session.Api.World.BeginWorldReload();
			await session.AdvanceAsync(TimeSpan.FromSeconds(40), token);
			await session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token,
				packet => packet.Get<int>("worldId") == contract.MapId);
			await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token,
				packet => packet.Get<int>("objectId") == session.CharacterId);
			session.AcceptTeleportPosition();
			Assert.Equal(contract.MapId, session.Api.World.MapId);
			Assert.Equal(13, session.Api.World.Quests[2002].StepAndFlags);
			session.BeginStep("ni07-q2002-verdandi-return", "report-return-from-ataxiar");
			verdandi = await ApproachShippedSpawnAsync(790002);
			await OpenQuestDialogAsync(verdandi, 2002);
			await session.SendPacketAsync(session.Api.SelectDialog(verdandi, DialogAction.SETPRO3, questId: 2002), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(14, session.Api.World.Quests[2002].StepAndFlags);
			session.BeginStep("ni07-q2002-ribbit", "find-and-interact-with-cute-ribbit");
			int ribbit = await ApproachShippedSpawnAsync(203538);
			await session.SendPacketAsync(session.Api.TalkTo(ribbit), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(15, session.Api.World.Quests[2002].StepAndFlags);
			int rae = await session.WaitForNpcAsync(203553, token);
			BotPosition raePosition = session.Api.World.Objects[rae].Position;
			NaturalNavigationResult raeApproach = await NaturalIshalgenNavigator.ApproachNpcAsync(
				contract.MapId, 203553, raePosition, navigator, token);
			Assert.True(raeApproach.Arrived, raeApproach.Reason);
			session.BeginStep("ni07-q2002-rae", "speak-to-quest-spawned-rae");
			await OpenQuestDialogAsync(rae, 2002);
			await session.SendPacketAsync(session.Api.SelectDialog(rae, DialogAction.SETPRO7, questId: 2002), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(4, session.Api.World.Quests[2002].Status);
			session.BeginStep("ni07-q2002-finish", "return-to-ulgorn-and-claim-priest-reward");
			await ApproachShippedSpawnAsync(203534); // Retrace the walked route through Dabi and Nobekk.
			await ApproachShippedSpawnAsync(203519);
			int ulgorn = await ApproachShippedSpawnAsync(203516);
			string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
			var inventory = NaturalIshalgenInventoryPolicy.Load(root,
				session.Api.World.Inventory.Values.Select(item => item.ItemId));
			int rewardIndex = inventory.ChooseReward(2002, session.Api.World.Level,
				session.Api.World.Inventory.Values);
			Assert.True(rewardIndex >= 0);
			await session.SendPacketAsync(session.Api.TalkTo(ulgorn), token);
			await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
				packet => packet.Get<int>("targetObjectId") == ulgorn);
			await session.SendPacketAsync(session.Api.SelectDialog(ulgorn, DialogAction.QUEST_SELECT, questId: 2002), token);
			await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
				packet => packet.Get<int>("targetObjectId") == ulgorn && packet.Get<int>("questId") == 2002);
			await session.SendPacketAsync(session.Api.SelectDialog(ulgorn, DialogAction.SETPRO8, questId: 2002), token);
			await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
				packet => packet.Get<int>("targetObjectId") == ulgorn && packet.Get<int>("questId") == 2002);
			await session.SendPacketAsync(session.Api.SelectDialog(ulgorn,
				checked((ushort)(DialogAction.SELECTED_QUEST_REWARD1 + rewardIndex)), questId: 2002), token);
			await WaitForQuestStatusAsync(session, 2002, 5, token);
			Assert.Contains(2002, session.Api.World.CompletedQuestIds);
		}

		async Task CompleteTreasureOfTheDeceasedAsync()
		{
			await WaitForQuestStatusAsync(session, 2003, 3, token);
			session.BeginStep("ni07-q2003-start", "walk-to-treasure-keeper-and-watch-campaign-movie");
			await WalkEasternRoadToDerotAsync(); // Eastern road avoids the collision-blocked direct valley line.
			int keeper = await ApproachShippedSpawnAsync(203539);
			await OpenQuestDialogAsync(keeper, 2003);
			await session.SendPacketAsync(session.Api.SelectDialog(keeper, DialogAction.SELECT1_1, questId: 2003), token);
			await session.SynchronizeAsync(token);
			Assert.Contains(session.PacketHistory, packet => packet.PacketType == typeof(SM_PLAY_MOVIE) &&
				packet.Get<int>("cutsceneId") == 53);
			await session.SendPacketAsync(session.Api.SelectDialog(keeper, DialogAction.SETPRO1, questId: 2003), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(1, session.Api.World.Quests[2003].StepAndFlags);
			int routedRevives = combat.ReviveCount;
			for (int attempt = 0; ItemCount(session.Api.World, 182203004) < 3; attempt++)
			{
				session.BeginStep($"ni07-q2003-kill-{attempt + 1}", "fight-and-loot-treasure-guardian");
				if (combat.ReviveCount > routedRevives)
				{
					await WalkEasternRoadToDerotAsync();
					routedRevives = combat.ReviveCount;
				}
				NaturalNavigationResult camp = await NaturalIshalgenNavigator.ExploreAnchorAsync(
					contract.MapId, -1, new BotPosition(667.961f, 1767.09f, 271.583f, 0),
					navigator, "shipped-west-side-camp", token);
				Assert.True(camp.Arrived, $"{camp.Reason} from {session.CurrentPosition}");
				await combat.RestAsync(token);
				if (combat.ReviveCount > routedRevives)
				{
					await WalkEasternRoadToDerotAsync();
					routedRevives = combat.ReviveCount;
				}
				int target = await ApproachShippedSpawnAsync(210592);
				bool killed = await combat.TryKillAsync(target, token,
					new BotPosition(948.97f, 1700.72f, 259.625f, 0));
				navigator.UnavailableObjects.Add(target);
				if (killed) await TryLootCorpseItemAsync(session, target, 182203004, token);
			}
			Assert.Equal(3, ItemCount(session.Api.World, 182203004));
			session.BeginStep("ni07-q2003-finish", "return-treasure-and-claim-reward");
			keeper = await ApproachShippedSpawnAsync(203539);
			await FinishItemQuestAsync(session, keeper, 2003, token);
			Assert.Contains(2003, session.Api.World.CompletedQuestIds);
		}

		async Task CompleteNewSkillAsync()
		{
			await WaitForQuestStatusAsync(session, 2132, 4, token);
			Assert.Equal(4, session.Api.World.Quests[2132].StepAndFlags);
			session.BeginStep("ni07-q2132-finish", "walk-to-priest-trainer-for-auto-learned-skill-quest");
			int trainer = await ApproachShippedSpawnAsync(203530);
			await FinishStandardQuestAsync(session, trainer, 2132, token);
			Assert.Contains(2132, session.Api.World.CompletedQuestIds);
		}

		async Task CompleteCharmedCubeAsync()
		{
			await WaitForQuestStatusAsync(session, 2004, 3, token);
			session.BeginStep("ni07-q2004-derot-start", "ask-derot-about-charmed-cube");
			int derot = await ApproachShippedSpawnAsync(203539);
			await OpenQuestDialogAsync(derot, 2004);
			await session.SendPacketAsync(session.Api.SelectDialog(derot, DialogAction.SETPRO1, questId: 2004), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(1, session.Api.World.Quests[2004].StepAndFlags);
			bool foundCube = false;
			for (int attempt = 1; !foundCube; attempt++)
			{
				session.BeginStep($"ni07-q2004-tombstone-{attempt}", "wake-and-fight-tombstone-guardian");
				await combat.RestAsync(token);
				BotPosition tombstoneIngress = session.CurrentPosition;
				int tombstone = await ApproachShippedSpawnAsync(700047);
				await session.SendPacketAsync(session.Api.TalkTo(tombstone), token);
				await session.WaitForPacketAsync(typeof(SM_EMOTION), token,
					packet => packet.Get<int>("senderObjectId") == session.CharacterId &&
					packet.Get<byte>("emotionType") == (byte)EmotionType.START_QUESTLOOT);
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(3001), token);
				await session.SynchronizeAsync(token);
				BotKnownObject? guardian = session.Api.World.Objects.Values.FirstOrDefault(item =>
					item.Kind == BotKnownObjectKind.Npc && item.TemplateId == 211755 &&
					!navigator.UnavailableObjects.Contains(item.ObjectId));
				Assert.NotNull(guardian);
				NaturalNavigationResult approach = await NaturalIshalgenNavigator.ApproachNpcAsync(
					contract.MapId, 211755, guardian.Position, navigator, token);
				Assert.True(approach.Arrived, approach.Reason);
				int target = Assert.IsType<int>(approach.TargetObjectId);
				bool killed = false;
				for (int pull = 0; pull < 3 && !killed; pull++)
				{
					if (pull > 0)
					{
						await combat.RestAsync(token);
						if (session.Api.World.LootStatuses.ContainsKey(target))
						{
							killed = true; // Ordinary defense during rest finished this guardian.
							break;
						}
						if (!navigator.Observe().Npcs.Any(npc => npc.ObjectId == target)) break;
					}
					killed = await combat.TryKillAsync(target, token, tombstoneIngress);
				}
				if (!killed) continue; // An escaped or despawned guardian did not earn a loot attempt.
				navigator.UnavailableObjects.Add(target);
				foundCube = await TryLootCorpseItemAsync(session, target, 182203005, token);
				await combat.RestAsync(token);
			}
			Assert.True(foundCube, "Ordinary tombstone guardians did not yield the quest cube.");
			Assert.Equal(1, ItemCount(session.Api.World, 182203005));
			session.BeginStep("ni07-q2004-derot-check", "show-quest-cube-to-derot");
			derot = await ApproachShippedSpawnAsync(203539);
			await OpenQuestDialogAsync(derot, 2004);
			await session.SendPacketAsync(session.Api.SelectDialog(derot,
				DialogAction.CHECK_USER_HAS_QUEST_ITEM, questId: 2004), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(2, session.Api.World.Quests[2004].StepAndFlags);
			session.BeginStep("ni07-q2004-munin-start", "take-charmed-cube-to-munin");
			NaturalNavigationResult camp = await NaturalIshalgenNavigator.ExploreAnchorAsync(
				contract.MapId, -1, new BotPosition(667.961f, 1767.09f, 271.583f, 0),
				navigator, "shipped-west-side-camp", token);
			Assert.True(camp.Arrived, camp.Reason); // Static route hint; no roaming NPC interaction.
			NaturalNavigationResult hillside = await NaturalIshalgenNavigator.ExploreAnchorAsync(
				contract.MapId, -1, new BotPosition(413.25f, 1901.5f, 319.136f, 0),
				navigator, "shipped-munin-hillside", token);
			Assert.True(hillside.Arrived, hillside.Reason);
			int munin = await ApproachShippedSpawnAsync(203550);
			await OpenQuestDialogAsync(munin, 2004);
			await session.SendPacketAsync(session.Api.SelectDialog(munin, DialogAction.SETPRO3, questId: 2004), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(3, session.Api.World.Quests[2004].StepAndFlags);
			BotPosition muninRefuge = session.CurrentPosition;
			for (int kill = 0; kill < 3; kill++)
			{
				bool killed = false;
				for (int attempt = 1; attempt <= 4 && !killed; attempt++)
				{
					session.BeginStep($"ni07-q2004-kill-{kill + 1}-try-{attempt}",
						"fight-munins-cube-target-from-priest-spell-range");
					await combat.RestAsync(token);
					int target = await ApproachShippedCombatSpawnAsync(210402);
					killed = await combat.TryKillAsync(target, token, muninRefuge);
					if (killed) navigator.UnavailableObjects.Add(target);
					else
					{
						NaturalNavigationResult regroup = await NaturalIshalgenNavigator.ExploreAnchorAsync(
							contract.MapId, -1, muninRefuge, navigator, "munin-retreat-refuge", token);
						Assert.True(regroup.Arrived, regroup.Reason);
					}
				}
				Assert.True(killed, $"Q2004 cube target {kill + 1} survived four ordinary Priest pulls.");
				await combat.RestAsync(token);
			}
			Assert.Equal(6, session.Api.World.Quests[2004].StepAndFlags);
			session.BeginStep("ni07-q2004-munin-report", "report-cube-combat-to-munin");
			munin = await ApproachShippedSpawnAsync(203550);
			await OpenQuestDialogAsync(munin, 2004);
			await session.SendPacketAsync(session.Api.SelectDialog(munin, DialogAction.SETPRO4, questId: 2004), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(4, session.Api.World.Quests[2004].Status);
			session.BeginStep("ni07-q2004-finish", "return-to-derot-for-quest-reward");
			derot = await ApproachShippedSpawnAsync(203539);
			await FinishStandardQuestAsync(session, derot, 2004, token);
			Assert.Contains(2004, session.Api.World.CompletedQuestIds);
		}

		async Task CompleteTeachingALessonAsync()
		{
			await WaitForQuestStatusAsync(session, 2005, 3, token);
			navigator.AvoidHostileAggro = true;
			session.BeginStep("ni07-q2005-mijou-start", "walk-to-mijou-and-watch-campaign-movie");
			int mijou = await ApproachShippedSpawnAsync(203540);
			await OpenQuestDialogAsync(mijou, 2005);
			await session.SendPacketAsync(session.Api.SelectDialog(mijou, DialogAction.SELECT1_1, questId: 2005), token);
			await session.SynchronizeAsync(token);
			Assert.Contains(session.PacketHistory, packet => packet.PacketType == typeof(SM_PLAY_MOVIE) &&
				packet.Get<int>("cutsceneId") == 54);
			await session.SendPacketAsync(session.Api.SelectDialog(mijou, DialogAction.SETPRO1, questId: 2005), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(1, session.Api.World.Quests[2005].StepAndFlags);
			// Approach from the Mijou side and stop at spell range. A spawn coordinate is
			// only a search hint: some Stalkers patrol and nearby Lycans are aggressive.
			// Shipped static/walker hints from the refreshed Ishalgen spawn XML, not
			// claims that a live Stalker is there. Live targets still come from packets.
			// Rotate after an unsafe pull instead of charging the same Lycan pack.
			BotPosition[] stalkerSearchAreas =
			[
				new(649.267f, 1535.266f, 294.283f, 0), // Western 210395 static.
				new(710.511f, 1460.318f, 285.937f, 0), // Eastern 210750 static.
				new(671.581f, 1331.605f, 299.5f, 0), // Southern 210750 static.
				new(708.95f, 998.82f, 321.7725f, 0), // Northern 210750 walker.
			];
			int routedRevives = combat.ReviveCount;
			bool lastStalkerKilled = false;
			BotPosition? successfulStalkerArea = null;
			int preferredAreaMisses = 0;
			int corridorClearAttempts = 0;
			var rejectedPullTargets = new HashSet<int>();
			var searchNotes = new List<string>();
			BotNavigationHazard[] ObservedFieldHazards() => navigator.Observe().Npcs
				.Select(npc => (Npc: npc,
					Template: Aion.GameServer.Dataholders.DataManager.NPC_DATA.GetNpcTemplate(npc.TemplateId)))
				.Where(entry => NaturalHostility.IsAggressive(entry.Template))
				.Select(entry => new BotNavigationHazard(entry.Npc.Position,
					entry.Template!.GetAggroRange())).ToArray();
			async Task<bool> RestAtObservedFieldCampAsync()
			{
				BotNavigationHazard[] hostiles = ObservedFieldHazards();
				if (ItemCount(session.Api.World, 182203006) >= 3 ||
					!NaturalFieldCampPolicy.CanRest(session.CurrentPosition, hostiles))
					return false;
				session.TraceDiagnostic("q2005-field-camp", new Dictionary<string, object?>
				{
					["position"] = session.CurrentPosition,
					["inventoryCount"] = ItemCount(session.Api.World, 182203006),
					["observedHostiles"] = hostiles.Length,
				});
				await combat.RestAsync(token); // Ordinary sit/stand; attack interrupts it.
				return true;
			}
			BotPosition[]? FindCheckedFiringEdge(NaturalNavigationObject target)
			{
				BotPosition start = session.CurrentPosition;
				foreach (float radius in new[] { 0f, 6f, 12f, 18f })
				for (int sector = 0; sector < 24; sector++)
				{
					float angle = sector * MathF.PI / 12;
					var candidate = new BotPosition(start.X + radius * MathF.Cos(angle),
						start.Y + radius * MathF.Sin(angle), start.Z, 0);
					IReadOnlyList<BotPosition>? edge = geometry.TraceEdge(contract.MapId, start, candidate);
					if (edge == null || !navigator.IsSegmentSafe(edge, target.ObjectId)) continue;
					BotPosition ground = edge[^1];
					if (Distance(ground, target.Position) <= 25 &&
						geometry.HasLineOfSight(contract.MapId, ground, target.Position))
						return edge.ToArray();
				}
				return null;
			}
			async Task<int> ReachMijouFromFieldAsync()
			{
				try
				{
					return await ApproachShippedNpcThroughObservedGuardsAsync(203540, maximumGuardClears: 12);
				}
				catch (InvalidDataException exception) when (exception.Message.StartsWith(
					"No checked guarded approach to NPC 203540 ", StringComparison.Ordinal))
				{
					// A respawned pack or collision pocket can make the entire checked
					// walk back impossible. Return is the Priest's learned player skill;
					// rejoin the previously visited road from the natural bind point.
					session.TraceDiagnostic("q2005-return-skill-fallback", new Dictionary<string, object?>
					{
						["position"] = session.CurrentPosition,
						["reason"] = exception.Message,
					});
					await UseLearnedReturnToBindAsync();
					await WalkEasternRoadToDerotAsync();
					return await ApproachShippedSpawnAsync(203540);
				}
			}
			async Task<int> ReturnToMijouAlongIngressAsync(int eventStart, BotPosition ingressStart)
			{
				// A distant reverse A* can fail on the geodata even though the bot just
				// walked in. Revisit its client-estimated movement checkpoints in reverse.
				BotPosition[] breadcrumbs = NaturalIshalgenNavigator.SelectRetraceCheckpoints(
					navigator.Events.Skip(eventStart), stride: 3);
				for (int guardClears = 0; guardClears <= 4; guardClears++)
				{
					int eventBefore = navigator.Events.Count;
					NaturalNavigationResult result = await NaturalIshalgenNavigator.RetraceIngressAsync(
						contract.MapId, ingressStart, breadcrumbs, navigator, token);
					if (result.Arrived) return await ReachMijouFromFieldAsync();
					BotPosition blockedCheckpoint = navigator.Events.Skip(eventBefore)
						.LastOrDefault(entry => entry.Action == "navigation-failed")?.Destination ?? ingressStart;
					if (guardClears < 4 && await TryClearObservedBlockerAsync(blockedCheckpoint))
						continue; // Ordinary kill may open a checked route; re-observe every hazard.
					session.TraceDiagnostic("q2005-ingress-blocked", new Dictionary<string, object?>
					{
						["position"] = session.CurrentPosition,
						["ingressStart"] = ingressStart,
						["blockedCheckpoint"] = blockedCheckpoint,
						["reason"] = result.Reason,
						["route"] = navigator.LastRouteDiagnostic,
					});
					return await ReachMijouFromFieldAsync();
				}
				throw new InvalidDataException("Q2005 exhausted checked ingress retries without a return outcome.");
			}
			async Task WaitForStalkerRespawnAsync()
			{
				// A pursuer can follow the Priest back to Mijou. Advancing the whole
				// respawn delay at once queues its attacks without letting the bot react.
				int defendedPulls = 0;
				int attackHistoryStart = session.PacketHistory.Count;
				await NaturalObservedWait.WaitAsync(TimeSpan.FromSeconds(181), TimeSpan.FromSeconds(2),
					() => fixture.Clock.NowMillis,
					async (interval, waitToken) =>
					{
						int packetStart = session.PacketHistory.Count;
						await session.AdvanceAsync(interval, waitToken);
						await session.SynchronizeAsync(waitToken);
						if (session.Api.World.IsDead) await navigator.SynchronizeAsync(waitToken);
						return session.PacketHistory.Skip(packetStart)
							.Where(packet => packet.PacketType == typeof(SM_ATTACK) &&
								packet.Get<int>("targetObjId") == session.CharacterId)
							.Select(packet => packet.Get<int>("attackerObjId")).Distinct().ToArray();
					},
					async (attacker, waitToken) =>
					{
						if (!session.Api.World.Objects.TryGetValue(attacker, out BotKnownObject? observed) ||
							observed.Kind != BotKnownObjectKind.Npc)
							throw new InvalidDataException($"Q2005 respawn wait received an attack from " +
								$"unobserved object {attacker} at {session.CurrentPosition}.");
						if (++defendedPulls > 4)
							throw new InvalidDataException($"Q2005 respawn wait exceeded four ordinary " +
								$"defensive fights near Mijou at {session.CurrentPosition}.");
						session.TraceDiagnostic("defend-respawn-wait", new Dictionary<string, object?>
						{
							["attacker"] = attacker,
							["npcId"] = observed.TemplateId,
							["hp"] = session.Api.World.CurrentHp,
							["position"] = session.CurrentPosition,
						});
						bool killed = await combat.TryKillAsync(attacker, waitToken,
							new BotPosition(946.29f, 1702.67f, 259.625f, 0), attackHistoryStart);
						if (killed)
						{
							navigator.UnavailableObjects.Add(attacker);
							await combat.RestAsync(waitToken);
						}
					}, token);
			}
			int unreachableAreas = 0;
			for (int attempt = 0; ItemCount(session.Api.World, 182203006) < 3; attempt++)
			{
				// Route failures take no game time, so a pocket closed by observed packs could otherwise
				// rotate through the search areas forever. Bound the whole search loudly.
				if (attempt >= 120)
					throw new InvalidDataException($"Q2005 Stalker search exceeded 120 attempts at {session.CurrentPosition}: " +
						string.Join(" | ", searchNotes.TakeLast(8)));
				BotPosition isolatedStalker = successfulStalkerArea ??
					stalkerSearchAreas[attempt % stalkerSearchAreas.Length];
				session.BeginStep($"ni07-q2005-stalker-{attempt + 1}",
					$"fight-and-loot-gray-mane-stalker-at-level-{session.Api.World.Level}");
				await combat.RestAsync(token);
				int attackHistoryStart = session.PacketHistory.Count;
				if (combat.ReviveCount > routedRevives)
				{
					await WalkEasternRoadToDerotAsync();
					mijou = await ApproachShippedSpawnAsync(203540);
					routedRevives = combat.ReviveCount;
				}
				if (lastStalkerKilled)
				{
					await WaitForStalkerRespawnAsync(); // Normal shipped respawn, with client-visible defense.
					lastStalkerKilled = false;
				}
				NaturalNavigationObject? nearbyStalker = navigator.Observe().Npcs
					.Where(npc => npc.TemplateId is 210395 or 210396 or 210750 &&
						Distance(session.CurrentPosition, npc.Position) <= 23)
					.OrderBy(npc => Distance(session.CurrentPosition, npc.Position))
					.FirstOrDefault();
				if (nearbyStalker != null)
					isolatedStalker = nearbyStalker.Position; // Prefer a client-observed quest target already in spell range.
				int ingressEventStart = navigator.Events.Count;
				BotPosition ingressStart = session.CurrentPosition;
				NaturalNavigationResult safeStalkerArea = await NaturalIshalgenNavigator.ExploreWithinRangeAsync(
					contract.MapId, -1, isolatedStalker, 23, navigator, "shipped-stalker-search-area", token);
				if (!safeStalkerArea.Arrived &&
					safeStalkerArea.Reason == "No collision-checked route to the current destination." &&
					corridorClearAttempts++ < 12)
				{
					NaturalNavigationObject[] observed = navigator.Observe().Npcs.ToArray();
					var blocker = observed
						.Where(npc => NaturalHostility.IsAggressive(
							Aion.GameServer.Dataholders.DataManager.NPC_DATA.GetNpcTemplate(npc.TemplateId)) &&
							Distance(session.CurrentPosition, npc.Position) <= 30 &&
							!rejectedPullTargets.Contains(npc.ObjectId))
						.Select(npc => new { Npc = npc, Edge = FindCheckedFiringEdge(npc) })
						.Where(entry => entry.Edge != null)
						.OrderBy(entry => observed.Count(other => other.ObjectId != entry.Npc.ObjectId &&
							NaturalHostility.IsAggressive(
								Aion.GameServer.Dataholders.DataManager.NPC_DATA.GetNpcTemplate(other.TemplateId)) &&
							Distance(other.Position, entry.Npc.Position) < 9))
						.ThenBy(entry => Distance(session.CurrentPosition, entry.Npc.Position))
						.FirstOrDefault();
					if (blocker != null)
					{
						BotPosition[] firingEdge = blocker.Edge!;
						if (Distance(session.CurrentPosition, firingEdge[^1]) > 1)
						{
							navigator.Record(new NaturalNavigationEvent(navigator.Events.Count + 1,
								"line-of-sight-flank", "planned", "Checked short ground move to Priest spell range " +
								"outside other client-observed aggro circles.", contract.MapId, blocker.Npc.TemplateId,
								session.CurrentPosition, firingEdge[^1], blocker.Npc.ObjectId, 0, 0, firingEdge));
							await navigator.MoveAsync(firingEdge, token);
							await navigator.SynchronizeAsync(token);
						}
						NaturalNavigationObject? currentBlocker = navigator.Observe().Npcs
							.FirstOrDefault(npc => npc.ObjectId == blocker.Npc.ObjectId);
						if (currentBlocker == null ||
							Distance(session.CurrentPosition, currentBlocker.Position) > 25 ||
							!geometry.HasLineOfSight(contract.MapId, session.CurrentPosition, currentBlocker.Position))
						{
							rejectedPullTargets.Add(blocker.Npc.ObjectId);
							searchNotes.Add($"corridor {corridorClearAttempts}: firing target moved or lost after checked flank");
							continue;
						}
						bool cleared = await combat.TryKillAsync(blocker.Npc.ObjectId, token,
							new BotPosition(946.253f, 1702.775f, 259.625f, 0));
						searchNotes.Add($"corridor {corridorClearAttempts}: " +
							$"{blocker.Npc.TemplateId}/{blocker.Npc.ObjectId} at {session.CurrentPosition}, " +
							$"killed={cleared}, HP={session.Api.World.CurrentHp}/{session.Api.World.MaxHp}");
						if (cleared)
						{
							navigator.UnavailableObjects.Add(blocker.Npc.ObjectId);
							if (blocker.Npc.TemplateId is 210395 or 210396 or 210750)
							{
								// The corridor guard is itself a shipped Odella source. A
								// non-drop still needs another kill; do not discard its corpse
								// merely because this fight started as route clearing.
								await TryLootCorpseItemAsync(session, blocker.Npc.ObjectId, 182203006, token);
								lastStalkerKilled = true;
								successfulStalkerArea = blocker.Npc.Position;
								preferredAreaMisses = 0;
								if (!await RestAtObservedFieldCampAsync())
								{
									mijou = await ReturnToMijouAlongIngressAsync(ingressEventStart, ingressStart);
								}
							}
						}
						else rejectedPullTargets.Add(blocker.Npc.ObjectId);
						if (combat.ReviveCount > routedRevives)
						{
							await WalkEasternRoadToDerotAsync();
							routedRevives = combat.ReviveCount;
						}
						attempt--; // The same Stalker search remains pending after a corridor fight.
						continue;
					}
				}
				if (!safeStalkerArea.Arrived)
				{
					searchNotes.Add($"attempt {attempt + 1} at {isolatedStalker}: {safeStalkerArea.Reason} " +
						$"from {session.CurrentPosition}; route={navigator.LastRouteDiagnostic}; " +
						$"nearby={string.Join(',', navigator.Observe().Npcs.Where(npc =>
							Distance(npc.Position, session.CurrentPosition) < 30)
							.Select(npc => $"{npc.TemplateId}@{Distance(npc.Position, session.CurrentPosition):F1}"))}");
					if (successfulStalkerArea != null && ++preferredAreaMisses >= 2)
						successfulStalkerArea = null;
					if (++unreachableAreas >= stalkerSearchAreas.Length)
					{
						// Every shipped area is closed from here: leave the pocket the ordinary way
						// (checked walk through observed guards, else the learned Return skill).
						unreachableAreas = 0;
						corridorClearAttempts = 0;
						mijou = await ReachMijouFromFieldAsync();
					}
					continue; // Try another shipped area; one blocked corridor is not proof all are blocked.
				}
				unreachableAreas = 0;
				await session.SynchronizeAsync(token);
				NaturalNavigationObject? observedStalker = navigator.Observe().Npcs
					.Where(npc => npc.TemplateId is 210395 or 210396 or 210750 &&
						Distance(session.CurrentPosition, npc.Position) <= 30)
					.OrderBy(npc => Distance(npc.Position, isolatedStalker))
					.FirstOrDefault();
				if (observedStalker == null)
				{
					searchNotes.Add($"attempt {attempt + 1} at {isolatedStalker}: no Stalker in client view");
					if (successfulStalkerArea != null && ++preferredAreaMisses >= 2)
						successfulStalkerArea = null;
					mijou = await ApproachShippedSpawnAsync(203540);
					continue; // A patrolling target may be outside this bounded scan; try another area.
				}
				// Choose the Stalker and the spell-range spot that bring no helpers (Java assist rule), off to the
				// side of any pack, and walk there before pulling; a planned clean pull replaces the blanket veto.
				NaturalPullPlan? stalkerPull = null;
				if (session.Api.World.CurrentHp * 100 >= session.Api.World.MaxHp * 90)
				{
					NaturalNavigationObject[] stalkers = navigator.Observe().Npcs
						.Where(npc => npc.TemplateId is 210395 or 210396 or 210750 &&
							Distance(session.CurrentPosition, npc.Position) <= 35)
						.OrderBy(npc => Distance(npc.Position, isolatedStalker)).ToArray();
					stalkerPull = await MoveToPullSpotAsync(stalkers, [], "q2005-stalker");
					if (stalkerPull is { Helpers.Count: 0 } &&
						navigator.Observe().Npcs.FirstOrDefault(npc => npc.ObjectId == stalkerPull.Target.Npc.ObjectId) is { } planned)
						observedStalker = planned;
				}
				bool cleanPull = stalkerPull is { Helpers.Count: 0 } && observedStalker.ObjectId == stalkerPull.Target.Npc.ObjectId;
				NaturalNavigationObject[] nearbyHostiles = cleanPull ? [] : navigator.Observe().Npcs
					.Where(npc => npc.ObjectId != observedStalker.ObjectId &&
						NaturalHostility.IsAggressive(Aion.GameServer.Dataholders.DataManager.NPC_DATA.GetNpcTemplate(npc.TemplateId)) &&
						Distance(session.CurrentPosition, npc.Position) < 12)
					.ToArray();
				if (nearbyHostiles.Length > 0 ||
					session.Api.World.CurrentHp * 100 < session.Api.World.MaxHp * 90)
				{
					searchNotes.Add($"attempt {attempt + 1} at {isolatedStalker}: " +
						$"unsafe HP={session.Api.World.CurrentHp}/{session.Api.World.MaxHp}, " +
						$"nearby={string.Join(',', nearbyHostiles.Select(npc => npc.TemplateId))}");
					if (successfulStalkerArea != null && ++preferredAreaMisses >= 2)
						successfulStalkerArea = null;
					int[] attackingIds = session.PacketHistory.Skip(attackHistoryStart)
						.Where(packet => packet.PacketType == typeof(SM_ATTACK) &&
							packet.Get<int>("targetObjId") == session.CharacterId)
						.Select(packet => packet.Get<int>("attackerObjId")).Distinct()
						.Where(attacker => navigator.Observe().Npcs.Any(npc => npc.ObjectId == attacker &&
							Distance(session.CurrentPosition, npc.Position) < 30)).ToArray();
					bool attacked = attackingIds.Length != 0;
					searchNotes.Add($"attempt {attempt + 1}: client-observed attack={attacked}; " +
						$"returning on checked ingress");
					if (attacked)
						await combat.EscapeAsync(new BotPosition(946.29f, 1702.67f, 259.625f, 0),
							attackingIds.ToHashSet(), token);
					mijou = await ReturnToMijouAlongIngressAsync(ingressEventStart, ingressStart);
					if (combat.ReviveCount > routedRevives)
					{
						await WalkEasternRoadToDerotAsync();
						routedRevives = combat.ReviveCount;
					}
					continue;
				}
				int target = observedStalker.ObjectId;
				bool killed = await combat.TryKillAsync(target, token,
					new BotPosition(946.29f, 1702.67f, 259.625f, 0), attackHistoryStart);
				searchNotes.Add($"attempt {attempt + 1} at {isolatedStalker}: " +
					$"target={observedStalker.TemplateId}/{target}, killed={killed}, " +
					$"HP={session.Api.World.CurrentHp}/{session.Api.World.MaxHp}");
				if (killed)
				{
					navigator.UnavailableObjects.Add(target);
					lastStalkerKilled = true;
					successfulStalkerArea = isolatedStalker;
					preferredAreaMisses = 0;
					await TryLootCorpseItemAsync(session, target, 182203006, token);
				}
				else if (successfulStalkerArea != null && ++preferredAreaMisses >= 2)
					successfulStalkerArea = null;
				if (combat.ReviveCount > routedRevives)
				{
					await WalkEasternRoadToDerotAsync();
					routedRevives = combat.ReviveCount;
				}
				if (killed && await RestAtObservedFieldCampAsync()) continue;
				mijou = await ReturnToMijouAlongIngressAsync(ingressEventStart, ingressStart);
			}
			Assert.True(ItemCount(session.Api.World, 182203006) == 3,
				$"Q2005 needs three earned Odella; observed {ItemCount(session.Api.World, 182203006)}. " +
				string.Join(" | ", searchNotes));
			session.BeginStep("ni07-q2005-mijou-finish", "show-odella-and-claim-priest-reward");
			mijou = await ApproachShippedSpawnAsync(203540);
			await OpenQuestDialogAsync(mijou, 2005);
			await session.SendPacketAsync(session.Api.SelectDialog(mijou,
				DialogAction.CHECK_USER_HAS_QUEST_ITEM, questId: 2005), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(4, session.Api.World.Quests[2005].Status);
			string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
			var inventory = NaturalIshalgenInventoryPolicy.Load(root,
				session.Api.World.Inventory.Values.Select(item => item.ItemId));
			int rewardIndex = inventory.ChooseReward(2005, session.Api.World.Level,
				session.Api.World.Inventory.Values);
			Assert.True(rewardIndex >= 0);
			await FinishStandardQuestAsync(session, mijou, 2005, token,
				DialogAction.SELECTED_QUEST_REWARD1 + rewardIndex);
			Assert.Contains(2005, session.Api.World.CompletedQuestIds);
		}

		async Task CompleteHitThemWhereItHurtsAsync()
		{
			// Java ce54b7931 _2006HitThemWhereitHurts and QuestItemNpcAI:
			// an ordinary USE_OBJECT on a quest_use_item sack creates its corpse loot.
			await WaitForQuestStatusAsync(session, 2006, 3, token);
			session.BeginStep("ni07-q2006-mijou-start", "ask-mijou-about-mau-grain");
			int mijou = await ApproachShippedSpawnAsync(203540);
			await OpenQuestDialogAsync(mijou, 2006);
			await session.SendPacketAsync(session.Api.SelectDialog(mijou, DialogAction.SETPRO1, questId: 2006), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(1, session.Api.World.Quests[2006].StepAndFlags);
			BotPosition ingressStart = session.CurrentPosition;
			int ingressEventStart = navigator.Events.Count;
			BotPosition[] firstSackIngress = [];
			for (int attempt = 1; ItemCount(session.Api.World, 182203008) < 3; attempt++)
			{
				session.BeginStep($"ni07-q2006-sack-{attempt}", "walk-to-client-observed-mau-sack-and-loot-grain");
				if (attempt == 1) await combat.RestAsync(token); // Mijou-side refuge; do not sit in the Mau field.
				int sack = await ApproachShippedSpawnAsync(700095, skipBlockedTarget: true);
				int interactionPacketStart = session.PacketHistory.Count;
				await session.SendPacketAsync(session.Api.TalkTo(sack), token);
				await session.WaitForPacketAsync(typeof(SM_EMOTION), token,
					packet => packet.Get<int>("senderObjectId") == session.CharacterId &&
						packet.Get<byte>("emotionType") == (byte)EmotionType.START_QUESTLOOT);
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(3001), token);
				await session.SynchronizeAsync(token);
				DecodedBotServerPacket? finish = session.PacketHistory.Skip(interactionPacketStart)
					.LastOrDefault(packet => packet.PacketType == typeof(SM_USE_OBJECT) &&
						packet.Get<int>("targetObjectId") == sack &&
						packet.Get<byte>("actionType") == 2);
				bool completedUse = finish?.Get<int>("durationMs") == 3000;
				bool looted = completedUse &&
					await TryLootCorpseItemAsync(session, sack, 182203008, token);
				if (completedUse) navigator.UnavailableObjects.Add(sack);
				if (firstSackIngress.Length == 0)
					firstSackIngress = NaturalIshalgenNavigator.SelectRetraceCheckpoints(
						navigator.Events.Skip(ingressEventStart));
				if (!looted)
					session.TraceDiagnostic(completedUse ? "sack-without-grain" : "sack-use-interrupted",
						new Dictionary<string, object?>
					{
						["objectId"] = sack,
						["durationMs"] = finish?.Get<int>("durationMs"),
						["inventoryCount"] = ItemCount(session.Api.World, 182203008),
						["position"] = session.CurrentPosition,
					});
				int[] attackers = session.PacketHistory.Skip(interactionPacketStart)
					.Where(packet => packet.PacketType == typeof(SM_ATTACK) &&
						packet.Get<int>("targetObjId") == session.CharacterId)
					.Select(packet => packet.Get<int>("attackerObjId")).Distinct().Take(4).ToArray();
				foreach (int attacker in attackers)
				{
					if (!session.Api.World.Objects.TryGetValue(attacker, out BotKnownObject? hostile) ||
						hostile.Kind != BotKnownObjectKind.Npc ||
						Distance(session.CurrentPosition, hostile.Position) > 30) continue;
					if (await combat.TryKillAsync(attacker, token, ingressStart,
						interactionPacketStart))
						navigator.UnavailableObjects.Add(attacker);
				}
			}
			Assert.Equal(3, ItemCount(session.Api.World, 182203008));
			session.BeginStep("ni07-q2006-mijou-check", "show-earned-mau-grain-to-mijou");
			if (firstSackIngress.Length > 0)
			{
				NaturalNavigationResult retrace = await NaturalIshalgenNavigator.RetraceIngressAsync(
					contract.MapId, ingressStart, firstSackIngress, navigator, token);
					session.TraceDiagnostic("q2006-checked-ingress-return", new Dictionary<string, object?>
				{
					["arrived"] = retrace.Arrived,
					["reason"] = retrace.Reason,
					["checkpoints"] = firstSackIngress.Length,
						["position"] = session.CurrentPosition,
					});
				if (!retrace.Arrived)
				{
					await UseLearnedReturnToBindAsync();
					await WalkEasternRoadToDerotAsync();
				}
			}
			mijou = await ApproachShippedNpcThroughObservedGuardsAsync(203540, maximumGuardClears: 12);
			await OpenQuestDialogAsync(mijou, 2006);
			await session.SendPacketAsync(session.Api.SelectDialog(mijou,
				DialogAction.CHECK_USER_HAS_QUEST_ITEM, questId: 2006), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(4, session.Api.World.Quests[2006].Status);
			session.BeginStep("ni07-q2006-ulgorn-reward", "return-to-ulgorn-and-claim-priest-reward");
			Assert.True(easternRoadIngressStart is BotPosition && easternRoadIngress.Length > 0,
				"Q2006 needs the earlier naturally walked eastern-road return breadcrumbs.");
			NaturalNavigationResult easternReturn = await NaturalIshalgenNavigator.RetraceIngressAsync(
				contract.MapId, easternRoadIngressStart.Value, easternRoadIngress, navigator, token);
			session.TraceDiagnostic("eastern-road-return", new Dictionary<string, object?>
			{
				["arrived"] = easternReturn.Arrived,
				["reason"] = easternReturn.Reason,
				["checkpoints"] = easternRoadIngress.Length,
				["position"] = session.CurrentPosition,
			});
			Assert.True(easternReturn.Arrived, easternReturn.Reason);
			int ulgorn = await ApproachShippedSpawnAsync(203516);
			string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
			var inventory = NaturalIshalgenInventoryPolicy.Load(root,
				session.Api.World.Inventory.Values.Select(item => item.ItemId));
			int rewardIndex = inventory.ChooseReward(2006, session.Api.World.Level,
				session.Api.World.Inventory.Values);
			Assert.True(rewardIndex >= 0);
			await FinishStandardQuestAsync(session, ulgorn, 2006, token,
				DialogAction.SELECTED_QUEST_REWARD1 + rewardIndex);
			Assert.Contains(2006, session.Api.World.CompletedQuestIds);
		}

		async Task CompleteWheresRaeThisTimeAsync()
		{
			// Java ce54b7931 _2007WheresRaeThisTime: all transitions are ordinary
			// NPC dialogue, quest_use_item interaction, or quest-granted teleport.
			await WaitForQuestStatusAsync(session, 2007, 3, token);
			(int npcId, int action, int step, string name)[] conversations =
			[
				(203516, DialogAction.SETPRO1, 1, "ulgorn"),
				(203519, DialogAction.SETPRO2, 2, "nobekk"),
				(203539, DialogAction.SETPRO3, 3, "derot"),
				(203552, DialogAction.SETPRO4, 4, "nalto"),
				(203554, DialogAction.SETPRO5, 5, "rae"),
			];
			// A player who dies in the Rae/Nalto camps walks back from bind and tries again. Ten deaths at one
			// objective is a finding worth stopping for; two was not.
			const int MaximumQ2007Recoveries = 10;
			async Task<int> ApproachGuardedCampaignNpcAsync(int templateId)
			{
				for (int recovery = 0; recovery <= MaximumQ2007Recoveries; recovery++)
				{
					try
					{
						if (recovery > 0)
						{
							// A death returns to bind. Never treat bind-area monsters as
							// guards of the distant Rae/Nalto corridor.
							session.BeginStep($"ni07-q2007-rejoin-{templateId}-{recovery}",
								"rest-at-bind-and-walk-previously-checked-eastern-road");
							await combat.RestAsync(token);
							// Only a Priest still near bind needs the eastern road. The navigator may already have
							// walked back south after the revive; turning round to Nobekk from Rae's camp would be
							// a 2 km detour through every camp on the way.
							if (Distance(session.CurrentPosition, new BotPosition(571.0388f, 2787.342f, 299.875f, 0)) < 400)
								await WalkEasternRoadToDerotAsync();
							// Rae and her generators lie past Nalto's camp: rejoin through Nalto, and for a generator
							// through Rae as well, one guarded leg at a time, as the first visit did.
							if (templateId is 203554 or 700085 or 700086 or 700087)
								await ApproachShippedNpcThroughObservedGuardsAsync(203552, maximumGuardClears: 8);
							if (templateId is 700085 or 700086 or 700087)
								await ApproachShippedNpcThroughObservedGuardsAsync(203554, maximumGuardClears: 8);
						}
						return await ApproachShippedNpcThroughObservedGuardsAsync(templateId,
							maximumGuardClears: 8);
					}
					catch (NaturalGuardedObjectiveRevivedException exception) when (recovery < MaximumQ2007Recoveries)
					{
					session.TraceDiagnostic("q2007-guarded-objective-revive", new Dictionary<string, object?>
						{
							["npcTemplateId"] = templateId,
							["recovery"] = recovery + 1,
							["position"] = session.CurrentPosition,
							["reason"] = exception.Message,
						});
					}
					catch (InvalidDataException exception) when (recovery < MaximumQ2007Recoveries &&
						exception.Message.StartsWith("No checked guarded approach", StringComparison.Ordinal))
					{
						// Every checked way in is closed right now (a patrol in the corridor, a camp that has not
						// respawned into a pullable shape). Back off, rest away from respawns, let patrols move,
						// and try again, as a player would, within the same recovery bound.
						session.TraceDiagnostic("q2007-guarded-objective-wait", new Dictionary<string, object?>
						{
							["npcTemplateId"] = templateId,
							["recovery"] = recovery + 1,
							["position"] = session.CurrentPosition,
							["reason"] = exception.Message.Length > 300 ? exception.Message[..300] : exception.Message,
						});
						await combat.RestAsync(token);
						await session.AdvanceAsync(TimeSpan.FromSeconds(30), token);
						await session.SynchronizeAsync(token);
					}
				}
				throw new InvalidDataException($"Q2007 guarded NPC {templateId} exhausted {MaximumQ2007Recoveries} recoveries.");
			}
			foreach (var (npcId, action, step, name) in conversations)
			{
				session.BeginStep($"ni07-q2007-{name}", "walk-to-quest-npc-and-advance-dialogue");
				if (npcId == 203539)
					await WalkEasternRoadToDerotAsync(); // Reuse the earlier checked road, not the blocked direct valley.
				int npc = npcId is 203552 or 203554
					? await ApproachGuardedCampaignNpcAsync(npcId)
					: await ApproachShippedSpawnAsync(npcId);
				await OpenQuestDialogAsync(npc, 2007);
				await session.SendPacketAsync(session.Api.SelectDialog(npc, checked((ushort)action), questId: 2007), token);
				await session.SynchronizeAsync(token);
				Assert.Equal(step, session.Api.World.Quests[2007].StepAndFlags);
			}
			foreach (var (npcId, step, color) in new[]
			{
				(700085, 6, "green"), (700086, 7, "blue"), (700087, 8, "violet"),
			})
			{
				session.BeginStep($"ni07-q2007-{color}-generator", "walk-to-and-use-quest-generator");
				// Generators are quest_use_item objects (like the Mau sacks): the quest advances only when the
				// ordinary use bar completes (SM_USE_OBJECT action 2 with its full duration). An attack aborts
				// the use (duration 0); defend and use again.
				for (int use = 1; session.Api.World.Quests[2007].StepAndFlags < step; use++)
				{
					if (use > 10) Assert.Fail($"Q2007 {color} generator use did not complete after ten attempts.");
					// Clear around the generator's spot from range before stepping onto it (the recorded human's
					// order), then approach with the same walk-back-after-death recovery as Rae and Nalto.
					BotPosition? generatorHint = graph.GetMap(contract.MapId)!.Waypoints
						.Where(waypoint => waypoint.TemplateId == npcId)
						.OrderBy(waypoint => Distance(session.CurrentPosition, waypoint.Position))
						.Select(waypoint => (BotPosition?)waypoint.Position).FirstOrDefault();
					// Never enter the camp low or without a potion in the bag (the recorded human topped up first).
					if (session.Api.World.CurrentHp * 100 < session.Api.World.MaxHp * 80 ||
						NaturalIshalgenPotionPolicy.SelectOwnedPotion(session.Api.World.Inventory.Values) == null)
						await combat.RestAsync(token);
					await ClearAroundSpotAsync(generatorHint, null, $"q2007-{color}-generator-approach");
					int generator = await ApproachGuardedCampaignNpcAsync(npcId);
					// A player clears what stands near an object before a 3 s use bar a single hit interrupts.
					await ClearAroundObjectiveAsync(generator, $"q2007-{color}-generator");
					if (session.Api.World.IsDead || !navigator.Observe().Npcs.Any(npc => npc.ObjectId == generator &&
						Distance(session.CurrentPosition, npc.Position) <= 3)) continue; // walked off while clearing
					int usePacketStart = session.PacketHistory.Count;
					await session.SendPacketAsync(session.Api.TalkTo(generator), token);
					await session.SynchronizeAsync(token);
					DecodedBotServerPacket? started = session.PacketHistory.Skip(usePacketStart)
						.LastOrDefault(packet => packet.PacketType == typeof(SM_USE_OBJECT) &&
							packet.Get<int>("targetObjectId") == generator && packet.Get<byte>("actionType") != 2);
					int durationMs = started?.Get<int>("durationMs") ?? 3000;
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(Math.Max(durationMs, 1) + 1), token);
					await session.SynchronizeAsync(token);
					if (session.Api.World.Quests[2007].StepAndFlags >= step) break;
					session.TraceDiagnostic("generator-use-incomplete", new Dictionary<string, object?>
					{
						["generator"] = npcId,
						["use"] = use,
						["durationMs"] = durationMs,
						["questStep"] = session.Api.World.Quests[2007].StepAndFlags,
						["position"] = session.CurrentPosition,
					});
					// A death here is an ordinary death: revive at bind, walk back and use the generator again.
					await DefendAgainstEngagedAsync($"q2007-{color}-generator");
				}
				Assert.Equal(step, session.Api.World.Quests[2007].StepAndFlags);
			}
			Assert.Contains(session.PacketHistory, packet => packet.PacketType == typeof(SM_PLAY_MOVIE) &&
				packet.Get<int>("cutsceneId") == 56);
			session.BeginStep("ni07-q2007-rae-return", "ask-rae-for-quest-transport-back-to-ulgorn");
			int rae = await ApproachShippedSpawnAsync(203554);
			await OpenQuestDialogAsync(rae, 2007);
			session.Api.World.BeginWorldReload();
			await session.SendPacketAsync(session.Api.SelectDialog(rae, DialogAction.SETPRO6, questId: 2007), token);
			// Java TeleportService.teleportToNpc(player, 203516) stays on Ishalgen, and a same-map teleport
			// (SpawnTask.run -> spawnOnSameMap) sends SM_CHANNEL_INFO and SM_PLAYER_INFO but no SM_PLAYER_SPAWN.
			await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token,
				packet => packet.Get<int>("objectId") == session.CharacterId);
			session.AcceptTeleportPosition();
			Assert.Equal(4, session.Api.World.Quests[2007].Status);
			session.BeginStep("ni07-q2007-ulgorn-reward", "claim-priest-reward-after-quest-transport");
			int ulgorn = await ApproachShippedSpawnAsync(203516);
			string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
			var inventory = NaturalIshalgenInventoryPolicy.Load(root,
				session.Api.World.Inventory.Values.Select(item => item.ItemId));
			int rewardIndex = inventory.ChooseReward(2007, session.Api.World.Level,
				session.Api.World.Inventory.Values);
			Assert.True(rewardIndex >= 0);
			await FinishStandardQuestAsync(session, ulgorn, 2007, token,
				DialogAction.SELECTED_QUEST_REWARD1 + rewardIndex);
			Assert.Contains(session.PacketHistory, packet => packet.PacketType == typeof(SM_PLAY_MOVIE) &&
				packet.Get<int>("cutsceneId") == 58);
			Assert.Contains(2007, session.Api.World.CompletedQuestIds);
		}

		async Task CompleteSparkleAndShineAsync()
		{
			// Java ce54b7931 ItemCollecting and ishalgen.xml Q2105; drops
			// come from ordinary client-observed 210367 kills and corpse loot.
			session.BeginStep("ni07-q2105-start", "accept-sparkie-collection-at-vanar");
			int vanarNpc = await ApproachShippedSpawnAsync(203502);
			await session.StartQuestAsync(vanarNpc, 2105, token);
			for (int attempt = 0; ItemCount(session.Api.World, 182203105) < 3; attempt++)
			{
				session.BeginStep($"ni07-q2105-kill-{attempt + 1}", "fight-and-check-sparkie-drop");
				int target = await ApproachShippedSpawnAsync(210367);
				await combat.KillAsync(target, token);
				navigator.UnavailableObjects.Add(target);
				await TryLootCorpseItemAsync(session, target, 182203105, token);
				await combat.RestAsync(token);
			}
			Assert.Equal(3, ItemCount(session.Api.World, 182203105));
			session.BeginStep("ni07-q2105-finish", "return-to-vanar-and-turn-in");
			vanarNpc = await ApproachShippedSpawnAsync(203502);
			await FinishItemQuestAsync(session, vanarNpc, 2105, token);
			Assert.Equal(5, session.Api.World.Quests[2105].Status);
			Assert.Equal(0, ItemCount(session.Api.World, 182203105));
		}

		async Task CompleteVanarsFlatteryAsync()
		{
			// Java ce54b7931 _2106VanarsFlattery: Vanar gives the letter,
			// SETPRO1 advances it, and 203517 accepts the ordinary turn-in.
			session.BeginStep("ni07-q2106-start", "accept-vanars-letter-without-creating-an-item");
			int vanarNpc = await ApproachShippedSpawnAsync(203502);
			await session.StartQuestAsync(vanarNpc, 2106, token);
			Assert.Equal(1, ItemCount(session.Api.World, 182203106));
			session.BeginStep("ni07-q2106-vanar-report", "confirm-letter-with-vanar");
			await OpenQuestDialogAsync(vanarNpc, 2106);
			await session.SendPacketAsync(session.Api.SelectDialog(vanarNpc, DialogAction.SETPRO1, questId: 2106), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(4, session.Api.World.Quests[2106].Status);
			session.BeginStep("ni07-q2106-delivery", "walk-to-recipient-and-deliver-vanars-letter");
			int recipient = await ApproachShippedSpawnAsync(203517);
			await FinishStandardQuestAsync(session, recipient, 2106, token);
			Assert.Contains(2106, session.Api.World.CompletedQuestIds);
		}

		async Task CompleteInsectProblemAsync()
		{
			// Java ce54b7931 _2114TheInsectProblem: the SETPRO1 branch
			// requires ten ordinary Grove Sparkie kills before Motgar's reward.
			session.BeginStep("ni07-q2114-motgar-start", "choose-grove-sparkie-insect-problem");
			int motgar = await ApproachShippedSpawnAsync(203533);
			await OpenQuestDialogAsync(motgar, 2114);
			await session.SendPacketAsync(session.Api.SelectDialog(motgar, DialogAction.SETPRO1, questId: 2114), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(1, session.Api.World.Quests[2114].StepAndFlags);
			for (int kill = 0; kill < 10; kill++)
			{
				session.BeginStep($"ni07-q2114-sparkie-{kill + 1}", "fight-grove-sparkie-for-motgar");
				int target = await ApproachShippedSpawnAsync(210734);
				await combat.KillAsync(target, token);
				navigator.UnavailableObjects.Add(target);
				await combat.RestAsync(token);
			}
			Assert.Equal(4, session.Api.World.Quests[2114].Status);
			session.BeginStep("ni07-q2114-motgar-reward", "return-to-motgar-for-insect-reward");
			motgar = await ApproachShippedSpawnAsync(203533);
			await FinishStandardQuestAsync(session, motgar, 2114, token);
			Assert.Contains(2114, session.Api.World.CompletedQuestIds);
		}

		async Task CompleteImprisonedGourmetAsync()
		{
			// Java ce54b7931 _2123TheImprisonedGourmet: a Methu Egg
			// supplies item 182203122; SETPRO2 selects reward group one.
			session.BeginStep("ni07-q2123-munin-start", "accept-munins-gourmet-request");
			int munin = await ApproachShippedSpawnAsync(203550);
			await session.StartQuestAsync(munin, 2123, token);
			session.BeginStep("ni07-q2123-methu-egg", "walk-to-and-loot-client-observed-methu-egg");
			int egg = await UseAndLootQuestObjectAsync(700128, 182203122, skipBlockedTarget: true);
			navigator.UnavailableObjects.Add(egg);
			Assert.Equal(1, ItemCount(session.Api.World, 182203122));
			session.BeginStep("ni07-q2123-munin-reward", "give-methu-egg-to-munin-without-ascension-dialogue");
			munin = await ApproachShippedSpawnAsync(203550);
			await OpenQuestDialogAsync(munin, 2123);
			await session.SendPacketAsync(session.Api.SelectDialog(munin, DialogAction.SETPRO2, questId: 2123), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(4, session.Api.World.Quests[2123].Status);
			Assert.Equal(0, ItemCount(session.Api.World, 182203122));
			await FinishStandardQuestAsync(session, munin, 2123, token);
			Assert.Contains(2123, session.Api.World.CompletedQuestIds);
		}

		async Task CompleteRobberyPlotAsync()
		{
			// Java ce54b7931 _2125TheRobberyPlot: Mijou -> Hephe -> Alfrigh.
			session.BeginStep("ni07-q2125-mijou-start", "accept-robbery-investigation");
			int mijou = await ApproachShippedSpawnAsync(203540);
			await session.StartQuestAsync(mijou, 2125, token);
			session.BeginStep("ni07-q2125-hephe", "ask-hephe-about-robbery");
			int hephe = await ApproachShippedSpawnAsync(203514);
			await OpenQuestDialogAsync(hephe, 2125);
			await session.SendPacketAsync(session.Api.SelectDialog(hephe, DialogAction.SETPRO1, questId: 2125), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(1, session.Api.World.Quests[2125].StepAndFlags);
			session.BeginStep("ni07-q2125-alfrigh", "report-investigation-to-alfrigh");
			int alfrigh = await ApproachShippedSpawnAsync(203543);
			await FinishStandardQuestAsync(session, alfrigh, 2125, token);
			Assert.Contains(2125, session.Api.World.CompletedQuestIds);
		}

		async Task CompleteForLoveOfNegiAsync()
		{
			// Java ce54b7931 _2135ForLoveofNegi: Bolir gives the
			// letter, Negi takes it, and Bolir grants the ordinary reward.
			session.BeginStep("ni07-q2135-bolir-start", "accept-bolirs-letter");
			int bolir = await ApproachShippedSpawnAsync(203532);
			await session.StartQuestAsync(bolir, 2135, token);
			Assert.Equal(1, ItemCount(session.Api.World, 182203131));
			session.BeginStep("ni07-q2135-negi", "deliver-bolirs-letter-to-negi");
			int negi = await ApproachShippedSpawnAsync(203531);
			await OpenQuestDialogAsync(negi, 2135);
			await session.SendPacketAsync(session.Api.SelectDialog(negi, DialogAction.SETPRO1, questId: 2135), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(1, session.Api.World.Quests[2135].StepAndFlags);
			Assert.Equal(0, ItemCount(session.Api.World, 182203131));
			session.BeginStep("ni07-q2135-bolir-reward", "return-to-bolir-for-delivery-reward");
			bolir = await ApproachShippedSpawnAsync(203532);
			await FinishStandardQuestAsync(session, bolir, 2135, token);
			Assert.Contains(2135, session.Api.World.CompletedQuestIds);
		}

		async Task CompleteTemplateQuestAsync(QuestRunPlan plan)
		{
			// Java ce54b7931 ItemCollecting, MonsterHunt, ReportTo. The plan
			// supplies shipped objectives only; this executor never uses Q4I's
			// Prepare grants, forced NPC HP, skill grants, neutrality or teleports.
			QuestRunBook book = QuestRunBook.Build(plan);
			for (int index = 0; index < book.Operations.Count; index++)
			{
				QuestRunOperation operation = book.Operations[index];
				session.BeginStep($"ni07-q{plan.Id}-{index}-{operation.Kind}",
					$"natural-template-{plan.Template}-{operation.Kind}");
				switch (operation.Kind)
				{
					case QuestRunOperationKind.Prepare:
						Assert.True(plan.FinishedQuestGroups.Count == 0 ||
							plan.FinishedQuestGroups.Any(group => group.All(session.Api.World.CompletedQuestIds.Contains)),
							$"Q{plan.Id} prerequisites must be earned before starting.");
						break;
					case QuestRunOperationKind.StartAtNpc:
					{
						int starterId = operation.Npcs?.FirstOrDefault()?.Id ??
							throw new InvalidDataException($"Q{plan.Id} has no shipped NPC starter.");
						int starter = await ApproachShippedSpawnAsync(starterId);
						await session.StartQuestAsync(starter, plan.Id, token);
						Assert.Equal(3, session.Api.World.Quests[plan.Id].Status);
						break;
					}
					case QuestRunOperationKind.Kill:
					{
						// Quest data can list alternatives that never spawn on this map (Q2117 names 210389 and
						// 210655; only 210389 has spawns): hunt what is actually there, as a player would.
						int[] targetIds = (operation.Npcs?.Select(npc => npc.Id).Distinct() ?? [])
							.Where(SpawnsOnMap).ToArray();
						if (targetIds.Length == 0) throw new InvalidDataException($"Q{plan.Id} kill has no shipped target.");
						for (int kill = 0; kill < operation.Count; kill++)
						{
							int target = await ApproachShippedSpawnAsync(targetIds[kill % targetIds.Length]);
							await combat.KillAsync(target, token);
							navigator.UnavailableObjects.Add(target);
							await combat.RestAsync(token);
						}
						break;
					}
					case QuestRunOperationKind.CollectQuestDrop:
					case QuestRunOperationKind.UseQuestObject:
					{
						int[] sourceIds = operation.Sources?.Select(source => source.Npc?.Id ?? 0)
							.Where(id => id > 0 && SpawnsOnMap(id)).Distinct().ToArray() ?? [];
						if (sourceIds.Length == 0)
							throw new InvalidDataException($"Q{plan.Id} item {operation.ItemId} has no shipped source.");
						for (int attempt = 0; ItemCount(session.Api.World, operation.ItemId) < operation.Count;
							attempt++)
						{
							int sourceId = sourceIds[attempt % sourceIds.Length];
							if (sourceId >= 700000)
							{
								navigator.UnavailableObjects.Add(
									await UseAndLootQuestObjectAsync(sourceId, operation.ItemId, skipBlockedTarget: true));
								continue;
							}
							int source = await ApproachShippedSpawnAsync(sourceId);
							await combat.KillAsync(source, token);
							await TryLootCorpseItemAsync(session, source, operation.ItemId, token);
							await combat.RestAsync(token);
							navigator.UnavailableObjects.Add(source);
						}
						Assert.Equal(operation.Count, ItemCount(session.Api.World, operation.ItemId));
						break;
					}
					case QuestRunOperationKind.Report:
						// These frozen plans use only a final report; ClaimReward performs its NPC dialogue.
						break;
					case QuestRunOperationKind.Gather:
						await GatherYoungAzphaAsync(operation);
						break;
					case QuestRunOperationKind.ClaimReward:
					{
						int recipientId = operation.Npcs?.FirstOrDefault()?.Id ??
							throw new InvalidDataException($"Q{plan.Id} has no shipped reward NPC.");
						int recipient = await ApproachShippedSpawnAsync(recipientId);
						int rewardAction = DialogAction.SELECTED_QUEST_NOREWARD;
						if (plan.HasSelectableReward)
						{
							string root = Aion.GameServer.TestKit.RealStaticData.RepoRoot();
							var inventory = NaturalIshalgenInventoryPolicy.Load(root,
								session.Api.World.Inventory.Values.Select(item => item.ItemId));
							int rewardIndex = inventory.ChooseReward(plan.Id, session.Api.World.Level,
								session.Api.World.Inventory.Values);
							Assert.True(rewardIndex >= 0);
							rewardAction = DialogAction.SELECTED_QUEST_REWARD1 + rewardIndex;
						}
						if (plan.Template == "item_collecting")
							await FinishItemQuestAsync(session, recipient, plan.Id, token, rewardAction);
						else
							await FinishStandardQuestAsync(session, recipient, plan.Id, token, rewardAction);
						Assert.Contains(plan.Id, session.Api.World.CompletedQuestIds);
						break;
					}
					default:
						throw new InvalidDataException($"Q{plan.Id} needs natural {operation.Kind} capability.");
				}
			}
		}

		async Task GatherYoungAzphaAsync(QuestRunOperation operation)
		{
			if (operation.ItemId != GatheringTarget.YoungAzpha.ItemId || operation.Count != 3 ||
				operation.Source?.GatherableId != GatheringTarget.YoungAzpha.TemplateId)
				throw new InvalidDataException("Natural gathering only supports the frozen Q2133 Young Azpha objective.");
			if (!session.Api.World.Skills.TryGetValue(30001, out BotSkill? gatheringSkill) || gatheringSkill.Level < 1)
				throw new InvalidDataException("Client did not observe the required level-1 gathering skill.");
			var policy = new NaturalIshalgenGatheringPolicy(SoakGatheringPool.StarterSpots());
			for (int decisionIndex = 0; decisionIndex < 96 &&
				ItemCount(session.Api.World, operation.ItemId) < operation.Count; decisionIndex++)
			{
				NaturalGatherNode[] visible = session.Api.World.Objects.Values
					.Where(obj => obj.Kind == BotKnownObjectKind.Gatherable &&
						obj.TemplateId == GatheringTarget.YoungAzpha.TemplateId)
					.Select(obj => new NaturalGatherNode(obj.ObjectId, obj.Position)).ToArray();
				TimeSpan elapsed = TimeSpan.FromMilliseconds(fixture.Clock.NowMillis);
				NaturalGatherChoice choice = policy.Decide(session.CurrentPosition, visible,
					ItemCount(session.Api.World, operation.ItemId), elapsed);
				session.TraceDiagnostic("azpha-decision", new Dictionary<string, object?>
				{
					["action"] = choice.Action,
					["reason"] = choice.Reason,
					["objectId"] = choice.ObjectId,
					["spot"] = choice.Spot?.Position,
					["inventoryCount"] = ItemCount(session.Api.World, operation.ItemId),
				});
				if (choice.Action == "explore" && choice.Spot is { } hint)
				{
					NaturalNavigationResult search = await NaturalIshalgenNavigator.ExploreAnchorAsync(
						contract.MapId, -1, hint.Position, navigator, "Young Azpha spawn hint", token);
					if (!search.Arrived)
					{
						if (search.Reason != "No collision-checked route to the current destination." ||
							!await TryClearObservedBlockerAsync(hint.Position))
							policy.RecordUnreachable(hint, TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
					}
					else if (!session.Api.World.Objects.Values.Any(obj =>
						obj.Kind == BotKnownObjectKind.Gatherable &&
						obj.TemplateId == GatheringTarget.YoungAzpha.TemplateId &&
						Distance(obj.Position, hint.Position) < 1))
						policy.RecordUnobserved(hint, TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
					continue;
				}
				if (choice.Action == "wait" && choice.Wait is { } wait)
				{
					for (TimeSpan remaining = wait; remaining > TimeSpan.Zero;)
					{
						TimeSpan interval = remaining < TimeSpan.FromSeconds(1) ? remaining : TimeSpan.FromSeconds(1);
						await session.AdvanceAsync(interval, token);
						await navigator.SynchronizeAsync(token);
						remaining -= interval;
					}
					continue;
				}
				if (choice.Action != "gather" || choice.Spot is not { } spot || choice.ObjectId is not int objectId)
					throw new InvalidDataException($"Unexpected Young Azpha decision {choice.Action}.");
				NaturalNavigationResult approach = await NaturalIshalgenNavigator.ApproachObservedObjectAsync(
					contract.MapId, GatheringTarget.YoungAzpha.TemplateId, spot.Position,
					navigator, "Young Azpha", token);
				if (!approach.Arrived || approach.TargetObjectId != objectId)
				{
					if (approach.Reason != "No collision-checked route to the current destination." ||
						!await TryClearObservedBlockerAsync(spot.Position, objectId))
						policy.RecordUnreachable(spot, TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
					continue;
				}
				long before = ItemCount(session.Api.World, operation.ItemId);
				int packetStart = session.PacketHistory.Count;
				foreach (BotClientPacket packet in session.Api.Gather(objectId))
					await session.SendPacketAsync(packet, token);
				DecodedBotServerPacket update = await session.WaitForPacketAsync(typeof(SM_GATHER_UPDATE), token);
				if (update.Get<byte>("action") == 0)
				{
					DecodedBotServerPacket? finished = null;
					for (int second = 0; second < 60 && finished == null; second++)
					{
						await session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
						await navigator.SynchronizeAsync(token);
						finished = session.PacketHistory.Skip(packetStart)
							.LastOrDefault(packet => packet.PacketType == typeof(SM_GATHER_UPDATE) &&
								packet.Get<byte>("action") >= 5);
					}
					update = finished ?? throw new TimeoutException("Young Azpha harvest did not finish within 60 virtual seconds.");
				}
				byte outcome = update.Get<byte>("action");
				policy.RecordAttempt(spot, objectId, outcome, TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
				if (outcome == 6)
				{
					for (int followup = 0; followup < 5 && ItemCount(session.Api.World, operation.ItemId) == before; followup++)
					{
						await session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
						await navigator.SynchronizeAsync(token);
					}
					Assert.Equal(before + 1, ItemCount(session.Api.World, operation.ItemId));
				}
			}
			Assert.Equal(operation.Count, ItemCount(session.Api.World, operation.ItemId));
		}

		async Task OpenQuestDialogAsync(int npc, int questId)
		{
			await session.SendPacketAsync(session.Api.TalkTo(npc), token);
			await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
				packet => packet.Get<int>("targetObjectId") == npc);
			await session.SendPacketAsync(session.Api.SelectDialog(npc, DialogAction.QUEST_SELECT, questId: questId), token);
			await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
				packet => packet.Get<int>("targetObjectId") == npc && packet.Get<int>("questId") == questId);
		}
	}

	private static NaturalIshalgenObservation ObserveNaturalJourney(SimulationL0Session session)
	{
		BotWorldModel world = session.Api.World;
		return new NaturalIshalgenObservation(true,
			session.PacketHistory.Any(packet => packet.PacketType == typeof(SM_QUEST_LIST)),
			session.PacketHistory.Any(packet => packet.PacketType == typeof(SM_QUEST_COMPLETED_LIST)),
			world.MapId, world.Level, world.IsDead,
			new Dictionary<int, BotQuestState>(world.Quests), world.CompletedQuestIds.ToHashSet(),
			session.CurrentPosition, world.Objects.Values.ToArray());
	}

	private sealed class NaturalSimulationNavigator(SimulationL0Session session,
		BotNavigationGraph graph, BotNavigationGeometry geometry) : INaturalNavigationDriver
	{
		public bool AvoidHostileAggro { get; set; }
		public bool InCombat { get; set; }
		public Func<int[], BotPosition, int, CancellationToken, Task>? DefendOnAttackAsync { get; set; }
		public string LastRouteDiagnostic { get; private set; } = "none";
		/// <summary>Travel planner tried first for long non-combat legs (see BotTravelPlanner.PlanJourney).</summary>
		public BotTravelPlanner? Planner { get; init; }
		public List<NaturalNavigationEvent> Events { get; } = [];
		public HashSet<int> UnavailableObjects { get; } = [];
		private BotPosition? lastMovementStart;
		public BotPosition? LastMovementStart => lastMovementStart;
		private bool defending;

		public NaturalNavigationObservation Observe()
		{
			BotWorldModel world = session.Api.World;
			// A walking NPC is placed where its last SM_MOVE was heading (a real client animates it there;
			// a walker that has been quiet for a while has arrived). Monsters that attacked the bot recently
			// keep their reported position: their move target is the bot itself.
			HashSet<int> attackers = session.PacketHistory.Skip(Math.Max(0, session.PacketHistory.Count - 400))
				.Where(packet => packet.PacketType == typeof(SM_ATTACK) && packet.Get<int>("targetObjId") == session.CharacterId)
				.Select(packet => packet.Get<int>("attackerObjId")).ToHashSet();
			return new(world.MapId, session.CurrentPosition, world.IsDead,
				world.Objects.Values.Where(item => (item.Kind is BotKnownObjectKind.Npc or BotKnownObjectKind.Gatherable) &&
					item.TemplateId != null &&
					!UnavailableObjects.Contains(item.ObjectId))
					.Select(item => new NaturalNavigationObject(item.ObjectId, item.TemplateId!.Value,
						attackers.Contains(item.ObjectId) ? item.Position : item.SettledPosition)).ToArray());
		}

		public Task<IReadOnlyList<BotPosition>> FindRouteAsync(BotPosition start, BotPosition destination,
			CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			int map = session.Api.World.MapId ?? throw new InvalidDataException("SIM journey map unobserved.");
			BotNavigationHazard[] hazards = ObservedHazards(destination);
			float remaining = Distance(start, destination);
			if (Planner != null && !InCombat && remaining >= BotTravelPlanner.MinimumJourneyDistance)
			{
				BotTravelPlan? plan = Planner.PlanJourney(map, start, destination, session.Api.World.Level, hazards);
				session.TraceDiagnostic(plan == null ? "travel-plan-unavailable" : "travel-plan", new Dictionary<string, object?>
				{
					["start"] = start,
					["destination"] = destination,
					["level"] = session.Api.World.Level,
					["observedHazards"] = hazards.Length,
					["navmeshOutcome"] = BotNavMeshRouter.LastOutcome.ToString(),
					["plan"] = plan == null ? null : BotTravelPlanner.Describe(plan),
					["waypoints"] = plan?.Waypoints.Select(node => new { node.Id, node.Name, node.Kind }).ToArray(),
				});
				if (plan != null)
				{
					LastRouteDiagnostic = $"travel-plan: {BotTravelPlanner.Describe(plan)}, hazards={hazards.Length}, destination={destination}";
					return Task.FromResult(plan.Route);
				}
			}
			if (map == NaturalIshalgenRoads.MapId && !InCombat &&
				session.CurrentStep.StartsWith("ni07-q2006", StringComparison.Ordinal))
			{
				IReadOnlyList<BotPosition> roadRoute = geometry.FindRoadPreferredJourneyPath(
					map, start, destination, NaturalIshalgenRoads.MijouToMauFarms, hazards);
				if (roadRoute.Count != 0)
				{
					LastRouteDiagnostic = $"mapped-road=Mijou-to-Mau-farms, checked={roadRoute.Count}, " +
						$"hazards={hazards.Length}, destination={destination}";
					session.TraceDiagnostic("road-route-selected", new Dictionary<string, object?>
					{
						["corridor"] = "Mijou-to-Mau-farms",
						["start"] = start,
						["destination"] = destination,
						["checkedPoints"] = roadRoute.Count,
						["observedHazards"] = hazards.Length,
					});
					return Task.FromResult(roadRoute);
				}
			}
			BotPosition[] candidates = graph.GetMap(map)?.Waypoints
				.Select(waypoint => waypoint.Position)
				.Where(point => Distance(start, point) is > 15 and <= 120 &&
					Distance(point, destination) < remaining - 15)
				.OrderBy(point => Distance(point, destination)).ThenBy(point => Distance(start, point))
				.Take(24).ToArray() ?? [];
			IReadOnlyList<BotPosition> FindCheckedForwardProgress()
			{
				foreach (BotPosition candidate in candidates)
				{
					if (geometry.TraceEdge(map, start, candidate) is { } edge &&
						BotNavigationGeometry.AvoidsHazards(start, edge, hazards))
						return edge;
				}
				foreach (BotPosition candidate in candidates.Take(8))
				{
					IReadOnlyList<BotPosition> local = geometry.FindLocalPath(map, start, candidate);
					if (local.Count != 0 && BotNavigationGeometry.AvoidsHazards(start, local, hazards))
						return local;
				}
				return [];
			}
			IReadOnlyList<BotPosition> route = graph.FindPath(map, start, destination);
			// A merely closer waypoint can end at a disconnected ledge. Prefer an
			// end-to-end checked route unless observed hostiles make it unsafe.
			if (route.Count == 0 && remaining > 200 && hazards.Length > 0)
				route = FindCheckedForwardProgress();
			if (route.Count == 0 && remaining <= 200) route = geometry.FindLocalPath(map, start, destination);
			if (route.Count == 0)
			{
				session.TraceDiagnostic("global-route-search", new Dictionary<string, object?>
				{
					["start"] = start,
					["destination"] = destination,
					["candidateCount"] = candidates.Length,
					["hazardCount"] = hazards.Length,
				});
				route = geometry.FindJourneyPath(map, start, destination);
			}
			if (route.Count == 0) route = FindCheckedForwardProgress();
			if (route.Count == 0 && Observe().Npcs.Any(npc => Distance(npc.Position, destination) < 0.1f))
				route = geometry.FindInteractionPath(map, start, destination);
			int checkedRoutePoints = route.Count;
			if (route.Count != 0 && !BotNavigationGeometry.AvoidsHazards(start, route, hazards))
			{
				// Even at short range, first try a checked local step around the
				// observed aggro circles before the expensive global avoidance search.
				route = FindCheckedForwardProgress();
				if (route.Count == 0)
				{
					session.TraceDiagnostic("hazard-route-search", new Dictionary<string, object?>
					{
						["start"] = start,
						["destination"] = destination,
						["candidateCount"] = candidates.Length,
						["hazardCount"] = hazards.Length,
					});
					route = geometry.FindJourneyPathAvoiding(map, start, destination, hazards);
				}
			}
			if (route.Count == 0)
				session.TraceDiagnostic("route-failed", new Dictionary<string, object?>
				{
					["start"] = start,
					["destination"] = destination,
					["navmeshOutcome"] = Aion.Bots.Navigation.NavMesh.BotNavMeshRouter.LastOutcome.ToString(),
					["hazardCircles"] = hazards.Select(hazard => new { hazard.Position.X, hazard.Position.Y, hazard.Position.Z, hazard.Radius }).ToArray(),
				});
			LastRouteDiagnostic = $"checked={checkedRoutePoints}, avoided={route.Count}, " +
				$"destination={destination}, distance={Distance(start, destination):F1}, " +
				$"hazards=[{string.Join(';', hazards.Select(hazard =>
					$"{hazard.Position.X:F1}/{hazard.Position.Y:F1}/r{hazard.Radius:F1}"))}]";
			return Task.FromResult(route);
		}

		public bool IsSegmentSafe(IReadOnlyList<BotPosition> segment, int? targetObjectId)
		{
			BotNavigationHazard[] hazards = ObservedHazards(null, targetObjectId);
			return BotNavigationGeometry.AvoidsHazards(session.CurrentPosition, segment, hazards);
		}

		public IReadOnlyList<BotPosition> FindRangedApproach(BotPosition start, BotPosition target,
			int targetObjectId, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			int map = session.Api.World.MapId ?? throw new InvalidDataException("SIM journey map unobserved.");
			BotNavigationHazard[] otherHazards = ObservedHazards(null, targetObjectId);
			IReadOnlyList<BotPosition> route = geometry.FindRangedApproachPath(
				map, start, target, otherHazards);
			LastRouteDiagnostic = $"ranged-approach={route.Count}, target={target}, " +
				$"otherHazards=[{string.Join(';', otherHazards.Select(hazard =>
					$"{hazard.Position.X:F1}/{hazard.Position.Y:F1}/r{hazard.Radius:F1}"))}]";
			session.TraceDiagnostic("combat-ranged-route", new Dictionary<string, object?>
			{
				["start"] = start,
				["target"] = target,
				["targetObjectId"] = targetObjectId,
				["checkedPoints"] = route.Count,
				["otherHazards"] = otherHazards.Length,
				["hazardCircles"] = otherHazards.Select(hazard => new
				{
					hazard.Position.X,
					hazard.Position.Y,
					hazard.Radius,
				}).ToArray(),
			});
			if (route.Count == 0 &&
				Environment.GetEnvironmentVariable("NI07_CAPTURE_BLOCKED_RANGED_ROUTE") == "1" &&
				session.CurrentStep.StartsWith("ni07-q2006", StringComparison.Ordinal))
				throw new InvalidDataException($"NI-07 captured the first blocked Q2006 ranged route: " +
					$"{LastRouteDiagnostic}; trace={session.CombatTracePath}");
			return route;
		}

		private BotNavigationHazard[] ObservedHazards(BotPosition? destination,
			int? targetObjectId = null) => !AvoidHostileAggro ? [] : Observe().Npcs
			.Where(npc => npc.ObjectId != targetObjectId &&
				(destination == null || Distance(npc.Position, destination.Value) > 0.1f))
			.Select(npc => (npc,
				template: Aion.GameServer.Dataholders.DataManager.NPC_DATA.GetNpcTemplate(npc.TemplateId)))
			.Where(entry => NaturalHostility.IsAggressive(entry.template) &&
				entry.template.GetAggroRange() > 0)
			.Select(entry => new BotNavigationHazard(entry.npc.Position,
				entry.template!.GetAggroRange() + 1f)).ToArray();

		public Task MoveAsync(IReadOnlyList<BotPosition> segment, CancellationToken token)
		{
			lastMovementStart = session.CurrentPosition;
			return session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing)
				.CreateGroundPlan(segment, session.CurrentPosition,
					session.Api.World.MovementSpeed ?? throw new InvalidDataException("SIM movement speed unobserved.")), token);
		}

		public async Task SynchronizeAsync(CancellationToken token)
		{
			int packetStart = session.PacketHistory.Count;
			await session.SynchronizeAsync(token);
			if (StopNi07OnDeath && session.Api.World.IsDead)
			{
				string nearby = string.Join(',', Observe().Npcs.Where(npc =>
					Distance(npc.Position, session.CurrentPosition) < 30)
					.Select(npc => $"{npc.TemplateId}/{npc.ObjectId}@" +
						$"{Distance(npc.Position, session.CurrentPosition):F1}m"));
				session.TraceDiagnostic("stop-on-death", new Dictionary<string, object?>
				{
					["mapId"] = session.Api.World.MapId,
					["level"] = session.Api.World.Level,
					["position"] = session.CurrentPosition,
					["hp"] = session.Api.World.CurrentHp,
					["mp"] = session.Api.World.CurrentMp,
					["nearby"] = nearby,
				});
				throw new InvalidDataException($"NI-07 stop-on-death: {session.CurrentStep}/" +
					$"{session.CurrentAction}, level={session.Api.World.Level}, " +
					$"position={session.CurrentPosition}, nearby={nearby}, " +
					$"combatTrace={session.CombatTracePath}");
			}
			if (defending || DefendOnAttackAsync == null || session.Api.World.IsDead) return;
			int[] attackers = session.PacketHistory.Skip(packetStart)
				.Where(packet => packet.PacketType == typeof(SM_ATTACK) &&
					packet.Get<int>("targetObjId") == session.CharacterId)
				.Select(packet => packet.Get<int>("attackerObjId")).Distinct().ToArray();
			if (attackers.Length == 0) return;
			defending = true;
			try
			{
				await DefendOnAttackAsync(attackers, lastMovementStart ?? session.CurrentPosition, packetStart, token);
			}
			finally { defending = false; }
		}

		public void Record(NaturalNavigationEvent navigationEvent)
		{
			Events.Add(navigationEvent);
			session.TraceDiagnostic("navigation-decision", new Dictionary<string, object?>
			{
				["action"] = navigationEvent.Action,
				["outcome"] = navigationEvent.Outcome,
				["reason"] = navigationEvent.Reason,
				["mapId"] = navigationEvent.MapId,
				["targetTemplateId"] = navigationEvent.TargetTemplateId,
				["position"] = navigationEvent.Position,
				["destination"] = navigationEvent.Destination,
				["targetObjectId"] = navigationEvent.TargetObjectId,
				["routeSearches"] = navigationEvent.RouteSearches,
				["segments"] = navigationEvent.Segments,
				["plannedRoutePoints"] = navigationEvent.PlannedRoute?.Length,
			});
		}
	}

	private sealed class NaturalSimulationCombat(SimulationL0Session session,
		NaturalSimulationNavigator navigator, SimulationWorldFixture fixture,
		BotNavigationGeometry geometry)
	{
		private readonly Dictionary<int, DateTimeOffset> cooldowns = [];
		private int revives;
		private int? engagedTarget;
		private string[] lastCombatTrace = [];
		private int obstacleRepositions;
		private int rangeRejections;
		public int ReviveCount => revives;
		public bool InCombat { get; private set; }
		public Func<CancellationToken, Task>? MaintainInventoryAsync { get; set; }
		/// <summary>Leave the pack; when no checked escape leads away from it (a pocket, a ledge, more
		/// monsters on every way out), fight the attackers nearest-first instead, as a cornered player would.</summary>
		public async Task EscapeAsync(BotPosition refuge, IReadOnlySet<int> observedAttackers,
			CancellationToken token)
		{
			if (await RetreatFromPackAsync(refuge, observedAttackers, token)) return;
			foreach (int attacker in observedAttackers
				.Select(id => navigator.Observe().Npcs.FirstOrDefault(npc => npc.ObjectId == id))
				.OfType<NaturalNavigationObject>()
				.Where(npc => Distance(session.CurrentPosition, npc.Position) < 30)
				.OrderBy(npc => Distance(session.CurrentPosition, npc.Position))
				.Select(npc => npc.ObjectId).ToArray())
			{
				cornered = true;
				await TryKillAsync(attacker, token);
				if (session.Api.World.IsDead || session.Api.World.CurrentHp <= 0) return;
			}
		}

		// Set when a retreat found no checked way out; the policy then fights instead of retreating again.
		private bool cornered;
		public const int MaximumCombatActions = 1000;
		private const int MaximumRevives = 20;

		/// <summary>Where the Priest died (client position at death). Pull planning avoids firing from near
		/// these, as a player avoids the spot where hidden or respawning monsters killed them.</summary>
		public List<BotPosition> DeathSpots { get; } = [];

		public async Task KillAsync(int target, CancellationToken token)
		{
			if (!await TryKillAsync(target, token))
				throw new InvalidDataException($"Engaged NPC {target} disappeared without client-observed kill evidence.");
		}

		public async Task<bool> TryKillAsync(int target, CancellationToken token,
			BotPosition? retreatAnchor = null, int? attackHistoryStart = null)
		{
			if (InCombat) throw new InvalidOperationException("Natural Priest combat cannot nest another fight.");
			InCombat = true;
			navigator.InCombat = true;
			try
			{
				return await TryKillCoreAsync(target, token, retreatAnchor, attackHistoryStart);
			}
			finally
			{
				InCombat = false;
				navigator.InCombat = false;
				cornered = false;
			}
		}

		private async Task<bool> TryKillCoreAsync(int target, CancellationToken token,
			BotPosition? retreatAnchor, int? attackHistoryStart)
		{
			engagedTarget = target;
			obstacleRepositions = 0;
			rangeRejections = 0;
			BotWorldModel world = session.Api.World;
			long startingExperience = world.CurrentExperience;
			var trace = new List<string>();
			int observedPacketCount = attackHistoryStart ?? session.PacketHistory.Count;
			int statusPacketCount = session.PacketHistory.Count;
			int? observedTargetHpPercent = null;
			bool healedThisFight = false;
			bool inEmergency = false;
			long lastHitByTargetMillis = long.MinValue;
			IReadOnlySet<int> blessingIds = NaturalPriestSkills.Ids("blessing");
			var incomingAttackers = new HashSet<int>();
			// Fights may run long: a cornered Priest alternates heals and damage, and respawns or chain
			// aggro can keep adding monsters. The bound only stops a genuine stall (every action rejected
			// forever) from hanging the run; at a few seconds per action it is about an hour of game time.
			for (int turn = 0; turn < MaximumCombatActions; turn++)
			{
				trace.Add($"t{turn}:pre-sync hp={world.CurrentHp}/{world.MaxHp} mp={world.CurrentMp}/{world.MaxMp} " +
					$"dead={world.IsDead} pos={session.CurrentPosition}");
				await session.SynchronizeAsync(token);
				DecodedBotServerPacket[] recentAttacks = session.PacketHistory.Skip(observedPacketCount)
					.Where(packet => packet.PacketType == typeof(SM_ATTACK) &&
						packet.Get<int>("targetObjId") == session.CharacterId).ToArray();
				foreach (DecodedBotServerPacket attack in recentAttacks)
				{
					int attacker = attack.Get<int>("attackerObjId");
					incomingAttackers.Add(attacker);
					if (attacker == target) lastHitByTargetMillis = fixture.Clock.NowMillis;
				}
				observedPacketCount = session.PacketHistory.Count;
				foreach (DecodedBotServerPacket status in session.PacketHistory.Skip(statusPacketCount)
					.Where(packet => packet.PacketType == typeof(SmAttackStatus) &&
						packet.Get<int>("objectId") == target))
					observedTargetHpPercent = status.Get<byte>("hpOrMp");
				statusPacketCount = session.PacketHistory.Count;
				int nearbyAttackers = incomingAttackers.Count(attacker =>
					world.Objects.TryGetValue(attacker, out BotKnownObject? attackerNpc) &&
					Distance(session.CurrentPosition, attackerNpc.Position) < 30);
				trace.Add($"t{turn}:post-sync hp={world.CurrentHp}/{world.MaxHp} mp={world.CurrentMp}/{world.MaxMp} dead={world.IsDead}");
				trace.Add($"t{turn}:client-observed-nearby-attackers={nearbyAttackers}");
				lastCombatTrace = trace.TakeLast(12).ToArray();
				if (world.IsDead || world.CurrentHp <= 0)
				{
					await ReviveAtBindAsync(token);
					return false; // The engaged corpse was not looted; reacquire from the client.
				}
				if (world.CurrentExperience > startingExperience || world.LootStatuses.ContainsKey(target)) return true;
				if (!world.Objects.TryGetValue(target, out BotKnownObject? npc))
					return false; // Reacquire a new client-observed mob; do not count this as a kill.
				DateTimeOffset now = fixture.Epoch.AddMilliseconds(fixture.Clock.NowMillis);
				// A monster that hit us in the last 3 s is in melee reach whatever its lagging client position says.
				bool targetAdjacent = fixture.Clock.NowMillis - lastHitByTargetMillis <= 3000 ||
					Distance(session.CurrentPosition, npc.Position) <= NaturalPriestCombatPolicy.MeleeReach;
				if (world.CurrentHp * 100 <= world.MaxHp * NaturalPriestCombatPolicy.EmergencyPercent) inEmergency = true;
				else if (world.CurrentHp * 100 >= world.MaxHp * NaturalPriestCombatPolicy.EmergencyClearPercent) inEmergency = false;
				bool hasBlessing = world.VisibleEffects?.Any(effect => blessingIds.Contains(effect.SkillId)) == true;
				BotInventoryItem? hotPotion = NaturalIshalgenPotionPolicy.SelectOwnedPotion(world.Inventory.Values);
				var hotTemplate = hotPotion == null ? null :
					Aion.GameServer.Dataholders.DataManager.ITEM_DATA.GetItemTemplate(hotPotion.ItemId);
				bool hotReady = hotTemplate != null && session.Api.Timing.TimeUntilItemUse(hotTemplate) == TimeSpan.Zero;
				NaturalCombatChoice choice = NaturalPriestCombatPolicy.Decide(new NaturalCombatObservation(
					world.Level, world.CurrentHp, world.MaxHp, world.CurrentMp, world.MaxMp, world.IsDead,
					nearbyAttackers > 0 || recentAttacks.Length > 0,
					Distance(session.CurrentPosition, npc.Position), target, world.Skills, cooldowns,
					NearbyAggressors: nearbyAttackers, TargetHpPercent: observedTargetHpPercent,
					HasHealedThisFight: healedThisFight, HasHotPotion: hotPotion != null,
					HotPotionReady: hotReady,
					HotPotionActive: NaturalIshalgenPotionPolicy.HasActiveHealing(world.VisibleEffects),
					Cornered: cornered, TargetAdjacent: targetAdjacent, InEmergency: inEmergency, HasBlessing: hasBlessing), now);
				trace.Add($"t{turn}:action={choice.Action}/{choice.Skill?.Id} targetDistance={Distance(session.CurrentPosition, npc.Position):F1}");
				lastCombatTrace = trace.TakeLast(12).ToArray();
				session.TraceDiagnostic("combat-decision", new Dictionary<string, object?>
				{
					["turn"] = turn,
					["action"] = choice.Action,
					["skillId"] = choice.Skill?.Id,
					["reason"] = choice.Reason,
					["checks"] = choice.Checks,
					["targetObjectId"] = target,
					["targetDistance"] = Distance(session.CurrentPosition, npc.Position),
					["hp"] = world.CurrentHp,
					["maxHp"] = world.MaxHp,
					["mp"] = world.CurrentMp,
					["observedAttackers"] = nearbyAttackers,
					["targetAdjacent"] = targetAdjacent,
					["inEmergency"] = inEmergency,
					["targetHpPercent"] = observedTargetHpPercent,
					["healedThisFight"] = healedThisFight,
					["hotPotionItemId"] = hotPotion?.ItemId,
					["hotPotionReady"] = hotReady,
					["hotPotionActive"] = NaturalIshalgenPotionPolicy.HasActiveHealing(world.VisibleEffects),
				});
				switch (choice.Action)
				{
					case "hot-potion":
					{
						if (hotPotion == null || hotTemplate == null)
							throw new InvalidDataException("Combat chose a potion absent from observed inventory.");
						long before = NaturalIshalgenPotionPolicy.Count(world.Inventory.Values, hotPotion.ItemId);
						int useStart = session.PacketHistory.Count;
						await session.SendPacketAsync(session.Api.UseItem(hotPotion.ObjectId, hotTemplate), token);
						await session.SynchronizeAsync(token);
						long after = NaturalIshalgenPotionPolicy.Count(world.Inventory.Values, hotPotion.ItemId);
						if (after == before && session.PacketHistory.Skip(useStart).Any(packet =>
							packet.PacketType == typeof(SM_SYSTEM_MESSAGE) &&
							packet.Get<object>("name") is "STR_SKILL_CAN_NOT_USE_ITEM_WHILE_IN_ABNORMAL_STATE"))
						{
							// Stunned or knocked down (Java PlayerRestrictions.canUseItem): the potion stays in the
							// bag. Wait for the state to wear off and decide again.
							session.TraceDiagnostic("combat-item-while-disabled", new Dictionary<string, object?>
							{
								["itemId"] = hotPotion.ItemId, ["hp"] = world.CurrentHp, ["position"] = session.CurrentPosition,
							});
							await session.AdvanceAsync(TimeSpan.FromMilliseconds(1000), token);
							break;
						}
						if (after != before - 1)
							throw new InvalidDataException($"Timed healing potion {hotPotion.ItemId} was not consumed: {before}->{after}.");
						session.TraceDiagnostic("combat-hot-potion", new Dictionary<string, object?>
						{
							["itemId"] = hotPotion.ItemId, ["before"] = before, ["after"] = after,
							["hp"] = world.CurrentHp, ["sharedUseDelayId"] = NaturalIshalgenPotionPolicy.SharedUseDelayId,
						});
						break;
					}
					case "cast-self": case "cast-target":
						TimeSpan clientCastWait = session.Api.Timing.TimeUntilCast(choice.Skill!.Id);
						if (clientCastWait > TimeSpan.Zero)
						{
							// A server-ready skill may still be inside the client's minimum cast interval.
							// Advance only a short slice, then re-observe HP and attackers before deciding again.
							TimeSpan pacingSlice = TimeSpan.FromMilliseconds(
								Math.Min(clientCastWait.TotalMilliseconds + 1, 350));
							trace.Add($"t{turn}:client-cast-gate skill={choice.Skill.Id} wait={clientCastWait.TotalMilliseconds:F0}ms");
							lastCombatTrace = trace.TakeLast(12).ToArray();
							session.TraceDiagnostic("combat-client-cast-gate", new Dictionary<string, object?>
							{
								["skillId"] = choice.Skill.Id,
								["waitMillis"] = clientCastWait.TotalMilliseconds,
								["hp"] = world.CurrentHp,
								["target"] = target,
							});
							await session.AdvanceAsync(pacingSlice, token);
							break;
						}
						if (!await CastAsync(choice.Skill!, choice.Action == "cast-self" ? session.CharacterId : target, token))
							return false;
						if (choice.Action == "cast-self" && choice.Skill!.Role == "heal")
							healedThisFight = true;
						break;
					case "attack":
						await session.SendPacketAsync(session.Api.Target(target), token);
						await session.SendPacketAsync(session.Api.Attack(target, 2500, checked((byte)turn)), token);
						await session.AdvanceAsync(TimeSpan.FromMilliseconds(2600), token);
						break;
					case "approach":
						BotPosition destination = npc.Position;
						if (Distance(session.CurrentPosition, destination) > 25)
						{
							IReadOnlyList<BotPosition> route = navigator.FindRangedApproach(
								session.CurrentPosition, destination, target, token);
							if (route.Count == 0)
								throw new NaturalCombatApproachBlockedException($"No checked spell-range approach to {npc.TemplateId}/{target} " +
									$"from {session.CurrentPosition}: {navigator.LastRouteDiagnostic}");
							IReadOnlyList<BotPosition> segment = NaturalCombatStandoff.NextSegment(route, destination);
							if (segment.Count == 0)
								throw new NaturalCombatApproachBlockedException($"No checked spell-range standoff for {npc.TemplateId}/{target}.");
							if (!navigator.IsSegmentSafe(segment, target))
								throw new NaturalCombatApproachBlockedException(
									$"A newly observed hostile blocked the checked approach to {npc.TemplateId}/{target}.");
							BotPosition approachStart = session.CurrentPosition;
							await navigator.MoveAsync(segment, token);
							await navigator.SynchronizeAsync(token);
							if (Distance(session.CurrentPosition, approachStart) < 0.5f)
								throw new NaturalCombatApproachBlockedException(
									$"Client position did not advance toward {npc.TemplateId}/{target}.");
							break;
						}
						NaturalNavigationResult approach = await NaturalIshalgenNavigator.ApproachNpcAsync(
							220010000, npc.TemplateId!.Value, destination, navigator, token);
						if (!approach.Arrived)
							throw new NaturalCombatApproachBlockedException(approach.Reason);
						break;
					case "wait":
						await session.AdvanceAsync(TimeSpan.FromMilliseconds(500), token);
						break;
					case "retreat":
						// Some quest pulls begin directly at an interacted object and have
						// no named refuge. Previously walked client positions remain valid
						// candidates, but each escape leg is checked against current mobs.
						if (await RetreatFromPackAsync(retreatAnchor ?? session.CurrentPosition,
							incomingAttackers, token))
							return false;
						cornered = true; // No way out: stay and fight (heal, potions, then the target).
						break;
					default: throw new InvalidDataException($"Natural combat cannot act: {choice.Action}: {choice.Reason}");
				}
			}
			throw new InvalidDataException($"Natural Priest exceeded {MaximumCombatActions} actions without a client-observed NPC kill.");
		}

		/// <summary>Where an attacker was when the bot first saw it: SM_NPC_INFO arrives as the NPC enters view,
		/// normally at or near its spawn, so this estimates the home its give-up distance is measured from.</summary>
		private BotPosition? FirstSeen(int objectId)
		{
			DecodedBotServerPacket? info = session.PacketHistory.FirstOrDefault(packet =>
				packet.PacketType == typeof(SM_NPC_INFO) && packet.Get<int>("objectId") == objectId);
			return info == null ? null
				: new BotPosition(info.Get<float>("x"), info.Get<float>("y"), info.Get<float>("z"), 0);
		}

		/// <summary>
		/// Break away from a pack: always away from the attackers, never back through them. Candidates are
		/// previously walked ground, the refuge, and navmesh ground in rings around the bot, filtered to the
		/// half-plane away from the attackers and outside other observed circles, and ranked by distance
		/// from the attackers' homes (Java AttackManager.checkGiveupDistance: a chaser gives up beyond
		/// 100 m from home after 10 s without being hit). A destination is kept until reached or blocked;
		/// on arrival with chasers still close, the next one is chosen further out.
		/// </summary>
		private async Task<bool> RetreatFromPackAsync(BotPosition refuge,
			IReadOnlySet<int> observedAttackers, CancellationToken token)
		{
			BotPosition origin = session.CurrentPosition;
			int map = session.Api.World.MapId ?? throw new InvalidDataException("Retreat map unobserved.");
			BotPosition[] homes = observedAttackers
				.Select(id => FirstSeen(id) ?? navigator.Observe().Npcs.FirstOrDefault(npc => npc.ObjectId == id)?.Position)
				.OfType<BotPosition>().ToArray();
			var tried = new List<BotPosition>();
			for (int replan = 0; replan < 8; replan++)
			{
				BotPosition[] attackerPositions = navigator.Observe().Npcs
					.Where(npc => observedAttackers.Contains(npc.ObjectId))
					.Select(npc => npc.Position).ToArray();
				if (attackerPositions.Length == 0 || !attackerPositions.Any(p => Distance(session.CurrentPosition, p) < 30)) return true;
				BotNavigationHazard[] otherHazards = navigator.Observe().Npcs
					.Where(npc => !observedAttackers.Contains(npc.ObjectId))
					.Select(npc => (npc, template: Aion.GameServer.Dataholders.DataManager.NPC_DATA.GetNpcTemplate(npc.TemplateId)))
					.Where(entry => NaturalHostility.IsAggressive(entry.template))
					.Select(entry => new BotNavigationHazard(entry.npc.Position, entry.template!.GetAggroRange())).ToArray();
				IEnumerable<BotPosition> candidates = navigator.Events
					.Where(item => item.Action == "segment-progress" && item.Position != null)
					.Select(item => item.Position!.Value).Append(refuge)
					.Concat(geometry.GroundAround(map, session.CurrentPosition, [45f, 75f, 110f, 150f]))
					.Where(p => !tried.Any(t => Distance(t, p) < 10) && geometry.OnSameIsland(map, session.CurrentPosition, p));
				BotPosition[] escapes = NaturalCombatRetreatPolicy.SelectEscape(session.CurrentPosition,
					attackerPositions, homes, candidates, otherHazards);
				IReadOnlyList<BotPosition> route = [];
				BotPosition? destination = null;
				var rejections = new List<string>();
				foreach (BotPosition escape in escapes)
				{
					tried.Add(escape);
					// Avoid every other observed monster, not the chasers themselves: they move with the bot,
					// so their circles always surround it and would reject every escape.
					IReadOnlyList<BotPosition> candidate = geometry.FindJourneyPathAvoiding(map, session.CurrentPosition, escape, otherHazards);
					// The first steps may swing sideways around another monster or a rock, but must not turn
					// back into the pack: the point about 10 m along stays within ~100 degrees of "away".
					string? why = candidate.Count == 0 ? $"no route ({BotNavMeshRouter.LastOutcome})"
						: Distance(session.CurrentPosition, candidate[^1]) < 10 ? "too short"
						: AwayCosine(attackerPositions, session.CurrentPosition, candidate[Math.Min(candidate.Count - 1, 4)]) < -0.2f
							? "first steps back into the pack" : null;
					if (why != null)
					{
						rejections.Add($"({escape.X:F0},{escape.Y:F0}) {why}");
						continue;
					}
					route = candidate;
					destination = escape;
					break;
				}
				if (destination == null)
				{
					session.TraceDiagnostic("combat-retreat-cornered", new Dictionary<string, object?>
					{
						["position"] = session.CurrentPosition,
						["observedAttackers"] = observedAttackers.ToArray(),
						["attackerPositions"] = attackerPositions,
						["rejections"] = rejections.ToArray(),
						["otherHazards"] = otherHazards.Length,
						["hp"] = session.Api.World.CurrentHp,
					});
					return false;
				}
				session.TraceDiagnostic("combat-retreat-route", new Dictionary<string, object?>
				{
					["origin"] = session.CurrentPosition,
					["destination"] = destination,
					["checkedPoints"] = route.Count,
					["observedAttackers"] = observedAttackers.ToArray(),
					["attackerHomes"] = homes,
					["fromNearestHome"] = homes.Length == 0 ? null : homes.Min(h => Distance(h, destination.Value)),
				});
				foreach (BotPosition[] segment in route.Chunk(2).Take(256))
				{
					BotNavigationHazard[] others = navigator.Observe().Npcs
						.Where(npc => !observedAttackers.Contains(npc.ObjectId))
						.Select(npc => (npc, template: Aion.GameServer.Dataholders.DataManager.NPC_DATA.GetNpcTemplate(npc.TemplateId)))
						.Where(entry => NaturalHostility.IsAggressive(entry.template))
						.Select(entry => new BotNavigationHazard(entry.npc.Position, entry.template!.GetAggroRange())).ToArray();
					if (!BotNavigationGeometry.AvoidsHazards(session.CurrentPosition, segment, others)) break; // new monster ahead
					await navigator.MoveAsync(segment, token);
					await session.SynchronizeAsync(token);
					if (session.Api.World.CurrentHp <= 0 || session.Api.World.IsDead)
					{
						await ReviveAtBindAsync(token);
						return true;
					}
					if (!navigator.Observe().Npcs.Any(npc =>
						observedAttackers.Contains(npc.ObjectId) &&
						Distance(session.CurrentPosition, npc.Position) < 30)) return true;
				}
			}
			session.TraceDiagnostic("combat-retreat-cornered", new Dictionary<string, object?>
			{
				["position"] = session.CurrentPosition,
				["origin"] = origin,
				["observedAttackers"] = observedAttackers.ToArray(),
				["reason"] = "chasers still close after eight away-from-pack replans",
			});
			return false;

			static float AwayCosine(IReadOnlyList<BotPosition> attackers, BotPosition from, BotPosition to)
			{
				float awayX = from.X - attackers.Average(a => a.X), awayY = from.Y - attackers.Average(a => a.Y);
				float stepX = to.X - from.X, stepY = to.Y - from.Y;
				float lengths = MathF.Sqrt(awayX * awayX + awayY * awayY) * MathF.Sqrt(stepX * stepX + stepY * stepY);
				return lengths < 0.01f ? 1f : (awayX * stepX + awayY * stepY) / lengths;
			}
		}

		/// <summary>Shipped spawn points of hostile monsters on the map, each with its aggro range. Resting
		/// inside one means every respawn interrupts the rest, a rest/fight/rest loop that ends in death.</summary>
		public IReadOnlyList<BotNavigationHazard> HostileSpawns { get; set; } = [];

		/// <summary>Kept beyond each aggro range when choosing where to rest: Java's assist offset (2 m,
		/// <c>SUPPORT_RANGE_OFFSET</c>) plus room for a respawn to stand a little off its spot.</summary>
		public const float RestMargin = 5f;

		/// <summary>
		/// Before sitting down, move to the nearest ground that no respawning or observed monster can aggro
		/// from: outside every hostile spawn point's aggro range and every observed monster's, each plus
		/// <see cref="RestMargin"/>. Candidates are recently walked ground and navmesh ground in rings around
		/// the bot on its own island, nearest first, reached on a checked route that avoids observed monsters
		/// (and other spawn circles when there is a route that does). Stays put when already clear or when
		/// nothing within 150 m qualifies.
		/// </summary>
		private async Task MoveToRestSpotAsync(CancellationToken token)
		{
			if (session.Api.World.MapId is not int map) return;
			BotPosition here = session.CurrentPosition;
			BotNavigationHazard[] observed = navigator.Observe().Npcs
				.Select(npc => (npc, template: Aion.GameServer.Dataholders.DataManager.NPC_DATA.GetNpcTemplate(npc.TemplateId)))
				.Where(entry => NaturalHostility.IsAggressive(entry.template))
				.Select(entry => new BotNavigationHazard(entry.npc.Position, entry.template!.GetAggroRange())).ToArray();
			BotNavigationHazard[] spawns = HostileSpawns.Where(spawn => Distance(spawn.Position, here) < 260).ToArray();
			BotNavigationHazard[] threats = [.. spawns, .. observed];
			bool Clear(BotPosition point) => threats.All(threat =>
				Horizontal(threat.Position, point) >= threat.Radius + RestMargin || MathF.Abs(threat.Position.Z - point.Z) > 15);
			if (Clear(here)) return;
			IEnumerable<BotPosition> walked = navigator.Events.AsEnumerable().Reverse()
				.Where(item => item.Action == "segment-progress" && item.Position != null)
				.Select(item => item.Position!.Value).Take(400);
			BotPosition[] candidates = walked.Concat(geometry.GroundAround(map, here, [15f, 25f, 40f, 60f, 85f, 115f, 150f], 24))
				.Where(point => Distance(point, here) <= 150 && Clear(point) && geometry.OnSameIsland(map, here, point))
				.OrderBy(point => Distance(point, here)).ToArray();
			int tried = 0;
			foreach (BotPosition spot in candidates)
			{
				if (++tried > 10) break;
				// Prefer a way there that also stays out of other respawn circles; fall back to observed monsters only.
				BotNavigationHazard[] passing = [.. observed, .. spawns.Where(spawn =>
					Horizontal(spawn.Position, here) >= spawn.Radius && Horizontal(spawn.Position, spot) >= spawn.Radius)];
				IReadOnlyList<BotPosition> route = geometry.FindJourneyPathAvoiding(map, here, spot, passing);
				if (route.Count == 0) route = geometry.FindJourneyPathAvoiding(map, here, spot, observed);
				if (route.Count == 0) continue;
				session.TraceDiagnostic("rest-relocate", new Dictionary<string, object?>
				{
					["from"] = here,
					["to"] = spot,
					["routePoints"] = route.Count,
					["candidates"] = candidates.Length,
					["spawnsNearby"] = spawns.Count(spawn => Horizontal(spawn.Position, here) < spawn.Radius + RestMargin),
				});
				foreach (BotPosition[] segment in route.Chunk(8))
				{
					if (!navigator.IsSegmentSafe(segment, null)) break; // something new ahead: rest where we are
					await navigator.MoveAsync(segment, token);
					await navigator.SynchronizeAsync(token);
					if (session.Api.World.IsDead) return;
				}
				return;
			}
			session.TraceDiagnostic("rest-relocate-none", new Dictionary<string, object?>
			{
				["position"] = here,
				["candidates"] = candidates.Length,
			});
		}

		private static float Horizontal(BotPosition a, BotPosition b) =>
			MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

		/// <summary>
		/// Keep the learned protection buff (Blessing of Guardianship) up between fights, as the recorded human
		/// did. The client sees its own effects (SM_ABNORMAL_EFFECT); the buff is recast only when absent, off
		/// cooldown and affordable. The combat policy's own blessing rule never fires mid-pull, so this is where
		/// it happens.
		/// </summary>
		public async Task MaintainBuffsAsync(CancellationToken token)
		{
			BotWorldModel world = session.Api.World;
			if (world.IsDead || world.CurrentHp <= 0 || InCombat) return;
			NaturalPriestSkill? blessing = NaturalPriestSkills.Best("blessing", world.Level, world.Skills);
			if (blessing == null) return;
			var blessingIds = NaturalPriestSkills.All.Where(skill => skill.Role == "blessing").Select(skill => (int)skill.Id).ToHashSet();
			if (world.VisibleEffects?.Any(effect => blessingIds.Contains(effect.SkillId)) == true) return;
			DateTimeOffset now = fixture.Epoch.AddMilliseconds(fixture.Clock.NowMillis);
			if (cooldowns.TryGetValue(blessing.CooldownId, out DateTimeOffset readyAt) && readyAt > now) return;
			if (world.CurrentMp < blessing.ManaCost) return;
			TimeSpan gate = session.Api.Timing.TimeUntilCast(blessing.Id);
			if (gate > TimeSpan.Zero) await session.AdvanceAsync(gate + TimeSpan.FromMilliseconds(1), token);
			session.TraceDiagnostic("buff-blessing", new Dictionary<string, object?>
			{
				["skillId"] = blessing.Id,
				["mp"] = world.CurrentMp,
				["position"] = session.CurrentPosition,
			});
			await CastAsync(blessing, session.CharacterId, token);
		}

		public async Task RestAsync(CancellationToken token)
		{
			BotWorldModel restWorld = session.Api.World;
			if (!restWorld.IsDead && (restWorld.CurrentHp * 100 < restWorld.MaxHp * 90 || restWorld.CurrentMp * 100 < restWorld.MaxMp * 80))
				await MoveToRestSpotAsync(token);
			// Twelve quiet rest intervals (about two minutes) to recover. An interval interrupted by an attack
			// does not count: the Priest fights, then rests again, however many monsters arrive.
			int quietIntervals = 0;
			for (int interval = 0; quietIntervals < 12 && interval < MaximumCombatActions; interval++)
			{
				BotWorldModel world = session.Api.World;
				bool interrupted = false;
				if (world.IsDead || world.CurrentHp <= 0) await ReviveAtBindAsync(token);
				if (world.CurrentHp * 100 >= world.MaxHp * 90 &&
					world.CurrentMp * 100 >= world.MaxMp * 80)
				{
					await MaintainBuffsAsync(token);
					if (MaintainInventoryAsync is { } maintain)
						await maintain(token);
					return;
				}
				int attackHistoryStart = 0;
				NaturalRestOutcome outcome = await NaturalRestCadence.RunAsync(
					(resting, waitToken) => session.SendPacketAsync(session.Api.Rest(resting), waitToken),
					async (duration, waitToken) =>
					{
						attackHistoryStart = session.PacketHistory.Count;
						await session.AdvanceAsync(duration, waitToken);
						await session.SynchronizeAsync(waitToken);
						int[] attackers = session.PacketHistory.Skip(attackHistoryStart)
							.Where(packet => packet.PacketType == typeof(SM_ATTACK) &&
								packet.Get<int>("targetObjId") == session.CharacterId)
							.Select(packet => packet.Get<int>("attackerObjId")).ToArray();
						return new NaturalRestTick(world.IsDead || world.CurrentHp <= 0, attackers);
					},
					async (attackers, defendToken) =>
					{
						interrupted = true;
						session.TraceDiagnostic("rest-interrupted-by-attack", new Dictionary<string, object?>
						{
							["attackers"] = attackers,
							["hp"] = world.CurrentHp,
							["position"] = session.CurrentPosition,
						});
						if (navigator.DefendOnAttackAsync is { } defend)
							await defend(attackers.ToArray(), navigator.LastMovementStart ?? session.CurrentPosition,
								attackHistoryStart, defendToken);
						// Never sit under attack: fight whatever is still on the Priest, nearest first. A fight that
						// ends in a retreat re-observes; attackers left more than 30 m behind are no longer a threat.
						for (int fight = 0; fight < MaximumCombatActions && !world.IsDead && world.CurrentHp > 0; fight++)
						{
							int[] remaining = attackers.Distinct()
								.Where(attacker => !navigator.UnavailableObjects.Contains(attacker) &&
									world.Objects.TryGetValue(attacker, out BotKnownObject? observed) &&
									Distance(session.CurrentPosition, observed.Position) < 30)
								.OrderBy(attacker => Distance(session.CurrentPosition, world.Objects[attacker].Position))
								.ToArray();
							if (remaining.Length == 0) break;
							session.TraceDiagnostic("rest-defend", new Dictionary<string, object?>
							{
								["attacker"] = remaining[0],
								["remaining"] = remaining,
								["hp"] = world.CurrentHp,
								["position"] = session.CurrentPosition,
							});
							if (await TryKillAsync(remaining[0], defendToken, session.CurrentPosition))
								navigator.UnavailableObjects.Add(remaining[0]);
						}
					}, token);
				if (outcome == NaturalRestOutcome.Dead)
				{
					await ReviveAtBindAsync(token);
					return;
				}
				if (!interrupted) quietIntervals++;
				await session.SynchronizeAsync(token);
			}
			throw new InvalidDataException("Priest could not recover HP/MP before the next pull within twelve quiet rest intervals.");
		}

		private async Task ReviveAtBindAsync(CancellationToken token)
		{
			if (StopNi07OnDeath)
			{
				string nearby = string.Join(',', navigator.Observe().Npcs
					.Where(npc => Distance(npc.Position, session.CurrentPosition) < 30)
					.Select(npc => $"{npc.TemplateId}/{npc.ObjectId}@" +
						$"{Distance(npc.Position, session.CurrentPosition):F1}m"));
				session.TraceDiagnostic("stop-on-death", new Dictionary<string, object?>
				{
					["mapId"] = session.Api.World.MapId,
					["level"] = session.Api.World.Level,
					["position"] = session.CurrentPosition,
					["hp"] = session.Api.World.CurrentHp,
					["mp"] = session.Api.World.CurrentMp,
					["target"] = engagedTarget,
					["nearby"] = nearby,
					["recentCombatChoices"] = lastCombatTrace,
				});
				throw new InvalidDataException($"NI-07 stop-on-death: {session.CurrentStep}/" +
					$"{session.CurrentAction}, level={session.Api.World.Level}, target={engagedTarget}, " +
					$"position={session.CurrentPosition}, nearby={nearby}; " +
					$"trace={string.Join(" | ", lastCombatTrace)}; " +
					$"combatTrace={session.CombatTracePath}.");
			}
			DeathSpots.Add(session.CurrentPosition);
			// A 41-quest journey through Q2007's camps can die several times, as a player does; twenty deaths
			// is a finding worth stopping for, three was not.
			if (++revives > MaximumRevives)
			{
				string nearby = string.Join(", ", navigator.Observe().Npcs
					.Where(npc => Distance(npc.Position, session.CurrentPosition) < 30)
					.Select(npc => $"{npc.TemplateId}/{npc.ObjectId}:{Distance(npc.Position, session.CurrentPosition):F1}m"));
				throw new InvalidDataException($"Natural Priest exceeded {MaximumRevives} ordinary bind revives; " +
					$"level={session.Api.World.Level}, " +
					$"target={engagedTarget}, position={session.CurrentPosition}, nearby={nearby}; " +
					$"trace={string.Join(" | ", lastCombatTrace)}.");
			}
			session.BeginStep($"ni07-bind-revive-{revives}", "accept-client-death-and-revive-at-bound-obelisk");
			if (!session.Api.World.IsDead)
			{
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(500), token);
				await session.WaitForPacketAsync(typeof(SM_DIE), token);
			}
			await session.SendPacketAsync(session.Api.Revive(BotReviveType.Bind), token);
			await session.WaitForPacketAsync(typeof(SM_CHANNEL_INFO), token);
			await session.SynchronizeAsync(token);
			if (session.Api.World.IsDead) throw new InvalidDataException("Bind revive did not clear client-observed death.");
			session.AcceptTeleportPosition();
		}

		/// <summary>Walk up to 8 m toward the target's last-known position (never nearer than 10 m), on a
		/// checked route that stays out of other observed circles.</summary>
		private async Task CloseInAfterRangeRejectionAsync(int target, CancellationToken token)
		{
			if (!session.Api.World.Objects.TryGetValue(target, out BotKnownObject? observed) ||
				session.Api.World.MapId is not int map) return;
			BotPosition start = session.CurrentPosition;
			// A walker is where its last SM_MOVE was heading, not where that move began.
			BotPosition settled = observed.SettledPosition;
			float distance = Distance(start, settled);
			if (distance <= 10) return;
			float t = MathF.Min(8, distance - 10) / distance;
			BotPosition? goal = geometry.SnapToGround(map, start with
			{
				X = start.X + (settled.X - start.X) * t,
				Y = start.Y + (settled.Y - start.Y) * t,
			});
			IReadOnlyList<BotPosition> route = goal == null ? [] : geometry.FindLocalPath(map, start, goal.Value);
			bool safe = route.Count > 0 && navigator.IsSegmentSafe(route, target);
			session.TraceDiagnostic("combat-range-close-in", new Dictionary<string, object?>
			{
				["targetObjectId"] = target,
				["from"] = start,
				["goal"] = goal,
				["clientTargetDistance"] = distance,
				["routePoints"] = route.Count,
				["safe"] = safe,
			});
			if (!safe) return;
			await navigator.MoveAsync(route, token);
			await navigator.SynchronizeAsync(token);
		}

		private async Task<bool> CastAsync(NaturalPriestSkill skill, int target, CancellationToken token)
		{
			BotSkill learned = session.Api.World.Skills[skill.Id];
			await session.SendPacketAsync(session.Api.Target(target), token);
			await session.SendPacketAsync(session.Api.Cast(new SpellCastData(skill.Id,
				checked((byte)learned.Level), 0) { TargetObjectId = target }), token);
			DecodedBotServerPacket started;
			try
			{
				started = await BotCastProtocol.WaitForStartAsync(
					(predicate, waitToken) => session.WaitForPacketAsync(packet => predicate(packet) ||
						BotCastProtocol.IsStartRejection(packet), waitToken),
					session.CharacterId, skill.Id, token);
			}
			catch (TimeoutException exception)
			{
				string[] recent = session.PacketHistory.TakeLast(15).Select(packet =>
					packet.PacketType == typeof(SM_SYSTEM_MESSAGE)
						? $"{packet.PacketType.Name}:{packet.Get<object>("name") ?? packet.Get<int>("msgId")}" : packet.PacketType.Name).ToArray();
				throw new InvalidDataException($"Cast {skill.Id} on {target} had no start; " +
					$"HP={session.Api.World.CurrentHp}/{session.Api.World.MaxHp}, " +
					$"MP={session.Api.World.CurrentMp}/{session.Api.World.MaxMp}, " +
					$"position={session.CurrentPosition}; packets={string.Join(',', recent)}.", exception);
			}
			if (started.PacketType == typeof(SM_SYSTEM_MESSAGE))
			{
				// A rejected cast never starts on the server; release the bot's local
				// casting gate before any legal movement or retry.
				session.Api.Timing.RecordCastCancelled();
				if (started.Get<object>("name") is "STR_SKILL_NOT_ENOUGH_DISTANCE")
				{
					int rejection = ++rangeRejections;
					session.TraceDiagnostic("combat-range-rejected", new Dictionary<string, object?>
					{
						["targetObjectId"] = target,
						["skillId"] = skill.Id,
						["rejection"] = rejection,
						["clientTargetDistance"] = session.Api.World.Objects.TryGetValue(target,
							out BotKnownObject? observed) ? Distance(session.CurrentPosition, observed.Position) : null,
					});
					if (rejection > 3) return false; // Reacquire another observed guard, not an infinite stale pull.
					// The server measured more than the skill's range although the client's last-known
					// position says otherwise: a walker moved on without a fresh SM_MOVE. Close in along
					// checked ground, as a player walks toward a target that drifted out of range.
					await CloseInAfterRangeRejectionAsync(target, token);
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(300), token);
					await session.SynchronizeAsync(token);
					return true; // Re-evaluate range, health and attackers before retrying.
				}
				if (started.Get<object>("name") is "STR_SKILL_OBSTACLE" &&
					session.Api.World.Objects.TryGetValue(target, out BotKnownObject? obstructedTarget) &&
					++obstacleRepositions <= 2)
				{
					// Aim at where a walking target settled (its SM_MOVE target), not where its walk began.
					obstructedTarget = obstructedTarget with { Position = obstructedTarget.SettledPosition };
					IReadOnlyList<BotPosition> checkedApproach = await navigator.FindRouteAsync(
						session.CurrentPosition, obstructedTarget.Position, token);
					NaturalNavigationObject[] otherHostiles = navigator.Observe().Npcs
						.Where(npc => npc.ObjectId != target &&
							NaturalHostility.IsAggressive(Aion.GameServer.Dataholders.DataManager.NPC_DATA.GetNpcTemplate(npc.TemplateId)))
						.ToArray();
					BotPosition? firingPoint = checkedApproach
						.Where(point => Distance(point, obstructedTarget.Position) is >= 8 and <= 21 &&
							geometry.HasLineOfSight(session.Api.World.MapId!.Value, point, obstructedTarget.Position) &&
							otherHostiles.All(enemy => Distance(point, enemy.Position) > 9))
						.OrderByDescending(point => otherHostiles.Length == 0 ? float.MaxValue :
							otherHostiles.Min(enemy => Distance(point, enemy.Position)))
						.ThenByDescending(point => Distance(point, obstructedTarget.Position))
						.Cast<BotPosition?>().FirstOrDefault();
					if (firingPoint == null) return false; // Reject this pull; the caller may choose another observed mob.
					NaturalNavigationResult reposition = await NaturalIshalgenNavigator.ExploreAnchorAsync(
						session.Api.World.MapId!.Value, -1, firingPoint.Value, navigator,
						"obstructed-combat-target", token);
					if (!reposition.Arrived) return false;
					return true; // Re-evaluate the visible target and healing state before recasting.
				}
				if (started.Get<object>("name") is "STR_SKILL_OBSTACLE") return false;
				if (started.Get<object>("name") is "STR_SKILL_CAN_NOT_ATTACK_WHILE_IN_ABNORMAL_STATE")
				{
					// Stunned or knocked down: the state wears off in a second or two. Re-evaluate then.
					session.TraceDiagnostic("combat-cast-while-disabled", new Dictionary<string, object?>
					{
						["skillId"] = skill.Id,
						["hp"] = session.Api.World.CurrentHp,
						["position"] = session.CurrentPosition,
					});
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(1000), token);
					await session.SynchronizeAsync(token);
					return true;
				}
				if (session.Api.World.CurrentHp <= 0 || session.Api.World.IsDead)
				{
					await ReviveAtBindAsync(token);
					return false;
				}
				throw new InvalidDataException($"Cast {skill.Id} rejected: {started.Get<object>("name")}.");
			}
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(started.Get<ushort>("castDuration") + 1), token);
			DecodedBotServerPacket result = await BotCastProtocol.WaitForCompletionAsync(
				session.WaitForPacketAsync, session.CharacterId, skill.Id, token);
			if (result.PacketType == typeof(SM_CASTSPELL_RESULT))
			{
				int deciseconds = result.Get<int>("cooldown");
				if (deciseconds > 0)
					cooldowns[skill.CooldownId] = fixture.Epoch.AddMilliseconds(fixture.Clock.NowMillis + deciseconds * 100L);
			}
			await session.AdvanceAsync(BotCastProtocol.RecoveryDelay(result), token);
			return true;
		}
	}

	private static async Task<bool> TryLootCorpseItemAsync(SimulationL0Session session,
		int objectId, int itemId, CancellationToken token)
	{
		long beforeCount = ItemCount(session.Api.World, itemId);
		await session.SendPacketAsync(session.Api.Loot(objectId), token);
		DecodedBotServerPacket list;
		using (var lootTimeout = CancellationTokenSource.CreateLinkedTokenSource(token))
		{
			lootTimeout.CancelAfter(TimeSpan.FromSeconds(5));
			try
			{
				list = await session.WaitForPacketAsync(typeof(SM_LOOT_ITEMLIST), lootTimeout.Token,
					packet => packet.Get<int>("targetObjectId") == objectId);
			}
			catch (OperationCanceledException) when (!token.IsCancellationRequested)
			{
				session.TraceDiagnostic("quest-loot-list-missing", new Dictionary<string, object?>
				{
					["objectId"] = objectId,
					["itemId"] = itemId,
					["inventoryCount"] = beforeCount,
					["position"] = session.CurrentPosition,
				});
				return false;
			}
		}
		IReadOnlyDictionary<string, object?>? item = list
			.Get<List<IReadOnlyDictionary<string, object?>>>("items")
			.FirstOrDefault(entry => Get<int>(entry, "itemId") == itemId);
		if (item == null)
		{
			await session.SendPacketAsync(session.Api.Loot(objectId, close: true), token);
			session.TraceDiagnostic("quest-drop-attempt", new Dictionary<string, object?>
			{
				["objectId"] = objectId,
				["itemId"] = itemId,
				["dropped"] = false,
				["inventoryCount"] = ItemCount(session.Api.World, itemId),
			});
			return false;
		}
		BotInventoryItem? existing = session.Api.World.Inventory.Values.SingleOrDefault(entry => entry.ItemId == itemId);
		await session.SendPacketAsync(session.Api.Loot(objectId, Get<byte>(item, "index")), token);
		if (existing == null)
			await session.WaitForPacketAsync(typeof(SM_INVENTORY_ADD_ITEM), token,
				packet => packet.Get<List<IReadOnlyDictionary<string, object?>>>("items")
					.Any(entry => Get<int>(entry, "itemId") == itemId));
		else
			await session.WaitForPacketAsync(typeof(SM_INVENTORY_UPDATE_ITEM), token,
				packet => packet.Get<int>("objectId") == existing.ObjectId);
		await session.SendPacketAsync(session.Api.Loot(objectId, close: true), token);
		long afterCount = ItemCount(session.Api.World, itemId);
		session.TraceDiagnostic("quest-drop-attempt", new Dictionary<string, object?>
		{
			["objectId"] = objectId,
			["itemId"] = itemId,
			["dropped"] = true,
			["inventoryCount"] = afterCount,
		});
		Assert.True(afterCount > beforeCount,
			$"Corpse {objectId} listed quest item {itemId}, but inventory stayed at {beforeCount}.");
		return true;
	}

	private static float Distance(BotPosition a, BotPosition b) => MathF.Sqrt(
		MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2) + MathF.Pow(a.Z - b.Z, 2));
}
