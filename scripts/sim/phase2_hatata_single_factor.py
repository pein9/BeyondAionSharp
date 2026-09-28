#!/usr/bin/env python3
"""Check whether baseline-safe combat knobs affect Hatata with an add."""

import json
from pathlib import Path
import subprocess


ROOT = Path(__file__).resolve().parents[2]
RUN_ROOT = ROOT / "run/bot-learning-phase2/current"
RUNNER = ROOT / "scripts/sim/run-mau-phase2.ps1"
DEST = ROOT / "docs/bot-learning-phase2-candidates"
OUTPUT = ROOT / "docs/bot-learning-phase2-hatata-single-factor.json"
IDENTITIES = ("baseline", "s04", "s05", "s06")


def summary(identity):
    policy_slug = "base" if identity == "baseline" else "cand"
    tag = "hat-sf" if identity == "baseline" else f"{identity}-hat"
    run = f"p2-hat-hatata2-{policy_slug}-s12-r01-{tag}"
    path = RUN_ROOT / run / "summary.json"
    if not path.exists():
        if path.parent.exists():
            raise RuntimeError(f"Incomplete run needs diagnosis: {path.parent}")
        cmd = ["pwsh", "-NoProfile", "-File", str(RUNNER), "-Course", "RaeToHatata",
               "-Encounter", "HatataWithAdd", "-Seeds", "12", "-Split", "development",
               "-Repeats", "1", "-Tag", tag]
        if identity != "baseline":
            cmd += ["-PolicyFile", str(DEST / f"{identity}.json")]
        print(" ".join(cmd), flush=True)
        subprocess.run(cmd, cwd=ROOT, check=True)
    return json.loads(path.read_text(encoding="utf-8"))


def main():
    results = {identity: summary(identity) for identity in IDENTITIES}
    baseline = results["baseline"]
    for identity in IDENTITIES[1:]:
        candidate = results[identity]
        if (baseline["moduleId"] != candidate["moduleId"] or
                baseline["courseResetSha256"] != candidate["courseResetSha256"]):
            raise ValueError(f"Unpaired binary/reset: {identity}")
    keys = ("run", "status", "moduleId", "courseResetSha256", "traceSha256",
            "policyId", "gameSeconds", "deaths", "maxObservedAttackers",
            "potions", "routeFailures", "decisionRecords")
    OUTPUT.write_text(json.dumps({"schemaVersion": 1, "course": "RaeToHatata",
                                  "encounter": "HatataWithAdd", "seed": 12,
                                  "results": {identity: {key: value[key] for key in keys}
                                              for identity, value in results.items()}},
                                 indent=2) + "\n", encoding="utf-8")
    for identity, result in results.items():
        print(f"{identity}: {result['status']}; deaths={result['deaths']}; "
              f"attackers={result['maxObservedAttackers']}; "
              f"game={result['gameSeconds']}; potions={result['potions']}", flush=True)


if __name__ == "__main__":
    main()
