#!/usr/bin/env python3
"""Extract road centerlines from a client world-map image into game coordinates.

The client's map art (Textures/ui/newmap/<level>/<level>.pak, cached by the aion-portal
spawn editor as assets/maps/<mapId>-<name>-map.webp) draws public roads as pale cream lines.
This script masks that colour, removes noise, skeletonizes the mask and traces the skeleton
into polylines, then converts pixels to game X/Y with the portal's calibration:

    game X = offsetY + row    * mapHeight / imageSize
    game Y = offsetX + column * mapWidth  / imageSize

Output: game-server/data/nav/roads/<mapId>.roads.json (generated; never hand-edit).
Roads are a cost hint for the bot navmesh. They never make ground walkable.

Requires numpy, scikit-image and Pillow (pip install numpy scikit-image pillow).

    python tools/nav/extract_roads.py --map-id 220010000 \
        --image ../aion-portal/assets/maps/220010000-ishalgen-map.webp \
        --manifest ../aion-portal/assets/maps/manifest.json [--preview out.png]
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from skimage import morphology

REPO = Path(__file__).resolve().parents[2]


def road_mask(rgb: np.ndarray) -> np.ndarray:
    r, g, b = (rgb[..., i].astype(np.int32) for i in range(3))
    mask = (r > 140) & (g > 130) & (r - b > 38) & (np.abs(r - g) < 30) & (g - b > 28)
    mask = morphology.closing(mask, morphology.disk(2)).astype(bool)
    mask = morphology.remove_small_objects(mask, max_size=249)
    mask = morphology.remove_small_holes(mask, max_size=63)
    return mask


NEIGHBOURS = [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)]


def trace(skeleton: np.ndarray) -> list[list[tuple[int, int]]]:
    """Split a one-pixel skeleton into polylines between junctions/endpoints."""
    pixels = set(zip(*np.nonzero(skeleton)))

    def neighbours(p):
        return [(p[0] + dy, p[1] + dx) for dy, dx in NEIGHBOURS if (p[0] + dy, p[1] + dx) in pixels]

    nodes = {p for p in pixels if len(neighbours(p)) != 2}
    visited_edges: set[frozenset] = set()
    lines = []
    for node in sorted(nodes):
        for first in neighbours(node):
            key = frozenset((node, first))
            if key in visited_edges:
                continue
            line = [node, first]
            visited_edges.add(key)
            prev, cur = node, first
            while cur not in nodes:
                nxt = [n for n in neighbours(cur) if n != prev and frozenset((cur, n)) not in visited_edges]
                if not nxt:
                    break
                visited_edges.add(frozenset((cur, nxt[0])))
                prev, cur = cur, nxt[0]
                line.append(cur)
            lines.append(line)
    # Pure loops without any node.
    remaining = pixels - {p for line in lines for p in line}
    while remaining:
        start = min(remaining)
        line, cur, prev = [start], start, None
        while True:
            nxt = [n for n in neighbours(cur) if n != prev and n in remaining and n not in line]
            if not nxt:
                break
            prev, cur = cur, nxt[0]
            line.append(cur)
        remaining -= set(line)
        if len(line) > 10:
            lines.append(line)
    return lines


def rdp(points: list[tuple[float, float]], epsilon: float) -> list[tuple[float, float]]:
    if len(points) < 3:
        return points
    a, b = np.array(points[0]), np.array(points[-1])
    ab = b - a
    norm = np.hypot(*ab)
    best, index = 0.0, 0
    for i in range(1, len(points) - 1):
        p = np.array(points[i])
        d = abs(ab[0] * (a[1] - p[1]) - ab[1] * (a[0] - p[0])) / norm if norm else np.hypot(*(p - a))
        if d > best:
            best, index = d, i
    if best <= epsilon:
        return [points[0], points[-1]]
    return rdp(points[: index + 1], epsilon)[:-1] + rdp(points[index:], epsilon)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--map-id", type=int, required=True)
    parser.add_argument("--image", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--min-length", type=float, default=25.0, help="drop dangling spurs shorter than this (metres)")
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()

    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    entry = next(m for m in manifest["maps"] if m["mapId"] == args.map_id)
    cal = entry["calibration"]
    image = Image.open(args.image).convert("RGB")
    size = image.size[0]
    if image.size[0] != image.size[1]:
        raise SystemExit("map image must be square")
    rgb = np.asarray(image)
    mask = road_mask(rgb)
    skeleton = morphology.skeletonize(mask)
    lines = trace(skeleton)
    sx, sy = cal["mapHeight"] / size, cal["mapWidth"] / size

    def world(p):
        row, col = p
        return (round(cal["offsetY"] + row * sx, 2), round(cal["offsetX"] + col * sy, 2))

    endpoints: dict[tuple[int, int], int] = {}
    for line in lines:
        for end in (line[0], line[-1]):
            endpoints[end] = endpoints.get(end, 0) + 1
    roads = []
    for line in lines:
        length = sum(np.hypot(line[i][0] - line[i - 1][0], line[i][1] - line[i - 1][1]) for i in range(1, len(line))) * sx
        dangling = endpoints[line[0]] == 1 or endpoints[line[-1]] == 1
        if length < (args.min_length if dangling else 4):
            continue
        simplified = rdp([(float(r), float(c)) for r, c in line], 1.5)
        points = [list(world(p)) for p in simplified]
        roads.append({"id": f"r{len(roads)}", "points": points})

    def length_of(points):
        return sum(float(np.hypot(points[i][0] - points[i - 1][0], points[i][1] - points[i - 1][1])) for i in range(1, len(points)))

    def components(items):
        parent = list(range(len(items)))

        def find(i):
            while parent[i] != i:
                parent[i] = parent[parent[i]]
                i = parent[i]
            return i
        owner = {}
        for i, r in enumerate(items):
            for p in r["points"]:
                key = (p[0], p[1])
                if key in owner:
                    parent[find(i)] = find(owner[key])
                else:
                    owner[key] = i
        return [find(i) for i in range(len(items))]

    # Join a dangling end to the nearest point of a different road network within 20 m
    # (gaps where trees or labels cover the painted road).
    comp = components(roads)
    ends = {}
    for r in roads:
        for q in (r["points"][0], r["points"][-1]):
            ends[(q[0], q[1])] = ends.get((q[0], q[1]), 0) + 1
    joins = []
    for i, road in enumerate(roads):
        for end in (road["points"][0], road["points"][-1]):
            if ends[(end[0], end[1])] != 1:
                continue
            best, best_d = None, 15.0
            for j, other in enumerate(roads):
                if comp[j] == comp[i]:
                    continue
                for q in other["points"]:
                    d = float(np.hypot(q[0] - end[0], q[1] - end[1]))
                    if d < best_d:
                        best, best_d = q, d
            if best is not None:
                joins.append([list(end), list(best)])
    for join in joins:
        roads.append({"id": f"j{len(roads)}", "points": join})
    # Drop isolated scraps (painted rocks, mushroom caps) shorter than 80 m in total (after joining).
    comp = components(roads)
    totals = {}
    for i, r in enumerate(roads):
        totals[comp[i]] = totals.get(comp[i], 0.0) + length_of(r["points"])
    roads = [r for i, r in enumerate(roads) if totals[comp[i]] >= 80]

    for index, road in enumerate(roads):
        road["id"] = f"r{index}"

    digest = hashlib.sha256(args.image.read_bytes()).hexdigest()[:16]
    out = REPO / "game-server" / "data" / "nav" / "roads" / f"{args.map_id}.roads.json"
    out.parent.mkdir(parents=True, exist_ok=True)
    document = {
        "mapId": args.map_id,
        "source": f"tools/nav/extract_roads.py from {entry['layers'][0].get('clientPak', args.image.name)} "
                  f"(image sha256 {digest}, calibration {cal})",
        "roads": roads,
    }
    out.write_text(json.dumps(document, separators=(",", ":")) + "\n", encoding="utf-8")
    total = sum(np.hypot(r["points"][i][0] - r["points"][i - 1][0], r["points"][i][1] - r["points"][i - 1][1])
                for r in roads for i in range(1, len(r["points"])))
    print(f"{args.map_id}: {len(roads)} road polylines, {total:.0f} m, {int(mask.sum())} road pixels -> {out}")

    if args.preview:
        preview = image.copy()
        draw = ImageDraw.Draw(preview)
        for r in roads:
            pts = [((y - cal["offsetX"]) / sy, (x - cal["offsetY"]) / sx) for x, y in r["points"]]
            draw.line(pts, fill=(255, 0, 0), width=3)
        preview.save(args.preview)


if __name__ == "__main__":
    main()
