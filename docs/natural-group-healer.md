# The Cleric as a group healer: heal the group, remove debuffs, never fight

Status: plan, 2026-10-12. Nothing in it is implemented. It is the last task before group play, and it follows
[natural-muster.md](natural-muster.md): the characters are the eleven of the one world, `ntc-group-s4`, and that
document's rules (MR-a to MR-f) hold here.

## What the operator said

2026-10-12: "we need to do one more task before we do group stuff: Cleric needs to learn to 'group heal' and dispell
negative buffs. When in group healing mode, the cleric needs to never do combat (but will need to loot when it's
their turn for quest items). Plan this out. This is critical for group play."

## The goal

A Cleric that is told to play as its group's healer does four things and nothing else:

1. keeps every member of its group alive with its heals;
2. removes the debuffs it can remove from them;
3. never attacks, pulls or fights back;
4. loots a corpse when the server gives it a quest item there.

A Cleric that is not told so plays alone as it does today. Nothing in this plan changes solo play.

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
- **Cleanse removes every debuff it can from its target in one cast**: every effect in the debuff slot whose dispel
  category is physical, mental or both and whose dispel level is 1 (`AbstractDispelEffect`, `EffectController.
  removeEffectByDispelCat`). About 2,280 debuff skills in the data are of that kind. Stuns (about 400), debuffs with
  no dispel category (about 400) and those of level 3 and 5 (about 140) are not removable.
- **The monsters beside the fortress give something to remove.** The ruthless worg's Rabies (poison and blindness,
  15 s), the savvy daru's Lower-body Paralysis (a snare, 4 s) and the ettins' Weaken Defense (5 s) are physical
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
The first rule that applies is taken:

1. **Itself in danger** (its own HP at or below 35%): Flash of Recovery on itself, else Healing Grace, else Healing
   Light; the Anti-Shock scroll and the life potion as today. A dead healer heals no one.
2. **A member in danger** (HP at or below 35%): Flash of Recovery, else Healing Grace, else Healing Light. The
   member with the least HP first.
3. **A debuff to remove**: a member has an effect Cleanse can remove with 3 s or more left to run. Cleanse. A snare
   on the member the monsters are on and a poison come before others.
4. **A member hurt** (HP at or below 70%): Healing Grace when the missing HP is at least what it heals and it is
   ready, else Healing Light.
5. **Several members hurt at once**: the group's heal, when it is learned and three or more members within its
   range are at or below 70%. At level 26 this rule finds no skill and passes.
6. **The member being attacked has no Light of Rejuvenation**: cast it. It is instant and costs 39 MP for 30 s.
7. **Between fights**: every member to 90% HP with Healing Light; Blessing of Guardianship on a member that lacks it;
   its own mana by the powder (MP Recovery), Penance and the mana serum, as its rest rules have them.
8. **Nothing to do**: stay in place behind the member it follows, look again in a quarter second.

The numbers are the run's parameters, as the fight table's are, and GH-10 tunes them from what the proofs show.

**Never combat.** In the mode the driver sends no attack and casts no skill at an enemy. It does not select a
monster as its target. This is one guard at the place every attack and cast is sent, so it cannot be forgotten in a
rule; a breach stops the bot. A monster that attacks the healer is not answered: the healer keeps healing, itself
included, and does not run from the group. If it dies, that is an outcome, as every death is.

**Where it stands.** It follows one member, the one the round names, and keeps between 8 and 18 m from it with a
line of sight: inside every heal's 23 m and outside a melee. It does not walk into a monster's aggro range to get
there when another way exists. When the member moves on, it follows.

**Loot.** When the server opens a corpse to the healer and the corpse holds an item of a quest the healer is
working, the healer walks to it and takes that item, by the quest-loot sweep the solo bot already has. It goes only
when no member is at or below 70% HP, and it goes at once when the member it follows moves on, so that it is not left
behind with the corpse. It takes quest items only, as every bot does.

**The members it heals.** A fighter that plays alone drinks its life potion at 90% HP and sits or uses the powder
after a fight. Beside a healer that would leave the healer nothing to do, and the two would heal the same wound. So a
member whose group has a living healer within range changes two things and no more: it keeps the potion for an
emergency (35%), and it does not rest for HP. Its shield scroll, its retreat and everything else stay. This is the
smallest part of "group stuff" the healer cannot be proved without; the rest of a fighter's group play is the next
plan's.

## Decisions and defaults

The work goes on by the default when the operator has not answered.

| | Question | Default |
|---|---|---|
| GH-Q1 | Does the healer raise a dead member (Light of Resurrection)? It was not asked for. A member that dies goes to the obelisk today, which splits the group. | **Yes, as the last item (GH-12).** The healer raises a dead member within 15 m once no member is being attacked; a dead member in a group with a living healer waits 60 s for it before it takes the obelisk. |
| GH-Q2 | Does the healer keep Blessing of Guardianship on the members? | **Yes** (rule 7). |
| GH-Q3 | The thresholds: danger at 35%, hurt at 70%, top up to 90% between fights, a debuff with 3 s left. | **As written**, tuned in GH-10. |
| GH-Q4 | The healer under attack. | **It stays, heals itself by the same rules, does not attack and does not flee.** |
| GH-Q5 | Whom it follows. | **The member the round names; the group's leader when it names none.** |
| GH-Q6 | Which classes get the mode now. | **The Cleric only.** The Chanter and the Bard have heals too; their tables are a later item, by data. |
| GH-Q7 | Are the proof rounds captured? | **No.** They are proofs. The group world stays `ntc-group-s4` until a group leg is played. |
| GH-Q8 | The healed member's two changes (potion at 35%, no rest for HP). | **Yes**, for a member with a living healer of its group within 25 m. |
| GH-Q9 | The healer's own level and gear rules. | **Unchanged**: the staff with the most magic boost, the kit, the shard, the cleanup. |

## Standing rules

- SIM only, seed 1, in the one world. A proof starts from `ntc-group-s4` and plays a round.
- Read the Java first (`../aion-server`, branch 4.8) for every server behavior an item relies on. Java wins.
- Generic first: the mode, the policy and the world model know no class. A class's support table is data.
- Natural play: what the healer knows is what a client sees. No server state is read to decide.
- No new unit tests for bot work. An item is proved by the build, the bundle and a SIM round, and its numbers are
  read from the traces by an audit script under `scripts/sim/trace/`.
- Solo play does not change. The five starter scopes of the gate stay identical after every item.
- One commit an item on `main`, code and then evidence. Nothing is pushed.

## Checklist

- [ ] **GH-01 Survey.** Read the Java for: when `SM_GROUP_MEMBER_INFO` is sent and what it carries; how HP of a
  member in sight reaches another client sooner; `Effect.broadcastHate`; `AbstractDispelEffect` and what Cleanse's
  count and power mean; `QuestService.getQuestDrop` and `DropService` for a group; the default loot rule; the
  resurrection offer and its answer. Then choose the proof ground: monsters near Morheim Ice Fortress against which
  a level-26 fighter loses 30% of its HP or more in a fight when it does not heal itself, among them one that puts a
  removable debuff of 10 s or more on it; and a quest with a drop of the one-for-the-group kind from them that no
  character has done (candidates: Q2307 "Irresistible Soup", Q2430 "Secret Information").
  - Proof: a facts table in this document, each row with its Java reference; the ground and the quest named, with
    the fights of one uncaptured round of two solo fighters there as evidence of the damage taken.
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
  action and the check of every rule it passed. The eight rules above, with the run's numbers.
  - Depends: GH-02, GH-03. Proof: the bundle. It is first exercised in GH-06.
- [ ] **GH-05 The mode.** A round's bot may be given `"role": "healer"` and `"with": "<line>"`. The fight driver
  runs the support table: the decision loop, the follow rule, the cast on a friend, and the guard that refuses every
  attack and every cast at an enemy. A group is formed at the stage's start by the invitation and its answer, as a
  client does it.
  - Depends: GH-04. Proof: the bundle; a round of the Cleric and one fighter in the fortress, grouped, with no
    fight: the Cleric follows the fighter through a walk and its trace holds no attack.
- [ ] **GH-06 Heal one fighter.** The first proof in the field: the Cleric and the Templar on the proof ground, the
  Templar hunting, with the two changes of a healed member (GH-Q8).
  - Depends: GH-05. Proof: the round, uncaptured. From the audit: no attack and no enemy cast by the Cleric; every
    time the Templar was at or below 70% a heal began within 3 s; the Templar's lowest HP; the Cleric within 23 m of
    it for 95% of the fights' time or more; the Cleric's mana at its lowest.
- [ ] **GH-07 Remove debuffs.** On the same ground against the monster with the removable debuff.
  - Depends: GH-06. Proof: the round. Of the debuffs put on a member with 5 s or more to run, how many Cleanse
    removed and how soon; none was cast at a debuff it cannot remove.
- [ ] **GH-08 Loot on its turn.** Both hold the proof quest. The Cleric loots when the server opens a corpse to it.
  - Depends: GH-06. Proof: the round. Both end with the quest's items; every item the server gave the Cleric was
    taken by the Cleric; it took nothing else; it left for a corpse only with every member above 70%.
- [ ] **GH-09 Three members.** The Cleric with the Templar and the Gladiator, both hunting.
  - Depends: GH-07, GH-08. Proof: the round. The same numbers for each member; the member in most need was healed
    first each time two were hurt at once.
- [ ] **GH-10 A long session and the numbers.** An hour of game time with the three. The thresholds are tuned once
  from what GH-06 to GH-09 showed, and the healer's mana rules are checked over many fights.
  - Depends: GH-09. Proof: the round. Deaths, each with its cause; the Cleric never out of mana in a fight for more
    than one decision; the kit used as the rules say.
- [ ] **GH-11 The healer attacked.** A fight in which a monster turns on the Cleric.
  - Depends: GH-09. Proof: a round in which it happened. The Cleric did not attack, healed itself by rule 1, stayed
    within range of the group; what became of it.
- [ ] **GH-12 Raise the dead** (GH-Q1). The healer casts Light of Resurrection on a dead member once no member is
  attacked; a dead member with a living healer in its group waits 60 s and accepts the offer.
  - Depends: GH-10. Proof: a round in which a member died and was raised; the group went on from there together.
- [ ] **GH-13 Close.** This document's closing status: what the healer does, the numbers of the proofs, what was
  found and not fixed, and what the group plan takes from here. The memory notes.
  - Depends: every item above.

## Hazards

1. **A fighter of level 26 loses little HP to the monsters of level 21 that are near.** In the fights of the muster
   that I read, the Templar stood between 91% and 96% of its HP. A healer has nothing to prove there. GH-01 has to
   find a ground that hurts.
2. **Old news.** The server sends a member's HP in the group packet on its own schedule. A heal decided on an old
   number comes late or is wasted. GH-02 takes the freshest of what it sees; GH-06 measures how late a heal starts.
3. **A cast heal lands 2 to 3 s after it is decided.** A member at 35% under three monsters can die inside a
   Healing Grace. Rule 2 takes the instant heal first for that reason, and it has a 30 s cooldown.
4. **Aggro on the healer.** Heals draw the monsters. With a fighter that holds no aggro on purpose the healer is hit
   often. The Templar's taunts are a fighter's group play and belong to the next plan; GH-11 records what happens
   without them.
5. **Line of sight and the follow.** A heal is refused through a wall or beyond 23 m. A healer that follows too far
   behind loses the member at a corner; one that follows too close stands in the fight.
6. **The loot walk.** A corpse 20 m off the path takes the healer out of range of a member that has gone on. The
   rule goes only when every member is above 70% and comes back at once.
7. **Mana.** Healing Light gives 594 HP for 75 MP. A long fight of three members empties the healer. The powder and
   Penance are for between fights; the mana serum is the one thing for inside a fight.
8. **Turns.** A healer that looks every quarter second takes four turns a game second for an hour. If a round
   becomes slow, the look is every half second while no member is attacked.

## Not in this plan

- How a group pulls, who holds aggro, who waits for whom, and how fighters assist each other: the group plan.
- The Chanter's and the Bard's support tables.
- Stigma heals, and any skill book.
- Healing a player outside its group, and a human in the group: the far goal, after the bots can play together.
- Recording the stale gate scopes again.
