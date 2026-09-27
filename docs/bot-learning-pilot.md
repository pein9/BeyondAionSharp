# Level 9 bot learning pilot: Mau camp and Hatata

## Goal and boundary

Build two repeatable **area challenges** with a level 9 Priest: a generator-to-Rae return that covers the Q2007 rejoin, and a Rae-to-Hatata course that ends when Hatata is dead and the Priest has disengaged safely. Compare learned decisions with the current deterministic bot on the same simulation seeds. These are diagnostic challenges, not a replacement for the retail quest order: Q2007 and Q2129 occur at different points in the full journey. The generator-to-Rae leg is required because `timing68` seed 3 exhausted ten guarded approaches on that return; a Rae-to-Hatata course alone would miss it.

For this pilot, the Priest does not choose quests, explore the world, or learn skills. It uses a fixed, recorded level 9 loadout and the client-observed skill list. The server's combat, spawn, and quest behavior remains the Java-defined reference. A model may use only information the bot could observe through the client; server internals may be used offline to label outcomes and diagnose failures.

The pilot should have two explicit policies:

- **Baseline:** the current pull planner and `NaturalPriestCombatPolicy`.
- **Candidate:** the same legal-action and navigation checks, with a learned choice at a narrow decision point. Keep a switch to run either policy against identical seeds.

## Phase 0 — Lock the benchmark

1. Specify each course's starting position and quest state, plus level, learned skills, gear, inventory, bind point, and world seed. Record the source of each value from a successful client-observed run. Do not assume the level grants a skill.
2. Define success for the return leg as reaching Rae with a checked route, alive and out of combat; for the Hatata leg, Hatata's objective completes and the bot disengages safely. Both must have no stalled recovery. Record deaths, extra attackers, retreat attempts, potions, elapsed **game** time and wall time, damage taken, and actions by encounter.
3. Reserve development seeds and unseen evaluation seeds before tuning. Keep the familiar seeds 1/3/4/5 as regression cases; they are too few to establish general reliability.

Keep the `recovery69` seed-5 Q2006 Mijou return as a separate navigation-cost regression: it completed but spent 320 wall seconds repeatedly searching hazard-rejected routes. It is outside the two Mau-area training legs and should not be hidden in their combat scores.

**Gate:** the unmodified policy produces an auditable trace and summary for each seed on both legs. Preserve failures as baseline outcomes rather than selecting only successful seeds. Its results become the comparison baseline for the exact code and data version used by training.

## Phase 1 — Make encounters cheap to repeat

1. Add a SIM course reset that reconstructs the character and the relevant world state, including spawns, patrol timing, cooldowns, inventory, and potions. Run the same seed repeatedly to check that outcomes are reproducible. `NaturalJourneyCheckpoint` is a client-state receipt for restart verification, not a world snapshot to restore.
2. Provide both full area legs and short encounter starts: an isolated Stalker, a two-attacker pull, a moving patrol, a blocked generator-to-Rae rejoin, Hatata alone, and Hatata with an add. Preserve normal client packets, cast cadence, movement, and collision checks after the initial setup.
3. Convert the existing JSONL trace into one training record per decision: observed state, **legal candidate actions**, chosen action, policy version, seed, and eventual encounter outcome. Record why the baseline rejected each candidate. Do not label an unchosen action as safe or unsafe merely from the chosen action's result.
4. Measure run cost before fixing a training budget. Keep the read-only bot monitor available during natural SIM runs; do not build or run other checks while a journey batch holds the DLLs.

**Gate:** repeated runs of one seed give the same meaningful outcome, and the encounter starts exercise the same combat and navigation code as the area challenge.

## Phase 2 — First training: tune the existing policy

Start with a small, inspectable parameter vector rather than a neural network: pull distance and patrol wait tolerance; healing and potion thresholds; mana reserve; low-HP finishing preference; and target priority when two mobs are engaged. Bounds must preserve legal skill use and a conservative emergency fallback.

Use random search as a reference, then a small genetic search if its generations are useful to inspect. Evaluate candidates on paired development seeds, stop clearly bad candidates early, and spend the larger run budget only on finalists. Rank results in this order: completion, deaths, extra attackers and unrecovered stalls, then game time and consumables. A faster death is not an improvement.

**Gate:** freeze the best candidate and compare it with the baseline on at least 20 previously unseen paired seeds, plus the familiar regression seeds. Adopt it only if completion and deaths do not regress and a practical benefit remains on the unseen seeds. Preserve the full trace for any surprising win or loss.

## Phase 3 — Learn which safe pull to choose

If Phase 2 exposes repeated pull mistakes, train a small outcome or risk model to rank the current planner's legal target/firing-spot choices. Features can include observed hostiles and patrol paths, expected helpers, target type, range, HP/MP, recent damage, and the bot's position. Predictions can cover extra attackers, damage, death, and time to clear.

For training labels, rerun *different legal candidates* from the same encounter reset where possible. Ordinary traces reveal the outcome of the chosen candidate only. First run the model in shadow mode, logging how its ranking differs from the baseline without changing actions. Then test it as a selectable policy on paired seeds. Keep the existing checked-route, collision, and emergency rules around it.

**Gate:** fewer unwanted adds or deaths on unseen seeds, with no drop in completion; inspect disagreements rather than accepting an aggregate score alone.

## Phase 4 — Optional combat learning

Only if parameter tuning leaves clear combat mistakes, train a sequential combat policy on the short encounters. Decisions occur at events such as a skill becoming ready, a chain opening, damage arriving, or a target changing. Actions are legal casts, attack, heal, owned potion, target switch, hold, or retreat. The learned policy sees client-observed HP/MP, target HP, attackers, distance, effects, cooldowns, and learned skills. Compare it with the tuned deterministic policy across single and multiple attacker fights before placing it in the area challenge.

Do not use the full quest journey as the training episode. Preserve the deterministic policy as the fallback for untested states and for missing or invalid model data.

## Graduation and later expansion

Version the chosen policy, training data, seeds, loadout, and emulator build. Run the full frozen Ishalgen journey only after the area challenge passes its held-out evaluation. Keep a separate exploratory bot profile so optimization does not remove the combat and recovery situations that the playtest harness is meant to exercise.

**Later project:** when leveling beyond 9 is available, condition a shared combat policy on the *observed learned skill list* and save validated policy snapshots at each level or skill unlock. Replay earlier-level encounters to catch regressions. Quest choice, exploration, and autonomous skill acquisition then become separate training problems; they are outside this pilot.

## Existing starting points

- `tests/Aion.Bots/Scenarios/NaturalPriestCombatPolicy.cs` — deterministic combat baseline and learned-skill eligibility.
- `tests/Aion.Bots/Scenarios/NaturalIshalgenJourney.cs` — Rae and Hatata journey decisions and trace points.
- `tests/Aion.Bots/Scenarios/NaturalJourneyCheckpoint.cs` — client-state restart receipt.
- `docs/natural-ishalgen-status.md` and `docs/bot-navigation.md` — baseline batches, failure causes, and human-run comparison.
- `scripts/sim/trace/` — current death, aggro, Hatata, and timing reports.
