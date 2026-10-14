# Aion Server — One-Command Docker Deploy

Run the whole Aion server (database + login + game + chat) on any machine with
[Docker](https://www.docker.com/products/docker-desktop/) installed. No coding, no
database setup, no editing config files by hand.

## Quick start

**Windows:**
```powershell
powershell -ExecutionPolicy Bypass -File docker\deploy.ps1
```

**Linux / macOS / WSL:**
```bash
./docker/deploy.sh
```

The first run creates **`docker/.env`** and stops. Open it, set **`SERVER_HOST`** to the
address your players will use, then run the command again:

| You want players to connect from… | Set `SERVER_HOST` to |
|-----------------------------------|----------------------|
| Only this same computer           | `127.0.0.1` (default) |
| Other PCs on your home network    | this PC's LAN IP, e.g. `192.168.1.50` |
| The internet                      | your public IP or domain name |

That's it. The script builds the server images, starts a MySQL database (creating and
seeding all three databases automatically on first run), and launches the three servers.

## Everyday use

```bash
# Status
docker compose -f docker/docker-compose.yml ps
# Live logs
docker compose -f docker/docker-compose.yml logs -f
# Stop (keeps your database)            # Windows: docker\stop.ps1
docker/stop.sh
# Start again (no rebuild)
docker compose -f docker/docker-compose.yml --env-file docker/.env up -d
```

## Nightly restart and deploy (RestartAll)

The live server restarts every night at 3 AM through the Windows scheduled task **Aion RestartAll**,
which runs `docker\RestartAll.ps1`. Each run:

1. Fast-forwards this checkout to `origin/main`, but only when it is on `main` with no modified or
   untracked files. Docker builds from the files on disk, so uncommitted work would otherwise go live.
2. Rebuilds only the server images whose inputs changed since they were last deployed, while the old
   servers keep running.
3. Refuses to deploy new code while the live database schema differs from the repo's
   `*/sql/aion_*.sql`. Apply the migration by hand, then run it again.
4. Backs up the three databases, restarts game, chat and login in order, and health-checks them.
   New images that fail the check are rolled back to the previous ones.
5. Builds and starts the companion host (below). It is no server: a problem with it is a warning in
   the log and changes neither the exit code nor a rollback.

MySQL is never restarted and Docker volumes are never touched.

| Where | What |
|---|---|
| `Documents\AionBackups\db\` | `aion-<date>.sql.gz`, one per run, kept 14 days |
| `Documents\AionBackups\logs\` | one log per run (`-build.log` has the full build output), kept 30 days |
| `Documents\AionBackups\deployed.json` | the commit each server image was last deployed from |

Task Scheduler's "Last Run Result" is the exit code: **0** fine, **1** failed (read the log),
**2** new code not deployed because of schema drift, **3** another run was in progress,
**4** update skipped (not on `main`, uncommitted changes, or diverged from `origin/main`).

```powershell
# Run it now; the window stays open if something went wrong
docker\RestartAll.bat
# What tonight's run would do; changes nothing
powershell -ExecutionPolicy Bypass -File docker\RestartAll.ps1 -PlanOnly
# Back up and restart only, no git or build
powershell -ExecutionPolicy Bypass -File docker\RestartAll.ps1 -RestartOnly
# Rebuild all three images
powershell -ExecutionPolicy Bypass -File docker\RestartAll.ps1 -ForceRebuild
# (Re)create the scheduled task
powershell -ExecutionPolicy Bypass -File docker\Register-RestartAllTask.ps1
# Decision tests, with Docker and git mocked
powershell -ExecutionPolicy Bypass -File docker\test-RestartAll.ps1
```

To keep the server down for maintenance, run `Disable-ScheduledTask 'Aion RestartAll'` first, or the
next run starts it again.

To restore a backup, stop the three servers, then load the dump into MySQL (it replaces all three databases):

```powershell
docker compose -f docker\docker-compose.yml stop gameserver chatserver loginserver
docker cp $env:USERPROFILE\Documents\AionBackups\db\aion-YYYYMMDD-HHMMSS.sql.gz aion-mysql:/tmp/restore.sql.gz
docker exec aion-mysql bash -c 'gunzip -c /tmp/restore.sql.gz | MYSQL_PWD=$MYSQL_ROOT_PASSWORD mysql -uroot && rm /tmp/restore.sql.gz'
powershell -ExecutionPolicy Bypass -File docker\RestartAll.ps1 -RestartOnly
```

## What `.env` controls

| Setting | Meaning |
|---------|---------|
| `SERVER_HOST` | Address players type into their client to reach your server |
| `DB_PASSWORD` | Database password (stays inside Docker) |
| `LOGIN_CLIENT_PORT` / `GAME_CLIENT_PORT` / `CHAT_CLIENT_PORT` | Ports clients connect to (2106 / 7777 / 10241) |
| `RESPAWN_TIME_MULTIPLIER` | Mob respawn speed — `1.0` normal, `0.5` faster, `2.0` slower |
| `AION_COMPANIONS` | Companion bots, `.companion <class>` in game. On by default; `false` turns them off |
| `AION_COMPANION_SPARE_SEATS` / `AION_COMPANION_MAX_SEATS` | Companions kept ready to be asked for (5) and the most at once (50) |

You never need to touch the `.properties` files — each server's container generates its
own config from these values at start-up.

## How it fits together

```
docker/deploy.sh / deploy.ps1
        │  builds images, runs docker compose
        ▼
docker compose (docker/docker-compose.yml)
        ├─ mysql        — auto-creates aion_ls / aion_gs / aion_cs on first boot
        ├─ loginserver  — :2106 clients, :9014 game-server bridge
        ├─ gameserver   — :7777 clients  (carries the game data)
        ├─ chatserver   — :10241 clients, :9021 game-server bridge
        └─ companionhost — no port: logs in as a client and plays the companions players ask for
```

The companion host (`docker/companionhost`, `docs/companion-live-host.md`) keeps a few accounts named
`companion1`, `companion2` and so on waiting at character selection. When a player types `.companion cleric`
the game server makes a character on one of them and the host plays it. It writes to no database. Its log is
`docker logs aion-companionhost`; its runs are under `run/companion-host-stack` in the checkout.

See [`SETUP-GUIDE.md`](SETUP-GUIDE.md) for a step-by-step walkthrough and troubleshooting.

## Local LIVE player simulation

The LIVE harness uses a separate Compose project with its own temporary Docker MySQL, server containers, ports
and logs. It never connects to or installs MySQL on the host. From the repository root, choose a unique lowercase
run id and start L0:

```powershell
pwsh -NoProfile -File scripts/live/run-live.ps1 `
  -Run r0917-l0 `
  -Scenario L0 `
  -WatcherMode record
```

`record` is the baseline mode while the known-problem ledger is being established in P3-14: it records every
fingerprint without turning known boot findings into a runner failure. Use `-WatcherMode enforce` for routine runs
once the ledger is in place; NEW and REGRESSED fingerprints then make the command exit non-zero. The dedicated
watcher probe is `-Scenario canaries`; it deliberately emits one allowlisted problem from each server log path.

The runner builds the local tools and images, waits for all services and schema anchors, runs the bots and watcher,
stops the servers gracefully, collects artifacts, and removes only its exact Compose project and volumes. Add
`-SkipImageBuild` only when the images already contain the code under test. Add `-Keep` to leave that project
running for inspection; remove it afterward with the exact project name printed by the runner.

To inspect NI-02's deterministic Natural Ishalgen decision tree, run a single retained Priest in
the isolated Docker stack while the bot and dashboard run on the host:

```powershell
pwsh -NoProfile -File scripts/live/run-live.ps1 `
  -Run ni02-review -Scenario NI-02 -Bots 1 `
  -DashboardPort 17880 -DecisionViewSeconds 60
```

Open `http://127.0.0.1:17880/` during the 60-second view window. The dashboard
shows the selected action and every quest's rule checks; `run/ni02-review/bots/b01.trace.jsonl`
retains the decisions afterward. NI-02 deliberately stops at `awaiting-capability` when
navigation, combat, gathering, or quest-specific play is needed; it does not yet
complete the area. The dashboard is loopback-only, and this command uses no host MySQL.

NI-03 adds a bounded, collision-checked walk to Q2101's packet-observed starter
without starting the quest. For a short isolated proof, use:

```powershell
pwsh -NoProfile -File scripts/live/run-live.ps1 `
  -Run ni03-review -Scenario NI-03 -Bots 1 `
  -StepTimeoutSeconds 60 -DashboardPort 17880 -DecisionViewSeconds 30
```

The dashboard includes route/replan decisions; `run/ni03-review/bots/b01.trace.jsonl`
retains checked route points and failure context. NI-03 requires an untouched Q2101
starter state and stops at approach, so it is not a quest-completion test.

The Phase 3 Full tier runs L0 and then the watcher canaries in fresh isolated stacks, both in enforce mode, and
keeps their artifacts together under `run/<id>/`:

```powershell
pwsh -NoProfile -File scripts/e2e/run-full.ps1 -Run r0917-full
```

While a run is active, follow its digest from another PowerShell terminal:

```powershell
Get-Content run\r0917-l0\digest.log -Wait |
  Where-Object { $_ -match ' (NEW|REGRESSED) ' }
```

With Claude Code, start `run-live.ps1` in the background and monitor the same `run/<id>/digest.log`, filtered to
lines containing ` NEW ` or ` REGRESSED `. Each new problem then arrives while the bots continue. Do not watch only
the console: `digest.log` joins server findings to bot and step, and coalesces repeat fingerprints.

The retained `run/<id>/` directory contains:

- `digest.log` and `logwatch-summary.json` — watcher classifications and counts.
- `bots-run.json`, `bots/*.trace.jsonl`, and `bot.problems.jsonl` — provenance, ordered bot traffic, and failures.
- `logs/gs`, `logs/ls`, and `logs/cs` — structured server events/problems and ordinary server logs.
- `logs/containers/*.log` and `events.jsonl.gz` — final container logs and Docker lifecycle events.

The script keeps the newest 20 local runs. A failed run still collects these artifacts and tears down its isolated
stack unless `-Keep` was supplied.
