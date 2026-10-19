using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Recipe;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Craft;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class CraftComponentSelectionPortTests
{
    [Theory]
    [InlineData("empty")]
    [InlineData("unrelated")]
    [InlineData("nonfirst-component")]
    [InlineData("no-sets")]
    public void MissingComponentSetReturnsBeforeConsumingMaterialsUsesOrStartingACraft(string mode)
    {
        using var s = new LimitedRecipeMorphPortTests.Scope(); Ready(s);
        s.Recipe.GetComponents()[0].component.Add(new Component {itemid=199999901, quantity=3});
        var sent = mode switch
        {
            "unrelated" => new Dictionary<int,long> {{123, 99}},
            "nonfirst-component" => new() {{199999901, 3}},
            _ => new()
        };
        if (mode == "no-sets") s.Recipe.GetComponents().Clear();
        CraftService.StartCrafting(s.Player, 9001, 0, 0, sent);
        Assert.Null(s.Player.GetInteractionTask()); Assert.Equal(3, s.Player.GetRecipeList().GetRecipes()[9001]);
        Assert.Equal(5, s.Player.GetInventory().GetItemCountByItemId(199999902));
        Assert.Equal(4, s.Player.GetInventory().GetItemCountByItemId(199999901));
        Assert.Empty(s.Connection.Packets);
    }

    [Fact]
    public void UnknownRecipeIsRejectedBeforeDereferencingItsTemplate()
    {
        using var s = new LimitedRecipeMorphPortTests.Scope(); Ready(s);
        CraftService.StartCrafting(s.Player, 9999, 0, 0, new());
        Assert.Null(s.Player.GetInteractionTask()); Assert.Equal(3, s.Player.GetRecipeList().GetRecipes()[9001]);
        Assert.Empty(s.Connection.Packets);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FirstDeclaredMatchingSetWinsAndServerQuantitiesAreUsed(bool bothKeys)
    {
        using var s = new LimitedRecipeMorphPortTests.Scope(); Ready(s);
        s.Recipe.GetComponents().Add(new ComponentsData {component = [new Component {itemid=199999901, quantity=3}]});
        var sent = new Dictionary<int,long> {{199999901, 0}}; // The client values do not choose quantities.
        if (bothKeys) sent[199999902] = 999;
        CraftService.StartCrafting(s.Player, 9001, 0, 0, sent);
        Assert.NotNull(s.Player.GetInteractionTask()); Assert.Equal(2, s.Player.GetRecipeList().GetRecipes()[9001]);
        Assert.Equal(bothKeys ? 3 : 5, s.Player.GetInventory().GetItemCountByItemId(199999902));
        Assert.Equal(bothKeys ? 4 : 1, s.Player.GetInventory().GetItemCountByItemId(199999901));
    }

    [Fact]
    public void SelectedSetChecksEveryServerComponentBeforeAnyConsumption()
    {
        using var s = new LimitedRecipeMorphPortTests.Scope(); Ready(s);
        s.Recipe.GetComponents()[0].component.Add(new Component {itemid=199999901, quantity=5});
        CraftService.StartCrafting(s.Player, 9001, 0, 0, new() {{199999902, 2}});
        Assert.Null(s.Player.GetInteractionTask()); Assert.Equal(3, s.Player.GetRecipeList().GetRecipes()[9001]);
        Assert.Equal(5, s.Player.GetInventory().GetItemCountByItemId(199999902)); Assert.Equal(4, s.Player.GetInventory().GetItemCountByItemId(199999901));
        Assert.Equal(4, LimitedRecipeMorphPortTests.Payload(Assert.Single(s.Connection.Packets.OfType<SM_CRAFT_UPDATE>()))[2]);
    }

    private static void Ready(LimitedRecipeMorphPortTests.Scope s)
    {
        s.Player.GetRecipeList().AddRecipe(9001, 3); s.Materials(5);
        var product = new Item(199999930, DataManager.ITEM_DATA.GetItemTemplate(199999901)); product.SetItemCount(4);
        s.Player.GetInventory().OnLoadHandler(product); s.Connection.Clear();
    }
}
