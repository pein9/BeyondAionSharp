# Deep Port Audit Remediation Tracker

**Started:** 2026-07-13

**Source audit:** [Deep-Port-Audit-2026-07-13.md](Deep-Port-Audit-2026-07-13.md)

**C# starting baseline:** `main` at `c677d77c7122b13126420840117ddeaf33fafa2e`

**Java specification baseline:** `4.8` at `59f65a9561bfa655eb24134da88ba3121c66ee8a`

**Overall state:** Close to resolved — all scoped code/automation work is complete; five findings await live operator journeys

## Status rules

Statuses in this tracker are evidence-based:

- **Not started:** no remediation implementation has begun.
- **In progress:** Java comparison and/or implementation is underway.
- **Code complete:** the scoped implementation and focused regression tests pass, but a required integration or release-gate check remains.
- **Verified:** every acceptance item for the finding is checked and the relevant gate test passes.
- **Blocked:** progress requires an external decision or unavailable environment; the blocker must be written in the finding row.
- **Accepted:** an intentional Java divergence was approved and documented. This must not be used merely because a fix is difficult.

A finding is not counted as resolved until it is **Verified**. “Code complete” is the tracker's close-to-resolved state.

## Baseline evidence

| Check | Starting result | Latest result |
|---|---:|---:|
| `dotnet test AionServer.slnx --no-build --no-restore -v:minimal` | 812 passed, 0 failed | **1,002 passed, 0 failed, 0 skipped** |
| `dotnet build AionServer.slnx -t:Rebuild -v:minimal` | 0 errors, 4,359 warnings | **0 errors, 4,325 warnings**; ratchet passes at 4,318 unique sites / 21 codes |
| `python scripts/parity/check_fidelity.py` | Passed | Passed |
| Audit/tracker/backlog local links | 69 valid, 0 broken at initial audit | **77 valid, 0 broken** after final reconciliation |

The baseline is not proof of runtime parity; it records the starting point against which regressions are measured.

## Overall progress

| Status | Count |
|---|---:|
| Verified | 13 |
| Code complete | 5 |
| In progress | 0 |
| Not started | 0 |
| Blocked | 0 |
| Accepted | 0 |

## Gate summary

| Gate | Findings | State | Exit evidence |
|---|---|---|---|
| Release Gate 1 — primary cross-server flows | BA-001, BA-002, BA-003, BA-004 | Close to resolved | BA-004 Verified; BA-001/002/003 await two-GS and retail-client/multi-process journeys |
| Release Gate 2 — gameplay and temporal data | BA-005, BA-006, BA-007 | Close to resolved | BA-007 Verified; BA-005/006 await in-world siege and full hardware-ban restart journeys |
| Hardening and setup Gate | BA-008 through BA-018 | Verified | Semantic/schema suites, static-data atomicity, warning ratchet, full solution, and fidelity checks pass |
| Completion Audit | All findings and backlog | Close to resolved | 13 Verified; five Code complete; final operator journeys listed below |

## Release Gate 1 — primary cross-server flows

### BA-001 — Character-transfer opcode

**Status:** Code complete

**Current work:** Implementation and focused tests pass. P10-09 now provisions two real C# GS/Chat pairs
and runs ordinary L0 setup followed by the real transfer queue. `p10-09-transfer-b` remains ACTIVE with
no target clone and logs a nullable quest-date serialization failure (`93be1a24`). The Java source shares
that defect and the already-recorded missing multi-section forwarding; `AccountDAO.updateAccount` also
does not persist activation changes. No non-Java protocol extension or error allowance was added.
The full journey remains open; see `docs/e2e-phase10-validation.md` and simulation-plan §7/118.

- [x] Every constructor writes `[0D, action, ...]` exactly as Java.
- [x] LS factory selects transfer control in the authenticated state.
- [x] Focused byte-golden and factory tests pass (Game Server bridge 10/10; Login protocol/transfer 30/30).
- [ ] Two-GS transfer covers the Java-reachable control flow (GS actions `1..4`, LS responses `20..23`) without unrelated state mutation; parser tests cover Java's dormant response cases `24..28`.
- [x] Full solution tests pass (1,002/1,002).

### BA-002 — Chat authentication response

**Status:** Code complete

**Current work:** Production response handling and focused loopback tests pass. P10-09 LIVE `B2` now
proves ordinary authentication, back-to-back auth responses with fresh tokens, and Chat-client
disconnect/reconnect before failing gag enforcement. Java and C# both send a duration but compare it
as an absolute expiry in Chat (simulation-plan §7/119). No production change or allowance was added.
LIVE `B2F` separately verifies an owned Chat crash/restart, a bounded unavailable-auth client deadline
with continued Game responses, and fresh Chat delivery after normal bridge recovery. The gag/ungag
journey remains open; optional C# callback API internals retain their focused loopback coverage.

- [x] The real client request path consumes Chat opcode `0x01` by resolving Java's current World player.
- [x] The exact token reaches the client in `SM_CHAT_INIT`.
- [x] Gag state matches Java (focused replay test: 300,000 ms remaining).
- [x] Tagged staff nickname behavior matches `player.getName(true)`, including Java `%s` tag substitution.
- [ ] Success, gagged, timeout/disconnect, and duplicate-request tests pass.
  - [x] LIVE B2 authenticates ordinary players and delivers channel messages to both (`p10-09-chat-b`).
  - [x] Back-to-back Game auth requests yield distinct tokens with stable account digest; latest token authenticates.
  - [x] Chat-client FIN/peer-close followed by fresh authentication restores message delivery.
  - [ ] Gag replay/enforcement: B2 captures forbidden text at both players (§7/119); ungag/final logout are unreached.
  - [x] LIVE B2F observes both Chat socket losses, no auth response during a three-second client deadline,
    continued Game replies, and fresh auth/delivery after an owned Chat restart without restarting Game.
- [x] Full solution tests pass (1,002/1,002).

### BA-003 — Complete GS↔LS response surface

**Status:** Code complete

**Current work:** The complete Java opcode/state table, post-auth account synchronization, runtime handlers, and focused parser/dispatch tests pass. LIVE B3 now proves duplicate-login refusal/kick, fast reconnect to the existing character without password fallback, consumed-key replay refusal, access grant/revoke, and an account-only ban with natural expiry/re-entry. Hardware-ban synchronization and the full transfer journey remain open; this does not close the release gate.

- [x] `0x02` kick/duplicate-login behavior matches Java.
- [x] `0x03` fast reconnect returns and consumes the reconnect key correctly.
- [x] `0x04` control response updates live account state and sends Java-equivalent feedback.
- [x] `0x05` ban response sends Java-equivalent feedback.
- [x] Post-auth GS account list opcode `0x04` is sent even when the list is empty.
- [x] `0x09` MAC and `0x0A` HDD lists populate the GS managers; entry counts are consumed incrementally instead of sizing peer-controlled arrays, with huge/truncated and Java-negative-count regressions pinned.
- [x] `0x0C` dispatch reaches all transfer response actions `20..28`.
- [x] Factory state/opcode tests cover every active legal opcode and illegal-state rejection.
- [ ] Loopback journeys cover duplicate login, kick, reconnect, grant, ban, hardware-ban sync, and transfer.
  - [x] LIVE B3: duplicate login returns ALREADY_LOGIN and kicks the original Game session with the expected message.
  - [x] LIVE B3: reconnect key authenticates a new Login session, replay closes, and the authenticated socket selects Game and recovers the same character.
  - [x] LIVE B3: director and subject see grant/revoke feedback; read-only live state verifies 0→1→0, and revocation survives relogin. No gameplay/login occurs at access 1.
  - [x] LIVE B3: account-only one-minute ban acknowledges, kicks without a duplicate-login notification requirement, refuses two fresh logins, and expires naturally before successful re-entry/logout.
  - [ ] MAC/HDD synchronization/enforcement and transfer still require their own full LIVE proofs.
- [x] `docs/Full-Parity-Backlog.md` §I1 reflects the implemented Java 4.8 opcode/state ownership.
- [x] Full solution tests pass (1,002/1,002).

### BA-004 — Bridge retry, reconnect, and fault isolation

**Status:** Verified

**Verification:** The supervised, generation-safe reconnect implementation, focused fault/lifecycle suite, and full solution pass. BA-001/002/003 retain the broader operator journeys that are outside this lifecycle finding.

- [x] LS and Chat initial connection failures retry with cancellation-aware bounded backoff.
- [x] Transport loss reconnects and reauthenticates without restarting GS.
- [x] LS reconnect resends the logged-in account list and reloads hardware bans.
- [x] Pending login/Chat requests fail and clear on disconnect.
- [x] One decoded packet handler exception is contained without losing the bridge.
- [x] Framing/protocol/transport failures still close and recover the connection safely.
- [x] Late-start, restart, and one-shot handler-failure integration tests pass.
- [x] Full solution tests pass (1,002/1,002).

## Release Gate 2 — enabled gameplay and temporal data

### BA-005 — Siege nested JAXB callbacks

**Status:** Code complete

**Current work:** Nested callback/binding fixes and exhaustive shipped-data invariants pass; a live world-object gate repair and full assault remain runtime QA.

- [x] `DoorRepairData` and `AssaultData` callbacks run before the parent location indexes.
- [x] `DoorRepairStone.static_id` binds through a public XML member/proxy.
- [x] Real-data test resolves fortress `1131`, stone `199`, door `53`.
- [x] A known assault location has non-empty teleport, commander, and combat lists.
- [ ] One gate repair and one automatic fortress assault pass QA/integration coverage.
- [x] Full solution tests pass (1,002/1,002).

### BA-006 — Database options and time semantics

**Status:** Code complete

**Current work:** The full instant-valued DAO sweep is complete. Deterministic tests and isolated MySQL 8 `America/New_York` winter/summer round-trips pass for Login and production Game repositories, including MAC/HDD reloads; the isolated container was removed. The remaining item is a complete LS→GS hardware-ban synchronization journey across restart.

- [x] Supported JDBC query options are explicitly translated to MySqlConnector; unsupported options fail visibly.
- [x] `players.last_online` has one UTC instant contract in every read/write path.
- [x] MAC/HDD ban timestamps preserve the same epoch before and after DB reload.
- [x] Winter and summer `America/New_York` player/Game DAO timestamp round-trips match Java.
- [ ] A full LS→GS MAC/HDD synchronization and enforcement journey preserves winter/summer epochs across database reload/restart.
- [x] Full solution tests pass (1,002/1,002).

### BA-007 — Nullable event themes

**Status:** Verified

- [x] Missing XML `theme` remains null rather than ordinal-zero `NONE`.
- [x] An always-active theme-less event does not suppress an overlapping themed event.
- [x] Multiple-themed-event selection retains Java's first-themed-event behavior; no beyond-parity precedence rule was introduced.
- [x] Full solution tests pass (1,002/1,002).

## Hardening and setup gate

### BA-008 — JDBC NULL primitive semantics

**Status:** Verified

- [x] Custom-instance collection loads `valid → nullable → valid` with Java zero/false defaults.
- [x] Registered housing items load `valid → nullable → valid` with Java zero defaults.
- [x] A nullable account-time row loads with Java zero defaults.
- [x] Full solution tests pass (1,002/1,002).

### BA-009 — Java-properties split brain

**Status:** Verified

- [x] Bootstrap and runtime use one Java-properties parser/transformer set.
- [x] `true/false/1/0`, invalid booleans, escapes, unicode, separators, and continuation lines match Java.
- [x] Enum properties accept only exact Java names; numeric, comma-combined, and wrong-case values are rejected.
- [x] Actual `Program` option construction is covered for Game, Login, and Chat.
- [x] Targeted parser/transformer suites and the full solution pass (1,002/1,002).

Java's filesystem enumeration order remains unspecified while the C# directory loader intentionally retains ordinal ordering. Java `byte` fields also remain represented by unsigned C# `byte`; all shipped values are in the shared safe range, and the out-of-range schema edge is documented rather than silently claimed as equivalent.

### BA-010 — Java numeric and enum command parsing

**Status:** Verified

- [x] Java-compatible int/long/byte/float/double, radix/decode, Unicode-digit, and Commons `isCreatable` rules replace framework parsing at the command boundary.
- [x] Admin, console, player-command, `ChatUtil`, and `SM_CUSTOM_PACKET` scans contain no raw primitive parser bypass.
- [x] Command enums accept only exact Java names; numeric/comma forms are rejected.
- [x] Domain `ArgumentException` behavior remains intact.
- [x] Java-oracle numeric/enum command tests pass 71/71 without broad exception logs.
- [x] Full solution tests pass (1,002/1,002).

### BA-011 — Java rounding

**Status:** Verified

- [x] A shared Java-compatible round helper replaces every identified incorrect Java `Math.round` port.
- [x] Positive/negative midpoint vectors match `floor(x + 0.5)`; Java NaN and saturation behavior is also pinned.
- [x] Arena reward and gate-repair regression vectors pass.
- [x] Full solution tests pass (1,002/1,002).

### BA-012 — Java zone hash ordering

**Status:** Verified

- [x] `ZoneName.Id()` uses Java `String.hashCode` semantics through the same helper as `SM_PLAYER_REGION`.
- [x] Known ASCII, collision, and UTF-16 surrogate hash vectors pass.
- [x] Equal-type/equal-priority overlap ordering is deterministic and matches Java.
- [x] Full solution tests pass (1,002/1,002).

### BA-013 — Required static-data atomicity

**Status:** Verified

- [x] Required and optional holders are explicitly classified from the active Java import graph.
- [x] Required-holder binding/callback failure aborts boot; optional failures preserve the current holder and required admin reloads replace only after validation.
- [x] Empty required core holders fail integrity/minimum invariants.
- [x] A throwing top-level callback proves boot failure and per-holder reload atomicity.
- [x] Static/bootstrap/JAXB focused tests and the full solution pass (1,002/1,002).

Atomicity is per published holder/admin reload. Boot builds an unpublished `StaticData` candidate, but manually re-running the full leaf-loader on an already published object is not a cross-holder transaction.

### BA-014 — Smaller cross-server fidelity items

**Status:** Verified

- [x] Reconnect validates the exact registered connection, not only account ID.
- [x] Bridge sends are allowed only when authenticated and failures are observed.
- [x] LS-down client auth sends Java's protocol failure before close.
- [x] Chat frame size is capped at Java's 16 KiB boundary.
- [x] Focused bridge tests pass.
- [x] Full solution tests pass (1,002/1,002).

Tagged Chat nickname is tracked with BA-002 because it shares the same Java request packet.

### BA-015 — Nullable enum edge paths

**Status:** Verified

- [x] Relinquish-craft handles an advertised NPC with no profession mapping as Java does.
- [x] A well-formed auction title with an unknown numeric result ID is ignored as Java does.
- [x] Full solution tests pass (1,002/1,002).

### BA-016 — Warning and boundary-test controls

**Status:** Verified

- [x] The warning set is baselined at 4,318 unique sites across 21 codes; new codes or increased code/total counts fail CI.
- [x] `global.json` and CI both pin .NET SDK `10.0.301`, making the compiler-derived inventory reproducible.
- [x] `CS0184`, `CS0472`, and `CS8605` are zero and promoted to build errors.
- [x] Required nested static-data invariants run in automated tests/boot checks.
- [x] Deterministic cross-server lifecycle and DB timezone/NULL tests are part of the normal suite; external MySQL runs remain explicitly gated.
- [x] A conventional clean rebuild records 4,325 warnings, 0 errors — 34 fewer than the audited baseline.
- [x] The normal warning ratchet and full solution pass.

### BA-017 — Fresh Game Server schema import

**Status:** Verified

- [x] The missing comma after `bookmark`'s composite primary key is restored as an infrastructure correction to the upstream SQL error.
- [x] A schema-sanity test pins the primary-key/foreign-key boundary.
- [x] A complete fresh isolated MySQL 8 import of `game-server/sql/aion_gs.sql` succeeds.
- [x] Full solution tests pass (1,002/1,002).

### BA-018 — Exact-name enum boundaries and stock counter skills

**Status:** Verified

- [x] One shared helper implements case-sensitive, declared-name-only Java `Enum.valueOf` semantics.
- [x] Transfer states and runtime signets throw on numeric/comma/wrong-case/unknown names like Java.
- [x] Untyped JAXB proxies for housing, item gender, and counter skills keep invalid values null.
- [x] All 97 shipped counter-skill attributes are pinned: 67 valid names bind and 30 comma values remain Java null (22 `BLOCK,RESIST`, six `RESIST,PARRY`, two `RESIST,DODGE`).
- [x] All 83 textual enum parse sites were triaged; remaining sites are protected by exact-name checks, MySQL `ENUM`, XSD enum/list types, or internally derived names.
- [x] Numeric/enum focused suites pass 77/77; full solution tests pass (1,002/1,002).

## Completion audit checklist

- [ ] Every BA finding is **Verified** or explicitly **Accepted** with a Java/infrastructure rationale.
- [ ] Every explicit acceptance item above has direct test/runtime evidence.
- [x] No active legal GS↔LS opcode is silently dropped in factory/dispatch coverage.
- [x] Required static-data holders and nested indexes pass real-data invariants.
- [ ] Hardware-ban epochs survive the full non-UTC LS→GS synchronization/restart journey.
- [x] Warning-ratchet rebuild succeeds at 0 errors.
- [x] `dotnet test AionServer.slnx` passes 1,002/1,002.
- [x] `python scripts/parity/check_fidelity.py` passes.
- [x] Audit/tracker/backlog local-link scan has 0 broken links.
- [x] `docs/Full-Parity-Backlog.md` and the source audit agree with current implementation status.
- [x] Final worktree/diff review found no whitespace errors and preserved the user's untracked `AGENTS.md`.

The unchecked completion items trace to the five Code-complete findings: BA-001, BA-002, BA-003,
BA-005, and BA-006. They require the two-GS/Login/Chat, in-world siege, and hardware-ban restart
journeys. P10-09 now has explicit failing runtime evidence for shared upstream transfer and Chat-gag
defects; "Code complete" describes the port, not a successful end-to-end journey. See
`docs/e2e-phase10-validation.md` for preserved failures and remaining coverage.

## Progress log

### 2026-07-13 — Remediation started

- Created this evidence-based tracker from the prioritized audit plan.
- Reconfirmed starting baseline: 812 tests pass; fidelity guardrail passes; build has 0 errors and 4,359 warnings.
- Began BA-001, BA-002, and BA-003 in parallel, each constrained to the paired Java implementation.
- BA-001 reached Code complete: opcode `0x0D` restored; three Java frame goldens and LS factory dispatch pass. End-to-end two-GS verification remains coupled to BA-003.
- Transfer review found an upstream Java 4.8 limitation: LS accepts only GS actions `1..4` and emits only responses `20..23`, although the GS parser contains dormant cases `24..28`. The port will preserve that protocol contract; full multi-section transfer is recorded as an upstream limitation rather than inventing a C#-only extension.
- BA-004 design completed: fresh per-session cancellation/state, generation-safe stale-reader/write protection, supervised Java-delay reconnect loops, pending-request cleanup with logged-account preservation, and per-packet business-handler fault boundaries across all four bridge endpoints.
- BA-002 reached Code complete: real `CM_CHAT_AUTH` loopback delivers the exact token, tagged nickname, and gag replay (2/2 new tests; 120/120 related tests; Game Server build 0 errors). Disconnect/duplicate-request lifecycle evidence remains under BA-004.
- BA-003 reached Code complete: the full Java `LsClientPacketFactory` opcode/state surface, post-auth account snapshot, kick/reconnect/control/ban/hardware-ban/transfer handlers, and exact-connection reconnect ownership are implemented. The focused bridge/protocol set passes 16/16; full runtime journeys remain in Release Gate 1.
- BA-007 reached Code complete: missing event themes now remain nullable through an XML string proxy, Java's null-skipping selection is restored, the real always-on theme-less event deserializes as null, and the focused event tests pass 3/3.
- BA-010 reached Code complete: malformed and overflowing numeric command arguments now follow Java's `NumberFormatException` error path while domain `ArgumentException` messages remain intact; focused tests pass 3/3 and the impossible CS0184 branch is gone.
- BA-005 reached Code complete: nested repair/assault callbacks run before location indexing, `static_id` binds publicly, all shipped repair/assault indexes pass, and the related XML/siege set passes 80/80. Live world-object repair/assault remains gate QA.
- BA-011 reached Code complete: a shared Java-round helper now covers the six incorrect calls across gate repair, Dark Poeta, and PvP arena rewards; 12/12 midpoint, edge, gate, and arena vectors pass.
- BA-012 reached Code complete: `ZoneName.Id()` and `SM_PLAYER_REGION` share a stable Java UTF-16 hash helper, with known vectors and equal-priority zone-order regression coverage; the focused/golden set passes 22/22.
- BA-015 reached Code complete: craft relinquish now carries Java's nullable profession result through the consumer, unknown auction result IDs remain nullable and are ignored, and the focused edge-path tests pass 3/3.
- BA-004 reached Code complete: Java-delay supervised retries, fresh per-session state, authenticated-only application sends, account/hardware resynchronization, pending-request cleanup, and per-packet fault isolation pass the 23-test focused bridge/lifecycle set and the 655-test Game Server suite.
- BA-008 implementation started against the three Java JDBC primitive-NULL loaders, with deterministic row-mapping seams planned for nullable collection/account-time rows.
- BA-013 implementation started with Java's all-imports-required boot contract and atomic required-holder reload behavior as the governing policy.
- BA-006 reached Code complete: JDBC options are explicitly translated or rejected, player and hardware-ban persistence share UTC/epoch boundaries, and focused deterministic winter/summer tests pass (Commons 14/14, Game Server 5/5, Login 4/4 deterministic, Chat 1/1). Live MySQL restart vectors remain unchecked.
- BA-008 reached Code complete: the three JDBC primitive-NULL loaders now use Java zero/false defaults without truncating collection rows; focused Game Server 2/2 and Login Server 1/1 tests pass.
- BA-014 reached Code complete: exact reconnect ownership, authenticated send gates, observed background send failures, LS-down protocol failure, and Java's 16 KiB Chat frame limit pass the focused Game Server 11/11 and Chat socket 5/5 sets.
- BA-009 implementation started by tracing all bootstrap option construction through the Java-properties parser and shared transformer behavior.
- BA-009 reached Code complete: every `Program` factory now uses the Java-properties grammar and shared Java transformers; focused parser/factory coverage passes and the provisional full solution passes 902/902.
- BA-013 reached Code complete: the active Java import graph now governs required-vs-optional policy, required callback/invariant failures surface, and admin reload replacement is per-holder atomic; static/bootstrap/JAXB coverage passes 90/90.
- BA-006 returned to In progress after independent review found additional `Timestamp` consumers beyond the original player/MAC/HDD evidence. Account-time, IP-ban, account-creation, and remaining gameplay DAO instant boundaries are being checked against Java before final temporal sign-off.
- BA-016 started: a no-suppression warning ratchet and CI enforcement are being added after the selected impossible-comparison warnings were removed by the semantic fixes.

### 2026-07-13 — Final automated verification

- BA-006's expanded sweep converted every instant-valued Login/Game repository boundary to the shared epoch contract; isolated MySQL 8 winter/summer tests passed for account time, IP/MAC/HDD bans, player and production gameplay DAOs. The fresh Game schema import passed, and the isolated container/listener were removed.
- BA-010 expanded from the original exception fix to every admin/console/player command plus `ChatUtil` and `SM_CUSTOM_PACKET`; exact Java integer, long, byte, float/double, radix/decode, Unicode-digit, Commons-number, and enum-name cases pass 71/71.
- BA-017 fixed the upstream `bookmark` SQL comma as an infrastructure exception, added schema-sanity coverage, and passed a complete fresh MySQL import.
- BA-018 was discovered during independent closeout review: .NET combined 30 shipped counter-skill comma values into `RESIST`; Java JAXB leaves them null. The shared exact-name helper, all 97 stock values, and representative transfer/XML/runtime boundaries pass 6/6 (77/77 with BA-010).
- BA-016 established the 4,318-site/21-code warning ceiling; the normal ratchet passes, high-signal codes are zero-as-errors, and a conventional rebuild is 0 errors / 4,325 warnings (34 fewer than baseline).
- Final adversarial review hardened the MAC/HDD inbound list readers against peer-sized allocation: impossible positive counts are rejected against frame capacity while valid and negative counts retain Java behavior. Huge/truncated production-lenient and negative-count cases pass in the 9/9 protocol suite. The warning toolchain is pinned to .NET SDK 10.0.301 locally and in CI.
- Final gates pass: 1,002/1,002 solution tests, fidelity guardrail, 0 raw command/downstream parser bypasses, sole `GetDateTime` use limited to SQL `DATE`, 0 broken local links, and `git diff --check` exit 0.
- Final state: 13 Verified, five Code complete. Remaining work is limited to the explicitly listed two-GS/Login/Chat, in-world siege, and hardware-ban synchronization/restart journeys.
