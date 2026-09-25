#!/usr/bin/env python3
"""Audit spawn heights on a map against the real collision geometry.

A placement pass that takes heights from the terrain heightmap puts anything under an overhang
or in a cave on the ground above it (the 5.8 Ishalgen import did this to nineteen spawns around
the Black Opal cave). This script asks Aion.NavBake for every surface in each spawn's vertical
column and reports:

  floating - no surface within --tolerance metres of the spot (buried or in the air; water
             surfaces are not in the mesh, so lake spawns show here and are fine)
  beneath  - the spot stands on a surface with another walkable floor at least --drop metres
             below it: a cave or overhang candidate. With --base <git rev>, spots that already
             existed at the same X/Y in that revision are trusted and skipped, so only newly
             placed spots are reported.

Requires a built tools/Aion.NavBake (dotnet build tools/Aion.NavBake).

    python tools/nav/audit_spawn_heights.py --map-id 220010000 --base HEAD~1
"""
import argparse
import math
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SPAWN_DIRS = ("Npcs", "Gather", "Statics")


def spawn_file(map_id, folder):
    matches = list((ROOT / "game-server/data/static_data/spawns" / folder).glob(f"{map_id}_*.xml"))
    return matches[0] if matches else None


def spots_from(text):
    out = []
    for spawn in ET.fromstring(text).iter("spawn"):
        for spot in spawn.iter("spot"):
            out.append((int(spawn.get("npc_id")), float(spot.get("x")), float(spot.get("y")), float(spot.get("z"))))
    return out


def current_spots(map_id):
    out = []
    for folder in SPAWN_DIRS:
        path = spawn_file(map_id, folder)
        if path:
            out += [(folder,) + spot for spot in spots_from(path.read_text(encoding="utf-8"))]
    return out


def base_spots(map_id, rev):
    out = []
    for folder in SPAWN_DIRS:
        path = spawn_file(map_id, folder)
        if not path:
            continue
        rel = path.relative_to(ROOT).as_posix()
        show = subprocess.run(["git", "show", f"{rev}:{rel}"], cwd=ROOT, capture_output=True, text=True, encoding="utf-8")
        if show.returncode == 0:
            out += spots_from(show.stdout)
    return out


def npc_names():
    result = {}
    pattern = re.compile(r'npc_id="(\d+)"[^>]*? name="([^"]*)"')
    for line in open(ROOT / "game-server/data/static_data/npcs/npc_templates.xml", encoding="utf-8"):
        match = pattern.search(line)
        if match:
            result[int(match.group(1))] = match.group(2)
    return result


def columns(map_id, points, top, bottom):
    """Every surface (z, steep) top to bottom in each X/Y column, from Aion.NavBake's columns command."""
    surfaces = {}
    for start in range(0, len(points), 400):
        chunk = points[start:start + 400]
        env = dict(os.environ, NAV_COLUMNS=";".join(f"{x},{y}" for x, y in chunk), NAV_ZTOP=str(top), NAV_ZBOTTOM=str(bottom))
        run = subprocess.run(["dotnet", "run", "--project", "tools/Aion.NavBake", "--no-build", "--", "columns", "--maps", str(map_id)],
                             cwd=ROOT, capture_output=True, text=True, env=env)
        if run.returncode != 0:
            sys.exit(f"Aion.NavBake columns failed:\n{run.stdout}\n{run.stderr}")
        for line in run.stdout.splitlines():
            key, sep, rest = line.partition(": ")
            if not sep or "," not in key:
                continue
            x, y = (float(value) for value in key.split(","))
            surfaces[(round(x, 3), round(y, 3))] = [(float(token.replace("(steep)", "")), "(steep)" in token) for token in rest.split()]
    return surfaces


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--map-id", type=int, required=True)
    parser.add_argument("--base", help="git revision whose spots at the same X/Y are trusted (skipped in 'beneath')")
    parser.add_argument("--tolerance", type=float, default=1.5, help="metres between the spot and its surface")
    parser.add_argument("--drop", type=float, default=3.0, help="metres to the floor beneath that makes a candidate")
    parser.add_argument("--ztop", type=float, default=420)
    parser.add_argument("--zbottom", type=float, default=100)
    args = parser.parse_args()

    spots = current_spots(args.map_id)
    if not spots:
        sys.exit(f"No spawn files for map {args.map_id}.")
    trusted = base_spots(args.map_id, args.base) if args.base else []
    names = npc_names()
    surfaces = columns(args.map_id, sorted({(x, y) for _, _, x, y, _ in spots}), args.ztop, args.zbottom)

    floating, beneath = [], []
    for folder, npc, x, y, z in spots:
        column = surfaces.get((round(x, 3), round(y, 3)), [])
        on = [surface for surface in column if abs(surface[0] - z) <= args.tolerance]
        if not on:
            floating.append((folder, npc, x, y, z, [surface[0] for surface in column][:6]))
            continue
        if trusted and any(math.hypot(t[1] - x, t[2] - y) <= 1.0 for t in trusted):
            continue
        top = max(surface[0] for surface in on)
        below = [surface[0] for surface in column if not surface[1] and top - surface[0] >= args.drop]
        if below:
            beneath.append((folder, npc, x, y, z, below[:4]))

    print(f"map {args.map_id}: {len(spots)} spots, {len(surfaces)} columns")
    print(f"floating (no surface within {args.tolerance} m): {len(floating)}")
    for folder, npc, x, y, z, column in floating:
        print(f"  {folder} {npc} {names.get(npc, '?')} at {x},{y},{z}  surfaces {column}")
    scope = f"new since {args.base}" if args.base else "all spots"
    print(f"beneath ({scope}; a walkable floor >= {args.drop} m below): {len(beneath)}")
    for folder, npc, x, y, z, below in sorted(beneath, key=lambda entry: -(entry[4] - entry[5][0])):
        print(f"  {folder} {npc} {names.get(npc, '?')} at {x},{y},{z}  floors below {below}  drop {z - below[0]:.1f} m")
    return 0


if __name__ == "__main__":
    sys.exit(main())
