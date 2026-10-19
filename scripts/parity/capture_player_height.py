"""Run 183723078's exact drowning decision with explicit world/player/timer boundary doubles."""
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT=Path(__file__).resolve().parents[2]
SHA='183723078bd11225af6e13fbf7376a0ed3a825ab'
BASE='game-server/src/com/aionemu/gameserver/'

def method(text,signature):
    start=text.index(signature);end=text.index('{',start);depth=1;end+=1
    while depth:
        depth+=(text[end]=='{')-(text[end]=='}');end+=1
    return text[start:end]

def main():
    pkg=ROOT/'artifacts/upstream/183723078-player-height-corrections/java-capture';pkg.mkdir(parents=True,exist_ok=True)
    hashes={};texts={}
    for name,path in [('PlayerAppearance','model/gameobjects/player/PlayerAppearance'),('BoundRadius','model/templates/BoundRadius'),('ZoneLevelService','world/zone/ZoneLevelService')]:
        file=BASE+path+'.java';raw=subprocess.check_output(['git','-C',str(ROOT.parent/'aion-server'),'show',SHA+':'+file])
        hashes[file]=hashlib.sha256(raw).hexdigest();texts[name]=raw.decode('utf-8')
        if name!='ZoneLevelService':
            cleaned=re.sub(r'^package .*;|^import .*;|^\s*@Xml[^\n]*','',texts[name],flags=re.M)
            (pkg/(name+'.java')).write_text(cleaned,encoding='utf-8')
    check=method(texts['ZoneLevelService'],'public static void checkZoneLevels(Player player)')
    source='''public class HeightOracle {
    enum Gender { MALE, FEMALE }
    static class Player {
        Gender gender;float z;boolean dead,drown;PlayerAppearance appearance=new PlayerAppearance();
        float getZ(){return z;}boolean isDead(){return dead;}int getWorldId(){return 1;}
        Player getController(){return this;}void die(){dead=true;}
        Gender getGender(){return gender;}PlayerAppearance getPlayerAppearance(){return appearance;}
    }
    static class World {
        static World getInstance(){return new World();}World getWorldMap(int id){return this;}
        int getDeathLevel(){return -100;}int getWaterLevel(){return 100;}
    }
    static void startDrowning(Player p){p.drown=true;}static void stopDrowning(Player p){p.drown=false;}
    '''+check+'''
    static int bits(float f){return Float.floatToRawIntBits(f);}
    public static void main(String[]args){
        System.out.println(bits(BoundRadius.PLAYER.getFront())+","+bits(BoundRadius.PLAYER.getSide())+","+bits(BoundRadius.PLAYER.getUpper()));
        for(Gender g:Gender.values())for(float s:new float[]{0.5f,0.8f,1f,1.25f,1.5f}){
            // Select neighboring input floats; the expected result is produced by the exact method above.
            float boundary=100-(s*(g==Gender.MALE?1.7f:1.58f)-0.05f);
            for(float z:new float[]{Math.nextDown(boundary),boundary,Math.nextUp(boundary)}){
                Player p=new Player();p.gender=g;p.z=z;p.appearance.setHeightScale(s);checkZoneLevels(p);
                System.out.println(g+","+bits(s)+","+bits(z)+","+p.drown);
            }
        }
    }
}'''
    (pkg/'HeightOracle.java').write_text(source,encoding='utf-8')
    subprocess.run(['javac','-encoding','UTF-8','-d',str(pkg),*[str(pkg/(n+'.java')) for n in ('HeightOracle','PlayerAppearance','BoundRadius')]],check=True)
    lines=subprocess.check_output(['java','-cp',str(pkg),'HeightOracle'],text=True).splitlines()
    bounds=list(map(int,lines[0].split(',')));cases=[]
    for line in lines[1:]:
        gender,scale,z,drown=line.split(',');cases.append(dict(gender=gender,scaleBits=int(scale),zBits=int(z),drown=drown=='true'))
    assert len(cases)==30
    fixture=dict(source='Java',sourceJavaCommit=SHA,sourceHashes=hashes,captureBoundary='Exact checkZoneLevels method and exact PlayerAppearance/BoundRadius; world levels fixed 100/-100, scheduling/stop are observation-only stubs.',boundRadiusBits=bounds,cases=cases)
    (ROOT/'parity-artifacts/golden/formulas/PlayerHeight.183723078.json').write_text(json.dumps(fixture,indent=2)+'\n',encoding='utf-8')
    print('Captured',len(cases),'float-boundary decisions and fixed bounds from',SHA)

if __name__=='__main__':main()
