# Companion bots: the live host

Status: 2026-10-13: L1 to L4 are done, and he has led five companions through Nochsana Training Camp (L5, still open for what was built after that run). His `aion` stack is up with companions on; the host keeps five seats waiting and logs in more as they are taken (run `ch-l5-7`), and every companion wears its class's stigma stone. It follows [companion-bots.md](companion-bots.md), which is done in SIM.

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

- [x] **L1 The server knows its seats.** A seat is an account named in `AION_COMPANION_ACCOUNTS` that is logged in
  and at character selection. A companion made on it appears in its character list. The note marks a companion;
  marked leftovers on a seat are deleted, a seat with any other character is refused. Tested as server code.
  - 2026-10-13. `CompanionService` finds its seats among the logged-in accounts: at every request, and every ten
    seconds with its watch. An account is a seat while its name is one of `AION_COMPANION_ACCOUNTS`, its client
    is at character selection and its connection is open. The companion is made on the seat's own account
    object, as `CM_CREATE_CHARACTER` puts a new character there, so the list the client asks for next has it
    (Java's `CM_CHARACTER_LIST` sends the account as it is at that moment). A deleted companion is taken out of
    that list again.
  - A companion's character has the note `companion`. A seat that is found with such characters has them
    deleted first. An account named as a seat that holds any other character is not used, with one warning in
    the log, and its character is not touched. A seat whose client is gone ends its companion; one that nobody
    played is deleted at once.
  - One line outside the feature's own folder: `LoginServer.LoggedInAccounts`, a read-only view of the map Java
    keeps to itself. No new call from ported code.
  - SIM test `CompanionSeatIsAnAccountWaitingAtCharacterSelection` plays it with real clients: no seat, a seat
    waiting, the Cleric appearing in its list and entered with, the seat back at character selection with an
    empty list and waiting again, a leftover deleted at the next login, a player's account named by mistake
    left alone, and a seat that goes away. The earlier server test still passes.
  - The round host still takes its requests from memory; L2 moves it to this protocol.
  - The two companion server tests now play in a process of their own: `AION_SIM_COMPANION=1` and a filter on
    `Companion`. The tests of the Fast process share one world, one clock and one seeded random source. With the
    new test among them, run `l1-fast` had two unrelated tests fail on fights that came out differently
    (`AltgardMythicalMonsterBurnsTheIncenseAndInfernusComesAndGoes`,
    `NativeObstacleRecoveryDetoursAroundUnsafeCloseInGround`). Without them the Fast process is what it was
    before the companions, and `l1-fast-2` passes.
  - Checks: the unit suite (4,645 passed, 16 skipped), the pre-commit checks, both server tests, Fast (`l1-fast-2`), and the
    companion stopping round once more (`l1-c9`, the same result). Code commit `59cd345fa`.
- [x] **L2 The SIM host uses the seat protocol.** The round's seats log in and wait at character selection instead
  of being told in memory. Shown: the stopping round `c9` again, with the same result.
  - 2026-10-13. Round `l2-c9-4` (`run/companion/c9.json`, from `ntc-group-s4`): the five seats log in and wait at
    character selection, asking for their character list once a second. Each sees its companion appear, enters
    the world with it on the same connection, and goes back to character selection when its play ends. The
    stand-in and its five companions reach the General 8 min 21 s into the camp after 34 pulls. Nobody died, no
    companion gained an item or a journal entry, and nothing was left on a seat. The server knew no companion
    4 s after the dismissal. The times differ from `c9-1` by seconds, because the seats log in before they play.
  - The round test tells the server the names of its seats' accounts and nothing else. The in-memory seat
    (`OfferSeat`, `TakeRequest`) is internal to the server now, for the server test that makes companions without
    playing them.
  - A companion's play no longer ends with a logout of its own. The host takes it back to character selection
    (`CM_QUIT` that stays connected), where the server deletes it and the seat waits for the next one. Between
    two companions the client's knowledge of the last one is dropped (`BotApi.BeginLoginObservation`).
  - A seat has one folder and one trace for all it plays: `companion-seat-<account>/seat.trace.jsonl`.
  - Found, and it would have cost a live seat: a companion that was resting when it was sent away did not notice
    for over half a minute, and the server's watch disconnected it (`l2-c9-2`). Its play now ends the moment its
    player no longer leads its group: it looks before every packet it is about to send. My first way, a
    cancelled wait, was wrong: a cancelled send leaves the connection's cipher a step ahead of the server's,
    and all five seats were refused at their next packet (`l2-c9-3`).
  - Two changes in the stand-in, the test tool. It asks again when it is told that no companion is free: it and
    the seats log in at the same moment of a round (`l2-c3-1`). And it names a corpse it had the right to and
    could not open, instead of stopping: one corpse of the field tests gave it no item list though it stood
    1.7 m from where its client had it. The right was its own at once, which is what the loot rule gives; the
    walk to a monster that died on the move is the stand-in's weakness, not a companion's.
  - Checks: the unit suite (4,645 passed, 16 skipped), the pre-commit checks, the companion server tests, Fast (`l2-fast`),
    and the Group NTC leg replayed once on the changed round harness (`n9-l2`). Code commit `03e5fcd54`.
- [x] **L3 The live host.** A scenario in `tools/Aion.LiveBots` and `scripts/live/companion-host.ps1 -Target aion`,
  with a stop file and the monitor page, modelled on the attach runner. Its script test runs against a mock Docker,
  as the attach runner's does.
  - 2026-10-13. The host is scenario `CH-01` of the live bot program, attach-only like NI-10
    (`tools/Aion.LiveBots/LiveCompanionHostScenario.cs`). Each seat logs in on its account (`companion1` to
    `companion5`), waits at character selection and asks for its character list every two seconds. A character
    that appears is a companion: the seat enters the world with it, plays it with the code the SIM rounds play,
    and goes back to character selection, where the server deletes it. The loop is the SIM seat's of L2, line
    for line.
  - What a live host needs and a SIM round does not. A seat that loses its connection, or whose play fails,
    writes a problem and logs in again after 15 seconds. A character that is on the account when the seat logs
    in is never played: it is a leftover, and the seat waits until the server has deleted it. All seats play
    on one thread, as the bots of a round do. The ground is the offline geometry of every shipped map
    (`BotNavigationAssets.AnyMapGeometry`); door and event state is not known offline.
  - The script `scripts/live/companion-host.ps1 -Target aion` (`-Seats 1..5`, `-Stop`) reads Docker through the
    attach controller's gate, which admits `ps`, `inspect` and `logs` only. It refuses a world whose game server
    was not started with `AION_COMPANIONS=true` or does not name every seat in `AION_COMPANION_ACCOUNTS`, before
    it makes a run folder. It runs a private copy of the build, shows the monitor page on port 17880, and stops
    on Ctrl+C or the stop file: the companions go back to character selection and the seats log out. Evidence is
    in `run/companion-host/<run>/`.
  - Its contract test `scripts/live/test-companion-host.ps1` (mock Docker, 26 assertions): no direct Docker call
    in the script or its controller, the seat accounts agree with the program, eight kinds of world that make no
    companions are refused, a refused world leaves no run folder, and only reads reach Docker.
  - Found by reading, and it would have cost a live companion: the live session sends its ping from a loop of
    its own, and that send ran the play's look before a send. The companion's look throws once when its player
    no longer leads; thrown in the ping loop it ended the pings and never reached the play. The ping no longer
    runs that look. The natural Priest's attach run (NI-10) had the same fault waiting.
  - Not shown yet: the host program has not spoken to a server. A SIM round has no sockets, and L1 to L3 do not
    touch his stack. Its first login is L4.
  - Checks: the unit suite (4,646 passed, 16 skipped), the pre-commit checks, the manifest's consumers
    (`ScenarioManifestTests`, `test-full-suite.ps1`, `test-full-promotion.py`, `test-code-coverage.py`), both
    attach contract tests, the companion server tests and Fast (`l3-fast`). The first unit run had one failure,
    `AbyssUndeadAiTests.HalfOfTheDeathsLeaveAFear`: twenty unseeded rolls of one in two, which fall outside its
    bounds about once in 2,500 runs; the second run passed. `test-code-coverage.py` failed until the system
    matrix in `e2e-player-simulation-plan.md` named `CH-01`. Code commit `780f75a79`.
- [x] **L4 The stack.** `AION_COMPANIONS` and `AION_COMPANION_ACCOUNTS` in `docker/docker-compose.yml`, off by
  default, and the lines for `RUNNING.md`. Then, after telling him, the one rebuild and restart of his stack with
  both set, and the host started on it.
  - 2026-10-13. The settings: the game server of `docker/docker-compose.yml` takes both from the environment or
    `docker/.env` and starts as before without them; `RUNNING.md` has section 6. Commit `05078ab12`.
  - What I found before the start, and it was not in this plan: the stack was not running. It had no login, game
    or chat server container, and by the dates of the images and containers it had been down since the evening
    of 2026-09-26. The container named `aion-mysql` that was up was the SIM database
    (`scripts/sim/new-sim-db.ps1` makes one under that name when it finds none): 17 `aion_gs_sim_*` schemas and
    nothing of his world. His world's volume `aion_aion-mysql-data` was untouched, and no SQL schema file has
    changed since 2026-09-13. The stack's MySQL needs that name and port 3306, so the SIM container had to give
    them up. I said so in the session before I started; nobody was online to be disconnected.
  - What I did, once. Built the three images from `05078ab12` (`docker compose --env-file .env build`). With no
    SIM run in progress and no connection open on it, stopped the SIM database container and renamed it
    `aion-mysql-sim`; nothing in it was deleted. Started the stack as `docker/deploy.ps1` does
    (`docker compose --env-file .env up -d`), with `AION_COMPANIONS=true` and
    `AION_COMPANION_ACCOUNTS=companion1,companion2,companion3,companion4,companion5` in the shell of that one
    command. MySQL came up healthy on `aion_aion-mysql-data`. No script wrote to a database.
  - The game server (started 14:57:43 UTC, no restart): `Companions are on: 11 profiles of level 26 from
    /app/game-server/data/companions; seat accounts: companion1, companion2, companion3, companion4, companion5`,
    then `Authenticated with login server; 1 game servers registered`. No `fail` or `crit` line.
  - The host: `scripts/live/companion-host.ps1 -Target aion -Run ch-l4-1`. It read the ports and the two settings
    from Docker, loaded the ground, and its five seats logged in through the login server, which made the five
    accounts at this first login. All five are at character selection and ask for their list every two seconds.
    No problem is recorded. This is the first time the host program spoke to a server. The monitor page is at
    http://127.0.0.1:17880/. Evidence: `run/companion-host/ch-l4-1/`.
  - Not shown yet: no companion has been made on the live world. That takes a player's `.companion`, which is L5.
  - To keep in mind while the stack is up:
    - The two settings were given to that one `up`. A later `docker compose up -d` without them starts the game
      server again with companions off. To keep them, the two lines go into `docker/.env`. I did not write
      them there: this session may not read or write that file.
    - The SIM scripts look for a container named `aion-mysql`, so a SIM run now uses the stack's MySQL server,
      in `aion_gs_sim_*` schemas beside his world's. Its root password is the one they use. I start none
      without his word.
    - To put the SIM database back: `docker/stop.ps1`, then `docker rename aion-mysql-sim aion-mysql` and
      `docker start aion-mysql`.
    - A waiting seat writes about 0.6 MB of trace an hour, 3 MB for five. Fine for a trial; a host left up for
      weeks needs a quieter wait.
- [ ] **L5 The first try in game.** He asks for a Cleric at Morheim Ice Fortress, walks, marks a monster, goes into
  NTC, sends it away. I read the host's traces and the server's log for what a real client does differently.
  - 2026-10-13. A character to lead with, on his word: "add a templar character for me, onto the 'rrfarmer'
    account, with identical profile/gear as a bot". His two characters were in Ishalgen. It is `Asimtemplar`, a
    copy of the bots' Templar of the group world `ntc-group-s4`, the character the Templar profile was generated
    from: level 26, by Morheim Ice Fortress and bound there, in its gear with both power shards, its supplies,
    772,938 kinah, and the bot's journal (188 quest rows).
  - How. With nobody online the game server was stopped, `aion_gs` was dumped to
    `run/companion/L5-prep/aion_gs-before-templar.sql.gz`, and the character's rows were copied from a temporary
    schema restored from the snapshot: new rows only, player id 310000 and items 310001 to 310027, above every
    id the world used. The eleven tables have the same columns in both schemas. Its one running effect was left
    out, because its end is a moment of the simulated clock. The temporary schema is dropped. Then the game
    server was started again: its ID factory reserves the ids of the database when it starts, so a character
    put in while it runs would share ids with what it had already spawned. This was a second start of the game
    server, for this request; companions stayed on, since the container keeps its settings.
  - The host's seats lost their connection and came back by themselves: each failed with `Login server did not
    advertise online game server 1` every 15 seconds while the game server was down, then logged in and waited
    again. That is the first time a seat's way back was played on a real server.
  - Not shown: I cannot log in on his account, so the character's first login is his.
  - 2026-10-13, his first try. His words: "I am able to log in, summon the cleric, and it heals/follows great!"
    From the server's log and the seat's trace (`run/companion-host/ch-l4-1/bots/b902.trace.jsonl`):
    `Asimtemplar` logged on at 16:03:35 UTC and asked at 16:04:42. `ClericA` was made on seat `companion2` and was
    in the world 0.2 s later. It played for 3 min 30 s: it followed him through the portal into Nochsana Training
    Camp, cast 30 times (heals, ten cleanses, four blessings), never attacked, did not die, gained no item and
    no journal entry, and got 238 kinah as the game's share of one corpse. No problem was recorded. It ended at
    16:08:12 when his client disconnected inside the camp: the Cleric saw itself become the leader, stopped,
    went back to character selection, and the server deleted it 0.1 s after the disconnect.
  - Still to try after this first try: a fighter and the targeting marks, and `.companion dismiss`.
  - 2026-10-13, his second try (run `ch-l4-1`, 18:44 to 18:46 UTC). He asked for a Gladiator and a Cleric, walked
    the fortress, and flew down to the ice. His words: "both got stuck and did not follow because they never
    started flying, then glitched out and looked like they were running forever and disappeared into the
    mountain"; he dismissed them and "could not re-summon". Three faults, from the traces:
    - **No flight.** A companion only walked. His character took off at (297, 2282, 449) and its samples were in
      the air; the follower took each for ground it could not step onto.
    - **A search that held the host.** With no sample it could walk to, the follower looked for a way round to
      where its leader was, which was in the air: 14.4 s for the Cleric's first search and 7.6 s for the next.
      The host's seats share one thread, so the Gladiator stood in the middle of a 1.2 m step for those 14 s:
      it had sent the start of the step with its velocity and could not send the stop. His client drew it
      running on in a straight line, 120 m and then 60 m. In SIM no game time passes while a bot thinks, so
      no round could show this.
    - **The seat's re-entry.** He asked for a Gladiator 4.7 s after he had sent the first one away. The seat
      entered the world at once and failed in the bot's own timing rule (`Cannot enter world for another
      3866 ms`: the client's ten seconds between two entries of one connection). The seat's connection was
      closed and the server deleted the new companion.
  - What changed, the same day:
    - The seat waits out the re-entry delay before it enters (`WaitForReentryAsync`), in the live host and in
      the round's seat. A companion asked for right after a dismissal can take up to ten seconds to come.
    - **Flight following** (`NaturalIshalgenJourney.Companion.cs`). The follow driver reads the leader's FLY,
      LAND and glide emotions and keeps its air samples apart from the ground trail. Where flight is allowed
      (the map's FLY zones) and its player or a piece of its player's way is in sight, the companion takes
      off, flies straight to its player while it sees it and otherwise the air samples in order, about a second
      of flight a turn, and hovers 4 m off. When its player has landed it flies to the ground beside it and
      lands; out of sight it plans a route that climbs, crosses and comes down. It lands where it is when its
      flight time would not cover the way down plus ten seconds, or after six seconds with no clear way. It
      flies by `CM_MOVE` with a height in its velocity (`BotMover.CreateFlyPlan`), as the 4.8 client does, so
      its player sees it fly; the bots' other flight packet, `CM_MOVE_IN_AIR`, is shown to no other player
      (Java's `CM_MOVE_IN_AIR` only updates the position).
    - To a leader in the air no way on the ground is looked for. Every other long search of the follower runs
      beside the play in the live host (`NaturalJourneyRuntime.SearchElsewhere`), and the host names one that
      took two seconds or more. In SIM it runs where it did.
    - Found by the SIM round, on the walk to the wall: the Cleric circled a stair for a minute, three steps to a
      round, after one sample that lay on the stair's face (`l5-flight-2`). A follower that takes twelve steps
      and comes no nearer to a sample gives it up for the next one.
  - Shown in SIM: round `l5-flight-3` (`run/companion/flight.json`, stand-in checkpoint `flight`). The stand-in
    walks to where he took off, flies 80 m down to where he stood on the ice, and back up. The Gladiator and the
    Cleric take off within 0.3 s of it both times, are never more than 26 m from it, and stand within 3.5 m one
    second after it has landed. Nobody dies. Shown live an hour later (run `ch-l5-3`): his Gladiator took off
    after him at 19:37:08 UTC and landed beside him six seconds later, then killed three monsters he had
    branded, one after the other (the client's default brand is mark 0). His words: "Looks like it is working
    great!"
  - Not covered: a companion neither fights nor heals in the air; a leader that glides is waited for and then
    walked to; a roof it lands on may have no way down on foot.
  - The simulation's database while his stack is up. The SIM scripts took the container named `aion-mysql`,
    which is his world's MySQL now, and its port 3306 does not answer from Windows since the stack came up (a
    WSL relay holds the loopback port; the game is not affected). The simulation has a container of its own
    for this: `AION_SIM_DB_CONTAINER=aion-sim-mysql` and `AION_SIM_DB_PORT=3307` in the shell that starts a
    run (`new-sim-db.ps1`, `sim-snapshot.ps1`, `run-round.ps1` and the test fixture read them; unset, nothing
    changed). No SIM schema is on his world's MySQL.
  - Checks: the unit suite (4,646 passed, 16 skipped), the pre-commit checks, both contract tests, the companion
    server tests, the flight round again (`l5-flight-4`), the stopping round (`l5-c9`), the Group NTC leg
    (`n9-l5`), the neutral gate on the five starter scopes (`l5-gate`), and Fast (`l5-fast-2`). Fast needs
    `AION_BOT_DASHBOARD_PORT` set to another port while the live host holds 17880; its first run failed on
    that port and on nothing else. Code commit `df885f1b2`.
  - 2026-10-13, his camp run (run `ch-l5-3`, 19:37 to 20:02 UTC): "I did a full NTC run". He led a Gladiator, a
    Cleric, a Sorcerer, a Chanter and a Templar through Nochsana Training Camp to the General. Nobody died. The four
    that fight took 30 to 34 targets from his brand, 5 to 7 from what he was attacking and 12 in defence of a
    member; all four fought the three named monsters, the gate and the General. The Cleric cast 258 times and
    never attacked. Each took off two or three times and landed beside him. No item, no journal entry; about
    1,530 kinah each as the game's share. All five left within 0.2 s of his dismissal. No problem on the host, no
    error line on the server. The farthest a companion was behind him: 113 m (the Gladiator, walking his trail
    back after a fight).
  - Two rules of his, the same evening (commit `a009f1ba7`):
    - **Stuck.** "If the bot can't path to the leader and gets stuck for more than 30 seconds, we need to teleport
      them ... the bot will tell us what it's doing in group chat." The check: more than 10 m from its player,
      and half a minute later still within 5 m of where it stood. A fight, a rest, a flight and being beside its
      player each start the half minute again. It says "I am stuck. I am teleporting to you." in group chat and
      asks the server with `.companion come`, a word only a companion may say, for itself, at most once in 20 s.
      The server puts it where its player stands, in its player's instance. A player in the air is waited for. A
      player on a map the companion has no way to is reached the same way after half a minute.
    - **Supplies.** "every 30 minutes, check its consumables supply. This is probably some global that affects all
      bots." The half hour is one shared setting (`NaturalHelpItemPolicy.StockCheckMinutes`). A companion asks the
      server with `.companion restock`: what it has used of the things its profile gave it is filled up to what
      the profile gave, at most once in five minutes; kinah and what it wears are not touched. It says "I have
      filled up my supplies." in group chat when something was filled. The natural journeys do not keep the
      timer yet: it would change their recorded runs, and re-recording the gates is on hold. His decision.
    - Shown in SIM: round `l5-rules-2` (`run/companion/rules.json`, stand-in checkpoint `rules`; its two seats carry
      the test word `no-way`, which makes a companion take no step, and a stock check of two minutes). The
      stand-in walks 60 m off; each companion says once that it is stuck and stands beside it 32 s after it
      left; two minutes in, each has its used scrolls filled up and says so. The first round (`l5-rules-1`)
      found that the companion did not take a teleport within one map in: that is `SM_TELEPORT_LOC` and its own
      `SM_PLAYER_INFO`, with no `SM_PLAYER_SPAWN`, and it asked again every half minute. The server test has the
      two words refused to a player and the supplies filled up once.
  - Three changes to following, on his word after the camp run (commit `cab67008a`):
    - **Skip ahead** ("yes, build the skip-ahead"). A follower walks its leader's trail sample by sample, which he
      saw as "the bot will fight the mob, then retrace the path". A companion now takes a checked way straight to
      where its player stands when that is shorter than what is left of the trail by more than 5 m and no longer
      than one and a half times the straight line. The group legs keep the trail (`SkipAhead` is the
      companion's).
    - **The step aside** ("Our bots come to a stopping point and overlap each other ... move 1m in a random
      direction after hitting their last waypoint"). Once it has come up and its player stands, a companion takes
      one step of a metre in a direction of its own, on checked level ground, and stands until its player is more
      than 6 m off. The same after a landing and after a stuck teleport. Where no such ground is found it stays.
    - The count that gives up a sample (the stair) starts again when a fight has taken the follower away from the
      trail. In his camp run it had also fired on the way back from a fight; all that did was skip a sample.
    - Shown in SIM: the stopping round with all three (`l5-c9-3`): the General falls 7 min 57 s into the camp
      (8 min 21 s before), 38 pulls, nobody dead, 48 skips ahead, 72 steps aside, nobody stuck. The flight round
      (`l5-flight-7`): after each landing the two companions stand 2.0 m apart (0.1 m and 0.002 m before). The
      Group NTC leg is unchanged (`n9-l5-2`).
  - Checks: the unit suite (4,646 passed, 16 skipped), the pre-commit checks, the companion server tests, the
    rounds above, the neutral gate and Fast (`l5-fast-3`) for the two rules; the unit suite and the warning
    baseline again for the following.
  - Live since 20:25 UTC the same day, on his word ("You can continue with your updates now"): the game server's image was built again and its container made anew with nobody online, companions on as before, and the host is run `ch-l5-4` with all of the above. Its five seats wait.
  - Mana, asked by him and not changed: a class with a mana skill of its own casts it at half its mana; a mana
    potion is drunk only when mana is below the cheapest attack (the Chanter also below its heal), the instant
    serums first; the healing Cleric casts its own mana recovery below half and drinks none. A rule that drinks
    the potion that restores over time at a threshold is his to ask for.
  - 2026-10-13, later, on his word before he went away ("run sims needed, rebuild, restart, whatever. Iterate."):
    - **The mana potion at a threshold, for companions only** (commit `0db9b51d3`). At or below half its mana a
      companion drinks a Mana Potion, the one that restores over twenty seconds, when none is at work and its
      health is at 70% or more (a Life Potion shares the delay). In a fight and between fights; the Cleric too.
      It is an opt-in of the shared fight (`ManaPotionAtPercent`) that no natural run sets. The casters' profiles
      carried instant serums only, so the profile generator gives every companion thirty Major Mana Potions
      (`COMPANION_EXTRAS`). Shown in SIM (`l5-c9-mana`): the Sorcerer and the Ranger drank three each at just
      under half their mana, the Cleric one, all at full health; the General fell, nobody died.
    - **Crowd control** (commit `79895bb06`). "Crowd control from Sorcerers or Spirit Masters. The skills 'Tree'
      or 'Fear', as the human leader marks them (there are marks for these in the game, 'Zzz' or something)."
      The client's brand sheet (`Textures/ui/ui.pak`, `v5_sign.dds`) has the Zzz as its fifteenth icon, which is
      brand 14 on the wire if the sheet is in the order of the brands; Java knows sixteen brands, 0 to 15, and
      the sheet's first sixteen end with the Zzz and a no-entry sign. Not yet confirmed from his client. A
      companion that has Curse of Roots (the level-26 Sorcerer: the Tree, 20 s, 30 s between two casts, 25 m)
      casts it on the monster with the Zzz brand, and Root (20 s, 60 s between) while the curse is not ready,
      before any fighting of its own. No companion attacks that monster: not by a mark, not as its player's
      target, not in defence. With the brand off it is a target like any other. The level-26 Spirit Master has
      Root and Body Root and no Fear; it joins when companions come at a level that has it. Shown in SIM
      (`k-control-1`, `run/companion/control.json`, stand-in checkpoint `control`): a Sorcerer and a Gladiator;
      the Zzz on one monster and a number on another. The tree stands 5.8 s after the brand (the Sorcerer walks
      into range first), the numbered one falls, nobody strikes the tree for twelve seconds more, and after the
      release both kill it. Not covered: a fight that outlasts the curse (the Sorcerer casts again only between
      two fights), and an area skill of a companion that wakes the tree.
    - **Seats as many as are asked for** (commit `5db82e9e2`). His question: "Every human player on the server
      would need up to '5 seats'. Are we creating these with static limits and not dynamically?" They were five,
      shared by everyone. Now the server takes seats by a pattern (`AION_COMPANION_ACCOUNTS=companion*`: the name
      and then digits only), and the host keeps five seats waiting (`-Spare`) and logs another in whenever a
      player takes one, up to fifty (`-MaxSeats`; a hundred at the most, the subject numbers it has). A seat that
      has waited beyond the spare ones for five minutes logs out. A seat on its way back after a failure counts
      as one that is coming, so a server that is down does not make the host log in all its seats. The server
      test names its seat by a pattern. Not shown: the pool growing, which takes players asking for more
      companions than are waiting; all seats still play on the host's one thread, and how many companions that
      thread carries at once is not measured beyond the five of his camp run.
    - Checks: the unit suite (4,646 passed, 16 skipped), the pre-commit checks, both contract tests, the
      companion server tests, the rounds named above, the stopping round again (`k-c9-2`), the neutral gate
      (`l5-gate-2`) and the Group NTC leg (`n9-l5-3`) for the fight's opt-in, and Fast (`l5-fast-4`, `k-fast`).
    - Live since 21:21 UTC: the game server's image built again (the new profiles and the pattern), its container
      made anew with nobody online and `AION_COMPANION_ACCOUNTS=companion*`, and the host run `ch-l5-5`: five
      seats waiting.
  - 2026-10-13, later still, while he was away ("Figure out what stigma we should select for each class ... Update
    profiles as needed."):
    - **Every companion wears its class's stigma stone** (commits `e03d71120`, `ffac3e513`; the stones and the
      rule are in `docs/natural-muster.md`, MR-g). The profiles come from `ntc-group-s5`. The damage classes cast
      their stone's skill in the fight they already had; the Cleric casts Saving Grace on a member at half its
      health. Shown in SIM from `ntc-group-s5`: rounds `s5-c9` and `s5-c9b-3` (`run/mr/MR-8`), all eleven classes
      as companions to the General, ten of the eleven skills cast (the Bard was never hurt enough for its heal).
    - **A player out of sight is away** (commit `4c5d9e435`). Round `s5-c9b`, the first with no Cleric among the
      companions, stopped at the camp's door: the stand-in went in, and all five companions stood outside for three
      minutes. The server tells a group where a member is when the member moves or its health changes (Java
      `TeamMoveUpdater`, `TeamStatUpdater`), so a player that goes through a door and stands still at full health
      is not heard of; with a Cleric the Cleric's own casts on its player had always brought the news. Now a
      player that is in the group, not in sight, and by the group's last word within forty metres counts as away:
      after half a minute the companion says it is stuck and is brought, into the instance too. And a player the
      companion sees is where it is seen, whatever map the group's last word names: without that the companions,
      once brought, were brought again every half minute until the player took a step. Shown in `s5-c9b-2`: five
      brought 28 s after the stand-in went in. In `s5-c9b-3` and `s5-c9-3` the news came in time and all went
      through the statue themselves.
    - Live since 22:31 UTC: the image built again, the game server's container made anew with nobody online,
      and the host run `ch-l5-6` on the new bot code: five seats waiting, no problem recorded.
  - 2026-10-13, on his word ("yes, give my Templar the stone"): his `Asimtemplar` has Inquisitor's Blow in its
    stigma slot, the row the bots' Templar has (item id 310028), put in by SQL with the game server stopped, nobody
    online and a dump of `aion_gs` taken first (`run/companion/L5-prep/aion_gs-before-stone.sql.gz`). No Kinah was
    taken. Host run `ch-l5-7` since.
  - **The nightly restart turns companions off** as things stand. `docker/RestartAll.ps1` starts the game server
    from `docker/.env` alone, and without the two settings in that file the stack resolves `AION_COMPANIONS=false`
    (read from `docker compose config`, not from the file). They were given by the shell each time the container was
    made. His to add to `docker/.env`: `AION_COMPANIONS=true` and `AION_COMPANION_ACCOUNTS=companion*`. The host is
    a process of the session that started it and is not started by that script.
  - Still to try before this step is ticked: the stuck rule, the supply check, the mana potion and the Zzz brand
    in game, a re-summon right after a dismissal, a sixth companion for a second player, and a companion's stigma
    skill in a fight (the Templar's Inquisitor's Blow from 15 m and the Sorcerer's Ice Harpoon are the ones to see).
  - Seen in the trace, not a fault yet: a companion in play asks the server's time about nine times a second
    (1,964 `CM_TIME_CHECK` in 210 s), which is how the live session waits for the server. Its trace grows by
    about 30 MB an hour of play, and a waiting seat's by 1.2 MB an hour, twice what I wrote under L4.

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
