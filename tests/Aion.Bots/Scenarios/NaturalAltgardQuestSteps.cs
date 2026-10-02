using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

/// <summary>
/// AF-07 (docs/natural-altgard-leveling.md): the scripted Leg 1 quest mechanics, driven by the contract's steps. A talk
/// step opens the NPC's dialog, sends each of the step's actions for its quest, waits for each page the handler sends,
/// finishes a movie, and checks that the quest moved on: an offer becomes START at var 0, a progress talk moves the var,
/// a reward talk completes the quest. Getting to the NPC (walking, the dungeon ramp, flight) is the caller's.
/// </summary>
public static class NaturalAltgardQuestSteps
{
	/// <summary>Play one contract step with an NPC in talk range. Returns a short description of the change.</summary>
	public static async Task<string> TalkAsync(INaturalJourneySession session, NaturalAltgardStep step, int npc, CancellationToken token)
	{
		BotWorldModel world = session.Api.World;
		(byte Status, int Var)? before = State(world, step.QuestId);
		Expect(step, before);
		await NaturalDialogProtocol.OpenAsync(session, npc, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == npc);
		// A walking NPC (Tulberg) walks on until the talk stops it. Java NpcController.onDialogSelect drops a choice made from out of
		// talk range without a word, so a walker that stopped beyond reach left the talk waiting for a page that never came (run-fast,
		// AG-00: 6.6 m away). Wait for it to stop, as a player watches it turn round, and step closer when it is out of reach.
		for (int wait = 0; wait < 12 && world.Objects.TryGetValue(npc, out BotKnownObject? walking) && walking.MoveTarget != null; wait++)
		{
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(250), token);
			await session.SynchronizeAsync(token);
		}
		if (world.Objects.TryGetValue(npc, out BotKnownObject? stood) &&
			MathF.Sqrt(MathF.Pow(session.CurrentPosition.X - stood.SettledPosition.X, 2) + MathF.Pow(session.CurrentPosition.Y - stood.SettledPosition.Y, 2)) > step.TalkRange)
			throw new NaturalDialogTooFarException();
		// USE_OBJECT is the talk itself: its page is the dialog just opened.
		string[] actions = step.Actions[0] == "USE_OBJECT" ? step.Actions[1..] : step.Actions;
		int pages = step.Actions[0] == "USE_OBJECT" ? 1 : 0;
		for (int i = 0; i < actions.Length; i++)
		{
			ushort action = checked((ushort)NaturalAscensionContract.DialogActionId(actions[i]));
			await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npc, action, questId: step.QuestId), token);
			// A movie plays on a SELECTn_n dialog action (SELECT2_1; Q2289's SELECT2_1_1, AB-06).
			if (step.MovieId != null && System.Text.RegularExpressions.Regex.IsMatch(actions[i], @"^SELECT\d_\d"))
				await NaturalMovieGate.FinishAsync(session, token);
			if (pages + i < step.Pages.Length)
			{
				int page = step.Pages[pages + i];
				await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet =>
					packet.Get<int>("targetObjectId") == npc && packet.Get<ushort>("dialogPageId") == page);
			}
		}
		await session.SynchronizeAsync(token);
		NaturalMovieGate.RecordSkipped(session);
		(byte Status, int Var)? after = State(world, step.QuestId);
		bool completed = world.CompletedQuestIds.Contains(step.QuestId);
		bool moved = step.ExpectedStatus switch
		{
			"OFFER" => after is (3, _), // an escort offer (Q2290 SELECT1_1) takes the quest straight to its follow var
			"REWARD" => completed,
			_ when step.Actions.Contains("SELECT_QUEST_REWARD") => completed,
			// A step may move the var by more than one (Q2239's check: var 1 -> 3, AB-06).
			_ => completed || after is (3, int advanced) && advanced > step.Var || after is (4, _),
		};
		if (!moved)
			throw new InvalidDataException($"{step.Key} did not move Q{step.QuestId} on: {Describe(before)} -> {(completed ? "complete" : Describe(after))}.");
		if (step.ReceivesItemId is int item && !world.Inventory.Values.Any(owned => owned.ItemId == item))
			throw new InvalidDataException($"{step.Key} did not hand over item {item}.");
		await session.SendPacketAsync(session.Api.CloseDialog(npc), token);
		return $"{step.Key}: {Describe(before)} -> {(completed ? "complete" : Describe(after))}";
	}

	/// <summary>Q2208: use the Mau Secret Remedy (anywhere); three seconds later the quest moves to var 1.</summary>
	public static async Task UseQuestItemAsync(INaturalJourneySession session, NaturalAltgardItemUse use, ItemTemplate template,
		CancellationToken token)
	{
		BotWorldModel world = session.Api.World;
		BotInventoryItem remedy = world.Inventory.Values.FirstOrDefault(item => item.ItemId == use.ItemId)
			?? throw new InvalidDataException($"Q{use.QuestId}: item {use.ItemId} is not in the inventory.");
		if (State(world, use.QuestId) is not (3, int current) || current != use.Var)
			throw new InvalidDataException($"Q{use.QuestId} is not at var {use.Var}.");
		await session.SendPacketAsync(session.Api.UseItem(remedy.ObjectId, template), token);
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(use.UseMillis + 100), token);
		await session.SynchronizeAsync(token);
		if (State(world, use.QuestId) is not (3, int next) || next != use.NextVar)
			throw new InvalidDataException($"Q{use.QuestId}: using item {use.ItemId} did not move the quest to var {use.NextVar}.");
	}

	/// <summary>
	/// Use a quest object in reach (AM-04, AM-05): open it as the client does, wait out the use bar (<c>SM_USE_OBJECT</c>),
	/// and for a loot take <paramref name="lootItemId"/> from the drop list it opens. False when the use was interrupted or
	/// the loot held no such item.
	/// </summary>
	public static async Task<bool> UseObjectAsync(INaturalJourneySession session, int objectId, int? lootItemId, CancellationToken token)
	{
		int start = session.PacketHistory.Count;
		await NaturalDialogProtocol.OpenAsync(session, objectId, token);
		await session.SynchronizeAsync(token);
		DecodedBotServerPacket? started = session.PacketHistory.Skip(start).LastOrDefault(packet =>
			packet.PacketType == typeof(SM_USE_OBJECT) && packet.Get<int>("targetObjectId") == objectId && packet.Get<byte>("actionType") != 2);
		int durationMs = started?.Get<int>("durationMs") ?? 3000;
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(Math.Max(durationMs, 1) + 1), token);
		await session.SynchronizeAsync(token);
		DecodedBotServerPacket? finish = session.PacketHistory.Skip(start).LastOrDefault(packet =>
			packet.PacketType == typeof(SM_USE_OBJECT) && packet.Get<int>("targetObjectId") == objectId && packet.Get<byte>("actionType") == 2);
		if (finish != null && finish.Get<int>("durationMs") <= 0) return false;
		return lootItemId is not int item || await NaturalIshalgenJourney.TryLootCorpseItemAsync(session, objectId, item, token, start);
	}

	/// <summary>
	/// AC-05: a contract object use. Some objects open a dialog as they are used (Q2221's safe: page 1693, Java
	/// <c>QuestItemNpcAI.handleUseItemFinish</c> sends it before the drop); the client answers it with the contract's close
	/// action after the loot, as a player clicks it away. False when the use was interrupted or the loot missed.
	/// </summary>
	public static async Task<bool> UseContractObjectAsync(INaturalJourneySession session, NaturalAltgardObjectUse use, int objectId,
		CancellationToken token)
	{
		int start = session.PacketHistory.Count;
		bool used = await UseObjectAsync(session, objectId, use.LootItemId, token);
		bool opened = use.DialogPage is int page && session.PacketHistory.Skip(Math.Min(start, session.PacketHistory.Count)).Any(packet =>
			packet.PacketType == typeof(SM_DIALOG_WINDOW) && packet.Get<int>("targetObjectId") == objectId && packet.Get<ushort>("dialogPageId") == page);
		if (opened && use.CloseAction is string close)
		{
			ushort action = checked((ushort)NaturalAscensionContract.DialogActionId(close));
			await session.SendPacketAsync(session.Api.SelectDialog(objectId, action, questId: use.QuestId), token);
			await session.SendPacketAsync(session.Api.CloseDialog(objectId), token);
			await session.SynchronizeAsync(token);
		}
		session.TraceDiagnostic("object-use", new Dictionary<string, object?>
		{
			["use"] = use.Key, ["object"] = objectId, ["used"] = used, ["dialog"] = opened, ["quest"] = State(session.Api.World, use.QuestId)?.ToString(),
		});
		return used;
	}

	/// <summary>Loot <paramref name="itemId"/> from a corpse in reach (AM-05: the Q24012 collections), through the journey's
	/// loot routine. False when the drop list does not hold it.</summary>
	public static Task<bool> LootItemAsync(INaturalJourneySession session, int objectId, int itemId, CancellationToken token) =>
		NaturalIshalgenJourney.TryLootCorpseItemAsync(session, objectId, itemId, token);

	/// <summary>An effect of <paramref name="skillId"/> on the bot, as the client sees it.</summary>
	public static bool HasEffect(BotWorldModel world, int skillId) =>
		world.VisibleEffects?.Any(effect => effect.SkillId == skillId) == true;

	/// <summary>The quest's status and first variable as the client sees it.</summary>
	public static (byte Status, int Var)? State(BotWorldModel world, int questId) =>
		world.Quests.TryGetValue(questId, out BotQuestState? quest) ? (quest.Status, quest.StepAndFlags & 0x3F) : null;

	private static void Expect(NaturalAltgardStep step, (byte Status, int Var)? state)
	{
		bool ready = step.ExpectedStatus switch
		{
			"OFFER" => state is null || state.Value.Status is not (3 or 4),
			"REWARD" => state is (4, _),
			_ => state is (3, int current) && current == step.Var,
		};
		if (!ready)
			throw new InvalidDataException($"{step.Key} expects Q{step.QuestId} {step.ExpectedStatus}{(step.Var is int v ? $" var {v}" : "")}, the client has {Describe(state)}.");
	}

	private static string Describe((byte Status, int Var)? state) => state is { } s ? $"status {s.Status} var {s.Var}" : "not taken";
}
