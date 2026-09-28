# Mau pilot Phase 1: encounter and reproducibility audit

## Status, 2026-09-27

The **two full area legs' reproducibility check passes** for development seeds
12, 16, and 20, each run twice in a fresh SIM process. The comparison is saved
as [machine-readable evidence](bot-learning-phase1-comparison.json). All 12
traces, test logs, summaries, completion receipts, and `course-reset.json`
snapshots remain under `run/bot-learning-phase0/current/phase0-*-phase1-snapshot-*`
and `run/bot-learning-phase1/current/phase1-*-ordered-v1` in this checkout.
The comparison lists each exact path and SHA-256 digest. Held-out evaluation
outcomes were not opened.

The Phase 1 gate now passes for the two full legs and six short encounter starts.
The six starts each repeated seed 12 twice against module
`687c9b38-2490-4449-940e-a05ad59af55d`, with matching reset hashes and
meaningful outcomes. Their decision streams also match after removing the
run identifier. The [short-start comparison](bot-learning-phase1-encounters-comparison.json)
lists each trace, reset hash, module ID, source/data hashes, and outcome. The
blocked rejoin fails reproducibly; this is a preserved baseline failure, not a
successful course. No model was trained and no held-out evaluation outcome was
opened.

## Reset and audit boundary

Each focused test process creates a fresh database, world, virtual clock, and
Priest. SIM now spawns maps in map-ID order instead of parallel during server
bootstrap. Production keeps the Java reference's parallel spawning. Within a
map, the ordinary shipped spawn templates and AI/timer registration are used.
At the course boundary, the harness restores the locked level 9 Priest skills,
equipment, inventory including potion quantities, HP/MP, quest state, and
effective bind; casts Blessing through the normal client protocol; teleports
for setup; verifies the resulting client packets; then resets the seeded RNG.
The snapshot requires zero RNG draws after this reset.

`course-reset.json` records the active seed and draw count; all NPCs in the
Priest's Ishalgen instance, including their spawn and current positions,
HP/death, AI state, walk step and movement target; all armed virtual timers in
deadline/registration order; and the Priest's items and skill/item cooldowns.
The full snapshot hash is also written into the JSONL trace and checked when a
summary is generated. These are server-side setup diagnostics, never bot
observations. After setup the existing journey uses ordinary client packets,
pull planning, combat timing, navigation, and collision checks.

## Repeated development runs

| Course | Seed | Repeats | Completion / deaths / disengaged | Game seconds, each run | Course wall seconds, pair |
|---|---:|---:|---|---:|---:|
| Generator to Rae | 12 | 2 | yes / 0 / yes | 721.129 | 35.786, 35.933 |
| Generator to Rae | 16 | 2 | yes / 2 / yes | 2552.675 | 124.671, 125.044 |
| Generator to Rae | 20 | 2 | yes / 1 / yes | 1565.049 | 103.221, 102.525 |
| Rae to Hatata | 12 | 2 | yes / 0 / yes | 134.640 | 4.689, 4.881 |
| Rae to Hatata | 16 | 2 | yes / 0 / yes | 135.341 | 4.685, 4.739 |
| Rae to Hatata | 20 | 2 | yes / 0 / yes | 132.040 | 4.710, 4.633 |

Every pair has the same byte-level reset snapshot hash, module ID
`4b72fdd4-7d1b-4309-8d01-cc498ee4a86c`, code/data hashes, completion,
deaths, terminal quest result, terminal disengagement, game time, maximum
observed attackers, potion count, and route-failure count. Each snapshot
contains 1,321 Ishalgen-instance NPCs. Generator starts have 2,473 armed
timers; Hatata starts have 2,472. Generator seed 12 is a useful regression:
the Phase 0 same-seed probes had varied in death count and elapsed game time.

The course portion took 4.6–5.0 wall seconds for Hatata and 35.8–125.1 wall
seconds for the generator leg. End-to-end `dotnet test` durations reported by
the logs were 6, 37, 104, or 126 seconds, depending on the seed/course. This
measures cost before selecting a later training budget; no model was trained.

## Six short encounter starts

Each start uses the locked level, learned skills, gear, inventory, bind, and
quest state from the Phase 0 courses. The setup selects a shipped NPC and
clears unrelated nearby aggressive spawns when needed. The moving patrol is
warmed for five virtual seconds before the course boundary. Hatata with an add
despawns the original nearby Stalker at its shipped spawn and creates one
single-time Stalker of that template beside Hatata, so the add stays in the
encounter rather than returning to its original home. The exact spawn and
timer state is in each `course-reset.json`. After setup, all six starts use the
normal journey's client packets, pull planner, combat policy, movement, and
collision checks.

| Start | Seed-12 repeats | Outcome each run | Game seconds each | Course wall seconds, pair | Decision records each |
|---|---:|---|---:|---:|---:|
| Isolated Stalker | 2 | complete, 0 deaths, 1 max attacker | 20.216 | 0.51, 0.58 | 11 |
| Two-attacker pull | 2 | complete, 0 deaths, 2 max attackers | 91.745 | 1.66, 1.57 | 33 |
| Moving patrol | 2 | complete, 0 deaths, 1 max attacker | 26.327 | 0.99, 1.08 | 14 |
| Blocked generator-to-Rae rejoin | 2 | failed, 0 deaths, 11 route failures | 60.276 | 1.74, 1.79 | 81 |
| Hatata alone | 2 | complete, 0 deaths, 1 max attacker | 43.854 | 0.69, 0.77 | 20 |
| Hatata with add | 2 | complete, 1 death, 2 max attackers | 1079.298 | 35.75, 35.42 | 142 |

The moving target is NPC object 54706, template 210407, with walker ID
`B2244F1915135A32F46D55216A0023A6A83A6E63`; its client `SM_MOVE` appears
at virtual time 7.001 seconds before engagement. The blocked rejoin fails at
`ni07-q2007-rae-return`: the normal route search cannot find a collision-checked
path to a client-observed Rae at the twelve shipped spawn hints. Both traces
preserve that exception and the 11 route failures. The read-only dashboard was
enabled during every run. The short-start course portion ranges from about
half a wall second to 36 seconds; fresh `dotnet test` process/database startup
adds overhead, as the saved test logs show.

## Decision records

`scripts/sim/trace/make_mau_decisions.py` turns `combat-decision` and
`pull-plan` trace events into `training-decisions.jsonl`, one record per event.
Each record has the observed client state, candidate actions and their observed
eligibility, chosen action and baseline rejection reason, policy version, seed,
trace line, and eventual encounter result where an encounter actually ended.
The summary holds the decision file's SHA-256 and record counts. Combat attempt
start/end markers identify the observed kill, death/revive, or error for the
executed sequence. An unchosen action gets no outcome label. Pull records name
the firing spots evaluated before the baseline ranking cutoff; spots not
evaluated by the planner are not claimed legal. Navigation-dependent combat
actions remain marked for a separate checked-route test. Pull waits link to a
later client-observed fight with the selected target; if no such fight follows,
the record stays unresolved. No-plan records state that no checked encounter
was reached. Every record in these twelve short-start traces has a linked
observed encounter result.
These limits are part of the record schema, not assumed success labels.

## Differences and limits

Wall time and trace SHA-256 differ across repetitions because trace timestamps
and run identifiers differ. The checked reset and meaningful outcome fields do
not differ. This is evidence for the three development seeds and the exact
recorded build, not a guarantee for every seed or future code change. The
read-only monitor was enabled at `http://127.0.0.1:17880/` during each run.

To repeat the development comparison with a newly built focused test binary:

```powershell
pwsh -NoProfile -File scripts/sim/run-mau-phase1.ps1 -Course GeneratorToRae -Seeds '12,16,20' -Repeats 2 -Tag new-build
pwsh -NoProfile -File scripts/sim/run-mau-phase1.ps1 -Course RaeToHatata -Seeds '12,16,20' -Repeats 2 -Tag new-build
```

Use new tags because existing runs are immutable. The Phase 1 runner accepts
only frozen development seeds and preserves failures. Compare the resulting
run directories with `scripts/sim/trace/compare_mau_phase1.py`.
