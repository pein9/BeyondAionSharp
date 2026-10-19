using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Enchants;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.Model.Templates.Items.Enums;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class ItemKindStoneGradePortTests
{
    private static readonly Lazy<ItemData> ChangedTemplates = new(LoadChangedTemplates);

    [Fact]
    public void StoneGradesLevelsQualitiesAndRejectionsMatchCompiledJava()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(),
            "parity-artifacts/golden/formulas/EnchantmentStone.784e0764b.json")));
        Assert.Equal("784e0764b59fe310cf4103843fc9f5636ea518f3", fixture.RootElement.GetProperty("sourceJavaCommit").GetString());
        var cases = fixture.RootElement.GetProperty("cases");
        Assert.Equal(211, cases.GetArrayLength());
        foreach (var c in cases.EnumerateArray())
        {
            int id = c.GetProperty("id").GetInt32();
            if (c.TryGetProperty("error", out var error))
            {
                Assert.Equal(error.GetString(), Assert.Throws<ArgumentException>(() =>
                    EnchantmentStoneExtensions.GetByItemId(id)).Message);
                continue;
            }
            var grade = EnchantmentStoneExtensions.GetByItemId(id);
            Assert.Equal(c.GetProperty("grade").GetString(), grade.ToString());
            Assert.Equal(c.GetProperty("level").GetInt32(), grade.GetBaseLevel());
            Assert.Equal(c.GetProperty("qualityId").GetInt32(), grade.GetBaseQuality().GetQualityId());
        }
    }

    [Theory]
    [InlineData(166000197, ItemGroup.ENCHANTMENT, 1)]
    [InlineData(166000198, ItemGroup.ENCHANTMENT, 10)]
    [InlineData(166000199, ItemGroup.ENCHANTMENT, 50)]
    [InlineData(166000200, ItemGroup.ENCHANTMENT, 100)]
    [InlineData(166000201, ItemGroup.ENCHANTMENT, 150)]
    [InlineData(166020001, ItemGroup.ENCHANTMENT, 200)]
    [InlineData(166020002, ItemGroup.ENCHANTMENT, 200)]
    [InlineData(166030009, ItemGroup.TAMPERING, -1)]
    [InlineData(167040000, ItemGroup.NONE, 0)]
    [InlineData(167040001, ItemGroup.NONE, 0)]
    [InlineData(167040002, ItemGroup.NONE, 0)]
    [InlineData(167040003, ItemGroup.NONE, 0)]
    [InlineData(167040004, ItemGroup.NONE, 0)]
    public void ShippedItemDataLoadsTheDeclaredGroupActionAndCount(int id, ItemGroup group, int count)
    {
        var template = ChangedTemplates.Value.GetItemTemplate(id);
        Assert.NotNull(template);
        Assert.Equal(group, template.GetItemGroup());
        var action = Assert.Single(template.GetActions().GetItemActions());
        if (count < 0)
            Assert.IsType<TamperingAction>(action);
        else
            Assert.Equal(count, Assert.IsType<EnchantItemAction>(action).count);
    }

    private static ItemData LoadChangedTemplates()
    {
        var wanted = new HashSet<int> { 166000197, 166000198, 166000199, 166000200, 166000201,
            166020001, 166020002, 166030009, 167040000, 167040001, 167040002, 167040003, 167040004 };
        var subset = new XElement("item_templates");
        using (var reader = XmlReader.Create(Path.Combine(Root(), "game-server/data/static_data/items/item_templates.xml")))
            while (reader.ReadToFollowing("item_template"))
                if (int.TryParse(reader.GetAttribute("id"), out int id) && wanted.Remove(id))
                {
                    using var item = reader.ReadSubtree();
                    subset.Add(XElement.Load(item));
                }
        Assert.Empty(wanted);
        using var input = subset.CreateReader();
        var result = (ItemData)new XmlSerializer(typeof(ItemData)).Deserialize(input)!;
        result.AfterUnmarshal(null!);
        return result;
    }

    private static string Root()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "AionServer.slnx")))
                return d.FullName;
        throw new DirectoryNotFoundException("AionServer.slnx");
    }
}
