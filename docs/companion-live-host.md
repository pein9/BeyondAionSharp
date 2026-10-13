# Companion bots: the live host

Status: planned 2026-10-13, nothing built. It follows [companion-bots.md](companion-bots.md), which is done in SIM.

## The goal

The operator, 2026-10-13: "Let's plan the live host so I can try it in game."

He logs in with his own client, types `.companion cleric`, and a companion comes to him, follows him and plays
its role, as the stand-in's companions did in SIM. NTC is the place to try it.

## How it works

1. **The host is a program on his machine**, beside the stack, like the bot that plays the Priest on his world
   today (`scripts/live/attach-live.ps1`, NI-10). It is the same companion code as in SIM
   (`NaturalIshalgenJourney.Companion.cs`) on the live bot session (`tools/Aion.LiveBots`).
2. **It speaks the client protocol and nothing else.** No admin API, no database access, no Docker lifecycle: the
   rule the Priest's attach runner already keeps.
3. **A seat is an account at the character selection screen.** The host logs in a few accounts of its own and
   leaves them at character selection. The server is told their names (`AION_COMPANION_ACCOUNTS`). When a player
   asks for a companion, the server makes the character on a seat that is waiting; the host sees a character
   appear in that account's list, and enters the world with it. When the companion ends, the host goes back to
   character selection and the seat waits again.
4. **The accounts** come into being the first time the host logs in, as the Priest's account did: the login server
   creates an account on its first login. Five seats, `companion1` to `companion5`.
5. **Leftovers.** A companion is marked as one in its character's note. A seat that logs in with marked
   characters on it has them deleted first (a server that stopped with companions alive). A seat account that
   holds any other character is refused, so a real account named by mistake loses nothing.
6. **The server** needs the new build, `AION_COMPANIONS=true` and the seats' names. The stack's game server image
   carries the code and the profiles, so the stack is rebuilt and restarted once.

## What he needs in game

- An **Asmodian** character of level 25 or higher, which is what he will lead with. The profiles are Asmodian, and
  a group takes one race.
- A map the bots have ground for: Ishalgen, Altgard, **Morheim**, Pandaemonium, Marchutan Priory, **Nochsana
  Training Camp**, Haramel. Anywhere else a companion cannot walk.
- The commands: `.companion cleric`, `.companion list`, `.companion dismiss`. The targeting marks of his client
  are the marks the companions read.

## Rules

- SIM first. The seat protocol is played in SIM before any live run, and the SIM host uses the same protocol as
  the live one, so one thing is tested.
- The live host only reads Docker state to find the ports, as the attach runner does. The stack is rebuilt and
  restarted once, at step L4, by me on his word of 2026-10-13: I tell him before I start, because whoever is
  online then is disconnected. No other step touches his stack.
- Kinah and experience are shared as the game shares them. Nothing of the loot code changes for the trial.
- Everything else as in [companion-bots.md](companion-bots.md): the player leads, the Cleric never attacks, no
  quest, no loot, throwaway characters.

## Steps

- [ ] **L1 The server knows its seats.** A seat is an account named in `AION_COMPANION_ACCOUNTS` that is logged in
  and at character selection. A companion made on it appears in its character list. The note marks a companion;
  marked leftovers on a seat are deleted, a seat with any other character is refused. Tested as server code.
- [ ] **L2 The SIM host uses the seat protocol.** The round's seats log in and wait at character selection instead
  of being told in memory. Shown: the stopping round `c9` again, with the same result.
- [ ] **L3 The live host.** A scenario in `tools/Aion.LiveBots` and `scripts/live/companion-host.ps1 -Target aion`,
  with a stop file and the monitor page, modelled on the attach runner. Its script test runs against a mock Docker,
  as the attach runner's does.
- [ ] **L4 The stack.** `AION_COMPANIONS` and `AION_COMPANION_ACCOUNTS` in `docker/docker-compose.yml`, off by
  default, and the lines for `RUNNING.md`. Then, after telling him, the one rebuild and restart of his stack with
  both set, and the host started on it.
- [ ] **L5 The first try in game.** He asks for a Cleric at Morheim Ice Fortress, walks, marks a monster, goes into
  NTC, sends it away. I read the host's traces and the server's log for what a real client does differently.

## What the try will show first

SIM has never had a real client as the leader. A real client moves differently from a bot: it jumps, strafes,
falls from ledges and sends its movement at its own pace. Following is the part most likely to need work, and
L5 is where that is found.

## Decisions

| | Decision | Source |
|---|---|---|
| LH-Q1 | The host speaks the client protocol only; seats wait at character selection | default |
| LH-Q2 | Five seats, accounts `companion1` to `companion5`, made by their first login | default |
| LH-Q3 | Kinah and experience stay as the game shares them, for the trial | default |
| LH-Q4 | A companion is marked in its character's note; no new table | default |
| LH-Q5 | I rebuild and restart his `aion` stack myself at step L4, once, and tell him before I start | operator, 2026-10-13 |
| LH-Q6 | He leads with an Asmodian of level 25 or higher: Morheim Ice Fortress and into NTC | operator, 2026-10-13 |
