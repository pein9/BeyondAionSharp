using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_EXCHANGE_CONFIRMATION (-Avol-). Sends an exchange confirmation/cancel action byte.</summary>
public class SM_EXCHANGE_CONFIRMATION : AionServerPacket
{
    private readonly Action action;

    public SM_EXCHANGE_CONFIRMATION(Action action)
    {
        this.action = action;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC((int)action);
    }

    public enum Action
    {
        COMPLETED = 0,
        CANCELED = 1,
        PARTNER_CONFIRMED = 2,
        PARTNER_LOCKED = 3
    }
}
