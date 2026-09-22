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

## Live bot monitor

Add `-DashboardPort 17880` to a host-executed `run-live.ps1` command, then open
<http://127.0.0.1:17880/> while the bot is running. The loopback-only, read-only
page refreshes once per second and shows each bot's current scenario action,
connection, identity, map/channel/position, HP/MP/XP, level, kinah, nearby-object
counts, skills/cooldowns, active/completed quests, inventory and last observed
packet/system message.

The dashboard is opt-in (`0`/disabled by default), has no control endpoints, and
uses only the bot's normal packet-derived world model. It neither reads admin/DB
state nor supplies gameplay decisions. It exists only while the bot process runs;
the normal traces, receipts and reports remain the durable evidence.

```powershell
pwsh -NoProfile -File scripts/live/run-live.ps1 -Run ni-monitor `
  -Scenario NI-01 -Bots 1 -StepTimeoutSeconds 60 -DashboardPort 17880
```

## NI-01: retained Natural Ishalgen identity

```powershell
pwsh -NoProfile -File scripts/live/run-live.ps1 -Run ni01-check `
  -Scenario NI-01 -Bots 1 -StepTimeoutSeconds 60
```

NI-01 uses the stable ordinary account `niishalgen` and character `Ishalgenbot`.
It creates an Asmodian Priest through normal login/game packets only when the
account is empty; otherwise it requires that account's sole character to be the
same non-deleted pre-Ascension Priest. It enters Ishalgen, verifies access level 0
through the read-only admin oracle, quits without deleting, then logs in again and
must select the same character id. The output includes
`natural-ishalgen-identity.json`. A conflicting account, class, race, name,
post-boundary level or pending deletion is a failure and is never repaired with GM
or database mutation.

## O1: server crash and duplicate login

Run through the owning Docker controller, not the standalone bot command:

```powershell
pwsh -NoProfile -File scripts/live/run-live.ps1 -Run lifecycle-check `
  -Scenario O1 -Bots 1 -StepTimeoutSeconds 1200
```

O1 runs alone with one ordinary Asmodian subject and no GM director. A temporary
second login connection tests the same account; peak connection population is two.
The maintainer's current ten-bot total cap still applies across all concurrent runs.
`-Keep` and record-only watching are rejected. `-BotExecution Docker` is supported;
the database and server stack always run in their isolated Docker project.

The bot walks checked starter-zone routes, waits for the normal 900-second player
save, then moves again. Read-only Docker SQL proves the saved checkpoint differs
from the initial position and that the second movement is not saved before the
declared SIGKILL. The watcher must arm the exact container's one-use fault plan.
After restart, character selection, world entry and the independent player-state
oracle must restore the saved position; level, kinah and inventory must survive.

The duplicate attempt must receive ALREADY_LOGIN (7), while the original game
connection receives STR_KICK_ANOTHER_USER_TRY_LOGIN and closes. The bot honors the
ordinary delayed logout and reentry window before proving fresh login succeeds.
No Java runtime, SQL mutation, quest reset, shortened save interval or GM setup
is used. This takes roughly sixteen minutes even when healthy.

The owning runner copies the existing fingerprint allowlist only for O1 and gives
the exact known QuestSpawnAnalyzer boot report (`231c488f`) two occurrences for
the two boots, preserving its owner and expiry. Global allowances are unchanged;
unrelated problems, extra deaths and missing recovery still fail. Evidence includes
the bot trace, SQL samples in `lifecycle-controller.json`, checkpoint/fault receipts,
child stdout/stderr and the watcher digest/summary. Implementation and contract
tests alone do not prove the LIVE journey; see `docs/e2e-phase10-validation.md`.
