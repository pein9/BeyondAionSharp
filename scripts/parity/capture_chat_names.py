"""Capture the exact four chat tag methods at 7181a3864 using the JDK."""
import base64
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
SHA = '7181a3864a404a4d78342bf6e84365e3af731c91'
SOURCE = 'game-server/src/com/aionemu/gameserver/utils/ChatUtil.java'

def main():
    raw = subprocess.check_output(['git', '-C', str(ROOT.parent/'aion-server'), 'show', SHA+':'+SOURCE])
    text = raw.decode('utf-8')
    methods = []
    for name in ('item', 'itemName', 'recipe', 'recipeName'):
        matches = re.findall(r'public static String '+name+r'\([^)]*\)\s*\{[^}]*\}', text)
        assert len(matches) == 1
        methods.append(matches[0])
    pkg = ROOT/'artifacts/upstream/7181a3864-chatutil-name-documentation/java-capture'
    pkg.mkdir(parents=True, exist_ok=True)
    source = 'import java.util.*;import java.nio.charset.StandardCharsets;public class ChatNamesOracle {\n'+'\n'.join(methods)+'''
    public static void main(String[]args) {
        Locale.setDefault(Locale.US);
        int id=Integer.parseInt(args[0]);
        String name=args[1].equals("-")?null:new String(Base64.getDecoder().decode(args[1]),StandardCharsets.UTF_8);
        for(String s:new String[]{item(id),itemName(id,name),recipe(id),recipeName(id,name)})
            System.out.println(Base64.getEncoder().encodeToString(s.getBytes(StandardCharsets.UTF_8)));
    }
}'''
    (pkg/'ChatNamesOracle.java').write_text(source, encoding='utf-8')
    subprocess.run(['javac', '-encoding', 'UTF-8', '-d', str(pkg), str(pkg/'ChatNamesOracle.java')], check=True)
    cases = []
    for id, name in [(100200836,'Ownername'), (155006741,'Ownername'), (0,''), (-2147483648,None),
                     (2147483647,'Raël玩家🚀'), (1,'a;b]c'), (9001,None), (9001,'')]:
        arg = '-' if name is None else base64.b64encode(name.encode('utf-8')).decode()
        result = subprocess.run(['java', '-cp', str(pkg), 'ChatNamesOracle', str(id), arg], capture_output=True, text=True, check=True)
        tags = [base64.b64decode(s).decode('utf-8') for s in result.stdout.splitlines()]
        assert len(tags) == 4
        cases.append(dict(id=id,playerName=name,**dict(zip(('item','itemName','recipe','recipeName'),tags))))
    fixture = dict(source='Java',sourceJavaCommit=SHA,sourceSha256=hashlib.sha256(raw).hexdigest(),
                   captureBoundary='Four exact static methods, Locale.US; UTF-8/base64 transport only.',cases=cases)
    (ROOT/'parity-artifacts/golden/formulas/ChatNames.7181a3864.json').write_text(json.dumps(fixture,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
    print('Captured',len(cases),'sets of four chat tags from',SHA)

if __name__ == '__main__':
    main()
