# Natural leg to 36: what was left, Brusthonin, the first Abyss quests and the check-in at 36

Status: being played, 2026-10-17: NL-0 to NL-2 done; the world is `nl-gb-s2-w1`. The plan was written the same day. The operator, the same day, after the level-31 profiles:

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
- [ ] **NL-3 The first Abyss quests (25 to 33).** The way into Reshanta, its flying (islands, death below 500 m), the Abyss
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
- [ ] **NL-4 Level 36.** Morheim's quests from 31 to 35 for any bot still short of 36 and for the zone's sake; the gear
  check-in at 36 (Elite Rank 5); the level-36 companion profiles; then Q24023 (the leg engine collects inside an
  instance, room by room, and goes down between the Sky Temple Interior's floors as a player does, by a glide or a
  drop), Q2355 and the quests a step left for a higher level.
- [ ] **NL-5 The report.** Per class its level, quests, deaths and time; what was not played and why.

## Decisions

The operator's, 2026-10-17: **Q24024 and Q24025 wait for the next planning phase,** until the bots are a higher level
("we do not want to do Q24024 and Q24025 until the bots are a higher level"); Q24026, which follows them, waits with them.

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
no attribution line. Never push, never branch, no worktree; leave the three docs/playtest-*.md files alone; no hand-edit of
generated data; never raise the warning baseline; SIM env AION_SIM_DB_CONTAINER=aion-sim-mysql AION_SIM_DB_PORT=3307
AION_BOT_DASHBOARD_PORT=17881. Look a stuck quest up online (aioncodex /48/) before recording it as a boundary. STOP when
NL-5 is ticked, or a step has failed three iterations running without progress, or a decision is needed that the document
does not give; on stopping send one push notification.
```
