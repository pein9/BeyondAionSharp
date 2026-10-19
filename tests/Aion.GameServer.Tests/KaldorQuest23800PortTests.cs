using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Handlers.Models;
using Aion.GameServer.QuestEngine.Handlers.Template;
using Aion.GameServer.QuestEngine.Model;
using Engine = Aion.GameServer.QuestEngine.QuestEngine;

namespace Aion.GameServer.Tests;

// Java 616cb3c040172a1e8cc310c2dcf94d1071a60687 changes only this report NPC
// (plus XML whitespace). Use the production XML loader and unchanged template handler.
[Collection("GoldenDataManager")]
public sealed class KaldorQuest23800PortTests
{
    private const int Quest = 23800;
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private static ReportToManyData Load()
    {
        using var file = File.OpenRead(Path.Combine(PvpExpPortTests.Root(), "game-server/data/static_data/quest_script_data/kaldor.xml"));
        var data = (XMLQuests)new XmlSerializer(typeof(XMLQuests)).Deserialize(file)!;
        data.AfterUnmarshal(null!);
        return Assert.IsType<ReportToManyData>(data.GetQuest(Quest));
    }

    [Fact]
    public void ProductionLoaderRetainsTheStartAndOrderedReportNpcs()
    {
        var data = Load();
        Assert.Equal("804719", data.StartNpcIdsRaw);
        Assert.Equal(new[] { 804753, 802433 }, data.npcInfos.SelectMany(n => n.GetNpcIds()).ToArray());
    }

    [Fact]
    public void ProductionRegistrationUsesTheReplacementNpcAndKeepsStartAndEndHooks()
    {
        using var s = new Scope();
        Assert.Contains(Quest, s.Engine.GetQuestNpc(804719).GetOnQuestStart());
        Assert.Contains(Quest, s.Engine.GetQuestNpc(804719).GetOnTalkEvent());
        Assert.Contains(Quest, s.Engine.GetQuestNpc(804753).GetOnTalkEvent());
        Assert.Contains(Quest, s.Engine.GetQuestNpc(802433).GetOnTalkEvent());
        Assert.DoesNotContain(Quest, s.Engine.GetQuestNpc(800980).GetOnTalkEvent());
    }

    [Theory]
    [InlineData(0, 804753, true)] [InlineData(0, 800980, false)] [InlineData(0, 802433, false)]
    [InlineData(1, 804753, false)] [InlineData(1, 800980, false)] [InlineData(1, 802433, true)]
    public void OnlyTheNpcForTheCurrentReportStepCanOpenTheDialog(int step, int npc, bool allowed)
    {
        using var s = new Scope(); s.State.SetQuestVarById(0, step);
        Assert.Equal(allowed, s.Dialog(npc, DialogAction.QUEST_SELECT));
        Assert.Equal(step, s.State.GetQuestVarById(0));
        var dialogs = s.Packets.OfType<SM_DIALOG_WINDOW>().ToArray();
        Assert.Equal(allowed ? 1 : 0, dialogs.Length);
        if (allowed)
        {
            int page = step == 0 ? s.DataDriven ? 1011 : 1352 : s.DataDriven ? 10002 : 2375;
            Assert.Equal(page, Value<int>(dialogs[0], "dialogPageId"));
            Assert.Equal(Quest, Value<int>(dialogs[0], "questId"));
        }
    }

    [Fact]
    public void FirstReportAdvancesAtTheNewNpcAndThenSelectsTheUnchangedFinalNpc()
    {
        using var s = new Scope();
        Assert.False(s.Dialog(800980, DialogAction.SETPRO1)); Assert.Equal(0, s.State.GetQuestVarById(0));
        Assert.True(s.Dialog(804753, DialogAction.SETPRO1)); Assert.Equal(1, s.State.GetQuestVarById(0));
        Assert.Equal(QuestStatus.START, s.State.GetStatus()); Assert.Single(s.Packets.OfType<SM_QUEST_ACTION>());
        Assert.Equal(1, s.Player.GetInventory().GetItemCountByItemId(182215490));
        Assert.True(s.Dialog(802433, DialogAction.QUEST_SELECT)); Assert.Equal(1, s.State.GetQuestVarById(0));
    }

    private static T Value<T>(object obj, string field) => (T)obj.GetType().GetField(field, Fields)!.GetValue(obj)!;
    private sealed class Scope : IDisposable
    {
        private readonly PvpExpPortTests.Scope common = new();
        public Engine Engine { get; } = Engine.GetInstance();
        public Player Player { get; }
        public QuestState State { get; } = new(Quest, QuestStatus.START);
        public ReportToMany Handler { get; }
        public bool DataDriven { get; }
        public Aion.GameServer.Network.Aion.AionServerPacket[] Packets => ((PvpExpPortTests.Connection)Player.GetClientConnection()).Packets;
        private readonly Dictionary<int, AbstractQuestHandler> handlers;
        private readonly Dictionary<int, QuestNpc> npcs;
        private readonly Dictionary<int, List<int>> canAct;
        private readonly AbstractQuestHandler? previousHandler;
        private readonly Dictionary<int, QuestNpc?> previousNpcs = new();
        private readonly Dictionary<int, List<int>?> previousCanAct = new();
        public Scope()
        {
            var element = XDocument.Load(Path.Combine(PvpExpPortTests.Root(), "game-server/data/static_data/quest_data/quest_data.xml"))
                .Root!.Elements("quest").Single(q => (int?)q.Attribute("id") == Quest);
            using var input = new XElement("quests", new XElement(element)).CreateReader();
            var quests = (QuestsData)new XmlSerializer(typeof(QuestsData)).Deserialize(input)!;
            quests.AfterUnmarshal(null!);
            typeof(StaticData).GetProperty(nameof(StaticData.Quests))!.SetValue(common.H.Data.StaticData, quests);
            using (var reader = XmlReader.Create(Path.Combine(PvpExpPortTests.Root(), "game-server/data/static_data/items/item_templates.xml")))
            {
                while (reader.Read())
                {
                    if (reader.NodeType != XmlNodeType.Element || reader.Name != "item_template" || reader.GetAttribute("id") != "182215490") continue;
                    using var subtree = reader.ReadSubtree();
                    using var itemsInput = new XElement("item_templates", XElement.Load(subtree)).CreateReader();
                    var items = (ItemData)new XmlSerializer(typeof(ItemData)).Deserialize(itemsInput)!; items.AfterUnmarshal(null!);
                    typeof(StaticData).GetProperty(nameof(StaticData.ItemDataDh))!.SetValue(common.H.Data.StaticData, items);
                    break;
                }
            }
            Assert.NotNull(DataManager.ITEM_DATA.GetItemTemplate(182215490));
            DataDriven = quests.GetQuestById(Quest).IsDataDriven();
            handlers = Value<Dictionary<int, AbstractQuestHandler>>(Engine, "questHandlers");
            npcs = Value<Dictionary<int, QuestNpc>>(Engine, "questNpcs");
            canAct = Value<Dictionary<int, List<int>>>(Engine, "questCanAct");
            previousHandler = handlers.GetValueOrDefault(Quest); handlers.Remove(Quest);
            foreach (int id in new[] { 804719, 804753, 800980, 802433 })
            {
                previousNpcs[id] = npcs.GetValueOrDefault(id); npcs.Remove(id);
                previousCanAct[id] = canAct.GetValueOrDefault(id); canAct.Remove(id);
            }
            Load().Register(Engine);
            Handler = Assert.IsType<ReportToMany>(handlers[Quest]);
            Player = common.Player(65); Player.GetInventory().SetLimit(10);
            Assert.True(Player.GetQuestStateList().AddQuest(Quest, State));
        }
        public bool Dialog(int npcId, int action)
        {
            ((PvpExpPortTests.Connection)Player.GetClientConnection()).Clear();
            // The handler reads only this target's template id and object id; no spawned-NPC journey is claimed.
            var npc = (Npc)RuntimeHelpers.GetUninitializedObject(typeof(Npc));
            typeof(VisibleObject).GetField("_objectTemplate", Fields)!.SetValue(npc, new NpcTemplate { npcId = npcId });
            return Handler.OnDialogEvent(new QuestEnv(npc, Player, Quest, action));
        }
        public void Dispose()
        {
            if (previousHandler == null) handlers.Remove(Quest); else handlers[Quest] = previousHandler;
            foreach (var entry in previousNpcs) { if (entry.Value == null) npcs.Remove(entry.Key); else npcs[entry.Key] = entry.Value; }
            foreach (var entry in previousCanAct) { if (entry.Value == null) canAct.Remove(entry.Key); else canAct[entry.Key] = entry.Value; }
            common.Dispose();
        }
    }
}
