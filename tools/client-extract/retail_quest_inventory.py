#!/usr/bin/env python3
"""RQ-01: join every no-handler quest against the 4.8 client and this server's data.

WHY THIS EXISTS
---------------
D32 (`docs/retail-quest-completion.md`) authorizes handlers for quests that the 4.8 client ships,
4.8 retail ran, and Java never implemented. The checked-in classifier
(`parity-artifacts/e2e/obtainable-quests.json`) says *which* quests have no handler; it says nothing
about what each one needs. This answers that from the client itself, read-only:

* the client's `quest.xml` entry (levels, prerequisites, collect items, drop sources, flags);
* its `quest_monster.csv` and `quest_script_monster.csv` rows, with every client devname turned into
  an npc id through the client's own `client_npcs_npc.xml` / `client_npcs_monster.xml`;
* for every npc and item that names, whether this server has a template and a spawn;
* where the client and this server's `quest_data.xml` disagree;
* whether Java (`../aion-server`, or `BEYOND_AION_JAVA_ROOT`) has a handler after all, and whether the
  client ships a dialog file for the quest.

It also lists the quests the client ships that `quest_data.xml` does not carry at all, since those
have no handler either and the classifier cannot see them.

WHAT IT DOES NOT DECIDE
-----------------------
It classifies nothing. Classes A-E need the retail evidence of RQ-03 and a person's reading (RQ-04).
The `readiness` block is a mechanical summary of what exists, not a verdict: a quest can have every
npc spawned and still be superseded, and a missing template can be a devname only the client uses.

Start and end npcs are **not** in `quest.xml`; they live in the quest dialogs, whose second encryption
layer this repository cannot decode (see README). RQ-03 takes them from aioncodex `/48/`.

The output keeps no localized text except the client's English quest name, which the server's own
`quest_data.xml` already carries in some form.

Usage:
    python retail_quest_inventory.py [--client DIR] [--java DIR] [--write | --check]
"""
from __future__ import annotations

import argparse
import csv
import importlib.util
import io
import json
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any, Iterable

sys.path.insert(0, str(Path(__file__).parent))
import aionpak  # noqa: E402
import bxml  # noqa: E402
from index_paks import entry_names  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parents[2]
STATIC_ROOT = REPO_ROOT / "game-server/data/static_data"
ITEM_TEMPLATES = STATIC_ROOT / "items/item_templates.xml"
COMPILER = REPO_ROOT / "scripts/e2e/compile-quest-plans.py"
CLASSIFIER = REPO_ROOT / "parity-artifacts/e2e/obtainable-quests.json"
OUTPUT = REPO_ROOT / "parity-artifacts/e2e/retail-quest-inventory.json"
SUMMARY = REPO_ROOT / "parity-artifacts/e2e/retail-quest-inventory.md"
RETAIL_EVIDENCE = REPO_ROOT / "parity-artifacts/e2e/retail-quest-evidence.json"
OVERRIDES = REPO_ROOT / "parity-artifacts/e2e/retail-quest-class-overrides.json"
DEFAULT_CLIENT = Path(r"C:\Program Files (x86)\Beyond Aion")

SCHEMA_VERSION = 1
# Client quest flags worth carrying verbatim: each one can move a quest out of D32 scope or change
# how it is offered, and none is in quest_data.xml in the same form.
CLIENT_FLAGS = (
    "category1", "category2", "extra_category", "mobile_event", "npcfaction_name", "target_type",
    "quest_repeat_cycle", "quest_permitted_worlds", "mentor_quest_type", "combineskill",
    "combine_skillpoint", "abyss_rank", "pcguild_level", "cannot_giveup", "can_report",
)
RACES = {"pc_light": "ELYOS", "pc_dark": "ASMODIANS", "pc_all": "PC_ALL"}
NUMBERED = re.compile(r"^(?P<base>[a-z_]+?)_?(?P<n>\d+)$")


# ----------------------------------------------------------------------------------------------
# Client readers
# ----------------------------------------------------------------------------------------------

def decode_member(data: bytes) -> ET.Element:
    return bxml.decode(data) if bxml.is_binary_xml(data) else ET.fromstring(data)


def pak_members(path: Path, wanted: Iterable[str]) -> dict[str, bytes]:
    """The named members of one archive, by lower-cased name."""
    wanted = {name.lower() for name in wanted}
    found = {entry.name.lower(): entry.data for entry in aionpak.read_pak(path) if entry.name.lower() in wanted}
    missing = wanted - set(found)
    if missing:
        raise ValueError(f"{path} lacks {sorted(missing)}")
    return found


def text_of(record: ET.Element, tag: str) -> str | None:
    value = record.findtext(tag)
    return value.strip() if value is not None and value.strip() else None


def numbered(record: ET.Element, prefix: str) -> list[str]:
    """Values of `<prefix>1>`, `<prefix>2>`... in index order."""
    pairs = []
    for child in record:
        if child.tag.startswith(prefix) and child.tag[len(prefix):].isdigit() and child.text:
            pairs.append((int(child.tag[len(prefix):]), child.text.strip()))
    return [value for _, value in sorted(pairs)]


def parse_client_quests(root: ET.Element) -> dict[int, dict[str, Any]]:
    quests = {}
    for record in root:
        quest_id = int(record.findtext("id"))
        drops = []
        for child in record:
            if child.tag.startswith("drop_monster_") and child.text:
                n = child.tag.rsplit("_", 1)[1]
                drops.append({
                    "index": int(n),
                    "monster": child.text.strip(),
                    "item": text_of(record, f"drop_item_{n}"),
                    "prob": int(text_of(record, f"drop_prob_{n}") or 0),
                    "eachMember": text_of(record, f"drop_each_member_{n}") == "1",
                })
        drops.sort(key=lambda drop: drop["index"])
        collect = []
        for value in numbered(record, "collect_item"):
            devname, _, count = value.partition(" ")
            collect.append({"devname": devname, "count": int(count or 1)})
        quests[quest_id] = {
            "id": quest_id,
            "desc": text_of(record, "desc"),
            "clientLevel": int(text_of(record, "client_level") or 0),
            "minLevel": int(text_of(record, "minlevel_permitted") or 0),
            "maxLevel": int(text_of(record, "maxlevel_permitted") or 0),
            "maxRepeat": int(text_of(record, "max_repeat_count") or 0),
            "race": RACES.get(text_of(record, "race_permitted") or "", text_of(record, "race_permitted")),
            "classPermitted": text_of(record, "class_permitted"),
            "finished": [quest_ref(v) for v in numbered(record, "finished_quest_cond")],
            "unfinished": [quest_ref(v) for v in numbered(record, "unfinished_quest_cond")],
            "noacquired": [quest_ref(v) for v in numbered(record, "noacquired_quest_cond")],
            "acquired": [quest_ref(v) for v in numbered(record, "acquired_quest_cond")],
            "collectItems": collect,
            "drops": [{k: v for k, v in drop.items() if k != "index"} for drop in drops],
            "flags": {tag: text_of(record, tag) for tag in CLIENT_FLAGS if text_of(record, tag) is not None},
        }
    return quests


def quest_ref(value: str) -> list[Any]:
    """`Q24112` -> [24112]; `Q2007,Q2022` -> [2007, 2022].

    One numbered condition is an AND group: the server carries `Q2007,Q2022,...` (Q2096) as a single
    `start_conditions` block with several `finished` entries. Separate numbered conditions are
    alternatives, carried as separate blocks (Q1365: Q1036 or Q14024)."""
    return [int(part[1:]) if part[:1] in "Qq" and part[1:].isdigit() else part
            for part in re.split(r"[\s,]+", value.strip()) if part]


def parse_quest_csv(text: str, source: str) -> dict[int, list[dict[str, Any]]]:
    rows: dict[int, list[dict[str, Any]]] = defaultdict(list)
    reader = csv.reader(io.StringIO(text))
    next(reader)
    for row in reader:
        row = [cell.strip() for cell in row]
        if not row or not row[0].isdigit():
            continue
        quest_id, progress, item, kind, source_name, num = row[:6]
        rows[int(quest_id)].append({
            "file": source,
            "progress": progress,
            "item": item or None,
            "sourceType": kind,
            "sourceName": source_name or None,
            "num": int(num) if num.isdigit() else num,
            "devnames": [name for name in row[6:] if name],
        })
    return rows


def parse_client_npcs(roots: Iterable[ET.Element]) -> dict[str, dict[str, Any]]:
    """lower-cased devname -> {id, desc}. The first record wins, as the client loads them."""
    npcs: dict[str, dict[str, Any]] = {}
    for root in roots:
        for record in root:
            name, npc_id = text_of(record, "name"), text_of(record, "id")
            if name and npc_id:
                npcs.setdefault(name.lower(), {"id": int(npc_id), "desc": text_of(record, "desc")})
    return npcs


def parse_strings(root: ET.Element) -> dict[str, str]:
    return {
        (record.findtext("name") or "").upper(): record.findtext("body") or ""
        for record in root
        if record.findtext("name")
    }


def client_dialog_ids(client: Path) -> set[int]:
    """Quest ids with a `quest_q<ID>.html` in any of the client's dialog archives."""
    ids = set()
    pattern = re.compile(r"(?:^|/)quest_q(\d+)\.html$", re.IGNORECASE)
    for pak in sorted((client / "Data" / "Dialogs").rglob("*.pak")):
        for name in entry_names(pak):
            match = pattern.search(name.replace("\\", "/"))
            if match:
                ids.add(int(match.group(1)))
    return ids


def load_client(client: Path) -> dict[str, Any]:
    quest_pak = pak_members(client / "Data/Quest/Quest.pak",
                            ("quest.xml", "quest_monster.csv", "quest_script_monster.csv"))
    npc_pak = pak_members(client / "Data/Npcs/Npcs.pak", ("client_npcs_npc.xml", "client_npcs_monster.xml"))
    strings = pak_members(client / "l10n/ENG/Data/Data.pak",
                          ("strings/client_strings_quest.xml", "strings/client_strings_npc.xml",
                           "strings/client_strings_monster.xml"))
    rows = parse_quest_csv(quest_pak["quest_monster.csv"].decode("utf-8-sig"), "quest_monster.csv")
    for quest_id, extra in parse_quest_csv(quest_pak["quest_script_monster.csv"].decode("utf-8-sig"),
                                           "quest_script_monster.csv").items():
        rows[quest_id].extend(extra)
    text = {}
    for member in strings.values():
        text.update(parse_strings(decode_member(member)))
    return {
        "quests": parse_client_quests(decode_member(quest_pak["quest.xml"])),
        "rows": dict(rows),
        "npcs": parse_client_npcs(decode_member(npc_pak[name])
                                  for name in ("client_npcs_npc.xml", "client_npcs_monster.xml")),
        "strings": text,
        "dialogs": client_dialog_ids(client),
    }


# ----------------------------------------------------------------------------------------------
# Server and Java readers
# ----------------------------------------------------------------------------------------------

def load_compiler():
    spec = importlib.util.spec_from_file_location("compile_quest_plans", COMPILER)
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module  # its dataclasses look themselves up there
    spec.loader.exec_module(module)
    return module


def load_items() -> tuple[dict[str, int], set[int]]:
    by_devname: dict[str, int] = {}
    ids: set[int] = set()
    for _, element in ET.iterparse(ITEM_TEMPLATES, events=("end",)):
        if element.tag == "item_template":
            item_id = int(element.attrib["id"])
            ids.add(item_id)
            cname = element.attrib.get("cName")
            if cname:
                by_devname.setdefault(cname.lower(), item_id)
            element.clear()
    return by_devname, ids


def load_npc_templates() -> dict[int, str]:
    names = {}
    for _, element in ET.iterparse(STATIC_ROOT / "npcs/npc_templates.xml", events=("end",)):
        if element.tag == "npc_template":
            names[int(element.attrib["npc_id"])] = element.attrib.get("name", "")
            element.clear()
    return names


def java_handler_ids(java_root: Path) -> set[int] | None:
    handlers = java_root / "game-server/data/handlers/quest"
    scripts = java_root / "game-server/data/static_data/quest_script_data"
    if not handlers.is_dir() or not scripts.is_dir():
        return None
    ids = {int(m.group(1)) for path in handlers.rglob("_*.java") if (m := re.match(r"^_(\d+)", path.stem))}
    for path in scripts.glob("*.xml"):
        ids.update(int(v) for v in re.findall(r'<\w+\s+id="(\d+)"', path.read_text(encoding="utf-8")))
    return ids


def java_head(java_root: Path) -> str | None:
    head = java_root / ".git/HEAD"
    if not head.exists():
        return None
    ref = head.read_text().strip()
    if ref.startswith("ref: "):
        target = java_root / ".git" / ref[5:]
        if target.exists():
            return target.read_text().strip()[:9]
        packed = java_root / ".git/packed-refs"
        if packed.exists():
            for line in packed.read_text().splitlines():
                if line.endswith(ref[5:]):
                    return line.split()[0][:9]
        return None
    return ref[:9]


# ----------------------------------------------------------------------------------------------
# The join
# ----------------------------------------------------------------------------------------------

class World:
    """What this server has: templates, spawns, items."""

    def __init__(self, npc_templates, spawned_ids, positions, dynamic_ids, items_by_devname, item_ids):
        self.npc_templates = npc_templates
        self.spawned_ids = spawned_ids
        self.positions = positions
        self.dynamic_ids = dynamic_ids
        self.items_by_devname = items_by_devname
        self.item_ids = item_ids

    def npc(self, npc_id: int | None) -> dict[str, Any]:
        if npc_id is None:
            return {"npcId": None, "template": False, "spawned": False, "maps": []}
        return {
            "npcId": npc_id,
            "name": self.npc_templates.get(npc_id),
            "template": npc_id in self.npc_templates,
            "spawned": npc_id in self.spawned_ids,
            "handlerSpawned": npc_id in self.dynamic_ids,
            "maps": sorted({p["mapId"] for p in self.positions.get(npc_id, [])}),
        }

    def item(self, devname: str | None) -> dict[str, Any]:
        item_id = self.items_by_devname.get((devname or "").lower())
        return {"devname": devname, "itemId": item_id, "template": item_id is not None}


# quest_monster.csv / quest_script_monster.csv source types. Only these name npcs. `itemUseArea` and
# `sensoryArea` name client areas; `goodsList` (a shop), `gatherSource` (a gather node) and `commonDrop`
# (hundreds of ordinary monsters) are other ways to get the item, not an npc the quest needs.
NPC_SOURCES = {"killedByUser", "questItemDropMonster", "dropMonster", "simpleQuest", "etcByNpc"}
OTHER_SUPPLY = {"goodsList", "gatherSource", "commonDrop"}


def unmet_sources(client_quest: dict[str, Any], steps: list[dict[str, Any]], npcs: dict[str, dict]) -> list[list[str]]:
    """Kill and drop sources with no spawned npc among their alternatives.

    A kill row is one source: any of its devnames spawned is enough, like the classifier's groups. An
    item is one source across quest.xml's drop and every csv row for it; it is met when any npc for it
    is spawned, or when the client names a shop, gather node or common drop for it as well."""
    by_item: dict[str, set[str]] = defaultdict(set)
    supplied: set[str] = set()
    kills: list[set[str]] = []
    for drop in client_quest["drops"]:
        by_item[(drop["item"] or drop["monster"]).lower()].add(drop["monster"].lower())
    for step in steps:
        names = {n.lower() for n in step["devnames"]}
        if step["sourceType"] in OTHER_SUPPLY and step["item"]:
            supplied.add(step["item"].lower())
        elif step["sourceType"] in NPC_SOURCES and names:
            if step["item"]:
                by_item[step["item"].lower()].update(names)
            else:
                kills.append(names)
    groups = [names for item, names in by_item.items() if item not in supplied] + kills
    unmet = {tuple(sorted(g)) for g in groups if not any(npcs[n]["spawned"] for n in g if n in npcs)}
    return sorted(list(g) for g in unmet)


def spellings(names: Iterable[str]) -> dict[str, str]:
    """lower-cased name -> one spelling. The client spells a devname differently from file to file
    (`LehparWaChD_18_An`, `lehparwachd_18_an`); the lookup ignores case, so keep the first in sort order."""
    out: dict[str, str] = {}
    for name in sorted(names):
        out.setdefault(name.lower(), name)
    return dict(sorted(out.items()))


def npc_entry(devname: str, client: dict[str, Any], world: World) -> dict[str, Any]:
    record = client["npcs"].get(devname.lower())
    entry = {"devname": devname, "clientId": record["id"] if record else None}
    entry.update(world.npc(record["id"] if record else None))
    if record and not entry.get("name"):
        entry["clientName"] = client["strings"].get((record["desc"] or "").upper())
    return entry


def server_facts(quest: ET.Element | None) -> dict[str, Any] | None:
    if quest is None:
        return None
    conditions = quest.findall("start_conditions")
    return {
        "name": quest.attrib.get("name"),
        "zone": quest.attrib.get("quest_zone"),
        "category": quest.attrib.get("category", "QUEST"),
        "minLevel": int(quest.attrib.get("minlevel_permitted", "0")),
        "maxRepeat": int(quest.attrib.get("max_repeat_count", "1")),
        "race": quest.attrib.get("race_permitted"),
        "finished": sorted({int(e.attrib["quest_id"]) for c in conditions for e in c.findall("finished")}),
        "unfinished": sorted({int(v) for c in conditions for e in c.findall("unfinished") for v in (e.text or "").split()}),
        "noacquired": sorted({int(v) for c in conditions for e in c.findall("noacquired") for v in (e.text or "").split()}),
        "collectItems": sorted(int(e.attrib["item_id"]) for e in quest.findall("./collect_items/collect_item")),
        "questDrops": sorted({(int(e.attrib["npc_id"]), int(e.attrib["item_id"])) for e in quest.findall("quest_drop")}),
    }


def flat_ids(values: list[Any]) -> list[int]:
    out = []
    for value in values:
        out.extend(value if isinstance(value, list) else [value])
    return sorted(v for v in out if isinstance(v, int))


def differences(client_quest: dict[str, Any], server: dict[str, Any] | None, npcs: dict[str, dict], items) -> list[dict[str, Any]]:
    """Where quest_data.xml disagrees with the client. Evidence for B, never a correction by itself."""
    if server is None:
        return [{"field": "quest_data", "client": "present", "server": "absent"}]
    diffs = []

    def compare(field, client_value, server_value):
        if client_value != server_value:
            diffs.append({"field": field, "client": client_value, "server": server_value})

    compare("minLevel", client_quest["minLevel"], server["minLevel"])
    if client_quest["race"] in ("ELYOS", "ASMODIANS") or server["race"] in ("ELYOS", "ASMODIANS"):
        compare("race", client_quest["race"] if client_quest["race"] != "PC_ALL" else None, server["race"])
    for field in ("finished", "unfinished", "noacquired"):
        compare(field, flat_ids(client_quest[field]), server[field])
    client_items = sorted(i for i in (items.get(c["devname"].lower()) for c in client_quest["collectItems"]) if i is not None)
    compare("collectItems", client_items, server["collectItems"])
    client_drops = sorted({(npcs[d["monster"].lower()]["npcId"], items.get((d["item"] or "").lower()))
                           for d in client_quest["drops"] if npcs.get(d["monster"].lower(), {}).get("npcId")})
    compare("questDrops", [list(d) for d in client_drops], [list(d) for d in server["questDrops"]])
    return diffs


def exclusions(quests: dict[int, dict[str, Any]]) -> dict[int, list[int]]:
    """quest -> the client quests that require it both unfinished and not acquired.

    That pair is how NCSoft retired a quest when a newer one replaced it: Q24113 needs Q2017 and Q2200
    neither done nor taken, so a character who ran the old mission is never offered the new quest.
    Two quests that name each other are alternatives (pick one), not a replacement; `mutual` says so.
    """
    out: dict[int, list[int]] = defaultdict(list)
    for quest_id, quest in quests.items():
        for excluded in set(flat_ids(quest["unfinished"])) & set(flat_ids(quest["noacquired"])):
            if excluded != quest_id:
                out[excluded].append(quest_id)
    return {k: sorted(v) for k, v in out.items()}


def disabled_chain(quest_id: int, quests: dict[int, dict[str, Any]], seen: frozenset = frozenset()) -> list[int]:
    """A chain of `finished` prerequisites from this quest to one the client sets to level 99.

    Level 99 is how the 4.8 client switches a quest off; everything after it in a chain can never be
    offered. Empty when no such chain exists. The numbered conditions are alternatives and each is an
    AND group (see `quest_ref`), so a quest is cut off only when every group holds a disabled quest."""
    quest = quests.get(quest_id)
    if quest is None or quest_id in seen:
        return []
    if quest["minLevel"] == 99:
        return [quest_id]
    groups = [[q for q in group if isinstance(q, int)] for group in quest["finished"]]
    if not groups:
        return []
    first = []
    for group in groups:
        chain = next((c for c in (disabled_chain(q, quests, seen | {quest_id}) for q in group) if c), [])
        if not chain:
            return []
        first = first or chain
    return [quest_id] + first


def build(client: dict[str, Any], classifier: dict[str, Any], server_quests: dict[int, ET.Element],
          world: World, java_ids: set[int] | None, evidence: dict[str, Any],
          overrides: dict[str, Any] | None = None) -> dict[str, Any]:
    availability = {entry["id"]: entry["availability"] for entry in classifier["quests"]}
    excluded_by = exclusions(client["quests"])
    targets = [entry for entry in classifier["quests"] if entry["availability"] == "no_handler"]
    entries = []
    for entry in targets:
        quest_id = entry["id"]
        client_quest = client["quests"].get(quest_id)
        server = server_facts(server_quests.get(quest_id))
        row: dict[str, Any] = {
            "id": quest_id,
            "name": server["name"] if server else None,
            "clientName": None,
            "zone": entry["zone"],
            "race": entry["race"],
            "javaHandler": None if java_ids is None else quest_id in java_ids,
            "clientDialog": quest_id in client["dialogs"],
        }
        if client_quest is None:
            row["client"] = None
            entries.append(row)
            continue
        row["clientName"] = client["strings"].get((client_quest["desc"] or "").upper())
        devnames = {d["monster"] for d in client_quest["drops"]}
        steps = client["rows"].get(quest_id, [])
        for step in steps:
            if step["sourceType"] in NPC_SOURCES:
                devnames.update(step["devnames"])
        npcs = {key: npc_entry(name, client, world) for key, name in spellings(devnames).items()}
        item_names = {c["devname"] for c in client_quest["collectItems"]}
        item_names.update(d["item"] for d in client_quest["drops"] if d["item"])
        item_names.update(s["item"] for s in steps if s["item"])
        items = {key: world.item(name) for key, name in spellings(item_names).items()}
        prerequisites = [
            {"id": q, "availability": availability.get(q, "absent"),
             "clientMinLevel": client["quests"][q]["minLevel"] if q in client["quests"] else None,
             "disabledChain": disabled_chain(q, client["quests"])}
            for q in flat_ids(client_quest["finished"])
        ]
        unmet = unmet_sources(client_quest, steps, npcs)
        readiness = {
            "npcs": len(npcs),
            "npcsWithoutClientId": sorted(n["devname"] for n in npcs.values() if n["clientId"] is None),
            "npcsWithoutTemplate": sorted(n["clientId"] for n in npcs.values() if n["clientId"] and not n["template"]),
            "npcsNotSpawned": sorted(n["npcId"] for n in npcs.values() if n["template"] and not n["spawned"]),
            "sourceGroupsUnmet": len(unmet),
            "sourcesUnmet": unmet,
            "itemsWithoutTemplate": sorted(i["devname"] for i in items.values() if not i["template"]),
            "prerequisitesWithoutHandler": [p["id"] for p in prerequisites if p["availability"] in ("no_handler", "absent")],
        }
        # A templateless alternative does not matter while a sibling of it is spawned, so readiness is
        # the unmet sources and the missing items, not every templateless devname.
        readiness["worldReady"] = not (readiness["sourceGroupsUnmet"] or readiness["itemsWithoutTemplate"])
        row["client"] = {
            key: client_quest[key]
            for key in ("clientLevel", "minLevel", "maxLevel", "maxRepeat", "race", "finished", "unfinished",
                        "noacquired", "acquired", "collectItems", "drops", "flags")
        }
        row["prerequisites"] = prerequisites
        row["excludedBy"] = [
            {"id": other, "availability": availability.get(other, "absent"),
             "mutual": quest_id in excluded_by.get(other, [])}
            for other in excluded_by.get(quest_id, [])
        ]
        row["steps"] = [{k: v for k, v in s.items() if k != "sourceName" or v} for s in steps]
        row["npcs"] = list(npcs.values())
        row["items"] = list(items.values())
        row["serverDiffs"] = differences(client_quest, server, npcs, world.items_by_devname)
        row["readiness"] = readiness
        if str(quest_id) in evidence:
            retail = row["retail"] = evidence[str(quest_id)]
            talk = [(npc_id, "giver") for npc_id in retail.get("questGivers", [])]
            talk += [(link["id"], "description") for link in retail.get("descriptionLinks", []) if link["kind"] == "npc"]
            seen = set()
            row["retailNpcs"] = []
            for npc_id, role in talk:
                if npc_id not in seen:
                    seen.add(npc_id)
                    row["retailNpcs"].append({"role": role, **world.npc(npc_id)})
        entries.append(row)

    known = set(server_quests)
    client_only = []
    for quest_id in sorted(set(client["quests"]) - known):
        quest = client["quests"][quest_id]
        client_only.append({
            "id": quest_id,
            "clientName": client["strings"].get((quest["desc"] or "").upper()),
            "zone": client["strings"].get((quest["flags"].get("category2") or "").upper(), quest["flags"].get("category2")),
            "category": quest["flags"].get("category1"),
            "race": quest["race"],
            "clientLevel": quest["clientLevel"],
            "minLevel": quest["minLevel"],
            "javaHandler": None if java_ids is None else quest_id in java_ids,
            "clientDialog": quest_id in client["dialogs"],
        })

    classify(entries, client["quests"], overrides or {})
    return {"quests": entries, "clientOnly": client_only}


# ----------------------------------------------------------------------------------------------
# RQ-04: classes A-E (docs/retail-quest-completion.md, "Classification")
# ----------------------------------------------------------------------------------------------

TEST_MARK = re.compile(r"\[test\]|data driven empty", re.IGNORECASE)
HIDDEN_MARK = re.compile(r"hidden quest", re.IGNORECASE)
# aioncodex prints "Player" before the player-facing summary, so a bare "Player" is an empty task text.
STUB_TEXT = re.compile(r"^\s*(?:Player)?\s*$|XXX|XP Test|View Cutscene|Quest Description Summary")


def classify(rows: list[dict[str, Any]], client_quests: dict[int, dict[str, Any]], overrides: dict[str, Any]) -> None:
    """Give each row `class` and `classReasons`. Rules first, in the order D, C, E, B, A; then the
    reviewed overrides in `retail-quest-class-overrides.json`, which always say why."""
    by_id = {row["id"]: row for row in rows}
    for row in rows:
        row["class"], row["classReasons"] = rule_class(row, client_quests)
    # A quest whose only way in is a superseded or out-of-scope quest is itself never offered.
    # The 2.x campaigns were retired whole: the 4.x quests name the missions they replace, and the
    # rest of the same campaign (same zone, a four-digit mission id) goes with them.
    retired: dict[str, list[int]] = defaultdict(list)
    for row in rows:
        if is_old_mission(row) and row["class"] == "C" and row["classReasons"][0].startswith("excluded"):
            retired[row["zone"]].append(row["id"])
    for row in rows:
        if is_old_mission(row) and row["class"] not in ("C", "D") and retired.get(row["zone"]):
            row["class"] = "C"
            row["classReasons"] = [f"part of the 2.x {row['zone']} campaign, retired with "
                                   f"Q{', Q'.join(map(str, retired[row['zone']][:6]))}"
                                   + (" and others" if len(retired[row["zone"]]) > 6 else "")]
    # A quest whose every way in passes a superseded or out-of-scope quest is never offered either.
    changed = True
    while changed:
        changed = False
        for row in rows:
            if row["class"] in ("C", "D"):
                continue
            blocker = blocking(row, lambda q: by_id.get(q, {}).get("class") in ("C", "D"))
            if blocker:
                row["class"] = by_id[blocker]["class"]
                row["classReasons"] = [f"every prerequisite group needs Q{blocker} or another class C/D quest"
                                       f" (Q{blocker} is class {row['class']})"]
                changed = True
    for row in rows:
        override = overrides.get(str(row["id"]))
        if override:
            row["classReasons"] = [f"reviewed: {override['reason']}"] + (
                [f"rules said {row['class']}: " + "; ".join(row["classReasons"])] if row["class"] != override["class"] else [])
            row["class"] = override["class"]


def is_old_mission(row: dict[str, Any]) -> bool:
    return row["id"] < 10000 and (row.get("client") or {}).get("flags", {}).get("category1") == "mission"


def blocking(row: dict[str, Any], blocked) -> int | None:
    """A quest that blocks every `finished` group of this one, or None. Groups are alternatives and
    each is an AND (see `quest_ref`), so one blocked member closes a group."""
    groups = [[q for q in group if isinstance(q, int)] for group in (row.get("client") or {}).get("finished", [])]
    if not groups:
        return None
    first = None
    for group in groups:
        member = next((q for q in group if blocked(q)), None)
        if member is None:
            return None
        first = first or member
    return first


def rule_class(row: dict[str, Any], client_quests: dict[int, dict[str, Any]]) -> tuple[str, list[str]]:
    client = row.get("client")
    retail = row.get("retail")
    names = " / ".join(filter(None, (row.get("name"), row.get("clientName"), (retail or {}).get("title"))))
    if client is None:
        return "E", ["not in the 4.8 client's quest.xml"]

    # D: out of scope.
    if row["zone"] == "Test zone" or (retail or {}).get("category") == "Test zone":
        return "D", ["test zone"]
    if TEST_MARK.search(names):
        return "D", [f"test quest ({names})"]
    if client["minLevel"] == 99:
        return "D", ["level 99 in the client"]
    if client["flags"].get("mobile_event") or client["flags"].get("category1") == "event":
        return "D", ["event quest"]

    # C: superseded in 4.8.
    replaced = [e["id"] for e in row.get("excludedBy", []) if not e["mutual"] and e["id"] > row["id"]]
    if replaced and row["id"] < 10000:
        return "C", [f"excluded (unfinished and not acquired) by Q{', Q'.join(map(str, replaced))}"]
    if replaced:
        # Among 4.x quests a one-way exclusion can be a branch (take this or that), not a retirement.
        return "E", [f"a 4.x quest excluded one way by Q{', Q'.join(map(str, replaced))}: replaced, or a branch?"]
    chains = {p["id"]: p["disabledChain"] for p in row.get("prerequisites", [])}
    cut = blocking(row, lambda q: bool(chains.get(q)))
    if cut:
        chain = " -> ".join(f"Q{q}" for q in chains[cut])
        return "C", [f"prerequisite chain ends at a quest the client sets to level 99 ({chain})"]

    # E: the evidence is missing or disagrees.
    if HIDDEN_MARK.search(names):
        return "E", [f"hidden client quest, no player-facing steps ({names})"]
    description = (retail or {}).get("description")
    if retail and retail.get("found") and (description is None or STUB_TEXT.search(description)):
        return "E", [f"aioncodex /48/ task text is a stub or placeholder ({description!r}): an unfinished or internal client entry"]
    reasons = []
    if retail is None:
        reasons.append("no aioncodex /48/ evidence stored yet")
    elif not retail.get("found"):
        reasons.append(f"aioncodex /48/ has no page (HTTP {retail.get('status')})")
    if not row["clientDialog"]:
        reasons.append("the client ships no dialog file for it")
    if reasons:
        return "E", reasons

    # B: live, but something it needs is missing.
    ready = row["readiness"]
    ids = {n["devname"].lower(): n for n in row.get("npcs", [])}
    missing = []
    for group in ready["sourcesUnmet"]:
        described = ", ".join(
            f"{name}={ids[name]['clientId']}" + ("" if ids[name]["template"] else " (no template)")
            if name in ids and ids[name]["clientId"] else f"{name} (not in the client npc tables)"
            for name in group)
        missing.append(f"kill/drop source with nothing spawned: {described}")
    if ready["itemsWithoutTemplate"]:
        missing.append("items without a template: " + ", ".join(ready["itemsWithoutTemplate"]))
    for npc in row.get("retailNpcs", []):
        # A giver must stand somewhere. An npc merely named in the task text is weaker evidence: it
        # may be spawned by an instance, a siege or another handler, so it is marked as such.
        where = "quest giver" if npc["role"] == "giver" else "npc named in the task text"
        if not npc["template"]:
            missing.append(f"{where} {npc['npcId']} has no template")
        elif not npc["spawned"]:
            missing.append(f"{where} {npc['npcId']} ({npc.get('name')}) has no static spawn")
    for diff in row.get("serverDiffs", []):
        if diff["field"] in ("questDrops", "collectItems", "quest_data"):
            missing.append(f"quest_data.xml {diff['field']} differs from the client")
    if missing:
        return "B", missing
    return "A", ["live in 4.8 (aioncodex /48/, client dialog); every npc and item it names exists and is spawned"]


def summarize(document: dict[str, Any]) -> dict[str, Any]:
    quests = document["quests"]
    return {
        "noHandler": len(quests),
        "inClient": sum(1 for q in quests if q.get("client")),
        "javaHandlerFound": sum(1 for q in quests if q["javaHandler"]),
        "clientDialog": sum(1 for q in quests if q["clientDialog"]),
        "worldReady": sum(1 for q in quests if q.get("readiness", {}).get("worldReady")),
        "withServerDiffs": sum(1 for q in quests if q.get("serverDiffs")),
        "clientOnly": len(document["clientOnly"]),
        "clientOnlyWithDialog": sum(1 for q in document["clientOnly"] if q["clientDialog"]),
        "retailEvidence": sum(1 for q in quests if q.get("retail", {}).get("found")),
        "classes": {c: sum(1 for q in quests if q.get("class") == c) for c in "ABCDE"},
    }


def markdown(document: dict[str, Any]) -> str:
    counts = document["counts"]
    by_zone: dict[str, list[dict]] = defaultdict(list)
    for quest in document["quests"]:
        by_zone[quest["zone"] or "?"].append(quest)
    lines = [
        "# Retail quest inventory (RQ-01, RQ-03, RQ-04)",
        "",
        "Generated by `tools/client-extract/retail_quest_inventory.py`; do not hand-edit. The data is",
        "`retail-quest-inventory.json` beside this file. What each column means and what it does not decide:",
        "`docs/retail-quest-completion.md`.",
        "",
        f"- **{counts['noHandler']}** no-handler quests (classifier), **{counts['inClient']}** of them in the 4.8 client's `quest.xml`.",
        f"- Java handler found after all: **{counts['javaHandlerFound']}**. Client dialog file shipped: **{counts['clientDialog']}**.",
        f"- World ready (every item has a template, every kill/drop source has a spawned alternative): **{counts['worldReady']}**.",
        f"- `quest_data.xml` disagrees with the client somewhere: **{counts['withServerDiffs']}**.",
        f"- Client quests `quest_data.xml` does not carry at all: **{counts['clientOnly']}** "
        f"({counts['clientOnlyWithDialog']} with a dialog file).",
        f"- aioncodex `/48/` page found: **{counts['retailEvidence']}**.",
        "- Classes: " + ", ".join(f"**{c}** {n}" for c, n in counts["classes"].items()) + ".",
        "",
        "## By zone",
        "",
        "| Zone | Quests | A | B | C | D | E | Elyos | Asmo | Both | World ready | Missing template | Source unspawned | Missing item | Prereq has no handler | Data diffs |",
        "|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|",
    ]
    for zone, quests in sorted(by_zone.items(), key=lambda kv: (-len(kv[1]), kv[0])):
        ready = [q.get("readiness", {}) for q in quests]
        lines.append("| {} | {} | {} | {} | {} | {} | {} | {} | {} | {} | {} | {} | {} | {} | {} | {} |".format(
            zone, len(quests), *(sum(q.get("class") == c for q in quests) for c in "ABCDE"),
            sum(q["race"] == "ELYOS" for q in quests),
            sum(q["race"] == "ASMODIANS" for q in quests),
            sum(q["race"] not in ("ELYOS", "ASMODIANS") for q in quests),
            sum(bool(r.get("worldReady")) for r in ready),
            sum(bool(r.get("npcsWithoutTemplate") or r.get("npcsWithoutClientId")) for r in ready),
            sum(bool(r.get("sourceGroupsUnmet")) for r in ready),
            sum(bool(r.get("itemsWithoutTemplate")) for r in ready),
            sum(bool(r.get("prerequisitesWithoutHandler")) for r in ready),
            sum(bool(q.get("serverDiffs")) for q in quests),
        ))
    zones = Counter(q["zone"] for q in document["clientOnly"])
    lines += [
        "",
        "## Client quests absent from `quest_data.xml`, by client zone",
        "",
        "| Client zone | Quests |",
        "|---|---:|",
    ]
    for zone, count in sorted(zones.items(), key=lambda kv: (-kv[1], str(kv[0]))):
        lines.append(f"| {zone} | {count} |")
    titles = {
        "A": "Class A: live, and everything it names exists and is spawned",
        "B": "Class B: live, something it needs is missing",
        "C": "Class C: superseded in 4.8",
        "E": "Class E: the evidence disagrees or is missing",
    }
    for cls, title in titles.items():
        rows = sorted((q for q in document["quests"] if q.get("class") == cls), key=lambda q: (q["zone"] or "", q["id"]))
        lines += ["", f"## {title} ({len(rows)})", "", "| Quest | Name | Zone | Race | Level | Why |", "|---|---|---|---|---:|---|"]
        for q in rows:
            why = "; ".join(q["classReasons"]).replace("|", "/")
            level = (q.get("client") or {}).get("minLevel", "")
            lines.append(f"| {q['id']} | {q['name']} | {q['zone']} | {q['race'] or ''} | {level} | {why} |")
    d_reasons = Counter(q["classReasons"][0].split(" (")[0] for q in document["quests"] if q.get("class") == "D")
    lines += ["", f"## Class D: out of scope ({sum(d_reasons.values())})", ""]
    lines += [f"- {reason}: {count}" for reason, count in sorted(d_reasons.items(), key=lambda kv: (-kv[1], kv[0]))]
    return "\n".join(lines) + "\n"


def generate(client_root: Path, java_root: Path) -> tuple[str, str]:
    compiler = load_compiler()
    client = load_client(client_root)
    classifier = json.loads(CLASSIFIER.read_text(encoding="utf-8-sig"))
    server_quests = compiler.load_quests()
    relevant = {npc["id"] for npc in client["npcs"].values()}
    dynamic = compiler.load_dynamic_spawn_ids()
    spawned, positions = compiler.load_spawns(relevant, dynamic)
    items_by_devname, item_ids = load_items()
    world = World(load_npc_templates(), spawned, positions, dynamic, items_by_devname, item_ids)
    java_ids = java_handler_ids(java_root)
    evidence = json.loads(RETAIL_EVIDENCE.read_text(encoding="utf-8")) if RETAIL_EVIDENCE.exists() else {}
    overrides = json.loads(OVERRIDES.read_text(encoding="utf-8"))["quests"] if OVERRIDES.exists() else {}
    body = build(client, classifier, server_quests, world, java_ids, evidence.get("quests", {}), overrides)
    document = {
        "schemaVersion": SCHEMA_VERSION,
        "generatedBy": "tools/client-extract/retail_quest_inventory.py",
        "sources": {
            "client": "Data/Quest/Quest.pak, Data/Npcs/Npcs.pak, l10n/ENG/Data/Data.pak, Data/Dialogs/**/*.pak",
            "classifier": "parity-artifacts/e2e/obtainable-quests.json",
            "javaCommit": java_head(java_root),
        },
        "counts": summarize(body),
        **body,
    }
    return json.dumps(document, indent=1, ensure_ascii=False) + "\n", markdown(document)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--client", type=Path, default=Path(os.environ.get("AION_CLIENT_ROOT", DEFAULT_CLIENT)))
    parser.add_argument("--java", type=Path,
                        default=Path(os.environ.get("BEYOND_AION_JAVA_ROOT", REPO_ROOT.parent / "aion-server")))
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--write", action="store_true", help="write the inventory and its summary")
    mode.add_argument("--check", action="store_true", help="fail when the checked-in inventory is stale")
    args = parser.parse_args()
    if not (args.client / "Data/Quest/Quest.pak").exists():
        print(f"no 4.8 client at {args.client}", file=sys.stderr)
        return 2
    inventory, summary = generate(args.client, args.java)
    if args.check:
        stale = [path for path, text in ((OUTPUT, inventory), (SUMMARY, summary))
                 if not path.exists() or path.read_text(encoding="utf-8") != text]
        for path in stale:
            print(f"stale: {path.relative_to(REPO_ROOT).as_posix()}", file=sys.stderr)
        if not stale:
            print("retail quest inventory is current")
        return 1 if stale else 0
    OUTPUT.write_text(inventory, encoding="utf-8", newline="\n")
    SUMMARY.write_text(summary, encoding="utf-8", newline="\n")
    counts = json.loads(inventory)["counts"]
    print(f"wrote {OUTPUT.relative_to(REPO_ROOT).as_posix()}: " + ", ".join(f"{k} {v}" for k, v in counts.items()))
    return 0


if __name__ == "__main__":
    sys.exit(main())
