# Natural Morheim leg, to level 31

Status: a plan, 2026-10-13, as the operator shaped it in three messages that day. Step M1, the quest list, is done:
[natural-morheim-quests.md](natural-morheim-quests.md). Steps M2 (Morheim Ice Fortress) and M3 (Kellan's Cabin) are
played, and step M4 (legs `md` and `mc`), where every bot reached 31 (`morheim-mc-s1-w1`). Step M7's gear check at 31
came next, on the operator's word (`morheim-g31-s1-w1`), then M8 and M5: the world is now `morheim-ma-s1-w1`, every
bot at 32, and the group step M6 starts from it.

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
2. **All eight at once.** A round is the eight bots in the one world, each playing alone, each starting ten game
   minutes after the one before, as the muster rounds were played. The operator, 2026-10-13, after "maybe 5 at a
   time?": "Can we do 'in parallel' all 8 classes in the world, each a delayed start? that way we can sim faster and
   finish. (we did this before)".
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
- [x] **M2 Morheim Ice Fortress.** The leg's contract and stage for a Morheim hub, on the fortress's quests. The
  first round of the eight from `ntc-group-s5`.
  - 2026-10-13: stage `mf`, contract `parity-artifacts/e2e/natural-morheim-mf-contract.json`: Q2302, Q2304, Q2305,
    Q2306 and Q2499 from their compiled plans, Q2493, Q2421 and Q2303 by steps, the leg ending at Favyr where Q2303
    and Q2306 are handed in. Round `m2-mf`, all eight ten game minutes apart, captured `morheim-mf-s1-w1` (and
    `morheim-mf-s1-<class>`): every bot did all eight quests with no death, in 24 to 31 game minutes each (Chanter
    24, Templar 26, Ranger 26, Sorcerer 26, Cleric 26, Gladiator 28, Assassin 30, Spirit Master 31). The Assassin
    is 27, the rest 26. The trials found three bot faults, all fixed: Purra stands on the fortress wall, which no
    route reaches, so the bot flies to her; a takeoff indoors under the fortress roof was refused, so the bot walks
    to open ground first; and Bragi walks a round, so the bot waits for him to stop before it talks. A bot with no
    route back to the hub flies there, or returns to its bind when it cannot take off (the operator: "If stuck on
    lake, can always FLY back"); this round did not need it. No server fault was found. Left at the fortress for
    later: Q24021 (level 27), Q26905 (28) and Q24023 (29), the campaign's next steps, and Q2330 and Q4300, which
    are handed in at Brusthonin.
- [x] **M3 Kellan's Cabin,** 27 quests.
  - 2026-10-14: stage `mk`, contract `parity-artifacts/e2e/natural-morheim-mk-contract.json`, from `morheim-mf-s1`: 26 of
    the 27, 22 from their compiled plans and Q2316, Q2498, Q2307 and Q2428 by steps; the leg binds at the cabin and
    ends at Nicoyerk in the fortress with Q2495. Q2319 (to Luimerk) and Q2356 (to Utgar) are taken and worked here
    and held for Alsig Village and Slag Bulwark. Round `m3-mk`, all eight ten game minutes apart, captured
    `morheim-mk-s1-w1`: every bot did all of them with no death; the Gladiator and the Assassin are 29, the rest 28.
    Each took 3.2 to 7.3 game hours (Cleric 3.2, Chanter 3.5, Ranger 3.9, Sorcerer 5.4, Spirit Master 5.9, Assassin
    6.2, Templar 6.3, Gladiator 7.3), most of the spread waiting at the worn document.
  - My decision, his to change: **Q2494 Three Treasures goes to the group step M6.** Sku, Paku and Seku are one spawn
    each (30, 30 and 60 minutes) and their items drop for each member of a group (`drop_each_member`); eight bots
    alone queued for hours on them (trial t3). The same holds for Q2328 at Desert Garrison (step M4).
  - New for the leg engine: a quest started by an item (`itemStarts`): got from an object, waited for at its spots
    (Q2498's worn document is one at a time and respawns 2950 s after a use: the bots waited 1 to 217 game minutes for
    it, in turn), or from a drop (Q2316's Old Notebook, a 5% drop of the Sprigg Habitat's sparkies: 11 to 123 tries).
    An object used with what the quest collects at the same var waits for the collection (Q2307's soup and its four
    ingredients). A bounded wait the bot cannot hurry counts as progress for the journey's one-hour watchdog.
  - The trials (t1 to t6) found these bot faults, all fixed: a template collection kept waiting on the one source of
    two that the other bots kept dead (Q2429's lone 212374 brax); a walking NPC (Spedor, on a long round) was talked
    to where he had been; a self-heal "refused" stopped the Assassin (M4 found the cause: a skill at a target that
    has just died is refused twice, Java `Skill.isValidTarget` and then the empty target list, and the bot read the
    second refusal as its next cast's); the item hand-in took another handler's dialog window for Vivi's page; the endpoint relog
    counted the invasion daily the server starts on entering Morheim (Q49020) as a changed journal.
  - Found in the server, both as Java has them and left so: Q2718's handler (Reshanta) sends an empty dialog window to
    everyone who talks to Vivi without having taken it; Q2307's handler does not check the soup's four ingredients
    (Java: "INGREDIENT CHECK NEED TO SCRIPT"), so the soup can be made without them. The bots gather them anyway.
- [x] **M4 Desert Garrison,** 18 quests.
  - 2026-10-14, the garrison's own quests: stage `md`, contract `parity-artifacts/e2e/natural-morheim-md-contract.json`,
    from `morheim-mk-s1`: 13 quests (11 from their compiled plans, Q2332 and Q2345 by steps), ending at Vili in the
    fortress with Q2345. Round `m4-md`, all eight ten game minutes apart, captured `morheim-md-s1-w1`: every bot did
    all 13 and stood at the endpoint, level 30 (the Gladiator, the Cleric and the Assassin 31), in 4.5 to 7.3 game hours,
    4 to 10 deaths each. The Cleric and the Assassin are recorded as stopped: the endpoint's second relog check (the
    later-capital receipt) did not yet allow what the server gives at a login, the invasion daily Q49020 and a stigma's
    next rank (fixed after the round; their characters are at the endpoint with every quest done).
  - My decisions, his to change: **Q2328, Q2344 and Q2343 go to the group step M6**: Millennium Eka is one spawn
    (1800 s) whose item drops for each member of a group; Q2344's ice claw flags and Q2343's stone altar censer stand
    in the Ice Claw village among elite brutal ice claws (levels 28 to 30), where a lone Cleric died 15 times for 2 of
    the 5 flags (trial t2). And: **a template quest a bot makes no progress on alone for the journey's hour is left for
    the group step**, with what needs it (no bot needed it in the round).
  - The trials (t1 to t4) found these bot faults, all fixed: the flags' plateau is a navmesh island the bake split
    from the rest of Morheim 0.9 m away at a pass, so a route crosses listed seams now (a search for every seam broke
    the group NTC replay, so only checked ones are listed: this one); eight bots on one herd of spectres (35% drops)
    left a bot an hour without a drop, so a collection counts its own kills as progress (forty in a row without the
    item end it); a lone named target (Chieftain Promakis) that other bots keep dead is waited for again; a skill at a
    target that has just died is refused twice (Java `Skill.isValidTarget`, then the empty target list) and the second
    refusal was read as the next cast's (the Assassin in M3, the Cleric here); the endpoint relog allowed neither the
    invasion daily nor a stigma rank granted at login.
  - Found in the server, as Java has it and left so: a stigma's next rank (the Assassin's Searching Strike II at 30)
    comes only at the next login (Java `StigmaService.onPlayerLogin`; there is no level-up hook).
  - 2026-10-14, the campaign: stage `mc`, contract `parity-artifacts/e2e/natural-morheim-mc-contract.json`, from
    `morheim-md-s1`: Q2333 (the Big Bucket filled at the lake, the creepy ribbit escorted to it), Q24022 (Tofa, Aprily,
    Jorund, an abex's item, the Dead Fire and the white brow tayga it raises, Landver, Protector Karhel) and Q24021
    (Bragi, Tofa and movie 73, the spectres' items and Deputy Lamipedon's, Tofynir, the item used in its zone), ending
    at the garrison's obelisk. Round `m4-mc`, captured `morheim-mc-s1-w1`: **every bot is level 31**. All eight did
    Q2333. None could kill Protector Karhel (level 35, EXPERT) alone, so every bot left Q24022 at var 6 for the group
    step. The Ranger and the Templar did Q24021; five left it for the group, Lamipedon being one spawn the others
    kept dead, and the Gladiator stopped at him (a lone source not waited for again; fixed after, `383b17d33`). Each
    took 3.0 to 4.3 game hours, 2 to 8 deaths.
  - New for the leg engine: a leg can have several item uses (one that makes an item, Q2333's bucket; one that ends
    the quest, Q24021's); an item an item use turns into another counts as collected; a spawn trigger whose item
    drops at its var waits for the drop (Q24022's Dead Fire); an escort can start with a talk that spawns its follower
    in front of the player (the protocol talks to the step's NPC and follows the nearest follower), and the goal stand
    is half the goal radius when that is nearer (Q2333's sphere is 10 m); a scripted drop list or hunt a bot cannot
    finish alone in the journey's hour is left for the group step, as a template quest is.
  - Left for later: Q24023 gathers inside the Sky Temple Interior (320050000, a solo instance) and waits for the leg
    engine to collect inside an instance; its reward is Gangleri's at Slag Bulwark.
- [x] **M5 Slag Bulwark and Alsig Village,** 13 quests, and the four without a giver where they fall.
  - 2026-10-14, Slag Bulwark: stage `ms`, contract `parity-artifacts/e2e/natural-morheim-ms-contract.json`, from
    `morheim-g31-s1`: Q2356 (held since Kellan's Cabin) to Utgar, Q2357, Q2358, Q2359, Q2360, Q4320, Q2361 (to Magni
    by Halabana Outpost), Q2367 by its steps (the first ending, decision 6) and Q2370 in the temple's crater. Round
    `m5-ms`, captured `morheim-ms-s1-w1`: all eight did all nine, 1.1 to 2.2 game hours, 4 deaths (the Sorcerer 3, the
    Assassin 1); seven are level 32 and the Sorcerer 31.
  - Sarinerk, the crater floor of Q2370, Padinerk and the Fire Temple's gate lie in Morheim's second FLY zone
    (`FLYINGZONESHAPESUBZONE_3`, around the crater, height 20 to 220), on ground the navmesh does not join to the
    rest: the operator, back on 2026-10-14, "around Fire temple is a FLY ZONE". The first trial walked to Sarinerk and
    stopped. A Morheim leg now reaches such ground as a player does: it walks to the zone's ground nearest the goal,
    flies and lands beside it, and out the same way; Sarinerk stands past the edge of his ledge's mesh, and the bot
    lands on his own static ground. The Cleric flew 128 m from the rim to Sarinerk, 286 m down to the crater floor and
    back up for the reward.
  - 2026-10-14, Alsig Village: stage `ma`, contract `parity-artifacts/e2e/natural-morheim-ma-contract.json`, from
    `morheim-ms-s1`: Q2476, Q2381, Q2382, Q2383 (to Sarinerk in the crater, by flight), Q2477 (decision 8: the five
    ingredients from Beluslan's meek crestliches, Lainita in Pandaemonium, Balom's reward), Q23830 (at REWARD since
    level 30, to Herner in the fortress) and Q2319 (held since Kellan's Cabin; Luimerk stands in Altgard, 900 m east of
    its fortress, not in Alsig Village as M3 said). Round `m5-ma`, captured **`morheim-ma-s1-w1`**: all eight did all
    seven, 1.2 to 2.4 game hours, no deaths, all level 32.
  - New for the leg engine: a scripted drop list can name the map its sources live on, and the leg goes there by its
    map trips (Q2477's, in Beluslan); a step far across another map walks the travel planner's road, defending on the
    way (the explorer alone replanned round the monsters toward Luimerk until its budget ran out, and Altgard's
    transporter toward him lands on Impetusium, 70 m above him with no way down).
  - The operator asked, the same day, whether Impetusium was a fly zone. It is: Altgard's `DF1A_FZ_TOWN3_220030000`
    (x 2401 to 2817, y 1455 to 1873, height 200 to 440) holds both the transporter's landing and Luimerk, so a bot
    could take the transporter and fly the last 70 m down. The crossing knows only the hub map's FLY zones (Morheim's);
    loading those of whatever map the bot stands on is left for later, since the road from the fortress works.
  - Not played, for the report: Q2372 Immortal Love (Q2367's second ending; its sulfur worms are in the Abyss),
    Q2324 The Spirit's Notebook and Q2325 (nothing in the server's data gives Hermod's Notebook), Q24023 (gathering
    inside the Sky Temple Interior, a solo instance).
- [ ] **M6 The group quests,** 11, in groups, with Q2494 and Q2328 (one named spawn each, whose items drop for each
  member of a group; see M3), Q2344 and Q2343 (the Ice Claw village's elites; see M4), Q24022 (Protector Karhel) and
  any quest a bot left for the group (Q24021 for five of them).
- [x] **M7 Level 31 and the gear check.** Every quest of the leg done by all eight; level 31; Elite Rank 6 where it
  beats what is worn; the sale of what it replaced, the kit and the cleanup; a relog. The capture is the next group
  world.
  - 2026-10-14, played before M5 and M6 (decision 7), from `morheim-mc-s1-w1`, where every bot is 31. Leg `mg` takes each
    bot back to the fortress and binds it there (round `m7-mg`, captured `morheim-mg-s1-w1`). Stage `check-in` is the
    muster's coin step at 31 (rule MR-i): each class's Elite Rank 6 pieces from the fortress's Silver Coin vendors
    (Alfhild and Gyrda), by the coin manifests the Abyss entry's tiers use, the coins the manifest is short supplied.
    Round `m7-g31`, captured **`morheim-g31-s1-w1`**: every bot bought what beat what it wore, and the quests of levels
    27 to 31 had already given it better pieces for the rest. The Cleric and the Chanter took the hauberk, handguards
    and staff (54 coins); the Sorcerer and the Spirit Master the tunic, gloves, shoes and spellbook (63); the Assassin
    the vambrace and two daggers (63); the Ranger the vambrace and bow (36); the Templar the sword and shield (45);
    the Gladiator the greatsword (27). The pieces they replaced were thrown away (coin gear does not sell), the kit
    was filled, no deaths, a relog. M8, M5 and M6 start from this world; the bots pass 31 in M5 and M6, and their next
    check-in is at 36.
- [x] **M8 Level-31 companions.** The profiles of the eight from that world, beside the level-26 ones. The server
  offers both: a companion comes at the highest level it has a profile for that is not above its player's, and
  `.companion <class> <level>` asks for one outright. A round with a level-31 stand-in and level-31 companions.
  The operator, 2026-10-14: "the cleric probably has more and better heals (a group heal?), make sure the heal
  rotation/profile is updated for a level 31 cleric for these Fire Temple/companion runs." So the Cleric's heal
  rotation (`NaturalGroupHealDriver`, its roles in the class profile) is brought up to what a level-31 Cleric has
  learned, a group heal among it, used when several members are hurt at once; the companion Cleric and the Fire
  Temple Cleric are the same driver.
  - 2026-10-14, before M5 (decision 7): the level-31 profiles of the eight, from `morheim-g31-s1-w1`, beside the
    level-26 ones; `scripts/sim/make-companion-profiles.py` has two sources now, the group world for every class at 26
    and the check-in world for the original eight at 31. The server keeps every profile it finds. A companion comes at
    the highest level its class has a profile for that is not above its player's, at the lowest when every one is, and
    `.companion <class> <level>` asks for a level outright (`.companion cleric 31`).
  - Every class now casts its highest rank of a skill: a rank of levels 27 to 31 takes the role of the rank before it
    (the skill tree's `skillLearn`, in `NaturalSkillCatalog.Build`); a rank of an excluded skill stays out. Until now
    the tables of roles ended at 26, and a character of 31 cast its rank of 26.
  - The Cleric's heals (`NaturalGroupHealDriver`): Healing Wind (learned at 28, every member within 25 m) is cast when
    two or more members in its reach are at 70% or less and none is at 35% or less, which a single heal answers first;
    Light of Recovery (31, a second's cast) comes before Healing Grace and Healing Light. The companion Cleric and the
    group Cleric are the same driver.
  - Round `m8-t1` (from `morheim-g31-s1`, not captured): the level-31 Templar plays the player's stand-in and brings a
    Cleric, a Gladiator, a Ranger, a Sorcerer and a Chanter, which is the Fire Temple group. All five come at level 31
    on their 31 profiles, join, follow, fight its marks and leave with nothing they did not bring. They cast their ranks
    of 28 to 30 (Cleave, Ice Chain, Thunderbolt Strike, Entangling Shot), and the Cleric kept every member under its
    Light of Rejuvenation of rank 30. Nobody fell below 70% in those fights, so Healing Wind waits for Fire Temple.
  - Checks: the unit suite, the pre-commit checks, the profile check, the companion server tests, Fast (`m8-fast`) and
    the neutral gate passed. The Group NTC replay did not, twice (`m8-n9`, `m8-n9b`): the members reach 27 in the camp,
    their new ranks change a fight by a little, and the Templar then retreated 80 m out of sight; its five followers
    stood with 48 samples of its way unwalked, since a follower waited to see a leader that stood still, until its wait
    for the group ran out. I committed the code (`088c9879e`) before I saw that the second replay had stopped as well.
    The follower now walks the trail it has recorded to a leader out of sight (`91c5774a2`), and the replay passes with
    it (`m8-n9c`, all six), as do the unit suite, the warning baseline and the neutral gate.
- [ ] **M9 The report:** per class its level, quests done, deaths and time; every quest that was not played and why;
  what the bots found wrong with the server.
- [ ] **M10 Fire Temple, as a group of six at 31.** The operator, 2026-10-14, before leaving for a while: once the list
  above is done, "Map out the quests for 'Fire Temple'", "Group up with a full group of 6, make sure to have a
  cleric/templar/sorcerer plus 3 more of your choice", "minimum of level 31 each (the companion profile)", "Do Fire
  Temple as a group. This is a more difficult instance, so it might take a few tries." His answers the same day:
  Fire Temple quests that Java has no handler for are skipped and reported, not added under D32; every Fire Temple
  quest is played once, repeatables included. Mine, his to change: the other three are the Chanter (a second
  healer), the Gladiator and the Ranger; it is played in SIM from the level-31 world, with the group stages of
  [natural-group-ntc.md](natural-group-ntc.md); a wipe is an outcome, and the group tries again. Its own plan
  (the quests, the entry, the bosses, the steps) is written when M9 is ticked.

## Decisions

The operator's, 2026-10-13:

1. Each of the eight plays alone; group quests are played in a group.
2. Spy quests are not played. Gathering quests wait for the gathering work.
3. The quests without a handler are in the list and left for D32, on a later word of his.
4. The leg ends on level 31 with the gear check, and the level-31 profiles become companions.
5. A round is all eight bots in the one world, each starting ten game minutes after the one before.

The operator's, 2026-10-14, back for a while after M4 ("every bot is level 31. That is the bookmark for our profile we
need"; "around Fire temple is a FLY ZONE"):

6. **Q2367 A Prized Possession: every bot takes the first ending.** Embla's two answers set the reward. The first
   opens Q2370 The Bottom of the Temple (Sarinerk: wiggling wriggots and blackscale basilisks on the crater floor);
   the second opens Q2372 Immortal Love (Paepaerunerk in Pandaemonium, poison arachnas in Altgard and sulfur worms
   in the Abyss). A character can have only one of the two. Q2372 goes into the report: the bots have no Abyss
   travel.
7. **The level-31 gear check and companion profiles come first,** from `morheim-mc-s1-w1`, where every bot is exactly
   31: step M7's check-in and step M8 are played before M5 and M6, which follow from the checked world. The bots
   level past 31 there.
8. **Q2477 A Dish For Dukar is played:** the leg engine learns to collect on another map (its ingredients drop only
   from Beluslan's meek crestliches, and Lainita's step is in Pandaemonium).

Mine, where he gave none; each is his to change:

- How a companion's level is chosen (step M8): the highest level its class has a profile for that is not above its
  player's, the lowest when every one is above it, or the level the player names.
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
- One world for every character; a round is the eight bots, each starting ten game minutes after the one before.
- Every quest of the leg, no level goal, nothing hunted for experience. No spy quests. A death is an outcome.
- Only the eight classes. Nothing new for the Rider, the Gunner or the Bard.
- No branch, worktree, subagent or push. No hand-edit of generated data. The warning baseline is not raised.

STOP when M10 is ticked, or when a step has failed three iterations running, or when a decision is needed that the
document does not give. On stopping, send one notification.
```
