# Natural Morheim leveling leg

Status: a plan, 2026-10-13, with the operator's answers: "your defaults are fine". Nothing is built or played yet;
the first step, M1, ends with a list for him to read.

It follows [natural-muster.md](natural-muster.md), whose rules MR-a to MR-i are this leg's rules, and starts from the
group world `ntc-group-s5`.

## The goal

The operator, 2026-10-13, asked what comes next ("Morheim quests? Reshanta quests? Companion improvements?") and
chose: "yes, write the Morheim plan". The same day he set the two rules the leg ends on: "Only the original 8" and "a
standing rule for gear 'check ins' at 31, 36, 41, 46, all based on coin gear if any is an upgrade."

So: **the eight original classes play Morheim's quests from level 26 to level 31, and end on the first gear
check-in.** The world they end in is the next group world.

## Rules

1. **The standing rules.** One world, eight bots, each playing alone, ten game minutes apart (MR-a). The classes are
   the Gladiator, Templar, Assassin, Ranger, Sorcerer, Spirit Master, Cleric and Chanter (MR-h); the other three
   stay in the world as they are. Levels come from quests. Bind at the hub being worked, see its commander first,
   take the flight teleporter between hubs, loot every kill, check the inventory after every turn-in, clean the
   bags, keep the kit and the shards. A death is an outcome, not a failure.
2. **Hub by hub, in the order of the hubs' levels.** A hub's quests are taken together and handed in together, as in
   Altgard.
3. **The leg ends at level 31.** A character that reaches 31 finishes what it holds and plays the check-in (MR-i):
   Elite Rank 6 for Silver Coins, every piece that beats what it wears. A class that is short of 31 when the quests
   run out gets there by its hub's repeatable coin quest, as the Templar got to 26.
4. **Left out, each named in the list of step M1:** quests marked [Group], [Gather] and [Spy], and quests that have
   no handler. Nothing is hunted for experience.
5. **A server bug a bot finds** is fixed the usual way: the Java first, retail only under a logged decision.
6. **Proof is the run.** No new unit tests for bot code; the repository's checks for the files changed, then the
   round. An uncaptured trial comes before every captured round.

## The ground

What the data holds for an Asmodian, counted against the Cleric of `ntc-group-s5`:

- Level 26 to 31 takes 13.8 million experience. Morheim has 142 one-time quests of level 31 and under still to do,
  worth 38.3 million; 131 of them have a handler, worth 31.6 million. So the level comes from questing with room to
  spare, and not every hub has to be played out.
- Of the 142: 11 are marked [Group], 18 [Gather], 7 [Spy]; 11 have no handler.
- The hubs, each with an obelisk and a flight teleporter:

| Hub | Quests given there | Their levels | Experience | Note |
|---|---:|---|---:|---|
| Morheim Ice Fortress | 12 | 19 to 28 | 1.3 million | where the group stands; sells the Silver Coin gear |
| Kellan's Cabin | 39 | 19 to 26 | 5.8 million | 3 are [Group]; coin quest Q2317 |
| Desert Garrison | 16 | 24 to 28 | 4.0 million | 1 is [Group]; coin quest Q2322 |
| Slag Bulwark | 14 | 27 to 30 | 4.6 million | 7 are [Group] |
| Alsig Village | 14 | 28 to 31 | 4.6 million | sells the Silver Coin gear too |
| Halabana Outpost | 7 | 31 | 3.4 million | past this leg's end |

  The table has the 102 quests whose giver the quest data names and places in Morheim. The other 40 are the
  campaign (Q2031 to Q2037, Q24021 to Q24023) and quests started by an item, a place or a giver elsewhere; step M1
  puts each one with a hub.

## Steps

One step per iteration. A step is ticked when a round shows it, with a dated line under it.

- [ ] **M1 The quest list.** Every quest of the five hubs through the plan compiler: what plays as it is, what
  needs a step the bot does not have yet, and what is left out and why. One table per hub, for the operator to
  read before anything is played.
- [ ] **M2 Morheim Ice Fortress.** The leg's contract and stage for a Morheim hub, on the fortress's quests. A round
  of the eight from `ntc-group-s5`.
- [ ] **M3 Kellan's Cabin.**
- [ ] **M4 Desert Garrison.**
- [ ] **M5 Slag Bulwark,** without its group quests.
- [ ] **M6 Alsig Village,** as far as level 31 asks.
- [ ] **M7 The end.** Level 31 for every class, the check-in, the sale of what it replaced, the kit and the cleanup,
  a relog. The capture is the next group world.
- [ ] **M8 The report:** per class its level, quests done, deaths and time; what was left out; what the bots found
  wrong with the server.

## Decisions

Five questions were put to the operator with a default each. His answer, 2026-10-13: "your defaults are fine." So:

1. **Alone.** Each of the eight plays alone, as in every leveling leg so far. The group legs stay for instances.
2. **The [Group], [Gather] and [Spy] quests** are left out. The group ones could be a group leg later; gathering
   waits for the crafting work; spying needs a rift into Elyos land.
3. **The 11 quests without a handler** are listed in M1 and left for D32, on a later word of his, as Q4732 was.
4. **The campaign** (Q2031 to Q2036, Q24021 to Q24023) is played where a character alone can do it; Q2037 is level
   31 and belongs to the next leg.
5. **Companions stay level 26** until he asks for another level.

## Loop prompt

For when the plan is approved:

```
Work the M checklist in docs/natural-morheim-leveling.md, one step per iteration.

Repository: C:\Users\ryanf\Documents\GitHub\BeyondAionSharp, existing checkout on main.
Read CLAUDE.md, docs/natural-morheim-leveling.md and docs/natural-muster.md (its rules MR-a to MR-i). Build on what
is there: the Altgard legs and the Abyss-entry leg, the quest plan compiler, the round runner. Don't over-do
anything.

THE GOAL
The eight original classes play Morheim's quests from level 26 to level 31, each alone in one world, and end on the
gear check-in at 31. The world they end in is the next group world.

EACH ITERATION
1. Orient: git status, the step list, scripts/sim/sim-run-marker.ps1. Never build while a run holds the build
   outputs. Leave files you did not change; commit your own by path.
2. Take the first open step. For a server fault, read the Java first.
3. Small change, build, see that the binary holds the new code, an uncaptured trial round, then the captured one.
   Check a round's trace sizes if it has not ended after ten minutes.
4. A dated progress line under the step, tick it when a round shows it, commit the code and then the document, with
   no attribution line. Never push.

RULES THAT DO NOT BEND
- One world for every character. Levels come from quests. A death is an outcome.
- Only the eight classes. Nothing new for the Rider, the Gunner or the Bard.
- No branch, worktree, subagent or push. No hand-edit of generated data. The warning baseline is not raised.
- M1 ends with the list for me to read: stop there and wait for my word.

STOP when M8 is ticked, or when a step has failed three iterations running, or when a decision is needed that the
document does not give. On stopping, send one notification.
```
