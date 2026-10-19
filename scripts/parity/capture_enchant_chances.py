"""Capture 65eebace9's exact enchant/socket chance and supplement-consumption methods.

Only the final boolean comparison is exposed as a float (early rejection is NaN).
Enums are the real Java sources; item/inventory/rate infrastructure is explicitly stubbed.
"""
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT=Path(__file__).resolve().parents[2]
SHA='65eebace942c3e7bfb539139a9f6976fe617c9eb'
PACKAGE=ROOT/'artifacts/upstream/65eebace9-fixed-item-exploits-and-aligned-enchanting-with/java-capture'

def method(source,marker):
    start=source.index(marker);i=source.index('{',start)+1;depth=1
    while depth:depth+=(source[i]=='{')-(source[i]=='}');i+=1
    return source[start:i]

def main():
    src=PACKAGE/'src';src.mkdir(parents=True,exist_ok=True);hashes={}
    def original(path):
        raw=subprocess.check_output(['git','-C',str(ROOT.parent/'aion-server'),'show',SHA+':'+path])
        hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode('utf-8')
    full=original('game-server/src/com/aionemu/gameserver/services/EnchantService.java')
    methods=[]
    for name in ['enchantItem','socketManastone']:
        body=method(full,'public static boolean '+name+'(')
        body=body.replace('public static boolean','public static float',1).replace('return false;','return Float.NaN;').replace('return Rnd.chance() < successChance;','return successChance;')
        methods.append(body)
    methods.append(method(full,'private static boolean consumeSupplements('))
    (src/'EnchantService.java').write_text('public class EnchantService {'+''.join(methods)+'}',encoding='utf-8')
    for name,path in [('EnchantmentStone','model/enchants/EnchantmentStone'),('ItemQuality','model/templates/item/ItemQuality')]:
        text=original('game-server/src/com/aionemu/gameserver/'+path+'.java')
        text=re.sub(r'^package .*;|^import .*;|^@.*','',text,flags=re.M)
        (src/(name+'.java')).write_text(text,encoding='utf-8')
    (src/'Stubs.java').write_text('''import java.util.*;
class EnchantItemAction {int count=2;float chance=20;boolean manastoneOnly;
 int getCount(){return count;}float getChance(){return chance;}boolean isManastoneOnly(){return manastoneOnly;}}
class ItemActions {EnchantItemAction action=new EnchantItemAction();EnchantItemAction getEnchantAction(){return action;}}
class ItemTemplate {int id,level;ItemQuality quality;ItemActions actions=new ItemActions();
 int getTemplateId(){return id;}int getLevel(){return level;}ItemQuality getItemQuality(){return quality;}ItemActions getActions(){return actions;}}
class Item {ItemTemplate template=new ItemTemplate(),fusion;int enchant;boolean amplified;List<Integer> stones=new ArrayList<>(),fusedStones=new ArrayList<>();
 int getItemId(){return template.id;}ItemTemplate getItemTemplate(){return template;}ItemTemplate getFusionedItemTemplate(){return fusion;}
 int getEnchantLevel(){return enchant;}boolean isAmplified(){return amplified;}List<Integer> getItemStones(){return stones;}List<Integer> getFusionStones(){return fusedStones;}
 boolean hasFusionedItem(){return fusion!=null;}int getSockets(boolean fused){return 6;}}
class Inventory {int count,calls;boolean allow;
 long getItemCountByItemId(int id){return count;}
 boolean decreaseByItemId(int id,int amount){calls++;if(!allow||count==0||count<amount)return false;count-=amount;return true;}}
class Player {Inventory inventory=new Inventory();Inventory getInventory(){return inventory;}}
class RatesConfig {static float[] ENCHANTMENT_STONE_BASE_CHANCES,ENCHANTMENT_STONE_AMPLIFIED_CHANCES,MANASTONE_CHANCES;}
class Rates {static float get(Player p,float[] rates){return rates[0];}}
class AuditLogger {static void log(Player p,String s){}}
''',encoding='utf-8')
    (src/'EnchantOracle.java').write_text('''public class EnchantOracle {
 public static void main(String[]args)throws Exception {
  try(var r=new java.io.BufferedReader(new java.io.InputStreamReader(System.in))){String line;
   while((line=r.readLine())!=null){String[]v=line.split(" ");boolean enchant=v[0].equals("E");float base=Float.parseFloat(v[1]);
    Player p=new Player();p.inventory.count=Integer.parseInt(v[2]);p.inventory.allow=v[3].equals("1");
    Item target=new Item();target.template.id=100000111;target.template.level=Integer.parseInt(v[4]);target.template.quality=ItemQuality.values()[Integer.parseInt(v[5])];target.enchant=Integer.parseInt(v[6]);target.amplified=v[7].equals("1");
    Item stone=new Item();stone.template.id=enchant?166000191:167000001;stone.template.level=Integer.parseInt(v[8]);stone.template.quality=ItemQuality.values()[Integer.parseInt(v[9])];
    Item supplement=new Item();supplement.template.id=166100001;supplement.template.actions.action.manastoneOnly=v[10].equals("1");
    boolean fusion=v[11].equals("1");target.fusion=fusion?target.template:null;for(int i=0;i<Integer.parseInt(v[12]);i++)(fusion?target.fusedStones:target.stones).add(i);
    RatesConfig.ENCHANTMENT_STONE_BASE_CHANCES=RatesConfig.ENCHANTMENT_STONE_AMPLIFIED_CHANCES=RatesConfig.MANASTONE_CHANCES=new float[]{base};
    float chance=enchant?EnchantService.enchantItem(p,stone,target,supplement):EnchantService.socketManastone(p,stone,target,supplement,fusion?2:1);
    System.out.println(Integer.toUnsignedString(Float.floatToRawIntBits(chance))+","+p.inventory.count+","+p.inventory.calls);
   }
  }
 }
}''',encoding='utf-8')
    cases=[]
    def case(**v):cases.append(v)
    for base in [0,60,1000]:
        for available in [0,1,4]:
            for allow in [False,True]:
                for level in [0,5,10]:case(kind='E',base=base,available=available,allow=allow,targetLevel=20,targetQuality=2,enchant=level,amplified=False,stoneLevel=20,stoneQuality=2,manastoneOnly=False,fusion=False,stoneCount=0)
    for amplified in [False,True]:
        for only in [False,True]:case(kind='E',base=1000,available=4,allow=True,targetLevel=65,targetQuality=5,enchant=15,amplified=amplified,stoneLevel=20,stoneQuality=2,manastoneOnly=only,fusion=False,stoneCount=0)
    for base in [0,60]:
        for available in [0,1,6]:
            for allow in [False,True]:
                for stoneLevel in [20,30,40]:
                    for count in [0,2,6]:
                        for fusion in [False,True]:case(kind='M',base=base,available=available,allow=allow,targetLevel=20,targetQuality=2,enchant=0,amplified=False,stoneLevel=stoneLevel,stoneQuality=2,manastoneOnly=count==2,fusion=fusion,stoneCount=count)
    order=['kind','base','available','allow','targetLevel','targetQuality','enchant','amplified','stoneLevel','stoneQuality','manastoneOnly','fusion','stoneCount']
    commands=[' '.join(str(int(c[k])) if isinstance(c[k],bool) else str(c[k]) for k in order) for c in cases]
    classes=PACKAGE/'classes';classes.mkdir(exist_ok=True)
    subprocess.run(['javac','-encoding','UTF-8','-d',str(classes),*[str(f) for f in src.glob('*.java')]],check=True)
    result=subprocess.run(['java','-cp',str(classes),'EnchantOracle'],input='\n'.join(commands)+'\n',capture_output=True,text=True,check=True)
    lines=result.stdout.splitlines();assert len(lines)==len(cases)
    for c,line in zip(cases,lines):
        chance,remaining,calls=map(int,line.split(','));c.update(chanceBits=chance,remaining=remaining,consumptionCalls=calls)
    fixture=dict(source='Java',sourceJavaCommit=SHA,sourceHashes=hashes,captureBoundary='Exact chance/consume method bodies; final comparison exposed as float, early rejection as NaN. Real quality/stone enums; scalar item/inventory/rate stubs.',cases=cases)
    (ROOT/'parity-artifacts/golden/formulas/EnchantChances.65eebace9.json').write_text(json.dumps(fixture,indent=2)+'\n',encoding='utf-8')
    print('Captured',len(cases),'chance/consumption cases from',SHA)

if __name__=='__main__':main()
