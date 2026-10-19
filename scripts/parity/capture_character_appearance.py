"""Capture a519ffc7b's exact appearance reader/writing blocks and full SM_PLAYER_INFO packet.

Full-packet capture needs the existing compiled Java golden harness and its cached Maven classpath.
The baseline must reproduce the historical fixture before the exact new packet classes are overlaid.
"""
import hashlib,json,re,struct,subprocess,xml.etree.ElementTree as ET
from pathlib import Path
from capture_npc_shop import block

ROOT=Path(__file__).resolve().parents[2]
JAVA=ROOT.parent/'aion-server'
SHA='a519ffc7bcc3df94e713a72ef72349106687b0c3'
PKG=ROOT/'artifacts/upstream/a519ffc7b-clamped-the-character-height-and-corrected-the-a/appearance-capture'
BASE='game-server/src/com/aionemu/gameserver/'
PATHS={'PlayerAppearance':'model/gameobjects/player/PlayerAppearance','AionServerPacket':'network/aion/AionServerPacket','AbstractCharacterEditPacket':'network/aion/clientpackets/AbstractCharacterEditPacket',
       'AbstractPlayerInfoPacket':'network/aion/serverpackets/AbstractPlayerInfoPacket','SM_PLAYER_INFO':'network/aion/serverpackets/SM_PLAYER_INFO'}

def sources(ref):
    texts={};hashes={}
    for name,path in PATHS.items():
        file=BASE+path+'.java';raw=subprocess.check_output(['git','-C',str(JAVA),'show',ref+':'+file]);texts[name]=raw.decode('utf-8');hashes[file]=hashlib.sha256(raw).hexdigest()
    return texts,hashes

def header_capture(texts,folder):
    folder.mkdir(parents=True,exist_ok=True)
    model=re.sub(r'^package .*;|^import .*;','',texts['PlayerAppearance'],flags=re.M)
    (folder/'PlayerAppearance.java').write_text(model,encoding='utf-8')
    reader=block(texts['AbstractCharacterEditPacket'],'protected void readAppearance()')
    constants=re.search(r'private static final float MIN_HEIGHT_SCALE[^;]*;',texts['AbstractCharacterEditPacket'])
    def fragment(name,start):
        s=texts[name];a=s.index(start);end='writeF(playerAppearance.getHeightScale());';b=s.index(end,a)+len(end);return s[a:b]
    account=fragment('AbstractPlayerInfoPacket','writeD(playerAppearance.getVoice());')
    world=fragment('SM_PLAYER_INFO','writeD(playerAppearance.getSkinRGB());')
    source='''import java.nio.*;import java.util.*;public class HeaderOracle {
    PlayerAppearance playerAppearance;ByteBuffer input;java.io.ByteArrayOutputStream bytes=new java.io.ByteArrayOutputStream();
    int readD(){return input.getInt();}int readUC(){return Byte.toUnsignedInt(input.get());}byte readC(){return input.get();}short readH(){return input.getShort();}float readF(){return input.getFloat();}
    void writeC(int x){bytes.write(x);}void writeD(int x){writeC(x);writeC(x>>>8);writeC(x>>>16);writeC(x>>>24);}void writeF(float x){writeD(Float.floatToRawIntBits(x));}
    '''+(constants.group() if constants else '')+'\n'+reader+'''
    byte[] account(){bytes.reset();'''+account+'''return bytes.toByteArray();}
    byte[] world(){bytes.reset();'''+world+'''return bytes.toByteArray();}
    public static void main(String[]args)throws Exception{
        HeaderOracle h=new HeaderOracle();
        if(args[0].equals("default"))h.playerAppearance=new PlayerAppearance();
        else{h.input=ByteBuffer.wrap(HexFormat.of().parseHex(args[0])).order(ByteOrder.LITTLE_ENDIAN);h.readAppearance();if(h.input.hasRemaining())throw new IllegalStateException("Unread input");}
        StringJoiner values=new StringJoiner("|");
        for(var f:PlayerAppearance.class.getDeclaredFields()){f.setAccessible(true);values.add(f.getName()+":"+(f.getType()==float.class?Float.floatToRawIntBits(f.getFloat(h.playerAppearance)):f.getInt(h.playerAppearance)));}
        System.out.println(values);System.out.println(HexFormat.of().formatHex(h.account()));System.out.println(HexFormat.of().formatHex(h.world()));
    }
}'''
    (folder/'HeaderOracle.java').write_text(source,encoding='utf-8')
    subprocess.run(['javac','-encoding','UTF-8','-d',str(folder),str(folder/'PlayerAppearance.java'),str(folder/'HeaderOracle.java')],check=True)
    def run(body):
        lines=subprocess.check_output(['java','-cp',str(folder),'HeaderOracle',body],text=True).splitlines();assert len(lines)==3
        fields={k:int(v) for k,v in (part.split(':') for part in lines[0].split('|'))};assert len(fields)==53
        return dict(fields=fields,accountHex=lines[1].upper(),worldHex=lines[2].upper())
    return run

def full_packet_capture(texts,hashes,parent_default,current_default):
    cp=None
    for report in (JAVA/'game-server/target/surefire-reports').glob('TEST-*.xml'):
        props={p.attrib['name']:p.attrib.get('value','') for p in ET.parse(report).findall('./properties/property')}
        for key in ('surefire.test.class.path','java.class.path'):
            value=props.get(key,'')
            if 'game-server' in value and 'test-classes' in value and all(Path(p).exists() for p in value.split(';') if p):cp=value;break
        if cp:break
    if cp is None:raise RuntimeError('Build the existing Java golden generator harness first; its cached full classpath is missing.')
    folder=PKG/'full';folder.mkdir(parents=True,exist_ok=True)
    (folder/'FullPlayerOracle.java').write_text('public class FullPlayerOracle {public static void main(String[]args)throws Exception{new com.aionemu.gameserver.network.aion.serverpackets.GoldenPlayerInfoFixtureGeneratorTest().generateGoldenPlayerInfoFixtures();System.exit(0);}}',encoding='utf-8')
    subprocess.run(['javac','-cp',cp,'-d',str(folder),str(folder/'FullPlayerOracle.java')],check=True)
    def run(name,classpath):
        work=folder/name;(work/'parity-artifacts').mkdir(parents=True,exist_ok=True)
        with (work/'console.log').open('w',encoding='utf-8') as output:
            subprocess.run(['java','-cp',classpath,'FullPlayerOracle'],cwd=work,stdout=output,stderr=subprocess.STDOUT,check=True,timeout=60)
        return json.loads((work/'parity-artifacts/golden/packets/SM_PLAYER_INFO.json').read_text(encoding='utf-8'))
    baseline=run('baseline',str(folder)+';'+cp)
    historical=json.loads((ROOT/'parity-artifacts/golden/packets/SM_PLAYER_INFO.json').read_text(encoding='utf-8-sig'))
    assert baseline==historical,'Historical full Java harness no longer reproduces its baseline fixture'
    src=folder/'src';src.mkdir(exist_ok=True);overlay=folder/'overlay';overlay.mkdir(exist_ok=True);paths=[]
    for name in ('PlayerAppearance','AbstractPlayerInfoPacket','SM_PLAYER_INFO'):
        text=texts[name]
        if name=='PlayerAppearance':
            # Only old harness/model constructors call these; production packet sources are unmodified.
            bridge='''public float getHeight(){return getHeightScale();}public void setHeight(float v){setHeightScale(v);}
            public float getBoundHeight(){return getHeightScale()*1.75f;}
            public int getJawHeigh(){return getChinHeight();}public void setJawHeigh(int v){setChinHeight(v);}
            public int getChinJut(){return getCheekBones();}public void setChinJut(int v){setCheekBones(v);}
            '''
            end=text.rfind('}');text=text[:end]+bridge+text[end:]
        p=src/(name+'.java');p.write_text(text,encoding='utf-8');paths.append(str(p))
    subprocess.run(['javac','-encoding','UTF-8','-cp',cp,'-d',str(overlay),*paths],check=True)
    updated=run('updated',str(overlay)+';'+str(folder)+';'+cp)
    old_block=bytes.fromhex(parent_default['worldHex']);new_block=bytes.fromhex(current_default['worldHex'])
    fragment_changes=[i for i,(a,b) in enumerate(zip(old_block,new_block)) if a!=b];assert len(fragment_changes)==1
    offsets={}
    for a,b in zip(baseline['cases'],updated['cases']):
        assert a['name']==b['name'] and a['inputs']==b['inputs']
        x=bytes.fromhex(a['payloadHex']);y=bytes.fromhex(b['payloadHex']);assert len(x)==len(y)
        changed=[i for i,(u,v) in enumerate(zip(x,y)) if u!=v];assert len(changed)==1 and x[changed[0]]==5 and y[changed[0]]==6
        start=changed[0]-fragment_changes[0];assert x[start:start+len(old_block)]==old_block and y[start:start+len(new_block)]==new_block
        offsets[a['name']]=start
    updated['sourceJavaCommit']=SHA;updated['sourceHashes']=hashes
    updated['captureBoundary']='Exact new full SM_PLAYER_INFO/AbstractPlayerInfoPacket with historical harness/runtime. New PlayerAppearance has legacy accessor bridges solely for old fixture constructors. Baseline reproduction proved first.'
    (ROOT/'parity-artifacts/golden/packets/SM_PLAYER_INFO.a519ffc7b.json').write_text(json.dumps(updated,indent=2)+'\n',encoding='utf-8')
    return offsets

def main():
    PKG.mkdir(parents=True,exist_ok=True);texts,hashes=sources(SHA);parent,parent_hashes=sources(SHA+'^')
    current=header_capture(texts,PKG/'current');previous=header_capture(parent,PKG/'parent')
    current_default=current('default');parent_default=previous('default')
    offsets=full_packet_capture(texts,hashes,parent_default,current_default)
    bits=[0,0x80000000,0x3f266665,0x3f266666,0x3f266667,0x3f800000,0x3f800001,0x3f999999,0x3f99999a,0x3f99999b,0x3fa00000,0x7f7fffff,0xff7fffff,0x7f800000,0xff800000,0x7fc00000,0x7fa00001,0xffc12345]
    features=bytes((i*7+3)%256 for i in range(52));prefix=struct.pack('<iiiii',-2147483647,0x12345678,-19088744,0x0077aaee,2147483647)+features
    cases=[]
    for value in bits:
        body=prefix+struct.pack('<I',value);cases.append(dict(name=f'height-{value:08X}',bodyHex=body.hex().upper(),**current(body.hex())))
    max_name=int(re.search(r'CHARNAME_MAX_LENGTH\s*=\s*(\d+)',texts['AbstractPlayerInfoPacket']).group(1))
    fixture=dict(source='Java',sourceJavaCommit=SHA,sourceHashes=hashes,parentSourceHashes=parent_hashes,
                 captureBoundary='Exact readAppearance, exact PlayerAppearance, exact account/world appearance-writing sequences; little-endian primitive byte sinks. Separate exact full-packet capture anchors world offsets.',
                 # AionServerPacket.writeS(text,fixedLength) writes fixedLength chars plus a UTF-16 terminator.
                 accountAppearanceOffset=4+(max_name+1)*2+12,worldAppearanceOffsets=offsets,cases=cases)
    (ROOT/'parity-artifacts/golden/formulas/CharacterAppearance.a519ffc7b.json').write_text(json.dumps(fixture,indent=2)+'\n',encoding='utf-8')
    print('Captured',len(cases),'complete appearance vectors and two exact full player-info packets; historical fixture unchanged.')

if __name__=='__main__':main()
