using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.World.Zone;
using Aion.GameServer.World.Zone.Handler;

namespace Aion.GameServer.Handlers.Zone.PvpZones;

/// <summary>Java PvPAreaZone: handles the capital PvP areas by their exact zone names.</summary>
[ZoneHandlerArea("LC1_PVP_SUB_C_110010000", "DC1_PVP_ZONE_120010000")]
public class PvPAreaZone : PvPZone
{
    public override void OnEnterZone(Creature creature, ZoneInstance zone)
    {
        if (creature is Player player && CustomConfig.KEEP_BUFFS_IN_COLISEUM)
            ((PlayerEffectController)player.GetEffectController()).SetKeepBuffsOnDie(true);
    }

    public override void OnLeaveZone(Creature creature, ZoneInstance zone)
    {
        if (creature is Player player)
            ((PlayerEffectController)player.GetEffectController()).SetKeepBuffsOnDie(false);
    }

    protected override void DoTeleport(Player player, ZoneInstance zone)
    {
        if (zone.Matches("LC1_PVP_SUB_C_110010000"))
            TeleportService.TeleportTo(player, 110010000, 1, 1470.3f, 1343.5f, 563.7f);
        else if (zone.Matches("DC1_PVP_ZONE_120010000"))
            TeleportService.TeleportTo(player, 120010000, 1, 1005.1f, 1528.9f, 222.1f);
    }
}
