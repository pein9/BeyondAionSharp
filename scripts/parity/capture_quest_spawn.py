"""Capture cabd580f3's exact Java spawn regex and two changed methods with scalar collaborator stubs."""
import base64,hashlib,json,re,subprocess
from pathlib import Path
from capture_npc_shop import block
ROOT=Path(__file__).resolve().parents[2];JAVA=ROOT.parent/'aion-server';SHA='cabd580f3c2a5bd59b274f4ee57e9b467a44bea2'
PKG=ROOT/'artifacts/upstream/cabd580f3-fixed-false-log-entries-about-missing-quest-spaw/oracle';PKG.mkdir(parents=True,exist_ok=True)
paths=['dataholders/HouseData','questEngine/QuestSpawnAnalyzer','questEngine/handlers/models/KillSpawnedData']
raw={p:subprocess.check_output(['git','-C',str(JAVA),'show',SHA+':game-server/src/com/aionemu/gameserver/'+p+'.java']) for p in paths};texts={p:b.decode() for p,b in raw.items()}
literal=re.search(r'Pattern pattern = Pattern.compile\((".*")\);',texts[paths[1]]).group(1)
source='''import java.util.*;import java.util.regex.*;import java.util.stream.Collectors;
public class SpawnOracle {
 static class HousingLand { int id; HousingLand(int n){id=n;} int getManagerNpcId(){return id;} }
 static class House { List<HousingLand> lands=new ArrayList<>(); '''+block(texts[paths[0]],'public void addAllManagerNpcIdsToSet(')+''' }
 static class Monster { List<Integer> ids;int spawner;Monster(int s,List<Integer> i){spawner=s;ids=i;} List<Integer> getNpcIds(){return ids;} int getSpawnerNpcId(){return spawner;} }
 static class Parent { public Set<Integer> getAlternativeNpcs(int id){return new HashSet<>(List.of(999901,999902));} }
 static class Kill extends Parent { List<Monster> monster=new ArrayList<>(); '''+block(texts[paths[2]],'public Set<Integer> getAlternativeNpcs(')+''' }
 static String result(Set<Integer> ids){return new TreeSet<>(ids).stream().map(String::valueOf).collect(Collectors.joining(","));}
 public static void main(String[]a){
  if(a[0].equals("regex")){Set<Integer> ids=new HashSet<>();Matcher m=Pattern.compile('''+literal+''').matcher(new String(Base64.getDecoder().decode(a[1]),java.nio.charset.StandardCharsets.UTF_8));while(m.find())for(int i=1;i<=m.groupCount();i++)if(m.group(i)!=null)ids.add(Integer.parseInt(m.group(i)));System.out.println(result(ids));}
  else if(a[0].equals("house")){House h=new House();for(String n:a[1].split(","))h.lands.add(new HousingLand(Integer.parseInt(n)));Set<Integer> ids=new HashSet<>(List.of(777777));h.addAllManagerNpcIdsToSet(ids);System.out.println(result(ids));}
  else {Kill k=new Kill();if(!a[2].equals("empty"))for(String row:a[2].split(";")){String[] v=row.split(":",-1);k.monster.add(new Monster(Integer.parseInt(v[0]),v[1].equals("null")?null:v[1].isEmpty()?List.of():Arrays.stream(v[1].split(",")).map(Integer::parseInt).toList()));}try{System.out.println(result(k.getAlternativeNpcs(Integer.parseInt(a[1]))));}catch(NullPointerException e){System.out.println("NullPointerException");}}
 }
}'''
(PKG/'SpawnOracle.java').write_text(source,encoding='utf-8');subprocess.run(['javac','-encoding','UTF-8','-d',str(PKG),str(PKG/'SpawnOracle.java')],check=True)
def run(*args):
 s=subprocess.check_output(['java','-cp',str(PKG),'SpawnOracle',*args],text=True).strip()
 return None if s=='NullPointerException' else [int(v) for v in s.split(',') if v]
inputs=['  spawn(211621, 1);','  spawnForFiveMinutes(211621, instance);','  spawnTemporarily(211622, player);','  sp(211623, 1);','//spawn(211624, 1);','// spawn(211625, 1);','spawn(211626, 1);','x spawn(flag ? 211627 : 211628, 1);','x spawn(211629, 999999);','x spawnWithDelay(211630, 1);','x respawn(211631, 1);','x spawnnpc(211632, 1);','x spawn(\n211633, 1);','  spawnForFiveMinutes(211621, instance);  spawn(211634, 1);','//  spawn(211635, 1);','x spawn (211636, 1);']
# The last whitespace case has an explicit native-source adaptation, not Java equality.
regex=[]
for s in inputs[:-1]:
 native=re.sub(r'\bsp(?=awn|\()', 'Sp',s)
 regex.append(dict(javaSource=s,csharpSource=native,expected=run('regex',base64.b64encode(s.encode()).decode())))
kill=[]
for query,rows in [(211621,'203620:211621'),(211621,'203620:211621,211622,211621'),(211621,'211621:211621'),(211621,'0:211621'),(211621,'211622:211621,211622'),(211621,'203620:211621;203621:211621'),(999900,'203620:211621'),(999900,'empty'),(211621,'203620:null')]:
 kill.append(dict(query=query,monsters=rows,expected=run('kill',str(query),rows),throwsNull=rows.endswith('null')))
houses=[dict(managers=ids,expected=run('house',','.join(map(str,ids)))) for ids in [[203620],[203620,203620,204001],[0,204001]]]
fixture=dict(sourceJavaCommit=SHA,sourceHashes={p+'.java':hashlib.sha256(b).hexdigest() for p,b in raw.items()},captureBoundary='Exact Java regex and method bodies, scalar HousingLand/Monster stubs and explicit superclass sentinel fallback; no full server boot claimed.',regex=json.loads(literal),regexCases=regex,killCases=kill,houseCases=houses)
output=ROOT/'parity-artifacts/golden/formulas/QuestSpawn.cabd580f3.json';output.write_text(json.dumps(fixture,indent=2)+'\n',encoding='utf-8');print(f'Captured {len(regex)} regex, {len(kill)} kill alternatives and {len(houses)} housing Java vectors')
