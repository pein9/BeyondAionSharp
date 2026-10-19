using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.World;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Drop;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_START_LOOT (alexa026, Metos, ATracer). Opens (0) or closes (1) a corpse drop list. DropService red-tolerated.</summary>
public class CM_START_LOOT : AionClientPacket
{
    private static readonly ILogger log = AionLog.For(nameof(CM_START_LOOT));
    private const float LOOT_DISTANCE = 6.5f;
    private const float MAX_HEIGHT_ABOVE_PLAYER = 50f;
    private const float RELIQUARY_LOOT_DISTANCE = 30f;
    private static readonly HashSet<int> RELIQUARY_MAPS = [WorldMapType.DANUAR_RELIQUARY.GetId(), WorldMapType.LUCKY_DANUAR_RELIQUARY.GetId(), WorldMapType.INFERNAL_DANUAR_RELIQUARY.GetId()];
    private int targetObjectId;
    private Action? action;

    public CM_START_LOOT(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        targetObjectId = ReadD();
        int actionId = ReadUC();
        action = actionId switch { 0 => Action.OPEN, 1 => Action.CLOSE, _ => null };
        if (action == null)
            log.LogWarning("{Player} sent unknown loot action type {Action}", GetConnection().GetActivePlayer(), actionId);
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (action == null)
            return;
        switch (action.Value)
        {
            case Action.OPEN:
                if (player.GetKnownList().GetObject(targetObjectId) is not Npc npc || !IsInLootRange(player, npc))
                {
                    GetConnection().SendPacket(SM_SYSTEM_MESSAGE.STR_LOOT_FAIL_TOO_FAR());
                    return;
                }
                DropService.GetInstance().RequestDropList(player, targetObjectId);
                break;
            case Action.CLOSE:
                DropService.GetInstance().CloseDropList(player, targetObjectId);
                break;
        }
    }

    private bool IsInLootRange(Player player, Npc npc)
    {
        if (player.GetWorldId() != npc.GetWorldId() || player.GetInstanceId() != npc.GetInstanceId())
            return false;
        float range = LOOT_DISTANCE + player.GetObjectTemplate().GetBoundRadius().GetMaxOfFrontAndSide()
            + npc.GetObjectTemplate().GetBoundRadius().GetMaxOfFrontAndSide();
        float dx = npc.GetX() - player.GetX();
        float dy = npc.GetY() - player.GetY();
        float dz = npc.GetZ() - player.GetZ();
        float horizontal = dx * dx + dy * dy;
        if (horizontal + dz * dz < range * range)
            return true;
        if (dz > 0 && dz < MAX_HEIGHT_ABOVE_PLAYER && horizontal < range * range)
            return true;
        return RELIQUARY_MAPS.Contains(player.GetWorldId()) && horizontal < RELIQUARY_LOOT_DISTANCE * RELIQUARY_LOOT_DISTANCE;
    }

    private enum Action { OPEN = 0, CLOSE = 1 }
}
