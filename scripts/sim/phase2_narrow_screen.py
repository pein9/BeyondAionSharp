#!/usr/bin/env python3
"""Screen narrower threshold changes using first-divergence development traces."""

import hashlib
import json
from pathlib import Path
import subprocess


ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / "docs/bot-learning-phase2-candidates"
RUN_ROOT = ROOT / "run/bot-learning-phase2/current"
RUNNER = ROOT / "scripts/sim/run-mau-phase2.ps1"
OUTPUT = ROOT / "docs/bot-learning-phase2-narrow-screen.json"
BASELINE = json.loads((DEST / "baseline.json").read_text(encoding="utf-8"))
MUTATIONS = {"n01": {"HealSinglePercent": 57}, "n02": {"HotPotionPercent": 92},
             "n03": {"HealSinglePercent": 56}, "n04": {"HotPotionPercent": 91}}
ORDER = (("GeneratorToRae", 14), ("GeneratorToRae", 16),
         ("GeneratorToRae", 17), ("GeneratorToRae", 12),
         ("GeneratorToRae", 11), ("GeneratorToRae", 13),
         ("GeneratorToRae", 15), ("GeneratorToRae", 20),
         ("RaeToHatata", 12), ("RaeToHatata", 16),
         ("RaeToHatata", 20), ("RaeToHatata", 11),
         ("RaeToHatata", 13), ("RaeToHatata", 14),
         ("RaeToHatata", 15))


def baseline_summary(course, seed):
    slug = "gen" if course == "GeneratorToRae" else "hat"
    tag = "dev-c" if seed in (11, 13, 14, 15, 17) else "dev-b"
    if course == "RaeToHatata" and seed == 16:
        tag = "patrolfix-v1"
    path = RUN_ROOT / f"p2-{slug}-full-base-s{seed}-r01-{tag}" / "summary.json"
    return json.loads(path.read_text(encoding="utf-8"))


def candidate_summary(identity, course, seed):
    slug = "gen" if course == "GeneratorToRae" else "hat"
    tag = f"{identity}-ns"
    path = RUN_ROOT / f"p2-{slug}-full-cand-s{seed}-r01-{tag}" / "summary.json"
    if not path.exists():
        if path.parent.exists():
            raise RuntimeError(f"Incomplete run needs diagnosis: {path.parent}")
        cmd = ["pwsh", "-NoProfile", "-File", str(RUNNER), "-Course", course,
               "-Seeds", str(seed), "-Split", "development", "-Repeats", "1",
               "-Tag", tag, "-PolicyFile", str(DEST / f"{identity}.json")]
        print(" ".join(cmd), flush=True)
        subprocess.run(cmd, cwd=ROOT, check=True)
    return json.loads(path.read_text(encoding="utf-8"))


def compact(summary):
    keys = ("run", "status", "moduleId", "policyId", "policyBaseline", "policySha256",
            "courseResetSha256", "traceSha256", "gameSeconds", "wallSeconds", "deaths",
            "maxObservedAttackers", "routeFailures", "potions", "retreatAttempts",
            "decisionRecords")
    return {key: summary[key] for key in keys}


def main():
    report = {"schemaVersion": 1, "method": "Narrow one-coordinate thresholds based on first action divergence in development traces; no evaluation seeds used",
              "firstDivergences": {"s01": "generator seed 14: first added potion at 617/669 HP (92.23%); seed 16 at 635/669 (94.92%); seed 17 at 624/669 (93.27%)",
                                   "s03": "generator seed 14: first added heal at 389/669 HP (58.15%); seed 16 at 375/669 (56.05%); seed 17 at 396/669 (59.19%)",
                                   "n01": "generator seed 12: first added heal at 377/669 HP (56.35%); seed 14 at 374/669 (55.90%)",
                                   "n02": "generator seed 12: first added potion at 615/669 HP (91.93%); seed 14 at 604/669 (90.28%)"},
              "order": [{"course": c, "seed": s} for c, s in ORDER],
              "earlyStopRule": "New failure or more deaths than paired baseline",
              "candidates": []}
    for identity, mutation in MUTATIONS.items():
        policy = dict(BASELINE, **mutation)
        source = DEST / f"{identity}.json"
        data = (json.dumps(policy, indent=2) + "\n").encode("utf-8")
        if source.exists() and source.read_bytes() != data:
            raise ValueError(f"Refusing to overwrite changed candidate: {source}")
        source.write_bytes(data)
        entry = {"id": identity, "mutation": mutation,
                 "policySourceSha256": hashlib.sha256(data).hexdigest(),
                 "pairs": [], "stopped": None}
        for course, seed in ORDER:
            baseline = baseline_summary(course, seed)
            candidate = candidate_summary(identity, course, seed)
            receipt = RUN_ROOT / candidate["run"] / "policy.json"
            if (baseline["moduleId"] != candidate["moduleId"] or
                    baseline["courseResetSha256"] != candidate["courseResetSha256"] or
                    candidate["policyBaseline"] or
                    json.loads(receipt.read_text(encoding="utf-8")) != policy):
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
            print(f"{identity}: complete narrow development screen", flush=True)


if __name__ == "__main__":
    main()
