#!/usr/bin/env python3
"""Expand the two safe single-factor finalists on paired development courses."""

import hashlib
import json
from pathlib import Path
import subprocess


ROOT = Path(__file__).resolve().parents[2]
RUN_ROOT = ROOT / "run/bot-learning-phase2/current"
RUNNER = ROOT / "scripts/sim/run-mau-phase2.ps1"
DEST = ROOT / "docs/bot-learning-phase2-candidates"
OUTPUT = ROOT / "docs/bot-learning-phase2-finalist-development.json"
CANDIDATES = ("s01", "s03")
# Difficult generator cases first; then the full Hatata course. All were reserved
# in the development split before this policy search began.
ORDER = (("GeneratorToRae", 11), ("GeneratorToRae", 13),
         ("GeneratorToRae", 14), ("GeneratorToRae", 15),
         ("GeneratorToRae", 20), ("RaeToHatata", 12),
         ("RaeToHatata", 16), ("RaeToHatata", 20),
         ("RaeToHatata", 11), ("RaeToHatata", 13),
         ("RaeToHatata", 14), ("RaeToHatata", 15))


def path_for(course, seed, identity):
    slug = "gen" if course == "GeneratorToRae" else "hat"
    if identity == "baseline":
        tag = "dev-c" if seed in (11, 13, 14, 15, 17) else "dev-b"
        if course == "RaeToHatata" and seed == 16:
            tag = "patrolfix-v1"
        name = f"p2-{slug}-full-base-s{seed}-r01-{tag}"
    else:
        tag = f"{identity}-fd"
        name = f"p2-{slug}-full-cand-s{seed}-r01-{tag}"
    return RUN_ROOT / name / "summary.json"


def read_or_run(course, seed, identity):
    path = path_for(course, seed, identity)
    if not path.exists():
        if identity == "baseline" or path.parent.exists():
            raise RuntimeError(f"Missing or incomplete paired run: {path}")
        cmd = ["pwsh", "-NoProfile", "-File", str(RUNNER), "-Course", course,
               "-Seeds", str(seed), "-Split", "development", "-Repeats", "1",
               "-Tag", f"{identity}-fd", "-PolicyFile", str(DEST / f"{identity}.json")]
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
    report = {"schemaVersion": 1, "order": [{"course": c, "seed": s} for c, s in ORDER],
              "earlyStopRule": "New failure or more deaths than paired baseline",
              "candidates": []}
    for identity in CANDIDATES:
        source = DEST / f"{identity}.json"
        entry = {"id": identity, "policySha256": hashlib.sha256(source.read_bytes()).hexdigest(),
                 "pairs": [], "stopped": None}
        for course, seed in ORDER:
            baseline = read_or_run(course, seed, "baseline")
            candidate = read_or_run(course, seed, identity)
            policy_receipt = RUN_ROOT / candidate["run"] / "policy.json"
            if (baseline["moduleId"] != candidate["moduleId"] or
                    baseline["courseResetSha256"] != candidate["courseResetSha256"] or
                    candidate["policyBaseline"] or
                    json.loads(policy_receipt.read_text(encoding="utf-8")) !=
                    json.loads(source.read_text(encoding="utf-8"))):
                raise ValueError(f"Unpaired binary/reset/policy: {identity} {course} {seed}")
            entry["pairs"].append({"course": course, "seed": seed,
                                   "baseline": compact(baseline), "candidate": compact(candidate)})
            if baseline["status"] == "complete" and candidate["status"] != "complete":
                entry["stopped"] = f"New course failure: {course} seed {seed}"
            elif candidate["deaths"] > baseline["deaths"]:
                entry["stopped"] = f"More deaths: {course} seed {seed}"
            report["candidates"] = [e for e in report["candidates"] if e["id"] != identity] + [entry]
            OUTPUT.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
            print(f"{identity} {course} {seed}: {baseline['status']}/{baseline['deaths']} -> "
                  f"{candidate['status']}/{candidate['deaths']}; "
                  f"game {baseline['gameSeconds']}->{candidate['gameSeconds']}; "
                  f"attackers {baseline['maxObservedAttackers']}->{candidate['maxObservedAttackers']}",
                  flush=True)
            if entry["stopped"]:
                print(f"Early stop: {entry['stopped']}", flush=True)
                break
        if not entry["stopped"]:
            print(f"{identity}: complete expanded development screen", flush=True)


if __name__ == "__main__":
    main()
