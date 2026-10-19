using System.Reflection;
using System.Runtime.CompilerServices;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Tests.Ai;
using Engine = Aion.GameServer.QuestEngine.QuestEngine;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class QuestlessSetproDialogPortTests
{
    private const int ProbeQuest = 1999999;
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData(DialogAction.SETPRO1 - 1, false, true, false)]
    [InlineData(DialogAction.SETPRO1, false, true, true)]
    [InlineData(DialogAction.SETPRO1 + 100, false, true, true)]
    [InlineData(DialogAction.SETPRO1, false, false, true)]
    [InlineData(DialogAction.USE_OBJECT, false, true, true)]
    [InlineData(DialogAction.EXCHANGE_COIN, false, true, true)]
    [InlineData(DialogAction.QUEST_SELECT, true, true, true)]
    public void DispatchesSetproWithoutQuestIdAndPreservesExistingDispatchAndPageFallback(int action, bool explicitQuest, bool handles, bool dispatches)
    {
        using var h = BossAiHarness.For().Build();
        var player = h.SpawnPlayer(); var npc = h.Spawn(217000);
        var connection = new Connection(); connection.SetActivePlayer(player); player.SetClientConnection(connection);
        var engine = Engine.GetInstance();
        var handlers = (Dictionary<int, AbstractQuestHandler>)typeof(Engine).GetField("questHandlers", PrivateInstance)!.GetValue(engine)!;
        var npcs = (Dictionary<int, QuestNpc>)typeof(Engine).GetField("questNpcs", PrivateInstance)!.GetValue(engine)!;
        bool hadNpc = npcs.TryGetValue(npc.GetNpcId(), out var previousNpc);
        var handler = (Probe)RuntimeHelpers.GetUninitializedObject(typeof(Probe));
        typeof(AbstractQuestHandler).GetField("questId", PrivateInstance)!.SetValue(handler, ProbeQuest);
        handler.Handles = handles;
        Assert.True(handlers.TryAdd(ProbeQuest, handler));
        var registration = new QuestNpc(npc.GetNpcId()); registration.AddOnTalkEvent(ProbeQuest);
        npcs[npc.GetNpcId()] = registration;
        try
        {
            int questId = explicitQuest ? ProbeQuest : 0;
            typeof(DialogService).GetMethod("HandleQuestDialogueOrSendNextPage", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, new object[] { action, player, npc, questId, 7 });
            Assert.Equal(dispatches ? 1 : 0, handler.Calls);
            if (dispatches)
            {
                Assert.Equal(action, handler.Action); Assert.Equal(ProbeQuest, handler.QuestIdAtDispatch);
                Assert.Equal(7, handler.RewardIndex); Assert.Same(player, handler.Player); Assert.Same(npc, handler.Npc);
            }
            if (dispatches && handles) Assert.Empty(connection.Packets);
            else
            {
                var page = Assert.IsType<SM_DIALOG_WINDOW>(Assert.Single(connection.Packets));
                Assert.Equal(action, Field<int>(page, "dialogPageId"));
                Assert.Equal(questId, Field<int>(page, "questId"));
                Assert.Equal(npc.GetObjectId(), Field<int>(page, "targetObjectId"));
            }
        }
        finally
        {
            handlers.Remove(ProbeQuest);
            if (hadNpc) npcs[npc.GetNpcId()] = previousNpc!; else npcs.Remove(npc.GetNpcId());
            player.SetClientConnection(null!);
        }
    }

    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, PrivateInstance)!.GetValue(instance)!;
    private sealed class Connection() : AionConnection("127.0.0.1")
    {
        public AionServerPacket[] Packets => GetSendMsgQueue().ToArray();
    }
    private sealed class Probe : AbstractQuestHandler
    {
        private Probe() : base(ProbeQuest) { }
        public bool Handles;
        public int Calls, Action, QuestIdAtDispatch, RewardIndex;
        public Player? Player;
        public VisibleObject? Npc;
        public override void Register() { }
        public override bool OnDialogEvent(QuestEnv env)
        {
            Calls++; Action = env.GetDialogActionId(); QuestIdAtDispatch = env.GetQuestId(); RewardIndex = env.GetExtendedRewardIndex();
            Player = env.GetPlayer(); Npc = env.GetVisibleObject(); return Handles;
        }
    }
}
