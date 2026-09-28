#!/usr/bin/env python3
"""Sequential random-search screen on paired development encounter resets."""

import argparse
import json
from pathlib import Path
import subprocess


ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ROOT / "docs/bot-learning-phase2-candidates/manifest.json"
OUTPUT = ROOT / "docs/bot-learning-phase2-screen.json"
RUN_ROOT = ROOT / "run/bot-learning-phase2/current"
RUNNER = ROOT / "scripts/sim/run-mau-phase2.ps1"
ENCOUNTERS = (("TwoAttackerPull", "twoadd"), ("HatataWithAdd", "hatata2"))


def summary_for(candidate_id, encounter, slug):
    tag = "screen-v1" if candidate_id == "baseline" else f"{candidate_id}-screen"
    policy_slug = "base" if candidate_id == "baseline" else "cand"
    run = f"p2-hat-{slug}-{policy_slug}-s12-r01-{tag}"
    directory = RUN_ROOT / run
    summary = directory / "summary.json"
    if not summary.exists():
        if directory.exists():
            raise RuntimeError(f"Incomplete run directory needs diagnosis: {directory}")
        cmd = ["pwsh", "-NoProfile", "-File", str(RUNNER), "-Course", "RaeToHatata",
               "-Encounter", encounter, "-Seeds", "12", "-Split", "development",
               "-Repeats", "1", "-Tag", tag]
        if candidate_id != "baseline":
            cmd += ["-PolicyFile", str(ROOT / "docs/bot-learning-phase2-candidates" / f"{candidate_id}.json")]
        print(" ".join(cmd), flush=True)
        subprocess.run(cmd, cwd=ROOT, check=True)
    result = json.loads(summary.read_text(encoding="utf-8"))
    if result["seed"] != 12 or result["encounter"] != encounter or result["course"] != "RaeToHatata":
        raise ValueError(f"Run summary identifies another benchmark: {summary}")
    return result


def compact(result):
    return {key: result[key] for key in ("run", "status", "seed", "course", "encounter", "moduleId",
                                          "policyId", "policyBaseline", "policySha256",
                                          "courseResetSha256", "traceSha256", "gameSeconds", "wallSeconds",
                                          "deaths", "maxObservedAttackers", "routeFailures", "potions",
                                          "decisionRecords")}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--max-candidates", type=int, default=12)
    args = parser.parse_args()
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    entries = manifest["candidates"][:args.max_candidates + 1]
    if entries[0]["id"] != "baseline":
        raise ValueError("Manifest must start with baseline")
    report = {"schemaVersion": 1, "searchSeed": manifest["searchSeed"],
              "selectionRule": "Two-attacker failure/death stops a candidate before Hatata-with-add; retain the baseline's one-death add outcome as the reference",
              "candidates": []}
    baseline_by_encounter = {}
    for entry in entries:
        candidate_id = entry["id"]
        runs = []
        for encounter, slug in ENCOUNTERS:
            result = summary_for(candidate_id, encounter, slug)
            if candidate_id == "baseline":
                baseline_by_encounter[encounter] = result
            else:
                base = baseline_by_encounter[encounter]
                if (result["moduleId"] != base["moduleId"] or
                        result["courseResetSha256"] != base["courseResetSha256"]):
                    raise ValueError(f"Candidate {candidate_id} does not pair with baseline {encounter}")
            runs.append(compact(result))
            if (candidate_id != "baseline" and encounter == "TwoAttackerPull" and
                    (result["status"] != "complete" or result["deaths"] > 0)):
                break
        gate = (len(runs) == 2 and runs[0]["status"] == "complete" and runs[0]["deaths"] == 0 and
                runs[1]["status"] == "complete" and runs[1]["deaths"] <= baseline_by_encounter["HatataWithAdd"]["deaths"])
        report["candidates"].append({"id": candidate_id, "sourcePolicySha256": entry["sha256"],
                                     "screenGate": gate, "runs": runs})
        OUTPUT.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
        print(f"{candidate_id}: screenGate={gate}; " +
              ", ".join(f"{run['encounter']} {run['status']} deaths={run['deaths']} game={run['gameSeconds']}"
                        for run in runs), flush=True)


if __name__ == "__main__":
    main()
