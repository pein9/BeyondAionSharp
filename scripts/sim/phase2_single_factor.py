#!/usr/bin/env python3
"""Screen single-coordinate baseline mutations on difficult development generator seeds."""

import hashlib
import json
from pathlib import Path
import subprocess


ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / "docs/bot-learning-phase2-candidates"
BASELINE = json.loads((DEST / "baseline.json").read_text(encoding="utf-8"))
RUN_ROOT = ROOT / "run/bot-learning-phase2/current"
RUNNER = ROOT / "scripts/sim/run-mau-phase2.ps1"
OUTPUT = ROOT / "docs/bot-learning-phase2-single-factor.json"
MUTATIONS = {
    "s01": {"HotPotionPercent": 95},
    "s02": {"HealMultiplePercent": 75},
    "s03": {"HealSinglePercent": 60},
    "s04": {"ManaReserveExtra": 8},
    "s05": {"FinishTargetHpPercent": 10},
    "s06": {"PreferWoundedWhenTwoAttackers": True},
}
SEEDS = (16, 17, 12)


def baseline_summary(seed):
    tag = "dev-b" if seed in (16, 12) else "dev-c"
    path = RUN_ROOT / f"p2-gen-full-base-s{seed}-r01-{tag}" / "summary.json"
    return json.loads(path.read_text(encoding="utf-8"))


def candidate_summary(identity, seed):
    tag = f"{identity}-sf"
    run = f"p2-gen-full-cand-s{seed}-r01-{tag}"
    path = RUN_ROOT / run / "summary.json"
    if not path.exists():
        if path.parent.exists():
            raise RuntimeError(f"Incomplete run needs diagnosis: {path.parent}")
        cmd = ["pwsh", "-NoProfile", "-File", str(RUNNER), "-Course", "GeneratorToRae",
               "-Seeds", str(seed), "-Split", "development", "-Repeats", "1", "-Tag", tag,
               "-PolicyFile", str(DEST / f"{identity}.json")]
        print(" ".join(cmd), flush=True)
        subprocess.run(cmd, cwd=ROOT, check=True)
    return json.loads(path.read_text(encoding="utf-8"))


def main():
    report = {"schemaVersion": 1, "parent": "baseline", "orderedSeeds": SEEDS,
              "rule": "Stop after a new failure or more deaths than paired baseline; evaluate one parameter at a time",
              "candidates": []}
    for identity, mutation in MUTATIONS.items():
        policy = dict(BASELINE, **mutation)
        path = DEST / f"{identity}.json"
        data = (json.dumps(policy, indent=2) + "\n").encode("utf-8")
        if path.exists() and path.read_bytes() != data:
            raise ValueError(f"Refusing to overwrite changed candidate: {path}")
        path.write_bytes(data)
        entry = {"id": identity, "mutation": mutation, "policySha256": hashlib.sha256(data).hexdigest(),
                 "pairs": [], "stopped": None}
        for seed in SEEDS:
            baseline = baseline_summary(seed)
            candidate = candidate_summary(identity, seed)
            if (candidate["courseResetSha256"] != baseline["courseResetSha256"] or
                    candidate["moduleId"] != baseline["moduleId"]):
                raise ValueError(f"Unpaired binary/reset: {identity} seed {seed}")
            entry["pairs"].append({"seed": seed, "baseline": {key: baseline[key] for key in
                                   ("run", "status", "deaths", "maxObservedAttackers", "gameSeconds",
                                    "routeFailures", "potions", "traceSha256")},
                                   "candidate": {key: candidate[key] for key in
                                   ("run", "status", "deaths", "maxObservedAttackers", "gameSeconds",
                                    "routeFailures", "potions", "traceSha256")}})
            if baseline["status"] == "complete" and candidate["status"] != "complete":
                entry["stopped"] = f"New course failure on seed {seed}"
            elif candidate["deaths"] > baseline["deaths"]:
                entry["stopped"] = f"More deaths on seed {seed}"
            print(f"{identity} seed {seed}: {baseline['status']}/{baseline['deaths']} -> "
                  f"{candidate['status']}/{candidate['deaths']}; game "
                  f"{baseline['gameSeconds']}->{candidate['gameSeconds']}", flush=True)
            if entry["stopped"]:
                break
        report["candidates"].append(entry)
        OUTPUT.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
        print(f"{identity}: stopped={entry['stopped']}; pairs={len(entry['pairs'])}", flush=True)


if __name__ == "__main__":
    main()
