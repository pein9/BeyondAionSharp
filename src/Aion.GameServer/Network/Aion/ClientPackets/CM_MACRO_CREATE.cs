using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Players;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_MACRO_CREATE (SoulKeeper). Validates the configured macro slot limit and replaces the client list on rejection. PlayerService/SM_MACRO_RESULT red-tolerated.</summary>
public class CM_MACRO_CREATE : AionClientPacket
{
    /// <summary>Macro number. Fist is 1, second is 2. Starting from 1, not from 0</summary>
    private int macroPosition;

    /// <summary>XML that represents the macro</summary>
    private string macroXML;

    public CM_MACRO_CREATE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        macroPosition = ReadUC();
        macroXML = ReadS();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (Macros.IsValidId(macroPosition))
        {
            PlayerService.AddMacro(player, macroPosition, macroXML);
            SendPacket(SM_MACRO_RESULT.SM_MACRO_CREATED);
        }
        else
        {
            // The client drops its rejected entry only when the complete list is replaced.
            SendPacket(SM_SYSTEM_MESSAGE.STR_MACRO_MSG_CANNOT_REGIST());
            PlayerService.SendMacroList(player);
        }
    }
}
