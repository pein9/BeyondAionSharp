"""Extract public movement waypoints from the operator's closed 4.8 recording.

Usage: python tools/recording/compile-fire-room-route.py RECORDING [--check]
The private recording stays in run/. No account IDs, packets or credentials are emitted.
"""
import argparse
import base64
import hashlib
import json
import math
import struct
import xml.etree.ElementTree as ET
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("recording", type=Path)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    raw = args.recording.read_bytes()
    entries = [json.loads(line) for line in raw.splitlines()]
    if not entries or entries[-1].get("event") != "session-close":
        raise SystemExit("Refusing an open or incomplete recording")

    def extract(first, last, map_id):
        rows = [e for e in entries if first <= e["seq"] <= last and
                e.get("dir") == "C" and e.get("packet") == "CM_MOVE"]
        if not rows or rows[0]["seq"] != first or rows[-1]["seq"] != last:
            raise SystemExit(f"Missing movement endpoints {first}..{last}")
        if any(e["player"]["mapId"] != map_id or e.get("outcome") != "executed" for e in rows):
            raise SystemExit("Route has an unexpected map or an unexecuted packet")
        return {"mapId": map_id, "points": [dict(
            sequence=e["seq"], milliseconds=e["t"] - rows[0]["t"],
            x=e["fields"]["x"], y=e["fields"]["y"], z=e["fields"]["z"],
            heading=e["fields"]["heading"], mask=e["fields"]["type"],
            glideFlag=e["fields"].get("glideFlag", 0)) for e in rows]}

    # Sequence boundaries are the last grounded position before takeoff/jump and
    # the stable landing. Wind Room exploration is deliberately excluded.
    entrance = [e for e in entries if e.get("packet") == "CM_MOVE" and
                "2026-10-20T14:56:46.417" <= e["wall"] <= "2026-10-20T14:57:04.781Z"]
    # First targeted casts on the five lower-floor guards, followed by the boss.
    # Object IDs are used only to join this recording and are never published.
    npcs, seen, fights, fight_targets = {}, set(), [], []
    for entry in entries:
        fields = entry.get("fields") or {}
        if entry.get("packet") == "SM_NPC_INFO":
            x, y, z, object_id, npc_id = struct.unpack_from(
                "<fffII", base64.b64decode(entry["frameBase64"]), entry.get("bodyOffset", 7))
            if object_id != fields["npc"]["objectId"]:
                raise SystemExit("NPC packet identity does not match its decoded body")
            npcs[object_id] = (npc_id, [x, y, z])
        if not (25426 < entry["seq"] <= 29582 and entry.get("dir") == "C" and
                entry.get("packet") == "CM_CASTSPELL" and entry.get("outcome") == "executed" and
                entry.get("player", {}).get("mapId") == 320050000):
            continue
        target = fields.get("targetObjectId")
        if target in seen or target not in npcs:
            continue
        npc_id, anchor = npcs[target]
        if npc_id not in (212782, 212781, 212778, 212780, 212876):
            continue
        seen.add(target)
        fight_targets.append(target)
        fights.append(dict(npcId=npc_id, sourceSequence=entry["seq"], anchor=anchor,
                           pullPosition=[entry["player"][axis] for axis in ("x", "y", "z")]))
    if [fight["npcId"] for fight in fights] != [212782, 212781, 212781, 212778, 212780, 212876]:
        raise SystemExit("Recorded Fire Room fight order does not match the reviewed sequence")
    root = Path(__file__).resolve().parents[2]
    spawns = ET.parse(root / "game-server/data/static_data/spawns/Instances/320050000_Sky_Temple_Interior.xml")
    walkers = {route.get("route_id"): [[float(step.get(axis)) for axis in ("x", "y", "z")] for step in route]
               for route in ET.parse(root / "game-server/data/static_data/npc_walker/npc_walker.xml").getroot()}

    def distance_to_segment(point, start, end):
        delta = [b - a for a, b in zip(start, end)]
        square = sum(value * value for value in delta)
        along = min(1, max(0, sum((p - a) * d for p, a, d in zip(point, start, delta)) / square)) if square else 0
        return math.dist(point, [a + along * d for a, d in zip(start, delta)])

    # Two demonstrated targets are patrols. Bind their route rather than treating the
    # recording's moving NPC position as a static spawn in every later world.
    for fight in fights:
        candidates = []
        for spawn in spawns.findall(f".//spawn[@npc_id='{fight['npcId']}']"):
            for spot in spawn.findall("spot"):
                route_id = spot.get("walker_id")
                path = walkers.get(route_id)
                at = [float(spot.get(axis)) for axis in ("x", "y", "z")]
                distance = min(distance_to_segment(fight["anchor"], a, b) for a, b in zip(path, path[1:])) if path else math.dist(fight["anchor"], at)
                candidates.append((distance, route_id or ""))
        distance, route_id = min(candidates)
        if distance > 0.25:
            raise SystemExit(f"Recorded NPC {fight['npcId']} does not match a native spawn or patrol")
        if route_id:
            fight["patrolRouteId"] = route_id
    ground_moves = [entry for entry in entries if entry.get("packet") == "CM_MOVE" and entry.get("dir") == "C" and
                    entry.get("player", {}).get("mapId") == 320050000]

    def position(entry):
        return [entry["fields"][axis] for axis in ("x", "y", "z")]

    def simplify(moves):
        if len(moves) < 3:
            return moves
        distance, index = max((distance_to_segment(position(entry), position(moves[0]), position(moves[-1])), index)
                              for index, entry in enumerate(moves[1:-1], 1))
        return simplify(moves[:index + 1])[:-1] + simplify(moves[index:]) if distance > 0.5 else [moves[0], moves[-1]]

    prior = 25426
    for fight in fights:
        first = max(entry["seq"] for entry in ground_moves if entry["seq"] <= prior)
        moves = [entry for entry in ground_moves if first <= entry["seq"] < fight["sourceSequence"]]
        if not moves or any(entry.get("outcome") != "executed" or entry["fields"]["type"] & 4 or
                            not 540 < entry["fields"]["z"] < 565 for entry in moves):
            raise SystemExit("Recorded lower-floor approach is not an executed ground walk")
        # Follow actual reported foot positions, never an unfulfilled x2/y2/z2 movement intention.
        # In particular the westward walk avoids the raised platform between the Guardian and Obscura.
        fight["approach"] = [dict(sequence=entry["seq"], **{key: entry["fields"][key] for key in ("x", "y", "z", "heading")})
                             for entry in simplify(moves)]
        if math.dist(position(moves[-1]), fight["pullPosition"]) > 0.05:
            fight["approach"].append(dict(sequence=fight["sourceSequence"], **dict(zip(("x", "y", "z"), fight["pullPosition"])),
                                          heading=moves[-1]["fields"]["heading"]))
        prior = fight["sourceSequence"]
    # The player also moves DURING fights: notably chasing the first Obscura east.
    # Bound each corridor by that target's explicit zero-HP packet, not the next pull.
    for fight, target in zip(fights, fight_targets):
        death = None
        for entry in entries:
            if entry["seq"] <= fight["sourceSequence"] or entry.get("packet") != "SmAttackStatus":
                continue
            object_id, _, status_type, hp = struct.unpack_from(
                "<iiBB", base64.b64decode(entry["frameBase64"]), entry.get("bodyOffset", 7))
            if object_id == target and status_type not in range(19, 24) and hp == 0:
                death = entry["seq"]
                break
        if death is None:
            raise SystemExit("Recorded Fire fight has no explicit target death")
        moves = [entry for entry in ground_moves if fight["sourceSequence"] < entry["seq"] < death]
        if not moves or any(entry.get("outcome") != "executed" or entry["fields"]["type"] & 4 or
                            not 540 < entry["fields"]["z"] < 565 for entry in moves):
            raise SystemExit("Recorded Fire fight movement is not an executed ground walk")
        fight["deathSequence"] = death
        fight["pursuit"] = [dict(sequence=fight["sourceSequence"], **dict(zip(("x", "y", "z"), fight["pullPosition"])),
                                 heading=moves[0]["fields"]["heading"])]
        fight["pursuit"].extend(dict(sequence=entry["seq"], **{key: entry["fields"][key] for key in ("x", "y", "z", "heading")})
                                for entry in simplify(moves))
    exit_use = next(entry for entry in entries if entry["seq"] > fights[-1]["deathSequence"] and
                    entry.get("packet") == "CM_SHOW_DIALOG" and entry.get("outcome") == "executed" and
                    npcs.get(entry.get("fields", {}).get("targetObjectId"), (None,))[0] == 730046)
    exit_moves = [entry for entry in ground_moves if fights[-1]["deathSequence"] < entry["seq"] < exit_use["seq"]]
    if len(exit_moves) < 2 or any(entry.get("outcome") != "executed" or entry["fields"]["type"] & 4 or
                                  not 540 < entry["fields"]["z"] < 565 for entry in exit_moves):
        raise SystemExit("Recorded Fire exit is not an executed ground walk")
    exit_route = dict(portalNpcId=730046, sourceUseSequence=exit_use["seq"],
                      points=[dict(sequence=entry["seq"], **{key: entry["fields"][key] for key in ("x", "y", "z", "heading")})
                              for entry in simplify(exit_moves)])
    document = {"schema": 4, "sourceSha256": hashlib.sha256(raw).hexdigest(),
                "recordedUtc": "2026-10-20", "recordedAccessLevel": 9,
                "entrance": extract(entrance[0]["seq"], entrance[-1]["seq"], 220020000),
                "descent": extract(24762, 25426, 320050000),
                "clearing": fights[:-1], "boss": fights[-1], "exitRoute": exit_route}
    output = Path(__file__).resolve().parents[2] / "parity-artifacts/e2e/natural-fire-room-route.json"
    rendered = json.dumps(document, indent=2) + "\n"
    if args.check:
        if output.read_text(encoding="utf-8") != rendered:
            raise SystemExit("Fire Room route drift; regenerate from the original recording")
        print("Fire Room route matches the recording")
    else:
        output.write_text(rendered, encoding="utf-8")
        print(output)


if __name__ == "__main__":
    main()
