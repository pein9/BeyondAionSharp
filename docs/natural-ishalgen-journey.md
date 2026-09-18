# Natural Ishalgen Journey

Status: deferred design, recorded 2026-09-18 after Phase 7. Revisit after Phase 10;
review the broader game journey after Phase 11. This document does not schedule or
start implementation. The active sequence remains the
[end-to-end player simulation plan](e2e-player-simulation-plan.md).

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

Build a reviewed quest list when implementation starts. Include the implemented,
naturally obtainable Ishalgen quests applicable to the character before Ascension,
with prerequisites and evidence for the actual acquisition path. Exclude quests
started by random drops, disabled/event-only content outside the selected profile,
unimplemented quests, and content unavailable to this character. Ordinary quest
objectives that require killing and collecting drops remain in scope; the random
drop exclusion concerns quest starters, not all loot objectives.

The Phase 7 classifier is a useful inventory, but a generated plan or an
"obtainable" label does not prove natural acquisition. In particular, Q2107's
starter source still needs verification; the current Q4I runner grants it through
the director. Q2122 and Q2136 have random-drop starters. These are review notes,
not a fixed completion count. Freeze the accepted scope before a run; a required
quest that fails during play remains a failure or blocker rather than being
silently removed from the denominator.

## Current foundation and remaining work

At `58113f4a8`, real client packets, movement timing, combat, gathering, quest
dialogs, persistence, and logging are exercised by scenarios. The missing layer
is a continuous player policy that selects and combines those actions and recovers
when the world does not follow a prescribed sequence.

The current [LIVE Q4I runner](../tools/Aion.LiveBots/LiveQuestPlanScenario.cs)
raises the subject to level 50/Gladiator, boosts attack and gathering, teleports to
objectives, supplies item starters, and can complete external prerequisites through
the GM director. Its 27 template completions demonstrate quest execution under
controlled setup; they do not establish natural progression. The present LIVE
character creation also selects Warrior. Keep these focused regression scenarios
useful independently of this later journey.

| Capability needed for continuous play | Foundation to review |
|---|---|
| Travel across the area on traversable routes; approach moving NPCs; recover from blocked paths; use legitimate transport and pay its costs | Phase 6 movement plus Phase 9 geodata/collision; packet emission alone is insufficient |
| Priest combat: choose targets and usable skills, respect range/cast/cooldown rules, heal, manage HP/MP, rest, retreat, and recover from death | Phase 6 combat primitives; an adaptive survival policy still needs implementation |
| Loot, choose usable quest rewards, equip earned upgrades, learn skills legitimately, manage cube space, protect quest items, and buy/sell within earned kinah | Phase 8 inventory, economy, gear, and lifecycle coverage |
| Gather quest materials at earned skill levels, handling failures, occupied nodes, depletion, respawns, and inventory limits | Phase 8 gathering coverage plus a resource acquisition policy |
| Select eligible quests, fulfill dependencies, handle implemented custom missions, and earn levels continuously | Phase 7 plans/dialog evidence plus new bot policies for existing content |
| Resume the same character after relog/restart, reconstruct progress, diagnose stalls, and capture reproducible failures | Phase 10 operations, reports, and client captures |
| Coexist with human players and tolerate contested mobs/nodes, changing object IDs, and unsolicited packets | LIVE operation and Phase 10 soaks/client validation; group cooperation expands after Phase 11 |

Completing Phases 8–11 supplies tested mechanics and diagnostics. It does not by
itself deliver autonomous progression: that orchestration is a separate future
implementation. Phase 10's loop over existing scenarios is not evidence that a
fresh character can progress through an area without setup assistance.

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
3. Earn level 9 and leave Ascension Q2008 at its initial, unadvanced state. Its
   automatic journal activation is allowed; do not perform its objectives or choose
   Chanter. Reaching level 9 early does not excuse remaining eligible Ishalgen
   quests; verify level/XP behavior against the reference when defining the route.
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

- [ ] After Phase 10, assess natural travel, Priest survival, gathering, inventory,
  persistence, and LIVE coexistence against the capability table above.
- [ ] Freeze the eligible Ishalgen quest set and validate starter sources, level
  gates, prerequisites, and the Ascension stopping condition at the then-current
  Java reference.
- [ ] Turn the remaining policy gaps into ordered implementation TODOs. Keep the
  current SIM/LIVE regression suites and their evidence intact.
- [ ] After Phase 11, define the broader character-to-endgame milestones, including
  required group composition, legitimate access to scheduled content, and durable
  progress across areas and sessions. Honor existing deferred-content decisions.

These are review checkpoints, not a promise that the preceding phases will have
implemented every capability. Continue Phases 8–11 in their existing order; decide
the natural journey's implementation scope using the resulting evidence.
