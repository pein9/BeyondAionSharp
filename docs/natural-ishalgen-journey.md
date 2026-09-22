# Natural Ishalgen Journey

Status: Phase 10 readiness review completed 2026-09-22 against Java `4.8` at
`ce54b7931`. The journey is **not yet executable end to end without assistance**:
the protocol, navigation, gathering and inventory foundations exist, but the
quest execution and durable recovery policies do not.
NI-00 through NI-06 are complete. NI-02 selects and explains the next bounded
step; NI-03 approaches its first quest starter without accepting the quest;
NI-04 proves a short Priest fight/rest loop; NI-05 proves sell-only inventory
housekeeping at a real vendor; NI-06 completes Q2133 through ordinary combat,
travel, gathering and turn-in. These slices do not yet execute the
41-quest journey. The broader game
journey remains a post-Phase 11 review. Review commit: `7d80e48d6` (before
SHA-recording amend).

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
| Travel on traversable routes and approach live objects | Partial | Geodata-backed local/journey pathing and checked long-distance LIVE movement exist. Add progress/stuck detection, bounded replanning and moving-target reacquisition. Q2002 legitimately teleports to the Ataxiar instance and returns by quest dialogue; the journey needs a narrowly scoped quest-transport policy, not an unrestricted map change. |
| Priest combat and survival | Policy implemented; bounded LIVE proof | NI-04 chooses only client-observed Sprigg Workers, intersects the frozen level-1–9 Priest map with the learned skill list, gates range/MP/group cooldowns, heals, uses owned starter potions, rests, and bounds retreat/revive attempts. Two ordinary LIVE kills and sit/stand recovery passed. Low-HP, consumable, retreat and death branches are policy-tested but not yet induced in LIVE; longer varied combats remain NI-07/NI-09 evidence. |
| Gathering | Policy implemented; bounded LIVE quest proof | NI-06 earned level 2 by ordinary Priest combat, accepted Q2133, walked to a client-observed Young Azpha, gathered three items and completed the quest with Nobekk. Failed-use depletion, occupied-node fallback, next-node search and normal respawn wait are policy-tested; this LIVE run had three successes on one node, so those recovery branches remain uninduced LIVE evidence. Cube-pressure handling uses NI-05's sell-only policy. |
| Inventory, equipment, skills and economy | Policy implemented; bounded LIVE sale proof | NI-05 selects class-usable rewards and gear from shipped templates, verifies auto-learned Priest skills from the client, protects all 41 quests' item references plus quest/key items and HP/MP supplies, and sells other sellable items without buying. A one-bot LIVE run sold the starter bandage stack at an active Ishalgen vendor; reward claiming, upgrade equipping and full-inventory recovery remain policy-tested until NI-07 exercises them naturally. Shipped unsellable starter extras cannot be sold. |
| Quest selection and execution | NI-07 in progress | Q4I completes 27 template quests with GM level/items/teleports and omits custom campaigns. A separate Docker SIM checkpoint now completes Q2000, Q2100–Q2104, and Q2001 on one ordinary Priest with checked walking, natural combat, object loot, class-appropriate rewards, and packet-confirmed quest state. This is 7/41, not the complete journey; Q2002 is auto-started next and its Ataxiar leg remains to be integrated. |
| Persistence and recovery | Partial | Relog/crash/save evidence and state oracles exist. A connection failure still ends a run; add checkpoint reconstruction, same-character resume, stall classification and bounded recovery. |
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
  and sell junk, unusable gear and other sellable surplus. There is no buy path.
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
- [ ] **NI-07 — Forty-one-quest execution.** Support the custom campaigns and
  quest-specific operations omitted by Q4I, then complete the frozen contract in
  SIM without grants, forced state, setup teleports or boosted stats. Allow the
  ordinary level-9 XP cap while finishing every included quest, then end at Munin
  before any Q2008 interaction. In progress: a Docker-backed focused SIM test
  creates a Priest and completes Q2000/Q2100–Q2104/Q2001 naturally on the same
  character, including ordinary Priest combat kills, post-kill rests, object loot,
  and class-appropriate rewards. Q2002 auto-starts at this checkpoint; its
  quest-driven Ataxiar visit is not yet exercised. Java `ce54b7931`
  `_2002WheresRae` specifies the temporary instance teleport and return.
  The decision engine now requires client-observed proximity to Munin before it
  can report `journey-complete`; journal state alone is insufficient. Neither
  the seven-quest checkpoint nor the endpoint rule satisfies the 41-quest TODO.
- [ ] **NI-08 — Durable resume and diagnosis.** Reconstruct state after relog or
  server interruption, resume the same character, and preserve a minimal failure
  package for stalls, disconnects and server defects.
- [ ] **NI-09 — Isolated LIVE acceptance.** Complete the entire contract in the
  normal Docker-only isolated LIVE stack under ordinary rates and rules.
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
