# Aion.LiveBots

The LIVE runner uses the shared `Aion.Bots` protocol library over real TCP. P3-05 provides a deliberately small
`connect` scenario list; L0 adds the first gameplay scenario in P3-09.

```powershell
dotnet run --project tools/Aion.LiveBots -- `
  --run r0917 --output run/r0917 --scenario connect --bots 2 --seed 1
```

Each bot gets `bots/bNN.trace.jsonl`. Failures are appended immediately to `bot.problems.jsonl` with the bot,
account and current step, and make the process exit non-zero. `bots-run.json` records the git SHA, seed, time
zone, profile and scenario list. The runner enforces real-time connection and step deadlines, schedules honest
180-183 second game pings after world entry, and records any automatic reconnect or unexpected quit response
as a problem even if recovery succeeds.
