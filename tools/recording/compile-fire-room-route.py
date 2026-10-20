"""Extract public movement waypoints from the operator's closed 4.8 recording.

Usage: python tools/recording/compile-fire-room-route.py RECORDING [--check]
The private recording stays in run/. No account IDs, packets or credentials are emitted.
"""
import argparse
import hashlib
import json
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("recording", type=Path)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    raw = args.recording.read_bytes()
    entries = [json.loads(line) for line in raw.splitlines()]
    if not entries or entries[-1].get("event") != "session-close":
        raise SystemExit("Refusing an open or incomplete recording")

    def extract(first, last, map_id):
        rows = [e for e in entries if first <= e["seq"] <= last and
                e.get("dir") == "C" and e.get("packet") == "CM_MOVE"]
        if not rows or rows[0]["seq"] != first or rows[-1]["seq"] != last:
            raise SystemExit(f"Missing movement endpoints {first}..{last}")
        if any(e["player"]["mapId"] != map_id or e.get("outcome") != "executed" for e in rows):
            raise SystemExit("Route has an unexpected map or an unexecuted packet")
        return {"mapId": map_id, "points": [dict(
            sequence=e["seq"], milliseconds=e["t"] - rows[0]["t"],
            x=e["fields"]["x"], y=e["fields"]["y"], z=e["fields"]["z"],
            heading=e["fields"]["heading"], mask=e["fields"]["type"],
            glideFlag=e["fields"].get("glideFlag", 0)) for e in rows]}

    # Sequence boundaries are the last grounded position before takeoff/jump and
    # the stable landing. Wind Room exploration and combat are deliberately excluded.
    entrance = [e for e in entries if e.get("packet") == "CM_MOVE" and
                "2026-10-20T14:56:46.417" <= e["wall"] <= "2026-10-20T14:57:04.781Z"]
    document = {"schema": 1, "sourceSha256": hashlib.sha256(raw).hexdigest(),
                "recordedUtc": "2026-10-20", "recordedAccessLevel": 9,
                "entrance": extract(entrance[0]["seq"], entrance[-1]["seq"], 220020000),
                "descent": extract(24762, 25426, 320050000)}
    output = Path(__file__).resolve().parents[2] / "parity-artifacts/e2e/natural-fire-room-route.json"
    rendered = json.dumps(document, indent=2) + "\n"
    if args.check:
        if output.read_text(encoding="utf-8") != rendered:
            raise SystemExit("Fire Room route drift; regenerate from the original recording")
        print("Fire Room route matches the recording")
    else:
        output.write_text(rendered, encoding="utf-8")
        print(output)


if __name__ == "__main__":
    main()
