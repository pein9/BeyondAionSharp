# Natural Ishalgen Journey

Status (2026-09-26): NI-00 through NI-09 are complete in their documented scope.
The integrated NI-07 SIM journey passed smart46 seeds 1/3/4/5: all 41 quests,
level 9 at Munin, zero deaths. NI-08 now rebuilds state from each login and resumes
partial quests on the same character. Final warm reconnect and cold restart
proofs completed all 41 quests with zero deaths, including a pending item reward
and a partial Hatata hunt. All 31 repository checks passed, including Docker Fast.
See [current evidence and retained failures](natural-ishalgen-status.md).
NI-09 isolated LIVE run `ni09-live-a4` completed all 41 quests with zero deaths
in 3h 42m 26s under ordinary rates. A final ordinary relog verified persistence,
level 9 at Munin and Q2008 START/0. Enforce watching and all 31 repository checks,
including Docker Fast, passed. NI-10 retained-world attach and NI-11 real-client
observation remain uncompleted, in that order.

The Phase 10 readiness review was against Java `4.8` at `ce54b7931`, review
commit `7d80e48d6` (before SHA-recording amend). The broader game journey remains
a post-Phase 11 review. Historical NI-07 diagnostics below explain the changes;
their earlier pauses and unproven-path notes are superseded by smart46.

## Goal

Build a bot that creates a character and plays through the game's implemented
progression using ordinary player actions, while detecting and reporting bugs.
Ishalgen is the first continuous area journey toward that goal. In LIVE mode the
bot is an ordinary visible player: the maintainer can encounter it while playing,
follow it, and observe its fights and interactions. Human attendance is optional;
the bot must progress without a spectator, manual instructions, or assistance.

The long-term goal is character creation through a defined endgame completion
milestone, across areas and sessions, including group content where required.
Before implementing that broader journey, define a finite completion contract for
this server revision (progression, campaigns, and selected endgame objectives).
An MMO's repeatable and scheduled activities do not have a universal "finished"
state. Ishalgen is a first acceptance milestone, not a claim of whole-game coverage.

## Content boundary

Use only content already implemented in the referenced Java/C# server revision.
This work adds bot behavior and tests; it does not authorize new server quests,
handlers, spawns, rewards, or content to make a checklist completable. Existing
implementation defects may be diagnosed and fixed through the normal Java-first
parity process. An upstream content limitation remains a recorded boundary.

The accepted scope is frozen at **41 quests** for Java/C# revision `ce54b7931`:

- Campaign/prologue: Q2000–Q2007.
- Other Ishalgen quests: Q2100–Q2106, Q2108–Q2110, Q2112–Q2121,
  Q2123–Q2129, Q2131–Q2135, and Q2137.

This list includes ordinary kill, collection and gathering objectives. The
random-drop exclusion applies only to quest starters. A required quest that fails
during play remains a failure or blocker rather than being silently removed from
the denominator.

The prerequisite chains visible in the shipped quest data are Q2102 → Q2103,
Q2104 → Q2106, Q2109 → Q2135, Q2110 → Q2114, Q2115 → Q2131, and
Q2118 → Q2137 → Q2116 → Q2119. Q2133 is the natural gathering proof: collect
three Young Azpha items at gathering skill 1 for Nobekk.

### Reviewed exclusions

| Quest/content | Classification | Evidence and treatment |
|---|---|---|
| Q2107 | Inactive/unobtainable shipped content | The Rolled Scroll (`182203107`) is required to start the quest, but an exact Java and C# source search finds no drop, reward, vendor or script source. The Q4I director grants it. Do not add a source or count it. |
| Q2122, Q2136 | Random-drop starters | The shipped Ishalgen global-drop rules provide their starter items at 5%. Excluded by the requested boundary; do not suppress their drops. |
| Q2111, Q2130 | Disabled/unused | Both use the shipped level-99 gate. Do not enable them. |
| Q2150, Q2151 | Unimplemented | The shipped script list marks them TODO/no-handler. Do not add handlers for this journey. |
| Q80160 and Q80619/Q80622/Q80644 | Disabled/event content | Outside the selected normal profile. |
| Q2144–Q2147 | Post-boundary content | Level 10/16 quests are beyond the pre-Ascension Ishalgen milestone. |

The quest-reward lower bound for the 41 included quests is 155,234 XP (maximum
156,219 XP), before ordinary kill and gathering XP. The shipped absolute level-9
threshold is 126,069 XP and the level-10 threshold is 182,252 XP. For an online
non-Daeva Priest, `PlayerCommonData.setExp` caps stored XP at 182,252 and displayed
level at 9, even with a full XP bar. Ordinary combat, gathering, and quest rewards
may reach that cap before all 41 quests are complete; this is expected and must
not cause route pruning, XP avoidance, or a false level-10 failure. Route ordering
must still satisfy each quest's level and prerequisite gates. Java `4.8` at
`ce54b7931`: `PlayerCommonData.setExp` and `player_experience_table.xml`; the
C# `PlayerCommonData.SetExp` mirrors this behavior.

Q2008 is automatically added to the journal at level 9. The accepted stop state
is the Priest physically standing at Munin (NPC 203550) in Ishalgen, with Q2008
at START, step/var 0 and no Q2008 dialogue, objectives, class selection, or
Ascension progression. Earlier conversations with Munin for included quests are
allowed and may be required. "Before Munin gives the quest" means before the
first Q2008 interaction, not that Q2008 is absent from the journal.
`ENABLE_SIMPLE_2NDCLASS` must remain off. Java `4.8` at `ce54b7931`:
`_2008Ascension.onLevelChangedEvent` and `AbstractQuestHandler.defaultOnLevelChangedEvent`;
the C# handler agrees on this boundary.

## Current foundation and remaining work

> **Navigation update 2026-09-24.** Route planning now goes through a baked navmesh with mapped
> roads and a travel graph ([bot-navigation.md](bot-navigation.md)). `BotNavigationGeometry`'s path
> methods use it automatically; every step still passes the same ground/collision check and the
> observed-hazard rule. Entries below that say "navigation has no navmesh" describe the grid search
> that preceded it; set `AION_BOT_NAVMESH=0` to reproduce those runs. Long legs go through the
> level-aware travel planner first (`AION_BOT_TRAVEL_PLANNER=0` disables it). Observed monsters
> no longer end an approach: the journey fights through them one pull at a time along the route
> that fights least (`TryFightThroughAsync`). The open problem at Q2007 is now retreat and
> survival against two attackers, not routing.
>
> **Spawn heights, 2026-09-24.** The 2026-09-23 Ishalgen spawn refresh (the aion-portal 5.8 import)
> took most heights from the outdoor terrain heightmap. Under overhangs and in caves that is the
> hill top, not the floor. Nineteen spawns landed 30–57 m above their retail height: Carak and the
> Q2119 Jewel Box in Carak's cave, and seventeen Lycans in the camp near Rae. The Jewel Box sat on
> the hill above the cave, where no walkable route exists, so Q2119 could not be finished. Those
> nineteen spots now use the server-geometry surface nearest their retail height. Carak (259.15) and
> the Jewel Box (260.89) match the Java spawn data exactly. The portal import's height snap needs the
> same rule, or a re-import will undo this: pick the surface nearest the source height, not the
> terrain heightmap.

At the Phase 10 closeout, real client packets, movement timing, combat, gathering,
quest dialogs, persistence, and logging are exercised by scenarios. The missing
layer is a continuous player policy that selects and combines those actions and
recovers when the world does not follow a prescribed sequence. P10-05's deferred
housing/siege/PvP boot tail is unrelated to Ishalgen and does not block this work.

The current [LIVE Q4I runner](../tools/Aion.LiveBots/LiveQuestPlanScenario.cs)
raises the subject to level 50/Gladiator, boosts attack and gathering, teleports to
objectives, supplies item starters, and can complete external prerequisites through
the GM director. Its 27 template completions demonstrate quest execution under
controlled setup; they do not establish natural progression. The present LIVE
character creation also selects Warrior. Keep these focused regression scenarios
useful independently of this later journey.

| Capability needed for continuous play | Readiness | Evidence and remaining gap |
|---|---|---|
| Create an Asmodian Priest normally | Mostly ready | The LIVE session supports an explicit base class and lifecycle tests cover Priest; journey entry points still default to Warrior and need a retained Priest identity. |
| Travel on traversable routes and approach live objects | Partial | Geodata-backed local/journey pathing and checked long-distance LIVE movement exist. Add progress/stuck detection, bounded replanning and moving-target reacquisition. Q2002's legitimate quest teleport to Ataxiar and scripted return now pass in Docker SIM under a narrowly scoped Q2002 START/99 policy exception; this is not an unrestricted map change or LIVE proof. |
| Priest combat and survival | Policy implemented; bounded LIVE proof | NI-04 chooses only client-observed Sprigg Workers, intersects the frozen level-1–9 Priest map with the learned skill list, gates range/MP/group cooldowns, heals, uses owned starter potions, rests, and bounds retreat/revive attempts. Two ordinary LIVE kills and sit/stand recovery passed. Low-HP, consumable, retreat and death branches are policy-tested but not yet induced in LIVE. NI-07's crowded Q2005 area shows that the journey driver still needs attack-aware travel, early multi-attacker retreat and safe pull selection; the policy alone is not a proof of survival there. |
| Gathering | Policy implemented; bounded LIVE quest proof | NI-06 earned level 2 by ordinary Priest combat, accepted Q2133, walked to a client-observed Young Azpha, gathered three items and completed the quest with Nobekk. Failed-use depletion, occupied-node fallback, next-node search and normal respawn wait are policy-tested; this LIVE run had three successes on one node, so those recovery branches remain uninduced LIVE evidence. Cube-pressure handling uses NI-05's sell-only policy. |
| Inventory, equipment, skills and economy | Policy implemented; bounded LIVE sale proof | NI-05 selects class-usable rewards and gear from shipped templates, verifies auto-learned Priest skills from the client, protects all 41 quests' item references plus quest/key items and HP/MP supplies, and sells other sellable items without buying. A one-bot LIVE run sold the starter bandage stack at an active Ishalgen vendor; reward claiming, upgrade equipping and full-inventory recovery remain policy-tested until NI-07 exercises them naturally. Shipped unsellable starter extras cannot be sold. |
| Quest selection and execution | NI-07 SIM complete | smart46 completed all 41 frozen quests on seeds 1/3/4/5, with level 9 at Munin, Q2008 untouched and zero deaths. The historical checkpoints below explain how that implementation developed. Isolated LIVE acceptance is NI-09. |
| Persistence and recovery | NI-08 SIM complete | Each login reconstructs both journals, position, stats, inventory and skills. Same-character warm reconnect and fresh-process restart proofs completed the journey. Stalls and a missing identity retained failure packages. Isolated LIVE acceptance is NI-09. |
| Share a world with a human player | Not ready | Multi-bot contention is proven only in isolated LIVE stacks. Add an explicit attach mode that never owns server/DB lifecycle, then validate visibility with the real client. P10-07's client capture remains deferred. |

Phases 8–10 supplied tested mechanics and diagnostics. They did not deliver
autonomous progression: Phase 10's loop over existing scenarios is not evidence
that a fresh character can progress through an area without setup assistance.
Phase 11 is not a prerequisite for this single-character starter-zone journey;
its group/scheduled-content work matters to the later whole-game goal.

## Ordered implementation path

These are new journey TODOs, not retroactive claims about the phase scenarios:

- [x] **NI-00 — Contract fixture.** Encode the 41 quest IDs, exclusions,
  prerequisites, XP/level gates, Q2133 source and Q2008 stop state as a reviewed,
  machine-checked journey contract. The fixture is
  `parity-artifacts/e2e/natural-ishalgen-contract.json`; focused tests verify it
  against shipped static data and the Ascension handler. (`9f4fe836b`, before
  SHA-recording amend)
- [x] **NI-01 — Natural Priest identity.** Create one access-level-0 Asmodian
  Priest through normal packets and retain the same account/character across runs.
  `NI-01` uses stable account `niishalgen` and character `Ishalgenbot`, creates
  only on an empty account, strictly reuses the same character ID, never deletes
  it, and rejects conflicting or post-boundary state. (`9c02d16ce`, before
  SHA-recording amend)
- [x] **NI-02 — Decision loop and trace.** The deterministic scheduler reads the
  retained Priest's client-observed map, level and quest journals; evaluates all
  41 frozen quests with level/prerequisite/active/completed checks; chooses the
  next eligible work in stable order; and records the full rule tree in bot trace
  and the live dashboard. Client synchronization retries are bounded at two.
  It stops on crossed Ascension/map boundaries and reports unavailable natural
  gameplay actions as `awaiting-capability`, not as completed quests. The NI-02
  LIVE run has no GM gameplay inputs. (`0ed2068407`, before SHA-recording amend)
- [x] **NI-03 — Resilient Ishalgen navigation.** A reusable bounded policy follows
  speed-paced, collision-checked graph/local/journey paths in short segments,
  synchronizes client packets, monitors client-estimated progress, replans after
  movement deviation, and reacquires moving/replaced NPCs by their observed
  template and object IDs. Each route and failure records map, positions, target,
  segment/search budgets and checked route points in the bot trace; navigation
  decisions appear in the dashboard. A one-bot enforced Docker LIVE run walked
  the ordinary Priest from spawn to packet-observed Asak for Q2101 and stopped
  before dialogue. The static approach anchor comes from shipped spawn waypoints,
  not a hand-authored route. Java does not echo the mover's own `CM_MOVE`, so
  segment progress is a client estimate, not continuous server confirmation;
  interaction and later persistence checks remain separate proofs. (`ced820ba05`,
  before SHA-recording amend)
- [x] **NI-04 — Priest combat and survival.** Implement natural target selection,
  learned-skill use, range/cast/cooldown/MP handling, self-healing, rest,
  consumables, retreat and death/revive recovery. The deterministic level-1–9
  policy is gated by the client's learned-skill list, uses independent cooldown
  groups, reserves healing mana, and records each rule check in trace/dashboard.
  No Priest follow-up chain skill is learnable before Ascension: Smite,
  Hallowed Strike and Infernal Blaze are openers. Follow-ups require an observed
  matching opening and their own cooldown; later-class rotation is outside this
  milestone. A one-bot Docker LIVE run `ni04-20260922-b` completed two normal
  Sprigg Worker kills with +80 XP each and packet-confirmed sit/stand pauses
  after kills; it did not need to heal, use potions, retreat or die. Those
  survival branches are implemented and policy-tested, not claimed LIVE-proven.
  The bot does not yet decode login-time `SM_ITEM_COOLDOWN`; it conservatively
  holds both shared-delay starter potions for 30 seconds after reentry.
  Java specification: `ce54b7931` `ChainCondition`, `ChainSkills`, `Skill`,
  `CM_CASTSPELL`, `CM_USE_ITEM`, `skill_tree.xml`, and `skill_templates.xml`.
  (`93dab2180`, before SHA-recording amend)
- [x] **NI-05 — Inventory and character growth.** Choose the best Priest-usable
  reward (or highest sale value when no upgrade exists), equip only mace and
  cloth/leather upgrades, reserve quest/key items and the two combat potions,
  and sell junk, unusable gear and other sellable surplus. At NI-05 completion
  there was no buy path; the later paused-NI-07 survivability addition adds one
  for shipped Minor Life Elixirs only.
  Priest level-1–9 skills are auto-learned by the server on create/level-up;
  the bot checks the client-observed skill list rather than buying books or
  granting skills. Packet-derived cube occupancy retains three slots for quest
  and gathering work; protected/unsellable pressure blocks instead of dropping
  items. A one-bot Docker LIVE run `ni05-20260922-b` walked to shipped vendor
  Ungfu using bounded collision-checked waypoint hops and sold 20 starter
  bandages (kinah 1,000 → 1,020; cube free slots 18 → 19), with no purchase or
  GM action. The initial direct route failed because the sparse graph and
  bounded long-distance search could not connect spawn to the vendor; the
  checked hop fallback resolved that route. The 10 selectable-reward decisions,
  equipment choices and full-cube branch are policy-tested, not yet claimed
  LIVE-proven; NI-07 integrates them with real quest rewards. Shipped items
  without a sellable mask, including several starter extras, are retained and
  explained. Java specification: `ce54b7931` `SkillLearnService`,
  `PlayerSkillList`, `Equipment`, `ItemTemplate`, `ItemMask`, `QuestService`,
  `CM_EQUIP_ITEM`, `CM_BUY_ITEM`, `TradeService`, `ItemGroup` and
  `ItemQuality`. (`b084e1270`, before SHA-recording amend)
- [x] **NI-06 — Natural gathering.** Integrated Q2133 with ordinary Priest
  level-2 combat, checked movement to Nobekk and client-observed Azpha nodes,
  ordinary-rate gathering, sell-only cube recovery, normal quest dialogue and
  packet-confirmed completion. Java `ce54b7931` `GatheringTask` and
  `GatherableController` consume a node use on success or failure; the bounded
  policy tries the next reachable node before waiting for the 295-second
  respawn. Focused policy tests cover failure, occupancy, depletion and an
  unseen spawn hint. One-bot Docker-only LIVE `ni06-live-a` passed with three
  successes on one node and no retained watcher fingerprint (447 seconds);
  this does not claim LIVE failure/respawn induction or the 41-quest journey.
  (`620a93363`, before SHA-recording amend)
- [x] **NI-07 — Forty-one-quest execution.** Support the custom campaigns and
  quest-specific operations omitted by Q4I, then complete the frozen contract in
  SIM without grants, forced state, setup teleports or boosted stats. Allow the
  ordinary level-9 XP cap while finishing every included quest, then end at Munin
  before any Q2008 interaction. **Completed by smart46; historical development
  notes follow.** An earlier Docker-backed focused SIM test
  creates a Priest and completes Q2000–Q2005/Q2100–Q2104/Q2132 naturally on the same
  character, including ordinary Priest combat kills, post-kill rests, object loot,
  class-appropriate rewards, Q2002's quest-driven Ataxiar visit and return,
  Q2003's guardian drops, Q2132's auto-learned skill turn-in, and Q2004's
  tombstone guardian, 80% cube drop, Munin visit and final Derot reward. Munin's
  exact spawn point is collision-blocked in the checked-in geodata; the route
  finds checked ground inside the ordinary three-metre interaction radius.
  A first Fast-tier run exposed a Q2003 death after insufficient inter-pull
  recovery; the bot now waits, with a two-minute bound, for at least 90% HP and
  80% MP before these pulls. The focused replay and subsequent Fast tier pass.
  The decision engine permits that temporary map only with Q2002 START/99.
  Java `ce54b7931` `_2002WheresRae`, `_2003TreasureOfTheDeceased`,
  `_2004ACharmedCube`, `_2132ANewSkill`, `ActionItemNpcAI` and
  `QuestItemNpcAI` specify these quest operations.
  The decision engine now requires client-observed proximity to Munin before it
  can report `journey-complete`; journal state alone is insufficient. Neither
  the twelve-quest checkpoint nor the endpoint rule satisfies the 41-quest TODO.
  Q2005 work exposed a further natural-play requirement: its Stalkers include a
  northern walker and southern static spawns surrounded by aggressive Lycans.
  A Priest with the level-8 auto-learned skill can still be overwhelmed by
  multiple attackers; self-healing alone does not make a crowded pull safe.
  NI-07 must use client-visible attack/position evidence to avoid unsafe routes,
  wait for a separated patrol or choose another shipped target, and retreat
  early when a pull draws multiple attackers. A spawn coordinate is a search
  hint, never proof of a live or safely reachable quest target. The Q2005 replay
  also found no hazard-free geometry route through the observed camp around
  (799,1530), even when replanned from Mijou; this is not the Q2002 Rae route.
  Bounded ordinary fights advanced the Priest to (711,1430), where a Stalker
  was client-visible about 21 metres away. The driver now prefers such a
  visible in-range quest target to a distant spawn hint. At that point, the
  pull and Q2005 turn-in were not yet proven by a passing replay.
  Ishalgen spawn refresh `1856203e5` changed the rehearsal baseline (including
  co-located Sprigg Worker/Guard spawns and the Stalker search areas). A bounded
  Docker SIM replay with `NI07_STOP_ON_DEATH=1` now selects unpaired,
  client-observed Sprigg Workers from spell range, holds range during short
  cooldowns, and again passes Q2002–Q2004 without a death. Q2005 still reaches
  the hostile corridor around (799,1530), where the client-observed Lycan aggro
  circles block a checked route to the Stalkers. Trying alternate shipped areas
  exhausted the eight-minute focused-test bound; this is not quest progress or
  evidence that the areas are unreachable. A short route/line-of-sight probe
  found checked ground within Priest spell range of individual corridor mobs.
  A subsequent stop-on-death Docker SIM replay used those short flanks to clear
  two blockers and naturally kill and loot one client-observed Q2005 Stalker,
  without a death. It later failed when an A* search from the northern area to
  the distant western search hint returned no checked staging path. The driver
  now treats that search failure as one bounded area miss and prefers the
  previously successful Stalker area after its ordinary respawn, falling back
  to other shipped areas after two misses. A second bounded replay reached the
  eastern Stalker area without dying, then could not find a distant reverse
  route to Mijou. The return now retraces recorded ingress checkpoints in
  reverse, replanning every leg against current client-observed hazards. A third
  stop-on-death replay showed that a Lycan can move onto an ingress checkpoint:
  the bot correctly rejected that leg. It may now skip up to four such blocked
  checkpoints only when a farther recorded point has a fresh checked route;
  focused tests prove reverse order, safe skip and bounded failure. A fourth
  replay then found an unsafe Stalker staging point where the driver invoked
  long-distance combat retreat despite having no client-observed attack. That
  branch now distinguishes merely nearby hostiles from actual attack packets:
  it retraces checked ingress while unengaged and reserves combat escape for
  attackers. A later stop-on-death replay caught a level-8 death during the
  third Q2005 Stalker approach near Mijou at (939.253,1693.775,260.214),
  after the refreshed spawns. That run held decoded packets only in memory.
  A focused stop-on-death replay with the new combat trace reproduced the
  same location and identified the cause: one level-6 Fanged Karnif (210389)
  pursued the Priest back to Mijou. The driver advanced a 181-second Stalker
  respawn wait as one unobserved block; the next synchronization delivered
  19 Karnif strikes, taking 537 HP to zero without a heal or counterattack.
  The wait now advances at most two virtual seconds per observation and
  defends against client-observed attackers. A focused replay passed the
  prior death point and began a fifth Stalker search attempt without dying, but
  hit its eight-minute wall-clock bound in route search; this does not prove
  Q2005 completion or that the defensive branch itself fired. The journey
  planner now tries checked, hazard-safe forward waypoints before expensive
  distant A*, and the Stalker search stops within 23 m of a shipped area hint
  instead of requiring a complete route to the hint's exact point. Navigation
  decisions and slow global-search starts are retained in the packet trace for
  diagnosis. The first replay after the forward-waypoint change found a new
  dead-end at (620.32,2439.20,280.73) on the ordinary Q2002 Dabi route:
  its closer waypoint had no checked continuation. Unobstructed travel now
  tries a complete checked route before the forward-waypoint shortcut.
  The next Docker-backed replay reached Dabi and packet-confirmed Q2005's
  ordinary turn-in (`SM_QUEST_ACTION` status 5) without another death. It then
  approached Q2006's first Mau Grain Sack, but a seven-hazard route search
  from (819.976,1548.345,278.999) exceeded the eight-minute focused-test
  bound. The navigator now attempts a checked, hazard-safe local waypoint
  even at short range before that expensive global search; this adjustment
  has not yet been replayed. The trace shows the nearest sack at
  (742.801,1515.770) is about five metres from a level-8 Dundu Highsitter.
  Its shipped `srange=15` is the aggro range; `arange=37` is attack range.
  An exact hazard-free approach to that sack is
  impossible from outside the circle, so geometry now rejects such goals
  before exhaustive search and the bot tries another shipped sack instead.
  The impossible-goal check is unit-tested; alternate-sack selection is
  compiled but not replay-proven. A subsequent focused Docker replay with
  the updated spawn layout stopped earlier, after its third Q2005 Stalker
  approach: the checked return from (723.976,1484.345) toward Mijou could
  not bypass five blocked ingress checkpoints in a 30-hazard field. The
  failure and packet history are in
  `run/ni07-combat/20260923T210333679Z-2adb87c2e04b48309f263b7a7acb0abb.trace.jsonl`.
  The return planner now examines all recorded ingress checkpoints instead of
  stopping after four blocked ones. If no checked safe leg exists, the journey
  can select a client-observed hostile intersecting the objective corridor,
  fight it with ordinary Priest combat, and replan; the same bounded clearing
  approach is wired to blocked grain sacks and Young Azpha gathering routes.
  A Q2006-focused Docker replay reached its first grain sack and selected three
  client-observed Mau guards for ordinary combat. A newly observed hostile then
  blocked the checked approach to a moving guard, so the replay stopped without
  grain proof (`run/ni07-combat/20260923T214554169Z-beed1c8dbf4d46a39df2117e47929e07.trace.jsonl`).
  That specific blocked approach is now recoverable by rejecting
  that guard and trying another observed guard or shipped sack; the recovery
  has not been replay-proven. A Q2005-only
  Docker replay with the revised 70% heal / 30% retreat policy passed in six
  minutes: Mijou sent `SM_QUEST_ACTION` Q2005 status 5 and no death appeared in
  `run/ni07-combat/20260923T212424658Z-97fdfcebdb9b4c51b00a1db08c5aa935.trace.jsonl`.
  It recovered from a blocked Stalker search without needing a guard-clearing
  fight, so that path and Q2006 remain unproven. The immediately preceding
  Q2005-only replay was stopped after the user changed the combat thresholds;
  it is not acceptance evidence. A later focused Q2006 replay stopped in Q2002
  when the bot selected a server-ready Smite before its client-side 350 ms cast
  interval had elapsed. The combat driver now waits a short slice and re-observes
  HP and attackers before retrying. The next Q2006 replay passed Q2002 and
  reached the first sack, proving the cast gate no longer aborts that checkpoint.
  A subsequent guard-recovery replay stopped earlier in Q2005: a roaming hostile
  attacked while the Priest sat through repeated ten-second rest intervals, and
  HP fell to 29/537
  (`run/ni07-combat/20260923T215440120Z-05e0be673f6e41ecb98b3e271034fcff.trace.jsonl`).
  Rest now synchronizes every two seconds, stands immediately
  on an observed attack, and invokes ordinary fight/retreat defense. Its cadence
  is unit-tested (including death and stand-before-defense ordering), but the
  integration has not yet been replay-proven; Q2006 guard recovery also remains
  unproven.
  A later Q2006-focused checkpoint stopped in Q2004 when the 30% HP retreat
  rule correctly triggered against Munin's cube target, but that fight had no
  safe retreat anchor. The Q2004 caller now records Munin as a refuge and
  approaches observed targets at Priest spell range instead of melee range.
  A Q2004-only Docker SIM checkpoint then passed: three targets were selected
  at 18.6–22.9 m, the server advanced Q2004 to step 6, and sent final status 5
  with no death (`run/ni07-combat/20260923T222351772Z-b5e0f49d7cb943ac8b0e8a513c00a857.trace.jsonl`).
  The next Q2006-focused run again completed Q2005, selected the mapped road
  route from Mijou with one observed hazard, fought through the first guarded
  sack approach, and received one `SM_INVENTORY_ADD_ITEM` for Mau Grain
  `182203008`. It then moved to a second sack but exhausted 24 combat turns
  on a guard without a kill (`run/ni07-combat/20260923T222952753Z-f86a1c8f2b494829b610d441ccabc3dc.trace.jsonl`).
  The trace shows repeated zero-progress standoff arrivals: the prior point
  choice plus the navigator's 3 m arrival tolerance could leave the guard
  outside 25 m spell range. Standoff selection now requires a checked point
  at most 21 m from the target, and every combat choice is written to the
  dashboard/packet trace; this correction is unit-tested but not replay-proven.
  A later Q2006 checkpoint completed Q2005 but exhausted its 12-minute budget
  while routing among the first sack's guarded approaches
  (`run/ni07-combat/20260923T224217677Z-a959f8f2faee4778b2b6c2ac22e51e9b.trace.jsonl`).
  It did not reach a completed Q2006 or replay-prove the standoff correction.
  The trace exposed an overly strict guard approach: it demanded a route to
  the guard's occupied position before selecting a Priest casting point.
  Combat approach now searches checked ground within 20 m with line of sight,
  keeping other observed aggro circles forbidden. It moves at most six checked
  points before re-observing HP and attackers, rather than treating the pulled
  guard's own 15 m detection circle as a forbidden corridor. A focused geodata test
  passes for that exact Mau guard/hazard snapshot and the short-segment policy
  is unit-tested. A subsequent Q2006-focused Docker SIM checkpoint exercised
  two `combat-ranged-route` decisions, survived, and acquired all three Mau
  Grain through ordinary sack use and client-observed inventory packets. It
  then failed to find a checked return from the third sack at (680.5,1504.1)
  to Mijou with 39 observed hostile circles
  (`run/ni07-combat/20260923T230531611Z-8c2de0d4717a4890aba38d3f43bac4e2.trace.jsonl`).
  The return leg now makes bounded ordinary attempts to clear observed guards
  obstructing that corridor, rechecking the route after each kill. A focused
  geodata test finds a visible, checked spell-range approach to the first
  corridor guard from the failed position. Another focused test finds a
  collision-checked road route from the third sack to Mijou when observed aggro
  circles are removed, isolating the obstruction to hostile avoidance rather
  than a terrain gap. The return now first retraces thinned, actually walked
  first-sack ingress checkpoints (26 segment observations produced 14 candidate
  checkpoints in the failed trace), rechecking each leg; guarded-corridor
  fighting remains the fallback. Neither return path is replay-proven; Q2006
  is not complete.
  Navigation has no navmesh: it uses shipped spawn/patrol waypoints and
  collision-checked geodata search. The eastern Derot route has explicit
  road-side NPC anchors. A first soft road-cost preference now uses a calibrated
  centerline from Mijou toward the Mau farms on longer Q2006 travel, then falls back
  to the ordinary checked route if the road search is unavailable. Combat approaches
  and emergency retreats do not take this scenic preference. A focused
  Docker geodata probe passed from Mijou to a shipped sack, and synthetic tests
  prove road preference still avoids observed aggro circles. The Q2006 replay
  selected a 228-point checked road route in the presence of one observed
  hazard. Further Ishalgen road corridors
  remain to be mapped and verified. The extracted
  client Ishalgen map and its calibration are available in the sibling
  `aion-portal/assets/maps/220010000-ishalgen-map.webp` and `manifest.json`
  (`offsetX=740`, `offsetY=0`, `mapWidth=mapHeight=2300`). Visual road pixels
  are not a walkability mesh; any road-derived route must still pass ordinary
  terrain/collision checks and observed-hostile handling.
  Q2006's alternate-sack logic was exercised after the first grain, but the
  quest still has no completion proof. Focused runs
  retain a filtered, flush-on-write client/server
  combat packet trace under ignored `run/ni07-combat/` (or
  `AION_NI07_COMBAT_DIR`), with a death snapshot and trace path in the failure.
  This is packet evidence, not the client's rendered chat text. The Priest now heals
  at or below 70% HP in combat and chooses retreat at or below 30% HP while
  fighting an engaged target or observed aggressor, regardless of attacker count;
  segment synchronization now routes
  client-observed incoming attacks through the Priest combat policy, though
  that travel reflex has not yet been induced in a replay. Q2005's three
  ordinary Stalker kills, three Odella and turn-in are now proven in one SIM
  run; this is not completion of NI-07. The Q2006 campaign path is staged from
  Java `ce54b7931` `_2006HitThemWhereitHurts`, `QuestItemNpcAI`, and the
  shipped quest/spawn data: three ordinary Mau Grain Sack uses/loots, Mijou's
  item check, and Ulgorn's Priest reward. The staged interaction now waits for
  the client's three-second quest-loot bar before opening the corpse list,
  matching Java `ActionItemNpcAI` and the sack template's talk delay; it does
  not rest between sacks inside the hostile field. An earlier approach timed
  out before interaction; the later checkpoint earned all three grain but
  could not return to Mijou. A subsequent bounded stop-on-death replay ended
  earlier, around `(784,1513)`, after twelve shipped sack hints had no
  hazard-checked route through the observed Mau pack; it did not die or reach
  the new return-path code. Thus the three-grain result remains from the
  previous replay, not that one. A later stop-on-death checkpoint again earned
  all three grain and then successfully retraced fifteen collision-checked
  first-sack ingress checkpoints to Mijou; it advanced Q2006 to reward status
  without a death. It then followed a broad checked route toward Ulgorn that
  ended around `(765,1783)` with no remaining checked continuation, so the
  final reward and Q2006 completion remain unproven. The driver now also
  remembers only positions actually walked along the earlier eastern road
  from Ulgorn's side and independently checks each reverse leg for the Q2006
  reward return. Focused geodata verifies the recorded reverse legs around
  Nobekk; this new return has not yet passed a replay. The next focused replay
  stopped earlier, after two Q2005 Odella, because a newly observed hostile
  pack sealed all eight checked ingress checkpoints from about `(731,1499)`;
  it did not die. That return now falls back to a fresh, checked Mijou route
  with bounded ordinary guard clearing. This fallback is not yet replay-proven.
  Rechecking the Java and C# NPC templates corrected an earlier geodata probe:
  their `GetAggroRange()` reads XML `srange`, not `arange`. With the actual
  6–9 m observed-pack safety circles, the eastern Mau pocket around
  `(784,1513)` has no safe ranged approach; the earlier 3 m-circle probe was
  a false positive. This is a navigation/combat problem, not an excuse to
  ignore the pack or relax collision checks. A corrected geodata probe does
  find a checked firing approach to a nearby side guard even when the direct
  Highsitter approach is blocked. Guard clearing now searches the direct
  corridor first, then a bounded 20 m shoulder of client-observed monsters;
  it still must win an ordinary fight and re-observe the route. This broader
  pull policy has not yet passed a journey replay.
  A later bounded Q2006 checkpoint did naturally collect all three Q2005
  Odella and reached the first Mau sack. It then spent its remaining wall
  budget waiting for a loot list that could never arrive: a client-observed
  attack interrupted the sack's three-second use bar. Java `ActionItemNpcAI`
  and the C# port both emit `SM_USE_OBJECT` action 2 with duration zero on
  abort, versus duration 3000 on successful completion. The bot now decodes
  that distinction, treats an aborted use as a retry, fights the observed
  attacker, and puts a short diagnostic bound on missing loot lists. This
  correction is decoder-tested but has not yet passed a journey replay.
  The next Q2005-focused replay failed while clearing a naturally observed
  Stalker-area pack: the Priest reached 19% HP and correctly chose the 30%
  retreat branch, but that caller had supplied its current position as the
  refuge, producing a one-point, zero-distance route. Retreat now selects
  previously walked, client-estimated checkpoints that are farther from the
  observed attackers, and rechecks each candidate against current collision
  and aggro before moving. Policy tests cover the recorded Q2005 geometry.
  The focused replay proved that this avoids the zero-distance retreat: the
  Priest moved along four separately checked retreat legs. It still failed
  Q2005 with two of three Odella earned when a moving two-attacker pack and
  nineteen observed aggro circles left no checked route from about
  `(757,1491)`. There was no death. This is an unresolved tactical-navigation
  limit, not grounds to bypass collision. Trace review found an earlier,
  avoidable detour: route clearing had killed a client-observed 210750 Stalker
  near `(710,1460)` but had not looted its corpse as a Q2005 source. The
  guarded-corridor branch now tries ordinary corpse loot, checks the actual
  Odella count and either rests at a client-observed safe field position or
  retraces its checked ingress to Mijou. If the drop misses,
  another ordinary kill is still required. This correction is compiled but
  has not yet passed a focused replay. The next Q2005 checkpoint again earned
  two Odella, then exhausted its eight-minute diagnostic budget trying to
  return from about `(724,1484)` through roughly thirty observed aggro
  circles. Trace-reconstructed client observations at that position show the
  nearest active Scratcher at roughly 15 m, beyond its 7 m aggression range.
  The bot now checks every currently observed monster's aggression radius
  plus a five-metre buffer and, when clear, sits and waits at that walked
  field position for the next Stalker instead of making the long hazardous
  Mijou round trip between kills. Attacks interrupt the rest/wait and trigger
  ordinary defense. A bounded Q2005-only Docker SIM checkpoint subsequently
  passed in six minutes: the trace recorded ordinary Stalker loot raising
  Odella inventory from zero to one, two and three, with checked field camps
  after the first and second drops, followed by real Mijou quest completion.
  The separate corridor-Stalker loot correction was not exercised in that
  passing run and remains unproven by a journey replay.
  Item collection loops check packet-observed
  inventory after each attempt and retry on non-drops until the required count, including
  Q2004's 80% cube and the generic quest-drop executor. The shipped Q2113
  and Q2118 drops are 70% and 80%; focused plan tests keep both as
  inventory-counted collections with reachable fallback sources. Q2118's
  210735 dropper is inactive content without a shipped Ishalgen spawn, not an
  extra vendor or spawn to enable. Q2105's Sparkie loop
  now follows the same rule rather than assuming three kills imply three items.
  Every corpse-loot attempt records the actual drop and resulting observed
  inventory count in the NI-07 trace. A non-drop does not advance the quest;
  the overall run timeout bounds a stuck journey rather than a fixed number
  of loot attempts. The latest focused replay observed
  Q2004's cube miss twice, then drop on the third ordinary guardian; its
  inventory count moved from zero to one only on that third loot.
  A subsequent Q2006-focused checkpoint stopped earlier during Q2004's first
  tombstone guardian: at 136/483 HP the 30% retreat policy fired, but that
  fight caller had no explicit refuge and threw before trying an escape. The
  combat driver now derives retreat candidates from previously walked,
  client-estimated positions when no refuge is named; the tombstone caller
  also keeps its approach ingress and retries the ordinary guardian pull after
  a checked retreat and rest. Trace-derived retreat-policy and live-geodata
  outward-route tests pass. The
  Q2006 sack and reward-return fixes were not exercised by that run.
  A following Q2006-focused run did reach its first sack area after a natural
  Q2005 completion. While clearing an observed guard, the server rejected a
  Smite with `STR_SKILL_NOT_ENOUGH_DISTANCE` after the client had observed
  the moving target about nine metres away. The bot previously waited ten
  seconds for a cast-start packet that cannot follow that rejection. It now
  recognizes the server message, releases its local cast gate, re-observes
  after a short pause and bounds the same pull to two such range retries.
  The sack and reward return remain unproven by this run.
  Another bounded replay stopped earlier in Q2005. Its Stalker was at 13%
  client-observed HP while the Priest was at 171/537 HP, just above the 30%
  retreat threshold. The old policy chose another four-second self-heal and
  emerged at 68 HP, where retreat was required but the moving pack had sealed
  every checked exit. The deterministic policy now permits a ready Smite as
  a finishing blow only after this fight has already self-healed, the target
  is at or below 15% observed HP, and Priest HP remains above 30%; the 30%
  retreat still takes precedence. Focused policy tests passed first; a
  subsequent bounded Q2006-only SIM
  checkpoint did exercise that finishing-blow rule on a 4%-HP Stalker, then
  completed Q2005 naturally. It looted three distinct Mau Grain sacks through
  successful use interactions, returned to Mijou over 16 checked ingress
  checkpoints, and retraced 57 eastern-road checkpoints to claim Q2006 from
  Ulgorn. This is a passing 13/41 prefix, not the full NI-07 acceptance.
  The first Q2007-only checkpoint then advanced the ordinary Ulgorn and
  Nobekk dialogue, but its direct route to Derot dead-ended near
  `(579,2430)` in the checked-in geodata. Q2003 had already walked a safe
  eastern-road sequence through Nobekk, Dabi, Verdandi and the next roadside
  waypoint; Q2007 now reuses that checked journey before speaking to Derot.
  The next Q2007-only checkpoint replayed the revised road successfully
  through Derot, then reached `(817,1469)` en route to Nalto. Offline geodata
  has a checked route from that exact position to Nalto, but five
  client-observed aggro circles blocked the live route. The Nalto leg now
  uses the ordinary guarded-objective policy to fight reachable corridor
  blockers and re-plan; that change awaits a focused replay. A subsequent
  Q2007-only replay stopped earlier at the Q2006 return: all 17 saved ingress
  checkpoints plus the Mijou endpoint had become blocked by a dense respawned
  Mau pack. Three sacks had already been looted normally, but the Priest could
  not safely approach a corridor guard from `(682,1499)`. No Q2007 Nalto
  conclusion can be drawn from that run. The trace is
  `run/ni07-combat/20260924T030408176Z-465493623b6b4dc894b44bbc151b2d21.trace.jsonl`.
  Live geodata confirms that position has neither a checked firing flank to
  the nearby wall-separated Mau nor a checked ground route to Mijou even with
  aggro avoidance removed. The fallback now casts the Priest's client-observed,
  level-1 auto-learned Return skill (Java `ReturnEffect`), waits for the
  server's normal bind-location teleport, and walks the checked eastern road
  back to Mijou. A separate fresh-Priest Docker SIM probe confirmed the
  observed skill, ordinary cast/result and same-map bind reload (which sends
  `SM_CHANNEL_INFO` and `SM_PLAYER_INFO`, not `SM_PLAYER_SPAWN`, after the
  result's hit delay). A Q2007-focused replay then did trigger this fallback:
  the Priest cast Return from the trapped Q2006 pocket and arrived at its
  natural bind point `(571,2787)`. The next direct bind-to-Nobekk search had
  no checked route, so that run stopped before Q2006 completion. An offline
  Docker geodata check now verifies checked interaction legs from the bind
  point through previously visited Vandar, Guheitun, Vanar, Ulgorn and Boromer
  to Nobekk; the driver uses those legs to rejoin the eastern road. This is
  not a setup teleport or a claimed Q2006 replay pass; the revised return
  journey still needs focused validation.
  The next Q2007-focused replay ended earlier after legitimately earning all
  three Q2005 Odella. Q2004's 80% cube missed on two ordinary guardian corpses
  and dropped on the third; the trace recorded inventory counts `0,0,1`.
  Q2005's final Stalker drop brought Odella to three, but its per-pull ingress
  started at a field camp, so retracing it did not actually reach Mijou. A new
  hostile then closed the fresh checked route from `(747.976,1488.345)`. The
  Q2005 return helper now verifies arrival at Mijou, tries client-observed
  guard clearing and falls back to the ordinary learned Return skill and
  checked bind-to-road legs only if that guarded approach remains blocked.
  The next Q2005-only Docker SIM checkpoint passed: three ordinary Stalker
  corpses yielded the required Odella, the bot reached the real Mijou NPC,
  and Q2005 completed. Its route stayed open in that replay, so the new
  guard-clearing/Return fallback remains unexercised; this checkpoint proves
  the verified-arrival path, not every possible blocked layout.
  A later Q2007-only checkpoint naturally completed Q2006 again, then
  advanced Q2007 through Derot and Nalto. It stopped at Rae: four newly
  observed hostile circles blocked the checked approach from
  `(729.8415,1157.7434)`. A Docker geodata test finds a collision-checked
  Nalto-to-Rae interaction route when hazards are absent, so this is a guarded
  corridor rather than a terrain gap. Rae now uses the same bounded,
  client-observed guard-clearing approach as Nalto. That Rae change awaits a
  focused replay; neither Return fallback was exercised in this run.
  The first Rae-focused replay stopped before Rae in a different Nalto pack
  layout: five observed hostiles closed every checked route from
  `(817.381,1469.214)`. The old guard selector examined only the straight
  line to Nalto; the collision-checked route curves through terrain and meets
  a guard outside that line. A route-aware third pass now chooses only a
  client-observed monster whose aggro circle intersects the actual
  collision-checked path, then still requires ordinary Priest combat.
  Focused unit and Docker geodata tests confirm selection on the trace's
  blocked Nalto layout. The revised guarded approach has not yet passed a
  journey replay.
  The next Q2007-only replay reached Rae after an ordinary corridor kill,
  but the Priest died at a second Rae guard and revived at bind. The guard
  loop then incorrectly kept interpreting bind-area monsters as Rae-route
  blockers. It now stops guard clearing immediately when the ordinary combat
  policy revives the Priest. Q2007 allows up to ten death recoveries (two until 2026-09-24):
  rest at bind, walk the previously checked bind-to-eastern-road legs, then
  re-approach Nalto before Rae. No setup teleport or guard deletion is used.
  This recovery compiles but has not yet passed a journey replay.
  Q2007 is also staged from Java `ce54b7931`
  `_2007WheresRaeThisTime` and the shipped spawns: five NPC conversations,
  three ordinary generator interactions, Rae's quest-granted return teleport,
  and Ulgorn's Priest reward. Q2105's existing natural Sparkie
  kill/loot/turn-in path is now reachable after the campaign even if Q2002's
  active-journal priority bypassed its early branch. Q2106's Vanar letter,
  in-person step and recipient delivery are staged from Java
  `_2106VanarsFlattery`. A frozen 26-plan snapshot compiled from the shipped
  Ishalgen XML now feeds a natural template executor for ordinary report,
  hunt, quest-drop/object collection and Priest-compatible rewards. It
  validates earned prerequisites instead of using Q4I's grants, forced HP,
  neutral-to-monsters state or setup teleports. The snapshot's IDs and
  operation kinds are unit-tested. Q2133 gathering now invokes NI-06's
  client-observed Young Azpha node policy: it walks to shipped hints, handles
  failures and respawn waits, and checks earned inventory without granting
  skill or materials. This integration is compiled but not replay-proven.
  The four remaining custom side quests (Q2114, Q2123, Q2125 and Q2135)
  now have Java-mirrored ordinary combat, object-loot or dialogue paths.
  The full-scope test asserts all 41 client-journal completions, level 9,
  untouched Q2008 START/0 and physical proximity to Munin before declaring
  `journey-complete`. `NI07_FULL_JOURNEY=1` opts into that bounded acceptance
  run; `NI07_STOP_AFTER_Q2004=1`, `NI07_STOP_AFTER_Q2005=1`,
  `NI07_STOP_AFTER_Q2006=1` and `NI07_STOP_AFTER_Q2007=1` select shorter
  diagnostic checkpoints. The staged paths beyond Q2007 remain unexecuted,
  and no full-scope acceptance result exists yet.
  **Superseding acceptance (2026-09-26):** smart46 completed the entire contract
  on all four seeds with zero deaths. See the status document and retained
  `run/natural-batch/smart46-full-s*/` evidence; the historical limitations above
  describe the earlier checkpoints, not the current implementation.
- [x] **NI-08 — Durable resume and diagnosis.** Reconstruct state after relog or
  server interruption, resume the same character, and preserve a minimal failure
  package for stalls, disconnects and server defects.
  Implemented and validated in SIM: warm reconnect at Q2102 START/2, cold
  restarts at Q2003 REWARD/1 and Q2129 START/1, followed by all 41 completions
  and Munin on the same character. Both final restart runs had zero deaths;
  journal, inventory, skills, level and location persistence comparisons passed.
  The earlier failed Hatata restart exercised the quest-progress stall report;
  a missing retained identity refuses replacement creation and preserves a
  startup failure report. Diagnostic receipts do not restore server state.
  These are ordinary saved-state restart proofs, not abrupt-crash durability
  or full LIVE acceptance claims. See the status document for retained evidence,
  failed diagnostics and the full checklist results.
- [x] **NI-09 — Isolated LIVE acceptance.** `ni09-live-a4` completed the entire
  contract in the normal Docker-only isolated LIVE stack under ordinary rates
  and rules: 41 quests, zero deaths, level 9 at Munin, Q2008 START/0 and final
  same-character relog persistence. Enforce watching and all 31 repository
  checks passed; see the status document for evidence and retained failures.
- [ ] **NI-10 — Existing-world attach.** Add an explicit operator-selected mode
  that attaches to a retained server and never creates, drops or owns its database,
  containers or lifecycle; prove the unattended bot can coexist with a human.
- [ ] **NI-11 — Real-client observation.** With the authorized Computer Use
  workflow, follow the bot in the 4.8 client and retain visual/log evidence without
  assisting it. This is the deferred P10-07-style proof, not a gameplay oracle.

Implement NI-00 first, then NI-01/NI-02. NI-03 through NI-06 build the reusable
player policy; NI-07 integrates it. NI-08 precedes either acceptance run, and
NI-09 must pass before the retained-world NI-10/NI-11 proof. Failures caused by
existing Java/C# content follow the normal Java-first parity process; shared Java
limitations remain declared boundaries rather than invented content.

## Rules for a natural run

- Create one ordinary access-level-0 Asmodian Priest at level 1, intended to become
  a Chanter later. Chanter is selected during Ascension; this milestone ends while
  the character is still a Priest.
- Earn XP, items, kinah, gathering skill, equipment, and learned combat skills
  through the same actions and eligibility rules as a human player.
- No GM assistance to the subject or its objectives: no forced levels/classes,
  boosted stats, granted items/skills, quest-state edits, kills, spawns, instant
  recovery, or setup teleports. Legitimate in-game transports and quest-driven
  teleports are allowed.
- The LIVE bot acts through the normal client protocol, in real time. Movement
  follows terrain and collision; target positions and changing world state come
  from received packets. Static quest/route knowledge may guide planning, like a
  walkthrough, but must not reveal hidden runtime state or bypass a requirement.
- Prefer mapped roads for longer travel when a checked road corridor exists;
  leave the road for the actual quest target, gatherable or guarded object.
  Map artwork is only a route hint, never proof of a traversable segment. If
  observed hostiles block the only objective corridor, the Priest may fight
  through them using ordinary combat and replan after each fight.
- In observed combat, begin self-healing at or below 70% HP and retreat at or
  below 30% HP regardless of whether one or several attackers are present.
  At or below 80% HP, try an owned Minor Life Potion or Elixir first if its
  shared item delay is ready and a timed healing effect is not already active.
  The 30% retreat decision takes priority over potion use.
- Use the selected world's normal player rules and rates. Record the profile;
  zero-failure gathering, bonus suppression, and other test conveniences cannot
  silently establish a natural-run pass. SIM remains useful for rehearsals and
  regression reproduction; the observable acceptance run is LIVE.
- Read-only server logs, database checks, and admin diagnostics may verify results
  and explain failures. They must not supply hidden state to gameplay decisions or
  change the character/world to rescue progress.
- Handle ordinary setbacks with bounded, recorded recovery: rest/heal, wait for a
  respawn, choose another eligible target/node, reroute, revive, or reconnect.
  Repeated lack of progress produces a diagnostic failure, not infinite retries.

## Potion support added during NI-07

The shipped Asmodian Priest starts with 100 Minor Life Potions (`162000002`).
They apply an immediate heal plus a 20-second healing-over-time effect and use
item delay group 11 for 30 seconds. The buyable Minor Life Elixir (`162000052`)
has the same kind of 20-second effect and delay group 11, but its use delay is
60 seconds. The bot chooses an observed starter stack first, then an observed
elixir stack; it does not assume either item exists. The 80% potion decision is
suppressed while either effect is client-visible, and `BotTimingContract`
prevents using either stack before the shared delay expires. The action trace
records item ID, counts and cooldown decision. Java reference: `ce54b7931`
`CM_USE_ITEM`, `SkillUseAction`, and the shipped item/skill XML.

When combined owned starter-potion and elixir stock falls to five or fewer,
the post-fight inventory session checks
ordinary Ishalgen vendors Crizpinerk (`798038`, 608.150/2451.652) and Denma
(`203542`, 933.167/1685.701) with collision-checked navigation. Both have
trade list 721 containing Minor Life Elixirs and normal buy/sell dialogue.
The session sells only NI-05-approved surplus, protects both starter and bought
potions, and buys enough elixirs to bring combined stock toward twelve only as
observed Kinah and the displayed
vendor modifier allow. It verifies the packet-observed stock after purchase.
If no elixir is affordable, it waits for Kinah to change rather than looping
at the vendor. smart46 now covers potion use during the SIM journey. The complete vendor
walk/sale/purchase path still needs focused runtime evidence; isolated LIVE
acceptance remains NI-09. Unit/static-data tests do not prove that runtime path. No new quests, vendors, stock, or server behavior were added.

## Sharing the world with a human player

The intended experience is incidental contact during normal play. No spectator
account, special spectator channel, ready gate, party membership, or human rescue
is required. The bot and human use separate accounts in the same LIVE world and
can meet in the same ordinary channel. Observe without fighting or supplying the
bot for the independent progression proof; record any outside assistance or world
interference so the result can be interpreted correctly.

First validate this in a retained development world accessible to a real 4.8
client. Support a runner that attaches to an explicitly selected existing server,
preserves its database, and leaves lifecycle ownership with the operator. The
longer-term use is the maintainer's normal development play world. This document
does not authorize launching bots there now or changing the current isolated LIVE
stack policy (D13). MySQL remains Docker-only in every mode. Human observation
also checks visible movement/combat; packet assertions cannot establish rendering
correctness by themselves.

## Ishalgen acceptance contract

1. Create the Priest through the normal login and character-creation flow.
2. Complete the reviewed eligible quest list on that same character, using natural
   travel, combat, healing, inventory decisions, and gathering. Implement missing
   bot behaviors for existing quests as needed; do not add missing server content.
3. Earn level 9 and finish physically standing at Munin (NPC 203550) in Ishalgen,
   still a Priest, with Ascension Q2008 at START, step/var 0. Its automatic journal
   activation is expected; do not interact with Munin for Q2008, perform its
   objectives, or choose Chanter. Earlier Munin interactions for included quests
   are allowed. Reaching level 9 or the 182,252-XP cap early does not excuse any
   remaining eligible Ishalgen quests; a full XP bar is acceptable.
4. Relog and verify that completed quests, character class/level, inventory, and
   location persisted. Resume normal play from observed state across interruptions.
5. Run with a real client in the same world for the coexistence proof, with the bot
   visible and followable. Also establish that the journey needs no human presence.
6. Retain a per-quest result and an action/decision trace, including why the bot
   chose a skill, item, route, or recovery. Preserve server problems, refusals,
   disconnects, stuck positions, recent packets, revision, profile, elapsed time,
   and available seed metadata. Distinguish server bugs, bot-policy failures,
   environmental interference, and predeclared content exclusions. Never count a
   skipped or blocked objective as completed. LIVE interaction with humans is not
   assumed to replay deterministically from a seed alone.

Use the existing fingerprint ledger and narrowly scoped allowlist rules for
reporting; a green run is not a claim that known server defects have disappeared.
Report every known problem encountered, and fail on new/regressed problems or an
unsatisfied journey objective. Successful retries must not erase earlier errors.

## Revisit checklist

- [x] After Phase 10, assess natural travel, Priest survival, gathering, inventory,
  persistence, and LIVE coexistence against the capability table above. (`7d80e48d6`)
- [x] Freeze the eligible Ishalgen quest set and validate starter sources, level
  gates, prerequisites, and the Ascension stopping condition at Java
  `ce54b7931`. (`7d80e48d6`)
- [x] Turn the remaining policy gaps into ordered implementation TODOs. Keep the
  current SIM/LIVE regression suites and their evidence intact. (`7d80e48d6`)
- [ ] After Phase 11, define the broader character-to-endgame milestones, including
  required group composition, legitimate access to scheduled content, and durable
  progress across areas and sessions. Honor existing deferred-content decisions.

The Phase 10 review is a readiness decision, not an implementation claim. The
open NI TODOs define the work required for the natural Ishalgen proof. The
post-Phase 11 checkpoint remains responsible for defining the much broader
character-to-endgame contract.
