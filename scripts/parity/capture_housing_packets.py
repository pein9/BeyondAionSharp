"""Compile exact 46e8d00b9 complete house edit/registry writers and dye writer with declared scalar collaborators.
No full Java constructors, world, usage-object bodies or server boot are claimed.
"""
import hashlib,json,os,re,subprocess
from pathlib import Path
from capture_npc_shop import block
ROOT=Path(__file__).resolve().parents[2];JAVA=Path(os.environ.get('BEYOND_AION_JAVA_ROOT',ROOT.parent/'aion-server'));SHA='46e8d00b923d83820e32e0bbe67835de32b6c81d'
PKG=ROOT/'artifacts/upstream/46e8d00b9-fixed-exploits-of-housing/packet-oracle';PKG.mkdir(parents=True,exist_ok=True)
paths={n:'game-server/src/com/aionemu/gameserver/'+p+'.java' for n,p in {'HousingCommand':'model/house/HousingCommand','HousingContainer':'model/house/HousingContainer','Edit':'network/aion/serverpackets/SM_HOUSE_EDIT','Registry':'network/aion/serverpackets/SM_HOUSE_REGISTRY','Base':'network/aion/AionServerPacket'}.items()}
raw={n:subprocess.check_output(['git','-C',str(JAVA),'show',SHA+':'+p]) for n,p in paths.items()};s={n:v.decode('utf-8') for n,v in raw.items()}
for name in ['HousingCommand','HousingContainer']:(PKG/(name+'.java')).write_text(re.sub(r'^package .*;','',s[name],flags=re.M),encoding='utf-8')
dye=re.search(r'protected [^{\n]*void writeDyeInfo\(',s['Base']).group()
code='''import java.util.*;import java.io.*;
class LoggerFactory {static LoggerFactory getLogger(Object c){return new LoggerFactory();}void warn(Object...v){}}
class Sink {ByteArrayOutputStream b=new ByteArrayOutputStream();void writeC(int v){b.write(v);}void writeH(int v){writeC(v);writeC(v>>>8);}void writeD(int v){writeH(v);writeH(v>>>16);}void writeF(float v){writeD(Float.floatToRawIntBits(v));}void writeB(byte[]v){b.writeBytes(v);}Sink getBuf(){return this;}
'''+block(s['Base'],dye)+'''}
class Template {int getTemplateId(){return 3520001;}int getTypeId(){return 5;}}
class HouseObject<T> {Integer color;int expiry;int getObjectId(){return 987614;}Template getObjectTemplate(){return new Template();}int secondsUntilExpiration(){return 65;}Integer getColor(){return color;}int getColorExpireEnd(){return expiry;}}
class UseableItemObject extends HouseObject<Object> {void writeUsageData(Sink s){}}
class HouseDecoration {int getObjectId(){return 987615;}int getTemplateId(){return 3521001;}}
class Building {List<Integer> getDefaultPartIds(){return List.of(111,222);}}
class Address {int getId(){return 98761;}}
class HouseRegistry {HouseObject<?> obj=new HouseObject<>();House getOwner(){return new House(this);}HouseObject<?> getObjectByObjId(int id){return id==987614?obj:null;}HouseDecoration getDecorByObjId(int id){return id==987615?new HouseDecoration():null;}List<HouseObject<?>> getNotSpawnedObjects(){return List.of(obj);}List<HouseDecoration> getUnusedDecors(){return List.of(new HouseDecoration());}}
class House {HouseRegistry registry;House(HouseRegistry r){registry=r;}HouseRegistry getRegistry(){return registry;}Address getAddress(){return new Address();}Building getBuilding(){return new Building();}}
class Cooldowns {int remainingSeconds(int id){return 0;}}
class Player {House h;Player(House v){h=v;}House getActiveHouse(){return h;}int getObjectId(){return 810000;}Player getCommonData(){return this;}int getPlayerObjId(){return 810000;}Cooldowns getHouseObjectCooldowns(){return new Cooldowns();}}
class AionConnection {Player p;AionConnection(Player v){p=v;}Player getActivePlayer(){return p;}}
public class HousingPacketOracle {
 static class Edit extends Sink {HousingCommand action;HousingContainer container;int itemObjectId;float x=1.25f,y=-2.5f,z=3.75f;int rotation=65535;
 '''+block(s['Edit'],'protected void writeImpl(')+'''}
 static class Registry extends Sink {HousingContainer container;
 '''+block(s['Registry'],'protected void writeImpl(')+'''}
 public static void main(String[]a){var registry=new HouseRegistry();registry.obj.color=a[2].equals("null")?null:Integer.parseInt(a[2]);registry.obj.expiry=Integer.parseInt(a[3]);var con=new AionConnection(new Player(new House(registry)));Sink result;
  if(a[0].equals("edit")){var p=new Edit();p.action=HousingCommand.getById(Integer.parseInt(a[1]));p.container=HousingContainer.getById(Integer.parseInt(a[4]));p.itemObjectId=Integer.parseInt(a[5]);p.writeImpl(con);result=p;}
  else{var p=new Registry();p.container=HousingContainer.getById(Integer.parseInt(a[1]));p.writeImpl(con);result=p;}
  System.out.println(HexFormat.of().withUpperCase().formatHex(result.b.toByteArray()));
 }
}'''
(PKG/'HousingPacketOracle.java').write_text(code,encoding='utf-8');subprocess.run(['javac','-encoding','UTF-8','-d',str(PKG),*[str(p) for p in PKG.glob('*.java')]],check=True)
cases=[]
def capture(kind,command,color=None,expiry=0,container=1,objectId=987614):
    payload=subprocess.check_output(['java','-cp',str(PKG),'HousingPacketOracle',kind,str(command),'null' if color is None else str(color),str(expiry),str(container),str(objectId)],text=True).strip()
    cases.append(dict(kind=kind,command=command,color=color,expiry=expiry,container=container,objectId=objectId,payloadHex=payload))
for command in [1,2,4,7,14,15]:capture('edit',command)
for color,expiry in [(None,0),(0x123456,1792411200),(0,-2147483648),(0xFFFFFF,2147483647)]:
    for command in [3,5]:capture('edit',command,color,expiry)
    capture('registry',1,color,expiry)
capture('edit',3,container=2,objectId=987615);capture('edit',4,container=2,objectId=987615);capture('registry',2)
fixture=dict(sourceJavaCommit=SHA,sourceHashes={paths[n]:hashlib.sha256(v).hexdigest() for n,v in raw.items()},captureBoundary=__doc__.strip(),cases=cases)
(ROOT/'parity-artifacts/golden/formulas/HousingPackets.46e8d00b9.json').write_text(json.dumps(fixture,indent=2)+'\n',encoding='utf-8');print('Captured',len(cases),'complete Java housing writer payloads')
