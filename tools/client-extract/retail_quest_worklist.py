#!/usr/bin/env python3
"""The D32 worklist: every quest to implement, one line each, with a Status column the maintainer owns.

WHY THIS EXISTS
---------------
`retail_quest_inventory.py` classifies the 440 no-handler quests. This turns the ones D32 implements
(classes A and B) into a review list, `docs/retail-quest-worklist.md`: one line per quest with its 4.8
evidence and a link to review it online. Quests the rules put in A, B or E but a reviewed override
moved out are listed too, marked Rejected with the reason, so nothing leaves the list silently.

THE CROSS-CHECK
---------------
Each line also shows two sources independent of the 4.8 client, cached in
`parity-artifacts/e2e/retail-quest-crosscheck.json` so the list can be rebuilt anywhere:

* **the 5.8 retail server** (`Map/XML/quest.xml` and `Quest_Simple*.xml`): whether retail still ran the
  quest in 5.8 (a level, or 999 when switched off by then), NCSoft's own dev note (its version tag,
  such as "4.7", is shown), and the simple-quest type retail implemented it with, which is a hint for
  the handler's form. 5.8 is later than 4.8, so "switched off by 5.8" does not mean "not in 4.8";
  the 4.8 client decides that.
* **aion.fandom.com**: the quest's page where one exists (the wiki covers few 4.x quests).

`--refresh` rebuilds that cache from the 5.8 data and `run/fandom/quests.json` (written by the fandom
lookup); without it the cache is only read.

THE STATUS COLUMN
-----------------
Blank, `Rejected` or `Done`. The generator keeps whatever the file already says for a quest, so the
maintainer's marks survive regeneration; a quest a reviewed override rejects starts as `Rejected`.

Usage:
    python retail_quest_worklist.py [--refresh [--retail58 DIR]] [--check]
"""
from __future__ import annotations

import argparse
import json
import re
import string
import sys
from pathlib import Path
from typing import Any

sys.path.insert(0, str(Path(__file__).parent))

REPO_ROOT = Path(__file__).resolve().parents[2]
INVENTORY = REPO_ROOT / "parity-artifacts/e2e/retail-quest-inventory.json"
CROSSCHECK = REPO_ROOT / "parity-artifacts/e2e/retail-quest-crosscheck.json"
FANDOM_CACHE = REPO_ROOT / "run/fandom/quests.json"
WORKLIST = REPO_ROOT / "docs/retail-quest-worklist.md"
READY = REPO_ROOT / "docs/retail-quest-ready.md"
DEFAULT_58 = Path("D:/Aion58ServerTesting/Server/Map/XML")
FANDOM = "https://aion.fandom.com/wiki/"

# The batch order the maintainer approved on 2026-09-30 (docs/retail-quest-completion.md, RQ-04).
BATCHES = [
    ("1 Altgard pilot", {"Altgard"}),
    ("2 Asmodian path", {"Morheim", "Beluslan", "Brusthonin"}),
    ("3 Elyos path", {"Verteron", "Eltnen", "Heiron", "Theobomos"}),
    ("4 Instance entry", {"Fire Temple", "Nochsana Training Camp", "Dark Poeta", "Draupnir Cave", "Theobomos Lab",
                          "Beshmundir Temple"}),
    ("5 Cities, Reshanta", {"Sanctum", "Pandaemonium", "Reshanta", "Ishalgen"}),
]
LAST_BATCH = "6 Level cap"
STATUSES = ("Rejected", "Done")


def batch_of(zone: str) -> str:
    return next((name for name, zones in BATCHES if zone in zones), LAST_BATCH)


def batch_order(row: dict[str, Any]) -> tuple:
    return (batch_of(row["zone"]), row["zone"], row["id"])


# ----------------------------------------------------------------------------------------------
# The cross-check cache
# ----------------------------------------------------------------------------------------------

def refresh(ids: set[int], retail58: Path) -> dict[str, Any]:
    from audit_missing_adds import read_text  # UTF-16 aware; only needed with the 5.8 data

    quests = {}
    for match in re.finditer(r"<quest>(.*?)</quest>", read_text(retail58 / "quest.xml"), re.S):
        body = match.group(1)
        quest_id = int(re.search(r"<id>(\d+)</id>", body).group(1))
        if quest_id in ids:
            fields = dict(re.findall(r"<(\w+)>([^<]*)</\1>", body))
            quests[quest_id] = {"minLevel": int(fields.get("minlevel_permitted", "0")),
                                "devNote": " ".join((fields.get("dev_name") or "").split())}
    for path in sorted(retail58.glob("Quest_Simple*.xml")):
        kind = path.stem.replace("Quest_Simple", "")
        for match in re.finditer(r'<id id="(\d+)">(.*?)</id>', read_text(path), re.S):
            quest_id = int(match.group(1))
            if quest_id in quests:
                fields = dict(re.findall(r"<(\w+)>([^<]*)</\1>", match.group(2)))
                fields.pop("dev_name", None)
                quests[quest_id].setdefault("simple", []).append({"type": kind, **fields})
    fandom = json.loads(FANDOM_CACHE.read_text(encoding="utf-8")) if FANDOM_CACHE.exists() else {}
    out = {}
    for quest_id in sorted(ids):
        row: dict[str, Any] = {"retail58": quests.get(quest_id)}
        page = fandom.get(str(quest_id), {})
        if page.get("matchedById"):
            hit = page["hits"][0]
            row["fandom"] = {"title": hit["title"], "archived": "Category:Archived" in hit["categories"]}
        out[str(quest_id)] = row
    return {"generatedBy": "tools/client-extract/retail_quest_worklist.py --refresh",
            "sources": {"retail58": "5.8 retail server Map/XML: quest.xml, Quest_Simple*.xml",
                        "fandom": "aion.fandom.com MediaWiki API, pages whose infobox number is the quest id"},
            "quests": out}


# ----------------------------------------------------------------------------------------------
# The document
# ----------------------------------------------------------------------------------------------

def existing_statuses(text: str) -> dict[int, str]:
    statuses = {}
    for line in text.splitlines():
        match = re.match(r"^\|\s*([^|]*?)\s*\|\s*\[?Q(\d+)", line)
        if match and match.group(1) in STATUSES:
            statuses[int(match.group(2))] = match.group(1)
    return statuses


def cell(text: str) -> str:
    return " ".join(str(text).split()).replace("|", "/")


def version_tag(note: str) -> str:
    tags = sorted(set(re.findall(r"\b([2-5]\.\d)\b", note or "")))
    return f", NCSoft note tagged {'/'.join(tags)}" if tags else ""


def evidence(row: dict[str, Any], cross: dict[str, Any]) -> str:
    parts = []
    client = row["client"]
    parts.append(f"4.8 client L{client['minLevel']}" + (", dialog" if row["clientDialog"] else ", no dialog"))
    retail = row.get("retail") or {}
    givers = [n for n in row.get("retailNpcs", []) if n["role"] == "giver"]
    if givers:
        parts.append("/48/ giver " + ", ".join(f"{string.capwords(n.get('name') or '') or n['npcId']} ({n['npcId']})"
                                               for n in givers))
    elif retail.get("found"):
        parts.append("/48/ page" + (" (granted mission)" if client["flags"].get("category1") == "mission" else ""))
    r58 = cross.get("retail58")
    if r58 is None:
        parts.append("absent from 5.8 retail")
    else:
        live = "still live" if r58["minLevel"] not in (99, 999) else "switched off"
        simple = "/".join(s["type"] for s in r58.get("simple", []))
        parts.append(f"5.8 retail: {live}" + (f", Simple{simple}" if simple else "") + version_tag(r58["devNote"]))
    if cross.get("fandom"):
        parts.append("fandom page" + (" (archived)" if cross["fandom"]["archived"] else ""))
    return "; ".join(parts)


def needs(row: dict[str, Any]) -> str:
    if row["class"] != "B":
        return ""
    short = []
    for reason in row["classReasons"]:
        if reason.startswith("reviewed:"):
            continue
        reason = reason.replace("npc named in the task text", "task-text npc")
        reason = reason.replace("kill/drop source with nothing spawned", "no spawn for")
        short.append(reason)
    return "; ".join(short)


def links(row: dict[str, Any], cross: dict[str, Any]) -> str:
    out = [f"[aioncodex]({(row.get('retail') or {}).get('url') or f'https://aioncodex.com/48/quest/{row['id']}/?sl=1'})"]
    if cross.get("fandom"):
        out.append(f"[fandom]({FANDOM}{cross['fandom']['title'].replace(' ', '_')})")
    return " ".join(out)


def line(row: dict[str, Any], cross: dict[str, Any], status: str) -> str:
    rejected = status == "Rejected" or row["class"] in ("C", "D")
    done = row.get("implemented")
    if done and not rejected:
        verdict = f"done: {done['form']}; SIM test {done['test']}; deviation {done['deviation']}"
    else:
        verdict = (next((r[len("reviewed: "):] for r in row["classReasons"] if r.startswith("reviewed:")), "")
                   if rejected else needs(row) or "ready: every npc and item it names is here")
    return "| {} | Q{} | {} | {} | {} | {} | {} | {} | {} | {} |".format(
        status, row["id"], cell(row["name"] or row["clientName"]), cell(row["zone"]),
        {"ELYOS": "Elyos", "ASMODIANS": "Asmo"}.get(row["race"], "Both"), row["client"]["minLevel"],
        batch_of(row["zone"]), cell(verdict), cell(evidence(row, cross)), links(row, cross))


HEADER = ("| Status | Quest | Name | Zone | Race | Lvl | Batch | Verdict / what it needs | 4.8 evidence | Review |\n"
          "|---|---|---|---|---|---:|---|---|---|---|")


def render(document: dict[str, Any], crosscheck: dict[str, Any], statuses: dict[int, str]) -> str:
    rows = [q for q in document["quests"] if q.get("ruleClass") in ("A", "B", "E") or q["class"] in ("A", "B")]
    cross = crosscheck["quests"]

    def status(row):
        if row["id"] in statuses:
            return statuses[row["id"]]
        if row.get("implemented"):
            return "Done"
        return "Rejected" if row["class"] in ("C", "D") else ""

    sections = [
        ("A", "Class A: live in 4.8, everything it names is here", [q for q in rows if q["class"] == "A"]),
        ("B", "Class B: live in 4.8, something it needs is missing here", [q for q in rows if q["class"] == "B"]),
        ("R", "Reviewed out: the rules put these in A, B or E, and the per-quest review rejected them",
         [q for q in rows if q["class"] in ("C", "D")]),
    ]
    counts = {key: len(items) for key, _, items in sections}
    done = sum(1 for q in rows if status(q) == "Done")
    lines = [
        "# D32 retail quest worklist",
        "",
        "One line per quest that D32 implements. **The Status column is yours**: leave it blank, or write",
        "`Rejected` or `Done`. Regenerating keeps what you wrote",
        "(`python tools/client-extract/retail_quest_worklist.py`; the plan and rules are in",
        "[retail-quest-completion.md](retail-quest-completion.md)).",
        "",
        f"**{counts['A']}** class A and **{counts['B']}** class B to implement, **{done}** done; "
        f"**{counts['R']}** rejected by the per-quest review.",
        "",
        "How each quest was checked for 4.8:",
        "",
        "- **4.8 client:** shipped and switched on (not level 99), with a dialog file, and not retired by a",
        "  newer quest or a retired campaign (the C rules in the plan).",
        "- **aioncodex `/48/`:** a page with real task text and, for a quest, its giver. The Review link",
        "  opens it.",
        "- **5.8 retail server:** whether retail still ran it in 5.8, NCSoft's own dev note (its version tag",
        "  is shown, such as 4.7), and the simple-quest type retail used (`SimpleHunt`, `SimpleTalk`, ...),",
        "  a hint for the handler's form. \"Switched off\" means switched off by 5.8, which is after 4.8,",
        "  and is not a reason to reject: the 4.8 client marks the quests switched off in 4.8 with level 99",
        "  (2,492 of them), and none of these is marked.",
        "- **Removed maps:** nothing it needs may stand only in a map gone in 4.8 (Tiamaranta, Sarpan,",
        "  Katalam, Danaria), per the fandom wiki's removal pages.",
        "- **fandom:** the quest's page where one exists (the wiki has few 4.x quests).",
        "",
        "The batches follow the approved order. Rejected lines say why in the Verdict column; their",
        "evidence is in `parity-artifacts/e2e/retail-quest-class-overrides.json`.",
    ]
    for key, title, items in sections:
        items = sorted(items, key=batch_order)
        lines += ["", f"## {title} ({len(items)})", "", HEADER]
        lines += [line(q, cross.get(str(q["id"]), {}), status(q)) for q in items]
    return "\n".join(lines) + "\n"


# The ready list: class A quests that pass the lighter bar the maintainer chose on 2026-09-30.
STUB = re.compile(r"^\s*(?:Player)?\s*$|XXX|XP Test|View Cutscene|Quest Description Summary")


def light_bar(row: dict[str, Any], cross: dict[str, Any]) -> list[str]:
    """The checks a quest fails, empty when it passes all four:
    1. active in the 4.8 client (not level 99) with a dialog file;
    2. still active in retail 5.8 (not level 99 or 999), which also means it was not retired in 4.x;
    3. real task text on aioncodex /48/ (not a placeholder or a cutscene trigger);
    4. every npc and item it names exists and is spawned here.
    That Java has no handler is given: every row comes from the classifier's no_handler set."""
    failed = []
    client = row.get("client")
    if client is None or client["minLevel"] == 99 or not row["clientDialog"]:
        failed.append("4.8 client")
    retail58 = cross.get("retail58")
    if retail58 is None or retail58["minLevel"] in (99, 999):
        failed.append("5.8 retail")
    description = (row.get("retail") or {}).get("description")
    if description is None or STUB.search(description):
        failed.append("task text")
    if not (row.get("readiness", {}).get("worldReady") and all(n["spawned"] for n in row.get("retailNpcs", []))):
        failed.append("spawned here")
    return failed


def render_ready(document: dict[str, Any], crosscheck: dict[str, Any], statuses: dict[int, str]) -> str:
    cross = crosscheck["quests"]
    rows = sorted((q for q in document["quests"] if q["class"] == "A" and not light_bar(q, cross.get(str(q["id"]), {}))),
                  key=batch_order)
    skipped = [q for q in document["quests"] if q["class"] == "A" and light_bar(q, cross.get(str(q["id"]), {}))]
    done = sum(1 for q in rows if q.get("implemented") or statuses.get(q["id"]) == "Done")
    lines = [
        "# D32 ready list: class A quests that pass every check",
        "",
        "Generated by `tools/client-extract/retail_quest_worklist.py` from the same data as",
        "[retail-quest-worklist.md](retail-quest-worklist.md); do not hand-edit. The **Status** column mirrors the",
        "work list, which stays the one place to write `Rejected` or `Done`.",
        "",
        "A quest is on this list when all of these hold:",
        "",
        "1. **Missing in Java:** no handler in Java 4.8 (or here, before D32).",
        "2. **Active in the 4.8 client:** shipped and switched on (not level 99), with a dialog file.",
        "3. **Active in retail 5.8:** still switched on in the 5.8 retail server data (not level 99 or 999),",
        "   so it was not retired or replaced in 4.x.",
        "4. **Real task text:** aioncodex `/48/` shows real steps, not a placeholder or a cutscene trigger.",
        "5. **Everything is here:** every npc and item it names exists and is spawned in this server (class A).",
        "",
        f"**{len(rows)}** quests, **{done}** done. Skipped from class A by these checks: **{len(skipped)}** "
        "(listed at the end, with the check each fails).",
        "",
        "| Status | Quest | Name | Zone | Race | Lvl | Batch | Retail 5.8 form | Review |",
        "|---|---|---|---|---|---:|---|---|---|",
    ]
    for q in rows:
        c = cross.get(str(q["id"]), {})
        simple = "/".join("Simple" + s["type"] for s in (c.get("retail58") or {}).get("simple", [])) or "scripted"
        status = "Done" if q.get("implemented") else statuses.get(q["id"], "")
        lines.append("| {} | Q{} | {} | {} | {} | {} | {} | {} | {} |".format(
            status, q["id"], cell(q["name"] or q["clientName"]), cell(q["zone"]),
            {"ELYOS": "Elyos", "ASMODIANS": "Asmo"}.get(q["race"], "Both"), q["client"]["minLevel"],
            batch_of(q["zone"]), simple, links(q, c)))
    lines += ["", f"## Skipped: class A, but not on this bar ({len(skipped)})", "",
              "| Quest | Name | Zone | Fails |", "|---|---|---|---|"]
    for q in sorted(skipped, key=batch_order):
        lines.append(f"| Q{q['id']} | {cell(q['name'])} | {cell(q['zone'])} | "
                     f"{', '.join(light_bar(q, cross.get(str(q['id']), {})))} |")
    return "\n".join(lines) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--refresh", action="store_true", help="rebuild the cross-check cache from the 5.8 data")
    parser.add_argument("--retail58", type=Path, default=DEFAULT_58)
    parser.add_argument("--check", action="store_true", help="fail when the worklist is stale")
    args = parser.parse_args()
    document = json.loads(INVENTORY.read_text(encoding="utf-8"))
    if args.refresh:
        ids = {q["id"] for q in document["quests"] if q.get("ruleClass") in ("A", "B", "E") or q["class"] in ("A", "B")}
        CROSSCHECK.write_text(json.dumps(refresh(ids, args.retail58), indent=1, ensure_ascii=False) + "\n",
                              encoding="utf-8", newline="\n")
    crosscheck = json.loads(CROSSCHECK.read_text(encoding="utf-8"))
    current = WORKLIST.read_text(encoding="utf-8") if WORKLIST.exists() else ""
    statuses = existing_statuses(current)
    outputs = {WORKLIST: render(document, crosscheck, statuses), READY: render_ready(document, crosscheck, statuses)}
    if args.check:
        stale = [path for path, text in outputs.items()
                 if not path.exists() or path.read_text(encoding="utf-8") != text]
        for path in stale:
            print(f"stale: {path.relative_to(REPO_ROOT).as_posix()}", file=sys.stderr)
        if stale:
            return 1
        print("retail quest worklist is current")
        return 0
    for path, text in outputs.items():
        path.write_text(text, encoding="utf-8", newline="\n")
        print(f"wrote {path.relative_to(REPO_ROOT).as_posix()}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
