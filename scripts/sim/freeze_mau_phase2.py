#!/usr/bin/env python3
"""Audit and freeze the Phase 2 development finalist before unseen evaluation."""

import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path
import subprocess


ROOT = Path(__file__).resolve().parents[2]
RUN_ROOT = ROOT / "run/bot-learning-phase2/current"
REPORT = ROOT / "docs/bot-learning-phase2-duration91-development.json"
LOCK = ROOT / "docs/bot-learning-phase0-lock.json"
POLICY = ROOT / "docs/bot-learning-phase2-candidates/n04.json"
FREEZE = ROOT / "docs/bot-learning-phase2-frozen.json"
SOURCE_FILES = (
    "tests/Aion.Bots/Scenarios/NaturalMauPolicyParameters.cs",
    "tests/Aion.Bots/Scenarios/NaturalPriestCombatPolicy.cs",
    "tests/Aion.Bots/Scenarios/NaturalIshalgenJourney.cs",
    "tests/Aion.Bots/Scenarios/NaturalJourneyRuntime.cs",
    "tests/Aion.Bots/Navigation/NaturalPullPlanner.cs",
    "tests/Aion.Simulation.Tests/SimulationMauCourseTests.cs",
    "tests/Aion.GameServer.TestKit/VirtualThreadPool.cs",
    "src/Aion.GameServer/Commons/Utils/Rnd.cs",
    "src/Aion.GameServer/SpawnEngine/SpawnEngine.cs",
    "game-server/data/static_data/spawns/Npcs/220010000_Ishalgen.xml",
    "game-server/data/static_data/skills/skill_templates.xml",
    "docs/bot-learning-phase0-lock.json",
    "scripts/sim/run-mau-phase2.ps1",
    "scripts/sim/trace/summarize_mau_course.py",
    "scripts/sim/trace/make_mau_decisions.py",
)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def aggregate(pairs, side):
    return {"completed": sum(p[side]["status"] == "complete" for p in pairs),
            "deaths": sum(p[side]["deaths"] for p in pairs),
            "gameSeconds": round(sum(p[side]["gameSeconds"] for p in pairs), 3),
            "potions": sum(p[side]["potions"] for p in pairs),
            "routeFailures": sum(p[side]["routeFailures"] for p in pairs),
            "retreatAttempts": sum(p[side]["retreatAttempts"] for p in pairs)}


def main():
    if FREEZE.exists():
        raise FileExistsError(f"Refusing to overwrite an existing freeze: {FREEZE}")
    report = json.loads(REPORT.read_text(encoding="utf-8"))
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    policy = json.loads(POLICY.read_text(encoding="utf-8"))
    pairs = report["pairs"]
    expected = {(course, seed) for course, seeds in (
        ("GeneratorToRae", (11, 12, 13, 14, 15, 16, 17, 20)),
        ("RaeToHatata", (11, 12, 13, 14, 15, 16, 20))) for seed in seeds}
    if report["stopped"] is not None or {(p["course"], p["seed"]) for p in pairs} != expected or len(pairs) != len(expected):
        raise ValueError("Development report is incomplete or early-stopped")
    if any(p["seed"] not in lock["seedSplit"]["development"] for p in pairs):
        raise ValueError("Development report used a seed outside the locked split")
    modules = set()
    policy_ids = set()
    policy_receipts = set()
    for pair in pairs:
        baseline, candidate = pair["baseline"], pair["candidate"]
        if (baseline["status"] != "complete" or candidate["status"] != "complete" or
                candidate["deaths"] > baseline["deaths"] or
                candidate["maxObservedAttackers"] > baseline["maxObservedAttackers"] or
                baseline["moduleId"] != candidate["moduleId"] or
                baseline["courseResetSha256"] != candidate["courseResetSha256"] or
                not baseline["policyBaseline"] or candidate["policyBaseline"]):
            raise ValueError(f"Development safety or pairing failure: {pair['course']} seed {pair['seed']}")
        for side, summary in (("baseline", baseline), ("candidate", candidate)):
            run = RUN_ROOT / summary["run"]
            trace = run / f"{summary['run']}.trace.jsonl"
            if sha(trace) != summary["traceSha256"]:
                raise ValueError(f"Trace hash changed: {summary['run']}")
            current = json.loads((run / "summary.json").read_text(encoding="utf-8"))
            if current["traceSha256"] != summary["traceSha256"]:
                raise ValueError(f"Summary changed: {summary['run']}")
            modules.add(summary["moduleId"])
            if side == "candidate":
                receipt = run / "policy.json"
                if json.loads(receipt.read_text(encoding="utf-8")) != policy or sha(receipt) != summary["policySha256"]:
                    raise ValueError(f"Candidate policy receipt changed: {summary['run']}")
                policy_ids.add(summary["policyId"])
                policy_receipts.add(summary["policySha256"])
    if len(modules) != 1 or len(policy_ids) != 1 or len(policy_receipts) != 1:
        raise ValueError("Development pairs use multiple modules or candidate policies")
    baseline = aggregate(pairs, "baseline")
    candidate = aggregate(pairs, "candidate")
    if (candidate["deaths"] >= baseline["deaths"] or
            candidate["routeFailures"] > baseline["routeFailures"] or
            candidate["gameSeconds"] >= baseline["gameSeconds"]):
        raise ValueError("Candidate has no practical development benefit")
    git_head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    freeze = {"schemaVersion": 1, "status": "frozen",
              "frozenAtUtc": datetime.now(timezone.utc).isoformat(),
              "frozenAtGitCommit": git_head,
              "candidate": "duration-aware timed potion at 91% HP under observed aggression",
              "policyFile": str(POLICY.relative_to(ROOT)).replace("\\", "/"),
              "policySha256": sha(POLICY),
              "policyReceiptSha256": next(iter(policy_receipts)),
              "policyId": next(iter(policy_ids)),
              "moduleId": next(iter(modules)),
              "sourceHashes": {name: sha(ROOT / name) for name in SOURCE_FILES},
              "developmentReport": str(REPORT.relative_to(ROOT)).replace("\\", "/"),
              "developmentReportSha256": sha(REPORT),
              "developmentPairCount": len(pairs),
              "developmentBaseline": baseline,
              "developmentCandidate": candidate,
              "evaluationSeeds": lock["seedSplit"]["evaluation"],
              "regressionSeeds": lock["seedSplit"]["regression"],
              "decision": "Evaluate paired, unseen seeds; adopt only if completion and deaths do not regress and a practical benefit remains."}
    FREEZE.write_text(json.dumps(freeze, indent=2) + "\n", encoding="utf-8")
    print(f"Frozen {freeze['policyId']} on module {freeze['moduleId']}; "
          f"development deaths {baseline['deaths']}->{candidate['deaths']}, "
          f"game seconds {baseline['gameSeconds']}->{candidate['gameSeconds']}")


if __name__ == "__main__":
    main()
