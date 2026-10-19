"""Compile exact f7230c5fa packet/model sources and capture payloads with deterministic owner/time stubs.

Run from any directory. The Java checkout is read-only; compilation stays in ignored task artifacts.
Expirable time is overridden by frozen model subclasses; production packet/model bodies are unchanged.
"""
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]
SHA = "f7230c5fa470511f793c1699cc6beeb650e98f94"
PACKAGE = ROOT / "artifacts/upstream/f7230c5fa-motion-emotion-learning-emotion-expiry-fixes-207/java-capture"


def main():
    assert PACKAGE.resolve().is_relative_to((ROOT / "artifacts/upstream").resolve())
    source = PACKAGE / "src"

    def write(path, text):
        target = source / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(text, encoding="utf-8")

    base = "com/aionemu/gameserver/"
    paths = ["network/aion/serverpackets/SM_MOTION.java", "network/aion/serverpackets/SM_EMOTION_LIST.java",
             "model/gameobjects/player/motion/Motion.java", "model/gameobjects/player/motion/MotionType.java",
             "model/gameobjects/player/emotion/Emotion.java", "model/Expirable.java"]
    hashes = {}
    for path in paths:
        repo_path = "game-server/src/" + base + path
        data = subprocess.check_output(["git", "-C", str(ROOT.parent / "aion-server"), "show", f"{SHA}:{repo_path}"])
        hashes[repo_path] = hashlib.sha256(data).hexdigest()
        write(base + path, data.decode("utf-8"))
    write(base + "network/aion/AionConnection.java", "package com.aionemu.gameserver.network.aion; public class AionConnection {}")
    write(base + "network/aion/AionServerPacket.java", """package com.aionemu.gameserver.network.aion;
public abstract class AionServerPacket {
  private final java.io.ByteArrayOutputStream bytes = new java.io.ByteArrayOutputStream();
  protected void writeC(int value) { bytes.write(value); }
  protected void writeH(int value) { writeC(value); writeC(value >>> 8); }
  protected void writeD(int value) { writeH(value); writeH(value >>> 16); }
  protected abstract void writeImpl(AionConnection con);
  public byte[] payload() { writeImpl(null); return bytes.toByteArray(); }
}""")
    write(base + "dataholders/DataManager.java", """package com.aionemu.gameserver.dataholders;
import com.aionemu.gameserver.model.gameobjects.player.motion.MotionType;
public class DataManager {
  public static final ItemData ITEM_DATA = new ItemData();
  public static class ItemData { public MotionType getMotionType(int id) { return null; } }
}""")
    write(base + "model/gameobjects/player/Player.java", """package com.aionemu.gameserver.model.gameobjects.player;
import java.util.*;
import com.aionemu.gameserver.model.gameobjects.player.motion.*;
public class Player {
  private final int id; private final Motions motions = new Motions();
  public Player(int id) { this.id=id; }
  public int getObjectId() { return id; }
  public Motions getMotions() { return motions; }
  public Emotions getEmotions() { return new Emotions(); }
  public static class Motions {
    public final Map<MotionType,Motion> active = new EnumMap<>(MotionType.class);
    public Map<MotionType,Motion> getActiveMotions() { return active; }
    public void remove(int id) {}
  }
  public static class Emotions { public void remove(int id) {} }
}""")
    write("MotionEmotionOracle.java", """import java.util.*;
import com.aionemu.gameserver.model.gameobjects.player.Player;
import com.aionemu.gameserver.model.gameobjects.player.motion.*;
import com.aionemu.gameserver.model.gameobjects.player.emotion.Emotion;
import com.aionemu.gameserver.network.aion.*;
import com.aionemu.gameserver.network.aion.serverpackets.*;
public class MotionEmotionOracle {
  static class FrozenMotion extends Motion {
    int seconds;
    FrozenMotion(int id, int seconds, boolean active) { super(id, 0, active); this.seconds=seconds; }
    public int secondsUntilExpiration() { return seconds; }
  }
  static class FrozenEmotion extends Emotion {
    final int seconds;
    FrozenEmotion(int id, int seconds) { super(id, 0); this.seconds=seconds; }
    public int secondsUntilExpiration() { return seconds; }
  }
  public static void main(String[] args) throws Exception {
    try(var reader=new java.io.BufferedReader(new java.io.InputStreamReader(System.in))) {
      String line;
      while((line=reader.readLine())!=null) {
        String[] p=line.split(" "); AionServerPacket packet;
        switch(p[0]) {
          case "emotion": {
            List<Emotion> emotions=p[2].equals("null")?null:new ArrayList<>();
            if(!p[2].equals("null") && !p[2].equals("-")) for(String raw:p[2].split(",")) {
              String[] v=raw.split(":"); emotions.add(new FrozenEmotion(Integer.parseInt(v[0]),Integer.parseInt(v[1])));
            }
            packet=new SM_EMOTION_LIST(SM_EMOTION_LIST.Action.valueOf(p[1]), emotions); break;
          }
          case "list": {
            List<Motion> motions=new ArrayList<>();
            if(!p[1].equals("-")) for(String raw:p[1].split(",")) {
              String[] v=raw.split(":"); motions.add(new FrozenMotion(Integer.parseInt(v[0]),Integer.parseInt(v[1]),v[2].equals("1")));
            }
            packet=SM_MOTION.list(motions); break;
          }
          case "add": {
            FrozenMotion motion=new FrozenMotion(Integer.parseInt(p[1]),Integer.parseInt(p[2]),false);
            packet=SM_MOTION.add(motion); motion.seconds=-123; break; // ADD snapshots time at construction
          }
          case "set": packet=SM_MOTION.set(Integer.parseInt(p[1]),MotionType.getById(Integer.parseInt(p[2])));break;
          case "remove": packet=SM_MOTION.remove(Integer.parseInt(p[1]));break;
          case "player": {
            Player player=new Player(Integer.parseInt(p[1]));
            if(!p[2].equals("-")) for(String raw:p[2].split(",")) {
              String[] v=raw.split(":"); int id=Integer.parseInt(v[1]);
              player.getMotions().active.put(MotionType.valueOf(v[0]),new FrozenMotion(id,0,true));
            }
            packet=SM_MOTION.playerMotions(player);break;
          }
          default:throw new IllegalArgumentException(line);
        }
        System.out.println(HexFormat.of().withUpperCase().formatHex(packet.payload()));
      }
    }
  }
}""")
    cases = []
    def case(name, kind, **inputs):
        cases.append(dict(name=name, kind=kind, inputs=inputs))
    for action in ("LIST", "ADD", "REMOVE"):
        case("emotion-empty-"+action, "emotion", action=action, emotions=[])
        case("emotion-nonempty-"+action, "emotion", action=action, emotions=[dict(id=64, seconds=86400), dict(id=65535, seconds=-1), dict(id=65536, seconds=0)])
    case("emotion-null-list", "emotion", action="LIST", emotions=None)
    case("motion-empty-list", "list", motions=[])
    case("motion-mixed-list", "list", motions=[dict(id=1001, seconds=0, active=True), dict(id=65535, seconds=86400, active=False), dict(id=-1, seconds=-1, active=True)])
    for seconds in (0, 86400, -1, 2147483647, -2147483648):
        case("motion-add-"+str(seconds), "add", id=65535, seconds=seconds)
    for slot in range(1,6):
        case("motion-set-slot-"+str(slot), "set", id=0 if slot==5 else 65535, slot=slot)
    case("motion-remove", "remove", id=65535)
    case("player-empty", "player", id=1, slots={})
    case("player-reverse-insertion", "player", id=1, slots=dict(SHOP=105, REST=104, JUMP=103, RUN=102, IDLE=101))
    commands = []
    for c in cases:
        v=c["inputs"]; k=c["kind"]
        if k=="emotion":
            raw="null" if v["emotions"] is None else ",".join(f'{e["id"]}:{e["seconds"]}' for e in v["emotions"]) or "-"
            command=f'emotion {v["action"]} {raw}'
        elif k=="list": command="list "+(",".join(f'{m["id"]}:{m["seconds"]}:{int(m["active"])}' for m in v["motions"]) or "-")
        elif k=="player": command=f'player {v["id"]} '+(",".join(f'{slot}:{id}' for slot,id in v["slots"].items()) or "-")
        else: command=" ".join(map(str,[k,v["id"],*([v["seconds"]] if k=="add" else [v["slot"]] if k=="set" else [])]))
        commands.append(command)
    classes=PACKAGE / "classes";classes.mkdir(parents=True,exist_ok=True)
    subprocess.run(["javac","-encoding","UTF-8","-d",str(classes),*[str(p) for p in source.rglob("*.java")]],check=True)
    output=subprocess.run(["java","-cp",str(classes),"MotionEmotionOracle"],input="\n".join(commands)+"\n",text=True,capture_output=True,check=True).stdout.splitlines()
    assert len(output)==len(cases)
    for c,hex in zip(cases,output):c["payloadHex"]=hex
    fixture=dict(schemaVersion=1,source="Java",sourceJavaCommit=SHA,sourceHashes=hashes,
                 captureBoundary="Exact packet/model sources; little-endian payload-only writer, minimal Player/DataManager and frozen expiration subclasses",cases=cases)
    (ROOT / "parity-artifacts/golden/packets/MotionEmotion.f7230c5fa.json").write_text(json.dumps(fixture,indent=2)+"\n",encoding="utf-8")
    print(f"Captured {len(cases)} payloads from exact Java {SHA}.")


if __name__ == "__main__": main()
