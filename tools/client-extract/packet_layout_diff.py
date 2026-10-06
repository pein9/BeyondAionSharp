#!/usr/bin/env python3
"""Compare what the 4.8 client reads from each server packet with what this server writes.

The client side is `out/client_packet_layouts.tsv`, made by `client_packet_layouts.py` from the
client's own `game.dll`: for every opcode, the byte offsets and sizes the client's handler loads
from the packet payload. The server side is read here from the C# packet classes: the run of
fixed-width `Write*` calls at the start of `WriteImpl`.

Both sides are often only partly known, and the report says so. A packet whose writer has an
`if`, a loop, a string or a helper call is known up to that point. A handler that reads through
a cursor object, or copies a variable length, is known only for the bytes seen.

Verdicts:
  READS_PAST_END  the client loads bytes beyond the end of a fully known server packet
                  (byte ranges are `first..one past last`, with the loading instruction's address)
  MISALIGNED      a client load starts or ends inside a server field
  SERVER_LONGER   both sides fully known, and the server writes bytes the client never loads
  NO_HANDLER      the server registers the opcode and the client's dispatch has no case for it
  IGNORED         the client's case does nothing
  MATCH           both sides fully known and they agree
  AGREES_SO_FAR   one side is partly known, and nothing known disagrees
  UNKNOWN         no client loads were found, or the writer could not be read

A verdict is a lead. A load may sit on a branch the server's packet never takes, so read the
handler before changing a writer; a real fix departs from Java and needs a logged decision.

Usage:
    python packet_layout_diff.py                 # the findings
    python packet_layout_diff.py --all           # every opcode
    python packet_layout_diff.py --tsv out.tsv   # every opcode, as TSV
"""
from __future__ import annotations

import argparse
import json
import os
import pathlib
import re
import sys

REPO = pathlib.Path(__file__).resolve().parents[2]
OPCODES = REPO / "src/Aion.GameServer/Network/Aion/ServerPacketsOpcodes.cs"
PACKETS = REPO / "src/Aion.GameServer/Network/Aion/ServerPackets"
GOLDEN = REPO / "parity-artifacts/golden/packets"
LAYOUTS = pathlib.Path(__file__).resolve().parent / "out/client_packet_layouts.tsv"

WIDTHS = {"WriteC": 1, "WriteH": 2, "WriteD": 4, "WriteQ": 8, "WriteF": 4, "WriteDF": 8, "WriteDyeInfo": 4}
CONTROL = ("if", "else", "for", "foreach", "while", "do", "switch", "return", "try", "using", "lock", "throw", "break")
ORDER = ["READS_PAST_END", "MISALIGNED", "SERVER_LONGER", "NO_HANDLER", "IGNORED", "UNKNOWN", "AGREES_SO_FAR", "MATCH"]


def strip_code(text: str) -> str:
    """Blank out comments, strings and chars, keeping every other character where it was."""
    out, i, n = [], 0, len(text)
    while i < n:
        two = text[i:i + 2]
        if two == "//":
            j = text.find("\n", i)
            j = n if j < 0 else j
            out.append(" " * (j - i))
            i = j
        elif two == "/*":
            j = text.find("*/", i + 2)
            j = n if j < 0 else j + 2
            out.append(re.sub(r"[^\n]", " ", text[i:j]))
            i = j
        elif text[i] in "\"'":
            quote, j = text[i], i + 1
            while j < n and text[j] != quote:
                j += 2 if text[j] == "\\" else 1
            out.append(quote + "_" * (j - i - 1) + quote)
            i = j + 1
        else:
            out.append(text[i])
            i += 1
    return "".join(out)


def statements(body: str) -> list[str]:
    """Top-level statements of a method body. A block statement is returned whole."""
    found, depth, start = [], 0, 0
    for i, ch in enumerate(body):
        if ch in "({[":
            depth += 1
        elif ch in ")}]":
            depth -= 1
            if ch == "}" and depth == 0:
                found.append(body[start:i + 1])
                start = i + 1
        elif ch == ";" and depth == 0:
            found.append(body[start:i])
            start = i + 1
    found.append(body[start:])
    return [s.strip() for s in found if s.strip()]


def server_layout(source: str) -> dict:
    """The fixed-width fields at the start of WriteImpl, and why the reading stopped, if it did."""
    code = strip_code(source)
    m = re.search(r"\bvoid\s+WriteImpl\s*\([^)]*\)\s*", code)
    if not m:
        return {"fields": [], "stop": "no WriteImpl"}
    rest = code[m.end():]
    if rest.startswith("=>"):
        body = rest[2:rest.index(";") + 1]
    elif rest.startswith("{"):
        depth = 0
        for i, ch in enumerate(rest):
            depth += ch == "{"
            depth -= ch == "}"
            if depth == 0:
                break
        body = rest[1:i]
    else:
        return {"fields": [], "stop": "WriteImpl not understood"}

    fields: list[int] = []
    for st in statements(body):
        call = re.fullmatch(r"(?:this\.|base\.)?(\w+)\s*\((.*)\)", st, re.S)
        word = re.match(r"[A-Za-z_]+", st)
        if word and word.group(0) in CONTROL:
            return {"fields": fields, "stop": word.group(0)}
        if not call:
            if "Write" in st or re.search(r"\w\s*\(", st.split("=", 1)[0]):
                return {"fields": fields, "stop": "statement: " + " ".join(st.split())[:40]}
            continue  # a declaration or an assignment writes nothing
        name, args = call.group(1), call.group(2)
        if name in WIDTHS:
            fields.append(WIDTHS[name])
        elif name == "WriteS" and (fixed := re.search(r",\s*(\d+)\s*$", args)):
            fields.append(int(fixed.group(1)) * 2 + 2)
        elif name == "WriteB" and (fixed := re.fullmatch(r"\s*new\s+byte\s*\[\s*(\d+)\s*\]\s*", args)):
            fields.append(int(fixed.group(1)))
        else:
            return {"fields": fields, "stop": name}
    return {"fields": fields, "stop": None}


def registered_opcodes() -> dict[int, str]:
    found = {}
    for line in OPCODES.read_text(encoding="utf-8").splitlines():
        m = re.match(r"\s*AddPacketOpcode\((0x[0-9A-Fa-f]+|\d+),\s*typeof\((\w+)\)", line)
        if m:
            found[int(m.group(1), 0)] = m.group(2)
    return found


def retail_names(java_root: pathlib.Path) -> dict[int, str]:
    """NCSoft's packet names, which the Java opcode table keeps in a comment on every line."""
    table = java_root / "game-server/src/com/aionemu/gameserver/network/aion/ServerPacketsOpcodes.java"
    if not table.is_file():
        return {}
    names = {}
    for line in table.read_text(encoding="utf-8").splitlines():
        m = re.search(r"addPacketOpcode\((\d+),[^)]*\);?\s*//\s*\[(\w+)\]", line)
        if m:
            names[int(m.group(1))] = m.group(2)
    return names


def golden_lengths(packet: str) -> list[int]:
    path = GOLDEN / f"{packet}.json"
    if not path.is_file():
        return []
    cases = json.loads(path.read_text(encoding="utf-8")).get("cases", [])
    return sorted({len(c["payloadHex"]) // 2 for c in cases if "payloadHex" in c})


def client_layouts(path: pathlib.Path) -> dict[int, dict]:
    rows = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        if line.startswith("#") or line.startswith("opcode\t"):
            continue
        op, kind, addr, handlers, reads, copies, var_reads, unresolved, _followed = (line.split("\t") + [""] * 9)[:9]
        spans = []  # (first byte, byte after the last, address of the instruction that loads it)
        for read in filter(None, reads.split(",")):
            span, _, at = read.partition("@")
            start, size = map(int, span.split(":"))
            spans.append((start, start + size, at))
        for copy in filter(None, copies.split(",")):
            start, size = map(int, copy.split(":"))
            spans.append((start, start + size, "memcpy"))
        rows[int(op)] = {
            "kind": kind, "addr": addr, "handlers": handlers, "spans": sorted(set(spans)),
            "partial": int(var_reads or 0) > 0 or bool(unresolved),
            "why": ("non-constant offsets; " if int(var_reads or 0) else "") + unresolved,
        }
    return rows


def compare(client: dict, server: dict | None) -> tuple[str, str]:
    if client["kind"] == "default":
        return "NO_HANDLER", "the dispatch has no case for this opcode"
    if client["kind"] == "noop":
        return "IGNORED", "the case goes straight to the dispatch's exit"
    spans = client["spans"]
    if not spans:
        return "UNKNOWN", "no client loads found" + (": " + client["why"] if client["why"] else "")
    if server is None or (not server["fields"] and server["stop"]):
        return "UNKNOWN", "writer not read" + (": " + server["stop"] if server else "")

    bounds, blobs, total = {0}, [], 0
    for width in server["fields"]:
        if width > 8:
            blobs.append((total, total + width))  # a fixed string or byte array, which is read in pieces
        total += width
        bounds.add(total)
    complete = server["stop"] is None
    extent = max(end for _, end, _ in spans)

    if complete and extent > total:
        beyond = [f"{a}..{b} at {at}" for a, b, at in spans if b > total]
        return "READS_PAST_END", f"server writes {total} bytes, client loads {', '.join(beyond)}"
    crooked = [f"{a}..{b} at {at}" for a, b, at in spans
               if b <= total and (a not in bounds or b not in bounds) and not any(s <= a and b <= e for s, e in blobs)]
    if crooked:
        return "MISALIGNED", "client loads " + ", ".join(crooked[:6]) + " against server fields " + field_text(server["fields"])
    if complete and not client["partial"]:
        if extent < total:
            return "SERVER_LONGER", f"server writes {total} bytes, client loads up to {extent}"
        return "MATCH", f"{total} bytes"
    known = []
    if not complete:
        known.append(f"writer known for {total} bytes, then {server['stop']}")
    if client["partial"]:
        known.append("handler partly followed: " + client["why"][:80])
    return "AGREES_SO_FAR", "; ".join(known)


def field_text(fields: list[int]) -> str:
    letters = {1: "c", 2: "h", 4: "d", 8: "q"}
    return "".join(letters.get(w, f"[{w}]") for w in fields)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--layouts", type=pathlib.Path, default=LAYOUTS, help="client layouts TSV")
    ap.add_argument("--java-root", type=pathlib.Path,
                    default=pathlib.Path(os.environ.get("BEYOND_AION_JAVA_ROOT", REPO.parent / "aion-server")),
                    help="Java checkout, for the retail packet names (optional)")
    ap.add_argument("--all", action="store_true", help="print every opcode, not only the findings")
    ap.add_argument("--tsv", type=pathlib.Path, help="also write every opcode to this TSV")
    args = ap.parse_args()

    if not args.layouts.is_file():
        print(f"missing {args.layouts}; make it with client_packet_layouts.py", file=sys.stderr)
        return 2
    client = client_layouts(args.layouts)
    ours = registered_opcodes()
    retail = retail_names(args.java_root)

    rows = []
    for op in sorted(client):
        packet = ours.get(op)
        if packet is None:
            if client[op]["kind"] != "default":
                rows.append((op, "", retail.get(op, ""), "NOT_SENT", "the client handles it and this server never sends it", "", ""))
            continue
        path = PACKETS / f"{packet}.cs"
        server = server_layout(path.read_text(encoding="utf-8")) if path.is_file() else None
        verdict, detail = compare(client[op], server)
        fields = field_text(server["fields"]) if server else ""
        golden = ",".join(map(str, golden_lengths(packet)))
        rows.append((op, packet, retail.get(op, ""), verdict, detail, fields, golden))

    if args.tsv:
        with args.tsv.open("w", encoding="utf-8", newline="\n") as f:
            f.write("opcode\tpacket\tretail_name\tverdict\tdetail\tserver_fields\tjava_golden_lengths\n")
            for row in rows:
                f.write("\t".join(str(c) for c in row) + "\n")

    counts: dict[str, int] = {}
    for row in rows:
        counts[row[3]] = counts.get(row[3], 0) + 1
    print("Server packets against the 4.8 client's handlers")
    print("  " + "  ".join(f"{v} {counts[v]}" for v in ORDER + ["NOT_SENT"] if v in counts))
    quiet = {"MATCH", "AGREES_SO_FAR", "UNKNOWN", "NOT_SENT"}
    for verdict in ORDER + ["NOT_SENT"]:
        if verdict in quiet and not args.all:
            continue
        group = [r for r in rows if r[3] == verdict]
        if not group:
            continue
        print(f"\n{verdict} ({len(group)})")
        for op, packet, name, _, detail, fields, golden in group:
            label = packet or name or "?"
            extra = f"  [{name}]" if packet and name else ""
            tail = f"  (Java golden: {golden} bytes)" if golden and verdict in ("READS_PAST_END", "SERVER_LONGER", "MISALIGNED") else ""
            print(f"  {op:3d}  {label}{extra}: {detail}{tail}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
