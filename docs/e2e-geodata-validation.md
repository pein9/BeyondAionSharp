# Phase 9 scenario validation

P9-04 validation is complete. This records the distinction between loaded geometry,
checked movement, and a natural-play bot. Java reference: `ce54b7931`.

## Navigation contract

- SIM reuses the booted world's geometry and current per-instance collision state.
- Every proposed edge checks ground at intervals no larger than 2 m and a collision
  ray between samples. Missing ground, abrupt height changes and collisions reject
  the edge, including direct shortcuts and temporary endpoint connectors.
- The graph still starts from shipped spawn/walker/portal/bind coordinates. Where
  those points leave a gap, a bounded local ground search can supply a route. It
  visits at most 8,192 cells, stays within a 20 m margin around the endpoints and
  rejects requests longer than 200 m. Exhaustion means no route, not a teleport
  or an unchecked straight line.
- M1 exercises that route through normal CM_MOVE packets. LIVE M1 loads the same
  checked-in assets in its standalone client process; it does not start another
  server. Its offline navigation is restricted to starter maps and initial geometry
  state. Dynamic door/event/town/shield synchronization is **not implemented**.
- The synthetic navigation regression suite covers walls, temporary connectors,
  detours, sampled heights, interior holes, missing terrain and per-instance
  obstacle changes. `p9-04-nav-d.log`: nine tests passed.

This is a point-sampled ground navigator, not a complete navmesh or a natural
journey planner. It has no jump/flight links, general obstacle interaction,
cross-map routing or combat-aware travel strategy. Other GM-assisted regression
scenarios must not be advertised as autonomous travel.

## Chase correction

C9's former setup attacked an NPC, then teleported the player away before jumping.
Java `TeleportService.sendLoc` despawns the player; `KnownList.clear/del` notifies
the NPC, and `CreatureController.notKnow` removes aggro. The C# behavior is faithful.
`p9-04-full-c` demonstrated zero chase distance and no hate even with real geo.

The revised scenario retreats through collision-checked CM_MOVE instead. It
checks hate immediately before jumping, observes target/aggro through a 100 ms
sampling window and requires actual NPC displacement. Airborne and landing
position packets are now included. The old expected-failure catch is removed,
and the manifest explicitly declares no expected failure.

The first corrected chase, navigation, combat and social setup changes passed
the complete shared SIM process in `p9-04-full-g` (six tests, including the manifest
scenario loop). The final matrix's shared SIM process also passes all 58 scenarios,
including the later explicit airborne/landing assertions. Coverage recovery also
repeated that shared process successfully. Reset and LIVE results are below.

## Displacement controls

`GEO-FEAR` and `GEO-KNOCKBACK` are separate SIM Full processes with ordinary
access-zero duelists. Director setup supplies level 28 and legal equipment;
the subjects cast their learned Fear (3775) and Trunk Shot (2058) through the real
protocol. No direct effect application, forced success or scripted displacement
is used. Resists get a bounded retry after normal cooldown expiry.

Each scenario compares open ground with a vertical mesh face in shipped Ishalgen.
The setup requires clear caster-to-target travel and LOS, and mesh intersections
at both chest and head height so a terrain incline cannot stand in for the wall.
The selected obstruction is
`levels/common/dark/natural/rocks/etc/na_d_rockgnbig_02a_02n.cgf`:
target `(550, 2794, 299.125)`, caster `(547, 2794, 299.125)`, collision 1.420227 m
east of the target. This is a rock wall, not a synthetic test mesh.

- `p9-04-fear-b`: open and wall controls passed, including SM_MOVE observation.
- `p9-04-knock-a`: open and wall controls passed, including SM_FORCED_MOVE's
  serialized target ID and coordinates matching the server's final position.
- Both watch 20 samples at 100 ms intervals and require positive displacement,
  survival, and stopping short of the obstruction. The open knockback must travel
  at least 1.9 m; open fear must exceed 3 m. The open-case upper bound is 20 m.
- Java `FearEffect`, `StaggerEffect`, `PlayableMoveController` and GeoService's
  collision wrappers were read before these checks. No effect implementation
  changes were needed for these passing cases.

## Full regression evidence

`p9-04-full-matrix-a` passed the shared SIM process, quest/calendar reset profiles,
and five exhaustive sweeps: 5,033 skills, 1,304 active trade catalogs (816 unreachable,
170 inactive), 238 teleporter routes (46 unreachable), 89 bind points (40 unreachable),
and all 12,494 recipes. Baselines were not broadened.

It stopped at gatherable 400017 with `STR_GATHER_OBSTACLE_EXIST`: the old sweep
always approached from the west, even when that position was obstructed. Java
`GatherableController.startGathering` and `GeoService.canSee` require this rejection.
The revised sweep searches eight nearby setup positions and other shipped copies
for ordinary LOS, then asserts `GeoService.CanSee` before gathering. It retains
director placement at the node's height (including aerial nodes); this is not
natural travel or a new flight policy. The production gathering rule is unchanged.
Focused rerun `p9-04-gather-b` passed: all 374 reachable templates, 382 unreachable
out of 756 total, with no baseline change. The rest of the matrix resumed under
the original artifact root via `run/p9-04-resume-matrix.ps1`; its log is
`run/p9-04-resume-matrix.log`. These are composite results, not a clean single
invocation of `run-full.ps1`.

Coverage recovery also reproduced the same fixed-side setup defect in SIM Q2134,
at gatherable 400201 in Poeta `(212.52646, 1054.5825, 122.08079)`. A pre-cast LOS
assertion changed the opaque 30-second packet timeout into a direct diagnosis
(`p9-04-q4i-diagnostic`). The quest driver now shares the sweep's visible-approach
helper and asserts LOS before sending the ordinary gather request. The 30-second
budget is unchanged. `sim-reset-q4i-fixed` passes all 27 Ishalgen plans;
`p9-04-q4p-fixed` passes all 25 Poeta plans and `p9-04-gather-c` repeats all 374
reachable gather templates successfully after sharing the helper.

All 41 LIVE invocations (40 scenarios plus canaries), the L0 packet comparison,
and both new SIM displacement processes passed in `p9-04-resume-matrix.log`.
However, final quest aggregation exposed an independent retention bug: each LIVE
child pruned the same matrix root to twenty folders, deleting earlier SIM and
LIVE artifacts before aggregation. Those deleted detailed artifacts are not
recoverable; the outer console logs retain the successful results. This was not
a gameplay or quest-completion failure, and no coverage baseline was relaxed.

Full-run children now skip retention. Standalone runs still retain twenty. The
temporary 40-child test `scripts/live/test-run-retention.ps1` fails before and
passes after this fix. The shared SIM and Q4P/Q4I SIM receipts were regenerated,
then `run/p9-04-recover-coverage.ps1` reran all five LIVE quest receipt sources.
`run/p9-04-recover-coverage-b.log` records their success and the final coverage
result: **58 SIM / 59 LIVE completed quests**, with no baseline regression.
`run/p9-04-full-matrix-a/quest-coverage.json` retains the receipt paths and result;
29 child folders survive, demonstrating the retention correction in the real run.
Do not describe the original invocation as a clean `run-full.ps1` pass: completion
is supported by the original successful scenario logs plus these targeted reruns.

Mandatory checks: 4,100 solution tests passed (21 explicit skips), warning
baseline unchanged at 4,243, logger/clock/custom-quest ratchets, fidelity, the
10-test quest-compiler and 23-test data-sweep Python suites, Docker compose
contract and the retention regression. Docker Fast `p9-04-fast-final` passes 6/6.
Final build/test logs are `p9-04-warning-final-b.log`, `p9-04-solution-final-b.log`
and `p9-04-drafts-final.log` under `run/`. No compiler or data-coverage baseline
was raised, no new log allowance was added, and no new content was enabled.

No new client extraction has been needed so far. The existing starter query
corpus matches Java; that proves parity with those assets, not fidelity to every
client build. A client version and extractor repository will be useful if later
validation finds a specific missing or incorrect asset.
