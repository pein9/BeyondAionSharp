#!/usr/bin/env python3
"""Pair the fixed Phase 2 finalists with baseline on both Mau full legs."""

import json
from pathlib import Path
import subprocess


ROOT = Path(__file__).resolve().parents[2]
PLAN = ROOT / "docs/bot-learning-phase2-development-plan.json"
OUTPUT = ROOT / "docs/bot-learning-phase2-development-a.json"
RUN_ROOT = ROOT / "run/bot-learning-phase2/current"
RUNNER = ROOT / "scripts/sim/run-mau-phase2.ps1"


def load_run(course, seed, candidate_id):
    course_slug = "gen" if course == "GeneratorToRae" else "hat"
    policy_slug = "base" if candidate_id == "baseline" else "cand"
    tag = "dev-a" if candidate_id == "baseline" else f"{candidate_id}-dev-a"
    run = f"p2-{course_slug}-full-{policy_slug}-s{seed}-r01-{tag}"
    directory = RUN_ROOT / run
    path = directory / "summary.json"
    if not path.exists():
        if directory.exists():
            raise RuntimeError(f"Incomplete run needs diagnosis: {directory}")
        cmd = ["pwsh", "-NoProfile", "-File", str(RUNNER), "-Course", course,
               "-Seeds", str(seed), "-Split", "development", "-Repeats", "1", "-Tag", tag]
        if candidate_id != "baseline":
            cmd += ["-PolicyFile", str(ROOT / "docs/bot-learning-phase2-candidates" / f"{candidate_id}.json")]
        print(" ".join(cmd), flush=True)
        subprocess.run(cmd, cwd=ROOT, check=True)
    summary = json.loads(path.read_text(encoding="utf-8"))
    if summary["seed"] != seed or summary["course"] != course or summary["encounter"] is not None:
        raise ValueError(f"Summary has wrong course/seed: {path}")
    return summary


def compact(summary):
    return {key: summary[key] for key in ("run", "status", "course", "seed", "moduleId",
                                            "policyId", "policyBaseline", "policySha256",
                                            "courseResetSha256", "traceSha256", "gameSeconds",
                                            "wallSeconds", "deaths", "maxObservedAttackers",
                                            "routeFailures", "potions", "retreatAttempts", "decisionRecords")}


def main():
    plan = json.loads(PLAN.read_text(encoding="utf-8"))
    screen = json.loads((ROOT / "docs/bot-learning-phase2-screen.json").read_text(encoding="utf-8"))
    passing = {entry["id"] for entry in screen["candidates"] if entry["screenGate"]}
    if not set(plan["finalists"]).issubset(passing):
        raise ValueError("Development finalists did not pass the random screen")
    report = {"schemaVersion": 1, "plan": plan, "pairs": [], "stopped": {}}
    for seed in plan["stageASeeds"]:
        for course in plan["courses"]:
            baseline = load_run(course, seed, "baseline")
            pair = {"seed": seed, "course": course, "baseline": compact(baseline), "candidates": {}}
            for candidate_id in plan["finalists"]:
                if candidate_id in report["stopped"]:
                    continue
                candidate = load_run(course, seed, candidate_id)
                if (candidate["moduleId"] != baseline["moduleId"] or
                        candidate["courseResetSha256"] != baseline["courseResetSha256"]):
                    raise ValueError(f"Unpaired baseline/candidate reset: {course} seed {seed} {candidate_id}")
                pair["candidates"][candidate_id] = compact(candidate)
                if baseline["status"] == "complete" and candidate["status"] != "complete":
                    report["stopped"][candidate_id] = f"New {course} course failure on seed {seed}"
                elif candidate["deaths"] > baseline["deaths"]:
                    report["stopped"][candidate_id] = f"More {course} deaths on seed {seed}"
                print(f"{course} seed {seed} {candidate_id}: {candidate['status']}, "
                      f"deaths {baseline['deaths']}->{candidate['deaths']}, "
                      f"game {baseline['gameSeconds']}->{candidate['gameSeconds']}", flush=True)
            report["pairs"].append(pair)
            OUTPUT.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"Stage A complete; stopped={report['stopped']}; evidence={OUTPUT}", flush=True)


if __name__ == "__main__":
    main()
