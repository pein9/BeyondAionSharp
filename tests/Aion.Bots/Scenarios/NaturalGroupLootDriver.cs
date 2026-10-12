using Aion.Bots.Movement;
using Aion.Bots.World;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

/// <summary>Loot only after the server grants this client rights; item lists retain per-player quest ownership.</summary>
public sealed class NaturalGroupLootDriver(INaturalJourneySession session, NaturalJourneyRuntime runtime, Func<int, bool> questItemNeeded)
{
	private readonly HashSet<int> visited = [];
	public int Collected { get; private set; }

	public async Task<bool> StepAsync(CancellationToken token)
	{
		await session.SynchronizeAsync(token);
		var world = session.Api.World;
		if (world.IsDead) return false;
		int[] friends = [session.CharacterId, .. world.GroupMembers.Keys];
		if (session.PacketHistory.TakeLast(150).Any(packet => (session.Api.Timing.Now - (packet.ReceivedAt ?? session.Api.Timing.Now)).TotalSeconds < 2 &&
			friends.Any(id => runtime.IncomingAttacker(packet, id) is int attacker && world.Objects.TryGetValue(attacker, out var enemy) && !enemy.IsCorpse))) return false;
		var corpse = world.LootStatuses.Where(pair => pair.Value == 0 && !visited.Contains(pair.Key))
			.Select(pair => world.Objects.GetValueOrDefault(pair.Key)).OfType<BotKnownObject>()
			.Where(known => known.Kind == BotKnownObjectKind.Npc && NaturalServicePolicy.Distance(session.CurrentPosition, known.Position) <= 15)
			.OrderBy(known => NaturalServicePolicy.Distance(session.CurrentPosition, known.Position)).FirstOrDefault();
		if (corpse == null) return false;
		if (NaturalServicePolicy.Distance(session.CurrentPosition, corpse.Position) > 3)
		{
			var path = runtime.CreateGeometry().FindLocalPath(world.MapId!.Value, session.CurrentPosition, corpse.Position);
			if (path.Count == 0) return false;
			await session.ExecuteMovementAsync(new BotMover(world, session.Api.Timing).CreateGroundPlan(path,
				session.CurrentPosition, world.MovementSpeed!.Value), token);
		}
		session.BeginStep("group-loot", "loot-granted-corpse");
		await session.SendPacketAsync(session.Api.Loot(corpse.ObjectId), token);
		var list = await session.WaitForPacketWithinAsync(typeof(SM_LOOT_ITEMLIST), TimeSpan.FromSeconds(1), token,
			packet => packet.Get<int>("targetObjectId") == corpse.ObjectId);
		if (list == null) return false; // Another eligible member may be using the window; never close theirs.
		var items = world.Loot!.Items.ToArray();
		int took = 0;
		try
		{
			foreach (var item in items)
			{
				var template = runtime.Data.ItemDataDh.GetItemTemplate(item.ItemId);
				if (template == null || template.itemGroup == ItemGroup.QUEST && !questItemNeeded(item.ItemId) ||
					template.HasLimitOne() && world.Inventory.Values.Any(owned => owned.ItemId == item.ItemId)) continue;
				long before = item.ItemId == 182400001 ? world.Kinah : world.Inventory.Values.Where(owned => owned.ItemId == item.ItemId).Sum(owned => owned.Count);
				await session.SendPacketAsync(session.Api.Loot(corpse.ObjectId, item.Index), token);
				await session.SynchronizeAsync(token);
				long after = item.ItemId == 182400001 ? world.Kinah : world.Inventory.Values.Where(owned => owned.ItemId == item.ItemId).Sum(owned => owned.Count);
				if (after > before) { Collected++; took++; }
				session.TraceDiagnostic("group-loot-item", new Dictionary<string, object?>
				{ ["corpse"] = corpse.ObjectId, ["item"] = item.ItemId, ["countBefore"] = before, ["countAfter"] = after });
			}
		}
		finally { await session.SendPacketAsync(session.Api.Loot(corpse.ObjectId, close: true), token); }
		visited.Add(corpse.ObjectId);
		session.TraceDiagnostic("group-loot-closed", new Dictionary<string, object?> { ["corpse"] = corpse.ObjectId, ["visibleItems"] = items.Length, ["collected"] = took });
		return true;
	}
}
