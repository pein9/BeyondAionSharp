#!/usr/bin/env python3
"""RQ-03: the aioncodex `/48/` facts for every quest in the retail quest inventory.

WHY THIS EXISTS
---------------
The client's `quest.xml` has no start or end npc, and the dialogs that do are behind an encryption
layer this repository cannot read (see `retail_quest_inventory.py`). aioncodex's `/48/` database is
built from the 4.8 client and shows the quest giver, the task statement with its npc and item links,
the prerequisites and the rewards. `docs/retail-quest-completion.md` names it as the retail source.

`/48/` only, never `/enc/`: that is today's live game, where quests were rewritten or removed.

POLITENESS
----------
One page at a time, a pause between requests, and **never re-fetched once stored**. Each page's raw
HTML is kept in `run/aioncodex-48/` (ignored by git), and the facts parsed from it go to the checked-in
`parity-artifacts/e2e/retail-quest-evidence.json`, written after every page, so an interrupted run
resumes where it stopped. `--reparse` rebuilds the facts from the local HTML without the network.

WHAT IS KEPT
------------
Structured facts and the one-line task statement (the quest's "Description", which is its steps).
The summary and the full dialog text are not kept: they are long localized text and the steps do not
need them. The page's own URL is kept with every row.

Usage:
    python retail_quest_evidence.py [--limit N] [--delay SECONDS] [--quest ID ...] [--reparse]
"""
from __future__ import annotations

import argparse
import html
import json
import re
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path
from typing import Any

REPO_ROOT = Path(__file__).resolve().parents[2]
INVENTORY = REPO_ROOT / "parity-artifacts/e2e/retail-quest-inventory.json"
EVIDENCE = REPO_ROOT / "parity-artifacts/e2e/retail-quest-evidence.json"
CACHE = REPO_ROOT / "run/aioncodex-48"
URL = "https://aioncodex.com/48/quest/{}/?sl=1"
USER_AGENT = "BeyondAionSharp-research/1.0 (one-time 4.8 quest evidence cache; one page at a time)"
SCHEMA_VERSION = 1

LINK = re.compile(r'<a href="/48/(npc|item|quest)/(\d+)/"[^>]*>(.*?)</a>', re.S)
DIC = re.compile(r"\[%dic:([^\]]+)\]")


def plain(fragment: str) -> str:
    """Text of an HTML fragment, links kept as `Name` and whitespace collapsed."""
    text = re.sub(r"<br\s*/?>", " ", fragment)
    text = re.sub(r"<[^>]+>", "", text)
    return re.sub(r"\s+", " ", html.unescape(text)).strip()


def links(fragment: str) -> list[dict[str, Any]]:
    out, seen = [], set()
    for kind, ident, name in LINK.findall(fragment):
        key = (kind, int(ident))
        if key not in seen:
            seen.add(key)
            out.append({"kind": kind, "id": int(ident), "name": plain(name)})
    return out


def section(body: str, label: str) -> str | None:
    """The fragment after `<b>label</b>:<br>` up to the next rule."""
    match = re.search(rf"<b>{re.escape(label)}</b>:<br>(.*?)<hr", body, re.S)
    return match.group(1) if match else None


def info_rows(body: str) -> dict[str, str]:
    """The "Additional info" table: label -> raw cell."""
    start = body.find("<b>Additional info</b>")
    if start < 0:
        return {}
    table = body[start:body.find("</table>", start)]
    return {plain(label): cell for label, cell in
            re.findall(r"<tr><td>([^<]+)</td><td[^>]*>(.*?)</td></tr>", table, re.S)}


def requirement_groups(body: str) -> dict[str, list[int]]:
    start = body.find("<b>Quest requirements</b>")
    if start < 0:
        return {}
    block = body[start:body.find("</td>", start)]
    groups: dict[str, list[int]] = {}
    for label, rest in re.findall(r"([A-Z][A-Za-z ]+):<br>(.*?)(?=[A-Z][A-Za-z ]+:<br>|$)", block, re.S):
        ids = [int(i) for i in re.findall(r'/48/quest/(\d+)/', rest)]
        if ids:
            groups[label.strip()] = ids
    return groups


def parse(quest_id: int, status: int, page: str | None) -> dict[str, Any]:
    row: dict[str, Any] = {"url": URL.format(quest_id), "status": status}
    if status != 200 or not page:
        return row
    body = re.sub(r"<script.*?</script>", "", page, flags=re.S)
    if f"ID: {quest_id}<" not in body:
        row["found"] = False
        return row
    row["found"] = True
    title = re.search(r'id="item_name"><b>(.*?)</b>', body, re.S)
    row["title"] = plain(title.group(1)) if title else None
    header = re.search(r'class="titles_cell">(.*?)</td>', body, re.S)
    if header:
        parts = [plain(p) for p in re.split(r"<br\s*/?>", header.group(1)) if plain(p)]
        row["kind"] = parts[0] if parts else None
        for part in parts[1:]:
            key, sep, value = part.partition(":")
            if sep:
                row[key.strip().lower()] = value.strip()
            else:
                row["raceText"] = part
    description = section(body, "Description")
    if description is not None:
        row["description"] = DIC.sub(lambda m: f"[{m.group(1)}]", plain(description))
        row["descriptionLinks"] = links(description)
    summary = section(body, "Summary")
    if summary is not None:
        row["summaryLinks"] = links(summary)
    row["hasDialogText"] = "Full quest's text" in body
    info = info_rows(body)
    row["info"] = {label: (links(cell) or plain(cell)) for label, cell in info.items()
                   if label not in ("Class", "Gender", "Can share", "Can cancel")}
    giver = info.get("Quest giver")
    row["questGivers"] = [link["id"] for link in links(giver) if link["kind"] == "npc"] if giver else []
    row["requirements"] = requirement_groups(body)
    reward = body[body.find("Basic Reward"):body.find("</table>", body.find("Basic Reward"))] if "Basic Reward" in body else ""
    row["rewardItems"] = sorted({int(i) for i in re.findall(r'/48/item/(\d+)/', reward)})
    return row


def fetch(quest_id: int) -> tuple[int, str | None]:
    request = urllib.request.Request(URL.format(quest_id), headers={"User-Agent": USER_AGENT})
    try:
        with urllib.request.urlopen(request, timeout=60) as response:
            return response.status, response.read().decode("utf-8", errors="replace")
    except urllib.error.HTTPError as error:
        return error.code, None


def load_evidence() -> dict[str, Any]:
    if EVIDENCE.exists():
        return json.loads(EVIDENCE.read_text(encoding="utf-8"))
    return {"schemaVersion": SCHEMA_VERSION, "generatedBy": "tools/client-extract/retail_quest_evidence.py",
            "source": "https://aioncodex.com/48/", "quests": {}}


def save(document: dict[str, Any]) -> None:
    document["quests"] = dict(sorted(document["quests"].items(), key=lambda kv: int(kv[0])))
    EVIDENCE.write_text(json.dumps(document, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--quest", type=int, action="append", default=[], help="only these ids")
    parser.add_argument("--limit", type=int, default=0, help="stop after this many network fetches")
    parser.add_argument("--delay", type=float, default=4.0, help="seconds between network fetches")
    parser.add_argument("--reparse", action="store_true", help="rebuild the facts from the local HTML only")
    args = parser.parse_args()

    inventory = json.loads(INVENTORY.read_text(encoding="utf-8"))
    ids = args.quest or [q["id"] for q in inventory["quests"]]
    document = load_evidence()
    CACHE.mkdir(parents=True, exist_ok=True)
    fetched = 0
    for quest_id in ids:
        cached = CACHE / f"{quest_id}.html"
        key = str(quest_id)
        if args.reparse:
            if cached.exists():
                previous = document["quests"].get(key, {})
                row = parse(quest_id, 200, cached.read_text(encoding="utf-8"))
                if "fetched" in previous:
                    row["fetched"] = previous["fetched"]
                document["quests"][key] = row
            continue
        if key in document["quests"] and document["quests"][key].get("status") == 200:
            continue  # stored: never re-fetched
        if cached.exists():
            status, page = 200, cached.read_text(encoding="utf-8")
        else:
            if args.limit and fetched >= args.limit:
                break
            if fetched:
                time.sleep(args.delay)
            status, page = fetch(quest_id)
            fetched += 1
            if status == 200 and page:
                cached.write_text(page, encoding="utf-8", newline="\n")
        row = parse(quest_id, status, page)
        row["fetched"] = time.strftime("%Y-%m-%d")
        document["quests"][key] = row
        save(document)
        print(f"{quest_id}: {status} {row.get('title', '')}", flush=True)
    save(document)
    stored = document["quests"]
    print(f"{len(stored)} stored, {sum(1 for r in stored.values() if r.get('found'))} found; {fetched} fetched now")
    return 0


if __name__ == "__main__":
    sys.exit(main())
