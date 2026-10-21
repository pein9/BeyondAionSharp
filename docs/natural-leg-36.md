# Natural leg to 36: what was left, Brusthonin, the first Abyss quests and the check-in at 36

Status: resumed by the operator, 2026-10-20: NL-0 to NL-3 done; NL-4 in progress. The retained shared world is now
`nl-rw-s2-w1`, after ordinary Abyss transit from `nl-vs-s3-w1`. All eight have completed Q2355 and Q2369 in groups.
Cleric, Gladiator, Chanter, Templar, Sorcerer and Ranger have persistent Q24023 completions; Assassin and Spirit Master remain open. Remaining group
quests, higher-level quests and the NL-5 report remain open. Decisions 15 and 16 record
the earlier pause; decision 17 resumes the loop.
The plan was written on 2026-10-17. The operator, that day, after the level-31 profiles:

> 1. Finish anything we were in the middle of, unless we were waiting until we leveled up a bit (like lvl 36 with better
>    gear).
> 2. Go back and do the Brusthonin quests, and any connecting quests that involve Morheim.
> 3. Then do the initial Abyss quests. I know a lot will unlock once we get there, and do things. (level 25 - 33)
> 4. Then we should be level 36 and can do better gear, and go back and do quests we had issues with.
>
> New rule: when doing solo quests, let's give every bot a Cleric companion. They won't do any quests, just follow for
> heals. Obviously they will also stay out of any individual instances. I think this will speed up our questing/training
> when they are doing solo tasks.

It follows [natural-morheim-leveling.md](natural-morheim-leveling.md) and its rules, those of
[natural-muster.md](natural-muster.md), and starts from the bots' own world, `morheim-ft-s1-w1`: the Fire Temple six at
35, the Spirit Master and the Assassin at 34.

## The rule (NL-a): a Cleric companion on solo quests

Every bot that plays a quest alone has a Cleric companion with it ([companion-bots.md](companion-bots.md)): it asks for
one with `.companion cleric` when it sets out, the companion follows and heals and does nothing else (no quests, no
loot), and it is sent away when the bot's round ends. The server makes it from the Cleric's profile at the highest level
not above the bot's (31 for these bots). A companion cannot be asked for inside an instance, and it does not go into a
solo instance: it waits at the entrance until its bot comes out. A group round (stage `group-field`) brings none; its
own Cleric heals it.

What the rule brings with it, as the server has it: the companion is a member of its bot's group, so a kill's
experience and kinah are shared with it (quest rewards are not), and a round of eight bots needs eight companion seats.

## What is left, and where it goes

| What | State in the world | Step |
|---|---|---|
| Q24023 A Blazing Rescue (campaign, 29): the Sky Temple Interior, a solo instance; three key guardians, Flame Judge Bolcan 57,376 HP | all eight have it started | NL-4 (my decision 4) |
| Q24024 A Nepra Protector, Q24025 Crisis in Morheim (campaign, 35) | started for the six at 35; the other two get them at 35 | the next phase (the operator) |
| The second stigma stone and the Elite Rank 6 pieces the level-31 check-in skipped (L1 of [level-31-profiles.md](level-31-profiles.md)) | the world's eight still wear the old picks and one stone | NL-1 |
| Q2330 Orders for Randet, Q4300 News For the Settlers (Morheim, 20): handed in at Brusthonin | not taken | NL-2 |
| Brusthonin's settlers area (levels 20 to 24): 18 quests, two of them group quests | none done | NL-2 |
| Reshanta's quests from 25 to 33: about 49, the Abyss campaign Q24040 to Q24046 among them | none done | NL-3 |
| Morheim's quests from 31 to 35: 53 | none done | NL-4 |
| Q24026 A Hand from Each Side (campaign, 35): Nidalber, a solo instance with a timed defence | waits for Q24023 to Q24025 | the next phase, with Q24024 and Q24025 |
| Q2355 Seeking the Spotlight (group): past the klaw camp and Queen Taran | six have it started | NL-4 |

Not played, as the operator decided on 2026-10-16: Q2343, Q26905, Q2324 and Q2325, Q2372, and the quests that have no
handler. The nine "Defeat Nth Rank Elyos Soldiers" quests (Q2702 to Q2710) need kills of Elyos players, which a SIM world
has none of: they go into the report.

## Rules

1. The rules of the Morheim leg and the muster, unchanged: one world, rounds of all eight with staggered starts, every
   quest of a step, group quests in a group, no spy quests, no hunting for a level, a death is an outcome.
2. NL-a, the Cleric companion, on every solo quest of this leg.
3. The Java is the spec for every quest and mechanic; the bots find bugs and they are fixed the usual way.
4. Proof is the round: the checks of `CLAUDE.md`, an uncaptured trial before every captured round, the neutral gate and
   the Group NTC replay when shared bot code changes. No new unit tests for bot code.

## Steps

One step per iteration. A step is ticked when its round shows it, with a dated line under it.

- [x] **NL-0 The companion rule.** A solo stage that asks for a Cleric companion at its start and sends it away at its
  end; companion seats in a round; the bot that enters a solo instance leaves its companion at the entrance. Shown in
  one trial round of all eight on a short solo leg.
  - 2026-10-17 (`f1649efb3`): a round names the companion with `soloCompanion` on a solo leg's bots and gives the round
    companion seats (stage `companion`, accounts of their own, after the bots). At the leg's start the bot says
    `.companion cleric`, asks again while no seat is free, and goes on alone if it gets none (the rule is a help, not a
    gate); before the endpoint's relog it sends the companion away. A seat now plays one companion after another (it
    had stopped on its log check at its second). Trials `nl0-t1` (leg `mgd`) and `nl0-t2` (leg `mgk`, from Slag Bulwark
    to Kellan's Cabin) from `morheim-ft-s1`: every bot's Cleric joined (level 31, its profile), followed it for 6 to 15
    game minutes and was sent away at the end; one seat played six in turn. My decision: before a solo instance the bot
    sends its companion away and asks for it again outside (a companion cannot be asked for inside an instance at all);
    that is built with the first solo instance, in NL-1.
- [x] **NL-1 What was in the middle.** The world's eight get the check-in's fixed picks and their second stone (the stage
  `check-in`, which their level allows). Q24023 moved to NL-4 (my decision 4).
  - 2026-10-17 (`ed7ab9b15`): leg `mg` took the eight from `morheim-ft-s1` back to the fortress, bound there
    (`nl-mg-s1`; its round file named no companion, an oversight for a walk of a few minutes). The first check-in from it (`nl1-g31`) stopped the Ranger: the coin check
    bought the level-31 Elite Rank 6 jerkin over its level-35 Flame Tunic on its higher grade and defence, and the
    gear rules, which rank armor by item level first, never wore it. The coin check now ranks a piece by its level
    alone, as the gear rules wear it. Trial `nl1-g31t2` and captured round `nl1-g31c2` from `nl-mg-s1`: all eight
    reached, each with its second stone socketed (bought in Pandaemonium) and 0 to 3 Elite Rank 6 pieces bought where
    one beat a worn piece (most wear level-35 Fire Temple pieces). The world is now `nl-g31-s2-w1`.
- [x] **NL-2 Brusthonin.** Q2330 and Q4300 from the fortress, the fortress teleporter (204399) to Brusthonin, the settlers
  area's quests (the hand-written ones get their steps), its two group quests in a group, and any quest of the area that
  sends a bot back to Morheim. The quest list first, made by `scripts/sim/zone-quest-list.py`.
  - 2026-10-17: the list is [natural-brusthonin-quests.md](natural-brusthonin-quests.md): below 36, sixteen quests to
    play alone (ten from their compiled plans, six by steps written from their Java handlers: Q4001, Q4004, Q4011,
    Q4012, Q4015, Q4020) and two group quests (Q4014, Q4017); the rest of the zone is from level 43 up. Brusthonin's
    navmesh and travel graph are baked (`04044c6f0`). Two legs: `mb` takes Morheim's Q2330 and Q4300 at the fortress
    and hands them in at the Settlers Campsite, by the fortress teleporter's Brusthonin line, and ends there; `bs` binds
    at the campsite's obelisk and plays its sixteen quests from it. Then the two group quests in a group.
  - 2026-10-17 (`919505317`): leg `mb`, trials `nl2-mb-t1` and `t2` and captured round `nl2-mb-c1` from `nl-g31-s2`:
    all eight took both orders at the fortress and handed them in at the campsite (`nl-mb-s1`). The first trial
    Returned to the fortress before the leg could end beside Randet; a leg whose endpoint is on another map than its hub
    now ends there.
  - 2026-10-17: leg `bs`, trial `nl2-bs-t2` from `nl-mb-s1`, stopped by hand after four hours of game time: Q4001,
    Q4004 and the ten template quests played, no death; then every bot froze at Q4011's faded letter, whose talk is a
    use bar (Java `QuestStartItemNpcAI`: three seconds, then the dialog), and the Chanter walked in 347 times on an
    unfest vigilante it never got nearer than 32.7 m to. A talk that opens with a use bar now runs the clock through
    it, and twenty walk-ins with not a metre gained leave that monster. A capture no longer records the companion
    seats (their throwaway Cleric made `nl-mb-s1`'s record list two of the line). Diagnostic `nl2-bs-d1` (the Cleric
    and the Templar, the scripted quests only: a round may now limit a bot to some quests): Q4011 with its trip to
    Kaindal in Ishalgen by way of Pandaemonium, Q4015, Q4016, Q4020 with Blume's reward talk, Q4010 and Q4012, all done.
  - 2026-10-17 (`336b2f8db`): captured round `nl2-bs-c1` from `nl-mb-s1` (`nl-bs-s1`): seven of the eight played all
    sixteen quests and ended at the campsite's obelisk, in 5 to 8 game hours, with no death; the walk-in bound was not
    needed. The Assassin stopped on Q4016 with three quests left: it stood on one spot 20 m from the guardian outriders
    for four game hours, with no checked way to any of them, until its six failed pulls were spent. A hunt whose failed
    pulls end where the bot stood now takes the way out once (a flight to the hub where it may fly, else Return), as an
    approach with no route does, and hunts on.
  - 2026-10-17 (`7056e4d54`): the Assassin's catch-up, trial `nl2-bsa-t1` and captured round `nl2-bsa-c1` from
    `nl-bs-s1`: its last three quests in 25 game minutes (`nl-bs-s2`). Every one of the eight has the sixteen.
  - 2026-10-17 (`1f7e6406c`): the two group quests, Q4014 and Q4017, by stage `group-field` bound at the campsite's
    obelisk, as M6 played Morheim's: six led by the Templar (round `gb-six.json`), then the Spirit Master and the
    Assassin with four of them (`gb-second.json`). Trial `nl2-gb-t1` was refused at login (the group stages may now
    take a mustered character up on Brusthonin), and `t2` waited fifteen minutes for Q4017's hand-ins: Vinduer walks
    round the campsite, every member's walk to his last place found no way, and each gave up without a word. A member
    now looks for its quest NPC up to 40 m off and meets a walking one where it stops, and the leader tries its own
    hand-in again. Trial `t3` and captured round `nl2-gb-c1` from `nl-bs-s2`: all six (`nl-gb-s1`); the second group's
    trial `nl2-gb2-t1` and captured round `nl2-gb2-c1`: the Spirit Master and the Assassin (`nl-gb-s2`).
  - The step is done: every one of the eight has played the eighteen quests of Brusthonin below 36 and Morheim's two
    orders for it, and none of the settlers' quests sends a bot back to Morheim. The quests' experience is small at
    these levels: the eight stand at 35, the Spirit Master and the Assassin at 34. The world is `nl-gb-s2-w1`.
- [x] **NL-3 The first Abyss quests (25 to 33).** The way into Reshanta, its flying (islands, death below 500 m), the Abyss
  campaign from Votan's Orders, the quests the list says play or need steps, and those that open on the way. The quest
  list first.
  - 2026-10-17 (`251cda903`): the list is [natural-reshanta-quests.md](natural-reshanta-quests.md): below 36, 26
    quests from compiled plans and 23 with hand-written handlers (the ten "Defeat Elyos Soldiers" among them, which
    need Elyos players and go into the report), one group quest (Q2728), four that ask for gathering, eight that cannot
    be taken; most are given at Primum Fortress, and by their rewards alone they carry the least experienced bot to 36.
    Reshanta's navmesh is baked; its travel graph cannot be made yet (the tool stops in `SiegeService`'s static start).
    The way in is the Morheim Abyss Gate (portal 730063, high in the Morheim fortress; Q2042), which lands at Primum.
  - 2026-10-17: the survey (kept in `run/nl/NL3/reshanta-research.md`) splits the step into legs, every one bound at
    Primum's obelisk (730072), in this order (my decision 7): `rw` the way in (the campsite's teleporter to Morheim, a
    flight up to the gate's platform 102 m above the fortress floor, the gate); `rp` Primum (Q24040, Q2721, Q2722,
    Q24041 on the training ground 44 m below the fortress); `re` the east (Q2724, Q2741, Q2743, Q2744); `rs` the south
    (Mokkurkalfi, Sinjah's tower, the Sulfur Tree Nest, Triminos: Q2725, Q2729 to Q2731, Q2753 to Q2756, Q1755, Q24042
    with its escort, Q24043 with its lure); `ru` the upper layer (Q2726, Q2764 to Q2766, Q1771, Q2772, Q2773); `ro` the
    trips out of the Abyss (Q24043's and Q24044's Pandaemonium and Ishalgen steps, Q24046, Q2757 and Q2758); and Q2728
    in a group. The engine work they need, smallest first: a map trip by a portal object, the FLY zones of the map the
    bot stands on (Reshanta is FLY everywhere, from its death level at 500 up), flights from island to island within
    the bots' flight time, and the few mechanics named with their quests.
  - 2026-10-17: leg `rw`. The first trials could not reach the Morheim Abyss Gate: it hangs 102 m above the fortress
    floor under a canopy over a small platform, the bots tried to land on the platform, and no checked flight landed
    there (and a stranded Return threw a bot back to its bind in Brusthonin). The operator, the same day: "You fly up to
    the ring in the middle of the air, and 'use' the door while flying. It takes several seconds to use the door, then you
    get teleported." Java agrees (talk range 16 m, no flight check, a 3 s bar, the teleport ends the flight). A portal
    that hangs in the air is now flown to, to a point beside it within its reach, and used in flight; a map trip may be
    a portal object; the FLY zones are the map's the bot stands on, and a map whose world flags hold FLY (Reshanta) is
    one from its death level up; a stranded Return is cast only when the bind is on the bot's map. Diagnostic
    `nl3-rw-d3`: the Cleric flew from the teleporter's landing to the ring, used it in the air and stood at Primum.
    Committed as `14e801ba9` (with a sight diagnostic in NavBake, `ad81fbbfa`); trial `nl3-rw-t5` and captured round
    `nl3-rw-c2` from `nl-gb-s2`: all eight in Primum, Q24040 started on arrival (`nl-rw-s1`).
  - 2026-10-17: leg `rp` (Q24040, Q24041, Q2721, Q2722; Q24045 is left out with its later steps, decision 8). Trial
    `nl3-rp-t1` stood every bot at Votan on its way to Leban, whose training ground is an island of its own: no route
    reached it, the stranded Return waited out a 19-minute cooldown, and the flight was never tried (the FLY flag of
    Reshanta's world map was read as a word the flags' text does not hold, so the map had no FLY zone at all). The map's
    flags are now read as flags, and a flight is tried before a stranded Return. Diagnostic `nl3-rp-d2`: the Cleric flew
    down to the trainers and back twice and played all four quests. Committed as `bff118c8e`; trial `nl3-rp-t2` and
    captured round `nl3-rp-c1` from `nl-rw-s1`: all eight played the four quests and stand bound at Primum (`nl-rp-s1`).
  - 2026-10-17: leg `re` (Q2741, Q2743, Q2744 on Gjalf and Ganglot's island; Q2724 left out, decision 9). The island is
    1,108 m from Primum and a full tank flies about 698 m, so a crossing now hops: from island ground to island ground
    nearer the landing, waiting for the flight time between. The diagnostics `nl3-re-d1` to `d5` showed three faults
    in the choice of a stone. Under Primum's roofs no stone was found, so a far crossing now takes off where there is
    open sky. From Ganglot's island nothing cleared 15 m above the higher end, so a 60 m cruise is tried next. A stone
    587 m across was 790 m flown, so the climb and descent now count. `d5` then played Q2724's talks to Mure and
    Ganglot and flew to Amundi's spot, where nobody stands. Trial `nl3-re-t1`: seven of eight. The Gladiator's flight
    back to the hub after a stranded fight was a straight 1,544 m (127 FP of 72), and it now hops. Committed as
    `540aec462` (with the `rs` contract, quests played as far as they go, and report_to_many's reports); trial
    `nl3-re-t2` and captured round `nl3-re-c1` from `nl-rp-s1`: all eight played the three quests, two deaths, three to
    seven hops each (`nl-re-s1`).
  - 2026-10-17: leg `rs` (the south: Q2729, Q2730, Q2731, Q2725, Q2753, Q2754, Q2756, Q2755 and Q1755, from Mokkurkalfi's
    ledge to Triminos's rock; Q24042 played to Glati and left at the prisoner, decision 9; Q24043 to Sinjah and left at
    the lure). Diagnostics `nl3-rs-d1` to `d3` and trials `t1` and `t2` found these faults:
    - Hop stones under islands: a ledge 100 m under Primum, and one 170 m under island 7. A stone now has open sky
      above it.
    - The Cleric was killed by ashikar assaulters while it waited out its flight time on a stone beside Sinjah's
      tower, and the Ranger died the same way on Mokkurkalfi's ledge. Every wait for flight time now answers an attack,
      and stones keep 30 m from aggressive spawns when they can.
    - The bag cleanup at a restock threw out Q2731's documents, its work item. An item a worked quest's data names now
      always stays.
    - Q2725 is a report_to_many quest, and its report before the end NPC's is now played.
    - A revive's Soul Sickness, and once another slow, cut the flight speed below the hop's plan (592 m costing 77 FP
      of 72). A refused hop now lands and plans again at the speed it took off at, and Soul Sickness is waited out
      before a crossing past one flight.
    - The Sorcerer left Q2753 for the group after an hour and ten deaths, as the engine allows (M4). The endpoint now
      also leaves out what follows such a quest (Q2754).

    Committed as `540aec462` (the contract) and `ece9d5ff4`; captured round `nl3-rs-c1` from `nl-re-s1`: all eight
    reached the end, levels 34 to 36, 32 to 63 hops and 0 to 10 deaths each; the Ranger left Q2754 for the group
    (`nl-rs-s1`).
  - 2026-10-17: leg `ru` (the upper layer, 1,200 m above the lower: Q2726, Q2762, Q2763, Q2764, Q2765, Q2766, Q1771 and
    Q2772). Diagnostics `nl3-ru-d1` to `d3` and trials `t1` and `t2` found these faults:
    - No flight climbs 1,200 m, and the airline planner priced the climb as a walk. A crossing to a goal more than
      600 m above or below the bot now takes the flight transporter between the layers, Mure up to Hwiath and
      Hwiath down.
    - Once the transporter was of the Elyos (Theon), who do not talk to an Asmodian. The transporter is now one of
      the bot's own race.
    - Junjunbinerk stands where no navmesh lies within 20 m, so the walk after the flight found no road again, and the
      walk and the crossing called each other without end. A walk now ends where its flight lands within reach.
    - Kuenunerk's jewel boxes (Q2772) stand on scraps of ground 190 m above one another. A bot below a box waited for
      guards to clear instead of flying. A spawn on another island is now flown to.
    - The Templar and the Chanter were killed in the air on a hop, and their landing waited for ever. The death now
      ends the wait.

    The `t2` world had the D7 boot tail in it (the siege garrisons stand at every fortress). Committed as `0c7d8d74a`;
    captured round `nl3-ru-c1` from `nl-rs-s1`: all eight reached the end, levels 35 and 36, 0 to 3 deaths and 5 to 11
    lifts each. The Sorcerer, the Chanter, the Assassin and the Ranger left Q2762 for the group after an hour without
    progress: most of its 42 mirrorglass waterworms float where no navmesh lies, as the survey guessed. Q2763 follows
    it for those four (`nl-ru-s1`).
  - 2026-10-18: leg `rt` (Tigraki Island: Q2767 and Q24045, decision 8's two quests, on the operator's word of the day
    before). The island stands 705 m from any other island, so the bots fly there on Greater Wind Serums. A landing past
    every stone is flown to, or else the stone nearest it, within what two serums add to one flight. The serums and a
    flight speed scroll are the leg's approved supply: the scroll is read on the ground, and a serum is drunk on the move
    once its whole restore fits. In this leg the scroll's speed alone carried every crossing, so no serum was drunk.
    Q24045's talks with Gaochinerk and Garkbinerk start scripted quest flights (flypaths 55 and 56, Black Cloud Island
    and back); a leg step now flies such a path and lands. Diagnostic `nl3-rt-d1` played both quests in one try, and trial
    `nl3-rt-t1` played them with all eight. Committed as `f4789b254`; captured round `nl3-rt-c1` from `nl-ru-s1`: all eight
    reached the end, levels 35 and 36 (`nl-rt-s1`).
  - 2026-10-18: leg `rq` (what the sieges opened: Q24042 to its end, Q2724 with Amundi; Q24043 waits at its lure,
    decision 10). With the D7 boot tail back, the siege garrisons stand, and a step's NPC that the travel graph does not
    know is found by the leg's own step positions. Q24042's prisoner is followed to coordinates (the escort's goal has no
    NPC); its hand-in at Lakadi once took no notice of the select that ends it, and a reward talk is now played once more
    when the quest does not move. Six of eight stopped at Q24043's lure in trial `nl3-rq-t1`. The diagnostics `d10` to
    `d15` and what they found are decision 10. Committed as `f4789b254` and `2ded7c925`; captured round `nl3-rq-c1` from
    `nl-rt-s1`: all eight reached the end, levels 35 and 36 (`nl-rq-s1`).
  - 2026-10-18: leg `ro` (the errands out of the Abyss: Q24044 by Munin in Ishalgen, Kasir and Lyeanenerk in Pandaemonium,
    Lugbug and the Artifact of the Inception on Leibo Island; Q24043's errand to Phosphor joins it once its lure is
    played). The survey's `ro` is split by where its quests end (my decision 11): `ro` here, `rb` the quests that end in
    Beluslan (Q2773, Q2757, Q2758), and `rd` Q24046 with the Shadow Court Dungeon. The bots leave by Primum's teleport
    device to Morheim: its use bar, then the fare question Java's `PortalRequestAI` asks. They change to Morheim's and
    Pandaemonium's teleporters as the shortest chain of the leg's trips, and come back by Return. The Cleric diagnostics
    `nl3-ro-d1` to `d14` found these faults:
    - Primum's devices stand on a terrace the navmesh splits off, and one was taken for a ring in the air. A portal is
      flown to only when no ground lies at its feet.
    - The walks to the device and to the Artifact found no approach point to their 3 m, for 43 minutes and an hour.
      They stand on bases no navmesh covers. A portal or quest object is now used from its talk range.
    - Kasir stands behind the library's desk, 6.3 m from where a player can stand. Java's talk range is measured
      between the bodies' edges (talk distance plus 1 m, the NPC's and the player's bound radii added: 6.6 m).
    - The bot now walks to its island's ground nearest such a thing and steps straight on over the server's ground until
      it is within reach.
    - The Temporal Stone comes by the leg's approved supply before the Artifact's use, on the operator's word.

    Diagnostic `d14` played Q24044 to its end. Committed as `2ded7c925`; captured round `nl3-ro-c1` from `nl-rq-s1`: all
    eight reached the end, levels 35 to 37 (`nl-ro-s1`).
  - 2026-10-18: leg `rb` (Q2773, from Kuenunerk on the upper layer by Choirinererk in Morheim to Horu in Beluslan
    Fortress). Q2757 and Q2758 were in the leg until diagnostic `d7` and wait for NL-4 (decision 12).
    The diagnostics `nl3-rb-d1` to `d7` and the rounds found these faults:
    - Choirinererk is a temporary spawn of Morheim's nights (21:00 to 05:00 game time). A talk step can now name its
      NPC's hours, and the bot waits at his spot for them.
    - The wait outlasted the hour of the watchdog for four bots in the first captured round, and it is now excused as
      it goes. An NPC not yet at his spot when the bot arrives is waited for there.
    - Horu stands on a platform of the fortress that only a flight joins to the ground. A fly-zone crossing now takes
      off from the bot's own island first. When its takeoff lies on another island, the bot flies down to that
      island's ground nearest it.
    - The packs round Choirinererk cornered bots off their road. The road to a talk is now rested and walked again to
      within 25 m, and a route shut by monsters is fought through along the road.
    - A round can go on from a world where bots stopped on one of the leg's trip maps.

    Built as `9d241294c`, `994d5680c` and `0e8cc4afd` (with the Beluslan navmesh, `0c49200c9`). Captured in three rounds
    of one world:
    - `nl3-rb-c1` from `nl-ro-s1`: the Cleric, the Templar and the Sorcerer (`nl-rb-s1`);
    - `nl3-rb-c2`: the Chanter, the Gladiator and the Assassin (`nl-rb-s2`);
    - `nl3-rb-c3`: the Spirit Master and the Ranger (`nl-rb-s3`).
  - 2026-10-18, the step is done. Every leg of decision 7 is played and captured in one world, `nl-rb-s3`:
    - legs: the way in, Primum, the east, the south, the upper layer, Tigraki, the siege quests, the errands out of the
      Abyss and Q2773 to Beluslan;
    - levels: the Cleric, the Chanter and the Gladiator at 37; the Templar, the Sorcerer and the Ranger at 36; the
      Spirit Master and the Assassin at 35.

    What it leaves, and where:
    - **Group, NL-4's group pass (decision 13):** Q2728 (Ladacus), Q2753 and Q2754 for the Sorcerer and the Ranger,
      Q2762 and Q2763 for four bots.
    - **A higher level, NL-4 (decision 12):** Q2757, Q2758 and Q24046.
    - **The operator's answer on the lure (decision 10):** Q24043 at Sinjah's lure.
    - **Not played (decision 8):** the Elyos player kills, Q2701, Q2727 and the Roah quests while Roah is the Balaur's,
      Q2846, and the four gathering quests.
- [ ] **NL-4 Level 36.** Morheim's quests from 31 to 35 for any bot still short of 36 and for the zone's sake; the gear
  check-in at 36 (Elite Rank 5); the level-36 companion profiles; then Q24023 (the leg engine collects inside an
  instance, room by room, and goes down between the Sky Temple Interior's floors as a player does, by a glide or a
  drop), Q2355 and the quests a step left for a higher level.
  - 2026-10-18 (`d4faa31ee`): the list is [natural-morheim-quests-36.md](natural-morheim-quests-36.md). Morheim's quests
    from 31 to 35, by hub:
    - Alsig Village 16, the Morheim Observatory 7, Halabana Outpost 2, the Ice Fortress 2 and Slag Bulwark 2 (group).
    - Q24024 to Q24026 among them wait for the next phase.
    - The legs go hub by hub, each bound at its hub's obelisk: `va` Alsig Village, `vo` the Observatory, then Halabana
      and the fortress.
  - 2026-10-18: leg `va` (Alsig Village's fifteen quests from 31 to 35; Q2369 is a group quest and waits for the group
    pass).
    - The plans are compiled into `natural-morheim-va-plans`, and Q2392 is played by steps and a feather from the
      virago looters.
    - The bots come from Primum. While a bot is bound off its hub's map, the leg's travel to the hub takes its trips
      (Primum's teleport device to Morheim) instead of Return, then the fortress's flight to Alsig.
    - Diagnostic `nl4-va-d1` played all fifteen with the Cleric. Committed as `0e8cc4afd`; captured round `nl4-va-c1`
      from `nl-rb-s3`: all eight reached the end. Every bot is now at 36 or more: the Spirit Master and the Assassin at
      36, the others at 37 (`nl-va-s1`).
  - 2026-10-18: legs `vo` and `vh`, committed as `3ffa7aa05`, each played by the Cleric in one try (diagnostics
    `nl4-vo-d1` and `nl4-vh-d1`):
    - `vo` is the Observatory's five template quests and Q2411 by steps (Tyr's choice, Angreboda's reward); two of them
      end in Alsig Village. Captured round `nl4-vo-c1` from `nl-va-s1`: all eight (`nl-vo-s1`).
    - `vh` is Halabana's two and the fortress's Q2401, offered by Neior and handed in to Modgud at Halabana. Captured
      round `nl4-vh-c1` from `nl-vo-s1`: all eight (`nl-vh-s1`).

    Morheim's quests from 31 to 35 are now played by all eight, but for Q24024 to Q24026 (the next phase) and the three
    group quests (Q2369, Q2371, Q2378), which go to the group pass.
  - 2026-10-18, the gear check-in at 36:
    - Leg `mg` took all eight back to the fortress and bound them there (`nl4-mg-c1` from `nl-vh-s1`, captured
      `nl-mg-s2`: the name `nl-mg-s1` was an earlier leg's).
    - Stage `check-in-36` (`3ffa7aa05`, its receipt `f6bc245fb`) is the muster's coin step at 36, from the fortress's
      Silver Coin vendors. Round `nl4-g36-c2`, captured **`nl-g36-s2`**: every class bought the whole Elite Rank 5 set,
      five armor pieces and its weapon. The Templar also took the shield and the Assassin a second dagger; nothing was
      left at a tie.
    - The levels there: the Cleric, the Chanter and the Gladiator at 38, the others at 37.
  - 2026-10-18, the companion profiles (`0594f2a33`): make-companion-profiles.py now takes the original eight from
    `nl-g36-s2` too, and `--check` passes for all 27 profiles. The companion server tests pass. They are at 37 and 38
    (decision 14).
    - Round `nl4-c38-t1` from `nl-g36-s2`: the Gladiator at 38 as the stand-in player called five companions (Cleric,
      Templar, Ranger, Sorcerer, Chanter). They came at 37 and 38, followed him, killed his marked targets in fifteen
      fights and left when he looted.
  - 2026-10-18, Q24023 in the Sky Temple Interior (leg `vs`, `2ef97d327`): its three rooms are one solo instance, each
    room entered by its own portal; the bot glides down between floors no walk joins (`NaturalFloorGlide`).
    - The Cleric played it whole in trial `nl4-vs-d6`: 2 h 45 min of game time and three deaths. It killed Flame Judge
      Bolcan alone, looted the Abyss Key, and finished Bulagan's check and Gangleri's reward.
    - Captured round `nl4-vs-c1` from `nl-g36-s2`, capturing `nl-vs-s1`: only the Cleric reached the end, with no
      deaths. Of the other seven:
      - The Templar, the Ranger, the Sorcerer and the Assassin stopped at a room portal: a hit cut off the 3-second
        use (Java `ItemUseObserver`), and the bot waited for a teleport that never came. The fix is committed as
        `b4201bfcf`: the use watches for the abort, and the bot fights and tries again. Its checks are chain 39.
      - The Fire Room's ordinary monsters are elites of 21,557 to 32,358 HP, standing in pairs; the other classes died
        there again and again.
        - The Gladiator stopped at 20 deaths on its top tier. Twice it was out longer than the copy's 600 s, and its
          kills came back with a new copy.
        - The Chanter: 16 entries and 6 deaths, an hour without a kill.
        - The Spirit Master: 7 deaths, then a takeoff near Slag Bulwark that the server refused
          (STR_FLYING_FORBIDDEN_HERE).
        - The Sorcerer had 9 deaths and the Assassin 2 before they stopped at the portal.
    - **Paused on the operator's word (2026-10-18)** until he settles the Fire Room. Open: how players go down it in
      the 4.8 client (the quest text speaks of three floating disks), and what the classes that cannot clear it alone
      should do.
    - 2026-10-18, picking up the handoff: the operator will manually record the Fire Room path. No further descent
      trials while waiting for that recording. The choice for classes that cannot clear it alone remains open; the
      recorded path must be assessed before drawing a new conclusion about their soloability.
    - 2026-10-18, the operator added Abyss flight rings to navigation's requirements, then authorized implementing
      them while keeping this loop paused. Solo and field-group flights now share ring routing and repeated crossings
      to replenish FP. A separate, uncaptured SIM diagnostic shows the routing and failed-ring landing; it does not
      complete any NL quest or the Abyss group pass. See the decision and evidence below.
  - 2026-10-21, first retained Morheim group wave (`a0f45af9e`): `nl4-group-morheim-c1` from `nl-vs-s2`, captured
    **`nl-gm-s1-w1`**. Templar, Cleric, Sorcerer, Chanter, Gladiator and Ranger completed Q2355 and Q2369 together,
    returned to Slag Bulwark and relogged. All six receipts are verified, both quests are COMPLETE in the saved SQL,
    and all six native traces have zero deaths. Each played about 2 h 15 min; levels remain 37–38. Snapshot and
    outcome hashes verify, and each trace's runtime build matches the committed source. All eleven original characters are
    preserved; the unplayed Spirit Master, Assassin, Bard, Rider and Gunner retain their experience, position,
    quests, skills and inventory. Cleric and Gladiator retain Q24023. The run's owner dropped its disposable schema.
    Evidence: `run/nl/NL4/nl4-group-morheim-c1/` and
    `run/nl/resume-20261020/nl-gm-s1-w1-audit.json`. The second wave starts from this retained world, with Spirit
    Master and Assassin plus four helpers who have already completed these quests. Its uncaptured trial,
    `nl4-group-morheim-second-t1`, verified all six after relogging, with no deaths or world errors. Spirit Master
    now has 334 completed quests in the trial and Assassin 332, both still level 37; each member played about
    1 h 46 min. The four completed helpers accompanied both quests normally. The trial's owner dropped its schema;
    captured round `nl4-group-morheim-second-c1` then retained the same results in **`nl-gm-s2-w1`**, from `nl-gm-s1`.
    All six receipts verify, both quests are COMPLETE in saved SQL, and the runtime build and snapshot hashes verify.
    The unplayed Sorcerer, Ranger and frozen three retain their experience, location, quests, skills and inventory;
    all eleven originals, the Cleric/Gladiator Q24023 completions and the other six characters' Temple keys are
    preserved. The capture owner dropped its schema. Evidence is under `run/nl/NL4/nl4-group-morheim-second-c1/`
    and `run/nl/resume-20261020/nl-gm-s2-w1-audit.json`. Both quests are now saved for all eight. NL-4 and NL-5 remain open.
  - 2026-10-21, Fire recovery capture (`5dc5056de`): `nl4-vs-recovery-c1` from `nl-gm-s2`, captured
    **`nl-vs-s3-w1`**. Sorcerer 37, Chanter 38, Templar 38 and Ranger 37 completed Q24023, verified before and after
    relogging and COMPLETE in the saved SQL. Their own game times were 85.94, 32.38, 26.62 and 46.64 minutes, with
    twelve, zero, zero and four native deaths. This is Ranger's first verified solo clear. Assassin and Spirit Master,
    both 37, reached their 90-minute limits with nine and thirteen deaths and remain START in saved SQL. All six
    actual runtime identities match the committed build. Snapshot and outcome hashes verify, all eleven originals
    are preserved, and the unplayed Cleric, Gladiator and frozen three retain their experience, position, quests,
    skills and inventory. Cleric and Gladiator's Q24023 completions persist. The owner dropped its schema and the
    marker is clear. Evidence is `run/nl/NL4/nl4-vs-recovery-c1/` and
    `run/nl/resume-20261020/nl-vs-s3-w1-audit.json`. The provisional report now covers 28 retained captures and 197
    original-character traces, all with observed entry and HP and no unclassified deaths; diagnostics and companions
    remain excluded. Only Assassin and Spirit Master lack a verified solo clear. Their long-term handling remains
    open; work on the independent remaining group quests proceeds without declaring a class soloability limit.
  - 2026-10-21, Abyss arrival capture (`484c734f5`): `nl4-rw-transit-c1` from `nl-vs-s3`, captured
    **`nl-rw-s2-w1`**. All eight reached Reshanta by the two ordinary Morheim hub flights and the floating Abyss
    Gate, verified before and after relogging. Own game times were 2.29–5.68 minutes; Assassin had one native death
    and recovered, the other seven had none. All companion seats ended as reached or unused. All eight actual
    runtime identities match the committed build; snapshot and outcome hashes verify. All eleven originals are
    preserved, the frozen three retain their experience, position, quests, skills and inventory, and every original
    quest completion persists. The six Q24023 completions and all eight Q2355/Q2369 completions remain saved;
    Assassin and Spirit Master's Q24023 remains START. This travel round claims no new quest completion. Its owner
    dropped the disposable schema and the marker is clear. Evidence: `run/nl/NL4/nl4-rw-transit-c1/` and
    `run/nl/resume-20261020/nl-rw-s2-w1-audit.json`. The refreshed provisional report covers 29 retained captures and
    205 original-character traces, all with observed entry and HP and no unclassified deaths; companions and
    uncaptured diagnostics remain excluded.
- [ ] **NL-5 The report.** Per class its level, quests, deaths and time; what was not played and why.

## Decisions

The operator's, 2026-10-17: **Q24024 and Q24025 wait for the next planning phase,** until the bots are a higher level
("we do not want to do Q24024 and Q24025 until the bots are a higher level"); Q24026, which follows them, waits with them.

The operator's, later the same day: **the bots are supplied a Temporal Stone when a quest needs one, and Greater Wind
Serums to use** ("Seed our bots with a temporal stone when needed and Greater Wind Serum for them to use"). This answers
decision 8's two questions without a change to the server's data: the Temporal Stone (188020000) comes by the bot's
supply when Q24044 reaches the step that asks for it, and Greater Wind Serums (162000026, skill 9911, one every 54 s, not
drunk on the move) carry a flight past one tank, to Tigraki Island for Q24045 and Q2767. Then: "We CAN drink the flight
potion while on the move, and should drink it when we get the max flight time out of it, also have our bots use a
greater whatever scroll for flight speed boosts": a serum is drunk in flight once the flight time it restores fits, and
the Greater Raging Wind Scroll (164000079) speeds long crossings. And: "We DO NEED the siege enabled", with housing and
the rest of the boot tail D7 declined, delegated to other agents while this leg goes on; decision 9's three quests are
played once the sieges stand.

The operator's, 2026-10-18, on picking up the handoff:

- **Q24023's Fire Room waits for a manually recorded path.** "I will have to record a path manually instead of you
  wasting time." Keep the NL loop paused and do not spend further trials guessing the descent. Resume the seven
  from `nl-vs-s1` once the recording resolves the route and the remaining class decision is settled. There is no new
  decision yet to improve their fights, declare the quest not soloable, or leave it for a higher level. The other
  questions in HANDOFF.md section 6 remain unanswered.
- **Use flight rings when planning Abyss travel.** The operator says each pass adds flight time and players can
  pass back and forth through a ring to stay airborne indefinitely. Routes must look for reachable rings and
  account for replenishment instead of assuming every long crossing needs a ground hop or serum. This applies to
  solo travel and the group pass of decision 13.
  - Source check on 2026-10-18: Java's `FlyRingObserver.moved` applies skill 265 (Wings of Aether) on every detected
    crossing in Reshanta (400010000), without a per-ring reuse check. C#'s `FlyRingObserver.Moved` mirrors it.
    Both skill templates specify `fphealinstant value="9"` and a six-second flight-speed effect; the shipped
    `fly_rings/fly_rings.xml` defines 55 rings on that map. A crossing is a movement segment through the ring's
    plane within its radius (`FlyRing.isCrossed`), not merely approaching its center.
  - The operator then authorized implementing this separately, running the existing checks and committing by path
    to main without pushing; **the NL loop stays paused.** `NaturalFlightRings` loads those definitions, derives
    approach and exit points on opposite sides of each plane and searches a chain within the flight-time budget.
    Each ring stop reserves enough FP for a checked ground landing if its refill fails. The route budgets at most
    9 m/s rather than relying on the temporary ring speed boost, and rechecks its remaining cost during flight.
  - Solo leg crossings try a ring chain before their ground-hop/serum fallback. Field-group members use the same
    planner with their own FP and takeoff speed; an unaffordable group flight now lands instead of flying anyway.
    Their flight-zone lookup also uses the shared map-aware policy, which recognizes Reshanta's world-wide FLY flag
    even though its zone file defines no FLY polygon.
    A ring stop recrosses until the observed bar is full, checks fresh `SM_FLY_TIME` updates before continuing and
    lands on checked ground if no refill arrives. Movement and refill loops are bounded and advance game time.
    No server behavior, generated data, companion-following flight code or existing baseline was changed.
  - **Focused SIM evidence:** `run/flight-rings-probe/probe4.log` and
    `run/capital-pass/flight-rings-probe-4-flightrings.trace.jsonl`. A scratch console reuses the existing SIM fixture
    and client session, with no new unit test. Setup teleports a disposable Cleric onto real Abyss ground, removes
    the map's NPCs to isolate flight from combat, and depletes FP for the refill case. After setup, flight and ring
    interactions use ordinary client movement; the server supplies every refill.
    - Ground (2741.85, 864.97504, 1475.4896) to (3153, 1432.5, 1396.3896), 705.3 m apart: a 60-FP character uses
      `TEMINON_LANDING_400010000_4` and `RESHANTA_400010000_1`, then lands exactly at the destination with 13 FP.
    - Beside the first ring with 12 FP: three back-and-forth passes give observed bars 29, 47 and 60, then a landing
      with 58 FP. The planner credits only the skill's 9-FP minimum per crossing; fresh packets confirm the refill.
    - The first ring removed from that disposable world: its expected crossing gives 48 to 47 FP, is rejected as
      no refill, and the bot lands on the planned nearby ground with 46 FP.
    - Earlier diagnostic attempts met Elyos guards as a level-10 Asmodian; those are setup failures, not evidence
      that routing passed. The final diagnostic isolates flight explicitly. No retained character was changed.
  - The real Abyss group pass, companion flight-following and the paused seven's Q24023 replay are not claimed by
    this diagnostic. The group pass still needs the other travel work of decision 13 when the NL loop resumes.
  - **Checks before the commit:** evidence in `run/flight-rings-probe/checks/` and `checks-final/`. The solution's
    unit suites, four CI checks (warning count 4,242, baseline unchanged), structural fidelity, plan compiler,
    snapshot contract, baked navmeshes, profile validation and the remaining script checks passed. Fast passed all
    eleven scenarios with no failure or flakiness. All five starter journeys show `Passed: 1`; each trace comparison
    still starts with D7's known object-id difference (133266 versus 135000), accepted under the standing interim
    criterion, without re-recording a baseline. Group NTC rounds `ntc-replay` and `ntc-final` both reached the end
    with all six; the latter uses the final map-aware group lookup. The NL loop remains paused and no snapshot was
    captured for this work.

Mine, where the operator gave none; each is his to change:

1. (Withdrawn by the operator: Q24024 and Q24025 in NL-1.)
2. **The world's eight get the second stone and the better level-31 pieces now (NL-1),** not at 36: the rotations of L3
   cast the stone's skill, and the companions already wear them.
3. **Morheim's quests from 31 to 35 are in NL-4,** after the Abyss: the operator's order has the Abyss bring the bots to
   36, and the zone's quests below 36 still belong to the leg (MR-j).
4. **Q24023 waits for level 36 and the Elite Rank 5 gear (NL-4),** as the operator's first point allows ("unless we were
   waiting until we leveled up a bit"). The Sky Temple Interior is a solo instance (the 4.8 client's
   `client_instance_cooltime.xml`, id 24: one member a side), so neither a companion nor a group can go in. Its Fire
   Room's monsters are the elite kind (client names `..._Ae`; 21,557 to 32,358 HP each where the Water and Wind Rooms'
   have 3,637 to 5,141), and Flame Judge Bolcan (57,376 HP) stands with three corrupt flame spirits of his tribe
   (32,358 HP each) within 13 m. The engine work it needs (three rooms of one instance entered by three portals, a
   collection inside an instance, glides or drops between floors that no path joins: falls cost 1 % of max HP a metre
   from 10 m, Java `FallDamageConfig`) is built then. The research is kept in `run/nl/NL1/q24023-research.md`.
5. **Baltasar Hill Village's two quests (Q4010, Q4012) are played from the Settlers Campsite (leg `bs`),** with no bind
   at Baltasar Hill's obelisk: Virhu and Vidohunir stand 600 m from it (the list puts them there as its nearest), on the
   way to Parica (Q4020), and the bots are ten levels above the ground.
6. **Q4011's second step is Kaindal in Ishalgen:** the bot takes the campsite's teleporter to Pandaemonium and
   Pandaemonium's to Ishalgen, as a player changes teleporters, and comes back by Return to the campsite's bind.
7. **NL-3's legs and their order** are the survey's: the way in, Primum, the east, the south, the upper layer, the trips
   out of the Abyss, then the group quest. The east comes before the south because it asks only for flights.
8. **What NL-3 does not play, into the report:** Q2702 to Q2710 (kills of Elyos players); Q2701 (nothing starts it);
   Q2727 from its third step and Q2774 to Q2776 (their NPCs spawn only while the Elyos hold Roah Fortress, and every
   fortress of the world is the Balaur's); Q2846 (its start item has no source in the data); Q24044 from var 5 (the
   Temporal Stone it asks for has no vendor or drop here; 4.8 retail sold it, by aioncodex: adding it is a retail
   correction, the operator's to approve); Q24045 from var 2 and Q2767 (Tigraki Island stands 705 m from any other
   island, past one flight; a Wind Serum drunk in the air is a player's way, and building it is the operator's to
   ask for); the four gathering quests. Each is played as far as it goes.
9. **No siege NPC stands in this world, so the quests that need one stop at it (NL-3):** the C# boot does not run
   `SiegeService.initSieges`, which places every fortress's, artifact's and outpost's NPCs by its holder; restoring it is
   P10-05 of `docs/e2e-player-simulation-plan.md`, deferred by the maintainer's D7, and not mine to reopen. Of Reshanta's
   quests below 36 it stops three: Q2724 at Amundi (Siel's Eastern Fortress, 1132), Q24042 at the captured prisoner it
   escorts (the Sulfur Tree Nest, 1141), and Q2727 at Dullaha (Roah, 1211; decision 8 names it). Q2724 is left out of the
   east leg, its talks to Mure and Ganglot shown once in `nl3-re-d5`; Q24042 is played to Glati and left at the prisoner.
   (Overtaken the same day: the operator's "We DO NEED the siege enabled" brought the sieges back, and the leg `rq` plays
   Q2724 and Q24042; Q2727 still waits for Roah in Elyos hands, decision 8.)
10. **Q24043 waits at its lure until the operator says how it is done (NL-3, 2026-10-18).** Sinjah's lure needs an ashikar
   assaulter struck with an auto-attack within 15 m of him; the nearest stands 85 m away. Java turns a monster home after
   20 s in which it has neither struck nor been struck (`AttackManager.checkGiveupDistance`; Reshanta has no `ai_info`).
   Six tries (`nl3-rq-t1`, diagnostics `d10` to `d15`) found more: a bot that runs is never caught; the Cleric companion
   (NL-a) draws the monster's attacks; the walk's own defence kills the monster on the way; and a monster drawn from 130 m
   away stays within 30 m of its spawn. The bot now strikes first, leads in stretches and strikes again as it catches up.
   That played the lure once (the Sorcerer in `d14`) and failed in the other tries. The operator was asked how a player
   brings the monster to Sinjah in the client (or for a recording); until then `rq` leaves Q24043 at var 2 and `ro`
   leaves out its errand to Phosphor.
11. **The survey's trips out of the Abyss are three legs, by where their quests end (NL-3, 2026-10-18):** `ro` (Q24044,
   Ishalgen and Pandaemonium), `rb` (Q2773, Q2757 and Q2758, which end in Beluslan), and `rd` (Q24046: Brusthonin,
   Pandaemonium and the Shadow Court Dungeon). Each is a round of the eight with its own snapshot, so a fault in one
   leg costs none of the others.
12. **Q2757, Q2758 and Q24046 wait for NL-4, among "the quests a step left for a higher level" (NL-3, 2026-10-18).**
   - Q2757 and Q2758 start at level 28, but both end with Etun on Kurngalfberg, 1.5 km into Beluslan's level-40
     ground. aioncodex's 4.8 page has the same place.
   - In diagnostic `nl3-rb-d7` the Cleric, at 37, met a pack every 20 m and was cornered twice. 120 hazards then
     rejected every route to him.
   - Q24046 sends a bot alone into the Shadow Court Dungeon, a solo instance where no companion goes (NL-a). Its
     monsters are levels 43 to 46.
   - They are played after the gear check-in at 36, or later.

   (aioncodex's 4.8 page gives Q2757 to Gagrad (278087) in Reshanta. Java's data and this server's give it to Etun
   in Beluslan; moving the giver would be a retail correction, the operator's to approve.)
13. **Reshanta's group quests go to NL-4's group pass, with Q2355 (NL-3, 2026-10-18).**
   - They are Q2728 (Ladacus, an Elyos guard of 77,859 HP, 2,750 m from Primum) and what bots left for the group:
     Q2753 and Q2754 for the Sorcerer and the Ranger, and Q2762 and Q2763 for four bots.
   - The group stage (`group-field`) originally flew its members across a fly zone in one flight. It now also shares
     the separately implemented ring routing above. Reshanta still needs coordinated ground hops where no ring chain
     reaches the destination; that work remains for NL-4's group pass.
   - Q2762's mirrorglass waterworms float where no navmesh lies. They are a kill in the air (the leg engine's air kills,
     `NaturalAltgardAirKills`), not a matter of numbers, and are tried there too.
14. **The "level-36" companion profiles are at 37 and 38 (NL-4, 2026-10-18).** The quests carried every bot past 36 before
   its check-in, and a profile is its character as the world has it.
   - The server gives a companion at the highest level it has a profile for that is not above its player's.
   - So a player at 36 still has the level-31 companions; a player at 37 has five new ones and the level-31 Cleric,
     Chanter and Gladiator; a player at 38 has all eight new ones.
   - Profiles at exactly 36 would need characters held at 36, which the leg's quests do not allow.
15. **Fire Room recording reviewed; the NL loop stays paused (the operator, 2026-10-20).** The operator demonstrated
   Q24023 on `rrfarmer`'s `Asimgladiator`, then asked for a recording review, a Java 4.8 mob-profile comparison and an
   audit of the missing HP potions and shield scrolls. Evidence is under `run/recordings/fire-room-prep/`; the closed
   session has 32,382 entries and passes the recording reader's structural check.
   - **Entrance:** the operator says to fly up and around to reach Fire Room Entrance. The recorded flight, landing
     and portal use must be retained; the portals' short straight-line separation does not show a walkable route.
   - **Descent:** one 16.840-second glide from approximately (100.665, 89.770, 611.551) to
     (220.145, 226.602, 554.217), followed by a small drop to (225.809, 227.640, 551.155), reaches the lower tier at
     full HP. It bypasses the middle-tier walk. `NaturalFloorGlide.MaximumDrop` is 45 m, and its horizontal/drop
     limit is 3:1; both exclude this recorded pair. The path also includes upwind flags and turns, so merely increasing
     the limits is not a validated fix.
     The accidental Wind Room visit supplies additional evidence of an intermediate ledge at z390.407.
   - **Conditions:** this account has GM access, unlimited flight time and the GM in-place revive. A normal-account
     replay has not been shown. The main glide estimates 34 FP at Java's 2 FP/s before reserve; the recording proves
     the movement, not that budget. One upper-tier death was followed by the in-place revive. Bolcan was killed and
     his key handed to Bulagan (REWARD); the final reward from Gangleri was not collected in this session.
   - **Mob profiles:** all 28 templates spawned in Sky Temple Interior match Java's stats, levels, ranks and ratings;
     its spawn file is byte-identical. The running image's instance templates also match the checkout, and recorded
     target HP agrees with Java. Most Fire trash has 21,557–32,358 HP versus Wind's 3,637–5,141, and Bolcan has 57,376.
     The default attack/defense calculations match Java. The `battle_cycle` AI replacements are the approved retail-AI
     layer, not a reason to revert them or reduce HP. No mob data or behavior was changed by this review.
   - **Supply audit:** every original class had 30 Major Life Potions (162000006) and 30 Major Anti-Shock Scrolls
     (164000070) in `nl-g36-s2-w1`; the generated Gladiator-38 profile still has both stacks. In `nl4-vs-c1` the
     Gladiator used all 30 potions (27 in combat, 3 during rest) and all 30 scrolls, with no further supplied items.
     Nineteen of its 20 deaths followed potion exhaustion; nine followed shield-scroll exhaustion. The long failure
     retries did not reach another effective run-start, level-up, town or successful endpoint stock check. This is
     a restock scheduling gap, not evidence of a broken item manifest or the cleanup deleting those supplies.
   - **Manual character preparation:** the recording character was copied from the depleted `nl-vs-s1-w1` failure
     snapshot rather than supplied from the full check-in kit. Its missing potions and scrolls were therefore real;
     copying a retained journal/gear state must also check and refill the approved kit before handing it to a player.
   - **Still open:** how to classify the other classes' solo capability. Neither depleted failed attempts nor the
     GM-assisted recording settle it. This review neither replays the seven nor ticks NL-4/NL-5.
   - **Validation:** the existing `CLAUDE.md` checks passed, including the solution suite, warning/clock guards,
     profile generation and export checks, navmesh checks and companion-server checks. Logs are in
     `run/recordings/fire-room-prep/checks/`. No new tests or gameplay changes were made.

16. **Recurring stock and recorded Fire travel implemented; NL remains paused (the operator, 2026-10-20).**
   The operator authorized these fixes and the existing checks, with a commit by path on `main` and no push.
   - **Stock:** from level 10, quiet pre-pull, after-rest and after-revive buff checks now inspect the class's approved
     kit. They call the existing supplier only below the existing per-family thresholds, retaining its delivery checks,
     `help-item-supplied` receipts and `help-items.json`. Disabled supply, death, active casting/item activities,
     engagement/pursuit, flight and gliding prevent recurring supply. Each row is checked again before delivery.
     This covers retries that never reach a successful endpoint or another level. Starter supply scheduling is unchanged.
   - **Fire approach:** go to the Wind side of the entrance pad, then follow the recorded climb, turns around the
     structure and landing at Fire Room Entrance. Each segment clears the shipped geometry and remains within ordinary
     FLY permissions, including the near-ceiling part. It is a finite flight with a landing reserve, not GM free flight.
   - **Fire descent:** walk to the recorded upper brink, defend/rest, refresh the ordinary travel buffs, jump and follow
     the turning glide to the lower floor. Preserve the recorded upwind flags and the final short FALL/ground samples.
     Select this recorded route before trying a ground approach to the lower Fire objective; it must not depend on a
     failed walk or on whether the navmesh labels the tiers as different islands.
     Pace movement no faster than the bot's observed speed or the recording, budget at 2 FP/s with the existing floor
     glide's five-FP reserve, and recover FP on the ground with defense checks. Generic floor-glide limits stay unchanged.
     The public waypoints are generated in `parity-artifacts/e2e/natural-fire-room-route.json` by
     `tools/recording/compile-fire-room-route.py`; the private raw recording remains under `run/recordings/`.
   - **Targeted evidence:** an ordinary access-level-0 Gladiator at 38, wearing the generated check-in equipment and using
     its approved Running scroll, flew the entrance route from the Wind side and landed with 53/72 FP. It then glided
     to (221.838, 226.147, 550.494), retained full 4,678 HP and landed with 28/72 FP; the ground route onward to Bolcan
     has 42 points. The disposable motion probe cleared nearby NPCs, so this is route/FP evidence, not an encounter pass.
     The stock probe refilled HP potions and shields repeatedly to 30 each, skipped supply while disabled, engaged,
     casting and airborne, then refilled after landing without duplicate delivery at full stock. Evidence is in
     `run/fire-room-fix-probe/final-probe.log` and the corresponding ignored probe trace.
   - **Scope:** no mob/stat data changes, no NL retry or snapshot capture, and no solo-capability classification.
     NL-4/NL-5 stay unticked until the operator resumes the loop after reviewing the route.
   - **SIM monitor override:** the round, snapshot and neutral wrappers were clearing `AION_BOT_DASHBOARD_PORT`,
     defeating the standing 17881 override beside the live stack. They now preserve it; the neutral evidence records
     the selected port. The existing snapshot/neutral contract assertions were updated accordingly; no new unit tests.
   - **Older snapshot schema:** the first required NTC replay stopped at authentication, before gameplay: its retained
     `ntc-group-s5` dump lacks `players.pvp_exp` and the renamed appearance columns (and predates the other shipped
     migrations). Validation uses a generated disposable copy with the exact current `game-server/sql/update.sql`
     appended to its restore, under `run/fire-room-fix-probe/compatible-snapshots/`. Its provenance retains the original
     dump hash; the original snapshot and every NL snapshot remain untouched. Apply those already-shipped migrations
     on disposable restores of older NL worlds before any future resumed run; do not silently rewrite their captures.
   - **Validation:** the final solution suite passed (5,856 GameServer, 342 Commons, 77 ChatServer, 224 LoginServer and
     164 non-integration Simulation tests, with existing skips), as did the warning/logger/clock/draft guards and fidelity.
     The existing Python/PowerShell contracts, profile/export checks, baked navmesh, companion-server checks and Fast SIM
     passed (115 tests and all 11 manifest scenarios). Five starter journeys passed, repeated with actual monitor port
     17881; exact trace comparison still fails first at object IDs under the standing D7 exception, with no re-recording.
     All six bots reached the NTC endpoint from the compatible disposable copy, on 17881. Logs and the detailed review
     are under `run/fire-room-fix-probe/`.
   - **Broader check resolved, 2026-10-20:** detailed diagnosis reproduced 11 failed stages: ten cooldown packet assertions
     compared serialization-time values to the later clock after arrival, and the GM-only Lucky Danuar staging point
     touched Poisoned Ide's material mesh. Java calculates the packet's seconds while writing and sends a dead Danuar
     logout to bind. The matrix now compares against the transport's serialization timestamp, stages that GM guest on
     verified safe ground, and retains row verdicts/full exceptions plus both cooldown timestamps. The full `INSTANCES`
     rerun passed all 108 stages across 59 maps, with zero log-policy problems. Detailed evidence, packet traces and TRX
     are under `run/instances-diagnosis/`; the Java sources and shared Danuar hazard observation are recorded under P11-01
     in `docs/e2e-player-simulation-plan.md`. The related PvP rerun also passed after re-keying existing Java-shared log
     allowances to structured message templates; the same per-race/count limits remain enforced. This fixes the matrix
     harness; server mechanics and portal/material data retain Java behavior, and compiler warning/neutral trace baselines
     are unchanged. NL remains paused, with NL-4/NL-5 still unticked and no NL replay or snapshot capture.
     All four entry/reset matrices passed. The required solution suite (6,663 passing, 252 existing skips), guards,
     fidelity, script contracts, profiles, baked navmeshes and companion-server checks also passed; the disposable
     allowance-boundary probe confirms the original per-race/total budgets and rejects another map or row.

17. **Resume NL-4/NL-5 from `nl-vs-s1` (the operator, 2026-10-20).** Replay the seven unfinished classes with the
   fixed recorded Fire Room travel and recurring top-ups, then play the group pass, evaluate the quests deferred for
   higher levels, and write the per-class NL-5 report. Keep combat rotations and learned skills current as the bots
   progress. Preserve the Cleric's completed state in the same world, with starts ten game minutes apart and NL-a.
   Restore the original snapshot through a generated disposable image with the exact shipped schema migrations;
   preserve its source hash and the original capture. The first round is uncaptured, and a captured round uses committed
   runtime code. Solo-capability classifications follow the resulting evidence, not the depleted older attempts.
   Other unanswered retail decisions remain open. The operator clarified the conflicting push instructions on
   2026-10-20: **commit and push as work progresses**. This overrides the earlier no-push rule; commits still name
   their paths explicitly on `main`, without a branch, worktree or attribution trailer.
   - **First uncaptured trial (`nl4-vs-resume-t2`):** only the Templar and Sorcerer had started when the host was
     stopped for diagnosis; the other five were not played. No snapshot was captured. The disposable schema was
     dropped by its owner, and the original `nl-vs-s1-w1` dump retains SHA-256
     `7351b114d7afd253c9c461880fb568eafeac0c403e2fd8b5b195f7bc144bc782`.
   - **Water Room:** the Templar recorded 194 blocked combat approaches, often at the same horizontal point as a
     moving Corrupt Water Spirit whose reported height was several metres above the floor. The final trace then
     shows eight kills in the room, the key item `182215366` looted, and departure at 7:07 game time. It later died
     outside and reached the Wind Room. The operator is unsure whether Water needs jumps/glides and permits geometry
     inspection and a bounded attempt, with a recording available if still needed. This is evidence of a costly
     approach problem, not a demonstrated impassable room; no Water route or NPC data was changed on that inference.
   - **Fire launch:** the Sorcerer flew the exterior recorded route and entered Fire, but the avoidance-only walk to
     the descent launch was hazard-rejected before any glide. The launch approach now reuses the existing defended
     blocker-clearing walk, bounded by progress, survival and the current map. The new trial must still prove it with
     live hostiles; the earlier motion probe cleared those hostiles.
   - **Skills through 38:** review the original eight against Java's auto-learn tree and templates, including later
     ranks. Extend the existing profile validator's ceiling from 31 to 38. Use ordinary single-target damage and chain
     follow-ups, conditional finishers only on observed target states, defensive buffs and Ranger's Breath of Nature
     toggle. Keep explicit exclusions for area pulls, unsupported trap/rear positioning, party resurrection, and
     Sage's Wisdom's doubled mana costs that the heal reserve does not model. Spirit Absorption's Java template has
     only a negative percent heal on the pet, so it remains excluded. No server skill data or learned skills are edited.
   - **Bounded diagnostic (`nl4-vs-resume-t3`):** the Sorcerer killed three Fire launch blockers through the defended
     approach; the Chanter killed its first. Neither proves a completed descent or Q24023. The Templar cleared Water
     and Wind. This diagnostic mistakenly set every seat's stop to minute 25 of the world, giving later starts shorter
     windows and the last four no window. Their companion seats then waited for players whose callbacks never began.
     The exact owned test host was stopped, letting its wrapper drop the disposable schema; no snapshot was captured.
     A corrected diagnostic must set each absolute stop to its own start plus the intended play duration. Do not count
     these cancellations as combat failures or claim that all seven played.
   - **Validation before this commit:** all 33 newly reviewed skill templates structurally match the Java 4.8 source.
     All eight profiles pass their existing validator through 38. The solution suite passed 6,663 tests with 252
     existing skips; warning/logger/clock/draft guards, fidelity, script contracts, profile generation/export,
     baked navmeshes and companion-server checks passed. Fast SIM passed 115 tests and all eleven manifest scenarios,
     with no failures or flakiness. All five starter journeys passed; exact comparisons still begin with the known
     D7 object-ID difference, without re-recording. The NTC replay (`nl4-r38-ntc`) retained six verified home receipts.
     Logs are under `run/nl/resume-20261020/`, with starter/NTC evidence under `run/nl/NL4/nl4-r38-*`.
     These checks validate the runtime changes; they do not complete NL-4, the seven's Q24023, or NL-5.
   - **Corrected-window trial (`nl4-vs-resume-t4`, 2026-10-20):** every seat had its own start plus 45 game minutes.
     The Chanter cleared the live guards and completed the recorded descent, landing at full 4,122 HP with 20 FP.
     None of the seven completed Q24023. The completed round's wrapper dropped its own disposable schema; no snapshot
     was captured. Its per-bot outcomes, not the wrapper's successful exit, define that result.
     - The Templar's entrance approach stopped 1.98 m from the launch: the walk required 1 m, although the flight's
       checked start already permitted 2 m. The walk and flight now share the same two-metre tolerance.
     - The Sorcerer reached the brink, then the mandatory post-rest loot sweep walked back to its earlier kills.
       Preparation now finishes defense, rest, loot and buffs and returns to the launch before flight, with bounded
       retries and the original clearance/zone/FP checks. It retains every required loot sweep.
     - A preparation callback now reports a death/revival or map change. A same-map revival during entrance preparation
       restarts ordinary quest travel instead of being mislabeled as an unsafe route.
     - The Gladiator was rejected before login because its retained state was inside Sky Temple. Only leg `vs`, from
       Q24023's minimum level 29, now accepts map 320050000. Java's `_24023ABlazingRescue.onEnterWorldEvent` explicitly
       handles that map. Other legs retain their map restrictions; this grants no teleport or quest progress.
     - The Ranger hit its intended world-minute-95 deadline while resting. Sending the cleanup stand packet on its
       closed transport masked cancellation as `ObjectDisposedException`. Rest cleanup now preserves an existing
       cancellation in that case; a genuine transport failure still propagates.
   - **Focused follow-up probe:** the existing scratch SIM console was copied under `run/fire-room-followup-probe/`.
     An ordinary level-38 Gladiator started at the Templar's near-launch point, flew the entrance, then deliberately
     walked away during descent preparation. It returned to the brink and glided to the lower floor at full 4,678 HP,
     with 28/72 FP. The probe also checked interrupted preparation, leg-scoped retained identity, cancellation
     preservation and propagation of genuine transport failure. Nearby NPCs were cleared for this motion diagnostic;
     live encounter evidence remains the Chanter's t4 descent. Logs: `run/fire-room-followup-probe/probe.log`.
     Trial `nl4-vs-resume-t5` tests the candidate with all seven, Gladiator first and ten game minutes between starts;
     each has a 45-minute play window. It is uncaptured. All seven played and reached their intentional deadlines as
     `OperationCanceledException`, with no route-refusal or disposed-transport exception ending a run. Gladiator,
     Chanter, Sorcerer and Assassin completed the live recorded descent (one, two, one and two times respectively),
     landing at full HP with 28, 20, 16 and 16 FP on their last descent. Loot/settlement relocation recovery occurred
     in the live Gladiator, Chanter and Assassin runs. The Gladiator's retained login succeeded. Nobody completed
     Q24023 in this window; a deadline is not a solo-capability classification. Per-class evidence is indexed in
     `run/nl/resume-20261020/t5-summary.json`. Its owner dropped its disposable schema.
   - **Follow-up validation:** the solution suite again passed 6,663 tests with 252 existing skips, along with all CI
     guards, fidelity, script contracts, profile generation/export, baked navmeshes and companion-server checks.
     Fast again passed 115 tests and all eleven manifest scenarios with no failure or flakiness. All five starter
     journeys passed; their exact comparisons retain D7's first object-ID difference, with no re-recording. NTC
     (`nl4-r38-followup-ntc`) retained six verified home receipts. Logs are kept separately under
     `run/nl/resume-20261020/followup/`. No compiler warning or trace baseline was raised, no generated data or mob
     statistics were edited, and no new bot unit test was written. NL-4/NL-5 remain open.
   - **DP jelly upkeep (2026-10-20):** the approved kit already supplied Cippo Aether Jelly to all classes, but the
     use policy only recognized the Cleric's Salvation. The original eight now refill DP for a mapped skill actually
     present in the client-learned list, using that skill's positive cost. Salvation keeps its existing 2,000-DP reserve;
     combat priorities and reserves do not change. Starter defaults and the frozen three retain their behavior.
     Java's item 160002274 permits every class from level 10, uses skill 10164 at level 4 (4,000 DP), and shares
     use-delay group 23 for 1,800 seconds. No manifest, learning, server skill data or DP balance was changed.
     The scratch console under `run/dp-kit-probe/` exercised the actual buff controller on ordinary accounts: all eight
     consumed one jelly, observed DP rise from zero to 4,000 and the real 30-minute delay, then withheld another use.
     The Gladiator spent 2,000 of that DP on an ordinary learned Daevic Fury cast. The frozen three left their jelly
     untouched. Controlled class/level/stock setup makes this item-use evidence, not a natural quest completion.
   - **NTC regressions exposed by the DP replay:** `nl4-r38-dp-ntc` stopped gathering because the Gladiator could not
     route from (306.074, 592.501, 373.024). The two-metre-high ground ray hit a steep surface above its supporting
     floor; Java's `GeoMap.getZ` rejects that nearest surface too. Bot ground queries now retry near the feet only
     after that ray fails on a low lip below the existing body collision ray, requiring support within 10 cm of the
     feet and retaining slope rejection, step limits and body-height collision checks. The exact failed
     point changes from zero paths to ten-point checked paths; unsupported higher points still have no route.
     Server geometry and generated meshes are unchanged. The next replay reached the gate but lost its death:
     F1's final cast was rejected, a teammate's delayed hit reported zero HP one second later, then the gate vanished.
     The group now retains the Gate/General's explicit zero-HP evidence after a fight returns, scoped to the instance
     and guarded against object-ID reuse. Disappearance alone does not satisfy this new observer. Java's HP percentage
     is zero only at zero HP. Trial `nl4-r38-dp-ground-ntc-t3` then retained six verified home receipts, including the
     gate's delayed death diagnostic. The first fallback was too broad: the existing Fast Q2006 Mau-return negative
     caught a route across its obstacle. It remains a failure in `dp-ground/checks/fast.log`; the narrowed fallback
     distinguishes NTC's 23 cm lip from the Mau obstacle 1.56 m above the feet. The offline check retains no Mau
     approach or journey, and still finds NTC's checked route. Evidence is under `run/ntc-dp-diagnosis/` and the NTC
     replay directories. The narrowed candidate passed all required checks: 6,663 solution tests (252 existing skips),
     warning baseline, source guards, fidelity, script contracts, profiles, baked navmeshes and the two companion-server
     tests. Fast passed 115 tests (98 existing opt-in skips) and all 11 scenarios. All five starter journeys passed
     functionally; their exact comparisons retain only the allowed first object-ID difference under D7. Final replay
     `nl4-r38-dp-lip-ntc` retained six verified home receipts. Logs are under `run/nl/resume-20261020/dp-lip/` and
     `run/nl/NL4/nl4-r38-dp-lip-*`. No new bot unit tests, raised baseline or generated-data edits were needed.
   - **Capture provenance:** the unchanged schema-compatible source image is also available at
     `run/snapshots/nl-vs-s1-compatible-w1/`, with both source and derived dump hashes verified. The report audit
     follows its `compatibility.sourceSnapshot` edge and excludes the generated layer from gameplay totals. It still
     finds exactly 24 retained NL captures and their evidence folders back to `morheim-ft-s1-w1`.
   - **Partial longer trial (`nl4-vs-resume-t6`):** all seven started ten game minutes apart and consumed jelly during
     ordinary questing. The trial was stopped for diagnosis after the Assassin began, rather than exhausting its
     planned 120-minute windows. No endpoint or snapshot is claimed. Its verified owner dropped only its disposable
     schema. `run/nl/resume-20261020/t6-live-audit.json` indexes the partial traces; deaths and interrupted windows
     are not a solo-capability classification. Sky Temple's PvE attack/defense map bonuses and their stat functions
     also match Java; no missing map multiplier was found.
   - **Use the existing recorded fights (operator clarification, 2026-10-20):** the operator already demonstrated
     which monsters to pull, as well as the descent. No further recording is needed to learn that sequence. After
     landing he cleared the nearby Guardian Kalgolem, two Destroyer Obscuras, then the Corrupt Fire/Flame Spirits,
     rested and pulled Bolcan alone. The t5 Gladiator instead followed its helper prediction north to
     (217.246, 302.170), first casting Cleave on the western Flame Spirit at (212.520, 290.760). Bolcan and another
     spirit joined that fight. The recording leaves that western spirit alive and pulls Bolcan from the east at
     (232.474, 281.307), opening with Taunt. This was a different pull, not proof that the player fought the same pack.
     The recording also uses Absorbing Fury and Pressure Wave, which the current single-pull table excludes to avoid
     waking extra enemies; assess conditional use against already engaged enemies using Java's actual area rules.
     Skill 240, GM's Armor, was used in the recording (+2,000 maximum HP/MP and movement/attack/flight benefits), so
     its survivability/timing is not an ordinary-account baseline. Its pull order remains useful evidence. The
     private sanitized comparison is `run/nl/resume-20261020/human-fire-fights.json`; the original recording is
     unchanged. These findings do not authorize copying GM skills or nerfing the mobs.
   - **Recorded combat candidate (2026-10-20, validation in progress):** the existing route compiler also extracts the
     five guard targets and the boss pull origin. It identifies the Guardian and the second Obscura by their native
     patrol routes, because the recorded NPC positions are not stationary spawn hints. The lower-floor routine uses
     current observed identities and normal combat, looting and rest. It does not treat an unseen guard as dead;
     corpse/zero-HP evidence is scoped to the instance, retained when returning to the same copy after a revival, and
     invalidated by a later live spawn. Before Bolcan it returns to the demonstrated eastern origin and prefers a
     learned non-damaging pull when the class has one, rather than making the generic helper planner draw the western pack.
     Gladiator's Absorbing Fury, Pressure Wave, Roiling Hack and Seismic Billow are conditional area attacks: actual
     Java radius/altitude and body bounds are checked, with a conservative position margin; uninvolved, unknown and
     controlled creatures withhold them. Other profile catalogs have no existing self-centred enemy-area attack to
     change. Callers without an area-safety observation withhold these new attacks. Recovery reserves and the
     three-attacker retreat threshold remain as before.
     The controlled ordinary-account probe `run/fire-fight-probe/probe-t4.log` passed: an unengaged nearby NPC blocked
     the area IDs; actual learned Absorbing Fury/Pressure Wave casts dealt damage and six absorbed-HP events restored
     1,756 HP, with no GM's Armor. Seismic Billow completed after the sixth real proc roll. Java gives Pressure Wave
     and Roiling Hack a 10% follow-up chance, not a guaranteed Billow; that probability and all cooldowns remain intact.
     Controlled class/gear/NPC placement and starting HP make this effect/chain evidence, not a survival or quest claim.
     Uncaptured diagnostic `nl4-vs-resume-t7` exposed two route-control defects before a completion: the Gladiator
     tried to cross the Guardian's guarded platform while its patrol was far away, and the Chanter's second descent
     forgot the Guardian it had killed in the same instance. Raw movement packets in the existing recording show the
     Guardian coming west to the landing before the first melee hit. The candidate now waits there, bounded at two
     game minutes with ordinary defense, and retains the same-instance kill evidence. The stopped trial's verified
     owner dropped only its disposable schema; it gives no solo-capability classification.
     `nl4-vs-resume-t8` then reached the Guardian correctly but exposed an omitted ground corridor: the player goes
     west along x219–222 before curving toward the first Obscura; a direct destination hint instead took the raised
     platform and the broad blocker fallback fought unrelated lower-floor mobs. The compiler now includes those
     connecting walks, simplified within 0.5 m from actual reported positions, with short geometry-checked steps at
     observed speed and ordinary defense. The offline check accepts all 36 points and 30 connecting segments on the
     correct floor; no further geometry or navmesh change is justified. T8 was stopped and its disposable schema
     dropped. `nl4-vs-resume-t9` followed that western corridor correctly, then stopped fighting the first Obscura
     at 47% NPC HP while the Gladiator remained healthy: the NPC moved east and four distance refusals exhausted
     recovery, whose generic planner rejected the guarded route. The existing human footage already shows this
     fight and chase: actual movement from (233.215, 252.787) to (254.561, 252.027) precedes that target's explicit
     zero-HP packet, source sequence 26609. No additional recording is needed for this diagnosis.
     The compiler now bounds each fight's ground pursuit by its own explicit death packet. Only those prepared
     Fire fights can use the corresponding corridor for range recovery, at most 12 m per recovery with fresh
     ground/collision checks, observed movement speed and normal combat observations between recoveries. It never
     extrapolates beyond the demonstrated path. T9 was stopped and its owner dropped its disposable schema.
     T10 proved the first 12 m of pursuit but exposed a second decision: at ranged Cleave distance the Gladiator
     waited for its cooldown while Obscura cast Hide Shape (16677), then abandoned the live target when it disappeared.
     Java's skill and `HideEffect` confirm a brief hide, not a death. A prepared melee Fire fight now continues along
     its corridor during that ranged-skill wait, and a completed observed self-hide permits a bounded visibility
     wait from the native effect duration; neither absence nor elapsed time supplies kill evidence. T10's owner
     dropped its disposable schema. T11 then caught and killed that first Obscura, waiting through its hide, with
     no revival or retreat. The next Obscura joined during the mandatory loot sweep, whose ordinary defense call
     bypassed the prepared pull's recovery callback. Recovery is now resolved for every currently bound recorded
     guard in that native instance, including loot/rest defenses; other targets retain their ordinary planner.
     T11 was stopped and its owner dropped its disposable schema. T12 killed both Obscura guards, then exposed a
     rotation stall: the Gladiator stood at Cleave range waiting between its 18-second cooldowns instead of closing
     for its weapon and melee chains. The Fire Spirit reached 4% HP before the existing 1,000-action stall guard
     stopped the fight. When solo, the Gladiator now requests weapon range between ranged cooldowns even when the enemy's
     template lists a melee attack range; other classes retain their existing rule. Recovery and retreat priorities
     and the action bound are unchanged. T12's owner dropped its disposable schema.
     **T13 ordinary-account result:** the Gladiator killed all five recorded guards and Bolcan (18:13.929–19:37.911
     world game time), then advanced to leaving the Fire Room with its key. Those six fights had no revival or
     retreat; the boss fight observed up to two attackers, so this is not a claim of an isolated one-target fight.
     The full Q24023 endpoint was not reached before the diagnostic stopped. Chanter also killed both Obscuras but
     its next walk rejected a valid ramp from a ledge reached during loot/rest. The ground executor now permits
     collision-checked supported steps between its current and destination elevations; a same-floor walk still
     rejects the raised deck. The offline audit proves all eight steps of that ramp.
     The footage already includes the exit: after the boss, source sequences 31338–31456 walk north along the
     upper floor to (233.134, 299.851, 550.494), then use portal 730046 at sequence 31467. Java's native six-metre
     talk range and three-second use permit that position. The compiler now emits this exit walk, and the bot uses
     the observed portal from that lip instead of routing toward its lower spawn coordinate through extra elites.
     T13's owner dropped its disposable schema.
     **T14 verified endpoint:** Gladiator completed Q24023, returned to Morheim through the recorded exit and took
     Gangleri's reward: `altgard-vs-completion.json` verifies Q24023 complete count 1, level 38 and zero deaths in
     1,574,645 elapsed game milliseconds. The receipt is under
     `run/nl/NL4/nl4-vs-resume-t14/w1/warrior-gladiator/`. All seven started. Chanter still left the intended corridor
     during later defense/recovery and stopped at (118.200, 226.200, 538.469); the nearby-ramp correction does not
     resolve that separate detour. The other five retained partial traces, including repeated failures before the
     Fire descent. The diagnostic was stopped for that follow-up, and its owner dropped its disposable schema.
     It captured no world and supplies no soloability classification for the unfinished classes. The original
     `nl-vs-s1` world and Cleric completion remain unchanged; a retained replay must use committed runtime code.
     The shared NTC gate exposed why that new closing rule must remain solo: in its first version the Gladiator
     died and revived away from the Aetheric Field Guard (256692), missing Q4733 credit while the other five members'
     native counters advanced from 65 to 4161. Java's `PlayerTeamDistributionService` awards quest kills only to
     online members within range. Group positioning now retains its existing rule; no quest counter or NPC state
     is bypassed. The focused `nl4-r38-fire-fights-ntc-solo-fill` replay produced six verified home receipts.
     The final candidate passed the required repository checks (6,663 tests, 252 existing skips), both companion
     tests, Fast (115 tests, 98 existing opt-in skips; all 11 scenario rows), all five functional starter journeys
     under the existing D7 first-object-ID trace allowance, and n9 with six verified home receipts. Logs are under
     `run/nl/resume-20261020/fire-fights-verified/`; the final NTC run is `nl4-r38-fire-fights-verified-ntc`.
     Earlier green results and the NTC regression remain under `fire-fights-final/`. No warning or trace baseline
     was raised, no bot unit test was added, and the final owned SIM schema was dropped with the run marker clear.
     NL-4/NL-5 are open.
   - **2026-10-21, continuing from the existing recording:** T15 (`nl4-vs-resume-t15`) gives the Chanter an ordinary
     Fire clear: all five recorded guards and Bolcan killed, no deaths or retreats in those fights, and the exit used.
     Its Bulagan hand-in remained unfinished when the diagnostic stopped. The Gladiator again completed Q24023,
     verified at zero deaths at receipt world time 2,668,282 game milliseconds. These are uncaptured diagnostics; the retained world is
     still S1, and the other five classes have no soloability classification. The operator asked which classes remained
     unfinished; that clarification is not a decision to declare them incapable at 37–38 or defer them for levels.
     The Chanter's earlier recovery detour exposed an identity gap: an Obscura first announced between patrol legs can
     miss the spawn matcher, while later ordinary WALK packets prove its native route. Matching now accepts that
     observed route evidence, preserving the one-metre tolerance and excluding RUN/chase movement as identity evidence.
     An experimental quiet-patrol movement exception never fired in T15 and was removed; ordinary defense remains.
     The recording also covers the full upper Fire approach before the descent. The compiler now preserves its western
     ground corridor, four explicit jumps and two short falls as separate segments (99 retained movement points).
     A read-only geometry audit checked every ground point and edge, airborne clearance, and supported takeoff/landing:
     zero unsupported points and zero blocked segments (`run/fire-upper-probe/probe-segmented.log`).
     T16 showed that blindly walking a ranged class into the recorded player's first pair is unsafe, and that combat
     can leave a stale waypoint after retreating. T17 showed that checking only the ground approach misses the pair
     across the first jump. The executor now also checks that next airborne segment for client-observed obstructions,
     pulls from ordinary spell range before entering their aggro circles, and makes at most three same-copy rejoins
     after a failed approach. All travel remains collision checked and speed paced, with actual combat, loot and rest.
     T18 reached the descent with ordinary Sorcerer, Chanter and Gladiator characters. The Chanter and Gladiator
     cleared the lower guards and Bolcan without a death; the Sorcerer killed the Guardian, then its first Obscura
     fight drew another Guardian after a refused Freezing Wind cast caused a twelve-metre pursuit. The caster policy
     had treated a recent incoming hit as proof that its short spell still reached a moving target. Java's
     `FirstTargetRangeProperty` checks each cast; casters now honor observed skill reach and use an available longer
     spell instead. The melee fighters' contact inference is unchanged.
     The recording continues after Fire's exit: FLY at source 31623, a climb from the exit to Bulagan's platform,
     LAND at 31728, and his dialog at 31747. A compiled return flight now handles that three-key hand-in before a
     generic walk can detour below the platform. Its 23 points cover 71.1 metres, with zero blocked segments, zero
     forbidden-zone samples and a supported landing (`run/fire-upper-probe/probe-return.log`). It uses the same
     ordinary-zone and finite-FP executor as the entrance; the recording's GM flight allowance is not inherited.
     T15–T18 were stopped for these diagnoses; each owner dropped its disposable schema. T19 played to its end,
     with verified Q24023 receipts for Chanter (38, 333 quests, one death, receipt world time 2,586,282 game milliseconds),
     Gladiator (38, 335 quests, zero deaths, 2,913,712 ms) and Templar (38, 336 quests, zero deaths, 5,347,282 ms).
     Chanter's ordinary return flight landed at 41:20.674 world game time with 49 FP, followed by Bulagan's key check
     and Gangleri's reward. Templar and Ranger both cleared Water and Wind; their earlier room obstructions are not
     evidence of an unsolved player-route mechanic in this round. T19 captured no world, and its owner dropped its schema.
     The Sorcerer's corrected rotation avoided the erroneous short-spell chase, but another Guardian still joined:
     its hard two-attacker retreat rule abandoned an Obscura at 20% while the Sorcerer had full HP. The remaining
     Sorcerer, Spirit Master and Ranger outcomes stopped on Fire clearing; their tactics remain open, not classified
     as class soloability limits. The Assassin exposed a mistake in the new anticipatory pulls: its table has no
     ranged attack, so the first ranged pull failed its approach three times without moving. That step now applies
     only to profiles whose pull style is not WalkIn; the Assassin follows the demonstrated walk/jumps and normal defense.
     The pre-final required checks passed (6,663 tests, 252 existing skips, unchanged warning baseline, contracts,
     profiles, navmeshes and both companion tests). T20 puts the Assassin first to validate that correction, then
     plays the other six with ten-minute starts in the same world. T20 finished normally, with another Chanter and
     Gladiator completion. The Assassin no longer attempted any ranged pull: it completed the upper approach and
     descent four times, killed the first Guardian and both Obscuras, then died during later Fire combat. Its initial
     login had 1,471/3,920 HP, despite correct level-36 Shadedrenched armor and two daggers; startup supplied 30 life
     potions and later refills are recorded. Initial recovery needs attention before any soloability verdict.
     T20's other unfinished bots hit its common world-minute-90 deadline or stopped on Fire clearing; that deadline
     gave the later starters less play time. The owner dropped the disposable schema. Receipt `ElapsedMillis` is
     world time, not individual play time; NL-5 must use each bot's `EndedAtMillis - StartedAtMillis`.
     Final verification passed on the WalkIn correction: 6,663 solution tests with 252 existing skips; the unchanged
     warning baseline and all guards/contracts; profiles, navmeshes and both companion tests; Fast's 115 tests with
     98 existing opt-in skips and all 11 scenario rows; all five functional starter journeys under the existing D7
     first-object-ID allowance; and n9 with six verified home receipts. Evidence is under
     `run/nl/resume-20261020/upper-fire-final/`, with n9 at `nl4-r38-upper-fire-final-ntc`.
     The final owned schema was dropped, the run marker is clear, and the compiler check reproduces the artifact
     from the original recording. No warning/trace baseline was changed and no bot unit test was added.
     NL-4/NL-5 remain open.

   - **2026-10-21, recovery and pair-fight follow-up:** the upper/return routing was committed and pushed
     as `4ac7ba6aa`. The next candidate settles with ordinary consumables, powder and rest before the upper approach,
     as well as at the launch. T21's first `fire-upper-prepared` observation records the Assassin at 1,511 HP before
     preparation and its full 3,920 afterward, before its first upper walk. This is native recovery, not a setup heal.
     A working tactics choice for Sorcerer, Spirit Master and Ranger allows finishing a target at 25% HP or below
     against exactly two attackers while own HP is at least 75% and a legal attack is ready. It applies outside a
     group; the ordinary recovery ladder still runs, and lower health, three attackers or no ready attack retain
     the usual retreat. Other classes keep their table. This is an implementation choice under the resumed goal,
     not an answer from the operator on deferring classes for levels.
     T21 finished as an uncaptured seven-class round in the same S1-derived world, with ten-minute starts and each
     deadline set to its start plus 90 minutes. Gladiator has a verified Q24023 receipt at 38 with 335 completed
     quests and no deaths; Templar has one at 38 with 336 quests and one death. Sorcerer killed the first lower
     Guardian and Obscura, then stopped on a blocked approach to the second Obscura after retreating. Spirit Master
     also killed a lower Guardian and Obscura before its time limit. Assassin descended seven times, killed both
     kinds of spirit as well as Guardians and Obscuras across its attempts, and reached its time limit with 12
     deaths. Ranger killed Guardians and Obscuras but stopped on safe clearing; Chanter stopped after a retreat,
     despite its prior complete runs. Those outcomes do not establish class soloability limits.
     The finish decision ran outside a corner twice for Sorcerer, four times for Spirit Master and five times for
     Ranger; Sorcerer's two casts finished its Obscura at 11% then 5% while own HP remained above 98%. The original
     human recording's lower-floor death packets confirm the five planned guards and Bolcan; no extra recorded
     Guardian death was omitted from that clearing sequence. Evidence audits are in
     `run/nl/resume-20261020/t21-final-audit.json`, `t21-finishing-audit.json` and `human-all-lower-deaths.json`.
     The round has no world problems; its owner dropped the disposable schema. Final verification passed: 6,663
     solution tests with 252 existing skips, the unchanged 4,097-site warning inventory and all guards/contracts,
     profiles, navmeshes and both companion server tests; Fast's 115 tests with 98 existing opt-in skips and all
     11 scenario rows; all five functional starter journeys with only the existing D7 first-object-ID difference;
     and n9 with six individually verified home receipts and no deaths. Evidence is under
     `run/nl/resume-20261020/recovery-pair-final/`, with n9 at `nl4-r38-recovery-pair-final-ntc`. No baseline was
     changed or bot unit test added. No retained capture or NL-4/NL-5 completion is claimed. The operator asked
     which classes remain unfinished, without choosing to defer them for levels.

   - **2026-10-21, retained replay and the group pass (in progress):** committed runtime `020222d4b` played
     `nl4-vs-resume-c2` from `nl-vs-s1-compatible`, retaining **`nl-vs-s2-w1`**. Its dump and outcome hashes match
     the manifest. Gladiator's Q24023 completion persists at 38 with 335 quests and no deaths; Cleric's earlier
     completion is carried unchanged. The other six did not complete this replay. Chanter's earlier uncaptured
     clears and Templar's earlier uncaptured clears remain diagnostic evidence, not completions in this world.
     Templar stopped before the descent: anticipatory selection used an upper guard's announced movement endpoint
     inside pull range, while combat used its current reported position outside that range. Three blocked approaches
     at the same position exhausted the route rejoin bound. The bounded wait for that moving
     target remains to be implemented and verified; no new descent recording is needed for this bot-side race.
     Four unfinished characters persist inside Sky Temple Interior. The first group trial exposed the character-list
     identity check rejecting them before native login could handle the expired copy. The candidate scopes this
     permission to field groups, while retaining their usual Morheim and Brusthonin starts. Java's
     `InstanceService.onPlayerLogin` sends an unregistered instance's player to its native exit; T2 observes all four
     at that exit in Morheim, followed by the existing six-second Return to their Slag Bulwark bind. No additional
     bootstrap or setup teleport is needed. The initial group config had incorrectly used the fortress rendezvous;
     it now uses the characters' existing Slag Bulwark obelisk, 700259.
     Q2369's plan is generated by the existing compiler; the first two group rounds play Q2355 and Q2369, without
     companions. T1 captured no world and made no quest progress; its owner dropped the schema. In T2, all six formed
     the group, killed the Servile Kalbak with native credit in every journal, and handed Q2355 in at 33:09.228 world
     game time. All six then collected and handed in Q2369, returned to Slag Bulwark and relogged: six verified
     receipts for both quests, no deaths or world problems, about 2 h 15 min of game time per member. Levels remain
     37–38. This trial captured no world, and its owner dropped the schema. Q2369's drops go to the first eligible member, as Java's
     `QuestService.getQuestDrop` specifies; a member stops taking them at 25. Unequal intermediate counts are not a
     loot-driver failure. The unused recovery bootstrap was removed; only the scoped identity permission and
     generated quest plan are needed. The plan regenerates identically. Final verification passed: 6,663 solution
     tests with 252 existing skips; the unchanged 4,097-site warning inventory and all guards/contracts; profiles,
     navmeshes and both companion server tests; Fast's 115 tests with 98 existing opt-in skips and all 11 scenario
     rows; all five functional starter journeys with only the existing D7 first-object-ID difference; and n9 with
     six individually verified home receipts and no deaths. Evidence is under
     `run/nl/resume-20261020/morheim-group-final/`, with n9 at `nl4-r38-morheim-group-final-ntc`. The final owned
     schema was dropped and the marker was clear before capture. No baseline changed and no bot unit test was added.
     Both captured group waves are recorded under NL-4 above.
     The frozen Bard, Rider and Gunner have the same experience, equipment, inventory and quests. The normal Java
     database update adds their zero PvP-exp field and normalizes 135 old skill-level entries to one; it adds no
     skills. The source snapshot remains immutable. Audits are in `run/nl/resume-20261020/c2-persisted-audit.json`
     and `c2-frozen-classes-audit.json`.
     The prerequisite audit matches Java's `QuestService`, `QuestTemplate.getRequiredConditionCount` and
     `XMLStartCondition`: Q2371 needs Q24023 (or legacy Q2035, which nobody holds), so only the retained Cleric and
     Gladiator can currently take it. Q2378 needs Q2373 plus all fifteen completions of repeatable Q2374; Java tests
     a repeatable prerequisite against its maximum count. Six have played Q2374 once and the other two have not
     played Fire Temple. M10 specified playing it once for that phase; whether NL-4 should complete the remaining
     prerequisite repeats or record Q2378 as locked was asked of the operator and remains open. The nine group quests' quest data and handler rows
     match Java 4.8. Evidence is `group-java-data-audit.json` and `nl-vs-s2-w1-group-prerequisites.json` in the same
     audit folder. Refreshed through `nl-gm-s2-w1`, the provisional NL-5 audit covers 27 retained captures and 191 character-round traces, all
     with observed world entry and HP. Deaths are counted from native self-death packets after an observed alive
     state; completion receipts count revival attempts separately. There are no unclassified death packets. The
     audit excludes uncaptured diagnostics and uses each bot's own end minus start time. Its hashes and events are
     in `retained-death-episodes.json`; it must be refreshed after later captures. Q2371, the Abyss group work, the
     higher-level quests and NL-5 remain open.
     This also corrects two handoff-era counts: the retained original `nl4-vs-c1` trace has 14 Chanter death events,
     rather than the handoff's six, and 21 Gladiator deaths, rather than its twenty completed revivals. The report
     uses the native events and preserves the earlier prose as historical evidence.

   - **Fire recovery diagnostic (2026-10-21, uncommitted candidate):** upper pulls now check the NPC's current
     position before fighting an obstruction identified from its announced movement destination. Lower-floor
     retreats must keep their destinations and every route point on the Fire fighting floor. A blocked recorded
     guard pull returns to ordinary defense and a bounded retry instead of ending the whole quest.
     Trial `nl4-vs-recovery-t1`, from `nl-gm-s2`, completed Q24023 with Sorcerer 37 (nine deaths), Chanter 38 (one)
     and Templar 38 (zero), with verified hand-ins before and after relogging. This is the Sorcerer's first verified
     solo clear. Sorcerer exercised a positive-duration patrol wait and a guard retry; Templar also exercised the
     guard retry. Assassin and Spirit Master reached their 90-minute diagnostic caps with 14 and 12 native deaths.
     Ranger stopped after one death because recovery exhausted its bounded sits. None of these trial outcomes is
     retained yet. Evidence is `run/nl/resume-20261020/fire-recovery-t1-audit.json`; its owned schema was dropped.
     Ranger still owned HP potions and had full MP, but NPC 62376 (212677) kept attacking during the supposedly
     quiet sits. Rest's packet window restarted before every wait, leaving gaps between observations. The next
     candidate keeps a continuous cursor, ignores dead/returned attackers, and logs incoming, accepted and ignored
     attackers. Trial `nl4-vs-recovery-t2` again verified Sorcerer, Chanter and Templar's hand-ins before and after
     relogging (11, zero and zero native deaths). Assassin and Spirit Master reached their diagnostic caps with
     nine and fourteen deaths. Spirit Master's continuous rest window included a hostile cast received before the
     wait and a later hit, exercising the continuous cursor; Ranger's former recovery failure did not recur, but it had no rest-attack
     diagnostic, so that specific failure was not reproduced in isolation. Ranger instead stopped after four
     deaths when an alive retreat returned it behind the cleared guards. The next approach skipped their connecting
     walks and attempted to cross the raised deck. The offline geometry probe reproduces thirteen rejected points
     on that direct path and verifies the recorded connecting corridor from the exact failure position, without
     relaxing the ground checks. The T3 candidate keeps earlier approach points available when rejoining a later
     fight, while preserving the recorded kill evidence. T3 is an uncaptured six-class trial from `nl-gm-s2`.
     Evidence is `fire-recovery-t2-audit.json` and `ranger-t2-ground-probe.log` in the same audit folder; T2's owner
     dropped its schema. No NPC statistics or server behavior was changed,
     and no bot unit test was added. The retained world remains `nl-gm-s2-w1`; NL-4 and NL-5 remain open.
     T3 has now finished: Sorcerer, Chanter and Templar verified Q24023 before and after relogging, with eleven,
     zero and zero native deaths. Assassin and Spirit Master reached their 90-minute caps with nine and fifteen
     deaths. Ranger died during its lower-floor retreat before rejoining the corridor, then stopped during a later
     descent preparation with three deaths. The offline rejoin proof stands; this trial does not demonstrate a
     successful native Ranger retry at that position. Evidence is `fire-recovery-t3-audit.json`, tied to the
     candidate source and DLL hashes in `fire-recovery-final/candidate-t3-build-identity.json`. T3's owner dropped
     its schema and the run marker is clear. Only Assassin, Spirit Master and Ranger still lack any verified solo
     Q24023 clear at that point; this was not a finding that those classes cannot solo it. The operator was asked whether the
     first two should play the remaining group quests before another solo attempt; that choice remains open.
     Final verification passed for these runtime sources: 6,663 solution tests with 252 existing skips; the unchanged
     4,097-site warning inventory and all guards/contracts; 27 generated profiles, baked navmeshes and both companion
     server tests; Fast's 115 tests with 98 existing skips and all eleven scenario rows; all five functional starter
     journeys, each with only the existing D7 first-object-ID difference; and n9 with six individually verified
     home receipts, native gate/General death evidence for all five fighters and no player deaths. The final n9
     schema was dropped and the marker was clear. Logs are under `fire-recovery-final/checks/` in the audit folder;
     the n9 round is `nl4-r38-fire-recovery-final-ntc`. Runtime sources still match T3's recorded hashes. No baseline
     changed and no bot unit test was added. The next capture must rebuild the committed runtime first.
   - **Abyss group arrival preparation (2026-10-21):** use the existing empty `rw` travel contract from
     `nl-vs-s3`, with all eight, ten-minute starts and Cleric companions. Java's native login sends the two expired
     Sky Temple attempts to Morheim; the identity permission is scoped to this transit leg. T1 exposed the empty
     travel leg returning to its old Brusthonin hub instead of its Reshanta endpoint. T2 selected the right map but
     attempted the long approach to the floating gate on foot. The candidate continues a travel-only leg from its
     current trip map and uses that map's hub flights before selecting takeoff ground. T3 took both normal flights
     to Morheim Fortress, then received `STR_FLYING_FORBIDDEN_HERE` at the airline landing point. That point is inside
     Java's FLY polygon; `onFlyTeleportEnd` queues zone revalidation on `ZoneUpdateService`'s 500 ms cadence. The next
     candidate allows one second for landing to settle before further travel. T3 was stopped for diagnosis by
     terminating only its owned testhost; the runner dropped its schema and cleared its marker. T4 is an uncaptured
     trial of the combined candidate. No transit capture, group quest progress, server change or new bot unit test
     is claimed by these preparation runs. The retained world remains `nl-vs-s3-w1`.
     T4 verified seven originals in Reshanta before and after relogging, including Spirit Master's native return from
     the expired Temple copy. Assassin died during an approach, then the flight wait continued from its revival
     position outside the selected flight zone. The candidate now stops a flight when its defended wait fails,
     changes map or revives, and returns a refused optional flight for replanning. Three companion seats also stopped
     on the existing generic quit-destination error. The existing SIM and live session helpers now record the actual
     quit mode/value and retry a normal logout's mode-0 refusal with a fresh countdown, bounded to six requests;
     unexpected destinations still fail. T5 verified all eight originals at the Reshanta endpoint before and after
     relogging. Its trace confirms normal logout refusals of ten, then seven seconds, followed by accepted mode 1
     after fresh countdowns. Two seats later reached `CompanionService`'s existing 30-second dismissal cutoff and
     lost their connections. The candidate SIM round now reconnects a seat after completed play when that connection
     ends, using the live host's existing 15-second recovery interval; unrelated play failures remain failures.
     T6 is an uncaptured trial of the complete player-and-seat lifecycle. T4 and T5's owners dropped their schemas;
     none of these diagnostics replaces the retained world or adds bot unit tests.
     T6 has now finished: all eight verified Reshanta arrival and relogging, with two ordinary hub flights each.
     Assassin had one native death and completed after recovery; the other seven had none. All companion seats
     ended as reached or unused. One seat recovered four post-play disconnects and observed eleven normal logout
     refusals before subsequent successful quits or reconnections. The owner dropped its schema and cleared the
     marker. Evidence is `rw-transit-t6-audit.json` and `rw-transit-t6-build-identity.json` in the audit folder.
     The existing generator now adds Templar's level-38 companion tier from `nl-vs-s3-w1`, retaining its level-37
     tier. All 27 existing profile hashes are unchanged; the fresh-generation check validates 28 profiles. The
     existing profile-count assertion is updated; no test method was added. Its appearance assertions now accept
     both the old `jaw_height`/`chin_jut` columns and the current `chin_height`/`cheek_bones` columns, checking the
     same semantic getters that the existing profile loader maps them to. All class rotations remain reviewed
     through the actual levels, 37–38.
     Fresh verification passed: 6,663 solution tests with 252 existing skips; the unchanged 4,097-site warning
     inventory and all guards/contracts; 28 generated profiles, baked navmeshes and both companion server tests;
     Fast's 115 tests with 98 existing skips and all eleven scenario rows; all five functional starter journeys,
     each with the existing D7 first-object-ID difference; and n9 with six individually verified home receipts,
     native gate/General death evidence for all five fighters and zero player deaths. The n9 owner dropped its
     schema and the marker is clear. Logs are under `abyss-transit-final/checks/`; n9 is
     `nl4-r38-abyss-transit-final-ntc`. All seven transit runtime source hashes still match the passed T6 trial.
     A fresh saved-SQL audit, `abyss-group-s3-scope.json`, also corrects the remaining Abyss scope: all eight already
     have Q2753 COMPLETE, and only Ranger still needs Q2754. Assassin, Sorcerer, Chanter and Ranger need Q2762–63;
     all eight need Q2728. These are existing completions, not progress claimed for the transit trial.
   - **Abyss field-group travel (2026-10-21):** the field stage now accepts a retained Reshanta arrival
     and loads the Abyss group plans made by the existing quest compiler. First uncaptured Ladacus trial
     `nl4-group-abyss-ladacus-t1`, from `nl-rw-s2`, formed and bound the six at Primum, then spent CPU on full-route
     height searches toward Peroniad, 2.7 km away, without advancing game time. Only its verified testhost was
     stopped; its owner dropped its disposable schema. The candidate bounds those searches by one flight bar,
     tries rings, and uses the solo leg's checked island stones when a direct flight cannot fit. F1 names each
     landing and gathers the group there. T2 ended normally with all six stopped after finding no first stone;
     its owner also dropped its schema. The offline geometry explains that refusal: the obelisk has a roof,
     and the nearest open same-island point is 61.77 m away, reached by an 88-point checked ground path. The earlier
     retained Primum trace also walked down from the obelisk before flying. The next candidate walks this island's
     checked ground to open sky before planning the crossing. New native proof and commit checks remain pending;
     no group quest completion, new retained world, server change or bot unit test is claimed by these trials.
     Evidence: `group-abyss-ladacus-t1-build-identity.json`, `group-abyss-ladacus-t2-build-identity.json` and
     `group-abyss-primum-offline-island.log` in the audit folder.
     T3 verified the 88-point walk-out and the first hop in the native run, then refused the next crossing. Its
     six bots stopped normally and the owner dropped its schema. The candidate's six-metres-per-second planning
     bound was still used after takeoff, although the first planned 240.14 m flight completed in about 19.16 s.
     As the solo leg does, the next candidate uses the last speed announced at takeoff and logs that speed with
     the observed FP and planned distance. This changes the bot's reach estimate, not the server's flight rules.
     T4 verified takeoff speed 12.51 m/s and several further hops, then refused Peroniad's final approach: 62 FP
     from an observed 70 would leave eight, below the unchanged ten-FP reserve. The six stopped normally and the
     owner dropped its schema. Saved SQL shows no Greater Wind Serum on any of the eight; that supply had only
     been scoped to the solo `rt`/`ro` legs. The group candidate now reuses the approved wind supplies on Abyss field
     crossings, within the same per-leg limits (20 serums and ten scrolls). It stocks four serums when needed and
     reads the greater flight-speed scroll on the ground. If a checked flight still needs a serum, it drinks while
     airborne only when the whole 36 FP restore fits and the 54-second reuse has passed, checking consumption and
     observed FP afterwards. Java's item 162000026 uses skill 9911 at level three, with the 12-per-level restore
     and 54-second item reuse; no server rule or item data changed. T5's native proof and fresh commit checks are pending.
     T5 verified all six accepting Q2728 from Peroniad, and native wind stock plus the scroll raising takeoff speed
     from 12.51 to 15.21 m/s. It did not exercise a serum drink. The next trip, toward Ladacus, found no ordinary
     stone; the group wrapper was still missing the solo leg's serum-extended fallback when that search returned
     none. Only its verified testhost was stopped, and the owner dropped the schema. T6 adds that fallback with
     owned-serum FP budgets and the same checked routes and ten-FP landing reserve. Native quest completion and
     fresh commit checks remain pending; the retained world is still `nl-rw-s2-w1`.
     T6 completed Q2728 for all six, verified after relogging at Primum, in 30.50–30.53 own game minutes and with
     zero native deaths. All levels remain 37–38. The native trace verifies the walk-out, intermediate hops,
     takeoff speeds, scoped wind stock and scroll, serum-extended route planning, and a return route through ring
     `RESHANTA_400010000_4`. No serum-drink event occurred, so the new consumption branch remains unexercised.
     All source and runtime identity hashes verify; the owner dropped its schema and the marker is clear.
     Evidence is `group-abyss-ladacus-t6-audit.json` and `group-abyss-ladacus-t6-build-identity.json` in the audit
     folder. This is an uncaptured trial; a retained group completion still needs the fresh checks, committed
     runtime rebuild and captured round.
     Final verification passed for this runtime: 6,663 solution tests with 252 existing skips, the unchanged
     4,097-site warning inventory and all guards/contracts, 28 generated profiles, baked navmeshes and both companion
     server tests; Fast's 115 tests with 98 existing skips and all eleven scenario rows; all five functional starter
     journeys with the existing D7 first-object-ID difference; and n9 with six individually verified home receipts,
     gate/General native death evidence for all five fighters and zero player deaths. Its owner dropped the schema
     and the marker is clear. Logs are under `group-abyss-final/checks/`; n9 is `nl4-r38-group-abyss-final-ntc`.
     All four bot source files and five compiled plan hashes still match T6. No bot unit test or baseline was added.

## Loop prompt

```
Work the NL checklist in docs/natural-leg-36.md, one step per iteration, through NL-5. Repository
C:\Users\ryanf\Documents\GitHub\BeyondAionSharp, existing checkout on main. Read CLAUDE.md, docs/natural-leg-36.md (its
rule NL-a, steps and decisions), docs/natural-morheim-leveling.md, docs/natural-muster.md and docs/companion-bots.md first.
Each iteration: orient (git status, the step list, scripts/sim/sim-run-marker.ps1, running background tasks); never build
while a run holds the build outputs; take the first open step; small change, build, uncaptured trial round, then the
captured round on committed code; the checks of CLAUDE.md before each commit (unit suite, warning baseline, neutral gate on
the five starter scopes and one replay of run/ntc/n9.json when shared bot code changed; make-companion-profiles.py --check
when profiles change); a dated line under the step, tick it when a round shows it, commit code then document by path with
no attribution line. Push verified path-specific commits as clarified in decision 17; never branch, no worktree; leave the three docs/playtest-*.md files alone; no hand-edit of
generated data; never raise the warning baseline; SIM env AION_SIM_DB_CONTAINER=aion-sim-mysql AION_SIM_DB_PORT=3307
AION_BOT_DASHBOARD_PORT=17881. Look a stuck quest up online (aioncodex /48/) before recording it as a boundary. STOP when
NL-5 is ticked, or a step has failed three iterations running without progress, or a decision is needed that the document
does not give; on stopping send one push notification.
```
