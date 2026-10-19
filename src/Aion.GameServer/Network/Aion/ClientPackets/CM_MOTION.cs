using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players.Motion;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_MOTION (MrPoke). Sets a motion active/inactive. Motions red-tolerated.</summary>
public class CM_MOTION : AionClientPacket
{
    private const int ACTIVATE = 4;
    private int request;
    private int motionId;
    private MotionType? motionType;

    public CM_MOTION(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        request = ReadUC();
        motionId = ReadUH();
        motionType = MotionTypeExtensions.GetById(ReadUC());
    }

    protected override void RunImpl()
    {
        if (request != ACTIVATE || motionType == null)
            return;
        Player player = GetConnection().GetActivePlayer();
        player.GetMotions().SetActive(motionId, motionType.Value);
    }
}
