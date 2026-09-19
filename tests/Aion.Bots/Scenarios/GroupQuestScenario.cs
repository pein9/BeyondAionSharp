using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public interface IGroupQuestDriver
{
	BotApi Api { get; }
	int CharacterId { get; }
	string CharacterName { get; }
	Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token);
	Task SendAsync(BotClientPacket packet, CancellationToken token);
	Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token);
	Task SynchronizeAsync(CancellationToken token);
	Task DelayAsync(TimeSpan duration, CancellationToken token);
	Task MoveAsync(BotPosition destination, CancellationToken token);
	Task VerifyGroupQuestAsync(int? corpseId, int? attackerId, CancellationToken token);
}

/// <summary>S6: share implemented quest 1112, credit a nearby non-attacker for every kill, and turn in for both players.</summary>
public static class GroupQuestScenario
{
	public const int MapId = 210010000;
	public const int QuestId = 1112;
	public const int GiverId = 203072; // Feira; To Fish in Peace, five brax + five slinks.
	public const int RewardItem = 169300002;
	public static readonly BotPosition GiverPoint = new(588.261f, 1094.75f, 101.514f, 103);
	public static readonly BotPosition SpawnPoint = GiverPoint with { X = GiverPoint.X + 10 };

	public static async Task RunAsync(IReadOnlyList<IGroupQuestDriver> players,
		Func<int, CancellationToken, Task> spawnForSetup, CancellationToken token = default)
	{
		Require(players.Count == 2 && players[0].CharacterId != players[1].CharacterId, "S6 needs two distinct players.");
		var leader = players[0]; var recipient = players[1];
		await SyncAsync(token);
		Require(players.All(p => p.Api.World.Level == 9 && p.Api.World.MapId == MapId && !p.Api.World.Quests.ContainsKey(QuestId)),
			"S6 needs fresh level-nine subjects without the test quest.");
		await StepAsync("form-quest-group-and-accept-at-feira", async ct =>
		{
			await leader.SendAsync(leader.Api.InviteToGroup(recipient.CharacterName), ct);
			await recipient.WaitAsync(typeof(SM_QUESTION_WINDOW), p => p.Get<int>("code") == 60000, ct);
			await recipient.SendAsync(recipient.Api.Answer(1), ct);
			await recipient.WaitAsync(typeof(SM_GROUP_INFO), p => p.Get<int>("leaderId") == leader.CharacterId, ct);
			await SyncAsync(ct);
			foreach (var player in players)
				Require(player.Api.World.GroupMembers.Keys.Order().SequenceEqual(players.Select(p => p.CharacterId).Order()), "S6 group roster differs.");
			int giver = Giver(leader);
			await leader.MoveAsync(GiverPoint with { X = GiverPoint.X - 1 }, ct);
			await leader.SendAsync(leader.Api.TalkTo(giver), ct);
			await DialogAsync(leader, giver, null, ct);
			await leader.SendAsync(leader.Api.SelectDialog(giver, DialogAction.QUEST_SELECT, questId: QuestId), ct);
			await DialogAsync(leader, giver, 1011, ct);
			await leader.SendAsync(leader.Api.SelectDialog(giver, DialogAction.QUEST_ACCEPT_1, questId: QuestId), ct);
			await QuestAsync(leader, 3, 0, ct);
			await DialogAsync(leader, giver, 1003, ct);
			await leader.SendAsync(leader.Api.CloseDialog(giver), ct);
			await SyncAsync(ct);
			Require(!recipient.Api.World.Quests.ContainsKey(QuestId), "Recipient already has the quest before sharing.");
		}, token);
		await StepAsync("share-quest-and-accept-from-player", async ct =>
		{
			await leader.SendAsync(leader.Api.ShareQuest(QuestId), ct);
			await recipient.WaitAsync(typeof(SM_QUEST_ACTION), p => p.Get<int>("questId") == QuestId && p.Get<byte>("action") == 5, ct);
			Require(recipient.Api.World.PendingQuestShare == new BotQuestShare(QuestId, leader.CharacterId, false), "Share sender, quest or team kind differs.");
			Require(!recipient.Api.World.Quests.ContainsKey(QuestId), "Receiving a share accepted it without a client response.");
			await leader.WaitAsync(typeof(SM_SYSTEM_MESSAGE), p => p.Get<int>("msgId") == 1100002, ct);
			await recipient.SendAsync(recipient.Api.AcceptSharedQuest(), ct);
			await QuestAsync(recipient, 3, 0, ct);
			await SyncAsync(ct);
			foreach (var player in players) await player.VerifyGroupQuestAsync(null, null, ct);
		}, token);

		int[] targets = [210259, 210065];
		int[] counters = [0, 0];
		for (int objective = 0; objective < 2; objective++)
		for (int kill = 0; kill < 5; kill++)
		{
			int npcId = targets[objective];
			var attacker = players[(objective * 5 + kill) % 2];
			await StepAsync($"group-credit-{npcId}-{kill + 1}-attacker-{attacker.CharacterName}", async ct =>
			{
				await SyncAsync(ct);
				var existing = leader.Api.World.Objects.Keys.ToHashSet();
				await spawnForSetup(npcId, ct);
				var spawned = await leader.WaitAsync(typeof(SM_NPC_INFO), p => p.Get<int>("npcId") == npcId && !existing.Contains(p.Get<int>("objectId")), ct);
				int target = spawned.Get<int>("objectId");
				await SyncAsync(ct);
				Require(players.All(p => p.Api.World.Objects.ContainsKey(target)), "Both quest members must perceive the kill target.");
				int before = counters[0] | counters[1] << 6;
				counters[objective]++;
				int after = counters[0] | counters[1] << 6;
				for (int attack = 0; attack < 40 && leader.Api.World.Quests[QuestId].StepAndFlags == before; attack++)
				{
					Require(players.All(p => !p.Api.World.IsDead), "A group quest subject died.");
					var npc = attacker.Api.World.Objects[target];
					await attacker.MoveAsync(npc.Position with { X = npc.Position.X - 1 }, ct);
					while (attacker.Api.Timing.TimeUntilAttack(1400) is var remaining && remaining > TimeSpan.Zero)
						await attacker.DelayAsync(remaining + TimeSpan.FromMilliseconds(1), ct);
					await attacker.SendAsync(attacker.Api.Target(target), ct);
					await attacker.SendAsync(attacker.Api.Attack(target, 1400, (byte)attack), ct);
					await SyncAsync(ct);
				}
				foreach (var player in players)
				{
					Require(player.Api.World.Quests[QuestId] is { Status: 3 } state && state.StepAndFlags == after,
						"Each group member must receive exactly one quest kill, including the member who never attacked.");
					await player.VerifyGroupQuestAsync(target, attacker.CharacterId, ct);
				}
				await leader.DelayAsync(TimeSpan.FromSeconds(2), ct); await SyncAsync(ct);
				Require(players.All(p => p.Api.World.Quests[QuestId].StepAndFlags == after), "Quest credit changed again after the kill.");
			}, token);
		}
		await StepAsync("turn-in-shared-quest-for-both-members", async ct =>
		{
			foreach (var player in players)
			{
				int giver = Giver(player);
				await player.MoveAsync(GiverPoint with { X = GiverPoint.X - 1 }, ct);
				long kinah = player.Api.World.Kinah, itemCount = CountReward(player);
				await player.SendAsync(player.Api.TalkTo(giver), ct); await DialogAsync(player, giver, null, ct);
				await player.SendAsync(player.Api.SelectDialog(giver, DialogAction.QUEST_SELECT, questId: QuestId), ct);
				await DialogAsync(player, giver, 1352, ct);
				await player.SendAsync(player.Api.SelectDialog(giver, DialogAction.SELECT_QUEST_REWARD, questId: QuestId), ct);
				await QuestAsync(player, 4, counters[0] | counters[1] << 6, ct);
				await DialogAsync(player, giver, 5, ct);
				await player.SendAsync(player.Api.SelectDialog(giver, DialogAction.SELECTED_QUEST_NOREWARD, questId: QuestId), ct);
				await QuestAsync(player, 5, 0, ct);
				await player.SynchronizeAsync(ct);
				Require(player.Api.World.Kinah == kinah + 1810 && CountReward(player) == itemCount + 30, "Shared quest completion rewards differ.");
				await player.VerifyGroupQuestAsync(null, null, ct);
				await player.SendAsync(player.Api.CloseDialog(giver), ct);
			}
		}, token);
		await StepAsync("leave-quest-group", async ct =>
		{
			await recipient.SendAsync(recipient.Api.LeaveGroup(), ct);
			await recipient.WaitAsync(typeof(SM_LEAVE_GROUP_MEMBER), _ => true, ct);
			await SyncAsync(ct);
			Require(players.All(p => p.Api.World.GroupId == null && p.Api.World.CompletedQuestIds.Contains(QuestId)), "Quest or group cleanup incomplete.");
		}, token);

		async Task SyncAsync(CancellationToken ct) { foreach (var player in players) await player.SynchronizeAsync(ct); }
		Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken ct) =>
			leader.StepAsync(action, inner => recipient.StepAsync(action, operation, inner), ct);
	}

	private static int Giver(IGroupQuestDriver player) => player.Api.World.Objects.Values.Single(n => n.TemplateId == GiverId).ObjectId;
	private static long CountReward(IGroupQuestDriver player) => player.Api.World.Inventory.Values.Where(i => i.ItemId == RewardItem).Sum(i => i.Count);
	private static Task<DecodedBotServerPacket> DialogAsync(IGroupQuestDriver player, int npc, ushort? page, CancellationToken token) =>
		player.WaitAsync(typeof(SM_DIALOG_WINDOW), p => p.Get<int>("targetObjectId") == npc && (page == null || p.Get<ushort>("dialogPageId") == page), token);
	private static Task<DecodedBotServerPacket> QuestAsync(IGroupQuestDriver player, byte status, int vars, CancellationToken token) =>
		player.WaitAsync(typeof(SM_QUEST_ACTION), p => p.Get<int>("questId") == QuestId && p.Get<byte>("action") is 1 or 2 &&
			p.Get<byte>("status") == status && p.Get<int>("stepAndFlags") == vars, token);
	private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
