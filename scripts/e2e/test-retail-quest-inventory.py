#!/usr/bin/env python3
"""Regression tests for tools/client-extract/retail_quest_inventory.py (RQ-01, D32).

The parsing and join tests run everywhere. The staleness check needs the 4.8 client and is skipped
without it; set AION_CLIENT_ROOT when the client is not at its default path.
"""

from __future__ import annotations

import importlib.util
import json
import os
import re
import subprocess
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
BUILDER = REPO_ROOT / "tools/client-extract/retail_quest_inventory.py"
EVIDENCE_READER = REPO_ROOT / "tools/client-extract/retail_quest_evidence.py"
WORKLIST_BUILDER = REPO_ROOT / "tools/client-extract/retail_quest_worklist.py"
INVENTORY = REPO_ROOT / "parity-artifacts/e2e/retail-quest-inventory.json"
SUMMARY = REPO_ROOT / "parity-artifacts/e2e/retail-quest-inventory.md"
CLASSIFIER = REPO_ROOT / "parity-artifacts/e2e/obtainable-quests.json"
IMPLEMENTED = REPO_ROOT / "parity-artifacts/e2e/retail-quest-implemented.json"
PLANS = REPO_ROOT / "parity-artifacts/e2e/retail-quest-plans"
PLAN_COMPILER = REPO_ROOT / "scripts/e2e/compile-quest-plans.py"
SIM_TESTS = REPO_ROOT / "tests/Aion.Simulation.Tests"
SIM_FIXTURE = SIM_TESTS / "SimulationWorldFixture.cs"
D32_ACCOUNTS = range(151, 201)

spec = importlib.util.spec_from_file_location("retail_quest_inventory", BUILDER)
assert spec is not None and spec.loader is not None
inventory = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = inventory
spec.loader.exec_module(inventory)

evidence_spec = importlib.util.spec_from_file_location("retail_quest_evidence", EVIDENCE_READER)
assert evidence_spec is not None and evidence_spec.loader is not None
evidence = importlib.util.module_from_spec(evidence_spec)
evidence_spec.loader.exec_module(evidence)

worklist_spec = importlib.util.spec_from_file_location("retail_quest_worklist", WORKLIST_BUILDER)
assert worklist_spec is not None and worklist_spec.loader is not None
worklist = importlib.util.module_from_spec(worklist_spec)
worklist_spec.loader.exec_module(worklist)

# The shape of an aioncodex /48/ quest page, reduced to the parts the reader uses.
CODEX_PAGE = """<html><body><table><tr><td colspan="2">ID: 24113</td></tr>
<tr><td colspan="2" class="item_title_cell"><span class="item_title" id="item_name"><b>Sword to Secrecy</b></span></td></tr>
<tr><td class="titles_cell"> Quest <br>Type: Quest <br>Category: Altgard <br>Level: 12 <br>Asmodian Only </td></tr>
<tr><td colspan="2"> <hr class="hr_long"> <b>Description</b>:<br>Seize [%dic:STR_DIC_I_QUEST_24113A] and take it to
<a href="/48/npc/203654/" class="qtooltip diclink" data-id="npc--203654">Aurtri</a>. <hr class="hr_long">
<b>Summary</b>:<br>Text <a href="/48/npc/203654/">Aurtri</a> more.<br> <hr class="hr_long">
<span class="bind_type"><b>Full quest's text</b>:</span> Basic Reward<br>
<a href="/48/item/162000044/" class="qtooltip">x</a> <a href="/48/item/188050880/">y</a> </td></tr></table>
<table class="item_grade_1"><tr><td colspan="2" class="align_center"><b>Additional info</b></td></tr>
<tr><td>Quest giver</td><td width="60%"><a href="/48/npc/203654/" class="qtooltip diclink">Aurtri</a></td></tr>
<tr><td>Level</td><td width="60%">12+</td></tr><tr><td>Class</td><td width="60%">Warrior</td></tr>
<tr><td colspan="2"><hr class="hr_long"> <div class="stretch center_text"><b>Quest requirements</b></div> Finished quests:<br>
<a href="/48/quest/24112/">No Laissez-faire</a> <br>Not accepted quests:<br> <a href="/48/quest/2200/">A</a> <br>
<a href="/48/quest/2017/">B</a> </td></tr></table></body></html>"""

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
        self.assertEqual([[24112]], quest["finished"])
        self.assertEqual([[2200], [2017]], quest["unfinished"])
        self.assertEqual([{"devname": "quest_24113a", "count": 1}], quest["collectItems"])
        self.assertEqual([{"monster": "LehparWaChD_18_An", "item": "quest_24113a", "prob": 100, "eachMember": True}],
                         quest["drops"])
        self.assertEqual({"category1": "quest", "category2": "STR_QUEST_ZONE06"}, quest["flags"])

    def test_one_condition_is_an_and_group(self) -> None:
        self.assertEqual([24112], inventory.quest_ref("Q24112"))
        self.assertEqual([2007, 2022, 2041], inventory.quest_ref("Q2007,Q2022,Q2041"))
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
        self.assertEqual("E", row["class"])  # no retail evidence in the fixture
        self.assertEqual("Tiamat's Sword", row["clientName"])
        self.assertIs(False, row["javaHandler"])
        self.assertEqual(210532, row["npcs"][0]["npcId"])
        self.assertEqual([220030000], row["npcs"][0]["maps"])
        self.assertEqual(182215473, row["items"][0]["itemId"])
        self.assertEqual([{"id": 24112, "availability": "obtainable", "clientMinLevel": None, "disabledChain": []}],
                         row["prerequisites"])
        self.assertEqual([], row["excludedBy"])
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


def client_quest(quest_id: int, **fields) -> dict:
    quest = {"id": quest_id, "desc": None, "clientLevel": 10, "minLevel": 10, "maxLevel": 0, "maxRepeat": 1,
             "race": "ASMODIANS", "classPermitted": None, "finished": [], "unfinished": [], "noacquired": [],
             "acquired": [], "collectItems": [], "drops": [], "flags": {"category1": "quest"}}
    quest.update(fields)
    return quest


class ClassRuleTests(unittest.TestCase):
    def test_level_99_anywhere_in_every_group_cuts_the_chain(self) -> None:
        quests = {
            1: client_quest(1, minLevel=99),
            2: client_quest(2, finished=[[1]]),
            3: client_quest(3, finished=[[2, 4]]),
            4: client_quest(4),
            5: client_quest(5, finished=[[2], [4]]),
        }
        self.assertEqual([3, 2, 1], inventory.disabled_chain(3, quests))
        self.assertEqual([], inventory.disabled_chain(5, quests))  # Q4 is an open alternative

    def test_replacement_is_one_way_and_alternatives_are_mutual(self) -> None:
        quests = {
            2017: client_quest(2017),
            24113: client_quest(24113, unfinished=[[2200], [2017]], noacquired=[[2200], [2017]]),
            14260: client_quest(14260, unfinished=[[14261]], noacquired=[[14261]]),
            14261: client_quest(14261, unfinished=[[14260]], noacquired=[[14260]]),
        }
        excluded = inventory.exclusions(quests)
        self.assertEqual([24113], excluded[2017])
        self.assertEqual([14261], excluded[14260])
        self.assertEqual([14260], excluded[14261])

    def rows(self, *specs) -> list:
        rows = []
        for quest_id, zone, category, extra in specs:
            row = {"id": quest_id, "name": f"Q{quest_id}", "clientName": None, "zone": zone, "race": "ASMODIANS",
                   "clientDialog": True, "client": client_quest(quest_id, flags={"category1": category}),
                   "prerequisites": [], "excludedBy": [], "serverDiffs": [], "retailNpcs": [],
                   "retail": {"found": True, "title": f"Q{quest_id}", "description": "Talk with Aurtri."},
                   "readiness": {"npcsWithoutTemplate": [], "npcsWithoutClientId": [], "sourceGroupsUnmet": 0,
                                 "sourcesUnmet": [], "itemsWithoutTemplate": []}}
            for key, value in extra.items():
                if key == "finished":
                    row["client"]["finished"] = value
                else:
                    row[key] = value
            rows.append(row)
        return rows

    def test_campaign_siblings_and_followers_of_a_replaced_mission_are_superseded(self) -> None:
        rows = self.rows(
            (2012, "Altgard", "mission", {"excludedBy": [{"id": 24110, "availability": "no_handler", "mutual": False}]}),
            (2011, "Altgard", "mission", {}),
            (2071, "Reshanta", "mission", {}),
            (4963, "Pandaemonium", "quest", {"finished": [[2011]]}),
            (24110, "Altgard", "important", {}),
        )
        inventory.classify(rows, {}, {})
        classes = {row["id"]: row["class"] for row in rows}
        self.assertEqual({2012: "C", 2011: "C", 2071: "A", 4963: "C", 24110: "A"}, classes)

    def test_test_zone_and_test_names_are_out_of_scope(self) -> None:
        rows = self.rows((9600, "Test zone", "quest", {}), (9612, "Poeta", "quest", {"clientName": "[Test] Talk"}))
        inventory.classify(rows, {}, {})
        self.assertEqual(["D", "D"], [row["class"] for row in rows])

    def test_missing_evidence_is_e_and_missing_spawn_is_b(self) -> None:
        rows = self.rows(
            (1, "Altgard", "quest", {"clientDialog": False}),
            (2, "Altgard", "quest", {"retailNpcs": [{"role": "giver", "npcId": 7, "template": True, "spawned": False}]}),
            (3, "Altgard", "quest", {"retail": {"found": True, "title": "Q3", "description": "Player"}}),
        )
        inventory.classify(rows, {}, {})
        self.assertEqual(["E", "B", "E"], [row["class"] for row in rows])
        self.assertIn("quest giver 7", rows[1]["classReasons"][0])

    def test_one_way_exclusion_of_a_4x_quest_is_a_question(self) -> None:
        rows = self.rows(
            (14251, "Heiron", "important", {"excludedBy": [{"id": 14270, "availability": "no_handler", "mutual": False}]}),
        )
        inventory.classify(rows, {}, {})
        self.assertEqual("E", rows[0]["class"])

    def test_a_shop_or_gather_source_meets_an_item_with_no_spawned_dropper(self) -> None:
        quest = client_quest(1, drops=[{"monster": "Mob_1", "item": "quest_1a", "prob": 100, "eachMember": True}])
        steps = [
            {"sourceType": "questItemDropMonster", "item": "quest_1a", "devnames": ["mob_1", "mob_2"]},
            {"sourceType": "killedByUser", "item": None, "devnames": ["boss_1"]},
            {"sourceType": "itemUseArea", "item": "quest_1b", "devnames": ["usearea_x"]},
        ]
        npcs = {"mob_1": {"spawned": False}, "mob_2": {"spawned": False}, "boss_1": {"spawned": False}}
        self.assertEqual([["boss_1"], ["mob_1", "mob_2"]], inventory.unmet_sources(quest, steps, npcs))
        steps.append({"sourceType": "goodsList", "item": "quest_1a", "devnames": ["shop"]})
        self.assertEqual([["boss_1"]], inventory.unmet_sources(quest, steps, npcs))

    def test_reviewed_override_wins_and_keeps_the_rule_verdict(self) -> None:
        rows = self.rows((2071, "Reshanta", "mission", {}))
        inventory.classify(rows, {}, {"2071": {"class": "E", "reason": "ask the maintainer"}})
        self.assertEqual("E", rows[0]["class"])
        self.assertEqual("reviewed: ask the maintainer", rows[0]["classReasons"][0])
        self.assertTrue(rows[0]["classReasons"][1].startswith("rules said A"))


    def test_reviewed_live_override_lets_the_b_rules_decide(self) -> None:
        rows = self.rows(
            (14251, "Heiron", "important", {"excludedBy": [{"id": 14270, "availability": "no_handler", "mutual": False}],
                                             "retailNpcs": [{"role": "giver", "npcId": 7, "template": True, "spawned": False}]}),
        )
        inventory.classify(rows, {}, {"14251": {"class": "live", "reason": "retail 5.8 still runs it"}})
        self.assertEqual(("E", "B"), (rows[0]["ruleClass"], rows[0]["class"]))
        self.assertIn("quest giver 7", rows[0]["classReasons"][1])


class WorklistTests(unittest.TestCase):
    def row(self, quest_id, cls, rule_cls, zone="Altgard"):
        return {"id": quest_id, "name": f"Quest {quest_id}", "clientName": None, "zone": zone, "race": "ASMODIANS",
                "class": cls, "ruleClass": rule_cls, "clientDialog": True,
                "classReasons": ["reviewed: gone in 4.8"] if cls == "C" else ["live"],
                "client": {"minLevel": 12, "flags": {"category1": "quest"}}, "retailNpcs": [],
                "retail": {"found": True, "url": evidence.URL.format(quest_id)}}

    def test_statuses_survive_and_rejections_start_rejected(self) -> None:
        document = {"quests": [self.row(1, "A", "A"), self.row(2, "A", "A"), self.row(3, "C", "B"), self.row(4, "C", "C")]}
        crosscheck = {"quests": {}}
        first = worklist.render(document, crosscheck, {})
        self.assertIn("| Rejected | Q3 |", first)
        self.assertNotIn("Q4 |", first)  # the rules already excluded it; it was never a candidate
        edited = first.replace("|  | Q2 |", "| Done | Q2 |")
        self.assertEqual({2: "Done", 3: "Rejected"}, worklist.existing_statuses(edited))
        again = worklist.render(document, crosscheck, worklist.existing_statuses(edited))
        self.assertIn("| Done | Q2 |", again)
        self.assertIn("https://aioncodex.com/48/quest/1/?sl=1", again)

    def test_batches_follow_the_approved_order(self) -> None:
        self.assertEqual("1 Altgard pilot", worklist.batch_of("Altgard"))
        self.assertEqual("2 Asmodian path", worklist.batch_of("Beluslan"))
        self.assertEqual("4 Instance entry", worklist.batch_of("Fire Temple"))
        self.assertEqual("6 Level cap", worklist.batch_of("Inggison"))

    def test_checked_in_worklist_is_current(self) -> None:
        result = subprocess.run([sys.executable, str(WORKLIST_BUILDER), "--check"], cwd=REPO_ROOT,
                                capture_output=True, text=True)
        self.assertEqual(0, result.returncode, result.stderr)


class EvidenceReaderTests(unittest.TestCase):
    def test_codex_page_facts(self) -> None:
        row = evidence.parse(24113, 200, CODEX_PAGE)
        self.assertTrue(row["found"])
        self.assertEqual("https://aioncodex.com/48/quest/24113/?sl=1", row["url"])
        self.assertEqual(("Sword to Secrecy", "Altgard", "12"), (row["title"], row["category"], row["level"]))
        self.assertEqual([203654], row["questGivers"])
        self.assertEqual([{"kind": "npc", "id": 203654, "name": "Aurtri"}], row["descriptionLinks"])
        self.assertIn("[STR_DIC_I_QUEST_24113A]", row["description"])
        self.assertEqual({"Finished quests": [24112], "Not accepted quests": [2200, 2017]}, row["requirements"])
        self.assertEqual([162000044, 188050880], row["rewardItems"])
        self.assertNotIn("Class", row["info"])
        self.assertTrue(row["hasDialogText"])

    def test_page_for_another_id_or_an_error_is_not_found(self) -> None:
        self.assertFalse(evidence.parse(24114, 200, CODEX_PAGE)["found"])
        self.assertEqual({"url": evidence.URL.format(1), "status": 404}, evidence.parse(1, 404, None))


class RegisterTests(unittest.TestCase):
    """The D32 register drives the SIM theory `RetailQuestPlaysEndToEnd`; these keep it and its plans honest."""

    @classmethod
    def setUpClass(cls) -> None:
        cls.register = json.loads(IMPLEMENTED.read_text(encoding="utf-8"))["quests"]

    def test_every_entry_has_a_plan_a_patch_and_its_own_fixture_account(self) -> None:
        accounts = [entry["simAccount"] for entry in self.register.values()]
        self.assertEqual(len(accounts), len(set(accounts)), "two D32 quests share a SIM account")
        for quest_id, entry in self.register.items():
            self.assertTrue((PLANS / f"{quest_id}.json").exists(), quest_id)
            self.assertTrue((REPO_ROOT / entry["patch"]).exists(), entry["patch"])
            self.assertIn(entry["simAccount"], D32_ACCOUNTS, f"{quest_id}: D32 accounts are 151-200")
        self.assertEqual(sorted(self.register), sorted(path.stem for path in PLANS.glob("*.json")))

    def test_the_fixture_accepts_the_d32_accounts_and_no_other_sim_test_uses_them(self) -> None:
        # A SIM test needs its account fresh, and RetailQuestPlaysEndToEnd shares a process with the scenarios, so a
        # clash fails whichever runs second ("Fresh simulation account sim-player-N already has a character").
        line = next(line for line in SIM_FIXTURE.read_text(encoding="utf-8").splitlines() if "var accounts =" in line)
        accepted = {account for start, count in re.findall(r"Enumerable\.Range\((\d+), (\d+)\)", line)
                    for account in range(int(start), int(start) + int(count))}
        self.assertTrue(set(D32_ACCOUNTS) <= accepted, "SimulationWorldFixture must accept accounts 151-200")
        used = sim_test_accounts()
        # The scan must still see each way the scenarios name an account, or an empty result would pass.
        for account in (16, 27, 41, 42, 47, 77, 91, 101, 117, 133):
            self.assertIn(account, used, f"the account scan no longer finds sim-player-{account}")
        self.assertEqual({}, {account: where for account, where in used.items() if account in D32_ACCOUNTS},
                         "accounts 151-200 belong to the D32 register")


def sim_test_accounts() -> dict[int, list[str]]:
    """The fixture accounts other SIM tests name: literals on a line about a session or an account, or right before a
    character name. Accounts computed from a literal (101 + index, account + 1) are found by their literal."""
    found: dict[int, list[str]] = {}
    for path in sorted(SIM_TESTS.glob("*.cs")):
        if path.name == "SimulationRetailQuestTests.cs":
            continue
        for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            about_accounts = re.search(r"SimulationL0Session|EnterCombatWorldAsync|sim-player-|account", line, re.I)
            for match in re.finditer(r"(?<![\w.])(\d+)(?![\w.])(?=(, \"[A-Z])?)", line):
                if about_accounts or match.group(2):
                    found.setdefault(int(match.group(1)), []).append(f"{path.name}:{number}")
    return found

    def test_every_registered_quest_now_has_a_handler(self) -> None:
        classifier = {q["id"]: q for q in json.loads(CLASSIFIER.read_text(encoding="utf-8-sig"))["quests"]}
        for quest_id in self.register:
            self.assertNotEqual("no_handler", classifier[int(quest_id)]["availability"], quest_id)
            self.assertNotEqual("none", classifier[int(quest_id)]["handlerKind"], quest_id)

    def test_checked_in_plans_are_current(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            command = [sys.executable, str(PLAN_COMPILER), "--output", temp]
            for quest_id in self.register:
                command += ["--quest", quest_id]
            subprocess.run(command, cwd=REPO_ROOT, check=True, capture_output=True)
            for quest_id in self.register:
                fresh = (Path(temp) / f"{quest_id}.json").read_text(encoding="utf-8")
                self.assertEqual(fresh, (PLANS / f"{quest_id}.json").read_text(encoding="utf-8"), quest_id)


class CheckedInInventoryTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.document = json.loads(INVENTORY.read_text(encoding="utf-8"))
        cls.classifier = json.loads(CLASSIFIER.read_text(encoding="utf-8-sig"))

    def test_covers_exactly_the_classifier_no_handler_set(self) -> None:
        implemented = json.loads((REPO_ROOT / "parity-artifacts/e2e/retail-quest-implemented.json").read_text(
            encoding="utf-8"))["quests"]
        expected = sorted(q["id"] for q in self.classifier["quests"]
                          if q["availability"] == "no_handler" or str(q["id"]) in implemented)
        self.assertEqual(expected, [q["id"] for q in self.document["quests"]])

    def test_counts_and_summary_are_derived_from_the_rows(self) -> None:
        body = {"quests": self.document["quests"], "clientOnly": self.document["clientOnly"]}
        self.assertEqual(inventory.summarize(body), self.document["counts"])
        self.assertEqual(inventory.markdown(self.document), SUMMARY.read_text(encoding="utf-8"))

    def test_worked_example_q24113(self) -> None:
        row = next(q for q in self.document["quests"] if q["id"] == 24113)
        self.assertEqual(17, row["client"]["clientLevel"])
        self.assertEqual([[24112]], row["client"]["finished"])
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
