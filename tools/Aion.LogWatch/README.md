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
The watcher begins missing-heartbeat checks only after that server emits its first heartbeat, so the check becomes
active when P3-12 supplies the heartbeat producer.

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
required before the recovery deadline. Only the GS heartbeat gap *after the
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
producer processes, not bots or servers. LIVE runner integration and the actual
small-population crash/relogin/duplicate-login journey remain P10-03 work; a
passing controller contract is not LIVE lifecycle evidence.
