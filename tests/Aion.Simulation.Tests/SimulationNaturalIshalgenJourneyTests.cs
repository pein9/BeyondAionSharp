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
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
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
			foreach (BotWaypoint anchor in anchors.Take(12))
			{
				NaturalNavigationResult result = await NaturalIshalgenNavigator.ApproachNpcAsync(
					contract.MapId, templateId, anchor.Position, navigator, token);
				if (result.Arrived && result.TargetObjectId is int objectId) return objectId;
			}
			throw new InvalidDataException($"No client-observed NPC {templateId} at twelve shipped spawn hints.");
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
			BotWorldModel world = session.Api.World;
			long startingExperience = world.CurrentExperience;
			for (int turn = 0; turn < 24; turn++)
			{
				await session.SynchronizeAsync(token);
				if (world.CurrentExperience > startingExperience || world.LootStatuses.ContainsKey(target)) return;
				if (world.IsDead) throw new InvalidDataException("Natural Priest died before a bounded recovery could be attempted.");
				if (!world.Objects.TryGetValue(target, out BotKnownObject? npc))
					throw new InvalidDataException("Engaged Sprigg disappeared without client-observed kill evidence.");
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
			throw new InvalidDataException("Natural Priest exceeded 24 actions without a client-observed Sprigg kill.");
		}

		public async Task RestAsync(CancellationToken token)
		{
			await session.SendPacketAsync(session.Api.Rest(true), token);
			await session.AdvanceAsync(TimeSpan.FromSeconds(10), token);
			await session.SendPacketAsync(session.Api.Rest(false), token);
			await session.SynchronizeAsync(token);
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

	private static float Distance(BotPosition a, BotPosition b) => MathF.Sqrt(
		MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2) + MathF.Pow(a.Z - b.Z, 2));
}
