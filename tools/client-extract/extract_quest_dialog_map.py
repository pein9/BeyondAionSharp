#!/usr/bin/env python3
"""Build a quest page -> button action map from decoded Aion client HTML.

Quest HTML has a second encryption layer after PAK extraction. Encoded files
start with 0x81 and must be decoded with their original quest_q<ID>.html file
name before this tool can parse them. The output intentionally excludes dialog
text; only the client-authored page and action topology is retained.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


QUEST_FILE = re.compile(r"^quest_q(\d+)\.(?:html|xml)$", re.IGNORECASE)
ACTION = re.compile(r"HACTION_([A-Za-z0-9_]+)", re.IGNORECASE)


def custom_quest_ids(classifier: Path) -> set[int]:
    document = json.loads(classifier.read_text(encoding="utf-8"))
    return {
        quest["id"]
        for quest in document["quests"]
        if quest["availability"] == "obtainable" and quest["handlerKind"] == "custom"
    }


def decode_xml(path: Path) -> ET.Element:
    data = path.read_bytes()
    if data.startswith(b"\x81"):
        raise ValueError(
            f"{path} is still 0x81-encoded; decode it with the original quest filename first"
        )
    return ET.fromstring(data)


def action_names(href: str | None) -> list[str]:
    if not href:
        return []
    return [match.group(1).upper() for match in ACTION.finditer(href)]


def page_map(root: ET.Element) -> list[dict[str, object]]:
    pages: list[dict[str, object]] = []
    for page in root.findall(".//HtmlPage"):
        buttons: list[dict[str, object]] = []
        for select in page.findall("./Selects/Act"):
            actions = action_names(select.get("href"))
            if actions:
                buttons.append({"actions": actions})
        if buttons:
            pages.append({"page": page.get("name"), "buttons": buttons})
    return pages


def sha256(path: Path | None) -> str | None:
    if path is None:
        return None
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def compile_map(
    decoded_root: Path,
    classifier: Path,
    archive: Path | None,
    client_version: str,
) -> dict[str, object]:
    expected = custom_quest_ids(classifier)
    files: dict[int, Path] = {}
    for path in sorted(decoded_root.rglob("quest_q*.*")):
        match = QUEST_FILE.match(path.name)
        if match and int(match.group(1)) in expected:
            files.setdefault(int(match.group(1)), path)

    quests: list[dict[str, object]] = []
    action_refs = 0
    page_count = 0
    for quest_id in sorted(expected & files.keys()):
        pages = page_map(decode_xml(files[quest_id]))
        page_count += len(pages)
        action_refs += sum(len(button["actions"]) for page in pages for button in page["buttons"])
        quests.append({"questId": quest_id, "pages": pages})

    return {
        "schemaVersion": 1,
        "generatedBy": "tools/client-extract/extract_quest_dialog_map.py",
        "client": {
            "version": client_version,
            "archive": "l10n/ENG/Data/Data.pak" if archive else None,
            "archiveSha256": sha256(archive),
            "format": "decoded 0x81 quest HTML",
        },
        "counts": {
            "expectedCustomQuests": len(expected),
            "clientQuestFiles": len(quests),
            "missingClientQuestFiles": len(expected - files.keys()),
            "pagesWithActions": page_count,
            "actionReferences": action_refs,
        },
        "missingQuestIds": sorted(expected - files.keys()),
        "quests": quests,
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("decoded_root", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument(
        "--classifier",
        type=Path,
        default=Path("parity-artifacts/e2e/obtainable-quests.json"),
    )
    parser.add_argument("--archive", type=Path)
    parser.add_argument("--client-version", default="4.8")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        document = compile_map(
            args.decoded_root,
            args.classifier,
            args.archive,
            args.client_version,
        )
    except (OSError, ValueError, ET.ParseError, KeyError) as error:
        print(error, file=sys.stderr)
        return 1
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(document, indent=2) + "\n", encoding="utf-8")
    counts = document["counts"]
    print(
        f"Mapped {counts['clientQuestFiles']}/{counts['expectedCustomQuests']} custom quests, "
        f"{counts['pagesWithActions']} pages, {counts['actionReferences']} action references."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
