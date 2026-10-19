using System;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/HouseScriptsDAO (@author Rolandas, Neon, Sykra). JDBC DAO over house_scripts (compressed per-house furniture
/// scripts). DatabaseFactory-style; positional '?' -> ordered MySqlParameter; executeQuery -> ExecuteReader; executeUpdate ->
/// ExecuteNonQuery; SQL (incl. ON DUPLICATE KEY UPDATE / ORDER BY date_added) verbatim. SLF4J "{}"+throwable -> LogError(e, "{HouseId}").
/// Compressed script bytes and declared inflated size are stored without transcoding or recompression.
/// </summary>
public class HouseScriptsDAO
{
    private static readonly ILogger log = AionLog.For(nameof(HouseScriptsDAO));

    private const string INSERT_QUERY = "INSERT INTO `house_scripts` (`house_id`,`script_id`,`script`,`uncompressed_size`) VALUES (?,?,?,?) ON DUPLICATE KEY UPDATE script=VALUES(script), uncompressed_size=VALUES(uncompressed_size)";
    private const string DELETE_QUERY = "DELETE FROM `house_scripts` WHERE `house_id`=? AND `script_id`=?";
    private const string DELETE_ALL_QUERY = "DELETE FROM `house_scripts` WHERE `house_id`=?";
    private const string SELECT_QUERY = "SELECT `script_id`, `script`, `uncompressed_size` FROM `house_scripts` WHERE `house_id`=? ORDER BY `date_added`";

    public static void StoreScript(int houseId, int scriptId, byte[] compressedXML, int uncompressedSize)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = INSERT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = houseId });
            stmt.Parameters.Add(new MySqlParameter { Value = scriptId });
            stmt.Parameters.Add(new MySqlParameter { Value = compressedXML });
            stmt.Parameters.Add(new MySqlParameter { Value = uncompressedSize });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not save script data for houseId: {HouseId}", houseId);
        }
    }

    public static PlayerScripts GetPlayerScripts(int houseId)
    {
        PlayerScripts scripts = new PlayerScripts(houseId);
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = houseId });
            using MySqlDataReader rset = stmt.ExecuteReader();
            while (rset.Read())
            {
                scripts.Set(rset.GetInt32(rset.GetOrdinal("script_id")), rset.IsDBNull(rset.GetOrdinal("script")) ? null! : (byte[])rset["script"], rset.GetInt32(rset.GetOrdinal("uncompressed_size")), false);
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not restore script data for houseId: {HouseId}", houseId);
        }
        return scripts;
    }

    public static void DeleteScript(int houseId, int scriptId)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = DELETE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = houseId });
            stmt.Parameters.Add(new MySqlParameter { Value = scriptId });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not delete script for houseId: {HouseId}", houseId);
        }
    }

    public static void DeleteScriptsForHouse(int houseId)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = DELETE_ALL_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = houseId });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not delete script for houseId: {HouseId}", houseId);
        }
    }

}
