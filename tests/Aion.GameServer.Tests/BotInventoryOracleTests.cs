using System.Text.Json;
using System.Text.Json.Nodes;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

public sealed class BotInventoryOracleTests
{
    private static readonly BotInventoryItem[] Items =
    [
        new(20, 162000001, "potion", 3, 7, "", 4, false),
        new(10, BotWorldModel.KinahItemId, "kinah", 5_000_000_001, 0, "", 65535, false),
        new(30, 100000001, "weapon", 1, 9, "Crafter", 1, false)
    ];

    [Fact]
    public void ExactUnorderedIdentityAndAllModelFieldsMatchWith64BitKinah()
    {
        Verify(Response(Items.Reverse().ToArray()));
    }

    [Theory]
    [InlineData("objectId", "99")]
    [InlineData("itemId", "162000002")]
    [InlineData("description", "\"wrong\"")]
    [InlineData("count", "2")]
    [InlineData("itemMask", "8")]
    [InlineData("creator", "\"Other\"")]
    [InlineData("equipmentSlot", "5")]
    [InlineData("cloth", "true")]
    public void DetectsEveryFieldMismatchWithoutMutatingClient(string field, string json)
    {
        var response = Response(Items);
        response["inventory"]!["items"]![0]![field] = JsonNode.Parse(json);
        Assert.Throws<InvalidDataException>(() => Verify(response));
        Assert.Equal(3, Items[0].Count);
        Assert.Equal(20, Items[0].ObjectId);
    }

    [Theory]
    [InlineData("missing")] [InlineData("extra")] [InlineData("duplicate")]
    [InlineData("wrong-character")] [InlineData("offline")] [InlineData("not-ok")]
    [InlineData("wrong-total")] [InlineData("wrong-kinah")] [InlineData("absent-kinah-row")]
    public void RejectsIncompleteOrInconsistentSnapshots(string change)
    {
        var response = Response(Items);
        var inventory = response["inventory"]!;
        var rows = inventory["items"]!.AsArray();
        switch (change)
        {
            case "missing": rows.RemoveAt(0); inventory["totalPacketItemCount"] = rows.Count; break;
            case "extra": rows.Add(JsonSerializer.SerializeToNode(Items[0] with { ObjectId = 99 }, Options)); inventory["totalPacketItemCount"] = rows.Count; break;
            case "duplicate": rows[2] = rows[0]!.DeepClone(); break;
            case "wrong-character": response["recipientCharacterId"] = 99; break;
            case "offline": response["online"] = false; break;
            case "not-ok": response["ok"] = false; break;
            case "wrong-total": inventory["totalPacketItemCount"] = 0; break;
            case "wrong-kinah": inventory["kinah"] = 1; break;
            case "absent-kinah-row": rows.RemoveAt(1); inventory["totalPacketItemCount"] = rows.Count; break;
        }
        Assert.Throws<InvalidDataException>(() => Verify(response));
    }

    [Fact]
    public void OldCountsOnlyEndpointIsNotAcceptedAsInventoryVerification()
    {
        var response = Response(Items);
        response["inventory"]!.AsObject().Remove("items");
        Assert.Throws<KeyNotFoundException>(() => Verify(response));
    }

    [Fact]
    public void DuplicateClientIdentityCannotPassAnOtherwiseMatchingOracle()
    {
        var response = JsonSerializer.SerializeToElement(Response(Items));
        var duplicate = Items.Append(Items[0]).ToArray();
        var error = Assert.Throws<InvalidDataException>(() =>
            BotInventoryOracle.Verify(42, duplicate, 5_000_000_001, response));
        Assert.Contains("Bot inventory contains duplicate object IDs", error.Message);
    }

    [Fact]
    public void EmptyInventoryAndNoKinahItemCanBeObservedWithoutCreatingOne()
    {
        var response = JsonSerializer.SerializeToElement(new { ok = true, online = true, recipientCharacterId = 42,
            inventory = new { kinah = 0, totalPacketItemCount = 0, items = Array.Empty<BotInventoryItem>() } }, Options);
        BotInventoryOracle.Verify(42, [], 0, response);
    }

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    private static JsonObject Response(BotInventoryItem[] rows) => JsonSerializer.SerializeToNode(new
    {
        ok = true, online = true, recipientCharacterId = 42,
        inventory = new { kinah = 5_000_000_001, totalPacketItemCount = rows.Length, items = rows }
    }, Options)!.AsObject();
    private static void Verify(JsonObject response) => BotInventoryOracle.Verify(42, Items, 5_000_000_001,
        JsonSerializer.SerializeToElement(response));
}
