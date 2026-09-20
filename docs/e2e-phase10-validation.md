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
`FullRun`, `Bots`, `DurationSeconds`, `Seed`, `PacketTap`, `SkipImageBuild`, and
optional `BotExecution` (`Host`, the default, or `Docker`).
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

## P10-02 sustained-work evidence correction

The post-commit audit of workload v1 exposed a gap (#84): total repeated-action
counts plus a final two-hour timestamp still accepted a synthetic twenty-second
burst followed by almost two hours of idle time. A red regression reproduces
that acceptance before the correction; no real capacity run was accepted by v1.

Workload/aggregate policy v2 requires positive selected-activity progress in
every one of the eight fifteen-minute windows for every subject. Validated
seeded decisions, starter quest completions and gather/craft outcomes count;
ambient packets, pings, think markers and cleanup outside the scheduled window
do not. The report exposes those eight counters per subject. This is a coarse
sustained-work gate, not a throughput SLA or proof that every intervening second
is busy. Older v1 reports cannot satisfy v2 acceptance. The regression's idle
case, including ambient traffic and think markers, now fails; its spread-out
control passes. All 21 workload evidence tests pass, as do the six aggregation
tests (now 21 individual mutation controls). Full solution validation passes
4,286 tests with 24 explicit skips; warnings remain 4,243, Docker Fast passes
6/6 and every required ancillary check passes. The real stats10-a diagnostic
still replays successfully under v2 without claiming capacity acceptance.

## P10-02 concurrent artifact retention

Audit found a second retention defect (#85): preserving the current run and Full
siblings did not protect a different active root from another standalone run's
twenty-folder cleanup. A regression reproduces deletion before the correction.
LIVE, SIM and Full roots now create immutable `run-owner.json` provenance with
machine, PID and process creation ticks. Cleanup counts only owners demonstrably
exited (including reused PIDs), keeps the nineteen newest completed runs plus the
current run, and leaves live/foreign/malformed/unregistered roots untouched.
Unknown legacy roots therefore need deliberate manual cleanup; age alone is not
proof that deleting them is safe. Ownership remains after completion, and nested
same-process runners keep protection through evidence aggregation. The contract
also checks that registration cannot overwrite another owner's record.

Validation: full solution 4,286 passed / 24 explicit skips; warning baseline
4,243; Docker Fast 6/6 with an actual owner record; all CLAUDE.md ancillary
checks pass. This changes artifact lifecycle only, not gameplay or Java parity.

The acceptance-owned `p10-02-capacity-matrix-a` started at source `746a9cbeb`,
seed 73, with ordered 50/200/500-subject two-hour children and separate Docker
ports/databases. Its first population overlaps the earlier fifty-subject
diagnostic until that workload ends at 07:45:22 UTC. This is shared-host load,
not an isolated hardware benchmark; all per-server acceptance gates still apply.
Neither invocation is terminal or accepted at this checkpoint. Both started
before the ownership correction and are not retroactively assigned markers;
their old standalone cleanup is protected by keeping newer root counts below
its historical retention limit while it remains active.

## P10-02 lifecycle isolation and HTTP pooling

The older mixed fifty-subject run showed increasing armed-timer samples despite
comparatively steady recent memory. A separate `p10-02-lifecycle50-a` selected
only group/trade/relog/crash-disconnect for fifty subjects/600 seconds to help
separate lifecycle pressure from the combat/economy workload. It ran on its own
Docker project and ports 12136/17807/11271/17810, sharing the physical host with
the two existing runs. It failed after 171.38 seconds at 06:46:21 UTC with Windows
socket error 10055 while opening an HTTP connection for the offline-state oracle.
The 47 retained bot records are two mirrored failures and 45 cancellation records;
the watcher reports one regressed generic bot-assertion fingerprint. No allowance
was added. Its stack was removed by normal cleanup; both older runs stayed live
with empty bot-problem files at the immediate follow-up check.

Inspection found six oracle paths constructed/disposed `HttpClient` on every
read. A real loopback regression fails before the change (eight reads, eight TCP
connections) and passes with one session-owned pool. All six paths now use that
client across relogs, retain request-local authentication and response disposal,
and dispose the pool on session teardown, even if transport cleanup throws.
The regression also verifies that a disposed session rejects further HTTP reads.
This corrects confirmed connection churn (#86), not a proven diagnosis of the
host's 10055 failure. No retry, socket/OS tuning or gameplay change is introduced.
The timer-growth investigation is separate and remains open pending evidence.

The corrected `p10-02-lifecycle50-b` repeats the same fifty-subject/600-second
diagnostic from `e7d820e04` plus the local HTTP-pooling diff. It is diagnostic,
not capacity acceptance. It passes 600 seconds / 1,215 cohort actions, with
final inventory/offline checks for all fifty subjects and clean enforced watching
(only the existing startup allowance). Workload ends 07:03:06 UTC; terminal
cleanup completes 20.157 seconds later. Two-minute diagnostic timer medians are
790, 752, 742, 744 and 774: this short lifecycle-only run does not reproduce the
mixed run's sustained rise, but cannot establish a two-hour plateau or causation.
Validation passes: full solution 4,287 tests / 24 explicit skips, warning baseline
4,243, Docker Fast 6/6, and every CLAUDE.md ancillary check. Failure records and
investigation limits are retained in the plan and known-problem ledger.

## P10-02 bounded watcher history

The older fifty-subject watcher reached about 1.8 GiB private memory after roughly
seventy minutes. Inspection found full raw packet lines retained indefinitely in
`stepsByAccount` (#87); this is host-side harness memory, distinct from the server
working-set/heap/timer acceptance metrics. The live cache now retains at most
64 records and 128 Ki characters per account. An eviction watermark prevents
incorrect cache attribution for old, equal-timestamp or out-of-order records;
those cases stream the retained account traces. Reproduction bundles recover the
exact latest fifty eligible records from disk with a bounded selection queue,
including old errors discovered after the live cache has moved on.

File tails use finite byte snapshots and pooled chunks, retaining only an
unfinished line between polls. Server-log reproduction context streams a bounded
window rather than reading the entire log. Docker follower producers apply
backpressure at 1,024 queued lines; records are not dropped. Summary fields expose
retained trace-record and character counts. Raw files, refusal detection,
fingerprint classification and allowance limits are unchanged.

A red regression retains 20,000 records before the fix; after it, the bounded
cache still resolves the error at second 56 and its exact preceding fifty
records. Focused tests cover oversized rows, tied and late timestamps, account
isolation, split UTF-8/CRLF/partial lines, truncation, early enumeration disposal,
finite snapshots during appends, and queue backpressure. `p10-02-duel50-a` passes
50 subjects / 600 seconds / 517 cohort actions, including 120 duels, with final
checks and clean enforced watching. The watcher retains 3,264 records (51 accounts
including the director), 880,683 characters, and preserves 100,091,770 raw trace
bytes. A mid-run private-memory sample is about 35 MiB. Two-minute server timer
medians are 832.5, 814.5, 811.5, 793 and 854; this short diagnostic does not prove
a plateau or establish the mixed-workload growth cause.

Validation passes: full solution 4,292 tests / 24 explicit skips, warning baseline
4,243, Docker Fast 6/6, and all CLAUDE.md ancillary checks. This is an infrastructure
correction, not a completed capacity gate; P10-02 remains unchecked.

## P10-02 timer-growth attribution (investigation open)

Both fifty-subject mixed runs show post-warm-up armed-timer growth. At the
95-minute checkpoint, the older run's rounded fifteen-minute medians are 1,193,
1,123, 1,231, 1,324, 1,388 and 1,483. The matrix child's first four are 1,187,
1,117, 1,211 and 1,302. These observations do not establish a leaking callback,
nor do they satisfy the unchanged plateau gate. The short lifecycle-only control
did not show the same sustained trend; duel isolation is running separately.

An opt-in `AION_TIMER_CENSUS=1` diagnostic now accompanies each game heartbeat in
the bot Docker stack. It groups active scheduled tasks by callback method,
one-shot/fixed-rate kind, original delay and period, with the oldest registration
time. The output caps groups at 128 and explicitly accounts for omitted active
tasks. Metadata is removed on completion, fault or cancellation; completed
history, delegate targets, stack traces and player objects are not retained.
Anonymous scheduler adapters can still have opaque method names, so the census
is attribution evidence, not an automatic leak verdict.

Production defaults to count-only metrics. The separate diagnostic event leaves
the existing heartbeat format and acceptance thresholds unchanged. Java
`utils/ThreadPoolManager.java:schedule/scheduleAtFixedRate/getStats` at
`ce54b7931` was read as the scheduling reference; this adds C# observability only,
with no callback execution, cancellation or timing change. Runtime attribution
still requires a newly built image; older running containers cannot emit it.

Validation passes: full solution 4,297 tests / 24 explicit skips, warning baseline
4,243, Docker Fast 6/6, and all CLAUDE.md ancillary checks. Focused tests cover
default-off behavior, grouping/oldest registration, every terminal state,
already-completed observations, output truncation accounting and the unchanged
heartbeat event contract.

## P10-02 conquest spawner investigation

The new census is being exercised by `p10-02-economy50-census-a`: fifty subjects,
seed 73, twenty minutes of gathering/crafting/vendor/social/lifecycle activities,
without quest/combat activities. Its game image is
`sha256:747613998e573b63c473941ae6e415e6bac97f36aa8b03a09268d512d543262c`
from `7fd98ccbe`; workload starts 07:28:21 UTC. It remains a diagnostic, not a
capacity population result.

The census exposed 162 armed conquest-spawner cycles at boot. Reading their
retail source found an unconditional-repeat defect (#88): success should stop
the timer with a latched flag until a valid reset message. The fixed-seed virtual
regression produces six offerings without resets before the correction and one
after it. All 24 spawner patterns share the same flag/stop/reset contract. Details
and the source hash are in `retail-ai-fidelity.md`. This corrects a demonstrated
behavior bug without disabling content; its contribution to LIVE timer growth
still requires a corrected-image run. The spot-family odds/lifetime gap (#89)
is recorded separately and remains open.

Spawner-fix validation passes: 13 focused AI tests, full solution 4,300 tests /
24 explicit skips, warning baseline 4,243, Docker Fast 6/6 and every CLAUDE.md
ancillary check. The old-image census's 24-hour effect timers rise from 344 at
boot to 391 after the first eight-minute spawn wave; this temporal association
is not substituted for corrected-image capacity evidence.

## P10-02 natural Kisk retirement and census parser corrections

`p10-02-mixed50-2h-d` reached natural Kisk expiry but hung in PvP step s380.
Subject b09 received, at 07:45:57.162 UTC, the final update for owned Kisk 134371
(42 charges, **one second** remaining), its old bind point, `STR_BINDSTONE_IS_REMOVED`
and `SM_DELETE`. The old loop awaited a zero lifetime that never arrived. Java
`KiskService.removeKisk`, `KiskAI.handleDespawned` and `Kisk.getRemainingLifetime`
at `ce54b7931` confirm this is a bot assumption, not a server timing divergence.

The bot now retains an explicit owned/bound removal notice joined to its subsequent
delete; visibility-only deletes do not suffice. It preserves the raw final update
and old binding, clears retirement for a new owned Kisk, rejects unexpected
destruction, and fails missing retirement evidence thirty seconds after the
observed lifetime. Replacement still uses ordinary item cooldown, casting and
binding. The captured sequence fails before the fix and passes afterward.

The obsolete mixed run and `p10-02-capacity-matrix-a` were intentionally aborted;
both owning scripts report failure and removed only their isolated Docker stacks.
Their raw files, incomplete windows and diagnostic-abort notes remain intact.
The corrected image/bot combination still needs a fresh owning capacity matrix;
these runs do not establish natural replacement or a plateau.

`p10-02-economy50-census-a` completed 1,200 seconds, 1,070 actions and all final
checks. Enforced watcher: one existing suppressed startup fingerprint, zero
new/known/regressed problems; retained cache 3,264 records / 862,104 characters.
Its owning invocation nevertheless failed when telemetry treated a census event
as a heartbeat. The parser regression recognizes only that explicit supplemental
event, includes its bytes in the source hash, and does not let it fill heartbeat
gaps. Wrong identities, corrupt census and unknown category events still fail.
`soak-telemetry-parser-replay.json` parses the preserved run but correctly fails
the two-hour requirement; the original failed invocation is not rewritten.

Validation: full solution 4,301 passed / 24 explicit skips; warning baseline
4,243; Docker Fast 6/6 (36.4 seconds); all CLAUDE.md ancillary checks, including
26 telemetry parser/policy tests, pass. P10-02 remains unchecked.

## P10-02 active-log bundle regression

The follow-up watcher audit found that server-context extraction still used
`File.ReadLines`, whose Windows share mode conflicts with an open writer. Two
regressions run the real watcher with a writable server-log handle held open;
both initially throw `IOException` from `ProblemBundleWriter.WriteServerContext`.
The bundle reader now reuses `FileTail`'s shared, finite complete-line snapshot.
The matched-message path remains bounded at 200 lines, the no-match fallback
at 101; neither includes a producer's unfinished last record. Both cases verify
the metadata file and successful subsequent producer writes. This is harness-only
and has no Java gameplay counterpart. The running matrix's binaries are not
rebuilt or replaced in place.

Validation passes: 17 focused watcher tests, full solution 4,303 passed / 24
explicit skips, warning baseline 4,243, Docker Fast 6/6 and every CLAUDE.md
ancillary check. P10-02 remains unchecked; this repairs error evidence, not the
pending capacity result.

## P10-02 object-ID lifecycle audit (open)

A read-only check of replacement Kisk identity found an existing server parity
gap (#93), not evidence that the running C# server reused a retired Kisk's ID.
At Java `ce54b7931`, `Kisk` inherits the `Npc` auto-release constructor path;
`AionObject` registers a Cleaner that hands pending respawns their release
responsibility or returns the ID to the factory after collection. The C# base
constructor explicitly discards that option. Its `RespawnService.SetAutoReleaseId`
exists but has no caller, and `World.RemoveObject` does not provide an alternative
release path. Both factories can reuse explicitly released IDs, which alone does
not prove that C# Kisk deletion releases one.

This remains open: do not change production object ownership or make a speculative
bot correction during the capacity run. An eventual lifecycle fix must preserve
the pending-respawn contract and test a replacement Kisk with a reused ID (the
current bot clears its retirement observation only for a different owned ID).
No measured memory slope is attributed to this gap; a passing bounded soak would
not prove indefinite ID reclamation. P10-02 remains unchecked.

Documentation-only validation: full solution 4,303 passed / 24 explicit skips;
warning baseline 4,243; all CLAUDE.md ancillary checks pass. No gameplay change,
upstream automation change, or additional LIVE stack was introduced.

## P10-02 matrix-b: complete 50-subject workload, failed heap availability

`run/p10-02-capacity-matrix-b/p10-02-capacity-matrix-b-soak-50` uses bot source
`87db142f6c8130938419cd2edf6c466d6aef0427`, seed 73, and game image
`sha256:1f38a4c1c4e5f45832ae7ba8eff67c98ecdd562d9091c7f04fb5d9df5217559d`
(production `83836fe47`). The measured window is 2026-09-20 08:02:43.0798546–
10:02:43.0798546 UTC; in-flight work and cleanup finish at 10:06:29.3262372 UTC.
The owning LIVE invocation succeeds, but overall soak acceptance **fails** and
the Full runner stops before 200/500. The failed terminal journal and all raw
artifacts remain unchanged. P10-02 stays unchecked.

- Workload replay passes for all 50 subjects and 4,732 cohort actions; all eight
  fifteen-minute activity windows have progress. Twenty eligible quest subjects
  persist Q1/Q2 exactly once; all subjects finish inventory/quit/offline checks.
- Economy fixed-prefix and whole-stream gates pass: 3,347 craft attempts and 714
  gather attempts, with twenty enrolled subjects per activity and twenty-prefix
  samples per subject. These are production-random outcomes, not rate overrides.
- All ten PvP subjects naturally retire their initial Kisk and bind a replacement.
  Retained packets verify removal notice, old-ID deletion, ordinary ten-second
  item cast, creator-matched fresh 72-charge/7,200-second update and new bind point.
  All ten subsequently complete their 62nd PvP cycle. For example, b39 receives
  the old Kisk 134342's final one-second update/removal/deletion at 10:02:53.780 UTC,
  then binds 145987 at 10:03:04.139 UTC and completes the cycle at 10:04:16.171 UTC.
- Enforced watcher: one existing suppressed startup fingerprint; zero new,
  known or regressed problems. Retained cache: 3,264 records / 773,602 characters.
- Game-server telemetry passes all six complete windows: timer medians 900,
  911, 905.5, 902, 904.5, 900; working-set endpoint growth 13,015,040 bytes and
  last-GC heap endpoint growth 4,974,952 bytes. Of 2,037,105 dispatch observations,
  p99 upper bound is 0.5 ms, p99.9 is 5 ms, maximum is 114.7255 ms, and oldest
  pending age is 0.4668 ms. These measure preparation dispatch, not network RTT.
- Login telemetry passes. Chat heartbeat/working-set/timer checks pass, but its
  first completed GC is too late: the first heap window has no samples and the
  second has only sixteen. The last-GC metric is unavailable before that event,
  not zero. This sole failing gate prevents population acceptance.

The correction is a capacity-only startup precondition, not a policy relaxation.
`scripts/live/soak-heap-readiness.ps1` waits for all three fresh, same-run heap
observations before any bots start. It keeps error watching active, takes bounded
shared-file snapshots, ignores partial edge records, preserves timestamp offsets,
rejects invalid identities/schema, and fails on watcher exit or a 90-minute default
timeout (`run-live.ps1 -SoakHeapReadyTimeoutSeconds`). No collection is forced and
no allocation pressure is generated. `soak-heap-readiness.json` records the extra
unmeasured setup time separately; it never claims capacity acceptance. The normal
two-hour workload, thirty-minute workload warm-up, six-window policy and all
thresholds remain unchanged. Short diagnostic runs do not incur this preflight.
Earlier startup growth is outside the measured workload, as with other setup;
this does not prove startup memory is flat or that an idle runtime must collect.

The new readiness contract is exercised by the existing mandatory
`scripts/live/test-run-soak.ps1` check. A fresh owning matrix was required;
matrix-c below supplies the accepted fifty-subject result. The old failed invocation
cannot be upgraded by changing its report.

Preflight validation passes: full solution 4,303 tests / 24 explicit skips,
warning baseline 4,243, Docker Fast 6/6 (37.3 seconds), every CLAUDE.md ancillary
check, and the new readiness contract through the existing soak-runner check.
The contract exercises the actual capacity-only runner guard as well as missing,
uncollected, measured-zero, stale, wrong-identity and offset-preserving samples;
shared bounded reads, partial records, timeout, watcher exit and a waiting-to-ready
transition are covered.

## P10-02 matrix-c readiness and watcher shutdown guard

The fresh `p10-02-capacity-matrix-c-soak-50` invocation, launched from
`b9613ef1b582d1d04081ce214f4563ee98d4197c` with the same game image as matrix-b,
passes natural-only heap readiness after 4,048.207 seconds. The readiness journal
ends at 2026-09-20 11:27:56.3256432 UTC with actual completed-GC indices on all
three services; no forced collection or allocation pressure was used. Its workload
starts at 11:28:13.8962836 UTC and ends two hours later (terminal result below). At the
halfway checkpoint, all fifty subjects have qualifying activity in each of four
completed activity windows, and all three services have ninety available heap
observations in each of the first two post-warm-up windows. Those were interim
read-only checks, not terminal acceptance. P10-02 remains unchecked.

A separate runner audit finds #95: `Stop-Watcher` previously accepted a watcher
that had already exited with code zero before the owning runner requested its
stop. A process-double regression executes the actual PowerShell helper and fails
on that early-clean case before the fix. The helper now rejects observed early
exits and forced termination regardless of exit code, while preserving the code
and disposing/clearing its handle so the outer `finally` remains idempotent.
Normal success, normal nonzero exit propagation, early zero/nonzero exits and
forced termination are covered by `test-stop-watcher.ps1`, invoked through the
existing mandatory `test-run-soak.ps1` check. No subprocess is started or killed by
the contract test. This is harness infrastructure with no Java gameplay analogue.

The already-running fifty-subject invocation keeps its original helper and
binaries; the watcher process is independently confirmed live during the audit.
Later matrix populations load the updated runner and record their actual source
revision. This guard is not proof of continuous watcher responsiveness and does
not relax the final raw-problem, workload, economy or telemetry gates.

Shutdown-guard validation passes: full solution 4,303 tests / 24 explicit skips,
warning baseline 4,243, and all CLAUDE.md ancillary checks (including the new
actual-helper contract through `test-run-soak.ps1`). Validation uses a separate
build output root, not the active capacity binaries. This change contains no
gameplay code or upstream automation changes and does not rebuild a Docker image.

## P10-02 build-revision capture

Audit #96 finds that the LIVE runner queried Git HEAD after service/heap readiness,
even though it built its bot and watcher tools before starting the stack. A commit
during the long readiness interval could therefore mislabel those tools. The runner
now captures HEAD before preparation/build, checks it again after both tool builds
and before Docker startup, and passes the captured revision into bot metadata.
Readiness-time commits do not relabel already-built binaries. Build-time revision
changes, malformed revisions and failed Git lookups fail closed.

`test-live-build-provenance.ps1` fails against the former ordering and passes after
the correction. It checks the actual build/start ordering and executes the real
revision-reader, build guard and bot-argument construction with mocked Git results;
it performs no build, Git mutation, or Docker operation. The existing mandatory
`test-run-soak.ps1` invokes this contract. This is harness infrastructure with no
Java gameplay analogue, not a gameplay or capacity-policy change.

This records the checkout's commit identity, not an immutable snapshot of dirty
working-tree files, nor proof of a reused Docker image's source. Matrix-c's
fifty-subject invocation recorded `b9613ef1b` before subsequent commits;
its metadata and binaries are not rewritten. Later populations load the corrected
runner and retain the revision captured for their own builds. P10-02 stays unchecked.

Provenance-fix validation passes: full solution 4,303 tests / 24 explicit skips,
warning baseline 4,243, and every CLAUDE.md ancillary check. The new contract runs
through the existing mandatory soak-runner check. No gameplay code, Docker image,
active capacity binary or upstream automation was changed.

## P10-02 matrix-c: fifty subjects accepted; 200/500 pending

`run/p10-02-capacity-matrix-c/p10-02-capacity-matrix-c-soak-50` passes the complete
`p10-02-acceptance-v2` policy: runner, workload, economy, telemetry and problems
all pass, with no acceptance failures. Independently invoking the analyzer against
the retained raw sources recomputes the result without rewriting any report.
The owning Full runner records `soak-50` as Passed and advances to 200. This is
one accepted population, not a completed capacity matrix or Phase 10.

Bot source is `b9613ef1b582d1d04081ce214f4563ee98d4197c`, seed 73, with the same
game image as matrix-b (`sha256:1f38a4c1c4e5f45832ae7ba8eff67c98ecdd562d9091c7f04fb5d9df5217559d`,
production `83836fe47`). After the separately journaled 4,048.207-second natural
heap preflight, the measured window is 2026-09-20 11:28:13.8962836–13:28:13.8962836
UTC, exactly 7,200 seconds. In-flight work and cleanup finish at 13:30:54.8868181;
the owning LIVE invocation succeeds at 13:31:01.3183430. The owner removes its
isolated Docker stack and preserves the evidence. No local MySQL is used.

- All 50 subjects pass replay and final inventory/quit/offline checks; all eight
  fifteen-minute activity windows have progress. The run completes 4,737 cohort
  actions. Twenty eligible subjects persist their race-specific starter journeys
  once, then continue other activities (D16); no completed quest is reset.
- Economy passes all four fixed-prefix/whole-stream tests: 3,337 craft attempts
  and 716 gathers, twenty subjects per activity, twenty prefix samples per subject,
  and zero impossible outcomes. Ordinary production randomness remains enabled.
- All ten PvP traces record natural Kisk retirement, a second binding, and another
  completed PvP cycle (61 for b39/b40, 62 for the others). As an independent raw
  packet example, b39 receives Kisk 135799's final one-second update, removal
  notice and deletion at 13:28:24.037 UTC, performs its own ordinary ten-second
  item cast, and receives creator-matched 72-charge/7,200-second Kisk 147229 plus
  `SM_BIND_POINT_INFO` at 13:28:34.386. Cycle 61 completes at 13:29:46.367.
  Replacement occurs during in-flight completion, outside the measured window;
  it is lifecycle evidence, not extra measured soak duration. No forced expiry,
  timed GM revival or replenishment is used.
- Enforced watcher: one existing suppressed startup fingerprint; zero new,
  known, regressed or repeated problems. Its bounded cache retains 3,264 records /
  777,372 characters. No allowance is added.
- All three servers pass telemetry with 720 heartbeat samples each and ninety
  available last-GC heap observations in each of six post-warm-up windows. These
  snapshots may repeat between collections; they are not 720 independent GCs.
  Game-server timer medians are 908.5, 915, 904.5, 907, 905 and 905.5. Its working-set
  endpoint growth is 2,973,696 bytes; last-GC heap endpoint growth is 278,104 bytes.
  Maximum heartbeat gaps are 11.171 s (login), 10.219 s (chat), and 10.027 s (game).
- Of 2,028,664 game dispatch observations, p99 upper bound is 0.5 ms, p99.9 is
  10 ms and maximum is 1,110.8702 ms; oldest pending age is 0.1409 ms. This is
  request-to-buffer-preparation latency, not network RTT. Two separate-output
  host validation cycles ran during the measured window for the watcher/provenance
  fixes; this is not a pristine isolated-host benchmark, and no causal attribution
  of the latency maximum is claimed. Active game/bot binaries stayed unchanged.

The 200-subject child starts natural heap preflight at approximately 13:32 UTC,
using tools built from `ed923ff651e0b3c95d61d6b900a5168a47acddbf` (also verified
from `Aion.LiveBots.dll` ProductVersion) and the same inspected game image. At this
recording checkpoint its measured workload has not started; 500 has not started.
The 200/500 acceptance gates, later Phase 10 TODOs, and the five-consecutive-Full
completion condition remain outstanding. The open object-ID lifecycle gap (#93)
is not closed by this bounded passing run. Matrix-b remains failed and unchanged.

Evidence-record validation: full solution 4,303 passed / 24 explicit skips;
warning baseline 4,243; all CLAUDE.md ancillary checks pass, including fidelity,
quest/compiler drift, sweep/telemetry/acceptance and runner/retention contracts.
Checks use the separate validation output root, leaving active 200-subject tools
untouched. This commit is documentation only; it neither changes gameplay nor
closes P10-02.

## P10-02 scaled gathering supply correction

A read-only audit during matrix-c's 200-subject heap preflight finds #97 before
500 starts. Each Poeta channel would enroll twenty gatherers, requiring 400
attempts under the existing twenty-sample-per-subject policy. The former 150 m
radius around the vendor rendezvous includes only four shipped Young Aria nodes.
Even instantaneous casts and travel allow only `4 * 3 * (1 + floor(7200 / 295))`
= 300 attempts in the window. One final in-flight attempt per subject raises the
upper bound to only 320. The old search also approached a shipped spot only once,
then waited on its known list instead of exploring other available locations.

Java source `GatherableController.completeInteraction` and
`GatheringTask.onInteractionFinish` at `ce54b7931` confirms per-attempt consumption;
the shipped template/spawn files supply three uses and 295-second respawn. These
server mechanics are unchanged. A new supply regression fails with radius 150
(`4 spots supply at most 300, need 400`) and passes with the revised 300 m search,
which includes 27 Poeta and 12 Ishalgen candidates. This optimistic bound catches
impossible enrollment; it does not predict actual harvest throughput.

The finite search itinerary rotates its starting choice across subjects sharing
a channel, skips coordinator-known busy/cooling spots, and continues exploring
when initially known nodes cannot be acquired. The availability query is only an
advisory hint: no unseen object is reserved or invented. Actual interaction still
requires a visible gatherable and the existing exclusive lease. Unreachable
candidates are rejected for that attempt; ordinary cancellation fails the run.
Travel and return use the existing graph/local/journey collision checks, with
packet draining between bounded movement segments and death checks. There is no
straight-line fallback, teleport, extra spawn, shortened cooldown, or statistical
policy change. Exploration traces do not count as successful gather outcomes or
fill the sustained-activity acceptance windows.

Nine focused tests pass, including the opt-in real-geometry test: both active hubs
have at least eight checked round-trip nodes from both initial subject offsets.
Other tests cover 50/200/500 enrollment bounds, rotated search, busy versus
unreachable candidates, exclusive ownership, and cooldown/respawn availability.
The geometry test also retains the alternate Poeta hub's existing route check.
The running 200-subject invocation is not hot-patched; its tools remain at
`ed923ff65`. Future populations rebuild from the new source. Revised LIVE routing
and 500-subject throughput still require runtime evidence; P10-02 stays unchecked.

Pre-commit validation passes: 4,307 solution tests / 24 explicit skips, warning
baseline 4,243, all CLAUDE.md ancillary checks, and Docker Fast 6/6 (37.1 seconds).
Separate validation outputs preserve the active matrix binaries. No production
source, Docker image, upstream automation, or allowance changes.

## P10-02 gather500-a failure and assertion-oracle isolation

`run/p10-02-gathering-diagnostics/p10-02-gather500-a`, source `d6a0a3d38`, seed 73,
uses the same game image as matrix-c on separate ports/build outputs. It prepares
all 500 subjects for a 600-second Gather/Vendor/Relog diagnostic, then fails after
18.994 seconds (2026-09-20 14:00:32.0338074–14:00:51.027898 UTC). The owning script
exits 1 and removes only its isolated stack. The failed window, logs and launch
provenance remain. This proves neither the revised gathering loop nor capacity.

Pool-acquisition timeouts first appear at 14:00:37.124, during the initial relog
burst. The restricted activity selection leaves 300 subjects with only Relog,
so this is a concentrated diagnostic load, not the full mixed workload's shape.
The first bot assertion failure is an admin player-state HTTP 404 at 14:00:50.087.
The server lookup logs a DB timeout and returns null; its HTTP handler maps null
to not-found. This is not evidence that a character was actually deleted.

Source audit #98 finds an important interference risk. Both Java and C# configure
five DB connections and a 5,000 ms acquisition timeout. `PlayerDAO` holds the
player-row connection while `PlayerCommonData.SetExp` calls `UpdateDaeva`; for a
non-starting class that can load quests using another connection. Five concurrent
outer reads can therefore occupy all slots while waiting for nested reads. The
C#-only admin endpoint is called concurrently by the bot oracle. No runtime wait
graph was captured before the owning failed-run cleanup, so the exact allocation
of all five connections at failure remains unproven. The Java-shared nested
connection lifetime remains open; it is not silently rewritten as a parity fix.

`LiveAdminClient` now keeps session-owned connection pooling but shares one
cancellable read-only request gate across the LIVE bot process. The gate covers
full response buffering. Cancellation while queued cannot release another caller's
permit; active cancellation and faults release it. HTTP error responses are not
retried or converted into success. All existing oracle call sites use the wrapper;
game packets, per-cohort scheduling and ordinary server SQL stay unchanged. This
isolates assertion traffic, not gameplay workload. It is not a DB concurrency fix
or proof of general HTTP endpoint capacity. Two tests fail against ungated request
delegation; all four focused gate/connection-reuse tests pass after correction.

Fingerprint triage (no new allowance):

| Fingerprints | Evidence and disposition |
|---|---|
| `ab16d694`, `31ac73fc`, `367d981e`, `96a8a5ff`, `09c0c5b0`, `2e9a8bbe`, `72c95289`, `528a4f09`, `1c6594e8`, `b4947b74`, `46ec4a02`, `8a9b3f9f` | Logged pool-acquisition timeouts in effects, quest, inventory, cooldown, player-state and character-count reads/writes. Track under #98; genuine failed persistence/load operations, not benign content. |
| `0d086b61` | Slow CM_QUIT execution during the same timeout interval. Track with #98; retain the warning and unchanged threshold. |
| `ebe67ab3` | Existing generic bot-failure fingerprint reused for the HTTP-404 assertion. Six HTTP exceptions plus 526 mirrored/cancelled-step records are retained; they are not 532 independent root causes. This run remains failed. |
| `3ef73bfe`, `4d999c25`, `72e453eb` | Raw cleanup errors after population cancellation: null connection in leave-world and duplicate abyss-rank INSERT. Separate open #99; exact lifecycle race still needs reproduction. No silent null/duplicate handling is added. |
| `231c488f` | The unchanged, already-owned startup content allowance. No new suppression. |

The watcher terminal summary reports 569 observations: one suppressed, twelve new,
one regressed and 555 repeated. Server logs continue during stack cleanup, so the
complete raw GS problem stream contains additional post-summary fingerprints; the
summary is not presented as a complete shutdown census. The raw evidence above
and generated ledger changes are retained. The 200-subject matrix preflight remains
live on its original binaries and has no new logged problem at this checkpoint.
Future runs load the oracle gate; revised LIVE validation is still required.

Pre-commit validation passes: full solution 4,310 tests / 24 explicit skips,
warning baseline 4,243, and every CLAUDE.md ancillary check. Four focused tests
exercise the gate and existing connection reuse. No production code, Docker
image, upstream automation or allowance changes; validation uses a separate
output root from the live capacity process.

## P10-02 gather500-b login pool divergence

`run/p10-02-gathering-diagnostics/p10-02-gather500-b`, clean source `99b88d1e2`,
seed 73, repeats the isolated 500-subject/600-second Gather/Vendor/Relog diagnostic
with the assertion gate. All subjects prepare; the workload starts at
2026-09-20 14:13:26.732180 UTC and fails at 14:14:01.4085839 UTC (34.676 seconds).
The owning script exits 1 and removes its isolated stack. This is failed diagnostic
evidence, not capacity acceptance. Its concentrated 300 relog-only subjects differ
from the full mixed workload.

No new GS/CS fingerprint appears in this short invocation. Login-server pool
timeouts begin at 14:13:59.180 UTC; b116 then receives unexpected login EOF.
The raw LS stream contains four fingerprints, each triaged as a bug under #100:

| Fingerprint | Count | Failing operation |
|---|---:|---|
| `9525cc21` | 3 | Update account time during successful login |
| `9bf40a7b` | 8 | Load account time while the account-row connection is still held |
| `ad257c69` | 238 | Acquire the outer account lookup connection |
| `373a2ce4` | 1 | Update last server during GS account authentication |

The existing generic bot-failure fingerprint `ebe67ab3` records the disconnect
and cancellation fallout; it does not identify independent root causes. The watcher
reports 671 observations: one suppressed, four new, one regressed and 665 repeated.
No new allowance is added. There are 181 exploration events, including 80 Poeta
events beyond the former 150 m radius, but only two completed gathers. This is
insufficient to validate revised gathering throughput.

Source comparison confirms a C#-specific nested pool checkout: Java's AccountDAO
returns after closing its query resources, then AccountController loads account
time. C# had combined both operations inside the first connection's lifetime.
The fix closes that scope before the time lookup; SQL, returned fields, missing
account behavior, null-time rejection, pool size, and production timeouts stay
unchanged. No login/cohort throttle is introduced.

A dedicated Docker MySQL 8.4 regression submits thirty simultaneous name, ID and
external-auth lookups with pools of one and five connections. Both cases reproduce
the pool timeout before the fix and pass afterward, verifying account/time values
and missing-account results. The initial attempt ran before MySQL was ready and
is retained separately, not counted as the reproduction; the ready-container RED
log is `run/p10-02-login-pool-red-ready.log`. All seven login DB integration tests
pass after correction, including the real encrypted login socket handshake.

The existing matrix-c 200-subject preflight is untouched on its original binaries
and image. It cannot validate this correction. A rebuilt login image and a new
scaled diagnostic are still needed, along with complete 200/500 acceptance runs.
P10-02 remains unchecked.

Pre-commit checks pass: 4,310 solution tests / 26 explicit skips (including the two
new Docker-only cases, separately executed successfully), warning baseline 4,243,
all CLAUDE.md ancillary checks, and Docker Fast 6/6 in 36.4 seconds. Validation
uses the separate stats output root and does not replace the active matrix tools.

## P10-02 gather500-c outward-only routing failure

The replacement diagnostic uses tools and a rebuilt test login image from
`ac08359f8508ce9ac4cb0af37e8868f471a8056a`:
`sha256:18c4130be3cfc46f1a1b75c85ecee505d72eabdff322d6e5508eee5fb62b3329`.
The game image remains matrix-c's `1f38a4c1c4e5f45832ae7ba8eff67c98ecdd562d9091c7f04fb5d9df5217559d`.
`run/p10-02-gathering-diagnostics/p10-02-gather500-c` retains command, ports and
image provenance. All 500 subjects prepare. The workload runs from
2026-09-20 14:25:19.9234991 to 14:26:09.7025097 UTC (49.779 seconds), then fails.
All LS/CS raw problem streams are empty; GS contains only the existing startup
allowance. This short result does not prove sustained pool health.

The first printed error is attributed to b324, but paired operations share their
failure: b323's trace has the successful gather, followed by failed return planning.
b324 is still gathering. b323 explored Ishalgen node `(480.537,2787.35,295.073)` on
channel 2, reached ground Z `295.0508`, then completed one successful harvest at
14:26:04.905. The bounded graph/local/journey searches could not find the return
to `(577.529,2817.34,303.613)`. These planners do not promise symmetric reachability.
The earlier geometry test required eight usable nodes, but runtime did not apply
that round-trip condition to every candidate it admitted.

`SoakGatheringRoute.FindReturnablePath` now applies that condition to both
exploration hints and observed-object approaches. It probes from the outward
path's normalized final position to the original rendezvous, even after intervening
exploration. A missing return rejects that candidate for the current gathering
attempt; no movement, forced return, unchecked edge reversal or altered geometry
budget is substituted. The final return is still freshly planned and can fail if
conditions change. Existing offline dynamic-world limitations remain.

The run records six gather outcomes, not adequate exposure. Watcher summary:
453 observations, one suppressed, zero new/known, one regressed, 451 repeated.
The generic `ebe67ab3` records two mirrored route exceptions and 450 cancellations,
triaged under #101; no new allowance. Its isolated stack was removed by the owner;
raw evidence remains. Matrix-c's 200-subject preflight continues untouched.

Two focused regressions fail with the previous outward-only behavior. All eight
focused tests pass after correction, including the full-geometry captured-node
regression and at least eight eligible round-trip nodes per active starter hub
from both initial offsets (plus the alternate Poeta hub's two-node check).
No production code or server image changes in this routing correction.
Pre-commit validation passes: 4,313 solution tests / 26 explicit skips, warning
baseline 4,243, and every CLAUDE.md ancillary check. The opt-in geometry test is
included in the eight separately executed focused tests, not counted as a full-suite pass.

## P10-02 gather500-d clock discontinuity and entry-refusal evidence

`run/p10-02-gathering-diagnostics/p10-02-gather500-d` uses clean tools at
`bdd488dc9`, the corrected login image from `ac08359f8`, and the unchanged game
image recorded for c. All 500 subjects prepare; the 600-second diagnostic starts
2026-09-20 14:35:40.5286425 UTC and fails at 14:38:00.4180898 UTC. Monotonic duration
is 136.0655742 seconds, but wall duration is 3.8238731 seconds longer. Its clock
consistency gate is correctly false. This is not accepted diagnostic/capacity evidence.

Windows System event 59188 (`Microsoft-Windows-Kernel-General`, ID 1) records
`OldTime=2026-09-20T14:37:45.6232524Z`,
`NewTime=2026-09-20T14:37:49.4476147Z`, `TimeDeltaInMs=3824`, process `svchost.exe`.
Two following events (59191/59193) are sub-millisecond adjustments. No agent action
changed the clock or time service. This corroborates a real host-clock discontinuity;
it does not by itself prove the server's exact refusal branch or DB timestamp at
the failed entry. Do not weaken the timing/clock gates or retroactively accept d.

b157 and b158 receive `SM_ENTER_WORLD_CHECK` message 6 at 14:37:56.622 and
14:37:56.650 UTC. Java source confirms this means reentry delay (including a still
online DB row), and the C# checks match. Their previous quit sends are at
14:37:45.571/45.572; b157's received quit response straddles the clock adjustment.
Persisted last-online values at refusal were not retained, so causal attribution
beyond the observed discontinuity/refusals remains open. The runner's fail-fast
cleanup removed only its isolated Docker stack; raw logs/traces remain.

Seventy gathers across 69 subjects completed before failure. No new raw server
problem appears: LS/CS are empty; GS has only the existing startup allowance.
The watcher reports 452 observations (one suppressed, zero new/known, one regressed,
450 repeated). Critically, its 451 bot problem rows are cancellation fallout:
the primary entry refusals were printed only to stderr because the step wrapper
assumed `LiveBotFailureException` was already recorded (#102).

The packet reader now records entry refusal before propagating it. Codes 1–6 retain
their original exception, bot/account/step and full stack; zero/unrelated packets
emit nothing. One regression fails with six expected records versus zero before
the correction and passes afterward through the actual step wrapper, which does
not duplicate the records. No production change, refusal retry or new allowance.

## P10-02 matrix-c 200-subject terminal failure

The existing 200-subject run completes natural-only heap preflight after 4,026.5
seconds and prepares all subjects. Its measured window is
2026-09-20 14:40:06.5540749–14:40:26.0744068 UTC (19.5202607 seconds), with a
consistent clock. It fails during initial relogs on the original, unpatched login
image: `9525cc21` (3), `9bf40a7b` (7), `ad257c69` (36), `373a2ce4` (1) are the
same pool-timeout fingerprints as gather500-b/#100. No additional GS/CS problem
appears beyond the owned startup allowance. This is not a failure of the corrected
login image, which was never injected into this invocation.

The watcher reports 237 observations: one suppressed, four new, one regressed,
231 repeated; its original snapshot classified these fingerprints as new. The
owning matrix exits 1 and removes only its test stack. Its 500-subject stage never
starts. Matrix-c's accepted fifty-subject evidence remains intact; the full matrix
is **not** accepted. Replacement 200/500 runs must use current tools/corrected login
image and record their own provenance, without relabelling any failed run.

Pre-commit validation for the refusal-ledger correction passes: 4,314 solution
tests / 26 explicit skips, warning baseline 4,243, all CLAUDE.md ancillary checks,
and the focused six-code regression. No gameplay, server image, timing gate or
upstream automation change.

## P10-02 gather500-e/f Windows socket failure

Both diagnostics use source `9a7a8c3d3`, the corrected login image from `ac08359f8`,
and the unchanged game image. Each prepares 500 subjects for the same 600-second
Gather/Vendor/Relog selection: 200 gatherers, 300 relog-only subjects. This is a
concentrated reconnect workload, not the full mixed life policy.

| Run | Workload start/end (UTC, 2026-09-20) | Duration | First failure | Gather outcomes |
|---|---|---:|---|---:|
| `p10-02-gather500-e` | 14:48:24.5491965–14:52:16.8001395 | 232.2508567 s | b136, 14:52:06.487, opening login socket | 196 across 175 subjects |
| `p10-02-gather500-f` | 14:57:43.1797496–14:58:54.1867903 | 71.0070044 s | b40, 14:58:50.032, opening game socket | 8 across 8 subjects |

Both throw Windows `SocketException` 10055 from
`Socket.WildcardBindForConnectIfNecessary`. Both clocks are consistent (wall/
monotonic differences 0.0000863 / 0.0000363 seconds). Their owners exit 1 and
remove only their own Docker stacks. LS/CS raw problem logs are empty; GS has
only the existing startup allowance. Watcher summaries are respectively
458/457 observations, one suppressed, zero new/known, one regressed and 456/455
repeated. Generic bot fingerprint `ebe67ab3` retains the primary socket errors
and cancellation fallout under open #103, not a new allowance.

The first post-e host snapshot at 14:52:47 has 4,600 TIME_WAIT rows, including
1,743 client connections to login, 502 to game and 121 to admin. That is after
cleanup, not a failure-time peak. Windows reports dynamic TCP ports 49152–65535;
excluded intervals within this range total 660 ports. Queries found no System
events 4227/4231/2004 in the relevant recent interval. `MaxUserPort`, `MaxFreeTcbs`,
`MaxHashTableSize`, `TcpNumConnections` and `TcpTimedWaitDelay` have no explicit
values in the queried TCP parameters key; absence is not a measured limit.

For f, one-off local read-only samplers retain `host-sockets.jsonl` (28 snapshots)
and `bot-process.jsonl` (17 snapshots), with explicit PID/start-time identity,
timestamps and bounded lifetimes. They capture aggregate TCP state/target-port
counts and resource usage, not packet contents or unrelated remote addresses.
The first sampler's process rows are dotnet wrappers; the second follows actual
`Aion.LiveBots.exe` PID 60528. Observed maxima: 5,112 TCP rows, 2,346 distinct
ephemeral local ports, 2,185 bot handles, 2,014,556,160 bot private bytes,
1,641,574,400 nonpaged pool bytes; observed available memory never falls below
21,328,646,144 bytes. Sampling gaps and post-failure cleanup mean these are not
continuous maxima or proof of the exact failing resource.

These results do **not** establish ordinary port-range exhaustion or an unclosed
socket leak. [Microsoft's troubleshooting guidance](https://learn.microsoft.com/en-us/troubleshoot/windows-client/networking/tcp-ip-port-exhaustion-troubleshooting)
likewise distinguishes TIME_WAIT churn from confirmed exhaustion. No host settings,
port ranges, close semantics, buffers, gameplay concurrency or refusal checks are
changed on a speculative diagnosis. An optional Docker-hosted Linux load generator
is implemented later below, keeping Host as the default. This is harness
infrastructure, not a maintainer-selected replacement or a Windows root-cause fix.

## P10-02 full-mixed 500-subject timeout diagnostic

`p10-02-mixed500-a` restores all ten activities with the same source `9a7a8c3d3`,
corrected login / unchanged game images, seed 73 and 600-second duration. The
only tracked dirty file at launch was the generated problem ledger; documentation
was edited subsequently. All 500 subjects prepare. Workload starts at
2026-09-20 15:03:21.7745674 UTC and fails at 15:03:51.8067951 after 30.0321938
monotonic seconds (clock drift 0.0000339 seconds). No gathering outcome completes.

The first recorded problem at 15:03:37.003 is `No matching cast start within 10
seconds.` on b328's enclosing paired step. The actual caster is b327, which sends
`CM_TARGET_SELECT` and `CM_CASTSPELL` at 15:03:23.598 after both reciprocal duel
start packets. Its final received packet is an unrelated `SM_MOVE` at that same
timestamp. Across all bot traces, 15:03:24 contains five records, seconds 25–33
contain none, and activity resumes at second 34. GS heartbeats continue at seconds
28/38 with packet queue depth zero, no pending dispatcher writes and maximum
completed dispatch latencies 151.9183 / 19.3789 ms. This is a clue for load-generator
stall diagnosis, **not proof** of thread-pool starvation, a server cast defect, or
the exact location of the missing response. No production or timeout change is
made on this evidence.

Bot problems contain one timeout and 487 cancellation records, no socket error.
LS/CS raw problem logs are empty; GS contains only the existing startup allowance.
Enforced watcher reports 489 observations: one suppressed, zero new/known, two
regressed, 486 repeated. The owner exits 1 and removes only its Docker stack.
Retained read-only `host-sockets.jsonl` has 24 samples: maximum 2,263 TCP rows /
1,143 distinct ephemeral ports and minimum 20,877,230,080 available bytes. Its
process rows describe dotnet wrappers, not actual bot apphost resource usage.

This failed diagnostic does not replace e/f or satisfy the two-hour capacity gate.
Independent matrix-d 200 remains in natural heap preflight on its original output
directory; 500 has not started. Diagnostic/sampler and validation overhead overlaps
that preflight, not a completed capacity measurement. Evidence checkpoint checks
pass: 4,314 solution tests / 26 skips, warning baseline 4,243, and every CLAUDE.md
ancillary check. No gameplay or upstream automation change.

## P10-02 bounded LIVE navigation work

The next full-mixed 500-subject diagnostic, `p10-02-mixed500-b`, launches on clean
source `7c7dfaaeb` with unchanged images/workload/seed and its own Docker stack.
It retains its original binaries while the queue correction is developed.
Locally installed diagnostic tools `dotnet-stack` / `dotnet-counters` version
10.0.745401 observe only its identified `Aion.LiveBots.exe` PID 8284 (start ticks
639255139128259403), not the capacity matrix's processes. Ten stack reports are
taken at 15:12:46.999–15:13:15.066 UTC, two seconds apart plus collection time.
Runtime counters and five-second process samples are bounded. Stack collection
and concurrent validation add overhead; this is not an uninstrumented benchmark.

Reports 2–8 contain respectively 30/28/28/27/33/30/28 synchronous
`BotNavigationGeometry.FindGroundPath` stacks. Report 3 has 57 reported threads,
28 ground-search stacks and 28 `DespawnableNode.IsActive` frames, all its active
worker-pool dispatch stacks in route computation. The modern runtime counter tool
exports thread-count/queue-length observations as **rates**, so those values are
not reported as absolute thread/queue sizes. Completed-work-item rates fall to
zero in multiple consecutive early samples. A separately started legacy
EventCounters capture provides absolute thread/queue sizes only from later in
the run. These are evidence of the unbounded navigation scheduling hazard; they
do not prove the exact cause of mixed500-a's missing cast response.

Java `game-server/.../geoEngine/scene/DespawnableNode.java:isActive` at `ce54b7931`
uses the same synchronized instance-read shape. Do not change that production
behavior to optimize the offline client. `LiveNavigationWorkQueue` instead limits
only LIVE starter quest/gather CPU route work to four concurrent calls. Excess
requests await a semaphore rather than starting further synchronous searches on
socket/timer worker threads. All original graph/local/journey search budgets and
collision checks still run; gathering's outbound-plus-return validation occupies
one slot, and its final return is freshly checked. Failed/canceled queued search
releases an acquired gathering lease. No packet/action concurrency, population,
activity schedule, timing gate, geometry result or server image is changed.

Tests exercise held slots, nonblocking queued admission, cancellation without
executing a queued search, exception identity and slot recovery after cancellation
inside a search. A mutation increasing available slots makes the admission test
fail (`Assert.False`, actual true); restored bound passes both new tests and three
existing return-route regressions. Logs: `run/p10-02-nav-work-queue-{red,green}.log`.
Pre-commit checks pass: 4,316 solution tests / 26 explicit skips, warning baseline
4,243, and all CLAUDE.md ancillary checks. Full logs are
`run/p10-02-nav-work-queue-{warning,fulltests}.log`. No gameplay or upstream script
change. Scaled replay with the corrected runner remains required.

## P10-02 profiled mixed500-b outcome and coverage reporting

The unmodified runner in `p10-02-mixed500-b` completes its planned ten-minute
workload window, then fails at 2026-09-20 15:23:50.6097912 UTC after 664.2083176
seconds including in-flight work/cleanup. Clock drift is 0.0000684 seconds.
The first failure is cohort 96's all-selected-activities check, not a socket or
cast timeout. b191's recorded schedule is Quest (15:12:47), CrashDisconnect
(15:20:48), Vendor (15:21:11), Group (15:21:14), Gather (15:21:16). Its Q1 journey
persists once at 15:20:44; the remaining Trade/Duel/Relog activities never start
before the window expires. Do not reset quests or narrow the activity list to
make this diagnostic pass. A longer diagnostic is needed; the two-hour capacity
requirement remains unchanged.

Retained traces show 97 gathering outcomes across 97 subjects, 2,099 crafts and
fourteen persisted finite quest journeys. Economic evidence is explicitly
`insufficient`, with zero impossible outcomes. No new server error: LS/CS problem
logs are empty, GS has only startup allowance `231c488f`. Enforced watcher records
248 observations: one suppressed, zero new/known, one regressed, 246 repeats.
The owner exits 1 and removes only its Docker stack. The process sampler peaks
at 2,072 handles, 69 threads and 2,576,105,472 private bytes; these sampled maxima
do not prove absence of transient native resource pressure. Both runtime counter
sessions finish normally when the bot exits.

All 247 bot problems were cancellation fallout: the primary coverage exception
was printed only on stderr. Finding #105 corrects this independent reporting
gap. Each cohort now traces its completed activity counts, and a failed check
writes one primary `activity-coverage` record at `soak-coverage`, attributed to
that pair's first bot/account, listing the sorted missing names and original
stack before triggering cancellation. Complete coverage is silent; empty
coverage is rejected. No success conversion, exception suppression or allowance.
The two-case regression fails when the write is removed, then passes when
restored; `run/p10-02-coverage-problem-{red,green}.log` retains both outcomes.
Pre-commit validation passes: 4,318 solution tests / 26 explicit skips, warning
baseline 4,243, and every CLAUDE.md ancillary check. Logs:
`run/p10-02-coverage-problem-{warning,fulltests}.log`. Harness-only correction;
no production behavior, image, timeout or upstream automation change.

## P10-02 full-mixed socket failure and optional Docker bot execution

`p10-02-mixed500-c` launches from clean `3e3d63446`, seed 73, all ten activities,
500 subjects and a 1,800-second diagnostic window. Corrected login image
`18c4130be3cf` and unchanged game image `1f38a4c1c4e5` are retained. Its workload
starts at 2026-09-20 15:31:43.9516483 UTC and fails at 15:39:30.2170675 after
466.2653841 seconds; drift is 0.0000351 seconds. The first recorded failure,
b120 at 15:39:25.245, is native Windows socket error 10055 while reconnecting
through `TcpBotTransport.ConnectAsync`/`WildcardBindForConnectIfNecessary`.
The paired b119 row reports the same failure, not proof of two native failures.
Bot problems contain two socket rows and 472 cancellation rows. LS/CS are clean;
GS has only startup allowance `231c488f`. Watcher totals: 475 observations,
one suppressed, zero new/known, one regressed, 473 repeated. Economic exposure
is insufficient with zero impossible outcomes. The owner removes only its stack.

Three stack reports, 26–34 seconds after workload start, each show four ground
searches, consistent with the new gate. This does not prove the exact original
stall cause or resolve #103. `bot-legacy-counters.csv`, `bot-process.jsonl`, raw
traces and failed-window evidence remain under the diagnostic run directory.

LIVE, Soak and Full runners now accept `-BotExecution Docker`; omission remains
Host. The optional `bot-runner` Compose profile builds the same C# bot code into
`aion-bots-runner:local`, mounts repository data read-only and that run's artifacts
writable, and uses numeric internal LS/GS/CS addresses with their native ports.
No extra game server, proxy, protocol change, retry, timeout relaxation or host
network tuning is introduced. The helper remains alive while `compose exec`
returns the actual bot process exit code; the host watcher drains before owned
stack shutdown. Container failures are not allowlisted. The runner verifies
container identity, exclusive project network, running state and captured image
revision, and writes `bot-execution.json`; bot metadata records OS/framework.
Server image reuse still obeys `-SkipImageBuild`; the bot image always builds.
Rebuilding server images must not rebuild the client without its revision label.

Contract tests cover endpoint/provenance rejection, workload forwarding,
Host/Docker selection, startup image-build policy and exit codes 0/1/137.
Three C# cases pin inherited IPv4/IPv6 and distinct service endpoints; fifteen
focused endpoint/options tests pass. The Docker smoke attempt `docker-smoke-a`
was an operator error (unsupported combined `connect,L0` selection); it correctly
returned bot exit 2 and removed its stack. It is failed evidence, not a pass.
`p10-02-docker-smoke-b` then passes L0 with two subjects, including chat, on
Ubuntu 24.04.5 / .NET 10.0.12. Enforced watching sees only the existing startup
allowance (one suppressed, no new/known/regressed/repeated problem); cleanup
removes all five owned containers. Both smoke runs used the uncommitted runner
changes atop `3e3d63446`, not an attested clean source revision. Their actual
image IDs and endpoints are retained in `bot-execution.json`.

Pre-commit checks pass: 4,321 solution tests / 26 explicit skips, warning baseline
4,243, every CLAUDE.md ancillary check, and the new Docker runner contract included
by `test-run-soak.ps1`. Logs: `run/p10-02-docker-runner-{focused,warning,fulltests}.log`.
No gameplay or upstream automation changes. This smoke validates the optional
execution path, not scaled workload behavior or two-hour acceptance.

Matrix-d 200 retains its original host binaries and begins the two-hour window
at 2026-09-20 15:55:26.1761525 UTC, scheduled to end 17:55:26.1761525. Docker smoke
builds/runs and local validation overlap this measured window; observed resource
and latency results include that overhead. Its 500 stage has not started.
P10-02 remains open; the accepted fifty-subject result is from matrix-c and does
not establish a uniform accepted matrix on the newer runner/images.

## P10-02 quest kill credit surviving corpse decay

The first Docker full-mixed diagnostic, `p10-02-mixed500-docker-a`, runs clean
`2b443a2fc`, 500 subjects, all ten activities, seed 73 and 1,800 planned seconds.
All subjects prepare. Workload starts 2026-09-20 16:04:16.7809703 UTC and fails
at 16:07:22.7847435 after 186.0110014 monotonic seconds (drift -0.0072282 seconds).
This is a harness quest exception, not a socket failure or accepted capacity.
Traces retain 79 gathering outcomes from 79 subjects and 483 craft outcomes from
197 subjects; no finite quest journey reaches its final persistence check.
LS/CS have no problems and GS only its existing startup allowance. Enforced
watching records 484 observations: one suppressed, zero new/known, one regressed,
482 repeated. The owner exits 1 and removes its five-container Docker project.
Independent matrix-d 200 remains running on its original host binaries.

b352's trace is decisive: target 141840 is NPC 210133; at 16:07:21.572 it receives
Q1102 status 3 / kill counter 1, then target-specific loot-enable at .584 and
`SM_DELETE` at .960. Its next cast iteration at 16:07:22.021 indexes the absent
NPC. `SM_DELETE` correctly removes both the object and current loot status.
Java `NpcController.doReward` invokes quest kill credit before drop registration;
`MonsterHunt.onKillEvent` updates the quest counter; `DropRegistrationService`
enables loot even for empty drops; `RespawnService.scheduleDecayTask` uses a
two-second empty-corpse delay. All references are at `ce54b7931`. The packet-drain
timestamps are receipt times, not the server's exact scheduling intervals.

Finding #106 fixes only the LIVE harness. For pure kill objectives, completion
requires the exact active quest counter; excess credit fails before another cast.
Item objectives still require that target's loot-enable plus the existing item
and inventory checks. A target disappearing without sufficient evidence fails
explicitly rather than indexing the dictionary. No retained corpse, guessed death,
quest reset, retry, timeout relaxation, production change or new allowance.

Six regressions cover the observed credit/loot/delete sequence, next-objective
credit, missing/wrong/completed quests, excessive credit and target-specific item
loot requirements. The former loot-only logic fails five cases; the corrected
logic passes all six plus three existing quest tests (one explicit geometry skip).
Logs: `run/p10-02-quest-kill-credit-{red,green}.log`. A new scaled replay is required;
the failed diagnostic and matrix-d binaries are never reinterpreted or hot-patched.
All pre-commit checks pass: 4,327 solution tests / 26 explicit skips, unchanged
4,243-warning baseline and every CLAUDE.md ancillary check. Full logs:
`run/p10-02-quest-kill-credit-{warning,fulltests}.log`. This is a harness-only
correction; no gameplay or upstream automation changes.

## P10-02 terminal replays and expanded quest collection

Matrix-d 200 is now **failed**, not still running. Natural heap readiness took
4,052.068 seconds. Its measured window starts 2026-09-20 15:55:26.1761525 UTC
and terminates at 16:25:34.9662644 after 1,808.7900451 seconds; clock drift is
0.0000668 seconds. The 500-subject stage never starts. Source remains `9a7a8c3d3`,
login image `18c4130be3cf`, game image `1f38a4c1c4e5`; no hot-patching occurred.
Concurrent local validation/diagnostics are included in its observed performance.

The slow subject is b154 (its paired b153 finished and persisted at 16:04:46).
b154 starts Q2104 at 15:58:20.469, gets baskets at 16:18:44.771, 16:23:24.922
and 16:23:42.950, completes it at 16:23:57.236, then finishes Q2105 and starts
Q2100 at 16:24:49.728. The unchanged 1,800-second quest deadline expires during
that final leg. Its channel is the intended index 0, not an accidental shared
fallback. Enforced watcher: 189 observations, one suppressed, zero new/known,
two regressed and 186 repeats. Owner cleanup removes only this run's stack.

Finding #108: all three former Q2104 hints cover only the same three baskets
within the 30 m selection radius. Eight quest subjects per Ishalgen channel at
population 200 need 24 items: even ideal use requires seven 295-second respawns
(2,065 seconds), exceeding the whole journey's deadline before travel. The shipped
zone has 32 baskets; Poeta likewise has 32 grain sacks. Spawn files match Java
byte-for-byte, and Java `RespawnService.RespawnTask.respawn` at `ce54b7931` uses
the original spawn template. No new content or faster respawn is necessary.

The harness now rotates through all shipped collection hints, distributing
initial positions across subjects on each channel. Hints are not object ids or
reservations: only actually observed objects may be leased. Exploration checks
outward travel and a return from the grounded endpoint to the quest NPC under
the existing four-search bound; unavailable paths are rejected. Movement still
drains packets and enforces collision/speed rules. Three required items, exact
inventory/rewards, finite completion and relog persistence remain unchanged.
Real-geometry integration passes in 3m51s, requiring at least 24 returnable spots
in each zone. This proves route availability, not scaled runtime throughput.
Unit cases pin the old impossible supply, all 32 hints, twenty distinct initial
positions, cycling/rejection and invalid inputs. Validation logs:
`run/p10-02-quest-search-{green,geometry,warning,fulltests}.log`.
All pre-commit checks pass: 4,332 solution tests / 27 explicit skips, unchanged
4,243-warning baseline and every CLAUDE.md ancillary check. The added skip is
the separately executed real-geometry test. This is a harness-only correction;
no production gameplay or upstream automation files change.

Docker replay b uses clean `385526021`, all ten activities, 500 subjects, seed 73,
1,800 planned seconds and the same server images. It validates #106 in live
packet sequences for both races: b122 receives Q1102 counter 3 / loot-enable /
delete before validating objective 3 at 16:21:41.323; b153 does the equivalent
for Q2102 at 16:21:14.183. However the run fails after 252.4143781 seconds at
16:22:37.3749019 UTC (drift -0.010462) on a separate ten-second game-socket connect
timeout (#107). b331 has no new key after quit, while b332 receives its key and
enters world before reporting the pair's propagated exception. GS heartbeats
continue with zero packet queue and pending writes. This does not identify the
failed connect's cause. Watcher: 481 observations, one suppressed, zero new/known,
two regressed, 478 repeats. All five owned containers are removed.

Instrumented Docker replay c keeps the same bot revision and server images;
the quest-search edits are not in its image. Its 16:29:10.6779188 UTC window fails
at 16:37:35.7726182 after 505.1147897 seconds (drift -0.0200903), not on the prior
game-socket timeout: b110's offline persistence HTTP request is reset while writing
(socket 104); b109 propagates the same exception (#109). There are two HTTP rows
and 486 cancellation rows. LS/CS problem logs are empty, GS has only startup
allowance `231c488f`. Watcher: 489 observations, one suppressed, zero new/known,
one regressed, 487 repeats. Owner exit is 1; its five containers are removed.
The maintainer's `aion-mysql` and unrelated `evejs-market-1` remain untouched.

Read-only five-second Linux `/proc/net` samples in both bot and game containers
show zero listen overflows/drops, SYN retransmissions, TCP timeouts, memory aborts
and backlog drops. Aggregate close/data-abort counters rise, but cannot identify
this request or distinguish expected crash-disconnect activity. Bot runtime
counters near failure show an empty worker queue; neither sampling result proves
absence of a transient problem. HTTP idle/reuse behavior is only a hypothesis.
The source, sampler commands, container identities and overhead are recorded in
the run's `launch-provenance.md`; counters and raw network snapshots are retained.
Local validation overlaps this diagnostic, but matrix-d's workload had already
ended. No host tuning, forced GC, new allowance, retry, deadline change or weakened
acceptance gate is introduced. Findings #107/#109 remain open, and all three
failed runs stay failed. P10-02 remains unchecked.

## P10-02 assertion-HTTP idle-boundary reproduction

Finding #109 remains distinct from the unproven game-socket connect timeout (#107).
The C# admin endpoint uses `HttpListener`. In the exact deployed .NET 10.0.12
[managed listener implementation](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Net.HttpListener/src/System/Net/Managed/HttpConnection.cs),
`BeginReadRequest` arms a fifteen-second timeout after the first response;
`OnTimeout` closes the socket. This creates a possible race with reuse of a
longer-idle client pool entry. No Java implementation exists for this infrastructure.

The isolated `run/p10-02-http-idle-probe/` experiment runs the real `LiveAdminClient`
against a loopback Linux listener in the bot runtime image, with `--network none`
and no database or game server. Two hundred session-owned clients serialize GETs
through the existing oracle gate. Eight rounds schedule reuse after 14.8–15.2
idle seconds. The original client reports socket-104 resets at 16:47:27.509 UTC
(id 7, round 3, measured idle 14,998.8381 ms) and 16:47:42.396 (id 1, round 4,
15,002.3482 ms). Both occur in HTTP read-ahead, whereas the LIVE failure was on
write; this proves the idle-boundary hazard, not exact attribution of the LIVE
connection. The original probe subsequently reports a 100-second HTTP timeout
and is explicitly stopped (exit 143) before completing all rounds. Its partial
output is failed diagnostic evidence, never an accepted run.

A comparison changes only `PooledConnectionIdleTimeout` to ten seconds and
completes all 1,800 requests without failure (`bounded.jsonl`). The implementation
now uses that bound by default for the assertion HTTP pool. It retains immediate
connection reuse, full response buffering under the global read gate, the normal
request timeout and transport/status failure propagation. No retry, suppression,
allowance, game-socket change, server timeout change or host tuning is added.
The maintainer's database is not involved. Probe containers auto-remove and raw
results remain on disk; the corrected-client probe uses its own published output,
not an overwritten running binary. Local probes/builds overlap the active
200-subject diagnostic, so it is not an uncontended capacity benchmark.

The initial loopback regression keeps the server connection open, performs four
reads, waits eleven idle seconds, then performs four more. The old client fails
because it uses one TCP connection instead of two; the revised client passes in
isolation. However the first full-suite run exposes the test's timing assumption:
pool cleanup is periodic, not guaranteed at exactly eleven seconds. The final
test awaits actual client-initiated EOF before continuing, bounded by its overall
thirty-second deadline; the server never initiates idle closure. A second case
verifies all eight immediate reads still share one connection. Existing gate,
cancellation, body-buffering and status tests pass unchanged (five focused cases
total). Logs: `run/p10-02-http-idle-{red,green,green-observed,fulltests}.log`; the
initial full-suite failure is retained, not silently retried as a pass.
This does not hot-patch the
already-running `p10-02-mixed200-docker-quest-a` image at `5f8da2f84`, which still
uses the original client. Scaled LIVE replay remains required.
The separately published revised default client also completes 1,800 requests
without failure (`corrected.jsonl`, terminal 2026-09-20 16:53:13.0954655 UTC).
That run supplies no injected handler; it exercises the actual corrected default.

The first warning rebuild cannot copy the host watcher's loaded DLL and fails
with MSB3021/MSB3027 (`run/p10-02-http-idle-warning.log`). Do not stop that watcher
or call this pass. Re-run the same baseline script with SDK `ArtifactsPath` /
`UseArtifactsOutput` pointed at the existing isolated validation directory;
this changes only build output placement, not the warning inventory or baseline.
Final validation passes: 4,333 solution tests / 27 explicit skips, warning baseline
4,243, and every CLAUDE.md ancillary check. Logs:
`run/p10-02-http-idle-{warning-final,fulltests-final}.log`. This is a harness-only
HTTP pool change; no production gameplay or upstream automation file changes.

At 2026-09-20 16:58:13 UTC the unchanged active 200-subject diagnostic has all
forty Asmodian subjects through Q2104, with per-subject durations 76.258–415.964
seconds (mean 208.540). b154 takes 97.612 seconds instead of its prior 25-minute
collection wait. The bot problem ledger is empty at that snapshot. This is direct
collection-throughput evidence for #108 at 200 subjects, not terminal success,
the whole finite journey's acceptance, or evidence for the HTTP change absent
from that image. Its thirty-minute workload still ends no earlier than 17:15:52 UTC.

## P10-02 completed 200-subject workload, failed probe isolation

`p10-02-mixed200-docker-quest-a` finishes its full mixed workload on source
`5f8da2f84`: 2026-09-20 16:45:52.8927586 UTC to scheduled end 17:15:52.8927586,
then in-flight work/final checks complete at 17:18:24.0713233. Monotonic elapsed
time is 1,951.2547717 seconds; drift -0.076207 seconds is within the unchanged
clock check. There are 4,130 cohort actions, 3,070 craft attempts, 458 gathers,
eighty finite quest journeys with relog persistence, and all 200 subjects finish
inventory/offline checks. The independent `--soak-evidence` replay passes and
hashes all input files (`soak-workload.json`); `CapacityConfiguration` and overall
acceptance remain false. Economy exposure is insufficient with no impossible
outcomes. Telemetry correctly rejects a thirty-minute window for two-hour capacity.

The **overall invocation fails**, exit 1, on enforced watching: four observations,
one suppressed, one new fingerprint `00c8849f`, two repeats. The three process
events occur at 16:50:14.239, 16:50:32.940 and 16:53:13.163 UTC, corresponding to
the bounded HTTP probe's exit, the explicit stop of the original HTTP probe, and
the corrected HTTP probe's exit. No bot problem or new GS/LS/CS problem is logged.
The actual workload container remains running until all subjects finish.

Finding #110 is an operator/probe isolation error, not a gameplay bug: the probes
used the Compose-built bot image with `--network none` but did not override its
inherited `com.docker.compose.project` / `.service` labels. The watcher follows
project-scoped Compose events, so those containers were labeled as its bot-runner.
The former image has since been removed by its owner; inspection of matrix-e's
replacement image independently confirms the Compose project/service labels.
The process-event timestamps and probe terminal logs are retained; raw engine
events are no longer available from Docker's bounded history. Do not invent their
missing actor payloads, remove the recorded fingerprint, or declare this run green.

Future standalone probes must use a clean base image or override **both** labels
with an independent probe identity before starting. A control container using
project `aion-probes-p10-02`, service `http-idle-probe` and no network is inspected
before execution, exits 0, and is removed by its exact verified id. It does not
enter matrix-e's event stream. No container-death allowance or watcher weakening
is added. The failed run's owner removes its five containers; maintainer
`aion-mysql` and unrelated `evejs-market-1` remain untouched. Workload/quest evidence
is useful but does not replace a fully passing invocation.

Replacement `p10-02-capacity-matrix-e` starts from clean `119205fff` with Docker
execution, seed 73, and 200 then 500 subjects for 7,200 seconds each, fail-fast.
It uses corrected LS image `18c4130be3cf`, unchanged GS `1f38a4c1c4e5`, and bot
image `44c65a2abd0b` for its first child. Ports and build outputs are independent
of the completed diagnostic; startup/preflight overlap and validation overhead
are disclosed in `run/p10-02-capacity-matrix-e/launch-provenance.md`. Natural heap
readiness is pending at this checkpoint; no measured capacity result exists yet.
The earlier accepted matrix-c fifty-subject result is retained separately, not
relabeled as new-revision evidence. All acceptance gates remain unchanged.

Pre-commit checks pass: 4,333 solution tests / 27 explicit skips, warning baseline
4,243 and every CLAUDE.md ancillary check. Logs:
`run/p10-02-quest200-terminal-{warning,fulltests}.log`. This checkpoint changes
evidence/triage documentation only. P10-02 remains unchecked.

## P10-02 repeated Docker game-socket timeout

`p10-02-mixed500-docker-d`, clean source `e537f6626`, includes both the expanded
quest collection search and the assertion-HTTP idle bound. All 500 subjects prepare;
the ten-activity workload starts at 2026-09-20 17:27:46.821841 UTC and fails at
17:29:16.6802641 after 89.8622702 monotonic seconds. Clock drift is -0.0038471
seconds. This is a failed thirty-minute diagnostic, not capacity acceptance.

The first timeout rows at 17:29:12.274 belong to b366 and b36: game-socket
`TcpClient.CompleteConnectAsync` exceeds the unchanged ten-second deadline.
The surrounding pair traces b365/b366 and b35/b36 all end after quit responses
at 17:28:33.863–17:28:36.001, without a new SM_KEY. Nested pair-step reporting
does not identify which individual socket failed; do not equate the row's bot
label with the originating connection. Finding #107 is reproduced, not fixed
by the independent HTTP mitigation. No HTTP reset occurs in this diagnostic.

Bounded, read-only probes execute inside the run's own bot and GS containers,
not extra Compose-labeled containers. Five-second namespace samples contain
zero ListenOverflows, ListenDrops, TCPSynRetrans, TCPTimeouts, TCPAbortOnMemory
and TCPBacklogDrop, with no sampled SYN_SENT entry. These observations narrow
the investigation but do not prove a TCP handshake or exclude transient events
between samples. Near failure the bot has 63–65 worker threads, fluctuating
queue length (2–63 in the inspected 17:28:55–17:29:12 interval), CPU about
12–19 percent and GC time 3–8 percent. Neither host exhaustion nor a global
worker-pool stall is established. No pool, timeout, workload or host tuning is
applied. Further connection-event/stack evidence is needed before a causal fix.

At 17:29:02.263 the GS heartbeat records 445 connections, packet queue zero,
2,851 armed timers, fifteen pending writes with oldest age 0.2952 ms, and
maximum completed write latency 11.0398 ms. LS/CS problem logs are empty;
GS contains only its existing startup allowance. Enforced watching correctly
fails: 491 observations, one suppressed, two regressed fingerprints and 488
repeats. Mirrored pair errors and cancellation fallout are not independent
root causes. All raw traces, counters, namespace samples, failed window and
launch provenance remain under `run/p10-02-gathering-diagnostics/p10-02-mixed500-docker-d`.

The owner exits 1 and removes its five containers. Matrix-e's idle heap
preflight overlaps this diagnostic; its original binaries and stack remain
untouched. Neither the maintainer's Docker MySQL nor unrelated containers are
changed. P10-02 remains incomplete.

## Maintainer population cap and stopped matrix

On 2026-09-20 the maintainer requested a maximum of ten bots during testing.
Apply this across concurrent runs and include setup/director connections in the
limit. Larger-population testing is deferred (D17), not accepted or silently
redefined as a ten-subject capacity check.

The final 500-subject diagnostic `p10-02-mixed500-docker-e` had already failed:
workload 17:39:27.0597211–17:41:45.6035293 UTC, elapsed 138.5498811 seconds,
drift -0.0060729 seconds, first reported game-connect timeout on paired step
b236 at 17:41:42.505. Its owner removed all five containers. Socket tracing
completed with zero reported lost events: 2,373 starts/stops and one
ConnectFailed event. This first trace omitted activity-flow enablement, so
do not pair asynchronous connects by thread or claim a bot-specific origin.
Periodic stack snapshots show many workers in synchronous trace-file flushes,
alongside four navigation searches and idle workers. This is an observed cost,
not yet a causal diagnosis of #107. Raw diagnostic artifacts are preserved;
the attempted second, activity-correlated trace found the container already
gone and collected no evidence. No further large diagnostic was started.

Matrix-e's 200-subject stage was still in natural heap preflight when the request
arrived. Signaling its watcher stop file causes readiness to fail closed and
the owner to remove its isolated stack; the 500 stage never starts. No subject
workload or capacity result exists for this invocation. Missing workload files
in its terminal analysis reflect this intentional preflight cancellation, not
a gameplay failure. `run/p10-02-capacity-matrix-e/maintainer-stop.md` records the
reason. Docker verification afterward shows only the untouched maintainer
`aion-mysql` and unrelated `evejs-market-1`; no bots remain running.

Pre-commit warning baseline passes at 4,243. The first solution run fails one
login loopback test at native listener bind with Windows socket error 10055;
retain `run/p10-02-socket-repeat-fulltests.log`. A subsequent host snapshot has
388 TCP entries, about 21 GiB available physical memory and the unchanged
16,384-port range; this does not establish port exhaustion or the exact cause.
No unrelated application is stopped and no host setting is changed. All 22
login socket smoke tests pass on a focused recheck; the complete solution
recheck passes 4,333 tests with 27 explicit skips. This does not erase the
earlier failure or close Windows finding #103. Every CLAUDE.md ancillary check
also passes. Logs: `run/p10-02-socket-repeat-{warning,loginfocused,fulltests-recheck}.log`.
This checkpoint changes documentation/triage only, not gameplay or acceptance gates.

## P10-03 bounded crash-watching foundation

Under D17, larger capacity runs stay deferred and P10-03 proceeds with a planned
small-population lifecycle test. This checkpoint adds supporting watcher
infrastructure only; no server is killed, no bot is launched and the TODO remains
unchecked. Production gameplay and the existing normal watcher mode are unchanged.

The opt-in `--expect-game-server-crash true` mode accepts one fresh, strict-schema
plan for the exact isolated project and full container id. The watcher publishes
an atomic SHA-256 arming receipt; the future controller must verify it before
injection. Death must be observed within thirty seconds of arming, with exit 137;
the same container must start and emit a fresh GS heartbeat within three minutes.
Only the observed-death-to-recovery GS heartbeat gap is expected. Other services
remain watched and ordinary GS heartbeat checks resume on recovery. Missing
plans/events, early termination and expired recovery fail closed. Expected events
are separately visible in the digest/summary, not fingerprint allowances.

Retained raw Compose events show that Compose removes its project/service labels
from the attributes object. `DockerFollowers` now carries its explicitly selected
project identity on event records; the matcher still requires the full planned
container id and service. No container-name inference or broad project allowance
is used. Normal watchers do not opt in merely because a plan file exists.

Forty new deterministic tests cover identity, freshness, deadline bounds,
malformed plans, one-use death matching, exit code, OOM, missing timestamps,
restart/heartbeat ordering, receipt hashing, missing recovery, other-server
heartbeat failures and post-recovery monitoring. Tests include previously tracked
process/server fingerprints: this opt-in mode also fails unallowlisted KNOWN
problems, so the tracked generic container-death fingerprint cannot conceal an
additional crash. The focused watcher suite passes 53 cases. One initial test
incorrectly expected separate NEW heartbeat fingerprints for LS and GS; the
existing shared fingerprint correctly makes the second observation REPEAT.
The corrected test asserts both server observations and total count two.

Remaining P10-03 work is the scoped Docker fault controller, saved-state bot
assertions, actual mid-session kill/restart/relogin and duplicate-login journey,
plus LIVE evidence and orchestration integration. Source review for that next
step uses Java `ce54b7931`: `PeriodicSaveConfig` / `PlayerEnterWorldService`
schedule general/items persistence every 900 seconds; `AccountController.login`
returns ALREADY_LOGIN (7) on the duplicate attempt while requesting the prior
session's kick, and `LoginServer.kickAccount` sends
`STR_KICK_ANOTHER_USER_TRY_LOGIN`. A subsequent fresh authentication must be
tested, not assumed to succeed on the duplicate attempt itself. No Java runtime
comparison is performed.

Final pre-commit validation passes: 4,373 solution tests / 27 explicit skips,
warning baseline 4,243, and every CLAUDE.md ancillary check. Logs:
`run/p10-03-crash-expectation-{focused-final,warning-final,fulltests-final}.log`.
No gameplay or upstream automation file changes; no new allowance or baseline increase.

## P10-03 owned Docker controller

The next infrastructure checkpoint adds `scripts/live/lifecycle-controller.ps1`.
Its runner owns a child process, drains stdout/stderr concurrently, watches the
log watcher and enforces a 25-minute total deadline. It reads only the selected
character row through the isolated Docker MySQL service. The first observed row
must retain the pre-movement position; a later row must contain the new position
while still online. A second movement request must continue that saved checkpoint,
and SQL must still contain the checkpoint immediately before fault injection.
No save interval is shortened and no SQL state is changed.

The fault controller checks the exact project, full container id, service label,
exclusive project network and image. It publishes one fresh plan, requires the
watcher's exact hash receipt, and rechecks identity/owner before SIGKILL. It
requires exit 137 without OOM, starts only that container, and requires a later
process start plus fresh GS startup/LS registration messages. Docker's log
ingestion cutoff excludes the first boot; the watcher still independently
requires matching death/start events and a new GS heartbeat. Failed/partial
recovery never emits a success receipt.

`test-lifecycle-controller.ps1` intercepts every Docker command. Tests cover
selection failures, malformed position/identity requests, changed containers,
missing/wrong receipts, lost owner, kill/start failure, wrong exit/OOM, stale
process/log evidence, missing registration, SQL failure/ambiguity, early child
exit, failed launch and process cleanup. Tiny real artifact-producer processes
exercise the complete controller sequence and SQL-gate failures; they are not
bots, servers, or a substitute for a real watcher integration. Two defects found
by these tests were corrected: JSON-decoded timestamps must retain subsecond
precision, and completed stream-copy tasks must not leak result objects into
the returned exit code. No production Java/C# divergence is introduced.

The controller is not yet wired into `run-live.ps1`. O1's protocol bot, natural
900-second save, actual Docker crash/relogin, duplicate-login assertion and LIVE
evidence remain to be implemented/verified. P10-03 stays unchecked. No Docker
container was started, stopped or deleted for this checkpoint, and no bots ran.

Pre-commit checks pass: 4,373 solution tests / 27 explicit skips, warning
baseline 4,243, 431 controller contract assertions and every other CLAUDE.md
check applicable to this infrastructure-only change. Logs:
`run/p10-03-controller-{contract,warning,fulltests}.log`. No gameplay or
upstream-automation files changed; no baseline or allowance was increased.

## Scope decisions

- P10-05 and siege/housing-dependent journeys remain deferred under D7.
- P10-06 Java runtime comparisons are explicitly deferred under D15. Java remains
  the source and golden-fixture reference; no Java server is started.
- P10-07 needs real **4.8** client protocol captures, not just extracted geodata.
- D17 caps current testing at ten concurrent bots total; larger capacity runs
  require explicit renewed authorization.
- A green orchestration contract does not prove a two-hour populated soak, five
  consecutive Full runs, or natural autonomous player progression.
