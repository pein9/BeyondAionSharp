# Bot navigation options: a map any playtest bot can route on

Discovery session, 2026-09-24. Scope: how the QA playtest bot should plan
ground routes from A to B on any map, starting with Ishalgen (220010000),
and how it should know where *not* to go. This is bot/test infrastructure.
Java has no pathfinding (its `SimpleAttackManager` still carries the note
"delete geo check when we've implemented a pathfinding system"), so nothing
here touches server NPC movement or the Java-is-spec rule.

> **Status 2026-09-24: implemented.** Options A (DotRecast navmesh), D (roads and travel graph) and
> the side quest C (client `path.dat` decoded, now the playable-area mask and validation ground truth)
> are built; see [bot-navigation.md](bot-navigation.md). The measurements below describe the grid
> search the navmesh replaced.

## 1. Where the bot stands today

The bot has no baked navigation data. Every route is searched online against
the server's collision engine:

1. A per-map waypoint graph of spawn spots, patrol-route steps, portals and
   bind points (`BotNavigationGraphFactory`), with edges only between points
   closer than 20 m. Ishalgen has 2,480 such waypoints.
2. When the graph has no path, a 2 m grid A* (`BotNavigationGeometry.FindGroundPath`)
   that ray-casts every candidate step through `GeoMap.GetZ` and
   `GeoMap.GetCollisions`, bounded by a visited-cell budget and a padded
   bounding box around start and destination.
3. A hand-transcribed road centerline (`NaturalIshalgenRoads`) that lowers step
   cost near the road, plus observed aggro circles as forbidden areas.

Measured on 2026-09-24 with the offline `BotNavigationAssets` load (Asmodian
race, instance 1, exact NPC spawn spots from the shipped Ishalgen spawn file,
no hazards; i7-14700K, Debug build):

| Leg | Distance | Waypoint graph | Journey A* (padding 60 m, 65k cells) | Journey A* avoiding (padding 180 m, 131k cells) |
|---|---:|---|---|---|
| Ulgorn -> Nobekk | 73 m | no route, 0.2 s | no route, 4.2 s | no route, 17.4 s |
| Mijou -> Mau farm (819,1548) | 200 m | no route | 141 pts, 2.5 s | 141 pts, 2.6 s (road-preferred: 156 pts, 2.9 s) |
| Ulgorn -> Dabi | 339 m | no route | no route, 2.1 s | 384 pts, 12.4 s |
| Dabi -> Derot | 524 m | no route | no route, 7.6 s | 412 pts, 18.6 s |
| Derot -> Nalto | 588 m | no route | 444 pts, 13.3 s | 444 pts, 15.4 s |
| Mijou -> Munin | 603 m | no route | no route, 19.6 s | no route, 33.2 s |
| Ulgorn -> Mijou | 802 m | no route, 0.1 s | no route, 2.0 s | 772 pts, 33.1 s |

The two NPC legs that never routed to the exact spawn point do route with the
interaction variant (`FindInteractionPath`, any checked point within 3 m of
the NPC, 60 m padding): Ulgorn -> Nobekk in 0.7 s (46 points) and
Mijou -> Munin in 9.6 s (404 points). Starting *from* Nobekk's or Munin's
exact spot fails instantly because the spot itself is not valid ground in the
checked-in geodata, which is the known NPC-in-collision case the interaction
search exists for. Ulgorn -> Mijou (802 m) still finds nothing under the
interaction variant after 32.5 s, and the reverse Mijou -> Ulgorn search ran
for 303.6 s before giving up, because that variant retries a full journey
search for each of sixteen fallback points around the target.

A single 15 m `TraceEdge` costs 0.34 ms, so the collision engine is not the
problem. The cost is the search: up to 131k expanded cells times eight
neighbours, each a fresh ray cast, and nothing is cached between searches.
The journey log (`docs/natural-ishalgen-journey.md`) records the consequences:
searches that hit the eight-minute test bound once hazards multiply the
search space, dead ends at (579,2430) and (620,2439), packs that close every
checked route, and a Return-skill fallback added because no ground route
could be found from a trapped pocket. The waypoint graph never produced a
route on any leg: 20 m edges over sparse spawn points fragment it into islands.

The searches fail on budgets, not on terrain. The same Ulgorn -> Dabi leg
that fails at 60 m padding succeeds at 180 m. A planner that must re-derive
walkability from ray casts on every call cannot be given enough budget to be
reliable and still finish in seconds.

## 2. What everyone else converged on

Every MMO emulator or bot project that solved "walk from A to B across an
outdoor zone" ended up with the same three layers:

1. **A baked walkability representation per map**, queried in milliseconds.
   WoW emulators (TrinityCore, cMaNGOS, AzerothCore) bake Recast/Detour
   "mmaps" from extracted terrain and collision models. EQEmu bakes Recast
   `.nav` files per zone with an editor for off-mesh links. Lineage 2 emulators
   use the client's per-cell geodata grid (height plus N/S/E/W flags) with A*.
   An Aion 5.8 Java emulator (AionEncomBase) baked navmeshes with recast4j from
   `.obj` exports of the same `models.mesh`/`.geo` format this port ships.
2. **A coarse travel graph on top** for long distances and for policy: the
   Playerbots "TravelNode" graph places nodes at innkeepers, flight masters,
   portals, transports and zone centres, generates walk links by running the
   local pathfinder between nodes and keeping only links that succeed, stores
   the result in tables, and A*-searches the graph before walking each leg with
   the navmesh. OpenKore does the same with a portal graph over per-map fields.
   This layer is where "roads with branches" and "avoid the level-8 camp" live.
3. **A runtime check** that the emitted path is still valid against live state
   (doors, spawned obstacles, observed hostiles). The bot already has this: its
   `TraceEdge` validation and aggro-circle rules should stay in front of any
   baked planner.

Recorded paths went the other way. Glider profiles were recorded waypoint
loops and rotted every patch; Honorbuddy replaced user-recorded meshes with a
Recast navmesh served centrally; WRobot and MMOMinion forums are full of
recorded routes walking into walls after updates. Recordings survive as
regression fixtures, not as the planner.

## 3. Options

### Option A (recommended core): bake a Recast/Detour navmesh per map with DotRecast

**Inputs already exist.** Each map is a triangle soup the server already loads:
a 2 m heightmap (Ishalgen: 1536 x 1536 samples, 4.7 M triangles) plus placed
collision models (Ishalgen: 5,017 placements, 362k triangles, 517 distinct
meshes). 151 maps ship placement files and 89 ship heightmaps. The Obelisk
importer (sibling repo, last present at commit `bd00c3c`) already exports
exactly this soup with placement transforms; that code can be lifted into a
`tools/` baker here.

**Library.** DotRecast 2026.3.1 (zlib, pure C#, targets net10.0, NuGet
`DotRecast.Recast` + `DotRecast.Detour`) is the maintained C# port of
recast4j. It supports tiled builds with a thread count, off-mesh connections,
per-polygon area flags and costs, `FindPath` + `FindStraightPath`
string-pulling, `Raycast`, and navmesh serialization. One `DtNavMeshQuery`
per thread; tile add/remove must be synchronized.

**Parameters** (starting point, to be tuned against the legs above): agent
radius 0.5 m and height 2 m (player collision), max climb 0.5 to 0.9 m, max
slope 45 degrees (the server's sloping-surface rule in `CollisionResults`),
cell size 0.25 to 0.5 m, cell height 0.2 m, tiles of 32 to 64 m. TrinityCore
bakes all WoW maps in about 7 minutes on 32 threads at 0.27 yd cells; one
3072 m Aion map should bake in seconds to low minutes. Detour tiles cap at
65,535 vertices, so keep tiles small.

**Output.** One serialized navmesh per map, generated by a tool and checked in
like the pattern tables (regenerable, never hand-edited), for example
`game-server/data/nav/220010000.navmesh`. Ishalgen and Poeta first.

**Integration.** Implement `INaturalNavigationDriver.FindRouteAsync` as: nearest
polygon for start and goal, `FindPath`, `FindStraightPath`, densify to the
existing 2 m sample spacing, then run the existing `TraceEdge` validation and
hazard checks over the result. The "never emit an unchecked edge" invariant is
preserved and costs one ray cast per 2 m of path instead of per explored
cell. Keep the current grid search as a fallback for the last few metres and
for gaps the navmesh cannot express. Query time is expected in the
millisecond range; the journey code's segment, replan and stall logic does not
change.

**Dynamic geometry.** Bake without `DespawnableNode` geometry (doors, town
levels, event objects) and either tag their footprints as door polygons whose
flags flip from observed state, or rely on the `TraceEdge` post-check plus
replanning, which already handles state changes. Instances share a map's
static geometry, so one navmesh per map serves every instance.

**Risks.** The bake must use the same triangle set and collision intentions
that `DEFAULT_COLLISIONS` and `IgnoreProperties.Of(race)` use, or the navmesh
will disagree with the server about walls; race-specific ignore rules may need
a per-race bake or per-polygon flags. Water, flight and gliding are out of
scope for a ground mesh. Effort: roughly one to two weeks for exporter, bake
tool, loader, adapter, overlay renderer and tests.

### Option B: bake a walkability raster from the server's own ground queries

Sample every 0.5 or 1 m cell once, offline, with the same `GetZ` and collision
rules `TraceEdge` uses; store height plus N/S/E/W walkability, with several
layers per cell for bridges and caves; search with jump-point search or
hierarchical A* (HPA*) and smooth with Theta* or line-of-sight pulling.

Pros: walkability is by construction identical to what the current
`TraceEdge` accepts, so there is no semantic mismatch; it renders directly as
a PNG for review; overrides are a pixel edit. Cons: a 3072 m map is 9.4 M
cells at 1 m (about 28 MB with heights) or four times that at 0.5 m; the bake
is one ray cast per cell, roughly 8 minutes per map per core at 1 m; paths
are jagged; every search, smoothing and link feature is custom code that
DotRecast already provides. Best use: as the cross-check that a navmesh
agrees with the server, or as the planner if DotRecast integration stalls.

### Option C: decode the client's retail ground grid (`Levels/<level>/<level>-path.dat`)

The client ships a binary per level: `DF1-path.dat` for Ishalgen is 2.2 MB,
45 levels have files over 1.5 MB, the Abyss file is 51 MB. Findings from this
session (all on Ishalgen):

- 132-byte header: a 16-byte hash, two 16-bit values (5, 6), zeros, and a
  32-bit value 680,637 whose meaning is unknown.
- The body is a stream of variable-length cell records. Each carries a
  centimetre height, then two 16-bit cell coordinates, then one or more
  flag/link bytes (values such as 0x8206, 0x8242, 0x8306 recur).
- With cell coordinates read as half-metres (world x = cx / 2, y = cy / 2),
  94.2% of 3,000 parsed records land within 3 m of this port's own terrain
  heightmap; every other axis mapping tried scores under 6%. So this is a
  0.5 m ground grid in the emulator's coordinate frame, most likely NCSoft's
  own walkability data.
- Not decoded: the exact record layout (the naive parse recovers only about
  9,700 records, the file must hold far more), the flag semantics, and any
  layer or link structure. No community documentation was found; the geobuilder
  and monono2 tools do not read it.

Payoff if decoded: retail-authored walkability for every level at 0.5 m, with
no bake, and a ground truth to validate option A or B against. Cost: a
reverse-engineering task of uncertain length (budget two to three days before
deciding). Even decoded, the server's collision engine still decides where
the bot can actually walk, so the runtime check stays.

### Option D (needed with any of A to C): a travel graph, i.e. roads with branches

Long journeys and policy belong in a coarse graph over the mesh:

- **Nodes**: bind points, quest and vendor NPC clusters, gather clusters,
  portal locations, flight-path ends, teleporters, and the zone's road
  junctions. All of these come from shipped static data the graph factory
  already reads.
- **Road centerlines**: the portal (`aion-portal`) already holds calibrated
  minimap images for 168 maps with a pixel-to-world transform. Skeletonizing
  the road-coloured pixels (Gaussian smooth, open/close, `skimage.skeletonize`,
  `sknw` to a graph, prune spurs) produces polylines automatically instead of
  the hand-transcribed `NaturalIshalgenRoads`. Every polyline segment is then
  validated against the navmesh before it becomes an edge; map art is a hint,
  never walkability.
- **Links**: generated the Playerbots way, by running the local planner between
  candidate node pairs and keeping only links that succeed, then pruning
  redundant ones. Special edges for portals, flight paths, quest teleports and
  the Return skill.
- **Costs and bans**: each edge carries length, an aggro-density and mob-level
  band computed from spawn data, and optional flags (road, guarded corridor,
  PvP area). A level-3 bot routes around the level-8 camp; a bot with a
  level-99 gate never enters. This is the "how not to navigate" part.
- **Storage**: one checked-in JSON or XML per map, regenerated by tool, plus a
  PNG overlay on the minimap for review.

### Option E: recorded paths

No memory reader is needed. The server already receives the human player's
positions in `CM_MOVE`; an admin command in the style of `FixPath` could
record a walk to a route file. The bot also already logs its own walked
checkpoints (`segment-progress` events), which today drive ingress retracing.
Shipped patrol routes (49 in Ishalgen, 1,212 spawn spots) are free recordings.
Use all of these as regression fixtures and as extra travel-graph nodes, not
as the planner: recorded routes cannot detour, rot when spawns move, and, as
the journey log shows, get blocked by respawned packs.

## 4. Recommendation

Adopt A + D, keep the current search as the fallback, and treat C as a
bounded side quest.

1. **Phase 1, Ishalgen and Poeta navmesh** (one to two weeks). Port the Obelisk
   exporter into `tools/`, add a DotRecast bake tool with a regen check like
   `regen_check.py`, write the loader and `FindRouteAsync` adapter with the
   existing `TraceEdge` post-check, render a navmesh overlay on the portal
   minimap, and make the legs above (plus the 41-quest NPC pairs) an
   env-gated measurement test. Acceptance: every leg routes, and queries are
   milliseconds.
2. **Phase 2, travel graph** (one week). Hub nodes from static data,
   skeletonized road centerlines, generated and validated links, hazard-band
   costs, portal and Return edges, JSON per map, overlay for review. Replace
   `NaturalIshalgenRoads` with the generated data.
3. **Phase 3, all maps.** Bake everything the `.geo` set covers, add door and
   dynamic-object flags, and decide race-specific bakes from real mismatches.
4. **Side quest (two to three days, stop if stuck).** Decode `path.dat` far
   enough to render it as a walkability PNG. If it works, it becomes the
   ground truth the bake is validated against, and possibly the source itself.

Constraints that stay: the bot still obeys observed hostiles and per-segment
collision checks; generated navigation data is never hand-edited; nothing
from this work feeds server NPC movement, which must keep matching Java.

## 5. How the numbers were measured

A temporary xunit probe in `tests/Aion.GameServer.Tests` (removed after the
session) loaded `BotNavigationAssets` from the repository root with the
`run/soak-navigation-test-cache` cache, took `StarterRoute(Race.ASMODIANS, 1)`,
read exact spawn spots for NPC ids 203516 (Ulgorn), 203519 (Nobekk), 203534
(Dabi), 203539 (Derot), 203540 (Mijou), 203552 (Nalto) and 203550 (Munin) from
`game-server/data/static_data/spawns/Npcs/220010000_Ishalgen.xml`, and timed
`BotNavigationGraph.FindPath`, `FindJourneyPath`, `FindJourneyPathAvoiding`
with no hazards, `FindRoadPreferredJourneyPath` and `FindInteractionPath`
with a `Stopwatch`. Triangle counts come from the Obelisk `reference/ishalgen`
export; the `path.dat` analysis used the same export's `terrain.220010000.r16`.

## Sources

- DotRecast: https://github.com/ikpil/DotRecast and releases 2026.3.1
- TrinityCore mmaps generator: https://github.com/TrinityCore/TrinityCore/tree/master/src/tools/mmaps_generator
- Playerbots travel nodes: https://github.com/celguar/mangosbot-bots/blob/master/playerbot/TravelNode.h
- EQEmu maps and navmesh editing: https://docs.eqemu.dev/server/maps/editing-maps/
- L2J-Mobius cell pathfinding: https://raw.githubusercontent.com/andridgitalbox/l2j-mobius/master/java/com/l2jserver/gameserver/pathfinding/cellnodes/CellPathFinding.java
- AionEncomBase (Aion 5.8 Java emulator with recast4j navmeshes): https://github.com/MATTYOneInc/AionEncomBase_Java7
- HPA*: http://webdocs.cs.ualberta.ca/~mmueller/ps/2004/hpastar.pdf
- JPS: https://ojs.aaai.org/index.php/AAAI/article/view/7994
- Theta*: https://jair.org/index.php/jair/article/view/10676
- Funnel algorithm: http://digestingduck.blogspot.com/2010/03/simple-stupid-funnel-algorithm.html
- Road skeletonization (CRESI): https://arxiv.org/pdf/1908.09715 and https://github.com/Image-Py/sknw
- OpenKore field and portal graph: https://openkore.com/wiki/Field_file_format and https://openkore.com/wiki/portals.txt
- Honorbuddy navmesh wrapper: https://github.com/Likon69/Navigation-C-
