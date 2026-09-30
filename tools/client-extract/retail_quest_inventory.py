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


def quest_ref(value: str) -> Any:
    """`Q24112` -> 24112. Conditions can hold several quests (`Q1 Q2`); keep those as a list."""
    ids = [int(part[1:]) if part[:1] in "Qq" and part[1:].isdigit() else part for part in value.split()]
    return ids[0] if len(ids) == 1 else ids


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


def build(client: dict[str, Any], classifier: dict[str, Any], server_quests: dict[int, ET.Element],
          world: World, java_ids: set[int] | None, evidence: dict[str, Any]) -> dict[str, Any]:
    availability = {entry["id"]: entry["availability"] for entry in classifier["quests"]}
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
            devnames.update(step["devnames"])
        npcs = {key: npc_entry(name, client, world) for key, name in spellings(devnames).items()}
        item_names = {c["devname"] for c in client_quest["collectItems"]}
        item_names.update(d["item"] for d in client_quest["drops"] if d["item"])
        item_names.update(s["item"] for s in steps if s["item"])
        items = {key: world.item(name) for key, name in spellings(item_names).items()}
        prerequisites = [
            {"id": q, "availability": availability.get(q, "absent")}
            for q in flat_ids(client_quest["finished"])
        ]
        # A kill or drop source counts as met when any alternative of it is spawned: the client lists
        # several devnames per row, of which one spawned is enough, exactly like the classifier's groups.
        # quest.xml's drop and the csv row for the same drop are one source, not two.
        sources = {frozenset(n.lower() for n in group)
                   for group in [[d["monster"]] for d in client_quest["drops"]] + [s["devnames"] for s in steps]
                   if group}
        unmet = [group for group in sources if not any(npcs[n]["spawned"] for n in group)]
        readiness = {
            "npcs": len(npcs),
            "npcsWithoutClientId": sorted(n["devname"] for n in npcs.values() if n["clientId"] is None),
            "npcsWithoutTemplate": sorted(n["clientId"] for n in npcs.values() if n["clientId"] and not n["template"]),
            "npcsNotSpawned": sorted(n["npcId"] for n in npcs.values() if n["template"] and not n["spawned"]),
            "sourceGroupsUnmet": len(unmet),
            "itemsWithoutTemplate": sorted(i["devname"] for i in items.values() if not i["template"]),
            "prerequisitesWithoutHandler": [p["id"] for p in prerequisites if p["availability"] in ("no_handler", "absent")],
        }
        readiness["worldReady"] = not (readiness["npcsWithoutClientId"] or readiness["npcsWithoutTemplate"]
                                       or readiness["sourceGroupsUnmet"] or readiness["itemsWithoutTemplate"])
        row["client"] = {
            key: client_quest[key]
            for key in ("clientLevel", "minLevel", "maxLevel", "maxRepeat", "race", "finished", "unfinished",
                        "noacquired", "acquired", "collectItems", "drops", "flags")
        }
        row["prerequisites"] = prerequisites
        row["steps"] = [{k: v for k, v in s.items() if k != "sourceName" or v} for s in steps]
        row["npcs"] = list(npcs.values())
        row["items"] = list(items.values())
        row["serverDiffs"] = differences(client_quest, server, npcs, world.items_by_devname)
        row["readiness"] = readiness
        if str(quest_id) in evidence:
            row["retail"] = evidence[str(quest_id)]
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

    return {"quests": entries, "clientOnly": client_only}


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
    }


def markdown(document: dict[str, Any]) -> str:
    counts = document["counts"]
    by_zone: dict[str, list[dict]] = defaultdict(list)
    for quest in document["quests"]:
        by_zone[quest["zone"] or "?"].append(quest)
    lines = [
        "# Retail quest inventory (RQ-01)",
        "",
        "Generated by `tools/client-extract/retail_quest_inventory.py`; do not hand-edit. The data is",
        "`retail-quest-inventory.json` beside this file. What each column means and what it does not decide:",
        "`docs/retail-quest-completion.md`.",
        "",
        f"- **{counts['noHandler']}** no-handler quests (classifier), **{counts['inClient']}** of them in the 4.8 client's `quest.xml`.",
        f"- Java handler found after all: **{counts['javaHandlerFound']}**. Client dialog file shipped: **{counts['clientDialog']}**.",
        f"- World ready (every client npc and item has a template, every kill/drop source has a spawn): **{counts['worldReady']}**.",
        f"- `quest_data.xml` disagrees with the client somewhere: **{counts['withServerDiffs']}**.",
        f"- Client quests `quest_data.xml` does not carry at all: **{counts['clientOnly']}** "
        f"({counts['clientOnlyWithDialog']} with a dialog file).",
        "",
        "## By zone",
        "",
        "| Zone | Quests | Elyos | Asmo | Both | World ready | Missing template | Source unspawned | Missing item | Prereq has no handler | Data diffs |",
        "|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|",
    ]
    for zone, quests in sorted(by_zone.items(), key=lambda kv: (-len(kv[1]), kv[0])):
        ready = [q.get("readiness", {}) for q in quests]
        lines.append("| {} | {} | {} | {} | {} | {} | {} | {} | {} | {} | {} |".format(
            zone, len(quests),
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
    body = build(client, classifier, server_quests, world, java_ids, evidence.get("quests", {}))
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
