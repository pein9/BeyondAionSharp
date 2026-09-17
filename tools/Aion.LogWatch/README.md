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
