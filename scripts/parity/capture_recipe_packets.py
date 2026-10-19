"""Capture e5c133836's exact recipe/craft packet bodies with a scalar item and LE writer stub."""
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT=Path(__file__).resolve().parents[2]
SHA='e5c133836804f97e86b9d026c2954ffdebd0609c'
PACKAGE=ROOT/'artifacts/upstream/e5c133836-fixed-limited-crafting-recipes-and-morphing-195/java-capture'

def method(source,marker):
    start=source.index(marker);i=source.index('{',start)+1;depth=1
    while depth:depth+=(source[i]=='{')-(source[i]=='}');i+=1
    return source[start:i]

def main():
    src=PACKAGE/'src';src.mkdir(parents=True,exist_ok=True);hashes={}
    def original(path):
        raw=subprocess.check_output(['git','-C',str(ROOT.parent/'aion-server'),'show',SHA+':'+path])
        hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode('utf-8')
    for name in ['SM_CRAFT_UPDATE','SM_LEARN_RECIPE','SM_RECIPE_LIST','SM_SKILL_REMOVE']:
        path=f'game-server/src/com/aionemu/gameserver/network/aion/serverpackets/{name}.java'
        text=re.sub(r'^package .*;|^import com\..*;','',original(path),flags=re.M)
        (src/(name+'.java')).write_text(text,encoding='utf-8')
    player=original('game-server/src/com/aionemu/gameserver/model/skill/PlayerSkillEntry.java')
    skill=original('game-server/src/com/aionemu/gameserver/model/skill/SkillEntry.java')
    bodies=[method(player,'public '+result+' '+name+'(') for result,name in [('boolean','isProfessionSkill'),('boolean','isTappingSkill'),('boolean','isCraftingSkill'),('boolean','isMorphSkill'),('int','getProfessionFlag'),('int','getCurrentXp'),('int','getSkillType')]]
    bodies += [method(skill,'public final int '+name+'(') for name in ['getSkillId','getSkillLevel']]
    constant=re.search(r'public static final int MORPH_SKILL_ID = \d+;',player).group()
    (src/'PlayerSkillEntry.java').write_text('class PlayerSkillEntry {int skillId,skillLevel,skillType,currentXp;'+constant+'PlayerSkillEntry(int id,int level,int type){skillId=id;skillLevel=level;skillType=type;}'+''.join(bodies)+'}',encoding='utf-8')
    (src/'Stubs.java').write_text('''class AionConnection {}
class ItemTemplate { int id; String l10n; ItemTemplate(int id,String l10n){this.id=id;this.l10n=l10n;} int getTemplateId(){return id;} String getL10n(){return l10n;} }
abstract class AionServerPacket {
 final java.io.ByteArrayOutputStream bytes=new java.io.ByteArrayOutputStream();
 protected void writeC(int x){bytes.write(x);} protected void writeH(int x){writeC(x);writeC(x>>>8);}
 protected void writeD(int x){writeH(x);writeH(x>>>16);} protected void writeS(String x){if(x!=null)for(char c:x.toCharArray())writeH(c);writeH(0);}
 protected abstract void writeImpl(AionConnection c); byte[] payload(){writeImpl(null);return bytes.toByteArray();}
}
''',encoding='utf-8')
    (src/'RecipePacketOracle.java').write_text('''import java.util.*;
public class RecipePacketOracle {
 public static void main(String[] args)throws Exception {
  try(var reader=new java.io.BufferedReader(new java.io.InputStreamReader(System.in))){String line;
   while((line=reader.readLine())!=null){String[] p=line.split(" ");AionServerPacket packet;
    switch(p[0]){
     case "craft":packet=new SM_CRAFT_UPDATE(Integer.parseInt(p[1]),new ItemTemplate(Integer.parseInt(p[2]),p[8].equals("-")?null:p[8]),Integer.parseInt(p[3]),Integer.parseInt(p[4]),Integer.parseInt(p[5]),Integer.parseInt(p[6]),Integer.parseInt(p[7]));break;
     case "learn":packet=new SM_LEARN_RECIPE(Integer.parseInt(p[1]),Integer.parseInt(p[2]));break;
     case "remove":packet=new SM_SKILL_REMOVE(new PlayerSkillEntry(Integer.parseInt(p[1]),Integer.parseInt(p[2]),Integer.parseInt(p[3])));break;
     default:var map=new LinkedHashMap<Integer,Integer>();if(!p[1].equals("-"))for(String pair:p[1].split(",")){var v=pair.split(":");map.put(Integer.parseInt(v[0]),Integer.parseInt(v[1]));}packet=new SM_RECIPE_LIST(map);
    }System.out.println(java.util.HexFormat.of().withUpperCase().formatHex(packet.payload()));
   }
  }
 }
}''',encoding='utf-8')
    cases=[];commands=[]
    def craft(name,v):
        cases.append(dict(name=name,kind='craft',inputs=v));commands.append('craft '+' '.join(str(v[k]) for k in ['skillId','itemId','success','failure','action','executionSpeed','delay'])+' '+(v.get('l10n') or '-'))
    for skill in [40001,40009]:
        for action in range(8):
            for delay in [0,3000]:craft(f'craft-{skill}-{action}-{delay}',dict(skillId=skill,itemId=199999901,success=1000,failure=50,action=action,executionSpeed=1200,delay=delay,l10n='Product'))
    for count in [0,1,127,255]:
        cases.append(dict(name=f'learn-{count}',kind='learn',inputs=dict(id=9001,count=count)));commands.append(f'learn 9001 {count}')
    for pairs in [[],[[9001,3],[9002,0]],[[2147483647,127],[-1,255]]]:
        cases.append(dict(name=f'list-{len(cases)}',kind='list',inputs=dict(pairs=pairs)));commands.append('list '+(','.join(f'{a}:{b}' for a,b in pairs) or '-'))
    legacy=ROOT/'parity-artifacts/golden/packets/SM_CRAFT_UPDATE.json';old=json.loads(legacy.read_text(encoding='utf-8-sig'));start=len(cases)
    for c in old['cases']:craft(c['name'],c['inputs'])
    removeFile=ROOT/'parity-artifacts/golden/packets/SM_SKILL_REMOVE.json';remove=json.loads(removeFile.read_text(encoding='utf-8-sig'));removeStart=len(cases)
    for c in remove['cases']:
        cases.append(dict(name=c['name'],kind='remove',inputs=c['inputs']));v=c['inputs'];commands.append(f'remove {v["skillId"]} {v["skillLvl"]} {v["skillType"]}')
    classes=PACKAGE/'classes';classes.mkdir(exist_ok=True)
    subprocess.run(['javac','-encoding','UTF-8','-d',str(classes),*[str(f) for f in src.glob('*.java')]],check=True)
    result=subprocess.run(['java','-cp',str(classes),'RecipePacketOracle'],input='\n'.join(commands)+'\n',capture_output=True,text=True,check=True)
    values=result.stdout.splitlines();assert len(values)==len(cases)
    for c,payload in zip(cases,values):c['payloadHex']=payload
    for c,result in zip(old['cases'],cases[start:removeStart]):
        c['payloadHex']=result['payloadHex']
        if c['name']=='morphForcesDelay1000':c['name']='morphSuccessUsesExplicitDelay'
    old['sourceJavaCommit']=SHA
    legacy.write_text(json.dumps(old,indent=2)+'\n',encoding='utf-8')
    for c,result in zip(remove['cases'],cases[removeStart:]):c['payloadHex']=result['payloadHex']
    remove['sourceJavaCommit']=SHA;removeFile.write_text(json.dumps(remove,indent=2)+'\n',encoding='utf-8')
    fixture=dict(source='Java',sourceJavaCommit=SHA,sourceHashes=hashes,captureBoundary='Exact packet bodies and extracted skill/profession methods; only packages/imports removed, scalar ItemTemplate/skill fields and LE/UTF16 writer stubs',cases=cases[:start])
    (ROOT/'parity-artifacts/golden/packets/RecipeCraft.e5c133836.json').write_text(json.dumps(fixture,indent=2)+'\n',encoding='utf-8')
    print(f'Captured {start} new cases and recaptured {len(old["cases"])} craft/{len(remove["cases"])} skill-removal historical inputs from {SHA}')

if __name__=='__main__':main()
