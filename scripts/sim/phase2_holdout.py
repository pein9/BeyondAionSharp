#!/usr/bin/env python3
"""Run all frozen Phase 2 paired evaluation or familiar regression courses."""

import argparse
import hashlib
import json
from pathlib import Path
import subprocess


ROOT = Path(__file__).resolve().parents[2]
RUN_ROOT = ROOT / "run/bot-learning-phase2/current"
RUNNER = ROOT / "scripts/sim/run-mau-phase2.ps1"
FREEZE = ROOT / "docs/bot-learning-phase2-frozen.json"
LOCK = ROOT / "docs/bot-learning-phase0-lock.json"
COURSES = ("GeneratorToRae", "RaeToHatata")


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def compact(summary):
    keys = ("run", "status", "moduleId", "policyId", "policyBaseline", "policySha256",
            "courseResetSha256", "traceSha256", "gameSeconds", "wallSeconds", "deaths",
            "maxObservedAttackers", "routeFailures", "potions", "retreatAttempts",
            "decisionRecords")
    return {key: summary[key] for key in keys}


def get_run(split, tag, course, seed, candidate, policy_file):
    slug = "gen" if course == "GeneratorToRae" else "hat"
    policy_slug = "cand" if candidate else "base"
    name = f"p2-{slug}-full-{policy_slug}-s{seed}-r01-{tag}"
    path = RUN_ROOT / name / "summary.json"
    if not path.exists():
        if path.parent.exists():
            raise RuntimeError(f"Incomplete run needs diagnosis: {path.parent}")
        cmd = ["pwsh", "-NoProfile", "-File", str(RUNNER), "-Course", course,
               "-Seeds", str(seed), "-Split", split, "-Repeats", "1", "-Tag", tag]
        if candidate:
            cmd += ["-PolicyFile", str(policy_file)]
        print(" ".join(cmd), flush=True)
        subprocess.run(cmd, cwd=ROOT, check=True)
    result = json.loads(path.read_text(encoding="utf-8"))
    if (result["course"], result["seed"], result["encounter"]) != (course, seed, None):
        raise ValueError(f"Wrong course summary: {path}")
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--split", choices=("evaluation", "regression"), required=True)
    args = parser.parse_args()
    split = args.split
    tag = "heldout-v1" if split == "evaluation" else "regress-v1"
    output = ROOT / f"docs/bot-learning-phase2-{split}.json"
    freeze = json.loads(FREEZE.read_text(encoding="utf-8"))
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    seeds = lock["seedSplit"][split]
    expected = freeze["evaluationSeeds" if split == "evaluation" else "regressionSeeds"]
    if freeze["status"] != "frozen" or seeds != expected or (split == "evaluation" and len(seeds) < 20):
        raise ValueError("Frozen split is missing, changed, or too small")
    policy_file = ROOT / freeze["policyFile"]
    if sha(policy_file) != freeze["policySha256"]:
        raise ValueError("Frozen policy source changed")
    for name, digest in freeze["sourceHashes"].items():
        if sha(ROOT / name) != digest:
            raise ValueError(f"Frozen source changed: {name}")
    report = {"schemaVersion": 1, "split": split,
              "freezeSha256": sha(FREEZE), "moduleId": freeze["moduleId"],
              "seeds": seeds, "courses": COURSES,
              "pairs": [], "complete": False}
    for seed in seeds:
        for course in COURSES:
            baseline = get_run(split, tag, course, seed, False, policy_file)
            candidate = get_run(split, tag, course, seed, True, policy_file)
            if (baseline["moduleId"] != freeze["moduleId"] or
                    candidate["moduleId"] != freeze["moduleId"] or
                    baseline["courseResetSha256"] != candidate["courseResetSha256"] or
                    not baseline["policyBaseline"] or candidate["policyBaseline"] or
                    candidate["policyId"] != freeze["policyId"] or
                    candidate["policySha256"] != freeze["policyReceiptSha256"]):
                raise ValueError(f"Unpaired frozen module/reset/policy: {course} seed {seed}")
            report["pairs"].append({"course": course, "seed": seed,
                                    "baseline": compact(baseline),
                                    "candidate": compact(candidate)})
            output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
            print(f"{split} {course} seed {seed}: "
                  f"{baseline['status']}/{baseline['deaths']} -> {candidate['status']}/{candidate['deaths']}; "
                  f"game {baseline['gameSeconds']}->{candidate['gameSeconds']}; "
                  f"attackers {baseline['maxObservedAttackers']}->{candidate['maxObservedAttackers']}",
                  flush=True)
    report["complete"] = True
    output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"{split} complete: {output}", flush=True)


if __name__ == "__main__":
    main()
