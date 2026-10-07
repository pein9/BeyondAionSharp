# Natural Ishalgen Priest: status and handoff (2026-09-27)

**2026-10-04 route revision:** [OD-16](natural-ascension-altgard.md#operator-decisions)
prioritizes Ascension Q2008 and the Pandaemonium ceremony Q2009 at the first level 9.
Ascension-enabled journeys return to finish the retained Ishalgen quests as a Cleric;
the Priest-only batch diagnostics below keep their historical endpoint. Fresh proof
is tracked as EA-01/EA-02 in the Ascension document.

Fresh proof `ea02-create-s1-a2` (2026-10-04) completed the revised Ishalgen/bridge segment:
Q2133/Q2134 while human, immediate level-9 departure during Q2005, ceremony and return
with staff and learned skills, all 41 Ishalgen quests at level 13, then the Altgard bridge
and endpoint relog. Zero deaths were recorded; both SIM schemas were dropped. Evidence:
`run/early-ascension/ea02-create-s1-a2/route-audit.json`. The first failed attempt is retained
and explained under EA-02. Full Altgard/Haramel evidence remains the prior AS-02 route.

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

Scripts that compare batches (`scripts/sim/trace/`):
`aggro_causes.py` (how every monster came to attack: respawn beside the bot, a patrol walking in, or
other; plus deaths), `death_profile.py` (the decisions and attackers before each death),
`hatata_profile.py` (the Q2129 Hatata step: length, kills, every attacker and whether it respawned
during the step), `step_times.py` (game-clock and real time per quest, one run or two
runs side by side). Their working-tree input adapter accepts a SIM run folder, a
LIVE run root containing `bots/*.trace.jsonl`, or one explicit trace file. It
requires a single bot trace so problem ledgers cannot be selected accidentally.
LIVE has no virtual clock and uses recorded wall timestamps. All four tools were
verified against `ni09-shared-s1` and the terminal `ni09-live-a2` trace; the former
still reports its one death and 193-second, five-kill Hatata step. The latter
reports 8,638 seconds, no deaths, and no Hatata step before its Q2116 failure.

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
| ni09-route-full | NI-09 shared SIM/LIVE driver, client animation timing, replan after guard-clearing movement without a kill | **4/4** | **4** | —; seeds 1/3/4/5 completed all 41 quests; deaths 1/0/1/2, all recovered |
| ni09-live-a4 | Full isolated LIVE journey, ordinary rates, final same-character relog | **1/1** | **0** | All 41 quests, level 9 at Munin, Q2008 START/0; persistence verified; 3h 42m 26s |
| retry60 | changed-spawn navigation and Return from Hatata to bind, before cast cooldown fix | 3/4 | 6 | seed 1: Q2006 Return refused because observed cast cooldown was not retained |
| timing64 | retain client-observed cast cooldown; recover Q2005 after revive | 3/4 | 2 | seed 4: Q2005 retraced stale pre-death breadcrumbs until watchdog |
| timing66 | discard Q2005 breadcrumbs after revive and rejoin from bind | 3/4 | 7 | seed 5: six Q2127 kills were not recognized after targets vanished during pull planning |
| timing68 | plus accept client-observed 0%-HP kill evidence; same final working tree | **3/4** | **4** | seed 3: Q2007 rejoin at (633.6, 913.4) found no collision-checked guarded route after 10 attempts; seeds 1/4/5 finished all 41 quests |
| recovery69 | heal HP between fights while MP ≥50%, sit only when MP <50% until 80%, timed HP potion at ≤90%, bounded terrain-checked route probe | **4/4** | **5** | all 41 quests completed; seed 1 died four times in Q2007 and once in Q2128; seeds 3/4/5 had zero deaths |
| retreat70 | heal at 55% vs one / 70% vs two; retreat immediately vs three; require client-observed disengagement | 2/4 | 9 | seed 3 could not route back to Hatata; seed 4 timed out at Q2007 rejoin |
| retreat72 | plus terrain-detour escape check and separate tactical-retreat retry budget | **4/4** | **8** | all 41 quests; deaths 3/3/1/1; four cornered escape fallbacks |

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
rules changed. NI-09 isolated LIVE acceptance subsequently passed below;
NI-10 retained-world attach and NI-11 real-client observation remain uncompleted.

## NI-09 isolated LIVE acceptance (complete, 2026-09-26)

The NI-09 change extracts the full quest/navigation/combat/recovery driver into
`tests/Aion.Bots/Scenarios/NaturalIshalgenJourney.cs`. SIM supplies its clock and
in-process session; LIVE supplies real sockets and wall-clock delays through
`INaturalJourneySession`. Static data is injected, with no running server's world
or administrative mutations available to the shared policy. The LIVE acceptance
entry requires a fresh ordinary Priest and runs the entire contract, with an
eight-hour overall limit and the NI-08 bounded recovery/diagnosis behavior.

```powershell
pwsh -NoProfile -File scripts/live/run-live.ps1 -Run ni09-live-a2 -Scenario NI-09 -Bots 1 -WatcherMode enforce -DashboardPort 17880 -StepTimeoutSeconds 120 -RunRoot run/ni09-live
```

Use a fresh run name. The runner owns a separate Docker database/server stack,
forces `docker-bots-natural`, refuses Keep or record-only watching, and inherits
ordinary shipped rates, gather failures and respawns. The host bot exposes the
loopback monitor; this is not retained-dev-world attachment (NI-10).

Evidence so far:

- `run/ni09-shared-s1.log`: the extracted driver passed all 41 quests in SIM,
  6m23s gameplay, with one Q2007 green-generator death and successful recovery.
  Hatata completed without a death or retreat. This preceded the explicit
  client animation hit-time change described below.
- `run/ni09-affected-tests-b.log`: 189 affected tests passed. A new LIVE session
  test proves a cancelled packet wait resumes its pending read without losing
  the packet. The Full suite planning contract passed with NI-09 included.
- `run/ni09-live/ni09-live-a1/`: failed immediately after login. Q2000's movie-end
  response had not arrived at the first time-check barrier. Fresh entry now waits
  explicitly for the ordinary Q2000 completion. The watcher also found Hulker's
  stale `pool=1` on a single spot left by `1856203e52`; removing that ineffective
  attribute preserves Java `SpawnEngine.checkPool`'s existing single-spawn
  fallback, coordinates, count and respawn delay. No warning exemption was added.
- Java `Skill.updateHitTime` requires ordinary client animation/projectile timing.
  The shared driver now calculates it from shipped motion data, equipped Priest
  weapon and observed target distance (as the earlier bounded LIVE combat driver
  did), instead of sending zero. Server skill/combat rules are unchanged.
- `ni09-live-a2` **failed after 31/41 quests and 8,638 seconds**, with zero deaths.
  Q2002's Ataxiar round trip and all Q2007 generators completed. At Q2116, object
  700139 was observed at its Java-shipped location, but its hazard-blocked approach
  exhausted the clearing helper. That helper had moved the bot about 108 m without
  killing a selected guard; the caller discarded this progress and failed against
  the original route result instead of replanning. The caller now observes walking
  progress even when clearing returns false, retaining the eight-stall and
  120-attempt limits. A regression covers progress without a guard kill and both
  bounds. No server or spawn-placement change was made for this failure.
- The route fix builds; `run/ni09-route-tests.log` records 180 affected tests
  passing. Map calibration/assets/spawn checks and all four trace tools passed.
  Four full sequential SIM runs (seeds 1/3/4/5) passed via the ignored
  `run/run-ni09-route-regression.ps1`, with receipts under `run/ni09-route-full-s*/`
  and exit codes in `run/ni09-route-regression-results.json`. Every receipt has
  level 9, all 41 completions, and Q2008 START/0. Deaths were 1/0/1/2: three in
  Q2007 and one on seed 5's Hatata approach; all recovered. All four trace tools
  ran successfully; Hatata's death count now uses recorded SM_DIE packets within
  the step span, including revival label changes. Its seed-5 count is one.
  None of these SIM runs exercised `guard-clear-replan-after-progress`, so they
  establish regression coverage, not direct proof of the LIVE route correction.
- `ni09-live-a3` was deliberately stopped early after an acceptance audit found
  that NI-09 lacked the contract's final relog proof. The runner collected its
  evidence and removed its owned stack; this is an incomplete attempt, not a pass.
  `operator-stop.txt` records the reason. The LIVE wrapper now relogs after the
  completed journey, requires a fresh connection generation and the same ordinary
  Priest, compares both journals, inventory, skills, level and saved position,
  then rechecks all 41 completions/Munin/Q2008 before its final logout. Successful
  runs retain `natural-ishalgen-persistence.json` with both observations.
  The build and 192 affected tests passed (`run/ni09-persistence-*.log`), including
  rejection of missing or changed persisted state. Gameplay policy is unchanged
  from the four-seed regression above.
- Full LIVE attempt **`ni09-live-a4` passed** with enforce watching, one ordinary
  access-level-0 Priest, and the ordinary profile. All 41 quests completed with
  **zero SM_DIE packets** in 13,345.9 seconds (3h 42m 26s), ending level 9 at Munin
  `(379, 1892.77, 327.688)` with Q2008 START/0. The final ordinary relog advanced
  connection generation 1 to 2, preserved journals, inventory, equipment, skills,
  level and saved position, and reverified the complete contract before logout.
  `natural-ishalgen-persistence.json` records `verified: true`; the completion
  artifact and trace contain the post-relog checkpoint and `scenario:complete`.
  Enforce watcher: zero new/known/regressed problems, only the existing suppressed
  startup warning. The runner exited 0 and removed its owned Docker stack.
  Evidence: `run/ni09-live/ni09-live-a4/`, runner `run/ni09-live-a4-runner.log`.
  All four trace tools succeeded (`run/ni09-live-a4-*.log`): Hatata's objective
  took 303 seconds including approach, seven kills, no retreats and no deaths.
  Q2134 took 1,373 seconds, including ordinary Essencetapping training to 15.
  Q2116 passed; this run did not exercise the movement-only replan branch.
- All **31 CLAUDE.md checks passed**, including Docker Fast
  `fast-20260926-080115` (11 scenarios; 22 tests, one gated skip), warning baseline
  (4,243 unique sites), and navigation bake verification. Evidence:
  `run/ni09-checks/final-results.json`; original failures remain in `results.json`.
  The final solution suite passed **4,837 tests** with 45 gated skips.
  The stale golden input hash was regenerated through Java: all 4,160 points and
  query results stayed identical. The scenario matrix and C# manifest assertion
  now include NI-09. Final full-suite output is `02-final.log`, coverage-matrix
  rerun `15-rerun.log`. Map calibration, hashes and all placements also passed.

Working-tree handoff: the map QoL and NI-09 implementation are included together
in the validated local main change; see `docs/bot-monitor.md`. No portal files were changed
and nothing was pushed. The preview on port 17880 shows a saved 41-quest snapshot
labelled **Map preview · no bot running**. NI-10/NI-11 remain the next milestones.

## NI-10 existing-world attach (implemented 2026-09-26; coexistence proof open)

NI-10 plays the NI-09 journey on a world the operator already runs, next to their
own client. Authorized for the maintainer's `aion` compose stack (D24).

```powershell
pwsh -NoProfile -File scripts/live/attach-live.ps1 -Target aion          # attach and play
pwsh -NoProfile -File scripts/live/attach-live.ps1 -Target aion -Stop    # from another terminal
```

- **The world stays the operator's.** The runner reads Docker only through an
  allowlisted gate (`ps`, `inspect`, `logs`, and one fixed SELECT of the NI-01
  identity). It never builds, starts, stops, restarts or recreates a container,
  writes the database, or calls the admin API. Before and after each run it
  records container ids, images and start times; `worldLifecycleUnchanged` in
  `attach-report.json` says whether anything changed underneath the bot.
- **Ordinary identity.** Account `niishalgen`, Priest `Ishalgenbot`, created once
  through normal login and character creation (the login server's autocreate),
  then resumed from whatever the client observes (NI-08). The runner refuses a
  GM account, a name owned by another account, a second character, or an
  already-online Priest. The client cannot see access level, so that comes from
  the read-only identity check.
- **Stop and resume.** Ctrl+C or `-Stop` cancels play; the Priest quits normally
  and the server keeps running. The next attach resumes where it left off. Exit
  code 3 means stopped, 0 complete (41 quests, Munin, relog verified), 1 failed.
- **Coexistence evidence.** `coexistence.json` lists every other player the
  server showed the bot (it only does so in mutual sight), with first/last
  sighting, closest distance and quests the bot completed since first sight.
  First sightings are also `coexistence:player-observed` trace actions.
- **Monitor.** The live map defaults to <http://127.0.0.1:17880/>; stop any map
  preview on that port first. The bot runs from a private copy under the run's
  `bot/` folder (removed when it exits), so the checkout can still be built.
- Evidence: `run/ni10-attach/<run>/` (`attach-report.json`, `attach-result.json`,
  `coexistence.json`, `target-*.json`, `logs/containers/`, `bots/b01.trace.jsonl`).
  Contract: `scripts/live/test-attach-live.ps1` (mock Docker, 43 assertions).

Before the first attach, the `aion` server images were rebuilt from `018cca211`
(all layers cache hits for that tree) and the login, chat and game containers
recreated on them at the maintainer's request; nobody was connected, and MySQL
and its volume were untouched. That was an operator action, not part of the runner.

First attaches on `aion` (2026-09-26):

- `ni10-a1`: created the ordinary Priest (id 104673) through normal login and
  creation, finished the Q2000 prologue and Q2101, then was stopped with `-Stop`
  in the middle of a Q2102 fight after 35 s. It quit normally (exit 3, "stopped");
  the read-only check showed it offline; all four containers kept their ids,
  images and start times; server logs for the window had no errors or warnings;
  zero bot problems. The shared driver's "cancelled" diagnosis package in `bots/`
  is the expected snapshot of where the stop landed, not a failure.
- `ni10-a2`: re-attached and resumed the same character (`createdThisRun: false`)
  at Q2102 START, the decision engine choosing `continue-quest 2102`, and then
  **completed the whole contract unattended in 4h 09m 36s**: all 41 quests, level 9
  at Munin `(379, 1892.77, 327.688)`, Q2008 START/0. The final relog advanced
  connection generation 1 to 2, the selection list showed the saved position,
  and journals, inventory, skills and level matched (`natural-ishalgen-persistence.json`,
  `verified: true`). Two deaths, both recovered by bind revive: one on the fifth
  Q2005 Stalker pull (14:20Z) and one during Q2128's quest-drop hunt (16:45Z);
  NI-09's `ni09-live-a4` had none. Zero bot problems, no reconnects. The world
  was untouched (same container ids, images and start times; the Priest offline
  afterwards; no server errors or warnings in the run window). **No other player
  was online, so `coexistence.json` is empty and the proof remains open.**

`ni10-observe-s2b` is the fresh full replay requested after `ni10-a2` finished
before the operator arrived. A separate ordinary account `niishalgen2` created
`Ishalgenbottwo` at level 1, preserving the completed original Priest. The first
attempt `ni10-observe-s2` was rejected before entering the world because Java's
character-name pattern forbids the digit in `Ishalgenbot2`; the replay name now
uses letters only. During the active replay, the bot recorded Pilot 44 m away
at 18:33:59 UTC. The operator confirmed seeing it move in the 4.8 client; it
then reached level 2 and three completed quests, with no bot problems at the
observation point. Interim evidence: `run/ni10-attach/ni10-observe-s2b/human-observation.json`
and the packet trace. At that observation point the bot was still active; NI-10
coexistence proof remained open.

Live observation investigation (2026-09-26): Pilot's grey, unattackable mobs
were caused by `//enemy none`, recorded in the game-server log during this
session. That Java-parity GM command makes every NPC neutral to Pilot;
`//enemy cancel` restored targeting and combat, as confirmed in the client.
This was separate from spawn placement. The bot packet trace recorded distinct
Sprigg Gatherer objects 210368 and 210369 at the same exact coordinates
`(612.850, 2297.140, 251.036)` and `(648.550, 2278.660, 252.966)`, and a
Gatherer 210369 and Sentry 210732 together at `(724.906, 2182.051, 254.738)`.
The XML loaded for this run (`HEAD`) has 59 exact-coordinate overlap sites (including
some inherited from Java 4.8); Java's XML has 13. The extra overlaps first
appear in the retail-placement import commit `1856203e5`. The spawn editor now
lives in `../aion-spawn-editor` (moved from `../aion-portal`); its 5.8 importer
tracks occupied positions per NPC group, not across groups, so shared territory
anchors can produce stacked mobs. This is a placement-data issue, not a bot
spawn action. Separately, Java and C# both respawn these Gatherers after 20 s
while retaining an unlooted corpse for up to five minutes. In the trace, the
bot's grain sack 700093/object 104996 received `SM_DELETE` with fade-out at
18:39:28 UTC after looting. The trace does not establish what Pilot's client
received for that object. Do not alter the running world during the observation;
audit and correct imported overlapping positions, preserve intentional Java
walker/pool overlaps, then rebake navigation and verify in a later live run.

The operator chose the Java 4.8 placements as the repair baseline. The working
tree now restores the pre-`1856203e5` Ishalgen NPC XML; a semantic comparison
of every spawn group and spot confirms it matches the sibling Java 4.8 XML.
The Ishalgen gathering XML already matched Java and was left unchanged. The
bot's fixed vendor hints were updated to the restored Crizpinerk and Denma
positions. This is only an on-disk candidate while `ni10-observe-s2b` runs:
the active game server and bot monitor still show the previously loaded world.
After the run, audit restored heights, regenerate the geo golden and Ishalgen
travel graph and dashboard map catalog, then run the affected tests and a fresh
live observation before deploying these placements.

Selective placement update (2026-09-26): Hatata's Hideout and Rae now use the
5.8 fixed-position reference within X 600–710, Y 840–980. Rae 203554, Patrol
210407, Strongfur 210408, Hatata 210409, and Stalkers 210750/211284 are the
selected templates; the 4.8-only Stalkers 210395/210396 were removed within
that boundary. The two walker spots and the gate/generator static objects were
preserved. The selected Rae/mob count changed from 36 to 18; all spots outside
the rectangle matched the Java 4.8 baseline at that stage. The editor's
`scripts/import_58_hatata_hideout.py` records the reproducible import. This is
an on-disk XML change; the running game server was not restarted.

The next selective pass (2026-09-26) covers the shipped Dubaro Vine Canyon
zone polygon, clipped to 980 < Y ≤ 1175 between Nalto and Rae. Nalto 203552,
Dundun Looklook 210394, Dundun Lookout 210406, Gray Mane Patrol 210407,
Strongfur 210408, and Stalker 210750 use 5.8 fixed-position references. The
4.8 Looklook 210393 and Stalkers 210395/210396 were removed only in this slice.
Selected NPC/mob spots changed from 34 to 19. Two Stalker walkers and all six
Treasure Box 210596 spots remain; the 5.8 box markers are territory anchors,
not fixed positions. The earlier Hatata/Rae pass and all other areas remain
unchanged. The editor's `scripts/import_58_dubaro_canyon.py` records the
boundary, backup, and height audit. The running game server was not restarted.

The Odella Plantation pass (2026-09-26) covers the complete shipped zone
polygon. Mob spots change from 135 to 109 by accounting for 98 fixed 5.8
references (86 explicit spots and 12 protected behavior matches), plus 11
retained 4.8 spots for territory-only populations. The 5.8 Karnif 210655
coordinate is used with the existing 4.8 Karnif ID 210389. Territory-only
Methu 210497 is not added. All 48 quest and field objects remain unchanged.
Twelve protected spots match fixed references and retain their walker or
random-walk behavior; one Farmer walker remains on the 4.8 territory baseline.
Its inherited route step at (602.36, 1494.14) was corrected from Z=-15.35597
to the collision floor Z=296.77. The other 203 steps across the twelve retained
routes were within 2 m of a walkable surface. One fixed Looklook is 1.12 m
from a Mau Grain Sack; they are distinct positions and the sack's static ID is
unchanged. The editor's `scripts/import_58_odella_plantation.py` records the
boundary, source checks, backup, count audit, and mesh-grounded heights.
At that stage, every spot outside the three selected areas still matched Java
4.8. This is an on-disk XML update; the game server was not restarted.

Sonna and Gardar follow-up (2026-09-26): The user's client screenshot shows
both visibly suspended near the village cliff. Their ordinary Ishalgen XML
spots had *not* moved during the 4.8 reset and matched Java 4.8 exactly.
The editor's `scripts/import_58_sonna_gardar.py` now moves only Gardar 801218
and Sonna 801219 to their explicit 5.8 X/Y references, respectively
(556.129, 2426.967) and (555.431, 2429.317), with the 5.8 heading. Their
Z values are set to walkable collision floors 278.500 and 278.350 rather than
5.8 source Z 281.0 and 280.708, which would be 2.4–2.5 m above those floors.
Their 4.8 groups remain unconditional. The separate 5.8 gated TSV rows are
marked as overlaps and skipped by the current gated loader. (Correction,
2026-10-07: they were not. The TSV was not regenerated after this move, so
both rows still read `overlaps_static = FALSE` and the gated copies kept
standing at the 5.8 Z above the moved static ones. The suspended pair in the
screenshot was most likely those gated copies. The rows are marked since
2026-10-07; see `docs/retail-ai-fidelity.md`, "Two Ishalgen trainers stood
twice".) No other NPC
spots or group settings changed. The candidate passed the spawn XSD, nearby
spawn check, and byte-idempotence check. The running game server was not
restarted; the user's next rebuilt container needs an in-game visual check.

Aldelle Hill and Lake Tunapre mob pass (2026-09-26): Used the complete shipped
zone polygons and changed only mob positions in groups 210363, 210727, and
210590. Aldelle Hill mobs change 273 to 274; Lake Tunapre mobs change 216 to
210. Three 210363 fixed 5.8 references are already reached by protected walker
routes, and four ordinary spots were replaced. Nine fixed 210727 Fighter spots
replace eight nearby 4.8 spots, leaving one additional Fighter. Lake Hulker
210590 uses its single fixed 5.8 spot instead of seven 4.8 spots. Territory
anchor populations remain on the 4.8 baseline. The 210364 western fixed
references were left alone because nearby 4.8 walkers cover that patrol area;
5.8-only 210728 was not added. All 203xxx NPC and raider groups, field/quest
objects, other mobs, and areas outside these two polygons retain their prior
spawns. The editor's `scripts/import_58_aldelle_lake_mobs.py` records the
boundary, backup, source checks, counts, and mesh-grounded fixed heights.

The same pass corrected two inherited mob patrol height errors: the 210650
Lake Braxie route had 20 steps and its attached spawn center roughly 19.7 m
above the collision floor; the 210364 hill worker route had two steps about
13.9 m high. Only those Z values changed. The editor's
`scripts/repair_aldelle_lake_mob_patrol_heights.py` records both backups and
the before/after values. A repeat audit found no step more than 2 m off a
walkable surface across all 50 retained routes (1,586 steps) in the zones.
The spawn editor now offers a read-only gatherable
overlay sourced from the shipped Gather XML (93 Ishalgen spots). The running
game server was not restarted; in-game results require the user's rebuild.

Remaining Ishalgen mob review (2026-09-26): Inventoried every shipped SUB
zone against 5.8 fixed positions and territory anchors. Aldelle Basin's eight
fixed references are already represented by current spots or patrol routes.
The selective import replaced 24 nearby 4.8 mob spots: three Whitefoot Daru
in Munihele Forest, 14 mobs in Anturoon Crossing (including its overlapping
coast), and seven Thorned Ampha in Ishalgen Prison Camp. The 5.8 Fanged
Karnif 210655 coordinates use the existing Java 4.8 ID 210389. Every group
count stays the same; the Daru random-walk flag, all NPCs and quest objects,
group settings, and patrol routes are unchanged. Territory-only populations
remain on their 4.8 baseline. Nine fixed references in the selected groups
were withheld because their only 4.8 candidate was distant or the target
was occupied by another mob. Other 5.8-only level variants and distant prison
references were not introduced. The editor scripts
`scripts/review_58_remaining_ishalgen.py` and
`scripts/import_58_remaining_ishalgen_mobs.py` record the inventory, guarded
replacement, backup, and per-spot report. The candidate passed `spawns.xsd`,
a second import was byte-identical, and comparison with the backup found
exactly 24 changed mob spot records. Docker was not managed in this pass.

## Live observer packet parity (2026-09-26)

Pilot watched `Ishalgenbottwo` appear to
stand while moving and skip NPC-interaction presentation. The existing Pilot
recording sends ground `CM_MOVE` starts mainly as relative-velocity mask `0xC0`
and ordinary position updates as `0x80`; the active bot trace uses absolute
`0xE0` starts to destinations about 2 m away every 0.3 s and `0xA0` updates.
Java broadcasts starts/stops, not the position-only updates, to observers.
Pilot selects quest NPCs before `CM_SHOW_DIALOG`, generally keeps that target
until selecting another, and spends roughly a second on each dialog page; the
bot had selected no NPCs and advanced pages on the response. The bot now uses
ground `0xC0`/`0x80`, selects before interaction, paces dialog choices, and
sends `CM_CLOSE_DIALOG` on departure or completion. `ni10-observe-s2b` exited
cleanly, then the solution build, 186 focused unit tests, nav check, and the
Q2004 SIM journey passed. That SIM journey first exposed the restored 4.8
Sprigg spawns being beyond sight of Verdandi; the bot now walks to their
shipped hint instead of waiting at the quest giver. The Docker game server was
rebuilt with the current on-disk spawn XML and packet recording for Pilot,
Testsorc, and bot slot 3. `ni10-presentation-s3` started level-1
Ishalgenbotthree with these DLLs; its trace confirms NPC select, paced pages,
close dialog, and `0xC0`/`0x80` movement. It was stopped cleanly at level 2
because Pilot and Testsorc were still offline after the server restart; a
different fresh identity was reserved for the actual client observation.
Pilot reconnected, and `ni10-observe-presentation-s4` started level-1
Ishalgenbotfour on that identity. Pilot's server-side recording contains the
bot's `SM_PLAYER_INFO` and hundreds of `SM_MOVE` packets while both characters
are in Ishalgen; the bot reached level 2 with Pilot nearby. The live run remains
active at `http://127.0.0.1:17880/` for visual observation. The packet record
proves delivery to Pilot's connection; client-side animation quality still
needs Pilot's observation. Pilot reported combat, selection, and quests now
look natural, but the bot held an incorrect pose after collecting Sprigg grain
sacks. In the Pilot recording, each sack sends `END_QUESTLOOT` and opens its
loot list, then the bot immediately sends `CM_START_LOOT`, reopening that same
list and causing a second rapid loot-animation cycle. The recorded human's
median delay from loot list to item click is about 492 ms (25 samples). The
working-tree bot now consumes an already-open quest-object loot list and waits
450 ms before clicking its item. This edit is not in the running bot DLL; it
needs a build, focused SIM check, and a fresh client observation after the live
run ends. Pilot also saw the bot move while its stand-up animation was still
playing. Five recorded Pilot stand-to-first-move intervals were 343-361 ms;
five comparable bot intervals were 2-14 ms. The working-tree rest callback
now waits 350 ms after sending `STAND` before any defense or navigation action.
That edit likewise awaits the post-run build and observation.
At the operator's request, `ni10-observe-presentation-s4` quit normally after
34m39s on 2026-09-26. The `aion` game, login, chat, and MySQL containers and
the Aion Portal preview container were stopped. The dashboard is offline.
Keep the stack and runners stopped until the operator asks to start them again;
the grain-sack and stand-up edits remain unbuilt and unverified.

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

## SIM-only quest-hub optimization experiment (2026-09-26)

`NI07_OPTIMIZE_HUBS=1` enables an experimental scheduler only in the accelerated
SIM journey. The default NI-07 and LIVE routes remain unchanged. The mode uses a
fixed named-hub list, accepts every eligible quest from a *nearby client-observed*
starter NPC (within 45 m), works compatible template quests before returning to
claim them, and orders work by minimum level with blue before campaign within
safe groups. The groups retain the proven early campaign progression; the dense
eastern blue quests and Q2116 are not attempted before Q2007. It binds at the
ordinary Aldelle obelisk and uses observed Return readiness, fare, and Java 4.8
flight paths to choose a faster inter-quest trip.

The initially broad "accept everything assigned to this hub" pass caused long
starter detours and unsafe early Q2116 work. A local-starter limit and safe
quest groups fixed completion. The final `hubopt9` four-seed SIM evidence is:

| Seed | smart46 virtual s / deaths | hubopt9 virtual s / deaths | Difference |
|---|---:|---:|---:|
| 1 | 12,475 / 0 | 12,918 / 1 | +443 s |
| 3 | 12,127 / 0 | 13,291 / 0 | +1,164 s |
| 4 | 12,461 / 0 | 11,894 / 0 | −567 s |
| 5 | 12,141 / 0 | 11,781 / 0 | −360 s |

Both modes completed all 41 quests on all four seeds. Hub optimization averaged
170 s **slower** and added one Hatata-approach death, so it is not the new default.
The slow seeds spent extra time in Q2005–Q2007's changing Mau patrols. Seed 5
confirmed 25 nearby hub pickups, one flight, and two Returns. The trace tools
`aggro_causes.py`, `death_profile.py`, `hatata_profile.py`, and `step_times.py`
were used against `game-server/run/hubopt9-full-s{1,3,4,5}` and the smart46
baseline. A later Q2006 Return-plus-flight shortcut was exercised in `hubopt10`
seed 3, but two subsequent Q2007 deaths led us to revert that experiment.
Further rollout should target the Mau route and compare multiple seeds again;
the current policy is a measured opt-in, not a demonstrated general speedup.

The operator's live Docker stack and runners remain stopped as requested. These
SIM runs used their isolated fixture and did not restart that stack.

Follow-up validation exposed variance beyond the `hubopt9` sample. The final
working tree scopes the extra ranged hold to the opt-in mode, retains the normal
bot's stationary-ranged-target behavior, and retries a refused hub dialogue with
a short final movement. The 28 focused hub/rotation/combat tests and solution
build pass. A subsequent final-code seed-1 run (`hubopt14`) was stopped after two
Q2007 deaths; **the final experimental mode is not four-seed accepted**. Keep it
disabled and use smart46 for playtests. The full solution test run also has four
unrelated, pre-existing working-tree failures: three spawn/geo golden assertions
after the Ishalgen placement edits and one admin-properties collection assertion.

## Changed-spawn navigation follow-up (2026-09-27)

The retail-placement edits changed the encounters around Rae and the Mau camp. This
follow-up kept the normal quest order and the opt-in hub scheduler off. It did not
change shipped spawn coordinates to make the four familiar seeds pass. The final
working tree waits and reobserves after a stalled guarded fight-through, uses an
observed Return to reach the bound obelisk after Hatata when that route is shorter,
retains per-skill cooldowns from `SM_CASTSPELL_RESULT`, rejoins Q2005 from bind
after a revive instead of retracing breadcrumbs from before death, and accepts
client-observed 0%-HP kill evidence when a Q2127 target disappears during pull
planning. These are general recovery and client-state fixes, not pre-baked routes
to current mob coordinates.

The final-code `timing68` run folders under `run/natural-batch/` are the handoff
evidence: seeds **1, 4, and 5 completed all 41 quests**; seed 3 stopped at Q2007
after 10 guarded-route rejoin attempts. Deaths were **0/2/1/1**, respectively.
The stopped bot stood at (633.6, 913.4) with full HP while the observed hazards
covered its checked approaches to the rejoin NPC and nearby blockers. Its earlier
blue-generator death involved four attackers; the other two Q2007 deaths in this
batch involved two and three Stalkers. `aggro_causes.py` counted 16 respawns near
the bot and 12 patrol contacts across the four runs. The three completed Hatata
steps took 297/314/310 virtual seconds, with 8/8/10 kills and **no deaths**.
Seed 3 never reached Hatata. `death_profile.py`, `hatata_profile.py`, and
`step_times.py` were run on these folders; seed 3 spent 3,190 virtual seconds in
Q2007 before its stop. This is a route and add-control problem in the Mau camp,
not evidence that the isolated Hatata fight is too slow.

Build of `tests/Aion.Simulation.Tests` and the 196 filtered natural/timing/API
unit tests passed after the final code edits. The full solution and Fast checks
were not rerun at that point, and the working tree was **uncommitted** with unrelated
live-bot, spawn, and hub-scheduler edits. One narrow branch (the 10-second wait
after a stalled fight-through) was not observed in the final full-journey traces;
it needs focused validation before acceptance.
The Docker stack and live runners remain stopped. The read-only monitor was
available during the SIM runs and is offline now.

The next controlled step is Phase 0 of `docs/bot-learning-pilot.md`: lock two
level-9 Mau-area legs (generator-to-Rae rejoin and Rae-to-Hatata) against the
*current* spawn baseline and record an auditable deterministic-policy baseline
on development and reserved seeds. Keep Q2007 rejoin failures and multi-attacker
blue-generator pulls visible as separate outcomes; a short Hatata fight alone
would miss the observed failure.
No new human recording is required to define this benchmark. Pilot can record
the same area later if a client-observed decision remains ambiguous.

## Recovery and potion rule batch (2026-09-27)

The next same-code batch, `run/natural-batch/recovery69-full-s{1,3,4,5}`,
completed **4/4** with deaths **5/0/0/0**. The bot now heals HP with its
client-observed Priest skill between fights while observed mana is at least
50%. It sits only after mana falls below 50%, stands at 80%, then heals any
remaining HP. It uses an owned, ready timed HP potion at or below 90% HP in a
fight, provided the healing effect is not already active. The four traces
contain 66 between-fight heals and 275 combat timed-potion uses, compared with
161 timed-potion uses and 76 rest relocations in `timing68`. Rest relocations
fell to 19. These are usage counts, not a controlled causal estimate of deaths.

If a long fight-through route is empty, the planner now tries a terrain-checked
route to a nearby observed guard that lies toward the objective. If that also
fails, it may advance a short checked ground segment crossing at most two new
aggro circles, defend after each two points, and re-plan. This fallback is
bounded and uses no quest-specific coordinates. **None of the four full runs
invoked it**, so `recovery69` does not prove the old seed-3 route stall is
fixed. Do not claim the planner can always find a way through a dense pack.

`aggro_causes.py`, `death_profile.py`, `hatata_profile.py`, and `step_times.py`
were run on all four traces. Seed 1's four Q2007 deaths came from two- or
three-attacker engagements with repeated stun/knockdown refusals of Healing
Light; the fifth came from three attackers in Q2128. All four Hatata steps
were death-free (317/401/407/360 virtual seconds, 10/11/11/11 kills). Seed 5
spent **320 wall seconds in Q2006**: after a checked ingress return failed,
the planner repeatedly searched hazard-rejected routes from the same position
before recovering. Preserve it as a separate navigation performance regression
alongside the two Mau-area legs, rather than rerunning the full journey for it.

The build and 24 focused combat/navigation tests passed before the batch;
all four SIM journeys passed on those DLLs. Afterwards, the solution build
passed with 17 existing test-project nullable warnings and 197 filtered
natural/timing/API tests passed. A final policy-only correction preserved
critical life- and mana-potion precedence while idle; the journey's combat
decisions always have a target, so that branch was not part of the four runs.
The prior `timing68` had 3/4
completion and four deaths. Treat `recovery69` as **good enough for the
natural-journey prototype**, with deaths and slow route search still open.
The next work is the controlled Mau-area benchmark in `docs/bot-learning-pilot.md`,
then a small, inspectable combat-policy search aimed first at avoiding
multi-attacker pulls and failed healing under stun. Keep the deterministic
policy as the baseline and use more than the familiar four seeds before
adopting any learned policy. At that point the working tree was uncommitted; the live
Docker stack and runners remain stopped.

## Two-attacker healing and pack disengagement (2026-09-27)

The operator corrected the combat thresholds: Healing Light starts at 55% HP against
one observed attacker and 70% against two. Three or more observed attackers trigger
retreat at any HP, ahead of potion use or damage. A retreat keeps moving along
collision-checked ground until every pursuer broadcasts Java's neutral/return NPC
emotion or leaves client sight; new attackers join the retreat. If no checked
escape exists, the Priest fights from the corner rather than claiming it escaped.
The first `retreat70` four-seed batch completed 2/4 with nine deaths. Several
otherwise outward routes were rejected because their first points turned sideways
around terrain. The revised route check allows such a detour while rejecting a
route that actually enters the pack. It completed seeds 1 and 3 (`retreat71`)
without deaths; seed 4 then exposed a separate retry error: successful tactical
retreats consumed the six-attempt kill limit for Q2128's 210391 target.

The final same-DLL `retreat72` run folders under `run/natural-batch/` completed
**4/4**, all 41 quests, with deaths **3/3/1/1** and 19 retreat routes. Seven
Q2128 retreats in seed 4 were retried separately from failed kills. Four
retreats still found no checked escape and fell back to combat. The eight deaths
are worse than `recovery69`'s five; this four-seed comparison is not a causal
estimate because SIM spawn/patrol timing varies. `death_profile.py` shows repeated
stun/knockdown refusals of Healing Light against **two** Mau attackers in most
deaths; the three-attacker threshold does not address that case. Seeds 1 and 3
each had a Hatata-step death and a bind-rejoin death; seed 1 had one more
bind-rejoin death, while seed 3 had one more Q2007 death. Seeds 4 and 5 each
died once in Q2007. Hatata itself remained death-free on
seeds 4 and 5. `aggro_causes.py`, `death_profile.py`, `hatata_profile.py`, and
`step_times.py` were used on the traces. These requested deterministic rules are
implemented, but do not establish a no-death policy. Further threshold tuning
alone is unlikely to solve stun-locked two-on-one fights; the next controlled
Mau-area pilot should focus on preventing the second add and testing safe exits.
The working tree, including live/spawn/hub work, was uncommitted at this handoff.

## Commit checkpoint (2026-09-27)

The live attach runner, selected Ishalgen spawn baseline, opt-in hub scheduler,
changed-spawn navigation recovery, and the requested healing, potion, and retreat
rules are committed together after a clean checkout review. `game-server/run/`
contains local traces and login observations; it is now ignored, alongside the
already-ignored root `run/` evidence. Neither directory is part of the commit.
The Java 4.8 generator refreshed the geo oracle for 4,275 current starter points;
the finite soak hints now match shipped Ishalgen spawn coordinates.

The full solution build passed with zero warnings, and the solution test suite
passed with 4,862 tests and 45 gated skips. All remaining 30 `CLAUDE.md`
checks passed, including the warning baseline, Java geo parity, baked navigation,
NI-10 mock-Docker attach contract, and Docker Fast (11 manifest scenarios).
`retreat72` is the last four-seed full-journey evidence: 4/4 completion, eight
deaths. No new journey batch or human recording was run for this commit. The live
Docker stack and bot runners remain stopped; the next learning-pilot step is the
controlled Mau-area benchmark described above.

## Level 9 Mau learning pilot concluded; Ascension handoff (2026-09-28)

The two level 9 Mau courses and fixed SIM seed split are locked in
[Phase 0](bot-learning-phase0.md). [Phase 1](bot-learning-phase1.md) verifies
same-seed replay and records legal, client-observable decisions for full legs
and six short starts. These are reusable diagnostic facilities, not a change
to the normal quest journey. The experimental parameter vector remains
course-only; the normal journey uses the deterministic baseline.

[Phase 2](bot-learning-phase2.md) tried inspectable policy parameters with
paired development courses, then froze one guarded 91% timed-potion candidate
before opening 20 unseen seeds. It matched baseline completion on the 40
unseen course pairs but added a death on generator seed 118. The four familiar
regression seeds matched baseline. The candidate was rejected and no policy
was adopted. The [seed-118 trace diagnosis](bot-learning-phase2-seed118-diagnosis.json)
shows the first changed action and a later canceled Smite in a two-attacker
fight; six additional Q2007 route searches followed the extra death. It does
not establish a safe alternative. The full notable trace pairs are archived
with hashes in the [trace manifest](bot-learning-phase2-traces/manifest.json).
No model was trained and no new full-journey batch was run. Evaluation seeds
101–120 are spent and must not be treated as unseen for a later candidate.

Further level 9 tuning is **paused**. The current evidence did not yield a
practical safe policy improvement, and the operator is moving to Ascension
and subsequent leveling/questing to broaden the character's observed skills
and situations. The next session should begin with the Java 4.8 Ascension
quest implementation and the [parity backlog](Full-Parity-Backlog.md), then
identify the C# quest/leveling gaps before changing behavior. Preserve the
client-observed learned-skill rule: a level alone does not grant a skill.
Keep the existing deterministic journey as the regression baseline; use
focused checks before any new full-journey batch. The last full-journey
evidence remains `retreat72` (four completed seeds, eight deaths) from the
commit checkpoint above.

## Next leg: Ascension to Altgard as a Cleric (planned 2026-09-28)

D25 authorizes the next leg, an Ascension bridge: Q2008 choosing **Cleric**, the Q2009
ceremony in Pandaemonium, Q2904 "Dispatch to Altgard", the Altgard Fortress bind (700065),
Q24010, an Altgard shop stop (equip owned gear and accessories, sell junk, potions only), and
the start of level 10 Cleric play (powder rest, a buff-ourself check with help-item scrolls,
wait-or-fight patrol handling); the Karmic Staff is the ceremony weapon. Deaths are recorded,
not failures. The Ishalgen
leg above stays frozen as its regression baseline. The route spec, hazards and ordered TODO
list (NA-00..NA-28, worked in Loop mode, one commit per item, each step proved once) are in
[natural-ascension-altgard.md](natural-ascension-altgard.md). The C# server path was audited
against Java `ce54b7931` and matches; the work is bot-side (multi-map context, class-aware
identity/gear, the leg handlers), plus checked-in navigation for 320020000, 120010000 and
220030000.

## Ascension bridge acceptance in SIM (NA-25, 2026-09-28)

The natural journey now runs from character creation to Altgard in one SIM run with the bridge on
(`NA_ASCENSION=1 bash scripts/sim/run-natural-batch.sh <prefix> <seed>`). Seed 1 (`run/natural-batch/na25-full-s1`)
took 12 min of wall time and 4 h 26 min of game time:
- all 41 Ishalgen quests, then Q2008 (Cleric), Q2009 (Karmic Staff, level 10), Q2904, the Altgard Fortress
  bind, Q24010 and the shop stop;
- the endpoint verified across a relog;
- 3 deaths, all at the Q2007 generators, each recovered by a bind revive;
- 11 movies skipped;
- the approved help items supplied (OD-13).

The Altgard start is saved as the SIM snapshot `altgard` (`scripts/sim/sim-snapshot.ps1 -Action Restore -Name
altgard`, character 133297). Use it as the starting point for Altgard play instead of replaying 4 hours.

One bug surfaced only in this full run: Java lists Q2008 LOCKED from about level 8. The bridge's resume check must
not treat that as a start (fixed in NA-25).

Details, findings and the remaining acceptance item (NA-27, the isolated LIVE run) are in
[natural-ascension-altgard.md](natural-ascension-altgard.md). NA-26 ran every `CLAUDE.md` check on 2026-09-28,
and all pass. Docker Fast first failed on the NA-08 service-step test's history-wide log policy (fixed there).

## Ascension bridge accepted in LIVE (NA-27, 2026-09-29)

`pwsh -NoProfile -File scripts/live/run-live.ps1 -Run <name> -Scenario NI-09 -AscensionBridge -Bots 1 -WatcherMode enforce
-DashboardPort 17880 -StepTimeoutSeconds 120 -RunRoot run/na27-live` runs a fresh Priest on its own isolated stack from
creation to a level 10 Cleric bound in Altgard. The run supplies the approved help items through the director's
`//add` and keeps the endpoint databases (`altgard-live-dump.sql.gz`).

Attempt `na27-live-a3` passed in 4 h 22 min: all 41 Ishalgen quests, the whole bridge verified across a relog, an
ordinary-identity check, 3 deaths recovered, and a clean enforce watcher. The two earlier attempts found:
- a Return cast attempted while dead (now a bind revive);
- Lake Hulker's restored ineffective `pool="1"` (removed again).

Details are in [natural-ascension-altgard.md](natural-ascension-altgard.md).
