using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_QUIT_RESPONSE (-Nemesiss-). Response to CM_QUIT (1 normal, 2 plastic surgery/gender switch). Converges AntiHackService/PlayerTransfer disconnect. AionServerPacket red-tolerated.</summary>
public class SM_QUIT_RESPONSE : AionServerPacket
{
    private readonly int result;
    private readonly int value;

    private SM_QUIT_RESPONSE(int result, int value)
    {
        this.result = result;
        this.value = value;
    }

    public static SM_QUIT_RESPONSE Quit() => new(1, 0);
    public static SM_QUIT_RESPONSE QuitToEditScreen() => new(2, 0);
    /// <summary>Seconds until out of combat, or milliseconds elapsed for an invalid countdown.</summary>
    public static SM_QUIT_RESPONSE Refused(int value) => new(0, value);

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(result); // 0 refused, 1 quit, 2 plastic surgery/gender switch
        WriteD(value);
        WriteD(-1); // home server, -1 for its own server
    }
}
