# Mau pilot Phase 0 lock and reference evidence

The machine-readable course lock is [`bot-learning-phase0-lock.json`](bot-learning-phase0-lock.json).
It freezes the current checkout at `32449aaa4c714593ec6987bea25aadc90342bf2e`, the
Ishalgen spawn file and Java quest references by SHA-256, two level-9 Priest
starting states, and disjoint seed sets. The saved states come from successful,
client-observed `retreat72` seed 5. They include the *observed* skill list and
item counts; level alone is never used to invent a learned skill. The observed
bound-obelisk revive in seed 1 (source trace line 119874, `SM_PLAYER_INFO`) places
the Priest at `(571.0388, 2787.342, 299.875)`.
The lock's `status` field records its original freeze-time state and remains
unchanged so its SHA-256 continues to match the focused traces.

| Course | Fixed start from seed 5 | Quest state | Terminal success |
|---|---|---|---|
| Generator → Rae | `(627.9615, 893.3795, 309.85568)`, heading 24 | Q2007 START/6, before the blue generator | Blue and violet generators used; Rae 203554 reached by a checked route, Priest alive and every pursuer disengaged, no stalled recovery. |
| Rae → Hatata | `(643.058, 912.591, 311.08878)`, heading 88 | Q2129 START/1, Strongfur objective already done | Hatata objective update received, Priest alive and every pursuer disengaged, no stalled recovery. |

Each course has a 3,600-second game-clock and 900-second wall-clock cap. A cap,
death, route exhaustion, or lack of observed disengagement is a recorded outcome.
The two quest states are intentional: Java's
`_2007WheresRaeThisTime` advances the generator variables 6–8, while Java's
`monster_hunt` template for Q2129 records the Strongfur kill before Hatata.
The saved inventory, equipment, HP/MP and skills are in the JSON lock. The
focused SIM runner creates a fresh Priest and applies those states, then checks
the client-observed position, heading, quest variable, skills, gear, inventory,
HP and MP before each course. `NaturalJourneyCheckpoint` records the receipt;
it is not a world snapshot.

The locked split is regression seeds **1, 3, 4, 5**, development seeds
**11–30**, and unseen evaluation seeds **101–120**. Tuning must use only the
development set. Every candidate is compared with the deterministic policy on
paired seeds; regression and evaluation outcomes remain separate. The
`recovery69` seed-5 Q2006 Mijou return (320 wall seconds) is a separate route-cost
regression and is excluded from these Mau scores.

## Archived deterministic-policy observations

`python scripts/sim/trace/phase0_mau_archive.py` extracts the local intervals
from the existing `run/natural-batch/retreat72-full-s{1,3,4,5}` traces. Its
output is `run/bot-learning-phase0/archive/manifest.json` and eight JSONL slices
with JSON summaries. The summaries give the original trace SHA-256, line
interval, build and module ID, plus the extracted trace SHA-256. The JSONL
records retain original virtual time, wall timestamp, step, packet direction,
packet name and fields. The script neither runs a server nor alters the source.

| Course | Seed | Game s | Wall s | Deaths | Max observed attackers | Retreat attempts | Potions |
|---|---:|---:|---:|---:|---:|---:|---:|
| Generator → Rae | 1 | 1,565 | 84 | 0 | 2 | 0 | 18 |
| Generator → Rae | 3 | 1,358 | 72 | 1 | 2 | 0 | 17 |
| Generator → Rae | 4 | 1,355 | 62 | 1 | 2 | 0 | 13 |
| Generator → Rae | 5 | 1,518 | 83 | 1 | 3 | 1 | 19 |
| Rae → Hatata | 1 | 3,047 | 152 | 3 | 5 | 1 | 18 |
| Rae → Hatata | 3 | 1,962 | 93 | 2 | 4 | 2 | 8 |
| Rae → Hatata | 4 | 177 | 6 | 0 | 2 | 0 | 4 |
| Rae → Hatata | 5 | 102 | 4 | 0 | 1 | 0 | 3 |

The summaries also retain client-observed HP decreases, attackers relative to
the latest combat-decision target, actions by encounter and per-step outcomes.
That attacker list is a diagnostic proxy; `maxObservedAttackers` is the direct
policy observation. In particular,
the blue-generator step remains separate from later Q2007 rejoin steps. The
older `timing68` seed-3 guarded-rejoin exhaustion and its blue-generator
multi-attacker pull remain failure references; the successful `retreat72` slices
do not erase them. A short isolated Hatata kill is not the course score.

## Frozen current-policy baseline

The Phase 0 trace gate is complete. `docs/bot-learning-phase0-baseline.json` is
the audit manifest for **88 focused runs**: both courses on all 20 development,
four regression, and 20 held-out evaluation seeds. It records development and
regression outcomes and trace checksums. The evaluation entries contain only
seed, run identity, file path, and trace/summary checksums; their outcomes stay
sealed for the later held-out comparison. The full traces, test logs, summaries,
completion receipts and failure packages are in
`run/bot-learning-phase0/current/phase0-{generator|hatata}-s{seed}-locked-v1/`.

| Split | Generator → Rae | Rae → Hatata |
|---|---|---|
| Regression 1/3/4/5 | 4/4 complete; three deaths across runs | 4/4 complete; zero deaths |
| Development 11–30 | 18/20 complete; failures at 16 and 20; 24 deaths across runs | 20/20 complete; zero deaths |
| Evaluation 101–120 | 20 traces and summaries verified; outcomes sealed | 20 traces and summaries verified; outcomes sealed |

Every `locked-v1` run uses build
`1.0.0+32449aaa4c714593ec6987bea25aadc90342bf2e`, module ID
`6796cc28-a2b1-4bc7-b261-711c6af4ca32`, and the same recorded source and
spawn-data hashes. The deterministic combat-policy source SHA-256 still matches
the original lock. The verifier checks each trace hash and record count, frozen
position/heading, quest, HP/MP, skills, inventory and full equipped slots,
game/wall caps, terminal quest update and disengagement receipt or failure
package. The checkpoint's 16-bit equipment slot omits slot 65536; the verifier
reads that full slot from the client inventory packet. Run the audit with:

```powershell
python scripts/sim/trace/verify_mau_phase0_baseline.py
```

The focused runner creates one fresh SIM world and Priest per seed, checks the
locked character state and effective bind fallback, resets the process RNG at
the course boundary, uses the current pull planner and
`NaturalPriestCombatPolicy`, and refuses seed numbers outside the frozen split
or existing run directories. It preserves failed runs. To run a new reserved
seed after building the simulation test project, use:

```powershell
pwsh -NoProfile -File scripts/sim/run-mau-phase0.ps1 -Course RaeToHatata -Seeds '11,12,13' -Tag another-version
```

Use `-Course GeneratorToRae` for the other leg. The runner executes only a
focused course and exposes the read-only monitor at `http://127.0.0.1:17880/`
while active. Each summary records game and wall time, deaths, observed extra
attackers, retreat attempts, potions, client-observed HP loss, route failures,
and actions by encounter and step.

## Interpretation and next gate

The eight `retreat72` slices above remain archived journey references. They
came from a different binary and do not verify the focused endpoint's
disengagement. They are excluded from the `locked-v1` counts. Earlier focused
setup and repeat probes, including one accidental out-of-split seed 111213,
are also excluded.

Phase 0 establishes auditable outcomes for a fixed deterministic policy and
code/data version. It does **not** establish repeatable world outcomes: before
the course-boundary RNG reset, generator seed 12 both failed and succeeded on
separate fresh processes. After the reset, two seed-12 probes both completed
with one death but differed in game time and first target distance; the
`locked-v1` seed-12 run completed with zero deaths. These are retained as
diagnostic evidence. The Phase 1 reset must reconstruct relevant spawns,
patrol timing, cooldowns and RNG call order, then show repeated runs of one
seed have the same meaningful outcome before candidate policies are compared
on paired seeds. No model was trained and no full-journey batch was run.
