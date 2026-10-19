using System.Collections.Generic;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Recipe snapshot requested by the client: recipe id and remaining production count (zero is unlimited).</summary>
public class SM_RECIPE_LIST : AionServerPacket
{
    private readonly IReadOnlyDictionary<int, int> recipes;

    public SM_RECIPE_LIST(IReadOnlyDictionary<int, int> recipes)
    {
        this.recipes = recipes;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteH(recipes.Count);
        foreach (var recipe in recipes)
        {
            WriteD(recipe.Key);
            WriteC(recipe.Value);
        }
    }
}
