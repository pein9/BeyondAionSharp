using System.Reflection;
using System.Text.RegularExpressions;
using Aion.Commons.Database;
using Aion.GameServer.Dao;
using Aion.GameServer.Data;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects.Players;
using MySqlConnector;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class CharacterAppearancePersistencePortTests
{
    // Explicit opt-in: all writes are confined to an owned random database on a disposable test server.
    [SkippableFact]
    public void ActualMigrationDaoAndCharacterSelectionPreserveAllAppearanceValuesAndLegacyHeight()
    {
        string? testConnection = Environment.GetEnvironmentVariable("AION_APPEARANCE_PORT_TEST_CONNECTION");
        Skip.If(string.IsNullOrWhiteSpace(testConnection), "Set AION_APPEARANCE_PORT_TEST_CONNECTION to a disposable MySQL test server.");
        using var scope = new PvpExpPortTests.Scope(); var player = scope.Player(65); int id = player.GetObjectId();
        string dbName = "appearance_port_" + Guid.NewGuid().ToString("N");
        var builder = new MySqlConnectionStringBuilder(testConnection) { Database = "" };
        using var admin = new MySqlConnection(builder.ConnectionString); admin.Open(); Execute(admin, "CREATE DATABASE `" + dbName + "`");
        try
        {
            builder.Database = dbName; using var connection = new MySqlConnection(builder.ConnectionString); connection.Open();
            string ddl = File.ReadAllText(Path.Combine(PvpExpPortTests.Root(), "game-server/sql/aion_gs.sql"));
            string Table(string name) => Regex.Match(ddl, "CREATE TABLE `" + name + @"` \([\s\S]*?\n\)[^;]*;").Value;
            string appearance = Table("player_appearance"), players = Table("players"); Assert.NotEmpty(appearance); Assert.NotEmpty(players);
            Execute(connection, Regex.Replace(players, @"(?m)^\s*`pvp_exp`[^\r\n]*\r?\n", ""));
            Execute(connection, "CREATE TABLE player_recipes (player_id int, recipe_id int)");
            Execute(connection, "CREATE TABLE player_skills (skill_id int, skill_level int)");
            Execute(connection, "INSERT INTO player_skills VALUES(29999,5),(30000,7),(40009,300)");
            Execute(connection, appearance.Replace("`chin_height`", "`jaw_height`").Replace("`cheek_bones`", "`chin_jut`"));
            using (var seed = new MySqlCommand("INSERT INTO players(id,name,account_id,account_name,x,y,z,heading,world_id,gender,race,player_class) VALUES(@id,'AppearanceFixture',1,'fixture',1,2,3,4,300100000,'MALE','ELYOS','GLADIATOR')", connection))
            { seed.Parameters.AddWithValue("@id", id); seed.ExecuteNonQuery(); }
            string[] columns = Regex.Matches(appearance, @"(?m)^\s*`(\w+)` ").Select(m => m.Groups[1].Value).ToArray();
            Assert.Equal(54, columns.Length);
            var values = columns.Select((column, index) => column == "player_id" ? (object)id : column == "height" ? (object)0.49f : index * 3 + 7).ToArray();
            values[Array.IndexOf(columns, "chin_height")] = 65537; values[Array.IndexOf(columns, "cheek_bones")] = -1234;
            using (var seed = new MySqlCommand("INSERT INTO player_appearance VALUES(" + string.Join(',', columns.Select((_, i) => "@p" + i)) + ")", connection))
            { for (int i = 0; i < values.Length; i++) seed.Parameters.AddWithValue("@p" + i, values[i]); seed.ExecuteNonQuery(); }
            Execute(connection, "CREATE TABLE house_scripts (house_id int, script_id tinyint, script mediumtext NOT NULL)");
            Execute(connection, File.ReadAllText(Path.Combine(PvpExpPortTests.Root(), "game-server/sql/update.sql")));
            Assert.Equal(1L, Convert.ToInt64(Scalar(connection, "SELECT skill_level FROM player_skills WHERE skill_id=29999")));
            Assert.Equal(1L, Convert.ToInt64(Scalar(connection, "SELECT skill_level FROM player_skills WHERE skill_id=40009")));
            Assert.Equal(7L, Convert.ToInt64(Scalar(connection, "SELECT skill_level FROM player_skills WHERE skill_id=30000")));
            Assert.Equal(0L, Convert.ToInt64(Scalar(connection, "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='player_appearance' AND COLUMN_NAME IN ('jaw_height','chin_jut')")));
            Assert.Equal(2L, Convert.ToInt64(Scalar(connection, "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='player_appearance' AND COLUMN_NAME IN ('chin_height','cheek_bones') AND DATA_TYPE='int' AND IS_NULLABLE='NO'")));
            using var source = new MySqlDataSourceBuilder(builder.ConnectionString).Build();
            var slot = typeof(DatabaseFactory).GetField("_dataSource", BindingFlags.Static | BindingFlags.NonPublic)!; object? previous = slot.GetValue(null); slot.SetValue(null, source);
            try
            {
                AssertValues(PlayerAppearanceDAO.Load(id), columns, values);
                using (var cmd = new MySqlCommand("SELECT * FROM player_appearance WHERE player_id=@id", connection))
                {
                    cmd.Parameters.AddWithValue("@id", id); using var reader = cmd.ExecuteReader(); Assert.True(reader.Read());
                    var selected = (CharacterAppearance)typeof(MySqlCharacterSelectionRepository).GetMethod("ReadAppearance", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [reader])!;
                    Assert.Equal(65537, selected.JawHeight); Assert.Equal(-1234, selected.ChinJut); Assert.Equal(0.49f, selected.Height);
                }
                var replacement = new PlayerAppearance();
                for (int i = 1; i < columns.Length; i++)
                {
                    object value = columns[i] == "height" ? (object)1.5f : i * 4 + 1;
                    values[i] = value; typeof(PlayerAppearance).GetMethod("Set" + Accessor(columns[i]))!.Invoke(replacement, [value]);
                }
                player.SetPlayerAppearance(replacement); Assert.True(PlayerAppearanceDAO.Store(player)); AssertValues(PlayerAppearanceDAO.Load(id), columns, values);
                // A fresh install uses the new names directly and does not need update.sql.
                Execute(connection, "DROP TABLE player_appearance"); Execute(connection, appearance);
                Assert.True(PlayerAppearanceDAO.Store(player)); AssertValues(PlayerAppearanceDAO.Load(id), columns, values);
            }
            finally { slot.SetValue(null, previous); }
        }
        finally { Execute(admin, "DROP DATABASE `" + dbName + "`"); }
    }

    private static string Accessor(string column) => column == "height" ? "HeightScale" : string.Concat(column.Split('_').Select(p => p == "rgb" ? "RGB" : char.ToUpperInvariant(p[0]) + p[1..]));
    private static void AssertValues(PlayerAppearance actual, string[] columns, object[] expected)
    {
        Assert.NotNull(actual);
        for (int i = 1; i < columns.Length; i++)
        {
            object value = typeof(PlayerAppearance).GetMethod("Get" + Accessor(columns[i]))!.Invoke(actual, null)!;
            if (value is float f) Assert.Equal(BitConverter.SingleToInt32Bits((float)expected[i]), BitConverter.SingleToInt32Bits(f));
            else Assert.Equal((int)expected[i], (int)value);
        }
    }
    private static void Execute(MySqlConnection connection, string sql) { using var cmd = new MySqlCommand(sql, connection); cmd.ExecuteNonQuery(); }
    private static object? Scalar(MySqlConnection connection, string sql) { using var cmd = new MySqlCommand(sql, connection); return cmd.ExecuteScalar(); }
}
