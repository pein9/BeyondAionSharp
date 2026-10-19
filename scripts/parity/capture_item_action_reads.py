"""Compile exact 65eebace9 appearance/manastone readers and ID enums against a ByteBuffer reader stub."""
import base64
import hashlib
import json
from pathlib import Path
import struct
import subprocess

ROOT=Path(__file__).resolve().parents[2]
SHA='65eebace942c3e7bfb539139a9f6976fe617c9eb'
PACKAGE=ROOT/'artifacts/upstream/65eebace9-fixed-item-exploits-and-aligned-enchanting-with/java-read-capture'

def block(text,marker):
    start=text.index(marker);i=text.index('{',start)+1;depth=1
    while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
    return text[start:i]

def main():
    src=PACKAGE/'src';src.mkdir(parents=True,exist_ok=True);hashes={}
    for name,fields,enum in [
        ('Appearance','private Service service;private int itemObjId;private String newName;','Service'),
        ('Manastone','private Action action;private int targetFusedSlot,targetItemUniqueId,stoneUniqueId,supplementUniqueId,slotNum,npcObjId;','Action')]:
        path='game-server/src/com/aionemu/gameserver/network/aion/clientpackets/CM_'+name.upper()+'.java'
        raw=subprocess.check_output(['git','-C',str(ROOT.parent/'aion-server'),'show',SHA+':'+path]);hashes[path]=hashlib.sha256(raw).hexdigest();text=raw.decode('utf-8')
        result=('return service+","+itemObjId+","+(newName==null?"-":java.util.Base64.getEncoder().encodeToString(newName.getBytes(java.nio.charset.StandardCharsets.UTF_8)));' if name=='Appearance' else 'return action+","+targetFusedSlot+","+targetItemUniqueId+","+stoneUniqueId+","+supplementUniqueId+","+slotNum+","+npcObjId;')
        (src/('Parser'+name+'.java')).write_text('public class Parser'+name+' extends ParserBase {'+fields+block(text,'protected void readImpl()')+block(text,'private enum '+enum)+'public String result(){'+result+'}}',encoding='utf-8')
    (src/'ParserBase.java').write_text('''abstract class ParserBase {
 java.nio.ByteBuffer bytes; int readUC(){return bytes.get()&255;}int readD(){return bytes.getInt();}
 String readS(){StringBuilder s=new StringBuilder();char c;while((c=bytes.getChar())!=0)s.append(c);return s.toString();}
 protected abstract void readImpl();abstract String result();
}''',encoding='utf-8')
    (src/'ReadOracle.java').write_text('''public class ReadOracle {
 public static void main(String[]args)throws Exception {
  try(var r=new java.io.BufferedReader(new java.io.InputStreamReader(System.in))){String line;
   while((line=r.readLine())!=null){String[]v=line.split(" ");ParserBase parser=v[0].equals("A")?new ParserAppearance():new ParserManastone();parser.bytes=java.nio.ByteBuffer.wrap(v.length<2?new byte[0]:java.util.HexFormat.of().parseHex(v[1])).order(java.nio.ByteOrder.LITTLE_ENDIAN);
    try{parser.readImpl();System.out.println("1,"+parser.bytes.remaining()+","+parser.result());}
    catch(java.nio.BufferUnderflowException e){System.out.println("0,"+parser.bytes.remaining());}
   }
  }
 }
}''',encoding='utf-8')
    cases=[]
    def case(kind,name,body):cases.append(dict(kind=kind,name=name,bodyHex=body.hex().upper()))
    for service,name in [(0,'Name'),(1,'Å🚀'),(2,None),(3,None),(256,None),(65536,None),(65538,None),(-1,None),(2147483647,None)]:
        case('A',f'appearance-{service}',struct.pack('<ii',service,0x11223344)+(b'' if name is None else name.encode('utf-16le')+b'\0\0'))
    for size in [0,3,4,7]:case('A',f'appearance-short-{size}',struct.pack('<ii',2,1)[:size])
    case('A','appearance-rename-missing-string',struct.pack('<ii',0,1))
    for action in [0,1,2,3,4,8,255]:case('M',f'manastone-{action}',struct.pack('<bbiii',action if action<128 else action-256,1,0x11223344,0x1020304,0x55667788))
    case('M','manastone-negative-slot',struct.pack('<bbiii',3,2,123,-1,456))
    for size in [0,1,2,5,6,9,13]:case('M',f'manastone-short-{size}',struct.pack('<bbiii',3,1,1,2,3)[:size])
    classes=PACKAGE/'classes';classes.mkdir(exist_ok=True)
    subprocess.run(['javac','-encoding','UTF-8','-d',str(classes),*[str(p) for p in src.glob('*.java')]],check=True)
    result=subprocess.run(['java','-cp',str(classes),'ReadOracle'],input='\n'.join(c['kind']+' '+c['bodyHex'] for c in cases)+'\n',capture_output=True,text=True,check=True)
    lines=result.stdout.splitlines();assert len(cases)==len(lines)
    for c,line in zip(cases,lines):
        parts=line.split(',');c['ok']=parts[0]=='1';c['remaining']=int(parts[1])
        if c['ok']:
            values=parts[2:]
            if c['kind']=='A':c['fields']=dict(service=None if values[0]=='null' else values[0],itemObjId=int(values[1]),newName=None if values[2]=='-' else base64.b64decode(values[2]).decode('utf-8'))
            else:c['fields']=dict(zip(['action','targetFusedSlot','targetItemUniqueId','stoneUniqueId','supplementUniqueId','slotNum','npcObjId'],[None if values[0]=='null' else values[0],*[int(v) for v in values[1:]]]))
    fixture=dict(source='Java',sourceJavaCommit=SHA,sourceHashes=hashes,captureBoundary='Exact readImpl methods and private enums; ByteBuffer LE primitives/UTF16-null-string stub. Native error handling compared only by success/failure, not failed-read cursor.',cases=cases)
    (ROOT/'parity-artifacts/golden/packets/ItemActionReads.65eebace9.json').write_text(json.dumps(fixture,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
    print('Captured',len(cases),'exact Java item-service reads from',SHA)

if __name__=='__main__':main()
