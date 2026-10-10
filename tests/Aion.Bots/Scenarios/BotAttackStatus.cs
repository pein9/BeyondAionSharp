using Aion.Bots.Protocol;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

/// <summary>
/// NR-121a: what an attack status says of its creature. Java SM_ATTACK_STATUS.writeImpl 121-157 writes one percentage
/// with every status: the creature's MP for the types HEAL_MP (19), DAMAGE_MP and ABSORBED_MP (20), MP (21), NATURAL_MP
/// (22) and USED_MP (23), and its HP for every other type. A monster has no mana, so a skill that takes mana from it
/// (Java MpAttackInstantEffect.applyEffect 36-39, DAMAGE_MP) says 0% of a monster that lives.
/// </summary>
public static class BotAttackStatus
{
	/// <summary>The packet is an attack status whose percentage is its creature's HP.</summary>
	public static bool CarriesHp(DecodedBotServerPacket packet) =>
		packet.PacketType == typeof(SmAttackStatus) && packet.Get<byte>("typeId") is not (19 or 20 or 21 or 22 or 23);
}
