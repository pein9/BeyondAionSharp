#!/usr/bin/env python3
"""Regression tests for compile-quest-plans.py using the checked-in data."""

from __future__ import annotations

import json
import importlib.util
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
COMPILER = REPO_ROOT / "scripts/e2e/compile-quest-plans.py"
CLASSIFIER = REPO_ROOT / "parity-artifacts/e2e/obtainable-quests.json"
CLIENT_MAP = REPO_ROOT / "parity-artifacts/e2e/custom-quest-client-dialogs.json"
CLIENT_EXTRACTOR = REPO_ROOT / "tools/client-extract/extract_quest_dialog_map.py"
COVERAGE_REPORTER = REPO_ROOT / "scripts/e2e/report-quest-coverage.py"
COVERAGE_BASELINE = REPO_ROOT / "parity-artifacts/e2e/quest-coverage-baseline.json"

extractor_spec = importlib.util.spec_from_file_location("extract_quest_dialog_map", CLIENT_EXTRACTOR)
assert extractor_spec is not None and extractor_spec.loader is not None
extractor = importlib.util.module_from_spec(extractor_spec)
extractor_spec.loader.exec_module(extractor)

coverage_spec = importlib.util.spec_from_file_location("report_quest_coverage", COVERAGE_REPORTER)
assert coverage_spec is not None and coverage_spec.loader is not None
coverage = importlib.util.module_from_spec(coverage_spec)
coverage_spec.loader.exec_module(coverage)


class QuestPlanCompilerTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.temp = tempfile.TemporaryDirectory()
        cls.output = Path(cls.temp.name)
        command = [
            sys.executable,
            str(COMPILER),
            "--output",
            str(cls.output),
            "--check-classifier",
            "--check-runnable",
        ]
        for quest_id in (1100, 1101, 1102, 1103, 1145, 2101):
            command.extend(("--quest", str(quest_id)))
        subprocess.run(command, cwd=REPO_ROOT, check=True)

    @classmethod
    def tearDownClass(cls) -> None:
        cls.temp.cleanup()

    def plan(self, quest_id: int) -> dict:
        return json.loads((self.output / f"{quest_id}.json").read_text(encoding="utf-8"))

    def test_report_to_has_positioned_start_and_end_npcs(self) -> None:
        plan = self.plan(1101)
        self.assertEqual("report_to", plan["handler"]["template"])
        self.assertEqual([203049], [npc["id"] for npc in plan["startNpcs"]])
        self.assertEqual([203057], [npc["id"] for npc in plan["endNpcs"]])
        self.assertTrue(plan["startNpcs"][0]["positions"])
        self.assertTrue(plan["endNpcs"][0]["positions"])

    def test_monster_hunt_has_kill_step_and_java_default_end(self) -> None:
        plan = self.plan(1102)
        kills = [step for step in plan["steps"] if step["kind"] == "kill"]
        self.assertEqual([210133, 210134], [npc["id"] for npc in kills[0]["npcs"]])
        self.assertEqual(3, kills[0]["count"])
        self.assertEqual([203057], [npc["id"] for npc in plan["endNpcs"]])

    def test_collect_step_resolves_quest_drop_source(self) -> None:
        plan = self.plan(1103)
        collect = next(step for step in plan["steps"] if step["kind"] == "collect")
        self.assertEqual(182200201, collect["item_id"])
        source = next(source for source in collect["sources"] if source["kind"] == "questDrop")
        self.assertEqual(700105, source["npc"]["id"])
        self.assertTrue(source["npc"]["positions"])

    def test_gather_source_and_custom_handler_are_explicit(self) -> None:
        gather_plan = self.plan(1145)
        collect = next(step for step in gather_plan["steps"] if step["kind"] == "collect")
        source = next(source for source in collect["sources"] if source["kind"] == "gatherable")
        self.assertEqual(400001, source["gatherableId"])
        self.assertTrue(source["positions"])
        custom_plan = self.plan(1100)
        self.assertEqual("custom", custom_plan["handler"]["kind"])
        self.assertEqual("custom", custom_plan["startTrigger"]["kind"])

    def test_ishalgen_report_to_has_positioned_npcs(self) -> None:
        plan = self.plan(2101)
        self.assertEqual([203500], [npc["id"] for npc in plan["startNpcs"]])
        self.assertEqual([203504], [npc["id"] for npc in plan["endNpcs"]])
        self.assertTrue(plan["startNpcs"][0]["positions"])
        self.assertTrue(plan["endNpcs"][0]["positions"])

    def test_classifier_keeps_unimplemented_distinct(self) -> None:
        classifier = json.loads(CLASSIFIER.read_text(encoding="utf-8"))
        self.assertEqual(
            {"obtainable": 4315, "disabled": 3008, "unreachable": 280, "no_handler": 440},
            classifier["counts"],
        )
        self.assertEqual(
            {"template": 2964, "template_incomplete": 424, "custom": 927, "none": 2824, "unavailable": 904},
            classifier["plannerCounts"],
        )
        by_id = {quest["id"]: quest for quest in classifier["quests"]}
        self.assertEqual("obtainable", by_id[1101]["availability"])
        self.assertEqual("obtainable", by_id[2101]["availability"])

    def test_checked_in_client_dialog_map_covers_custom_quests(self) -> None:
        dialog_map = json.loads(CLIENT_MAP.read_text(encoding="utf-8"))
        self.assertEqual(
            {
                "expectedCustomQuests": 927,
                "clientQuestFiles": 923,
                "missingClientQuestFiles": 4,
                "pagesWithActions": 7391,
                "actionReferences": 8554,
            },
            dialog_map["counts"],
        )
        self.assertEqual([3219, 3220, 4219, 4220], dialog_map["missingQuestIds"])
        by_id = {quest["questId"]: quest for quest in dialog_map["quests"]}
        select1 = next(page for page in by_id[1100]["pages"] if page["page"] == "select1")
        self.assertEqual([["SELECT_QUEST_REWARD"]], [button["actions"] for button in select1["buttons"]])

    def test_client_dialog_extractor_parses_composite_actions_and_rejects_encoded_input(self) -> None:
        self.assertEqual(
            ["SELECT2_1", "SETPRO3"],
            extractor.action_names("PLAYEMOTION_thanks;HACTION_SELECT2_1;HACTION_SETPRO3"),
        )
        with tempfile.TemporaryDirectory() as directory:
            encoded = Path(directory) / "quest_q42.html"
            encoded.write_bytes(b"\x81not-decoded")
            with self.assertRaisesRegex(ValueError, "0x81-encoded"):
                extractor.decode_xml(encoded)

    def test_quest_coverage_report_groups_modes_and_excludes_unrunnable_populations(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.write_coverage_receipts(root)
            report, errors = coverage.build_report(
                root,
                json.loads(CLASSIFIER.read_text(encoding="utf-8")),
                json.loads(COVERAGE_BASELINE.read_text(encoding="utf-8")),
            )

            self.assertEqual([], errors)
            self.assertTrue(report["baselineComparison"]["passed"])
            self.assertEqual(440, report["excluded"]["noHandler"]["total"])
            self.assertEqual(280, report["excluded"]["unreachable"]["total"])
            for mode in report["modes"]:
                self.assertEqual(61, mode["totals"]["accepted"])
                self.assertEqual(57, mode["totals"]["completed"])
                poeta = next(row for row in mode["zones"] if row["zone"] == "Poeta" and row["race"] == "ELYOS")
                ishalgen = next(row for row in mode["zones"] if row["zone"] == "Ishalgen" and row["race"] == "ASMODIANS")
                verteron = next(row for row in mode["zones"] if row["zone"] == "Verteron" and row["race"] == "ELYOS")
                ascension = next(row for row in mode["zones"] if row["zone"] == "Ascension Quests" and row["race"] == "ELYOS")
                self.assertEqual((29, 27), (poeta["accepted"], poeta["completed"]))
                self.assertEqual((29, 29), (ishalgen["accepted"], ishalgen["completed"]))
                self.assertEqual((2, 0), (verteron["accepted"], verteron["completed"]))
                self.assertEqual((1, 1), (ascension["accepted"], ascension["completed"]))

    def test_quest_coverage_report_rejects_completed_drop(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.write_coverage_receipts(root, omit_completed=2137)
            report, errors = coverage.build_report(
                root,
                json.loads(CLASSIFIER.read_text(encoding="utf-8")),
                json.loads(COVERAGE_BASELINE.read_text(encoding="utf-8")),
            )

            self.assertFalse(report["baselineComparison"]["passed"])
            self.assertEqual(2, len(errors))
            self.assertTrue(all("Q2137" in error for error in errors))

    @staticmethod
    def write_coverage_receipts(root: Path, omit_completed: int | None = None) -> None:
        baseline = json.loads(COVERAGE_BASELINE.read_text(encoding="utf-8"))
        scenario_ids = {
            "Q1": {1000, 1100, 1101, 1102, 1103, 1104, 1105, 1106},
            "Q2": {2000, 2100, 2101, 2102, 2103, 2104, 2105},
            "Q3": {1000, 1006, 1100, 1114, 1123, 1146, 1149},
            "Q4P": {1101, 1102, 1103, 1104, 1105, 1106, 1108, 1109, 1110, 1112, 1113,
                     1115, 1116, 1117, 1118, 1119, 1120, 1121, 1124, 1125, 1126, 1127, 1129, 1206, 1207},
            "Q4I": {2101, 2102, 2103, 2104, 2105, 2107, 2108, 2109, 2110, 2112, 2113,
                     2115, 2116, 2117, 2118, 2119, 2120, 2121, 2124, 2126, 2127, 2128, 2129,
                     2131, 2133, 2134, 2137},
        }
        partial = {1114, 1123, 1146, 1149}
        for mode in ("SIM", "LIVE"):
            accepted_reference = set(baseline["referenceByMode"][mode]["acceptedQuestIds"])
            completed_reference = set(baseline["referenceByMode"][mode]["completedQuestIds"])
            for scenario, ids in scenario_ids.items():
                accepted = sorted(ids & accepted_reference)
                completed = sorted(
                    ((ids - partial) & completed_reference)
                    - ({omit_completed} if omit_completed else set())
                )
                path = root / f"{mode.lower()}-{scenario.lower()}" / "quest-coverage" / f"{mode.lower()}-{scenario.lower()}.json"
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(
                    json.dumps(
                        {
                            "schemaVersion": 1,
                            "mode": mode,
                            "scenario": scenario,
                            "acceptedQuestIds": accepted,
                            "completedQuestIds": completed,
                            "echoFailures": [],
                            "stuckReasons": [],
                        },
                        indent=2,
                    )
                    + "\n",
                    encoding="utf-8",
                )


if __name__ == "__main__":
    unittest.main()
