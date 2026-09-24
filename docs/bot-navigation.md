# Bot navigation: navmesh, roads and travel graph

How the playtest bots plan ground routes on any map. This implements the "A plus D"
recommendation of [bot-navigation-options.md](bot-navigation-options.md): a baked
Recast/Detour navmesh per map, mapped roads, and a travel graph ("roads with branches")
on top, with the client's own walkability grid as ground truth.

This is bot and test infrastructure only. Server NPC movement is untouched and keeps
matching Java, which has no pathfinding.

## What a bot uses, in order

1. **Travel graph** (`<mapId>.graph.json`), optional, for long level-aware journeys. Hubs
   (quest and service NPCs, bind points, portals, gather clusters, road junctions) are joined
   by walk links that each passed the checked router. Every link lists the aggressive spawns
   it crosses. `BotTravelPlanner` runs Dijkstra with a level-dependent danger cost, then plans
   each leg on the navmesh.
2. **Navmesh** (`<mapId>.navmesh`), which picks the corridor. It is baked from the same
   heightmap and collision meshes the server ray-casts, using the server's own rules. Mapped
   roads are a cheaper area type there.
3. **Server geometry check.** Every emitted step still passes `BotNavigationGeometry.TraceEdge`
   (2 m samples, ground within ±2 m, 45° rule, 1 m collision ray). This is the same "never emit an
   unchecked edge" rule the grid search had. A rejected step is sidestepped or repaired with a
   short grid search. Otherwise the route fails and is never emitted unchecked.
4. **Observed hostiles** stay hard rules. When a checked route breaks the
   `BotNavigationGeometry.AvoidsHazards` rule, the offending window is re-planned on a 1 m grid
   over the navmesh surface with the circles forbidden, then re-checked. Detour's polygon costs
   alone cannot bend a route around a circle that sits inside one large polygon.

## Switch-over: nothing to change in bot code

`BotNavigationGeometry.FindLocalPath`, `FindJourneyPath`, `FindJourneyPathAvoiding`,
`FindRangedApproachPath`, `FindInteractionPath` and `FindRoadPreferredJourneyPath` now try the
navmesh first whenever one is checked in for the map. This covers SIM (`ForServerWorld`), LIVE
(`BotNavigationAssets`) and the soak runners.

- A navmesh answer is final for requests longer than `GridFallbackDistance` (60 m). Shorter
  failed requests still get the old bounded grid search. This covers NPCs standing inside
  collision, doors and small gaps.
- The old searches remain available as `GridLocalPath`, `GridJourneyPath`,
  `GridJourneyPathAvoiding`, `GridRangedApproachPath`, `GridInteractionPath` and
  `GridRoadPreferredJourneyPath`.
- Set `AION_BOT_NAVMESH=0` to disable the navmesh for a run. Set
  `AION_BOT_NAVMESH_DIR=<dir>` to use navmeshes from another folder.
- `BotNavigationGeometry.WithNavMesh(set)` overrides the default for one geometry instance.
  Tests use it.
- `BotNavMeshRouter.LastOutcome` says why the last request ended: `Routed`, `NoNavMesh`,
  `EndpointOffMesh`, `NotConnected`, `GeometryRejected`, `HazardRejected` or `NoApproachPoint`.
  It can go straight into a bot trace.

## Files

| Path | Contents | Made by |
|---|---|---|
| `game-server/data/nav/<mapId>.navmesh` | gzip of DotRecast's mesh-set format | `tools/Aion.NavBake bake` |
| `game-server/data/nav/<mapId>.navmesh.json` | manifest: settings, input and output hashes, polygon counts by area | same |
| `game-server/data/nav/<mapId>.graph.json` | travel graph: nodes, verified links with danger, teleport exits | `tools/Aion.NavBake graph` |
| `game-server/data/nav/roads/<mapId>.roads.json` | road centrelines in game X/Y | `tools/nav/extract_roads.py` |
| `game-server/data/nav/masks/<mapId>.mask.json` | client playable-area sectors (16 m), all 153 client levels | `tools/nav/extract_walk_masks.py` |
| `run/nav/` (git-ignored) | navmeshes for every other map, found automatically after the checked-in folder | `tools/Aion.NavBake bake --maps all --out run/nav` |

All of these are generated. Never hand-edit them. Change the inputs or settings and regenerate.

## Regenerating

```powershell
# 1. Playable-area masks from the client's <level>-path.dat (all maps, about 45 s)
python tools/nav/extract_walk_masks.py --client "C:/Program Files (x86)/Beyond Aion"
# 2. Roads from the client map art cached by the aion-portal spawn editor (needs numpy, scikit-image, pillow)
python tools/nav/extract_roads.py --map-id 220010000 --image ../aion-portal/assets/maps/220010000-ishalgen-map.webp --manifest ../aion-portal/assets/maps/manifest.json
# 3. Navmeshes (starter maps by default; --maps all|baked|<id,id>)
dotnet run --project tools/Aion.NavBake -- bake --maps starter
# 4. Travel graphs
dotnet run --project tools/Aion.NavBake -- graph --maps starter
# 5. Regeneration check: fails when a checked-in navmesh no longer matches its inputs or settings
dotnet run --project tools/Aion.NavBake -- check --maps baked
```

Review tools:

```powershell
dotnet run --project tools/Aion.NavBake -- render   --maps 220010000 --out run/nav-render   # PNG: islands, roads, water, graph
dotnet run --project tools/Aion.NavBake -- measure  --maps 220010000                        # every hub pair through the router
dotnet run --project tools/Aion.NavBake -- hazards  --maps 220010000 --legacy               # journey legs with monster circles, vs the grid
dotnet run --project tools/Aion.NavBake -- validate --maps 220010000 --bin run/df1.bin --out run/df1-validate.txt
```

`validate` needs the client grid exported by
`python tools/client-extract/decode_path_dat.py "<client>/Levels/df1/DF1-path.dat" --terrain <r16> --bin run/df1.bin`
(see [client-path-dat-format.md](client-path-dat-format.md)). It writes a report and a diff
image. Green means both agree, red means walkable for the client but not on the navmesh, blue
means navmesh where the client has no ground.

## Bake rules and why

| Setting | Value | Reason |
|---|---|---|
| Cell size, height | 0.3 m, 0.2 m | resolves 1 m gaps; 1 s per starter map |
| Agent radius, height | 0.5 m, 1.6 m | keeps corners off walls; the server's own check is a 1 m ray |
| Max climb | 0.8 m | the collision ray at +1 m clears steps below it |
| Max slope | 45° | Java `CollisionResults` sloping-surface rule; the terrain uses its 2 m rule |
| Flag merge | 1 voxel | the server takes only the top surface and rejects it outright if it is steep |
| Low-hanging obstacle filter | off | Recast would make steep faces within climb height walkable; the server does not |
| Ground surfaces | `PHYSICAL` meshes only | `GeoMap.getZ` uses `PHYSICAL`; `DEFAULT_COLLISIONS` meshes only block |
| Despawnables | houses; placeables with a spawned static id; level-1 town objects | their default server state; doors are marked as door areas instead |
| Water | ground more than 1.5 m below the map's water level | swim area, excluded unless `AllowSwimming` |
| Roads | 3 m half-width quads along the extracted centrelines | road area. Ground costs 1.25× by default and 1.5× on road-preferred journeys |
| Mask | tiles within 16 m of a client playable sector | out-of-bounds terrain costs no space; Ishalgen went from 12.6 MB to 2.8 MB |

Changing any of these changes `BotNavMeshSettings.Fingerprint()`. Changing the extraction or
bake logic needs a `BakerVersion` bump. Either one makes `check` report the navmesh as stale.

## Measured on Ishalgen (2026-09-24)

| Measure | Result |
|---|---|
| Bake | about 1 s, 42,631 polygons, 2.8 MB |
| Client walkable area on the navmesh | 99.0% within 1 m (92.5% exact) |
| Hub-pair connectivity (3,916 pairs) | 3,718 connected; the rest all involve 4 objects on raised spots |
| Hub pairs through the full router, every step checked (pre-mask bake) | 3,785 / 3,916 routed; median 342 ms, p95 962 ms |
| All maps (155 with geometry) | 4.5 min, 314 MB: only the starter maps are checked in, the rest go to `run/nav` locally |
| Journey legs with every nearby monster as an observed circle (25 legs) | navmesh 20/25, mean 36 ms, max 0.3 s; grid 21/25, mean 3.8 s, max 31 s |
| Legs the old tests timed (Ulgorn to Mijou, 802 m) | 772 points in 33 s on the grid; tens of ms on the navmesh |

The travel graph has 335 nodes and 1,468 verified links, with 332 nodes on the main network.
It builds in about 10 s.

## Docker SIM evidence (natural Ishalgen Priest, 2026-09-24)

| Run | Navmesh | Grid (`AION_BOT_NAVMESH=0`) |
|---|---|---|
| Q2004 checkpoint (the last proven green point), seed 1 | passed in 42 s | passed in 4 min 49 s |
| Q2006 checkpoint, seed 1 | passed in 4 min 16 s (including a natural Return-skill escape) | not rerun |
| Q2006 checkpoint, seed 2 | failed once (no route through a pack near Nalto), passed on rerun in 1 min 12 s | not rerun |
| Q2007 checkpoint, seed 1 | failed in Q2005 (retreat could not outrun three attackers) or at Q2006 before the spacing fix | failed at Q2007 Rae (dead end at 620,2439 after a death) |

Runs vary from one run to the next through combat outcomes, so single failures are not
regressions by themselves. Q2007 has never passed on either planner. Two problems surfaced
and were fixed along the way.

- **Unbounded retry.** The Q2005 Stalker search could rotate through its four search areas
  forever when every route was closed by observed packs. Grid failures used to burn wall time
  until the test's bound; navmesh failures are instant, so the loop spun. It now leaves the
  pocket after one failed rotation, the same way the journey already leaves trapped pockets
  (a checked walk through observed guards, else the learned Return skill). It is also capped
  at 120 attempts.
- **Point spacing.** Combat moves a fixed number of route points per turn. Routes must keep the
  grid search's roughly 2 m spacing, and tests pin that.

## Known limits

- **Doors and dynamic objects** are baked open. Door footprints become door areas, and
  `AllowDoors = false` excludes them. The per-step collision check rejects a closed door at run
  time, so the bot re-plans.
- **Flight, gliding, jumping and swimming routes** are not planned. Water is marked but excluded
  by default.
- **Soft danger** (`BotNavQuery.Danger`) is best effort. Hard observed hazards
  (`BotNavQuery.Hazards`) are enforced.
- **Objects on their own scrap of mesh** (a crate top, the Abyss Gate platform) route by
  interaction search (within 3 m). An exact route to them can report `NotConnected`.
- **Teleports** are recorded as graph exits (`BotTravelExit`), but the planner walks. Using an
  exit is a dialog action the journey code has to take.
- The **road art** also marks a few painted rocks on Poeta. These are cost hints only and change
  no walkability.
