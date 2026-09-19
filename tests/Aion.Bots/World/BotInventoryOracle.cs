using System.Text.Json;

namespace Aion.Bots.World;

/// <summary>Independent read-only server oracle; never imports server state into the bot's perception.</summary>
public static class BotInventoryOracle
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public static void Verify(int characterId, IReadOnlyCollection<BotInventoryItem> clientItems, long clientKinah, JsonElement response, BotCubeExpansion? cube = null)
    {
        Require(response.GetProperty("ok").GetBoolean() && response.GetProperty("online").GetBoolean(), "Storage oracle did not return an online player.");
        Require(response.GetProperty("recipientCharacterId").GetInt32() == characterId, "Storage oracle returned the wrong character.");
        var inventory = response.GetProperty("inventory");
        if (cube != null)
            Require(cube.Capacity == inventory.GetProperty("cubeLimit").GetInt32(), "Storage oracle cube capacity differs from the client expansion counts.");
        long serverKinah = inventory.GetProperty("kinah").GetInt64();
        var serverItems = inventory.GetProperty("items").EnumerateArray().Select(row => new BotInventoryItem(
            row.GetProperty("objectId").GetInt32(), row.GetProperty("itemId").GetInt32(), row.GetProperty("description").GetString()!,
            row.GetProperty("count").GetInt64(), row.GetProperty("itemMask").GetUInt16(), row.GetProperty("creator").GetString()!,
            row.GetProperty("equipmentSlot").GetUInt16(), row.GetProperty("cloth").GetBoolean())
        {
            Details = row.GetProperty("details").Deserialize<BotItemDetails>(JsonOptions)
                ?? throw new InvalidDataException("Storage oracle omitted item details.")
        }).ToArray();
        Require(serverItems.Length == inventory.GetProperty("totalPacketItemCount").GetInt32(), "Storage oracle item count differs from its rows.");
        Require(serverItems.Select(i => i.ObjectId).Distinct().Count() == serverItems.Length, "Storage oracle contains duplicate object IDs.");
        Require(clientItems.Select(i => i.ObjectId).Distinct().Count() == clientItems.Count, "Bot inventory contains duplicate object IDs.");
        Require(clientKinah == serverKinah, $"Storage oracle kinah mismatch: bot={clientKinah}, server={serverKinah}.");
        Require(serverItems.Where(i => i.ItemId == BotWorldModel.KinahItemId).Sum(i => i.Count) == serverKinah,
            "Storage oracle kinah total differs from its item rows.");
        var expected = clientItems.OrderBy(i => i.ObjectId).ToArray();
        var actual = serverItems.OrderBy(i => i.ObjectId).ToArray();
        if (!expected.SequenceEqual(actual))
            throw new InvalidDataException($"Storage oracle inventory mismatch for character {characterId}: " +
                $"bot-only/changed={JsonSerializer.Serialize(expected.Except(actual))}; server-only/changed={JsonSerializer.Serialize(actual.Except(expected))}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
