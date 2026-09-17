# Isolated LIVE bot stack

`docker-compose.bots.yml` is deliberately independent of the maintainer's `aion` compose project. Always use a
unique project name and set `AION_E2E_RUN_DIR` to an existing per-run directory containing `logs/ls`, `logs/cs`
and `logs/gs`.

The default `overlay/` profile makes gathering and crafting deterministic by setting both failure chances to
zero. For statistical soaks, set `AION_BOT_OVERLAY_DIR=./bots/overlay-soak`; that profile keeps the same logging
and safety controls but leaves the shipped failure rates unchanged.

The stack publishes login, chat and game on host ports 12106, 11241 and 17777 by default. The admin API is
published only on `127.0.0.1:17780`. Override any port with the corresponding `AION_BOT_*_PORT` variable.
