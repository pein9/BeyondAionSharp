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
   servers keep running. A server's inputs are what its Dockerfile copies, read at the new commit.
   If the update changes `RestartAll.ps1` itself, the new copy does the rest of the run.
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

## What the game server runs on its own

Since the Java boot tail was restored (P10-05, approved as D7 on 2026-10-17), the game server runs Java's sieges
and housing as shipped. Times are in the server's time zone: `gameserver.timezone` is empty, so it is the
container's own zone (`docker-compose.yml` sets no `TZ`).

**Sieges** (`gameserver.siege.enable = true` in `game-server/config/main/siege.properties`). At boot every fortress,
outpost and stand-alone artifact (one that belongs to no fortress) gets its owner's NPCs, and each stand-alone
artifact starts its endless siege.
The calendar is `game-server/config/schedule/siege_schedule.xml`; a siege lasts its location's `siege_duration` in
`game-server/data/static_data/siege/siege_locations.xml`.

| Fortress | Siege starts | Lasts |
|---|---|---|
| Divine Fortress | Fri 21:00 | 50 min |
| Siel's Western, Siel's Eastern, Sulfur, Roah, Asteria | Tue, Thu, Sat 17:00 | 60 min |
| Krotan Refuge, Kysis, Miren | Mon 19:00; Sat 21:00 | 60 min |
| Temple of Scales, Altar of Avarice (Inggison) | Sat 19:00; Mon, Wed 21:00 | 40 min |
| Vorgaltem Citadel, Crimson Temple (Gelkmaros) | Thu, Sat 19:00; Wed 21:00 | 40 min |
| Wealhtheow Keep (Kaldor) | Sun 21:00 | 60 min |
| The agent fight (Levinshor) | Tue 20:50 | 70 min |

- A fortress siege is prepared five minutes before it starts. A fortress the Elyos or the Asmodians have held for its
  `occupy_count` sieges (2 for every fortress above: the capture counts one, each defence adds one) then goes back to
  the Balaur. The agent fight starts with no preparation.
- When a fortress siege starts, every player and kisk not of the owner's race is cleared out: the players go to
  their bind point and the kisks die.
- Balaur assaults (`gameserver.siege.assault.enable = true`) only hit places a player race holds: a chance at each
  fortress siege start, coming 1 to 15 minutes in, and for an artifact 3 to 48 hours after its siege starts.
- Every hour on the hour the server works out each fortress's next state and sends every player online the
  fortress status.

**Housing** (`game-server/config/main/housing.properties`):

- Auto-fill, Monday 00:00 (`gameserver.housing.auction.auto_fill.time = 0 0 0 ? * MON`): for each race the house
  broker is topped up with unowned houses to 20 houses, 10 mansions, 5 estates and 1 palace on auction.
- Auction end, Sunday 12:00 (`gameserver.housing.auction.end_time = 0 0 12 ? * SUN`). A bid in the last five minutes
  keeps that auction open five minutes more, up to 30 minutes past the end. Players may put a house up Monday to
  Friday (`gameserver.housing.auction.register_days = 1, 5`).
- Maintenance, Monday 00:00 (`gameserver.housing.maintain.time = 0 0 0 ? * MON`): rent is weekly and the first week
  is free. An owner who is behind gets a mail; after two weeks unpaid the house is impounded and auctioned, and 90%
  of its price is mailed back.
- A housing task the server was down for runs once at the next boot, as Java's `AbstractCronTask` does.

**PvP map.** At boot the server makes the custom PvP map (an instance of 301220000 with its NPCs). While
`gameserver.pvpmap.enable = false` (`custom.properties`, as shipped), `.pvp join` answers "The PvP-Map is currently
disabled." to everyone but staff, and the map's supply drops and random boss do not come.

**Java's off switches:**

| Setting | Effect |
|---|---|
| `gameserver.siege.enable = false` | No siege locations, siege NPCs or siege calendar |
| `gameserver.siege.assault.enable = false` | No Balaur assaults |
| `gameserver.housing.auction.enable = false` | No bids, house registration or auto-fill; running auctions still end on Sunday |
| `gameserver.housing.pay.enable = false` | No rent: maintenance skips every house |
| `gameserver.housing.auction.auto_fill.time =` (empty) | The auto-fill task is deactivated |
| `gameserver.pvpmap.enable = true` | Opens the PvP map (level 60 and up) |

To change one on the live server, either:

- **Commit it.** Edit the key in `game-server/config/main/` on `main`. The image copies `game-server/config/`, so the
  nightly run rebuilds the game server and restarts it with the new value.
- **Use the overlay.** At every start `docker/gameserver/entrypoint.sh` appends each `*.properties` file in
  `/app/config-overlay` to the `mygs.properties` it generates, after its own values, and `mygs.properties` wins over
  `config/main/`. The live `docker-compose.yml` mounts nothing there yet: add a bind mount to the `gameserver`
  service once (a commit too), for example `- ../run/config-overlay:/app/config-overlay:ro`. From then on a file in
  `run/config-overlay` changes the keys at the next game-server start (`RestartAll.ps1 -RestartOnly`). Git ignores
  `run/`, so the file does not stop the nightly deploy.

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
