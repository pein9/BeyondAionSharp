"""Compile 784e0764b's exact stone/quality enums and capture aliases, legacy IDs and rejection bounds."""
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT=Path(__file__).resolve().parents[2]
SHA='784e0764b59fe310cf4103843fc9f5636ea518f3'
PACKAGE=ROOT/'artifacts/upstream/784e0764b-fixed-item-templates-missing-their-item-kind/java-capture'

def main():
    src=PACKAGE/'src';src.mkdir(parents=True,exist_ok=True);hashes={}
    for name,path in [('EnchantmentStone','model/enchants/EnchantmentStone'),('ItemQuality','model/templates/item/ItemQuality')]:
        file='game-server/src/com/aionemu/gameserver/'+path+'.java';raw=subprocess.check_output(['git','-C',str(ROOT.parent/'aion-server'),'show',SHA+':'+file]);hashes[file]=hashlib.sha256(raw).hexdigest()
        text=re.sub(r'^package .*;|^import .*;|^@.*','',raw.decode('utf-8'),flags=re.M)
        (src/(name+'.java')).write_text(text,encoding='utf-8')
    (src/'StoneOracle.java').write_text('''public class StoneOracle {
 public static void main(String[]args)throws Exception {try(var r=new java.io.BufferedReader(new java.io.InputStreamReader(System.in))){String line;
  while((line=r.readLine())!=null){int id=Integer.parseInt(line);try{var s=EnchantmentStone.getByItemId(id);System.out.println(s.name()+","+s.getBaseLevel()+","+s.getBaseQuality().getQualityId());}catch(IllegalArgumentException e){System.out.println("error,"+e.getMessage());}}
 }}
}''',encoding='utf-8')
    ids=[*range(166000000,166000203),*range(166020000,166020005),-2147483648,2147483647,0]
    classes=PACKAGE/'classes';classes.mkdir(exist_ok=True)
    subprocess.run(['javac','-encoding','UTF-8','-d',str(classes),*[str(p) for p in src.glob('*.java')]],check=True)
    result=subprocess.run(['java','-cp',str(classes),'StoneOracle'],input='\n'.join(map(str,ids))+'\n',capture_output=True,text=True,check=True)
    lines=result.stdout.splitlines();assert len(lines)==len(ids);cases=[]
    for id,line in zip(ids,lines):
        v=line.split(',');cases.append(dict(id=id,error=v[1]) if v[0]=='error' else dict(id=id,grade=v[0],level=int(v[1]),qualityId=int(v[2])))
    fixture=dict(source='Java',sourceJavaCommit=SHA,sourceHashes=hashes,captureBoundary='Exact enums; only package/import/JAXB metadata removed.',cases=cases)
    (ROOT/'parity-artifacts/golden/formulas/EnchantmentStone.784e0764b.json').write_text(json.dumps(fixture,indent=2)+'\n',encoding='utf-8')
    print('Captured',len(cases),'stone-grade/rejection cases from',SHA)

if __name__=='__main__':main()
