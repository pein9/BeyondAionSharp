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
  its own throwaway schema that is dropped afterwards. One class run at a time. Answers
  (2026-10-09), on that last sentence:
  - "add it as an item before phase D, and safely do parallel work when possible";
    "being able to run multiple bots at the same time is a major requirement/spec. Better
    to build for that sooner than later!"; "Don't stop anything currently running."
  - Asked whether that means separate worlds or one: "We don't need to do 'group' work,
    but we can use the same world and run multiple bots in it. If you need to, keep the
    bots 10 minutes apart so one can get progress and move on before the next starts".
    And how to use it: "run multiple bots (multiple class testing even at a time), log
    what happened for each as you go, when all are completed then make code changes, then
    do another multi-bot in 1 world run."
  - Asked whether a capture must still be one bot in its own world: "I would rather accept
    captures straight out of a shared world."

  So: several bots, each playing alone, in one world and one run; no party play. The loop
  works in rounds by rule (w), from the tick of NR-44 on, and a class's captures, its
  `ntc-ready-<class>-s1` among them, are taken from a round.
- **NR-Q10. Unit tests.** As rule (n) of the class-profile plan: no new unit test for bot
  work. A server fix made Java-first gets the kind of test the server code beside it has.
- **NR-Q11. How many bots in one world?** Asked by Survey C1, which found that bots in one
  world take turns on one thread: a world of N bots takes about as long as N runs one
  after another and saves memory, not time, while separate worlds side by side save time.
  Default: a round spreads its bots over as many worlds as memory takes, eight on this
  machine, so eleven classes play one or two to a world. One world for all is used when
  the point is to see bots beside each other. The operator's later Answer outranks this.

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
- (w) **Several bots at once** (the operator's answers to NR-Q9, 2026-10-09; Survey C1).
  Each part holds from the tick of its item; until then one bot and one run at a time, and
  no build, check or run while another process holds the build outputs.
  - **A round.** Several bots, of several classes when that helps, play at the same time.
    Each plays alone: no party, no trade, no help between them. A round is one world or
    several worlds side by side, each with one bot or more (NR-Q11). Bots in one world
    start some game minutes apart (the operator's figure: ten) so that one has moved on
    before the next arrives. Each bot has its own trace and its own outcome record. A bot
    that stops does not stop the others. Code is changed after the round, when every bot
    has finished or stopped, and then the next round is played.
  - **What a round is for.** Finding what to fix (rotation, recovery, route, gear), and,
    by the operator's answer, the captures: a character that reaches a leg's end or the
    endpoint in a round is captured from that world and verified there. No second run
    alone is asked of it.
  - **What stays one bot in its own world.** The recorded scopes and the gate. Their
    baselines were recorded by a bot alone, and a bot beside others does not meet the same
    monsters or the same dice, so a round is never compared with a recorded scope.
  - **Separate runs side by side** (NR-45): gate scopes, and the worlds of a round, each
    in its own world and process. Eight at once on this machine.
  - **While any run is going.** It is never stopped to make room. Nothing is built, and no
    check that builds is run. Nothing under game-server/data, parity-artifacts or the
    bots' data is edited. Source may be edited. The runs of one batch start from one build.
  - **What counts.** A result got beside other runs counts as a solo result only where the
    same run alone gives the same trace; NR-45 proves that for the gate.

  This rule amends rule (r)'s "one class at a time" and two lines of the loop prompt, the
  one on the build outputs and the one on the monitor's port, and nothing else.

## The phases

| Phase | Items | What it does |
|---|---|---|
| A. Open | NR-00 to NR-09 | Commit this plan, survey what is the Cleric's alone, and close the tool gaps the later phases need. |
| B. The Cleric on the generic rules | NR-10 to NR-29 | The Priest and the Cleric play by the table policy, the table rest and the table gear rules alone. Their scopes are re-recorded; a generic Cleric is played to the endpoint and preserved as ntc-ready-cleric-s1. |
| C. The legs opened by class line | NR-30 to NR-49 | The trial, the class choice, the bridge and every leg take the class from the line: identity, contracts, rewards, coin gear, kit, the leg-specific skills. The Cleric's scopes stay identical. First in the phase: several bots at once (NR-43 to NR-47). |
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

### Survey C1: several bots at once (NR-43, 2026-10-09)

No code. Read: the virtual clock (tests/Aion.GameServer.TestKit/VirtualThreadPool.cs), the
in-process transport (tests/Aion.Bots/Transport/InProcessBotTransport.cs), the SIM session
(SimulationL0Session in tests/Aion.Simulation.Tests/SimulationFastScenarioTests.cs), the
journey's test (SimulationNaturalIshalgenJourneyTests.cs), the world fixture
(SimulationWorldFixture.cs), the monitor (tests/Aion.Bots/Dashboard/LiveBotDashboard.cs)
and the two run scripts. Measured with a one-time probe that is not committed; its output
is under run/nr/NR-43/.

**1. How a run uses time today.**

- One world is one process, and after loading it plays on one thread. The clock is
  VirtualThreadPool. Advance(by) runs every due server timer in order on the calling
  thread, and it refuses a second caller.
- A bot's packet is handled by the server at once, inside the send. Time does not move.
- Time moves only when a bot waits. Every wait ends in one delegate, made in
  SimulationL0Session.OpenAsync: `elapsed => fixture.Clock.Advance(elapsed)`. Two more
  places move the clock directly, both around a relog: AdvanceOfflineAsync and
  WaitForReentryAsync.
- All of it completes at once. A journey never yields, so two journeys started as two
  tasks would play one after the other, not together.
- Several sessions in one world exist already: the social, trade and gathering scenarios
  log several characters into one world and step them by hand.

**2. What it costs, measured.**

| What | Wall time | Memory |
|---|---|---|
| Loading a world | about 41 s of processor time, on many threads | 2.4 GB |
| An empty world, one game hour | 5.9 s | |
| One character standing in Ishalgen, one game hour | 7.9 s | |
| Six characters standing there, one game hour | 8.2 s | about 20 MB more for each |
| Scope mage: Ishalgen to Q2004, 30.7 game minutes | 30.7 s, of it 5.1 s in the clock and 2.1 s in sends | peak 2.7 GB |
| Scope c: Altgard leg 4, 176.5 game minutes | 310.6 s, of it 39.8 s in the clock and 8.7 s in sends | peak 2.9 GB |
| NR-19's whole journey, 19 h 20 min of game time | about 68 minutes | |

The clock and the sends are the server's work. The rest is on the bot's side: reading and
decoding packets, the bot's world model, decisions, routes, the trace and the monitor.

- **The server's work is 16 to 24% of a run's wall time. The bot's side is 76 to 84%.**
- This machine: 20 cores (28 logical), 63.8 GB of memory, 30 GB free when measured.

**3. What that means for time.**

- Bots in one world take turns on one thread. A world with N bots takes about as long as
  the N runs one after another: it saves one world load each (41 s) and a share of the
  server's work, and no more. It saves memory: one world of 2.4 GB, not N. Eleven classes
  from creation to the endpoint in one world would be about ten hours of wall time.
- Separate worlds side by side each take one thread. Eight fit this machine's free memory
  with room to spare. Eleven classes in separate worlds are two batches of 70 to 80
  minutes.
- So one world is the way to see bots beside each other and to save memory, and separate
  worlds are the way to save time. A round can be both: several worlds side by side, each
  with one bot or more (NR-Q11).
- Four fifths of a run being the bot's side is a finding of its own. NR-46 looks where it
  goes; whatever is saved there is saved for every run and every round.

**4. Taking turns in one world: the design for NR-44.**

- One turn table for a world owns the clock. A bot's wait becomes "wake me at now + dt"
  and yields. When every bot is waiting or done, the table moves the clock to the earliest
  wake and lets that one bot play until its next wait. Bots that are due at the same
  instant go in the order of the round's list. One bot plays at a time, always on the same
  thread, so a world's play does not depend on the machine.
- The seam is small: the delegate in OpenAsync, the two direct calls around a relog, and
  InProcessBotTransport.AdvanceAsync, which must be able to wait (today it calls the
  delegate under a lock and returns done).
- With one bot the table moves the clock inside the call, so the same calls are made in
  the same order as today. The proof is the full gate identical.
- The world's bots run on one thread of their own with its own context, so that a bot
  never continues on another thread.
- A real wait inside a turn (a file write) only holds the table until that bot waits
  again.

**5. What each bot needs of its own.**

| Need | Today |
|---|---|
| Account and name | Each class line has its own (NaturalClassLine.SimAccountId, CharacterName). One bot of a line in a world. |
| Trace and receipts | The trace takes a bot id, and every receipt and the failure record are written beside the trace. A folder for each bot keeps them apart. |
| Monitor | LiveBotDashboardState keeps a row for each bot id already. One host and one port serve a world. |
| Runtime | NaturalJourneyRuntime is made for one journey. Nothing to change. |
| Options | Built from the process's environment variables in the journey's test (CP_CLASS, AF_ALTGARD, NA_ASCENSION and more). A round gives them for each bot from a round file. |
| Bot id | "b01" is fixed in the test. One for each bot. |
| Help supply | Counted for one journey in the test. One count for each bot. |
| Static state | The bot library has none that changes, apart from the profile table filled once from static data. |

- **A bot that stops.** A journey that throws ends its own task. The table drops it and
  the others go on. Its session is closed, so the character is saved where it stood.
- **Server problems.** The problem policy is one for the process, and the journey checks
  it nine times. As it is, one bot's server problem would fail every bot's next check. A
  problem raised inside a bot's send is that bot's. One raised while the clock moves is
  the world's: it is written into the round's record and ends that world's round.
- **Wall-clock limits.** A packet wait gives up after three minutes of real time; that
  holds inside a turn. The journey's test gives one journey 45 minutes, or 90 for the
  continuous one; a world of several bots needs a limit sized by its bots.

**6. What bots in one world share.**

- The random stream, one for the process. Another bot's fights change this bot's rolls.
- Monsters and their spawn timers, quest objects, named quest spawns. The trial, Haramel
  and the arenas are instances of one player.
- The start. Every Asmodian starter begins at the same place with the same first quests,
  so the ten game minutes between starts matter most in Ishalgen. Later the classes pass
  the same hubs at their own pace and will meet. What happens then is an outcome to log.
- Not read here: who gets a quest's kill when two players hit one monster. NR-44 reads the
  Java for it first.

**7. The round.**

- A round file lists the bots: class line, start offset in game minutes, where the bot
  starts (fresh, or a character of a round snapshot), and how far it plays (a stage, as
  the scopes name them today). It also says which world each bot is in.
- The runner starts the worlds side by side. Each world plays its bots by turns.
- Logged for each bot as it goes: one line for each stage it ends, as
  continuous-progress.json has today; and at its end one outcome record: reached or
  stopped, with the message and step of a stop, level, quests, deaths, game time and the
  ledger counts.
- The round ends when every bot has finished or stopped. Code is changed then, not before.

**8. Captures from a round** (the operator's answer to NR-Q9).

- A world's schema holds its bots' characters. A bot that reaches its stage's end logs
  out, which saves it there, and stays out.
- When the world's last bot is done the schema is dumped once: a round snapshot, with a
  record that lists each character: line, character id, the stage reached with its
  verified receipt, or "stopped short" with its last observation.
- A class's named capture, ntc-ready-<class>-s1 among them, is a small record that points
  at the round snapshot and the character in it. The dump is not copied.
- Verify of such a capture restores the dump and resumes that one character, as today.
- A later round resumes the characters of one dump together in one world. Characters of
  two dumps cannot be put into one world, so a world's bots stay together from round to
  round, or a character goes on alone.
- A character that stopped short is in the dump as it was saved. After the fix the next
  round resumes it there; if it was saved in a state it cannot leave, it is played again
  from the round snapshot before.
- sim-snapshot.ps1 takes one character today. The round snapshot gets actions of its own.

**9. Does a round repeat?** Nothing in a world reads the wall clock for play, the random
stream is seeded and the turn table is fixed, so a world played twice should give the same
traces. NR-44 proves it. If it holds, a round can later be recorded as a baseline.

**10. Separate runs side by side.**

| | Finding |
|---|---|
| Schema, scratch folder, evidence, trace | Each run has its own already (a GUID, the process id, the run's name). |
| Monitor port | In the way. Every run opens 17880, the scripts clear an inherited port, and a second listener on the port throws at its start. NR-45: the first free port from 17880, printed and written into the run's evidence. |
| Build outputs | Many runs can read them. A build while a run is going fails on locked files or changes what a later run loads. NR-45: a run in progress is marked, the scripts refuse to build while a mark is live, and the runs of a batch start from one build. |
| Read from the repository while running | Static data at the world's load; leg contracts and plans at each leg's start (parity-artifacts/e2e); inventory and navigation data as they are used. While a run is going nothing under game-server/data, parity-artifacts or the bots' data is edited. Editing source does not touch a running run. |
| A run from its own copy of a build | Possible: the repository is found by walking up from the binaries, so a copy under run/ works. It would let builds go on during a long run, but the data would still be read from the checkout. Not built now; it becomes an item only if waiting for builds is what holds the loop up. |
| Wall-clock limits | Three minutes for a packet wait, 45 or 90 minutes for a journey, 30 seconds to 5 minutes in the Fast scenarios. With one thread to a run and 20 cores, eight runs do not starve each other. NR-45 proves it by the gate identical. |
| The development MySQL | One pool to a run. Not measured; eight runs are few connections. |

**11. Numbers for this machine.** Eight worlds at once: 30 GB free against a peak of 2.9 GB
for a world, with room left. A world takes many bots for memory, but each bot adds its own
bot-side time to that world's wall time.

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
- [x] **NR-05 - The comparer counts the rest's potions and sits.** Depends: NR-00
  - Work: scripts/sim/trace/compare_traces.py counts rest-life-potion and
    rest-sit-for-health among its step counts, so a class scope shows them. The digest of a
    trace must not change; if it would, the item stops and says so.
  - Proof: scripts/sim/trace/test_compare_traces.py passes and every recorded scope's
    digest is unchanged (gate, set all plus the five class scopes).
  - 2026-10-08: done. No server behavior is involved.
    - **The change.** compare_traces.py --counts has two more counts, after lifePotions:
      restLifePotions (rest-life-potion records) and restSitsForHealth
      (rest-sit-for-health records). The digest and the comparison do not read the counts
      and are untouched. The existing counts test in test_compare_traces.py has three more
      records and the two new names; no test was added.
    - **What the counts show on the recorded traces** (run/nr/NR-05/stored-digests.txt,
      read from run/cp/baseline): the seven Priest and Cleric scopes have neither record,
      as they heal by skill. Mage 3 rest potions and 1 sit for health, Warrior 5 and 0,
      Artist 1 and 0, Engineer 5 and 1, Scout 5 and 0. The counts stored in the baseline
      file's rows are from each row's own recording and gain the two names when a row is
      recorded again; the gate compares the digest, not the counts.
    - **Proof.** test_compare_traces.py passes (10 tests). With the changed script, the
      digest of every stored baseline trace equals its row. Gate, set
      all+mage+warrior+artist+engineer+scout, run gate-a1
      (run/nr/NR-05/gate-a1/verdict.json): verdict pass, all twelve scopes identical to
      their baselines (p 37,222, m 112,397, b 144,048, l1 30,693, c 96,166, hm 39,564, ax
      15,762, mage 23,555, warrior 24,199, artist 23,104, engineer 24,580, scout 27,592).
      This is also the first full gate since NR-03 and NR-04: the Cleric's legs play as
      recorded with the off-hand rules in the inventory policy.
    - Bundle: the seven pre-commit checks, test-sim-snapshot.ps1 and Fast (run nr05-fast,
      11 passed) pass.
    - Phase A is closed. Phase B starts with NR-10.

### B. The Cleric on the generic rules

Written by NR-02 from Survey A2 (rule (q)). The table is widened first, with every class
playing as recorded (NR-10 to NR-13). Then the Priest moves, then the Cleric, each with its
scopes re-recorded (rule (p)). Then the old rule is removed and the generic Cleric is played
to the endpoint. The close of phase B is NR-21.

- [x] **NR-10 - The recovery ladder can say what the Priest's heal rule says.** Depends:
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
  - 2026-10-08: done. No server behavior is involved. Sc/Classes/
    NaturalRotationCombatPolicy.cs alone is changed; no profile sets a new value.
    - **A ladder step** (NaturalRecoveryStep) may now give: a second percentage for two or
      more attackers (HpPercentMultiple); that it is for emergencies only (EmergencyOnly);
      that a skill cancelled last is passed over for the next step until another cast
      completes (PassOverWhenCancelled, read from the fight loop's last cancelled cast);
      that the finisher may be cast in its place (FinishInstead); and that the run's heal
      or life potion percentage replaces its own (FromRun). A table that takes a number
      from the run must state the run's baseline, so the table still says what is played;
      the policy refuses one that does not.
    - **The rules** (NaturalRotationRules) may now give: a second emergency entry for two
      or more attackers on a Seasoned target, ending as far above it as the ordinary
      emergency ends above its own (EmergencySeasonedPairPercent); a finisher, the attack
      cast in place of a marked step once the fight has had a recovery cast and the target
      is at or below a percentage, which the run may replace (Finisher); the recovery role
      whose cost is kept back from attacks, where today it is the first learned skill of
      the ladder (ReserveRole); and a margin over that cost below which a mana potion is
      drunk (ManaPotionReserveMargin).
    - **Not carried over from the Priest's rule.** With a raised potion percentage, the
      old rule kept the baseline percentage in three cases (no attacker yet, one attacker
      on a target below 60% HP, the stun ready). They apply only to a tuning run and are
      left out; at the baseline parameters the potion is drunk at 90% as before. The old
      rule also asked for the mana potion before it left at critical HP; the table leaves
      first.
    - **One-time check, not committed** (run/nr/NR-10/check-a1.log, 21 cases, all as
      expected on the first run), on the Cleric's own skill rows at level 25 with a table
      that sets every new value. At 60% HP the bot attacks against one attacker and casts
      Healing Grace against two; at 54% it casts Grace, and Healing Light when Grace was
      cancelled last. In an emergency it casts Salvation while DP pays for it and Flash of
      Recovery when not; Flash is not cast at 54% outside an emergency; Salvation is cast
      at 24%. With a heal already cast in the fight and the target at 10% it casts Smite,
      at 20% Grace, and Smite again when the run finishes at 25%; without an earlier heal,
      or in an emergency, it does not finish. The run's heal percentage of 65 makes it heal
      at 60%, and the run's potion percentage of 80 keeps the potion at 85%. With 70 MP
      against a heal of 65 and a margin of 10 it drinks the mana potion; with no margin it
      swings. The emergency is 55 to 65 against two on a Seasoned target and 35 to 45
      otherwise. A table that states 60 for the run's heal percentage, and one that marks
      a step for a finisher it does not name, are refused by name.
    - **Guard, rule (c).** Run guard-a1, gate p and the five class scopes: all six
      identical to their baselines (p 37,222, mage 23,555, warrior 24,199, artist 23,104,
      engineer 24,580, scout 27,592). Bundle: the seven pre-commit checks pass,
      Aion.GameServer.Tests passes (4,675 passed, 16 skipped) and Fast passes (run nr10-fast, 11 passed).
- [x] **NR-11 - The attack list can say what the Cleric's rotation says.** Depends: NR-10
  - Work: An opener brought forward while a follow-up it opens can be cast after it; a
    role with a least target HP; an upkeep for the time under attack only; the stand-off
    answer reading the profile's ranged hold. Unset in the five tables.
  - Proof: One-time check of each rule; guard: gate p and the five class scopes identical.
  - 2026-10-08: done. Sc/Classes/NaturalRotationCombatPolicy.cs alone is changed; no
    profile sets a new value.
    - **Java, as the old rule cites it.** Any other chain's first step resets an open
      chain (ChainCondition.shouldReset, read for CP-36 and CP-48), which is why an opener
      is brought forward only when its follow-up can follow at once. Nothing new on the
      server is relied on.
    - **The rules may now give:** openers, attack roles brought to the front of the list
      while a follow-up of the list that they open is off cooldown and opener, follow-up
      and reserve can all be paid, and left in their place otherwise (Openers); a least
      target HP for a role, at or below which it is left out (OnlyWhileTargetAbove); a
      fight upkeep that is cast only while the bot is being attacked (UnderAttackOnly on
      an upkeep); and a distance for the ranged hold: with nothing to cast or swing at a
      target that attacks from range, the bot holds while the profile's ranged hold is on
      and the target is within that distance or two attackers are there
      (RangedHoldWithin).
    - **One-time check, not committed** (run/nr/NR-11/check-a1.log, 21 cases, all as
      expected on the first run), on the Cleric's skill rows at level 25 with a table that
      sets every new value, beside the same table without them. With everything ready
      Smite goes first, at melee and at 20 m; without an opener in the table the Holy
      Servant does. With Flashbolt cooling down the servant is summoned, and Infernal
      Blaze on a target at 40% HP; without the least target HP the servant is summoned at
      40% too. At 162 MP Smite is not brought forward and at 163 it is (Smite 52,
      Flashbolt 46, the heal's 65). With Smite's chain open Flashbolt is cast. Under
      attack with no heal over time observed, Light of Rejuvenation is cast; on a target
      not yet pulled it is not, where a plain fight upkeep casts it; with it observed it
      is not cast again. With nothing payable: at 10 m from a ranged target the bot waits,
      and walks up without a hold in the table, with the profile's hold off, or at 20 m
      against one attacker; at 20 m against two it waits. A table that names an opener its
      catalog does not hold is refused by name.
    - **Guard, rule (c).** Run guard-a1, gate p and the five class scopes: all six
      identical to their baselines (p 37,222, mage 23,555, warrior 24,199, artist 23,104,
      engineer 24,580, scout 27,592). Bundle: the seven pre-commit checks pass,
      Aion.GameServer.Tests passes (4,675 passed, 16 skipped) and Fast passes (run nr11-fast, 11 passed).
- [x] **NR-12 - The skill record and the rest rule without role names.** Depends: NR-10
  - Work: Whether a skill is cast on the bot comes from the catalog's target kind for
    every row, and the rest rule names its rest-only skills by kind (a reagent skill for
    health, one for mana, a skill that trades health for mana) with the numbers
    NaturalPowderRestPolicy holds. TargetsSelf, IsPowderRest and IsRestSkill no longer
    read a role name. The Priest and the Cleric rest as they did.
  - Proof: The full gate identical: the rest is unchanged for every class.
  - 2026-10-08: done. No server behavior is involved.
    - **The skill record** (NaturalPriestSkill) no longer has TargetsSelf, IsPowderRest
      and IsRestSkill. The table rule casts a skill on the bot when the template's first
      target is the caster, or a friend or the caster, and no longer falls back to a role
      name. The old static rule, which is the only user of the hand-typed rows and goes
      with them in NR-18, says the same three things by role in its own code.
    - **The rest rule** (Sc/Classes/NaturalRestRules.cs) gains NaturalRestSkills: the
      rest-only skills by kind (a reagent skill for health, one for mana, a skill that
      trades health for mana) with the own heal, the shared cooldown group, the HP below
      which the rest heals and the HP the trade needs. Each names its role and the name a
      trace reason gives it, so the Cleric's reasons read as they did. The rules also name
      the heal's role, where "heal" stood. NaturalPowderRestPolicy takes the kinds and
      holds no role name; the Cleric's kinds are NaturalClericSkills.RestSkills (Herb
      Treatment, MP Recovery, Penance, Healing Light, group 1153, 90% and 70%), and the
      Priest line's rest rules name them. A class with no rest-only skills names none.
    - **Tests.** Two assertions of the existing NaturalClericCombatPolicyTests read the
      removed members and now ask the rest kinds the same question; no test was added.
    - **One-time check, not committed** (run/nr/NR-12/check-a2.log, with the existing
      Priest, Cleric and rest tests: 38 pass; a1 did not compile for a missing using in the
      check). Every generated row of the five starters states its first target (Warrior 6
      rows, Mage 7, Artist 5, Engineer 7, Scout 5; none without), so nothing relied on the
      removed fallback. The Priest's 8 and the Cleric's 50 hand-typed rows state none and
      are read by the old rule alone. Of the Cleric's rows 6 are reagent skills and 8 are
      rest-only.
    - **Left for NR-18:** the fight loop still marks a fight as healed by the role name
      "heal" and traces the Lesser Odella Powder count by item id.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout, run gate-a1
      (run/nr/NR-12/gate-a1/verdict.json): verdict pass, all twelve scopes identical to
      their baselines (p 37,222, m 112,397, b 144,048, l1 30,693, c 96,166, hm 39,564, ax
      15,762, mage 23,555, warrior 24,199, artist 23,104, engineer 24,580, scout 27,592).
      Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,675 passed, 16 skipped) and
      Fast passes (run nr12-fast, 11 passed).
- [x] **NR-13 - Generated catalogs for the Priest and the Cleric.** Depends: NR-12
  - Work: The Priest's and the Cleric's catalogs are generated from the skill templates
    with a role per skill id, as the other classes' are, with the reason for every active
    skill left out. A one-time check sets each generated row beside its hand-typed row
    (cost, range, cooldown group and time, chain category, required category, chain time,
    DP and reagent); every difference is written into this document with the template
    line, and the template wins. The profiles still use the old fight rule in this item.
  - Proof: The list of differences; when it is empty the full gate is identical, and when
    it is not, the scopes it changes are re-recorded (rule (p)).
  - 2026-10-08: done. The list of differences is empty.
    - **Java.** Nothing new of the server is relied on. A catalog reads the skill tree as
      Java's SkillLearnService does (learnNewSkills calls autoLearnSkills, lines 84-93):
      the rows of the class the character is at each level. A new rank is added and the
      old one is kept (addSkill at line 91, and no removal), which is why the highest
      learned rank of a role is the one cast.
    - **The comparison** (one-time check, not committed; run/nr/NR-13/compare-a1.log). Each
      generated row was set beside its hand-typed row in eleven fields: level, mana cost,
      range, cooldown group, cooldown time, chain category, required category, chain time,
      DP, reagent and reagent count. Priest: 8 rows against 8, 0 differences. Cleric: 50
      rows against 50 (its own 42 and the Priest's 8), 0 differences. So no template line
      is written here and no scope is re-recorded.
    - **What a generated row holds beside those fields.** The hand-typed rows held none of
      this, and the old fight rule reads none of it.
      - First target. The caster: Herb Treatment, MP Recovery, Salvation, Penance. A
        friend or the caster: Healing Light, Blessing of Guardianship, Light of
        Rejuvenation, Healing Grace, Flash of Recovery. The target: every attack and the
        Holy Servant.
      - Cast time. Healing Light 2 s, Smite 1.5 s, Earth's Wrath 1.5 s, Healing Grace 3 s,
        Herb Treatment and MP Recovery 4 s. Every other row is instant.
      - Weapon. Hallowed Strike adds the weapon's range to its 1 m and needs a greatsword,
        dagger, mace, polearm, staff or sword. Divine Touch adds the weapon's range to its
        25 m. No row is a counter skill, a charge skill or barred in combat.
    - **The one place the fight loop reads such a field for the old rule.** After the
      server refuses a cast for distance, the loop closes to 2 m for a skill that reaches
      3 m or less and to 10 m for any other. Hallowed Strike now reaches 2.5 m with a mace
      and 3 m with a staff where the hand-typed row said 1 m, and Divine Touch 27 m where
      it said 25 m: the answer is the same for both. The recorded scopes p, m and b hold
      1, 8 and 1 such refusals, and the gate below shows them unchanged.
    - **Code.** NaturalPriestProfile holds the two role tables (8 skill ids for the Priest,
      50 for the Cleric) and builds both profiles from the run's static data;
      NaturalChanterProfile takes the Priest's generated catalog. NaturalClassProfiles has
      one table now: all eight profiles are built from the shipped data, once, and a run
      hands its static data over when it starts (Supply), so no caller without it comes
      first. The validator runs when a profile is built: the Priest to level 9, the Cleric
      to level 24. The Priest now names the three common exclusions (Return, Bandage Heal,
      Escape); the Cleric names those and its own seven. All three still fight by the old
      rule (StaticPolicy).
    - **Second one-time check, on the built profiles** (run/nr/NR-13/check-a2.log; not
      committed). Priest 8 rows, Cleric 50, Chanter 8; 0 differences with the role
      included, so the typed role tables say what the hand-typed rows said. The old rule's
      best rank of every role at every level from 1 to 26 is the same from either table
      (130, 494 and 130 picks).
    - **Found, for NR-16.** The catalog stops at level 24, as the hand-typed table did,
      and the Cleric plays to 26. Twelve skills of levels 25 and 26 have neither a role
      nor an exclusion: the next ranks of Herb Treatment (253), MP Recovery (254), Penance
      (3869), Light of Rejuvenation (3942), Flashbolt (4028), Slashing Wind (4064),
      Earth's Wrath (4086) and Summon Holy Servant (4110) at 25, of Healing Light (1843)
      and Smite (4017) at 26, and the new skills Splendor of Flight (4006) at 25 and
      Stability (3880) at 26. The recorded Cleric casts the level-24 ranks at 25 and 26.
      NR-16 gives each its role or its reason and validates to level 26.
    - **Left for NR-18.** The hand-typed tables remain. They are read by the old rule's
      defaults, by the static Priest and Cleric gear rules (ids and levels only) and by
      the existing tests.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout, run gate-a1
      (run/nr/NR-13/gate-a1/verdict.json): verdict pass, all twelve scopes identical to
      their baselines (p 37,222, m 112,397, b 144,048, l1 30,693, c 96,166, hm 39,564, ax
      15,762, mage 23,555, warrior 24,199, artist 23,104, engineer 24,580, scout 27,592).
      Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,675 passed, 16 skipped) and
      Fast passes (run nr13-fast, 11 passed).
- [x] **NR-14 - The Priest on the table.** Depends: NR-11, NR-13
  - Work: Write the Priest's rule table into this document first, then build it:
    NaturalPriestProfile.Priest takes NaturalRotationCombatPolicy with the generated
    catalog. The roles, percentages and order are the old rule's, said by the table. The
    Chanter's placeholder takes the same table. SIM probe rows priest-1 and priest-7 on
    the probe accounts, as the other starters have.
  - Proof: The two probe rows pass, with the heal, the stun and the slow shown in play;
    guard: the five class scopes identical. The Priest's scopes are NR-15's.
  - 2026-10-08: done. The Priest and the Chanter placeholder fight by the rule table.
    - **Java.** Nothing new of the server is relied on. What the table side of the fight
      loop does was read for earlier items: a swing no sooner than the weapon's speed less
      300 ms (PlayerController.attackTarget, CP-39 and NR-04), and the chain rule
      (ChainCondition, CP-36). The Priest's four attacks open chains and none follows
      another, so no chain is kept.
    - **The Priest's rule table, natural-priest-v1** (NaturalPriestProfile.PriestRules).
      Written here first, then built.

      | Part | What the table says |
      |---|---|
      | With the monster on it | Infernal Blaze (the stun), Hallowed Strike (the slow), Smite, in that order. |
      | From range | Smite. It is the pull, and the filler while the monster comes. |
      | The weapon | Swung whenever no skill is ready and the monster is on the Priest. |
      | Buff | Blessing of Guardianship, before the first hit, when the effects are seen without it. |
      | Ladder, first | The Anti-Shock scroll at 50% HP. |
      | Ladder, second | The life potion at 90% HP (the run's potion percentage). |
      | Ladder, third | Healing Light at 55% HP, and at 70% against two or more attackers (the run's two heal percentages). |
      | Finisher | Smite in Healing Light's place, outside an emergency, once the fight has had a heal and the target is at or below 15% HP (the run's). |
      | Emergency | From 35% HP until 45%. Against two or more attackers on a Seasoned target, from 55% until 65%. Every ladder step is tried at once. |
      | Mana kept back | Healing Light's cost, from every attack and from the Blessing. |
      | Mana potion | Below Healing Light's cost and 10, or below the cheapest attack. |
      | Leaves | At three attackers. At 30% HP when nothing of the ladder is available. Nothing is cast first; Root is the Cleric's. |
      | A target that attacks from range | With nothing ready: holds within 12 m, or with two attackers, when the run asks for the hold. Otherwise walks up to it. |

    - **Two rules added to the table policy, for every class** (Sc/Classes/
      NaturalRotationCombatPolicy.cs). Survey A2 had not listed them; the old rule had
      both.
      - The wait for an attack that only cools down no longer keeps a class that fills
        with its weapon out of the weapon's reach of a target that attacks from range.
        That target does not come, so the class walks up, as the old Priest did between
        two Smites.
      - Holding is for a target some listed attack reaches from where the bot stands. A
        target none reaches is gone to: the old rule's approach beyond 25 m.
      - No recorded class scope holds a decision either rule changes. The five baseline
        traces hold no stand-off wait at all, and the only cooldown waits are the
        Artist's 86, and the Artist swings as a last resort. The guard below confirms it.
    - **One-time check, not committed** (run/nr/NR-14/check-a1.log and check-a2.log, the
      same sample with the facts of each class printed). The table and the old rule each
      decided 400,000 sampled Priest states with a target: levels 1 to 9, HP, mana,
      attackers, distance, a ranged target, the hold, cooldowns, potion, scroll, mana
      potion, emergency, an earlier heal, the target's HP, cornered, and the Blessing.
      392,275 are the same decision. The other 7,725 are four differences, and every
      choice of the table is in its own list of legal candidates.
      - 3,289: an unpulled target that is not on the Priest, with no attack that can be
        paid for. The old rule waited for a monster that was not coming. The table walks
        up to it, as it does for every class.
      - 2,706: at or below 30% HP with no heal and no potion, and the Anti-Shock scroll
        ready. The old rule left first. The table uses the scroll and leaves at the next
        decision.
      - 1,157: levels 1 and 2 with 23 or 24 MP. The table drinks the mana potion below
        Smite's 25 MP, its cheapest attack then; the old rule drank it below 23.
      - 573: from level 5, with no attacker yet and the Blessing not seen. The table
        casts it before the first hit; the old rule left it to the check between fights.
      - Not in the sample, and already written under NR-10: the three tuned-potion cases
        and the mana potion before leaving.
    - **What changes with the table in the fight loop.** The Priest now takes the table
      side of the loop's branches (Survey A2): the weapon swings at its own speed where
      a fixed 2.5 s stood; the wait after a cast includes the animation's last hit; the
      effects seen on the bot are given to the rule as a list. The profile's Blessing
      check between fights and the rest are unchanged.
    - **Code.** NaturalPriestProfile builds the Priest with NaturalRotationCombatPolicy
      over the generated catalog and validates the table's two lines; NaturalChanterProfile
      takes the same table. The Cleric still fights by the old rule. Two probe rows are
      added to the starter field probe.
    - **Proof, the probe** (SIM, seed 1, the two probe accounts, one process).
      - Attempt a1 (run nr14-probe-a1, run/nr/NR-14/probe-a1.log): row priest-1 passed;
        row priest-7 failed on the row's own design, not on the rule. The row cut HP to
        50% and fought a Fanged Karnif (478 HP). The life potion's first tick took HP from
        265 to 302 of 531, above the heal's 55%, so no heal was due, and the Karnif died
        to two Smites and Infernal Blaze before Hallowed Strike. The row now cuts HP to
        40% and fights a Vengeful Ghost (210593, 719 HP, level 8).
      - Attempt a2 (run nr14-probe-a2, run/nr/NR-14/probe-a2.log): both rows pass.
      - priest-1: a level-1 Priest, 201 HP, three Sprigg Workers. Each dies to one Smite,
        cast from 10.6 m, 18.3 m and 18.1 m, in 2,100 ms. Before the second and the third
        kill the director halves HP, and each rest heals with one Healing Light and
        drinks no potion. Every decision is the table's.
      - priest-7: a level-7 Priest, 531 HP, cut to 40% as the fight begins. The life
        potion at 212 HP, then Healing Light at 249 HP (46%), which leaves 490. Smite from
        20.3 m, a wait of 1.5 s for its cooldown, Smite again; with the Ghost at 2.9 m
        Infernal Blaze, Hallowed Strike and Smite, and the Ghost is dead 11,767 ms after
        the fight began. The Ghost never hit the Priest. Decisions: 1 heal, 5 attacks, 1
        potion, 3 waits.
    - **Seen, not acted on.** A Smite takes 3.6 s from one to the next at range: 1.5 s to
      cast, 0.6 s for the animation's last hit, and 1.5 s of waiting for its 2 s cooldown.
    - **Guard, rule (c).** Run guard-a1, gate mage+warrior+artist+engineer+scout
      (run/nr/NR-14/guard-a1/verdict.json): all five identical to their baselines (mage
      23,555, warrior 24,199, artist 23,104, engineer 24,580, scout 27,592). Scopes p, m
      and b change with this item and are left out until NR-15 and NR-17 re-record them;
      l1, c, hm and ax start from a Cleric, which still fights by the old rule. Bundle:
      the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,675 passed, 16 skipped) and Fast
      passes (run nr14-fast, 11 passed).
- [x] **NR-15 - The Priest's Ishalgen replayed and re-recorded.** Depends: NR-14
  - Work: No code beyond one small change (rule (e)). Replay scope m (the Priest from
    creation to Munin); two attempts. Compare its ledger with the old baseline's run:
    deaths, retreats, potions, game time. Then re-record p and m twice (rule (p)). Scope b
    changes too and is left out of every guard until NR-17 re-records it.
  - Proof: Scope m reaches Munin; p and m repeat in two passes each.
  - 2026-10-08: done. Scope m reaches Munin on the second attempt, with one small change;
    p and m are re-recorded.
    - **Java.** AttackEventHandler.onAttack (lines 32-37): an npc in the state RETURNING
      that is attacked aborts its move, goes idle and is told it is not at home.
      ReturningEventHandler.onNotAtHome then sets RETURNING again, sends the two emotes of
      EmoteManager.emoteStartReturning and walks on. CreatureEventHandler.checkAggro lets a
      returning npc take no aggro. So a monster that is walking home cannot be pulled
      before it is home. It sends the same two emotes when it arrives
      (emoteStartIdling), so one emote does not tell the client which of the two happened.
    - **Attempt a1** (run m-a1 at 1216d6c87, run/nr/NR-15/m-a1; kept): stopped at Q2128
      after 2 h 08 min 42 s, with 38 quests done, level 9 and no death: "NPC 210391 was
      not killed in 6 non-retreat attempts".
      - At 2:07:49 the Priest pulled a dundun farmer (210391, level 7). Its two neighbours
        came with it, though the pull plan named no adds. With three on it after 7.9 s the
        Priest left, as the table and the old rule both say, with the target at 26% HP.
      - From 2:08:15 the kill loop pulled the three while they walked home: six fights
        of one Smite each, every one ended by the target giving up, in 28 s. The sixth was
        the template's last allowed failed pull.
      - The old baseline run met the same pack at 2:13:34 and opened the same way (two
        Smites, Infernal Blaze). There the third farmer's first hit came later; the Priest
        killed its target with two on it and left in the next fight. The table's Priest
        decides a quarter of a second later after an instant skill, because it waits for
        the animation's last hit. The weakness is the kill loop's and not the table's.
    - **The one small change, rule (e)** (commit 3ac9e756f, Sc/NaturalIshalgenJourney.cs
      and its fight loop). A fight that ended because the target gave up and walks home is
      not one of a template's six failed pulls. It has its own bound of twelve. The rule
      that leaves a monster after it failed twice stands, so the hunt goes on to another
      of its kind.
    - **Attempt a2** (run m-a2, run/nr/NR-15/m-a2, with the change not yet committed):
      passed. "All included quests complete; standing at client-observed Munin", 41
      quests, level 9, 769 of 769 HP, no death, 9,980,890 ms of game time. At Q2128 the
      same retreat and the same six fights; the next pull went to another dundun farmer
      and killed it with one attacker.
    - **Re-recorded, rule (p).** Run record-a1 at 3ac9e756f on a clean tree
      (run/nr/NR-15/record-a1/verdict.json): two passes of each scope, identical after
      normalization. p: 35,463 records, SHA-256
      ce9e6ec6c8fa1027a134d6d9c9249294bda015f3be6aa26f5951f4a8a88c069c. m: 109,039 records, SHA-256
      53790cf7c92275430fbc6f19af3a6c14bdf77d143c10b1aecde5b181f2d33a2e. The recorded m has the counts of attempt a2. Both rows of
      parity-artifacts/e2e/natural-neutral-baseline.json are replaced; the traces are
      under run/cp/baseline/3ac9e756f...
    - **The ledger, old baseline run beside the new one.** From the baseline traces of
      49cf15b60 and of 3ac9e756f.

      | | p, old rule | p, table | m, old rule | m, table |
      |---|---|---|---|---|
      | Game time | 1 h 03 min 48 s | 1 h 01 min 03 s | 2 h 51 min 38 s | 2 h 46 min 20 s |
      | Fights | 45 | 43 | 150 | 146 |
      | Kills | 44 | 42 | 136 | 133 |
      | Seconds in fights | 386 | 346 | 1,541 | 1,397 |
      | Fight decisions | 274 | 257 | 1,133 | 1,121 |
      | Deaths | 0 | 0 | 0 | 0 |
      | Retreats | 0 | 1 | 1 | 1 |
      | Life potions in fights | 16 | 14 | 36 | 34 |
      | Shield scrolls | 0 | 0 | 1 | 0 |
      | Heals between fights | 3 | 0 | 15 | 6 |
      | Pull plans | 9 | 6 | 103 | 110 |
      | Lowest HP seen at a decision, % | 77 | 81 | 41 | 53 |
      | Smite | 91 | 88 | 366 | 365 |
      | Hallowed Strike | 45 | 42 | 148 | 136 |
      | Infernal Blaze | 15 | 15 | 76 | 71 |
      | Healing Light, rest included | 2 | 0 | 19 | 10 |
      | Weapon swings | 37 | 36 | 126 | 116 |
      | Waits | 55 | 51 | 295 | 327 |
      | Targets that gave up and walked home | 0 | 0 | 3 | 7 |

    - **Found, not acted on (rule (f)).**
      - The bot pulls monsters that are walking home, and learns it only from the wasted
        cast. A rule that leaves a pack alone until it is home would save those casts and
        the six fights above.
      - The pull plan named no adds for a dundun farmer whose two neighbours came with it.
      - After leaving a pack at the swarm limit, the kill loop goes back to the same pack.
    - **Guard, rule (c)**, for the small change. Run guard-a1, gate
      l1+c+hm+ax+mage+warrior+artist+engineer+scout (run/nr/NR-15/guard-a1/verdict.json):
      nine scopes identical to their baselines (l1 30,693, c 96,166, hm 39,564, ax 15,762,
      mage 23,555, warrior 24,199, artist 23,104, engineer 24,580, scout 27,592). No
      recorded scope ever reached a template's six failed pulls. Scope b is left out
      until NR-17. Bundle at the code commit: the seven pre-commit checks pass,
      Aion.GameServer.Tests passes (4,675 passed, 16 skipped) and Fast passes (run nr15-fast, 11 passed).
- [x] **NR-16 - The Cleric on the table.** Depends: NR-15
  - Work: Write the Cleric's rule table into this document first, then build it, with the
    Cleric's rest by NR-12's kinds. SIM probe rows on prepared Clerics at levels 10, 16,
    20 and 25 against monsters of the legs: the chain Smite, Flashbolt, Divine Spark; the
    servant; the heal ladder with Salvation; Root before a retreat.
  - Proof: The four probe rows pass with those shown in play; guard: p, m and the five
    class scopes identical.
  - 2026-10-08: done. The Cleric fights by the rule table, and its catalog reaches level
    26.
    - **Java.**
      - Skill.java 629-640: when a chain skill is cast by a player, the server rolls the
        template's chain_skill_prob, and only a success keeps the chain; a failure resets
        it. Smite, Slashing Wind and Divine Touch have 100. Flashbolt has 10, in every
        rank. So Divine Spark is open after one Flashbolt in ten.
      - ChainCondition.validate and ChainSkills.updateChain: a chain expires by the time
        of the step cast last, and Smite carries none. So Java takes Flashbolt at any
        time after Smite. The table casts a follow-up only inside the follow-up's own
        3 s, which is the default of CP-Q17 and always legal on Java.
    - **The Cleric's rule table, natural-cleric-v1** (NaturalPriestProfile.ClericRules).
      Written here first, then built.

      | Part | What the table says |
      |---|---|
      | An open follow-up | Cast before anything else: Divine Spark after Flashbolt, Flashbolt after Smite, Divine Touch after Slashing Wind, each inside 3 s. |
      | Smite as the opener | Brought to the front while Flashbolt is off cooldown and Smite, Flashbolt and the heal can all be paid for. At other times it is the last filler. |
      | The Holy Servant | Summoned first of the rest, on a target above 50% HP or of unseen HP. |
      | With the monster on it | Infernal Blaze, Hallowed Strike, Slashing Wind, Earth's Wrath, Smite, in that order. |
      | From range | Earth's Wrath, Slashing Wind, Smite. |
      | The weapon | Swung whenever no skill is ready and the monster is on the Cleric. |
      | Buffs | Blessing of Guardianship before the first hit. Light of Rejuvenation while the Cleric is being hit and it is not seen on it. |
      | Ladder, first | The Anti-Shock scroll at 50% HP. |
      | Ladder, second | Salvation, paid with 2,000 DP, at 25% HP or in an emergency. |
      | Ladder, third | The life potion at 90% HP (the run's). |
      | Ladder, fourth | Flash of Recovery, in an emergency only. |
      | Ladder, fifth | Healing Grace at 55% HP, and at 70% against two or more attackers (the run's). Passed over once after it was cancelled. |
      | Ladder, sixth | Healing Light at the same percentages. |
      | Finisher | Smite in a heal's place, outside an emergency, once the fight has had a Healing Light and the target is at or below 15% HP (the run's). |
      | Emergency | From 35% HP until 45%. Against two or more attackers on a Seasoned target, from 55% until 65%. Every ladder step is tried at once. |
      | Mana kept back | Healing Light's cost, from every attack, from both buffs and from Root. |
      | Mana potion | Below Healing Light's cost and 10. |
      | Leaves | At three attackers. At 30% HP when nothing of the ladder is available. Root is cast on the target first, when it can be. |
      | A target that attacks from range | With nothing ready: holds within 12 m, or with two attackers, when the run asks for the hold. Otherwise walks up to it. |

    - **The rest** is unchanged: the Cleric's rest already names its rest-only skills by
      kind (NR-12).
    - **The catalog to level 26** (found by NR-13). Ten ranks get the role of the rank
      before them: Herb Treatment 253, MP Recovery 254, Penance 3869, Light of
      Rejuvenation 3942, Flashbolt 4028, Slashing Wind 4064, Earth's Wrath 4086 and the
      Holy Servant 4110 at level 25, Healing Light 1843 and Smite 4017 at 26. Two are
      left out with a reason: Splendor of Flight 4006 restores flight time, and Stability
      3880 is the third rank of a skill already left out. The profile is validated to
      level 26: 60 rows, 12 exclusions.
      - The powder skills of level 25 use Odella Powder (169300004), where the ranks
        below use Lesser Odella Powder (169300003). The approved kit has supplied Odella
        Powder from level 25 all along, and until now no skill of the catalog used it.
    - **One rule of the table policy adjusted, for every class.** Holding for a target is
      now decided by the listed attacks that need no open chain. Divine Touch reaches the
      weapon's range farther than Slashing Wind, which opens it, so a target at 26 m was
      held for although nothing could be cast at it. No other table has a follow-up that
      reaches farther than its opener.
    - **One-time check, not committed** (run/nr/NR-16/check-a3.log; a1 and a2 are the
      same sample before the adjustment above and before an impossible cooldown on
      Healing Light was taken out of the sample). The table and the old rule each decided
      600,000 sampled Cleric states with a target, at levels 10 to 24. 577,634 are the
      same decision. The other 22,366 are six differences, and every choice of the table
      is in its own list of legal candidates.
      - 7,014: Smite's chain is older than 3 s. The old rule still cast Flashbolt; the
        table casts Smite again first (CP-Q17).
      - 5,148: under attack with a follow-up open and Light of Rejuvenation not up. The
        old rule cast the heal over time, which resets the chain; the table casts the
        follow-up first.
      - 3,919: no attacker yet and the Blessing not seen: cast before the first hit, as
        for the Priest (NR-14).
      - 3,288: an unpulled target with no attack that can be paid for: walked up to, as
        for the Priest.
      - 2,489: at or below 30% HP with no heal and no potion, and the Anti-Shock scroll
        ready: the scroll first, then leave, as for the Priest.
      - 508: Divine Touch open with the target between 25 m and 26.5 m. The old rule held
        its range to be 25 m; the template adds the weapon's.
    - **Code.** NaturalPriestProfile builds the Cleric with NaturalRotationCombatPolicy
      over the generated catalog and validates the table's two lines to level 26. The
      static rule is no longer used by any profile; NR-18 removes it. Four probe rows are
      added to the starter field probe, with three director acts: make the character its
      second class at a level, place it on another map, and spawn monsters 4 m away and
      set them on it.
    - **Proof, the probe** (SIM, seed 1, the two probe accounts, two rows a process; runs
      nr16-probe-a6 and nr16-probe-a6b, logs run/nr/NR-16/probe-a6.log and probe-a6b.log).
      All four rows pass, and every fight decision in them is the table's. Each Cleric is
      prepared by the director from a fresh Priest and placed in Altgard at a pull spot of
      the recorded legs; it wears what a new Priest wears.
      - cleric-10 (690 HP), ice crasaurs (210415, level 11). First fight: Smite, then
        Flashbolt 735 ms later, Earth's Wrath, Slashing Wind, Smite, Light of Rejuvenation
        once the crasaur is on the Cleric, Infernal Blaze; a kill in 9.6 s. Second fight,
        against a crasaur the director set on the Cleric: a kill, and the heal over time is
        not cast again while it lasts.
      - cleric-16 (1,074 HP), a tusked mosbear (210437, level 14). Smite, Light of
        Rejuvenation under attack, the Holy Servant, then Smite and Flashbolt together,
        Infernal Blaze. The spot brings two more monsters: with three attackers the Cleric
        casts Root and leaves. No kill, no death; that is the outcome.
      - cleric-20 (1,372 HP), starved mosbears (210564, level 13). First fight, from 30%
        HP with 2,000 DP: Salvation at 29% in the emergency, then the life potion, and a
        kill in 8.3 s with the DP spent. Second fight, from 50% HP: Healing Grace at 50%,
        and a kill.
      - cleric-25 (1,789 HP), starved mosbears. Eight fights, eight kills; Flashbolt
        followed Smite at once in every one and opened Divine Spark in the eighth: Smite,
        Flashbolt 735 ms later, Divine Spark 1,169 ms after that. Then a fight against
        three mosbears the director spawned 4 m away and set on the Cleric: with three
        attackers, Root, then the retreat. No death.
    - **The attempts before a6.** Five, and every change was to a row's preparation or
      to what it checks; the table was not changed for a row.
      - a1 and a1b: cleric-10 and cleric-20 passed. cleric-16 asked for a kill and got
        the retreat above. cleric-25 asked for Divine Spark in one fight; Flashbolt's
        result carried no chain flag, which led to the Java lines above.
      - a2b: Divine Spark shown after eight fights. The pack was three monsters set on
        the Cleric from where they stood, and none came in 90 s (a3b the same).
      - a3: two rows named on one probe account; the second was refused at login. That
        was the loop's pairing mistake.
      - a4: cleric-25 passed with the spawned pack. cleric-10 ran second in its process
        and its crasaur died before it reached the Cleric, so nothing was kept up.
      - a5: cleric-10 with the second fight, which found the heal over time still on.
    - **Seen, not acted on.**
      - Twice a first Smite did no damage and opened no chain (cleric-16, and cleric-10's
        second fight). Not looked into.
      - The recorded pull spot by the tusked mosbears brings three attackers at once.
    - **Guard, rule (c).** Run guard-a1, gate p+m+mage+warrior+artist+engineer+scout
      (run/nr/NR-16/guard-a1/verdict.json): seven scopes identical to their baselines (p
      35,463, m 109,039, mage 23,555, warrior 24,199, artist 23,104, engineer 24,580,
      scout 27,592). Scopes b, l1, c, hm and ax change with this item; NR-17 replays and
      re-records them. Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests
      passes (4,675 passed, 16 skipped) and Fast passes (run nr16-fast, 11 passed).
- [x] **NR-17 - The Cleric's scopes replayed and re-recorded.** Depends: NR-16
  - Work: No code beyond one small change an attempt. Replay b, l1, c, hm and ax, each to
    its endpoint, two attempts each; a scope that fails twice becomes a lettered item with
    the step where it stopped. Compare each ledger with the old one. Then re-record the
    five twice (rule (p)).
  - Proof: Every scope reaches its endpoint and repeats in two passes.
  - 2026-10-08: done. All five scopes reach their endpoints on the first attempt, with no
    change to the code, and all five are re-recorded.
    - **Java.** Nothing of the server is relied on beyond what NR-16 read.
    - **The replays** (runs b-a1, l1-a1, c-a1, hm-a1 and ax-a1 at 60115ff14, under
      run/nr/NR-17/). Each passed and wrote its completion file, verified.
      - b: the Ishalgen quests and the early Ascension, a Cleric of level 13.
      - l1: Altgard leg 1, level 13, 56 quests, no death.
      - c: leg l4, level 19, 85 quests, 3 deaths.
      - hm: Haramel, leg l12, level 25, 156 quests, no death.
      - ax: the Abyss entry, level 26 in Morheim with Q24020, Q2945, Q2946, Q2947 and
        Q2042 complete, no death.
    - **The ledger, old baseline run beside the new one.** From the baseline traces of
      49cf15b60 and of 60115ff14.

      | Scope | Rule | Game time | Fights | Kills | Fight decisions | Deaths | Retreats | Life potions | Shield scrolls | Powder-rest casts | Lowest HP, % |
      |---|---|---|---|---|---|---|---|---|---|---|---|
      | b | old | 3 h 44 min | 171 | 165 | 765 | 0 | 2 | 24 | 0 | 6 | 77 |
      | b | table | 3 h 28 min | 155 | 149 | 670 | 0 | 3 | 19 | 0 | 4 | 81 |
      | l1 | old | 0 h 39 min | 45 | 45 | 280 | 0 | 0 | 4 | 0 | 8 | 84 |
      | l1 | table | 0 h 37 min | 41 | 41 | 252 | 0 | 0 | 2 | 0 | 6 | 88 |
      | c | old | 2 h 29 min | 213 | 188 | 1,590 | 1 | 25 | 102 | 4 | 60 | 8 |
      | c | table | 2 h 56 min | 253 | 221 | 1,917 | 3 | 37 | 109 | 7 | 88 | 20 |
      | hm | old | 1 h 50 min | 138 | 128 | 528 | 1 | 8 | 15 | 0 | 11 | 67 |
      | hm | table | 1 h 41 min | 140 | 129 | 555 | 0 | 10 | 10 | 0 | 9 | 83 |
      | ax | old | 0 h 16 min | 10 | 10 | 18 | 0 | 0 | 2 | 0 | 0 | 65 |
      | ax | table | 0 h 16 min | 10 | 10 | 15 | 0 | 0 | 2 | 0 | 0 | 69 |

    - **Four scopes are as good or better.** b is 16 minutes shorter with fewer potions.
      hm has no death where the old run had one, and is 9 minutes shorter. l1 and ax are
      as they were.
    - **Scope c is worse: 3 deaths against 1, 37 retreats against 25, 27 minutes more.**
      It reaches its endpoint, and a death is an outcome. The three deaths:
      - 1:59:43, at the grave robbers' camp (af-062-hunt). The journey stood in the second
        of four 15 s holds for a patrol, with two grave robbing sentries hitting the
        Cleric. It did not answer, and it went on holding for 30 s after the death.
      - 2:17:32, on the way back. After a kill and a rest the journey stood for 60 s in a
        loot sweep and was killed there.
      - 2:30:51, on the way back again: four attackers, no way out, and the Cleric died
        inside Healing Grace's 3 s cast at 59% HP.
      - The old run's one death was at the same camp, in a fight with five attackers.
    - **Found, not acted on (rule (f)): the journey does not answer an attack while it
      holds for a patrol or sweeps loot.** It is not the table's: the old runs took such
      hits too. Hits on the Cleric while the last thing the journey did was a patrol hold
      or a loot sweep, old run then new: b 0 then 8; c 29 then 49; hm 187 then 290. In
      the new c it killed the Cleric twice. This is the first thing to fix in the journey
      for every class that reaches Altgard.
    - **Re-recorded, rule (p).** Run record-a1 at 60115ff14 on a clean tree
      (run/nr/NR-17/record-a1/verdict.json): two passes of each scope, identical after
      normalization, and each with the counts of its first replay.
      - b: 131,197 records, SHA-256 904bc164e98c38cfcacbb6dc1cc5eba5f3a691ec7450fcc6240fe9ec63eefcaa.
      - l1: 29,797 records, SHA-256 d0510830f33ea43479e013696d0b19504b328a30656d43bb0065ee31b6fac12f.
      - c: 112,256 records, SHA-256 14f670d3cf63da3bcf42335ae1c64c1919d17e02a1ed425eaacf952b3d4c9204.
      - hm: 37,510 records, SHA-256 8da8a690323675b2610e0fa5df6246bd258990ab5000340455f183545dd41511.
      - ax: 15,715 records, SHA-256 cd5d6c8aef5d43f1a047d23e319657d77d08fb2adb8ac26004ccda0ee850e40c.
      The five rows of parity-artifacts/e2e/natural-neutral-baseline.json are replaced;
      the traces are under run/cp/baseline/60115ff14... With p and m of NR-15, all seven
      Priest and Cleric scopes are now recorded with the rule table, and rule (c) guards
      them again.
    - **No bundle.** The item changes no code: this is an evidence commit.
- [x] **NR-18 - The old rule removed.** Depends: NR-17
  - Work: Delete NaturalPriestCombatPolicy.Decide and CandidateActions, the StaticPolicy
    adapter, the two hand-typed skill tables and the one-sided branches of the fight loop
    listed in Survey A2. tools/Aion.LiveBots reads the profile's catalog. The tests of the
    deleted rule go with it; the Mau course runs on the table with the run's parameters.
    Nothing plays differently.
  - Proof: The whole solution builds and its tests pass; the full gate identical.
  - 2026-10-08: done. The static rule, its adapter and the two hand-typed tables are gone;
    every recorded scope plays as recorded.
    - **Java.** No server behavior is involved.
    - **Removed from the bot library.**
      - The static rule: NaturalPriestCombatPolicy with Decide, CandidateActions and its
        constants, and the StaticPolicy adapter of NaturalPriestProfile.
      - The hand-typed tables: NaturalPriestSkills.All and Ids, NaturalClericSkills.Cleric,
        All and ForClass. NaturalPriestSkills.Best and NaturalPowderRestPolicy.Decide now
        take the catalog they search; neither has a table to fall back on.
      - The fight loop's second side (Survey A2): the chain kept with an expiry, the
        swing at a fixed 2.5 s, the cast wait without the animation's last hit, and every
        test of whether the profile is table driven. NaturalClassProfile.TableDriven is
        gone with them.
      - The gear rules' skill catalog, which only the Priest's hand-typed rows filled.
    - **Kept, and why.**
      - The observation's HasBlessing and HasRejuvenation. No rule reads them, but every
        recorded fight decision writes them, and the gate compares those records.
      - The rest's trace field that counts Lesser Odella Powder by its item id, for the
        same reason.
      - The file names NaturalPriestCombatPolicy.cs and NaturalClericSkills.cs (section 7
        of the class-profile plan). The first now holds the skill row, the observation,
        the choice and Best; the second the Cleric's exclusions, the rest kinds and the
        powder's two numbers.
    - **Said another way.**
      - A fight "has had its heal" when the class's own heal was cast in it: the role the
        profile's rest names, "heal" for every class today, where the loop had the word.
      - The patrol policy holds its own number of monsters it will take, 2, and the
        Priest's and the Cleric's tables take their swarm limit from it (one more).
      - The check that the level 1 skills are observed is of the class's masteries. It
        also asked for the hand-typed Priest rows by level; a generated catalog needs the
        run's data, and the one caller, the live inventory scenario, has none.
    - **tools/Aion.LiveBots.** Survey A2 said it read the hand-typed table. It also
      called the static rule, in the fight driver of the live scenarios NI-04 and NI-06.
      That driver now builds the Priest's profile from the static data it already loads
      and decides by the Priest's table. What the static rule answered between fights is
      said in the driver: a sit below 90% HP or 50% MP. The starter's life potion is the
      table's life potion. **This is compiled and not run: a LIVE run is outside this
      loop.** The operator should run NI-04 once before relying on it.
    - **Tests.** The tests of the removed rule go with it (rule (n) lets tests go):
      NaturalPriestCombatPolicyTests (12), seven of NaturalPriestRotationTests, seventeen
      of NaturalClericCombatPolicyTests, two of NaturalHelpItemPolicyTests and one of
      NaturalIshalgenPotionPolicyTests: 39 test methods, 45 cases. What stays moved to files
      named for it:
      the one hostility test to NaturalHostilityTests, and the two powder-rest tests to
      NaturalPowderRestPolicyTests, now over the Cleric's generated catalog. One help-item
      test keeps its help-item half, and one inventory assertion follows the mastery
      check. The SIM obstacle probe passes the Priest's catalog to Best.
    - **The Mau course** runs on the table with the run's parameters: one encounter as a
      smoke (run nr18-mau, RaeToHatata, IsolatedStalker; run/nr/NR-18/mau.log) passes, and
      its ten fight decisions carry natural-priest-v1 with the run's parameter id.
    - **Proof.** The solution builds with no error. Gate, set
      all+mage+warrior+artist+engineer+scout, run gate-a1
      (run/nr/NR-18/gate-a1/verdict.json): verdict pass, all twelve scopes identical to
      their baselines (p 35,463, m 109,039, b 131,197, l1 29,797, c 112,256, hm 37,510, ax
      15,715, mage 23,555, warrior 24,199, artist 23,104, engineer 24,580, scout 27,592).
      Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,630 passed, 16 skipped) and
      Fast passes (run nr18-fast, 11 passed).
- [x] **NR-19 - The generic Cleric from creation to the Altgard endpoint.** Depends: NR-18,
  NR-19a
  - Work: One continuous capture from committed code (sim-snapshot.ps1 -Action Capture
    -ContinuousJourney -LaterCapital), named altgard-complete-cleric-s1. Two attempts; a
    stop inside a leg becomes a lettered item for that leg. Acceptance from the receipt
    before Verify: a Cleric at the Altgard endpoint with the accepted run's quests
    complete; the ledger of deaths, retreats, potions and game time beside the accepted
    run's 20 h 17 min.
  - Proof: sim-snapshot.ps1 -Action Verify -Name altgard-complete-cleric-s1 passes.
  - 2026-10-08: two attempts, both stopped inside a leg; no snapshot was made. By rule
    (i) the second stop is NR-19a, and this item is tried again after it with a fresh
    two attempts.
    - **Attempt a1** (run nr19-continuous-a1 at 031a56083; evidence under
      run/snapshots/_capture/nr19-continuous-a1, log run/nr/NR-19/capture-a1.log): stopped
      in leg l4 after 5 h 56 min of game time, at level 18 with 86 quests and no death:
      "Q24013: using item 182215359 did not move the quest to var 3".
      - The Cleric stood 3 m inside the poison's use area and the server answered
        STR_CANNOT_USE_ITEM_INVALID_LOCATION.
      - Java does the same. ZoneUpdateService refreshes a creature's zones every 500 ms,
        and PlayerRestrictions.canUseItem tests the use area against that list
        (MapRegion.isInsideItemUseZone, ZoneInstance.isInsideCreature), not against the
        position. The Cleric walked into the area and used the item in the same instant.
        The accepted run and scope c had stood in the area longer by chance.
      - **The one small change, rule (e)** (commit 16ad6b05c,
        Sc/NaturalAltgardQuestSteps.cs): a use that is refused for its place is sent
        again, three times at most; the use time has passed by then. Guard guard-a1, the
        full gate: twelve scopes identical. Seven checks, the unit suite (4,630 passed, 16
        skipped) and Fast (nr19-fast) pass.
    - **Attempt a2** (run nr19-continuous-a2 at 16ad6b05c; evidence under
      run/snapshots/_capture/nr19-continuous-a2, log run/nr/NR-19/capture-a2.log): twelve
      of the fourteen stages, through leg l11, then stopped in the coin-gear leg after
      17 h 36 min, at level 24 with 165 quests: "Altgard leg cg stopped: blocked
      coin-loadout-incomplete: The five chain body slots, retained cloth gloves and
      19-coin balance are required."
      - At Q24013 the item was refused once for its place and used again; the quest moved.
      - The coin purchases were made and worn: three chain pieces, 19 coins left, the
        staff in hand. The one thing missing is the accepted run's cloth gloves, Altgard
        Legionary Gloves 111101650, which the leg's end check asks for in the bag. The
        generic Cleric never had them: since the table gear rules (CP-29a) it takes the
        chain Altgard Legionary Handguards 111501698 where the accepted Cleric took the
        cloth pair, and it kept those in the bag.
      - So the continuous journey has not been able to finish since CP-29a. No recorded
        scope plays the coin-gear leg, and no continuous run was made between.
    - **The ledger to the stop, stage by stage**, beside the accepted run
      (run/snapshots/altgard-rc-complete-s1). Game time is at the end of the stage.

      | Stage | Accepted: game time | deaths | level | Generic: game time | deaths | level |
      |---|---|---|---|---|---|---|
      | ishalgen-ascension | 3 h 47 min | 0 | 14 | 3 h 40 min | 0 | 14 |
      | l1 | 4 h 31 min | 0 | 15 | 4 h 30 min | 0 | 15 |
      | l2 | 5 h 09 min | 0 | 17 | 5 h 07 min | 0 | 17 |
      | l3 | 5 h 25 min | 0 | 17 | 5 h 22 min | 0 | 17 |
      | l4 | 8 h 20 min | 2 | 20 | 7 h 50 min | 1 | 19 |
      | l5 | 10 h 16 min | 2 | 21 | 10 h 00 min | 3 | 21 |
      | l6 | 11 h 04 min | 3 | 21 | 11 h 00 min | 3 | 21 |
      | l7 | 11 h 47 min | 3 | 22 | 11 h 37 min | 3 | 22 |
      | l8 | 12 h 20 min | 4 | 22 | 12 h 08 min | 4 | 22 |
      | l9 | 13 h 21 min | 4 | 23 | 13 h 10 min | 4 | 23 |
      | l10 | 18 h 11 min | 13 | 24 | 17 h 10 min | 5 | 24 |
      | l11 | 18 h 22 min | 13 | 24 | 17 h 21 min | 5 | 24 |

      To the same point the generic Cleric is an hour faster and has 5 deaths against 13;
      leg l10 alone has 1 against 9. In the whole of a2: 80 retreats, 376 life potions,
      20 shield scrolls, 5 soul heals.
  - 2026-10-09: done, after NR-19a, on the first of the fresh two attempts.
    altgard-complete-cleric-s1 is captured and verified.
    - **Attempt a3** (run nr19-continuous-a3 at 709f33816; evidence under
      run/snapshots/_capture/nr19-continuous-a3, log run/nr/NR-19/capture-a3.log): all
      fourteen stages. Snapshot altgard-complete-cleric-s1: character 133266, 69,627,001
      ms of game time, dump sha256 69f8b8eb95934ef2.
    - **The two places that stopped a1 and a2.** Q24013's item was refused once for its
      place and used again. The coin-gear leg ended complete, bound to the Altgard
      Legionary Handguards 111501698 the Cleric wore as the leg started; Haramel then took
      the character in and was played to its end, two visits.
    - **Acceptance from the receipt, before Verify** (run/nr/NR-19/a3-ledger.txt), beside
      altgard-rc-complete-s1:
      - the same 176 quests complete, and the same journal: Q2945 started, var 0;
      - a Cleric of level 25, alive, at the same Altgard bind (1660.43, 1813.49);
      - 19 iron coins, 7 bronze coins and the sealed bundle;
      - the same worn gear but for the helm: 125001764 where the accepted Cleric wears
        125004139. In the bag it has the handguards 111501698 and no cloth gloves;
      - kinah 739,882 against 748,485; experience 8,332,221 against 8,497,039.
    - **The ledger, stage by stage.** Game time is at the end of the stage.

      | Stage | Accepted: game time | deaths | level | Generic: game time | deaths | level |
      |---|---|---|---|---|---|---|
      | ishalgen-ascension | 3 h 47 min | 0 | 14 | 3 h 40 min | 0 | 14 |
      | l1 | 4 h 31 min | 0 | 15 | 4 h 30 min | 0 | 15 |
      | l2 | 5 h 09 min | 0 | 17 | 5 h 07 min | 0 | 17 |
      | l3 | 5 h 25 min | 0 | 17 | 5 h 22 min | 0 | 17 |
      | l4 | 8 h 20 min | 2 | 20 | 7 h 50 min | 1 | 19 |
      | l5 | 10 h 16 min | 2 | 21 | 10 h 00 min | 3 | 21 |
      | l6 | 11 h 04 min | 3 | 21 | 11 h 00 min | 3 | 21 |
      | l7 | 11 h 47 min | 3 | 22 | 11 h 37 min | 3 | 22 |
      | l8 | 12 h 20 min | 4 | 22 | 12 h 08 min | 4 | 22 |
      | l9 | 13 h 21 min | 4 | 23 | 13 h 10 min | 4 | 23 |
      | l10 | 18 h 11 min | 13 | 24 | 17 h 10 min | 5 | 24 |
      | l11 | 18 h 22 min | 13 | 24 | 17 h 21 min | 5 | 24 |
      | cg | 18 h 35 min | 13 | 24 | 17 h 36 min | 5 | 24 |
      | l12 | 20 h 17 min | 13 | 25 | 19 h 20 min | 5 | 25 |

    - **The whole journey** (scripts/sim/trace/compare_traces.py --counts;
      run/nr/NR-19/a3-counts.json and accepted-counts.json):

      | | Accepted | Generic |
      |---|---|---|
      | Game time | 20 h 17 min | 19 h 20 min |
      | Deaths | 13 | 5 |
      | Retreats | 85 | 88 |
      | Emergency decisions | 104 | 56 |
      | Life potions | 340 | 388 |
      | Mana potions | 8 | 6 |
      | Shield scrolls | 35 | 20 |
      | Speed scrolls | 134 | 110 |
      | Running scrolls | 9 | 15 |
      | Powder rests | 383 | 309 |
      | Patrol waits | 613 | 631 |
      | Soul heals | 0 | 5 |
      | Binds | 9 | 11 |
      | Trace records | 736,017 | 715,203 |

      The generic Cleric is 57 minutes faster and dies 5 times against 13; leg l10 has 1
      death against 9. It drinks more life potions and uses fewer shield scrolls. Its
      five soul heals are the death rule, which the accepted run did not have yet.
    - **Proof.** sim-snapshot.ps1 -Action Verify -Name altgard-complete-cleric-s1, run
      nr19-verify-a1 (run/nr/NR-19/verify-a1.log; evidence under
      run/snapshots/_verify/nr19-verify-a1): "Verified snapshot
      altgard-complete-cleric-s1: character 133266 resumed at its endpoint."
- [x] **NR-19a - The gloves the coin purchase replaced are the character's own.** Depends:
  NR-18
  - Work: Two places ask for the accepted run's cloth gloves, item 111101650, by id: the
    coin-gear leg's end check (Sc/NaturalCoinGearPolicy.cs, an unequipped pair in the bag)
    and Haramel's list of items that must come in (Sc/NaturalHaramel.cs,
    RequiredIncomingItemIds, which the continuous journey makes mandatory in
    NaturalAltgardContinuation.BindIncoming). Both read what the character has instead:
    the continuous journey binds the coin-gear leg to the gloves the character wears when
    the leg starts, and that pair is the one that must be in the bag at the end; none
    worn, none asked for. Haramel asks for the cloth pair only when the character owns it
    as the leg starts, as it already does for the accessories. A run that starts from an
    accepted snapshot keeps the contract's own pair. No other receipt is touched: the
    staff, the three purchases, the five body slots, the coin counts and the sealed
    bundle were all met by the generic Cleric.
  - Proof: A one-time check, not committed, on the last observation of run
    nr19-continuous-a2: the coin-gear decision is "complete" when bound to the gloves that
    character wore, and the Haramel binding accepts its inventory. Guard: the full gate
    identical (scope hm plays Haramel from the accepted snapshot). The first run through
    both legs is NR-19's next attempt.
  - 2026-10-08: done. Both places read the character's own gloves; every recorded scope
    plays as recorded.
    - **Java.** No server behavior is involved. These are the bot's own leg gates.
    - **The change.**
      - Sc/NaturalCoinGearPolicy.cs: the coin-gear contract carries the gloves the
        handguards purchase replaces (ReplacedGlovesItemId). Unbound, it is the
        contract's own cloth pair, 111101650. The end check asks for that pair unequipped
        in the bag; 0 asks for none.
      - Sc/NaturalAltgardContinuation.cs, BindIncoming: for the coin-gear leg the pair is
        the one the character wears in the gloves slot as the leg starts. It joins the
        leg's protected items, so the inventory check holds it through the leg.
      - Sc/NaturalHaramel.cs: RequiredIncomingItemIds no longer lists the cloth pair. The
        pair stays in the contract's protected list, and the binding keeps from that list
        what the character owns.
      - A run from an accepted snapshot keeps the contract's pair: scope hm is not bound,
        and a bound leg from such a snapshot finds the cloth pair worn.
    - **Tests.** One case went: the theory row that said the binding keeps the cloth pair
      mandatory when the character has none (NaturalAltgardHaramelContractTests). No test
      was added.
    - **Proof, the one-time check** (run/nr/NR-19a/check.log; the check file is not
      committed) on run nr19-continuous-a2. The start of leg cg is the "after" of
      altgard-l11-completion.json; its end is the last observation of the failure record.
      - The Cleric wore Altgard Legionary Handguards 111501698 as leg cg started. At the
        stop it wore the purchased 111501065 and had 111501698 in the bag.
      - A. The unbound contract, pair 111101650: coin-loadout-incomplete, the a2 stop.
      - B. Bound: the pair is 111501698; the decision is coin-gear-complete and the leg
        decides leg-complete.
      - C. Bound, and the pair gone from the bag: coin-loadout-incomplete.
      - D. No gloves worn as the leg starts: bound 0, coin-gear-complete.
      - E. The cloth pair worn as the leg starts: bound 111101650, and the protected list
        equals the contract's.
      - G. Haramel bound from the same end state does not ask for the cloth pair, and the
        character owns everything it protects. The Haramel receipt begins (13 worn
        objects) and the leg's first decision is "talk", planned.
      - H. With the cloth pair mandatory, as before: the receipt cannot begin ("Haramel
        lost the retained staff/gear or sealed stigma bundle"). The second place would
        have stopped the run one leg later.
      - I. A character that owns the cloth pair: Haramel protects it.
    - **The coin-gear steps on a server.** Fast plays the three coin probes and they pass
      (run/nr/NR-19a/fast.log): the staff preparation probe, with the reward, the three
      purchases, the Haramel binding and the receipt across a relog; the coin shop probe;
      the coin quest probe. All three give the probe character the accepted Cleric's
      cloth pair, so they show the contract's own pair still works. They do not show the
      new binding.
    - **Guard.** Gate, set all+mage+warrior+artist+engineer+scout, run guard-a1
      (run/nr/NR-19a/guard-a1/verdict.json): verdict pass, all twelve scopes identical to
      their baselines. Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests
      passes (4,629 passed, 16 skipped) and Fast passes (run nr19a-fast, 11 passed).
    - Not proven here: a run through both legs. That is NR-19's next attempt.
- [x] **NR-20 - The generic Cleric through the Abyss entry.** Depends: NR-19
  - Work: Capture the ax leg from altgard-complete-cleric-s1 as ntc-ready-cleric-s1. The
    leg's start facts that are receipts of the accepted run are met by the new run or
    become lettered items here; phase C opens them for other classes.
  - Proof: Verify of ntc-ready-cleric-s1: a Cleric of level 25 or higher, alive at Morheim
    Ice Fortress, with Q2945, Q2946, Q2947 and Q2042 complete.
  - 2026-10-09: done on the first attempt. ntc-ready-cleric-s1 is captured and verified.
    No start fact of the leg stopped the new run, so no lettered item was needed.
    - **Java.** No server behavior beyond what the accepted leg already relies on (AX list).
    - **Attempt a1** (run nr20-ax-a1 at 807ddd43b, a docs commit on the code of 709f33816;
      evidence under run/snapshots/_capture/nr20-ax-a1, log run/nr/NR-20/capture-a1.log):
      sim-snapshot.ps1 -Action Capture -AltgardLeg1 -Leg ax -From
      altgard-complete-cleric-s1 -Name ntc-ready-cleric-s1. Character 133266; 70,631,041
      ms of game time since creation; dump sha256 c7b4fc30b1c91105.
    - **Acceptance from the receipt, before Verify** (run/nr/NR-20/a1-ledger.txt), beside
      morheim-abyss-entry-s1:
      - a Cleric of level 26, alive, at Morheim Ice Fortress, at the accepted run's own
        position and bind;
      - Q24020, Q2945, Q2946, Q2947 and Q2042 complete; the same 181 quests complete and
        the same journal (Q24021 to Q24026 locked);
      - the level-26 coin gear worn, staff 101500818 and five armor pieces; the same
        worn items as the accepted Cleric but one earring (120001132 for 120001116);
      - 19 iron coins, no bronze coins left, the sealed bundle; kinah 734,555 against
        743,158.
    - **The leg's ledger.** 16 min 24 s of game time against 16 min 30 s; no death, no
      retreat, 2 life potions, 3 speed scrolls and 3 running scrolls in both; 15,755
      trace records against 15,763. The whole journey from creation to this endpoint is
      19 h 37 min of game time for the generic Cleric against 20 h 34 min.
    - **Proof.** sim-snapshot.ps1 -Action Verify -Name ntc-ready-cleric-s1, run
      nr20-verify-a1 (run/nr/NR-20/verify-a1.log; evidence under
      run/snapshots/_verify/nr20-verify-a1): "Verified snapshot ntc-ready-cleric-s1:
      character 133266 resumed at its endpoint."
- [x] **NR-21 - Phase B closed.** Depends: NR-10 to NR-20
  - Work: No code. The full gate on every recorded scope. Write into this document the
    seven re-recorded baselines with their commits and record counts, and the generic
    Cleric beside the accepted one: game time, deaths, retreats and consumables.
  - Proof: Every scope identical to its baseline.
  - 2026-10-09: done. Phase B is closed. No code.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout, run gate-a1 at
      b5f8cbcef (run/nr/NR-21/gate-a1/verdict.json): verdict pass, all twelve scopes
      identical to their baselines.
    - **The seven re-recorded baselines** (parity-artifacts/e2e/natural-neutral-baseline.json).
      "Code" is the commit a scope was recorded at; the counts are of the recorded play.

    | Scope | Plays | Code | Recorded in | Records | Deaths | Retreats | Life potions |
    |---|---|---|---|---|---|---|---|
    | p | Ishalgen to the Ascension quest, and the capital pass to its start | 3ac9e756f | 0dd26b7c4 | 35,463 | 0 | 1 | 14 |
    | m | Ishalgen to Munin | 3ac9e756f | 0dd26b7c4 | 109,039 | 0 | 1 | 34 |
    | b | the Ascension bridge to Altgard | 60115ff14 | 32d51c32c | 131,197 | 0 | 3 | 19 |
    | l1 | Altgard leg 1 with the later capital | 60115ff14 | 32d51c32c | 29,797 | 0 | 0 | 2 |
    | c | Altgard leg 4 | 60115ff14 | 32d51c32c | 112,256 | 3 | 37 | 109 |
    | hm | Haramel, leg 12 | 60115ff14 | 32d51c32c | 37,510 | 0 | 10 | 10 |
    | ax | the Abyss entry | 60115ff14 | 32d51c32c | 15,715 | 0 | 0 | 2 |

      The five starter scopes were not re-recorded in this phase: mage 23,555, warrior
      24,199, artist 23,104, engineer 24,580, scout 27,592.
    - **The generic Cleric beside the accepted one.**

    | From creation to the Abyss-entry endpoint | Accepted Cleric | Generic Cleric |
    |---|---|---|
    | Snapshots | altgard-rc-complete-s1, morheim-abyss-entry-s1 | altgard-complete-cleric-s1, ntc-ready-cleric-s1 |
    | Game time | 20 h 34 min | 19 h 37 min |
    | Level at the end | 26 | 26 |
    | Quests complete | 181 | 181 |
    | Deaths | 13 | 5 |
    | Retreats | 85 | 88 |
    | Emergency decisions | 104 | 56 |
    | Life potions | 342 | 390 |
    | Mana potions | 8 | 6 |
    | Shield scrolls | 35 | 20 |
    | Speed scrolls | 137 | 113 |
    | Running scrolls | 12 | 18 |
    | Powder rests | 383 | 309 |
    | Soul heals | 0 | 5 |

      The stage table of the journey to Altgard is under NR-19, the Abyss leg under NR-20.
    - **What phase B leaves open**, logged under its items and not fixed (rule (f)):
      - the journey does not answer an attack while it holds for a patrol or sweeps loot
        (NR-17; it killed the Cleric twice in scope c, which is the one scope that plays
        worse than before: 3 deaths against 1);
      - the bot pulls monsters that are walking home, and goes back to a pack it left at
        the swarm limit (NR-15, NR-17);
      - the fight driver of the live scenarios NI-04 and NI-06 decides by the Priest's
        table and has not been run (NR-18): the operator should run NI-04 once.
    - From here rule (c) guards the Priest's and the Cleric's scopes again (rule (p)).

### C. The legs opened by class line

Written by NR-01 from Survey A1 (rule (q)). In this phase no class but the Cleric plays a
leg: each item is proven by the Cleric's scopes staying identical and by a one-time check,
not committed, that prints what the changed rule gives each of the eleven classes. The
first class of phase D is the first to play what this phase opens, and what it finds
becomes lettered items there. Every item depends on the close of phase B.

Worked first in this phase, by the operator's answers to NR-Q9 (2026-10-09). They are
not about the class line; they are here so that this phase's gates and phase D's classes
can use them. The order below is the order of work: the item that saves time comes first.

- [x] **NR-43 - Several bots at once: survey.** Depends: the close of phase B
  - Work: No code (rule (q)). Read the SIM fixture, the session, the journey and the run
    scripts; write what was found, rule (w) in full and the items that follow.
  - Proof: The survey and the items are in this document; seven checks pass.
  - 2026-10-09: done. Survey C1 is in the Surveys section. No code is changed; the
    one-time probe is removed.
    - **The main finding.** Bots in one world take turns on one thread, and four fifths of
      a run's wall time is on the bot's side. So one world for several bots saves memory
      and shows bots beside each other, but takes about as long as the runs one after
      another. Separate worlds side by side save the time. A round can be both.
    - **Measured** (run/nr/NR-43/world-cost.log, split.txt; replays split-mage and
      split-c, both passed): a world loads in about 41 s of processor time and holds 2.4
      GB; an empty world takes 5.9 s for a game hour, 8.2 s with six characters standing
      in it; scope c takes 310.6 s, of it 48.5 s in the server.
    - **Written:** rule (w) in full, NR-Q11 with its default, the phase D scheme for
      rounds, and the items NR-45, NR-46, NR-44 and NR-47 below.
    - **Proof.** Seven pre-commit checks pass (run/nr/NR-43/checks.log).
- [x] **NR-45 - Separate runs side by side.** Depends: NR-43
  - Work: Survey C1, part 10. A run takes the first free monitor port from 17880, prints
    its address and writes it into its evidence; a run alone is at 17880 as now. A run in
    progress is marked, and the scripts refuse to build while a mark is live. The gate
    plays its scopes several at a time in compare mode from one build (-Parallel, default
    1); recording stays one scope at a time. A capture records the commit it was built
    from, read at its start. The script tests follow the changed contract.
  - Proof: The script tests pass. The full gate with eight at a time is identical to the
    baselines, with its wall time beside the serial gate's 45 minutes. One scope played
    alone afterwards is identical too.
  - 2026-10-09: done. The full gate eight at a time is identical and takes 9 minutes
    where it took about 45.
    - **Java.** No server behavior is involved.
    - **The change.**
      - **Monitor port** (tests/Aion.Bots/Dashboard/LiveBotDashboard.cs,
        LiveBotDashboardHost.OpenFirstFree): the journey and the starter probe open the
        monitor at 17880 and, when another run holds it, at the next free port up to 32
        on. The journey writes the address into its evidence as monitor.json. The other
        probes and the live runner open their port as before.
      - **The mark** (scripts/sim/sim-run-marker.ps1, new): sim-snapshot.ps1 marks a
        journey under run/.sim-running/ while it plays and takes the mark away after. A
        mark whose process is gone is dropped. Both run scripts refuse to build while a
        mark is live. Run by itself the file lists the live runs and exits 1 when there
        is one, so the loop asks it before any other build.
      - **The gate** (scripts/sim/run-neutral-gate.ps1): -Parallel n, 1 to 16, default 1.
        Above 1 a comparison plays its scopes n at a time from the one build, each Replay
        in a process of its own, because a journey's settings are its process's
        environment. The traces are then compared one after another as before. Recording
        with -Parallel is refused. The verdict records the number.
      - **Captures** (scripts/sim/sim-snapshot.ps1): the commit in a snapshot's record is
        read before the capture plays. NR-19's capture needed the plan's commit held back
        for that; it no longer does. The new file is in the list a capture wants
        committed.
      - **Script tests** (scripts/sim/test-sim-snapshot.ps1): the runner's test gives it a
        mark folder of its own and checks the mark while the journey plays, its removal,
        the refusal to build beside a live mark and the dropping of a dead one. The
        parallel path starts real processes, which the test's fake dotnet cannot stand
        in for; the gate below is its proof.
    - **Proof.**
      - The script tests pass (run/nr/NR-45/script-tests.log): test-sim-snapshot.ps1,
        test_compare_traces.py and test-code-coverage.ps1.
      - Gate, set all+mage+warrior+artist+engineer+scout, -Parallel 8, run gate-p8
        (run/nr/NR-45/gate-p8/verdict.json): verdict pass, all twelve scopes identical to
        their baselines, in 544 seconds. The twelve monitors were at ports 17880 to 17887.
        No mark was left.
      - One scope alone afterwards, set ax, run solo-ax: identical, 74 seconds, at 17880.
      - A first try with two at a time, set ax+mage, run smoke-p2: identical, 78 seconds.
      - Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629
        passed, 16 skipped) and Fast passes (run nr45-fast, 11 passed).
    - **From here** the loop plays the guard gate with -Parallel 8, and asks
      sim-run-marker.ps1 before it builds.
- [ ] **NR-46 - Where the bot's side of a run goes.** Depends: NR-45
  - Work: No committed code. With a one-time probe on scope c, split the bot's side into
    its parts: reading and decoding packets, the bot's world model, decisions, routes, the
    trace, the monitor. Write the table here. For each part that is more than a tenth of
    the run and can be made cheaper without changing a trace, write a lettered item.
  - Proof: The table is in this document; the probe is removed and the tree is clean.
- [ ] **NR-44 - Bots take turns in one world.** Depends: NR-45
  - Work: Survey C1, parts 4 to 6. Java first: who gets a quest's kill when two players
    hit one monster. The turn table; the wait that can yield; a bot id, options, trace
    folder and help count for each bot; a problem laid to the bot whose turn raised it; a
    wall-clock limit sized by the world's bots.
  - Proof: The full gate identical: a bot alone plays as before. One world with the six
    starter lines through Ishalgen to Q2004, ten game minutes apart: each bot has its own
    trace and outcome record; the same world played twice gives the same six traces; a bot
    stopped on purpose leaves the others playing.
- [ ] **NR-47 - The round: runner, outcome records and round snapshots.** Depends: NR-44
  - Work: Survey C1, parts 7 and 8. The round file, the runner that starts its worlds side
    by side, the progress line and the outcome record for each bot, the round snapshot
    with its list of characters, a class's capture as a record that points into it, Verify
    of such a capture, and a round that resumes characters from a round snapshot.
  - Proof: The script tests pass. A round of two worlds with three starter lines each, to
    Q2004: six outcome records and two round snapshots. Verify of one character's capture
    passes. A second round resumes all six from the snapshots and each reports where it
    stands.
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
- [ ] **NR-42 - Phase C closed.** Depends: NR-30 to NR-41 and NR-43 to NR-47
  - Work: No code. The full gate on every recorded scope, and one table in this document
    of what each class gets at each class-bound point of the route.
  - Proof: Every scope identical to its baseline.

### D. One class after another

For each class, in the order of NR-Q1, ten numbers are reserved: Templar NR-50, Sorcerer
NR-60, Chanter NR-70, Gladiator NR-80, Assassin NR-90, Ranger NR-100, Spirit Master NR-110,
Gunner NR-120, Rider NR-130, Bard NR-140. The class's survey item writes the others.

**In rounds** (rule (w), Survey C1; this replaces "one class after another" for the play):

1. The surveys NR-x0 are worked first, one class after another. They are reading and
   data, and each ends with its profile accepted by the validator.
2. The probe rows NR-x1 of the classes are played side by side, each in its own world.
3. Then the rounds, numbered NR-R1, NR-R2 and so on, written by the loop as it goes. A
   round takes every class that is not parked from where its last capture stands to the
   next stage: first from creation to the Altgard bind, then the legs in the Cleric's
   order, last the endpoint. The round's item says what was played, has one outcome line
   for each class, and gets a lettered item for each stop with its fix, generic first
   (rule (s)). The next round starts when those are done.
4. A class's NR-x2 to NR-x7 are ticked by the round that gave the capture.
5. NR-x8, the class scope, is recorded by a bot alone, twice, as before.

The template:

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
- 2026-10-08 — Loop: NR-05 done. The comparer's counts gain restLifePotions and
  restSitsForHealth; the digest is untouched and every stored baseline trace still hashes
  to its row. Full gate gate-a1, twelve scopes: identical, which also covers the Cleric's
  legs after NR-03 and NR-04. Seven checks, the two script tests and Fast (nr05-fast) pass.
  Phase A is closed. Next: NR-10, the recovery ladder widened to say the Priest's heal rule.
- 2026-10-08 — Loop: NR-10 done. The recovery ladder can give a second percentage for two
  attackers, an emergency-only step, a cancelled skill passed over, a finisher in a step's
  place and a percentage from the run; the rules can give the emergency on a Seasoned
  target, the reserve role and the mana potion's margin. No profile sets any of it. One-time
  check of 21 cases on the Cleric's skill rows, not committed. Guard guard-a1, p and five
  class scopes: identical. Seven checks, unit suite and Fast (nr10-fast) pass. Next: NR-11,
  the attack list widened to say the Cleric's rotation.
- 2026-10-08 — Loop: NR-11 done. The rules can give openers brought forward while their
  follow-up can follow at once, a least target HP for a role, a fight upkeep for the time
  under attack only, and the ranged hold's distance. No profile sets any of it. One-time
  check of 21 cases on the Cleric's skill rows, not committed. Guard guard-a1, p and five
  class scopes: identical. Seven checks, unit suite and Fast (nr11-fast) pass. Next: NR-12,
  the skill record and the rest rule without role names.
- 2026-10-08 — Loop: NR-12 done. The shared skill record lost its three role-name members;
  the table rule reads a row's own first target, and the rest rule names its rest-only
  skills by kind, with the Cleric's kinds as data. The old static rule keeps the role
  names in its own code until NR-18. Full gate gate-a1, twelve scopes: identical, so the
  Priest and the Cleric rest as they did. Seven checks, unit suite and Fast (nr12-fast)
  pass. Next: NR-13, generated catalogs for the Priest and the Cleric.
- 2026-10-08 — Loop: NR-13 done. The Priest's, the Cleric's and the Chanter placeholder's
  catalogs are generated from the skill templates; the comparison with the hand-typed rows
  found 0 differences in 8 and 50 rows, so nothing was re-recorded. All three still fight
  by the old rule. Found for NR-16: twelve Cleric skills of levels 25 and 26 have no role
  yet. Full gate gate-a1, twelve scopes: identical. Seven checks, unit suite and Fast
  (nr13-fast) pass. Next: NR-14, the Priest on the table.
- 2026-10-08 — Loop: NR-14 done. The Priest and the Chanter placeholder fight by the rule
  table natural-priest-v1, written into the plan. Beside the old rule on 400,000 sampled
  states it decides 392,275 the same; the rest are four named differences. Two rules were
  added to the table policy for every class (walk up to a ranged target between skills
  when the weapon is a filler; never hold for a target no attack reaches). Probe a2: rows
  priest-1 and priest-7 pass, with the heal, the stun and the slow in play; a1 failed on
  the row's design. Guard guard-a1, five class scopes: identical. Seven checks, unit suite
  and Fast (nr14-fast) pass. Scopes p, m and b now differ from their baselines until
  NR-15 and NR-17. Next: NR-15, the Priest's Ishalgen replayed and re-recorded.
- 2026-10-08 — Loop: NR-15 done. Replay m-a1 stopped at Q2128: after leaving a pack of
  three, the kill loop used its six failed pulls on monsters that were walking home. One
  small change (3ac9e756f): such a fight is not a failed pull. Replay m-a2 reaches Munin,
  41 quests, no death, 2 h 46 min 20 s against 2 h 51 min 38 s by the old rule.
  Guard guard-a1, nine scopes: identical. Seven checks, unit suite and Fast (nr15-fast)
  pass. Re-recorded at 3ac9e756f, two passes each: p 35,463 records, m
  109,039. Scope b still differs until NR-17. Next: NR-16, the Cleric on
  the table.
- 2026-10-08 — Loop: NR-16 done. The Cleric fights by the rule table natural-cleric-v1,
  written into the plan, and its catalog reaches level 26 (ten new ranks, two exclusions).
  Beside the old rule on 600,000 sampled states it decides 577,634 the same; the rest are
  six named differences. Java: Flashbolt opens Divine Spark one time in ten. Probe a6 and
  a6b: rows cleric-10, cleric-16, cleric-20 and cleric-25 pass, with the chain, the
  servant, Salvation and Root before a retreat in play; five earlier attempts changed the
  rows, not the table. Guard guard-a1, seven scopes: identical. Seven checks, unit suite
  and Fast (nr16-fast) pass. Scopes b, l1, c, hm and ax now differ from their baselines.
  Next: NR-17, the Cleric's scopes replayed and re-recorded.
- 2026-10-08 — Loop: NR-17 done. Replays b, l1, c, hm and ax at 60115ff14 all reach their
  endpoints on the first attempt, with no code change. Four are as good or better than the
  old runs; c has 3 deaths against 1 and 37 retreats against 25. Found: the journey does
  not answer an attack while it holds for a patrol or sweeps loot, in the old runs too;
  two of c's deaths are that. Re-recorded, two passes each: b 131,197, l1
  29,797, c 112,256, hm 37,510, ax 15,715 records. All seven
  Priest and Cleric scopes are recorded with the rule table. Next: NR-18, the old rule
  removed.
- 2026-10-08 — Loop: NR-18 done. The static rule, its adapter, the two hand-typed tables
  and the fight loop's second side are removed; 39 test methods of the removed rule went
  with it. The live driver of NI-04 and NI-06 decides by the Priest's table: compiled, not run
  (LIVE is outside the loop). Full gate gate-a1, twelve scopes: identical. Seven checks,
  unit suite (4,630 passed, 16 skipped) and Fast (nr18-fast) pass. Next: NR-19, the generic
  Cleric from creation to the Altgard endpoint.
- 2026-10-08 — Loop: NR-19 not done; two capture attempts, both stopped inside a leg. a1:
  leg l4, Q24013's item refused for its place (the server refreshes zones every 500 ms,
  Java too); one small change, 16ad6b05c, full gate identical. a2: twelve of fourteen
  stages, an hour faster than the accepted run with 5 deaths against 13, then stopped in
  the coin-gear leg, whose end check asks for the accepted run's cloth gloves by id. The
  continuous journey has not been able to finish since the table gear rules (CP-29a).
  Written: NR-19a, the replaced gloves read from the character. Next: NR-19a.
- 2026-10-08 — Loop: NR-19a done. The coin-gear end check and Haramel's incoming list read
  the gloves the character has: bound to the pair worn as leg cg starts, none worn none
  asked for; the cloth pair is kept when owned. One-time check on the a2 end state:
  coin-gear-complete when bound, and the Haramel receipt begins. Full gate guard-a1,
  twelve scopes: identical. Seven checks, unit suite (4,629 passed, 16 skipped) and Fast (nr19a-fast, with
  its three coin probes) pass. Next: NR-19 again, a fresh two attempts.
- 2026-10-09 — Operator, checking in: runs side by side are approved, as an item before
  phase D; several bots at the same time is a major requirement; nothing running is to be
  stopped. Written: the answer to NR-Q9, rule (w), and NR-43 and NR-44 at the head of
  phase C; NR-42 depends on them. No code. NR-19's attempt a3 (run nr19-continuous-a3 at
  709f33816) was running and was not touched. Next: NR-19, as before.
- 2026-10-09 — Operator, a second answer: not party play, but several bots in the same
  world in one run, each playing alone, ten game minutes apart if needed; log each, change
  code when all are done, play the next round. Rewritten: the answer to NR-Q9 and rule
  (w) (rounds; the gate and the captures stay one bot in its own world unless NR-43 finds
  otherwise), NR-43 (survey), NR-44 (several bots in one world), NR-45 (separate runs
  side by side). Known already: several sessions in one world exist; each moves the one
  virtual clock itself, so journeys cannot take turns yet. No code. Next: NR-19, as before.
- 2026-10-09 — Operator, a third answer: captures are accepted straight out of a shared
  world. Rule (w) and NR-43 changed to that: a class's captures come from a round; only
  the recorded scopes and the gate stay one bot in its own world. No code. Next: NR-19, as
  before.
- 2026-10-09 — Loop: NR-19 done. Attempt a3 (nr19-continuous-a3 at 709f33816) played all
  fourteen stages: altgard-complete-cleric-s1, 19 h 20 min against the accepted
  20 h 17 min, 5 deaths against 13, the same 176 quests, level 25 at the same bind.
  Verify nr19-verify-a1 passes. Next: NR-20, the generic Cleric through the Abyss entry.
- 2026-10-09 — Loop: NR-20 done on the first attempt. ntc-ready-cleric-s1 (run nr20-ax-a1,
  from altgard-complete-cleric-s1): a Cleric of level 26 alive at Morheim Ice Fortress
  with Q2945, Q2946, Q2947 and Q2042 complete, the same 181 quests, position and bind as
  morheim-abyss-entry-s1; no death. Verify nr20-verify-a1 passes. Next: NR-21, phase B
  closed.
- 2026-10-09 — Loop: NR-21 done. Phase B is closed: full gate gate-a1 at b5f8cbcef, twelve
  scopes identical. The generic Cleric from creation to the Abyss-entry endpoint:
  19 h 37 min against 20 h 34 min, 5 deaths against 13. Next: NR-43, the survey for several
  bots at once, the first item of phase C.
- 2026-10-09 — Loop: NR-43 done, the survey (Survey C1). Bots in one world take turns on
  one thread and four fifths of a run is the bot's side, so one world saves memory, not
  time; separate worlds side by side save time; a round can be both (NR-Q11, default:
  spread over eight worlds). Written: rule (w) in full, the phase D scheme for rounds,
  NR-45 (runs side by side), NR-46 (where the bot's side goes), NR-44 (turns in one
  world), NR-47 (the round and its snapshots). Seven checks pass. Next: NR-45.
- 2026-10-09 — Loop: NR-45 done. Runs side by side: a monitor port for each run, a mark
  for a run in progress with no build beside it, the gate's -Parallel, and a capture's
  commit read at its start. Full gate gate-p8, eight at a time: twelve scopes identical in
  544 seconds, against about 45 minutes one by one; ax alone afterwards identical. Script
  tests, seven checks, unit suite (4,629 passed, 16 skipped) and Fast (nr45-fast) pass.
  The guard gate is played with -Parallel 8 from here. Next: NR-46, where the bot's side
  of a run goes.
