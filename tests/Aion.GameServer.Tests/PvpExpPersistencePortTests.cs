using System.Reflection;
using System.Text.RegularExpressions;
using Aion.Commons.Database;
using Aion.GameServer.Dao;
using Aion.GameServer.Model;
using MySqlConnector;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class PvpExpPersistencePortTests
{
    // Explicit opt-in only: creates and drops its own random database on a disposable test server.
    [SkippableFact]
    public void MigrationAndActualPlayerDaoRoundTripPreserveSigned64BitCounterAndAdjacentFields()
    {
        string? testConnection = Environment.GetEnvironmentVariable("AION_PVP_PORT_TEST_CONNECTION");
        Skip.If(string.IsNullOrWhiteSpace(testConnection), "Set AION_PVP_PORT_TEST_CONNECTION to a disposable MySQL test server.");
        using var scope = new PvpExpPortTests.Scope();
        string dbName = "pvp_port_" + Guid.NewGuid().ToString("N");
        var builder = new MySqlConnectionStringBuilder(testConnection) {Database = ""};
        using var admin = new MySqlConnection(builder.ConnectionString); admin.Open();
        Execute(admin, "CREATE DATABASE `" + dbName + "`");
        try
        {
            builder.Database = dbName;
            using var connection = new MySqlConnection(builder.ConnectionString); connection.Open();
            string ddl = File.ReadAllText(Path.Combine(PvpExpPortTests.Root(), "game-server/sql/aion_gs.sql"));
            string players = Regex.Match(ddl, @"CREATE TABLE `players` \([\s\S]*?\n\)[^;]*;").Value;
            Assert.NotEmpty(players);
            Execute(connection, Regex.Replace(players, @"(?m)^\s*`pvp_exp`[^\r\n]*\r?\n", ""));
            Execute(connection, "CREATE TABLE player_skills (skill_id int, skill_level int)");
            Execute(connection, "INSERT INTO player_skills VALUES (29999,5),(30000,7)");
            Execute(connection, File.ReadAllText(Path.Combine(PvpExpPortTests.Root(), "game-server/sql/update.sql")));
            Assert.Equal(1L, Convert.ToInt64(Scalar(connection, "SELECT skill_level FROM player_skills WHERE skill_id=29999")));
            Assert.Equal(7L, Convert.ToInt64(Scalar(connection, "SELECT skill_level FROM player_skills WHERE skill_id=30000")));
            Execute(connection, "INSERT INTO players(id,name,account_id,account_name,x,y,z,heading,world_id,gender,race,player_class,note) VALUES(1,'DbFixture',1,'fixture',1,2,3,4,300100000,'MALE','ELYOS','WARRIOR',NULL)");
            Assert.Equal(0L, Convert.ToInt64(Scalar(connection, "SELECT pvp_exp FROM players WHERE id=1")));
            using var dataSource = new MySqlDataSourceBuilder(builder.ConnectionString).Build();
            var source = typeof(DatabaseFactory).GetField("_dataSource", BindingFlags.Static | BindingFlags.NonPublic)!;
            object? previous = source.GetValue(null); source.SetValue(null, dataSource);
            try
            {
                // Keep this player out of World, so loading it cannot fire unrelated login/quest callbacks.
                var player = PvpExpPortTests.BarePlayer(1, 1); var pcd = player.GetCommonData();
                pcd.SetPlayerClass(PlayerClass.WARRIOR); pcd.SetRace(Race.ELYOS); pcd.SetGender(Gender.MALE);
                pcd.SetName("DbFixture"); pcd.SetNote(null!); pcd.SetMentorFlagTime(91); pcd.SetWorldOwnerId(37);
                pcd.SetCurrentReposeEnergy(6_000_000_000L);
                player.SetPosition(scope.H.World.CreatePosition(300100000, 11, 22, 33, 44, 1));
                pcd.SetWorldOwnerId(37); // SetPosition synchronizes common data with the map's default owner.
                foreach (long xp in new[] {8_000_000_000L, -9_000_000_000L, long.MaxValue, long.MinValue, 0})
                {
                    pcd.SetPvpExp(xp); PlayerDAO.StorePlayer(player);
                    var loaded = PlayerDAO.LoadPlayerCommonData(1); Assert.NotNull(loaded);
                    Assert.Equal(xp, loaded.GetPvpExp());
                    Assert.Equal(6_000_000_000L, loaded.GetCurrentReposeEnergy());
                    Assert.Equal(91, loaded.GetMentorFlagTime()); Assert.Equal(37, loaded.GetWorldOwnerId());
                    Assert.Equal(11f, loaded.GetX()); Assert.Equal(44, loaded.GetHeading());
                    Assert.Null(loaded.GetNote()); Assert.Null(loaded.GetLastOnline());
                }
                // Also check the fresh-install schema, without applying a migration to it.
                Execute(connection, "DROP TABLE players"); Execute(connection, players);
                Assert.Equal("bigint", Convert.ToString(Scalar(connection, "SELECT DATA_TYPE FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='players' AND COLUMN_NAME='pvp_exp'")));
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
