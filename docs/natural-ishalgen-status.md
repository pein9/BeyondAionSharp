# Natural Ishalgen Priest: status and handoff (2026-09-25)

The automated Priest plays Ishalgen 1–9 like a human: the frozen NI-07 journey
(`NaturalIshalgenPriestCompletesFrozenJourneyWithoutSetup`, Q2001 through Q2134 and Munin,
205 quest updates). This page is the running state of that work: what the bot does now, how to
measure it, what the last batches showed and what is left. The design rationale for navigation and
combat lives in `docs/bot-navigation.md`; this page only points at it.

## How to run and read a batch

```bash
bash scripts/sim/run-natural-batch.sh smart43 1 3 4 5
```

One full journey per seed, sequentially, ~4–6 min real time each when it completes (a 4-hour
virtual day). One summary line per run: verdict, pulls (`clean` = single pulls with no expected
helper), defends, retreats, close-ins, deaths, quest updates, last step, first exception. Runs land
in `run/natural-batch/<prefix>-full-s<seed>/` with a combat trace (`*.trace.jsonl`: every packet
and every decision, one JSON object per line, fields `vt` virtual time, `step`, `dir` (`>` sent,
`<` received, `action` decision), `packet`, `fields`).

Trace queries that were used to compare batches (Python one-liners over the trace):

- Deaths and where: `accept-client-death-and-revive-at-bound-obelisk` packets; the preceding
  `step` is the fight that killed the bot; attackers are `SM_ATTACK` with `targetObjId` = the
  player's object id (132732 in these runs) since the last kill.
- A fight's profile: within a `step`, `combat-decision` fields `hp`, `observedAttackers`,
  `action`/`skillId`; own casts interrupted = `SM_SKILL_CANCEL` with the player's `objectId`;
  stuns = `SM_ABNORMAL_STATE` effects with `skillId` 8217.
- The pull logic: `pull-plan` (`target`, `firingPosition`, `expectedHelpers`),
  `adds-that-would-join` (`adds`, `purpose` ending `-at-target` for the no-clean-spot fallback),
  `pull-wait-for-patrol`, `fight-through-plan/-walk/-advance/-stand/-cleared`,
  `kill-retry-after-death`, `kill-target-vanished`, `combat-obstacle-close-in`,
  `combat-range-close-in`, `dialog-too-far`.

Before committing gameplay changes run the checks listed in `CLAUDE.md` (warning baseline, null
loggers, clock reads, fidelity, Fast tier). They rebuild, so never run them while a batch is
running: the test host holds the DLLs.

## Where the bot stands

Batches of the same four seeds (1, 3, 4, 5); "completed" means the whole journey.

| Batch | Build | Completed | Deaths | Stops |
|---|---|---|---|---|
| smart22 | tribe-blind hostility, ranged rotation | 0/4 | 84 | Q2007 lycan camp, revive cap |
| smart25 | Stalkers visible, melee rotation, real cast cadence | 4/4 finished Q2007 | 1 | later side quests |
| smart31 | 3D hazards, walk/stand fallbacks, generic gather + Essencetapping practice, Return retry | 4/4 | 1 | — |
| smart32–34 | lifted spawn heights; Return gate, long advance, kill retry, Munin arrival | 1–3/4 | 0–3 | each stop fixed in turn |
| smart35 | as above, arrival handed back from the blocker loop | 4/4 | 0 | — |
| smart36 | quest kills as planned pulls (first cut) | 0/4 | 3 | neutral targets had no pull entry; fired without sight |
| smart37 (seed 5) / 38 | pull entry for any tribe, obstacle → old reposition | 3/4 | 4 | Hatata: no clean spot, walked in with two patrols |
| smart39 | patrol wait, adds at the target's spot, obstacle → melee, ranged targets | 3/4 | 1 | Q2110 start: "too far to talk" then a hang to the 45-min cap |
| smart40 (seed 3) | same build | 1/1 | 0 | — |
| smart41 | dialog re-approach, 3-minute packet-wait cap | 3/4 | 0 | seed 4: bare InvalidOperationException 37 s in |
| smart42 | resumable packet read (fix for the above) | not run to completion | | stopped for the handoff |

The last commit's build (smart41 + the resumable read) has **not** been batch-verified as a whole:
smart41 ran 3/4 with zero deaths before the read fix; the read fix was built and unit-tested only.
The first thing to do in a new session is `run-natural-batch.sh smart43 1 3 4 5`.

### What the smart41 fights looked like

Hatata the Torturer (Q2129, 1,821 HP, Seasoned, stuns, in the Black Opal cave) is the only fight
that has killed the bot since the melee rotation. One-on-one he dies in 8–13 s. Every death was an
add: a second Gray Mane on the bot. The planned-pull path (below) kept the last batches at
zero deaths, but seed 3 in smart41 still met a stalker and two patrols right at the arrival point
after a three-minute fight-through: the cave entrance respawns in 180 s, and the way in takes
about that long. That is the open combat risk.

## What the bot does now (pointers)

All in `docs/bot-navigation.md`, sections "Pulling and retreating like a player" and the dated
entries at the end; the code is `tests/Aion.Bots` (policies, planners, navigation) and the journey
`tests/Aion.Simulation.Tests/SimulationNaturalIshalgenJourneyTests.cs`.

- **Hostility** is the server's tribe rule, not the template type (`NaturalHostility`).
- **Rotation** by level with real cooldowns and cast cadence: Smite opener at range, hold, then
  Infernal Blaze → Hallowed Strike → Smite → mace at melee (`NaturalPriestCombatPolicy`,
  `NaturalPriestSkills`); Blessing of Guardianship kept up; gear upgrades from the bag.
- **Quest kills are planned pulls**: stop at pull range, firing spot with the fewest adds, the adds
  that would join (the server's assist rule at that spot: `NaturalPullPlanner.AddsAt`) pulled and
  killed first, rest only inside the 180 s respawn window, engage at ≥80 % HP / 60 % MP. No clean
  spot: wait up to a minute for patrols, then clear the adds at the target's spot, then walk in.
- **Emergency** at 55 % HP against a Seasoned+ target with a second attacker (35 % otherwise).
- **Recoveries**: retry a kill after a death or a vanished target (six attempts), re-approach on
  "too far to talk", close to melee on a server-side obstacle, walk up to rooted ranged monsters,
  Return recast after an interrupt, arrival recognised after a fight-through walk.
- **Gathering**: any node template on the map; Essencetapping practised on Young Azpha until the
  node's skill level (Q2134 needs 15; ~30 harvests).
- **Session robustness**: a single packet wait is capped at three minutes of real time; a read
  abandoned by a real-time timeout is resumed by the next wait (never re-entered).

## Open items, in the order I would take them

1. **Batch-verify the current build** (smart43). Expect 4/4; if seed 4 shows the loot-list timeout
   again it is now harmless (`quest-loot-list-missing` trace, then the next wait resumes).
2. **Hatata's cave respawn race.** The fight-through to him takes ~3 minutes; the entrance
   respawns in 180 s. Options: skip the unconditional rest after each fight-through kill when HP
   is high (it is in `TryFightThroughAsync` after `fight-through-cleared`), or pull him out to the
   cave mouth. Measure with the seed-3 profile query above.
3. **The generator clearing** (Q2007) still uses the old wide margin (aggro + 22 m). It has gone
   4/4 in every batch; only tighten it with a measured reason.
4. **SIM oddity**: NPCs swing every exactly 2.000 s in SIM regardless of template `attack_speed`
   (2100 in LIVE). Unpinned; affects how SIM fight lengths compare to LIVE.
5. **Portal importer** (`../aion-portal/scripts/import_58_ishalgen.py`, uncommitted there along
   with another session's spawn-editor work): fixed retail positions now take the collision surface
   nearest the retail height. Commit it in that repo with its test.
6. Pre-existing, unrelated: none left; the two tests broken by the 2026-09-23 spawn import were
   repaired (soak basket count, geo golden regenerated; `scripts/parity/regen-geo-golden.ps1`).

## Spawn placement passes (for the next retail-accuracy edit)

```bash
python tools/nav/audit_spawn_heights.py --map-id 220010000 --base HEAD
pwsh -NoProfile -File scripts/parity/regen-geo-golden.ps1
```

Then `dotnet run --project tools/Aion.NavBake -- graph --maps 220010000` if spawn positions
changed (the travel graph is derived from them), and a batch.
