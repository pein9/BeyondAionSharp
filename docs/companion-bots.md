# Companion bots

Status: planned 2026-10-12; C1 to C4 are done. A companion joins its player's group, follows it with nobody's
words, and the Cleric keeps its support up; it leaves the game when it is sent away. A damage class does not
fight yet. The step list at the end is worked one step at a time.

It follows [natural-group-ntc.md](natural-group-ntc.md) and [natural-group-healer.md](natural-group-healer.md):
following F1, the Cleric's healing-only role, the class fights, gear and supplies are there and are reused.

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
dismissed; the first version takes level 26 only.

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
5. **Following.** The companion follows F1's observed movement, as in the group stages. When F1's map changes it
   takes the portal NPC that leads there. Flight and teleporters come later.
6. **Fighting.** A damage class attacks the target the leader marked (`SM_SHOW_BRAND`, which the bot does not read
   yet). Reading the marks is required. Of several marked targets the lowest mark comes first; with no mark, what
   the leader is attacking. The Cleric heals, cleanses and blesses, and never attacks.
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
| Reading the leader's marks | Missing in the bot; the server sends them |
| A bot that waits for nobody's commands | Built (C4): the companion's own loop follows the leader it observes and plays its role |
| Loot left to the player | The group is set to free for all, so the server's loot code stays as it is; a companion opens no corpse |
| Following through a portal without being told | Half there: a follower enters on the leader's command today |
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
- [ ] **C5 Marks.** The bot reads the leader's marks. Shown: five companions kill what the stand-in marks in the
  field, and what it attacks when it marks nothing.
- [ ] **C6 Loot is the player's.** The group is set to free for all when the first companion joins, and no
  companion opens a corpse. Shown: the stand-in can take every corpse at once.
- [ ] **C7 Into the instance.** Companions follow through Vallack when the stand-in goes in. Shown: six in one
  instance.
- [ ] **C8 NTC.** The stand-in leads to the General, marking as it goes. Shown: the General dead; no companion took
  a quest or loot. Deaths are outcomes.
- [ ] **C9 The end.** Dismissal, the player's logout and leaving the group each remove the companion and its rows.

Not in this plan: a live host process and a live world, other levels, Elyos, a support table for the Chanter,
flight and teleporters, companions that are kept.

## Working rules

Those of [natural-group-healer.md](natural-group-healer.md#verification-and-working-rules): SIM only, seed 1, one
world, Java read first, decisions from client observations, no new unit tests for bot work, deaths are outcomes.
The server's part is new server code and is tested like any other. The stand-in is a test tool and never becomes a
way to play.
