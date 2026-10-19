"""Capture e2b0c35e2's exact trade-list calculation and CM_BUY_ITEM reader/enums with explicit boundary doubles."""
import hashlib,json,re,struct,subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
SHA='e2b0c35e2bd364366821db65cdfa274a38aa71b8'
BASE='game-server/src/com/aionemu/gameserver/'
PKG=ROOT/'artifacts/upstream/e2b0c35e2-fixed-npc-shop-exploits/java-capture'

def block(text,signature):
    start=text.index(signature);end=text.index('{',start)+1;depth=1
    while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
    return text[start:end]

def main():
    PKG.mkdir(parents=True,exist_ok=True);texts={};hashes={}
    for name,path in [('TradeList','model/trade/TradeList'),('CM_BUY_ITEM','network/aion/clientpackets/CM_BUY_ITEM')]:
        file=BASE+path+'.java';raw=subprocess.check_output(['git','-C',str(ROOT.parent/'aion-server'),'show',SHA+':'+file]);texts[name]=raw.decode('utf-8');hashes[file]=hashlib.sha256(raw).hexdigest()
    (PKG/'TradeList.java').write_text(re.sub(r'^package .*;|^import (?!java\.).*;','',texts['TradeList'],flags=re.M),encoding='utf-8')
    read=block(texts['CM_BUY_ITEM'],'protected void readImpl()')
    enums=block(texts['CM_BUY_ITEM'],'private enum TradeAction')+'\n'+block(texts['CM_BUY_ITEM'],'private enum EntryType')
    (PKG/'ShopOracle.java').write_text('''import java.util.*;import java.nio.*;
public class ShopOracle {
    int sellerObjId;TradeAction tradeAction;boolean isAudit;TradeList tradeList;RepurchaseList repurchaseList;
    ByteBuffer buffer;Player player=new Player();
    ShopOracle(byte[] body){buffer=ByteBuffer.wrap(body).order(ByteOrder.LITTLE_ENDIAN);}
    ShopOracle getConnection(){return this;}Player getActivePlayer(){return player;}
    int readD(){return buffer.getInt();}int readUC(){return Byte.toUnsignedInt(buffer.get());}
    byte readC(){return buffer.get();}int readUH(){return Short.toUnsignedInt(buffer.getShort());}long readQ(){return buffer.getLong();}
'''+read+'\n'+enums+'''
    public static void main(String[]args){
        if(args[0].equals("packet")){
            ShopOracle p=new ShopOracle(HexFormat.of().parseHex(args[1]));p.readImpl();
            StringJoiner rows=new StringJoiner("|");if(p.tradeList!=null)for(TradeItem i:p.tradeList.getTradeItems())rows.add(i.getItemId()+":"+i.getCount());
            System.out.println(p.isAudit+","+(p.tradeAction==null?"null":p.tradeAction.name())+","+rows+","+(p.repurchaseList==null?"":p.repurchaseList.rows.toString()));
        }else if(args[0].equals("ap")){
            Acquisition.price=Integer.parseInt(args[1]);PricesService.vendor=Integer.parseInt(args[4]);Player p=new Player();p.rank.ap=Integer.parseInt(args[5]);
            TradeList t=new TradeList();for(int i=0;i<Integer.parseInt(args[6]);i++)t.addItem(1,Long.parseLong(args[2]));
            boolean ok=t.calculateAbyssRewardBuyList(p,Integer.parseInt(args[3]));System.out.println(t.getRequiredAp()+","+ok+","+p.messages);
        }else System.out.println((long)Double.longBitsToDouble(Long.parseLong(args[1])));
    }
}
class Player {Rank rank=new Rank();int messages;Rank getAbyssRank(){return rank;}Storage getInventory(){return new Storage();}Object getRace(){return null;}}
class Rank {int ap;int getAp(){return ap;}}
class Storage {long getKinah(){return Long.MAX_VALUE;}long getItemCountByItemId(int id){return Long.MAX_VALUE;}}
class TradeItem {int id;long count;TradeItem(int id,long c){this.id=id;count=c;}int getItemId(){return id;}long getCount(){return count;}ItemTemplate getItemTemplate(){return new ItemTemplate();}}
class ItemTemplate {Acquisition getAcquisition(){return new Acquisition();}long getPrice(){return 0;}}
enum AcquisitionType {AP,ABYSS,REWARD}
class Acquisition {static int price;AcquisitionType getType(){return AcquisitionType.AP;}int getRequiredAp(){return price;}int getItemId(){return 0;}int getItemCount(){return 0;}}
class PricesService {static int vendor;static int getVendorBuyModifier(){return vendor;}static long getBuyPrice(long n,Object race){return 0;}}
class PacketSendUtility {static void sendPacket(Player p,Object message){p.messages++;}}
class SM_SYSTEM_MESSAGE {static String STR_MSG_NOT_ENOUGH_ABYSSPOINT(){return "AP";}}
class AuditLogger {static void log(Player p,String message){}}
class RepurchaseList {List<Integer> rows=new ArrayList<>();RepurchaseList(int seller){}void addRepurchaseItem(Player p,int id,long count){if(id==401&&!rows.contains(id))rows.add(id);}}
''',encoding='utf-8')
    subprocess.run(['javac','-encoding','UTF-8','-d',str(PKG),str(PKG/'TradeList.java'),str(PKG/'ShopOracle.java')],check=True)
    def run(*args):return subprocess.check_output(['java','-cp',str(PKG),'ShopOracle',*map(str,args)],text=True).strip()
    packets=[]
    def packet(action,rows,unused=0):
        body=struct.pack('<iBBH',51,action,unused,len(rows))+b''.join(struct.pack('<iq',i,c) for i,c in rows)
        audit,name,items,repurchase=run('packet',body.hex()).split(',')
        packets.append(dict(bodyHex=body.hex().upper(),audit=audit=='true',action=None if name=='null' else name,rows=items,repurchases=repurchase))
    for a in (0,1,2,13,14,15,16,17,3,255):
        for rows in ([(401,1)],[(401,1),(401,2)],[(401,0)],[(0,1)],[(-1,1)]):packet(a,rows)
    packet(13,[(401,20000)],255);packet(13,[(401,20001)]);packet(1,[(401,-1)]);packet(13,[(401,1)]*36);packet(13,[(401,1)]*37);packet(255,[(401,1)]*37);packet(2,[(402,1)])
    ap=[]
    for price,count,modifier,vendor,available,rows in [
        (17,2,75,83,100,1),(2147483647,20000,100,100,2147483647,1),(2000000000,1,100,100,2147483647,2),
        (2147483647,1,100,100,2147483647,1),(1,1,1,1,0,1),(2147483647,20000,2147483647,2147483647,2147483647,1),
        (-2147483648,20000,2147483647,2147483647,0,1),(2147483647,9223372036854775807,2,100,2147483647,2),
        (100,1,-100,100,0,1),(0,20000,100,100,0,1)]:
        total,ok,messages=run('ap',price,count,modifier,vendor,available,rows).split(',')
        ap.append(dict(price=price,count=count,modifier=modifier,vendor=vendor,available=available,rows=rows,total=int(total),accepted=ok=='true',messages=int(messages)))
    casts=[]
    for value in (float('nan'),float('inf'),float('-inf'),0.,-0.,1.9,-1.9,float(9223372036854775807),-float(9223372036854775808),1e30,-1e30):
        bits=struct.unpack('<q',struct.pack('<d',value))[0];casts.append(dict(bits=bits,value=int(run('cast',bits))))
    fixture=dict(source='Java',sourceJavaCommit=SHA,sourceHashes=hashes,captureBoundary='Exact TradeList class, CM_BUY_ITEM.readImpl and both enums. Player/price/inventory/audit boundary doubles; repurchase eligibility fixed to object401. No runImpl capture.',packetCases=packets,apCases=ap,castCases=casts)
    (ROOT/'parity-artifacts/golden/formulas/NpcShop.e2b0c35e2.json').write_text(json.dumps(fixture,indent=2)+'\n',encoding='utf-8')
    print('Captured',len(packets),'packet reads,',len(ap),'AP totals and',len(casts),'Java long casts')

if __name__=='__main__':main()
