#!/usr/bin/env python3
"""Read game-server session recordings (docs/session-recording.md).

    python tools/recording/recording.py summary  RECORDING.jsonl
    python tools/recording/recording.py timeline RECORDING.jsonl [--client-only] [--limit N]
    python tools/recording/recording.py path     RECORDING.jsonl [--out path.json]
    python tools/recording/recording.py client   RECORDING.jsonl [--out client-stream.json]
    python tools/recording/recording.py check    RECORDING.jsonl

summary   counts per direction and packet, duration, account and characters.
timeline  one line per packet: time since start, direction, packet, and the fields that matter.
path      the character's positions (from CM_MOVE and the server's snapshot), targets, kills, deaths and rests.
client    the client packet stream for a replayer: time offset, packet, and the body bytes after the 5-byte header.
check     structural checks: sequence gap-free, every client payload's header consistent, frame lengths correct.
"""
from __future__ import annotations

import argparse
import base64
import collections
import json
import sys
from pathlib import Path


def load(path: Path) -> list[dict]:
    with path.open(encoding="utf-8") as handle:
        return [json.loads(line) for line in handle if line.strip()]


def start_time(entries: list[dict]) -> int:
    return entries[0]["t"] if entries else 0


def summary(entries: list[dict]) -> None:
    if not entries:
        print("empty recording")
        return
    duration = (entries[-1]["t"] - entries[0]["t"]) / 1000
    events = [e for e in entries if e["dir"] == "E"]
    accounts = {e.get("account") for e in events if e.get("event") == "account"}
    players = {(e.get("player") or {}).get("name") for e in events if e.get("event") == "enter-world"} - {None}
    counts = collections.Counter((e["dir"], e.get("packet") or e.get("event")) for e in entries)
    print(f"{len(entries)} entries over {duration:.1f} s; account {', '.join(filter(None, accounts)) or '?'}; "
          f"characters {', '.join(sorted(players)) or 'none'}")
    for direction in ("C", "S", "E"):
        rows = sorted(((n, c) for (d, n), c in counts.items() if d == direction), key=lambda r: -r[1])
        label = {"C": "client packets", "S": "server packets", "E": "events"}[direction]
        print(f"{label}: {sum(c for _, c in rows)}")
        for name, count in rows[:40]:
            print(f"  {count:7d}  {name}")
    outcomes = collections.Counter(e.get("outcome") for e in entries if e["dir"] == "C")
    print("client outcomes:", dict(outcomes))


def brief(fields: dict | None, limit: int = 6) -> str:
    if not fields:
        return ""
    parts = []
    for key, value in fields.items():
        if isinstance(value, (dict, list)):
            continue
        parts.append(f"{key}={value}")
        if len(parts) == limit:
            break
    return " ".join(parts)


def timeline(entries: list[dict], client_only: bool, limit: int | None) -> None:
    t0 = start_time(entries)
    shown = 0
    for e in entries:
        if client_only and e["dir"] != "C":
            continue
        offset = (e["t"] - t0) / 1000
        if e["dir"] == "E":
            print(f"{offset:9.3f}  --  {e.get('event')} {json.dumps({k: v for k, v in e.items() if k not in ('seq', 't', 'wall', 'dir', 'event')})[:160]}")
        else:
            where = ""
            player = e.get("player")
            if player and "x" in player:
                where = f" @({player['x']:.1f},{player['y']:.1f},{player['z']:.1f}) hp {player.get('hp')}/{player.get('maxHp')}"
            outcome = f" [{e.get('outcome')}]" if e["dir"] == "C" and e.get("outcome") != "executed" else ""
            arrow = ">>" if e["dir"] == "C" else "<<"
            print(f"{offset:9.3f}  {arrow} {e.get('packet') or e.get('rawOpcode')}{outcome} {brief(e.get('fields'))}{where}")
        shown += 1
        if limit and shown >= limit:
            break


def path_of(entries: list[dict]) -> dict:
    t0 = start_time(entries)
    points, targets, kills, deaths, rests = [], [], [], [], []
    for e in entries:
        offset = e["t"] - t0
        player = e.get("player")
        if e["dir"] == "C" and player and "x" in player:
            if not points or (abs(points[-1]["x"] - player["x"]) + abs(points[-1]["y"] - player["y"]) > 0.5):
                points.append({"t": offset, "x": player["x"], "y": player["y"], "z": player["z"], "hp": player.get("hp")})
        name = e.get("packet") or ""
        fields = e.get("fields") or {}
        if name == "CM_TARGET_SELECT":
            targets.append({"t": offset, "fields": fields})
        elif name == "SM_DIE":
            deaths.append({"t": offset, "at": points[-1] if points else None})
        elif name == "CM_EMOTION" and "SIT" in json.dumps(fields).upper():
            rests.append({"t": offset, "at": points[-1] if points else None})
        elif name == "SM_STATUPDATE_EXP":
            kills.append({"t": offset, "at": points[-1] if points else None})
    return {"points": points, "targets": targets, "experienceGains": kills, "deaths": deaths, "rests": rests}


def client_stream(entries: list[dict]) -> list[dict]:
    t0 = start_time(entries)
    stream = []
    for e in entries:
        if e["dir"] != "C":
            continue
        payload = base64.b64decode(e["payloadBase64"])
        stream.append({
            "seq": e["seq"],
            "offsetMs": e["t"] - t0,
            "packet": e.get("packet"),
            "opcode": e.get("opcode"),
            "state": e.get("state"),
            "outcome": e.get("outcome"),
            "bodyBase64": base64.b64encode(payload[e.get("bodyOffset", 5):]).decode(),
        })
    return stream


def check(entries: list[dict]) -> int:
    problems = []
    for index, e in enumerate(entries, start=1):
        if e.get("seq") != index:
            problems.append(f"seq {e.get('seq')} at line {index}")
            break
    for e in entries:
        if e["dir"] == "C":
            p = base64.b64decode(e["payloadBase64"])
            if len(p) < 5 or p[2] != 0x65 or ((p[0] | p[1] << 8) ^ 0xFFFF) != (p[3] | p[4] << 8):
                problems.append(f"seq {e['seq']}: client header is not [op][0x65][~op]")
        elif e["dir"] == "S":
            f = base64.b64decode(e["frameBase64"])
            if (f[0] | f[1] << 8) != len(f) or f[4] != 0x44:
                problems.append(f"seq {e['seq']}: server frame length or static byte is wrong")
    for p in problems[:20]:
        print("PROBLEM", p)
    print(f"{len(entries)} entries, {len(problems)} problem(s)")
    return 1 if problems else 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("command", choices=["summary", "timeline", "path", "client", "check"])
    parser.add_argument("recording", type=Path)
    parser.add_argument("--client-only", action="store_true")
    parser.add_argument("--limit", type=int)
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    entries = load(args.recording)
    if args.command == "summary":
        summary(entries)
    elif args.command == "timeline":
        timeline(entries, args.client_only, args.limit)
    elif args.command == "check":
        return check(entries)
    else:
        result = path_of(entries) if args.command == "path" else client_stream(entries)
        text = json.dumps(result, indent=1)
        if args.out:
            args.out.write_text(text + "\n", encoding="utf-8")
            print(f"wrote {args.out}")
        else:
            print(text)
    return 0


if __name__ == "__main__":
    sys.exit(main())
