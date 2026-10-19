"""Exact 46e8d00b9 Java compression/validation, enum and client-reader oracle.
Readers use an explicit little-endian byte source and no-op logger; no world, DAO or handler execution is claimed.
"""
import hashlib,json,os,re,struct,subprocess,zlib
from pathlib import Path
from capture_npc_shop import block
ROOT=Path(__file__).resolve().parents[2]
JAVA=Path(os.environ.get('BEYOND_AION_JAVA_ROOT',ROOT.parent/'aion-server'))
SHA='46e8d00b923d83820e32e0bbe67835de32b6c81d'
PKG=ROOT/'artifacts/upstream/46e8d00b9-fixed-exploits-of-housing/oracle';PKG.mkdir(parents=True,exist_ok=True)
paths={'CompressUtil':'utils/xml/CompressUtil','HousingCommand':'model/house/HousingCommand','HousingContainer':'model/house/HousingContainer','Scripts':'model/gameobjects/player/PlayerScripts','ScriptReader':'network/aion/clientpackets/CM_HOUSE_SCRIPT','EditReader':'network/aion/clientpackets/CM_HOUSE_EDIT'}
raw={n:subprocess.check_output(['git','-C',str(JAVA),'show',SHA+':game-server/src/com/aionemu/gameserver/'+p+'.java']) for n,p in paths.items()};s={n:v.decode('utf-8') for n,v in raw.items()}
for name in ['CompressUtil','HousingCommand','HousingContainer']:
    (PKG/(name+'.java')).write_text(re.sub(r'^package .*;','',s[name],flags=re.M),encoding='utf-8')
code='''import java.io.*;import java.nio.*;import java.util.*;
class LoggerFactory {static LoggerFactory getLogger(Object c){return new LoggerFactory();}void warn(Object...v){}}
public class HousingOracle {
 static class Source {ByteBuffer b;int readD(){return b.getInt();}int readUC(){return Byte.toUnsignedInt(b.get());}int readUH(){return Short.toUnsignedInt(b.getShort());}float readF(){return b.getFloat();}byte[] readB(int n){byte[] v=new byte[n];b.get(v);return v;}Source getConnection(){return this;}Object getActivePlayer(){return null;}}
 static class CM_HOUSE_EDIT extends Source {HousingCommand action;int itemObjectId,rotation,buildingId;float x,y,z;
 '''+block(s['EditReader'],'protected void readImpl()')+'''}
 static class ScriptReader extends Source {int address,scriptId,totalSize,compressedSize,uncompressedSize;byte[] scriptContent;
 '''+re.search(r'private static final int MAX_SCRIPT_BLOCK_SIZE = [^;]+;',s['ScriptReader']).group()+block(s['ScriptReader'],'protected void readImpl()')+'''}
 '''+block(s['Scripts'],'private boolean isValid(')+'''
 static String hex(byte[]v){return v==null?"null":HexFormat.of().withUpperCase().formatHex(v);}
 public static void main(String[]args)throws Exception {
  var self=new HousingOracle();var reader=new BufferedReader(new InputStreamReader(System.in));String line;
  while((line=reader.readLine())!=null){var a=line.split("[|]",-1);byte[] bytes=a[2].equals("null")?null:HexFormat.of().parseHex(a[2]);
   if(a[0].equals("inflate")){int size=Integer.parseInt(a[1]);System.out.println((bytes==null?"null":CompressUtil.inflatesTo(bytes,size))+"|"+self.isValid(bytes,size));}
   else if(a[0].equals("edit")){var r=new CM_HOUSE_EDIT();r.b=ByteBuffer.wrap(bytes).order(ByteOrder.LITTLE_ENDIAN);r.readImpl();System.out.println(r.action+"|"+r.itemObjectId+"|"+Float.floatToRawIntBits(r.x)+"|"+Float.floatToRawIntBits(r.y)+"|"+Float.floatToRawIntBits(r.z)+"|"+r.rotation+"|"+r.buildingId+"|"+r.b.remaining());}
   else {var r=new ScriptReader();r.b=ByteBuffer.wrap(bytes).order(ByteOrder.LITTLE_ENDIAN);r.readImpl();System.out.println(r.address+"|"+r.scriptId+"|"+r.totalSize+"|"+r.compressedSize+"|"+r.uncompressedSize+"|"+hex(r.scriptContent)+"|"+r.b.remaining());}
  }
 }
}'''
(PKG/'HousingOracle.java').write_text(code,encoding='utf-8')
subprocess.run(['javac','-encoding','UTF-8','-d',str(PKG),*[str(p) for p in PKG.glob('*.java')]],check=True)
requests=[];inflation=[];edits=[];scripts=[]
def ask(kind,size,data):requests.append(kind+'|'+str(size)+'|'+('null' if data is None else data.hex()))
def inflate(name,data,size):inflation.append(dict(name=name,hex=None if data is None else data.hex().upper(),size=size));ask('inflate',size,data)
for data in [None,b'']:
    for size in [-1,0,1,2147483647]:inflate('empty-'+str(data)+'-'+str(size),data,size)
for n in [0,1,15,1023,1024,1025,4096,65536]:
    plain=bytes((i*17)%256 for i in range(n));compressed=zlib.compress(plain)
    for size in sorted(set([-1,0,n-1,n,n+1,2147483647])):inflate('length-'+str(n)+'-size-'+str(size),compressed,size)
    for cut in sorted(set([0,1,2,len(compressed)//2,*range(max(0,len(compressed)-6),len(compressed))])):inflate('truncate-'+str(n)+'-'+str(cut),compressed[:cut],n)
    for suffix in [b'\x00',b'junk',compressed]:inflate('trailing-'+str(n)+'-'+suffix[:4].hex(),compressed+suffix,n)
    corrupt=bytearray(compressed);corrupt[-1]^=1;inflate('checksum-'+str(n),corrupt,n)
dictionary=zlib.compressobj(zdict=b'dictionary');encoded=dictionary.compress(b'dictionarydictionary')+dictionary.flush();inflate('dictionary',encoded,20)
for junk in [b'\x00'*16,b'\x78\x9c',b'\x78\x9c\x07',bytes(range(32))]:inflate('invalid-'+junk[:4].hex(),junk,20)
for action in [0,1,2,3,4,5,6,7,8,14,15,16,17,128,255]:
    data=bytes([action])
    if action in [3,4,7]:data+=struct.pack('<i',-1234567)
    elif action in [5,6]:data+=struct.pack('<i3fH',123456,1.25,-2.5,1234.5,65535)
    elif action==16:data+=struct.pack('<i',7103)
    edits.append(dict(bodyHex=data.hex().upper()));ask('edit',0,data)
for total,compressed,uncompressed in [(0,0,0),(1,0,0),(7,0,0),(8,0,0),(8,-1,0),(9,1,4),(9,2,4),(3072,3064,100),(3072,3065,100),(3073,0,0),(65535,0,0),(12,0,-1),(12,4,0)]:
    data=struct.pack('<iBH',123456,255,total)
    if 0<total<=3072:data+=struct.pack('<ii',compressed,uncompressed)+bytes([0x41])*max(0,min(compressed,3065))
    scripts.append(dict(bodyHex=data.hex().upper()));ask('script',0,data)
rows=subprocess.check_output(['java','-cp',str(PKG),'HousingOracle'],input='\n'.join(requests)+'\n',text=True).splitlines();assert len(rows)==len(requests)
at=0
for c in inflation:
    a,b=rows[at].split('|');at+=1;c.update(inflates=None if a=='null' else a=='true',valid=b=='true')
for c in edits:
    a,*values=rows[at].split('|');at+=1;c.update(action=None if a=='null' else a,values=list(map(int,values)))
for c in scripts:
    values=rows[at].split('|');at+=1;c.update(values=list(map(int,values[:5])),content=None if values[5]=='null' else values[5],remaining=int(values[6]))
fixture=dict(sourceJavaCommit=SHA,sourceHashes={paths[n]+'.java':hashlib.sha256(v).hexdigest() for n,v in raw.items()},captureBoundary=__doc__.strip(),inflation=inflation,edits=edits,scripts=scripts)
(ROOT/'parity-artifacts/golden/formulas/Housing.46e8d00b9.json').write_text(json.dumps(fixture,indent=2)+'\n',encoding='utf-8')
print('Captured',len(inflation),'inflation+validation,',len(edits),'edit and',len(scripts),'script reader cases')
