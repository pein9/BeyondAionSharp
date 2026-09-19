using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public interface IGroupLootDriver
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
	Task VerifyLootAsync(int corpseId, IReadOnlyList<int> allowedLooters, bool collected, CancellationToken token);
}

/// <summary>S5: real kills of temporary copies of an existing NPC; unmodified guaranteed global drop and ordinary group loot.</summary>
public static class GroupLootScenario
{
	public const int MapId = 220010000;
	public const int NpcId = 210648; // Squzii Ironfist, level 2, 199 HP.
	public const int ItemId = 162000031; // Existing Named Potion rule: five Squzii's Carrot Juices at 100%.
	public const int DropCount = 5;
	public static readonly BotPosition SpawnPoint = new(573, 2789, 299.875f, 0);

	public static async Task RunAsync(IReadOnlyList<IGroupLootDriver> players, Func<CancellationToken, Task> spawnForSetup,
		CancellationToken token = default)
	{
		Require(players.Count == 3 && players.Select(p => p.CharacterId).Distinct().Count() == 3, "S5 requires three distinct players.");
		var leader = players[0];
		await SyncAsync(token);
		await StepAsync("form-three-player-loot-group", async ct =>
		{
			foreach (var invited in players.Skip(1))
			{
				await leader.SendAsync(leader.Api.InviteToGroup(invited.CharacterName), ct);
				await invited.WaitAsync(typeof(SM_QUESTION_WINDOW), p => p.Get<int>("code") == 60000, ct);
				await invited.SendAsync(invited.Api.Answer(1), ct);
				await invited.WaitAsync(typeof(SM_GROUP_INFO), p => p.Get<int>("leaderId") == leader.CharacterId, ct);
			}
			await SyncAsync(ct);
			foreach (var viewer in players)
				Require(viewer.Api.World.GroupId > 0 && viewer.Api.World.GroupId == leader.Api.World.GroupId &&
					viewer.Api.World.GroupMembers.Count == 3 && viewer.Api.World.GroupMembers.Keys.Order().SequenceEqual(players.Select(p => p.CharacterId).Order()),
					"S5 group roster must contain all three subjects.");
		}, token);

		var roundRobinOwners = new HashSet<int>();
		int kills = 0;
		foreach (var (rule, distribution, repetitions, name) in new[]
		{
			((byte)0, 0, 1, "free-for-all"), ((byte)1, 0, 3, "round-robin"),
			((byte)2, 0, 1, "leader-only"), ((byte)0, 2, 1, "three-player-roll"),
		})
		{
			await StepAsync("set-loot-" + name, async ct =>
			{
				await leader.SendAsync(leader.Api.SetGroupLoot(rule, distribution), ct);
				foreach (var viewer in players)
					await viewer.WaitAsync(typeof(SM_GROUP_INFO), p => p.Get<int[]>("lootRules").SequenceEqual(
						new[] { (int)rule, 0, distribution, distribution, distribution, distribution, distribution, distribution }), ct);
				await SyncAsync(ct);
			}, token);
			for (int iteration = 0; iteration < repetitions; iteration++)
			{
				await StepAsync($"kill-and-loot-{name}-{iteration + 1}", async ct =>
				{
					var existing = leader.Api.World.Objects.Keys.ToHashSet();
					await spawnForSetup(ct);
					var spawned = await leader.WaitAsync(typeof(SM_NPC_INFO), p => p.Get<int>("npcId") == NpcId && !existing.Contains(p.Get<int>("objectId")), ct);
					int corpse = spawned.Get<int>("objectId");
					await SyncAsync(ct);
					Require(players.All(p => p.Api.World.Objects.ContainsKey(corpse)), "All three subjects must see the spawned target.");
					long[] before = players.Select(CountItem).ToArray();
					await DefeatAsync(corpse, ct);
					var allowed = players.Where(p => p.Api.World.LootStatuses.TryGetValue(corpse, out byte status) && status == 0).ToArray();
					Require(allowed.Length == (rule == 0 ? 3 : 1), "Wrong number of clients received initial corpse loot rights.");
					if (rule == 1) Require(roundRobinOwners.Add(allowed[0].CharacterId), "Round-robin did not rotate to a new member across three consecutive kills.");
					if (rule == 2) Require(allowed[0].CharacterId == leader.CharacterId, "Leader-only corpse rights were assigned to another member.");
					int[] rights = allowed.Select(p => p.CharacterId).ToArray();
					foreach (var viewer in players) await viewer.VerifyLootAsync(corpse, rights, false, ct);
					// In FFA explicitly use a non-leader, proving the right is usable and not just cosmetic.
					var looter = allowed[^1];
					await looter.SendAsync(looter.Api.Loot(corpse), ct);
					var list = await looter.WaitAsync(typeof(SM_LOOT_ITEMLIST), p => p.Get<int>("targetObjectId") == corpse, ct);
					await looter.WaitAsync(typeof(SM_LOOT_STATUS), p => p.Get<int>("targetObjectId") == corpse && p.Get<byte>("status") == 2, ct);
					var items = list.Get<List<IReadOnlyDictionary<string, object?>>>("items").Where(row => (int)row["itemId"]! == ItemId).ToArray();
					Require(items.Length == 1 && (int)items[0]["count"]! == DropCount, "Guaranteed named potion drop must be exactly one stack of five.");
					Require(looter.Api.World.Loot?.TargetObjectId == corpse &&
						looter.Api.World.Loot.Items.Count(item => item.ItemId == ItemId && item.Count == DropCount) == 1,
						"Opening the loot window lost the previously received item list.");
					byte index = (byte)items[0]["index"]!;
					await looter.SendAsync(looter.Api.Loot(corpse, index), ct);
					IGroupLootDriver winner = distribution == 2 ? await RollAsync(corpse, index, before, ct) : looter;
					int winnerIndex = players.ToList().IndexOf(winner);
					await WaitItemCountAsync(winner, before[winnerIndex] + DropCount, ct);
					await SyncAsync(ct);
					AssertCounts(before, winner);
					foreach (var viewer in players) await viewer.VerifyLootAsync(corpse, rights, true, ct);
					await looter.SendAsync(looter.Api.Loot(corpse, close: true), ct);
					await SyncAsync(ct);
					if (distribution == 2)
					{
						// Observe across the scheduled 17-second roll timeout, not only at immediate completion.
						for (int second = 0; second < 18; second++)
						{
							await leader.DelayAsync(TimeSpan.FromSeconds(1), ct); await SyncAsync(ct); AssertCounts(before, winner);
						}
					}
					kills++;
				}, token);
			}
		}
		Require(roundRobinOwners.Count == 3 && kills == 6 && players.Sum(CountItem) == 6 * DropCount,
			"S5 did not complete six kills with conserved potion counts and a full round-robin cycle.");
		await StepAsync("leave-loot-group", async ct =>
		{
			foreach (var member in players.Skip(1))
			{
				await member.SendAsync(member.Api.LeaveGroup(), ct);
				await member.WaitAsync(typeof(SM_LEAVE_GROUP_MEMBER), _ => true, ct);
			}
			await SyncAsync(ct);
			Require(players.All(p => p.Api.World.GroupId == null), "S5 group did not disband.");
		}, token);

		async Task DefeatAsync(int target, CancellationToken ct)
		{
			for (int attack = 0; attack < 30; attack++)
			{
				foreach (var player in players)
				{
					Require(!player.Api.World.IsDead, "A loot subject died in ordinary combat.");
					var npc = player.Api.World.Objects[target];
					await player.MoveAsync(npc.Position with { X = npc.Position.X - 1 }, ct);
					// Task.Delay truncates fractional milliseconds; re-check the actual timing gate after waking.
					while (player.Api.Timing.TimeUntilAttack(1400) is var remaining && remaining > TimeSpan.Zero)
						await player.DelayAsync(remaining + TimeSpan.FromMilliseconds(1), ct);
					await player.SendAsync(player.Api.Target(target), ct);
					await player.SendAsync(player.Api.Attack(target, 1400, (byte)attack), ct);
					await SyncAsync(ct);
					if (players.Any(p => p.Api.World.LootStatuses.TryGetValue(target, out byte status) && status == 0)) return;
				}
				await leader.DelayAsync(TimeSpan.FromMilliseconds(1450), ct);
				await SyncAsync(ct);
			}
			throw new InvalidDataException("Loot target survived the bounded normal-attack window.");
		}
		async Task<IGroupLootDriver> RollAsync(int corpse, byte index, long[] before, CancellationToken ct)
		{
			foreach (var viewer in players)
			{
				var start = await viewer.WaitAsync(typeof(SM_GROUP_LOOT), p => Match(p) && p.Get<int>("playerId") == 0, ct);
				Require(start.Get<int>("luck") == 1, "Roll did not start normally.");
			}
			int[] rolls = new int[3];
			for (int i = 0; i < 3; i++)
			{
				await players[i].SendAsync(players[i].Api.RollForLoot(leader.Api.World.GroupId!.Value, index, ItemId, corpse), ct);
				foreach (var viewer in players)
				{
					// The Java wire field is the recipient id for intermediate rolls, not the roller id.
					var rolled = await viewer.WaitAsync(typeof(SM_GROUP_LOOT), p => Match(p) && p.Get<int>("playerId") == viewer.CharacterId &&
						p.Get<int>("luck") > 0, ct);
					int value = rolled.Get<int>("luck");
					Require(value is >= 1 and <= 100 && (rolls[i] == 0 || rolls[i] == value), "Clients disagree on an in-range group roll.");
					rolls[i] = value;
				}
				if (i < 2)
				{
					await SyncAsync(ct);
					Require(players.Select(CountItem).SequenceEqual(before), "Item was distributed before all three rolls.");
				}
			}
			var winner = players[Array.IndexOf(rolls, rolls.Max())]; // Strict greater-than: first roller wins a tie.
			foreach (var viewer in players)
			{
				var end = await viewer.WaitAsync(typeof(SM_GROUP_LOOT), p => Match(p) && p.Get<int>("luck") == -1, ct);
				Require(end.Get<int>("playerId") == winner.CharacterId, "Roll winner is not the first highest roller.");
			}
			return winner;
			bool Match(DecodedBotServerPacket p) => p.Get<int>("lootCorpseId") == corpse && p.Get<int>("index") == index &&
				p.Get<int>("itemId") == ItemId && p.Get<int>("itemCount") == DropCount && p.Get<byte>("distributionId") == 2 &&
				p.Get<int>("groupId") == leader.Api.World.GroupId;
		}
		async Task SyncAsync(CancellationToken ct) { foreach (var player in players) await player.SynchronizeAsync(ct); }
		Task StepAsync(string name, Func<CancellationToken, Task> operation, CancellationToken ct) =>
			players[0].StepAsync(name, inner => players[1].StepAsync(name, next => players[2].StepAsync(name, operation, next), inner), ct);
		void AssertCounts(long[] before, IGroupLootDriver winner)
		{
			for (int i = 0; i < players.Count; i++)
				Require(CountItem(players[i]) == before[i] + (players[i] == winner ? DropCount : 0), "Loot was lost, duplicated or delivered to a losing player.");
		}
	}

	private static long CountItem(IGroupLootDriver player) => player.Api.World.Inventory.Values.Where(item => item.ItemId == ItemId).Sum(item => item.Count);
	private static async Task WaitItemCountAsync(IGroupLootDriver player, long count, CancellationToken token)
	{
		long before = CountItem(player);
		if (before == count) return;
		await player.WaitAsync(before == 0 ? typeof(SM_INVENTORY_ADD_ITEM) : typeof(SM_INVENTORY_UPDATE_ITEM), _ => CountItem(player) == count, token);
	}
	private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
