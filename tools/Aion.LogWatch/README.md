# Aion.LogWatch

`Aion.LogWatch` is the LIVE-run problem oracle. It follows the three servers' JSONL event/problem streams,
bot traces and bot failures, Docker Compose logs/events, and the run's MySQL container log. It appends a concise
classification to `digest.log` and writes `logwatch-summary.json` when it stops.

```powershell
dotnet run --project tools/Aion.LogWatch -- `
  --run r0917a `
  --run-dir run/r0917a `
  --project aion-bots-r0917a `
  --mode enforce `
  --stop-file run/r0917a/watcher.stop
```

`enforce` exits nonzero for `NEW` and `REGRESSED` fingerprints. `record` captures the same digest without failing
the run. `KNOWN` comes from a tracked entry in `parity-artifacts/e2e/known-problems.json`; a fixed entry becomes
`REGRESSED`. The ledger is optional until P3-14. Matching LIVE entries in `log-allowlist.json` are suppressed only
up to their `maxCount`.

For a file-only snapshot (useful when inspecting an existing run), pass `--no-docker true --duration-seconds 0`.
Snapshots check only servers whose heartbeat is present. Continuing watches expect all three servers:
`--initial-heartbeat-timeout-seconds` defaults to 30 seconds from watcher startup, and
`--heartbeat-timeout-seconds` defaults to 20 seconds since the last sample (producer cadence is 10 seconds).
The owning LIVE runner starts the watcher after stack readiness. Bounds are 20–600 seconds for the initial
sample and 20–300 for subsequent samples; both thresholds are recorded in the summary. An absent producer
is therefore visible instead of silently disabling liveness checking. Duplicate, older and still-stale samples
do not rearm an active alert; a fresh sample does. Recovery never erases a failure already recorded in this run.
Unallowlisted heartbeat failures fail enforcement even when their fingerprint is already `KNOWN`.
The declared P10-03 restart gap remains restricted to the game server and its original deadline.

These are suspected-hang alerts, not proof of deadlock. Unlike Java's `DeadLockDetector`, a missing heartbeat
cannot establish a cycle of thread/lock ownership. Only the actual `Server heartbeat` / `Server heartbeat:`
template supplies liveness; census/configuration messages from the same category do not.

## Two-game-server runs

Pass `--second-game-server true` for a topology containing Compose services `gameserver2` and `chatserver2`.
Chat admits one GS per process in both Java and C#, so each GS needs its own Chat instance.
The default remains one GS. The second service must mount its logs under `logs/gs2/`;
production filenames and JSON `srv` remain `gs` (`gs.events.jsonl`, `gs.problems.jsonl`).
The watcher attributes them to instance `gs2` using the separate source directory,
and rejects a producer inconsistent with that directory. No server logging change is required.

The second Chat service similarly writes production `cs.*.jsonl` files under `logs/cs2`.
Continuous watching then expects five independent heartbeat producers. One instance cannot
refresh the other; the P10-03 expected restart gap still applies only to `gs`.
Second-server problems, Docker failures, repro context and hang diagnostics retain
`gs2` identity. Its hang collector selects only service `gameserver2` and validates
the same project/network/image/process ownership rules as other collectors.
The summary records the expected server list. Fingerprints remain content-based;
allowlist server scope is not broadened (`gs` does not match `gs2`) and existing
maximum counts are unchanged. This option alone does not provision the second server
or prove a cross-server journey; the owning runner must do both.

## Bounded hang diagnostics

Continuing Docker watches collect once per affected server under `hangs/<gs|gs2|ls|cs|cs2>/`, without blocking
the polling loop. `observation.json` contains the detection time, thresholds and last heartbeat sample
(capped at 16,384 characters). `collection.json` records command results and target identity; failures
instead produce `failure.json`. The watcher summary/digest reports `collected`, `partial`, or `failed`.
A partial collection is not a clean bill of health and never clears the underlying heartbeat failure.
Repeated alerts still reach the digest but do not create repeated dumps. File-only and snapshot watches
never start diagnostic Docker commands.

Target selection requires exactly one full container ID with this run's `aion-bots-<run>` project and
matching service labels, on only its own default network. Inspection deliberately excludes environment
variables/credentials. It collects selected state fields, `docker top`, resource usage, and revalidates
identity/image/process start before and after an in-container managed stack probe. Paused, stopped or restarting
containers skip that probe explicitly. No restart, unpause, server signal, host tuning or SQL is performed.

Each collection has a 20-second budget; individual CLI calls have 3–10-second deadlines. Captured command
output is capped at 128 Ki characters stdout / 16 Ki stderr, with overflow reported as partial evidence.
Only the owned diagnostic child is terminated on timeout/overflow. The managed probe additionally uses
an eight-second in-container `timeout`, so a disconnected Docker CLI cannot leave an unbounded probe.
It targets PID 1 only after checking the expected server assembly. An unresponsive runtime may not supply
managed stacks; failure/timeout evidence and container state are retained instead.

The bot Compose file selects `bot-diagnostics` build targets containing pinned `dotnet-stack 10.0.745401`.
The final/default `runtime` targets are unchanged deployment images without diagnostic tooling. Stack
collection uses [Microsoft's EventPipe-based dotnet-stack tool](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-stack)
and adds sampling overhead; it is not lock-cycle proof. Rebuild bot images after this change. Reusing an
older image with `-SkipImageBuild` will record a missing-tool result (exit 69), never fabricate stacks.

### Local fault-injection validation

```powershell
pwsh -NoProfile -File scripts/live/test-hang-diagnostics.ps1 -Run hang-unique-id
```

This deliberately freezes each server in a new isolated Docker stack, one at a time, with zero player bots.
It uses ports 32106/31241/37777/37780, its own Docker MySQL, healthy managed-stack controls, and guarded
unpause/cleanup. It verifies that the collector identifies the paused target, records partial diagnostics,
and that all servers resume emitting genuine heartbeats without restarting. The watcher **must exit 1**:
these faults must remain failures, not become allowlisted. Only the fault-injection validator exits 0 when
the exact expected evidence and post-cleanup raw logs pass. Its ledger is local to the run, never the shared
known-problem ledger. Artifacts, copied probe source, hashes and failed watcher summary are retained under
`run/p10-04-hangs/<id>`. Use `-SkipImageBuild` only after building current bot diagnostic image targets.
The mock ownership gate `scripts/live/test-hang-probe-contract.ps1` starts no Docker services or bots.

Live trace attribution retains at most 64 records and 128 Ki characters per account. Older or ambiguous
timestamp lookups and reproduction bundles stream the original trace files; do not remove them while a run
is active. `logwatch-summary.json` exposes retained trace-record and character counts. File tails process
finite snapshots in chunks, and Docker followers backpressure at 1,024 queued lines without dropping them.

## Deliberate game-server crash (P10-03 infrastructure)

`--expect-game-server-crash true` opts into one bounded SIGKILL/restart expectation.
It does not inject a fault or run the bot scenario. The owning controller must
verify its isolated Docker container, atomically publish `game-server-crash-plan.json`
in the run directory, then await `game-server-crash-armed.json` and verify its
`run` and `planSha256` against the UTF-8 plan text before killing anything. The
receipt prevents a race between publishing the plan and reading Docker events.
Do not reuse a run directory or overwrite/rearm a plan.

Plan schema (camel case): `schemaVersion: 1`, `run`, `project: "aion-bots-<run>"`,
exact 64-character lowercase `containerId`, and UTC timestamps `armedUtc`,
`killDeadlineUtc`, `recoveryDeadlineUtc`. The plan must be read within five seconds
of arming; kill must occur within thirty seconds and recovery within three minutes
of arming. Use whole-second `armedUtc` if the Docker event source only has
whole-second precision. Missing/invalid/expired plans fail closed.

Only one matching game-server `die` event with exit 137 is expected. Its project
comes from native event labels or the explicitly selected Compose follower
(Compose removes its project/service labels from event attributes). Other container
ids, services, projects, exit codes, OOM/restart events and repeated deaths remain
problems. A `start` for the same container and a fresh GS heartbeat are both
required before the recovery deadline. The heartbeat must be in a later second
than the start event: Docker's whole-second timestamps cannot distinguish an old
process heartbeat from a rapid kill/restart within that same second. Only the GS heartbeat gap *after the
observed death* is expected; LS/CS checks continue and GS checks resume on recovery.
The digest records `EXPECTED_FAULT` observations separately from allowances and
the summary requires `expectedGameServerCrash: true`. Missing observations fail
even on early watcher shutdown. In this opt-in mode unallowlisted `KNOWN` problems
also fail, so an existing tracked process fingerprint cannot hide another death.
No category or fingerprint allowance is added.

`scripts/live/lifecycle-controller.ps1` implements the owning controller. Its
read-only SQL calls run through the isolated Compose MySQL service; it never
starts a host database. It observes the subject's initial position, its delayed
save, then a second movement still absent from storage before arming the kill.
It validates project/service labels, exclusive project network, full container
id and stable image before injection. Restart requires the same container/image,
a later process start, and GS startup plus LS registration logs ingested after
the kill boundary. Old startup messages cannot satisfy this check. The watcher
independently requires its fresh heartbeat and matching Docker events.

The controller retains SQL samples, phase/failure, child stdout/stderr, and
kill/restart receipts. Its contract tests mock Docker and use tiny artifact
producer processes, not bots or servers. `run-live.ps1 -Scenario O1 -Bots 1
-StepTimeoutSeconds 1200` now integrates the controller with the protocol bot.
The actual small-population journey still needs LIVE evidence; passing controller
contracts alone do not complete P10-03.
