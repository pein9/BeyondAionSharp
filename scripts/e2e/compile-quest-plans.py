#!/usr/bin/env python3
"""Compile static quest data into deterministic player-simulation plans.

The generated plans are build artifacts. The small availability classifier is
checked in so coverage reports can distinguish disabled, unreachable, and
unimplemented quests without starting the game server.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable


REPO_ROOT = Path(__file__).resolve().parents[2]
STATIC_ROOT = REPO_ROOT / "game-server/data/static_data"
QUEST_DATA = STATIC_ROOT / "quest_data/quest_data.xml"
SCRIPT_ROOT = STATIC_ROOT / "quest_script_data"
NPC_TEMPLATES = STATIC_ROOT / "npcs/npc_templates.xml"
GATHERABLES = STATIC_ROOT / "gatherables/gatherable_templates.xml"
NPC_FACTIONS = STATIC_ROOT / "npc_factions/npc_factions.xml"
HANDLER_ROOT = REPO_ROOT / "src/Aion.GameServer/Handlers/Quest"
HANDLER_SPAWN_IDS = REPO_ROOT / "src/Aion.GameServer/Questengine/HandlerSpawnedNpcIds.cs"
DEFAULT_CLASSIFIER = REPO_ROOT / "parity-artifacts/e2e/obtainable-quests.json"

CUSTOM_HANDLER_ID = re.compile(r"^_(\d+)")
INTEGER = re.compile(r"^-?\d+$")
FLOAT = re.compile(r"^-?(?:\d+\.\d*|\d*\.\d+)$")
NPC_ATTRIBUTE_NAMES = {
    "npc_id",
    "npc_ids",
    "start_npc_id",
    "start_npc_ids",
    "end_npc_id",
    "end_npc_ids",
    "next_npc_id",
    "talk_npc_id1",
    "talk_npc_id2",
    "aggro_start_npc_ids",
    "start_dist_npc_id",
    "spawner_object_id",
    "ids",  # XML quest on_talk_event ids are NPC alternatives.
}
END_DEFAULTS_TO_START = {
    "item_collecting",
    "monster_hunt",
    "report_to",
    "skill_use",
    "kill_spawned",
    "kill_in_world",
    "kill_in_zone",
    "xml_quest",
}
START_IS_END = {"work_order", "relic_rewards", "fountain_rewards"}


def parse_scalar(value: str) -> Any:
    stripped = value.strip()
    lowered = stripped.lower()
    if lowered == "true":
        return True
    if lowered == "false":
        return False
    if INTEGER.fullmatch(stripped):
        return int(stripped)
    if FLOAT.fullmatch(stripped):
        return float(stripped)
    return stripped


def int_list(value: str | None) -> list[int]:
    if not value:
        return []
    return [int(part) for part in value.replace(",", " ").split()]


def text_list(element: ET.Element | None) -> list[Any]:
    if element is None or not element.text:
        return []
    return [parse_scalar(part) for part in element.text.split()]


def attrs(element: ET.Element) -> dict[str, Any]:
    return {key: parse_scalar(value) for key, value in sorted(element.attrib.items())}


def xml_payload(element: ET.Element) -> dict[str, Any]:
    result: dict[str, Any] = {"tag": element.tag}
    if element.attrib:
        result["attributes"] = attrs(element)
    text = (element.text or "").strip()
    if text:
        result["text"] = [parse_scalar(part) for part in text.split()]
    children = [xml_payload(child) for child in element]
    if children:
        result["children"] = children
    return result


def relative(path: Path) -> str:
    return path.relative_to(REPO_ROOT).as_posix()


@dataclass(frozen=True)
class Handler:
    kind: str
    source: str
    template: str | None
    element: ET.Element | None


@dataclass
class Inputs:
    quests: dict[int, ET.Element]
    handlers: dict[int, Handler]
    gatherables_by_item: dict[int, list[dict[str, Any]]]
    npc_names: dict[int, str]
    positions: dict[int, list[dict[str, Any]]]
    spawned_ids: set[int]
    dynamic_spawn_ids: set[int]
    valid_factions: set[int]


def load_quests() -> dict[int, ET.Element]:
    return {int(element.attrib["id"]): element for element in ET.parse(QUEST_DATA).getroot().findall("quest")}


def load_handlers() -> dict[int, Handler]:
    handlers: dict[int, Handler] = {}
    for path in sorted(SCRIPT_ROOT.glob("*.xml")):
        for element in ET.parse(path).getroot():
            if element.tag == "import":
                continue
            quest_id = int(element.attrib["id"])
            if quest_id in handlers:
                raise ValueError(f"duplicate quest handler for {quest_id}")
            handlers[quest_id] = Handler("template", relative(path), element.tag, element)

    for path in sorted(HANDLER_ROOT.rglob("*.cs")):
        match = CUSTOM_HANDLER_ID.match(path.stem)
        if not match:
            continue
        quest_id = int(match.group(1))
        if quest_id in handlers:
            raise ValueError(f"quest {quest_id} has both template and custom handlers")
        handlers[quest_id] = Handler("custom", relative(path), None, None)
    return handlers


def load_gatherables() -> dict[int, list[dict[str, Any]]]:
    by_item: dict[int, list[dict[str, Any]]] = defaultdict(list)
    for template in ET.parse(GATHERABLES).getroot().findall("gatherable_template"):
        template_id = int(template.attrib["id"])
        for material in template.findall("./materials/material"):
            item_id = int(material.attrib["itemid"])
            by_item[item_id].append(
                {
                    "gatherableId": template_id,
                    "name": template.attrib.get("name"),
                    "rate": int(material.attrib["rate"]),
                    "skillLevel": int(template.attrib.get("skillLevel", "0")),
                }
            )
    for values in by_item.values():
        values.sort(key=lambda value: (value["gatherableId"], value["rate"]))
    return dict(by_item)


def referenced_npc_ids(
    quests: dict[int, ET.Element], handlers: dict[int, Handler], gatherables_by_item: dict[int, list[dict[str, Any]]]
) -> set[int]:
    result: set[int] = set()
    collect_item_ids: set[int] = set()
    for quest in quests.values():
        for element in quest.iter():
            for name, value in element.attrib.items():
                if name in NPC_ATTRIBUTE_NAMES and name != "ids":
                    result.update(int_list(value))
            if element.tag == "collect_item" and "item_id" in element.attrib:
                collect_item_ids.add(int(element.attrib["item_id"]))
    for handler in handlers.values():
        if handler.element is None:
            continue
        for element in handler.element.iter():
            for name, value in element.attrib.items():
                if name in NPC_ATTRIBUTE_NAMES:
                    result.update(int_list(value))
    for item_id in collect_item_ids:
        result.update(source["gatherableId"] for source in gatherables_by_item.get(item_id, []))
    return result


def load_dynamic_spawn_ids() -> set[int]:
    source = HANDLER_SPAWN_IDS.read_text(encoding="utf-8-sig")
    try:
        body = source.split("internal static readonly int[] All", 1)[1].split("[", 1)[1].split("]", 1)[0]
    except IndexError as error:
        raise ValueError(f"could not parse {relative(HANDLER_SPAWN_IDS)}") from error
    return {int(value) for value in re.findall(r"\d+", body)}


def spawn_sources() -> Iterable[tuple[Path, bool]]:
    for root in (STATIC_ROOT / "spawns", STATIC_ROOT / "town_spawns"):
        for path in sorted(root.rglob("*.xml")):
            yield path, False
    for path in sorted((STATIC_ROOT / "events/timed_events").glob("*.xml")):
        yield path, True


def load_spawns(relevant_ids: set[int], dynamic_ids: set[int]) -> tuple[set[int], dict[int, list[dict[str, Any]]]]:
    spawned_ids = set(dynamic_ids)
    positions: dict[int, list[dict[str, Any]]] = defaultdict(list)
    seen_positions: dict[int, set[tuple[Any, ...]]] = defaultdict(set)
    for path, conditional in spawn_sources():
        root = ET.parse(path).getroot()
        for spawn_map in root.iter("spawn_map"):
            map_id = int(spawn_map.attrib["map_id"])
            for spawn in spawn_map.iter("spawn"):
                if "npc_id" not in spawn.attrib:
                    continue
                npc_id = int(spawn.attrib["npc_id"])
                spawned_ids.add(npc_id)
                if npc_id not in relevant_ids:
                    continue
                for spot in spawn.findall("spot"):
                    position = {
                        "mapId": map_id,
                        "x": float(spot.attrib["x"]),
                        "y": float(spot.attrib["y"]),
                        "z": float(spot.attrib["z"]),
                        "heading": int(spot.attrib.get("h", "0")),
                        "source": relative(path),
                    }
                    if conditional:
                        position["conditionalEvent"] = True
                    key = tuple(position.items())
                    if key not in seen_positions[npc_id]:
                        positions[npc_id].append(position)
                        seen_positions[npc_id].add(key)
    for values in positions.values():
        values.sort(key=lambda value: (value["mapId"], value["x"], value["y"], value["z"], value["source"]))
    return spawned_ids, dict(positions)


def load_npc_names(relevant_ids: set[int]) -> dict[int, str]:
    names: dict[int, str] = {}
    for _, element in ET.iterparse(NPC_TEMPLATES, events=("end",)):
        if element.tag == "npc_template":
            npc_id = int(element.attrib["npc_id"])
            if npc_id in relevant_ids:
                names[npc_id] = element.attrib.get("name", "")
        element.clear()
    return names


def load_valid_factions(spawned_ids: set[int]) -> set[int]:
    valid: set[int] = set()
    for faction in ET.parse(NPC_FACTIONS).getroot().findall("npc_faction"):
        members = int_list(faction.attrib.get("npc_ids"))
        if not members or any(npc_id in spawned_ids for npc_id in members):
            valid.add(int(faction.attrib["id"]))
    return valid


def load_inputs() -> Inputs:
    quests = load_quests()
    handlers = load_handlers()
    gatherables = load_gatherables()
    relevant_ids = referenced_npc_ids(quests, handlers, gatherables)
    dynamic_ids = load_dynamic_spawn_ids()
    spawned_ids, positions = load_spawns(relevant_ids, dynamic_ids)
    return Inputs(
        quests,
        handlers,
        gatherables,
        load_npc_names(relevant_ids),
        positions,
        spawned_ids,
        dynamic_ids,
        load_valid_factions(spawned_ids),
    )


def npc_ref(npc_id: int, inputs: Inputs) -> dict[str, Any]:
    return {
        "id": npc_id,
        "name": inputs.npc_names.get(npc_id),
        "positions": inputs.positions.get(npc_id, []),
        "handlerSpawned": npc_id in inputs.dynamic_spawn_ids,
    }


def npc_group(ids: Iterable[int], inputs: Inputs) -> list[dict[str, Any]]:
    return [npc_ref(npc_id, inputs) for npc_id in sorted(set(ids))]


def start_and_end_ids(quest: ET.Element, handler: Handler | None) -> tuple[list[int], list[int]]:
    if handler is None or handler.element is None:
        return [], []
    element = handler.element
    template = handler.template or ""
    start = int_list(element.attrib.get("start_npc_ids"))
    start += int_list(element.attrib.get("start_npc_id"))
    end = int_list(element.attrib.get("end_npc_ids"))
    end += int_list(element.attrib.get("end_npc_id"))
    if template == "report_to_many":
        groups = [int_list(info.attrib.get("npc_ids")) for info in element.findall("npc_infos")]
        if groups:
            end = groups[-1]
    elif template == "item_order":
        end = int_list(element.attrib.get("end_npc_id"))
    elif template == "crafting_rewards":
        if not end:
            end = list(start)
    elif template in START_IS_END:
        end = list(start)
    elif template in END_DEFAULTS_TO_START and not end:
        end = list(start)
    return sorted(set(start)), sorted(set(end))


def start_trigger(quest: ET.Element, handler: Handler | None, start_ids: list[int], inputs: Inputs) -> dict[str, Any]:
    if handler is None:
        return {"kind": "none"}
    if handler.kind == "custom":
        return {"kind": "custom", "source": handler.source}
    assert handler.element is not None
    element = handler.element
    if handler.template == "report_on_levelup":
        return {"kind": "levelUp", "minimumLevel": int(quest.attrib.get("minlevel_permitted", "0"))}
    start_item_id = int(element.attrib.get("start_item_id", "0"))
    if handler.template == "item_order":
        work_item = quest.find("./quest_work_items/quest_work_item")
        if work_item is not None:
            start_item_id = int(work_item.attrib["item_id"])
    if start_item_id:
        return {"kind": "item", "itemId": start_item_id}
    if start_ids:
        return {"kind": "npc", "npcs": npc_group(start_ids, inputs)}
    if element.attrib.get("start_zone"):
        return {"kind": "zone", "zone": element.attrib["start_zone"]}
    if element.attrib.get("invasion_world"):
        return {"kind": "world", "mapId": int(element.attrib["invasion_world"])}
    return {"kind": "automatic"}


def normalized_start_conditions(quest: ET.Element) -> list[dict[str, Any]]:
    groups: list[dict[str, Any]] = []
    for condition in quest.findall("start_conditions"):
        group: dict[str, Any] = {
            "finished": [attrs(item) for item in condition.findall("finished")],
        }
        for tag in ("unfinished", "noacquired", "acquired", "equipped", "required_title"):
            values = text_list(condition.find(tag))
            if values:
                group[tag] = values
        groups.append(group)
    return groups


def normalized_rewards(quest: ET.Element) -> dict[str, Any]:
    def reward(element: ET.Element) -> dict[str, Any]:
        value = attrs(element)
        value["rewardItems"] = [attrs(item) for item in element.findall("reward_item")]
        value["selectableItems"] = [attrs(item) for item in element.findall("selectable_reward_item")]
        return value

    class_rewards: dict[str, list[dict[str, Any]]] = {}
    for child in quest:
        if child.tag.endswith("_selectable_reward"):
            class_rewards.setdefault(child.tag.removesuffix("_selectable_reward"), []).append(attrs(child))
    result: dict[str, Any] = {
        "standard": [reward(element) for element in quest.findall("rewards")],
        "classSelectable": class_rewards,
    }
    extended = quest.find("extended_rewards")
    if extended is not None:
        result["extended"] = reward(extended)
    bonus = quest.find("bonus")
    if bonus is not None:
        result["bonus"] = attrs(bonus)
    return result


def item_sources(item_id: int, quest: ET.Element, handler: Handler | None, inputs: Inputs) -> list[dict[str, Any]]:
    sources: list[dict[str, Any]] = []
    for drop in quest.findall("quest_drop"):
        if int(drop.attrib.get("item_id", "0")) != item_id:
            continue
        source = {"kind": "questDrop", **attrs(drop)}
        source["npc"] = npc_ref(int(drop.attrib["npc_id"]), inputs)
        sources.append(source)
    for gatherable in inputs.gatherables_by_item.get(item_id, []):
        source = {"kind": "gatherable", **gatherable}
        source["positions"] = inputs.positions.get(gatherable["gatherableId"], [])
        sources.append(source)
    if handler is not None and handler.template == "xml_quest" and handler.element is not None:
        for event in handler.element.findall("on_talk_event"):
            for npc in event.iter("npc"):
                if not any(int(give.attrib.get("item_id", "0")) == item_id for give in npc.iter("give_item")):
                    continue
                npc_id = int(npc.attrib["id"])
                source = {"kind": "questObject", "npc": npc_ref(npc_id, inputs)}
                if source not in sources:
                    sources.append(source)
    return sources


def normalized_steps(quest: ET.Element, handler: Handler | None, end_ids: list[int], inputs: Inputs) -> list[dict[str, Any]]:
    steps: list[dict[str, Any]] = []
    for kill in quest.findall("quest_kill"):
        step = {"kind": "kill", **attrs(kill)}
        step["npcs"] = npc_group(int_list(kill.attrib.get("npc_ids")), inputs)
        steps.append(step)
    collect = quest.find("collect_items")
    if collect is not None:
        for item in collect.findall("collect_item"):
            item_id = int(item.attrib["item_id"])
            steps.append({"kind": "collect", **attrs(item), "sources": item_sources(item_id, quest, handler, inputs)})

    if handler is None or handler.element is None:
        return steps
    element = handler.element
    template = handler.template or ""
    if template == "report_to_many":
        for index, info in enumerate(element.findall("npc_infos"), start=1):
            steps.append({"kind": "report", "sequence": index, "npcs": npc_group(int_list(info.attrib["npc_ids"]), inputs)})
    elif template == "item_order":
        for index, name in enumerate(("talk_npc_id1", "talk_npc_id2", "end_npc_id"), start=1):
            ids = int_list(element.attrib.get(name))
            if ids:
                steps.append({"kind": "report", "sequence": index, "npcs": npc_group(ids, inputs)})
    elif template == "work_order":
        steps.append(
            {
                "kind": "craft",
                "recipeId": int(element.attrib["recipe_id"]),
                "components": [attrs(item) for item in element.findall("give_component")],
            }
        )
    elif template == "skill_use":
        for skill in element.findall("skill"):
            steps.append({"kind": "useSkill", **attrs(skill)})
    elif template == "kill_spawned":
        for monster in element.findall("monster"):
            step = {"kind": "killSpawned", **attrs(monster)}
            step["npcs"] = npc_group(int_list(monster.attrib.get("npc_ids")), inputs)
            steps.append(step)
    elif template in {"kill_in_world", "kill_in_zone"}:
        steps.append({"kind": template.replace("_", ""), **attrs(element)})

    if end_ids and template not in {"report_to_many", "item_order"}:
        steps.append({"kind": "report", "npcs": npc_group(end_ids, inputs)})
    return steps


def compile_plan(quest_id: int, inputs: Inputs) -> dict[str, Any]:
    quest = inputs.quests[quest_id]
    handler = inputs.handlers.get(quest_id)
    start_ids, end_ids = start_and_end_ids(quest, handler)
    handler_json: dict[str, Any] = {"kind": "none"}
    if handler is not None:
        handler_json = {"kind": handler.kind, "source": handler.source}
        if handler.template:
            handler_json["template"] = handler.template
            assert handler.element is not None
            handler_json["templateData"] = xml_payload(handler.element)
    return {
        "schemaVersion": 1,
        "quest": {
            "id": quest_id,
            "name": quest.attrib.get("name"),
            "nameId": int(quest.attrib.get("nameId", "0")),
            "zone": quest.attrib.get("quest_zone"),
            "category": quest.attrib.get("category", "QUEST"),
            "race": quest.attrib.get("race_permitted"),
        },
        "gates": {
            "minimumLevel": int(quest.attrib.get("minlevel_permitted", "0")),
            "maximumLevel": int(quest.attrib.get("maxlevel_permitted", "0")),
            "classes": text_list(quest.find("class_permitted")),
            "gender": (quest.findtext("gender_permitted") or "").strip() or None,
            "maximumRepeats": int(quest.attrib.get("max_repeat_count", "1")),
            "rewardRepeatCount": int(quest.attrib.get("reward_repeat_count", "0")),
            "repeatCycle": (quest.attrib.get("repeat_cycle") or "").split(),
            "npcFactionId": int(quest.attrib.get("npcfaction_id", "0")),
            "startConditions": normalized_start_conditions(quest),
        },
        "handler": handler_json,
        "startTrigger": start_trigger(quest, handler, start_ids, inputs),
        "startNpcs": npc_group(start_ids, inputs),
        "endNpcs": npc_group(end_ids, inputs),
        "steps": normalized_steps(quest, handler, end_ids, inputs),
        "rewards": normalized_rewards(quest),
        "questData": xml_payload(quest),
    }


RUNNABLE_TEMPLATES = {
    "crafting_rewards",
    "fountain_rewards",
    "item_collecting",
    "item_order",
    "kill_in_world",
    "kill_in_zone",
    "kill_spawned",
    "monster_hunt",
    "relic_rewards",
    "report_on_levelup",
    "report_to",
    "report_to_many",
    "skill_use",
    "work_order",
    "xml_quest",
}
RUNNABLE_START_TRIGGERS = {"automatic", "item", "levelUp", "npc", "world", "zone"}
RUNNABLE_STEPS = {"collect", "craft", "kill", "killinworld", "killinzone", "killSpawned", "report", "useSkill"}


def validate_runnable_plan(plan: dict[str, Any]) -> list[str]:
    """Return structural defects that would leave the shared runner without an action."""
    issues: list[str] = []
    quest_id = plan["quest"]["id"]
    template = plan["handler"].get("template")
    if template not in RUNNABLE_TEMPLATES:
        issues.append(f"Q{quest_id}: unsupported template {template!r}")
    trigger = plan["startTrigger"]["kind"]
    if trigger not in RUNNABLE_START_TRIGGERS:
        issues.append(f"Q{quest_id}: unsupported start trigger {trigger!r}")
    if trigger == "npc" and not plan["startTrigger"].get("npcs"):
        issues.append(f"Q{quest_id}: NPC start has no NPC alternatives")
    if trigger == "item" and not plan["startTrigger"].get("itemId"):
        issues.append(f"Q{quest_id}: item start has no item id")

    for index, step in enumerate(plan["steps"]):
        kind = step["kind"]
        if kind not in RUNNABLE_STEPS:
            issues.append(f"Q{quest_id}: step {index} has unsupported kind {kind!r}")
        if kind in {"kill", "killSpawned", "report"} and not step.get("npcs"):
            issues.append(f"Q{quest_id}: {kind} step {index} has no NPC alternatives")
        if kind == "collect" and template != "work_order" and not step.get("sources"):
            issues.append(f"Q{quest_id}: collect step {index} has no declared source")
        if kind == "craft" and not step.get("recipeId"):
            issues.append(f"Q{quest_id}: craft step {index} has no recipe id")
        if kind == "useSkill" and not (step.get("ids") or step.get("skill_ids") or step.get("skill_id")):
            issues.append(f"Q{quest_id}: skill step {index} has no skill id")
    if template not in {"fountain_rewards", "relic_rewards"} and not plan["endNpcs"]:
        issues.append(f"Q{quest_id}: template {template!r} has no reward NPC")
    return issues


def required_npc_groups(quest: ET.Element, handler: Handler | None, gatherables_by_item: dict[int, list[dict[str, Any]]]) -> list[tuple[str, list[int]]]:
    if handler is None or handler.element is None:
        return []  # Java analyzer assumes custom handler alternatives work.
    start_ids, end_ids = start_and_end_ids(quest, handler)
    groups: list[tuple[str, list[int]]] = []
    if start_ids:
        groups.append(("start", start_ids))
    if end_ids:
        groups.append(("end", end_ids))
    for kill in quest.findall("quest_kill"):
        ids = int_list(kill.attrib.get("npc_ids"))
        if ids:
            groups.append(("kill", ids))
    drops_by_item: dict[int, list[int]] = defaultdict(list)
    for drop in quest.findall("quest_drop"):
        drops_by_item[int(drop.attrib.get("item_id", "0"))].append(int(drop.attrib["npc_id"]))
    collect = quest.find("collect_items")
    if collect is not None:
        for item in collect.findall("collect_item"):
            item_id = int(item.attrib["item_id"])
            ids = drops_by_item[item_id] + [source["gatherableId"] for source in gatherables_by_item.get(item_id, [])]
            if ids:
                groups.append(("collect", ids))
    element = handler.element
    if handler.template == "report_to_many":
        for info in element.findall("npc_infos"):
            groups.append(("report", int_list(info.attrib.get("npc_ids"))))
    elif handler.template == "item_order":
        for name in ("talk_npc_id1", "talk_npc_id2"):
            ids = int_list(element.attrib.get(name))
            if ids:
                groups.append(("report", ids))
    elif handler.template == "kill_spawned":
        for monster in element.findall("monster"):
            ids = int_list(monster.attrib.get("npc_ids"))
            if ids:
                groups.append(("killSpawned", ids))
    return [(role, sorted(set(ids))) for role, ids in groups if ids]


def compile_classifier(inputs: Inputs) -> dict[str, Any]:
    status: dict[int, str] = {}
    reasons: dict[int, list[dict[str, Any]]] = defaultdict(list)
    prerequisite_groups: dict[int, list[list[int]]] = {}

    for quest_id, quest in inputs.quests.items():
        handler = inputs.handlers.get(quest_id)
        minimum_level = int(quest.attrib.get("minlevel_permitted", "0"))
        faction_id = int(quest.attrib.get("npcfaction_id", "0"))
        category = quest.attrib.get("category", "QUEST")
        if minimum_level == 99:
            status[quest_id] = "disabled"
            reasons[quest_id].append({"code": "minimum-level-99"})
        elif category == "EVENT" or quest_id >= 80000:
            status[quest_id] = "disabled"
            reasons[quest_id].append({"code": "event-quest"})
        elif faction_id and faction_id not in inputs.valid_factions:
            status[quest_id] = "disabled"
            reasons[quest_id].append({"code": "missing-faction-spawn", "npcFactionId": faction_id})
        elif handler is None:
            status[quest_id] = "no_handler"
            reasons[quest_id].append({"code": "no-handler"})
        else:
            missing = []
            for role, ids in required_npc_groups(quest, handler, inputs.gatherables_by_item):
                if not any(npc_id in inputs.spawned_ids for npc_id in ids):
                    missing.append({"role": role, "npcIds": ids})
            if missing:
                status[quest_id] = "unreachable"
                reasons[quest_id].append({"code": "missing-spawn", "groups": missing})
            else:
                status[quest_id] = "obtainable"
        prerequisite_groups[quest_id] = [
            [int(item.attrib["quest_id"]) for item in condition.findall("finished")]
            for condition in quest.findall("start_conditions")
            if condition.findall("finished")
        ]

    changed = True
    while changed:
        changed = False
        for quest_id in sorted(inputs.quests):
            if status[quest_id] != "obtainable":
                continue
            for group in prerequisite_groups[quest_id]:
                unavailable = [required for required in group if status.get(required) != "obtainable"]
                if group and len(unavailable) == len(group):
                    status[quest_id] = "unreachable"
                    reasons[quest_id].append(
                        {"code": "unobtainable-prerequisites", "questIds": sorted(group)}
                    )
                    changed = True
                    break

    entries = []
    planner_counts: Counter[str] = Counter()
    for quest_id in sorted(inputs.quests):
        quest = inputs.quests[quest_id]
        handler = inputs.handlers.get(quest_id)
        plan_issues: list[dict[str, Any]] = []
        if handler is None:
            plan_support = "none"
        elif status[quest_id] != "obtainable":
            plan_support = "unavailable"
        elif handler.kind == "custom":
            plan_support = "custom"
        else:
            unresolved_items: list[int] = []
            collect = quest.find("collect_items")
            if handler.template != "work_order" and collect is not None:
                for item in collect.findall("collect_item"):
                    item_id = int(item.attrib["item_id"])
                    if not item_sources(item_id, quest, handler, inputs):
                        unresolved_items.append(item_id)
            if unresolved_items:
                plan_support = "template_incomplete"
                plan_issues.append({"code": "unresolved-item-source", "itemIds": sorted(set(unresolved_items))})
            else:
                plan_support = "template"
        planner_counts[plan_support] += 1
        entries.append(
            {
                "id": quest_id,
                "zone": quest.attrib.get("quest_zone"),
                "race": quest.attrib.get("race_permitted"),
                "handlerKind": handler.kind if handler else "none",
                "template": handler.template if handler else None,
                "availability": status[quest_id],
                "reasons": reasons[quest_id],
                "planSupport": plan_support,
                "planIssues": plan_issues,
            }
        )
    counts = Counter(status.values())
    return {
        "schemaVersion": 1,
        "generatedBy": "scripts/e2e/compile-quest-plans.py",
        "counts": {key: counts.get(key, 0) for key in ("obtainable", "disabled", "unreachable", "no_handler")},
        "plannerCounts": {
            key: planner_counts.get(key, 0)
            for key in ("template", "template_incomplete", "custom", "none", "unavailable")
        },
        "quests": entries,
    }


def json_text(value: Any) -> str:
    return json.dumps(value, indent=2, ensure_ascii=False, sort_keys=False) + "\n"


def write_plans(output: Path, quest_ids: list[int], inputs: Inputs) -> None:
    output.mkdir(parents=True, exist_ok=True)
    for quest_id in quest_ids:
        if quest_id not in inputs.quests:
            raise ValueError(f"unknown quest id {quest_id}")
        (output / f"{quest_id}.json").write_text(json_text(compile_plan(quest_id, inputs)), encoding="utf-8", newline="\n")
    print(f"wrote {len(quest_ids)} quest plan(s) to {output}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, help="directory for per-quest JSON plans")
    parser.add_argument("--quest", type=int, action="append", default=[], help="quest id to compile (repeatable; defaults to all)")
    parser.add_argument("--zone", action="append", default=[], help="limit generated plans to an exact quest zone (repeatable)")
    parser.add_argument("--runnable-only", action="store_true", help="generate only obtainable template plans")
    parser.add_argument("--check-runnable", action="store_true", help="validate every obtainable template plan has runnable actions")
    parser.add_argument("--classifier", type=Path, default=DEFAULT_CLASSIFIER, help="availability classifier path")
    classifier = parser.add_mutually_exclusive_group()
    classifier.add_argument("--write-classifier", action="store_true", help="write the availability classifier")
    classifier.add_argument("--check-classifier", action="store_true", help="fail when the checked-in classifier is stale")
    args = parser.parse_args()
    if args.output is None and not args.write_classifier and not args.check_classifier and not args.check_runnable:
        parser.error("choose --output, --write-classifier, --check-classifier, or --check-runnable")
    if args.runnable_only and args.output is None:
        parser.error("--runnable-only requires --output")
    if args.zone and args.output is None:
        parser.error("--zone requires --output")

    try:
        inputs = load_inputs()
        classifier_document: dict[str, Any] | None = None
        if args.output is not None:
            quest_ids = sorted(set(args.quest)) if args.quest else sorted(inputs.quests)
            if args.runnable_only:
                classifier_document = compile_classifier(inputs)
                runnable = {
                    entry["id"]
                    for entry in classifier_document["quests"]
                    if entry["availability"] == "obtainable" and entry["planSupport"] == "template"
                }
                quest_ids = [quest_id for quest_id in quest_ids if quest_id in runnable]
            if args.zone:
                zones = set(args.zone)
                quest_ids = [quest_id for quest_id in quest_ids if inputs.quests[quest_id].attrib.get("quest_zone") in zones]
            write_plans(args.output, quest_ids, inputs)
        if args.check_runnable:
            classifier_document = classifier_document or compile_classifier(inputs)
            runnable_ids = [
                entry["id"]
                for entry in classifier_document["quests"]
                if entry["availability"] == "obtainable" and entry["planSupport"] == "template"
            ]
            runnable_issues = [
                issue
                for quest_id in runnable_ids
                for issue in validate_runnable_plan(compile_plan(quest_id, inputs))
            ]
            if runnable_issues:
                for issue in runnable_issues:
                    print(issue, file=sys.stderr)
                print(f"{len(runnable_issues)} runnable quest plan defect(s)", file=sys.stderr)
                return 1
            print(f"all {len(runnable_ids)} obtainable template plans have runnable actions")
        if args.write_classifier or args.check_classifier:
            generated = json_text(classifier_document or compile_classifier(inputs))
            classifier_path = args.classifier.resolve()
            if args.check_classifier:
                if not classifier_path.exists() or classifier_path.read_text(encoding="utf-8-sig") != generated:
                    print(f"stale quest classifier: {classifier_path}", file=sys.stderr)
                    return 1
                parsed = json.loads(generated)
                print(f"quest classifier is current ({parsed['counts']['obtainable']} obtainable)")
            else:
                classifier_path.parent.mkdir(parents=True, exist_ok=True)
                classifier_path.write_text(generated, encoding="utf-8", newline="\n")
                parsed = json.loads(generated)
                print(f"wrote {classifier_path} ({parsed['counts']['obtainable']} obtainable)")
        return 0
    except (OSError, ET.ParseError, ValueError) as error:
        print(f"quest plan compiler failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
