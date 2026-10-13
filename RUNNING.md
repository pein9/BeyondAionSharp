# Running the C# server stack (local dev)

Three executable projects in `AionServer.slnx`: **Aion.LoginServer**, **Aion.GameServer**, **Aion.ChatServer** (the rest are libraries/tests). Target framework: **net10.0**.

## 1. Prerequisites

- **.NET 10 SDK** (and a Visual Studio that supports it).
- **MySQL/MariaDB** reachable from the host with three databases — `aion_ls`, `aion_gs`, `aion_cs`.
- Free TCP ports: **2106** (LS client), **7777** (GS client), **10241** (CS client), **9014** (LS↔GS bridge), **9021** (CS bridge).

## 2. Database

Base configuration uses **`localhost:3306`**, user **`root`**, and a blank password. For local overrides, create
`login-server/config/myls.properties`, `game-server/config/mygs.properties`, and `chat-server/config/mycs.properties`.
Those files are ignored by Git. Set `database.url`, `database.user`, and `database.password` there.
**Run the servers on the host (via VS), so use `localhost:<published-port>` — not `host.docker.internal`.**

Load schema + the required gameservers seed (example uses the local Docker container `aion-gameserver-integration-mysql` published on 3307):

```bash
# from repo root
docker exec aion-gameserver-integration-mysql mysql -uroot -paion -e \
  "CREATE DATABASE IF NOT EXISTS aion_ls; CREATE DATABASE IF NOT EXISTS aion_gs; CREATE DATABASE IF NOT EXISTS aion_cs;"

docker exec -i aion-gameserver-integration-mysql mysql -uroot -paion aion_ls < login-server/sql/aion_ls.sql
docker exec -i aion-gameserver-integration-mysql mysql -uroot -paion aion_gs < game-server/sql/aion_gs.sql
docker exec -i aion-gameserver-integration-mysql mysql -uroot -paion aion_cs < chat-server/sql/aion_cs.sql

# REQUIRED: authorize the game server to register with the login server
docker exec -i aion-gameserver-integration-mysql mysql -uroot -paion aion_ls < login-server/sql/seed_gameservers.sql
```

The GS registers as **id=1 / password=`1234`** (`gameserver.network.id` / `gameserver.network.login.password`). Without the
`gameservers` row the login server returns `NOT_AUTHED` and the GS never registers — this is the most common first-run failure.

## 3. Config

At startup each server walks **up** from its `bin/` output (`AppContext.BaseDirectory`) via `FindRepoRoot`
until it finds `game-server/config`, then reads the real `*/config` directories + the per-instance `my*.properties` overrides.
Static data (665 XMLs under `game-server/data/static_data`) is loaded the same way — the first GS boot is a little slow while it parses.

## 4. Run in Visual Studio

1. Open `AionServer.slnx`.
2. Right-click the **solution** → **Configure Startup Projects…** → **Multiple startup projects**.
3. Action = **Start** for `Aion.LoginServer`, `Aion.ChatServer`, `Aion.GameServer`; **None** for everything else.
4. Order: **LoginServer → ChatServer → GameServer** (drag up). Order is forgiving — GS/CS retry the bridge every ~2s — but LS-first keeps logs clean.
5. **F5** (debug) or **Ctrl+F5** (run).

Or from the CLI (one terminal each, in order):

```bash
dotnet run --project src/Aion.LoginServer
dotnet run --project src/Aion.ChatServer
dotnet run --project src/Aion.GameServer
```

## 5. Healthy startup looks like

- **LoginServer:** `Loaded 1 registered game servers`, then listening on 2106 (client) + 9014 (GS bridge).
- **ChatServer:** connects to LS, listening on 10241.
- **GameServer:** static data loads, then `Authenticated with login server; 1 game servers registered`, then steady ping/pong keep-alive.

If the GameServer logs `NOT_AUTHED` or keeps retrying: missing `gameservers` seed row, or a DB port/credential mismatch.

## 6. Companion bots (optional)

`.companion <class>` brings a player a companion that follows and plays its role (D41, `docs/companion-bots.md`).
It needs a game server started with companions on, and the host program beside it (`docs/companion-live-host.md`).

- **Game server:** set `AION_COMPANIONS=true` and `AION_COMPANION_ACCOUNTS=companion*` in its environment. The
  pattern is every account named `companion` and then digits; the names can also be listed outright. The Docker
  stack passes both through (`docker/docker-compose.yml`, off by default): put the two lines in `docker/.env`, or
  set them in the shell that runs `docker compose up -d`. A seat account holds no character of its own; the login
  server makes the account at its first login.
- **Host:** `pwsh -NoProfile -File scripts/live/companion-host.ps1 -Target aion`, and the same with `-Stop` from
  another terminal. It only reads Docker state and speaks the client protocol. It refuses a world that was started
  without the two settings. It keeps five seats waiting (`-Spare`) and logs in another whenever a player takes
  one, up to fifty (`-MaxSeats`, a hundred at the most).
- **In game** (an Asmodian, on a map the bots have ground for): `.companion cleric`, `.companion list`,
  `.companion dismiss`.
