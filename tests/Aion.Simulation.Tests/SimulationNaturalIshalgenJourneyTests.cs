using Aion.Bots.Navigation;
using Aion.Bots.Movement;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>
	/// First executable NI-07 checkpoint. This deliberately uses the same fresh Priest,
	/// client packets, checked walking, and completed-journal oracle that the eventual
	/// forty-one-quest loop must retain; it does not replace that acceptance run.
	/// </summary>
	[SkippableFact]
	public async Task NaturalIshalgenPriestCompletesOpeningQuestsWithoutSetup()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		using var policy = NewPolicy("NI07", includeHistory: true);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(
			fixture, policy, "b01", accountId: 41, "Asimnjour", Race.ASMODIANS);
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
		NaturalDecision decision = NaturalIshalgenDecisionEngine.Decide(contract,
			ObserveNaturalJourney(session), 1);
		Assert.Equal("find-quest-starter", decision.SelectedAction);
		Assert.Equal(2101, decision.SelectedQuestId);
		Assert.NotEqual("complete", decision.Outcome);

		BotNavigationGraph graph = BotNavigationGraphFactory.Build(fixture.DataManager.StaticData,
			[203500, 203504, 203501, 203502, 203516, 203518,
			203519, 203534, 790002, 210377, 210378, 700045, 203538,
			203539, 210592, 700047, 203550, 210402, 210403, 203530, 203535, 203551,
			210363, 210367, 210369, 700124, 700093],
			BotNavigationGeometry.ForServerWorld(player.GetInstanceId(), player.GetRace()));
		var navigator = new NaturalSimulationNavigator(session, graph,
			BotNavigationGeometry.ForServerWorld(player.GetInstanceId(), player.GetRace()));
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
		var combat = new NaturalSimulationCombat(session, navigator, fixture);
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
			int objectId = await ApproachShippedSpawnAsync(700124);
			await LootActionObjectAsync(session, objectId, 182203104, token);
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
		session.BeginStep("ni07-q2105-start", "accept-sparkie-collection-at-vanar");
		await session.StartQuestAsync(vanar, 2105, token);
		for (int kill = 0; kill < 3; kill++)
		{
			session.BeginStep($"ni07-q2105-kill-{kill + 1}", "fight-and-loot-sparkie");
			int target = await ApproachShippedSpawnAsync(210367);
			await combat.KillAsync(target, token);
			navigator.UnavailableObjects.Add(target);
			await LootCorpseItemAsync(session, target, 182203105, token);
			await combat.RestAsync(token);
		}
		Assert.Equal(3, ItemCount(session.Api.World, 182203105));
		session.BeginStep("ni07-q2105-finish", "return-to-vanar-and-turn-in");
		vanar = await ApproachAsync(203502, new BotPosition(220.15f, 2678.81f, 295.25f, 0));
		await FinishItemQuestAsync(session, vanar, 2105, token);
		Assert.Equal(5, session.Api.World.Quests[2105].Status);
		Assert.Equal(0, ItemCount(session.Api.World, 182203105));
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

		for (int grind = 0; session.Api.World.Level < 3 && grind < 20; grind++)
		{
			session.BeginStep($"ni07-level-3-kill-{grind + 1}", "ordinary-priest-level-gate-combat");
			int target = await ApproachShippedSpawnAsync(210363);
			await combat.KillAsync(target, token);
			navigator.UnavailableObjects.Add(target);
			await combat.RestAsync(token);
		}
		Assert.True(session.Api.World.Level >= 3, "Ordinary combat did not earn the level-3 campaign gate.");
		if (!session.Api.World.CompletedQuestIds.Contains(2100)) await FinishCaptainOrderAsync();
		Assert.Equal(5, session.Api.World.Quests[2100].Status);
		Assert.Equal(41, contract.Quests.Length);
		Assert.InRange(session.Api.World.Level, (ushort)1, (ushort)9);
		await session.QuitAsync(token);
		policy.AssertClean();

		async Task<int> ApproachAsync(int templateId, BotPosition anchor)
		{
			NaturalNavigationResult result = await NaturalIshalgenNavigator.ApproachNpcAsync(
				contract.MapId, templateId, anchor, navigator, token);
			Assert.True(result.Arrived, result.Reason);
			return Assert.IsType<int>(result.TargetObjectId);
		}

		async Task<int> ApproachShippedSpawnAsync(int templateId)
		{
			BotWaypoint[] anchors = graph.GetMap(contract.MapId)!.Waypoints
				.Where(waypoint => waypoint.TemplateId == templateId)
				.OrderBy(waypoint => Distance(session.CurrentPosition, waypoint.Position)).ToArray();
			if (anchors.Length == 0) throw new InvalidDataException($"Shipped spawn graph has no NPC {templateId}.");
			var reasons = new List<string>();
			foreach (BotWaypoint anchor in anchors.Take(12))
			{
				NaturalNavigationResult result = await NaturalIshalgenNavigator.ApproachNpcAsync(
					contract.MapId, templateId, anchor.Position, navigator, token);
				if (result.Arrived && result.TargetObjectId is int objectId) return objectId;
				reasons.Add(result.Reason);
			}
			throw new InvalidDataException($"No client-observed NPC {templateId} at twelve shipped spawn hints " +
				$"from {session.CurrentPosition}: {string.Join(" | ", reasons)}");
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
				int objectId = await ApproachShippedSpawnAsync(700093);
				await LootActionObjectAsync(session, objectId, 182203002, token);
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
			for (int kill = 0; kill < 7; kill++)
			{
				session.BeginStep($"ni07-q2002-kill-{kill + 1}", "fight-sprigg-in-verdandis-task");
				int target = await ApproachShippedSpawnAsync(210377);
				await combat.KillAsync(target, token);
				navigator.UnavailableObjects.Add(target);
				await combat.RestAsync(token);
			}
			Assert.Equal(10, session.Api.World.Quests[2002].StepAndFlags);
			session.BeginStep("ni07-q2002-verdandi-report", "report-sprigg-kills-to-verdandi");
			verdandi = await ApproachShippedSpawnAsync(790002);
			await OpenQuestDialogAsync(verdandi, 2002);
			await session.SendPacketAsync(session.Api.SelectDialog(verdandi, DialogAction.SETPRO3, questId: 2002), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(11, session.Api.World.Quests[2002].StepAndFlags);
			session.BeginStep("ni07-q2002-mushroom", "collect-sticky-mushroom-for-verdandi");
			int mushroom = await ApproachShippedSpawnAsync(700045);
			await LootActionObjectAsync(session, mushroom, 182203003, token);
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
			await ApproachShippedSpawnAsync(203519); // Retrace the Nobekk-Dabi route proved during Q2002.
			await ApproachShippedSpawnAsync(203534);
			await ApproachShippedSpawnAsync(790002);
			await ApproachShippedSpawnAsync(203535); // Eastern road avoids the collision-blocked direct valley line.
			int keeper = await ApproachShippedSpawnAsync(203539);
			await OpenQuestDialogAsync(keeper, 2003);
			await session.SendPacketAsync(session.Api.SelectDialog(keeper, DialogAction.SELECT1_1, questId: 2003), token);
			await session.SynchronizeAsync(token);
			Assert.Contains(session.PacketHistory, packet => packet.PacketType == typeof(SM_PLAY_MOVIE) &&
				packet.Get<int>("cutsceneId") == 53);
			await session.SendPacketAsync(session.Api.SelectDialog(keeper, DialogAction.SETPRO1, questId: 2003), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(1, session.Api.World.Quests[2003].StepAndFlags);
			for (int attempt = 0; attempt < 9 && ItemCount(session.Api.World, 182203004) < 3; attempt++)
			{
				session.BeginStep($"ni07-q2003-kill-{attempt + 1}", "fight-and-loot-treasure-guardian");
				await combat.RestAsync(token);
				int target = await ApproachShippedSpawnAsync(210592);
				bool killed = await combat.TryKillAsync(target, token);
				navigator.UnavailableObjects.Add(target);
				if (killed) await LootCorpseItemAsync(session, target, 182203004, token);
				await combat.RestAsync(token);
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
			for (int attempt = 1; attempt <= 8 && !foundCube; attempt++)
			{
				session.BeginStep($"ni07-q2004-tombstone-{attempt}", "wake-and-fight-tombstone-guardian");
				await combat.RestAsync(token);
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
				await combat.KillAsync(target, token);
				navigator.UnavailableObjects.Add(target);
				foundCube = await TryLootCorpseItemAsync(session, target, 182203005, token);
				await combat.RestAsync(token);
			}
			Assert.True(foundCube, "Eight ordinary tombstone guardians did not drop the quest cube.");
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
			for (int kill = 0; kill < 3; kill++)
			{
				session.BeginStep($"ni07-q2004-kill-{kill + 1}", "fight-munins-cube-target");
				await combat.RestAsync(token);
				int target = await ApproachShippedSpawnAsync(210402);
				await combat.KillAsync(target, token);
				navigator.UnavailableObjects.Add(target);
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
		public List<NaturalNavigationEvent> Events { get; } = [];
		public HashSet<int> UnavailableObjects { get; } = [];

		public NaturalNavigationObservation Observe()
		{
			BotWorldModel world = session.Api.World;
			return new(world.MapId, session.CurrentPosition, world.IsDead,
				world.Objects.Values.Where(item => item.Kind == BotKnownObjectKind.Npc && item.TemplateId != null &&
					!UnavailableObjects.Contains(item.ObjectId))
					.Select(item => new NaturalNavigationObject(item.ObjectId, item.TemplateId!.Value, item.Position)).ToArray());
		}

		public Task<IReadOnlyList<BotPosition>> FindRouteAsync(BotPosition start, BotPosition destination,
			CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			int map = session.Api.World.MapId ?? throw new InvalidDataException("SIM journey map unobserved.");
			IReadOnlyList<BotPosition> route = graph.FindPath(map, start, destination);
			if (route.Count == 0) route = geometry.FindLocalPath(map, start, destination);
			if (route.Count == 0) route = geometry.FindJourneyPath(map, start, destination);
			if (route.Count == 0)
			{
				float remaining = Distance(start, destination);
				BotPosition[] candidates = graph.GetMap(map)?.Waypoints
					.Select(waypoint => waypoint.Position)
					.Where(point => Distance(start, point) is > 15 and <= 120 &&
						Distance(point, destination) < remaining - 15)
					.OrderBy(point => Distance(point, destination)).ThenBy(point => Distance(start, point))
					.Take(24).ToArray() ?? [];
				foreach (BotPosition candidate in candidates)
				{
					if (geometry.TraceEdge(map, start, candidate) is { } edge) return Task.FromResult((IReadOnlyList<BotPosition>)edge);
				}
				foreach (BotPosition candidate in candidates.Take(8))
				{
					route = geometry.FindLocalPath(map, start, candidate);
					if (route.Count != 0) break;
				}
			}
			if (route.Count == 0 && Observe().Npcs.Any(npc => Distance(npc.Position, destination) < 0.1f))
				route = geometry.FindInteractionPath(map, start, destination);
			return Task.FromResult(route);
		}

		public Task MoveAsync(IReadOnlyList<BotPosition> segment, CancellationToken token) =>
			session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing)
				.CreateGroundPlan(segment, session.CurrentPosition,
					session.Api.World.MovementSpeed ?? throw new InvalidDataException("SIM movement speed unobserved.")), token);

		public Task SynchronizeAsync(CancellationToken token) => session.SynchronizeAsync(token);

		public void Record(NaturalNavigationEvent navigationEvent) => Events.Add(navigationEvent);
	}

	private sealed class NaturalSimulationCombat(SimulationL0Session session,
		NaturalSimulationNavigator navigator, SimulationWorldFixture fixture)
	{
		private readonly Dictionary<int, DateTimeOffset> cooldowns = [];

		public async Task KillAsync(int target, CancellationToken token)
		{
			if (!await TryKillAsync(target, token))
				throw new InvalidDataException($"Engaged NPC {target} disappeared without client-observed kill evidence.");
		}

		public async Task<bool> TryKillAsync(int target, CancellationToken token)
		{
			BotWorldModel world = session.Api.World;
			long startingExperience = world.CurrentExperience;
			for (int turn = 0; turn < 24; turn++)
			{
				await session.SynchronizeAsync(token);
				if (world.CurrentExperience > startingExperience || world.LootStatuses.ContainsKey(target)) return true;
				if (world.IsDead) throw new InvalidDataException(
					$"Natural Priest died fighting {target} at level {world.Level}; HP={world.CurrentHp}/{world.MaxHp}, " +
					$"MP={world.CurrentMp}/{world.MaxMp}, position={session.CurrentPosition}.");
				if (!world.Objects.TryGetValue(target, out BotKnownObject? npc))
					return false; // Reacquire a new client-observed mob; do not count this as a kill.
				DateTimeOffset now = fixture.Epoch.AddMilliseconds(fixture.Clock.NowMillis);
				NaturalCombatChoice choice = NaturalPriestCombatPolicy.Decide(new NaturalCombatObservation(
					world.Level, world.CurrentHp, world.MaxHp, world.CurrentMp, world.MaxMp, world.IsDead,
					false, Distance(session.CurrentPosition, npc.Position), target, world.Skills, cooldowns), now);
				switch (choice.Action)
				{
					case "cast-self": case "cast-target":
						await CastAsync(choice.Skill!, choice.Action == "cast-self" ? session.CharacterId : target, token);
						break;
					case "attack":
						await session.SendPacketAsync(session.Api.Target(target), token);
						await session.SendPacketAsync(session.Api.Attack(target, 2500, checked((byte)turn)), token);
						await session.AdvanceAsync(TimeSpan.FromMilliseconds(2600), token);
						break;
					case "approach":
						NaturalNavigationResult approach = await NaturalIshalgenNavigator.ApproachNpcAsync(
							220010000, npc.TemplateId!.Value, npc.Position, navigator, token);
						if (!approach.Arrived) throw new InvalidDataException(approach.Reason);
						break;
					default: throw new InvalidDataException($"Natural combat cannot act: {choice.Action}: {choice.Reason}");
				}
			}
			throw new InvalidDataException("Natural Priest exceeded 24 actions without a client-observed NPC kill.");
		}

		public async Task RestAsync(CancellationToken token)
		{
			for (int interval = 0; interval < 12; interval++)
			{
				BotWorldModel world = session.Api.World;
				if (world.IsDead) throw new InvalidDataException("Cannot rest while dead.");
				if (world.CurrentHp * 100 >= world.MaxHp * 90 &&
					world.CurrentMp * 100 >= world.MaxMp * 80) return;
				await session.SendPacketAsync(session.Api.Rest(true), token);
				await session.AdvanceAsync(TimeSpan.FromSeconds(10), token);
				await session.SendPacketAsync(session.Api.Rest(false), token);
				await session.SynchronizeAsync(token);
			}
			throw new InvalidDataException("Priest could not recover HP/MP before the next pull within two minutes.");
		}

		private async Task CastAsync(NaturalPriestSkill skill, int target, CancellationToken token)
		{
			BotSkill learned = session.Api.World.Skills[skill.Id];
			await session.SendPacketAsync(session.Api.Target(target), token);
			await session.SendPacketAsync(session.Api.Cast(new SpellCastData(skill.Id,
				checked((byte)learned.Level), 0) { TargetObjectId = target }), token);
			DecodedBotServerPacket started = await BotCastProtocol.WaitForStartAsync(
				session.WaitForPacketAsync, session.CharacterId, skill.Id, token);
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
		}
	}

	private static async Task<bool> TryLootCorpseItemAsync(SimulationL0Session session,
		int objectId, int itemId, CancellationToken token)
	{
		await session.SendPacketAsync(session.Api.Loot(objectId), token);
		DecodedBotServerPacket list = await session.WaitForPacketAsync(typeof(SM_LOOT_ITEMLIST), token,
			packet => packet.Get<int>("targetObjectId") == objectId);
		IReadOnlyDictionary<string, object?>? item = list
			.Get<List<IReadOnlyDictionary<string, object?>>>("items")
			.FirstOrDefault(entry => Get<int>(entry, "itemId") == itemId);
		if (item == null)
		{
			await session.SendPacketAsync(session.Api.Loot(objectId, close: true), token);
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
		return true;
	}

	private static float Distance(BotPosition a, BotPosition b) => MathF.Sqrt(
		MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2) + MathF.Pow(a.Z - b.Z, 2));
}
