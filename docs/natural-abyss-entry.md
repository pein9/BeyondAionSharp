# Natural Morheim arrival and Abyss-entry leg

Status (2026-10-06): **approved. AX-00 (this plan) is done. The operator answered
AX-Q1..AX-Q6 and then revised the order the same day; see
[Operator decisions](#operator-decisions).** AX-01 is done: the server side is compared and
measured. AX-02 is done: Morheim and the arena have checked-in navigation data. AX-03 is
done: the leg has a frozen contract, and a restored run proves its start. AX-04 is next.
No step of the leg has been played yet.

This leg takes the level-25 Cleric from the preserved Altgard endpoint to Morheim Ice
Fortress, and through the four Asmodian Abyss-entry missions. In order:

1. Go to Morheim, bind, and talk to the commander (Q24020 "Aegir's Orders").
2. Buy the level-21 coin armor that beats what is worn.
3. Do Q2945 → Q2946 → Q2947 → Q2042.
4. If the Cleric is still below level 26, do a few fortress quests.
5. Buy the level-26 coin armor that beats what is worn.

It is worked one item at a time with the
[loop protocol](natural-ascension-altgard.md#how-to-work-this-list-loop-protocol), using
**AX** in place of **NA**.

**Why this leg.** It serves the first two
[long-term goals](../CLAUDE.md#where-this-leads-the-long-term-goals):
- It play-tests server code no bot has touched: flying rings, a timed solo instance, and the
  D33 timer correction on its own quest.
- Every later character needs it. The planned Templar and Sorcerer must each complete the same
  chain, so the route and the two new skills are written once.
- The commander's talk opens the fortress's quests, the next questing ground.

**The inventory rule (the operator, 2026-10-06).** After every quest turn-in the bot runs
its inventory management check: wear better gear (the staff rule included), open reward
containers, discard what is to be discarded, and check cube space. Selling waits for the
next vendor visit.

## Start and finish

**Start:** `sim-snapshot.ps1 -Action Restore -Name altgard-rc-complete-s1`.
- Character 133276, Asimnjour, Cleric 25, alive, bound at Altgard Fortress.
- 176 completed quests. The only active quest is Q2945 at START/0.
- 748,485 Kinah; 7 Bronze Coins; 880,687 XP to level 26.

**Finish (the endpoint contract):**
- Q24020, Q2945, Q2946, Q2947 and Q2042 are complete in the client-observed journal, each
  once.
- Q2947 was completed through the arena branch, with reward group 1.
- Every arena attempt and every ring-course attempt is in the outcome ledger, failures
  included. A death, a lost timer or a missed ring is an outcome, not a test failure.
- The Cleric is level 26 or above, reached by questing. No hunting was done for it.
- The Cleric took Altruist's Staff from Q2947 and wears the staff it owns with the most
  magic boost (AX-Q1).
- The flight-time manastone was discarded. The two Strange Green Sacks and the two Bronze
  Coin Chests were opened, and what they gave is recorded (AX-Q4, AX-Q5).
- The level-21 and the level-26 coin armor were each bought only for the slots where the
  coin piece beats what was worn, and are worn. Every supplied coin is in the run's profile.
- The inventory check ran after every quest turn-in.
- The stigma bundle 188053787 is still sealed. No skill book, stigma or weapon was bought.
- GM input touched the character only for the approved help: the OD-13 consumables, one
  flight-speed scroll (AX-Q3) and the Bronze Coins for the coin armor.
- The Cleric is bound and parked at Morheim Ice Fortress (AX-Q2).
- The endpoint is relog-verified and saved from committed code under a new snapshot name.
  `altgard-rc-complete-s1` and every older snapshot stay unchanged.

## Route (Java `4.8` at `ce54b7931`)

This plan rests on five Java handlers, read in full (Q24020, Q2945, Q2946, Q2947, Q2042),
and on the shipped quest, spawn, portal, teleporter, goods-list and fly-ring data. The C#
handlers are ports. So far only Q2947's branch, timer and teleport lines and Q2042's D33
guard were checked in C#; AX-01 compares all five line by line.

### Morheim first

| # | Step | Where | Action |
|---|---|---|---|
| M1 | Travel | Ukin 203581, Altgard Fortress | teleport to Morheim (location 10) for 2,401 Kinah; lands at (309.53, 2271.51, 449.41) |
| M2 | Q24020 starts | on entering Morheim | the handler starts it by itself for a character of level 21 or above |
| M3 | Bind | obelisk 700231 (268.11, 2338.76, 444.12) | 2,690 Kinah |
| M4 | Q24020 | Aegir 204301 (225.23, 2415.47, 454.11), about 167 m from the landing | QUEST_SELECT → REWARD and page 1011; SELECT_QUEST_REWARD → page 5; SELECTED_QUEST_REWARD4, the hauberk |
| M5 | Coin armor | Vebna 204425 (220.57, 2333.51, 446.32), about 82 m from Aegir | the level-21 pieces that beat what is worn; see [Coin armor](#coin-armor) |

**The commander's quest.** The operator remembered a quest picked up before Morheim. That
is Q2300 "Morheim Commander's Call", which has no handler in Java or C#. Its 4.8
replacement is Q24020 "Aegir's Orders". It needs no pickup: entering Morheim starts it, as
long as Q2300 was never taken. Its one step is the talk with Aegir.

### Abyss entry

| # | Quest and step | Where | Action |
|---|---|---|---|
| 1 | Q2945, var 0 | Balder 204075, Pandaemonium (1469.10, 1466.08, 177.82) | QUEST_SELECT → 1011; SETPRO1 → var 1 |
| 2 | Q2945, var 1 | Therf 204088 (933.43, 1601.21, 223.77) | QUEST_SELECT → 1352; SET_SUCCEED → REWARD |
| 3 | Q2945 reward | Balder | USE_OBJECT → 10002; reward. Q2946 starts on completion |
| 4 | Q2946, var 0 | Balder | QUEST_SELECT → 1011; SETPRO1 → var 1 |
| 5 | Q2946, var 1 | 204210 (1368.89, 1030.47, 206.56) | QUEST_SELECT → 1352; SETPRO2 → var 2 |
| 6 | Q2946, var 2 | 204211 (1330.38, 1059.80, 206.02) | QUEST_SELECT → 1693; SETPRO3 → var 3 |
| 7 | Q2946, var 3 | 204208 (1392.07, 1048.95, 206.68) | QUEST_SELECT → 2034; SET_SUCCEED → REWARD |
| 8 | Q2946 reward | Kvasir 204053 (1281.15, 1176.92, 215.09) | USE_OBJECT → 10002; reward. Q2947 starts on completion |
| 9 | Q2947, var 0 | Kvasir | QUEST_SELECT → 1011; **SETPRO12 → var 4** (the arena branch) |
| 10 | Q2947, var 4 | Garm 204089 (1001.38, 1528.27, 222.19) | QUEST_SELECT → 1693; SETPRO3 → var 5 |
| 11 | Q2947, var 5 | Arena entrance 700368 (978.18, 1556.51, 210.55) | portal to Triniel Underground Arena, map 320090000 |
| 12 | Q2947, arena | inside, entry at (276, 293, 163) | movie 167; kill ten spirits in 240 s |
| 13 | Q2947, var 5 with ten kills | Garm | QUEST_SELECT → 2034; SETPRO4 → var 7, REWARD |
| 14 | Q2947 reward | Aegir 204301, Morheim Ice Fortress (225.23, 2415.47, 454.11) | USE_OBJECT → 3739; SELECT_QUEST_REWARD → page 6; SELECTED_QUEST_REWARD2, the staff. Q2042 starts on completion |
| 15 | Q2042, var 0 | Aegir | QUEST_SELECT → 1011; SETPRO1 → var 1 |
| 16 | Q2042, var 1 | Yornduf 204319 (311.83, 2271.25, 449.41) | QUEST_SELECT → 1352; SELECT2_1_1 → movie 89, page 1354; SETPRO2 → var 2 and a 70 s timer |
| 17 | Q2042, var 2–7 | six flying rings | each ring in order adds one; the sixth gives var 8 and ends the timer |
| 18 | Q2042, var 8 | Yornduf | QUEST_SELECT → 1693; SET_SUCCEED → REWARD |
| 19 | Q2042 reward | Aegir | USE_OBJECT → 10002; reward |

**Q2947 has one working branch.** Kvasir's only progressing action is SETPRO12, which leads to
Garm's arena. The handler still holds kill code for the Morheim monsters and loot code for
the Statue of Urgasch, but no dialog reaches var 2 or var 9, so those two old choices cannot
be started. The reward is forced to group 1.

**Travel.** Three paid teleports, 6,637 Kinah in all (AX-01, as charged):
- Altgard to Morheim: Ukin 203581, 2,401 Kinah (row M1).
- Morheim to Pandaemonium, before row 1: Orhe 204399, 2,118 Kinah.
- Pandaemonium to Morheim, before row 14: Doman 204191, 2,118 Kinah.

The shipped base prices are 1,700 and 1,500; the server's price modifier makes the fares
above, and SM_PRICES shows them to the client. The Morheim teleport lands two metres from
Yornduf. Pandaemonium has an obelisk too, 700068 at (1457.01, 1413.84, 177.51), for 1,582
Kinah; this leg does not bind there.

**The arena.** Eleven level-25 spirits stand in three groups; ten must die.

| Group | Spirits | Around | From the entry |
|---|---|---|---|
| West | four Mage Spirits 213584 | (235, 239) | 68 m |
| South | two Warrior Spirits 213583 and two Mage Spirits | (276, 168) | 125 m |
| East | three Warrior Spirits | (316, 239) | 67 m |

- West to south is about 82 m, and south to east about 82 m. The whole round is about 230 m of running.
- The spirits' tribe is AGGRESSIVESUPPORTMONSTER, so a group is likely to fight together.
- AX-01: a Mage Spirit has 205 HP, an 8 m aggro range and a 16 m attack range. A Warrior
  Spirit has 265 HP, a 6 m aggro range and a 2 m attack range. A kill pays 738 XP.
- Their spawn data gives no respawn time. Only one of the eleven may be left alive.
- The instance data allows one player and sets no entry cooldown.
- **Every attempt starts in a new instance (D34).** When an attempt fails, the server
  destroys its instance as soon as the player is out of it. Garm's SETPRO3 also destroys any
  instance still registered to the player. So the entrance opens a new one with all eleven
  spirits, and no wait is needed. Before D34 the entrance returned to the failed instance for
  600 s, with the dead spirits still dead.
- A cleared instance is not reset. Like any solo instance it is destroyed 600 s after the
  player leaves it, checked once a minute.
- The tenth kill ends the timer and plays movie 168. When the client reports that movie's
  end, the server teleports the player to (1006.1, 1526, 222.2) beside Garm. The exit
  730067 at (275.90, 295.69, 163.53) leads to Pandaemonium too.
- **Failure:** when the 240 s run out, the quest goes to var 6 and the player is teleported to
  Garm. Garm then answers USE_OBJECT with page 1779, and SETPRO3 gives var 5 again. D34: only
  the player's own attempt fails this way (var 5, fewer than ten kills, inside the arena).
  Another quest's timer no longer touches Q2947.
- **Leaving early also fails it.** The enter-world hook sets var 6 when the player is at
  var 5 anywhere but the arena. AB-Q6 found that hook runs on a relog, a revive and a
  teleport that respawns the player. D34 resets the arena here too.
- **A death alone does not fail it.** This is Java's rule, and D34 keeps it. The timer runs
  on over the corpse. A revive in place goes on with the attempt, and kills still count. The
  bind revive leaves the arena, which fails it. A player still dead when the timer ends is
  revived by the teleport to Garm, with a fifth of their HP and MP and Soul Sickness.

**The ring course.** The rings have a 6 m radius and must be passed in order.

| Ring | Centre | From the previous point | Height change |
|---|---|---|---|
| 1 | (310.84, 2296.98, 470.04) | 33 m from Yornduf | +21 m |
| 2 | (376.05, 2316.87, 513.79) | 81 m | +44 m |
| 3 | (405.12, 2387.78, 476.62) | 85 m | −37 m |
| 4 | (312.29, 2382.61, 551.21) | 119 m | +75 m |
| 5 | (234.67, 2383.18, 575.80) | 81 m | +25 m |
| 6 | (262.28, 2344.98, 488.92) | 99 m | −87 m |

- The straight-line course is about 499 m. Ring 5 is 126 m above Yornduf.
- Ring 6 is 97 m from Yornduf and 40 m above the ground there.
- **A ring counts** when the line between two position updates crosses the ring's plane
  within 6 m of its centre. Direction does not matter, and the server does not ask whether
  the player is flying.
- **Each ring passed on this quest casts Wings of Aether** (skill 265) on the player: 9
  flight points back at once, and flight speed up to the 16 m/s cap for 6 s.
- **All six rings lie inside the fortress's FLY zone**, which spans z 429.23 to 579.23.
  Ring 5's centre is 3.44 m under that ceiling. Flying above it ends the flight.
- **Failure:** when the 70 s run out at var 2–7, the quest goes to var 9. Yornduf then shows
  page 3057, and SETPRO2 starts the course again from ring 1 with a new timer.
- A death or a world entry during the course also ends it (var 9). This is D33's guarded
  path: it only applies to a player on this course. Do not port it back.

## Rewards

| Quest | XP | Kinah | AP | Items |
|---|---:|---:|---:|---|
| Q24020 | 293,759 | 0 | 0 | one level-26 legendary chest piece, chosen |
| Q2945 | 20,110 | 0 | 250 | two Strange Green Sacks 188051192 |
| Q2946 | 20,110 | 0 | 250 | Bronze Coin Chest 188050878 |
| Q2947 | 403,012 | 4,000 | 0 | Manastone: Maximum Flight Time +4 (167000465), and one class weapon |
| Q2042 | 301,641 | 0 | 0 | Bronze Coin Chest 188050873, ten Greater Raging Wind Scrolls 164000079, five Teas of Repose 162001057 |

- **Level 26 by quest XP alone.** The five quests pay 1,038,632 XP against the 880,687
  needed: 157,945 to spare, before any XP lost to deaths. The running total passes the
  mark at Q2042's reward, the leg's last turn-in. After Q2947 it is 143,696 short.
- **Q24020's reward** has six choices. The two chain ones are both "Morheim Dark Legionary
  Hauberk", level 26, 177 physical defence: 110551147 with 12 healing boost and 9
  concentration (the fourth choice), and 110551149 with 84 MP (the fifth). The Cleric takes
  110551147. It cannot be worn until level 26.
- **Q2947's figures correct the readiness report.** The quest has three reward groups:
  31,320 Kinah, 301,641 XP and 250 AP; 4,000 Kinah and 403,012 XP; 24,000 Kinah and
  403,012 XP. The handler sets reward group 1, and `QuestService` reads the group as a
  zero-based index, in Java and in C#. Group 1 is therefore the **second** one.
  [The readiness report](natural-ntc-readiness.md) and the 2026-10-06 handoff budgeted the
  first. **AX-01 observed every row of the table above as paid**, Q2947's 403,012 XP, 4,000
  Kinah and no AP included.
- This is Java's behavior and it stays. The handler's header says the arena was once
  "choice 0" of three; whether 4.8 retail paid the arena the first group is not checked, and
  no change is proposed.
- The Cleric's weapon choices in Q2947 are **Altruist's Mace** 100101199 (the first, taken
  with REWARD1) and **Altruist's Staff** 101501224 (the second, REWARD2), both level 25.
  AX-Q1: the staff is taken. It has 460 magic boost against the worn staff's 370, so it is
  worn at once.
- These are the character's first Abyss Points: 500.

## Coin armor

Bronze Coin is item 186000007. **Vebna 204425** sells for it at Morheim Ice Fortress.
Skataon 204426, at (658.30, 899.82, 364.27), sells the same lists. The lists hold weapons
(989), cloth and chain armor (991), a scale shield (993) and coin chests (2125). They hold
no helmet, belt or accessory.

**Level 21: Rank 8, the only level-21 tier (rare).** Bought right after the commander's
talk, for each slot where it beats what is worn.

| Slot | Worn now | Defence | Rank 8 piece | Coins | Defence |
|---|---|---:|---|---:|---:|
| Torso | Altgard Legionary Hauberk 110551139 | 112 | Hauberk 110501097 | 3 | 134 |
| Gloves | Rank 9 Asmodian Handguards 111501065 | 67 | Handguards 111501066 | 2 | 80 |
| Shoulders | Rank 9 Asmodian Spaulders 112501015 | 67 | Spaulders 112501016 | 2 | 80 |
| Legs | Rank 9 Asmodian Chausses 113501074 | 90 | Chausses 113501075 | 2 | 107 |
| Feet | Altgard Legionary Brogans 114501726 | 67 | Brogans 114501082 | 2 | 80 |
| **All five** | | | | **11** | |

- The Cleric holds 7 Bronze Coins, so up to 4 are supplied.
- Other stats differ too. The Rank 8 Hauberk has 15 magic boost and 61 HP but no healing
  boost, where the worn one has 7 healing boost and 5 concentration. AX-06 writes the full
  comparison for every slot before buying.

**Level 26: Elite Rank 7, the best tier (legendary).** Bought at the end, once the Cleric
is level 26, for each slot where it beats what is then worn.

| Slot | Elite Rank 7 piece | Coins | Defence |
|---|---|---:|---:|
| Torso | Hauberk 110501104 | 13 | 177 |
| Gloves | Handguards 111501073 | 7 | 107 |
| Shoulders | Spaulders 112501023 | 7 | 123 |
| Legs | Chausses 113501082 | 10 | 142 |
| Feet | Brogans 114501089 | 7 | 123 |
| **All five** | | **44** | |

- Q24020's hauberk has the same 177 defence and 12 healing boost as the Elite torso. If
  AX-12 finds the coin torso no better, four pieces are bought for 31 coins.
- A rare level-26 tier (Rank 7) costs 18 coins for five pieces. It is not the best tier.
- The coins come from what is left, what the two Bronze Coin Chests gave, and the supply.
- "Magic" variants of these pieces exist in the item data. Vebna's lists do not hold them.
  AX-06 finds who sells them and compares them too.

## Hazards

1. **The ring course is not tight on flight time (AX-01).** A level-25 Cleric has 60 flight
   points and flies at 11.97 m/s: the base 9 m/s plus a third from the passive Winged
   Blessing I. Flying drains one point a second inside the FLY zone; gliding there drains
   one every two seconds. Each ring gives 9 points back and 16 m/s for 6 s. On those figures
   the six rings take about 32 s of the 70 s, and the flight never runs low. The supplied
   Greater Raging Wind Scroll 164000079 can be used at level 25: use is gated by the
   `restrict` attribute, which it lacks, not by its item level 30. It gives 14.67 m/s for
   300 s, and must be used standing still.
2. **Ring 5 is 3.44 m under the FLY zone's ceiling.** Leaving the zone ends the flight. Pass
   ring 5 at its centre or below, never above.
3. **The bind revive from the arena sends the Cleric to Morheim.** The bind is at Morheim Ice
   Fortress from row M3 on. A fall on the ring course then respawns beside it. The bind
   revive from the arena respawns in Morheim, fails the attempt, and costs Orhe's 2,118
   Kinah to return. A death alone fails nothing (D34 keeps Java's rule): under Hand of
   Reincarnation the Cleric revives in place and the same timer runs on. A Cleric still dead
   when the timer ends is revived beside Garm with a fifth of its HP and MP and Soul Sickness.
4. ~~**A second arena attempt must wait eleven minutes.**~~ Settled by D34: a failed
   attempt's instance is destroyed at once, and every attempt starts in a new one. The clear
   itself is unchanged. The spirits have 205 and 265 HP, so ten kills in 240 s is mostly
   running: about 230 m.
5. **The 240 s timer starts twice.** It starts on entering the arena, and again when the
   client reports the end of movie 167. A bot that skips the movie early loses nothing; one
   that never reports its end still has the first timer.
6. **Var 5 is fragile.** Between Garm's SETPRO3 and the arena entrance the bot must not
   relog or teleport. The entrance is about 37 m from Garm and 12 m lower.
7. ~~**No navigation data.**~~ Settled by AX-02: Morheim (220020000) and the arena
   (320090000) are baked and checked in.
8. **Other players' timers.** In Java, Q1044's and Q2042's enter-world hooks end any running
   quest timer. Entering the arena is a world entry, so in Java they could end Q2947's own
   240 s timer. D33 guards both hooks in C#. AX-01 confirmed it: the arena timer is still
   running after the entry, and again after movie 167 ends.
9. **Deaths cost XP.** Level 26 has 157,945 XP to spare. Enough deaths use that up, and then
   AX-11's fortress quests are needed.

## What the bot has to learn

- **Fly a course.** Six 3D waypoints in order, each passed within 6 m, inside one flight-time
  bar and a 70 s timer, then a landing. The bot can take off, fly to a point and glide down
  (Borender's rock in Leg 1). It has never flown a sequence against a clock.
- **Clear against a clock.** Pull whole groups, skip rests the timer cannot afford, and
  decide which spirit to leave.
- **Recognise and replay a failed attempt.** Var 6 at Garm and var 9 at Yornduf, with a
  bounded number of tries (AX-Q3).
- **Morheim Ice Fortress as a hub.** The teleports, the bind point, Aegir, Vebna.
- **The staff rule** (AX-Q1): always a staff, the one with the most magic boost.
- **The inventory check after every turn-in**, with opening containers and discarding.
- **Buy from a Bronze Coin vendor**, as it bought from Lohaban with Iron Coins.

It already has: capital travel and Doman's teleporter, instance entry and exit (Haramel),
movie handling, timed-quest policy (Q2288, Q2230), reward choice, and the outcome ledgers.

## AX checklist

- [x] **AX-00 - Plan the leg.** Read the Java handlers and shipped data; write the route,
  hazards, checklist and operator questions.
  - 2026-10-06: this document. Planning only. No C# or Java comparison beyond the lines
    named above, no measurement, no build and no run is claimed.
  - 2026-10-06: the operator answered AX-Q1..AX-Q6, then revised the order: Morheim and
    the commander first, level-21 coin armor at once, fortress quests if below level 26,
    level-26 coin armor at the end, and an inventory check after every turn-in. The items
    below were renumbered to AX-01..AX-14 before any of them started.
- [x] **AX-01 - Contract and server confidence.** Depends AX-00. No bot change.
  - Compare the five C# handlers with Java line by line, and the code they rely on: quest
    timers, fly-ring passing, portal entry for 700368, the arena's instance exit, movie end,
    Q24020's start on entering Morheim, reward-shop purchases.
  - With a free probe account, measure: flight time and flight speed at level 25; the drain
    while flying and gliding; whether all six rings lie inside a FLY zone; what counts as
    passing a ring; a spirit's HP, damage, XP and how far a group assists; whether a new
    arena entry gives a new instance; the bind points and their prices at Morheim Ice
    Fortress and in Pandaemonium; the three fares; the XP, Kinah and AP each quest pays;
    which reward choice gives hauberk 110551147.
  - Check which flight-speed scroll a level-25 character can use (164000079 is level 30,
    164000078 level 20), what it adds and how long it lasts.
  - Confirm the arena timer survives the world entry (hazard 8).
  - A Java/C# divergence is fixed Java-first. A defect shared with Java is recorded under
    "Blocked / questions for the operator"; it is not fixed without a decision.
  - 2026-10-06, comparison: the five C# handlers match Java token for token, apart from
    syntax and Q2042's D33 guard. `FlyRing`, `FlyRingObserver`, `FlyRingController`,
    `Plane3D`, the quest timer start and end, `CM_PLAY_MOVIE_END`, `PortalService`, the
    quest engine's enter-world, movie, ring, timer and death dispatch, `FlyController` and
    `CM_BUY_ITEM` match the same way. `InstanceService` differs only by this port's gated
    spawns. The reward-shop path was already played in CG-03. No divergence was found.
  - 2026-10-06, measured by four free-account probes at level 25 (accounts 231–234,
    `SimulationAbyssEntryContractTests.cs`, proof run `run/ax01/probes-proof.log`, 4 passed):
    - **Flight:** 60 flight points; 11.97 m/s; 3 points in 3.3 s of flying. The Greater
      Raging Wind Scroll is usable at 25 and gives 14.67 m/s. Q2042's timer is 70 s. Ring 1
      took var 2 to 3 and cast Wings of Aether: 16 m/s for 6 s, then back to 14.67. The
      timer's end gave var 9.
    - **Arena:** the timer (240 s) survived the entry and the movie. Six Mage Spirits (205
      HP) and five Warrior Spirits (265 HP), all level 25. One kill paid 738 XP and counted
      one. Leaving at var 5 gave var 6. Re-entry at once: the same instance, ten spirits.
      After 661 s: a new instance, eleven spirits. **This reuse is the behavior before D34.**
      Since D34 the probe is `AbyssEntryArenaKeepsItsTimerAndStartsEveryAttemptInANewInstance`,
      and a re-entry at once gives a new instance with eleven spirits.
    - **Morheim:** Ukin charged 2,401 Kinah; Q24020 started on arrival; the bind cost 2,690;
      Aegir paid 293,759 XP and the fourth choice gave hauberk 110551147; Orhe and Doman
      charged 2,118 each.
    - **Payments:** Q2945 20,110 XP and 250 AP; Q2946 the same; Q2947 403,012 XP, 4,000
      Kinah, no AP, the staff 101501224 with REWARD2 (its reward page is 6, not 5) and the
      manastone; Q2042 301,641 XP and its three item rewards.
  - 2026-10-06, read from data: all six rings are inside FLY zone
    `FLYINGZONESHAPESUBZONE_1_220020000` (z 429.23–579.23), at least 64 m from its edge;
    ring 5 is 3.44 m under the top. Altruist's Staff has 460 magic boost, the worn staff 370.
  - Not measured: how hard a spirit hits, and how far a group assists. The probe stood
    beside four mages for three seconds unharmed, but it had not moved since a setup
    teleport, so that shows nothing. AX-08 sees both in the real fight.
  - Kept failures: the first probe drafts are in `run/ax01/`. Account ids above 255 cannot
    log in (the session builds its address and MAC from the id); the arena kill first paid
    nothing because it was credited from 68 m away; Q2947's reward page was assumed to be 5.
  - Fast passes 107 tests with five guarded skips and all eleven scenarios in 8.59 minutes,
    the four probes among them (`run/ax01-fast/`). All seven pre-commit checks pass
    (`run/ax01/checks/`).
- [x] **AX-02 - Navigation data.** Depends AX-00. Bake 220020000 and 320090000 with
  `tools/Aion.NavBake`, and the Morheim travel graph for Asmodians. `check --maps
  220020000,320090000 --rebake` passes. Generated data is not hand-edited.
  - 2026-10-06: `bake --maps 220020000,320090000` gives Morheim 194,651 polygons in 4,421
    tiles (12.4 MiB) and the arena 159 polygons in 10 tiles (6 KiB, 13 of them door area).
    `graph --maps 220020000 --race ASMODIANS` gives 1,302 nodes, 4,772 links and 11 exits,
    with 1,137 nodes on the main network. The arena has no graph, as Bregirun has none.
  - `check --maps 220020000,320090000 --rebake` and `check --maps baked` (eleven maps) pass
    (`run/ax02/`).
  - Routes checked with `points`: in the arena, from the entry to all three spirit groups
    and the exit, one island. In Morheim, from the teleport landing to Yornduf, Orhe, the
    obelisk, Aegir, Vebna and Vallack. The obelisk's own spot is refused, as its collision
    should be; the interaction route reaches it.
  - No roads were extracted for Morheim, as for Altgard. No bot or server code changed, so
    Fast was not rerun; the four AX-01 probes, which stand on these maps, pass again on the
    checked-in meshes, and all seven pre-commit checks pass (`run/ax02/checks/`).
- [x] **AX-03 - Segment contract.** Depends AX-01. Add a contained segment that restores
  `altgard-rc-complete-s1` through `sim-snapshot.ps1` and `Invoke-NaturalJourney`. Freeze
  the five quests, the protected items and the reward items. Add arena and ring-course
  attempts to the outcome ledger. Add the one flight-speed scroll and the supplied Bronze
  Coins to the help-item profile, with their provenance. The level condition is "26 by
  questing"; no hunting. Historical segments keep their scopes.
  - 2026-10-06, the contract: `parity-artifacts/e2e/natural-abyss-entry-contract.json` is leg
    `ax`. It holds the start (176 completed quests, Q2945 at START/0, level 25), the Morheim
    hub and bind, the three teleports, all nineteen dialog steps, the two reward choices, the
    FLY zone, and an `abyssEntry` section: the arena, the ring course, what the inventory
    check opens, discards and keeps sealed, the protected items, the two supplies, both coin
    armor tiers, the staff rule and the level rule. `NaturalAbyssEntry.Validate` refuses a
    file that differs from the approved scope.
  - Attempts: `NaturalAbyssAttempts` gives the arena and the course three tries each. A
    failed arena try waited 661 s for a new instance; since D34 it enters again at once, and
    the contract's two instance-lifetime fields are gone. A third failed course asks for the
    operator's recorded flight. Each try is traced as `timed-arena-attempt` or
    `timed-ring-course-attempt`, which the outcome ledger's auditor already collects.
  - Supplies: `NaturalHelpItemAllowlist.LegApproved` approves one scroll 164000079 (AX-Q3)
    and up to 44 Bronze Coins (the AX-Q5 revision) on leg `ax` only, as running totals. The
    level-band kit is unchanged, and neither item is help on any other leg.
  - The segment: `sim-snapshot.ps1` takes `-Leg ax`. `Get-LegEnvironment` makes the leg asked
    for win over the Haramel endpoint's own `l12` selector, and keeps the Haramel receipt for
    `l12` alone. The identity rules allow the level-25 Cleric on Morheim and in the arena on
    this leg only. The journey verifies the start from the client's view and writes
    `altgard-ax-start.json`. **The segment ends there for now**; AX-05 onward add its steps.
  - Proof `run/ax03/ax03-start-a1`: public Restore of `altgard-rc-complete-s1`, the leg
    environment (`AF_ALTGARD=ax`, no Haramel receipt), `Invoke-NaturalJourney`, 22.7 s. The
    receipt shows character 133276, level 25 on map 220030000, 176 completed quests, Q2945
    alone in the journal, 748,485 Kinah, 7 Bronze Coins and the worn staff object 156530.
    The owned schema was dropped. No game action was taken.
  - Tests: sixteen contract tests pin the file against the shipped quest, spawn, teleporter,
    bind, ring, zone, instance, portal, goods-list and item data
    (`NaturalAbyssEntryContractTests.cs`), and seventeen altered starts are each refused. The
    snapshot script's contract test passes with the new leg cases. Every other leg still
    loads without the new scope.
  - Fast passes 107 tests with five guarded skips and all eleven scenarios in 12.33 minutes
    (`run/ax03-fast/`). All seven pre-commit checks pass (`run/ax03/checks/`); the first warning
    run caught one nullable warning in the new contract test, fixed before the commit.
- [ ] **AX-04 - The staff rule and the inventory check.** Depends AX-00.
  - The Cleric's equipment check always chooses a staff, and wears the staff it owns with
    the most magic boost (AX-Q1). This is the rule at all times and in every leg. It
    replaces "keep staff 101501357 equipped" and the ban on weapon auto-equipping from
    [the coin-gear leg](natural-altgard-coin-gear.md). Where a reward offers a staff, the
    staff is taken. No weapon is bought.
  - The inventory management check runs after every quest turn-in: wear better gear, open
    reward containers, discard what is to be discarded, check cube space.
  - Read the ledgers of the accepted run `rc11-full-create-s1-a25` for any moment where an
    owned staff had more magic boost than the worn one. Record what the rule would have
    changed. Do not rerun the journey for it.
- [ ] **AX-05 - Morheim and the commander.** Depends AX-02, AX-03 and AX-04. Route rows
  M1–M4: Ukin's teleport, Q24020's start on arrival, the bind at Morheim Ice Fortress
  (AX-Q2), the talk with Aegir, the hauberk 110551147, and the inventory check.
- [ ] **AX-06 - Level-21 coin armor.** Depends AX-05. Route row M5.
  - Write the manifest here first: every slot, worn against Rank 8 (and its "Magic"
    variant, if a vendor sells it), with all stats, and which pieces count as better.
  - Supply the missing Bronze Coins through the help-item mechanism, listed in the run
    profile. Buy only the manifest. Wear the pieces.
  - Buy nothing if no piece is better.
- [ ] **AX-07 - The capital steps.** Depends AX-06. Orhe's teleport to Pandaemonium, then
  route rows 1–9: Q2945, Q2946 and Q2947's acceptance at Kvasir. The inventory check opens
  the two Strange Green Sacks and the Bronze Coin Chest and records what each gives
  (AX-Q5). Resumed runs send no duplicate dialogs.
- [ ] **AX-08 - The arena.** Depends AX-07. Route rows 10–13, both movies, the timed clear,
  and the failure path through var 6. Up to three attempts. No wait is needed after a failed
  attempt: Garm's SETPRO3 leads to a new instance (D34).
- [ ] **AX-09 - Back to Morheim.** Depends AX-08. Doman's teleport; Q2947's reward taken
  with REWARD2, the staff, which the inventory check wears; the manastone 167000465
  discarded (AX-Q4); Q2042's first talk with Aegir.
- [ ] **AX-10 - The ring course.** Depends AX-01 and AX-09. Route rows 16–19, the landing,
  and the failure path through var 9.
  - Use the one supplied flight-speed scroll when the course first starts (AX-Q3): on the
    ground, standing still, before Yornduf's SETPRO2. It lasts 300 s.
  - Pass each ring through its centre, and ring 5 never above it.
  - Up to three attempts. If all three fail, stop the item and ask the operator for a
    recorded flight: they offered to fly the course while the server
    [records it](session-recording.md).
  - The existing D33 test stays green, and one probe shows a death on the course still
    fails that course, as in Java.
  - The inventory check opens the second Bronze Coin Chest when Q2042 pays it.
- [ ] **AX-11 - Level 26, by fortress quests if needed.** Depends AX-10.
  - If the Cleric is level 26 after Q2042, record that and tick the item.
  - If not, list the quests Morheim Ice Fortress offers the Cleric that have a handler, are
    not group, gathering or repeatable coin quests, and lie nearest the fortress. Read the
    Java for each. Write the list here, then play them in that order until level 26. Stop
    there; the rest of the fortress is a later leg.
  - A quest that needs a decision goes under "Blocked / questions", and the next one is taken.
- [ ] **AX-12 - Level-26 coin armor.** Depends AX-11.
  - Write the manifest here first: every slot, worn against Elite Rank 7 (and its "Magic"
    variant, if sold), with all stats, and which pieces count as better.
  - Count the Bronze Coins held. Supply the rest through the help-item mechanism, listed in
    the run profile. Buy only the manifest. Wear the pieces.
- [ ] **AX-13 - One contained SIM from the snapshot.** Depends AX-12. Restore, play the whole
  leg without help beyond the approved items, and relog at the endpoint. Record deaths,
  attempts, costs, consumables, supplied items, the level and XP. A failed run is fixed and
  rerun, and the failed evidence is kept.
- [ ] **AX-14 - Preserve the endpoint.** Depends AX-13. Save the committed-code endpoint under
  a new name, verify its restore and relog, drop every owned schema, and update
  [the readiness report](natural-ntc-readiness.md) with the measured level and XP. Run the
  final checks and Fast.

**Not in this leg unless asked:** a fresh-create full run, and an isolated LIVE run.

## Operator decisions

Answered by the operator on 2026-10-06.

| # | Question | Decided |
|---|---|---|
| AX-Q1 | Which Q2947 weapon, and is it worn? | **The equipment check always chooses a staff, and wears whatever staff we have with more magic boost. This is the rule all the time.** |
| AX-Q2 | Where is the Cleric bound and parked? | **(a)**: bind at Morheim Ice Fortress on arrival, and park there. |
| AX-Q3 | How many tries? | The operator can log in and fly the course while the server records the flight path, if the bot cannot work out the rings in time. **One "Greater Flight Speed" scroll may be supplied and used when the timed flight starts.** |
| AX-Q4 | The flight-time manastone | **Discard it.** |
| AX-Q5 | The reward containers | **Open them.** Coin armor: see the revision below. |
| AX-Q6 | Level 26 | **There are no level goals, only questing goals.** See the revision below. |

**Revised by the operator later the same day**, after the first reading of AX-Q5 would have
bought level-26 armor at level 25 and held it:

1. Begin with the quest that leads to the Morheim commander. Go to Morheim, bind, and talk
   to the commander. This unlocks many quests at the fortress.
2. At once, supply enough coins for the best level-21 coin gear, only if any of it is
   better than what is worn, and wear it as needed.
3. At the end of the Abyss entry the Cleric should be level 26. If not, do a few quests for
   the fortress.
4. At the end of whatever leg or batch of quests that is, get the best level-26 coin gear
   and wear what is better.
5. Do the inventory management checks after every quest turn-in.

What these change in the standing decisions:
- **The staff.** AX-Q1 replaces "keep the earned Altgard Dark Legionary Staff equipped" and
  "no weapon auto-equipping". Whichever owned staff has the most magic boost is worn.
- **Coin gear.** The revision goes beyond the three Iron Coin pieces approved on
  2026-10-03: Bronze Coin armor at level 21 and again at level 26.
- **Help items.** OD-13's consumables now include one flight-speed scroll for the ring
  course, and the Bronze Coins for the coin armor. Both are listed in the run profile.
- **Level.** Level 26 is reached by quests. Hunting or soul healing for it stays out.
- **Unchanged:** the stigma bundle 188053787 stays sealed; no stigma is socketed; no skill
  book is bought; Q24114, the gathering quests and Q2147 stay as decided.

## Blocked / questions for the operator

Nothing blocks AX-02..AX-14. The answers left room in four places. The loop works with
these defaults; say so to change one.

- ~~**The flight-speed scroll's tier.**~~ Settled by AX-01: a level-25 character can use the
  Greater Raging Wind Scroll 164000079, so that is the one supplied.
- **Tries (AX-Q3).** The answer did not give a number. Default: three arena attempts and
  three ring-course attempts, as AB-Q2 and AC-Q2 decided for the earlier timed quests. After
  three failed courses the item stops and asks for the operator's recorded flight.
- **What "better" means for armor.** Default: chain only, and the piece with more physical
  defence is better. The manifest still lists every stat, so a piece that gives up healing
  boost or magic boost for defence is visible before it is bought.
- **Coin weapons.** Vebna also sells staffs: Rank 8 (370 magic boost, level 21), Rank 7
  (420, level 26) and Elite Rank 7 (470, level 26, 19 coins). Altruist's Staff has 460.
  Default: no coin weapon is bought, as decided on 2026-10-03; "coin gear" is read as armor.
- **Which fortress quests (AX-11).** Default: the loop picks them by the rule in AX-11 and
  records the list before playing, without waiting for approval.

AX-Q1 was asked and answered for this Cleric. Other classes get their own weapon rule when
their profiles are planned.

**Found in AX-01, shared with Java, corrected by D34 (2026-10-06):** Q2947's timer-end hook
was not limited to the arena. While Q2947 was START with fewer than ten kills, the end of any
quest timer set it to var 6 and teleported the player to Garm. It is the same kind of defect
D33 corrected for Q1044 and Q2042. D34 limits the hook to the player's own attempt, and
resets a failed attempt's arena (deviation 156, upstream patch
`docs/upstream-reports/q2947-following-through-arena-reset.patch`). Do not port it back.

**What retail did (looked up 2026-10-06).** Sources: aioncodex's 4.8 page for quest 2947
(the 4.8 client's text), the fandom walkthrough, NCSoft's 5.8 server data and Java's history.
- **Garm sends the player in.** "I'll send you to the Arena as soon as you're ready", and
  after a failure "I'll send you back". Java's Elyos twin does this: Q1922's SETPRO3 makes a
  new instance and teleports the player. Java's Q2947 makes the player walk to the entrance
  700368. Asked below.
- **A new arena each time.** NCSoft's `instance_creation.xml` lists the arena as
  `INSTANCE_INSTANT` only, with the other quest instances, and with no rejoin entry. D34
  gives the same result.
- **A death means speaking to Garm again**, with the normal death penalty (the walkthrough).
  The arena has no resurrection point in NCSoft's entry. D34 and Java agree for a revive at
  the bind. No source covers a self-revive inside.
- **Twelve spirits, not eleven.** The walkthrough has four warriors on the left, two
  warriors and two mages ahead, and four mages on the right. The shipped spawns have three
  warriors in that first group. Recorded only: a spawn needs coordinates and a decision.
- **Ten kills end the test at once**, and the spirits "have very low HP". Both as here.
- **Only the arena is offered.** Both Java handlers say the other two choices are no longer
  available. The 4.8 client still carries their text.
- **The reward is not settled.** Java pays the second group for both twins on purpose:
  403,012 XP, 4,000 Kinah, no AP. An old wiki page shows the arena with 250 AP, the first
  group's shape. NCSoft's 5.8 data pays 4,000 gold and no AP in every group. No change.
- **Not found:** the time limit (240 s is Java's).

**Asked 2026-10-06, not yet answered:** should Garm's SETPRO3 make the new instance and
teleport the player in, as retail's text and Java's Q1922 do? It would replace the walk to
the entrance and the fragile var 5 window (hazard 6). D34 does not do this.

**Found with D34, shared with Java, not changed:**
- **Q1922 "Deliver on Your Promises", the Elyos twin, has the same unguarded timer hook.**
  While it is START with fewer than ten kills, the end of any quest timer sets it to var 6
  and teleports the player to Sanctum. It does not have the instance reuse: Epeios's
  SETPRO3 makes a new instance for every attempt.
- **The arena's instance-exit row looks wrong.** `instance_exit.xml` sends an Asmodian out of
  320090000 to Pandaemonium at (275.897, 295.694, 163.531). Those are the exit object
  730067's own coordinates inside the arena. The exit portal itself leads to (981.60,
  1552.97, 210.46). The server uses the row when it destroys an instance with a player in
  it, and when a player logs in to an instance that is gone. Where it lands a player was
  not checked. D34 never destroys an instance with a player inside, so it does not use the
  row. Shadow Court Dungeon 320120000 has the same row.
- **The arena entrance asks for no quest.** Any Asmodian can use 700368, at any step of
  Q2947. Outside var 5 no timer starts and no kill counts. D34's reset at Garm's SETPRO3
  keeps such a visit from spoiling the next attempt.
- **A relog inside the arena starts a new 240 s with the kills kept,** as long as the
  instance still exists.
- **The enter-world hook ends whichever quest timer is running.** At var 5 outside the arena
  it calls the timer's end without asking whose timer it is. Between Garm and the entrance
  that can end another quest's timer.

The original questions, with the recommendations made at the time:

- **AX-Q1 — Which Q2947 weapon, and is it worn?** The choices are Altruist's Mace and
  Altruist's Staff. Your standing decision keeps the earned Altgard Dark Legionary Staff
  (101501357, object 156530) equipped.
  - (a) Take the staff. Keep the Dark Legionary Staff equipped. AX-01 reports how the two
    compare, and you decide later.
  - (b) Take the staff and wear whichever of the two is better. It is earned, not bought.
  - (c) Take the mace.

  **Recommendation: (a).** The profile is a staff Cleric and owns no shield. (a) changes
  nothing you have already decided.
- **AX-Q2 — Where is the Cleric bound and parked?**
  - (a) Bind at Morheim Ice Fortress on arrival, and park there. Aegir, Yornduf and Vallack
    800510 (the NTC entrance) all stand in the fortress. AX-01 confirms its bind point and
    price first.
  - (b) Keep the Altgard Fortress bind, and return there at the end.

  **Recommendation: (a).** It follows the standing bind policy, a fall on the course then
  respawns beside it, and the next group work starts at Vallack. Until Morheim, the bind stays
  at Altgard: an arena death costs the trip back. If AX-01 finds a bind point in Pandaemonium,
  using it for the arena comes back to you as a question.
- **AX-Q3 — How many tries?**
  - (a) Up to three arena attempts and up to three ring-course attempts. Each is recorded.
    Three failures stop the leg as a finding to fix.
  - (b) Keep trying.

  **Recommendation: (a)**, as AB-Q2 and AC-Q2 decided for the earlier timed quests.
- **AX-Q4 — The flight-time manastone.** Q2947 gives "Maximum Flight Time +4" just before
  the course.
  - (a) Hold it. Fly the course on the Cleric's own flight time.
  - (b) Socket it into a worn piece before the course. Socketing can fail.

  **Recommendation: (a)**, and come back to you with the numbers if AX-01 shows the course
  cannot be flown without it. No flight potion or scroll is added either way.
- **AX-Q5 — The reward containers.** Two Strange Green Sacks and two Bronze Coin Chests.
  - (a) Hold them sealed, as the stigma bundle 188053787 is held.
  - (b) Open them like a player would, and record what they give.

  **Recommendation: (a).** Coins feed coin gear, which you have limited to three pieces.
- **AX-Q6 — Level 26.** The fixed rewards stop 135,814 XP short.
  - (a) Do not hunt. Record the level the leg ends at.
  - (b) Also pay a soul healer for the 97,328 recoverable XP.
  - (c) Hunt in Morheim to level 26.

  **Recommendation: (a).** The party proof is planned for levels 25–27, and the Cleric waits
  while the other classes catch up.

## Out of scope

- Entering the Abyss itself. Reshanta is a PvP map with no navigation data.
- Morheim quests beyond the few AX-11 may need. The rest of the fortress is a later leg.
- NTC, a party, other classes, crafting and gathering.
- Stigma socketing, and any purchase of weapons or skill books.
- Hunting or soul healing for a level.
- Q24114, the gathering quests Q2250/Q2275/Q2276/Q2297, and Q2147 stay as decided.

## Progress log

- 2026-10-06 — AX-00: plan written from the Java handlers and shipped data. Six operator
  questions raised. AX-01 and AX-02 are unblocked.
- 2026-10-06 — The operator answered AX-Q1..AX-Q6. The checklist was renumbered to
  AX-00..AX-12: the staff rule and the level-26 coin armor were new.
- 2026-10-06 — The operator revised the order: Morheim and the commander first (Q24020,
  which starts on entering Morheim), level-21 coin armor at once, fortress quests if below
  level 26, level-26 coin armor at the end, an inventory check after every turn-in. The
  checklist was renumbered to AX-00..AX-14 before any item started. Five defaults are
  recorded where the answers left room.
- 2026-10-06 — AX-01: no Java/C# divergence in the five handlers or the code under them.
  Four level-25 probes measured flight (60 points, 11.97 m/s, 16 m/s for 6 s per ring), the
  arena (205 and 265 HP spirits, 738 XP a kill, the instance reused for 600 s), the Morheim
  arrival (fares 2,401 and 2,118, bind 2,690) and every quest payment.
- 2026-10-06 — AX-02: Morheim (194,651 polygons, 1,302-node Asmodian graph) and the arena
  (159 polygons) are baked and checked in. Both rebake checks pass, and every route the leg
  needs in the fortress and the arena is found.
- 2026-10-06 — AX-03: leg `ax` has its frozen contract, bounded attempts, leg-scoped
  supplies and snapshot selector. A restored run verified the start (`run/ax03/ax03-start-a1`)
  and stopped there.
- 2026-10-06 — D34: Q2947 fails only the player's own arena attempt, and every attempt
  starts in a new instance. A death is unchanged. Six SIM tests pin it (`run/d34/`), and
  the bot's 661 s wait is gone from the contract.
