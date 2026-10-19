"""Capture ea257e5e9's exact preflight method and all four confirmation packet actions."""
import hashlib,json,re,subprocess
from pathlib import Path
from capture_npc_shop import block

ROOT=Path(__file__).resolve().parents[2]
SHA='ea257e5e9bd8e6e6e091feb607a56d5fa187ef0a'
BASE='game-server/src/com/aionemu/gameserver/'
PKG=ROOT/'artifacts/upstream/ea257e5e9-fixed-exchanges-taking-items-from-one-side-when/java-capture'

def main():
    PKG.mkdir(parents=True,exist_ok=True);texts={};hashes={}
    for name,path in [('SM_EXCHANGE_CONFIRMATION','network/aion/serverpackets/SM_EXCHANGE_CONFIRMATION'),('ExchangeService','services/ExchangeService')]:
        file=BASE+path+'.java';raw=subprocess.check_output(['git','-C',str(ROOT.parent/'aion-server'),'show',SHA+':'+file]);texts[name]=raw.decode('utf-8');hashes[file]=hashlib.sha256(raw).hexdigest()
    (PKG/'SM_EXCHANGE_CONFIRMATION.java').write_text(re.sub(r'^package .*;|^import .*;','',texts['SM_EXCHANGE_CONFIRMATION'],flags=re.M),encoding='utf-8')
    check=block(texts['ExchangeService'],'private boolean canGive(')
    (PKG/'ExchangeOracle.java').write_text('''import java.util.*;
public class ExchangeOracle {
'''+check+'''
    public static void main(String[]args){
        if(args[0].equals("packet")){
            for(var a:SM_EXCHANGE_CONFIRMATION.Action.values()){
                var p=new SM_EXCHANGE_CONFIRMATION(a);p.writeImpl(null);System.out.println(a.name()+","+HexFormat.of().formatHex(p.bytes.toByteArray()));
            }
        }else{
            Player p=new Player();p.storage.kinah=Long.parseLong(args[1]);Exchange e=new Exchange();e.kinah=Long.parseLong(args[2]);
            if(!args[3].equals("-"))for(String row:args[3].split(";")){
                String[] f=row.split(":");int id=Integer.parseInt(f[0]);if(!f[1].equals("null"))p.storage.items.put(id,new Item(Long.parseLong(f[1])));
                e.items.put(id,new ExchangeItem(id,Long.parseLong(f[2])));
            }
            System.out.println(new ExchangeOracle().canGive(p,e));
        }
    }
}
abstract class AionServerPacket {java.io.ByteArrayOutputStream bytes=new java.io.ByteArrayOutputStream();void writeC(int x){bytes.write(x);}protected abstract void writeImpl(AionConnection c);}
class AionConnection {}
class Player {Storage storage=new Storage();Storage getInventory(){return storage;}}
class Storage {long kinah;Map<Integer,Item> items=new HashMap<>();long getKinah(){return kinah;}Item getItemByObjId(int id){return items.get(id);}}
class Item {long count;Item(long n){count=n;}long getItemCount(){return count;}}
class ExchangeItem {int id;long count;ExchangeItem(int id,long n){this.id=id;count=n;}int getItemObjId(){return id;}long getItemCount(){return count;}}
class Exchange {long kinah;Map<Integer,ExchangeItem>items=new LinkedHashMap<>();Map<Integer,ExchangeItem>getItems(){return items;}long getKinahCount(){return kinah;}}
''',encoding='utf-8')
    subprocess.run(['javac','-encoding','UTF-8','-d',str(PKG),str(PKG/'SM_EXCHANGE_CONFIRMATION.java'),str(PKG/'ExchangeOracle.java')],check=True)
    def run(*args):return subprocess.check_output(['java','-cp',str(PKG),'ExchangeOracle',*map(str,args)],text=True).strip()
    packets=[]
    for line in run('packet').splitlines():
        name,payload=line.split(',');packets.append(dict(action=name,payloadHex=payload.upper()))
    cases=[]
    for balance,offered,rows in [(0,0,[]),(5,5,[]),(4,5,[]),(100,20,[(1,3,3)]),(100,20,[(1,2,3)]),
        (100,20,[(1,None,3)]),(100,20,[(1,3,3),(2,4,4)]),(100,20,[(1,3,3),(2,None,4)]),
        (100,20,[(1,3,3),(2,3,4)]),(100,101,[(1,3,3)]),(0,0,[(1,0,0)]),
        (9223372036854775807,9223372036854775807,[(1,9223372036854775807,9223372036854775807)])]:
        encoded=';'.join(f'{id}:{"null" if stock is None else stock}:{count}' for id,stock,count in rows) or '-'
        cases.append(dict(balance=balance,offeredKinah=offered,items=[dict(id=id,stock=stock,offered=count) for id,stock,count in rows],accepted=run('check',balance,offered,encoded)=='true'))
    fixture=dict(source='Java',sourceJavaCommit=SHA,sourceHashes=hashes,captureBoundary='Exact canGive method with inventory/offer boundary doubles; exact confirmation packet class with writeC byte sink.',preflightCases=cases,packetCases=packets)
    (ROOT/'parity-artifacts/golden/formulas/ExchangePreflight.ea257e5e9.json').write_text(json.dumps(fixture,indent=2)+'\n',encoding='utf-8')
    print('Captured',len(cases),'preflights and',len(packets),'confirmation payloads from',SHA)

if __name__=='__main__':main()
