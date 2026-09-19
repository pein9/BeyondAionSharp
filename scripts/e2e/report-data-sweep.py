#!/usr/bin/env python3
"""Validate an exhaustive runtime sweep before comparing or recording its coverage baseline."""
import argparse
from collections import Counter
import hashlib
import json
import math
from pathlib import Path
import xml.etree.ElementTree as ET

REPO = Path(__file__).resolve().parents[2]
SWEEPS = {
    "gatherables": ("SWEEP-GATHER", "gatherables/gatherable_templates.xml", "gatherable_template"),
    "recipes": ("SWEEP-CRAFT", "recipe/recipe_templates.xml", "recipe_template"),
    "bindpoints": ("SWEEP-BIND", "bind_points/bind_points.xml", "bind_point"),
    "teleporters": ("SWEEP-TELEPORT", "teleport_location.xml", "teleloc_template"),
    "tradelists": ("SWEEP-TRADE", "npc_trade_list.xml", "*"),
    "skills": ("SWEEP-SKILL", "skills/skill_templates.xml", "skill_template"),
}

CLASS_FAMILIES = (
    ("WARRIOR", "GLADIATOR", "TEMPLAR"), ("SCOUT", "ASSASSIN", "RANGER"),
    ("MAGE", "SORCERER", "SPIRIT_MASTER"), ("PRIEST", "CLERIC", "CHANTER"),
    ("ENGINEER", "RIDER", "GUNNER"), ("ARTIST", "BARD"),
)


def skill_inventory(learn_rows, templates):
    """Independent XML inventory, including inherited pre-ascension and global skills."""
    templates = {s.attrib["skill_id"]: s for s in templates}
    classes = [c for family in CLASS_FAMILIES for c in family]
    cases = {}
    for learn in learn_rows:
        skill_id = learn.attrib["skillId"]
        template = templates[skill_id]
        activation = template.attrib["activation"]
        action = {"ACTIVE": "Cast", "TOGGLE": "Cast", "CHARGE": "Cast", "MAINTAIN": "Cast", "PASSIVE": "Passive"}.get(activation)
        if activation == "NONE":
            action = {"30001": "Gather", "30003": "Gather", "40009": "Craft"}.get(skill_id)
        if action is None:
            raise ValueError(f"Skill {skill_id} has no explicit execution route")
        race = learn.attrib.get("race", "PC_ALL")
        race = "ELYOS" if race == "PC_ALL" else race
        if race not in ("ELYOS", "ASMODIANS"):
            raise ValueError(f"Skill {skill_id} has unsupported race {race}")
        minimum = int(learn.attrib["minLevel"])
        declared = learn.attrib.get("classId")
        applicable = classes if declared is None else [declared]
        if declared is not None:
            if declared not in classes:
                raise ValueError(f"Unknown player class {declared}")
            if minimum < 10:
                applicable = next((list(f) for f in CLASS_FAMILIES if f[0] == declared), applicable)
        for player_class in applicable:
            key = f"{player_class}:{race}:{skill_id}"
            if key in cases:
                raise ValueError(f"Duplicate skill case {key}")
            cases[key] = {"class": player_class, "race": race, "skillId": skill_id,
                          "skillLevel": template.attrib.get("lvl", "0"), "minimumLevel": str(minimum),
                          "activation": activation, "action": action,
                          "packetSkillLevel": "1" if int(skill_id) < 30000 and int(learn.attrib.get("stigma", "0")) == 0 else template.attrib.get("lvl", "0")}
    if not cases:
        raise ValueError("Skill inventory is empty")
    return cases


def validate_skill_evidence(report, cases):
    for row in report["rows"]:
        if row["status"] != "Passed":
            raise ValueError("Every skill case requires execution; no inactive/unreachable skill exemptions")
        expected, details = cases[row["id"]], row.get("details", {})
        if any(details.get(key) != value for key, value in expected.items()):
            raise ValueError(f"Skill {row['id']} has incorrect class/race/template evidence")
        if details.get("learnedObserved") != "true" or details.get("learnedLevel") != expected["skillLevel"]:
            raise ValueError(f"Skill {row['id']} lacks observed learning at its shipped level")
        if int(details.get("subjectLevel", 0)) < int(expected["minimumLevel"]):
            raise ValueError(f"Skill {row['id']} used a subject below its source learning level")
        action = expected["action"]
        if action == "Cast" and details.get("castObserved") != "true":
            raise ValueError(f"Skill {row['id']} lacks an actual completed cast")
        if action == "Passive" and (details.get("passiveObserved") != "true" or details.get("effectSkillLevel") != expected["skillLevel"]):
            raise ValueError(f"Skill {row['id']} lacks passive application at its learned level")
        if action in ("Gather", "Craft"):
            if details.get("actionObserved") != "true" or any(int(details.get(key, 0)) <= 0 for key in ("productId", "productCount")):
                raise ValueError(f"Profession {row['id']} lacks a real action/product")


def validate_skill_pet_orders(report, templates, pet_skills):
    templates = {n.attrib["skill_id"]: n for n in templates}
    mappings = {(n.attrib["order_skill"], n.attrib["pet_id"]): n.attrib["skill_id"] for n in pet_skills if "order_skill" in n.attrib}
    orders = {order for order, _ in mappings}
    for row in report["rows"]:
        details = row["details"]
        skill_id = details["skillId"]
        if skill_id in orders:
            expected = mappings.get((skill_id, details.get("petNpcId")))
            if expected is None or details.get("petSkillId") != expected or details.get("petCastObserved") != "true":
                raise ValueError(f"Pet order {row['id']} lacks its mapped spirit's completed cast")
        properties = templates[skill_id].find("properties")
        if skill_id in orders or properties is not None and properties.get("first_target") == "MYPET":
            setup = templates.get(details.get("setupSummonSkill"))
            if setup is None or details.get("petNpcId") not in {n.attrib["npc_id"] for n in setup.findall("effects/summon")}:
                raise ValueError(f"Skill {row['id']} lacks a shipped compatible summon prerequisite")


def validate_skill_professions(report, gatherables, recipes):
    """A profession receipt must match a usable shipped source, not merely name a product."""
    gatherables = {n.attrib["id"]: n for n in gatherables}
    recipes = {n.attrib["id"]: n for n in recipes}
    for row in report["rows"]:
        details = row["details"]
        if details["action"] == "Gather":
            node = gatherables.get(details.get("gatherableId"))
            if node is None or node.attrib["harvestSkill"] != details["skillId"] or int(node.attrib["skillLevel"]) > int(details["skillLevel"]):
                raise ValueError(f"Skill {row['id']} did not use a matching learnable gathering source")
            if details["productId"] not in {m.attrib["itemid"] for m in node.findall("materials/material")}:
                raise ValueError(f"Skill {row['id']} gathered an unshipped product")
        elif details["action"] == "Craft":
            recipe = recipes.get(details.get("recipeId"))
            if recipe is None or int(recipe.attrib["skillpoint"]) > int(details["skillLevel"]):
                raise ValueError(f"Skill {row['id']} did not use a learnable recipe")
            validate_recipe_evidence({"rows": [{"id": recipe.attrib["id"], "details": details}]}, [recipe])
            before, after = int(details.get("dpBefore", -1)), int(details.get("dpAfter", -1))
            cost = int(recipe.attrib.get("dp", 0))
            starting = details.get("class") in {family[0] for family in CLASS_FAMILIES}
            rule = "starting-class-unchanged" if starting else "recipe-cost"
            if before < cost or after != (before if starting else before - cost) or details.get("dpRule") != rule:
                raise ValueError(f"Skill {row['id']} has incorrect source-rule DP consumption")


def validate(report, metadata, expected_ids, spawned_ids):
    sweep = report.get("sweep")
    if report.get("schemaVersion") != 1 or sweep not in SWEEPS:
        raise ValueError("Unsupported sweep report schema/kind")
    if metadata.get("status") != "passed" or metadata.get("mode") != "SIM":
        raise ValueError("A complete, successful SIM run is required, including its log gate and teardown")
    for key in ("run", "gitSha", "seed", "virtualEpoch", "timeZone", "configProfile"):
        if metadata.get(key) is None:
            raise ValueError(f"Run metadata omitted {key}")
    if SWEEPS[sweep][0] not in metadata.get("scenarios", []):
        raise ValueError(f"Run did not execute {SWEEPS[sweep][0]}")
    rows = report.get("rows", [])
    ids = [r["id"] for r in rows]
    if not ids or len(ids) != len(set(ids)) or set(ids) != set(expected_ids):
        raise ValueError("Sweep inventory differs from the complete current source inventory")
    statuses = ("Pending", "Passed", "Unreachable", "Failed") + (("Inactive",) if sweep == "tradelists" else ())
    counts = {s: sum(r["status"] == s for r in rows) for s in statuses}
    if report.get("counts") != counts or not report.get("complete"):
        raise ValueError("Incomplete report or inconsistent counts")
    for row in rows:
        status = row["status"]
        allowed = ("Passed", "Unreachable") + (("Inactive",) if sweep == "tradelists" else ())
        if status not in allowed or not row.get("evidence", "").strip():
            raise ValueError(f"Row {row['id']} lacks an allowed final status or explicit evidence")
        if status == "Unreachable" and row["id"] in spawned_ids:
            raise ValueError(f"Row {row['id']} has a shipped spawn; it cannot be omitted as unreachable")
        if status == "Unreachable" and sweep in ("recipes", "skills"):
            raise ValueError("Recipes and skills require every row to execute; prerequisites are supplied by setup")
        if status == "Passed":
            details = row.get("details", {})
            required = ("mapId", "instanceId") if sweep in ("bindpoints", "teleporters", "tradelists", "skills") else ("mapId", "instanceId", "productId", "productCount")
            if any(int(details.get(key, 0)) <= 0 for key in required):
                raise ValueError(f"Passed row {row['id']} lacks location/product evidence")
            if sweep == "recipes" and (int(details.get("skillId", 0)) <= 0 or not details.get("components")):
                raise ValueError(f"Recipe row {row['id']} lacks skill/component evidence")
            if sweep == "teleporters":
                if details.get("type") not in ("REGULAR", "FLIGHT") or int(details.get("npcId", 0)) <= 0 or int(details.get("fee", -1)) < 0:
                    raise ValueError(f"Destination {row['id']} lacks type, route NPC or fee evidence")
                if details["type"] == "FLIGHT" and int(details.get("flightMillis", 0)) <= 0:
                    raise ValueError(f"Flight destination {row['id']} lacks duration evidence")
            if sweep == "bindpoints":
                if any(details.get(key) != "true" for key in ("persisted", "deathObserved", "bindObserved")):
                    raise ValueError(f"Bind point {row['id']} lacks binding, persistence or death evidence")
                if details.get("npcId") != row["id"] or details.get("reviveMapId") != details["mapId"]:
                    raise ValueError(f"Bind point {row['id']} has incorrect NPC/revive map evidence")
                bound = [float(v) for v in details.get("bindPosition", "").split(",")]
                revived = [float(v) for v in details.get("revivePosition", "").split(",")]
                if len(bound) != 3 or len(revived) != 3 or not all(math.isfinite(v) for v in bound + revived) or math.dist(bound, revived) > 1.5:
                    raise ValueError(f"Bind point {row['id']} did not revive at its bound position")
    return [{"id": r["id"], "status": r["status"]} for r in sorted(rows, key=lambda r: r["id"])]


def compare(rows, baseline, sweep="gatherables"):
    if baseline.get("schemaVersion") != 1 or baseline.get("sweep") != sweep or sweep not in SWEEPS:
        raise ValueError("Unsupported baseline schema/kind")
    old = {r["id"]: r["status"] for r in baseline["rows"]}
    current = {r["id"]: r["status"] for r in rows}
    if len(old) != len(baseline["rows"]) or set(old) != set(current):
        raise ValueError("Baseline inventory changed; review additions/removals explicitly")
    allowed = ("Passed", "Unreachable") + (("Inactive",) if sweep == "tradelists" else ())
    if any(status not in allowed for status in old.values()):
        raise ValueError("Baseline contains incomplete or unknown statuses")
    regressed = [key for key in old if old[key] == "Passed" and current[key] != "Passed"]
    if regressed:
        raise ValueError("Previously passed rows lost transaction/action coverage: " + ",".join(regressed))


def validate_recipe_evidence(report, templates):
    """Independently verify runtime products and selected components against the shipped XML."""
    by_id = {node.attrib["id"]: node for node in templates}
    for row in report["rows"]:
        recipe = by_id[row["id"]]
        details = row["details"]
        products = {recipe.attrib["productid"]} | {p.attrib["itemid"] for p in recipe.findall("comboproduct")}
        if details["productId"] not in products or int(details["productCount"]) != int(recipe.attrib["quantity"]):
            raise ValueError(f"Recipe {row['id']} produced an unshipped item or incorrect quantity")
        if details["skillId"] != recipe.attrib["skillid"]:
            raise ValueError(f"Recipe {row['id']} used the wrong skill")
        if recipe.attrib["race"] != "PC_ALL" and details.get("race") != recipe.attrib["race"]:
            raise ValueError(f"Recipe {row['id']} used the wrong race")
        actual = Counter()
        for component in details["components"].split(","):
            item, quantity = component.split(":")
            actual[item] += int(quantity)
        alternatives = []
        for group in recipe.findall("components_data"):
            expected = Counter()
            for component in group.findall("component"):
                expected[component.attrib["itemid"]] += int(component.attrib["quantity"])
            alternatives.append(expected)
        if not actual or actual not in alternatives:
            raise ValueError(f"Recipe {row['id']} did not consume a shipped component alternative")


def teleport_routes(templates):
    result = {}
    for template in templates:
        for route in template.findall("locations/telelocation"):
            for npc in template.attrib["npc_ids"].split():
                result.setdefault(route.attrib["loc_id"], []).append((npc, route))
    return result


def validate_trade_evidence(report, catalogs, npcs, goods, items):
    """Check inactive declarations and actual transaction membership independently of server dataholders."""
    catalogs = {f"{n.tag}:{n.attrib['npc_id']}": n for n in catalogs}
    npcs = {n.attrib["npc_id"]: n for n in npcs}
    goods = {(n.tag, n.attrib["id"]): n for n in goods}
    items = {n.attrib["id"]: n for n in items}
    kinds = {"tradelist_template": (2, "list"), "trade_in_list_template": (78, "in_list"), "purchase_template": (103, "purchase_list")}
    for row in report["rows"]:
        catalog = catalogs[row["id"]]
        npc_id = catalog.attrib["npc_id"]
        action, goods_kind = kinds[catalog.tag]
        npc = npcs.get(npc_id)
        talk = npc.find("talk_info") if npc is not None else None
        actions = {int(x) for x in talk.attrib.get("func_dialogs", "").split()} if talk is not None else set()
        details = row.get("details", {})
        if details.get("npcId") != npc_id or details.get("kind") != catalog.tag:
            raise ValueError(f"Catalog {row['id']} has incorrect NPC/kind evidence")
        if row["status"] == "Inactive":
            if npc is None or action in actions or details.get("unsupportedAction") != str(action):
                raise ValueError(f"Catalog {row['id']} is not demonstrably inactive in shipped NPC actions")
            continue
        if npc is not None and action not in actions:
            raise ValueError(f"Catalog {row['id']} lacks its shipped action and must be reported separately as inactive")
        if row["status"] != "Passed":
            continue
        if action not in actions:
            raise ValueError(f"Catalog {row['id']} claims a transaction for an unsupported action")
        tabs = [t.attrib["id"] for t in catalog.findall("tradelist")]
        if sorted(details.get("tabs", "").split(",")) != sorted(tabs):
            raise ValueError(f"Catalog {row['id']} did not expose its shipped tabs")
        offered = {i.attrib["id"] for tab in tabs for i in goods[(goods_kind, tab)].findall("item")}
        if catalog.tag != "purchase_template":
            if details.get("buyObserved") != "true" or details.get("productId") not in offered or details.get("productCount") != "1":
                raise ValueError(f"Catalog {row['id']} lacks a shipped purchase")
            if any(int(details.get(key, -1)) < 0 for key in ("buyKinah", "buyAp")):
                raise ValueError(f"Catalog {row['id']} lacks exact purchase currency evidence")
            product = items[details["productId"]]
            expected = Counter()
            if catalog.tag == "trade_in_list_template":
                for item in product.findall("tradein_list/tradein_item"):
                    expected[item.attrib["id"]] += int(item.attrib["price"])
                if not expected:
                    raise ValueError(f"Trade-in {row['id']} has no shipped materials")
            else:
                acquisition = product.find("acquisition")
                if acquisition is not None and acquisition.attrib.get("item", "0") != "0":
                    expected[acquisition.attrib["item"]] += int(acquisition.attrib["count"])
            actual = Counter()
            for component in filter(None, details.get("components", "").split(",")):
                item, count = component.split(":")
                actual[item] += int(count)
            if actual != expected:
                raise ValueError(f"Catalog {row['id']} consumed incorrect purchase materials")
        purchase = catalogs.get(f"purchase_template:{npc_id}")
        can_sell = 3 in actions or (2 in actions and f"tradelist_template:{npc_id}" in catalogs) or (103 in actions and purchase is not None)
        if catalog.tag == "purchase_template" or can_sell:
            if details.get("sellObserved") != "true" or int(details.get("soldItemId", 0)) <= 0 or any(int(details.get(k, -1)) < 0 for k in ("sellKinah", "sellAp")):
                raise ValueError(f"Catalog {row['id']} lacks an actual sale and currency evidence")
            if purchase is not None:
                accepted = {i.attrib["id"] for tab in purchase.findall("tradelist")
                            for i in goods[("purchase_list", tab.attrib["id"])].findall("item")}
                if details["soldItemId"] not in accepted:
                    raise ValueError(f"Catalog {row['id']} claims sale of an unaccepted item")
        elif catalog.tag != "trade_in_list_template" or not details.get("sellNotApplicable"):
            raise ValueError(f"Catalog {row['id']} omitted its sale without supported-role evidence")


def validate_teleport_evidence(report, destinations, routes, paths):
    destinations = {node.attrib["loc_id"]: node for node in destinations}
    paths = {node.attrib["id"]: node for node in paths}
    for row in report["rows"]:
        if row["status"] != "Passed":
            continue
        details = row["details"]
        matching = [r for npc, r in routes.get(row["id"], []) if npc == details["npcId"] and r.attrib["type"] == details["type"]
                    and r.attrib.get("teleportid", "0") == details.get("teleportId")]
        if len(matching) != 1:
            raise ValueError(f"Destination {row['id']} does not identify one shipped incoming route")
        destination = destinations[row["id"]]
        if details["mapId"] != destination.attrib["mapid"]:
            raise ValueError(f"Destination {row['id']} arrived in the wrong map")
        if details["type"] == "FLIGHT":
            path = paths[str(int(details["teleportId"]) // 1000)]
            expected = [float(path.attrib[key]) for key in ("ex", "ey", "ez")]
            if path.attrib["eworld"] != details["mapId"] or path.attrib["sworld"] != details["sourceMapId"] or abs(int(details["flightMillis"]) - float(path.attrib["time"]) * 1000) > 1:
                raise ValueError(f"Flight destination {row['id']} has wrong endpoints or duration")
        else:
            expected = [float(destination.attrib.get(key, 0)) for key in ("posX", "posY", "posZ")]
        actual = [float(v) for v in details.get("position", "").split(",")]
        if len(actual) != 3 or not all(math.isfinite(v) for v in actual) or math.dist(actual, expected) > 0.1:
            raise ValueError(f"Destination {row['id']} arrived at the wrong coordinates")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--baseline", type=Path, required=True)
    parser.add_argument("--write-baseline", action="store_true", help="Explicitly record only a complete, clean runtime run")
    args = parser.parse_args()
    report = json.loads(args.report.read_text(encoding="utf-8"))
    sweep = report.get("sweep")
    if sweep not in SWEEPS:
        raise ValueError("Unsupported sweep kind")
    source = REPO / "game-server/data/static_data" / SWEEPS[sweep][1]
    templates = ET.parse(source).getroot().findall(SWEEPS[sweep][2])
    id_attribute = {"bindpoints": "npcid", "teleporters": "loc_id"}.get(sweep, "id")
    skill_cases = {}
    source_paths = [source]
    if sweep == "skills":
        source_paths += sorted((REPO / "game-server/data/static_data/skill_tree").glob("*.xml"))
        skill_cases = skill_inventory((row for path in source_paths[1:] for row in ET.parse(path).getroot()), templates)
        ids = set(skill_cases)
    else:
        ids = {f"{n.tag}:{n.attrib['npc_id']}" for n in templates} if sweep == "tradelists" else {node.attrib[id_attribute] for node in templates}
    spawned = {node.attrib["npc_id"] for path in (REPO / "game-server/data/static_data/spawns").rglob("*.xml")
               for node in ET.parse(path).iter("spawn") if "npc_id" in node.attrib}
    metadata = json.loads((args.report.parent.parent / "run.json").read_text(encoding="utf-8"))
    routes = {}
    if sweep == "tradelists":
        spawned = {f"{n.tag}:{n.attrib['npc_id']}" for n in templates if n.attrib["npc_id"] in spawned}
    if sweep == "teleporters":
        routes = teleport_routes(ET.parse(REPO / "game-server/data/static_data/npc_teleporter.xml").getroot())
        if set(routes) - ids:
            raise ValueError("Teleporter routes reference destinations missing from teleport_location.xml")
        spawned = {location for location, incoming in routes.items() if any(npc in spawned for npc, _ in incoming)}
    rows = validate(report, metadata, ids, spawned)
    if sweep == "recipes":
        validate_recipe_evidence(report, templates)
    if sweep == "skills":
        validate_skill_evidence(report, skill_cases)
        static = REPO / "game-server/data/static_data"
        validate_skill_professions(report, ET.parse(static / "gatherables/gatherable_templates.xml").getroot(),
                                  ET.parse(static / "recipe/recipe_templates.xml").getroot())
        validate_skill_pet_orders(report, templates, ET.parse(static / "pet_skills/pet_skills.xml").getroot())
    if sweep == "tradelists":
        countries = {int(r["details"]["countryCode"]) for r in report["rows"] if r["status"] == "Passed"}
        if len(countries) != 1:
            raise ValueError("Trade sweep must identify one actual loaded country profile")
        region = {1: "usa", 2: "europe", 4: "japan", 5: "china", 6: "taiwan", 7: "russia"}.get(countries.pop())
        static = REPO / "game-server/data/static_data"
        goods_path = static / "goodslists" / (f"goodslists_{region}.xml" if region else "goodslists.xml")
        if not goods_path.exists():
            goods_path = static / "goodslists/goodslists.xml"
        validate_trade_evidence(report, templates, ET.parse(static / "npcs/npc_templates.xml").getroot(),
                                ET.parse(goods_path).getroot(), ET.parse(static / "items/item_templates.xml").getroot())
    if sweep == "teleporters":
        validate_teleport_evidence(report, templates, routes, ET.parse(REPO / "game-server/data/static_data/flypath_template.xml").getroot())
    if args.write_baseline:
        baseline = {"schemaVersion": 1, "sweep": sweep, "verifiedRun": metadata["run"],
                    "gitSha": metadata["gitSha"], "seed": metadata["seed"],
                    "sourceSha256": hashlib.sha256(b"".join(p.read_bytes().replace(b"\r\n", b"\n") for p in source_paths)).hexdigest(), "rows": rows}
        args.baseline.parent.mkdir(parents=True, exist_ok=True)
        args.baseline.write_text(json.dumps(baseline, indent=2) + "\n", encoding="utf-8")
    else:
        compare(rows, json.loads(args.baseline.read_text(encoding="utf-8")), sweep)
    print(f"{sweep} sweep validated: {len(rows)} rows, "
          f"{sum(r['status'] == 'Passed' for r in rows)} passed, "
          f"{sum(r['status'] == 'Unreachable' for r in rows)} unreachable, "
          f"{sum(r['status'] == 'Inactive' for r in rows)} inactive")


if __name__ == "__main__":
    main()
