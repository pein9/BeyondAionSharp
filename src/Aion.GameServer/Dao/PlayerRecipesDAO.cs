using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Dao;

/// <summary>Player recipes and persisted remaining productions, through the native DB callbacks.</summary>
public class PlayerRecipesDAO
{
    private const string SELECT_QUERY = "SELECT `recipe_id`, `production_limit` FROM player_recipes WHERE `player_id`=?";
    private const string ADD_QUERY = "INSERT INTO player_recipes (`player_id`, `recipe_id`, `production_limit`) VALUES (?, ?, ?)";
    private const string UPDATE_QUERY = "UPDATE player_recipes SET `production_limit`=? WHERE `player_id`=? AND `recipe_id`=?";
    private const string DELETE_QUERY = "DELETE FROM player_recipes WHERE `player_id`=? AND `recipe_id`=?";

    public static RecipeList Load(int playerId)
    {
        var list = new RecipeList(); DB.Select(SELECT_QUERY, new LoadHandler(playerId, list)); return list;
    }
    private sealed class LoadHandler(int playerId, RecipeList list) : ParamReadStH
    {
        public void SetParams(MySqlCommand ps) => ps.Parameters.Add(new MySqlParameter {Value = playerId});
        public void HandleRead(MySqlDataReader rs)
        {
            while (rs.Read())
            {
                int id = rs.GetInt32(rs.GetOrdinal("recipe_id"));
                int count = rs.GetInt32(rs.GetOrdinal("production_limit"));
                if (count == 0) count = DataManager.RECIPE_DATA.GetMaxProductionCount(id);
                list.AddRecipe(id, count);
            }
        }
    }
    public static bool AddRecipe(int playerId, int recipeId, int productionLimit) => DB.InsertUpdate(ADD_QUERY, new WriteHandler(playerId, recipeId, productionLimit));
    public static bool UpdateProductionLimit(int playerId, int recipeId, int productionLimit) => DB.InsertUpdate(UPDATE_QUERY, new WriteHandler(productionLimit, playerId, recipeId));
    public static bool DelRecipe(int playerId, int recipeId) => DB.InsertUpdate(DELETE_QUERY, new WriteHandler(playerId, recipeId));
    private sealed class WriteHandler(params int[] values) : IUStH
    {
        public void HandleInsertUpdate(MySqlCommand ps)
        {
            foreach (int value in values) ps.Parameters.Add(new MySqlParameter {Value = value});
            ps.ExecuteNonQuery();
        }
    }
}
