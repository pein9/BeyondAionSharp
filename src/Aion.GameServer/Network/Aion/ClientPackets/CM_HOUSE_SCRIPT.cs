using Aion.GameServer.Model.GameObjects;
using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_HOUSE_SCRIPT (Rolandas, Neon, Sykra). Uploads/removes a house script (sign script) with size limits. PlayerScripts/SM_HOUSE_SCRIPTS red-tolerated.</summary>
public class CM_HOUSE_SCRIPT : AionClientPacket
{
    private const int MAX_SCRIPT_BLOCK_SIZE = 3072;
    private int address;
    private int scriptId;
    private int totalSize;
    private int compressedSize;
    private int uncompressedSize;
    private byte[] scriptContent;

    public CM_HOUSE_SCRIPT(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        address = ReadD();
        scriptId = ReadUC();
        totalSize = ReadUH();
        if (totalSize > 0 && totalSize <= MAX_SCRIPT_BLOCK_SIZE)
        {
            compressedSize = ReadD();
            uncompressedSize = ReadD();
            if (compressedSize >= 0 && compressedSize <= totalSize - 8)
                scriptContent = ReadB(compressedSize);
        }
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (totalSize > MAX_SCRIPT_BLOCK_SIZE)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_SCRIPT_OVERFLOW());
            return;
        }
        if (totalSize > 0 && scriptContent == null)
        {
            AuditLogger.Log(player, "sent a house script of " + compressedSize + " bytes in a block of " + totalSize + " bytes");
            return;
        }
        House house = player.GetActiveHouse();
        if (house == null || house.GetAddress().GetId() != address)
        {
            AuditLogger.Log(player, "tried to modify script of house they don't own (address " + address + ")");
            return;
        }
        if (!house.IsOnLand(player))
            return;
        PlayerScripts scripts = house.GetPlayerScripts();
        if (totalSize == 0)
            scripts.Remove(scriptId);
        else
            scripts.Set(scriptId, scriptContent, uncompressedSize);
        PacketSendUtility.BroadcastPacket(player, new SM_HOUSE_SCRIPTS(address, scripts.Get(scriptId)));
    }
}
