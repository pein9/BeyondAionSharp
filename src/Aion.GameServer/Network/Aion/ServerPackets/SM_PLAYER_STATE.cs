using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_PLAYER_STATE (Luno, Sweetkr). Sends a creature's visual/see state (e.g. stop login blinking). Creature/CreatureVisualState red-tolerated.</summary>
public class SM_PLAYER_STATE : AionServerPacket
{
    private int playerObjId;
    private int visualState;
    private int seeState;
    private bool redrawSurroundings;

    public SM_PLAYER_STATE(Creature creature) : this(creature, false) { }

    /// <summary>Redraw surroundings only when explicitly requested for the receiving player's own spawn state.</summary>
    public SM_PLAYER_STATE(Creature creature, bool redrawSurroundings)
    {
        this.playerObjId = creature.GetObjectId();
        this.visualState = creature.GetVisualState();
        this.seeState = creature.GetSeeState();
        this.redrawSurroundings = redrawSurroundings;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(playerObjId);
        WriteC(visualState);
        WriteC(seeState);
        WriteC(redrawSurroundings ? 1 : 0);
    }
}
