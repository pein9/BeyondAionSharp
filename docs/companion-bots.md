# Companion bots

Status: done in SIM, 2026-10-13: C1 to C9 are ticked. The report is at the end. The live host is planned in
[companion-live-host.md](companion-live-host.md). A companion joins its player's group and follows it with nobody's
words. A damage class attacks what the leader marked, or what the leader attacks; the Cleric keeps its support
up and attacks nothing. The group's loot is free for all and a companion opens no corpse. It leaves the game
when it is sent away, and it goes through a portal after its leader. Five companions went with the stand-in
through Nochsana Training Camp to the General, and were gone from the server after it sent them away. The step list at the end is worked one step at a time.

It follows [natural-group-ntc.md](natural-group-ntc.md) and [natural-group-healer.md](natural-group-healer.md):
following F1, the Cleric's healing-only role, the class fights, gear and supplies are there and are reused.

## Skill levels after the Java updates (2026-10-20)

Java `267ce6033` stopped using a skill's grade as its learned level; `e5c133836` extended the migration to
morphing. The generator applies the approved Java `update.sql` rule on export: `skill_id < 30000` or
`skill_id == 40009` has level 1. Every other saved level, including profession ranks, is copied unchanged.
The played source snapshots remain immutable. All 27 profiles were regenerated: 1,694 skill levels corrected,
54 profession entries preserved, with no other profile fields or source hashes changed.

`make-companion-profiles.py --check` enforces the rule even without the source snapshots. The server refuses a
profile with a stale combat or morphing level before creating a companion. Generator regression checks and
`CompanionProfileTests` cover both sides of the skill-ID boundary, morphing, and retained profession ranks up to
499; the companion server test checks the actual learned levels after creation and login.

Validation: all 27 profiles match fresh generation; the three generator regression tests pass; the normal suite
passes 6,663 tests (252 skipped); both isolated companion server tests pass; the host contract passes 32 assertions;
Fast `companion-levels-fast-20261020` passes 115 tests (98 skipped). The warning baseline remains 4,097 sites, and
clock, logger, fidelity and custom-quest draft guards pass. The five-companion marks replay
`run/cp/companion-levels/marks-20261020/w1` has all six clients reaching the end, no world errors, and no companions
or characters left on the seats. It restored `ntc-group-s5` and applied the checked-in Java migration only to its
owned temporary SIM database; the source snapshot and live databases were untouched. This checks marks and
cleanup, not a fresh full NTC run.

## The goal

The operator, 2026-10-12:

> the bots have 2 functions: To playtest the whole game, and to be a companion (dps or heals/buffs) for human
> players

- The player is always the group leader. "What the player is doesn't matter. The bots just need to play their role."
- "We will need to come up with a contract (in game chat?) to input commands to spawn bots with specific attributes.
  A bot will know their role based on class (dps or heals/buffs)." A command such as `/companion {class} {level}`
  spawns a bot "geared like they would be at this point in the game", until the group has six.
- "A bot will just follow the player where they go, even an instance."
- "With a player, the bots do not need to do quests."
- "The bots will just DPS a player marked target, and the cleric just heals as normal."
- "NTC is the first place we can do this type of gameplay."
- "We will have to make some profiles or configurations for these. This is why we have been running simulated runs
  so the bots will know combat skills and rotations and gear selections."

On the plan, the same day: "One thing we really need: We want the bots to read 'marks', and we can set group loot
to 'free for all' if that makes things easier."

His answers the same day: it is built and tried in SIM only for now; a test-only stand-in plays the player's part
there, because no bot leads in the product; a companion is a throwaway character, made when asked and deleted when
dismissed; the first version takes level 26 only. Level 31 came on 2026-10-14 (step M8 of
[natural-morheim-leveling.md](natural-morheim-leveling.md)): the eight original classes have a level-31 profile from the
world of the gear check-in at 31, `morheim-g31-s1-w1`; a companion comes at the highest level its class has that is not
above its player's (the lowest when every one is), and `.companion <class> <level>` asks for one outright.

## How it works

1. **The command.** The player types `.companion <class>` in chat. The emulator's player commands begin with a
   dot (`Handlers/PlayerCommands`); the 4.8 client does not send an unknown slash command. `.companion dismiss`
   sends one or all away, `.companion list` names them.
2. **Who may ask.** A player alone or leading a group, while the group has fewer than six with the companions on
   their way counted. Not in an alliance, and not from inside an instance. Asmodian only at first: the profiles
   are Asmodian.
3. **What is made.** A character of that class at level 26, wearing and carrying what its profile says, standing
   where the player stands and bound where the player is bound. The server makes it, on an account that a host
   offered. A host is the program that plays companions: it offers the accounts it can log in on, takes the
   requests made on them, and plays each character as an ordinary client, which joins the player's group.
4. **The profile** is generated from the group world `ntc-group-s4`, where all eleven classes stand at 26 in the
   gear, shards and supplies the natural runs gave them: one file per class. The fight is the class's own table.
   Since 2026-10-13 it is `ntc-group-s5`, where each class also wears its stigma stone (`docs/natural-muster.md`,
   MR-g); a companion is made with the stigma quest done, so the stone stays in and teaches its skill at login.
5. **Following.** The companion follows F1's observed movement, as in the group stages. When F1's map changes it
   takes the portal NPC that leads there. Flight and teleporters come later.
6. **Fighting.** A damage class attacks the target the leader marked (`SM_SHOW_BRAND`). Of several marked targets
   the lowest mark comes first; with no mark, what the leader is attacking; with neither, a monster that is
   attacking a member of the group. A mark alone is enough: the leader need not strike first. A companion
   finishes the monster it is on before it takes the next one. The Cleric heals, cleanses and blesses, and never
   attacks.
7. **Not its business.** No quests, no quest NPCs, no chests, no loot. The group's loot is set to free for all
   when the first companion joins, so every corpse is the player's at once.
8. **Words.** No command language in chat. A companion says a short plain line when the player needs to know: out
   of mana, dead, cannot reach you.
9. **Death.** It waits a minute for a resurrection, then revives at its bind and comes back through the portal.
10. **The end.** Dismissed, or when the player logs out or leaves the group: the bot logs out and the server
    deletes the character. The account is the host's and is used for the next companion.

## What is there, and what is missing

| Part | Today |
|---|---|
| Follow F1 from group packets; heal, cleanse, bless without attacking; class fights, rests, gear, shards, kit | Built and played (the group stages) |
| Characters of all eleven classes at 26 | In `ntc-group-s4`, as played characters, not as profiles |
| A chat command, a companion made by the server, its deletion | Built (C2): `src/Aion.GameServer/Custom/Companion`, off unless `AION_COMPANIONS=true`, with tests |
| Reading the leader's marks | Built (C5): the bot decodes `SM_SHOW_BRAND` and keeps the marks of its team |
| A bot that waits for nobody's commands | Built (C4): the companion's own loop follows the leader it observes and plays its role |
| Loot left to the player | Built (C6): the server sets the group to free for all with its first companion; a companion opens no corpse |
| Following through a portal without being told | Built (C7): a companion that sees its leader on another map takes the portal NPC that leads there |
| Where bots run | In SIM, inside the round test, which is the host (C3): it offers the accounts of its `companion` seats and plays what the server makes on them. Bot code depends on the game server, so the server cannot host it; a live host process is a later step |

## Decisions

| | Decision | Source |
|---|---|---|
| CB-Q1 | Built and tried in SIM only for now | operator |
| CB-Q2 | A test-only stand-in plays the player in SIM; no bot leads in the product | operator |
| CB-Q3 | A companion is thrown away at its end and keeps nothing | operator |
| CB-Q4 | Level 26 only at first | operator |
| CB-Q10 | The bots read the leader's marks: "One thing we really need" | operator |
| CB-Q11 | Group loot is free for all with companions, rather than a change to the loot turns | operator |
| CB-Q5 | The command is `.companion`; the feature is off unless switched on | default |
| CB-Q6 | Asmodian companions only at first | default |
| CB-Q7 | With no mark, a damage class attacks what the leader attacks | default |
| CB-Q8 | The Chanter fights and keeps its own buffs, as in the group stages; only the Cleric plays support | default |
| CB-Q9 | Experience is shared as the game shares it among group members | default |
| CB-Q12 | Of several marked targets, the lowest mark first | default |
| CB-Q13 | With no mark and nothing the leader attacks, a damage class fights a monster that is attacking a member of the group | default |
| CB-Q14 | A mark alone starts the fight, and a companion finishes the monster it is on before it takes the next mark | default |

## Steps

One step per iteration. A step is ticked when a SIM round from `ntc-group-s4` shows it, with a dated line under it.

- [x] **C1 Decision and profiles.** Log D41. Generate the eleven level-26 profiles from `ntc-group-s4`, with a
  check that every item exists and the class can wear it.
  - 2026-10-12. D41 is in the decision log of [e2e-player-simulation-plan.md](e2e-player-simulation-plan.md).
    `python scripts/sim/make-companion-profiles.py` wrote eleven files to `game-server/data/companions`, named
    `asmodians-<class>-26.json`: the class's appearance, the 15 or 16 things it wears (its coin gear and weapon, both
    power shard stacks), the 9 or 10 stacks in its cube (its kit), and the 72 to 99 skills it has learned. Kinah
    is left out. A file names the snapshot and the dump's hash it came from.
  - `--check` reads the files that are there: every item exists, and its class may wear what it wears by the
    item's restrict row. Where the snapshot is at hand it also compares them with a fresh generation. Both pass.
    A power shard has no restrict row, so its own level (65 for the Ancient one) is no gate, as rule MR-c found.
  - The baseline's hashes are in `run/companion/baseline.json`.
- [x] **C2 The server's part.** `.companion <class>`, `dismiss` and `list`: the limits, the character made from its
  profile beside the player, its end and deletion, and a queue of requests for a host. Tested as server code.
  - 2026-10-12. `CompanionService`, `CompanionProfile` and `CompanionCommand` in
    `src/Aion.GameServer/Custom/Companion`. The feature is off unless the server starts with
    `AION_COMPANIONS=true` or a host switches it on; while it is off `.companion` is no command, and the words go
    out as chat. Two lines of ported code call it: the server's start, and the end of
    `PlayerLeaveWorldService.LeaveWorld`. Deviation 162 in the decision log's register.
  - The character is made the way `CM_CREATE_CHARACTER` makes one (`PlayerService.StoreNewPlayer`), with its
    profile in place of the starter's skills and items. It gets its race's Ascension quest as complete, because
    that is what makes a character a Daeva. Its name is the class and a letter: ClericA, TemplarA.
  - Refused in plain words: a word that is no class, a class with no profile for the player's race, a member who
    does not lead, an alliance, a full group, a player inside an instance, no free seat, a companion that asks.
    `.companion spirit master`, `songweaver`, `gunslinger` and `aethertech` are understood.
  - A companion that leaves the world is deleted with its rows, whatever made it leave. One that nobody came to
    play is deleted at once when it is sent away. A player's logout ends its companions.
  - Run `c2-chain`: SIM test `CompanionCommandMakesAThrowawayCharacterAndDeletesIt` plays all of it with a
    player and a second client that logs the made Cleric in: level 26, a Daeva, its profile's 15 worn things,
    9 carried stacks and 94 skills, the player's place and bind point; then the dismissal and the empty
    tables. Unit tests `CompanionProfileTests` read the eleven profiles. The unit suite (4,645 passed, 16
    skipped), the checks and Fast (`c2-fast`, 11 passed) pass. The unit test that counts the shipped player
    commands now counts what the command loader registers, which is public classes; `.companion` is not one.
    Code commit `cce28721c`.
  - Left for later steps: the companion joins the group when it enters the world (C3); a dismissed companion
    that stays connected is not thrown out by the server yet, and a player who leaves the group does not end
    its companions yet (C9). Left for a live world: a server that stops with companions alive leaves their
    characters behind, and an admin's reload of the commands drops `.companion` until the next start.
- [x] **C3 SIM host and stand-in.** A stand-in types the command; the host logs in what the server made; the
  companion joins the stand-in's group. Shown with one Cleric.
  - 2026-10-12. Rounds `c3-1` and `c3-2` (`run/companion/c3.json`, from `ntc-group-s4`, not captured). The
    stand-in is the group world's Templar, stage `companion-lead`. It types `.companion cleric`; the server
    makes ClericA; the seat on account 102 takes the request and logs the character in; a quarter of a second
    of game time later the stand-in sees a level-26 Cleric in its group on its own map, and it leads.
    `.companion list` names it. After `.companion dismiss` the group is gone and the companion leaves the game.
    No deaths. Both bots reached their end; the world had no problem of its own.
  - The server puts a companion into its player's group as it enters the world, the way an accepted invitation
    does (`PlayerGroupInvite.AcceptRequest`): a third line of ported code, at the end of
    `PlayerEnterWorldService.EnterWorld`. Where the game's own invitation rule refuses (the player is dead, the
    group is full), the companion ends. The server test of C2 now checks the group as well.
  - The host in SIM is the round test. A seat is a bot of the round with `"line": "companion"`,
    `"stage": "companion"` and the account it is played on. The test switches companions on, offers the seats'
    accounts, and each seat sleeps on the turn table until a request is made on its account, plays that
    character, and waits again. It ends when no player of the round is left. A companion's folder is
    `companion-<name>`.
  - The stand-in's round fields are `companions` (the classes it asks for, in order) and `companionUntil` (how
    far it plays: `joined` so far). Its receipt is `companion-completion.json`.
  - The companion's own rule so far: in its player's group it stays; out of it, it logs out with `CM_QUIT`, and
    the server deletes it.
  - Found: as a companion enters the world, the server writes the campaign missions of its level into its
    journal, as it does for any character of that level (2009, 2100, 2900 and 2945 started, 2132 ready to hand
    in). The bot never touches them. C8 compares the journal at the end with the journal at the start.
  - Found: a new group's loot rule is round robin. C6 sets it.
  - Checks: the unit suite (4,645 passed, 16 skipped), the pre-commit checks, Fast (`c3-fast`), and the Group NTC leg
    replayed once on the changed round harness (`n9-c3`). Code commit `4ce3038a1`.
- [x] **C4 Companion mode.** The bot follows F1 and plays its role with no commands and no quests. Shown: the
  stand-in walks the fortress and the Cleric keeps up and keeps its support going.
  - 2026-10-12. Round `c4-4` (`run/companion/c4.json`, `companionUntil: walk`). The stand-in walks 92 m across
    the fortress to Vallack and 90 m back, as a player walks: it waits for nobody. The Cleric is never more
    than 20 m behind and stands beside it 2.0 s and 1.5 s after it stops. It blesses itself and the stand-in at
    the start and casts four heals over time at the stops; the stand-in carries two of the Cleric's effects
    (skills 1684 and 3942) at the end. No attack, no death, no quest touched.
  - The companion's loop is its own (`NaturalIshalgenJourney.Companion.cs`): the follow driver, the Cleric's
    heal driver, the class's fight driver for a death and for its kit. It reads no chat.
  - What three rounds taught (`c4-1` to `c4-3`: up to 39 m behind, 6 s to come up). A companion runs exactly
    as fast as its leader, so ground it loses is not made up until the leader stops. Two rules came of it.
    Behind its leader, the Cleric walks first: a blessing or an idle heal over time waits until it stands within
    6 m again, and only a hurt member (70% HP or less) is healed on the way. Left more than 12 m behind, a
    companion is on a long leg and uses its kit's Running scroll, by the rule every class already has
    (`BuffOurselfAsync`, the travel trigger). The same call keeps its power shards on.
  - Not done here: the minute's wait for a resurrection and the way back through the portal (C7 and C8, where a
    death can happen).
  - Checks: the unit suite (4,645 passed, 16 skipped) and the pre-commit checks. Only the companion stage's own bot code
    changed, so no gate was due. Code commit `ad3845382`.
- [x] **C5 Marks.** The bot reads the leader's marks. Shown: five companions kill what the stand-in marks in the
  field, and what it attacks when it marks nothing.
  - 2026-10-13. Rounds `c5-3` and `c5-4`, with the same figures (`run/companion/c5.json`,
    `companionUntil: marks`): the stand-in with a Cleric, a Gladiator, a Ranger, a Sorcerer and a Chanter, at the monsters of the fortress's coin quest (npc 211298).
    Three tests, 130 s of game time in all, no deaths.
    - A mark alone, which the stand-in does not attack: the monster falls 3.5 s after the mark, struck by all
      four damage classes.
    - Two marks, the higher one set first and on the nearer monster: the monster with the lower mark falls after
      5.75 s, the other after 10.0 s. All four struck both.
    - No mark, and the stand-in attacks a monster: all four struck it, and it fell after 5.4 s.
    - Each damage class fought four times: mark 1 twice, mark 2 once, the leader's target once. The Cleric
      fought nothing: 4 blessings and 21 heals over time.
  - Built: the decoder of `SM_SHOW_BRAND`, checked against Java's golden bytes for the packet
    (`parity-artifacts/golden/packets/SM_SHOW_BRAND.json`; the pinned count of decoders is 128), the marks of the team in the
    bot's world model (`GroupBrands`: taken off by a target of 0, forgotten with the group), and `CM_SHOW_BRAND`
    for the stand-in. Java first: `CM_SHOW_BRAND`, `SM_SHOW_BRAND`, `TemporaryPlayerTeam.updateBrand` and
    `sendBrands`; a member that joins is sent every mark of its team.
  - Two rules the plan did not have are in the decision table as defaults: CB-Q13 (defence) and CB-Q14 (a mark
    alone starts the fight; the monster in hand is finished first).
  - Round `c5-1` did not end, by my mistake. A companion took the monster it had just killed as its target again:
    a fresh kill is no corpse to the client yet, and the mark stays on it. That turn of its loop took no game
    time, so the whole world stood still for 4 h 32 min of real time, until the round test's own limit, and its
    trace grew to 48 GB (deleted). Now a companion remembers every monster it saw fall, every turn of its loop
    takes game time, and a loop that turns 200 times with the clock standing still stops itself. Round `c5-2`
    ended in the stand-in instead: it waited for a corpse, where the health packet already says the monster fell.
  - Not shown here: a mark set while a companion is in another fight (it finishes that one first, by CB-Q14).
  - Checks: the unit suite (4,645 passed, 16 skipped), the pre-commit checks, the packet coverage check, Fast (`c5-fast`),
    the neutral gate on the five starter scopes, identical, and the Group NTC leg replayed once (`n9-c5`).
    Code commit `19d17e9f6`.
- [x] **C6 Loot is the player's.** The group is set to free for all when the first companion joins, and no
  companion opens a corpse. Shown: the stand-in can take every corpse at once.
  - 2026-10-13. Round `c6-2` (`run/companion/c6.json`, `companionUntil: loot`): the marks of C5 again, and after
    each test the stand-in walks to the corpses and opens them. All four corpses were the stand-in's to open at
    once, the one it never struck too; it opened all four and took the seven items in them. The loot rule it
    sees is `[0, 0, 0, 0, 0, 0, 0, 0]`. Each of the five companions left the game with no item it did not come
    with, no kinah and no new journal entry. No deaths.
  - The server sets the rule when the first companion of a group has joined: free for all, and every quality
    picked up as it is, so no roll window opens for a companion (`LootGroupRules(FREEFORALL, 0, ...)` through
    `PlayerGroupService.ChangeGroupRules`, as a leader's own setting goes). It is set once; a player who
    changes it afterwards keeps what he chose. The server's loot code is as it was: Java's
    `DropRegistrationService` gives every member in range the right under this rule. The server test checks it.
  - A companion has no code that opens a corpse. It records what it carries and what its journal holds as it
    joins, and when it leaves the game it stops with an error if it carries an item it did not have.
  - Round `c6-1` stopped in the stand-in: it had the right at once, but stood 30 m from the corpse and the loot
    driver opens what is within 15 m. It walks to the corpse now, as a player does.
  - Java has a third rule, `LEADER`, which gives the right to the leader alone. The operator named free for
    all, so that is what is set.
  - Checks: the unit suite (4,645 passed, 16 skipped), the pre-commit checks, the server test and Fast (`c6-fast`). Code
    commit `dc45a4987`.
- [x] **C7 Into the instance.** Companions follow through Vallack when the stand-in goes in. Shown: six in one
  instance.
  - 2026-10-13. Round `c7-2` (`run/companion/c7.json`, `companionUntil: inside`). After the field tests the
    stand-in walks to Vallack and goes into Nochsana Training Camp. It tells nobody and waits for nobody. Three
    seconds later all five companions stand beside it in instance 2 of map 300030000: each saw its leader on
    another map, found Vallack (800510, dialog 10000) as the NPC of its map that leads there, and went through.
    Sent away inside, they left the game there. No deaths.
  - The lookup is from the shipped data (`portal_template2.xml` for the NPC and its dialog choice,
    `portal_loc.xml` for where it leads), for any map, not for this camp alone. Round `c7-1` found no portal: the
    server's own table (`Portal2Data`) keeps its entries to itself once loaded, as Java's does, so the bot reads
    the file.
  - With no portal NPC that leads to its leader's map, a companion says "I cannot reach you." once in group chat
    and waits. That is where a leader stands who left an instance by another way; nothing plays it yet.
  - Checks: the unit suite (4,645 passed, 16 skipped) and the pre-commit checks; only the companion stage's own
    bot code changed. Code commit `bbadfc66a`.
- [x] **C8 NTC.** The stand-in leads to the General, marking as it goes. Shown: the General dead; no companion took
  a quest or loot. Deaths are outcomes.
  - 2026-10-13. Round `c8-1` (`run/companion/c8.json`, `companionUntil: general`), the first attempt: the
    stand-in and its five companions through the camp to the General, 8 min 33 s inside, 11 min 55 s for the
    whole round. Nobody died.
    - The way is the Group NTC leg's without its quests and chests: the Protector (2:04), the Guard (3:30), the
      Aetheric Field Guard (4:24), the gate (6:09), the General (8:33).
    - The stand-in plays as a leading player plays: it lets its group come up before a pull, puts mark 1 on what
      it pulls, fights it, and opens the corpse. 36 pulls, 30 monsters down by its count.
    - Marks followed. The Gladiator, the Ranger, the Sorcerer and the Chanter fought 34 to 37 times each: 20 to
      25 for a mark, once for what the stand-in attacked with no mark, 11 to 13 in defence of a member. All
      four struck the General.
    - The Cleric fought nothing: 116 heals over time, 21 cleanses, 3 heals, 4 top-ups, 4 blessings, 2 mana
      recoveries, and 3 casts the server refused.
    - Loot: the stand-in opened 14 corpses and took 49 items. No companion carries an item it did not come
      with, and no journal has a new entry.
  - Found: kinah is not an item of the loot rule. `DropService.requestDropItem` shares kinah among the members
    near the looter under every rule ("Kinah is distributed to all group/alliance members nearby"), so each
    companion was given about 1,023 kinah of what the stand-in picked up, and it goes when the companion is
    deleted. With five companions a player keeps a sixth of the kinah. This is the game's own rule, and it is
    the operator's to keep or change.
  - A fallen companion waits a minute for the Cleric's resurrection and takes it, then revives at its bind and
    comes back through the portal (C7's rule). No run has played it: nobody died.
  - Not exercised: a death and a resurrection, the way back in, a boss rule (taunt, the artifact), the
    Teleporter and the chests, which are quest matters.
  - Checks: the unit suite (4,645 passed, 16 skipped) and the pre-commit checks; only the companion stage's own
    bot code changed. Code commit `1defe2616`.
- [x] **C9 The end.** Dismissal, the player's logout and leaving the group each remove the companion and its rows.
  - 2026-10-13. Round `c9-1` (`run/companion/c9.json`): the stopping round. The stand-in brings five
    companions, they follow it into the camp and fight what it marks to the General, and it sends them away
    inside. The server knew no companion of the stand-in 15 s after the dismissal.
    The host found no companion known to the server and no character on any of the five seats' accounts.
    The numbers are in the report below.
  - The other two ways, each with one Cleric: round `c9-leave-1`, where the stand-in leaves its group, and the
    server knows no companion of it 1.25 s later; round `c9-logout-1`, where the stand-in logs out and the host
    finds nothing left. In all three the companion sees that its player no longer leads its group and logs
    out, and the server deletes it as it leaves the world.
  - The server does not depend on that. Every ten seconds it looks at its companions (`CompanionService.Watch`):
    one whose player is gone or no longer leads the group it is in ends; one that is still in the world 30 s
    after it was sent away is disconnected; one that nobody logged in within two minutes is deleted. The
    server test plays a client that logs a companion in and then does nothing: it is ended at the next look,
    disconnected, and its rows are gone.
  - The host checks every round with a seat when it is over, and a round with a companion left fails.
    `round-outcome.json` has `companionSeats`. The stand-in's round field `companionEnd` is `dismiss` (the
    default), `leave` or `logout`.
  - Checks: the unit suite (4,645 passed, 16 skipped), the pre-commit checks, the server test, Fast (`c9-fast`), and the
    Group NTC leg replayed once on the changed round harness (`n9-c9`). The baseline `ntc-group-s4` has the
    hashes it had at the start. Code commit `566ea46be`.

Not in this plan: a live host process and a live world, other levels, Elyos, a support table for the Chanter,
flight and teleporters, companions that are kept.

## The report

2026-10-13, at the stop. Everything here is SIM, seed 1, from `ntc-group-s4`, which is unchanged.

### What a companion does

- `.companion <class>` makes a level-26 Asmodian of any of the eleven classes where the player stands, from the
  class's generated profile: its gear, both power shard stacks, its kit and its skills. `.companion list` and
  `.companion dismiss [name]` name them and send them away. The feature is off unless the server starts with
  `AION_COMPANIONS=true`.
- It is in the player's group as it enters the world, and the player leads. With the first companion the
  group's loot is free for all.
- It follows the leader it sees, with nobody's words, and goes through a portal NPC after it.
- A damage class attacks the lowest of the leader's marks, then what the leader attacks, then a monster that is
  attacking a member. The Cleric heals, cleanses, blesses and resurrects, and attacks nothing.
- It takes no quest and opens no corpse. It keeps its own buffs, scrolls and shards up from its kit.
- When its player sends it away, logs out or leaves the group, it logs out and the server deletes it. The server
  ends one by itself that does not.

### What it does not do yet

- It runs in SIM only. There is no program that plays companions against a live server.
- Level 26 and Asmodians only. No flight, no teleporter, no airship.
- It cannot be asked for from inside an instance. A leader who leaves an instance by a way that is no portal NPC
  is not followed out: the companion says "I cannot reach you." and waits.
- It finishes the monster it is on before it takes the next mark.
- Only the Cleric plays support. The Chanter fights.

### The round to the General

| | |
|---|---|
| Round | `c9-1`: 12:10 of game time, the whole way from the first `.companion` to the last companion gone |
| The group | The stand-in (the group world's Templar) and a Cleric, a Gladiator, a Ranger, a Sorcerer and a Chanter, each made by `.companion` |
| Into the camp | All five beside the stand-in 3 s after it went in, in its instance |
| The way | the Protector 2:04, the Guard 3:30, the Aetheric Field Guard 4:24, the gate 6:09, the General 8:33 (time inside the camp) |
| Deaths | None: not the stand-in, not a companion |
| Marks followed | 36 pulls, each with the stand-in's mark on it. Each damage class fought 34 to 37 times: 20 to 25 for a mark, 1 for the stand-in's unmarked target, 11 to 13 in defence of a member. All four struck the General |
| Healing and dispels | The Cleric: 3 heals, 4 top-ups, 21 cleanses, 116 heals over time, 4 blessings, 2 mana recoveries. 3 casts were refused by the server, all three on itself while the General's effect was on it. No attack. No resurrection was needed |
| Loot left to the stand-in | It opened 14 corpses in the camp and 4 in the field and took 56 items. No companion carries an item it did not come with. The server gave each companion 1,023 to 1,036 kinah as its share of what the stand-in picked up |
| Quests | No companion's journal has an entry it did not have when it joined |
| The end | Sent away inside the camp. The server knew no companion 15 s later, and no character was left on a seat's account |

### Not exercised

- A death: the minute's wait for the Cleric's resurrection, the revive at the bind and the way back through the
  portal are written and no run has played them. Nobody died in any round.
- A boss rule (a taunt, the camp's artifact). The Teleporter and the chests, which are quest matters.
- Six of the eleven classes as companions: the Templar, the Assassin, the Spirit Master, the Gunner, the
  Aethertech and the Songweaver have profiles and fight tables and were not asked for in a round.
- A mark set while a companion is in another fight. A real client as the leader.

### For the operator to decide before a live world

1. **The host.** Bot code cannot run inside the server, so companions need a program of their own that logs in
   as clients. It has to be told of a request (the queue is in the server's memory today), and it needs
   accounts that exist in the login server: how many seats, and whose.
2. **Kinah.** The game shares looted kinah among the members near the looter under every loot rule. With five
   companions the player keeps a sixth, and a companion's share is deleted with it. Keep that, or give the
   player the companions' shares, which changes loot code.
3. **Experience** is shared as the game shares it in a group (CB-Q9). Five companions take their shares.
4. **Who may ask.** Anyone, when the feature is on. A limit for each player, a level of access, Elyos, and other
   levels are open.
5. **Leftovers.** A server that stops with companions alive leaves their characters in the database; nothing
   marks a character as a companion, so nothing can clear them at the next start. An admin's reload of the
   commands drops `.companion` until the next start.
6. **The loot rule.** Free for all, as he said. Java also has `LEADER`, which gives every corpse to the leader
   alone.
7. **My defaults**, which he has not confirmed: CB-Q5 to CB-Q9 and CB-Q12 to CB-Q14 in the decision table.

## Working rules

Those of [natural-group-healer.md](natural-group-healer.md#verification-and-working-rules): SIM only, seed 1, one
world, Java read first, decisions from client observations, no new unit tests for bot work, deaths are outcomes.
The server's part is new server code and is tested like any other. The stand-in is a test tool and never becomes a
way to play.
