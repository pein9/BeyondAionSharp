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
Quest/gather/craft/vendor/duel/PvP runtime, shared-resource coordination, statistical
checks, dispatcher/memory/timer telemetry and every two-hour acceptance run remain
outstanding. System-message history is also still unbounded; the diagnostic
packet-history cap alone is not a claim of flat bot-process memory.

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

## Scope decisions

- P10-05 and siege/housing-dependent journeys remain deferred under D7.
- P10-06 Java runtime comparisons are explicitly deferred under D15. Java remains
  the source and golden-fixture reference; no Java server is started.
- P10-07 needs real **4.8** client protocol captures, not just extracted geodata.
- A green orchestration contract does not prove a two-hour populated soak, five
  consecutive Full runs, or natural autonomous player progression.
