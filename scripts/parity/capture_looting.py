"""Capture 54880a691's exact Java loot model, geometry, client readers and complete server writer bodies.
Scalar graph/byte-sink collaborators are declared below; no full server boot or constructor oracle is claimed.
"""
import hashlib,json,re,struct,subprocess,math
from pathlib import Path
from capture_npc_shop import block
ROOT=Path(__file__).resolve().parents[2];JAVA=ROOT.parent/'aion-server';SHA='54880a6914b4ee03d18608eeab6b3532ed9759c9';PKG=ROOT/'artifacts/upstream/54880a691-fixed-exploits-of-looting/oracle';PKG.mkdir(parents=True,exist_ok=True)
paths={'Rules':'model/team/common/legacy/LootGroupRules','LootRuleType':'model/team/common/legacy/LootRuleType','TeamType':'model/team/TeamType','Start':'network/aion/clientpackets/CM_START_LOOT','Distribution':'network/aion/clientpackets/CM_DISTRIBUTION_SETTINGS','Group':'network/aion/serverpackets/SM_GROUP_INFO','Alliance':'network/aion/serverpackets/SM_ALLIANCE_INFO','Messages':'network/aion/serverpackets/SM_SYSTEM_MESSAGE','Maps':'world/WorldMapType'}
raw={n:subprocess.check_output(['git','-C',str(JAVA),'show',SHA+':game-server/src/com/aionemu/gameserver/'+p+'.java']) for n,p in paths.items()};s={n:b.decode('utf-8') for n,b in raw.items()}
for n,filename in [('Rules','LootGroupRules'),('LootRuleType','LootRuleType'),('TeamType','TeamType')]:
 (PKG/(filename+'.java')).write_text(re.sub(r'^package .*;|^import .*;','',s[n],flags=re.M),encoding='utf-8')
# Removed imports from the exact model refer only to these explicitly bounded collaborators.
rules=PKG/'LootGroupRules.java';rules.write_text('import java.util.*;import java.util.concurrent.*;\n'+rules.read_text(),encoding='utf-8')
msg='class SM_SYSTEM_MESSAGE {int id;SM_SYSTEM_MESSAGE(int n){id=n;}int getId(){return id;}'+''.join(block(s['Messages'],'public static SM_SYSTEM_MESSAGE '+name+'()') for name in ['STR_MSG_JUNK_DISTRIBUTE_ON','STR_MSG_JUNK_DISTRIBUTE_OF'])+'}'
maps=[int(re.search(r'\b'+name+r'\((\d+)',s['Maps']).group(1)) for name in ['DANUAR_RELIQUARY','LUCKY_DANUAR_RELIQUARY','INFERNAL_DANUAR_RELIQUARY']]
constants='\n'.join(re.findall(r'private static final float \w+ = [^;]+;',s['Start']))
source='''import java.util.*;import java.nio.*;import java.io.*;
class DropItem {} enum ItemQuality {JUNK,COMMON,RARE,LEGEND,UNIQUE,EPIC,MYTHIC} enum PlayerMode {IN_ROLL}
class InRoll {int getIndex(){return 0;}int getNpcId(){return 0;}int getRollType(){return 0;}int getItemId(){return 0;}}
class DropDistributionService {static DropDistributionService getInstance(){return new DropDistributionService();}void handleRollOrBid(Player p,int a,int b,int c,int d,int e,int f){}}
class ThreadPoolManager {static ThreadPoolManager getInstance(){return new ThreadPoolManager();}void schedule(Runnable r,int t){}}
class BoundRadius {float front,side;float getMaxOfFrontAndSide(){return Math.max(front,side);}}
class Template {BoundRadius r=new BoundRadius();BoundRadius getBoundRadius(){return r;}}
class Player {int map=210010000,instance=1;float x,y,z;Template t=new Template();InRoll inRoll;boolean positioned=true;int getWorldId(){return map;}int getInstanceId(){return instance;}float getX(){return x;}float getY(){return y;}float getZ(){return z;}Template getObjectTemplate(){return t;}Object getPosition(){return positioned?this:null;}boolean isInPlayerMode(PlayerMode m){return false;}}
class Npc extends Player {}
class AionConnection {Player p;AionConnection(Player v){p=v;}Player getActivePlayer(){return p;}}
class PlayerAllianceGroup {int id;PlayerAllianceGroup(int n){id=n;}int getTeamId(){return id;}}
class PlayerAlliance {List<PlayerAllianceGroup> groups=new ArrayList<>();List<Integer> vice=new ArrayList<>();int groupSize(){return groups.size();}Collection<Integer> getViceCaptainIds(){return vice;}Collection<PlayerAllianceGroup> getGroups(){return groups;}}
class Sink {ByteArrayOutputStream bytes=new ByteArrayOutputStream();void writeC(int n){bytes.write(n);}void writeH(int n){writeC(n);writeC(n>>>8);}void writeD(int n){writeH(n);writeH(n>>>16);}void writeS(String v){if(v!=null)for(int i=0;i<v.length();i++)writeH(v.charAt(i));writeH(0);}String hex(){return HexFormat.of().withUpperCase().formatHex(bytes.toByteArray());}}
'''+msg+'''
public class LootOracle {
 static class Geometry {'''+constants+'\n private static final Set<Integer> RELIQUARY_MAPS=Set.of('+','.join(map(str,maps))+');\n'+block(s['Start'],'private boolean isInLootRange(')+''' }
 static class Log {void warn(Object...v){}}
 static class StartReader {ByteBuffer buf;int targetObjectId;Action action;static Log log=new Log();int readD(){return buf.getInt();}int readUC(){return Byte.toUnsignedInt(buf.get());}AionConnection getConnection(){return new AionConnection(null);}
 '''+block(s['Start'],'private enum Action')+block(s['Start'],'protected void readImpl()')+''' }
 static class DistReader {ByteBuffer buf;boolean autoDistributeJunk;LootRuleType lootRules;int commonItemAbove,superiorItemAbove,heroicItemAbove,fabledItemAbove,ethernalItemAbove,mythicItemAbove;int readD(){return buf.getInt();}
 '''+block(s['Distribution'],'protected void readImpl()')+''' }
 static class GroupPacket extends Sink {LootGroupRules lootRules;int groupId=4100,leaderId=810000,messageId;TeamType type;
 '''+block(s['Group'],'protected void writeImpl(')+''' }
 static class AlliancePacket extends Sink {LootGroupRules lootRules,lootLeagueRules;PlayerAlliance alliance=new PlayerAlliance();int groupid=4200,leaderid=810000,type=63,subType=0,leagueId,messageId;String message;List<AllianceInfo> leagueData=new ArrayList<>();
 '''+block(s['Alliance'],'private class AllianceInfo')+block(s['Alliance'],'protected void writeImpl(')+''' }
 static float f(String v){return Float.intBitsToFloat((int)Long.parseUnsignedLong(v,16));}
 public static void main(String[]a) {
  if(a[0].equals("geometry")){Player p=new Player();Npc n=new Npc();p.map=Integer.parseInt(a[1]);n.map=Integer.parseInt(a[2]);n.instance=Integer.parseInt(a[3]);n.x=f(a[4]);n.y=f(a[5]);n.z=f(a[6]);p.t.r.front=p.t.r.side=.25f;n.t.r.front=f(a[7]);n.t.r.side=f(a[8]);System.out.println(new Geometry().isInLootRange(p,n));}
  else if(a[0].equals("start")){StartReader r=new StartReader();r.buf=ByteBuffer.wrap(HexFormat.of().parseHex(a[1])).order(ByteOrder.LITTLE_ENDIAN);r.readImpl();System.out.println(r.targetObjectId+"|"+r.action+"|"+r.buf.remaining());}
  else if(a[0].equals("dist")){DistReader r=new DistReader();r.buf=ByteBuffer.wrap(HexFormat.of().parseHex(a[1])).order(ByteOrder.LITTLE_ENDIAN);r.readImpl();System.out.println(r.lootRules+"|"+r.autoDistributeJunk+"|"+r.commonItemAbove+"|"+r.superiorItemAbove+"|"+r.heroicItemAbove+"|"+r.fabledItemAbove+"|"+r.ethernalItemAbove+"|"+r.mythicItemAbove+"|"+r.buf.remaining());}
  else if(a[0].equals("rule")){boolean junk=Boolean.parseBoolean(a[1]);LootGroupRules r=new LootGroupRules(LootRuleType.ROUNDROBIN,junk,0,2,2,2,2,2);LootGroupRules prev=a[2].equals("null")?null:new LootGroupRules(LootRuleType.LEADER,Boolean.parseBoolean(a[2]),0,2,2,2,2,2);System.out.println(r.getJunkDistributionChangeMessageId(prev));System.out.println(r.isAutoDistributedJunk(null));for(ItemQuality q:ItemQuality.values())System.out.println(q+"|"+r.isAutoDistributedJunk(q)+"|"+r.getQualityRule(q));}
  else if(a[0].equals("group")){GroupPacket p=new GroupPacket();p.lootRules=new LootGroupRules(LootRuleType.LEADER,Boolean.parseBoolean(a[1]),10,11,12,13,14,15);p.type=TeamType.valueOf(a[2]);p.messageId=Integer.parseInt(a[3]);Player active=new Player();if(a[4].equals("false"))active=null;p.writeImpl(new AionConnection(active));System.out.println(p.hex());}
  else {AlliancePacket p=new AlliancePacket();p.lootRules=new LootGroupRules(LootRuleType.ROUNDROBIN,Boolean.parseBoolean(a[1]),10,11,12,13,14,15);for(String id:a[2].split(","))p.alliance.groups.add(new PlayerAllianceGroup(Integer.parseInt(id)));p.alliance.vice.addAll(List.of(811001,811002));p.messageId=Integer.parseInt(a[3]);p.message="Captainβ";if(Boolean.parseBoolean(a[4])){p.leagueId=4300;p.lootLeagueRules=new LootGroupRules(LootRuleType.FREEFORALL,false,20,21,22,23,24,25);for(int i=0;i<2;i++){var info=p.new AllianceInfo();info.setAlliancePosition(i*2);info.setAllianceObjectId(4200+i*100);info.setMemberCount(6+i*6);info.setCaptainName(i==0?"Alpha":"βeta");info.setCaptainWorldId(210010000+i*10000000);p.leagueData.add(info);}}p.writeImpl(new AionConnection(new Player()));System.out.println(p.hex());}
 }
}'''
(PKG/'LootOracle.java').write_text(source,encoding='utf-8');subprocess.run(['javac','-encoding','UTF-8','-d',str(PKG),*[str(p) for p in PKG.glob('*.java')]],check=True)
def run(*args):return subprocess.check_output(['java','-cp',str(PKG),'LootOracle',*map(str,args)],text=True).strip()
def bits(v):return struct.pack('>f',v).hex().upper()
def adjacent(v,d):return struct.unpack('>f',struct.pack('>I',int(bits(v),16)+d))[0]
geometry=[]
def geom(name,x=0,y=0,z=0,map=210010000,nmap=None,instance=1,front=2,side=1):
 b=[bits(v) for v in [x,y,z,front,side]];geometry.append(dict(name=name,map=map,npcMap=map if nmap is None else nmap,instance=instance,bits=b,expected=run('geometry',map,map if nmap is None else nmap,instance,*b)=='true'))
for d in [-1,0,1]:
 geom('horizontal-'+str(d),x=adjacent(8.75,d));geom('above-limit-'+str(d),x=8,z=adjacent(50,d));geom('reliquary-edge-'+str(d),x=adjacent(30,d),z=99999,map=maps[0])
for z in [0,1,-1,8.75,-8.75,49,50,51,-51,1000000,float('nan'),float('inf'),-float('inf')]:geom('height-'+str(z),x=1,z=z)
for map_id in maps:geom('special-'+str(map_id),x=29,z=-1000000,map=map_id)
geom('world-mismatch',nmap=220010000);geom('instance-mismatch',instance=2);geom('nan-x',x=float('nan'));geom('infinite-x',x=float('inf'));geom('negative-x',x=-8);geom('bigger-front',x=14,front=8,side=1);geom('bigger-side',x=14,front=1,side=8);geom('no-bounds',x=6.75,front=0,side=0);geom('diagonal',x=6,y=6,z=-1)
start=[]
for target in [-2147483648,123456,2147483647]:
 for action in [0,1,2,128,255]:
  body=struct.pack('<iB',target,action).hex();oid,name,remaining=run('start',body).split('|');assert remaining=='0';start.append(dict(bodyHex=body.upper(),target=int(oid),action=None if name=='null' else name))
dist=[]
for rule in [-1,0,1,2,3,2147483647]:
 for junk in [-1,0,1,2,2147483647]:
  body=struct.pack('<10i',2147483647,rule,junk,10,11,12,13,14,15,-2147483648).hex();out=run('dist',body).split('|');assert out[-1]=='0';dist.append(dict(bodyHex=body.upper(),rule=None if out[0]=='null' else out[0],autoJunk=out[1]=='true',grades=list(map(int,out[2:-1]))))
rules=[]
for flag in [False,True]:
 for previous in [None,False,True]:
  rows=run('rule',str(flag).lower(),'null' if previous is None else str(previous).lower()).splitlines();rules.append(dict(autoJunk=flag,previous=previous,message=int(rows[0]),nullAuto=rows[1]=='true',qualities=[dict(name=q,auto=auto=='true',qualityRule=qr=='true') for q,auto,qr in (row.split('|') for row in rows[2:])]))
packets=[]
for flag,typ,msg,active in [(False,'GROUP',0,True),(True,'AUTO_GROUP',1390210,True),(False,'GROUP',1390211,False),(True,'GROUP',0,True)]:packets.append(dict(kind='group',autoJunk=flag,type=typ,message=msg,active=active,payloadHex=run('group',str(flag).lower(),typ,msg,str(active).lower())))
for flag,ids,msg,league in [(False,[1003,1001,1002,1000],0,False),(True,[7001,3001,11001,2001],1390210,False),(True,[7001,3001,11001,2001],1390211,True),(False,[1003,1001,1002,1000],0,True)]:packets.append(dict(kind='alliance',autoJunk=flag,groups=ids,message=msg,league=league,payloadHex=run('alliance',str(flag).lower(),','.join(map(str,ids)),msg,str(league).lower())))
fixture=dict(sourceJavaCommit=SHA,sourceHashes={paths[n]+'.java':hashlib.sha256(b).hexdigest() for n,b in raw.items()},captureBoundary=__doc__.strip(),geometry=geometry,startReads=start,distributionReads=dist,rules=rules,packets=packets)
(ROOT/'parity-artifacts/golden/formulas/Looting.54880a691.json').write_text(json.dumps(fixture,indent=2)+'\n',encoding='utf-8');print('Captured',len(geometry),'geometry,',len(start),'start readers,',len(dist),'distribution readers,',len(rules),'rule and',len(packets),'complete writer cases')
