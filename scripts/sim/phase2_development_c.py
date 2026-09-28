#!/usr/bin/env python3
"""Expand paired r05/baseline development coverage before freezing a policy."""

import json
from pathlib import Path
import subprocess


ROOT = Path(__file__).resolve().parents[2]
PLAN = ROOT / "docs/bot-learning-phase2-development-plan-b.json"
OUTPUT = ROOT / "docs/bot-learning-phase2-development-c.json"
RUN_ROOT = ROOT / "run/bot-learning-phase2/current"
RUNNER = ROOT / "scripts/sim/run-mau-phase2.ps1"


def get_run(course, seed, candidate):
    course_slug = "gen" if course == "GeneratorToRae" else "hat"
    policy_slug = "cand" if candidate else "base"
    run = f"p2-{course_slug}-full-{policy_slug}-s{seed}-r01-dev-c"
    path = RUN_ROOT / run / "summary.json"
    if not path.exists():
        if path.parent.exists():
            raise RuntimeError(f"Incomplete run needs diagnosis: {path.parent}")
        cmd = ["pwsh", "-NoProfile", "-File", str(RUNNER), "-Course", course,
               "-Seeds", str(seed), "-Split", "development", "-Repeats", "1", "-Tag", "dev-c"]
        if candidate:
            cmd += ["-PolicyFile", str(ROOT / "docs/bot-learning-phase2-candidates/r05.json")]
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
    earlier = json.loads((ROOT / "docs/bot-learning-phase2-development-b.json").read_text(encoding="utf-8"))
    if earlier["stopped"] or len(earlier["pairs"]) != 6:
        raise ValueError("The initial three-seed development pairs did not pass their early-stop gate")
    report = {"schemaVersion": 1, "candidate": "r05", "seeds": plan["stageBSeeds"],
              "courses": plan["courses"], "pairs": [], "stopped": None}
    for seed in plan["stageBSeeds"]:
        for course in plan["courses"]:
            baseline = get_run(course, seed, False)
            candidate = get_run(course, seed, True)
            if (baseline["moduleId"] != candidate["moduleId"] or
                    baseline["courseResetSha256"] != candidate["courseResetSha256"] or
                    baseline["moduleId"] != earlier["pairs"][0]["baseline"]["moduleId"]):
                raise ValueError(f"Unpaired or changed binary/reset: {course} seed {seed}")
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
    print(f"Development C complete: {OUTPUT}", flush=True)


if __name__ == "__main__":
    main()
