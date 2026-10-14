# Natural Morheim leg, to level 31

Status: a plan, 2026-10-13, as the operator shaped it in three messages that day. Step M1, the quest list, is done:
[natural-morheim-quests.md](natural-morheim-quests.md). Nothing is played yet.

It follows [natural-muster.md](natural-muster.md), whose rules MR-a to MR-i are this leg's rules, and starts from the
group world `ntc-group-s5`.

## The goal

The operator, 2026-10-13:

- On what comes next, "yes, write the Morheim plan", with "Only the original 8" and "a standing rule for gear 'check
  ins' at 31, 36, 41, 46, all based on coin gear if any is an upgrade."
- On a first draft that said not every hub had to be played out: "that is not true. Remember: the bots have 2 goals,
  One is to play every quest in the game, to see if anything is broken (and later we will do all other mechanics for
  crafting/gathering), and the other is for our companion bots."
- On a second draft that played Morheim to its last quest at level 39: "we want to only do legs until level 31, then
  make sure we do an equipment check (level 31 best coin gear). Then save a profile for level 31 to spawn a companion
  as (so we can look at Fire Temple). Don't do 'spy' quests. Group quests we might actually need to do in a group.
  Remember to spawn in the same world, 10 minutes apart so we can parallel training, maybe 5 at a time?"

So: **the eight original classes play every Morheim quest that starts below level 31, end on level 31 with the gear
check, and their level-31 profiles become companions.** Morheim's quests from 31 up are the next leg.

## Rules

1. **The standing rules.** One world. The classes are the Gladiator, Templar, Assassin, Ranger, Sorcerer, Spirit
   Master, Cleric and Chanter (MR-h); the other three stay in the world as they are. Bind at the hub being worked,
   see its commander first, take the flight teleporter between hubs, loot every kill, check the inventory after every
   turn-in, clean the bags, keep the kit and the shards. A death is an outcome, not a failure.
2. **Five at a time.** A round is up to five bots in the one world, each playing alone, ten game minutes apart. The
   eight take a hub in two rounds; the second starts from the first one's capture, which carries the others over.
3. **Every quest of the leg, and no level goal.** A hub's quests are taken together and handed in together, as in
   Altgard. A character that reaches 31 before the last hub plays on to the leg's last quest. Nothing is hunted for
   experience; a character that has done every quest and is short of 31 gets there by its hub's repeatable coin
   quest, as the Templar got to 26.
4. **The leg ends on the gear check at 31** (MR-i): Elite Rank 6 for Silver Coins, every piece that beats what is
   worn, sold at Morheim Ice Fortress and at Alsig Village.
5. **Group quests are played in a group** of the eight's characters, with the group stages there are. **Spy quests
   are not played.** A quest that asks for gathering waits for the gathering work. A quest that cannot be taken as
   the server is goes into the report.
6. **A server bug a bot finds** is fixed the usual way: the Java first, retail only under a logged decision. Finding
   them is what the leg is for.
7. **Proof is the run.** No new unit tests for bot code; the repository's checks for the files changed, then the
   round. An uncaptured trial comes before every captured round.

## The ground

From the list, for the eight classes of `ntc-group-s5`, the quests that start below level 31:

| | Quests | Experience |
|---|---:|---:|
| Play as they are (a template handler; the compiled plan is what the bot follows) | 58 | 12.2 million |
| Need their steps written (a hand-written handler, as in the Altgard legs) | 17 | 5.5 million |
| [Group] quests | 11 | 3.0 million |
| [Spy] quests, not played | 10 | |
| Ask for gathering, waiting | 20 | |
| Cannot be taken as the server is | 14 | |
| Repeatable or an event | 34 | |

- **The 75 a character plays alone** are given at Morheim Ice Fortress (13), Kellan's Cabin (27), Desert Garrison
  (18), Slag Bulwark (8) and Alsig Village (5); four have no giver on the map. Every hub has an obelisk and a flight
  teleporter.
- **The 11 group quests:** three at Kellan's Cabin, one at Desert Garrison, seven at Slag Bulwark.
- **Experience.** Level 26 to 31 takes 13.8 million. By the rewards alone, with nothing for a kill, the least
  experienced of the eight is level 31 after 66 of the 75, so every one ends the leg at 31.
- **The campaign** of 4.8 is Q24020 to Q24026. Q24021 to Q24023 fall in this leg and are among the 17 that need
  steps.
- **The 14 that cannot be taken.** Seven are the campaign of before 4.8 (Q2031 to Q2036 and Q2300), which the Q240xx
  quests replaced; neither Java nor this server has a handler for them. Three more have no handler (Q4338, Q24120,
  Q24240): candidates for D32. Two wait on a quest that cannot be had (Q2313, Q2314), for the operator to look at.
  Two are switched off in the quest data.
- **Morheim from 31 up** is 105 quests more, to level 39: Alsig Village, Halabana Outpost, Morheim Observatory, and
  the higher quests of the fortress and Slag Bulwark. They are the next leg, to the check-in at 36.
- **Fire Temple** admits six from level 27. A level-31 companion can go in; a player needs level 27.

## Steps

One step per iteration. A step is ticked when a round shows it, with a dated line under it.

- [x] **M1 The quest list.** Every quest of the zone, by hub: what plays as it is, what needs steps, what is not
  played and why.
  - 2026-10-13: [natural-morheim-quests.md](natural-morheim-quests.md), made by `scripts/sim/zone-quest-list.py`
    from the quest data, the compiled plans and `ntc-group-s5`. The numbers above are its.
- [ ] **M2 Morheim Ice Fortress.** The leg's contract and stage for a Morheim hub, on the fortress's 13 quests. The
  first rounds of five and three from `ntc-group-s5`.
- [ ] **M3 Kellan's Cabin,** 27 quests.
- [ ] **M4 Desert Garrison,** 18 quests.
- [ ] **M5 Slag Bulwark and Alsig Village,** 13 quests, and the four without a giver where they fall.
- [ ] **M6 The group quests,** 11, in groups.
- [ ] **M7 Level 31 and the gear check.** Every quest of the leg done by all eight; level 31; Elite Rank 6 where it
  beats what is worn; the sale of what it replaced, the kit and the cleanup; a relog. The capture is the next group
  world.
- [ ] **M8 Level-31 companions.** The profiles of the eight from that world, beside the level-26 ones. The server
  offers both: a companion comes at the highest level it has a profile for that is not above its player's, and
  `.companion <class> <level>` asks for one outright. A round with a level-31 stand-in and level-31 companions.
- [ ] **M9 The report:** per class its level, quests done, deaths and time; every quest that was not played and why;
  what the bots found wrong with the server.

## Decisions

The operator's, 2026-10-13:

1. Each of the eight plays alone; group quests are played in a group.
2. Spy quests are not played. Gathering quests wait for the gathering work.
3. The quests without a handler are in the list and left for D32, on a later word of his.
4. The leg ends on level 31 with the gear check, and the level-31 profiles become companions.
5. Rounds are up to five bots at a time, in the one world, ten game minutes apart.

Mine, where he gave none; each is his to change:

- How a companion's level is chosen (step M8).
- His own Templar is level 26 and Fire Temple asks for 27. On his word it gets the level-31 Templar's state the way
  it got the level-26 one's.

## Loop prompt

```
Work the M checklist in docs/natural-morheim-leveling.md, one step per iteration.

Repository: C:\Users\ryanf\Documents\GitHub\BeyondAionSharp, existing checkout on main.
Read CLAUDE.md, docs/natural-morheim-leveling.md, docs/natural-morheim-quests.md and docs/natural-muster.md (its
rules MR-a to MR-i). Build on what is there: the Altgard legs and the Abyss-entry leg, the quest plan compiler, the
round runner, the group stages, the companion profiles. Don't over-do anything.

THE GOAL
The eight original classes play every Morheim quest that starts below level 31, alone and hub by hub, then its group
quests in groups. They end on level 31 with the gear check, and their level-31 profiles become companions. Finding
what is broken is the point: a quest that cannot be played is written down with its reason.

EACH ITERATION
1. Orient: git status, the step list, scripts/sim/sim-run-marker.ps1. Never build while a run holds the build
   outputs. Leave files you did not change; commit your own by path.
2. Take the first open step. For a server fault, read the Java first.
3. Small change, build, see that the binary holds the new code, an uncaptured trial round, then the captured one.
   Check a round's trace sizes if it has not ended after ten minutes.
4. A dated progress line under the step, tick it when a round shows it, commit the code and then the document, with
   no attribution line. Never push.

RULES THAT DO NOT BEND
- One world for every character; a round is up to five bots, ten game minutes apart.
- Every quest of the leg, no level goal, nothing hunted for experience. No spy quests. A death is an outcome.
- Only the eight classes. Nothing new for the Rider, the Gunner or the Bard.
- No branch, worktree, subagent or push. No hand-edit of generated data. The warning baseline is not raised.

STOP when M9 is ticked, or when a step has failed three iterations running, or when a decision is needed that the
document does not give. On stopping, send one notification.
```
