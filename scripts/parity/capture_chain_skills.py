"""Execute b55aaf32c's ChainSkills and ChainCondition with a controlled clock.

Package/import/XML annotations are stripped; System.currentTimeMillis is replaced
by a fixed clock. Player, Skill and packet-send collaborators are boundary doubles.
"""
import hashlib
import json
import os
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SHA = 'b55aaf32c86a41db8cac4806985531479f1b98d5'
OUT = ROOT / 'artifacts/upstream/b55aaf32c-fixed-chain-skills-expiring-too-early-217/java-capture'


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    hashes = {}
    java = Path(os.environ.get('BEYOND_AION_JAVA_ROOT', ROOT.parent / 'aion-server'))
    for name, folder in [('ChainSkills', 'model'), ('ChainCondition', 'condition')]:
        path = f'game-server/src/com/aionemu/gameserver/skillengine/{folder}/{name}.java'
        raw = subprocess.check_output(['git', '-C', str(java), 'show', SHA + ':' + path])
        hashes[path] = hashlib.sha256(raw).hexdigest()
        text = re.sub(r'^package .*;|^import .*;|^\s*@Xml[^\n]*', '', raw.decode('utf-8'), flags=re.M)
        text = text.replace('System.currentTimeMillis()', 'Clock.now')
        (OUT / (name + '.java')).write_text(text, encoding='utf-8')
    (OUT / 'ChainOracle.java').write_text(r'''
import java.io.*;import java.lang.reflect.*;
public class ChainOracle {
    public static void main(String[] args)throws Exception {
        try(BufferedReader in=new BufferedReader(new FileReader(args[0]))) {
            for(String line;(line=in.readLine())!=null;) {
                String[] v=line.split("[|]",-1);
                Clock.now=1000000;Player p=new Player();int count=Integer.parseInt(v[1]);
                for(int i=0;i<count;i++)p.chain.updateChain(v[0],Integer.parseInt(v[2]));
                Clock.now+=Long.parseLong(v[3]);ChainCondition c=new ChainCondition();
                String[] fields={"time","selfCount","preCount","category","preCategory"};
                for(int i=0;i<fields.length;i++){
                    Field f=ChainCondition.class.getDeclaredField(fields[i]);f.setAccessible(true);
                    f.set(c,i<3?Integer.valueOf(v[i+4]):v[i+4].equals("NULL")?null:v[i+4]);
                }
                Skill s=new Skill(p);boolean ok=c.validate(s);
                System.out.println(ok+"|"+s.reset+"|"+p.messages+"|"+c.getActivationWindow()+"|"+p.chain.getCurrentChainCount(v[0]));
            }
        }
    }
}
class Clock { static long now; }
abstract class Condition { public abstract boolean validate(Skill s); }
class Player { ChainSkills chain=new ChainSkills();int messages;ChainSkills getChainSkills(){return chain;} }
class Skill { Player player;boolean reset;Skill(Player p){player=p;}Object getEffector(){return player;}void setChainResetOnCastStart(boolean b){reset=b;} }
class SM_SYSTEM_MESSAGE { static int STR_SKILL_CANT_CAST_IN_NONE_CHAINSKILL_STATUS(){return 1300107;} }
class PacketSendUtility { static void sendPacket(Player p,int message){if(message!=1300107)throw new AssertionError();p.messages++;} }
''', encoding='utf-8')
    cases = []

    def case(initial='OPEN', count=1, hit=0, elapsed=0, time=0, self_count=1, pre_count=1, category='NEXT', pre='OPEN'):
        cases.append(dict(initial=initial, count=count, hit=hit, elapsed=elapsed, time=time,
                          selfCount=self_count, preCount=pre_count, category=category, pre=pre))

    for time in (0, 500, 5000):
        window = 3000 if time == 0 else time + 1500
        for hit in (0, 750):
            for elapsed in (0, hit + window - 1, hit + window, hit + window + 1):
                case(time=time, hit=hit, elapsed=elapsed)
        for count in (1, 2, 3):
            for elapsed in (window - 1, window):
                case(count=count, time=time, elapsed=elapsed, self_count=3, category='OPEN', pre='ROOT')
    for count in (0, 1, 2, 3):
        case(count=count, pre_count=2)
        case(initial='OTHER', count=count, pre_count=2)
    for self_count in (1, 3):
        for count in (1, 3):
            for elapsed in (0, 10000):
                case(count=count, elapsed=elapsed, self_count=self_count, category='OPEN', pre=None)
    case(time=2147483647)
    case(time=-1500)
    lines = ['|'.join(str(c[k]) if c[k] is not None else 'NULL' for k in
                      ('initial', 'count', 'hit', 'elapsed', 'time', 'selfCount', 'preCount', 'category', 'pre')) for c in cases]
    (OUT / 'cases.txt').write_text('\n'.join(lines), encoding='utf-8')
    subprocess.run(['javac', '-encoding', 'UTF-8', '-d', str(OUT), *map(str, OUT.glob('*.java'))], check=True)
    rows = subprocess.check_output(['java', '-cp', str(OUT), 'ChainOracle', str(OUT / 'cases.txt')], text=True).splitlines()
    assert len(rows) == len(cases)
    for c, row in zip(cases, rows):
        ok, reset, messages, window, count = row.split('|')
        c.update(accepted=ok == 'true', resetOnStart=reset == 'true', messages=int(messages), window=int(window), countAfter=int(count))
    fixture = dict(source='Java', sourceJavaCommit=SHA, sourceHashes=hashes,
                   captureBoundary='Exact ChainSkills and ChainCondition classes with fixed clock substitution; scalar Player/Skill/message collaborators. Does not execute full Skill.endCast.', cases=cases)
    (ROOT / 'parity-artifacts/golden/formulas/ChainSkills.b55aaf32c.json').write_text(json.dumps(fixture, indent=2) + '\n', encoding='utf-8')
    print('Captured', len(cases), 'exact Java chain/condition cases')


if __name__ == '__main__':
    main()
