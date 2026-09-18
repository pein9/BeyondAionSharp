using System.Globalization;
using Aion.Bots.Gm;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private static async Task<int> RunQuestPlanZoneAsync(
		LiveBotOptions options,
		LiveBotProblemWriter problems,
		string zone,
		Race race,
		string characterName,
		CancellationToken cancellationToken)
	{
		string scenarioId = zone == "Poeta" ? "Q4P" : "Q4I";
		await using var subject = new L0Actor(options, problems, 1, race, characterName: characterName);
		await using var director = new L0Actor(options, problems, 99, Race.ELYOS,
			bot: "gm", account: LiveGmFacade.DirectorAccount, characterName: "Director");
		subject.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = scenarioId });
		director.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = scenarioId + "-director" });
		try
		{
			foreach (L0Actor actor in new[] { subject, director })
			{
				await actor.StepAsync("login-game-auth", actor.Session.LoginAndAuthenticateAsync, cancellationToken);
				await actor.StepAsync("create-character", actor.Session.CreateCharacterAsync, cancellationToken);
				await actor.StepAsync("enter-world", actor.Session.EnterWorldAsync, cancellationToken);
			}

			LiveGmFacade gm = director.Session.CreateLiveGmFacade();
			var gmSubject = new GmSubject(subject.Session.CharacterId, subject.Session.CharacterName);
			await director.StepAsync("move-director-to-subject", async token =>
			{
				await gm.ExecuteAsync(new GmCommand("moveto", [subject.Session.CharacterName], "Teleported to"),
					cancellationToken: token);
				await director.Session.CompleteTeleportAsync(race == Race.ELYOS ? 210010000 : 220010000, token);
			}, cancellationToken);
			await director.StepAsync("make-subject-level-fifty", async token =>
			{
				await gm.ExecuteAsync(new GmCommand("set", ["level", "9"], "level to 9"), gmSubject, token);
				await gm.ExecuteVerifiedAsync(
					new GmCommand("set", ["class", "gladiator"], "replyless class change"),
					new GmCommand("set", ["level", "50"], "level to 50"), gmSubject, token);
			}, cancellationToken);
			await director.StepAsync("give-subject-gathering", token => gm.ExecuteAsync(
				new GmCommand("addskill", ["30001", "15"], "success add skill"), gmSubject, token), cancellationToken);
			await director.StepAsync("boost-subject-physical-attack", token => gm.ExecuteAsync(
				new GmCommand("stat", ["physical_attack", "10000"], "is now set to 10000"), gmSubject, token),
				cancellationToken);

			string root = Environment.GetEnvironmentVariable("AION_E2E_QUEST_PLAN_ROOT")
				?? throw new InvalidOperationException("AION_E2E_QUEST_PLAN_ROOT is required for quest-plan scenarios.");
			IReadOnlyList<QuestRunPlan> plans = OrderLiveQuestPlans(
				QuestRunPlan.LoadDirectory(Path.Combine(root, zone)));
			if (plans.Count == 0)
				throw new InvalidDataException($"No runnable quest plans were generated for {zone}.");
			var driver = new LiveQuestRunDriver(subject, director, gm, plans.Select(plan => plan.Id).ToHashSet());
			foreach (QuestRunPlan plan in plans)
			{
				subject.Trace.WriteAction($"q{plan.Id}", "quest-plan:start", new Dictionary<string, object?>
				{
					["questId"] = plan.Id,
					["template"] = plan.Template,
				});
				try
				{
					await QuestRunExecutor.ExecuteAsync(QuestRunBook.Build(plan), driver, cancellationToken);
				}
				catch (Exception exception) when (exception is not OperationCanceledException)
				{
					throw new InvalidDataException($"Q{plan.Id} ({plan.Template}) failed: {exception.Message}", exception);
				}
				if (!subject.Session.Api.World.Quests.TryGetValue(plan.Id, out BotQuestState? state) || state.Status != 5)
					throw new InvalidDataException($"Q{plan.Id} did not finish with COMPLETE status.");
			}

			await subject.StepAsync("quit", subject.Session.QuitAsync, cancellationToken);
			await director.StepAsync("quit", director.Session.QuitAsync, cancellationToken);
			subject.Trace.WriteAction(subject.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = scenarioId });
			director.Trace.WriteAction(director.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = scenarioId + "-director" });
			Console.WriteLine($"LIVE {scenarioId} completed {plans.Count} generated quest plans.");
			return 0;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception exception)
		{
			Console.Error.WriteLine($"{scenarioId} failed: {exception}");
			return 1;
		}
	}

	private static IReadOnlyList<QuestRunPlan> OrderLiveQuestPlans(IReadOnlyList<QuestRunPlan> plans)
	{
		var byId = plans.ToDictionary(plan => plan.Id);
		var ordered = new List<QuestRunPlan>(plans.Count);
		var complete = new HashSet<int>();
		while (ordered.Count != plans.Count)
		{
			QuestRunPlan? next = byId.Values
				.Where(plan => !complete.Contains(plan.Id))
				.Where(plan => plan.FinishedQuestGroups.Count == 0 || plan.FinishedQuestGroups.Any(
					group => group.All(required => !byId.ContainsKey(required) || complete.Contains(required))))
				.OrderBy(plan => plan.Id)
				.FirstOrDefault();
			if (next == null)
				throw new InvalidDataException("Quest-plan prerequisites contain a cycle inside the selected zone.");
			ordered.Add(next);
			complete.Add(next.Id);
		}
		return ordered;
	}

	private sealed class LiveQuestRunDriver(
		L0Actor subject,
		L0Actor director,
		LiveGmFacade gm,
		IReadOnlySet<int> selectedQuestIds) : IQuestRunDriver
	{
		private readonly GmSubject gmSubject = new(subject.Session.CharacterId, subject.Session.CharacterName);

		public async Task ExecuteAsync(QuestRunPlan plan, QuestRunOperation operation, CancellationToken cancellationToken)
		{
			string action = $"q{plan.Id}-{operation.Kind.ToString().ToLowerInvariant()}";
			switch (operation.Kind)
			{
				case QuestRunOperationKind.Prepare:
					await PrepareAsync(plan, cancellationToken);
					break;
				case QuestRunOperationKind.StartAtNpc:
					await subject.StepAsync(action, async token =>
					{
						int npc = await MoveToNpcAsync(RequiredNpcs(operation), 0, action, token);
						await subject.Session.StartQuestAsync(npc, plan.Id, token);
						await subject.Session.WaitForQuestStatusAsync(plan.Id, 3, token);
					}, cancellationToken);
					break;
				case QuestRunOperationKind.StartWithItem:
					await StartWithItemAsync(plan, operation.ItemId, action, cancellationToken);
					break;
				case QuestRunOperationKind.Kill:
				case QuestRunOperationKind.KillSpawned:
					await KillAsync(RequiredNpcs(operation), operation.Count, action, cancellationToken);
					break;
				case QuestRunOperationKind.CollectQuestDrop:
				case QuestRunOperationKind.UseQuestObject:
					await CollectAsync(plan, operation, action, cancellationToken);
					break;
				case QuestRunOperationKind.Gather:
					await GatherAsync(operation, action, cancellationToken);
					break;
				case QuestRunOperationKind.Report:
					await ReportAsync(plan, operation, action, cancellationToken);
					break;
				case QuestRunOperationKind.ClaimReward:
					await ClaimRewardAsync(plan, action, cancellationToken);
					break;
				default:
					throw new InvalidDataException($"Starter-zone LIVE driver cannot execute {operation.Kind} for Q{plan.Id}.");
			}
		}

		private async Task PrepareAsync(QuestRunPlan plan, CancellationToken token)
		{
			IReadOnlyList<int>? group = plan.FinishedQuestGroups.FirstOrDefault();
			if (group == null)
				return;
			foreach (int prerequisite in group)
			{
				bool complete = subject.Session.Api.World.Quests.TryGetValue(prerequisite, out BotQuestState? state) && state.Status == 5 ||
					subject.Session.Api.World.CompletedQuests.ContainsKey(prerequisite);
				if (complete)
					continue;
				if (selectedQuestIds.Contains(prerequisite))
					throw new InvalidDataException($"Selected prerequisite Q{prerequisite} was not completed before Q{plan.Id}.");
				await director.StepAsync($"setup-q{plan.Id}-prerequisite-{prerequisite}", cancellation => gm.ExecuteAsync(
					new GmCommand("quest",
						[subject.Session.CharacterName, prerequisite.ToString(CultureInfo.InvariantCulture), "set", "COMPLETE", "0"],
						"Set quest status"), cancellationToken: cancellation), token);
			}
		}

		private async Task StartWithItemAsync(QuestRunPlan plan, int itemId, string action, CancellationToken token)
		{
			await director.StepAsync("setup-" + action, cancellation => gm.ExecuteAsync(
				new GmCommand("add", [subject.Session.CharacterName, itemId.ToString(CultureInfo.InvariantCulture), "1"], "You gave"),
				cancellationToken: cancellation), token);
			await subject.StepAsync(action, async cancellation =>
			{
				await subject.Session.WaitForInventoryItemAsync(itemId, cancellation);
				BotInventoryItem item = subject.Session.Api.World.Inventory.Values.Single(entry => entry.ItemId == itemId);
				await subject.Session.SendPacketAsync(GameClientPackets.UseItem(item.ObjectId), cancellation);
				DecodedBotServerPacket dialog = await subject.Session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), cancellation,
					packet => packet.Get<int>("questId") == plan.Id);
				int target = dialog.Get<int>("targetObjectId");
				await subject.Session.SendPacketAsync(subject.Session.Api.SelectDialog(
					target, DialogAction.QUEST_ACCEPT_1, questId: plan.Id), cancellation);
				await subject.Session.WaitForQuestStatusAsync(plan.Id, 3, cancellation);
			}, token);
		}

		private async Task KillAsync(IReadOnlyList<QuestRunNpc> npcs, int count, string action, CancellationToken token)
		{
			var defeated = new HashSet<int>();
			for (int index = 0; index < count; index++)
			{
				int ordinal = index;
				await subject.StepAsync($"{action}-{index + 1}", async cancellation =>
				{
					int npc = await MoveToNpcAsync(npcs, ordinal, $"{action}-{ordinal + 1}", cancellation, defeated);
					await subject.Session.KillNpcAsync(npc, cancellation);
					defeated.Add(npc);
				}, token);
			}
		}

		private async Task CollectAsync(QuestRunPlan plan, QuestRunOperation operation, string action, CancellationToken token)
		{
			QuestRunNpc npcPlan = operation.Source?.Npc
				?? throw new InvalidDataException("Quest-item operation has no NPC source.");
			var used = new HashSet<int>();
			for (int attempts = 0; ItemCount(operation.ItemId) < operation.Count; attempts++)
			{
				if (attempts >= operation.Count + 32)
					throw new InvalidDataException($"Could not collect item {operation.ItemId} after {attempts} attempts.");
				int ordinal = attempts;
				await subject.StepAsync($"{action}-{attempts + 1}", async cancellation =>
				{
					int npc = await MoveToNpcAsync([npcPlan], ordinal, $"{action}-{ordinal + 1}", cancellation, used);
					used.Add(npc);
					if (operation.Kind == QuestRunOperationKind.UseQuestObject)
						await subject.Session.UseQuestObjectAsync(npc, operation.ItemId, cancellation);
					else if (npcPlan.Id >= 700000)
						await subject.Session.LootQuestItemAsync(npc, operation.ItemId, openLoot: false, cancellation);
					else
					{
						await subject.Session.KillNpcAsync(npc, cancellation);
						await subject.Session.TryLootQuestItemAsync(npc, operation.ItemId, cancellation);
					}
				}, token);
			}
			if (ItemCount(operation.ItemId) != operation.Count)
				throw new InvalidDataException($"Q{plan.Id} collected the wrong count for item {operation.ItemId}.");
		}

		private async Task GatherAsync(QuestRunOperation operation, string action, CancellationToken token)
		{
			QuestRunSource source = operation.Source ?? throw new InvalidDataException("Gather operation has no source.");
			int templateId = source.GatherableId ?? throw new InvalidDataException("Gather source has no template id.");
			var used = new HashSet<int>();
			for (int attempts = 0; ItemCount(operation.ItemId) < operation.Count; attempts++)
			{
				if (attempts >= operation.Count + 16)
					throw new InvalidDataException($"Could not gather item {operation.ItemId} after {attempts} attempts.");
				QuestRunPosition position = source.Positions[attempts % source.Positions.Count];
				await MoveSubjectWithDirectorAsync(director, subject, gm, position.MapId,
					position.X - 1, position.Y, position.Z, $"{action}-{attempts + 1}", token);
				await subject.StepAsync($"{action}-{attempts + 1}", async cancellation =>
				{
					int gatherable = await subject.Session.WaitForNearestObjectExceptAsync(
						BotKnownObjectKind.Gatherable, templateId, used, cancellation);
					used.Add(gatherable);
					await subject.Session.MoveToKnownObjectAsync(gatherable, cancellation);
					foreach (BotClientPacket packet in subject.Session.Api.Gather(gatherable))
						await subject.Session.SendPacketAsync(packet, cancellation);
					DecodedBotServerPacket initial = await subject.Session.WaitForPacketAsync(typeof(SM_GATHER_UPDATE), cancellation);
					if (initial.Get<byte>("action") != 0)
						throw new InvalidDataException($"Gatherable {templateId}/{gatherable} refused start.");
					await subject.Session.WaitForPacketAsync(typeof(SM_GATHER_UPDATE), cancellation,
						packet => packet.Get<byte>("action") is 6 or 7);
				}, token);
			}
		}

		private async Task ReportAsync(QuestRunPlan plan, QuestRunOperation operation, string action, CancellationToken token)
		{
			if (plan.Template is not ("report_to_many" or "item_order"))
				return;
			int maximum = plan.Steps.Where(step => step.Kind == "report").Max(step => step.Sequence);
			if (operation.Sequence == maximum)
				return;
			await subject.StepAsync(action, async cancellation =>
			{
				int npc = await MoveToNpcAsync(RequiredNpcs(operation), 0, action, cancellation);
				ushort dialogAction = plan.Template == "report_to_many"
					? checked((ushort)(DialogAction.SETPRO1 + operation.Sequence - 1))
					: checked((ushort)DialogAction.SETPRO1);
				await subject.Session.ReportQuestStepAsync(npc, plan.Id, dialogAction, cancellation);
			}, token);
		}

		private async Task ClaimRewardAsync(QuestRunPlan plan, string action, CancellationToken token)
		{
			await subject.StepAsync(action, async cancellation =>
			{
				int npc = await MoveToNpcAsync(plan.EndNpcs, 0, action, cancellation);
				if (plan.Template is "item_collecting" or "xml_quest")
					await subject.Session.FinishItemQuestAsync(npc, plan.Id, cancellation);
				else
					await subject.Session.FinishQuestWithActionAsync(npc, plan.Id,
						plan.HasSelectableReward ? DialogAction.SELECTED_QUEST_REWARD1 : DialogAction.SELECTED_QUEST_NOREWARD,
						cancellation);
			}, token);
		}

		private async Task<int> MoveToNpcAsync(IReadOnlyList<QuestRunNpc> npcs, int ordinal, string action,
			CancellationToken token, IReadOnlySet<int>? excluded = null)
		{
			QuestRunNpc selected = npcs.FirstOrDefault(npc => npc.Positions.Count != 0)
				?? throw new InvalidDataException("Quest operation has no positioned NPC alternative.");
			QuestRunPosition position = selected.Positions[ordinal % selected.Positions.Count];
			await MoveSubjectWithDirectorAsync(director, subject, gm, position.MapId,
				position.X - 1, position.Y, position.Z, action, token);
			int objectId = await subject.Session.WaitForNearestObjectExceptAsync(
				BotKnownObjectKind.Npc, selected.Id, excluded ?? new HashSet<int>(), token);
			await subject.Session.MoveToKnownObjectAsync(objectId, token);
			return objectId;
		}

		private long ItemCount(int itemId) =>
			subject.Session.Api.World.Inventory.Values.Where(item => item.ItemId == itemId).Sum(item => item.Count);

		private static IReadOnlyList<QuestRunNpc> RequiredNpcs(QuestRunOperation operation) =>
			operation.Npcs is { Count: > 0 } npcs
				? npcs
				: throw new InvalidDataException($"Quest operation {operation.Kind} has no NPC alternatives.");
	}
}

internal sealed partial class LiveBotSession
{
	public async Task<int> WaitForNearestObjectExceptAsync(BotKnownObjectKind kind, int templateId,
		IReadOnlySet<int> excludedObjectIds, CancellationToken cancellationToken)
	{
		BotPosition position = CurrentPosition;
		BotKnownObject? nearest = Api.World.Objects.Values
			.Where(candidate => candidate.Kind == kind && candidate.TemplateId == templateId &&
				!excludedObjectIds.Contains(candidate.ObjectId))
			.OrderBy(candidate => DistanceSquared(position, candidate.Position))
			.FirstOrDefault();
		if (nearest == null)
		{
			Type packetType = kind == BotKnownObjectKind.Npc ? typeof(SM_NPC_INFO) : typeof(SM_GATHERABLE_INFO);
			DecodedBotServerPacket packet = await WaitForPacketAsync(packetType, cancellationToken,
				candidate => candidate.Get<int>(kind == BotKnownObjectKind.Npc ? "npcId" : "templateId") == templateId &&
					!excludedObjectIds.Contains(candidate.Get<int>("objectId")));
			nearest = Api.World.Objects[packet.Get<int>("objectId")];
		}
		float distance = MathF.Sqrt(DistanceSquared(position, nearest.Position));
		if (distance > 200f)
			throw new InvalidDataException($"Nearest {kind} template {templateId} was {distance:F1}m away after setup teleport.");
		return nearest.ObjectId;
	}

	public Task MoveToKnownObjectAsync(int objectId, CancellationToken cancellationToken) =>
		MoveToNpcAsync(objectId, cancellationToken);

	public async Task<bool> TryLootQuestItemAsync(int objectId, int itemId, CancellationToken cancellationToken)
	{
		await SendPacketAsync(Api.Loot(objectId), cancellationToken);
		DecodedBotServerPacket list = await WaitForPacketAsync(typeof(SM_LOOT_ITEMLIST), cancellationToken,
			packet => packet.Get<int>("targetObjectId") == objectId);
		IReadOnlyDictionary<string, object?>? item = list
			.Get<List<IReadOnlyDictionary<string, object?>>>("items")
			.SingleOrDefault(entry => ReadField<int>(entry, "itemId") == itemId);
		if (item == null)
		{
			await SendPacketAsync(Api.Loot(objectId, close: true), cancellationToken);
			return false;
		}
		BotInventoryItem? existing = Api.World.Inventory.Values.SingleOrDefault(entry => entry.ItemId == itemId);
		await SendPacketAsync(Api.Loot(objectId, ReadField<byte>(item, "index")), cancellationToken);
		if (existing == null)
		{
			await WaitForPacketAsync(typeof(SM_INVENTORY_ADD_ITEM), cancellationToken,
				packet => packet.Get<List<IReadOnlyDictionary<string, object?>>>("items")
					.Any(entry => ReadField<int>(entry, "itemId") == itemId));
		}
		else
		{
			await WaitForPacketAsync(typeof(SM_INVENTORY_UPDATE_ITEM), cancellationToken,
				packet => packet.Get<int>("objectId") == existing.ObjectId);
		}
		await SendPacketAsync(Api.Loot(objectId, close: true), cancellationToken);
		return true;
	}

	public async Task UseQuestObjectAsync(int objectId, int itemId, CancellationToken cancellationToken)
	{
		BotInventoryItem? existing = Api.World.Inventory.Values.SingleOrDefault(entry => entry.ItemId == itemId);
		await SendPacketAsync(Api.TalkTo(objectId), cancellationToken);
		await WaitForPacketAsync(typeof(SM_EMOTION), cancellationToken,
			packet => packet.Get<int>("senderObjectId") == CharacterId &&
				packet.Get<byte>("emotionType") == (byte)EmotionType.START_QUESTLOOT);
		await WaitForPacketAsync(typeof(SM_EMOTION), cancellationToken,
			packet => packet.Get<int>("senderObjectId") == CharacterId &&
				packet.Get<byte>("emotionType") == (byte)EmotionType.START_QUESTLOOT);
		if (existing == null)
		{
			await WaitForPacketAsync(typeof(SM_INVENTORY_ADD_ITEM), cancellationToken,
				packet => packet.Get<List<IReadOnlyDictionary<string, object?>>>("items")
					.Any(entry => ReadField<int>(entry, "itemId") == itemId));
		}
		else
		{
			await WaitForPacketAsync(typeof(SM_INVENTORY_UPDATE_ITEM), cancellationToken,
				packet => packet.Get<int>("objectId") == existing.ObjectId);
		}
	}

	public async Task ReportQuestStepAsync(int npcObjectId, int questId, ushort action,
		CancellationToken cancellationToken)
	{
		await SendPacketAsync(Api.TalkTo(npcObjectId), cancellationToken);
		await WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), cancellationToken);
		await SendPacketAsync(Api.SelectDialog(npcObjectId, DialogAction.QUEST_SELECT, questId: questId), cancellationToken);
		await WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), cancellationToken);
		await SendPacketAsync(Api.SelectDialog(npcObjectId, action, questId: questId), cancellationToken);
		await WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), cancellationToken);
	}
}
