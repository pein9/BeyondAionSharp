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

Default `SOAK` requests **all** activities and currently rejects the unimplemented
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

## P10-02 finite quest workload (LIVE validation pending)

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
Fresh twenty-minute run `p10-02-quest10-c` is in progress. Whole-cohort persistence,
scheduler retirement and the final run outcome remain unproven.

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
fails closed.

## Scope decisions

- P10-05 and siege/housing-dependent journeys remain deferred under D7.
- P10-06 Java runtime comparisons are explicitly deferred under D15. Java remains
  the source and golden-fixture reference; no Java server is started.
- P10-07 needs real **4.8** client protocol captures, not just extracted geodata.
- A green orchestration contract does not prove a two-hour populated soak, five
  consecutive Full runs, or natural autonomous player progression.
