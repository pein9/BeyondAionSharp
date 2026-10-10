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
- **NR-Q12. Accessories and hats at a reward.** Asked by NR-32. The gear score ranks
  weapons and armor. It does not rank a ring, an earring, a necklace, a belt or a hat of
  the HEAD group, which every class may wear; among those the choice went to the first in
  the list. Default: among those, and between two pieces of one score, the class's bonus
  order decides (the gear table's BonusOrder): a class that casts and heals looks for
  magic boost, magical accuracy, healing boost, mana, concentration; a class that casts
  for magic boost, magical accuracy, magical critical, mana, concentration; a class that
  strikes for physical attack, critical, accuracy, health. **The Cleric's table has no
  order**, because an order changes a pick of its recorded scopes: at Q2227, which is not
  pinned, the recorded Cleric took the first ring (+28 HP) and the healer's order takes
  the second (+28 MP). So the Cleric plays as recorded: its fourteen pins, and the first
  in the list elsewhere. To decide:
  - whether the Cleric gets the healer's order, which re-records scope c and what follows
    it. The order would then give twelve of its fourteen pins. It would not give the
    Topaz Necklace of Q24014 (+70 HP; the order takes the +70 MP one, as the pin of Q2288
    takes the +28 MP ring) or the Chain Helm of Q24015 (the order takes the Bandana, +87
    MP);
  - whether a class wants another order than its default.
  Until then every other class takes the order's pick.
- **NR-Q13. Help for a class that does not cast.** Asked by NR-34. Two defaults, each from
  what the starters' profiles already say, and each for the class's own first item to
  confirm:
  - **Who casts from mana.** The Priest, Mage and Artist lines keep mana potions and the
    Awakening scroll; the Warrior, Scout and Engineer lines do not. So Gladiator, Templar,
    Assassin, Ranger, Gunner and Rider are supplied no mana serum and no Awakening scroll.
    They restore mana with MP Recovery and the powder, which every second class has.
  - **The Courage scroll.** A class that keeps Courage in the shared scroll slot (the
    Warrior, Scout and Engineer lines) is supplied no scroll for it from level 20: OD-13
    approved the Awakening scroll only, and the allowlist is the operator's. It keeps a
    Courage scroll up only while it owns one. To decide: whether the Courage scrolls
    164000072 (20 to 29) and 164000073 (30 to 39) are approved in the Awakening scroll's
    counts.
- **NR-Q14. Bronze Coins for another class.** Asked by NR-38b. The operator approved 44
  Bronze Coins for the Abyss entry (the AX-Q5 revision), and the leg refuses a supply
  beyond that. The recorded Cleric needed 33: it arrived wearing quest rewards that most
  of the level-21 tier and the level-26 hauberk do not beat. A class that arrives worse
  dressed needs more: from the worst bag, the whole level-16 coin set and the arena
  weapon, every class would buy 55 to 74 coins' worth and need 48 to 67 supplied. Default:
  the 44 stands and no rule trims the purchases; a run that needs more stops at the
  vendor with the leg's own refusal and is reported. To decide, if that happens: more
  coins, or a rule for what to leave unbought.
- **NR-Q15. The Templar's shield.** Asked by NR-50. NR-Q5 gives the Templar a one-hand
  weapon and a shield, and the route hands it none as the code stands: the Warrior it was
  holds nothing in the off hand (CP-Q10), so it passes over the Raider's Shield of Q2100;
  no quest after it offers a shield; the coin vendors sell one at every tier and no leg
  buys it (NR-38a, NR-38b). Default, NR-50c: a line whose second class holds a shield
  holds one from the start, and a class that holds a shield and owns none takes an offered
  shield before a weapon. So the Templar's Warrior takes the Raider's Shield at Q2100, its
  own pick there as the class-profile plan foresaw (CP-Q10), and its swords at Q2002 and
  Q2134 as the Warrior does. NR-54a: each coin tier buys the vendor's shield from the
  coins left after the armor and the weapon. Nothing is bought with Kinah (CP-Q10) and
  no coin is added. To decide: whether a Templar may buy a shield with Kinah from an
  armor merchant at Ascension, or is supplied the coins for one.
- **NR-Q16. A class that ends the Abyss missions at level 25.** Asked by NR-56. The
  accepted Cleric is level 26 when its missions are done, and its leg then buys the
  level-26 coin tier. The Templar is level 25 there, and the leg stopped: the fortress
  quests that would give the level are not listed (AX-11), and no level is hunted for. The
  plan's finish asks for level 25 or higher (NR-Q3). Default, NR-56b: for a class other
  than the contract's, the leg's level is the level it starts from, 25. A coin tier above
  the character's level cannot be worn and is not asked, and the leg ends at Morheim Ice
  Fortress with the missions complete. A class that is level 26 by then is asked the last
  tier as the Cleric is, and meets NR-Q14 there. To decide: whether such a class should
  play fortress quests to level 26 for the last tier (AX-11), and with it the coins of
  NR-Q14.
- **NR-Q17. May a Cleric scope be recorded again for where a stagger puts a monster?**
  Asked by NR-110g. The bot does not read where a stagger, a stumble, a lift or a pull
  puts a monster. The server says it to a client only inside the cast result, and for a
  weapon's swing inside SM_ATTACK. The fix is generic: every class then sees such a
  monster where it is, from the cast result on and not from the monster's next move on.
  With it the Cleric's scope b differs first at record 125,694 of 131,197, where a
  critical Hallowed Strike with the staff makes its target stumble 2 m, and the Mage's
  scope at record 18,915 of 23,555, after Frozen Shock; the other eleven scopes are
  identical. NR-Q2 keeps the Priest's and the Cleric's scopes identical outside phase B.
  Default: the scopes stay as they are. NR-110g waits under Blocked with its change kept
  as a patch, and the Spirit Master goes round it by NR-110h. To decide: whether scope b
  may be recorded again for this fix, with the Mage's scope by rule (j).

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
| C. The legs opened by class line | NR-30 to NR-49 | The trial, the class choice, the bridge and every leg take the class from the line: identity, contracts, rewards, coin gear, kit, the leg-specific skills. The Cleric's scopes stay identical. First in the phase: several bots at once (NR-43 to NR-48). |
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

Corrected by NR-32 (2026-10-09): the server offers a class list at two of the four, Q2009
and Q2947, which have use_class_reward. Q2900 and Q28505 carry class lists in the data
without it, and Java never reads them (QuestService.getRewardItems 172-183): Q2900 offers
no choice, and Q28505 offers every class its general list of six armor pieces. And a leg
contract's start.class is read from NR-32 on: it says whose the leg's reward picks are.

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
| Account and name | Corrected by NR-44: every class line plays on the one SIM account 41 (CP-Q19); only the character name is the line's, and the Cleric's and the Chanter's lines share one name. A round gives each seat an account of its own. |
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
- [x] **NR-46 - Where the bot's side of a run goes.** Depends: NR-45
  - Work: No committed code. With a one-time probe on scope c, split the bot's side into
    its parts: reading and decoding packets, the bot's world model, decisions, routes, the
    trace, the monitor. Write the table here. For each part that is more than a tenth of
    the run and can be made cheaper without changing a trace, write a lettered item.
  - Proof: The table is in this document; the probe is removed and the tree is clean.
  - 2026-10-09: done. Half of a run is one function, the tracing of an edge on the
    ground. The parts the survey expected to cost (packets, the world model, decisions,
    the trace, the monitor) cost almost nothing.
    - **How it was measured.** A one-time probe, not committed, of named sections with
      exclusive time, on replays of two scopes (run/nr/NR-46/: split-c, split-c2, split-c3
      and split-mage, all passed). Three passes on scope c, each with more sections.
    - **Scope c, Altgard leg 4: 298.3 seconds** (the third pass, split-c3.txt).

      | Part | Seconds | Share | Calls |
      |---|---|---|---|
      | Tracing an edge on the ground (BotNavigationGeometry.TraceEdge) | 154.9 | 51.9% | 1,362,870 |
      | The journey's own code, not split further | 57.9 | 19.4% | |
      | Navmesh routes | 34.8 | 11.7% | 683 |
      | The server: its clock | 34.3 | 11.5% | 19,114 |
      | The server: handling the bot's packets | 7.4 | 2.5% | 42,076 |
      | The grid search, without its edge traces | 4.3 | 1.4% | 150 |
      | The trace, building and writing its lines | 1.2 | 0.4% | 112,257 |
      | Building the waypoint graph | 1.1 | 0.4% | 2 |
      | Line of sight | 0.6 | 0.2% | 11,844 |
      | Loading the inventory policy | 0.5 | 0.2% | 3 |
      | Decoding packets, the world model, the problem policy, the monitor, fight and leg decisions, together | 0.9 | 0.3% | |

      - Nearly all the edge traces come from the grid search. With its traces counted in
        (the second pass) the grid search is 139.9 seconds over 150 searches: almost a
        second a search. It is the fallback for a route the navmesh does not give within
        60 m.
      - So routes are 65% of this scope, the server 14%, the journey's own code 19%.
    - **Scope mage, Ishalgen to Q2004: 30.1 seconds** (one pass, split-mage.txt).

      | Part | Seconds | Share | Calls |
      |---|---|---|---|
      | Waiting in real time for a packet | 10.0 | 33.3% | 12,644 reads |
      | The grid search with its edge traces | 7.4 | 24.7% | 23 |
      | The server: its clock | 5.0 | 16.7% | 6,082 |
      | The server: handling the bot's packets | 1.8 | 6.1% | 13,954 |
      | Navmesh routes | 0.5 | 1.7% | 57 |
      | Everything else, the journey's own code in it | 5.4 | 17.5% | |

      - The ten seconds are two waits of five real seconds for a loot list that did not
        come (NaturalIshalgenJourney.cs, the loot of a corpse). Nothing can arrive during
        such a wait: a bot's packets come only when it sends or waits in game time. Scope
        c had none.
    - **What follows.** Four lettered items, below. The server's own 14% is the port's
      code and is not this plan's to speed up.
    - **Proof.** The tables above. The probe is removed, the tree is clean and the build
      outputs are rebuilt without it.
- [x] **NR-46a - The grid search and its edge traces.** Depends: NR-46
  - Work: First count, with a one-time probe on scope c: how many of the 150 searches end
    with no route, how many cells each visits, and how often one search traces the same
    edge twice. Then make the search cheaper with the same answers: an edge traced once in
    a search is not traced again, and whatever else the count shows. No route may change.
  - Proof: The full gate with -Parallel 8 identical. Scope c's wall time before and after.
  - 2026-10-09: done. Scope c plays in 3 min 28 s where it took 4 min 55 s, and every
    recorded scope is identical.
    - **Java.** No server behavior is involved.
    - **The count** (a one-time probe, removed; run/nr/NR-46a/count-c.txt, 150 searches of
      scope c, 1,255,915 edge traces, 141.0 seconds):
      - 141 of the 150 searches end with no route, and they are all of the time. The nine
        that find a route visit two cells each.
      - No search reaches its budget. A search that fails has walked all the ground it can
        reach: a median of 504 cells, 11,380 at the most, eight traces a cell.
      - 90 are the hazard-avoiding search and all 90 fail (119.3 s). 63 of them are the
        retreat trying its escapes one after another from where the bot stands (94.0 s);
        18 are the navigator's search around observed monsters (25.2 s). The other 57 are
        local repairs of a navmesh route (15.0 s).
      - 112 of the 150 start where another search started; 16 from one spot at the most.
      - Inside one search no edge is traced twice. What repeats inside a search is the
        ground under a cell, asked once for each of its eight neighbours; and from search
        to search from the same spot, whole edges.
    - **The change** (Sc/../Navigation/BotNavigationGeometry.cs: RememberEdges, EdgeMemory).
      - Inside a scope, a trace asked again gets the answer it got, and the ground under a
        point is asked once. The key is the exact bits of the points, never a near point.
        The destination's heading is not part of the question; it is stamped on the
        answer.
      - A scope covers one instant only, a stretch that neither sends a packet nor lets
        game time pass, because a door or another obstacle of the instance can change
        between instants. Nothing is kept from one instant to the next.
      - Scopes: every grid search; every navmesh question with its leg checks and repairs;
        the interaction search's seventeen grid searches; the retreat's loop over its
        escapes (NaturalIshalgenJourney.Combat.cs); the navigator's six close-in searches
        (NaturalIshalgenJourney.Navigator.cs).
      - Not given a scope: the rest relocation's loop, which walks inside its body. It was
        not among scope c's costs.
      - On scope c 679,721 traces were answered from memory and 625,100 were traced; the
        ground was answered from memory 515,154 times and asked 109,946 times
        (run/nr/NR-46a/hits-c.txt, a temporary count, removed).
    - **What is left of the grid search.** A failing search still walks its ground once.
      The navigator's 18 single searches (25 s) and the first search of each retreat are
      what remains; a cheaper way to learn that no route exists would change the search
      and is not attempted here.
    - **Proof.**
      - Gate, set all+mage+warrior+artist+engineer+scout, -Parallel 8, run guard-p8
        (run/nr/NR-46a/guard-p8/verdict.json): verdict pass, all twelve scopes identical to
        their baselines, in 459 seconds where NR-45's gate took 544.
      - Scope c alone, run after-c: identical; the journey takes 3 min 28 s against 4 min
        55 s before (run/nr/NR-21/gate-a1-c), 29% less.
      - Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629
        passed, 16 skipped) and Fast passes (run nr46a-fast, 11 passed).
- [x] **NR-46b - A wait that no packet can end.** Depends: NR-46a
  - Work: A wait with a real-time limit (five seconds for a loot list, two for a quest's
    reply) sits out its limit although nothing can arrive. In the SIM session such a wait
    ends at once when no packet is queued, with the same outcome as the limit. The live
    session is not touched.
  - Proof: The full gate with -Parallel 8 identical. Scope mage's wall time before and
    after.
  - 2026-10-09: done. Scope mage plays in 19 seconds where it took 30, and every recorded
    scope is identical.
    - **Java.** No server behavior is involved.
    - **The three waits.** Two for a loot list, five seconds each
      (NaturalIshalgenJourney.cs, the loot of a corpse and the loot of a quest item), and
      one for the reply to a quest's hand-in, two seconds (the SIM session's
      FinishQuestAsync). No other wait in the bot library has a real-time limit of its
      own. Every wait still has the session's cap of three minutes, which is a failure.
    - **The change.**
      - Sc/INaturalJourneySession.cs: WaitForPacketWithinAsync(type, realTime, token,
        predicate) waits at most that long of real time and gives null when the packet did
        not come. Its default body is the limit as it was, so the live session behaves as
        before and is not edited.
      - The two loot waits call it. A missing list has the outcome it had: nothing taken,
        and for a quest item the "quest-loot-list-missing" record.
      - The SIM session answers it without the wait. Its packets are queued only while it
        sends or lets game time pass, so once no read is pending and its queue is empty,
        nothing more can come and the answer is null at once. The quest reply's wait uses
        the same call and fails as before when no reply is there.
      - tests/Aion.Bots/Transport/InProcessBotTransport.cs counts the packets it has
        queued and not yet handed over; the channel it uses cannot be asked.
      - If an earlier wait left a read pending, the limit is kept: that read's packet may
        be on its way from another thread. After this change no wait of the journey
        leaves one.
    - **Proof.**
      - Gate, set all+mage+warrior+artist+engineer+scout, -Parallel 8, run guard-p8
        (run/nr/NR-46b/guard-p8/verdict.json): verdict pass, all twelve scopes identical to
        their baselines, in 446 seconds.
      - Scope mage alone, run after-mage: identical; the journey takes 19 seconds against
        30 before (run/nr/NR-21/gate-a1-mage).
      - The whole solution builds, the live tool with it. Bundle: the seven pre-commit
        checks pass, Aion.GameServer.Tests passes (4,629 passed, 16 skipped) and Fast
        passes (run nr46b-fast, 11 passed).
- [x] **NR-46c - The journey's own code.** Depends: NR-48
  - Work: A fifth of scope c is the journey's own code and was not split. Split it with a
    one-time probe (pull planning, movement plans, what it observes each step) and write
    the table. A part over a tenth of the run that can be made cheaper with the same
    answers is fixed here; otherwise the table closes the item.
  - Proof: The table is in this document; the full gate identical if code changed.
  - 2026-10-09: done, with no code. The journey's own code is about a fiftieth of the
    run. What NR-46 measured as a fifth was the navmesh router, which the journey also
    asks directly, past the place that was measured.
    - **How it was measured.** The probe of NR-46 again, with sections on the journey's
      own parts, on scope c in two passes (run/nr/NR-46c/split-c.txt and split-c2.txt;
      both replays passed). The probe is removed and the tree is clean.
    - **The first pass** put 30.6 seconds on the pull (44 pulls), 16.3 on the approach to
      a spawn and 11.4 on the navigator's route search, and under a second on the fight's
      loop, the rest, the retreat, the hook before every send and the movement plans.
    - **The second pass** timed the navmesh router itself, and the pull's and the
      approach's time went to it. Scope c, 208.9 seconds:

      | Part | Seconds | Share | Calls |
      |---|---|---|---|
      | The navmesh router finding a path, without its edge traces | 75.2 | 36.0% | 1,178 |
      | Tracing an edge on the ground | 67.6 | 32.4% | 683,149 |
      | The server: its clock | 33.2 | 15.9% | 19,114 |
      | The navigator's route search, its own part | 10.1 | 4.8% | 153 |
      | The server: handling the bot's packets | 7.2 | 3.5% | 42,076 |
      | The grid search, without its edge traces | 4.1 | 2.0% | 150 |
      | The approach to a spawn, its own part | 3.8 | 1.8% | 109 |
      | The journey, everything not named here | 2.3 | 1.1% | |
      | The trace | 1.2 | 0.6% | 112,257 |
      | The pull planner, the pull, the fight's loop, the retreat, the rest, the navigator's observation, the world model, movement, the hook before every send, together | 2.9 | 1.4% | |

      - The router is asked 1,178 times: 683 times through the geometry's route methods
        and 495 times directly, most of them by the pull, which asks whether each firing
        spot it considers can be reached. A path costs 64 ms without its traces.
      - Most of the edge traces that are left are the router's own checks of its legs.
      - So routes are 71% of this scope now, the server 19%, and everything the journey
        decides is under 3%.
    - **Nothing to fix here.** No part of the journey's own code is a tenth of the run.
      The router is NR-46d's, whose text is brought up to these numbers.
- [x] **NR-46d - Navmesh routes.** Depends: NR-48
  - Work: The navmesh router is 36% of scope c without its edge traces, and most of the
    remaining 32% of edge traces are its leg checks (NR-46c): 1,178 paths at 64 ms, 495 of
    them asked directly by the journey, mostly by the pull for each firing spot. First
    count, with a one-time probe: how many paths are asked again at the same instant with
    the same arguments, and where a path's time goes (the corridor search, the pulling of
    the string, the checks and repairs of its legs). Then reuse an answer only where it
    is certain to be the same, and make cheaper what the count shows, with the same
    paths.
  - Proof: The full gate with -Parallel 8 identical. Scope c's wall time before and after.
  - 2026-10-09: done. Scope c plays in 2 min 25 s where it took 3 min 28 s, and every
    recorded scope is identical. Since NR-46 it has halved, from 4 min 55 s.
    - **Java.** No server behavior is involved.
    - **The count** (a one-time probe, removed; run/nr/NR-46d/split-c.txt and
      count-c.txt, scope c):
      - 1,178 paths take 88.7 seconds. The 727 asked with observed hazards take 81.0 of
        them; the 451 without take 7.7.
      - 364 paths end "hazard rejected" and take 62.5 seconds; the 802 that route take
        21.8.
      - A path asked again at the same instant with the same arguments: 30 times, 0.4
        seconds. There is nothing to gain by keeping answers.
      - Where a path's time goes: the local detour around hazards 76.6 seconds over 1,587
        detours; the corridor and its corners 7.9; resolving zones, checking legs and
        accepting edges under a second together.
    - **Why the detour cost so much.** It searches a grid of one-metre cells around the
      place where a route breaks the hazard rule. For every neighbour of every cell it
      opened it asked again whether that neighbour lies in a forbidden circle and what a
      step onto it costs: a test against every observed hazard, forty and more in leg 4,
      up to eight times for one cell.
    - **The change** (tests/Aion.Bots/Navigation/NavMesh/BotNavMeshRouter.cs,
      LocalDetour): the answer is kept for the cell. Every one of those tests reads the
      point's X and Y alone, so the answer does not depend on the cell the search came
      from. The cells are opened in the same order with the same costs, and the detours
      are the same.
    - **Proof.**
      - Scope c alone, run after-c: identical; the journey takes 2 min 25 s against 3 min
        28 s after NR-46a, 30% less.
      - Gate, set all+mage+warrior+artist+engineer+scout, -Parallel 8, run guard-p8
        (run/nr/NR-46d/guard-p8/verdict.json): verdict pass, all twelve scopes identical to
        their baselines, in 391 seconds.
      - Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629
        passed, 16 skipped) and Fast passes (run nr46d-fast, 11 passed).
    - **What is left.** The server's own work is now the largest single part of a run.
      The grid search still walks its ground once when it fails, and a path with hazards
      still costs most where no detour exists.
- [x] **NR-44 - Bots take turns in one world.** Depends: NR-45
  - Work: Survey C1, parts 4 to 6. Java first: who gets a quest's kill when two players
    hit one monster. The turn table; the wait that can yield; a bot id, options, trace
    folder and help count for each bot; a problem laid to the bot whose turn raised it; a
    wall-clock limit sized by the world's bots.
  - Proof: The full gate identical: a bot alone plays as before. One world with the six
    starter lines through Ishalgen to Q2004, ten game minutes apart: each bot has its own
    trace and outcome record; the same world played twice gives the same six traces; a bot
    stopped on purpose leaves the others playing.
  - 2026-10-09: done. Six starters played Ishalgen in one world, twice, with the same six
    traces. A bot alone plays as before.
    - **Java first: a kill hit by two players** (NpcController.doReward, lines 201 to
      231; the port's NpcController.cs is the same). Every living player in the monster's
      damage list gets the quest engine's kill, and experience by its share of the damage.
      The loot belongs to the one with the most damage. So two bots on one monster both
      get a kill quest's credit; a quest's item drop goes to one of them.
    - **The change.**
      - tests/Aion.Simulation.Tests/SimulationTurnTable.cs, new. A bot's wait is "wake me
        at now + dt". The table moves the clock to the earliest wake and lets that one bot
        play until its next wait or its end; bots due at the same instant go in seat
        order. Either the table runs or one bot does, never both. A bot that throws ends
        alone. A seat can start later and can be stopped on purpose at a game time.
      - The table's thread has no synchronization context. The first form had one, and
        hung at a bot's first wait: the clock runs the server's own work and waits for it,
        and that work could not finish on a thread that was waiting. A bot may come back
        from a real wait of its own on another thread; the table does nothing meanwhile.
      - tests/Aion.Bots/Transport/InProcessBotTransport.cs: a transport either moves the
        clock itself, as before, or waits for its turn and serializes what the world queued
        for it when the turn comes back.
      - The SIM session takes a turn table (Turns). With one, its wait, its offline wait
        and its wait for re-entry go to the table. Without one nothing is changed.
      - tests/Aion.Simulation.Tests/SimulationNaturalRoundTests.cs, new:
        NaturalRoundPlaysItsBotsInOneWorld plays the bots of a round file (NR_ROUND_FILE:
        line, start offset in game minutes, stop boundary, a stop on purpose). Each bot
        has its seat's bot id and account (111 and up), its line's character name, its own
        session, problem policy, help count, options and folder: trace, the journey's
        receipts, the failure record and outcome.json. round-outcome.json has them all.
        The wall-clock limit is 45 minutes for each bot.
    - **Proof.**
      - **Six starters, one world, ten game minutes apart** (run nr44-six-a1,
        run/nr/NR-44/six-a1; round file round-6.json; stop boundary Q2004):

      | Seat | Line | Starts at | Outcome | Level | Quests | Its game time |
      |---|---|---|---|---|---|---|
      | b01 | priest-cleric | minute 0 | reached | 8 | 11 | 30 min 00 s |
      | b02 | warrior | minute 10 | reached | 8 | 11 | 33 min 29 s |
      | b03 | mage | minute 20 | reached | 8 | 11 | 31 min 38 s |
      | b04 | artist | minute 30 | reached | 8 | 11 | 31 min 21 s |
      | b05 | engineer | minute 40 | reached | 8 | 11 | 32 min 01 s |
      | b06 | scout | minute 50 | stopped | 6 | 9 | 22 min 58 s |

      - **Played twice** (run nr44-six-a2): the same outcomes to the millisecond, and all
        six traces identical by the comparer (run/nr/NR-44/six-compare.txt): 23,605,
        25,590, 24,934, 24,207, 25,634 and 17,696 records.
      - **A bot that stops leaves the others playing.** In both plays the Scout stopped by
        itself and the five before it went on to their end. On purpose (run nr44-stop-a1,
        round-stop.json): the Warrior was stopped ten game minutes after its start; the
        Mage before it and the Artist after it reached Q2004.
      - **A bot alone plays as before.** Gate, set
        all+mage+warrior+artist+engineer+scout, -Parallel 8, run guard-p8
        (run/nr/NR-44/guard-p8/verdict.json): verdict pass, all twelve scopes identical to
        their baselines.
      - Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629
        passed, 16 skipped) and Fast passes (run nr44-fast, 11 passed).
    - **Time.** The six-bot world played in 1 min 36 s. The same six alone are about 2 min
      50 s of play together, each with a world load of its own on top. So after NR-46a and
      NR-46b a shared world is cheaper than the runs one after another, not equal to them
      as Survey C1 measured before those two. Six worlds side by side would still finish
      first, in about a minute. NR-Q11's default stands; its numbers are these now.
    - **Found, and logged (rule (f)).**
      - **The Scout's stop is what a shared world does.** It reached the place of its
        fourth Q2003 kill and saw no monster: "Reached the static area anchor but no NPC
        was observed" (step ni07-q2003-kill-4, level 6, 9 quests). Five bots had hunted
        there before it. Alone, a bot never finds a hunting ground empty, so the journey
        has no wait for a respawn there. This is the first thing a round of classes will
        need; NR-47's rounds will show how often.
      - **A server problem is not yet laid to one bot.** Each bot has a problem policy of
        its own from its start, so a problem logged while several bots are in the world is
        seen by each of them. NR-44a.
      - **The Cleric's and the Chanter's lines share a character name**, so they cannot be
        in one world as they are. NR-47 gives a round's bots their names.
- [x] **NR-44a - A server problem is laid to the bot whose turn raised it.** Depends: NR-47
  - Work: The turn table knows whose turn it is when a problem is logged. A problem raised
    in a bot's turn is that bot's alone; one raised while the clock moves is the world's,
    is written into the round's record, and stops no bot that did not meet it.
  - Proof: A one-time check, not committed, that logs a server problem in one bot's turn
    of a two-bot round: that bot stops and the other reaches its end. The full gate
    identical.
  - 2026-10-09: done. A bot answers for the problems raised in its own play; the world's
    are written into the round's record and stop no bot.
    - **Java.** No server behavior is involved.
    - **How a problem is laid to a bot.** The turn table did not have to be asked. A log
      entry carries the scope it was raised in, and the scope follows a bot's play from
      turn to turn. A bot's whole play in a round is now marked with its bot id. An entry
      raised while the table moves the clock between turns has no bot.
    - **The change.**
      - tests/Aion.Simulation.Tests/SimulationLogPolicy.cs: Owns, a test on the "bot" of
        the scope a problem was raised in. A policy with it answers only for the problems
        it owns; a virtual timer's failure is the world's. Without it a policy answers for
        every problem, as before. SnapshotUnallowlisted reads the problems the allowlist
        does not cover without completing the policy.
      - The round test: each bot's policy owns the entries of its own bot id, and its play
        is marked for as long as it lasts. A policy of the world owns the entries with no
        bot. After the round its problems are printed and written into round-outcome.json
        as worldProblems. They do not fail the round.
    - **Proof, the one-time check** (two lines, not committed; run nr44a-check,
      run/nr/NR-44a/check-a1, round file run/nr/NR-44/round-2.json): an error logged in the
      Mage's play and one logged outside any bot's play, before the round's first turn.
      - The Mage stopped: its journey's own end check found "1 unallowlisted problem".
      - The Warrior, in the same world, reached Q2004.
      - The world's problem is in round-outcome.json's worldProblems and stopped neither.
    - **Guard.** Gate, set all+mage+warrior+artist+engineer+scout, -Parallel 8, run
      guard-p8 (run/nr/NR-44a/guard-p8/verdict.json): verdict pass, all twelve scopes
      identical to their baselines. Seven pre-commit checks pass; Fast passes (run
      nr44a-fast, 11 passed).
- [x] **NR-47 - The round: runner, outcome records and round snapshots.** Depends: NR-44
  - Work: Survey C1, parts 7 and 8. The round file, the runner that starts its worlds side
    by side, the progress line and the outcome record for each bot, the round snapshot
    with its list of characters, a class's capture as a record that points into it, Verify
    of such a capture, and a round that resumes characters from a round snapshot.
  - Proof: The script tests pass. A round of two worlds with three starter lines each, to
    Q2004: six outcome records and two round snapshots. Verify of one character's capture
    passes. A second round resumes all six from the snapshots and each reports where it
    stands.
  - 2026-10-09: done. A round of two worlds was played and captured, one character's
    capture verifies, and a second round resumed all six characters.
    - **Java.** No server behavior is involved.
    - **One change from the proof as written.** The rounds play to Munin, the end of the
      plain journey, not to Q2004. Q2004 is a diagnostic stop boundary, and a capture has
      to be of an endpoint the journey itself checks.
    - **The change** (commit ee97c1376).
      - scripts/sim/run-round.ps1, new. A round file names worlds, and bots in each world.
        The runner builds once and starts one process for each world, side by side. A
        world has a throwaway schema of its own and marks its run. round.json has one
        outcome record for each bot; each bot's folder has its trace, the journey's
        receipts and outcome.json. The exit code is 0 when every world played to its end.
      - **-Capture**: a world's schema is dumped once when its last bot has ended, as the
        round snapshot <name>-w<n>, with a record of every character in it: seat, line,
        account, name, character id, outcome, level, quests, and the step and message of a
        stop. A bot's own capture is a record that points at the round snapshot and its
        character, written when the bot reached its end. The dump is not copied. A
        captured round wants committed code, as every capture does.
      - **-From**: each world is restored from its round snapshot, and a bot resumes its
        line's character on that character's account and under its name. The world's
        clock goes on from the snapshot's.
      - scripts/sim/sim-snapshot.ps1 restores a pointing capture: the round's dump and
        clock, the one character, and its seat's account and name. The journey's test
        takes those two from NI08_RESUME_ACCOUNT and NI08_RESUME_NAME; unset, they are the
        line's as always.
      - The round test resumes a character, lets a seat give its account and its
        character's name, and writes the world's clock and each character's id.
      - scripts/sim/test-sim-snapshot.ps1: a pointing capture restores its world's dump
        with the character, account, name, line and clock; one that names a character its
        round does not hold, another seat's account, or a missing round snapshot is
        refused.
    - **Guard, before the code was committed.** Gate, set
      all+mage+warrior+artist+engineer+scout, -Parallel 8, run guard-p8
      (run/nr/NR-47/guard-p8/verdict.json): twelve scopes identical. The script tests pass
      (run/nr/NR-47/script-tests.log). Seven pre-commit checks pass; Fast passes (run
      nr47-fast, 11 passed).
    - **Proof.**
      - **Round 1** (run r1-a1 at ee97c1376, -Capture nr47-r1; run/nr/NR-47/r1-a1; round
        file round-munin.json): two worlds side by side, three starter lines in each, ten
        game minutes apart, the plain journey to Munin. 943 seconds of wall time.

      | World | Seat | Line | Outcome | Level | Quests | Game minutes | Where it stopped |
      |---|---|---|---|---|---|---|---|
      | 1 | b01 | priest-cleric | reached | 9 | 41 | 197.7 |  |
      | 1 | b02 | warrior | stopped | 9 | 13 | 58.8 | ni07-q2007-green-generator: Cast 2865 rejected: STR_SKILL_TARGET_IS_NOT_VALID. |
      | 1 | b03 | mage | reached | 9 | 41 | 186.8 |  |
      | 2 | b01 | artist | reached | 9 | 41 | 194.6 |  |
      | 2 | b02 | engineer | stopped | 8 | 11 | 51.6 | ni07-q2005-stalker-17: Cast 1958 rejected: STR_SKILL_TARGET_IS_NOT_VALID. |
      | 2 | b03 | scout | stopped | 9 | 13 | 134.0 | ni07-q2007-rejoin-700087-3: Natural journey made no quest, quest-item or level progress for 01:00:00. |

      - **Two round snapshots**: nr47-r1-w1 and nr47-r1-w2, each with its world's three
        characters, reached or stopped. Three captures that point into them:
        munin-priest-r1, munin-mage-r1 and munin-artist-r1.
      - **Verify of one character's capture**: sim-snapshot.ps1 -Action Verify -Name
        munin-mage-r1, run nr47-verify-mage (run/nr/NR-47/verify-mage.log): "Verified
        snapshot munin-mage-r1: character 134202 resumed at its endpoint." The world's
        dump was restored with its three characters and the Mage was resumed alone, on
        account 113.
      - **Round 2** (run r2-a1, -From nr47-r1; round file round-munin-2.json): all six
        resumed their own characters, the same ids on the same accounts. 509 seconds.

      | World | Seat | Line | Outcome | Level | Quests | Game minutes | Where it stopped |
      |---|---|---|---|---|---|---|---|
      | 1 | b01 | priest-cleric | reached | 9 | 41 | 0.0 |  |
      | 1 | b02 | warrior | reached | 9 | 41 | 120.5 |  |
      | 1 | b03 | mage | reached | 9 | 41 | 0.0 |  |
      | 2 | b01 | artist | reached | 9 | 41 | 0.0 |  |
      | 2 | b02 | engineer | stopped | 9 | 13 | 15.3 | ni07-q2007-green-generator: Cast 2220 rejected: STR_SKILL_TARGET_IS_NOT_VALID. |
      | 2 | b03 | scout | reached | 9 | 41 | 125.1 |  |

        The three that had reached Munin report it at once. The Warrior and the Scout go
        on from where they stopped and reach Munin. The Engineer goes on and stops again.
    - **Found: a bot meets what another bot has taken.** Four of the six stops of the
      rounds so far are of one kind, and a bot alone never meets it.
      - A cast is refused with STR_SKILL_TARGET_IS_NOT_VALID (the Warrior at Q2007's
        generator in round 1, the Engineer there in round 2 and at a stalker of Q2005 in
        round 1). The journey throws on a refused cast. Corrected by NR-48: the target had
        not gone. It was a monster another bot had killed, still standing in this bot's
        view.
      - The Scout made no progress for an hour on its way to Q2007's object 700087 in
        round 1. Corrected by NR-48: the object was not missing. The Scout fought through
        the corridor of monsters before it again and again and was pushed back each time.
        That is a stop of another kind.
      - A hunting ground is empty: the Scout at Q2003 in NR-44's round.
      - NR-48 is written for it. It is the first thing rounds of classes need.
- [x] **NR-48 - A bot meets what another bot has taken.** Depends: NR-47
  - Work: Java first: when a used quest object and a killed monster come back. Three
    cases, each a stop today and each an outcome of a shared world: a cast refused because
    its target has just gone; a quest object that is not there to use; a hunting ground
    with no monster in it. In each the bot looks again and waits for what it needs to
    come back, inside the stall budget it already has, and goes on. Generic: no branch on
    a class or a quest. A bot alone must play as before.
  - Proof: The full gate with -Parallel 8 identical. The round of round-munin.json played
    again in two worlds: all six reach Munin, or what still stops is of another kind and
    gets its own item.
  - 2026-10-09: done. The same round now takes all six starters to Munin, and a bot alone
    plays as before.
    - **Java first.** Skill.java, canUseSkill, lines 244 to 248: a skill on a dead target
      is refused with STR_SKILL_TARGET_IS_NOT_VALID. A monster's death is sent to the
      players around it as an emotion, and its corpse is removed later; when it comes back
      is its spawn group's respawn time, which the journey's wait for an empty place
      already reads.
    - **What the stops were.** The traces of NR-47's refused casts show the target still
      in the bot's view and no SM_DELETE for it. The bot's world model takes a monster for
      dead only from the state in its SM_NPC_INFO or when it is removed; it does not read
      a death out of an emotion. Alone, a bot is the only one that kills, so it never
      walks up to a corpse it did not make. In a shared world it does, and casts on it.
    - **The change** (generic: no class, no quest).
      - Sc/NaturalIshalgenJourney.Combat.cs, the cast: a refusal with that message on
        another object sets the target aside (the navigator's unavailable objects), counts
        it (TargetsTaken), records "combat-target-taken" and ends the fight without a
        kill. Every other refusal throws as before.
      - The same file, KillAsync, which the Ishalgen quest steps call for a kill they
        need: when the target was taken it returns. The step hunts by its quest's count
        or its item count, finds it unchanged, and goes for the next monster. This was
        the one small change of rule (e), after attempt a1.
      - Sc/NaturalIshalgenJourney.cs, the kill loop: a monster that is gone from the
        bot's view when the fight's approach fails is a taken target, not a blocked
        approach. Taken targets are counted apart from the six failed pulls, twenty-four
        to a hunt. The place fills again through the wait the loop already has.
      - Not done: the world model does not learn a death from the emotion. That would
        change what a bot alone sees wherever a guard kills a monster, and so the recorded
        scopes.
    - **Proof.**
      - **Attempt a1** (run r3-a1, run/nr/NR-48/r3-a1; round file
        run/nr/NR-47/round-munin.json; two worlds, three starters each, to Munin): five
        reach Munin. The Warrior and the Engineer met a taken target three times each and
        went on. The Priest stopped at Q2105: its cast was refused, the target was set
        aside, and the quest step that needs a kill threw.
      - **Attempt a2** (run r3-a2, run/nr/NR-48/r3-a2, with the small change): **all six
        reach Munin**, level 9 with 41 quests each. A taken target was met fourteen times:
        the Priest six, the Warrior four, the Engineer three, the Mage one. 1,314 seconds
        of wall time.
      - **Not shown in play:** the kill loop's own case, a monster gone before the bot
        reaches it, did not occur in either attempt ("kill-target-taken" 0 times). It is
        the Scout's stop of NR-44's round, and stays untested until a round meets it.
      - **A bot alone plays as before.** Gate, set
        all+mage+warrior+artist+engineer+scout, -Parallel 8, run guard-p8
        (run/nr/NR-48/guard-p8/verdict.json): verdict pass, all twelve scopes identical.
      - Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629
        passed, 16 skipped) and Fast passes (run nr48-fast, 11 passed).
    - **Logged, not fixed (rule (f)).**
      - Q2114's hunt counts its attempts to ten. Taken targets use attempts up, so in a
        crowded world it could end before the quest is done.
      - The Scout's hour in the corridor before Q2007's object (NR-47, round 1) did not
        come back in three more rounds.
- [x] **NR-30 - The class line carries the pair, the pick and the dispatch; the scripts
  accept every line.** Depends: NR-01, the close of phase B
  - Work: A class line with a second class names its ceremony pick, and ForLine passes it
    (CP-26's finding). The capital contract's dispatch quest is the line's.
    scripts/sim/sim-snapshot.ps1 takes its line ids and its capital lines from one list
    that holds every line with a second class; a snapshot of such a line records it.
  - Proof: scripts/sim/test-sim-snapshot.ps1 passes; gate p, b and l1 identical.
  - 2026-10-09: done. A line names its ceremony pick, the capital pass has the line's
    dispatch quest, and the snapshot script has one list of lines. The accepted line
    plays as recorded.
    - **Java.** Read for Survey A1 and not changed: quest_data.xml gives a class limit to
      the dispatch quest alone on the route, and the ceremony's reward list is by class
      (_2009ACeremonyinPandaemonium.java).
    - **The change.**
      - Sc/Classes/NaturalClassLine.cs: CeremonyItemId, the weapon the line takes at
        Q2009. Null is the reviewed bridge's pick, the staff, which the Cleric's and the
        Chanter's lists offer; the two lines that exist keep it.
      - Sc/NaturalAscensionContract.cs, ForLine, and Sc/NaturalIshalgenInventoryPolicy.cs:
        both pass the line's pick, which CP-26 found they did not.
      - Sc/NaturalCapitalContract.cs, ForLine: the reviewed walkthrough with the dispatch
        quest of the line's second class from the class-line contract. The journey loads
        it for its line. Nothing else of the walkthrough is by class.
      - scripts/sim/sim-snapshot.ps1: one list, each line with the second class it takes
        or none. The line ids and the lines that play the capital pass come from it. So
        the Chanter's line now has a capital leg in the script, as every line with a
        second class has; the bot still refuses its pass until NR-31. A snapshot of a
        line other than the accepted one records its line, as it did.
      - scripts/sim/test-sim-snapshot.ps1 follows: a Chanter's capital snapshot restores
        on the first pass; the refusal of a first pass is shown on a line with no second
        class; and a new check holds the script's list to NaturalClassLine.All, line for
        line with the same second classes.
      - **Not done here:** the nine lines of the other second classes are not added. Each
        is its class's first item (NR-x0), which settles its pick by NR-Q5, and adds the
        line to the C# list and to the script's list; the check above fails until both
        have it.
    - **Proof, the one-time check** (run/nr/NR-30/check.log; the check file is not
      committed): what the rule gives each of the eleven second classes.

      | Second class | Its ceremony list offers | No pick on the line | Dispatch quest |
      |---|---|---|---|
      | Cleric, Chanter | mace 100100495, staff 101500498 | loads with the staff | Q2904 |
      | Gladiator | sword 100000640, greatsword 100900488, polearm 101300479 | refused: no staff offered | Q2901 |
      | Templar | sword 100000640, greatsword 100900488 | refused | Q2901 |
      | Assassin, Ranger | dagger 100200605, sword 100000640, bow 101700515 | refused | Q2902 |
      | Sorcerer, Spirit Master | spellbook 100600532, orb 100500500 | refused | Q2903 |
      | Gunner | gun 101800506 | refused | Q29070 |
      | Rider | keyblade 102100489 | refused | Q29070 |
      | Bard | harp 102000523 | refused | Q29071 |

      With each offered item named on the line, the bridge takes that item with its own
      reward action, protects it and wears it at its endpoint, and the bridge and the
      capital pass both have the class's dispatch quest. A line with no second class
      keeps the reviewed capital contract.
    - **Proof.** scripts/sim/test-sim-snapshot.ps1 passes, with test_compare_traces.py and
      test-code-coverage.ps1 (run/nr/NR-30/script-tests.log). Gate, set
      all+mage+warrior+artist+engineer+scout, -Parallel 8, run guard-p8
      (run/nr/NR-30/guard-p8/verdict.json): verdict pass, all twelve scopes identical, p, b
      and l1 among them. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629
      passed, 16 skipped) and Fast passes (run nr30-fast, 11 passed).
- [x] **NR-31 - The gates read the line's second class.** Depends: NR-30
  - Work: The five refusals of Survey A1's first table (the Ishalgen finish, the Altgard
    and Abyss leg starts, the bridge endpoint, the capital pass), `returnedCleric` in the
    decision loop and the leg-scoped maps of the identity rules accept the second class of
    the run's line, and still refuse every other class by name. The NA-23 encounter stays
    the Cleric's.
  - Proof: One-time check of Classify and the capital decision for every line; the full
    gate identical.
  - 2026-10-09: done. Every gate of Survey A1's first table but the NA-23 encounter now
    asks for the second class of the run's line. The accepted line plays as recorded.
    - **Java.** No server behavior is relied on beyond what Survey A1 read: all eleven
      classes can be chosen at Q2008, and no map of the route has a class limit.
    - **The change.**
      - Sc/Classes/NaturalClassLine.cs, SecondName: the second class as a refusal names it
        ("Cleric", "Spirit Master"); a line that takes none is asked for "the second class".
      - Sc/NaturalJourneyIdentityRules.cs, Classify: the Convent and the l11, l12 and ax
        maps are open to the second class of the line it is given, each with its leg and
        level as before. A call that names no line still asks for the Cleric.
      - Sc/NaturalIshalgenDecisionLoop.cs, Decide: takes the line. `returnedCleric` is
        `returnedSecond`: the line's second class, at level 10 or above with Q2008 and
        Q2009 complete. Any other class stops with "returned-class", by name.
        Sc/NaturalJourneyCheckpoint.cs, Capture, passes the line on, because the Ishalgen
        loop takes its next decision from the checkpoint.
      - Sc/NaturalCapitalDecisionEngine.cs, Decide: takes the line; the pass is its second
        class's, and the refusal over the dispatch quest names the contract's quest.
      - J: the Ishalgen finish, the Altgard leg start and the Abyss leg start ask
        IsLineSecondClass; the bridge endpoint classifies with the run's line; all 24 calls
        of the Ishalgen decision and the checkpoint pass the line; the capital pass passes
        it. The NA-23 encounter stays the Cleric's.
    - **What is still the Cleric's past these gates** is the rest of phase C: reward pins
      (NR-32), protected items (NR-33), the help kit (NR-34), the shop (NR-35), the shot in
      flight (NR-36), the patrol rule (NR-37), coin gear (NR-38), the destiny and Haramel
      data (NR-39, NR-40) and the receipts (NR-41). A line of another class passes the
      gates now and stops at the first of those it meets.
    - **Proof, the one-time check** (run/nr/NR-31/check.log; the check file is not
      committed): the seven lines a run can name, and a line made for each of the nine
      second classes that have none yet.
      - **Classify.** Each line's second class is accepted in Altgard, in the Convent, in
        the Space of Destiny with leg l11 at level 20, in Haramel with leg l12 at level 16,
        and in Morheim and the arena with leg ax at level 25. The same maps without the
        leg, or one level below, are refused. Each of the ten other second classes is
        refused in all six places (60 refusals a line). A line with no second class has no
        second-class state at all.
      - **The return to Ishalgen.** The line's second class goes on with the Ishalgen
        quests and ends with journey-complete; another class is stopped, for example "The
        Ishalgen return after the ceremony needs the Spirit Master; the character is
        GLADIATOR."
      - **The capital pass.** With the line's class and its dispatch quest at START/0 the
        first decision is the same for all eleven: talk to npc 204079 for Q2911. The
        dispatch quest is Q2901, Q2902, Q2903, Q2904, Q29070 or Q29071 by class. Another
        class is blocked by name, and so is another class's dispatch quest in the journal.
      - **Without a line** both engines ask for the Cleric, as before.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout, -Parallel 8, run
      guard-p8 (run/nr/NR-31/guard-p8/verdict.json): verdict pass, all twelve scopes
      identical. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629 passed,
      16 skipped) and Fast passes (run nr31-fast, 11 passed).
- [x] **NR-32 - Reward picks by the class's gear rule.** Depends: NR-31
  - Work: Java first: the reward lists of the route's quests, and how a per-class list is
    chosen (QuestTemplate, as read for CP-31). A leg's reward pin is the Cleric's; for
    another class the pick at the same quest is what its gear rule scores highest in the
    list the server offers it. The four per-class lists (Q2009, Q2900, Q2947, Q28505) are
    read for the class. Write the table of picks for every class and quest into this
    document.
  - Proof: One-time check that prints the table; for the Cleric the rule gives every pin;
    the full gate identical.
  - 2026-10-09: done on the second attempt, with one part of the Proof line not met: the
    rule does not give the Cleric every pin (nine of fourteen by its own table, twelve by
    the healer's bonus order; NR-Q12). The Cleric keeps its pins, so the accepted line
    plays as recorded.
    - **Java.** QuestService.getRewardItems 164-183: the chosen index is read from the
      class's list when the quest has use_class_reward (1 on every repeat, 2 on the last;
      QuestTemplate 285-291), from getSelectableRewardByClass 212-238, which has a list
      for each second class and none for a starter. Otherwise it is read from the general
      list of the reward group. getRewardIndex 216-218: SELECTED_QUEST_REWARD1 to 15 are
      indexes 0 to 14, so a list of thirteen weapons can be chosen from. The port has the
      same lines (Services/QuestService.cs 160-227). No server change.
    - **What the data says.** Of the 181 quests in the route's contracts and plans, two
      have use_class_reward: Q2009 and Q2947. Q2900 and Q28505 have class lists and no
      use_class_reward, so the server never offers them; Survey A1 is corrected. Whether
      retail offered Q28505's class weapons is a retail question that stops no class; it
      is not followed here.
    - **The change.**
      - Sc/NaturalIshalgenInventoryPolicy.cs, Load: the list a quest offers is the class's
        own list when the quest has use_class_reward, by the second class of the policy's
        line, and the general list otherwise. The ceremony was the one quest read that
        way. RewardList gives a quest's list. RewardChoiceFor gives a class's pick at a
        pinned quest: what ChooseReward takes there.
      - ChooseReward has one more key, after the score and the consumables and before the
        sale price: the class's bonus order (NR-Q12). NaturalItem carries the template's
        flat bonus lines for it. Sc/Classes/NaturalClassGearTable.cs has BonusOrder and the
        three orders; a table that names none takes the one of its weapon stat, the
        Priest and the Chanter name the healer's, and the Cleric names an empty one.
        Sc/Classes/NaturalGearRules.cs compares two pieces by it.
      - Sc/NaturalAltgardContract.cs, WithRewardChoices: the same leg with other picks, in
        the list and in the step that sends each.
      - J: when a leg is taken up, a character whose class is not the contract's
        start.class gets its own picks, chosen by its gear rules from what it then owns;
        the picks are traced as leg-reward-picks. The Cleric's leg is the contract itself.
        The template hand-in loads the policy for the run's line.
    - **First attempt.** The Cleric's table had the healer's order. Gate run guard-p8
      (run/nr/NR-32/guard-p8/verdict.json): eleven scopes identical, scope c different,
      first at record 18626: the claim of Q2227 sent choice 2 where the baseline sends
      choice 1. Q2227 is a template hand-in with two rings; the recorded Cleric takes the
      first. The one change (rule (i)): the Cleric's table names no order. The first
      check's output is run/nr/NR-32/check-a1.log.
    - **Not done here.** What a leg protects, keeps or checks by a pinned item's id is
      NR-33; the coin-gear and Abyss tiers are NR-38. The nine second classes without a
      gear table were checked with provisional tables by NR-Q5 and NR-Q7 (in the check
      file, not committed); each class's first item writes its own table, and its picks
      follow from it.
    - **Proof, the one-time check** (run/nr/NR-32/check.log; the check file is not
      committed): every second class's pick at the ceremony and at the fourteen pinned
      quests of the legs, with nothing better owned. The Cleric's are its pins.

      | Quest | Leg | Pick by class: choice number, item, group |
      |---|---|---|
      | Q2009 | bridge | Cleric, Chanter: 2, 101500498 STAFF; Gladiator: 2, 100900488 GREATSWORD; Templar: 1, 100000640 SWORD; Assassin: 1, 100200605 DAGGER; Ranger: 3, 101700515 BOW; Sorcerer, Spirit Master: 1, 100600532 SPELLBOOK; Gunner: 1, 101800506 GUN; Rider: 1, 102100489 KEYBLADE; Bard: 1, 102000523 HARP |
      | Q24011 | l1 | Cleric, Chanter: 4, 114501726 CH_SHOES; Gladiator, Templar: 6, 114601575 PL_SHOES; Assassin, Ranger: 2, 114301817 LT_SHOES; Sorcerer, Spirit Master, Bard: 1, 114101696 RB_SHOES; Gunner: 3, 114301819 LT_SHOES; Rider: 5, 114501728 CH_SHOES |
      | Q24012 | l2 | Cleric, Chanter: 4, 110551139 CH_TORSO; Gladiator, Templar: 6, 110601622 PL_TORSO; Assassin, Ranger: 2, 110301811 LT_TORSO; Sorcerer, Spirit Master, Bard: 1, 110101836 RB_TORSO; Gunner: 3, 110301813 LT_TORSO; Rider: 5, 110551141 CH_TORSO |
      | Q24013 | l4 | Cleric, Chanter: 8, 101501355 STAFF; Gladiator: 6, 100901373 GREATSWORD; Templar: 1, 100001735 SWORD; Assassin: 3, 100201501 DAGGER; Ranger: 9, 101701366 BOW; Sorcerer, Spirit Master: 5, 100601429 SPELLBOOK; Gunner: 10, 101801216 GUN; Rider: 13, 102101070 KEYBLADE; Bard: 12, 102001244 HARP |
      | Q2288 | l4 | Cleric, Chanter, Sorcerer, Spirit Master, Gunner, Rider, Bard: 2, 122001285 RING; Gladiator, Templar, Assassin, Ranger: 1, 122001284 RING |
      | Q2223 | l4 | Cleric, Chanter, Sorcerer, Spirit Master, Gunner, Rider, Bard: 2, 120001132 EARRING; Gladiator, Templar, Assassin, Ranger: 1, 120001131 EARRING |
      | Q2292 | l5 | Cleric, Chanter, Sorcerer, Spirit Master, Gunner, Rider, Bard: 2, 120001521 EARRING; Gladiator, Templar, Assassin, Ranger: 1, 120001520 EARRING |
      | Q24014 | l10 | Cleric, Gladiator, Templar, Assassin, Ranger: 1, 121001394 NECKLACE; Chanter, Sorcerer, Spirit Master, Gunner, Rider, Bard: 2, 121001395 NECKLACE |
      | Q24015 | l10 | Cleric: 3, 125004139 HEAD; Chanter, Sorcerer, Spirit Master, Gunner, Rider, Bard: 1, 125004137 HEAD; Gladiator, Templar, Assassin, Ranger: 2, 125004138 HEAD |
      | Q24016 | l10 | Cleric, Chanter: 8, 101501357 STAFF; Gladiator: 6, 100901375 GREATSWORD; Templar: 1, 100001737 SWORD; Assassin: 3, 100201503 DAGGER; Ranger: 9, 101701368 BOW; Sorcerer, Spirit Master: 5, 100601431 SPELLBOOK; Gunner: 10, 101801218 GUN; Rider: 13, 102101072 KEYBLADE; Bard: 12, 102001246 HARP |
      | Q28500 | l12 | Cleric, Chanter, Rider: 4, 112501641 CH_SHOULDER; Gladiator, Templar: 6, 112601569 PL_SHOULDER; Assassin, Ranger: 2, 112301692 LT_SHOULDER; Sorcerer, Spirit Master, Bard: 1, 112101602 RB_SHOULDER; Gunner: 3, 112301694 LT_SHOULDER |
      | Q28505 | l12 | Cleric, Chanter, Rider: 4, 113501720 CH_PANTS; Gladiator, Templar: 6, 113601570 PL_PANTS; Assassin, Ranger: 2, 113301784 LT_PANTS; Sorcerer, Spirit Master, Bard: 1, 113101664 RB_PANTS; Gunner: 3, 113301786 LT_PANTS |
      | Q28507 | l12 | Cleric, Chanter, Sorcerer, Spirit Master, Gunner, Rider, Bard: 2, 123001440 BELT; Gladiator, Templar, Assassin, Ranger: 1, 123001439 BELT |
      | Q24020 | ax | Cleric, Chanter: 4, 110551147 CH_TORSO; Gladiator, Templar: 6, 110601626 PL_TORSO; Assassin, Ranger: 2, 110301819 LT_TORSO; Sorcerer, Spirit Master, Bard: 1, 110101840 RB_TORSO; Gunner: 3, 110301821 LT_TORSO; Rider: 5, 110551149 CH_TORSO |
      | Q2947 | ax | Cleric, Chanter: 2, 101501224 STAFF; Gladiator: 4, 100901214 GREATSWORD; Templar: 1, 100001562 SWORD; Assassin: 1, 100201365 DAGGER; Ranger: 3, 101701246 BOW; Sorcerer, Spirit Master: 1, 100601285 SPELLBOOK; Gunner: 1, 101801035 GUN; Rider: 1, 102100833 KEYBLADE; Bard: 1, 102001058 HARP |

      Q2009's pick is the line's own (NR-30); the score alone ranks the same item first
      for every class. The Gunner's table holds the pistol only: the class-line contract
      gives it no cannon mastery. For each class every leg was rewritten: each pick stands
      in the list and in the one step that sent the pin, and no other step changes.
    - **The Cleric and the rule.** By its own table, with no bonus order, the rule gives
      nine of fourteen pins: not the ring of Q2288, the earrings of Q2223 and Q2292, the
      helm of Q24015 or the belt of Q28507, where it takes the first in the list. By the
      healer's order it gives twelve: not the necklace of Q24014 or the helm of Q24015.
      No one order gives both the +28 MP ring of Q2288 and the +70 HP necklace of Q24014,
      and the four hats of Q24015 are one item group.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout, -Parallel 8, run
      guard-p8-a2 (run/nr/NR-32/guard-p8-a2/verdict.json): verdict pass, all twelve scopes
      identical, so the bonus order changes no recorded pick of the Priest or the five
      starters. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629 passed,
      16 skipped) and Fast passes (run nr32-fast-a2, 11 passed).
- [x] **NR-33 - Protected and kept items by rule.** Depends: NR-32
  - Work: What a leg protects, keeps or cleans up by item id (the bridge, coin gear,
    Haramel, the Abyss entry) is derived for another class from its gear rules and from
    what it wears at the leg's start; the Cleric's lists stay as they are. The inventory
    policy takes the observed class's rules everywhere it took `cleric`.
  - Proof: One-time check; the full gate identical.
  - 2026-10-09: done. A leg's protected items are its contract's class's; another class
    gets its own when the leg is taken up. The accepted line plays as recorded.
    - **Java.** None read: what the bot keeps in its bag is no server behavior.
    - **What the lists hold.** The check sorted every protected id of the three scopes
      (run/nr/NR-33/check.log):

      | Scope | Every class carries | The leg's own reward pins | The Cleric's gear |
      |---|---|---|---|
      | Coin gear (cg), 19 ids | power shard 169000004, the two coins, the Stigma Support Bundle 188053787 | none | 15: the staff 101501357, seven armor pieces, two earrings, two rings, a necklace, a belt, the helm |
      | Haramel (l12), 17 ids | the two coins, the bundle | none in the list | 14: the same without the robe leggings |
      | Abyss entry (ax), 7 ids | scroll 164000079, the two coins, the bundle | 101501224 and 110551147 | 1: the staff 101501357 |

      The Haramel clean-up list is eleven quest items and the Abyss entry's open, discard
      and keep-sealed lists are bundles and a manastone; they are the same for every class
      and are not changed. The bridge's protected list holds one piece of gear, the
      ceremony pick, which ForChoice already swaps for the line's.
    - **The rule, for a class other than the contract's** (NaturalAltgardContract
      .ProtectedFor): an id that is a reward pin of the leg becomes the class's pick at
      the same quest (NR-32); any other gear in the list is dropped; what the character
      wears when the leg is taken up is added; everything else stays. Gear is a weapon, a
      shield, armor, a hat or an accessory, by the slots its item group may take
      (NaturalItem.IsEquipment); a power shard, a stigma stone and wings are not.
    - **The change.**
      - Sc/NaturalAltgardContract.cs: ProtectedFor and WithProtectedItems, which gives the
        coin-gear, Haramel and Abyss-entry scopes their lists for another class.
      - J: the step that gives another class its reward picks (NR-32) gives it these lists
        too, and traces them as leg-protected-items. The Cleric's leg is the contract
        itself, after BindIncoming as before.
      - J, the bridge: the three kept accessories are the reviewed pair's by item id.
        Another pair keeps the accessories it wears when the bridge is taken up
        (bridge-kept-accessories), and its endpoint asks that they are still worn.
        (Replaced by NR-35: the endpoint asks the gear rule.)
      - Sc/NaturalIshalgenInventoryPolicy.cs: NaturalItem.IsEquipment. Nothing else: the
        policy's decision has taken the observed class's rules since CP-23 (GearRules), and
        its `cleric` flag and IsCleric are called by unit tests alone.
    - **Left to later items, by their own text.** The coin-gear purchases and body slots,
      and the retained weapon's identity in the coin-gear and Haramel scopes, are NR-38
      and NR-40; BindIncoming still asks for the Cleric's staff, and Haramel's
      RequiredIncomingItemIds are still the Cleric's. The bridge's supplies are NR-34 and
      NR-35. So another class passes this rule and stops at those.
    - **Proof, the one-time check** (the check file is not committed): for each of the
      eleven second classes and each scope, the list with nothing worn is what every class
      carries plus the class's picks, for example the Gladiator at the Abyss entry:
      164000079, the two coins, the bundle, and its picks 100901214 and 110601626. With the
      Cleric's gear given as worn, the rule gives the contract's own list back for every
      class, pick for pin. A leg with none of the three scopes is returned as it is.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout, -Parallel 8, run
      guard-p8 (run/nr/NR-33/guard-p8/verdict.json): verdict pass, all twelve scopes
      identical. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629 passed,
      16 skipped) and Fast passes (run nr33-fast, 11 passed).
- [x] **NR-34 - The help kit by class from level 10.** Depends: NR-31
  - Work: NR-Q8. The allowlist's rows get a kind: for every class, for a class that casts
    from mana, for a class with a reagent skill. The profile says which kinds its class
    takes. The Cleric's supply is what it was. Write each second class's kit into this
    document as a manifest.
  - Proof: One-time check that prints each class's kit at levels 10, 20 and 25; the full
    gate identical, with help-items.json of the Cleric's scopes unchanged.
  - 2026-10-09: done. The allowlist's rows have a kind and a profile takes its kinds. The
    Cleric's kit is every row, as it was.
    - **Java and data.** No server behavior is changed or relied on beyond the data:
      skill_tree.xml gives every one of the eleven second classes Herb Treatment 246 and
      MP Recovery 249 at level 10, and skill_templates.xml has both use Lesser Odella
      Powder 169300003. So every second class has a reagent skill; the Cleric and the
      Chanter are not alone in it.
    - **The change.**
      - Sc/NaturalHelpItemAllowlist.cs: NaturalHelpKind, and a kind on each row. Kit(caster,
        reagent) is the level 1-9 kit and the rows from level 10 on that are for every
        class or of a kind the class takes.
      - Sc/Classes/NaturalClassProfile.cs: NaturalHelpItemRules.ForKinds, by which a
        profile says its kinds. The Cleric's and the Chanter's profiles take both, which
        is every row in the allowlist's order.
      - No id is added to the allowlist, and the supply's refusal of an unapproved id is
        as it was.
    - **The manifest (NR-Q8).** Counts are "top up to N when fewer than M are owned".

      | Kind | Rows from level 10 |
      |---|---|
      | Every class | Anti-Shock scroll 164000067 at 10-19, 164000068 at 20-29, 164000069 at 30-39: 30 below 8. Life Potion 162000002 at 10-19, 162000003 at 20-29, 162000004 at 30-39: 30 below 10. Running scroll 164000075 at 20-29, 164000076 at 30-39: 20 below 5. Zeller Aether Jelly 160002273 at 10-39: 8 below 2. |
      | A class that casts from mana | Mana Serum 162000017 at 10-19, 162000018 at 20-29, 162000019 at 30-39: 40 below 10. Awakening scroll 164000133 at 20-29, 164000134 at 30-39: 60 below 15. |
      | A class with a reagent skill | Odella powder 169300003 at 10-24, 169300004 at 25-39: 200 below 50. |

      Every kit starts with the level 1-9 rows of CP-Q12. By default (NR-Q13) Cleric,
      Chanter, Sorcerer, Spirit Master and Bard take all three kinds, and Gladiator,
      Templar, Assassin, Ranger, Gunner and Rider take the first and the third. A class's
      first item writes the kinds into its profile.
    - **Proof, the one-time check** (run/nr/NR-34/check.log; the check file is not
      committed): each class's kit at levels 10, 20 and 25, every supply passed by the
      allowlist's own refusal.

      | Classes | Level | Supplied to a character that has none of it |
      |---|---|---|
      | Cleric, Chanter, Sorcerer, Spirit Master, Bard | 10 | 30 x 164000067 anti-shock, 30 x 162000002 life-potion, 40 x 162000017 mana-serum, 8 x 160002273 dp-jelly, 200 x 169300003 powder |
      | Cleric, Chanter, Sorcerer, Spirit Master, Bard | 20 | 60 x 164000133 awakening, 20 x 164000075 running, 30 x 164000068 anti-shock, 30 x 162000003 life-potion, 40 x 162000018 mana-serum, 8 x 160002273 dp-jelly, 200 x 169300003 powder |
      | Cleric, Chanter, Sorcerer, Spirit Master, Bard | 25 | 60 x 164000133 awakening, 20 x 164000075 running, 30 x 164000068 anti-shock, 30 x 162000003 life-potion, 40 x 162000018 mana-serum, 8 x 160002273 dp-jelly, 200 x 169300004 powder |
      | Gladiator, Templar, Assassin, Ranger, Gunner, Rider | 10 | 30 x 164000067 anti-shock, 30 x 162000002 life-potion, 8 x 160002273 dp-jelly, 200 x 169300003 powder |
      | Gladiator, Templar, Assassin, Ranger, Gunner, Rider | 20 | 20 x 164000075 running, 30 x 164000068 anti-shock, 30 x 162000003 life-potion, 8 x 160002273 dp-jelly, 200 x 169300003 powder |
      | Gladiator, Templar, Assassin, Ranger, Gunner, Rider | 25 | 20 x 164000075 running, 30 x 164000068 anti-shock, 30 x 162000003 life-potion, 8 x 160002273 dp-jelly, 200 x 169300004 powder |

      A kit with both kinds is the allowlist's nineteen rows, row for row; one without the
      caster's kind has fourteen and no mana serum or Awakening scroll at any level.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout, -Parallel 8, run
      guard-p8 (run/nr/NR-34/guard-p8/verdict.json): verdict pass, all twelve scopes
      identical. help-items.json of each scope is byte for byte the one of NR-33's gate
      (run/nr/NR-34/help-items.log): p, m, b, c, hm, ax and the five starters; l1 writes
      none in either. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629
      passed, 16 skipped) and Fast passes (run nr34-fast, 11 passed).
- [x] **NR-35 - The bridge's shop and stops by class.** Depends: NR-33, NR-34
  - Work: The Altgard shop stop buys the powder only for a class with a reagent skill and
    the potions by the class's restock rule; the kept accessories come from the gear rule.
  - Proof: One-time check; gate b identical.
  - 2026-10-09: done. The shop stop's two purchases and the three kept accessories are the
    reviewed pair's, the Priest who becomes a Cleric; another pair has them by rule. The
    accepted line plays as recorded.
    - **Java.** None read: what the bot buys and wears is no server behavior. The vendor
      still decides every trade.
    - **The change.**
      - Sc/NaturalAscensionContract.cs: ReviewedPair, and PurchasesFor, the purchases of
        another pair. Of the reviewed two, the powder 169300003 is a help item of the
        allowlist and is bought, up to the reviewed 30, only by a class whose kit has it
        (NR-34). The Lesser Life Elixir 162000053 is a potion the class's restock line
        counts, so it is bought by that line: when all its life potions together are at or
        below the line's threshold, up to the line's target, as far as its spendable Kinah
        goes.
      - J, the shop stop: another pair's purchases are those, traced as
        altgard-shop-purchases. A purchase the vendor's price puts out of reach is traced
        as altgard-shop-shortfall and does not stop the run; for the reviewed pair it still
        does.
      - J, the endpoint: another pair names no kept accessory by id. Its endpoint asks the
        gear rule: nothing in the bag that the equipment check would still put in an
        accessory slot. This replaces NR-33's list of what was worn when the bridge was
        taken up, which goes stale as soon as the shop stop's equipment check wears a
        better ring.
    - **Proof, the one-time check** (run/nr/NR-35/check.log; the check file is not
      committed), for the eleven second classes with the kinds of NR-34 and their starter
      line's restock rule (buy at five or fewer life potions, up to twelve; a Kinah floor
      of 500 for every line but the Priest's):

      | The bag at the shop | What is bought |
      |---|---|
      | The level-10 kit as supplied: 30 life potions, 200 powder | Nothing by any class: the potions are above the threshold and the powder above its 30. |
      | Three life potions, no powder, 20,000 Kinah | Nine elixirs (to twelve potions in all) and 30 powder, by every class. |
      | Three life potions, no powder, 600 Kinah | The Priest line's classes, with no floor, one elixir. The others no elixir: 100 Kinah above their floor does not pay for one at 450. The powder is asked for as reviewed. |
      | A class that does not take the reagent kind | No powder. |

      With the help kit on, as every run has it, another class buys nothing at this stop.
      The gear rule, as the endpoint asks it: with a level-14 ring in the bag beside two
      worn level-7 rings, one accessory is left to put on; once it is worn, none.
    - **Not covered by a rule.** The powder purchase is not held back by the restock
      rule's Kinah floor; the vendor refuses what the Kinah does not cover and the
      shortfall is traced. The Tea of Repose is drunk by every class that owns one.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout, -Parallel 8, run
      guard-p8 (run/nr/NR-35/guard-p8/verdict.json): verdict pass, all twelve scopes
      identical, b among them. Seven pre-commit checks pass, Aion.GameServer.Tests passes
      (4,629 passed, 16 skipped) and Fast passes (run nr35-fast, 11 passed).
- [x] **NR-36 - A shot in flight by role.** Depends: NR-31
  - Work: NaturalAirCombat takes its skill from a role the profile names for a ranged
    attack that may be cast in flight, with that skill's range; a class with no such skill
    flies into its weapon's reach and swings. Java first: which skills may be used while
    flying (the skill templates' flight conditions).
  - Proof: One-time check that prints each starter's and second class's air attack; gate
    l1 and c identical.
  - 2026-10-09: done. The air fight takes its attack from the class's profile; the Cleric's
    is the Smite it has always cast, from the same 15 m.
    - **Java.** A skill's flight conditions are start conditions of its template:
      NoFlyingCondition (the caster must not fly), SelfFlyingCondition with restriction
      GROUND or FLY, TargetFlyingCondition for the target (skillengine/condition; 156
      skills carry one among their start conditions). A weapon swing has none:
      PlayerController.attackTarget 399-436 asks for range, the weapon's range plus one
      metre, and for sight. The port has the same conditions
      (SkillEngine/Condition). No server change.
    - **The change.**
      - Sc/Classes/NaturalSkillCatalog.cs and the skill row: GroundOnly and TargetFlight,
        read from the template with the other conditions.
      - Sc/Classes/NaturalClassProfile.cs: AirAttackRoles, the roles a class shoots with
        in flight, in order. The Priest, the Cleric and the Chanter name Smite. The Mage,
        the Artist and the Engineer name their rotation's roles for a target at range. The
        Warrior and the Scout name none.
      - Sc/NaturalAirCombat.cs: AttackFor gives the air attack: of the named roles, the
        first whose best learned skill is not ground-only, asks nothing of the target's
        flight and needs no earlier chain step; failing that, the held weapon's swing at
        the weapon's own speed. NaturalAirAttack gives the hover distance: ten metres
        inside the reach, no nearer than three metres, and half a metre inside a reach
        shorter than that. The fight, its flight-time rule and its hover search take that
        distance; the search still tries two thirds and four thirds of it. A caller that
        names no attack gets the Cleric's Smite and 15 m, as the probes always had.
      - J, the air kills of leg 1: the attack is the observed class's.
    - **Proof, the one-time check** (run/nr/NR-36/check.log; the check file is not
      committed), with the weapon each class holds as Q24013 hands it out:

      | Profile | Roles named | Air attack |
      |---|---|---|
      | Priest | smite | Smite 4013 at level 9, reach 25 m, hover 15 m |
      | Cleric | smite | Smite 4013 at level 10, 4014 at 11, 4015 at 16, 4016 at 25; reach 25 m, hover 15 m |
      | Chanter | smite | Smite 4013, the rank it learned as a Priest; reach 25 m, hover 15 m |
      | Mage | ice, shock, bolt, blaze | 1363 (ice), reach 25 m, hover 15 m. Shock and blaze are later chain steps and are passed over. |
      | Artist | ice, pulse | 4222 (ice), reach 25 m, hover 15 m |
      | Engineer | hot, gunshot, rapid, direct | 1942 (hot), reach 20 m with the pistol's range, hover 10 m |
      | Warrior | none | swings the sword: reach 1.5 m, every 1.4 s, hover 1 m |
      | Scout | none | swings the dagger: reach 1.5 m, every 1.2 s, hover 1 m |

      The nine second classes without a profile have none to ask; each one's first item
      names its roles. What the data gives them by level 12: the only skill any of the
      eleven is refused in flight is 243, and none asks for a flying target. Targeted
      skills that reach 10 m or more and may be cast in flight: Gladiator and Templar 2981
      (15 m, from level 10), Assassin 3455 (20 m), Rider 2807 (20 m); Ranger, Sorcerer,
      Spirit Master, Gunner and Bard have several from 20 to 26 m.
    - **Not proven here.** No class but the Cleric has fought in the air. The swing in
      flight, and a hover at one metre from a fungus, are first played when a Warrior or a
      Scout line reaches leg 1.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout, -Parallel 8, run
      guard-p8 (run/nr/NR-36/guard-p8/verdict.json): verdict pass, all twelve scopes
      identical, l1 and c among them. Seven pre-commit checks pass, Aion.GameServer.Tests
      passes (4,629 passed, 16 skipped) and Fast passes (run nr36-fast, 11 passed).
- [x] **NR-37 - The patrol rule and the blocked-pull view by profile.** Depends: NR-31
  - Work: NaturalPatrolPolicy applies to every second class with the profile's swarm
    limit, and the blocked-pull view asks the profile for its recovery roles.
  - Proof: The full gate identical.
  - 2026-10-09: done. The patrol rule reads the class's own fight table. The Cleric's
    answers are word for word what they were.
    - **Java.** None read: when the bot waits for a patrol is no server behavior.
    - **The change.**
      - Sc/NaturalPatrolPolicy.cs: NaturalPatrolView, what the rule asks a class about. From
        a fight table it takes the heal (the table's reserve role, or else the last ladder
        skill paid with mana that is not for emergencies only), the heal kept up under
        attack, the first ladder skill paid with DP, and the pull limit, one fewer than
        the attackers the table leaves at. The assessment uses the view's limit and names,
        and asks nothing about a recovery the class does not have. With no view given it
        is the Cleric's, in NA-22's words.
      - Sc/Classes/NaturalClassProfile.cs: every profile has its Patrol view, made from
        its table. The Cleric's keeps the recorded wording ("Healing Light", "the heal over
        time or Salvation").
      - J, the blocked-pull view: the three recovery lookups ask the view's roles in place
        of "heal", "rejuvenation" and "salvation".
      - The rule's on-switch was already the profile's (PatrolRule). The Chanter's profile
        now holds and assesses, as the Cleric's does. A class's first item writes
        HoldAndAssess into its second class's profile; the starters keep the run's short
        waits.
    - **What each profile's view is** (a one-time check, run/nr/NR-37/check.log; the check
      file is not committed):

      | Profile | Rule | Heal | Under attack | Paid with DP | Pull limit |
      |---|---|---|---|---|---|
      | Cleric | hold and assess | heal | rejuvenation | salvation | 2 |
      | Chanter | hold and assess | heal | none | none | 2 |
      | Priest | baseline | heal | none | none | 2 |
      | Warrior | baseline | none | none | none | 2 |
      | Scout | baseline | evasion | none | none | 2 |
      | Mage | baseline | none | none | none | 1 |
      | Engineer | baseline | resist | none | none | 1 |
      | Artist | baseline | heal | none | none | 1 |

      The Scout's and the Engineer's "heal" is the one mana-paid skill of their recovery
      ladder, a defence and not a heal; it matters only once such a line holds and
      assesses, and its second class's first item names its own. The Cleric's view equals
      the one the rule had built in, and its assessment of a blocked pull of three reads
      as before, line for line.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout, -Parallel 8, run
      guard-p8 (run/nr/NR-37/guard-p8/verdict.json): verdict pass, all twelve scopes
      identical. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629 passed,
      16 skipped) and Fast passes (run nr37-fast, 11 passed).
- [x] **NR-38 - Coin gear by class.** Depends: NR-32, NR-33
  - Work: Java first: the iron-coin and bronze-coin vendors' goods lists by armor type and
    weapon. The coin-gear leg and the Abyss entry's two tiers buy the pieces of the
    class's armor type and the weapon of its group that beat what is worn by the class's
    own stat; counts and the held weapon's identity are observed, not pinned. The Cleric's
    manifests are what they were.
  - Proof: One-time check that prints each class's manifest at each tier; gate hm and ax
    identical.
  - 2026-10-09: done as the manifest rule, and split by rule (q): the two legs that shop
    are NR-38a and NR-38b below. No leg reads the rule yet, so every class plays as before.
    - **Java.** A coin vendor is a reward shop. TradeService.performBuyFromShop 62-75 sends
      the REWARD type to performBuyTransaction 80-163 without Kinah; validateBuyItems
      165-180 asks only that the item is in one of the vendor's trade tabs' goods lists.
      The price is the item template's acquisition: the coin and the count. Nothing asks
      for the buyer's class or level; wearing the piece does. The port has the same
      (Services/TradeService.cs). No server change.
    - **What the data holds.** Each coin tier of the route has two vendors side by side,
      and the Cleric's contracts name one of each pair:

      | Tier | The contract's vendor | It sells | The vendor beside it | It sells |
      |---|---|---|---|---|
      | Iron Coin 186000006, level 16, Altgard | Lohaban 203689 | chain and robe armor; harp, mace, orb, spellbook, staff; a shield | Lateni 203659, 4 m away | plate, leather and chain armor; bow, dagger, greatsword, gun, keyblade, mace, polearm, sword; a shield |
      | Bronze Coin 186000007, levels 21 and 26, Morheim | Vebna 204425 | the same kinds | Nott 204360, 14 m away | the same kinds, and a cannon at 26 |

      So a class that wears plate or leather, or holds a sword, a bow or a gun, cannot buy
      from the Cleric's vendor at all. Level 26 has two ranks of every piece; the Elite
      rank is LEGEND quality and costs about two and a half times the other.
    - **The change.** Sc/NaturalCoinManifest.cs, new: NaturalCoinManifests reads the
      shipped data. VendorsBeside gives the contract's vendor and every other reward shop
      for the same coin within 30 m of it. At gives what one vendor sells a class at a
      tier: for each of the five body slots and for the weapon, the piece its gear rules
      rank highest among those it may wear, the better quality and then the dearer of two
      they rank alike, and a shield for a class that holds one. For gives the class's
      vendor: the one whose weapon, then whose armor, its gear rules rank highest.
    - **Proof, the one-time check** (run/nr/NR-38/check.log; the check file is not
      committed), with the provisional tables of NR-32 for the nine classes without one:

      | Tier | Classes | Vendor | Armor: item, group, coins | Weapon | Shield | All |
      |---|---|---|---|---|---|---|
      | iron, level 16 (coin gear leg) | Cleric, Chanter | 203689 | 110501096 CH_TORSO for 2, 111501065 CH_GLOVE for 1, 112501015 CH_SHOULDER for 1, 113501074 CH_PANTS for 2, 114501081 CH_SHOES for 1 | 101500810 STAFF for 3 | none | 10 coins |
      | iron, level 16 (coin gear leg) | Gladiator | 203659 | 110601078 PL_TORSO for 2, 111601055 PL_GLOVE for 1, 112601029 PL_SHOULDER for 1, 113601039 PL_PANTS for 2, 114601035 PL_SHOES for 1 | 100900782 GREATSWORD for 3 | none | 10 coins |
      | iron, level 16 (coin gear leg) | Templar | 203659 | 110601078 PL_TORSO for 2, 111601055 PL_GLOVE for 1, 112601029 PL_SHOULDER for 1, 113601039 PL_PANTS for 2, 114601035 PL_SHOES for 1 | 100001040 SWORD for 3 | 115001074 for 2 | 12 coins |
      | iron, level 16 (coin gear leg) | Assassin | 203659 | 110301126 LT_TORSO for 2, 111301080 LT_GLOVE for 1, 112301026 LT_SHOULDER for 1, 113301098 LT_PANTS for 2, 114301132 LT_SHOES for 1 | 100200920 DAGGER for 3 | none | 10 coins |
      | iron, level 16 (coin gear leg) | Ranger | 203659 | 110301126 LT_TORSO for 2, 111301080 LT_GLOVE for 1, 112301026 LT_SHOULDER for 1, 113301098 LT_PANTS for 2, 114301132 LT_SHOES for 1 | 101700827 BOW for 3 | none | 10 coins |
      | iron, level 16 (coin gear leg) | Sorcerer, Spirit Master | 203689 | 110101191 RB_TORSO for 2, 111101081 RB_GLOVE for 1, 112101040 RB_SHOULDER for 1, 113101095 RB_PANTS for 2, 114101122 RB_SHOES for 1 | 100600860 SPELLBOOK for 3 | none | 10 coins |
      | iron, level 16 (coin gear leg) | Gunner | 203659 | 110301126 LT_TORSO for 2, 111301080 LT_GLOVE for 1, 112301026 LT_SHOULDER for 1, 113301098 LT_PANTS for 2, 114301132 LT_SHOES for 1 | 101800684 GUN for 3 | none | 10 coins |
      | iron, level 16 (coin gear leg) | Rider | 203659 | 110551016 CH_TORSO for 2, 111501574 CH_GLOVE for 1, 112501516 CH_SHOULDER for 1, 113501592 CH_PANTS for 2, 114501601 CH_SHOES for 1 | 102100606 KEYBLADE for 3 | none | 10 coins |
      | iron, level 16 (coin gear leg) | Bard | 203689 | 110101191 RB_TORSO for 2, 111101081 RB_GLOVE for 1, 112101040 RB_SHOULDER for 1, 113101095 RB_PANTS for 2, 114101122 RB_SHOES for 1 | 102000721 HARP for 3 | none | 10 coins |
      | bronze, level 21 (Abyss entry) | Cleric, Chanter | 204425 | 110501097 CH_TORSO for 3, 111501066 CH_GLOVE for 2, 112501016 CH_SHOULDER for 2, 113501075 CH_PANTS for 2, 114501082 CH_SHOES for 2 | 101500811 STAFF for 4 | none | 15 coins |
      | bronze, level 21 (Abyss entry) | Gladiator | 204360 | 110601079 PL_TORSO for 3, 111601056 PL_GLOVE for 2, 112601030 PL_SHOULDER for 2, 113601040 PL_PANTS for 2, 114601036 PL_SHOES for 2 | 100900783 GREATSWORD for 4 | none | 15 coins |
      | bronze, level 21 (Abyss entry) | Templar | 204360 | 110601079 PL_TORSO for 3, 111601056 PL_GLOVE for 2, 112601030 PL_SHOULDER for 2, 113601040 PL_PANTS for 2, 114601036 PL_SHOES for 2 | 100001041 SWORD for 4 | 115001075 for 3 | 18 coins |
      | bronze, level 21 (Abyss entry) | Assassin | 204360 | 110301127 LT_TORSO for 3, 111301081 LT_GLOVE for 2, 112301027 LT_SHOULDER for 2, 113301099 LT_PANTS for 2, 114301133 LT_SHOES for 2 | 100200921 DAGGER for 4 | none | 15 coins |
      | bronze, level 21 (Abyss entry) | Ranger | 204360 | 110301127 LT_TORSO for 3, 111301081 LT_GLOVE for 2, 112301027 LT_SHOULDER for 2, 113301099 LT_PANTS for 2, 114301133 LT_SHOES for 2 | 101700828 BOW for 4 | none | 15 coins |
      | bronze, level 21 (Abyss entry) | Sorcerer, Spirit Master | 204425 | 110101192 RB_TORSO for 3, 111101082 RB_GLOVE for 2, 112101041 RB_SHOULDER for 2, 113101096 RB_PANTS for 2, 114101123 RB_SHOES for 2 | 100600861 SPELLBOOK for 4 | none | 15 coins |
      | bronze, level 21 (Abyss entry) | Gunner | 204360 | 110301127 LT_TORSO for 3, 111301081 LT_GLOVE for 2, 112301027 LT_SHOULDER for 2, 113301099 LT_PANTS for 2, 114301133 LT_SHOES for 2 | 101800685 GUN for 4 | none | 15 coins |
      | bronze, level 21 (Abyss entry) | Rider | 204360 | 110551017 CH_TORSO for 3, 111501575 CH_GLOVE for 2, 112501517 CH_SHOULDER for 2, 113501593 CH_PANTS for 2, 114501602 CH_SHOES for 2 | 102100607 KEYBLADE for 4 | none | 15 coins |
      | bronze, level 21 (Abyss entry) | Bard | 204425 | 110101192 RB_TORSO for 3, 111101082 RB_GLOVE for 2, 112101041 RB_SHOULDER for 2, 113101096 RB_PANTS for 2, 114101123 RB_SHOES for 2 | 102000722 HARP for 4 | none | 15 coins |
      | bronze, level 26 (Abyss entry) | Cleric, Chanter | 204425 | 110501104 CH_TORSO for 13, 111501073 CH_GLOVE for 7, 112501023 CH_SHOULDER for 7, 113501082 CH_PANTS for 10, 114501089 CH_SHOES for 7 | 101500818 STAFF for 19 | none | 63 coins |
      | bronze, level 26 (Abyss entry) | Gladiator | 204360 | 110601086 PL_TORSO for 13, 111601063 PL_GLOVE for 7, 112601037 PL_SHOULDER for 7, 113601047 PL_PANTS for 10, 114601043 PL_SHOES for 7 | 100900790 GREATSWORD for 19 | none | 63 coins |
      | bronze, level 26 (Abyss entry) | Templar | 204360 | 110601086 PL_TORSO for 13, 111601063 PL_GLOVE for 7, 112601037 PL_SHOULDER for 7, 113601047 PL_PANTS for 10, 114601043 PL_SHOES for 7 | 100001048 SWORD for 19 | 115001082 for 13 | 76 coins |
      | bronze, level 26 (Abyss entry) | Assassin | 204360 | 110301134 LT_TORSO for 13, 111301088 LT_GLOVE for 7, 112301034 LT_SHOULDER for 7, 113301106 LT_PANTS for 10, 114301140 LT_SHOES for 7 | 100200928 DAGGER for 19 | none | 63 coins |
      | bronze, level 26 (Abyss entry) | Ranger | 204360 | 110301134 LT_TORSO for 13, 111301088 LT_GLOVE for 7, 112301034 LT_SHOULDER for 7, 113301106 LT_PANTS for 10, 114301140 LT_SHOES for 7 | 101700835 BOW for 19 | none | 63 coins |
      | bronze, level 26 (Abyss entry) | Sorcerer, Spirit Master | 204425 | 110101199 RB_TORSO for 13, 111101089 RB_GLOVE for 7, 112101048 RB_SHOULDER for 7, 113101103 RB_PANTS for 10, 114101130 RB_SHOES for 7 | 100600868 SPELLBOOK for 19 | none | 63 coins |
      | bronze, level 26 (Abyss entry) | Gunner | 204360 | 110301134 LT_TORSO for 13, 111301088 LT_GLOVE for 7, 112301034 LT_SHOULDER for 7, 113301106 LT_PANTS for 10, 114301140 LT_SHOES for 7 | 101800692 GUN for 19 | none | 63 coins |
      | bronze, level 26 (Abyss entry) | Rider | 204360 | 110551019 CH_TORSO for 13, 111501577 CH_GLOVE for 7, 112501519 CH_SHOULDER for 7, 113501595 CH_PANTS for 10, 114501604 CH_SHOES for 7 | 102100610 KEYBLADE for 19 | none | 63 coins |
      | bronze, level 26 (Abyss entry) | Bard | 204425 | 110101199 RB_TORSO for 13, 111101089 RB_GLOVE for 7, 112101048 RB_SHOULDER for 7, 113101103 RB_PANTS for 10, 114101130 RB_SHOES for 7 | 102000729 HARP for 19 | none | 63 coins |

      Every class has five armor pieces and a weapon at every tier. For the Cleric the
      rule gives the contracts' own lists. At level 16 the leg buys three of the five; the
      two it leaves, the hauberk and the brogans, score no higher than the Legionary pieces
      it wears. At levels 21 and 26 the rule gives the contract's five pieces and its
      staff, id for id and coin for coin.
    - **For NR-38b and the operator.** A full level-26 set is 44 coins of armor and 19 for
      the weapon; a Templar's shield is 13 more. The supply the operator approved for the
      Abyss entry is 44 Bronze Coins (AX-Q5). What a class does when its tier costs more
      is NR-38b's to settle by that item's default, not this one's.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout, -Parallel 8, run
      guard-p8 (run/nr/NR-38/guard-p8/verdict.json): verdict pass, all twelve scopes
      identical, hm and ax among them. Seven pre-commit checks pass, Aion.GameServer.Tests
      passes (4,629 passed, 16 skipped) and Fast passes (run nr38-fast, 11 passed).
- [x] **NR-38a - The coin-gear leg shops by the class's manifest.** Depends: NR-38
  - Work: For a class other than the contract's, the coin-gear leg (cg) takes its vendor
    and its level-16 manifest from NaturalCoinManifests, and buys and wears the pieces
    that beat what it wears by its own gear score. The incoming coins, the reward and the
    balance at the end are observed, not pinned to 18, 5 and 19. The weapon it holds when
    the leg starts is the one it must still hold, whatever its item id. The Cleric's
    receipts and its three purchases are what they were.
  - Proof: One-time check of the leg's decisions for every class from a prepared bag; gate
    hm identical.
  - 2026-10-09: done. For a class other than the contract's, the coin-gear scope is made
    from its manifest when the leg is taken up. The Cleric's leg is the contract itself.
    - **Java.** As NR-38: a reward shop asks nothing of the buyer's class. No server
      change.
    - **The change.**
      - Sc/NaturalCoinGearPolicy.cs, NaturalCoinGear.ForClass: the vendor and the trade
        tab are the manifest's; the purchases are the manifest's armor pieces the class's
        gear rules score above what it wears in the slot, in the manifest's order, while
        the coins it has and the quest's five pay for them; the body slots are what it
        then wears; the incoming and the end balance are counted; the weapon it holds in
        its main hand is the one it must keep, with the slot it is worn in (WeaponSlot: 3
        for a two-hand weapon, 1 for a one-hand). No weapon is bought on this leg, for any
        class: the leg keeps the loadout it arrives with but for the armor it buys.
      - The policy and the steps read the weapon's slot and the two coin counts from the
        scope where they had 3, 18 and 23 written in.
      - J: the step that gives another class its picks and protected items gives it this
        scope too, and traces it as leg-coin-manifest.
    - **Proof, the one-time check** (run/nr/NR-38a/check.log; the check file is not
      committed). Each class starts with its level-21 weapon held, the level-16 Legionary
      torso and shoes of its armor worn, 18 Iron Coins and the sealed bundle, and the
      leg's own decision rule is played to its end: the reward, each purchase with its
      receipt, each equip, and "complete". Every class ends at 18 + 5 - 4 = 19 coins.

      | Class | Vendor, tab | Weapon kept, slot | Bought and worn | Left at the vendor |
      |---|---|---|---|---|
      | Cleric | 203689, 985 | 101501357, 3 | 111501065 for 1, 112501015 for 1, 113501074 for 2 | 110501096 CH_TORSO, 114501081 CH_SHOES |
      | Chanter | 203689, 985 | 101501357, 3 | 111501065 for 1, 112501015 for 1, 113501074 for 2 | 110501096 CH_TORSO, 114501081 CH_SHOES |
      | Gladiator | 203659, 984 | 100901375, 3 | 111601055 for 1, 112601029 for 1, 113601039 for 2 | 110601078 PL_TORSO, 114601035 PL_SHOES |
      | Templar | 203659, 984 | 100001737, 1 | 111601055 for 1, 112601029 for 1, 113601039 for 2 | 110601078 PL_TORSO, 114601035 PL_SHOES |
      | Assassin | 203659, 984 | 100201503, 1 | 111301080 for 1, 112301026 for 1, 113301098 for 2 | 110301126 LT_TORSO, 114301132 LT_SHOES |
      | Ranger | 203659, 984 | 101701368, 3 | 111301080 for 1, 112301026 for 1, 113301098 for 2 | 110301126 LT_TORSO, 114301132 LT_SHOES |
      | Sorcerer | 203689, 985 | 100601431, 3 | 111101081 for 1, 112101040 for 1, 113101095 for 2 | 110101191 RB_TORSO, 114101122 RB_SHOES |
      | Spirit Master | 203689, 985 | 100601431, 3 | 111101081 for 1, 112101040 for 1, 113101095 for 2 | 110101191 RB_TORSO, 114101122 RB_SHOES |
      | Gunner | 203659, 984 | 101801218, 1 | 111301080 for 1, 112301026 for 1, 113301098 for 2 | 110301126 LT_TORSO, 114301132 LT_SHOES |
      | Rider | 203659, 984 | 102101072, 3 | 111501574 for 1, 112501516 for 1, 113501592 for 2 | 110551016 CH_TORSO, 114501601 CH_SHOES |
      | Bard | 203689, 985 | 102001246, 3 | 111101081 for 1, 112101040 for 1, 113101095 for 2 | 110101191 RB_TORSO, 114101122 RB_SHOES |

      The torso and the shoes are left because the Legionary pieces worn score as high.
      Given the accepted Cleric's bag, the rule returns the contract's own scope: vendor,
      tab, staff, slot, the three purchases in order, the five body slots, the protected
      list, 18 in and 19 out, and the cloth gloves as the pair replaced.
    - **Not proven here.** No class but the Cleric has walked to Lateni and traded with
      him; that is first played when another line reaches this leg. The leg's end check
      of the retained loadout and Haramel's use of the kept weapon are NR-40.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout, -Parallel 8, run
      guard-p8 (run/nr/NR-38a/guard-p8/verdict.json): verdict pass, all twelve scopes
      identical, hm among them. Seven pre-commit checks pass, Aion.GameServer.Tests passes
      (4,629 passed, 16 skipped) and Fast passes (run nr38a-fast, 11 passed).
- [x] **NR-38b - The Abyss entry's two tiers shop by the class's manifest.** Depends:
  NR-38a
  - Work: For a class other than the contract's, the two coin-armor tiers of the Abyss
    entry take the vendor and the manifests of levels 21 and 26, and the weapon is the
    class's own by its own stat. Default for a tier that costs more than the 44 Bronze
    Coins the operator approved: armor first, the slots it improves, dearest gain first,
    and the weapon and the shield only from coins left over; nothing more is supplied.
  - Proof: One-time check of each class's purchases at each tier from a prepared bag;
    gate ax identical.
  - 2026-10-09: done, without the trimming rule the Work line named: see NR-Q14. For a
    class other than the contract's, the two tiers are made from its manifests when the
    leg is taken up. The Cleric's leg is the contract itself.
    - **Java.** As NR-38. No server change.
    - **The change.**
      - Sc/NaturalAbyssEntry.cs, NaturalAbyssCoinArmor.ForClass: the vendor, the armor tab
        and the weapon tab are the manifests'; each tier's five pieces and its weapon are
        the class's, the weapon with the slot it is worn in.
      - Sc/Classes/NaturalGearRules.cs, WeaponNumber: a weapon as one whole number that
        rises with the gear score, the group's place and then the class's weapon stat. It
        is what the equipment check wears by, so a weapon the tier calls better is one the
        check then puts on.
      - Sc/NaturalAbyssCoinArmor.cs: the tier's weapon is compared by magic boost for the
        contract's class and by the weapon number for another (the rule name
        "weapon-number"); the wording follows; the weapon is bought from the weapon tab
        whatever its rule.
      - J: the step that gives another class its picks gives it these tiers too
        (leg-coin-manifest), and the leg's weapon comparison asks the class's number.
      - **Not done:** a shield is not part of a tier, so a class that holds one buys none
        here. And no rule trims a tier that costs more coins than are approved (NR-Q14).
    - **Proof, the one-time check** (run/nr/NR-38b/check.log; the check file is not
      committed). The worst bag: the whole level-16 coin set worn, the arena weapon held,
      seven Bronze Coins. The leg's own rule is asked at level 21, the purchases are worn,
      and it is asked at level 26; after each tier's purchases are worn it has nothing
      more to buy there.

      | Class | Vendor; armor tab, weapon tab | Weapon slot | Level 21 | Level 26 | All |
      |---|---|---|---|---|---|
      | Cleric | 204425; 991, 989 | 3 | buys 5 for 11 | buys 6 for 63 | 74 coins, 67 to supply |
      | Chanter | 204425; 991, 989 | 3 | buys 5 for 11 | buys 6 for 63 | 74 coins, 67 to supply |
      | Gladiator | 204360; 990, 988 | 3 | buys 5 for 11 | buys 5 for 44, the weapon left | 55 coins, 48 to supply |
      | Templar | 204360; 990, 988 | 1 | buys 5 for 11 | buys 6 for 63 | 74 coins, 67 to supply |
      | Assassin | 204360; 990, 988 | 1 | buys 5 for 11 | buys 6 for 63 | 74 coins, 67 to supply |
      | Ranger | 204360; 990, 988 | 3 | buys 5 for 11 | buys 6 for 63 | 74 coins, 67 to supply |
      | Sorcerer | 204425; 991, 989 | 3 | buys 4 for 9 | buys 6 for 63 | 72 coins, 65 to supply |
      | Spirit Master | 204425; 991, 989 | 3 | buys 4 for 9 | buys 6 for 63 | 72 coins, 65 to supply |
      | Gunner | 204360; 990, 988 | 1 | buys 5 for 11 | buys 6 for 63 | 74 coins, 67 to supply |
      | Rider | 204360; 990, 988 | 3 | buys 5 for 11 | buys 6 for 63 | 74 coins, 67 to supply |
      | Bard | 204425; 991, 989 | 3 | buys 4 for 9 | buys 6 for 63 | 72 coins, 65 to supply |

      The Gladiator leaves the level-26 greatsword because the arena greatsword's number is
      as high. For the Cleric the rule returns the contract's vendor, tabs, pieces and
      staffs. The recorded Cleric, which arrives better dressed than this bag, was supplied
      33 coins (run/nr/NR-38b/guard-p8-ax/help-items.json).
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout, -Parallel 8, run
      guard-p8 (run/nr/NR-38b/guard-p8/verdict.json): verdict pass, all twelve scopes
      identical, ax among them. Seven pre-commit checks pass, Aion.GameServer.Tests passes
      (4,629 passed, 16 skipped) and Fast passes (run nr38b-fast, 11 passed).
- [x] **NR-39 - The destiny leg by class.** Depends: NR-32
  - Work: Java first (_2900NoEscapingDestiny.java). The stone, its skill and the reward are
    read by class; the instance's fight casts the temporary skill by a role. A class whose
    stone needs a melee weapon and that holds none is recorded as a finding for that class.
  - Proof: One-time check of the four stones against Java's table; the Cleric's l11 leg
    replayed from altgard-l10 with an identical outcome.
  - 2026-10-09: done. The campaign's stone, the skill it grants and the class reward the
    server never gives are the class's own. The Cleric's leg is the contract itself.
    - **Java.** _2900NoEscapingDestiny.java: Heimdall hands the stone of getStoneId
      (240-262) at SELECT7_1; equipping any of the four moves the quest from 99 to 97
      (onEquipItemEvent, registered for all four at 31-41); SETPRO8 spawns the enemy for
      five minutes; its death, by any means, moves 98 to 9 and teleports the player out
      (onKillEvent). The handler never asks that the stone's skill is used. getStoneId
      carries a TODO: which stones retail gave each class is not known to Java; that is a
      retail question and stops no class. The port has the same table
      (Handlers/Quest/pandaemonium/_2900NoEscapingDestiny.cs 279-298). No server change.
    - **The table.**

      | Stone | Classes | Skill granted (the one skill of the stone's group) |
      |---|---|---|
      | 140000001 Healing Light II | Cleric, Chanter, Bard | 11504 |
      | 140000002 Flame Cage I | Ranger, Gunner, Rider | 11505 |
      | 140000003 Ferocious Strike III, which needs a melee weapon | Gladiator, Templar, Assassin | 11506 |
      | 140000004 Hydro Eruption II | Sorcerer, Spirit Master | 11507 |

      The three classes of the third stone hold a melee weapon by NR-Q5 (greatsword, sword,
      daggers), so no class is left with a stone it cannot use. The class reward of Q2900
      that the leg checks was never given (NR-32) is each class's own entry: Gladiator
      140000008, Templar 140000027, Ranger 140000047, Assassin 140000076, Sorcerer
      140000131, Spirit Master 140000147, Cleric 140000098, Chanter 140000112, Bard
      140000859, Gunner 140000943, Rider 140001002.
    - **The change.**
      - Sc/NaturalAltgardContract.cs, NaturalAltgardDestiny: StoneFor is Java's table;
        ForClass gives the campaign for a class with its stone, the skill of the stone's
        group from the shipped skill data, and its class reward entry from quest_data.
      - J: the step that gives another class its picks gives it this campaign too
        (leg-destiny-stone), and the coin-gear leg's "this stigma skill must not be
        learned" is the class's own stone's skill.
      - **Not done, on purpose:** no class casts the stone's skill in the instance. Java
        does not ask for it, and the Cleric does not cast its own; the fight is the class's
        ordinary fight.
    - **Proof, the one-time check** (run/nr/NR-39/check.log; the check file is not
      committed): the bot's table was compared with the switch read out of Java's source
      file for the eleven second classes; a starter class is refused; for the Cleric
      ForClass returns the contract.
    - **Proof, the Cleric's leg 11.** Replayed from altgard-l10 on the build without this
      item's change (run l11-before, by setting the two files aside and back) and on the
      build with it (run l11-after): both complete with the endpoint verified, and the
      two traces are identical record for record, 7,323 records, digest
      0f6d4d5b39a812e6 (run/nr/NR-39/l11-compare.log).
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout, -Parallel 8, run
      guard-p8 (run/nr/NR-39/guard-p8/verdict.json): verdict pass, all twelve scopes
      identical. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629 passed,
      16 skipped) and Fast passes (run nr39-fast, 11 passed).
- [x] **NR-40 - Haramel by class.** Depends: NR-33, NR-38a
  - Work: Java first (HaramelInstance.java). The chest by class, the upgrade groups by the
    class's armor type, Q28505's list by class, and the object ids and coin counts read at
    the leg's start.
  - Proof: One-time check of the four chests against Java's table; gate hm identical.
  - 2026-10-09: done. The Haramel scope's chest, kept weapon, coin counts, armor and
    stigma facts are the class's own. The Cleric's leg is the contract itself.
    - **Java.** HaramelInstance.onDie 24-53: when the boss 216922 dies, the instance
      spawns one chest at the same spot, by the class of the player who did it most
      damage: 700829 for Gladiator and Templar; 700830 for Assassin, Ranger and Gunner;
      700831 for Bard, Sorcerer and Spirit Master; 700832 for Cleric, Chanter and Rider. A
      starter class gets none. The port has the same switch. No server change.
    - **Q28505's list.** Nothing to do: NR-32 found that the server offers every class the
      general list there, and the leg's pick is the class's own by that item.
    - **The change.**
      - Sc/NaturalHaramel.cs: ChestFor is Java's table. The scope carries what was written
        into the code for the Cleric: the slot the kept weapon is worn in (3), the five
        armor pieces it brings in by id, the armor it may put on inside the leg (chain),
        and the stigma stone, reward and skill that must not be found (NR-39). ForClass
        gives a class its own: its chest, the weapon it holds with its object and slot, no
        armor asked for by id, its own armor kind, its Iron Coins as counted, its Bronze
        Coins as counted and the seven the leg's quests pay, and its own stone's facts.
      - Sc/NaturalHaramelDecisionEngine.cs and the leg's checks read those from the scope.
      - Sc/NaturalAltgardContinuation.cs, BindIncoming: told that the character is not of
        the contract's class, it binds the journal only; the staff and gloves it looked
        for are the Cleric's.
      - J: the step that gives another class its picks gives it this scope too
        (leg-haramel-scope); the equipment check inside the leg asks the scope what may be
        put on.
    - **Proof, the one-time check** (run/nr/NR-40/check.log; the check file is not
      committed): the bot's chest table was compared with the switch read out of Java's
      source for the eleven second classes. Each class's scope from a bag with its weapon
      held, 19 Iron Coins and no Bronze Coins:

      | Classes | Chest | Weapon slot | Armor it may put on | Stone, skill that must not be held |
      |---|---|---|---|---|
      | Gladiator | 700829 | 3 | plate | 140000003, 11506 |
      | Templar | 700829 | 1 | plate | 140000003, 11506 |
      | Assassin | 700830 | 1 | leather | 140000003, 11506 |
      | Ranger | 700830 | 3 | leather | 140000002, 11505 |
      | Gunner | 700830 | 1 | leather | 140000002, 11505 |
      | Sorcerer, Spirit Master | 700831 | 3 | robe | 140000004, 11507 |
      | Bard | 700831 | 3 | robe | 140000001, 11504 |
      | Cleric, Chanter | 700832 | 3 | chain | 140000001, 11504 |
      | Rider | 700832 | 3 | chain | 140000002, 11505 |

      Every class ends with 19 iron and 7 bronze from that bag. The contract's own scope
      reads as it did: the same incoming list, the same groups it may put on, chest
      700832. For the Cleric, ForClass returns the contract's facts.
    - **Not proven here.** No class but the Cleric has opened its chest. The chest is
      spawned by the instance and found by sight; whether the walk to another chest id
      works is first played when another line reaches Haramel.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout, -Parallel 8, run
      guard-p8 (run/nr/NR-40/guard-p8/verdict.json): verdict pass, all twelve scopes
      identical, hm among them. Seven pre-commit checks pass, Aion.GameServer.Tests passes
      (4,629 passed, 16 skipped) and Fast passes (run nr40-fast, 11 passed).
- [x] **NR-41 - Leg starts as minimums, and one run through every leg for a line.**
  Depends: NR-31 to NR-40, NR-38a and NR-38b
  - Work: A leg's start facts that are receipts of the accepted run (an exact level, coin
    count or journal) are minimums for another line. The continuous journey takes a class
    line and plays the legs in the Cleric's order; a capture after a leg is named
    <the Cleric's snapshot name>-<class>.
  - Proof: scripts/sim/test-sim-snapshot.ps1 passes; the full gate identical.
  - 2026-10-09: done. A leg's start is the accepted run's receipt for the contract's
    class and a set of minimums for another. The Cleric plays as recorded.
    - **Java.** None read: these are the bot's own checks of where a leg may begin.
    - **Where the receipts were.**
      - The Abyss entry's start (NaturalAbyssEntryLeg.VerifyStart): level exactly 25, the
        journal exactly Q2945, the Altgard Dark Legionary Staff in both hands, exactly
        seven Bronze Coins. For another class: at least level 25, Q2945 untouched among
        whatever else is open, one weapon held in the main hand, whatever coins it has.
      - Every leg's incoming journal (start.completedQuestIds), which the coin-gear,
        Haramel and Abyss legs count against. It names Q2904, the dispatch quest of the
        Priest's classes. For another class the leg is first given its own dispatch quest
        there, and the journal is then bound to what the character has completed, so the
        counts are minimums met. This binding ran only on the revised route; another class
        gets it on every leg.
      - The coin counts, the kept weapon and the worn gear of the coin-gear and Haramel
        legs became the class's own in NR-38a and NR-40.
    - **The change.**
      - Sc/NaturalAbyssEntry.cs, VerifyStart: told whether the character is of the
        contract's class.
      - J: a leg is made the class's own before its journal is bound, in both places a
        leg is taken up; the step that makes it the class's own now runs for every leg,
        not only those with picks or a scope, and swaps the dispatch quest in the incoming
        journal.
      - **The continuous journey** has no check of its own on the line: it plays the legs
        in NaturalAltgardContinuation.Order, the Cleric's, and each leg is given to the
        observed class as above. Nothing was added there.
      - scripts/sim/sim-snapshot.ps1: a leg capture of another line that has a second
        class must be named with the class after the accepted line's snapshot name
        (altgard-l1-chanter). A name without it is refused before anything is played.
        scripts/sim/test-sim-snapshot.ps1 holds that.
    - **Proof, the one-time check** (run/nr/NR-41/check.log; the check file is not
      committed), on the Abyss entry's start:

      | The character arrives with | The contract's class | Another class |
      |---|---|---|
      | Level 25, the staff, 7 Bronze Coins, Q2945 alone | starts | starts |
      | Level 26 | refused | starts |
      | Level 24 | refused | refused |
      | A sword in one hand | refused | starts |
      | 3 Bronze Coins | refused | starts |
      | Another quest open beside Q2945 | refused | starts |

      Another class's incoming binding takes the journal as the character has it and
      leaves the leg's scopes alone.
    - **Not proven here.** No line but the accepted one has played a leg, and no run
      through every leg was played for another line in this item: its Proof line asks for
      none. The first is phase D's, in a round.
    - **Proof.** scripts/sim/test-sim-snapshot.ps1 passes, with test_compare_traces.py and
      test-code-coverage.ps1 (run/nr/NR-41/script-tests.log). Gate, set
      all+mage+warrior+artist+engineer+scout, -Parallel 8, run guard-p8
      (run/nr/NR-41/guard-p8/verdict.json): verdict pass, all twelve scopes identical.
      Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629 passed, 16
      skipped) and Fast passes (run nr41-fast, 11 passed).
- [x] **NR-42 - Phase C closed.** Depends: NR-30 to NR-41, NR-38a, NR-38b, NR-43 to NR-48,
  NR-44a and NR-46a to NR-46d
  - Work: No code. The full gate on every recorded scope, and one table in this document
    of what each class gets at each class-bound point of the route.
  - Proof: Every scope identical to its baseline.
  - 2026-10-09: done. No code. Phase C is closed at 8b87eaa08.
    - **Proof.** Gate at the committed head, set all+mage+warrior+artist+engineer+scout,
      -Parallel 8, run close-p8 (run/nr/NR-42/close-p8/verdict.json): verdict pass, all
      twelve scopes identical to their baselines: p 35,463 records, m 109,039, b 131,197,
      l1 29,797, c 112,256, hm 37,510, ax 15,715, mage 23,555, warrior 24,199, artist
      23,104, engineer 24,580, scout 27,592. The replays record commit 8b87eaa08.
    - **What each class gets at each class-bound point of the route.**

      | Class | Ceremony weapon, Q2009 (NR-30) | Dispatch quest (NR-30) | Coin vendors, iron and bronze; armor (NR-38) | Destiny stone, its skill (NR-39) | Haramel chest (NR-40) | Arena weapon, Q2947 (NR-32) | Help kinds from level 10 (NR-34) |
      |---|---|---|---|---|---|---|---|
      | Cleric | staff 101500498 | Q2904 | Lohaban 203689, Vebna 204425; chain | 140000001, 11504 | 700832 | staff 101501224 | every class, caster, reagent |
      | Chanter | staff 101500498 | Q2904 | Lohaban, Vebna; chain | 140000001, 11504 | 700832 | staff 101501224 | every class, caster, reagent |
      | Gladiator | greatsword 100900488 | Q2901 | Lateni 203659, Nott 204360; plate | 140000003, 11506 | 700829 | greatsword 100901214 | every class, reagent |
      | Templar | sword 100000640 | Q2901 | Lateni, Nott; plate, and a shield | 140000003, 11506 | 700829 | sword 100001562 | every class, reagent |
      | Assassin | dagger 100200605 | Q2902 | Lateni, Nott; leather | 140000003, 11506 | 700830 | dagger 100201365 | every class, reagent |
      | Ranger | bow 101700515 | Q2902 | Lateni, Nott; leather | 140000002, 11505 | 700830 | bow 101701246 | every class, reagent |
      | Sorcerer | spellbook 100600532 | Q2903 | Lohaban, Vebna; robe | 140000004, 11507 | 700831 | spellbook 100601285 | every class, caster, reagent |
      | Spirit Master | spellbook 100600532 | Q2903 | Lohaban, Vebna; robe | 140000004, 11507 | 700831 | spellbook 100601285 | every class, caster, reagent |
      | Gunner | pistol 101800506 | Q29070 | Lateni, Nott; leather | 140000002, 11505 | 700830 | pistol 101801035 | every class, reagent |
      | Rider | cipher-blade 102100489 | Q29070 | Lateni, Nott; chain | 140000002, 11505 | 700832 | cipher-blade 102100833 | every class, reagent |
      | Bard | harp 102000523 | Q29071 | Lohaban, Vebna; robe | 140000001, 11504 | 700831 | harp 102001058 | every class, caster, reagent |

      The fourteen reward picks of the legs are NR-32's table; the coin pieces by tier are
      NR-38's; the bridge's shop is NR-35's; the shot in flight is NR-36's; the patrol
      view is NR-37's. The dispatch quest, the stone, the chest and the vendors' goods are
      the server's, read from Java and the shipped data. The ceremony weapon is NR-Q5's.
      The armor kinds, the weapon groups and the help kinds of the nine classes without a
      profile are the defaults of NR-Q5, NR-Q7 and NR-Q13: each class's first item (NR-x0)
      writes its gear table and profile, and what follows from them follows.
    - **What phase C leaves.**
      - **Not played.** Every rule of this phase was proven on the Cleric's scopes staying
        identical and on one-time checks of what the rule gives each class. No line but
        the accepted one has played a leg. First played in phase D: a trade with Lateni
        and with Nott, a chest other than 700832, a weapon swing in flight, another class's
        stone, a start held to minimums.
      - **For the operator, each with its default in force:** NR-Q11 (bots to a world),
        NR-Q12 (the Cleric and the bonus order for accessories and hats), NR-Q13 (who
        casts from mana; no Courage scroll is approved), NR-Q14 (Bronze Coins beyond the
        approved 44).
      - **Recorded, not followed:** quest_data.xml carries class reward lists for Q2900
        and Q28505 that the server never offers (NR-32), and Java's own TODO on which
        stigma stone retail gave each class (NR-39). Both are retail questions that stop
        no class.
      - **Known gaps a class will meet:** a shield is not part of a coin tier, so a
        Templar buys none (NR-38b); the Scout's and the Engineer's patrol "heal" is a
        defence until their second classes name their own (NR-37); no class casts its
        stone's skill in the Space of Destiny, as the Cleric does not (NR-39).
    - **Next.** Phase D by rule (w): the surveys NR-50 to NR-140 first, one class after
      another, in the order of NR-Q1, each ending with its line, its gear table and its
      profile accepted by the validator; then the probe rows side by side; then the rounds.

### D. One class after another

For each class, in the order of NR-Q1, ten numbers are reserved: Templar NR-50, Sorcerer
NR-60, Chanter NR-70, Gladiator NR-80, Assassin NR-90, Ranger NR-100, Spirit Master NR-110,
Gunner NR-120, Rider NR-130, Bard NR-140. The class's survey item writes the others.

**In rounds** (rule (w), Survey C1; this replaces "one class after another" for the play):

1. The surveys NR-x0 are worked first, one class after another. They are reading and
   data, and each ends with its profile accepted by the validator.
2. The probe rows NR-x1 of the classes are played side by side, each in its own world.
   The Templar's rows (NR-51) are played first and alone, before the other surveys: they
   are the first fights of a second class without a heal, on forms the other profiles copy.
   For the same reason the Templar plays its first stage, to the Altgard bind (NR-52),
   alone and before the other surveys: no line but the Priest's has passed the trial, the
   class choice, the ceremony and the dispatch, and what stops it there stops every class.
   NR-52 showed it: two stops on the bridge, both at places only the Priest's line had
   passed. So the Templar is the pilot for the legs too (NR-53 to NR-57): it plays each
   alone and ahead, and what it meets is mended once. The other classes' surveys and the
   rounds of all classes follow.
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

- [x] **NR-50 - Templar: survey and profile data.** Depends: the close of phase C
  - 2026-10-09: done. The Templar has a class line, a gear table and a profile the
    validator accepts. Nothing has been played. Four things its skills need that the
    shared forms cannot say yet are items NR-50a to NR-50d below.
    - **Java.**
      - **What it learns.** SkillLearnService.learnNewSkills 60-97 gives a character, at
        each level, every auto-learn row of skill_tree.xml for the class it then is (85-86,
        as read for CP-35). Of the Templar's 93 rows to level 26, 76 are auto-learned and
        17 are stigma rows from level 20. None is a skill book, so the route needs no
        trainer. 63 of the auto-learned are active skills (run/nr/NR-50/survey.log).
      - **Chains.** ChainCondition.validate 32-53 accepts a follow-up whose step before it
        is the current chain step or the one before that; shouldReset 55-73 resets the
        chain at another chain's first step. So after Ferocious Strike and Robust Blow,
        Rage may be cast and Wrath Strike still follows Robust Blow; after Wrath Strike,
        Rage may not.
      - **A shield in the left hand.** LeftHandCondition.validate 25-45: with type SHIELD
        the skill needs a shield worn; with DUAL a second weapon or a two-hand weapon.
        Shield Bash, Shield Counter, Avenging Blow, Courageous Shield and the toggle
        Stubborn Spirit carry it.
      - **Counter skills.** SkillTemplate.java 104-105 holds counter_skill as one
        AttackStatus, and Skill.java 163-169 refuses the skill unless that status was the
        player's within the last 5 s. The data gives 30 skills two statuses in one
        attribute ("BLOCK,RESIST" on 22, "RESIST,PARRY" on 6, "RESIST,DODGE" on 2). No
        single status has such a name, so JAXB leaves the field empty and Java asks
        nothing before those skills; the port reads it the same way on purpose
        (SkillEngine/Model/SkillTemplate.cs 163-176). The Templar's Shield Counter,
        Avenging Blow and Courageous Shield are three of the 22. A client offers them only
        after a block or a resist. This is under Blocked as a retail question; it stops
        nothing, because the profile leaves the three out.
      - **Class rewards on the route** are the two of NR-32, Q2009 and Q2947, and the
        stone of Q2900 (NR-39). Nothing new.
    - **The class line.** `warrior-templar`: a Warrior who becomes a Templar, SIM account
      41, character Asimtemplar, the sword 100000640 at the ceremony (Q2009, choice 1;
      NR-Q5). Its dispatch quest is Q2901 (NR-30). It has a name of its own because the
      lines of a round play in one world. It is in NaturalClassLine.All and in the script's
      list; the script test holds the two together.
    - **The gear table** (Sc/Classes/NaturalClassGearTable.cs, Templar).

      | Part | The Templar's | From |
      |---|---|---|
      | Weapon groups | sword, then mace; ranked by the physical stat | NR-Q5, CP-Q7 |
      | Off hand | the best shield it owns | NR-Q5 |
      | Armor | plate, chain, leather, robe, clothes; item level first, the type breaks ties | NR-Q7, CP-Q24 |
      | Bonus order at a reward | physical attack, critical, accuracy, then HP | NR-32 |
      | Kept | life potions, its help kit, a mana potion it finds, every help scroll and food | as the Cleric from Ascension |

      Its masteries allow the greatsword too; NR-Q5 gives it the one-hand weapon. The
      table is the provisional one of NR-32, so the Templar's rows of NR-32, NR-38,
      NR-38a, NR-38b and NR-40 stand as written.
    - **Where its shield comes from** (NR-Q15). On the route: the Raider's Shield
      115000024 (level 3) in the reward list of Q2100 in Ishalgen; a shield at each coin
      vendor (115001074 for 2 Iron Coins at level 16; 13 Bronze Coins for the two of
      Morheim, NR-38). Q28505's class list holds Lateni's Shield 115001093, which the
      server never offers (NR-32). As the code stands the line takes none of them, so a
      Templar would reach the endpoint with an empty off hand unless a monster dropped
      a shield. NR-50c.
    - **The help kit from level 10, as a manifest** (NR-Q8, NR-Q13; the rows of
      NaturalHelpItemAllowlist.Kit(caster: false, reagent: true)).

      | Item | Family | Levels | Topped up to | When it owns fewer than |
      |---|---|---|---|---|
      | 162000002 | life potion | 10 to 19 | 30 | 10 |
      | 162000003 | life potion | 20 to 29 | 30 | 10 |
      | 162000004 | life potion | 30 to 39 | 30 | 10 |
      | 164000067 | shield scroll | 10 to 19 | 30 | 8 |
      | 164000068 | shield scroll | 20 to 29 | 30 | 8 |
      | 164000069 | shield scroll | 30 to 39 | 30 | 8 |
      | 164000075 | running scroll | 20 to 29 | 20 | 5 |
      | 164000076 | running scroll | 30 to 39 | 20 | 5 |
      | 160002273 | DP jelly | 10 to 39 | 8 | 2 |
      | 169300003 | powder | 10 to 24 | 200 | 50 |
      | 169300004 | powder | 25 to 39 | 200 | 50 |

      Not supplied: the mana serums 162000017 to 162000019 and the Awakening scrolls
      164000133 and 164000134, which are for a class that casts from mana. The scroll
      slot Awakening shares is the Templar's for Courage, and no Courage scroll is
      supplied (NR-Q13). Levels 1 to 9 are the Warrior's kit (CP-Q12).
    - **Its skills by role** (Sc/Classes/NaturalTemplarProfile.cs; 35 skills of 63).

      | Role | Skill, and the levels of its ranks | What it is |
      |---|---|---|
      | strike | Ferocious Strike: 1, 6, 11, 16, 21, 26 | Opens the first chain. 10 s. |
      | robust | Robust Blow: 3, 8, 13, 18, 23 | Follows Ferocious Strike inside 3 s. 8 s. |
      | rage | Rage: 7, 12, 17, 22 | Follows Ferocious Strike: attack and a shield for 10 s. 17 to 26 MP, 24 s. |
      | wrath | Wrath Strike: 19, 24 | Follows Robust Blow inside 3 s. 8 s. |
      | smash | Body Smash: 5, 10, 15, 20, 25 | Opens a chain of its own. 12 s. |
      | dazing | Dazing Severe Blow: 10, 15, 20, 25 | Opens the third chain; slows the target's attacks and lowers its physical defence for 12 s. 12 s. |
      | divine | Divine Blow: 11, 16, 21, 26 | Follows Dazing Severe Blow inside 3 s. 8 s. |
      | chastise | Empyrean Chastisement: 10, 15, 20, 25 | 2,000 DP: a hit, and a shield that takes half of every hit for 15 s. 6 s. |
      | armor | Empyrean Armor: 13 | Heals a quarter of its HP and raises the most it has by half for 3 min. 113 MP, 5 min. |

    - **The rule table, natural-templar-v1.** With the monster on it, an open follow-up
      first: Divine Blow, Robust Blow, then Rage when it is at or below 80% HP, then Wrath
      Strike. Of the openers, Dazing Severe Blow, then Ferocious Strike, then Body Smash.
      Empyrean Chastisement last, and only at or below 70% HP, where its shield is worth
      the DP. The weapon swings whenever no skill is ready. Nothing reaches a target that
      is not on it, so it walks in, as the Warrior. The ladder: the shield scroll at 50%
      HP, the life potion at or below 75%, Empyrean Armor in an emergency only (from 35%
      until 45%); the armor's 113 MP are kept back from Rage. It leaves at three
      attackers, or at 25% HP with nothing ready. Between fights it rests as the Warrior:
      the life potion below 90% HP, then sitting. It holds for a patrol and assesses
      (NR-37) with no heal to ask about, and a pull may bring two. In flight it swings
      its weapon from one metre (NR-36): nothing it has by level 12 hurts from range.
    - **Left out, with the reason** (28 skills).

      | Skill, levels | Why it is not cast |
      |---|---|
      | Return, Bandage Heal, Escape: 1 | As for every class (CP-35, CP-Q11). |
      | Herb Treatment and MP Recovery: 10, 15, 20, 25 | A class with no heal rests by the potion plan, which has no powder step. NR-50a. |
      | Shield Bash: 10, 15, 20, 25 | It needs a shield in the left hand, which the skill row and the fight's observation do not say. NR-50b. |
      | Shield Counter: 10, 15, 20, 25; Courageous Shield: 15; Avenging Blow: 22 | A client offers them only after a block or a resist, which the bot does not observe. Java would accept them at any time (above); the bot does not send what a client could not. |
      | Taunt: 10, 15, 20, 25; Provoking Roar: 25 | They raise enmity and deal no damage; alone, the monster is already on the Templar. |
      | Aether Leash: 16 | A pull from 15 m with a 30 s cooldown. A listed attack at range that only cools down is waited for where the class stands (CP-47), which would hold a class that walks in away from its target. The probe rows decide its rule. |
      | Charge: 25 | Run speed for 13 s; the journey's travel casts no skill. |

      Not in the catalog at all: Stubborn Spirit (10, 15, 20, 25), a toggle that needs a
      shield and gives 500 block, a tenth more physical defence and resistances at no
      cost; no rule keeps a toggle on (NR-50d). And the stone's skill 11506, as for every
      class (NR-39).
    - **Proof.** The one-time check, not committed (run/nr/NR-50/check.log): the line
      parses and holds both classes; its character is a Warrior until the client observes
      a Templar; NaturalClassProfiles builds the Templar's profile, which requires the
      validator: each of the 63 active skills has one role or one reason, every follow-up
      has its opener, no rotation line breaks a chain, and the gear groups lie inside the
      masteries. The check prints the 35 rows, the 28 reasons, the best rank of each role
      at levels 10, 13, 16, 20, 25 and 26, the gear table, the kit and the patrol view.
      Gate, set all+mage+warrior+artist+engineer+scout, -Parallel 8, run guard-p8
      (run/nr/NR-50/guard-p8/verdict.json): verdict pass, all twelve scopes identical.
      Seven pre-commit checks pass, the three script tests pass
      (run/nr/NR-50/script-tests.log), Aion.GameServer.Tests passes (4,629 passed, 16
      skipped) and Fast passes (run nr50-fast, 11 passed).
- [x] **NR-50a - A rest with the powder for a class without a heal.** Depends: NR-50
  - Work: Java first: the two powder skills' templates and what they spend, as read for
    NA-18. The potion plan of NaturalRestRules gets a powder step: a class that has
    learned a reagent skill and owns the powder casts Herb Treatment below its HP target
    and MP Recovery below a mana floor the profile names, before the life potion and
    before sitting. The rest skills are named by the profile, with no heal and no
    health-for-mana skill among them. The Templar's eight powder skills get their roles
    and its mana floor is written here. Generic: every second class without a heal
    rests this way.
  - Proof: One-time check of the rest's decisions for a Templar at chosen HP, MP and
    powder counts, and for a Warrior, which has no reagent skill and rests as before; the
    full gate identical.
  - 2026-10-09: done. A class on the potion plan that has learned a powder skill casts it
    first. The Templar's eight powder skills have their roles: 43 of its 63 active skills
    have a role now and 20 are left out.
    - **Java.** The templates (skill_templates.xml): Herb Treatment heals 281, 342, 382
      and 411 HP by rank for one powder; MP Recovery restores 206, 233, 265 and 297 MP at
      once and 31 to 47 more every 3 s for 10 s, for two powder. Both are 4 s casts on a
      shared 16 s cooldown (group 1153). Ranks 1 to 3 (levels 10, 15, 20) spend Lesser
      Odella Powder 169300003 and rank 4 (level 25) Odella Powder 169300004.
      ItemUseAction.canAct 43-49 refuses the cast when the bag holds fewer than the count
      and act 32-40 takes them. The port is the same. No server change.
    - **The change.**
      - Sc/Classes/NaturalRestRules.cs: the potion plan (DecideWithoutHeal) asks the powder
        policy first when the class's rest skills are named and one is learned. A powder
        skill it gives is cast; otherwise the plan goes on as before: the life potion,
        then the sit. NaturalRestSkills may name no heal and no health-for-mana skill;
        ReagentOnly gives the two powder skills alone.
      - Sc/NaturalClericSkills.cs, NaturalPowderRestPolicy: with no heal named there is
        none to fall back on, and with no health-for-mana skill none to start with. The
        Cleric's answers and their wording are unchanged.
      - Sc/Classes/NaturalTemplarProfile.cs: roles herb and mp-recovery for the eight
        skills; the rest names the two powder skills, casts from mana, and has the mana
        floor below.
    - **The Templar's numbers.** Herb Treatment below 90% HP, the plan's HP target. MP
      Recovery from below 25% MP until 50%. Its pool is 840 MP at level 10, 1,050 at 13,
      1,261 at 16, 1,542 at 20 and 1,963 at 26, so a quarter pays for Empyrean Armor (113
      MP) and several Rages at every level, and one MP Recovery brings it from the floor
      to about half. With no powder it sits for mana below the floor.
    - **Proof, the one-time check** (run/nr/NR-50a/check.log; the check file is not
      committed; check-a1.log is a first run that failed on the check's own rounding of
      90% and 50%). Base HP and MP of the level, powder skills of the level.

      | The character | The rest says |
      |---|---|
      | Templar 10, 16, 20 at 60% HP, full MP, lesser powder | Herb Treatment 246, 247, 251 |
      | Templar 25 at 60% HP, Odella powder | Herb Treatment 253 |
      | Templar 25 at 60% HP, only the lesser powder | the life potion |
      | Templar 16 at 91% HP, 26% MP | done |
      | Templar 16 at 95% HP, 20% MP | MP Recovery 250, and the mana rest is on |
      | Templar 16 at 95% HP, 40% MP, mana rest on | MP Recovery 250 |
      | Templar 16 at 95% HP, 51% MP, mana rest on | done |
      | Templar 16 at 95% HP, 20% MP, one powder | sits for mana |
      | Templar 16 at 60% HP, 20% MP | MP Recovery first, the larger deficit; then Herb Treatment, the one not cast last |
      | Templar 16 at 60% HP, the shared cooldown running | the life potion; with its heal still running, sits |
      | Templar 16 at 60% HP, powder ready, the potion's heal running | Herb Treatment 247 |
      | Templar 16 at 60% HP, no powder | the life potion; with none, sits; after 12 sits, blocked |
      | Warrior 9 at 60% HP, powder in the bag or not | the life potion; on its delay, sits; no powder is asked about |
      | Cleric 16 at 60% HP | Herb Treatment 247; cooling down, Healing Light 1841; at 80% HP and 40% MP, Penance 3867 |

    - **Not proven here.** No Templar has rested in a world; the journey's rest loop
      casts a powder skill the same way for any class, and the Cleric's is the one played.
      NR-51 plays it.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout, -Parallel 8, run
      guard-p8 (run/nr/NR-50a/guard-p8/verdict.json): verdict pass, all twelve scopes
      identical, the Cleric's five and the five starters among them. Seven pre-commit
      checks pass, Aion.GameServer.Tests passes (4,629 passed, 16 skipped) and Fast passes
      (run nr50a-fast, 11 passed).
- [x] **NR-50b - A skill's off-hand condition.** Depends: NR-50a
  - Work: Java first: LeftHandCondition, and what the server answers a cast without the
    shield. The skill row carries the template's left-hand condition (SHIELD or DUAL);
    the fight's observation says what the off hand holds; the table refuses such a skill
    without it, by name, like any other refusal. Shield Bash gets its role and its place
    in the Templar's table. Generic: the Assassin's dual-wield skills use the same field.
  - Proof: One-time check: Shield Bash is refused with an empty off hand and is legal
    with a shield worn; the validator accepts the profile; the full gate identical.
  - 2026-10-09: done. A skill row says what its template asks of the left hand, the fight
    observes what the bot holds, and the table refuses the skill without it. Shield Bash
    is in the Templar's table: 47 of its 63 active skills have a role and 16 are left out.
    - **Java.** LeftHandCondition.validate 25-50: type SHIELD asks
      Equipment.isShieldEquipped (504-510: the off-hand item's sub type is SHIELD) and
      answers STR_SKILL_NEED_SHIELD without it; type DUAL asks for a weapon in the off
      hand or a two-hand weapon in the main hand and answers STR_SKILL_NEED_DUAL_WEAPON.
      The port is the same (SkillEngine/Condition/LeftHandCondition.cs). In the data 238
      skills carry the condition, 61 SHIELD and 177 DUAL. Of the skills a class learns by
      itself to level 26, the Templar has 14 (Shield Bash, Shield Counter, Courageous
      Shield, Avenging Blow, Stubborn Spirit) and the Gunner 17, all DUAL, from level 10.
      No server change.
    - **The change.**
      - The skill row (Sc/NaturalPriestCombatPolicy.cs) has RequiredOffHand, read by
        Sc/Classes/NaturalSkillCatalog.cs with the other conditions: SHIELD, DUAL or
        nothing. OffHandHeld gives what the worn gear holds, by the server's own rule: a
        shield in the off hand is SHIELD; a weapon in the off hand, or a two-hand weapon,
        is DUAL.
      - The fight's observation has OffHand; J builds it from the worn items at every
        decision. It is not written into the decision record, so no trace changes.
      - Sc/Classes/NaturalRotationCombatPolicy.cs, Refusals: a skill that asks for what
        the bot does not hold is refused by name ("The skill needs a shield, and none is
        worn."), and so is never chosen and never waited for.
      - Sc/Classes/NaturalTemplarProfile.cs: Shield Bash has the role bash, after Dazing
        Severe Blow and its follow-up and before Ferocious Strike: a 2 s stun once a
        minute for 30 to 49 MP. It is another chain's first step, so it goes only when no
        follow-up is ready, like every opener.
    - **Proof, the one-time check** (run/nr/NR-50b/check.log; the check file is not
      committed). The Templar's profile is accepted with the four ranks of Shield Bash as
      rows that need SHIELD. No other profile has a row with the condition (Priest,
      Cleric, Chanter, Warrior, Mage, Artist, Engineer, Scout). The Gunner's Trunk Shot
      reads DUAL.

      | Worn | The condition is given |
      |---|---|
      | A sword, nothing in the off hand; the same with a shield in the bag; nothing | nothing |
      | A sword and a shield | SHIELD |
      | Two daggers; a greatsword in both hands | DUAL |

      A level-10 Templar on its target with Dazing Severe Blow cooling down:

      | It holds | It casts | Shield Bash |
      |---|---|---|
      | A shield | Shield Bash 3072 | legal |
      | Nothing in the off hand | Ferocious Strike 2865 | refused: the skill needs a shield, and none is worn |
      | A second weapon | Ferocious Strike 2865 | refused the same way |
      | A shield, Shield Bash cooling down | Ferocious Strike 2865 | refused: the cooldown |
      | A shield, 20 MP | Ferocious Strike 2865 | refused: mana |

    - **Not proven here.** No Shield Bash has been cast in a world. NR-51 plays it, with
      and without a shield.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout, -Parallel 8, run
      guard-p8 (run/nr/NR-50b/guard-p8/verdict.json): verdict pass, all twelve scopes
      identical. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629 passed,
      16 skipped) and Fast passes (run nr50b-fast, 11 passed).
- [x] **NR-50c - The shield of a line whose class holds one.** Depends: NR-50b
  - Work: NR-Q15's default. Java first: Equipment.java as read for CP-68 and NR-03, and
    the reward shops as read for NR-38. A line whose second class's table holds a shield
    holds one from the start: its starter's gear rules get that off-hand mode, so the
    Warrior of `warrior-templar` takes the Raider's Shield at Q2100 when its sword is no
    worse (NR-03's reward rule); the line `warrior` is as recorded. Each coin tier buys
    the manifest's shield from the coins left after the armor and the weapon, and wears
    it. No Kinah purchase; no coin added.
  - Proof: One-time check of the pick at Q2100 for both Warrior lines and of each tier's
    purchases for a Templar from a prepared bag; the full gate identical, the warrior
    scope among it.
  - 2026-10-09: done for the line and its pick. The coin tiers are split off as NR-54a
    (rule (q)): a shield is on another trade tab than the armor, the legs' purchases are
    written for one tab, and nothing before the coin-gear leg needs it.
    - **Java.** Equipment.equip 181-213 puts an item into the slot asked for and takes
      out only what that slot holds (getUnequipSlots 215-222: both hands only when a
      two-hand weapon is held), after checkAvailableEquipSkills (85, 323) found the
      mastery; a Warrior has Basic Shield Training (43) from level 1. So a one-hand sword
      and a shield are worn together by two requests. The reward index is NR-32's
      (QuestService.getRewardIndex). No server change.
    - **What the reading changed.** The Work line said the Warrior takes the shield at
      Q2100 "when its sword is no worse". It holds the Training Sword there and Raider's
      Sword is offered beside the shield, so by that rule it would take the sword and
      never see a shield again: Q2100 is the only quest of the route that offers one. The
      class-profile plan foresaw the Templar's own pick at Q2100 (CP-Q10: the Templar
      "makes its own Q2100 pick"; the operator's "Warrior does take weapon" is the
      Warrior line's). So the rule is: a class that holds a shield and owns none takes an
      offered shield before a weapon. NR-Q15 says so now.
    - **The change.**
      - Sc/Classes/NaturalClassProfile.cs, NaturalClassProfiles.For: the starter of a line
        whose second class's gear table holds a shield gets the starter's own profile with
        a shield in its gear rules. The profile and the gear rules are records now, so the
        copy differs in nothing else. Sc/Classes/NaturalGearRules.cs, HoldingShield;
        Sc/Classes/NaturalClassGearTable.cs, All and Of: every class's table by class.
      - Sc/NaturalIshalgenInventoryPolicy.cs, ChooseReward: one key before the others: a
        shield the class can wear, when it holds shields and owns none. A class that holds
        no shield, and one that owns a shield, choose as before.
      - Keep and sell and the equipment check were made for a shield in NR-03.
    - **Proof, the one-time check** (run/nr/NR-50c/check.log; the check file is not
      committed; check-a1.log is a first run that failed on the check's own expectation
      that the Warrior line sells a shield, which is not sellable and is held).

      | Line | Its Warrior's off hand | At Q2100 with the Training Sword | At Q2002 | At Q2134 |
      |---|---|---|---|---|
      | warrior-templar | a shield | choice 5, Raider's Shield 115000024 | choice 2, sword 100000639 | choice 2, sword 100000108 |
      | warrior | nothing | choice 1, Raider's Sword 100000107 | choice 2, sword 100000639 | choice 2, sword 100000108 |

      With a shield already worn, or in the bag, the Templar's Warrior takes Raider's
      Sword at Q2100. After the turn-in its keep-and-sell says equip for the shield and
      the equipment check puts it into the off hand beside the sword; a better sword then
      goes into the main hand and the shield stays. The Warrior line holds a shield it is
      handed and does not wear it. The two Warrior profiles share their fight table,
      skills, rest and help rules; only the gear rules differ, by the shield. No starter
      of any other line holds a shield (the Scout holds its second dagger, NR-04).
    - **Not proven here.** No Warrior has worn a shield in a world. The round that takes
      the line to Altgard plays it (NR-52).
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout, -Parallel 8, run
      guard-p8 (run/nr/NR-50c/guard-p8/verdict.json): verdict pass, all twelve scopes
      identical, the warrior scope among them. Seven pre-commit checks pass,
      Aion.GameServer.Tests passes (4,629 passed, 16 skipped) and Fast passes (run
      nr50c-fast, 11 passed).
- [x] **NR-50d - A toggle kept on.** Depends: NR-50c
  - Work: Java first: how a toggle skill is turned on and off, what it costs while on,
    what ends it, and what the client is told of it (the skill engine's toggle handling
    and the packets it sends). The bot's world state observes which toggles are on. A
    profile names the toggles it keeps on; the journey turns one on when it is observed
    off and its conditions hold, and never casts one that is on. Stubborn Spirit for the
    Templar, with a shield worn. Generic: the Chanter's mantras use the same form.
  - Proof: One-time check, or one probe row on a probe account, that shows the toggle on
    after the rule ran once and not cast again; the full gate identical.
  - 2026-10-09: done by reading, with no code: the Templar's one toggle is a stance that
    any skill cast ends, so its table leaves it off. The form that keeps a toggle on is
    left to the first class that keeps one, the Chanter (NR-70).
    - **Java, a toggle.** It is cast like any skill. When its effect starts,
      Effect.startEffect 670-678 sends the caster SM_SKILL_ACTIVATION(skill, true)
      (activateToggleSkill 694-696) and the effect lasts for the template's toggle timer,
      or with none until it is ended. A client turns it off with
      CM_TOGGLE_SKILL_DEACTIVATE (runImpl 31-42: the effect is removed), not by casting it
      again. Effect.endEffect 735-737 sends SM_SKILL_ACTIVATION(skill, false)
      (deactivateToggleSkill 701-703). A toggle whose slot is NOSHOW is not dispelled
      (EffectController.isNoShowToggle 600-602).
    - **Java, a stance.** A template with stance="true" starts a stance when it is cast
      (Skill.java 624-625; PlayerController.startStance 694-699). StanceObserver 31-49
      ends it (stopStance 701-708 removes the effect) when the player starts to cast any
      skill that is not an item's, a remedy's or a potion's, uses an item that has no
      skill, such as a mount, or is put into a state that forbids stances. The port has
      the same observer (Controllers/Observer/StanceObserver.cs). No server change.
    - **Stubborn Spirit** (3000 to 3003, levels 10, 15, 20, 25) is a toggle and a stance:
      it needs a shield worn, costs nothing, has no timer and an 8 s cooldown, and gives
      500 block, a tenth more physical defence and resistances. The Templar's table casts
      a skill whenever one is ready, every second or two of a fight, and the first cast
      would end the stance. It is worth having only while nothing but the weapon swings.
      So it stays off, and it is in no rule. 17 skills in the data are stances.
    - **Who keeps a toggle.** Of the skills a class learns by itself to level 26: the
      Chanter's four mantras, the Gladiator's Defense Preparation and Slaughter, the
      Assassin's Sprinting and the Rider's Embark, Kinetic Battery and Mounting
      Frustration are toggles and no stances. The Sorcerer, the Spirit Master, the Ranger,
      the Gunner, the Bard and the Cleric have none. The first of them in the order of
      NR-Q1 is the Chanter, whose survey writes the form: the world state observes
      SM_SKILL_ACTIVATION, a profile names the toggles it keeps on, and the journey casts
      one that is observed off and never one that is on.
    - **Proof.** Nothing to check in play: no code changed and no rule names the stance.
      Seven pre-commit checks pass at the head of NR-50c (run/nr/NR-50d/checks.log).
- [x] **NR-51 - Templar: probe rows.** Depends: NR-50a to NR-50d
  - Work: Rows templar-10, templar-16, templar-20 and templar-25 beside the Cleric's in
    SimulationNaturalStarterProbeTests: prepared Templars on the two probe accounts, in
    the gear the route has given by that level, with and without a shield at level 10,
    fight the monsters the Cleric's rows fight. Each row's trace shows the table in
    play: the three chains, Rage and Empyrean Chastisement only when hurt, Shield Bash,
    the ladder, the rest with the powder. What a row shows decides the open rules: the
    leash, the toggle's worth, the mana floor. A fix is one small change (rule (i)).
  - Proof: The four rows end with the monster dead or a recorded retreat, no refused
    cast repeats, and the numbers are written here; the full gate identical.
  - 2026-10-09: done on the second attempt. The four rows pass; the table changed in one
    place: Empyrean Chastisement goes first when the Templar is hurt.
    - **Java.** Nothing new is relied on; what a row met is read below (the chain after
      Rage).
    - **The rows** (SimT/SimulationNaturalStarterProbeTests.cs: templar-10, templar-16,
      templar-20, templar-25, on the probe accounts 98 and 100, at the places and monsters
      of the Cleric's rows). The director makes the Warrior a Templar of the level with
      the skills of every level up to it, puts gear of the route into its bag, and
      places it. From there the journey's equipment check, fight and rest act. New
      director acts: items into the bag, and an MP cut. Run with CP_PROBE_ROWS, two rows
      to a process, the two processes side by side: `bash run/nr/NR-51/probe.sh <attempt>`.
    - **First attempt** (runs nr51-probe-a1 and a1b; logs probe-a1.log, probe-a1b.log).
      Rows 10, 16 and 25 passed. Row 20 failed: in a fight begun at 45% HP with 2,000 DP,
      Empyrean Chastisement was not cast. It stood last in the list, and the starved
      mosbear was dead after the seven skills before it (3057 3132 2867 2880 2905 3038
      2893). The one change (rule (i)): at or below 70% HP it goes before every other
      opener, where its shield takes half of the hits that follow. The level-16 row had
      a second fight that named the monster the Templar had just left; it was walking
      home, and the fight ended at once with nothing done. That was the row's own
      mistake, and the row now has its one fight, as the Cleric's has.
    - **Second attempt** (runs nr51-probe-a2 and a2b; logs probe-a2.log, probe-a2b.log;
      the eight traces are under run/nr/NR-51/).

      | Row | Prepared by the director | What the journey did | Outcome |
      |---|---|---|---|
      | templar-10 | Level 10, the ceremony's sword; then Raider's Shield; then 20 powder and half HP | No shield: Dazing Severe Blow, Ferocious Strike, Robust Blow, Body Smash, then six swings; Shield Bash refused at the first decision ("The skill needs a shield, and none is worn.") and never sent. With the shield, worn by the equipment check: Dazing Severe Blow, Shield Bash, Ferocious Strike, Robust Blow, Body Smash, three swings. Rest from 440 of 881 HP: Herb Treatment once for one powder, one sit, no potion. | An ice crasaur (level 11) killed in 15.4 s, lowest HP 705 of 881; the next in 12.6 s, lowest HP 807. 881 of 881 HP after the rest. |
      | templar-16 | Level 16; the sword of Q24013, the plate shoes and breastplate of Q24011 and Q24012, the shield; 30% HP as the fight begins | The check wore all four. The life potion, then Empyrean Armor at 32% HP in the emergency (489 HP became 1,255 of 2,187), the walk in, Dazing Severe Blow, Divine Blow, Shield Bash. The tusked mosbear's two neighbours came: with three attackers it left. | A retreat, alive at 1,807 of 2,187 HP. The Cleric's row leaves the same spot the same way. |
      | templar-20 | Level 20; the sword of Q24016, the shield, 20 powder; then 2,000 DP and 45% HP; then a tenth of its mana | Unhurt: Dazing Severe Blow, Divine Blow, Shield Bash, Ferocious Strike, Robust Blow 0.8 s later, Wrath Strike 0.7 s after that. Hurt: the life potion, Empyrean Chastisement first at 54% HP, Dazing Severe Blow, Divine Blow, Ferocious Strike, Robust Blow, Rage at 55% HP, Body Smash; DP 7 left. Rest from 156 of 1,562 MP: MP Recovery once for two powder, a sit, the life potion. | A starved mosbear (level 13) killed in 8.3 s, lowest HP 1,750 of 1,852; the next in 13.6 s. After the rest 1,671 of 1,852 HP and 1,049 of 1,562 MP. |
      | templar-25 | Level 25; the same sword and shield, 20 Odella Powder; then three starved mosbears set on it; then half HP | Three fights alone: Dazing Severe Blow, Divine Blow, Shield Bash once (its minute), Ferocious Strike, Robust Blow, Wrath Strike, Body Smash. Against the pack: one swing, then it left, with three attackers. Rest: the fourth rank of Herb Treatment for one Odella Powder, the life potion, a sit. | Three kills in 14.2 s, 16.0 s and 13.6 s; 2,498 HP became 2,371 over the three. One retreat, no death. |

      In no fight was a skill decided more than twice running, and the server answered
      no cast with a missing shield. Every decision carries the table natural-templar-v1.
    - **What the rows decide.**
      - **Empyrean Chastisement** goes first when the Templar is at or below 70% HP
        (Sc/Classes/NaturalTemplarProfile.cs). Unhurt it is not cast.
      - **The mana floor of NR-50a stands.** One MP Recovery brought a tenth of its mana
        to two thirds. The fights cost little: its mana was 13 to 15 lower after a kill
        at levels 20 and 25, where only Shield Bash and Rage cost anything.
      - **The shield** is worth its place: the level-10 kill was 2.8 s shorter with Shield
        Bash, and the lowest HP 807 against 705.
      - **Aether Leash stays out.** Every monster of the rows was walked up to. None of
        them attacks from range; the legs have some, and the round that meets them says
        whether the leash gets a rule. Its reason in the profile stands.
      - **Stubborn Spirit** is NR-50d's: off.
    - **Found, and logged (rule (f)).**
      - **Rage after Empyrean Chastisement ends the chain.** In the hurt fight Rage's
        result came without the chain flag, the chain was gone, and Wrath Strike did not
        follow (trace a2b, templar-20, step s04). Java ends the chain when every effect of
        a step is resisted or dodged (Skill.java 601-609 and 630-639). Rage's shield
        and the veil Empyrean Chastisement leaves on the Templar have one effect id
        (154), so the likely reading is that Rage's shield was refused beside the veil; in the
        Warrior's traces Rage keeps the chain. It costs one Rage and one Wrath Strike for
        each 2,000 DP. No rule yet: the table cannot say "not while an effect is seen"
        for an attack role.
      - **Three attackers.** A level-25 Templar left three level-13 monsters at once, by
        the Warrior's limit. Whether a Templar should stand against more is for the
        rounds to show; the limit is one number of its table.
      - **A monster that is walking home ends a fight at once** (combat-target-returned).
        It is the journey's ordinary give-up and the caller plans again; here it was the
        row that asked for such a monster.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout, -Parallel 8, run
      guard-p8 (run/nr/NR-51/guard-p8/verdict.json): verdict pass, all twelve scopes
      identical. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629 passed,
      16 skipped) and Fast passes (run nr51-fast, 11 passed).
- [x] **NR-52 - Templar: to Altgard.** Depends: NR-51; ticked by the round that gives it
  - Work: A fresh Asimtemplar plays Ishalgen as a Warrior, the trial, the Templar choice
    at Munin, the ceremony with the sword and the dispatch Q2901, and is captured at the
    Altgard bind as altgard-templar-s1.
  - Proof: The capture verifies.
  - 2026-10-09: two attempts stopped, each at a place no line but the Priest's had come
    to; with both mended a replay reached the Altgard bind. Not captured yet: a capture is
    made from committed code, which NR-52a commits.
    - **Attempt 1** (capture run nr52-capture-a1; nothing captured; evidence
      run/nr/NR-52/capture-a1/). Ishalgen as a Warrior, with Raider's Shield from Q2100,
      to the Ascension trial at 1 h 10 min, level 9. It stopped at the trial's first
      opponent, 3.9 m away: "Map, position, or survival state changed during navigation."
      The journey fights the trial in Ataxiar (320020000) with the fight it made for
      Ishalgen, whose navigator and map are Ishalgen's. The Priest casts from where it
      stands and never walks there. The one change (rule (i)): a fight that must go to
      its target on a map that is not its own asks the journey for the map the client is
      on (J.Combat, EnterObservedMap; J, FightAscensionTrialAsync). A fight that never
      walks never asks, so the Priest's trial is as recorded.
    - **Attempt 2** (replay pilot-a2, with that change; evidence run/nr/NR-52/pilot-a2/).
      The trial's five opponents, the Templar choice, the ceremony with the sword, and
      the ten quests of the capital pass, to level 11 at 1 h 25 min. It stopped at the
      pass's end check, which looked for the Karmic Staff among what was worn before the
      pass. A second failure, so by rule (i) it is NR-52a.
    - **Replay pilot-a3** (both changes; run/nr/NR-52/pilot-a3/): reached and verified.
      The early Ascension ended at 1 h 25 min with no death; the bridge ended at 3 h 28
      min: a level-14 Templar bound at the Altgard obelisk, 55 quests complete, 128,650
      records, 149 fights, two retreats, no death. It wears the ceremony's sword, Raider's
      Shield, chain on four slots, two rings and a necklace. Supplied: 30 life potions,
      30 shield scrolls, 20 running scrolls, 8 DP jelly and 200 powder, and no mana
      serum (NR-Q13). In the trial the fight entered the observed map once and killed
      the first four opponents in 15 s.
    - **Found, and logged (rule (f)).** In the trial Body Smash was decided three times
      running against the fifth opponent (1:10:56 to 1:10:58), which is what a cast the
      server did not carry out looks like. The fight went on and won. Not followed here.
  - 2026-10-09: done. altgard-templar-s1 is captured from committed code, and the capture
    verified its endpoint. The script's Verify action has no form for a bridge snapshot.
    - **The capture** (run nr52-capture-a3 at c86110252; sim-snapshot.ps1 -Action Capture
      -Bridge -Class warrior-templar, as the accepted line's `altgard` was made; log
      run/nr/NR-52/capture-a3.log, evidence run/snapshots/_capture/nr52-capture-a3).
      Character 133266, class line warrior-templar, 12,489,286 ms of game time
      (3 h 28 min), dump sha256 d3520144b20bf26f. A level-14 Templar,
      alive, bound at the Altgard obelisk, 55 quests complete, the ceremony's sword
      in the main hand and Raider's Shield in the off hand.
    - **Proof.** The capture dumps only a verified bridge endpoint: its
      bridge-completion.json says verified, for this character, and is kept in the
      snapshot. That is what the accepted line's `altgard` rests on too.
    - **Found, and logged (rule (f)): Verify has no form for a bridge snapshot.**
      sim-snapshot.ps1 -Action Verify -Name altgard-templar-s1 (run nr52-verify-a3,
      run/nr/NR-52/verify-a3/) restored a fresh copy and failed in 29 s: "Natural
      journey expected 9, observed 14." A restore gives a plain bridge snapshot no
      NA_ASCENSION, so the resumed run checks the Munin stop of level 9. It is so for
      every line, the accepted one's `altgard` included, which was never asked. The
      snapshot is what the next leg starts from, and that leg checks its start (NR-53).
      The script is not changed here: the restore environment of `altgard` is what the
      l1 scope of the gate starts on.
    - **Next for the Templar.** It is the pilot (In rounds, step 2): its legs are played
      from this snapshot one after another, each captured with -LaterCapital as the
      accepted line's altgard-rc-l1 and the rest were, before the other classes' rounds.
- [x] **NR-52a - The capital pass's end check names the line's ceremony weapon.**
  Depends: NR-51
  - Work: The second stop of NR-52. The check at the end of the capital pass asks that
    the weapon taken at the ceremony is still worn, and named the Karmic Staff
    101500498. It asks for the line's own pick now, from the line's bridge. Java first:
    nothing of the server is relied on; this is the bot's own check.
  - Proof: The Templar's line passes the capital pass and the bridge in a replay; the
    full gate identical.
  - 2026-10-09: done. This commit also carries NR-52's own one change, the fight that
    enters the observed map: the two are in one file, and the capture NR-52 asks for is
    made from committed code.
    - **The change.** J, VerifyCapitalPass: the worn weapon it looks for is
      LineBridge().CeremonyReward.ItemId, which is 101500498 for the accepted line and
      100000640 for the Templar's. J.Combat: EnterObservedMap, asked at the approach
      step when the client's map is not the fight's; the journey sets it for the trial
      and clears it after.
    - **Proof.** Replay pilot-a3 (run/nr/NR-52/pilot-a3/replay.json: passed, class line
      warrior-templar): the numbers are under NR-52. Gate, set
      all+mage+warrior+artist+engineer+scout, -Parallel 8, run guard-p8
      (run/nr/NR-52a/guard-p8/verdict.json): verdict pass, all twelve scopes identical,
      the bridge scope b with the Priest's trial and the capital pass among them. Seven
      pre-commit checks pass, Aion.GameServer.Tests passes (4,629 passed, 16 skipped) and
      Fast passes (run nr52a-fast, 11 passed).
- [x] **NR-53 - Templar: Altgard legs l1 to l5.** Depends: NR-52; ticked by its round
  - Work: The Cleric's legs in order, each end captured under the Cleric's snapshot name
    with -templar (NR-41). In leg 1 it swings at the fungus in flight.
  - Proof: Each capture verifies.
  - 2026-10-09, leg l1: the first attempt stopped at the kills in flight. With NR-53a a
    replay played the leg to its end. Not captured yet: the capture is made from
    committed code.
    - **Attempt 1** (capture run nr53-l1-a1 from altgard-templar-s1 with -LaterCapital;
      nothing captured; evidence run/nr/NR-53/l1-a1/). The leg went as the Cleric's to
      the fungus of Q24011 (step af-037, 36 min in, level 15). The Templar flew to the
      first fungus and swung 36 times, then to the second and swung 36 times: 72 swings
      sent, none carried out, the quest's count unmoved, a whole flight time spent on
      each. At the third: "No hover point in sight of fungus 69377". NR-36 had said it:
      the swing in flight is first played when a Warrior's line reaches leg 1.
    - **Replay l1-a2** (with NR-53a; run/nr/NR-53/l1-a2/): the leg complete and
      verified. 66 quests complete, level 15, no death, 42 min 29 s of game time, 29,438
      records, 43 fights. Five fungus in two sorties, three swings and 4.5 s each: 15
      swings sent, 15 carried out. The Cleric's probe (AF-06) measured two Smites and
      5.7 s a fungus.
  - 2026-10-09, legs l1 to l3 captured; leg l4 stopped, and with NR-53b a replay played it
    to its end. Legs l4 and l5 are captured after NR-53b is committed.
    - **Captured** from the committed code at 23cfb741b, each with -LaterCapital from the
      capture before it (runs nr53-l1-a2, nr53-l2-a2, nr53-l3-a2; logs
      run/nr/NR-53/l1-a2.log and so on; character 133266 of class line warrior-templar):

      | Snapshot | From | The leg | At its end | Dump sha256 |
      |---|---|---|---|---|
      | altgard-rc-l1-templar | altgard-templar-s1 | 42 min 29 s, no death | level 15, 66 quests, 15,058,334 ms of game time | f0fde6d9cd358771 |
      | altgard-rc-l2-templar | altgard-rc-l1-templar | 1 h 07 min, no death | level 16, 76 quests, 19,116,099 ms | 3b2a43491589a56d |
      | altgard-rc-l3-templar | altgard-rc-l2-templar | 18 min 26 s, no death | level 17, 81 quests, 20,242,291 ms | a609a6acbe92b203 |

      Each capture dumps only a verified leg endpoint (altgard-l<n>-completion.json in the
      snapshot), and each next leg started from it and checked its start.
    - **Leg l4, attempt 1** (capture run nr53-l4-a2 from altgard-rc-l3-templar; nothing
      captured; evidence run/nr/NR-53/l4-a2/). No quest, item or level progress for an
      hour of game time. Q2225's kills were made; at Q2224 the Templar walked into the
      mosbears' packs. Of 49 fights 14 were kills. The walk to the target was refused 22
      times ("No collision-checked route": the target stands in its neighbours' circles,
      and a pack of two or more is not accepted), and it left 13 fights at three or more
      attackers. One death. By rule (i) the mend is NR-53b: it is the class's way of
      opening a fight, not a small change.
  - 2026-10-09, leg l4 captured; leg l5 stopped, and with NR-53c a replay played it to
    its end.
    - **Captured** at 3beac1cb0: altgard-rc-l4-templar from altgard-rc-l3-templar with
      -LaterCapital (run nr53-l4-c1, log run/nr/NR-53/l4-c1.log): the leg in 2 h 50 min
      with two deaths; level 20, 95 quests, 30,502,320 ms of game time, dump sha256
      ce75a2dcc6d163c3.
    - **Leg l5, attempt 1** (capture run nr53-l5-c1 from altgard-rc-l4-templar; nothing
      captured; evidence run/nr/NR-53/l5-c1/). An hour into the leg, at Q24231's grave
      robbers, a fencer at 4% HP ran 10 m off. The Templar decided to close in 971 times
      and did not move: "1,000 actions without a kill". Its walk went to the nearest
      monster of the target's kind, which was another fencer beside it, and arrived
      there at once every time. NR-53c.
  - 2026-10-09: done. Leg l5 is captured, and with it the five legs.
    - **Captured** at dd96cca3d: altgard-rc-l5-templar from altgard-rc-l4-templar with
      -LaterCapital (run nr53-l5-c2, log run/nr/NR-53/l5-c2.log): the leg in 2 h
      29 min with 1 death(s); level 21, 109 quests,
      39,518,001 ms of game time (10 h 58 min since
      creation), dump sha256 4bfd0027e9a01df7.
    - **The five legs.** altgard-rc-l1-templar to altgard-rc-l5-templar, each from the
      one before. Three stops, each a lettered item: NR-53a (the swing in flight), NR-53b
      (the pull with Taunt) and NR-53c (the walk to its own target). Each capture dumps
      only a verified leg endpoint, and each next leg checked its start.
- [x] **NR-53a - A swing in flight hovers inside the swing's reach.** Depends: NR-52
  - Work: The first stop of NR-53. Java first: what the server asks of a swing's
    distance. The air attack of a class with no skill for the air is its weapon's swing
    (NR-36); its reach and its hover point are made what the server accepts.
  - Proof: The Templar's leg 1 is played to its end in a replay, with every swing in
    flight carried out; the full gate identical, the l1 scope with the Cleric's Smite
    among it.
  - 2026-10-09: done.
    - **Java.** PlayerController.attackTarget 399-411: a swing is answered
      TARGET_TOO_FAR_AWAY unless the target is within the weapon's attack range and one
      metre more ("client allows attacking from +0.9 meters further away"), by
      PositionUtil.isInAttackRange 275-287, which measures in three dimensions from bound
      radius to bound radius (isInRange 243-251). Then it asks for sight.
      PlayerRestrictions.canAttack 206-237 forbids a swing on a flight path or a
      windstream (checkFly 42-49), and not in free flight. The port has the same lines
      (Controllers/PlayerController.cs 397-402). No server change.
    - **What was wrong.** NR-36 gave a swing the weapon's range alone, 1.5 m for a sword,
      and so a hover at 1 m. And the hover search looks at the fungus's own height and 6
      m and 12 m above and below it, which Smite's 25 m covers. Coming down from the
      cruise height the nearest of those points is the one 12 m above the fungus, and
      the Templar swung from there. The server refused every swing for distance. The bot
      has no decoder for that answer (SM_ATTACK_RESPONSE), so it saw only that nothing
      was carried out.
    - **The change** (Sc/NaturalAirCombat.cs). A swing's reach is the weapon's range and
      the server's metre: 2.5 m for a sword, and a hover at 2 m. The two bound radii the
      server adds are left as margin. FindHover takes the attack's reach: a point farther
      from the fungus than the reach less a quarter metre is no hover point, and a hover
      nearer than 6 m also looks half its own distance above and below. A fungus no
      hover point reaches is left for another while another is in view
      (air-combat-out-of-reach); with no other in view the run stops as before. Smite's
      25 m keeps every point it had, so the Cleric hovers as recorded.
    - **Found, and logged (rule (f)).** A refused swing is silent to the bot: it does not
      decode SM_ATTACK_RESPONSE. A fight on the ground would wait out the same refusal
      without a word in the trace.
    - **Proof.** Replay l1-a2, class line warrior-templar, from altgard-templar-s1
      (run/nr/NR-53/l1-a2/replay.json: passed): the numbers are under NR-53. Gate, set
      all+mage+warrior+artist+engineer+scout, -Parallel 8, run guard-p8
      (run/nr/NR-53a/guard-p8/verdict.json): verdict pass, all twelve scopes identical,
      l1 with its five fungus among them. Seven pre-commit checks pass,
      Aion.GameServer.Tests passes (4,629 passed, 16 skipped) and Fast passes (run
      nr53a-fast, 11 passed).
- [x] **NR-53b - The Templar pulls from range with Taunt.** Depends: NR-53a
  - Work: The stop of leg l4. Java first: what Taunt does to a monster and what a drag
    tells the client. The Templar opens a fight as a tank does: Taunt from range, and the
    monster comes. The distances of its profile are those of a 15 m skill. A pull is no
    attack: the rule table says so, generic for every class that pulls.
  - Proof: The four probe rows pass with the pull; the Templar's leg l4 is played to its
    end in a replay; the full gate identical.
  - 2026-10-09: done, after two forms that did not hold (the leash, and a pull that was
    waited for without end).
    - **Java.** Taunt's effects are hostileup and targetchange (skill_templates.xml,
      2981): enmity, and no damage; a monster given enmity fights. Aether Leash's drag is
      its sub-skill 8441, effect pulled: PulledEffect.calculate 33-53 puts the target 1.5 m
      from the caster and startEffect 56-72 moves it there, and sends SM_FORCED_MOVE only
      when the one dragged is a player. For a monster the place is told in the cast's
      result alone. No server change.
    - **The change.**
      - Sc/Classes/NaturalTemplarProfile.cs: Taunt has the role taunt and is the one
        role at range. The pull style is the stand-off, not the walk-in. Ranges of its
        own: a planned pull opens at 13 m; a far target is approached to 10 m with sight
        of it; the stand-off is 11 m (15 m less the arrival tolerance and the margin),
        and the approach must end inside it, or no point of its route is one to cast
        from (the first probe attempt stopped on a 12 m approach).
      - Sc/Classes/NaturalRotationCombatPolicy.cs: PullRoles, a new part of the rule
        table. A pull role is cast once in a fight and waited for while it cools down,
        which is the time the target has to come. After that, or as soon as the bot is
        under attack or the target is hurt, it is left out of the list: a target that
        does not come is gone to. The fight's observation carries what the fight has
        cast (J.Combat). No table of a recorded class names a pull role.
      - Aether Leash is left out again, with its reason in the profile: the bot does not
        read a dragged monster's new place from the cast's result. It took the monster to
        be where it was, walked there, and its skills were refused for distance
        (STR_SKILL_NOT_ENOUGH_DISTANCE) while it believed it stood on the target.
      - SimT, the Templar's probe rows: the level-20 row no longer asks that Rage is
        cast in the hurt fight, which now ends before Rage's place in the chain.
    - **The forms that did not hold** (replays of leg l4 from altgard-rc-l3-templar;
      evidence run/nr/NR-53/l4-a3/ and run/nr/NR-53b/l4-a4/, l4-a5/):
      - **l4-a3, Aether Leash before Taunt.** Q2224 was finished in 18 min. At Q2227's
        lake spirits the leash dragged the spirit to the Templar and the Templar walked
        to where the spirit had been; Dazing Severe Blow was refused for distance seven
        times a fight. "NPC 210660 was not killed in 6 non-retreat attempts."
      - **l4-a4, Taunt alone.** 179 kills, level 19, 94 quests in 3 h 11 min. Then a
        grave robbing sentry at 1% HP ran 15 m off and the Templar stood taunting it
        every 10 s: 1,000 actions without a kill.
      - **l4-a5, a pull left out once the fight is on.** The same place: the sentry was
        a new fight with nothing known of its HP and no attack, so the pull was still
        waited for. Hence "cast once in a fight".
    - **Proof, the replay** (l4-a6; run/nr/NR-53b/l4-a6/replay.json: passed). Leg l4
      complete and verified: level 20, 95 quests, 2 h 50 min of game time, 104,262
      records. 270 fights, 212 kills, 30 retreats, 36 approaches refused, two deaths with
      their soul heals. 91 life potions drunk in fights. The Cleric's recorded leg is
      112,256 records.
    - **Proof, the probe rows** (runs nr53b-probe-a3 and a3b; logs
      run/nr/NR-53b/probe-a3.log, probe-a3b.log; a1 and a2 are the earlier forms). All
      four pass. Every fight opens with Taunt from inside 15 m and the monster comes:
      an ice crasaur dead in 14.8 s at level 10; a starved mosbear in 9.2 s at level 20
      and in 14.3 to 14.8 s at level 25, where the walk-in took 13.6 to 16.0 s.
    - **Found, and logged (rule (f)).**
      - **A leg does not wear its rewards.** After leg l4 the Templar is level 20 with
        the ceremony's level-10 sword in its hand and the Altgard Legionary Sword,
        Sabatons and Breastplate of Q24013, Q24011 and Q24012 (level 16) in its bag. The
        recorded Cleric does the same: its leg l4 begins and ends at level 19 with the
        Karmic Staff. The equipment check runs at a leg's town-service stops and
        between legs of the continuous journey, not after a turn-in. It is how the legs
        are; a class that fights with its weapon pays more for it than a caster.
      - **The leash.** Reading a dragged monster's place from SM_CASTSPELL_RESULT would
        give the Templar its second pull, which is the one that brings a monster that
        attacks from range.
      - **Thirty retreats and two deaths in one leg.** The Templar leaves at three
        attackers and the mosbears come in threes. Whether it should stand against three
        is the swarm limit of its table; the rounds will say.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout, -Parallel 8, run
      guard-p8 (run/nr/NR-53b/guard-p8/verdict.json): verdict pass, all twelve scopes
      identical. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629 passed,
      16 skipped) and Fast passes (run nr53b-fast, 11 passed).
- [x] **NR-53c - A class that walks up walks to its own target.** Depends: NR-53b
  - Work: The stop of leg l5. Java first: nothing of the server is relied on. When the
    fight has the bot walk up, a walk-in class walks to the monster it fights, and the
    Priest line walks to the nearest monster of that kind, as recorded. Which of the
    two a class does is said by its movement rules; the Templar, which pulls from range
    and fights at melee, walks to its own target.
  - Proof: The Templar's leg l5 is played to its end in a replay; the full gate
    identical.
  - 2026-10-09: done.
    - **The change.** Sc/Classes/NaturalFightMovement.cs: WalksToItsTarget, with
      GoesToItsTarget for the fight loop; not given, it is the walk-in style, so every
      profile that has played answers as before. J.Combat, the approach step: the two
      places that asked for the walk-in style ask the movement rules. The Templar's
      movement names it.
    - **Proof, the replay** (l5-a2 from altgard-rc-l4-templar;
      run/nr/NR-53c/l5-a2/replay.json: passed). Leg l5 complete and verified: level 21,
      109 quests, 2 h 30 min of game time, 106,309 records. 205 fights, 184 kills, 18
      retreats, five approaches refused, one death.
    - **Found, and logged (rule (f)).** At level 21 the Templar still holds the
      ceremony's level-10 sword: leg l5 has no stop that wears what is in the bag
      either (NR-53b). The legs after it are watched for the first that does.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout, -Parallel 8, run
      guard-p8 (run/nr/NR-53c/guard-p8/verdict.json): verdict pass, all twelve scopes
      identical. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629 passed,
      16 skipped) and Fast passes (run nr53c-fast, 11 passed).
- [x] **NR-54 - Templar: Altgard legs l6 to l11.** Depends: NR-53; ticked by its round
  - Work: As NR-53. Leg 11 is the destiny quest: its stone is 140000003 (NR-39).
  - Proof: Each capture verifies.
  - 2026-10-09, legs l6 to l8 captured; leg l9 stopped, and with NR-54b a replay played it
    to its end.
    - **Captured** from the committed code at dd96cca3d, each with -LaterCapital from the
      capture before it (runs nr54-l6-a1, nr54-l7-a1, nr54-l8-a1; logs
      run/nr/NR-54/l6-a1.log and so on; character 133266):

      | Snapshot | From | The leg | At its end | Dump sha256 |
      |---|---|---|---|---|
      | altgard-rc-l6-templar | altgard-rc-l5-templar | 2 h 02 min, no death | level 21, 119 quests, 46,882,001 ms of game time | 64f2165f563bda5c |
      | altgard-rc-l7-templar | altgard-rc-l6-templar | 47 min, no death | level 22, 135 quests, 49,727,001 ms | aacce3a374a5fb7b |
      | altgard-rc-l8-templar | altgard-rc-l7-templar | 24 min, no death | level 22, 142 quests, 51,189,226 ms | e20c2b9790429446 |

    - **Leg l9, attempt 1** (capture run nr54-l9-a1 from altgard-rc-l8-templar; nothing
      captured; evidence run/nr/NR-54/l9-a1/). At the robe's clothing, far from the hub,
      a walk was blocked and the journey asked for Return, which had 10 min 49 s of its
      cooldown left. While it waited, monsters came three at a time; the Templar
      retreated, was cornered and died, and was revived at its bind in the hub. The
      wait then ended and Return was cast where it stood: "Return completed but did not
      move the Warrior out of the checked-route pocket." NR-54b.
  - 2026-10-09, leg l9 captured; leg l10 stopped twice.
    - **Captured** at 13b96c669: altgard-rc-l9-templar from altgard-rc-l8-templar with
      -LaterCapital (run nr54-l9-a2, log run/nr/NR-54/l9-a2.log): the leg in 1 h 04 min
      with one death; level 23, 152 quests, 55,073,001 ms of game time, dump sha256
      01c564e199cbfdc8.
    - **Leg l10, attempt 1** (capture run nr54-l10-a2 from altgard-rc-l9-templar; nothing
      captured; evidence run/nr/NR-54/l10-a2/). On the road to Q2273's monsters a
      patrolling lycan blocked the route and was not taken as a blocker. The Templar
      then walked round the camp for 41 minutes, 57 routes and 994 segments, and the run
      ended with "no quest, quest-item or level progress for 01:00:00". NR-54c.
    - **Leg l10, attempt 2** (replay l10-a3 with NR-54c's change; evidence
      run/nr/NR-54c/l10-a3/). Q2273 is done. At Q2277 a rest was interrupted by two
      monsters, the first of which shot from 7 m away inside its neighbours' circles;
      the walk to it was refused and the refusal ended the run. NR-54d.
  - 2026-10-09, leg l10 played to its end in a replay, after three more stops.
    - **The stops**, each a lettered item: at Q2282 a stagger landed the Templar on a
      face no route starts from (NR-54e); at Q24014's collect step the travel defence
      could not walk to its attacker (NR-54d, with the stop at Q2277); at Bregirun's
      door the journey asked for the Cleric's rebirth (NR-54f).
    - **Replay l10-a8** (the code of the three items, from altgard-rc-l9-templar;
      run/nr/NR-54e/l10-a8/, replay.json: passed). Leg l10 complete and verified:
      level 24, 162 quests, 3 h 50 min of game time, no death.
  - 2026-10-09, leg l10 captured; leg l11 stopped, and with NR-54g a replay played it
    to its end.
    - **Captured** at 77b893022: altgard-rc-l10-templar from altgard-rc-l9-templar with
      -LaterCapital (run nr54-l10-a3, log run/nr/NR-54/l10-a3.log): the leg in 3 h 50 min
      with no death; level 24, 162 quests, 68,874,001 ms of game time, dump sha256
      510d5c982adc856a.
    - **Leg l11, attempt 1** (capture run nr54-l11-a3 from altgard-rc-l10-templar;
      nothing captured; evidence run/nr/NR-54/l11-a3/). Ten minutes into the leg, at
      Heimdall: "q2900-stone did not hand over item 140000001". Heimdall hands the
      Templar 140000003. NR-54g.
  - 2026-10-09: done. Leg l11 is captured, and with it the six legs.
    - **Captured** at 1c3cb0a62: altgard-rc-l11-templar from altgard-rc-l10-templar with
      -LaterCapital (run nr54-l11-a4, log run/nr/NR-54/l11-a4.log): the leg in 12 min
      35 s with no death; level 24, 164 quests, 69,649,485 ms of game time (19 h 20 min
      since creation), dump sha256 cb443b3025abdea2.
    - **The six legs.** altgard-rc-l6-templar to altgard-rc-l11-templar, each from the
      one before. Six stops, each a lettered item: NR-54b (Return after a bind revive),
      NR-54c (the road blocker), NR-54d (a defence that cannot walk to its attacker),
      NR-54e (a forced landing on a steep face), NR-54f (the rebirth before Bregirun)
      and NR-54g (the destiny quest's stone). Each capture dumps only a verified leg
      endpoint, and each next leg checked its start. Seven pre-commit checks pass
      (run/nr/NR-54/checks-run.log).
- [x] **NR-54b - A bind revive while Return cools down replaces the cast.** Depends: NR-53c
  - Work: The stop of leg l9. Java first: nothing new; Return goes to the bind point and
    a bind revive stands there (as read for NA-27). The journey already lets a death at
    the moment of the cast take Return's place. A fight lost during the wait for
    Return's cooldown is revived inside the defence, and the cast went on after it.
  - Proof: The Templar's leg l9 is played to its end in a replay; the full gate
    identical.
  - 2026-10-09: done.
    - **The change.** J, UseLearnedReturnToBindAsync: it counts the fight's bind revives
      as it starts. After the cooldown wait, and before each attempt at the cast, a
      count that has risen ends the helper: the character is at its bind, which is where
      Return goes (natural-return-replaced-by-bind-revive). A run with no death in the
      wait is as before.
    - **Proof, the replay** (l9-a2 from altgard-rc-l8-templar; evidence
      run/nr/NR-54b/l9-a2/, replay.json: passed; its receipt carries the label NR-54a,
      given before this item had its letter). Leg l9 complete and verified: level 23,
      152 quests, 1 h 04 min of game time, 38,102 records, 96 fights, six retreats, one
      death, and Return replaced by the bind revive once.
    - **Found, and logged (rule (f)).**
      - **Eleven minutes of waiting in a monster's field.** The wait for Return's
        cooldown is spent where the walk was blocked. It is the Cleric's rule too; the
        Templar pays for it with a death here.
      - **57 of 96 fights of the leg ended with the approach refused**, 33 with a kill.
        The leg still ends; what refuses so many approaches is not looked into here.
      - **Still the ceremony's sword at level 23.** Legs l6 to l9 have no stop that
        wears what is in the bag (NR-53b).
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout, -Parallel 8, run
      guard-p8 (run/nr/NR-54b/guard-p8/verdict.json): verdict pass, all twelve scopes
      identical. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629 passed,
      16 skipped) and Fast passes (run nr54b-fast, 11 passed).
- [x] **NR-54c - The Templar's fight-through takes a blocker as every line does.** Depends: NR-54b
  - Work: The first stop of leg l10. Java first: nothing of the server is relied on. The
    fight-through's firing range says which of a route's blockers is taken: the one whose
    place is within that range of the route's last point outside its circle. NR-53b gave
    the Templar 14 m there, the reach of its pull. A patrol blocks the stretch of its
    path it can walk to, so that last point can be farther from the patrol than the pull
    reaches, and the blocker was never taken. The pull is planned after the blocker is
    chosen, with the pull's own distances.
  - Proof: The Templar's leg l10 passes Q2273 in a replay; the full gate identical.
  - 2026-10-09: done.
    - **The change.** Sc/Classes/NaturalTemplarProfile.cs: the firing range of its
      engage ranges is NaturalFightThrough.FiringRange (23 m), the one every line that
      has played uses. The pull's distances stay: spell range 14 m, pull at 13 m.
    - **Proof, the replay** (l10-a3 from altgard-rc-l9-templar; evidence
      run/nr/NR-54c/l10-a3/). Where attempt 1 was refused the patrol (210463 on the road
      at 1757, 2197), the replay took it and went on: 90 fight-through plans, 46
      blockers cleared, no replan for a hostile. Q2273 was reported after 55 minutes of
      the leg. The replay did not end the leg: it stopped at Q2277 after 1 h 28 min
      (45,031 records, 131 fights, 76 kills, 38 fights ended with the approach
      refused), which is NR-54d.
    - **Found, and logged (rule (f)).** After the first refusal the navigator walked an
      avoidance loop for 41 minutes before it gave up: its rule counts returns to the
      same route start within 0.5 m, and the loop came back to one only every eight to
      twelve minutes. It is shared code and every line has it.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout, -Parallel 8, run
      guard-p8 (run/nr/NR-54c/guard-p8/verdict.json): verdict pass, all twelve scopes
      identical. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629 passed,
      16 skipped) and Fast passes (run nr54c-fast, 11 passed).
- [x] **NR-54d - A defence whose walk to an attacker is refused goes on.** Depends: NR-54c
  - Work: The second stop of leg l10, and its fourth. Java first: nothing of the server
    is relied on. A monster that shoots from inside its neighbours' circles cannot be
    walked to: the fight refuses the walk when the pack would bring it to the swarm
    limit. In a defence that refusal ended the run, outside Haramel. A refused attacker
    is passed over and the next one is fought; the walk or the rest that was interrupted
    then goes on and observes again.
  - Proof: Both stops are passed in replays of the Templar's leg l10; the full gate
    identical.
  - 2026-10-09: done. The item was written for the rest's defence; the fourth stop was
    the same refusal in the travel defence, and it is one item.
    - **The change.** J, the navigator's defence (DefendOnAttackAsync), which also
      answers an interrupted rest first: a refused walk is traced
      (navigation-defense-approach-blocked) and the next attacker is taken. Haramel's
      own answer, which ends the defence there, stays. J.Combat, DefendDuringRestAsync:
      a refused attacker is set aside and the nearest one that can be fought is fought;
      after that fight the list is asked again. When none can be reached they get 2 s
      to come, three times, and then the rest observes again
      (rest-defend-approach-blocked).
    - **Proof, the replays** (each from altgard-rc-l9-templar).
      - l10-a4 (run/nr/NR-54d/l10-a4/, with the rest's defence alone). The stop at
        Q2277 is passed, and Q2277, Q2280 and Q2281 are done. Four refusals were
        answered in rests; in the last of them the monster had come by the third ask
        and was killed. The replay stopped at Q2282 after 2 h 45 min (71,589 records,
        213 fights, 130 kills): NR-54e.
      - l10-a6 (run/nr/NR-54d/l10-a6/, with the travel defence's change and the first
        form of NR-54e). The stop at Q24014's collect step is passed. Two refusals were
        passed over in the travel defence: 210551 at Q2277, whose neighbour was fought,
        and 210751 at the collect step. The replay stopped at Bregirun's door after
        4 h 29 min (118,821 records): NR-54f.
      - l10-a8 (run/nr/NR-54e/l10-a8/, the committed code of the three items): the leg
        is played to its end, with one refusal passed over.
    - **Proof.** One bundle ran on the tree that holds NR-54d, NR-54e and NR-54f
      together; the three are committed one after the other from that tree. Gate, set
      all+mage+warrior+artist+engineer+scout, -Parallel 8, run guard-p8
      (run/nr/NR-54f/guard-p8/verdict.json): verdict pass, all twelve scopes identical.
      Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629 passed, 16
      skipped) and Fast passes (run nr54f-fast2, 11 passed).
- [x] **NR-54e - A forced landing on a face too steep to leave stands beside it.** Depends: NR-54d
  - Work: The third stop of leg l10. Java first: StaggerEffect.calculate (63-68) lands
    a staggered character 2 m back, at GeoService.getClosestCollision; the ground there
    is GeoMap.getZ without the slope rule (GeoMap.getClosestCollision, 137-153). The
    port has the same (StaggerEffect.cs 56-60). The bot's edge check asks for the
    ground under its start with the slope rule. From a landing on a face steeper than
    45 degrees it found none and refused every edge, so no route started where the
    server had put the character.
  - Proof: The Templar's leg l10 is played to its end in a replay; the full gate
    identical.
  - 2026-10-09: done.
    - **The stop** (replay l10-a4, run/nr/NR-54d/l10-a4/). In Q2282's camp a monster's
      stagger (skill 16606) moved the Templar to (2414.108, 2171.982, 269.156) on map
      220030000. Every route from there ended GeometryRejected: the walk to the monster
      3 m away, the walk to the next pull. Six tries later the hunt gave up: "NPC
      210538 was not killed in 6 non-retreat attempts".
    - **First form, taken back.** The edge check itself was let start from the surface
      that is there. Replay l10-a5 (run/nr/NR-54e/l10-a5/) passed the stop and the gate
      stayed identical, but Fast failed
      (run/nr/NR-54e/bundle-broad/): Q2006MauReturnObstacleCannotBePulledFromTheFarSide
      holds that no route starts from a pocket of the Ishalgen journey, and with the
      change one did. The check is as it was.
    - **The change.** J, the journey's answer to a forced landing
      (ResolveForcedLanding), which already stands a landing off the mesh on the mesh's
      ground within 3 m, as the client does. A landing on the mesh, within 0.5 m of it,
      was left where it was. When the geometry has no ground under it by the slope
      rule, the bot now stands on that mesh point beside it
      (forced-landing-on-ground).
    - **Proof, the tool** (tools/Aion.NavBake points, run/nr/NR-54e/points-beside.log).
      From the landing: the ground with the slope rule is NaN, without it 269.15564,
      and four destinations have no route. From the mesh point 0.10 m beside it
      (2414.093, 2172.085, 269.370): 25, 31 and 26 checked points to three of them.
    - **Proof, the replay** (l10-a8 from altgard-rc-l9-templar, the committed code of
      the three items; evidence run/nr/NR-54e/l10-a8/, replay.json: passed). The same
      stagger at 02:38:07 is stood beside, and the fight goes on. Leg l10 complete and
      verified: level 24, 162 quests, 3 h 50 min of game time, no death, 102,765
      records, 231 fights, 171 kills, 40 fights ended with the approach refused, six
      retreats.
    - **Found, and logged (rule (f)).**
      - **Fifty staggers in the leg.** A class that fights at arm's length takes every
        such skill; three of the landings were stood on other ground.
      - **The leg is long for the Templar**: Q2273 alone takes 50 minutes, most of it
        fighting through camps one blocker at a time.
    - **Proof.** The bundle of NR-54d, on the tree that holds the three items
      (run/nr/NR-54f/guard-p8/verdict.json): all twelve scopes identical; seven checks,
      the unit suite and Fast (nr54f-fast2) pass.
- [x] **NR-54f - The rebirth before a quest instance is the class's own.** Depends: NR-54e
  - Work: The fifth stop of leg l10. Java first: nothing new. A death in the instance
    is answered by the option the client observes, as before. Before Bregirun the
    journey put up Hand of Reincarnation (4005) and stopped without it: "Bregirun needs
    the observed learned Hand of Reincarnation." That is the Cleric's skill. The profile
    names the class's self-rebirth. A class that names none goes in without one, and a
    death there is revived at its bind, as the leg's decisions already have it.
  - Proof: The Templar's leg l10 is played to its end in a replay; the Cleric's leg
    l10 still puts its rebirth up (rule (k)); the full gate identical.
  - 2026-10-09: done.
    - **The change.** Sc/Classes/NaturalClassProfile.cs: SelfRebirthSkillId, null
      when not given. The Cleric's profile names 4005. J, PrepareRebirthAsync: the
      skill is the profile's; with none it traces quest-instance-without-rebirth and
      returns.
    - **Proof, the Templar** (replays from altgard-rc-l9-templar). l10-a7
      (run/nr/NR-54f/l10-a7/, passed, with the first form of NR-54e) went into Bregirun
      without a rebirth and played the leg to its end: level 24, 162 quests, two
      deaths before the instance, none in it. l10-a8 (run/nr/NR-54e/l10-a8/, passed,
      the committed code) did the same with no death.
    - **Proof, the Cleric** (replay cleric-l10-a1 from altgard-rc-l9, the accepted
      line; run/nr/NR-54f/cleric-l10-a1/, replay.json: passed). No recorded scope goes
      into Bregirun, so the leg was played once. Before the portal it put up Hand of
      Reincarnation (quest-instance-rebirth-prepared, skill 4005), and the leg is
      complete and verified: level 24, 152 quests, 4 h 18 min of game time, four
      deaths, 118,605 records. Neither defence of NR-54d met a refused walk in it.
    - **Proof.** The bundle of NR-54d, on the tree that holds the three items
      (run/nr/NR-54f/guard-p8/verdict.json): all twelve scopes identical; seven checks,
      the unit suite and Fast (nr54f-fast2) pass.
- [x] **NR-54g - The destiny quest's stone step expects the class's stone.** Depends: NR-54f
  - Work: The stop of leg l11. Java first: _2900NoEscapingDestiny.getStoneId (240-262)
    hands one of four stones by second class, as read for NR-39. NR-39 gave the leg's
    destiny campaign the class's stone, skill and reward. The contract's step at which
    Heimdall hands the stone over still named the contract's stone, 140000001, which
    is the Cleric's and the Chanter's.
  - Proof: The Templar's leg l11 is played to its end in a replay; the Cleric's leg l11
    is unchanged (rule (k)); the full gate identical.
  - 2026-10-09: done.
    - **The change.** J, where NR-39 makes the leg's destiny the class's own: a step
      that receives the contract's stone receives the class's stone.
    - **Proof, the Templar** (replay l11-a4 from altgard-rc-l10-templar; evidence
      run/nr/NR-54g/l11-a4/, replay.json: passed). The stone is 140000003, the skill it
      grants 11506. Leg l11 complete and verified: level 24, 164 quests, 12 min 35 s of
      game time, no death, 8,894 records.
    - **Proof, the Cleric** (replay cleric-l11-a1 from altgard-l10, the accepted line;
      run/nr/NR-54g/cleric-l11-a1/, replay.json: passed). No recorded scope plays leg
      l11, so it was played once. The class's own destiny is not asked for the
      contract's class, so the changed lines do not run; the leg is complete and
      verified: level 24, 144 quests, 9 min 53 s, no death.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout, -Parallel 8, run
      guard-p8 (run/nr/NR-54g/guard-p8/verdict.json): verdict pass, all twelve scopes
      identical. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629 passed,
      16 skipped) and Fast passes (run nr54g-fast, 11 passed).
- [x] **NR-54a - The coin tiers buy the shield.** Depends: NR-50c
  - Work: Split from NR-50c. Java first: the reward shops as read for NR-38
    (TradeService.performBuyFromShop and validateBuyItems), and which trade tab of
    Lateni 203659 and of Nott 204360 holds the shield. For a class that holds a shield,
    the coin-gear leg and the two tiers of the Abyss entry buy the manifest's shield
    when it beats the one worn, from the coins left after the armor and the weapon, on
    its own tab, and wear it in the off hand; the leg's end check holds it. No Kinah
    purchase; no coin added (NR-Q14, NR-Q15). The Cleric's legs are the contracts
    themselves.
  - Proof: One-time check of each tier's purchases for a Templar from a prepared bag, with
    coins to spare and without; gates hm and ax identical with the full gate.
  - 2026-10-09: done.
    - **Java first.** TradeService.validateBuyItems (166-180) lets a reward shop sell
      every item of every trade tab of its own; the buy names no tab. The tab is the
      bot's own check of the offer. Lateni 203659 has the plate armor on tab 984 and the
      shield on 986; Nott 204360 has them on 990 and 992, the weapons on 988.
    - **The change, the coin-gear leg.** Sc/NaturalCoinGearPolicy.cs, ForClass: after
      the armor, the manifest's shield is bought when the gear rules score it above the
      shield held and the coins left pay for it, for the off hand (slot 2); the scope
      names the shield's tab. The shield held at the end is one of the slots the leg's
      end check holds, bought or not. Sc/NaturalCoinGearSteps.cs: the shop's offer is
      checked on the armor's tab and, for the shield, on its own.
    - **The change, the Abyss tiers.** Sc/NaturalAbyssEntry.cs: a tier names its shield
      and the scope the shield's tab, both from the class's manifests. Sc/
      NaturalAbyssCoinArmor.cs, Plan: the shield is compared by the class's own number,
      as its weapon is. It is bought only when the coins owned, less what the tier's
      armor and weapon cost, pay for it; so it never adds to the coins to supply.
      Owned and not worn, it is worn by the inventory check like any piece.
    - **Proof, the one-time check** (run/nr/NR-54a/check.log; the check file is not
      committed).
      - Coin-gear leg, the Templar with the bag of leg 11 and its 18 Iron Coins: it buys
        the shoulders for 1, the greaves for 2 and the shield 115001074 for 2, wears the
        shield in the off hand, and the leg's decisions run to their end with 18 coins.
        From a bare bag with 2 coins the five armor pieces take all 7 and no shield is
        bought. Holding the coin shield already, none is bought. The contract's Cleric
        is as it was: three purchases, five slots, no shield tab.
      - Abyss tiers, the Templar: at level 21 the armor and the sword cost 15 and the
        shield 115001075 costs 3; with 17 coins it is kept, with 18 bought, and the
        coins to supply are the same with it and without. At level 26 they cost 63 and
        the shield 115001082 costs 13; kept with 75 coins, bought with 76.
    - **Found, and logged (rule (f)).** The Templar's level-26 tier costs 63 Bronze
      Coins for the armor and the sword, and 15 at level 21: 78 from the bag of leg 11,
      where 44 are approved (NR-Q14). By that decision's default the Abyss-entry leg
      stops at the vendor with its own refusal; NR-56 will meet it.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout, -Parallel 8, run
      guard-p8 (run/nr/NR-54a/guard-p8/verdict.json): verdict pass, all twelve scopes
      identical, hm and ax among them. Seven pre-commit checks pass,
      Aion.GameServer.Tests passes (4,629 passed, 16 skipped) and Fast passes (run
      nr54a-fast, 11 passed).
- [x] **NR-55 - Templar: coin gear and Haramel.** Depends: NR-54, NR-54a; ticked by its round
  - Work: The coin-gear leg at Lateni (plate, and the shield by NR-54a) and Haramel with
    chest 700829 (NR-40), captured as altgard-coingear-templar and
    altgard-rc-complete-s1-templar.
  - Proof: Each capture verifies.
  - 2026-10-09, the coin-gear leg captured; Haramel stopped twice, and with NR-55a and
    NR-55b a replay played it to its end.
    - **Captured** at df7e4de09: altgard-coingear-templar from altgard-rc-l11-templar with
      -LaterCapital (run nr55-cg-a1, log run/nr/NR-55/cg-a1.log): the leg in 17 min with
      no death; level 24, 165 quests, 70,688,001 ms of game time, dump sha256 517b192010113bd1.
      At Lateni it bought the shoulders for 1 Iron Coin, the greaves for 2 and the
      shield 115001074 for 2 (NR-54a), wears the shield in the off hand and keeps 18.
    - **Haramel, attempt 1** (capture run nr55-l12-a1 from altgard-coingear-templar;
      nothing captured; evidence run/nr/NR-55/l12-a1/). Refused as it began: "Haramel
      starts from the level-24 CG endpoint with all three purchases equipped and 19
      Iron." NR-55a.
    - **Haramel, attempt 2** (replay l12-a2 with NR-55a's change; evidence
      run/nr/NR-55a/l12-a2/). Twenty-seven minutes in, on the way to Q28504's monsters, a
      pack of seven stood on the route. The Templar taunted one of them and asked to
      walk to it 1,007 times: "Warrior could not clear engaged attackers before resting
      or pulling." NR-55b.
  - 2026-10-09: done. Haramel is captured, and with it the Altgard endpoint.
    - **Captured** at eea719940: altgard-rc-complete-s1-templar from
      altgard-coingear-templar with -LaterCapital (run nr55-l12-a2, log
      run/nr/NR-55/l12-a2.log): Haramel in 1 h 42 min with no death; level 25, 176
      quests, 18 Iron Coins and 7 Bronze Coins, 76,828,001 ms of game time (21 h 20 min
      since creation), dump sha256 ccc07d86def1cb66.
    - **The two legs.** Two stops, each a lettered item: NR-55a (Haramel's start check)
      and NR-55b (the walk into a decided pack). Seven pre-commit checks pass
      (run/nr/NR-55/checks-run.log).
- [x] **NR-55a - Haramel's start asks the class's own scope.** Depends: NR-54a
  - Work: The first stop of Haramel. Java first: nothing of the server is relied on. The
    leg's start check held the contract's endpoint of the coin-gear leg: 19 Iron Coins,
    no Bronze Coin, and the Cleric's three coin pieces worn. NR-40 gave the leg's scope
    the class's own coins and asks another class for no armor by id; the start check
    did not ask the scope.
  - Proof: The start is passed in a replay of the Templar's Haramel; gate hm identical
    with the full gate.
  - 2026-10-09: done.
    - **The change.** Sc/NaturalHaramel.cs: the scope carries the Bronze Coins brought
      in, none for the contract's class. NaturalHaramelProgress.Begin asks for the
      scope's Iron Coins and those Bronze Coins, and for each of the three coin pieces
      only when the scope names it as incoming armor. For the Cleric that is 19, 0 and
      all three, as before.
    - **Proof, the replay** (l12-a2 from altgard-coingear-templar; evidence
      run/nr/NR-55a/l12-a2/). The Templar starts with its 18 Iron Coins and plays 27
      minutes of the leg, through the first visit's quests, before the stop that is
      NR-55b (30,453 records).
    - **Proof.** One bundle ran on the tree that holds NR-55a and NR-55b together; the
      two are committed one after the other from that tree. Gate, set
      all+mage+warrior+artist+engineer+scout, -Parallel 8, run guard-p8
      (run/nr/NR-55b/guard-p8/verdict.json): verdict pass, all twelve scopes identical,
      hm among them. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629
      passed, 16 skipped) and Fast passes (run nr55b-fast, 11 passed).
- [x] **NR-55b - A class that walks up takes a decided pull into its pack.** Depends: NR-55a
  - Work: The second stop of Haramel. Java first: nothing of the server is relied on. A
    pack of seven weak monsters stands on the way to Q28504's monsters. The Cleric's
    patrol rule waits four times and then pulls anyway (OD-12), and its fight is at
    range. The Templar has the same patrol rule and walks up to the monster it fights.
    Two things kept it out. (1) A defence whose approach is refused waits for its spell
    when the class "is not walk-in"; the Templar pulls from range and is not, but it
    has no spell to wait for. (2) The walk into a pack is refused when the pack would
    bring the fight to the swarm limit, also after the patrol rule has decided to take
    the pull with its helpers.
  - Proof: The Templar's Haramel is played to its end in a replay; the full gate
    identical.
  - 2026-10-09: done.
    - **The change.** J, DefendAgainstEngagedAsync: the wait for a spell is for a class
      that does not walk to its target (the movement rules' GoesToItsTarget); every
      profile that has played answers as before. J, MoveToPullSpotAsync: a pull the
      patrol rule decides to take, fight or pull anyway, is named to the fight
      (DecidedPullTarget); J.Combat, the approach step: the walk to that monster accepts
      its pack whatever its size (walk-in-accepts-pack, decided). The fight's own table
      still answers the swarm. Only the Templar has both the patrol rule and the walk.
    - **Proof, the replays** (from altgard-coingear-templar).
      - l12-a3 (run/nr/NR-55b/l12-a3/, with the first change alone): the defence no
        longer loops, but the pack is never entered: 180 walks refused, 138 defences
        given up, and the run ended with no progress for an hour.
      - l12-a4 (run/nr/NR-55b/l12-a4/, replay.json: passed, both changes). Four decided
        walks into packs of one, six, one and two; none refused. Haramel complete and
        verified: level 25, 176 quests, 1 h 42 min of game time, no death, 36,505
        records, 132 fights, 127 kills, six retreats.
    - **Proof.** The bundle of NR-55a, on the tree that holds both items
      (run/nr/NR-55b/guard-p8/verdict.json): all twelve scopes identical; seven checks,
      the unit suite and Fast (nr55b-fast) pass.
- [x] **NR-56 - Templar: the Abyss entry.** Depends: NR-55; ticked by its round
  - Work: Q24020, then Q2945, Q2946, Q2947 and Q2042 with the two coin tiers at Nott
    (NR-38b, NR-Q14), captured as morheim-abyss-entry-s1-templar.
  - Proof: The capture verifies.
  - 2026-10-09, the leg stopped twice, and with NR-56a and NR-56b a replay played it to
    its end.
    - **Attempt 1** (capture run nr56-ax-a1 from altgard-rc-complete-s1-templar; nothing
      captured; evidence run/nr/NR-56/ax-a1/). At Aegir, 16 s into the leg: "q24020-aegir
      did not hand over item 110551147", the Cleric's hauberk. NR-56a.
    - **Attempt 2** (replay ax-a2 with NR-56a's change; evidence run/nr/NR-56a/ax-a2/).
      The four missions are done in 15 min 52 s. The leg then stopped as its contract
      says: "The missions are done and the Cleric is level 25, below 26: it needs a few
      fortress quests, which are not listed yet (AX-11)." NR-56b, NR-Q16.
  - 2026-10-09: done. The leg is captured.
    - **Captured** at 362b5baef: morheim-abyss-entry-s1-templar from
      altgard-rc-complete-s1-templar (run nr56-ax-a4, log run/nr/NR-56/ax-a4.log): the leg
      in 16 min 02 s with no death; a Templar of level 25 at Morheim Ice Fortress, 181
      quests, Q24020, Q2945, Q2946, Q2947 and Q2042 among them; 77,810,164 ms of game
      time (21 h 36 min since creation), dump sha256 9f9cdfd9d38dba24.
    - **Two stops**, each a lettered item: NR-56a (the reward step) and NR-56b (the
      leg's end at level 25, NR-Q16). The level-21 tier was paid from the Templar's own
      coins and the level-26 tier is not asked at level 25, so NR-Q14 was not met.
      Seven pre-commit checks pass (run/nr/NR-56/checks-run.log).
- [x] **NR-56a - A reward's hand-over step expects the class's pick.** Depends: NR-55
  - Work: The first stop of the Abyss entry. Java first: nothing new; a quest hands
    over the reward selected, as read for NR-32. NR-32 gave a leg the class's own reward
    picks. The contract's steps at which a pinned reward is handed over, Q24020's
    hauberk and Q2947's staff, still named the contract's items.
  - Proof: The stop is passed in a replay of the Templar's Abyss entry; the full gate
    identical.
  - 2026-10-09: done.
    - **The change.** J, where a leg takes the class's reward picks: a step that
      receives a pinned reward receives the class's pick.
    - **Proof, the replay** (ax-a2 from altgard-rc-complete-s1-templar; evidence
      run/nr/NR-56a/ax-a2/). Aegir hands the Templar the plate 110601626. At the level-21
      tier it buys the shoes for 2 and the shield 115001075 for 3 from the 7 Bronze
      Coins it has (NR-54a), and no coin is supplied. Garm's arena is done at the first
      try, ten spirits with 172 s to spare, and the ring course at the first try with 24
      s on the clock, with the one flight-speed scroll. The replay stopped at the leg's
      level rule: NR-56b (14,817 records).
    - **Proof.** One bundle ran on the tree that holds NR-56a and NR-56b together; the
      two are committed one after the other from that tree. Gate, set
      all+mage+warrior+artist+engineer+scout, -Parallel 8, run guard-p8
      (run/nr/NR-56b/guard-p8/verdict.json): verdict pass, all twelve scopes identical,
      ax among them. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629
      passed, 16 skipped) and Fast passes (run nr56b-fast, 11 passed).
- [x] **NR-56b - The Abyss entry ends at the plan's finish for a class below level 26.** Depends: NR-56a
  - Work: The second stop of the Abyss entry. Java first: nothing of the server is relied
    on; these are the leg's own rules. The contract holds level 26 after the missions,
    by quests alone, and then the level-26 coin tier. Those are receipts of the accepted
    run, as its start facts were (NR-41). The Templar ends the missions at level 25 with
    the same quests done. NR-Q16 gives the default.
  - Proof: The Templar's Abyss entry is played to its end in a replay; gate ax identical
    with the full gate.
  - 2026-10-09: done.
    - **The change.** J, the class's Abyss scope: its level and the endpoint's level are
      the level the leg starts from when that is lower. Sc/
      NaturalAbyssEntryDecisionEngine.cs: a coin tier above the character's level is
      not asked. Sc/NaturalAbyssEntry.cs: the endpoint and the ledger hold the tiers the
      character can wear. The contract's class reaches level 26 before its last tier,
      so nothing changes for it.
    - **Proof, the replay** (ax-a3 from altgard-rc-complete-s1-templar; evidence
      run/nr/NR-56b/ax-a3/, replay.json: passed). The leg is complete and verified at
      its endpoint: a Templar of level 25, alive at Morheim Ice Fortress and bound there;
      Q24020, Q2945, Q2946, Q2947 and Q2042 complete, 181 quests in all; 16 min 02 s of
      game time, no death, 14,918 records. It wears Q2947's sword 100001562, the
      level-21 coin shield 115001075, Aegir's plate 110601626 and the coin shoes, and
      holds 12 Bronze Coins and 18 Iron Coins. One help item was supplied, the
      flight-speed scroll; no coin.
    - **Proof.** The bundle of NR-56a, on the tree that holds both items
      (run/nr/NR-56b/guard-p8/verdict.json): all twelve scopes identical; seven checks,
      the unit suite and Fast (nr56b-fast) pass.
- [x] **NR-57a - The Abyss entry's capture may take the plan's finish name.** Depends: NR-56
  - Work: The plan preserves each class as ntc-ready-<class>-s1, as the accepted line's
    ntc-ready-cleric-s1 is. NR-41 has a leg capture of another line named after the
    accepted line's snapshot with the class behind it (morheim-abyss-entry-s1-templar),
    and the script refuses every other name. The Abyss-entry leg ends the plan, so its
    capture may take the finish name as well.
  - Proof: scripts/sim/test-sim-snapshot.ps1 passes; the full gate identical. The
    capture itself is NR-57.
  - 2026-10-09: done.
    - **The change.** scripts/sim/sim-snapshot.ps1, the capture of a leg for a line with
      a second class: the name ends with the class, or the leg is ax and the name is
      ntc-ready-<class>-s1. Every other name is refused as before.
    - **Proof.** The three script tests pass (run/nr/NR-57a/script-tests.log), the one
      that holds the refusal among them. Gate, set
      all+mage+warrior+artist+engineer+scout, -Parallel 8, run guard-p8
      (run/nr/NR-57a/guard-p8/verdict.json): verdict pass, all twelve scopes identical.
      Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629 passed, 16
      skipped) and Fast passes (run nr57a-fast, 11 passed).
- [x] **NR-57 - Templar: the endpoint.** Depends: NR-56; ticked by its round
  - Work: Captured and verified as ntc-ready-templar-s1: alive at Morheim Ice Fortress,
    level 25 or higher, Q2945, Q2946, Q2947 and Q2042 complete.
  - Proof: The capture verifies.
  - 2026-10-09: done. ntc-ready-templar-s1 is captured and verified.
    - **Captured** at 37870a51e: sim-snapshot.ps1 -Action Capture -AltgardLeg1 -Leg ax
      -From altgard-rc-complete-s1-templar -Name ntc-ready-templar-s1 (run nr57-ax-a1,
      log run/nr/NR-57/capture-a1.log; NR-57a lets the leg's capture take this name).
      The leg was played once more, to the receipt morheim-abyss-entry-s1-templar has:
      16 min 02 s, no death, 77,810,164 ms of game time since creation. Dump sha256
      326663d8fcb9e437.
    - **The endpoint, from the receipt.** Character 133266, the one created by packets
      for altgard-templar-s1: a Templar of level 25, alive at Morheim Ice Fortress and
      bound there; Q24020, Q2945, Q2946, Q2947 and Q2042 complete, 181 quests in all. It
      wears Q2947's sword 100001562 and the level-21 coin shield 115001075, and holds
      12 Bronze Coins and 18 Iron Coins.
    - **Proof.** sim-snapshot.ps1 -Action Verify -Name ntc-ready-templar-s1, run
      nr57-verify-a1 (run/nr/NR-57/verify-a1.log; evidence under
      run/snapshots/_verify/nr57-verify-a1): "Verified snapshot ntc-ready-templar-s1:
      character 133266 resumed at its endpoint." Seven pre-commit checks pass
      (run/nr/NR-57/checks-run.log).
    - **The Templar's line, for scale.** 21 h 36 min of game time from creation to the
      endpoint, against the generic Cleric's 19 h 37 min. Twenty lettered items, NR-50a
      to NR-57a, none of them a branch on the class.
- [x] **NR-58 - Templar: the class scope.** Depends: NR-57
  - Work: One scope of the Templar's play, chosen where its rules differ most from the
    Warrior's, recorded by a bot alone, twice, and added to the gate's sets.
  - Proof: The two recordings are identical.
  - 2026-10-09: done. Scope templar is recorded and stands in the baseline file.
    - **The scope.** Haramel, leg l12 from altgard-coingear-templar with the later
      capital. It is where the Templar plays least like the Warrior it was: every pull is
      a Taunt from range, four packs are walked into after the patrol rule decided to
      take them (NR-55b), the patrol rule holds 153 times, and the shield bought at
      Lateni is in the off hand throughout. It is of the size of the Cleric's own
      Haramel scope, hm.
    - **The row** (scripts/sim/run-neutral-gate.ps1, committed first as 86ee86187 with
      the script tests, the full gate on twelve scopes, the seven checks, the unit suite
      and Fast nr58-fast).
    - **The recording** at 86ee86187 on a clean tree (run record-a1,
      run/nr/NR-58/record-a1/verdict.json): two passes, identical after normalization:
      36,504 records, sha256 fae0a9da6017c6b1. No death, four retreats, 274 pull plans, one help
      item supplied. The traces are kept under run/cp/baseline/86ee86187... and in the
      second copy beside the repository.
    - **Proof.** A comparison of the scope with its new row passes (run guard-templar,
      run/nr/NR-58/guard-templar/verdict.json). Seven pre-commit checks and the three
      script tests pass on the tree with the new row (run/nr/NR-58/evidence-run.log).
    - **The full gate from here** is set
      all+mage+warrior+artist+engineer+scout+templar: thirteen scopes.
- [x] **NR-60 - Sorcerer: survey and profile data.** Depends: the close of phase C
  - 2026-10-10: done. The Sorcerer has a class line, a gear table and a profile the
    validator accepts. Nothing has been played. One thing its skills need that the shared
    forms cannot say yet is item NR-60a below.
    - **Java.**
      - **What it learns.** SkillLearnService.learnNewSkills 60-76 and autoLearnSkills
        85-93, as read for NR-50. Of the Sorcerer's 97 rows to level 26, 76 are
        auto-learned, 18 are stigma rows from level 20 and three are skill books
        (learnSkillBook, 95): Sleep in its two looks at level 10 (books 169500932 and
        169500933) and Homeward Bound at 21 (169500953, 242,500 Kinah). The route buys no
        book, so its Sorcerer has no Sleep. 61 of the auto-learned are active skills, and
        with the seven it keeps from the Mage the catalog has 68 to give a role or a
        reason (run/nr/NR-60/survey.log).
      - **Chains.** ChainCondition as read for NR-50. Flame Bolt and Flame Harpoon (level
        13) are both first steps of one chain, and Blaze follows either inside 3 s. Ice
        Chain opens the chain Frozen Shock follows. Wind Spear (level 15) is a chain on
        itself: it may be cast three times in a row, each inside 3 s, once in 90 s.
      - **One cooldown for two skills.** Flame Cage (level 16) has Erosion's cooldown id,
        273. Both are an instant hit with 15 s of damage after it; the stronger one is
        cast.
      - **Class rewards on the route** are the two of NR-32, Q2009 and Q2947, the stone
        of Q2900 (140000004, NR-39) and Haramel's chest 700831 (NR-40). Nothing new.
    - **The class line.** `mage-sorcerer`: a Mage who becomes a Sorcerer (SETPRO11), SIM
      account 41, character Asimsorcerer, the spellbook 100600532 at the ceremony (Q2009
      offers it and the orb 100500500; NR-Q5). Its dispatch quest is Q2903 (NR-30). It is
      in NaturalClassLine.All and in the script's list; the script test holds the two
      together.
    - **The gear table** (Sc/Classes/NaturalClassGearTable.cs, Sorcerer).

      | Part | The Sorcerer's | From |
      |---|---|---|
      | Weapon group | spellbook; ranked by magic boost, then the most damage | NR-Q5 |
      | Off hand | nothing | CP-Q10 |
      | Armor | robe, clothes; item level first, the type breaks ties | NR-Q7, CP-Q24 |
      | Bonus order at a reward | magic boost, magical accuracy, magical critical, then mana and concentration | NR-32 |
      | Kept | life potions, its help kit, the mana potions, every help scroll and food | as the Cleric from Ascension |

      Its masteries allow the orb too; NR-Q5 gives it the spellbook.
    - **The help kit from level 10, as a manifest** (NR-Q8; the rows of
      NaturalHelpItemAllowlist.Kit(caster: true, reagent: true), which is every row).

      | Item | Family | Levels | Topped up to | When it owns fewer than |
      |---|---|---|---|---|
      | 162000002 | life potion | 10 to 19 | 30 | 10 |
      | 162000003 | life potion | 20 to 29 | 30 | 10 |
      | 162000004 | life potion | 30 to 39 | 30 | 10 |
      | 162000017 | mana serum | 10 to 19 | 40 | 10 |
      | 162000018 | mana serum | 20 to 29 | 40 | 10 |
      | 162000019 | mana serum | 30 to 39 | 40 | 10 |
      | 164000067 | shield scroll | 10 to 19 | 30 | 8 |
      | 164000068 | shield scroll | 20 to 29 | 30 | 8 |
      | 164000069 | shield scroll | 30 to 39 | 30 | 8 |
      | 164000133 | Awakening scroll | 20 to 29 | 60 | 15 |
      | 164000134 | Awakening scroll | 30 to 39 | 60 | 15 |
      | 164000075 | running scroll | 20 to 29 | 20 | 5 |
      | 164000076 | running scroll | 30 to 39 | 20 | 5 |
      | 160002273 | DP jelly | 10 to 39 | 8 | 2 |
      | 169300003 | powder | 10 to 24 | 200 | 50 |
      | 169300004 | powder | 25 to 39 | 200 | 50 |

      Levels 1 to 9 are the Mage's kit (CP-Q12).
    - **Its skills by role** (Sc/Classes/NaturalSorcererProfile.cs; 52 skills of 68).

      | Role | Skill, and the levels of its ranks | What it is |
      |---|---|---|
      | bolt | Flame Bolt: 1, 11, 16, 21, 26 | Opens the fire chain. 2 s cast, no cooldown. |
      | harpoon | Flame Harpoon: 13, 18, 23 | Opens the same chain, harder. 2 s cast, 3 s. |
      | blaze | Blaze: 5, 10, 15, 20, 25 | Follows either inside 3 s. Instant, 30 s. |
      | ice | Ice Chain: 3, 13, 18, 23 | Opens the ice chain and snares. 2 s cast, 10 s. |
      | shock | Frozen Shock: 7, 12, 17, 22 | Follows Ice Chain inside 3 s. Instant, 30 s. |
      | erosion | Erosion: 5; Flame Cage: 16, 21, 26 | An instant hit and 15 s of damage. 3 s, then 5 s. |
      | blast | Delayed Blast: 19, 24 | Hits 4 s after a 2 s cast. 30 s. |
      | empyrean | Empyrean Fire: 10, 15, 20, 25 | 2,000 DP: the hardest hit it has. 2 s cast, 60 s. |
      | spear | Wind Spear: 15, 20, 25 | An instant hit, three in a row, one time in three a snare. 90 s. |
      | frost | Freezing Wind: 25 | An instant hit at 3 m. 194 MP, 2 s. |
      | root | Root: 1 | Holds the target for 20 s until it is hit. 60 s. |
      | skin | Stone Skin: 7, 12, 17, 22 | A shield for 5 min. 130 to 253 MP, 2 min. |
      | robe | Robe of Flame: 10, 15, 20, 25 | Magic boost and mana regeneration for 30 min. 97 to 128 MP. |
      | herb, mp-recovery | Herb Treatment and MP Recovery: 10, 15, 20, 25 | The two powder skills of a rest (NR-50a). |

    - **The rule table, natural-sorcerer-v1.** The Mage's, with what the Sorcerer adds.
      From range: Ice Chain, then Frozen Shock at once; Empyrean Fire when the DP are
      there; Delayed Blast early, since it lands later; then Flame Harpoon, Flame Bolt
      and Blaze, an open follow-up always first; Wind Spear last. With the monster on it
      the instants come first, which a hit cannot push back: Freezing Wind, then Flame
      Cage or Erosion. Stone Skin goes up before the first hit and is kept up between
      fights, as is Robe of Flame. Root is cast only on the way out, before a retreat.
      The ladder: the shield scroll at 50% HP, the life potion at or below 75%. It leaves
      at two attackers, or at 25% HP with nothing ready. A mana serum is drunk only when
      the cheapest attack cannot be paid; the spellbook swings only then. Between fights
      it rests with the powder first, then as the Mage: the life potion below 90% HP, a
      sit for mana below 40% until 80%. It holds for a patrol and assesses (NR-37) with
      no heal to ask about, and a pull may bring one. In flight it shoots with the list
      it has at range (NR-36).
    - **Left out, with the reason** (16 skills).

      | Skill, levels | Why it is not cast |
      |---|---|
      | Return, Bandage Heal, Escape: 1 | As for every class (CP-35, CP-Q11). |
      | Gain Mana: 10, 15, 20, 25 | It restores mana at no cost, once in 3 min. The ladder has steps for HP only, and a rest knows the powder and a sit. NR-60a. |
      | Curse of Roots: 10 | It holds its target for 20 s after a 1.5 s cast, and a hit ends it. The table names one control, the instant Root. |
      | Blind Leap: 13 | It throws the Sorcerer 15 m ahead to a place the server picks; the bot walks checked routes only. |
      | Winter Binding: 16, 21, 26 | It hits and roots up to eight monsters within 15 m. The bot pulls one at a time, and an area skill wakes every other one in reach. |
      | Somnolence: 20 | A 4 s sleep for 268 MP that a hit ends; the table names one control. |
      | Illusion: 20 | It dodges the next two hits inside 10 s for 269 MP; no rule times a defence against a hit that has not come. |
      | Summon Rift: 22 | A rift for a group to travel by; the journey's travel casts no skill. |
      | Boon of Peace: 25 | It lowers the enmity of monsters within 5 m; alone, they have no one else to turn to. |

      Not in the catalog at all: Sleep, which is a book (above), and the stone's skill, as
      for every class (NR-39).
    - **Proof.** The one-time check, not committed (run/nr/NR-60/check.log): the line
      parses and holds both classes; its character is a Mage until the client observes a
      Sorcerer; NaturalClassProfiles builds the Sorcerer's profile, which requires the
      validator: each of the 68 active skills has one role or one reason, every follow-up
      has its opener, no rotation line breaks a chain, and the gear groups lie inside the
      masteries. The check prints the 52 rows, the 16 reasons, the best rank of each role
      at levels 10, 13, 16, 20, 25 and 26, the gear groups, the kit and the patrol view.
      Gate, set all+mage+warrior+artist+engineer+scout+templar, -Parallel 8, run guard-p8
      (run/nr/NR-60/guard-p8/verdict.json): verdict pass, all thirteen scopes identical.
      Seven pre-commit checks pass, the three script tests pass
      (run/nr/NR-60/script-tests.log), Aion.GameServer.Tests passes (4,629 passed, 16
      skipped) and Fast passes (run nr60-fast, 11 passed).
- [x] **NR-60a - A skill that restores mana.** Depends: NR-60
  - Work: Gain Mana gives a Sorcerer 314 MP at once and 124 a second for 5 s more, at no
    cost, once in 3 min; the Spirit Master and other classes have skills of the kind.
    Java first: the skill's effects and what a fight or a rest refuses of it. The rule
    table gets a step that casts the role below an MP percentage in a fight, before the
    mana serum, and the rest casts it before the powder's MP Recovery. Generic: a role
    and a number in the table, no branch on a class.
  - Proof: One-time check of the decisions at three MP levels in a fight and in a rest;
    the full gate identical.
  - 2026-10-10: done.
    - **The skill, from the shipped template** the server loads (run/nr/NR-60/survey.log).
      Gain Mana asks for nothing: no mana, no DP, no weapon, no state of the fight. It is
      instant, on the caster, with a cooldown of 180 s of its own (id 1472). Rank I gives
      314 MP at once and 124 a second for 5 s.
    - **The change, the fight.** Sc/Classes/NaturalRotationCombatPolicy.cs: a rule table
      may name a mana step, a role and an MP percentage. At or below it the best learned
      skill of the role is cast on the bot when it is ready, before the table asks for a
      mana potion. No table but the Sorcerer's names one.
    - **The change, the rest.** Sc/Classes/NaturalRestRules.cs: a class's rest skills
      may name a free mana skill. Sc/NaturalClericSkills.cs, the powder policy: when
      mana is needed and that skill is ready, it is cast first, before the skill that
      trades health for mana and before the powder. The Cleric's rest skills name none.
    - **The Sorcerer.** Gain Mana has the role gainmana: 56 of its 68 active skills have
      a role now and 12 a reason. The table casts it at or below half its mana, and its
      rest casts it first.
    - **Proof, the one-time check** (run/nr/NR-60a/check.log; the check file is not
      committed; its first run, check-a1.log, failed on a number of the check's own). A
      Sorcerer of level 10, 20 and 25, each with its rank:
      - In a fight: at 51% mana it casts Ice Chain; at 50% Gain Mana; at 50% with Gain
        Mana cooling down, Ice Chain; with no mana and a serum, Gain Mana, and with Gain
        Mana cooling down, the serum.
      - In a rest: at 60% mana it is done; at 39% it casts Gain Mana; with Gain Mana
        cooling down, MP Recovery for two powders; with no powder either, it sits. While
        the sit for mana is on it casts Gain Mana at 70%, and at 80% the rest is done.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout+templar, -Parallel 8,
      run guard-p8 (run/nr/NR-60a/guard-p8/verdict.json): verdict pass, all thirteen
      scopes identical. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629
      passed, 16 skipped) and Fast passes (run nr60a-fast, 11 passed).
- [x] **NR-61 - Sorcerer: probe rows.** Depends: NR-60a, NR-61a
  - Work: Rows sorcerer-10, sorcerer-16, sorcerer-20 and sorcerer-25 in
    SimulationNaturalStarterProbeTests: prepared Sorcerers on the two probe accounts, in
    the gear the route has given by that level, fight the monsters the Cleric's rows
    fight. Each row's trace shows the table in play: the two chain pairs, Flame Harpoon
    before Flame Bolt, Flame Cage for Erosion from 16, Wind Spear's three casts, Delayed
    Blast and Empyrean Fire, Stone Skin and Robe of Flame up, the ladder, the rest with
    the powder. What a row shows decides the open rules: Wind Spear's worth, and whether
    Freezing Wind earns its 194 MP. A fix is one small change (rule (i)).
  - Proof: The four rows end with the monster dead or a recorded retreat, no refused
    cast repeated and no skill outside the table cast.
  - 2026-10-10: done. The four rows pass; the table changed in one place: Wind Spear
    is out of it.
    - **Java.** What the rows met is read under NR-61a (a target about to die) and
      below (Wind Spear's three casts).
    - **The rows** (SimT/SimulationNaturalStarterProbeTests.cs: sorcerer-10, sorcerer-16,
      sorcerer-20, sorcerer-25, on the probe accounts 98 and 100, at the places and
      monsters of the Cleric's rows). The director makes the Mage a Sorcerer of the level
      with the skills of every level up to it, puts the spellbook and the robe pieces of
      the route into its bag, and places it. From there the journey's equipment check,
      buff check, fight and rest act. Two rows to a process, the two processes side by
      side: `bash run/nr/NR-61/probe.sh <attempt>`.
    - **Before NR-61a** (runs nr61-probe-a1 and a1b, kept under rows-a1): rows 10 and 16
      passed; rows 20 and 25 stopped at what NR-61a mends. That attempt belongs to the
      budget NR-61a renewed (rule (i)).
    - **First attempt** (runs nr61-probe-a2 and a2b, kept under rows-a2): the four rows
      passed with the table of NR-60. In nine fights Wind Spear was decided at no time.
    - **Second attempt** (runs nr61-probe-a3 and a3b; the four traces are under
      run/nr/NR-61/), with Wind Spear out of the table: the four rows pass, cast for
      cast as before.

      | Row | Prepared by the director | What the journey did | Outcome |
      |---|---|---|---|
      | sorcerer-10 | Level 10, the ceremony's spellbook; then 20 powder and half HP | The check took the book; the buff check cast Stone Skin and Robe of Flame. One ice crasaur: Ice Chain, Frozen Shock 701 ms after it, Flame Bolt, Blaze 1,159 ms after it. The rest cast Herb Treatment once. | A kill in 7.1 s; MP 1,140 to 981 of 1,367; HP untouched. |
      | sorcerer-16 | Level 16; the spellbook of Q24013 and the robe shoes and tunic of Q24011 and Q24012 | The check wore all three. One tusked mosbear: Ice Chain, Frozen Shock, Flame Harpoon, Blaze; then two were on it: Root, and it left. | A retreat, as the Cleric leaves there; MP 2,199 to 1,691; HP untouched. |
      | sorcerer-20 | Level 20; the spellbook of Q24016, 20 powder, two shield scrolls, three life potions; then 2,000 DP and 45% HP; then 45% of its mana; then a tenth of it | Unhurt: Ice Chain, Frozen Shock, Delayed Blast, Flame Harpoon, Blaze. Hurt with DP: the shield scroll and the life potion at 44% HP, Ice Chain, Empyrean Fire. Short of mana: Gain Mana at 44%, then the same line. The rest cast MP Recovery twice. | Three kills; DP 2,000 to 14; MP 249 to 2,369 of 2,491 by the rest, four powders. |
      | sorcerer-25 | Level 25; the same spellbook, 20 Odella Powder; then one starved mosbear set on it; then two; then half HP | Three fights alone: the chain pairs, Delayed Blast, Flame Harpoon; in the first the fight waited 4,067 ms for the blast (NR-61a). With one on it: Delayed Blast before it struck, then Freezing Wind, Flame Cage, Flame Harpoon. With two: Gain Mana, Ice Chain, Root, a retreat that a corner refused, then Freezing Wind, Flame Cage, Flame Harpoon. The rest cast Herb Treatment IV. | Five kills in 10.0, 12.3, 11.9, 8.6 and 10.0 s; MP 3,032 to 1,452 over the first four; HP untouched. |

      In no fight was a skill decided more than twice running, no skill outside the
      table was cast, and no target was given up. Every decision carries the table
      natural-sorcerer-v1.
    - **What the rows decide.**
      - **Wind Spear is out of the table** (Sc/Classes/NaturalSorcererProfile.cs: a
        reason in place of its role). From range it stood behind Flame Bolt, which is
        always ready, and was never reached. It is also the dearest damage of the table
        for its mana: 216 for 103 MP in its third rank, where Flame Bolt gives 360 for
        87 and Delayed Blast 407 for 92. 53 of the Sorcerer's 68 skills have a role now
        and 15 a reason.
      - **Freezing Wind keeps its place.** With the mosbear on the Sorcerer one cast
        took it to half its HP, and that fight was the shortest of the five.
      - **The ladder and the mana step answer** as the table says: the scroll and the
        potion at 44% HP, Gain Mana at 44% and 48% of its mana.
    - **Found, and logged (rule (f)).**
      - **A skill cast several times in a row.** The server lets Wind Spear be cast
        three times, each inside 3 s of the one before, and holds its 90 s only after
        that (Player.isSkillDisabled 1525-1531; the second and third cast set no
        cooldown, Skill.java 626-627, and have no cast time, calculateCastDuration
        377-379). The table holds a skill by its cooldown after the first cast. The
        Engineer's and the Gunner's Rapidfire is such a skill too; its cooldown is 1 s,
        so the table waits that second. No class needs the form to finish; it has no
        item.
      - **The rows' monsters did not hurt the Sorcerer.** Its HP was untouched in the
        seven fights of the rows 10, 16 and 25, so the rows say nothing of how it stands
        a hit; the hurt fight is the director's. The rounds will say.
      - **Mana.** Four fights in 44 s cost a level-25 Sorcerer 1,580 of 3,032 MP, 381 of
        it the two buffs. Gain Mana and the powder carry that; the rounds will say for
        how long.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout+templar, -Parallel 8,
      run guard-p8 (run/nr/NR-61/guard-p8/verdict.json): verdict pass, all thirteen
      scopes identical. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629
      passed, 16 skipped) and Fast passes (run nr61-fast, 11 passed).
- [x] **NR-61a - A target that is about to die by a hit already made.** Depends: NR-60a
  - Work: Java first: when the server takes no skill at a target, and what it says. The
    fight knows which of its own hits is still on its way and waits for it. Generic: a
    skill row and the fight, no branch on a class.
  - Proof: One probe row on a probe account in which Delayed Blast is cast on a monster
    it will kill, a cast after it is refused, and the fight ends in the kill; the full
    gate identical.
  - 2026-10-10: done.
    - **Found in NR-61's first attempt.** The level-25 Sorcerer cast Ice Chain, Frozen
      Shock and Delayed Blast at a starved mosbear. 0.7 s after the blast's cast the
      server answered Flame Harpoon with STR_SKILL_TARGET_IS_NOT_VALID. The fight read
      that as a monster another player had killed, gave the target up and ended with no
      kill and no retreat (trace a1b, sorcerer-25, step s03-1, 7.3 s). The mosbear died
      4 s later, by the blast. The level-20 row, next in that world, was given the same
      mosbear and stopped at once; alone it passes (run/nr/NR-61/probe-diag.log).
    - **Java.**
      - When a hit is counted that takes its target's HP to 0, the target is marked
        with the killing blow and is about to die (Effect.setReserveds 345-361;
        CreatureLifeStats.isAboutToDie 59-61). The mark goes when it dies or is healed
        above the blow (CreatureLifeStats 93, 107 and 189).
      - No skill is taken at such a target (Skill.isValidTarget 244-245, which
        canUseSkill reaches), and the caster is told STR_SKILL_TARGET_IS_NOT_VALID, the
        message for a dead target too (validateEffectedList 208-213).
      - For nearly every skill the mark lasts its hit time, which the fight waits out
        after every cast. Delayed Blast counts its damage when the effect is applied
        and deals it 4,000 ms later (DelayedSpellAttackInstantEffect.applyEffect
        25-35): for those 4 s the monster lives, strikes, and can be given no skill.
      - The port has the same lines (Effect.cs 376-386, Skill.cs 231).
    - **The change, generic.**
      - Sc/NaturalPriestCombatPolicy.cs and Sc/Classes/NaturalSkillCatalog.cs: a skill
        row says how long after it is applied the skill's damage lands.
      - Sc/NaturalIshalgenJourney.Combat.cs: after such a cast the fight keeps the
        target and the time the hit lands: the cast's result, its hit time and the
        delay. When the server calls that target not valid before then, the fight
        waits until the hit has landed and looks again; the trace says
        combat-target-about-to-die. At any other time the answer is read as before.
    - **Proof, the probe row** sorcerer-blast (SimT/SimulationNaturalStarterProbeTests.cs,
      probe account 98; `bash run/nr/NR-61a/probe.sh <attempt>`). Prepared by the
      director: a level-25 Sorcerer with the spellbook of Q24016, by the starved
      mosbears. First attempt (nr61a-probe-a1): passed. Ice Chain and Frozen Shock take
      the mosbear to 35% HP; Delayed Blast is cast at 6.6 s; at 7.3 s the server refuses
      Flame Harpoon; the fight waits 4,067 ms; the blast lands at 11.4 s, the mosbear
      dies and the experience comes. One fight, one kill, in 10.0 s, and no target given
      up.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout+templar, -Parallel 8,
      run guard-p8 (run/nr/NR-61a/guard-p8/verdict.json): verdict pass, all thirteen
      scopes identical. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629
      passed, 16 skipped) and Fast passes (run nr61a-fast, 11 passed).
- [ ] **NR-62 - Sorcerer: to Altgard.** Depends: NR-61; ticked by the round that gives it
  - Work: A fresh Asimsorcerer plays Ishalgen as a Mage, the trial, the Sorcerer choice at
    Munin, the ceremony with the spellbook and the dispatch Q2903, and is captured at the
    Altgard bind as altgard-sorcerer-s1.
  - Proof: The capture verifies.
- [ ] **NR-63 - Sorcerer: Altgard legs l1 to l5.** Depends: NR-62; ticked by its round
  - Work: The Cleric's legs in order, each end captured under the Cleric's snapshot name
    with -sorcerer (NR-41). In leg 1 it shoots the fungus in flight.
  - Proof: Each capture verifies.
- [ ] **NR-64 - Sorcerer: Altgard legs l6 to l11.** Depends: NR-63; ticked by its round
  - Work: As NR-63. Leg 11 is the destiny quest: its stone is 140000004 (NR-39).
  - Proof: Each capture verifies.
- [ ] **NR-65 - Sorcerer: coin gear and Haramel.** Depends: NR-64; ticked by its round
  - Work: The coin-gear leg (cloth, by its manifest, NR-38a) and Haramel with chest
    700831 (NR-40), captured as altgard-coingear-sorcerer and
    altgard-rc-complete-s1-sorcerer.
  - Proof: Each capture verifies.
- [ ] **NR-66 - Sorcerer: the Abyss entry.** Depends: NR-65; ticked by its round
  - Work: Q24020, then Q2945, Q2946, Q2947 and Q2042 with the coin tiers it can wear
    (NR-38b, NR-Q14, NR-Q16), captured as morheim-abyss-entry-s1-sorcerer.
  - Proof: The capture verifies.
- [ ] **NR-67 - Sorcerer: the endpoint.** Depends: NR-66; ticked by its round
  - Work: Captured and verified as ntc-ready-sorcerer-s1: alive at Morheim Ice Fortress,
    level 25 or higher, Q2945, Q2946, Q2947 and Q2042 complete.
  - Proof: The capture verifies.
- [ ] **NR-68 - Sorcerer: the class scope.** Depends: NR-67
  - Work: One scope of the Sorcerer's play, chosen where its rules differ most from the
    Mage's, recorded by a bot alone, twice, and added to the gate's sets.
  - Proof: The two recordings are identical.
- [x] **NR-70 - Chanter: survey and profile data.** Depends: the close of phase C
  - 2026-10-10: done. The Chanter's placeholder is a profile of its own now, and the
    validator accepts it. Its class line and its gear table stood already (CP-29a, CP-32).
    Nothing has been played. One thing its skills need that the shared forms cannot say
    yet is item NR-70a below.
    - **Before.** NaturalChanterProfile took the Priest's catalog and the Priest's table,
      and left every skill a Chanter learns out with one reason: no Chanter leg exists.
    - **Java.**
      - **What it learns.** SkillLearnService as read for NR-50. Of the Chanter's 104
        rows to level 26, 79 are auto-learned, 24 are stigma rows from level 20 and one
        is a skill book, Homeward Bound at 21. 56 of the auto-learned are active skills,
        and with the eight ranks it keeps from the Priest the catalog has 64 to give a
        role or a reason (run/nr/NR-70/survey.log). Six more are toggles, the mantras:
        below.
      - **Chains.** ChainCondition as read for NR-50. Hallowed Strike opens a chain of
        three: Booming Strike (level 10) follows it inside 3 s and Crashing Strike (13)
        follows that. From level 22 Booming Smash is a second follow-up of Hallowed
        Strike, and nothing follows it. Meteor Strike (10) opens the chain Incandescent
        Blow (19) follows. Infernal Blaze opens the chain Thunderbolt Strike (10)
        follows, both from 25 m. A chain skill keeps its chain open on a roll of its
        own chain_skill_prob (Skill.java 630-639): the three openers and Booming Strike
        have 100, so no follow-up of the table is lost to the roll.
      - **A counter skill.** Parrying Strike (level 25) has counter_skill PARRY. The
        server takes it only within 5 s of a parry of the Chanter's own (Skill.java
        163-169), and the bot does not observe its parries.
      - **The mantras.** Celerity Mantra (level 10), Shield Mantra (13, 18, 23), Revival
        Mantra (22) and Wind Mantra (25) are toggles of the sub type CHANT in the slot
        NOSHOW. They cost nothing and have no timer. While one is on,
        AuraEffect.startEffect 75-77 applies its effect skill every 6.5 s, for 6.5 s
        each; a Chanter alone gets it itself (onPeriodicAction 45-68). Celerity adds
        1,000 to the speed stat, which is the run speed times 1,000
        (PlayerGameStats.getMovementSpeed 101-123). Shield adds 20 physical defence, 40
        block, 40 parry and 20 evasion at its first rank. Revival heals 40 HP every 3 s.
        Wind adds flight speed and flight time. A Chanter may keep three at once: a
        fourth ends the first (EffectController.addEffect 76-86; the port has the same
        lines). The four share one cooldown id, 1519, and only Wind Mantra sets a time
        on it, 10 s. Every start and every end tells the caster with SM_SKILL_ACTIVATION
        (Effect.startEffect 670-678, endEffect 735-737), a death included
        (EffectController.removeAllEffects 660-671).
      - **Class rewards on the route** are the Cleric's at every point (the table under
        NR-42): the staff 101500498 at the ceremony and the dispatch Q2904 (NR-30), the
        chain rewards and the staves of NR-32, the stone 140000001 (NR-39), Haramel's
        chest 700832 (NR-40) and the chain coin armor at Lohaban and Vebna (NR-38).
        Nothing new.
    - **The class line**, as it stood: `priest-chanter`, a Priest who becomes a Chanter
      (SETPRO13), SIM account 41. Its character name is the Cleric's line's, Asimnjour;
      a round gives its bots their names (NR-47). The line names no ceremony item: the
      bridge's reviewed pick, the staff, is taken.
    - **The gear table**, as it stood (Sc/Classes/NaturalClassGearTable.cs, Chanter).

      | Part | The Chanter's | From |
      |---|---|---|
      | Weapon group | staff, then mace; ranked by magic boost, then the most damage | NR-Q5, CP-Q7 |
      | Off hand | nothing | CP-Q10 |
      | Armor | chain, leather, robe, clothes; item level first, the type breaks ties | NR-Q7, CP-Q24 |
      | Bonus order at a reward | magic boost, magical accuracy, healing boost, then mana and concentration | NR-32 |
      | Kept | life potions, its help kit, the mana potions, every help scroll and food | as the Cleric from Ascension |

      Six of its eight attack roles hit with the staff's physical attack; only Infernal
      Blaze and Smite are spells. CP-Q7 ranks its staff by magic boost all the same.
      Every staff a quest or a coin vendor of the route offers it is the Cleric's, so the
      order decides nothing there. It is the operator's to change.
    - **The help kit from level 10** is every row of the manifest written under NR-60
      (NaturalHelpItemAllowlist.Kit(caster: true, reagent: true)), which is the Cleric's.
      Levels 1 to 9 are the Priest's kit (CP-Q12).
    - **Its skills by role** (Sc/Classes/NaturalChanterProfile.cs; 58 skills of 64).

      | Role | Skill, and the levels of its ranks | What it is |
      |---|---|---|
      | infernal | Infernal Blaze: 7, 14, 19, 24 | Opens the chain from range; one time in two a 3 s stun. Instant, 25 m, 24 s. |
      | thunderbolt | Thunderbolt Strike: 10, 15, 20, 25 | Follows Infernal Blaze inside 3 s, a hit of the weapon from 25 m. Instant, 22 s. |
      | smite | Smite: 1, 6 | The Priest's spell from 25 m; it has no rank after 6. 1.5 s cast, 2 s. |
      | hallowed | Hallowed Strike: 3, 8, 13, 18, 23 | Opens the chain of three and slows the target's attacks. 8 s. |
      | booming | Booming Strike: 10, 15, 20, 25 | Follows Hallowed Strike and lowers physical defence. 6 s. |
      | crashing | Crashing Strike: 13, 18, 23 | Follows Booming Strike: the hardest hit of the three. 6 s. |
      | meteor | Meteor Strike: 10, 15, 20, 25 | Opens the second chain and lowers physical defence. 8 s. |
      | incandescent | Incandescent Blow: 19, 24 | Follows Meteor Strike. 8 s. |
      | circle | Winter Circle: 10 | 2,000 DP: twice the physical attack for 30 s. 60 s. |
      | heal | Healing Light: 1, 6, 11, 16, 21, 26 | The Priest's heal. 2 s cast, no cooldown, 13 to 75 MP. |
      | revival | Word of Revival: 13, 18, 23 | A heal at once and every 2 s for 38 s. Instant, 5 s, 52 to 76 MP. |
      | ward | Protective Ward: 22 | For 10 s it takes three tenths of every hit. 74 MP, 2 min. |
      | blessing | Blessing of Guardianship: 5; Protectorate's Prayer: 10, 15, 20, 25 | The Priest's blessing and its next ranks. 41 to 56 MP. |
      | promise | Promise of Earth: 16, 21, 26 | For 30 min one weapon hit in ten strikes with earth as well. 60 to 85 MP. |
      | rage | Rage Spell: 20 | For 30 min 15% more physical attack, and heals are cast a fifth faster. 379 MP. |
      | binding | Binding Word: 20, 25 | For 5 s the target moves at half speed and casts no physical skill. 221 to 251 MP, 30 s. |
      | herb, mp-recovery | Herb Treatment and MP Recovery: 10, 15, 20, 25 | The two powder skills of a rest (NR-50a). |

    - **The rule table, natural-chanter-v1.** The Priest's, with what the Chanter adds.
      From range: Infernal Blaze and Thunderbolt Strike at once after it, both instant,
      then Smite while the monster comes. With the monster on it: Winter Circle when the
      DP are there; the same pair when it is ready again; Hallowed Strike, Booming
      Strike and Crashing Strike; Meteor Strike and Incandescent Blow; Smite last. An
      open follow-up is always cast first, and the staff swings between skills.
      Protectorate's Prayer goes up before the first hit, and Word of Revival is kept up
      while the Chanter is being hit. Promise of Earth and Rage Spell are kept up between
      fights. The ladder is the Priest's with one step more: the shield scroll at 50% HP,
      Protective Ward at 60%, the life potion at 90%, Healing Light at 55% against one
      attacker and at 70% against two or more; the potion's and the heal's percentages
      are the run's, as the Priest's. Once a fight has had its heal and the target is at
      or below 15% HP, Smite finishes it in the heal's place. Healing Light's cost is
      kept back from every attack. It leaves at three attackers, or at 30% HP with
      nothing of the ladder left, and casts Binding Word on its target first. Its rest,
      its ranges, its readiness and its restock are the Priest line's. It holds for a
      patrol and assesses (NR-37) with Healing Light and Word of Revival to ask about,
      and a pull may bring two. In flight it shoots with the list it has at range
      (NR-36).
    - **Left out, with the reason** (6 skills).

      | Skill, levels | Why it is not cast |
      |---|---|
      | Return, Bandage Heal, Escape: 1 | As for every class (CP-35, CP-Q11). |
      | Light of Resurrection: 10 | It revives another player; the bot plays alone. |
      | Booming Smash: 22 | It takes Booming Strike's place after Hallowed Strike: a weaker hit with an 8 s snare, and Crashing Strike does not follow it. The table keeps the chain of three. |
      | Parrying Strike: 25 | A counter skill (above): the bot does not observe its own parries. |

      Not in the catalog at all: the six mantra rows, which are toggles (NR-70a);
      Homeward Bound, which is a book; and the stone's skill, as for every class (NR-39).
    - **Proof.** The one-time check, not committed (run/nr/NR-70/check.log): the line
      parses and holds both classes; its character is a Priest until the client observes
      a Chanter; NaturalClassProfiles builds the Chanter's profile, which requires the
      validator: each of the 64 active skills has one role or one reason, every follow-up
      has its opener, no rotation line breaks a chain or casts a counter skill, and the
      gear groups lie inside the masteries. The check prints the 58 rows, the 6 reasons,
      the best rank of each role at levels 10, 13, 16, 20, 25 and 26, the gear groups,
      the patrol view and the six mantra rows. Its first run (check-a1.log) was refused
      by the validator: the table had Parrying Strike in its list, a counter skill.
      Gate, set all+mage+warrior+artist+engineer+scout+templar, -Parallel 8, run guard-p8
      (run/nr/NR-70/guard-p8/verdict.json): verdict pass, all thirteen scopes identical.
      Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629 passed, 16
      skipped) and Fast passes (run nr70-fast, 11 passed).
- [x] **NR-70a - A toggle kept on.** Depends: NR-70
  - Work: The form NR-50d left to the first class that keeps a toggle. Java first, beyond
    what NR-50d and NR-70 read: what else ends a mantra (a logout, a move to another map,
    a fourth mantra), the cooldown the four share, and what the client is told of a
    toggle (SM_SKILL_ACTIVATION, SM_MANTRA_EFFECT, the effect skill among its effects).
    The bot's world state observes which toggles are on. A profile names, in order, the
    toggle roles it keeps on; the buff check between fights casts one that is observed
    off and never one that is on. The Chanter names Celerity Mantra, Shield Mantra and,
    from level 22, Revival Mantra: three, which is what the server lets a Chanter keep.
    Wind Mantra is for flight and stays off. The bot moves at the speed the server gives
    it, as with the running scroll. Generic: roles and an order in the profile, no branch
    on a class; the Gladiator's, the Assassin's and the Rider's toggles use the same form.
  - Proof: A one-time check, or one probe row on a probe account, that shows each named
    mantra on after the rule ran once, none cast while it is on, and each cast again
    after it was ended; the full gate identical.
  - 2026-10-10: done. A profile names the toggles it keeps on, the world state knows
    which are on from the server's own word, and the buff check casts one only when it
    is off. The Chanter keeps Celerity Mantra, Shield Mantra and Revival Mantra.
    - **Java, beyond NR-50d and NR-70.**
      - **What ends a mantra.** A death (CreatureController.onDie 157;
        EffectController.removeAllEffects 656-671 ends every effect that may be removed
        on death, and the mantras carry no noremoveatdie). A logout
        (PlayerLeaveWorldService 103-112 ends every effect; PlayerEffectsDAO
        .storePlayerEffects 63-67 stores abnormal effects only, and
        EffectController.getAbnormalEffects 688-690 holds no toggle of the slot NOSHOW),
        so after a login none is on and none is told. Entering a map that takes buffs
        away (PlayerController.onEnterWorld 253-257). A fourth mantra
        (EffectController.addEffect 76-86). The client's own CM_TOGGLE_SKILL_DEACTIVATE
        (runImpl 31-42). A teleport or a move to another map ends none: TeleportService
        216 only sends the effect icons again.
      - **A mantra that is on is not cast.** AuraEffect.applyEffect 35-42 adds nothing
        and logs the cast as an abuse of the cast packet.
      - **A new rank.** The ranks of one mantra share one effect id; the server ends
        the older for the newer unless the older is the stronger
        (EffectController.searchConflict 124-156). Wind Mantra's effect has Celerity
        Mantra's effect id, 112141, so the server would end the one for the other.
      - **What the client is told.** SM_SKILL_ACTIVATION, to the caster alone, at every
        start and every end: the skill, 0, and whether it is on, seven bytes; the middle
        number is 1 for a removed stigma. SM_MANTRA_EFFECT every 6.5 s to those around
        (AuraEffect.onPeriodicAction 67). The mantra's effect skill is among the
        caster's visible effects for 6.5 s at a time, so the list of effects is no steady
        sign of a mantra; the activation packet is.
      - **The cooldown** the four share, id 1519, is 0 s for three of them and 10 s for
        Wind Mantra.
      - **The port** has the same lines (Controllers/Effect/EffectController.cs 89-101,
        SkillEngine/Model/Effect.cs 762, 787-793, 831, SkillEngine/Effect/AuraEffect.cs).
        No server change.
    - **The change, generic.**
      - Bots/Protocol/BotServerPacketDecoder.cs decodes SM_SKILL_ACTIVATION: 119 packet
        types, and the decoder's own test counts them so.
      - Bots/World/BotEffectWorldState.cs: the world state holds the toggles the server
        said are on and has not said are off. A world reload keeps them; a new login
        starts with none, as the server does.
      - Sc/Classes/NaturalClassProfile.cs: a profile names the toggles it keeps on, each
        a role and a trace name, in the order they are checked. A profile that names
        none keeps none.
      - Sc/NaturalIshalgenJourney.Combat.cs, the buff check before a pull and after a
        rest: after the buffs, the best learned rank of each kept role is cast when the
        world state does not hold it, outside a fight, and traced with what the server
        then said. One that is on is not cast.
      - Sc/Classes/NaturalProfileValidator.cs and NaturalSkillCatalog.cs: every toggle a
        class learns by itself gets a role or a reason, as its active skills do. A
        toggle is in no rotation line. A kept toggle is a toggle and no stance. The kept
        ones are no more than the server keeps at once: three mantras; of the other
        toggles of the slot NOSHOW one, and two for a Ranger or a Rider.
      - SimT/SimulationFastScenarioTests.cs: a SIM trace now holds the server's
        SM_SKILL_ACTIVATION. No recorded scope receives one.
      - Sc/NaturalIshalgenJourney.cs: RunObservedBuffCheckAsync, the journey's buff check
        alone, for a probe.
    - **The Chanter.** Celerity Mantra has the role celerity, Shield Mantra's three
      ranks shield-mantra, Revival Mantra revival-mantra, and the profile keeps the
      three. Wind Mantra is left out with its reason: it serves flight, and the three
      kept are all the server keeps; it could not be on beside Celerity Mantra either
      (above). Of its 70 skills, 64 active and 6 toggles, 63 have a role now and 7 a
      reason.
    - **The Templar.** Stubborn Spirit's four ranks are left out with NR-50d's reason:
      a toggle that is a stance too, which the first skill cast ends.
    - **Proof, the one-time check** (run/nr/NR-70a/check.log; the check file is not
      committed). Every profile of the nine class lines is built, which requires the
      validator: the Chanter has six toggles to level 26, five with a role and one with
      a reason; the Templar has four, each with a reason; no other profile has one. The
      Chanter keeps none at level 9, Celerity from 10, Shield Mantra from 13 with its
      newest rank, Revival from 22. The validator refuses, each by name: four mantras
      kept; a mantra with neither a role nor a reason; a mantra in a rotation line; a
      kept role that holds no toggle, and one that holds nothing; the Templar's stance
      kept. The world state takes Java's golden payloads: on, a removed stigma changes
      nothing, a world reload keeps the toggle, off; a payload of six bytes is refused.
    - **Proof, the probe row** chanter-mantras (SimT/SimulationNaturalStarterProbeTests
      .cs, probe account 98; `bash run/nr/NR-70a/probe.sh <attempt>`). Prepared by the
      director: a level-22 Chanter with the skills of every level up to it, in Altgard.
      Every other act is the journey's buff check. Run nr70a-probe-a1 passed with the
      server's word not yet in the trace; run nr70a-probe-a2, with it, passed, and so
      did nr70a-probe-a3 on the committed tree:
      - **First check.** It casts Protectorate's Prayer, Promise of Earth and Rage
        Spell, then Celerity Mantra 1809, Shield Mantra II 1658 and Revival Mantra 1746.
        The server said each of the three is on, and has all three. The speed the
        client moves at went from 6 to 7 m/s.
      - **Second check.** No cast, and the server said nothing of a toggle.
      - **Ended.** The director ends every effect, as a death does: the server said
        each of the three is off.
      - **Third check.** Protectorate's Prayer and the three mantras again, each said
        on. Promise of Earth and Rage Spell were still cooling down, 10 s and 15 s.
      - **A new rank.** The director makes it level 23, where Shield Mantra III 1659
        is learned. The check casts 1659 alone; the server said 1658 off and 1659 on.
      - A level-23 Chanter has 1,847 HP and 2,688 MP: Rage Spell's 379 MP is a seventh
        of it. NR-71's row at level 20 shows it where the spell is learned.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout+templar, -Parallel 8,
      run guard-p8 (run/nr/NR-70a/guard-p8/verdict.json): verdict pass, all thirteen
      scopes identical. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629
      passed, 16 skipped) and Fast passes (run nr70a-fast2, 11 passed). The first bundle
      (run/nr/NR-70a/bundle-a1/) failed one check: the probe row had an assertion the
      xunit analyzer warns of (XUNIT2029), one warning over the baseline. The assertion
      was reworded, the row played again, and the bundle run again from the start.
- [ ] **NR-71 - Chanter: probe rows.** Depends: NR-70a
  - Work: Rows chanter-10, chanter-16, chanter-20 and chanter-25 in
    SimulationNaturalStarterProbeTests: prepared Chanters on the two probe accounts, in
    the gear the route has given by that level, fight the monsters the Cleric's rows
    fight. Each row's trace shows the table in play: Infernal Blaze and Thunderbolt
    Strike from range, the chain of three, Meteor Strike with Incandescent Blow from 19,
    Winter Circle at 2,000 DP, Protectorate's Prayer, Promise of Earth and Rage Spell up,
    Word of Revival while it is hit, Protective Ward and the ladder, the mantras on, the
    rest with the powder. What a row shows decides the open rules: whether Rage Spell's
    379 MP can be paid at level 20, and whether Smite earns its 1.5 s cast once the
    monster is on the Chanter. A fix is one small change (rule (i)).
  - Proof: The four rows end with the monster dead or a recorded retreat, no refused
    cast repeated and no skill outside the table cast.
- [ ] **NR-72 - Chanter: to Altgard.** Depends: NR-71; ticked by the round that gives it
  - Work: A fresh character of the line priest-chanter plays Ishalgen as a Priest, the
    trial, the Chanter choice at Munin, the ceremony with the staff and the dispatch
    Q2904, and is captured at the Altgard bind as altgard-chanter-s1. The preserved
    pandaemonium-chanter-start-s1 is left as it is: it was played by the placeholder.
  - Proof: The capture verifies.
- [ ] **NR-73 - Chanter: Altgard legs l1 to l5.** Depends: NR-72; ticked by its round
  - Work: The Cleric's legs in order, each end captured under the Cleric's snapshot name
    with -chanter (NR-41). In leg 1 it shoots the fungus in flight.
  - Proof: Each capture verifies.
- [ ] **NR-74 - Chanter: Altgard legs l6 to l11.** Depends: NR-73; ticked by its round
  - Work: As NR-73. Leg 11 is the destiny quest: its stone is the Cleric's, 140000001
    (NR-39).
  - Proof: Each capture verifies.
- [ ] **NR-75 - Chanter: coin gear and Haramel.** Depends: NR-74; ticked by its round
  - Work: The coin-gear leg (chain, by its manifest, NR-38a) and Haramel with chest
    700832 (NR-40), captured as altgard-coingear-chanter and
    altgard-rc-complete-s1-chanter.
  - Proof: Each capture verifies.
- [ ] **NR-76 - Chanter: the Abyss entry.** Depends: NR-75; ticked by its round
  - Work: Q24020, then Q2945, Q2946, Q2947 and Q2042 with the coin tiers it can wear
    (NR-38b, NR-Q14, NR-Q16), captured as morheim-abyss-entry-s1-chanter.
  - Proof: The capture verifies.
- [ ] **NR-77 - Chanter: the endpoint.** Depends: NR-76; ticked by its round
  - Work: Captured and verified as ntc-ready-chanter-s1: alive at Morheim Ice Fortress,
    level 25 or higher, Q2945, Q2946, Q2947 and Q2042 complete.
  - Proof: The capture verifies.
- [ ] **NR-78 - Chanter: the class scope.** Depends: NR-77
  - Work: One scope of the Chanter's play, chosen where its rules differ most from the
    Cleric's, recorded by a bot alone, twice, and added to the gate's sets.
  - Proof: The two recordings are identical.
- [x] **NR-80 - Gladiator: survey and profile data.** Depends: the close of phase C
  - 2026-10-10: done. The Gladiator has a class line, a gear table and a profile the
    validator accepts. Nothing has been played. One thing its skills need that the shared
    forms cannot say yet is item NR-80a below.
    - **Java.**
      - **What it learns.** SkillLearnService as read for NR-50. Of the Gladiator's 100
        rows to level 26, 79 are auto-learned, 20 are stigma rows from level 20 and one
        is a skill book, Homeward Bound at 21. 50 of the auto-learned are active skills,
        and with the six ranks it keeps from the Warrior the catalog has 56 to give a
        role or a reason; two more are toggles (run/nr/NR-80/check.log).
      - **Chains.** ChainCondition as read for NR-50. Ferocious Strike opens the first
        chain; Robust Blow and Rage are its second steps; Wrathful Strike (level 13) and
        Rupture (16) are both third steps after Robust Blow. Corrected by NR-80a:
        Wrathful Strike carries no chain_skill_prob, which is 100, and keeps the chain
        open, so Rupture is cast after it; Rupture carries 10 (Skill.java 630-639), and
        after it the chain is over nine times in ten; the cast's result says which.
        Body Smash opens a chain of its own. Absorbing Fury (10) opens the
        chain Roiling Hack follows. Cleave (19) opens a chain nothing follows before
        level 26.
      - **A target in a state.** Crashing Blow (level 25) names target_status
        OPENAERIAL. Properties.validateEffectedList 103-117 and TargetStatusProperty.set
        20-28 refuse a skill whose first target has none of the states it names. Aerial
        Lockdown (22) puts its target into that state for 2 s by its sub-effect, Aether's
        Hold 8224. 139 templates of the shipped data name a target status; the port has
        the same check (SkillEngine/Properties/TargetStatusProperty.cs).
      - **Toggles.** Slaughter (13) and Defense Preparation (25) are toggles of the slot
        NOSHOW and no mantras, and no stances. The server keeps one such toggle on
        (EffectController.addEffect 76-86, as read for NR-70): the second ends the first.
      - **Class rewards on the route** are those of the table under NR-42: the
        greatsword 100900488 at the ceremony and the dispatch Q2901 (NR-30), the plate
        rewards and the greatswords of NR-32, the stone 140000003 (NR-39), Haramel's
        chest 700829 (NR-40) and the plate coin armor with a greatsword at Lateni and
        Nott (NR-38). Nothing new.
    - **The class line.** `warrior-gladiator`: a Warrior who becomes a Gladiator, SIM
      account 41, character Asimgladiator, the greatsword 100900488 at the ceremony
      (Q2009; NR-Q5). Its dispatch quest is Q2901 (NR-30). Its Warrior holds nothing in
      the off hand, as the line `warrior` does. It is in NaturalClassLine.All and in the
      script's list; the script test holds the two together.
    - **The gear table** (Sc/Classes/NaturalClassGearTable.cs, Gladiator).

      | Part | The Gladiator's | From |
      |---|---|---|
      | Weapon groups | greatsword, then polearm; ranked by the physical stat | NR-Q5, CP-Q7 |
      | Off hand | nothing | CP-Q10 |
      | Armor | plate, chain, leather, robe, clothes; item level first, the type breaks ties | NR-Q7, CP-Q24 |
      | Bonus order at a reward | physical attack, critical, accuracy, then HP | NR-32 |
      | Kept | life potions, its help kit, a mana potion it finds, every help scroll and food | as the Cleric from Ascension |

      NR-Q5 asks for the best two-hand weapon and gives damage per second as the
      default. The table's form ranks a group before its numbers, and the order
      greatsword, polearm is that default at the ceremony: the Karmic Greatsword swings
      for 93 to 99 every 2.4 s, 40.0 a second, and the Karmic Spear for 71 to 133 every
      2.8 s, 36.4 a second, though by the stat of one swing the spear is ahead. Every
      row NR-32 and NR-38 wrote for the Gladiator names a greatsword and plate, which is
      what this table ranks first. A polearm of a higher level from a drop would stay in
      the bag; a table that ranks its groups together by damage per second is not
      written, because nothing on the route asks for it.
    - **The help kit from level 10** is the manifest written under NR-50
      (NaturalHelpItemAllowlist.Kit(caster: false, reagent: true)), the Templar's: no
      mana serum and no Awakening scroll. Levels 1 to 9 are the Warrior's kit (CP-Q12).
    - **Its skills by role** (Sc/Classes/NaturalGladiatorProfile.cs; 47 skills of 58).

      | Role | Skill, and the levels of its ranks | What it is |
      |---|---|---|
      | strike | Ferocious Strike: 1, 6, 11, 16, 21, 26 | Opens the first chain. 10 s. |
      | robust | Robust Blow: 3, 8, 13, 18, 23 | Follows Ferocious Strike inside 3 s. 8 s. |
      | rage | Rage: 7, 12, 17, 22 | Follows Ferocious Strike: attack and a shield for 10 s. 17 to 26 MP, 24 s. |
      | wrathful | Wrathful Strike: 13, 18, 23 | Follows Robust Blow; seven times in ten it throws the target down for 2 s. 28 to 44 MP, 20 s. |
      | rupture | Rupture: 16, 21, 26 | Follows Robust Blow too: the harder hit, for no mana. 8 s. |
      | smash | Body Smash: 5, 10, 15, 20, 25 | Opens a chain of its own. 12 s. |
      | explosion | Explosion of Rage: 10, 15, 20, 25 | 2,000 DP: a hit that cannot miss and throws the target down for 2 s. 10 s. |
      | cleave | Cleave: 19, 24 | Thrown from 15 m; one time in two it halves the target's speed for 6 to 8 s. 23 to 26 MP, 18 s. |
      | taunt | Taunt: 10, 15, 20, 25 | Sets a monster 15 m away on the Gladiator; no damage. Free, 10 s. |
      | aerial | Aerial Lockdown: 22 | A hit that lifts the target for 2 s. 40 MP, 3 min. |
      | wind | Second Wind: 20 | Heals 35% of its HP and raises the most it has by 35% for a minute. 298 MP, 3 min. |
      | slaughter | Slaughter: 13 | A toggle: 15% more physical attack while it is on. Free. |
      | herb, mp-recovery | Herb Treatment and MP Recovery: 10, 15, 20, 25 | The two powder skills of a rest (NR-50a). |

    - **The rule table, natural-gladiator-v1.** The Warrior's, with what the Gladiator
      adds. With the monster on it: Explosion of Rage when the DP are there; Ferocious
      Strike and Robust Blow; Rage when it is at or below 80% HP; after Robust Blow,
      Wrathful Strike when it is ready and then Rupture (corrected by NR-80a); then
      Body Smash, Aerial Lockdown and Cleave. An open follow-up is always cast first,
      and the weapon swings
      whenever no skill is ready. It pulls as the Templar does (NR-53b), with the
      Templar's distances: from range Cleave from level 19, otherwise Taunt, and then it
      holds where it stands until the monster is on it. The ladder: the shield scroll at
      50% HP, the life potion at or below 75%, Second Wind at or below 45%; Second
      Wind's 298 MP are kept back from every attack. It leaves at three attackers, or at
      25% HP with nothing ready. Between fights it rests as the Templar (NR-50a) and
      keeps Slaughter on (NR-70a). It holds for a patrol and assesses (NR-37) with Second
      Wind to ask about, and a pull may bring two. In flight it throws Cleave from level
      19 and swings its weapon before (NR-36).
    - **Left out, with the reason** (11 skills).

      | Skill, levels | Why it is not cast |
      |---|---|
      | Return, Bandage Heal, Escape: 1 | As for every class (CP-35, CP-Q11). |
      | Absorbing Fury: 10, 15, 20, 25; Roiling Hack: 10 | They hit up to three monsters within 7 m of the Gladiator. The bot pulls one at a time, and an area skill wakes every other one in reach. |
      | Charge: 16 | Run speed for 13 s; the journey's travel casts no skill. |
      | Crashing Blow: 25 | It needs its target in the air, which it is for 2 s after Aerial Lockdown. No rule says that of a skill yet. NR-80a. |
      | Defense Preparation: 25 | A toggle of Slaughter's kind, and the server keeps one of them on. It raises parry, block and enmity; Slaughter's attack is kept. |

      Not in the catalog at all: Homeward Bound, which is a book, and the stone's skill
      11506, as for every class (NR-39).
    - **Proof.** The one-time check, not committed (run/nr/NR-80/check.log): the line
      parses and holds both classes; its character is a Warrior, with an empty off hand,
      until the client observes a Gladiator; NaturalClassProfiles builds the Gladiator's
      profile, which requires the validator: each of the 56 active skills and the two
      toggles has one role or one reason, every follow-up has its opener, no rotation
      line breaks a chain, the one kept toggle is within what the server keeps, and the
      gear groups lie inside the masteries. The check prints the skill tree, the 47
      rows, the 11 reasons, the best rank of each role at eight levels, the gear groups,
      the score of the ceremony's three weapons (the greatsword first) and the patrol
      view. Gate, set all+mage+warrior+artist+engineer+scout+templar, -Parallel 8, run
      guard-p8 (run/nr/NR-80/guard-p8/verdict.json): verdict pass, all thirteen scopes
      identical. Seven pre-commit checks pass, the three script tests pass
      (run/nr/NR-80/script-tests.log), Aion.GameServer.Tests passes (4,629 passed, 16
      skipped) and Fast passes (run nr80-fast, 11 passed).
- [x] **NR-80a - A skill that needs its target in a state.** Depends: NR-80
  - Work: Crashing Blow hits a target that is in the air for more than twice what
    Rupture does, and the Gladiator's own Aerial Lockdown puts it there for 2 s. Other
    classes have skills of the kind: 139 templates name a target status. Java first:
    TargetStatusProperty, what puts a target into each state and for how long, and what
    the client is told of a monster's state. The skill row holds the states its target
    must have. The table casts such a skill only while the bot has seen one of them on
    its target, and the validator refuses one in a rotation line whose catalog has no
    skill that causes it. Generic: the states in the row, no branch on a class.
  - Proof: A one-time check of the decisions with the state seen and not seen, and one
    probe row on a probe account that casts Crashing Blow after Aerial Lockdown; the
    full gate identical.
  - 2026-10-10: done. The bot knows a creature's abnormal states from the server's own
    packet, a skill row holds the states its target must be in, and the rule table casts
    such a skill first while one of them is seen and at no other time. The Gladiator
    casts Crashing Blow.
    - **Java, beyond NR-80.**
      - **What puts a target into the air.** OpenAerialEffect.calculate 31-51: not a
        target that is pulled, thrown down, in the air, staggered or spinning, and the
        target may resist it (OPENAERIAL_RESISTANCE). startEffect 54-68 sets the state
        on the target and endEffect 71-73 takes it off. Aether's Hold, the sub-effect of
        Aerial Lockdown, lasts 2 s.
      - **What the client is told.** EffectController.addEffect starts an effect and
        then calls broadCastEffects 304-308; clearEffect 310-325 calls it when one ends.
        It sends SM_ABNORMAL_EFFECT to everyone who sees the creature: its object id, 1
        for a monster or 2 for a player, its abnormal states as bits, and its visible
        effects. The bits are the ids of AbnormalState; OPENAERIAL is 1 << 16.
        PlayerController.see 110-111 sends the packet for a creature that comes into
        sight with effects on it.
      - **A chain skill without chain_skill_prob has 100** (SkillTemplate.java 90-91), not
        none. NR-80 read it wrong: Wrathful Strike keeps its chain open, and Rupture,
        which follows Robust Blow by the step before the current one, is cast after it.
        The probe row below shows the four in a row. NR-80's record and the profile's
        comment are corrected.
    - **What the reading changed.** The Work line asked the validator to refuse a
      state-bound skill whose catalog has no skill that causes the state. It asks
      nothing: such a skill is sent only while the state is seen, so the server can
      refuse none, and one whose state never comes is never sent.
    - **The change, generic.**
      - Bots/Protocol/BotServerPacketDecoder.cs decodes SM_ABNORMAL_EFFECT: 120 packet
        types, and the decoder's own test counts them so.
      - Bots/World/BotEffectWorldState.cs: the world state holds each creature's states
        as the server last told them, forgets them at a world reload and when the
        creature leaves sight.
      - Sc/NaturalPriestCombatPolicy.cs and Sc/Classes/NaturalSkillCatalog.cs: a skill
        row holds the states of its template's target_status, and the fight's
        observation the target's states. The decision record's observed state is as it
        was, so no recorded trace changes; the decision's checks say what was found.
      - Sc/Classes/NaturalRotationCombatPolicy.cs: a skill whose target is in none of
        its states is refused, as the server would. One that is ready is cast first,
        before an open follow-up: the state lasts a moment. One whose state is not seen,
        or that cools down, is out of the line, and nothing waits for it.
      - No profile but the Gladiator's has such a row to level 26.
    - **The Gladiator.** Crashing Blow has the role crashing: 48 of its 58 skills have a
      role now and 10 a reason.
    - **Found, not changed here.** The table keeps the mana of its first recovery skill
      back from every attack, and below that amount it refuses the attacks that cost no
      mana too: a Gladiator under 298 MP only swings its weapon. NR-80b.
    - **Proof, the one-time check** (run/nr/NR-80a/check.log; the check file is not
      committed). Of the fifteen profiles of the ten class lines only the Gladiator's has
      a state-bound row. A Gladiator of level 25 on a monster that is on it: with no
      state seen, or thrown down only, it casts Ferocious Strike and the check says
      Crashing Blow's target is in none of its states; in the air, Crashing Blow, with a
      follow-up open too; in the air with Crashing Blow cooling down, the follow-up. At
      level 24, which has not learned it, the table never asks. The world state takes
      Java's golden payloads: a creature in the air, the same told again with none, and
      none after a world reload; a monster's and a player's effect rows are read, and a
      payload a byte too long is refused.
    - **Proof, the probe row** gladiator-aerial (SimT/SimulationNaturalStarterProbeTests
      .cs, probe account 100; `bash run/nr/NR-80a/probe.sh <attempt>`). Prepared by the
      director: a level-25 Gladiator with the skills of every level up to it, with the
      sword it was created with so that a fight lasts, by the starved mosbears of the
      Cleric's row. Every other act is the journey's. Runs nr80a-probe-a1 and, after the
      comment was corrected, nr80a-probe-a2 passed alike:
      - The buff check casts Slaughter, and the server says it is on (NR-70a).
      - One fight, a kill in 10.6 s: Cleave from range, then Ferocious Strike, Robust
        Blow, Wrathful Strike, Rupture, Body Smash, Aerial Lockdown and, 1,002 ms after
        it, Crashing Blow, decided because the target was seen in the air. The server
        completed the cast, which it does only on a target that is in the air.
      - A level-25 Gladiator has 2,326 HP and 1,853 MP.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout+templar, -Parallel 8,
      run guard-p8 (run/nr/NR-80a/guard-p8/verdict.json): verdict pass, all thirteen
      scopes identical. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629
      passed, 16 skipped) and Fast passes (run nr80a-fast, 11 passed).
- [x] **NR-80b - An attack that costs no mana is not held back by the reserve.** Depends: NR-80a
  - Work: The rule table keeps the mana of its first recovery skill back from every
    attack (NR-10). Below that amount it refuses every attack, the ones that cost no
    mana too: a Gladiator under 298 MP and a Templar under 113 MP only swing, though
    Ferocious Strike, Robust Blow, Rupture and Body Smash cost nothing. No server
    behavior is relied on. A skill of the table's attack lists whose mana cost is 0 is
    not refused for the reserve. Generic: the rule of every table. A skill outside the
    lists keeps its refusal, because a recorded decision writes each skill's refusals
    and rule (c) guards the recorded scopes: of the thirteen baseline traces only the
    Cleric's scope c names a free skill as refused for the reserve, in eleven decisions,
    and those are Herb Treatment, MP Recovery and Penance, which no list holds.
  - Proof: A one-time check of the decisions below the reserve for a Gladiator, a
    Templar and a Cleric; the full gate identical.
  - 2026-10-10: done. Below its reserve a class goes on with the attacks that cost it
    nothing.
    - **Java.** Nothing is relied on: the reserve is the bot's own rule, and a skill's
      cost is its template's.
    - **The rule as it stood.** Sc/Classes/NaturalRotationCombatPolicy.cs kept the
      reserve, the mana of the table's reserve role or of its first learned recovery
      skill (NR-10), back from every skill but a recovery skill: a skill was refused
      when mana was below its own cost and the reserve together, whatever its cost.
    - **The change.** Nothing is kept back from a recovery skill, as before, nor from a
      skill that costs no mana and stands in one of the table's two attack lists. The
      three places that asked for the reserve ask this: a skill's refusal, the wait of
      a last-resort swing, and the opener rule of NR-11. A skill outside the lists keeps
      its refusal.
    - **The recorded traces** (run/nr/NR-80b/baseline-scan.log, of the thirteen traces
      under run/cp/baseline). The reserve's reason stands in 73 decisions, all of the
      Cleric's scope c. Eleven of them name a skill that costs no mana as refused for it
      alone: Herb Treatment 246 and 247, MP Recovery 249 and 250 and Penance 3867, which
      no list holds, so they read as they did. The Templar's recorded scope was never
      below its reserve.
    - **Proof, the one-time check** (run/nr/NR-80b/check.log; the check file is not
      committed), each on a monster that is on the bot:
      - A Gladiator of level 20, whose reserve is Second Wind's 298 MP. At 297 MP and
        at none it casts Ferocious Strike, with the chain open Robust Blow, and after
        Robust Blow Rupture; Rage, Wrathful Strike and Cleave are held back. Above the
        reserve Wrathful Strike follows Robust Blow.
      - A Templar of level 20, whose reserve is Empyrean Armor's 113 MP. At 100 MP it
        casts Dazing Severe Blow, and with the chain open Robust Blow; Rage and Shield
        Bash are held back.
      - A Cleric of level 20 at 20 MP swings its staff, as before: every attack of its
        lists costs mana, and Herb Treatment, MP Recovery and Penance are still named
        as held back.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout+templar, -Parallel 8,
      run guard-p8 (run/nr/NR-80b/guard-p8/verdict.json): verdict pass, all thirteen
      scopes identical. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629
      passed, 16 skipped) and Fast passes (run nr80b-fast, 11 passed).
- [ ] **NR-81 - Gladiator: probe rows.** Depends: NR-80b
  - Work: Rows gladiator-10, gladiator-16, gladiator-20 and gladiator-25 in
    SimulationNaturalStarterProbeTests: prepared Gladiators on the two probe accounts, in
    the gear the route has given by that level, fight the monsters the Cleric's rows
    fight. Each row's trace shows the table in play: the first chain with Wrathful
    Strike and Rupture, Rage only when hurt, Explosion of Rage at 2,000 DP, the pull by
    Taunt and from 19 by Cleave, Second Wind on the ladder, Aerial Lockdown and Crashing
    Blow, Slaughter on, the rest with the powder. What a row shows decides the open
    rules: whether a class without a shield should pull by Taunt as the Templar does or
    walk in, and whether the 298 MP kept back for Second Wind starve Rage and Wrathful
    Strike at level 20. A fix is one small change (rule (i)).
  - Proof: The four rows end with the monster dead or a recorded retreat, no refused
    cast repeated and no skill outside the table cast.
- [ ] **NR-82 - Gladiator: to Altgard.** Depends: NR-81; ticked by the round that gives it
  - Work: A fresh Asimgladiator plays Ishalgen as a Warrior, the trial, the Gladiator
    choice at Munin, the ceremony with the greatsword and the dispatch Q2901, and is
    captured at the Altgard bind as altgard-gladiator-s1.
  - Proof: The capture verifies.
- [ ] **NR-83 - Gladiator: Altgard legs l1 to l5.** Depends: NR-82; ticked by its round
  - Work: The Cleric's legs in order, each end captured under the Cleric's snapshot name
    with -gladiator (NR-41). In leg 1 it has nothing that reaches from range yet and
    swings its weapon at the fungus in flight, as the Templar did.
  - Proof: Each capture verifies.
- [ ] **NR-84 - Gladiator: Altgard legs l6 to l11.** Depends: NR-83; ticked by its round
  - Work: As NR-83. Leg 11 is the destiny quest: its stone is the Templar's, 140000003
    (NR-39).
  - Proof: Each capture verifies.
- [ ] **NR-85 - Gladiator: coin gear and Haramel.** Depends: NR-84; ticked by its round
  - Work: The coin-gear leg (plate, by its manifest, NR-38a) and Haramel with chest
    700829 (NR-40), captured as altgard-coingear-gladiator and
    altgard-rc-complete-s1-gladiator.
  - Proof: Each capture verifies.
- [ ] **NR-86 - Gladiator: the Abyss entry.** Depends: NR-85; ticked by its round
  - Work: Q24020, then Q2945, Q2946, Q2947 and Q2042 with the coin tiers it can wear
    (NR-38b, NR-Q14, NR-Q16), captured as morheim-abyss-entry-s1-gladiator.
  - Proof: The capture verifies.
- [ ] **NR-87 - Gladiator: the endpoint.** Depends: NR-86; ticked by its round
  - Work: Captured and verified as ntc-ready-gladiator-s1: alive at Morheim Ice Fortress,
    level 25 or higher, Q2945, Q2946, Q2947 and Q2042 complete.
  - Proof: The capture verifies.
- [ ] **NR-88 - Gladiator: the class scope.** Depends: NR-87
  - Work: One scope of the Gladiator's play, chosen where its rules differ most from the
    Templar's, recorded by a bot alone, twice, and added to the gate's sets.
  - Proof: The two recordings are identical.
- [x] **NR-90 - Assassin: survey and profile data.** Depends: the close of phase C, NR-04
  - 2026-10-10: done. The Assassin has a class line, a gear table and a profile the
    validator accepts. Nothing has been played. One thing its skills need that the shared
    forms cannot say yet is item NR-90a below.
    - **Java.**
      - **What it learns.** SkillLearnService as read for NR-50. Of the Assassin's 92
        rows to level 26, 73 are auto-learned, 18 are stigma rows from level 20 and one
        is a skill book, Homeward Bound at 21. 55 of the auto-learned are active skills,
        and with the nine ranks it keeps from the Scout the catalog has 64 to give a
        role or a reason; one more is a toggle (run/nr/NR-90/check.log).
      - **Chains.** ChainCondition as read for NR-50. Swift Edge opens the chain Soul
        Slash follows and Rune Slash (level 21) follows that, one time in ten (corrected
        by NR-90a: Soul Slash's chain_skill_prob is 10). Fang Strike (16) opens the
        chain Beast Kick (26) follows. Rune Carve (13), Surprise Attack, Dash Attack (10)
        and Ambush (25) each open a chain nothing follows before level 26.
      - **Runes.** CarveSignetEffect.applyEffect 31-45: a carving skill ends the rune
        effect its target carries and puts on the one of the next level, up to the
        skill's cap. Rune Carve and Rune Slash carve to three, Fang Strike and Beast
        Kick to five. A rune effect lasts 24 s and is a skill of its own, 8303 for one
        rune to 8307 for five. SignetBurstEffect.calculateDamage 31-47: a burst
        multiplies its damage by the rune level's number and ends the runes. For Pain
        Rune and Binding Rune the numbers are 0.1 with no rune, 0.2, 0.5, 1.0, 1.2 and
        1.5 with five (signet_data_templates.xml, SIGNET1), and Pain Rune's 3 s stun
        comes four times in ten at two runes and always from three.
      - **Skills that move the caster.** DashEffect.calculate 23-37 and
        MoveBehindEffect.calculate 23-37 put the caster at a point by its target that
        the server takes from its geometry, and RandomMoveLocEffect 35 at one 15 m off.
        None sends a forced move: the place is in the cast's result, which the bot does
        not read. Dash Attack, Ambush and Flash of Speed are these.
      - **Counter skills.** Counterattack and Whirlwind Slash carry counter_skill DODGE
        (Skill.java 163-169, as read for NR-50).
      - **A toggle.** Sprinting (13) is a toggle of the slot NOSHOW that spends 3% of
        the Assassin's mana every 6 s while it is on.
      - **Class rewards on the route** are those of the table under NR-42: the dagger
        100200605 at the ceremony and the dispatch Q2902 (NR-30), the leather rewards
        and the daggers of NR-32, the stone 140000003 (NR-39), Haramel's chest 700830
        (NR-40) and the leather coin armor with a dagger at Lateni and Nott (NR-38).
        Nothing new.
    - **The class line.** `scout-assassin`: a Scout who becomes an Assassin, SIM account
      41, character Asimassassin, the dagger 100200605 at the ceremony (Q2009; NR-Q5).
      Its dispatch quest is Q2902 (NR-30). Its Scout holds two daggers from level 5, as
      the line `scout` does (NR-04). It is in NaturalClassLine.All and in the script's
      list; the script test holds the two together.
    - **The gear table** (Sc/Classes/NaturalClassGearTable.cs, Assassin).

      | Part | The Assassin's | From |
      |---|---|---|
      | Weapon group | dagger; ranked by the physical stat | NR-Q5, CP-Q7 |
      | Off hand | a second dagger; a new one takes the hand of the worse | NR-Q6, NR-04 |
      | Armor | leather, robe, clothes; item level first, the type breaks ties | NR-Q7, CP-Q24 |
      | Bonus order at a reward | physical attack, critical, accuracy, then HP | NR-32 |
      | Kept | life potions, its help kit, a mana potion it finds, every help scroll and food | as the Cleric from Ascension |

      Each reward and each coin tier of the route names one dagger for the Assassin
      (NR-32, NR-38). The other hand keeps the dagger it held; what a second purchase at
      a coin vendor would take is the coin-gear leg's to show.
    - **The help kit from level 10** is the manifest written under NR-50
      (NaturalHelpItemAllowlist.Kit(caster: false, reagent: true)), the Templar's: no
      mana serum and no Awakening scroll. Levels 1 to 9 are the Scout's kit (CP-Q12).
    - **Its skills by role** (Sc/Classes/NaturalAssassinProfile.cs; 36 skills of 65).

      | Role | Skill, and the levels of its ranks | What it is |
      |---|---|---|
      | edge | Swift Edge: 1, 6, 11, 16, 21, 26 | Opens the first chain. 7 s. |
      | slash | Soul Slash: 7, 12, 17, 22 | Follows Swift Edge inside 3 s. 6 s. |
      | runeslash | Rune Slash: 21, 26 | Follows Soul Slash one time in ten (NR-90a): the hardest hit of the three, and one rune, to three. 5 s. |
      | fang | Fang Strike: 16, 21, 26 | Opens the second chain: a hit and one rune, to five. 31 to 39 MP, 6 s. |
      | kick | Beast Kick: 26 | Follows Fang Strike: a hit and one rune, to five. 42 MP, 6 s. |
      | carve | Rune Carve: 13, 18, 23 | A light hit and one rune, to three. Free, 7 s. |
      | divine | Divine Strike: 10, 15, 20, 25 | 2,000 DP: the hardest hit it has. 30 s. |
      | devotion | Devotion: 9 | 40% more physical attack for 5 s. 19 MP, 30 s. |
      | eye | Killer's Eye: 10 | Half as much again on the next physical skill inside 15 s, and accuracy. 40 MP, 30 s. |
      | flurry | Flurry: 20 | For 30 s the daggers swing a fifth faster and strike critically more often. 115 MP, 2 min. |
      | poison | Apply Deadly Poison: 15 | For 5 min one swing in five poisons the target, and one in fifty stuns it. 52 MP. |
      | evasion | Focused Evasion: 1 | 5 s in which every hit misses. Free, 30 s. |
      | herb, mp-recovery | Herb Treatment and MP Recovery: 10, 15, 20, 25 | The two powder skills of a rest (NR-50a). |

    - **The rule table, natural-assassin-v1.** The Scout's, with what the Assassin adds.
      With the monster in reach: Devotion when it is ready, then Divine Strike when the
      DP are there, then Swift Edge, Soul Slash and Rune Slash; then Fang Strike and
      Beast Kick; then Rune Carve. Killer's Eye and Flurry are cast when no attack is
      ready, in the time a dagger would swing. An open follow-up is always cast first,
      and the table holds an open chain, as the Scout's does. The daggers swing whenever
      no skill is ready. Nothing is cast from range: it walks in, as the Scout. The
      ladder is the Scout's: Focused Evasion at or below 70% HP, the shield scroll at
      50%, the life potion at or below 75%. It leaves at three attackers, or at 25% HP
      with nothing ready. Between fights it rests as the Templar (NR-50a) and keeps
      Apply Deadly Poison up. It holds for a patrol and assesses (NR-37), and a pull may
      bring two. In flight it swings its daggers (NR-36).
    - **Left out, with the reason** (29 skills).

      | Skill, levels | Why it is not cast |
      |---|---|
      | Return, Bandage Heal, Escape: 1 | As for every class (CP-35, CP-Q11). |
      | Surprise Attack: 3, 8, 13, 18, 23 | As the Scout (CP-Q16): it does little from the front, opens its own chain and so resets Swift Edge's. |
      | Counterattack: 5, 10, 15, 20, 25; Whirlwind Slash: 25 | Counter skills: the server takes them only within 5 s of a dodge of the Assassin's own, which the bot does not observe. |
      | Stealth: 5 | As the Scout: not in combat, and the journey does not sneak. |
      | Dash Attack: 10, 15, 20, 25; Ambush: 25 | They move the Assassin to its target or behind it, to a place only the cast's result names (above). The bot walks checked routes only. |
      | Flash of Speed: 17 | It throws the Assassin 15 m to a place the server picks. |
      | Pain Rune: 13, 18, 23; Binding Rune: 25 | They burst the runes on their target: a tenth of the damage with none, all of it with three. No rule counts the runes on a target yet. NR-90a. |
      | Calming Whisper: 19, 24 | It lowers the enmity of its target; alone, the monster has no one else to turn to. |
      | Aethertwisting: 22 | It resists the next two spells inside 8 s; no rule times a defence against a hit that has not come. |
      | Sprinting: 13 | A toggle that spends 3% of its mana every 6 s for a fifth more speed; the kit's running scroll is its speed. |

      Not in the catalog at all: Homeward Bound, which is a book, and the stone's skill
      11506, as for every class (NR-39).
    - **Proof.** The one-time check, not committed (run/nr/NR-90/check.log): the line
      parses and holds both classes; its character is a Scout, with a second weapon in
      the off hand, until the client observes an Assassin; NaturalClassProfiles builds
      the Assassin's profile, which requires the validator: each of the 64 active skills
      and the one toggle has one role or one reason, every follow-up has its opener, no
      rotation line breaks a chain or casts a counter skill, and the gear groups lie
      inside the masteries. The check prints the skill tree, the 36 rows, the 29
      reasons, the best rank of each role at eight levels, the gear groups and the
      patrol view. Gate, set all+mage+warrior+artist+engineer+scout+templar, -Parallel
      8, run guard-p8 (run/nr/NR-90/guard-p8/verdict.json): verdict pass, all thirteen
      scopes identical. Seven pre-commit checks pass, the three script tests pass
      (run/nr/NR-90/script-tests.log), Aion.GameServer.Tests passes (4,629 passed, 16
      skipped) and Fast passes (run nr90-fast, 11 passed).
- [x] **NR-90a - A skill that bursts the runes on its target.** Depends: NR-90
  - Work: Pain Rune is the Assassin's hardest hit beside Divine Strike when its target
    carries three runes, and a waste of 68 MP when it carries none. Java first, beyond
    what NR-90 read: what the client is told of the rune effect on a monster (its skill
    and level among the creature's effects, NR-80a) and when it ends. The world state
    holds each creature's visible effects. A skill row holds the rune it carves, with
    its first effect skill and its cap, and the rune it bursts. The table names for a
    burst role the least number of runes it is cast at; the role is out of the line
    while the target is seen with fewer. The Assassin gives Pain Rune and Binding Rune
    roles, each at three runes. Generic: the numbers in the table, no branch on a class;
    the Ranger's and the Gunner's bursts use the same form if they have one.
  - Proof: A one-time check of the decisions at each rune count, and one probe row on a
    probe account that carves three runes and bursts them; the full gate identical.
  - 2026-10-10: done. The bot knows the runes on its target, and the rule table casts a
    burst only on a target seen with as many as the table names. The Assassin casts Pain
    Rune and Binding Rune at three.
    - **Java, beyond NR-90.**
      - **What the client is told of a rune.** A rune is an effect of the slot DEBUFF
        on the monster, and when it starts or ends EffectController.broadCastEffects
        304-308 sends SM_ABNORMAL_EFFECT for that slot. The packet's constructor keeps
        every effect of the slots it names and no other, so a packet for one slot is
        that slot's whole list and says nothing of the rest; 127 names them all. Each
        row has the effect's skill and its level: 8303 at level 1 to 8307 at level 5.
      - **Soul Slash keeps its chain open one time in ten** (chain_skill_prob 10, in
        every rank). NR-90 wrote that Rune Slash follows Soul Slash and did not say how
        seldom. So the runes come from Fang Strike and Rune Carve, and from Rune Slash
        now and then. NR-90's record and the profile's comment are corrected.
    - **The change, generic.**
      - Bots/World/BotEffectWorldState.cs: the world state holds each creature's visible
        effects, replaced slot by slot as the packets name them.
      - Sc/NaturalPriestCombatPolicy.cs and Sc/Classes/NaturalSkillCatalog.cs: a skill
        row holds the rune it carves and the rune it bursts, from its template's
        effects; the fight's observation holds the runes seen on the target, each by
        its name with its level. The fight loop finds them among the target's effects:
        an effect whose template holds a rune, named by its stack as the server names
        it. The decision record's observed state is as it was.
      - Sc/Classes/NaturalRotationCombatPolicy.cs: a rule table may name, for a role
        that bursts a rune, the least number of runes it is cast at. The role is out of
        the line while the target is seen with fewer, and each decision writes a check,
        runes-<role>, with the number seen. A table that counts runes for a role that
        bursts none is refused when it is built.
      - No profile but the Assassin's has a row that carves or bursts a rune.
    - **The Assassin.** Pain Rune has the role pain and Binding Rune the role binding,
      both at three runes, where the burst does all of its damage and Pain Rune always
      stuns. They stand before Swift Edge in the list; an open follow-up still goes
      first. 40 of its 65 skills have a role now and 25 a reason.
    - **Proof, the one-time check** (run/nr/NR-90a/check.log; the check file is not
      committed). Of the seventeen profiles of the eleven class lines only the
      Assassin's has rune rows: four roles carve SIGNET1 and two burst it. An Assassin
      of level 13 and one of level 25 on a monster that is on it: with no rune, one or
      two it casts Swift Edge and the check says how many it saw; with three or five,
      Pain Rune; with three and a follow-up open, Soul Slash; with three and Pain Rune
      cooling down, Binding Rune at level 25 and Swift Edge at 13. The world state
      keeps a buff when the debuff slot is told with the second rune in the first one's
      place, and when it is told empty.
    - **Proof, the probe row** assassin-runes (SimT/SimulationNaturalStarterProbeTests
      .cs, probe account 98; `bash run/nr/NR-90a/probe.sh <attempt>`). Prepared by the
      director: a level-25 Assassin with the skills of every level up to it, with the
      dagger it was created with so that a fight lasts, by the starved mosbears of the
      Cleric's row. Every other act is the journey's. Run nr90a-probe-a1 failed on the
      row's own list, which named Rune Slash's rank of level 26. Runs nr90a-probe-a2
      and, after the comment was corrected, nr90a-probe-a3 passed alike:
      - The buff check casts Apply Deadly Poison.
      - Three fights, three kills. The first two ended with two runes on the target
        and no burst. The third, 17.5 s: Swift Edge, Soul Slash, Fang Strike, Devotion,
        Rune Carve, Killer's Eye, Swift Edge, Soul Slash, Fang Strike, and then Pain
        Rune III. The decisions saw 0, 0, 0, 1, 1, 2, 2, 2, 2, 2, 2, 2 and 3 runes; Pain
        Rune was decided at the one that saw three. The server completed it.
      - Soul Slash never left the chain open in the three fights, so Rune Slash was not
        cast: one time in ten.
      - A level-25 Assassin has 1,794 HP and 1,853 MP.
    - **For NR-91.** Three runes take about ten seconds of a fight at level 25 and
      longer before level 16, where Rune Carve alone carves, once in 7 s. The probe rows
      decide whether Pain Rune should be cast at two there: half its damage, and the
      stun four times in ten.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout+templar, -Parallel 8,
      run guard-p8 (run/nr/NR-90a/guard-p8/verdict.json): verdict pass, all thirteen
      scopes identical. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629
      passed, 16 skipped) and Fast passes (run nr90a-fast, 11 passed).
- [ ] **NR-91 - Assassin: probe rows.** Depends: NR-90a
  - Work: Rows assassin-10, assassin-16, assassin-20 and assassin-25 in
    SimulationNaturalStarterProbeTests: prepared Assassins on the two probe accounts, in
    the gear the route has given by that level and with two daggers held, fight the
    monsters the Cleric's rows fight. Each row's trace shows the table in play: the
    chain of three, Fang Strike, Rune Carve and the runes burst, Divine Strike at 2,000
    DP, Devotion, Killer's Eye and Flurry in the gaps, Apply Deadly Poison up, Focused
    Evasion on the ladder, the rest with the powder. What a row shows decides the open
    rules: whether a class that walks in needs a pull in Altgard's packs, as the Templar
    did (NR-53b), which would ask for Dash Attack and a reading of the cast's result;
    and whether Killer's Eye should stand before the hardest hit. A fix is one small
    change (rule (i)).
  - Proof: The four rows end with the monster dead or a recorded retreat, no refused
    cast repeated and no skill outside the table cast.
- [ ] **NR-92 - Assassin: to Altgard.** Depends: NR-91; ticked by the round that gives it
  - Work: A fresh Asimassassin plays Ishalgen as a Scout, the trial, the Assassin choice
    at Munin, the ceremony with the dagger and the dispatch Q2902, and is captured at
    the Altgard bind as altgard-assassin-s1.
  - Proof: The capture verifies.
- [ ] **NR-93 - Assassin: Altgard legs l1 to l5.** Depends: NR-92; ticked by its round
  - Work: The Cleric's legs in order, each end captured under the Cleric's snapshot name
    with -assassin (NR-41). In leg 1 it swings its daggers at the fungus in flight.
  - Proof: Each capture verifies.
- [ ] **NR-94 - Assassin: Altgard legs l6 to l11.** Depends: NR-93; ticked by its round
  - Work: As NR-93. Leg 11 is the destiny quest: its stone is the Templar's, 140000003
    (NR-39).
  - Proof: Each capture verifies.
- [ ] **NR-95 - Assassin: coin gear and Haramel.** Depends: NR-94; ticked by its round
  - Work: The coin-gear leg (leather, by its manifest, NR-38a; one dagger a tier, the
    other hand as it is) and Haramel with chest 700830 (NR-40), captured as
    altgard-coingear-assassin and altgard-rc-complete-s1-assassin.
  - Proof: Each capture verifies.
- [ ] **NR-96 - Assassin: the Abyss entry.** Depends: NR-95; ticked by its round
  - Work: Q24020, then Q2945, Q2946, Q2947 and Q2042 with the coin tiers it can wear
    (NR-38b, NR-Q14, NR-Q16), captured as morheim-abyss-entry-s1-assassin.
  - Proof: The capture verifies.
- [ ] **NR-97 - Assassin: the endpoint.** Depends: NR-96; ticked by its round
  - Work: Captured and verified as ntc-ready-assassin-s1: alive at Morheim Ice Fortress,
    level 25 or higher, Q2945, Q2946, Q2947 and Q2042 complete.
  - Proof: The capture verifies.
- [ ] **NR-98 - Assassin: the class scope.** Depends: NR-97
  - Work: One scope of the Assassin's play, chosen where its rules differ most from the
    Scout's, recorded by a bot alone, twice, and added to the gate's sets.
  - Proof: The two recordings are identical.
- [x] **NR-100 - Ranger: survey and profile data.** Depends: the close of phase C
  - 2026-10-10: done. The Ranger has a class line, a gear table and a profile the
    validator accepts. Nothing has been played. Its skills ask for no new form.
    - **Java.**
      - **What it learns.** SkillLearnService as read for NR-50. Of the Ranger's 104 rows
        to level 26, 81 are auto-learned, 18 are stigma rows from level 20 and five are
        skill books: Transformation: White Tiger and Transformation: Krall at 10 and 22,
        and Homeward Bound at 21. The route buys no book. 64 of the auto-learned are
        active skills, and with the nine ranks it keeps from the Scout the catalog has
        73 to give a role or a reason; none is a toggle (run/nr/NR-100/check.log).
      - **Chains.** ChainCondition as read for NR-50. Swift Shot opens the chain Arrow
        Strike (level 13) follows; Stunning Shot opens the chain Rupture Arrow (25)
        follows. Each of the four keeps its chain open every time (chain_skill_prob
        100).
      - **A bow.** Every shot's start condition names the bow, and the Scout's blade
        skills name sword, dagger and the other melee weapons. WeaponCondition.validate
        29-34 and isValidWeapon 45-49 refuse a skill whose list does not hold the type
        of the weapon in the main hand. So a Ranger with a bow has none of Swift Edge,
        Soul Slash, Surprise Attack and Counterattack, in the Ranger's ranks too. No
        template of the shipped data asks for arrows.
      - **A counter skill.** Seizure Arrow (15) carries counter_skill DODGE.
      - **Skills that move the caster or lay a thing down.** BackDashEffect.calculate
        27-36 puts the caster 25 m behind where it stood, by the cast's result alone,
        as DashEffect does (NR-90): Retreating Slash. SummonTrapEffect.applyEffect 30-54
        spawns a trap where the caster stands, for 60 s: Spike Trap and Poisoning Trap.
      - **Class rewards on the route** are those of the table under NR-42: the bow
        101700515 at the ceremony and the dispatch Q2902 (NR-30), the leather rewards
        and the bows of NR-32, the stone 140000002 (NR-39), Haramel's chest 700830
        (NR-40) and the leather coin armor with a bow at Lateni and Nott (NR-38).
        Nothing new.
    - **The class line.** `scout-ranger`: a Scout who becomes a Ranger, SIM account 41,
      character Asimranger, the bow 101700515 at the ceremony (Q2009, choice 3; NR-Q5).
      Its dispatch quest is Q2902 (NR-30). Its Scout holds two daggers and walks in, as
      the line `scout` does; from the ceremony on the character shoots. It is in
      NaturalClassLine.All and in the script's list; the script test holds the two
      together.
    - **The gear table** (Sc/Classes/NaturalClassGearTable.cs, Ranger).

      | Part | The Ranger's | From |
      |---|---|---|
      | Weapon group | bow; ranked by the physical stat | NR-Q5, CP-Q7 |
      | Off hand | nothing: a bow takes both hands | CP-Q10 |
      | Armor | leather, robe, clothes; item level first, the type breaks ties | NR-Q7, CP-Q24 |
      | Bonus order at a reward | physical attack, critical, accuracy, then HP | NR-32 |
      | Kept | life potions, its help kit, a mana potion it finds, every help scroll and food | as the Cleric from Ascension |

      Every bow of the route reaches 25 m and shoots once in 2.4 s.
    - **The help kit from level 10** is the manifest written under NR-50
      (NaturalHelpItemAllowlist.Kit(caster: false, reagent: true)), the Templar's: no
      mana serum and no Awakening scroll. Levels 1 to 9 are the Scout's kit (CP-Q12).
    - **Its skills by role** (Sc/Classes/NaturalRangerProfile.cs; 33 skills of 73).

      | Role | Skill, and the levels of its ranks | What it is |
      |---|---|---|
      | swift | Swift Shot: 10, 15, 20, 25 | Opens the first chain, from the bow's reach. Free, 8 s. |
      | arrow | Arrow Strike: 13, 18, 23 | Follows Swift Shot inside 3 s. Free, 6 s. |
      | stun | Stunning Shot: 10, 15, 20, 25 | Opens the second chain from 20 m: a hard shot and a stun of half a second. 37 MP and more, 12 s. |
      | rupture | Rupture Arrow: 25 | Follows Stunning Shot and staggers the target. 76 MP, 24 s. |
      | entangle | Entangling Shot: 10, 15, 20, 25 | A light hit that slows the target by three fifths for 16 to 20 s. 28 MP and more, 16 s. |
      | deadshot | Deadshot: 10, 15, 20, 25 | A shot that is ready every 2 s. 39 MP and more. |
      | mau | Transformation: Mau: 10 | 2,000 DP: for 2 min a quarter more attack, a quarter faster shots and two fifths more speed. |
      | sleep | Sleep Arrow: 19, 24 | From 15 m its target sleeps for 12 s, until it is hit. 56 MP and more, 3 min. |
      | devotion | Devotion: 9 | The Scout's: 40% more physical attack for 5 s. 19 MP, 30 s. |
      | evasion | Focused Evasion: 1 | The Scout's: 5 s in which every hit misses. Free, 30 s. |
      | herb, mp-recovery | Herb Treatment and MP Recovery: 10, 15, 20, 25 | The two powder skills of a rest (NR-50a). |

    - **The rule table, natural-ranger-v1.** In the form of the Engineer's, whose
      pistol reaches 20 m as Stunning Shot does. From range: Transformation: Mau when
      the DP are there and Devotion when it is ready; Entangling Shot, which keeps the
      monster on its way longer; Stunning Shot and Rupture Arrow; Swift Shot and Arrow
      Strike; Deadshot whenever nothing else is ready. With the monster on it the two
      chains come before Entangling Shot. An open follow-up is always cast first, and
      the bow shoots whenever no skill is ready. It stands off at 18 m and never walks
      in, with the Engineer's distances and thresholds. The ladder is the Scout's:
      Focused Evasion at or below 70% HP, the shield scroll at 50%, the life potion at
      or below 75%. It leaves at two attackers, as the Engineer does, or at 25% HP with
      nothing ready, and puts its target to sleep first. Between fights it rests as
      the Templar (NR-50a). It holds for a patrol and assesses (NR-37), and a pull
      brings one. In flight it shoots Deadshot (NR-36).
    - **Left out, with the reason** (40 skills).

      | Skill, levels | Why it is not cast |
      |---|---|
      | Return, Bandage Heal, Escape: 1 | As for every class (CP-35, CP-Q11). |
      | Swift Edge: 1, 6, 11, 16, 21, 26; Soul Slash: 7, 12, 17, 22; Surprise Attack: 3, 8, 13, 18, 23; Counterattack: 5, 10, 15, 20, 25 | They need a dagger or a sword in hand, and the Ranger holds a bow. |
      | Seizure Arrow: 15, 20, 25 | A counter skill: the server takes it only within 5 s of a dodge of the Ranger's own, which the bot does not observe. |
      | Stealth: 5 | As the Scout: not in combat, and the journey does not sneak. |
      | Spike Trap: 13, 18, 23; Poisoning Trap: 16, 21, 26 | A trap is laid at the Ranger's feet for a monster to walk into; the table has no rule that lays one on a pulled monster's way. |
      | Retreating Slash: 22 | It throws the Ranger 25 m back, to a place only the cast's result names. |
      | Dodging: 10; Aiming: 19; Strong Shots: 25 | 200 evasion, 200 accuracy or 5% more attack for a minute, for 68, 84 and 97 MP; the table spends its mana on shots. |
      | Silence Arrow: 25 | It silences its target for 8 to 10 s and hits for little; the table has no rule for a target that casts. |
      | Calming Whisper: 19, 24 | It lowers the enmity of its target; alone, the monster has no one else to turn to. |

      Not in the catalog at all: the five books, and the stone's skill 11505, as for
      every class (NR-39).
    - **Proof.** The one-time check, not committed (run/nr/NR-100/check.log): the line
      parses and holds both classes; its character is a Scout, with a second weapon in
      the off hand, until the client observes a Ranger; NaturalClassProfiles builds the
      Ranger's profile, which requires the validator: each of the 73 active skills has
      one role or one reason, every follow-up has its opener, no rotation line breaks a
      chain or casts a counter skill, and the gear groups lie inside the masteries. The
      check prints the skill tree with each chain step's chance to keep its chain, the
      33 rows, the 40 reasons, the best rank of each role at eight levels, the gear
      groups and the patrol view. Gate, set
      all+mage+warrior+artist+engineer+scout+templar, -Parallel 8, run guard-p8
      (run/nr/NR-100/guard-p8/verdict.json): verdict pass, all thirteen scopes
      identical. Seven pre-commit checks pass, the three script tests pass
      (run/nr/NR-100/script-tests.log), Aion.GameServer.Tests passes (4,629 passed, 16
      skipped) and Fast passes (run nr100-fast, 11 passed).
- [ ] **NR-101 - Ranger: probe rows.** Depends: NR-100
  - Work: Rows ranger-10, ranger-16, ranger-20 and ranger-25 in
    SimulationNaturalStarterProbeTests: prepared Rangers on the two probe accounts, in
    the gear the route has given by that level and with its bow, fight the monsters the
    Cleric's rows fight. Each row's trace shows the table in play: the stand-off at 18 m
    with the bow shooting between skills, Entangling Shot first, the two chains,
    Deadshot in the gaps, Transformation: Mau at 2,000 DP, Focused Evasion on the
    ladder, Sleep Arrow before a retreat, the rest with the powder. What a row shows
    decides the open rules: whether Deadshot empties its mana before a fight ends,
    whether the three one-minute buffs earn their mana, and whether a bow that reaches
    25 m should stand off farther than the Engineer's 18 m. A fix is one small change
    (rule (i)).
  - Proof: The four rows end with the monster dead or a recorded retreat, no refused
    cast repeated and no skill outside the table cast.
- [ ] **NR-102 - Ranger: to Altgard.** Depends: NR-101; ticked by the round that gives it
  - Work: A fresh Asimranger plays Ishalgen as a Scout, the trial, the Ranger choice at
    Munin, the ceremony with the bow and the dispatch Q2902, and is captured at the
    Altgard bind as altgard-ranger-s1. The bow is in its hands from the ceremony on.
  - Proof: The capture verifies.
- [ ] **NR-103 - Ranger: Altgard legs l1 to l5.** Depends: NR-102; ticked by its round
  - Work: The Cleric's legs in order, each end captured under the Cleric's snapshot name
    with -ranger (NR-41). In leg 1 it shoots the fungus in flight.
  - Proof: Each capture verifies.
- [ ] **NR-104 - Ranger: Altgard legs l6 to l11.** Depends: NR-103; ticked by its round
  - Work: As NR-103. Leg 11 is the destiny quest: its stone is 140000002 (NR-39).
  - Proof: Each capture verifies.
- [ ] **NR-105 - Ranger: coin gear and Haramel.** Depends: NR-104; ticked by its round
  - Work: The coin-gear leg (leather, by its manifest, NR-38a) and Haramel with chest
    700830 (NR-40), captured as altgard-coingear-ranger and
    altgard-rc-complete-s1-ranger.
  - Proof: Each capture verifies.
- [ ] **NR-106 - Ranger: the Abyss entry.** Depends: NR-105; ticked by its round
  - Work: Q24020, then Q2945, Q2946, Q2947 and Q2042 with the coin tiers it can wear
    (NR-38b, NR-Q14, NR-Q16), captured as morheim-abyss-entry-s1-ranger.
  - Proof: The capture verifies.
- [ ] **NR-107 - Ranger: the endpoint.** Depends: NR-106; ticked by its round
  - Work: Captured and verified as ntc-ready-ranger-s1: alive at Morheim Ice Fortress,
    level 25 or higher, Q2945, Q2946, Q2947 and Q2042 complete.
  - Proof: The capture verifies.
- [ ] **NR-108 - Ranger: the class scope.** Depends: NR-107
  - Work: One scope of the Ranger's play, chosen where its rules differ most from the
    Scout's, recorded by a bot alone, twice, and added to the gate's sets.
  - Proof: The two recordings are identical.
- [x] **NR-110 - Spirit Master: survey and profile data.** Depends: the close of phase C
  - 2026-10-10: done. The Spirit Master has a class line, a gear table and a profile the
    validator accepts. Nothing has been played. The profile is a caster's: its spirit,
    and all that orders, heals or arms the spirit, need forms the table does not have,
    items NR-110a and NR-110b below.
    - **Java.**
      - **What it learns.** SkillLearnService as read for NR-50. Of the Spirit Master's
        91 rows to level 26, 73 are auto-learned, 17 are stigma rows from level 20 and
        one is a skill book, Homeward Bound at 21. 59 of the auto-learned are active
        skills, and with the seven ranks it keeps from the Mage the catalog has 66 to
        give a role or a reason; none is a toggle (run/nr/NR-110/check.log).
      - **Chains.** ChainCondition as read for NR-50. Chain of Earth (level 13) opens
        the chain Stone Shock follows. Of the Mage's two chains it keeps the first rank
        only: Flame Bolt with Blaze, Ice Chain with Frozen Shock. Every chain step it
        has keeps its chain open every time (chain_skill_prob 100).
      - **A spirit.** SummonEffect.applyEffect 31-39 and SummonsService.createSummon
        29-40: a Summon spell puts a spirit beside its master, tells the master with
        SM_SUMMON_PANEL and those around with SM_SUMMON_UPDATE, and is refused while the
        master has one. The client sends the spirit by CM_SUMMON_COMMAND:
        SummonsService.doMode 184-216 takes attack with a target, guard, rest and
        release. A skill whose first target is MYPET is refused without a spirit
        (FirstTargetProperty 104-111). Fire Spirit comes at level 10, Wind at 13, Earth
        at 16 and Water at 19; each cast takes 4.5 s.
      - **Class rewards on the route** are the Sorcerer's at every point (the table
        under NR-42): the spellbook 100600532 at the ceremony and the dispatch Q2903
        (NR-30), the cloth rewards and the spellbooks of NR-32, the stone 140000004
        (NR-39), Haramel's chest 700831 (NR-40) and the cloth coin armor with a
        spellbook at Lohaban and Vebna (NR-38). Nothing new.
    - **The class line.** `mage-spirit-master`: a Mage who becomes a Spirit Master, SIM
      account 41, character Asimspirit, the spellbook 100600532 at the ceremony (Q2009,
      choice 1; NR-Q5). Its dispatch quest is Q2903 (NR-30). Its snapshots end in
      -spirit-master. It is in NaturalClassLine.All and in the script's list; the script
      test holds the two together.
    - **The gear table** (Sc/Classes/NaturalClassGearTable.cs, SpiritMaster) is the
      Sorcerer's: a spellbook, ranked by magic boost; nothing in the off hand; robe,
      then clothes; the bonus order of a class that casts; and the Sorcerer's kept
      items.
    - **The help kit from level 10** is every row of the manifest written under NR-60
      (NaturalHelpItemAllowlist.Kit(caster: true, reagent: true)). Levels 1 to 9 are the
      Mage's kit (CP-Q12).
    - **Its skills by role** (Sc/Classes/NaturalSpiritMasterProfile.cs; 34 skills of 66).

      | Role | Skill, and the levels of its ranks | What it is |
      |---|---|---|
      | erosion | Erosion: 5, 10, 15, 20, 25 | A hit at once and every 3 s for 15 s. Instant, 51 MP and more, 3 s. |
      | earth | Chain of Earth: 13, 18, 23 | Opens a chain: a light hit at once and for 15 s, and half speed for 15 to 20 s. Instant, 60 MP and more, 15 s. |
      | stone | Stone Shock: 13, 18, 23 | Follows Chain of Earth and staggers the target. Instant, 88 MP and more, 30 s. |
      | servant | Summon Wind Servant: 10, 15, 20, 25 | Two servants that strike three times each. 1.5 s cast, 120 MP and more, 10 s. |
      | choke | Vacuum Choke: 22 | Its hardest spell. 2 s cast, 86 MP, ready again after 1 s. |
      | backdraft | Backdraft: 26 | A hit that gives back as HP and MP what it takes. 2 s cast, 110 MP, 24 s. |
      | bolt, blaze, ice, shock | Flame Bolt: 1; Blaze: 5; Ice Chain: 3; Frozen Shock: 7 | The Mage's two chains in their first rank; it learns no later one. |
      | root | Root: 1 | The Mage's: holds the target for 20 s until it is hit. 60 s. |
      | skin | Stone Skin: 7, 12, 17, 22 | A shield for 5 min. 2 min. |
      | herb, mp-recovery | Herb Treatment and MP Recovery: 10, 15, 20, 25 | The two powder skills of a rest (NR-50a). |

    - **The rule table, natural-spirit-master-v1.** The Mage's, with the Spirit Master's
      own spells. From range: Chain of Earth, whose snare keeps the monster away longer,
      and Stone Shock at once; Erosion; Vacuum Choke and Backdraft; Summon Wind Servant;
      then the Mage's two chains. With the monster on it Erosion comes first, which a
      hit cannot push back. An open follow-up is always cast first. Stone Skin goes up
      before the first hit and is kept up between fights. Root is cast only on the way
      out, before a retreat. The ladder: the shield scroll at 50% HP, the life potion at
      or below 75%. It leaves at two attackers, or at 25% HP with nothing ready. A mana
      serum is drunk only when the cheapest attack cannot be paid; the spellbook swings
      only then. Between fights it rests with the powder first, then as the Mage. It
      holds for a patrol and assesses (NR-37) with no heal to ask about, and a pull
      brings one. In flight it casts Erosion (NR-36).
    - **Left out, with the reason** (32 skills).

      | Skill, levels | Why it is not cast |
      |---|---|
      | Return, Bandage Heal, Escape: 1 | As for every class (CP-35, CP-Q11). |
      | Summon: Fire Spirit: 10, 15, 20, 25; Wind Spirit: 13, 18, 23; Earth Spirit: 16, 21, 26; Water Spirit: 19, 24 | A spirit that fights beside it. No rule summons a spirit, keeps it or sends it at a target yet. NR-110a. |
      | Spirit Disturbance: 10; Spirit Wrath Position: 17; Spirit Erosion: 22 | Orders to the spirit. NR-110b. |
      | Replenish Element: 16, 21, 26 | It heals the spirit for the Spirit Master's own HP. NR-110b. |
      | Divine Spirit Armor: 10, 15, 20, 25 | 2,000 DP that arm the spirit for 10 min. NR-110b. |
      | Root of Enervation: 10 | A fifth slower attacks for 35 to 45 s, for 244 MP: the mana of four Erosions. |
      | Body Root: 20 | For 8 to 10 s its target casts no physical skill and still swings. 108 MP. |
      | Sigil of Silence: 21 | It silences its target; the table has no rule for a target that casts. |
      | Dispel Magic: 16; Ignite Aether: 25 | They take a buff off the target and hit for each one taken; the monsters of the route carry none. |
      | Sandblaster: 25 | It hits up to six monsters around its target; an area skill wakes every other one in reach. |
      | Summon Group Member: 23 | It calls a member of its group; the bot plays alone. |

      Not in the catalog at all: Homeward Bound, which is a book, and the stone's skill
      11507, as for every class (NR-39).
    - **What the class is without its spirit.** It can play the route as a caster:
      Erosion every 3 s, the earth chain, the servants, and Vacuum Choke from level 22.
      It is not the class a group expects, and it has no pet between it and the monster.
      The plan's order holds: NR-110a and NR-110b come before its probe rows.
    - **Proof.** The one-time check, not committed (run/nr/NR-110/check.log): the line
      parses and holds both classes; its character is a Mage until the client observes
      a Spirit Master; NaturalClassProfiles builds the Spirit Master's profile, which
      requires the validator: each of the 66 active skills has one role or one reason,
      every follow-up has its opener, no rotation line breaks a chain, and the gear
      groups lie inside the masteries. The check prints the skill tree with each chain
      step's chance to keep its chain, the 34 rows, the 32 reasons, the best rank of
      each role at eight levels, the gear groups and the patrol view. Gate, set
      all+mage+warrior+artist+engineer+scout+templar, -Parallel 8, run guard-p8
      (run/nr/NR-110/guard-p8/verdict.json): verdict pass, all thirteen scopes
      identical. Seven pre-commit checks pass, the three script tests pass
      (run/nr/NR-110/script-tests.log), Aion.GameServer.Tests passes (4,629 passed, 16
      skipped) and Fast passes (run nr110-fast, 11 passed).
- [x] **NR-110a - A spirit summoned and kept.** Depends: NR-110
  - Work: Java first, beyond what NR-110 read: what ends a spirit (its death, its
    master's death, a release, a move to another map, a logout, a timer) and what the
    client is told of its own spirit (SM_SUMMON_PANEL, SM_SUMMON_UPDATE and the packets
    that take it away). The bot's world state knows whether it has a spirit, which one,
    and its HP. A profile names the spirits it keeps, best first; the buff check between
    fights summons the best learned one when none is out, and never while one is. The
    Spirit Master names Earth Spirit, then Fire Spirit: the one that takes the hits when
    it has it, and its first before. Generic: roles and an order in the profile, no
    branch on a class.
  - Proof: A one-time check, or one probe row on a probe account, that shows the spirit
    out after the rule ran once, not summoned again while it is out, and summoned again
    after it was released; the full gate identical.
  - 2026-10-10: done. A profile names the spirits it keeps, the world state knows the
    bot's own spirit from the server's word, and the buff check summons the best learned
    one when none is out. The Spirit Master keeps the Earth Spirit, and the Fire Spirit
    before it has one.
    - **Java, beyond NR-110.**
      - **What ends a spirit.** Every end goes through SummonsService.release 45-53: the
        spirit's death (SummonController 123), its master's death (PlayerController
        297), a master that left its sight, as a teleport does (SummonController.notKnow
        33-37), a master too far away (FollowSummonTaskAI.run 37-41), a logout
        (PlayerLeaveWorldService 117), a timer when the spell names one (SummonEffect
        34-36; the four spirits name none), and the master's own release, which waits
        3 s and may be taken back.
      - **What the client is told.** A new spirit: SM_SUMMON_PANEL to its master, with
        the spirit's object, level and HP (createSummon 36). A spirit that is gone:
        SM_SUMMON_PANEL_REMOVE with the summoning skill and SM_SUMMON_OWNER_REMOVE with
        the object, both to the master (ReleaseSummonTask.run 91-92). SM_SUMMON_UPDATE
        names no spirit and goes to everyone around, so the bot does not read it.
      - **The summon's cooldown** starts when the spirit is gone (run 85), 5 s, and the
        client is not told of it.
      - **The port** has the same service (Services/Summons/SummonsService.cs). No server
        change.
    - **Found for the next item: the server moves no spirit.** VisibleObjectSpawner
      .spawnSummon 234-257 puts the spirit 2 m in front of its master, and nothing on
      the server moves it after that or swings for it: the follow task of the server is
      the siege weapon's alone (SiegeWeaponController 54, 68), and SummonsService
      .attackMode 168-174 only sets the mode and tells the master. The owner's client
      drives the spirit: CM_SUMMON_MOVE reports its steps (runImpl 65), CM_SUMMON_ATTACK
      each of its attacks, which SummonController.attackTarget 77-91 holds to the
      spirit's attack speed, and CM_SUMMON_CASTSPELL its skills. So a spirit that is
      summoned and not driven stays where it stood and is released when its master
      walks out of its sight. The row above summons and keeps it in one place. NR-110b
      is split into three items by rule (q).
    - **The change, generic.**
      - Bots/Protocol/BotServerPacketDecoder.cs decodes SM_SUMMON_PANEL,
        SM_SUMMON_PANEL_REMOVE and SM_SUMMON_OWNER_REMOVE: 123 packet types, and the
        decoder's own test counts them so.
      - Bots/World/BotSummonWorldState.cs: the world state holds the bot's own spirit,
        with its object, level and HP, until the server says it is gone. A world reload
        keeps it; a new login starts with none.
      - Sc/NaturalPriestCombatPolicy.cs and Sc/Classes/NaturalSkillCatalog.cs: a skill
        row holds the spirit it summons, from the summon effect of that very class; a
        servant, a trap and a totem derive from it and are no spirits.
      - Sc/Classes/NaturalClassProfile.cs: a profile names the spirits it keeps, best
        first. Sc/NaturalIshalgenJourney.Combat.cs, the buff check: after the buffs and
        the toggles, with no spirit out, the first kept spirit whose skill is learned is
        summoned, outside a fight, and traced with what the server then said. With a
        spirit out nothing is summoned.
      - Sc/Classes/NaturalProfileValidator.cs: a skill that summons a spirit is in no
        rotation line, and a kept spirit holds such skills only.
      - SimT/SimulationFastScenarioTests.cs: a SIM trace now holds the three summon
        packets. No recorded scope receives one.
    - **The Spirit Master.** Summon: Earth Spirit has the role earth-spirit and Summon:
      Fire Spirit the role fire-spirit, kept in that order. Wind Spirit and Water Spirit
      are left out with their reason: one spirit is out at a time. 41 of its 66 skills
      have a role now and 25 a reason. The orders, the heal and the armor wait for
      NR-110b.
    - **Proof, the one-time check** (run/nr/NR-110a/check.log; the check file is not
      committed). Every profile of the thirteen class lines is built; only the Spirit
      Master's has rows that summon a spirit, seven, and keeps any. It keeps none at
      level 9, the Fire Spirit from 10 in its newest rank, the Earth Spirit from 16.
      Summon Wind Servant summons no spirit. The validator refuses, each by name: a
      summon in a rotation line; a kept spirit that holds the servant; a kept spirit
      that holds nothing. The world state takes a panel, keeps the spirit through
      another creature's removal and a world reload, and lets it go at its own removal
      and at the panel's.
    - **Proof, the probe row** spirit-master-spirit (SimT/SimulationNaturalStarterProbe
      Tests.cs, probe account 100; `bash run/nr/NR-110a/probe.sh <attempt>`). Prepared
      by the director: a level-10 Spirit Master in Altgard. Every other act is the
      journey's buff check. Runs nr110a-probe-a1 and, on the committed tree,
      nr110a-probe-a2 passed alike:
      - **First check, level 10.** It casts Stone Skin and Summon: Fire Spirit. The
        server sent the panel; the Fire Spirit 833344 is out with 644 HP.
      - **Second check.** No cast, and the server said nothing of a spirit.
      - **Level 16.** The director sets the level, where Summon: Earth Spirit is
        learned. The check casts nothing: a spirit is out.
      - **Released.** The director releases the spirit at once, as the server does when
        one dies: the server sent both removals, and the bot has none.
      - **After 6 s.** The check casts Summon: Earth Spirit, the best it has: the Earth
        Spirit 833288 is out with 1,575 HP.
      - A level-16 Spirit Master has 799 HP and 2,028 MP.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout+templar, -Parallel 8,
      run guard-p8 (run/nr/NR-110a/guard-p8/verdict.json): verdict pass, all thirteen
      scopes identical. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629
      passed, 16 skipped) and Fast passes (run nr110a-fast2, 11 passed). The first bundle
      (run/nr/NR-110a/bundle-a1/) failed the unit suite in the decoder's own contract
      test: every decoded packet must give a field of its Java fixture under the
      fixture's name, or have its layout pinned there. The removal's field took the
      fixture's name, summonObjId, and the panel, which has no Java fixture because it
      reads a live spirit, got its layout pinned in that test. The row was played again
      and the bundle run again from the start.
- [x] **NR-110b - The spirit follows its master.** Depends: NR-110a
  - Work: The server moves no spirit (NR-110a): the owner's client does, by
    CM_SUMMON_MOVE. Java first: the packet's fields and what CM_SUMMON_MOVE.runImpl takes
    and refuses, the spirit's speed, and how far a spirit may be from its master before
    the server releases it. The bot's protocol writes the packet. While the bot walks
    with a spirit out, the spirit walks the bot's own route a few metres behind it, at
    the spirit's speed, and stands when the bot stands. A spirit that fell behind is
    left to the server, which releases it; the buff check summons the next. Generic:
    every class that keeps a spirit.
  - Proof: One probe row on a probe account in which a Spirit Master with its spirit out
    walks a route of 200 m or more and the spirit is beside it at the end, by the
    server's own position of it; the full gate identical.
  - 2026-10-10: done. A ground walk of a bot with a spirit out carries the spirit's own
    steps, and the server moves the spirit by them. Over 555 m the spirit was never more
    than 3.0 m from its master.
    - **Java, beyond NR-110a.**
      - **The packet.** CM_SUMMON_MOVE.readImpl 35-62: the spirit's object, x, y, z, the
        heading, the movement mask; with POSITION, MANUAL and ABSOLUTE in the mask, the
        point it walks to; with GLIDE or VEHICLE their fields.
      - **What the server takes.** runImpl 65-102. It refuses a spirit that is not the
        sender's or is not spawned, and one that cannot move, is afraid or is confused
        (66-72). A mask of IMMEDIATE stops it (80-81). A start, POSITION with MANUAL,
        sets its direction to the point named (85-86); a start without ABSOLUTE is
        dropped whole (83-84). Every other mask is a step (88). Then the spirit is put
        at the x, y, z sent (97). The server checks no speed, no distance and no
        geometry of it. A mask with POSITION, and a stop, go to every player who sees
        the spirit as SM_MOVE (100-101), its master among them.
      - **How far.** A creature is seen within 95 m (VisibleObject.getVisibleDistance
        247-249, KnownList 214). A spirit whose master is no longer in its sight is
        released (SummonController.notKnow 33-37, NR-110a).
      - **The port** has the same packet (Network/Aion/ClientPackets/CM_SUMMON_MOVE.cs)
        and the same 95 m. No server change.
    - **The change, generic.** No profile and no class is named: every bot whose world
      state holds a spirit.
      - Bots/Movement/BotMover.cs, `WeaveSpirit`, called for every ground plan. The
        spirit's path is from where it stands to where the bot starts, and then the
        bot's own route. It walks that path 3 m behind the bot (`SpiritLagMeters`), no
        faster than its own speed, and only while the bot walks. Its start is sent with
        the bot's first step and names where it will stand; a step follows each step of
        the bot in which the spirit moved 5 cm or more; a stop ends it. The bot's own
        steps, the plan's length and its time are unchanged.
      - The spirit's speed is the one the server has said of it (SM_EMOTION), and the
        bot's own when it has said none.
      - Not driven: a jump, a glide and a flight, and a spirit more than 60 m from the
        bot (`SpiritLostMeters`). That spirit is left to the server, which releases it
        at 95 m; the buff check summons the next (NR-110a).
      - SimT/SimulationFastScenarioTests.cs: a SIM trace now holds the four client
        packets that drive a spirit. No recorded scope sends one.
    - **Proof, the probe row** spirit-master-walk (SimT/SimulationNaturalStarterProbe
      Tests.cs, probe account 98; `bash run/nr/NR-110b/probe.sh <attempt>`). Prepared by
      the director: a level-26 Spirit Master, which the monsters of Altgard leave alone,
      where the Cleric's level-16 row fights. The buff check summons Earth Spirit III.
      Then the journey walks by its own routes to where the Cleric's level-25 row fights.
      - **First attempt** (nr110b-probe-a1, kept): the walk ended after 19 m. The
        probe's approach returns at the first segment a monster has made unsafe, here a
        fierce mosbear 9.6 m from the bot, where the journey itself plans again. The
        spirit had followed: its last SM_MOVE put it 2.6 m from its master. The row's
        walk was changed, not the item's code: it asks for the approach again, up to
        twelve times, and counts the length walked.
      - **Second attempt** (nr110b-probe-a2): passed. 555 m in a straight line in eight
        approaches, the goal reached. The bot sent 1,432 steps of its own and 782 of
        the spirit's, and the server sent 782 SM_MOVE of the spirit back. By the
        server's own positions the spirit moved 556 m, stood 2.1 m from its master
        before the walk, at most 3.0 m between approaches and 3.0 m at the end. It is
        the same spirit, 833292, and Summon: Earth Spirit was not cast again.
    - **Left for NR-110c.** The spirit moves only while the bot walks, and with no speed
      said by the server it walks at the bot's own. So a gap left by a jump or a glide is
      kept and not closed. The spirit's own walk while the bot stands, which NR-110c
      needs for the walk to a target, closes it. The spirit's first stretch, to where
      the bot stood, is a straight line and not a checked route.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout+templar, -Parallel 8,
      run guard-p8 (run/nr/NR-110b/guard-p8/verdict.json): verdict pass, all thirteen
      scopes identical. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629
      passed, 16 skipped) and Fast passes (run nr110b-fast, 11 passed).
- [x] **NR-110c - The spirit fights.** Depends: NR-110b
  - Work: Java first: CM_SUMMON_COMMAND and SummonsService.doMode, CM_SUMMON_ATTACK and
    SummonController.attackTarget, and whose enmity a monster keeps when a spirit and
    its master both hit it. In a fight the bot sends its spirit at the target it pulls:
    attack mode, the walk to the target, and an attack each time the spirit's attack
    speed allows; guard mode and the walk back when the fight ends. The spirit's own
    walk while the bot stands also closes a gap that a jump or a glide left (NR-110b).
    The journey's fight rules are read for a monster that is on the spirit and not on
    the bot: what counts as an attacker, when the bot is hit, the stand-off, the retreat.
  - Proof: One probe row on a probe account in which the spirit is sent at a monster,
    the monster is on the spirit, and the fight ends with the monster dead or a recorded
    retreat; the full gate identical.
  - 2026-10-10: done. In a fight the bot sends its spirit first, walks it to the target,
    reports its swings, and calls it back when the fight ends; the bot's next walk takes
    it along. Two rules of the fight do not fit a spirit yet and are the items NR-110e
    and NR-110f below.
    - **Java, beyond NR-110b.**
      - **The order.** CM_SUMMON_COMMAND carries the mode, two zeros and the target;
        SummonsService.doMode 184-216 takes ATTACK only for a creature the spirit knows
        and may attack (SummonController.canAttack 72-74). attackMode 168-174 keeps the
        mode, tells the master and stops the spirit's HP from coming back; guardMode
        156-163 starts that again. Neither moves the spirit nor makes it swing.
      - **The swing.** CM_SUMMON_ATTACK names the spirit and the target (runImpl 39-51).
        SummonController.attackTarget 77-91 refuses a dead target and one that is no
        enemy, and a swing that comes sooner than the spirit's attack speed less 50 ms.
        CreatureController.attackTarget 307-360 then deals the damage. Neither checks a
        range or a line of sight. SM_ATTACK goes to everyone who sees the spirit, its
        master among them, and calls the target's friends (347-349).
      - **Whose enemy.** A monster keeps the spirit and its master as two enemies: the
        hate is ten times the damage, of the one who dealt it (AggroList.addDamage 37-52,
        StatFunctions.calculateHate 267-270), and it turns on the one it hates more
        (AttackManager 75-80). A spirit's damage counts for its master in the kill
        (AggroList.getFinalDamageList), so the experience and the loot are the master's.
      - **The port** has the same packets, service and checks, and holds the swing to
        its game clock (Controllers/SummonController.cs 60-78). No server change.
    - **The change, generic.** No profile and no class is named. A bot without a spirit
      sends nothing and waits for nothing.
      - Sc/Classes/NaturalSpiritDriver.cs, new. A beat: the order when the target is
        new; the stretch the spirit ran since the beat before, at its own speed, to half
        a metre inside its own reach of the target; a swing when it stands in reach and
        its attack speed allows one. The opening: before the bot's first attack the
        spirit is sent, and the bot holds until it sees the spirit's first hit, for 8 s
        at most; it holds nothing when something is on it already. Standing down: the
        spirit stops and gets the order to guard.
      - Sc/NaturalIshalgenJourney.Combat.cs: the fight has its opening before its first
        attack and a beat with each of its turns. The spirit stands down when the fight
        ends, before a retreat, and when a target cannot be reached. The buff check
        looks up the spirit's run speed in the client's data of its kind.
      - Bots/World/BotSummonWorldState.cs: the world state holds the bot's own order
        (the spirit fights or not) and the spirit's speed; a new spirit and a spirit
        that is gone have neither.
      - Bots/Movement/BotMover.cs: a walk of the bot leaves a spirit that fights where
        it is, and drives a spirit at its own speed, 8.4 m/s for an Earth Spirit, so
        that it closes a gap (NR-110b's open point).
    - **Proof, the probe row** spirit-master-fight (SimT/SimulationNaturalStarterProbe
      Tests.cs, probe account 100; `bash run/nr/NR-110c/probe.sh <attempt>`). Prepared by
      the director: a level-16 Spirit Master by the tusked mosbears where the Cleric's
      level-16 row fights. The buff check summons the Earth Spirit, 1,575 HP. The first
      attempt (nr110c-probe-a1) passed. The row then got its walk after the fight, and
      the second attempt (nr110c-probe-a2) passed with the same fight to the millisecond.
      - **The opening.** The spirit is sent from 15.8 m at 8.4 m/s: the order, a start,
        three steps half a second apart, a stop and a swing after 2.0 s. The server shows
        its hit and the mosbear strikes the spirit; the opening ends after 2,500 ms.
      - **The fight.** The bot then casts eight skills of its table from 17.6 to 19.6 m.
        The spirit swings four times, and the server shows all four. The mosbear dies
        13.9 s after the fight began. It struck the spirit once and its master once, at
        the end, when the master had dealt more damage.
      - **The end.** The spirit gets the order to guard at the kill (two orders in all)
        and is in guard mode on the server, with 1,244 HP; the bot has 564 of 799 HP.
      - **The walk on.** The spirit stands 16.5 m from its master, where it fought. The
        journey walks 82 m in two approaches with 108 steps of the spirit, and the
        spirit stands 3.0 m from its master at the end, by the server's positions.
      - Row spirit-master-walk was played again on the changed walk (run
        nr110c-probe-walk-a1): 555 m, the spirit never more than 3.0 m away, as before.
    - **The fight rules, read for a spirit.**
      - **An attacker is one that strikes the bot.** Two tusked mosbear cubs (210439)
        of the target's family struck the spirit nine times in the fight and three
        times more in the walk after it. The bot's rules count neither: they are in no
        count of attackers, the rest would not defend against them, a retreat would
        not flee them. Item NR-110e.
      - **Where the spirit stands.** The spirit goes to the target and stands inside
        the circles of the target's neighbours, 6 m for the cubs, and draws them. The
        bot's own pull from 17 m draws none. Item NR-110f.
      - **When the bot is hit.** A ruthless mosbear (210581) struck the bot six times
        through the fight. It was one attacker, below the table's count for a swarm,
        and the fight went on as for any class.
      - **The stand-off.** The monster stayed with the spirit, so the bot cast its
        range line through the whole fight and never its adjacent line.
      - **The retreat** calls the spirit back first, and the walk then takes it along.
        The row has no retreat.
      - **The spirit's pace.** A beat comes with each act of the bot. The spirit swung
        four times in 11.9 s, where its attack speed of 2,040 ms allows six.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout+templar, -Parallel 8,
      run guard-p8 (run/nr/NR-110c/guard-p8/verdict.json): verdict pass, all thirteen
      scopes identical. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629
      passed, 16 skipped) and Fast passes (run nr110c-fast, 11 passed).
- [x] **NR-110d - The spirit's orders, its heal and its armor.** Depends: NR-110c
  - Work: Java first: PetOrderUseUltraSkillEffect and CM_SUMMON_CASTSPELL, and the skills
    whose first target is the spirit. The table gets Spirit Disturbance, Spirit Erosion
    and Spirit Wrath Position as orders, Replenish Element below a percentage of the
    spirit's HP, and Divine Spirit Armor for its DP.
  - Proof: One probe row on a probe account in which an order is carried out and the
    spirit is healed; the full gate identical.
  - 2026-10-10: done. The table orders the spirit, the bot answers the server so that
    the spirit casts, and a ladder of its own heals and arms the spirit. Found on the
    way: the bot does not read where a stagger puts a monster, item NR-110g below.
    - **Java, beyond NR-110c.**
      - **An order.** A skill with PetOrderUseUltraSkillEffect makes the spirit cast
        nothing by itself. applyEffect 28-52 looks up the spirit's own skill for this
        order and this kind of spirit, puts it on the spirit's list with the order's
        hate, and tells the master's client with SM_SUMMON_USESKILL: the spirit, the
        skill, its level, the target. The spirit casts when the client answers with
        CM_SUMMON_CASTSPELL (runImpl 68-75, SummonController.useSkill 126-136). The
        order is taken off the list whether the spirit's cast succeeds or not.
      - **The spirit's skills** (pet_skills.xml, skill_templates.xml), for the Earth
        Spirit of level 16, 21 and 26. Spirit Disturbance: Command: Earth Disturbance
        hits for 347, 455 or more, gives the spirit half of it as HP, and carries the
        order's hate of 1,104; it reaches 5 m from the spirit. Spirit Erosion: Earth Erosion,
        131 or 154 damage every 6 s for 30 s. Spirit Wrath Position: Command: Earth
        Wrath, on the spirit itself: 14% more attack and 1,450 HP more for 30 s, with a
        heal of 1,450.
      - **A skill on the spirit.** First target MYPET: the server takes the caster's
        spirit whatever the caster has selected, and refuses without one (Java
        FirstTargetProperty 104-114). Replenish Element heals the spirit at once and
        costs its caster HP, 226, 394 and 562 by rank; a player whose HP is not above
        the cost is refused (HpCondition.canValidate 40-47). Divine Spirit Armor costs
        2,000 DP and lasts 10 min.
      - **The spirit's HP.** Every change of it goes to everyone who sees the spirit as
        SM_ATTACK_STATUS, which carries the HP as a percentage.
      - **The port** has the same effect, packets and conditions. No server change.
    - **The change, generic.** No class is named; a table and a profile say what is
      played.
      - Bots/Protocol/BotServerPacketDecoder.cs decodes SM_SUMMON_USESKILL: 124 packet
        types, with the decoder's own test, which reads the Java fixture.
      - Bots/World/BotSummonWorldState.cs: the world state holds the spirit's HP as a
        percentage, from the panel and then from each SM_ATTACK_STATUS of the spirit,
        and the list of skills the server has asked the spirit for.
      - Sc/Classes/NaturalSpiritDriver.cs: each beat, and the end of a fight, answers
        every skill asked for with CM_SUMMON_CASTSPELL.
      - Sc/NaturalPriestCombatPolicy.cs and Sc/Classes/NaturalSkillCatalog.cs: a skill
        row says whether it is an order to the spirit and what HP it costs. The table
        sees whether a spirit is out, whether it stands at the target, its HP and how
        far it is.
      - Sc/Classes/NaturalRotationCombatPolicy.cs. A skill for the spirit is refused
        without a spirit; a skill on the spirit when it is out of the skill's range; an
        order at a target until the spirit stands at that target, where its own skill
        reaches; a skill that costs HP when the bot's HP is not above the cost. New in
        the table: the spirit's ladder, steps cast on the spirit at or below a
        percentage of its HP, each with a floor for the bot's own HP, tried after an
        open follow-up and before the attacks. A step cast in the last 3 s has not
        shown its effect yet, and no later step is cast for the same loss.
      - Sc/Classes/NaturalProfileValidator.cs: a skill cast on the spirit is in no
        attack line, and a profile that gives a role to a spirit skill keeps a spirit.
      - SimT/SimulationFastScenarioTests.cs: a SIM trace holds SM_SUMMON_USESKILL.
    - **The Spirit Master.** Spirit Disturbance and Spirit Erosion stand before its own
      spells in both lines. The ladder: Divine Spirit Armor at any HP, which is cast
      when it has the DP; Spirit Wrath Position at 60% of the spirit's HP; Replenish
      Element at 40%, with its own HP above 70%. 51 of its 66 skills have a role now and
      15 a reason.
    - **Proof, the probe row** spirit-master-orders (SimT/SimulationNaturalStarterProbe
      Tests.cs, probe account 98; `bash run/nr/NR-110d/probe.sh <attempt>`). Prepared by
      the director: a level-22 Spirit Master by the starved mosbears where the Cleric's
      level-25 row fights; before each of three fights one thing: the spirit's HP cut
      to 30%, the same again, and 2,000 DP. Run nr110d-probe-a2, on the committed tree:
      - **First fight, the spirit at 30%.** The table casts Spirit Wrath Position. The
        server asks for Command: Earth Wrath 0.7 s later, the bot answers, and the
        spirit casts it 0.5 s after that. Then the spirit is sent, and Spirit
        Disturbance and Spirit Erosion follow: three skills asked for, three answered,
        three cast by the spirit. The spirit's HP goes from 472 to 1,742. Replenish
        Element is not cast.
      - **Second fight, the spirit at 30% again**, Spirit Wrath Position cooling down:
        Replenish Element is cast on the spirit and costs the bot 394 HP; the spirit's
        HP goes from 690 to 2,278.
      - **Third fight, 2,000 DP:** Divine Spirit Armor is cast on the spirit, and 2 DP
        are left.
      - Five skills were asked for over the three fights, and five answered. No death
        and no refused cast repeated.
      - **The first attempt** (nr110d-probe-a1) passed too. In its first fight
        Replenish Element followed Spirit Wrath Position at once, before the order's
        heal had landed, for 394 HP and a life potion. The 3 s of the ladder are from
        that.
    - **The other spirit rows, played again.** spirit-master-walk and
      spirit-master-spirit pass as before. spirit-master-fight holds in all it asserts:
      Spirit Disturbance goes first, and the mosbear stays on the spirit, four strikes
      on it and none on its master. The run fails the log policy on one line of the
      server's audit: the hit time of Flame Bolt was 48 ms short.
    - **Found: a stagger moves a monster and the bot does not see it.** Stone Shock
      staggers its target 2 m back (Java StaggerEffect.calculate 50-69). The server
      moves it (startEffect 41) and sends SM_FORCED_MOVE for a player only (42-44); for
      a monster the landing point is in SM_CASTSPELL_RESULT (140-151), which the bot
      reads only the start of. A staggered monster that then runs at the bot tells its
      place with its next move. One that the spirit holds stays 2 m farther away than
      the bot believes, and the bot's hit time for a spell that flies is short by more
      than the server allows. Shown by a run with one more line printed
      (run/nr/NR-110d/probe-diag.log): the server has the mosbear at 1445.33, 797.36
      and the bot at 1443.46, 796.64. Item NR-110g, taken before NR-110e.
    - **Open for the Spirit Master's rows.** Replenish Element costs a third of the
      bot's HP at level 22, and the ladder of the bot then drinks a life potion: whether
      the heal earns that. What the table does when the spirit is hurt and both heals
      are cooling down.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout+templar, -Parallel 8,
      run guard-p8 (run/nr/NR-110d/guard-p8/verdict.json): verdict pass, all thirteen
      scopes identical. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629
      passed, 16 skipped) and Fast passes (run nr110d-fast, 11 passed).
- [ ] **NR-110g - A stagger moves the monster for the bot too.** Depends: NR-110d
  - Work: Found by NR-110d. Java first: SM_CASTSPELL_RESULT's list of effects, the
    spell status of each and the place it carries for a stagger, a stumble, a lift and
    a pull, and which of them send SM_FORCED_MOVE besides. The bot's decoder reads the
    whole packet, and the world state puts the creature where the packet says. Generic:
    every class and every skill that moves its target. A recorded scope whose bot
    staggers or pulls a monster sees that monster in another place from then on. The
    seven scopes of the Priest and the Cleric must stay identical (rule (j), NR-Q2); if
    one changes, the item stops and goes under Blocked for the operator. A class scope
    that changes is recorded again twice by rule (j).
  - Proof: Row spirit-master-fight passes with no line of the server's audit; the gate
    with the Priest's and the Cleric's seven scopes identical, and every class scope
    identical or recorded again twice, its first difference explained by a stagger.
  - 2026-10-10: stopped by its own rule and written under Blocked (NR-Q17): the change
    is made and proven, and it changes the Cleric's scope b. Nothing of it is committed.
    - **Java.**
      - **Who moves, and who is told.** A stagger, a stumble, a lift and a pull move
        their target on the server (StaggerEffect.startEffect 41, StumbleEffect 42,
        OpenAerialEffect 62, PulledEffect 65), and each sends SM_FORCED_MOVE for a
        player only. A step back sends SM_POSITION for a creature that is no player
        (SimpleRootEffect 54-58).
      - **The cast result.** SM_CASTSPELL_RESULT.writeImpl 121-165: an entry for each
        effect, with the creature, the result, both HP percentages and the spell
        status. A status of 1, 2, 4 or 8 (stumble, stagger, lift, the fall after it) is
        followed by the place; 16 by a heading. A pull and a step back put their place
        there too, with a status that names none, and nothing in the packet says so.
      - **A critical hit can do it too.** One critical hit in ten of a physical skill
        makes the target stumble when the weapon is a polearm, a staff or a greatsword,
        and staggers it with a bow (Effect 534-543, SkillEngine.createCriticalProcEffect
        193-219). A weapon's swing does the same (CreatureController.attackTarget
        342-346), and its place is in SM_ATTACK, which this change does not read yet.
      - **The port** has the same effects and the same packet.
    - **The change** (run/nr/NR-110g/nr110g-change.patch, 147 lines; not committed).
      Bots/Protocol/BotServerPacketDecoder.cs reads the cast result to its end and gives
      the places beside the decoded fields, not among them: a trace writes the fields,
      so every recorded trace keeps this packet as it was. A pull's place is found by
      trying both readings against the packet's length. Bots/World/BotWorldModel.cs
      puts a creature that is no player at its place, as it does for SM_FORCED_MOVE.
    - **Proof, the one-time check** (run/nr/NR-110g/check.log; the check file is not
      committed): nine packets built by hand, among them the Java fixture, a stagger, a
      stumble after a shielded hit, a pull, a spin, a skill of an item, a dash that
      staggers two and a packet cut short; each gives the places it holds and no other.
      The world state moves the monster and no other object.
    - **Proof, the row** spirit-master-fight (run nr110g-probe-a1): it passes with no
      line of the server's audit. The spirit follows the staggered mosbear, seven steps
      where it took five, and the mosbear strikes the spirit four times and its master
      never.
    - **The gate** (run guard-p8, run/nr/NR-110g/guard-p8/verdict.json): eleven scopes
      identical. Scope b differs first at record 125,694 of 131,197: the Cleric casts
      Hallowed Strike, the hit is critical and its staff makes the target stumble, and
      the next decision sees the target at 4.8 m where the recorded one saw 2.8 m. Scope
      mage differs first at record 18,915 of 23,555: after Frozen Shock the next
      decision sees the target at 4.1 m where the recorded one saw 2.4 m. The unit suite
      (4,629 passed, 16 skipped), the seven checks and Fast (nr110g-fast) pass.
- [x] **NR-110h - The Spirit Master without its staggers, until NR-110g.** Depends: NR-110d
  - Work: The way round NR-110g (rule (t)). A stagger puts the monster 2 m from the
    spirit that holds it, at a place the bot does not see, and the server's audit then
    names the bot's hit time. Stone Shock and Frozen Shock leave the Spirit Master's
    lists and are left out with that reason. Chain of Earth stays for its snare and its
    damage. NR-110g gives both back. Only the Spirit Master's own profile changes.
  - Proof: Rows spirit-master-fight and spirit-master-orders pass with no line of the
    server's audit; the full gate identical.
  - 2026-10-10: done. Stone Shock and Frozen Shock are out of the Spirit Master's table,
    and no fight of its rows leaves a monster where the bot does not see it.
    - **The change.** Sc/Classes/NaturalSpiritMasterProfile.cs alone: the roles stone
      and shock leave both lists, and Stone Shock I to III and Frozen Shock I are left
      out with the reason and NR-110g's name. Chain of Earth and Ice Chain stay. 47 of
      its 66 skills have a role now and 19 a reason.
    - **Proof, the rows** (`bash run/nr/NR-110h/probe.sh <attempt>` with ROW set), each
      on its first attempt and with no line of the server's audit.
      - spirit-master-fight (nr110h-probe-fight-a1): the mosbear is dead after 14.4 s,
        where it took 12.6 s with Stone Shock. Flame Bolt is cast twice from 17.6 m. The
        mosbear strikes the spirit four times and its master never.
      - spirit-master-orders (nr110h-probe-orders-a1): five skills asked for and five
        answered, the spirit healed by its order and by Replenish Element, the armor
        cast, as in NR-110d.
    - **Found: a kill by the spirit is read as another player's.** In the second fight
      of the orders row the mosbear is at 1% HP when the turn begins. The spirit's swing
      goes out in its beat and kills it; the bot's Erosion, sent in the same turn, is
      refused on a dead target (STR_SKILL_TARGET_IS_NOT_VALID). The fight loop reads that
      refusal as NR-48 wrote it for another player's kill: the fight ends with no kill
      and the corpse is left alone. The experience of the kill had reached the bot
      before the refusal. Item NR-110i.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout+templar, -Parallel 8,
      run guard-p8 (run/nr/NR-110h/guard-p8/verdict.json): verdict pass, all thirteen
      scopes identical. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629
      passed, 16 skipped) and Fast passes (run nr110h-fast, 11 passed).
- [x] **NR-110i - A kill by the spirit is the bot's own.** Depends: NR-110h
  - Work: Found by NR-110h. The spirit's swing goes out in the beat before the bot's own
    act. When that swing kills, the bot's cast of the same turn is refused on a dead
    target, and the fight loop takes the kill for another player's (NR-48): no kill is
    counted and the corpse is not looted. Java first: where Skill.canUseSkill refuses a
    dead target, and in what order the server sends the kill's experience, its loot and
    that refusal. The loop reads the refusal as its own kill when the kill's experience
    or its loot reached the client before it. Generic: every class; a kill by the bot's
    own lasting damage in that instant is read the same way.
  - Proof: One probe row on a probe account in which the spirit's swing kills in the
    turn of a cast, the fight ends as a kill and the step after a kill runs, the one the
    journey's loot sweep hangs on; the full gate identical.
  - 2026-10-10: done. A cast refused on a dead target is the bot's own kill when the
    kill's reward reached the bot before the refusal.
    - **Java.** NpcController.onDie 150-151 gives the reward while the monster dies:
      the experience and the loot go out then (doReward 201). Skill.canUseSkill 247-250
      refuses a later cast on the dead target with STR_SKILL_TARGET_IS_NOT_VALID. So a
      client that killed has its experience before the refusal of a cast that came
      after. The port has the same lines.
    - **The change, generic.** Sc/NaturalIshalgenJourney.Combat.cs, the cast: the bot
      notes its experience before a cast is sent. On that refusal, with more experience
      than before or the target's loot offered to it, the cast returns to the fight,
      which looks again and finds the kill. Without either, the refusal is another
      player's kill as NR-48 wrote it. No class is named: a kill by a spirit's swing and
      a kill by the bot's own lasting damage in that instant are read alike.
    - **Proof, the row** spirit-master-orders (run nr110i-probe-a1, its first attempt).
      The row now asks every fight for a kill and hands the fight the step after a
      kill, which the journey's loot sweep hangs on. In its second fight the mosbear is
      at 1% when the turn begins. The spirit's swing kills it, the bot's Erosion of the
      same turn is refused, and the bot has 545 experience in hand: the fight ends as a
      kill. Three fights, three kills, and the step after a kill ran three times. No
      record says a target was taken.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout+templar, -Parallel 8,
      run guard-p8 (run/nr/NR-110i/guard-p8/verdict.json): verdict pass, all thirteen
      scopes identical. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629
      passed, 16 skipped) and Fast passes (run nr110i-fast, 11 passed).
- [x] **NR-110e - A monster on the spirit is an attacker.** Depends: NR-110i
  - Work: Found by NR-110c: the bot counts as an attacker only a creature that strikes
    the bot itself. Java first: what the master is told of a strike at its spirit
    (SM_ATTACK, the hostile spells, SM_SUMMON_UPDATE). The attacker rule takes the bot's
    own spirit: a creature that strikes the spirit, or casts a hostile skill at it, is
    an attacker in the fight's count, in the rest's defence and in a retreat, for as
    long as the spirit is out. Generic: every place that asks who attacks, and every
    class that keeps a spirit. What the table then does with a swarm that is on the
    spirit is read from the row and is a rule for the class's own rows.
  - Proof: One probe row on a probe account in which a monster that is on the spirit
    alone when a fight ends is fought next or fled from; the full gate identical.
  - 2026-10-10: done. A creature that strikes the bot's spirit, or casts a hostile skill
    at it, is in the bot's fight as one that strikes the bot is.
    - **Java.** A monster keeps the spirit and its master as two enemies and strikes
      the one it hates more (NR-110c). Its swing goes to everyone who sees it as
      SM_ATTACK (CreatureController.attackTarget 347-349), and its skills as
      SM_CASTSPELL and SM_CASTSPELL_RESULT, whoever the target is. So the master sees
      every strike at its spirit; SM_SUMMON_UPDATE (SummonController.onAttack) adds the
      spirit's HP for the master alone. No server change.
    - **The change, generic.** Bots/Navigation/NaturalCombatRetreatPolicy.cs: the three
      readings of who attacks take the bot's own spirit, and a swing or a hostile skill
      aimed at it names its attacker. What the bot and its spirit cast on each other
      names none. Without a spirit nothing is read differently. It is asked with the
      spirit in the fight's count of attackers, in the reading of a target that gave
      up, in a retreat's list of pursuers, in a rest's defence, in the journey's look
      for monsters still engaged before a new pull, and in the defence on a walk
      (Sc/NaturalIshalgenJourney.Combat.cs, .cs and .Navigator.cs,
      Sc/NaturalJourneyRuntime.cs). One reading stays the bot's alone: only a strike at
      the bot says that the target stands at the bot.
    - **Proof, the row** spirit-master-pack (SimT/SimulationNaturalStarterProbeTests.cs,
      probe account 100; run nr110e-probe-a1, its first attempt). The fight of row
      spirit-master-fight: a level-16 Spirit Master and its Earth Spirit at a tusked
      mosbear among its family. The spirit goes first and hits after 2.5 s. By then a
      ruthless mosbear has struck the bot and the tusked mosbear the spirit: the first
      decision counts two attackers, of which one has not struck the bot. The table
      leaves at two: it casts Root at the target, calls the spirit back and retreats
      150 m from both. A cub strikes the spirit on the way. The spirit runs with its
      master, 161 steps to the bot's 195. After 25 s nothing follows: no death, the bot
      at full HP, the spirit at full HP in guard mode 3.0 m from its master.
    - **What the row says of the table.** In NR-110c the bot killed this mosbear in
      13.9 s with one creature on itself and three on its spirit, none of the three
      counted. Counted, they make the table leave at once, as the Mage's number of two
      says. Whether a class whose spirit takes the hits should leave at more than two
      is a rule for its own rows (NR-111).
    - **The other spirit rows, played again** (run/nr/NR-110e/probe-<row>-a1.log): all
      four pass. spirit-master-fight is the same fight and now ends in the same
      retreat; orders, walk and spirit are as before.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout+templar, -Parallel 8,
      run guard-p8 (run/nr/NR-110e/guard-p8/verdict.json): verdict pass, all thirteen
      scopes identical. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629
      passed, 16 skipped) and Fast passes (run nr110e-fast, 11 passed).
- [x] **NR-110f - Where the spirit fights.** Depends: NR-110e
  - Work: Found by NR-110c: the spirit that goes first stands at its target, inside the
    circles of the target's neighbours, and draws them. Java first: the aggro range and
    the call for help that an attack makes. The rule: the spirit goes first only at a
    target that stands clear of every other monster's circle, by the pack the walk-in
    plan already counts (CP-56a). At any other target the bot pulls by its table and the
    spirit meets the target near its master. Generic: every profile that keeps a spirit.
  - Proof: One probe row on a probe account with two fights: a target that stands clear,
    where the spirit goes first and no other monster strikes it, and a target in a
    family, where the bot pulls and the spirit fights beside its master; the full gate
    identical.
  - 2026-10-10: done. The spirit goes first only at a target that stands clear. At any
    other target the bot pulls by its table, and the spirit is sent once the target is
    within 8 m of its master.
    - **Java.** Two things bring a second monster into a fight. A monster's own circle:
      it takes whoever comes within its aggro range. And the call for help
      (AggroEventHandler.onCreatureNeedsSupport 26-35): a monster whose tribe may help
      the one that is hit joins against the attacker when it is within its own aggro
      range and 2 m of either of them (isInSupportRange 51-52). So a helper comes
      whoever strikes and wherever the fight is, and goes for the one who struck. A
      monster that is no helper comes only for someone inside its circle: for a spirit
      that stands at the target, and not for a bot that pulls from 17 m. The mosbear
      cubs help the tusked mosbear and it helps them (tribe_relations.xml); the starved,
      fierce and ruthless mosbears help no one.
    - **The change, generic.** Sc/NaturalIshalgenJourney.Combat.cs: at the fight's
      first attack, a bot with a spirit asks who would join a fight at the target's own
      place, with the count the walk-in plan uses (CP-56a: the helpers and every circle
      that reaches the spot). With no one, the spirit goes first as before. With anyone,
      the spirit is held back: the bot pulls, and the spirit has its first beat when
      the target is within 8 m of the bot (Sc/Classes/NaturalSpiritDriver.cs,
      `MeetMetres`), and every beat from then on. Orders on the spirit are answered in
      the meantime. A probe can ask for that count (NaturalIshalgenJourney
      .ObservedPackOf). No class is named; a bot without a spirit asks nothing.
    - **Proof, the row** spirit-master-place (SimT/SimulationNaturalStarterProbeTests.cs,
      probe account 98; run nr110f-probe-a1, its first attempt). Prepared by the
      director: a level-16 Spirit Master by the starved mosbears where the Cleric's
      level-25 row fights. They stand alone there and help no one, so before the second
      fight the director spawns one more 5 m beside the next mosbear, on the side away
      from the bot.
      - **A mosbear that stands clear.** No one would join. The spirit is sent from
        21.0 m, 3.7 s before the bot's first cast, and strikes four times. The kill
        takes 12.2 s. Only the mosbear strikes the spirit or the bot.
      - **A mosbear with a neighbour.** The count names the neighbour. The spirit is
        held back with the mosbear 20.5 m away. The bot casts first; 3.5 s later the
        mosbear is 4.2 m from the bot and the spirit is sent. The kill takes 13.2 s. The
        neighbour strikes no one and lives.
    - **The rows of NR-110c and NR-110e stood at the tusked mosbear's family** and no
      longer showed their subject there: a cub is counted, the spirit is held back, the
      mosbear comes to the bot with its helpers, and the table leaves at two
      (run/nr/NR-110f/rows-a1/). Both now stand by the starved mosbears.
      - spirit-master-fight (nr110f-probe-fight-a2): the spirit goes first from 21.0 m,
        the kill takes 12.2 s, the mosbear strikes the spirit four times and its master
        never, and after a walk of 77 m the spirit is 3.0 m from its master again.
      - spirit-master-pack (nr110f-probe-pack-a2): the director sets a second mosbear
        on the spirit alone. Nothing strikes the bot; two creatures strike the spirit.
        The first decision counts one and casts; the next counts two, and the table
        casts Root and leaves both.
      - spirit-master-orders, -walk and -spirit pass as before.
    - **Open for the Spirit Master's rows.** In the second fight the spirit swung once
      in 5.4 s: it reached the mosbear only with the bot's next act, after a 1.8 s cast.
      A beat comes with each act of the bot (NR-110c), and a long cast is a long wait.
      And the spirit stood 13 m from the mosbear when it was sent, behind its master: a
      walk of the bot takes the spirit along only while the bot walks (NR-110b).
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout+templar, -Parallel 8,
      run guard-p8 (run/nr/NR-110f/guard-p8/verdict.json): verdict pass, all thirteen
      scopes identical. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629
      passed, 16 skipped) and Fast passes (run nr110f-fast, 11 passed).
- [ ] **NR-111 - Spirit Master: probe rows.** Depends: NR-110f
  - Work: Rows spirit-master-10, spirit-master-16, spirit-master-20 and spirit-master-25
    in SimulationNaturalStarterProbeTests: prepared Spirit Masters on the two probe
    accounts, in the gear the route has given by that level, fight the monsters the
    Cleric's rows fight. Each row's trace shows the table in play: the spirit out and
    sent, the orders, Erosion, Chain of Earth, the servants, Vacuum Choke from 22, Stone
    Skin up, both ladders, the rest with the powder. What a row shows decides the open
    rules: which spirit serves before level 16; whether Root of Enervation or Body Root
    earn their mana; and those NR-110d to NR-110f left open: at how many attackers a
    class leaves whose spirit takes the hits, whether Replenish Element earns the HP
    and the potion it costs, and whether the spirit needs a beat inside the bot's long
    casts. A fix is one small change (rule (i)).
  - Proof: The four rows end with the monster dead or a recorded retreat, no refused
    cast repeated and no skill outside the table cast.
- [ ] **NR-112 - Spirit Master: to Altgard.** Depends: NR-111; ticked by the round that gives it
  - Work: A fresh Asimspirit plays Ishalgen as a Mage, the trial, the Spirit Master
    choice at Munin, the ceremony with the spellbook and the dispatch Q2903, and is
    captured at the Altgard bind as altgard-spirit-master-s1.
  - Proof: The capture verifies.
- [ ] **NR-113 - Spirit Master: Altgard legs l1 to l5.** Depends: NR-112; ticked by its round
  - Work: The Cleric's legs in order, each end captured under the Cleric's snapshot name
    with -spirit-master (NR-41). In leg 1 it casts Erosion at the fungus in flight.
  - Proof: Each capture verifies.
- [ ] **NR-114 - Spirit Master: Altgard legs l6 to l11.** Depends: NR-113; ticked by its round
  - Work: As NR-113. Leg 11 is the destiny quest: its stone is the Sorcerer's, 140000004
    (NR-39).
  - Proof: Each capture verifies.
- [ ] **NR-115 - Spirit Master: coin gear and Haramel.** Depends: NR-114; ticked by its round
  - Work: The coin-gear leg (cloth, by its manifest, NR-38a) and Haramel with chest
    700831 (NR-40), captured as altgard-coingear-spirit-master and
    altgard-rc-complete-s1-spirit-master.
  - Proof: Each capture verifies.
- [ ] **NR-116 - Spirit Master: the Abyss entry.** Depends: NR-115; ticked by its round
  - Work: Q24020, then Q2945, Q2946, Q2947 and Q2042 with the coin tiers it can wear
    (NR-38b, NR-Q14, NR-Q16), captured as morheim-abyss-entry-s1-spirit-master.
  - Proof: The capture verifies.
- [ ] **NR-117 - Spirit Master: the endpoint.** Depends: NR-116; ticked by its round
  - Work: Captured and verified as ntc-ready-spirit-master-s1: alive at Morheim Ice
    Fortress, level 25 or higher, Q2945, Q2946, Q2947 and Q2042 complete.
  - Proof: The capture verifies.
- [ ] **NR-118 - Spirit Master: the class scope.** Depends: NR-117
  - Work: One scope of the Spirit Master's play, chosen where its rules differ most from
    the Mage's, recorded by a bot alone, twice, and added to the gate's sets.
  - Proof: The two recordings are identical.
- [x] **NR-120 - Gunner: survey and profile data.** Depends: the close of phase C, NR-03
  - 2026-10-10: done. The Gunner has a class line, a gear table and a profile the
    validator accepts. Nothing has been played. Two of its skills ask for a form the
    table does not have: items NR-120a and NR-120b.
    - **Java.**
      - **What it learns.** SkillLearnService as read for NR-50. To level 26 the
        Gunner, with the Engineer it was, learns 57 active skills by itself, the three
        common ones among them, and none is a toggle. 22 rows from level 20 are stigma
        rows and one is a skill book, Homeward Bound at 21; the route buys no book
        (run/nr/NR-120/check.log).
      - **A pistol, and two.** Every shot's start condition names the pistol; two
        skills of level 25 name the aethercannon, which the Gunner may hold from level
        20 (WeaponCondition as read for NR-100). From level 10 most of what is new also
        asks for a second weapon in the off hand or a two-hand weapon
        (LeftHandCondition.validate 26-38, as read for NR-50b): Spend Success,
        Bulletproof, Crosstrigger, Canted Shot, Wing Clip, and of those left out Reload,
        Trunk Shot, Volley, Automatic Fire and Parting Shot. The Gunner learns to hold
        two weapons at level 10 (skill 55), as the Scout does at 5.
      - **Chains.** Gunshot opens the chain Rapidfire follows, twice, and Automatic
        Fire (level 16) follows Rapidfire, twice. Crosstrigger (22) opens the chain
        Canted Shot follows, twice. Trunk Shot (13) opens the chain Volley follows,
        twice. Every step keeps its chain open every time (chain_skill_prob 100).
        Automatic Fire is the one follow-up that asks more of the step before it:
        Rapidfire must have been cast twice (ChainCondition 22-23 and 42, precount).
      - **One cooldown for three openers.** Gunshot, Trunk Shot and Crosstrigger share
        the cooldown 1802: 16 s after Gunshot, 24 s after either of the others. So one
        chain is opened at a time.
      - **Reload** ends that cooldown at once (SkillCooltimeResetEffect.applyEffect
        30-47: every cooldown from first_cd to last_cd loses delta percent of what is
        left, here all of 1802) and tells the player with SM_SKILL_COOLDOWN.
      - **Skills that move.** Trunk Shot staggers its target 2 m (NR-110g). Parting Shot
        throws the Gunner 15 m back by the cast's result alone (BackDashEffect, as read
        for NR-100).
      - **Class rewards on the route** are those of the table under NR-42: the pistol
        101800506 at the ceremony (Q2009, choice 1) and the dispatch Q29070 (NR-30), the
        leather rewards and the pistols of NR-32, the stone 140000002 (NR-39), Haramel's
        chest 700830 (NR-40) and the leather coin armor with a pistol at Lateni and Nott
        (NR-38). Nothing new.
    - **The class line.** `engineer-gunner`: an Engineer who becomes a Gunner, SIM
      account 41, character Asimgunner, the pistol 101800506 at the ceremony (NR-Q5).
      Its Engineer holds one pistol, as the line `engineer` does. It is in
      NaturalClassLine.All and in the script's list; the script test holds the two
      together.
    - **The gear table** (Sc/Classes/NaturalClassGearTable.cs, Gunner).

      | Part | The Gunner's | From |
      |---|---|---|
      | Weapon group | pistol; ranked by the magical stat | NR-Q5, CP-Q7 |
      | Off hand | a second pistol from level 10, when skill 55 is observed | NR-Q5, NR-Q6 |
      | Armor | leather, robe, clothes; item level first, the type breaks ties | NR-Q7, CP-Q24 |
      | Kept | life potions, its help kit, a mana potion it finds, every help scroll and food | as the Cleric from Ascension |

      NR-Q5 gives the Gunner "the weapon their skills need". All but two of its skills
      to level 26 need a pistol and most need two; with an aethercannon it would have
      those two. So the table names the pistol alone. Every pistol of the route reaches
      20 m. The second pistol is the one the Engineer already holds when the ceremony
      gives the Karmic Pistol.
    - **The help kit from level 10** is the manifest written under NR-50
      (NaturalHelpItemAllowlist.Kit(caster: false, reagent: true)), the Templar's: no
      mana serum and no Awakening scroll (NR-Q13). Levels 1 to 9 are the Engineer's kit
      (CP-Q12).
    - **Its skills by role** (Sc/Classes/NaturalGunnerProfile.cs; 40 skills of 57).

      | Role | Skill, and the levels of its ranks | What it is |
      |---|---|---|
      | direct | Direct Shot: 1, 6, 11, 16, 21, 26 | A shot that is ready every 2 s. Free to level 20; 31 and 42 MP from 21. |
      | gunshot | Gunshot: 3, 8, 13, 18, 23 | Opens the first chain. 8 to 31 MP, 16 s. |
      | rapid | Rapidfire: 5, 10, 15, 20, 25 | Follows Gunshot inside 2 s, twice. 14 to 51 MP. |
      | cross | Crosstrigger: 22 | Opens the second chain: a hard shot that takes 80 MP from its target. Free; two pistols. |
      | canted | Canted Shot: 22 | Follows Crosstrigger inside 2 s, twice. Free; two pistols. |
      | hot | Hot Shot: 9, 14, 19, 24 | A shot that lowers fire resistance and slows its target's attacks for 10 s. 50 MP and more, 24 s. |
      | grenade | Green Grenade: 10, 15, 20, 25 | A light hit that roots its target for 4 s. 27 MP and more, 24 s. |
      | clip | Wing Clip: 25 | A hard shot once a minute; what it does to a flyer does nothing on the ground. 50 MP; two pistols. |
      | success | Spend Success: 10 | 2,000 DP: for 2 min 150 more magic boost and shots 15% faster. Two pistols. |
      | resist | Bullet Resistance: 7, 12, 17 | The Engineer's: half of every hit for 10 s. 42 MP and more, 1 min. |
      | proof | Bulletproof: 16 | 8 s in which every hit misses. 55 MP, 30 s; two pistols. |
      | herb, mp-recovery | Herb Treatment and MP Recovery: 10, 15, 20, 25 | The two powder skills of a rest (NR-50a). |

    - **The rule table, natural-gunner-v1.** The Engineer's with the Gunner's own
      shots. Spend Success when the DP are there. From range Green Grenade first: a
      rooted monster stands 4 s in the pistol's reach. Then Hot Shot, Gunshot and
      Rapidfire twice; Crosstrigger and Canted Shot, which the shared cooldown leaves
      for the time Gunshot cannot be paid for; Wing Clip; Direct Shot for everything
      between. With the monster on it the grenade comes last before Direct Shot. It
      holds an open chain and stands off at 18 m with the Engineer's distances and
      thresholds, and the pistols fire whenever no skill is ready. The ladder is the
      Engineer's with Bulletproof under it: the shield scroll at 50% HP, the life
      potion at or below 75%, Bullet Resistance at 60%, Bulletproof at 45%. It leaves
      at two attackers, or at 25% HP with nothing ready, and roots its target first.
      Between fights it rests as the Templar (NR-50a). It holds for a patrol and
      assesses (NR-37). In flight it fires Direct Shot (NR-36).
    - **Left out, with the reason** (17 skills).

      | Skill, levels | Why it is not cast |
      |---|---|
      | Return, Bandage Heal, Escape: 1 | As for every class (CP-35, CP-Q11). |
      | Automatic Fire: 16, 21, 26 | It follows Rapidfire only after two Rapidfires, and the table's reading of a chain does not count the step before. Item NR-120a. |
      | Reload: 10 | It ends the cooldown of the three openers; the fight loop keeps a cooldown from its own cast and does not read that it was ended. Item NR-120b. |
      | Trunk Shot: 13, 18, 23 | It staggers its target, and the bot does not read where a stagger puts a monster (NR-110g, blocked by NR-Q17). |
      | Volley: 13, 18, 23 | It follows Trunk Shot only. |
      | Parting Shot: 19, 24 | It throws the Gunner 15 m back, to a place only the cast's result names. |
      | Harassing Fire, Incendiary Shell: 25 | They need an aethercannon, and the Gunner holds pistols. |

      Not in the catalog at all: the stigma rows, the book, and the stone's skill 11505,
      as for every class (NR-39).
    - **Proof.** The one-time check, not committed (run/nr/NR-120/check.log): the line
      parses and holds both classes; its character is an Engineer, with nothing in the
      off hand, until the client observes a Gunner; NaturalClassProfiles builds the
      Gunner's profile, which requires the validator: each of the 57 active skills has
      one role or one reason, every follow-up has its opener, no rotation line breaks a
      chain, and the gear groups lie inside the masteries. The check prints the skill
      tree with each chain step's chance to keep its chain, the 40 rows, the 17 reasons,
      the best rank of each role at eight levels, which skills ask for which weapon and
      hand, and the counts four follow-ups ask of the step before them. Gate, set
      all+mage+warrior+artist+engineer+scout+templar, -Parallel 8, run guard-p8
      (run/nr/NR-120/guard-p8/verdict.json): verdict pass, all thirteen scopes
      identical. Seven pre-commit checks pass, the three script tests pass
      (run/nr/NR-120/script-tests.log), Aion.GameServer.Tests passes (4,629 passed, 16
      skipped) and Fast passes (run nr120-fast, 11 passed).
- [x] **NR-120a - A follow-up that needs the step before it twice.** Depends: NR-120
  - Work: Java first: ChainCondition's precount and where a chain step's use count
    comes from (ChainSkills). A skill row holds what a follow-up asks of the step
    before it, and the table's reading of an open chain takes the follow-up only when
    that step was cast so often. Automatic Fire then gets its role in the Gunner's
    table, after Rapidfire. Generic: every class's follow-ups; all the others ask for
    one cast.
  - Proof: A one-time check, or one probe row on a probe account, in which Gunshot,
    Rapidfire twice and Automatic Fire twice follow one another and Automatic Fire is
    decided at no other time; the full gate identical.
  - 2026-10-10: done. The table takes a follow-up only when the step before it was cast
    as often as the follow-up asks, and Automatic Fire is in the Gunner's table.
    - **Java.** ChainCondition.validate 31-48. A follow-up names the category before
      it. When that category is the player's current chain step, the step's use count
      must reach the follow-up's precount (41-43); when it is the step before the
      current one, the follow-up is a repeat of itself and nothing is counted. The use
      count is the number of casts of the current step in a row
      (ChainSkills.updateChain: a cast of the same category adds one, a cast of another
      makes the current step the one before). Of the skills in the profiles written so
      far only Automatic Fire asks for more than one; in the shipped data six others
      do (Aerial Shot, Blast, Body Slice, Muzzle Flash, Revival Wave, Steel Shot). The
      port has the same lines (SkillEngine/Condition/ChainCondition.cs 40-46).
    - **The change, generic.** Sc/NaturalPriestCombatPolicy.cs and
      Sc/Classes/NaturalSkillCatalog.cs: a skill row holds the precount of its chain
      condition. Sc/Classes/NaturalRotationCombatPolicy.cs, the reading of an open
      chain: a follow-up whose step before it is the current step is open only when
      that step was cast so often. Every other follow-up asks for one cast, which an
      open chain always has. With the table's hold on an open chain, the Gunner waits
      for its second Rapidfire and casts no Automatic Fire in its place.
    - **The Gunner.** Automatic Fire I to III have the role auto, after Rapidfire in
      both lists. 43 of its 57 skills have a role now and 14 a reason.
    - **Proof, the probe row** gunner-chain (SimT/SimulationNaturalStarterProbeTests.cs,
      probe account 100; run nr120a-probe-a1, its first attempt). Prepared by the
      director: a level-16 Gunner with the ceremony's pistol beside its own, by the
      starved mosbears where the Cleric's level-25 row fights. The equipment check takes
      a pistol into each hand. Three fights by the table, three kills:
      - Green Grenade, Hot Shot, Gunshot, Rapidfire twice, Automatic Fire; the mosbear
        is dead before a second.
      - Direct Shot three times while the rest cools down, Gunshot, Rapidfire twice;
        dead.
      - Green Grenade, Hot Shot, Gunshot, Rapidfire twice, Automatic Fire twice: the
        five chain casts in 3,970 ms.
      Every Automatic Fire was decided after two Rapidfires, and the server carried out
      each one.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout+templar, -Parallel 8,
      run guard-p8 (run/nr/NR-120a/guard-p8/verdict.json): verdict pass, all thirteen
      scopes identical. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629
      passed, 16 skipped) and Fast passes (run nr120a-fast, 11 passed).
- [x] **NR-120b - A skill that ends a cooldown.** Depends: NR-120a
  - Work: Java first: SkillCooltimeResetEffect and the SM_SKILL_COOLDOWN it sends. The
    fight loop keeps a cooldown from the result of its own cast; it reads the server's
    word for a cooldown it holds and ends it as the server says. A skill row says which
    cooldowns the skill ends, and the table casts such a skill only while one of them
    runs. Reload then gets its role in the Gunner's table: after the chain, while
    Gunshot cools down. Generic: every class with such a skill.
  - Proof: One probe row on a probe account in which Reload is cast while Gunshot cools
    down and Gunshot is cast again before its 16 s are over; the full gate identical.
  - 2026-10-10: done. The fight reads the server's word that a cooldown is over, a
    skill row says which cooldowns the skill ends, and Reload is in the Gunner's table.
    - **Java.** SkillCooltimeResetEffect.applyEffect 30-47: for every cooldown from
      first_cd to last_cd that still runs, what is left loses delta hundredths, in whole
      hundreds, so a delta of 100 ends it and a smaller one changes nothing; without a
      delta it loses value. The player is told with SM_SKILL_COOLDOWN, which lists every
      learned skill of those cooldowns with the seconds left (SM_SKILL_COOLDOWN 25-35 and
      45-53). Reload names the one cooldown 1802 with delta 100. The port has the same
      lines.
    - **The change, generic.**
      - Sc/NaturalPriestCombatPolicy.cs and Sc/Classes/NaturalSkillCatalog.cs: a skill
        row holds the cooldowns the skill ends.
      - Sc/Classes/NaturalRotationCombatPolicy.cs: such a skill is refused unless one of
        its cooldowns has more than 4 s left. It does nothing while none runs, and
        little for one that is nearly over.
      - Sc/NaturalIshalgenJourney.Combat.cs: the fight keeps a cooldown from the result
        of its own cast. With each turn it now reads SM_SKILL_COOLDOWN: a skill the
        server lists with nothing left ends the cooldown the fight still holds for it,
        and the trace says so.
    - **The Gunner.** Reload has the role reload, after Automatic Fire in both lists.
      44 of its 57 skills have a role now and 13 a reason.
    - **Proof, the probe row** gunner-reload (SimT/SimulationNaturalStarterProbeTests.cs,
      probe account 98; `bash run/nr/NR-120b/probe.sh <attempt>`). Prepared by the
      director: a level-10 Gunner with the ceremony's pistol beside its own, by the ice
      crasaurs where the Cleric's level-10 row fights; a pistol in each hand by the
      equipment check.
      - **First attempt** (nr120b-probe-a1, kept): the row's own check failed. It looked
        for the Gunshot before a Reload in the same fight, and the cooldown ran on from
        the fight before. The row now reads the casts of every fight together; the item's
        code was not changed.
      - **Second attempt** (nr120b-probe-a2): passed. The first fight casts Gunshot at
        3.9 s and ends in a kill at 7.1 s. The second begins with Reload, with 12.8 s of
        Gunshot's cooldown left. The server lists Gunshot I and II with nothing left;
        the fight ends the cooldown 0.7 s later, walks into range and casts Gunshot at
        11.8 s, 7.9 s after the first, and Rapidfire twice. Two fights, two kills, no
        death.
      - Row gunner-chain, played again (nr120b-probe-chain-a1), passes: its second fight
        opens with Reload and casts the whole chain in 4,155 ms.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout+templar, -Parallel 8,
      run guard-p8 (run/nr/NR-120b/guard-p8/verdict.json): verdict pass, all thirteen
      scopes identical. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629
      passed, 16 skipped) and Fast passes (run nr120b-fast, 11 passed).
- [ ] **NR-121 - Gunner: probe rows.** Depends: NR-120b
  - Work: Rows gunner-10, gunner-16, gunner-22 and gunner-25 in
    SimulationNaturalStarterProbeTests: prepared Gunners on the two probe accounts, in
    the gear the route has given by that level and with two pistols, fight the monsters
    the Cleric's rows fight. Each row's trace shows the table in play: the stand-off at
    18 m with the pistols firing between skills, Green Grenade first, Hot Shot, the
    Gunshot chain with Automatic Fire and Reload, the Crosstrigger chain from 22, Wing
    Clip at 25, Spend Success at 2,000 DP, Bullet Resistance and Bulletproof on the
    ladder, the grenade before a retreat, the rest with the powder. What a row shows
    decides the open rules: whether a root at range earns the first place, whether
    Direct Shot's mana from level 21 empties the Gunner before a fight ends, and
    whether the Crosstrigger chain should lead when mana is plenty. A fix is one small
    change (rule (i)).
  - Proof: The four rows end with the monster dead or a recorded retreat, no refused
    cast repeated and no skill outside the table cast.
- [ ] **NR-122 - Gunner: to Altgard.** Depends: NR-121; ticked by the round that gives it
  - Work: A fresh Asimgunner plays Ishalgen as an Engineer, the trial, the ceremony with
    the pistol and the dispatch Q29070, and is captured at the Altgard bind as
    altgard-gunner-s1. From level 10 it holds two pistols.
  - Proof: The capture verifies.
- [ ] **NR-123 - Gunner: Altgard legs l1 to l5.** Depends: NR-122; ticked by its round
  - Work: The Cleric's legs in order, each end captured under the Cleric's snapshot name
    with -gunner (NR-41). In leg 1 it shoots the fungus in flight.
  - Proof: Each capture verifies.
- [ ] **NR-124 - Gunner: Altgard legs l6 to l11.** Depends: NR-123; ticked by its round
  - Work: As NR-123. Leg 11 is the destiny quest: its stone is 140000002 (NR-39).
  - Proof: Each capture verifies.
- [ ] **NR-125 - Gunner: coin gear and Haramel.** Depends: NR-124; ticked by its round
  - Work: The coin-gear leg (leather, by its manifest, NR-38a) and Haramel with chest
    700830 (NR-40), captured as altgard-coingear-gunner and
    altgard-rc-complete-s1-gunner.
  - Proof: Each capture verifies.
- [ ] **NR-126 - Gunner: the Abyss entry.** Depends: NR-125; ticked by its round
  - Work: Q24020, then Q2945, Q2946, Q2947 and Q2042 with the coin tiers it can wear
    (NR-38b, NR-Q14, NR-Q16), captured as morheim-abyss-entry-s1-gunner.
  - Proof: The capture verifies.
- [ ] **NR-127 - Gunner: the endpoint.** Depends: NR-126; ticked by its round
  - Work: Captured and verified as ntc-ready-gunner-s1: alive at Morheim Ice Fortress,
    level 25 or higher, Q2945, Q2946, Q2947 and Q2042 complete.
  - Proof: The capture verifies.
- [ ] **NR-128 - Gunner: the class scope.** Depends: NR-127
  - Work: One scope of the Gunner's play, chosen where its rules differ most from the
    Engineer's, recorded by a bot alone, twice, and added to the gate's sets.
  - Proof: The two recordings are identical.
- [x] **NR-130 - Rider: survey and profile data.** Depends: the close of phase C
  - 2026-10-10: done. The Rider has a class line, a gear table and a profile the
    validator accepts. Nothing has been played. Every skill of its own needs its mech,
    and the table does not know whether the bot rides: item NR-130a, which writes the
    Rider's table.
    - **Java.**
      - **What it learns.** SkillLearnService as read for NR-50. To level 26 the Rider,
        with the Engineer it was, learns 77 active skills and 10 toggles by itself, the
        three common skills among them. 22 rows from level 20 are stigma rows and one
        is a skill book, Homeward Bound at 21 (run/nr/NR-130/check.log).
      - **The mech.** Embark is a toggle whose start condition names the cipher-blade.
        RideRobotEffect.startEffect 22-36 takes the mech's kind from the skin of the
        weapon in the main hand and tells everyone with SM_RIDE_ROBOT; the mech ends
        when the weapon is taken off, and with it every effect of a skill that needs it
        (endEffect 38-46). While it is on, the blade reaches 4 m farther and the Rider
        has more defence, parry and resistance. It costs 104 to 136 MP once and nothing
        while it lasts, and is cast standing still. A death ends it as it ends every
        toggle.
      - **Every skill of the Rider's own needs the mech.** RideRobotCondition.validate
        17-24: a player who is not in a mech is refused. 38 active skills and six ranks
        of toggles carry it; only Embark itself, the Engineer's shots and the common
        skills do not.
      - **The Engineer's shots need a pistol** (WeaponCondition, as read for NR-100),
        in the Rider's ranks too, and Embark needs the cipher-blade. So a Rider holds
        the blade and has none of Direct Shot, Gunshot, Rapidfire, Hot Shot, Green
        Grenade and Bullet Resistance.
      - **Toggles.** A Rider may keep two toggles of the hidden kind
        (EffectController.addEffect 76-86, as read for NR-70a): Embark and Mounting
        Frustration are of it. Kinetic Battery is a buff of the open kind.
      - **Class rewards on the route** are those of the table under NR-42: the
        cipher-blade 102100489 at the ceremony (Q2009, choice 1) and the dispatch Q29070
        (NR-30), the chain rewards and the cipher-blades of NR-32, the stone 140000002
        (NR-39), Haramel's chest 700832 (NR-40) and the chain coin armor with a
        cipher-blade at Lateni and Nott (NR-38). The ceremony's blade calls a mech
        (item_templates.xml, robot 2500003). Nothing new.
    - **The class line.** `engineer-rider`: an Engineer who becomes a Rider, SIM account
      41, character Asimrider, the cipher-blade 102100489 at the ceremony (NR-Q5). Its
      Engineer holds a pistol, as the line `engineer` does; from the ceremony on the
      character holds the blade. It is in NaturalClassLine.All and in the script's list;
      the script test holds the two together.
    - **The gear table** (Sc/Classes/NaturalClassGearTable.cs, Rider).

      | Part | The Rider's | From |
      |---|---|---|
      | Weapon group | cipher-blade; ranked by the magical stat | NR-Q5, CP-Q7 |
      | Off hand | nothing | CP-Q10 |
      | Armor | chain, leather, robe, clothes; item level first, the type breaks ties | NR-Q7, CP-Q24 |
      | Kept | life potions, its help kit, a mana potion it finds, every help scroll and food | as the Cleric from Ascension |

    - **The help kit from level 10** is the manifest written under NR-50
      (NaturalHelpItemAllowlist.Kit(caster: false, reagent: true)), the Templar's: no
      mana serum and no Awakening scroll (NR-Q13). Levels 1 to 9 are the Engineer's kit
      (CP-Q12).
    - **Its skills by role** (Sc/Classes/NaturalRiderProfile.cs; 12 skills of 87).

      | Role | Skill, and the levels of its ranks | What it is |
      |---|---|---|
      | embark | Embark: 10, 15, 20, 25 | The mech, kept on as a toggle by the buff check (NR-70a). |
      | herb, mp-recovery | Herb Treatment and MP Recovery: 10, 15, 20, 25 | The two powder skills of a rest (NR-50a). |

    - **The rule table, natural-rider-v1,** names no skill yet. The cipher-blade swings
      whenever its target is in reach; the ladder is the shield scroll at 50% HP and the
      life potion at or below 75%; it leaves at three attackers, as the two plate
      classes do, or at 25% HP with nothing ready. It walks in with the Templar's
      distances, rests as the Templar (NR-50a) and holds for a patrol (NR-37).
    - **Left out, with the reason** (75 skills).

      | Skill, levels | Why it is not cast |
      |---|---|
      | Return, Bandage Heal, Escape: 1 | As for every class (CP-35, CP-Q11). |
      | Bludgeon: 10, 15, 20, 25; Battery: 10, 15, 20, 25; Cinder Cannon: 10, 15, 20, 25; Provoking Whispers: 10, 15, 20, 25; Rocket Punch: 16, 21, 26; Electric Shock: 19, 24; Lightning Tether: 22; Sundering Blade: 25; Overdrive Trigger: 10, 15, 20, 25; Nullification Trigger: 20; Kinetic Battery, a toggle: 16, 21, 26 | They need the mech, and the table does not know whether the bot rides. Item NR-130a gives each its role or its own reason. |
      | Kinetic Slam: 10, 15, 20, 25 | A charge skill; the fight loop holds no charge. |
      | Chilling Wave: 13, 18, 23 | It hits up to eight monsters around the mech; the bot pulls one at a time. |
      | Mounting Frustration, a toggle: 13, 18, 23 | It doubles the Rider's enmity; alone, the monster has no one else to turn to. |
      | Boost: 13; Steam Rush: 22 | They throw the mech 15 m forward or to its target, to a place only the cast's result names. |
      | Fuel Reserves: 25 | It gives back flight time for 250 MP; the journey's flights are short. |
      | Direct Shot: 1, 6, 11, 16, 21, 26; Gunshot: 3, 8, 13, 18, 23; Rapidfire: 5, 10, 15, 20, 25; Hot Shot: 9, 14, 19, 24; Green Grenade: 10, 15, 20, 25; Bullet Resistance: 7, 12, 17, 22 | They need a pistol in hand, and the Rider holds a cipher-blade. |

      Not in the catalog at all: the stigma rows, the book, and the stone's skill 11505,
      as for every class (NR-39).
    - **Proof.** The one-time check, not committed (run/nr/NR-130/check.log): the line
      parses and holds both classes; its character is an Engineer until the client
      observes a Rider; NaturalClassProfiles builds the Rider's profile, which requires
      the validator: each of the 87 skills has one role or one reason, the kept toggle
      is a toggle and no stance, and the gear groups lie inside the masteries. The check
      prints the skill tree, the 12 rows, the 75 reasons, the gear groups, and for each
      of the 87 skills whether it needs the mech: 44 do, 43 do not. Gate, set
      all+mage+warrior+artist+engineer+scout+templar, -Parallel 8, run guard-p8
      (run/nr/NR-130/guard-p8/verdict.json): verdict pass, all thirteen scopes
      identical. Seven pre-commit checks pass, the three script tests pass
      (run/nr/NR-130/script-tests.log), Aion.GameServer.Tests passes (4,629 passed, 16
      skipped) and Fast passes (run nr130-fast, 11 passed).
- [x] **NR-130a - The mech, and the Rider's table.** Depends: NR-130
  - Work: Java first: RideRobotCondition and RideRobotEffect as read for NR-130, and
    what a client is told when the mech comes and goes (SM_SKILL_ACTIVATION,
    SM_RIDE_ROBOT). A skill row says whether the skill needs a mech and whether it is
    one. The table sees whether the bot rides, as the server says it, and refuses a
    skill that needs the mech without it; a cast from the mech is timed by the mech's
    animations, as the server times it (MotionTime.getTimesFor 50-56). Generic: every
    class with such skills. Then the Rider's table: Cinder Cannon from range for the
    pull, Bludgeon and Battery, Rocket Punch, Sundering Blade, Electric Shock and
    Lightning Tether, Provoking Whispers, Overdrive Trigger for its DP, Nullification
    Trigger on the ladder, for Kinetic Battery a role or its reason (it takes 64 MP
    every 6 s while it is on), and what it attacks with in flight.
  - Proof: One probe row on a probe account in which a prepared Rider boards its mech in
    the buff check and fights by its table, and, with the mech ended by the director,
    decides no skill that needs it until the buff check has boarded again; the full
    gate identical.
  - 2026-10-10: done. The bot knows from the server whether it rides, the table casts
    a skill that needs the mech only then, a cast from the mech is timed by the mech's
    animations, and the Rider has its table.
    - **Java.**
      - **The mech comes and goes** with SM_RIDE_ROBOT, sent to everyone who sees the
        player and to the player (RideRobotEffect.startEffect 22-36, endEffect 38-46):
        the player and the mech's id, 0 for none. Embark is a toggle, so
        SM_SKILL_ACTIVATION is sent beside it (Effect.startEffect 670-678, endEffect
        735-737). The mech ends when any weapon leaves the hand and when its effect
        ends, a death included, and its end starts no cooldown.
      - **A skill that needs it** is refused while the player's mech id is 0
        (RideRobotCondition.validate 17-24, Player.isInRobotMode 1611).
      - **Timing.** From a mech the server takes a skill's animation from the mech's
        rows, whatever weapon is held, and counts the first hit from the animation's
        whole length (MotionTime.getTimesFor 50-56,
        MotionData.calculateAnimationTimeUntilFirstHit 59). A client that sends a
        shorter time is logged (Skill.updateHitTime).
      - **A cost in hundredths.** With ratio, a mana cost is that many hundredths of
        the caster's whole mana, in whole numbers (MpCondition.getCost 48-51).
        Nullification Trigger costs 2. Seventeen skills in the shipped data have such a
        cost; none had a role before this item.
      - **Kinetic Battery** takes three tenths of every hit and pays half of what it
        takes with mana (MPShieldEffect, AttackShieldObserver 90-112), costs mana every
        6 s besides, 64 MP in its third rank, and ends by itself after 90 s: a toggle
        lasts its toggle_timer (Effect.startEffect 670-675).
      - The port has the same lines in each place.
    - **The change, generic.**
      - Sc/Protocol/BotServerPacketDecoder.cs reads SM_RIDE_ROBOT (125 packet types
        now), and the world model keeps the bot's own mech id (Sc/World).
      - Sc/NaturalPriestCombatPolicy.cs and Sc/Classes/NaturalSkillCatalog.cs: a skill
        row says whether the skill needs a mech, whether it is one, and a mana cost in
        hundredths. A cost with ratio is no longer read as that much mana.
      - Sc/Classes/NaturalRotationCombatPolicy.cs: a skill that needs a mech is refused
        while the server has not said the bot rides one, and a skill with a cost in
        hundredths while the bot has less than that share.
      - Sc/NaturalJourneyRuntime.cs: while the bot rides, a cast's hit time and the wait
        after it come from the mech's animations.
      - Sc/Classes/NaturalProfileValidator.cs: a profile that gives a role to a skill
        that needs a mech keeps a toggle that is one.
    - **The Rider's table, natural-rider-v1.** 40 of its 87 skills have a role now and
      47 a reason.

      | Where | In cast order |
      |---|---|
      | From range | Cinder Cannon from 20 m, the pull; then it holds where it stands while the monster comes. Electric Shock from 8 m; from 6 m Sundering Blade, Rocket Punch, Bludgeon and Battery, Provoking Whispers. Lightning Tether is not cast from range: it would root the monster outside the mech's reach. |
      | On the target | An open follow-up first: Battery after Bludgeon, Lightning Tether after Electric Shock, each inside 3.5 s. Then Sundering Blade, Rocket Punch, Bludgeon, Electric Shock, Cinder Cannon, Provoking Whispers. The cipher-blade swings whenever no skill is ready. |
      | In a fight | Overdrive Trigger whenever its 2,000 DP are there and its minute is over. |
      | Ladder | The shield scroll at 50% HP, Nullification Trigger at or below 60%, the life potion at or below 75%. It leaves at three attackers, or at 25% HP with nothing ready. |
      | In flight | It swings the cipher-blade, as the Templar swings its sword: Cinder Cannon is ready every 16 s, too seldom for the flight time an air kill is counted with. |

      Kinetic Battery is left out, with the reason above. The distances and the
      movement are the Templar's, a pull from 15 m; Cinder Cannon reaches 5 m farther.
    - **Proof, the probe row** rider-mech (SimT/SimulationNaturalStarterProbeTests.cs,
      probe account 100; `bash run/nr/NR-130a/probe.sh <attempt>`). Prepared by the
      director: a level-16 Rider with the ceremony's cipher-blade, by the starved
      mosbears where the Cleric's level-25 row fights.
      - **First attempt** (nr130a-probe-a1, kept under rows-a1): the row's own check
        failed. It compared the time of the boarding with the time of the first fight
        decision, and the two were the same. The row now reads the trace's order; the
        item's code was not changed.
      - **Second attempt** (nr130a-probe-a2): passed.
        - The equipment check takes the blade; the buff check casts Embark, and the
          server says mech 2500003.
        - The fight from the mech: Cinder Cannon from 10.9 m, Rocket Punch, Bludgeon,
          Battery 1,635 ms after it, Provoking Whispers; a kill in 11.7 s. The casts are
          timed at 1,552, 1,800, 1,633, 1,533 and 1,000 ms, the mech's animations, and
          the server logged no changed hit time.
        - The director gives the level-16 coin blade 102100606. The equipment check
          takes it, and the server says mech 0 and Embark off.
        - The next fight: its rest boards again, the server says mech 2500003, and the
          fight is cast from the mech; a kill in 16.6 s.
        - The last fight, with the mech ended by the director as it begins: 21
          decisions, 16 of them swings, none a skill that needs the mech; of Cinder
          Cannon the table says "The skill needs a mech, and the server has not said the
          bot rides one." A kill in 46.9 s with one life potion, from 1,378 HP to 369.
          The buff check after it boards once more.
      - **What the row shows for NR-131.** On foot the same mosbear takes four times as
        long and three quarters of its HP. A Rider that a fight catches on foot does not
        board in it: a toggle is cast by the buff check only.
    - **Proof.** Gate, set all+mage+warrior+artist+engineer+scout+templar, -Parallel 8,
      run guard-p8 (run/nr/NR-130a/guard-p8/verdict.json): verdict pass, all thirteen
      scopes identical. Seven pre-commit checks pass, Aion.GameServer.Tests passes (4,629
      passed, 16 skipped) and Fast passes (run nr130a-fast, 11 passed).
- [ ] **NR-131 - Rider: probe rows.** Depends: NR-130a
  - Work: Rows rider-10, rider-16, rider-20 and rider-25 in
    SimulationNaturalStarterProbeTests: prepared Riders on the two probe accounts, in
    the gear the route has given by that level and with its cipher-blade, fight the
    monsters the Cleric's rows fight. Each row's trace shows the table in play: the
    mech on, Cinder Cannon from range, the walk in, the Bludgeon chain, Rocket Punch
    from 16, Electric Shock and Lightning Tether from 19 and 22, Sundering Blade at 25,
    Overdrive Trigger at 2,000 DP, Nullification Trigger on the ladder, the rest with
    the powder. What a row shows decides the open rules: whether a Rider sits, eats the
    powder and drinks in its mech; at how many attackers it leaves; whether Kinetic
    Battery's shield earns the mana it takes; whether the mech's own reach calls for
    another distance than the Templar's; and whether a Rider that a fight catches on
    foot boards in it (NR-130a: on foot a kill took four times as long). A fix is one
    small change (rule (i)).
  - Proof: The four rows end with the monster dead or a recorded retreat, no refused
    cast repeated and no skill outside the table cast.
- [ ] **NR-132 - Rider: to Altgard.** Depends: NR-131; ticked by the round that gives it
  - Work: A fresh Asimrider plays Ishalgen as an Engineer, the trial, the ceremony with
    the cipher-blade and the dispatch Q29070, and is captured at the Altgard bind as
    altgard-rider-s1. The blade is in its hands from the ceremony on.
  - Proof: The capture verifies.
- [ ] **NR-133 - Rider: Altgard legs l1 to l5.** Depends: NR-132; ticked by its round
  - Work: The Cleric's legs in order, each end captured under the Cleric's snapshot name
    with -rider (NR-41). In leg 1 it kills the fungus in flight.
  - Proof: Each capture verifies.
- [ ] **NR-134 - Rider: Altgard legs l6 to l11.** Depends: NR-133; ticked by its round
  - Work: As NR-133. Leg 11 is the destiny quest: its stone is 140000002 (NR-39).
  - Proof: Each capture verifies.
- [ ] **NR-135 - Rider: coin gear and Haramel.** Depends: NR-134; ticked by its round
  - Work: The coin-gear leg (chain, by its manifest, NR-38a) and Haramel with chest
    700832 (NR-40), captured as altgard-coingear-rider and
    altgard-rc-complete-s1-rider.
  - Proof: Each capture verifies.
- [ ] **NR-136 - Rider: the Abyss entry.** Depends: NR-135; ticked by its round
  - Work: Q24020, then Q2945, Q2946, Q2947 and Q2042 with the coin tiers it can wear
    (NR-38b, NR-Q14, NR-Q16), captured as morheim-abyss-entry-s1-rider.
  - Proof: The capture verifies.
- [ ] **NR-137 - Rider: the endpoint.** Depends: NR-136; ticked by its round
  - Work: Captured and verified as ntc-ready-rider-s1: alive at Morheim Ice Fortress,
    level 25 or higher, Q2945, Q2946, Q2947 and Q2042 complete.
  - Proof: The capture verifies.
- [ ] **NR-138 - Rider: the class scope.** Depends: NR-137
  - Work: One scope of the Rider's play, chosen where its rules differ most from the
    Engineer's, recorded by a bot alone, twice, and added to the gate's sets.
  - Proof: The two recordings are identical.
- [x] **NR-140 - Bard: survey and profile data.** Depends: the close of phase C
  - 2026-10-10: done. The Bard has a class line, a gear table and a profile the
    validator accepts. Nothing has been played. Everything it casts fits the table as
    it is, so the survey names no form item; the last of the ten surveys is done.
    - **Java.**
      - **What it learns.** SkillLearnService as read for NR-50. To level 26 the Bard,
        with the Artist it was, learns 63 active skills by itself, the three common
        skills among them, and no toggle. 20 rows from level 20 are stigma rows and one
        is a skill book, Homeward Bound at 21 (run/nr/NR-140/check.log).
      - **Every skill of its own needs a harp** (WeaponCondition), which it holds from
        its creation on.
      - **A song cast on a friend or itself** (first target TARGETORME, relation
        FRIEND) is turned onto the caster when the selected target is an enemy
        (FirstTargetProperty 26-41), as the Artist's Soothing Melody has been since
        CP-47. Soothing Counterpoint, Resonating Melody and Etude are of that kind.
      - **The chains.** Song of Ice opens the chain Song of Fire follows and Song of
        Earth after it, each inside 3 s. Soothing Melody opens the one Soothing
        Counterpoint follows inside 3 s; a follow-up cast on the caster follows whatever
        is selected, as the Warrior's Rage does (ChainCondition). Syncopated Echo,
        Bright Strike and Resonating Melody each open a chain nothing learned by level
        26 follows; as another chain's first step, each ends an open chain.
      - **Syncopated Echo** puts an effect on its target that lasts 5 s and, when its
        time is over, applies the damage skill it names (DelayedSkillEffect.endEffect
        22-26), as the Sorcerer's Delayed Blast does.
      - **Protective Ode** takes every hit until it has taken its value, 1,067 in its
        fourth rank, or 5 min are over (ShieldEffect.startEffect 37-43,
        AttackShieldObserver 90-121). It is ready every 2 min.
      - **Resonating Melody** gives mana at once and every 2 s for 30 s more, at no
        cost, once in 30 s: 765 and 240 in its fourth rank.
      - **Class rewards on the route** are those of the table under NR-42: the harp
        102000523 at the ceremony (Q2009, choice 1) and the dispatch Q29071 (NR-30), the
        robe rewards and the harps of NR-32, the stone 140000001 (NR-39), Haramel's
        chest 700831 (NR-40) and the robe coin armor with a harp at Lohaban and Vebna
        (NR-38). Nothing new.
    - **The class line.** `artist-bard`: an Artist who becomes a Bard, SIM account 41,
      character Asimbard, the harp 102000523 at the ceremony (NR-Q5). It is in
      NaturalClassLine.All and in the script's list; the script test holds the two
      together.
    - **The gear table** (Sc/Classes/NaturalClassGearTable.cs, Bard).

      | Part | The Bard's | From |
      |---|---|---|
      | Weapon group | harp; ranked by the magical stat | NR-Q5, CP-Q7 |
      | Off hand | nothing | CP-Q10 |
      | Armor | robe, clothes; item level first, the type breaks ties | NR-Q7, CP-Q24 |
      | Kept | life potions, its help kit, the mana potions, every help scroll and food | as the Cleric from Ascension |

    - **The help kit from level 10** is every row of the manifest written under NR-60
      (NaturalHelpItemAllowlist.Kit(caster: true, reagent: true)), as the Sorcerer's.
      Levels 1 to 9 are the Artist's kit (CP-Q12).
    - **Its skills by role** (Sc/Classes/NaturalBardProfile.cs; 52 skills of 63).

      | Role | Skill, and the levels of its ranks | What it is |
      |---|---|---|
      | pulse | Pulse: 1, 6, 11, 16, 21, 26 | The pull and the filler: 25 m, a 1 s cast, ready every 2 s. |
      | ice, fire, earth | Song of Ice: 3, 8, 13, 18, 23; Song of Fire: 10, 15, 20, 25; Song of Earth: 15, 20, 25 | The chain: Song of Ice has a 1 s cast and 12 s between; the two that follow are instant. |
      | echo | Syncopated Echo: 10, 15, 20, 25 | An instant whose damage lands 5 s later; every 16 s. |
      | strike | Bright Strike: 24 | An instant attack every 16 s. |
      | flair | Minstrel's Flair: 10, 15, 20, 25 | The attack paid with 2,000 DP. |
      | heal, counterpoint | Soothing Melody: 5, 10, 15, 20, 25; Soothing Counterpoint: 17, 22 | Its heal, a 1 s cast, and the instant heal that follows it. |
      | resonate | Resonating Melody: 10, 15, 20, 25 | Mana at no cost, by the table's mana step and first in a rest for mana (NR-60a). |
      | ode, etude | Protective Ode: 10, 15, 20, 25; Etude: 22 | Kept up by the buff check: the shield, and a tenth more HP for an hour. The table also puts the shield up before the first hit. |
      | captivate | Captivate: 10 | A root for 8 s from 25 m, cast before a retreat. |
      | herb, mp-recovery | Herb Treatment and MP Recovery: 10, 15, 20, 25 | The two powder skills of a rest (NR-50a). |

    - **The rule table, natural-bard-v1,** is the Artist's with what the Bard adds.

      | Where | In cast order |
      |---|---|
      | From range | Song of Ice, Song of Fire and Song of Earth, an open follow-up always first; Syncopated Echo; Minstrel's Flair; Bright Strike; Pulse. |
      | On the target | The instants first, which a hit cannot push back: Syncopated Echo, Bright Strike, Minstrel's Flair; then the songs and Pulse. |
      | Ladder | The shield scroll at 50% HP, the life potion at or below 75%, and at or below 55% Soothing Counterpoint while Soothing Melody has opened it, else Soothing Melody. The attacks keep Soothing Melody's mana back. |
      | Mana | Resonating Melody at or below half its mana, before any mana potion. |
      | Leaves | At two attackers, or at 25% HP with nothing ready; Captivate first when it is ready. |
      | In flight | Pulse. |

      The harp swings only when no attack can be paid for. It stands off at the Priest
      line's distances, rests with the powder first, then Soothing Melody, and holds for
      a patrol (NR-37).
    - **Left out, with the reason** (11 skills).

      | Skill, levels | Why it is not cast |
      |---|---|
      | Return, Bandage Heal, Escape: 1 | As for every class (CP-35, CP-Q11). |
      | Fiery Descant: 7, 12, 17, 22 | A charge skill; the bot holds no charge (CP-Q16). |
      | Requiem: 13 | It puts up to six monsters within 7 m to sleep for 6 s; the bot pulls one at a time. |
      | Sonicportation: 15 | It throws the Bard to a place the server picks, for 323 MP. |
      | Purifying Paean: 19 | It removes a physical debuff; no rule reads the bot's own debuffs. |
      | Soaring Sonnet: 25 | It gives back flight time to a group in flight; the journey's flights are short. |

      Not in the catalog at all: the stigma rows, the book, and the stone's skill 11504,
      as for every class (NR-39).
    - **Proof.** The one-time check, not committed (run/nr/NR-140/check.log): the line
      parses and holds both classes; its character is an Artist until the client
      observes a Bard; NaturalClassProfiles builds the Bard's profile, which requires
      the validator: each of the 63 skills has one role or one reason, no line casts
      another chain's opener between a song and its follow-up, and the gear groups lie
      inside the masteries. The check prints the skill tree, the 52 rows, the 11 reasons
      and the gear groups. Gate, set all+mage+warrior+artist+engineer+scout+templar,
      -Parallel 8, run guard-p8 (run/nr/NR-140/guard-p8/verdict.json): verdict pass, all
      thirteen scopes identical. Seven pre-commit checks pass, the three script tests
      pass (run/nr/NR-140/script-tests.log), Aion.GameServer.Tests passes (4,629 passed,
      16 skipped) and Fast passes (run nr140-fast, 11 passed).
- [ ] **NR-141 - Bard: probe rows.** Depends: NR-140
  - Work: Rows bard-10, bard-16, bard-20 and bard-25 in
    SimulationNaturalStarterProbeTests: prepared Bards on the two probe accounts, in the
    gear the route has given by that level and with its harp, fight the monsters the
    Cleric's rows fight. Each row's trace shows the table in play: Protective Ode up
    before the pull and Etude from 22, the stand-off, Song of Ice with Song of Fire and
    from 15 Song of Earth behind it, Syncopated Echo, Pulse between, Bright Strike at
    25, Minstrel's Flair at 2,000 DP, Soothing Melody and from 17 Soothing Counterpoint
    on the ladder, Resonating Melody at half its mana, Captivate before a retreat, the
    rest with the powder. What a row shows decides the open rules: whether Captivate at
    the pull earns its mana, as the Gunner's grenade is asked; whether Protective Ode
    holds through a fight; whether a heal between Song of Ice and its follow-ups costs
    the chain too often; and at how many attackers it leaves. A fix is one small change
    (rule (i)).
  - Proof: The four rows end with the monster dead or a recorded retreat, no refused
    cast repeated and no skill outside the table cast.
- [ ] **NR-142 - Bard: to Altgard.** Depends: NR-141; ticked by the round that gives it
  - Work: A fresh Asimbard plays Ishalgen as an Artist, the trial, the ceremony with the
    harp and the dispatch Q29071, and is captured at the Altgard bind as altgard-bard-s1.
  - Proof: The capture verifies.
- [ ] **NR-143 - Bard: Altgard legs l1 to l5.** Depends: NR-142; ticked by its round
  - Work: The Cleric's legs in order, each end captured under the Cleric's snapshot name
    with -bard (NR-41). In leg 1 it shoots the fungus in flight.
  - Proof: Each capture verifies.
- [ ] **NR-144 - Bard: Altgard legs l6 to l11.** Depends: NR-143; ticked by its round
  - Work: As NR-143. Leg 11 is the destiny quest: its stone is 140000001 (NR-39).
  - Proof: Each capture verifies.
- [ ] **NR-145 - Bard: coin gear and Haramel.** Depends: NR-144; ticked by its round
  - Work: The coin-gear leg (robe, by its manifest, NR-38a) and Haramel with chest
    700831 (NR-40), captured as altgard-coingear-bard and altgard-rc-complete-s1-bard.
  - Proof: Each capture verifies.
- [ ] **NR-146 - Bard: the Abyss entry.** Depends: NR-145; ticked by its round
  - Work: Q24020, then Q2945, Q2946, Q2947 and Q2042 with the coin tiers it can wear
    (NR-38b, NR-Q14, NR-Q16), captured as morheim-abyss-entry-s1-bard.
  - Proof: The capture verifies.
- [ ] **NR-147 - Bard: the endpoint.** Depends: NR-146; ticked by its round
  - Work: Captured and verified as ntc-ready-bard-s1: alive at Morheim Ice Fortress,
    level 25 or higher, Q2945, Q2946, Q2947 and Q2042 complete.
  - Proof: The capture verifies.
- [ ] **NR-148 - Bard: the class scope.** Depends: NR-147
  - Work: One scope of the Bard's play, chosen where its rules differ most from the
    Artist's, recorded by a bot alone, twice, and added to the gate's sets.
  - Proof: The two recordings are identical.

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

- **Recorded by NR-50, blocking nothing: a counter skill of two statuses asks for
  none.** 30 skills carry two statuses in counter_skill ("BLOCK,RESIST" on 22,
  "RESIST,PARRY" on 6, "RESIST,DODGE" on 2). Java holds the attribute as one AttackStatus
  (SkillTemplate.java 104-105), JAXB leaves it empty for such a value, and Skill.java
  163-169 then asks for no block, parry, dodge or resist before the skill. The port
  mirrors that on purpose (SkillEngine/Model/SkillTemplate.cs 163-176). In retail these
  are usable only after one of the two. It is a defect Java shares, so by rule (t) it is
  written here and not changed. The bots go round it: a profile leaves such a skill out
  with this reason (the Templar's Shield Counter, Avenging Blow and Courageous Shield).
  To decide: whether the port should read both statuses, as a logged retail correction
  offered upstream.
- **NR-110g, blocked by NR-Q17: where a stagger puts a monster.** The bot does not read
  the place a cast result gives for a staggered, stumbled, lifted or pulled monster, so it
  believes the monster 2 m nearer than it is until the monster's next move. The fix is
  written and proven (the item's record; the patch run/nr/NR-110g/nr110g-change.patch)
  and changes the Cleric's scope b, where a critical Hallowed Strike with the staff
  makes its target stumble, and the Mage's scope. NR-Q2 keeps the Cleric's scopes
  identical, so nothing is committed. No class is parked: the Spirit Master, whose
  spirit holds a staggered monster in place, plays without its two staggers by NR-110h.
  To decide: NR-Q17. With a yes, NR-110g applies the patch, reads SM_ATTACK for a swing's
  critical too, records b and mage again and gives the Spirit Master its staggers back.

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
- 2026-10-09 — Loop: NR-46 done, no committed code. Scope c, 298 seconds: tracing an edge
  on the ground 51.9% (1.36 million calls, nearly all from 150 grid searches), the
  journey's own code 19.4%, navmesh routes 11.7%, the server 14%; packets, the world
  model, decisions, the trace and the monitor under 1% together. Scope mage lost a third
  of its 30 seconds to two real-time waits for a loot list. Written: NR-46a (grid search),
  NR-46b (waits no packet can end), then after NR-47 NR-46c (the journey's own code) and
  NR-46d (navmesh routes). Next: NR-46a.
- 2026-10-09 — Loop: NR-46a done. 141 of scope c's 150 grid searches find no route and
  are all of the time; most are the retreat trying escape after escape from one spot.
  Inside one instant a trace asked again now gets the answer it got. Scope c: 3 min 28 s
  against 4 min 55 s. Full gate guard-p8, eight at a time: twelve scopes identical in 459
  seconds. Seven checks, unit suite (4,629 passed, 16 skipped) and Fast (nr46a-fast) pass.
  Next: NR-46b, a wait that no packet can end.
- 2026-10-09 — Loop: NR-46b done. The two loot waits and the quest reply's wait no longer
  sit out a real-time limit in the SIM session: with no read pending and an empty queue
  nothing can come, and the answer is given at once. The live session keeps the limit.
  Scope mage: 19 seconds against 30. Full gate guard-p8, eight at a time: twelve scopes
  identical in 446 seconds. Seven checks, unit suite (4,629 passed, 16 skipped) and Fast
  (nr46b-fast) pass. Next: NR-44, bots take turns in one world.
- 2026-10-09 — Loop: NR-44 done. A turn table owns the clock of a world of several bots;
  a bot's wait yields to it. Six starters played Ishalgen in one world, ten game minutes
  apart, twice: the same six traces. Five reached Q2004; the Scout stopped at a hunting
  ground the five before it had emptied, and the others went on. Java: both of two
  players on one monster get the quest's kill. Full gate guard-p8: twelve scopes
  identical. Seven checks, unit suite (4,629 passed, 16 skipped) and Fast (nr44-fast)
  pass. Written: NR-44a. Next: NR-47, the round: runner, outcome records and snapshots.
- 2026-10-09 — Loop: NR-47 done. run-round.ps1 plays a round's worlds side by side,
  captures each world as a round snapshot and resumes from it; a class's capture points
  into its world's dump. Round r1-a1, two worlds of three starters to Munin: three reach
  it, three stop; munin-mage-r1 verifies. Round r2-a1 resumes all six: five at Munin, the
  Engineer stops again. Four of the six stops so far are a bot meeting what another bot
  has taken. Written: NR-48 for it; NR-46c and NR-46d wait for it. Next: NR-44a, a server
  problem laid to the bot whose turn raised it.
- 2026-10-09 — Loop: NR-44a done. A bot's play in a round is marked in the log's scope, and
  its problem policy answers only for what was raised there; the world's problems are
  written into the round's record and stop no bot. One-time check: the Mage with a problem
  in its play stopped, the Warrior beside it reached its end. Full gate guard-p8: twelve
  scopes identical. Seven checks and Fast (nr44a-fast) pass. Next: NR-48, a bot meets
  what another bot has taken.
- 2026-10-09 — Loop: NR-48 done. The refused casts were on monsters another bot had killed
  and that still stood in the bot's view (Java refuses a skill on a dead target with that
  message). Such a target is set aside and the hunt goes on; a quest step that needs a
  kill looks for the next. Round r3-a2, two worlds of three starters: all six reach Munin.
  Full gate guard-p8: twelve scopes identical. Seven checks, unit suite (4,629 passed, 16
  skipped) and Fast (nr48-fast) pass. Next: NR-46c, the journey's own code.
- 2026-10-09 — Loop: NR-46c done, no code. The journey's own code is about a fiftieth of
  scope c. The fifth NR-46 saw was the navmesh router asked directly: 1,178 paths, 36% of
  the scope without their edge traces, and most of the 32% of edge traces are its leg
  checks. NR-46d's text is brought up to these numbers. Next: NR-46d, navmesh routes.
- 2026-10-09 — Loop: NR-46d done. A path's time was the local detour around hazards, which
  asked for every neighbour of every cell whether it lies in a forbidden circle; the
  answer is now kept for the cell. Scope c: 2 min 25 s against 3 min 28 s, and against
  4 min 55 s before NR-46a. Full gate guard-p8: twelve scopes identical in 391
  seconds. Seven checks, unit suite (4,629 passed, 16 skipped) and Fast (nr46d-fast)
  pass. The items for several bots at once are done. Next: NR-30, the class line carries
  the pair, the pick and the dispatch.
- 2026-10-09 — Loop: NR-30 done. A class line names its ceremony pick and ForLine passes
  it; the capital pass has the line's dispatch quest; sim-snapshot.ps1 has one list of
  lines with their second classes, held to the C# list by the script test. One-time check
  on the eleven second classes: only the Cleric's and the Chanter's lists offer the staff,
  so every other line must name its pick. Full gate guard-p8: twelve scopes identical.
  Script tests, seven checks, unit suite (4,629 passed, 16 skipped) and Fast (nr30-fast)
  pass. Next: NR-31, the gates read the line's second class.
- 2026-10-09 — Loop: NR-31 done. The Ishalgen finish, the Altgard and Abyss leg starts, the
  bridge endpoint, the return to Ishalgen, the capital pass and the leg-scoped maps of the
  identity rules ask for the second class of the run's line and refuse every other class
  by name; NA-23 stays the Cleric's. One-time check on the seven lines and on a line for
  each of the nine other second classes. Full gate guard-p8: twelve scopes identical.
  Seven checks, unit suite (4,629 passed, 16 skipped) and Fast (nr31-fast) pass. Next:
  NR-32, reward picks by the class's gear rule.
- 2026-10-09 — Loop: NR-32 done on the second attempt. A leg's reward picks are its
  contract's class's; another class takes what its gear rules choose from the list the
  server offers it, the class list where the quest has use_class_reward (Q2009, Q2947) and
  the general list elsewhere (Q2900 and Q28505 have class lists the server never reads;
  Survey A1 corrected). The reward choice has a bonus order for accessories, hats and ties;
  the Cleric's table has none, because the first gate (guard-p8) showed the order changing
  its recorded pick at Q2227 (NR-Q12, open). One-time check: the table of picks for the
  eleven second classes at fifteen quests. Full gate guard-p8-a2: twelve scopes identical.
  Seven checks, unit suite (4,629 passed, 16 skipped) and Fast (nr32-fast-a2) pass. Next:
  NR-33, protected and kept items by rule.
- 2026-10-09 — Loop: NR-33 done. For a class other than a leg's contract class, the
  coin-gear, Haramel and Abyss-entry scopes protect what every class carries, the class's
  own picks, and what it wears when the leg is taken up; the Cleric's gear ids are dropped.
  The bridge keeps the accessories another pair wears. The clean-up and bundle lists are the
  same for every class. The retained weapon's identity and the purchases stay with NR-38
  and NR-40. Full gate guard-p8: twelve scopes identical. Seven checks, unit suite (4,629
  passed, 16 skipped) and Fast (nr33-fast) pass. Next: NR-34, the help kit by class from
  level 10.
- 2026-10-09 — Loop: NR-34 done. The allowlist's rows have a kind (every class, a class
  that casts from mana, a class with a reagent skill) and a profile takes its kinds; the
  Cleric and the Chanter take both, which is every row. Every second class learns Herb
  Treatment and MP Recovery at level 10, so all take the powder. Defaults for who casts
  and the missing Courage scroll are NR-Q13, open. Full gate guard-p8: twelve scopes
  identical, help-items.json of every scope unchanged. Seven checks, unit suite (4,629
  passed, 16 skipped) and Fast (nr34-fast) pass. Next: NR-35, the bridge's shop and stops
  by class.
- 2026-10-09 — Loop: NR-35 done. At the bridge's shop stop a pair other than the reviewed
  one buys the powder only when its kit has it and the life elixir by its own restock
  rule; with the kit on it buys nothing. Its endpoint asks the gear rule about accessories
  in place of a list by id, which also replaces NR-33's list. Full gate guard-p8: twelve
  scopes identical. Seven checks, unit suite (4,629 passed, 16 skipped) and Fast
  (nr35-fast) pass. Next: NR-36, a shot in flight by role.
- 2026-10-09 — Loop: NR-36 done. The air fight's attack is the class's: a skill of a role
  its profile names that Java lets it cast in flight, hovering inside its reach, or the
  weapon's swing. The Cleric's is Smite from 15 m, as recorded; Mage, Artist and Engineer
  name their ranged roles; Warrior and Scout swing. The skill rows carry the template's
  flight conditions. Full gate guard-p8: twelve scopes identical. Seven checks, unit suite
  (4,629 passed, 16 skipped) and Fast (nr36-fast) pass. Next: NR-37, the patrol rule and
  the blocked-pull view by profile.
- 2026-10-09 — Loop: NR-37 done. The patrol rule asks each class's own fight table for its
  heal, its heal under attack, its DP rescue and its pull limit, and asks nothing about a
  recovery the class lacks. The Cleric's view and wording are as recorded; the Chanter now
  holds and assesses too. Full gate guard-p8: twelve scopes identical. Seven checks, unit
  suite (4,629 passed, 16 skipped) and Fast (nr37-fast) pass. Next: NR-38, coin gear by
  class.
- 2026-10-09 — Loop: NR-38 done as the manifest rule; the two legs that shop are split off
  as NR-38a and NR-38b (rule (q)). Each coin tier has two vendors side by side, and the
  Cleric's sells no plate, no leather and no sword, bow or gun, so every class needs its
  own vendor and manifest. The rule gives all eleven classes five armor pieces and a weapon
  at levels 16, 21 and 26, and gives the Cleric its contracts' lists. Full gate guard-p8:
  twelve scopes identical. Seven checks, unit suite (4,629 passed, 16 skipped) and Fast
  (nr38-fast) pass. Next: NR-38a, the coin-gear leg shops by the class's manifest.
- 2026-10-09 — Loop: NR-38a done. For a class other than the contract's, the coin-gear
  scope is made from its manifest when the leg is taken up: its vendor and tab, the armor
  pieces that beat what it wears, the weapon it holds and its slot, and the coins as
  counted. One-time check: all eleven classes play the leg's decisions from a prepared bag
  to "complete", three pieces for four coins each, and the rule returns the Cleric's own
  contract. Full gate guard-p8: twelve scopes identical. Seven checks, unit suite (4,629
  passed, 16 skipped) and Fast (nr38a-fast) pass. Next: NR-38b, the Abyss entry's two
  tiers shop by the class's manifest.
- 2026-10-09 — Loop: NR-38b done. For a class other than the contract's, the Abyss entry's
  two coin tiers are its own manifests: its vendor and tabs, its five pieces and its
  weapon, the weapon compared by the number its equipment check wears by. No rule trims a
  tier that costs more than the approved 44 Bronze Coins; the leg's refusal stands, and
  the question is NR-Q14 (the recorded Cleric needed 33; the worst bag needs 48 to 67).
  Full gate guard-p8: twelve scopes identical. Seven checks, unit suite (4,629 passed, 16
  skipped) and Fast (nr38b-fast) pass. Next: NR-39, the destiny leg by class.
- 2026-10-09 — Loop: NR-39 done. The destiny campaign's stone is Java's by class (four
  stones for eleven classes), its skill the one of the stone's group, and the class reward
  never given the class's own entry. The bot's table equals the switch in Java's source.
  The Cleric's leg 11, replayed from altgard-l10 before and after the change, gives
  identical traces. Full gate guard-p8: twelve scopes identical. Seven checks, unit suite
  (4,629 passed, 16 skipped) and Fast (nr39-fast) pass. Next: NR-40, Haramel by class.
- 2026-10-09 — Loop: NR-40 done. The Haramel scope is the class's own: its chest by
  Java's table (four chests for eleven classes), the weapon it holds and its slot, its
  armor kind, its coins as counted and its own stone's facts; the incoming binding leaves
  another class's scopes to the journey. The bot's chest table equals the switch in Java's
  source. Full gate guard-p8: twelve scopes identical. Seven checks, unit suite (4,629
  passed, 16 skipped) and Fast (nr40-fast) pass. Next: NR-41, leg starts as minimums, and
  one run through every leg for a line.
- 2026-10-09 — Loop: NR-41 done. A leg's start is a receipt for the contract's class and
  minimums for another: at least the level, the leg's quests untouched among those open,
  one weapon held, whatever coins; the incoming journal gets the class's own dispatch
  quest and is bound to what the character has completed, on every leg. A leg capture of
  another line with a second class must carry the class in its name. Script tests, full
  gate guard-p8 (twelve scopes identical), seven checks, unit suite (4,629 passed, 16
  skipped) and Fast (nr41-fast) pass. Next: NR-42, phase C closed.
- 2026-10-09 — Loop: NR-42 done. Phase C is closed at 8b87eaa08: full gate close-p8 at the
  committed head, twelve scopes identical to their baselines. One table of what each of the
  eleven classes gets at each class-bound point of the route is under NR-42, with what the
  phase leaves unplayed and the four questions open with their defaults (NR-Q11 to
  NR-Q14). Next: NR-50, the Templar's survey, the first item of phase D.
- 2026-10-09 — Loop: NR-50 done. The Templar has its line (warrior-templar, Asimtemplar,
  the sword at the ceremony), its gear table (sword or mace and a shield, plate first) and
  a profile the validator accepts: 35 of its 63 active skills to level 26 by role in the
  table natural-templar-v1, 28 left out with reasons. Nothing played. Four forms it needs
  are items NR-50a to NR-50d (the powder in a rest without a heal, a skill's off-hand
  condition, where the shield comes from, a toggle kept on), then NR-51 to NR-58. New:
  NR-Q15 (the shield), and under Blocked a counter skill of two statuses that Java and the
  port ask nothing for. Full gate guard-p8 (twelve scopes identical), seven checks, script
  tests, unit suite (4,629 passed, 16 skipped) and Fast (nr50-fast) pass. Next: NR-50a, a
  rest with the powder for a class without a heal.
- 2026-10-09 — Loop: NR-50a done. A class on the potion plan that has learned a powder
  skill casts it before the life potion and the sit; the rest skills may name no heal. The
  Templar casts Herb Treatment below 90% HP and MP Recovery from below 25% MP until 50%;
  43 of its 63 active skills have a role. One-time check of the rest for a Templar, a
  Warrior and a Cleric; full gate guard-p8 (twelve scopes identical), seven checks, unit
  suite (4,629 passed, 16 skipped) and Fast (nr50a-fast) pass. Next: NR-50b, a skill's
  off-hand condition.
- 2026-10-09 — Loop: NR-50b done. A skill row carries its left-hand condition (SHIELD or
  DUAL), the fight observes what the bot holds, and the table refuses the skill without
  it. Shield Bash is in the Templar's table, after Dazing Severe Blow and before Ferocious
  Strike; 47 of its 63 active skills have a role. One-time check; full gate guard-p8
  (twelve scopes identical), seven checks, unit suite (4,629 passed, 16 skipped) and Fast
  (nr50b-fast) pass. Next: NR-50c, the shield of a line whose class holds one.
- 2026-10-09 — Loop: NR-50c done for the line and its pick. The Warrior of
  warrior-templar holds a shield from the start, and a class that holds a shield and owns
  none takes an offered shield before a weapon: Raider's Shield at Q2100, the only one
  the route offers; the warrior line is as recorded. NR-Q15 says so. The coin tiers'
  shield is split off as NR-54a, before the coin-gear leg. One-time check; full gate
  guard-p8 (twelve scopes identical), seven checks, unit suite (4,629 passed, 16 skipped)
  and Fast (nr50c-fast) pass. Next: NR-50d, a toggle kept on.
- 2026-10-09 — Loop: NR-50d done by reading, no code. Stubborn Spirit is a toggle and a
  stance, and Java ends a stance when the player starts to cast any skill, so the
  Templar's table leaves it off. The form that keeps a toggle on is left to the Chanter's
  survey (NR-70): its mantras, and later the Gladiator's, the Assassin's and the Rider's
  toggles. Seven checks pass. Next: NR-51, the Templar's probe rows.
- 2026-10-09 — Loop: NR-51 done on the second attempt. Four probe rows, templar-10 to
  templar-25, at the Cleric's places: five kills, two retreats at three attackers, no
  death; the three chains, Shield Bash only with a shield worn, Empyrean Armor in the
  emergency, Rage and Empyrean Chastisement only when hurt, and the rest with the powder
  all shown. One change: Empyrean Chastisement goes first when hurt (it stood last and a
  monster was dead before its turn). Found: Rage after it ends the chain. Full gate
  guard-p8 (twelve scopes identical), seven checks, unit suite (4,629 passed, 16 skipped)
  and Fast (nr51-fast) pass. Next: NR-52, the Templar to Altgard, alone, as the first
  play of the bridge by a line that is not the Priest's.
- 2026-10-09 — Loop: NR-52 played, NR-52a done. The Templar's line stopped twice on the
  bridge, at places only the Priest's line had passed: the trial's fight walked by
  Ishalgen's navigator inside Ataxiar, and the capital pass's end check named the Karmic
  Staff. Both mended: a fight that must walk on another map than its own asks for the
  client's map, and the check asks for the line's ceremony weapon. Replay pilot-a3 then
  reached the Altgard bind: a level-14 Templar with sword and shield, 55 quests, no death,
  in 3 h 28 min of game time. Full gate guard-p8 (twelve scopes identical), seven checks,
  unit suite (4,629 passed, 16 skipped) and Fast (nr52a-fast) pass. Next: NR-52, the
  capture altgard-templar-s1 from the committed code, and its verification.
- 2026-10-09 — Loop: NR-52 done. altgard-templar-s1 is captured at c86110252, its endpoint
  verified by the capture (the Verify action has no form for a bridge snapshot of any line):
  character 133266 of class line warrior-templar, a level-14 Templar bound at
  the Altgard obelisk with the ceremony's sword and Raider's Shield, 55 quests,
  3 h 28 min of game time. The Templar is the pilot for the legs too.
  Next: NR-53, the Templar's Altgard legs l1 to l5, each from the last capture with
  -LaterCapital.
- 2026-10-09 — Loop: NR-53 leg l1 played, NR-53a done. The Templar's first Altgard leg
  stopped at the fungus of Q24011: it hovered 12 m above each and swung 72 times, none
  carried out. A swing's reach is now the weapon's range and the server's metre, and a
  hover point lies inside the attack's reach. Replay l1-a2 then played the leg to its end:
  66 quests, level 15, no death, five fungus at three swings each. Full gate guard-p8
  (twelve scopes identical), seven checks, unit suite (4,629 passed, 16 skipped) and Fast
  (nr53a-fast) pass. Next: NR-53, the capture of leg l1 as altgard-rc-l1-templar from the
  committed code, then legs l2 to l5.
- 2026-10-09 — Loop: NR-53 legs l1 to l3 captured, NR-53b done. altgard-rc-l1-templar,
  altgard-rc-l2-templar and altgard-rc-l3-templar are captured at 23cfb741b: levels 15, 16
  and 17, no death. Leg l4 stopped: walking in put the Templar inside every mosbear pack
  (13 retreats, 22 walks refused, an hour without progress). The Templar now pulls with
  Taunt from range, with the distances of a 15 m skill, and the rule table has pull
  roles: cast once in a fight, waited for while cooling, then the target is gone to.
  Aether Leash stays out: the bot does not read where a dragged monster lands. Replay
  l4-a6 played the leg to its end: level 20, 95 quests, 212 kills, 30 retreats, two
  deaths. Probe rows a3 pass. Full gate guard-p8 (twelve scopes identical), seven checks,
  unit suite (4,629 passed, 16 skipped) and Fast (nr53b-fast) pass. Next: NR-53, the
  captures of legs l4 and l5 from the committed code.
- 2026-10-09 — Loop: NR-53 leg l4 captured, NR-53c done. altgard-rc-l4-templar is
  captured at 3beac1cb0: level 20, 95 quests, two deaths. Leg l5 stopped where a monster
  at its last HP ran off: the Templar's walk went to the nearest monster of that kind and
  not to its target, 971 times. The movement rules now say whether a class walks to its
  own target; the Templar does. Replay l5-a2 played the leg to its end: level 21, 109
  quests, 184 kills, 18 retreats, one death. Full gate guard-p8 (twelve scopes identical),
  seven checks, unit suite (4,629 passed, 16 skipped) and Fast (nr53c-fast) pass. Next:
  NR-53, the capture of leg l5 from the committed code, which closes the item.
- 2026-10-09 — Loop: NR-53 done. altgard-rc-l5-templar is captured at dd96cca3d: level
  21, 109 quests, 10 h 58 min of game time since
  creation. The Templar's five first Altgard legs are captured, after three lettered items
  (NR-53a to NR-53c). Next: NR-54, the Templar's legs l6 to l11.
- 2026-10-09 — Loop: NR-54 legs l6 to l8 captured, NR-54b done. altgard-rc-l6-templar,
  altgard-rc-l7-templar and altgard-rc-l8-templar are captured at dd96cca3d: levels 21,
  22 and 22, no death. Leg l9 stopped: the Templar died while it waited out Return's
  cooldown, was revived at its bind, and Return was then cast on the spot. A bind revive
  during that wait now takes Return's place. Replay l9-a2 played the leg to its end:
  level 23, 152 quests, one death. Full gate guard-p8 (twelve scopes identical), seven
  checks, unit suite (4,629 passed, 16 skipped) and Fast (nr54b-fast) pass. Next: NR-54,
  the captures of legs l9 to l11 from the committed code.
- 2026-10-09 — Loop: NR-54 leg l9 captured, NR-54c done. altgard-rc-l9-templar is
  captured at 13b96c669: level 23, 152 quests, one death. Leg l10 stopped on the road to
  Q2273: a patrol that blocked the route was farther than the Templar's 14 m firing range
  and was never taken, and the navigator walked round the camp for 41 minutes. The
  Templar's fight-through now takes a blocker within 23 m, as every line does. Replay
  l10-a3 passed Q2273 and stopped at Q2277, where a rest's defence could not walk to an
  attacker (NR-54d, written here). Full gate guard-p8 (twelve scopes identical), seven
  checks, unit suite (4,629 passed, 16 skipped) and Fast (nr54c-fast) pass. Next: NR-54d.
- 2026-10-09 — Loop: NR-54d done. A walk to an attacker that shoots from inside its
  neighbours' circles is refused by the fight; in a defence that refusal ended the run,
  at Q2277 in a rest and at Q24014's collect step on the road. The travel defence now
  passes such an attacker over and takes the next, and the rest's defence sets it aside
  and fights the nearest it can. Replays l10-a4 and l10-a6 passed both stops. One bundle
  ran for NR-54d, NR-54e and NR-54f together: full gate guard-p8 (twelve scopes
  identical), seven checks, unit suite (4,629 passed, 16 skipped) and Fast (nr54f-fast2)
  pass. Next: NR-54e.
- 2026-10-09 — Loop: NR-54e done. A stagger landed the Templar on a face steeper than 45
  degrees, where the server lets a character stand (Java StaggerEffect, GeoMap.getZ
  without the slope rule) and the bot's edge check found no ground to start from: no
  route left that point and Q2282's hunt gave up. A first form, which let the edge check
  start there, kept the gate identical but failed a Fast test that holds a pocket of the
  Ishalgen journey closed; it was taken back. The journey's answer to a forced landing
  now stands on the mesh's ground beside such a face. Replay l10-a8 played the leg to its
  end: level 24, 162 quests, no death. Proof by the bundle of NR-54d (twelve scopes
  identical, Fast nr54f-fast2). Next: NR-54f.
- 2026-10-09 — Loop: NR-54f done. Before Bregirun the journey asked every class for the
  Cleric's Hand of Reincarnation. The profile now names the class's self-rebirth; the
  Templar names none and goes in without one. Replays l10-a7 and l10-a8 played the
  Templar's leg l10 to its end (level 24, 162 quests), and a replay of the Cleric's leg
  l10 from altgard-rc-l9 still put its rebirth up and ended verified. Proof by the bundle
  of NR-54d (twelve scopes identical, Fast nr54f-fast2). Next: NR-54, the captures of
  legs l10 and l11 from the committed code.
- 2026-10-09 — Loop: NR-54 leg l10 captured, NR-54g done. altgard-rc-l10-templar is
  captured at 77b893022: level 24, 162 quests, no death. Leg l11 stopped at Heimdall: the
  contract's step expected the Cleric's stone, and the Templar is handed 140000003. The
  step now expects the class's stone, as NR-39 has it for the rest of the campaign. Replay
  l11-a4 played the leg to its end: level 24, 164 quests, no death; a replay of the
  Cleric's leg l11 is unchanged. Full gate guard-p8 (twelve scopes identical), seven
  checks, unit suite (4,629 passed, 16 skipped) and Fast (nr54g-fast) pass. Next: NR-54,
  the capture of leg l11 from the committed code, which closes the item.
- 2026-10-09 — Loop: NR-54 done. altgard-rc-l11-templar is captured at 1c3cb0a62: level
  24, 164 quests, 19 h 20 min of game time since creation. The Templar's legs l6 to l11
  are captured, after six lettered items (NR-54b to NR-54g). Next: NR-54a, the coin tiers
  buy the shield.
- 2026-10-09 — Loop: NR-54a done. A class that holds a shield buys the coin vendor's
  shield from the coins left: in the coin-gear leg after the armor, in the two Abyss tiers
  after the armor and the weapon, on the shield's own trade tab, and wears it in the off
  hand. No coin is added for it. One-time check: the Templar with the bag of leg 11 buys
  shoulders, greaves and the shield for 5 of its 23 Iron Coins. Logged: its two Abyss
  tiers cost 78 Bronze Coins where 44 are approved (NR-Q14). Full gate guard-p8 (twelve
  scopes identical), seven checks, unit suite (4,629 passed, 16 skipped) and Fast
  (nr54a-fast) pass. Next: NR-55, the Templar's coin gear and Haramel.
- 2026-10-09 — Loop: NR-55 coin-gear leg captured, NR-55a done. altgard-coingear-templar
  is captured at df7e4de09: level 24, 165 quests; the Templar bought shoulders, greaves
  and the shield for 5 Iron Coins and wears the shield. Haramel refused its start: the
  check held the Cleric's 19 Iron Coins and three coin pieces. It now asks the class's own
  scope. One bundle ran for NR-55a and NR-55b together: full gate guard-p8 (twelve scopes
  identical), seven checks, unit suite (4,629 passed, 16 skipped) and Fast (nr55b-fast)
  pass. Next: NR-55b.
- 2026-10-09 — Loop: NR-55b done. In Haramel a pack of seven stood on the Templar's way.
  Its defence waited for a spell it does not have, and its walk into the pack was refused
  even after the patrol rule had decided to pull anyway. A class that walks to its target
  now takes the walk-in answer in a defence, and a decided pull is walked into. Replay
  l12-a4 played Haramel to its end: level 25, 176 quests, no death. Proof by the bundle of
  NR-55a (twelve scopes identical, Fast nr55b-fast). Next: NR-55, the capture of Haramel
  from the committed code, which closes the item.
- 2026-10-09 — Loop: NR-55 done. altgard-rc-complete-s1-templar is captured at eea719940:
  level 25, 176 quests, 21 h 20 min of game time since creation. The Templar has played
  every Altgard leg of the accepted line. Next: NR-56, the Templar's Abyss entry; its two
  coin tiers cost 78 Bronze Coins where 44 are approved (NR-Q14).
- 2026-10-09 — Loop: NR-56a done. The Abyss entry stopped at Aegir: the step expected the
  Cleric's hauberk as Q24020's reward, and the Templar picks plate. A step that receives a
  pinned reward now receives the class's pick. Replay ax-a2 then played the four missions:
  the level-21 tier's shoes and shield from the Templar's own 7 Bronze Coins, the arena
  and the ring course each at the first try. One bundle ran for NR-56a and NR-56b
  together: full gate guard-p8 (twelve scopes identical), seven checks, unit suite (4,629
  passed, 16 skipped) and Fast (nr56b-fast) pass. Next: NR-56b.
- 2026-10-09 — Loop: NR-56b done. The Templar ends the Abyss missions at level 25, where
  the Cleric's contract holds level 26 and then the level-26 coin tier. For another class
  the leg's level is now the level it starts from, and a tier above the character's level
  is not asked (NR-Q16, a default for the operator to confirm). Replay ax-a3 played the
  leg to its endpoint: level 25 at Morheim Ice Fortress, the five quests complete, no
  death, no coin supplied. Proof by the bundle of NR-56a (twelve scopes identical, Fast
  nr56b-fast). Next: NR-56, the capture of the leg from the committed code.
- 2026-10-09 — Loop: NR-56 done. morheim-abyss-entry-s1-templar is captured at 362b5baef:
  a Templar of level 25 at Morheim Ice Fortress with the five quests complete, 21 h 36 min
  of game time since creation. Next: NR-57, the endpoint ntc-ready-templar-s1.
- 2026-10-09 — Loop: NR-57a done. The snapshot script names a class line's leg capture
  after the accepted line's snapshot with the class behind it, and refused the plan's
  finish name. The Abyss-entry leg's capture may now be named ntc-ready-<class>-s1 as
  well. Script tests, full gate guard-p8 (twelve scopes identical), seven checks, unit
  suite (4,629 passed, 16 skipped) and Fast (nr57a-fast) pass. Next: NR-57, the capture
  and verification of ntc-ready-templar-s1.
- 2026-10-09 — Loop: NR-57 done. ntc-ready-templar-s1 is captured at 37870a51e and
  verified: a Templar of level 25, alive at Morheim Ice Fortress, with Q2945, Q2946,
  Q2947 and Q2042 complete. The pilot class has reached the plan's finish. Next: NR-58,
  the Templar's class scope.
- 2026-10-09 — Loop: NR-58 done. The Templar's class scope is Haramel from
  altgard-coingear-templar. Recorded twice at 86ee86187, identical: 36,504 records, no
  death. The row stands in the baseline file, and the full gate is thirteen scopes from
  here: all+mage+warrior+artist+engineer+scout+templar. The pilot class is complete: every
  item from NR-50 to NR-58 is ticked. Next: NR-60, the Sorcerer's survey.
- 2026-10-10 — Loop: NR-60 done. The Sorcerer has its class line (mage-sorcerer,
  Asimsorcerer, the spellbook at the ceremony), its gear table and a profile the validator
  accepts: 52 of its 68 active skills to level 26 have a role in the table
  natural-sorcerer-v1 and 16 a reason. Sleep is a skill book and is not learned on the
  route. Gain Mana needs a step the table does not have yet (NR-60a). The Sorcerer's items
  NR-61 to NR-68 are written. Full gate guard-p8 (thirteen scopes identical), seven checks,
  script tests, unit suite (4,629 passed, 16 skipped) and Fast (nr60-fast) pass. Next:
  NR-60a.
- 2026-10-10 — Loop: NR-60a done. A rule table may name a mana step, and a class's rest a
  free mana skill. The Sorcerer casts Gain Mana at or below half its mana in a fight,
  before any serum, and first in a rest for mana; it has 56 roles and 12 reasons now.
  One-time check at levels 10, 20 and 25. Full gate guard-p8 (thirteen scopes identical),
  seven checks, unit suite (4,629 passed, 16 skipped) and Fast (nr60a-fast) pass. Next:
  NR-61, the Sorcerer's probe rows.
- 2026-10-10 — Loop: NR-70 done. By rule (w) the surveys come before any class's probe
  rows, so NR-61 waits and the Chanter's survey was taken. Its placeholder is a profile of
  its own: the table natural-chanter-v1, the Priest's with the chain of three, Meteor
  Strike and Incandescent Blow, Thunderbolt Strike after Infernal Blaze, Winter Circle,
  Word of Revival, Protective Ward, and Promise of Earth and Rage Spell kept up. Of its 64
  active skills 58 have a role and 6 a reason; Parrying Strike is a counter skill, which
  the validator refused in the table. The six mantra rows are toggles and need the form of
  item NR-70a. The Chanter's items NR-71 to NR-78 are written. Full gate guard-p8 (thirteen
  scopes identical), seven checks, unit suite (4,629 passed, 16 skipped) and Fast
  (nr70-fast) pass. Next: NR-70a, a toggle kept on.
- 2026-10-10 — Loop: NR-70a done. The form NR-50d left open: a profile names the toggles
  it keeps on, the world state follows the server's SM_SKILL_ACTIVATION, and the buff check
  casts a kept toggle only when it is off. The validator asks every toggle for a role or a
  reason and holds the kept ones to what the server keeps. The Chanter keeps Celerity
  Mantra, Shield Mantra and Revival Mantra; Wind Mantra and the Templar's Stubborn Spirit
  have their reasons. One-time check, and the probe row chanter-mantras (run
  nr70a-probe-a2): three on, none cast twice, three on again after every effect was ended,
  the new rank at level 23 in the old one's place, speed 6 to 7 m/s. Full gate guard-p8
  (thirteen scopes identical), seven checks, unit suite (4,629 passed, 16 skipped) and Fast
  (nr70a-fast2) pass, on the second bundle: the first met one analyzer warning in the probe
  row. Next: NR-80, the Gladiator's survey; the probe rows NR-61 and NR-71 wait for the
  surveys (rule (w)).
- 2026-10-10 — Loop: NR-80 done. The Gladiator has the line warrior-gladiator, which takes
  the greatsword at the ceremony, a gear table (greatsword, then polearm; plate first) and
  the table natural-gladiator-v1: the Warrior's with Wrathful Strike and Rupture after
  Robust Blow, Explosion of Rage, a pull by Taunt and from 19 by Cleave, Second Wind on the
  ladder, Aerial Lockdown, and Slaughter kept on. Of its 56 active skills and two toggles 47
  have a role and 11 a reason. Crashing Blow needs its target in the air, which no rule
  says yet: item NR-80a. The Gladiator's items NR-81 to NR-88 are written. Full gate
  guard-p8 (thirteen scopes identical), seven checks, three script tests, unit suite (4,629
  passed, 16 skipped) and Fast (nr80-fast) pass. Next: NR-80a, a skill that needs its
  target in a state.
- 2026-10-10 — Loop: NR-80a done. The bot decodes SM_ABNORMAL_EFFECT and knows each
  creature's abnormal states; a skill row holds the states its target must be in; the rule
  table casts such a skill first while one is seen and at no other time. The Gladiator
  casts Crashing Blow on a target Aerial Lockdown lifted. One-time check, and the probe row
  gladiator-aerial (runs nr80a-probe-a1 and a2): Crashing Blow 1,002 ms after Aerial
  Lockdown, completed by the server. NR-80's reading of the chain roll is corrected: a
  skill without chain_skill_prob has 100, so Rupture follows Wrathful Strike. Found: the
  mana reserve holds back the free skills too, item NR-80b. Full gate guard-p8 (thirteen
  scopes identical), seven checks, unit suite (4,629 passed, 16 skipped) and Fast
  (nr80a-fast) pass. Next: NR-80b, an attack that costs no mana and the reserve.
- 2026-10-10 — Loop: NR-80b done. An attack of the table's lists that costs no mana is no
  longer held back by the mana kept for a recovery skill: below 298 MP a Gladiator goes on
  with Ferocious Strike, Robust Blow, Rupture and Body Smash, and a Templar below 113 MP
  likewise. A skill outside the lists keeps its refusal, so the eleven recorded decisions of
  the Cleric's scope c that name one read as they did. One-time check for a Gladiator, a
  Templar and a Cleric. Full gate guard-p8 (thirteen scopes identical), seven checks, unit
  suite (4,629 passed, 16 skipped) and Fast (nr80b-fast) pass. Next: NR-90, the Assassin's
  survey; the probe rows NR-61, NR-71 and NR-81 wait for the surveys (rule (w)).
- 2026-10-10 — Loop: NR-90 done. The Assassin has the line scout-assassin, which takes the
  dagger at the ceremony, a gear table (two daggers, leather first) and the table
  natural-assassin-v1: the Scout's with Rune Slash after Soul Slash, Fang Strike and Beast
  Kick, Rune Carve, Divine Strike, Killer's Eye and Flurry in the gaps, and Apply Deadly
  Poison kept up. Of its 64 active skills and one toggle 36 have a role and 29 a reason:
  the counter skills, the skills that move the caster, and the two rune bursts, which need
  a count of the runes on the target, item NR-90a. The Assassin's items NR-91 to NR-98 are
  written. Full gate guard-p8 (thirteen scopes identical), seven checks, three script
  tests, unit suite (4,629 passed, 16 skipped) and Fast (nr90-fast) pass. Next: NR-90a, a
  skill that bursts the runes on its target.
- 2026-10-10 — Loop: NR-90a done. The world state holds each creature's visible effects,
  slot by slot; a skill row holds the rune it carves and the rune it bursts; a rule table
  names the least number of runes a burst is cast at. The Assassin casts Pain Rune and
  Binding Rune at three. One-time check, and the probe row assassin-runes (runs
  nr90a-probe-a2 and a3): Pain Rune decided when the target was seen with three runes and
  at no other time. NR-90's reading is corrected: Soul Slash keeps its chain open one time
  in ten, so Rune Slash is seldom cast. Full gate guard-p8 (thirteen scopes identical),
  seven checks, unit suite (4,629 passed, 16 skipped) and Fast (nr90a-fast) pass. Next:
  NR-100, the Ranger's survey; the probe rows NR-61, NR-71, NR-81 and NR-91 wait for the
  surveys (rule (w)).
- 2026-10-10 — Loop: NR-100 done. The Ranger has the line scout-ranger, which takes the bow
  at the ceremony, a gear table (a bow, leather first) and the table natural-ranger-v1 in
  the form of the Engineer's: a stand-off at 18 m, Entangling Shot first, Stunning Shot
  with Rupture Arrow, Swift Shot with Arrow Strike, Deadshot in the gaps, Transformation:
  Mau for its DP, and Sleep Arrow before a retreat. Of its 73 active skills 33 have a role
  and 40 a reason: the Scout's blade skills need a blade, and the traps, the counter shot,
  the back dash and three one-minute buffs are left out. No new form is asked for. The
  Ranger's items NR-101 to NR-108 are written. Full gate guard-p8 (thirteen scopes
  identical), seven checks, three script tests, unit suite (4,629 passed, 16 skipped) and
  Fast (nr100-fast) pass. Next: NR-110, the Spirit Master's survey.
- 2026-10-10 — Loop: NR-110 done. The Spirit Master has the line mage-spirit-master, which
  takes the spellbook at the ceremony, the Sorcerer's gear table and the table
  natural-spirit-master-v1: the Mage's with Erosion in every rank, Chain of Earth and Stone
  Shock, Summon Wind Servant, Vacuum Choke and Backdraft. Of its 66 active skills 34 have a
  role and 32 a reason. Its four spirits, the three orders, the spirit's heal and its armor
  are left out: a spirit summoned and kept is item NR-110a, a spirit that fights item
  NR-110b. The Spirit Master's items NR-111 to NR-118 are written. Full gate guard-p8
  (thirteen scopes identical), seven checks, three script tests, unit suite (4,629 passed,
  16 skipped) and Fast (nr110-fast) pass. Next: NR-110a, a spirit summoned and kept.
- 2026-10-10 — Loop: NR-110a done. A profile names the spirits it keeps, best first; the
  world state holds the bot's own spirit from SM_SUMMON_PANEL until the server says it is
  gone; the buff check summons the best learned spirit when none is out. The Spirit Master
  keeps the Earth Spirit, and the Fire Spirit before level 16. One-time check, and the
  probe row spirit-master-spirit (runs nr110a-probe-a1 and a2): the Fire Spirit out after one
  check at level 10, nothing summoned while it is out, and the Earth Spirit summoned after
  a release at level 16. Found: the server moves no spirit and swings for none; the owner's
  client drives it. NR-110b is split into three: the spirit follows (NR-110b), fights
  (NR-110c), and takes orders (NR-110d). Full gate guard-p8 (thirteen scopes identical),
  seven checks, unit suite (4,629 passed, 16 skipped) and Fast (nr110a-fast2) pass, on the
  second bundle: the first failed the decoder's contract test. Next: NR-110b, the spirit
  follows its master.
- 2026-10-10 — Loop: NR-110b done. A ground walk of a bot with a spirit out carries the
  spirit's steps (CM_SUMMON_MOVE): the bot's own route, 3 m behind, a start that names
  where it will stand, a step with each of the bot's, a stop. The server puts the spirit
  where each step says and checks no speed. Probe row spirit-master-walk: the first
  attempt's walk ended after 19 m at a segment a mosbear made unsafe, with the spirit
  2.6 m behind; the row now asks for the approach again. Second attempt
  (nr110b-probe-a2): 555 m, 1,432 steps of the bot and 782 of the spirit, the spirit never
  more than 3.0 m from its master, the same spirit at the end. Full gate guard-p8
  (thirteen scopes identical), seven checks, unit suite (4,629 passed, 16 skipped) and
  Fast (nr110b-fast) pass. Next: NR-110c, the spirit fights.
- 2026-10-10 — Loop: NR-110c done. In a fight the bot's spirit goes first: the order
  (CM_SUMMON_COMMAND), the run to the target at the spirit's own speed, a swing each time
  its attack speed allows (CM_SUMMON_ATTACK), and the order to guard when the fight ends;
  the bot holds its own first attack until it sees the spirit's first hit. Probe row
  spirit-master-fight (nr110c-probe-a1 and a2): a level-16 Spirit Master and its Earth
  Spirit kill a tusked mosbear in 13.9 s, the spirit's four swings all shown by the
  server, the mosbear on the spirit first; after the fight the spirit closes 16.5 m to
  3.0 m in a walk of 82 m. Found: two cubs of the target's family struck the spirit and
  were counted by no rule, and the spirit draws them by standing at the target: items
  NR-110e and NR-110f, before the Spirit Master's rows. Full gate guard-p8 (thirteen
  scopes identical), seven checks, unit suite (4,629 passed, 16 skipped) and Fast
  (nr110c-fast) pass. Next: NR-110d, the spirit's orders, its heal and its armor.
- 2026-10-10 — Loop: NR-110d done. The table orders the spirit: the server queues the
  spirit's skill and asks the client (SM_SUMMON_USESKILL), the bot answers
  (CM_SUMMON_CASTSPELL), the spirit casts. A ladder of the spirit's own heals and arms it:
  Divine Spirit Armor for its DP, Spirit Wrath Position at 60% of the spirit's HP,
  Replenish Element at 40% with the bot's own HP above 70%. Probe row spirit-master-orders
  (nr110d-probe-a1 and a2): five skills asked for and five answered over three fights, the
  spirit healed from 472 to 1,742 HP by its order and from 690 to 2,278 by the heal, the
  armor cast for 2,000 DP. Found: a stagger moves a monster 2 m and the bot does not read
  the landing, so a monster the spirit holds is farther than the bot believes and the
  server's audit names the hit time; row spirit-master-fight fails the log policy on that
  line. Item NR-110g, before NR-110e. Full gate guard-p8 (thirteen scopes identical),
  seven checks, unit suite (4,629 passed, 16 skipped) and Fast (nr110d-fast) pass. Next:
  NR-110g, a stagger moves the monster for the bot too.
- 2026-10-10 — Loop: NR-110g stopped and written under Blocked (NR-Q17). The change is
  made and proven: the bot reads the place a cast result gives for a staggered, stumbled,
  lifted or pulled monster and puts the monster there; row spirit-master-fight passes with
  no audit line (nr110g-probe-a1). The gate, run guard-p8: eleven scopes identical; the
  Cleric's scope b differs first at record 125,694, where a critical Hallowed Strike with
  the staff makes its target stumble, and the Mage's at record 18,915, after Frozen Shock.
  NR-Q2 keeps the Cleric's scopes identical, so nothing is committed; the change is kept
  as run/nr/NR-110g/nr110g-change.patch. The Spirit Master goes round it: item NR-110h, its
  table without Stone Shock and Frozen Shock. Next: NR-110h.
- 2026-10-10 — Loop: NR-110h done. Stone Shock and Frozen Shock are out of the Spirit
  Master's table until NR-110g is answered; only its own profile changed. Rows
  spirit-master-fight and spirit-master-orders pass with no line of the server's audit
  (nr110h-probe-fight-a1, nr110h-probe-orders-a1). Found: when the spirit's swing kills in
  the turn of a cast, the refused cast makes the fight loop take its own kill for another
  player's, with no kill counted and no loot: item NR-110i, before NR-110e. Full gate
  guard-p8 (thirteen scopes identical), seven checks, unit suite (4,629 passed, 16
  skipped) and Fast (nr110h-fast) pass. Next: NR-110i, a kill by the spirit is the bot's
  own.
- 2026-10-10 — Loop: NR-110i done. A cast refused on a dead target is read as the bot's
  own kill when the kill's experience or loot reached the bot before the refusal; without
  either it is another player's kill, as before. Probe row spirit-master-orders
  (nr110i-probe-a1): in its second fight the spirit's swing kills the mosbear in the turn
  of the bot's cast, the bot has 545 experience in hand, and the fight ends as a kill; three
  fights, three kills, the step after a kill run three times. Full gate guard-p8 (thirteen
  scopes identical), seven checks, unit suite (4,629 passed, 16 skipped) and Fast
  (nr110i-fast) pass. Next: NR-110e, a monster on the spirit is an attacker.
- 2026-10-10 — Loop: NR-110e done. A creature that strikes the bot's spirit is an
  attacker of the bot's fight: in the count, in a retreat's pursuers, in a rest's defence
  and before a new pull. Probe row spirit-master-pack (nr110e-probe-a1): with a ruthless
  mosbear on the bot and the target on the spirit the first decision counts two, the table
  casts Root and leaves, the spirit runs with its master, and both are whole after 25 s.
  The same fight was won in 13.9 s in NR-110c with the three creatures on the spirit not
  counted: whether a class with a spirit leaves at more than two is for its rows. The four
  other spirit rows pass. Full gate guard-p8 (thirteen scopes identical), seven checks,
  unit suite (4,629 passed, 16 skipped) and Fast (nr110e-fast) pass. Next: NR-110f, where
  the spirit fights.
- 2026-10-10 — Loop: NR-110f done. The spirit goes first only at a target that stands
  clear, by the count of who joins a fight at the target's own place; at any other target
  the bot pulls and the spirit is sent once the target is within 8 m of its master. Probe
  row spirit-master-place (nr110f-probe-a1): at a mosbear that stands alone the spirit is
  sent 3.7 s before the bot's first cast; at one with a neighbour 5 m beside it the bot
  casts first, the spirit is sent with the mosbear 4.2 m from its master, and the neighbour
  strikes no one. Rows spirit-master-fight and spirit-master-pack stood at the tusked
  mosbear's family, where a cub now holds the spirit back; both moved to the starved
  mosbears and pass, as do the three others. Full gate guard-p8 (thirteen scopes
  identical), seven checks, unit suite (4,629 passed, 16 skipped) and Fast (nr110f-fast)
  pass. Next: NR-111, the Spirit Master's probe rows, which wait for the surveys with the
  other classes' (rule (w)); so NR-120, the Gunner's survey.
- 2026-10-10 — Loop: NR-120 done. The Gunner has the line engineer-gunner, which takes
  the pistol at the ceremony, a gear table (two pistols from level 10, leather first) and
  the table natural-gunner-v1 in the form of the Engineer's: Green Grenade first from
  range, Hot Shot, Gunshot and Rapidfire, the free Crosstrigger chain for when Gunshot
  cannot be paid for, Wing Clip, Direct Shot between, Spend Success for its DP, and
  Bulletproof under the Engineer's ladder. Of its 57 active skills 40 have a role and 17 a
  reason. Two ask for a form: Automatic Fire follows Rapidfire only after two of them
  (NR-120a), and Reload ends a cooldown the fight loop keeps by itself (NR-120b). Trunk
  Shot staggers and waits for NR-110g. The Gunner's items NR-121 to NR-128 are written.
  Full gate guard-p8 (thirteen scopes identical), seven checks, three script tests, unit
  suite (4,629 passed, 16 skipped) and Fast (nr120-fast) pass. Next: NR-120a, a follow-up
  that needs the step before it twice.
- 2026-10-10 — Loop: NR-120a done. A skill row holds what a follow-up asks of the step
  before it (chain/precount), and the table takes the follow-up only when that step was
  cast so often. Automatic Fire is in the Gunner's table after Rapidfire. Probe row
  gunner-chain (nr120a-probe-a1): a level-16 Gunner with two pistols kills three mosbears;
  every Automatic Fire is decided after two Rapidfires and none is refused, and the third
  fight casts Gunshot, Rapidfire twice and Automatic Fire twice in 3,970 ms. Full gate
  guard-p8 (thirteen scopes identical), seven checks, unit suite (4,629 passed, 16
  skipped) and Fast (nr120a-fast) pass. Next: NR-120b, a skill that ends a cooldown.
- 2026-10-10 — Loop: NR-120b done. A skill row holds the cooldowns a skill ends; the table
  casts such a skill only while one of them has more than 4 s left; the fight reads
  SM_SKILL_COOLDOWN and ends a cooldown the server lists with nothing left. Reload is in
  the Gunner's table after Automatic Fire. Probe row gunner-reload: the first attempt
  failed on the row's own check, which did not look across fights; the second
  (nr120b-probe-a2) passed: Reload with 12.8 s of Gunshot's cooldown left, the server's
  word read 0.7 s later, and the second Gunshot 7.9 s after the first. Row gunner-chain
  passes with Reload in play. Full gate guard-p8 (thirteen scopes identical), seven
  checks, unit suite (4,629 passed, 16 skipped) and Fast (nr120b-fast) pass. Next: NR-130,
  the Rider's survey; the Gunner's rows NR-121 wait for the surveys (rule (w)).
- 2026-10-10 — Loop: NR-130 done. The Rider has the line engineer-rider, which takes the
  cipher-blade at the ceremony, a gear table (the blade, chain first) and the profile
  natural-rider-v1 with Embark, its mech, kept on as a toggle. Of its 87 skills 12 have a
  role and 75 a reason: 44 need the mech, which the table cannot see yet, and the
  Engineer's shots need a pistol the Rider no longer holds. Its table names no skill until
  NR-130a. The Rider's items NR-130a and NR-131 to NR-138 are written. Full gate guard-p8
  (thirteen scopes identical), seven checks, three script tests, unit suite (4,629 passed,
  16 skipped) and Fast (nr130-fast) pass. Next: NR-130a, the mech and the Rider's table.
- 2026-10-10 — Loop: NR-130a done. The bot reads SM_RIDE_ROBOT and knows its own mech; a
  skill row says whether a skill needs a mech, whether it is one, and a mana cost in
  hundredths; the table refuses a mech skill on foot; a cast from the mech is timed by the
  mech's animations. The Rider's table casts Cinder Cannon for the pull, then the mech's
  arms, with Nullification Trigger on the ladder and Overdrive Trigger for its DP; 40 of
  its 87 skills have a role. Probe row rider-mech: the first attempt failed on the row's
  own order check; the second (nr130a-probe-a2) passed: boards at the buff check, a kill
  from the mech in 11.7 s, the mech ended by a new blade and boarded again by the rest,
  and on foot no mech skill decided and a kill in 46.9 s. Full gate guard-p8 (thirteen
  scopes identical), seven checks, unit suite (4,629 passed, 16 skipped) and Fast
  (nr130a-fast) pass. Next: NR-140, the Bard's survey; the Rider's rows NR-131 wait for
  the surveys (rule (w)).
- 2026-10-10 — Loop: NR-140 done. The Bard has the line artist-bard, which takes the harp
  at the ceremony, a gear table (the harp, cloth) and the profile natural-bard-v1: the
  Artist's table with the song chain to Song of Earth, Syncopated Echo, Bright Strike,
  Minstrel's Flair for its DP, the heal's follow-up on the ladder, Resonating Melody for
  mana, and Protective Ode and Etude kept up. Of its 63 skills 52 have a role and 11 a
  reason; no form item was needed. The Bard's items NR-141 to NR-148 are written. Full
  gate guard-p8 (thirteen scopes identical), seven checks, three script tests, unit suite
  (4,629 passed, 16 skipped) and Fast (nr140-fast) pass. The ten surveys are done. Next:
  the probe rows, side by side (rule (w)), beginning with NR-61, the Sorcerer's.
- 2026-10-10 — Loop: NR-61 tried once, and NR-61a done. In NR-61's first attempt the rows
  sorcerer-10 and sorcerer-16 passed; sorcerer-25 gave up a mosbear that its own Delayed
  Blast was about to kill, and sorcerer-20 was given the same mosbear after it. NR-61a:
  the server takes no skill at a target that a counted hit will kill and answers as for a
  dead one; a skill row says how long a skill's damage is on its way, and the fight waits
  for its own hit and looks again. Probe row sorcerer-blast (nr61a-probe-a1) passed: the
  refusal at 7.3 s, a wait of 4,067 ms, the kill at 11.4 s. Full gate guard-p8 (thirteen
  scopes identical), seven checks, unit suite (4,629 passed, 16 skipped) and Fast
  (nr61a-fast) pass. Next: NR-61 again, with a fresh two attempts.
- 2026-10-10 — Loop: NR-61 done. The Sorcerer's four probe rows pass (nr61-probe-a3 and
  a3b): the two chain pairs, Flame Harpoon, Delayed Blast with the wait of NR-61a, Empyrean
  Fire for its DP, the scroll and the potion at 44% HP, Gain Mana at 44% mana, Root and a
  retreat at two attackers, the rest with the powder. One change to the table: Wind Spear
  is out; it was never reached and is the dearest damage for its mana. Freezing Wind keeps
  its place. Logged: the server's rule for a skill cast several times in a row, which the
  table has no form for. Full gate guard-p8 (thirteen scopes identical), seven checks, unit
  suite (4,629 passed, 16 skipped) and Fast (nr61-fast) pass. Next: NR-71, the Chanter's
  probe rows.
