"""Compile d725054e1's table/limit algorithms and reward method against deterministic Java stubs.

Only JAXB metadata/package names are removed; the limit service's wall-clock read is bound to Clock.now.
The reward method is extracted verbatim. SQL and C# integration are tested separately.
"""
import base64
import hashlib
import json
from pathlib import Path
import re
import struct
import subprocess

ROOT=Path(__file__).resolve().parents[2]
SHA="d725054e1431f29ee0d91f0ae85acfe4b9dc3085"
PACKAGE=ROOT/"artifacts/upstream/d725054e1-implement-retail-pvp-exp-system-and-acquisition/java-capture"


def method(source,marker):
    a=source.index(marker);i=source.index("{",a)+1;depth=1
    while depth:depth+=(source[i]=="{")-(source[i]=="}");i+=1
    return source[a:i]


def main():
    assert PACKAGE.resolve().is_relative_to((ROOT/"artifacts/upstream").resolve())
    src=PACKAGE/"src";src.mkdir(parents=True,exist_ok=True)
    sources={};hashes={}
    def original(path):
        data=subprocess.check_output(["git","-C",str(ROOT.parent/"aion-server"),"show",f"{SHA}:{path}"])
        hashes[path]=hashlib.sha256(data).hexdigest();return data.decode("utf-8")
    names=["PvpExpTable","PvpExpModTable","PartyExpModTable","MenteeExpLimitTable"]
    for name in names:
        text=original(f"game-server/src/com/aionemu/gameserver/dataholders/{name}.java")
        text=re.sub(r"^package .*;|^import jakarta.*;","",text,flags=re.M)
        text=re.sub(r"^\s*@[^\r\n]*","",text,flags=re.M)
        (src/(name+".java")).write_text(text,encoding="utf-8")
    for file in ["pvp_exp_table","pvp_exp_mod_table","party_exp_mod_table","mentee_exp_limit_table"]:
        (PACKAGE/(file+".xml")).write_text(original(f"game-server/data/static_data/exp_mod_table/{file}.xml"),encoding="utf-8")
    text=original("game-server/src/com/aionemu/gameserver/services/PvpExpLimitService.java")
    text=re.sub(r"^package .*;|^import com\.aionemu.*;","",text,flags=re.M).replace("System.currentTimeMillis()","Clock.now")
    (src/"PvpExpLimitService.java").write_text(text,encoding="utf-8")
    full=original("game-server/src/com/aionemu/gameserver/services/PvpService.java")
    reward=method(full,"private Map<Player, Integer> calculateXpRewards(").replace("private Map", "public Map",1)
    share=full[full.index("float xpShare ="):full.index("// Add damage last")]
    share="public float calculateShare(int victimXp,int damage,int totalDamage,boolean isFirst){DamageInfo damageInfo=new DamageInfo(damage);DamageInfo firstDamage=isFirst?damageInfo:null;"+share+"return xpShare;}"
    (src/"PvpService.java").write_text("import java.util.*; public class PvpService {private static final float FIRST_ATTACKER_XP_SHARE=0.1f;"+reward+share+"}",encoding="utf-8")
    (src/"Stubs.java").write_text("""import java.util.*;
class Unmarshaller {}
class Clock { static long now; }
class DamageInfo { final int damage; DamageInfo(int damage){this.damage=damage;} int getDamage(){return damage;} }
class PlayerCommonData { long xp; long getPvpExp(){return xp;} void setPvpExp(long value){xp=value;} }
class Player { int id,level; boolean mentor; final PlayerCommonData data=new PlayerCommonData();
 Player(int id,int level,boolean mentor){this.id=id;this.level=level;this.mentor=mentor;}
 int getObjectId(){return id;} int getLevel(){return level;} boolean isMentor(){return mentor;}
 PlayerCommonData getCommonData(){return data;}
}
class World { static final World W=new World(); final Map<Integer,Player> players=new HashMap<>();
 static World getInstance(){return W;} Player getPlayer(int id){return players.get(id);}
}
class DataManager { static PvpExpTable PVP_EXP_TABLE; static PvpExpModTable PVP_EXP_MOD_TABLE;
 static PartyExpModTable PARTY_EXP_MOD_TABLE; static MenteeExpLimitTable MENTEE_EXP_LIMIT_TABLE;
}
class SM_SYSTEM_MESSAGE { static String STR_CANNOT_GET_PVP_EXP_TIMEBASE_LIMIT(){return "TB";}
 static String STR_CANNOT_GET_PVP_EXP_TARGET_LIMIT(){return "TG";}
}
class PacketSendUtility { static final List<String> messages=new ArrayList<>(); static void sendPacket(Player p,String msg){messages.add(msg);} }
class ThreadPoolManager { static final ThreadPoolManager P=new ThreadPoolManager(); Runnable cleanup;
 static ThreadPoolManager getInstance(){return P;} void scheduleAtFixedRate(Runnable r,long initial,long interval){cleanup=r;}
}
""",encoding="utf-8")
    (src/"PvpExpOracle.java").write_text("""import java.util.*;import java.lang.reflect.*;
import javax.xml.parsers.*;import org.w3c.dom.*;
public class PvpExpOracle {
 static void load(Object table,String file)throws Exception{
  Class<?> row=table.getClass().getDeclaredClasses()[0];List<Object> rows=new ArrayList<>();
  NodeList nodes=DocumentBuilderFactory.newInstance().newDocumentBuilder().parse(file).getDocumentElement().getChildNodes();
  for(int i=0;i<nodes.getLength();i++)if(nodes.item(i) instanceof Element e){
   Constructor<?> ctor=row.getDeclaredConstructor();ctor.setAccessible(true);Object r=ctor.newInstance();
   for(Field f:row.getDeclaredFields()){
    String attr=switch(f.getName()){
      case "level"->"lvl";case "levelDiff"->"level_diff";case "expLimit"->"exp_limit";
      case "maxFromAllUser"->"get_max_from_all_user";case "reduceAmount"->"get_max_reduce_amount";
      case "reduceInterval"->"get_max_reduce_interval";case "delayTime"->"delay_time";default->f.getName();};
    String raw=e.getAttribute(attr);f.setAccessible(true);
    if(f.getType()==int[].class)f.set(r,Arrays.stream(raw.split("\\s+")).mapToInt(Integer::parseInt).toArray());
    else if(f.getType()==long.class)f.setLong(r,Long.parseLong(raw));else f.setInt(r,Integer.parseInt(raw));
   }rows.add(r);
  }
  Field field=table.getClass().getDeclaredField("rows");field.setAccessible(true);field.set(table,rows);
  table.getClass().getDeclaredMethod("afterUnmarshal",Unmarshaller.class,Object.class).invoke(table,null,null);
 }
 static String bits(float value){return Integer.toUnsignedString(Float.floatToRawIntBits(value));}
 public static void main(String[] args)throws Exception{
  DataManager.PVP_EXP_TABLE=new PvpExpTable();load(DataManager.PVP_EXP_TABLE,args[0]+"/pvp_exp_table.xml");
  DataManager.PVP_EXP_MOD_TABLE=new PvpExpModTable();load(DataManager.PVP_EXP_MOD_TABLE,args[0]+"/pvp_exp_mod_table.xml");
  DataManager.PARTY_EXP_MOD_TABLE=new PartyExpModTable();load(DataManager.PARTY_EXP_MOD_TABLE,args[0]+"/party_exp_mod_table.xml");
  DataManager.MENTEE_EXP_LIMIT_TABLE=new MenteeExpLimitTable();load(DataManager.MENTEE_EXP_LIMIT_TABLE,args[0]+"/mentee_exp_limit_table.xml");
  PvpExpLimitService service=null;Map<Integer,Player> players=new HashMap<>();
  try(var reader=new java.io.BufferedReader(new java.io.InputStreamReader(System.in))){String line;
   while((line=reader.readLine())!=null){String[] p=line.split(" ");String result;
    switch(p[0]){
     case "table":{int l=Integer.parseInt(p[1]);var t=DataManager.PVP_EXP_TABLE;result=t.getExp(l)+","+t.getMaxFromAllUser(l)+","+t.getReduceAmount(l)+","+t.getReduceIntervalMillis(l)+","+t.getDelayTimeMillis(l)+","+DataManager.MENTEE_EXP_LIMIT_TABLE.getExpLimit(l);break;}
     case "mod":result=bits(DataManager.PVP_EXP_MOD_TABLE.getMultiplier(Integer.parseInt(p[1]),Integer.parseInt(p[2])));break;
     case "weight":result=bits(DataManager.PARTY_EXP_MOD_TABLE.getWeight(Integer.parseInt(p[1]),Integer.parseInt(p[2])));break;
     case "share":result=bits(new PvpService().calculateShare(Integer.parseInt(p[1]),Integer.parseInt(p[2]),Integer.parseInt(p[3]),p[4].equals("1")));break;
     case "rewards":{
      List<Player> members=new ArrayList<>();int id=1;for(String raw:p[4].split(",")){String[] v=raw.split(":");members.add(new Player(id++,Integer.parseInt(v[0]),v[1].equals("1")));}
      var rewards=new PvpService().calculateXpRewards(members,p[1].equals("1"),new Player(999,Integer.parseInt(p[2]),false),Float.parseFloat(p[3]));
      result=String.join(",",members.stream().map(m->rewards.get(m).toString()).toList());break;
     }
     case "reset":{
      Constructor<PvpExpLimitService> ctor=PvpExpLimitService.class.getDeclaredConstructor();ctor.setAccessible(true);service=ctor.newInstance();players.clear();World.W.players.clear();result="reset";break;
     }
     default:{
      Clock.now=Long.parseLong(p[1]);PacketSendUtility.messages.clear();boolean allowed=false;int id=Integer.parseInt(p[2]);
      Player player=players.computeIfAbsent(id,k->new Player(k,30,false));
      switch(p[0]){
       case "set":player.data.xp=Long.parseLong(p[3]);break;
       case "enter":World.W.players.put(id,player);service.onEnterWorld(player);break;
       case "leave":service.onLeaveWorld(player);World.W.players.remove(id);break;
       case "gain":allowed=service.tryGainPvpExp(player,players.computeIfAbsent(Integer.parseInt(p[3]),k->new Player(k,30,false)),Long.parseLong(p[4]));break;
       case "cleanup":ThreadPoolManager.P.cleanup.run();break;
       default:throw new IllegalArgumentException(line);
      }
      Field map=PvpExpLimitService.class.getDeclaredField("limitsByPlayer");map.setAccessible(true);Map<?,?> limits=(Map<?,?>)map.get(service);
      Object limit=limits.get(id);long last=Long.MIN_VALUE;int victims=0;
      if(limit!=null){Field f=limit.getClass().getDeclaredField("lastReduceTime");f.setAccessible(true);last=f.getLong(limit);f=limit.getClass().getDeclaredField("cooldownEndByVictim");f.setAccessible(true);victims=((Map<?,?>)f.get(limit)).size();}
      result=(allowed?"1":"0")+","+player.data.xp+","+last+","+victims+","+limits.size()+","+String.join("|",PacketSendUtility.messages);
     }
    }System.out.println(result);
   }
  }
 }
}
""",encoding="utf-8")
    commands=[]
    def query(*parts):commands.append(" ".join(map(str,parts)));return len(commands)-1
    levels=[-5,0,*range(1,111),2147483647,-2147483648]
    table=[dict(level=l,index=query("table",l)) for l in levels]
    mods=[query("mod",k,v) for k in range(1,111) for v in range(1,111)]
    weights=[query("weight",m,h) for m in range(1,111) for h in range(1,111)]
    shares=[dict(victimXp=x,damage=d,total=t,first=f,index=query("share",x,d,t,int(f))) for x in [0,1,9,10,5060,2147483647] for d,t in [(1,3),(2,3),(100,100)] for f in [False,True]]
    rewards=[]
    for team in [False,True]:
        for member_levels in [[30],[28,30],[20,30,40],[70,80,100],[1,10]]:
            for mentor in [False,True]:
                for share in ["0","0.01","1.9","10000.5","2.14748365E9"]:
                    members=[dict(level=l,mentor=mentor and i==0) for i,l in enumerate(member_levels)]
                    rewards.append(dict(team=team,victim=30,share=share,members=members,index=query("rewards",int(team),30,share,",".join(f'{m["level"]}:{int(m["mentor"])}' for m in members))))
    scenarios={
      "cap-includes-equality-and-blocked-attempt-updates-both-limits":[("enter",0,1),("set",0,1,5059),("gain",1,1,2,1),("gain",2,1,3,100),("gain",3,1,2,100)],
      "victim-block-still-increments-accumulator":[("enter",0,1),("gain",1,1,2,100),("gain",2,1,2,200)],
      "victim-equality-is-blocked-and-restarts-cooldown":[("gain",0,1,2,1),("gain",1800000,1,2,1),("gain",1800001,1,2,1),("gain",3600002,1,2,1)],
      "fixed-reduction-remainder":[("enter",0,1),("set",0,1,12000),("gain",59000,1,2,10),("gain",60000,1,3,10),("gain",119999,1,4,10),("gain",120000,1,5,10)],
      "logout-reduces-online-time-only":[("enter",0,1),("set",0,1,20000),("leave",90000,1),("enter",9990000,1),("gain",10020000,1,2,1),("leave",10051000,1)],
      "cleanup-keeps-active-players-and-equal-cooldowns":[("enter",0,1),("gain",0,1,2,100),("cleanup",1800000,1),("leave",1800000,1),("cleanup",1800001,1)],
      "relog-keeps-victim-limit":[("enter",0,1),("gain",10,1,2,100),("leave",100,1),("enter",10000,1),("gain",10001,1,2,100)],
      "signed-long-addition":[("enter",0,1),("set",0,1,-10),("gain",1,1,2,9223372036854775807),("gain",2,1,3,100)],
    }
    traces=[]
    for name,steps in scenarios.items():
        query("reset");actions=[]
        for op,now,id,*extra in steps:actions.append(dict(op=op,time=now,id=id,args=extra,index=query(op,now,id,*extra)))
        traces.append(dict(name=name,actions=actions))
    classes=PACKAGE/"classes";classes.mkdir(exist_ok=True)
    subprocess.run(["javac","-encoding","UTF-8","-d",str(classes),*[str(p) for p in src.glob("*.java")]],check=True)
    result=subprocess.run(["java","-cp",str(classes),"PvpExpOracle",str(PACKAGE)],input="\n".join(commands)+"\n",capture_output=True,text=True,check=True)
    values=result.stdout.splitlines();assert len(values)==len(commands)
    for c in table:c["values"]=[int(v) for v in values[c.pop("index")].split(",")]
    for c in rewards:c["expected"]=[int(v) for v in values[c.pop("index")].split(",")]
    for c in shares:c["expectedBits"]=int(values[c.pop("index")])
    for trace in traces:
        for a in trace["actions"]:
            v=values[a.pop("index")].split(",");a["allowed"]=v[0]=="1";a["counter"]=int(v[1]);a["lastReduce"]=int(v[2]);a["victims"]=int(v[3]);a["entries"]=int(v[4]);a["messages"]=v[5].split("|") if v[5] else []
    def packed(indexes):return base64.b64encode(b"".join(struct.pack("<I",int(values[i])) for i in indexes)).decode()
    fixture=dict(schemaVersion=1,source="Java",sourceJavaCommit=SHA,sourceHashes=hashes,captureBoundary="Exact methods/share block; JAXB metadata stripped, DOM field population and deterministic Clock/Player/World/ThreadPool stubs",table=table,pvpMultipliers=packed(mods),partyWeights=packed(weights),matrixLevels=110,shares=shares,rewards=rewards,traces=traces)
    (ROOT/"parity-artifacts/golden/formulas/PvpExp.d725054e1.json").write_text(json.dumps(fixture,indent=2)+"\n",encoding="utf-8")
    print(f"Captured {len(table)} level rows, {len(mods)+len(weights)} float lookups, {len(rewards)} reward splits and {len(traces)} limit traces from {SHA}")


if __name__=="__main__":main()
