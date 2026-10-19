using System;
using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_MACRO_LIST (-Nemesiss-). Sends the player's macro list (paged), optionally clearing first. Converges PlayerEnterWorldService SplitList paging (STATIC_BODY_SIZE + DYNAMIC_BODY_PART_SIZE_CALCULATOR). Function->Func; Macros.Macro record accessors xml()/id()->Xml()/Id(); Signed count marks the final partition. Macros/AionServerPacket red-tolerated.</summary>
public class SM_MACRO_LIST : AionServerPacket
{
    public const int STATIC_BODY_SIZE = 7;
    public static readonly Func<Macros.Macro, int> DYNAMIC_BODY_PART_SIZE_CALCULATOR = macro => 1 + macro.Xml.Length * 2 + 2;

    private readonly int playerObjectId;
    private readonly List<Macros.Macro> macros;
    private readonly bool clearList;
    private readonly bool isLast;

    public SM_MACRO_LIST(int playerObjectId, List<Macros.Macro> macros, bool clearList, bool isLast)
    {
        this.playerObjectId = playerObjectId;
        this.macros = macros;
        this.clearList = clearList;
        this.isLast = isLast;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(playerObjectId);
        WriteC(clearList ? 1 : 0); // 1 = clears all entries in the macro list before adding the ones sent here
        WriteH(isLast ? -macros.Count : macros.Count); // negative marks the last part
        foreach (Macros.Macro macro in macros)
        {
            WriteC(macro.Id);
            WriteS(macro.Xml);
        }
    }
}
