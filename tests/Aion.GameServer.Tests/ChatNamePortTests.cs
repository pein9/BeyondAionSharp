using System.Text.Json;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Tests;

public sealed class ChatNamePortTests
{
    [Theory]
    [MemberData(nameof(JavaCases))]
    public void OwnerNamesAndNullsMatchExactJavaTags(int id, string? owner, string item, string itemName, string recipe, string recipeName)
    {
        Assert.Equal(itemName, ChatUtil.ItemName(id, owner));
        Assert.Equal(recipeName, ChatUtil.RecipeName(id, owner));
        Assert.Equal(item, ChatUtil.Item(id));
        Assert.Equal(recipe, ChatUtil.Recipe(id));
    }

    public static IEnumerable<object?[]> JavaCases()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "AionServer.slnx"))) root = root.Parent;
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(root!.FullName,
            "parity-artifacts/golden/formulas/ChatNames.7181a3864.json")));
        Assert.Equal("7181a3864a404a4d78342bf6e84365e3af731c91", fixture.RootElement.GetProperty("sourceJavaCommit").GetString());
        foreach (var c in fixture.RootElement.GetProperty("cases").EnumerateArray())
            yield return [c.GetProperty("id").GetInt32(), c.GetProperty("playerName").GetString(),
                c.GetProperty("item").GetString(), c.GetProperty("itemName").GetString(),
                c.GetProperty("recipe").GetString(), c.GetProperty("recipeName").GetString()];
    }
}
