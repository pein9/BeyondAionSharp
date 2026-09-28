#!/usr/bin/env python3
"""Extract auditable Mau-area intervals from the existing NI-07 SIM traces.

These are archived journey observations, not resettable Phase-0 course runs.
The script never starts the server or changes the original trace files.
"""

import argparse
from collections import Counter
from copy import deepcopy
from datetime import datetime
import hashlib
import json
import math
from pathlib import Path


RAE = (648.419, 920.773)
REGRESSION_SEEDS = (1, 3, 4, 5)


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def seconds(value):
    hours, minutes, tail = value.split(":")
    return int(hours) * 3600 + int(minutes) * 60 + float(tail)


def state_before(records, boundary):
    state = {"selfObjectId": None, "level": None, "hp": None, "maxHp": None, "mp": None, "maxMp": None,
             "position": None, "bind": None, "quests": {}, "skills": {}, "inventory": {}}
    for row in records[:boundary]:
        event = row["packet"]
        fields = row["fields"]
        if row["dir"] != "<":
            if event == "navigation-decision" and isinstance(fields.get("position"), dict):
                state["position"] = fields["position"]
            continue
        if event == "SM_STATS_INFO":
            for key, value in (("level", "level"), ("hp", "currentHp"), ("maxHp", "maxHp"),
                               ("mp", "currentMp"), ("maxMp", "maxMp")):
                state[key] = fields.get(value, state[key])
        elif event == "SM_STATUPDATE_HP":
            state["hp"] = fields.get("currentHp", state["hp"])
            state["maxHp"] = fields.get("maxHp", state["maxHp"])
        elif event == "SM_STATUPDATE_MP":
            state["mp"] = fields.get("currentMp", state["mp"])
            state["maxMp"] = fields.get("maxMp", state["maxMp"])
        elif event == "SM_PLAYER_INFO":
            state["selfObjectId"] = fields["objectId"]
            state["position"] = {"X": fields["x"], "Y": fields["y"], "Z": fields["z"],
                                 "Heading": fields["heading"]}
        elif event == "SM_BIND_POINT_INFO" and fields.get("bindPointType") == 0:
            state["bind"] = {key: fields[key] for key in ("mapId", "x", "y", "z")}
        elif event == "SM_QUEST_LIST":
            state["quests"] = {str(item["questId"]): item for item in fields["quests"]}
        elif event == "SM_QUEST_ACTION" and "questId" in fields and "status" in fields:
            state["quests"][str(fields["questId"])] = {
                "questId": fields["questId"], "status": fields["status"],
                "stepAndFlags": fields.get("stepAndFlags")}
        elif event == "SM_SKILL_LIST":
            if fields.get("silentUpdate"):
                state["skills"].clear()
            for skill in fields["skills"]:
                state["skills"][str(skill["skillId"])] = skill["level"]
        elif event == "SM_INVENTORY_INFO":
            if fields.get("firstPacket"):
                state["inventory"].clear()
            for item in fields["items"]:
                state["inventory"][str(item["objectId"])] = deepcopy(item)
        elif event == "SM_INVENTORY_ADD_ITEM":
            for item in fields["items"]:
                state["inventory"][str(item["objectId"])] = deepcopy(item)
        elif event == "SM_INVENTORY_UPDATE_ITEM":
            item = state["inventory"].get(str(fields["objectId"]))
            if item is not None:
                for key in ("itemCount", "itemMask", "equipmentSlot", "details"):
                    if fields.get(key) is not None:
                        item[key] = fields[key]
    inventory = []
    for item in state.pop("inventory").values():
        if item.get("itemCount", 0) <= 0:
            continue
        details = item.get("details") or {}
        inventory.append({"itemId": item["itemId"], "count": item["itemCount"],
                          "equipmentSlot": item.get("equipmentSlot"),
                          "equippedSlot": details.get("EquippedSlot")})
    state["inventory"] = sorted(inventory, key=lambda item: (item["itemId"], item["equipmentSlot"] or 0))
    state["skills"] = dict(sorted(state["skills"].items(), key=lambda pair: int(pair[0])))
    state["quests"] = dict(sorted(state["quests"].items(), key=lambda pair: int(pair[0])))
    return state


def bounds(records, course):
    if course == "generator_to_rae":
        start = next(i for i, row in enumerate(records)
                     if row["step"] == "ni07-q2007-blue-generator"
                     and row["packet"] == "walk-to-and-use-quest-generator")
        end = next(i for i in range(start + 1, len(records))
                   if records[i]["step"] == "ni07-q2007-rae-return"
                   and records[i]["packet"] == "navigation-decision"
                   and records[i]["fields"].get("action") == "navigation-arrived"
                   and records[i]["fields"].get("targetTemplateId") == 203554)
    else:
        start = next(i for i, row in enumerate(records)
                     if row["step"] == "ni07-q2129-3-Kill"
                     and isinstance(row["fields"].get("position"), dict)
                     and math.dist((row["fields"]["position"]["X"], row["fields"]["position"]["Y"]), RAE) <= 20)
        end = next(i for i in range(start + 1, len(records))
                   if records[i]["packet"] == "SM_QUEST_ACTION"
                   and records[i]["fields"].get("questId") == 2129
                   and (records[i]["fields"].get("stepAndFlags") or 0) >= 65)
    return start, end


def summarize(rows, state, course):
    first, last = rows[0], rows[-1]
    actions = Counter(row["fields"].get("action", "unknown") for row in rows
                      if row["packet"] == "combat-decision")
    choices = [row for row in rows if row["packet"] == "combat-decision"]
    attacks = [row for row in rows if row["packet"] == "SM_ATTACK"
               and row["fields"].get("targetObjId") == state["selfObjectId"]]
    current_target = None
    extra_attackers = set()
    for row in rows:
        if row["packet"] == "combat-decision":
            current_target = row["fields"].get("targetObjectId")
        elif row["packet"] == "SM_ATTACK" and row["fields"].get("targetObjId") == state["selfObjectId"]:
            attacker = row["fields"].get("attackerObjId")
            if current_target is not None and attacker != current_target:
                extra_attackers.add(attacker)
    encounters = []
    for row in choices:
        fields = row["fields"]
        if fields.get("turn") == 0 or not encounters:
            encounters.append({"startVt": row["vt"], "endVt": row["vt"],
                               "targetObjectId": fields.get("targetObjectId"),
                               "actions": Counter(), "maxObservedAttackers": 0})
        encounter = encounters[-1]
        encounter["endVt"] = row["vt"]
        encounter["actions"][fields.get("action", "unknown")] += 1
        encounter["maxObservedAttackers"] = max(encounter["maxObservedAttackers"],
                                                fields.get("observedAttackers") or 0)
    hp = state["hp"]
    damage = 0
    for row in rows:
        fields = row["fields"]
        if row["packet"] in ("SM_STATUPDATE_HP", "SM_STATS_INFO"):
            current = fields.get("currentHp")
            if current is not None and hp is not None and current < hp:
                damage += hp - current
            hp = current
    result = {
        "course": course, "sourceRun": first["run"], "startVt": first["vt"], "endVt": last["vt"],
        "gameSeconds": round(seconds(last["vt"]) - seconds(first["vt"]), 3),
        "wallSeconds": round((datetime.fromisoformat(last["ts"].replace("Z", "+00:00")) -
                              datetime.fromisoformat(first["ts"].replace("Z", "+00:00"))).total_seconds(), 3),
        "startState": state, "endPosition": next((row["fields"]["position"] for row in reversed(rows)
                                                  if isinstance(row["fields"].get("position"), dict)), None),
        "endHp": hp, "clientObservedHpDecreases": damage,
        "deaths": sum(row["packet"] == "accept-client-death-and-revive-at-bound-obelisk" for row in rows),
        "retreatAttempts": actions.get("retreat", 0),
        "retreatRoutes": sum(row["packet"] == "combat-retreat-route" for row in rows),
        "retreatCornered": sum(row["packet"] == "combat-retreat-cornered" for row in rows),
        "potions": sum(row["packet"] == "combat-hot-potion" for row in rows),
        "maxObservedAttackers": max((row["fields"].get("observedAttackers") or 0 for row in choices), default=0),
        "multiAttackerDecisions": sum((row["fields"].get("observedAttackers") or 0) >= 2 for row in choices),
        "attackerObjectIds": sorted({row["fields"].get("attackerObjId") for row in attacks
                                     if row["fields"].get("attackerObjId") is not None}),
        "extraAttackerObjectIdsRelativeToCurrentTarget": sorted(extra_attackers),
        "combatActions": dict(sorted(actions.items())), "encounters": encounters,
        "lastStep": last["step"], "lastPacket": last["packet"],
    }
    if course == "generator_to_rae":
        result["reachedRae"] = last["packet"] == "navigation-decision" and last["fields"].get("action") == "navigation-arrived"
        result["quest2007AtStart"] = state["quests"].get("2007")
    else:
        result["hatataObjectiveUpdate"] = last["fields"]
        result["quest2129AtStart"] = state["quests"].get("2129")
    result["disengagementVerified"] = False  # no explicit all-pursuers-neutral proof at these slice ends
    steps = {}
    for row in rows:
        step = steps.setdefault(row["step"], {"firstVt": row["vt"], "lastVt": row["vt"],
                                            "deaths": 0, "potions": 0, "maxObservedAttackers": 0,
                                            "retreatAttempts": 0})
        step["lastVt"] = row["vt"]
        step["deaths"] += row["packet"] == "accept-client-death-and-revive-at-bound-obelisk"
        step["potions"] += row["packet"] == "combat-hot-potion"
        if row["packet"] == "combat-decision":
            step["maxObservedAttackers"] = max(step["maxObservedAttackers"],
                                               row["fields"].get("observedAttackers") or 0)
            step["retreatAttempts"] += row["fields"].get("action") == "retreat"
    result["steps"] = steps
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=Path("run/natural-batch"))
    parser.add_argument("--output", type=Path, default=Path("run/bot-learning-phase0/archive"))
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    manifest = {"schemaVersion": 1, "kind": "archived-journey-slices", "seeds": list(REGRESSION_SEEDS), "slices": []}
    for seed in REGRESSION_SEEDS:
        folder = args.source / f"retreat72-full-s{seed}"
        paths = list(folder.glob("*.trace.jsonl"))
        if len(paths) != 1:
            raise ValueError(f"Expected one bot trace in {folder}, got {len(paths)}")
        source = paths[0]
        with source.open(encoding="utf-8") as stream:
            records = [json.loads(line) for line in stream]
        context = next(row["fields"]["context"] for row in records if row["packet"] == "natural-run-context")
        if context["Seed"] != seed:
            raise ValueError(f"Seed mismatch: {source}")
        for course in ("generator_to_rae", "rae_to_hatata"):
            start, end = bounds(records, course)
            rows = records[start:end + 1]
            state = state_before(records, start)
            if isinstance(rows[0]["fields"].get("position"), dict):
                state["position"] = rows[0]["fields"]["position"]
            summary = summarize(rows, state, course)
            prefix = f"{course}-retreat72-s{seed}"
            trace = args.output / f"{prefix}.trace.jsonl"
            with trace.open("w", encoding="utf-8", newline="\n") as stream:
                for row in rows:
                    stream.write(json.dumps(row, ensure_ascii=False, separators=(",", ":")) + "\n")
            summary.update({"seed": seed, "sourceTrace": str(source).replace("\\", "/"),
                            "sourceSha256": sha256(source), "sourceLineStart": start + 1,
                            "sourceLineEnd": end + 1, "sourceBuild": context["Build"],
                            "sourceModuleId": context["ModuleId"], "trace": str(trace).replace("\\", "/"),
                            "traceSha256": sha256(trace), "traceRecords": len(rows)})
            summary_path = args.output / f"{prefix}.summary.json"
            summary_path.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
            manifest["slices"].append({"course": course, "seed": seed,
                                       "summary": str(summary_path).replace("\\", "/"),
                                       "summarySha256": sha256(summary_path),
                                       "trace": str(trace).replace("\\", "/"),
                                       "traceSha256": summary["traceSha256"]})
            print(course, seed, f"{summary['gameSeconds']} game s", f"{summary['deaths']} deaths",
                  f"{summary['traceRecords']} records")
    (args.output / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
