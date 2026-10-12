using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;

namespace Aion.Bots.World;

public sealed partial class BotWorldModel
{
	private void ApplyGroupHealth(DecodedBotServerPacket packet)
	{
		foreach (var health in packet.Health ?? []) Update(health.ObjectId, health.Percent);
		if (BotAttackStatus.CarriesHp(packet)) Update(packet.Get<int>("objectId"), packet.Get<byte>("hpOrMp"));
		void Update(int id, byte percent)
		{
			if (groupMembers.TryGetValue(id, out BotGroupMember? member) && member.Online && ObservationTime >= member.HealthObservedAt)
				groupMembers[id] = member with { SightedHpPercent = percent, HealthObservedAt = ObservationTime };
		}
	}

	private void ApplyGroupEffects(DecodedBotServerPacket packet)
	{
		if (!packet.Fields.ContainsKey("effects")) return;
		ApplyEffectSnapshot(packet.Get<int>("objectId"), packet.Get<byte>("effectSlots"),
			packet.Get<List<IReadOnlyDictionary<string, object?>>>("effects").Select(row => new BotVisibleEffect(
				(int)row["effectorId"]!, (ushort)row["skillId"]!, (byte)row["level"]!,
				(byte)row["slotOrdinal"]!, (int)row["remainingMillis"]!)));
	}
}
