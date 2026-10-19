using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_LEARN_RECIPE (lord_rex). Confirms a learned recipe (recipeId).</summary>
public class SM_LEARN_RECIPE : AionServerPacket
{
    private readonly int recipeId;
    private readonly int productionLimit;

    public SM_LEARN_RECIPE(int recipeId, int productionLimit)
    {
        this.recipeId = recipeId;
        this.productionLimit = productionLimit;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(recipeId);
        WriteC(productionLimit); // Remaining production count; zero is unlimited.
    }
}
