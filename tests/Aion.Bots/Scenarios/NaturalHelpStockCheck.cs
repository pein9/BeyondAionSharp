using Aion.Bots.Scenarios.Classes;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

/// <summary>Recurring checks of the approved kit between actions, including recovery inside a long retry.</summary>
public sealed class NaturalHelpStockCheck(INaturalJourneySession session, NaturalHelpItemRules help,
	Func<bool> enabled, Func<bool> quiet, Func<string, Func<bool>, Task> topUp)
{
	private bool checking;

	private bool Safe()
	{
		var world = session.Api.World;
		if (!enabled() || world.Level <= NaturalHelpItemAllowlist.StarterMaxLevel ||
			!help.Supplied(world.Level) || world.IsDead || world.CurrentHp <= 0 ||
			session.Api.Timing.BlockingActivities.Count != 0 || !quiet()) return false;
		// Retry sessions can retain hundreds of thousands of packets. Read backward without copying that history.
		for (int index = session.PacketHistory.Count - 1; index >= 0; index--)
		{
			var packet = session.PacketHistory[index];
			if (packet.PacketType == typeof(SM_EMOTION) && packet.Get<int>("senderObjectId") == session.CharacterId)
				return packet.Fields.ContainsKey("state") &&
					(packet.Get<ushort>("state") & (int)(CreatureState.FLYING | CreatureState.GLIDING)) == 0;
		}
		return true;
	}

	public async Task CheckAsync(NaturalHelpTrigger trigger, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		if (checking || !Safe()) return;
		var world = session.Api.World;
		var owned = world.Inventory.Values.GroupBy(item => item.ItemId)
			.ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
		if (NaturalHelpItemSupply.Plan(world.Level, owned, help.Kit).Count == 0) return;
		checking = true;
		try { await topUp($"safe-{trigger}", Safe); }
		finally { checking = false; }
	}
}
