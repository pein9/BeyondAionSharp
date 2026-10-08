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

## NR checklist

### A. Open

- [x] **NR-00 - Commit the plan.** Depends: none
  - Work: Commit this document, its row in CLAUDE.md's "Where things live" and decision
    D40 in docs/e2e-player-simulation-plan.md. Docs only.
  - Proof: The seven pre-commit checks pass.
  - 2026-10-08: done. This document, the CLAUDE.md row, D40 and a pointer under the
    class-profile plan's closing status are committed together. The seven pre-commit
    checks pass (run/nr/NR-00/checks.log).
- [ ] **NR-01 - Survey: what is the Cleric's alone, from the trial to the endpoint.**
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
- [ ] **NR-02 - Survey: what the Priest and the Cleric do that the table policy does not.**
  Depends: NR-00
  - Work: No code. Set NaturalPriestCombatPolicy, the journey's rest and heal code, the
    buff check, the mana rules and the Cleric's leg-specific fights (air combat, the arena
    of Q2947, the ring course) beside NaturalRotationCombatPolicy, NaturalRestRules and the
    class profile. List what the table cannot say today, and for each say how the table is
    widened for every class (rule (s)). Write "Survey A2" and the items of phase B from it.
  - Proof: The list stands in this document; phase B has its items; seven checks pass.
- [ ] **NR-03 - The off hand in keep-and-sell and in upgrades.** Depends: NR-00
  - Work: Java first (Equipment.java, as read for CP-68). The inventory policy keeps what
    the class's off-hand mode holds: one shield for mode Shield, two weapons for mode
    SecondWeapon. With two weapons held, SelectUpgrades compares a new weapon with the
    worse of the two and puts it in that hand; the two need not match (NR-Q6). Every
    profile still has mode None, so every scope stays identical.
  - Proof: One-time check, not committed, of the keep-and-sell and worst-of-two cases;
    guard: gate p and the five class scopes identical.
- [ ] **NR-04 - The Scout holds two daggers from level 5.** Depends: NR-03
  - Work: Turn mode SecondWeapon on for the Scout line (NR-Q6). SIM probe row on a prepared
    level-5 Scout with two daggers in the bag: both are equipped by packets and a fight is
    fought. Then the Scout's scope is re-recorded twice by rule (j).
  - Proof: The probe row passes with both hands filled, observed in the client's view; the
    Scout scope repeats in two passes; gate p and the other four class scopes identical.
- [ ] **NR-05 - The comparer counts the rest's potions and sits.** Depends: NR-00
  - Work: scripts/sim/trace/compare_traces.py counts rest-life-potion and
    rest-sit-for-health among its step counts, so a class scope shows them. The digest of a
    trace must not change; if it would, the item stops and says so.
  - Proof: scripts/sim/trace/test_compare_traces.py passes and every recorded scope's
    digest is unchanged (gate, set all plus the five class scopes).

### B. The Cleric on the generic rules

- [ ] **NR-10 - Phase B's items.** Depends: NR-02
  - Work: Written by NR-02 (rule (q)). They must end with: the Priest and the Cleric
    profiles built from a generated catalog and a rule table like every other class;
    NaturalPriestCombatPolicy and the Priest-only rest code no longer called; the seven
    scopes re-recorded (rule (p)); and a fresh-create run of the generic Priest who becomes
    a Cleric, through every leg to the endpoint, captured leg by leg under new names and
    preserved at the end as ntc-ready-cleric-s1.
  - Proof: Each item's own; the phase closes with the gate on the re-recorded scopes and
    the Verify of ntc-ready-cleric-s1.

### C. The legs opened by class line

- [ ] **NR-30 - Phase C's items.** Depends: NR-01, and the close of phase B
  - Work: Written by NR-01 (rule (q)). They must end with: the trial fought and the class
    chosen by the line's own profile; the bridge's pick, shop, kept accessories, capital
    pass and endpoint check read from the line; the leg identity rules by line; every
    later-leg contract, reward pin, coin-gear tier and manifest read by class; each
    leg-specific Cleric skill replaced by a role the profile names. No class but the Cleric
    plays a leg in this phase; the Cleric's scopes stay identical (rule (c)).
  - Proof: Each item's own; the phase closes with the full gate identical.

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
