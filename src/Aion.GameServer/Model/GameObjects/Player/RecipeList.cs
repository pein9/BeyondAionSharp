using System.Collections.Generic;
using System.Collections.ObjectModel;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>Java parity: model/gameobjects/player/RecipeList.</summary>
public class RecipeList
{
    // Recipe id -> remaining productions. Zero means unlimited.
    private readonly Dictionary<int, int> productionLimitByRecipeId = new();

    public IReadOnlyDictionary<int, int> GetRecipes()
    {
        lock (this) return new ReadOnlyDictionary<int, int>(new Dictionary<int, int>(productionLimitByRecipeId));
    }

    public bool AddRecipe(Player player, int recipeId) => AddRecipe(player, recipeId, DataManager.RECIPE_DATA.GetMaxProductionCount(recipeId));
    public bool AddRecipe(int recipeId, int productionLimit) => AddRecipe(null, recipeId, productionLimit);

    public bool AddRecipe(Player? player, int recipeId, int productionLimit)
    {
        lock (this)
        {
            if (!productionLimitByRecipeId.TryAdd(recipeId, productionLimit)) return false;
            if (player != null)
            {
                if (!PlayerRecipesDAO.AddRecipe(player.GetObjectId(), recipeId, productionLimit))
                {
                    productionLimitByRecipeId.Remove(recipeId); return false;
                }
                PacketSendUtility.SendPacket(player, new SM_LEARN_RECIPE(recipeId, productionLimit));
            }
            return true;
        }
    }

    public bool DeleteRecipe(Player player, int recipeId)
    {
        lock (this)
        {
            if (productionLimitByRecipeId.ContainsKey(recipeId) && PlayerRecipesDAO.DelRecipe(player.GetObjectId(), recipeId))
            {
                productionLimitByRecipeId.Remove(recipeId);
                PacketSendUtility.SendPacket(player, new SM_RECIPE_DELETE(recipeId)); return true;
            }
            return false;
        }
    }

    public void DecreaseProductionLimit(Player player, int recipeId)
    {
        lock (this)
        {
            if (!productionLimitByRecipeId.TryGetValue(recipeId, out int count) || count == 0) return;
            count = unchecked(count - 1);
            if (count > 0)
            {
                productionLimitByRecipeId[recipeId] = count;
                PlayerRecipesDAO.UpdateProductionLimit(player.GetObjectId(), recipeId, count);
            }
            else
            {
                productionLimitByRecipeId.Remove(recipeId);
                PlayerRecipesDAO.DelRecipe(player.GetObjectId(), recipeId);
                PacketSendUtility.SendPacket(player, new SM_RECIPE_DELETE(recipeId));
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_COMBINE_USAGE_OVER(ChatUtil.RecipeName(recipeId, player.GetName())));
            }
        }
    }

    public bool IsRecipePresent(int recipeId)
    {
        lock (this) return productionLimitByRecipeId.ContainsKey(recipeId);
    }

    public int Size()
    {
        lock (this) return productionLimitByRecipeId.Count;
    }
}
