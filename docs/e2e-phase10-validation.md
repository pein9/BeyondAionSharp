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

## Scope decisions

- P10-05 and siege/housing-dependent journeys remain deferred under D7.
- P10-06 Java runtime comparisons are explicitly deferred under D15. Java remains
  the source and golden-fixture reference; no Java server is started.
- P10-07 needs real **4.8** client protocol captures, not just extracted geodata.
- A green orchestration contract does not prove a two-hour populated soak, five
  consecutive Full runs, or natural autonomous player progression.
