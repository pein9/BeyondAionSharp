"""Capture a17e66f10's exact storage record, pet mapping, broker predicate and cooldown body with scalar collaborators."""
import hashlib,json,re,subprocess,struct
from pathlib import Path
from capture_npc_shop import block
ROOT=Path(__file__).resolve().parents[2];JAVA=ROOT.parent/'aion-server';SHA='a17e66f105a67479af04055ef9d143401e13eb19'
PKG=ROOT/'artifacts/upstream/a17e66f10-fixed-exploits-of-mail-the-broker-and-storages/oracle';PKG.mkdir(parents=True,exist_ok=True)
paths={'StorageType':'model/items/storage/StorageType','StorageManager':'model/gameobjects/player/StorageManager','PetFunction':'model/templates/pet/PetFunction','BrokerService':'services/BrokerService','Express':'network/aion/clientpackets/CM_READ_EXPRESS_MAIL','Kinah':'network/aion/clientpackets/CM_LEGION_WH_KINAH'}
raw={n:subprocess.check_output(['git','-C',str(JAVA),'show',SHA+':game-server/src/com/aionemu/gameserver/'+p+'.java']) for n,p in paths.items()};s={n:b.decode() for n,b in raw.items()}
for n in ['StorageType','StorageManager']:(PKG/(n+'.java')).write_text(re.sub(r'^package .*;|^import .*;','',s[n],flags=re.M),encoding='utf-8')
predicate=re.search(r'if \((itemCount > buyingItem.getItemCount\(\).*?)\) \{',s['BrokerService']).group(1)
source='''import java.util.*;import java.util.concurrent.*;import java.time.Duration;import java.nio.*;
public class StorageOracle {
 static class PetFunction { int id; '''+block(s['PetFunction'],'public int getPetBagStorageId()')+''' }
 static class Broker {long count; boolean split; long getItemCount(){return count;}boolean isSplittingAvailable(){return split;} }
 static class Player {int id;Player(int n){id=n;}int getObjectId(){return id;} }
 static class System {static long now;static long currentTimeMillis(){return now;} }
 private static final Map<Integer,Long> cooldownByPlayer=new ConcurrentHashMap<>();
 '''+block(s['Express'],'public static void startPostmanCooldown(')+'''
 static class KinahReader { ByteBuffer buf;long amount;Action action;long readQ(){return buf.getLong();}int readUC(){return Byte.toUnsignedInt(buf.get());}
 '''+block(s['Kinah'],'private enum Action')+block(s['Kinah'],'protected void readImpl()')+''' }
 public static void main(String[]a){
  if(a[0].equals("matrix")){for(StorageType type:StorageType.values()){StringJoiner j=new StringJoiner(",");for(StorageType request:StorageType.values())if(new StorageManager(type,42).matches(request))j.add(""+request.getId());java.lang.System.out.println(type.getId()+"|"+type+"|"+j);}}
  else if(a[0].equals("pet")){PetFunction p=new PetFunction();p.id=Integer.parseInt(a[1]);java.lang.System.out.println(p.getPetBagStorageId());}
  else if(a[0].equals("broker")){Broker buyingItem=new Broker();buyingItem.count=10;buyingItem.split=Boolean.parseBoolean(a[1]);long itemCount=Long.parseLong(a[2]);java.lang.System.out.println('''+predicate+''');}
  else if(a[0].equals("packet")){KinahReader r=new KinahReader();r.buf=ByteBuffer.wrap(HexFormat.of().parseHex(a[1])).order(ByteOrder.LITTLE_ENDIAN);r.readImpl();java.lang.System.out.println(r.amount+"|"+r.action+"|"+r.buf.remaining());}
  else {System.now=Long.parseLong(a[1]);cooldownByPlayer.put(2,System.now-1);cooldownByPlayer.put(3,System.now);cooldownByPlayer.put(4,System.now+1);startPostmanCooldown(new Player(1));for(var e:new TreeMap<>(cooldownByPlayer).entrySet())java.lang.System.out.println(e.getKey()+"|"+e.getValue());}
 }
}'''
(PKG/'StorageOracle.java').write_text(source,encoding='utf-8');subprocess.run(['javac','-encoding','UTF-8','-d',str(PKG),str(PKG/'StorageType.java'),str(PKG/'StorageManager.java'),str(PKG/'StorageOracle.java')],check=True)
def run(*args):return subprocess.check_output(['java','-cp',str(PKG),'StorageOracle',*map(str,args)],text=True).strip()
matches=[]
for row in run('matrix').splitlines():
 id,name,allowed=row.split('|');matches.append(dict(opened=int(id),name=name,allowed=list(map(int,allowed.split(',')))))
pet=[dict(id=n,storage=int(run('pet',n))) for n in [-2147483648,-5,0,*range(3,15),2147483647]]
broker=[dict(split=split,count=n,rejected=run('broker',str(split).lower(),n)=='true') for split in [False,True] for n in [-9223372036854775808,-1,0,1,9,10,11,9223372036854775807]]
cooldowns=[dict(now=n,expected={k:int(v) for k,v in (line.split('|') for line in run('cooldown',n).splitlines())}) for n in [0,1000,-9223372036854775798,9223372036854775797]]
packets=[]
for amount in [-9223372036854775808,-1,42,9223372036854775807]:
 for action in [0,1,2,255]:
  body=struct.pack('<qB',amount,action).hex();value,name,remaining=run('packet',body).split('|');assert remaining=='0'
  packets.append(dict(bodyHex=body.upper(),amount=int(value),action=None if name=='null' else name))
fixture=dict(sourceJavaCommit=SHA,sourceHashes={paths[n]+'.java':hashlib.sha256(b).hexdigest() for n,b in raw.items()},captureBoundary='Exact enum/record, pet method, broker conditional, kinah reader/action enum and cooldown body. Scalar Player/Broker, byte readers and clock stubs; runtime service behavior tested separately in C#.',storageMatches=matches,petCases=pet,brokerCases=broker,cooldowns=cooldowns,kinahPackets=packets)
(ROOT/'parity-artifacts/golden/formulas/MailStorage.a17e66f10.json').write_text(json.dumps(fixture,indent=2)+'\n',encoding='utf-8');print('Captured',len(matches)**2,'storage pairs,',len(pet),'pet IDs,',len(broker),'broker,',len(cooldowns),'cooldown and',len(packets),'packet vectors')
