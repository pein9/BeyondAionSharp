# Isolated LIVE bot stack

`docker-compose.bots.yml` is deliberately independent of the maintainer's `aion` compose project. Always use a
unique project name and set `AION_E2E_RUN_DIR` to an existing per-run directory containing `logs/ls`, `logs/cs`
and `logs/gs`.

The default `overlay/` profile makes gathering and crafting deterministic by setting both failure chances to
zero. For statistical soaks, set `AION_BOT_OVERLAY_DIR=./bots/overlay-soak`; that profile keeps the same logging
and safety controls but leaves the shipped failure rates unchanged.

The stack publishes login, chat and game on host ports 12106, 11241 and 17777 by default. The admin API is
published only on `127.0.0.1:17780`. Override any port with the corresponding `AION_BOT_*_PORT` variable.

The game container enables `AION_TIMER_CENSUS=1` for capacity diagnostics. Every heartbeat is accompanied
by a separate bounded breakdown of active scheduled callback methods, delays, periods and oldest registration
times. Omitted groups have an explicit active-task count. This does not alter timer execution or acceptance
thresholds; ordinary production configuration defaults to count-only metrics.

After starting a uniquely named compose project, wait for the real stack contract rather than the login/chat
healthcheck pacing timers:

```powershell
pwsh -NoProfile -File scripts/live/wait-ready.ps1 `
  -ProjectName aion-bots-r0917 -RunDirectory run/r0917
```

The gate requires all published ports, the game-server startup line, game server 1's login-server registration,
and the current schema shape. It queries MySQL only inside the compose container.

The bot-only database seed registers game server 1 and creates the director account `director` / `aion-bots`
with access level 9. Subject accounts are auto-created at access level 0. Name them `b{bot:D2}r{MMdd}` (for
example, `b01r0917`) so server log scopes can be joined to a run and bot without extra protocol traffic.

## Cross-server topology prerequisite

`scripts/live/test-cross-server-topology.ps1 -Run <unique-id>` owns a temporary,
zero-bot Docker validation run and removes its stack/databases afterwards. It
checks two Game/Chat pairs sharing Login; Java and C# Chat each admit one GS only.
This proves provisioning and observation, not player transfer.

The optional `cross-server` Compose profile adds `gameserver2` and `chatserver2`.
Set `AION_BOT_SECOND_GS=true` **before fresh MySQL initialization** to create
`aion_gs2`, `aion_cs2` and Login registration 2. Instance overlays are appended
after the shared profile. Default second-instance ports are game 17778, admin
17781 (loopback only), and chat 11242; use `AION_BOT_GAME2_PORT`,
`AION_BOT_ADMIN2_PORT`, and `AION_BOT_CHAT2_PORT` to override them. Both pairs use
the same built images; build the primary services first. Mount separate `logs/gs2`
and `logs/cs2` directories, call `wait-ready.ps1 -SecondGameServer`, and pass
`--second-game-server true` to the watcher. Default single-GS runs are unchanged.

Add `-TransferAttempt` to run a **diagnostic**, not a passing BA-001 scenario:
two ordinary L0 clients create source characters and disconnect, then the runner
inserts one task into Login's existing `player_transfers` operator queue. It waits
for the normal scheduler and records task/account/player/inventory observations.
It never accelerates the scheduler, invents missing bridge messages, unlocks the
account, or repairs transferred data. Incomplete/stalled evidence exits nonzero;
the isolated stack is removed afterwards. Parent metadata is retained in
`topology-run.json`; root bot metadata/traces describe the L0 setup workload.
