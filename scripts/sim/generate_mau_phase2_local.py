#!/usr/bin/env python3
"""Freeze one-coordinate ablations of random-search leader r05."""

import hashlib
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / "docs/bot-learning-phase2-candidates"
PARENT = DEST / "r05.json"
MUTATIONS = {
    "l01": {"PullDistanceMeters": 22.0},
    "l02": {"PatrolWaitCycles": 3},
    "l03": {"PreferWoundedWhenTwoAttackers": False},
    "l04": {"PullDistanceMeters": 22.0, "PatrolWaitCycles": 3},
}


def main():
    parent = json.loads(PARENT.read_text(encoding="utf-8"))
    entries = []
    for identity, changes in MUTATIONS.items():
        candidate = dict(parent, **changes)
        path = DEST / f"{identity}.json"
        data = (json.dumps(candidate, indent=2) + "\n").encode("utf-8")
        if path.exists() and path.read_bytes() != data:
            raise ValueError(f"Refusing to overwrite changed candidate: {path}")
        path.write_bytes(data)
        entries.append({"id": identity, "parent": "r05", "mutation": changes,
                        "sha256": hashlib.sha256(data).hexdigest()})
    manifest = {"schemaVersion": 1, "parentSha256": hashlib.sha256(PARENT.read_bytes()).hexdigest(),
                "reason": "Ablate r05 pull timing and two-attacker priority after Rae-to-Hatata gained an add",
                "candidates": entries}
    path = DEST / "local-manifest.json"
    data = (json.dumps(manifest, indent=2) + "\n").encode("utf-8")
    if path.exists() and path.read_bytes() != data:
        raise ValueError(f"Refusing to overwrite changed manifest: {path}")
    path.write_bytes(data)
    print(f"Frozen {len(entries)} local ablations of r05")


if __name__ == "__main__":
    main()
