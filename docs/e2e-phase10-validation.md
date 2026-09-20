# Phase 10 execution and evidence

## P10-01: suite orchestration

`scripts/e2e/run-full.ps1` accepts `-Suite Breadth` (the default), `Soak`, or `All`.
The default preserves the existing on-demand breadth workflow; it is not a claim
that the complete Phase 10 acceptance matrix ran. `-Suite All` selects breadth
followed by 50, 200 and 500 subject bots for 7,200 seconds per population. Shorter
`-SoakSeconds` values and selected `-SoakBots` populations are diagnostic runs,
not acceptance evidence.

`-PlanOnly` prints the selected steps without starting processes or creating run
directories. Until P10-02 supplies the soak driver, selecting actual `Soak` or
`All` execution fails before any run starts. Missing policy is never a green soak.
The driver contract is `scripts/live/run-soak.ps1` with `Run`, `RunRoot`,
`FullRun`, `Bots`, `DurationSeconds`, `Seed`, `PacketTap`, and `SkipImageBuild`.
It must throw on failure and preserve its sibling artifacts.

Breadth selection comes from the shared manifest: currently 72 SIM scenarios and
42 LIVE scenarios, including the previously omitted `connect` smoke scenario.
SIM reset epochs and shard allocation are unchanged. LIVE children each retain
their isolated Docker stack; L0 runs first, then its packet comparison, and canaries
run last. Quest coverage follows all breadth children. Existing scenario deadlines
are preserved; new LIVE entries use a 600-second step deadline until explicitly
specialized. Only the first LIVE/soak child builds images. Every child receives the
requested seed (the previous runner omitted it for LIVE).

The LIVE dispatcher now rejects unimplemented entries and unsupported multi-scenario
selections. Previously they fell through to connection-only smoke tests labeled
with the requested scenario IDs, a false-positive path exposed by this review.
Two focused regressions verify failure before creating a bot or opening a socket.

Runs preserve `suite-plan.json` (git SHA, seed, time zone, selection and ordered
steps) and append `suite-results.jsonl` after each step, including full exceptions
on failure. Execution stops on the first failure; it does not retry SIM or disguise
unexecuted steps as passed. These orchestration records are not the richer
`report.json`/`report.md` deliverables of P10-10.

`scripts/e2e/test-full-suite.ps1` tests selection, isolation at 1/2/100 shards,
deadlines, parity order, automatic inclusion of new manifest entries, the exact
capacity matrix, fail-fast/no-retry behavior, public dry planning and unavailable
soak handling without Docker. It is a required pre-commit check.

Validation: the contract test passes, including execution of the actual runner
dispatch block with recording children to verify seed 73, build-once behavior,
shared artifact roots and enforced LIVE watching. `p10-01-connect` passes against
the isolated Docker stack with seed 73 and no new log allowance. The solution has
4,102 passing tests and 21 explicit skips; the warning baseline remains 4,243.
Logger/clock/custom-quest ratchets, fidelity, quest-compiler/data-sweep Python
tests and retention regression pass. No gameplay code changed. This is not a new
full breadth replay or a soak result.

## P10-02: capacity and policy foundations (in progress)

The LIVE option cap is now 1,000 subjects. `BotIdentity` preserves existing small
run names/MACs, reserves the director's historical MAC, and extends addresses and
alphabetic character suffixes without collisions. Population tests cover every
identity at 50/200/500/1,000, including the old 254/255 and 676 boundaries.

`SoakLifePolicy` allocates same-race pairs to Poeta, Ishalgen and both capitals,
plus opposing-race pairs to Reshanta. Capitals are needed for the shipped crafting
facilities; their inclusion does not claim ordinary travel between these areas.
Each pair has an independent seeded random stream and shuffled action cycles,
with 1–5 second think times. Every allowed activity appears once per cycle rather
than relying on chance to eventually cover it. The combined policy covers quest,
gather, craft, vendor, trade, group, duel, PvP, relog and crash-disconnect. Sources
must be existing, enabled LIVE scenario contracts, not deferred D7 content.

The policy references the breadth protocol contracts, not the one-shot fixtures:
E1 already gathers in both starter zones and E5 crafts in both capitals despite
their single-map manifest setup fields. Social and lifecycle packet flows are
reusable outside their original fixture map. Runtime drivers still need to prove
their repeatable state, timing, legitimate failures, cleanup and observations;
the policy alone proves none of those actions work at scale.

The soak overlay now mirrors every shared observability/chat/channel/geo control.
Its admission limit is 1,001 (up to 1,000 subjects plus the director), because the
unchanged production/Java default of 100 cannot admit the 200/500-player runs.
The existing single I/O dispatcher, production gather/craft failure rates, events
and random quest bonus rewards are untouched. The compose regression checks both
shared controls and the permitted capacity-only difference.

This is preparation, **not P10-02 completion**. The long-lived action runtime,
resource coordination, latency/memory/timer sampling, statistical assertions and
all two-hour population runs remain outstanding. The Full runner must continue
to reject unavailable soak execution until that runtime is implemented.

Foundation evidence: 21 focused identity/policy tests pass. Docker run
`p10-02-connect500` records `500 passed, 0 failed` and 500 separate bot traces,
with enforced log watching and no new allowance. This is a TCP key-exchange/close
smoke workload, **not** 500 authenticated or simultaneously in-world players;
it does not exercise the new admission limit. The test stack was removed after
the run; the maintainer's Docker MySQL container was left alone.
The foundation solution run passes 4,123 tests with 21 explicit skips; compiler
warnings remain 4,243. All required logger/clock/draft ratchets, fidelity, Python
compiler/report tests, retention, Full-suite and compose contract checks pass.

## P10-02 repeatable social/lifecycle runtime (still diagnostic)

The explicit `SOAK` manifest entry is excluded from breadth selection. The LIVE
runner can now create a long-lived population and execute seeded, independently
scheduled pairs. Earlier pairs drain their incoming packets while later pairs
are prepared; the requested duration starts only after the whole population is
ready. A failed pair cancels its peers, and all tasks are joined before disposal.
The director only supplies initial class/level, position, kinah and bandages, then
logs out. Subjects remain ordinary access-zero players. This is fixture setup,
not a natural, cheat-free journey or a claim of travel between zones.

Implemented diagnostic activities:

- Group invitation/acceptance, reciprocal roster checks, leave and cleanup.
- Trade cancellation and commitment on every invocation, item/kinah offers in
  both directions and exact per-subject inventory conservation. Trading one
  bandage each keeps the workload repeatable without replenishment.
- Normal logout/relogin and abrupt client disconnect/relogin, respecting the
  existing delayed logout and reentry gates. Each reconnect must preserve the
  character ID, position, level and inventory on a fresh connection; every
  subject ends with the independent inventory oracle and an offline DB check.

In-memory packet lookback is capped only for this profile; disk packet traces
remain complete and existing breadth lookback behavior is unchanged. Per-pair
decisions are traced, and `soak-runtime.json` records activity counts with
`Acceptance: false`. Every selected eligible activity must actually execute in
each cohort or the diagnostic fails. There is no silent skipping of activities.

For example (the run ID must be unused):

```powershell
pwsh scripts/live/run-live.ps1 -Run soak-diagnostic -Scenario SOAK -Bots 10 `
  -SoakSeconds 180 -SoakActivities Group,Trade,Relog,CrashDisconnect `
  -StepTimeoutSeconds 90 -Seed 73 -FullRun
```

At this early checkpoint default `SOAK` requested **all** activities and rejected the unimplemented
ones before opening bot sessions. The PowerShell wrapper may already have started
its disposable Docker stack at that point, and cleans it up. `run-full.ps1`
continues to reject Soak/All before startup while `run-soak.ps1` is unavailable.
Quest/gather/duel/PvP runtime, shared-resource coordination, statistical
checks, dispatcher/memory/timer telemetry and every two-hour acceptance run remain
outstanding. At this checkpoint system-message history was still unbounded; the
later gathering checkpoint bounds it too, without claiming flat process memory.

First runtime evidence: `p10-02-cycle10-a`, seed 1, 10 authenticated subjects,
180 seconds after population setup, 74 cohort actions. It passed enforced log
watching with no new allowance. This run predates the additional reconnect
level/inventory comparison; its existing final independent inventory oracle
passed. Twelve new option/preflight regressions cover explicit subsets, invalid
values and failure before sockets for unavailable activities or empty cohorts.

The stronger runtime passed `p10-02-cycle50-b`: seed 73, 50 subjects/25 cohorts,
180 seconds after population setup, 368 cohort actions and all 50 completion
traces. Every reconnect checked character identity, position, level and exact
inventory totals. The bot problem file is empty and enforced log watching passed
without any new allowance. Both diagnostic stacks were removed; the maintainer's
Docker MySQL container was untouched. These results are **not** the two-hour
50/200/500 matrix or a memory/timer plateau measurement.

Checkpoint validation: 4,135 solution tests pass with 21 explicit skips; 4,243
compiler warnings (unchanged). Logger/clock/custom-quest ratchets, fidelity,
quest-compiler/data-sweep tests, retention, Full-suite and compose contracts pass.
No production gameplay code or upstream automation changed. Java's
`AionConnection.java:onDisconnect` at `ce54b7931` was checked for the existing
maximum ten-second delayed crash logout; no Java runtime was used.

## P10-02 repeatable economy workload (still diagnostic)

The explicit SOAK subset now also supports vendor buy/sell/repurchase and Cooking
work orders at production failure rates. Cooking starts at skill one through the
ordinary trainer dialog. The catalog selects the highest eligible shipped
apprentice order for the subject's current skill, through the native skill-99
mastery gate; it does not add quests or automatically upgrade mastery. Exhausting
issued materials abandons/reaccepts the order through ordinary client packets,
preserving already-crafted products as Java does. Ingredient stock is bought in
small batches from the existing active cooking merchants, with exact price,
kinah and item assertions; there are no GM ingredient grants.

Each craft checks ingredient consumption on success **and** failure. Work-order
completion checks the race/skill-specific TASK bonus against an independent
static-data oracle, requiring exactly one eligible reward when any group matches.
Useful ingredient rewards remain in inventory; other bonus items are discarded
through normal client packets, never by resetting server state. Faster crafters
continue draining incoming packets while their paired subject finishes. Bandage
fragments left by trades/vendor loops are consolidated through `CM_SPLIT_ITEM`,
with source/destination and total conservation checks.

Fourteen new contract tests cover every apprentice level for both races, reward
race/window/count bounds, and injected missing/duplicate/unrelated reward deltas.
The SIM vendor regression begins with multiple stacks and exercises splitting
and merging through ordinary packets. These tests do not replace LIVE evidence
for higher work orders, ingredient replenishment, or the two-hour mixed workload.

Failed exploratory diagnostics remain useful evidence, not passes:

- `p10-02-economy10-a` found the E3 one-stack assumption (§7 #73); vendor assertions
  now aggregate stacks and pick a sufficient sale stack.
- `p10-02-economy10-b` filled the cube with an oversized draft GM ingredient grant.
  That fixture was removed in favor of ordinary small-batch merchant purchases.
  No inventory-full refusal or bot failure was allowlisted.

The plan's old ~79% craft / ~74% gather completion estimates are not validated
acceptance thresholds. Java `CraftingTask.analyzeInteraction` and
`GatheringTask.analyzeInteraction` at `ce54b7931` advance competing progress bars;
the production failure chance of 33 gives 67% success per step at skill lead zero.
The future statistical assertion must model those bars and stratify by skill
lead; this checkpoint traces craft outcomes/skill lead but makes no statistical
acceptance claim. No production gameplay code or Java runtime is involved.

LIVE `p10-02-economy10-c` passed with seed 73, ten subjects and an eight-minute
window after population setup: 143 cohort actions, twelve completed work orders
across four crafters, 46 craft attempts (36 successes / 10 failures), and three
abandon/reaccept recoveries. All ten subjects completed their final inventory
oracle and offline checks. The bot problem file is empty; enforced log watching
reports zero new/known/regressed fingerprints and only the existing startup
content warning allowance. Its isolated stack was removed. This run predates the
stack-consolidation addition. It reached Cooking 10 but completed only the first
orders, so higher-order selection and merchant replenishment still require LIVE
proof; neither is claimed by this result.

The final stack-consolidation code passed `p10-02-stacks10-a`: ten subjects,
seed 73, three minutes, 78 cohort actions and 32 ordinary stack merges. Its
group/trade/vendor/relog/crash subset completed with an empty bot problem file
and enforced watcher success, without new allowances. The temporary stack was
removed; the maintainer's Docker MySQL container was left running.

Final checkpoint checks: 4,149 solution tests passed / 21 explicit skips; 4,243
compiler warnings (unchanged). Docker Fast `p10-02-economy-fast-final` passed all
eleven scenarios, including the multiple-stack vendor and packet split/merge
regression. Null-logger/clock/custom-quest ratchets, fidelity, ten quest-compiler
tests, 23 data-sweep report tests, retention, Full-suite and compose contracts
all passed. P10-02 remains unchecked; the Full soak driver remains unavailable
until the complete workload and capacity assertions exist.

## P10-02 coordinated gathering (still diagnostic)

The prior economy implementation also passed the longer `p10-02-cooking20-a`
diagnostic (seed 73, ten subjects, twenty-minute window): 345 cohort actions,
24 completed orders, 116 craft attempts (96 successes / 20 failures), four
abandon/reaccept recoveries, and three ordinary ingredient purchases from both
racial merchants. Eight orders were the next apprentice tier (5501/6501), closing
the first-order-only and unexercised-purchase gaps above. This does not prove all
twenty order templates or the two-hour workload. All subjects passed final inventory
and offline checks; the problem file was empty and enforced log watching passed
with only the existing startup content allowance. Its isolated stack was removed.

The starter gathering workload uses the existing Young Aria/Azpha spawns and
production randomness. Starter subjects stay level-9 Mages so they retain human
gathering; making them Daevas would replace that skill (§7 #74). The director
still only supplies initial setup and then quits. No plants or skills are spawned
or granted. Capital and Reshanta subjects keep their earlier setup.

The bounded reservation table keys by shipped position, template, map and selected
channel, not by an ever-growing set of respawn object IDs. Only one bot may own a
node at once; different nodes can be gathered concurrently. Both success and
failure consume one of its three uses, exactly as Java `completeInteraction`
does. The third attempt must observe deletion; the coordinator waits at least
295 seconds before permitting reuse and requires an actually visible node.
Queued bots keep draining packets. Cancellation releases reservations and fails
the population rather than silently continuing with uncertain node state.

Movement uses the existing collision-checked starter graph/local search, including
the return to a pair's rendezvous before social activity. The offline route test
(`AION_SOAK_NAV_INTEGRATION=1`) loads real geometry and requires round trips to at
least two nodes for each starter hub and both initial subject offsets. It passed;
this is route evidence, not yet proof of the LIVE gathering loop or capacity.

`SM_CHANNEL_INFO` now has a strict eight-byte decoder and world-model observation,
pinned to the existing Java golden fixture and malformed-length tests. Source and
trace inspection showed that Java constructs this packet before spawn, sending
its `1/1` fallback on login/teleport/channel changes. That fallback is not an
instance observation. Gathering cohorts distribute evenly across the five existing
starter channels (indices 0–4 at 50/200/500 subjects) and explicitly select theirs. They
require the ordinary channel-change system-message acknowledgement, including
after every reconnect; reservations/navigation use the selected index plus one.
The shared Java packet quirk is preserved. The
soak also opts into bounded system-message lookback, alongside its packet history
bound; disk traces and online refusal checks remain complete. Neither bound alone
proves a flat process working set. Four reservation tests cover concurrent owners,
separate instances, real cooldown boundaries and 100 respawn generations without
table growth. The existing breadth lookback remains unbounded by default.

Checkpoint checks: 4,155 solution tests passed / 22 explicit skips, with 4,243
compiler warnings unchanged. The opt-in real-geometry route check also passed
separately. Docker Fast `p10-02-gather-fast`, logger/clock/custom-quest ratchets,
fidelity, ten quest-compiler tests, 23 report tests, retention and Full-suite
contracts passed. `p10-02-gather10-a` passed its ten-subject/ten-minute diagnostic:
184 cohort actions, 18 gather attempts (13 successes / 5 failures), four depleted
plants, and two successfully harvested natural respawns. The respawns were Asmodian:
original objects 134/442 reappeared as 134297/134301 at the same shipped coordinates
after their 295-second cooldown, then were gathered with use count one. Both races
exercised depletion, but Elyos respawn reuse and nonzero-channel gathering remain
unproven by this run. All selected activities executed in every eligible cohort;
all subjects passed final inventory/offline checks. Enforced watching passed with
only the existing startup content allowance, no bot problems, and isolated-stack
cleanup. No gameplay change, new allowance or Java runtime was involved. The
two-hour workload, higher populations and resource fairness remain unaccepted.

## P10-02 repeatable duels (short LIVE diagnostic passed)

The existing S1 duel exchange/combat assertions are shared with the soak driver,
not replaced by a simulated outcome. Same-race pairs alternate caster/winner roles
on successive duels. Before fighting they recover through ordinary sit/stand
packets and the server's natural HP/MP regeneration, draining both clients while
waiting. Each duel requires reciprocal opponents, observed cast/results, reciprocal
win/loss, no actual death, cleared duel state, no blocking interaction, and unchanged
inventory totals. Spell hit timing uses the caster's actual race with the existing
male starter-book profile; both racial pointfire timings are pinned by tests.

Java reference at `ce54b7931`: `DuelService`, `PlayerController.onDie` (duel defeat
restores a 33% HP/MP floor instead of death), `CM_EMOTION` (resting state),
`PlayerGameStats` (rest regeneration multipliers), `CreatureLifeStats` and
`LifeStatsRestoreService` (ordinary restore tasks). No server behavior is changed.
The SIM S1 regression adds natural recovery and a second duel with reversed roles,
checking actual server life stats and cleanup. It passed Docker run
`p10-02-duel-sim-a` (Full shard-35/100, selecting exactly S1, seed 73).
Docker Fast `p10-02-duel-fast-a` also passed. Final checks: 4,155 solution tests
passed / 22 explicit skips; 4,243 warnings unchanged; all CLAUDE ratchets, fidelity,
quest-compiler/report tests, retention and Full-suite contracts passed.
`p10-02-duel10-a` passed its ten-subject/twenty-minute diagnostic mixing duel,
gathering, vendor, group, trade, relog and crash disconnect: 257 cohort actions,
35 duels (5/8/11/11 in the eligible cohorts), both races and reversed winner roles.
Every selected activity ran in every eligible cohort. Final inventory/offline
checks and enforced watching passed: no bot problems, only the existing startup
content fingerprint (one suppressed occurrence), no new allowance. The isolated
stack was cleaned up; the maintainer's `aion-mysql` stayed running. This is not
full P10-02 acceptance or a two-hour capacity result.

## P10-02 repeated PvP reward contract (runtime still missing)

`SoloPvpRewardContract` provides an independent AP/rank/counter oracle for GP-zero
soldiers, ordinary account rates, and one opposing damage source. It does not call
production reward functions. The source is `PvpService.rewardPlayerTeam`,
`KillCounter`, `StatFunctions.calculatePvpApGained/calculatePvPApLost`,
`AbyssRankEnum`, `AbyssRank.addAp` and `AbyssPointsService` at `ce54b7931`.

Java increments its rolling opponent counter before comparing it strictly against
the default limit five: kills 1–4 receive full AP, kill 5 onward receives 1 AP.
Victim AP loss still applies and clamps at zero. AP gains update earned daily/weekly
totals, losses do not subtract from those totals, and every credited kill increments
the kill counters. Level and soldier-rank penalties preserve single-precision Java
rounding. Victim loss uses the winner's level **after** the kill XP reward; the
winner's AP gain uses the level before that reward. Leaderboard cache position is
explicitly outside this per-kill contract; rank, max rank, GP and period counters
are checked. Officer/GP-bearing snapshots are rejected. Teams, boosted rates and
AP caps are outside this model; callers must use the ordinary-rate, uncapped profile.

Fourteen focused cases pin the fifth-kill boundary, level penalties (including
90 × 0.65f rounding to 58), level-up ordering, rank promotion/demotion, rank-penalty
cutoff, zero-AP victims and injected wrong rewards/counters/GP. The existing S2
flight/combat scenario now invokes this oracle in addition to its original
first-kill +300/-90 checks. Repeated PvP runtime, ordinary resurrection and return
remain required; this checkpoint does not claim those paths or fifth-kill LIVE
execution. The server's anti-farming limits are not disabled or reset.

Docker SIM `p10-02-pvp-oracle-sim` passed (Full shard-36/100, selecting S2,
seed 73), exercising the first-kill oracle through the real server. The
fifth-kill boundary is currently a focused contract test, not repeated LIVE proof.

## P10-02 finite quest workload (ten-subject diagnostic passed)

D16 settles the single-completion starter workload: each eligible bot completes
its racial Q1/Q2 journey once, then continues the other activities. The policy's
explicit completion operation removes Quest from the unconsumed shuffle and all
future cycles, preserving queued non-quest actions, continuous sequence numbers,
seeded reproducibility and ordinary think times. Duplicate completion, a cohort
without quests, and retirement with no remaining activities fail visibly.

The diagnostic runtime now has a finite Q1/Q2 driver for the prepared mage subjects.
It checks ordinary dialog acceptance, exclusive target ownership, Flame Bolt
cast/result timing, kill credit, item acquisition/consumption, exact quest XP/kinah
and chosen/item rewards, one completion, database rows and the completed-list
packet after normal relog. Only then may the cohort scheduler retire Quest. No GM
commands are used after setup. Shipped rewards, objectives and coordinates are
pinned independently against the XML; source handlers are Java `ReportTo`,
`MonsterHunt`, `ItemCollecting`, `_1100KaliosCall`, `_2100OrderoftheCaptain`,
`QuestItemNpcAI`, `QuestService` and `RespawnService` at `ce54b7931`.

Consumed object ids cannot be claimed again by another quest subject. This table
is limited to ten objectives per configured subject, not an unbounded record of
repeating activity. Reservation tests exercise concurrent claims, channel isolation,
failure release, duplicate completion and the finite capacity limit. Actual new
respawns must be observed over the protocol; the driver never creates replacements.

The existing sparse spawn graph/local fallback could not bridge the vendor-to-Elpas
walk. A separate longer ground search retains two-metre height/collision checks,
with a 1,000-metre distance and 65,536-visited-cell limit; the original local search
limits stay unchanged. The direct Ulgorn-to-hub search still has no route within
its bounds, so the return visits Vanar and Vandar again. Walks drain incoming packets
in short segments. This remains static starter geometry, not dynamic-door navigation
or autonomous path planning. LIVE quest execution has not yet passed.

Driver checkpoint checks: 11 focused cases passed with the real-geometry gate
enabled, including every planned outward/return leg (about 102 seconds for the
two complete routes). The full solution passed 4,177 tests / 23 explicit skips;
warnings remained at 4,243. Docker Fast `p10-02-quest-driver-fast` passed 6/6, as
did all mandatory ratchets, fidelity, compiler/report, retention and Full-suite
contracts. `p10-02-quest10-a` failed during the initial ten-subject diagnostic:
Q1101 returned exact 130 XP and COMPLETE status, but the driver incorrectly
expected complete_count in `SM_QUEST_ACTION`, which does not carry it. The
Asmodian actors also waited for prologue action state after a prior relog had
correctly moved that quest into `SM_QUEST_COMPLETED_LIST`. Both are harness
assumptions, not server divergences (§7 #75). The immediate reward assertion now
checks XP/status, and the existing final database/completed-list checks still
require count one. A focused packet-state regression covers both representations.
The failed run is retained; no problem is allowlisted. LIVE proof remains pending.

Wire-state correction checks passed: 4,178 solution tests / 23 explicit skips,
4,243 warnings unchanged, the focused packet-state regression, Docker Fast
`p10-02-quest-wire-fast` 6/6, and every mandatory ancillary check. Fresh run
`p10-02-quest10-b` later failed at the leading Elyos subject's database check,
after that subject completed the entire chain and relogged. The admin endpoint
exposes persisted `quests` only while offline; the soak caller used it after
reconnecting (§7 #76). This is not a server quest failure or a passing run.
The database assertion now runs after confirmed logout, before reconnect;
count-one/nonrepeatable packet checks remain after login. Five focused contract
cases reject online/missing snapshots, missing rows and wrong completion counts.
Fresh run `p10-02-quest10-c` passed ten subjects/twenty minutes, seed 73, with
382 cohort actions. Both Elyos subjects completed seven journey quests and both
Asmodian subjects completed six, once each. Each also verified its already-finished
prologue: 30 persisted count-one rows/completed-list entries across the four bots.
Both quest cohorts retired that action exactly once and continued all their
other eligible activities. Counts by cohort: 76, 33, 104, 106 and 63; quest counts
are one for each starter cohort, not repeated abandon/reaccept operations.
All ten subjects passed final inventory/offline checks; `bot.problems.jsonl` is
empty. The enforced watcher recorded one existing suppressed startup-content
fingerprint and zero new/known/regressed fingerprints (`failed: false`). The
isolated stack was removed; the maintainer's `aion-mysql` remained running.
No allowance was added. Higher-population contention, the whole mixed workload,
statistical/telemetry acceptance and the two-hour matrix remain unproven.

Previous scheduling/reward checkpoint validation: 26 focused policy/reward cases passed. The full solution
passed 4,172 tests with 22 explicit skips; compiler warnings stayed at 4,243.
Docker Fast `p10-02-finite-quest-fast` passed 6/6. Logger/clock/custom-quest
ratchets, fidelity, ten quest-compiler tests, 23 report tests, retention and
Full-suite contracts all passed. No new allowance or production behavior change.

## P10-02 resurrection perception checkpoint

Audited Java `SM_BIND_POINT_INFO.writeImpl` and `SM_KISK_UPDATE.writeImpl` at
`ce54b7931` now have bot decoders: exact 22/32-byte layouts, independent obelisk
and Kisk bindings, explicit Kisk-clear handling, and one last-observed Kisk update
with creator ID, members, remaining resurrections and lifetime in seconds. Nearby
same-race Kisks also broadcast updates: consumers must match IDs, not assume the
last update belongs to them. Updates do not imply binding or successful revival.
Tests cover every truncated length, trailing bytes, binding replacement/clear,
world reload and bounded observation state. The decoder inventory is now 112.

A local offline geometry probe found candidate grounded triangles near the old
airborne S2 encounter. For example, `(3180,2480,1557.9525)` to
`(3192,2480,1557.6388)` has a checked ground edge; a third checked corner is
`(3180,2492,1550.9584)`. The origin is about 108m from the nearest ordinary static
spawn returned by `SpawnsDh`. This is reconnaissance only, **not a selected or
LIVE-validated camp**: dynamic siege/artifact state, actual hostile clearance,
Kisk placement, resurrection/return and capacity spacing still need validation.
The temporary probe was removed; raw output remains in
`run/p10-02-reshanta-probe.log`. No geodata or production behavior was changed.

Focused packet/persistence checks passed 36 cases; the full solution passed
4,185 tests with 23 explicit skips and warnings stayed at 4,243. Mandatory
logger/clock/custom-quest ratchets, fidelity, ten quest-compiler tests, 23 report
tests, retention and Full-suite contracts passed. Docker Fast
`p10-02-quest-persistence-fast` passed 6/6. Repeated PvP is still unavailable and
fails closed at this perception-only checkpoint; the following driver checkpoint supersedes that limitation.

## P10-02 repeatable PvP driver (twenty-minute LIVE diagnostic passed)

The diagnostic runtime now implements the last activity type, `Pvp`. It shares
S2's ordinary Flame Bolt combat with race-specific animation timing and the
independent reward oracle. Winner roles alternate, and opponent kill counts
survive client reconnects, matching the server's 24-hour counter window. S2's
first-kill +300/-90 contract and flight assertions remain intact.

Reshanta subjects receive level/class, 500 AP and four ordinary race-specific
medium Kisk items only during fixture setup. The director logs out before the
timed workload. Subjects place and bind their own Kisks with ordinary item-use
and question-response packets. Every cast, consumed item, creator, binding,
72-charge initial count and two-hour lifetime is checked. Each actual PvP death
must offer Kisk revival; the driver sends `CM_REVIVE(4)`, requires exactly one
charge consumed, observes restored life and position, rests normally, and walks
back over checked ground. It never patches HP/MP, resets a cooldown or respawns
a Kisk. Near-expiry/exhausted Kisks wait for ordinary terminal updates and the
real item cooldown before consuming another finite-supply item.

The shared load-test camp uses the grounded Reshanta edge documented above,
with exact own-Kisk/target IDs even when cohorts share the encounter. This is an
intentional encounter hotspot, not distributed autonomous exploration or a
general safe-camp planner. Each round walks from home to its encounter point:
Java `CM_MOVE` only ends protection on actual displacement, so a zero-distance
turn after relog would not suffice. Kisk placement and combat safety remain
subject to the real server, including live zone restrictions and hostile NPCs.

Java source references at `ce54b7931`: `ToyPetSpawnAction`, `KiskAI`, `KiskService`,
`Kisk`, `CM_REVIVE`, `PlayerReviveService.kiskRevive`, `TeleportService`,
`PlayerController.see/startProtectionActiveTask`, `CM_MOVE`, and `KillCounter`.
No production behavior or geodata changes. The client retains only one
creator-matched Kisk snapshot plus its last general update; neither implies
binding or successful revival on its own.

Focused packet/item/option/reward and actual-ground-route checks passed 32 cases.
Docker Fast `p10-02-pvp-driver-fast` passed 6/6; Full SIM shard 36/100
`p10-02-pvp-driver-sim` selected and passed S2. The solution passed 4,188 tests
with 24 explicit skips, compiler warnings remained 4,243, and all mandatory
ancillary checks passed. Full LIVE repeat-kill, resurrection, fifth-kill reward,
expiry/replacement and higher-population evidence remain pending. Default SOAK
can now select every implemented activity, but its output still says
`Acceptance: false`; `run-full.ps1` still refuses Soak/All until the acceptance
driver/telemetry exists. This is not P10-02 completion.

First LIVE attempt `p10-02-pvp10-a` failed after both subjects successfully used
their normal ten-second placement casts and bound to their own 72-charge Kisks.
The generic local A* then explored a nearby dynamic shield node from the offline
client, which has no server `SiegeService` (§7 #77). The camp now uses only its
bounded direct ground edge and rechecks every segment; it does not ignore shields,
strip geometry or initialize gameplay services in the client. A fresh-process
actual-geometry regression runs 20 round trips per race with database-rounded and
ground-normalized starting coordinates and rejects off-edge destinations.
The failed run is retained, with no new allowance. The next attempt,
`p10-02-pvp10-b`, again passed both placements but failed on a stationary home
sample even with the bounded path. The original A*-only diagnosis was incomplete:
`TraceEdge` sent zero-length collision rays after ground normalization. Such a
ray is not a movement segment and may traverse unrelated scene bounds. A focused
regression first failed, then passed when the bot skipped only exactly stationary
collision segments (retaining ground lookup and every nonzero collision check).
All eight navigation/camp tests pass, including a real-geometry route test using
the movement packet's final position. No server geometry behavior is changed.
LIVE `p10-02-pvp10-c` still failed on the subsequent home sample: the zero-ray
guard alone did not resolve the LIVE dependency, so it is not claimed as the
complete root cause. The camp now treats an already-home request (exact same
X/Y, less than one centimetre Z rounding) as no movement. It sends no frames and
does not snap or replace the current position. Unit tests pin this for exact,
rounded and adjacent-float heights and require actual displacement to consult
geometry. The real home-to-encounter segment is still checked normally.
LIVE d also failed. Instrumented e identified the actual failing segment as
`(3192,2480,1557.64)` to `(3190,2480,1558.3164)`: the Asmodian's two-metre approach,
not the no-op home walk suggested by the earlier source-line attribution. The
fresh-process route tests did not reproduce this LIVE failure and are not proof
of its resolution.

Java/C# `Node.collideWith` and `BoundingBox.intersects(Ray)` use an infinite-ray
broad phase; `DespawnableNode` consults siege state before its own bounds check.
The bot's offline Reshanta scene now wraps dynamic nodes with a conservative
finite-segment AABB broad phase. It skips only nodes wholly outside the segment
(with padding), retains every node and collision intention, and delegates nearby,
unknown-bound or unbounded queries to the original path. It neither strips
shields nor supplies invented siege state; actual nearby dynamic dependencies
still fail closed. The server and SIM geometry remain unchanged. A focused test
requires distant dynamic state not to be queried while boundary, inside-box and
unbounded cases retain the original failure. Thirteen navigation/camp tests pass.
Corrected LIVE run `p10-02-pvp10-f` passes ten subjects/twenty minutes with 453
cohort actions, including eleven alternating PvP kills, ordinary Kisk revivals,
natural recoveries and checked return walks. The first kill gives +300/-90 AP;
both winners' fifth kills give exactly 1 AP, and the Elyos sixth kill also obeys
the reduced reward. Final Kisk charges are 67 (Elyos) and 66 (Asmodian).
The PvP cohort also completes eleven ordinary relogs and eleven crash-disconnect
actions without losing binding/owned-Kisk state. All subjects finish inventory
and offline checks. Enforced watching has no new/known/regressed fingerprint;
only the existing startup quest-spawn allowance occurs. Natural Kisk expiry and
replacement, higher populations and the full mixed workload remain unproven.
Offline dynamic siege-state synchronization remains
unsupported; none of these changes permits crossing an unobserved shield.

Final checkpoint validation: 4,192 solution tests passed, 24 explicit skips,
4,243 compiler warnings (unchanged). Docker Fast `p10-02-pvp-guard-fast` passed
6/6 and all CLAUDE.md ancillary checks passed. P10-02 remains unchecked.

## P10-02 process and outbound-dispatch telemetry (short LIVE diagnostic passed)

All three process heartbeats now include refreshed process working-set bytes and
`GC.GetTotalMemory(false)` (an estimate, without forcing collection). The game
server adds bounded outbound-dispatch observations: the first pending request
through the next dispatcher buffer-preparation attempt. Repeated enqueues
coalesce into one sample. This is **not** per-packet latency, completed socket
flush time, or client-observed response time.

Each ten-second heartbeat drains a disjoint aggregate window with sample count,
mean/max milliseconds, abandoned observations, and fourteen histogram counts.
The inclusive bucket upper bounds in milliseconds are
`0.1, 0.5, 1, 2, 5, 10, 20, 50, 100, 250, 500, 1000, 5000, +infinity`.
The heartbeat also samples the count and oldest age of still-pending connections,
so a stuck writer cannot disappear merely because it never contributes a
completed latency sample. Closing/discarding a pending queue counts as abandoned,
not a zero-latency success. No packet, connection or individual-sample history is
retained by the aggregate. Per-connection state is one optional probe; socketless
SIM connections do not create probes. The probe's real monotonic clock is Commons
infrastructure and does not change gameplay clocks or the clock-read ratchet.

`dispatcherWrites` is serialized as invariant JSON within the existing heartbeat
message, preserving arrays in JSONL without changing the logging envelope. Login
and chat report `null` for this game-reactor-specific metric. Counters and process
memory are independently sampled, not one global atomic server-state transaction.
No acceptance thresholds, memory-plateau claim or statistical workload success
claim follows from merely emitting these fields.

Java `commons/network/AConnection.sendPacket/close` and `Dispatcher.write` at
`ce54b7931` were inspected. Queueing, wakeups, serialization, writes and close
semantics are unchanged; these are observation-only infrastructure additions.
Three Commons metric/heartbeat tests and seven game wiring/socketless/DI tests
pass. Alternate-port Docker diagnostic `p10-02-telemetry10-a` exposed a launcher
bug (§7 #78): Compose/readiness used the configured game port, but the bot command
overrode it with 17777, producing a correctly refused cross-stack authentication.
The hardcoded override is removed; a serialized environment/launcher regression
pins all four configured ports. Corrected `p10-02-telemetry10-b` passes ten subjects
for 180 seconds, 75 cohort actions and final persistence/inventory cleanup.
Enforced watching reports no new/known/regressed fingerprint; only the existing
startup quest-spawn allowance occurs. The failed run is retained, not allowlisted.

Twenty game-server heartbeat samples contain 11,939 completed dispatch batches
(histogram totals agree), one abandoned observation, a 13.8977 ms observed maximum,
and no pending writer at the sampled instants. Working-set samples range from
2,478,239,744 to 2,535,055,360 bytes; armed timers range 632–708 across startup,
connections and cleanup. These short raw measurements are **not** a two-hour
memory/timer plateau or latency-tail acceptance result. All three processes emit
memory fields; only the game reactor emits dispatch samples. Solution validation
passes 4,196 tests with 24 explicit skips, warnings stay 4,243, Docker Fast passes
6/6, and all mandatory ancillary checks pass.

Concurrent watchers also expose a shared-ledger limitation (§7 #79): each saves
its startup snapshot with atomic replacement, not a concurrent merge. That can
overwrite later tracking edits or another run's counts. Per-run logs/reports are
the authoritative evidence. After both writers finished, the shared ledger was
reconciled to retain the telemetry10-a occurrence (count 118 rather than the stale
117) and the current tracking annotations.

## P10-02 concurrent evidence preservation

The shared ledger now serializes reads and atomic replacement through a
path-keyed cross-process mutex. Each save reloads the current ledger and adds
only observations accumulated since that watcher's previous load/save. It keeps
later tracking/status edits, first-seen provenance and unrelated fingerprints.
Automatic fixed-status transitions apply only if the current record still
equals the original snapshot; another observation or maintainer edit prevents
that stale transition. The in-memory baseline advances only after replacement
succeeds, so saving twice does not count the same observations twice.

Before the fix, regressions reproduced both count loss (5 overwritten by 3) and
Windows replacement collisions. Six new cases now cover stale green saves,
maintainer edits, auto-fix conflicts, sixteen concurrent writers, repeat-save
idempotence and two separate watcher processes. The latter waits until both
processes observe their input before permitting either to save. Together with
the existing watcher tests, all sixteen focused cases pass. No Java analogue or
gameplay behavior changes. This protects cooperating updated watchers, not old
already-running binaries or unsynchronized external edits during a save.
P10-02 remains incomplete; evidence preservation is not capacity acceptance.
Checkpoint validation passes 4,202 solution tests with 24 explicit skips;
warnings remain 4,243 and all CLAUDE.md ancillary checks pass. No gameplay,
shared-bot or network code changed, so Docker Fast is not required for this
ledger-only checkpoint.

## P10-02 telemetry evidence gate (not full soak acceptance)

`scripts/e2e/soak-telemetry.py` reads the LS/CS/GS JSONL heartbeats for an explicit
offset-qualified `--start`/`--end` workload interval and writes `--output` JSON.
It verifies run/server identity, monotonic sample times, memory presence and
dispatcher histogram/count/max consistency, and hashes all three source files.
Malformed or missing evidence fails closed; an evidence-validation failure replaces
any previous output with a failed report. It exits 0 only when this **telemetry gate** passes,
1 for a failed gate, and 2 for invalid inputs. `overallSoakAccepted` is always
false: population, workload completion, clean watching, persistence and economic
statistics are separate requirements. Runtime-window integration remains pending;
manually supplied timestamps are diagnostic analysis, not automatic acceptance.

Policy `p10-02-telemetry-v1` is declared before the capacity matrix, not fitted to
an observed two-hour result. These are harness engineering tolerances, not Java
gameplay rules or a claim of a production service-level agreement:

- Require at least 7,200 seconds and no heartbeat gap over 30 seconds, including
  either interval edge (three nominal ten-second beats).
- Ignore the first 30 minutes for plateau calculations, then require at least
  six complete 15-minute windows. Compare both last-minus-first window medians
  and positive least-squares fitted growth across the medians. Each must remain
  within max(64 MiB, 5% of the first median) for working set and managed heap,
  and max(10 timers, 2% of the first median) for armed timers, on every server.
  Medians avoid treating a single GC/allocation spike as sustained growth;
  absolute floors accommodate small processes. Peaks remain visible in reports.
- Aggregate disjoint dispatcher windows with count-weighted means and histogram
  quantile upper bounds: p99 at most 100 ms, p99.9 at most 1,000 ms, observed max
  at most 5,000 ms, and sampled pending-write age at most 1,000 ms. These bounds
  distinguish routine dispatch from sustained delays and severe individual stalls.
  The first selected heartbeat's dispatch batch is excluded because part of it
  predates the requested interval. Empty evidence and unbounded quantile buckets
  fail. Abandoned observations are reported separately, not disguised as fast
  writes or automatically failed: crash-disconnect is part of this workload.

Changing these tolerances requires a documented policy revision and rerun, not
silently relaxing a failing report. The metrics measure request-to-buffer-
preparation, not flush/RTT; sampled plateaus do not prove all leaks absent, and
pending age is sampled rather than continuously observed. Synthetic regressions
cover missing edges/interior beats, short windows, growing timers/memory,
trend-vs-endpoint masking, outlier latency, stalled uncompleted writes, invalid
histograms and real-file provenance. Retained LIVE `p10-02-telemetry10-b` logs
parse successfully and correctly fail two-hour/plateau eligibility as a short
diagnostic. No additional allowance or server behavior change is introduced.
All 17 telemetry regressions and required ancillary checks pass; the solution
passes 4,202 tests with 24 explicit skips, and warnings remain 4,243. Docker Fast
is not required for these offline reporting-only changes.

## P10-02 first complete mixed-workload diagnostic

`p10-02-mixed10-a` (source `8eda3e7ec`, seed 73) passes ten subjects with a
1,200-second scheduled workload and all ten activity types selected together.
Every cohort exercises every activity assigned to its zone. The 134 completed
cohort actions comprise 2 finite quest journeys, 7 gathering actions, 11 Cooking
actions, 4 vendor transactions, 16 trades, 16 group cycles, 15 duels, 11 PvP
cycles, 25 ordinary relogs and 27 crash-disconnects. Pair actions exercise both
subjects: the trace contains 14 gathers (13 successes), 22 completed work orders
and 99 craft attempts (84 successes). These small, mixed-skill samples are not
statistical acceptance evidence.

Both Elyos and both Asmodian quest subjects verify their persisted journeys
(30 saved completions including the four prologues), retire quest work without
resetting it, and continue their other activities. All ten subjects verify final
inventory, quit, and verify offline status. Enforced watching reports zero new,
known or regressed fingerprints; the only suppressed event is the existing
startup quest-spawn allowance. `bot.problems.jsonl` is empty. The isolated Docker
stack is removed after a terminal exit 0; the maintainer's database is untouched.

Some actions begun before the deadline finish afterwards: the final Cooking
pair completes at 04:24:43 UTC, after the scheduled workload ended around
04:21:12 UTC. This overrun is completion/cleanup, not additional scheduled
population time and must not extend a capacity telemetry window. The diagnostic
retains `Acceptance: false`. Fifty/two-hundred/five-hundred subjects for two hours,
Kisk expiry/replacement, statistical assertions and the automated acceptance
driver remain outstanding. This supersedes earlier statements that the complete
mixed workload had not yet been exercised, but does not close P10-02.

Post-run telemetry analysis finds an additional observability defect (§7 #80):
the login-server heartbeat records a negative `GC.GetTotalMemory(false)` estimate
of -907,936 bytes at 04:22:00 UTC, followed by more negative samples during
cleanup. The strict telemetry reader rejects the evidence. The successful
workload/watcher result above does not override that rejection, and no values
are clamped or allowlisted. The sampler needs a reliable, explicitly defined
replacement or qualification before capacity telemetry acceptance. The retained
raw logs are the source for this finding. The full solution and all mandatory
ancillary checks remain green (4,202 passed, 24 explicit skips; 4,243 warnings).

## P10-02 explicitly last-GC heap telemetry (v2)

The sampler no longer uses `GC.GetTotalMemory(false)`. It records
`lastGcHeapBytes` and `lastGcIndex` from the same `GC.GetGCMemoryInfo()` result.
These names deliberately change the log schema: last-collection heap size must
not be mistaken for the previous current-heap estimate. No collection is forced,
and no invalid value is clamped. Index zero means no collection has happened;
the analyzer treats that heap observation as unavailable, not a measured zero.
A real zero from an identified collection remains a zero measurement.

The API describes the last collection and can remain stale between collections,
as documented in [Microsoft's GCMemoryInfo reference](https://learn.microsoft.com/en-us/dotnet/api/system.gcmemoryinfo?view=net-10.0).
Reports expose first/last collection index and the number of distinct indices
observed, alongside working-set measurements that are still refreshed every
heartbeat. This does not claim the heap snapshot captures current allocations.
Policy `p10-02-telemetry-v2` keeps all v1 tolerances and changes only this named
measurement and its availability semantics. It rejects old-schema records,
including the retained negative estimate; v1 reports are not silently upgraded.
Three Commons heartbeat tests and 21 Python telemetry cases pass, including
uncollected vs measured-zero heaps, legacy rejection and visible staleness.
Corrected LIVE `p10-02-heap10-a` passes ten subjects/180 seconds, 75 cohort
actions and all final checks. Enforced watching has no new/known/regressed
fingerprint; only the existing startup allowance occurs. The retained samples
parse under v2 and correctly fail two-hour eligibility. In the diagnostic window,
all three servers have 18 samples. LS transitions from collection index zero to
one (1,320,272-byte last-GC heap); CS has no collection and reports unavailable,
not a flat zero; GS shows indices 369 through 383 with fifteen distinct observed
indices. This validates measurement identity and availability, not a plateau.
The solution passes 4,204 tests with 24 explicit skips, warnings remain 4,243,
Docker Fast passes 6/6, and all required ancillary checks pass.

## P10-02 first 50-subject mixed attempt (failed, not a soak result)

`p10-02-mixed50-2h-a` (source `4fd4e0e87`, seed 73) prepares all fifty subjects,
then stops at 04:31:20 UTC on cohort 25's PvP assertion, well before its requested
two hours. The winner expected 300 AP but received 330; victim AP loss and kill
counts match. The trace places a `[Server Buff]` notification immediately before
the 330-AP system message, and decoded `SM_ABNORMAL_STATE` confirms skill 10549,
level 1, BOOST slot, with 3,600,000 ms remaining. Source inspection identifies the shipped 2% PvP-kill
trigger for skill 10549 (+10% AP). Java invokes that event before calculating
the reward and applies `AP_BOOST` through `Rates.AP_PVP`; C# follows this path.
The existing independent oracle explicitly excludes boosts, so it does not
cover the enabled production-random event. This is tracked as §7 #81, not a
reason to disable the event or widen the acceptable AP range.

The runner exits 1 and removes its isolated Docker stack. Enforced watching
records the reused generic bot fingerprint plus cancellation fallout (46 bot
records, not 46 independent causes). The ledger preserves those counts and the
new diagnosis. No allowance is added. The next scaled replay needs an oracle
that validates the actually observed active effect and its reward timing,
including the fifth-opponent-kill reduction that bypasses ordinary boosted AP.

## P10-02 observed-effect PvP reward correction

The bot now retains the complete visible effect snapshot from `SM_ABNORMAL_STATE`
and captures it at the AP/counter-changing `SM_ABYSS_RANK`, rather than consulting
whatever effects happen to remain when the combat assertion eventually runs.
Later expiry and leaderboard-position refresh cannot rewrite that evidence.
World entry/reload invalidates the snapshot; unknown is not treated as no buffs.
The slot mask does not make this a slot-filtered delta: Java
`PlayerEffectController.updatePlayerEffectIcons` sends all visible effects even
for a single changed slot. The normal post-entry full snapshot restores authority.

An independent shipped-skill-data catalog supports the currently shipped static
ADD `AP_BOOST` modifiers. Unknown skills and unsupported future AP definitions
fail closed instead of silently assuming a rate. The oracle applies their
single-precision multiplier after base reward rounding, truncates as
`Rates.AP_PVP` does, and still bypasses it for the fifth-and-later 1-AP reward.
Victim loss and all rank/kill/daily/weekly assertions remain exact. No gameplay,
event probability, production data or allowlist changes are made.

Thirteen new regression cases include the failed run's observed
710 → 1040 AP transition with skill 10549, post-reward expiry, reload invalidation,
malformed packets, unsupported modifiers, boosted rewards, Java truncation and
the repeat-kill reduction. All 65 focused decoder/model/reward tests pass.
The full solution passes 4,217 tests with 24 explicit skips; warnings remain
4,243. Docker Fast and the isolated Full S2 shard each pass all six selected
tests; all required ancillary checks pass.

`p10-02-mixed50-2h-b` did not validate the fix at scale: an invocation error left
the generic 15-second step timeout in place. All fifty subjects were prepared,
but the first ordinary duel was cancelled during its legitimate cast delays;
a concurrent crash/reconnect step also reached that deadline. The run exits 1,
removes its Docker stack, and retains both timeout fingerprints and mirrored
cancellation fallout (48 bot records). This is not evidence of a production
combat/reconnect defect. The corrected invocation must explicitly allow the
long bounded quest/combat/recovery steps. A passing scaled replay is still
required; no timeout is allowlisted and no capacity acceptance is claimed.

## P10-02 runner-recorded workload window

`soak-window.json` anchors the telemetry interval to the same monotonic start
released to all prepared cohorts after the director logs out. Its scheduled
end is start plus the requested workload duration. Completion/cleanup time is
recorded separately and cannot enlarge the interval. An early/cancelled run
remains failed, and a hard-killed process leaves a nonterminal running record;
neither can pass recorded-window analysis. Terminal evidence reports the
difference between elapsed monotonic time and elapsed wall time; more than two
seconds of disagreement invalidates the UTC mapping instead of shifting it.

The CLI's `--recorded-window` mode validates run identity, subject count,
configured duration, terminal status, elapsed time and clock consistency before
the unchanged v2 telemetry gates. It hashes the window and bot metadata along
with the server logs. Manual `--start`/`--end` remain explicitly labelled manual
diagnostics and cannot be mixed with the recorded option. Bot metadata now also
records step/connect timeouts and the selected soak duration/activities, so an
invocation such as mixed50-2h-b is auditable without guessing its timeout.

`run-live.ps1` automatically retains `soak-telemetry.json` after a SOAK run.
Short diagnostics can pass workload checks while failing two-hour telemetry
eligibility; invalid/missing evidence is a runner failure. Both cases are
explicitly separate from overall soak acceptance, which is always false until
the remaining economic, workload and capacity gates are implemented. Full/Soak
still refuses to launch without the complete acceptance driver.

Eight C# window tests and 25 Python telemetry tests pass. The full solution
passes 4,225 tests with 24 explicit skips; warnings remain 4,243. Docker Fast
passes 6/6, and all required ancillary checks pass.

LIVE `p10-02-window10-a` passes ten subjects, 180 scheduled seconds, all final
inventory/offline checks and enforced log watching (only the existing startup
allowance; no new/known/regressed fingerprint). The retained window spans
05:02:01.3557186–05:05:01.3557186 UTC on 2026-09-20. Completion is separately
recorded at 05:05:16.7732755, with 195.4175185 monotonic seconds and only
0.0000384 seconds of wall-clock disagreement. An in-flight probe correctly
rejects the running record. Automatic terminal analysis hashes all five inputs,
reports exactly 180 workload seconds, and rejects two-hour eligibility without
counting the 15.4-second cleanup overrun. The diagnostic exits 0 and removes its
isolated Docker stack. This is reporting evidence, not capacity acceptance.

The longer fifty-subject mixed replay `p10-02-mixed50-2h-c` runs source
`1fb659efe` with an explicit 1,800-second activity-step timeout. It started
before the recorded-window change and cannot gain that provenance retroactively.
It exits 1 at 05:26:54 UTC on b01's 1,800-second quest-step timeout; its isolated
Docker stack is removed. Nineteen other finite starter journeys persisted.
The b01 trace identifies the actual stall: a Flame Bolt (1282) starts at
05:02:52.569 against object 12667, then an NPC attack and `SM_SKILL_CANCEL`
arrive at 05:02:54.582. The bot waits only for `SM_CASTSPELL_RESULT`, which Java
correctly does not send for this cancelled cast. The subject remains connected,
but that is not workload progress. Section 7 #83 tracks this harness omission;
do not disable interruptions or extend the timeout. Watching records one startup
allowance, two regressed generic fingerprints and 41 repeats (43 bot records,
including cancellation fallout, not independent root causes). No allowance is
added. This failed run is not capacity or new-policy statistics evidence.

## P10-02 source-derived statistical economy gate

The probability model and gate are specified in
[the versioned statistical policy](e2e-soak-statistics.md). This replaces the
unverified completion-rate guesses with discrete competing-bar calculations,
including critical progress, skill lead, float scaling and craft truncation.
The twenty Cooking recipes/products are validated against the supported model.
Every new LIVE outcome records its pre-action probability/model version.

The gate uses both a fixed first-twenty sample per enrolled subject and a
whole-stream test retaining every later attempt. Omitted subjects, small samples
and insufficient expected failures cannot pass. Forced early success/failure
and late biased-stream controls reject. Reports retain log evidence, exposure
and bounded counters; neither statistical non-rejection nor a short diagnostic
claims overall soak acceptance. Section 7 #82 records a newly identified random
float endpoint divergence; its small, explicitly bounded probability envelope
does not mark that production issue fixed.

Twenty-eight model/statistics tests pass, including ten independent
150,000-attempt bar simulations, exhaustive 24-bit increment enumeration and a
failure-to-problem-watcher regression. The full solution passes 4,253 tests with
24 explicit skips; warnings remain 4,243 and Docker Fast passes 6/6.
All required ancillary checks pass. The failed mixed50-2h-c predates this gate
and cannot retroactively acquire its runtime observations or be reported as
passing the new statistics policy.

LIVE `p10-02-stats10-a` passes ten subjects/600 scheduled seconds, 130 cohort
actions, exact final inventory/offline checks and enforced watching. It records
67 crafts (60 successful) and 22 gathers (17 successful), each with its pre-action
probability. All four statistical tests correctly remain **insufficient**:
individual prefixes are incomplete, and gathering has only 4.7504 expected
failures. There are no impossible outcomes. This is integration evidence, not
proof of the null or a full soak. Automatic telemetry analyzes exactly 600
seconds and rejects two-hour eligibility. The final in-flight work order and
cleanup take another 211.7233 seconds; they do not enlarge that telemetry window.
The run exits 0 and removes its isolated Docker stack. Its compiled diagnostic
predates the final failure-to-problem-watcher helper, which is unit-tested;
this run does not claim LIVE rejection-path coverage.

## P10-02 interrupted-cast handling

Quest combat, S1 duels and S2/PvP now share caster-and-skill-filtered start and
terminal waits. A normal `SM_SKILL_CANCEL` is terminal without a result, matching
Java `PlayerController.cancelCurrentSkill`; the existing `BotApi` observation
releases its casting gate. All packets still pass through one reader and the
ordinary perception/reflex path. The next attempt retains the conservative
two-second cadence and the existing 30/60-attempt combat bound. Successful casts
retain their animation delay, and death/reward/loot assertions are unchanged.

Each start/completion packet wait has its own ten-second deadline, distinct
from the longer overall quest budget. Completion waits begin after the advertised
cast-duration advance in either SIM or LIVE. Missing packets fail the scenario;
they are not retried on a potentially pending socket read. Caller cancellation
is not relabelled as a protocol timeout. Twelve focused tests include the
mixed50-2h-c caster/skill cancellation, other-caster/skill filtering, preserved
animation recovery and manually driven deadline/cancellation checks without
real-time sleeps. The full solution passes 4,265 tests with 24 explicit skips;
warnings remain 4,243. Docker Fast and the separately selected Full S1/S2 shards
each pass all six selected tests, including log and teardown gates. All required
ancillary checks pass. The corrected scaled LIVE replay remains pending.

The corrected `p10-02-mixed50-2h-d` runs source `58c74165d`, seed 73, all ten
activities, a 1,800-second activity budget and the full 7,200-second workload.
Its recorded window is 05:45:22.7835411–07:45:22.7835411 UTC on 2026-09-20.
At 05:53:53.404, b41 records a normal Flame Bolt interruption, then completes
and persists its full Q1 journey at 05:55:50.810. This is direct recovery
evidence for #83, not a terminal capacity result. The run remains active.
By 06:08 UTC, all twenty starter subjects have persisted their finite journeys;
the only server problem fingerprint observed is the existing startup allowance.

## P10-02 workload replay and aggregate acceptance

`dotnet run --project tools/Aion.LiveBots -- --soak-evidence run/<id>` reads
stable terminal evidence and writes `soak-workload.json`. It streams the traces
with bounded per-subject counters and hashes their exact bytes. It reconstructs
the window, checks every expected subject, replays each seeded cohort schedule,
checks observed maps/channel selections, requires the actual activity-start
records and compares decision counts against the runtime summary. Each subject
must join the shared start within 30 seconds, stop starting activities at the
scheduled end, and finish inventory/quit/offline checks afterward. Full capacity
configurations require every repeatable activity at least twice per eligible
subject, not just one action plus a long idle connection.

Finite Q1/Q2 retirement requires every ordered reward plus the exact persisted
quest set once. The reader recomputes each gather/craft probability from its
skill/recipe evidence and reruns both statistical tests; the recorded economic
summary must agree (floating score totals allow only 1e-8 accumulation error
from interleaving). The director must quit before the window begins and send
no later traffic. No missing data is relabelled a pass.

`scripts/live/run-soak.ps1` supplies the Full runner's missing child. It fixes
enforced watching, the full mixed workload and the explicit activity budget,
preserves Full-run evidence retention, and journals the owning LIVE invocation's
terminal result. It then runs workload validation and `soak-acceptance.py`.
The aggregator rehashes every workload input, recomputes telemetry from raw
heartbeats, requires sufficient/passing economy statistics, checks enforced
watcher success against retained problem logs, and rechecks each used exact
allowance's owner/reason/tracking/expiry/count. All gates must pass before
`soak-acceptance.json` sets `overallSoakAccepted: true`; that accepts one population,
not the three-population matrix or Phase 10. Invalid evidence overwrites stale
green output with failure. Short diagnostics remain available through run-live
and cannot pass run-soak acceptance.

Nineteen C# evidence tests and six Python aggregation tests (including seventeen
individually exercised mutation cases) cover missing/altered evidence, schedule,
quest retirement, probabilities, telemetry, watcher and allowance failures.
The PowerShell contract executes the actual runner dispatch with substituted
children and verifies propagation, its terminal journal and all failure
boundaries without Docker. Synthetic green evidence is explicitly only a test
of these gates. The real retained `p10-02-stats10-a` replays successfully as a
diagnostic, with insufficient economy exposure and no capacity configuration;
the active fifty-subject window is correctly rejected as nonterminal.
That fifty-subject invocation predates the new owning-wrapper journal and will
remain diagnostic evidence even if it finishes cleanly; no terminal authority
is fabricated retroactively. The full solution passes 4,284 tests with 24
explicit skips; the warning inventory remains 4,243. Docker Fast passes 6/6.
All mandatory ancillary checks, including the new acceptance and runner
contracts now listed in CLAUDE.md, pass. No production behavior or allowance
has changed. Full 50/200/500-subject accepted runs are still outstanding.

## Scope decisions

- P10-05 and siege/housing-dependent journeys remain deferred under D7.
- P10-06 Java runtime comparisons are explicitly deferred under D15. Java remains
  the source and golden-fixture reference; no Java server is started.
- P10-07 needs real **4.8** client protocol captures, not just extracted geodata.
- A green orchestration contract does not prove a two-hour populated soak, five
  consecutive Full runs, or natural autonomous player progression.
