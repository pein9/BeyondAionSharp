"""Capture e7a16c553's movement algorithms with javac/java; never modify the Java checkout.

The state and direction types are copied verbatim (package/import/public scope removed,
and the imported nested MoveState name qualified in the direction enum).
Controller classification methods and StatFunctions' table/methods are extracted unchanged.
Small owner/controller stubs isolate the public stat wrapper; this does not exercise world/network IO.
The C# integration tests cover that boundary separately.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import tempfile

SHA = "e7a16c553baec87dc4d58df78cb67c6019f25674"
ROOT = Path(__file__).resolve().parents[2]
BASE = "game-server/src/com/aionemu/gameserver/"


def method(source, marker):
    start = source.index(marker)
    opening = source.index("{", start)
    depth = 1
    end = opening + 1
    while depth:
        depth += (source[end] == "{") - (source[end] == "}")
        end += 1
    return source[start:end]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--java-root", type=Path, default=ROOT.parent / "aion-server")
    parser.add_argument("--output", type=Path, default=ROOT / "parity-artifacts/golden/formulas/MovementModifiers.json")
    parser.add_argument("--player-fixture", type=Path, default=ROOT / "parity-artifacts/golden/formulas/StatFunctions.adjustStatByMovementModifier.player.json")
    args = parser.parse_args()
    paths = ["controllers/movement/" + name + ".java" for name in
             ("MovementModifierState", "MovementModifierDirection", "PlayableMoveController", "PlayerMoveController", "MovementMask")]
    paths.append("utils/stats/StatFunctions.java")
    sources = {p: subprocess.check_output(["git", "-C", str(args.java_root), "show", f"{SHA}:{BASE}{p}"]) for p in paths}
    source = {Path(p).stem: value.decode("utf-8") for p, value in sources.items()}

    def standalone(name):
        text = re.sub(r"^(package|import) .*;\s*", "", source[name], flags=re.M)
        if name == "MovementModifierDirection":
            text = text.replace("MoveState.", "MovementModifierState.MoveState.")
        return re.sub(r"public (class|enum|final class)", r"\1", text, count=1)

    stats = ["PHYSICAL_ATTACK", "MAGICAL_ATTACK", "PHYSICAL_DEFENSE", "MAGICAL_DEFEND", "FIRE_RESISTANCE",
             "EARTH_RESISTANCE", "WATER_RESISTANCE", "WIND_RESISTANCE", "LIGHT_RESISTANCE", "DARK_RESISTANCE",
             "PARRY", "BLOCK", "EVASION", "SPEED", "PHYSICAL_CRITICAL", "MAGICAL_CRITICAL"]
    sf = source["StatFunctions"]
    table = sf[sf.index("private static final int[][] MOVEMENT_MODIFIERS"):sf.index("public static float adjustStatByMovementModifier")]
    scaffold = "\n".join(standalone(name) for name in ("MovementModifierState", "MovementModifierDirection", "MovementMask"))
    scaffold += "\nclass PlayableMoveController {" + method(source["PlayableMoveController"], "static MovementModifierDirection calculateDirection(") + "}\n"
    scaffold += "class PlayerMoveController {" + method(source["PlayerMoveController"], "static MoveRequest getMoveRequest(") + method(source["PlayerMoveController"], "enum MoveRequest {") + "}\n"
    scaffold += "enum StatEnum {" + ",".join(stats) + "}\n"
    scaffold += "class StatFunctions {" + table + "\n" + "\n".join(method(sf, marker) for marker in
        ("public static float adjustStatByMovementModifier(", "static float adjustStatByMoveState(", "public static float adjustSpeedByMovementModifier(")) + "}\n"
    scaffold += """
class Creature {}
class Player extends Creature {
  final MovementModifierState state = new MovementModifierState();
  MovementModifierState getMoveController() { return state; }
}
public class MovementOracle {
  public static void main(String[] args) throws Exception {
    MovementModifierState state = new MovementModifierState();
    try (var reader = new java.io.BufferedReader(new java.io.InputStreamReader(System.in))) {
      String line;
      while ((line = reader.readLine()) != null) {
        String[] p = line.split(" ");
        String result = switch (p[0]) {
          case "heading" -> PlayableMoveController.calculateDirection((byte)Integer.parseInt(p[1]), (byte)Integer.parseInt(p[2])).name();
          case "mask" -> PlayerMoveController.getMoveRequest((byte)Integer.parseInt(p[1])).name();
          case "table" -> Float.toString(StatFunctions.adjustStatByMoveState(Integer.parseInt(p[1]), StatEnum.valueOf(p[2]), Float.parseFloat(p[3])));
          case "speed" -> Float.toString(StatFunctions.adjustSpeedByMovementModifier(MovementModifierDirection.valueOf(p[1]), Float.parseFloat(p[2])));
          case "player" -> {
            Player player = new Player();
            player.state.setMoveState(MovementModifierDirection.valueOf(p[1]).getMoveStateFlag(), System.currentTimeMillis()-667);
            yield Float.toString(StatFunctions.adjustStatByMovementModifier(player, StatEnum.valueOf(p[2]), Float.parseFloat(p[3])));
          }
          case "reset" -> { state = new MovementModifierState(); yield "reset"; }
          case "set" -> { long now = Long.parseLong(p[2]); state.setMoveState(Integer.parseInt(p[1]), now); yield Integer.toString(state.getMoveState(now)); }
          case "get" -> Integer.toString(state.getMoveState(Long.parseLong(p[1])));
          default -> throw new IllegalArgumentException(line);
        };
        System.out.println(result);
      }
    }
  }
}
"""
    commands = []

    def queue(*parts):
        index = len(commands)
        commands.append(" ".join(map(str, parts)))
        return index

    headings = [queue("heading", dest, own) for dest in range(256) for own in range(256)]
    masks = [queue("mask", mask) for mask in range(256)]
    table_cases = []
    for state in range(8):
        for stat in stats:
            for value in ("1000.0", "1234.5", "-50.0", "-0.0"):
                table_cases.append(dict(moveState=state, stat=stat, value=value, resultFloat=queue("table", state, stat, value)))
    speed_cases = [dict(direction=d, value=v, resultFloat=queue("speed", d, v))
                   for d in ("NONE", "FORWARD", "BACKWARD", "SIDEWAYS") for v in ("6000.0", "1.25", "-0.0")]
    scenarios = {
        "activation-and-no-idle-timeout": [(1, 0), (None, 0), (None, 666), (None, 667), (None, 100000)],
        "continued-direction": [(1, 0), (1, 600), (None, 667)],
        "exclusive-fade": [(2, 0), (0, 1000), (None, 2331), (None, 2332)],
        "short-movement": [(1, 0), (0, 666), (None, 5000)],
        "activation-does-not-accumulate": [(1, 0), (0, 600), (1, 700), (None, 1300), (None, 1366), (None, 1367)],
        "two-slots": [(1, 0), (4, 1000), (None, 1500), (None, 1666), (None, 1667), (None, 2332)],
        "replace-confirmed": [(1, 0), (2, 700), (4, 1400), (None, 2100), (None, 2732)],
        "same-strafe-bit": [(4, n) for n in range(0, 3000, 300)] + [(None, 667)],
        "intermittent": [(s, n) for n in range(0, 7000, 700) for s, n in ((1, n), (0, n+600))],
        "resume-keeps-fade": [(1, 0), (0, 1000), (1, 1200), (None, 1500), (None, 2332), (None, 5000)],
        "stop-preserves-confirmed": [(1, 0), (4, 1000), (0, 1500), (None, 2331), (None, 2332)],
        "repeated-stops": [(1, 0)] + [(0, n) for n in range(1000, 1050, 6)] + [(None, 2331), (None, 2332)],
        "signed-long-wrap": [(1, 9223372036854775700), (None, -9223372036854775250), (None, -9223372036854775249), (0, -9223372036854775000), (None, -9223372036854773668)],
    }
    traces = []
    for name, actions in scenarios.items():
        queue("reset")
        trace = dict(name=name, actions=[])
        for state, now in actions:
            action = dict(time=now)
            if state is not None:
                action["set"] = state
                action["expected"] = queue("set", state, now)
            else:
                action["expected"] = queue("get", now)
            trace["actions"].append(action)
        traces.append(trace)
    player = json.loads(args.player_fixture.read_text(encoding="utf-8-sig"))
    player_indexes = [queue("player", c["inputs"]["player"]["direction"], c["inputs"]["stat"], c["inputs"]["value"]) for c in player["cases"]]
    with tempfile.TemporaryDirectory(prefix="movement-oracle-") as folder:
        folder = Path(folder)
        (folder / "MovementOracle.java").write_text(scaffold, encoding="utf-8")
        subprocess.run(["javac", "-encoding", "UTF-8", str(folder / "MovementOracle.java")], check=True)
        result = subprocess.run(["java", "-cp", str(folder), "MovementOracle"], input="\n".join(commands)+"\n", text=True, capture_output=True, check=True)
    values = result.stdout.splitlines()
    if len(values) != len(commands):
        raise RuntimeError("Incomplete Java capture")
    for case in table_cases + speed_cases:
        case["resultFloat"] = values[case["resultFloat"]]
    for trace in traces:
        for action in trace["actions"]:
            action["expected"] = int(values[action["expected"]])
    hashes = {BASE + p: hashlib.sha256(data).hexdigest() for p, data in sources.items()}
    fixture = dict(schemaVersion=1, source="Java", sourceJavaCommit=SHA, sourceHashes=hashes,
                   captureBoundary="Exact extracted methods/types; minimal Creature/Player owner stubs; no world/network IO",
                   headingsDestinationMajor="".join(values[i][0] for i in headings),
                   moveRequests="".join(values[i][0] for i in masks), table=table_cases, speed=speed_cases, traces=traces)
    args.output.write_text(json.dumps(fixture, indent=2)+"\n", encoding="utf-8")
    for case, index in zip(player["cases"], player_indexes):
        case["resultFloat"] = values[index]
    player["signature"] = "float adjustStatByMovementModifier(Creature creature, StatEnum stat, float value) [Player activated movement-state branches]"
    player["sourceJavaCommit"] = SHA
    player["captureBoundary"] = fixture["captureBoundary"]
    player["sourceHashes"] = hashes
    # Keep historical case formatting compact so changed outputs remain easy to review.
    header = {k: v for k, v in player.items() if k != "cases"}
    text = json.dumps(header, indent=2)[:-2] + ',\n  "cases": [\n'
    text += ",\n".join("    " + json.dumps(c) for c in player["cases"]) + "\n  ]\n}\n"
    args.player_fixture.write_text(text, encoding="utf-8")
    print(f"Captured {len(headings)} heading pairs, {len(masks)} masks, {len(table_cases)} table cases, {len(speed_cases)} speed cases, {len(traces)} state traces and {len(player_indexes)} player cases from {SHA}")


if __name__ == "__main__":
    main()
