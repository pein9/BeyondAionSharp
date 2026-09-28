#!/usr/bin/env python3
"""Compare r05 ablations with the revised-binary Rae-to-Hatata seed-16 baseline."""

import json
from pathlib import Path
import subprocess


ROOT = Path(__file__).resolve().parents[2]
RUN_ROOT = ROOT / "run/bot-learning-phase2/current"
RUNNER = ROOT / "scripts/sim/run-mau-phase2.ps1"
OUTPUT = ROOT / "docs/bot-learning-phase2-local-screen.json"


def load(path):
    return json.loads(path.read_text(encoding="utf-8"))


def main():
    baseline = load(RUN_ROOT / "p2-hat-full-base-s16-r01-patrolfix-v1/summary.json")
    parent = load(RUN_ROOT / "p2-hat-full-cand-s16-r01-patrolfix-v1/summary.json")
    manifest = load(ROOT / "docs/bot-learning-phase2-candidates/local-manifest.json")
    report = {"schemaVersion": 1, "seed": 16, "course": "RaeToHatata",
              "baseline": baseline["run"], "parent": parent["run"], "candidates": []}
    for entry in manifest["candidates"]:
        identity = entry["id"]
        tag = f"{identity}-local"
        run = f"p2-hat-full-cand-s16-r01-{tag}"
        directory = RUN_ROOT / run
        summary_path = directory / "summary.json"
        if not summary_path.exists():
            if directory.exists():
                raise RuntimeError(f"Incomplete run needs diagnosis: {directory}")
            cmd = ["pwsh", "-NoProfile", "-File", str(RUNNER), "-Course", "RaeToHatata",
                   "-Seeds", "16", "-Split", "development", "-Repeats", "1", "-Tag", tag,
                   "-PolicyFile", str(ROOT / "docs/bot-learning-phase2-candidates" / f"{identity}.json")]
            print(" ".join(cmd), flush=True)
            subprocess.run(cmd, cwd=ROOT, check=True)
        summary = load(summary_path)
        if (summary["moduleId"] != baseline["moduleId"] or
                summary["courseResetSha256"] != baseline["courseResetSha256"]):
            raise ValueError(f"Ablation {identity} is not paired with baseline")
        result = {"id": identity, "policySha256": entry["sha256"], "run": run,
                  "status": summary["status"], "deaths": summary["deaths"],
                  "maxObservedAttackers": summary["maxObservedAttackers"],
                  "routeFailures": summary["routeFailures"], "gameSeconds": summary["gameSeconds"],
                  "potions": summary["potions"], "traceSha256": summary["traceSha256"]}
        report["candidates"].append(result)
        OUTPUT.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
        print(f"{identity}: {result['status']}, deaths={result['deaths']}, "
              f"attackers={result['maxObservedAttackers']}, game={result['gameSeconds']}", flush=True)


if __name__ == "__main__":
    main()
