"""Capture a485569df's full MultiClientService and exact character-ban method.

Only package/import declarations and the clock are substituted. Scalar world,
connection/account/player/config/logger doubles bound the capture; no SQL/login
pipeline or concurrent admission guarantee is claimed.
"""
import hashlib
import json
import os
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SHA = 'a485569dfa0bd56eaf5c9e6b8ffa46bc05d1562a'
OUT = ROOT / 'artifacts/upstream/a485569df-multi-client-limit-216/java-capture'
BASE = 'game-server/src/com/aionemu/gameserver/'


def block(text, signature):
    start = text.index(signature)
    end = text.index('{', start) + 1
    depth = 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[start:end]


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    java = Path(os.environ.get('BEYOND_AION_JAVA_ROOT', ROOT.parent / 'aion-server'))
    hashes = {}

    def source(relative):
        path = BASE + relative + '.java'
        raw = subprocess.check_output(['git', '-C', str(java), 'show', SHA + ':' + path])
        hashes[path] = hashlib.sha256(raw).hexdigest()
        return raw.decode('utf-8')

    for name, relative in [('MultiClientService', 'services/player/MultiClientService'), ('CharacterBanInfo', 'model/account/CharacterBanInfo')]:
        text = re.sub(r'^package .*;|^import (?:com\.|org\.).*;', '', source(relative), flags=re.M)
        (OUT / (name + '.java')).write_text(text.replace('System.currentTimeMillis()', 'Clock.now'), encoding='utf-8')
    ban = block(source('network/aion/serverpackets/AbstractPlayerInfoPacket'), 'private CharacterBanInfo getCharBanInfo(')
    (OUT / 'BanOracle.java').write_text('class BanOracle {\n' + ban.replace('private CharacterBanInfo', 'CharacterBanInfo', 1).replace('System.currentTimeMillis()', 'Clock.now') + '\n}', encoding='utf-8')
    (OUT / 'MultiClientOracle.java').write_text(r'''
import java.util.*;import java.io.*;import java.lang.reflect.*;import java.nio.charset.StandardCharsets;
public class MultiClientOracle {
    static String value(String s){return s.equals("~")?"":s;}
    public static void main(String[]args)throws Exception{
        try(BufferedReader reader=new BufferedReader(new FileReader(args[0]))){
            for(String line;(line=reader.readLine())!=null;){
                String[] parts=line.split("[|]",-1);Clock.now=1700000000000L;World.players.clear();
                Field cache=MultiClientService.class.getDeclaredField("sessionsByAccountId");cache.setAccessible(true);((Map<?,?>)cache.get(null)).clear();
                SecurityConfig.MULTI_CLIENT_MAX_CLIENTS_PER_COMPUTER=Integer.parseInt(parts[0]);
                SecurityConfig.MULTI_CLIENT_ALLOW_BOTH_FACTIONS=Boolean.parseBoolean(parts[1]);
                SecurityConfig.MULTI_CLIENT_FACTION_SWITCH_COOLDOWN_MINUTES=Integer.parseInt(parts[2]);
                SecurityConfig.MULTI_CLIENT_IGNORED_MAC_ADDRESSES=new HashSet<>();
                if(!parts[3].isEmpty())SecurityConfig.MULTI_CLIENT_IGNORED_MAC_ADDRESSES.addAll(List.of(parts[3].split(",")));
                Map<Integer,Player> players=new HashMap<>();StringJoiner results=new StringJoiner(";");
                for(String command:parts[4].split(";")){
                    String[] a=command.split(",",-1);String result="-";
                    if(a[0].equals("T")){Clock.now+=Long.parseLong(a[1]);}
                    else if(a[0].equals("L")){Player p=players.get(Integer.parseInt(a[1]));MultiClientService.onLeaveWorld(p);p.connection=null;}
                    else{
                        int id=Integer.parseInt(a[1]);Race race=Race.valueOf(a[2]);
                        Player p=players.computeIfAbsent(id,k->new Player(id,race));
                        AionConnection con=new AionConnection(value(a[3]),value(a[4]),value(a[5]),p.account);
                        if(a[0].equals("E")){Msg denial=MultiClientService.tryEnterWorld(p,con);result=denial==null?"allowed":denial.name();}
                        else if(a[0].equals("O")){p.connection=con;if(!World.players.contains(p))World.players.add(p);}
                        else if(a[0].equals("X")){p.connection=null;World.players.add(p);}
                        else if(a[0].equals("Q")){result=MultiClientService.checkForFactionSwitchCooldownTime(race,con)==null?"clear":"blocked";}
                        else if(a[0].equals("B")){
                            p.account.access=Integer.parseInt(a[6]);
                            CharacterBanInfo real=a[7].equals("0")?null:new CharacterBanInfo(Clock.now/1000,Long.parseLong(a[7]),"existing punishment");
                            CharacterBanInfo b=new BanOracle().getCharBanInfo(new PlayerAccountData(race,real),con);
                            result=b==null?"none":Base64.getEncoder().encodeToString(b.getReason().getBytes(StandardCharsets.UTF_8))+":"+(b.getEnd()-b.getStart());
                        }
                    }
                    results.add(result);
                }
                System.out.println(results);
            }
        }
    }
}
class Clock{static long now;}
enum Race{ELYOS,ASMODIANS}
enum Msg{CONNECTION_ERROR,BOTH_FACTIONS}
class SecurityConfig{static int MULTI_CLIENT_MAX_CLIENTS_PER_COMPUTER,MULTI_CLIENT_FACTION_SWITCH_COOLDOWN_MINUTES;static boolean MULTI_CLIENT_ALLOW_BOTH_FACTIONS;static Set<String> MULTI_CLIENT_IGNORED_MAC_ADDRESSES;}
class Logger{void info(String message,Object...args){}}
class LoggerFactory{static Logger getLogger(Class<?> type){return new Logger();}}
class World{static List<Player> players=new ArrayList<>();static World getInstance(){return new World();}List<Player> getAllPlayers(){return players;}}
class Account{int id,access;Account(int id){this.id=id;}int getId(){return id;}int getAccessLevel(){return access;}}
class Player{Account account;Race race;AionConnection connection;Player(int id,Race race){account=new Account(id);this.race=race;}Account getAccount(){return account;}Race getRace(){return race;}AionConnection getClientConnection(){return connection;}}
class AionConnection{String ip,mac,hdd;Account account;AionConnection(String i,String m,String h,Account a){ip=i;mac=m;hdd=h;account=a;}String getIP(){return ip;}String getMacAddress(){return mac;}String getHddSerial(){return hdd;}Account getAccount(){return account;}}
class PlayerAccountData{Race race;CharacterBanInfo ban;PlayerAccountData(Race r,CharacterBanInfo b){race=r;ban=b;}CharacterBanInfo getCharBanInfo(){return ban;}PlayerAccountData getPlayerCommonData(){return this;}Race getRace(){return race;}}
''', encoding='utf-8')
    cases = []

    def row(steps, limit=2, both=True, minutes=20, ignored=''):
        cases.append(dict(limit=limit, both=both, minutes=minutes, ignored=ignored.split(',') if ignored else [], steps=steps))

    def event(op, id=1, race='ELYOS', ip='IP', mac='MAC', hdd='HDD', access=0, ban=0):
        return dict(op=op, id=id, race=race, ip=ip, mac=mac, hdd=hdd, access=access, ban=ban)

    for limit in (0, -1, 1, 2, 3):
        for count in (0, 1, 2, 3):
            row([event('O', i+1) for i in range(count)] + [event('E', 99)], limit=limit)
    for ip, mac, hdd in [('IP','OTHER','HDD'),('OTHER','MAC','HDD'),('IP','mac','HDD'),('IP','','HDD'),('IP','MAC','other')]:
        row([event('O'),event('E',99,ip=ip,mac=mac,hdd=hdd)],limit=1)
    row([event('X'),event('O',2,mac=''),event('E',99,mac='')],limit=1)
    for limit in (0, 1, 2):
        for ignored in ('', 'MAC'):
            row([event('E'),event('O'),event('E',99,'ASMODIANS')],limit=limit,both=False,ignored=ignored)
    for ip, mac in [('IP','OTHER'),('OTHER','MAC'),('OTHER','OTHER')]:
        row([event('E'),event('E',99,'ASMODIANS',ip,mac)],limit=0,both=False)
    for minutes in (0, 20):
        row([event('E'),event('Q',99,'ASMODIANS'),dict(op='L',id=1),event('Q',99,'ASMODIANS'),dict(op='T',elapsed=max(0,minutes*60000-1)),event('Q',99,'ASMODIANS'),dict(op='T',elapsed=1),event('Q',99,'ASMODIANS')],limit=0,both=False,minutes=minutes)
        for race in ('ELYOS','ASMODIANS'):
            for access in (0, 1):
                opposite='ASMODIANS' if race=='ELYOS' else 'ELYOS'
                row([event('E',1,opposite),event('B',99,race,access=access)],limit=0,both=False,minutes=minutes)
    row([event('E'),event('B',99,'ASMODIANS',ban=10)],limit=0,both=False)
    row([event('E',mac='a'),event('E',ip='B',mac='IGN'),event('Q',99,'ASMODIANS',ip='IP',mac='other')],limit=0,both=False,ignored='IGN')
    steps=[]
    for name in ('a','b','c','d'): steps += [event('E',ip=name,mac=name),dict(op='L',id=1),dict(op='T',elapsed=1)]
    steps += [event('Q',99,'ASMODIANS',ip='a',mac='other'),event('Q',99,'ASMODIANS',ip='b',mac='other')]
    row(steps,limit=0,both=False)
    lines=[]
    for c in cases:
        commands=[]
        for s in c['steps']:
            if s['op']=='T': commands.append('T,'+str(s['elapsed']))
            elif s['op']=='L': commands.append('L,'+str(s['id']))
            else: commands.append(','.join(str(s[k]) if s[k]!='' else '~' for k in ('op','id','race','ip','mac','hdd','access','ban')))
        lines.append('|'.join([str(c['limit']),str(c['both']).lower(),str(c['minutes']),','.join(c['ignored']),';'.join(commands)]))
    (OUT/'cases.txt').write_text('\n'.join(lines),encoding='utf-8')
    subprocess.run(['javac','-encoding','UTF-8','-d',str(OUT),*map(str,OUT.glob('*.java'))],check=True)
    results=subprocess.check_output(['java','-cp',str(OUT),'MultiClientOracle',str(OUT/'cases.txt')],text=True).splitlines()
    assert len(results)==len(cases)
    for c, result in zip(cases,results):
        values=result.split(';');assert len(values)==len(c['steps'])
        for step, expected in zip(c['steps'],values):step['expected']=expected
    fixture=dict(source='Java',sourceJavaCommit=SHA,sourceHashes=hashes,captureBoundary=__doc__.strip(),cases=cases)
    (ROOT/'parity-artifacts/golden/formulas/MultiClient.a485569df.json').write_text(json.dumps(fixture,indent=2)+'\n',encoding='utf-8')
    print('Captured',len(cases),'Java multi-client scenarios with',sum(len(c['steps']) for c in cases),'steps')


if __name__=='__main__':main()
