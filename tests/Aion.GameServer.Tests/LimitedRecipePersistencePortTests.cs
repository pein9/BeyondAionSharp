using System.Reflection;
using System.Text.RegularExpressions;
using Aion.Commons.Database;
using Aion.GameServer.Dao;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using MySqlConnector;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class LimitedRecipePersistencePortTests
{
    [SkippableFact]
    public void ActualMigrationAndRecipeDaoPreserveLegacyCountsRemainingUsesAndFailureRollback()
    {
        string? testConnection = Environment.GetEnvironmentVariable("AION_RECIPE_PORT_TEST_CONNECTION");
        Skip.If(string.IsNullOrWhiteSpace(testConnection), "Set AION_RECIPE_PORT_TEST_CONNECTION to a disposable MySQL test server.");
        using var scope = new LimitedRecipeMorphPortTests.Scope();
        var builder = new MySqlConnectionStringBuilder(testConnection) {Database = ""};
        using var admin = new MySqlConnection(builder.ConnectionString); admin.Open();
        string dbName = "recipe_port_" + Guid.NewGuid().ToString("N");
        Execute(admin, "CREATE DATABASE `" + dbName + "`");
        try
        {
            builder.Database = dbName; using var connection = new MySqlConnection(builder.ConnectionString); connection.Open();
            string ddl = File.ReadAllText(Path.Combine(PvpExpPortTests.Root(), "game-server/sql/aion_gs.sql"));
            string Table(string name) => Regex.Match(ddl, "CREATE TABLE `" + name + @"` \([\s\S]*?\n\)[^;]*;").Value;
            string players = Table("players"), recipes = Table("player_recipes"); Assert.NotEmpty(players); Assert.NotEmpty(recipes);
            Execute(connection, Regex.Replace(players, @"(?m)^\s*`pvp_exp`[^\r\n]*\r?\n", ""));
            Execute(connection, Regex.Replace(recipes, @"(?m)^\s*`production_limit`[^\r\n]*\r?\n", ""));
            Execute(connection, "CREATE TABLE player_skills (skill_id int, skill_level int)");
            Execute(connection, "CREATE TABLE player_appearance (jaw_height int NOT NULL, chin_jut int NOT NULL)");
            Execute(connection, "INSERT INTO player_skills VALUES(29999,5),(30000,7),(40009,300)");
            Execute(connection, File.ReadAllText(Path.Combine(PvpExpPortTests.Root(), "game-server/sql/update.sql")));
            Assert.Equal(1L, Convert.ToInt64(Scalar(connection, "SELECT skill_level FROM player_skills WHERE skill_id=29999")));
            Assert.Equal(1L, Convert.ToInt64(Scalar(connection, "SELECT skill_level FROM player_skills WHERE skill_id=40009")));
            Assert.Equal(7L, Convert.ToInt64(Scalar(connection, "SELECT skill_level FROM player_skills WHERE skill_id=30000")));
            int id = scope.Player.GetObjectId();
            using (var seed = new MySqlCommand("INSERT INTO players(id,name,account_id,account_name,x,y,z,heading,world_id,gender,race,player_class) VALUES(@id,'RecipeFixture',1,'fixture',1,2,3,4,300100000,'MALE','ELYOS','GLADIATOR')", connection))
            {seed.Parameters.AddWithValue("@id", id); seed.ExecuteNonQuery();}
            Execute(connection, $"INSERT INTO player_recipes(player_id,recipe_id) VALUES({id},9001),({id},9002),({id},9999)");
            Assert.Equal(0L, Convert.ToInt64(Scalar(connection, "SELECT production_limit FROM player_recipes WHERE recipe_id=9001")));
            using var dataSource = new MySqlDataSourceBuilder(builder.ConnectionString).Build();
            var source = typeof(DatabaseFactory).GetField("_dataSource", BindingFlags.NonPublic | BindingFlags.Static)!;
            object? previous = source.GetValue(null); source.SetValue(null, dataSource);
            try
            {
                var list = PlayerRecipesDAO.Load(id); Assert.Equal(3, list.GetRecipes()[9001]);
                Assert.Equal(0, list.GetRecipes()[9002]); Assert.Equal(0, list.GetRecipes()[9999]);
                scope.Player.SetRecipeList(list);
                list.DecreaseProductionLimit(scope.Player, 9001);
                Assert.Equal(2, PlayerRecipesDAO.Load(id).GetRecipes()[9001]);
                list.DecreaseProductionLimit(scope.Player, 9001); Assert.Equal(1, PlayerRecipesDAO.Load(id).GetRecipes()[9001]);
                list.DecreaseProductionLimit(scope.Player, 9001); Assert.False(PlayerRecipesDAO.Load(id).IsRecipePresent(9001));
                scope.Connection.Clear(); Assert.True(list.AddRecipe(scope.Player, 9001));
                Assert.Equal(3, PlayerRecipesDAO.Load(id).GetRecipes()[9001]);
                Assert.Equal(3, LimitedRecipeMorphPortTests.Payload(Assert.Single(scope.Connection.Packets.OfType<SM_LEARN_RECIPE>()))[4]);
                Assert.True(list.DeleteRecipe(scope.Player, 9001)); Assert.False(PlayerRecipesDAO.Load(id).IsRecipePresent(9001));
                scope.Connection.Clear(); var empty = new RecipeList();
                Assert.False(empty.AddRecipe(scope.Player, 9002)); Assert.False(empty.IsRecipePresent(9002)); Assert.Empty(scope.Connection.Packets);
                Assert.True(PlayerRecipesDAO.UpdateProductionLimit(id, 9002, -1));
                Assert.Equal(-1, PlayerRecipesDAO.Load(id).GetRecipes()[9002]); // SQL tinyint is signed, JDBC getInt preserves it.
                Assert.False(PlayerRecipesDAO.UpdateProductionLimit(id, 9002, 128));
                Assert.Equal(-1, PlayerRecipesDAO.Load(id).GetRecipes()[9002]);
                Execute(connection, "DROP TABLE player_recipes"); Execute(connection, recipes);
                Assert.Equal("tinyint", Convert.ToString(Scalar(connection, "SELECT DATA_TYPE FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='player_recipes' AND COLUMN_NAME='production_limit'")));
            }
            finally {source.SetValue(null, previous);}
        }
        finally {Execute(admin, "DROP DATABASE `" + dbName + "`");}
    }
    private static void Execute(MySqlConnection connection, string sql)
    {using var cmd = new MySqlCommand(sql, connection); cmd.ExecuteNonQuery();}
    private static object? Scalar(MySqlConnection connection, string sql)
    {using var cmd = new MySqlCommand(sql, connection); return cmd.ExecuteScalar();}
}
