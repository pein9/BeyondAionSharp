using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Xml.Serialization;
using Aion.Commons.Nio;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Recipe;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Craft;
using Aion.GameServer.Services.Items;
using Aion.GameServer.SkillEngine.Task;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class LimitedRecipeMorphPortTests
{
    private const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    [Fact]
    public void RecipeAndCraftPacketsMatchAllExactJavaPayloads()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(PvpExpPortTests.Root(), "parity-artifacts/golden/packets/RecipeCraft.e5c133836.json")));
        Assert.Equal("e5c133836804f97e86b9d026c2954ffdebd0609c", json.RootElement.GetProperty("sourceJavaCommit").GetString());
        foreach (var c in json.RootElement.GetProperty("cases").EnumerateArray())
        {
            var v = c.GetProperty("inputs"); AionServerPacket packet;
            switch (c.GetProperty("kind").GetString())
            {
                case "craft": packet = new SM_CRAFT_UPDATE(v.GetProperty("skillId").GetInt32(), new NamedItem {itemId=v.GetProperty("itemId").GetInt32()}, v.GetProperty("success").GetInt32(), v.GetProperty("failure").GetInt32(), v.GetProperty("action").GetInt32(), v.GetProperty("executionSpeed").GetInt32(), v.GetProperty("delay").GetInt32()); break;
                case "learn": packet = new SM_LEARN_RECIPE(v.GetProperty("id").GetInt32(), v.GetProperty("count").GetInt32()); break;
                case "list": packet = new SM_RECIPE_LIST(v.GetProperty("pairs").EnumerateArray().ToDictionary(p => p[0].GetInt32(), p => p[1].GetInt32())); break;
                default: throw new InvalidOperationException();
            }
            Assert.True(c.GetProperty("payloadHex").GetString() == Convert.ToHexString(Payload(packet)), c.GetProperty("name").GetString());
        }
        Assert.Equal(0x2D, ItemPacketService.ItemUpdateType.INC_ITEM_CRAFT.GetMask());
        Assert.True(ItemPacketService.ItemUpdateType.INC_ITEM_CRAFT.IsSendable());
    }

    [Fact]
    public void RecipeSnapshotIsImmutableAndCountsAreConsumedEvenWhenDatabaseWritesFail()
    {
        using var s = new Scope(); var list = s.Player.GetRecipeList();
        Assert.True(list.AddRecipe(9001, 3)); Assert.False(list.AddRecipe(9001, 99)); Assert.True(list.AddRecipe(9002, 0));
        var snapshot = list.GetRecipes();
        Assert.Throws<NotSupportedException>(() => ((IDictionary<int,int>)snapshot).Add(123, 4));
        Assert.False(list.AddRecipe(s.Player, 9003, 5)); Assert.False(list.IsRecipePresent(9003)); // insert rollback
        Assert.False(list.DeleteRecipe(s.Player, 9001)); Assert.True(list.IsRecipePresent(9001)); // explicit delete waits for DB success
        s.Connection.Clear(); list.DecreaseProductionLimit(s.Player, 9001);
        Assert.Equal(2, list.GetRecipes()[9001]); Assert.Equal(3, snapshot[9001]); Assert.Empty(s.Connection.Packets);
        list.DecreaseProductionLimit(s.Player, 9001); Assert.Equal(1, list.GetRecipes()[9001]);
        list.DecreaseProductionLimit(s.Player, 9001); Assert.False(list.IsRecipePresent(9001));
        Assert.Equal(2, s.Connection.Packets.Length);
        Assert.IsType<SM_RECIPE_DELETE>(s.Connection.Packets[0]);
        Assert.Equal(Payload(SM_SYSTEM_MESSAGE.STR_COMBINE_USAGE_OVER(ChatUtil.Recipe(9001, s.Player))), Payload(s.Connection.Packets[1]));
        s.Connection.Clear(); list.DecreaseProductionLimit(s.Player, 9002); list.DecreaseProductionLimit(s.Player, 9999);
        Assert.Equal(0, list.GetRecipes()[9002]); Assert.Empty(s.Connection.Packets);
        Assert.Equal($"[recipe_ex:9001;{s.Player.GetName()}]", ChatUtil.Recipe(9001, s.Player));
        Assert.Equal("[recipe:9001]", ChatUtil.Recipe(9001));
    }

    [Fact]
    public void MorphIdentityAndProfessionFlagsDoNotGiveMorphingCraftSkillPoints()
    {
        using var s = new Scope();
        Assert.Equal(3, DataManager.RECIPE_DATA.GetMaxProductionCount(9001));
        Assert.Equal(0, DataManager.RECIPE_DATA.GetMaxProductionCount(9002));
        Assert.Equal(0, DataManager.RECIPE_DATA.GetMaxProductionCount(9999));
        Assert.True(s.Recipe.IsMorph()); Assert.False(DataManager.RECIPE_DATA.GetRecipeTemplateById(9002).IsMorph());
        var morph = Entry(40009); Assert.True(morph.IsMorphSkill()); Assert.False(morph.IsCraftingSkill()); Assert.Equal(0, morph.GetProfessionFlag());
        Assert.Equal(1, Entry(30001).GetProfessionFlag());
        Assert.True(Entry(40001).IsCraftingSkill());
    }

    [Fact]
    public void Opcode158RequestsCurrentRecipesAndIgnoresMissingActivePlayer()
    {
        using var s = new Scope(); s.Player.GetRecipeList().AddRecipe(9001, 3);
        var registrations = (Array)typeof(AionClientPacketFactory).GetField("packets", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var info = registrations.GetValue(158)!;
        Assert.Equal(nameof(CM_RECIPE_LIST), info.GetType().GetMethod("GetPacketClassName")!.Invoke(info, null));
        var packet = (AionClientPacket)info.GetType().GetMethod("NewPacket")!.Invoke(info, new object[] {158, ByteBuffer.Allocate(0), s.Connection})!;
        Assert.True(packet.Read()); packet.Run();
        Assert.Equal(Payload(new SM_RECIPE_LIST(s.Player.GetRecipeList().GetRecipes())), Payload(Assert.Single(s.Connection.Packets)));
        s.Player.GetRecipeList().DecreaseProductionLimit(s.Player, 9001); s.Connection.Clear(); packet.Run();
        Assert.Equal(Payload(new SM_RECIPE_LIST(new Dictionary<int,int> {{9001,2}})), Payload(Assert.Single(s.Connection.Packets)));
        s.Connection.Clear(); s.Connection.SetActivePlayer(null!); packet.Run(); Assert.Empty(s.Connection.Packets);
    }

    [Fact]
    public void MorphUsesOneThreeSecondBarNoProgressComboOrExperienceAndConsumesAtStart()
    {
        using var s = new Scope(); s.Player.GetRecipeList().AddRecipe(9001, 3); s.Materials(5);
        long exp = s.Player.GetCommonData().GetExp(); int skillXp = s.Player.GetSkillList().GetSkillEntry(40009).GetCurrentXp();
        CraftService.StartCrafting(s.Player, 9001, 0, 0, new() {{199999902, 2}});
        Assert.IsType<CraftingTask>(s.Player.GetInteractionTask()); Assert.Equal(2, s.Player.GetRecipeList().GetRecipes()[9001]);
        Assert.Equal(3, s.Player.GetInventory().GetItemCountByItemId(199999902));
        var start = s.Connection.Packets.OfType<SM_CRAFT_UPDATE>().ToArray(); Assert.Equal(2, start.Length);
        foreach (var p in start) Assert.Equal(3000, BitConverter.ToInt32(Payload(p), 19));
        s.Connection.Clear(); s.H.Clock.Advance(TimeSpan.FromMilliseconds(2999));
        Assert.NotNull(s.Player.GetInteractionTask()); Assert.Empty(s.Connection.Packets.OfType<SM_CRAFT_UPDATE>());
        s.H.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Null(s.Player.GetInteractionTask()); Assert.Equal(1, s.Player.GetInventory().GetItemCountByItemId(199999901));
        var end = Assert.Single(s.Connection.Packets.OfType<SM_CRAFT_UPDATE>()); Assert.Equal(5, Payload(end)[2]);
        Assert.Equal(3000, BitConverter.ToInt32(Payload(end), 19)); Assert.Equal(0, BitConverter.ToInt32(Payload(end), 23));
        Assert.Equal(exp, s.Player.GetCommonData().GetExp()); Assert.Equal(skillXp, s.Player.GetSkillList().GetSkillEntry(40009).GetCurrentXp());
        Assert.Equal(2, s.Player.GetRecipeList().GetRecipes()[9001]); Assert.Empty(s.H.Clock.Faults);
    }

    [Fact]
    public void FailedValidationKeepsMaterialsAndUsesButCancellingAStartedCraftDoesNotRefundThem()
    {
        using var s = new Scope(); var list = s.Player.GetRecipeList(); list.AddRecipe(9001, 1); s.Materials(1);
        CraftService.StartCrafting(s.Player, 9001, 0, 0, new() {{199999902,2}});
        Assert.Null(s.Player.GetInteractionTask()); Assert.Equal(1, list.GetRecipes()[9001]);
        Assert.Equal(1, s.Player.GetInventory().GetItemCountByItemId(199999902));
        s.Materials(4);
        CraftService.StartCrafting(s.Player, 9001, 0, 0, new() {{199999902,2}});
        Assert.NotNull(s.Player.GetInteractionTask()); Assert.False(list.IsRecipePresent(9001));
        s.Player.GetInteractionTask().Abort(); s.H.Clock.Advance(TimeSpan.FromSeconds(4));
        Assert.False(list.IsRecipePresent(9001)); Assert.Equal(3, s.Player.GetInventory().GetItemCountByItemId(199999902));
        Assert.Equal(0, s.Player.GetInventory().GetItemCountByItemId(199999901)); Assert.Empty(s.H.Clock.Faults);
    }

    internal static PlayerSkillEntry Entry(int id) => new(id, 1, 0, IPersistable.PersistentState.NOACTION);

    [Fact]
    public void BotRequestsRecipeSnapshotAndTracksObservedProductionCounts()
    {
        var api = new Aion.Bots.Api.BotApi(); var request = api.RequestRecipes();
        Assert.Equal(typeof(CM_RECIPE_LIST), request.PacketType); Assert.Empty(request.Body);
        Assert.Equal(158, Aion.Bots.Protocol.GamePacketRegistry.Instance.GetClient(request.PacketType).Opcode);
        var decoder = new Aion.Bots.Protocol.BotServerPacketDecoder();
        api.World.Apply(decoder.Decode(typeof(SM_RECIPE_LIST), Payload(new SM_RECIPE_LIST(new Dictionary<int,int> {{9001,3},{9002,0}}))));
        Assert.Equal((byte)3, api.World.RecipeProductionLimits[9001]); Assert.Equal((byte)0, api.World.RecipeProductionLimits[9002]);
        api.World.Apply(decoder.Decode(typeof(SM_LEARN_RECIPE), Payload(new SM_LEARN_RECIPE(9003,127))));
        Assert.Equal((byte)127, api.World.RecipeProductionLimits[9003]);
        api.World.Apply(decoder.Decode(typeof(SM_RECIPE_DELETE), Payload(new SM_RECIPE_DELETE(9001))));
        Assert.False(api.World.Recipes.Contains(9001)); Assert.False(api.World.RecipeProductionLimits.ContainsKey(9001));
        api.World.Apply(decoder.Decode(typeof(SM_RECIPE_LIST), Payload(new SM_RECIPE_LIST(new Dictionary<int,int> {{9003,2}}))));
        Assert.Equal((byte)2, Assert.Single(api.World.RecipeProductionLimits).Value); Assert.Equal(9003, Assert.Single(api.World.Recipes));
    }
    internal static byte[] Payload(AionServerPacket packet)
    {
        var buffer = ByteBuffer.Allocate(8192).Order(ByteOrder.LITTLE_ENDIAN); packet.SetBuf(buffer);
        typeof(AionServerPacket).GetMethod("WriteImpl", Fields)!.Invoke(packet, new object?[] {null});
        var bytes = new byte[buffer.Position()]; buffer.Flip(); buffer.Get(bytes); return bytes;
    }
    private sealed class NamedItem : ItemTemplate {public override string GetL10n() => "Product";}
    internal sealed class Scope : IDisposable
    {
        private readonly PvpExpPortTests.Scope common = new();
        internal Aion.GameServer.Tests.Ai.BossAiHarness H => common.H;
        internal Player Player {get;}
        internal PvpExpPortTests.Connection Connection => (PvpExpPortTests.Connection)Player.GetClientConnection();
        internal RecipeTemplate Recipe => DataManager.RECIPE_DATA.GetRecipeTemplateById(9001);
        public Scope()
        {
            Player = common.Player(30); Player.GetCommonData().SetPlayerClass(PlayerClass.GLADIATOR);
            Player.SetRecipeList(new RecipeList()); Player.SetSkillList(new PlayerSkillList(new() {Entry(40009)}));
            Set("ItemDataDh", Read<ItemData>("""
              <item_templates><item_template id="199999901" max_stack_count="100"/><item_template id="199999902" max_stack_count="100"/></item_templates>
              """));
            Set("RecipeDataDh", Read<RecipeData>("""
              <recipe_templates>
                <recipe_template id="9001" skillid="40009" skillpoint="1" max_production_count="3" productid="199999901" quantity="1">
                  <components_data><component itemid="199999902" quantity="2"/></components_data><comboproduct itemid="199999902"/>
                </recipe_template>
                <recipe_template id="9002" skillid="40001" skillpoint="1" productid="199999901" quantity="1"/>
              </recipe_templates>
              """));
        }
        private void Set(string property, object value) => typeof(StaticData).GetField("<" + property + ">k__BackingField", Fields)!.SetValue(H.Data.StaticData, value);
        private static T Read<T>(string xml)
        {
            var data = (T)new XmlSerializer(typeof(T)).Deserialize(new StringReader(xml))!;
            typeof(T).GetMethod("AfterUnmarshal")!.Invoke(data, new object?[] {null}); return data;
        }
        internal void Materials(int count)
        {
            var item = new Item(199999910 + Player.GetInventory().GetItems().Count, DataManager.ITEM_DATA.GetItemTemplate(199999902));
            item.SetItemCount(count); Player.GetInventory().OnLoadHandler(item);
        }
        public void Dispose() {Player.GetInteractionTask()?.Abort(); common.Dispose();}
    }
}
