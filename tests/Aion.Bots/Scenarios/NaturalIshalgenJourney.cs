using Aion.Bots.Dashboard;
using Aion.Bots.Navigation;
using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.Movement;
using Aion.Bots.Protocol;
using Aion.Bots.Reflexes;
using Aion.Bots.Timing;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.Network.Aion.ServerPackets;

using Require = Aion.Bots.Scenarios.NaturalJourneyRequirements;

namespace Aion.Bots.Scenarios;

public sealed class NaturalIshalgenJourney(INaturalJourneySession session, NaturalJourneyRuntime runtime, NaturalJourneyOptions options)
{
	private enum TemplatePhase { Full, Work, Claim }
	private sealed class NaturalJourneyCheckpointStopException : Exception;
	private sealed class NaturalEarlyAscensionRequiredException : Exception;
	private sealed class NaturalCapitalCheckpointStopException(string stage) : Exception
	{
		public string Stage { get; } = stage;
	}
	private sealed record CapitalPayment(int QuestId, long Experience, long Kinah);
	private sealed class NaturalCombatApproachBlockedException(string message) : IOException(message);
	private sealed class NaturalGuardedObjectiveRevivedException(string message) : IOException(message);
	private sealed class NaturalHaramelSourcesExhaustedException(string message) : IOException(message);
	private sealed class NaturalHaramelGroundApproachUnavailableException(string message) : IOException(message);
	private sealed class NaturalBookCollectionMapChangedException : IOException;

	/// <summary>AC-06: the Leg 3 escort's clear areas hold grave robbers with respawn_time 295 s (the Altgard spawn data);
	/// a clear holds that long after its first kill.</summary>
	private const long EscortClearRespawnMillis = 295_000;
	// The general quest-loot sweep opens lootable corpses this close after a kill (a Cleric fights at spell range, 25 m).
	private const float LootSweepReach = 60f;
	// AB-08: how far inside the Q24013 poison zone's edge the poison is used.
	private const float ZoneMargin = 3f;
	// AG-07: an Altgard leg walks its road toward an approach from farther than this, and leaves the last stretch to the navigator.
	private const float FarApproachMetres = 100f, FarApproachStop = 40f;

	/// <summary>ND-05: a controlled probe uses the journey's ordinary rest, buffs and combat against
	/// a target produced by a real quest dialog. No loot action follows a kill that leaves this map.</summary>
	public async Task<NaturalCombatDiagnosticResult> RunObservedCombatAsync(
		Func<CancellationToken, Task<int>> startEncounter, CancellationToken token,
		Func<CancellationToken, Task>? afterKill = null, Action? afterBindRevive = null)
	{
		int map = session.Api.World.MapId ?? throw new InvalidDataException("Combat map unobserved.");
		BotNavigationGeometry geometry = runtime.CreateGeometry();
		var graph = BotNavigationGraphFactory.Build(runtime.Data, [], geometry);
		var navigator = new NaturalJourneyNavigator(session, graph, geometry, runtime, stopOnDeath: false);
		var combat = new NaturalJourneyCombat(session, navigator, runtime, geometry, stopOnDeath: false,
			conservativeRangedHold: false, NaturalMauPolicyParameters.Baseline)
		{
			ApproachMapId = map, AfterKillAsync = afterKill, AfterBindRevive = afterBindRevive,
		};
		await combat.RestAsync(token);
		long started = runtime.NowMillis;
		int target = await startEncounter(token);
		bool killed = await combat.TryKillAsync(target, token, session.CurrentPosition);
		if (killed && session.Api.World.MapId != map)
		{
			await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token,
				packet => packet.Get<int>("objectId") == session.CharacterId);
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(token);
		}
		return new(killed, combat.ReviveCount, combat.CompletedRetreats, runtime.NowMillis - started);
	}

	public async Task RunAsync(CancellationToken token)
	{
		NaturalCapitalDecisionEngine.ValidateScope(options, runtime.Profile);
		NaturalLaterCapitalContract? laterCapital = options.LaterCapital ? NaturalLaterCapitalContract.LoadDefault() : null;
		laterCapital?.ValidateScope(options, runtime.Profile);
		bool continuousAltgard = options.AltgardLegId == "all";
		if (continuousAltgard && (!runtime.Profile.StartsWith("SIM-", StringComparison.Ordinal) || !options.AscensionBridge ||
			options.StopAfterQuest != null || options.StopAt != null || options.RelogAt != null || options.AltgardOnlyQuests != null ||
			options.Course != null || options.ClericEncounter || options.CoinGearReceiptPath != null || options.HaramelProgressPath != null))
			throw new InvalidOperationException("The complete SIM journey requires creation, Ascension and every approved leg without diagnostic shortcuts or saved receipts.");
		session.IdentityAltgardLegId = options.AltgardLegId;
		if (options.MauPolicy != null && options.Course == null)
			throw new InvalidOperationException("A tuned Mau policy is restricted to the focused SIM course.");
		NaturalMauPolicyParameters mauPolicy = options.MauPolicy ?? NaturalMauPolicyParameters.Baseline;
		mauPolicy.Validate();
		bool stopAfterQ2004 = options.StopAfterQuest == 2004, stopAfterQ2005 = options.StopAfterQuest == 2005,
			stopAfterQ2006 = options.StopAfterQuest == 2006, stopAfterQ2007 = options.StopAfterQuest == 2007;
		bool fightRouteOpen = false;
		int emptySpawnWaits = 0;
		BotPosition? fightRouteOpenAt = null;
		var quietNeighbours = new HashSet<int>();
		var combatTrace = runtime.Trace;
		string combatTracePath = session.CombatTracePath ?? throw new InvalidOperationException("Natural journey requires a trace path.");
		var dashboard = runtime.Dashboard;
		NaturalIshalgenContract contract = NaturalIshalgenContract.LoadDefault();
		NaturalCapitalContract capitalContract = NaturalCapitalContract.LoadDefault();
		NaturalJourneyCheckpoint? checkpoint = null;
		NaturalCoinGearProgress? coinGearProgress = null;
		NaturalHaramelProgress? haramelProgress = null;
		long HaramelNow() => checked(runtime.Epoch.ToUnixTimeMilliseconds() + runtime.NowMillis);
		NaturalJourneyRunContext RunContext() => new(combatTrace.Run, runtime.Profile, runtime.Seed,
			typeof(NaturalIshalgenJourney).Assembly.GetCustomAttributes(false)
				.OfType<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion,
			typeof(NaturalIshalgenJourney).Module.ModuleVersionId.ToString(), runtime.NowMillis);
		combatTrace.WriteAction("ni08-run", "natural-run-context", new Dictionary<string, object?> { ["context"] = RunContext() });
		if (options.Course != null)
			combatTrace.WriteAction("ni08-run", "phase2-policy", new Dictionary<string, object?>
			{
				["policyId"] = mauPolicy.Id, ["parameters"] = mauPolicy,
				["baseline"] = options.MauPolicy == null,
			});
		void WriteFailure(string kind, Exception failure)
		{
			// Diagnosis must never replace the original failure, including an incomplete or wrong-character login.
			try
			{
				if (session.Api.World.LoginStateObserved && session.Api.World.SelfObjectId == session.CharacterId)
					checkpoint = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
						session.ConnectionGeneration, contract, session.CurrentPosition, coinGearProgress: coinGearProgress, haramelProgress: haramelProgress, earlyAscension: options.AscensionBridge);
				string path = new NaturalJourneyFailure(kind, session.CurrentStep, session.CurrentAction,
					session.ConnectionGeneration, failure.GetType().FullName!, failure.Message, failure.StackTrace,
					checkpoint, combatTracePath, session.PacketHistory.TakeLast(64).Select(p => p.PacketType.Name).ToArray(), RunContext())
					{ ServerProblems = System.Text.Json.JsonSerializer.SerializeToElement(runtime.SnapshotProblems()) }
					.Write(Path.GetDirectoryName(combatTracePath)!);
				Console.WriteLine($"NI-08 failure package: {path}");
			}
			catch (Exception diagnosisFailure)
			{
				Console.Error.WriteLine($"NI-08 could not save diagnosis for {failure}: {diagnosisFailure}");
			}
		}
		try
		{
			bool resuming = await runtime.EnterAsync(token);
			if (options.CapitalStage == "first")
				Require.True(resuming && session.Api.World.CompletedQuestIds.IsSupersetOf(new[] { 2008, 2009 }),
					"The contained capital pass must resume the retained post-ceremony character.");
			Require.True(!continuousAltgard || !resuming, "The complete SIM journey must create a fresh character.");
			if (options.Course != null || options.ClericEncounter)
			{
				if (resuming || runtime.PrepareCourseAsync == null)
					throw new InvalidOperationException("A Mau course or Cleric encounter requires a fresh SIM character and explicit preparation.");
				await runtime.PrepareCourseAsync(token);
			}
			// The LIVE movie-end reply can arrive after the first time-check barrier: the
			// reflex is sent while draining login packets. Observe completion explicitly.
			if (!resuming && !session.Api.World.CompletedQuestIds.Contains(2000))
				await session.WaitForPacketAsync(typeof(SM_QUEST_ACTION), token,
					packet => packet.Get<int>("questId") == 2000 && packet.Get<byte>("status") == 5);
			checkpoint = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
				session.ConnectionGeneration, contract, session.CurrentPosition, coinGearProgress: coinGearProgress, haramelProgress: haramelProgress, earlyAscension: options.AscensionBridge);
			await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "login-observation.json"),
				System.Text.Json.JsonSerializer.Serialize(checkpoint), token);
			Require.Contains(2000, session.Api.World.CompletedQuestIds);

			IReadOnlyDictionary<int, QuestRunPlan> templatePlans = QuestRunPlan.LoadDirectory(
				Path.Combine(runtime.RepoRoot, "parity-artifacts", "e2e",
					"natural-ishalgen-plans")).ToDictionary(plan => plan.Id);
			Require.Equal(26, templatePlans.Count);
			Require.All(templatePlans.Keys, id => Require.Contains(id, contract.Quests.Select(quest => quest.Id)));
			NaturalDecision decision = NaturalIshalgenDecisionEngine.Decide(contract,
				ObserveNaturalJourney(session), 1, earlyAscension: options.AscensionBridge);
			if (!resuming && options.Course == null && !options.ClericEncounter)
			{
				Require.Equal("find-quest-starter", decision.SelectedAction);
				Require.Equal(2101, decision.SelectedQuestId);
			}

			BotNavigationGeometry geometry = runtime.CreateGeometry();
			// A stumble into a rock face (Java stops the knockback at the first collision, at the old height) lands
			// off walkable ground: stand on the ground at its foot, as the client does, instead of where no step is legal.
			session.ResolveForcedLanding = landed =>
			{
				if (geometry.NavMesh?.NavMeshes.Get(contract.MapId) is not { } mesh ||
					mesh.Snap(landed, BotNavQuery.Default with { SnapHorizontal = 0.5f, SnapVertical = 2 }) != null ||
					mesh.Snap(landed, BotNavQuery.Default with { SnapHorizontal = 3, SnapVertical = 4 }) is not BotPosition ground)
					return landed;
				session.TraceDiagnostic("forced-landing-on-ground", new Dictionary<string, object?>
				{
					["landed"] = landed,
					["standing"] = ground,
				});
				return ground with { Heading = landed.Heading };
			};
			// AF-08: Altgard Leg 1 walks to its own NPCs and hunts on the Ice Lake; they join the waypoint graph.
			// AM-06/07: the same runner plays any Altgard leg ("l1" the fortress, "l2" Moslan Crossroad).
			string? altgardLegId = continuousAltgard ? null : options.AltgardLegId ?? (options.AltgardLeg1 ? "l1" : null);
			NaturalAltgardContract? altgardLeg = altgardLegId is { } legId ? NaturalAltgardContract.LoadLeg(legId) : null;
			if (laterCapital != null && altgardLeg != null)
				altgardLeg = NaturalAltgardContinuation.BindIncoming(altgardLeg, session.Api.World.CompletedQuestIds,
					session.Api.World.Inventory.Values.Select(i => new NaturalJourneyItem(i.ObjectId, i.ItemId, i.Count, i.EquipmentSlot)).ToArray());
			coinGearProgress = altgardLeg?.CoinGear == null ? null : NaturalCoinGearProgress.Empty;
			if (altgardLeg?.Haramel is { } haramel)
				haramelProgress = options.HaramelProgressPath is { Length: > 0 } savedHaramel
					? NaturalHaramelProgress.Read(savedHaramel, session.CharacterId, HaramelNow(), session.Api.World, altgardLeg)
					: NaturalHaramelProgress.Begin(session.CharacterId, HaramelNow(), session.Api.World, haramel);
			else if (options.HaramelProgressPath is { Length: > 0 })
				throw new InvalidDataException("Haramel receipts require the l12 leg.");
			if (options.CoinGearReceiptPath is { Length: > 0 } receiptPath)
			{
				NaturalCoinGear gear = altgardLeg?.CoinGear ?? throw new InvalidDataException("Coin receipts require the CG leg.");
				coinGearProgress = NaturalCoinGearProgress.ReadVerifiedEndpoint(receiptPath, session.CharacterId, gear,
					NaturalAltgardObservation.Observe(session.Api.World, session.CurrentPosition));
			}
			NaturalJourneyItem[] coinIncomingLoadout = altgardLeg?.CoinGear == null ? [] : session.Api.World.Inventory.Values
				.Where(i => i.EquipmentSlot is > 0 and < 65535 && i.EquipmentSlot is not (16 or 4096 or 8192 or 16384))
				.Select(i => new NaturalJourneyItem(i.ObjectId, i.ItemId, i.Count, i.EquipmentSlot)).ToArray();
			int[] destinyIncomingSkills = altgardLeg?.Destiny is { } incomingDestiny
				? session.Api.World.Skills.Keys.Where(id => id != incomingDestiny.StigmaSkillId).ToArray() : [];
			IReadOnlyDictionary<int, QuestRunPlan> altgardPlans = altgardLegId is { } planLeg
				? NaturalAltgardContract.LoadPlans(planLeg) : new Dictionary<int, QuestRunPlan>();
			var collectionLimits = altgardPlans.Values.SelectMany(plan => plan.Steps
				.Where(step => step.Kind == "collect" && step.ItemId > 0 && step.Count > 0)
				.Select(step => (plan.Id, step.ItemId, step.Count)))
				.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.ToArray());
			bool QuestDropStillNeeded(int itemId) => !collectionLimits.TryGetValue(itemId, out var needs) ||
				needs.Any(need => !session.Api.World.CompletedQuestIds.Contains(need.Id) &&
					ItemCount(session.Api.World, itemId) < need.Count);
			// The maintainer's 2026-10-01 note: hubs have flight transporters; their routes come from the client (generated).
			IReadOnlyList<NaturalAirlineRoute> airlines = NaturalAirlineRoutes.Load(runtime.RepoRoot);
			// AG-07: an Altgard leg's road walk toward a far approach (set by the leg's runner; see ApproachShippedSpawnAsync).
			Func<BotPosition, int, Func<bool>?, Task>? farApproach = null;
			Func<int, float, Func<int?>?, Task<int>>? haramelApproach = null;
			Func<IReadOnlyList<int>, int>? haramelKind = null;
			bool farApproachUnderway = false;
			bool bookCollectionUnderway = false;
			int[] altgardNpcs = continuousAltgard ? NaturalAltgardContinuation.Order.SelectMany(id =>
				NaturalAltgardContract.LoadLeg(id).GraphNpcIds(NaturalAltgardContract.LoadPlans(id)))
				.Concat(airlines.Select(route => route.NpcId)).Distinct().ToArray()
				: altgardLeg == null ? [] : altgardLeg.GraphNpcIds(altgardPlans)
					.Concat(airlines.Where(route => route.MapId == altgardLeg.Hub.MapId).Select(route => route.NpcId)).Distinct().ToArray();
			if (laterCapital != null) altgardNpcs = altgardNpcs
				.Concat(NaturalLaterCapitalSteps.HeritagePickup.Concat(NaturalLaterCapitalSteps.BookPreparation).Select(step => step.NpcId))
				.Concat(NaturalLaterCapitalSteps.HeritageCity.Concat(NaturalLaterCapitalSteps.RobePreparation)
					.Concat(NaturalLaterCapitalSteps.Juice).Append(NaturalLaterCapitalSteps.MaternalReturn)
					.Append(NaturalLaterCapitalSteps.RobeBerth).Concat(NaturalLaterCapitalSteps.Leg7City)
					.Append(NaturalLaterCapitalSteps.RobeHeart).Concat(NaturalLaterCapitalSteps.Leg9City)
					.Append(NaturalLaterCapitalSteps.LibraryPermission).Concat(NaturalLaterCapitalSteps.FinalCity).Select(step => step.NpcId))
				.Append(700211)
				.Concat(new[] { 210404, 203679, 203581, 204191 }).Concat(airlines.Select(route => route.NpcId)).Distinct().ToArray();
			// AK-08: items an open Altgard quest still needs (its collect items, the ring carriers' rings): never worn as gear and
			// never sold. The leg 5 smoke run wore Q2292's level 16 rings as upgrades, which its hand-in would not have found.
			IReadOnlySet<int> QuestNeededItems() => (altgardLeg == null ? Enumerable.Empty<int>() : altgardPlans.Values
				.Where(plan => !session.Api.World.CompletedQuestIds.Contains(plan.Id))
				.SelectMany(plan => plan.Steps.Where(step => step.Kind == "collect").Select(step => step.ItemId))
				.Concat(altgardLeg.TimedSpawnList.Where(carrier => !session.Api.World.CompletedQuestIds.Contains(carrier.QuestId))
					.Select(carrier => carrier.ItemId))
				.Concat(altgardLeg.Destiny is { } destiny ? new[] { destiny.StoneItemId, destiny.RewardBundleId, destiny.LegacyRewardId } : [])
				.Concat(altgardLeg.CoinGear?.ProtectedItemIds ?? [])
				.Concat(altgardLeg.Haramel?.ProtectedItemIds ?? [])
				.Concat(altgardLeg.Haramel?.CleanupItemIds ?? [])
				.Concat(altgardLeg.Haramel?.TowerChestKeys?.Select(key => key.ItemId) ?? [])
				.Where(item => item > 0)).Concat(capitalContract.ProtectedItemIds)
				.Concat(laterCapital?.ProtectedItemIds ?? new HashSet<int>()).ToHashSet();
			BotNavigationGraph graph = BotNavigationGraphFactory.Build(runtime.Data, altgardNpcs.Concat(new[] {
				203500, 203504, 203501, 203502, 203516, 203518,
				203519, 203534, 790002, 210377, 210378, 700045, 203538,
				203539, 210592, 700047, 203550, 210402, 210403, 203530, 203535, 203551, 203547,
				203540, 210395, 210396, 210750, 700095,
				203552, 203554, 700085, 700086, 700087, 203517,
				203533, 210734, 203514, 203543, 203532, 203531, 700128,
					210363, 210367, 210369, 700124, 700093,
					700063, 203513, 203545, 203679}),
				geometry);
			var navigator = new NaturalJourneyNavigator(session, graph, geometry, runtime, options.StopOnDeath)
			{
				// Level-aware roads-and-branches planning for long legs; null (grid/navmesh chain only)
				// when the map has no baked navmesh or travel graph.
				Planner = BotTravelPlanner.For(NaturalIshalgenRoads.MapId, geometry, runtime.Data),
			};
			// NA-06: navigation for whichever map the client entered. Ishalgen keeps the journey's own navigator;
			// any other map (an instance, Pandaemonium, Altgard) gets geometry, graph and planner of its own.
			var mapNavigators = new NaturalJourneyMapContexts<NaturalJourneyNavigator>(key =>
			{
				if (key.MapId == contract.MapId) return navigator;
				BotNavigationGeometry mapGeometry = runtime.CreateGeometry();
				BotNavigationGraph mapGraph = BotNavigationGraphFactory.Build(runtime.Data,
					key.MapId == 320010000 ? [205020] : altgardLeg != null || continuousAltgard || laterCapital != null ? altgardNpcs : [], mapGeometry);
				return new NaturalJourneyNavigator(session, mapGraph, mapGeometry, runtime, options.StopOnDeath)
				{
					Planner = BotTravelPlanner.For(key.MapId, mapGeometry, runtime.Data),
				};
			});
			BotPosition? easternRoadIngressStart = null;
			BotPosition[] easternRoadIngress = [];
			NaturalJourneyCombat? navigationDefense = null;
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
					try
					{
						if (await navigationDefense.TryKillAsync(attacker, defendToken, refuge, packetStart))
							navigator.UnavailableObjects.Add(attacker);
						else return; // Re-observe after a retreat or lost target before another pull.
					}
					catch (NaturalCombatApproachBlockedException blocked) when (altgardLeg?.Haramel?.MapId == session.Api.World.MapId)
					{
						session.TraceDiagnostic("haramel-navigation-defense-replan", new Dictionary<string, object?>
						{ ["attacker"] = attacker, ["position"] = session.CurrentPosition, ["reason"] = blocked.Message });
						return;
					}
				}
			};
			var combat = new NaturalJourneyCombat(session, navigator, runtime, geometry,
				options.StopOnDeath, options.OptimizeHubs, mauPolicy, haramelProgress?.Revives ?? 0);
			navigationDefense = combat;
			// The general quest-loot rule: after any kill, open every corpse near the Cleric that the server marked lootable
			// for it, and take its quest items, as a player does. A quest item drops only while its quest needs it, so every
			// one is wanted. Before this only the monster a step aimed at was looted: in Leg 4, Q2230 got 6 tusks from 21
			// mosbears (85% drop) and spent three timers, because the mosbears that died as adds or on the way were left.
			var lootSwept = new HashSet<int>();
			bool sweeping = false;
			void WithQuestLoot(NaturalJourneyCombat fighter) => fighter.AfterKillAsync = SweepQuestItemsWhenSafeAsync;
			async Task SweepQuestItemsWhenSafeAsync(CancellationToken sweepToken)
			{
				if (sweeping || Engaged().Attackers.Length > 0) return;
				sweeping = true;
				try { await LootQuestItemsAroundAsync(sweepToken); }
				finally { sweeping = false; }
			}
			WithQuestLoot(combat);
			bool maintainingInventory = false;
			var workedTemplates = new HashSet<int>();
			long lastReturnMillis = long.MinValue / 2;
			long failedRestockKinah = -1;
			var refusedGear = new HashSet<int>();
			PlayerClass? gearClass = null;
			bool bridgeShopVisited = false; // NA-16: the Altgard shop stop is done
			var helpSupplied = new List<NaturalHelpSupplied>(); // NA-21: every approved help item supplied
			int helpCheckedAtLevel = -1;
			bool earlyAscensionUnderway = false;
			NaturalJourneyCheckpoint? capitalBefore = null;
			var capitalPayments = new Dictionary<int, CapitalPayment>();
			int capitalSequences = 0;
			long capitalFares = 0;
			var capitalTravel = new NaturalCapitalTravel(session, runtime.RepoRoot, () => runtime.NowMillis);
			bool CapitalPending() => capitalContract.CompletedQuestIds.Any(id => !session.Api.World.CompletedQuestIds.Contains(id));
			bool IshalgenPending() => contract.Quests.Any(quest => !session.Api.World.CompletedQuestIds.Contains(quest.Id));
			bool EarlyAscensionNeeded() => options.AscensionBridge && altgardLegId == null && IshalgenPending() &&
				(session.Api.World.Level >= contract.AscensionLevel || AscensionBridgeStarted()) &&
				(!session.Api.World.CompletedQuestIds.Contains(2009) || CapitalPending() || session.Api.World.MapId is 120010000 or 120020000);

			// Wear the best gear in the bag (the recorded human put on four unused quest rewards at Nalto).
			// The client knows each item's slots, level and class/race limits from its tooltip; the server
			// still checks every equip, and an item it refuses is never asked for again.
			async Task EquipUpgradesAsync(CancellationToken gearToken)
			{
				// CG authorizes exactly three explicit armour equips and freezes the incoming loadout.
				if (altgardLeg?.CoinGear != null) return;
				BotWorldModel world = session.Api.World;
				if (world.IsDead) return;
				// NA-09: the observed class and race decide what can be worn; after Ascension the Cleric's new
				// masteries (chain, shield, staff) make items the Priest was refused wearable, so refusals reset.
				BotKnownObject? self = world.SelfObjectId is int selfId ? world.Objects.GetValueOrDefault(selfId) : null;
				var playerClass = self?.PlayerClass is byte classId
					? PlayerClassExtensions.GetPlayerClassById(classId, true) ?? PlayerClass.PRIEST : PlayerClass.PRIEST;
				var race = self?.Race is byte raceId ? (Race)raceId : Race.ASMODIANS;
				var gender = Gender.MALE; // the natural character is created male; gender is not in the client model
				if (playerClass != gearClass)
				{
					gearClass = playerClass;
					refusedGear.Clear();
				}
				NaturalGearInfo? Describe(int itemId)
				{
					var template = runtime.Data.ItemDataDh.GetItemTemplate(itemId);
					if (template == null || template.GetItemSlot() == 0) return null;
					var genderLimit = template.GetUseLimits()?.GetGenderPermitted();
					return new NaturalGearInfo(template.GetItemSlot(), template.GetRequiredLevel(playerClass), template.GetLevel(),
						(template.GetRace() == Aion.GameServer.Model.Race.PC_ALL || template.GetRace() == race) &&
						(genderLimit == null || genderLimit == gender));
				}
				IReadOnlySet<int> questNeeded = QuestNeededItems();
				foreach (NaturalGearUpgrade upgrade in NaturalGearPolicy.SelectUpgrades(
					world.Inventory.Values.Where(item => !questNeeded.Contains(item.ItemId) && (altgardLeg?.Haramel == null ||
						item.Details.EquippedSlot.GetValueOrDefault() != 0 || NaturalHaramel.CanUpgradeGroup(
							runtime.Data.ItemDataDh.GetItemTemplate(item.ItemId)?.GetItemGroup().ToString()))), world.Level,
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
			// Every aggressive spawn spot, plus every step of a patrol's route (a walker stands anywhere on it).
			var hostileSpawns = graph.GetMap(contract.MapId)!.Waypoints
				.Select(waypoint => (waypoint, template: waypoint.TemplateId is int id
					? runtime.Data.NpcDataDh.GetNpcTemplate(id) : null))
				.Where(entry => runtime.IsAggressive(entry.template))
				.Select(entry => new BotNavigationHazard(entry.waypoint.Position, entry.template!.GetAggroRange())).ToList();
			foreach (var group in runtime.Data.SpawnsDh.GetSpawnsByWorldId(contract.MapId))
			{
				var template = runtime.Data.NpcDataDh.GetNpcTemplate(group.GetNpcId());
				if (!runtime.IsAggressive(template)) continue;
				foreach (var spot in group.GetSpawnTemplates())
				{
					if (spot.GetWalkerId() is not { Length: > 0 } routeId) continue;
					var route = runtime.Data.WalkerDataDh.GetWalkerTemplate(routeId);
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
					string root = runtime.RepoRoot;
					var inventoryPolicy = NaturalIshalgenInventoryPolicy.Load(root,
						world.Inventory.Values.Select(item => item.ItemId));
					NaturalInventoryPlan plan = inventoryPolicy.Decide(world, QuestNeededItems(), altgardLeg?.CoinGear, altgardLeg?.Haramel);
					long basePrice = runtime.Data.ItemDataDh
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
						int vendor = Require.IsType<int>(approach.TargetObjectId);
						await NaturalDialogProtocol.OpenAsync(session, vendor, maintenanceToken);
						await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), maintenanceToken);
						if (plan.Sales.Count > 0)
						{
							await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(vendor, 3), maintenanceToken);
							await session.WaitForPacketAsync(typeof(SM_SELL_ITEM), maintenanceToken);
							foreach (NaturalInventoryDecision sale in plan.Sales)
							{
								await session.SendPacketAsync(session.Api.Sell(vendor,
									[(sale.ObjectId, sale.Count)]), maintenanceToken);
								await session.SynchronizeAsync(maintenanceToken);
							}
						}
						await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(vendor, 2), maintenanceToken);
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
			int QuestStatus(int questId) => session.Api.World.CompletedQuestIds.Contains(questId) ? 5 :
				session.Api.World.Quests.TryGetValue(questId, out var q) ? q.Status : 0;
			int QuestVar(int questId) => session.Api.World.Quests.TryGetValue(questId, out var q) ? q.StepAndFlags & 0x00FFFFFF : 0;
			bool AtQuestStep(int questId, int step) => QuestStatus(questId) == 3 && QuestVar(questId) == step;
			var progress = new NaturalJourneyProgress(TimeSpan.FromMinutes(60),
				templatePlans.Values.Concat(altgardPlans.Values).SelectMany(p => p.Steps).Where(s => s.ItemId > 0).Select(s => s.ItemId).ToHashSet(),
				haramelProgress?.StallBudget);
			long journeyStart = haramelProgress is { } originalHaramel
				? originalHaramel.StartedAtMillis - runtime.Epoch.ToUnixTimeMilliseconds() : runtime.NowMillis;
			void EnforceCourseGameClock()
			{
				if (options.Course != null && runtime.NowMillis - journeyStart >= TimeSpan.FromHours(1).TotalMilliseconds)
					throw new TimeoutException($"Mau course exceeded its 3,600-second game-clock cap ({runtime.NowMillis - journeyStart} ms).");
			}
			long lastProgressObservation = -1000;
			session.AfterSynchronize = () =>
			{
				EnforceCourseGameClock();
				if (!session.Api.World.LoginStateObserved || runtime.NowMillis - lastProgressObservation < 1000) return;
				lastProgressObservation = runtime.NowMillis;
				checkpoint = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
					session.ConnectionGeneration, contract, session.CurrentPosition, coinGearProgress: coinGearProgress, haramelProgress: haramelProgress, earlyAscension: options.AscensionBridge);
				progress.Observe(checkpoint, TimeSpan.FromMilliseconds(runtime.NowMillis - journeyStart));
				if (haramelProgress != null)
				{
					haramelProgress = haramelProgress with { StallBudget = progress.State, Revives = combat.ReviveCount, LastObservedAtMillis = HaramelNow() };
					haramelProgress.Write(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "haramel-progress.json"));
				}
			};
			const float DeathSpotAvoidance = 12f;
			int reconnects = 0;
			string? relogAt = options.RelogAt;
			string? stopAt = options.StopAt;
			if (string.IsNullOrWhiteSpace(relogAt)) relogAt = null;
			if (string.IsNullOrWhiteSpace(stopAt)) stopAt = null;
			bool relogInjected = false;
			bool stopInjected = false;
			int[]? ParseBoundary(string? value) => value == null ? null : value.Split(':').Select(int.Parse).ToArray();
			// NA-17: several relog boundaries may be listed ("2008:3:52,2009:3:2"); each is injected once, in order met.
			int[][] relogBoundaries = relogAt == null ? [] : relogAt.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)
				.Select(value => ParseBoundary(value.Trim())!).ToArray();
			var relogsInjected = new HashSet<int>();
			int[]? stopBoundary = ParseBoundary(stopAt);
			if (relogBoundaries.Any(boundary => boundary.Length != 3) || stopBoundary is { Length: not 3 })
				throw new ArgumentException("NI08_RELOG_AT and NI08_STOP_AT must be questId:status:packedVars.");
			session.BeforeSend = () =>
			{
				EnforceCourseGameClock();
				if (!stopInjected && stopBoundary is { } stop && QuestStatus(stop[0]) == stop[1] && QuestVar(stop[0]) == stop[2])
				{
					stopInjected = true;
					throw new NaturalJourneyCheckpointStopException();
				}
				for (int index = 0; index < relogBoundaries.Length; index++)
				{
					int[] at = relogBoundaries[index];
					if (relogsInjected.Contains(index) || QuestStatus(at[0]) != at[1] || QuestVar(at[0]) != at[2]) continue;
					relogsInjected.Add(index);
					relogInjected = relogsInjected.Count == relogBoundaries.Length;
					throw new EndOfStreamException($"NI-08 injected connection interruption at Q{at[0]} status {at[1]} vars {at[2]}.");
				}
				// Stop before the next ordinary action once level 9 is observed. Finish any current
				// combat/cast/movie first; the outer loop reconstructs the interrupted quest from its journal.
				if (!earlyAscensionUnderway && options.StopAfterQuest == null && EarlyAscensionNeeded() &&
					session.Api.World.MapId == contract.MapId && !session.Api.World.IsDead && !combat.InCombat &&
					session.Api.Timing.BlockingActivities.Count == 0 && Engaged() is var engaged &&
					engaged.Attackers.Length == 0 && engaged.Pursuers.Length == 0)
					throw new NaturalEarlyAscensionRequiredException();
			};
			if (altgardLegId != null)
			{
				await RunAltgardLeg1Async();
				session.PublishDashboard("completed", force: true);
				await session.QuitAsync(token);
				runtime.AssertClean();
				return;
			}
			if (options.ClericEncounter)
			{
				await RunClericEncounterAsync();
				session.PublishDashboard("completed", force: true);
				await session.QuitAsync(token);
				runtime.AssertClean();
				return;
			}
			if (options.Course is NaturalMauCourse course)
			{
				navigator.AvoidHostileAggro = true;
				NaturalMauEncounter? encounter = options.Encounter;
				int questId = course == NaturalMauCourse.GeneratorToRae ? 2007 : 2129;
				int expectedVar = encounter == NaturalMauEncounter.BlockedGeneratorRejoin ? 8 :
					course == NaturalMauCourse.GeneratorToRae ? 6 : 1;
				Require.True(AtQuestStep(questId, expectedVar),
					$"Mau course requires Q{questId} START/{expectedVar} observed from client packets.");
				Require.Equal((ushort)9, session.Api.World.Level);
				session.TraceDiagnostic("phase0-course-start", new Dictionary<string, object?>
				{
					["course"] = course.ToString(), ["encounter"] = encounter?.ToString(),
					["seed"] = runtime.Seed,
					["checkpoint"] = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
						session.ConnectionGeneration, contract, session.CurrentPosition, coinGearProgress: coinGearProgress, haramelProgress: haramelProgress, earlyAscension: options.AscensionBridge),
				});
				if (encounter != null)
				{
					session.BeginStep($"phase1-{encounter}", "shared-mau-navigation-and-combat");
					session.TraceDiagnostic("phase1-encounter-start", new Dictionary<string, object?>
					{
						["encounter"] = encounter.ToString(), ["position"] = session.CurrentPosition,
					});
					if (encounter == NaturalMauEncounter.BlockedGeneratorRejoin)
					{
						await CompleteWheresRaeThisTimeAsync();
						Require.Equal(8, QuestVar(2007));
					}
					else
					{
						int templateId = encounter switch
						{
							NaturalMauEncounter.IsolatedStalker => 210750,
							NaturalMauEncounter.TwoAttackerPull => 211284,
							NaturalMauEncounter.MovingPatrol => 210407,
							NaturalMauEncounter.HatataAlone or NaturalMauEncounter.HatataWithAdd => 210409,
							_ => throw new ArgumentOutOfRangeException(nameof(encounter)),
						};
						await KillShippedSpawnAsync(templateId);
						if (templateId == 210409)
							Require.True(QuestVar(2129) >= 65, "Hatata objective update was not client-observed.");
					}
				}
				else if (course == NaturalMauCourse.GeneratorToRae)
					await CompleteWheresRaeThisTimeAsync();
				else
				{
					session.BeginStep("phase0-rae-to-hatata", "kill-hatata-with-current-pull-and-combat-policy");
					await KillShippedSpawnAsync(210409);
					Require.True(QuestVar(2129) >= 65, "Hatata objective update was not client-observed.");
				}
				await DefendAgainstEngagedAsync("phase0-terminal-disengagement");
				await session.AdvanceAsync(TimeSpan.FromSeconds(2), token);
				await session.SynchronizeAsync(token);
				var (remainingAttackers, remainingPursuers) = Engaged();
				Require.True(!session.Api.World.IsDead && remainingAttackers.Length == 0 && remainingPursuers.Length == 0,
					$"Mau course ended with attackers [{string.Join(',', remainingAttackers)}] and pursuers [{string.Join(',', remainingPursuers)}].");
				string outcome = Path.Combine(Path.GetDirectoryName(combatTracePath)!, "course-completion.json");
				await File.WriteAllTextAsync(outcome, System.Text.Json.JsonSerializer.Serialize(new
				{
					Course = course.ToString(), Encounter = encounter?.ToString(), runtime.Seed,
					EndGameMillis = runtime.NowMillis,
					Position = session.CurrentPosition, Quest = session.Api.World.Quests[questId],
					Deaths = combat.ReviveCount, Disengaged = true,
					Checkpoint = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
						session.ConnectionGeneration, contract, session.CurrentPosition, coinGearProgress: coinGearProgress, haramelProgress: haramelProgress, earlyAscension: options.AscensionBridge),
				}), token);
				session.TraceDiagnostic("phase0-course-complete", new Dictionary<string, object?>
				{
					["course"] = course.ToString(), ["encounter"] = encounter?.ToString(),
					["deaths"] = combat.ReviveCount,
					["position"] = session.CurrentPosition, ["disengaged"] = true,
				});
				await session.QuitAsync(token);
				runtime.AssertClean();
				return;
			}
			for (int sequence = 1; sequence <= 100; sequence++)
			{
				try
				{
					if (options.CapitalStage == "first" || EarlyAscensionNeeded())
					{
						await RunEarlyAscensionAsync();
						continue;
					}
					if (options.AscensionBridge && !IshalgenPending() && AscensionBridgeStarted())
					{
						if (session.Api.World.CompletedQuestIds.Contains(2009) && session.Api.World.MapId == contract.MapId)
						{
							await CompleteJourneyAsync();
							return;
						}
						// NA-17: a login (fresh, or after an interruption) past the Munin stop resumes the bridge.
						await RunAscensionBridgeAsync();
						if (continuousAltgard) await RunAllAltgardLegsAsync();
						session.PublishDashboard("completed", force: true);
						await session.QuitAsync(token);
						runtime.AssertClean();
						return;
					}
					if (session.Api.World.IsDead) await RestSafelyAsync(token);
					checkpoint = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
						session.ConnectionGeneration, contract, session.CurrentPosition, sequence, earlyAscension: options.AscensionBridge);
					progress.Observe(checkpoint, TimeSpan.FromMilliseconds(runtime.NowMillis - journeyStart));
					decision = checkpoint.Next;
					if (options.OptimizeHubs && NaturalIshalgenHubPolicy.MayReorder(decision) && session.Api.World.MapId == contract.MapId &&
						!session.Api.World.IsDead)
					{
						await AcceptAllAtCurrentHubAsync();
						checkpoint = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
							session.ConnectionGeneration, contract, session.CurrentPosition, sequence, earlyAscension: options.AscensionBridge);
						decision = checkpoint.Next;
						int[] safeGroup = NaturalIshalgenHubPolicy.CurrentSafeGroup(
							session.Api.World.CompletedQuestIds);
						int[] available = NaturalIshalgenHubPolicy.Order(
							decision.Quests.Where(quest => safeGroup.Contains(quest.QuestId) &&
								NaturalIshalgenHubPolicy.CanWorkNow(
								quest.QuestId, session.Api.World.CompletedQuestIds) &&
								(quest.Verdict == "active" ||
								quest.Verdict == "candidate" &&
								(templatePlans.ContainsKey(quest.QuestId) ||
								NaturalIshalgenHubPolicy.CustomNpcStarters.ContainsKey(quest.QuestId) ||
								quest.QuestId == 2114)))
								.Select(quest => quest.QuestId), contract,
							id => runtime.Data.Quests.GetQuestById(id)?.GetCategory() ?? QuestCategory.QUEST,
							NaturalIshalgenHubPolicy.At(session.CurrentPosition), workedTemplates);
						if (safeGroup.Length > 0 && available.Length == 0)
							throw new InvalidDataException($"Safe hub group [{string.Join(',', safeGroup)}] has no available quest at level {session.Api.World.Level}.");
						if (available.Length > 0)
							decision = decision with
							{
								SelectedQuestId = available[0],
								SelectedAction = QuestStatus(available[0]) >= 3 ? "continue-quest" : "find-quest-starter",
								Reason = $"Safe hub group: lowest level, blue before gold, and unfinished local work; selected Q{available[0]}.",
							};
					}
					// Q2005 originally switched this on for the rest of the run. Reconstruct the policy
					// from the journal: a cold login after Q2005 must not walk straight through the Mau camp.
					navigator.AvoidHostileAggro = decision.SelectedQuestId is 2005 or 2006 or 2007 ||
						session.Api.World.CompletedQuestIds.Contains(2005);
					dashboard.PublishDecision("b01", decision);
					session.TraceDiagnostic("natural-resume-observation", new Dictionary<string, object?> { ["checkpoint"] = checkpoint });
					if (decision.SelectedQuestId is not int questId)
					{
						await CompleteJourneyAsync();
						return;
					}
					if (options.OptimizeHubs)
					{
						BotPosition? journeyDestination = null;
						if (templatePlans.TryGetValue(questId, out QuestRunPlan? selectedPlan))
						{
							QuestRunNpc? endpoint = workedTemplates.Contains(questId)
								? selectedPlan.EndNpcs.FirstOrDefault()
								: selectedPlan.StartNpcs.FirstOrDefault();
							QuestRunPosition? place = endpoint?.Positions.FirstOrDefault(position =>
								position.MapId == contract.MapId);
							if (place != null)
								journeyDestination = new(place.X, place.Y, place.Z, checked((byte)place.Heading));
						}
						else if (QuestStatus(questId) < 3)
							journeyDestination = NaturalIshalgenHubPolicy.ForQuest(questId)?.Center;
						if (journeyDestination is BotPosition destination)
						{
							await UseFasterTravelAsync(destination);
							await AcceptAllAtCurrentHubAsync();
						}
					}
					switch (questId)
					{
						case 2101: await CompleteQ2101Async(); break;
						case 2102: await CompleteQ2102Async(); break;
						case 2103: await CompleteQ2103Async(); break;
						case 2104: await CompleteQ2104Async(); break;
						case 2100: await FinishCaptainOrderAsync(); break;
						case 2001: await CompleteThinkingAheadAsync(); break;
						case 2002: await AdvanceWheresRaeAsync(); break;
						case 2003: await CompleteTreasureOfTheDeceasedAsync(); break;
						case 2004: await CompleteCharmedCubeAsync(); break;
						case 2005: await CompleteTeachingALessonAsync(); break;
						case 2006: await CompleteHitThemWhereItHurtsAsync(); break;
						case 2007: await CompleteWheresRaeThisTimeAsync(); break;
						case 2105: await CompleteSparkleAndShineAsync(); break;
						case 2106: await CompleteVanarsFlatteryAsync(); break;
						case 2132: await CompleteNewSkillAsync(); break;
						case 2114: await CompleteInsectProblemAsync(); break;
						case 2123: await CompleteImprisonedGourmetAsync(); break;
						case 2125: await CompleteRobberyPlotAsync(); break;
						case 2135: await CompleteForLoveOfNegiAsync(); break;
						default:
							if (!templatePlans.TryGetValue(questId, out var plan))
								throw new InvalidDataException($"Natural Ishalgen Q{questId} has no journey actions.");
						if (!options.OptimizeHubs)
							await CompleteTemplateQuestAsync(plan);
						else if (workedTemplates.Contains(questId))
						{
							await CompleteTemplateQuestAsync(plan, TemplatePhase.Claim);
							workedTemplates.Remove(questId);
						}
						else
						{
							await CompleteTemplateQuestAsync(plan, TemplatePhase.Work);
							if (QuestStatus(questId) != 5) workedTemplates.Add(questId);
						}
						break;
					}
					if (questId == 2004 && stopAfterQ2004 || questId == 2005 && stopAfterQ2005 ||
						questId == 2006 && stopAfterQ2006 || questId == 2007 && stopAfterQ2007)
					{
						Require.Contains(questId, session.Api.World.CompletedQuestIds);
						await session.QuitAsync(token);
						runtime.AssertClean();
						return;
					}
				}
				catch (NaturalEarlyAscensionRequiredException)
				{
					// This is a route interruption, not a failure or a reset of the quest/revival budgets.
					continue;
				}
				catch (NaturalCapitalCheckpointStopException stop)
				{
					await CompleteCapitalCheckpointAsync(stop.Stage);
					await session.QuitAsync(token);
					runtime.AssertClean();
					return;
				}
				catch (NaturalJourneyCheckpointStopException)
				{
					if (relogAt != null) Require.True(relogInjected, "Requested NI-08 interruption was never exercised before checkpoint.");
					session.BeforeSend = null;
					await session.QuitAsync(token);
					checkpoint = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
						session.ConnectionGeneration, contract, session.CurrentPosition, coinGearProgress: coinGearProgress, haramelProgress: haramelProgress, earlyAscension: options.AscensionBridge);
					string receipt = Path.Combine(Path.GetDirectoryName(combatTracePath)!, "resume-receipt.json");
					await File.WriteAllTextAsync(receipt, System.Text.Json.JsonSerializer.Serialize(new
					{
						checkpoint.CharacterId,
						ElapsedMillis = runtime.NowMillis,
						Checkpoint = checkpoint,
					}), token);
					session.PublishDashboard("checkpoint-saved", force: true);
					runtime.AssertClean();
					return; // Deliberately partial: the restart runner must verify phase two completes.
				}
				catch (Exception failure) when (NaturalJourneyReconnect.IsConnectionLoss(failure) && reconnects < 3 && !token.IsCancellationRequested)
				{
					WriteFailure("disconnect", failure);
					reconnects++;
					await session.CloseSelectionAsync(token);
					await session.AdvanceOfflineAsync(TimeSpan.FromSeconds(11), token);
					await session.WaitForReentryAsync(token);
					session.ClearPacketHistory();
					await NaturalJourneyReconnect.RunAsync(async reconnectToken =>
					{
						await session.ReloginExistingCharacterAsync(reconnectToken);
						await session.EnterWorldAsync(reconnectToken);
						await session.SynchronizeAsync(reconnectToken);
					}, session.AdvanceOfflineAsync, (_, reconnectFailure) => WriteFailure("reconnect", reconnectFailure), token);
					navigator.UnavailableObjects.Clear();
					quietNeighbours.Clear();
					fightRouteOpen = false;
					fightRouteOpenAt = null;
					emptySpawnWaits = 0;
					continue;
				}

			}

			throw new TimeoutException("Natural journey exhausted its 100 decision attempts.");

			async Task CompleteQ2101Async()
			{
				session.BeginStep("ni07-q2101-start", "walk-to-asak-and-accept");
				if (QuestStatus(2101) < 3)
				{
					int asak = await ApproachShippedSpawnAsync(203500);
					await session.StartQuestAsync(asak, 2101, token);
				}

				session.BeginStep("ni07-q2101-finish", "walk-to-vandar-and-report");
				NaturalNavigationResult vandarApproach = await NaturalIshalgenNavigator.ApproachNpcAsync(
					contract.MapId, 203504, new BotPosition(526.99f, 2775.67f, 295.751f, 0), navigator, token);
				Require.True(vandarApproach.Arrived, vandarApproach.Reason);
				int vandar = Require.IsType<int>(vandarApproach.TargetObjectId);
				await session.FinishQuestAsync(vandar, 2101, token);
				Require.Equal(5, session.Api.World.Quests[2101].Status);
			}

			async Task CompleteQ2102Async()
			{
				if (QuestStatus(2102) < 3)
					await session.StartQuestAsync(await ApproachShippedSpawnAsync(203504), 2102, token);
				for (int kill = 0; QuestStatus(2102) == 3 && QuestVar(2102) < 4; kill++)
				{
					session.BeginStep($"ni07-q2102-kill-{kill + 1}", "approach-and-fight-sprigg-worker");
					BotPosition anchor = graph.GetMap(contract.MapId)!.Waypoints
						.Where(waypoint => waypoint.TemplateId == 210363)
						.OrderBy(waypoint => Distance(session.CurrentPosition, waypoint.Position))
						.First().Position;
					NaturalNavigationResult approach = await NaturalIshalgenNavigator.ApproachNpcAsync(
						contract.MapId, 210363, anchor, navigator, token);
					Require.True(approach.Arrived, approach.Reason);
					int target = Require.IsType<int>(approach.TargetObjectId);
					await combat.KillAsync(target, token);
					navigator.UnavailableObjects.Add(target);
					await RestSafelyAsync(token);
				}
				Require.Equal(4, session.Api.World.Quests[2102].StepAndFlags);
				session.BeginStep("ni07-q2102-finish", "return-to-vandar-and-claim");
				var vandarApproach = await NaturalIshalgenNavigator.ApproachNpcAsync(contract.MapId, 203504,
					new BotPosition(526.99f, 2775.67f, 295.751f, 0), navigator, token);
				Require.True(vandarApproach.Arrived, vandarApproach.Reason);
				await session.FinishQuestAsync(Require.IsType<int>(vandarApproach.TargetObjectId), 2102, token);
				Require.Equal(5, session.Api.World.Quests[2102].Status);
			}

			async Task CompleteQ2103Async()
			{
				session.BeginStep("ni07-q2103-start", "accept-sprigg-report-at-vandar");
				if (QuestStatus(2103) < 3)
					await session.StartQuestAsync(await ApproachShippedSpawnAsync(203504), 2103, token);
				session.BeginStep("ni07-q2103-finish", "walk-to-guheitun-and-report");
				NaturalNavigationResult guheitunApproach = await NaturalIshalgenNavigator.ApproachNpcAsync(contract.MapId,
					203501, new BotPosition(223.975f, 2679.86f, 295.25f, 0), navigator, token);
				Require.True(guheitunApproach.Arrived, guheitunApproach.Reason);
				await session.FinishQuestAsync(Require.IsType<int>(guheitunApproach.TargetObjectId), 2103, token);
				Require.Equal(5, session.Api.World.Quests[2103].Status);
			}

			async Task CompleteQ2104Async()
			{
				session.BeginStep("ni07-q2104-start", "walk-to-vanar-and-accept");
				if (QuestStatus(2104) < 3)
					await session.StartQuestAsync(await ApproachShippedSpawnAsync(203502), 2104, token);
				for (int basket = 0; QuestStatus(2104) == 3 && ItemCount(session.Api.World, 182203104) < 3; basket++)
				{
					session.BeginStep($"ni07-q2104-basket-{basket + 1}", "walk-and-loot-shipped-basket");
					int objectId = await UseAndLootQuestObjectAsync(700124, 182203104);
					navigator.UnavailableObjects.Add(objectId);
				}
				if (QuestStatus(2104) == 3) Require.Equal(3, ItemCount(session.Api.World, 182203104));
				session.BeginStep("ni07-q2104-finish", "return-to-vanar-and-turn-in");
				int vanar = await ApproachAsync(203502, new BotPosition(220.15f, 2678.81f, 295.25f, 0));
				await FinishNaturalItemQuestAsync(vanar, 2104);
				Require.Equal(5, session.Api.World.Quests[2104].Status);
				Require.Equal(0, ItemCount(session.Api.World, 182203104));
			}

			async Task CompleteJourneyAsync()
			{
				if (stopAt != null) Require.True(stopInjected, "Requested NI-08 checkpoint was never exercised.");
				// With the bridge on, its endpoint checks that every requested interruption happened (NA-17).
				if (relogAt != null && !options.AscensionBridge) Require.True(relogInjected, "Requested NI-08 interruption was never exercised.");
				Require.Equal(41, contract.Quests.Length);
				Require.All(contract.Quests, quest => Require.Contains(quest.Id, session.Api.World.CompletedQuestIds));
				if (options.AscensionBridge && session.Api.World.CompletedQuestIds.Contains(2009))
				{
					Require.True(session.Api.World.Level >= 10 && combat.IsCleric, "Ishalgen must finish as the ceremony-proven Cleric.");
					Require.Contains(2008, session.Api.World.CompletedQuestIds);
					session.BeginStep("ni07-ascended-ishalgen-complete", "finish-all-ishalgen-quests-after-the-early-ceremony");
				}
				else
				{
					Require.Equal((ushort)9, session.Api.World.Level);
					Require.DoesNotContain(contract.AscensionQuestId, session.Api.World.CompletedQuestIds);
					Require.Equal(3, session.Api.World.Quests[contract.AscensionQuestId].Status);
					Require.Equal(0, session.Api.World.Quests[contract.AscensionQuestId].StepAndFlags);
					session.BeginStep("ni07-munin-stop", "stand-at-munin-without-ascension-dialogue");
					await ApproachShippedSpawnAsync(contract.AscensionNpcId);
				}
				decision = NaturalIshalgenDecisionEngine.Decide(contract, ObserveNaturalJourney(session), 57, earlyAscension: options.AscensionBridge);
				Require.Equal("journey-complete", decision.SelectedAction);
				Require.Equal("complete", decision.Outcome);
				await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "completion.json"),
					System.Text.Json.JsonSerializer.Serialize(NaturalJourneyCheckpoint.Capture(session.Api.World,
						session.CharacterId, session.ConnectionGeneration, contract, session.CurrentPosition, coinGearProgress: coinGearProgress, haramelProgress: haramelProgress, earlyAscension: options.AscensionBridge)), token);
				// NA-03: a saved Munin snapshot restores with this clock so game time keeps moving forward.
				await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "completion-clock.json"),
					System.Text.Json.JsonSerializer.Serialize(new { session.CharacterId, ElapsedMillis = runtime.NowMillis }), token);
				// NA-11/12: with the bridge enabled, the Ascension bridge (docs/natural-ascension-altgard.md) starts where
				// Ishalgen ends instead of quitting here.
				if (options.AscensionBridge)
				{
					if (session.Api.World.CompletedQuestIds.Contains(2009) && session.Api.World.MapId == contract.MapId)
						await TakeCeremonyTeleporterAsync(toIshalgen: false);
					await RunAscensionBridgeAsync();
				}
				if (continuousAltgard) await RunAllAltgardLegsAsync();
				session.PublishDashboard("completed", force: true);
				await session.QuitAsync(token);
				runtime.AssertClean();
			}

			// NA-12: the Ascension bridge runner. The NA-11 engine picks each move from the client's view; contract "talk"
			// steps are played by one generic handler; moves not built yet stop the run with a precise receipt.
			async Task RunAscensionBridgeAsync(bool ceremonyOnly = false)
			{
				NaturalAscensionContract bridge = NaturalAscensionContract.LoadDefault();
				await TopUpHelpItemsAsync("run-start");
				await combat.BuffOurselfAsync(NaturalHelpTrigger.AfterRelog, token); // NA-19: a fresh login or a resume
				string? previous = null;
				int repeats = 0;
				for (int sequence = 1; sequence <= 80; sequence++)
				{
					await session.SynchronizeAsync(token);
					if (!ceremonyOnly && CapitalPending() && session.Api.World.CompletedQuestIds.Contains(2009) &&
						session.Api.World.MapId == capitalContract.MapId && AtQuestStep(capitalContract.DispatchQuestId, 0))
						await RunCapitalPassAsync();
					if (!ceremonyOnly && laterCapital != null && !IshalgenPending() && session.Api.World.Level >= 13 &&
						session.Api.World.MapId == capitalContract.MapId && AtQuestStep(capitalContract.DispatchQuestId, 0))
						await PrepareLaterCapitalBookAsync();
					if (session.Api.World.Level != helpCheckedAtLevel) await TopUpHelpItemsAsync("level-up");
					NaturalAscensionDecision next = NaturalAscensionDecisionEngine.Decide(bridge,
						NaturalAscensionObservation.Observe(session.Api.World, bridgeShopVisited), sequence, ceremonyOnly);
					session.TraceDiagnostic("ascension-bridge-decision", new Dictionary<string, object?>
					{
						["sequence"] = sequence, ["action"] = next.Action, ["step"] = next.StepKey, ["quest"] = next.QuestId,
						["outcome"] = next.Outcome, ["reason"] = next.Reason, ["map"] = session.Api.World.MapId,
						["checks"] = next.Checks.Select(check => $"{check.Rule}:{check.Verdict}").ToArray(),
					});
					session.PublishDashboard();
					string signature = $"{next.Action}/{next.StepKey}/{next.Reason}";
					repeats = signature == previous ? repeats + 1 : 0;
					previous = signature;
					Require.True(repeats < 3, $"The Ascension bridge made no progress on {signature}: {next.Reason}");
					if (next.Outcome != "planned")
					{
						await WriteBridgeStopAsync(next);
						if (next.Outcome == "complete" && !ceremonyOnly) await CompleteAscensionLegAsync(bridge);
						return;
					}
					NaturalAscensionStep? step = next.StepKey is string key ? bridge.Steps.Single(s => s.Key == key) : null;
					if (next.Action == "talk" && step != null && ImplementedBridgeSteps.Contains(step.Key))
						await PlayBridgeTalkAsync(step);
					else if (next.Action == "fight-trial")
						await FightAscensionTrialAsync(bridge);
					else if (next.Action == "wait-flight")
						await session.AdvanceAsync(TimeSpan.FromSeconds(20), token);
					else if (next.Action == "teleport")
						await TakeBridgeTeleporterAsync(bridge);
					else if (next.Action == "bind")
						await BindInAltgardAsync(bridge);
					else if (next.Action == "shop")
						await ShopInAltgardAsync(bridge);
					else
					{
						await WriteBridgeStopAsync(next with { Outcome = "awaiting-capability", Reason = "Not built yet: " + next.Reason });
						return;
					}
				}
				throw new InvalidDataException("The Ascension bridge exceeded 80 decisions.");
			}

			long ObservedExperience() => session.Api.World.CurrentExperience +
				runtime.Data.PlayerExperienceTable.GetStartExpForLevel(session.Api.World.Level);

			async Task<int> ApproachCapitalNpcAsync(int npcId)
			{
				int map = session.Api.World.MapId!.Value;
				BotPosition anchor = runtime.Data.SpawnsDh.GetSpawnsByWorldId(map).Where(group => group.GetNpcId() == npcId)
					.SelectMany(group => group.GetSpawnTemplates()).Select(spot => new BotPosition(spot.GetX(), spot.GetY(), spot.GetZ(), spot.GetHeading()))
					.OrderBy(at => Distance(session.CurrentPosition, at)).First();
				BotNavigationGeometry mapGeometry = runtime.CreateGeometry();
				NaturalJourneyNavigator here = mapNavigators.Enter(NaturalMapKey.Observe(session.Api.World), newEntry: false);
				combat.EnterMap(here, mapGeometry, map);
				await combat.BuffOurselfAsync(NaturalHelpTrigger.TravelLeg, token, Distance(session.CurrentPosition, anchor));
				if (Distance(session.CurrentPosition, anchor) > 5 &&
					mapGeometry.FindInteractionPath(map, session.CurrentPosition, anchor).Count == 0)
					await capitalTravel.ConnectColiseumAsync(mapGeometry, anchor, token);
				NaturalNavigationResult approach = await NaturalIshalgenNavigator.ApproachNpcAsync(map, npcId, anchor, here, token);
				if (!approach.Arrived && laterCapital != null &&
					approach.Reason == "Reached the static area anchor but no NPC was observed.")
				{
					// Lusena can be away from her spawn anchor. Search the shipped patrol hints in reverse
					// to meet the walker, then let ordinary client observations identify and approach her.
					var patrols = runtime.Data.SpawnsDh.GetSpawnsByWorldId(map).Where(g => g.GetNpcId() == npcId)
						.SelectMany(g => g.GetSpawnTemplates()).Select(s => s.GetWalkerId()).OfType<string>().Distinct();
					foreach (string patrol in patrols)
					{
						var route = runtime.Data.WalkerDataDh.GetWalkerTemplate(patrol);
						if (route == null) continue;
						foreach (var point in route.GetRouteSteps().AsEnumerable().Reverse())
						{
							BotPosition hint = new(point.GetX(), point.GetY(), point.GetZ(), 0);
							session.TraceDiagnostic("later-capital-walker-search", new Dictionary<string, object?>
							{ ["npc"] = npcId, ["route"] = patrol, ["hint"] = hint });
							approach = await NaturalIshalgenNavigator.ApproachNpcAsync(map, npcId, hint, here, token);
							if (approach.Arrived) break;
						}
						if (approach.Arrived) break;
					}
				}
				if (!approach.Arrived && laterCapital != null)
				{
					// The generic navigator uses a three-metre interaction radius. City NPCs
					// can have reachable ground farther out but still inside their shipped talk range.
					BotKnownObject? seen = session.Api.World.Objects.Values.FirstOrDefault(o => o.TemplateId == npcId && !o.IsCorpse);
					BotPosition target = seen?.SettledPosition ?? anchor;
					float range = Math.Min(5, runtime.Data.NpcDataDh.GetNpcTemplate(npcId)!.GetTalkDistance());
					IReadOnlyList<BotPosition> route = NaturalCapitalTravel.FindGroundApproachPath(mapGeometry, map, session.CurrentPosition, target, range);
					if (route.Count > 0 && here.IsSegmentSafe(route, seen?.ObjectId))
					{
						await here.MoveAsync(route, token);
						await here.SynchronizeAsync(token);
						seen = session.Api.World.Objects.Values.Where(o => o.TemplateId == npcId && !o.IsCorpse)
							.OrderBy(o => Distance(session.CurrentPosition, o.SettledPosition)).FirstOrDefault();
						if (seen != null && Distance(session.CurrentPosition, seen.SettledPosition) <= range)
						{
							session.TraceDiagnostic("capital-talk-range-approach", new Dictionary<string, object?>
							{ ["npc"] = npcId, ["range"] = range, ["checkedPoints"] = route.Count, ["position"] = session.CurrentPosition });
							return seen.ObjectId;
						}
					}
				}
				Require.True(approach.Arrived, $"Capital NPC {npcId}: {approach.Reason}");
				return Require.IsType<int>(approach.TargetObjectId);
			}

			async Task PlayLaterCapitalStepAsync(NaturalAltgardStep step)
			{
				session.BeginStep("rc-" + step.Key, "later-capital-dialog");
				int npc = session.Api.World.MapId == capitalContract.MapId ? await ApproachCapitalNpcAsync(step.NpcId)
					: await ApproachShippedSpawnAsync(step.NpcId, withinRange: step.TalkRange);
				for (int attempt = 0; ; attempt++)
				{
					try
					{
						long xp = ObservedExperience(), kinah = session.Api.World.Kinah;
						bool completedBefore = session.Api.World.CompletedQuestIds.Contains(step.QuestId);
						if (step.NpcId == 700212) await NaturalLaterCapitalSteps.ReadQuestBookAsync(session, step, npc, token);
						else session.TraceDiagnostic("later-capital-step", new Dictionary<string, object?>
							{ ["change"] = await NaturalAltgardQuestSteps.TalkAsync(session, step, npc, token) });
						if (!completedBefore && session.Api.World.CompletedQuestIds.Contains(step.QuestId))
							session.TraceDiagnostic("later-capital-payment", new Dictionary<string, object?>
							{ ["quest"] = step.QuestId, ["experience"] = ObservedExperience() - xp, ["kinah"] = session.Api.World.Kinah - kinah });
						return;
					}
					catch (NaturalDialogTooFarException) when (attempt < NaturalLaterCapitalSteps.DialogRetryLimit(step.NpcId))
					{
						// Deyla walks: the announced next waypoint is not her current position. After a server refusal,
						// intercept the last observed position instead of repeatedly arriving at that future waypoint.
						BotKnownObject seen = session.Api.World.Objects[npc];
						BotPosition destination = NaturalLaterCapitalSteps.DialogReapproachPosition(step.NpcId, seen);
						session.TraceDiagnostic("later-capital-dialog-reapproach", new Dictionary<string, object?>
						{ ["npc"] = step.NpcId, ["position"] = seen.Position, ["nextWaypoint"] = seen.MoveTarget, ["destination"] = destination });
						NaturalJourneyNavigator here = mapNavigators.Enter(NaturalMapKey.Observe(session.Api.World), newEntry: false);
						IReadOnlyList<BotPosition> route = await here.FindRouteAsync(session.CurrentPosition, destination, token);
						Require.True(route.Count > 0 || Distance(session.CurrentPosition, destination) <= 1,
							$"Capital NPC {step.NpcId}: no checked dialogue reapproach.");
						if (route.Count > 0) await here.MoveAsync(route, token);
						await here.SynchronizeAsync(token);
						await session.AdvanceAsync(TimeSpan.FromMilliseconds(250), token);
					}
				}
			}

			async Task PrepareLaterCapitalBookAsync()
			{
				if (laterCapital == null || session.Api.World.CompletedQuestIds.Contains(2919) || session.Api.World.Level < 13) return;
				NaturalIshalgenContract savedContract = contract;
				NaturalJourneyNavigator savedNavigator = navigator;
				BotNavigationGeometry savedGeometry = geometry;
				var savedFarApproach = farApproach;
				Action? savedRevive = combat.AfterBindRevive;
				void EnterBookMap()
				{
					NaturalMapKey key = NaturalMapKey.Observe(session.Api.World);
					var defend = navigator.DefendOnAttackAsync;
					navigator = mapNavigators.Enter(key, newEntry: false);
					navigator.DefendOnAttackAsync = defend;
					navigator.AvoidHostileAggro = true;
					geometry = runtime.CreateGeometry();
					contract = contract with { MapId = key.MapId };
					combat.EnterMap(navigator, geometry, key.MapId);
					navigator.AvoidSpots = combat.DeathSpots;
				}
				try
				{
					farApproach = null;
					combat.AfterBindRevive = EnterBookMap;
					EnterBookMap();
					if (!await NaturalLaterCapitalSteps.PrepareBookAsync(session, PlayLaterCapitalStepAsync)) return;
					if (NaturalAltgardQuestSteps.State(session.Api.World, 2919) is (3, 4))
					{
						bookCollectionUnderway = true;
						await new NaturalLaterCapitalBookTravel(session, runtime,
							id => session.Api.World.MapId == capitalContract.MapId ? ApproachCapitalNpcAsync(id) : ApproachShippedSpawnAsync(id),
							EnterBookMap, async (id, collection) =>
							{
								try { return await KillShippedSpawnAsync(id, collection); }
								catch (NaturalBookCollectionMapChangedException) { return 0; }
							}, async id => { await TryLootCorpseItemAsync(session, id, 182207011, token); })
							.CollectAmphaAndReturnAsync(token);
					}
				}
				finally
				{
					bookCollectionUnderway = false;
					contract = savedContract;
					navigator = savedNavigator;
					geometry = savedGeometry;
					farApproach = savedFarApproach;
					combat.AfterBindRevive = savedRevive;
					combat.EnterMap(navigator, geometry, contract.MapId);
				}
			}

			async Task RunCapitalPassAsync()
			{
				capitalBefore ??= NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
					session.ConnectionGeneration, contract, session.CurrentPosition, earlyAscension: true);
				while (++capitalSequences <= 80)
				{
					await session.SynchronizeAsync(token);
					NaturalCapitalDecision next = NaturalCapitalDecisionEngine.Decide(capitalContract,
						NaturalAscensionObservation.Observe(session.Api.World, false));
					session.BeginStep(next.Step?.Key ?? $"pc-{next.Action}", next.Action);
					session.TraceDiagnostic("capital-decision", new Dictionary<string, object?>
					{ ["sequence"] = capitalSequences, ["decision"] = next, ["map"] = session.Api.World.MapId });
					session.PublishDashboard();
					try
					{
						switch (next.Action)
						{
							case "complete": VerifyCapitalPass(); return;
							case "recover": await RestSafelyAsync(token); break;
							case "travel-city": await TakeCeremonyTeleporterAsync(toIshalgen: false); break;
							case "observe": await session.AdvanceAsync(TimeSpan.FromMilliseconds(100), token); break;
							case "read-book":
								await NaturalCapitalSteps.ReadBookAsync(session, runtime.Data.ItemDataDh.GetItemTemplate(182212217), token);
								break;
							case "portal":
								await NaturalCapitalSteps.PortalAsync(session, next.Portal!, await ApproachCapitalNpcAsync(next.Portal!.NpcId), token);
								break;
							case "talk":
								NaturalAltgardStep step = next.Step!;
								long? xp = null;
								long kinah = 0;
								try
								{
									for (int attempt = 1; ; attempt++)
									{
										try
										{
											int npc = await ApproachCapitalNpcAsync(step.NpcId);
											xp = ObservedExperience();
											kinah = session.Api.World.Kinah;
											string outcome = await NaturalAltgardQuestSteps.TalkAsync(session, step, npc, token);
											session.TraceDiagnostic("capital-talk", new Dictionary<string, object?> { ["outcome"] = outcome });
											break;
										}
										catch (NaturalDialogTooFarException) when (attempt < 3) { }
									}
								}
								finally
								{
									// Even an injected disconnect on closing the reward dialog keeps the observed payment.
									if (xp != null && session.Api.World.CompletedQuestIds.Contains(step.QuestId) && !capitalPayments.ContainsKey(step.QuestId))
									{
										var payment = new CapitalPayment(step.QuestId, ObservedExperience() - xp.Value, session.Api.World.Kinah - kinah);
										capitalPayments.Add(step.QuestId, payment);
										session.TraceDiagnostic("capital-payment", new Dictionary<string, object?> { ["payment"] = payment });
									}
								}
								await EquipUpgradesAsync(token);
								break;
							default: throw new InvalidDataException(next.Reason);
						}
						if (session.Api.World.Level != helpCheckedAtLevel) await TopUpHelpItemsAsync("level-up");
					}
					catch (Exception) when (session.Api.World.IsDead && !token.IsCancellationRequested)
					{
						await RestSafelyAsync(token); // Record/recover the outcome; keep the shared counters and clock.
					}
				}
				throw new TimeoutException("The capital pass exceeded 80 decisions across recovery attempts.");
			}

			void VerifyCapitalPass()
			{
				Require.NotNull(capitalBefore);
				BotWorldModel world = session.Api.World;
				Require.All(capitalContract.CompletedQuestIds, id => Require.Contains(id, world.CompletedQuestIds));
				Require.All(capitalBefore.CompletedQuestIds, id => Require.Contains(id, world.CompletedQuestIds));
				Require.All(capitalBefore.Quests.Where(q => contract.Quests.Any(entry => entry.Id == q.QuestId) && q.Status is 3 or 4), old =>
					Require.True(world.Quests.TryGetValue(old.QuestId, out BotQuestState? current) &&
						current.Status == old.Status && current.StepAndFlags == old.StepAndFlags, $"Capital pass changed retained Ishalgen Q{old.QuestId}."));
				Require.True(AtQuestStep(capitalContract.DispatchQuestId, 0), "The capital pass advanced Q2904.");
				NaturalJourneyItem staff = capitalBefore.Inventory.Single(item => item.ItemId == 101500498 && item.EquipmentSlot > 0);
				Require.True(world.Inventory.TryGetValue(staff.ObjectId, out BotInventoryItem? stillWorn) &&
					stillWorn.ItemId == staff.ItemId && stillWorn.Details.EquippedSlot is > 0, "The capital pass replaced the ceremony staff.");
				Require.True(capitalBefore.BindPoint == null || world.ObeliskBindPoint is { } bound &&
					bound.MapId == capitalBefore.BindPoint.MapId && Distance(bound.Position, capitalBefore.BindPoint.Position) < .1f,
					"The capital pass changed the bind point.");
				Require.All(world.Skills.Values.Where(skill => skill.SkillType is 1 or 3), skill =>
					Require.Contains(skill.SkillId, capitalBefore.Skills.Where(old => old.SkillType is 1 or 3).Select(old => old.SkillId)));
				NaturalCapitalQuest[] paid = capitalContract.Quests.Where(q => !capitalBefore.CompletedQuestIds.Contains(q.Id)).ToArray();
				Require.True(paid.Select(q => q.Id).Order().SequenceEqual(capitalPayments.Keys.Order()), "A capital reward payment was not observed.");
				Require.All(paid, quest =>
				{
					Require.Equal((long)quest.Experience, capitalPayments[quest.Id].Experience);
					Require.Equal(quest.Kinah, capitalPayments[quest.Id].Kinah);
				});
				Require.All(new[] { 122000870, 188508000, 169600066, 169600084, 169600085, 190000055 }, id => Require.True(ItemCount(world, id) > 0,
					$"Optional capital reward {id} was not retained."));
				Require.True(ItemCount(world, 164000074) >= 2, "The Lost Love running scrolls were not retained.");
				Require.True(ItemCount(world, 182207039) == 0 && ItemCount(world, 182212217) == 0,
					"The supply request or manual was not consumed at its proper hand-in.");
			}

			async Task CompleteCapitalCheckpointAsync(string stage)
			{
				if (relogAt != null) Require.True(relogInjected, "Requested capital interruption was not exercised.");
				if (stage == "first") { VerifyCapitalPass(); Require.Equal(contract.MapId, session.Api.World.MapId!.Value); }
				else Require.True(session.Api.World.Level == 10 && session.Api.World.MapId == capitalContract.MapId &&
					AtQuestStep(capitalContract.DispatchQuestId, 0), "The post-ceremony capital start is outside its contract.");
				NaturalJourneyCheckpoint before = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
					session.ConnectionGeneration, contract, session.CurrentPosition, earlyAscension: true);
				session.BeforeSend = null;
				await session.QuitAsync(token);
				await session.WaitForReentryAsync(token);
				await session.ReloginExistingCharacterAsync(token);
				await session.EnterWorldAsync(token);
				await session.SynchronizeAsync(token);
				NaturalJourneyCheckpoint after = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
					session.ConnectionGeneration, contract, session.CurrentPosition, earlyAscension: true);
				NaturalJourneyPersistence.Verify(before, after);
				await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "capital-stage-completion.json"),
					System.Text.Json.JsonSerializer.Serialize(new
					{
						Stage = stage, before, after, verified = true, session.CharacterId, ElapsedMillis = runtime.NowMillis,
						CapitalBefore = capitalBefore, Payments = capitalPayments.Values.OrderBy(p => p.QuestId).ToArray(),
						TransportFares = capitalFares, Deaths = combat.ReviveCount, Retreats = combat.CompletedRetreats,
						Reconnects = reconnects, StigmaSkills = after.Skills.Where(skill => skill.SkillType is 1 or 3).ToArray(),
						RegularSkills = after.Skills.Where(skill => skill.SkillType == 0).ToArray(),
					}), token);
				session.PublishDashboard("capital-checkpoint-complete", force: true);
			}

			async Task RunEarlyAscensionAsync()
			{
				earlyAscensionUnderway = true;
				try
				{
					NaturalJourneyCheckpoint before = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
						session.ConnectionGeneration, contract, session.CurrentPosition, earlyAscension: true);
					session.TraceDiagnostic("early-ascension-start", new Dictionary<string, object?>
					{ ["level"] = before.Level, ["completed"] = before.CompletedQuestIds, ["position"] = before.Position });
					if (!session.Api.World.CompletedQuestIds.Contains(2009)) await RunAscensionBridgeAsync(ceremonyOnly: true);
					Require.True(session.Api.World.Level >= 10 && combat.IsCleric &&
						session.Api.World.CompletedQuestIds.IsSupersetOf(new[] { 2008, 2009 }), "The early ceremony did not complete.");
					if (options.CapitalStage == "start")
					{
						Require.True(IshalgenPending() && session.Api.World.MapId == capitalContract.MapId &&
							capitalContract.CompletedQuestIds.All(id => !session.Api.World.CompletedQuestIds.Contains(id)),
							"The capital start must retain unfinished Ishalgen and precede every first-pass completion.");
						throw new NaturalCapitalCheckpointStopException("start");
					}
					long capitalStartExperience = ObservedExperience();
					await RunCapitalPassAsync();
					if (continuousAltgard && laterCapital != null)
						await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "capital-pass-completion.json"),
							System.Text.Json.JsonSerializer.Serialize(new { verified = true, session.CharacterId, before = capitalBefore,
								after = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId, session.ConnectionGeneration,
									contract, session.CurrentPosition, earlyAscension: true),
								StartingExperience = capitalStartExperience, Experience = ObservedExperience(),
								Payments = capitalPayments.Values.OrderBy(p => p.QuestId).ToArray(), TransportFares = capitalFares }), token);
					if (session.Api.World.MapId == 120010000) await TakeCeremonyTeleporterAsync(toIshalgen: true);
					Require.Equal(contract.MapId, session.Api.World.MapId!.Value);
					geometry = runtime.CreateGeometry();
					combat.EnterMap(navigator, geometry, contract.MapId);
					navigator.UnavailableObjects.Clear();
					quietNeighbours.Clear();
					fightRouteOpen = false;
					fightRouteOpenAt = null;
					await TopUpHelpItemsAsync("level-up");
					await combat.BuffOurselfAsync(NaturalHelpTrigger.AfterRelog, token);
					NaturalJourneyCheckpoint after = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
						session.ConnectionGeneration, contract, session.CurrentPosition, earlyAscension: true);
					Require.All(before.CompletedQuestIds, id => Require.Contains(id, after.CompletedQuestIds));
					Require.Equal(3, session.Api.World.Quests[2904].Status);
					Require.Equal(0, QuestVar(2904));
					await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "early-ascension-completion.json"),
						System.Text.Json.JsonSerializer.Serialize(new { before, after, verified = true, session.CharacterId,
							ElapsedMillis = runtime.NowMillis, Deaths = combat.ReviveCount }), token);
					if (options.CapitalStage == "first") throw new NaturalCapitalCheckpointStopException("first");
				}
				finally { earlyAscensionUnderway = false; }
			}

			async Task TakeCeremonyTeleporterAsync(bool toIshalgen)
			{
				NaturalAscensionContract bridge = NaturalAscensionContract.LoadDefault();
				int npc;
				if (toIshalgen)
				{
					session.BeginStep("early-ascension-return", "doman-teleporter-back-to-unfinished-ishalgen");
					NaturalAscensionStep doman = bridge.Steps.Single(step => step.Key == "q2904-v0-doman");
					NaturalJourneyNavigator here = mapNavigators.Enter(NaturalMapKey.Observe(session.Api.World), newEntry: false);
					npc = await ApproachBridgeNpcAsync(doman, new(doman.Position[0], doman.Position[1], doman.Position[2], 0), here);
				}
				else
				{
					session.BeginStep("early-ascension-dispatch", "ishalgen-teleporter-to-pandaemonium-after-all-ishalgen-quests");
					BotPosition anchor = graph.GetMap(contract.MapId)!.Waypoints.First(waypoint => waypoint.TemplateId == 203679).Position;
					await UseFasterTravelAsync(anchor, forceHubFlight: true);
					npc = await ApproachShippedSpawnAsync(203679);
				}
				long fareBefore = session.Api.World.Kinah;
				NaturalServiceOutcome result = await new NaturalServiceSteps(session).TeleportAsync(npc,
					session.Api.World.Objects[npc].Position, 5, toIshalgen ? 8 : 7, 100,
					toIshalgen ? contract.MapId : 120010000, token);
				Require.True(result.IsDone, result.Reason);
				if (capitalBefore != null) capitalFares += fareBefore - session.Api.World.Kinah;
				mapNavigators.Enter(NaturalMapKey.Observe(session.Api.World));
			}

			// NA-13: Q2008's trial in Ataxiar. Its NPCs deal 1 damage (AscensationNpcAI) and the instance has no exit, so the
			// Priest fights as if cornered: no swarm retreat, heals and potions still on. One kill per decision.
			async Task FightAscensionTrialAsync(NaturalAscensionContract bridge)
			{
				session.BeginStep("na-q2008-trial", "scripted-ascension-trial");
				var trial = bridge.Instance.Trial.Select(group => group.NpcId).ToHashSet();
				int? target = null;
				for (int wait = 0; wait < 30 && target == null; wait++)
				{
					target = session.Api.World.Objects.Values
						.Where(npc => npc.Kind == BotKnownObjectKind.Npc && npc.TemplateId is int id && trial.Contains(id))
						.OrderBy(npc => Distance(session.CurrentPosition, npc.Position)).Select(npc => (int?)npc.ObjectId).FirstOrDefault();
					if (target == null)
					{
						await session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
						await session.SynchronizeAsync(token);
					}
				}
				Require.True(target != null, "No trial opponent became visible in Ataxiar.");
				combat.ScriptedTrial = true;
				try
				{
					bool killed = await combat.TryKillAsync(target!.Value, token, retreatAnchor: session.CurrentPosition);
					session.TraceDiagnostic("ascension-trial-kill", new Dictionary<string, object?>
					{
						["target"] = target, ["killed"] = killed, ["hp"] = session.Api.World.CurrentHp,
						["quest"] = session.Api.World.Quests.TryGetValue(2008, out BotQuestState? q) ? q.StepAndFlags : null,
					});
				}
				finally { combat.ScriptedTrial = false; }
			}

			// NA-21: the stock check for the approved help items (OD-13): the Cleric only, and only when the runtime may
			// supply (SIM, or the isolated LIVE stack's director). Every supply is checked from the client's inventory,
			// traced, and listed in help-items.json beside the run's other evidence.
			async Task TopUpHelpItemsAsync(string trigger)
			{
				BotWorldModel world = session.Api.World;
				helpCheckedAtLevel = world.Level;
				if (runtime.SupplyHelpItemAsync is not { } supply || !combat.IsCleric || world.IsDead) return;
				var owned = world.Inventory.Values.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
				IReadOnlyList<NaturalHelpTopUp> plan = NaturalHelpItemSupply.Plan(world.Level, owned);
				foreach (NaturalHelpTopUp topUp in plan)
				{
					NaturalHelpItemSupply.RequireApproved(topUp.ItemId, topUp.Count);
					await supply(topUp.ItemId, topUp.Count, token);
					await session.SynchronizeAsync(token);
					long after = ItemCount(world, topUp.ItemId);
					Require.True(after >= topUp.Owned + topUp.Count, $"Help item {topUp.ItemId} did not arrive: {topUp.Owned} -> {after}.");
					helpSupplied.Add(new NaturalHelpSupplied(trigger, topUp.ItemId, topUp.Family, topUp.Count, topUp.Owned, after,
						world.Level, runtime.NowMillis));
					session.TraceDiagnostic("help-item-supplied", new Dictionary<string, object?>
					{
						["trigger"] = trigger, ["itemId"] = topUp.ItemId, ["family"] = topUp.Family, ["count"] = topUp.Count,
						["before"] = topUp.Owned, ["after"] = after, ["level"] = world.Level,
					});
				}
				if (plan.Count > 0)
					await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "help-items.json"),
						NaturalHelpItemSupply.ProfileJson(helpSupplied), token);
			}

			async Task RunAllAltgardLegsAsync()
			{
				int characterId = session.CharacterId;
				var stages = new List<object>();
				stages.Add(new { Stage = "ishalgen-ascension", CharacterId = characterId, ElapsedMillis = runtime.NowMillis,
					Deaths = combat.ReviveCount, Completed = session.Api.World.CompletedQuestIds.Count,
					Level = session.Api.World.Level, Experience = ObservedExperience(), Kinah = session.Api.World.Kinah });
				foreach (string id in NaturalAltgardContinuation.Order)
				{
					await session.SynchronizeAsync(token);
					Require.Equal(characterId, session.CharacterId);
					altgardLegId = id;
					session.IdentityAltgardLegId = id;
					NaturalAltgardContract shipped = NaturalAltgardContract.LoadLeg(id);
					altgardLeg = NaturalAltgardContinuation.BindIncoming(shipped, session.Api.World.CompletedQuestIds,
						session.Api.World.Inventory.Values.Select(i => new NaturalJourneyItem(i.ObjectId, i.ItemId, i.Count, i.EquipmentSlot)).ToArray());
					altgardPlans = NaturalAltgardContract.LoadPlans(id);
					collectionLimits = altgardPlans.Values.SelectMany(plan => plan.Steps
						.Where(step => step.Kind == "collect" && step.ItemId > 0 && step.Count > 0)
						.Select(step => (plan.Id, step.ItemId, step.Count)))
						.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.ToArray());
					coinGearProgress = altgardLeg.CoinGear == null ? null : NaturalCoinGearProgress.Empty;
					coinIncomingLoadout = altgardLeg.CoinGear == null ? [] : session.Api.World.Inventory.Values
						.Where(i => i.EquipmentSlot is > 0 and < 65535 && i.EquipmentSlot is not (16 or 4096 or 8192 or 16384))
						.Select(i => new NaturalJourneyItem(i.ObjectId, i.ItemId, i.Count, i.EquipmentSlot)).ToArray();
					destinyIncomingSkills = altgardLeg.Destiny is { } destiny
						? session.Api.World.Skills.Keys.Where(skill => skill != destiny.StigmaSkillId).ToArray() : [];
					haramelProgress = altgardLeg.Haramel is { } rules
						? NaturalHaramelProgress.Begin(characterId, HaramelNow(), session.Api.World, rules) with { Revives = combat.ReviveCount }
						: null;
					haramelApproach = null;
					haramelKind = null;
					long started = runtime.NowMillis;
					int deathsBefore = combat.ReviveCount;
					ushort startedLevel = session.Api.World.Level;
					long startedExperience = ObservedExperience(), startedKinah = session.Api.World.Kinah;
					session.TraceDiagnostic("continuous-leg-start", new Dictionary<string, object?>
					{ ["leg"] = id, ["characterId"] = characterId, ["elapsedMillis"] = started, ["deaths"] = deathsBefore });
					Console.WriteLine($"Continuous SIM: starting {id}, level {session.Api.World.Level}, {session.Api.World.CompletedQuestIds.Count} completions.");
					await RunAltgardLeg1Async();
					Require.All(altgardLeg.Start.CompletedQuestIds, quest => Require.Contains(quest, session.Api.World.CompletedQuestIds));
					stages.Add(new { Stage = id, CharacterId = characterId, StartedMillis = started, ElapsedMillis = runtime.NowMillis,
						Deaths = combat.ReviveCount, NewDeaths = combat.ReviveCount - deathsBefore, Completed = session.Api.World.CompletedQuestIds.Count,
						StartedLevel = startedLevel, StartedExperience = startedExperience, StartedKinah = startedKinah,
						Level = session.Api.World.Level, Experience = ObservedExperience(), Kinah = session.Api.World.Kinah });
					await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "continuous-progress.json"),
						System.Text.Json.JsonSerializer.Serialize(new { CharacterId = characterId, stages }), token);
				}
				Require.Equal(1, session.Api.World.CompletedQuestCounts.GetValueOrDefault(2217));
				if (laterCapital != null)
					Require.All(laterCapital.CompletedQuestIds, id => Require.Equal(1, session.Api.World.CompletedQuestCounts.GetValueOrDefault(id)));
				await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "continuous-completion.json"),
					System.Text.Json.JsonSerializer.Serialize(new { verified = true, CreatedCharacter = true, CharacterId = characterId,
						ElapsedMillis = runtime.NowMillis, Deaths = combat.ReviveCount, stages, LaterCapital = laterCapital != null,
						Experience = ObservedExperience(), ExperienceToNextLevel = runtime.Data.PlayerExperienceTable.GetStartExpForLevel(session.Api.World.Level + 1) - ObservedExperience(),
						ExperienceToLevel32 = Math.Max(0, runtime.Data.PlayerExperienceTable.GetStartExpForLevel(32) - ObservedExperience()),
						Endpoint = NaturalJourneyCheckpoint.Capture(session.Api.World, characterId, session.ConnectionGeneration,
							contract, session.CurrentPosition, coinGearProgress: coinGearProgress, haramelProgress: haramelProgress, earlyAscension: options.AscensionBridge) }), token);
			}

			// AF-08: Altgard Leg 1 (docs/natural-altgard-leveling.md), from the `altgard` snapshot to the fortress endpoint.
			// The journey is rebound to Altgard as for NA-23; the Leg 1 engine picks each move from the client's view. Template
			// quests run on the Ishalgen runner, scripted steps on NaturalAltgardQuestSteps, flight and the air kills on the
			// AF-04..AF-06 code. Every decision is traced; a move that makes no progress three times stops the run.
			async Task RunAltgardLeg1Async()
			{
				NaturalAltgardContract leg = altgardLeg ?? throw new InvalidOperationException("An Altgard leg needs its contract.");
				IReadOnlySet<int>? only = options.AltgardOnlyQuests?.ToHashSet();
				Require.True(combat.IsCleric, "An Altgard leg needs the Cleric.");
				Require.True(session.Api.World.MapId == leg.Hub.MapId || leg.Haramel?.MapId == session.Api.World.MapId || leg.Destiny?.AllowedMaps.Contains(session.Api.World.MapId ?? 0) == true,
					"The retained character is outside the approved leg maps.");
				NaturalJourneyNavigator here = mapNavigators.Enter(LegMapKey());
				here.DefendOnAttackAsync = navigator.DefendOnAttackAsync;
				here.AvoidHostileAggro = true;
				navigator = here;
				geometry = runtime.CreateGeometry();
				contract = contract with { MapId = session.Api.World.MapId!.Value };
				combat = new NaturalJourneyCombat(session, here, runtime, geometry, stopOnDeath: false, options.OptimizeHubs, mauPolicy,
					continuousAltgard ? combat.ReviveCount : haramelProgress?.Revives ?? 0)
				{
					ApproachMapId = contract.MapId,
				};
				if (leg.Destiny != null || leg.Haramel != null) combat.AfterBindRevive = () => EnterLegMap(newEntry: true);
				here.AvoidSpots = combat.DeathSpots;
				navigationDefense = combat;
				WithQuestLoot(combat);
				var haramelHints = new HashSet<(int Id, BotPosition At)>();
				int haramelKillHistoryStart = session.PacketHistory.Count;
				int haramelEntryHistoryStart = haramelKillHistoryStart;
				IReadOnlyDictionary<int, NaturalTemplateObjective> objectives = NaturalTemplateObjective.From(altgardPlans);
				var preservedQuests = (leg.Start.StartedQuestIds ?? []).Concat(leg.Start.LockedQuestIds).Except(leg.Order)
					.ToDictionary(id => id, id => session.Api.World.Quests.TryGetValue(id, out BotQuestState? state)
						? (state.Status, state.StepAndFlags) : throw new InvalidDataException($"Incoming Q{id} is missing."));
				IReadOnlyList<NaturalFlyZone> zones = NaturalFlyZone.Load(Path.Combine(runtime.RepoRoot,
					$"game-server/data/static_data/zones/zones_{leg.Hub.MapId}.xml"));
				// Leg 1's flight: Borender's rock is the landing (AF-05); a leg with no flight step never flies.
				NaturalAltgardStep? borender = leg.Steps.FirstOrDefault(step => step.Flight);
				BotPosition? rockTop = borender == null ? null : geometry.SnapToGround(leg.Hub.MapId, new BotPosition(borender.Position[0] - 2.5f,
					borender.Position[1], borender.Position[2] + 3, 0)) ?? throw new InvalidDataException("No rock top beside Borender.");
				float[] home = leg.Town?.Anchor ?? leg.Hub.Anchor;
				BotPosition ground = geometry.SnapToGround(leg.Hub.MapId, new BotPosition(home[0] - 3, home[1], home[2] + 1, 0))
					?? throw new InvalidDataException("No ground beside the Altgard obelisk.");
				float cruise = (rockTop?.Z ?? 0) + 8;
				BotPosition Rock() => rockTop ?? throw new InvalidDataException($"{leg.Leg} has no flight landing.");
				long? lastTakeoff = null;
				var haramelTravel = new NaturalHaramelTravel(session, runtime, () => geometry,
					async (at, range) =>
					{
						for (int retry = 0; retry < 100 && session.Api.World.MapId == leg.Haramel?.MapId; retry++)
							if (await WalkHaramelAsync(at, range)) return;
						Require.True(session.Api.World.MapId != leg.Haramel?.MapId, "Haramel floor approach exceeded its bounded replans.");
					});
				if (leg.Haramel != null)
				{
					haramelApproach = ApproachHaramelAsync;
					haramelKind = HaramelKind;
				}
				await TopUpHelpItemsAsync("run-start");
				await combat.BuffOurselfAsync(NaturalHelpTrigger.AfterRelog, token);
				string? previous = null;
				int repeats = 0;
				// AB-08: a decision repeated because the Cleric died on it is a retry, not a stall (OD-12): up to six of them.
				bool itemApproachRetreated = false;
				int revivesAtPrevious = combat.ReviveCount, deathRetries = 0;
				// AC-06: the escort's state across protocol runs (a death ends a run; the attempts and ended followers stay).
				int escortAttempts = 0;
				var escortEndedFollowers = new HashSet<int>();
				long? escortFollowerGoneAt = null, escortClearedUntil = null;
				// AB-08: timers started and when the client expects each to end; Infernus attempts.
				var timedAttempts = new Dictionary<int, int>();
				var timedEnds = new Dictionary<int, long>();
				var spawnAttempts = new Dictionary<string, int>();
				long? destinySpawnedAt = null;
				// AK-08: the cube's free slots as the inventory policy counts them, for a leg whose town has a merchant.
				// AG-07: far approaches take the leg's road first. A walk cut short by a fight (a retreat, or a death and the
				// revive at the bind) walks again from where it left the Cleric, three walks at most; no road at all leaves the
				// approach to the navigator.
				bool approachingAirline = false;
				int? warlockRoadRevives = null;
				farApproach = async (at, templateId, sourceComplete) =>
				{
					// AE-06/AO-04: delivery legs take hub flights; approaching the flight pad must not plan another flight.
					if (altgardLegId is "l7" or "l8" or "l9" or "l10" or "cg" or "l12" && session.Api.World.MapId == leg.Hub.MapId && !approachingAirline)
					{
						approachingAirline = true;
						try { await FlyTowardAsync(at); }
						finally { approachingAirline = false; }
					}
					await ReachPillarLevelAsync(at);
					float approachStop = UsesProvenWarlockSpawn(templateId) ? NaturalPullPlanner.SpellRange + 3 : FarApproachStop;
					for (int walk = 0; walk < 3 && Distance(session.CurrentPosition, at) > approachStop; walk++)
					{
						if (sourceComplete?.Invoke() == true) return;
						if (altgardLegId == "l10" && templateId == 210538 && warlockRoadRevives != combat.ReviveCount)
						{
							// BC-06: the fortress approach repeatedly re-entered the hunter/spellshifter camp,
							// then its avoidance route detoured as far south as Basfelt. Reach the lower
							// warlocks from Heart instead, using learned Return and the existing hub flight.
							NaturalAltgardContract heartLeg = NaturalAltgardContract.LoadLeg("l9");
							NaturalAltgardHub heart = heartLeg.Hub;
							BotPosition heartAt = new(heart.Anchor[0], heart.Anchor[1], heart.Anchor[2], 0);
							if (Distance(session.CurrentPosition, heartAt) > heart.Radius)
							{
								if (Distance(session.CurrentPosition, ground) > 60) await UseLearnedReturnToBindAsync();
								await RestSafelyAsync(token);
								approachingAirline = true;
								try { Require.True(await FlyTowardAsync(heartAt), "The warlock approach needs the fortress-to-Heart hub flight."); }
								finally { approachingAirline = false; }
							}
							int pillarRevives = combat.ReviveCount;
							await ReachPillarLevelAsync(at, heartLeg);
							if (combat.ReviveCount != pillarRevives) continue; // prepare the hub flight again after a death
							warlockRoadRevives = combat.ReviveCount;
							session.TraceDiagnostic("black-claw-warlock-heart-approach", new Dictionary<string, object?>
							{
								["position"] = session.CurrentPosition, ["revives"] = warlockRoadRevives,
							});
						}
						BotPosition before = session.CurrentPosition;
						int revives = combat.ReviveCount;
						if (await WalkRoadDefendingAsync(at, $"approach-road-{templateId}", stopAt: point => Distance(point, at) <= approachStop,
							sourceComplete: sourceComplete))
							return;
						if (combat.ReviveCount == revives && Distance(before, session.CurrentPosition) < 1)
							return;
					}
				};
				int? FreeCubeSlots() => leg.Town?.VendorNpcId == null ? null
					: NaturalIshalgenInventoryPolicy.Load(runtime.RepoRoot, session.Api.World.Inventory.Values.Select(item => item.ItemId))
						.Decide(session.Api.World, QuestNeededItems(), leg.CoinGear, leg.Haramel).FreeSlots;
				async Task SaveCoinProgressAsync() => await File.WriteAllTextAsync(
					Path.Combine(Path.GetDirectoryName(combatTracePath)!, "coin-gear-progress.json"),
					System.Text.Json.JsonSerializer.Serialize(new { session.CharacterId, ElapsedMillis = runtime.NowMillis, coinGearProgress }), token);
				if (laterCapital != null && altgardLegId == "l1")
					await NaturalLaterCapitalSteps.PickUpHeritageAsync(session, async step =>
					{
						session.BeginStep("rc-" + step.Key, "later-capital-heritage-pickup");
						int npc = await ApproachShippedSpawnAsync(step.NpcId, withinRange: step.TalkRange);
						for (int attempt = 1; ; attempt++)
						{
							try
							{
								string change = await NaturalAltgardQuestSteps.TalkAsync(session, step, npc, token);
								session.TraceDiagnostic("later-capital-step", new Dictionary<string, object?> { ["change"] = change });
								break;
							}
							catch (NaturalDialogTooFarException) when (attempt < 3) { await ReapproachForDialogAsync(npc); }
							}
					});
				if (laterCapital != null && altgardLegId == "l5" && NaturalLaterCapitalSteps.Leg5CityNeeded(session.Api.World))
					await VisitLeg5CapitalAsync();
				for (int sequence = 1; sequence <= 400; sequence++)
				{
					await session.SynchronizeAsync(token);
					EnterLegMap();
					if (haramelProgress != null) SaveHaramelProgress();
					if (laterCapital != null && altgardLegId == "l7" && session.Api.World.MapId == capitalContract.MapId)
					{
						var proposal = NaturalAltgardQuestSteps.State(session.Api.World, 2278);
						bool paid = session.Api.World.CompletedQuestIds.Contains(2278);
						// Finish Q2278's Cavalorn contact first, then batch the nearby book/pickups.
						// Leave the market hand-ins until after Balder, so no library return is needed.
						if ((paid || proposal is (3, >= 2) or (4, _)) && NaturalLaterCapitalSteps.Leg7LibraryNeeded(session.Api.World))
							await NaturalLaterCapitalSteps.CompleteLeg7LibraryAsync(session, PlayLaterCapitalStepAsync);
						if ((paid || proposal is (3, >= 3) or (4, _)) && NaturalLaterCapitalSteps.Leg7ErrandsNeeded(session.Api.World))
							await NaturalLaterCapitalSteps.CompleteLeg7ErrandsAsync(session, PlayLaterCapitalStepAsync);
					}
					if (laterCapital != null && altgardLegId == "l9" && session.Api.World.MapId == capitalContract.MapId &&
						!session.Api.World.CompletedQuestIds.Contains(2920))
						await NaturalLaterCapitalSteps.CompleteLeg9CityAsync(session, PlayLaterCapitalStepAsync, ApproachCapitalNpcAsync, token);
					if (laterCapital != null && altgardLegId == "l10" && session.Api.World.MapId == capitalContract.MapId &&
						!session.Api.World.CompletedQuestIds.Contains(2916))
						await NaturalLaterCapitalSteps.CompleteLeg10RobeAsync(session, PlayLaterCapitalStepAsync);
					if (laterCapital != null && altgardLegId == "l11" && session.Api.World.MapId == capitalContract.MapId &&
						(session.Api.World.CompletedQuestIds.Contains(2900) || NaturalAltgardQuestSteps.State(session.Api.World, 2900) is (4, 10)))
						await NaturalLaterCapitalSteps.CompleteLeg11LibraryAsync(session, PlayLaterCapitalStepAsync);
					if (laterCapital != null && altgardLegId == "l6" &&
						NaturalAltgardQuestSteps.State(session.Api.World, 2916) is (3, 3) &&
						Distance(session.CurrentPosition, ground) <= leg.Hub.Radius)
						await NaturalLaterCapitalSteps.RunMatchingStepsAsync(session, [NaturalLaterCapitalSteps.RobeBerth], PlayLaterCapitalStepAsync);
					if (session.Api.World.Level != helpCheckedAtLevel) await TopUpHelpItemsAsync("level-up");
					NaturalAltgardDecision next = NaturalAltgardDecisionEngine.Decide(leg,
						NaturalAltgardObservation.Observe(session.Api.World, session.CurrentPosition, session.Api.Timing.Now, FreeCubeSlots(), coinGearProgress, haramelProgress, HaramelNow()), objectives, sequence, only);
					session.TraceDiagnostic($"altgard-{altgardLegId}-decision", new Dictionary<string, object?>
					{
						["sequence"] = sequence, ["action"] = next.Action, ["step"] = next.StepKey, ["quest"] = next.QuestId,
						["outcome"] = next.Outcome, ["reason"] = next.Reason, ["level"] = session.Api.World.Level,
						["position"] = session.CurrentPosition, ["fp"] = session.Api.World.CurrentFlightTime,
						["checks"] = next.Checks.Select(check => $"{check.Rule}:{check.Verdict}").ToArray(),
					});
					session.PublishDashboard();
					string signature = $"{next.Action}/{next.StepKey}/{next.QuestId}/{next.Reason}";
					if (leg.Haramel != null) signature += "/" + string.Join(',', leg.Order.Select(id => $"{id}:{QuestStatus(id)}:{QuestVar(id)}")) +
						"/" + string.Join(',', leg.Haramel.CleanupItemIds.Select(id => ItemCount(session.Api.World, id)));
					bool diedSincePrevious = combat.ReviveCount > revivesAtPrevious;
					revivesAtPrevious = combat.ReviveCount;
					if (signature != previous) { repeats = 0; deathRetries = 0; }
					else if (diedSincePrevious) deathRetries++;
					else if (!itemApproachRetreated) repeats++;
					itemApproachRetreated = false;
					previous = signature;
					Require.True(repeats < 3 && deathRetries <= 6, $"Altgard leg {altgardLegId} made no progress on {signature}");
					if (next.Outcome == "complete")
					{
						if (only != null)
						{
							session.TraceDiagnostic($"altgard-{altgardLegId}-only-complete", new Dictionary<string, object?>
							{
								["quests"] = only.ToArray(), ["level"] = session.Api.World.Level, ["deaths"] = combat.ReviveCount,
							});
							return;
						}
						if (laterCapital != null && altgardLegId == "l5")
						{
							session.BeginStep("rc-book-field-materials", "collect-native-leg-5-book-drops");
							await NaturalLaterCapitalFieldCollection.CollectAsync(session, (kind, operation) => KillShippedSpawnAsync(kind, operation),
								async (source, item) => { await TryLootCorpseItemAsync(session, source, item, token); navigator.UnavailableObjects.Add(source); },
								() => RestSafelyAsync(token));
							if (Distance(session.CurrentPosition, ground) > leg.Hub.Radius) await UseLearnedReturnToBindAsync();
							await RestSafelyAsync(token);
						}
						if (laterCapital != null && altgardLegId == "l10")
						{
							Require.True(session.Api.World.CompletedQuestIds.Contains(2916), "Deyla's robe hand-in is incomplete.");
							await NaturalLaterCapitalSteps.ObtainLibraryPermissionAsync(session, PlayLaterCapitalStepAsync);
							await ApproachShippedSpawnAsync(leg.Endpoint.BindNpcId!.Value, withinRange: 5);
						}
						if (laterCapital != null && altgardLegId == "l11")
							Require.True(session.Api.World.CompletedQuestIds.Contains(2938) && ItemCount(session.Api.World, 182207026) == 0,
								"The library finish must consume Suthran's permission once.");
						if (laterCapital != null && altgardLegId == "l6")
							Require.True(NaturalAltgardQuestSteps.State(session.Api.World, 2916) is (3, 4),
								"Neparinerk must leave the carried robe quest at START/4 for Banatisai.");
						if (laterCapital != null && altgardLegId == "l7")
							Require.True(!NaturalLaterCapitalSteps.Leg7CityNeeded(session.Api.World), "The scheduled Leg 7 capital batch is incomplete.");
						if (laterCapital != null && altgardLegId == "l9")
						{
							Require.True(session.Api.World.CompletedQuestIds.Contains(2920), "Deyla's Leg 9 answer is incomplete.");
							await CollectLeg9ClothingAsync();
							if (Distance(session.CurrentPosition, ground) > leg.Hub.Radius) await UseLearnedReturnToBindAsync();
							await RestSafelyAsync(token);
						}
						foreach (var (id, expected) in preservedQuests)
							Require.True(session.Api.World.Quests.TryGetValue(id, out BotQuestState? state) &&
								(((continuousAltgard || laterCapital != null) && NaturalAltgardContinuation.AllowsAutomaticCampaignUnlock(id, expected, state,
									session.Api.World.Level, session.Api.World.CompletedQuestIds)) ||
								(altgardLegId == "l12" ? NaturalHaramelDecisionEngine.PreservesDeferredQuest(id, expected, state, session.Api.World.Level)
									: (state.Status, state.StepAndFlags) == expected)), $"Deferred Q{id} changed during {leg.Leg}.");
						await CompleteAltgardLeg1Async(leg);
						return;
					}
					if (next.Outcome != "planned")
					{
						await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, $"altgard-{altgardLegId}-stop.json"),
							System.Text.Json.JsonSerializer.Serialize(next), token);
						throw new InvalidDataException($"Altgard leg {altgardLegId} stopped: {next.Outcome} {next.Action}: {next.Reason}");
					}
					session.BeginStep($"af-{sequence:000}-{next.Action}", next.StepKey ?? next.Action);
					switch (next.Action)
					{
						case "enter-haramel":
							await EnterHaramelAsync();
							break;
						case "observe-haramel-entry":
							await ObserveHaramelEntryAsync();
							break;
						case "leave-haramel":
							await LeaveHaramelAsync();
							break;
						case "wait-haramel-expiry":
							while (HaramelNow() < haramelProgress!.FreshEntryAfterMillis)
							{
								await session.AdvanceAsync(TimeSpan.FromMilliseconds(Math.Min(1000, haramelProgress.FreshEntryAfterMillis!.Value - HaramelNow())), token);
								await session.SynchronizeAsync(token);
								await DefendAgainstEngagedAsync("haramel-empty-expiry");
							}
							break;
						case "haramel-movie":
							await NaturalHaramelQuestSteps.FinishLeadInMovieAsync(session, token);
							break;
						case "haramel-carts":
							while (QuestVar(28510) < 3 && session.Api.World.MapId == leg.Haramel!.MapId)
							{
								await KillShippedSpawnAsync(700950);
								await RestSafelyAsync(token);
							}
							break;
						case "haramel-ginseng":
							while (ItemCount(session.Api.World, 182212022) < 5 && session.Api.World.MapId == leg.Haramel!.MapId)
								await UseAndLootQuestObjectAsync(700954, 182212022, skipBlockedTarget: true);
							break;
						case "haramel-object":
						{
							NaturalAltgardStep use = leg.Steps.Single(step => step.Key == next.StepKey);
							int source = await ApproachShippedSpawnAsync(use.NpcId, withinRange: TalkRange(use.NpcId));
							await NaturalAltgardQuestSteps.UseObjectAsync(session, source, null, token);
							await session.SynchronizeAsync(token);
							break;
						}
						case "haramel-soup":
							await MakeHaramelSoupAsync();
							break;
						case "haramel-boss":
							await KillShippedSpawnAsync(leg.Haramel!.BossNpcId);
							await ObserveHaramelBossAsync();
							break;
						case "haramel-loot":
							await LootHaramelChestAsync();
							break;
						case "travel-to-map":
						{
							if (leg.Destiny != null)
							{
								await TravelDestinyMapAsync(next.MapId!.Value);
								break;
							}
							if (next.MapId == leg.Hub.MapId)
								await UseLearnedReturnToBindAsync();
							else
							{
								NaturalAltgardMapTrip trip = leg.MapTripList.Single(entry => entry.MapId == next.MapId);
								await EnsureOnGroundAsync();
								int teleporter = await ApproachShippedSpawnAsync(trip.TeleporterNpcId);
								NaturalServiceOutcome travelled = await new NaturalServiceSteps(session).TeleportAsync(teleporter,
									session.Api.World.Objects[teleporter].Position, trip.TalkRange, trip.LocationId, trip.Fare, trip.MapId, token);
								Require.True(travelled.IsDone, travelled.Reason);
							}
							EnterLegMap();
							Require.Equal(next.MapId, session.Api.World.MapId);
							break;
						}
						case "refresh-observation":
							await session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
							break;
						case "recover-destiny":
							await RecoverDestinyAsync("observed-state");
							break;
						case "equip-stigma":
						{
							NaturalAltgardStep stoneStep = leg.Steps.Single(step => step.Key == "q2900-stone");
							int skuld = await ApproachDestinyNpcAsync(stoneStep);
							if (skuld == 0 || NaturalAltgardQuestSteps.State(session.Api.World, leg.Destiny!.QuestId) is not (3, 99)) break;
							await NaturalAltgardQuestSteps.EquipDestinyStigmaAsync(session, leg.Destiny!, skuld, token);
							break;
						}
						case "destiny-fight":
							await FightDestinyAsync();
							break;
						case "revive-at-bind":
						case "revive-in-place":
							await RestSafelyAsync(token);
							break;
						case "enter-instance":
						{
							NaturalAltgardInstanceTrip trip = leg.InstanceTripList.Single(entry => entry.QuestId == next.QuestId);
							await PrepareRebirthAsync();
							int portal = await ApproachShippedSpawnAsync(trip.PortalNpcId, withinRange: TalkRange(trip.PortalNpcId));
							await UseInstancePortalAsync(portal, trip.UseMillis, trip.MapId);
							break;
						}
						case "leave-instance":
						{
							NaturalAltgardInstanceTrip trip = leg.InstanceTripList.Single(entry => entry.QuestId == next.QuestId);
							// BC-02 proved these intermediate routes; the long direct gate-to-exit query is disconnected.
							NaturalAltgardObjectUse guardian = leg.ObjectUseList.Single(use => use.QuestId == trip.QuestId && use.FromVar == trip.EnterVar);
							await ApproachShippedSpawnAsync(guardian.NpcId, withinRange: TalkRange(guardian.NpcId));
							NaturalNavigationResult entry = await NaturalIshalgenNavigator.ExploreWithinRangeAsync(trip.MapId, -1,
								new(trip.Arrival[0], trip.Arrival[1], trip.Arrival[2], 0), 3, navigator, "instance-entry", token);
							Require.True(entry.Arrived, entry.Reason);
							int exit = await ApproachShippedSpawnAsync(trip.ExitNpcId, withinRange: TalkRange(trip.ExitNpcId));
							await UseInstancePortalAsync(exit, trip.ExitUseMillis, leg.Hub.MapId);
							break;
						}
						case "template-accept":
						{
							await EnsureOnGroundAsync();
							QuestRunPlan plan = altgardPlans[next.QuestId!.Value];
							int starterId = plan.StartTrigger.Npcs.First().Id;
							int starter = await ApproachShippedSpawnAsync(starterId);
							for (int attempt = 1; ; attempt++)
							{
								try { await session.StartQuestAsync(starter, plan.Id, token); break; }
								catch (NaturalDialogTooFarException) when (attempt < 3) { await ReapproachForDialogAsync(starter); }
							}
							break;
						}
						case "template-work":
						{
							await EnsureOnGroundAsync();
							try
							{
								if (next.QuestId == 28509 && leg.Haramel != null) await CollectHaramelKeysAsync();
								if (next.QuestId == 28507 && leg.Haramel != null)
									foreach (int boss in new[] { 216897, 216907, 216915 })
									{
										RecordHaramelDeadHints();
										if (graph.GetMap(leg.Haramel.MapId)!.Waypoints.Any(w => w.TemplateId == boss && !haramelHints.Contains((boss, w.Position))))
											await KillShippedSpawnAsync(boss);
									}
								await CompleteTemplateQuestAsync(altgardPlans[next.QuestId!.Value], TemplatePhase.Work);
								if (leg.Haramel != null) await ObserveHaramelBossAsync();
							}
							catch (NaturalHaramelSourcesExhaustedException source) when (leg.Haramel != null)
							{
								session.TraceDiagnostic("haramel-exhausted-sources-outcome", new Dictionary<string, object?> { ["quest"] = next.QuestId, ["reason"] = source.Message });
								await LeaveHaramelAsync();
							}
							break;
						}
						case "template-claim":
						{
							await EnsureOnGroundAsync();
							NaturalAltgardObservation? beforeCoinReward = leg.CoinGear == null ? null :
								NaturalAltgardObservation.Observe(session.Api.World, session.CurrentPosition);
							await CompleteTemplateQuestAsync(altgardPlans[next.QuestId!.Value], TemplatePhase.Claim);
							if (leg.CoinGear is { } rewardGear)
							{
								coinGearProgress = coinGearProgress!.ObserveReward(rewardGear, beforeCoinReward!,
									NaturalAltgardObservation.Observe(session.Api.World, session.CurrentPosition));
								await SaveCoinProgressAsync();
							}
							await TopUpHelpItemsAsync("town");
							break;
						}
						case "coin-purchase":
						case "coin-equip":
						{
							NaturalCoinGear gear = leg.CoinGear!;
							int item = int.Parse(next.StepKey!);
							await EnsureOnGroundAsync();
							var inventory = NaturalIshalgenInventoryPolicy.Load(runtime.RepoRoot,
								session.Api.World.Inventory.Values.Select(i => i.ItemId).Concat(gear.ProtectedItemIds));
							var shopping = new NaturalCoinGearSteps(session, runtime.Data, gear, inventory);
							if (next.Action == "coin-purchase")
							{
								int vendor = await ApproachShippedSpawnAsync(gear.VendorNpcId, withinRange: TalkRange(gear.VendorNpcId));
								coinGearProgress = await shopping.PurchaseAsync(vendor, item, coinGearProgress!, token);
								await SaveCoinProgressAsync();
							}
							else await shopping.EquipAsync(item, coinGearProgress!, token);
							break;
						}
						case "talk":
							await PlayContractStepAsync(leg.Steps.Single(candidate => candidate.Key == next.StepKey));
							break;
						case "use-item":
						{
							NaturalAltgardItemUse use = leg.RequiredItemUse;
							if (!use.Anywhere && use.ZoneAnchor is { } zone)
							{
								int preparationRetreats = combat.CompletedRetreats, preparationRevives = combat.ReviveCount;
								// AB-08: Q24013's poison works only inside its zone, and the zone is packed with Feral Sharpeyes (18
								// spawns, 295 s respawn): no route keeps clear of every circle. Do what a player does: take the road toward
								// the anchor, fighting what engages, and use the poison at the first step that is well inside the zone,
								// before the next Sharpeye walks up. A death or a retreat leaves the quest at its var, and the next
								// decision starts again from the bind.
								await EnsureOnGroundAsync();
								BotPosition anchor = new(zone[0], zone[1], zone[2], 0);
								bool Inside(BotPosition at) => use.InZone(at.X, at.Y, at.Z, ZoneMargin);
								if (!Inside(session.CurrentPosition) && !await WalkRoadDefendingAsync(anchor, "item-use-zone-road", stopAt: Inside))
								{
									RecordPreparationRetreat();
										break;
								}
								if (!await DefendAgainstEngagedAsync("item-use-zone") || session.Api.World.IsDead || !Inside(session.CurrentPosition))
								{
									await RestSafelyAsync(token);
									RecordPreparationRetreat();
									break;
								}
								session.TraceDiagnostic($"altgard-{altgardLegId}-item-zone", new Dictionary<string, object?>
								{
									["zone"] = use.Zone, ["position"] = session.CurrentPosition, ["hp"] = session.Api.World.CurrentHp,
								});

								void RecordPreparationRetreat()
								{
									itemApproachRetreated = combat.CompletedRetreats > preparationRetreats &&
										combat.ReviveCount == preparationRevives;
									if (!itemApproachRetreated) return;
									// The poison was not sent. Resume from the observed retreat position without
									// spending an item-use attempt; actual use retries and the watchdog stay bounded.
									session.TraceDiagnostic("quest-item-approach-retreated", new Dictionary<string, object?>
									{
										["questId"] = use.QuestId, ["itemId"] = use.ItemId, ["itemUseAttempted"] = false,
										["retreats"] = combat.CompletedRetreats - preparationRetreats,
										["position"] = session.CurrentPosition,
									});
								}
							}
							await NaturalAltgardQuestSteps.UseQuestItemAsync(session, use, runtime.Data.ItemDataDh.GetItemTemplate(use.ItemId), token);
							// The use may spawn monsters (Q24013: two Feral Black Claw Sharpeyes): deal with them first.
							await session.AdvanceAsync(TimeSpan.FromSeconds(4), token);
							await DefendAgainstEngagedAsync("item-use-spawns");
							await RestSafelyAsync(token);
							break;
						}
						case "bind":
						{
							// AB-08, the standing bind policy: bind at the obelisk of the hub the leg works out of. AG-07: a hub the
							// Cleric does not stand at (Leg 6: Trader's Berth, from the Basfelt bind) is reached by the hub flights.
							await EnsureOnGroundAsync();
							NaturalAltgardBind bind = leg.Bind ?? throw new InvalidDataException($"{leg.Leg} has no bind.");
							await FlyTowardAsync(new BotPosition(bind.Position[0], bind.Position[1], bind.Position[2], 0));
							int stone = await ApproachShippedSpawnAsync(bind.NpcId);
							NaturalServiceOutcome bound = await new NaturalServiceSteps(session).BindAsync(stone, session.Api.World.Objects[stone].Position,
								leg.Hub.MapId, bind.Price, bind.AcceptRange, token);
							Require.True(bound.IsDone, bound.Reason);
							break;
						}
						case "hunt":
						{
							// AB-08: a custom kill counter, one kill per decision (the var moves, so the next decision differs).
							await EnsureOnGroundAsync();
							int var = NaturalAltgardQuestSteps.State(session.Api.World, next.QuestId!.Value)?.Var ?? 0;
							NaturalAltgardHunt hunt = leg.HuntList.First(entry => entry.QuestId == next.QuestId && var >= entry.FromVar && var < entry.ToVar);
							bool Counted() => (NaturalAltgardQuestSteps.State(session.Api.World, next.QuestId!.Value)?.Var ?? 0) != var;
							try
							{
								if (leg.InstanceTripList.FirstOrDefault(trip => trip.QuestId == next.QuestId && trip.MapId == session.Api.World.MapId && var == trip.SpawnVar) is { } bossTrip)
								{
									NaturalNavigationResult reached = await NaturalIshalgenNavigator.ExploreWithinRangeAsync(bossTrip.MapId, bossTrip.BossNpcId,
										new(bossTrip.BossPosition[0], bossTrip.BossPosition[1], bossTrip.BossPosition[2], 0),
										NaturalPullPlanner.SpellRange + 3, navigator, "quest-spawn", token);
									Require.True(reached.Arrived, reached.Reason);
									int boss = reached.TargetObjectId ?? await session.WaitForNpcAsync(bossTrip.BossNpcId, token);
									await PullAndKillAsync(boss, "instance-boss");
									await RestSafelyAsync(token);
									break;
								}
								int huntKind = hunt.NpcIds.First(SpawnsOnMap);
								int target;
								try { target = await KillShippedSpawnAsync(huntKind); }
								catch (InvalidDataException noRoute) when (!Counted() && !session.Api.World.IsDead &&
									noRoute.Message.Contains("No collision-checked route", StringComparison.Ordinal))
								{
									// The navigator found no route from where the last fight or revive left the Cleric (the Leg 4
									// catch-up, toward Sumarhon). Take the planner's road to the nearest spawn, as talk steps do.
									BotPosition spawn = graph.GetMap(contract.MapId)!.Waypoints.Where(waypoint => waypoint.TemplateId == huntKind)
										.Select(waypoint => waypoint.Position).MinBy(position => Distance(position, session.CurrentPosition));
									session.TraceDiagnostic($"altgard-{altgardLegId}-hunt-road", new Dictionary<string, object?>
									{
										["quest"] = next.QuestId, ["kind"] = huntKind, ["spawn"] = spawn, ["reason"] = noRoute.Message,
									});
									if (!await WalkRoadDefendingAsync(spawn, $"hunt-road-{huntKind}", within: 40) && !session.Api.World.IsDead)
										await UseLearnedReturnToBindAsync();
									target = await KillShippedSpawnAsync(huntKind);
								}
								navigator.UnavailableObjects.Add(target);
							}
							catch (InvalidDataException exception) when (Counted())
							{
								// The kill counted on the way, in a fight-through (Q24112: Sumarhon fell as an attacker in his own
								// camp), and the search then waited for a spawn that was already dead. The var is what counts.
								session.TraceDiagnostic($"altgard-{altgardLegId}-hunt-counted-on-the-way", new Dictionary<string, object?>
								{
									["quest"] = next.QuestId, ["from"] = var, ["reason"] = exception.Message,
								});
							}
							await RestSafelyAsync(token);
							break;
						}
						case "timed":
							await RunTimedQuestAsync(next.QuestId!.Value);
							break;
						case "spawn-kill":
							await RunSpawnKillAsync(leg.SpawnList.Single(spawn => spawn.Key == next.StepKey));
							break;
						case "air-kills":
						{
							// No fungus is visible from the ground: the fight starts from Borender's rock (AF-06).
							await FlyToAsync(Rock());
							NaturalAirCombat.Outcome outcome = await NaturalAirCombat.RunAsync(session, geometry, leg.Hub.MapId, zones,
								leg.RequiredFlight.WaterLevel, new NaturalLandingTarget("platform", Rock()), cruise, leg.RequiredAirKills.QuestId,
								(origin, skill, level, target) => runtime.CreateSpellCast(session.Api.World, origin, skill, level, target),
								() => runtime.NowMillis, token);
							lastTakeoff = runtime.NowMillis;
							session.TraceDiagnostic($"altgard-{altgardLegId}-air-kills", new Dictionary<string, object?>
							{
								["kills"] = outcome.Kills, ["sorties"] = outcome.Sorties, ["missed"] = outcome.Missed,
								["shootingSeconds"] = outcome.ShootingSeconds, ["landedFp"] = outcome.LandedFp,
							});
							break;
						}
						case "hunt-for-level":
						{
							await EnsureOnGroundAsync();
							int[] hunted = altgardPlans.Values.SelectMany(plan => plan.Steps.Where(step => step.Kind == "kill"))
								.SelectMany(step => step.Npcs.Select(npc => npc.Id)).Where(SpawnsOnMap).Distinct().ToArray();
							int levelBefore = session.Api.World.Level;
							for (int kill = 0; kill < 12 && session.Api.World.Level == levelBefore; kill++)
							{
								int target = await KillShippedSpawnAsync(hunted[kill % hunted.Length]);
								navigator.UnavailableObjects.Add(target);
								await RestSafelyAsync(token);
							}
							break;
						}
						case "use-object":
						{
							// AM-04/05: a quest object in the world (Q2213's Okaru Tree, Q24012's carts), used as the client does.
							await EnsureOnGroundAsync();
							NaturalAltgardObjectUse use = leg.ObjectUseList.Single(candidate => candidate.Key == next.StepKey);
							int item = await ApproachShippedSpawnAsync(use.NpcId, skipBlockedTarget: true,
								withinRange: use.MapId == null ? null : TalkRange(use.NpcId));
							if (altgardLegId == "l10")
							{
								// Java ItemUseObserver aborts the bar on an attack. The last approach can draw
								// another instance enemy even after its earlier guards were cleared.
								await RestSafelyAsync(token);
								if (NaturalAltgardQuestSteps.State(session.Api.World, use.QuestId)?.Var != use.FromVar) break;
								item = await ApproachShippedSpawnAsync(use.NpcId, skipBlockedTarget: true, withinRange: TalkRange(use.NpcId));
							}
							// AC-05: the use may open a dialog first (Q2221's safe), answered with the contract's close action.
							bool used = await NaturalAltgardQuestSteps.UseContractObjectAsync(session, use, item, token);
							if (used && use.MovieId != null)
							{
								await session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token, packet => packet.Get<int>("worldId") == leg.Hub.MapId);
								await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
								session.AcceptTeleportPosition();
								await session.SynchronizeAsync(token);
							}
							// A used cart dies but stays in view (AM-05): never pick it again.
							if (use.Disappears) navigator.UnavailableObjects.Add(item);
							session.TraceDiagnostic($"altgard-{altgardLegId}-object", new Dictionary<string, object?>
							{
								["use"] = use.Key, ["object"] = item, ["used"] = used,
								["quest"] = NaturalAltgardQuestSteps.State(session.Api.World, use.QuestId)?.ToString(),
							});
							break;
						}
						case "escort":
							await EnsureOnGroundAsync();
							await RunEscortAsync(leg.EscortList.Single(candidate => candidate.Key == next.StepKey));
							break;
						case "enter-zone":
						{
							// Q24012's farmland step: the quest's own objects stand inside the zone, so walking to the nearest one
							// enters it (the hub's NPCs stand outside the zone polygon, AM-05).
							await EnsureOnGroundAsync();
							NaturalAltgardZoneStep zone = leg.ZoneStepList.Single(entry => entry.QuestId == next.QuestId);
							if (zone.Anchor is { } anchor)
								await WalkRoadDefendingAsync(new(anchor[0], anchor[1], anchor[2], 0), "campaign-zone", within: Math.Min(3, zone.Radius));
							else
								await ApproachShippedSpawnAsync(leg.ObjectUseList.First(use => use.QuestId == next.QuestId).NpcId, skipBlockedTarget: true);
							break;
						}
						case "collect":
						{
							// Q24012's hairpins and waist bands, which drop only from var 5 (AM-05). AM-Q3: the SEASONED black claw
							// patrols are not sought out; they are fought only when they block the way.
							await EnsureOnGroundAsync();
							NaturalAltgardCollection collection = leg.CollectionList.Single(entry => entry.QuestId == next.QuestId);
							NaturalAltgardCollectedItem wanted = collection.Items.First(entry => ItemCount(session.Api.World, entry.ItemId) < entry.Count);
							// BC-06: the campaign's boss can be killed and looted while clearing its approach.
							// Hand the observed item back just as the template collection loop does.
							int? CollectedCampaignSource() => altgardLegId == "l10" && ItemCount(session.Api.World, wanted.ItemId) >= wanted.Count &&
								SweptQuestItems.TryGetValue(session.Api.World, out Dictionary<int, List<int>>? swept)
								? swept.Where(entry => entry.Value.Contains(wanted.ItemId)).Select(entry => (int?)entry.Key).LastOrDefault()
								: null;
							int[] sources = wanted.SourceNpcIds.Where(SpawnsOnMap)
								.Where(id => runtime.Data.NpcDataDh.GetNpcTemplate(id)?.GetRank() != Aion.GameServer.Model.Templates.Npc.NpcRank.SEASONED)
								.ToArray();
							// AB-08: Komu's Horn has one source, a SEASONED one; take it. If he is dead (an hourly respawn, AB-Q3), hunt
							// the grounds around him until he is back.
							if (sources.Length == 0) sources = wanted.SourceNpcIds.Where(SpawnsOnMap).ToArray();
							Require.True(sources.Length > 0, $"No source of item {wanted.ItemId} spawns here.");
							if (leg.AvoidList.FirstOrDefault(avoid => sources.Contains(avoid.NpcId)) is { } boss)
							{
								await ApproachShippedSpawnAsync(boss.NpcId, skipBlockedTarget: true, withinRange: NaturalPullPlanner.SpellRange + 3,
									completedSource: altgardLegId == "l10" ? CollectedCampaignSource : null);
								long waitUntil = runtime.NowMillis + boss.RespawnSeconds * 1000L;
								while (CollectedCampaignSource() == null && !session.Api.World.Objects.Values.Any(known => known.TemplateId == boss.NpcId && !known.IsCorpse) && runtime.NowMillis < waitUntil)
								{
									session.TraceDiagnostic($"altgard-{altgardLegId}-wait-for-respawn", new Dictionary<string, object?> { ["npc"] = boss.NpcId });
									// Komu's neighbours (AB-02): bigfoot and ruthless mosbears and grove malodors.
									if (boss.NpcId == 210566)
									{
										int[] around = [210441, 210581, 210444];
										int prey = await KillShippedSpawnAsync(around.First(SpawnsOnMap));
										navigator.UnavailableObjects.Add(prey);
									}
									else await session.AdvanceAsync(TimeSpan.FromSeconds(30), token);
									await RestSafelyAsync(token);
									await ApproachShippedSpawnAsync(boss.NpcId, skipBlockedTarget: true, withinRange: NaturalPullPlanner.SpellRange + 3,
										completedSource: altgardLegId == "l10" ? CollectedCampaignSource : null);
								}
							}
							for (int kill = 0; kill < 6 && ItemCount(session.Api.World, wanted.ItemId) < wanted.Count; kill++)
							{
								int source = await KillShippedSpawnAsync(sources[kill % sources.Length], altgardLegId == "l10"
									? new(QuestRunOperationKind.CollectQuestDrop, Count: wanted.Count, ItemId: wanted.ItemId) : null);
								await TryLootCorpseItemAsync(session, source, wanted.ItemId, token);
								navigator.UnavailableObjects.Add(source);
								await RestSafelyAsync(token);
							}
							break;
						}
						case "return-to-endpoint":
						{
							// AM-Q1: Leg 2 ends at Manir's Campsite, beside the NPC who takes its last hand-in.
							await EnsureOnGroundAsync();
							float[] end = leg.Endpoint.Anchor ?? leg.Hub.Anchor;
							int endNpc = altgardPlans.Values.SelectMany(plan => plan.EndNpcs)
								.First(npc => npc.Positions.Any(at => MathF.Sqrt(MathF.Pow(at.X - end[0], 2) + MathF.Pow(at.Y - end[1], 2)) <= leg.Endpoint.Radius)).Id;
							await ApproachShippedSpawnAsync(endNpc);
							break;
						}
						case "cube-expansion":
							await VisitLeg5CapitalAsync();
							break;
						case "town-service":
						{
							// AK-08: the cube is too full to loot. At the town's merchant: wear the upgrades, then sell what the inventory
							// policy calls surplus (never buying anything, OD-7), as the bridge's shop stop does.
							await EnsureOnGroundAsync();
							await EquipUpgradesAsync(token);
							await session.SynchronizeAsync(token);
							BotWorldModel world = session.Api.World;
							NaturalInventoryPlan plan = NaturalIshalgenInventoryPolicy.Load(runtime.RepoRoot, world.Inventory.Values.Select(item => item.ItemId))
								.Decide(world, QuestNeededItems(), leg.CoinGear, leg.Haramel);
							var sales = plan.Sales.Select(sale => new NaturalSale(sale.ObjectId, sale.ItemId, sale.Count)).ToList();
							int vendor = await ApproachShippedSpawnAsync(leg.Town!.VendorNpcId!.Value);
							NaturalVendorResult trade = await new NaturalServiceSteps(session).TradeAsync(vendor, sales, [],
								id => runtime.Data.GoodsListDataDh.GetGoodsListById(id)?.GetItemIdList() ?? [],
								item => runtime.Data.ItemDataDh.GetItemTemplate(item).GetPrice(), token);
							session.TraceDiagnostic("town-service", new Dictionary<string, object?>
							{
								["vendor"] = leg.Town.VendorNpcId, ["freeBefore"] = plan.FreeSlots, ["freeAfter"] = FreeCubeSlots(),
								["sold"] = trade.Sold.Select(sold => $"{sold.ItemId}x{sold.Count}").ToArray(), ["kinah"] = world.Kinah,
							});
							break;
						}
						case "carrier-hunt":
						{
							// AK-08: a ring carrier present in its hours (Q2292): walk to its spawn, kill it with the journey's combat,
							// and loot its ring. Gone or dead when the Cleric arrives (another player, a respawn), the next decision
							// asks the policy again with the clock moved on.
							await EnsureOnGroundAsync();
							NaturalAltgardTimedSpawn carrier = leg.TimedSpawnList.Single(candidate => $"{candidate.NpcId}" == next.StepKey);
							int target;
							try { target = await KillShippedSpawnAsync(carrier.NpcId); }
							catch (InvalidDataException missing) when (missing.Message.Contains("No client-observed NPC", StringComparison.Ordinal))
							{
								session.TraceDiagnostic("carrier-missed", new Dictionary<string, object?>
								{
									["carrier"] = carrier.NpcId, ["ring"] = carrier.ItemId, ["gameMinutes"] = session.Api.World.GameMinutesAt(session.Api.Timing.Now),
									["reason"] = missing.Message,
								});
								break;
							}
							navigator.UnavailableObjects.Add(target);
							bool looted = ItemCount(session.Api.World, carrier.ItemId) > 0 ||
								await TryLootCorpseItemAsync(session, target, carrier.ItemId, token);
							session.TraceDiagnostic("carrier-killed", new Dictionary<string, object?>
							{
								["carrier"] = carrier.NpcId, ["ring"] = carrier.ItemId, ["looted"] = looted,
								["gameMinutes"] = session.Api.World.GameMinutesAt(session.Api.Timing.Now),
							});
							await RestSafelyAsync(token);
							break;
						}
						case "wait-for-carrier":
						case "wait-for-escort":
						{
							// AK-08 (AK-Q3): nothing else is left before a carrier's window opens (AG-07, AG-Q3 (a): or an escort
							// follower's). Wait at the hub's obelisk, an hour of game time (5 real minutes) per decision, so the clock is
							// read again as it runs.
							await EnsureOnGroundAsync();
							float[] hub = leg.Hub.Anchor;
							if (MathF.Sqrt(MathF.Pow(session.CurrentPosition.X - hub[0], 2) + MathF.Pow(session.CurrentPosition.Y - hub[1], 2)) > leg.Hub.Radius)
								await ApproachShippedSpawnAsync(leg.Endpoint.BindNpcId ?? leg.Bind?.NpcId ?? leg.Start.BindNpcId);
							await RestSafelyAsync(token);
							long before = session.Api.World.GameMinutesAt(session.Api.Timing.Now) ?? 0;
							await session.AdvanceAsync(TimeSpan.FromMinutes(5), token);
							await session.SynchronizeAsync(token);
							bool escortWait = next.Action == "wait-for-escort";
							session.TraceDiagnostic(escortWait ? "escort-wait" : "carrier-wait", new Dictionary<string, object?>
							{
								[escortWait ? "escort" : "carrier"] = next.StepKey, ["from"] = before,
								["to"] = session.Api.World.GameMinutesAt(session.Api.Timing.Now),
							});
							break;
						}
						case "return-to-hub":
							await EnsureOnGroundAsync();
							// AB-08: the hub's own obelisk when the leg binds there (Basfelt), not the one the leg started bound at.
							await ApproachShippedSpawnAsync(leg.Endpoint.BindNpcId ?? leg.Bind?.NpcId ?? leg.Start.BindNpcId);
							break;
						default:
							throw new InvalidDataException($"The Altgard leg runner has no executor for {next.Action}.");
					}
				}
				throw new InvalidDataException("An Altgard leg exceeded 400 decisions.");

				// Fly from wherever the Cleric stands (AF-05): wait out the takeoff reuse and a refill, check the policy first.
				async Task FlyToAsync(BotPosition destination, float? cruiseHeight = null, NaturalAltgardFlight? flightRules = null)
				{
					// Java PlayerController.updateSoulSickness applies 8291 on bind revival. Its speed penalty
					// doubles the pillar descent's FP cost; wait for the real icon removal before taking off.
					if (altgardLegId is "l10" or "l12" && session.Api.World.VisibleEffects?.FirstOrDefault(effect => effect.SkillId == 8291) is { } sickness)
					{
						Require.True(sickness.RemainingMillis > 0, "Soul Sickness has no observed expiry for the flight wait.");
						long until = runtime.NowMillis + sickness.RemainingMillis + 1000L;
						session.TraceDiagnostic("flight-wait-for-soul-sickness", new Dictionary<string, object?>
						{
							["skillId"] = sickness.SkillId, ["remainingMillis"] = sickness.RemainingMillis,
						});
						while (NaturalAltgardQuestSteps.HasEffect(session.Api.World, 8291) && runtime.NowMillis < until)
						{
							await session.AdvanceAsync(TimeSpan.FromMilliseconds(Math.Min(1000, until - runtime.NowMillis)), token);
							await session.SynchronizeAsync(token);
							if (session.Api.World.IsDead) { await RestSafelyAsync(token); return; }
						}
						Require.True(!NaturalAltgardQuestSteps.HasEffect(session.Api.World, 8291), "Soul Sickness did not expire after its observed flight wait.");
					}
					BotPosition from = session.CurrentPosition;
					if (Distance(from, destination) < 3) return;
					BotWorldModel world = session.Api.World;
					long wait = NaturalFlightPolicy.RestoreMillis(world.CurrentFlightTime, world.MaxFlightTime, world.MaxFlightTime);
					if (lastTakeoff is { } last) wait = Math.Max(wait, last + NaturalFlightPolicy.TakeoffReuseMillis - runtime.NowMillis);
					if (wait > 0) await session.AdvanceAsync(TimeSpan.FromMilliseconds(wait + 100), token);
					await session.SynchronizeAsync(token);
					NaturalFlightDecision ready = NaturalFlightPolicy.CanTakeOff(new NaturalTakeoffObservation(true, from, false,
						(flightRules ?? leg.RequiredFlight).WaterLevel, runtime.NowMillis, lastTakeoff, false, false, false), zones);
					Require.True(ready.Allowed, $"Cannot take off: {ready.Reason}");
					NaturalFlightRoute route = NaturalFlightProtocol.Plan(geometry, leg.Hub.MapId, from, destination, cruiseHeight ?? cruise);
					Require.True(route.IsUsable, $"No flight to {destination}: {route.Refusal}");
					lastTakeoff = runtime.NowMillis;
					float speed = await NaturalFlightProtocol.TakeOffAsync(session, token);
					NaturalFlightDecision go = NaturalFlightPolicy.CanFly(NaturalFlightProtocol.ToPlan(route, from, speed), world.CurrentFlightTime, zones);
					session.TraceDiagnostic($"altgard-{altgardLegId}-flight", new Dictionary<string, object?>
					{
						["from"] = from, ["to"] = destination, ["meters"] = route.Meters, ["fp"] = world.CurrentFlightTime, ["decision"] = go.Reason,
					});
					Require.True(go.Allowed, go.Reason);
					await NaturalFlightProtocol.FlyAsync(session, leg.Hub.MapId, from, route.Waypoints, speed, token);
					await NaturalFlightProtocol.LandAsync(session, token);
					await session.SynchronizeAsync(token);
				}

				// Everything but Borender's steps and the air fight is on foot: come down from the rock first.
				async Task EnsureOnGroundAsync()
				{
					if (rockTop is { } top && session.CurrentPosition.Z > top.Z - 20) await FlyToAsync(ground);
				}

				async Task ReachPillarLevelAsync(BotPosition destination, NaturalAltgardContract? pillarLeg = null)
				{
					NaturalAltgardContract flightLeg = pillarLeg ?? leg;
					if (flightLeg.PillarFlight is not { } pillar || session.Api.World.MapId != flightLeg.Hub.MapId ||
						pillar.IsUpper(session.CurrentPosition.Z) == pillar.IsUpper(destination.Z)) return;
					bool goingUp = pillar.IsUpper(destination.Z);
					float[] departure = goingUp ? pillar.Lower : pillar.Upper;
					float[] arrival = goingUp ? pillar.Upper : pillar.Lower;
					BotPosition start = new(departure[0], departure[1], departure[2], 0);
					BotPosition landing = new(arrival[0], arrival[1], arrival[2], 0);
					if (Distance(session.CurrentPosition, start) > 5)
						await WalkRoadDefendingAsync(start, "pillar-takeoff-road", within: 5);
					NaturalNavigationResult atStart = await NaturalIshalgenNavigator.ExploreAnchorAsync(
						leg.Hub.MapId, -1, start, navigator, "pillar-takeoff", token);
					Require.True(atStart.Arrived, atStart.Reason);
					await DefendAgainstEngagedAsync("pillar-takeoff");
					await RestSafelyAsync(token);
					if (pillar.IsUpper(session.CurrentPosition.Z) == goingUp) return; // a revive can already reach the upper bind
					await FlyToAsync(landing, MathF.Max(start.Z, landing.Z) + 12, flightLeg.RequiredFlight);
				}

				// AB-08: walk the Altgard travel planner's road to a point (the navmesh path when it has none), one section of 16 points
				// at a time, defending against whatever engages between sections.
				// The maintainer's 2026-10-01 note: fly between hubs rather than walk. AG-00: take the quickest journey by walks and
				// flight transporters (NaturalAirlineRoutes.Journey), one flight at a time, planning again from each landing, so
				// Basfelt to Trader's Berth is two flights. False when walking is better from the start.
				async Task<bool> FlyTowardAsync(BotPosition destination)
				{
					int flown = 0;
					for (NaturalAirlineJourney? journey = NaturalAirlineRoutes.Journey(airlines, leg.Hub.MapId, session.CurrentPosition, destination);
						journey != null && flown < 4;
						journey = NaturalAirlineRoutes.Journey(airlines, leg.Hub.MapId, session.CurrentPosition, destination))
					{
						NaturalAirlineRoute route = journey.Flights.First();
						session.TraceDiagnostic("airline-chosen", new Dictionary<string, object?>
						{
							["route"] = route.Route, ["npc"] = route.NpcId, ["location"] = route.LocationId, ["from"] = session.CurrentPosition,
							["destination"] = destination, ["flights"] = journey.Flights.Select(flight => flight.Route).ToArray(),
							["seconds"] = journey.Seconds, ["walkAllSeconds"] = journey.WalkAllSeconds,
						});
						if (altgardLegId == "l12") await ReachPillarLevelAsync(route.Departure);
						if (Distance(session.CurrentPosition, route.Departure) > 60)
							await WalkRoadDefendingAsync(route.Departure, "airline-road", within: 15);
						int transporter = await ApproachShippedSpawnAsync(route.NpcId);
						if (Distance(session.CurrentPosition, route.Departure) > 3)
						{
							NaturalNavigationResult atPad = await NaturalIshalgenNavigator.ExploreAnchorAsync(
								contract.MapId, -1, route.Departure, navigator, "airline-departure", token);
							Require.True(atPad.Arrived, atPad.Reason);
						}
						NaturalServiceOutcome outcome = await new NaturalServiceSteps(session).FlyAsync(transporter,
							session.Api.World.Objects[transporter].Position, 6, route, token);
						Require.True(outcome.IsDone, outcome.Reason);
						flown++;
					}
					return flown > 0;
				}

				async Task<bool> WalkRoadDefendingAsync(BotPosition destination, string purpose, float within = 12,
					Func<BotPosition, bool>? stopAt = null, Func<bool>? sourceComplete = null)
				{
					int map = session.Api.World.MapId ?? leg.Hub.MapId;
					// The ground nearest the point, as AB-02 found it: a spot on a slope may not snap where it stands.
					BotPosition goal = geometry.GroundAround(map, destination, [3f, 5f, 8f, 12f]).FirstOrDefault() is { } ground &&
						ground != default ? ground : geometry.SnapToGround(map, destination with { Z = destination.Z + 2 }) ?? destination;
					BotTravelPlanner? planner = BotTravelPlanner.For(map, geometry, runtime.Data);
					IReadOnlyList<BotPosition> road = planner?.PlanJourney(map, session.CurrentPosition, goal, session.Api.World.Level, [])?.Route
						?? geometry.FindJourneyPath(map, session.CurrentPosition, goal);
					if (road.Count == 0)
					{
						// The Cleric stands on ground the navmesh does not connect to the goal (a ledge a fight left it on).
						session.TraceDiagnostic($"altgard-{altgardLegId}-no-road", new Dictionary<string, object?>
						{
							["purpose"] = purpose, ["goal"] = goal, ["outcome"] = BotNavMeshRouter.LastOutcome.ToString(),
							["position"] = session.CurrentPosition,
						});
						return false;
					}
					// Walk the road only as far as its first point where the caller wants to stop.
					if (stopAt != null && road.Select((point, index) => (point, index)).FirstOrDefault(entry => stopAt(entry.point)) is
						{ point: var stop, index: var cut } && stopAt(stop))
						road = road.Take(cut + 1).ToArray();
					for (int at = 0; at < road.Count; at += 16)
					{
						// A navigation fight can fulfill this source's objective before the road finishes.
						// Hand its real zero-HP evidence back rather than crossing more camp for another target.
						if (sourceComplete?.Invoke() == true) return true;
						BotPosition[] segment = road.Skip(at).Take(16).ToArray();
						int roadRevives = combat.ReviveCount;
						if (altgardLegId is "l10" or "l11" or "l12")
						{
							// BC-06: the first Heart descent succeeded, but the unchecked road walked
							// into a pack before defense ran. Clear observed blockers before crossing it.
							var progress = new NaturalApproachProgress();
							for (int clear = 0; Distance(session.CurrentPosition, segment[^1]) > 3 && !navigator.IsSegmentSafe(segment, null); clear++)
							{
								BotPosition beforeClear = session.CurrentPosition;
								int killsBefore = navigator.UnavailableObjects.Count;
								if (!progress.CanRetry(clear) || !await TryClearObservedBlockerAsync(segment[^1])) return false;
								if (sourceComplete?.Invoke() == true) return true;
								if (combat.ReviveCount != roadRevives || session.Api.World.IsDead) { await RestSafelyAsync(token); return false; }
								progress.Observe(Distance(beforeClear, session.CurrentPosition), navigator.UnavailableObjects.Count > killsBefore);
							}
							// Clearing can walk a checked detour all the way to this segment's endpoint. The old
							// points behind us may still cross the guard's circle; do not walk or reject them again.
							if (Distance(session.CurrentPosition, segment[^1]) <= 3) continue;
						}
						await navigator.MoveAsync(segment, token);
						await session.SynchronizeAsync(token);
						if (sourceComplete?.Invoke() == true) return true;
						// Synchronize can defend and bind-revive before the explicit defense below. Its old
						// road no longer starts here; return to the caller so it plans from the new position.
						if (altgardLegId is "l10" or "l11" or "l12" && combat.ReviveCount != roadRevives) return false;
						if (!await DefendAgainstEngagedAsync(purpose) || session.Api.World.IsDead)
						{
							await RestSafelyAsync(token);
							return false;
						}
						// A fight on the road leaves the Cleric short: rest before the next section, not at the end of a chain.
						if (session.Api.World.CurrentHp * 100 < session.Api.World.MaxHp * 80) await RestSafelyAsync(token);
					}
					bool reached = stopAt?.Invoke(session.CurrentPosition) ?? Distance(session.CurrentPosition, goal) <= within;
					session.TraceDiagnostic($"altgard-{altgardLegId}-road", new Dictionary<string, object?>
					{
						["purpose"] = purpose, ["points"] = road.Count, ["position"] = session.CurrentPosition,
						["miss"] = Distance(session.CurrentPosition, goal), ["reached"] = reached,
					});
					return reached;
				}

				// AB-08: one contract talk step: approach the NPC (flying for a flight step), talk, and follow a walking NPC.
				float TalkRange(int npcId) => runtime.Data.NpcDataDh.GetNpcTemplate(npcId)?.GetTalkDistance() ?? 3;

				async Task TravelDestinyMapAsync(int destination)
				{
					if (session.Api.World.MapId == destination) return;
					if (destination == leg.Hub.MapId)
					{
						// ND-02 proved Doman's ordinary fallback when the learned Return is cooling down.
						if (session.Api.World.MapId != 120010000 || session.Api.Timing.TimeUntilCast(243) <= TimeSpan.FromSeconds(5))
						{
							await UseLearnedReturnToBindAsync();
							EnterLegMap(newEntry: true);
							return;
						}
					}
					else if (session.Api.World.MapId is 220010000 or 320070000)
					{
						await UseLearnedReturnToBindAsync();
						EnterLegMap(newEntry: true);
					}
					NaturalAltgardMapTrip trip = leg.MapTripList.Single(entry => entry.MapId == destination);
					if (trip.FromMapId is int source && session.Api.World.MapId != source) await TravelDestinyMapAsync(source);
					if (trip.FromMapId is int required && session.Api.World.MapId != required) return; // Re-plan after a road death.
					int revives = combat.ReviveCount;
					int teleporter = await ApproachShippedSpawnAsync(trip.TeleporterNpcId, withinRange: trip.TalkRange);
					if (combat.ReviveCount != revives) return;
					NaturalServiceOutcome result = await new NaturalServiceSteps(session).TeleportAsync(teleporter,
						session.Api.World.Objects[teleporter].Position, trip.TalkRange, trip.LocationId, trip.Fare, trip.MapId, token);
					Require.True(result.IsDone, result.Reason);
					EnterLegMap(newEntry: true);
				}

				async Task<bool> FlyDestinyHubAsync(int transporterId)
				{
					if (session.Api.World.VisibleEffects?.FirstOrDefault(effect => effect.SkillId == 8291) is { } sickness)
					{
						Require.True(sickness.RemainingMillis > 0, "Soul Sickness has no observed expiry for the hub flight wait.");
						long until = runtime.NowMillis + sickness.RemainingMillis + 1000L;
						while (NaturalAltgardQuestSteps.HasEffect(session.Api.World, 8291) && runtime.NowMillis < until)
						{
							await session.AdvanceAsync(TimeSpan.FromMilliseconds(Math.Min(1000, until - runtime.NowMillis)), token);
							await session.SynchronizeAsync(token);
							if (session.Api.World.IsDead) { await RestSafelyAsync(token); return false; }
						}
						Require.True(!NaturalAltgardQuestSteps.HasEffect(session.Api.World, 8291), "Soul Sickness did not expire before the hub flight.");
					}
					NaturalAirlineRoute route = airlines.Single(entry => entry.MapId == 220010000 && entry.NpcId == transporterId);
					int revives = combat.ReviveCount;
					if (Distance(session.CurrentPosition, route.Departure) > 40 &&
						!await WalkRoadDefendingAsync(route.Departure, "destiny-airline-road", within: 30)) return false;
					int transporter = await ApproachShippedSpawnAsync(transporterId, withinRange: 6);
					if (combat.ReviveCount != revives || session.Api.World.MapId != route.MapId) return false;
					NaturalNavigationResult pad = await NaturalIshalgenNavigator.ExploreAnchorAsync(route.MapId, -1,
						route.Departure, navigator, "destiny-flight-pad", token);
					Require.True(pad.Arrived, pad.Reason);
					NaturalServiceOutcome result = await new NaturalServiceSteps(session).FlyAsync(transporter,
						session.Api.World.Objects[transporter].Position, 6, route, token);
					Require.True(result.IsDone, result.Reason);
					session.TraceDiagnostic("destiny-hub-flight", new Dictionary<string, object?>
					{
						["npc"] = transporterId, ["route"] = route.Route, ["position"] = session.CurrentPosition,
					});
					return true;
				}

				async Task<int> ApproachDestinyNpcAsync(NaturalAltgardStep step)
				{
					int map = leg.StepMap(step), revives = combat.ReviveCount;
					BotPosition at = new(step.Position[0], step.Position[1], step.Position[2], 0);
					Require.Equal(map, session.Api.World.MapId);
					if (map == 220010000)
					{
						NaturalAirlineRoute alder = airlines.Single(route => route.MapId == map && route.NpcId == 203513);
						// The prison-to-Urd ground graph is 1,947 m: force the proved Anturoon/Aldelle flight.
						if (step.NpcId == 790003 && Distance(session.CurrentPosition, at) > 200)
						{
							if (!await FlyDestinyHubAsync(203545)) return 0;
						}
						else if (step.NpcId is 203546 or 203550 && Distance(session.CurrentPosition, alder.Departure) < 200)
						{
							if (!await FlyDestinyHubAsync(203513)) return 0;
						}
					}
					if (Distance(session.CurrentPosition, at) > 40 &&
						!await WalkRoadDefendingAsync(at, $"destiny-road-{step.Key}", within: 30)) return 0;
					if (combat.ReviveCount != revives || session.Api.World.MapId != map) return 0;
					NaturalNavigationResult reached = await NaturalIshalgenNavigator.ExploreWithinRangeAsync(map, step.NpcId,
						at, step.TalkRange, navigator, "destiny-recipient", token);
					Require.True(reached.Arrived, $"{step.Key}: {reached.Reason}");
					return reached.TargetObjectId ?? await session.WaitForNpcAsync(step.NpcId, token);
				}

				async Task RecoverDestinyAsync(string reason)
				{
					NaturalAltgardDestiny destiny = leg.Destiny!;
					session.TraceDiagnostic("destiny-attempt-lost", new Dictionary<string, object?>
					{
						["reason"] = reason, ["quest"] = NaturalAltgardQuestSteps.State(session.Api.World, destiny.QuestId),
						["map"] = session.Api.World.MapId, ["position"] = session.CurrentPosition, ["deaths"] = combat.ReviveCount,
					});
					if (session.Api.World.MapId == destiny.MapId) await TravelDestinyMapAsync(leg.Hub.MapId);
					await session.SynchronizeAsync(token);
					Require.Equal((byte)3, NaturalAltgardQuestSteps.State(session.Api.World, destiny.QuestId)?.Status);
					// Ordinary defense while Return is interrupted can earn real kill credit. Preserve that progress.
					Require.True(NaturalAltgardQuestSteps.State(session.Api.World, destiny.QuestId)?.Var is var observed &&
						(observed == destiny.ResetVar || observed == destiny.KillVar), "Lost attempt neither reset nor earned kill credit.");
					Require.Equal(0L, ItemCount(session.Api.World, destiny.StoneItemId));
					Require.True(!session.Api.World.Skills.ContainsKey(destiny.StigmaSkillId), "Lost attempt retained the tutorial skill.");
					destinySpawnedAt = null;
				}

				async Task FightDestinyAsync()
				{
					NaturalAltgardDestiny destiny = leg.Destiny!;
					int? enemy = session.Api.World.Objects.Values.FirstOrDefault(npc => npc.TemplateId == destiny.EnemyNpcId && !npc.IsCorpse)?.ObjectId;
					if (enemy == null || destinySpawnedAt is long spawn && runtime.NowMillis - spawn >= destiny.LifetimeSeconds * 1000L)
					{
						await RecoverDestinyAsync("five-minute-window-missed");
						return;
					}
					long started = runtime.NowMillis;
					int revives = combat.ReviveCount, retreats = combat.CompletedRetreats;
					bool killed = await combat.TryKillAsync(enemy.Value, token, session.CurrentPosition,
						Math.Max(0, session.PacketHistory.Count - 400));
					session.TraceDiagnostic("destiny-combat-outcome", new Dictionary<string, object?>
					{
						["killed"] = killed, ["deaths"] = combat.ReviveCount - revives, ["retreats"] = combat.CompletedRetreats - retreats,
						["durationMillis"] = runtime.NowMillis - started, ["target"] = enemy.Value,
					});
					if (NaturalAltgardQuestSteps.State(session.Api.World, destiny.QuestId) is (3, 9))
					{
						await session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token, packet => packet.Get<int>("worldId") == destiny.KillTeleport.MapId);
						await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
						session.AcceptTeleportPosition();
						await session.SynchronizeAsync(token);
						EnterLegMap(newEntry: true);
						Require.True(!session.Api.World.Objects.ContainsKey(enemy.Value) && !session.Api.World.LootStatuses.ContainsKey(enemy.Value), "Old Hellion survived the kill teleport.");
						Require.Equal(0L, ItemCount(session.Api.World, destiny.StoneItemId));
						Require.True(!session.Api.World.Skills.ContainsKey(destiny.StigmaSkillId), "Kill exit retained the tutorial skill.");
						destinySpawnedAt = null;
					}
					else if (session.Api.World.MapId == destiny.MapId) await RecoverDestinyAsync(killed ? "missing-kill-credit" : "retreat-or-expired-target");
				}

				async Task VisitLeg5CapitalAsync()
				{
					// AK-Q4 (a): the Pandaemonium cube expansions, with the Cleric's own kinah. Walk to the fortress teleporter,
					// travel, buy each level as Java CubeExpandService.expandCube offers it (EXTEND_INVENTORY, then the
					// STR_WAREHOUSE_EXPAND_WARNING question with the price). RC-04 batches the city quests,
					// Doman/Arekedil return and Basfelt flight; historical runs retain their learned Return.
					await EnsureOnGroundAsync();
					NaturalAltgardCubeExpansion cube = leg.CubeExpansion!;
					BotWorldModel world = session.Api.World;
					var steps = new NaturalServiceSteps(session);
					// The fortress is 1.1 km from Basfelt, past the navigator's segment budget (smoke run 5): take the travel
					// planner's road to the teleporter's square, then approach it.
					BotPosition teleporterSpawn = graph.GetMap(contract.MapId)!.Waypoints
						.First(waypoint => waypoint.TemplateId == cube.TeleporterNpcId).Position;
					if (!await FlyTowardAsync(teleporterSpawn))
						await WalkRoadDefendingAsync(teleporterSpawn, "cube-teleporter-road", within: 15);
					int teleporter = await ApproachShippedSpawnAsync(cube.TeleporterNpcId);
					NaturalServiceOutcome travelled = await steps.TeleportAsync(teleporter, world.Objects[teleporter].Position,
						cube.TeleporterTalkRange, cube.LocationId, cube.Fare, cube.MapId, token);
					Require.True(travelled.IsDone, travelled.Reason);
					NaturalJourneyNavigator city = mapNavigators.Enter(NaturalMapKey.Observe(world));
					NaturalNavigationResult reached = await NaturalIshalgenNavigator.ApproachNpcAsync(cube.MapId, cube.ExpanderNpcId,
						new BotPosition(cube.ExpanderPosition[0], cube.ExpanderPosition[1], cube.ExpanderPosition[2], 0), city, token);
					Require.True(reached.Arrived, $"Cube expander {cube.ExpanderNpcId}: {reached.Reason}");
					int expander = Require.IsType<int>(reached.TargetObjectId);
					for (int level = world.CubeExpansion?.Npc ?? 0; level < cube.Levels && world.Kinah >= cube.Prices[level];
						level = world.CubeExpansion?.Npc ?? 0)
					{
						long before = world.Kinah;
						await NaturalDialogProtocol.OpenAsync(session, expander, token);
						await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == expander);
						await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(expander,
							checked((ushort)DialogAction.EXTEND_INVENTORY)), token);
						DecodedBotServerPacket question = await session.WaitForPacketAsync(typeof(SM_QUESTION_WINDOW), token,
							packet => packet.Get<int>("code") == SM_QUESTION_WINDOW.STR_WAREHOUSE_EXPAND_WARNING);
						await session.SendPacketAsync(GameClientPackets.QuestionResponse(question.Get<int>("code"), 1,
							question.Get<int>("senderId")), token);
						await session.WaitForPacketAsync(typeof(SM_CUBE_UPDATE), token);
						await session.SynchronizeAsync(token);
						await session.SendPacketAsync(session.Api.CloseDialog(expander), token);
						session.TraceDiagnostic("cube-expanded", new Dictionary<string, object?>
						{
							["expander"] = cube.ExpanderNpcId, ["npcExpansions"] = world.CubeExpansion?.Npc,
							["capacity"] = world.CubeExpansion?.Capacity, ["kinahBefore"] = before, ["kinahAfter"] = world.Kinah,
						});
						Require.True((world.CubeExpansion?.Npc ?? 0) == level + 1 && before - world.Kinah == cube.Prices[level],
							$"Cube expansion {level + 1} not observed (level {world.CubeExpansion?.Npc}, Kinah {before} -> {world.Kinah}).");
					}
					if (laterCapital != null && altgardLegId == "l5")
					{
						await NaturalLaterCapitalSteps.PrepareLeg5CityAsync(session, PlayLaterCapitalStepAsync,
							runtime.Data.ItemDataDh.GetItemTemplate(182207009), token);
						await PrepareLaterCapitalBookAsync();
						int doman = await ApproachCapitalNpcAsync(204191);
						NaturalServiceOutcome returned = await steps.TeleportAsync(doman, world.Objects[doman].Position,
							5, 9, 500, leg.Hub.MapId, token);
						Require.True(returned.IsDone, returned.Reason);
						EnterLegMap();
						await NaturalLaterCapitalSteps.CompleteMaternalReturnAsync(session, PlayLaterCapitalStepAsync);
						Require.True(await FlyTowardAsync(ground), "Leg 5 city preparation needs the fortress-to-Basfelt hub flight.");
					}
					else
					{
						await UseLearnedReturnToBindAsync();
						navigator = mapNavigators.Enter(NaturalMapKey.Observe(world));
					}
					Require.Equal(leg.Hub.MapId, world.MapId ?? 0);
				}

				void EnterLegMap(bool newEntry = false)
				{
					NaturalMapKey key = LegMapKey();
					if (!newEntry && mapNavigators.Current == key && contract.MapId == key.MapId) return;
					var defend = navigator.DefendOnAttackAsync;
					navigator = mapNavigators.Enter(key, newEntry);
					navigator.DefendOnAttackAsync = defend;
					navigator.AvoidHostileAggro = true;
					geometry = runtime.CreateGeometry();
					contract = contract with { MapId = key.MapId };
					combat.EnterMap(navigator, geometry, key.MapId);
					navigator.AvoidSpots = combat.DeathSpots;
				}

				NaturalMapKey LegMapKey() => leg.Haramel?.MapId == session.Api.World.MapId
					? new(session.Api.World.MapId!.Value, (session.Api.World.InstanceId ?? throw new InvalidDataException("No actual Haramel copy observed.")) - 1)
					: NaturalMapKey.Observe(session.Api.World);
				void SaveHaramelProgress()
				{
					RecordHaramelDeadHints();
					haramelProgress = haramelProgress! with { Revives = combat.ReviveCount, StallBudget = progress.State, LastObservedAtMillis = HaramelNow() };
					haramelProgress.Write(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "haramel-progress.json"));
				}
				async Task EnterHaramelAsync()
				{
					NaturalHaramel rules = leg.Haramel!;
					BotPosition entrance = new(rules.PortalPosition[0], rules.PortalPosition[1], rules.PortalPosition[2], 0);
					await ReachPillarLevelAsync(entrance);
					while (!await WalkHaramelAsync(entrance, MathF.Min(5, TalkRange(rules.PortalNpcId)))) await RestSafelyAsync(token);
					int portal = await session.WaitForNpcAsync(rules.PortalNpcId, token);
					int start = session.PacketHistory.Count;
					haramelEntryHistoryStart = start;
					Require.True(await NaturalAltgardQuestSteps.UseObjectAsync(session, portal, null, token, reloadWorld: true), "Haramel entrance use interrupted.");
					await AcceptHaramelTransitionAsync(changedMap: true, start);
					await ObserveHaramelEntryAsync();
				}
				async Task AcceptHaramelTransitionAsync(bool changedMap, int start)
				{
					Type arrival = changedMap ? typeof(SM_PLAYER_SPAWN) : typeof(SM_CHANNEL_INFO);
					if (!session.PacketHistory.Skip(start).Any(packet => packet.PacketType == arrival)) await session.WaitForPacketAsync(arrival, token);
					if (!session.PacketHistory.Skip(start).Any(packet => packet.PacketType == typeof(SM_PLAYER_INFO) && packet.Get<int>("objectId") == session.CharacterId))
						await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
					session.AcceptTeleportPosition();
					await session.SynchronizeAsync(token);
					EnterLegMap(newEntry: true);
				}
				async Task ObserveHaramelEntryAsync()
				{
					NaturalHaramel rules = leg.Haramel!;
					int anchor = await ApproachShippedSpawnAsync(rules.AnchorNpcId, withinRange: 5);
					BotInstanceEntry entry = session.Api.World.InstanceEntries[(session.CharacterId, rules.CooldownId)];
					NaturalHaramelVisit? previousVisit = haramelProgress!.CurrentVisit;
					bool fresh = previousVisit == null || previousVisit.AnchorObjectId != anchor ||
						previousVisit.EntriesUsed != entry.EntriesUsed || haramelProgress.NeedsInstanceObservation &&
						session.Api.World.Objects.Values.Any(n => n.TemplateId == 216897 && !n.IsCorpse);
					if (fresh) { haramelHints.Clear(); haramelKillHistoryStart = haramelEntryHistoryStart; }
					haramelProgress = haramelProgress.ObserveEntry(session.Api.World.InstanceId!.Value, anchor, entry, HaramelNow(),
						session.Api.World.CompletedQuestIds.Contains(28507), fresh);
					SaveHaramelProgress();
					session.TraceDiagnostic("haramel-entry-receipt", new Dictionary<string, object?> { ["visit"] = haramelProgress.CurrentVisit, ["fresh"] = fresh });
				}
				async Task LeaveHaramelAsync()
				{
					NaturalHaramel rules = leg.Haramel!;
					int exitId = haramelProgress!.CurrentVisit?.BossMovieObserved == true ? rules.BossExitNpcId : rules.EntryExitNpcId;
					int exit = await ApproachShippedSpawnAsync(exitId, withinRange: TalkRange(exitId));
					int start = session.PacketHistory.Count;
					Require.True(await NaturalAltgardQuestSteps.UseObjectAsync(session, exit, null, token, reloadWorld: true), "Haramel exit use interrupted.");
					await AcceptHaramelTransitionAsync(changedMap: true, start);
					haramelProgress = haramelProgress.ObserveExit(HaramelNow(), rules);
					SaveHaramelProgress();
				}
				async Task ObserveHaramelBossAsync()
				{
					if (session.Api.World.MapId != leg.Haramel!.MapId) return;
					await NaturalMovieGate.FinishAsync(session, token);
					bool movie = session.PacketHistory.Skip(haramelKillHistoryStart).Any(packet => packet.PacketType == typeof(SM_PLAY_MOVIE) &&
						packet.Get<int>("cutsceneId") == leg.Haramel.BossMovieId) &&
						session.PacketHistory.Skip(haramelKillHistoryStart).Any(packet => packet.PacketType == typeof(SM_NPC_INFO) &&
							packet.Get<int>("npcId") == leg.Haramel.BossExitNpcId);
					haramelProgress = haramelProgress!.ObserveBoss(movie, false, HaramelNow());
					SaveHaramelProgress();
				}
				async Task LootHaramelChestAsync()
				{
					int chest = await ApproachShippedSpawnAsync(leg.Haramel!.ChestNpcId, withinRange: TalkRange(leg.Haramel.ChestNpcId));
					int chestStart = session.PacketHistory.Count;
					Require.True(await NaturalAltgardQuestSteps.UseObjectAsync(session, chest, null, token), "Haramel class chest use interrupted.");
					await session.SynchronizeAsync(token);
					Require.True(session.PacketHistory.Skip(chestStart).Any(packet => packet.PacketType == typeof(SM_LOOT_ITEMLIST) &&
						packet.Get<int>("targetObjectId") == chest), "Haramel class chest did not produce its actual loot list.");
					BotLootItem[] offered = session.Api.World.Loot?.Items.ToArray() ?? [];
					foreach (BotLootItem item in offered)
					{
						long before = ItemCount(session.Api.World, item.ItemId);
						await session.AdvanceAsync(TimeSpan.FromMilliseconds(450), token);
						await session.SendPacketAsync(session.Api.Loot(chest, item.Index), token);
						await session.SynchronizeAsync(token);
						session.TraceDiagnostic("haramel-class-chest-loot", new Dictionary<string, object?> { ["item"] = item, ["before"] = before, ["after"] = ItemCount(session.Api.World, item.ItemId) });
					}
					await session.SendPacketAsync(session.Api.Loot(chest, close: true), token);
					haramelProgress = haramelProgress!.ObserveBoss(true, true, HaramelNow());
					SaveHaramelProgress();
				}
				async Task MakeHaramelSoupAsync()
				{
					int cauldron = await ApproachShippedSpawnAsync(730359, withinRange: 5);
					await NaturalDialogProtocol.OpenAsync(session, cauldron, token);
					if (haramelProgress!.SoupPayment == null)
					{
						long before = ItemCount(session.Api.World, 182212022);
						await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(cauldron, DialogAction.CHECK_USER_HAS_QUEST_ITEM, questId: 28511), token);
						await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == cauldron && packet.Get<ushort>("dialogPageId") == 1352);
						await session.SynchronizeAsync(token);
						haramelProgress = haramelProgress.ObserveSoupPayment(cauldron, before, ItemCount(session.Api.World, 182212022), 1352, HaramelNow());
						SaveHaramelProgress(); // Persist payment before the item-give action.
					}
					if (ItemCount(session.Api.World, 182212023) == 0 && QuestStatus(28511) == 3)
						await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(cauldron, DialogAction.SETPRO2, questId: 28511), token);
					await session.SynchronizeAsync(token);
					Require.Equal(1L, ItemCount(session.Api.World, 182212023));
					Require.Equal(4, QuestStatus(28511));
					SaveHaramelProgress();
				}
				async Task CollectHaramelKeysAsync()
				{
					foreach (NaturalHaramelKey key in leg.Haramel!.TowerChestKeys!)
						while (ItemCount(session.Api.World, key.ItemId) < key.Count)
						{
							int npc = await KillShippedSpawnAsync(key.NpcId, new(QuestRunOperationKind.CollectQuestDrop, ItemId: key.ItemId, Count: key.Count));
							await TryLootCorpseItemAsync(session, npc, key.ItemId, token);
							await RestSafelyAsync(token);
						}
				}
				int HaramelKind(IReadOnlyList<int> kinds)
				{
					RecordHaramelDeadHints();
					NaturalNavigationObject? live = navigator.Observe().Npcs.Where(n => kinds.Contains(n.TemplateId))
						.OrderByDescending(n => Distance(session.CurrentPosition, n.Position) <= NaturalPullPlanner.SpellRange + 3 &&
							geometry.HasLineOfSight(leg.Haramel!.MapId, session.CurrentPosition, n.Position))
						.ThenByDescending(n => geometry.OnSameIsland(leg.Haramel!.MapId, session.CurrentPosition, n.Position))
						.ThenBy(n => Distance(session.CurrentPosition, n.Position)).FirstOrDefault();
					if (live != null) return live.TemplateId;
					int kind = graph.GetMap(leg.Haramel!.MapId)!.Waypoints.Where(w => w.TemplateId is int id && kinds.Contains(id) && !haramelHints.Contains((id, w.Position)))
						.OrderByDescending(w => geometry.OnSameIsland(leg.Haramel.MapId, session.CurrentPosition, w.Position))
						.ThenBy(w => w.TemplateId == leg.Haramel.BossNpcId).ThenBy(w => Distance(session.CurrentPosition, w.Position))
						.Select(w => w.TemplateId!.Value).FirstOrDefault();
					return kind > 0 ? kind : throw new NaturalHaramelSourcesExhaustedException("All original qualifying hints in this copy are exhausted.");
				}
				async Task<int> ApproachHaramelAsync(int templateId, float range, Func<int?>? completed)
				{
					NaturalHaramel rules = leg.Haramel!;
					for (int search = 0; search < 100; search++)
					{
						RecordHaramelDeadHints();
						if (completed?.Invoke() is int done) return done;
						if (session.Api.World.IsDead) await RestSafelyAsync(token);
						if (session.Api.World.MapId != rules.MapId) await EnterHaramelAsync();
						NaturalNavigationObject? live = navigator.Observe().Npcs.Where(n => n.TemplateId == templateId)
							.OrderByDescending(n => geometry.OnSameIsland(rules.MapId, session.CurrentPosition, n.Position)).ThenBy(n => Distance(session.CurrentPosition, n.Position)).FirstOrDefault();
						BotWaypoint? hint = graph.GetMap(rules.MapId)!.Waypoints.Where(w => w.TemplateId == templateId && !haramelHints.Contains((templateId, w.Position)))
							.OrderByDescending(w => geometry.OnSameIsland(rules.MapId, session.CurrentPosition, w.Position)).ThenBy(w => Distance(session.CurrentPosition, w.Position)).FirstOrDefault();
						// Handler-spawned chest/exit have no static waypoint. Their actual spawn packet remains
						// an observed location after the bot walks out of sight, scoped to this copy's entry.
						DecodedBotServerPacket? seen = hint == null ? session.PacketHistory.Skip(haramelKillHistoryStart).LastOrDefault(packet =>
							packet.PacketType == typeof(SM_NPC_INFO) && packet.Get<int>("npcId") == templateId &&
							!navigator.UnavailableObjects.Contains(packet.Get<int>("objectId"))) : null;
						if (live == null && hint == null && seen == null) throw new NaturalHaramelSourcesExhaustedException($"Haramel source {templateId} is exhausted in this copy.");
						BotPosition at = live?.Position ?? hint?.Position ?? new(seen!.Get<float>("x"), seen.Get<float>("y"), seen.Get<float>("z"), seen.Get<byte>("heading"));
						session.TraceDiagnostic("haramel-source-search", new Dictionary<string, object?>
						{ ["template"] = templateId, ["search"] = search, ["object"] = live?.ObjectId, ["from"] = session.CurrentPosition, ["to"] = at, ["range"] = range });
						await ReachHaramelFloorAsync(at, templateId);
						if (session.Api.World.MapId != rules.MapId || session.Api.World.IsDead) continue;
						try { if (!await WalkHaramelAsync(at, range, live?.ObjectId)) continue; }
						catch (NaturalHaramelGroundApproachUnavailableException missing)
						{
							if (live != null) navigator.UnavailableObjects.Add(live.ObjectId);
							BotWaypoint? unavailable = graph.GetMap(rules.MapId)!.Waypoints.Where(w => w.TemplateId == templateId)
								.OrderBy(w => Distance(w.Position, at)).FirstOrDefault();
							if (unavailable != null) haramelHints.Add((templateId, unavailable.Position));
							session.TraceDiagnostic("haramel-source-unreachable", new Dictionary<string, object?>
							{ ["template"] = templateId, ["object"] = live?.ObjectId, ["position"] = at, ["reason"] = missing.Message });
							continue;
						}
						if (completed?.Invoke() is int collected) return collected;
						NaturalNavigationObject? arrived = navigator.Observe().Npcs.Where(n => n.TemplateId == templateId && Distance(session.CurrentPosition, n.Position) <= range + 1)
							.OrderBy(n => Distance(session.CurrentPosition, n.Position)).FirstOrDefault();
						if (arrived != null) return arrived.ObjectId;
						if (hint != null) haramelHints.Add((templateId, hint.Position));
					}
					throw new InvalidDataException($"Haramel source {templateId} exceeded its bounded observation search.");
				}
				async Task ReachHaramelFloorAsync(BotPosition at, int templateId)
				{
					int map = leg.Haramel!.MapId;
					BotPosition tower = new(231.031f, 223.091f, 139.82f, 0);
					bool TowerFloor(BotPosition p) => p.Z > 115 && p.X is > 195 and < 250 && p.Y is > 190 and < 250;
					bool onTower = TowerFloor(session.CurrentPosition), onOffice = session.CurrentPosition.Z > 120 && session.CurrentPosition.Y >= 260;
					// Some shipped workers stand above their floor's mesh snap (216913 is at 142.766).
					// Their audited room and elevation still require the same ordinary lift/elevator.
					bool targetTower = TowerFloor(at), targetOffice = at.Z > 120 && at.Y >= 260;
					async Task FinishTowerAscentAsync()
					{
						for (int retry = 0; retry < 100 && session.Api.World.MapId == map && !session.Api.World.IsDead; retry++)
							if (await WalkHaramelAsync(tower, 3)) return;
						Require.True(session.Api.World.MapId != map || session.Api.World.IsDead, "Haramel tower ascent exceeded its guarded route budget.");
					}
					if (onTower && targetTower)
					{
						if (session.CurrentPosition.Z < 134) await FinishTowerAscentAsync();
						return;
					}
					if (onOffice && targetOffice || geometry.NavMesh!.FindInteractionPath(map, session.CurrentPosition, at).Count > 0) return;
					if (onTower && session.CurrentPosition.Z < 134) await FinishTowerAscentAsync();
					if (onTower) await haramelTravel.GlideToLowerFloorAsync(token);
					else if (onOffice) await haramelTravel.RideOfficeElevatorDownAsync(token);
					if (session.Api.World.MapId != map) return;
					if (templateId == 700853 || targetTower)
					{
						int lift = await ApproachHaramelAsync(leg.Haramel.LiftNpcId, 5, null);
						int start = session.PacketHistory.Count;
						await NaturalDialogProtocol.OpenAsync(session, lift, token);
						session.Api.World.BeginWorldReload();
						await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(lift, checked((ushort)leg.Haramel.LiftDialog)), token);
						await AcceptHaramelTransitionAsync(changedMap: false, start);
						await FinishTowerAscentAsync();
					}
					else if (targetOffice) await haramelTravel.RideOfficeElevatorAsync(token);
				}
				async Task<bool> WalkHaramelAsync(BotPosition target, float range, int? objective = null)
				{
					int map = session.Api.World.MapId!.Value, revives = combat.ReviveCount;
					if (Distance(session.CurrentPosition, target) <= range && (range <= 5 || geometry.HasLineOfSight(map, session.CurrentPosition, target))) return true;
					BotPosition from = session.CurrentPosition;
					IReadOnlyList<BotPosition> route = range <= 1 ? geometry.NavMesh!.FindPath(map, from, target)
						: range <= 5 ? geometry.NavMesh!.FindInteractionPath(map, from, target) : [];
					if (route.Count > 0 && Distance(route[^1], target) > range) route = [];
					IEnumerable<BotPosition> InteractionGround()
					{
						BotNavMesh mesh = geometry.NavMesh!.NavMeshes.Get(map)!;
						// HM-02: a portal can block its own tiny island. The adjacent floor is an ordinary
						// dialog approach; it does not need a sight ray through the portal's placeable model.
						foreach (float radius in new[] { MathF.Max(1, range - 1), MathF.Max(1, range - 2), 1f })
							for (int sector = 0; sector < 16; sector++)
							{
								float angle = sector * MathF.PI / 8;
								BotPosition sample = target with { X = target.X + radius * MathF.Cos(angle), Y = target.Y + radius * MathF.Sin(angle) };
								if (mesh.Snap(sample, BotNavQuery.Default with { SnapHorizontal = 1, SnapVertical = 3 }) is BotPosition ground &&
									Distance(ground, target) <= range - .5f) yield return ground;
							}
					}
					// Ranged targets can also occupy a tiny isolated mesh island (the office cages).
					// Keep checked firing points on any reachable neighbouring floor, as for portal dialogs.
					BotPosition[] approaches = new[] { target }.Concat(InteractionGround()).Concat(range > 5 ?
						geometry.GroundAround(map, target, [MathF.Max(1, range - 1), MathF.Max(1, range - 2), 3f, 2f, 1f]) : [])
						.Where(p => Distance(p, target) <= range - MathF.Min(.5f, range / 2) && (range <= 5 || geometry.HasLineOfSight(map, p, target)))
						.OrderBy(p => Distance(from, p)).Distinct().ToArray();
					BotTravelPlanner? planner = BotTravelPlanner.For(map, geometry, runtime.Data);
					foreach (BotPosition point in approaches)
					{
						if (route.Count > 0) break;
						route = range > 1 ? planner?.PlanJourney(map, from, point, session.Api.World.Level, [])?.Route ??
							geometry.NavMesh!.FindPath(map, from, point) : geometry.NavMesh!.FindPath(map, from, point);
					}
					if (route.Count == 0 && approaches.Length > 0) route = geometry.FindJourneyPath(map, from, approaches[0]);
					if (route.Count == 0 && range > 5) throw new NaturalHaramelGroundApproachUnavailableException($"No checked firing approach {from} -> {target}, range {range}.");
					Require.True(route.Count > 0, $"No checked Haramel ground route {from} -> {target}, range {range}.");
					session.TraceDiagnostic("haramel-ground-route", new Dictionary<string, object?>
					{ ["map"] = map, ["from"] = from, ["target"] = target, ["range"] = range, ["points"] = route.Count, ["last"] = route[^1] });
					for (int index = 0; index < route.Count; index += 8)
					{
						BotPosition[] segment = route.Skip(index).Take(8).ToArray();
						if (!navigator.IsSegmentSafe(segment, objective))
						{
							BotPosition before = session.CurrentPosition;
							int killsBefore = navigator.UnavailableObjects.Count;
							float Aggro(int id)
							{
								var npc = runtime.Data.NpcDataDh.GetNpcTemplate(id);
								return runtime.IsAggressive(npc) ? npc.GetAggroRange() + 1 : 0;
							}
							NaturalNavigationObject? guard = NaturalGuardedObjectivePolicy.SelectBlockerOnRoute(before, segment,
								navigator.Observe().Npcs, Aggro, objective);
							bool cleared = guard != null && await PullAndKillAsync(guard.ObjectId, "haramel-route-guard");
							if (cleared) navigator.UnavailableObjects.Add(guard!.ObjectId);
							if (!cleared && navigator.UnavailableObjects.Count == killsBefore && Distance(before, session.CurrentPosition) < 2)
								cleared = await TryClearObservedBlockerAsync(segment[^1], objective);
							if (combat.ReviveCount != revives || session.Api.World.MapId != map) return false;
							Require.True(cleared || navigator.UnavailableObjects.Count > killsBefore || Distance(before, session.CurrentPosition) >= 2,
								"Haramel ground route remains guarded.");
							return false; // Fight/approach moved us: rebuild the route instead of walking stale points behind us.
						}
						if (Distance(session.CurrentPosition, segment[^1]) > .25f) await navigator.MoveAsync(segment, token);
						await session.SynchronizeAsync(token);
						await DefendAgainstEngagedAsync("haramel-ground");
						if (combat.ReviveCount != revives || session.Api.World.MapId != map) return false;
					}
					return Distance(session.CurrentPosition, target) <= range + 1;
				}
				void RecordHaramelDeadHints()
				{
					if (leg.Haramel == null || session.Api.World.MapId != leg.Haramel.MapId) return;
					var killed = session.PacketHistory.Skip(haramelKillHistoryStart).Where(packet => packet.PacketType == typeof(SmAttackStatus) &&
						packet.Get<byte>("typeId") is not (19 or 20 or 21 or 22 or 23) && packet.Get<byte>("hpOrMp") == 0)
						.Select(packet => packet.Get<int>("objectId")).ToHashSet();
					foreach (int deadObject in killed) navigator.UnavailableObjects.Add(deadObject);
					foreach (DecodedBotServerPacket spawn in session.PacketHistory.Skip(haramelKillHistoryStart).Where(packet =>
						packet.PacketType == typeof(SM_NPC_INFO) && (killed.Contains(packet.Get<int>("objectId")) ||
							session.Api.World.Objects.GetValueOrDefault(packet.Get<int>("objectId"))?.IsCorpse == true)))
					{
						int id = spawn.Get<int>("npcId");
						BotPosition at = new(spawn.Get<float>("x"), spawn.Get<float>("y"), spawn.Get<float>("z"), 0);
						BotWaypoint? hint = graph.GetMap(leg.Haramel.MapId)!.Waypoints.Where(w => w.TemplateId == id).OrderBy(w => Distance(w.Position, at)).FirstOrDefault();
						if (hint != null) haramelHints.Add((id, hint.Position));
					}
				}

				async Task UseInstancePortalAsync(int objectId, int duration, int destination)
				{
					await NaturalDialogProtocol.OpenAsync(session, objectId, token);
					DecodedBotServerPacket started = await session.WaitForPacketAsync(typeof(SM_USE_OBJECT), token,
						packet => packet.Get<int>("targetObjectId") == objectId && packet.Get<byte>("actionType") == 1);
					Require.Equal(duration, started.Get<int>("durationMs"));
					session.Api.World.BeginWorldReload();
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(duration + 1), token);
					await session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token, packet => packet.Get<int>("worldId") == destination);
					await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
					session.AcceptTeleportPosition();
					await session.SynchronizeAsync(token);
					EnterLegMap(newEntry: true);
					session.TraceDiagnostic("quest-instance-portal", new Dictionary<string, object?>
					{
						["object"] = objectId, ["duration"] = duration, ["map"] = session.Api.World.MapId, ["position"] = session.CurrentPosition,
					});
				}

				async Task PrepareRebirthAsync()
				{
					const ushort skillId = 4005;
					if (NaturalAltgardQuestSteps.HasEffect(session.Api.World, skillId)) return;
					Require.True(session.Api.World.Skills.TryGetValue(skillId, out BotSkill? learned), "Bregirun needs the observed learned Hand of Reincarnation.");
					await RestSafelyAsync(token);
					while (session.Api.Timing.TimeUntilCast(skillId) is { } remaining && remaining > TimeSpan.Zero)
					{
						await DefendAgainstEngagedAsync("rebirth-reuse");
						await session.AdvanceAsync(TimeSpan.FromMilliseconds(Math.Min(5000, remaining.TotalMilliseconds) + 1), token);
						await session.SynchronizeAsync(token);
					}
					await session.SendPacketAsync(session.Api.Target(session.CharacterId), token);
					await session.SendPacketAsync(session.Api.Cast(runtime.CreateSpellCast(session.Api.World, session.CurrentPosition,
						skillId, checked((byte)learned!.Level), session.CharacterId)), token);
					DecodedBotServerPacket started = await BotCastProtocol.WaitForStartAsync((predicate, waitToken) =>
						session.WaitForPacketAsync(packet => predicate(packet) || BotCastProtocol.IsStartRejection(packet), waitToken), session.CharacterId, skillId, token);
					Require.Equal(typeof(SM_CASTSPELL), started.PacketType);
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(started.Get<ushort>("castDuration") + 1), token);
					DecodedBotServerPacket result = await BotCastProtocol.WaitForCompletionAsync(session.WaitForPacketAsync, session.CharacterId, skillId, token);
					Require.Equal(typeof(SM_CASTSPELL_RESULT), result.PacketType);
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(result.Get<ushort>("hitTime") + 1), token);
					await session.SynchronizeAsync(token);
					Require.True(NaturalAltgardQuestSteps.HasEffect(session.Api.World, skillId), "Hand of Reincarnation did not produce its observed buff.");
					session.TraceDiagnostic("quest-instance-rebirth-prepared", new Dictionary<string, object?> { ["skill"] = skillId, ["level"] = learned.Level });
				}

				async Task CollectLeg9ClothingAsync()
				{
					for (int attempt = 1; attempt <= 6; attempt++)
					{
						EnterLegMap();
						await NaturalLaterCapitalSteps.RunMatchingStepsAsync(session, [NaturalLaterCapitalSteps.RobeHeart], PlayLaterCapitalStepAsync);
						if (NaturalAltgardQuestSteps.State(session.Api.World, 2916) is (3, 6) && ItemCount(session.Api.World, 182207007) == 1) break;
						Require.True(NaturalAltgardQuestSteps.State(session.Api.World, 2916) is (3, 5 or 6), "Banatisai must precede the clothing visit.");
						session.BeginStep("rc-robe-clothing", "approach-distance-trigger-and-loot");
						int clothing = await ApproachShippedSpawnAsync(700211, withinRange: 2);
						await session.SynchronizeAsync(token);
						if (session.Api.World.IsDead) { await RestSafelyAsync(token); continue; }
						Require.True(NaturalAltgardQuestSteps.State(session.Api.World, 2916) is (3, 6), "The actual five-metre clothing trigger was not observed.");
						bool collected = await NaturalAltgardQuestSteps.UseObjectAsync(session, clothing, 182207007, token);
						session.TraceDiagnostic("later-capital-clothing-use", new Dictionary<string, object?>
						{ ["objectId"] = clothing, ["attempt"] = attempt, ["collected"] = collected });
						await RestSafelyAsync(token);
					}
					Require.True(NaturalAltgardQuestSteps.State(session.Api.World, 2916) is (3, 6) && ItemCount(session.Api.World, 182207007) == 1,
						"The normal clothing loot must remain for Deyla's later city hand-in.");
					BotInventoryItem carried = session.Api.World.Inventory.Values.Single(i => i.ItemId == 182207007);
					session.TraceDiagnostic("later-capital-clothing-carried", new Dictionary<string, object?>
					{ ["objectId"] = carried.ObjectId, ["item"] = carried.ItemId, ["count"] = carried.Count, ["status"] = 3, ["var"] = 6 });
				}

				async Task PlayContractStepAsync(NaturalAltgardStep step)
				{
					// Java Q2252 sets REWARD/1 for the Spirit and REWARD/2 for Drakie.
					// The historic contract carries group 0; observe group 1's page on the alternate native spawn.
					if (step.QuestId == 2252 && step.ExpectedStatus == "REWARD" &&
						NaturalAltgardQuestSteps.State(session.Api.World, 2252) is (4, 2))
					{
						step = step with { Pages = [1352, 6] };
						session.TraceDiagnostic("minushan-reward-group", new Dictionary<string, object?>
						{ ["quest"] = 2252, ["var"] = 2, ["group"] = 1, ["page"] = 6 });
					}
					if (leg.Haramel != null && step.NpcId is 730306 or 730307)
					{
						int source = await ApproachShippedSpawnAsync(step.NpcId, withinRange: step.TalkRange);
						await NaturalHaramelQuestSteps.UseLeadInObjectAsync(session, step, source, token);
						return;
					}
					if (leg.Destiny is { } destiny)
					{
						if (step.Key == destiny.SpawnStep)
						{
							await RestSafelyAsync(token);
							if (NaturalAltgardQuestSteps.State(session.Api.World, destiny.QuestId) is not (3, 97)) return;
						}
						int recipient = await ApproachDestinyNpcAsync(step);
						if (recipient == 0 || session.Api.World.MapId != leg.StepMap(step) ||
							NaturalAltgardQuestSteps.State(session.Api.World, destiny.QuestId)?.Var != step.Var && step.ExpectedStatus == "START") return;
						string change = await NaturalAltgardQuestSteps.TalkAsync(session, step, recipient, token);
						if (step.Key == destiny.SpawnStep) destinySpawnedAt = runtime.NowMillis;
						EnterLegMap(newEntry: step.Teleport != null);
						session.TraceDiagnostic($"altgard-{altgardLegId}-step", new Dictionary<string, object?> { ["step"] = step.Key, ["change"] = change });
						return;
					}
					int npc;
					int stepMap = leg.StepMap(step);
					Require.Equal(stepMap, session.Api.World.MapId);
					if (laterCapital != null && stepMap == capitalContract.MapId)
						npc = await ApproachCapitalNpcAsync(step.NpcId);
					else if (stepMap != leg.Hub.MapId && leg.Haramel == null)
					{
						NaturalNavigationResult reached = await NaturalIshalgenNavigator.ExploreWithinRangeAsync(stepMap, step.NpcId,
							new BotPosition(step.Position[0], step.Position[1], step.Position[2], 0), step.TalkRange, navigator, "NPC", token);
						Require.True(reached.Arrived, $"{step.Key}: {reached.Reason}");
						npc = Require.IsType<int>(reached.TargetObjectId);
					}
					else if (step.Flight)
					{
						await FlyToAsync(Rock());
						npc = await session.WaitForNpcAsync(step.NpcId, token);
					}
					else
					{
						await EnsureOnGroundAsync();
						try { npc = await ApproachShippedSpawnAsync(step.NpcId); }
						catch (InvalidDataException exception)
						{
							// The navigator found no route from where the last fight ended (Q24112: off Sumarhon's height toward
							// Brodir). Take the travel planner's road to the NPC, as AB-02 walked it, and look again from there.
							session.TraceDiagnostic($"altgard-{altgardLegId}-talk-road", new Dictionary<string, object?>
							{
								["step"] = step.Key, ["reason"] = exception.Message, ["position"] = session.CurrentPosition,
							});
							BotPosition at = new(step.Position[0], step.Position[1], step.Position[2], 0);
							if (!await WalkRoadDefendingAsync(at, $"talk-road-{step.Key}") && !session.Api.World.IsDead)
							{
								// No road from this ground at all: Return to the hub's obelisk, as a player does (the bind policy,
								// AB-Q5), and walk from there.
								await UseLearnedReturnToBindAsync();
								await WalkRoadDefendingAsync(at, $"talk-road-{step.Key}-after-return");
							}
							npc = await ApproachShippedSpawnAsync(step.NpcId);
						}
					}
					for (int attempt = 1; ; attempt++)
					{
						try
						{
							string change = await NaturalAltgardQuestSteps.TalkAsync(session, step, npc, token);
							session.TraceDiagnostic($"altgard-{altgardLegId}-step", new Dictionary<string, object?> { ["step"] = step.Key, ["change"] = change });
							return;
						}
						catch (NaturalDialogTooFarException) when (attempt < 3 && !step.Flight) { await ReapproachForDialogAsync(npc); }
					}
				}

				// AB-08: open an NPC's dialog for a quest and send actions, waiting for each page (the steps that move no var:
				// Q2230's expired check and new chance, Lamir's refill incense).
				async Task DialogAsync(int npcId, int questId, (string Action, int? Page)[] actions,
					(byte Status, int Var)? expectedState = null)
				{
					await EnsureOnGroundAsync();
					int npc = await ApproachShippedSpawnAsync(npcId);
					for (int attempt = 1; ; attempt++)
					{
						// A defensive kill on the approach can finish the summoned objective. Lamir only
						// refills incense at START/1; return to the journal decision before sending a stale choice.
						if (expectedState != null && NaturalAltgardQuestSteps.State(session.Api.World, questId) != expectedState)
						{
							session.TraceDiagnostic("altgard-obsolete-dialog", new Dictionary<string, object?>
							{
								["quest"] = questId, ["npcId"] = npcId, ["expected"] = expectedState.ToString(),
								["observed"] = NaturalAltgardQuestSteps.State(session.Api.World, questId)?.ToString(),
							});
							return;
						}
						try
						{
							await NaturalDialogProtocol.OpenAsync(session, npc, token);
							await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == npc);
							break;
						}
						catch (NaturalDialogTooFarException) when (attempt < 3) { await ReapproachForDialogAsync(npc); }
					}
					foreach ((string action, int? page) in actions)
					{
						await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npc,
							checked((ushort)NaturalAscensionContract.DialogActionId(action)), questId: questId), token);
						if (page is int expected)
							await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet =>
								packet.Get<int>("targetObjectId") == npc && packet.Get<ushort>("dialogPageId") == expected);
					}
					await session.SynchronizeAsync(token);
					await session.SendPacketAsync(session.Api.CloseDialog(npc), token);
				}

				// AB-08: a timed quest (Q2288, Q2230) from its offer to its hand-in, on NaturalTimedQuestPolicy. The client sees each
				// timer's length in SM_QUEST_ACTION and the bot notes when it should end. A death ends the timer on the server (AB-Q6:
				// Q1044's and Q2042's hooks), so after a revive Q2230's timer counts as ended and Q2288's as gone.
				async Task RunTimedQuestAsync(int questId)
				{
					int preparedAttempt = -1;
					NaturalAltgardTimer timer = leg.TimerList.Single(entry => entry.QuestId == questId);
					NaturalAltgardQuest quest = leg.Quest(questId);
					NaturalAltgardHunt? hunt = leg.HuntList.FirstOrDefault(entry => entry.QuestId == questId);
					NaturalAltgardCollectedItem? item = leg.CollectionList.FirstOrDefault(entry => entry.QuestId == questId)?.Items.Single();
					NaturalAltgardStep offer = leg.StepsFor(questId).Single(step => step.ExpectedStatus == "OFFER");
					for (int round = 0; round < 300; round++)
					{
						await session.SynchronizeAsync(token);
						BotWorldModel world = session.Api.World;
						if (world.IsDead || world.CurrentHp <= 0)
						{
							await RestSafelyAsync(token);
							if (timedEnds.ContainsKey(questId)) timedEnds[questId] = timer.OnExpiry == "new-chance" ? runtime.NowMillis : long.MinValue;
							continue;
						}
						(byte Status, int Var)? state = NaturalAltgardQuestSteps.State(world, questId);
						bool completed = world.CompletedQuestIds.Contains(questId);
						string? status = completed ? "COMPLETE" : state switch { (3, _) => "START", (4, _) => "REWARD", null => null, _ => "OTHER" };
						int var = state?.Var ?? 0;
						if (status == null) timedEnds.Remove(questId); // abandoned: its timer is gone with it
						long? endsAt = timedEnds.TryGetValue(questId, out long ends) && ends != long.MinValue ? ends : null;
						int unitsLeft = hunt != null ? hunt.ToVar - Math.Max(var, hunt.FromVar) : item!.Count - (int)ItemCount(world, item.ItemId);
						double perKill = hunt != null ? 1 : 0.8;
						BotPosition giver = new(offer.Position[0], offer.Position[1], offer.Position[2], 0);
						bool ready = world.CurrentHp * 100 >= world.MaxHp * 90 && world.CurrentMp * 100 >= world.MaxMp * 80 && Engaged().Attackers.Length == 0;
						NaturalTimedChoice choice = NaturalTimedQuestPolicy.Decide(new NaturalTimedObservation(status, var, runtime.NowMillis, endsAt,
							Math.Max(unitsLeft, 0), perKill, 45, Distance(session.CurrentPosition, giver) / 6, timedAttempts.GetValueOrDefault(questId), ready), timer);
						session.TraceDiagnostic($"altgard-{altgardLegId}-timed", new Dictionary<string, object?>
						{
							["quest"] = questId, ["action"] = choice.Action, ["reason"] = choice.Reason, ["status"] = status, ["var"] = var,
							["slack"] = double.IsFinite(choice.SlackSeconds) ? choice.SlackSeconds : null, ["attempts"] = timedAttempts.GetValueOrDefault(questId),
							["unitsLeft"] = unitsLeft,
						});
						void TimerStarted()
						{
							timedAttempts[questId] = timedAttempts.GetValueOrDefault(questId) + 1;
							int seconds = session.Api.World.Quests.TryGetValue(questId, out BotQuestState? quest) && quest.TimerSeconds is int seen && seen > 0
								? seen : timer.Seconds;
							timedEnds[questId] = runtime.NowMillis + seconds * 1000L;
						}
						switch (choice.Action)
						{
							case "done":
								return;
							case "give-up":
								await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, $"altgard-{altgardLegId}-timed-{questId}-given-up.json"),
									System.Text.Json.JsonSerializer.Serialize(choice), token);
								throw new InvalidDataException($"Altgard leg {altgardLegId}: Q{questId}'s timer ran out {timedAttempts.GetValueOrDefault(questId)} times (AB-Q2).");
							case "take":
								if (questId == 2263 && preparedAttempt != timedAttempts.GetValueOrDefault(questId))
								{
									// AE-06: clear companions around the three nearest local pollen spots before accepting. Keep the
									// malodors for the collection; all clearing and travel use ordinary movement and combat.
									NaturalAltgardArea area = leg.Area(quest.Area!);
									await FlyTowardAsync(giver); // Q2253 leaves the Cleric at the Berth; return by its hub transporter.
									BotPosition[] spots = graph.GetMap(leg.Hub.MapId)!.Waypoints
										.Where(at => at.TemplateId is int id && item!.SourceNpcIds.Contains(id) && area.Contains(at.Position.X, at.Position.Y, at.Position.Z))
										.Select(at => at.Position).Distinct().OrderBy(at => Distance(at, giver)).Take(3).ToArray();
									int revives = combat.ReviveCount;
									foreach (BotPosition spot in spots)
									{
										if (Distance(session.CurrentPosition, spot) > 45)
											await WalkRoadDefendingAsync(spot, "pollen-preparation", stopAt: at => Distance(at, spot) <= 45);
										await ClearAroundSpotAsync(spot, null, "pollen-preparation", item!.SourceNpcIds);
										if (combat.ReviveCount != revives || session.Api.World.IsDead) break;
									}
									if (combat.ReviveCount != revives || session.Api.World.IsDead) continue;
									await ApproachShippedSpawnAsync(offer.NpcId);
									await RestSafelyAsync(token);
									preparedAttempt = timedAttempts.GetValueOrDefault(questId);
									continue; // re-observe HP/mana before acceptance starts the timer
								}
								await PlayContractStepAsync(offer);
								if (NaturalTimedQuestPolicy.TimerStartsAtAccept(timer)) TimerStarted();
								break;
							case "start-timer":
								await PlayContractStepAsync(leg.Steps.Single(step => step.Key == timer.StartStep));
								TimerStarted();
								break;
							case "hunt":
							{
								await EnsureOnGroundAsync();
								int[] kinds = hunt?.NpcIds ?? item!.SourceNpcIds;
								int target = await KillShippedSpawnAsync(NearestKind(kinds));
								if (item != null) await TryLootCorpseItemAsync(session, target, item.ItemId, token);
								navigator.UnavailableObjects.Add(target);
								break;
							}
							case "turn-in":
								await PlayContractStepAsync(leg.StepsFor(questId).Single(step => step.ExpectedStatus == "START" && step.Var == var &&
									step.Key != timer.StartStep));
								break;
							case "new-chance":
								await DialogAsync(quest.StartNpcId!.Value, questId,
									[("QUEST_SELECT", null), ("CHECK_USER_HAS_QUEST_ITEM", timer.ExpiredPage), (timer.NewChanceAction!, null)]);
								TimerStarted();
								break;
							case "wait-until-ready":
								await RestSafelyAsync(token);
								// The powder rest calls 65% mana recovered; the timer wants 80%. Let it come back, as a player waits.
								await session.AdvanceAsync(TimeSpan.FromSeconds(10), token);
								break;
							default: // wait-for-journal
								await session.AdvanceAsync(TimeSpan.FromSeconds(2), token);
								break;
						}
					}
					throw new InvalidDataException($"Q{questId}'s timed run exceeded 300 rounds.");
				}

				// AB-08: Q2223's Infernus. Without incense, Lamir gives another (page 1779). With it: rest to full, burn it at the Old
				// Incense Burner (movie 67), and fight Infernus inside his five minutes. Three tries (AB-Q2).
				// AG-07: Q2252's bones raise Minushan's Spirit or Drakie (no movie). AG-06 found the bones' peckus joining the Spirit
				// (48% HP and a retreat), so whatever walks around the trigger is cleared first, and the kill takes the monster whose
				// SM_NPC_INFO follows the use: AG-04 found a gone one can stay in view after a teleport.
				async Task RunSpawnKillAsync(NaturalAltgardSpawn spawn)
				{
					int tries = spawnAttempts.GetValueOrDefault(spawn.Key);
					if (tries >= NaturalTimedQuestPolicy.MaxAttempts)
						throw new InvalidDataException($"Altgard leg {altgardLegId}: {spawn.Key} failed {tries} times (AB-Q2).");
					if (ItemCount(session.Api.World, spawn.RequiresItemId) == 0)
					{
						await DialogAsync(spawn.RefillNpcId, spawn.QuestId, [("QUEST_SELECT", spawn.RefillPage)], (3, spawn.AtVar));
						return;
					}
					await RestSafelyAsync(token);
					await EnsureOnGroundAsync();
					int burner = await ApproachShippedSpawnAsync(spawn.TriggerNpcId, skipBlockedTarget: true);
					if (!session.Api.World.Objects.ContainsKey(burner))
					{
						await session.AdvanceAsync(TimeSpan.FromSeconds(30), token); // the burner respawns 295 s after a use
						return;
					}
					await ClearAroundObjectiveAsync(burner, $"spawn-{spawn.Key}");
					await RestSafelyAsync(token);
					burner = await ApproachShippedSpawnAsync(spawn.TriggerNpcId, skipBlockedTarget: true);
					spawnAttempts[spawn.Key] = tries + 1;
					int start = session.PacketHistory.Count;
					bool used = await NaturalAltgardQuestSteps.UseObjectAsync(session, burner, null, token);
					await NaturalMovieGate.FinishAsync(session, token);
					int[] raised = [spawn.NpcId, .. spawn.AlternateNpcIds ?? []];
					int? monster = null;
					for (int wait = 0; wait < 10 && monster == null; wait++)
					{
						monster = session.PacketHistory.Skip(Math.Min(start, session.PacketHistory.Count)).LastOrDefault(packet =>
							packet.PacketType == typeof(SM_NPC_INFO) && raised.Contains(packet.Get<int>("npcId")))?.Get<int>("objectId");
						if (monster != null) break;
						await session.AdvanceAsync(TimeSpan.FromMilliseconds(500), token);
						await session.SynchronizeAsync(token);
					}
					bool killed = monster is int raisedId && await combat.TryKillAsync(raisedId, token, session.CurrentPosition);
					await session.SynchronizeAsync(token);
					session.TraceDiagnostic($"altgard-{altgardLegId}-spawn-kill", new Dictionary<string, object?>
					{
						["spawn"] = spawn.Key, ["try"] = tries + 1, ["used"] = used, ["monster"] = monster,
						["template"] = monster is int seen ? session.Api.World.Objects.GetValueOrDefault(seen)?.TemplateId : null, ["killed"] = killed,
						["quest"] = NaturalAltgardQuestSteps.State(session.Api.World, spawn.QuestId)?.ToString(),
					});
					await RestSafelyAsync(token);
				}

				// AC-06: the escort (docs/natural-altgard-leveling.md, "The escort handler"). NaturalEscortProtocol runs it; this
				// gives it the walk, the fight, and the clear: before each start the clear areas are cleared (AC-Q3), the dock
				// end first (121 m from the follower, out of sight from him), then back along the line. A death ends the
				// protocol run; the next decision revives and the escort resumes with the attempts and ended followers kept.
				async Task RunEscortAsync(NaturalAltgardEscort escort)
				{
					int map = leg.Hub.MapId;
					NaturalAltgardStep restart = leg.Steps.Single(step => step.Key == escort.RestartStep);
					BotPosition followerStart = geometry.SnapToGround(map, new BotPosition(restart.Position[0], restart.Position[1], restart.Position[2] + 1, 0))
						?? throw new InvalidDataException($"No ground at the {escort.Key} follower.");
					BotPosition standPoint = NaturalEscortPolicy.GoalStand(escort.Goal, followerStart);
					BotPosition stand = geometry.SnapToGround(map, standPoint with { Z = standPoint.Z + 1 })
						?? throw new InvalidDataException($"No ground at the {escort.Key} goal stand.");
					IReadOnlyList<BotPosition> escortRoute = geometry.FindJourneyPath(map, followerStart, stand);
					Require.True(escortRoute.Count > 0, $"No route for the {escort.Key} escort.");
					bool InClearArea(BotPosition at) => escort.ClearAreas.Any(key => leg.Area(key).Contains(at.X, at.Y, at.Z));
					var protocol = new NaturalEscortProtocol(session, leg, escort, () => runtime.NowMillis)
					{
						WalkToAsync = async (to, walkToken) =>
						{
							IReadOnlyList<BotPosition> path = geometry.FindJourneyPath(map, session.CurrentPosition, to);
							await navigator.MoveAsync(path.Count > 0 ? path : [to], walkToken);
							await session.SynchronizeAsync(walkToken);
						},
						Route = () => escortRoute,
						FightAsync = async _ => await DefendAgainstEngagedAsync($"escort-{escort.Key}"),
						RetreatAsync = async _ => await DefendAgainstEngagedAsync($"escort-{escort.Key}-retreat"),
						Threat = () => (Engaged().Attackers.Length, false),
						ClearAreasHaveAggressors = () => escortClearedUntil is not long until || until <= runtime.NowMillis ||
							ObservedPullMonsters().Any(monster => InClearArea(monster.Npc.Position)),
						ClearAsync = async _ =>
						{
							long? firstKill = null;
							int kills = 0;
							foreach (int anchorNpc in new[] { escort.GoalNpcId, escort.FollowerNpcId })
							{
								await ApproachShippedSpawnAsync(anchorNpc, withinRange: 15);
								for (int round = 0; round < 16; round++)
								{
									if (!await DefendAgainstEngagedAsync($"escort-{escort.Key}-clear") || session.Api.World.IsDead) return null;
									NaturalPullMonster? robber = ObservedPullMonsters().Where(monster => InClearArea(monster.Npc.Position))
										.OrderBy(monster => Distance(session.CurrentPosition, monster.Npc.Position)).FirstOrDefault();
									if (robber == null) break;
									if (await PullAndKillAsync(robber.Npc.ObjectId, $"escort-{escort.Key}-clear"))
									{
										// RC-11: a looted corpse can remain in view; clear a live source only once.
										navigator.UnavailableObjects.Add(robber.Npc.ObjectId);
										session.TraceDiagnostic("escort-clear-retire-killed-source", new Dictionary<string, object?>
										{
											["escort"] = escort.Key, ["objectId"] = robber.Npc.ObjectId, ["npcId"] = robber.Npc.TemplateId,
										});
										firstKill ??= runtime.NowMillis;
										kills++;
									}
									else if (session.Api.World.IsDead) return null;
									else navigator.UnavailableObjects.Add(robber.Npc.ObjectId);
								}
							}
							escortClearedUntil = (firstKill ?? runtime.NowMillis) + EscortClearRespawnMillis;
							session.TraceDiagnostic($"altgard-{altgardLegId}-escort-clear", new Dictionary<string, object?>
							{
								["escort"] = escort.Key, ["kills"] = kills, ["firstRespawn"] = firstKill is long kill ? kill + EscortClearRespawnMillis : null,
							});
							return firstKill is long first ? first + EscortClearRespawnMillis : null;
						},
						PriorAttempts = escortAttempts,
						EndedFollowerObjectIds = escortEndedFollowers,
						FollowerGoneAtMillis = escortFollowerGoneAt,
						OffHours = escort.FollowerSpawnHour is int opens && escort.FollowerDespawnHour is int closes
							? () => session.Api.World.GameMinutesAt(session.Api.Timing.Now) is long minutes &&
								!NaturalGameClock.Within(minutes, opens, closes)
							: null,
					};
					NaturalEscortResult result = await protocol.RunAsync(token);
					escortAttempts = result.Attempts;
					escortEndedFollowers.UnionWith(result.EndedFollowers ?? []);
					escortFollowerGoneAt = result.FollowerGoneAtMillis;
					session.TraceDiagnostic($"altgard-{altgardLegId}-escort", new Dictionary<string, object?>
					{
						["escort"] = escort.Key, ["outcome"] = result.Outcome, ["attempts"] = result.Attempts, ["movie"] = result.MovieSeen,
						["followerSpeed"] = result.FollowerSpeed,
						["log"] = result.Log.Select(attempt => $"#{attempt.Number} {attempt.Outcome} {(attempt.EndMillis - attempt.StartMillis) / 1000.0:F0}s " +
							$"gap {attempt.LongestGap:F1} hops {attempt.Hops} {attempt.LossReason}").ToArray(),
					});
					if (result.Outcome == "give-up")
					{
						// AC-Q2: three failed attempts end the leg without the escort (and Q2222, which follows it): a finding to fix.
						await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, $"altgard-{altgardLegId}-escort-given-up.json"),
							System.Text.Json.JsonSerializer.Serialize(result), token);
						throw new InvalidDataException($"Altgard leg {altgardLegId}: the {escort.Key} escort failed {result.Attempts} times (AC-Q2).");
					}
				}
			}

			// AF-08: the Leg 1 endpoint. Assert it from the client's view, then quit, log back in and require that the
			// character survived as it was; altgard-l1-completion.json carries the clock for the `altgard-l12` snapshot.
			async Task CompleteAltgardLeg1Async(NaturalAltgardContract leg)
			{
				session.BeginStep("af-endpoint", "verify-and-relog-at-the-leg-1-endpoint");
				await TopUpHelpItemsAsync("checkpoint");
				BotWorldModel world = session.Api.World;
				Require.All(leg.Endpoint.CompletedQuestIds, quest => Require.Contains(quest, world.CompletedQuestIds));
				foreach (int id in leg.Endpoint.CompletedQuestIds)
					foreach (var item in runtime.Data.Quests.GetQuestById(id).GetQuestWorkItems()?.GetQuestWorkItem() ?? [])
						Require.Equal(0L, ItemCount(world, item.GetItemId()));
				// AK-08 (AK-Q2): the held hand-ins are taken, not handed in, and their objectives are met.
				IReadOnlyDictionary<int, NaturalTemplateObjective> heldObjectives = NaturalTemplateObjective.From(altgardPlans);
				var heldItems = world.Inventory.Values.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
				Require.All(leg.HeldList, held => Require.True(!world.CompletedQuestIds.Contains(held.QuestId) &&
					world.Quests.TryGetValue(held.QuestId, out BotQuestState? state) && state.Status is 3 or 4 &&
					heldObjectives[held.QuestId].IsDone(state, heldItems), $"Q{held.QuestId} is not held ready for {held.EndNpcId}."));
				Require.Equal(leg.Endpoint.MapId, world.MapId!.Value);
				Require.True(world.Level >= leg.Endpoint.MinimumLevel, $"Endpoint level {world.Level} is below {leg.Endpoint.MinimumLevel}.");
				Require.True(!world.IsDead, "The endpoint character is dead.");
				VerifyDestinyEndpoint();
				VerifyCoinEndpoint();
				VerifyHaramelEndpoint();
				NaturalJourneyCheckpoint before = NaturalJourneyCheckpoint.Capture(world, session.CharacterId,
					session.ConnectionGeneration, contract, session.CurrentPosition, coinGearProgress: coinGearProgress, haramelProgress: haramelProgress, earlyAscension: options.AscensionBridge);
				session.BeforeSend = null;
				await session.QuitAsync(token);
				await session.WaitForReentryAsync(token);
				await session.ReloginExistingCharacterAsync(token);
				await session.EnterWorldAsync(token);
				await session.SynchronizeAsync(token);
				NaturalJourneyCheckpoint after = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
					session.ConnectionGeneration, contract, session.CurrentPosition, coinGearProgress: coinGearProgress, haramelProgress: haramelProgress, earlyAscension: options.AscensionBridge);
				if (leg.Haramel != null)
					await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "haramel-endpoint-relog.json"),
						System.Text.Json.JsonSerializer.Serialize(new { before, after }), token);
				NaturalJourneyPersistence.Verify(before, after);
				VerifyDestinyEndpoint();
				if (laterCapital != null) await laterCapital.WriteCheckpointAsync(Path.GetDirectoryName(combatTracePath)!,
					altgardLegId!, before, after, token, distinctSegment: continuousAltgard);
				VerifyCoinEndpoint();
				VerifyHaramelEndpoint();
				await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, $"altgard-{altgardLegId}-completion.json"),
					System.Text.Json.JsonSerializer.Serialize(new
					{
						before, after, verified = true, session.CharacterId, ElapsedMillis = runtime.NowMillis,
						Deaths = combat.ReviveCount,
					}), token);
				session.TraceDiagnostic($"altgard-{altgardLegId}-complete", new Dictionary<string, object?>
				{
					["level"] = after.Level, ["map"] = after.MapId, ["completed"] = leg.Endpoint.CompletedQuestIds,
					["deaths"] = combat.ReviveCount, ["kinah"] = session.Api.World.Kinah,
				});

				void VerifyDestinyEndpoint()
				{
					if (leg.Destiny is not { } destiny) return;
					BotWorldModel observed = session.Api.World;
					Require.All(leg.Start.CompletedQuestIds, id => Require.Contains(id, observed.CompletedQuestIds));
					IEnumerable<int> expected = leg.Start.CompletedQuestIds.Append(destiny.QuestId);
					if (laterCapital != null) expected = expected.Append(2938);
					Require.True(observed.CompletedQuestIds.SetEquals(expected), "Destiny must preserve the incoming journal and complete only its scheduled finishes.");
					Require.Equal(0L, ItemCount(observed, destiny.StoneItemId));
					Require.Equal(0L, ItemCount(observed, destiny.LegacyRewardId));
					Require.Equal(1L, ItemCount(observed, destiny.RewardBundleId));
					Require.True(!observed.Skills.ContainsKey(destiny.StigmaSkillId), "The tutorial stigma skill survived cleanup.");
					Require.All(destinyIncomingSkills, id => Require.Contains(id, observed.Skills.Keys));
					NaturalAltgardDecision endpoint = NaturalAltgardDecisionEngine.Decide(leg,
						NaturalAltgardObservation.Observe(observed, session.CurrentPosition, session.Api.Timing.Now),
						new Dictionary<int, NaturalTemplateObjective>(), 1);
					Require.Equal("complete", endpoint.Outcome);
				}

				void VerifyCoinEndpoint()
				{
					if (leg.CoinGear is not { } gear) return;
					BotWorldModel observed = session.Api.World;
					Require.Equal(leg.Start.CompletedQuestIds.Length + 1, observed.CompletedQuestIds.Count);
					Require.All(leg.Start.CompletedQuestIds, id => Require.Contains(id, observed.CompletedQuestIds));
					Require.Equal(1, observed.CompletedQuestCounts.GetValueOrDefault(gear.QuestId));
					Require.Equal((long)gear.EndpointCoins, ItemCount(observed, gear.CoinItemId));
					Require.Equal(0L, ItemCount(observed, 186000007));
					Require.Equal(1L, ItemCount(observed, gear.SealedBundleId));
					Require.True(!observed.Skills.ContainsKey(gear.ForbiddenStigmaSkillId), "Keep the stigma reward sealed.");
					foreach (NaturalJourneyItem original in coinIncomingLoadout)
						Require.True(observed.Inventory.TryGetValue(original.ObjectId, out BotInventoryItem? item) &&
							item.ItemId == original.ItemId && item.EquipmentSlot == original.EquipmentSlot,
							$"CG changed retained equipped object {original.ObjectId}/{original.ItemId}.");
					NaturalInventoryPlan bag = NaturalIshalgenInventoryPolicy.Load(runtime.RepoRoot,
						observed.Inventory.Values.Select(i => i.ItemId)).Decide(observed, QuestNeededItems(), gear);
					NaturalAltgardDecision endpoint = NaturalAltgardDecisionEngine.Decide(leg,
						NaturalAltgardObservation.Observe(observed, session.CurrentPosition, session.Api.Timing.Now, bag.FreeSlots, coinGearProgress, haramelProgress, HaramelNow()),
						NaturalTemplateObjective.From(altgardPlans), 1);
					Require.Equal("complete", endpoint.Outcome);
				}
				void VerifyHaramelEndpoint()
				{
					if (leg.Haramel is not { } rules) return;
					BotWorldModel observed = session.Api.World;
					Require.Equal(leg.Start.CompletedQuestIds.Union(leg.Endpoint.CompletedQuestIds).Count(), observed.CompletedQuestIds.Count);
					Require.All(leg.Order, id => Require.Equal(1, observed.CompletedQuestCounts.GetValueOrDefault(id)));
					Require.All(leg.Start.CompletedQuestIds, id => Require.Contains(id, observed.CompletedQuestIds));
					Require.Equal((long)rules.IronCount, ItemCount(observed, rules.IronItemId));
					Require.Equal((long)rules.BronzeCount, ItemCount(observed, rules.BronzeItemId));
					Require.All(rules.CleanupItemIds, id => Require.Equal(0L, ItemCount(observed, id)));
					Require.True(observed.Inventory.TryGetValue(rules.StaffObjectId, out BotInventoryItem? staff) && staff.ItemId == rules.StaffItemId && staff.Details.EquippedSlot == 3, "Haramel replaced the retained staff.");
					Require.True(haramelProgress!.Visits.Count(visit => visit.BossMovieObserved && visit.ChestResolved) >= 2 &&
						haramelProgress.Visits.Any(visit => visit.PostBossQuests && visit.FreshSpawnsObserved), "Two actual fresh clears and their class chest outcomes must be retained.");
					Require.Equal(combat.ReviveCount, haramelProgress.Revives);
					Require.Equal("complete", NaturalAltgardDecisionEngine.Decide(leg,
						NaturalAltgardObservation.Observe(observed, session.CurrentPosition, session.Api.Timing.Now, haramelProgress: haramelProgress, nowMillis: HaramelNow()),
						NaturalTemplateObjective.From(altgardPlans), 1).Outcome);
				}
			}

			// NA-23: the focused Cleric encounter (diagnostic, SIM only). The fixture prepares a level 10 Cleric outside
			// Altgard Fortress and, per stage, the monsters. The journey's own pull, patrol, combat and rest code then
			// plays each stage on the Altgard map: the navigator, combat and map it captures are rebound to Altgard here.
			async Task RunClericEncounterAsync()
			{
				Require.True(combat.IsCleric, "The NA-23 encounter needs the level 10 Cleric.");
				Require.True(runtime.PrepareEncounterStageAsync != null, "The NA-23 encounter needs its stage setup.");
				NaturalJourneyNavigator here = mapNavigators.Enter(NaturalMapKey.Observe(session.Api.World));
				here.DefendOnAttackAsync = navigator.DefendOnAttackAsync;
				here.AvoidHostileAggro = true;
				navigator = here;
				// The journey's geometry was made for the world instance it started in (Ishalgen); line of sight and
				// routes for the pull planner must come from the instance the Cleric now stands in.
				geometry = runtime.CreateGeometry();
				contract = contract with { MapId = session.Api.World.MapId!.Value };
				combat = new NaturalJourneyCombat(session, here, runtime, geometry, stopOnDeath: false, options.OptimizeHubs, mauPolicy)
				{
					ApproachMapId = contract.MapId,
				};
				navigationDefense = combat;
				WithQuestLoot(combat);
				await TopUpHelpItemsAsync("run-start"); // NA-21: the approved help items
				var stages = new List<Dictionary<string, object?>>();
				foreach (string stage in runtime.EncounterStages ?? ["single", "pair", "patrol"])
				{
					session.BeginStep($"na23-{stage}", "cleric-encounter-stage");
					await runtime.PrepareEncounterStageAsync!(stage, token);
					await session.SynchronizeAsync(token);
					BotPosition origin = session.CurrentPosition;
					int revivesBefore = combat.ReviveCount, retreatsBefore = combat.CompletedRetreats, kills = 0, plans = 0;
					var defeated = new HashSet<int>();
					long start = runtime.NowMillis;
					for (int pull = 0; pull < 6; pull++)
					{
						await DefendAgainstEngagedAsync($"na23-{stage}");
						IEnumerable<NaturalNavigationObject> encounterTargets = runtime.EncounterNpcIds is { } kinds
							? navigator.Observe().Npcs.Where(npc => kinds.Contains(npc.TemplateId))
							: ObservedPullMonsters().Select(monster => monster.Npc);
						NaturalNavigationObject[] targets = encounterTargets
							.Where(npc => !defeated.Contains(npc.ObjectId) && Distance(origin, npc.Position) < 60)
							.OrderBy(npc => Distance(session.CurrentPosition, npc.Position)).ToArray();
						if (targets.Length == 0) break;
						NaturalPullPlan? plan = await MoveToPullSpotAsync(targets, [], $"na23-{stage}");
						if (plan == null) break;
						plans++;
						if (await combat.TryKillAsync(plan.Target.Npc.ObjectId, token, retreatAnchor: session.CurrentPosition))
						{
							defeated.Add(plan.Target.Npc.ObjectId);
							kills++;
						}
					}
					await DefendAgainstEngagedAsync($"na23-{stage}-after");
					await RestSafelyAsync(token);
					stages.Add(new()
					{
						["stage"] = stage, ["plans"] = plans, ["kills"] = kills, ["deaths"] = combat.ReviveCount - revivesBefore,
						["retreats"] = combat.CompletedRetreats - retreatsBefore,
						["gameSeconds"] = (runtime.NowMillis - start) / 1000, ["hp"] = session.Api.World.CurrentHp,
						["mp"] = session.Api.World.CurrentMp, ["dp"] = session.Api.World.CurrentDp,
					});
					session.TraceDiagnostic("na23-stage-complete", stages[^1]);
				}
				await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "na23-summary.json"),
					System.Text.Json.JsonSerializer.Serialize(stages, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }), token);
			}

			bool AscensionBridgeStarted() => NaturalAscensionDecisionEngine.BridgeStarted(session.Api.World.CompletedQuestIds,
				session.Api.World.Quests, session.Api.World.MapId);

			// NA-17: the bridge endpoint. Assert the contract from the client's view, then quit, log back in and require
			// that class, level, quests, bind point, equipment, inventory and position all survived.
			async Task CompleteAscensionLegAsync(NaturalAscensionContract bridge)
			{
				session.BeginStep("na-endpoint", "verify-and-relog-at-the-bridge-endpoint");
				await TopUpHelpItemsAsync("checkpoint");
				if (relogAt != null) Require.True(relogInjected, "A requested bridge interruption was never exercised.");
				BotWorldModel world = session.Api.World;
				Require.True(NaturalJourneyIdentityRules.Classify(world.Objects[session.CharacterId].PlayerClass ?? 0, world.Level, world.MapId)
					== NaturalJourneyStage.AscensionCleric, "The endpoint character is not the bridge's Cleric.");
				Require.True(world.Level >= bridge.Endpoint.MinimumLevel, $"Endpoint level {world.Level} is below {bridge.Endpoint.MinimumLevel}.");
				Require.All(bridge.Endpoint.CompletedQuestIds, quest => Require.Contains(quest, world.CompletedQuestIds));
				Require.Equal(bridge.Endpoint.MapId, world.MapId!.Value);
				Require.True(world.ObeliskBindPoint is { } bound && bound.MapId == bridge.Bind.MapId, "The endpoint is not bound in Altgard.");
				Require.All(bridge.Endpoint.EquippedItemIds, item => Require.True(world.Inventory.Values.Any(owned => owned.ItemId == item &&
					(owned.Details.EquippedSlot ?? 0) > 0), $"Endpoint item {item} is not worn."));
				// NA-20a: every kept accessory is worn. The item's full slot mask decides (the belt's WAIST bit, 1<<16, does not
				// fit the inventory packet's 16-bit slot field that the checkpoint records).
				Require.All(bridge.KeptAccessories, item => Require.True(world.Inventory.Values.Any(owned => owned.ItemId == item &&
					(owned.Details.EquippedSlot ?? 0) > 0), $"Kept accessory {item} is not worn at the endpoint."));
				Require.True(!world.IsDead, "The endpoint character is dead.");
				NaturalJourneyCheckpoint before = NaturalJourneyCheckpoint.Capture(world, session.CharacterId,
					session.ConnectionGeneration, contract, session.CurrentPosition, coinGearProgress: coinGearProgress, haramelProgress: haramelProgress, earlyAscension: options.AscensionBridge);
				session.BeforeSend = null;
				await session.QuitAsync(token);
				await session.WaitForReentryAsync(token);
				await session.ReloginExistingCharacterAsync(token);
				await session.EnterWorldAsync(token);
				await session.SynchronizeAsync(token);
				NaturalJourneyCheckpoint after = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
					session.ConnectionGeneration, contract, session.CurrentPosition, coinGearProgress: coinGearProgress, haramelProgress: haramelProgress, earlyAscension: options.AscensionBridge);
				NaturalJourneyPersistence.Verify(before, after);
				if (laterCapital != null) await laterCapital.WriteCheckpointAsync(Path.GetDirectoryName(combatTracePath)!,
					"bridge", before, after, token, distinctSegment: continuousAltgard);
				await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "bridge-completion.json"),
					System.Text.Json.JsonSerializer.Serialize(new
					{
						before, after, verified = true,
						// NA-25: an `altgard` snapshot restores with this clock so game time keeps moving forward.
						session.CharacterId, ElapsedMillis = runtime.NowMillis,
					}), token);
				session.TraceDiagnostic("ascension-bridge-complete", new Dictionary<string, object?>
				{
					["level"] = after.Level, ["class"] = after.PlayerClass, ["map"] = after.MapId, ["bind"] = after.BindPoint?.MapId,
					["completed"] = bridge.Endpoint.CompletedQuestIds, ["kinah"] = session.Api.World.Kinah,
				});
			}

			// NA-16: the Altgard shop stop. Wear the best owned gear (accessories included), sell what the Cleric rules call
			// surplus, buy only the contract's consumables (better potions, powder; never gear, OD-7), drink one Tea of Repose.
			async Task ShopInAltgardAsync(NaturalAscensionContract bridge)
			{
				session.BeginStep("na-shop-altgard", "altgard-shop-stop");
				await TopUpHelpItemsAsync("town");
				BotWorldModel world = session.Api.World;
				await EquipUpgradesAsync(token);
				await session.SynchronizeAsync(token);
				long Owned(int item) => world.Inventory.Values.Where(i => i.ItemId == item).Sum(i => i.Count);
				IReadOnlyCollection<int> Tab(int id) => runtime.Data.GoodsListDataDh.GetGoodsListById(id)?.GetItemIdList() ?? [];
				long Base(int item) => runtime.Data.ItemDataDh.GetItemTemplate(item).GetPrice();
				Require.True(!bridge.Shop.BuysGear, "The bridge never buys gear (OD-7).");
				Require.True(bridge.Shop.Purchases.All(p => runtime.Data.ItemDataDh.GetItemTemplate(p.ItemId).GetItemSlot() == 0),
					"A bridge purchase would be equipment.");
				var plan = NaturalIshalgenInventoryPolicy.Load(runtime.RepoRoot, world.Inventory.Values.Select(i => i.ItemId)).Decide(world);
				var sales = plan.Sales.Select(sale => new NaturalSale(sale.ObjectId, sale.ItemId, sale.Count)).ToList();
				BotPosition VendorAt(int npc) => runtime.Data.SpawnsDh.GetSpawnsByWorldId(bridge.Bind.MapId)
					.Where(group => group.GetNpcId() == npc).SelectMany(group => group.GetSpawnTemplates())
					.Select(spot => new BotPosition(spot.GetX(), spot.GetY(), spot.GetZ(), 0)).First();
				var steps = new NaturalServiceSteps(session);
				var vendors = new[] { bridge.Shop.SellNpcId, bridge.Shop.PotionNpcId, bridge.Shop.ReagentNpcId }.Distinct()
					.OrderBy(npc => Distance(session.CurrentPosition, VendorAt(npc))).ToArray();
				foreach (int vendorNpc in vendors)
				{
					var wanted = bridge.Shop.Purchases
						.Select(p => new NaturalPurchase(p.ItemId, Math.Max(0, (p.Target ?? p.TargetCombinedLifePotions ?? 0) - Owned(p.ItemId))))
						.Where(p => p.Count > 0).ToList();
					var sell = vendorNpc == bridge.Shop.SellNpcId ? sales : [];
					if (wanted.Count == 0 && sell.Count == 0) continue;
					NaturalJourneyNavigator here = mapNavigators.Enter(NaturalMapKey.Observe(session.Api.World), newEntry: false);
					NaturalNavigationResult approach = await NaturalIshalgenNavigator.ApproachNpcAsync(bridge.Bind.MapId, vendorNpc,
						VendorAt(vendorNpc), here, token);
					Require.True(approach.Arrived, $"Altgard vendor {vendorNpc}: {approach.Reason}");
					NaturalVendorResult trade = await steps.TradeAsync(Require.IsType<int>(approach.TargetObjectId), sell, wanted, Tab, Base, token);
					if (vendorNpc == bridge.Shop.SellNpcId) sales = [];
					session.TraceDiagnostic("altgard-shop-visit", new Dictionary<string, object?>
					{
						["vendor"] = vendorNpc, ["sold"] = trade.Sold.Select(sold => $"{sold.ItemId}x{sold.Count}").ToArray(),
						["bought"] = trade.Bought.Select(b => $"{b.ItemId}x{b.Count}").ToArray(),
						["refused"] = trade.Refused.Select(r => $"{r.Purchase.ItemId}:{r.Reason}").ToArray(), ["kinah"] = world.Kinah,
					});
				}
				foreach (NaturalAscensionPurchase purchase in bridge.Shop.Purchases)
					Require.True(Owned(purchase.ItemId) >= (purchase.Target ?? 0), $"Altgard shop left {purchase.ItemId} below its target.");
				// Tea of Repose (OD-8): once, out of combat, now that the Cleric is level 10.
				int teaId = bridge.CeremonyReward.TeaItemId;
				if (world.Inventory.Values.FirstOrDefault(item => item.ItemId == teaId) is { } tea)
				{
					long before = Owned(teaId);
					await session.SendPacketAsync(session.Api.UseItem(tea.ObjectId, runtime.Data.ItemDataDh.GetItemTemplate(teaId)), token);
					await session.AdvanceAsync(TimeSpan.FromSeconds(3), token);
					await session.SynchronizeAsync(token);
					Require.Equal(before - 1, Owned(teaId));
				}
				session.TraceDiagnostic("altgard-shop-result", new Dictionary<string, object?>
				{
					["worn"] = world.Inventory.Values.Where(item => (item.Details.EquippedSlot ?? 0) > 0).Select(item => item.ItemId).Order().ToArray(),
					["supplies"] = new[] { 162000053, 169300003, 160002273, 162001057, 162000002 }.Select(id => $"{id}x{Owned(id)}").ToArray(),
					["kinah"] = world.Kinah,
				});
				bridgeShopVisited = true;
			}

			// NA-15: Doman's teleporter to Altgard (NA-08 service step: the price-adjusted fare, the map change).
			async Task TakeBridgeTeleporterAsync(NaturalAscensionContract bridge)
			{
				session.BeginStep("na-teleport-altgard", "doman-teleporter-to-altgard");
				NaturalAscensionStep doman = bridge.Steps.Single(step => step.Key == "q2904-v0-doman");
				var anchor = new BotPosition(doman.Position[0], doman.Position[1], doman.Position[2], 0);
				NaturalJourneyNavigator here = mapNavigators.Enter(NaturalMapKey.Observe(session.Api.World), newEntry: false);
				int npc = await ApproachBridgeNpcAsync(doman, anchor, here);
				NaturalServiceOutcome result = await new NaturalServiceSteps(session).TeleportAsync(npc,
					session.Api.World.Objects[npc].Position, doman.TalkRange, bridge.Teleporter.LocationId, bridge.Teleporter.BasePrice,
					bridge.Teleporter.Destination.MapId, token);
				Require.True(result.IsDone, result.Reason);
				mapNavigators.Enter(NaturalMapKey.Observe(session.Api.World));
			}

			// NA-15: the Altgard Fortress obelisk, first thing in Altgard (NA-08 service step: within 5 m, raw price).
			async Task BindInAltgardAsync(NaturalAscensionContract bridge)
			{
				session.BeginStep("na-bind-altgard", "bind-at-altgard-fortress-obelisk");
				var obelisk = new BotPosition(bridge.Bind.Position[0], bridge.Bind.Position[1], bridge.Bind.Position[2], 0);
				NaturalJourneyNavigator here = mapNavigators.Enter(NaturalMapKey.Observe(session.Api.World), newEntry: false);
				NaturalNavigationResult approach = await NaturalIshalgenNavigator.ApproachNpcAsync(bridge.Bind.MapId, bridge.Bind.NpcId,
					obelisk, here, token);
				Require.True(approach.Arrived, $"Altgard obelisk: {approach.Reason}");
				int stone = Require.IsType<int>(approach.TargetObjectId);
				BotPosition at = session.Api.World.Objects[stone].Position;
				NaturalServiceOutcome result = await new NaturalServiceSteps(session).BindAsync(stone, at, bridge.Bind.MapId,
					bridge.Bind.Price, bridge.Bind.AcceptRange, token);
				Require.True(result.IsDone, result.Reason);
			}

			async Task WriteBridgeStopAsync(NaturalAscensionDecision stop)
			{
				session.TraceDiagnostic("ascension-bridge-stop", new Dictionary<string, object?>
				{
					["action"] = stop.Action, ["step"] = stop.StepKey, ["outcome"] = stop.Outcome, ["reason"] = stop.Reason,
				});
				await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "bridge-stop.json"),
					System.Text.Json.JsonSerializer.Serialize(new
					{
						stop, session.Api.World.MapId, session.Api.World.Level, session.CurrentPosition,
						Quests = session.Api.World.Quests.Values.Where(q => q.QuestId is 2008 or 2009 or 2904 or 24010).ToArray(),
						Completed = session.Api.World.CompletedQuestIds.Where(q => q is 2008 or 2009 or 2904 or 24010).ToArray(),
					}), token);
			}

			// One contract talk step: approach the NPC on the current map, open its dialog, send the step's actions,
			// wait for each dialog page, finish any movie, and follow a same-map or cross-map quest teleport.
			async Task PlayBridgeTalkAsync(NaturalAscensionStep step)
			{
				session.BeginStep($"na-{step.Key}", "ascension-bridge-talk");
				var anchor = new BotPosition(step.Position[0], step.Position[1], step.Position[2], 0);
				NaturalJourneyNavigator here = mapNavigators.Enter(NaturalMapKey.Observe(session.Api.World), newEntry: false);
				int npc = await ApproachBridgeNpcAsync(step, anchor, here);
				for (int attempt = 1; ; attempt++)
				{
					try
					{
						await NaturalDialogProtocol.OpenAsync(session, npc, token);
						await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == npc);
						break;
					}
					catch (NaturalDialogTooFarException) when (attempt < 3)
					{
						npc = await ApproachBridgeNpcAsync(step, anchor, here);
					}
				}
				string[] actions = step.Actions;
				int pageOffset = 0;
				if (actions[0] == "USE_OBJECT")
				{
					// The talk itself was the first action; its page was the dialog just opened.
					actions = actions[1..];
					pageOffset = 1;
				}
				for (int i = 0; i < actions.Length; i++)
				{
					bool last = i == actions.Length - 1;
					bool teleports = last && step.Teleport != null;
					bool otherMap = teleports && step.Teleport!.MapId != session.Api.World.MapId;
					if (otherMap) session.Api.World.BeginWorldReload();
					ushort action = checked((ushort)NaturalAscensionContract.DialogActionId(actions[i]));
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npc, action, questId: step.QuestId), token);
					if (step.MovieId != null && actions[i] is "SELECT5_1" or "SELECT2_1" or "SELECT3_1")
						await NaturalMovieGate.FinishAsync(session, token);
					if (pageOffset + i < step.Pages.Length && !teleports)
					{
						int page = step.Pages[pageOffset + i];
						await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet =>
							packet.Get<int>("targetObjectId") == npc && packet.Get<ushort>("dialogPageId") == page);
					}
					if (!teleports) continue;
					if (otherMap)
					{
						await session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token, packet => packet.Get<int>("worldId") == step.Teleport!.MapId);
						await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
					}
					else
					{
						await session.WaitForPacketAsync(typeof(SM_CHANNEL_INFO), token);
						await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
					}
					session.AcceptTeleportPosition();
					if (otherMap) mapNavigators.Enter(NaturalMapKey.Observe(session.Api.World));
				}
				if (step.FlyPathId != null)
				{
					// Hagen's QUEST_SELECT starts the scripted flight (teleport 3001, flypath 3); the client flies the path
					// with CM_MOVE_IN_AIR and lands with LAND_FLYTELEPORT. The trial spawns about 2 s before the landing.
					NaturalAscensionInstance instance = NaturalAscensionContract.LoadDefault().Instance;
					await session.WaitForPacketAsync(typeof(SM_EMOTION), token, packet =>
						packet.Get<int>("senderObjectId") == session.CharacterId &&
						packet.Get<byte>("emotionType") == (byte)EmotionType.START_FLYTELEPORT);
					var landing = new BotPosition(instance.FlyPathEnd[0], instance.FlyPathEnd[1], instance.FlyPathEnd[2], 0);
					await session.ExecuteMovementAsync(CapitalAscensionScenario.CreateQuestFlight(session.CurrentPosition, landing,
						instance.MapId, TimeSpan.FromSeconds(instance.FlightSeconds)), token);
					await session.SendPacketAsync(GameClientPackets.Emotion((byte)EmotionType.LAND_FLYTELEPORT), token);
				}
				await session.SynchronizeAsync(token);
				NaturalMovieGate.RecordSkipped(session);
				if (step.ReceivesItemId is int card)
					Require.True(ItemCount(session.Api.World, card) >= 1 || session.Api.World.Quests[step.QuestId].StepAndFlags > step.Var,
						$"{step.Key} did not hand over item {card}.");
				// Munin's SELECT5_1 takes the three Destiny Cards back before Ataxiar opens.
				if (step.Key == "q2008-v4-munin")
					Require.True(NaturalAscensionContract.LoadDefault().Steps.Where(s => s.QuestId == 2008 && s.ReceivesItemId != null)
						.All(s => ItemCount(session.Api.World, s.ReceivesItemId!.Value) == 0), "Munin did not take the Destiny Cards back.");
				if (step.QuestId == NaturalAscensionContract.LoadDefault().CeremonyReward.QuestId && step.ExpectedStatus == "REWARD")
				{
					// NA-14: the ceremony pays level 10, the Karmic Staff (OD-5), 250,000 Kinah and five teas; wear the staff.
					NaturalAscensionCeremonyReward reward = NaturalAscensionContract.LoadDefault().CeremonyReward;
					Require.True(ItemCount(session.Api.World, reward.ItemId) == 1, "The ceremony did not pay the Karmic Staff.");
					await EquipUpgradesAsync(token);
					await session.SynchronizeAsync(token);
					Require.True(session.Api.World.Inventory.Values.Any(item => item.ItemId == reward.ItemId &&
						(item.Details.EquippedSlot ?? 0) > 0), "The Karmic Staff was not equipped after the ceremony.");
				}
				session.TraceDiagnostic("ascension-bridge-step", new Dictionary<string, object?>
				{
					["step"] = step.Key, ["map"] = session.Api.World.MapId, ["position"] = session.CurrentPosition,
					["quest"] = session.Api.World.Quests.TryGetValue(step.QuestId, out BotQuestState? state) ? $"{state.Status}/{state.StepAndFlags}" : "done",
				});
			}

			async Task<int> ApproachBridgeNpcAsync(NaturalAscensionStep step, BotPosition anchor, NaturalJourneyNavigator here)
			{
				// NA-19: a travel leg starts; the straight line is a lower bound of the planned route.
				await combat.BuffOurselfAsync(NaturalHelpTrigger.TravelLeg, token, Distance(session.CurrentPosition, anchor));
				NaturalNavigationResult approach = await NaturalIshalgenNavigator.ApproachNpcAsync(step.MapId, step.NpcId, anchor, here, token);
				Require.True(approach.Arrived, $"{step.Key}: {approach.Reason}");
				return Require.IsType<int>(approach.TargetObjectId);
			}

			async Task<int> ApproachAsync(int templateId, BotPosition anchor)
			{
				NaturalNavigationResult result = await NaturalIshalgenNavigator.ApproachNpcAsync(
					contract.MapId, templateId, anchor, navigator, token);
				Require.True(result.Arrived, result.Reason);
				return Require.IsType<int>(result.TargetObjectId);
			}

			async Task BindAtAldelleIfNeededAsync()
			{
				if (session.Api.World.ObeliskBindPoint is { MapId: 220010000 } bound &&
					Distance(bound.Position, new BotPosition(587.688f, 2467.1f, 278.788f, 0)) < 20)
					return;
				int fee = runtime.Data.BindPointDataDh.GetBindPointTemplate(700063)?.GetPrice() ?? int.MaxValue;
				if (session.Api.World.Kinah < fee) return;
				session.BeginStep("ni07-bind-aldelle", "register-ordinary-aldele-obelisk");
				int obelisk = await ApproachShippedSpawnAsync(700063);
				// NA-08: the map-agnostic bind step (the Altgard Fortress bind uses it too).
				BotPosition obeliskPosition = session.Api.World.Objects[obelisk].Position;
				NaturalServiceOutcome bindOutcome = await new NaturalServiceSteps(session).BindAsync(obelisk, obeliskPosition,
					contract.MapId, fee, acceptRange: 5, token);
				Require.True(bindOutcome.IsDone, bindOutcome.Reason);
				Require.True(session.Api.World.ObeliskBindPoint is { MapId: 220010000 } registered &&
					Distance(registered.Position, session.CurrentPosition) < 20);
			}

			async Task UseFasterTravelAsync(BotPosition destination, bool forceHubFlight = false)
			{
				if ((!options.OptimizeHubs && !forceHubFlight) || session.Api.World.MapId != contract.MapId ||
					session.Api.World.IsDead) return;
				long fare = session.Api.World.VendorPrices?.ServicePrice(160) ?? 160;
				NaturalTravelChoice choice = NaturalJourneyTravelPolicy.Choose(
					session.CurrentPosition, destination, session.Api.World.ObeliskBindPoint,
					!forceHubFlight && session.Api.World.Skills.ContainsKey(243) &&
						session.Api.Timing.TimeUntilCast(243) <= TimeSpan.FromSeconds(1) &&
						runtime.NowMillis - lastReturnMillis >= TimeSpan.FromMinutes(20).TotalMilliseconds + 1000,
					session.Api.World.Kinah >= fare);
				if (choice == NaturalTravelChoice.Walk) return;
				session.TraceDiagnostic("journey-travel-choice", new Dictionary<string, object?>
				{
					["choice"] = choice.ToString(), ["from"] = session.CurrentPosition,
					["destination"] = destination, ["fare"] = fare,
				});
				if (choice == NaturalTravelChoice.Return)
				{
					await UseLearnedReturnToBindAsync();
					return;
				}
				int npcId = choice == NaturalTravelChoice.FlightToAnturoon ? 203513 : 203545;
				int locationId = choice == NaturalTravelChoice.FlightToAnturoon ? 18 : 17;
				int pathId = choice == NaturalTravelChoice.FlightToAnturoon ? 11 : 12;
				var path = runtime.Data.FlyPathDataDh.GetPathTemplate(pathId)
					?? throw new InvalidDataException($"Java 4.8 flight path {pathId} is missing.");
				BotPosition departure = new(path.GetStartX(), path.GetStartY(), path.GetStartZ(), 0);
				BotPosition arrival = new(path.GetEndX(), path.GetEndY(), path.GetEndZ(), 0);
				session.BeginStep($"ni07-flight-{pathId}", "take-cheaper-faster-flight-transporter");
				int transporter = await ApproachAsync(npcId, departure);
				if (Distance(session.CurrentPosition, departure) > 6)
				{
					NaturalNavigationResult atPath = await NaturalIshalgenNavigator.ExploreAnchorAsync(
						contract.MapId, -1, departure, navigator, "flight-departure", token);
					Require.True(atPath.Arrived, atPath.Reason);
				}
				Require.True(Distance(session.CurrentPosition, departure) <= 7,
					"Flight validator requires the client to stand by the path start.");
				await session.SendPacketAsync(session.Api.Teleport(transporter, locationId), token);
				await session.WaitForPacketAsync(typeof(SM_EMOTION), token, packet =>
					packet.Get<int>("senderObjectId") == session.CharacterId &&
					packet.Get<byte>("emotionType") == (byte)EmotionType.START_FLYTELEPORT &&
					packet.Get<int>("teleportId") == pathId * 1000 + 1);
				await session.ExecuteMovementAsync(CapitalAscensionScenario.CreateQuestFlight(
					session.CurrentPosition, arrival, contract.MapId,
					TimeSpan.FromMilliseconds(path.GetTimeInMs())), token);
				await session.SendPacketAsync(GameClientPackets.Emotion((byte)EmotionType.LAND_FLYTELEPORT), token);
				await session.SynchronizeAsync(token);
				Require.True(Distance(session.CurrentPosition, arrival) < 8,
					"Flight landing was not client-observed at the destination.");
			}


			bool SpawnsOnMap(int templateId) =>
				graph.GetMap(contract.MapId)!.Waypoints.Any(waypoint => waypoint.TemplateId == templateId);
			bool UsesProvenWarlockSpawn(int templateId) => altgardLegId == "l10" && templateId == 210538;

			// Of several kinds that serve the same objective, the one to hunt next: a live one in view first, else the kind whose
			// shipped spawn lies nearest (Q2230's tusks drop from six mosbear kinds; always the first meant long walks past others).
			int NearestKind(IReadOnlyList<int> kinds)
			{
				if (haramelKind != null && altgardLeg?.Haramel?.MapId == session.Api.World.MapId) return haramelKind(kinds);
				bool Connected(BotPosition at) => altgardLeg?.PillarFlight is not { } pillar || pillar.IsUpper(session.CurrentPosition.Z) ||
					geometry.GroundAround(contract.MapId, at, [3f, 5f, 8f, 12f]).Any(point => Distance(point, at) <= 6 &&
						geometry.OnSameIsland(contract.MapId, session.CurrentPosition, point));
				BotKnownObject? seen = session.Api.World.Objects.Values
					.Where(known => known.Kind == BotKnownObjectKind.Npc && known.TemplateId is int id && kinds.Contains(id) && !known.IsCorpse &&
						!navigator.UnavailableObjects.Contains(known.ObjectId) && Connected(known.Position))
					.OrderBy(known => Distance(known.Position, session.CurrentPosition)).FirstOrDefault();
				if (seen?.TemplateId is int visible) return visible;
				return graph.GetMap(contract.MapId)!.Waypoints.Where(waypoint => waypoint.TemplateId is int id && kinds.Contains(id) && Connected(waypoint.Position))
					.OrderBy(waypoint => Distance(waypoint.Position, session.CurrentPosition)).Select(waypoint => waypoint.TemplateId!.Value)
					.DefaultIfEmpty(kinds.First(SpawnsOnMap)).First();
			}

			/// <param name="withinRange">Stop this far from the observed NPC instead of at arm's reach, outside its
			/// aggro circle, so the fight can be planned (a pull) rather than started by walking into it.</param>
			async Task<int> ApproachShippedSpawnAsync(int templateId, bool skipBlockedTarget = false, float? withinRange = null,
				bool returnedFromStrand = false, Func<int?>? completedSource = null, bool acceptObservedKill = false)
			{
				if (bookCollectionUnderway && templateId == 210404 && session.Api.World.MapId != 220010000)
					throw new NaturalBookCollectionMapChangedException();
				int sourceHistoryStart = session.PacketHistory.Count;
				HashSet<int> sourceObjects = session.Api.World.Objects.Values.Where(known => known.TemplateId == templateId)
					.Select(known => known.ObjectId).ToHashSet();
				int? SourceKilledOnTheWay()
				{
					foreach (DecodedBotServerPacket packet in session.PacketHistory.Skip(sourceHistoryStart).Where(packet =>
						packet.PacketType == typeof(SM_NPC_INFO) && packet.Get<int>("npcId") == templateId))
						sourceObjects.Add(packet.Get<int>("objectId"));
					// Java SM_ATTACK_STATUS 19..23 carries MP; its zero percentage is not a kill.
					return session.PacketHistory.Skip(sourceHistoryStart).Where(packet => packet.PacketType == typeof(SmAttackStatus) &&
						packet.Get<byte>("typeId") is not (19 or 20 or 21 or 22 or 23) &&
						packet.Get<byte>("hpOrMp") == 0 && sourceObjects.Contains(packet.Get<int>("objectId")))
						.Select(packet => (int?)packet.Get<int>("objectId")).LastOrDefault();
				}
				int? CompletedApproachSource()
				{
					if (completedSource?.Invoke() is int collectedFrom)
					{
						emptySpawnWaits = 0;
						return collectedFrom;
					}
					// BC-06: navigation defense can kill the requested source before this approach selects it.
					// Return that observed kill to the ordinary objective loop, including a partially collected stack.
					if (acceptObservedKill && SourceKilledOnTheWay() is int killedSource)
					{
						emptySpawnWaits = 0;
						session.TraceDiagnostic("source-killed-during-approach", new Dictionary<string, object?>
						{
							["templateId"] = templateId, ["objectId"] = killedSource,
						});
						return killedSource;
					}
					return null;
				}
				if (haramelApproach != null && altgardLeg?.Haramel?.MapId == session.Api.World.MapId)
					return await haramelApproach(templateId, withinRange ?? MathF.Min(5, runtime.Data.NpcDataDh.GetNpcTemplate(templateId)?.GetTalkDistance() ?? 3), CompletedApproachSource);
				// Dead on entry (a use bar or a walk ended in a death nobody handled): revive and recover first.
				if (session.Api.World.IsDead || session.Api.World.CurrentHp <= 0) await RestSafelyAsync(token);
				bool Connected(BotWaypoint waypoint) => templateId is < 210000 or >= 700000 ||
					altgardLeg?.PillarFlight is not { } pillar || pillar.IsUpper(session.CurrentPosition.Z) ||
					geometry.GroundAround(contract.MapId, waypoint.Position, [3f, 5f, 8f, 12f]).Any(point => Distance(point, waypoint.Position) <= 6 &&
						geometry.OnSameIsland(contract.MapId, session.CurrentPosition, point));
				BotWaypoint[] anchors = graph.GetMap(contract.MapId)!.Waypoints
					.Where(waypoint => waypoint.TemplateId == templateId)
					// BC-07 observed two ordinary kills at this lower camp, including its 300-second respawn.
					// The continuous run repeatedly died re-entering the western camp after its first kill.
					// Farm the proved shipped source; wait for its real respawn rather than cross more camps.
					.Where(waypoint => !UsesProvenWarlockSpawn(templateId) ||
						Distance(waypoint.Position, new BotPosition(2400.88f, 2171.88f, 270.328f, 2)) < 1)
					.OrderByDescending(Connected).ThenBy(waypoint => Distance(session.CurrentPosition, waypoint.Position)).ToArray();
				if (anchors.Length == 0) throw new InvalidDataException($"Shipped spawn graph has no NPC {templateId}.");
				// AG-07: from far off, the navigator's hazard replanning can circle over monster ground for its whole budget (the
				// Leg 6 smoke run: 1,000 segments between Trader's Berth and Gerger, across the angolems). An Altgard leg walks its
				// road there first, fighting what engages, as its talk steps and hunts do when the navigator gives up.
				if (farApproach != null && !farApproachUnderway && (Distance(session.CurrentPosition, anchors[0].Position) > FarApproachMetres ||
					altgardLeg?.PillarFlight is { } pillar && pillar.IsUpper(session.CurrentPosition.Z) != pillar.IsUpper(anchors[0].Position.Z)))
				{
					farApproachUnderway = true;
					try { await farApproach(anchors[0].Position, templateId, altgardLegId == "l10" ? () => CompletedApproachSource() != null : null); }
					finally { farApproachUnderway = false; }
					anchors = [.. anchors.OrderByDescending(Connected).ThenBy(waypoint => Distance(session.CurrentPosition, waypoint.Position))];
				}
				if (CompletedApproachSource() is int roadSource) return roadSource;
				var reasons = new List<string>();
				foreach (BotWaypoint anchor in anchors.Take(12))
				{
					var progress = new NaturalApproachProgress();
					for (int guardClears = 0; guardClears <= NaturalApproachProgress.MaximumAttempts; guardClears++)
					{
						BotPosition beforeApproach = session.CurrentPosition;
						int killsBeforeApproach = navigator.UnavailableObjects.Count;
						NaturalNavigationResult result = withinRange is float range
							? await NaturalIshalgenNavigator.ExploreWithinRangeAsync(contract.MapId, templateId, anchor.Position, range, navigator, "NPC", token,
								stopWhen: () => CompletedApproachSource() != null)
							: await NaturalIshalgenNavigator.ApproachNpcAsync(contract.MapId, templateId, anchor.Position, navigator, token);
						if (CompletedApproachSource() is int collectedFrom) return collectedFrom;
						if (result.Arrived && result.TargetObjectId is int objectId) { emptySpawnWaits = 0; return objectId; }
						if (result.Arrived) // explore mode reached the hint with nothing in view: same as an empty hint
							result = new(false, $"Reached the spawn hint but no NPC was observed within {withinRange:F0} m.", null, result.RouteSearches, result.Segments);
						// Monsters on the way are not a wall: fight through to the objective (observed or its
						// shipped anchor) one pull at a time before giving up on this spawn hint.
						if (progress.CanRetry(guardClears) && result.Reason is "No collision-checked route to the current destination." or
								"New client-observed hazards exceeded the bounded replan budget." or NaturalIshalgenNavigator.RepeatedRouteReason)
						{
							NaturalNavigationObject? seen = result.TargetObjectId is int blockedObjectId
								? navigator.Observe().Npcs.FirstOrDefault(npc => npc.ObjectId == blockedObjectId) : null;
							int revivesBefore = combat.ReviveCount;
							bool cleared = await TryClearObservedBlockerAsync(seen?.Position ?? anchor.Position, seen?.ObjectId);
							if (combat.ReviveCount > revivesBefore)
							{
								// A death moved the client back to its bind point. None of this hint's
								// failed route observations apply there; recover and plan the journey again.
								await RestSafelyAsync(token);
								return await ApproachShippedSpawnAsync(templateId, skipBlockedTarget, withinRange, completedSource: completedSource, acceptObservedKill: acceptObservedKill);
							}
							bool progressed = progress.Observe(Distance(beforeApproach, session.CurrentPosition),
								navigator.UnavailableObjects.Count > killsBeforeApproach);
							if (CompletedApproachSource() is int clearedSource) return clearedSource;
							if (cleared || progressed)
							{
								// Returning from bind may need more than eight guards. Count stalls, not successful
								// fights or checked walking progress, against the local recovery budget.
								// A failed pull can still move to a new approach: re-observe and route from there.
								if (!cleared)
									session.TraceDiagnostic("guard-clear-replan-after-progress", new Dictionary<string, object?>
									{
										["templateId"] = templateId, ["from"] = beforeApproach,
										["position"] = session.CurrentPosition, ["attempt"] = guardClears + 1,
									});
							continue;
							}
							// A moving patrol can briefly close every checked route. Re-observe after
							// it has had time to move, but do not wait indefinitely at one hint.
							if (progress.StalledAttempts <= 3)
							{
								session.TraceDiagnostic("guard-clear-wait-and-retry", new Dictionary<string, object?>
								{
									["templateId"] = templateId, ["position"] = session.CurrentPosition,
									["stall"] = progress.StalledAttempts,
								});
								await session.AdvanceAsync(TimeSpan.FromSeconds(10), token);
								await session.SynchronizeAsync(token);
								continue;
							}
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
				// Every hint empty: wait through the shipped respawn, including the Heart debris's 295 s.
				// BC-06: navigation defense can kill a newly respawned source before this approach selects it.
				// Give that kill its own respawn wait; the prior hunter's exhausted budget is not a stall here.
				if (altgardLegId == "l10" && SourceKilledOnTheWay() != null)
				{
					emptySpawnWaits = 0;
					session.TraceDiagnostic("spawn-wait-reset-after-source-kill", new Dictionary<string, object?> { ["templateId"] = templateId });
				}
				int respawnSeconds = runtime.Data.SpawnsDh.GetSpawnsByWorldId(contract.MapId)
					.Where(group => group.GetNpcId() == templateId).Select(group => group.GetRespawnTime()).DefaultIfEmpty(180).Max();
				int waitRounds = Math.Max(4, (int)Math.Ceiling(respawnSeconds / 60d) + 1);
				if (reasons.Count > 0 && reasons.All(reason => reason.Contains("no NPC was observed", StringComparison.Ordinal)) &&
					++emptySpawnWaits <= waitRounds)
				{
					session.TraceDiagnostic("spawn-wait", new Dictionary<string, object?>
					{
						["templateId"] = templateId, ["wait"] = emptySpawnWaits, ["position"] = session.CurrentPosition,
					});
					await RestSafelyAsync(token);
					if (CompletedApproachSource() is int sweptSource) return sweptSource;
					if (altgardLegId == "l10" && SourceKilledOnTheWay() != null) emptySpawnWaits = 0;
					if (altgardLegId == "l10")
					{
						// A patrol can respawn next to the waiting player. Observe and defend throughout this
						// minute instead of advancing its attacks for sixty seconds without a player response.
						long until = runtime.NowMillis + 60000;
						while (runtime.NowMillis < until)
						{
							await session.AdvanceAsync(TimeSpan.FromMilliseconds(Math.Min(1000, until - runtime.NowMillis)), token);
							await session.SynchronizeAsync(token);
							if (session.Api.World.IsDead) await RestSafelyAsync(token);
							await DefendAgainstEngagedAsync("source-respawn-wait");
							if (CompletedApproachSource() is int respawnSource) return respawnSource;
						}
					}
					else await session.AdvanceAsync(TimeSpan.FromSeconds(60), token);
					await session.SynchronizeAsync(token);
					return await ApproachShippedSpawnAsync(templateId, skipBlockedTarget, withinRange, completedSource: completedSource, acceptObservedKill: acceptObservedKill);
				}
				emptySpawnWaits = 0;
				// AK-08: no hint has a route from here at all: a fight left the Cleric on ground the navmesh does not connect (smoke
				// run 7: 20 m above MuMu Village). Cast Return, as the road walk does, and try once more from the bind.
				if (!returnedFromStrand && reasons.Count > 0 && session.Api.World.Skills.ContainsKey(243) &&
					reasons.All(reason => reason == "No collision-checked route to the current destination."))
				{
					session.TraceDiagnostic("stranded-return", new Dictionary<string, object?>
					{
						["templateId"] = templateId, ["position"] = session.CurrentPosition,
					});
					await UseLearnedReturnToBindAsync();
					await RestSafelyAsync(token);
					return await ApproachShippedSpawnAsync(templateId, skipBlockedTarget, withinRange, returnedFromStrand: true, completedSource: completedSource, acceptObservedKill: acceptObservedKill);
				}
				throw new InvalidDataException($"No client-observed NPC {templateId} at {Math.Min(12, anchors.Length)} shipped spawn hints " +
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
						int revivesBefore = combat.ReviveCount;
						NaturalNavigationResult result = await NaturalIshalgenNavigator.ApproachNpcAsync(
							contract.MapId, templateId, anchor.Position, navigator, token);
						if (combat.ReviveCount > revivesBefore)
							throw new NaturalGuardedObjectiveRevivedException(
								$"Priest revived while approaching guarded NPC {templateId} from {anchor.Position}.");
						if (result.Arrived && result.TargetObjectId is int objectId) return objectId;
						reasons.Add(result.Reason);
						if (result.Reason is not ("No collision-checked route to the current destination." or
							"New client-observed hazards exceeded the bounded replan budget.") ||
							cleared == maximumGuardClears) break;
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
				.Select(npc => (npc, template: runtime.Data.NpcDataDh.GetNpcTemplate(npc.TemplateId)))
				.Where(entry => runtime.IsAggressive(entry.template))
				.Select(entry => new NaturalPullMonster(entry.npc, entry.template!.GetAggroRange(),
					entry.template.GetTribe().ToString(),
					entry.template.GetBoundRadius().GetMaxOfFrontAndSide()))
				.ToArray();
			NaturalPullMonster PullMonsterOf(NaturalNavigationObject npc)
			{
				var template = runtime.Data.NpcDataDh.GetNpcTemplate(npc.TemplateId);
				return new NaturalPullMonster(npc, runtime.AggroRadius(template),
					template?.GetTribe().ToString() ?? "NONE",
					template?.GetBoundRadius().GetMaxOfFrontAndSide() ?? 0);
			}
			bool CanSupport(string helper, string asking) =>
				Enum.TryParse(helper, out Aion.GameServer.Model.TribeClass h) && Enum.TryParse(asking, out Aion.GameServer.Model.TribeClass a) &&
				runtime.Data.TribeRelations.CanSupport(h, a);

			// Monsters already on the bot (attacked it in the recent packet window and still within 30 m), pursuers
			// still closing in (over 10 m from where they were first seen, and nearer the bot than that spot), and
			// monsters whose aggro circle the bot stands in (one that just respawned beside it, as a Q2007 stalker
			// did). A player deals with these before pulling anything new. One that stays quiet (out of sight) is
			// waited for only once (quietNeighbours).
			(int[] Attackers, int[] Pursuers) Engaged()
			{
				var observed = navigator.Observe().Npcs.ToDictionary(npc => npc.ObjectId);
				var active = new HashSet<int>();
				NaturalCombatRetreatPolicy.ObserveEngagement(active, session.PacketHistory.TakeLast(400), session.CharacterId,
					id => observed.TryGetValue(id, out var npc) ? runtime.Data.NpcDataDh.GetNpcTemplate(npc.TemplateId)?.GetL10n() : null);
				int[] attackers = active
					.Where(id => observed.TryGetValue(id, out var npc) && Distance(session.CurrentPosition, npc.Position) < 30)
					.ToArray();
				int[] pursuers = ObservedPullMonsters()
					.Where(m => !attackers.Contains(m.Npc.ObjectId) && Distance(session.CurrentPosition, m.Npc.Position) < 45)
					// A known walker can travel >10 m from its first SM_NPC_INFO on its normal Java
					// route (NpcMoveController.isNextRouteStepChosen). Displacement alone does not
					// show pursuit; an actual attack or an overlapping aggro circle is checked below.
					.Where(m => m.Npc.PatrolPath is not { Count: > 0 })
					.Where(m => session.PacketHistory.FirstOrDefault(packet => packet.PacketType == typeof(SM_NPC_INFO) &&
						packet.Get<int>("objectId") == m.Npc.ObjectId) is { } info &&
						new BotPosition(info.Get<float>("x"), info.Get<float>("y"), info.Get<float>("z"), 0) is var home &&
						Distance(home, m.Npc.Position) > 10 &&
						Distance(session.CurrentPosition, m.Npc.Position) < Distance(session.CurrentPosition, home))
					.Select(m => m.Npc.ObjectId).ToArray();
				int[] neighbours = ObservedPullMonsters()
					.Where(m => !attackers.Contains(m.Npc.ObjectId) && !pursuers.Contains(m.Npc.ObjectId) &&
						!quietNeighbours.Contains(m.Npc.ObjectId) && m.AggroRadius > 0 &&
						new BotNavigationHazard(m.Npc.Position, m.AggroRadius).Contains(session.CurrentPosition))
					.Select(m => m.Npc.ObjectId).ToArray();
				return (attackers, [.. pursuers, .. neighbours]);
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
					await NaturalDialogProtocol.OpenAsync(session, objectId, token);
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
					if (completed && (await TryLootCorpseItemAsync(session, objectId, itemId, token, start) ||
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
						await RestSafelyAsync(token);
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
			async Task ClearAroundSpotAsync(BotPosition? objective, int? objectiveObjectId, string purpose, IReadOnlyCollection<int>? preservedKinds = null)
			{
				if (objective is not BotPosition spot) return;
				// Clear the camp only once the bot has rejoined it. After a bind revive, old camp NPCs
				// may still be in the client model despite being over a kilometre away.
				if (Distance(session.CurrentPosition, spot) > 80) return;
				bool cleared = false;
				for (int pull = 0; pull < 20 && !session.Api.World.IsDead; pull++)
				{
					// A patrol counts wherever on its observed path it is now: it walks past the objective sooner or later.
					NaturalNavigationObject[] near = ObservedPullMonsters(objectiveObjectId)
						.Where(m => preservedKinds == null || !preservedKinds.Contains(m.Npc.TemplateId))
						.Where(m => m.Npc.PossiblePositions().Any(at =>
							Distance(at, spot) <= m.AggroRadius + NaturalPullPlanner.SupportRangeOffset + 20))
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
						await RestSafelyAsync(token);
					else
						await combat.BuffOurselfAsync(NaturalHelpTrigger.PrePull, token);
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
				int pursuerWaits = 0, blockedWaits = 0;
				for (int round = 0; round < NaturalJourneyCombat.MaximumCombatActions; round++)
				{
					var (attackers, pursuers) = Engaged();
					if (attackers.Length == 0 && pursuers.Length == 0) return true;
					if (attackers.Length == 0)
					{
						// Let a chaser arrive and commit rather than walking into a fresh pull with it behind us.
						await session.AdvanceAsync(TimeSpan.FromSeconds(2), token);
						await session.SynchronizeAsync(token);
						if (++pursuerWaits >= 3)
						{
							quietNeighbours.UnionWith(pursuers);
							return true; // it turned back (leash/give-up) or never saw the Priest; carry on
						}
						continue;
					}
					pursuerWaits = 0;
					int attacker = ChooseEngagedTarget(attackers, session, mauPolicy.PreferWoundedWhenTwoAttackers);
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
					bool killed;
					// BC-06: Engaged found these hits in the recent packet window. Carry that same evidence
					// into combat; starting after the triggering hits falsely describes a pack as zero attackers.
					int? engagementStart = altgardLegId == "l10" ? Math.Max(0, session.PacketHistory.Count - 400) : null;
					try { killed = await combat.TryKillAsync(attacker, token, session.CurrentPosition, engagementStart); }
					catch (NaturalCombatApproachBlockedException) when (
						navigator.Observe().Npcs.Any(npc => npc.ObjectId == attacker &&
							Distance(session.CurrentPosition, npc.Position) <= NaturalPullPlanner.SpellRange))
					{
						// A ranged aggressor can be in Smite range across a blocked seam. Wait
						// for the spell cooldown instead of ending the whole quest or entering adds.
						await session.AdvanceAsync(TimeSpan.FromSeconds(2), token);
						await session.SynchronizeAsync(token);
						continue;
					}
					catch (NaturalCombatApproachBlockedException exception)
					{
						// AB-08: a retreat left the attacker out of range behind its pack (Sumarhon's camp): no checked way back.
						// Let it come to us, as a pursuer, and when it does not, leave it to the caller's next plan.
						session.TraceDiagnostic("defend-approach-blocked", new Dictionary<string, object?>
						{
							["purpose"] = purpose, ["attacker"] = attacker, ["waits"] = blockedWaits, ["reason"] = exception.Message,
							["position"] = session.CurrentPosition,
						});
						if (combat.ReviveCount > revivesBefore || session.Api.World.IsDead) return false;
						if (++blockedWaits >= 3)
						{
							quietNeighbours.Add(attacker);
							return true;
						}
						await session.AdvanceAsync(TimeSpan.FromSeconds(2), token);
						await session.SynchronizeAsync(token);
						continue;
					}
					if (combat.ReviveCount > revivesBefore || session.Api.World.IsDead) return false;
					if (killed) navigator.UnavailableObjects.Add(attacker);
					else return true; // combat decided otherwise (retreat); let the caller re-plan
				}
				throw new InvalidDataException("Priest could not clear engaged attackers before resting or pulling.");
			}

			async Task LootQuestItemsAroundAsync(CancellationToken lootToken)
			{
				BotWorldModel world = session.Api.World;
				int[] lootable = world.LootStatuses.Where(entry => entry.Value == 0 && !lootSwept.Contains(entry.Key))
					.Select(entry => entry.Key).ToArray();
				foreach (int corpse in lootable)
				{
					if (world.IsDead || Engaged().Attackers.Length > 0) return;
					string? skipped = null;
					if (!world.Objects.TryGetValue(corpse, out BotKnownObject? seen) || seen.Kind != BotKnownObjectKind.Npc)
					{
						lootSwept.Add(corpse); // gone (despawned or out of sight for good)
						skipped = "gone";
					}
					else if (Distance(seen.Position, session.CurrentPosition) > LootSweepReach)
					{
						// A ranged fight can end far from the first kill; leave it for a later sweep unless it is far behind.
						if (Distance(seen.Position, session.CurrentPosition) > 60) lootSwept.Add(corpse);
						skipped = "far";
					}
					else if (Distance(seen.Position, session.CurrentPosition) > 4 && world.MapId is int map &&
						geometry.SnapToGround(map, seen.Position with { Z = seen.Position.Z + 2 }) is { } ground)
					{
						IReadOnlyList<BotPosition> path = geometry.FindLocalPath(map, session.CurrentPosition, ground);
						if (path.Count == 0 || !navigator.IsSegmentSafe(path, corpse)) skipped = "no-safe-path"; // try again later
						else
						{
							await navigator.MoveAsync(path, lootToken);
							await session.SynchronizeAsync(lootToken);
						}
					}
					if (skipped != null)
					{
						session.TraceDiagnostic("quest-loot-skip", new Dictionary<string, object?>
						{
							["corpse"] = corpse, ["npcId"] = seen?.TemplateId, ["reason"] = skipped,
							["distance"] = seen == null ? null : Distance(seen.Position, session.CurrentPosition),
						});
						continue;
					}
					lootSwept.Add(corpse);
					// BC-06: several corpses can receive a drop while the bag is below its required count.
					// Check the current stack before each take rather than collecting an extra from an older corpse.
					IReadOnlyList<int> taken = await LootQuestItemsAsync(session, corpse, runtime.Data, lootToken,
						altgardLegId == "l10" ? QuestDropStillNeeded : null,
						altgardLeg?.Haramel?.TowerChestKeys?.Select(key => key.ItemId).ToHashSet());
					session.TraceDiagnostic("quest-loot-sweep", new Dictionary<string, object?>
					{
						["corpse"] = corpse, ["npcId"] = seen!.TemplateId, ["taken"] = taken, ["position"] = session.CurrentPosition,
					});
				}
			}

			async Task RestSafelyAsync(CancellationToken restToken)
			{
				restToken.ThrowIfCancellationRequested();
				if (!session.Api.World.IsDead && session.Api.World.CurrentHp > 0)
					await DefendAgainstEngagedAsync("before-rest");
				await combat.RestAsync(restToken);
				// AO-04: a camp fight can defer every kill's loot while another attacker is engaged. Revisit those
				// corpses once recovery settles, before leaving or searching for a drop source already killed as an add.
				if (!session.Api.World.IsDead) await SweepQuestItemsWhenSafeAsync(restToken);
			}

			// Observe and defend while waiting for a patrol to pass instead of leaving its attacks unanswered.
			async Task<bool> WaitBeforePullDefendingAsync(int milliseconds, string purpose)
			{
				long until = runtime.NowMillis + milliseconds;
				while (runtime.NowMillis < until)
				{
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(Math.Min(1000, until - runtime.NowMillis)), token);
					await session.SynchronizeAsync(token);
					if (session.Api.World.IsDead || !await DefendAgainstEngagedAsync(purpose + "-patrol-wait")) return false;
				}
				return true;
			}

			// Pull like a player: choose, among the given targets (earlier ones preferred on ties), the one and the
			// spot that bring the fewest helpers (Java assist rule), at spell range off to the side of the pack,
			// outside every circle; if helpers would still come, wait briefly for patrols to move. Then walk there
			// on a checked, hostile-free route. Returns the target to fight, or null when no spot is reachable.
			// A preference, not a wall: the spot where the Priest died is avoided when any other firing spot
			// works, but when it is the only way in (Rae's camp) the Priest still goes.
			async Task<NaturalPullPlan?> MoveToPullSpotAsync(IReadOnlyList<NaturalNavigationObject> targets,
				IReadOnlyList<BotPosition> stagingPoints, string purpose)
			{
				if (!await DefendAgainstEngagedAsync(purpose)) return null;
				if (session.Api.World.CurrentHp * 100 < session.Api.World.MaxHp * 80) await RestSafelyAsync(token);
				await combat.BuffOurselfAsync(NaturalHelpTrigger.PrePull, token);
				NaturalPullPlan? plan = null;
				// NA-22: the Cleric waits 15 s at a time, up to four times, then decides (NaturalPatrolPolicy); the Priest
				// keeps its baseline of short waits.
				bool cleric = combat.IsCleric;
				int waitCycles = cleric ? NaturalPatrolPolicy.MaximumWaits : mauPolicy.PatrolWaitCycles;
				for (int wait = 0; wait <= waitCycles; wait++)
				{
					NaturalPullMonster[] monsters = ObservedPullMonsters();
					var observed = navigator.Observe().Npcs.ToDictionary(npc => npc.ObjectId);
					NaturalPullMonster[] pullTargets = targets
						.Select(t => monsters.FirstOrDefault(m => m.Npc.ObjectId == t.ObjectId) ??
							(observed.TryGetValue(t.ObjectId, out NaturalNavigationObject? seen) ? PullMonsterOf(seen) : null))
						.OfType<NaturalPullMonster>().ToArray();
					if (pullTargets.Length == 0) return null;
					// Walking there only passes the monsters; the planner keeps the spot itself off every patrol's whole path.
					BotNavigationHazard[] hazards = monsters.SelectMany(m => m.Npc.Hazards(m.AggroRadius, BotPatrolPath.PassingReach)).ToArray();
					var reachable = new Dictionary<(int, int), bool>();
					bool avoidDeaths = combat.DeathSpots.Count > 0;
					// Not on a patrol's path or a respawn point (the shipped spawn circles): a fight of a minute or
					// more there meets the patrol or the respawn, as the Hatata fights that went wrong did.
					bool avoidSpawns = combat.HostileSpawns.Count > 0;
					var evaluatedPullCandidates = new List<NaturalPullCandidate>();
					NaturalPullPlan? PlanOnce() => NaturalPullPlanner.Plan(session.CurrentPosition, pullTargets, monsters, stagingPoints, CanSupport,
						(a, b) => geometry.HasLineOfSight(contract.MapId, a, b),
						point => geometry.SnapToGround(contract.MapId, point),
						spot =>
						{
							var key = ((int)MathF.Round(spot.X), (int)MathF.Round(spot.Y));
							// Navmesh-connected only: a grid shortcut can climb onto a rock top the navmesh keeps
							// as a separate island, stranding the bot for every later route.
							if (avoidDeaths && combat.DeathSpots.Any(death => Distance(death, spot) < DeathSpotAvoidance)) return false;
							if (avoidSpawns && combat.HostileSpawns.Any(spawn => spawn.DistanceTo(spot) <= spawn.Radius + NaturalPullPlanner.SupportRangeOffset)) return false;
							if (!reachable.TryGetValue(key, out bool ok))
								reachable[key] = ok = Distance(session.CurrentPosition, spot) < 1 ||
									(geometry.NavMesh is { } router && router.NavMeshes.Get(contract.MapId) is { } mesh
										? mesh.IslandOf(spot) >= 0 && mesh.IslandOf(spot) == mesh.IslandOf(session.CurrentPosition) &&
											router.FindPath(contract.MapId, session.CurrentPosition, spot,
												BotNavQuery.Default with { Hazards = hazards }).Count > 0
										: geometry.FindJourneyPathAvoiding(contract.MapId, session.CurrentPosition, spot, hazards).Count > 0);
							return ok;
						}, audit: evaluatedPullCandidates.Add,
						pullDistanceMeters: mauPolicy.PullDistanceMeters);
					plan = PlanOnce();
					if (plan == null && avoidSpawns)
					{
						avoidSpawns = false; // every clean spot is on a patrol path or respawn point: accept one
						plan = PlanOnce();
					}
					if (plan == null && avoidDeaths)
					{
						avoidDeaths = false; // the only way in passes where the Priest died: go anyway
						plan = PlanOnce();
					}
					session.TraceDiagnostic("pull-plan", new Dictionary<string, object?>
					{
						["purpose"] = purpose,
						["policyVersion"] = mauPolicy.Id,
						["seed"] = runtime.Seed,
						["candidateScope"] = "spots-evaluated-before-baseline-rank-cutoff",
						["chosenAction"] = plan == null ? "no-plan" :
							plan.Helpers.Count > 0 && wait < waitCycles ? "wait" : "pull",
						["observedState"] = new
						{
							position = session.CurrentPosition,
							targets = pullTargets.Select(target => new
							{
								target.Npc.ObjectId, target.Npc.TemplateId, target.Npc.Position,
								target.AggroRadius, target.Tribe, target.BoundRadius,
							}).ToArray(),
							monsters = monsters.Select(monster => new
							{
								monster.Npc.ObjectId, monster.Npc.TemplateId, monster.Npc.Position,
								monster.AggroRadius, monster.Tribe, monster.BoundRadius,
								possiblePositions = monster.Npc.PossiblePositions().ToArray(),
							}).ToArray(),
							avoidDeaths, avoidSpawns,
						},
						["candidateActions"] = evaluatedPullCandidates.Select(candidate => new
						{
							candidate.TargetObjectId, candidate.FiringPosition,
							candidate.ExpectedHelperObjectIds, candidate.ClearanceFromOthers,
							candidate.Legal, candidate.IllegalReason,
							BaselineRejection = !candidate.Legal ? candidate.IllegalReason :
								plan != null && plan.Helpers.Count > 0 && wait < waitCycles ?
								"Baseline waits for helpers to move." :
								plan?.Target.Npc.ObjectId == candidate.TargetObjectId &&
								plan.FiringPosition == candidate.FiringPosition ? null :
								"Baseline chose a better-ranked evaluated pull.",
						}).ToArray(),
						["wait"] = wait,
						["position"] = session.CurrentPosition,
						["candidates"] = pullTargets.Select(t => $"{t.Npc.TemplateId}/{t.Npc.ObjectId}").ToArray(),
						["target"] = plan == null ? null : $"{plan.Target.Npc.TemplateId}/{plan.Target.Npc.ObjectId}",
						["firingPosition"] = plan?.FiringPosition,
						["expectedHelpers"] = plan?.Helpers.Select(h => $"{h.Npc.TemplateId}/{h.Npc.ObjectId}").ToArray(),
						["clearance"] = plan?.ClearanceFromOthers,
					});
					if (plan == null || plan.Helpers.Count == 0) break;
					if (cleric)
					{
						NaturalPatrolDecision patrol = NaturalPatrolPolicy.Decide(ObservePatrol(plan, wait));
						session.TraceDiagnostic("patrol-decision", new Dictionary<string, object?>
						{
							["purpose"] = purpose, ["action"] = patrol.Action, ["wait"] = wait, ["winnable"] = patrol.Winnable,
							["reason"] = patrol.Reason, ["assessment"] = patrol.Assessment,
							["helpers"] = plan.Helpers.Select(h => $"{h.Npc.TemplateId}/{h.Npc.ObjectId}").ToArray(),
						});
						if (patrol.Action == "reroute") return null;
						if (patrol.Action != "wait") break; // fight, or pull anyway
						if (altgardLegId == "l10")
						{
							if (!await WaitBeforePullDefendingAsync(patrol.WaitMillis, purpose)) return null;
						}
						else
						{
							await session.AdvanceAsync(TimeSpan.FromMilliseconds(patrol.WaitMillis), token);
							await session.SynchronizeAsync(token);
						}
						continue;
					}
					if (wait == waitCycles) break;
					// A helper stands in range: patrols move, so give it a few seconds before accepting a chain pull.
					await session.AdvanceAsync(TimeSpan.FromSeconds(3), token);
					await session.SynchronizeAsync(token);
				}
				if (plan == null) return null;
				if (Distance(session.CurrentPosition, plan.FiringPosition) > 1.5f)
				{
					BotNavigationHazard[] hazards = ObservedPullMonsters()
						.SelectMany(m => m.Npc.Hazards(m.AggroRadius, BotPatrolPath.PassingReach)).ToArray();
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

			// NA-22: the client's view of a blocked pull for the Cleric's patrol decision.
			NaturalPatrolObservation ObservePatrol(NaturalPullPlan plan, int completedWaits)
			{
				BotWorldModel world = session.Api.World;
				bool Learned(string role, out NaturalPriestSkill? skill)
				{
					skill = NaturalPriestSkills.Best(role, world.Level, world.Skills, NaturalClericSkills.All);
					return skill != null;
				}
				bool heal = Learned("heal", out NaturalPriestSkill? healSkill) && world.CurrentMp >= healSkill!.ManaCost;
				bool hot = Learned("rejuvenation", out NaturalPriestSkill? hotSkill) && world.CurrentMp >= hotSkill!.ManaCost;
				bool salvation = Learned("salvation", out NaturalPriestSkill? salvationSkill) && world.CurrentDp >= salvationSkill!.DpCost;
				var effects = world.VisibleEffects ?? [];
				bool buffs = effects.Any(effect => NaturalPriestSkills.Ids("blessing").Contains(effect.SkillId)) &&
					effects.Any(effect => NaturalHelpItemPolicy.All.Any(item => item.SkillId == effect.SkillId &&
						item.EffectSlot == NaturalHelpItemPolicy.AwakeningSlot));
				int[] levels = plan.Helpers.Prepend(plan.Target)
					.Select(member => (int)(runtime.Data.NpcDataDh.GetNpcTemplate(member.Npc.TemplateId)?.GetLevel() ?? 0)).ToArray();
				// Rerouting through another corridor belongs to the route planner; the pull planner already chose the
				// spot with the fewest helpers, so here no other way is known.
				return new(combat.IsCleric, completedWaits, world.Level, world.CurrentHp, world.MaxHp, world.CurrentMp, world.MaxMp,
					levels, heal, hot, salvation, buffs, NaturalIshalgenPotionPolicy.TotalHealingCount(world.Inventory.Values),
					RerouteAvailable: false);
			}

			// Fight your way in: when observed monsters close every hostile-free route, take the route that fights
			// the least, walk its hostile-free prefix to a firing point just outside the first circle it enters,
			// and pull that one monster with ordinary combat. True after a kill (the caller re-observes and
			// re-plans); false when there is no terrain route or no safe pull.
			async Task<bool> TryFightThroughAsync(BotPosition objective, int? objectiveObjectId, HashSet<int> rejected)
			{
				// Never walk on with monsters on the Priest (a fight given up still has its attacker): fight them first.
				if (!await DefendAgainstEngagedAsync("fight-through")) return false;
				float AggroRadius(int templateId)
				{
					var template = runtime.Data.NpcDataDh.GetNpcTemplate(templateId);
					return runtime.IsAggressive(template) ? template.GetAggroRange() + 1f : 0f;
				}
				NaturalObservedMonster[] monsters = navigator.Observe().Npcs
					.Where(npc => npc.ObjectId != objectiveObjectId)
					.Select(npc => new NaturalObservedMonster(npc, AggroRadius(npc.TemplateId)))
					.Where(monster => monster.Radius > 0).ToArray();
				BotPosition origin = session.CurrentPosition;
				BotNavigationHazard[] fightHazards = monsters
					.SelectMany(m => m.Npc.Hazards(m.Radius, BotPatrolPath.PassingReach)).ToArray();
				IReadOnlyList<BotPosition> fightRoute = geometry.FindFightThroughPath(contract.MapId, origin,
					objective, fightHazards);
				if (fightRoute.Count == 0 && Distance(origin, objective) > 30)
				{
					// A long destination can exceed the local fight-through search while the next monster is
					// quite reachable. Plan to an observed forward guard first, then re-plan toward the objective.
					foreach (NaturalObservedMonster guard in monsters
						.Where(m => Distance(origin, m.Npc.Position) <= 65 &&
								Distance(m.Npc.Position, objective) < Distance(origin, objective) - 3)
						.OrderBy(m => Distance(origin, m.Npc.Position)).Take(12))
					{
						IReadOnlyList<BotPosition> leg = geometry.FindFightThroughPath(contract.MapId,
							origin, guard.Npc.Position, fightHazards);
						if (leg.Count == 0) continue;
						fightRoute = leg;
						session.TraceDiagnostic("fight-through-local-guard", new Dictionary<string, object?>
						{
							["objective"] = objective, ["guard"] = $"{guard.Npc.TemplateId}/{guard.Npc.ObjectId}",
							["routePoints"] = leg.Count, ["position"] = origin,
						});
						break;
					}
				}
				if (fightRoute.Count == 0)
				{
					// If even the nearby guard routes fail, take a short terrain-checked probe toward the
					// objective. Treat aggro as a cost (at most two new circles), defend after every two
					// points, and re-plan from the resulting client position. This is not a teleport or
					// permission to cross an unwalkable navmesh edge.
					var probes = geometry.GroundAround(contract.MapId, origin, [12f, 24f, 36f], 16)
						.Where(point => Distance(point, objective) < Distance(origin, objective) - 3 &&
							geometry.OnSameIsland(contract.MapId, origin, point))
						.OrderBy(point => Distance(point, objective)).Take(24)
						.Select(point => geometry.FindLocalPath(contract.MapId, origin, point).Take(6).ToArray())
						.Where(path => path.Length > 0 && Distance(origin, path[^1]) >= 2)
						.Select(path => new
						{
							Path = path,
							NewHazards = fightHazards.Count(hazard => !hazard.Contains(origin) &&
								path.Any(hazard.Contains)),
							Progress = Distance(origin, objective) - Distance(path[^1], objective),
						})
						.Where(probe => probe.NewHazards <= 2 && probe.Progress > 1)
						.OrderBy(probe => probe.NewHazards).ThenByDescending(probe => probe.Progress)
						.FirstOrDefault();
					if (probes == null) return false;
					session.TraceDiagnostic("fight-through-terrain-probe", new Dictionary<string, object?>
					{
						["objective"] = objective, ["from"] = origin, ["toward"] = probes.Path[^1],
						["newHazards"] = probes.NewHazards, ["routePoints"] = probes.Path.Length,
					});
					foreach (BotPosition[] segment in probes.Path.Chunk(2))
					{
						await navigator.MoveAsync(segment, token);
						await navigator.SynchronizeAsync(token);
						if (session.Api.World.IsDead || !await DefendAgainstEngagedAsync("fight-through-terrain-probe"))
							return false;
					}
					return Distance(origin, session.CurrentPosition) >= 2;
				}
				IReadOnlyList<NaturalObservedMonster> orderedBlockers =
					NaturalFightThrough.BlockersInOrder(session.CurrentPosition, fightRoute, monsters);
				// The first circle on this route must be cleared first. Skipping a refused
				// Stalker to pull a patrol behind it puts both onto the Priest.
				if (orderedBlockers.Count > 0 && rejected.Contains(orderedBlockers[0].Npc.ObjectId))
					return false;
				NaturalFightThroughBlocker? next = fightRoute.Count == 0 ? null
					: NaturalFightThrough.SelectNext(session.CurrentPosition, fightRoute, monsters, rejected);
				session.TraceDiagnostic("fight-through-plan", new Dictionary<string, object?>
				{
					["objective"] = objective,
					["position"] = session.CurrentPosition,
					["routePoints"] = fightRoute.Count,
					["blockers"] = orderedBlockers
						.Select(m => $"{m.Npc.TemplateId}/{m.Npc.ObjectId}").ToArray(),
					["next"] = next == null ? null : $"{next.Monster.Npc.TemplateId}/{next.Monster.Npc.ObjectId}",
					["firingPosition"] = next?.FiringPosition,
				});
				fightRouteOpen = fightRoute.Count > 0 && orderedBlockers.Count == 0;
				NaturalNavigationObject? walkBlocker = null;
				if (next == null)
				{
					// The walk below can end within arm's reach of the objective (2.8 m from Munin on his platform),
					// where no route of any kind is left to plan: report success so the navigator confirms arrival.
					if (Distance(session.CurrentPosition, objective) <= 3f) return true;
					// The strict router refused (a circle's clearance margin closes the last metres) but the
					// checked fight-through route enters no circle: walk it, segment by segment, as a player would.
					if (fightRouteOpen && Distance(session.CurrentPosition, objective) > 3 &&
						(fightRouteOpenAt is not BotPosition walked || Distance(walked, session.CurrentPosition) > 2))
					{
						fightRouteOpenAt = session.CurrentPosition;
						session.TraceDiagnostic("fight-through-walk", new Dictionary<string, object?>
						{
							["objective"] = objective, ["position"] = session.CurrentPosition, ["routePoints"] = fightRoute.Count,
						});
						foreach (BotPosition[] segment in fightRoute.Chunk(8))
						{
							if (!navigator.IsSegmentSafe(segment, objectiveObjectId))
							{
								// The segment brushes a circle the plan did not count (the margin, not the circle). A
								// player kills that monster and walks on: pull whichever one the segment touches.
								walkBlocker = monsters
									.Where(m => !rejected.Contains(m.Npc.ObjectId) && segment.Any(point =>
										// Horizontal: the server's aggro range ignores height (a peon under a deck still aggroes).
										m.Npc.PossiblePositions(BotPatrolPath.PassingReach).Any(at => MathF.Sqrt((point.X - at.X) * (point.X - at.X) +
											(point.Y - at.Y) * (point.Y - at.Y)) < m.Radius + 2)))
									.OrderBy(m => Distance(session.CurrentPosition, m.Npc.Position))
									.Select(m => m.Npc).FirstOrDefault();
								break;
							}
							await navigator.MoveAsync(segment, token);
							await navigator.SynchronizeAsync(token);
							if (session.Api.World.IsDead) return false;
						}
						if (walkBlocker == null) return true;
					}
					else return false;
				}
				// Pull the first monster the route meets before advancing into later circles.
				NaturalNavigationObject[] blockers = walkBlocker != null ? [walkBlocker]
					: next == null ? [] : [next.Monster.Npc];
				NaturalPullPlan? pull = await MoveToPullSpotAsync(blockers, next?.Staging ?? [], "fight-through");
				if (session.Api.World.IsDead) return false;
				bool OutOfReach(NaturalNavigationObject npc) =>
					Distance(session.CurrentPosition, npc.Position) > NaturalPullPlanner.SpellRange + 3 ||
					!geometry.HasLineOfSight(contract.MapId, session.CurrentPosition, npc.Position);
				NaturalNavigationObject? target;
				if (pull == null)
				{
					// No clean firing spot exists (a dense camp): a player fights the blocker in front of them from
					// where they stand rather than giving up. Take the nearest blocker already in reach.
					target = blockers.Select(b => navigator.Observe().Npcs.FirstOrDefault(npc => npc.ObjectId == b.ObjectId))
						.OfType<NaturalNavigationObject>().Where(npc => !OutOfReach(npc))
						.OrderBy(npc => Distance(session.CurrentPosition, npc.Position)).FirstOrDefault();
					if (target == null)
					{
						// Nothing in reach or in sight from here (the Methu nest hides the Mau 8.6 m away): walk the
						// checked fight-through route toward the nearest blocker until it is in spell range and sight,
						// as a melee player closes on the monster in front of them. An attack on the way is fought
						// through the navigator's own defence.
						NaturalNavigationObject? advance = blockers
							.Select(b => navigator.Observe().Npcs.FirstOrDefault(npc => npc.ObjectId == b.ObjectId))
							.OfType<NaturalNavigationObject>()
							.OrderBy(npc => Distance(session.CurrentPosition, npc.Position)).FirstOrDefault();
						const float PullRange = 30f;
						if (advance != null && fightRoute.Count > 1)
						{
							int closest = Enumerable.Range(0, fightRoute.Count)
								.OrderBy(i => Distance(fightRoute[i], advance.Position)).ThenBy(i => i).First();
							session.TraceDiagnostic("fight-through-advance", new Dictionary<string, object?>
							{
								["target"] = $"{advance.TemplateId}/{advance.ObjectId}",
								["distance"] = Distance(session.CurrentPosition, advance.Position),
								["routePoints"] = closest + 1,
								["position"] = session.CurrentPosition,
							});
							bool far = Distance(session.CurrentPosition, advance.Position) > PullRange;
							foreach (BotPosition[] pair in fightRoute.Take(closest + 1).Skip(1).Chunk(2))
							{
								await navigator.MoveAsync(pair, token);
								await navigator.SynchronizeAsync(token);
								if (session.Api.World.IsDead) return false;
								target = navigator.Observe().Npcs.FirstOrDefault(npc => npc.ObjectId == advance.ObjectId);
								if (target == null || !OutOfReach(target)) break;
								// Started far away: stop once within pull range and plan a proper pull from here.
								if (far && Distance(session.CurrentPosition, target.Position) <= PullRange) return true;
							}
						}
						if (target == null || OutOfReach(target))
						{
							rejected.Add(walkBlocker?.ObjectId ?? next?.Monster.Npc.ObjectId ?? advance?.ObjectId ?? -1);
							return true; // re-plan: nothing reachable to pull from here now
						}
					}
					session.TraceDiagnostic("fight-through-stand", new Dictionary<string, object?>
					{
						["target"] = $"{target.TemplateId}/{target.ObjectId}",
						["distance"] = Distance(session.CurrentPosition, target.Position),
						["position"] = session.CurrentPosition,
					});
				}
				else
					target = navigator.Observe().Npcs.FirstOrDefault(npc => npc.ObjectId == pull.Target.Npc.ObjectId);
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
					if (!await DefendAgainstEngagedAsync("fight-through-cleared")) return false;
					await combat.RestAsync(token);
					return combat.ReviveCount == revivesBefore;
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
					// A fight-through walk can end at the objective itself: hand back so the navigator confirms arrival.
					if (Distance(session.CurrentPosition, objective) <= 3f) return true;
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
					if (navigator.UnavailableObjects.Count > killsBefore ||
						Distance(session.CurrentPosition, objective) <= 3f) return true;
				}
				// A pack can block every checked detour even when a side guard's own
				// circle misses the straight objective line. Try the direct corridor
				// first, then a bounded twenty-metre shoulder of observed monsters.
				for (int corridorPass = 0; corridorPass < 3; corridorPass++)
				for (int attempt = 0; attempt < 4; attempt++)
				{
					float AggroRadius(int templateId)
					{
						var template = runtime.Data.NpcDataDh.GetNpcTemplate(templateId);
						return runtime.IsAggressive(template)
							? template.GetAggroRange() + 1f : 0f;
					}
					// A bind revive can leave distant camp NPCs in the client model until its known list
					// catches up. Only a nearby guard can be pulled from this position.
					IReadOnlyList<NaturalNavigationObject> observed = navigator.Observe().Npcs
						.Where(npc => Distance(session.CurrentPosition, npc.Position) <= 80).ToArray();
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

			async Task<bool> RevivedInsteadOfReturnAsync()
			{
				if (!session.Api.World.IsDead && session.Api.World.CurrentHp > 0) return false;
				session.TraceDiagnostic("natural-return-dead-revive-at-bind", new Dictionary<string, object?>
				{
					["position"] = session.CurrentPosition, ["hp"] = session.Api.World.CurrentHp,
				});
				await RestSafelyAsync(token); // revives at the bound obelisk, then rests
				return true;
			}

			async Task UseLearnedReturnToBindAsync()
			{
				// Java ReturnEffect uses the obelisk's map, including a city delivery's trip back to Altgard.
				int returnMap = session.Api.World.ObeliskBindPoint?.MapId ?? contract.MapId;
				// Java ce54b7931 ReturnEffect moves the player to the character's
				// bind location. Skill 243 is auto-learned at level 1 for all classes.
				// This is an ordinary player cast, not a setup or GM teleport.
				const int returnSkillId = 243;
				Require.True(session.Api.World.Skills.TryGetValue(returnSkillId, out BotSkill? learned),
					"The Priest did not observe the auto-learned Return skill.");
				session.BeginStep("ni07-natural-return", "cast-learned-return-after-checked-route-blocked");
				// NA-27 (LIVE): a monster can kill the bot on the very walk whose failure asked for Return, and Java refuses
				// a dead player's cast (CM_CASTSPELL: STR_SKILL_CANT_CAST, DEAD). A bind revive lands at the same bind
				// point Return goes to, so it replaces the cast; the death is recorded, not failed (OD-12).
				if (await RevivedInsteadOfReturnAsync()) return;
				// A checked route can fail again soon after a prior Return. Honor the
				// SM_CASTSPELL_RESULT cooldown while staying ready to fight nearby monsters.
				TimeSpan remainingCooldown = session.Api.Timing.TimeUntilCast(returnSkillId);
				if (remainingCooldown > TimeSpan.FromSeconds(5))
				{
					session.TraceDiagnostic("natural-return-cooldown-wait", new Dictionary<string, object?>
					{
						["remainingMillis"] = remainingCooldown.TotalMilliseconds,
						["position"] = session.CurrentPosition,
					});
					while ((remainingCooldown = session.Api.Timing.TimeUntilCast(returnSkillId)) > TimeSpan.FromSeconds(5))
					{
						await DefendAgainstEngagedAsync("natural-return-cooldown");
						if (session.Api.World.IsDead || session.Api.World.CurrentHp < session.Api.World.MaxHp * 0.75f)
							await RestSafelyAsync(token);
						remainingCooldown = session.Api.Timing.TimeUntilCast(returnSkillId);
						if (remainingCooldown <= TimeSpan.FromSeconds(5)) break;
						await session.AdvanceAsync(TimeSpan.FromMilliseconds(Math.Min(5000, remainingCooldown.TotalMilliseconds)), token);
						await session.SynchronizeAsync(token);
					}
				}
				BotPosition origin = session.CurrentPosition;
				int packetStart;
				DecodedBotServerPacket result;
				// A monster that reaches the bot during the cast interrupts it (Java Skill.cancelCast on damage); a
				// player fights it off, recovers and casts again.
				for (int attempt = 1; ; attempt++)
				{
					if (await RevivedInsteadOfReturnAsync()) return;
					packetStart = session.PacketHistory.Count;
					TimeSpan castGate = session.Api.Timing.TimeUntilCast(returnSkillId);
					if (castGate > TimeSpan.Zero) await session.AdvanceAsync(castGate + TimeSpan.FromMilliseconds(1), token);
					await session.SendPacketAsync(session.Api.Target(session.CharacterId), token);
					await session.SendPacketAsync(session.Api.Cast(runtime.CreateSpellCast(session.Api.World,
						session.CurrentPosition, returnSkillId, checked((byte)learned!.Level), session.CharacterId)), token);
					DecodedBotServerPacket started = await BotCastProtocol.WaitForStartAsync(
						(predicate, waitToken) => session.WaitForPacketAsync(packet => predicate(packet) ||
							BotCastProtocol.IsStartRejection(packet), waitToken),
						session.CharacterId, returnSkillId, token);
					Require.Equal(typeof(SM_CASTSPELL), started.PacketType);
					IReadOnlyList<BotKnownObject> visibleBefore = session.Api.World.SnapshotObjects();
					session.Api.World.BeginWorldReload();
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(started.Get<ushort>("castDuration") + 1), token);
					result = await BotCastProtocol.WaitForCompletionAsync(
						session.WaitForPacketAsync, session.CharacterId, returnSkillId, token);
					if (result.PacketType == typeof(SM_CASTSPELL_RESULT)) break;
					Require.Equal(typeof(SM_SKILL_CANCEL), result.PacketType);
					Require.True(attempt < 4, "Return was interrupted four times in a row.");
					session.Api.World.RestoreObjects(visibleBefore);
					session.TraceDiagnostic("natural-return-interrupted", new Dictionary<string, object?>
					{
						["attempt"] = attempt,
						["hp"] = session.Api.World.CurrentHp,
						["position"] = session.CurrentPosition,
					});
					await session.SynchronizeAsync(token);
					await DefendAgainstEngagedAsync("natural-return");
					await RestSafelyAsync(token); // revives when the defence was lost, rests otherwise
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(BotCastProtocol.ReactionMillis), token);
				}
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(result.Get<ushort>("hitTime") + 1), token);
				await session.SynchronizeAsync(token);
				lastReturnMillis = runtime.NowMillis;
				// AK-Q4: a Return to a bind on another map (Pandaemonium to Basfelt) spawns the player on the new map first;
				// SM_PLAYER_INFO follows once the client has entered it, as after a teleporter's map change.
				if (session.PacketHistory.Skip(packetStart).Any(packet => packet.PacketType == typeof(SM_PLAYER_SPAWN)) &&
					!session.PacketHistory.Skip(packetStart).Any(packet => packet.PacketType == typeof(SM_PLAYER_INFO) &&
						packet.Get<int>("objectId") == session.CharacterId))
				{
					await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
					await session.SynchronizeAsync(token);
				}
				Require.Contains(session.PacketHistory.Skip(packetStart), packet =>
					packet.PacketType == typeof(SM_CHANNEL_INFO));
				Require.Contains(session.PacketHistory.Skip(packetStart), packet =>
					packet.PacketType == typeof(SM_PLAYER_INFO) &&
					packet.Get<int>("objectId") == session.CharacterId);
				session.AcceptTeleportPosition();
				Require.Equal(returnMap, session.Api.World.MapId);
				Require.True(Distance(origin, session.CurrentPosition) > 30,
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
				if (QuestStatus(2100) < 3) await WaitForQuestStatusAsync(session, 2100, 3, token);
				session.BeginStep("ni07-q2100-finish", "walk-to-ulgorn-and-claim-natural-reward");
				int ulgorn = await ApproachShippedSpawnAsync(203516);
				string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
				var inventory = NaturalIshalgenInventoryPolicy.Load(root,
					session.Api.World.Inventory.Values.Select(item => item.ItemId));
				int rewardIndex = inventory.ChooseReward(2100, session.Api.World.Level,
					session.Api.World.Inventory.Values);
				Require.True(rewardIndex >= 0, "Q2100 should present a selectable reward to the natural Priest.");
				await FinishStandardQuestAsync(session, ulgorn, 2100, token,
					DialogAction.SELECTED_QUEST_REWARD1 + rewardIndex);
				Require.Equal(5, session.Api.World.Quests[2100].Status);
			}

			async Task CompleteThinkingAheadAsync()
			{
				if (QuestStatus(2001) < 3) await WaitForQuestStatusAsync(session, 2001, 3, token);
				int boromer;
				if (AtQuestStep(2001, 0))
				{
					boromer = await ApproachShippedSpawnAsync(203518);
					session.BeginStep("ni07-q2001-boromer-start", "speak-to-boromer-and-watch-campaign-movie");
					await OpenQuestDialogAsync(boromer, 2001);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(boromer, DialogAction.SELECT1_1, questId: 2001), token);
					await session.SynchronizeAsync(token);
					Require.Contains(session.PacketHistory, packet => packet.PacketType == typeof(SM_PLAY_MOVIE) &&
						packet.Get<int>("cutsceneId") == 51);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(boromer, DialogAction.SETPRO1, questId: 2001), token);
					await session.SynchronizeAsync(token);
					Require.Equal(1, session.Api.World.Quests[2001].StepAndFlags);
				}

				if (AtQuestStep(2001, 1))
				{
					for (int sack = 0; ItemCount(session.Api.World, 182203002) < 3; sack++)
					{
						session.BeginStep($"ni07-q2001-sack-{sack + 1}", "walk-and-loot-sprigg-grain-sack");
						int objectId = await UseAndLootQuestObjectAsync(700093, 182203002);
						navigator.UnavailableObjects.Add(objectId);
					}
					Require.Equal(3, ItemCount(session.Api.World, 182203002));
					boromer = await ApproachShippedSpawnAsync(203518);
					session.BeginStep("ni07-q2001-boromer-check", "present-grain-and-advance-mission");
					await OpenQuestDialogAsync(boromer, 2001);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(boromer,
						DialogAction.CHECK_USER_HAS_QUEST_ITEM, questId: 2001), token);
					await session.SynchronizeAsync(token);
					Require.Equal(2, session.Api.World.Quests[2001].StepAndFlags);
				}
				if (AtQuestStep(2001, 2))
				{
					boromer = await ApproachShippedSpawnAsync(203518);
					await OpenQuestDialogAsync(boromer, 2001);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(boromer, DialogAction.SETPRO3, questId: 2001), token);
					await session.SynchronizeAsync(token);
					Require.Equal(3, session.Api.World.Quests[2001].StepAndFlags);
				}

				for (int kill = 0; QuestStatus(2001) == 3 && QuestVar(2001) is >= 3 and <= 8; kill++)
				{
					session.BeginStep($"ni07-q2001-kill-{kill + 1}", "fight-sprigg-gatherer-for-campaign");
					int target = await ApproachShippedSpawnAsync(210369);
					await combat.KillAsync(target, token);
					navigator.UnavailableObjects.Add(target);
					await RestSafelyAsync(token);
				}
				Require.Equal(4, session.Api.World.Quests[2001].Status);
				boromer = await ApproachShippedSpawnAsync(203518);
				session.BeginStep("ni07-q2001-finish", "claim-priest-appropriate-campaign-reward");
				string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
				var inventory = NaturalIshalgenInventoryPolicy.Load(root,
					session.Api.World.Inventory.Values.Select(item => item.ItemId));
				int rewardIndex = inventory.ChooseReward(2001, session.Api.World.Level,
					session.Api.World.Inventory.Values);
				Require.True(rewardIndex >= 0);
				await FinishStandardQuestAsync(session, boromer, 2001, token,
					DialogAction.SELECTED_QUEST_REWARD1 + rewardIndex);
				Require.Equal(5, session.Api.World.Quests[2001].Status);
			}

			async Task AdvanceWheresRaeAsync()
			{
				int verdandi;
				if (QuestStatus(2002) < 3) await WaitForQuestStatusAsync(session, 2002, 3, token);
				if (AtQuestStep(2002, 0))
				{
					session.BeginStep("ni07-q2002-nobekk", "walk-to-nobekk-and-ask-about-rae");
					int nobekk = await ApproachShippedSpawnAsync(203519);
					await OpenQuestDialogAsync(nobekk, 2002);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(nobekk, DialogAction.SETPRO1, questId: 2002), token);
					await session.SynchronizeAsync(token);
					Require.Equal(1, session.Api.World.Quests[2002].StepAndFlags);
				}

				if (AtQuestStep(2002, 1))
				{
					session.BeginStep("ni07-q2002-dabi", "walk-to-dabi-and-ask-about-verdandi");
					int dabi = await ApproachShippedSpawnAsync(203534);
					await OpenQuestDialogAsync(dabi, 2002);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(dabi, DialogAction.SELECT2_1, questId: 2002), token);
					await session.SynchronizeAsync(token);
					Require.Contains(session.PacketHistory, packet => packet.PacketType == typeof(SM_PLAY_MOVIE) &&
						packet.Get<int>("cutsceneId") == 52);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(dabi, DialogAction.SETPRO2, questId: 2002), token);
					await session.SynchronizeAsync(token);
					Require.Equal(2, session.Api.World.Quests[2002].StepAndFlags);
				}

				if (AtQuestStep(2002, 2))
				{
					session.BeginStep("ni07-q2002-verdandi", "walk-to-verdandi-and-accept-sprigg-task");
					verdandi = await ApproachShippedSpawnAsync(790002);
					await OpenQuestDialogAsync(verdandi, 2002);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(verdandi, DialogAction.SETPRO3, questId: 2002), token);
					await session.SynchronizeAsync(token);
					Require.Equal(3, session.Api.World.Quests[2002].StepAndFlags);
				}
				if (QuestStatus(2002) == 3 && QuestVar(2002) is >= 3 and < 10)
				{
					await ApproachShippedSpawnAsync(790002);
					BotPosition spriggRefuge = session.CurrentPosition; // Client-observed, already visited quest-giver ground.
					var rejectedSpriggs = new HashSet<int>();
					BotPosition[] pairedGuardHints = graph.GetMap(contract.MapId)!.Waypoints
						.Where(waypoint => waypoint.TemplateId == 210378)
						.Select(waypoint => waypoint.Position).ToArray();
					navigator.AvoidHostileAggro = true;
					for (int kill = 0, attempt = 0; QuestVar(2002) < 10 && attempt < 21; attempt++)
					{
						session.BeginStep($"ni07-q2002-kill-{kill + 1}-try-{attempt + 1}",
							"pull-client-observed-sprigg-from-priest-spell-range");
						await RestSafelyAsync(token);
						await session.SynchronizeAsync(token);
						NaturalNavigationObject[] observed = navigator.Observe().Npcs.ToArray();
						NaturalNavigationObject[] candidates = observed
							.Where(npc => npc.TemplateId == 210377 && !rejectedSpriggs.Contains(npc.ObjectId) &&
								pairedGuardHints.All(guard => Distance(guard, npc.Position) >= 10))
							.OrderBy(npc => observed.Count(other => other.ObjectId != npc.ObjectId &&
								runtime.IsAggressive(
									runtime.Data.NpcDataDh.GetNpcTemplate(other.TemplateId)) &&
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
						if (selected == null && candidates.Length == 0)
						{
							// The 4.8 Sprigg spawns are beyond sight of Verdandi. Walk to a shipped
							// hint before treating an empty observed list as a respawn wait.
							try { selected = await ApproachShippedCombatSpawnAsync(210377); }
							catch (InvalidDataException exception) when (exception.Message.StartsWith(
								"No spell-range client-observed NPC 210377", StringComparison.Ordinal))
							{
								session.TraceDiagnostic("sprigg-hint-unreachable", new Dictionary<string, object?>
								{
									["reason"] = exception.Message,
									["position"] = session.CurrentPosition,
								});
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
					Require.Equal(10, session.Api.World.Quests[2002].StepAndFlags);
				}
				if (AtQuestStep(2002, 10))
				{
					session.BeginStep("ni07-q2002-verdandi-report", "report-sprigg-kills-to-verdandi");
					verdandi = await ApproachShippedSpawnAsync(790002);
					await OpenQuestDialogAsync(verdandi, 2002);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(verdandi, DialogAction.SETPRO3, questId: 2002), token);
					await session.SynchronizeAsync(token);
					Require.Equal(11, session.Api.World.Quests[2002].StepAndFlags);
				}
				if (AtQuestStep(2002, 11))
				{
					session.BeginStep("ni07-q2002-mushroom", "collect-sticky-mushroom-for-verdandi");
					if (ItemCount(session.Api.World, 182203003) == 0) await UseAndLootQuestObjectAsync(700045, 182203003);
					Require.Equal(1, ItemCount(session.Api.World, 182203003));
					session.BeginStep("ni07-q2002-mushroom-report", "present-collected-mushroom-to-verdandi");
					verdandi = await ApproachShippedSpawnAsync(790002);
					await OpenQuestDialogAsync(verdandi, 2002);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(verdandi,
						DialogAction.CHECK_USER_HAS_QUEST_ITEM, questId: 2002), token);
					await session.SynchronizeAsync(token);
					Require.Equal(12, session.Api.World.Quests[2002].StepAndFlags);
				}
				if (QuestStatus(2002) == 3 && QuestVar(2002) is 12 or 99 && session.Api.World.MapId == contract.MapId)
				{
					verdandi = await ApproachShippedSpawnAsync(790002);
					await OpenQuestDialogAsync(verdandi, 2002);
					session.BeginStep("ni07-q2002-ataxiar-enter", "take-verdandis-quest-teleport-to-ataxiar");
					session.Api.World.BeginWorldReload();
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(verdandi, DialogAction.SETPRO5, questId: 2002), token);
					await session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token,
						packet => packet.Get<int>("worldId") == 320010000);
					await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token,
						packet => packet.Get<int>("objectId") == session.CharacterId);
					session.AcceptTeleportPosition();
					Require.Equal(320010000, session.Api.World.MapId);
					Require.Equal(99, session.Api.World.Quests[2002].StepAndFlags);
					NaturalDecision inInstance = NaturalIshalgenDecisionEngine.Decide(contract,
						ObserveNaturalJourney(session), 7);
					if (!options.OptimizeHubs)
					{
						Require.Equal(2002, inInstance.SelectedQuestId);
						Require.Equal("continue-quest", inInstance.SelectedAction);
					}
					Require.Contains(inInstance.GlobalChecks, check => check.Rule == "quest-transport" && check.Verdict == "pass");
				}
				if (session.Api.World.MapId == 320010000 && AtQuestStep(2002, 99))
				{
					session.BeginStep("ni07-q2002-hagen", "walk-to-hagen-and-take-quest-return-flight");
					NaturalJourneyNavigator instanceNavigator = mapNavigators.Enter(NaturalMapKey.Observe(session.Api.World));
					NaturalNavigationResult hagenApproach = await NaturalIshalgenNavigator.ApproachNpcAsync(
						320010000, 205020, new BotPosition(434.75f, 399.5f, 235f, 25), instanceNavigator, token);
					Require.True(hagenApproach.Arrived, hagenApproach.Reason);
					int hagen = Require.IsType<int>(hagenApproach.TargetObjectId);
					await NaturalDialogProtocol.OpenAsync(session, hagen, token);
					await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
						packet => packet.Get<int>("targetObjectId") == hagen);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(hagen, DialogAction.QUEST_SELECT, questId: 2002), token);
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
					Require.Equal(contract.MapId, session.Api.World.MapId);
					Require.Equal(13, session.Api.World.Quests[2002].StepAndFlags);
				}
				if (AtQuestStep(2002, 13))
				{
					session.BeginStep("ni07-q2002-verdandi-return", "report-return-from-ataxiar");
					verdandi = await ApproachShippedSpawnAsync(790002);
					await OpenQuestDialogAsync(verdandi, 2002);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(verdandi, DialogAction.SETPRO3, questId: 2002), token);
					await session.SynchronizeAsync(token);
					Require.Equal(14, session.Api.World.Quests[2002].StepAndFlags);
				}
				if (AtQuestStep(2002, 14))
				{
					session.BeginStep("ni07-q2002-ribbit", "find-and-interact-with-cute-ribbit");
					int ribbit = await ApproachShippedSpawnAsync(203538);
					await NaturalDialogProtocol.OpenAsync(session, ribbit, token);
					await session.SynchronizeAsync(token);
					Require.Equal(15, session.Api.World.Quests[2002].StepAndFlags);
				}
				if (AtQuestStep(2002, 15))
				{
					int rae = await session.WaitForNpcAsync(203553, token);
					BotPosition raePosition = session.Api.World.Objects[rae].Position;
					NaturalNavigationResult raeApproach = await NaturalIshalgenNavigator.ApproachNpcAsync(
						contract.MapId, 203553, raePosition, navigator, token);
					Require.True(raeApproach.Arrived, raeApproach.Reason);
					session.BeginStep("ni07-q2002-rae", "speak-to-quest-spawned-rae");
					await OpenQuestDialogAsync(rae, 2002);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(rae, DialogAction.SETPRO7, questId: 2002), token);
					await session.SynchronizeAsync(token);
					Require.Equal(4, session.Api.World.Quests[2002].Status);
				}
				session.BeginStep("ni07-q2002-finish", "return-to-ulgorn-and-claim-priest-reward");
				await ApproachShippedSpawnAsync(203534); // Retrace the walked route through Dabi and Nobekk.
				await ApproachShippedSpawnAsync(203519);
				int ulgorn = await ApproachShippedSpawnAsync(203516);
				string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
				var inventory = NaturalIshalgenInventoryPolicy.Load(root,
					session.Api.World.Inventory.Values.Select(item => item.ItemId));
				int rewardIndex = inventory.ChooseReward(2002, session.Api.World.Level,
					session.Api.World.Inventory.Values);
				Require.True(rewardIndex >= 0);
				await NaturalDialogProtocol.OpenAsync(session, ulgorn, token);
				await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
					packet => packet.Get<int>("targetObjectId") == ulgorn);
				await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(ulgorn, DialogAction.QUEST_SELECT, questId: 2002), token);
				await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
					packet => packet.Get<int>("targetObjectId") == ulgorn && packet.Get<int>("questId") == 2002);
				await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(ulgorn, DialogAction.SETPRO8, questId: 2002), token);
				await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
					packet => packet.Get<int>("targetObjectId") == ulgorn && packet.Get<int>("questId") == 2002);
				await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(ulgorn,
					checked((ushort)(DialogAction.SELECTED_QUEST_REWARD1 + rewardIndex)), questId: 2002), token);
				await WaitForQuestStatusAsync(session, 2002, 5, token);
				Require.Contains(2002, session.Api.World.CompletedQuestIds);
			}

			async Task CompleteTreasureOfTheDeceasedAsync()
			{
				int keeper;
				if (QuestStatus(2003) < 3) await WaitForQuestStatusAsync(session, 2003, 3, token);
				if (AtQuestStep(2003, 0))
				{
					session.BeginStep("ni07-q2003-start", "walk-to-treasure-keeper-and-watch-campaign-movie");
					await WalkEasternRoadToDerotAsync(); // Eastern road avoids the collision-blocked direct valley line.
					keeper = await ApproachShippedSpawnAsync(203539);
					await OpenQuestDialogAsync(keeper, 2003);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(keeper, DialogAction.SELECT1_1, questId: 2003), token);
					await session.SynchronizeAsync(token);
					Require.Contains(session.PacketHistory, packet => packet.PacketType == typeof(SM_PLAY_MOVIE) &&
						packet.Get<int>("cutsceneId") == 53);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(keeper, DialogAction.SETPRO1, questId: 2003), token);
					await session.SynchronizeAsync(token);
					Require.Equal(1, session.Api.World.Quests[2003].StepAndFlags);
				}
				if (AtQuestStep(2003, 1))
				{
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
						Require.True(camp.Arrived, $"{camp.Reason} from {session.CurrentPosition}");
						await RestSafelyAsync(token);
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
					Require.Equal(3, ItemCount(session.Api.World, 182203004));
				}
				session.BeginStep("ni07-q2003-finish", "return-treasure-and-claim-reward");
				keeper = await ApproachShippedSpawnAsync(203539);
				await FinishNaturalItemQuestAsync(keeper, 2003);
				Require.Contains(2003, session.Api.World.CompletedQuestIds);
			}

			async Task CompleteNewSkillAsync()
			{
				await WaitForQuestStatusAsync(session, 2132, 4, token);
				Require.Equal(4, session.Api.World.Quests[2132].StepAndFlags);
				session.BeginStep("ni07-q2132-finish", "walk-to-priest-trainer-for-auto-learned-skill-quest");
				int trainer = await ApproachShippedSpawnAsync(203530);
				await FinishStandardQuestAsync(session, trainer, 2132, token);
				Require.Contains(2132, session.Api.World.CompletedQuestIds);
			}

			async Task CompleteCharmedCubeAsync()
			{
				int derot, munin;
				if (QuestStatus(2004) < 3) await WaitForQuestStatusAsync(session, 2004, 3, token);
				if (AtQuestStep(2004, 0))
				{
					session.BeginStep("ni07-q2004-derot-start", "ask-derot-about-charmed-cube");
					derot = await ApproachShippedSpawnAsync(203539);
					await OpenQuestDialogAsync(derot, 2004);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(derot, DialogAction.SETPRO1, questId: 2004), token);
					await session.SynchronizeAsync(token);
					Require.Equal(1, session.Api.World.Quests[2004].StepAndFlags);
				}
				if (AtQuestStep(2004, 1))
				{
					bool foundCube = ItemCount(session.Api.World, 182203005) > 0;
					for (int attempt = 1; !foundCube; attempt++)
					{
						session.BeginStep($"ni07-q2004-tombstone-{attempt}", "wake-and-fight-tombstone-guardian");
						await RestSafelyAsync(token);
						BotPosition tombstoneIngress = session.CurrentPosition;
						int tombstone = await ApproachShippedSpawnAsync(700047);
						await NaturalDialogProtocol.OpenAsync(session, tombstone, token);
						await session.WaitForPacketAsync(typeof(SM_EMOTION), token,
							packet => packet.Get<int>("senderObjectId") == session.CharacterId &&
							packet.Get<byte>("emotionType") == (byte)EmotionType.START_QUESTLOOT);
						await session.AdvanceAsync(TimeSpan.FromMilliseconds(3001), token);
						await session.SynchronizeAsync(token);
						BotKnownObject? guardian = session.Api.World.Objects.Values.FirstOrDefault(item =>
							item.Kind == BotKnownObjectKind.Npc && item.TemplateId == 211755 &&
							!navigator.UnavailableObjects.Contains(item.ObjectId));
						Require.NotNull(guardian);
						NaturalNavigationResult approach = await NaturalIshalgenNavigator.ApproachNpcAsync(
							contract.MapId, 211755, guardian.Position, navigator, token);
						Require.True(approach.Arrived, approach.Reason);
						int target = Require.IsType<int>(approach.TargetObjectId);
						bool killed = false;
						for (int pull = 0; pull < 3 && !killed; pull++)
						{
							if (pull > 0)
							{
								await RestSafelyAsync(token);
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
						await RestSafelyAsync(token);
					}
					Require.True(foundCube, "Ordinary tombstone guardians did not yield the quest cube.");
					Require.Equal(1, ItemCount(session.Api.World, 182203005));
					session.BeginStep("ni07-q2004-derot-check", "show-quest-cube-to-derot");
					derot = await ApproachShippedSpawnAsync(203539);
					await OpenQuestDialogAsync(derot, 2004);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(derot,
						DialogAction.CHECK_USER_HAS_QUEST_ITEM, questId: 2004), token);
					await session.SynchronizeAsync(token);
					Require.Equal(2, session.Api.World.Quests[2004].StepAndFlags);
				}
				if (AtQuestStep(2004, 2))
				{
					session.BeginStep("ni07-q2004-munin-start", "take-charmed-cube-to-munin");
					NaturalNavigationResult camp = await NaturalIshalgenNavigator.ExploreAnchorAsync(
						contract.MapId, -1, new BotPosition(667.961f, 1767.09f, 271.583f, 0),
						navigator, "shipped-west-side-camp", token);
					Require.True(camp.Arrived, camp.Reason); // Static route hint; no roaming NPC interaction.
					NaturalNavigationResult hillside = await NaturalIshalgenNavigator.ExploreAnchorAsync(
						contract.MapId, -1, new BotPosition(413.25f, 1901.5f, 319.136f, 0),
						navigator, "shipped-munin-hillside", token);
					Require.True(hillside.Arrived, hillside.Reason);
					munin = await ApproachShippedSpawnAsync(203550);
					await OpenQuestDialogAsync(munin, 2004);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(munin, DialogAction.SETPRO3, questId: 2004), token);
					await session.SynchronizeAsync(token);
					Require.Equal(3, session.Api.World.Quests[2004].StepAndFlags);
				}
				if (QuestStatus(2004) == 3 && QuestVar(2004) is >= 3 and < 6)
				{
					await ApproachShippedSpawnAsync(203550);
					BotPosition muninRefuge = session.CurrentPosition;
					for (int kill = 0; QuestVar(2004) < 6; kill++)
					{
						bool killed = false;
						for (int attempt = 1; attempt <= 4 && !killed; attempt++)
						{
							session.BeginStep($"ni07-q2004-kill-{kill + 1}-try-{attempt}",
								"fight-munins-cube-target-from-priest-spell-range");
							await RestSafelyAsync(token);
							int target = await ApproachShippedCombatSpawnAsync(210402);
							killed = await combat.TryKillAsync(target, token, muninRefuge);
							if (killed) navigator.UnavailableObjects.Add(target);
							else
							{
								NaturalNavigationResult regroup = await NaturalIshalgenNavigator.ExploreAnchorAsync(
									contract.MapId, -1, muninRefuge, navigator, "munin-retreat-refuge", token);
								Require.True(regroup.Arrived, regroup.Reason);
							}
						}
						Require.True(killed, $"Q2004 cube target {kill + 1} survived four ordinary Priest pulls.");
						await RestSafelyAsync(token);
					}
					Require.Equal(6, session.Api.World.Quests[2004].StepAndFlags);
				}
				if (AtQuestStep(2004, 6))
				{
					session.BeginStep("ni07-q2004-munin-report", "report-cube-combat-to-munin");
					munin = await ApproachShippedSpawnAsync(203550);
					await OpenQuestDialogAsync(munin, 2004);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(munin, DialogAction.SETPRO4, questId: 2004), token);
					await session.SynchronizeAsync(token);
					Require.Equal(4, session.Api.World.Quests[2004].Status);
				}
				session.BeginStep("ni07-q2004-finish", "return-to-derot-for-quest-reward");
				derot = await ApproachShippedSpawnAsync(203539);
				await FinishStandardQuestAsync(session, derot, 2004, token);
				Require.Contains(2004, session.Api.World.CompletedQuestIds);
			}

			async Task CompleteTeachingALessonAsync()
			{
				int mijou;
				if (QuestStatus(2005) < 3) await WaitForQuestStatusAsync(session, 2005, 3, token);
				navigator.AvoidHostileAggro = true;
				if (AtQuestStep(2005, 0))
				{
					session.BeginStep("ni07-q2005-mijou-start", "walk-to-mijou-and-watch-campaign-movie");
					mijou = await ApproachShippedSpawnAsync(203540);
					await OpenQuestDialogAsync(mijou, 2005);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(mijou, DialogAction.SELECT1_1, questId: 2005), token);
					await session.SynchronizeAsync(token);
					Require.Contains(session.PacketHistory, packet => packet.PacketType == typeof(SM_PLAY_MOVIE) &&
						packet.Get<int>("cutsceneId") == 54);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(mijou, DialogAction.SETPRO1, questId: 2005), token);
					await session.SynchronizeAsync(token);
					Require.Equal(1, session.Api.World.Quests[2005].StepAndFlags);
				}
				if (AtQuestStep(2005, 1))
				{
					// Java Q2005 awards Odella only from these three Stalker templates. Shipped
					// spots guide exploration; a target still has to arrive in client packets.
					// Build the hints from this world's loaded spawn data so retail placement
					// edits cannot leave yesterday's coordinates in the search loop.
					BotPosition[] stalkerSearchAreas = NaturalStalkerSearchPolicy.SelectAreas(
						runtime.Data.SpawnsDh.GetSpawnsByWorldId(contract.MapId)
							.Where(group => group.GetNpcId() is 210395 or 210396 or 210750)
							.SelectMany(group => group.GetSpawnTemplates())
							.Select(spot => new BotPosition(spot.GetX(), spot.GetY(), spot.GetZ(), 0)),
						session.CurrentPosition);
					if (stalkerSearchAreas.Length == 0)
						throw new InvalidDataException("Q2005 has no shipped Odella-dropping Stalker search areas.");
					session.TraceDiagnostic("q2005-search-areas", new Dictionary<string, object?>
					{
						["origin"] = session.CurrentPosition,
						["areas"] = stalkerSearchAreas,
					});
					int routedRevives = combat.ReviveCount;
					bool lastStalkerKilled = false;
					BotPosition? successfulStalkerArea = null;
					int preferredAreaMisses = 0;
					int corridorClearAttempts = 0;
					var rejectedPullTargets = new HashSet<int>();
					BotPosition rejectionPosition = session.CurrentPosition;
					long rejectionMillis = runtime.NowMillis;
					void RejectPullTarget(int objectId)
					{
						rejectedPullTargets.Add(objectId);
						rejectionPosition = session.CurrentPosition;
						rejectionMillis = runtime.NowMillis;
					}
					var searchNotes = new List<string>();
					BotNavigationHazard[] ObservedFieldHazards() => navigator.Observe().Npcs
						.Select(npc => (Npc: npc,
							Template: runtime.Data.NpcDataDh.GetNpcTemplate(npc.TemplateId)))
						.Where(entry => runtime.IsAggressive(entry.Template))
						.SelectMany(entry => entry.Npc.Hazards(entry.Template!.GetAggroRange())).ToArray();
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
						await RestSafelyAsync(token); // Ordinary sit/stand; attack interrupts it.
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
						catch (NaturalGuardedObjectiveRevivedException exception)
						{
							// The failed field route belongs to the position before death. Rejoin
							// from the observed bind point instead of continuing that stale approach.
							session.TraceDiagnostic("q2005-return-after-revive", new Dictionary<string, object?>
							{
								["position"] = session.CurrentPosition, ["reason"] = exception.Message,
							});
							return await RecoverMijouAfterReviveAsync();
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
					async Task<int> RecoverMijouAfterReviveAsync()
					{
						await RestSafelyAsync(token);
						await WalkEasternRoadToDerotAsync();
						routedRevives = combat.ReviveCount;
						return await ApproachShippedSpawnAsync(203540);
					}
					async Task<int> ReturnToMijouAlongIngressAsync(int eventStart, BotPosition ingressStart)
					{
						if (combat.ReviveCount > routedRevives)
							return await RecoverMijouAfterReviveAsync();
						// A distant reverse A* can fail on the geodata even though the bot just
						// walked in. Revisit its client-estimated movement checkpoints in reverse.
						BotPosition[] breadcrumbs = NaturalIshalgenNavigator.SelectRetraceCheckpoints(
							navigator.Events.Skip(eventStart), stride: 3);
						for (int guardClears = 0; guardClears <= 4; guardClears++)
						{
							int eventBefore = navigator.Events.Count;
							NaturalNavigationResult result = await NaturalIshalgenNavigator.RetraceIngressAsync(
								contract.MapId, ingressStart, breadcrumbs, navigator, token);
							if (combat.ReviveCount > routedRevives)
								return await RecoverMijouAfterReviveAsync();
							if (result.Arrived) return await ReachMijouFromFieldAsync();
							BotPosition blockedCheckpoint = navigator.Events.Skip(eventBefore)
								.LastOrDefault(entry => entry.Action == "navigation-failed")?.Destination ?? ingressStart;
							if (guardClears < 4 && await TryClearObservedBlockerAsync(blockedCheckpoint))
							{
								if (combat.ReviveCount > routedRevives)
									return await RecoverMijouAfterReviveAsync();
								continue; // Ordinary kill may open a checked route; re-observe every hazard.
							}
							if (combat.ReviveCount > routedRevives)
								return await RecoverMijouAfterReviveAsync();
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
							() => runtime.NowMillis,
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
									await RestSafelyAsync(waitToken);
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
						await RestSafelyAsync(token);
						// A failed pull describes this position and patrol moment, not the NPC forever.
						if (rejectedPullTargets.Count > 0 && NaturalStalkerSearchPolicy.ShouldRetryRejected(
							session.CurrentPosition, rejectionPosition, runtime.NowMillis - rejectionMillis))
						{
							session.TraceDiagnostic("q2005-retry-rejected-targets", new Dictionary<string, object?>
							{
								["count"] = rejectedPullTargets.Count,
								["from"] = rejectionPosition,
								["position"] = session.CurrentPosition,
								["elapsedMs"] = runtime.NowMillis - rejectionMillis,
							});
							rejectedPullTargets.Clear();
						}
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
								!rejectedPullTargets.Contains(npc.ObjectId) &&
								Distance(session.CurrentPosition, npc.Position) <= 80)
							.OrderBy(npc => Distance(session.CurrentPosition, npc.Position))
							.FirstOrDefault();
						if (nearbyStalker != null)
							isolatedStalker = nearbyStalker.Position;
						session.TraceDiagnostic("q2005-search-choice", new Dictionary<string, object?>
						{
							["attempt"] = attempt + 1,
							["source"] = nearbyStalker == null ? "shipped-spot" : "client-observed-stalker",
							["position"] = session.CurrentPosition,
							["destination"] = isolatedStalker,
							["observedObjectId"] = nearbyStalker?.ObjectId,
							["rejectedObjectIds"] = rejectedPullTargets.Count,
						});
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
								.Where(npc => runtime.IsAggressive(
									runtime.Data.NpcDataDh.GetNpcTemplate(npc.TemplateId)) &&
									Distance(session.CurrentPosition, npc.Position) <= 30 &&
									!rejectedPullTargets.Contains(npc.ObjectId))
								.Select(npc => new { Npc = npc, Edge = FindCheckedFiringEdge(npc) })
								.Where(entry => entry.Edge != null)
								.OrderBy(entry => observed.Count(other => other.ObjectId != entry.Npc.ObjectId &&
									runtime.IsAggressive(
										runtime.Data.NpcDataDh.GetNpcTemplate(other.TemplateId)) &&
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
									RejectPullTarget(blocker.Npc.ObjectId);
									searchNotes.Add($"corridor {corridorClearAttempts}: firing target moved or lost after checked flank");
									continue;
								}
								bool cleared;
								try
								{
									cleared = await combat.TryKillAsync(blocker.Npc.ObjectId, token,
										new BotPosition(946.253f, 1702.775f, 259.625f, 0));
								}
								catch (NaturalCombatApproachBlockedException)
								{
									RejectPullTarget(blocker.Npc.ObjectId);
									searchNotes.Add($"corridor {corridorClearAttempts}: moving blocker had no checked combat approach");
									continue;
								}
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
								else RejectPullTarget(blocker.Npc.ObjectId);
								if (combat.ReviveCount > routedRevives)
								{
									await WalkEasternRoadToDerotAsync();
									routedRevives = combat.ReviveCount;
								}
								attempt--; // The same Stalker search remains pending after a corridor fight.
								continue;
							}
							// A dense patrol can leave no safe short flank from the current spot.
							// Use the journey's checked fight-through route before declaring every
							// Stalker area unreachable from this same corridor.
							BotPosition beforeFightThrough = session.CurrentPosition;
							var unavailableBefore = navigator.UnavailableObjects.ToHashSet();
							bool fightProgress = await TryFightThroughAsync(
								isolatedStalker, nearbyStalker?.ObjectId, rejectedPullTargets);
							NaturalNavigationObject[] defeated = observed
								.Where(npc => !unavailableBefore.Contains(npc.ObjectId) &&
									navigator.UnavailableObjects.Contains(npc.ObjectId)).ToArray();
							session.TraceDiagnostic("q2005-corridor-fight-through", new Dictionary<string, object?>
							{
								["attempt"] = attempt + 1,
								["destination"] = isolatedStalker,
								["progressed"] = fightProgress,
								["movedMetres"] = Distance(beforeFightThrough, session.CurrentPosition),
								["defeated"] = defeated.Select(npc => $"{npc.TemplateId}/{npc.ObjectId}").ToArray(),
							});
							foreach (NaturalNavigationObject defeatedStalker in defeated.Where(npc =>
								npc.TemplateId is 210395 or 210396 or 210750))
							{
								await TryLootCorpseItemAsync(session, defeatedStalker.ObjectId, 182203006, token);
								lastStalkerKilled = true;
								successfulStalkerArea = defeatedStalker.Position;
								preferredAreaMisses = 0;
								if (!await RestAtObservedFieldCampAsync())
									mijou = await ReturnToMijouAlongIngressAsync(ingressEventStart, ingressStart);
							}
							if (combat.ReviveCount > routedRevives)
							{
								await WalkEasternRoadToDerotAsync();
								routedRevives = combat.ReviveCount;
							}
							if (fightProgress && (defeated.Length > 0 ||
								Distance(beforeFightThrough, session.CurrentPosition) > 1))
							{
								attempt--; // Reobserve the same area after a real fight or checked move.
								continue;
							}
							searchNotes.Add($"corridor {corridorClearAttempts}: no checked flank or fight-through progress");
						}
						if (!safeStalkerArea.Arrived)
						{
							session.TraceDiagnostic("q2005-search-route-blocked", new Dictionary<string, object?>
							{
								["attempt"] = attempt + 1,
								["destination"] = isolatedStalker,
								["reason"] = safeStalkerArea.Reason,
								["route"] = navigator.LastRouteDiagnostic,
							});
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
								!rejectedPullTargets.Contains(npc.ObjectId) &&
								Distance(session.CurrentPosition, npc.Position) <= 30)
							.OrderBy(npc => Distance(npc.Position, isolatedStalker))
							.FirstOrDefault();
						if (observedStalker == null)
						{
							session.TraceDiagnostic("q2005-search-empty", new Dictionary<string, object?>
							{
								["attempt"] = attempt + 1,
								["area"] = isolatedStalker,
								["position"] = session.CurrentPosition,
							});
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
									!rejectedPullTargets.Contains(npc.ObjectId) &&
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
								runtime.IsAggressive(runtime.Data.NpcDataDh.GetNpcTemplate(npc.TemplateId)) &&
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
						bool killed;
						try
						{
							killed = await combat.TryKillAsync(target, token,
								new BotPosition(946.29f, 1702.67f, 259.625f, 0), attackHistoryStart);
						}
						catch (NaturalCombatApproachBlockedException exception)
						{
							RejectPullTarget(target);
							searchNotes.Add($"attempt {attempt + 1}: moving Stalker {target} had no checked pull: {exception.Message}");
							continue; // Reobserve at a different shipped area instead of walking into its pack.
						}
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
						if (killed && await RestAtObservedFieldCampAsync()) continue;
						mijou = await ReturnToMijouAlongIngressAsync(ingressEventStart, ingressStart);
					}
					Require.True(ItemCount(session.Api.World, 182203006) == 3,
						$"Q2005 needs three earned Odella; observed {ItemCount(session.Api.World, 182203006)}. " +
						string.Join(" | ", searchNotes));
					session.BeginStep("ni07-q2005-mijou-finish", "show-odella-and-claim-priest-reward");
					mijou = await ApproachShippedSpawnAsync(203540);
					await OpenQuestDialogAsync(mijou, 2005);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(mijou,
						DialogAction.CHECK_USER_HAS_QUEST_ITEM, questId: 2005), token);
					await session.SynchronizeAsync(token);
					Require.Equal(4, session.Api.World.Quests[2005].Status);
				}
				mijou = await ApproachShippedSpawnAsync(203540);
				string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
				var inventory = NaturalIshalgenInventoryPolicy.Load(root,
					session.Api.World.Inventory.Values.Select(item => item.ItemId));
				int rewardIndex = inventory.ChooseReward(2005, session.Api.World.Level,
					session.Api.World.Inventory.Values);
				Require.True(rewardIndex >= 0);
				await FinishStandardQuestAsync(session, mijou, 2005, token,
					DialogAction.SELECTED_QUEST_REWARD1 + rewardIndex);
				Require.Contains(2005, session.Api.World.CompletedQuestIds);
			}

			async Task CompleteHitThemWhereItHurtsAsync()
			{
				// Java ce54b7931 _2006HitThemWhereitHurts and QuestItemNpcAI:
				// an ordinary USE_OBJECT on a quest_use_item sack creates its corpse loot.
				int mijou;
				if (QuestStatus(2006) < 3) await WaitForQuestStatusAsync(session, 2006, 3, token);
				if (AtQuestStep(2006, 0))
				{
					session.BeginStep("ni07-q2006-mijou-start", "ask-mijou-about-mau-grain");
					mijou = await ApproachShippedSpawnAsync(203540);
					await OpenQuestDialogAsync(mijou, 2006);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(mijou, DialogAction.SETPRO1, questId: 2006), token);
					await session.SynchronizeAsync(token);
					Require.Equal(1, session.Api.World.Quests[2006].StepAndFlags);
				}
				if (AtQuestStep(2006, 1))
				{
					BotPosition ingressStart = session.CurrentPosition;
					int ingressEventStart = navigator.Events.Count;
					BotPosition[] firstSackIngress = [];
					for (int attempt = 1; ItemCount(session.Api.World, 182203008) < 3; attempt++)
					{
						session.BeginStep($"ni07-q2006-sack-{attempt}", "walk-to-client-observed-mau-sack-and-loot-grain");
						if (attempt == 1) await RestSafelyAsync(token); // Mijou-side refuge; do not sit in the Mau field.
						// A sack's 3 s use bar breaks on any hit: fight what is on the Priest first, and never start a
						// use below 80% HP (a death here used to leave the loop navigating while dead).
						if (!await DefendAgainstEngagedAsync("q2006-sack")) { await RestSafelyAsync(token); continue; }
						if (session.Api.World.CurrentHp * 100 < session.Api.World.MaxHp * 80) await RestSafelyAsync(token);
						int sack = await ApproachShippedSpawnAsync(700095, skipBlockedTarget: true);
						int interactionPacketStart = session.PacketHistory.Count;
						await NaturalDialogProtocol.OpenAsync(session, sack, token);
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
							await TryLootCorpseItemAsync(session, sack, 182203008, token, interactionPacketStart);
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
				Require.Equal(3, ItemCount(session.Api.World, 182203008));
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
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(mijou,
						DialogAction.CHECK_USER_HAS_QUEST_ITEM, questId: 2006), token);
					await session.SynchronizeAsync(token);
					Require.Equal(4, session.Api.World.Quests[2006].Status);
				}
				session.BeginStep("ni07-q2006-ulgorn-reward", "return-to-ulgorn-and-claim-priest-reward");
				if (easternRoadIngressStart is BotPosition && easternRoadIngress.Length > 0)
				{
					NaturalNavigationResult easternReturn = await NaturalIshalgenNavigator.RetraceIngressAsync(
						contract.MapId, easternRoadIngressStart.Value, easternRoadIngress, navigator, token);
					session.TraceDiagnostic("eastern-road-return", new Dictionary<string, object?>
					{
						["arrived"] = easternReturn.Arrived,
						["reason"] = easternReturn.Reason,
						["checkpoints"] = easternRoadIngress.Length,
						["position"] = session.CurrentPosition,
					});
					Require.True(easternReturn.Arrived, easternReturn.Reason);
				}
				else await UseLearnedReturnToBindAsync(); // Ordinary learned spell when no prior walking history survives.
				int ulgorn = await ApproachShippedSpawnAsync(203516);
				string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
				var inventory = NaturalIshalgenInventoryPolicy.Load(root,
					session.Api.World.Inventory.Values.Select(item => item.ItemId));
				int rewardIndex = inventory.ChooseReward(2006, session.Api.World.Level,
					session.Api.World.Inventory.Values);
				Require.True(rewardIndex >= 0);
				await FinishStandardQuestAsync(session, ulgorn, 2006, token,
					DialogAction.SELECTED_QUEST_REWARD1 + rewardIndex);
				Require.Contains(2006, session.Api.World.CompletedQuestIds);
			}

			async Task CompleteWheresRaeThisTimeAsync()
			{
				// Java ce54b7931 _2007WheresRaeThisTime: all transitions are ordinary
				// NPC dialogue, quest_use_item interaction, or quest-granted teleport.
				if (QuestStatus(2007) < 3) await WaitForQuestStatusAsync(session, 2007, 3, token);
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
							bool atBind = Distance(session.CurrentPosition,
								new BotPosition(571.0388f, 2787.342f, 299.875f, 0)) < 400;
							if (recovery > 0 || atBind && templateId is (203552 or 203554 or 700085 or 700086 or 700087))
							{
								// A death returns to bind. Never treat bind-area monsters as
								// guards of the distant Rae/Nalto corridor.
								session.BeginStep($"ni07-q2007-rejoin-{templateId}-{recovery}",
									"rest-at-bind-and-walk-previously-checked-eastern-road");
								await RestSafelyAsync(token);
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
							await RestSafelyAsync(token);
							if (recovery == 2)
							{
								session.TraceDiagnostic("q2007-guarded-route-return", new Dictionary<string, object?>
								{
									["npcTemplateId"] = templateId,
									["position"] = session.CurrentPosition,
								});
								await UseLearnedReturnToBindAsync();
							}
							await session.AdvanceAsync(TimeSpan.FromSeconds(30), token);
							await session.SynchronizeAsync(token);
						}
					}
					throw new InvalidDataException($"Q2007 guarded NPC {templateId} exhausted {MaximumQ2007Recoveries} recoveries.");
				}
				foreach (var (npcId, action, step, name) in conversations)
				{
					if (!AtQuestStep(2007, step - 1)) continue;
					session.BeginStep($"ni07-q2007-{name}", "walk-to-quest-npc-and-advance-dialogue");
					if (npcId == 203539)
						await WalkEasternRoadToDerotAsync(); // Reuse the earlier checked road, not the blocked direct valley.
					int npc = npcId is 203552 or 203554
						? await ApproachGuardedCampaignNpcAsync(npcId)
						: await ApproachShippedSpawnAsync(npcId);
					await OpenQuestDialogAsync(npc, 2007);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npc, checked((ushort)action), questId: 2007), token);
					await session.SynchronizeAsync(token);
					Require.Equal(step, session.Api.World.Quests[2007].StepAndFlags);
				}
				foreach (var (npcId, step, color) in new[]
				{
					(700085, 6, "green"), (700086, 7, "blue"), (700087, 8, "violet"),
				})
				{
					if (!AtQuestStep(2007, step - 1)) continue;
					session.BeginStep($"ni07-q2007-{color}-generator", "walk-to-and-use-quest-generator");
					// Generators are quest_use_item objects (like the Mau sacks): the quest advances only when the
					// ordinary use bar completes (SM_USE_OBJECT action 2 with its full duration). An attack aborts
					// the use (duration 0); defend and use again.
					for (int use = 1; session.Api.World.Quests[2007].StepAndFlags < step; use++)
					{
						if (use > 10) Require.Fail($"Q2007 {color} generator use did not complete after ten attempts.");
						// Clear around the generator's spot from range before stepping onto it (the recorded human's
						// order), then approach with the same walk-back-after-death recovery as Rae and Nalto.
						BotPosition? generatorHint = graph.GetMap(contract.MapId)!.Waypoints
							.Where(waypoint => waypoint.TemplateId == npcId)
							.OrderBy(waypoint => Distance(session.CurrentPosition, waypoint.Position))
							.Select(waypoint => (BotPosition?)waypoint.Position).FirstOrDefault();
						// Never enter the camp low or without a potion in the bag (the recorded human topped up first).
						if (session.Api.World.CurrentHp * 100 < session.Api.World.MaxHp * 80 ||
							NaturalIshalgenPotionPolicy.SelectOwnedPotion(session.Api.World.Inventory.Values) == null)
							await RestSafelyAsync(token);
						await ClearAroundSpotAsync(generatorHint, null, $"q2007-{color}-generator-approach");
						int generator = await ApproachGuardedCampaignNpcAsync(npcId);
						// A player clears what stands near an object before a 3 s use bar a single hit interrupts.
						await ClearAroundObjectiveAsync(generator, $"q2007-{color}-generator");
						if (session.Api.World.IsDead || !navigator.Observe().Npcs.Any(npc => npc.ObjectId == generator &&
							Distance(session.CurrentPosition, npc.Position) <= 3)) continue; // walked off while clearing
						int usePacketStart = session.PacketHistory.Count;
						await NaturalDialogProtocol.OpenAsync(session, generator, token);
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
					Require.Equal(step, session.Api.World.Quests[2007].StepAndFlags);
				}
			if (AtQuestStep(2007, 8))
			{
				session.BeginStep("ni07-q2007-rae-return", "ask-rae-for-quest-transport-back-to-ulgorn");
				int rae = await ApproachShippedSpawnAsync(203554);
				if (options.Course == NaturalMauCourse.GeneratorToRae) return;
					await OpenQuestDialogAsync(rae, 2007);
					session.Api.World.BeginWorldReload();
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(rae, DialogAction.SETPRO6, questId: 2007), token);
					// Java TeleportService.teleportToNpc(player, 203516) stays on Ishalgen, and a same-map teleport
					// (SpawnTask.run -> spawnOnSameMap) sends SM_CHANNEL_INFO and SM_PLAYER_INFO but no SM_PLAYER_SPAWN.
					await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token,
						packet => packet.Get<int>("objectId") == session.CharacterId);
					session.AcceptTeleportPosition();
					Require.Equal(4, session.Api.World.Quests[2007].Status);
				}
				session.BeginStep("ni07-q2007-ulgorn-reward", "claim-priest-reward-after-quest-transport");
				int ulgorn = await ApproachShippedSpawnAsync(203516);
				string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
				var inventory = NaturalIshalgenInventoryPolicy.Load(root,
					session.Api.World.Inventory.Values.Select(item => item.ItemId));
				int rewardIndex = inventory.ChooseReward(2007, session.Api.World.Level,
					session.Api.World.Inventory.Values);
				Require.True(rewardIndex >= 0);
				await FinishStandardQuestAsync(session, ulgorn, 2007, token,
					DialogAction.SELECTED_QUEST_REWARD1 + rewardIndex);
				Require.Contains(session.PacketHistory, packet => packet.PacketType == typeof(SM_PLAY_MOVIE) &&
					packet.Get<int>("cutsceneId") == 58);
				Require.Contains(2007, session.Api.World.CompletedQuestIds);
			}

			async Task CompleteSparkleAndShineAsync()
			{
				// Java ce54b7931 ItemCollecting and ishalgen.xml Q2105; drops
				// come from ordinary client-observed 210367 kills and corpse loot.
				session.BeginStep("ni07-q2105-start", "accept-sparkie-collection-at-vanar");
				int vanarNpc = await ApproachShippedSpawnAsync(203502);
				if (QuestStatus(2105) < 3) await session.StartQuestAsync(vanarNpc, 2105, token);
				if (QuestStatus(2105) == 3)
				{
					for (int attempt = 0; ItemCount(session.Api.World, 182203105) < 3; attempt++)
					{
						session.BeginStep($"ni07-q2105-kill-{attempt + 1}", "fight-and-check-sparkie-drop");
						int target = await ApproachShippedSpawnAsync(210367);
						await combat.KillAsync(target, token);
						navigator.UnavailableObjects.Add(target);
						await TryLootCorpseItemAsync(session, target, 182203105, token);
						await RestSafelyAsync(token);
					}
					Require.Equal(3, ItemCount(session.Api.World, 182203105));
				}
				session.BeginStep("ni07-q2105-finish", "return-to-vanar-and-turn-in");
				vanarNpc = await ApproachShippedSpawnAsync(203502);
				await FinishNaturalItemQuestAsync(vanarNpc, 2105);
				Require.Equal(5, session.Api.World.Quests[2105].Status);
				Require.Equal(0, ItemCount(session.Api.World, 182203105));
			}

			async Task CompleteVanarsFlatteryAsync()
			{
				// Java ce54b7931 _2106VanarsFlattery: Vanar gives the letter,
				// SETPRO1 advances it, and 203517 accepts the ordinary turn-in.
				session.BeginStep("ni07-q2106-start", "accept-vanars-letter-without-creating-an-item");
				int vanarNpc = await ApproachShippedSpawnAsync(203502);
				if (QuestStatus(2106) < 3) await session.StartQuestAsync(vanarNpc, 2106, token);
				if (QuestStatus(2106) == 3)
				{
					Require.Equal(1, ItemCount(session.Api.World, 182203106));
					session.BeginStep("ni07-q2106-vanar-report", "confirm-letter-with-vanar");
					await OpenQuestDialogAsync(vanarNpc, 2106);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(vanarNpc, DialogAction.SETPRO1, questId: 2106), token);
					await session.SynchronizeAsync(token);
					Require.Equal(4, session.Api.World.Quests[2106].Status);
				}
				session.BeginStep("ni07-q2106-delivery", "walk-to-recipient-and-deliver-vanars-letter");
				int recipient = await ApproachShippedSpawnAsync(203517);
				await FinishStandardQuestAsync(session, recipient, 2106, token);
				Require.Contains(2106, session.Api.World.CompletedQuestIds);
			}

			async Task CompleteInsectProblemAsync()
			{
				// Java ce54b7931 _2114TheInsectProblem: the SETPRO1 branch
				// requires ten ordinary Grove Sparkie kills before Motgar's reward.
				session.BeginStep("ni07-q2114-motgar-start", "choose-grove-sparkie-insect-problem");
				int motgar = await ApproachShippedSpawnAsync(203533);
				if (QuestStatus(2114) < 3)
				{
					await OpenQuestDialogAsync(motgar, 2114);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(motgar, DialogAction.SETPRO1, questId: 2114), token);
					await session.SynchronizeAsync(token);
					Require.Equal(1, session.Api.World.Quests[2114].StepAndFlags);
				}
				for (int kill = 0; QuestStatus(2114) == 3 && kill < 10; kill++)
				{
					session.BeginStep($"ni07-q2114-sparkie-{kill + 1}", "fight-grove-sparkie-for-motgar");
					int target = await ApproachShippedSpawnAsync(QuestVar(2114) < 11 ? 210734 : 210380);
					await combat.KillAsync(target, token);
					navigator.UnavailableObjects.Add(target);
					await RestSafelyAsync(token);
				}
				Require.Equal(4, session.Api.World.Quests[2114].Status);
				session.BeginStep("ni07-q2114-motgar-reward", "return-to-motgar-for-insect-reward");
				motgar = await ApproachShippedSpawnAsync(203533);
				await FinishStandardQuestAsync(session, motgar, 2114, token);
				Require.Contains(2114, session.Api.World.CompletedQuestIds);
			}

			async Task CompleteImprisonedGourmetAsync()
			{
				// Java ce54b7931 _2123TheImprisonedGourmet: a Methu Egg
				// supplies item 182203122; SETPRO2 selects reward group one.
				session.BeginStep("ni07-q2123-munin-start", "accept-munins-gourmet-request");
				int munin = await ApproachShippedSpawnAsync(203550);
				if (QuestStatus(2123) < 3) await session.StartQuestAsync(munin, 2123, token);
				if (QuestStatus(2123) == 3)
				{
					session.BeginStep("ni07-q2123-methu-egg", "walk-to-and-loot-client-observed-methu-egg");
					if (ItemCount(session.Api.World, 182203122) == 0)
						navigator.UnavailableObjects.Add(await UseAndLootQuestObjectAsync(700128, 182203122, skipBlockedTarget: true));
					Require.Equal(1, ItemCount(session.Api.World, 182203122));
					session.BeginStep("ni07-q2123-munin-reward", "give-methu-egg-to-munin-without-ascension-dialogue");
					munin = await ApproachShippedSpawnAsync(203550);
					await OpenQuestDialogAsync(munin, 2123);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(munin, DialogAction.SETPRO2, questId: 2123), token);
					await session.SynchronizeAsync(token);
					Require.Equal(4, session.Api.World.Quests[2123].Status);
					Require.Equal(0, ItemCount(session.Api.World, 182203122));
				}
				await FinishStandardQuestAsync(session, munin, 2123, token);
				Require.Contains(2123, session.Api.World.CompletedQuestIds);
			}

			async Task CompleteRobberyPlotAsync()
			{
				// Java ce54b7931 _2125TheRobberyPlot: Mijou -> Hephe -> Alfrigh.
				session.BeginStep("ni07-q2125-mijou-start", "accept-robbery-investigation");
				int mijou = await ApproachShippedSpawnAsync(203540);
				if (QuestStatus(2125) < 3) await session.StartQuestAsync(mijou, 2125, token);
				if (AtQuestStep(2125, 0))
				{
					session.BeginStep("ni07-q2125-hephe", "ask-hephe-about-robbery");
					int hephe = await ApproachShippedSpawnAsync(203514);
					await OpenQuestDialogAsync(hephe, 2125);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(hephe, DialogAction.SETPRO1, questId: 2125), token);
					await session.SynchronizeAsync(token);
					Require.Equal(1, session.Api.World.Quests[2125].StepAndFlags);
				}
				session.BeginStep("ni07-q2125-alfrigh", "report-investigation-to-alfrigh");
				int alfrigh = await ApproachShippedSpawnAsync(203543);
				await FinishStandardQuestAsync(session, alfrigh, 2125, token);
				Require.Contains(2125, session.Api.World.CompletedQuestIds);
			}

			async Task CompleteForLoveOfNegiAsync()
			{
				// Java ce54b7931 _2135ForLoveofNegi: Bolir gives the
				// letter, Negi takes it, and Bolir grants the ordinary reward.
				session.BeginStep("ni07-q2135-bolir-start", "accept-bolirs-letter");
				int bolir = await ApproachShippedSpawnAsync(203532);
				if (QuestStatus(2135) < 3) await session.StartQuestAsync(bolir, 2135, token);
				if (AtQuestStep(2135, 0))
				{
					Require.Equal(1, ItemCount(session.Api.World, 182203131));
					session.BeginStep("ni07-q2135-negi", "deliver-bolirs-letter-to-negi");
					int negi = await ApproachShippedSpawnAsync(203531);
					await OpenQuestDialogAsync(negi, 2135);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(negi, DialogAction.SETPRO1, questId: 2135), token);
					await session.SynchronizeAsync(token);
					Require.Equal(1, session.Api.World.Quests[2135].StepAndFlags);
					Require.Equal(0, ItemCount(session.Api.World, 182203131));
				}
				session.BeginStep("ni07-q2135-bolir-reward", "return-to-bolir-for-delivery-reward");
				bolir = await ApproachShippedSpawnAsync(203532);
				await FinishStandardQuestAsync(session, bolir, 2135, token);
				Require.Contains(2135, session.Api.World.CompletedQuestIds);
			}

			async Task FinishNaturalItemQuestAsync(int npc, int questId, int rewardAction = DialogAction.SELECTED_QUEST_NOREWARD)
			{
				if (QuestStatus(questId) == 4)
					await FinishStandardQuestAsync(session, npc, questId, token, rewardAction);
				else await FinishItemQuestAsync(session, npc, questId, token, rewardAction);
			}

			async Task AcceptAllAtCurrentHubAsync()
			{
				NaturalIshalgenHubPolicy.Hub? hub = NaturalIshalgenHubPolicy.At(session.CurrentPosition);
				if (hub == null) return;
				if (hub.Name == "aldelle") await BindAtAldelleIfNeededAsync();
				int[] eligible = NaturalIshalgenHubPolicy.Order(
					contract.Quests.Where(quest => hub.QuestIds.Contains(quest.Id) &&
						NaturalIshalgenHubPolicy.CanAccept(quest, session.Api.World))
						.Select(quest => quest.Id), contract,
					id => runtime.Data.Quests.GetQuestById(id)?.GetCategory() ?? QuestCategory.QUEST, hub);
				foreach (int id in eligible)
				{
					int? starterId = templatePlans.TryGetValue(id, out QuestRunPlan? plan) &&
						plan.StartTrigger.Kind == "npc" ? plan.StartTrigger.Npcs.FirstOrDefault()?.Id :
						NaturalIshalgenHubPolicy.CustomNpcStarters.GetValueOrDefault(id);
					if (starterId is not > 0) continue; // auto-start and scripted campaign dialogue
					// "At this hub" means an ordinary nearby NPC the client sees. A broad
					// named hub may cover several hundred metres; walking to every starter
					// just to accept its quest defeats grouping and can enter unsafe packs.
					if (!navigator.Observe().Npcs.Any(npc => npc.TemplateId == starterId.Value &&
						Distance(session.CurrentPosition, npc.Position) <= 45)) continue;
					if (!NaturalIshalgenHubPolicy.CanAccept(contract.Quests.Single(quest => quest.Id == id),
						session.Api.World)) continue;
					if (plan != null && plan.FinishedQuestGroups.Count > 0 &&
						!plan.FinishedQuestGroups.Any(group => group.All(session.Api.World.CompletedQuestIds.Contains)))
						continue;
					session.BeginStep($"ni07-hub-{hub.Name}-accept-{id}", "accept-all-available-hub-quests");
					int starter = await ApproachShippedSpawnAsync(starterId.Value);
					for (int attempt = 1; ; attempt++)
					{
						try { await session.StartQuestAsync(starter, id, token); break; }
						catch (NaturalDialogTooFarException) when (attempt < 3)
						{
							await ReapproachForDialogAsync(starter);
						}
					}
					session.TraceDiagnostic("hub-quest-accepted", new Dictionary<string, object?>
					{
						["hub"] = hub.Name, ["questId"] = id, ["starterId"] = starterId,
						["level"] = session.Api.World.Level,
					});
				}
			}

			async Task CompleteTemplateQuestAsync(QuestRunPlan plan, TemplatePhase phase = TemplatePhase.Full)
			{
				// Java ce54b7931 ItemCollecting, MonsterHunt, ReportTo. The plan
				// supplies shipped objectives only; this executor never uses Q4I's
				// Prepare grants, forced NPC HP, skill grants, neutrality or teleports.
				QuestRunBook book = QuestRunBook.Build(plan);
				for (int index = 0; index < book.Operations.Count; index++)
				{
					QuestRunOperation operation = book.Operations[index];
					if (phase == TemplatePhase.Work && operation.Kind == QuestRunOperationKind.ClaimReward ||
						phase == TemplatePhase.Claim && operation.Kind != QuestRunOperationKind.ClaimReward)
						continue;
					if (QuestStatus(plan.Id) == 5) return;
					if (QuestStatus(plan.Id) >= 3 && operation.Kind == QuestRunOperationKind.StartAtNpc) continue;
					if (QuestStatus(plan.Id) == 4 && operation.Kind != QuestRunOperationKind.ClaimReward) continue;
					session.BeginStep($"ni07-q{plan.Id}-{index}-{operation.Kind}",
						$"natural-template-{plan.Template}-{operation.Kind}");
					switch (operation.Kind)
					{
						case QuestRunOperationKind.Prepare:
							Require.True(plan.FinishedQuestGroups.Count == 0 ||
								plan.FinishedQuestGroups.Any(group => group.All(session.Api.World.CompletedQuestIds.Contains)),
								$"Q{plan.Id} prerequisites must be earned before starting.");
							break;
						case QuestRunOperationKind.StartAtNpc:
						{
							int starterId = operation.Npcs?.FirstOrDefault()?.Id ??
								throw new InvalidDataException($"Q{plan.Id} has no shipped NPC starter.");
							int starter = await ApproachShippedSpawnAsync(starterId);
							for (int attempt = 1; ; attempt++)
							{
								try { await session.StartQuestAsync(starter, plan.Id, token); break; }
								catch (NaturalDialogTooFarException) when (attempt < 3) { await ReapproachForDialogAsync(starter); }
							}
							Require.Equal(3, session.Api.World.Quests[plan.Id].Status);
							break;
						}
						case QuestRunOperationKind.Kill:
						{
							// Quest data can list alternatives that never spawn on this map (Q2117 names 210389 and
							// 210655; only 210389 has spawns): hunt what is actually there, as a player would.
							int[] targetIds = (operation.Npcs?.Select(npc => npc.Id).Distinct() ?? [])
								.Where(SpawnsOnMap).ToArray();
							// BC-06: the L17 source fulfills all four kills; the higher variant's camp stalled
							// the natural approach after the first counter. Farm the proved lower source instead.
							if (altgardLegId == "l10" && plan.Id == 2280) targetIds = targetIds.Where(id => id == 210508).ToArray();
							// BC-02/03: the lower L19 warlocks fulfill all three kills. The L20 variant is on the
							// gate ground reached only by Q24016's later teleport; it is not a pre-campaign hunt route.
							if (altgardLegId == "l10" && plan.Id == 2282) targetIds = targetIds.Where(id => id != 210539).ToArray();
							if (targetIds.Length == 0) throw new InvalidDataException($"Q{plan.Id} kill has no shipped target.");
							var exhaustedKinds = new HashSet<int>();
							for (int kill = 0; NaturalQuestProgress.RemainingKills(plan, operation, session.Api.World) > 0; kill++)
							{
								// BC-06: Q2281's alternatives occupy different camps. Stay with an observed nearby
								// valid source rather than crossing the camps after every counter increment.
								int[] available = targetIds.Where(id => !exhaustedKinds.Contains(id)).ToArray();
								if (available.Length == 0) throw new NaturalHaramelSourcesExhaustedException($"Q{plan.Id} has no remaining reachable sources in this copy.");
								int kind = altgardLegId is "l10" or "l12" ? NearestKind(available) : available[kill % available.Length];
								int target;
								try { target = await KillShippedSpawnAsync(kind,
									objectiveDone: () => NaturalQuestProgress.RemainingKills(plan, operation, session.Api.World) == 0); }
								catch (NaturalHaramelSourcesExhaustedException) when (altgardLegId == "l12" && available.Length > 1)
								{
									exhaustedKinds.Add(kind);
									continue; // Another qualifying kind may still meet this counter without a new entry.
								}
								if (target != 0) navigator.UnavailableObjects.Add(target);
								await RestSafelyAsync(token);
							}
							if (altgardLegId == "l10" && plan.Id == 2282)
							{
								NaturalAltgardHub hub = altgardLeg!.Hub;
								BotPosition hubAt = new(hub.Anchor[0], hub.Anchor[1], hub.Anchor[2], 0);
								if (session.Api.World.MapId != hub.MapId || Distance(session.CurrentPosition, hubAt) > 60)
									await UseLearnedReturnToBindAsync();
								await RestSafelyAsync(token);
							}
							break;
						}
						case QuestRunOperationKind.CollectQuestDrop:
						case QuestRunOperationKind.UseQuestObject:
						{
							int[] sourceIds = operation.Sources?.Select(source => source.Npc?.Id ?? 0)
								.Where(id => id > 0 && SpawnsOnMap(id)).Distinct().ToArray() ?? [];
							// BC-03/06: all ten trinkets can come from the L16 hunter. Wait for its proved ordinary
							// respawn instead of alternating into the upper L17 camp while the nearby source is dead.
							if (altgardLegId == "l10" && plan.Id == 2277) sourceIds = sourceIds.Where(id => id == 210551).ToArray();
							if (sourceIds.Length == 0)
								throw new InvalidDataException($"Q{plan.Id} item {operation.ItemId} has no shipped source.");
							for (int attempt = 0; ItemCount(session.Api.World, operation.ItemId) < operation.Count;
								attempt++)
							{
								int sourceId = altgardLeg?.PillarFlight != null ? NearestKind(sourceIds) : sourceIds[attempt % sourceIds.Length];
								if (sourceId >= 700000)
								{
									navigator.UnavailableObjects.Add(
										await UseAndLootQuestObjectAsync(sourceId, operation.ItemId, skipBlockedTarget: true));
									continue;
								}
								int source = await KillShippedSpawnAsync(sourceId, operation);
								// Defense and the general quest sweep may finish the collection during the kill's approach.
								if (ItemCount(session.Api.World, operation.ItemId) < operation.Count)
									await TryLootCorpseItemAsync(session, source, operation.ItemId, token);
								await RestSafelyAsync(token);
								navigator.UnavailableObjects.Add(source);
							}
							// Java QuestService.collectItemCheck accepts surplus and consumes only the required count.
							// Defensive kills and the quest-loot sweep can yield an extra item before this loop finishes.
							Require.True(ItemCount(session.Api.World, operation.ItemId) >= operation.Count,
								$"Q{plan.Id} needs at least {operation.Count} of item {operation.ItemId}.");
							break;
						}
						case QuestRunOperationKind.Report:
							// These frozen plans use only a final report; ClaimReward performs its NPC dialogue.
							break;
						case QuestRunOperationKind.Gather:
							await GatherAsync(operation);
							break;
						case QuestRunOperationKind.ClaimReward:
						{
							int recipientId = operation.Npcs?.FirstOrDefault()?.Id ??
								throw new InvalidDataException($"Q{plan.Id} has no shipped reward NPC.");
							if (plan.Id == 2129 &&
								session.Api.World.ObeliskBindPoint is { MapId: 220010000 } bind &&
								session.Api.World.Skills.ContainsKey(243) &&
								session.Api.Timing.TimeUntilCast(243) <= TimeSpan.FromSeconds(1) &&
								runtime.NowMillis - lastReturnMillis >= TimeSpan.FromMinutes(20).TotalMilliseconds + 1000)
							{
								BotPosition? rewardHint = graph.GetMap(contract.MapId)!.Waypoints
									.Where(waypoint => waypoint.TemplateId == recipientId)
									.Select(waypoint => (BotPosition?)waypoint.Position)
									.FirstOrDefault();
								if (rewardHint is BotPosition destination &&
									NaturalJourneyTravelPolicy.Choose(session.CurrentPosition, destination, bind,
										returnReady: true, flightFareAffordable: false) == NaturalTravelChoice.Return)
									await UseLearnedReturnToBindAsync();
							}
							QuestRunPosition? recipientAt = operation.Npcs?.FirstOrDefault()?.Positions.FirstOrDefault(position => !position.ConditionalEvent);
							int recipient;
							if (recipientAt is { } at && at.MapId != (altgardLeg?.Hub.MapId ?? contract.MapId) && altgardLeg?.Haramel == null)
							{
								Require.Equal(at.MapId, session.Api.World.MapId);
								float talkRange = runtime.Data.NpcDataDh.GetNpcTemplate(recipientId)?.GetTalkDistance() ?? 3;
								NaturalNavigationResult reached = await NaturalIshalgenNavigator.ExploreWithinRangeAsync(at.MapId, recipientId,
									new BotPosition(at.X, at.Y, at.Z, 0), talkRange, navigator, "NPC", token);
								Require.True(reached.Arrived, reached.Reason);
								recipient = Require.IsType<int>(reached.TargetObjectId);
							}
							else recipient = await ApproachShippedSpawnAsync(recipientId);
							int rewardAction = DialogAction.SELECTED_QUEST_NOREWARD;
							// AK-08: a leg's contract may fix the choice (Q2292's Turquoise Earrings); it outranks the inventory policy.
							if (altgardLeg?.RewardChoiceList.FirstOrDefault(choice => choice.QuestId == plan.Id) is { } chosen)
								rewardAction = NaturalAscensionContract.DialogActionId(chosen.Action);
							else if (plan.HasSelectableReward)
							{
								string root = runtime.RepoRoot;
								var inventory = NaturalIshalgenInventoryPolicy.Load(root,
									session.Api.World.Inventory.Values.Select(item => item.ItemId));
								int rewardIndex = inventory.ChooseReward(plan.Id, session.Api.World.Level,
									session.Api.World.Inventory.Values);
								Require.True(rewardIndex >= 0);
								rewardAction = DialogAction.SELECTED_QUEST_REWARD1 + rewardIndex;
							}
							for (int attempt = 1; ; attempt++)
							{
								try
								{
									if (plan.Template == "item_collecting")
										await FinishNaturalItemQuestAsync(recipient, plan.Id, rewardAction);
									else
										await FinishStandardQuestAsync(session, recipient, plan.Id, token, rewardAction);
									break;
								}
								catch (NaturalDialogTooFarException) when (attempt < 3) { await ReapproachForDialogAsync(recipient); }
								catch (QuestDialogEchoException followUp) when (altgardLeg?.Haramel != null && plan.Id is 28504 or 28505 &&
									followUp.Action == new PendingQuestDialogAction(recipient, rewardAction, plan.Id) &&
									session.Api.World.CompletedQuestIds.Contains(plan.Id) && session.Api.World.CompletedQuestCounts.GetValueOrDefault(plan.Id) == 1)
								{
									session.TraceDiagnostic("haramel-completed-reward-followup-refused", new Dictionary<string, object?> { ["quest"] = plan.Id, ["action"] = rewardAction });
									await session.SynchronizeAsync(token);
									break;
								}
							}
							Require.Contains(plan.Id, session.Api.World.CompletedQuestIds);
							break;
						}
						default:
							throw new InvalidDataException($"Q{plan.Id} needs natural {operation.Kind} capability.");
					}
				}
			}

			// Kill one shipped spawn of this template. A death mid-fight ends the engagement without kill
			// evidence (the monster resets while the bot revives at the obelisk): walk back and fight again,
			// as a player does, rather than treating the vanished target as an error. A target that despawns
			// or walks out of view for another reason is simply looked for again.
			async Task<int> KillShippedSpawnAsync(int templateId, QuestRunOperation? collection = null, Func<bool>? objectiveDone = null)
			{
				int? CompletedObjectiveSource()
				{
					if (objectiveDone?.Invoke() != true) return CompletedCollectionSource();
					// RC-11: defense can finish the counter while approach/recovery is still running.
					// Zero means no extra target was killed; the ordinary quest journal is the completion evidence.
					session.TraceDiagnostic("hunt-objective-completed-during-approach", new Dictionary<string, object?>
					{
						["templateId"] = templateId, ["position"] = session.CurrentPosition,
					});
					return 0;
				}
				int CollectedSource(int objectId)
				{
					// RC-11: a partially collected stack still needs a new live source. The first
					// full run selected the same one-tail Ampha corpse for all thirty attempts.
					if (collection != null) navigator.UnavailableObjects.Add(objectId);
					return objectId;
				}
				// AO-04: defense can kill and loot the requested source while navigation is still approaching it.
				// Client inventory plus the ordinary loot record completes that objective; do not wait for another live spawn.
				int? CompletedCollectionSource() => collection != null && ItemCount(session.Api.World, collection.ItemId) >= collection.Count &&
					SweptQuestItems.TryGetValue(session.Api.World, out Dictionary<int, List<int>>? swept)
					? swept.Where(entry => entry.Value.Contains(collection.ItemId)).Select(entry => (int?)entry.Key).LastOrDefault()
					: null;
				int unsuccessfulKills = 0, tacticalRetreats = 0;
				var failedTargets = new Dictionary<int, int>();
				for (int attempt = 1; ; attempt++)
				{
					// RC-03: a bind death can leave Ishalgen. Let the normal transport loop return before another pull.
					if (laterCapital != null && collection?.ItemId == 182207011 && session.Api.World.MapId != collection.MapId) return 0;
					// Stop at pull range, outside the target's circle: the fight is planned from there, not started
					// by walking into it (which is how every add reached the bot at Hatata's cave).
					int approachEvidenceStart = session.PacketHistory.Count;
					int target = await ApproachShippedSpawnAsync(templateId, withinRange: NaturalPullPlanner.SpellRange + 3,
						completedSource: collection == null && objectiveDone == null ? null : CompletedObjectiveSource,
						acceptObservedKill: altgardLegId is "l10" or "l12");
					if (objectiveDone?.Invoke() == true) return 0;
					if (CompletedCollectionSource() is int collectedFrom) return CollectedSource(collectedFrom);
					if (altgardLegId is "l10" or "l12" && session.PacketHistory.Skip(approachEvidenceStart).Any(packet =>
						packet.PacketType == typeof(SmAttackStatus) && packet.Get<byte>("typeId") is not (19 or 20 or 21 or 22 or 23) &&
						packet.Get<int>("objectId") == target && packet.Get<byte>("hpOrMp") == 0))
						return CollectedSource(target);
					int revives = combat.ReviveCount;
					int retreats = combat.CompletedRetreats;
					int evidenceStart = session.PacketHistory.Count;
					bool killed = await PullAndKillAsync(target, $"kill-{templateId}");
					// Pull planning may defend against this very target as an add, then report
					// it vanished when its corpse leaves the visible NPC list. At the level cap
					// the EXP total cannot rise; its 0% HP packet is still kill evidence.
					if (killed || session.PacketHistory.Skip(evidenceStart).Any(packet =>
						packet.PacketType == typeof(SmAttackStatus) &&
						packet.Get<int>("objectId") == target && packet.Get<byte>("hpOrMp") == 0))
						return CollectedSource(target);
					bool died = combat.ReviveCount > revives;
					bool retreated = !died && combat.CompletedRetreats > retreats;
					// AK-08: the same monster failing twice with no death or retreat cannot be fought from anywhere the bot reaches
					// (the Leg 5 smoke run: a Sumarhon sentry 2.8 m below the Cleric's ledge, every cast STR_SKILL_OBSTACLE). Leave it,
					// as a player does, and pull another of its kind.
					if (!died && !retreated && failedTargets.TryGetValue(target, out int failures) && failures >= 1)
					{
						navigator.UnavailableObjects.Add(target);
						session.TraceDiagnostic("kill-target-abandoned", new Dictionary<string, object?>
						{
							["template"] = templateId, ["target"] = target, ["position"] = session.CurrentPosition,
						});
					}
					if (!died && !retreated) failedTargets[target] = failedTargets.GetValueOrDefault(target) + 1;
					session.TraceDiagnostic(died ? "kill-retry-after-death" : retreated ? "kill-retry-after-retreat" : "kill-target-vanished", new Dictionary<string, object?>
					{
						["template"] = templateId,
						["target"] = target,
						["attempt"] = attempt,
						["tacticalRetreats"] = tacticalRetreats,
						["position"] = session.CurrentPosition,
					});
					if (retreated)
					{
						if (++tacticalRetreats >= 12)
							throw new InvalidDataException($"NPC {templateId} remains guarded after {tacticalRetreats} disengagement retreats.");
					}
					else if (++unsuccessfulKills >= 6)
						throw new InvalidDataException($"NPC {templateId} was not killed in {unsuccessfulKills} non-retreat attempts (last target {target}).");
					await RestSafelyAsync(token);
				}
			}

			// Pull one quest target the way a player pulls a named: from the firing spot with the fewest adds, and
			// only after the monsters that would actually join (the server's assist rule at that spot, plus any circle
			// that reaches it: NaturalPullPlanner.AddsAt) have been pulled and killed one at a time, each planned the
			// same way. Nothing else nearby is touched. Adds respawn in 180 s, so rest between them only when needed
			// and engage the target inside that window. With no clean spot at all (a dense cave), fight from here and
			// take the adds as they come.
			async Task<bool> PullAndKillAsync(int target, string purpose)
			{
				int startingRevives = combat.ReviveCount;
				bool LostEngagement() => session.Api.World.IsDead || combat.ReviveCount != startingRevives;
				for (int add = 0; add < 8; add++)
				{
					if (LostEngagement()) return false;
					NaturalNavigationObject? npc = navigator.Observe().Npcs.FirstOrDefault(n => n.ObjectId == target);
					if (npc == null) return false;
					NaturalPullPlan? plan = await MoveToPullSpotAsync([npc], [], purpose);
					if (LostEngagement()) return false;
					if (plan == null)
					{
						// No spot in range, sight and outside every circle. Patrols move: watch them walk on and plan
						// again, up to a minute, as a player waits at the cave mouth for the patrol to pass.
						for (int patience = 0; plan == null && patience < 6 && !LostEngagement(); patience++)
						{
							session.TraceDiagnostic("pull-wait-for-patrol", new Dictionary<string, object?>
							{
								["purpose"] = purpose,
								["target"] = $"{npc.TemplateId}/{npc.ObjectId}",
								["patience"] = patience,
								["position"] = session.CurrentPosition,
							});
							if (altgardLegId == "l10")
							{
								if (!await WaitBeforePullDefendingAsync(10000, purpose)) return false;
							}
							else await session.AdvanceAsync(TimeSpan.FromSeconds(10), token);
							await navigator.SynchronizeAsync(token);
							npc = navigator.Observe().Npcs.FirstOrDefault(n => n.ObjectId == target);
							if (npc == null) return false;
							plan = await MoveToPullSpotAsync([npc], [], purpose);
						}
						if (LostEngagement()) return false;
						if (plan != null) continue; // plan the adds from the spot found
						// Still none: the fight will be at the target itself. Take the adds that would join there
						// first, nearest first, each from a spot of its own if it has one, else where it stands. Two
						// Gray Mane patrols at Hatata's side took four lives in one run before this.
						NaturalPullMonster[] monstersNow = ObservedPullMonsters();
						NaturalPullMonster standTarget = monstersNow.FirstOrDefault(m => m.Npc.ObjectId == target) ?? PullMonsterOf(npc);
						IReadOnlyList<NaturalPullMonster> addsThere = NaturalPullPlanner.AddsAt(standTarget, npc.Position, monstersNow,
							CanSupport, (a, b) => geometry.HasLineOfSight(contract.MapId, a, b), NaturalPriestCombatPolicy.MeleeReach);
						session.TraceDiagnostic("adds-that-would-join", new Dictionary<string, object?>
						{
							["purpose"] = purpose + "-at-target",
							["target"] = $"{standTarget.Npc.TemplateId}/{standTarget.Npc.ObjectId}",
							["firingPosition"] = npc.Position,
							["adds"] = addsThere.Select(a => $"{a.Npc.TemplateId}/{a.Npc.ObjectId}").ToArray(),
						});
						foreach (NaturalPullMonster addMonster in addsThere.OrderBy(a => Distance(session.CurrentPosition, a.Npc.Position)))
						{
							if (navigator.UnavailableObjects.Contains(addMonster.Npc.ObjectId)) continue;
							NaturalPullPlan? addPlanThere = await MoveToPullSpotAsync([addMonster.Npc], [], purpose + "-add");
							if (LostEngagement()) return false;
							if (addPlanThere == null)
							{
								NaturalNavigationResult closeAdd = await NaturalIshalgenNavigator.ApproachNpcAsync(
									contract.MapId, addMonster.Npc.TemplateId, addMonster.Npc.Position, navigator, token);
								if (LostEngagement()) return false;
								if (!closeAdd.Arrived) continue;
							}
							int revivesAdd = combat.ReviveCount;
							bool killedThere;
							try { killedThere = await combat.TryKillAsync(addMonster.Npc.ObjectId, token, session.CurrentPosition); }
							catch (NaturalCombatApproachBlockedException) { killedThere = false; }
							if (combat.ReviveCount > revivesAdd) return false;
							if (killedThere) navigator.UnavailableObjects.Add(addMonster.Npc.ObjectId);
							if (session.Api.World.CurrentHp * 100 < session.Api.World.MaxHp * 60 ||
								session.Api.World.CurrentMp * 100 < session.Api.World.MaxMp * 40)
								await RestSafelyAsync(token);
							else
								await combat.BuffOurselfAsync(NaturalHelpTrigger.PrePull, token);
						}
						npc = navigator.Observe().Npcs.FirstOrDefault(n => n.ObjectId == target);
						if (npc == null) return false;
						NaturalNavigationResult close = await NaturalIshalgenNavigator.ApproachNpcAsync(
							contract.MapId, npc.TemplateId, npc.Position, navigator, token);
						if (LostEngagement()) return false;
						if (!close.Arrived && !await TryClearObservedBlockerAsync(npc.Position, npc.ObjectId)) return false;
						break;
					}
					NaturalPullMonster[] monsters = ObservedPullMonsters();
					NaturalPullMonster pullTarget = monsters.FirstOrDefault(m => m.Npc.ObjectId == target) ?? PullMonsterOf(
						navigator.Observe().Npcs.FirstOrDefault(n => n.ObjectId == target) ?? npc);
					IReadOnlyList<NaturalPullMonster> adds = NaturalPullPlanner.AddsAt(pullTarget, session.CurrentPosition, monsters,
						CanSupport, (a, b) => geometry.HasLineOfSight(contract.MapId, a, b), NaturalPriestCombatPolicy.MeleeReach);
					session.TraceDiagnostic("adds-that-would-join", new Dictionary<string, object?>
					{
						["purpose"] = purpose,
						["target"] = $"{pullTarget.Npc.TemplateId}/{pullTarget.Npc.ObjectId}",
						["firingPosition"] = session.CurrentPosition,
						["adds"] = adds.Select(a => $"{a.Npc.TemplateId}/{a.Npc.ObjectId}").ToArray(),
					});
					if (adds.Count == 0) break;
					// The add with the fewest adds of its own goes first, so each fight stays one-on-one.
					NaturalPullPlan? addPlan = await MoveToPullSpotAsync(adds.Select(a => a.Npc).ToArray(), [], purpose + "-add");
					if (LostEngagement()) return false;
					if (addPlan == null) break;
					int revivesBefore = combat.ReviveCount;
					bool killedAdd;
					try { killedAdd = await combat.TryKillAsync(addPlan.Target.Npc.ObjectId, token, session.CurrentPosition); }
					catch (NaturalCombatApproachBlockedException) { killedAdd = false; }
					if (combat.ReviveCount > revivesBefore) return false;
					if (killedAdd) navigator.UnavailableObjects.Add(addPlan.Target.Npc.ObjectId);
					if (session.Api.World.CurrentHp * 100 < session.Api.World.MaxHp * 60 ||
						session.Api.World.CurrentMp * 100 < session.Api.World.MaxMp * 40)
						await RestSafelyAsync(token);
					else
						await combat.BuffOurselfAsync(NaturalHelpTrigger.PrePull, token);
				}
				// A named is engaged rested: full heals in reserve matter more than the respawn window's last seconds.
				if (session.Api.World.CurrentHp * 100 < session.Api.World.MaxHp * 80 ||
					session.Api.World.CurrentMp * 100 < session.Api.World.MaxMp * 60)
					await RestSafelyAsync(token);
				if (LostEngagement()) return false;
				try { return await combat.TryKillAsync(target, token, session.CurrentPosition); }
				catch (NaturalCombatApproachBlockedException) { return false; }
			}

			int GatheringSkillLevel() =>
				session.Api.World.Skills.TryGetValue(30001, out BotSkill? gatheringSkill) ? gatheringSkill.Level : 0;

			// A collect step served by gatherable nodes: Q2133's Young Azpha (skill 1) and Q2134's Impure Iron Ore
			// (skill 15). The plan lists every node template that yields the item on every map; only the spots on
			// this map count. Both require human Collecting 30001, not the Daeva's Essencetapping 30002:
			// OD-16 schedules them before Ascension. When Collecting is below the node's level, skill up on Young Azpha,
			// as a player does (Java GatherableController.OnStartUse refuses a node above the skill level, and
			// PlayerSkillList.AddSkillXp raises the skill one point per ~76-224 xp at 91 xp a harvest).
			async Task GatherAsync(QuestRunOperation operation)
			{
				QuestRunSource source = (operation.Sources ?? []).Prepend(operation.Source).OfType<QuestRunSource>()
					.FirstOrDefault(candidate => candidate.Kind == "gatherable" && candidate.GatherableId is > 0 &&
						candidate.Positions.Any(position => position.MapId == contract.MapId))
					?? throw new InvalidDataException($"Item {operation.ItemId} has no gatherable node on map {contract.MapId}.");
				if (GatheringSkillLevel() < 1)
					throw new InvalidDataException("Client did not observe the required level-1 gathering skill.");
				if (GatheringSkillLevel() < source.SkillLevel)
				{
					session.TraceDiagnostic("gather-train", new Dictionary<string, object?>
					{
						["template"] = source.GatherableId,
						["requiredSkillLevel"] = source.SkillLevel,
						["gatheringSkillLevel"] = GatheringSkillLevel(),
					});
					var trainer = new NaturalIshalgenGatheringPolicy(SoakGatheringPool.StarterSpots(),
						GatheringTarget.YoungAzpha.MapId, GatheringTarget.YoungAzpha.TemplateId, long.MaxValue);
					for (int round = 0; round < 8 && GatheringSkillLevel() < source.SkillLevel; round++)
						await HarvestAsync(trainer, GatheringTarget.YoungAzpha.ItemId, "Essencetapping practice",
							() => GatheringSkillLevel() >= source.SkillLevel);
					if (GatheringSkillLevel() < source.SkillLevel)
						throw new InvalidDataException($"Essencetapping stayed at {GatheringSkillLevel()} below the node's level {source.SkillLevel}.");
				}
				IEnumerable<SoakGatheringSpot> spots = source.GatherableId == GatheringTarget.YoungAzpha.TemplateId
					? SoakGatheringPool.StarterSpots()
					: source.Positions.Where(position => position.MapId == contract.MapId)
						.Select(position => new SoakGatheringSpot(position.MapId, 1, source.GatherableId!.Value, position.X, position.Y, position.Z));
				var policy = new NaturalIshalgenGatheringPolicy(spots, contract.MapId, source.GatherableId!.Value, operation.Count);
				await HarvestAsync(policy, operation.ItemId, $"gatherable {source.GatherableId}",
					() => ItemCount(session.Api.World, operation.ItemId) >= operation.Count);
				Require.Equal(operation.Count, ItemCount(session.Api.World, operation.ItemId));
			}

			async Task HarvestAsync(NaturalIshalgenGatheringPolicy policy, int itemId, string purpose, Func<bool> done)
			{
				for (int decisionIndex = 0; decisionIndex < 96 && !done(); decisionIndex++)
				{
					NaturalGatherNode[] visible = session.Api.World.Objects.Values
						.Where(obj => obj.Kind == BotKnownObjectKind.Gatherable &&
							obj.TemplateId == policy.TemplateId)
						.Select(obj => new NaturalGatherNode(obj.ObjectId, obj.Position)).ToArray();
					TimeSpan elapsed = TimeSpan.FromMilliseconds(runtime.NowMillis);
					NaturalGatherChoice choice = policy.Decide(session.CurrentPosition, visible,
						ItemCount(session.Api.World, itemId), elapsed);
					session.TraceDiagnostic("gather-decision", new Dictionary<string, object?>
					{
						["purpose"] = purpose,
						["template"] = policy.TemplateId,
						["gatheringSkillLevel"] = GatheringSkillLevel(),
						["action"] = choice.Action,
						["reason"] = choice.Reason,
						["objectId"] = choice.ObjectId,
						["spot"] = choice.Spot?.Position,
						["inventoryCount"] = ItemCount(session.Api.World, itemId),
					});
					if (choice.Action == "explore" && choice.Spot is { } hint)
					{
						NaturalNavigationResult search = await NaturalIshalgenNavigator.ExploreAnchorAsync(
							contract.MapId, -1, hint.Position, navigator, $"{purpose} spawn hint", token);
						if (!search.Arrived)
						{
							if (search.Reason != "No collision-checked route to the current destination." ||
								!await TryClearObservedBlockerAsync(hint.Position))
								policy.RecordUnreachable(hint, TimeSpan.FromMilliseconds(runtime.NowMillis));
						}
						else if (!session.Api.World.Objects.Values.Any(obj =>
							obj.Kind == BotKnownObjectKind.Gatherable &&
							obj.TemplateId == policy.TemplateId &&
							Distance(obj.Position, hint.Position) < 1))
							policy.RecordUnobserved(hint, TimeSpan.FromMilliseconds(runtime.NowMillis));
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
						throw new InvalidDataException($"Unexpected {purpose} decision {choice.Action}.");
					NaturalNavigationResult approach = await NaturalIshalgenNavigator.ApproachObservedObjectAsync(
						contract.MapId, policy.TemplateId, spot.Position,
						navigator, purpose, token);
					if (!approach.Arrived || approach.TargetObjectId != objectId)
					{
						if (approach.Reason != "No collision-checked route to the current destination." ||
							!await TryClearObservedBlockerAsync(spot.Position, objectId))
							policy.RecordUnreachable(spot, TimeSpan.FromMilliseconds(runtime.NowMillis));
						continue;
					}
					long before = ItemCount(session.Api.World, itemId);
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
						update = finished ?? throw new TimeoutException($"{purpose} harvest did not finish within 60 virtual seconds.");
					}
					byte outcome = update.Get<byte>("action");
					policy.RecordAttempt(spot, objectId, outcome, TimeSpan.FromMilliseconds(runtime.NowMillis));
					if (outcome == 6)
					{
						for (int followup = 0; followup < 5 && ItemCount(session.Api.World, itemId) == before; followup++)
						{
							await session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
							await navigator.SynchronizeAsync(token);
						}
						Require.Equal(before + 1, ItemCount(session.Api.World, itemId));
					}
				}
			}

			// A walking NPC moved on between the client-estimated arrival and the talk (Q2110's starter is a
			// walker): walk up to it again, as a player does when the "too far" message appears.
			async Task ReapproachForDialogAsync(int npc)
			{
				NaturalNavigationObject? seen = navigator.Observe().Npcs.FirstOrDefault(n => n.ObjectId == npc);
				if (seen == null) throw new InvalidDataException($"NPC {npc} is too far to talk to and no longer observed.");
				session.TraceDiagnostic("dialog-too-far", new Dictionary<string, object?>
				{
					["npc"] = $"{seen.TemplateId}/{npc}",
					["distance"] = Distance(session.CurrentPosition, seen.Position),
					["position"] = session.CurrentPosition,
				});
				NaturalNavigationResult again = await NaturalIshalgenNavigator.ApproachNpcAsync(
					contract.MapId, seen.TemplateId, seen.Position, navigator, token);
				if (!again.Arrived) throw new InvalidDataException($"Could not walk back to NPC {npc} to talk: {again.Reason}");
				// Arrival permits 3 m of client-estimated distance. A talk refusal is
				// stronger server evidence: send the final short movement and allow its
				// ground position to settle before retrying the same ordinary dialogue.
				seen = navigator.Observe().Npcs.FirstOrDefault(n => n.ObjectId == npc);
				if (seen != null && Distance(session.CurrentPosition, seen.Position) > 0.4f)
					await navigator.MoveAsync([seen.Position], token);
				await navigator.SynchronizeAsync(token);
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(500), token);
			}

			async Task OpenQuestDialogAsync(int npc, int questId)
			{
				for (int attempt = 1; ; attempt++)
				{
					try
					{
						await NaturalDialogProtocol.OpenAsync(session, npc, token);
						await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
							packet => packet.Get<int>("targetObjectId") == npc);
						break;
					}
					catch (NaturalDialogTooFarException) when (attempt < 3)
					{
						await ReapproachForDialogAsync(npc);
					}
				}
				await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npc, DialogAction.QUEST_SELECT, questId: questId), token);
				await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
					packet => packet.Get<int>("targetObjectId") == npc && packet.Get<int>("questId") == questId);
			}
		}
		catch (Exception failure)
		{
			WriteFailure(failure is TimeoutException ? "stall" : failure is OperationCanceledException ? "cancelled" :
				"execution", failure);
			session.PublishDashboard("failed", force: true);
			throw;
		}
	}

	/// <summary>Bridge talk steps the runner can play so far (NA-12: the Q2008 Norn circuit into Ataxiar).</summary>
	private static readonly HashSet<string> ImplementedBridgeSteps =
		["q2008-v0-munin", "q2008-v1-urd", "q2008-v2-verdandi", "q2008-v3-skuld", "q2008-v4-munin",
		"q2008-v99-hagen", "q2008-v6-munin-class", "q2008-reward-munin",
		"q2009-v0-munin", "q2009-v1-heimdall", "q2009-v2-balder", "q2009-reward-lyfjaberga",
		"q2904-v0-doman", "q2904-reward-meiyer", "q24010-reward-suthran"];

	private static NaturalIshalgenObservation ObserveNaturalJourney(INaturalJourneySession session)
	{
		BotWorldModel world = session.Api.World;
		return new NaturalIshalgenObservation(true,
			world.QuestJournalObserved, world.CompletedJournalObserved,
			world.MapId, world.Level, world.IsDead,
			new Dictionary<int, BotQuestState>(world.Quests), world.CompletedQuestIds.ToHashSet(),
			session.CurrentPosition, world.Objects.Values.ToArray())
		{
			PlayerClass = world.Objects.GetValueOrDefault(session.CharacterId)?.PlayerClass,
		};
	}

	private sealed class NaturalJourneyNavigator(INaturalJourneySession session,
		BotNavigationGraph graph, BotNavigationGeometry geometry, NaturalJourneyRuntime runtime, bool stopOnDeath) : INaturalNavigationDriver
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
					item.TemplateId != null && !item.IsCorpse &&
					!UnavailableObjects.Contains(item.ObjectId))
					.Select(item => new NaturalNavigationObject(item.ObjectId, item.TemplateId!.Value,
						attackers.Contains(item.ObjectId) ? item.Position : item.SettledPosition, item.PatrolPath)).ToArray());
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
			BotNavigationHazard[] otherHazards = ObservedHazards(target, targetObjectId);
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

		/// <summary>Places to keep clear of on routes: where the Cleric died (the combat's death spots). A swamp mosbear family
		/// senses only 6 m, so its circles are small, but brushing one brings all six: the Leg 4 catch-up died on the same
		/// spot six times walking the same tight route back to Sumarhon.</summary>
		public IReadOnlyList<BotPosition> AvoidSpots { get; set; } = [];
		private const float AvoidSpotRadius = 20f;
		private const float StaleSegmentDistance = 30f;

		private BotNavigationHazard[] ObservedHazards(BotPosition? destination,
			int? targetObjectId = null)
		{
			if (!AvoidHostileAggro) return [];
			IReadOnlyList<NaturalNavigationObject> observed = Observe().Npcs;
			// Segment checks must use the same destination exemption as route planning.
			// BC-06 otherwise repeatedly rejected the route back to a target beside a death spot.
			BotPosition? objective = destination ?? observed
				.FirstOrDefault(npc => npc.ObjectId == targetObjectId)?.Position;
			return observed
			.Where(npc => npc.ObjectId != targetObjectId &&
				(destination == null || Distance(npc.Position, destination.Value) > 0.1f))
			.Select(npc => (npc,
				template: runtime.Data.NpcDataDh.GetNpcTemplate(npc.TemplateId)))
			.Where(entry => runtime.IsAggressive(entry.template) &&
				entry.template.GetAggroRange() > 0)
			.SelectMany(entry => entry.npc.Hazards(entry.template!.GetAggroRange() + 1f, BotPatrolPath.PassingReach))
			// A death spot is avoided unless the destination itself lies there (a target the Cleric died beside).
			.Concat(AvoidSpots.Where(spot => objective == null || Distance(spot, objective.Value) > AvoidSpotRadius)
				.Select(spot => new BotNavigationHazard(spot, AvoidSpotRadius))).ToArray();
		}

		public async Task MoveAsync(IReadOnlyList<BotPosition> segment, CancellationToken token)
		{
			// A segment planned before a revive or a teleport starts where the Cleric no longer is. Walking it would cross
			// straight from the obelisk to the old route (the Leg 4 catch-up walked 270 m that way, back into the mosbears
			// that had just killed it, six times). Refuse it; the caller sees no progress and plans a new route from here.
			if (segment.Count > 0 && Distance(session.CurrentPosition, segment[0]) > StaleSegmentDistance)
			{
				session.TraceDiagnostic("stale-segment-refused", new Dictionary<string, object?>
				{
					["position"] = session.CurrentPosition, ["firstPoint"] = segment[0],
					["distance"] = Distance(session.CurrentPosition, segment[0]),
				});
				return;
			}
			// The regular client closes an NPC window before walking away. This also delivers
			// DIALOG_FINISH to the NPC AI, so it can resume its idle movement and facing.
			if (segment.Count > 0 && session.Api.OpenDialogTargetId is int dialogTarget)
				await session.SendPacketAsync(session.Api.CloseDialog(dialogTarget), token);
			lastMovementStart = session.CurrentPosition;
			await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing)
				.CreateGroundPlan(segment, session.CurrentPosition,
					session.Api.World.MovementSpeed ?? throw new InvalidDataException("SIM movement speed unobserved.")), token);
		}

		public async Task SynchronizeAsync(CancellationToken token)
		{
			int packetStart = session.PacketHistory.Count;
			await session.SynchronizeAsync(token);
			if (stopOnDeath && session.Api.World.IsDead)
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

	private sealed class NaturalJourneyCombat(INaturalJourneySession session,
		NaturalJourneyNavigator navigator, NaturalJourneyRuntime runtime,
		BotNavigationGeometry geometry, bool stopOnDeath, bool conservativeRangedHold,
		NaturalMauPolicyParameters mauPolicy, int initialRevives = 0)
	{
		// A contained encounter or map segment may create another observer for the same client.
		// Java Skill.setCooldowns belongs to the player, not that observer: retain the packet-derived
		// group deadlines (Herb Treatment and MP Recovery share 1153) across those boundaries.
		private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<BotWorldModel,
			Dictionary<int, DateTimeOffset>> CooldownsByWorld = new();
		private readonly Dictionary<int, DateTimeOffset> cooldowns = CooldownsByWorld.GetValue(session.Api.World, _ => new());
		// NA-18: the chain the last chain skill opened, from its SM_CASTSPELL_RESULT chain flag (Java ChainSkills).
		private (string Category, int Target, DateTimeOffset ExpiresAt)? openChain;
		private ushort? lastCancelledSkillId;
		private ushort? lastPowderSkill;
		// NA-19: when the current visible-effect snapshot was first seen (its remaining times are as of then).
		private IReadOnlyList<BotVisibleEffect>? effectsSnapshot;
		private long effectsSeenAtMillis;
		private int revives = initialRevives is >= 0 and <= MaximumRevives ? initialRevives : throw new ArgumentOutOfRangeException(nameof(initialRevives));
		private int completedRetreats;
		private int? engagedTarget;
		private int combatAttemptId;
		private string[] lastCombatTrace = [];
		private int obstacleRepositions;
		private int rangeRejections;
		public int ReviveCount => revives;
		/// <summary>The map a far target is approached on (NA-23 fights in Altgard); Ishalgen for the journey.</summary>
		public int ApproachMapId { get; set; } = 220010000;
		public Action? AfterBindRevive { get; set; }

		/// <summary>BC-06: retain combat/revival counters while switching to the new map's checked navigation.</summary>
		public void EnterMap(NaturalJourneyNavigator currentNavigator, BotNavigationGeometry currentGeometry, int mapId)
		{
			navigator.InCombat = false;
			navigator = currentNavigator;
			navigator.InCombat = InCombat;
			geometry = currentGeometry;
			ApproachMapId = mapId;
			openChain = null;
		}

		/// <summary>NA-18: the observed class chooses the catalog (the Cleric adds its level 10 skills).</summary>
		private NaturalPriestSkill[] Catalog => NaturalClericSkills.ForClass(
			session.Api.World.Objects.GetValueOrDefault(session.CharacterId)?.PlayerClass);
		public int CompletedRetreats => completedRetreats;
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
				.OrderBy(npc => PriorityForEngagedTarget(npc.ObjectId, observedAttackers.Count,
					session, mauPolicy.PreferWoundedWhenTwoAttackers))
				.ThenBy(npc => Distance(session.CurrentPosition, npc.Position))
				.Select(npc => npc.ObjectId).ToArray())
			{
				cornered = true;
				await TryKillAsync(attacker, token);
				if (session.Api.World.IsDead || session.Api.World.CurrentHp <= 0) return;
			}
		}

		// Set when a retreat found no checked way out; the policy then fights instead of retreating again.
		private bool cornered;

		/// <summary>NA-13: Q2008's Ataxiar trial — no exit and 1-damage NPCs, so never retreat from the swarm.</summary>
		public bool ScriptedTrial { get; set; }
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

		/// <summary>Runs after every fight that ends in a kill, outside the fight (the general quest-loot sweep).</summary>
		public Func<CancellationToken, Task>? AfterKillAsync { get; set; }

		public async Task<bool> TryKillAsync(int target, CancellationToken token,
			BotPosition? retreatAnchor = null, int? attackHistoryStart = null)
		{
			int? fightMap = session.Api.World.MapId;
			bool killed = await FightAsync(target, token, retreatAnchor, attackHistoryStart);
			if (killed && session.Api.World.MapId == fightMap && AfterKillAsync is { } afterKill && !session.Api.World.IsDead)
				await afterKill(token);
			return killed;
		}

		private async Task<bool> FightAsync(int target, CancellationToken token, BotPosition? retreatAnchor, int? attackHistoryStart)
		{
			if (InCombat) throw new InvalidOperationException("Natural Priest combat cannot nest another fight.");
			int attemptId = ++combatAttemptId;
			int revivesBefore = revives;
			InCombat = true;
			navigator.InCombat = true;
			session.TraceDiagnostic("combat-encounter-start", new Dictionary<string, object?>
			{
				["encounterId"] = attemptId, ["targetObjectId"] = target,
			});
			try
			{
				bool killed = await TryKillCoreAsync(target, token, retreatAnchor, attackHistoryStart);
				session.TraceDiagnostic("combat-encounter-end", new Dictionary<string, object?>
				{
					["encounterId"] = attemptId, ["targetObjectId"] = target,
					["clientObservedKill"] = killed, ["revives"] = revives - revivesBefore,
					["retreats"] = completedRetreats,
				});
				return killed;
			}
			catch (Exception error)
			{
				session.TraceDiagnostic("combat-encounter-end", new Dictionary<string, object?>
				{
					["encounterId"] = attemptId, ["targetObjectId"] = target,
					["clientObservedKill"] = false, ["revives"] = revives - revivesBefore,
					["error"] = error.GetType().Name,
				});
				throw;
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
			var targetTemplate = navigator.Observe().Npcs.FirstOrDefault(npc => npc.ObjectId == target) is { } observedTarget
				? runtime.Data.NpcDataDh.GetNpcTemplate(observedTarget.TemplateId) : null;
			bool targetSeasoned = targetTemplate != null && targetTemplate.GetRank() >= Aion.GameServer.Model.Templates.Npc.NpcRank.SEASONED;
			// A monster that attacks from range (the thorned ampha, 37 m) hits without being anywhere near.
			bool targetRanged = targetTemplate != null && targetTemplate.GetAttackRange() > NaturalPriestCombatPolicy.MeleeReach + 1;
			long? lastHitByTargetMillis = null;
			IReadOnlySet<int> blessingIds = NaturalPriestSkills.Ids("blessing");
			IReadOnlySet<int> rejuvenationIds = NaturalClericSkills.Cleric.Where(skill => skill.Role == "rejuvenation")
				.Select(skill => (int)skill.Id).ToHashSet();
			var incomingAttackers = new HashSet<int>();
			// Fights may run long: a cornered Priest alternates heals and damage, and respawns or chain
			// aggro can keep adding monsters. The bound only stops a genuine stall (every action rejected
			// forever) from hanging the run; at a few seconds per action it is about an hour of game time.
			for (int turn = 0; turn < MaximumCombatActions; turn++)
			{
				trace.Add($"t{turn}:pre-sync hp={world.CurrentHp}/{world.MaxHp} mp={world.CurrentMp}/{world.MaxMp} " +
					$"dead={world.IsDead} pos={session.CurrentPosition}");
				await session.SynchronizeAsync(token);
				DecodedBotServerPacket[] recentPackets = session.PacketHistory.Skip(observedPacketCount).ToArray();
				DecodedBotServerPacket[] recentAttacks = recentPackets
					.Where(packet => packet.PacketType == typeof(SM_ATTACK) &&
						packet.Get<int>("targetObjId") == session.CharacterId).ToArray();
				NaturalCombatRetreatPolicy.ObserveEngagement(incomingAttackers,
					recentPackets, session.CharacterId, LocalizedName);
				bool targetReturned = NaturalCombatRetreatPolicy.TargetReturned(recentPackets,
					target, session.CharacterId, targetTemplate?.GetL10n());
				foreach (DecodedBotServerPacket attack in recentAttacks)
				{
					int attacker = attack.Get<int>("attackerObjId");
					if (attacker == target) lastHitByTargetMillis = runtime.NowMillis;
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
				// Kill evidence: experience, a loot window, or the target's own status at 0% HP (a grey monster
				// pays no experience, so SM_STATUPDATE_EXP never comes; SM_DELETE follows the death status).
				if (world.CurrentExperience > startingExperience || world.LootStatuses.ContainsKey(target) ||
					observedTargetHpPercent == 0) return true;
				if (targetReturned)
				{
					session.TraceDiagnostic("combat-target-returned", new Dictionary<string, object?>
					{
						["targetObjectId"] = target, ["npcId"] = targetTemplate?.GetTemplateId(),
						["clientObservedKill"] = false,
					});
					return false; // Ordinary give-up, not a corpse: let the caller resume/re-plan.
				}
				if (!world.Objects.TryGetValue(target, out BotKnownObject? npc))
					return false; // Reacquire a new client-observed mob; do not count this as a kill.
				DateTimeOffset now = runtime.Epoch.AddMilliseconds(runtime.NowMillis);
				// A monster that hit us in the last 3 s is in melee reach whatever its lagging client position says.
				bool targetAdjacent = !targetRanged && lastHitByTargetMillis is long lastHit && runtime.NowMillis - lastHit <= 3000 ||
					Distance(session.CurrentPosition, npc.Position) <= NaturalPriestCombatPolicy.MeleeReach;
				if (world.CurrentHp * 100 <= world.MaxHp * NaturalPriestCombatPolicy.EmergencyEnterPercent(nearbyAttackers, targetSeasoned)) inEmergency = true;
				else if (world.CurrentHp * 100 >= world.MaxHp * NaturalPriestCombatPolicy.EmergencyExitPercent(nearbyAttackers, targetSeasoned)) inEmergency = false;
				bool hasBlessing = world.VisibleEffects?.Any(effect => blessingIds.Contains(effect.SkillId)) == true;
				BotInventoryItem? hotPotion = NaturalIshalgenPotionPolicy.SelectOwnedPotion(world.Inventory.Values);
				var hotTemplate = hotPotion == null ? null :
					runtime.Data.ItemDataDh.GetItemTemplate(hotPotion.ItemId);
				bool hotReady = hotTemplate != null && session.Api.Timing.TimeUntilItemUse(hotTemplate) == TimeSpan.Zero;
				NaturalHelpItemChoice? shieldChoice = IsCleric ? NaturalHelpItemPolicy.DecideShield(ObserveHelpItems(), now) : null;
				// NA-20a: the Cleric drinks its owned mana potions (the policy's mana-potion rule); the Priest never did.
				BotInventoryItem? manaPotion = IsCleric ? NaturalIshalgenPotionPolicy.SelectOwnedManaPotion(world.Inventory.Values) : null;
				var manaTemplate = manaPotion == null ? null : runtime.Data.ItemDataDh.GetItemTemplate(manaPotion.ItemId);
				bool manaReady = manaTemplate != null && session.Api.Timing.TimeUntilItemUse(manaTemplate) == TimeSpan.Zero;
				var observation = new NaturalCombatObservation(
					world.Level, world.CurrentHp, world.MaxHp, world.CurrentMp, world.MaxMp, world.IsDead,
					nearbyAttackers > 0 || recentAttacks.Length > 0,
					Distance(session.CurrentPosition, npc.Position), target, world.Skills, cooldowns,
					NearbyAggressors: nearbyAttackers, TargetHpPercent: observedTargetHpPercent,
					HasHealedThisFight: healedThisFight, HasHotPotion: hotPotion != null,
					HotPotionReady: hotReady,
					HotPotionActive: NaturalIshalgenPotionPolicy.HasActiveHealing(world.VisibleEffects),
					Cornered: cornered || ScriptedTrial, TargetAdjacent: targetAdjacent, InEmergency: inEmergency, HasBlessing: hasBlessing,
					TargetSeasoned: targetSeasoned, TargetRanged: targetRanged,
					ConservativeRangedHold: conservativeRangedHold,
					OpenChainCategory: openChain?.Category, OpenChainTargetId: openChain?.Target, ChainExpiresAt: openChain?.ExpiresAt,
					Dp: world.CurrentDp,
					HasRejuvenation: world.VisibleEffects?.Any(effect => rejuvenationIds.Contains(effect.SkillId)),
					ShieldScrollReady: shieldChoice?.Item != null,
					HasManaPotion: manaPotion != null, ManaPotionReady: manaReady,
					LastCancelledSkillId: lastCancelledSkillId);
				NaturalPriestSkill[] catalog = Catalog;
				NaturalCombatChoice choice = NaturalPriestCombatPolicy.Decide(observation, now, catalog, parameters: mauPolicy);
				NaturalCombatCandidate[] candidates = NaturalPriestCombatPolicy.CandidateActions(observation, now, choice,
					catalog, parameters: mauPolicy);
				if (!candidates.Any(candidate => candidate.Action == choice.Action &&
					candidate.SkillId == choice.Skill?.Id && candidate.Legal))
					throw new InvalidDataException($"Baseline chose an action absent from the legal candidate list: {choice.Action}/{choice.Skill?.Id}.");
				trace.Add($"t{turn}:action={choice.Action}/{choice.Skill?.Id} targetDistance={Distance(session.CurrentPosition, npc.Position):F1}");
				lastCombatTrace = trace.TakeLast(12).ToArray();
				session.TraceDiagnostic("combat-decision", new Dictionary<string, object?>
				{
					["encounterId"] = combatAttemptId,
					["turn"] = turn,
					["policyVersion"] = mauPolicy.Id,
					["seed"] = runtime.Seed,
					["observedState"] = new
					{
						observation.Level, observation.Hp, observation.MaxHp, observation.Mp, observation.MaxMp,
						observation.Dead, observation.Aggro, observation.TargetDistance, observation.TargetObjectId,
						learnedSkillIds = observation.Learned.Keys.Order().ToArray(),
						cooldowns = observation.Cooldowns.OrderBy(entry => entry.Key).Select(entry =>
							new { id = entry.Key, readyAt = entry.Value }).ToArray(),
						observation.NearbyAggressors, observation.TargetHpPercent, observation.HasHealedThisFight,
						observation.HasHotPotion, observation.HotPotionReady, observation.HotPotionActive,
						observation.Cornered, observation.TargetAdjacent, observation.InEmergency,
						observation.HasBlessing, observation.TargetSeasoned, observation.TargetRanged,
						observation.ConservativeRangedHold, observation.OpenChainCategory, observation.Dp,
						observation.HasRejuvenation, observation.LastCancelledSkillId,
					},
					["candidateActions"] = candidates,
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
					["shieldScroll"] = shieldChoice?.Item?.ItemId,
				});
				switch (choice.Action)
				{
					case "mana-potion":
					{
						long before = ItemCount(world, manaPotion!.ItemId);
						await session.SendPacketAsync(session.Api.UseItem(manaPotion.ObjectId, manaTemplate!), token);
						await session.SynchronizeAsync(token);
						session.TraceDiagnostic("combat-mana-potion", new Dictionary<string, object?>
						{
							["itemId"] = manaPotion.ItemId, ["before"] = before, ["after"] = ItemCount(world, manaPotion.ItemId),
							["mp"] = world.CurrentMp,
						});
						if (ItemCount(world, manaPotion.ItemId) == before)
							await session.AdvanceAsync(TimeSpan.FromMilliseconds(1000), token); // refused (stunned): decide again
						break;
					}
					case "shield-scroll":
						if (!await UseHelpItemAsync(shieldChoice!.Item!, token))
							await session.AdvanceAsync(TimeSpan.FromMilliseconds(1000), token); // refused (stunned): decide again
						break;
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
						long approachStarted = runtime.NowMillis;
						NaturalNavigationResult approach = await NaturalIshalgenNavigator.ApproachNpcAsync(
							ApproachMapId, npc.TemplateId!.Value, destination, navigator, token);
						if (!approach.Arrived)
							throw new NaturalCombatApproachBlockedException(approach.Reason);
						// Navigation can already be at a walking NPC's announced destination while combat still
						// sees its last reported position. Let motion and cooldowns advance before re-observing.
						if (runtime.NowMillis == approachStarted)
							await session.AdvanceAsync(TimeSpan.FromMilliseconds(350), token);
						break;
					case "wait":
						await session.AdvanceAsync(TimeSpan.FromMilliseconds(500), token);
						break;
				case "retreat":
						// Some quest pulls begin directly at an interacted object and have
						// no named refuge. Previously walked client positions remain valid
						// candidates, but each escape leg is checked against current mobs.
					if (await RetreatFromPackAsync(retreatAnchor ?? session.CurrentPosition,
						incomingAttackers.Append(target).ToHashSet(), token))
					{
						if (!session.Api.World.IsDead && session.Api.World.CurrentHp > 0) completedRetreats++;
						return false;
					}
						cornered = true; // No way out: stay and fight (heal, potions, then the target).
						break;
					default: throw new InvalidDataException($"Natural combat cannot act: {choice.Action}: {choice.Reason}");
				}
			}
			throw new InvalidDataException($"Natural Priest exceeded {MaximumCombatActions} actions without a client-observed NPC kill.");

			string? LocalizedName(int id) => world.Objects.TryGetValue(id, out BotKnownObject? known) && known.TemplateId is int kind
				? runtime.Data.NpcDataDh.GetNpcTemplate(kind)?.GetL10n() : null;
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
		/// from the attackers' homes (Java AttackManager.checkGiveupDistance). Keep running until every
		/// pursuer broadcasts its neutral/return emotion or leaves client sight. A gap in distance alone
		/// does not show that the NPC stopped chasing. New attackers join the same retreat.
		/// </summary>
		private async Task<bool> RetreatFromPackAsync(BotPosition refuge,
			IReadOnlySet<int> observedAttackers, CancellationToken token)
		{
			BotPosition origin = session.CurrentPosition;
			int map = session.Api.World.MapId ?? throw new InvalidDataException("Retreat map unobserved.");
			var activeAttackers = observedAttackers.ToHashSet();
			activeAttackers.RemoveWhere(id => !session.Api.World.Objects.ContainsKey(id));
			int packetStart = session.PacketHistory.Count;
			var tried = new List<BotPosition>();
			for (int replan = 0; replan < 24; replan++)
			{
				activeAttackers.RemoveWhere(id => !session.Api.World.Objects.ContainsKey(id));
				if (activeAttackers.Count == 0) return true;
				BotPosition[] attackerPositions = navigator.Observe().Npcs
					.Where(npc => activeAttackers.Contains(npc.ObjectId))
					.Select(npc => npc.Position).ToArray();
				BotPosition[] homes = activeAttackers
					.Select(id => FirstSeen(id) ?? navigator.Observe().Npcs.FirstOrDefault(npc => npc.ObjectId == id)?.Position)
					.OfType<BotPosition>().ToArray();
				BotNavigationHazard[] otherHazards = navigator.Observe().Npcs
					.Where(npc => !activeAttackers.Contains(npc.ObjectId))
					.Select(npc => (npc, template: runtime.Data.NpcDataDh.GetNpcTemplate(npc.TemplateId)))
					.Where(entry => runtime.IsAggressive(entry.template))
					.SelectMany(entry => entry.npc.Hazards(entry.template!.GetAggroRange(), BotPatrolPath.PassingReach)).ToArray();
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
					// A sideways turn around terrain is fine; reject only a departure that actually
					// takes the bot deeper into the pack before heading for the outward destination.
					string? why = candidate.Count == 0 ? $"no route ({BotNavMeshRouter.LastOutcome})"
						: Distance(session.CurrentPosition, candidate[^1]) < 10 ? "too short"
						: !NaturalCombatRetreatPolicy.ClearsPackOnDeparture(session.CurrentPosition, candidate, attackerPositions)
							? "route enters the pack" : null;
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
						["observedAttackers"] = activeAttackers.ToArray(),
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
					["observedAttackers"] = activeAttackers.ToArray(),
					["attackerHomes"] = homes,
					["fromNearestHome"] = homes.Length == 0 ? null : homes.Min(h => Distance(h, destination.Value)),
				});
				foreach (BotPosition[] segment in route.Chunk(2).Take(256))
				{
					BotNavigationHazard[] others = navigator.Observe().Npcs
						.Where(npc => !activeAttackers.Contains(npc.ObjectId))
						.Select(npc => (npc, template: runtime.Data.NpcDataDh.GetNpcTemplate(npc.TemplateId)))
						.Where(entry => runtime.IsAggressive(entry.template))
						.SelectMany(entry => entry.npc.Hazards(entry.template!.GetAggroRange(), BotPatrolPath.PassingReach)).ToArray();
					if (!BotNavigationGeometry.AvoidsHazards(session.CurrentPosition, segment, others)) break; // new monster ahead
					await navigator.MoveAsync(segment, token);
					await session.SynchronizeAsync(token);
					NaturalCombatRetreatPolicy.ObserveEngagement(activeAttackers,
						session.PacketHistory.Skip(packetStart), session.CharacterId);
					packetStart = session.PacketHistory.Count;
					if (session.Api.World.CurrentHp <= 0 || session.Api.World.IsDead)
					{
						await ReviveAtBindAsync(token);
						return true;
					}
					if (activeAttackers.Count == 0) return true;
				}
			}
			session.TraceDiagnostic("combat-retreat-cornered", new Dictionary<string, object?>
			{
				["position"] = session.CurrentPosition,
				["origin"] = origin,
				["observedAttackers"] = activeAttackers.ToArray(),
				["reason"] = "pursuers still engaged after 24 away-from-pack replans",
			});
			return false;
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
				.Select(npc => (npc, template: runtime.Data.NpcDataDh.GetNpcTemplate(npc.TemplateId)))
				.Where(entry => runtime.IsAggressive(entry.template))
				.SelectMany(entry => entry.npc.Hazards(entry.template!.GetAggroRange())).ToArray();
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
			DateTimeOffset now = runtime.Epoch.AddMilliseconds(runtime.NowMillis);
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

		public bool IsCleric => session.Api.World.Objects.GetValueOrDefault(session.CharacterId)?.PlayerClass ==
			PlayerClass.CLERIC.GetClassId();

		/// <summary>NA-19: the client-observed state the help-item policy reads.</summary>
		private NaturalHelpItemObservation ObserveHelpItems(float travelMeters = 0, bool crossMap = false)
		{
			BotWorldModel world = session.Api.World;
			if (!ReferenceEquals(world.VisibleEffects, effectsSnapshot))
			{
				effectsSnapshot = world.VisibleEffects;
				effectsSeenAtMillis = runtime.NowMillis;
			}
			DateTimeOffset now = runtime.Epoch.AddMilliseconds(runtime.NowMillis);
			var counts = world.Inventory.Values.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
			var delays = new Dictionary<int, DateTimeOffset>();
			foreach (NaturalHelpItem help in NaturalHelpItemPolicy.All.Where(help => counts.GetValueOrDefault(help.ItemId) > 0))
				if (runtime.Data.ItemDataDh.GetItemTemplate(help.ItemId) is { } template)
					delays[help.UseDelayId] = now + session.Api.Timing.TimeUntilItemUse(template);
			IReadOnlySet<BotBlockingActivity> blocking = session.Api.Timing.BlockingActivities;
			return new(world.Level, world.CurrentHp, world.MaxHp, world.IsDead || world.CurrentHp <= 0,
				blocking.Contains(BotBlockingActivity.Casting), blocking.Contains(BotBlockingActivity.Cutscene),
				Flying: false, Disabled: false, world.VisibleEffects, runtime.NowMillis - effectsSeenAtMillis, counts, delays,
				travelMeters, crossMap, world.CurrentDp,
				Catalog.Any(skill => skill.Role == "salvation" && world.Skills.ContainsKey(skill.Id)));
		}

		/// <summary>NA-19: the buff-ourself check. Class buffs as before (pre-pull, after rest); then, for the Cleric,
		/// the help scrolls the policy names, one at a time, each decision traced. With nothing owned it uses nothing.</summary>
		public async Task BuffOurselfAsync(NaturalHelpTrigger trigger, CancellationToken token, float travelMeters = 0, bool crossMap = false)
		{
			if (trigger is NaturalHelpTrigger.PrePull or NaturalHelpTrigger.AfterRest) await MaintainBuffsAsync(token);
			if (!IsCleric || InCombat) return;
			for (int use = 0; use < 3; use++)
			{
				await session.SynchronizeAsync(token);
				DateTimeOffset now = runtime.Epoch.AddMilliseconds(runtime.NowMillis);
				NaturalHelpItemChoice choice = NaturalHelpItemPolicy.DecideBuffs(ObserveHelpItems(travelMeters, crossMap), now, trigger);
				session.TraceDiagnostic("buff-ourself", new Dictionary<string, object?>
				{
					["trigger"] = trigger.ToString(), ["itemId"] = choice.Item?.ItemId, ["reason"] = choice.Reason,
					["checks"] = choice.Checks.Select(check => $"{check.Rule}:{check.Verdict}:{check.Reason}").ToArray(),
				});
				if (choice.Item == null || !await UseHelpItemAsync(choice.Item, token)) return;
			}
		}

		/// <summary>Use one help scroll between casts; false when the server refused it (it stays in the bag).</summary>
		private async Task<bool> UseHelpItemAsync(NaturalHelpItem help, CancellationToken token)
		{
			BotWorldModel world = session.Api.World;
			BotInventoryItem? owned = world.Inventory.Values.Where(item => item.ItemId == help.ItemId && item.Count > 0)
				.OrderBy(item => item.ObjectId).FirstOrDefault();
			var template = runtime.Data.ItemDataDh.GetItemTemplate(help.ItemId);
			if (owned == null || template == null) return false;
			long before = ItemCount(world, help.ItemId);
			int useStart = session.PacketHistory.Count;
			await session.SendPacketAsync(session.Api.UseItem(owned.ObjectId, template), token);
			await session.SynchronizeAsync(token);
			long after = ItemCount(world, help.ItemId);
			session.TraceDiagnostic("help-item-used", new Dictionary<string, object?>
			{
				["itemId"] = help.ItemId, ["family"] = help.Family, ["before"] = before, ["after"] = after,
				["hp"] = world.CurrentHp, ["useDelayId"] = help.UseDelayId,
				["refused"] = session.PacketHistory.Skip(useStart).Where(packet => packet.PacketType == typeof(SM_SYSTEM_MESSAGE))
					.Select(packet => packet.Get<object>("name")?.ToString()).FirstOrDefault(),
			});
			return after == before - 1;
		}

		public async Task RestAsync(CancellationToken token)
		{
			// Heal while there is mana to spend. Sitting solely for missing HP leaves the Priest exposed to
			// respawns and patrols; reserve sitting for MP below half, then recover it to 80%.
			bool locatedForManaRest = false;
			bool recoveringMana = false;
			int quietIntervals = 0;
			for (int interval = 0; interval < MaximumCombatActions; interval++)
			{
				BotWorldModel world = session.Api.World;
				bool interrupted = false;
				if (world.IsDead || world.CurrentHp <= 0)
				{
					await ReviveAtBindAsync(token);
					return;
				}
				if (world.CurrentMp * 100 < world.MaxMp * 50) recoveringMana = true;
				if (recoveringMana && world.CurrentMp * 100 >= world.MaxMp * 80)
				{
					recoveringMana = false;
					locatedForManaRest = false;
				}
				// NA-18 (OD-9): a Cleric rests with powder first. Sitting and Healing Light stay the fallback below.
				if (Catalog.Any(skill => skill.IsPowderRest && world.Skills.ContainsKey(skill.Id)))
				{
					DateTimeOffset restNow = runtime.Epoch.AddMilliseconds(runtime.NowMillis);
					NaturalPowderRestChoice powder = NaturalPowderRestPolicy.Decide(new NaturalPowderRestObservation(
						world.Level, world.CurrentHp, world.MaxHp, world.CurrentMp, world.MaxMp, recoveringMana, world.Skills,
						cooldowns, world.Inventory.Values.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count)),
						LastPowderSkillId: lastPowderSkill), restNow, Catalog);
					session.TraceDiagnostic("powder-rest-decision", new Dictionary<string, object?>
					{
						["action"] = powder.Action, ["skillId"] = powder.Skill?.Id, ["reason"] = powder.Reason,
						["hp"] = world.CurrentHp, ["maxHp"] = world.MaxHp, ["mp"] = world.CurrentMp, ["maxMp"] = world.MaxMp,
						["powder"] = ItemCount(world, NaturalClericSkills.LesserOdellaPowder),
					});
					if (powder.Skill is { IsRestSkill: true } restSkill)
					{
						TimeSpan gate = session.Api.Timing.TimeUntilCast(restSkill.Id);
						if (gate > TimeSpan.Zero) await session.AdvanceAsync(gate + TimeSpan.FromMilliseconds(1), token);
						int castStart = session.PacketHistory.Count;
						if (!await CastAsync(restSkill, session.CharacterId, token)) return;
						lastPowderSkill = restSkill.Id;
						await session.SynchronizeAsync(token);
						int[] hitBy = session.PacketHistory.Skip(castStart)
							.Where(packet => packet.PacketType == typeof(SM_ATTACK) && packet.Get<int>("targetObjId") == session.CharacterId)
							.Select(packet => packet.Get<int>("attackerObjId")).Distinct().ToArray();
						if (hitBy.Length > 0)
						{
							// The hit cancelled the cast: never cast or sit under attack, fight first.
							session.TraceDiagnostic("powder-rest-interrupted", new Dictionary<string, object?>
							{
								["skillId"] = restSkill.Id, ["attackers"] = hitBy, ["hp"] = world.CurrentHp,
							});
							await DefendDuringRestAsync(hitBy, castStart, token);
							locatedForManaRest = false;
						}
						continue;
					}
				}
				if (!recoveringMana)
				{
					if (world.CurrentHp * 100 < world.MaxHp * 90)
					{
						NaturalPriestSkill? heal = NaturalPriestSkills.Best("heal", world.Level, world.Skills, Catalog);
						if (heal == null || world.CurrentMp < heal.ManaCost)
							throw new InvalidDataException("Priest has mana but no client-observed usable self-heal between fights.");
						TimeSpan gate = session.Api.Timing.TimeUntilCast(heal.Id);
						if (gate > TimeSpan.Zero)
						{
							await session.AdvanceAsync(gate + TimeSpan.FromMilliseconds(1), token);
							await session.SynchronizeAsync(token);
							continue; // Reobserve HP and attackers after the cast gate.
						}
						session.TraceDiagnostic("between-fights-heal", new Dictionary<string, object?>
						{
							["skillId"] = heal.Id, ["hp"] = world.CurrentHp, ["maxHp"] = world.MaxHp,
							["mp"] = world.CurrentMp, ["maxMp"] = world.MaxMp,
						});
						if (!await CastAsync(heal, session.CharacterId, token)) return;
						await session.SynchronizeAsync(token);
						continue;
					}
					await BuffOurselfAsync(NaturalHelpTrigger.AfterRest, token);
					if (MaintainInventoryAsync is { } maintain)
						await maintain(token);
					return;
				}
				if (quietIntervals >= 12) break;
				if (!locatedForManaRest)
				{
					await MoveToRestSpotAsync(token);
					locatedForManaRest = true;
				}
				int attackHistoryStart = 0;
				NaturalRestOutcome outcome = await NaturalRestCadence.RunAsync(
					async (resting, waitToken) =>
					{
						await session.SendPacketAsync(session.Api.Rest(resting), waitToken);
						// Pilot's recorded client leaves 343-361 ms between STAND and its first move.
						// Keep the Priest in place until the stand-up animation completes for observers.
						if (!resting) await session.AdvanceAsync(TimeSpan.FromMilliseconds(350), waitToken);
					},
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
						await DefendDuringRestAsync(attackers, attackHistoryStart, defendToken);
					}, token);
				if (outcome == NaturalRestOutcome.Dead)
				{
					await ReviveAtBindAsync(token);
					return;
				}
				if (!interrupted) quietIntervals++;
				else locatedForManaRest = false;
				await session.SynchronizeAsync(token);
			}
			throw new InvalidDataException("Priest could not recover HP/MP before the next pull within bounded healing and mana-rest attempts.");
		}

		/// <summary>Fight the attackers that interrupted a sit or a powder cast (NA-18 shares it with the sit).</summary>
		private async Task DefendDuringRestAsync(IReadOnlyList<int> attackers, int attackHistoryStart, CancellationToken defendToken)
		{
			BotWorldModel world = session.Api.World;
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
				if (remaining.Length == 2 && mauPolicy.PreferWoundedWhenTwoAttackers)
					remaining = remaining.OrderBy(attacker => PriorityForEngagedTarget(attacker,
						remaining.Length, session, preferWounded: true)).ToArray();
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
		}

		private async Task ReviveAtBindAsync(CancellationToken token)
		{
			if (stopOnDeath)
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
			// BC-04 proved the ordinary learned self-revival option. Bregirun's die hook resets the quest to var 1;
			// revive here, then let the leg decision leave/re-enter through its real portals.
			if (session.Api.World.MapId == 320030000 && session.Api.World.ReviveOptions?.BySkill == true)
			{
				session.BeginStep($"bc-self-revive-{revives}", "accept-observed-learned-rebirth");
				await session.SendPacketAsync(session.Api.Revive(BotReviveType.Rebirth), token);
				await session.SynchronizeAsync(token);
				Require.True(!session.Api.World.IsDead, "Rebirth did not clear client-observed death.");
				session.TraceDiagnostic("quest-instance-self-revival", new Dictionary<string, object?> { ["revives"] = revives, ["position"] = session.CurrentPosition });
				await BuffOurselfAsync(NaturalHelpTrigger.AfterRevive, token);
				await RestAsync(token);
				return;
			}
			session.BeginStep($"ni07-bind-revive-{revives}", "accept-client-death-and-revive-at-bound-obelisk");
			if (!session.Api.World.IsDead)
			{
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(500), token);
				await session.WaitForPacketAsync(typeof(SM_DIE), token);
			}
			// Java TeleportService.sendLoc despawns the player before rebuilding its known list, even on the
			// same map. BC-06 saw a pre-death hunter survive in the bot view after a fortress bind revive;
			// its server object was gone when the bot returned. Drop that view before the new spawn packets.
			int? bindMap = session.Api.World.ObeliskBindPoint?.MapId;
			bool otherMap = bindMap is int bound && bound != session.Api.World.MapId;
			// RC-11: Leg 8 retained a pre-revive assassin that the server no longer knew.
			// Every bind revive rebuilds the known list, including on the same map.
			session.Api.World.BeginWorldReload();
			await session.SendPacketAsync(session.Api.Revive(BotReviveType.Bind), token);
			if (otherMap)
			{
				await session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token, packet => packet.Get<int>("worldId") == bindMap);
				await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
			}
			else
				await session.WaitForPacketAsync(typeof(SM_CHANNEL_INFO), token);
			await session.SynchronizeAsync(token);
			if (session.Api.World.IsDead) throw new InvalidDataException("Bind revive did not clear client-observed death.");
			session.AcceptTeleportPosition();
			AfterBindRevive?.Invoke();
			await BuffOurselfAsync(NaturalHelpTrigger.AfterRevive, token);
			// A bind revive leaves a quarter of HP and MP (and soul sickness lowers the maximum). Rest at the obelisk before
			// anything else, as a player does: in Leg 4 the walk back out at 25% HP met three swamp mosbears and died three
			// more times in five minutes, because RestAsync returned straight after the revive that it ran.
			session.TraceDiagnostic("rest-after-revive", new Dictionary<string, object?>
			{
				["hp"] = session.Api.World.CurrentHp, ["maxHp"] = session.Api.World.MaxHp,
				["mp"] = session.Api.World.CurrentMp, ["maxMp"] = session.Api.World.MaxMp, ["revives"] = revives,
			});
			await RestAsync(token);
		}

		/// <summary>Walk up to 8 m toward the target's last-known position (never nearer than 10 m), on a
		/// checked route that stays out of other observed circles.</summary>
		/// <param name="reach">How close to get: melee reach for a melee skill, spell range otherwise.</param>
		private async Task CloseInAfterRangeRejectionAsync(int target, CancellationToken token, float reach = 10f)
		{
			if (!session.Api.World.Objects.TryGetValue(target, out BotKnownObject? observed) ||
				session.Api.World.MapId is not int map) return;
			BotPosition start = session.CurrentPosition;
			// A walker is where its last SM_MOVE was heading, not where that move began.
			BotPosition settled = observed.SettledPosition;
			float distance = Distance(start, settled);
			if (distance <= reach) return;
			float t = MathF.Min(12, distance - reach) / distance;
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
			// AM-06: Java Skill.useSkill resets the player's chain when a skill without a chain category is cast, so a
			// Light of Rejuvenation between Smite and Flashbolt breaks the chain and the server silently refuses Flashbolt.
			if (skill.ChainCategory == null) openChain = null;
			await session.SendPacketAsync(session.Api.Target(target), token);
			await session.SendPacketAsync(session.Api.Cast(runtime.CreateSpellCast(session.Api.World,
				session.CurrentPosition, skill.Id, checked((byte)learned.Level), target)), token);
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
				// The cast never started: release the local casting gate, or the next walk is refused ("CM_MOVE is blocked
				// while Casting") even when the caller recovers from this.
				session.Api.Timing.RecordCastCancelled();
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
					await CloseInAfterRangeRejectionAsync(target, token,
						skill.Range <= NaturalPriestCombatPolicy.MeleeReach ? NaturalPriestCombatPolicy.MeleeReach - 1 : 10f);
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(300), token);
					await session.SynchronizeAsync(token);
					return true; // Re-evaluate range, health and attackers before retrying.
				}
				if (started.Get<object>("name") is "STR_SKILL_OBSTACLE" &&
					session.Api.World.Objects.TryGetValue(target, out BotKnownObject? obstructedTarget) &&
					++obstacleRepositions <= 2)
				{
					// The server sees something between us that the bot's geometry does not (a hut wall, a fence,
					// the nest rim). A priest fights at melee anyway: close in to the target along checked ground
					// and cast from there rather than hunting for another spot with the same blind geometry.
					session.TraceDiagnostic("combat-obstacle-close-in", new Dictionary<string, object?>
					{
						["targetObjectId"] = target,
						["skillId"] = skill.Id,
						["reposition"] = obstacleRepositions,
						["clientTargetDistance"] = Distance(session.CurrentPosition, obstructedTarget.SettledPosition),
					});
					await CloseInAfterRangeRejectionAsync(target, token, NaturalPriestCombatPolicy.MeleeReach - 1);
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(300), token);
					await session.SynchronizeAsync(token);
					return true; // Re-evaluate the visible target and healing state before recasting.
				}
				if (started.Get<object>("name") is "STR_SKILL_OBSTACLE") return false;
				if (started.Get<object>("name") is "STR_SKILL_CAN_NOT_ATTACK_WHILE_IN_ABNORMAL_STATE" or
					"STR_SKILL_CANT_CAST_MAGIC_SKILL_WHILE_SILENCED")
				{
					// Stun/knockdown or silence is an ordinary refusal. Let the clock/effects advance,
					// then re-evaluate health, consumables and the target rather than waiting for a cast.
					session.TraceDiagnostic("combat-cast-while-disabled", new Dictionary<string, object?>
					{
						["skillId"] = skill.Id,
						["reason"] = started.Get<object>("name"),
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
			// AG-07: Java ChainCondition.shouldReset clears the chain when an opener starts while its own chain is used up (Smite's
			// selfcount is 1), and only a completed cast opens it again (Skill.endCast). A Smite cut short (STR_SKILL_CANCELED)
			// leaves no chain, and the server refuses the Flashbolt after it without a word (the Leg 6 smoke run).
			if (skill.ChainCategory != null && skill.RequiresChainCategory == null) openChain = null;
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(started.Get<ushort>("castDuration") + 1), token);
			DecodedBotServerPacket result = await BotCastProtocol.WaitForCompletionAsync(
				session.WaitForPacketAsync, session.CharacterId, skill.Id, token);
			lastCancelledSkillId = result.PacketType == typeof(SM_SKILL_CANCEL) ? skill.Id : null;
			if (result.PacketType == typeof(SM_CASTSPELL_RESULT))
			{
				int deciseconds = result.Get<int>("cooldown");
				if (deciseconds > 0)
					cooldowns[skill.CooldownId] = runtime.Epoch.AddMilliseconds(runtime.NowMillis + deciseconds * 100L);
				// NA-18: flag 32 is a successful chain step (Java SM_CASTSPELL_RESULT); anything else resets it.
				// Smite's chain has no time limit; a stepped chain lasts its template window.
				if (skill.ChainCategory != null)
					openChain = (result.Get<byte>("flags") & 32) != 0
						? (skill.ChainCategory, target, skill.RequiresChainCategory == null || skill.ChainWindowMillis == 0
							? DateTimeOffset.MaxValue : runtime.Epoch.AddMilliseconds(runtime.NowMillis + skill.ChainWindowMillis))
						: null;
			}
			await session.AdvanceAsync(BotCastProtocol.RecoveryDelay(result), token);
			return true;
		}
	}

	// What the general quest-loot sweep took from each corpse, so a later TryLootCorpseItemAsync on that corpse counts it.
	private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<BotWorldModel, Dictionary<int, List<int>>> SweptQuestItems = new();

	/// <summary>Take QUEST-group drops and explicitly required non-quest keys. Returns the item ids taken.</summary>
	internal static async Task<IReadOnlyList<int>> LootQuestItemsAsync(INaturalJourneySession session, int objectId,
		Aion.GameServer.Dataholders.StaticData data, CancellationToken token, Func<int, bool>? itemNeeded = null,
		IReadOnlySet<int>? additionalItemIds = null)
	{
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
			catch (OperationCanceledException) when (!token.IsCancellationRequested) { return []; }
		}
		var taken = new List<int>();
		foreach (IReadOnlyDictionary<string, object?> entry in list.Get<List<IReadOnlyDictionary<string, object?>>>("items"))
		{
			int itemId = Get<int>(entry, "itemId");
			var template = data.ItemDataDh.GetItemTemplate(itemId);
			if (template == null || template.itemGroup != Aion.GameServer.Model.Templates.Items.Enums.ItemGroup.QUEST && additionalItemIds?.Contains(itemId) != true) continue;
			if (itemNeeded?.Invoke(itemId) == false) continue;
			BotInventoryItem? existing = session.Api.World.Inventory.Values.FirstOrDefault(owned => owned.ItemId == itemId);
			// Java DropService rejects a second limit-one item; there will be no inventory update to wait for.
			if (existing != null && template.HasLimitOne()) continue;
			await session.SendPacketAsync(session.Api.Loot(objectId, Get<byte>(entry, "index")), token);
			if (existing == null)
				await session.WaitForPacketAsync(typeof(SM_INVENTORY_ADD_ITEM), token,
					packet => packet.Get<List<IReadOnlyDictionary<string, object?>>>("items").Any(item => Get<int>(item, "itemId") == itemId));
			else
				await session.WaitForPacketAsync(typeof(SM_INVENTORY_UPDATE_ITEM), token, packet => packet.Get<int>("objectId") == existing.ObjectId);
			taken.Add(itemId);
		}
		await session.SendPacketAsync(session.Api.Loot(objectId, close: true), token);
		if (taken.Count > 0)
		{
			Dictionary<int, List<int>> swept = SweptQuestItems.GetOrCreateValue(session.Api.World);
			if (!swept.TryGetValue(objectId, out List<int>? items)) swept[objectId] = items = [];
			items.AddRange(taken);
		}
		return taken;
	}

	internal static async Task<bool> TryLootCorpseItemAsync(INaturalJourneySession session,
		int objectId, int itemId, CancellationToken token, int preopenedPacketStart = -1)
	{
		// The general sweep may have taken it already, right after the kill.
		if (SweptQuestItems.TryGetValue(session.Api.World, out Dictionary<int, List<int>>? sweptItems) &&
			sweptItems.TryGetValue(objectId, out List<int>? sweptFromCorpse) && sweptFromCorpse.Contains(itemId))
		{
			session.TraceDiagnostic("quest-drop-attempt", new Dictionary<string, object?>
			{
				["objectId"] = objectId, ["itemId"] = itemId, ["dropped"] = true, ["sweptAfterKill"] = true,
				["inventoryCount"] = ItemCount(session.Api.World, itemId),
			});
			return true;
		}
		long beforeCount = ItemCount(session.Api.World, itemId);
		// Quest-use objects open their drop list on completion. Reopening that list sends a second
		// START_LOOT/END_LOOT cycle within the same client frame and can leave observers in a loot pose.
		DecodedBotServerPacket? list = preopenedPacketStart >= 0
			? session.PacketHistory.Skip(preopenedPacketStart).LastOrDefault(packet =>
				packet.PacketType == typeof(SM_LOOT_ITEMLIST) && packet.Get<int>("targetObjectId") == objectId)
			: null;
		if (list == null)
		{
			await session.SendPacketAsync(session.Api.Loot(objectId), token);
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
		if (preopenedPacketStart >= 0) await session.AdvanceAsync(TimeSpan.FromMilliseconds(450), token);
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
		Require.True(afterCount > beforeCount,
			$"Corpse {objectId} listed quest item {itemId}, but inventory stayed at {beforeCount}.");
		return true;
	}

	private static float Distance(BotPosition a, BotPosition b) => MathF.Sqrt(
		MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2) + MathF.Pow(a.Z - b.Z, 2));
	private static async Task FinishStandardQuestAsync(
		INaturalJourneySession session,
		int npcObjectId,
		int questId,
		CancellationToken token,
		int rewardAction = DialogAction.SELECTED_QUEST_NOREWARD)
	{
		await NaturalDialogProtocol.OpenAsync(session, npcObjectId, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npcObjectId, DialogAction.QUEST_SELECT, questId: questId), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		if (session.Api.World.Quests[questId].Status == 3)
		{
			await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npcObjectId, DialogAction.SELECT_QUEST_REWARD, questId: questId), token);
			await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		}
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npcObjectId, checked((ushort)rewardAction), questId: questId), token);
		await WaitForQuestStatusAsync(session, questId, 5, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
			packet => packet.Get<int>("targetObjectId") == npcObjectId);
		await session.SendPacketAsync(session.Api.CloseDialog(npcObjectId), token);
	}

	private static async Task WaitForQuestStatusAsync(
		INaturalJourneySession session, int questId, byte status, CancellationToken token)
	{
		if (session.Api.World.Quests.TryGetValue(questId, out BotQuestState? quest) && quest.Status == status)
			return;
		await session.WaitForPacketAsync(typeof(SM_QUEST_ACTION), token,
			packet => packet.Get<int>("questId") == questId &&
				packet.Fields.TryGetValue("status", out object? value) && value is byte actual && actual == status);
	}

	private static long ItemCount(BotWorldModel world, int itemId) =>
		world.Inventory.Values.Where(item => item.ItemId == itemId).Sum(item => item.Count);

	private static int ChooseEngagedTarget(IReadOnlyList<int> attackers, INaturalJourneySession session,
		bool preferWounded) => attackers
		.OrderBy(id => PriorityForEngagedTarget(id, attackers.Count, session, preferWounded))
		.ThenBy(id => session.Api.World.Objects.TryGetValue(id, out BotKnownObject? npc)
			? Distance(session.CurrentPosition, npc.Position) : float.MaxValue)
		.First();

	private static int PriorityForEngagedTarget(int objectId, int attackerCount,
		INaturalJourneySession session, bool preferWounded)
	{
		if (!preferWounded || attackerCount != 2) return 0;
		for (int index = session.PacketHistory.Count - 1; index >= 0; index--)
		{
			DecodedBotServerPacket packet = session.PacketHistory[index];
			if (packet.PacketType == typeof(SmAttackStatus) && packet.Get<int>("objectId") == objectId)
				return packet.Get<byte>("hpOrMp");
		}
		return 100; // Unknown target HP is not treated as wounded.
	}

	private static T Get<T>(IReadOnlyDictionary<string, object?> fields, string name) =>
		fields.TryGetValue(name, out object? value) && value is T typed
			? typed
			: throw new InvalidDataException($"Decoded field '{name}' was missing or was not {typeof(T).Name}.");
	private static async Task FinishItemQuestAsync(
		INaturalJourneySession session, int npcObjectId, int questId, CancellationToken token,
		int rewardAction = DialogAction.SELECTED_QUEST_NOREWARD)
	{
		await NaturalDialogProtocol.OpenAsync(session, npcObjectId, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npcObjectId, DialogAction.QUEST_SELECT, questId: questId), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(
			npcObjectId, DialogAction.CHECK_USER_HAS_QUEST_ITEM, questId: questId), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		Require.Equal((byte)4, session.Api.World.Quests[questId].Status);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(
			npcObjectId, checked((ushort)rewardAction), questId: questId), token);
		await WaitForQuestStatusAsync(session, questId, 5, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
			packet => packet.Get<int>("targetObjectId") == npcObjectId);
		await session.SendPacketAsync(session.Api.CloseDialog(npcObjectId), token);
	}


}
