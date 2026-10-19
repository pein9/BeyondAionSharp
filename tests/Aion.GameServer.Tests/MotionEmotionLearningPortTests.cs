using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml.Serialization;
using Aion.Commons.Database;
using Aion.Commons.Nio;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.Players.Emotion;
using Aion.GameServer.Model.GameObjects.Players.Motion;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Taskmanager.Tasks;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class MotionEmotionLearningPortTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void AllPacketActionsMatchTheTargetJavaCaptureIncludingLargeTimesAndExpirationSnapshot()
    {
        using var scope = new Scope();
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "parity-artifacts/golden/packets/MotionEmotion.f7230c5fa.json")));
        Assert.Equal("f7230c5fa470511f793c1699cc6beeb650e98f94", json.RootElement.GetProperty("sourceJavaCommit").GetString());
        foreach (var c in json.RootElement.GetProperty("cases").EnumerateArray())
        {
            var input = c.GetProperty("inputs"); AionServerPacket packet;
            int Expire(int seconds) => seconds == 0 ? 0 : unchecked((int)SystemClock.CurrentSeconds() + seconds);
            switch (c.GetProperty("kind").GetString())
            {
                case "emotion":
                    var elements = input.GetProperty("emotions");
                    packet = new SM_EMOTION_LIST(Enum.Parse<SM_EMOTION_LIST.Action>(input.GetProperty("action").GetString()!),
                        elements.ValueKind == JsonValueKind.Null ? null : elements.EnumerateArray().Select(e => new Emotion(e.GetProperty("id").GetInt32(), Expire(e.GetProperty("seconds").GetInt32()))).ToList());
                    break;
                case "list":
                    packet = SM_MOTION.List(input.GetProperty("motions").EnumerateArray().Select(m => new Motion(m.GetProperty("id").GetInt32(), Expire(m.GetProperty("seconds").GetInt32()), m.GetProperty("active").GetBoolean())).ToList());
                    break;
                case "add":
                    packet = SM_MOTION.Add(new Motion(input.GetProperty("id").GetInt32(), Expire(input.GetProperty("seconds").GetInt32()), false));
                    scope.H.Clock.Advance(TimeSpan.FromSeconds(1)); // ADD snapshots time, unlike LIST
                    break;
                case "set": packet = SM_MOTION.Set(input.GetProperty("id").GetInt32(), MotionTypeExtensions.GetById(input.GetProperty("slot").GetInt32())!.Value); break;
                case "remove": packet = SM_MOTION.Remove(input.GetProperty("id").GetInt32()); break;
                case "player":
                    var player = new GoldenPlayerFormulaFixtureTests.HarnessPlayer(1, Race.ELYOS, Aion.GameServer.Controllers.Movement.MovementModifierDirection.NONE, new(), 1);
                    player.SetMotions(new MotionList(player));
                    var active = new SortedDictionary<MotionType, Motion>();
                    foreach (var slot in input.GetProperty("slots").EnumerateObject()) active[Enum.Parse<MotionType>(slot.Name)] = new Motion(slot.Value.GetInt32(), 0, true);
                    typeof(MotionList).GetField("activeMotions", PrivateInstance)!.SetValue(player.GetMotions(), active);
                    Assert.Equal(input.GetProperty("id").GetInt32(), player.GetObjectId()); packet = SM_MOTION.PlayerMotions(player);
                    break;
                default: throw new InvalidOperationException();
            }
            Assert.True(c.GetProperty("payloadHex").GetString() == Convert.ToHexString(Payload(packet)), c.GetProperty("name").GetString());
        }
    }

    [Fact]
    public void ItemIndexesUseEveryActionAndLastMotionSlotWinsWithoutStaticEmotionLeakage()
    {
        using var scope = new Scope();
        var first = ReadItems("""
            <item_templates>
              <item_template id="1"><actions><animation idle="900" run="900"/><learnemotion emotionid="155"/><learnemotion emotionid="64"/></actions></item_template>
              <item_template id="2"><actions><animation rest="900"/><learnemotion emotionid="64"/></actions></item_template>
            </item_templates>
            """);
        Assert.Equal(MotionType.REST, first.GetMotionType(900)); Assert.Null(first.GetMotionType(901));
        Assert.Equal(new[] {64,155}, first.GetLearnableEmotionIds()); Assert.True(first.IsLearnableEmotion(155));
        var second = ReadItems("<item_templates><item_template id='3'><actions><learnemotion emotionid='77'/></actions></item_template></item_templates>");
        Assert.Equal(new[] {77}, second.GetLearnableEmotionIds()); Assert.False(second.IsLearnableEmotion(64));
        scope.SetItems(second); Assert.True(scope.Player.GetEmotions().CanUse(64)); Assert.False(scope.Player.GetEmotions().CanUse(77));
        scope.Player.GetEmotions().Add(77, 0, false); Assert.True(scope.Player.GetEmotions().CanUse(77));
        Assert.True(scope.Player.GetEmotions().CanUse(1)); Assert.True(scope.Player.GetEmotions().CanUse(10006));
        Assert.Null(MotionTypeExtensions.GetById(0)); Assert.Null(MotionTypeExtensions.GetById(6));
        Assert.Equal(new[] {1,2,3,4,5}, Enum.GetValues<MotionType>().Select(t => t.GetId()));
    }

    [Fact]
    public void ActivationUsesTheLearnedSlotAndRepeatedSelectionsStillReplyWithoutDeactivatingIt()
    {
        using var scope = new Scope(); var list = scope.Player.GetMotions();
        var motion = new Motion(1001, 0, false); list.Add(motion, false);
        list.SetActive(1001, MotionType.SHOP);
        Assert.Same(motion, list.GetActiveMotions()[MotionType.IDLE]); Assert.False(list.GetActiveMotions().ContainsKey(MotionType.SHOP));
        Assert.Equal("05E90305", Convert.ToHexString(Payload(scope.Connection.Packets.OfType<SM_MOTION>().First())));
        scope.Connection.Clear(); list.SetActive(1001, MotionType.SHOP);
        Assert.True(motion.IsActive()); Assert.Equal(2, scope.Connection.Packets.OfType<SM_MOTION>().Count());
        scope.Connection.Clear(); list.SetActive(0, MotionType.REST); // clearing an empty slot still answers
        Assert.Equal(2, scope.Connection.Packets.OfType<SM_MOTION>().Count()); Assert.True(motion.IsActive());
        list.SetActive(0, MotionType.IDLE); Assert.False(motion.IsActive()); Assert.Empty(list.GetActiveMotions());
    }

    [Fact]
    public void RemovingAStaleActiveMotionCannotRemoveItsReplacementAndUnknownSlotsAreSafe()
    {
        using var scope = new Scope(); var list = scope.Player.GetMotions();
        var first = new Motion(1001, 0, true); var replacement = new Motion(1006, 0, true);
        list.Add(first, false); list.Add(replacement, false); Assert.False(first.IsActive());
        first.SetActive(true); Assert.True(list.Remove(1001)); Assert.Same(replacement, list.GetActiveMotions()[MotionType.IDLE]);
        var unknown = new Motion(999999, 0, true); var other = new MotionList(scope.Player); other.Add(unknown, false);
        Assert.Empty(other.GetActiveMotions()); Assert.True(other.Remove(999999)); Assert.False(other.Remove(999999));
    }

    [Theory]
    [InlineData(4, 5, true, 2)]
    [InlineData(3, 1, true, 0)]
    [InlineData(255, 1, true, 0)]
    [InlineData(4, 0, true, 0)]
    [InlineData(4, 6, true, 0)]
    [InlineData(4, 1, false, 0)]
    public void MotionRequestsAcceptOnlyActivationAndKnownSlotsForLearnedMotions(int request, int slot, bool learned, int replies)
    {
        using var scope = new Scope(); if (learned) scope.Player.GetMotions().Add(new Motion(1001, 0, false), false);
        var packet = new CM_MOTION(1, new HashSet<AionConnection.State> {AionConnection.State.IN_GAME});
        packet.SetConnection(scope.Connection); packet.SetBuffer(ByteBuffer.Wrap(new byte[] {(byte)request,0xE9,3,(byte)slot}).Order(ByteOrder.LITTLE_ENDIAN));
        Assert.True(packet.Read()); packet.Run(); Assert.Equal(replies, scope.Connection.Packets.OfType<SM_MOTION>().Count());
        Assert.Equal(replies > 0, scope.Player.GetMotions().GetActiveMotions().ContainsKey(MotionType.IDLE));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void MotionBooksRejectAnyAlreadyLearnedMemberBeforeCasting(int expiry)
    {
        using var scope = new Scope(); var book = scope.Book(199999901); var action = scope.Animation(book);
        scope.Player.GetMotions().Add(new Motion(1002, expiry, false), false);
        Assert.False(action.CanAct(scope.Player, book, null!)); Assert.Equal(1, book.GetItemCount());
        Assert.Equal(Payload(SM_SYSTEM_MESSAGE.STR_MSG_CUSTOMANIMATION_ALREADY_HAS_MOTION()), Payload(Assert.Single(scope.Connection.Packets.OfType<SM_SYSTEM_MESSAGE>())));
    }

    [Fact]
    public void DuplicateLearnedDuringCastFailsWithoutConsumingTheBook()
    {
        using var scope = new Scope(); var book = scope.Book(199999901); var action = scope.Animation(book);
        Assert.True(action.CanAct(scope.Player, book, null!)); action.Act(scope.Player, book, null!);
        scope.Player.GetMotions().Add(new Motion(1001, 0, false), false); scope.Connection.Clear();
        scope.H.Clock.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(1, book.GetItemCount()); Assert.NotNull(scope.Player.GetInventory().GetItemByObjId(book.GetObjectId()));
        Assert.Single(scope.Player.GetMotions().GetMotions());
        Assert.Equal(Payload(SM_SYSTEM_MESSAGE.STR_ITEM_IS_NOT_USABLE()), Payload(Assert.Single(scope.Connection.Packets.OfType<SM_SYSTEM_MESSAGE>())));
        Assert.Equal(Payload(new SM_ITEM_USAGE_ANIMATION(scope.Player.GetObjectId(), book.GetObjectId(), book.GetItemId(), 0, Aion.GameServer.Model.Items.ItemUseAnimation.USE_FAIL)), Payload(Assert.Single(scope.Connection.Packets.OfType<SM_ITEM_USAGE_ANIMATION>())));
    }

    [Fact]
    public void CompletedBookLearnsAllSlotsInOrderAndUsesEpochSecondsForExpiration()
    {
        using var scope = new Scope(); var book = scope.Book(199999901); var action = scope.Animation(book);
        action.Act(scope.Player, book, null!); scope.H.Clock.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Null(scope.Player.GetInventory().GetItemByObjId(book.GetObjectId()));
        Assert.Equal(new[] {1001,1002}, scope.Player.GetMotions().GetMotions().Keys);
        Assert.All(scope.Player.GetMotions().GetMotions().Values, m => { Assert.True(m.IsActive()); Assert.Equal(1060, m.GetExpireTime()); });
        Assert.Equal(new[] {MotionType.IDLE,MotionType.RUN}, scope.Player.GetMotions().GetActiveMotions().Keys);
        Assert.Equal(2, scope.Connection.Packets.OfType<SM_MOTION>().Count(p => Payload(p)[0] == 2));
        Assert.Empty(scope.H.Clock.Faults);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(0, 1000)]
    public void MissingMotionDurationIsPermanentWhileExplicitZeroUsesTheCurrentEpoch(int? minutes, int expires)
    {
        using var scope = new Scope(); var book = scope.Book(199999902); var action = scope.Animation(book);
        action.minutes = minutes; action.Act(scope.Player,book,null!);
        Assert.Equal(3,scope.Player.GetMotions().GetMotions().Count);
        Assert.All(scope.Player.GetMotions().GetMotions().Values,m => Assert.Equal(expires,m.GetExpireTime()));
    }

    [Fact]
    public void EmotionBookLearnsAndSendsTheNewAddShapeWithFullRemainingSeconds()
    {
        using var scope = new Scope(); var book = scope.Book(199999904);
        var action = book.GetItemTemplate().GetActions().GetItemActions().OfType<EmotionLearnAction>().Single();
        Assert.True(action.CanAct(scope.Player,book,null!)); action.Act(scope.Player,book,null!);
        Assert.True(scope.Player.GetEmotions().CanUse(77));
        Assert.Equal(1060,Assert.Single(scope.Player.GetEmotions().GetEmotions()).GetExpireTime());
        Assert.Null(scope.Player.GetInventory().GetItemByObjId(book.GetObjectId()));
        Assert.Equal("0101004D003C000000",Convert.ToHexString(Payload(Assert.Single(scope.Connection.Packets.OfType<SM_EMOTION_LIST>()))));
    }

    [Fact]
    public void AbortingBookUseKeepsTheBookAndDoesNotLearnAnything()
    {
        using var scope = new Scope(); var book = scope.Book(199999901); scope.Animation(book).Act(scope.Player, book, null!);
        scope.Player.GetObserveController().NotifyMoveObservers(); scope.H.Clock.Advance(TimeSpan.FromMilliseconds(200));
        Assert.Equal(1, book.GetItemCount()); Assert.Empty(scope.Player.GetMotions().GetMotions());
        Assert.Empty(scope.H.Clock.Faults);
    }

    [Fact]
    public void ExpirySendsClientRemovalPacketsWithoutBlankSystemMessagesAndLeavesPermanentEntries()
    {
        using var scope = new Scope(); var player = scope.Player;
        player.GetEmotions().Add(77, 1001, false); player.GetEmotions().Add(64, 0, false);
        var timedMotion = new Motion(1001,1001,true); player.GetMotions().Add(timedMotion,false);
        var permanent = new Motion(1002,0,true); player.GetMotions().Add(permanent,false);
        var timer = new ExpireTimerTask();
        timer.RegisterExpirable(timedMotion,player);
        timer.RegisterExpirable(player.GetEmotions().GetEmotions().Single(e => e.GetId()==77),player);
        timer.RegisterExpirable(permanent,player);
        scope.H.Clock.Advance(TimeSpan.FromMilliseconds(1500)); Assert.True(player.GetEmotions().Contains(77));
        scope.Connection.Clear(); scope.H.Clock.Advance(TimeSpan.FromMilliseconds(1100));
        Assert.False(player.GetEmotions().Contains(77)); Assert.True(player.GetEmotions().Contains(64));
        Assert.False(player.GetMotions().GetMotions().ContainsKey(1001)); Assert.Same(permanent,player.GetMotions().GetActiveMotions()[MotionType.RUN]);
        Assert.Equal("0201004D0000000000",Convert.ToHexString(Payload(Assert.Single(scope.Connection.Packets.OfType<SM_EMOTION_LIST>()))));
        Assert.Equal("06E903",Convert.ToHexString(Payload(Assert.Single(scope.Connection.Packets.OfType<SM_MOTION>()))));
        Assert.Empty(scope.Connection.Packets.OfType<SM_SYSTEM_MESSAGE>());
        scope.Connection.Clear(); player.GetEmotions().Remove(99999); Assert.Empty(scope.Connection.Packets);
    }

    [Fact]
    public void DuplicateEmotionBookUsesTheCorrectSystemMessage()
    {
        using var scope = new Scope(); var book = scope.Book(199999904);
        scope.Player.GetEmotions().Add(77,0,false);
        Assert.False(new EmotionLearnAction { emotionId=77 }.CanAct(scope.Player,book,null!));
        Assert.Equal(Payload(SM_SYSTEM_MESSAGE.STR_MSG_SOCIALACTION_ALREADY_HAS_SKILL()),Payload(Assert.Single(scope.Connection.Packets.OfType<SM_SYSTEM_MESSAGE>())));
    }

    private static ItemData ReadItems(string xml)
    {
        var data = (ItemData)new XmlSerializer(typeof(ItemData)).Deserialize(new StringReader(xml))!;
        data.AfterUnmarshal(null!); return data;
    }
    private static byte[] Payload(AionServerPacket packet)
    {
        var buffer=ByteBuffer.Allocate(8192).Order(ByteOrder.LITTLE_ENDIAN); packet.SetBuf(buffer);
        typeof(AionServerPacket).GetMethod("WriteImpl",PrivateInstance)!.Invoke(packet,new object?[]{null});
        var bytes=new byte[buffer.Position()]; buffer.Flip(); buffer.Get(bytes); return bytes;
    }
    private static string Root()
    {
        var dir=new DirectoryInfo(AppContext.BaseDirectory);
        while(dir!=null && !File.Exists(Path.Combine(dir.FullName,"AionServer.slnx"))) dir=dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException();
    }
    private sealed class Connection() : AionConnection("127.0.0.1")
    {
        public AionServerPacket[] Packets=>GetSendMsgQueue().ToArray();
        public void Clear()=>GetSendMsgQueue().Clear();
    }
    private sealed class Scope : IDisposable
    {
        public BossAiHarness H {get;} = BossAiHarness.For().Build();
        public Player Player {get;}
        public Connection Connection {get;} = new();
        private readonly FieldInfo source=typeof(DatabaseFactory).GetField("_dataSource",BindingFlags.Static|BindingFlags.NonPublic)!;
        private readonly object? previousSource;
        public Scope()
        {
            previousSource=source.GetValue(null); source.SetValue(null,null); // DAO calls fail locally; no real database can be opened.
            SystemClock.UseSource(()=>1_000_000+H.Clock.NowMillis);
            SetItems(ReadItems("""
                <item_templates>
                  <item_template id="199999901" casting_delay="100"><actions><animation idle="1001" run="1002" minutes="1"/></actions></item_template>
                  <item_template id="199999902"><actions><animation jump="1003" rest="1004" shop="1005"/></actions></item_template>
                  <item_template id="199999903"><actions><animation idle="1006"/></actions></item_template>
                  <item_template id="199999904"><actions><learnemotion emotionid="77" minutes="1"/></actions></item_template>
                  <item_template id="199999905"><actions><learnemotion emotionid="64"/></actions></item_template>
                </item_templates>
                """));
            Player=H.SpawnPlayer(); Player.SetMotions(new MotionList(Player)); Player.SetEmotions(new EmotionList(Player));
            Connection.SetActivePlayer(Player); Connection.SetState(AionConnection.State.IN_GAME); Player.SetClientConnection(Connection);
        }
        public void SetItems(ItemData data)=>typeof(StaticData).GetField("<ItemDataDh>k__BackingField",PrivateInstance)!.SetValue(H.Data.StaticData,data);
        public Item Book(int id)
        {
            var item=new Item(id,DataManager.ITEM_DATA.GetItemTemplate(id)); Player.GetInventory().OnLoadHandler(item); return item;
        }
        public AnimationAddAction Animation(Item item)=>item.GetItemTemplate().GetActions().GetItemActions().OfType<AnimationAddAction>().Single();
        public void Dispose()
        {
            ExpireTimerTask.GetInstance().UnregisterExpirables(Player); Player.SetClientConnection(null!);
            source.SetValue(null,previousSource); H.Dispose();
        }
    }
}
