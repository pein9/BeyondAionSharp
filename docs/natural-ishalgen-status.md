# Natural Ishalgen Priest: status and handoff (2026-09-26)

The automated Priest plays Ishalgen 1–9 like a human: the frozen NI-07 journey
(`NaturalIshalgenPriestCompletesFrozenJourneyWithoutSetup`, Q2001 through Q2134 and Munin,
205 quest updates). This page is the running state of that work: what the bot does now, how to
measure it, what the last batches showed and what is left. The design rationale for navigation and
combat lives in `docs/bot-navigation.md`; this page only points at it.

## How to run and read a batch

```bash
bash scripts/sim/run-natural-batch.sh smart47 1 3 4 5
```

Use a new prefix for the next batch; preserve the smart46 evidence.

One full journey per seed, sequentially, ~4–6 min real time each when it completes (a 4-hour
virtual day). One summary line per run: verdict, pulls (`clean` = single pulls with no expected
helper), defends, retreats, close-ins, deaths, quest updates, last step, first exception. Runs land
in `run/natural-batch/<prefix>-full-s<seed>/` with a combat trace (`*.trace.jsonl`: every packet
and every decision, one JSON object per line, fields `vt` virtual time, `step`, `dir` (`>` sent,
`<` received, `action` decision), `packet`, `fields`).

Scripts that compare batches (`scripts/sim/trace/`, each takes run folders under `run/natural-batch/`):
`aggro_causes.py` (how every monster came to attack: respawn beside the bot, a patrol walking in, or
other; plus deaths), `death_profile.py` (the decisions and attackers before each death),
`hatata_profile.py` (the Q2129 Hatata step: length, kills, every attacker and whether it respawned
during the step), `step_times.py` (virtual and real time per quest, two runs side by side).

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
  `combat-range-close-in`, `dialog-too-far`, `forced-landing-on-ground`; `SM_FORCED_MOVE` (knockbacks)
  is traced since 2026-09-26.

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
| smart43 | commit b6a047006 (previous baseline) | 4/4 | 3 | — (deaths: Q2007 green and blue generators, Hatata) |
| smart44 | + patrol paths (whole path everywhere), `SM_FORCED_MOVE`, in-aggro = engaged, defend before fight-through | 3/4 | 0 | seed 4: knocked into a rock face, no legal step from there |
| smart45 | + passing reach (15 m of a path on routes), forced landing on ground | 3/4 | 1 | seed 1: rested with a stalker on it after a fight-through kill and died; the retry found no route back to Hatata |
| smart46 | + defend before journey rests, progress-based guarded-approach retries, preserve pull range after empty-spawn waits | **4/4** | **0** | —; all four reached Munin with 205 quest updates and no retreats |

**Baseline validation** (2026-09-26): the combined smart44-46 changes were committed
as `3930f9aaa`. smart46 passed 4/4 with zero deaths; 153 affected unit tests passed. The
full **31-command CLAUDE.md checklist passed**, including solution tests (4,797 passed, 45 gated
skips), warning baseline, null loggers, clock reads, custom-quest drafts, structural fidelity,
all harness contracts, both baked navmeshes and the Docker Fast tier. Fast run
`fast-20260925-215110` passed all 11 manifest scenarios (22 tests passed, one gated skip).
Logs and exit codes are in `run/smart46-checks/`; no new warnings or baseline increases.
The throwaway cadence probe was removed before these checks. No server combat rules changed.

### What the batches since smart41 showed

Measured with the scripts above; the design is in `docs/bot-navigation.md`, entry "Patrols are known
by the path they walk; a knockback moves the bot (2026-09-25)".

- **There is no Hatata respawn race.** In ten Hatata steps (smart37–41) no attacker was a respawn of
  anything killed on the way in. The smart41 seed-3 "stalker and two patrols at the arrival point"
  were original spawns: the Gray Mane patrol (walker loop x 620–642, y 859–877) walked into the bot's
  route, and its two neighbours joined by assist.
- **All three smart43 deaths were in that one area** (the Q2007 generators and Hatata share it): a pull
  of the patrol from a spot on its own loop; a stalker respawning 3.9 m from the bot while it planned
  the next pull; and melee casts refused at "1.97 m" after stumbles the bot never saw (it did not
  decode `SM_FORCED_MOVE`), after which the fight-through walked on with two attackers.
- **smart44/45 with the fixes**: 0 deaths in smart44's four runs, 1 in smart45's. Q2007 on seed 3 went
  from 3,201 s plus a 2,094 s rejoin (two deaths) to 1,175 s. Knockbacks now reach the bot 7–16 times
  a run.
- **Whole patrol paths on routes were too much**: they closed the Mau farm corridor on the way back to
  Ulgorn (all ten checkpoints refused against 62 circles; Return fallback, +350 s). Routes now use the
  15 m of a path a walker can reach while the bot passes, and smart45 real time is back near smart43.

- **smart46 is 4/4, zero deaths and zero retreats.** Seeds 1/3/4/5 finished in 6:20/7:10/8:36/8:09
  real time, each with 205 quest updates. Whole-journey virtual time was 3:22–3:28. The Hatata kill
  steps (including approach and guards) were 364/304/277/250 s, with 8/8/7/7 kills and no attacker
  identified as a respawn during the step. Hatata combat decisions themselves span 27.4/26.4/25.9/26.0 s.
- **The defence-before-rest change is exercised outside Hatata too:** 91 pre-rest defence decisions
  across the four runs. Knockbacks were received 18/8/6/5 times; seed 1 resolved one landing to ground
  and continued. `aggro_causes.py` classified 30 nearby arrivals as respawn-on-bot and 14 as patrol
  engagements; these are heuristic classifications, not proof that every nearby arrival was a respawn.
  `death_profile.py` reports no deaths on any seed. Reports are in `run/smart46-analysis/`.
- **Cost remains in route searches.** Versus smart45, Q2007 virtual time is lower on seeds 3/4/5
  (1152/1204/1158 s) and higher on seed 1 (1589 s). Real time is slower on three seeds, and also varies
  across unchanged travel legs. The zero-death result is not a claim of a route-performance improvement.

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
- **Patrols**: the world model learns each walker's path from its walk moves
  (`BotPatrolPath`); places the bot stays at keep off the whole path, routes off the 15 m a walker can
  reach meanwhile. **Knockbacks** move the bot (`SM_FORCED_MOVE`); a landing against a rock face stands
  on the ground at its foot. A monster whose aggro circle the bot stands in counts as engaged; the
  fight-through defends before walking on and after a kill before resting. All journey rests
  check engaged monsters before relocating. Guarded approaches count consecutive stalls, not successful
  fights and walking progress, with an overall attempt cap.

## NI-08 durable resume and diagnosis (complete, 2026-09-26)

NI-08 builds on `3930f9aaa`. Each login rebuilds
its client world from current/completed quest journals, location, stats, inventory
and skills. The journey chooses from that state; a saved diagnostic receipt never
restores or grants server state. Active quest handlers skip finished steps and
use remaining kill counters or held items, including Hatata's separate six-bit
counter. Disconnects preserve an incident file, honor reentry timing, and resume
the same character with bounded connection retries. A quest-progress budget and
the overall run timeout bound stalls; failure packages include the last complete
observation, recent packet names, trace path, build/run/seed and elapsed time.

A cold Hatata restart exposed a policy-state dependency: route hazard avoidance
had been enabled only by executing Q2005. Each decision now derives that mode
from the current/completed journal, so resuming beyond Q2005 cannot silently
walk through hostile circles. Pull plans are abandoned after a revive. Fresh
`SM_NPC_INFO` observations also decode Java's state/heading fields, so a corpse
is excluded from live targets without relying on remembered kill object IDs.

The read-only monitor is enabled at `http://127.0.0.1:17880/` during natural SIM
runs. `AION_BOT_DASHBOARD_PORT=0` disables it. It is unavailable during server boot
and between runs; announce when a run is available to watch.

Cold restart proof uses a dedicated GUID-named SIM database, owned only by the
wrapper. It stops and saves through ordinary quit, then launches a fresh server
and bot process, reconstructs the same character, and completes the journey:

```powershell
pwsh -NoProfile -File scripts/sim/run-natural-resume.ps1 -Run ni08-next -StopAt 2007:3:6 -Seed 1
```

The wrapper never opens a dev-world database; its `finally` drops only its own
schema. `StopAt` means quest ID, status and packed quest variables. This is a
saved-state restart proof, not an abrupt server-crash durability claim.
`NI08_RELOG_AT=2102:3:2` injects one lost connection into a normal full SIM run.
`NI08_RESUME_CHARACTER` requires an existing matching identity and never creates
a replacement if missing. Run directories must be new to preserve failed evidence.

| NI-08 evidence | Result | Limits |
|---|---|---|
| `ni08-early-reconnect-s1` | Failed on an obsolete spawn-position assertion | Replaced with login-derived location |
| `ni08-early-reconnect-b-s1` | Same character resumed Q2102; diagnostic stopped at repeated Q2007 route failure | Not a pass; added an ordinary Return fallback |
| `ni08-cold-generator-a-s1` | Failed before gameplay on empty elapsed-time environment input | Empty optional environment values normalized |
| `ni08-cold-generator-b-s1` | Fresh processes resumed Q2007 START/6 and completed 41 quests at Munin | 0 deaths before restart, 4 after; not zero-death evidence |
| `ni08-warm-c-s1` | Disconnect at Q2102 START/2; same character finished all 41 quests on connection generation 2 | Zero deaths; original incident retained |
| `ni08-cold-hatata-s1` | State persistence passed; journey failed after six deaths and the 60-minute quest-progress limit | Revealed a route-policy flag initialized only by executing Q2005; cold resume skipped that initialization |
| `ni08-cold-hatata-b-s1` | Same checkpoint completed after policy reconstruction; all persistence comparisons passed | Zero deaths before and after restart; 40 saved completions, then all 41 at Munin |
| `ni08-cold-reward-s1` | Warm reconnect at Q2102 START/2, cold restart at Q2003 REWARD/1 with its items consumed, then all 41 quests | Zero deaths before/after; pending reward claimed and all persistence comparisons passed |
| `ni08-missing-character` | Expected startup refusal for missing retained ID 987654 | No replacement creation; startup failure package and trace verified |

**Working-tree note:** NI-08 is included in the commit containing this entry on
main, without a push. The 183 affected unit tests passed. All **31 CLAUDE.md
checklist commands passed**, including 4,820 solution tests (45 gated skips),
warning baseline, clock reads, fidelity and both baked navmeshes. Docker Fast
`fast-20260926-000243` passed all 11 manifest scenarios (22 tests passed, one
gated skip). Logs and exit codes: `run/ni08-checks/results.json`; expected missing
identity verification: `run/ni08-missing-verification.log`. No production server
rules changed. NI-09 isolated LIVE acceptance is next; NI-10 retained-world attach
and NI-11 real-client observation remain uncompleted.

## Open items, in the order I would take them

1. **Done — defend before rest.** `TryFightThroughAsync` defends after `fight-through-cleared`;
   `RestSafelyAsync` covers the other journey rests, including add clearing and quest kills.
2. **Done — keep fight-through on the return from bind.** The old eight-total-clear budget was exhausted
   while the smart45 approach made progress before and after a death. The next blocked segment never
   reached fight-through. `NaturalApproachProgress` now allows progress while bounding consecutive
   stalls at eight and total attempts at 120. Empty-spawn retries retain `withinRange`.
3. **Done - smart46 4/4, zero deaths.** Full checklist and Fast tier passed; committed locally to main.
4. **Travel-route cost**: every patrol point is a circle, so failed hazard searches cost about 1.3 s of
   real time each (0.8 s before). Thinning the circles to r/2 spacing would narrow the band to 0.87 r;
   only do it if real time matters.
5. **Done — SIM swing cadence measured after smart46 was green.** A throwaway SIM probe placed a
   character beside a shipped 211284 Stalker, initiated one ordinary attack, kept the character at full
   HP and advanced 50 ms at a time for 30 s. Eleven `SM_ATTACK` packets were sampled against
   `fixture.Clock.NowMillis`: first at 750 ms, eight subsequent intervals of **2100 ms** and two of
   5200 ms. The current attack-speed stat was 2100 throughout. This contradicts the earlier
   "exactly 2.000 s regardless of attack_speed" claim; no scheduler change is justified. The longer
   gaps were not classified by this minimal probe. An initial passive setup did not establish combat
   and produced no swings. Evidence: `run/smart46-cadence.json`, `run/smart46-cadence.log`, source
   retained as `run/SimulationNpcCadenceProbe.cs.txt`; the throwaway test was removed before the full
   checklist. Java `SimpleAttackManager` and `NpcGameStats.getNextAttackInterval` remain the spec.
6. **The generator clearing** (Q2007) still uses the wide margin (aggro + 22 m), now measured from a
   patrol's whole path. The Q2007 deaths are gone in smart44-46; leave it.
7. **Portal importer** (`../aion-portal`): its 8 tests pass, but it is one change with the 5.8 overlay
   there (`scripts/cache_58_ishalgen.py` and its test, `assets/maps/ishalgen-58.json`, the spawn-editor
   UI files, the `package.json` scripts, `docs/PROGRESS.md` iterations 109–110; also a modified
   `assets/maps/bot-deaths.json`). Ask the user how to commit it; do not commit that repo alone.

## Recorded human Hatata fight (reviewed 2026-09-26)

Found `run/recordings/rrfarmer-20260924T234749765Z-1a0d5d17712-486fe9.recording.jsonl`:
Pilot, level 9, a 1,308 s session with no `SM_DIE`. Hatata (210409, object 41392) was engaged
at **2026-09-25 00:05:16.307 UTC** (September 24, 8:05 p.m. Eastern). The quest update and
loot-enable arrived at **00:06:03.978/979**, 47.7 s later. He was the only attacker during
that fight; the lowest server-reported HP was 393/669 (59%). Pilot fought around (670, 872),
used Smite 4012, Infernal Blaze 1814, Hallowed Strike 1615, Healing Light 1838, a potion and
seven mace attacks. Two Smite casts were interrupted.

For comparison, the successful **Q2129** Hatata fights in smart45 seeds 3/4/5 span 33.0/28.2/24.2 s
from first to last combat decision (excluding the final cast's completion). Their minimum decision
HP was 530/613/584, and each had one observed attacker. These are different gear/skill-rank and
random-outcome samples, not a controlled speed benchmark. They do show that the bot can finish
an isolated Hatata fight promptly. Approach clearing and carrying adds into a rest are the measured
problems; another human recording is not needed to diagnose those.

## Spawn placement passes (for the next retail-accuracy edit)

```bash
python tools/nav/audit_spawn_heights.py --map-id 220010000 --base HEAD
pwsh -NoProfile -File scripts/parity/regen-geo-golden.ps1
```

Then `dotnet run --project tools/Aion.NavBake -- graph --maps 220010000` if spawn positions
changed (the travel graph is derived from them), and a batch.
