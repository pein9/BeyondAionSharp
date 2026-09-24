#!/usr/bin/env python3
"""Write the client's playable-area mask for every world map, for the bot navmesh baker.

Each Aion level ships Levels/<level>/<level>-path.dat, the client's own ground grid (decoded by
tools/client-extract/decode_path_dat.py; format in docs/client-path-dat-format.md). A 16 m sector
holds ground nodes only where a character can stand. The sectors that do are written as

    game-server/data/nav/masks/<mapId>.mask.json   {"mapId", "level", "sectorMetres": 16, "sectors": [[sx, sy], ...]}

The baker builds navmesh tiles only near those sectors, so out-of-bounds terrain (outside the
zone's invisible walls) costs neither bake time nor repository space. Generated; never hand-edit.

    python tools/nav/extract_walk_masks.py --client "C:/Program Files (x86)/Beyond Aion" [--maps 220010000,210010000]
"""
from __future__ import annotations

import argparse
import hashlib
import json
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / "tools" / "client-extract"))
import decode_path_dat as pathdat  # noqa: E402


def level_files(client: Path) -> dict[str, Path]:
    files = {}
    for path in (client / "Levels").glob("*/*-path.dat"):
        files[path.name[: -len("-path.dat")].lower()] = path
    return files


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--client", type=Path, required=True)
    parser.add_argument("--maps", help="comma-separated map ids (default: every world map with a path.dat)")
    args = parser.parse_args()
    wanted = {int(m) for m in args.maps.split(",")} if args.maps else None
    files = level_files(args.client)
    out_dir = REPO / "game-server" / "data" / "nav" / "masks"
    out_dir.mkdir(parents=True, exist_ok=True)
    root = ET.parse(REPO / "game-server" / "data" / "static_data" / "world_maps.xml").getroot()
    written = 0
    for node in root:
        if node.tag != "map":
            continue
        map_id = int(node.get("id"))
        level = (node.get("cName") or "").lower()
        if wanted is not None and map_id not in wanted:
            continue
        path = files.get(level)
        if path is None:
            continue
        pd = pathdat.parse(str(path))
        # Node keys are sector origins in metres; records carry global 0.5 m cell coordinates.
        sectors = sorted({(key[0] // pathdat.SECTOR_METRES, key[1] // pathdat.SECTOR_METRES)
                          for key, nodes in pd.nodes.items() if nodes})
        for record in pd.records.values():
            sectors.append((record.cx // pathdat.CELLS, record.cy // pathdat.CELLS))
        sectors = sorted(set(sectors), key=lambda s: (s[1], s[0]))
        digest = hashlib.sha256(path.read_bytes()).hexdigest()[:16]
        document = {"mapId": map_id, "level": path.name, "source": f"{path.name} sha256 {digest}",
                    "sectorMetres": pathdat.SECTOR_METRES, "sectors": [list(s) for s in sectors]}
        (out_dir / f"{map_id}.mask.json").write_text(json.dumps(document, separators=(",", ":")) + "\n", encoding="utf-8")
        written += 1
        print(f"{map_id} {path.name}: {len(sectors)} sectors ({len(sectors) * 256 / 1e6:.2f} km2)", flush=True)
    print(f"{written} masks -> {out_dir}")


if __name__ == "__main__":
    main()
