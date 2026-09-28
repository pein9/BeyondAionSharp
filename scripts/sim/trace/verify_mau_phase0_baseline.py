#!/usr/bin/env python3
"""Audit the frozen Mau Phase 0 course traces without exposing held-out scores."""

from __future__ import annotations

import hashlib
import json
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
LOCK = ROOT / "docs/bot-learning-phase0-lock.json"
RUNS = ROOT / "run/bot-learning-phase0/current"
REPORT = ROOT / "docs/bot-learning-phase0-baseline.json"
TAG = "locked-v1"
COURSES = {"generator_to_rae": "generator", "rae_to_hatata": "hatata"}


def digest(path: Path, count_lines: bool = False) -> tuple[str, int]:
    sha = hashlib.sha256()
    lines = 0
    with path.open("rb") as source:
        while block := source.read(1024 * 1024):
            sha.update(block)
            if count_lines:
                lines += block.count(b"\n")
    return sha.hexdigest(), lines


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def validate_start(summary: dict, course: dict, run: str) -> None:
    start = summary["startCheckpoint"]
    require(start["MapId"] == course["mapId"], f"{run}: start map")
    require(start["Level"] == course["level"], f"{run}: start level")
    require(not start["IsDead"], f"{run}: dead at start")
    for key, observed in (
        ("hp", "CurrentHp"), ("maxHp", "MaxHp"),
        ("mp", "CurrentMp"), ("maxMp", "MaxMp"),
    ):
        require(start[observed] == course[key], f"{run}: start {key}")
    for axis in ("X", "Y", "Z"):
        require(abs(start["Position"][axis] - course["position"][axis]) < 0.01,
                f"{run}: start {axis}")
    require(start["Position"]["Heading"] == course["position"]["Heading"],
            f"{run}: start heading")
    quest = course["quest"]
    require(any(q["QuestId"] == quest["questId"] and q["Status"] == quest["status"]
                and q["StepAndFlags"] == quest["stepAndFlags"] for q in start["Quests"]),
            f"{run}: start quest")
    skills = {str(skill["SkillId"]): skill["Level"] for skill in start["Skills"]}
    require(skills == course["skills"], f"{run}: learned skills")
    expected_items = Counter()
    expected_gear = Counter()
    for item in course["inventory"]:
        expected_items[item["itemId"]] += item["count"]
        if item["equippedSlot"] and item["equippedSlot"] not in (65535, 65536):
            expected_gear[item["itemId"], item["equippedSlot"]] += item["count"]
    observed_items = Counter()
    observed_gear = Counter()
    for item in start["Inventory"]:
        observed_items[item["ItemId"]] += item["Count"]
        if item["EquipmentSlot"] not in (0, 65535):
            observed_gear[item["ItemId"], item["EquipmentSlot"]] += item["Count"]
    require(observed_items == expected_items, f"{run}: inventory")
    require(observed_gear == expected_gear, f"{run}: equipped gear")


def validate_equipment_packets(trace_path: Path, course: dict, run: str) -> None:
    """The checkpoint's ushort slot truncates 65536; packets retain the full slot."""
    observed_slots: dict[int, int | None] = {}
    reached_start = False
    with trace_path.open(encoding="utf-8") as trace:
        for line in trace:
            event = json.loads(line)
            if event.get("packet") == "phase0-course-start":
                reached_start = True
                break
            if event.get("packet") == "SM_INVENTORY_ADD_ITEM":
                for item in event.get("fields", {}).get("items", []):
                    observed_slots[item["itemId"]] = item.get("details", {}).get("EquippedSlot")
    require(reached_start, f"{run}: missing course start marker")
    expected = {item["itemId"]: item["equippedSlot"] for item in course["inventory"]
                if item["equippedSlot"] not in (None, 0, 65535)}
    observed = {item_id: slot for item_id, slot in observed_slots.items()
                if slot not in (None, 0, 65535) and item_id in expected}
    require(observed == expected, f"{run}: full equipped slots in client packets")


def main() -> None:
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    split = lock["seedSplit"]
    require(split["regression"] == [1, 3, 4, 5], "regression split changed")
    require(split["development"] == list(range(11, 31)), "development split changed")
    require(split["evaluation"] == list(range(101, 121)), "evaluation split changed")
    require(len(set(sum((split[key] for key in split if isinstance(split[key], list)), []))) == 44,
            "seed split overlaps")
    definitions = {course["id"]: course for course in lock["courses"]}
    require(set(definitions) == set(COURSES), "course definitions changed")
    source_traces: dict[str, tuple[str, int]] = {}
    for course in definitions.values():
        source = course["canonicalSource"]
        if source["run"] not in source_traces:
            candidates = list((ROOT / "run/natural-batch" / source["run"]).glob("*.trace.jsonl"))
            require(len(candidates) == 1, f"{source['run']}: canonical trace missing")
            source_traces[source["run"]] = digest(candidates[0], count_lines=True)
        source_sha, source_lines = source_traces[source["run"]]
        require(source_sha == source["traceSha256"] and source_lines >= source["traceLine"],
                f"{source['run']}: canonical source integrity")

    report: dict = {
        "schemaVersion": 1,
        "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
        "phase0LockSha256": digest(LOCK)[0],
        "canonicalSourceTracesVerified": True,
        "tag": TAG,
        "policy": "current deterministic pull planner and NaturalPriestCombatPolicy",
        "evaluationOutcomesSealed": True,
        "build": None,
        "moduleId": None,
        "codeAndDataSha256": None,
        "regression": [],
        "development": [],
        "evaluation": [],
    }
    for split_name in ("regression", "development", "evaluation"):
        for course_id, slug in COURSES.items():
            course = definitions[course_id]
            for seed in split[split_name]:
                run = f"phase0-{slug}-s{seed}-{TAG}"
                directory = RUNS / run
                summary_path = directory / "summary.json"
                trace_path = directory / f"{run}.trace.jsonl"
                require(summary_path.is_file() and trace_path.is_file(), f"{run}: files missing")
                require((directory / "test.log").is_file(), f"{run}: test log missing")
                summary = json.loads(summary_path.read_text(encoding="utf-8"))
                require(summary["run"] == run and summary["seed"] == seed,
                        f"{run}: run identity")
                expected_course = "GeneratorToRae" if slug == "generator" else "RaeToHatata"
                require(summary["course"] == expected_course, f"{run}: course identity")
                require(summary["status"] in ("complete", "failed"), f"{run}: terminal status")
                sha, lines = digest(trace_path, count_lines=True)
                require(sha == summary["traceSha256"] and lines == summary["traceRecords"],
                        f"{run}: trace integrity")
                require(summary["gameSeconds"] <= course["maximumGameSeconds"] + 0.01,
                        f"{run}: game-clock cap")
                require(summary["wallSeconds"] <= course["maximumWallSeconds"],
                        f"{run}: wall-clock cap")
                validate_start(summary, course, run)
                validate_equipment_packets(trace_path, course, run)
                for field in ("build", "moduleId", "codeAndDataSha256"):
                    if report[field] is None:
                        report[field] = summary[field]
                    require(report[field] == summary[field], f"{run}: changed {field}")
                completion = summary["completion"]
                failures = summary["failureFiles"]
                if summary["status"] == "complete":
                    require(completion is not None and not failures, f"{run}: completion package")
                    require(completion["Course"] == expected_course and completion["Seed"] == seed
                            and completion["Disengaged"] and completion["Deaths"] == summary["deaths"],
                            f"{run}: completion receipt")
                    goal_step = 8 if slug == "generator" else 65
                    require(completion["Quest"]["QuestId"] == course["quest"]["questId"]
                            and completion["Quest"]["StepAndFlags"] >= goal_step,
                            f"{run}: quest terminal")
                else:
                    require(completion is None and failures, f"{run}: failure package")
                    require(all(Path(path).is_file() for path in failures),
                            f"{run}: failure file missing")
                record = {
                    "course": course_id,
                    "seed": seed,
                    "run": run,
                    "trace": str(trace_path.relative_to(ROOT)).replace("\\", "/"),
                    "traceSha256": sha,
                    "summarySha256": digest(summary_path)[0],
                }
                if split_name != "evaluation":
                    record.update({key: summary[key] for key in (
                        "status", "gameSeconds", "wallSeconds", "deaths",
                        "maxObservedAttackers", "retreatAttempts", "potions",
                        "clientObservedHpDecreases", "routeFailures", "combatActions",
                    )})
                report[split_name].append(record)

    require(len(report["regression"]) == 8 and len(report["development"]) == 40
            and len(report["evaluation"]) == 40, "wrong course coverage")
    for relative, expected_sha in report["codeAndDataSha256"].items():
        require(digest(ROOT / relative)[0] == expected_sha,
                f"current source/data changed since baseline: {relative}")
    require(report["codeAndDataSha256"]["tests/Aion.Bots/Scenarios/NaturalPriestCombatPolicy.cs"]
            == lock["sourceData"]["deterministicPolicySha256"],
            "baseline policy differs from frozen deterministic policy")
    REPORT.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print("Phase 0 audit: 88/88 focused traces verified; evaluation outcomes sealed.")
    print(f"Baseline manifest: {REPORT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
