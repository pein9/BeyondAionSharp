"""Compile exact 33aa13cb6 door bounds lookup/range methods and chair guard.
Scalar geometry/world collaborators and the exact PositionUtil methods are declared;
this is not a full Java geodata load, door lifecycle or client journey.
"""
import hashlib,json,os,struct,subprocess
from pathlib import Path
from capture_npc_shop import block
ROOT=Path(__file__).resolve().parents[2];JAVA=Path(os.environ.get('BEYOND_AION_JAVA_ROOT',ROOT.parent/'aion-server'));SHA='33aa13cb60b7ce0d096042c4b37ec5413fbe27aa'
PKG=ROOT/'artifacts/upstream/33aa13cb6-fixed-static-doors-and-chairs-being-used-from-an/oracle';PKG.mkdir(parents=True,exist_ok=True)
paths={'GeoMap':'geoEngine/models/GeoMap','GeoService':'world/geo/GeoService','StaticDoorService':'services/StaticDoorService','CM_EMOTION':'network/aion/clientpackets/CM_EMOTION','PositionUtil':'utils/PositionUtil'}
raw={n:subprocess.check_output(['git','-C',str(JAVA),'show',SHA+':game-server/src/com/aionemu/gameserver/'+p+'.java']) for n,p in paths.items()};src={n:b.decode() for n,b in raw.items()}
source='''import java.io.*;import java.util.*;
class Vector3f{float x,y,z;Vector3f(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}
class BoundingBox{String label;Vector3f center;float x,y,z;BoundingBox(String l,Vector3f c,float x,float y,float z){label=l;center=c;this.x=x;this.y=y;this.z=z;}Vector3f getCenter(){return center;}float getXExtent(){return x;}float getYExtent(){return y;}float getZExtent(){return z;}}
class DespawnableNode{Object bound;DespawnableNode(Object b){bound=b;}Object getWorldBound(){return bound;}}
class Radius{float getMaxOfFrontAndSide(){return 0;}}class Template{Radius getBoundRadius(){return new Radius();}}
class VisibleObject{float x,y,z;int world=300100000,instance=1;float getX(){return x;}float getY(){return y;}float getZ(){return z;}int getWorldId(){return world;}int getInstanceId(){return instance;}Template getObjectTemplate(){return new Template();}}
class Player extends VisibleObject{String getPosition(){return "declared";}}class StaticDoor extends VisibleObject{}
class GeoMap{Map<Integer,DespawnableNode[]>despawnableDoors=new HashMap<>();'''+block(src['GeoMap'],'public BoundingBox getDoorBounds(int doorId)')+'''}
class GeoService{Map<Integer,GeoMap>geoMaps=new HashMap<>();static GeoService service=new GeoService();static GeoService getInstance(){return service;}'''+block(src['GeoService'],'public BoundingBox getDoorBounds(int worldId, int doorId)')+'''}
class StaticDoorService{static final float MAX_DOOR_DISTANCE=5,MAX_DOOR_POSITION_DISTANCE=20;'''+block(src['StaticDoorService'],'private boolean isNearDoor(Player player, StaticDoor door, int doorId)')+'''}
class AuditLogger{static boolean audited;static void log(Player p,String m){audited=true;}}
enum EmotionType{CHAIR_SIT,CHAIR_UP,OTHER}
class Chair{Player player;EmotionType emotionType;float x,y,z;static final float MAX_CHAIR_DISTANCE=10;boolean passed;void check(){'''+block(src['CM_EMOTION'],'if ((emotionType == EmotionType.CHAIR_SIT || emotionType == EmotionType.CHAIR_UP) && !PositionUtil.isInRange(player, x, y, z, MAX_CHAIR_DISTANCE))')+'''passed=true;}}
class PositionUtil{'''+''.join(block(src['PositionUtil'],sig) for sig in ['public static boolean isInRange(VisibleObject object, VisibleObject object2, float range)','public static boolean isInRange(VisibleObject object, VisibleObject object2, float range, boolean centerToCenter)','public static boolean isInRange(VisibleObject obj, float x, float y, float z, float range)','public static boolean isInRange(float x1, float y1, float z1, float x2, float y2, float z2, float range)'])+'''}
public class StaticInteractionOracle{
 static float f(String s){return Float.intBitsToFloat((int)Long.parseLong(s,16));}static int n(String s){return Integer.parseInt(s);}
 public static void main(String[]args)throws Exception{var in=new BufferedReader(new InputStreamReader(System.in));String line;while((line=in.readLine())!=null){String[]a=line.split("[|]",-1);var map=new GeoMap();GeoService.service.geoMaps.put(300100000,map);GeoService.service.geoMaps.put(300100001,map);var closed=new BoundingBox("closed",new Vector3f(100,200,300),30,2,10);var open=new BoundingBox("open",new Vector3f(500,500,500),1,1,1);
  String mode=a[1];if(!mode.equals("none"))map.despawnableDoors.put(7,new DespawnableNode[]{mode.charAt(0)=='0'?null:new DespawnableNode(mode.charAt(0)=='n'?null:closed),mode.charAt(1)=='0'?null:new DespawnableNode(open)});
  if(a[0].equals("lookup")){var result=GeoService.getInstance().getDoorBounds(300100000,7);System.out.println(result==null?"null":result.label);continue;}
  var player=new Player();player.x=f(a[2]);player.y=f(a[3]);player.z=f(a[4]);
  if(a[0].equals("door")){var door=new StaticDoor();door.x=500;door.y=500;door.z=500;door.world=n(a[5]);door.instance=n(a[6]);var m=StaticDoorService.class.getDeclaredMethod("isNearDoor",Player.class,StaticDoor.class,int.class);m.setAccessible(true);System.out.println(m.invoke(new StaticDoorService(),player,door,7));}
  else{var chair=new Chair();chair.player=player;chair.emotionType=EmotionType.valueOf(mode);chair.x=f(a[5]);chair.y=f(a[6]);chair.z=f(a[7]);AuditLogger.audited=false;chair.check();System.out.println(chair.passed+"|"+AuditLogger.audited);}
 }}
}
'''
(PKG/'StaticInteractionOracle.java').write_text(source)
cases=[];queries=[]
def add(kind,args):cases.append({'kind':kind,'args':args});queries.append(kind+'|'+'|'.join(map(str,args)))
def bits(v):return struct.pack('>f',v).hex()
for mode in ['none','00','10','01','11','n1']:add('lookup',[mode])
points=[(100,200,300),(135,207,315),(135.001,200,300),(100,207.001,300),(100,200,315.001),(65,193,285),(64.999,200,300),(500,500,500),(519.999,500,500),(520,500,500),(500,519.999,500),(500,500,520),(510,510,510),(float('nan'),200,300),(float('inf'),200,300)]
for mode in ['none','00','10','01','11','n1']:
    for point in points:add('door',[mode,*map(bits,point),300100000,1])
for mode in ['none','10']:
    for world,instance in [(300100001,1),(300100000,2)]:add('door',[mode,*map(bits,(500,500,500)),world,instance])
for emotion in ['CHAIR_SIT','CHAIR_UP','OTHER']:
    for target in [(100,200,300),(109.999,200,300),(110,200,300),(100,210,300),(100,200,310),(105,205,305),(106,206,306),(float('nan'),200,300),(float('inf'),200,300)]:add('chair',[emotion,*map(bits,(100,200,300)),*map(bits,target)])
subprocess.run(['javac',str(PKG/'StaticInteractionOracle.java')],check=True,capture_output=True)
run=subprocess.run(['java','-cp',str(PKG),'StaticInteractionOracle'],input='\n'.join(queries)+'\n',text=True,capture_output=True,check=True);lines=run.stdout.splitlines();assert len(lines)==len(cases),(len(lines),len(cases))
for c,line in zip(cases,lines):c['expected']=line.split('|')
fixture={'upstreamSha':SHA,'boundary':'Exact new GeoMap/GeoService/StaticDoorService methods,exact new CM_EMOTION guard and4PositionUtil methods;scalar geometry/world collaborators;chair audit presence only,not numeric log formatting or full lifecycle.','sources':{paths[n]+'.java':hashlib.sha256(b).hexdigest() for n,b in raw.items()},'cases':cases}
target=ROOT/'parity-artifacts/golden/formulas/StaticInteractionRange.33aa13cb6.json';target.write_text(json.dumps(fixture,indent=2)+'\n',encoding='utf-8',newline='')
print(json.dumps({'cases':len(cases),'byKind':{k:sum(c['kind']==k for c in cases) for k in ['lookup','door','chair']}}))
