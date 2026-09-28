#!/usr/bin/env python3
"""Extract the first divergence and downstream seed-118 events from frozen traces."""

import hashlib
import json
from collections import Counter
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
REPORT = ROOT / "docs/bot-learning-phase2-evaluation.json"
RUN_ROOT = ROOT / "run/bot-learning-phase2/current"
OUTPUT = ROOT / "docs/bot-learning-phase2-seed118-diagnosis.json"


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def decision_view(row):
    fields = row["fields"]
    return {key: fields.get(key) for key in
            ("encounterId", "turn", "action", "skillId", "hp", "maxHp",
             "mp", "targetHpPercent", "observedAttackers", "hotPotionReady",
             "hotPotionActive")}


def main():
    report = json.loads(REPORT.read_text(encoding="utf-8"))
    if not report["complete"]:
        raise ValueError("Evaluation is incomplete")
    pair = next(p for p in report["pairs"]
                if p["course"] == "GeneratorToRae" and p["seed"] == 118)
    traces = {}
    for side in ("baseline", "candidate"):
        run = pair[side]["run"]
        path = RUN_ROOT / run / f"{run}.trace.jsonl"
        if sha(path) != pair[side]["traceSha256"]:
            raise ValueError(f"Trace receipt mismatch: {run}")
        traces[side] = [json.loads(line) for line in path.open(encoding="utf-8")]

    decisions = {side: [r for r in rows if r["packet"] == "combat-decision"]
                 for side, rows in traces.items()}
    differences = [(a, b) for a, b in zip(decisions["baseline"], decisions["candidate"])
                   if (a["step"], decision_view(a)["encounterId"], decision_view(a)["turn"],
                       decision_view(a)["action"], decision_view(a)["skillId"]) !=
                      (b["step"], decision_view(b)["encounterId"], decision_view(b)["turn"],
                       decision_view(b)["action"], decision_view(b)["skillId"])]
    if not differences:
        raise ValueError("No combat decision difference")
    first = differences[0]
    next_start = {side: next(r for r in rows if r["fields"]["encounterId"] == 10
                             and r["fields"]["turn"] == 0)
                  for side, rows in decisions.items()}
    if next_start["baseline"]["fields"]["observedState"] != \
            next_start["candidate"]["fields"]["observedState"]:
        raise ValueError("Encounter 10 did not start with the same observed state")

    def event(side, packet, vt, predicate=lambda r: True):
        return next(r for r in traces[side] if r["packet"] == packet and r["vt"] == vt
                    and predicate(r))

    death_vt = "00:15:34.250"
    result = {
        "schemaVersion": 1,
        "sourceReport": str(REPORT.relative_to(ROOT)).replace("\\", "/"),
        "sourceReportSha256": sha(REPORT),
        "course": pair["course"], "seed": pair["seed"],
        "runs": {side: {"run": pair[side]["run"], "traceSha256": pair[side]["traceSha256"],
                        "status": pair[side]["status"], "deaths": pair[side]["deaths"],
                        "gameSeconds": pair[side]["gameSeconds"],
                        "routeFailures": pair[side]["routeFailures"],
                        "potions": pair[side]["potions"]}
                 for side in ("baseline", "candidate")},
        "firstDifferentCombatDecision": {
            side: {"vt": row["vt"], "step": row["step"], **decision_view(row)}
            for side, row in zip(("baseline", "candidate"), first)
        },
        "nextEncounterStart": {
            "encounterId": 10,
            "vt": next_start["baseline"]["vt"],
            "sameClientObservedState": True,
            "targetObjectId": next_start["baseline"]["fields"]["targetObjectId"],
        },
        "encounter10Smite": {
            "baselineResult": {
                "vt": "00:15:17.612",
                "packet": "SM_CASTSPELL_RESULT",
                "flags": event("baseline", "SM_CASTSPELL_RESULT", "00:15:17.612",
                               lambda r: r["fields"].get("skillId") == 4013)["fields"]["flags"],
            },
            "candidateCancel": {
                "vt": "00:15:17.612",
                "packet": "SM_SKILL_CANCEL",
                "skillId": event("candidate", "SM_SKILL_CANCEL", "00:15:17.612")["fields"]["skillId"],
            },
            "targetHpPercentAtTurn8": {
                side: next(r["fields"]["targetHpPercent"] for r in rows
                           if r["fields"]["encounterId"] == 10 and r["fields"]["turn"] == 8)
                for side, rows in decisions.items()
            },
        },
        "candidateExtraDeath": {
            "vt": death_vt,
            "step": event("candidate", "SM_SYSTEM_MESSAGE", death_vt,
                          lambda r: r["fields"].get("name") == "STR_MSG_COMBAT_MY_DEATH")["step"],
            "message": "STR_MSG_COMBAT_MY_DEATH",
        },
        "routeFailures": {},
    }
    for side, rows in traces.items():
        failures = [r for r in rows if r["packet"] == "route-failed"]
        result["routeFailures"][side] = {
            "total": len(failures),
            "beforeExtraDeath": sum(r["vt"] < death_vt for r in failures),
            "afterExtraDeath": sum(r["vt"] > death_vt for r in failures),
            "byStep": dict(sorted(Counter(r["step"] for r in failures).items())),
            "byNavmeshOutcome": dict(sorted(Counter(r["fields"]["navmeshOutcome"]
                                                for r in failures).items())),
        }
    OUTPUT.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(f"Seed 118 diagnosis written: {OUTPUT}")


if __name__ == "__main__":
    main()
