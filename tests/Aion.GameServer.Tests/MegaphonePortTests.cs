using System.Collections;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using Aion.Commons.Nio;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Ban;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class MegaphonePortTests
{
    private const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;

    [Theory]
    [InlineData(188929999,false,Race.ELYOS)]
    [InlineData(188930000,true,Race.ELYOS)]
    [InlineData(188939999,true,Race.ELYOS)]
    [InlineData(188940000,false,Race.ELYOS)]
    [InlineData(188929999,false,Race.ASMODIANS)]
    [InlineData(188930000,true,Race.ASMODIANS)]
    public void RecipientFactionAndWireLabelMatchTheExactItemIdBand(int itemId,bool cross,Race race)
    {
        using var s=new Scope(race);var item=s.Item(itemId,2);var observer=new UseProbe(s.Sender);s.Sender.GetObserveController().AddObserver(observer);
        s.Request(item,"message").Run();
        Assert.Equal(1,item.GetItemCount());Assert.Equal(1,observer.Uses);Assert.True(s.Sender.HasCooldown(item));
        var own=Assert.Single(s.SenderConnection.Packets.OfType<SM_MEGAPHONE>());
        var ally=Assert.Single(s.AllyConnection.Packets.OfType<SM_MEGAPHONE>());
        var expectedLabel=cross?(byte)race.GetRaceId():(byte)255;
        Assert.Equal(expectedLabel,Payload(own)[^1]);Assert.Equal(Payload(own),Payload(ally));
        if(cross)Assert.Equal(Payload(own),Payload(Assert.Single(s.EnemyConnection.Packets.OfType<SM_MEGAPHONE>())));
        else Assert.Empty(s.EnemyConnection.Packets.OfType<SM_MEGAPHONE>());
    }

    [Theory]
    [InlineData(0,false,true)]
    [InlineData(512,false,true)]
    [InlineData(513,false,false)]
    [InlineData(256,true,true)]
    [InlineData(257,true,false)]
    public void MessageLimitCountsUtf16UnitsBeforeUseObserversCooldownAndConsumption(int count,bool surrogatePairs,bool accepted)
    {
        using var s=new Scope();var item=s.Item(188930000,2);var observer=new UseProbe(s.Sender);s.Sender.GetObserveController().AddObserver(observer);
        string text=surrogatePairs?string.Concat(Enumerable.Repeat("🚀",count)):new string('x',count);
        s.Request(item,text).Run();
        Assert.Equal(accepted?1:2,item.GetItemCount());Assert.Equal(accepted?1:0,observer.Uses);Assert.Equal(accepted,s.Sender.HasCooldown(item));
        if(accepted)Assert.Single(s.EnemyConnection.Packets.OfType<SM_MEGAPHONE>());
        else {Assert.Empty(s.SenderConnection.Packets);Assert.Empty(s.AllyConnection.Packets);Assert.Empty(s.EnemyConnection.Packets);}
    }

    [Fact]
    public void ChatBanRejectsMegaphoneWithoutUseSideEffectsAndRetainsBanDeadline()
    {
        using var s=new Scope();var item=s.Item(188930000,2);var observer=new UseProbe(s.Sender);s.Sender.GetObserveController().AddObserver(observer);
        long deadline=s.Common.Now+120_000;s.Bans[s.Sender.GetObjectId()]=deadline;
        s.Request(item,"blocked").Run();
        Assert.Equal(2,item.GetItemCount());Assert.Equal(0,observer.Uses);Assert.False(s.Sender.HasCooldown(item));
        Assert.Equal(deadline,s.Bans[s.Sender.GetObjectId()]);
        Assert.Equal(Payload(SM_SYSTEM_MESSAGE.STR_INGAME_BLOCK_IN_NO_CHAT(2)),Payload(Assert.Single(s.SenderConnection.Packets.OfType<SM_SYSTEM_MESSAGE>())));
        Assert.Empty(s.AllyConnection.Packets.OfType<SM_MEGAPHONE>());Assert.Empty(s.EnemyConnection.Packets.OfType<SM_MEGAPHONE>());
    }

    [Fact]
    public void RemovedItemCannotBroadcastAndGenericUseWithoutAMessageIsRefused()
    {
        using var s=new Scope();var item=s.Item(188930000,1);var action=item.GetItemTemplate().GetActions().GetItemActions().OfType<MegaphoneAction>().Single();
        Assert.False(action.CanAct(s.Sender,item,null!));Assert.False(action.CanAct(s.Sender,item,null!,17));
        Assert.False(action.CanAct(s.Sender,item,null!,new object[] {null!}));
        Assert.True(action.CanAct(s.Sender,item,null!,""));Assert.True(action.CanAct(s.Sender,item,null!,"message",17));
        s.Sender.GetInventory().Delete(item);s.Clear();action.Act(s.Sender,item,null!,"stale");
        Assert.Empty(s.SenderConnection.Packets);Assert.Empty(s.AllyConnection.Packets);Assert.Empty(s.EnemyConnection.Packets);
    }

    private static byte[] Payload(AionServerPacket packet)=>LimitedRecipeMorphPortTests.Payload(packet);
    private sealed class UseProbe(Player p):ItemUseObserver(p)
    {
        internal int Uses;
        protected override void OnAbort()=>Uses++;
    }
    private sealed class Scope:IDisposable
    {
        internal PvpExpPortTests.Scope Common {get;}=new();
        internal Player Sender {get;}
        internal PvpExpPortTests.Connection SenderConnection=>(PvpExpPortTests.Connection)Sender.GetClientConnection();
        internal PvpExpPortTests.Connection AllyConnection {get;}=new();
        internal PvpExpPortTests.Connection EnemyConnection {get;}=new();
        internal IDictionary Bans {get;}=(IDictionary)typeof(ChatBanService).GetField("chatBans",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
        private readonly Player ally,enemy;
        public Scope(Race race=Race.ELYOS)
        {
            Common.Now=100_000;Sender=Common.Player(30);Sender.GetCommonData().SetRace(race);
            ally=Common.H.SpawnPlayer(race:race);enemy=Common.H.SpawnPlayer(race:race==Race.ELYOS?Race.ASMODIANS:Race.ELYOS);
            Connect(ally,AllyConnection);Connect(enemy,EnemyConnection);
            string xml="<item_templates>"+string.Concat(new[]{188929999,188930000,188939999,188940000}.Select(id=>$"<item_template id='{id}' max_stack_count='100'><uselimits usedelay='2' usedelayid='7001'/><actions><megaphone color='FFFFFF'/></actions></item_template>"))+"</item_templates>";
            var items=(ItemData)new XmlSerializer(typeof(ItemData)).Deserialize(new StringReader(xml))!;items.AfterUnmarshal(null!);
            typeof(StaticData).GetField("<ItemDataDh>k__BackingField",Fields)!.SetValue(Common.H.Data.StaticData,items);Clear();
        }
        private static void Connect(Player p,PvpExpPortTests.Connection c){c.SetActivePlayer(p);c.SetState(AionConnection.State.IN_GAME);p.SetClientConnection(c);}
        internal Item Item(int id,int count){var item=new Item(199999901,DataManager.ITEM_DATA.GetItemTemplate(id));item.SetItemCount(count);Sender.GetInventory().OnLoadHandler(item);return item;}
        internal CM_MEGAPHONE Request(Item item,string message)
        {
            var text=Encoding.Unicode.GetBytes(message+"\0");var body=new byte[text.Length+4];text.CopyTo(body,0);System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(text.Length),item.GetObjectId());
            var packet=new CM_MEGAPHONE(0,new HashSet<AionConnection.State>{AionConnection.State.IN_GAME});packet.SetConnection(SenderConnection);packet.SetBuffer(ByteBuffer.Wrap(body).Order(ByteOrder.LITTLE_ENDIAN));Assert.True(packet.Read());Assert.Equal(0,packet.GetRemainingBytes());return packet;
        }
        internal void Clear(){SenderConnection.Clear();AllyConnection.Clear();EnemyConnection.Clear();}
        public void Dispose(){Bans.Remove(Sender.GetObjectId());Sender.GetController().CancelTask(TaskId.GAG);ally.SetClientConnection(null!);enemy.SetClientConnection(null!);Common.Dispose();}
    }
}
