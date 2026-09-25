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
- **Travel planner in the journeys.** The natural Ishalgen drivers ask `BotTravelPlanner.PlanJourney`
  first for every non-combat leg of 100 m or more: the SIM driver
  (`SimulationNaturalIshalgenJourneyTests`) and the LIVE driver (natural decision, gathering and
  inventory scenarios through `LiveBotSession.TravelPlanner`). It passes the bot's level and the
  observed hostiles. The plan is used when it satisfies the hostiles as hard rules. Otherwise the
  driver's ordinary search chain runs as before. Each attempt is traced as `travel-plan`, with its
  waypoints and the static danger crossed, or as `travel-plan-unavailable`.
  `AION_BOT_TRAVEL_PLANNER=0` turns the planner off without touching the navmesh.
- **Fight your way in.** Monsters on the way are not a wall. When observed aggro circles close every
  hostile-free route to an objective, `BotNavigationGeometry.FindFightThroughPath` plans the route
  that fights least. Circles become costs, so avoidable monsters are skirted and unavoidable ones
  crossed. `NaturalFightThrough.SelectNext` names the first monster that route enters. The route's
  samples before that entry are outside every circle and end within spell range. The journey
  (`TryFightThroughAsync`) walks that prefix, pulls that one monster with ordinary combat, rests,
  observes again and re-plans, up to eight pulls per spawn hint. Every guarded approach and every
  blocked spawn approach tries this before the older blocker heuristics. Each attempt is traced as
  `fight-through-plan`, `fight-through-cleared` or `fight-through-pull-blocked`.
- `BotNavMeshRouter.LastOutcome` says why the last request ended: `Routed`, `NoNavMesh`,
  `EndpointOffMesh`, `NotConnected`, `GeometryRejected`, `HazardRejected` or `NoApproachPoint`.
  It can go straight into a bot trace.

## Pulling and retreating like a player

**Chain aggro, from the Java spec.** `AggroEventHandler.onCreatureNeedsSupport` decides who joins a
fight. When a monster is hit, or aggroes and broadcasts, every NPC whose tribe can support it
(`TribeRelationsData.canSupport`) and is not already fighting joins. It joins when it is within its
own aggro range + 2 m of the monster or of the attacker, and can see it. `NaturalPullPlanner` uses
exactly that rule:

- **Target choice.** Among the first monsters the route meets, it prefers the one with no
  supporters within their assist range. A lone monster further along the path beats the first one
  of a pair.
- **Firing spot.** The spot is within spell range (22 m) and in line of sight. It is outside every
  monster's aggro circle and every supporter's assist range, so no one joins. It sits on the side
  with the most clearance, which is the "left or right side of the path".
- **Waiting.** When helpers would still come, the bot waits up to three times, 3 s each, for
  patrols to move. After that it takes the pull with the fewest helpers.
- **Reachability.** The bot walks to the spot on a checked, hostile-free navmesh route on its own
  navmesh island, so it never ends up on a rock top.

The journey uses the planner in three places: fighting through blockers (`MoveToPullSpotAsync`), the
Q2005 Stalker hunt, and the quest-kill standoff. Each decision is traced as `pull-plan`, with the
candidates, the target, the firing spot, the expected helpers and the clearance.

**Passing by.** Routes that only pass observed monsters keep an extra 4 m where there is room
(`BotNavQuery.HazardClearance`, `BotNavigationGeometry.PassingClearance`), so the bot takes the far
side of the road from a pack.

**Retreat.** Java `AttackManager.checkGiveupDistance` sets the escape rule. With Ishalgen's defaults,
a chaser gives up beyond 50 m from its target, beyond 200 m from home, or beyond 100 m from home
after 10 s without being hit. An equal-speed chaser cannot be outrun, so `RetreatFromPackAsync`
drags the pack away from home:

- **Candidates.** Walked ground, the refuge, and navmesh rings at 45–150 m. Only ground on the
  bot's own navmesh island counts: a ring point that snaps onto a rock top can never be reached.
- **Direction.** Candidates must lie ahead, away from the attackers' centre. None may be inside
  another observed circle.
- **Ranking.** Candidates are ranked by distance from the attackers' homes (first-seen
  `SM_NPC_INFO` positions).
- **First steps.** Routes avoid every other observed monster but not the chasers, whose circles
  move with the bot. The first steps may swing sideways around a rock or another monster, but a
  route whose point about 10 m along turns back into the pack is rejected.
- **Cornered.** When no candidate passes, or the chasers are still close after eight replans, the
  retreat reports `combat-retreat-cornered` and the Priest fights it out. The combat policy then
  never picks retreat again in that fight (`NaturalCombatObservation.Cornered`). It heals, uses
  potions and keeps attacking, as a player with no way out would. A death there is an ordinary
  death and revive, not a failed run.

The destination is kept until the bot reaches it. The old policy re-measured clearance from chasers
that moved with the bot, and alternated between checkpoints back toward them.

**No fight-length limits.** A fight lasts as long as it needs to. Chain aggro and respawns can keep
monsters coming, and a cornered Priest alternates heals and damage for minutes. The only bound is
1,000 combat actions (`MaximumCombatActions`), about an hour of game time, which stops a genuine
stall from hanging the run. The same applies to defending before a pull, which keeps killing
attackers while any remain. It also applies to resting. A rest interrupted by an attack fights the
attackers nearest first instead of failing, and only quiet rest intervals count toward the
recovery budget.

**Open routes after a fight.** When the fight-through plan finds a route to the objective that no
observed monster blocks, for example after a retreat dragged the pack away, the caller walks again
instead of giving up.

**Walkers.** A walking NPC's `SM_MOVE` carries its walk target after the start position (Java
`SM_MOVE.writeImpl`: `POSITION|MANUAL|ABSOLUTE`), and a real client animates the NPC toward it. The bot
decoder used to drop those floats, so a walker looked frozen where its walk began. It now keeps the
target (`BotKnownObject.MoveTarget`, `SettledPosition`). When the server answers
`STR_SKILL_NOT_ENOUGH_DISTANCE` or `STR_SKILL_OBSTACLE` although the last-known position looks fine, the
Priest closes in, up to 8 m and never nearer than 10 m, toward where the walker settled
(`combat-range-close-in`), or finds a firing spot with sight of it. That was the long-standing
"no checked guarded approach to Nalto" failure: a patrolling Mau stood 25 m past where the bot thought.

**Chasing a walker.** When the pull's target has walked on by the time the bot reaches its firing
spot, the bot plans the pull again against the target's new position, up to three times
(`pull-target-moved`), instead of dropping it.

**Learning from deaths.** The Priest records where it died (`NaturalSimulationCombat.DeathSpots`). Pull
planning prefers firing spots more than 12 m from one, as a player avoids the spot where hidden or
respawning monsters killed them. It is a preference, not a wall: when the only way in passes there, as
at Rae's camp, the Priest goes anyway. The camp near Rae (about 641, 961) is the usual example: monsters that
were never visible joined "clean" pulls there.

**Resting away from respawns.** Before sitting down, the Priest moves to the nearest ground that nothing
respawning or already visible can aggro from. That means outside every shipped hostile spawn point's aggro
range and every observed monster's, plus 5 m (Java's 2 m assist offset and some slack), within 150 m
(`NaturalSimulationCombat.RestMargin`, traced as `rest-relocate`). Candidates are recently walked ground
and navmesh rings on the bot's own island, nearest first. The walk there avoids observed monsters, and
other spawn circles when it can. Resting inside a spawn circle means every respawn interrupts the rest,
a rest, fight, rest loop that ends in death. The Stalkers at Rae's camp respawn every 180 s, 12–15 m
from where the Priest used to rest.

**Learning from a human run (2026-09-24).** A recorded session (`docs/session-recording.md`) of a player
driving the same Priest from Nalto finished Q2007 and Q2129 with no deaths. Three habits the bot lacked
now drive it:

- **Wear upgrades.** After every full rest the Priest equips, per slot, the highest-level bag item its
  class, race and level may wear (`NaturalGearPolicy`, traced as `gear-equip`). The human put on four
  unused quest rewards (a mace, a ring, shoes, leggings), which raised max HP from 594 to 669.
- **Keep the blessing up.** Blessing of Guardianship is recast between fights whenever the client's own
  effect list lacks it (`MaintainBuffsAsync`, `buff-blessing`). The bot had never cast it.
- **Heal through a double pull.** At 30% HP or below, the Priest retreats only with three or more attackers,
  or when no heal or potion is available (`NaturalPriestCombatPolicy.SwarmedAttackers`). The human chained
  Healing Light down to 24% HP against two monsters and won; the bot used to run into the next camp.

**The Priest is a melee class (2026-09-25).** Two findings from the human recording changed the combat
core. First, the bot had never cast Hallowed Strike (45 casts in the human session, 0 in every bot run):
its 1 m template range never passed against the client's lagging distance, and the rotation order put it
last. Second, the human opened from range (median 11.5 m) and then fought at melee (median 2.3 m for every
skill), with instants first. `NaturalPriestCombatPolicy` now plays it that way: Smite opens and is the
filler while the monster closes; the bot holds position instead of walking into melee (walking pulled it
into the neighbours' circles); once the monster is adjacent, by client distance or because it hit the bot
in the last 3 s, the order is Infernal Blaze (instant, stun), Hallowed Strike (instant, 30% attack-speed
slow, every 8 s), Smite, then the mace. Heals at 70% against one attacker and 55% against two or more, the
potion at 80%, and below 35% sustain only until 45%. After a cast the bot waits for the animation or a
0.7 s reaction, not a fixed 2 s: the human cast every 2.4 s, the bot every 3.5 s, and against two Stalkers
that gap is the difference between 78 HP/s of healing and a slow loss.

**The bot could not see the Stalkers.** The two Gray Mane Stalker templates in the Q2007 camp (210750,
211284) carry no `type` attribute, so `GetNpcTemplateType()` is NONE for them, and every "is it a monster?"
test in the journey used that attribute: a Stalker was in the final fight of all 84 deaths across four
runs while every one of the 128 Q2007 pull plans reported a clean pull. `NaturalHostility.IsAggressive`
now mirrors the server's own aggro test (tribe aggressive to the player's tribe, not friendly, aggro range
above 0) and is used at all 14 sites. Patrol routes count too: every step of a walker's route is a spawn
circle for rest and route decisions.

**Clearing around an objective.** Before a use bar that one hit interrupts (the Q2007 generators),
the Priest pulls, one at a time, every observed monster whose aggro circle plus 2 m comes within 20 m of
the object (`objective-clear`). It does this from range around the generator's shipped spot before
stepping onto it, as the human did (pull the Stalker beside the blue generator from 20 m, then use it), and
again on arrival for anything newly visible. A death on the way to a generator rejoins
through Nalto and Rae from bind, the same recovery as the Rae leg.

**Stuns.** A cast refused with `STR_SKILL_CAN_NOT_ATTACK_WHILE_IN_ABNORMAL_STATE` (Java
`PlayerRestrictions.canUseSkill`: stunned, knocked down) is a start rejection, and a potion refused with
`STR_SKILL_CAN_NOT_USE_ITEM_WHILE_IN_ABNORMAL_STATE` (`canUseItem`) stays in the bag. Either way the
Priest waits a second and re-evaluates instead of failing.

**Travel caps are stall guards, not distance limits.** The navigator moves in 8-point segments. Its
cap is 1,000 segments, about 16 km, so a cross-map walk back to a quest giver never runs out. Following
an NPC that walks around town is ordinary travel and does not use the hazard replan budget. That budget
(three) counts replans in a row without progress, so a long walk that meets new monsters along the way
keeps going. A target
that drops out of view at the edge of visibility range is not a failure either. The bot walks on to
the shipped spawn hint and reacquires it, and reports it missing only when it stands there and still
sees nothing.

**Stepping off islands.** If the bot stands on a scrap of mesh the bake split off (the server
geometry can allow it), `BotNavMeshRouter` takes a short checked step to nearby ground on the
destination's island first.

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

**After a spawn placement pass** (retail-accuracy edits to `spawns/Npcs`, `Gather` or `Statics`
on a starter map): run the height audit above — a heightmap-derived placement puts anything under
an overhang or in a cave on the ground above it (nineteen Ishalgen spawns around the Black Opal
cave were 30–57 m up after the 5.8 import) — then regenerate the Java geo golden, which hashes
those files and would otherwise fail `GoldenGeoFixtureTests`. Bot tests pin what a scenario needs
(for example at least 20 basket spots for the soak), never a raw spot count.
The portal's importer (`aion-portal/scripts/import_58_ishalgen.py`) now asks Aion.NavBake for the
collision surfaces under every fixed retail position and takes the one nearest the retail height
(within 6 m), so cave floors, ship decks and platforms come out right on the next pass; territory
spawns, whose retail height is only an area anchor, still use the heightmap. Replayed on the
pre-import XML it reproduces the nineteen cave floors and additionally lifts nine spots onto
their retail deck or platform (the Eyvindr peon under the Anturoon deck, the dundun highsitters,
Munin, the Methu egg); those nine heights are applied, with the travel graph and geo golden
regenerated.

The batches on the lifted heights surfaced three last journey gaps, none about placement: the
Return fallback cast within the client's own cast interval after a Smite (it now waits the gate
out), the fight-through advance only closed on a blocker within 40 m (it now walks the checked
route from any distance and re-plans the pull within 30 m, which is what reaches Hatata's cave),
and a death mid-kill or a target that reset out of view ended the step (kill steps now walk back
and try again, up to six times). A fight-through walk that ends at the objective itself is
reported as arrival. With those, four of four seeds completed the journey with no deaths at all.

## Regenerating

```powershell
# 1. Playable-area masks from the client's <level>-path.dat (all maps, about 45 s)
python tools/nav/extract_walk_masks.py --client "C:/Program Files (x86)/Beyond Aion"
# 2. Roads from the client map art cached by the aion-portal spawn editor (needs numpy, scikit-image, pillow)
python tools/nav/extract_roads.py --map-id 220010000 --image ../aion-portal/assets/maps/220010000-ishalgen-map.webp --manifest ../aion-portal/assets/maps/manifest.json
python tools/nav/audit_spawn_heights.py --map-id 220010000 --base <rev before the spawn change>   # floating spots and floors beneath
pwsh -NoProfile -File scripts/parity/regen-geo-golden.ps1                                          # after any starter spawn/walker edit
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
| Full journey, seed 1 | completed Q2000–Q2006, Q2100–Q2104 and Q2132, then stopped in Q2007: 11 observed aggro circles in the Mau camp closed every checked approach to Nalto (the journey doc's open tactical limit) | not rerun |
| Q2007 checkpoint, seed 1 | failed in Q2005 (retreat could not outrun three attackers) or at Q2006 before the spacing fix | failed at Q2007 Rae (dead end at 620,2439 after a death) |

With the travel planner wired in (2026-09-24), the Q2004 checkpoint passes in 33 s, and Q2006
passes on seeds 1 and 2 in about 1.5 min each. The planner served 25–37 long legs per run, about
a third of them through graph waypoints. With the planner on and off (`AION_BOT_TRAVEL_PLANNER`),
full journeys on seeds 3 and 4 both reached Q2007 with 13 quests. Every run stopped on a route that
observed monsters closed.

With fight-through, full journeys on seeds 1, 3 and 4 made 5–7 ordinary pulls each and cleared
their way through the Q2006 Mau farms and to Nalto. On seed 3 the Priest also reached the Rae
leg. They still stop in Q2007, now on combat survival rather than routing. Two monsters engage,
and `RetreatFromPackAsync` alternates between the same walked checkpoints until its five-replan
limit. That retreat policy is the next thing to fix for the journey.

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

## Q2007 after the melee rotation (2026-09-25)

| Batch (4 seeds) | Deaths | Retreats | Q2007 |
|---|---|---|---|
| Before (tribe-blind hostility, ranged rotation, 2 s cast floor) | 84 | 27 | 0 of 4 finished, all hit the 20-revive cap |
| After (see the Stalkers, melee rotation, real cadence, respawn-window rests) | 1 | 0 | 4 of 4 finished; runs stopped later, in side quests |

The remaining stops were ordinary journey gaps, not combat: waiting for a respawn at empty spawn
hints, a grey monster that pays no experience (its 0% status is now kill evidence), the strict router
refusing the last metres to a quest object that the checked fight-through route reaches, and an
unhandled death during a use bar. Each is fixed in the journey code; the next batches measure them.

The batch after that reached two more journey gaps. Three seeds stopped on the way from the Methu
egg to Munin: the egg sits 8.6 m from a Mau spawn, so no clean firing position exists and the pull
planner returned nothing; the fight-through now stands and fights the nearest blocker when there is
no clean pull. The fourth reached Q2134 (Alfrigh's Request), whose Iron Ore comes from Impure Iron
Ore nodes at Essencetapping 15. The gather step was frozen to Q2133's Young Azpha; it now takes the
plan's node template on the current map, and when the skill is below the node's level it first
practises on Young Azpha (91 skill xp a harvest against 76-224 xp a point, about thirty harvests),
which is what a player does before mining. With both in place all four seeds completed the whole
Ishalgen journey (Q2004 through Q2134 and Munin, 205 quest updates, one death in four runs); each
raised Essencetapping from 3 to 16 with 29-30 practice harvests before the ore.

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
- **The planner declines legs blocked by observed hostiles.** The driver's older chain, whose grid
  backstop has a wider search, then decides. Combat approaches and retreats do not use the planner.
- The **road art** also marks a few painted rocks on Poeta. These are cost hints only and change
  no walkability.
