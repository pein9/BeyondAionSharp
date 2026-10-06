using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>
/// D38 (deviation 161): no Java counterpart. Java gives the Triniel Underground Arena (Q2947 "Following Through") no handler,
/// so GeneralInstanceHandler.allowInstanceRevive() is false and a player who dies there revives at the bind point. Retail
/// revived the player inside: NCSoft's world file for the arena (Worlds/iddc1_arena/world.xml) carries five
/// dead_start_at_default points just inside the entry, and Java's own instance handlers revive at their worlds' points
/// (TalocsHollowInstance to the same x and y). With a handler class the instance revive is offered, as in every Java
/// instance that has one. The point is the first of NCSoft's five, heading 270 degrees.
/// </summary>
[InstanceID(320090000)]
public class TrinielUndergroundArenaInstance : GeneralInstanceHandler
{
    public TrinielUndergroundArenaInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override bool OnReviveEvent(Player player)
    {
        PlayerReviveService.Revive(player, 25, 25, true, 0);
        player.GetGameStats().UpdateStatsAndSpeedVisually();
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REBIRTH_MASSAGE_ME());
        TeleportService.TeleportTo(player, instance, 277.86734f, 289.877625f, 164.1f, (byte)90);
        return true;
    }
}
