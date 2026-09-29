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
		// USE_OBJECT is the talk itself: its page is the dialog just opened.
		string[] actions = step.Actions[0] == "USE_OBJECT" ? step.Actions[1..] : step.Actions;
		int pages = step.Actions[0] == "USE_OBJECT" ? 1 : 0;
		for (int i = 0; i < actions.Length; i++)
		{
			ushort action = checked((ushort)NaturalAscensionContract.DialogActionId(actions[i]));
			await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npc, action, questId: step.QuestId), token);
			if (step.MovieId != null && actions[i] is "SELECT2_1" or "SELECT3_1" or "SELECT5_1")
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
			"OFFER" => after is (3, 0),
			"REWARD" => completed,
			_ when step.Actions.Contains("SELECT_QUEST_REWARD") => completed,
			_ => after is (3, int advanced) && advanced == step.Var + 1 || after is (4, _),
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
