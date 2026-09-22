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

## What `.env` controls

| Setting | Meaning |
|---------|---------|
| `SERVER_HOST` | Address players type into their client to reach your server |
| `DB_PASSWORD` | Database password (stays inside Docker) |
| `LOGIN_CLIENT_PORT` / `GAME_CLIENT_PORT` / `CHAT_CLIENT_PORT` | Ports clients connect to (2106 / 7777 / 10241) |
| `RESPAWN_TIME_MULTIPLIER` | Mob respawn speed — `1.0` normal, `0.5` faster, `2.0` slower |

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
        └─ chatserver   — :10241 clients, :9021 game-server bridge
```

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
