"""Capture bce89b2fc login writers and crypto from the exact approved Java source.

Only package/import declarations are removed. Collaborator stubs supply fixed
connection/account/server data; no Netty transport or authentication is simulated.
"""
import hashlib
import json
import os
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SHA = 'bce89b2fcf740d3b2bcd1b75dc62e7c62382aeca'
BASE = 'login-server/src/com/aionemu/loginserver/'
OUT = ROOT / 'artifacts/upstream/bce89b2fc-moved-the-login-server-to-netty-and-matched-the/java-capture'


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    hashes = {}
    java = Path(os.environ.get('BEYOND_AION_JAVA_ROOT', ROOT.parent / 'aion-server'))
    writers = ['SM_INIT', 'SM_AUTH_GG', 'SM_LOGIN_FAIL', 'SM_PLAY_FAIL',
               'SM_ACCOUNT_KICK', 'SM_ACCOUNT_BANNED', 'SM_ACCOUNT_BANNED_2',
               'SM_LOGIN_OK', 'SM_PLAY_OK', 'SM_SERVER_LIST']
    sources = [(name, 'network/aion/serverpackets/' + name) for name in writers]
    sources += [('AionAuthResponse', 'network/aion/AionAuthResponse'),
                ('CryptEngine', 'network/ncrypt/CryptEngine'),
                ('BlowfishCipher', 'network/ncrypt/BlowfishCipher')]
    for name, relative in sources:
        path = BASE + relative + '.java'
        raw = subprocess.check_output(['git', '-C', str(java), 'show', SHA + ':' + path])
        hashes[path] = hashlib.sha256(raw).hexdigest()
        source = re.sub(r'^package .*;|^import com\..*;', '', raw.decode('utf-8'), flags=re.M)
        (OUT / (name + '.java')).write_text(source, encoding='utf-8')
    (OUT / 'LoginOracle.java').write_text(r'''
import java.nio.*;
import java.util.*;
import java.io.*;
public class LoginOracle {
    static String hex(byte[] b) { return HexFormat.of().withUpperCase().formatHex(b); }
    static byte[] bytes(int n, int mul, int add) {
        byte[] b = new byte[n]; for(int i=0;i<n;i++)b[i]=(byte)(i*mul+add); return b;
    }
    static byte[] key = {1,3,5,7,9,11,13,15,2,4,6,8,10,12,14,16};
    static void packet(String name, AionServerPacket p) {
        System.out.println("packet|"+name+"|"+hex(p.serialize()));
    }
    public static void main(String[] args) {
        SessionKey session = new SessionKey();
        SM_INIT init = new SM_INIT(new LoginConnection(), new javax.crypto.spec.SecretKeySpec(key,"Blowfish"));
        packet("init",init); packet("gameGuard",new SM_AUTH_GG());
        packet("loginFail",new SM_LOGIN_FAIL(AionAuthResponse.STR_L2AUTH_S_INCORRECT_PWD));
        packet("playFail",new SM_PLAY_FAIL(AionAuthResponse.STR_L2AUTH_S_SERVER_DOWN));
        packet("kick",new SM_ACCOUNT_KICK(AionAuthResponse.STR_L2AUTH_S_BLOCKED_IP));
        packet("banned",new SM_ACCOUNT_BANNED()); packet("bannedReasonFlags",new SM_ACCOUNT_BANNED(26,0x80000002));
        packet("banned2",new SM_ACCOUNT_BANNED_2()); packet("loginOk",new SM_LOGIN_OK(session));
        packet("playOk",new SM_PLAY_OK(session,(byte)7)); packet("serverList",new SM_SERVER_LIST());
        byte[] plain=init.serialize(); ByteBuffer encrypted=ByteBuffer.allocate(256);
        for(int i=0;i<plain.length;i++)encrypted.put(i,plain[i]);
        CryptEngine e=new CryptEngine();e.updateKey(key);int len=e.encrypt(encrypted,0,plain.length,0x01020304);
        byte[] frame=new byte[len+2];frame[0]=(byte)frame.length;frame[1]=(byte)(frame.length>>8);
        for(int i=0;i<len;i++)frame[i+2]=encrypted.get(i);
        System.out.println("packet|initFrame|"+hex(frame));
        for(int offset:new int[]{0,2,7,31}) for(int size:new int[]{0,1,3,4,7,8,13,40,65,128,170}) {
            CryptEngine engine=new CryptEngine();engine.updateKey(key);
            for(int seq=0;seq<2;seq++) {
                ByteBuffer b=ByteBuffer.allocate(256);Arrays.fill(b.array(),(byte)0x5A);
                byte[] p=bytes(size,7,3);for(int i=0;i<size;i++)b.put(offset+i,p[i]);
                int count=engine.encrypt(b,offset,size,0x1234ABCD);
                System.out.println("crypto|"+offset+"|"+size+"|"+seq+"|"+hex(Arrays.copyOfRange(b.array(),offset,offset+count)));
                for(int i=0;i<offset;i++)if(b.get(i)!=(byte)0x5A)throw new AssertionError("prefix");
                for(int i=offset+count;i<b.capacity();i++)if(b.get(i)!=(byte)0x5A)throw new AssertionError("suffix");
            }
        }
    }
}
class Rnd { static int nextInt(){return 0x1234ABCD;} }
abstract class AionServerPacket {
    ByteBuffer b=ByteBuffer.allocate(4096).order(ByteOrder.LITTLE_ENDIAN);int opcode;
    AionServerPacket(int op){opcode=op;}
    protected abstract void writeImpl(LoginConnection con);
    void writeC(int v){b.put((byte)v);}void writeH(int v){b.putShort((short)v);}
    void writeD(int v){b.putInt(v);}void writeB(byte[] v){b.put(v);}
    byte[] serialize(){b.clear();writeC(opcode);writeImpl(new LoginConnection());return Arrays.copyOf(b.array(),b.position());}
}
class LoginConnection {
    byte[] getEncryptedModulus(){return LoginOracle.bytes(128,1,0);}
    int getSessionId(){return 0x11223344;}Account getAccount(){return new Account();}
}
class Account { int getId(){return 1001;}int getLastServer(){return 1;} }
class SessionKey { int accountId=1001,loginOk=0x11223344,playOk1=0x01020304,playOk2=0x55667788; }
class GameServerTable { static Collection<GameServerInfo> getGameServers(){return List.of(new GameServerInfo());} }
class AccountController { static Map<Byte,Integer> getGSCharacterCountsFor(int id){return Map.of((byte)1,2);} }
class GameServerInfo {
    byte getId(){return 1;}byte[] getIp(){return new byte[]{127,0,0,1};}int getPort(){return 7777;}
    int getCurrentPlayers(){return 0;}int getMaxPlayers(){return 100;}boolean isOnline(){return true;}
}
''', encoding='utf-8')
    subprocess.run(['javac', '-encoding', 'UTF-8', '-d', str(OUT),
                    *map(str, OUT.glob('*.java'))], check=True)
    rows = subprocess.check_output(['java', '-cp', str(OUT), 'LoginOracle'], text=True).splitlines()
    packets, crypto = {}, []
    for row in rows:
        values = row.split('|')
        if values[0] == 'packet':
            packets[values[1]] = values[2]
        else:
            crypto.append(dict(offset=int(values[1]), size=int(values[2]), sequence=int(values[3]), cipherHex=values[4]))
    fixture = dict(source='Java', sourceJavaCommit=SHA, sourceHashes=hashes,
                   captureBoundary='Exact writer, CryptEngine and BlowfishCipher classes; only package/import declarations removed. Fixed account, server, session and RNG collaborators. No authentication or transport capture.',
                   packets=packets, cryptoCases=crypto)
    target = ROOT / 'parity-artifacts/golden/formulas/LoginPipeline.bce89b2fc.json'
    target.write_text(json.dumps(fixture, indent=2) + '\n', encoding='utf-8')
    print(f'Captured {len(packets)} packet vectors and {len(crypto)} crypto cases from {SHA}')


if __name__ == '__main__':
    main()
