# Natural leg to 36: what was left, Brusthonin, the first Abyss quests and the check-in at 36

Status: being played, 2026-10-17: NL-0 and NL-1 done; the world is `nl-g31-s2-w1`. The plan was written the same day. The operator, the same day, after the level-31 profiles:

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
  - 2026-10-17 (`ed7ab9b15`): leg `mg` took the eight from `morheim-ft-s1` back to the fortress, bound there, with
    their Cleric companions (`nl-mg-s1`). The first check-in from it (`nl1-g31`) stopped the Ranger: the coin check
    bought the level-31 Elite Rank 6 jerkin over its level-35 Flame Tunic on its higher grade and defence, and the
    gear rules, which rank armor by item level first, never wore it. The coin check now ranks a piece by its level
    alone, as the gear rules wear it. Trial `nl1-g31t2` and captured round `nl1-g31c2` from `nl-mg-s1`: all eight
    reached, each with its second stone socketed (bought in Pandaemonium) and 0 to 3 Elite Rank 6 pieces bought where
    one beat a worn piece (most wear level-35 Fire Temple pieces). The world is now `nl-g31-s2-w1`.
- [ ] **NL-2 Brusthonin.** Q2330 and Q4300 from the fortress, the fortress teleporter (204399) to Brusthonin, the settlers
  area's quests (the hand-written ones get their steps), its two group quests in a group, and any quest of the area that
  sends a bot back to Morheim. The quest list first, made by `scripts/sim/zone-quest-list.py`.
- [ ] **NL-3 The first Abyss quests (25 to 33).** The way into Reshanta, its flying (islands, death below 500 m), the Abyss
  campaign from Votan's Orders, the quests the list says play or need steps, and those that open on the way. The quest
  list first.
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
