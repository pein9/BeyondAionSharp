# Natural all classes to NTC-ready: the Cleric on the generic rules, every class along the Cleric's legs

Status: opened 2026-10-08. Nothing in this document is implemented. It follows the closed
class-profile plan ([natural-class-profiles.md](natural-class-profiles.md), CP-00 to CP-69)
and is worked by a loop, one NR item at a time, without the operator present. Decision D40.

## The request

The operator, 2026-10-08, after reading the closing report of the class-profile plan:

- "all classes should be able to follow the Cleric's legs just the same."
- "we need a loop that will fix the cleric to the same generic everyone will use, and get
  all characters to the same 'ready for NTC after Abyssal entry quests completed'".
- "I have to leave and you need to be able to self direct."
- On the weapons, the same day: the table under
  [After the close](natural-class-profiles.md#after-the-close-the-operators-answers-2026-10-08),
  and for the Assassin: "Yes two daggers, they don't have to match, but when we consider
  upgrades, we will have to handle replacing the worst one vs the new item."

## Start and finish

- **Start:** `main` at `df3fdb80d`. One class seam. The Priest and the Cleric are on it
  behind adapters: their fight, rest and heal decisions are still their own code
  (`NaturalPriestCombatPolicy`, the rest in the journey), not the table policy the five new
  starters use. The Cleric alone can pass the Ascension bridge and the legs after it; its
  endpoint is `morheim-abyss-entry-s1` (Cleric 26 at Morheim Ice Fortress, Q2945, Q2946,
  Q2947 and Q2042 complete). Six other characters are preserved: a Chanter after the
  Pandaemonium ceremony and a Mage, Warrior, Artist, Engineer and Scout at Munin. Twelve
  scopes stand in the neutral baseline.
- **Finish:** the Priest and the Cleric play by the same table rules as every other class,
  with no fight, rest, heal, gear or route code of their own. Every Asmodian second class
  (Cleric, Chanter, Templar, Gladiator, Sorcerer, Spirit Master, Assassin, Ranger, Gunner,
  Rider, Bard) has a character that was created by packets, played Ishalgen, fought the
  trial, chose its class, followed the Cleric's legs through Altgard and Morheim, and is
  preserved and verified as `ntc-ready-<class>-s1`: alive at Morheim Ice Fortress, level 25
  or higher, with Q2945, Q2946, Q2947 and Q2042 complete. That is the state the NTC plan
  asks of each party member.
- **Not in this plan:** the NTC instance itself, the party controller, a second character
  in one run, LIVE, Elyos, crafting and gathering, and levels past the endpoint.

## What is known at the start

From the closing status of the class-profile plan. Each point is the reason for an item.

1. The bridge and the legs are the Cleric's: the capital pass, the endpoint identity check,
   the bridge's shop, kept accessories and protected supplies, the leg-scoped identity
   rules (the seam, section 6), and the later-leg data (the Altgard and Abyss contracts,
   reward pins, the coin-gear and Haramel manifests, air combat with Smite, Hand of
   Reincarnation, the destiny leg's stigma).
2. No second class but the Cleric has a profile for level 10 on: no skill catalog, no rule
   table, no help kit.
3. The table policy has gaps the five starters showed: no answer for an attacker that is
   not the target; an obstacle refusal is answered by closing in; a melee opener is refused
   for distance on a target counted as adjacent; unlooted corpses in every run; the kit's
   potions can run out between checkpoints.
4. The off-hand mode of CP-68 is built and off. Keep-and-sell and the reward choice do not
   know the off hand, and no run has equipped an off-hand item by packets.
5. No natural character of another class has fought the trial or chosen its class. A probe
   on prepared characters shows the server lets all eleven be chosen and dispatched.
6. A possible defect shared with Java was seen and not acted on: an npc that blocks appears
   to take ten times the damage (AttackUtil.adjustDamageByStatModifiers). It is not this
   plan's to change; if a leg meets it, it is recorded.

## Decisions and defaults

The operator is away. A question with no Answer line runs on its default; the operator's
later Answer outranks the default, and an item already done on a default is then revisited
by a lettered item. Answers already given are quoted.

- **NR-Q1. Which classes, in which order?** Default: all eleven Asmodian second classes.
  The Cleric first (phase B), then the two the NTC party needs, Templar and Sorcerer, then
  Chanter, Gladiator, Assassin, Ranger, Spirit Master, Gunner, Rider, Bard.
- **NR-Q2. May the Cleric's recorded play change?** Answer (2026-10-08): "fix the cleric to
  the same generic everyone will use". So phase B changes the Priest's and the Cleric's
  play on purpose and re-records their seven scopes. Outside phase B those scopes must stay
  identical. No existing snapshot is overwritten, recaptured or re-verified; a new capture
  gets a new name.
- **NR-Q3. The endpoint.** Answer (2026-10-08): "ready for NTC after Abyssal entry quests
  completed", the same for every class. Default for its form: the facts of
  `morheim-abyss-entry-s1` that do not depend on the class, preserved as
  `ntc-ready-<class>-s1`. The Cleric's is a new capture under that name, played by the
  generic rules; `morheim-abyss-entry-s1` stays as it is.
- **NR-Q4. The route.** Answer (2026-10-08): every class follows the Cleric's legs "just
  the same": the same quests in the same order, the same hubs, binds, flights and coin
  gear tiers. What must differ is read from the class line: the class's own reward picks,
  trainer, dispatch quest, gear types and kit. A quest a class cannot take or finish is
  recorded with the Java lines and left out for that class, not replaced.
- **NR-Q5. Weapons.** Answer (2026-10-08), the table in the class-profile plan: staff for
  Cleric and Chanter; a one-hand weapon and a shield for the Templar; the best two-hand
  weapon for the Gladiator; spellbook for Sorcerer and Spirit Master; bow for the Ranger;
  two daggers for the Assassin; for Gunner, Rider and Bard the weapon their skills need,
  best damage first. Default for the Gladiator's "best": damage per second, which takes the
  greatsword over the spear at the ceremony (the loop's recommendation, not objected to).
- **NR-Q6. Two daggers.** Answer (2026-10-08): "Yes two daggers, they don't have to match,
  but when we consider upgrades, we will have to handle replacing the worst one vs the new
  item." So with two weapons held, a new weapon is compared with the worse of the two and
  takes its hand when it is better; the two need not be the same item. The Scout holds two
  from level 5, when skill 55 is observed. That changes the recorded Scout, so its scope is
  re-recorded and its later captures get new names; `munin-scout-s1` stays.
- **NR-Q7. Armor.** As the operator's gear rules of 2026-10-07 say: Priest types chain
  after Ascension, Warrior types plate, Mage types cloth; item level first, the type breaks
  ties (CP-Q24). Default for the rest: Scout types leather, Gunner leather, Rider chain,
  Bard cloth.
- **NR-Q8. Help items from level 10.** Default: every class gets the Cleric's approved
  bands (OD-13) at the same levels, with the best healing potion its level may use, the
  shield scroll and the running scroll; a class that casts from mana also gets the mana
  potions the Cleric gets, and a class that does not gets none. Each kit is written into
  this document as a manifest before its first use. No stigma stone and no skill book is
  supplied; what a quest of the route itself hands out is used.
- **NR-Q9. Runs.** Default, as CP-Q3: SIM only, seed 1, fresh-create runs allowed, each on
  its own throwaway schema that is dropped afterwards. One class run at a time.
- **NR-Q10. Unit tests.** As rule (n) of the class-profile plan: no new unit test for bot
  work. A server fix made Java-first gets the kind of test the server code beside it has.

## Standing rules

The operator rules and standing rules of the class-profile plan apply unchanged, with the
loop protocol of docs/natural-ascension-altgard.md and rules (a) to (n) of
[section 9](natural-class-profiles.md#9-rules-of-this-list), with NR in place of CP. In
short: natural play with no GM input in a journey; GM setup only in probes on the two probe
accounts, two rows to a process on different accounts; deaths, retreats and failed attempts
are recorded outcomes; the bot's death rule; the inventory check after every turn-in; no
level goals; loot every kill; bind at the working hub; hub flights from Altgard on; help
items on, never NA_HELP_ITEMS=0; no bandages; Java first, and Java wins; capture only from
committed code under new names; never branch, use worktrees, spawn subagents, push or check
out an older commit; do not touch the operator's aion stack; leave the three untracked
docs/playtest-*.md files alone.

What this plan changes or adds:

- (p) **The Cleric's scopes in phase B.** An item of phase B that means to change how the
  Priest or the Cleric plays re-records the scopes it changes, twice, by rule (j). This is
  the operator's decision NR-Q2. After phase B is closed, rule (c) guards them again.
- (q) **Items are written by the loop.** A phase starts with a survey item. It reads the
  code and the Java, writes what it found into this document, and writes the phase's
  remaining items under it, each with Depends and Proof lines, inside the phase's number
  range. A survey changes no code. An item that turns out to be several is split the same
  way. Rule (i) still covers failures.
- (r) **One class at a time, and a class may be parked.** A class's items are worked in
  order. When a class is stopped by something that needs the operator (a retail question, a
  defect shared with Java), it is written under Blocked and the next class is taken. The
  loop stops only when no item of any class can be taken.
- (s) **Generic first.** A class needs nothing the table, the profile or the class line
  cannot say. When it does, the shared form is widened for every class in a lettered item;
  no branch on a class is added. Rule (k) then re-runs the scopes of the classes that
  already use the shared code.
- (t) **A server defect.** A place where the port differs from Java is fixed Java-first in
  its own lettered item, with the bundle. A defect Java shares, or a retail question, is
  written under Blocked with the lines, and the class is parked by rule (r) if it cannot go
  round it.
- (u) **Evidence.** Under run/nr/<item>/. The gate takes -Item NR-nn. Fast runs are named
  nrNN-fast.
- (v) **The Progress log carries the state.** The last line names the next item. The loop
  reads it first and trusts the document over its own memory.

## The phases

| Phase | Items | What it does |
|---|---|---|
| A. Open | NR-00 to NR-09 | Commit this plan, survey what is the Cleric's alone, and close the tool gaps the later phases need. |
| B. The Cleric on the generic rules | NR-10 to NR-29 | The Priest and the Cleric play by the table policy, the table rest and the table gear rules alone. Their scopes are re-recorded; a generic Cleric is played to the endpoint and preserved as ntc-ready-cleric-s1. |
| C. The legs opened by class line | NR-30 to NR-49 | The trial, the class choice, the bridge and every leg take the class from the line: identity, contracts, rewards, coin gear, kit, the leg-specific skills. The Cleric's scopes stay identical. |
| D. One class after another | NR-50 to NR-149 | Ten items reserved for each class in the order of NR-Q1: profile, probes, bridge, legs, endpoint, scope. |
| E. Close | NR-150 to NR-152 | The whole check list, the full gate, the closing status and the readiness document. |

## Surveys

### Survey A1: what is the Cleric's alone, from the trial to the endpoint (NR-01, 2026-10-08)

Read: the journey (Sc/NaturalIshalgenJourney.cs and its Combat and Navigator parts), the
bridge, capital, Altgard, coin-gear, Haramel and Abyss engines and steps under
tests/Aion.Bots/Scenarios, the identity rules, the inventory policy, the help-item
allowlist, the leg contracts under parity-artifacts/e2e, scripts/sim/sim-snapshot.ps1
and the gate. Java and the quest data for the class-dependent server steps.

**What is already generic.** The trial is fought by the class's own fight rules
(FightAscensionTrialAsync calls the fight loop with ScriptedTrial set), so a natural Scout
uses its skills there. The class seam took most class literals out of the bot; what is left is in
the tables below. No leg contract's start.class is read by
code; it is data only. Of the 121 quests on the route, quest_data.xml gives a class limit to
one, the dispatch quest, which the class line already carries (Q2904 for the Priest's
classes), and per-class reward lists to four: Q2009, Q2900, Q2947 and Q28505. Every other
reward list is the same for all classes.

**Gates that name the Cleric.**

| Where | What it does today | What it must read instead |
|---|---|---|
| J: Ishalgen finish (`Require.True(... combat.IsCleric` "Ishalgen must finish as the ceremony-proven Cleric") | Refuses any other class at the end of an early-Ascension Ishalgen. | The line's second class. |
| J: the Altgard legs ("An Altgard leg needs the Cleric") and the Abyss leg ("The Abyss-entry leg needs the Cleric") | Refuse any other class at the start of a leg. | The line's second class. |
| J: CompleteAscensionLegAsync ("The endpoint character is not the bridge's Cleric") | Classifies the endpoint with the Priest line. | The run's line. |
| Sc/NaturalIshalgenDecisionLoop.cs, `returnedCleric` (class id 10) | Lets only the Cleric return to Ishalgen after the ceremony. | The line's second class id. |
| Sc/NaturalCapitalDecisionEngine.cs ("The capital pass requires the completed level-10 Cleric ceremony") | Refuses another class at the capital pass. | The line's second class and its dispatch quest. |
| Sc/NaturalJourneyIdentityRules.cs, `second == PlayerClass.CLERIC` | The Convent and the l11, l12 and ax maps are open to the Cleric only. | Any line's second class. |
| scripts/sim/sim-snapshot.ps1, `$capitalClassLines = @('priest-cleric')` and the list of seven line ids | Only the accepted line plays the capital's first pass; a second-class line id is unknown. | Every line that has a second class. |
| J: RunClericEncounterAsync (NA-23) | A diagnostic for the level-10 Cleric. | Stays the Cleric's; it is not on the route. |

**Leg data that is the Cleric's.**

| Leg | The Cleric's data | By class |
|---|---|---|
| Bridge (natural-ascension-contract.json) | SETPRO14, Q2904, the Karmic Staff; the shop (Lesser Life Elixir 162000053 and Lesser Odella Powder 169300003), the kept accessories, the protected items. ForLine passes no ceremony pick. | The pair from the class line (ForChoice does this already), the pick from the line, the powder only for a class whose catalog has a reagent skill. |
| Capital (natural-capital-contract.json) | dispatchQuestId 2904. | The line's dispatch quest. |
| Altgard legs 1 to 10 | Reward pins: CH_SHOES 114501726, CH_TORSO 110551139, STAFF 101501355 and 101501357, a chain shoulder and legs, and accessories. Each is one item of a list all classes are offered. | The class's gear rule picks from the same list; the pins stay as the Cleric's own. |
| Leg 1, 2 and later: air kills | NaturalAirCombat shoots with Smite (`SmiteIds`), from 25 m. | A role the profile names for a shot in flight; a class with no ranged skill flies into its weapon's reach. |
| Coin gear (cg) | staffItemId 101501357, three chain purchases, five chain body slots, the cloth gloves, 18 coins in and 19 at the end, the stigma skill 11504 that must not be learned. | The vendor's pieces of the class's armor type, the weapon the class holds, and counts taken from what is observed. |
| Leg 11, destiny (Q2900) | stoneItemId 140000001, stigmaSkillId 11504, the legacy reward 140000098. | Java gives four stones by class (_2900NoEscapingDestiny.java:242-259): 140000001 for Cleric, Chanter and Bard; 140000002 for Rider, Gunner and Ranger; 140000003 for Gladiator, Assassin and Templar, which needs a melee weapon; 140000004 for Sorcerer and Spirit Master. The reward list is per class. |
| Leg 12, Haramel | staffItemId and StaffObjectId 137763, the chain groups of CanUpgradeGroup, chestNpcId 700832, 19 iron and 7 bronze coins, protected and cleanup item ids. | Java spawns one of four chests by class (HaramelInstance.java:32-52): 700829 Gladiator and Templar; 700830 Assassin, Ranger and Gunner; 700831 Bard, Sorcerer and Spirit Master; 700832 Cleric, Chanter and Rider. Q28505's reward list is per class. Object ids and counts are read at the leg's start. |
| Abyss entry (ax) | Reward pins CH_TORSO 110551147 and STAFF 101501224 (Q2947's list is per class), two tiers of chain coin armor with a staff each, the staff rule by magic boost, the protected items. Start level exactly 25 with Q2945 started. | The class's armor type and weapon group at each tier, ranked by the class's own stat; the start as a minimum. |

**Rules that are the Cleric's.**

| Where | Today | By class |
|---|---|---|
| Sc/NaturalHelpItemAllowlist.cs, `Approved` | The Cleric's bands from level 10: awakening scrolls, mana serums, DP jelly and Odella powder beside the life potions, the shield scroll and the running scroll. | A kit by class (NR-Q8): the shared rows for every class, the mana rows for a class that casts from mana, the powder for a class with a reagent skill. |
| Sc/NaturalPatrolPolicy.cs | `Cleric` decides whether the patrol rule applies at all; the pack limit comes from NaturalPriestCombatPolicy.SwarmedAttackers. | Every second class, with the profile's swarm limit. |
| J: the blocked-pull view (heal, rejuvenation and salvation looked up by role) | Names three Cleric roles. | The profile's recovery roles. |
| Sc/NaturalIshalgenInventoryPolicy.cs | `IsCleric`, `Decide(..., cleric)`, the Cleric's supplies added to the bridge's protected set. | The observed class's gear rules, as the equipment check already does. |
| Leg contracts as receipts | Several legs require exact values of the accepted run: a level, a coin count, an object id. | Kept for the Cleric; for another class the same facts are read as minimums or observed at the leg's start. |

**Java, read for this survey.** _2008Ascension.java:141-162 (all eleven classes can be
chosen, CP-67). _2009ACeremonyinPandaemonium.java:136-178 (the preceptor by starter).
_2900NoEscapingDestiny.java:32 and 242-259 (four stones by class). HaramelInstance.java:
28-54 (four chests by class). quest_data.xml: class_permitted stands on Q2904 alone among
the route's quests; per-class reward lists on Q2009, Q2900, Q2947 and Q28505. No server change is expected.

**Not found.** Hand of Reincarnation 4005 is named only in the Cleric's list of skills left
out; no leg casts it. The ring course's boost skill 265 and the poison of leg 2 (skill 255)
have no class input.

### Survey A2: what the Priest and the Cleric decide in their own code (NR-02, 2026-10-08)

Read: Sc/NaturalPriestCombatPolicy.cs (the fight rule and the two hand-typed skill tables
with it and in Sc/NaturalClericSkills.cs), Sc/Classes/NaturalRotationCombatPolicy.cs (the
table rule), Sc/Classes/NaturalPriestProfile.cs (the two adapters), Sc/Classes/
NaturalRestRules.cs with NaturalPowderRestPolicy, Sc/NaturalMauPolicyParameters.cs, and the
fight loop in Sc/NaturalIshalgenJourney.Combat.cs. No server behavior is involved; no Java
was needed.

**What is already shared.** The rest is one rule for every class (NaturalRestRules, CP-17
and CP-37). Ranges, readiness, movement, campaign numbers, restock, gear, the patrol rule
and the buff kept up between fights are profile data for the Priest line as for the others.
The fight loop asks the profile's policy and does not know which kind it is, except in the
places listed last.

**What the Priest's fight rule decides that the table cannot say.**

| The Priest's and the Cleric's rule | The table today | How the table is widened, for every class |
|---|---|---|
| Heal at 55% HP against one attacker and at 70% against two or more. | One percentage a ladder step. | A step may give a second percentage for two or more attackers. |
| An emergency starts at 35%, or at 55% with two attackers on a Seasoned target, and ends ten points higher. | One emergency percentage. | The emergency may give a second entry for two attackers on a Seasoned target. |
| Salvation, paid with DP, in an emergency or at 25% HP. Flash of Recovery in an emergency only. | A skill step is tried at its percentage and in every emergency. | A step may be for emergencies only. The DP cost is already checked. |
| Healing Grace is passed over once after it was cancelled, for the shorter Healing Light. | No memory of a cancelled cast. | A cast-time recovery skill that was cancelled last is passed over for the next step. |
| The finisher: when a heal is due outside an emergency, the fight has already had one, and the target is at or below 15% HP, Smite is cast instead. | Nothing. | The rules may name a finishing role and a target percentage. |
| A mana potion when mana is below the heal's cost plus 10. | A mana potion when the cheapest attack cannot be paid. | The mana potion is also due when the recovery reserve cannot be paid. |
| Leave at 25% HP when no heal can be cast, and at 30% when neither heal nor potion is there. | One flee percentage, when no ladder step is available. | Covered: the flee percentage, 30. |
| Light of Rejuvenation is kept up while the bot is being hit. | Upkeep before the first hit, or during the fight from the moment a target is selected. | An upkeep may be for the time under attack only. |
| The opener Smite goes first while its follow-up is learned, off cooldown and payable with the reserve; otherwise it is the filler at the end. | Fixed order. | An attack role may be marked as an opener that is brought forward while a follow-up it opens can be cast after it. |
| The Holy Servant is summoned only on a target above 50% HP. | A role may depend on the bot's own HP only. | A role may be given a least target HP. |
| Against a target that attacks from range: hold while it is within 12 m or two attackers are there, when the run asks for the hold. | A stand-off holds unless the target is ranged. | The stand-off answer reads the profile's ranged hold. |
| The run's parameters (the Mau course) set the two heal percentages, the potion percentage, the finisher percentage and the extra mana reserve. | Only the extra mana reserve is read. | A ladder step and the finisher may take their percentage from a run parameter, so the tuning course works for any class. |

Not carried over, because the fight loop never reaches them: the rule's between-fights
answers (rest below 50% mana, heal below 90% HP, the blessing with no target) are the
rest's and the buff check's, and its instant life potion step reads a field the fight loop
never sets.

**The skills.** The Priest's eight rows and the Cleric's 42 more are typed by hand; every other
class has a catalog generated from the skill templates with a role per skill id. Three
members of the shared skill record decide by role name: TargetsSelf, IsPowderRest and
IsRestSkill. A generated row carries the template's own target, and the rest rule can name
its rest-only roles.

**The rest.** NaturalPowderRestPolicy names the Cleric's roles: herb and mp-recovery (cast
with Lesser Odella Powder, cancelled by any hit, sharing one cooldown), penance (HP for
mana, from 70% HP) and heal. As data: reagent skills for health and for mana, and a skill
that trades health for mana.

**The fight loop's own branches for the Priest line** (Sc/NaturalIshalgenJourney.Combat.cs,
`profile.TableDriven`): the chain is tracked with an expiry instead of the table's chain
state; the weapon swings at a fixed interval instead of the weapon's speed; the wait after
a cast leaves out the animation's last hit; the observation carries HasBlessing and
HasRejuvenation instead of the list of active effects; and `healedThisFight` is set by the
role name "heal". With the Priest line on the table these branches have one side left.

**What is tied to the old rule and goes with it.** NaturalPriestCombatPolicyTests and the
policy's cases in NaturalIshalgenPotionPolicyTests; the Mau course (SimulationMauCourseTests
and scripts/sim/run-mau-phase*.ps1), which keeps running on the run parameters the table
reads; tools/Aion.LiveBots, which reads NaturalPriestSkills.All; and the Chanter's
placeholder profile, which borrows the Priest's adapter.

**The accepted run, for scale.** altgard-rc-complete-s1 was one continuous run of
73,044,001 game ms (20 h 17 min) from creation to the Altgard endpoint
(sim-snapshot.ps1 -Action Capture -ContinuousJourney -LaterCapital, fourteen stages), and
the Abyss entry was one more capture from it. The generic Cleric is played the same way.

## NR checklist

### A. Open

- [x] **NR-00 - Commit the plan.** Depends: none
  - Work: Commit this document, its row in CLAUDE.md's "Where things live" and decision
    D40 in docs/e2e-player-simulation-plan.md. Docs only.
  - Proof: The seven pre-commit checks pass.
  - 2026-10-08: done. This document, the CLAUDE.md row, D40 and a pointer under the
    class-profile plan's closing status are committed together. The seven pre-commit
    checks pass (run/nr/NR-00/checks.log).
- [x] **NR-01 - Survey: what is the Cleric's alone, from the trial to the endpoint.**
  Depends: NR-00
  - Work: No code. Read the journey from the first walk to Munin to the end of the Abyss
    leg, the bridge contract, the Altgard, Haramel and Abyss contracts and manifests, the
    identity rules and the inventory policy. List every place that names the Priest, the
    Cleric, a Cleric skill, a Cleric item or a Cleric number, with file and symbol, and say
    for each what the class line or the profile must supply instead. Read the Java for
    every class-dependent server step on the route (trainers, class rewards, stigma and
    skill quests, class_permitted on the route's quests; the missing RIDER on 178 quests is
    already known). Write the list into this document as "Survey A1" and write the items of
    phase C from it (rule (q)).
  - Proof: The list stands in this document; phase C has its items; seven checks pass.
  - 2026-10-08: done. No code changed. "Survey A1" stands above the checklist, and phase C
    has thirteen items, NR-30 to NR-42, written from it. Seven pre-commit checks pass
    (run/nr/NR-01/checks.log).
- [x] **NR-02 - Survey: what the Priest and the Cleric do that the table policy does not.**
  Depends: NR-00
  - Work: No code. Set NaturalPriestCombatPolicy, the journey's rest and heal code, the
    buff check, the mana rules and the Cleric's leg-specific fights (air combat, the arena
    of Q2947, the ring course) beside NaturalRotationCombatPolicy, NaturalRestRules and the
    class profile. List what the table cannot say today, and for each say how the table is
    widened for every class (rule (s)). Write "Survey A2" and the items of phase B from it.
  - Proof: The list stands in this document; phase B has its items; seven checks pass.
  - 2026-10-08: done. No code changed. "Survey A2" stands above the checklist, and phase B
    has twelve items, NR-10 to NR-21, written from it. Seven pre-commit checks pass
    (run/nr/NR-02/checks.log).
- [x] **NR-03 - The off hand in keep-and-sell and in upgrades.** Depends: NR-00
  - Work: Java first (Equipment.java, as read for CP-68). The inventory policy keeps what
    the class's off-hand mode holds: one shield for mode Shield, two weapons for mode
    SecondWeapon. With two weapons held, SelectUpgrades compares a new weapon with the
    worse of the two and puts it in that hand; the two need not match (NR-Q6). Every
    profile still has mode None, so every scope stays identical.
  - Proof: One-time check, not committed, of the keep-and-sell and worst-of-two cases;
    guard: gate p and the five class scopes identical.
  - 2026-10-08: done. Every profile still has mode None.
    - **Java.** Equipment.java, as read for CP-68, and nothing new: equipping into a hand
      takes out only what that hand holds (equip and getUnequipSlots, lines 181-222), and
      an item that is already worn cannot be asked for again (equipItem, line 64). So two
      held weapons cannot change hands in one request, and a weapon taken out of the main
      hand is in the bag for the next request.
    - **Keep and sell** (Sc/NaturalIshalgenInventoryPolicy.cs, Decide). A class that holds
      a shield has SHIELD among its gear groups (Sc/Classes/NaturalClassGearTable.cs), so
      its best usable shield is kept to be worn and the others are spare. A class that
      holds two weapons keeps its two best usable one-hand weapons of its own groups; the
      second is held with the reason second-weapon. A weapon for a later level is kept
      when it beats the second, and with one weapon owned any second is kept.
    - **The reward choice** (ChooseReward). For a class that holds two weapons, a one-hand
      weapon of its groups is an upgrade when it beats the worse of the two it owns, or
      when it owns fewer than two. A shield is scored like armor for a class that holds
      one, and a weapon upgrade still comes first, which keeps CP-Q10.
    - **The equipment check** (Sc/NaturalInventoryCheck.cs, EquipAsync). A class with an
      off-hand mode looks again after a pass that wore something, twice at most, because a
      weapon the main-hand pick took out is in the bag only then. In the end the worse of
      the two held weapons has left, and the better of the pair is in the main hand unless
      it was already in the off hand. SelectUpgrades itself is CP-68's. A class with no
      mode has its one pass.
    - **One-time check, not committed** (run/nr/NR-03/check-a3.log, run with the existing
      NaturalGearPolicyTests and NaturalIshalgenInventoryPolicyTests: 18 tests pass; a1 and
      a2 failed on the check's own expectations, not on the code). On real item templates.
      Scout with two weapons, Aldelle Dagger worn, three daggers in the bag: Ulgorn's is
      held as the second weapon, the other two are sold; with no off hand all three are
      sold. With Aldelle and Ulgorn's worn, a worse dagger is sold. Warrior in shield
      mode with two shields: the level-8 one is kept to be worn and the other sold; with
      no off hand both are sold. At Q2100: a Warrior in shield mode with the Training
      Sword takes the sword; with the Aldelle Sword and no shield it takes the shield;
      with a better shield worn, the sword. The equipment check, pass after pass: with
      Mercenary and Training held and Aldelle in the bag, Aldelle goes to the main hand
      and Mercenary to the off hand in the second pass, and Training is left in the bag;
      with Aldelle and Training held, Ulgorn's replaces Training in the off hand; with
      Training in the main hand and Aldelle in the off hand, Ulgorn's replaces Training
      in the main hand; a dagger worse than both is not asked for; with no dual-wield
      skill only the main hand is filled.
    - **Not shown by the check.** The Scout's reward pick at Q2100 is the dagger with and
      without the mode, because no Asmodian quest offers a dagger beside another upgrade a
      Scout could take; the changed comparison decides only when two upgrades compete.
      No off-hand item has been equipped by packets yet: NR-04 does that.
    - **Guard, rule (c).** Run guard-a1, gate p and the five class scopes: all six
      identical to their baselines (p 37,222, mage 23,555, warrior 24,199, artist 23,104,
      engineer 24,580, scout 31,082). Bundle: the seven pre-commit checks pass,
      Aion.GameServer.Tests passes (4,675 passed, 16 skipped) and Fast passes (run nr03-fast, 11 passed).
- [x] **NR-04 - The Scout holds two daggers from level 5.** Depends: NR-03
  - Work: Turn mode SecondWeapon on for the Scout line (NR-Q6). SIM probe row on a prepared
    level-5 Scout with two daggers in the bag: both are equipped by packets and a fight is
    fought. Then the Scout's scope is re-recorded twice by rule (j).
  - Proof: The probe row passes with both hands filled, observed in the client's view; the
    Scout scope repeats in two passes; gate p and the other four class scopes identical.
  - 2026-10-08: done, every run on its first attempt. Two commits by rule (j): the code
    at 5917ca9b1 with its bundle green, then the re-recorded baseline.
    - **Java, read first.** skill_tree.xml:90: the Scout learns skill 55 at level 5, and
      its wpndual effect is what Equipment.equipItem asks for before it leaves a one-hand
      weapon in the off hand (lines 70-71, CP-68). PlayerGameStats.getBaseAttackSpeed (lines
      85-97): with a second weapon the swing takes the main weapon's speed and a quarter
      of the off-hand weapon's. PlayerController.attackTarget (lines 423-431): a swing
      that comes more than 300 ms sooner than that is dropped with no message. The port
      has the same lines.
    - **The change.** The Scout's gear table takes mode SecondWeapon
      (Sc/Classes/NaturalClassGearTable.cs). The fight loop's swing time adds a quarter of
      the off-hand weapon's speed, so two 1.2 s daggers swing every 1.5 s; nothing changes
      with an empty off hand. A probe can run the journey's ordinary equipment check
      (NaturalIshalgenJourney.RunObservedEquipmentCheckAsync).
    - **Probe, row scout-two** (run/nr/NR-04/probe-a1.log, trace beside it), on account
      98 as Asimtwodagger. Prepared: level 5, Raider's and Ulgorn's daggers put in the bag,
      a place 18 m from a Fanged Karnif. The equipment check asks for Ulgorn's Dagger
      100200604 in slot 1 and Raider's Dagger 100200125 in slot 2; the client's inventory
      and the server's equipment both show them there, and the Training Dagger is in the
      bag. The server's swing time is 1,500 ms. The Karnif is killed in 12.9 s: 5 swings
      sent and 5 carried out, two casts on the target, one on the bot and one potion; no
      death, 359 of 399 HP at the end.
    - **The journey, run journey-a1** (run/nr/NR-04/journey-a1): the Scout from creation to
      Q2004 complete, beside the scope as it was recorded:

      | | Recorded (e1ff5f857) | Two daggers |
      |---|---|---|
      | Game time | 41 min 25 s | 36 min 34 s |
      | Encounters, kills | 39, 30 | 34, 29 |
      | Swings | 278 | 205 |
      | Casts on a target | 90 | 84 |
      | Life potions in fights, in rests | 13, 5 | 11, 5 |
      | Sits for health | 1 | 0 |
      | Retreats, deaths | 3, 0 | 2, 0 |

      The equipment check wears Raider's Dagger at Q2100 as before, takes the Training
      Dagger into the off hand at 10:38, when level 5 is reached, and at 15:18 puts
      Ulgorn's Dagger in the main hand and, in the second pass, Raider's in the off hand:
      the Training Dagger, the worse of the two, is the one that leaves.
    - **Re-recorded, rule (j).** Run record-a1 at 5917ca9b1 on a clean tree
      (run/nr/NR-04/record-a1/verdict.json): two passes of scope scout, identical after
      normalization, 27,592 records, SHA-256 4cbd457f311b2cf45cb5b7ee3ea74106e98f6db4260993792cebc5a2c50a485c. The row of
      parity-artifacts/e2e/natural-neutral-baseline.json is replaced; the trace is kept
      under run/cp/baseline/5917ca9b1... The snapshot munin-scout-s1 is as it was: it
      holds the Scout who played with one dagger.
    - **Rule (k) and the guard.** Rows scout-1 and scout-7 pass as before
      (run/nr/NR-04/probe-k1.log). Guard guard-a1, gate p and the four other class scopes:
      identical (p 37,222, mage 23,555, warrior 24,199, artist 23,104, engineer 24,580).
      Bundle at the code commit: the seven pre-commit checks pass, Aion.GameServer.Tests
      passes (4,675 passed, 16 skipped) and Fast passes (run nr04-fast, 11 passed). At the
      baseline commit: the seven checks, scripts/sim/trace/test_compare_traces.py and
      scripts/sim/test-sim-snapshot.ps1.
- [ ] **NR-05 - The comparer counts the rest's potions and sits.** Depends: NR-00
  - Work: scripts/sim/trace/compare_traces.py counts rest-life-potion and
    rest-sit-for-health among its step counts, so a class scope shows them. The digest of a
    trace must not change; if it would, the item stops and says so.
  - Proof: scripts/sim/trace/test_compare_traces.py passes and every recorded scope's
    digest is unchanged (gate, set all plus the five class scopes).

### B. The Cleric on the generic rules

Written by NR-02 from Survey A2 (rule (q)). The table is widened first, with every class
playing as recorded (NR-10 to NR-13). Then the Priest moves, then the Cleric, each with its
scopes re-recorded (rule (p)). Then the old rule is removed and the generic Cleric is played
to the endpoint. The close of phase B is NR-21.

- [ ] **NR-10 - The recovery ladder can say what the Priest's heal rule says.** Depends:
  NR-02
  - Work: NaturalRotationRules and NaturalRecoveryStep are widened as Survey A2's table
    says for recovery: a second percentage for two or more attackers; an emergency entry
    for two attackers on a Seasoned target; a step for emergencies only; a cancelled
    cast-time recovery skill passed over once; the finishing role with its target
    percentage; the mana potion due below the recovery reserve; a percentage that may come
    from a run parameter. Every new value is unset in the five tables that exist, so no
    class plays differently.
  - Proof: One-time check, not committed, of each new rule on a table that sets it; guard:
    gate p and the five class scopes identical.
- [ ] **NR-11 - The attack list can say what the Cleric's rotation says.** Depends: NR-10
  - Work: An opener brought forward while a follow-up it opens can be cast after it; a
    role with a least target HP; an upkeep for the time under attack only; the stand-off
    answer reading the profile's ranged hold. Unset in the five tables.
  - Proof: One-time check of each rule; guard: gate p and the five class scopes identical.
- [ ] **NR-12 - The skill record and the rest rule without role names.** Depends: NR-10
  - Work: Whether a skill is cast on the bot comes from the catalog's target kind for
    every row, and the rest rule names its rest-only skills by kind (a reagent skill for
    health, one for mana, a skill that trades health for mana) with the numbers
    NaturalPowderRestPolicy holds. TargetsSelf, IsPowderRest and IsRestSkill no longer
    read a role name. The Priest and the Cleric rest as they did.
  - Proof: The full gate identical: the rest is unchanged for every class.
- [ ] **NR-13 - Generated catalogs for the Priest and the Cleric.** Depends: NR-12
  - Work: The Priest's and the Cleric's catalogs are generated from the skill templates
    with a role per skill id, as the other classes' are, with the reason for every active
    skill left out. A one-time check sets each generated row beside its hand-typed row
    (cost, range, cooldown group and time, chain category, required category, chain time,
    DP and reagent); every difference is written into this document with the template
    line, and the template wins. The profiles still use the old fight rule in this item.
  - Proof: The list of differences; when it is empty the full gate is identical, and when
    it is not, the scopes it changes are re-recorded (rule (p)).
- [ ] **NR-14 - The Priest on the table.** Depends: NR-11, NR-13
  - Work: Write the Priest's rule table into this document first, then build it:
    NaturalPriestProfile.Priest takes NaturalRotationCombatPolicy with the generated
    catalog. The roles, percentages and order are the old rule's, said by the table. The
    Chanter's placeholder takes the same table. SIM probe rows priest-1 and priest-7 on
    the probe accounts, as the other starters have.
  - Proof: The two probe rows pass, with the heal, the stun and the slow shown in play;
    guard: the five class scopes identical. The Priest's scopes are NR-15's.
- [ ] **NR-15 - The Priest's Ishalgen replayed and re-recorded.** Depends: NR-14
  - Work: No code beyond one small change (rule (e)). Replay scope m (the Priest from
    creation to Munin); two attempts. Compare its ledger with the old baseline's run:
    deaths, retreats, potions, game time. Then re-record p and m twice (rule (p)). Scope b
    changes too and is left out of every guard until NR-17 re-records it.
  - Proof: Scope m reaches Munin; p and m repeat in two passes each.
- [ ] **NR-16 - The Cleric on the table.** Depends: NR-15
  - Work: Write the Cleric's rule table into this document first, then build it, with the
    Cleric's rest by NR-12's kinds. SIM probe rows on prepared Clerics at levels 10, 16,
    20 and 25 against monsters of the legs: the chain Smite, Flashbolt, Divine Spark; the
    servant; the heal ladder with Salvation; Root before a retreat.
  - Proof: The four probe rows pass with those shown in play; guard: p, m and the five
    class scopes identical.
- [ ] **NR-17 - The Cleric's scopes replayed and re-recorded.** Depends: NR-16
  - Work: No code beyond one small change an attempt. Replay b, l1, c, hm and ax, each to
    its endpoint, two attempts each; a scope that fails twice becomes a lettered item with
    the step where it stopped. Compare each ledger with the old one. Then re-record the
    five twice (rule (p)).
  - Proof: Every scope reaches its endpoint and repeats in two passes.
- [ ] **NR-18 - The old rule removed.** Depends: NR-17
  - Work: Delete NaturalPriestCombatPolicy.Decide and CandidateActions, the StaticPolicy
    adapter, the two hand-typed skill tables and the one-sided branches of the fight loop
    listed in Survey A2. tools/Aion.LiveBots reads the profile's catalog. The tests of the
    deleted rule go with it; the Mau course runs on the table with the run's parameters.
    Nothing plays differently.
  - Proof: The whole solution builds and its tests pass; the full gate identical.
- [ ] **NR-19 - The generic Cleric from creation to the Altgard endpoint.** Depends: NR-18
  - Work: One continuous capture from committed code (sim-snapshot.ps1 -Action Capture
    -ContinuousJourney -LaterCapital), named altgard-complete-cleric-s1. Two attempts; a
    stop inside a leg becomes a lettered item for that leg. Acceptance from the receipt
    before Verify: a Cleric at the Altgard endpoint with the accepted run's quests
    complete; the ledger of deaths, retreats, potions and game time beside the accepted
    run's 20 h 17 min.
  - Proof: sim-snapshot.ps1 -Action Verify -Name altgard-complete-cleric-s1 passes.
- [ ] **NR-20 - The generic Cleric through the Abyss entry.** Depends: NR-19
  - Work: Capture the ax leg from altgard-complete-cleric-s1 as ntc-ready-cleric-s1. The
    leg's start facts that are receipts of the accepted run are met by the new run or
    become lettered items here; phase C opens them for other classes.
  - Proof: Verify of ntc-ready-cleric-s1: a Cleric of level 25 or higher, alive at Morheim
    Ice Fortress, with Q2945, Q2946, Q2947 and Q2042 complete.
- [ ] **NR-21 - Phase B closed.** Depends: NR-10 to NR-20
  - Work: No code. The full gate on every recorded scope. Write into this document the
    seven re-recorded baselines with their commits and record counts, and the generic
    Cleric beside the accepted one: game time, deaths, retreats and consumables.
  - Proof: Every scope identical to its baseline.

### C. The legs opened by class line

Written by NR-01 from Survey A1 (rule (q)). In this phase no class but the Cleric plays a
leg: each item is proven by the Cleric's scopes staying identical and by a one-time check,
not committed, that prints what the changed rule gives each of the eleven classes. The
first class of phase D is the first to play what this phase opens, and what it finds
becomes lettered items there. Every item depends on the close of phase B.

- [ ] **NR-30 - The class line carries the pair, the pick and the dispatch; the scripts
  accept every line.** Depends: NR-01, the close of phase B
  - Work: A class line with a second class names its ceremony pick, and ForLine passes it
    (CP-26's finding). The capital contract's dispatch quest is the line's.
    scripts/sim/sim-snapshot.ps1 takes its line ids and its capital lines from one list
    that holds every line with a second class; a snapshot of such a line records it.
  - Proof: scripts/sim/test-sim-snapshot.ps1 passes; gate p, b and l1 identical.
- [ ] **NR-31 - The gates read the line's second class.** Depends: NR-30
  - Work: The five refusals of Survey A1's first table (the Ishalgen finish, the Altgard
    and Abyss leg starts, the bridge endpoint, the capital pass), `returnedCleric` in the
    decision loop and the leg-scoped maps of the identity rules accept the second class of
    the run's line, and still refuse every other class by name. The NA-23 encounter stays
    the Cleric's.
  - Proof: One-time check of Classify and the capital decision for every line; the full
    gate identical.
- [ ] **NR-32 - Reward picks by the class's gear rule.** Depends: NR-31
  - Work: Java first: the reward lists of the route's quests, and how a per-class list is
    chosen (QuestTemplate, as read for CP-31). A leg's reward pin is the Cleric's; for
    another class the pick at the same quest is what its gear rule scores highest in the
    list the server offers it. The four per-class lists (Q2009, Q2900, Q2947, Q28505) are
    read for the class. Write the table of picks for every class and quest into this
    document.
  - Proof: One-time check that prints the table; for the Cleric the rule gives every pin;
    the full gate identical.
- [ ] **NR-33 - Protected and kept items by rule.** Depends: NR-32
  - Work: What a leg protects, keeps or cleans up by item id (the bridge, coin gear,
    Haramel, the Abyss entry) is derived for another class from its gear rules and from
    what it wears at the leg's start; the Cleric's lists stay as they are. The inventory
    policy takes the observed class's rules everywhere it took `cleric`.
  - Proof: One-time check; the full gate identical.
- [ ] **NR-34 - The help kit by class from level 10.** Depends: NR-31
  - Work: NR-Q8. The allowlist's rows get a kind: for every class, for a class that casts
    from mana, for a class with a reagent skill. The profile says which kinds its class
    takes. The Cleric's supply is what it was. Write each second class's kit into this
    document as a manifest.
  - Proof: One-time check that prints each class's kit at levels 10, 20 and 25; the full
    gate identical, with help-items.json of the Cleric's scopes unchanged.
- [ ] **NR-35 - The bridge's shop and stops by class.** Depends: NR-33, NR-34
  - Work: The Altgard shop stop buys the powder only for a class with a reagent skill and
    the potions by the class's restock rule; the kept accessories come from the gear rule.
  - Proof: One-time check; gate b identical.
- [ ] **NR-36 - A shot in flight by role.** Depends: NR-31
  - Work: NaturalAirCombat takes its skill from a role the profile names for a ranged
    attack that may be cast in flight, with that skill's range; a class with no such skill
    flies into its weapon's reach and swings. Java first: which skills may be used while
    flying (the skill templates' flight conditions).
  - Proof: One-time check that prints each starter's and second class's air attack; gate
    l1 and c identical.
- [ ] **NR-37 - The patrol rule and the blocked-pull view by profile.** Depends: NR-31
  - Work: NaturalPatrolPolicy applies to every second class with the profile's swarm
    limit, and the blocked-pull view asks the profile for its recovery roles.
  - Proof: The full gate identical.
- [ ] **NR-38 - Coin gear by class.** Depends: NR-32, NR-33
  - Work: Java first: the iron-coin and bronze-coin vendors' goods lists by armor type and
    weapon. The coin-gear leg and the Abyss entry's two tiers buy the pieces of the
    class's armor type and the weapon of its group that beat what is worn by the class's
    own stat; counts and the held weapon's identity are observed, not pinned. The Cleric's
    manifests are what they were.
  - Proof: One-time check that prints each class's manifest at each tier; gate hm and ax
    identical.
- [ ] **NR-39 - The destiny leg by class.** Depends: NR-32
  - Work: Java first (_2900NoEscapingDestiny.java). The stone, its skill and the reward are
    read by class; the instance's fight casts the temporary skill by a role. A class whose
    stone needs a melee weapon and that holds none is recorded as a finding for that class.
  - Proof: One-time check of the four stones against Java's table; the Cleric's l11 leg
    replayed from altgard-l10 with an identical outcome.
- [ ] **NR-40 - Haramel by class.** Depends: NR-33, NR-38
  - Work: Java first (HaramelInstance.java). The chest by class, the upgrade groups by the
    class's armor type, Q28505's list by class, and the object ids and coin counts read at
    the leg's start.
  - Proof: One-time check of the four chests against Java's table; gate hm identical.
- [ ] **NR-41 - Leg starts as minimums, and one run through every leg for a line.**
  Depends: NR-31 to NR-40
  - Work: A leg's start facts that are receipts of the accepted run (an exact level, coin
    count or journal) are minimums for another line. The continuous journey takes a class
    line and plays the legs in the Cleric's order; a capture after a leg is named
    <the Cleric's snapshot name>-<class>.
  - Proof: scripts/sim/test-sim-snapshot.ps1 passes; the full gate identical.
- [ ] **NR-42 - Phase C closed.** Depends: NR-30 to NR-41
  - Work: No code. The full gate on every recorded scope, and one table in this document
    of what each class gets at each class-bound point of the route.
  - Proof: Every scope identical to its baseline.

### D. One class after another

For each class, in the order of NR-Q1, ten numbers are reserved: Templar NR-50, Sorcerer
NR-60, Chanter NR-70, Gladiator NR-80, Assassin NR-90, Ranger NR-100, Spirit Master NR-110,
Gunner NR-120, Rider NR-130, Bard NR-140. The class's survey item writes the others. The
template:

- [ ] **NR-x0 - <Class>: survey and profile data.** Depends: the close of phase C
  - Work: Java first: the class's skill tree from level 10 to 26, its trainer, its class
    rewards on the route, its stigma or skill quests. Write into this document, before any
    run: the class line (starter, second class, ceremony pick by NR-Q5), the gear table
    (weapon groups, off-hand mode, armor order), the help kit from level 10 as a manifest
    (NR-Q8), the roles of its skills and its rule table with the reason for every skill
    left out. Then write the class's remaining items.
  - Proof: The profile validator accepts the profile; seven checks pass.
- **NR-x1 - Probe rows:** prepared characters at levels 10, 16, 20 and 25 fight monsters of
  the legs they will meet; the rules are shown in play.
- **NR-x2 - To Altgard:** a fresh character through Ishalgen, the trial, the class choice,
  the ceremony and the dispatch, captured at the Altgard bind as altgard-<class>-s1.
- **NR-x3 to NR-x6 - The legs:** the Altgard legs, coin gear and Haramel, and the Abyss
  entry, each captured under <the Cleric's snapshot name>-<class>.
- **NR-x7 - The endpoint:** captured and verified as ntc-ready-<class>-s1.
- **NR-x8 - The class scope:** recorded twice.

- [ ] **NR-50 - Templar: survey and profile data.** Depends: the close of phase C
- [ ] **NR-60 - Sorcerer: survey and profile data.** Depends: the close of phase C
- [ ] **NR-70 - Chanter: survey and profile data.** Depends: the close of phase C
- [ ] **NR-80 - Gladiator: survey and profile data.** Depends: the close of phase C
- [ ] **NR-90 - Assassin: survey and profile data.** Depends: the close of phase C, NR-04
- [ ] **NR-100 - Ranger: survey and profile data.** Depends: the close of phase C
- [ ] **NR-110 - Spirit Master: survey and profile data.** Depends: the close of phase C
- [ ] **NR-120 - Gunner: survey and profile data.** Depends: the close of phase C, NR-03
- [ ] **NR-130 - Rider: survey and profile data.** Depends: the close of phase C
- [ ] **NR-140 - Bard: survey and profile data.** Depends: the close of phase C

### E. Close

- [ ] **NR-150 - Final gate and close-out.** Depends: every item above, or its class parked
  under Blocked
  - Work: No code. The whole check list of CLAUDE.md, the full gate on every recorded
    scope, the closing status at the head of this document (each class's endpoint, deaths
    and consumables, every lettered item, every parked class with its reason, the findings
    not fixed), and the update of docs/natural-ntc-readiness.md.
  - Proof: Every scope identical to its baseline.

## Blocked / questions for the operator

Nothing is blocked at the start.

## Loop prompt

```
Work the NR checklist in docs/natural-all-classes-ntc.md, one item per iteration.
The operator is away: decide by the document's defaults and go on.

Repository: C:\Users\ryanf\Documents\GitHub\BeyondAionSharp, branch main.

EACH ITERATION
1. Orient. Read CLAUDE.md, then docs/natural-all-classes-ntc.md: Start and finish,
   Decisions and defaults, Standing rules, the checklist, Blocked, and the last Progress
   log lines. The rules of docs/natural-class-profiles.md (section 9, rules (a) to (n),
   its Standing rules and operator rules) apply with NR in place of CP, and rules (p) to
   (v) of this document outrank them. Run git status. Check for running SIM processes.
   Never build, run checks or start a run while another process holds the build outputs.
   Apply the re-record rule: a commit from outside this list under src, tests,
   game-server or parity-artifacts means the full gate at HEAD before any item.
2. Pick the first unchecked NR item whose Depends are ticked and that is not under
   Blocked. A survey item writes its phase's items (rule (q)). A parked class is skipped
   (rule (r)). If no item can be taken, stop and report.
3. Read the Java first (../aion-server, branch 4.8) for every server behavior the item
   relies on. Java wins. A port-only difference is fixed Java-first in a lettered item; a
   defect shared with Java or a retail question goes under Blocked (rule (t)).
4. Do only that item. Generic first (rule (s)): widen the table, the profile or the class
   line for every class; add no branch on a class.
5. Run the item's one proof. A journey item has two attempts; a fix inside it is one small
   change; anything larger, and any second failure, becomes a lettered item (rule (i)).
   Rule (k) re-runs the scopes of the classes that use changed shared code. Rules (j) and
   (p) say when a scope is re-recorded. Keep failed evidence under run/nr/.
6. Before committing: the seven pre-commit checks; the Aion.GameServer.Tests project when
   tests/Aion.Bots changed; the script tests when scripts/sim changed; Fast unless the
   commit is docs or evidence only; and the guard of rule (c) on every recorded scope the
   item must not change.
7. Record: tick the box, add a dated evidence line with run ids and numbers, append one
   Progress log line that names the next item.
8. Commit on main: only the item's files, one commit per item (code, then evidence for a
   capture), imperative subject, the NR id and the evidence in the body, no co-author or
   attribution trailer. Never push.

RULES THAT DO NOT BEND
- SIM only, seed 1. No LIVE run. Do not touch the operator's aion stack. Every run uses
  its own throwaway schema and drops it.
- Natural play: no GM input in a journey apart from the approved help items. GM setup only
  in probes on the two probe accounts. Deaths, retreats and failed attempts are outcomes.
- Never overwrite, recapture or re-verify an existing snapshot. New captures get new names.
- Never branch, use worktrees, spawn subagents, push, or check out an older commit. Do not
  hand-edit generated data or raise the warning baseline. No new unit tests for bot work.
- Leave docs/playtest-aethertapping-plan.md, docs/playtest-crafting-alchemy-cooking-plan.md
  and docs/playtest-ground-essencetapping-plan.md alone.
- Keep the bot monitor at http://127.0.0.1:17880/ available during runs.

STOP when NR-150 is ticked, when no item can be taken, or when the full gate fails and its
first difference does not point at one item. When you stop, send one notification and
report what was done, what is parked or blocked, and what the operator must decide.
```

## Progress log

- 2026-10-08 — Plan written from the closing status of the class-profile plan and the
  operator's answers of the same day. Next: NR-00.
- 2026-10-08 — Loop: NR-00 done. The plan, its CLAUDE.md row and decision D40 are committed;
  seven checks pass. Next: NR-01, the survey of what is the Cleric's alone.
- 2026-10-08 — Loop: NR-01 done. Survey A1 is written: seven gates name the Cleric, the leg
  contracts hold its reward pins, coin gear, destiny stone and Haramel chest, and Java gives
  four stones and four chests by class; only Q2009, Q2900, Q2947 and Q28505 have per-class
  reward lists and only the dispatch quest has a class limit. The trial is already fought by
  the class's own rules. Phase C has its items, NR-30 to NR-42. Docs only; seven checks pass.
  Next: NR-02, the survey of the Priest's and the Cleric's own fight and rest code.
- 2026-10-08 — Loop: NR-02 done. Survey A2 is written: what the Priest's fight rule decides
  that the table cannot say yet (heal by attackers, the emergency on a Seasoned
  target, emergency-only steps, a cancelled heal passed over, the finisher, the opener
  brought forward, the servant's target HP, upkeep under attack, the ranged hold, the run's
  parameters), three role-name reads in the skill record, the powder rest's roles, and five
  one-sided branches of the fight loop. The rest, the ranges and the gear are already
  shared. Phase B has its items, NR-10 to NR-21. Docs only; seven checks pass. Next: NR-03,
  the off hand in keep-and-sell and in upgrades.
- 2026-10-08 — Loop: NR-03 done. Keep-and-sell keeps a shield for a class that holds one and
  the two best one-hand weapons for a class that holds two; the reward choice and gear for
  a later level compare a weapon with the worse of the two; the equipment check looks again
  after a pass that wore something, so the worse of two held weapons is the one that
  leaves. One-time check on real items, not committed. Every profile still has mode None;
  guard guard-a1, p and five class scopes: identical. Seven checks, unit suite and Fast
  (nr03-fast) pass. Next: NR-04, the Scout holds two daggers from level 5.
- 2026-10-08 — Loop: NR-04 done. The Scout's table takes the second weapon. Probe row
  scout-two: a level-5 Scout puts two daggers on by packets, the server's swing time is
  1,500 ms and every swing sent is carried out. The journey to Q2004 with two daggers takes
  36 min 34 s against 41 min 25 s, with 205 swings against 278 and no death. Scope scout
  re-recorded at 5917ca9b1, two passes identical, 27,592 records; munin-scout-s1 is
  untouched. Guard: p and the four other class scopes identical. Next: NR-05, the comparer
  counts the rest's potions and sits.
