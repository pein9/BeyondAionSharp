#!/usr/bin/env python3
"""Summarize one focused Mau SIM course without discarding failed traces."""

import argparse
from collections import Counter
from datetime import datetime
import hashlib
import json
from pathlib import Path


def digest(path):
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def seconds(value):
    hours, minutes, tail = value.split(":")
    return int(hours) * 3600 + int(minutes) * 60 + float(tail)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run_dir", type=Path)
    args = parser.parse_args()
    traces = list(args.run_dir.glob("*.trace.jsonl"))
    if len(traces) != 1:
        raise ValueError(f"Expected exactly one trace in {args.run_dir}; found {len(traces)}")
    trace = traces[0]
    rows = [json.loads(line) for line in trace.open(encoding="utf-8")]
    if not rows:
        raise ValueError(f"Empty trace: {trace}")
    context = next(row["fields"]["context"] for row in rows if row["packet"] == "natural-run-context")
    starts = [row for row in rows if row["packet"] == "phase0-course-start"]
    completions = [row for row in rows if row["packet"] == "phase0-course-complete"]
    failures = list(args.run_dir.glob("*.failure.json"))
    if len(starts) != 1 or len(completions) > 1:
        raise ValueError("Course start/completion marker count is invalid")
    start = starts[0]
    end = rows[-1]
    decisions = [row for row in rows if row["packet"] == "combat-decision"]
    actions = Counter(row["fields"].get("action") for row in decisions)
    steps = {}
    for row in rows:
        step = steps.setdefault(row["step"], {"firstVt": row["vt"], "lastVt": row["vt"],
                                             "deaths": 0, "potions": 0, "maxObservedAttackers": 0})
        step["lastVt"] = row["vt"]
        step["deaths"] += row["packet"] == "accept-client-death-and-revive-at-bound-obelisk"
        step["potions"] += row["packet"] == "combat-hot-potion"
        if row["packet"] == "combat-decision":
            step["maxObservedAttackers"] = max(step["maxObservedAttackers"],
                                               row["fields"].get("observedAttackers") or 0)
    hp = start["fields"]["checkpoint"]["CurrentHp"]
    damage = 0
    for row in rows[rows.index(start) + 1:]:
        if row["packet"] in ("SM_STATUPDATE_HP", "SM_STATS_INFO"):
            current = row["fields"].get("currentHp")
            if current is not None and hp is not None and current < hp:
                damage += hp - current
            hp = current
    encounters = []
    for row in decisions:
        fields = row["fields"]
        if fields.get("turn") == 0 or not encounters:
            encounters.append({"startVt": row["vt"], "endVt": row["vt"],
                               "targetObjectId": fields.get("targetObjectId"),
                               "actions": Counter(), "maxObservedAttackers": 0})
        encounter = encounters[-1]
        encounter["endVt"] = row["vt"]
        encounter["actions"][fields.get("action")] += 1
        encounter["maxObservedAttackers"] = max(encounter["maxObservedAttackers"],
                                                fields.get("observedAttackers") or 0)
    completion = json.loads((args.run_dir / "course-completion.json").read_text(encoding="utf-8")) \
        if (args.run_dir / "course-completion.json").exists() else None
    status = "complete" if completion is not None and len(completions) == 1 else \
             "failed" if failures else "interrupted-or-incomplete"
    root = Path(__file__).resolve().parents[3]
    code_hashes = {}
    for relative in ("tests/Aion.Bots/Scenarios/NaturalIshalgenJourney.cs",
                     "tests/Aion.Bots/Scenarios/NaturalJourneyRuntime.cs",
                     "tests/Aion.Bots/Scenarios/NaturalPriestCombatPolicy.cs",
                     "tests/Aion.Bots/Scenarios/NaturalMauPolicyParameters.cs",
                     "tests/Aion.Bots/Navigation/NaturalPullPlanner.cs",
                     "tests/Aion.Simulation.Tests/SimulationMauCourseTests.cs",
                     "tests/Aion.Simulation.Tests/SimulationWorldFixture.cs",
                     "tests/Aion.GameServer.TestKit/VirtualThreadPool.cs",
                     "src/Aion.GameServer/SpawnEngine/SpawnEngine.cs",
                     "src/Aion.GameServer/Commons/Utils/Rnd.cs",
                     "game-server/data/static_data/spawns/Npcs/220010000_Ishalgen.xml",
                     "docs/bot-learning-phase0-lock.json",
                     "scripts/sim/trace/make_mau_decisions.py"):
        code_hashes[relative] = digest(root / relative)
    reset_path = args.run_dir / "course-reset.json"
    reset_markers = [row for row in rows if row["packet"] == "phase1-course-reset"]
    if reset_path.exists():
        if len(reset_markers) != 1 or reset_markers[0]["fields"].get("sha256") != digest(reset_path):
            raise ValueError("Course reset snapshot does not match its trace marker")
    elif reset_markers:
        raise ValueError("Trace names a course reset snapshot that is missing")
    summary = {
        "schemaVersion": 1, "status": status, "course": start["fields"]["course"],
        "encounter": start["fields"].get("encounter"),
        "run": context["Run"], "seed": context["Seed"], "build": context["Build"],
        "moduleId": context["ModuleId"], "trace": str(trace.resolve()),
        "traceSha256": digest(trace), "traceRecords": len(rows), "codeAndDataSha256": code_hashes,
        "courseResetSha256": digest(reset_path) if reset_path.exists() else None,
        "startVt": start["vt"], "endVt": end["vt"],
        "gameSeconds": round(seconds(end["vt"]) - seconds(start["vt"]), 3),
        "wallSeconds": round((datetime.fromisoformat(end["ts"].replace("Z", "+00:00")) -
                              datetime.fromisoformat(start["ts"].replace("Z", "+00:00"))).total_seconds(), 3),
        "startCheckpoint": start["fields"]["checkpoint"], "completion": completion,
        "failureFiles": [str(path.resolve()) for path in failures],
        "deaths": sum(row["packet"] == "accept-client-death-and-revive-at-bound-obelisk" for row in rows),
        "retreatAttempts": actions["retreat"],
        "potions": sum(row["packet"] == "combat-hot-potion" for row in rows),
        "clientObservedHpDecreases": damage,
        "maxObservedAttackers": max((row["fields"].get("observedAttackers") or 0 for row in decisions), default=0),
        "routeFailures": sum(row["packet"] in ("route-failed", "guard-approach-blocked") for row in rows),
        "combatActions": dict(sorted(actions.items())), "encounters": encounters, "steps": steps,
        "lastStep": end["step"], "lastPacket": end["packet"],
    }
    policy_markers = [row for row in rows if row["packet"] == "phase2-policy"]
    if policy_markers:
        if len(policy_markers) != 1 or not (args.run_dir / "policy.json").exists():
            raise ValueError("Phase 2 policy marker or policy.json missing")
        if json.loads((args.run_dir / "policy.json").read_text(encoding="utf-8")) != policy_markers[0]["fields"]["parameters"]:
            raise ValueError("Phase 2 policy file differs from the executed trace policy")
        summary["policyId"] = policy_markers[0]["fields"]["policyId"]
        summary["policyBaseline"] = policy_markers[0]["fields"]["baseline"]
        summary["policySha256"] = digest(args.run_dir / "policy.json")
    output = args.run_dir / "summary.json"
    output.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(f"{summary['run']}: {status}, {summary['gameSeconds']} game s, "
          f"{summary['deaths']} deaths, {summary['maxObservedAttackers']} max attackers")


if __name__ == "__main__":
    main()
