"""Capture ba383f739's exact Java emblem model, upload reader and rank admission guard.
The guard is the original leading if block; remaining service/restriction/DAO behavior is outside this oracle.
"""
import hashlib
import json
import os
import re
import struct
import subprocess
from pathlib import Path
from capture_npc_shop import block

ROOT = Path(__file__).resolve().parents[2]
JAVA = Path(os.environ.get('BEYOND_AION_JAVA_ROOT', ROOT.parent / 'aion-server'))
SHA = 'ba383f739dce32b60986b352b67b5e837c6479a1'
PKG = ROOT / 'artifacts/upstream/ba383f739-fixed-exploits-of-legion-emblems-and-brigade-ran/oracle'
PKG.mkdir(parents=True, exist_ok=True)
paths = {'LegionEmblem': 'model/team/legion/LegionEmblem', 'LegionEmblemType': 'model/team/legion/LegionEmblemType', 'LegionRank': 'model/team/legion/LegionRank', 'Reader': 'network/aion/clientpackets/CM_LEGION_UPLOAD_EMBLEM', 'Service': 'services/LegionService'}
raw = {n: subprocess.check_output(['git', '-C', str(JAVA), 'show', SHA + ':game-server/src/com/aionemu/gameserver/' + p + '.java']) for n, p in paths.items()}
texts = {n: v.decode('utf-8') for n, v in raw.items()}
for name in ['LegionEmblem', 'LegionEmblemType', 'LegionRank']:
    (PKG / (name + '.java')).write_text(re.sub(r'^package .*;|^import .*;', '', texts[name], flags=re.M), encoding='utf-8')
rank_guard = block(texts['Service'], 'public void appointRank(').split('\n\t\tLegionMember legionMember')[0]
rank_guard = rank_guard.replace('public void appointRank(', 'public void checkRank(') + '\n allowed=true; }'
source = '''import java.util.*;import java.nio.*;
interface Persistable {enum PersistentState {NEW,UPDATE_REQUIRED,UPDATED,DELETED}void setPersistentState(PersistentState s);PersistentState getPersistentState();}
class Player {}
class AuditLogger {static void log(Player p,String s){}}
public class EmblemOracle {
 boolean allowed;'''+rank_guard+'''
 static class Reader {ByteBuffer buf;byte[] data;int readD(){return buf.getInt();}byte[] readB(int n){byte[] b=new byte[n];buf.get(b);return b;}
 '''+block(texts['Reader'], 'protected void readImpl()')+'''}
 public static void main(String[]args){
  if(args[0].equals("rank")){var o=new EmblemOracle();o.checkRank(new Player(),"Target",Integer.parseInt(args[1]));System.out.println(o.allowed);return;}
  if(args[0].equals("read")){var r=new Reader();r.buf=ByteBuffer.wrap(HexFormat.of().parseHex(args[1])).order(ByteOrder.LITTLE_ENDIAN);r.readImpl();System.out.println(HexFormat.of().withUpperCase().formatHex(r.data)+"|"+r.buf.remaining());return;}
  var e=new LegionEmblem();
  for(int i=1;i<args.length;i++){String a=args[i];Boolean accepted=null;
   if(a.startsWith("size:"))e.setUploadSize(Integer.parseInt(a.substring(5)));
   else if(a.startsWith("data:"))accepted=e.addUploadData(HexFormat.of().parseHex(a.substring(5)));
   else if(a.equals("reset"))e.resetUploadSettings();else if(a.equals("on"))e.setUploading(true);
   System.out.println(accepted+"|"+e.isUploadComplete()+"|"+e.isUploading()+"|"+(e.getUploadData()==null?"null":HexFormat.of().withUpperCase().formatHex(e.getUploadData())));
  }
 }
}'''
(PKG / 'EmblemOracle.java').write_text(source, encoding='utf-8')
subprocess.run(['javac', '-encoding', 'UTF-8', '-d', str(PKG), *[str(p) for p in PKG.glob('*.java')]], check=True)

def run(*args):
    return subprocess.check_output(['java', '-cp', str(PKG), 'EmblemOracle', *map(str, args)], text=True).strip()

sequences = [
    ['data:01', 'reset'], ['size:0', 'data:', 'data:01'],
    ['size:4', 'on', 'data:0102', 'data:03', 'data:04', 'data:05', 'reset'],
    ['size:3', 'data:01020304', 'data:0102', 'data:03'],
    ['size:4', 'data:0102', 'size:2', 'data:0304', 'reset', 'size:1', 'data:FF'],
    ['size:1', 'data:', 'data:01', 'data:', 'reset', 'data:'],
]
traces = []
for actions in sequences:
    rows = run('trace', *actions).splitlines()
    states = []
    for action, row in zip(actions, rows, strict=True):
        accepted, complete, uploading, data = row.split('|')
        states.append(dict(action=action, accepted=None if accepted == 'null' else accepted == 'true', complete=complete == 'true', uploading=uploading == 'true', dataHex=None if data == 'null' else data))
    traces.append(states)
reads = []
for data in [b'', b'\x00', bytes([0, 127, 128, 255]), bytes(range(256)), bytes(range(256)) * 31]:
    body = struct.pack('<i', len(data)) + data
    expected, remaining = run('read', body.hex()).split('|')
    assert remaining == '0'
    reads.append(dict(bodyHex=body.hex().upper(), dataHex=expected))
fixture = dict(sourceJavaCommit=SHA, sourceHashes={paths[n] + '.java': hashlib.sha256(v).hexdigest() for n, v in raw.items()}, captureBoundary=__doc__.strip(), traces=traces, reads=reads, ranks=[dict(id=n, allowed=run('rank', n) == 'true') for n in [-2147483648, -1, 0, 1, 2, 3, 4, 5, 255, 2147483647]])
(ROOT / 'parity-artifacts/golden/formulas/LegionEmblem.ba383f739.json').write_text(json.dumps(fixture, indent=2) + '\n', encoding='utf-8')
print('Captured', sum(map(len, traces)), 'model transitions,', len(reads), 'readers and', len(fixture['ranks']), 'rank guards')
