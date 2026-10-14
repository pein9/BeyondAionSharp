# Natural Morheim leg

Status: a plan, 2026-10-13, with the operator's answers and his correction of its goal the same day. Step M1, the
quest list, is done and is his to read: [natural-morheim-quests.md](natural-morheim-quests.md). Nothing is played yet.

It follows [natural-muster.md](natural-muster.md), whose rules MR-a to MR-i are this leg's rules, and starts from the
group world `ntc-group-s5`.

## The goal

The operator, 2026-10-13, asked what comes next ("Morheim quests? Reshanta quests? Companion improvements?") and
chose: "yes, write the Morheim plan". The same day he set two rules: "Only the original 8" and "a standing rule for
gear 'check ins' at 31, 36, 41, 46, all based on coin gear if any is an upgrade."

The first draft of this plan ended the leg at level 31 and said "not every hub has to be played out". He corrected
that: "that is not true. Remember: the bots have 2 goals, One is to play every quest in the game, to see if anything
is broken (and later we will do all other mechanics for crafting/gathering), and the other is for our companion
bots."

So: **the eight original classes play every quest of Morheim.** What a character can do alone it does alone, hub by
hub, to the last quest the zone has for it; the group quests are played by groups; and what no bot can play yet is on
the list with its reason. The gear check-ins at 31 and 36 fall inside the leg. The world the eight end in is the next
group world.

## Rules

1. **The standing rules.** One world, eight bots, each playing alone, ten game minutes apart (MR-a). The classes are
   the Gladiator, Templar, Assassin, Ranger, Sorcerer, Spirit Master, Cleric and Chanter (MR-h); the other three
   stay in the world as they are. Bind at the hub being worked, see its commander first, take the flight teleporter
   between hubs, loot every kill, check the inventory after every turn-in, clean the bags, keep the kit and the
   shards. A death is an outcome, not a failure.
2. **Every quest, and no level goal.** A hub's quests are taken together and handed in together, as in Altgard. A hub
   is visited again when the character has the level for more of its quests. Nothing is hunted for experience. A
   character that has done everything it has the level for, while the zone still holds quests above it, plays its
   hub's repeatable coin quest until it has the level, as the Templar did to 26.
3. **The check-ins** (MR-i) are played at the level they belong to, 31 and 36: Elite Rank 6 and Elite Rank 5, both
   for Silver Coins, sold at Morheim Ice Fortress and Alsig Village.
4. **What is not played alone,** each quest named in the list: the [Group] quests are played by groups, after the
   hubs (step M8). The [Spy] quests go into Elyos land through a rift and wait until a bot can. The quests that ask
   for gathering wait for the gathering work. A quest that cannot be taken as the server is goes into the report.
5. **A server bug a bot finds** is fixed the usual way: the Java first, retail only under a logged decision. Finding
   them is what the leg is for.
6. **Proof is the run.** No new unit tests for bot code; the repository's checks for the files changed, then the
   round. An uncaptured trial comes before every captured round.

## The ground

From the list, for the eight classes of `ntc-group-s5`:

| | Quests | Experience |
|---|---:|---:|
| Play as they are (a template handler; the compiled plan is what the bot follows) | 107 | 53.8 million |
| Need their steps written (a hand-written handler, as in the Altgard legs) | 29 | 26.9 million |
| [Group] quests, levels 25 to 39 | 37 | 44.8 million |
| [Spy] quests | 14 | 6.6 million |
| Ask for gathering | 23 | 5.5 million |
| Cannot be taken as the server is | 25 | 29.0 million |
| Repeatable or an event | 34 | |
| Done by all eight (Q24020) | 1 | |

- **The 136 quests a character plays alone** start at these levels: 46 at 26 and under, 29 at 27 to 30, 24 at 31 to
  33, 16 at 34 and 35, 21 at 36 to 39. Morheim's quests run to level 39.
- **Where they are given:** Morheim Ice Fortress 17 (four of them from level 34), Kellan's Cabin 27, Desert
  Garrison 18, Slag Bulwark 9, Alsig Village 36 (five at 30 and under, sixteen at 31 to 33, fifteen from 34),
  Halabana Outpost 10 (from 31), Morheim Observatory 14 (from 35), and five without a giver on the map. Every hub
  has an obelisk and a flight teleporter.
- **Experience.** By the rewards alone, with nothing for a kill, the least experienced of the eight goes from 26 to
  33 on 99 quests. The other 37 start at 34 and above. Kills on the way will close part of that; where they do not,
  rule 2 does, with the coin quests of Halabana Outpost (Q2398 from 33, Q2410 from 37).
- **The campaign** of 4.8 is Q24020 to Q24026; Q24021 to Q24026 are among the 29 that need steps.
- **The 25 that cannot be taken.** Twelve are the campaign of before 4.8 (Q2031 to Q2041 and Q2300), which the
  Q240xx quests replaced; neither Java nor this server has a handler for them. Five more have no handler (Q4338,
  Q24120, Q24122, Q24123, Q24240): candidates for D32. Six wait on a quest that cannot be had (Q2313, Q2314, Q2433,
  Q2486, Q2487, Q2488), most of them on the old campaign: these are for the operator to look at, as the quest data
  may hold a 4.8 condition wrongly. Two are switched off in the quest data.

## Steps

One step per iteration. A step is ticked when a round shows it, with a dated line under it.

- [x] **M1 The quest list.** Every quest of the zone, by hub: what plays as it is, what needs steps, what waits and
  why.
  - 2026-10-13: [natural-morheim-quests.md](natural-morheim-quests.md), made by `scripts/sim/zone-quest-list.py`
    from the quest data, the compiled plans and `ntc-group-s5`. The numbers above are its.
- [ ] **M2 Morheim Ice Fortress.** The leg's contract and stage for a Morheim hub, on the fortress's quests the
  characters have the level for. A round of the eight from `ntc-group-s5`.
- [ ] **M3 Kellan's Cabin.**
- [ ] **M4 Desert Garrison.**
- [ ] **M5 Slag Bulwark, and Alsig Village's quests of 30 and under.**
- [ ] **M6 Level 31:** the first check-in, then Alsig Village's and Halabana Outpost's quests of 31 to 33.
- [ ] **M7 Level 34 and up:** what the fortress, Slag Bulwark, Alsig Village and Halabana Outpost hold from 34, and
  Morheim Observatory; the second check-in at 36.
- [ ] **M8 The group quests,** by groups of the eight.
- [ ] **M9 The end.** Every quest the list marks to play is done by all eight. The sale of what the check-ins
  replaced, the kit and the cleanup, a relog. The capture is the next group world.
- [ ] **M10 The report:** per class its level, quests done, deaths and time; every quest that could not be played
  and why; what the bots found wrong with the server.

## Decisions

Five questions were put to the operator with a default each. His answer, 2026-10-13: "your defaults are fine." His
correction of the goal, above, changes the second.

1. **Alone.** Each of the eight plays alone, as in every leveling leg so far. The group legs stay for instances and
   group quests.
2. **The [Group] quests are played, by groups, in step M8** (the default had left them for a later leg). The [Spy]
   quests and the gathering quests wait, each for the thing it needs.
3. **The quests without a handler** are in the list and left for D32, on a later word of his, as Q4732 was.
4. **The campaign** (Q24021 to Q24026) is played where a character alone can do it.
5. **Companions stay level 26** until he asks for another level.

## Loop prompt

```
Work the M checklist in docs/natural-morheim-leveling.md, one step per iteration.

Repository: C:\Users\ryanf\Documents\GitHub\BeyondAionSharp, existing checkout on main.
Read CLAUDE.md, docs/natural-morheim-leveling.md, docs/natural-morheim-quests.md and docs/natural-muster.md (its
rules MR-a to MR-i). Build on what is there: the Altgard legs and the Abyss-entry leg, the quest plan compiler, the
round runner, the group stages. Don't over-do anything.

THE GOAL
The eight original classes play every quest of Morheim: alone, hub by hub, to the last quest the zone has for them,
with the gear check-ins at 31 and 36 on the way; then the group quests as groups. The world they end in is the next
group world. Finding what is broken is the point: a quest that cannot be played is written down with its reason.

EACH ITERATION
1. Orient: git status, the step list, scripts/sim/sim-run-marker.ps1. Never build while a run holds the build
   outputs. Leave files you did not change; commit your own by path.
2. Take the first open step. For a server fault, read the Java first.
3. Small change, build, see that the binary holds the new code, an uncaptured trial round, then the captured one.
   Check a round's trace sizes if it has not ended after ten minutes.
4. A dated progress line under the step, tick it when a round shows it, commit the code and then the document, with
   no attribution line. Never push.

RULES THAT DO NOT BEND
- One world for every character. Every quest, no level goal, nothing hunted for experience. A death is an outcome.
- Only the eight classes. Nothing new for the Rider, the Gunner or the Bard.
- No branch, worktree, subagent or push. No hand-edit of generated data. The warning baseline is not raised.

STOP when M10 is ticked, or when a step has failed three iterations running, or when a decision is needed that the
document does not give. On stopping, send one notification.
```
