"""Compile exact 6dd3aa507 broadcast enum, reader/handler and complete writer.

Scalar collaborators declare team membership, ordered lands and known recipients;
native Java team/zone/world lifecycle and persistence are not executed.
"""
import hashlib,json,os,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];JAVA=Path(os.environ.get('BEYOND_AION_JAVA_ROOT',ROOT.parent/'aion-server'));SHA='6dd3aa5070f5c07ef1598665f7aa1ef2c321e0eb'
PKG=ROOT/'artifacts/upstream/6dd3aa507-fixed-client-broadcasts-reaching-the-wrong-playe/oracle';PKG.mkdir(parents=True,exist_ok=True)
paths={'ClientBroadcastType':'model/ClientBroadcastType','CM_GROUP_DATA_EXCHANGE':'network/aion/clientpackets/CM_GROUP_DATA_EXCHANGE','SM_GROUP_DATA_EXCHANGE':'network/aion/serverpackets/SM_GROUP_DATA_EXCHANGE'}
raw={n:subprocess.check_output(['git','-C',str(JAVA),'show',SHA+':game-server/src/com/aionemu/gameserver/'+p+'.java']) for n,p in paths.items()}
for n,b in raw.items():(PKG/(n+'.java')).write_text('import java.util.*;\n'+re.sub(r'^package .*;|^import .*;','',b.decode(),flags=re.M),encoding='utf-8')
adapter='''import java.io.*;import java.util.*;import java.nio.*;import java.util.function.*;import java.security.*;
enum State{IN_GAME}
class AionConnection{Player player;Player getActivePlayer(){return player;}}
abstract class AionClientPacket{ByteBuffer buf;AionConnection con=new AionConnection();AionClientPacket(int o,Set<State>s){}int readUC(){return Byte.toUnsignedInt(buf.get());}int readD(){return buf.getInt();}byte[] readB(int n){byte[] b=new byte[n];buf.get(b);return b;}int getRemainingBytes(){return buf.remaining();}AionConnection getConnection(){return con;}protected abstract void readImpl();protected abstract void runImpl();}
abstract class AionServerPacket{ByteArrayOutputStream buf=new ByteArrayOutputStream();void writeC(int n){buf.write(n);}void writeD(int n){for(int i=0;i<4;i++)writeC(n>>>(i*8));}void writeB(byte[] b){buf.writeBytes(b);}protected abstract void writeImpl(AionConnection c);}
class Player{int id;List<Player> group,alliance,league;List<String> lands=new ArrayList<>();Set<String> inside=new HashSet<>();Player(int n){id=n;}boolean isInGroup(){return group!=null;}boolean isInAlliance(){return alliance!=null;}boolean isInLeague(){return league!=null;}TeamData getPlayerGroup(){return new TeamData(group);}TeamData getPlayerAlliance(){return new TeamData(alliance,league);}TeamData getPlayerAllianceGroup(){return new TeamData(group);}Position getPosition(){return new Position(lands);}boolean isInsideZone(String n){return inside.contains(n);}}
class TeamData{List<Player> members,league;TeamData(List<Player>m){members=m;}TeamData(List<Player>m,List<Player>l){members=m;league=l;}List<Player>getOnlineMembers(){return members;}TeamData getLeague(){return new TeamData(league);}}
class Position{List<String> lands;Position(List<String>l){lands=l;}Region getMapRegion(){return new Region(lands);}}
class Region{List<String> lands;Region(List<String>l){lands=l;}List<Zone>findZones(Player p){return lands.stream().map(Zone::new).toList();}}
class Zone{String name;Zone(String n){name=n;}Zone getZoneTemplate(){return this;}String getName(){return name;}}
class AuditLogger{static String message="";static void log(Player p,String m){message=m;}}
class PacketSendUtility{static List<Player>known;static List<Integer>recipients=new ArrayList<>();static String body="";static void sendPacket(Player p,AionServerPacket packet){packet.buf.reset();packet.writeImpl(null);body=HexFormat.of().withUpperCase().formatHex(packet.buf.toByteArray());recipients.add(p.id);}static void broadcastPacket(Player p,AionServerPacket packet,boolean self,Predicate<Player>filter){if(self)sendPacket(p,packet);for(Player other:known)if(filter.test(other))sendPacket(other,packet);}}
public class ClientBroadcastOracle{
 static Object get(Object p,String n)throws Exception{var f=p.getClass().getDeclaredField(n);f.setAccessible(true);return f.get(p);}
 static String id(Object o)throws Exception{if(o==null)return "null";var f=o.getClass().getDeclaredField("id");f.setAccessible(true);return f.get(o).toString();}
 static int n(String s){return Integer.parseInt(s);}static byte[] data(int n){byte[]b=new byte[n];for(int i=0;i<n;i++)b[i]=(byte)(i*37+11);return b;}
 static CM_GROUP_DATA_EXCHANGE read(String[]a){int type=n(a[1]),team=n(a[2]),tag=n(a[3]),size=n(a[4]),available=n(a[5]);var buf=ByteBuffer.allocate((type==0?7:5)+available).order(ByteOrder.LITTLE_ENDIAN);buf.put((byte)type);if(type==0)buf.put((byte)team).put((byte)tag);buf.putInt(size).put(data(available)).flip();var p=new CM_GROUP_DATA_EXCHANGE(0,Set.of());p.buf=buf;p.readImpl();return p;}
 public static void main(String[]args)throws Exception{var input=new BufferedReader(new InputStreamReader(System.in));String line;while((line=input.readLine())!=null){String[]a=line.split("[|]",-1);StringJoiner out=new StringJoiner("|");
  if(a[0].equals("writer")){var p=n(a[1])==0?new SM_GROUP_DATA_EXCHANGE(n(a[2]),data(n(a[3]))):new SM_GROUP_DATA_EXCHANGE(data(n(a[3])));p.writeImpl(null);out.add(HexFormat.of().withUpperCase().formatHex(p.buf.toByteArray()));}
  else{var p=read(a);if(a[0].equals("read")){byte[] b=(byte[])get(p,"data");out.add(id(get(p,"type"))).add(id(get(p,"team"))).add(get(p,"tag").toString()).add(get(p,"dataSize").toString()).add(b==null?"null":HexFormat.of().withUpperCase().formatHex(MessageDigest.getInstance("SHA-256").digest(b))).add(Integer.toString(p.buf.remaining()));}
   else {var self=new Player(1);var same=new Player(2);var otherGroup=new Player(3);var otherAlliance=new Player(4);var outside=new Player(5);String mode=a[6];if(!mode.equals("none")){self.group=List.of(self,same);self.alliance=List.of(self,same,otherGroup);if(mode.equals("league"))self.league=List.of(self,same,otherGroup,otherAlliance);}if(!a[7].isEmpty())self.lands=Arrays.asList(a[7].split(","));same.inside.add("HOUSE_A");otherGroup.inside.add("HOUSE_B");otherAlliance.inside.add("HOUSE_A");PacketSendUtility.known=List.of(same,otherGroup,outside);p.con.player=n(a[8])==1?self:null;p.runImpl();out.add(PacketSendUtility.recipients.toString().replace(" ","")).add(PacketSendUtility.body).add(AuditLogger.message);PacketSendUtility.recipients.clear();PacketSendUtility.body="";AuditLogger.message="";}
  }System.out.println(out.toString());
 }}
}
'''
(PKG/'ClientBroadcastOracle.java').write_text(adapter)
cases=[];queries=[]
def add(kind,args):cases.append({'kind':kind,'args':args});queries.append(kind+'|'+'|'.join(map(str,args)))
for type in range(256):add('read',[type,255,231,3,3])
for type in [0,1,2,3,255]:
    for size,available in [(-2147483648,0),(-1,8),(0,0),(0,8),(1,0),(1,1),(3,2),(6143,6143),(6144,6144),(6145,6145),(2147483647,8)]:
        for team in ([0,1,2,3,255] if type==0 else [0]):add('read',[type,team,231,size,available])
for type in [0,1]:
    for tag in [-1,0,7,255,256,65535]:
        for size in [0,1,3,6144]:add('writer',[type,tag,size])
for type in [0,1,2,3,255]:
    for team in ([0,1,2,3,255] if type==0 else [0]):
        for mode in ['none','alliance','league']:
            for land in ['', 'SUB,HOUSE_A,HOUSE_B','HOUSE_B,HOUSE_A','house_A']:
                add('run',[type,team,231,3,3,mode,land,1])
for type in [0,1,255]:
    for size,available in [(-1,0),(0,0),(6145,8),(3,2)]:
        for present in [0,1]:add('run',[type,0,231,size,available,'league','HOUSE_A',present])
subprocess.run(['javac',*[str(p) for p in PKG.glob('*.java')]],check=True,capture_output=True)
run=subprocess.run(['java','-cp',str(PKG),'ClientBroadcastOracle'],input='\n'.join(queries)+'\n',text=True,capture_output=True,check=True);lines=run.stdout.splitlines();assert len(lines)==len(cases),(len(lines),len(cases))
for c,line in zip(cases,lines):c['expected']=line.split('|')
fixture={'upstreamSha':SHA,'boundary':'Full Java enum/CM/SM bodies with declared scalar collaborators for memberships,known candidates and ordered house lands. Not full Java world/lifecycle/persistence.','sources':{paths[n]+'.java':hashlib.sha256(b).hexdigest() for n,b in raw.items()},'cases':cases}
target=ROOT/'parity-artifacts/golden/formulas/ClientBroadcast.6dd3aa507.json';target.write_text(json.dumps(fixture,indent=2)+'\n',encoding='utf-8',newline='')
print(json.dumps({'cases':len(cases),'byKind':{k:sum(c['kind']==k for c in cases) for k in ['read','writer','run']}}))
