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

extractor_spec = importlib.util.spec_from_file_location("extract_quest_dialog_map", CLIENT_EXTRACTOR)
assert extractor_spec is not None and extractor_spec.loader is not None
extractor = importlib.util.module_from_spec(extractor_spec)
extractor_spec.loader.exec_module(extractor)


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


if __name__ == "__main__":
    unittest.main()
