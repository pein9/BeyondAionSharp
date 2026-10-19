using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_MAY_QUIT (xavier). Sent by client when starting its ten-second quit countdown.</summary>
public class CM_MAY_QUIT : AionClientPacket
{
    public CM_MAY_QUIT(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        // empty
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player != null)
            player.SetQuitCountdownStartMillis(SystemClock.CurrentMillis());
    }
}
