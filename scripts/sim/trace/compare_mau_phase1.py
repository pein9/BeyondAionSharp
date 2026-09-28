#!/usr/bin/env python3
"""Compare repeated Mau development courses without opening held-out outcomes."""

import argparse
from collections import defaultdict
import hashlib
import json
from pathlib import Path


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def normalized_decisions(path):
    stream = hashlib.sha256()
    for line in path.open(encoding="utf-8"):
        record = json.loads(line)
        record.pop("run")
        stream.update(json.dumps(record, sort_keys=True, separators=(",", ":")).encode("utf-8"))
        stream.update(b"\n")
    return stream.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("run_dirs", nargs="+", type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[3]
    lock = json.loads((root / "docs/bot-learning-phase0-lock.json").read_text())
    development = set(lock["seedSplit"]["development"])
    grouped = defaultdict(list)
    for directory in args.run_dirs:
        summary_path = directory / "summary.json"
        snapshot_path = directory / "course-reset.json"
        summary = json.loads(summary_path.read_text(encoding="utf-8"))
        seed = summary["seed"]
        if seed not in development:
            raise ValueError(f"{directory}: seed {seed} is outside development split")
        if not snapshot_path.exists() or summary.get("courseResetSha256") != sha256(snapshot_path):
            raise ValueError(f"{directory}: reset snapshot missing or hash mismatch")
        snapshot = json.loads(snapshot_path.read_text(encoding="utf-8"))
        if (snapshot["seed"] != seed or snapshot["course"] != summary["course"] or
                snapshot.get("encounter") != summary.get("encounter")):
            raise ValueError(f"{directory}: reset snapshot identifies another course or seed")
        decision_path = directory / "training-decisions.jsonl"
        if summary.get("decisionRecordSha256") is not None and (
                not decision_path.exists() or summary["decisionRecordSha256"] != sha256(decision_path)):
            raise ValueError(f"{directory}: decision record file missing or hash mismatch")
        completion = summary["completion"]
        failure_paths = [Path(path) for path in summary["failureFiles"]]
        failure = json.loads(failure_paths[0].read_text(encoding="utf-8")) if failure_paths else None
        outcome = {
            "completed": summary["status"] == "complete",
            "deaths": summary["deaths"],
            "terminalDisengaged": completion.get("Disengaged") if completion else False,
            "questStepAndFlags": completion["Quest"]["StepAndFlags"] if completion else None,
            "terminalStep": summary["lastStep"] if completion is None else None,
            "failureType": failure.get("ExceptionType") if failure else None,
            "failureReason": failure.get("Message") if failure else None,
        }
        grouped[(summary["course"], summary.get("encounter"), seed)].append({
            "run": summary["run"], "directory": directory.resolve().relative_to(root).as_posix(),
            "moduleId": summary["moduleId"], "codeAndDataSha256": summary["codeAndDataSha256"],
            "traceSha256": summary["traceSha256"], "resetSha256": summary["courseResetSha256"],
            "rng": snapshot["rng"], "npcCount": len(snapshot["npcs"]),
            "armedTimerCount": len(snapshot["armedTimers"]), "outcome": outcome,
            "gameSeconds": summary["gameSeconds"], "wallSeconds": summary["wallSeconds"],
            "maxObservedAttackers": summary["maxObservedAttackers"],
            "potions": summary["potions"], "routeFailures": summary["routeFailures"],
            "decisionRecords": summary.get("decisionRecords"),
            "normalizedDecisionStreamSha256": normalized_decisions(decision_path) if decision_path.exists() else None,
        })
    groups = []
    for (course, encounter, seed), runs in sorted(grouped.items(),
                                                  key=lambda item: (item[0][0], item[0][1] or "", item[0][2])):
        fields = ("moduleId", "codeAndDataSha256", "resetSha256", "outcome",
                  "gameSeconds", "maxObservedAttackers", "potions", "routeFailures", "decisionRecords",
                  "normalizedDecisionStreamSha256")
        differences = [field for field in fields if any(run[field] != runs[0][field] for run in runs[1:])]
        if any(run["rng"] != {"seed": seed, "drawCount": 0} for run in runs):
            differences.append("rngReset")
        groups.append({"course": course, "encounter": encounter, "seed": seed, "repeatCount": len(runs),
                       "sameResetAndOutcome": len(runs) >= 2 and not differences,
                       "differences": differences, "runs": runs})
    result = {"schemaVersion": 1, "status": "pass" if groups and all(
        group["sameResetAndOutcome"] for group in groups) else "fail", "groups": groups}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    for group in groups:
        print(f"{group['course']}/{group['encounter'] or 'full'} seed {group['seed']}: {group['repeatCount']} repeats, "
              f"same reset/outcome={group['sameResetAndOutcome']}, differences={group['differences']}")
    print(f"Comparison: {result['status']} ({args.output})")
    return 0 if result["status"] == "pass" else 1


if __name__ == "__main__":
    raise SystemExit(main())
