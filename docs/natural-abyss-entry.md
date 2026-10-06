# Natural Abyss-entry leg: Q2945 → Q2946 → Q2947 → Q2042

Status (2026-10-06): **approved. AX-00 (this plan) is done, and the operator answered
AX-Q1..AX-Q6 the same day; see [Operator decisions](#operator-decisions).** AX-01 is next.
Nothing was built, probed or run for it yet.

This leg takes the level-25 Cleric from the preserved Altgard endpoint through the four
Asmodian Abyss-entry missions. It is worked one item at a time with the
[loop protocol](natural-ascension-altgard.md#how-to-work-this-list-loop-protocol), using
**AX** in place of **NA**.

**Why this leg.** It serves the first two
[long-term goals](../CLAUDE.md#where-this-leads-the-long-term-goals):
- It play-tests server code no bot has touched: flying rings, a timed solo instance, and the
  D33 timer correction on its own quest.
- Every later character needs it. The planned Templar and Sorcerer must each complete the same
  chain, so the route and the two new skills are written once.

## Start and finish

**Start:** `sim-snapshot.ps1 -Action Restore -Name altgard-rc-complete-s1`.
- Character 133276, Asimnjour, Cleric 25, alive, bound at Altgard Fortress.
- 176 completed quests. The only active quest is Q2945 at START/0.
- 748,485 Kinah; 880,687 XP to level 26.

**Finish (the endpoint contract):**
- Q2945, Q2946, Q2947 and Q2042 are complete in the client-observed journal, each once.
- Q2947 was completed through the arena branch, with reward group 1.
- Every arena attempt and every ring-course attempt is in the outcome ledger, failures
  included. A death, a lost timer or a missed ring is an outcome, not a test failure.
- There is no level target. The level the leg ends at is recorded, not aimed for (AX-Q6).
- The Cleric took Altruist's Staff from Q2947 and wears the staff it owns with the most
  magic boost (AX-Q1).
- The flight-time manastone was discarded. The two Strange Green Sacks and the two Bronze
  Coin Chests were opened, and what they gave is recorded (AX-Q4, AX-Q5).
- The level-26 coin armor was handled as AX-09 and AX-10 say, with every supplied coin in
  the run's profile.
- The stigma bundle 188053787 is still sealed. No skill book, stigma or weapon was bought.
- GM input touched the character only for the approved help: the OD-13 consumables, one
  flight-speed scroll (AX-Q3) and the Bronze Coins for the coin armor (AX-Q5).
- The Cleric is bound and parked at Morheim Ice Fortress (AX-Q2).
- The endpoint is relog-verified and saved from committed code under a new snapshot name.
  `altgard-rc-complete-s1` and every older snapshot stay unchanged.

## Route (Java `4.8` at `ce54b7931`)

This plan rests on the four Java handlers, read in full, and on the shipped quest, spawn,
portal, teleporter and fly-ring data. The C# handlers are ports. So far only Q2947's
branch, timer and teleport lines and Q2042's D33 guard were checked in C#; AX-01 compares
all four line by line.

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

**Travel.** Altgard Fortress to Pandaemonium uses the capital travel the bot already has.
Doman 204191 sells the Morheim teleport (location 10) for 1,500 Kinah. It lands at
(309.53, 2271.51, 449.41), two metres from Yornduf. Aegir is about 167 m from there.

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
| Q2945 | 20,110 | 0 | 250 | two Strange Green Sacks 188051192 |
| Q2946 | 20,110 | 0 | 250 | Bronze Coin Chest 188050878 |
| Q2947 | 403,012 | 4,000 | 0 | Manastone: Maximum Flight Time +4 (167000465), and one class weapon |
| Q2042 | 301,641 | 0 | 0 | Bronze Coin Chest 188050873, ten Greater Raging Wind Scrolls 164000079, five Teas of Repose 162001057 |

- **Q2947's figures correct the readiness report.** The quest has three reward groups:
  31,320 Kinah, 301,641 XP and 250 AP; 4,000 Kinah and 403,012 XP; 24,000 Kinah and
  403,012 XP. The handler sets reward group 1, and `QuestService` reads the group as a
  zero-based index, in Java and in C#. Group 1 is therefore the **second** one.
  [The readiness report](natural-ntc-readiness.md) and the 2026-10-06 handoff budgeted the
  first. AX-01 confirms the paid amounts by observation.
- This is Java's behavior and it stays. The handler's header says the arena was once
  "choice 0" of three; whether 4.8 retail paid the arena the first group is not checked, and
  no change is proposed.
- Fixed XP is 744,873, and fixed Kinah is 4,000. That leaves the Cleric at level 25,
  135,814 XP short of level 26. Arena kills add to it; the amount is not known until AX-01
  measures a spirit.
- The Cleric's weapon choices in Q2947 are **Altruist's Mace** 100101199 (the first, taken
  with REWARD1) and **Altruist's Staff** 101501224 (the second, REWARD2), both level 25.
  AX-Q1: the staff is taken. The worn Altgard Dark Legionary Staff has 370 magic boost; the
  one with more is worn.
- These are the character's first Abyss Points: 500.

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
3. **A death is expensive under the current bind.** The Cleric is bound at Altgard Fortress.
   A death in the arena sends it there, one paid teleport from Garm; a fall on the course,
   two from Yornduf. AX-Q2: the Cleric binds at Morheim Ice Fortress on arrival, so a fall
   on the course respawns beside it. The arena comes before that, under the Altgard bind.
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

## What the bot has to learn

- **Fly a course.** Six 3D waypoints in order, each passed within 6 m, inside one flight-time
  bar and a 70 s timer, then a landing. The bot can take off, fly to a point and glide down
  (Borender's rock in Leg 1). It has never flown a sequence against a clock.
- **Clear against a clock.** Pull whole groups, skip rests the timer cannot afford, and
  decide which spirit to leave.
- **Recognise and replay a failed attempt.** Var 6 at Garm and var 9 at Yornduf, with a
  bounded number of tries (AX-Q3).
- **Morheim Ice Fortress as a hub.** The teleport, the obelisk, the walk to Aegir.
- **The staff rule** (AX-Q1): always a staff, the one with the most magic boost.
- **Open reward containers, and discard an item** (AX-Q4, AX-Q5).
- **Buy from a Bronze Coin vendor** (AX-Q5), as it bought from Lohaban with Iron Coins.

It already has: capital travel and Doman's teleporter, instance entry and exit (Haramel),
movie handling, timed-quest policy (Q2288, Q2230), reward choice, and the outcome ledgers.

## AX checklist

- [x] **AX-00 - Plan the leg.** Read the Java handlers and shipped data; write the route,
  hazards, checklist and operator questions.
  - 2026-10-06: this document. Planning only. No C# or Java comparison beyond the lines
    named above, no measurement, no build and no run is claimed.
  - 2026-10-06: the operator answered AX-Q1..AX-Q6. The items below were renumbered for the
    answers before any of them started: the staff rule is AX-04, and the coin armor is AX-09
    and AX-10.
- [ ] **AX-01 - Contract and server confidence.** Depends AX-00. No bot change.
  - Compare the four C# handlers with Java line by line, and the code they rely on: quest
    timers, fly-ring passing, portal entry for 700368, the arena's instance exit, movie end.
  - With a free probe account, measure: flight time and flight speed at level 25; the drain
    while flying and gliding; whether all six rings lie inside a FLY zone; what counts as
    passing a ring; a spirit's HP, damage, XP and how far a group assists; whether a new
    arena entry gives a new instance; the magic boost of the two staffs; the bind points and
    their prices at Morheim Ice Fortress and in Pandaemonium; every fare on the route; the
    XP, Kinah and AP each quest actually pays.
  - Check which flight-speed scroll a level-25 character can use (164000079 is level 30,
    164000078 level 20), what it adds and how long it lasts.
  - Confirm the arena timer survives the world entry (hazard 8).
  - A Java/C# divergence is fixed Java-first. A defect shared with Java is recorded under
    "Blocked / questions for the operator"; it is not fixed without a decision.
- [ ] **AX-02 - Navigation data.** Depends AX-00. Bake 220020000 and 320090000 with
  `tools/Aion.NavBake`, and the Morheim travel graph for Asmodians. `check --maps
  220020000,320090000 --rebake` passes. Generated data is not hand-edited.
- [ ] **AX-03 - Segment contract.** Depends AX-01. Add a contained Abyss-entry segment that
  restores `altgard-rc-complete-s1` through `sim-snapshot.ps1` and `Invoke-NaturalJourney`.
  Freeze the four quests, the protected items and the reward items. Add arena and
  ring-course attempts to the outcome ledger. Add the one flight-speed scroll and the
  supplied Bronze Coins to the help-item profile, with their provenance. The segment has no
  level target. Historical segments keep their scopes.
- [ ] **AX-04 - The staff rule.** Depends AX-00. The Cleric's equipment check always chooses
  a staff, and wears the staff it owns with the most magic boost (AX-Q1). This is the rule
  at all times and in every leg.
  - It replaces "keep staff 101501357 equipped" and the ban on weapon auto-equipping from
    [the coin-gear leg](natural-altgard-coin-gear.md).
  - Reward choices follow it: where a staff is offered, the staff is taken.
  - No weapon is bought.
  - Read the ledgers of the accepted run `rc11-full-create-s1-a25` for any moment where an
    owned staff had more magic boost than the worn one. Record what the rule would have
    changed. Do not rerun the journey for it.
- [ ] **AX-05 - The capital steps.** Depends AX-03. Route rows 1–9: Q2945, Q2946 and Q2947's
  acceptance at Kvasir. Open the two Strange Green Sacks and the Bronze Coin Chest when they
  arrive, and record what each gives (AX-Q5). Resumed runs send no duplicate dialogs.
- [ ] **AX-06 - The arena.** Depends AX-02 and AX-05. Route rows 10–13, both movies, the timed
  clear, and the failure path through var 6. Up to three attempts.
- [ ] **AX-07 - Morheim arrival.** Depends AX-04 and AX-06. Doman's teleport; the bind at
  Morheim Ice Fortress (AX-Q2); Q2947's reward taken with REWARD2, the staff; the equipment
  check; the manastone 167000465 discarded (AX-Q4); Q2042's first talk with Aegir.
- [ ] **AX-08 - The ring course.** Depends AX-01, AX-02 and AX-07. Route rows 16–19, the
  landing, and the failure path through var 9.
  - Use the one supplied flight-speed scroll when the course first starts (AX-Q3).
  - Up to three attempts. If all three fail, stop the item and ask the operator for a
    recorded flight: they offered to fly the course while the server
    [records it](session-recording.md).
  - The existing D33 test stays green, and one probe shows a death on the course still
    fails that course, as in Java.
  - Open the second Bronze Coin Chest when Q2042 pays it.
- [ ] **AX-09 - Coin armor: scope and manifest.** Depends AX-01. No purchase.
  - Find the Asmodian Bronze Coin vendor and its shipped catalogue.
  - Compare each worn armor slot with its level-26 coin piece. List the slots where the coin
    piece is better, and say by which stats.
  - Count the Bronze Coins owned after the chests, and the coins to supply.
  - Write the manifest here, with the defaults under
    [Blocked / questions](#blocked--questions-for-the-operator) applied.
- [ ] **AX-10 - Coin armor: supply and buy.** Depends AX-08 and AX-09. Supply the missing
  Bronze Coins through the help-item mechanism, listed in the run profile. Buy the manifest.
  The equipment check wears each piece once the Cleric's level allows it.
- [ ] **AX-11 - One contained SIM from the snapshot.** Depends AX-10. Restore, play the whole
  leg without help beyond the approved items, and relog at the endpoint. Record deaths,
  attempts, costs, consumables, supplied items and XP. A failed run is fixed and rerun, and
  the failed evidence is kept.
- [ ] **AX-12 - Preserve the endpoint.** Depends AX-11. Save the committed-code endpoint under
  a new name, verify its restore and relog, drop every owned schema, and update
  [the readiness report](natural-ntc-readiness.md) with the measured level and XP. Run the
  final checks and Fast.

**Not in this leg unless asked:** a fresh-create full run, and an isolated LIVE run.

### Level-26 coin armor in the item data

Bronze Coin is item 186000007. The Cleric holds seven, before the two chests. The level-26
Asmodian chain pieces, with their prices in Bronze Coins:

| Slot | Rank 7 (rare) | Coins | Elite Rank 7 (legendary) | Coins |
|---|---:|---:|---:|---:|
| Torso | 110501098 | 5 | 110501104 | 13 |
| Gloves | 111501067 | 3 | 111501073 | 7 |
| Shoulders | 112501017 | 3 | 112501023 | 7 |
| Legs | 113501076 | 4 | 113501082 | 10 |
| Feet | 114501083 | 3 | 114501089 | 7 |
| **All five** | | **18** | | **44** |

- Each piece also has a "Magic" variant at the same price, like the ones Lateni sold in Altgard.
- A level-21 tier (Rank 8) costs 11 Bronze Coins for the five pieces. It was not asked for.
- The vendor, and whether it sells a helmet or accessories, is not yet known. AX-09 finds it.
- **These pieces need level 26, and the Cleric is level 25.**

## Operator decisions

Answered by the operator on 2026-10-06.

| # | Question | Decided |
|---|---|---|
| AX-Q1 | Which Q2947 weapon, and is it worn? | **The equipment check always chooses a staff, and wears whatever staff we have with more magic boost. This is the rule all the time.** |
| AX-Q2 | Where is the Cleric bound and parked? | **(a)**: bind at Morheim Ice Fortress on arrival, and park there. |
| AX-Q3 | How many tries? | The operator can log in and fly the course while the server records the flight path, if the bot cannot work out the rings in time. **One "Greater Flight Speed" scroll may be supplied and used when the timed flight starts.** |
| AX-Q4 | The flight-time manastone | **Discard it.** |
| AX-Q5 | The reward containers | **Open them.** Also: **supply enough coins to upgrade the armor to the level-26 coin gear, if it is better than what we have, for all slots.** |
| AX-Q6 | Level 26 | **There are no level goals, only questing goals.** |

What these change in the standing decisions:
- **The staff.** AX-Q1 replaces "keep the earned Altgard Dark Legionary Staff equipped" and
  "no weapon auto-equipping". Whichever owned staff has the most magic boost is worn.
- **Coin gear.** AX-Q5 goes beyond the three Iron Coin pieces approved on 2026-10-03. It
  covers armor. A coin weapon is still not bought.
- **Help items.** OD-13's consumables now include one flight-speed scroll for the ring
  course, and the Bronze Coins for the coin armor. Both are listed in the run profile.
- **Unchanged:** the stigma bundle 188053787 stays sealed; no stigma is socketed; no skill
  book is bought; Q24114, the gathering quests and Q2147 stay as decided.

## Blocked / questions for the operator

Nothing blocks AX-01..AX-08. The answers left room in four places. The loop works with
these defaults; say so to change one.

- **The flight-speed scroll's tier.** The Greater Raging Wind Scroll 164000079 is a level-30
  item. Default: if AX-01 shows a level-25 character cannot use it, supply one Raging Wind
  Scroll 164000078 (level 20) instead. Still one scroll, used once.
- **Tries (AX-Q3).** The answer did not give a number. Default: three arena attempts and
  three ring-course attempts, as AB-Q2 and AC-Q2 decided for the earlier timed quests. After
  three failed courses the item stops and asks for the operator's recorded flight.
- **Coin armor tier.** Default: Elite Rank 7, the legendary tier, following the coin-gear
  rule "choose the better tier". That is up to 44 Bronze Coins for five pieces. AX-09 picks
  plain or "Magic" for each slot and reports why.
- **Coin armor and level 25.** The pieces need level 26, and there is no level goal.
  Default: buy them in AX-10 and hold them; the equipment check wears each one when the
  Cleric reaches level 26, in this leg or a later one.

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

- Entering the Abyss itself. Reshanta is a PvP map with no navigation data; this leg ends
  when Q2042 is complete and the coin armor is handled.
- Other Morheim quests, NTC, a party, other classes, crafting and gathering.
- Stigma socketing, and any purchase of weapons or skill books.
- Hunting for a level.
- Q24114, the gathering quests Q2250/Q2275/Q2276/Q2297, and Q2147 stay as decided.

## Progress log

- 2026-10-06 — AX-00: plan written from the Java handlers and shipped data. Six operator
  questions raised. AX-01 and AX-02 are unblocked.
- 2026-10-06 — The operator answered AX-Q1..AX-Q6. The checklist was renumbered to
  AX-00..AX-12: the staff rule (AX-04) and the level-26 coin armor (AX-09, AX-10) are new.
  Four defaults are recorded where the answers left room.
