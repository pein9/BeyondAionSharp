#!/usr/bin/env python3
"""Regression tests for tools/client-extract/retail_quest_inventory.py (RQ-01, D32).

The parsing and join tests run everywhere. The staleness check needs the 4.8 client and is skipped
without it; set AION_CLIENT_ROOT when the client is not at its default path.
"""

from __future__ import annotations

import importlib.util
import json
import os
import subprocess
import sys
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
BUILDER = REPO_ROOT / "tools/client-extract/retail_quest_inventory.py"
INVENTORY = REPO_ROOT / "parity-artifacts/e2e/retail-quest-inventory.json"
SUMMARY = REPO_ROOT / "parity-artifacts/e2e/retail-quest-inventory.md"
CLASSIFIER = REPO_ROOT / "parity-artifacts/e2e/obtainable-quests.json"

spec = importlib.util.spec_from_file_location("retail_quest_inventory", BUILDER)
assert spec is not None and spec.loader is not None
inventory = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = inventory
spec.loader.exec_module(inventory)

CLIENT_QUEST = """<quests><quest><id>24113</id><name>Q24113</name><desc>STR_QUEST_NAME_Q24113</desc>
<category1>quest</category1><category2>STR_QUEST_ZONE06</category2><max_repeat_count>1</max_repeat_count>
<client_level>17</client_level><minlevel_permitted>12</minlevel_permitted><maxlevel_permitted>0</maxlevel_permitted>
<finished_quest_cond1>Q24112</finished_quest_cond1><unfinished_quest_cond1>Q2200</unfinished_quest_cond1>
<unfinished_quest_cond2>Q2017</unfinished_quest_cond2><noacquired_quest_cond1>Q2200</noacquired_quest_cond1>
<noacquired_quest_cond2>Q2017</noacquired_quest_cond2><collect_item1>quest_24113a 1</collect_item1>
<drop_monster_1>LehparWaChD_18_An</drop_monster_1><drop_item_1>quest_24113a</drop_item_1><drop_prob_1>100</drop_prob_1>
<drop_each_member_1>1</drop_each_member_1><class_permitted>warrior</class_permitted><race_permitted>pc_dark</race_permitted>
</quest></quests>"""

MONSTER_CSV = """questId, questProgress, item, sourceType, sourceName, num, monsters_gathers_npcs_list
24113,0,quest_24113a,questItemDropMonster,,1,lehparwachd_18_an
1002,6,quest_1002b,questItemDropMonster,,4,dbrowniemwd_6_an,dbrowniemwd_7_an
"""

SCRIPT_CSV = """questId,progress,item,sourceType,sourceName,num,monsters_gathers_npcs_list
10020,Progress(SECTION_1<10; SECTION_0==3),,killedByUser,,2,LF4_A2_DrakanWorkerFi_52_An,lf4_a2_drakanworkerfi_farm_52_an
"""

SERVER_QUEST = """<quest id="24113" name="Sword to Secrecy" quest_zone="Altgard" minlevel_permitted="12"
race_permitted="ASMODIANS"><collect_items><collect_item item_id="182215473" count="1"/></collect_items>
<quest_drop npc_id="210532" item_id="182215473" drop_each_member="1"/>
<start_conditions><finished quest_id="24112"/><unfinished>2200</unfinished><noacquired>2200</noacquired></start_conditions>
<start_conditions><unfinished>2017</unfinished><noacquired>2017</noacquired></start_conditions></quest>"""


def fixture_world(spawned: bool = True) -> "inventory.World":
    return inventory.World(
        npc_templates={210532: "commander gattban"},
        spawned_ids={210532} if spawned else set(),
        positions={210532: [{"mapId": 220030000}]} if spawned else {},
        dynamic_ids=set(),
        items_by_devname={"quest_24113a": 182215473},
        item_ids={182215473},
    )


def fixture_client() -> dict:
    return {
        "quests": inventory.parse_client_quests(ET.fromstring(CLIENT_QUEST)),
        "rows": inventory.parse_quest_csv(MONSTER_CSV, "quest_monster.csv"),
        "npcs": {"lehparwachd_18_an": {"id": 210532, "desc": "STR_LEHPARWACHD_18_AN"}},
        "strings": {"STR_QUEST_NAME_Q24113": "Tiamat's Sword"},
        "dialogs": {24113},
    }


def fixture_classifier() -> dict:
    return {"quests": [
        {"id": 24112, "zone": "Altgard", "race": "ASMODIANS", "availability": "obtainable"},
        {"id": 24113, "zone": "Altgard", "race": "ASMODIANS", "availability": "no_handler"},
    ]}


class ParsingTests(unittest.TestCase):
    def test_client_quest_fields(self) -> None:
        quest = inventory.parse_client_quests(ET.fromstring(CLIENT_QUEST))[24113]
        self.assertEqual((17, 12, "ASMODIANS"), (quest["clientLevel"], quest["minLevel"], quest["race"]))
        self.assertEqual([24112], quest["finished"])
        self.assertEqual([2200, 2017], quest["unfinished"])
        self.assertEqual([{"devname": "quest_24113a", "count": 1}], quest["collectItems"])
        self.assertEqual([{"monster": "LehparWaChD_18_An", "item": "quest_24113a", "prob": 100, "eachMember": True}],
                         quest["drops"])
        self.assertEqual({"category1": "quest", "category2": "STR_QUEST_ZONE06"}, quest["flags"])

    def test_condition_with_several_quests_stays_a_list(self) -> None:
        self.assertEqual(24112, inventory.quest_ref("Q24112"))
        self.assertEqual([1, 2], inventory.quest_ref("Q1 Q2"))

    def test_csv_rows_keep_every_alternative_and_raw_progress(self) -> None:
        rows = inventory.parse_quest_csv(MONSTER_CSV, "quest_monster.csv")
        self.assertEqual(["dbrowniemwd_6_an", "dbrowniemwd_7_an"], rows[1002][0]["devnames"])
        script = inventory.parse_quest_csv(SCRIPT_CSV, "quest_script_monster.csv")[10020][0]
        self.assertEqual("Progress(SECTION_1<10; SECTION_0==3)", script["progress"])
        self.assertEqual("killedByUser", script["sourceType"])
        self.assertEqual(2, script["num"])

    def test_server_start_conditions_merge_across_blocks(self) -> None:
        facts = inventory.server_facts(ET.fromstring(SERVER_QUEST))
        self.assertEqual([2017, 2200], facts["unfinished"])
        self.assertEqual([(210532, 182215473)], facts["questDrops"])


class JoinTests(unittest.TestCase):
    def build(self, world) -> dict:
        server = {24113: ET.fromstring(SERVER_QUEST)}
        return inventory.build(fixture_client(), fixture_classifier(), server, world, {1}, {})

    def test_ready_quest_joins_npc_item_and_prerequisite(self) -> None:
        row = self.build(fixture_world())["quests"][0]
        self.assertEqual("Tiamat's Sword", row["clientName"])
        self.assertIs(False, row["javaHandler"])
        self.assertEqual(210532, row["npcs"][0]["npcId"])
        self.assertEqual([220030000], row["npcs"][0]["maps"])
        self.assertEqual(182215473, row["items"][0]["itemId"])
        self.assertEqual([{"id": 24112, "availability": "obtainable"}], row["prerequisites"])
        self.assertEqual([], row["serverDiffs"])
        self.assertTrue(row["readiness"]["worldReady"])

    def test_unspawned_drop_source_is_not_ready(self) -> None:
        readiness = self.build(fixture_world(spawned=False))["quests"][0]["readiness"]
        self.assertEqual([210532], readiness["npcsNotSpawned"])
        self.assertEqual(1, readiness["sourceGroupsUnmet"])
        self.assertFalse(readiness["worldReady"])

    def test_disagreeing_server_level_is_reported(self) -> None:
        server = {24113: ET.fromstring(SERVER_QUEST.replace('minlevel_permitted="12"', 'minlevel_permitted="15"'))}
        row = inventory.build(fixture_client(), fixture_classifier(), server, fixture_world(), set(), {})["quests"][0]
        self.assertEqual([{"field": "minLevel", "client": 12, "server": 15}], row["serverDiffs"])

    def test_client_quest_missing_from_quest_data_is_listed_separately(self) -> None:
        client = fixture_client()
        client["quests"][99001] = dict(client["quests"][24113], id=99001)
        document = inventory.build(client, fixture_classifier(), {24113: ET.fromstring(SERVER_QUEST)},
                                   fixture_world(), set(), {})
        self.assertEqual([99001], [q["id"] for q in document["clientOnly"]])


class CheckedInInventoryTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.document = json.loads(INVENTORY.read_text(encoding="utf-8"))
        cls.classifier = json.loads(CLASSIFIER.read_text(encoding="utf-8-sig"))

    def test_covers_exactly_the_classifier_no_handler_set(self) -> None:
        expected = sorted(q["id"] for q in self.classifier["quests"] if q["availability"] == "no_handler")
        self.assertEqual(expected, [q["id"] for q in self.document["quests"]])

    def test_counts_and_summary_are_derived_from_the_rows(self) -> None:
        body = {"quests": self.document["quests"], "clientOnly": self.document["clientOnly"]}
        self.assertEqual(inventory.summarize(body), self.document["counts"])
        self.assertEqual(inventory.markdown(self.document), SUMMARY.read_text(encoding="utf-8"))

    def test_worked_example_q24113(self) -> None:
        row = next(q for q in self.document["quests"] if q["id"] == 24113)
        self.assertEqual(17, row["client"]["clientLevel"])
        self.assertEqual([24112], row["client"]["finished"])
        self.assertEqual([210532], [n["npcId"] for n in row["npcs"]])
        self.assertTrue(row["readiness"]["worldReady"])
        self.assertIs(False, row["javaHandler"])

    @unittest.skipUnless(
        (Path(os.environ.get("AION_CLIENT_ROOT", inventory.DEFAULT_CLIENT)) / "Data/Quest/Quest.pak").exists(),
        "4.8 client not installed",
    )
    def test_checked_in_inventory_is_current(self) -> None:
        result = subprocess.run([sys.executable, str(BUILDER), "--check"], cwd=REPO_ROOT,
                                capture_output=True, text=True)
        self.assertEqual(0, result.returncode, result.stderr)


if __name__ == "__main__":
    unittest.main()
