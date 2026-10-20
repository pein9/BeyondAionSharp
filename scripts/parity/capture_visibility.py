"""Exact b0a597458 visibility rules, Player/Kisk exemptions, target selection,
range eligibility and SM_PLAYER_STATE writer. Methods/enums are compiled unchanged;
XML annotations/imports alone are stripped. Master/team/known-list/life/packet-sink
collaborators are scalar doubles. No Java world, combat, scheduling, DB or client
execution is claimed; native tests exercise the C# entry points separately.
"""
import hashlib,json,os,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];SHA='b0a597458330ea36e1de112f21a0ab5c899c869a';OUT=ROOT/'artifacts/upstream/b0a597458-unified-invisibility-and-spawn-protection-checks/java-capture';BASE='game-server/src/com/aionemu/gameserver/'
def block(s,sig):
 a=s.index(sig);z=s.index('{',a)+1;d=1
 while d:d+=(s[z]=='{')-(s[z]=='}');z+=1
 return s[a:z]
def main():
 OUT.mkdir(parents=True,exist_ok=True);hashes={}
 def source(p):
  p=BASE+p+'.java';raw=subprocess.check_output(['git','-C',str(Path(os.environ.get('BEYOND_AION_JAVA_ROOT',ROOT.parent/'aion-server'))),'show',SHA+':'+p]);hashes[p]=hashlib.sha256(raw).hexdigest();return raw.decode('utf-8')
 for n,p in [('CreatureVisualState','model/gameobjects/state/CreatureVisualState'),('CreatureSeeState','model/gameobjects/state/CreatureSeeState'),('SM_PLAYER_STATE','network/aion/serverpackets/SM_PLAYER_STATE')]:
  s=source(p);s=re.sub(r'^package .*;|^import (?!java\.).*;|^\s*@[^\n]*','',s,flags=re.M);(OUT/(n+'.java')).write_text(s,encoding='utf-8')
 c=source('model/gameobjects/Creature');p=source('model/gameobjects/player/Player');k=source('model/gameobjects/Kisk');r=source('skillengine/properties/TargetRangeProperty');sel=source('network/aion/clientpackets/CM_TARGET_SELECT')
 methods='\n'.join(block(c,x) for x in ['public boolean canSee(VisibleObject object)','public boolean canTarget(Creature target)','public boolean canAffect(Creature target)','private boolean seesThrough(int visualState)','public boolean ignoresInvisibilityOf(Creature target)'])
 player=block(p,'public boolean ignoresInvisibilityOf(Creature target)');kisk='\n'.join(block(k,x) for x in ['public boolean isOwnerOrTeamMember(Player player)','public boolean isEnemyFrom(Player player)','public CreatureType getType(Creature creature)','private boolean isEnemyRaceInPvPZone(Player player)'])
 checks=block(r,'private static boolean checkCommonRequirements(')+'\n'+block(sel,'private static boolean canSelect(')
 (OUT/'VisibilityOracle.java').write_text(r'''
import java.util.*;import java.io.*;
class VisibleObject {boolean canSee(VisibleObject o){return true;}CreatureType getType(Creature c){return CreatureType.SUPPORT;}}
class Creature extends VisibleObject {int visual,see,id=700001;boolean dead,pvp;Creature master;Creature getMaster(){return master==null?this:master;}int getVisualState(){return visual;}int getSeeState(){return see;}boolean isInSeeState(CreatureSeeState s){return (see&s.getId())!=0;}int getObjectId(){return id;}boolean isDead(){return dead;}boolean isInsidePvPZone(){return pvp;}Known getKnownList(){return new Known();}
'''+methods+r'''
}
class Known {boolean sees(VisibleObject o){return true;}}
class Pet extends VisibleObject {Player owner;Player getMaster(){return owner;}}
class Player extends Creature {int team,opponent;Set<Integer> members=new HashSet<>();boolean isInSameTeam(Player p){return team!=0&&team==p.team;}boolean isInTeam(){return team!=0;}Player getCurrentTeam(){return this;}boolean hasMember(int id){return members.contains(id);}Integer getOpponentId(){return opponent==0?null:opponent;}Integer getRace(){return 0;}
'''+player+r'''
}
class Kisk extends Creature {int creator;Integer ownerRace=0;int getCreatorId(){return creator;}
'''+kisk+r'''
}
enum CreatureType {ATTACKABLE,SUPPORT;}
class DuelService {static DuelService getInstance(){return new DuelService();}Integer getOpponentId(Player p){return p.getOpponentId();}}
class SkillTemplate {boolean resurrect;boolean hasResurrectEffect(){return resurrect;}}
class AionConnection {}
abstract class AionServerPacket {ByteArrayOutputStream out=new ByteArrayOutputStream();void writeC(int i){out.write(i);}void writeD(int i){writeC(i);writeC(i>>>8);writeC(i>>>16);writeC(i>>>24);}protected abstract void writeImpl(AionConnection c);String hex(){out.reset();writeImpl(null);return HexFormat.of().withUpperCase().formatHex(out.toByteArray());}}
public class VisibilityOracle {
'''+checks+r'''
 static Creature[] pair(int rel){Player a=new Player(),b=new Player();a.id=11;b.id=22;Creature o=a,t=b;
  switch(rel){case 1:t=a;break;case 2:o=new Creature();o.master=a;t=new Creature();t.master=a;break;case 3:t=new Creature();t.master=a;break;case 4:o=new Creature();o.master=a;t=a;break;case 5:a.team=b.team=7;o=new Creature();o.master=a;t=new Creature();t.master=b;break;case 6:a.team=b.team=7;a.opponent=b.id;b.opponent=a.id;break;case 7:t=new Kisk();((Kisk)t).creator=b.id;break;case 8:a.team=7;a.members.add(b.id);t=new Kisk();((Kisk)t).creator=b.id;break;case 9:t=new Kisk();((Kisk)t).creator=a.id;break;case 10:o=new Creature();o.master=a;t=new Kisk();((Kisk)t).creator=a.id;break;}
  return new Creature[]{o,t};
 }
 public static void main(String[]args){
  int[] sees={0,1,2,20,64,128,129,130,148,255},visuals={0,1,2,3,20,64,65,84,128,129,148,192,212,255};
  for(int rel=0;rel<11;rel++)for(int see:sees)for(int visual:visuals){var pair=pair(rel);var o=pair[0];var t=pair[1];o.see=see;t.visual=visual;System.out.println("matrix|"+rel+"|"+see+"|"+visual+"|"+o.canSee(t)+"|"+o.canTarget(t)+"|"+o.canAffect(t));}
  for(int see:sees)for(int visual:visuals)for(boolean first:new boolean[]{false,true})for(boolean dead:new boolean[]{false,true})for(boolean resurrect:new boolean[]{false,true}){Player a=new Player();Creature b=new Creature();a.see=see;b.visual=visual;b.dead=dead;SkillTemplate template=new SkillTemplate();template.resurrect=resurrect;System.out.println("range|"+see+"|"+visual+"|"+first+"|"+dead+"|"+resurrect+"|"+checkCommonRequirements(b,a,template,first?b:a));}
  for(int see:sees)for(int visual:visuals){Creature c=new Creature();c.see=see;c.visual=visual;for(boolean redraw:new boolean[]{false,true})System.out.println("packet|"+see+"|"+visual+"|"+redraw+"|"+new SM_PLAYER_STATE(c,redraw).hex());}
  int[] ids={700001,700002,700003},vs={0,64,2},ss={0,0,1};for(int i=0;i<3;i++){Creature c=new Creature();c.id=ids[i];c.visual=vs[i];c.see=ss[i];System.out.println("legacy|"+i+"|"+new SM_PLAYER_STATE(c).hex());}
 }
}
''',encoding='utf-8')
 subprocess.run(['javac','-encoding','UTF-8',*[str(p) for p in OUT.glob('*.java')]],check=True)
 lines=subprocess.check_output(['java','-cp',str(OUT),'VisibilityOracle'],text=True).splitlines();matrix=[];ranges=[];packets=[];legacy={}
 def value(x):return x=='true' if x in ['true','false'] else int(x)
 for line in lines:
  row=line.split('|');kind=row.pop(0)
  if kind=='matrix':matrix.append([value(x) for x in row])
  elif kind=='range':ranges.append([value(x) for x in row])
  elif kind=='packet':packets.append([*[value(x) for x in row[:-1]],row[-1]])
  else:assert kind=='legacy';legacy[int(row[0])]=row[1]
 f=dict(sourceJavaCommit=SHA,sourceHashes=hashes,boundary=__doc__,matrixColumns=['relation','see','visual','canSee','canTarget','canAffect'],rangeColumns=['see','visual','first','dead','resurrect','allowed'],packetColumns=['see','visual','redraw','payloadHex'],matrix=matrix,ranges=ranges,packets=packets)
 # Compact vectors retain readable provenance without thousands of redundant object keys.
 (ROOT/'parity-artifacts/golden/formulas/Visibility.b0a597458.json').write_text(json.dumps(f,separators=(',',':'))+'\n',encoding='utf-8')
 old=json.loads((ROOT/'parity-artifacts/golden/packets/SM_PLAYER_STATE.json').read_text(encoding='utf-8-sig'));old['sourceJavaCommit']=SHA
 for i,c in enumerate(old['cases']):c['payloadHex']=legacy[i]
 (ROOT/'parity-artifacts/golden/packets/SM_PLAYER_STATE.b0a597458.json').write_text(json.dumps(old,indent=2)+'\n',encoding='utf-8')
 print(f'Captured {len(matrix)} visibility, {len(ranges)} range and {len(packets)+len(legacy)} packet vectors from exact Java.')
if __name__=='__main__':main()
