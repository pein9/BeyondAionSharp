# Natural Morheim arrival and Abyss-entry leg

Status (2026-10-06): **approved. AX-00 (this plan) is done. The operator answered
AX-Q1..AX-Q6 and then revised the order the same day; see
[Operator decisions](#operator-decisions).** AX-01 is next. Nothing was built, probed or
run for it yet.

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
| M1 | Travel | Ukin 203581, Altgard Fortress | teleport to Morheim (location 10) for 1,700 Kinah; lands at (309.53, 2271.51, 449.41) |
| M2 | Q24020 starts | on entering Morheim | the handler starts it by itself for a character of level 21 or above |
| M3 | Bind | Morheim Ice Fortress | AX-01 finds the bind point and its price |
| M4 | Q24020 | Aegir 204301 (225.23, 2415.47, 454.11), about 167 m from the landing | QUEST_SELECT → REWARD and page 1011; then the reward |
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
| 14 | Q2947 reward | Aegir 204301, Morheim Ice Fortress (225.23, 2415.47, 454.11) | USE_OBJECT → 3739; class reward. Q2042 starts on completion |
| 15 | Q2042, var 0 | Aegir | QUEST_SELECT → 1011; SETPRO1 → var 1 |
| 16 | Q2042, var 1 | Yornduf 204319 (311.83, 2271.25, 449.41) | QUEST_SELECT → 1352; SELECT2_1_1 → movie 89, page 1354; SETPRO2 → var 2 and a 70 s timer |
| 17 | Q2042, var 2–7 | six flying rings | each ring in order adds one; the sixth gives var 8 and ends the timer |
| 18 | Q2042, var 8 | Yornduf | QUEST_SELECT → 1693; SET_SUCCEED → REWARD |
| 19 | Q2042 reward | Aegir | USE_OBJECT → 10002; reward |

**Q2947 has one working branch.** Kvasir's only progressing action is SETPRO12, which leads to
Garm's arena. The handler still holds kill code for the Morheim monsters and loot code for
the Statue of Urgasch, but no dialog reaches var 2 or var 9, so those two old choices cannot
be started. The reward is forced to group 1.

**Travel.** Three paid teleports, 4,700 Kinah in all:
- Altgard to Morheim: Ukin 203581, 1,700 Kinah (row M1).
- Morheim to Pandaemonium, before row 1: Orhe 204399, 1,500 Kinah.
- Pandaemonium to Morheim, before row 14: Doman 204191, 1,500 Kinah.

The Morheim teleport lands two metres from Yornduf.

**The arena.** Eleven level-25 spirits stand in three groups; ten must die.

| Group | Spirits | Around | From the entry |
|---|---|---|---|
| West | four Mage Spirits 213584 | (235, 239) | 68 m |
| South | two Warrior Spirits 213583 and two Mage Spirits | (276, 168) | 125 m |
| East | three Warrior Spirits | (316, 239) | 67 m |

- West to south is about 82 m, and south to east about 82 m. The whole round is about 230 m of running.
- The spirits' tribe is AGGRESSIVESUPPORTMONSTER, so a group is likely to fight together.
- Their spawn data gives no respawn time. Only one of the eleven may be left alive.
- The instance data allows one player and sets no entry cooldown.
- The tenth kill ends the timer and plays movie 168. When the client reports that movie's
  end, the server teleports the player to (1006.1, 1526, 222.2) beside Garm. The exit
  730067 at (275.90, 295.69, 163.53) leads to Pandaemonium too.
- **Failure:** when the 240 s run out, the quest goes to var 6 and the player is teleported to
  Garm. Garm then answers USE_OBJECT with page 1779, and SETPRO3 gives var 5 again.
- **Leaving early also fails it.** The enter-world hook sets var 6 when the player is at
  var 5 anywhere but the arena. AB-Q6 found that hook runs on a relog, a revive and a
  teleport that respawns the player.

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
  first. AX-01 confirms the paid amounts by observation.
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

1. **The ring course is tight on flight time.** A level-10 Cleric had 60 flight points, and
   flying drains one per tick inside a FLY zone. If the base flight speed is 9 m/s, 499 m
   takes about 55 s. That leaves about 5 s to come down 40 m from ring 6. Running out means a
   fall. AX-01 measures the level-25 flight time, the real speed and the drain before any
   bot code is written. Gliding inside a FLY zone drains at half the rate. AX-Q3 allows one
   supplied flight-speed scroll, used when the course starts. **The Greater Raging Wind
   Scroll 164000079 is a level-30 item**, and the Cleric is level 25. The Raging Wind Scroll
   164000078 is level 20. AX-01 checks which tier a level-25 character can use.
2. **The rings may not all be inside a FLY zone.** Leaving a FLY zone while flying ends the
   flight. The Morheim Ice Fortress zone's bounds are not yet checked against ring 5 at
   z 576. AX-01 does that.
3. **An arena death sends the Cleric to Morheim.** The bind is at Morheim Ice Fortress from
   row M3 on. A fall on the ring course then respawns beside it. A death in the arena
   respawns in Morheim, fails the attempt, and costs Orhe's 1,500 Kinah to return.
4. **Twenty-four seconds per spirit, running included.** If the 230 m round takes about 40 s,
   ten kills leave about 20 s each, against groups that fight together. The Cleric has never
   had a kill-rate deadline. AX-01 measures a spirit and whether a new entry gives a new
   instance with all eleven alive.
5. **The 240 s timer starts twice.** It starts on entering the arena, and again when the
   client reports the end of movie 167. A bot that skips the movie early loses nothing; one
   that never reports its end still has the first timer.
6. **Var 5 is fragile.** Between Garm's SETPRO3 and the arena entrance the bot must not
   relog or teleport. The entrance is about 37 m from Garm and 12 m lower.
7. **No navigation data.** `game-server/data/nav/` has no mesh for Morheim (220020000) or the
   arena (320090000).
8. **Other players' timers.** In Java, Q1044's and Q2042's enter-world hooks end any running
   quest timer. Entering the arena is a world entry, so in Java they could end Q2947's own
   240 s timer. D33 already guards both hooks in C#. AX-01 confirms the arena timer survives
   the entry and records it as evidence for D33.
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
- [ ] **AX-01 - Contract and server confidence.** Depends AX-00. No bot change.
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
- [ ] **AX-02 - Navigation data.** Depends AX-00. Bake 220020000 and 320090000 with
  `tools/Aion.NavBake`, and the Morheim travel graph for Asmodians. `check --maps
  220020000,320090000 --rebake` passes. Generated data is not hand-edited.
- [ ] **AX-03 - Segment contract.** Depends AX-01. Add a contained segment that restores
  `altgard-rc-complete-s1` through `sim-snapshot.ps1` and `Invoke-NaturalJourney`. Freeze
  the five quests, the protected items and the reward items. Add arena and ring-course
  attempts to the outcome ledger. Add the one flight-speed scroll and the supplied Bronze
  Coins to the help-item profile, with their provenance. The level condition is "26 by
  questing"; no hunting. Historical segments keep their scopes.
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
  and the failure path through var 6. Up to three attempts.
- [ ] **AX-09 - Back to Morheim.** Depends AX-08. Doman's teleport; Q2947's reward taken
  with REWARD2, the staff, which the inventory check wears; the manastone 167000465
  discarded (AX-Q4); Q2042's first talk with Aegir.
- [ ] **AX-10 - The ring course.** Depends AX-01 and AX-09. Route rows 16–19, the landing,
  and the failure path through var 9.
  - Use the one supplied flight-speed scroll when the course first starts (AX-Q3).
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

Nothing blocks AX-01..AX-14. The answers left room in five places. The loop works with
these defaults; say so to change one.

- **The flight-speed scroll's tier.** The Greater Raging Wind Scroll 164000079 is a level-30
  item. Default: if AX-01 shows a level-25 character cannot use it, supply one Raging Wind
  Scroll 164000078 (level 20) instead. Still one scroll, used once.
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
