"""Exact Java 93d5fa15b entry/kill handlers, QuestVars and changeQuestStep.
World/NPC/state/packet collaborators are scalar doubles. Dialog/spawn/teleport
behavior is covered by separate native C# tests, not this capture.
"""
import hashlib,json,os,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];SHA='93d5fa15b126a8ccf1fba99e55b8844a7ae7fe14'
OUT=ROOT/'artifacts/upstream/93d5fa15b-fixed-quest-2002-where-s-rae-219/java-capture'
def block(s,sig):
 start=s.index(sig);end=s.index('{',start)+1;depth=1
 while depth:depth+=(s[end]=='{')-(s[end]=='}');end+=1
 return s[start:end]
def main():
 OUT.mkdir(parents=True,exist_ok=True);hashes={}
 def source(path):
  raw=subprocess.check_output(['git','-C',str(Path(os.environ.get('BEYOND_AION_JAVA_ROOT',ROOT.parent/'aion-server'))),'show',SHA+':'+path]);hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode('utf-8')
 handler=source('game-server/data/handlers/quest/ishalgen/_2002WheresRae.java')
 parent=source('game-server/src/com/aionemu/gameserver/questEngine/handlers/AbstractQuestHandler.java')
 methods='\n'.join(block(handler,sig) for sig in ['public boolean onEnterWorldEvent(QuestEnv env)','public boolean onKillEvent(QuestEnv env)'])
 methods+='\n'+'\n'.join(block(parent,sig) for sig in ['public boolean changeQuestStep(QuestEnv env, int oldStep, int newStep)','public boolean changeQuestStep(QuestEnv env, int step, int nextStep, boolean reward)','public boolean changeQuestStep(QuestEnv env, int step, int nextStep, boolean reward, int varNum)'])
 (OUT/'Rae.java').write_text('class Rae {int questId=2002;int updates;void updateQuestStatus(QuestEnv e){updates++;}\n'+methods+'\n}',encoding='utf-8')
 qv=source('game-server/src/com/aionemu/gameserver/questEngine/model/QuestVars.java');qv=re.sub(r'^package .*;|^import .*;','',qv,flags=re.M);(OUT/'QuestVars.java').write_text(qv,encoding='utf-8')
 (OUT/'RaeOracle.java').write_text(r'''
import java.io.*;
class LoggerFactory {static LoggerFactory getLogger(Class<?>c){return new LoggerFactory();}void warn(String s,Object...v){}}
enum QuestStatus {START,REWARD,COMPLETE;}
enum WorldMapType {ATAXIAR;int getId(){return 320010000;}}
class QuestState {QuestVars vars;QuestStatus status;QuestState(int v,QuestStatus s){vars=new QuestVars(v);status=s;}QuestVars getQuestVars(){return vars;}int getQuestVarById(int id){return vars.getVarById(id);}void setQuestVarById(int id,int v){vars.setVarById(id,v);}void setQuestVar(int v){vars.setVar(v);}QuestStatus getStatus(){return status;}void setStatus(QuestStatus s){status=s;}}
class Player {QuestState qs;int map;Player(QuestState q,int m){qs=q;map=m;}Player getQuestStateList(){return this;}QuestState getQuestState(int id){return qs;}int getWorldId(){return map;}}
class Npc {int id;Npc(int i){id=i;}int getNpcId(){return id;}}
class QuestEnv {Player p;Npc npc;QuestEnv(Player p,int id){this.p=p;npc=new Npc(id);}Player getPlayer(){return p;}Object getVisibleObject(){return npc;}}
class PacketSendUtility {static void sendPacket(Player p,Object m){}}
class SM_SYSTEM_MESSAGE {static Object STR_QUEST_SYSTEMMSG_GIVEUP(int l){return null;}}
class DataManager {static DataManager QUEST_DATA=new DataManager();DataManager getQuestById(int i){return this;}int getL10n(){return 0;}}
public class RaeOracle {public static void main(String[]a)throws Exception{try(BufferedReader r=new BufferedReader(new FileReader(a[0]))){for(String l;(l=r.readLine())!=null;){String[]v=l.split(",");QuestState q=v[1].equals("null")?null:new QuestState(Integer.parseInt(v[2]),QuestStatus.valueOf(v[1]));Player p=new Player(q,Integer.parseInt(v[3]));Rae h=new Rae();QuestEnv e=new QuestEnv(p,Integer.parseInt(v[4]));boolean result=v[0].equals("entry")?h.onEnterWorldEvent(e):h.onKillEvent(e);System.out.println(result+","+(q==null?0:q.getQuestVars().getQuestVars())+","+h.updates);}}}}
''',encoding='utf-8')
 cases=[]
 for status in ['null','START','REWARD']:
  for world in [220010000,320010000]:
   for value in [12,99,13,76]:cases.append(dict(op='entry',status=status,value=value,world=world,npc=0))
 for status in ['START','REWARD']:
  for npc in [210377,210378,210379]:
   for value in [2,3,9,10,67]:cases.append(dict(op='kill',status=status,value=value,world=220010000,npc=npc))
 inputs=OUT/'cases.csv';inputs.write_text('\n'.join(','.join(str(c[k]) for k in ['op','status','value','world','npc']) for c in cases)+'\n',encoding='utf-8')
 subprocess.run(['javac','-encoding','UTF-8',*[str(f) for f in OUT.glob('*.java')]],check=True)
 lines=subprocess.check_output(['java','-cp',str(OUT),'RaeOracle',str(inputs)],text=True).splitlines();assert len(lines)==len(cases)
 for c,line in zip(cases,lines):
  accepted,value,updates=line.split(',');c.update(accepted=accepted=='true',after=int(value),updates=int(updates))
 fixture=dict(sourceJavaCommit=SHA,sourceHashes=hashes,boundary=__doc__,cases=cases)
 (ROOT/'parity-artifacts/golden/formulas/WheresRae.93d5fa15b.json').write_text(json.dumps(fixture,indent=2)+'\n',encoding='utf-8');print(f'Captured {len(cases)} exact Java entry/kill/packed-state cases.')
if __name__=='__main__':main()
