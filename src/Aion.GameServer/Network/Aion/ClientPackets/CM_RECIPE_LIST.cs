using System.Collections.Generic;
using Aion.GameServer.Network.Aion.ServerPackets;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public class CM_RECIPE_LIST : AionClientPacket
{
    public CM_RECIPE_LIST(int opcode, ISet<State> validStates) : base(opcode, validStates) { }
    protected override void ReadImpl() { }
    protected override void RunImpl()
    {
        var player = GetConnection().GetActivePlayer();
        if (player == null) return;
        SendPacket(new SM_RECIPE_LIST(player.GetRecipeList().GetRecipes()));
    }
}
