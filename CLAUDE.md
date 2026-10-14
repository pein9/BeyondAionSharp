# CLAUDE.md

This is a **Java → C# port** of the Aion server (the `aionemu` codebase). The Java
source is the reference implementation; the C# port exists to match its behavior 1:1.

**The project goal (the maintainer, 2026-09-30): parity with Aion 4.8 retail as it was
historically.** The Java source is the base this port started from, not the target; it has
bugs and missing content of its own. Java stays the default reference below, because it is
usually right and the port was checked against it. Where 4.8 retail evidence shows Java wrong
or missing, retail wins, under a logged decision: the retail AI exception, D19, D26–D31, or D32.

## Where this leads: the long-term goals

Set by the maintainer on 2026-10-06 and restated on 2026-10-12: the bots have two functions, to
play-test the whole game and to be companions for human players. Every bot leg, snapshot and
fix serves these:

1. **A server play-tested by our bots.** The bots play the game the way a player does. We
   find and fix bugs as we encounter them. That means every quest in the game: a leg plays its zone
   out, and what cannot be played yet is written down with its reason (the maintainer,
   2026-10-13).
2. **An intelligent bot that can play the whole game:** all quests, gathering, crafting and
   the rest.
3. **Bots that are companions for a human player,** as an extra healer or DPS. The player
   leads; a chat command brings a bot of a class, and it follows and plays its role. Started
   on 2026-10-12, beside the first two: the simulated runs are how each class learned its
   fight and its gear. The plan is `docs/companion-bots.md`.

A bug the bots find is fixed under the rules below: read the Java first, and let retail win
only under a logged decision. The leg being worked now, and its checklist, is in the
natural-bot documents under [Where things live](#where-things-live).

Two standing rules for every leg after level 26 (the maintainer, 2026-10-13; `docs/natural-muster.md`, MR-h and
MR-i). **The bots are the original eight classes:** Gladiator, Templar, Assassin, Ranger, Sorcerer, Spirit Master,
Cleric and Chanter. The Rider, the Gunner and the Bard stay as they stand at level 26 and get nothing new.
**Gear check-ins at levels 31, 36, 41 and 46:** the coin gear of the level, every piece that beats what is worn.

## Golden rule: the Java source is the spec

- **Before fixing or porting anything, read the corresponding Java implementation first**
  and mirror it. Do not infer intended behavior from the C# alone — the C# may be a
  faithful port of a Java quirk (keep it) or "reworked" code that diverged (fix it to
  match Java). The Java tree is the source of truth.
- When Java and C# disagree, **Java wins** — except pure infrastructure
  (DI, lifecycle, threading, sockets), where idiomatic C# is acceptable.

**Retail AI exception:** NPC AI behavior sourced from NCSoft's own retail AI
pattern data outranks aionemu, whose version is an approximation. Those changes are
logged in `docs/retail-ai-fidelity.md` — read it before "correcting" NPC skill,
summon, or shout data back toward Java, and add to it when making such a change.

**The retail dump is 5.8; this port is 4.8.** The exception above is about *behavior*,
not about content: retail names npcs, skills, routes and mechanics that 4.8 does not
have, and those are **boundaries to record, not gaps to close**. Never add a 4.8
template, spawn or skill so that a 5.8 pattern will fit. Every extractor under
`tools/client-extract/` refuses what this port does not have and prints the count —
keep it that way, and see section E of `docs/retail-ai-backlog.md` for what the
boundary currently costs.

**Approved shared-defect corrections:** E2E decision D19 in
`docs/e2e-player-simulation-plan.md` authorized narrowly scoped C# fixes for the
Java-shared character-transfer and Chat-gag defects (§7/118–119). The Chat-gag
correction remains approved and must not be silently ported back. D20 supersedes
the transfer half: this is a single-server emulator and character transfer is not
a supported product journey, so do not extend or runtime-validate that dormant
path. D26 and D27 approve two more: registering Borender (203572) for Q2209 "The
Scribbler" and Lamir (203620) for Q2223 "A Mythical Monster", whose Java handlers never
register them (reported upstream, `docs/upstream-reports/`). D28–D31 approve the same
one-line fix for Annju (204151) in Q2916, the teleport devices (730888, 730898) in
Q14031 and Q24031, and Koray (799585) in Q18400. All six are applied in C# (§7/57 and
§7/141–145; D26 and D27 have tests in `SimulationQuestCorrectionTests.cs`) and must not
be ported back.
D33 approves guarding Q1044's and Q2042's die and enter-world hooks so they end
only their own ring-course timer, not every player's running quest timer (§7/152,
`SimulationQuestTimerCorrectionTests.cs`); it must not be ported back either.
D34 approves limiting Q2947's timer-end hook to the player's own arena attempt and
destroying a failed attempt's arena instance, so every attempt starts in a new one
(§7/156, same test file); a death is left as Java has it, and it must not be ported back.
D35 approves Garm's SETPRO3 making a new arena instance and teleporting the player into
it, as 4.8 retail's text and Java's Elyos twin Q1922 do (§7/157, same test file); it must
not be ported back either.
D36 approves two more retail corrections of Q2947: a death fails the arena attempt at once,
and the arena has NCSoft's twelve spirits, not Java's eleven (§7/158–159, same test file);
neither is ported back.
D37 approves writing the sixth int64 the 4.8 client reads from `SM_STATUPDATE_EXP`, always
zero, where Java writes five (§7/160, `GoldenPacketFixtureTests`). It is the client's Blessing
of Wisdom pool; the mechanic itself is not implemented. It must not be ported back.
D38 approves giving the two quest arenas (Triniel 320090000, Sanctum 310080000) instance handlers, so a player who dies
there is offered the instance revive and stands up inside at NCSoft's own point, as in retail (§7/161, same test file as
D34–D36). It must not be ported back.
These decisions are not a general exemption from parity or authorization to
reopen other deferred behavior.

**Retail 4.8 quest completion (D32):** quests that the 4.8 client ships and 4.8 retail ran,
but that Java has no handler for, may get C# handlers (and the data they cannot run without).
Each is tested, logged as a deviation, and offered upstream. The plan, sources, rules and
register are in `docs/retail-quest-completion.md`; read it before adding any quest.

## Always watch for Java ↔ C# semantic gaps

These differ in ways that silently change behavior. Check them on every port/fix:

- **Null vs throw** — Java `ResultSet.getString()`, `Map.get()`, etc. return `null`;
  the C# equivalents (`GetString()`, dictionary indexer) **throw**. A throw mid-loop can
  abort an entire multi-row load.
- **enum ordinal** — Java `ordinal()` ≠ C# `(int)enum` when the C# enum has explicit /
  non-sequential values. Use `Array.IndexOf(Enum.GetValues(typeof(E)), e)`.
- **String hash on the wire** — the client expects Java `String.hashCode()` (`31*h + c`),
  not C# `string.GetHashCode()`.
- **DateTime Kind** — Java local/UTC handling vs C# `DateTimeKind`; mismatches corrupt
  timestamps.
- **Nullable enums from XML** — a missing `@XmlAttribute` enum defaults to ordinal-0 in
  C#, not `null`.
- **Numbers/char/division/overflow** — verify semantics whenever the value goes over the
  wire or into a calculation.

The Java checkout is expected at `../aion-server` by default. Set `BEYOND_AION_JAVA_ROOT`
when it lives elsewhere.

## Where things live

| What | Path |
|---|---|
| **Java reference (the spec)** | separate sibling `../aion-server` checkout, branch `4.8` |
| **C# port** | repository root (solution `AionServer.slnx`, target `net10.0`) |
| Authoritative parity backlog | `docs/Full-Parity-Backlog.md` — read before doing parity work |
| Upstream update queue | `docs/upstream-port-log.md` and `docs/upstream-porting.md` |
| Retail AI: what is left | `docs/retail-ai-backlog.md` — **read this first** |
| Retail AI log (why each decision was made) | `docs/retail-ai-fidelity.md` — a running log, not a to-do list |
| Retail AI data (generated, do not hand-edit) | `game-server/data/static_data/pattern_tables/*.xml`, `.../guard_answers/` |
| Retail AI extractors and emitters | `tools/client-extract/` — `regen_check.py` runs the whole pipeline |
| Run / DB / setup guide | `RUNNING.md` |
| Automated player simulation (bots, SIM/LIVE modes, live log watching) | `docs/e2e-player-simulation-plan.md` — phased plan with TODOs |
| Natural Ishalgen Priest: current state, batch runner, open items | `docs/natural-ishalgen-status.md`; `scripts/sim/run-natural-batch.sh <prefix> <seeds>`; trace analysis `scripts/sim/trace/`; play it on your running world: `scripts/live/attach-live.ps1 -Target aion` (NI-10, D24) |
| Natural Ascension → Altgard (Cleric) leg: route spec, hazards, TODO list and loop protocol | `docs/natural-ascension-altgard.md` — read it first when working that list (D25) |
| Natural Altgard leveling leg: approved quest list and route, flight rules, Leg 1 (fortress) TODO list | `docs/natural-altgard-leveling.md` — read it first when working the AF list |
| Natural Morheim arrival and Abyss-entry leg (Q24020, then Q2945 → Q2946 → Q2947 → Q2042, with the level-21 and level-26 coin armor): route, hazards, AX checklist, operator decisions and defaults | `docs/natural-abyss-entry.md` — read it first when working the AX list; starts from snapshot `altgard-rc-complete-s1` and ends at `morheim-abyss-entry-s1` (Cleric 26, AX-14) |
| Natural class profiles (D39): the class seam, the Chanter branch at Ascension, levels 1-9 for every starter class, the level 1-9 help kit and the Ishalgen hub binds: seam design, standing rules, per-class notes, hazards, CP checklist, operator decisions and loop prompt | `docs/natural-class-profiles.md` — read it first when working the CP list; the accepted line stays the Priest who becomes a Cleric |
| Natural all classes to NTC-ready (D40): the Priest and Cleric on the generic table rules, the bridge and every leg opened by class line, and every Asmodian second class played to `ntc-ready-<class>-s1` (Abyss-entry quests complete): decisions and defaults, rules, NR checklist and loop prompt | `docs/natural-all-classes-ntc.md` — read it first when working the NR list; it follows the closed class-profile plan and uses its rules |
| One world for every bot character, and the muster after the Abyss entry (2026-10-12): the group world `ntc-group-s5`, no coin limit, the best usable consumables and power shard from level 10 (shards always on), the bag cleanup at every gear check and restock (use it or get rid of it), level 26 by the fortress's repeatable coin quest, and each class's stigma stone in its slot (2026-10-13, rule MR-g, `NaturalStigma`) | `docs/natural-muster.md` — read it first before any run that makes or changes a character the group uses; `scripts/sim/join-world.py`; round stage `muster` |
| The Cleric's healing-only role and basic group mechanics (done 2026-10-12): formation with the Templar as leader, F1 following, heal and dispel without ever fighting, resurrection waiting, round-robin loot; stage `group-coin`, groups of two to six on Q2318 | `docs/natural-group-healer.md` — read it first before changing group play; it follows `docs/natural-muster.md` and starts from the group world |
| Group NTC leg (played 2026-10-12): six in one group through Nochsana Training Camp to the General, its Morheim quests, the chests one member at a time, the disband as the way out; stage `group-ntc`, round `run/ntc/n9.json`; one step open (Q4732 under D32) | `docs/natural-group-ntc.md` — read it first before changing group play in an instance; its report has the numbers; every attempt restores the group world, `ntc-group-s5` now |
| Companion bots (D41, played in SIM 2026-10-13, C1 to C9 done; its report names what a live world still needs): `.companion <class>` brings a throwaway level-26 bot that follows the player, who always leads; damage classes attack the marked target, the Cleric heals; no quests, no loot; SIM only for now, with a test stand-in as the player; steps C1 to C9. The server's part is `src/Aion.GameServer/Custom/Companion`, which has no Java counterpart (deviation 162) and is off unless `AION_COMPANIONS=true` | `docs/companion-bots.md` — read it first when working the C list; it follows the two group documents; profiles (generated, do not hand-edit) in `game-server/data/companions/`, made by `scripts/sim/make-companion-profiles.py`. The live host is scenario CH-01 of `tools/Aion.LiveBots`. The Docker stack has companions on by default (the maintainer, 2026-10-13): its game server starts with `AION_COMPANIONS=true` and `AION_COMPANION_ACCOUNTS=companion*`, and its service `companionhost` (`docker/companionhost`) is the host, built and started by the nightly `docker/RestartAll.ps1` too. `scripts/live/companion-host.ps1 -Target aion` runs the checkout's bot code in its place, once that service is stopped; its plan, steps L1 to L5 and everything built from the operator's tries in game (flight, the stuck teleport, the supply check, skip-ahead, the step aside, the mana potion, crowd control by the Zzz brand, seats on demand) are in `docs/companion-live-host.md`. While his stack is up a SIM run needs `AION_SIM_DB_CONTAINER=aion-sim-mysql AION_SIM_DB_PORT=3307`, and `AION_BOT_DASHBOARD_PORT=17881` while a host started by the script holds the monitor port (the stack's own host opens none) |
| Natural Morheim leg (a plan, 2026-10-13; M1, the quest list, is done): the eight original classes play every quest of Morheim, alone hub by hub from `ntc-group-s5` with the gear check-ins at 31 and 36 on the way, then the group quests as groups; the M checklist, the operator's decisions and the loop prompt | `docs/natural-morheim-leveling.md` — read it first when working the M list; its rules are those of `docs/natural-muster.md`. The quest list by hub (generated, do not hand-edit) is `docs/natural-morheim-quests.md`, made by `scripts/sim/zone-quest-list.py` |
| Retail 4.8 quest completion (D32): the quests Java lacks, sources, TODO list and register | `docs/retail-quest-completion.md` — read it first when adding a quest; the one-line-per-quest work list with the maintainer's Status column is `docs/retail-quest-worklist.md` |
| Bot navigation (navmesh, roads, travel graph; generated, do not hand-edit) | `docs/bot-navigation.md`; data in `game-server/data/nav/`; tools `tools/Aion.NavBake`, `tools/nav/` |
| Session recording (every packet of chosen accounts, for replay) | `docs/session-recording.md`; `AION_RECORD`; reader `tools/recording/recording.py` |
| Live server nightly restart/deploy (3 AM task "Aion RestartAll") | `docker/RestartAll.ps1`; how it works in `docker/README.md`; backups and logs in `%USERPROFILE%\Documents\AionBackups` |

**The live server deploys from this checkout.** Every night at 3 AM, `docker/RestartAll.ps1` builds what
is committed on `main` here and restarts the live Docker stack. It skips the deploy if the tree has
uncommitted or untracked files or is not on `main`. A schema change (`*/sql/aion_*.sql`) also needs its
migration applied to the live databases by hand: the script will not deploy new code while the live
schema differs from those files.

## Build & test (C#)

```bash
dotnet build AionServer.slnx
dotnet test  AionServer.slnx        # golden/parity suite + unit tests
pwsh -NoProfile -File scripts/ci/check-warning-baseline.ps1   # run before every commit
pwsh -NoProfile -File scripts/ci/check-null-loggers.ps1       # prevent silent source loggers
pwsh -NoProfile -File scripts/ci/check-clock-reads.ps1        # direct game clock reads may only shrink
pwsh -NoProfile -File scripts/ci/check-custom-quest-drafts.ps1 # Roslyn custom-quest draft drift
python scripts/parity/check_fidelity.py                        # structural-fidelity check
python scripts/e2e/test-quest-plan-compiler.py                 # quest plan/classifier drift; checked-in D32/Altgard plans recompile identically
python scripts/e2e/test-retail-quest-inventory.py             # D32 retail quest inventory, classes and work list drift (client optional)
python scripts/e2e/test-data-sweep-report.py                   # exhaustive sweep evidence/baseline contract
python scripts/e2e/test-soak-telemetry.py                      # capacity heartbeat/plateau/latency evidence gate
python scripts/e2e/test-soak-acceptance.py                     # all soak evidence gates must agree
python scripts/e2e/test-mutation-runner.py                     # mutation verdicts, prerequisites and exact restoration
python scripts/e2e/test-run-report.py                          # report evidence joins, failures, fingerprints and metrics
python scripts/e2e/test-packet-coverage.py                     # frozen opcode catalogs, raw/tap distinction and identity floors
python scripts/e2e/test-code-coverage.py                       # Coverlet provenance, unique line/branch unions and scoped deltas
python scripts/e2e/test-flake-policy.py                        # raw retry evidence and three-in-ten Full-run quarantine policy
python scripts/e2e/test-full-flake.py                          # Full retry joins, coverage selection, quarantine and atomic history
python scripts/e2e/test-full-promotion.py                      # aggregate-only problem promotion, complete plan and fresh evidence
pwsh -NoProfile -File scripts/e2e/test-full-flake.ps1          # actual Full/soak finalizers retain history and build identity
pwsh -NoProfile -File scripts/e2e/test-live-retry.ps1          # sequential single retry, original failures and no SIM retries
pwsh -NoProfile -File scripts/sim/test-code-coverage.ps1       # collector requests, attachment/restoration failures and runner selection
pwsh -NoProfile -File scripts/sim/test-sim-snapshot.ps1        # snapshot, Replay and neutral-gate contract (fake Docker, fake journey)
python scripts/sim/trace/test_compare_traces.py                # trace comparer: differences, coverage counts and the baseline digest
python scripts/sim/make-companion-profiles.py --check          # companion profiles: items exist and are wearable; identical to a fresh generation when the snapshot is here
AION_SIM_COMPANION=1 AION_SIM_DB_INTEGRATION=1 dotnet test tests/Aion.Simulation.Tests --filter "FullyQualifiedName~Companion" # companion server tests (D41), in a process of their own (Docker)
pwsh -NoProfile -File scripts/e2e/test-run-report.ps1            # report finalization and runner failure paths
pwsh -NoProfile -File scripts/live/test-run-soak.ps1           # acceptance runner propagation and failure paths
pwsh -NoProfile -File scripts/live/test-lifecycle-controller.ps1 # isolated crash ownership and saved-state gates (mock Docker)
pwsh -NoProfile -File scripts/live/test-hang-probe-contract.ps1 # hang probe target/identity safeguards (mock Docker)
pwsh -NoProfile -File scripts/live/test-cross-server-contract.ps1 # topology identity safeguards and read-only Compose resolution
pwsh -NoProfile -File scripts/live/test-hardware-ban-controller.ps1 # exact seasonal epochs and owned Login fault safeguards (mock Docker)
pwsh -NoProfile -File scripts/live/test-run-retention.ps1       # Full child runs must preserve sibling evidence
pwsh -NoProfile -File scripts/live/test-attach-live.ps1         # NI-10 attach can only read the operator's world (mock Docker)
pwsh -NoProfile -File scripts/live/test-companion-host.ps1      # CH-01 companion host only reads the operator's world and refuses one that takes none of its seats (mock Docker)
pwsh -NoProfile -File scripts/e2e/test-full-suite.ps1            # breadth/soak selection and fail-fast orchestration
pwsh -NoProfile -File docker/test-RestartAll.ps1                # nightly live restart/deploy decisions (mock Docker and git)
dotnet run --project tools/Aion.NavBake -- check --maps baked # checked-in bot navmeshes match their inputs
pwsh -NoProfile -File scripts/e2e/run-fast.ps1                 # additionally, before gameplay-change commits (Docker)
```

There is no hosted CI: these checks run locally. The baseline script rebuilds everything and **fails if
the compiler warning count rises above `scripts/ci/warning-baseline.json`**. `dotnet build` and
`dotnet test` succeed with new warnings, so only the baseline script catches them. Fix new warnings
rather than raising the baseline (`-UpdateBaseline` is only for recording reviewed reductions). The clock-read
ratchet follows the same rule while gameplay time is migrated to `SystemClock`.

For upstream fixes, port one Java commit at a time and include an
`Upstream-Java-SHA` trailer. Never merge or cherry-pick Java history into `main`.

Run the stack (separate terminals, in order — details in `RUNNING.md`):

```bash
dotnet run --project src/Aion.LoginServer
dotnet run --project src/Aion.ChatServer
dotnet run --project src/Aion.GameServer
```
