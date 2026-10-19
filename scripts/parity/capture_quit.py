"""Capture exact 6f5feaceb quit handler, response packet and Player timing methods.
Scalar world/account/NPC/DAO collaborators isolate admission and wire behavior;
real logout persistence and native timer cancellation are tested separately in C#.
"""
import hashlib,json,os,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
SHA='6f5feaceb2bda56337f81a8c10c89f6f6b16ff16'
OUT=ROOT/'artifacts/upstream/6f5feaceb-aligned-quitting-the-game-with-retail-215/java-capture'
def block(s,signature):
    start=s.index(signature);end=s.index('{',start)+1;depth=1
    while depth:
        depth+=(s[end]=='{')-(s[end]=='}');end+=1
    return s[start:end]
def main():
    OUT.mkdir(parents=True,exist_ok=True);hashes={}
    def source(path):
        path='game-server/src/com/aionemu/gameserver/'+path+'.java'
        raw=subprocess.check_output(['git','-C',str(Path(os.environ.get('BEYOND_AION_JAVA_ROOT',ROOT.parent/'aion-server'))),'show',SHA+':'+path]);hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode('utf-8')
    for name,path in [('CM_QUIT','network/aion/clientpackets/CM_QUIT'),('CM_MAY_QUIT','network/aion/clientpackets/CM_MAY_QUIT'),('SM_QUIT_RESPONSE','network/aion/serverpackets/SM_QUIT_RESPONSE')]:
        s=re.sub(r'^package .*;|^import (?:com\.|org\.).*;','',source(path),flags=re.M).replace('System.currentTimeMillis()','Clock.now')
        (OUT/(name+'.java')).write_text(s,encoding='utf-8')
    player=source('model/gameobjects/player/Player')
    methods='\n'.join(block(player,'public '+signature) for signature in ['long getQuitCountdownStartMillis()','void setQuitCountdownStartMillis(long quitCountdownStartMillis)','void blockQuitting()','void unblockQuitting()','long getMillisUntilQuittingAllowed()']).replace('System.currentTimeMillis()','Clock.now')
    support=r'''
import java.util.*;import java.io.*;
class Clock {static long now;}
enum State {AUTHED,IN_GAME;}
class AionClientPacket {AionConnection con;AionClientPacket(int op,Set<State>s){} AionConnection getConnection(){return con;}int readC(){return con.stay?1:0;}void sendPacket(SM_QUIT_RESPONSE p){con.sent=p;}protected void readImpl(){}protected void runImpl(){}}
class AionServerPacket {ByteArrayOutputStream bytes=new ByteArrayOutputStream();void writeC(int x){bytes.write(x);}void writeD(int x){for(int i=0;i<4;i++)bytes.write(x>>>(i*8));}protected void writeImpl(AionConnection c){}}
class AionConnection {Player player;boolean stay,closed;SM_QUIT_RESPONSE sent;Player getActivePlayer(){return player;}Account getAccount(){return new Account();}void close(SM_QUIT_RESPONSE p){closed=true;sent=p;}}
class VisibleObject {}
class Npc extends VisibleObject {ObjectTemplate getObjectTemplate(){return new ObjectTemplate();}}
class ObjectTemplate {boolean supportsAction(DialogAction action){return true;}}
enum DialogAction {EDIT_CHARACTER_ALL,EDIT_CHARACTER_GENDER;}
class PositionUtil {static boolean isInTalkRange(Player p,Npc n){return true;}}
class AuditLogger {static void log(Player p,String s){}}
class PlayerPunishmentsDAO {static Object getCharBanInfo(int i){return null;}}
class PlayerLeaveWorldService {static int leaves;static void leaveWorldNow(Player p){leaves++;}}
class Account {List<PlayerAccountData> getPlayerAccDataList(){return List.of();}}
class PlayerAccountData {void setVisibleItems(Object x){}void setCharBanInfo(Object x){}CommonData getPlayerCommonData(){return new CommonData();}}
class CommonData {boolean edit;boolean isInEditMode(){return edit;}int getPlayerObjId(){return 1;}}
class Equipment {Object getEquippedForAppearance(){return null;}}
class Player {long quitCountdownStartMillis,quitBlockedUntilMillis;CommonData data=new CommonData();VisibleObject target;CommonData getCommonData(){return data;}VisibleObject getTarget(){return target;}PlayerAccountData getAccountData(){return new PlayerAccountData();}Equipment getEquipment(){return new Equipment();}
'''+methods+'\n}\n'
    (OUT/'Support.java').write_text(support,encoding='utf-8')
    (OUT/'QuitOracle.java').write_text(r'''
import java.util.*;import java.io.*;
public class QuitOracle {public static void main(String[]args)throws Exception{
try(BufferedReader r=new BufferedReader(new FileReader(args[0]))){for(String line;(line=r.readLine())!=null;){String[]s=line.split(",");
long elapsed=Long.parseLong(s[0]),remaining=Long.parseLong(s[1]);Clock.now=1700000000000L;
Player p=new Player();p.setQuitCountdownStartMillis(Clock.now-elapsed);Clock.now-=10000-remaining;p.blockQuitting();Clock.now=1700000000000L;
p.data.edit=Boolean.parseBoolean(s[2]);if(Boolean.parseBoolean(s[3]))p.target=new Npc();
AionConnection c=new AionConnection();if(Boolean.parseBoolean(s[4]))c.player=p;c.stay=Boolean.parseBoolean(s[5]);PlayerLeaveWorldService.leaves=0;
CM_QUIT packet=new CM_QUIT(3,Set.of(State.AUTHED,State.IN_GAME));packet.con=c;packet.readImpl();packet.runImpl();String hex="none";
if(c.sent!=null){c.sent.writeImpl(c);hex=HexFormat.of().withUpperCase().formatHex(c.sent.bytes.toByteArray());}
System.out.println(hex+","+PlayerLeaveWorldService.leaves+","+c.closed+","+p.getMillisUntilQuittingAllowed());
}}}}
''',encoding='utf-8')
    cases=[]
    def case(name,elapsed=11000,remaining=0,edit=False,target=False,player=True,stay=False):cases.append(dict(name=name,elapsed=elapsed,remaining=remaining,edit=edit,target=target,player=player,stay=stay))
    for elapsed in [-2147483649,-1,0,7999,8000,11000,20000,20001,2147483648,1700000000000]:case('countdown-'+str(elapsed),elapsed=elapsed)
    for remaining in [1,999,1000,1001,9999,10000]:case('blocked-'+str(remaining),remaining=remaining)
    case('countdown-precedes-block',elapsed=7999,remaining=10000)
    case('stay-connected',stay=True)
    case('edit-bypasses-countdown',elapsed=0,edit=True,target=True,stay=True)
    case('edit-still-blocked',elapsed=0,remaining=10000,edit=True,target=True,stay=True)
    case('edit-invalid-target',edit=True,target=False,stay=True)
    case('no-player-close',player=False)
    case('no-player-stay',player=False,stay=True)
    inputs=OUT/'cases.csv';inputs.write_text('\n'.join(','.join(str(c[k]).lower() for k in ['elapsed','remaining','edit','target','player','stay']) for c in cases)+'\n',encoding='utf-8')
    subprocess.run(['javac','-encoding','UTF-8',*[str(f) for f in OUT.glob('*.java')]],check=True)
    lines=subprocess.check_output(['java','-cp',str(OUT),'QuitOracle',str(inputs)],text=True).splitlines();assert len(lines)==len(cases)
    for c,line in zip(cases,lines):
        payload,leaves,closed,remaining=line.split(',');c.update(payloadHex=payload,leaves=int(leaves),closed=closed=='true',blockedMillis=int(remaining))
    fixture=dict(sourceJavaCommit=SHA,sourceHashes=hashes,boundary='Exact Java handlers, Player timing methods and packet writer; scalar collaborators replace logout persistence/account/NPC/DAO. Native logout lifecycle and timers tested separately.',cases=cases)
    path=ROOT/'parity-artifacts/golden/formulas/Quit.6f5feaceb.json';path.write_text(json.dumps(fixture,indent=2)+'\n',encoding='utf-8');print(f'Captured {len(cases)} exact Java quit outcomes and response payloads.')
if __name__=='__main__':main()
