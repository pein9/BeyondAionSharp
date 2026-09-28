#!/usr/bin/env python3
"""Evaluate the 21.5 m midpoint on paired development full legs."""

import json
from pathlib import Path
import subprocess


ROOT = Path(__file__).resolve().parents[2]
PLAN = ROOT / "docs/bot-learning-phase2-midpoint-plan.json"
OUTPUT = ROOT / "docs/bot-learning-phase2-midpoint-development.json"
RUN_ROOT = ROOT / "run/bot-learning-phase2/current"
RUNNER = ROOT / "scripts/sim/run-mau-phase2.ps1"


def run_for(course, seed, candidate):
    course_slug = "gen" if course == "GeneratorToRae" else "hat"
    policy_slug = "cand" if candidate else "base"
    if candidate and (course, seed) in (("GeneratorToRae", 12), ("GeneratorToRae", 17),
                                        ("RaeToHatata", 16)):
        tag = "midpoint-v1"
    elif not candidate and (course, seed) == ("RaeToHatata", 16):
        tag = "patrolfix-v1"
    elif not candidate and seed in (12, 16, 20):
        tag = "dev-b"
    elif not candidate and seed != 18:
        tag = "dev-c"
    else:
        tag = "midpoint-dev"
    run = f"p2-{course_slug}-full-{policy_slug}-s{seed}-r01-{tag}"
    path = RUN_ROOT / run / "summary.json"
    if not path.exists():
        if path.parent.exists():
            raise RuntimeError(f"Incomplete run needs diagnosis: {path.parent}")
        cmd = ["pwsh", "-NoProfile", "-File", str(RUNNER), "-Course", course,
               "-Seeds", str(seed), "-Split", "development", "-Repeats", "1", "-Tag", tag]
        if candidate:
            cmd += ["-PolicyFile", str(ROOT / "docs/bot-learning-phase2-candidates/m01.json")]
        print(" ".join(cmd), flush=True)
        subprocess.run(cmd, cwd=ROOT, check=True)
    result = json.loads(path.read_text(encoding="utf-8"))
    if result["course"] != course or result["seed"] != seed or result["encounter"] is not None:
        raise ValueError(f"Wrong run summary: {path}")
    return result


def compact(result):
    return {key: result[key] for key in ("run", "status", "course", "seed", "moduleId",
                                           "policyId", "policyBaseline", "policySha256",
                                           "courseResetSha256", "traceSha256", "gameSeconds", "wallSeconds",
                                           "deaths", "maxObservedAttackers", "routeFailures", "potions",
                                           "retreatAttempts", "decisionRecords")}


def main():
    plan = json.loads(PLAN.read_text(encoding="utf-8"))
    report = {"schemaVersion": 1, "plan": plan, "pairs": [], "stopped": None}
    module_id = None
    for seed in plan["seeds"]:
        for course in plan["courses"]:
            baseline = run_for(course, seed, False)
            candidate = run_for(course, seed, True)
            if (baseline["moduleId"] != candidate["moduleId"] or
                    baseline["courseResetSha256"] != candidate["courseResetSha256"] or
                    module_id is not None and baseline["moduleId"] != module_id):
                raise ValueError(f"Unpaired or changed binary/reset: {course} seed {seed}")
            module_id = baseline["moduleId"]
            report["pairs"].append({"course": course, "seed": seed,
                                    "baseline": compact(baseline), "candidate": compact(candidate)})
            if baseline["status"] == "complete" and candidate["status"] != "complete":
                report["stopped"] = f"New {course} failure on seed {seed}"
            elif candidate["deaths"] > baseline["deaths"]:
                report["stopped"] = f"More {course} deaths on seed {seed}"
            OUTPUT.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
            print(f"{course} seed {seed}: {baseline['status']}/{baseline['deaths']} -> "
                  f"{candidate['status']}/{candidate['deaths']}; game "
                  f"{baseline['gameSeconds']}->{candidate['gameSeconds']}; "
                  f"attackers {baseline['maxObservedAttackers']}->{candidate['maxObservedAttackers']}", flush=True)
            if report["stopped"]:
                print(f"Early stop: {report['stopped']}", flush=True)
                return
    print(f"Midpoint development complete: {OUTPUT}", flush=True)


if __name__ == "__main__":
    main()
