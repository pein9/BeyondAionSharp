"""Decode an Aion client ``<level>-path.dat`` ground/walkability grid.

The format was recovered from the 4.x client's loader in ``bin32/Cry3DEngine.dll``
(function at 0x100B3AF0, the string ``Levels\\%s\\%s-path.dat``). The full
specification and the evidence for each field live in
``docs/client-path-dat-format.md``; the summary below is enough to read the code.

File layout (all little-endian)::

    0x00  16 bytes  random GUID (build id; not checked by the loader)
    0x10  u16 5, u16 6   version; loader requires dword 0x00060005 or major 6
    0x14  108 bytes zero
    0x80  i32 blobSize, then blobSize bytes   sparse-cell record blob
          i32 linkCount, then linkCount u32   edge link table
          sectors, row-major (y outer, x inner), 16 m x 16 m each:
              u8 nodeCount, then nodeCount nodes (layers)

A node covers one sector as a 32 x 32 grid of 0.5 m cells (index = y*32 + x),
or, for type 16, a run of sparse records in the blob. Nodes in one sector are
"layers": the k-th node read gets layer id ``nodeCount - 1 - k``. Node types
0..15 come in pairs; an odd type carries an extra 512-byte table of 4-bit
per-cell exit masks. Every file parses to its exact end with no gaps.

Cell heights are centimetres (``/100`` gives metres). Several node types say
"this cell follows the terrain heightmap" instead of storing a height, and the
heightmap itself is not in this file; pass ``--terrain`` to fill those in.

CLI::

    python decode_path_dat.py <file-path.dat>                       # header + stats
    python decode_path_dat.py <file> --png out.png [--scale 2] [--color kind|height|layers]
    python decode_path_dat.py <file> --bin out.bin                  # every walkable cell
    python decode_path_dat.py <file> --json out.json [--json-cells]
    python decode_path_dat.py <file> --terrain t.r16 --terrain-size 1536 --terrain-unit 2 --validate

Pure standard library.
"""
from __future__ import annotations

import argparse
import json
import math
import struct
import sys
import zlib
from dataclasses import dataclass, field

SECTOR_METRES = 16
CELLS = 32                      # cells per sector side
CELL_METRES = 0.5
NO_HEIGHT = None
TERRAIN = "T"                   # sentinel: height comes from the terrain heightmap

# Exit directions, in the client's order (dx, dy tables at 0x1019AD68/0x1019AD78).
DIRS = ((1, 0), (0, 1), (-1, 0), (0, -1))    # 0:+x 1:+y 2:-x 3:-y

# Payload bytes read after the common 1+16 bytes, by node type (odd types add 512).
PAYLOAD = {0: 4, 2: 0, 4: 4096, 6: 128, 8: 8 + 256, 10: 56 + 512, 12: 4 + 1024, 14: 4 + 2048}

TYPE_NAMES = {
    0: "constant height",
    2: "all terrain",
    4: "i32 height grid",
    6: "terrain + 1-bit hole mask",
    8: "2-bit palette (2 heights)",
    10: "4-bit palette (14 heights)",
    12: "u8 offset grid",
    14: "u16 offset grid",
    16: "sparse records",
}


def record_size(flags: int) -> int:
    """Sparse record size: 9 bytes + a link per direction (2-bit field each)."""
    return 9 + sum((0, 2, 4, 0)[(flags >> (2 * d)) & 3] for d in range(4))


@dataclass
class Node:
    sx: int                     # sector origin, metres
    sy: int
    layer: int                  # client layer id (nodeCount - 1 - read index)
    type: int
    edge_mode: int = 0          # bit d: edge[d] is a 32-bit block mask, else a link-table base
    edges: tuple = (0, 0, 0, 0)
    heights: list | None = None  # 1024 entries: int cm, TERRAIN, or None (no ground)
    exits: bytes | None = None   # odd types: 512 bytes of 4-bit exit masks
    blob_off: int = 0           # type 16
    rec_count: int = 0
    offset: int = 0             # file offset of the node

    def exit_mask(self, lx: int, ly: int) -> int:
        """4-bit exit mask for an odd-type cell (0x100B18C0); 0xF for even types."""
        if self.exits is None:
            return 0xF
        b = self.exits[ly * 16 + lx // 2]
        return (b >> 4) & 0xF if lx & 1 else b & 0xF


@dataclass
class Record:
    offset: int
    z: int                      # centimetres
    cx: int                     # global cell coordinates (0.5 m)
    cy: int
    flags: int
    links: list = field(default_factory=list)  # per direction: raw link or 0


@dataclass
class PathDat:
    guid: bytes
    version: tuple
    blob: bytes
    link_table: list
    sectors_x: int
    sectors_y: int
    nodes: dict                 # (sx, sy) -> list[Node] in file order
    records: dict               # blob offset -> Record
    file_size: int
    parsed_to: int


# --------------------------------------------------------------------------- parse

def _decode_heights(t: int, payload: bytes, pos: int) -> tuple[list, int]:
    """Return (1024 heights, bytes consumed) for grid node types 0..15 (even base)."""
    base = t & ~1
    if base == 0:
        (z,) = struct.unpack_from("<i", payload, pos)
        return [z] * 1024, 4
    if base == 2:
        return [TERRAIN] * 1024, 0
    if base == 4:
        vals = struct.unpack_from("<1024i", payload, pos)
        return [None if v == 0x7FFFFFFF else v for v in vals], 4096
    if base == 6:
        bits = payload[pos:pos + 128]
        return [None if (bits[i >> 3] >> (i & 7)) & 1 else TERRAIN for i in range(1024)], 128
    if base == 8:
        pal = struct.unpack_from("<2i", payload, pos)
        data = payload[pos + 8:pos + 8 + 256]
        out = []
        for i in range(1024):
            v = (data[i >> 2] >> ((i & 3) * 2)) & 3
            out.append(pal[v] if v < 2 else (TERRAIN if v == 2 else None))
        return out, 264
    if base == 10:
        pal = struct.unpack_from("<14i", payload, pos)
        data = payload[pos + 56:pos + 56 + 512]
        out = []
        for i in range(1024):
            b = data[i >> 1]
            v = (b >> 4) if i & 1 else (b & 0xF)
            out.append(pal[v] if v < 14 else (TERRAIN if v == 14 else None))
        return out, 568
    if base == 12:
        (z0,) = struct.unpack_from("<i", payload, pos)
        data = payload[pos + 4:pos + 4 + 1024]
        return [z0 + v if v < 0xFE else (TERRAIN if v == 0xFE else None) for v in data], 1028
    if base == 14:
        (z0,) = struct.unpack_from("<i", payload, pos)
        vals = struct.unpack_from("<1024H", payload, pos + 4)
        return [z0 + v if v < 0xFFFE else (TERRAIN if v == 0xFFFE else None) for v in vals], 2052
    raise ValueError(f"unknown node type {t}")


def parse(path: str) -> PathDat:
    d = open(path, "rb").read()
    if len(d) < 0x88:
        raise ValueError("file too short")
    guid = d[:16]
    minor, major = struct.unpack_from("<HH", d, 0x10)
    if (major, minor) != (6, 5) and major != 6:
        raise ValueError(f"unsupported version {major}.{minor}")
    (blob_size,) = struct.unpack_from("<i", d, 0x80)
    p = 0x84
    blob = d[p:p + blob_size]
    p += blob_size
    (link_count,) = struct.unpack_from("<i", d, p)
    p += 4
    link_table = list(struct.unpack_from(f"<{link_count}I", d, p))
    p += 4 * link_count

    # Sector grid: the loader is told the level size by the engine; the file does not
    # store it. Every shipped level is square, so read sectors until EOF and take sqrt.
    raw_sectors = []
    while p < len(d):
        count = d[p]
        if count >= 128:
            raise ValueError(f"negative node count at 0x{p:x}")
        p += 1
        nodes = []
        for k in range(count):
            node_off = p
            t = d[p]
            p += 1
            if t == 16:
                blob_off, rec_count = struct.unpack_from("<iH", d, p)
                p += 6
                nodes.append(Node(0, 0, count - 1 - k, 16, blob_off=blob_off,
                                  rec_count=rec_count, offset=node_off))
                continue
            if t > 16:
                raise ValueError(f"bad node type {t} at 0x{node_off:x}")
            edge_mode = d[p]
            edges = struct.unpack_from("<4I", d, p + 1)
            p += 17
            heights, used = _decode_heights(t, d, p)
            assert used == PAYLOAD[t & ~1]
            p += used
            exits = None
            if t & 1:
                exits = d[p:p + 512]
                p += 512
            nodes.append(Node(0, 0, count - 1 - k, t, edge_mode, edges, heights, exits,
                              offset=node_off))
        raw_sectors.append(nodes)
    side = math.isqrt(len(raw_sectors))
    if side * side != len(raw_sectors):
        raise ValueError(f"{len(raw_sectors)} sectors is not a square grid")
    grid = {}
    for i, nodes in enumerate(raw_sectors):
        sx, sy = (i % side) * SECTOR_METRES, (i // side) * SECTOR_METRES
        for n in nodes:
            n.sx, n.sy = sx, sy
        grid[(sx, sy)] = nodes

    records = {}
    for nodes in grid.values():
        for n in nodes:
            if n.type != 16:
                continue
            off = n.blob_off
            for _ in range(n.rec_count):
                z, cx, cy, flags = struct.unpack_from("<ihhB", blob, off)
                q = off + 9
                links = []
                for dd in range(4):
                    f = (flags >> (2 * dd)) & 3
                    if f == 1:
                        (w,) = struct.unpack_from("<H", blob, q)
                        q += 2
                        links.append(((w + n.blob_off) << 7) & 0xFFFFFF80)
                    elif f == 2:
                        (v,) = struct.unpack_from("<I", blob, q)
                        q += 4
                        links.append(v)
                    else:
                        links.append(0)
                records[off] = Record(off, z, cx, cy, flags, links)
                off += record_size(flags)
    return PathDat(guid, (major, minor), blob, link_table, side, side, grid, records,
                   len(d), p)


# --------------------------------------------------------------------------- queries

class Terrain:
    """Aion .r16 heightmap: sample (x, y) at row = x/unit, column = y/unit."""

    def __init__(self, path: str, size: int, unit: float):
        raw = open(path, "rb").read()
        if len(raw) != size * size * 2:
            raise ValueError(f"terrain is {len(raw)} bytes, expected {size * size * 2}")
        self.h = struct.unpack(f"<{size * size}H", raw)
        self.size, self.unit = size, unit

    def sample(self, r: int, c: int):
        r = min(max(r, 0), self.size - 1)
        c = min(max(c, 0), self.size - 1)
        v = self.h[c + r * self.size]
        return None if v == 65535 else v * 2048.0 / 65536.0

    def z(self, x: float, y: float):
        """Bilinear height in metres, None over a hole."""
        fx, fy = x / self.unit, y / self.unit
        r0, c0 = int(math.floor(fx)), int(math.floor(fy))
        tx, ty = fx - r0, fy - c0
        s = [self.sample(r0, c0), self.sample(r0 + 1, c0),
             self.sample(r0, c0 + 1), self.sample(r0 + 1, c0 + 1)]
        if any(v is None for v in s):
            return next((v for v in s if v is not None), None)
        return (s[0] * (1 - tx) * (1 - ty) + s[1] * tx * (1 - ty)
                + s[2] * (1 - tx) * ty + s[3] * tx * ty)


def _node_by_layer(pd: PathDat, sx: int, sy: int, layer: int):
    for n in pd.nodes.get((sx, sy), ()):
        if n.layer == layer:
            return n
    return None


def _sector_of(cx: int, cy: int) -> tuple[int, int]:
    return (cx // CELLS) * SECTOR_METRES, (cy // CELLS) * SECTOR_METRES


def _follow_link(pd: PathDat, link: int, src: Node, cx: int, cy: int):
    """Resolve a link to ('grid', node, cx, cy) or ('rec', node, record) (client 0x100B2D30).

    ``link >> 7`` is a blob offset (1 = "a grid cell", records start at 4) and
    ``link & 0x7F`` the target layer id. As in the client, the layer id is only
    consulted when the step leaves the source sector; otherwise the node stays.
    """
    if link == 0:
        return None
    sx, sy = _sector_of(cx, cy)
    node = src if (sx, sy) == (src.sx, src.sy) else _node_by_layer(pd, sx, sy, link & 0x7F)
    off = link >> 7
    if node is None:
        return ("dangling", None, cx, cy)
    if off == 1:
        return ("grid", node, cx, cy)
    return ("rec", node, pd.records.get(off))


def grid_exits(pd: PathDat, node: Node, lx: int, ly: int) -> list:
    """Destinations reachable from a grid cell, per direction (client step 0x100B3070)."""
    cx, cy = node.sx * 2 + lx, node.sy * 2 + ly
    out = []
    for d, (dx, dy) in enumerate(DIRS):
        nx, ny = lx + dx, ly + dy
        if 0 <= nx < CELLS and 0 <= ny < CELLS:
            if not node.exit_mask(lx, ly) >> d & 1:
                out.append(None)
            elif node.heights[ny * CELLS + nx] is None:
                out.append(None)
            else:
                out.append(("grid", node, cx + dx, cy + dy))
            continue
        along = ly if d % 2 == 0 else lx
        if node.edge_mode >> d & 1:
            if node.edges[d] >> along & 1:
                out.append(None)
            else:
                sx, sy = _sector_of(cx + dx, cy + dy)
                tgt = _node_by_layer(pd, sx, sy, 0)
                out.append(("grid", tgt, cx + dx, cy + dy) if tgt else None)
        else:
            idx = node.edges[d] + along
            link = pd.link_table[idx] if idx < len(pd.link_table) else 0
            out.append(_follow_link(pd, link, node, cx + dx, cy + dy))
    return out


def record_exits(pd: PathDat, node: Node, rec: Record) -> list:
    out = []
    for d, (dx, dy) in enumerate(DIRS):
        out.append(_follow_link(pd, rec.links[d], node, rec.cx + dx, rec.cy + dy))
    return out


def iter_cells(pd: PathDat, terrain: Terrain | None = None, with_exits: bool = True):
    """Yield (cx, cy, z_metres|None, node_type, layer, exits4, is_terrain) per walkable cell."""
    for (sx, sy), nodes in pd.nodes.items():
        for n in nodes:
            if n.type == 16:
                continue
            for i, h in enumerate(n.heights):
                if h is None:
                    continue
                lx, ly = i % CELLS, i // CELLS
                cx, cy = sx * 2 + lx, sy * 2 + ly
                if h is TERRAIN:
                    z = terrain.z(cx * CELL_METRES + 0.25, cy * CELL_METRES + 0.25) if terrain else None
                    is_t = True
                else:
                    z, is_t = h / 100.0, False
                ex = 0
                if with_exits:
                    for d, dest in enumerate(grid_exits(pd, n, lx, ly)):
                        if dest is not None:
                            ex |= 1 << d
                yield cx, cy, z, n.type, n.layer, ex, is_t
    for n in (n for ns in pd.nodes.values() for n in ns if n.type == 16):
        off = n.blob_off
        for _ in range(n.rec_count):
            r = pd.records[off]
            ex = sum(1 << d for d in range(4) if r.links[d]) if with_exits else 0
            yield r.cx, r.cy, r.z / 100.0, 16, n.layer, ex, False
            off += record_size(r.flags)


# --------------------------------------------------------------------------- checks

def self_check(pd: PathDat) -> dict:
    """Structural invariants: blob fully covered, records inside their sector, links land."""
    covered = bytearray(len(pd.blob))
    outside = 0
    for ns in pd.nodes.values():
        for n in ns:
            if n.type != 16:
                continue
            off = n.blob_off
            for _ in range(n.rec_count):
                r = pd.records[off]
                sz = record_size(r.flags)
                for b in range(off, off + sz):
                    covered[b] += 1
                if _sector_of(r.cx, r.cy) != (n.sx, n.sy):
                    outside += 1
                off += sz
    uncovered = [i for i, c in enumerate(covered) if c == 0]
    overlap = sum(1 for c in covered if c > 1)
    links = ok = bad = 0
    edge_links = edge_ok = 0
    for ns in pd.nodes.values():
        for n in ns:
            if n.type == 16:
                off = n.blob_off
                for _ in range(n.rec_count):
                    r = pd.records[off]
                    for d, dest in enumerate(record_exits(pd, n, r)):
                        if r.links[d]:
                            links += 1
                            good = _dest_ok(dest, r.cx + DIRS[d][0], r.cy + DIRS[d][1])
                            ok += good
                            bad += not good
                    off += record_size(r.flags)
                continue
            # Edge crossings through the link table (edge_mode bit clear).
            for d, (dx, dy) in enumerate(DIRS):
                if n.edge_mode >> d & 1:
                    continue
                for along in range(CELLS):
                    lx, ly = ((CELLS - 1 if dx > 0 else 0, along) if d % 2 == 0
                              else (along, CELLS - 1 if dy > 0 else 0))
                    if n.heights[ly * CELLS + lx] is None:
                        continue
                    link = pd.link_table[n.edges[d] + along]
                    if not link:
                        continue
                    cx, cy = n.sx * 2 + lx + dx, n.sy * 2 + ly + dy
                    edge_links += 1
                    edge_ok += _dest_ok(_follow_link(pd, link, n, cx, cy), cx, cy)
    return {
        "parsedToEof": pd.parsed_to == pd.file_size,
        "blobBytes": len(pd.blob),
        "blobUncoveredBytes": len(uncovered),
        "blobUncoveredRanges": _ranges(uncovered)[:8],
        "blobOverlapBytes": overlap,
        "recordsOutsideOwnSector": outside,
        "recordLinks": links,
        "recordLinksLandingOnMatchingCell": ok,
        "recordLinksBroken": bad,
        "gridEdgeLinks": edge_links,
        "gridEdgeLinksLandingOnGround": edge_ok,
    }


def _dest_ok(dest, cx: int, cy: int) -> bool:
    """A link is sound when it lands on a record at (cx, cy) or a grid cell with ground."""
    if not dest or dest[1] is None:
        return False
    if dest[0] == "rec":
        return dest[2] is not None and (dest[2].cx, dest[2].cy) == (cx, cy)
    node = dest[1]
    return node.type != 16 and node.heights[(cy % CELLS) * CELLS + cx % CELLS] is not None


def _ranges(xs):
    out = []
    for x in xs:
        if out and out[-1][1] == x - 1:
            out[-1][1] = x
        else:
            out.append([x, x])
    return out


def stats(pd: PathDat) -> dict:
    types = {}
    layers_hist = {}
    cells = {"explicit": 0, "terrain": 0, "none": 0, "sparse": len(pd.records)}
    for ns in pd.nodes.values():
        layers_hist[len(ns)] = layers_hist.get(len(ns), 0) + 1
        for n in ns:
            types[n.type] = types.get(n.type, 0) + 1
            if n.heights is None:
                continue
            for h in n.heights:
                if h is None:
                    cells["none"] += 1
                elif h is TERRAIN:
                    cells["terrain"] += 1
                else:
                    cells["explicit"] += 1
    return {
        "guid": pd.guid.hex(),
        "version": f"{pd.version[0]}.{pd.version[1]}",
        "fileSize": pd.file_size,
        "blobSize": len(pd.blob),
        "linkTableEntries": len(pd.link_table),
        "sectors": f"{pd.sectors_x}x{pd.sectors_y} of {SECTOR_METRES} m",
        "worldMetres": pd.sectors_x * SECTOR_METRES,
        "nodes": sum(types.values()),
        "nodeTypes": {f"{t} ({TYPE_NAMES[t & ~1 if t < 16 else 16]}{', +exit masks' if t < 16 and t & 1 else ''})": c
                      for t, c in sorted(types.items())},
        "nodesPerSector": dict(sorted(layers_hist.items())),
        "gridCells": cells,
    }


def validate(pd: PathDat, terrain: Terrain) -> dict:
    """Compare every explicit-height cell with the terrain heightmap."""
    res = {}
    for label, swap in (("x=cx/2,y=cy/2", False), ("swapped", True)):
        n = w1 = w3 = above = below = 0
        for cx, cy, z, t, layer, ex, is_t in iter_cells(pd, None, with_exits=False):
            if is_t or z is None:
                continue
            x, y = cx * CELL_METRES + 0.25, cy * CELL_METRES + 0.25
            tz = terrain.z(y, x) if swap else terrain.z(x, y)
            if tz is None:
                continue
            n += 1
            dz = z - tz
            w1 += abs(dz) <= 1
            w3 += abs(dz) <= 3
            above += dz > 3
            below += dz < -3
        res[label] = {"cells": n, "within1m": round(w1 / max(n, 1), 4),
                      "within3m": round(w3 / max(n, 1), 4),
                      "above3m": round(above / max(n, 1), 4),
                      "below3m": round(below / max(n, 1), 4)}
    return res


def divergence(pd: PathDat, terrain: Terrain, top: int = 15) -> list:
    """Sectors with the most explicit cells more than 3 m off the terrain."""
    per = {}
    for cx, cy, z, t, layer, ex, is_t in iter_cells(pd, None, with_exits=False):
        if is_t:
            continue
        tz = terrain.z(cx * CELL_METRES + 0.25, cy * CELL_METRES + 0.25)
        if tz is None or abs(z - tz) <= 3:
            continue
        key = _sector_of(cx, cy)
        e = per.setdefault(key, [0, 0.0, -1e9, 1e9])
        e[0] += 1
        e[1] += z - tz
        e[2] = max(e[2], z - tz)
        e[3] = min(e[3], z - tz)
    rows = sorted(per.items(), key=lambda kv: -kv[1][0])[:top]
    return [{"sectorX": k[0], "sectorY": k[1], "cells": v[0], "meanDz": round(v[1] / v[0], 1),
             "maxDz": round(v[2], 1), "minDz": round(v[3], 1)} for k, v in rows]


# --------------------------------------------------------------------------- output

def write_png(path: str, width: int, height: int, rgb: bytearray) -> None:
    """Minimal RGB PNG writer (zlib + struct). Row 0 is the top of the image."""
    raw = bytearray()
    stride = width * 3
    for y in range(height):
        raw.append(0)
        raw += rgb[y * stride:(y + 1) * stride]

    def chunk(tag, body):
        c = struct.pack(">I", len(body)) + tag + body
        return c + struct.pack(">I", zlib.crc32(tag + body) & 0xFFFFFFFF)

    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)))
        f.write(chunk(b"IDAT", zlib.compress(bytes(raw), 6)))
        f.write(chunk(b"IEND", b""))


def render(pd: PathDat, out: str, scale: int, color: str, terrain: Terrain | None) -> None:
    """Top-down image; +x to the right, +y upward (image row 0 = max y)."""
    n = pd.sectors_x * CELLS
    w = h = n // scale
    kind = bytearray(w * h)            # 0 none, 1 terrain, 2 explicit, 3 sparse
    count = bytearray(w * h)
    zmax = [None] * (w * h)
    zs = []
    for cx, cy, z, t, layer, ex, is_t in iter_cells(pd, terrain, with_exits=False):
        px, py = cx // scale, cy // scale
        if not (0 <= px < w and 0 <= py < h):
            continue
        i = (h - 1 - py) * w + px
        k = 3 if t == 16 else (1 if is_t else 2)
        kind[i] = max(kind[i], k)
        if count[i] < 255:
            count[i] += 1
        if z is not None and (zmax[i] is None or z > zmax[i]):
            zmax[i] = z
    zs = [z for z in zmax if z is not None]
    lo, hi = (min(zs), max(zs)) if zs else (0.0, 1.0)
    span = max(hi - lo, 1e-6)
    rgb = bytearray(w * h * 3)
    per_cell = scale * scale
    for i in range(w * h):
        k = kind[i]
        if k == 0:
            c = (18, 18, 24)
        else:
            shade = 0.35 + 0.65 * ((zmax[i] - lo) / span) if zmax[i] is not None else 0.7
            layers = count[i] / per_cell
            if color == "height":
                g = int(255 * shade)
                c = (g, g, g)
            elif color == "layers":
                c = ((70, 150, 70) if layers <= 1.01 else
                     (230, 160, 40) if layers <= 2.01 else (230, 60, 60))
                c = tuple(int(v * shade) for v in c)
            else:
                base = {1: (70, 160, 70), 2: (70, 120, 230), 3: (220, 70, 220)}[k]
                if layers > 1.01:
                    base = (240, 150, 40)
                c = tuple(min(255, int(v * shade)) for v in base)
        rgb[i * 3:i * 3 + 3] = bytes(c)
    write_png(out, w, h, rgb)


BIN_MAGIC = b"APDG"
BIN_RECORD = struct.Struct("<hhfBBBB")  # cx, cy, z, nodeType, layer, exits, flags


def write_bin(pd: PathDat, out: str, terrain: Terrain | None) -> int:
    """Binary cell list. See docs/client-path-dat-format.md, 'Export format'."""
    rows = sorted(iter_cells(pd, terrain, with_exits=True), key=lambda r: (r[1], r[0], r[4]))
    with open(out, "wb") as f:
        f.write(BIN_MAGIC)
        f.write(struct.pack("<IiifI", 1, pd.sectors_x * CELLS, pd.sectors_y * CELLS,
                            CELL_METRES, len(rows)))
        for cx, cy, z, t, layer, ex, is_t in rows:
            flags = (1 if is_t else 0) | (2 if t == 16 else 0) | (4 if z is None else 0)
            f.write(BIN_RECORD.pack(cx, cy, float("nan") if z is None else z, t, layer, ex, flags))
    return len(rows)


def write_json(pd: PathDat, out: str, terrain: Terrain | None, cells: bool) -> None:
    doc = {"header": stats(pd), "check": self_check(pd), "cellMetres": CELL_METRES,
           "sectorMetres": SECTOR_METRES, "sectors": []}
    for (sx, sy), ns in sorted(pd.nodes.items(), key=lambda kv: (kv[0][1], kv[0][0])):
        if not ns:
            continue
        doc["sectors"].append({"x": sx, "y": sy, "nodes": [
            {"layer": n.layer, "type": n.type, "edgeMode": n.edge_mode, "edges": list(n.edges)}
            if n.type != 16 else {"layer": n.layer, "type": 16, "blobOffset": n.blob_off,
                                  "records": n.rec_count} for n in ns]})
    doc["sparseRecords"] = [[r.cx, r.cy, r.z, r.flags, r.links] for r in pd.records.values()]
    if cells:
        doc["cells"] = [[cx, cy, None if z is None else round(z, 2), t, layer, ex]
                        for cx, cy, z, t, layer, ex, is_t in iter_cells(pd, terrain)]
    with open(out, "w", encoding="utf-8") as f:
        json.dump(doc, f, separators=(",", ":"))


def main() -> None:
    ap = argparse.ArgumentParser(description="Decode an Aion client <level>-path.dat grid.")
    ap.add_argument("path")
    ap.add_argument("--png", help="write a top-down PNG")
    ap.add_argument("--scale", type=int, default=2, help="cells per pixel (default 2 = 1 px/m)")
    ap.add_argument("--color", choices=("kind", "height", "layers"), default="kind")
    ap.add_argument("--bin", help="write every walkable cell as binary (APDG v1)")
    ap.add_argument("--json", help="write header, sectors and sparse records as JSON")
    ap.add_argument("--json-cells", action="store_true", help="also put every cell in the JSON")
    ap.add_argument("--terrain", help=".r16 heightmap (uint16, row = x / unit)")
    ap.add_argument("--terrain-size", type=int, default=1536)
    ap.add_argument("--terrain-unit", type=float, default=2.0)
    ap.add_argument("--validate", action="store_true", help="compare explicit heights with --terrain")
    ap.add_argument("--check", action="store_true", help="run structural self-checks")
    a = ap.parse_args()

    pd = parse(a.path)
    print(json.dumps(stats(pd), indent=2))
    if a.check:
        print(json.dumps(self_check(pd), indent=2))
    terrain = Terrain(a.terrain, a.terrain_size, a.terrain_unit) if a.terrain else None
    if a.validate:
        if not terrain:
            sys.exit("--validate needs --terrain")
        print(json.dumps({"validation": validate(pd, terrain),
                          "topDivergentSectors": divergence(pd, terrain)}, indent=2))
    if a.png:
        render(pd, a.png, a.scale, a.color, terrain)
        print(f"wrote {a.png}")
    if a.bin:
        print(f"wrote {write_bin(pd, a.bin, terrain)} cells to {a.bin}")
    if a.json:
        write_json(pd, a.json, terrain, a.json_cells)
        print(f"wrote {a.json}")


if __name__ == "__main__":
    main()
