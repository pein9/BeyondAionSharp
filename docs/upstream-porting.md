# Upstream Java porting

Port merged Java changes in strict `4.8` history order. One upstream Java commit produces one completed C# commit or one explicit blocked record. Never merge or cherry-pick Java history into C#.

## Repository policy

- C# work happens locally on `main`; do not create port branches or pull requests.
- The C# repository must contain only `main` and must be clean before a package is prepared.
- The Java checkout is read-only behavioral reference data.
- `docs/upstream-port-state.json` is the queue cursor. Do not select a later commit manually.
- `docs/upstream-port-log.md` is the human ledger.
- Generated patches, prompts, reports, and logs belong under ignored `artifacts/upstream/`.

## Per-commit workflow

1. Run `scripts/upstream/scan-upstream.ps1` and confirm the first pending merged commit.
2. Run `scripts/upstream/prepare-next.ps1`; use its generated `prompt.md` and exact Java patch.
3. Explain the behavior, map every affected Java artifact to C#, and port only that commit.
4. Add focused regression coverage and run `scripts/upstream/validate-port.ps1`.
5. Run `scripts/upstream/complete-port.ps1` with the reviewed status and evidence-based notes.
6. Review and stage only the intended files, then commit with the generated `commit-message.txt`.
7. Run `scripts/upstream/verify-port.ps1` before considering the queue item complete.

`scripts/upstream/test-upstream-automation.ps1` self-tests these scripts against throwaway repositories; run it after changing any of them.

## Status rules

| CLI status | Ledger status | Advance cursor | Validation required |
|---|---|---:|---:|
| `ported` | Ported | Yes | Yes |
| `direct-data` | Direct data carryover | Yes | Yes |
| `not-applicable` | Not applicable | Yes | No, but precise evidence is required |
| `blocked` | Blocked | No | No, but the missing prerequisite is required |

A blocked record may be resolved later. The later completed C# commit uses the same Java SHA, replaces the ledger row, and advances the cursor. Later Java commits remain ineligible until then.

## Commit contract

Every tracker decision is a local C# commit with exactly one trailer pair:

```text
Upstream-Java-SHA: <40-character-sha>
Port-Status: ported|direct-data|not-applicable|blocked
```

## Review rules

- Java describes intent and observable behavior; C# uses established local infrastructure.
- Preserve ordering, null behavior, enum and ordinal semantics, numeric overflow, timing, packet layouts, persistence effects, and data compatibility.
- Do not bundle cleanup, redesign, or adjacent Java commits.
- Language-neutral XML, SQL, and configuration may be carried directly only after C# loader and model compatibility is verified.
- A green build alone is insufficient. Require focused regression coverage or a concrete explanation of the validation boundary.

## Mechanical Java-to-C# mappings

### Approved E2E shared-defect corrections

Decision D19 in `docs/e2e-player-simulation-plan.md` authorizes scoped corrections
for the shared character-transfer and Chat-gag defects (§7/118–119). Keep the
documented corrections when applying upstream changes; do not silently restore
the known defects in the name of parity. These are not new upstream queue items.

- Chat `ChatService.gagPlayer` receives a **duration in milliseconds** from Game's
  `ChatBanService` and `CM_CS_PLAYER_AUTH_RESPONSE`, while `ChatClient.gagTime` is an
  absolute epoch deadline. C# converts at `Services/ChatService.GagPlayer`: zero
  clears, otherwise deadline = current UTC epoch milliseconds + duration (ordinary
  signed-long addition). The wire field/layout, Game replay and Chat refusal path
  stay unchanged. Java reference: `ce54b7931`, E2E §7/119 / D19. Service and socket
  regressions pin duration conversion, replay replacement, immediate ungag and
  no forbidden-message broadcast.
- **Transfer nullable quest timestamps:** `QuestState` permits null dates, but Java
  `SM_PTRANSFER_CONTROL` dereferences them and `CMT_CHARACTER_INFORMATION` always
  constructs a timestamp. Under D19, C# reserves signed-long `MinValue` for null
  in those two existing fields. Both C# peers encode/decode it; real dates retain
  their epoch milliseconds, including epoch zero and negative epochs. Do not replace
  null with 1970 or change field widths/order. Mixed Java/C# transfer is not supported
  by this correction. Java reference `ce54b7931`; E2E §7/118.
- **Transfer section relay and activation:** Java Login `CM_PTRANSFER_CONTROL`
  drops Game actions 5–9, although Game's `CM_PTRANSFER_RESPONSE` defines their
  corresponding responses 24–28. C# relays those opaque payloads with the existing
  target format (task id, byte length, bytes), checking active request/source,
  target availability and sequential section order. Failed sends do not advance
  that order. This deliberately supplies the missing Login path, not new Game
  response formats. `AccountRepository.UpdateAccountAsync` additionally persists
  `activated`, omitted by Java `AccountDAO.updateAccount` despite transfer's
  deactivation/reactivation calls. Reference `ce54b7931`, D19 / E2E §7/118.
  A successful relay or persisted flag alone is not transfer acceptance.

### General mappings

| Java | C# | Notes |
|---|---|---|
| `LoggerFactory.getLogger(X.class)` | `AionLog.For(nameof(X))` | The returned logger is late-bound: it resolves the current `ILoggerFactory` when each message is written. |
| `LoggerFactory.getLogger("CATEGORY")` | `AionLog.For("CATEGORY")` | Preserve named Java categories exactly, including dedicated audit and gameplay log channels. |
| `System.currentTimeMillis()` | `SystemClock.CurrentMillis()` | Gameplay time must remain on the shared overridable clock; use `SystemClock.UtcNow()` when the C# API requires an instant. |
| `ImageIO.read` terrain rasters in `GeoWorldLoader` | `GeoEngine.PngReader` | Shipped PNGs are non-interlaced 16-bit gray heights or 8-bit gray/indexed materials. Preserve raw unsigned samples/palette indices, not converted colors. PNG filters 0–4 and split IDAT chunks are tested. |
| `GeoWorldLoader.load` / `GeoService.init` | `GeoWorldLoader.Load` / `GeoService.Init` | Load real binary meshes/placements, terrain, material zones and background parallel collision data. Empty C# `gameserver.geodata.map.ids` preserves all-map production loading; the optional explicit map filter is a SIM infrastructure seam. Measurement tests wait for collision preloading; production retains Java's background behavior. |
| `services.craft.CraftSkillUpdateService` | `Services.Craft.CraftSkillUpdateService` | Single canonical implementation; `getProfessionByNpc` maps to `Profession?` (unknown NPC is null, not enum ordinal zero). `EconomyServiceParityTests` and `NullableEnumEdgeParityTests` guard consolidation and all 36 trainer mappings. |
| `InventoryDAO.store` catches `SQLException` | `InventoryDAO.Store` catches `MySqlException` | Do not catch programming/infrastructure exceptions as SQL failures; `EconomyServiceParityTests` pins the catch contract and non-SQL propagation. |

## Follow-up after the merged Java queue is ported

Maintainer decision, 2026-10-19: retain these findings and revisit them after the
remaining merged Java commits have been ported in order. Do not bundle fixes for
them into an unrelated upstream port.

The extra Fast run during the `83b9e3322` port (`1a6fe36cf`) finished with **85
passed, 30 failed and 98 skipped**. Three representative failures also reproduced
with the exact pre-port `TeleportService`; that establishes that they predate this
port, not that every failure is a Java defect.

| Failures | Finding | Follow-up |
|---:|---|---|
| 14 | Thirteen world-entry refusals with message 6 (`REENTRY_TIME`), plus one character still online after `CM_QUIT`. C# follows Java's older delayed logout. | Recheck after `6f5feaceb` (#215), which makes a valid explicit quit leave immediately. Bots must also obey its new `CM_MAY_QUIT` and countdown protocol. The fix has not yet been validated against all fourteen failures. |
| 14 | Thirteen disposed bot transports and one unexpectedly ended transport. A diagnostic of `AbyssEntryFlightMeasuresTimeSpeedScrollAndTheFirstRing` confirmed a send-stall disconnect after **60,200 ms**, with **524 packets queued**. The simulator drains packets only after advancing virtual time. | Preserve the already-ported Java send-stall guard; investigate pumping/draining the simulator during long time advances. The other thirteen closed-session cases have not been individually instrumented. No remaining Java commit changes that guard. |
| 1 | `EastGateSpiritDialogsAndPollenTimerPlayThroughTheContract(203, 0)` found no Q2263 pollen after twelve attempts (`SimulationAltgardEastGateScriptTests.cs:108`). | Root cause unresolved. Java has the Q2263 handler and 80% drop configuration; neither is changed by the remaining queue. A Java-shared defect has not been established. |
| 1 | `FreshPriestCanCastLearnedReturnToNaturalBindLocation` timed out waiting for the opening `SM_PLAY_MOVIE` (`SimulationNaturalRoadTests.cs:25`), before casting Return. | Investigate the movie expectation/entry path; this does not establish a Return-skill defect. No direct fix was found in the remaining queue. |

Also retain the separate local validation drift: the clock-read check counts 22
direct reads against a baseline of 20 (three `Stopwatch.StartNew` sites versus
one), and generated custom quest-handler drafts differ from their checked-in
baseline. Both were present before `83b9e3322`; neither is a confirmed Java
gameplay defect. Review the drift without raising baselines merely to make checks
green.

Evidence is under ignored `artifacts/upstream/83b9e3322-fixed-the-pvp-price-of-teleports-to-vulnerable-f/`:
`fast-failures.json`, `failure-investigation.json`, `baseline-sim-comparison.log`,
and `diagnostic-{flight,reentry,pollen}.json`. The original Fast run is
`run/port-83b9e3322-20261019-135045/`.

After the queue is complete, rerun Fast, compare the remaining failures with this
record, and investigate each unresolved cause before claiming full validation.

### Simulator packet-draining follow-up (2026-10-19)

The socketless transport now registers with the shared virtual clock, serializes
queued packets before time moves and between timer bodies, and acknowledges each
completed frame through the existing write-progress callback. Parked clients are
pumped too; closing a client removes its registration. Production connection
guards and their queue/stall limits are unchanged.

Validation: 34 focused clock/transport/guard tests and 6,625 normal-suite tests pass
(252 gated skips); warning inventory remains 4,097 and fidelity/logger checks pass.
Replaying the fifteen transport failures plus the uphill shared-history probe gives
15 passes and one later failure: Destiny recovery now reaches Java's valid
`SM_QUIT_RESPONSE` refusal with a ten-second combat block. There are no disposed
transports, virtual-scheduler faults, or collection-cleanup failures in this rerun.
The bot quit contract needs a separate follow-up; preserve the server's refusal.
The pre-existing clock-read and generated quest-draft drift remain separately
recorded above. Evidence: ignored `artifacts/upstream/simulator-packet-draining/`.

### Bot contract review after the Java queue (2026-10-19)

Reviewed the client-facing changes in all 71 port records from `206f9b5db` through
`b0a597458`, including the bot adaptations already made during those ports. The
audit inputs and cumulative protocol/bot diffs are saved under ignored
`artifacts/upstream/bot-port-followup/`. The reference for the completed queue is
Java `b0a597458`, rather than the older working-tree HEAD of the Java checkout.

| Area | Bot consequence |
|---|---|
| Recipe limits (`e5c133836`) and entry | Preserve the recipe request/response handshake and observed limits. That handshake can consume the prologue movie, so SIM and LIVE startup callers now accept the matching race/quest movie from the current entry's history. Previous entries and other movies cannot satisfy this wait. Ordinary packet waits retain their existing behavior. |
| Quit/lost connection (`6f5feaceb`) | Preserve `CM_MAY_QUIT`, the countdown, combat refusals, immediate successful quit and lost-connection grace. The Destiny missed-window probe now interrupts its connection and verifies the ten-second grace; it no longer asks for a successful normal quit while Hellion is attacking. LIVE quit bookkeeping is reset even on refusal/cancellation. |
| Loot validation (`54880a691`) | The pollen probe now walks from its ranged attack position to the corpse before looting. Natural journey looting already approaches corpses. The server's range checks and Java's drop probability remain unchanged. |
| Quest discovery (`cabd580f3`, `616cb3c04`) | Regenerate the classifier from the expanded handler-spawn inventory and corrected Q23800 recipient (Migrak, 804753). Eighteen quests become obtainable: 18033, 18201–18203, 18206–18209, 23800, 28033, 28201–28202, 28206–28209, 30279 and 30379. Four are custom handlers (18208/18209/28208/28209); refresh their dialog drafts and the other changed registrations. |
| Quest routes (`44c8d4387`, `4617c9fd2`, `93d5fa15b`) | Keep the ported ring data, zone identifiers and Rae dialog/state adaptations. Regenerated handler drafts reflect the current handlers; template plans read the corrected server data. |
| Chains, flight zones, trade and macros | Keep the existing bot changes for hit-based chain deadlines/success flags, zone attributes, trade-tab lookup and signed macro chunks. These do not need a second rewrite. |
| Login and character/service packets | Keep the updated login framing. Review the game-auth passport field, appearance layout, and manastone selected-NPC requirement against the current builders. The bot's existing default values/field widths and selected-target behavior remain compatible. |
| Guarding, commerce, housing, pets, doors/chairs, broadcasts and visibility | Keep server validation and observe its results/known-object updates. Do not bypass guards or alter bot identity allocation. No additional packet changes were needed by the existing scenarios. Learned motion/emotion and pet-emote packets are not modeled as gameplay features by the current bots; this review does not claim new feature coverage for them. |
| Combat, AI, scheduling, logging and build-only changes | Preserve the ported behavior and existing native/retail fixes. These changes do not introduce new bot requests. |

The classifier now has 4,377 obtainable quests, 226 unreachable quests, 3,008
template plans and 942 custom handlers. Only the classifier population fields of
the coverage baseline changed; accepted/completed quest evidence is unchanged.
The retail inventory and its generated worklists now use the updated spawn
knowledge, with every existing nonempty maintainer status preserved. Dashboard
spawn metadata was regenerated; map artwork and calibrations are unchanged.

Validation: **6,629 normal-suite tests pass** (252 gated skips), and the complete
Fast run `post-port-bots-20261019` has **115 passes, 98 gated skips and zero
failures**. All nineteen failures from the immediate post-port rerun now pass,
including both pollen cases, fresh-priest Return, Destiny interruption/recovery,
the fixed-order manifest and the shared-history uphill probe. The compiler's
fourteen checks, retail inventory/worklist's twenty-nine checks, custom-draft,
dashboard, fidelity and logger checks pass. Warning inventory stays at 4,097.
LIVE helpers compile; a LIVE server playthrough and the Full tier were not run.
The unrelated clock-read ratchet still reports 22 reads against 20; its baseline
and production clock code are unchanged. Detailed results are in ignored
`artifacts/upstream/bot-port-followup/validation.json` and
`run/post-port-bots-20261019/report.md`.
