namespace Aion.Bots.World;

/// <summary>Packet-derived cube expansions; the 4.8 cube starts at 27 slots and adds nine per expansion.</summary>
public sealed record BotCubeExpansion(byte Npc, byte Quest, byte Item)
{
	public int Capacity => 27 + 9 * (Npc + Quest + Item);
}
