# Client `<level>-path.dat` format

Every level folder in the client ships a `<LEVEL>-path.dat`, for example
`Levels/df1/DF1-path.dat` for Ishalgen (220010000). The file is NCSoft's own
ground and walkability grid. It covers the whole level in 0.5 m cells, gives the
height of the walkable surface in each cell, can hold several stacked layers
(for bridges and caves), and links each cell to its neighbours.

The format is **fully decoded**. It was read out of the client's loader rather
than guessed from the bytes. Decoder: `tools/client-extract/decode_path_dat.py`.

## Where it was found

`bin32/Cry3DEngine.dll`, the 4.x client (image base 0x10000000):

| Address | Role |
|---|---|
| `0x100B3AF0` | loader. Opens `Levels\%s\%s-path.dat`, falling back to `Levels\%s\pathfind.pak` / `pathfind\%s-path.dat` |
| `0x100B1920` | height of cell (i, j) in a node. This is how each node type's payload is read |
| `0x100B3070` | one step from a cell in direction d. Returns 0 (blocked), 1 (one-way) or 2 (two-way) |
| `0x100B2D30` | crossing a sector edge, or following a sparse-record link |
| `0x100B16B0` | reads one link out of a sparse record |
| `0x100B18C0` | reads the 4-bit exit mask of a cell in an odd-type node |
| `0x100B2CC0` | finds the node in a sector that has a given layer id |
| `0x100B3260` | debug draw. This is what proves that sparse records carry global cell x/y |
| `0x1019BD88` | 256-entry table of sparse-record sizes |
| `0x1019AD68` / `0x1019AD78` | dx = (1,0,-1,0), dy = (0,1,0,-1) |

The disassembly was done with capstone in a throwaway venv. The decoder itself
uses only the standard library.

## Layout

Everything is little-endian.

```
0x00  16 B   GUID (random, version-4 layout). Two files with identical bodies
             (idldf5re_03, idldf5re_03_l) have different GUIDs, so this is a build
             id, not a hash. The loader reads it and ignores it.
0x10  u16 5, u16 6   format version. The loader accepts dword 0x00060005, or any major 6.
0x14  108 B  zero
0x80  i32 blobSize            <- the "uint32 at 0x80": the byte size of the next block
0x84  blobSize bytes          sparse-record blob. Bytes 0..3 are reserved (zero), so
                              offsets 0 and 1 can serve as sentinels
....  i32 linkCount
....  linkCount x u32         edge link table
....  sectors until EOF
```

The earlier "uint32 at 0x88" (32114 for DF1) is simply the first record in the
blob: its height, 321.14 m.

**Sectors.** A sector is 16 m x 16 m. Sectors are stored row-major with y as the
outer loop, from world (0, 0). The loader is told the level size by the engine,
and the file does not store it. Every one of the 191 shipped files holds a square
number of sectors, so the decoder counts them and takes the square root. DF1 and
LF1 are 192 x 192 sectors (3072 m). Small instances are 32 x 32 (512 m).

```
u8 nodeCount           signed, < 128. 0 = no walkable ground in this sector
nodeCount x node
```

The k-th node read in a sector gets **layer id = nodeCount - 1 - k** (`node+0x28`).
Links address nodes by this id.

**Node.** Each node starts with a type byte `t` (0..16).

For t = 0..15 the node next holds a common part:

```
u8   edgeMode          bit d set:   edges[d] is a 32-bit mask of cells blocked on edge d
                       bit d clear: edges[d] is a base index into the link table
u32  edges[4]          d = 0:+x  1:+y  2:-x  3:-y
```

After the common part comes a payload that depends on the type. Types come in
pairs. An odd type has the same payload as the even type below it, plus 512 bytes
at the end (see "Odd types").

Heights are **signed centimetres**. "Terrain" means "use the terrain heightmap
here", and that heightmap is not in this file. "None" means there is no walkable
ground in the cell on this layer. The cell index is `y*32 + x`, with local x/y
from 0 to 31.

| t | Payload | How a cell's height is read |
|---|---|---|
| 0/1 | i32 z | every cell is z |
| 2/3 | nothing | every cell is terrain |
| 4/5 | 1024 x i32 | the value; 0x7FFFFFFF = none |
| 6/7 | 128 B bitmask | bit 1 = none, bit 0 = terrain |
| 8/9 | 2 x i32 palette, then 256 B of 2-bit codes | codes 0-1 = palette entry, 2 = terrain, 3 = none |
| 10/11 | 14 x i32 palette, then 512 B of 4-bit codes (low nibble first) | codes 0-13 = palette entry, 14 = terrain, 15 = none |
| 12/13 | i32 base, then 1024 x u8 | base + v; 0xFE = terrain, 0xFF = none |
| 14/15 | i32 base, then 1024 x u16 | base + v; 0xFFFE = terrain, 0xFFFF = none |
| 16 | i32 blobOffset, u16 recordCount | sparse records; no common part is stored |

**Odd types.** The extra 512 bytes are 4-bit exit masks, one per cell: index
`y*16 + x/2`, with the high nibble used when x is odd. Bit d is set when the cell
may be left in direction d.

**Sparse record** (in the blob, at `blobOffset`, `recordCount` of them back to back):

```
i32 z (cm)   i16 cx   i16 cy   u8 linkFlags   links...
```

- `cx` and `cy` are *global* cell coordinates. World x = cx * 0.5 + 0.25, and
  world y likewise.
- `linkFlags` holds four 2-bit fields, one per direction d:
  - 0 or 3: no link
  - 1: a u16 offset, relative to the node's `blobOffset`
  - 2: a raw u32 link
- The record size is therefore 9 + the bytes its links use, which reproduces the
  client's size table exactly.

**Link value.** A link is a u32:

- `link >> 7` is a blob offset. The value 1 means "a grid cell" rather than a
  record.
- `link & 0x7F` is the target node's layer id. The id is consulted only when the
  step leaves the current sector.
- A link of 0 is blocked.

The link table always begins with the same 4000 entries: 32 zeros, then 124 rows
of 32 copies of `0x80 | k`. Each row means "every cell on this edge crosses into
the grid of layer k". An edge whose cells all go to the same place just points at
the matching row.

**Stepping** (`0x100B3070`):

- **Inside a sector.** The target cell must have ground in the same node. In odd
  types, bit d of the source cell's exit mask must also be set.
- **Across a sector edge** (x>>5 or y>>5 changes).
  - If the edge-mask bit for d is set, a blocked cell stays put. Otherwise the step
    goes to layer 0 of the next sector.
  - Otherwise the link is `linkTable[edges[d] + along]`, where `along` is y&31
    for d = 0/2 and x&31 for d = 1/3.
- **From a sparse record.** Its own link is followed.
- **Return value.** 2 when the reverse step comes back to the source, else 1.

## Confidence

| Field | Confidence | Basis |
|---|---|---|
| Header, blob, link table, sector loop, node payload sizes | certain | from the loader; all 191 files parse to exactly EOF |
| Height decoding per type, cm units, sentinels | certain | from `0x100B1920` |
| Layer id = count-1-k; how links are encoded; sparse record layout | certain | from the loader and `0x100B2D30` / `0x100B16B0` / `0x100B3260`. Of 39,202,351 record links and 41,900,902 edge links across all 191 files, **all** land on a record with the expected (cx, cy) or on a grid cell that has ground. The blob is covered exactly (only the 4 reserved bytes are unused, with no overlap), and no record lies outside its own sector |
| Axis mapping: file x/y = emulator world x/y | certain | see the validation below; the swapped mapping scores 0 % |
| GUID meaning | high | version-4 bits; identical bodies carry different GUIDs |
| *Why* a cell is "none" (the generator's rules) | inferred | see the slope evidence below |

## Validation: DF1 (Ishalgen)

Checked against the emulator heightmap `terrain.220010000.r16` (bilinear;
row = x/2, column = y/2).

- **Composition.**
  - 3814 of 36,864 sectors hold data. The other 33,050 are empty, which means
    unplayable space. The PNG outline matches the zone's shape.
  - Grid cells: 2,856,236 terrain, 134,463 explicit height, 1,486,229 none.
  - 40,171 sparse records.
  - 15,514 (x, y) positions have 2 or 3 stacked layers.
- **Explicit-height cells against terrain** (173,773 cells): 70.1 % within 1 m,
  79.4 % within 3 m. 13.9 % sit more than 3 m *below* the terrain and 6.7 % more
  than 3 m *above* it. With x and y swapped, only 0.2 % fall within 1 m.
- **Where they diverge.** The largest block is sectors x 624-688, y 848-1040
  (about 20 sectors), where cells lie 30-58 m below the terrain. That is an
  underground cave with a terrain layer above it, and it is the orange patch in
  `--color layers`. Sector (1216, 1872) sits a steady 11.7-12.7 m above the
  terrain, which is a raised structure. Other divergences are building floors and
  bridges.
- **Emulator spawns** (`spawns.220010000.json`, 1386 spots): 99.3 % sit within
  1 m of a walkable path cell (checking a 3 x 3 cell neighbourhood). Only 6 have
  no cell at all. For the **walker routes** (1175 steps), 98.0 % are within 1 m.
  With the axes swapped, both scores are 0 %.
- **Slope.** Among terrain-type cells in single-layer sectors, walkable cells
  almost never exceed 45° (about 0.1 % are at 50° or more). 80 % of "none" cells
  are on slopes of 40° or more. The remaining flat "none" cells are the dark
  specks in the PNG: trees, rocks and building footprints. So "none" means
  "too steep, or blocked by an object".
- **Exits.** 2,882,721 cells have all 4 exits, 119,684 have 3, 26,992 have 2 and
  1473 have 1.

Reproduce:

```
python tools/client-extract/decode_path_dat.py "C:/Program Files (x86)/Beyond Aion/Levels/df1/DF1-path.dat" --check \
  --terrain <ProjectObelisk>/reference/ishalgen/terrain.220010000.r16 --validate --png df1.png
```

## Export format (`--bin`, "APDG" v1)

```
char[4] "APDG"   u32 version=1   i32 cellsX   i32 cellsY   f32 cellMetres=0.5   u32 count
count x 12 B, sorted by (cy, cx, layer):
  i16 cx   i16 cy   f32 z (metres; NaN if terrain-following and no --terrain was given)
  u8 nodeType (0..16)   u8 layer   u8 exits (bit d = a step in direction d exists)   u8 flags
  flags: bit0 = height taken from the terrain heightmap, bit1 = sparse record,
         bit2 = z unknown
```

World position of a cell centre is (cx * 0.5 + 0.25, cy * 0.5 + 0.25, z). The
`exits` field records that a link exists. It does not record whether the link is
two-way. DF1 exports as 3,030,870 cells in 36 MB.

`--json` writes the header, the self-check, per-sector nodes and all sparse
records. Add `--json-cells` to include every cell as well; that is large for
open-world maps.

## Use as walkability ground truth for the bot navmesh

**Verdict: usable.** The file is the client's own answer to "can a character
stand here, and at what height", at 0.5 m resolution. It covers the terrain and
every placed structure layer, and gives neighbour connectivity. Its heights agree
with emulator spawns and walker routes at the 98-99 % level.

To compare it with a Recast navmesh baked from the emulator geodata:

1. **Coverage.** For each cell, take its centre (x, y) and z, and check whether
   the navmesh has a polygon within about 0.5 m vertically. Report:
   - cells with no navmesh polygon: the navmesh is too conservative, for example
     slope or climb limits set too tight, or missing geometry;
   - navmesh area with no cell on any layer: the navmesh is too permissive, for
     example it walks through obstacles or onto steep slopes the client forbids.
2. **Height.** Compare z only on explicit-height and sparse cells. Terrain cells
   come from the same heightmap, so comparing them proves nothing.
3. **Connectivity.** Build a 4-neighbour graph from `exits` and compare its
   connected components, and a sample of shortest paths, with the navmesh.
4. **Tuning.** A 45° walkable slope reproduces the client's terrain cut-off.

**Caveats:**

- Terrain-following cells need the heightmap to get a z. Use the same `.r16` the
  emulator uses.
- The file holds no agent radius or step height. Its rules come from NCSoft's
  generator, which we only have the output of.
- Dynamic objects (doors, siege gates) are not represented.
- This is 4.x client data. It matches this port's content boundary, so it can be
  used directly.
