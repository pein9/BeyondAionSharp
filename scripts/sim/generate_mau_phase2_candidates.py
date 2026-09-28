#!/usr/bin/env python3
"""Generate the fixed Phase 2 random-search reference before inspecting outcomes."""

import hashlib
import json
from pathlib import Path
import random


ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / "docs" / "bot-learning-phase2-candidates"
SEARCH_SEED = 20260927
COUNT = 12
BASELINE = {
    "PullDistanceMeters": 22.0,
    "PatrolWaitCycles": 3,
    "HealSinglePercent": 55,
    "HealMultiplePercent": 70,
    "HotPotionPercent": 90,
    "ManaReserveExtra": 0,
    "FinishTargetHpPercent": 15,
    "PreferWoundedWhenTwoAttackers": False,
}
CHOICES = {
    "PullDistanceMeters": [18.0, 19.0, 20.0, 21.0, 22.0],
    "PatrolWaitCycles": [1, 2, 3, 4],
    "HealSinglePercent": [50, 55, 60, 65],
    "HealMultiplePercent": [65, 70, 75, 80],
    "HotPotionPercent": [80, 85, 90, 95],
    "ManaReserveExtra": [0, 8, 16, 24],
    "FinishTargetHpPercent": [0, 10, 15, 20, 25],
    "PreferWoundedWhenTwoAttackers": [False, True],
}


def save(path, payload):
    data = (json.dumps(payload, indent=2) + "\n").encode("utf-8")
    if path.exists() and path.read_bytes() != data:
        raise ValueError(f"Refusing to overwrite changed candidate: {path}")
    path.write_bytes(data)
    return hashlib.sha256(data).hexdigest()


def main():
    rng = random.Random(SEARCH_SEED)
    DEST.mkdir(parents=True, exist_ok=True)
    entries = [{"id": "baseline", "sha256": save(DEST / "baseline.json", BASELINE),
                "parameters": BASELINE}]
    seen = {tuple(BASELINE.values())}
    while len(entries) <= COUNT:
        candidate = {name: rng.choice(choices) for name, choices in CHOICES.items()}
        if candidate["HealMultiplePercent"] < candidate["HealSinglePercent"]:
            continue
        key = tuple(candidate.values())
        if key in seen:
            continue
        seen.add(key)
        identity = f"r{len(entries):02d}"
        entries.append({"id": identity,
                        "sha256": save(DEST / f"{identity}.json", candidate),
                        "parameters": candidate})
    manifest = {"schemaVersion": 1, "searchSeed": SEARCH_SEED, "randomCandidateCount": COUNT,
                "bounds": CHOICES, "candidates": entries,
                "selectionRule": "paired development seeds; completion, deaths, extra attackers and stalls, game time, then potions"}
    save(DEST / "manifest.json", manifest)
    print(f"Frozen {COUNT} random candidates and baseline in {DEST}")


if __name__ == "__main__":
    main()
