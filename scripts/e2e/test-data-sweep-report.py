#!/usr/bin/env python3
import copy
import importlib.util
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

spec = importlib.util.spec_from_file_location("sweeps", Path(__file__).with_name("report-data-sweep.py"))
sweeps = importlib.util.module_from_spec(spec)
spec.loader.exec_module(sweeps)


class SweepValidationTests(unittest.TestCase):
    def setUp(self):
        self.report = {"schemaVersion": 1, "sweep": "gatherables", "complete": True,
                       "counts": {"Pending": 0, "Failed": 0, "Passed": 1, "Unreachable": 1},
                       "rows": [{"id": "1", "status": "Passed", "evidence": "packet and inventory",
                                 "details": {"mapId": "1", "instanceId": "1", "productId": "2", "productCount": "1"}},
                                {"id": "2", "status": "Unreachable", "evidence": "no shipped spawn"}]}
        self.meta = {"mode": "SIM", "status": "passed", "run": "test", "gitSha": "abc", "seed": 1,
                     "virtualEpoch": "2026-09-16T08:59:00Z", "timeZone": "UTC", "configProfile": "sim-full",
                     "scenarios": ["SWEEP-GATHER"]}

    def check(self):
        return sweeps.validate(self.report, self.meta, {"1", "2"}, {"1"})

    def test_clean_complete_run_is_accepted(self):
        self.assertEqual(len(self.check()), 2)

    def test_failed_parent_run_is_not_accepted_even_if_all_rows_pass(self):
        self.meta["status"] = "failed"
        with self.assertRaises(ValueError): self.check()

    def test_missing_duplicate_and_unknown_rows_fail(self):
        original = copy.deepcopy(self.report)
        for rows in ([original["rows"][0]], [original["rows"][0]] * 2,
                     original["rows"] + [{"id": "3", "status": "Passed"}]):
            self.report["rows"] = rows
            with self.assertRaises(ValueError): self.check()

    def test_pending_failed_and_unknown_statuses_fail(self):
        for status in ("Pending", "Failed", "Skipped"):
            self.report["rows"][0]["status"] = status
            with self.assertRaises(ValueError): self.check()

    def test_spawned_content_cannot_be_called_unreachable(self):
        self.report["rows"][0]["status"] = "Unreachable"
        self.report["counts"].update(Passed=0, Unreachable=2)
        with self.assertRaises(ValueError): self.check()

    def test_missing_product_and_metadata_fail(self):
        self.report["rows"][0]["details"]["productCount"] = "0"
        with self.assertRaises(ValueError): self.check()
        self.report["rows"][0]["details"]["productCount"] = "1"
        del self.meta["virtualEpoch"]
        with self.assertRaises(ValueError): self.check()

    def test_baseline_rejects_lost_coverage_but_accepts_gains(self):
        rows = self.check()
        baseline = {"schemaVersion": 1, "sweep": "gatherables", "rows": copy.deepcopy(rows)}
        sweeps.compare(rows, baseline)
        baseline["rows"][1]["status"] = "Passed"
        with self.assertRaises(ValueError): sweeps.compare(rows, baseline)
        baseline["rows"][0]["status"] = baseline["rows"][1]["status"] = "Unreachable"
        sweeps.compare(rows, baseline)
        baseline["rows"][0]["status"] = "Pending"
        with self.assertRaises(ValueError): sweeps.compare(rows, baseline)

    def test_recipes_require_actual_crafts_and_component_evidence(self):
        self.report["sweep"] = "recipes"
        self.meta["scenarios"] = ["SWEEP-CRAFT"]
        self.report["rows"][0]["details"].update(skillId="40009", components="152000901:1")
        with self.assertRaises(ValueError): self.check()  # No recipe can be silently unreachable.
        self.report["rows"][1] = copy.deepcopy(self.report["rows"][0])
        self.report["rows"][1]["id"] = "2"
        self.report["counts"].update(Passed=2, Unreachable=0)
        rows = self.check()
        sweeps.compare(rows, {"schemaVersion": 1, "sweep": "recipes", "rows": rows}, "recipes")
        del self.report["rows"][1]["details"]["components"]
        with self.assertRaises(ValueError): self.check()

    def test_wrong_scenario_and_baseline_kind_fail(self):
        self.meta["scenarios"] = ["SWEEP-CRAFT"]
        with self.assertRaises(ValueError): self.check()
        with self.assertRaises(ValueError):
            sweeps.compare([], {"schemaVersion": 1, "sweep": "recipes", "rows": []})

    def test_recipe_evidence_matches_shipped_product_components_skill_and_race(self):
        template = ET.fromstring('''<recipe_template id="1" productid="2" quantity="3" skillid="40009" race="ELYOS">
            <comboproduct itemid="4"/><components_data><component itemid="5" quantity="2"/></components_data>
            <components_data><component itemid="6" quantity="1"/></components_data></recipe_template>''')
        details = {"productId": "4", "productCount": "3", "skillId": "40009", "race": "ELYOS", "components": "6:1"}
        report = {"rows": [{"id": "1", "details": details}]}
        sweeps.validate_recipe_evidence(report, [template])
        for field, bad in (("productId", "9"), ("productCount", "1"), ("skillId", "40001"),
                           ("race", "ASMODIANS"), ("components", "6:2")):
            saved = details[field]
            details[field] = bad
            with self.assertRaises(ValueError): sweeps.validate_recipe_evidence(report, [template])
            details[field] = saved

    def test_skill_profession_receipts_require_matching_shipped_sources(self):
        node = ET.fromstring('''<gatherable_template id="1" harvestSkill="30001" skillLevel="1">
            <materials><material itemid="2"/></materials></gatherable_template>''')
        recipe = ET.fromstring('''<recipe_template id="3" skillid="40009" skillpoint="1" race="ELYOS" productid="4" quantity="3" dp="200">
            <components_data><component itemid="5" quantity="1"/></components_data></recipe_template>''')
        for details, mutations in (({"action": "Gather", "gatherableId": "1", "skillId": "30001", "skillLevel": "1", "productId": "2"},
                                     (("gatherableId", "0"), ("skillId", "30003"), ("skillLevel", "0"), ("productId", "4"))),
                                  ({"action": "Craft", "recipeId": "3", "skillId": "40009", "skillLevel": "1", "race": "ELYOS",
                                    "productId": "4", "productCount": "3", "components": "5:1", "class": "GLADIATOR",
                                    "dpBefore": "200", "dpAfter": "0", "dpRule": "recipe-cost"},
                                     (("recipeId", "0"), ("skillId", "30001"), ("skillLevel", "0"), ("race", "ASMODIANS"),
                                      ("productId", "2"), ("productCount", "1"), ("components", "5:2"),
                                      ("dpBefore", "199"), ("dpAfter", "200"), ("dpRule", "starting-class-unchanged")))):
            report = {"rows": [{"id": "test", "details": details}]}
            sweeps.validate_skill_professions(report, [node], [recipe])
            for field, bad in mutations:
                with self.subTest(action=details["action"], field=field):
                    original = details[field]
                    details[field] = bad
                    with self.assertRaises(ValueError): sweeps.validate_skill_professions(report, [node], [recipe])
                    details[field] = original

    def test_starting_class_morphing_preserves_dp_like_java(self):
        recipe = ET.fromstring('''<recipe_template id="3" skillid="40009" skillpoint="1" race="ELYOS" productid="4" quantity="3" dp="200">
            <components_data><component itemid="5" quantity="1"/></components_data></recipe_template>''')
        details = {"action": "Craft", "recipeId": "3", "skillId": "40009", "skillLevel": "1", "race": "ELYOS",
                   "productId": "4", "productCount": "3", "components": "5:1", "class": "WARRIOR",
                   "dpBefore": "4000", "dpAfter": "4000", "dpRule": "starting-class-unchanged"}
        report = {"rows": [{"id": "test", "details": details}]}
        sweeps.validate_skill_professions(report, [], [recipe])
        for field, bad in (("dpAfter", "3800"), ("dpBefore", "0"), ("dpRule", "recipe-cost"), ("class", "GLADIATOR")):
            original = details[field]
            details[field] = bad
            with self.assertRaises(ValueError): sweeps.validate_skill_professions(report, [], [recipe])
            details[field] = original

    def test_bind_report_requires_persisted_binding_death_and_return_to_saved_position(self):
        self.report["sweep"] = "bindpoints"
        self.meta["scenarios"] = ["SWEEP-BIND"]
        details = {"mapId": "1", "instanceId": "1", "npcId": "1", "reviveMapId": "1", "persisted": "true",
                   "bindObserved": "true", "deathObserved": "true", "bindPosition": "1,2,3", "revivePosition": "1,2,3"}
        self.report["rows"][0]["details"] = details
        self.check()
        for field, bad in (("persisted", "false"), ("deathObserved", "false"), ("bindObserved", "false"),
                           ("npcId", "2"), ("reviveMapId", "2"), ("revivePosition", "1,2,30"), ("bindPosition", "nan,2,3")):
            saved = details[field]
            details[field] = bad
            with self.assertRaises(ValueError): self.check()
            details[field] = saved

    def test_teleport_evidence_checks_source_route_destination_and_flight_duration(self):
        destination = ET.fromstring('<teleloc_template loc_id="1" mapid="10" posX="1" posY="2" posZ="3"/>')
        source = ET.fromstring('''<teleporter_template npc_ids="123"><locations>
            <telelocation loc_id="1" type="REGULAR"/>
            <telelocation loc_id="2" type="FLIGHT" teleportid="5001"/></locations></teleporter_template>''')
        routes = sweeps.teleport_routes([source])
        self.assertEqual(set(routes), {"1", "2"})
        details = {"npcId": "123", "mapId": "10", "type": "REGULAR", "teleportId": "0", "position": "1,2,3"}
        report = {"rows": [{"id": "1", "status": "Passed", "details": details}]}
        sweeps.validate_teleport_evidence(report, [destination], routes, [])
        details["position"] = "1,2,30"
        with self.assertRaises(ValueError): sweeps.validate_teleport_evidence(report, [destination], routes, [])
        destination.set("loc_id", "2")
        report["rows"][0]["id"] = "2"
        details.update(type="FLIGHT", teleportId="5001", position="1,2,3", sourceMapId="10", flightMillis="35000")
        path = ET.fromstring('<flypath_location id="5" sworld="10" eworld="10" ex="1" ey="2" ez="3" time="35"/>')
        sweeps.validate_teleport_evidence(report, [destination], routes, [path])
        details["flightMillis"] = "100"
        with self.assertRaises(ValueError): sweeps.validate_teleport_evidence(report, [destination], routes, [path])
        details["flightMillis"] = "35000"
        details["npcId"] = "321"
        with self.assertRaises(ValueError): sweeps.validate_teleport_evidence(report, [destination], routes, [path])

    def test_inactive_catalog_is_not_a_pass_and_requires_missing_shipped_action(self):
        catalog = ET.fromstring('<tradelist_template npc_id="123"><tradelist id="1"/></tradelist_template>')
        npc = ET.fromstring('<npc_template npc_id="123"><talk_info/></npc_template>')
        report = {"rows": [{"id": "tradelist_template:123", "status": "Inactive",
                           "details": {"kind": "tradelist_template", "npcId": "123", "unsupportedAction": "2"}}]}
        sweeps.validate_trade_evidence(report, [catalog], [npc], [], [])
        report["rows"][0]["status"] = "Unreachable"
        with self.assertRaises(ValueError): sweeps.validate_trade_evidence(report, [catalog], [npc], [], [])
        report["rows"][0]["status"] = "Inactive"
        npc.find("talk_info").set("func_dialogs", "2 3")
        with self.assertRaises(ValueError): sweeps.validate_trade_evidence(report, [catalog], [npc], [], [])
        with self.assertRaises(ValueError): sweeps.validate_trade_evidence(report, [catalog], [], [], [])

    def test_trade_evidence_rejects_missing_transactions_wrong_products_and_tokens(self):
        catalogs = [ET.fromstring('<tradelist_template npc_id="123"><tradelist id="1"/></tradelist_template>')]
        npcs = [ET.fromstring('<npc_template npc_id="123"><talk_info func_dialogs="2 3"/></npc_template>')]
        goods = [ET.fromstring('<list id="1"><item id="10"/></list>')]
        items = [ET.fromstring('<item_template id="10"><acquisition type="ABYSS" item="20" count="2"/></item_template>')]
        details = {"kind": "tradelist_template", "npcId": "123", "tabs": "1", "buyObserved": "true", "productId": "10",
                   "productCount": "1", "components": "20:2", "buyKinah": "0", "buyAp": "100", "sellObserved": "true",
                   "soldItemId": "30", "sellKinah": "2", "sellAp": "0"}
        report = {"rows": [{"id": "tradelist_template:123", "status": "Passed", "details": details}]}
        sweeps.validate_trade_evidence(report, catalogs, npcs, goods, items)
        for field, value in (("productId", "11"), ("components", "20:1"), ("buyObserved", "false"), ("sellObserved", "false"), ("tabs", "2")):
            saved = details[field]; details[field] = value
            with self.assertRaises(ValueError): sweeps.validate_trade_evidence(report, catalogs, npcs, goods, items)
            details[field] = saved

    def test_purchase_only_catalog_requires_sale_from_its_purchase_tabs(self):
        catalogs = [ET.fromstring('<purchase_template npc_id="123"><tradelist id="1"/></purchase_template>')]
        npcs = [ET.fromstring('<npc_template npc_id="123"><talk_info func_dialogs="103"/></npc_template>')]
        goods = [ET.fromstring('<purchase_list id="1"><item id="10"/></purchase_list>')]
        details = {"kind": "purchase_template", "npcId": "123", "tabs": "1", "sellObserved": "true", "soldItemId": "10", "sellKinah": "0", "sellAp": "5"}
        report = {"rows": [{"id": "purchase_template:123", "status": "Passed", "details": details}]}
        sweeps.validate_trade_evidence(report, catalogs, npcs, goods, [])
        details["soldItemId"] = "11"
        with self.assertRaises(ValueError): sweeps.validate_trade_evidence(report, catalogs, npcs, goods, [])

    def test_inactive_status_is_restricted_and_cannot_replace_previous_pass(self):
        self.report["rows"][1]["status"] = "Inactive"
        with self.assertRaises(ValueError): self.check()
        self.report["sweep"] = "tradelists"; self.meta["scenarios"] = ["SWEEP-TRADE"]
        self.report["counts"].update(Unreachable=0, Inactive=1)
        rows = self.check()
        baseline = {"schemaVersion": 1, "sweep": "tradelists", "rows": [{"id": "1", "status": "Passed"}, {"id": "2", "status": "Passed"}]}
        with self.assertRaises(ValueError): sweeps.compare(rows, baseline, "tradelists")


class SkillSweepEvidenceTests(unittest.TestCase):
    def test_independent_inventory_covers_all_shipped_class_skill_cases(self):
        root = sweeps.REPO / "game-server/data/static_data"
        cases = sweeps.skill_inventory(
            (row for path in sorted((root / "skill_tree").glob("*.xml")) for row in ET.parse(path).getroot()),
            ET.parse(root / "skills/skill_templates.xml").getroot())
        self.assertEqual(5033, len(cases))
        self.assertEqual(17, len({r["class"] for r in cases.values()}))
        self.assertEqual(34, sum(r["action"] == "Gather" for r in cases.values()))
        self.assertEqual(17, sum(r["action"] == "Craft" for r in cases.values()))

    def test_inventory_inherits_starting_skills_and_preserves_race_and_zero_level(self):
        cases = sweeps.skill_inventory([ET.fromstring('<skill skillId="40" classId="WARRIOR" race="ASMODIANS" minLevel="9"/>')],
                                      [ET.fromstring('<skill_template skill_id="40" activation="PASSIVE"/>')])
        self.assertEqual({"WARRIOR:ASMODIANS:40", "GLADIATOR:ASMODIANS:40", "TEMPLAR:ASMODIANS:40"}, set(cases))
        self.assertTrue(all(c["skillLevel"] == "0" for c in cases.values()))

    def test_learning_alone_cannot_pass_a_cast_passive_or_profession(self):
        for skill_id, activation, evidence in (("1", "ACTIVE", {"castObserved": "true"}),
                                               ("40", "PASSIVE", {"passiveObserved": "true", "effectSkillLevel": "5"}),
                                               ("30001", "NONE", {"actionObserved": "true", "productId": "10", "productCount": "1"}),
                                               ("40009", "NONE", {"actionObserved": "true", "productId": "10", "productCount": "1"})):
            with self.subTest(activation=activation, skill=skill_id):
                cases = sweeps.skill_inventory([ET.fromstring(f'<skill skillId="{skill_id}" classId="GLADIATOR" minLevel="10"/>')],
                                              [ET.fromstring(f'<skill_template skill_id="{skill_id}" activation="{activation}" lvl="5"/>')])
                key = next(iter(cases))
                details = {**cases[key], "learnedObserved": "true", "learnedLevel": "5", "subjectLevel": "65"}
                report = {"rows": [{"id": key, "status": "Passed", "details": details}]}
                with self.assertRaises(ValueError): sweeps.validate_skill_evidence(report, cases)
                details.update(evidence)
                sweeps.validate_skill_evidence(report, cases)
                details["subjectLevel"] = "9"
                with self.assertRaises(ValueError): sweeps.validate_skill_evidence(report, cases)
                details["subjectLevel"] = "65"
                correct_wire = details["packetSkillLevel"]
                details["packetSkillLevel"] = "99"
                with self.assertRaises(ValueError): sweeps.validate_skill_evidence(report, cases)
                details["packetSkillLevel"] = correct_wire
                details["learnedLevel"] = "1"
                with self.assertRaises(ValueError): sweeps.validate_skill_evidence(report, cases)
                details["learnedLevel"] = "5"
                report["rows"][0]["status"] = "Unreachable"
                with self.assertRaises(ValueError): sweeps.validate_skill_evidence(report, cases)

    def test_pet_orders_require_a_compatible_summon_and_its_actual_cast(self):
        templates = [ET.fromstring('<skill_template skill_id="1"><properties first_target="MYPET"/></skill_template>'),
                     ET.fromstring('<skill_template skill_id="2"><effects><summon npc_id="3"/></effects></skill_template>')]
        pets = [ET.fromstring('<pet_skill order_skill="1" pet_id="3" skill_id="4"/>')]
        details = {"skillId": "1", "petNpcId": "3", "setupSummonSkill": "2", "petSkillId": "4", "petCastObserved": "true"}
        report = {"rows": [{"id": "test", "details": details}]}
        sweeps.validate_skill_pet_orders(report, templates, pets)
        for field, bad in (("petNpcId", "9"), ("setupSummonSkill", "9"), ("petSkillId", "9"), ("petCastObserved", "false")):
            with self.subTest(field=field):
                original = details[field]
                details[field] = bad
                with self.assertRaises(ValueError): sweeps.validate_skill_pet_orders(report, templates, pets)
                details[field] = original

    def test_unknown_routes_and_duplicate_inventory_fail(self):
        learn = ET.fromstring('<skill skillId="1" classId="GLADIATOR" minLevel="10"/>')
        template = ET.fromstring('<skill_template skill_id="1" activation="NONE"/>')
        with self.assertRaises(ValueError): sweeps.skill_inventory([learn], [template])
        template.set("activation", "ACTIVE")
        with self.assertRaises(ValueError): sweeps.skill_inventory([learn, learn], [template])


if __name__ == "__main__":
    unittest.main()
