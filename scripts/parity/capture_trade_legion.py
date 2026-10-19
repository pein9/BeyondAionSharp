"""Capture exact 638ed3dd4 tab filtering/validation, model and three trade packets.
XML annotations/imports alone are stripped. NPC/player/holder/limited-stock/price
collaborators and the little-endian sink are scalar doubles. This does not run
Java's DB, dialog dispatcher or transaction/persistence tail; native tests cover
those C# entry points and the existing purchase/sale guards.
"""
import hashlib,json,os,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
SHA='638ed3dd4032ff01e4691a4ec3d6eb85cdf951d3'
OUT=ROOT/'artifacts/upstream/638ed3dd4-fixed-tradeservice-validatebuyitems-ignored-the/java-capture'
BASE='game-server/src/com/aionemu/gameserver/'
def block(s,sig):
 start=s.index(sig);end=s.index('{',start)+1;depth=1
 while depth:depth+=(s[end]=='{')-(s[end]=='}');end+=1
 return s[start:end]
def main():
 OUT.mkdir(parents=True,exist_ok=True);hashes={}
 def source(path):
  path=BASE+path+'.java';raw=subprocess.check_output(['git','-C',str(Path(os.environ.get('BEYOND_AION_JAVA_ROOT',ROOT.parent/'aion-server'))),'show',SHA+':'+path]);hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode('utf-8')
 for name,path in [('TradeListTemplate','model/templates/tradelist/TradeListTemplate'),('TradeNpcType','model/templates/tradelist/TradeNpcType'),*[(n,'network/aion/serverpackets/'+n) for n in ['SM_TRADELIST','SM_SELL_ITEM','SM_TRADE_IN_LIST']]]:
  s=source(path);s=re.sub(r'^package .*;|^import (?!java\.).*;|^\s*@[^\n]*','',s,flags=re.M);(OUT/(name+'.java')).write_text(s,encoding='utf-8')
 s=source('services/TradeService')
 methods=block(s,'public static List<Integer> getTradeListTabIds(')+'\n'+block(s,'private static boolean validateBuyItems(')
 (OUT/'TradeService.java').write_text('import java.util.*;class TradeService {static final GoodsListData goodsListData=DataManager.GOODSLIST_DATA;\n'+methods+'\nstatic boolean validate(TradeListTemplate t,TradeList l,Player p){return validateBuyItems(t,l,p);}}',encoding='utf-8')
 (OUT/'TradeOracle.java').write_text(r'''
import java.util.*;import java.io.*;import java.lang.reflect.*;
class AionConnection {}
abstract class AionServerPacket {ByteArrayOutputStream out=new ByteArrayOutputStream();void writeC(int i){out.write(i);}void writeH(int i){writeC(i);writeC(i>>>8);}void writeD(int i){writeH(i);writeH(i>>>16);}protected abstract void writeImpl(AionConnection c);String hex(){out.reset();writeImpl(null);return HexFormat.of().withUpperCase().formatHex(out.toByteArray());}}
class Npc {int id=990991,obj=700123;boolean sell,buy,purchase;int getNpcId(){return id;}int getObjectId(){return obj;}boolean canSell(){return sell;}boolean canBuy(){return buy;}boolean canPurchase(){return purchase;}}
class Player {int level=-1;int getObjectId(){return 900123;}Player getLegion(){return level<0?null:this;}int getLegionLevel(){return level;}}
class GoodsList {int level;List<Integer> items;GoodsList(int l,List<Integer> i){level=l;items=i;}int getLegionLevel(){return level;}List<Integer> getItemIdList(){return items;}}
class GoodsListData {Map<Integer,GoodsList> map=new HashMap<>();GoodsList getGoodsListById(int id){return map.get(id);}}
class DataManager {static GoodsListData GOODSLIST_DATA=new GoodsListData();static DataManager TRADE_LIST_DATA=new DataManager();static TradeListTemplate template;TradeListTemplate getPurchaseTemplate(int id){return template;}}
class PricesService {static int getVendorSellModifier(){return 73;}}
class TradeItem {int id;long count;TradeItem(int i,long c){id=i;count=c;}int getItemId(){return id;}long getCount(){return count;}}
class TradeList {List<TradeItem> rows=new ArrayList<>();List<TradeItem> getTradeItems(){return rows;}}
class LimitedItem {int id,buy,sell;LimitedItem(int i,int b,int s){id=i;buy=b;sell=s;}int getItemId(){return id;}int getBuyCount(int player){if(player!=900123)throw new AssertionError();return buy;}int getSellLimit(){return sell;}}
class LimitedTradeNpc {List<LimitedItem> items=new ArrayList<>();List<LimitedItem> getLimitedItems(){return items;}}
class LimitedItemTradeService {static LimitedItemTradeService instance=new LimitedItemTradeService();Map<Integer,LimitedTradeNpc> map=new HashMap<>();static LimitedItemTradeService getInstance(){return instance;}LimitedTradeNpc getLimitedTradeNpc(int id){return map.get(id);}}
public class TradeOracle {
 static void set(Object o,String f,Object v)throws Exception{Field x=o.getClass().getDeclaredField(f);x.setAccessible(true);x.set(o,v);}
 static TradeListTemplate template(TradeNpcType type,int...ids)throws Exception{TradeListTemplate t=new TradeListTemplate();set(t,"npcId",0);set(t,"tradeNpcType",type);set(t,"buyPriceRate",115);t.tradeTablist=new ArrayList<>();for(int id:ids){var tab=new TradeListTemplate.TradeTab();tab.id=id;t.tradeTablist.add(tab);}return t;}
 public static void main(String[]a)throws Exception {
  var goods=DataManager.GOODSLIST_DATA.map;goods.put(101,new GoodsList(0,List.of(162000052,162000053)));goods.put(102,new GoodsList(3,List.of(162000053)));goods.put(103,new GoodsList(7,List.of(162000052)));goods.put(104,new GoodsList(0,null));
  Player p=new Player();Npc n=new Npc();
  for(int level:new int[]{-1,0,2,3,7,8})for(int query=0;query<7;query++){
   p.level=level;var t=template(TradeNpcType.NORMAL,103,999,101,102,101,104);var l=new TradeList();
   if(query>0)l.rows.add(new TradeItem(query==1?162000054:162000053,query==3?0:query==4?-1:query==5?Long.MAX_VALUE:1));
   if(query==6){t.tradeTablist.removeIf(tab->tab.id==101);}
   String ids=TradeService.getTradeListTabIds(t,p).toString().replace(" ","");
   System.out.println("filter|"+level+"|"+query+"|"+ids+"|"+TradeService.validate(t,l,p));
  }
  p.level=-1;
  for(var type:TradeNpcType.values())for(String kind:List.of("buy","sell","tradein")){
   var t=template(type,101,102,101);DataManager.template=t;List<Integer> ids=new ArrayList<>(List.of(101,102,101));
   AionServerPacket packet=kind.equals("buy")?new SM_TRADELIST(p,n,type,ids,83):kind.equals("sell")?new SM_SELL_ITEM(n):new SM_TRADE_IN_LIST(n,t,83);
   System.out.println("packet|"+kind+"|"+type+"|"+packet.hex());
  }
  String[] names={"tradeInListNormal","tradeInListAbyssSingleTab","tradeInListEmptyCount","tradeInListZeroNpcId"};
  for(int i=0;i<4;i++){int[] ids=i==0?new int[]{1,2,3}:i==1?new int[]{42}:i==2?new int[]{}:new int[]{1};var t=template(i==1?TradeNpcType.ABYSS:TradeNpcType.NORMAL,ids);set(t,"npcId",i==3?0:798001);System.out.println("legacy|"+names[i]+"|"+new SM_TRADE_IN_LIST(n,t,i==1?100:80).hex());}
  var stock=new LimitedTradeNpc();stock.items.add(new LimitedItem(162000052,2,9));LimitedItemTradeService.instance.map.put(n.id,stock);
  var ids=new ArrayList<>(List.of(101));var live=new SM_TRADELIST(p,n,TradeNpcType.LEGION_COIN,ids,83);ids.add(102);stock.items.add(new LimitedItem(162000053,65537,65538));n.obj=123;
  System.out.println("special|live|"+live.hex());n.obj=700123;
  var t=template(TradeNpcType.ABYSS_KINAH,101,102,101);DataManager.template=t;var sell=new SM_SELL_ITEM(n);var tradein=new SM_TRADE_IN_LIST(n,t,83);t.tradeTablist.get(0).id=999;t.tradeTablist.clear();set(t,"tradeNpcType",TradeNpcType.NORMAL);
  System.out.println("special|sellSnapshot|"+sell.hex());System.out.println("special|tradeinSnapshot|"+tradein.hex());
  DataManager.template=null;System.out.println("special|sellMissing|"+new SM_SELL_ITEM(n).hex());
  var empty=new TradeListTemplate();var result=empty.getTradeTabIds();if(empty.tradeTablist!=null||!result.isEmpty())throw new AssertionError();
  try{result.add(1);throw new AssertionError();}catch(UnsupportedOperationException expected){}
  t=template(TradeNpcType.NORMAL,1,2,1);result=t.getTradeTabIds();t.tradeTablist.get(0).id=9;t.tradeTablist.clear();if(!result.equals(List.of(1,2,1)))throw new AssertionError();
  try{result.set(0,2);throw new AssertionError();}catch(UnsupportedOperationException expected){}
  try{new SM_TRADE_IN_LIST(n,null,1);throw new AssertionError();}catch(NullPointerException expected){}
  System.out.println("model|passed");
 }
}
''',encoding='utf-8')
 subprocess.run(['javac','-encoding','UTF-8',*[str(f) for f in OUT.glob('*.java')]],check=True)
 lines=subprocess.check_output(['java','-cp',str(OUT),'TradeOracle'],text=True).splitlines()
 filters=[];packets=[];special={};legacy={}
 for line in lines:
  v=line.split('|')
  if v[0]=='filter':filters.append(dict(level=int(v[1]),query=int(v[2]),tabs=json.loads(v[3]),accepted=v[4]=='true'))
  elif v[0]=='packet':packets.append(dict(kind=v[1],npcType=v[2],payloadHex=v[3]))
  elif v[0]=='special':special[v[1]]=v[2]
  elif v[0]=='legacy':legacy[v[1]]=v[2]
  else:assert line=='model|passed'
 fixture=dict(sourceJavaCommit=SHA,sourceHashes=hashes,boundary=__doc__,filterCases=filters,packetCases=packets,special=special,modelChecks=True)
 (ROOT/'parity-artifacts/golden/formulas/TradeLegion.638ed3dd4.json').write_text(json.dumps(fixture,indent=2)+'\n',encoding='utf-8')
 old=json.loads((ROOT/'parity-artifacts/golden/packets/SM_TRADE_IN_LIST.json').read_text(encoding='utf-8-sig'))
 old['sourceJavaCommit']=SHA
 for c in old['cases']:c['payloadHex']=legacy[c['name']]
 (ROOT/'parity-artifacts/golden/packets/SM_TRADE_IN_LIST.638ed3dd4.json').write_text(json.dumps(old,indent=2)+'\n',encoding='utf-8')
 print(f'Captured {len(filters)} Java filter/validation cases, {len(packets)+len(special)+len(legacy)} packet payloads and model snapshot/null checks.')
if __name__=='__main__':main()
