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
        {
            Details = new(new(true, 7, 100000002, 3, 4, new(1, 2, 3, 4, 5, 6), 168000123, 5, true, 1234),
                new(100000003, new(7, 8, 9, 10, 11, 12), 2, 3), 1L << 40 | 1, 543210, new(4, 2), 3)
        }
    ];

    [Fact]
    public void ExactUnorderedIdentityAndAllModelFieldsMatchWith64BitKinah()
    {
        Verify(Response(Items.Reverse().ToArray()));
    }

    [Fact]
    public void CubeCapacityIsIndependentlyComparedWhenObservedOnTheWire()
    {
        var response = Response(Items);
        response["inventory"]!["cubeLimit"] = 36;
        var json = JsonSerializer.SerializeToElement(response);
        BotInventoryOracle.Verify(42, Items, 5_000_000_001, json, new(1, 0, 0));
        Assert.Throws<InvalidDataException>(() => BotInventoryOracle.Verify(42, Items, 5_000_000_001, json, new(0, 0, 0)));
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
    public void GearFieldsUseValueEqualityAcrossIndependentlyDeserializedSnapshots()
    {
        Verify(Response(Items));
        Assert.NotSame(Items[2].Details, JsonSerializer.Deserialize<BotInventoryItem>(
            JsonSerializer.Serialize(Items[2], Options), Options)!.Details);
    }

    [Theory]
    [MemberData(nameof(GearMismatches))]
    public void DetectsEveryObservedGearFieldMismatch(string path, string json)
    {
        var response = Response(Items);
        JsonNode row = response["inventory"]!["items"]![2]!["details"]!;
        var parts = path.Split('.');
        foreach (string part in parts[..^1]) row = row[part]!;
        row[parts[^1]] = JsonNode.Parse(json);
        Assert.Throws<InvalidDataException>(() => Verify(response));
    }

    public static IEnumerable<object[]> GearMismatches()
    {
        foreach (string field in new[] { "enchantLevel", "skinId", "optionalSockets", "enchantBonus", "godstoneId", "tempering", "buffSkill" })
            yield return new object[] { "enchantment." + field, "99" };
        yield return new object[] { "enchantment.soulBound", "false" };
        yield return new object[] { "enchantment.amplified", "false" };
        for (int slot = 0; slot < 6; slot++)
        {
            yield return new object[] { "enchantment.manastones.slot" + slot, "99" };
            yield return new object[] { "fusion.manastones.slot" + slot, "99" };
        }
        foreach (string field in new[] { "fusion.itemId", "fusion.optionalSockets", "fusion.bonusStatsId", "equippedSlot", "chargePoints", "premium.bonusStatsId", "premium.tuneCount", "packCount" })
            yield return new object[] { field, "99" };
        foreach (string field in new[] { "enchantment", "fusion", "premium", "equippedSlot", "chargePoints", "packCount" })
            yield return new object[] { field, "null" };
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
