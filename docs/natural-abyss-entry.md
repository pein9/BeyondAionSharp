# Natural Morheim arrival and Abyss-entry leg

Status (2026-10-06): **approved. AX-00 (this plan) is done. The operator answered
AX-Q1..AX-Q6 and then revised the order the same day; see
[Operator decisions](#operator-decisions).** AX-01 is done: the server side is compared and
measured. AX-02 is done: Morheim and the arena have checked-in navigation data. AX-03 is
done: the leg has a frozen contract, and a restored run proves its start. AX-04 is done:
the staff rule and the inventory check exist, and the check runs at the leg's start. AX-05
is done: the Cleric teleports to Morheim, binds, talks to Aegir and wears his hauberk. AX-06
is done: the Rank 8 gloves and brogans are bought with the Cleric's own coins and worn.
AX-07 is done: Q2945 and Q2946 are turned in at Pandaemonium and Q2947 is taken at Kvasir.
AX-08 is done: Garm's arena is cleared on the first try in 59 s, and both failure paths
lead back to Garm and a second, successful try. AX-09 is done: back in Morheim the Cleric
holds Altruist's Staff and has taken Q2042. AX-10 is done: the ring course is flown on the
first try in 39 s, Q2042 is turned in and the Cleric is level 26. AX-11 is done: level 26
came from the five quests, so no fortress quest was needed. AX-12 is done: the four Elite
Rank 7 pieces that beat what was worn are bought for 31 Bronze Coins, 13 of them supplied,
and worn. The operator then added three rules (see [Operator decisions](#operator-decisions)):
revive inside the arena as retail does, buy the best coin staff when it is better, and
always soul heal after an obelisk resurrection. They are AX-12a, AX-12b and AX-12c, worked
before AX-13. AX-12a is done: a Cleric that dies in the arena stands up inside it (D38),
leaves by the exit and clears the arena on its second try. AX-12b is done: after a revive
at the obelisk the Cleric is soul healed by Golenthor before it rests. AX-12c is done: the
Elite Rank 7 staff is bought with the level-26 armor and worn. AX-13, the one contained run
with its relog, is next.

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
| 10 | Q2947, var 4 | Garm 204089 (1001.38, 1528.27, 222.19) | QUEST_SELECT → 1693; SETPRO3 → var 5, and Garm sends the Cleric into a new arena (D35) |
| 11 | Q2947, var 5 | no step | Not walked since D35. The entrance 700368 at (978.18, 1556.51, 210.55) still works, but no attempt uses it |
| 12 | Q2947, arena | Triniel Underground Arena, map 320090000, arrival at (276, 293, 163) | movie 167; kill ten spirits in 240 s |
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

**The arena.** Twelve level-25 spirits stand in three groups; ten must die (D36: Java's
file had eleven).

| Room | Spirits | Around | From the entry | Door |
|---|---|---|---|---|
| West | four Mage Spirits 213584 | (235, 239) | 68 m | 10, at (245.25, 240.00, 158.63) |
| South | two Warrior Spirits 213583 and two Mage Spirits | (276, 168) | 125 m | 2, at (276.13, 176.62, 161.87) |
| East | four Warrior Spirits; two stand on the same point (320.24, 236.09) | (316, 239) | 67 m | 1, at (307.25, 239.88, 158.63) |

- **Each room is behind a closed door (AX-08).** The three static doors are closed and
  clickable in the shipped data (state 6), and in NCSoft's world file. The player clicks a
  door open (CM_OPEN_STATICDOOR); no key is needed. Until then nothing can be walked or
  cast through it. The bot had never opened a door; the first arena run stopped there.
- West to south is about 82 m, and south to east about 82 m. The whole round is about 230 m of running.
- The spirits' tribe is AGGRESSIVESUPPORTMONSTER, so a group is likely to fight together.
- AX-01: a Mage Spirit has 205 HP, an 8 m aggro range and a 16 m attack range. A Warrior
  Spirit has 265 HP, a 6 m aggro range and a 2 m attack range.
- **A Mage Spirit's kill pays 738 XP and a Warrior Spirit's 954** (AX-08). AX-01 killed only
  a Mage Spirit and wrote 738 for both. Five of each, as the bot kills them, pay 8,460 XP.
- An idle Cleric among the four Mage Spirits died in 15 s. Fighting, it never fell below
  1,551 of 2,099 HP: each spirit dies to one or two spells.
- Their spawn data gives no respawn time. Two of the twelve may be left alive.
- The instance data allows one player and sets no entry cooldown.
- **Every attempt starts in a new instance (D34).** When an attempt fails, the server
  destroys its instance as soon as the player is out of it. Garm's SETPRO3 also destroys any
  instance still registered to the player, and then makes a new one and teleports the player
  into it (D35). So every attempt meets all twelve spirits, and no wait is needed. Before D34
  the entrance returned to the failed instance for 600 s, with the dead spirits still dead.
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
- **A death fails it at once (D36).** Retail: "the player must speak to Garm again to
  retry". The death ends the timer and sets var 6. The player then revives in the ordinary
  way. After a revive in place the Cleric stands in the arena with no timer, and kills count
  for nothing: the way on is the exit and Garm. In Java a death alone failed nothing, and a
  revive in place went on with the attempt.

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
  mark at Q2042's reward, the leg's last turn-in. After Q2947 it is 143,696 short. The ten
  arena kills add about 8,460 XP to that (AX-08), and a death there took 17,453.
- **Q24020's reward** has six choices. The two chain ones are both "Morheim Dark Legionary
  Hauberk", level 26, 177 physical defence: 110551147 with 12 healing boost and 9
  concentration (the fourth choice), and 110551149 with 84 MP (the fifth). The Cleric takes
  110551147. AX-00 said it could not be worn until level 26; AX-05 showed the server lets
  the level-25 Cleric wear it. See "Found in AX-05" under
  [Blocked / questions](#blocked--questions-for-the-operator).
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

**The level-21 manifest (AX-06, written 2026-10-06 before buying).** Every stat is from the
shipped item data, as the tooltips show it (`run/ax06/manifest.json`). "Better" is the
leg's default: more physical defence. A tie is not better.

| Slot | Worn after AX-05 | Rank 8 piece at Vebna | Coins | Decision |
|---|---|---|---:|---|
| Torso | Morheim Dark Legionary Hauberk 110551147: 177 defence, 98 evasion, 88 magic resist, 12 healing boost, 9 concentration, 4 flight speed, −8 enmity | Hauberk 110501097: 134 defence, 76 evasion, 54 magic resist, 61 HP, 15 magic boost, 6 concentration, −5 enmity | 3 | **Keep.** 134 against 177 |
| Gloves | Haramel's Handguards 111501081: 78 defence, 45 evasion, 31 magic resist, 5 healing boost, 3 concentration | Handguards 111501066: 80 defence, 46 evasion, 32 magic resist, 30 HP, attack speed −2, 3 concentration, −2 enmity | 2 | **Buy.** 80 against 78; gives up 5 healing boost |
| Shoulders | Altgard Dark Legionary Spaulders 112501641: 80 defence, 46 evasion, 32 magic resist, 30 HP, 7 magic boost, 3 concentration, −2 enmity | Spaulders 112501016: the same seven stats | 2 | **Keep.** A tie |
| Legs | Altgard Dark Legionary Chausses 113501720: 107 defence, 61 evasion, 43 magic resist, 46 HP, 1 flight speed, 5 concentration, −4 enmity | Chausses 113501075: the same seven stats | 2 | **Keep.** A tie |
| Feet | Altgard Legionary Brogans 114501726: 67 defence, 39 evasion, 27 magic resist, 27 HP, 3 healing boost, 3 concentration, −2 enmity | Brogans 114501082: 80 defence, 46 evasion, 32 magic resist, 30 HP, 7 run speed, 3 concentration, −2 enmity | 2 | **Buy.** 80 against 67; gives up 3 healing boost |

- **To buy: the Handguards and the Brogans, 4 Bronze Coins.** The Cleric holds 7, so none
  is supplied.
- **The "Magic" variants are not better.** Nott 204360, 14 m from Vebna at (224.01,
  2320.13, 446.32), sells a Rank 8 "Magic" piece for every slot (goods list 990). Each has
  the plain piece's defence, evasion, magic resist, HP and magic boost. It raises enmity (+5
  to +11) where the plain piece lowers it, and has no concentration. So it never beats the
  plain piece, and none is bought.
- The gloves are the one close call: 2 defence and 30 HP against 5 healing boost. The
  default rule buys them. The operator was told on 2026-10-06 and can reverse it.
- At the preserved endpoint the Cleric wore the level-16 Rank 9 gloves, shoulders and legs
  (67, 67 and 90 defence) and carried better pieces unworn. AX-04's check put those on, and
  AX-05's turn-in added the hauberk. That is why Rank 8 now only ties the shoulders and legs.

**Level 26: Elite Rank 7, the best tier (legendary).** Bought at the end, once the Cleric
is level 26, for each slot where it beats what is then worn.

**The level-26 manifest (AX-12, written 2026-10-06 before buying).** Every stat is from the
shipped item data, as the tooltips show it: where a piece lists a stat as a base value and a
bonus, the two are added. "Better" is the leg's default: more physical defence. A tie is
not better.

| Slot | Worn at level 26 | Elite Rank 7 piece at Vebna | Coins | Decision |
|---|---|---|---:|---|
| Torso | Morheim Dark Legionary Hauberk 110551147: 177 defence, 98 evasion, 88 magic resist, 12 healing boost, 9 concentration, 4 flight speed, −8 enmity | Hauberk 110501104: the same seven stats | 13 | **Keep.** A tie |
| Gloves | Rank 8 Handguards 111501066: 80 defence, 46 evasion, 32 magic resist, 30 HP, attack speed −2, 3 concentration, −2 enmity | Handguards 111501073: 107 defence, 59 evasion, 51 magic resist, 6 healing boost, 12 magic boost, 5 concentration, −4 enmity | 7 | **Buy.** 107 against 80 |
| Shoulders | Altgard Dark Legionary Spaulders 112501641: 80 defence, 46 evasion, 32 magic resist, 30 HP, 7 magic boost, 3 concentration, −2 enmity | Spaulders 112501023: 123 defence (107 and 16), 59 evasion, 43 magic resist, 6 healing boost, 18 parry, 5 concentration, −4 enmity | 7 | **Buy.** 123 against 80 |
| Legs | Altgard Dark Legionary Chausses 113501720: 107 defence, 61 evasion, 43 magic resist, 46 HP, 1 flight speed, 5 concentration, −4 enmity | Chausses 113501082: 142 defence, 79 evasion, 57 magic resist, 72 HP, 9 healing boost, 18 magic boost, 7 concentration, −6 enmity | 10 | **Buy.** 142 against 107 |
| Feet | Rank 8 Brogans 114501082: 80 defence, 46 evasion, 32 magic resist, 30 HP, 7 run speed, 3 concentration, −2 enmity | Brogans 114501089: 123 defence (107 and 16), 59 evasion, 43 magic resist, 6 healing boost, 11 run speed, 5 concentration, −4 enmity | 7 | **Buy.** 123 against 80 |

- **To buy: the Handguards, Spaulders, Chausses and Brogans, 31 Bronze Coins.** The torso is
  a tie: Aegir's hauberk is the Elite hauberk in all but name.
- **Coins.** The Cleric brought 7, spent 4 at level 21 and got 10 from the first chest: 13.
  The second chest gave 2, 5, 6 and 9 in four runs, so it holds 15 to 22 here. **The supply
  makes up the rest of the 31: 9 to 16 coins**, inside the 44 the operator approved. The
  run counts the coins it finds and records what it supplied.
- **The "Magic" variants are not better.** Nott 204360 sells an Elite Rank 7 "Magic" piece
  for every slot. Each has the plain piece's defence, evasion and magic resist. It has MP
  where the plain piece has healing boost and concentration, and it raises enmity (+8 to
  +16) where the plain piece lowers it. None is bought.
- A rare level-26 tier (Rank 7) costs 18 coins for five pieces. It is not the best tier.
- The Flight speed lost with the chausses (1%) is the only stat given up besides HP on the
  gloves, shoulders and feet (30 each); the legs gain 26 HP.

**The staff manifest (AX-12c, written 2026-10-06 before buying).** The operator: "Also
upgrade our weapon to the best coin weapon if it's better than what we have (staff)." Vebna
sells three staffs on her weapon tab (goods list 989). "Better" is the staff rule: more
magic boost. A tie is not better. Every stat is from the shipped item data.

| Tier | Worn staff | Vebna's best staff of the tier | Coins | Decision |
|---|---|---|---:|---|
| Level 21, after Aegir | Altgard Dark Legionary Staff 101501357: 370 magic boost, 191 magic accuracy, 88-132 damage, 433 parry, 368 accuracy, 60 crit, 2.0 s; bonus 77 HP, 34 accuracy | Rank 8 Asmodian Staff 101500811: the same numbers in every stat | 4 | **Keep.** A tie |
| Level 26, at the end | Altruist's Staff 101501224: 460 magic boost, 232 magic accuracy, 112-168 damage, 512 parry, 474 accuracy, 60 crit, 2.0 s; bonus 22 physical crit, 115 MP | Elite Rank 7 Asmodian Staff 101500818: 470 magic boost, 236 magic accuracy, 114-172 damage, 521 parry, 484 accuracy, 60 crit, 2.0 s; bonus 24 physical crit, 121 MP | 19 | **Buy.** 470 against 460, and no stat is lower |

- **To buy: the Elite Rank 7 Asmodian Staff, 19 Bronze Coins, at level 26.** It needs level
  26 (`restrict`), like the Elite armor.
- The Rank 7 Asmodian Staff 101500812 (420 magic boost, 7 coins) is not the tier's best and
  is weaker than the worn staff. It is not bought.
- Altruist's Staff is kept in the cube. It is the quest's reward and is not discarded.
- **Coins.** With the four armor pieces the level-26 manifest is 31 + 19 = 50 coins. The
  Cleric holds 15 to 22 there, so **the supply is 28 to 35 coins**, inside the approved 44.

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
3. **A death in the arena is revived from inside it (D38).** The death prompt offers the
   instance revive there, not the bind. The Cleric stands up at (277.87, 289.88, 164.1),
   6 m from the exit, with a quarter of its HP and MP and soul sickness. Any death fails
   the attempt at once (D36), so the way on is the exit and Garm. The bind is at Morheim
   Ice Fortress from row M3 on: a fall on the ring course respawns beside its obelisk,
   where Golenthor soul heals the Cleric (AX-12b). The death takes the flight-speed scroll's
   effect, so the next try flies at 12.42 m/s, not 15.12; it still has time to spare.
4. ~~**A second arena attempt must wait eleven minutes.**~~ Settled by D34: a failed
   attempt's instance is destroyed at once, and every attempt starts in a new one. The clear
   itself is unchanged. The spirits have 205 and 265 HP, so ten kills in 240 s is mostly
   running: about 230 m.
5. **The 240 s timer starts twice.** It starts on entering the arena, and again when the
   client reports the end of movie 167. A bot that skips the movie early loses nothing; one
   that never reports its end still has the first timer.
6. ~~**Var 5 is fragile.**~~ Settled by D35: Garm's SETPRO3 teleports the Cleric into the
   arena, so there is no walk to the entrance at var 5 and nothing to interrupt.
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
- [x] **AX-04 - The staff rule and the inventory check.** Depends AX-00.
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
  - 2026-10-06, the staff rule: `NaturalGearPolicy.SelectUpgrades` is the one equipment
    check of every leg. Once the character owns a staff it can wear, the main hand holds
    the owned staff with the most magic boost, and no mace or shield goes on. Item level no
    longer decides between weapons. Before the first staff (the Priest in Ishalgen) the old
    rule stands. `ChooseStaffReward` picks the usable staff with the most magic boost from
    a reward's offers; for Q2947 it picks Altruist's Staff, which is the contract's choice.
    Four new unit tests cover it (`NaturalGearPolicyTests.cs`, 23 pass with the contract
    tests).
  - The inventory check: `NaturalInventoryCheck.RunAsync` wears better gear, opens the
    containers the leg names (one use at a time, with the use bar and the item's delay),
    discards what the leg discards, wears anything a container gave, proves the sealed
    bundle is untouched, and reports the free cube slots. It traces `gear-equip`,
    `reward-container-opened`, `item-discarded` and `inventory-check`. The journey's own
    `EquipUpgradesAsync` now uses the same code.
  - Probe `AbyssEntryInventoryCheckWearsTheBestStaffOpensTheRewardsAndDiscardsTheManastone`
    (account 95, `run/ax04/probe-a1.log`): a level-25 Cleric with a mace worn and three
    staffs owned ended holding Altruist's Staff 101501224 (460). Two Strange Green Sacks
    gave 166000193 and 166000192. The two Bronze Coin Chests gave 10 and 9 Bronze Coins.
    Manastone 167000465 was discarded. Bundle 188053787 was kept. 10 slots were free.
  - Audit of the accepted run (`run/ax04/staff-audit.json`, `reward-audit.json`):
    - No weapon other than a staff was ever chosen over a staff. Every reward that offered
      a staff took it: Q2009, Q24013 and Q24016. The rule changes no choice.
    - Q28505 lists Lateni's Staff (330) in a class reward the quest does not use, so it was
      never offered.
    - **The check ran too seldom.** The Altgard Legionary Staff (320) was earned at game
      time 06:18:58, at level 18, and worn at 18:04:48, at level 24. For 11 hours 46 minutes
      of play the Cleric held the Karmic Staff (260). The Dark Legionary Staff (370) was
      earned at 18:09:31 and worn only in the coin-gear leg's preparation.
  - Proof at the leg's start, `run/ax04/ax04-start-a1` (public Restore, leg `ax`, 32.4 s,
    schema dropped): the start was verified, then the check wore **five better pieces that
    sat unworn in the cube at the preserved endpoint**: Altgard Dark Legionary Spaulders
    112501641 and Chausses 113501720 (level 21, over the level-16 Rank 9 pieces), Haramel's
    Handguards 111501081 (20 over 16), Hamerun's Crystal Earrings 120001116 (21 over 14)
    and the Dark Legionary Cloth Band 123001440 (19 over 14). The staff 101501357 was
    already the best owned. Nothing was opened or discarded; 24 slots were free. The
    [Coin armor](#coin-armor) table now shows what is worn after this check.
  - Where it runs: at the leg's start now, and after each turn-in as AX-05 onward add the
    leg's steps. Earlier legs keep the points where they already ran the equipment check;
    see [Blocked / questions](#blocked--questions-for-the-operator).
  - Fast and the seven pre-commit checks: see the Progress log.
- [x] **AX-05 - Morheim and the commander.** Depends AX-02, AX-03 and AX-04. Route rows
  M1–M4: Ukin's teleport, Q24020's start on arrival, the bind at Morheim Ice Fortress
  (AX-Q2), the talk with Aegir, the hauberk 110551147, and the inventory check.
  - 2026-10-06, the leg's runner: `NaturalAbyssEntryDecisionEngine` is the leg's decision
    rule over the contract and the client's view. It gives one action at a time: `travel`,
    `bind`, `talk`, `revive`, a wait for the journal, or `frontier` when the next phase is
    not played yet. `RunAbyssEntryAsync` in the journey carries each one out with the
    existing teleport, bind and talk steps and the city approach, then runs the inventory
    check after a turn-in. **The segment now ends at the frontier `coin-armor-21`.**
  - Proof `run/ax05/ax05-morheim-a2` (public Restore of `altgard-rc-complete-s1`, leg `ax`,
    36.9 s, schema dropped, no death), in 59.4 s of game time:
    - Ukin's teleport from Altgard Fortress, 94 m from the obelisk: 2,401 Kinah, landing at
      (309.53, 2271.51, 449.41). Q24020 was in the journal on arrival.
    - The bind at obelisk 700231: 2,690 Kinah.
    - Aegir's talk: Q24020 complete in one dialog, 293,759 XP, no Kinah. The Cleric is still
      level 25, with 586,928 XP to go.
    - The inventory check after the turn-in wore the Morheim Dark Legionary Hauberk
      110551147 (177 defence) over the level-16 one. The staff 101501357 is still the best
      owned. 23 cube slots are free.
    - Kinah: 748,485 at the start, 743,394 after; every Kinah of the difference is the fare
      and the bind.
  - `VerifyMorheimArrival` checks all of that from the client's view before the segment
    ends, and writes `altgard-ax-progress.json`. Q2945 has not moved.
  - **Found:** completing Q24020 puts six Morheim campaign quests in the journal as locked:
    Q24021 to Q24026, for levels 27, 28, 29, 35, 35 and 35. This is Java's own rule. They
    are recorded in the receipt and not played; none can serve AX-11 at level 25 or 26.
  - Tests: two new contract tests cover the rule's decisions and fourteen refused arrivals
    (`NaturalAbyssEntryContractTests.cs`, 18 pass). The first run `ax05-morheim-a1` passed
    too; its receipt listed the completed and locked quests as started, so the receipt was
    split and the run repeated.
  - Fast and the seven pre-commit checks: see the Progress log.
- [x] **AX-06 - Level-21 coin armor.** Depends AX-05. Route row M5.
  - Write the manifest here first: every slot, worn against Rank 8 (and its "Magic"
    variant, if a vendor sells it), with all stats, and which pieces count as better.
  - Supply the missing Bronze Coins through the help-item mechanism, listed in the run
    profile. Buy only the manifest. Wear the pieces.
  - Buy nothing if no piece is better.
  - 2026-10-06, the manifest: written under [Coin armor](#coin-armor) before any coin was
    spent. Rank 8 beats what is worn in two slots, gloves (80 defence against 78) and feet
    (80 against 67). It loses on the torso and ties the shoulders and legs. The "Magic"
    variants Nott sells never beat the plain pieces. Cost: 4 Bronze Coins of the 7 held, so
    **nothing was supplied**.
  - The rule and the steps: `NaturalAbyssCoinArmorPolicy.Plan` compares each slot's worn
    piece with the tier's piece by physical defence, from the tooltips. A tie is kept. The
    decision rule asks for `coin-armor` while a piece is better, and for an inventory check
    while a bought piece is not worn. `NaturalAbyssCoinArmorSteps.BuyAsync` checks Vebna's
    reward shop and each piece's price in coins, buys one piece at a time and verifies the
    coins, the Kinah and the new item. Coins the Cleric lacks go through the leg's approved
    supply and into `help-items.json`; that path was not needed here.
  - Proof `run/ax06/ax06-coin-a1` (public Restore, leg `ax`, 56.4 s, schema dropped, no
    death), 90.1 s of game time: the manifest was traced first, then Handguards 111501066 and
    Brogans 114501082 were bought for 2 coins each (7 to 5 to 3) and no Kinah. The inventory
    check wore both. 3 Bronze Coins, 743,394 Kinah and 21 free cube slots are left.
    `VerifyProgress` confirmed it from the client's view: the purchases are the manifest,
    both are worn, and no Rank 8 piece beats what is worn now.
  - **The segment now ends at the frontier `capital-missions`.**
  - Tests: three contract tests cover the rule's decisions, the manifest and 24 refused
    frontiers (`NaturalAbyssEntryContractTests.cs`, 19 pass).
  - Fast and the seven pre-commit checks: see the Progress log.
- [x] **AX-07 - The capital steps.** Depends AX-06. Orhe's teleport to Pandaemonium, then
  route rows 1–9: Q2945, Q2946 and Q2947's acceptance at Kvasir. The inventory check opens
  the two Strange Green Sacks and the Bronze Coin Chest and records what each gives
  (AX-Q5). Resumed runs send no duplicate dialogs.
  - 2026-10-06, the rule: after the coin armor the decision rule takes the missions in
    order. For each it picks the contract step that matches the quest's observed status and
    var, and asks for the approved teleport first when that step is on another map. Q2947
    after Kvasir's var 0 is the frontier `arena`. A mission that is turned in before the next
    one shows in the journal is waited for.
  - No duplicate dialogs on resume: the step comes from the journal alone. A unit test
    plays every status and var of the three quests and gets each step once.
  - Proof `run/ax07/ax07-capital-a1` (public Restore, leg `ax`, 51.1 s, schema dropped, no
    death), 9 min 20 s of game time:
    - Orhe's teleport from Morheim: 2,118 Kinah, landing at (1685.7, 1400.5, 195.5).
    - Q2945: Balder, Therf, Balder again; 20,110 XP. The inventory check opened both Strange
      Green Sacks: they gave 166000191 and 166000192, one each.
    - Q2946: Balder, the three trainers 204210, 204211 and 204208, then Kvasir; 20,110 XP.
      The check opened Bronze Coin Chest 188050878: 10 Bronze Coins, 13 held now.
    - Q2947: Kvasir's SETPRO12, var 4. The Cleric stands beside Kvasir.
    - 741,276 Kinah, 333,979 XP gained in all, still level 25, 19 free cube slots.
  - `VerifyProgress` confirmed the frontier from the client's view: three turn-ins in
    order, each for its shipped XP; both fares; every container opened; the coins add up.
  - Tests: two more contract tests, the mission steps and twelve refused arena frontiers
    (`NaturalAbyssEntryContractTests.cs`, 21 pass).
  - The sacks' contents are random: AX-04's probe got 166000193 and 166000192.
  - Fast and the seven pre-commit checks: see the Progress log.
- [x] **AX-08 - The arena.** Depends AX-07. Route rows 10–13, both movies, the timed clear,
  and the failure path through var 6. Up to three attempts. No wait is needed after a failed
  attempt, and no walk: Garm's SETPRO3 sends the Cleric into a new instance (D34, D35).
  A death ends an attempt at once (D36), and the arena holds twelve spirits.
  - 2026-10-06, the rule: Garm's first talk (var 4) or his second (var 6) sends the Cleric
    in. Inside, the rule asks for one kill at a time until the journal counts ten. Var 6
    goes back to Garm while tries are left; after three failures it stops as a finding.
    Ten kills outside the arena is the report to Garm; his reward state is the frontier
    `morheim-return`.
  - The fight: a try starts when Garm's talk lands the Cleric inside and movie 167 is
    answered. The runner takes the nearest room first: it walks to the door, clicks it open,
    and kills the room's spirits from the nearest on, with the journey's own combat and no
    retreat. Under 8 s on the clock it stops and waits for the server's teleport. The
    tenth kill plays movie 168, whose end teleports the Cleric to Garm.
  - **Proof, the clear: `run/ax08/ax08-arena-a5`** (public Restore, leg `ax`, 49.7 s, schema
    dropped, no death). One try, 59.4 s: the west door at 8 s and four Mage Spirits, the
    east door and four Warrior Spirits, the south door and two more. Ten counted with 180 s
    to spare, 8,460 XP, lowest HP 1,551 of 2,099. Then Garm's SETPRO4: Q2947 at its reward.
  - **Proof, the timer: `run/ax08/ax08-arena-timeout-a1`.** The first try was left to run
    out at the entry. At 241 s the server set var 6 and teleported the Cleric to Garm. Garm's
    second talk (USE_OBJECT, page 1779, SETPRO3) sent it into a new instance, cleared in
    59 s with 181 s to spare.
  - **Proof, a death: `run/ax08/ax08-arena-death-a1`.** In the first try the Cleric opened
    the west door, walked in and did not fight. It died after 15 s. The client saw the timer
    end and var 6 at once (D36). It revived at the Morheim bind, rested 26 s, paid Orhe's
    2,118 Kinah again and went back to Garm. The second try cleared in 68 s with 172 s to
    spare. The death took 17,453 XP.
  - The two lost tries were asked for with `AX_ARENA_FIRST_TRY=timeout` and `=death`. They
    are ordinary play: waiting, and standing among the spirits. No GM input is used.
  - Every try is on the ledger as `timed-arena-attempt`, failed ones included.
  - Kept failures: `ax08-arena-a1` (no route: the closed doors), `a2` (a dead spirit chosen
    again; a spirit leaves no loot, so the client keeps its body in view unmarked), `a3`
    (the tenth kill's teleport, which a movie-skipping client follows inside the fight, was
    not accepted) and `a4` (the XP check assumed 738 for every spirit).
  - Changed for it: `GameClientPackets.OpenStaticDoor`; the three doors and the Warrior
    Spirit's 954 XP in the contract; `sim-snapshot.ps1` clears `AX_ARENA_FIRST_TRY` like its
    other switches.
  - Tests: two more contract tests, the arena decisions and ten refused cleared frontiers,
    and the doors pinned against the shipped door data (22 pass).
  - Fast and the seven pre-commit checks: see the Progress log.
- [x] **AX-09 - Back to Morheim.** Depends AX-08. Doman's teleport; Q2947's reward taken
  with REWARD2, the staff, which the inventory check wears; the manastone 167000465
  discarded (AX-Q4); Q2042's first talk with Aegir.
  - 2026-10-06, the rule: Q2947 at its reward is a talk step at Aegir, so the rule asks for
    Doman's teleport first. Once it is turned in, Q2042 shows in the journal and its var 0
    is Aegir's too. From var 1 on Q2042 is the frontier `ring-course`.
  - Proof `run/ax09/ax09-return-a1` (public Restore, leg `ax`, schema dropped, no death),
    13 min 42 s of game time for the leg so far:
    - Doman's teleport: 2,118 Kinah, the leg's third fare, 6,637 in all.
    - Aegir: Q2947 turned in with SELECTED_QUEST_REWARD2 for 403,012 XP and 4,000 Kinah.
    - The inventory check wore Altruist's Staff 101501224 (460 magic boost) in place of the
      Dark Legionary Staff (370), and discarded the one Manastone 167000465.
    - Aegir again: Q2042's SETPRO1, var 1.
    - The Cleric has gained 745,451 XP and is still level 25, 135,236 XP short of 26.
      743,158 Kinah, 13 Bronze Coins, 18 free cube slots.
  - `VerifyProgress` confirmed the frontier: four turn-ins in order, each for its shipped
    XP; the staff worn; the manastone discarded once and nothing else; the Kinah exact.
  - Tests: one more contract test and nine refused frontiers (23 pass).
  - Fast and the seven pre-commit checks: see the Progress log.
- [x] **AX-10 - The ring course.** Depends AX-01 and AX-09. Route rows 16–19, the landing,
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
  - 2026-10-06, the rule: Yornduf's talk (var 1, or var 9 after a failed try) is
    `ring-course-start`. While rings are left (var 2 to 7) the rule asks for
    `ring-course-fly` and names the next ring. Var 8 is the report to Yornduf, then Aegir's
    reward. After three failed tries it stops and asks for the operator's recorded flight.
  - The start: the runner waits for full flight time, takes the one supplied Greater Raging
    Wind Scroll through the leg's approved supply (it is in `help-items.json`), uses it
    standing beside Yornduf, then talks. Movie 89 is answered and SETPRO2 starts the 70 s.
  - The flight: take-off, then each ring through its centre and four metres on, so the
    path crosses the ring's plane. No point is within two metres of the FLY zone's ceiling,
    so ring 5 is passed just below its centre. A leg whose straight line is blocked climbs
    or sinks to the ring's height first. A ring that does not count is flown again from
    the far side, twice at most. Then down to the take-off spot and the landing.
  - **Proof, the flight: `run/ax10/ax10-ring-a2`** (public Restore, leg `ax`, schema dropped,
    no death). One try. Flight speed 15.12 m/s: the scroll, and 5% from the hauberk and the
    chausses. The rings counted 2.7, 8.4, 14.5, 26.3, 31.9 and 39.1 s after Yornduf's talk.
    The leg to ring 4 climbed 75 m first, 171 m in all, because the straight line is
    blocked. The Cleric landed 45.3 s after the talk, with 24.7 s on the clock and 54 of 60
    flight points. Yornduf's SET_SUCCEED, then Aegir: **301,641 XP, level 26.** The
    inventory check opened Bronze Coin Chest 188050873. 15 min 52 s of game time for the
    whole leg so far, 743,158 Kinah, 17 free cube slots.
  - **Proof, the failure path: `run/ax10/ax10-ring-timeout-a1`.** The first try stayed on the
    ground (`AX_RING_FIRST_TRY=timeout`, ordinary play). At 70 s the quest went to var 9.
    Yornduf's second talk (QUEST_SELECT, page 3057, SETPRO2) started a new 70 s, and the
    second try flew all six rings and landed with 24.7 s on the clock.
  - A death on the course fails it, as in Java: the existing D33 test
    `RingCourseQuestsEndOnlyTheirOwnTimer` shows the die hook and the world entry each end
    Q2042's own timer and set var 9. It stays green in Fast.
  - Every try is on the ledger as `timed-ring-course-attempt`.
  - Kept: `ax10-ring-a1` passed too, but flew the ring 4 leg straight through something
    the geometry calls solid; the server does not check a flying player's path. The leg is
    routed around it now. `ax10-ring-a2-stale-build` ran the old build after a compile error
    and proves nothing.
  - **The second chest's coins vary:** 9 in AX-04's probe, then 6, 5 and 2 in these runs.
    AX-12 has to count the coins it finds, not assume a number.
  - Tests: one more contract test and twelve refused endpoint frontiers (24 pass).
  - Fast and the seven pre-commit checks: see the Progress log.
- [x] **AX-11 - Level 26, by fortress quests if needed.** Depends AX-10.
  - If the Cleric is level 26 after Q2042, record that and tick the item.
  - If not, list the quests Morheim Ice Fortress offers the Cleric that have a handler, are
    not group, gathering or repeatable coin quests, and lie nearest the fortress. Read the
    Java for each. Write the list here, then play them in that order until level 26. Stop
    there; the rest of the fortress is a later leg.
  - A quest that needs a decision goes under "Blocked / questions", and the next one is taken.
  - 2026-10-06: **the Cleric is level 26 after Q2042.** The leg gained 1,047,092 XP against
    the 880,687 that level 26 needed, 166,405 to spare: 1,038,632 from the five quests and
    8,460 from the ten arena kills. No fortress quest was played and none is listed.
  - The rule now checks it: with the missions done, a Cleric below level 26 stops the leg
    as a finding that asks for fortress quests. It is never sent hunting or soul healing.
    The level-26 coin armor comes only after that check. A death in the arena took 17,453
    XP, so about nine deaths would be needed to fall short.
  - Proof `run/ax11/ax11-level-a1` (public Restore, leg `ax`, schema dropped, no death): the
    segment ends at the frontier `coin-armor-26`, level 26, 15 min 53 s of game time.
    `VerifyProgress` refuses that frontier below level 26.
  - Tests: the contract tests cover both sides of the check (24 pass).
  - Fast and the seven pre-commit checks: see the Progress log.
- [x] **AX-12 - Level-26 coin armor.** Depends AX-11.
  - Write the manifest here first: every slot, worn against Elite Rank 7 (and its "Magic"
    variant, if sold), with all stats, and which pieces count as better.
  - Count the Bronze Coins held. Supply the rest through the help-item mechanism, listed in
    the run profile. Buy only the manifest. Wear the pieces.
  - 2026-10-06: the manifest is under [Coin armor](#coin-armor), written before the run
    bought anything. The run decided the same one from the client's view.
  - The rule: one coin armor rule serves both tiers. After the level check it plans the
    Elite Rank 7 tier against what is worn: an owned piece that is better is put on, a
    better piece that is not owned is bought in Morheim, and a tie is left. When nothing of
    either tier is better, the leg is at its `endpoint` frontier.
  - **Proof `run/ax12/ax12-coin-a1`** (public Restore, leg `ax`, schema dropped, no death, one
    arena try, one ring-course try). At level 26 the Cleric held 18 Bronze Coins: 3 left
    from level 21, 10 from the first chest and 5 from the second. The manifest: torso keep
    (177 against 177), Handguards 111501073 (107 against 80), Spaulders 112501023 (123
    against 80), Chausses 113501082 (142 against 107), Brogans 114501089 (123 against 80).
    **31 coins, 13 supplied** through the approved help supply and listed in the run's
    `help-items.json` beside the one scroll. Vebna sold the four for coins alone (31, 24,
    17, 7, 0 left; Kinah unchanged at 743,158). The inventory check wore all four. The two
    Rank 8 pieces and the two Altgard pieces they replace stay in the cube, 13 slots free.
  - The frontier check now requires, at the endpoint: both manifests decided once and
    bought exactly; the supplied coins equal to what the manifests were short, inside the
    approved 44; in every slot the piece bought last is the one worn; no coin armor piece
    of either tier still better than what is worn. Before the missions are done it refuses
    any level-26 manifest or purchase.
  - The whole leg so far: 16 min 19 s of game time, level 26, 1,047,092 XP gained, 743,158
    Kinah, 8 inventory checks, 14 supplied items in all (1 scroll, 13 coins).
  - Tests: one more contract test, the level-26 decisions and ten refused endpoint
    frontiers (25 pass). The tooltip check now adds a piece's base and bonus defence.
  - Fast and the seven pre-commit checks: see the Progress log.
- [x] **AX-12a - Revive inside the arena (D38).** Depends AX-12. Operator, 2026-10-06:
  "Revive inside the arena, as retail does."
  - Server: the two quest arenas (Triniel 320090000 and its Elyos twin, Sanctum 310080000)
    offer the instance revive and put the player at NCSoft's own "dead start" point just
    inside the entry. Java does this for every instance that has a handler; these two
    have none. Log it as a decision and a deviation, test it, and offer it upstream.
  - Bot: a Cleric that dies in the arena takes the revive the client offers, leaves by the
    exit and talks to Garm again (D36 has already failed the attempt).
  - Proof: a contained run whose first arena try ends in a death.
  - 2026-10-06, the Java: `SM_DIE` offers the instance revive where
    `InstanceHandler.allowInstanceRevive()` is true, and `GeneralInstanceHandler` answers
    true only for an instance that has its own handler class. `CM_REVIVE` with
    INSTANCE_REVIVE then calls the handler's `onReviveEvent`. The two quest arenas have no
    handler, so Java offers the bind revive only.
  - The server change (D38, deviation 161): `TrinielUndergroundArenaInstance` and
    `SanctumUndergroundArenaInstance`. Each revives at 25% HP and MP with soul sickness, as
    Java's Draupnir and Kromede handlers do, and teleports inside the instance to the
    first of NCSoft's five points for that arena: (277.86734, 289.877625, 164.1) and
    (278.503967, 289.728882, 164.1), heading 270 degrees. The extract is
    `run/d38/retail-arena-dead-start.txt`. Offered upstream as
    `docs/upstream-reports/quest-arenas-revive-inside.patch`; it applies to Java `4.8`.
  - **SIM proof `run/d38/arena-all-a4.log`** (six arena tests pass). The death test: the
    probe died in instance 8 with one kill; var 6 at once and no timer (D36); the prompt
    offered the instance revive; 241 s later it was still dead in the arena; the revive put
    it 0.00 m from NCSoft's point, 6.2 m from the exit, with 284 of 1,138 HP; out by the
    exit the instance was destroyed; Garm sent it into instance 9 with 12 spirits and no
    kill counted. The Sanctum arena gets its handler and offers the revive too. No Elyos
    character played it.
  - The bot: where the leg says an instance revives inside, and the death prompt offers
    it, the Cleric sends the instance revive, as the client's button does. The spawn is on
    the same map, so there is no world entry. Then the rule's own next step is the exit.
  - **Contained proof `run/ax12a/ax12a-death-a2`** (public Restore, leg `ax`, first arena
    try lost by a death, schema dropped). The Cleric died 49 s into the first try with no
    kill. It revived at NCSoft's point with 362 of 1,448 HP, rested 26 s, left by the exit
    to (981.60, 1552.97) in Pandaemonium, and Garm sent it into a new arena: ten spirits
    with 136 s to spare. The death cost 17,453 XP; the leg still ends at level 26 with
    1,029,639 XP gained. It paid no extra fare: 743,158 Kinah, as in a run without a death.
    The second chest gave 2 coins, so 16 were supplied. 18 min 49 s of game time.
  - Kept: `ax12a-death-a1` failed one step after the revive. The exit had never been used
    in a contained run, and the bot planned its next route from the arena's coordinates.
    It now takes the position the exit's teleport gives it.
  - **Found, not changed: Haramel.** Haramel has a Java handler, so its death prompt offers
    the instance revive too. The accepted Haramel leg sends the bind revive there, which
    Java accepts. It is left as it is ("Don't change earlier runs").
  - Tests: the D36 death test now plays the offered revive; two more decision assertions
    (25 contract tests pass).
  - Fast and the seven pre-commit checks: see the Progress log.
- [x] **AX-12b - Soul heal after an obelisk resurrection.** Depends AX-12a. Operator,
  2026-10-06: "Always soul heal when we resurrect at an Obelisk."
  - After every bind revive the Cleric talks to the Soul Healer by the obelisk, accepts
    the price, and only then rests and goes back to work. The Kinah is on the ledger.
  - Read the Java first: dialog action RECOVERY, the price, and what it restores.
  - Proof: a contained run with one obelisk resurrection in it. The ring course is the
    leg's only place for one once AX-12a is in: the first try ends in a fall.
  - 2026-10-06, the Java (`DialogService`, action RECOVERY 35): with recoverable XP the
    server asks `STR_ASK_RECOVER_EXPERIENCE` (160011) and names the price,
    `(int) (xp * (xp < 1000000 ? 0.25 - 0.00000015 * xp : 0.1))`. Accepted, it adds the
    recoverable XP back, takes the Kinah, removes the soul sickness (skill 8291) and clears
    the death count. With nothing to recover it asks nothing and removes the sickness for
    free. C# is the same. A death takes a third of its XP for good; the rest is recoverable,
    up to a quarter of the level.
  - The rule in the bot: the revive at the bind is followed at once by the soul healing,
    then the buffs and the rest. The leg names its Soul Healer in the contract: Golenthor
    204318, title 350412 "Soul Healer", 1.2 m from the obelisk (176 NPCs carry that title).
    The step talks, chooses the healing, accepts the question, and checks from the
    client's view that no recoverable XP is left, that the XP came back, and that the
    Kinah charged is the price asked and the formula's.
  - On the ledger and in the receipt: every soul healing, the count of obelisk revives, and
    what the course's deaths and healings changed in XP. The frontier check requires one
    healing per obelisk revive, Golenthor's, at the formula's price, and takes the price
    off the Kinah account.
  - **SIM proof `run/ax12b/soulheal-probe-a1.log`** (free account 97). A level-25 probe
    bound at Morheim died: 17,453 XP gone, 5,817 for good and 11,636 recoverable. The bind
    revive left soul sickness. Golenthor, 1.3 m from the revive point, gave the 11,636 XP
    back for 2,888 Kinah and took the sickness off. A second visit asked nothing and cost
    nothing.
  - **Contained proof `run/ax12b/ax12b-fall-a3`** (public Restore, leg `ax`, first course
    try lost by a fall, schema dropped). With ring 1 passed the Cleric climbed to 60 m over
    the roof below it, ended its flight and fell: all of its HP, as Java deals from 50 m.
    Q2042's die hook failed the course (var 9, D33's guard). It revived at Morheim's
    obelisk and Golenthor healed it: **108,964 XP back for 25,460 Kinah.** That is the
    11,636 this death left recoverable and 97,328 the Cleric had carried since the earlier
    legs. It rested, Yornduf started a second try, and it flew the six rings at 12.42 m/s
    with 23 s on the clock at the sixth and 15.5 s at the landing. Level 26 at the end,
    1,138,603 XP gained, 717,698 Kinah, 11 coins supplied, 17 min 26 s of game time.
  - **Found: the death takes the scroll's effect.** The one supplied scroll was used at
    the first start. The second try flew without it and still passed. No second scroll
    was supplied.
  - **Found: 97,328 recoverable XP carried in.** See "Blocked / questions".
  - Kept: `ax12b-fall-a1` (no clear air straight above the take-off spot; the fall now
    starts beyond ring 1) and `ax12b-fall-a2` (the fall killed the Cleric, and the run
    looked for the death prompt half a second before the server sends it).
  - The rule is on in this leg and later ones. The accepted earlier legs are unchanged.
  - Tests: one more contract test and eight refused endpoint ledgers (26 pass); one more
    SIM test.
  - Fast and the seven pre-commit checks: see the Progress log.
- [x] **AX-12c - The best coin staff.** Depends AX-12. Operator, 2026-10-06: "Also upgrade
  our weapon to the best coin weapon if it's better than what we have (staff)."
  - Write the staff manifest under [Coin armor](#coin-armor) first, for both tiers: the
    worn staff against the tier's coin staffs, every stat, and which is better. The staff
    rule decides it: more magic boost. A tie is not better.
  - Supply the coins it is short, inside the approved 44, listed in the run profile. Buy
    only the manifest. The equipment check wears it.
  - 2026-10-06: the staff manifest is under [Coin armor](#coin-armor), written before the
    run bought anything.
  - The rule: each tier names its best staff, on Vebna's weapon tab (goods list 989). The
    coin gear rule plans it with the armor: more magic boost than the worn staff is
    better, an owned better staff is put on, a tie is left. The contract's weapon rule now
    buys. Q2947's Altruist's Staff has to be owned at the end; which staff is worn is the
    staff rule's check.
  - **Proof `run/ax12c/ax12c-staff-a1`** (public Restore, leg `ax`, schema dropped, no death).
    Level 21: the Rank 8 staff ties with the worn one at 370 and is not bought. Level 26:
    the Cleric held 18 coins; the manifest was the four armor pieces and the Elite Rank 7
    Asmodian Staff 101500818 (470 magic boost against 460), 50 coins, **32 supplied** and
    listed in `help-items.json`. Vebna sold all five for coins alone, the staff from her
    weapon tab. The inventory check wore the staff and the armor. Altruist's Staff is in
    the cube; 12 slots are free. 16 min 20 s of game time, 743,158 Kinah.
  - Tests: the contract test pins both staffs to Vebna's list, their prices and their magic
    boost, and that the level-26 one is the best staff she sells (26 pass).
  - Fast and the seven pre-commit checks: see the Progress log.
- [ ] **AX-13 - One contained SIM from the snapshot.** Depends AX-12c. Restore, play the whole
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

**Added by the operator on 2026-10-06, during AX-04:** Garm sends the player straight into
the arena, as retail and the Elyos twin do. This is D35.

**Added the same day, before AX-05:** "Q2947 - Do whatever retail does! The goal is to get
as close to retail as possible." This is D36: a death fails the attempt, and the arena has
twelve spirits. And: "Don't change earlier runs", so the earlier legs keep their equipment
check points.

**Added the same day, after AX-12:** three more rules.
- "Revive inside the arena, as retail does." This answers the question that was open
  below. It is D38 and AX-12a.
- "Also upgrade our weapon to the best coin weapon if it's better than what we have
  (staff)." This withdraws "no coin weapon" from 2026-10-03. AX-12c.
- "We need to add a new rule I didn't realize we weren't doing already: Always soul heal
  when we resurrect at an Obelisk." AX-12b.

What these change in the standing decisions:
- **The staff.** AX-Q1 replaces "keep the earned Altgard Dark Legionary Staff equipped" and
  "no weapon auto-equipping". Whichever owned staff has the most magic boost is worn.
- **Coin gear.** The revision goes beyond the three Iron Coin pieces approved on
  2026-10-03: Bronze Coin armor at level 21 and again at level 26.
- **Help items.** OD-13's consumables now include one flight-speed scroll for the ring
  course, and the Bronze Coins for the coin armor. Both are listed in the run profile.
- **Level.** Level 26 is reached by quests. Hunting or soul healing for it stays out.
  Soul healing after an obelisk resurrection is ordinary play and is always done (AX-12b);
  it gives back XP a death took, and is not a way to reach a level.
- **Coin weapon.** The best coin staff of a tier is bought when it has more magic boost
  than the worn staff (AX-12c).
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
- ~~**Coin weapons.**~~ Settled by the operator, 2026-10-06: the best coin staff is bought
  when it is better than the worn one. Vebna sells staffs: Rank 8 (370 magic boost, level
  21), Rank 7 (420, level 26) and Elite Rank 7 (470, level 26, 19 coins). Altruist's Staff
  has 460. AX-12c writes the manifest.
- **Where the soul heal rule applies (AX-12b).** The rule says "always". Default: it is on
  in this leg and every later one. The accepted earlier legs, Ishalgen to Haramel, are
  left as they are, as "Don't change earlier runs" decided for the equipment check. Say so
  to turn it on there too.
- **The recoverable XP the Cleric already carries (found in AX-12b).** The snapshot's
  Cleric logs in with 97,328 recoverable XP, left by deaths in the earlier legs, which
  never soul healed. The rule heals after an obelisk resurrection, so a run without one
  leaves that XP where it is. Default: the rule as given. Say so to have the Cleric soul
  heal on arriving at Morheim's obelisk as well: 97,328 XP back for 22,911 Kinah.
- **A revive that is not at an obelisk (AX-12a, AX-12b).** The rule names the obelisk.
  Default: after a revive inside the arena the Cleric does not go looking for a Soul
  Healer; it leaves, talks to Garm and tries again with the soul sickness it has. Say so
  to have it soul heal at Pandaemonium's Soul Healer first.
- **Which fortress quests (AX-11).** Default: the loop picks them by the rule in AX-11 and
  records the list before playing, without waiting for approval.

- **The inventory check in the earlier legs.** AX-04 found the accepted run wore its
  staffs and five armor pieces late, because Ishalgen to Haramel run the equipment check
  at a few fixed points only. **Settled by the operator, 2026-10-06: "Don't change earlier
  runs."** Those legs keep their points. The staff rule itself is already in every leg.

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
  700368. **Corrected by D35**, on the operator's answer below.
- **A new arena each time.** NCSoft's `instance_creation.xml` lists the arena as
  `INSTANCE_INSTANT` only, with the other quest instances, and with no rejoin entry. D34
  gives the same result.
- **A death means speaking to Garm again**, with the normal death penalty (the walkthrough:
  "Deaths will incur death penalty as normal, and the player must speak to Garm again to
  retry"). Java agreed only for a revive at the bind. **Corrected by D36:** every death
  fails the attempt at once.
- **Twelve spirits, not eleven.** The walkthrough has four warriors on the left, two
  warriors and two mages ahead, and four mages on the right. NCSoft's own world file for
  the arena (`Worlds/iddc1_arena/world.xml`) confirms it: three parties of four. Eleven of
  its points are the shipped spots exactly. The twelfth repeats the third east Warrior's
  point, so a file made without duplicates lost it. **Corrected by D36.**
- **The revive point.** The same file gives five "dead start" points just inside the
  arena's entry, about 6 m from the exit. Seven Java instance handlers revive players at
  their own world's points, Taloc's Hollow to the same coordinates. So in retail a player
  who died here revived inside, not at the bind point. **Corrected by D38** for the two
  quest arenas.
- **Ten kills end the test at once**, and the spirits "have very low HP". Both as here.
- **Only the arena is offered.** Both Java handlers say the other two choices are no longer
  available. The 4.8 client still carries their text.
- **The reward is not settled.** Java pays the second group for both twins on purpose:
  403,012 XP, 4,000 Kinah, no AP. An old wiki page shows the arena with 250 AP, the first
  group's shape. NCSoft's 5.8 data pays 4,000 gold and no AP in every group. No change.
- **Not found:** the time limit (240 s is Java's).

**Asked and answered 2026-10-06:** should Garm's SETPRO3 make the new instance and teleport
the player in, as retail's text and Java's Q1922 do? The operator: "send the player straight
in, as retail and the Elyos twin do." Done as **D35** (deviation 157, upstream patch
`docs/upstream-reports/q2947-following-through-garm-sends-you-in.patch`): from var 4 and
var 6, Garm's SETPRO3 makes a new instance and teleports the player to (276, 293, 163).
The walk to the entrance and the var 5 window (hazard 6) are gone. Do not port it back.

**Asked and answered 2026-10-06:** should any death fail the attempt at once? The operator:
"Q2947 - Do whatever retail does! The goal is to get as close to retail as possible." Done
as **D36** (deviations 158 and 159, upstream patch
`docs/upstream-reports/q2947-following-through-death-and-twelfth-spirit.patch`): a death
fails the attempt at once, and the arena has retail's twelve spirits. The evidence extracts
are in `run/d36/`. Do not port it back.

**Asked and answered 2026-10-06:** should a player who dies in the arena revive inside it,
at NCSoft's points beside the exit, instead of at the bind point? The evidence is above.
Every retail world carries such points, and Java uses them for the instances that have a
handler. The operator: "Revive inside the arena, as retail does." It is **D38**, applied to
the two quest arenas (AX-12a).

**Found in AX-05, shared with Java, not changed: armor with no level limit.** The level-25
Cleric wore the level-26 Morheim Dark Legionary Hauberk at once. Java's item data gives that
piece no `restrict` attribute, and Java reads a missing one as "every class from level 1".
C# does the same. 9,757 of the 49,401 wearable item templates have no `restrict`: 1,793
head pieces, most quest reward gear, many weapons. Retail showed a level on such items;
whether it enforced it for them is not checked. No change is proposed here. It is a
question for the item data as a whole, and the 4.8 client's own item file could settle it.

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
  keeps such a visit from spoiling the next attempt. Since D35 no attempt uses it.
- **A relog inside the arena starts a new 240 s with the kills kept,** as long as the
  instance still exists.
- **The enter-world hook ends whichever quest timer is running.** At var 5 outside the arena
  it calls the timer's end without asking whose timer it is. Since D35 a player is at var 5
  outside the arena only after leaving it, when the running timer is the arena's own.

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
- 2026-10-06 — AX-04: the equipment check always wears the owned staff with the most magic
  boost, and the inventory check wears, opens, discards and counts. A probe proved all four
  (`run/ax04/probe-a1.log`). At the leg's start it wore five better pieces the endpoint
  carried unworn (`run/ax04/ax04-start-a1`). The accepted run chose every staff correctly
  but wore one 11 hours 46 minutes late. Fast passes 113 of 118 tests with five guarded
  skips and all eleven scenarios in 9.50 minutes (`run/ax04-fast/`); the seven pre-commit
  checks pass (`run/ax04/checks/`).
- 2026-10-06 — D35: Garm's SETPRO3 makes a new arena and teleports the player in, as the
  operator asked. Seven arena SIM tests pass (`run/d35/arena-all-a1.log`); the contract's two
  Garm steps now end in the arena and the entrance is out of the bot's route. The first Fast
  run failed in an unrelated probe (`run/d35/failed/`): Haramel's HM-04 pulled a monster that
  was still walking home from the fight before, and the pull was refused four times. Fast
  shares one world, clock and dice, so the new test moved that fight. The probe now waits
  for the monster to be home, as other probes do. Fast then passes 114 of 119 tests with
  five guarded skips and all eleven scenarios (`run/d35-fast2/`); the seven checks pass.
- 2026-10-06 — D36: Q2947 follows retail in two more places. A death fails the arena attempt
  at once, and the arena has NCSoft's twelve spirits. Seven arena SIM tests pass
  (`run/d36/arena-all-a2.log`). NCSoft's revive points inside the arena are recorded, not
  applied. The earlier legs stay as they are. Fast passes 114 of 119 tests with five
  guarded skips and all eleven scenarios in 9.72 minutes (`run/d36-fast/`); the seven
  pre-commit checks pass (`run/d36/checks/`).
- 2026-10-06 — AX-05: the leg has its decision rule and runner. A contained run teleported
  to Morheim (2,401 Kinah), bound at the fortress (2,690), completed Q24020 with Aegir
  (293,759 XP) and wore his hauberk, in 59 s of game time (`run/ax05/ax05-morheim-a2`). The
  segment ends at the level-21 coin armor. Six campaign quests entered the journal locked.
  Fast passes 114 of 119 tests with five guarded skips and all eleven scenarios in 11.05
  minutes (`run/ax05-fast/`); the seven pre-commit checks pass (`run/ax05/checks/`).
- 2026-10-06 — AX-06: the level-21 manifest was written first. Rank 8 beats what is worn in
  the gloves and the feet only; both were bought for 4 of the Cleric's own 7 Bronze Coins
  and worn (`run/ax06/ax06-coin-a1`). Nothing was supplied. The segment ends at the capital
  missions. Fast passes 114 of 119 tests with five guarded skips and all eleven scenarios
  in 9.37 minutes (`run/ax06-fast/`); the seven pre-commit checks pass (`run/ax06/checks/`).
- 2026-10-06 — AX-07: the capital steps play from the journal. A contained run took Orhe's
  teleport (2,118 Kinah), turned in Q2945 and Q2946 (20,110 XP each), opened the two sacks
  and the coin chest (10 coins) and took Q2947 at Kvasir, in 9 min 20 s of game time
  (`run/ax07/ax07-capital-a1`). The segment ends at Garm's arena. Fast passes 114 of 119
  tests with five guarded skips and all eleven scenarios in 9.22 minutes (`run/ax07-fast/`).
  The seven pre-commit checks pass (`run/ax07/checks/`); the first warning run caught four
  nullable warnings in the new unit test, fixed before the commit.
- 2026-10-06 — AX-08: Garm's arena. The rooms are behind closed doors the player clicks
  open; the bot does that now. A contained run cleared the arena on the first try in 59 s
  with 180 s to spare (`run/ax08/ax08-arena-a5`). A try lost to the timer and one lost to
  a death each led back to Garm and a clear on the second try. Fast passes 114 of 119 tests
  with five guarded skips and all eleven scenarios in 8.63 minutes (`run/ax08-fast/`); the
  seven pre-commit checks pass (`run/ax08/checks/`).
- 2026-10-06 — AX-09: back in Morheim by Doman's teleport (2,118 Kinah). Q2947 paid 403,012
  XP and 4,000 Kinah; the inventory check wore Altruist's Staff and discarded the
  manastone; Q2042 is taken (`run/ax09/ax09-return-a1`). The Cleric is 135,236 XP short of
  level 26, which Q2042's 301,641 covers. The segment ends at the ring course. Fast passes
  114 of 119 tests with five guarded skips and all eleven scenarios in 9.72 minutes
  (`run/ax09-fast/`); the seven pre-commit checks pass (`run/ax09/checks/`).
- 2026-10-06 — AX-10: the ring course. One supplied scroll, used at the start. A contained
  run flew all six rings in 39.1 s and landed with 24.7 s on the clock; Q2042 paid 301,641
  XP and the Cleric is level 26 (`run/ax10/ax10-ring-a2`). A try left on the ground failed
  at 70 s and the second try flew the course. The segment ends at the level check. Fast
  passes 114 of 119 tests with five guarded skips and all eleven scenarios in 8.58 minutes
  (`run/ax10-fast/`); the seven pre-commit checks pass (`run/ax10/checks/`).
- 2026-10-06 — AX-11: level 26 after Q2042, by quests alone, with 166,405 XP to spare. No
  fortress quest was needed. The rule stops the leg as a finding if the Cleric is ever
  below 26 there (`run/ax11/ax11-level-a1`). The segment ends at the level-26 coin armor.
  Fast passes 114 of 119 tests with five guarded skips and all eleven scenarios in 9.09
  minutes (`run/ax11-fast/`); the seven pre-commit checks pass (`run/ax11/checks/`).
- 2026-10-06 — AX-12: the level-26 coin armor. The manifest was written first: four Elite
  Rank 7 pieces for 31 Bronze Coins, the hauberk a tie. A contained run held 18 coins, was
  supplied 13, bought the four and wore them (`run/ax12/ax12-coin-a1`). The segment ends at
  the endpoint. Fast passes 114 of 119 tests with five guarded skips and all eleven scenarios in 8.85
  minutes (`run/ax12-fast/`); the seven pre-commit checks pass (`run/ax12/checks/`).
- 2026-10-06 — The operator added three rules after AX-12: revive inside the arena as retail
  does, buy the best coin staff when it is better, and always soul heal after an obelisk
  resurrection. They are AX-12a, AX-12b and AX-12c, before AX-13.
- 2026-10-06 — AX-12a (D38): the two quest arenas have instance handlers, so a death there
  is revived from inside, at NCSoft's point by the exit. SIM: six arena tests pass
  (`run/d38/arena-all-a4.log`). Contained: the first try ended in a death, the Cleric
  revived inside, left by the exit and cleared the second try (`run/ax12a/ax12a-death-a2`).
  Fast passes 114 of 119 tests with five guarded skips and all eleven scenarios in 9.19 minutes
  (`run/ax12a-fast/`); the seven pre-commit checks pass (`run/ax12a/checks/`).
- 2026-10-06 — AX-12b: soul healing after an obelisk resurrection. SIM: a death, the bind
  revive, and Golenthor gives the 11,636 recoverable XP back for 2,888 Kinah
  (`run/ax12b/soulheal-probe-a1.log`). Contained: the first course try ended in a 60 m
  fall, the Cleric revived at the obelisk, was healed (108,964 XP for 25,460 Kinah, 97,328
  of it carried from earlier legs) and flew the second try (`run/ax12b/ax12b-fall-a3`).
  Fast passes 115 of 120 tests with five guarded skips and all eleven scenarios in 8.62 minutes
  (`run/ax12b-fast/`); the seven pre-commit checks pass (`run/ax12b/checks/`).
- 2026-10-06 — AX-12c: the best coin staff. The manifest was written first: a tie at level
  21, the Elite Rank 7 staff (470 against 460) for 19 coins at level 26. A contained run
  held 18 coins, was supplied 32, bought the four armor pieces and the staff and wore them
  (`run/ax12c/ax12c-staff-a1`). Fast passes 115 of 120 tests with five guarded skips and all eleven
  scenarios in 8.59 minutes (`run/ax12c-fast/`); the seven pre-commit checks pass
  (`run/ax12c/checks/`).
