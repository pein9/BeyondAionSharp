#!/usr/bin/env python3
"""Pair baseline and attacker-aware 91% potion policy on both Mau legs."""

import hashlib
import json
from pathlib import Path
import subprocess


ROOT = Path(__file__).resolve().parents[2]
RUN_ROOT = ROOT / "run/bot-learning-phase2/current"
RUNNER = ROOT / "scripts/sim/run-mau-phase2.ps1"
POLICY = ROOT / "docs/bot-learning-phase2-candidates/n04.json"
OUTPUT = ROOT / "docs/bot-learning-phase2-threat91-development.json"
TAG = "threat91-v1"
ORDER = (("GeneratorToRae", 15), ("GeneratorToRae", 14),
         ("GeneratorToRae", 12), ("GeneratorToRae", 16),
         ("GeneratorToRae", 17), ("GeneratorToRae", 11),
         ("GeneratorToRae", 13), ("GeneratorToRae", 20),
         ("RaeToHatata", 12), ("RaeToHatata", 16),
         ("RaeToHatata", 20), ("RaeToHatata", 11),
         ("RaeToHatata", 13), ("RaeToHatata", 14),
         ("RaeToHatata", 15))


def get_run(course, seed, candidate):
    slug = "gen" if course == "GeneratorToRae" else "hat"
    policy_slug = "cand" if candidate else "base"
    run = f"p2-{slug}-full-{policy_slug}-s{seed}-r01-{TAG}"
    path = RUN_ROOT / run / "summary.json"
    if not path.exists():
        if path.parent.exists():
            raise RuntimeError(f"Incomplete run needs diagnosis: {path.parent}")
        cmd = ["pwsh", "-NoProfile", "-File", str(RUNNER), "-Course", course,
               "-Seeds", str(seed), "-Split", "development", "-Repeats", "1",
               "-Tag", TAG]
        if candidate:
            cmd += ["-PolicyFile", str(POLICY)]
        print(" ".join(cmd), flush=True)
        subprocess.run(cmd, cwd=ROOT, check=True)
    result = json.loads(path.read_text(encoding="utf-8"))
    if (result["course"], result["seed"], result["encounter"]) != (course, seed, None):
        raise ValueError(f"Wrong course summary: {path}")
    return result


def compact(summary):
    keys = ("run", "status", "moduleId", "policyId", "policyBaseline", "policySha256",
            "courseResetSha256", "traceSha256", "gameSeconds", "wallSeconds", "deaths",
            "maxObservedAttackers", "routeFailures", "potions", "retreatAttempts",
            "decisionRecords")
    return {key: summary[key] for key in keys}


def main():
    policy = json.loads(POLICY.read_text(encoding="utf-8"))
    report = {"schemaVersion": 1, "candidate": "HotPotionPercent=91",
              "codeVariant": TAG,
              "policySourceSha256": hashlib.sha256(POLICY.read_bytes()).hexdigest(),
              "orderedPairs": [{"course": c, "seed": s} for c, s in ORDER],
              "earlyStopRule": "New course failure or more deaths than paired baseline",
              "pairs": [], "stopped": None}
    for course, seed in ORDER:
        baseline = get_run(course, seed, False)
        candidate = get_run(course, seed, True)
        receipt = RUN_ROOT / candidate["run"] / "policy.json"
        if (baseline["moduleId"] != candidate["moduleId"] or
                baseline["courseResetSha256"] != candidate["courseResetSha256"] or
                not baseline["policyBaseline"] or candidate["policyBaseline"] or
                json.loads(receipt.read_text(encoding="utf-8")) != policy):
            raise ValueError(f"Unpaired binary/reset/policy: {course} seed {seed}")
        report["pairs"].append({"course": course, "seed": seed,
                                "baseline": compact(baseline), "candidate": compact(candidate)})
        if baseline["status"] == "complete" and candidate["status"] != "complete":
            report["stopped"] = f"New course failure: {course} seed {seed}"
        elif candidate["deaths"] > baseline["deaths"]:
            report["stopped"] = f"More deaths: {course} seed {seed}"
        OUTPUT.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
        print(f"{course} seed {seed}: {baseline['status']}/{baseline['deaths']} -> "
              f"{candidate['status']}/{candidate['deaths']}; "
              f"game {baseline['gameSeconds']}->{candidate['gameSeconds']}; "
              f"attackers {baseline['maxObservedAttackers']}->{candidate['maxObservedAttackers']}",
              flush=True)
        if report["stopped"]:
            print(f"Early stop: {report['stopped']}", flush=True)
            break
    if not report["stopped"]:
        print(f"Development pairs complete: {OUTPUT}", flush=True)


if __name__ == "__main__":
    main()
