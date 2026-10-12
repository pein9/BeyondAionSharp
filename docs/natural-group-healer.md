# The Cleric as a group healer: heal the group, remove debuffs, never fight

Status: GH-01 survey waiting for a decision, 2026-10-12. The Java facts and two field attempts are recorded below;
the healer mode is not implemented. The operator answered the plan's questions the same day; the
answers are under [Decisions](#decisions). It is the last task before group play, and it follows
[natural-muster.md](natural-muster.md): the characters are the eleven of the one world, `ntc-group-s4`, and that
document's rules (MR-a to MR-f) hold here. The prompt to start the work with is at the end.

## What the operator said

2026-10-12: "we need to do one more task before we do group stuff: Cleric needs to learn to 'group heal' and dispell
negative buffs. When in group healing mode, the cleric needs to never do combat (but will need to loot when it's
their turn for quest items). Plan this out. This is critical for group play."

The same day, on the plan's four questions:

1. On raising the dead: "Yes when posisble but not a priority."
2. On changing how a healed fighter plays: "No, others fight just the same, healer is just there to make sure we
   survive thru bosses."
3. On the healer under attack: "Heal yourself is the priority."
4. On buffing the members: "Yes, buff."

## The goal

A Cleric that is told to play as its group's healer does four things and nothing else:

1. keeps itself and every member of its group alive with its heals, itself first;
2. removes the debuffs it can remove from them;
3. never attacks, pulls or fights back;
4. loots a corpse when the server gives it a quest item there.

It is there to bring the group through the fights it would not survive on its own, a boss above all. The other
members fight just as they do alone: their potions, scrolls, rests and retreats are untouched. A Cleric that is not
told to heal plays alone as it does today. Nothing in this plan changes solo play.

## What is there today

Found by reading the port and the shipped data; GH-01 checks each against the Java source before anything is built
on it.

**The Cleric's skills at level 26.** All of these are learned automatically and the group's Cleric has them.

| Skill | Learned | Cast, cooldown | What it does | Used today |
|---|---|---|---|---|
| Healing Light VI (1843) | 1, rank at 26 | 2 s, none | 594 HP to one friend within 23 m; 75 MP | on itself only |
| Light of Rejuvenation IV (3942) | 10, rank at 25 | instant, 5 s | heal over 30 s on one friend within 23 m; 39 MP | on itself only |
| Healing Grace II (4204) | 19, rank at 24 | 3 s, 6 s | 1,502 HP to one friend within 23 m; 134 MP | on itself only |
| Flash of Recovery I (3951) | 24 | instant, 30 s | 1,353 HP to one friend within 23 m; 127 MP | on itself only |
| Cleanse III (3937) | 13, rank at 23 | instant, 0.5 s | removes debuffs from one friend within 25 m; 106 MP | never: left out of the table |
| Blessing of Guardianship (1684) | 5 | instant | one hour of defence on one friend within 15 m | on itself only |
| Light of Resurrection (1699) | 10 | 6 s | raises a dead player within 15 m | never |

- **There is no heal for the whole group at level 26.** Healing Wind (4195: 1,291 HP to every member within 25 m,
  3.5 s cast) is learned at level 28, Acquittal (the group's debuffs and a heal) at 31, Splendor of Recovery at 40.
  The stigma skills of level 20 (Noble Grace, Saving Grace, Sympathetic Heal) are not used: no bot takes a stigma.
  So at level 26 "group heal" means healing the members one at a time, the one in most need first. The plan's table
  takes Healing Wind up by itself on the day the Cleric learns it.
- **Cleanse removes up to 100 eligible debuffs from its target in one cast**: effects in the debuff slot whose dispel
  category is physical, mental or both and whose dispel level is at most 1, with at most 30 remaining dispel resistance
  (`AbstractDispelEffect`, `EffectController.removeEffectByDispelCat`). A stronger resistance is reduced by 30 and
  leaves the effect in place. About 2,280 debuff skills in the data have the category and level. Stuns (about 400), debuffs with
  no dispel category (about 400) and those of level 3 and 5 (about 140) are not removable.
- **The monsters beside the fortress give something to remove.** The ruthless worg's Rabies (poison and blindness,
  19.3 s at its learned rank 12), the savvy daru's Lower-body Paralysis (a snare, 4 s) and the ettins' Weaken Defense (5 s) are physical
  debuffs of level 1. These are the monsters of the coin quest the muster already hunts.

**What the bots know of a group.** `BotWorldModel` holds the group's id, leader and loot rule, and for each member
its id, name, map, position, level, HP and whether it is online. The bot API can invite, leave, set the loot rule and
roll. Two Fast scenarios form a group and loot in it. The world model also holds the effects of every creature in
sight (`EffectsOf`) and the loot status of every corpse (`LootStatuses`). What is missing: a member's MP, class and
debuffs when it is out of sight (the server sends them in `SM_GROUP_MEMBER_INFO`; the bot drops them), who is
attacking a member, and any code that heals, cleanses or follows another character.

**What the server does.**

- A heal draws the monsters that fight the healed member onto the healer (`Effect.broadcastHate`). The healer will be
  attacked at times.
- The group's loot rule is round robin unless it is changed. A quest's drop is one of two kinds by its data: for
  each member (3,428 of 6,090 drops), or one for the group. A drop of the second kind is given to the first member
  that still needs it, only that member may loot it, and the server tells that member the corpse is open to it
  (`QuestService.getQuestDrop`, `SM_LOOT_STATUS`). That is the Cleric's "turn".
- Bots in one world take turns on one clock (`SimulationTurnTable`): a bot plays until it waits, then the bot with
  the earliest wake plays. A healer that looks at its group every quarter second reacts within a quarter second.

## How the healer plays

**The mode.** A round names a bot's role: `"role": "healer"` and whom it stays with. The fight driver then runs the
support table of the bot's class in place of its fight table. The mode is the driver's, not the Cleric's: a class has
it when its profile has a support table, and only the Cleric gets one in this plan. No branch on a class.

**One decision at a time**, from what the client observes, each one traced with its checks like a fight decision.
The first rule that applies is taken. At every rule the healer comes before a member: "Heal yourself is the
priority."

1. **In danger** (HP at or below 35%), itself and then the member with the least HP: Flash of Recovery, else Healing
   Grace, else Healing Light. For itself also the Anti-Shock scroll and the life potion, as today.
2. **A debuff to remove**, on itself and then on a member: an effect Cleanse can remove with 3 s or more left to
   run. A snare on the member the monsters are on and a poison come before others.
3. **Hurt** (HP at or below 70%), itself and then a member: Healing Grace when the missing HP is at least what it
   heals and it is ready, else Healing Light.
4. **Several members hurt at once**: the group's heal, when it is learned and three or more members within its
   range are at or below 70%. At level 26 this rule finds no skill and passes.
5. **The one being attacked has no Light of Rejuvenation**: cast it, on itself when it is the one. It is instant and
   costs 39 MP for 30 s.
6. **Between fights**: Blessing of Guardianship on itself and on every member that lacks it; everyone to 90% HP with
   Healing Light; its own mana by the powder (MP Recovery), Penance and the mana serum, as its rest rules have them.
7. **A dead member**, when no one is being attacked and it is within reach: Light of Resurrection. Last, and only
   when possible.
8. **Nothing to do**: stay in place behind the member it follows, look again in a quarter second.

The numbers are the run's parameters, as the fight table's are, and GH-10 tunes them from what the proofs show.

**Never combat.** In the mode the driver sends no attack and casts no skill at an enemy. It does not select a
monster as its target. This is one guard at the place every attack and cast is sent, so it cannot be forgotten in a
rule; a breach stops the bot. A monster that attacks the healer is not answered. The healer stays with the group,
keeps itself alive first and heals the others with what is left. If it dies, that is an outcome, as every death is.

**Where it stands.** It follows one member, the one the round names, and keeps between 8 and 18 m from it with a
line of sight: inside every heal's 23 m and outside a melee. It does not walk into a monster's aggro range to get
there when another way exists. When the member moves on, it follows.

**Loot.** When the server opens a corpse to the healer and the corpse holds an item of a quest the healer is
working, the healer walks to it and takes that item, by the quest-loot sweep the solo bot already has. It goes only
when no one is at or below 70% HP, and it goes at once when the member it follows moves on, so that it is not left
behind with the corpse. It takes quest items only, as every bot does.

**The other members** fight just the same. A fighter beside a healer still drinks its potion at the HP it does
alone, still rests after a fight and still retreats by its own rules. The healer's heals come on top of that. Against
ordinary monsters the healer will often find nothing to do; that is expected. Its work is the fight in which the
members' own means are not enough.

## Decisions

| | Question | Decision |
|---|---|---|
| GH-Q1 | Does the healer raise a dead member (Light of Resurrection)? | **Yes, when possible, and not a priority** (the operator). It is rule 7 and the last item, GH-12. |
| GH-Q2 | Does the healer keep Blessing of Guardianship on the members? | **Yes** (the operator): rule 6. |
| GH-Q3 | The thresholds: danger at 35%, hurt at 70%, top up to 90% between fights, a debuff with 3 s left. | Not asked. **As written**, tuned in GH-10. |
| GH-Q4 | The healer under attack. | **Its own healing is the priority** (the operator). It stays with the group, does not attack and does not flee. |
| GH-Q5 | Whom it follows. | Not asked. **The member the round names; the group's leader when it names none.** |
| GH-Q6 | Which classes get the mode now. | Not asked. **The Cleric only.** The Chanter and the Bard have heals too; their tables are a later item, by data. |
| GH-Q7 | Are the proof rounds captured? | Not asked. **No.** They are proofs. The group world stays `ntc-group-s4` until a group leg is played. |
| GH-Q8 | Does a fighter beside a healer change how it plays (its potion later, no rest for HP)? | **No** (the operator): "others fight just the same, healer is just there to make sure we survive thru bosses." |
| GH-Q9 | The healer's own level and gear rules. | Not asked. **Unchanged**: the staff with the most magic boost, the kit, the shard, the cleanup. |

## Standing rules

- SIM only, seed 1, in the one world. A proof starts from `ntc-group-s4` and plays a round.
- Read the Java first (`../aion-server`, branch 4.8) for every server behavior an item relies on. Java wins.
- Generic first: the mode, the policy and the world model know no class. A class's support table is data.
- Natural play: what the healer knows is what a client sees. No server state is read to decide.
- No new unit tests for bot work. An item is proved by the build, the bundle and a SIM round, and its numbers are
  read from the traces by an audit script under `scripts/sim/trace/`.
- Solo play does not change, and neither does how a fighter plays in a group. The five starter scopes of the gate
  stay identical after every item.
- One commit an item on `main`, code and then evidence. Nothing is pushed.

## Checklist

### GH-01 survey facts (2026-10-12)

Java paths below are relative to `../aion-server/game-server/src/com/aionemu/gameserver/`, on branch `4.8`.
The corresponding C# packet, life-stat updater, effect controller and revive paths retain these rules.

| Fact | Java reference |
|---|---|
| A group member packet carries object/group ids, exact HP/MP and flight points, map and instance, XYZ, class, gender, level, fly state and mentor state. Offline members carry zero vitals. ENTER/UPDATE includes the visible effects; UPDATE_EFFECTS replaces the named slot mask, with caster, skill/rank, target-slot ordinal and remaining milliseconds. | `network/aion/serverpackets/SM_GROUP_MEMBER_INFO.java:28, 53` |
| Joining or reconnecting exchanges member packets. HP/MP changes queue a MOVEMENT update on a 500 ms FIFO; movement has its own 2 s updater. Adding/removing a nonpassive effect sends MOVEMENT and an immediate UPDATE_EFFECTS for its slot. Updates go to every other group member. | `model/team/group/events/PlayerGroupEnteredEvent.java:26`; `PlayerConnectedEvent.java:34`; `PlayerGroupUpdateEvent.java:32`; `model/stats/container/PlayerLifeStats.java:38, 67`; `taskmanager/tasks/TeamStatUpdater.java:26`; `TeamMoveUpdater.java:26`; `controllers/effect/PlayerEffectController.java:70` |
| Sighted players receive SM_ATTACK_STATUS on HP changes, with the current HP **percentage**, not exact HP. MP packet types 19–23 carry MP percentages; they must never be mistaken for deaths. The member itself receives exact SM_STATUPDATE_HP. Ordinary attacks and spell results identify attackers and targets. | `model/stats/container/CreatureLifeStats.java:152`; `PlayerLifeStats.java:93`; `network/aion/serverpackets/SM_ATTACK_STATUS.java:124`; `SM_ATTACK.java:52`; `SM_CASTSPELL_RESULT.java` |
| Indirect effects add their hate to nearby creatures already hating the caster or recipient. A heal can therefore draw an engaged monster to the healer; the effect's hate is zeroed after broadcasting. | `skillengine/model/Effect.java:622` |
| Dispel count is the inherited base value; power is `power + dpower * skillLevel`. Cleanse III's data gives count 100, power 30, level 1 and ALL categories in DEBUFF. Each eligible effect loses 30 resistance; only one reaching zero ends. STUN is a separate category; long-duration protected effects and already-designated effects are excluded. | `skillengine/effect/AbstractDispelEffect.java:32`; `DispelDebuffEffect.java:20`; `controllers/effect/EffectController.java:528, 604, 649`; `skillengine/model/Effect.java:154, 1057`; Java `data/static_data/skills/skill_templates.xml`, skill 3937 |
| Default loot is ROUNDROBIN. Ordinary loot cycles through the nearby entitled members. Quest drops are processed separately: each-member drops make a personal item for every eligible member; a shared drop chooses the first eligible member in the supplied member collection, records that member as item owner and opens the corpse to it. It is not alternating quest ownership by the ordinary loot counter. | `model/team/common/legacy/LootGroupRules.java:36`; `services/drop/DropRegistrationService.java:84, 122`; `services/QuestService.java:666, 733`; `model/templates/quest/QuestDrop.java:29, 67` |
| Opening a corpse and taking its items still requires loot rights. SM_LOOT_STATUS is corpse-level permission; SM_LOOT_ITEMLIST filters out items the player cannot view. The bot must inspect that list and take only its needed quest items. A dead corpse entering sight sends LOOT_ENABLE to an entitled player. | `services/drop/DropService.java:90, 277, 488`; `network/aion/serverpackets/SM_LOOT_ITEMLIST.java:26` |
| Resurrection on a dead player enables its offer and sends SM_RESURRECT. Accepting is CM_REVIVE with SKILL_REVIVE id 3; the server requires the active offer and revives at 35% HP/MP with soul sickness. A normal resurrection stays at the corpse unless the positional-resurrection state is set. | `skillengine/effect/ResurrectEffect.java:27, 37`; `network/aion/serverpackets/SM_RESURRECT.java`; `network/aion/clientpackets/CM_REVIVE.java:49`; `model/gameobjects/player/ReviveType.java:22`; `services/player/PlayerReviveService.java:42` |

The three candidates, with the hard-fight choice pending the decision below:

- **Hard fight:** King Zugog (212644), level 27 HERO/MASTER, shipped at (896.833, 2169.32, 213.125),
  about 691 m in a straight line from the fortress hub. Java's NPC template, Morheim spawn and NPC skill rows
  name it. The port's approved retail `battle_cycle` table drives it (pattern `ND2_KnN`); the proof uses that
  shipped behavior and the Templar's existing potion, scroll and retreat rules.
- **Removable debuff:** ruthless worg (211302), Rabies (16819), rank 12: DEBUFF_PHYSICAL, level 1,
  resistance 10, poison and blindness. Java `Effect.calculateTemplateDuration` uses `15100 + 350 * 12 = 19300 ms`.
  Sources: Java `data/static_data/npc_skills/npc_skills.xml`, NPC 211302; `skills/skill_templates.xml`, skill 16819;
  `skillengine/model/Effect.java:900`. The port ships the same rows.
- **Shared quest drop:** Q2307 "Irresistible Soup", started at Favyr (204378). Its four ingredient drops omit
  `drop_each_member` (Java defaults it to 0), including the worg's Meat (182204107). Neither Q2307 nor Q2430 has a
  row in the group snapshot's `player_quests` table; Q2307 has no prerequisite and avoids Q2430's prerequisite
  and collecting-step-8 sequence. Sources: Java `data/static_data/quest_data/quest_data.xml`, quest 2307;
  `data/handlers/quest/morheim/_2307IrresistibleSoup.java:30`; `model/templates/quest/QuestDrop.java:29`.

### Items

- [ ] **GH-01 Survey.** Read the Java for: when `SM_GROUP_MEMBER_INFO` is sent and what it carries; how HP of a
  member in sight reaches another client sooner; `Effect.broadcastHate`; `AbstractDispelEffect` and what Cleanse's
  count and power mean; `QuestService.getQuestDrop` and `DropService` for a group; the default loot rule; the
  resurrection offer and its answer. Then choose three things near Morheim Ice Fortress. A hard fight: an elite or
  named monster, or a pull of several, that takes a level-26 fighter below half its HP although it uses its own
  potion and scroll. A monster that puts a removable debuff of 10 s or more on a player (the ruthless worg is the
  candidate). A quest with a drop of the one-for-the-group kind that no character has done (candidates: Q2307
  "Irresistible Soup", Q2430 "Secret Information").
  - Proof: a facts table in this document, each row with its Java reference; the three choices named, with one
    uncaptured round of a fighter alone in the hard fight as evidence of what it costs.
  - 2026-10-12, code `3dd8f2b41`: the `gh-survey` round stage resumes a mustered level-26 character, walks to
    `targetNpcId`, and makes one attempt with its existing fight table. The receipt verifies that the survey was
    played, including a death; the trace audit separately decides whether its evidence meets the hard-fight criterion.
    No new unit tests, fighter policy changes or healer mode were added.
  - Two uncaptured seed-1 rounds from `ntc-group-s4`, under `run/gh/GH-01`, audited by
    `scripts/sim/trace/group_healer_survey.py <absolute round folder>`:

    | Run | Target | Lowest living HP | Deaths | Target killed | Fight time | Life potions consumed | Anti-Shock consumed / attempted |
    |---|---|---:|---:|---|---:|---:|---:|
    | `zugog-solo1` | King Zugog (212644), 504,427 HP | 19.86% (then 0%) | 1 | no | 13.948 s | 1 | 0 / 1 |
    | `seku-solo1` | Seku the Youngest (213800), level 25 elite, 12,513 HP | 74.65% | 0 | yes | 35.129 s | 1 | 0 / 0 |

    Zugog's scroll was refused with `STR_SKILL_CAN_NOT_USE_ITEM_WHILE_IN_ABNORMAL_STATE`; the count remained 30.
    Its Courage scroll was consumed at the start of the fight. Seku never drove the fighter below half HP.
    Both remained level 26 and made no retreat. Both survey stages reached their end; both owned schemas were
    dropped, and no snapshot was captured or changed.
  - The bundle, `run/gh/GH-01/bundle.log`: server tests 4,632 passed, 16 skipped; seven checks and the three script
    checks passed; Fast `gh01-fast` 11 scenarios passed (its simulation prerequisites: 115 passed, 96 skipped).
    Gate `guard-starters`, parallel 5: mage 23,555, warrior 24,199, artist 23,104, engineer 24,580 and scout 27,592
    normalized trace records, each count and digest identical to its baseline. Build servers shut down after the bundle.
  - **Stop after two attempts, as the handoff prompt requires.** The audit interprets "although it uses its own
    potion and scroll" as a consumed life potion and defensive Anti-Shock scroll, rather than an attempted scroll
    or the speed scroll. Under that interpretation both audits say `passed: false`. This qualification is an
    interpretation, not a new operator decision. The Java survey, worg and Q2307 choices are ready, but GH-01 stays
    unticked until the operator either accepts Zugog's normal-fighter result with its recorded refusal, or authorizes
    another hard-fight survey (a level-26 veteran elite such as Klaw Spawnguard, 212816, 28,351 HP, is untested).
    GH-02 through GH-13 remain untouched.
- [ ] **GH-02 What the client sees of a group.** The world model keeps a member's MP, class, flight state and
  effects from `SM_GROUP_MEMBER_INFO`, counts HP 0 as dead, and takes the freshest HP of the group packet and what
  it sees of a member in sight. It keeps who attacks whom for every creature in sight, not for itself alone.
  - Depends: GH-01. Proof: the bundle; a round of two grouped bots whose traces show a member's HP, MP and a debuff
    as the other saw them, against the member's own.
- [ ] **GH-03 The support table's data.** The Cleric's catalog gains the roles of its support skills: the four heals
  by kind (cast heal, great heal, instant heal, heal over time), cleanse, blessing, the group's heal and the
  resurrection, each with every rank. A dispel catalog is built from the shipped skill data: for a dispel skill, the
  effects it can remove (slot, category, level).
  - Depends: GH-01. Proof: the bundle; the profile validator accepts the Cleric with the new roles; the catalog
    lists Rabies, Lower-body Paralysis and Weaken Defense for Cleanse III and no stun.
- [ ] **GH-04 The triage policy.** A pure policy, `NaturalGroupHealPolicy`: from the observed group it returns one
  action and the check of every rule it passed. The eight rules above, itself before a member at each, with the
  run's numbers.
  - Depends: GH-02, GH-03. Proof: the bundle. It is first exercised in GH-05.
- [ ] **GH-05 The mode.** A round's bot may be given `"role": "healer"` and `"with": "<line>"`. The fight driver
  runs the support table: the decision loop, the follow rule, the cast on a friend, the blessing on every member,
  and the guard that refuses every attack and every cast at an enemy. A group is formed at the stage's start by the
  invitation and its answer, as a client does it.
  - Depends: GH-04. Proof: the bundle; a round of the Cleric and one fighter in the fortress, grouped, with no
    fight: the Cleric follows the fighter through a walk, both carry the blessing, and its trace holds no attack.
- [ ] **GH-06 Through a hard fight.** The first proof in the field: the Cleric and the Templar against the hard
  fight of GH-01, the Templar playing exactly as it does alone. The same fight is played once without the Cleric,
  for the difference.
  - Depends: GH-05. Proof: the two rounds, uncaptured. From the audit: no attack and no enemy cast by the Cleric;
    every time the Templar was at or below 70% a heal began within 3 s; the Templar's lowest HP with and without the
    healer and whether it lived; the Cleric within 23 m of it for 95% of the fight's time or more; the Cleric's own
    lowest HP and mana.
- [ ] **GH-07 Remove debuffs.** Against the monster with the removable debuff.
  - Depends: GH-05. Proof: the round. Of the debuffs put on the healer or a member with 5 s or more to run, how many
    Cleanse removed and how soon; none was cast at a debuff it cannot remove.
- [ ] **GH-08 Loot on its turn.** Both hold the proof quest. The Cleric loots when the server opens a corpse to it.
  - Depends: GH-05. Proof: the round. Both end with the quest's items; every item the server gave the Cleric was
    taken by the Cleric; it took nothing else; it left for a corpse only with everyone above 70%.
- [ ] **GH-09 Three members.** The Cleric with the Templar and the Gladiator in the hard fight.
  - Depends: GH-06, GH-07, GH-08. Proof: the round. The same numbers for each member; the healer came first and the
    member in most need next each time two were hurt at once.
- [ ] **GH-10 A long session and the numbers.** An hour of game time of the three hunting ordinary monsters, with
  the hard fight in it. The thresholds are tuned once from what GH-06 to GH-09 showed.
  - Depends: GH-09. Proof: the round. The fighters' kills an hour beside the healer against the same hour without
    it: the healer must not slow them. Deaths, each with its cause; the Cleric never out of mana in a fight for more
    than one decision; how many of its heals landed on a member that its own potion was already healing.
- [ ] **GH-11 The healer attacked.** A fight in which a monster turns on the Cleric.
  - Depends: GH-09. Proof: a round in which it happened. The Cleric did not attack, healed itself before anyone
    else, stayed within range of the group; what became of it.
- [ ] **GH-12 Raise the dead, when possible** (GH-Q1). The healer casts Light of Resurrection on a dead member within
  15 m once no one is attacked. The member has to be there to be raised: today a dead bot takes the obelisk at once.
  So a dead member whose group has a living healer within 40 m waits 30 s for the offer and accepts it; with no
  offer it takes the obelisk as before. That wait is the one thing this plan asks of the other classes. If the
  server's offer and answer need more than a small change, the item is written up and left open.
  - Depends: GH-10. Proof: a round in which a member died and was raised, and the group went on together; or the
    write-up of what stands in the way.
- [ ] **GH-13 Close.** This document's closing status: what the healer does, the numbers of the proofs, what was
  found and not fixed, and what the group plan takes from here. The memory notes.
  - Depends: every item above; GH-12 may be open.

## Hazards

1. **The healer is for the fights a group does not survive alone, and those are not beside the fortress.** In the
   fights of the muster that I read, the Templar stood between 91% and 96% of its HP against monsters of level 21.
   GH-01 has to find a fight in the open world that hurts. The real test is the group plan's first boss.
2. **Two healers for one wound.** A fighter drinks its potion at the HP it does alone, and the healer heals the same
   member. Some heals are wasted. That is the price of leaving the fighters as they are; GH-10 counts it.
3. **Old news.** The server sends a member's HP in the group packet on its own schedule. A heal decided on an old
   number comes late or is wasted. GH-02 takes the freshest of what it sees; GH-06 measures how late a heal starts.
4. **A cast heal lands 2 to 3 s after it is decided.** A member at 35% under three monsters can die inside a
   Healing Grace. Rule 1 takes the instant heal first for that reason, and it has a 30 s cooldown.
5. **Aggro on the healer.** Heals draw the monsters. A fighter that holds no aggro on purpose leaves the healer hit
   often, and the healer then spends its heals on itself, as the operator wants. The Templar's taunts are a
   fighter's group play and belong to the next plan; GH-11 records what happens without them.
6. **Line of sight and the follow.** A heal is refused through a wall or beyond 23 m. A healer that follows too far
   behind loses the member at a corner; one that follows too close stands in the fight.
7. **The loot walk.** A corpse 20 m off the path takes the healer out of range of a member that has gone on. The
   rule goes only when everyone is above 70% and comes back at once.
8. **Mana.** Healing Light gives 594 HP for 75 MP. A long fight of three members empties the healer. The powder and
   Penance are for between fights; the mana serum is the one thing for inside a fight.
9. **Turns.** A healer that looks every quarter second takes four turns a game second for an hour. If a round
   becomes slow, the look is every half second while no one is attacked.

## Not in this plan

- How a group pulls, who holds aggro, who waits for whom, and how fighters assist each other: the group plan.
- Any change to how a fighter plays beside a healer (GH-Q8), beyond the wait of GH-12.
- The Chanter's and the Bard's support tables.
- Stigma heals, and any skill book.
- Healing a player outside its group, and a human in the group: the far goal, after the bots can play together.
- Recording the stale gate scopes again.

## Handoff prompt

```
Work the GH checklist in docs/natural-group-healer.md, one item at a time, starting with GH-01.

Repository: C:\Users\ryanf\Documents\GitHub\BeyondAionSharp, branch main. Nothing of the GH list is implemented.

READ FIRST
- CLAUDE.md, then docs/natural-group-healer.md from top to bottom: the goal, what is there today, how the healer
  plays, the Decisions table (the operator answered GH-Q1, Q2, Q4 and Q8 on 2026-10-12; the others stand as
  written), the standing rules, the checklist and the hazards.
- docs/natural-muster.md: the one world and its rules MR-a to MR-f, how a round is run, and the muster stage in
  NaturalIshalgenJourney.cs, which is the nearest thing to copy for a new stage. A round's stages and roles are read
  in tests/Aion.Simulation.Tests/SimulationNaturalRoundTests.cs and scripts/sim/run-round.ps1.
- The memory notes on the one world database, the group healer plan, power shards and the bag cleanup, and no new
  unit tests for bot work.

WHERE THINGS STAND
- The group is the round snapshot ntc-group-s4: eleven Asmodian second classes at level 26 in Morheim Ice Fortress,
  one account each (111 to 121), in one world database. Every proof starts from it:
    pwsh -NoProfile -File scripts/sim/run-round.ps1 -Round <round.json> -Item GH-<n> -Run <id> -From ntc-group-s4
      -ReplayRoot <ABSOLUTE path to run/gh>
  Proof rounds are not captured (GH-Q7). Pass -ReplayRoot as an absolute path: the test process runs in game-server.
- The neutral gate guards five scopes only: -Set mage+warrior+artist+engineer+scout -Parallel 5. The other
  seventeen are stale by the operator's word and are not recorded again.

EACH ITEM
1. Read the Java first (../aion-server, branch 4.8) for every server behavior the item relies on. Java wins.
2. Do only that item. Generic first: the mode, the policy and the world model know no class; the Cleric's support
   table is data. Solo play and a fighter's play in a group do not change.
3. Prove it as the item says: the build, the bundle, and SIM rounds from ntc-group-s4 whose numbers an audit script
   under scripts/sim/trace/ reads from the traces. No new unit tests for bot work.
4. Before a code commit: dotnet test tests/Aion.GameServer.Tests (4,632 passed and 16 skipped today); the seven
   checks (check-warning-baseline, check-null-loggers, check-clock-reads, check-custom-quest-drafts,
   check_fidelity.py, test-quest-plan-compiler.py, test-retail-quest-inventory.py); the three script tests when
   scripts/sim changed; Fast (scripts/e2e/run-fast.ps1 -Run <name>, 11 passed); and the gate on the five scopes,
   which must stay identical. run/mr/MR-1/chain.sh runs all of it in order and is the pattern to copy.
5. Tick the item in the document with a dated line of evidence (run ids and numbers), and commit on main: the code,
   then the evidence. No co-author or attribution line. Never push.

PRACTICAL
- A long job (a bundle, a round) runs in the background; wait for its notification. A foreground sleep is blocked.
- Many source files have CRLF line ends: an exact-text edit has to allow for them. A bash heredoc halves the
  backslashes of a Python script inside it: write such a script to a file first.
- On Windows, stopping a background bash chain kills only its wrapper: check by process id before building.

RULES THAT DO NOT BEND
- SIM only, seed 1. No LIVE run. Do not touch the operator's aion stack beyond the throwaway aion_gs_sim_ni08_*
  schemas the scripts make and drop.
- One world database. No character gets a schema of its own, and no capture is made by playing one character alone.
- Never branch, use a worktree, spawn a subagent, push, or check out an older commit. Never overwrite a snapshot;
  a new capture gets a new name. Do not hand-edit generated data or raise the warning baseline.
- Never build or run checks while a run holds the build outputs (scripts/sim/sim-run-marker.ps1 says). After a
  chain, dotnet build-server shutdown.
- Leave docs/playtest-aethertapping-plan.md, docs/playtest-crafting-alchemy-cooking-plan.md and
  docs/playtest-ground-essencetapping-plan.md alone.
- The healer never attacks. Its own healing comes before a member's. Other members fight just the same.
- A death, a refused cast and a failed attempt are outcomes to record, not failures to hide.

STOP AND REPORT when GH-13 is ticked, when an item cannot be proved in two attempts and needs a decision, or when a
question comes up that the Decisions table does not answer. GH-12 may be left open with a write-up. Report what was
done with its numbers, what is open, and what the operator must decide.
```
