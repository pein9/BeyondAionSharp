using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_PET_EMOTE (ATracer). Pet movement/emotion packet. PetEmote ported PascalCase (PetEmoteResolver.GetEmoteById). SM_PET_EMOTE/World red-tolerated.</summary>
public class CM_PET_EMOTE : AionClientPacket
{
    private const float MAX_DISTANCE_TO_MASTER = 50;
    private const int ACTION_FLAG = 0x80, TARGET_FLAG = 0x04;
    private PetEmote emote;
    private float x1, y1, z1, x2, y2, z2;
    private byte h;
    private int emoteId;
    private int actionParam;

    public CM_PET_EMOTE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        emoteId = ReadUC();
        emote = PetEmoteResolver.GetEmoteById(emoteId);
        if ((emoteId & ACTION_FLAG) != 0)
        {
            actionParam = ReadUH();
        }
        else
        {
            x1 = ReadF();
            y1 = ReadF();
            z1 = ReadF();
            h = ReadC();
            if ((emoteId & TARGET_FLAG) != 0)
            {
                x2 = ReadF();
                y2 = ReadF();
                z2 = ReadF();
            }
        }
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        Pet pet = player.GetPet();

        if (pet == null || !pet.IsSpawned()) // client sometimes just doesn't care...
            return;
        if (emote == PetEmote.Unknown)
        {
            AionLog.For(GetType().Name).LogWarning("{Player} / {Pet} sent pet emote {EmoteId} (actionParam: {ActionParam})", player, pet, emoteId, actionParam);
            return;
        }

        if ((emoteId & ACTION_FLAG) == 0 && !PositionUtil.IsInRange(player, x1, y1, z1, MAX_DISTANCE_TO_MASTER))
            return;

        switch (emote)
        {
            case PetEmote.MoveStop:
            case PetEmote.MovePositionUpdate:
                global::Aion.GameServer.World.World.GetInstance().UpdatePosition(pet, x1, y1, z1, h);
                BroadcastToSightedPlayers(pet, new SM_PET_EMOTE(pet, emote), false);
                break;
            case PetEmote.MoveTo:
                global::Aion.GameServer.World.World.GetInstance().UpdatePosition(pet, x1, y1, z1, h);
                pet.GetMoveController().SetNewDirection(x2, y2, z2, h);
                BroadcastToSightedPlayers(pet, new SM_PET_EMOTE(pet, emote), false);
                break;
            default:
                BroadcastToSightedPlayers(pet, new SM_PET_EMOTE(pet, emote, actionParam), emote == PetEmote.Emotion);
                break;
        }
    }

    private void BroadcastToSightedPlayers(Pet pet, AionServerPacket packet, bool withMaster)
    {
        PacketSendUtility.BroadcastPacket(pet, packet, false, other => (withMaster || !other.Equals(pet.GetMaster())) && other.GetKnownList().Sees(pet));
    }
}
