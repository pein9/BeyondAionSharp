#!/usr/bin/env python3
"""Aggregate successful SIM/LIVE quest receipts and enforce the coverage floor."""

from __future__ import annotations

import argparse
import json
import sys
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_CLASSIFIER = REPO_ROOT / "parity-artifacts/e2e/obtainable-quests.json"
DEFAULT_BASELINE = REPO_ROOT / "parity-artifacts/e2e/quest-coverage-baseline.json"


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"{path} must contain a JSON object")
    return value


def zone_race(quest: dict[str, Any]) -> tuple[str, str]:
    return (quest.get("zone") or "<none>", quest.get("race") or "PC_ALL")


def classification_rows(quests: list[dict[str, Any]], availability: str) -> list[dict[str, Any]]:
    counts = Counter(zone_race(quest) for quest in quests if quest["availability"] == availability)
    return [
        {"zone": zone, "race": race, "count": count}
        for (zone, race), count in sorted(counts.items())
    ]


def problem_rows(problems: list[dict[str, Any]]) -> list[dict[str, Any]]:
    grouped: dict[str, list[int]] = defaultdict(list)
    for problem in problems:
        grouped[str(problem["reason"])].append(int(problem["questId"]))
    return [
        {"reason": reason, "count": len(ids), "questIds": sorted(ids)}
        for reason, ids in sorted(grouped.items())
    ]


def load_receipts(run_root: Path, quest_ids: set[int]) -> dict[str, dict[str, dict[str, Any]]]:
    result: dict[str, dict[str, dict[str, Any]]] = defaultdict(dict)
    for path in sorted(run_root.rglob("quest-coverage/*.json")):
        receipt = read_json(path)
        if receipt.get("schemaVersion") != 1:
            raise ValueError(f"unsupported quest coverage receipt schema in {path}")
        mode = str(receipt["mode"]).upper()
        scenario = str(receipt["scenario"]).upper()
        if mode not in {"SIM", "LIVE"}:
            raise ValueError(f"unknown quest coverage mode {mode!r} in {path}")
        if scenario in result[mode]:
            raise ValueError(f"duplicate {mode} {scenario} quest coverage receipts")
        observed = {
            int(quest_id)
            for key in ("acceptedQuestIds", "completedQuestIds")
            for quest_id in receipt[key]
        }
        unknown = observed - quest_ids
        if unknown:
            raise ValueError(f"unknown quest ids in {path}: {sorted(unknown)}")
        receipt["source"] = path.relative_to(run_root).as_posix()
        result[mode][scenario] = receipt
    return result


def build_report(
    run_root: Path,
    classifier: dict[str, Any],
    baseline: dict[str, Any],
) -> tuple[dict[str, Any], list[str]]:
    quests = classifier["quests"]
    by_id = {int(quest["id"]): quest for quest in quests}
    receipts = load_receipts(run_root, set(by_id))
    expected_counts = baseline["classifierCounts"]
    if classifier["counts"] != expected_counts:
        raise ValueError(
            f"quest classifier counts drifted: expected {expected_counts}, got {classifier['counts']}"
        )

    obtainable_groups = sorted(
        {zone_race(quest) for quest in quests if quest["availability"] == "obtainable"}
    )
    errors: list[str] = []
    mode_reports: list[dict[str, Any]] = []
    comparison_modes: list[dict[str, Any]] = []
    for mode in ("SIM", "LIVE"):
        mode_receipts = receipts.get(mode, {})
        accepted = {
            int(quest_id)
            for receipt in mode_receipts.values()
            for quest_id in receipt["acceptedQuestIds"]
        }
        completed = {
            int(quest_id)
            for receipt in mode_receipts.values()
            for quest_id in receipt["completedQuestIds"]
        }
        echo_failures = [
            problem
            for receipt in mode_receipts.values()
            for problem in receipt["echoFailures"]
        ]
        stuck = [
            problem
            for receipt in mode_receipts.values()
            for problem in receipt["stuckReasons"]
        ]
        reference = baseline["referenceByMode"][mode]
        expected_scenarios = set(baseline["expectedScenariosByMode"][mode])
        missing_receipts = sorted(expected_scenarios - set(mode_receipts))
        missing_completed = sorted(set(reference["completedQuestIds"]) - completed)
        missing_accepted = sorted(set(reference["acceptedQuestIds"]) - accepted)
        if missing_receipts:
            errors.append(f"{mode} missing coverage receipts: {', '.join(missing_receipts)}")
        if missing_completed:
            errors.append(
                f"{mode} completed quest coverage dropped by {len(missing_completed)}: "
                + ", ".join(f"Q{quest_id}" for quest_id in missing_completed)
            )

        zones: list[dict[str, Any]] = []
        for zone, race in obtainable_groups:
            obtainable_ids = {
                int(quest["id"])
                for quest in quests
                if quest["availability"] == "obtainable" and zone_race(quest) == (zone, race)
            }
            zone_echo = [problem for problem in echo_failures if int(problem["questId"]) in obtainable_ids]
            zone_stuck = [problem for problem in stuck if int(problem["questId"]) in obtainable_ids]
            zones.append(
                {
                    "zone": zone,
                    "race": race,
                    "obtainable": len(obtainable_ids),
                    "accepted": len(accepted & obtainable_ids),
                    "completed": len(completed & obtainable_ids),
                    "echoFailures": len(zone_echo),
                    "stuckReasons": problem_rows(zone_stuck),
                }
            )

        mode_reports.append(
            {
                "mode": mode,
                "receipts": [mode_receipts[name]["source"] for name in sorted(mode_receipts)],
                "totals": {
                    "accepted": len(accepted),
                    "completed": len(completed),
                    "echoFailures": len(echo_failures),
                    "stuck": len(stuck),
                },
                "acceptedQuestIds": sorted(accepted),
                "completedQuestIds": sorted(completed),
                "zones": zones,
            }
        )
        comparison_modes.append(
            {
                "mode": mode,
                "missingReceipts": missing_receipts,
                "missingAcceptedQuestIds": missing_accepted,
                "missingCompletedQuestIds": missing_completed,
            }
        )

    return (
        {
            "schemaVersion": 1,
            "run": run_root.name,
            "classifierCounts": classifier["counts"],
            "modes": mode_reports,
            "excluded": {
                "noHandler": {
                    "total": classifier["counts"]["no_handler"],
                    "byZoneRace": classification_rows(quests, "no_handler"),
                },
                "unreachable": {
                    "total": classifier["counts"]["unreachable"],
                    "byZoneRace": classification_rows(quests, "unreachable"),
                },
            },
            "baselineComparison": {
                "passed": not errors,
                "modes": comparison_modes,
            },
        },
        errors,
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--classifier", type=Path, default=DEFAULT_CLASSIFIER)
    parser.add_argument("--baseline", type=Path, default=DEFAULT_BASELINE)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        report, errors = build_report(
            args.run_root.resolve(),
            read_json(args.classifier.resolve()),
            read_json(args.baseline.resolve()),
        )
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
        for error in errors:
            print(error, file=sys.stderr)
        if errors:
            return 1
        totals = ", ".join(
            f"{mode['mode']} {mode['totals']['completed']} completed"
            for mode in report["modes"]
        )
        print(f"Quest coverage passed: {totals}. Report: {args.output}")
        return 0
    except (OSError, ValueError, KeyError, TypeError, json.JSONDecodeError) as error:
        print(f"quest coverage report failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
