# Natural class profiles: the class seam, the Chanter branch, and levels 1-9 for every starter

Status: planning only, 2026-10-06. Nothing in this document is implemented. It answers the
operator's question "how generic is what we have", fixes the design of the class seam, and
gives the CP checklist that a loop works one item at a time. Line numbers are those of
commit `b45b72a43`.

The plan was drawn from a read-only review of the bot on 2026-10-06: fifteen readers over
the journey, the combat and pull code, gear, contracts, harness, rules, the shipped level
1-9 skill data and quest 2008; three independent plans; three judges; and three critics,
whose blocker and major findings are already folded in. Line references were checked by the
reviewers and a sample was checked again by hand. They move with every edit, so each item
finds its site by the name quoted with it. A fact check and a loop-fit review followed on
the same day. Their findings are folded in too, and the checklist was renumbered once,
before any item was started.

On 2026-10-07 the operator read the plan and answered. The answers stand under Operator
decisions. They turn help items on for every class line, rule out bandages, add a bind at
each Ishalgen quest hub and settle the Warrior's Q2100 pick. Three items were added for
them (CP-05 to CP-07) and the checklist was renumbered a second time, again before any item
was started. Nothing is blocked.

## The request

The operator, 2026-10-06: "OK, so now the next goal is to be able to create Class profiles
and not just have a Cleric. So, how generic is what we have, where we have reusable core
'run the bot' but can use a new combat rotation or other rules for survivability? Some
things like the quest list, ordering, inventory, etc, can be reused probably. But skills,
recovery, when to hit potions or 'what stat is the chooser for gear' will have to be
class-specific. Also, we can approach this where we already have a 'Priest' class that can
get to Ascension, so a Chanter would just diverge at that point instead of being scoped just
to Cleric. SO along those lines, we can create a '1-9' to Munin class select for the other
types. We can go ahead and knock out any other starter-class types 1-9 profiles and rules.
But I bet we would need to do a bit of prep work refactoring what we have to be reusable?"

## Short names used here

| Short | Means |
|---|---|
| `J` | `tests/Aion.Bots/Scenarios/NaturalIshalgenJourney.cs` |
| `Sc/` | `tests/Aion.Bots/Scenarios/` |
| `Nav/` | `tests/Aion.Bots/Navigation/` |
| `UT/` | `tests/Aion.GameServer.Tests/` |
| `SimT/` | `tests/Aion.Simulation.Tests/` |
| `e2e/` | `parity-artifacts/e2e/` |
| `JAVA/` | `../aion-server/game-server/` (branch `4.8`) |
| line | one character's path: `priest-cleric`, `priest-chanter`, `warrior`, `scout`, `mage`, `engineer`, `artist` |
| profile | how one class plays: skills, recovery, pull, gear |
| neutral gate | a same-seed replay whose trace must equal a recorded baseline |

## How generic is what we have

**Short answer.** What we have is generic in mechanism and Priest and Cleric in rule. The
split in the request is right: there is a reusable "run the bot" core, and skills, recovery,
potion use and the gear stat are the class part. Three more things belong to the class part:
how the bot pulls, how it moves inside a fight, and the auto-attack. Prep work does come
first, but it is smaller than the size of the journey file suggests.

**Already reusable, with no class logic in it**

- The bot core: creating a character of any class, target, swing, cast and item use
  (`tests/Aion.Bots/Api/BotApi.cs`), the cast protocol, and hit timing for sixteen weapon
  motions.
- Navigation and the travel planner (apart from the Priest's spell-range constants, see
  CP-18), retreat and rest-spot geometry, and the death rule.
- The fight-loop shell, the cast step (apart from its weapon-motion lookup, see the table
  below) and cooldown tracking.
- The Ishalgen decision loop, the 41-quest contract with its 26 plans, the template-quest
  executor and the hand-written quest executors (apart from Q2132's Priest trainer and var,
  and the campaign's Priest range literals; see CP-20 and CP-21).
- Hub pickup, gathering, the vendor protocol, bind and Return.
- Checkpoints, relog and stop boundaries, and snapshot Capture, Restore and Verify.
- The "1-9 to Munin" endpoint itself. With the bridge off, the journey ends at level 9 with
  Q2008 at START/0, standing at Munin, and that code has no class check (`J:1010-1027`).

**The server needs nothing**

- Java's Q2008 handler maps SETPRO7 to SETPRO17 to all eleven second classes
  (`JAVA/data/handlers/quest/ascension/_2008Ascension.java:141-162`).
- Q2132 "A New Skill" and Q2009 branch on the starting class, and six dispatch quests exist.
- None of the 41 Ishalgen quests is class-gated.
- The skill sweep already passes every starter skill row in SIM.

**Where the class knowledge sits today**

- Two hand-typed skill tables, behind a switch that gives every class but the Cleric the
  Priest's table (`Sc/NaturalClericSkills.cs:93-94`).
- The fight rule `NaturalPriestCombatPolicy.Decide`, and the rest rule in `RestAsync`.
- The pull: a stand-off at 22 m.
- The gear and reward scores, with the Cleric's staff rule.
- The identity rules, which accept a Priest and a Cleric and nothing else.
- Contract literals: SETPRO14, Q2904, the Karmic Staff. Journey literals: the Priest trainer
  203530 and var 4 (J:314, 6691-6694).
- About a dozen reads of `IsCleric` in the journey. Four of them keep help items from every
  class but the Cleric: the supply (J:1505), the shield scroll (J:9130), the mana potion
  (J:9132) and the scrolls of the buff check (J:9596). Every band of the approved list
  starts at level 10 or later (`Sc/NaturalHelpItemAllowlist.cs:22-37`). CP-05 and CP-06
  change that first, by the operator's decision of 2026-10-07.

**The costly part is structure, not class logic.** `RunAsync` is one method of about 8,400
lines whose local functions share about 45 mutable locals. This plan does not split it. The
Priest becomes a Cleric inside one run, so the profile has to be looked up from the observed
class at every decision, and the existing closures can do that. Only the two self-contained
nested classes, the combat class and the navigator, are lifted into their own files, as a
pure move.

**What each class needs**

- **Chanter:** the smallest step. On the server it differs from the Cleric by SETPRO13,
  class id 11 and the name of a reward list. It reuses the whole Priest run to Munin. Every
  leg after the ceremony is Cleric data, so it stops there.
- **Mage and Artist:** they fit the present ranged pull at 22 m, with their own skills and
  recovery.
- **Engineer:** its skills reach as far as its pistol (20 m), so it pulls at 18 m.
- **Warrior and Scout:** a walk-in pull, weapon swings as the filler between skills, and
  recovery without a heal.
- **Recovery without a heal** is less unknown than it looked. Only the Priest (level 1) and
  the Artist (level 5) learn a heal. Every class starts with the same 100 life potions, the
  help kit keeps a better one in stock (CP-05), and Java restores (level + 3) x 8 x
  Health/100 HP every 6 s while sitting (Health: Warrior 110, Scout and Engineer 100, Priest
  and Artist 95, Mage 90). So a class without a heal drinks a life potion when its HP is
  below the target and the potion is ready, and sits while the potion is on its delay. No
  bandage is used: the operator ruled them out on 2026-10-07 (CP-Q11).

**How "nothing changed" is proven.** No test asserts the journey's decisions today. Four
logged pairs of same-seed runs had record-identical traces, so the plan first builds a trace
comparer and a replay that captures nothing. Then three items change the Priest's levels 1-9
on purpose, by the operator's decisions of 2026-10-07: the help kit, its use by the Priest,
and the bind at each Ishalgen quest hub. Only after them are the baselines recorded, twice,
so the baselines hold the new Priest play. Then today's numbers are pinned in a unit test,
and only then does code move. No Priest or Cleric scope is ever recorded a second time.

**So the order is:** eleven items in Phase A (CP-00 to CP-10), eight of proof tools and the
three that change the Priest's levels 1-9 (CP-05 to CP-07); eighteen small items that put
the Priest and Cleric behind the seam with no behavior change (CP-11 to CP-28); five for
the Chanter, the first of which is the gear rule it shares with the new classes (CP-29 to
CP-33); twelve that add what other classes need, check the finished refactor with a full
gate and put a Warrior in the field before the seam is closed (CP-34 to CP-45). Then come
four profile items (CP-46 to CP-49), seventeen items of Phase F (CP-50 to CP-66) and three
to close (CP-67 to CP-69): 70 in all. After the seam a class costs one profile item proven
by two probe rows, one or two checkpoint runs, a capture with its Verify, and its class
scope recorded twice.

**Where a class other than the Priest stops today**

| What happens | Where |
|---|---|
| The identity check throws for any class but Priest and Cleric | `Sc/NaturalJourneyIdentityRules.cs:33-45` |
| A cast throws for any weapon but mace or staff | `Sc/NaturalJourneyRuntime.cs:44-56` |
| Resting throws when no heal is learned | `RestAsync` in `J` |
| Q2132 "A New Skill" asks for the Priest trainer and var 4 | `J:6689-6697` |
| A melee class waits out the fight loop's 1,000-action bound and then throws: inside 25 m the fight rule never walks to a target that will not come | `Sc/NaturalPriestCombatPolicy.cs:338-345`; the bound is `J:8978` and the throw `J:9333` |
| It fights from the Priest's skill table | `Sc/NaturalClericSkills.cs:93-94` |
| It keeps what it wears but sells unworn class gear as "unneeded-or-unusable", and it picks rewards by the Priest's rules: the mace (index 2) at Q2100 and Q2134, the cloth piece (index 0) at the armor quests | `Sc/NaturalIshalgenInventoryPolicy.cs`, fourteen call sites in `J` (six Decide, seven ChooseReward, one passed to NaturalCoinGearSteps at `J:2801`) |
| A gun or spellbook class is walked up to melee | the fight loop's approach and obstacle handling in `J` |

## Start and finish

- **Start:** `main` at `b45b72a43`. The accepted line is the Asmodian Priest who becomes a
  Cleric; its contracts, its forty snapshots and its endpoint `morheim-abyss-entry-s1` stay
  as they are. The snapshot `munin` already holds a level-9 Priest standing at Munin with
  Q2008 at START/0, before the Ascension quest is touched.
- **Changed on purpose, once:** CP-05, CP-06 and CP-07 change how the accepted Priest line
  plays levels 1-9, by the operator's decision of 2026-10-07. The Priest is supplied and
  uses the level 1-9 help kit, and it binds at each Ishalgen quest hub. In a run with the
  bridge on (scopes p and b) the Priest becomes a Cleric at level 9 and finishes Ishalgen as
  a level-10 Cleric, so that Cleric plays Ishalgen with the same two binds and the hub
  revive. The Cleric's kit and every leg from Altgard on are not changed. The old snapshot
  `munin` and every other snapshot stay as they are; none is recaptured, and `munin` keeps
  the Priest who played without the kit and
  without a bind.
- **Finish:** the class seam, with the Priest and Cleric on it and provably unchanged from
  the baselines of CP-08 and CP-09; the Chanter chosen at Munin and preserved after the
  Pandaemonium ceremony as `pandaemonium-chanter-start-s1`; and a Warrior, Scout, Mage,
  Engineer and Artist each played from level 1 to Munin with Q2008 at START/0 and preserved
  as `munin-<class>-s1` (or under the `-a2` name of rule (l), when a first capture was
  rejected).
- **Not in this plan:** anything past those endpoints. See [Out of scope](#out-of-scope).

## The phases

| Phase | Goal |
|---|---|
| A. Open the leg and build the proof tools (CP-00 to CP-10) | Commit the plan with decision D39 and the operator's answers, freeze the per-class server facts in one contract, and build what can say 'nothing changed': a trace comparer, a replay that captures nothing and the gate script. Then three items change the Priest's levels 1-9 by the operator's decision of 2026-10-07 (CP-05 to CP-07): the level 1-9 help kit, its use by the Priest, and the bind at each Ishalgen quest hub. Only then are the baselines recorded, twice, so they hold the new Priest play and no refactor item has moved code yet. Last, one contained run shows that a status-5 stop writes its receipt, before any class run leans on it. |
| B. The class seam, with the Priest and Cleric moved onto it unchanged (CP-11 to CP-28) | Pin the numbers and the gear picks first, as they stand after CP-07, lift the two large nested classes (the navigator and the combat class) out as a pure move, add the class line and the class profile, and route every Priest and Cleric rule on the 1-9 and bridge path through them with exactly the values the baselines recorded. Each item is proven by a same-seed trace-identical replay of a scope that runs the touched code, or by a unit test where nothing calls the new code yet. |
| C. The Chanter branch at Ascension (CP-29 to CP-33) | First the table gear rule, which the Chanter shares with the new classes. Then show on a prepared character that the server pays a Chanter, let the accepted Priest line choose SETPRO13 at Munin and preserve the result after the Pandaemonium ceremony. It depends on Phases A and B only, so a stalled Warrior cannot hold it up. |
| D. What another class needs, and a Warrior in the field before the seam is closed (CP-34 to CP-45) | Add the generic pieces a non-Priest needs (generated catalog, table policy, rest without a heal, any weapon, chain and swing timing, walk-in pull), prove each neutral for the Priest and Cleric, and run the full gate over the finished refactor. Then make the class least like the Priest rest, fight and play its first quests. Only after that close the seam with the ratchet. |
| E. Profiles for Mage, Artist, Engineer and Scout (CP-46 to CP-49) | One profile per class, each proven by two probe rows: first kills at level 1 on a character created by packets, and the follow-up or heal at the level it is learned. Every weapon type has fought through the bot before its long journey. |
| F. Levels 1-9 to Munin, one preserved snapshot per class (CP-50 to CP-66) | Play each new starter through Ishalgen by natural play in bounded steps: a checkpoint at Q2004 for every class, a second at Q2007 for the first ranged and the first melee class, then the Munin capture, proven by restore and resume, and last the class scope recorded twice. |
| G. Close-out (CP-67 to CP-69) | Run the other nine second classes through the class choice on prepared characters as evidence for the next plan, add the off-hand gear mode, and finish with the full gate and the closing status. |

## The seam

### The rule

Pure decisions in the profile, one shared executor in the core. A profile never sends a
packet; the core never names a class.

### 1. Which character: the class line

NaturalClassLine (new, public, Sc/Classes/NaturalClassLine.cs) with Id, Starter, Second (or
none), SimAccountId and CharacterName. Ids: priest-cleric (the default: account 41,
Asimnjour), priest-chanter, warrior, scout, mage, engineer, artist. Every line is a male
Asmodian, as today, so there is no gender field. The line has ONE carrier:
NaturalJourneyOptions.ClassLine, a last optional parameter of the public record at
Sc/NaturalJourneyRuntime.cs:85-91. The journey hands it to the combat class it builds.
INaturalJourneySession gains no member, so both session types (LiveBotSession in
tools/Aion.LiveBots and SimulationL0Session in SimT/) and the 31 positional runtime
constructions (30 `new NaturalJourneyRuntime(` and one target-typed `new(` at
SimT/SimulationAltgardDestinyTravelTests.cs:81) compile unchanged. Later, CP-27 gives the
SIM session type a line of its own, on the concrete type and not on the interface, for its
relog check. The one journey test reads the new variable CP_CLASS. It is not called Profile:
NaturalJourneyRuntime.Profile already means the configuration profile string.

Where a line is defined: the lines are entries of one table in NaturalClassLine.cs. CP-14
adds priest-cleric, and each class's first profile item adds its own line: CP-32 for the
Chanter; CP-42, which adds the Warrior's line with the probe harness; and CP-46 to CP-49.
The class-line contract file holds no line. From CP-28 on, sim-snapshot.ps1 holds the list
of the seven ids and refuses any other; an id that the C# table does not hold yet fails in
NaturalClassLine.Parse with a message that names it.

### 2. How a class plays: the class profile

NaturalClassProfile (new, public, Sc/Classes/), one per PlayerClass, looked up by
NaturalClassProfiles.For(observed class id, line) at every decision and never fixed at
construction, because the Priest becomes a Cleric inside one run. An unobserved class gives
the line's starter (what Sc/NaturalClericSkills.cs:93-94 does today); a class outside the
line throws. The Priest and Cleric profiles are adapters over the untouched static
NaturalPriestCombatPolicy and today's rest rule, and report today's policy version
(mauPolicy.Id). The Chanter profile reuses the Priest adapter. Every other class uses the
table-driven NaturalRotationCombatPolicy with a policy version of its own, a skill catalog
generated in memory from skill_tree.xml and skill_templates.xml (only roles and exclusions
are hand-written) and the table gear rule. Rule tables are compiled C#, not JSON, and no
generated kit file is checked in. The new types are public because NaturalJourneyOptions is
public and Aion.Bots shows internals only to Aion.GameServer.Tests
(tests/Aion.Bots/Aion.Bots.csproj:18).

### 3. What the server decides per class: the class-line contract

e2e/natural-class-lines.json (new, one javaReference, so an upstream port bumps one file)
with loader Sc/Classes/NaturalClassLineContract.cs. e2e/natural-ascension-contract.json
stays byte-identical; NaturalAscensionContract.ForLine and ForChoice(starter, second class)
build any other pair's bridge in memory. The Ishalgen contract and its 26 plans stay shared
and frozen. Its field initialClass PRIEST (e2e/natural-ishalgen-contract.json:7) stays; the
bot loader does not read it, and the class-lines file is the authority for the class.

### 4. Structure

NaturalIshalgenJourney becomes a partial class and its two large nested classes,
NaturalJourneyNavigator (J:8496) and NaturalJourneyCombat (J:8853), move, still private and
nested, to Sc/NaturalIshalgenJourney.Navigator.cs and Sc/NaturalIshalgenJourney.Combat.cs.
The eight nested exception classes, TemplatePhase and CapitalPayment (J:21-33) stay in the
main file. RunAsync, its 45 captured locals, its leg runners, the three single-slot session
hooks (BeforeSend, AfterSynchronize and ResolveForcedLanding) and the exception-based
control flow stay as they are.

### 5. Stays in the core, with no class input

Api, World, Timing, Movement, Protocol; BotCastProtocol; navigation, travel planner and
patrol learning; the fight-loop shell, CastAsync and cooldown tracking; retreat, rest-spot
geometry, rest cadence and the death rule; the Ishalgen decision loop; the 19 hand-written
quest executors and the template executor; hub pickup, gathering, the vendor protocol, the
bind at the working hub's obelisk and Return; the help-item supply and its stock checks
(what a kit holds at a level is the profile's); checkpoints, relog and stop boundaries;
snapshot Capture, Restore and Verify.

### 6. The leg gates stay Cleric-only

the end-of-Ishalgen check (J:1006), the returned-Cleric test
(Sc/NaturalIshalgenDecisionLoop.cs:83, evaluated inside every checkpoint), the capital pass
(Sc/NaturalCapitalDecisionEngine.cs:42-45) and the later-leg requirements (J:1601,
2373, 4540) keep refusing every class but the Cleric, with a message that names the class.
Only the early-ceremony check (J:1398), the bridge talk and the identity classification
(Sc/NaturalJourneyIdentityRules.cs:33-45, called from
Sc/NaturalAscensionDecisionEngine.cs:80, the journey test and the SIM session's relog check)
take the line's second class.

### 7. Not renamed or moved

NaturalPriestCombatPolicy.cs, NaturalClericSkills.cs, NaturalIshalgenJourney.cs,
NaturalJourneyRuntime.cs, NaturalPullPlanner.cs and NaturalMauPolicyParameters.cs; the two
NaturalJourneyStage names (they now mean before and after Ascension); the test method
NaturalIshalgenPriestCompletesFrozenJourneyWithoutSetup; the parsed trace keys pull-plan and
defend-before-pull; every Priest step and label string; every public member
tools/Aion.LiveBots calls. The Mau freeze hashes five of these files by path
(scripts/sim/freeze_mau_phase2.py:17-22). That freeze is historical for all of them: its
scripts are not re-frozen and not run as checks.

### 8. Proof tools and the neutral gate

The tools, all new:

- `scripts/sim/trace/compare_traces.py`: compares two traces record by record after dropping
  the time stamp and run id. Its `--counts` mode says how many deaths, retreats, rests,
  vendor buys and pulls a trace holds, so an item can name a scope that really runs what it
  moves.
- `scripts/sim/sim-snapshot.ps1 -Action Replay`: a fresh schema or a restored snapshot, one
  scope played, the schema dropped, nothing captured. It takes `-StopAt`, `-StopAfterQuest`,
  `-Item` and `-Run`, and writes its evidence under `run/cp/<item>/<run>/`. CP-28 adds
  `-Class`.
- `scripts/sim/run-neutral-gate.ps1`: replays the named scopes and compares each with its
  baseline. One command, one verdict. With `-Record` it plays each named scope twice,
  compares the two passes and writes the baseline rows once, at the end.
- Baselines under `run/cp/baseline/<sha>/`, with their hashes and coverage counts committed
  in `e2e/natural-neutral-baseline.json`. The baseline sha is the commit they were recorded
  at. A second copy of the traces is kept beside the repository, in
  `../BeyondAionSharp-cp-baseline/<sha>/`, as the Java checkout sits in `../aion-server`.
- Gated SIM probes (the class-choice, trainer and starter probes). A gated row runs only
  when the new variable `CP_PROBE_ROWS` names it, in a comma-separated list. A probe is run
  alone like this: `AION_SIM_DB_INTEGRATION=1 CP_PROBE_ROWS=<rows> dotnet test
  tests/Aion.Simulation.Tests --filter "FullyQualifiedName~<theory name>"`. The level-1
  first-kill rows of CP-Q18 are not gated. "The director" in a probe means the SIM account
  99, named director, with access level 9 (SimT/SimulationWorldFixture.cs:207). In a probe
  it is the only GM input, and it never touches a journey. The one GM-style input in a
  journey is the help-item supply, which the SIM host makes for the approved items of every
  class line.

Gate scopes, all seed 1:

| Scope | What it plays | About |
|---|---|---|
| `p` | A fresh Priest with `NA_ASCENSION=1` and `PC_CAPITAL=start`: levels 1-9, the class choice and the ceremony. It holds no Cleric fight and few Priest branches | 38 s |
| `m` | A fresh Priest with the bridge off: all 41 quests to the Munin stop | 5 min 22 s |
| `b` | The `-Bridge` scope: a fresh Priest with early Ascension to the bridge endpoint | 7 min 36 s |
| `c` | Cleric leg `l4`, replayed from `altgard-rc-l3` | 2 min 17 s |
| `l1` | Leg 1 from `altgard` with `-LaterCapital` | 1 min 33 s |
| `hm` | Haramel leg `l12` from `altgard-coingear` | 3 min 53 s |
| `ax` | Abyss entry from `altgard-rc-complete-s1` | 44 s |
| `all` | `p`, `m`, `b`, `l1`, `c`, `hm` and `ax`: every Priest and Cleric scope that CP-08 and CP-09 kept, which is all seven | 22 minutes of play, about 25 with the schemas |
| class scope | `Replay -Class <line> -StopAt 2004:5:0`, one for each new starter: `mage`, `warrior`, `artist`, `engineer`, `scout`. The five are in the gate's table from the start, and each is turned on when its baseline is recorded (CP-53, CP-57, CP-60, CP-63, CP-66) | not measured |

- The times are a pass of the record runs of CP-08 and CP-09, on 2026-10-07.

**What each scope reaches.** From the baseline traces of CP-08 and CP-09. A Priest column
counts what the character does below level 10, a Cleric column from level 10 on. An item
names a scope for the code it moves; this table is where to check that the scope runs it.

| What | p | m | b | l1 | c | hm | ax |
|---|---|---|---|---|---|---|---|
| Game time | 1 h 05 | 3 h 14 | 3 h 23 | 0 h 40 | 2 h 29 | 1 h 50 | 0 h 17 |
| Fight decisions, Priest | 274 | 1,526 | 274 | - | - | - | - |
| Fight decisions, Cleric | - | - | 457 | 280 | 1,590 | 528 | 18 |
| Deaths (each with its revive) | 0 | 1 Priest | 0 | 0 | 1 | 1 | 0 |
| Soul heals | 0 | 1 | 0 | 0 | 1 | 0 | 0 |
| Retreat decisions | 2 | 1 | 3 | 0 | 25 | 8 | 0 |
| Emergency decisions | 0 | 7 | 0 | 0 | 11 | 0 | 0 |
| Between-fight heals | 1 | 15 | 1 | 0 | 3 | 0 | 0 |
| Walks to a rest spot (a sit follows) | 0 | 6 | 1 | 0 | 2 | 0 | 0 |
| Rests interrupted by an attack | 0 | 0 | 0 | 0 | 4 | 0 | 0 |
| Powder-rest decisions | 0 | 0 | 91 | 68 | 179 | 86 | 0 |
| Pull plans | 5 | 148 | 112 | 52 | 201 | 311 | 0 |
| Patrol waits (all the Cleric's) | 0 | 0 | 21 | 9 | 120 | 192 | 0 |
| At-target pulls | 0 | 0 | 0 | 0 | 0 | 9 | 0 |
| Life potions drunk | 18 | 69 | 26 | 4 | 102 | 15 | 2 |
| Mana potions drunk | 0 | 0 | 0 | 0 | 1 | 0 | 0 |
| Shield scrolls used | 0 | 3 | 0 | 0 | 4 | 0 | 0 |
| Running scrolls used | 7 | 20 | 8 | 0 | 0 | 0 | 3 |
| Speed scrolls used | 2 | 7 | 6 | 1 | 5 | 19 | 3 |
| Help items supplied | 6 | 5 | 6 | 0 | 3 | 1 | 2 |
| Binds | 2 | 2 | 3 | 0 | 1 | 1 | 1 |
| Returns cast | 1 | 1 | 3 | 0 | 0 | 0 | 0 |
| Vendor trades with a purchase | 0 | 0 | 1 | 0 | 0 | 0 | 7 coin armor |
| Gear equips | 6 | 10 | 13 | 0 | 0 | 0 | 14 |

Reached by no scope: the Ishalgen vendor buy, a mana potion drunk by the Priest, a patrol
wait by the Priest, a revive at the map's first spawn point, the village's Soul Healer,
and the environment-gated Mau course and Cleric encounter.
- The gate pins its environment and writes it beside each baseline: `NA_HELP_ITEMS` unset
  for every line, because help items are on for every class line (Standing rules),
  `AION_BOT_DASHBOARD_PORT` and `AION_SIM_PROCESS_KEY`. A run with `NA_HELP_ITEMS=0` is not
  a gate run.
- A passing candidate trace is deleted and only its verdict kept. A failing one is kept.
- **Re-record rule.** Baselines belong to one commit, the baseline sha. Until CP-08 records
  the first baselines there is none, and this rule does not apply. From then on, at the
  start of every iteration, run `git log <baseline sha>..HEAD`. A commit from outside this
  list is one whose message names no CP item. If such a commit touches `src`, `tests`,
  `game-server` or `parity-artifacts`, run the full gate (set `all` and every recorded class
  scope) at HEAD before taking an item. If every scope passes, log the commit and the
  verdict in the Progress log and go on; an outside commit already logged with a passing
  verdict is not checked again. If a scope fails, stop and report to the operator,
  who decides between re-recording at HEAD and reverting the outside commit. The loop does
  neither by itself, and it checks out no older commit: it makes no branch and no worktree
  and does not move HEAD. When the operator chooses to re-record, the gate is run with
  `-Record` at HEAD, both hashes are logged, and the baseline sha moves. An upstream port
  must bump `javaReference` in `e2e/natural-class-lines.json` (CP-01's test asks for it).
  That one edit to a file of this list by an outside commit is allowed, and the rule above
  still applies to the commit.
- **Full-gate failure.** When the full gate fails in CP-41 or CP-69, the item is not ticked.
  Nothing is bisected by checking out older commits, for the same reason. The comparer's
  first differing record names the step, and `git log` on the file that step runs names the
  item that last changed it. The fix is a lettered item (rule (i)) that brings the old
  behavior back, proven by the gate on the failing scope. If the first difference does not
  point at one item, the loop stops and reports.

### 9. Rules of this list

Beside the loop protocol of docs/natural-ascension-altgard.md:2394-2440. Rule (m) says which
of the two wins where they differ.

- (a) One proof per item; one gate invocation with one verdict is one proof, and so is one
  probe invocation that names several rows; unit tests an item writes as part of its work
  run in the bundle.
- (b) Bundle for every commit: the seven pre-commit checks (warning baseline, null loggers,
  clock reads, custom quest drafts, fidelity, quest-plan compiler, retail quest inventory;
  docs/natural-altgard-haramel.md:433-435); the Aion.GameServer.Tests project when the
  commit touches tests/Aion.Bots; scripts/sim/test-sim-snapshot.ps1 and
  test_compare_traces.py when it touches scripts/sim; Fast unless the commit is docs or
  evidence only; never while a run holds the build outputs. CP-41 and CP-69 run the whole
  list in CLAUDE.md.
- (c) Guard, from CP-13 on: every commit that edits tests/Aion.Bots or the journey test also
  runs gate p. The guard starts once CP-08 has recorded baseline p. CP-05, CP-06 and CP-07
  edit tests/Aion.Bots before any baseline exists and are meant to change the Priest's play,
  so the guard does not apply to them. CP-14 only adds files that nothing calls and changes
  no existing file, so it needs no guard when it is worked before that. After that gate p is
  never skipped. From the first class-scope recording on (CP-53), such a commit also runs
  every recorded class scope. There is one exemption: a commit that changes only one class's
  own profile file skips the other classes' scopes. That class's own scope is expected to
  change, so the item re-records it by rule (j). The guard is not the item's proof.
- (d) A capture item makes two commits: first the code commit with the bundle green, then
  the capture and its Verify, then one evidence-only commit. Capture refuses a dirty tree
  (scripts/sim/sim-snapshot.ps1:263-264), so the code cannot wait for the evidence. "One
  commit per item" in this document means this: one commit for an item, and code then
  evidence for a capture item. A capture item with no code change of its own makes only the
  evidence commit. An item that re-records a class scope under rule (j) also makes two
  commits: the code, then the re-recorded baseline file.
- (e) Journey items: at most two attempts. A fix inside the item is one small change (a
  profile number, a reward pick, one attempt-budget override with its reason). Anything
  larger, and any second failure, goes under Blocked and becomes a lettered item by rule
  (i), with its own single proof: the gate set of the item that introduced the code it edits
  (rule (k)) plus the recorded class scopes when it touches shared code. Rule (j) covers a
  class scope the fix is meant to change.
- (f) A finding met while refactoring is logged, not fixed.
- (g) Java is read first for any server behavior; natural play only; deaths are recorded
  outcomes; GM setup only in probes on free accounts; help items on for every class line,
  each kit written into this document as a manifest before its first use; no bandages;
  snapshots only from committed code under new names; no existing snapshot is recaptured or
  verified again; one commit per item on main (two for a capture item, rule (d)), with the
  CP id in the commit message, no trailers, no push, never a branch or a worktree; say when
  a run can be watched on the bot monitor at http://127.0.0.1:17880/.
- (h) Pick rule. Take the first unchecked item that is not listed under Blocked and whose
  Depends are all ticked. An item listed under Blocked is passed over until its entry is
  removed. If no item can be taken, stop and report.
- (i) Lettered items. A lettered item CP-NNx is written directly after its parent CP-NN,
  with its own Depends and Proof lines. It is added to the parent's Depends, and that takes
  the parent off Blocked. So the pick rule works the lettered item first and the parent
  after it. The parent is then retried with a fresh two-attempt budget. Items that depend on
  the parent need no edit: they wait for the parent, and the parent waits for the lettered
  item. No existing id changes.
- (j) Intended changes to a new class. An item that means to change how a new class plays
  names the class scopes it will change, re-records those twice in the same item (the gate
  with -Record) and logs both hashes. Priest and Cleric scopes (p, m, b, c, l1, hm and ax)
  are never re-recorded that way. CP-05, CP-06 and CP-07 change the Priest on purpose, but
  they come before the first baselines, so this rule is not bent for them.
- (k) A fix to shared code made inside a probe or journey item re-runs the gate set of the
  item that introduced that code, not only p. For example the rest executor came from CP-37,
  the chain and swing code from CP-39 and the walk-in branch from CP-40, each proven on m+c.
  That run is a guard, like rule (c)'s; the item's proof stays its own.
- (l) Captures. Every capture attempt passes its own -Run, so two attempts never share an
  evidence folder. An attempt whose run fails before the dump leaves no snapshot, and the
  next attempt keeps the name. A capture that exists but fails its item's acceptance keeps
  its name, is logged as rejected and is never deleted or overwritten. The second attempt is
  then captured as <name>-a2 (precedent: altgard-rc-l1-a2), and later items use the accepted
  name.
- (m) Where the borrowed loop protocol of docs/natural-ascension-altgard.md and this
  document differ, this document wins. The protocol's pick rule is rule (h). Its three
  pre-commit checks and its Fast run "when an item changes server code" are rule (b). Its
  "one run per OD-11" is CP-Q3. Its OD-3 is the Git standing rule. Its "full checklist for
  NA-26" is run at CP-41 and CP-69; NA-26 names no item of this list. Its "move on to
  another unblocked item" is rules (h) and (i). Its "NA-21's mechanism" is the help-item
  supply of that document and keeps its NA id; it is not CP-21. Here the SIM host supplies
  the approved help items of every class line.
- (n) No new unit tests (the operator, 2026-10-07: "stop writing tests, they are mostly a
  waste of time right now and waste of tokens. Remove any tests also unless you deem any
  one of them absolutely necessary."). This rule outranks every Proof line below that names
  a unit test: such an item is proven by the build, the bundle of rule (b) and its gate
  set, its probe or its journey. An item with none of those is proven by the build and the
  bundle, and by the first item that uses its code in a run. The existing tests are run,
  not extended. A gated SIM probe is a run on a prepared character, not a unit test, and
  stays the way to see server behavior before a journey reaches it. Kept from this plan,
  as necessary: UT/NaturalClassLineContractTests (the one check that the hand-written
  e2e/natural-class-lines.json still matches the shipped data and the handlers after an
  upstream port), and the two tool tests scripts/sim/test-sim-snapshot.ps1 and
  scripts/sim/trace/test_compare_traces.py (they check the snapshot tool and the comparer
  that every gate verdict rests on). Removed on 2026-10-07: the twelve other test files
  this plan added (the pin test, the golden gear test with e2e/natural-gear-golden.txt, and
  the tests of the profile, rest, movement, gear, restock, seam, hub bind and help kit),
  and the methods it added to seven older test files. Text below that cites one of them is
  history. CP-41's pin-test check is void.

## What a class profile supplies

- **Skills:** the catalog for the class. Numbers (level, raw range, add-weapon-range flag,
  cast time, cooldown group, MP, chain category, required chain, chain time, self count,
  weapon condition) are generated from skill_tree.xml and skill_templates.xml; the profile
  hand-writes only each skill's role and the exclusions with their reasons. The Priest and
  Cleric keep their frozen hand-typed tables.
- **Combat policy:** the ordered attack lists for an adjacent target and for a target at
  range, with opener and follow-up pairs, which skill is the pull and which the filler, and
  the policy version written into every combat-decision record. For the Priest, Cleric and
  Chanter this is an adapter over the static NaturalPriestCombatPolicy; for every other
  class it is one rule table from which both Decide and CandidateActions are built.
- **Chain discipline (table profiles):** a follow-up is cast at once after its opener and
  only within its own chain time; no non-chain cast and no second opener goes between them,
  because Java resets the chain on either (Skill.java:160-161; ChainCondition.java:61-63).
- **Sustain in a fight:** the recovery ladder with HP triggers, which may be empty (own heal
  where learned, defensive cooldown, timed potion), the emergency enter and exit
  percentages, the potion threshold, whether and when a mana potion is drunk, the mana
  reserve (only when a heal is learned) and the HP at which the shield scroll is used.
- **Retreat:** the swarm limit, the flee thresholds, the control skill cast before leaving
  or none, and the no-retreat mode for scripted fights (the ScriptedTrial flag, set today
  for the Q2008 trial and the Q2947 arena).
- **Upkeep:** a list of buffs kept up, each with when it is recast (before a pull, between
  fights, while being hit) and the effect ids that prove it is active; the scroll family for
  the shared slot (casting speed or attack speed), when the run-speed scroll is kept up, and
  the DP skill or none. The Priest's list has one entry.
- **Rest between fights:** how HP comes back (powder skill, own heal, or a life potion and
  then sitting while the potion is on its delay), the HP and MP targets, the sit bound, and
  the switch to the class's own heal once it is observed in the skill list. No profile uses
  a bandage.
- **Pull:** the style (stand-off, weapon-range stand-off or walk-in), the opening distance
  (22 m or less, the planner's cap), the named per-site distances (today's 20, 22, 23, 25
  and 30, and the 21 m standoff that NaturalCombatStandoff.Select derives as 25 - 3 - 1,
  stay separate numbers), the conservative ranged hold, and the patrol wait rule with the
  readiness record it reads.
- **Movement inside a fight:** where an approach stops (weapon reach for a walk-in class,
  the hold distance for a ranged one), what counts as adjacent, how far to close in after a
  range refusal, and what to do after an obstacle refusal (close to melee for the Priest
  line, find another sight line for a ranged class).
- **Readiness:** the named HP and MP thresholds the shared helpers ask for (ready to pull,
  rest between adds, before a named monster, before a use bar, during Return's cooldown,
  before a timed quest).
- **Auto-attack:** whether a swing is the filler between skills (Warrior, Scout) or the last
  resort (casters, Engineer); the interval and reach come from the main-hand weapon.
- **Gear:** wearable weapon groups and armor types from the class's mastery rows, the item
  restrict column equal to the class id, preferred weapon groups and one defined ranking
  stat (physical: mean weapon damage plus the flat physical-attack bonus, per swing;
  magical: magic boost), the hand layout and off-hand mode, the armor type order, one score
  for equip, keep, sell and reward choice, and the expected mastery ids.
- **Rewards and supplies:** the pick at a class-dependent reward (weapon, armor type,
  consumable order), contract pins as overrides, the protected supplies (the life potions
  and the items of the help kit; bandages are not one), the restock table (item, threshold,
  target, trade list) with a Kinah floor, and the help kit by level band, as its manifest
  lists it.
- **Not in the profile.** In the class-line contract (decided by the server): starter and
  second class ids, Q2132 var and trainer, class-selection page, SETPRO action, Q2009 var,
  reward group, preceptor and page, the reward list, the dispatch quest. Decided by the
  operator, not the server: the SIM account and character name, which are in the class line
  (the code record NaturalClassLine, not the contract file), and the ceremony pick (OD-5 for
  the Cleric, question 7 for the Chanter), which is ceremonyReward.itemId in
  e2e/natural-ascension-contract.json today and part of the bridge that ForChoice builds for
  another line.

## Standing rules

These bind every item. They are the operator's rules for the natural bot as they stand on
2026-10-07. The sources are CLAUDE.md, AGENTS.md (the bot monitor),
`docs/natural-abyss-entry.md`, `docs/natural-ascension-altgard.md` (OD-7, OD-12, OD-13 and
the loop protocol), `docs/natural-altgard-leveling.md` (the bind policy and the flight
transporters), `docs/natural-altgard-haramel.md:433-435` (the seven checks) and
`docs/e2e-player-simulation-plan.md` (D22, D23, D25). "Loot every kill" and "no subagents"
are the operator's instructions and are first written down here. Three rules come from the
operator's answers of 2026-10-07 and are quoted where they stand: help items for every
line, no bandages, and the bind at each Ishalgen quest hub.

- **Java first.** Read the Java (`../aion-server`, branch `4.8`) for every server behavior
  an item relies on. Java wins; a retail correction needs a logged decision. No server or
  data change is expected in this plan.
- **Natural play.** No GM input in a journey, apart from the approved help items, which
  every run lists in its help-items.json. They are on for every class line (see Help items
  below). In SIM the host adds them on the server
  (SimT/SimulationNaturalIshalgenJourneyTests.cs:95-102). GM setup is allowed only in probes
  on free SIM accounts, and each probe says what it prepared.
- **Deaths are outcomes, not failures.** So are retreats, lost timers and failed attempts.
  They are recorded. The operator again on 2026-10-07: "Death is not failure."
- **The death rule, for every class and level.** Whenever the bot dies it revives at the
  obelisk it is bound to and soul heals at the nearest Soul Healer. With the hub bind below,
  that is the working hub's obelisk and the Soul Healer beside it. In an instance it revives
  in the instance and heals after the next obelisk resurrection. It is a rule of the bot,
  not of a profile.
- **Inventory check after every quest turn-in:** wear better gear, open reward containers,
  discard what is to be discarded, check cube space. Question 13 decides how this applies in
  Ishalgen.
- **Gear is the profile's rule.** The Cleric's staff rule (the owned staff with the most
  magic boost) stays the Cleric's. No gear is bought at levels 1-9. Each profile item writes
  the class's weapon, armor and reward picks into its per-class note before the class's
  first run.
- **No level goals.** Levels come from quests. No hunting and no soul healing for a level.
- **Loot every kill.**
- **Hub bind and hub flight.** Bind at the working hub's obelisk everywhere, Ishalgen
  included. The operator, 2026-10-07: "We should have been binding in Ishalgen the whole
  time, at each quest hub (the village and the outpost), and soul heal as discussed." In
  Ishalgen that is two binds, each on first arrival at the hub for work, for every line, the
  Priest included: the village (Aldelle, obelisk 700063, fee 43 Kinah, Soul Healer Linevir
  203512 beside it) and the outpost (obelisk 700064, fee 134 Kinah, Soul Healer Rusalka
  203680 beside it). CP-07 builds it. Hub flight transporters are used from Altgard on. In
  Ishalgen the hub flight stays off, as on the accepted route (NI07_OPTIMIZE_HUBS is unset):
  the answer was about the bind.
- **Help items** are on for every class line and every level, the Priest's levels 1-9
  included. The operator, 2026-10-07: "help items on for everyone, and even change the
  priest defaults! Get better healing potions, the shield scroll, greater running scroll.
  Any consumable to make these more survivable and faster." The approved families at levels
  1-9 are the best healing potion the level may use, the shield scroll, the Greater Running
  Scroll, and any other consumable that makes the class more survivable or faster; the three
  event scrolls and the mana potions a starter owns are among them. Each kit is written into
  this document as a manifest before it is first used, as the coin-armor manifests were, and
  every supplied item is listed in the run's help-items.json. The Cleric's kit from level 10
  on stays as approved (OD-13). No run of this plan sets NA_HELP_ITEMS=0.
- **Recovery without a heal: potions, no bandages.** The operator, 2026-10-07: "DO not use
  bandages, just use Potions, rest when potion is on cooldown if needed". A class without a
  heal drinks a life potion when its HP is below the target and the potion is ready, in a
  fight and between fights. While the potion is on its delay it sits and rests if it needs
  more HP. Bandage Heal 245 and the Bandage item are not used, not bought and not a
  protected supply.
- **Don't change earlier runs.** The accepted Priest and Cleric journey, its contracts and
  its snapshots keep working. No existing snapshot is recaptured or verified again. One
  change is made on purpose, by the operator's decision of 2026-10-07: CP-05, CP-06 and
  CP-07 change how the accepted Priest line plays levels 1-9 (the help kit and the bind at
  each Ishalgen quest hub). In a bridge run the level-10 Cleric finishes Ishalgen with the
  same two binds and the hub revive. The Cleric's kit and every leg from Altgard on are not
  changed, and the old snapshot `munin` and every other snapshot stay as they are.
- **Snapshots** come only from committed code, under new names, and are never overwritten.
- **Git.** One commit per item on `main`. A capture item makes two, the code commit and then
  one evidence-only commit, and "one commit per item" means this (rule (d)). Only that
  item's files, the CP id in the message, no co-author or attribution trailer, never a push,
  never a branch, never a worktree, no subagents.
- **Checks.** The seven pre-commit checks before every commit; Fast before any commit that
  is not docs or evidence only; never while a run holds the build outputs. The warning
  baseline is not raised and generated data is not hand-edited.
- **SIM only, seed 1.** Every run in its own throwaway schema, nothing existing captured
  over, no multi-seed batches (CP-Q3, answered 2026-10-07). No LIVE run and nothing on the
  operator's `aion` stack. Say when a run can be watched on the bot monitor at
  http://127.0.0.1:17880/.

## Per-class notes

### Every line: the level 1-9 help kit and the Ishalgen binds

Two things are the same for every class line: the help kit at levels 1-9 and the bind at
each Ishalgen quest hub. What follows was read in the code and the data on 2026-10-07.
None of it was run.

**The help kit: the manifest.** Written by CP-05 on 2026-10-07, before any run uses it.
Levels 1-9 are one band: nothing in the data asks for a split, because every item below is
usable from level 1. A supplied row is topped up to N when fewer than M are owned, at each
stock check. An owned row is what every starter is created with. The unit test
UT/NaturalStarterHelpKitTests reads this table and requires it to agree with the allowlist
and the shipped data, so edit the table and the allowlist together.

<!-- CP-05 manifest -->
| Item | Id | Family | Levels | Source | Keep | Item level | Use skill | Delay | What it does | When it is used |
|---|---|---|---|---|---|---|---|---|---|---|
| Major Life Potion | 162000006 | life-potion | 1-9 | supplied | 30 when below 10 | 50 | 9893 at 1 | group 11, 30000 ms | 1,694 HP: 154 at once, then 154 every 2 s for 20 s | Used: drunk by the class's potion rule, in a fight and between fights, ahead of the Minor Life Potion. |
| Lesser Anti-Shock Scroll | 164000067 | anti-shock | 1-9 | supplied | 30 when below 8 | 20 | 9953 at 1 | group 32, 60000 ms | a shield that absorbs 158 damage for 24 s | Used: in a fight at 50% HP or below, when no shield is up. |
| Greater Running Scroll | 164000076 | running | 1-9 | supplied | 20 when below 5 | 30 | 9960 at 3 | group 35, 15000 ms | +30% run speed for 5 min | Used: before a leg of 150 m or more, or a leg to another map. |
| [Event] Rx: Accelerox | 164002116 | running | 1-9 | owned 50 | - | 30 | 10465 at 3 | group 35, 1000 ms | +30% run speed for 30 min | Used: as the Running scroll when no Greater Running Scroll is owned. |
| [Event] Rx: Castafodin | 164002118 | awakening | 1-9 | owned 50 | - | 30 | 10467 at 3 | group 34, 1000 ms | +9% casting speed for 30 min | Used: kept up by a class whose profile names casting speed for the shared speed slot. |
| [Event] Rx: Blitzopan | 164002117 | courage | 1-9 | owned 50 | - | 30 | 10466 at 3 | group 34, 1000 ms | 9% faster attacks for 30 min | Used: kept up by a class whose profile names attack speed (the Warrior and the Scout). |
| Minor Mana Potion | 162000007 | mana-potion | 1-9 | owned 100 | - | 10 | 9894 at 1 | group 11, 30000 ms | 649 MP: 59 at once, then 59 every 2 s for 20 s | Used: in a fight when MP is below the main attack's cost and HP is above the life-potion threshold (CP-Q12). |
| Minor Life Potion | 162000002 | life-potion | 1-9 | owned 100 | - | 10 | 9889 at 1 | group 11, 30000 ms | 407 HP: 37 at once, then 37 every 2 s for 20 s | Used: only when no Major Life Potion is owned; it shares the Major's delay. |
| Mercenary's Fruit Juice | 160000001 | fruit-juice | 1-9 | owned 12 | - | 1 | 10034 at 1 | group 21, 5000 ms | +2 natural HP healing for 15 min | Left out: 2 HP of natural healing changes nothing that a potion does not. |
| [Event] Lodas Amulet III | 169620005 | xp-amulet | 1-9 | owned 2 | - | 1 | 10249 at 1 | group 71, 14400000 ms | +20% XP for 2 h | Left out: an XP boost, and levels come from quests (no level goals). |
| Administrator's Boon 3-Day Pass | 164002039 | boon | 1-9 | owned 1 | - | 1 | 10350 at 1 | group 31, 15000 ms | no death penalty for 1 h | Left out: the death rule's soul heal is the operator's stated rule. |
| Bandage | 169300002 | bandage | 1-9 | owned 20 | - | 1 | none | none | the reagent of Bandage Heal 245 | Left out: the operator ruled bandages out on 2026-10-07 (CP-Q11). |

Why these three are supplied. The operator named them: "Get better healing potions, the
shield scroll, greater running scroll."

- **The potion** is the life potion with the largest heal a level 1 character may use: 30
  heal-over-time potions of delay group 11 carry no level requirement, and 1,694 HP is the
  most any of them heals. The next tier, the Fine Life Potion, needs level 50.
- **The shield scroll** is the tier OD-13 approved for levels 10-19, so the stock carries
  on into that band.
- **The Running scroll** is the one the operator named.

What the data and the code said when this was read, before CP-06:

- A consumable is gated by its `restrict` attribute, not by its item level. Java refuses an
  item by class and by required level, both read from the restrict row
  (JAVA/src/com/aionemu/gameserver/restrictions/PlayerRestrictions.java:327-336; C# twin
  src/Aion.GameServer/Restrictions/PlayerRestrictions.cs:389-395). With no restrict
  attribute the row is 1 for all seventeen classes
  (JAVA/src/com/aionemu/gameserver/model/templates/item/ItemTemplate.java:34, 85). AX-01
  found the same for scroll 164000079 (docs/natural-abyss-entry.md:343-345).
- **Healing potion.** A life potion heals at once and then every 2 s for 20 s, with a 30 s
  delay on group 11. In all: Minor 162000002 (the 100 a starter owns) 407 HP, Lesser
  162000003 737, Life Potion 162000004 1,067, Greater 162000005 1,397 and Major 162000006
  1,694. None of the five has a restrict (item_templates.xml:830724-830753). Fine 162000075
  needs level 50. So the best one a level 1-9 character may use is the Major Life Potion.
  The bot knows the tiers up to 162000004 only (Sc/NaturalIshalgenPotionPolicy.cs:8-20,
  33-41).
- **Shield scroll.** It is the Anti-Shock scroll: a 24 s shield, a 60 s delay, group 32.
  Lesser 164000067 absorbs 158, plain 164000068 245, Greater 164000069 338 and Major
  164000070 425. None of the four has a restrict (item_templates.xml:832739-832762). Fine
  164000131 needs level 50.
- **Greater Running Scroll** 164000076 uses skill 9960 at level 3: +30% run speed for 5
  min, a 15 s delay, group 35, no restrict (item_templates.xml:832793-832798).
- **What a starter owns.** Every starter has 100 Minor Life Potions, 100 Minor Mana Potions
  162000007, 20 Bandages, and 50 each of the event scrolls Accelerox 164002116 (run speed),
  Blitzopan 164002117 (attack speed) and Castafodin 164002118 (casting speed)
  (player_initial_data.xml:5-22 for the Warrior; the other five lists hold the same). It
  also has 12 Mercenary's Fruit Juice 160000001 (+2 natural HP healing for 15 min), 2 Lodas
  Amulet III 169620005 (+20% XP for 2 h) and one Administrator's Boon pass 164002039 (no
  death penalty for an hour).
- **The event-scroll finding, settled by CP-05: the data read is right and the catalog is
  wrong.** The three event scrolls use their skills at level 3
  (item_templates.xml:835126-835143). An item casts its skill at the level its skilluse
  action names (Java SkillUseAction.canAct), and a stat change is the value plus the delta
  times the skill level (Java BufEffect.getModifiers, line 74; C#
  src/Aion.GameServer/SkillEngine/Effect/BufEffect.cs:58). So Accelerox is +30% run speed,
  the Greater Running Scroll's percent, Castafodin +9% casting speed, the Greater Awakening
  Scroll's, and Blitzopan 9% faster attacks, the Greater Courage Scroll's. Each lasts 30
  min with a 1 s delay, where the tiered scrolls last 5 min with 15 s.
  UT/NaturalStarterHelpKitTests computes this from the item's skill level and the skill's
  delta. The bot's catalog and Appendix D.2 file them as the Lesser tier, +10% and +3%
  (Sc/NaturalHelpItemPolicy.cs:69-71; docs/natural-ascension-altgard.md:2832-2836). Neither
  is edited here: the catalog's tier decides what the Cleric picks from level 10 on, which
  this plan does not change. What it costs the Cleric is in the "Not blocking" list.
- **Where help items are gated today.**
  - Supply: TopUpHelpItemsAsync (J:1501-1526) returns unless the character is a Cleric
    (J:1505). The bridge runner and the later legs call it, first at J:1047 and J:1060. The
    Ishalgen decision loop (from J:724) never calls it.
  - The supply plan and the refusal of an unapproved item both read one table,
    NaturalHelpItemAllowlist.Approved (Sc/NaturalHelpItemAllowlist.cs:20-38, 78-84,
    93-113). Every band in it starts at level 10 or later.
  - Use: the shield scroll and the mana potion are offered to the Cleric only (J:9130,
    9132), and the scroll part of BuffOurselfAsync returns for any other class (J:9596).
  - Tier rules: the help policy picks a scroll only when its tier is at or below the
    character's level, or level + 10 for the shield (Sc/NaturalHelpItemPolicy.cs:85, 102,
    136, 160-163). The event scrolls count as tier 10 and the Lesser Anti-Shock as tier 20,
    so below level 10 it picks nothing.
  - The Running scroll is used only at the TravelLeg trigger, before a leg of 150 m or to
    another map (Sc/NaturalHelpItemPolicy.cs:45, 101). Only two bridge sites send that
    trigger (J:1114, 4863).
  - The life potion has no class gate: the fight loop offers any owned one to the policy
    (J:9126; Sc/NaturalIshalgenPotionPolicy.cs:33-41).
  - The SIM host supplies an item only after NaturalHelpItemSupply.RequireApproved accepts
    it (SimT/SimulationNaturalIshalgenJourneyTests.cs:71-72, 95-102). Three test files pin
    the list and the policy: UT/NaturalHelpItemAllowlistTests.cs,
    UT/NaturalHelpItemPolicyTests.cs and UT/NaturalHelpItemSupplyTests.cs.
  - Since CP-05 the level 1-9 rows are NaturalHelpItemAllowlist.Starter, and the supply
    plan and RequireApproved read them with Approved (AllLevels). Two things follow for
    CP-06. First, the Cleric's keep list reads Approved alone
    (Sc/NaturalIshalgenInventoryPolicy.cs:133), so the Major Life Potion is not in it: a
    level-10 Cleric's shop stop would sell what is left of the level 1-9 potions unless
    CP-06 adds the Starter rows there. Second, a Cleric is level 9 between the class choice
    and the ceremony; the supply gate lets a Cleric through, so a stock check in that
    window now supplies the level 1-9 kit. No stock check falls in it on the present
    scopes: the level has not changed since the Priest's last check.
- **Defaults for the manifest**, where the operator's words leave room. Say so to change
  one. The shield scroll is the Lesser Anti-Shock 164000067, the tier OD-13 approved for
  levels 10-19, so the stock carries on into that band; up to the Major is usable. Stock
  numbers follow OD-13's for the same families: potions to 30 when below 10, shield scrolls
  to 30 when below 8, running scrolls to 20 when below 5. Use follows the Cleric's rules:
  the shield scroll in a fight at 50% HP or below, the Running scroll before a leg of 150 m
  or more or to another map (Sc/NaturalHelpItemPolicy.cs:45-46). The scroll for the shared
  speed slot is the owned event scroll: Castafodin for a class that casts, Blitzopan for
  the Warrior and the Scout; the profile names it. Three owned consumables are left out: the
  fruit juice (2 HP of natural healing changes nothing a potion does not), the Lodas Amulet
  (an XP boost; levels come from quests) and the Administrator's Boon (it removes the death
  penalty, and the death rule's soul heal is the operator's stated rule).

**The binds.**

- Java: the obelisk asks the player to confirm and shows the price
  (JAVA/data/handlers/ai/ResurrectAI.java:76, 106). On yes it needs the price in Kinah and
  the player within 5 m (lines 83-89), stores the player's own position as the bind point
  (91-96) and takes the price (97). It refuses a second bind within 20 m of the present
  bind point (52-57). The price is the bind point's `price` attribute as it stands
  (JAVA/src/com/aionemu/gameserver/model/templates/BindPointTemplate.java:21-34). The C#
  twin is src/Aion.GameServer/Handlers/AI/ResurrectAI.cs.
- Fees (bind_points/bind_points.xml:22-23): 43 Kinah at obelisk 700063, Aldelle Village,
  and 134 Kinah at obelisk 700064, which that file calls Anturoon Crossing. That is 177
  Kinah in all, from a purse that starts at 1,000.
- Spawns (spawns/Npcs/220010000_Ishalgen.xml): obelisk 700063 at (587.7, 2467.1, 278.8)
  (lines 1248-1250) with Soul Healer Linevir 203512 2.3 m from it (1167-1169); obelisk
  700064 at (936.9, 1704.7, 259.5) (1252-1254) with Soul Healer Rusalka 203680 2.5 m from
  it (1283-1285). Both healers carry title 350412 (npc_templates.xml:6701, 8989), which is
  how the death rule finds a Soul Healer (Sc/NaturalServiceSteps.cs:40-52). Both obelisks
  run the resurrect AI (npc_templates.xml:439832, 439837).
- The bot's hub table (Sc/NaturalIshalgenHubPolicy.cs:35-49) calls the village `aldelle`,
  centre (575, 2440). The outpost obelisk stands 6 m from the centre of the hub it calls
  `mijou` (940, 1700). The hub `anturoon` (930, 1565) is 140 m from that obelisk and has
  none of its own.
- What exists: BindAtAldelleIfNeededAsync (J:4877-4893). It does nothing when the bot is
  already bound within 20 m of the Aldelle obelisk, or when the purse does not cover the
  fee. Otherwise it walks to 700063 and binds with NaturalServiceSteps.BindAsync, the step
  the Altgard legs use. Its one caller is AcceptAllAtCurrentHubAsync (J:7824-7828), called
  at J:756 and J:813, both only when options.OptimizeHubs is set. That option is
  NI07_OPTIMIZE_HUBS=1 (SimT/SimulationNaturalIshalgenJourneyTests.cs:78), and the runners
  clear the variable (scripts/sim/sim-snapshot.ps1:91). So the accepted route has never
  bound in Ishalgen. Nothing binds at 700064, and the navigation graph's NPC list holds
  700063 but not 700064 (J:311-320).
- After a bind revive the death rule soul heals at the nearest Soul Healer within 30 m of
  the revive point (J:8901-8933). With no bind, Java revives a player at the race's first
  spawn point
  (JAVA/src/com/aionemu/gameserver/services/teleport/TeleportService.java:362-383), which
  for an Asmodian is (571, 2787) in Ishalgen (player_initial_data.xml:3). Ishalgen has two
  Soul Healers, the two above, and neither is within 30 m of that point. So by this reading
  the accepted route's deaths in Ishalgen have gone without a soul heal.
- One branch becomes reachable with a bind in Ishalgen: Q2129's reward claim casts Return to
  the bind when the bot is bound on the Ishalgen map and the travel policy prefers it
  (J:7990-8004). It is not under the hub optimizer, and it has never run on the default
  route.
- The Ishalgen campaign's routing after a revive or a Return is written for the map's first
  spawn point (571, 2787), and a bind at the outpost changes where both land.
  WalkEasternRoadToDerotAsync (J:6161-6198) tests for that point at J:6165 and otherwise
  walks to Nobekk 203519 by Aldelle first. It is called after every revive during outpost
  work: J:6660-6673 (Q2003), RecoverMijouAfterReviveAsync J:6946-6952 (Q2005), J:7062-7064,
  7168-7170, 7204-7206 and 7307-7309; J:7512 and 7523 test "within 400 m of the spawn
  point". Four Return fallbacks already run on the default route and change their landing
  point: J:6941 (Q2005), 7451 and 7476 (Q2006) and 7566 (Q2007), with the stranded Return at
  J:5197. UseLearnedReturnToBindAsync also requires the cast to move the bot more than 30 m
  (J:6304-6305), which can throw when a fallback fires beside the bound obelisk. CP-07
  changes these sites; they are listed there. A death before the first village bind still
  revives at the spawn point, with no Soul Healer within 30 m.

### PRIEST and CLERIC (the accepted line, priest-cleric)

They stay on their present code, with one change made on purpose before the baselines: at
levels 1-9 the Priest gets and uses the help kit (CP-05, CP-06) and binds at both Ishalgen
hubs (CP-07). The profile is an adapter over the static NaturalPriestCombatPolicy and the
RestAsync rule, and their skill tables stay hand-typed and frozen
(Sc/NaturalPriestCombatPolicy.cs:30-40; Sc/NaturalClericSkills.cs:24-94). Numbers that must
not move after that, pinned by CP-11 before any code moves: heal at 55/70%, emergency 35 to
45%, retreat at three attackers, pull at 22 m, rest by Healing Light below 90% HP, sit below
50% MP until 80%, at most 12 quiet sits, restock at 5 potions up to 12, the staff rule.
Server facts: trainer Kirhen 203530 with Q2132 var 4, class page 4080, SETPRO14, preceptor
Lyfjaberga 204083 with Q2009 var 40, Karmic Staff 101500498, dispatch Q2904. Every leg gate
keeps requiring the Cleric. Help items are on, as for every line; the Cleric's kit from
level 10 on stays as OD-13 approved it. Known quirks are logged, not fixed: the combat
objects rebuilt at J:2382 and 4550 lose MaintainInventoryAsync (assigned only at J:442), the
swing counter overflows at turn 256 (J:9279), robe and leather tie on the gear score.

Gear, since CP-29a (the operator's rules of 2026-10-07): both use the table rules of every
class, and the paragraph above no longer holds for gear. Priest: a mace, ranked by magic
boost; leather, then robe, then clothes. Cleric: a staff before a mace, each ranked by magic
boost, so the staff rule is the table's weapon order; chain, then leather, robe, clothes.
Armor is ranked by item level first and by type second (CP-Q24). The Priest's picks: the
maces at Q2100, Q2002 and Q2134 (100100024, 100100493, 100100025) and the leather piece at
Q2001, Q2005, Q2006, Q2007 and Q2129 (114300804, 113300791, 114300805, 110301182,
111300768). The level-10 Cleric of a bridge run takes the chain piece at Q2005, Q2006,
Q2007 and Q2129 (113500762, 114500767, 110501156, 111500751). Q2117 and Q2124 keep reward
1. The ceremony pick stays the Karmic Staff (OD-5). Both keep the mana potions; the Cleric also keeps what
is left of the level 1-9 kit and every help item. The robe and leather tie is gone.

### CHANTER (second class of PRIEST, line priest-chanter)

Server delta from the Cleric, checked in Java: SETPRO13 (_2008Ascension.java:153-154), class
id 11 and the list name chanter_selectable_reward; same class page 4080, same preceptor,
same Q2904. It reuses the Priest line's whole run and identity and differs first at the
class-choice send. Its profile is the Priest adapter over the Priest catalog with the Priest
rest plan; every level-10 Chanter active is excluded with a reason. Since CP-29a its gear
table is the Cleric's: a staff before a mace by magic boost, chain first (CP-Q7, answered).
The two ceremony weapons rank differently by reading: Karmic Staff 58-88 at 2.0 s hits harder per swing; Karmic
Warhammer 44-66 with +7 physical attack at 1.5 s does more per second and leaves the shield
hand free (question 7). Help items are on, as for every line. Its levels 1-9 are the
Priest's, with the level 1-9 kit. Five help-item bands start at level 10, and the allowlist
is keyed by item and level, not class (Sc/NaturalHelpItemAllowlist.cs:22-37). So once the
supply gate takes the line's classes, the level-up to 10 at the ceremony tops the Chanter up
with the same level-10 bands as the Cleric, and the run lists them in help-items.json. It
has no use for them before its stop; a Chanter kit of its own belongs to a later Chanter
leg. It stops at the capital-start checkpoint (J:1400-1405); its snapshot restores with
PC_CAPITAL=start, which re-checks the endpoint and stops (read in the code at J:408-410 and
1396-1405, not run). It has no legs after the ceremony until the Altgard contracts are made
class-aware.

### WARRIOR

Starter id 0; trainer Minu 203527 with Q2132 var 1; class page 3057; Gladiator (SETPRO7) and
Templar (SETPRO8). Kit: Training Sword 100000094 (16-20, 1.4 s, 1.5 m) and chain; no heal
and no ranged skill at 1-9. Its strikes add weapon range to a template range of 1
(skill_templates.xml:48244), so they reach 2.5 m with the sword, inside the 3 m the bot
calls adjacent today. Chain rule read in Java: Ferocious Strike 2864 opens W_CHAINA_1TH_1;
Robust Blow 2877 and Rage 2903 both name it as their required step with time 3000; Rage may
also follow Robust Blow inside Robust Blow's 3 s, because Java accepts a match on the
previous chain skill (ChainCondition.java:40-46); Body Smash 2890 is another opener and
resets the chain; any non-chain cast resets it too. At Q2100 the defined stat prefers
Raider's Mace (20-30 at 1.5 s) to Raider's Sword (20-26 at 1.4 s), but since CP-29a a
table's weapon groups are an order and the Warrior's is sword, then mace: it takes Raider's
Sword 100000107, and the swords at Q2002 and Q2134 (100000639, 100000108). Q2100 is also the
only Ishalgen quest that offers a shield. The Warrior takes the weapon there, not the shield
(the operator, 2026-10-07: "Warrior does take weapon"). It recovers by life potion and by
sitting while the potion is on its delay; it uses no bandage. It is the first class to rest
and to fight in a probe (CP-42, CP-43) and to walk in during a journey (CP-44), and the
second to reach Munin.

### SCOUT

Starter id 3; trainer Wiokan 203528 with var 2; class page 3398; Assassin (SETPRO9) and
Ranger (SETPRO10). Kit: Training Dagger 100200112 (15-17, 1.2 s) and leather; no heal. Swift
Edge 3182 opens SRA_CHAINA_1TH and Soul Slash 3223 follows it within 3 s. Devotion and
Focused Evasion have no chain, so casting either between the two loses the follow-up;
Devotion goes before the opener. Excluded with reasons: Surprise Attack 3196 (opens its own
chain SRA_CHAINN_1TH, costs 13 MP, and its back damage needs a position the bot does not
take), Counterattack 3209 (the bot does not see its own dodge) and Stealth 3222 (not usable
in combat). It holds one dagger until CP-68 and the operator's answer. It journeys last and
reuses the Warrior's melee fixes.

### MAGE

Starter id 6; trainer Jurwen 203529 with var 3; class page 3739; Sorcerer (SETPRO11) and
Spirit Master (SETPRO12). Kit: Training Spellbook 100600034 and robe; the frailest starter
and no heal. All six targeted actives reach 25 m (Stone Skin is self-cast); it pulls at 22 m
so the planner's cap stays. Flame Bolt 1282 (opener M_CHAINA_1TH_1) then Blaze 1403 (time
3000); Ice Chain 1363 then Frozen Shock 1226. Erosion, Root and Stone Skin have no chain, so
none may be cast between an opener and its follow-up. Root 1328 has resistchance 10: every
hit on the rooted monster removes it unless a 0-100 roll is below 10
(RootEffect.java:47-53), so it is for a second attacker or a retreat only. Stone Skin 1155
is upkeep (a 300 s shield for 130 MP). The 100 starter life potions have a 30 s delay, and
so has every life potion the kit may supply; the vendor's Minor Life Elixir has 60 s on the
same group (item_templates.xml:830724-830728, 831033-831037). Life and mana potions share
that group, so the life potion goes first. It is the first to journey to Munin. The
Warrior's short run to Q2132 (CP-44) is listed before it, but the Mage does not wait for
it: if CP-44 is blocked, the Mage journeys first.

### ENGINEER

Starter id 12; trainer Wild Wilhelm 801218 with var 5; class page 3569; Gunner (SETPRO15)
and Rider (SETPRO17), which are not contiguous. Kit: Pistol for Training 101800181 (magical
fire 20-23, 1.8 s, 20 m, magic boost 20) and leather; no heal. Its attack skills add weapon
range and have no template range (skill_templates.xml:29975, 35193), so they reach the
pistol's 20 m and the profile pulls at 18 m. Gunshot 1957 is an opener with time 2000;
Rapidfire 2142 has time 2000 and selfcount 2, so it may be cast twice, each within 2 s of
the step before it. Direct Shot has no chain and resets one, so it is never cast between
them. Pistols are ranked by magic boost. Its trainer, class page, preceptor 801220 and
dispatch Q29070 have never been run by a bot. RIDER is missing from class_permitted on 178
of the 197 quests that list GUNNER (for example Q2011 and Q2012); the other 19, Q29070 among
them, list both. That is recorded, not touched.

### ARTIST

Starter id 15; trainer Sona 801219 with var 6; class page 3910; only Bard (SETPRO16). Kit:
Harp for Training 102000194 (25 m) and robe. Every active skill needs a harp. Pulse 4408 is
pull and filler at 22 m; Soothing Melody 4339 heals from level 5 (it is itself a chain
opener, A_CHAINB_1TH; skill_templates.xml:73647), so recovery switches from the potion and
the sit to its own heal when the skill is observed. Fiery Descant 4300 is a charge skill and
is excluded. No follow-up is learned at 1-9, so chain resets cost it nothing. It is the
closest to the Priest's survival model. Its trainer, class page, preceptor 801221 and
dispatch Q29071 have never been run by a bot.

## Hazards

1. (Answered for p and c by CP-08 on 2026-10-07: both repeat at HEAD, record for record.)
   Trace identity at HEAD is unverified: the four record-identical pairs come from logs
   dated 2026-09-28 to 2026-10-06. CP-08 and CP-09 record every scope twice to find out. If
   p or c does not repeat, the loop stops; a decision projection is a weaker proof and needs
   the operator's notice.
2. (Did not happen: CP-09 kept all seven scopes on 2026-10-07.) A baseline scope may no
   longer pass on current code; b (the bridge route) and hm (Haramel
   from altgard-coingear, the pre-RC line) are the oldest. CP-09 drops such a scope instead
   of fixing it, which narrows what the full gate covers.
3. Some code is reached by no scope: the Ishalgen vendor buy, the Cleric's in-fight mana
   potion, and the environment-gated Mau course and Cleric encounter, which use the same
   combat path and are in neither Fast nor any gate. These rest on unit tests, the pin test
   and the golden gear test only. The fight-at-the-target block of PullAndKillAsync is
   reached only by scope hm (six at-target records in the hm07 capture trace); if CP-09
   drops hm, no scope reaches it. After CP-06 the Priest may drink a mana potion in scope m;
   CP-09's counts say whether it does (CP-02's counts mode also counts potions drunk, shield
   and speed scrolls used, help items supplied, binds and soul heals).
4. (Replaced by CP-09 on 2026-10-07: the table "What each scope reaches" holds the counts
   of the baselines, and no Proof line needed an edit.) The coverage statements in this
   plan come from a reviewer's counts in older traces (for
   example 3 deaths and 5 retreats in the existing scope-m trace, and vendor buys only in
   the bridge trace, at the Altgard shop stop, and in the Abyss-entry trace). CP-09 replaces
   them with counts from the new baselines. Those older traces were played without the
   level 1-9 kit. With a strong potion and a shield scroll the Priest may die, retreat and
   rest much less in scopes p, m and b, so a scope may no longer run the code an item names
   it for. CP-09 checks every later item's scope against the new counts and edits the Proof
   line where it must.
5. Baselines live under run/, which is git-ignored, and belong to one commit. If they are
   lost, they are restored from the second copy beside the repository
   (../BeyondAionSharp-cp-baseline). If main changes from outside this list, the re-record
   rule of section 8 applies. Never prune run/cp/baseline or the second copy.
6. Most line numbers come from the readers' map at commit b45b72a43; the ones this plan
   leans on were rechecked. They shift with every edit. CP-05, CP-06 and CP-07 edit the
   journey and the help-item files before any refactor item, so every J line quoted by a
   later item is already a little off when that item starts. After CP-13, J:8496-10115
   lives in two new files and the statics that follow (today J:10117-10332) move up by
   about 1,620 lines. So each item finds its sites by name.
7. The LIVE path is covered by none of the SIM proofs. tools/Aion.LiveBots builds with
   warnings as errors and calls the Priest policy and Classify directly, so every public
   member it uses keeps its signature, and the session interface gains no member. CP-06 and
   CP-07 edit journey code the LIVE Priest runs too, so its levels 1-9 change with them; no
   LIVE run is made in this plan, and the first LIVE run after it is where that is seen.
8. Findings met while refactoring must not be fixed there: the rebuilt combat objects lose
   MaintainInventoryAsync and their death history, the Priest's swing counter overflows at
   turn 256, map 320010000 lists Hagen 205020. Fixing any of them changes the trace; they
   are logged and decided after CP-45.
9. Forty snapshots resume into current code. Receipt, checkpoint and snapshot.json formats
   may gain optional fields only; a renamed or removed field breaks restores of accepted
   endpoints.
10. Whether a class without a heal survives Hatata (1,821 HP), the Q2007 generators and the
    Q2005 stalkers is unknown. In a fight it has one life potion every 30 s and the shield
    scroll every 60 s, and nothing else; between fights it sits while the potion is on its
    delay. The help kit keeps a potion with the 30 s delay in stock, so the vendor elixir
    and its 60 s delay matter only if the supply fails. The attempt budgets were sized on
    Priest runs; deaths are outcomes, but an exhausted budget fails a run.
11. A walk-in fight happens at the target's position, inside its neighbours' assist range,
    so melee classes will take more adds than the Priest's 22 m stand-off. The walk-in is
    first played in CP-44. The quest order comes from the decision engine and the frozen
    contract, and it was tuned on the Priest. The hub order and safe work groups of
    NaturalIshalgenHubPolicy are not used on this route (they run only with
    NI07_OPTIMIZE_HUBS set), so there is no hub order to override. CP-07 takes only the
    two binds from that code and leaves the rest of it off.
12. Ishalgen code written for a ranged puller (the Sprigg stand-off, Q2005's firing-edge
    search, fight-through) may need per-quest work for Warrior and Scout. The plan handles
    it by lettered items (rule (i)), so the list can grow past 70.
13. Auto-attack is weakly observed: SM_ATTACK_RESPONSE is not decoded, so a refused swing is
    silent. Swings with a spellbook, pistol or harp at weapon reach have never been sent by
    the natural bot.
14. The Java chain rule was read, not played: no bot has cast a Warrior, Scout, Mage or
    Engineer follow-up after its own opener. The skill sweep casts each follow-up with the
    chain state set by the director (SimT/SimulationSkillSweepTests.cs:229-233). A wrongly
    sequenced follow-up is refused without a message and shows as a cast-start timeout; the
    leveled probe rows are where that is found. A potion is an item skill (SkillMethod.ITEM)
    and does not reset a chain (Skill.java:131, 160-161); a cast with no chain category
    does.
15. Walking routes on the bot's navigation data to the five other trainers were not checked,
    and a template id missing from the graph list may make the approach helper throw. The
    CP-34 probe checks the handler and dialog, not the walk.
16. The Engineer and Artist trainers, class pages, preceptors, weapon rewards and dispatch
    quests have never been played by any bot. Their starter weapons have only cast skills:
    scenario C11 casts Direct Shot 2219 and Pulse 4408 with the starter pistol and harp
    (SimT/SimulationCombatScenarioTests.cs:479-490) and the skill sweep equips a gun or a
    harp for its rows; the natural bot has never swung one (hazard 13). Which SETPRO buttons
    the 4.8 client's class pages show was not checked when this was written; CP-01 checked
    it on 2026-10-07, and each page shows exactly its own starter's second classes (see its
    evidence line). The handler does not check the
    action against the page. But ClassChangeService.setClass refuses a second class outside
    the starter's own two (ClassChangeService.java:60-70), so a wrong button fails; it
    cannot give a foreign class. A server defect found there is fixed Java-first under its
    own lettered item.
17. The 26 Ishalgen plans are outside the plan-drift test and one reader found their NPC
    anchors stale against current spawn data. They are not regenerated (the Priest baseline
    would move), so new classes inherit the same anchors.
18. (Answered by CP-10 on 2026-10-07: the stop at 2132:5:0 fired on the Priest and wrote
    resume-receipt.json.) Stop boundaries with status 5 have never been used; the only boundary in the files is
    2007:3:6. The code says they fire (J:553, 601; QuestService.cs:86-87). CP-10 plays one
    on the Priest before any class run leans on it, and it is found and fixed there if it
    does not fire. The fallback is the new -StopAfterQuest switch. It writes no
    resume-receipt.json (J:854-861; only the NI08_STOP_AT stop at J:875-891 writes one). It
    exists only for Q2004 to Q2007. And it has its own shorter deadline, sized on the
    Priest: 6 minutes for Q2004, 8 for Q2005, 16 for Q2006 and 20 for Q2007, against 45
    without it (SimT/SimulationNaturalIshalgenJourneyTests.cs:51-52). So on the fallback the
    proof is the passing run and the trace, not a receipt, and the Q2132 stops of CP-10 and
    CP-44 have no fallback.
19. The Chanter snapshot's Verify depends on a path read in the code, not run: on a resumed
    level-10 character in Pandaemonium EarlyAscensionNeeded is true and stage start stops
    again. If it does not, the proof of CP-33 falls back to the capture receipt and a
    Restore-only check, and the doc must say so.
20. No SIM account id is known to be free. Ids 77-82 belong to the gear scenarios
    (SimT/SimulationGearScenarioTests.cs:34) and 91-94 to the geo displacement scenarios
    (SimT/SimulationGeoDisplacementScenarioTests.cs:30-33); an earlier grep missed them,
    because the ids are picked by a ternary chain and by account + 1, not passed as one
    plain literal. 101-112 belong to the character-lifecycle test, 151-200 are reserved by
    D32, and ids above 255 cannot log in. CP-31 either finds free ids by reading every
    session construction, computed ids included, or adds new ids to the fixture's list
    (SimT/SimulationWorldFixture.cs:204-206).
21. Proof runs, the bundle and Fast cannot overlap, and other processes on this machine use
    the same build outputs. The gate builds once and runs with --no-build; a full gate holds
    the outputs for about half an hour.
22. Run volume is real: logged trace sizes are about 12 MB for p, 55 MB for the 41-quest
    scope and 61 MB for leg 4. The gate deletes passing candidate traces; failed ones and
    all class-run evidence accumulate under run/cp.
23. The default endpoint plays all 41 quests at level 9 under the non-Daeva XP cap, the
    order OD-16 replaced. The Munin snapshots are play-test evidence; a second-class leg
    that started from one would start short of XP.
24. Elixirs, the two Ishalgen bind fees (43 and 134 Kinah, 177 in all) and soul healing
    after deaths draw on one purse that starts at 1,000 Kinah. Help items cost nothing: the
    host supplies them. No bandage is bought. The Kinah floor in question 11 is a starting
    number, not a measured budget, and it is for purchases: a bind is paid whenever the
    purse covers its fee.
25. (CP-11, 2026-10-07: every pending row has its owner, and the test also fails when a
    pending literal leaves the source before its owner has turned the row on.) Pending rows in the pin test could be forgotten. So CP-11 writes every pending row with
    the id of the item that turns it on, CP-41 makes the test fail while any row is pending,
    and no item may edit an expected value.
26. CP-06 and CP-07 change the accepted Priest with no trace to compare against: the first
    baselines are recorded after them, on purpose. So no gate can show that only levels 1-9
    changed. What stands in for it: the three existing help-item test files stay green with
    only the two level-9 expectations changed that CP-05 and CP-06 name (every assertion
    about the Cleric's bands and tier rules from level 10 on stays), each of
    the two items is proven by its own contained run of scope m, and scopes c, l1, hm and ax
    start from snapshots that hold none of the level 1-9 kit.
27. What is left of the level 1-9 kit at level 10 is owned stock. The bands stop supplying
it. The leftover potions and shield scrolls are used up; the leftover Greater Running
Scrolls stay unused until level 30 under the unchanged tier rule, and the running family
steps down at level 10 (nothing supplied at 10-19). That is how the supply rule treats an
old tier, as the supply rule says of an old tier
    (Sc/NaturalHelpItemAllowlist.cs:66-69). In a fresh run through the bridge (scopes p and
    b) the new Cleric still drinks what the Priest left. The Cleric snapshots hold none, so
    the legs replayed from them do not see it. The kit also steps down at level 10: by the
    reading of the per-class note the level 1-9 potion is the Major Life Potion (1,694 HP),
    and the Cleric's level 10-19 band supplies the Minor (407 HP). CP-05 records that for
    the operator and does not change the band.
28. (Settled by CP-05 on 2026-10-07: the data read is right. See the "Every line" note and
    the "Not blocking" list.) The event-scroll finding of the per-class notes was open: the
    data read says Accelerox is +30% run speed and the bot's catalog says +10%. If the data read is right, the
    Cleric's level 20-29 bands supply scrolls weaker than the event scrolls it owns. CP-05
    settles the reading with a unit test and records the result for the operator. It does
    not change the Cleric's bands.
29. A bind in Ishalgen moves every bind revive from the first spawn point of the map to the
    working hub, which changes the walk after each death, and it makes Q2129's Return branch
    reachable for the first time on the default route (J:7990-8004). Both are meant, and
    both need the routing edits that CP-07 lists: without them a revive at the outpost walks
    back to Aldelle and out again. The
    soul heal after a hub revive has not been run in Ishalgen: Soul Healers 203512 and
    203680 are not in the navigation graph's NPC list, and a template missing from that
    list may make the approach helper throw (hazard 15). CP-07 finds out. (It found out
    on 2026-10-07: the revive at the outpost and the soul heal at Rusalka 203680 ran in
    both attempts, with no entry in the list. The village's Soul Healer was not used: no
    death fell while the bind was there.)
30. The kill loop can spend its whole budget on monsters that are walking home (CP-07,
    attempt 1): after a retreat from three attackers in Q2128, six attempts ended within
    30 s, each one to three seconds after its pull, with the target reported as returned.
    The run then failed with "not killed in 6 non-retreat attempts". It is logged, not
    fixed. A same-seed replay repeats it or not as a whole, so it does not threaten a
    baseline that was recorded; it can fail a first run of a changed route.

## CP checklist

One item per loop iteration. Take the first unchecked item that is not listed under Blocked
and whose Depends are all ticked (rule (h)). Each item has one proof of its own. A refactor
item changes no behavior: no rename, no reordering, no fix. Three items are not refactor
items: CP-05, CP-06 and CP-07 change the Priest's levels 1-9 on purpose, before the
baselines. So in every item after them "today" means the code as CP-08 and CP-09 recorded
it. The ids were renumbered twice, on 2026-10-06 and on 2026-10-07, both times before any
item was started. From now on an inserted item gets a letter (rule (i)) and no id changes.

### A. Open the leg and build the proof tools

- [x] **CP-00 - Commit this plan.** Depends: None
  - Work: This file, docs/natural-class-profiles.md, exists untracked and holds every
    section it needs. Do not write it again. Check that it still has the house-style
    sections of docs/natural-abyss-entry.md: dated Status, start and finish contract, a
    Standing rules section (Java first; natural play; deaths are outcomes and the death
    rule; loot every kill; the bind at the working hub's obelisk everywhere, Ishalgen
    included, and hub flight transporters from Altgard on; help items for every class line;
    potions and no bandages; no level goals; the inventory check; no gear bought; snapshots
    only from committed code), per-class notes, hazards, the CP checklist, the operator
    decisions with every answer or default, Blocked, Out of scope, a Progress log, and a
    'Combined goal prompt' section holding the loop prompt (precedent
    docs/natural-altgard-haramel.md:437-439). Seven answers already stand in it as Answer
    lines: CP-Q1 and CP-Q8, taken from the request on 2026-10-06 and confirmed on
    2026-10-07, and CP-Q3, CP-Q10, CP-Q11, CP-Q12 and CP-Q21, given on 2026-10-07. Add an
    Answer line for anything the operator has answered since. Add decision row D39 beside
    D38 (docs/e2e-player-simulation-plan.md:2715). It quotes the maintainer's request and
    those answers, extends the natural-play rules of D22 and D23 to all six starters, and
    records the three things the answers of 2026-10-07 change: help items for every class
    line from level 1 (OD-13 approved them for the Cleric from level 10), no bandages, and
    the bind at each Ishalgen quest hub with the change it makes to the accepted Priest
    line's levels 1-9. Add a pointer row in CLAUDE.md and one line in
    docs/natural-ntc-readiness.md. Two older documents state rules the answers of 2026-10-07
    widened, so add one dated line to each that points here and changes nothing else: under
    OD-13 in docs/natural-ascension-altgard.md (help items are now for every class line and
    from level 1), and at the standing bind policy AB-Q5 in docs/natural-altgard-leveling.md
    (the bind at the working hub now holds in Ishalgen too). Commit those six files and
    stage nothing else: three
    untracked docs/playtest-*.md files are in the tree and are not part of this.
  - Proof: The commit itself: git show --stat HEAD lists exactly those six files, with the
    seven pre-commit checks green (docs only, so no Fast run).
  - 2026-10-07: done. The house-style sections were checked against
    docs/natural-abyss-entry.md and all stand; no Answer line was added, because the
    operator has answered nothing since the seven. D39 stands above D38 in
    docs/e2e-player-simulation-plan.md, where the newest row goes. The pointer row is in
    CLAUDE.md, and one dated line each is in docs/natural-ntc-readiness.md, under the
    decision table that holds OD-13 in docs/natural-ascension-altgard.md (a table row
    takes no line under it) and under the standing bind policy in
    docs/natural-altgard-leveling.md. The seven pre-commit checks pass
    (run/cp/CP-00/checks/, seven logs, each exit 0); docs only, so no Fast run. The commit
    holds exactly these six files.
- [x] **CP-01 - Class-line contract for six starters and eleven second classes.** Depends:
  CP-00
  - Work: Java first: JAVA/data/handlers/quest/ishalgen/_2132ANewSkill.java:24-67,
    ascension/_2008Ascension.java:136-162, _2009ACeremonyinPandaemonium, the six dispatch
    handlers and src/com/aionemu/gameserver/services/ClassChangeService.java:60-107. Freeze
    e2e/natural-class-lines.json with one javaReference and its loader
    Sc/Classes/NaturalClassLineContract.cs. Per starter: class id, Q2132 var and trainer,
    class-selection page, Q2009 var, reward group, preceptor and page, and the level-1
    mastery ids with the weapon groups and armor types they unlock. Per second class: SETPRO
    action, class id, parent, level-9 masteries, the *_selectable_reward list with its
    items, the dispatch quest and its work item. Where the 4.8 client's dialog pages can be
    read through tools/client-extract, note which SETPRO buttons pages 3057, 3398, 3739,
    4080, 3569 and 3910 show; otherwise record that as unverified. The file holds server
    facts only. It holds no line: SIM accounts and character names are in
    NaturalClassLine.cs (CP-14). No bot code reads the file yet.
  - Proof: Unit test UT/NaturalClassLineContractTests: one theory over the 6 starters and 11
    second classes that recomputes every value from quest_data.xml, skill_tree.xml, the
    Ishalgen and Pandaemonium spawn files and the C# handler source, requires the PRIEST and
    CLERIC rows to equal e2e/natural-ascension-contract.json:35, 54-56, and requires
    javaReference to equal lastCompletedJavaCommit in docs/upstream-port-state.json, the
    field the other natural contract tests compare with (for example
    UT/NaturalAscensionContractTests.cs:223).
  - 2026-10-07: done. e2e/natural-class-lines.json holds 6 starters and 11 second classes at
    javaReference ce54b7931; the loader is Sc/Classes/NaturalClassLineContract.cs, public,
    in the namespace Aion.Bots.Scenarios.Classes, and no bot code reads it.
    UT/NaturalClassLineContractTests passes 20 of 20: the theory's 17 class rows and 3 facts
    (run/cp/CP-01/proof-NaturalClassLineContractTests.log). Three wrong values put into the
    file by hand (a trainer, a mastery, a work item) failed three rows, and the file was put
    back. The Java handlers named in Work were read first, and the C# handlers the test
    reads say the same.
    - The class pages are verified, not left open. The checked-in page map of the 4.8
      client's dialogs (e2e/custom-quest-client-dialogs.json, QUEST_Q2008) shows: select7
      (3057, Warrior) SETPRO7 and SETPRO8; select8 (3398, Scout) SETPRO9 and SETPRO10;
      select9 (3739, Mage) SETPRO11 and SETPRO12; select10 (4080, Priest) SETPRO14 and
      SETPRO13; select8_3 (3569, Engineer) SETPRO15 and SETPRO17; select9_3 (3910, Artist)
      SETPRO16 alone. Each page shows exactly its own starter's second classes, and each
      page id is the DialogAction constant of the client's page name. No client file was
      decoded for this.
    - Level-1 masteries, from skill_tree.xml and the mastery effects of skill_templates.xml.
      Every starter has Clothes 40 and Cloth Armor (robe) 103. Warrior adds sword 37, mace
      39, leather 41, chain 42 and shield 43. Scout: sword 37, dagger 66, leather 41. Mage:
      spellbook 100. Priest: mace 39, leather 41. Engineer: pistol 112, leather 41. Artist:
      harp 114. So a Priest wears no chain and no staff before level 9, and only the Warrior
      may carry a shield.
    - Level-9 masteries worth knowing: the Gunner's only row is Advanced Pistol 117 and the
      Bard's only row is Advanced Stringed Instrument 124; the Rider gets chain 49 and
      Cipher-Blade 115 and no advanced pistol row. The Cleric and the Chanter have the same
      six rows (46, 48, 49, 50, 89, 106).
    - Q2009 reward lists: the Cleric's list is named priest_selectable_reward, and the
      Chanter's own chanter_selectable_reward holds the same two items (Karmic Warhammer
      100100495, Karmic Staff 101500498). The Gunner, the Rider and the Bard are each
      offered one weapon. Q2132 has six reward groups of 275 XP and no item. The six
      dispatch handlers are the same code: Q2901 to Q2904, Q29070 (Gunner and Rider) and
      Q29071 (Bard).
    - Bundle: seven pre-commit checks pass, Aion.GameServer.Tests passes 4,675 with 16
      skipped, and Fast passes all 11 scenario gates (run cp01-fast: 115 tests passed, 5
      skipped, 8.5 minutes). The logs are in run/cp/CP-01/checks/.
- [x] **CP-02 - Trace comparer with coverage counts.** Depends: CP-00
  - Work: Add scripts/sim/trace/compare_traces.py, reading through
    scripts/sim/trace/trace_input.py: stream two .trace.jsonl files, drop ts and run, skip
    the natural-run-context record, take an ignore list of field paths, print the index and
    both sides of the first differing record plus both record counts, and exit 0 only when
    identical. Add a --counts mode that prints, for one trace, how many deaths, revive
    steps, retreats, rest sits, between-fight heals, vendor buys, pull plans, patrol waits,
    emergency decisions and at-target pulls it holds, and how many life and mana potions
    were drunk, shield scrolls and speed or running scrolls used, help items supplied, binds
    made and soul heals done. No C# change.
  - Proof: python scripts/sim/trace/test_compare_traces.py: fixtures for a pair identical
    but for ts and run, one changed field, one missing record, two swapped records, and a
    counts fixture.
  - 2026-10-07: done. scripts/sim/trace/compare_traces.py and its test are added; no C#
    changed. python scripts/sim/trace/test_compare_traces.py passes 10 of 10
    (run/cp/CP-02/proof-test_compare_traces.log): the pair identical but for ts, run and the
    context record; one changed field, reported with its record index, its field path, both
    sides and both counts; a changed vt, which is a difference; a missing record; a trace
    that stops early; two swapped records; the ignore list, for every packet and for one;
    a missing trace and a folder with two traces, which are errors (exit 2) and not
    differences; the counts fixture; and the digest.
    - Three modes. Comparing streams both traces and exits 0 only when they are identical.
      --counts prints 22 numbers, with --json as one object. --digest prints the record
      count and the SHA-256 of the normalized trace; it was added here because CP-08 stores
      that hash and must use the same normalization.
    - What is dropped: ts, run and the whole natural-run-context record (it holds the run
      id, the build and the module id). The virtual clock vt is compared.
    - Tried on real traces, beside the proof: the munin capture trace compares identical
      with itself (151,111 records), and two older Fast traces of different runs,
      run/ax12a-fast and run/ax12b-fast, compare identical (67,181 records). The munin
      capture counts 3 deaths, 3 revive steps, 5 retreats, 30 between-fight heals, 156 pull
      plans, 4 patrol waits, 14 emergency decisions, 79 life potions, and no vendor buy,
      help item, bind or soul heal. That agrees with the reviewer's numbers in hazard 4.
    - **A finding: a rest sit writes no trace record.** The sit is a CM_EMOTION packet,
      which the trace does not record, and RestAsync traces nothing when it sits. Only the
      walk to a rest spot is traced (rest-relocate, or rest-relocate-none), and a sit at a
      spot that is already clear leaves nothing. So the count is named restSitsTraced and
      is a lower bound: 5 in the munin capture. A changed sit still shows in a trace
      comparison, because the virtual clock of every later record moves. But CP-09 cannot
      say from the counts alone how often a scope sits, which CP-17 leans on. It is logged
      here and not fixed: a new trace record would be a C# change, and after CP-08 it would
      change the baselines.
    - Bundle: seven pre-commit checks pass, scripts/sim/test-sim-snapshot.ps1 and the new
      test pass, and Fast passes all 11 scenario gates (run cp02-fast). The logs are in
      run/cp/CP-02/checks/.
- [x] **CP-03 - Replay without capture.** Depends: CP-00
  - Work: CP-Q3 was answered on 2026-10-07, so this item is not blocked.
    scripts/sim/sim-snapshot.ps1 gains -Action Replay: a fresh owned schema through
    new-sim-db.ps1 (as Capture makes one at lines 284-285) or Restore-Snapshot for -From,
    one scope's environment through Invoke-NaturalJourney into a new run directory, then the
    schema is dropped. It writes nothing under run/snapshots. It accepts -CapitalStage,
    -Bridge, -AltgardLeg1 with -Leg, -From, -LaterCapital, a new -StopAt (NI08_STOP_AT, as
    questId:status:packedVars; status 5 must be accepted) and a new -StopAfterQuest
    (NI07_STOP_AFTER_Q2004 to Q2007); the three Capture-only guards at lines 59, 63 and 66
    are widened for Replay. It also takes -Item and -Run and writes its evidence under
    run/cp/<item>/<run>/; without -Run it makes a run id from the time. With -StopAfterQuest
    the journey test picks its own shorter deadline
    (SimT/SimulationNaturalIshalgenJourneyTests.cs:51-52), and Replay does not change that.
    Invoke-NaturalJourney's cleared list (lines 88-92) gains NA_HELP_ITEMS,
    AION_BOT_DASHBOARD_PORT and AION_SIM_PROCESS_KEY, which run-natural-complete.ps1:17-21
    already clears. With NA_HELP_ITEMS cleared the help supply is on, as the Standing rules
    want it for every line. The fake Docker of the script test reaches only
    sim-snapshot.ps1's own Invoke-Docker (its -Docker parameter); a fresh schema is made by
    a child pwsh running new-sim-db.ps1, which calls the real docker and has no -Docker
    parameter. So new-sim-db.ps1 gains a -Docker parameter that sim-snapshot.ps1 passes
    through (or the test defines a pwsh function that stands in for it), so that the
    fresh-schema Replay runs against the fake Docker.
  - Proof: pwsh -NoProfile -File scripts/sim/test-sim-snapshot.ps1 with new cases against
    the fake Docker and a fake dotnet, for both Replay forms, the fresh schema (scopes p, m
    and b use it) and -From (Restore-Snapshot): Replay writes no snapshot directory, writes
    its evidence under run/cp/<item>/<run>/, drops the schema on success and on failure, and
    a historical Restore prints exactly what it prints today.
  - 2026-10-07: done. scripts/sim/sim-snapshot.ps1 has -Action Replay, and
    scripts/sim/new-sim-db.ps1 takes -Docker, which sim-snapshot.ps1 passes through. pwsh
    -NoProfile -File scripts/sim/test-sim-snapshot.ps1 passes with the new cases
    (run/cp/CP-03/proof-test-sim-snapshot.log), against the fake Docker and a fake dotnet:
    - the fresh form for scope m, scope p (-CapitalStage start), scope b (-Bridge
      -LaterCapital), -StopAt 2132:5:0 and -StopAfterQuest 2004; and the restored form for
      -From munin alone and for -AltgardLeg1 -Leg l12 -From a mock endpoint;
    - each plays on an owned schema, sends its evidence to <ReplayRoot>/<Item>/<Run>/, ends
      with DROP DATABASE of that schema, runs no mysqldump and leaves the snapshot root as
      it was; a failed journey, fresh or restored, still drops the schema and keeps its
      evidence with a receipt that says passed false;
    - the journey gets no NA_HELP_ITEMS, AION_BOT_DASHBOARD_PORT or AION_SIM_PROCESS_KEY
      from a parent that sets all three, and the parent gets them back;
    - a historical Restore prints the same five properties and the same three environment
      names; thirteen refusals touch no MySQL.
    Two deliberate breakages of the script (no drop in Replay; NA_HELP_ITEMS not cleared)
    each failed the test, and the script was put back.
    - What Replay does beyond the Work line, so later items can lean on it: -From by itself
      resumes the snapshot on the environment its Restore prints; -ReplayRoot moves the
      evidence root (the test uses it; the default is run/cp); an evidence folder that
      exists is refused, so two attempts never share one; and a working tree with
      uncommitted changes is allowed, because CP-06 and CP-07 prove their change before
      they commit it. The receipt replay.json records the scope's environment, the git sha
      and the uncommitted paths.
    - Capture and Verify go through the same runner, so they too now clear NA_HELP_ITEMS,
      AION_BOT_DASHBOARD_PORT and AION_SIM_PROCESS_KEY. With them cleared the help supply
      is on and the bot monitor is at its default port, 17880.
    - No journey was played for this item. With the real Docker, new-sim-db.ps1 created a
      throwaway schema (61 tables) and sim-snapshot.ps1 -Action Drop removed it; no
      aion_gs_sim_ni08_* schema is left. CP-06 is the first real Replay.
    - Bundle: seven pre-commit checks pass, both script tests pass, and Fast passes all 11
      scenario gates (run cp03-fast). The logs are in run/cp/CP-03/checks/.
- [x] **CP-04 - The neutral gate script.** Depends: CP-02, CP-03
  - Work: Add scripts/sim/run-neutral-gate.ps1 -Set <names> [-Record] [-Item <id>] [-Run
    <id>]: build once, run each scope through Replay with --no-build, compare each trace
    with its baseline, print one verdict and write verdict.json under run/cp/<item>/<run>/;
    a passing candidate trace is deleted. The scope table at the top of the script is data.
    It holds the seven Priest and Cleric scopes, set all (which includes p), and from the
    start the five class scopes mage, warrior, artist, engineer and scout. A class scope is
    off until its row stands in the baseline file, so recording one later changes no script
    and its commit is evidence only. -Record refuses to start with uncommitted changes under
    src, tests, game-server and parity-artifacts. It then plays each named scope twice
    itself, compares the two passes, and writes e2e/natural-neutral-baseline.json once, at
    the end, because that file is under parity-artifacts and an earlier write would make the
    next pass refuse. It copies the recorded traces to run/cp/baseline/<sha>/ and to the
    second copy in ../BeyondAionSharp-cp-baseline/<sha>/. The gate pins NA_HELP_ITEMS
    (unset, for every line: help items are on), AION_BOT_DASHBOARD_PORT and
    AION_SIM_PROCESS_KEY and writes them beside each baseline. Add both script tests
    (scripts/sim/test-sim-snapshot.ps1 and scripts/sim/trace/test_compare_traces.py) to the
    list in CLAUDE.md.
  - Proof: pwsh -NoProfile -File scripts/sim/test-sim-snapshot.ps1 with gate cases against
    the fake Docker and a fake dotnet: the gate's verdict is pass, fail and
    refused-dirty-record in three fixture cases; -Record writes the baseline file once after
    two identical passes and not at all after two different ones; and a class scope with no
    baseline row is refused by name.
  - 2026-10-07: done. scripts/sim/run-neutral-gate.ps1 is added, with its cases in
    scripts/sim/test-sim-snapshot.ps1, and both script tests are in the list in CLAUDE.md.
    The test passes (run/cp/CP-04/proof-test-sim-snapshot.log), against the fake Docker, a
    fake git and a fake dotnet that writes a small trace:
    - the verdict is pass (exit 0), fail (exit 1) and refused-dirty-record (exit 2, nothing
      played, no baseline written);
    - -Record of three scopes plays six journeys after one build, keeps each trace in the
      baseline folder and in the second copy, deletes the pass traces from the evidence and
      writes the baseline file once, rows in table order, each with its replay, snapshot,
      commit, pinned environment, record count, hash and counts row;
    - -Record of two different passes writes nothing, keeps both traces and names the first
      differing record; a set in which one scope fails writes none of its scopes;
    - a passing candidate trace is deleted and its other evidence kept; a failing one is
      kept, with the first differing record in the verdict; a failed journey is a failed
      scope, not a crash of the gate;
    - a class scope with no baseline row is refused by name, alone and beside set all.
    Two deliberate breakages of the gate (hash not compared; baseline written after a
    failed record) each failed the test, and the script was put back.
    - What the gate does beyond the Work line, so later items can lean on it. **Set all** is
      every Priest and Cleric scope that has a row, and the verdict lists the ones left
      out; in a -Record run it is all seven. **-Ignore** is given at -Record only and is
      stored in the scope's row; a comparison uses its row's list and takes none of its
      own. **-ReRecord** is needed to record a Priest or Cleric scope that already has a
      row, so rule (j) cannot be broken by a slip; a class scope's row may be replaced
      without it. A recorded trace missing from run/cp/baseline is copied back from the
      second copy when a comparison needs it. A comparison may run on a changed tree, and
      the verdict lists the uncommitted paths.
    - Where things are written. The verdict is run/cp/<item>/<run>/verdict.json. Each
      scope's Replay evidence is beside it, in run/cp/<item>/<run>-<scope>/ (and
      <run>-<scope>-pass1 and -pass2 in a record run).
    - **The baseline sha is per row.** Each row holds the commit it was recorded at, and
      its trace is run/cp/baseline/<that commit>/<scope>.trace.jsonl. CP-08 and CP-09
      record at two commits, so the baseline sha of the re-record rule is the oldest
      commit among the rows.
    - A class scope replays with -Class <line> -StopAt 2004:5:0. sim-snapshot.ps1 learns
      -Class in CP-28; until then such a replay fails by name, and no class scope is
      recorded before CP-53.
    - No journey was played for this item. CP-08 is the first real gate run.
    - Bundle: seven pre-commit checks pass, both script tests pass, and Fast passes all 11
      scenario gates (run cp04-fast). The logs are in run/cp/CP-04/checks/.
- [x] **CP-05 - The level 1-9 help kit: manifest and allowlist.** Depends: CP-00
  - Work: The operator approved help items for every class line on 2026-10-07 (Standing
    rules, CP-Q12). This item writes down what the kit is; nothing supplies or uses it
    before CP-06. Read first: Sc/NaturalHelpItemAllowlist.cs and
    Sc/NaturalHelpItemPolicy.cs; the supply and use sites in J (TopUpHelpItemsAsync, the
    SupplyHelpItemAsync hook, the NaturalHelpTrigger sites and the IsCleric tests beside
    them); OD-13 and Appendix D and D.2 of docs/natural-ascension-altgard.md (lines 179 and
    2773-2879); and the item and skill data. Java first for the gate: a consumable is
    refused by its restrict row, not by its item level
    (JAVA/src/com/aionemu/gameserver/restrictions/PlayerRestrictions.java:327-336). The
    note "Every line" under Per-class notes holds what was read on 2026-10-07; check each
    line of it against the files before leaning on it. Then:
    (1) Write the manifest into that note as a table. Per level band (levels 1-9 are one
    band unless the data gives a reason to split it): the item, what it is for, when it is
    used, and how many are kept (top up to N when fewer than M are owned). It holds the
    best life potion a level 1-9 character may use by its restrict row, the shield scroll,
    the Greater Running Scroll 164000076, the scroll for the shared speed slot, the mana
    potion, and every other consumable a starter owns, each marked used or left out with
    its reason. The defaults of that note apply where the operator's words leave room.
    (2) Settle the event-scroll finding of that note in the unit test and write the result
    into the doc.
    (3) Extend the allowlist to levels 1-9 for every class line. The list is keyed by item
    and level, not class, so one set of rows serves every line; which owned scroll a class
    puts in the shared speed slot is its profile's choice. The level 1-9 rows go in a table
    of their own beside NaturalHelpItemAllowlist.Approved, and the supply plan and
    RequireApproved read both (Sc/NaturalHelpItemAllowlist.cs:78-84, 93-113). Approved
    itself holds the Cleric's bands from level 10 on and is not edited.
    (4) If a family of the level 1-9 kit is missing from the Cleric's bands from level 10
    on, or weaker there, write that into the "Not blocking" list of the Blocked section, as
    a finding for the operator. It blocks no item. Do not change the Cleric's bands or the
    Cleric legs.
    tools/Aion.LiveBots reads the allowlist (LiveNaturalHelpItemSupplier.cs) and must still
    build; LIVE itself stays out of scope.
  - Proof: Unit test UT/NaturalStarterHelpKitTests: it reads the manifest table from this
    file and requires every supplied row to equal a level 1-9 row of the allowlist (id,
    band, N and M) and the shipped data (the item exists; its item level, use skill, skill
    level, use-delay group and delay match; its restrict row lets all six starter classes
    use it at the band's first level); nothing else is approved below level 10; the potion
    row is the life potion with the largest heal among those a level-1 character may use;
    the running row is 164000076; and the event scrolls' speed is computed from the item's
    skill level and the skill's delta. The three existing help-item test files
    (UT/NaturalHelpItemAllowlistTests.cs, UT/NaturalHelpItemPolicyTests.cs and
    UT/NaturalHelpItemSupplyTests.cs) run in the bundle. Two of their assertions state the
    old default, nothing below level 10, which the operator replaced on 2026-10-07.
    UT/NaturalHelpItemSupplyTests.cs:22 (the plan at level 9 is empty) is the one expected
    value this item changes, to the level 1-9 kit. UT/NaturalHelpItemPolicyTests.cs:51 (no
    shield scroll is picked at level 9) is changed by CP-06. No other expected value is
    edited, and every assertion about level 10 and above stays.
  - 2026-10-07: done. The four parts:
    - (1) The manifest is the table under "Every line: the level 1-9 help kit and the
      Ishalgen binds": twelve rows, one band for levels 1-9. Three are supplied: Major Life
      Potion 162000006 (30 when below 10), Lesser Anti-Shock Scroll 164000067 (30 when
      below 8) and Greater Running Scroll 164000076 (20 when below 5). Nine are what every
      starter owns: five used (Accelerox, Castafodin, Blitzopan, the Minor Mana Potion and
      the Minor Life Potion as the fallback) and four left out, each with its reason (the
      fruit juice, the Lodas Amulet, the Administrator's Boon and the bandages).
    - (2) The event-scroll finding is settled: the data read is right and the bot's
      catalog is wrong. The event scrolls are as strong as the Greater tier (+30% run
      speed, +9% casting speed, 9% faster attacks) and last 30 min. The catalog is not
      edited; the cost to the Cleric at levels 20-29 is under "Not blocking".
    - (3) NaturalHelpItemAllowlist.Starter holds the three level 1-9 rows beside Approved,
      which is not edited; the supply plan and RequireApproved read both through AllLevels.
      Nothing calls the plan below level 10 before CP-06, so nothing is supplied yet.
    - (4) Where the Cleric's bands are weaker than the level 1-9 kit is under "Not
      blocking": the potion at levels 10-19 and the Running scroll at levels 10-29. Two
      more points are there for the operator: Accelerox against the Greater Running
      Scroll, and a recovery potion that heals mana too.
    Java first: PlayerRestrictions.canUseItem refuses by the restrict row's class and
    level (lines 327-336), the row defaults to 1 for all seventeen classes
    (ItemTemplate.java:34), SkillUseAction casts the item's skill at the action's level,
    and BufEffect.getModifiers adds the delta times that level.
    - Proof: UT/NaturalStarterHelpKitTests passes 6 of 6
      (run/cp/CP-05/proof-NaturalStarterHelpKitTests.log, with the three existing help-item
      files: 46 of 46). It reads the manifest table from this file. Three wrong values put
      into the table by hand (a kept count, an owned count, a delay) failed three tests,
      and the table was put back. Of the three existing files one expected value changed,
      as named: the plan at level 9 is now the level 1-9 kit
      (UT/NaturalHelpItemSupplyTests.cs).
    - Two notes for CP-06 are in the "Every line" note: the Cleric's keep list reads
      Approved alone, and a level-9 Cleric passes the supply gate.
    - Bundle: seven pre-commit checks pass (the warning baseline builds tools/Aion.LiveBots
      too), Aion.GameServer.Tests passes, and Fast passes all 11 scenario gates (run
      cp05-fast). The logs are in run/cp/CP-05/checks/.
- [x] **CP-06 - The Priest plays levels 1-9 with the kit.** Depends: CP-03, CP-05
  - Work: This item changes the accepted Priest line's levels 1-9 on purpose (the operator,
    2026-10-07: "even change the priest defaults!"). It is not a refactor. Open the gates
    the "Every line" note lists, for the Priest at levels 1-9:
    (1) Supply. The Cleric test in TopUpHelpItemsAsync (J:1505) lets the Priest through,
    and the Ishalgen decision loop (from J:724) gains the stock checks the later legs have:
    at run start, after each level-up, at a vendor visit and at a checkpoint.
    (2) Use in a fight. The shield scroll and the mana potion are offered to the Priest too
    (J:9130, 9132). The static NaturalPriestCombatPolicy already orders both
    (Sc/NaturalPriestCombatPolicy.cs:234, 289-290) and is not edited.
    (3) Use out of a fight. The scroll part of BuffOurselfAsync (J:9596) runs for the
    Priest, and the Ishalgen walks send the TravelLeg trigger, so the Running scroll is used
    before a long leg.
    (4) Tier rules. At levels 1-9 the help policy picks what the manifest lists, by the
    restrict gate. From level 10 on its picks stay as they are
    (Sc/NaturalHelpItemPolicy.cs:85, 102, 136).
    (5) The potion. SelectOwnedPotion, TotalHealingCount and HealingSkillIds
    (Sc/NaturalIshalgenPotionPolicy.cs:20, 29-41) learn the manifest's potion, ahead of the
    tiers they know.
    (6) Keep. The Priest's keep-or-sell rule holds three supplies today and marks any other
    sellable consumable "unneeded-or-unusable" (Sc/NaturalIshalgenInventoryPolicy.cs:70,
    198-200). Add the manifest's items to what it holds, or the first vendor visit sells
    the kit. The starter's 20 bandages stay unprotected.
    The Cleric's supply, bands and picks from level 10 on do not change: in the three
    existing help-item test files this item changes one expected value,
    UT/NaturalHelpItemPolicyTests.cs:51 (at level 9 the shield scroll is now picked), and
    every assertion about level 10 and above stays. The new Ishalgen stock checks of (1) run
    only below level 10: a Cleric who returns to Ishalgen after an early ceremony is
    supplied as today. The LIVE Priest gets the same level 1-9 changes, because the code is
    shared; no LIVE run is made in this plan. What is left of the kit at
    level 10 is owned stock and runs out (hazard 27). Every supplied item is written to
    help-items.json, as today. No snapshot is recaptured, and `munin` stays as it is. Rule
    (e) applies: two attempts at most, then Blocked.
  - Proof: One contained run under run/cp/CP-06/<run-id>: sim-snapshot.ps1 -Action Replay
    -Item CP-06 of scope m (a fresh Priest, bridge off, seed 1, to the Munin stop). The run
    passes; help-items.json lists every supplied row of the manifest; and the trace shows
    the kit's life potion drunk, the shield scroll used and the Running scroll used, each
    at least once by a Priest below level 10. If the Priest's HP never falls to the shield
    scroll's threshold, the lowest HP seen is reported and the item is not ticked.
  - 2026-10-07: done on the first attempt. Run m-a1 (run/cp/CP-06/m-a1/, scope m, seed 1,
    a fresh Priest, bridge off) passed in 7 min 51 s and reached the Munin stop at game
    time 3 h 07 min, level 9, with no death.
    - help-items.json lists the three rows of the manifest, supplied at the run's start at
      level 1 (30 Major Life Potions, 30 Lesser Anti-Shock Scrolls, 20 Greater Running
      Scrolls), and three top-ups at level 9, each at a checkpoint: 30 and 21 potions and
      16 Running scrolls.
    - Used by the Priest below level 10: 51 Major Life Potions (the first at level 3, 37 of
      them at level 9), 3 Lesser Anti-Shock Scrolls (all at level 9), 21 Greater Running
      Scrolls (from level 3 on) and 7 Castafodin. The lowest HP seen was 30.6%, at the
      violet generator of Q2007. No mana potion was drunk: the Priest never ran below its
      healing reserve.
    - Against the old munin capture, which played without the kit: deaths 3 to 0, retreats
      5 to 3, emergency decisions 14 to 0, between-fight heals 30 to 12, patrol waits 4 to
      0, pull plans 156 to 134, records 151,112 to 121,214. Hazard 4 is real: several
      branches are run much less. CP-09 checks every later item's scope against the new
      counts.
    - What changed, by the six parts of Work. (1) Supply: TopUpHelpItemsAsync asks the new
      UsesHelpItems (the Cleric, or any character at level 9 or below), and the Ishalgen
      decision loop checks the stock at the run's start, after a level-up and at each
      decision, below level 10 only; a vendor visit checks it too. (2) and (3): the shield
      scroll, the mana potion and the scroll upkeep ask UsesHelpItems. Below level 10 the
      walks ApproachAsync and ApproachShippedSpawnAsync send the TravelLeg trigger first;
      from level 10 on that trigger stays at the two bridge sites. (4) Below level 10
      NaturalHelpItemPolicy picks among StarterScrollIds, the manifest's five scrolls, with
      no tier rule; from level 10 on the tier rule is untouched. (5) The potion policy
      knows the Major Life Potion 162000006, drinks it first and counts it as stock. (6)
      The Priest's keep list gains the three supplied items; the Cleric's keep list reads
      AllLevels, so what is left of the kit at level 10 is not sold.
    - Two things differ from the Work line. The event scrolls were not added to the
      Priest's keep list: they cannot be sold, an existing test pins that reason for
      Accelerox, and so they were held already. And the gate is by level, not by class:
      below level 10 any character passes, which is what every later class line needs;
      CP-16 moves it behind the profile.
    - Tests. UT/NaturalStarterHelpKitUseTests is new and passes (the policy's picks at
      levels 1, 5 and 9 and from level 10 on, the potion order, the keep rule). In the
      three existing help-item files one expected value changed, as named: at level 9 the
      shield scroll is picked (UT/NaturalHelpItemPolicyTests.cs). No other expected value
      was edited.
    - Not run, and changed by the shared code: the environment-gated Mau course plays a
      level-9 Priest through the same fight loop, so it now offers that Priest the kit it
      owns; and the LIVE Priest gets the same level 1-9 changes.
    - Bundle: seven pre-commit checks pass, Aion.GameServer.Tests passes, and Fast passes
      all 11 scenario gates (run cp06-fast). The logs are in run/cp/CP-06/checks/.
- [x] **CP-07 - Bind at each Ishalgen quest hub.** Depends: CP-03, CP-06
  - Work: This item too changes the accepted Priest line's levels 1-9 on purpose (the
    operator, 2026-10-07: "We should have been binding in Ishalgen the whole time, at each
    quest hub (the village and the outpost), and soul heal as discussed."). It depends on
    CP-06 so that the two changes are proven one at a time. Java first: the bind and its
    price (JAVA/data/handlers/ai/ResurrectAI.java:44-107;
    JAVA/src/com/aionemu/gameserver/model/templates/BindPointTemplate.java:21-34) and where
    a revive goes
    (JAVA/src/com/aionemu/gameserver/services/teleport/TeleportService.java:362-383).
    What exists today: BindAtAldelleIfNeededAsync (J:4877-4893) binds at the village
    obelisk 700063. Its one caller is AcceptAllAtCurrentHubAsync (J:7824-7828), which is
    called at J:756 and J:813, and both calls run only with NI07_OPTIMIZE_HUBS set. No
    runner sets it, so the accepted route never binds in Ishalgen. The outpost bind does
    not exist yet.
    To do: for every line, bind at the village obelisk 700063 on the bot's first arrival at
    the village for work, and at the outpost obelisk 700064 when the work moves there. The
    outpost bind serves the two hubs the bot's table calls mijou and anturoon. Each bind is
    made once: a later visit to the village does not move the bind back. Do it from the
    Ishalgen decision loop with one helper for both obelisks, built from
    BindAtAldelleIfNeededAsync. Do not turn on the rest of the hub optimizer: no hub pickup
    order, no safe work groups, no hub flight. The pure lookups
    NaturalIshalgenHubPolicy.At and ForQuest may be used to tell where the work is. The fee
    is read from the bind point data: 43 Kinah at the village and 134 at the outpost
    (bind_points/bind_points.xml:22-23), 177 in all. A bind is skipped only when the purse
    does not cover the fee, as the helper does today, and the skip is traced. Add 700064
    and the Soul Healers 203512 and 203680 to the navigation graph's NPC list (J:311-320)
    if the walk to them needs it. Routing is part of this item's work, not a rule (e) fix.
    The sites listed in the "Every line" note assume a revive or a Return at the first spawn
    point. Once bound, each of them skips the eastern-road walk when the revive or the
    Return landed at the outpost bind, and keeps it when it landed at the spawn point or at
    the village; the Return helper's 30 m requirement (J:6304-6305) is met or the fallback
    is not cast beside the bound obelisk. If these edits do not fit one iteration with the
    bind, do the village bind here and write the outpost bind with its routing as lettered
    item CP-07a. The death rule is not edited: after a death it now revives the bot at the
    working hub and soul heals at the Soul Healer beside that
    obelisk (Linevir 203512 at the village, Rusalka 203680 at the outpost). Read Q2129's
    Return branch (J:7990-8004), which a bind in Ishalgen makes reachable, and write into
    the doc whether it ran. Write into the doc both fees as paid, the Kinah left at Munin,
    and, if the Priest died, where it revived and whether the soul heal ran; a run with no
    death leaves that unshown, and the doc says so. Rule (e) applies.
  - Proof: One contained run under run/cp/CP-07/<run-id>: sim-snapshot.ps1 -Action Replay
    -Item CP-07 of scope m (a fresh Priest, bridge off, seed 1). Both binds are observed in
    the trace, the village's with its fee of 43 Kinah and the outpost's with 134, each
    followed by a client-observed bind point within 20 m of its obelisk, and the run
    reaches the Munin stop.
  - 2026-10-07: done on the second attempt. Run m-a2 (run/cp/CP-07/m-a2/, scope m, seed 1,
    a fresh Priest, bridge off) passed in 5 min 20 s and reached the Munin stop at game
    time 3 h 14 min: level 9, all 41 quests, Q2008 untouched, bound at the outpost.
    - **The village bind:** at 0 h 04 min, before Q2100, at obelisk 700063 for 43 Kinah
      (2,380 to 2,337); the client's bind point is 2.3 m from the obelisk.
    - **The outpost bind:** at 0 h 18 min, before Q2003, at obelisk 700064 for 134 Kinah
      (2,337 to 2,203); the bind point is 1.2 m from the obelisk. The bot walked the
      eastern road there once, and Q2003's own walk was then skipped.
    - **The death:** one, at the violet generator of Q2007 at 1 h 10 min. The Priest
      revived at the outpost bind, and the death rule soul healed at Rusalka 203680 beside
      it: 287 XP back for 71 Kinah. Neither Soul Healer needed an entry in the navigation
      graph's NPC list. No death fell before the village bind, so a revive at the map's
      first spawn point is not shown.
    - **Q2129's Return branch ran.** At the reward claim of Q2129, at 3 h 05 min, the bot
      cast Return from (659, 904) and landed at the outpost bind. It was the run's only
      Return.
    - **Kinah left at Munin:** 40,342. The CP-06 run ended with 40,590; the difference of
      248 is the two fees and the soul heal (43 + 134 + 71).
    - Counts of m-a2: 123,113 records, 1 death, 1 retreat, 7 emergency decisions, 15
      between-fight heals, 148 pull plans, 69 life potions, 3 shield scrolls, 20 Running
      scrolls, 2 binds, 1 soul heal.
    - **Attempt 1 (run/cp/CP-07/m-a1/, kept) failed** with "NPC 210391 was not killed in 6
      non-retreat attempts" in Q2128. It showed two things. First, a fault in the new
      helper: the village bind was never made. The client shows a bind point from the
      first login, because Java sends the map's first spawn point when the character has
      none (TeleportService.sendObeliskBindPoint), and the helper took that for a bind
      that must not be moved back. The one small change of rule (e): only a bind at the
      outpost obelisk holds the village bind back. Second, a finding that is logged and
      not fixed: after a retreat from three attackers, the kill loop spent its six
      attempts in 30 s on monsters that were walking home ("combat-target-returned" one
      to three seconds after each pull). It did not happen in m-a2 or in the CP-06 run.
      Attempt 1 also had its death at the same generator, the same revive at the outpost
      and the same soul heal, and two Returns that landed at the outpost bind.
    - **What was built.** BindAtIshalgenHubIfNeededAsync runs in the decision loop before
      a quest's own steps: a quest of the hub aldelle binds at 700063, a quest of mijou or
      anturoon at 700064, any other quest nowhere. It is one helper for both obelisks,
      built from BindAtAldelleIfNeededAsync, which now calls it. It is not gated by level
      or class, so the Cleric who returns to finish Ishalgen binds the same way. A bind is
      skipped only when the purse does not cover the fee, and the skip is traced
      (ishalgen-hub-bind-skipped). Obelisk 700064 joined the navigation graph's NPC list.
    - **Routing.** Every revive and Return site of the "Every line" note goes through
      WalkEasternRoadToDerotAsync, so the edit is one: within 100 m of the outpost obelisk
      the road is skipped and the bot walks to Derot. A bot that landed at the spawn point
      or at the village still walks the road. Q2007's two tests for "within 400 m of the
      spawn point" needed no edit: at the outpost they are false, which is the right
      answer. UseLearnedReturnToBindAsync does not cast when the bot stands within 30 m
      of its bind on the Ishalgen map; on every other map it is what it was. The death
      rule is not edited.
    - One site is left as it was and is not reached on this route: when no walked history
      of the eastern road survives, Q2006's reward step casts Return, which now lands at
      the outpost and not near Ulgorn.
    - Tests: UT/NaturalIshalgenHubBindTests is new and passes 5 of 5 (the two fees, the
      Soul Healer the death rule finds beside each obelisk and none at the spawn point,
      and which hubs the obelisks serve).
    - Bundle: seven pre-commit checks pass, Aion.GameServer.Tests passes, and Fast passes
      all 11 scenario gates (run cp07-fast). The logs are in run/cp/CP-07/checks/.
- [x] **CP-07a - The first walk to Munin returns to the hub bind when no checked route is
  left.** Depends: CP-07
  - Work: A lettered item by rule (i), found by CP-08's record run on 2026-10-07 (run
    rec-a1). Scope p, the early-Ascension order, failed in its first pass: "q2008-v0-munin:
    No collision-checked route to the current destination." The Priest reached level 9 in
    the Mau field at (690, 1548), in the second stalker fight of Q2005, and left for Munin
    from there. Its first plan crossed monsters, and 16 s later, at (717, 1485) with 42
    hazards in view, no plan was left. The old accepted run (rc11-full-create-s1-a25)
    reached level 9 one fight earlier, at (723, 1533), found a road route at once and
    walked by the outpost to Munin. So the walk depends on where level 9 falls, and
    CP-06 and CP-07 moved that. It is not a fault of the kit or of a bind; it is the first
    run of the new levels 1-9 in the early-Ascension order, which CP-09's Work line sends
    back here. Scope p cannot be dropped: it is the guard of rule (c).
    To do: in ApproachBridgeNpcAsync, on the Ishalgen map only. When the approach fails
    with that reason and the bot is bound on that map more than 30 m away, cast the
    learned Return (UseLearnedReturnToBindAsync, as Q2005's fallback and the stranded
    Return already do), rest, and approach once more from the bind. A second failure
    throws as it does today. Trace the fallback. The outpost bind of CP-07 is what makes
    this a way out: from the outpost the old run had a road route to Munin. The change
    fires only where the run throws today, so no run that passes changes, and the bridge's
    steps on other maps are not touched. Rule (e) applies: two attempts at most.
  - Proof: One contained run under run/cp/CP-07a/<run-id>: sim-snapshot.ps1 -Action Replay
    -Item CP-07a -CapitalStage start (scope p, a fresh Priest, seed 1). The run passes, the
    trace shows the fallback's Return landing at the hub bind, and the Priest arrives at
    Munin for Q2008. No baseline exists yet, so rule (k) has no gate to run; CP-08 records
    p next.
  - 2026-10-07: done on the first attempt. Run p-a1 (run/cp/CP-07a/p-a1/, scope p, seed 1)
    passed in 38 s and its capital-start checkpoint is verified: no death, 2 retreats.
    - The Priest reached level 9 at the same place as in CP-08's failed pass, (690, 1548),
      and the first approach to Munin again ended at (717, 1485) with no checked route.
      The fallback fired there (trace record bridge-approach-return-to-bind), Return
      landed at the outpost bind (937, 1706) at game time 1 h 00 min, and from the outpost
      the Priest walked to Munin, played Q2008 and Q2009 and reached level 10 at the
      ceremony at 1 h 04 min.
    - In the same run, for CP-06 and CP-07 in the early-Ascension order: the kit was
      supplied at level 1, both binds were made (the village's at 0 h 02 min, the
      outpost's at 0 h 18 min), and at level 10 the stock check supplied the Cleric's mana
      serum, jelly and powder. It supplied no shield scroll and no life potion: the 30
      Lesser Anti-Shock Scrolls of the kit and the starter's potions already cover those
      bands.
    - What was built: ApproachBridgeNpcAsync, on the Ishalgen map, casts the learned Return
      once when the approach has no checked route and the bind is more than 30 m away,
      rests, and approaches again; a second failure ends the run as before. Bridge steps
      on other maps are not touched.
    - Bundle: seven pre-commit checks pass, Aion.GameServer.Tests passes, and Fast passes
      all 11 scenario gates (run cp07a-fast). The logs are in run/cp/CP-07a/checks/.
- [x] **CP-08 - Record baselines p and c, twice.** Depends: CP-04, CP-07, CP-07a
  - Work: No file under src or tests changes. CP-05, CP-06 and CP-07 are ticked, so these
    baselines hold the Priest's new levels 1-9: the help kit and both Ishalgen binds. They
    are recorded once, and rule (j) keeps them. On a clean tree, with nothing else using the
    build outputs, run run-neutral-gate.ps1 -Set p+c -Record -Item CP-08: it plays p and c
    twice each into run/cp/baseline/<sha>/ and keeps the second copy outside the repo, in
    ../BeyondAionSharp-cp-baseline/<sha>/. Commit e2e/natural-neutral-baseline.json: scope,
    pinned environment, snapshot, commit, record count, SHA-256 of the normalized trace and
    the --counts row. If two passes of a scope differ, narrow the ignore list once; if they
    still differ, stop the loop and report. A decision-projection comparer is a weaker proof
    and becomes a lettered item under this one only after the operator is told.
  - Proof: The record run: for p and for c, pass one and pass two are identical after
    normalization (run/cp/CP-08/<run-id>/verdict.json).
  - 2026-10-07, not ticked. Record run rec-a1 at commit f8aa9fd47
    (run/cp/CP-08/rec-a1/verdict.json): verdict fail, and nothing was written.
    - **Scope c repeated.** Its two passes are identical after normalization: 96,166
      records, SHA-256 fce0e6a7c27ad4d85cf42687e4d7b0aacbc77455c1387b61153f8f042a6d35cc,
      2 min 17 s each. That answers hazard 1 for the Cleric: a same-seed replay repeats at
      HEAD. Its counts: 1 death, 25 retreats, 36 retreat routes, 60 powder rest casts, 120
      patrol waits, 11 emergency decisions, 201 pull plans, 102 life potions, 1 mana
      potion, 4 shield scrolls, 1 bind, 1 soul heal. Both traces are kept in
      run/cp/CP-08/rec-a1-c-pass1 and -pass2.
    - **Scope p failed in its first pass** after 53 s, at the walk to Munin for Q2008
      (run/cp/CP-08/rec-a1-p-pass1/). The gate played no second pass. The cause and the
      fix are lettered item CP-07a, which is now in this item's Depends. This is not the
      case this item stops the loop for: no two passes differed.
    - The gate itself worked on its first real run: one build, four journeys, the
      refusal to write a baseline for a set in which one scope failed, and no schema left
      behind.
  - 2026-10-07: done, after CP-07a. Record run rec-a2 at commit 200ec4c24, on a clean
    tree: verdict pass (run/cp/CP-08/rec-a2/verdict.json). For p and for c, pass one and
    pass two are identical after normalization, with an empty ignore list.
    - **p:** 35,811 records, SHA-256
      df5ad770ee208e33705322fd87088b1babdfdd4bdd3d3531a555179ed95b9ed5, 38 s a pass. It
      holds the kit supplied at level 1, both Ishalgen binds, CP-07a's Return to the
      outpost, the class choice and the ceremony. Counts: no death, 2 retreats, 4 retreat
      routes, 5 pull plans, 18 life potions, 7 Running scrolls, 2 speed scrolls, 6 help
      items supplied, 2 binds.
    - **c:** 96,166 records, SHA-256
      fce0e6a7c27ad4d85cf42687e4d7b0aacbc77455c1387b61153f8f042a6d35cc, 2 min 17 s a pass.
      It is the hash rec-a1 had before CP-07a, so that item changed nothing in the Cleric
      leg. Counts: 1 death, 25 retreats, 36 retreat routes, 60 powder rest casts, 120
      patrol waits, 11 emergency decisions, 201 pull plans, 102 life potions, 1 mana
      potion, 4 shield scrolls, 1 bind, 1 soul heal.
    - e2e/natural-neutral-baseline.json holds both rows: scope, replay, snapshot, commit,
      pinned environment, record count, hash and counts. The traces are in
      run/cp/baseline/200ec4c2408dbc58b675daf1bcc4e03c4eabc47a/ and, as the second copy,
      in ../BeyondAionSharp-cp-baseline/ under the same commit (12 MB for p, 61 MB for c).
    - **The baseline sha is 200ec4c24.** From here on the re-record rule applies, the
      guard of rule (c) can run, and scopes p and c are never recorded again without the
      operator's decision. Hazard 1 is answered for both: a same-seed replay repeats at
      HEAD.
    - Evidence only: the seven pre-commit checks pass, no Fast run.
- [x] **CP-09 - Record baselines m, b, l1, hm and ax, twice.** Depends: CP-07, CP-08
  - Work: The same procedure for m, b, l1 (from altgard with -LaterCapital, as altgard-rc-l1
    was captured), hm and ax, in one -Record run with -Item CP-09. Scopes m and b start from
    a fresh Priest, so they hold the new levels 1-9 of CP-06 and CP-07; l1, hm and ax start
    from Cleric snapshots and hold none of it. Scopes p and b are the first runs of CP-06
    and CP-07 in the early-Ascension order, where the level-10 Cleric finishes Ishalgen with
    the binds. If p or b fails at a bind, a hub revive, a Return or a kit step, it is not
    dropped: the fault becomes a lettered item under CP-06 or CP-07. Any other scope that
    does not pass or does not repeat at HEAD is written under Blocked and left out of every
    set; nothing is fixed here. If m is lost, stop the loop and report, because the Ishalgen
    items need it. From the counts rows, write into the doc which scope reaches which code
    (deaths, retreats, sits, vendor buys, patrol waits, emergency decisions), so every later
    item names a scope that runs what it moves. Where the counts show that a later item's
    named scope does not run the code that item moves, edit that item's Proof line to a
    scope that does, and log the edit in the Progress log. This check matters more than it
    did: with the kit the Priest may die, retreat and rest much less than in the older
    traces (hazard 4). The fallbacks already written into the Proof lines (for a dropped b,
    hm or ax) need no edit.
  - Proof: The record run: every scope kept is identical across its two passes
    (run/cp/CP-09/<run-id>/verdict.json).
  - 2026-10-07: done. Record run rec-a1 at commit 5ffbac512, on a clean tree: verdict pass
    (run/cp/CP-09/rec-a1/verdict.json). All five scopes passed and repeated record for
    record with an empty ignore list, so none is dropped and none is under Blocked. Set
    all is the seven scopes.

    | Scope | Records | SHA-256 | A pass |
    |---|---|---|---|
    | m | 123,112 | bd264337d998ffc3fdae5897ea10d8cbeef88558dc6c1d88f674103ece27a196 | 5 min 22 s |
    | b | 128,937 | 7247e6389d7f2bea493ddd2635aa4eb8c262658751e40fcebfd2cf4abecb1bca | 7 min 36 s |
    | l1 | 30,693 | f5dd15e9a3c1c2620feefc574f9ed498df433d60345ca3bc22cb698fceb06074 | 1 min 33 s |
    | hm | 39,564 | aaadd6e3329a728c242aa4b77c1b10483703516b6a5e579c2084bbfc84d961ca | 3 min 53 s |
    | ax | 15,762 | e4d1aa17104f26ea8c4090ec9d0e5266ec6979d59452b2af0a8b30345d2f9db5 | 44 s |

    - The rows of p and c in e2e/natural-neutral-baseline.json are unchanged. The five new
      traces are in run/cp/baseline/5ffbac5120727f265466fc407766e57bc9d154ad/ and in the
      second copy. The rows now carry two commits, 200ec4c24 (p, c) and 5ffbac512 (the
      other five); the baseline sha of the re-record rule is the older, 200ec4c24.
    - Scope b is the first run of the early-Ascension order through the whole bridge with
      the kit and the binds. It passed without a fault at a bind, a hub revive, a Return or
      a kit step: CP-07a's Return fired as in p, the Cleric who came back to Ishalgen cast
      Return twice more and skipped the eastern road once at the outpost, and the Altgard
      shop stop sold and bought.
    - Which scope reaches which code is the table "What each scope reaches" under "Proof
      tools and the neutral gate". The times in the scope table there are the new ones.
    - **The check of every later item's scope against the new counts found nothing to
      edit.** CP-13, CP-15 and CP-38 (p+c): p holds 274 Priest fight decisions and c 1,590
      Cleric ones. CP-16, CP-17, CP-19, CP-37, CP-39 and CP-40 (m+c): m holds the Priest's
      7 emergency decisions, 4 shield scrolls, 15 between-fight heals, 6 walks to a rest
      spot, its death, revive and soul heal; c holds the Cleric's 11 emergency decisions,
      its only mana potion, 179 powder-rest decisions and 120 patrol waits. CP-18
      (m+c+hm): hm was kept and holds the 9 at-target pulls. CP-20, CP-21 and CP-24 (m).
      CP-22 and CP-23 (m+b+c+ax): b holds the only vendor trade and ax the 7 coin-armor
      purchases and 14 equips. CP-26 and CP-27 (p+b+c). The fallbacks written into the
      Proof lines for a dropped b, hm or ax are not used.
    - Three things the counts say that the plan did not expect. The Priest waits for no
      patrol in any scope: every patrol wait is the Cleric's, on the same code. The Priest
      never drinks a mana potion; the Cleric drinks one, in c. And no scope reaches the
      Ishalgen vendor, as before.
    - **A finding in the counts tool, logged and not fixed.** vendorBuys reads 0 for ax,
      but the trace holds 7 coin-armor purchases: the Abyss-entry leg names its record
      coin-armor-purchase, and the counts mode looks for the older legs'
      coin-purchase-receipt. The gate compares the trace, not the counts, so nothing is
      unprotected; the number in the baseline row is low.
    - Evidence only: the seven pre-commit checks pass, no Fast run.
- [x] **CP-10 - A status-5 stop writes its receipt: the Priest stopped at Q2132.** Depends:
  CP-09
  - Work: No code change is expected. Stop boundaries with status 5 have never been used
    (hazard 18), and every class checkpoint and class scope leans on one. Run
    sim-snapshot.ps1 -Action Replay -StopAt 2132:5:0 -Item CP-10 on the default line,
    priest-cleric (seed 1, bridge off, a fresh character, the 45-minute deadline). A
    completed quest reads as status 5 with var 0 (J:553;
    src/Aion.GameServer/Services/QuestService.cs:86-87), so the stop should fire once Q2132
    is turned in, and the stop writes resume-receipt.json (J:875-891). If the boundary does
    not fire, the cause is found and fixed here, before any class run leans on it. The fix
    is one small change to the stop check; no baseline run sets NI08_STOP_AT, so gate p+m is
    then run once as a guard and must stay identical. Two attempts at most, then Blocked.
    This stop has no fallback: -StopAfterQuest exists only for Q2004 to Q2007 (J:141-142).
  - Proof: One contained run under run/cp/CP-10/<run-id>: resume-receipt.json is written,
    and its checkpoint holds 2132 in CompletedQuestIds.
  - 2026-10-07: done on the first attempt, with no code change. Run stop-a1 at commit
    a6fa1f0b4, on a clean tree: sim-snapshot.ps1 -Action Replay -StopAt 2132:5:0 -Item
    CP-10 -Run stop-a1 passed (run/cp/CP-10/stop-a1/replay.json: passed, schema dropped,
    environment NI08_STOP_AT=2132:5:0 and nothing else).
    - **The stop fired and wrote its receipt.** run/cp/CP-10/stop-a1/resume-receipt.json
      exists. Its checkpoint holds CompletedQuestIds 2000, 2001, 2002, 2100, 2101, 2102,
      2103, 2104 and 2132, and Q2132 reads status 5, step 0, complete count 1. The last
      trace records are the turn-in itself: SM_QUEST_ACTION with questId 2132, status 5,
      stepAndFlags 0, at game time 14 min 36 s. The run stopped there and played nothing
      after it.
    - The character at the stop: a Priest of level 6 at Aldelle Village (554, 2404), not
      dead, full HP and MP, bound at the village obelisk (585, 2467), 2,337 Kinah. The
      next quest the planner names is Q2003. Q2003 to Q2006 are in the journal at status
      3 and Q2007 is locked (status 6), as Java's QuestStatus has them (START 3, REWARD
      4, COMPLETE 5, LOCKED 6).
    - The run in numbers: 11,545 trace records, 9 s of test time, 32 s with the schema.
      No death and no retreat; 1 pull plan, 2 between-fight heals, 3 life potions, 2
      Running scrolls, 1 speed scroll, 1 bind (the village), 3 help items supplied at
      run start (help-items.json: 30 Major Life Potions, 30 Lesser Anti-Shock Scrolls,
      20 Greater Running Scrolls).
    - Hazard 18 is answered: a status-5 boundary fires and writes the receipt. CP-44 and
      the class checkpoints of CP-50 on can lean on it. The guard run of gate p+m was
      not needed, because nothing was changed.
    - Evidence only: the seven pre-commit checks pass, no Fast run.

### B. The class seam, with the Priest and Cleric moved onto it unchanged

- [x] **CP-11 - Pin the Priest and Cleric numbers before any code moves.** Depends: CP-00,
  CP-06, CP-07
  - Work: It waits for CP-06 and CP-07, so it pins the numbers as those two items left
    them. Add UT/NaturalClassSeamPinTests with a table of every number the seam will carry,
    each with its source line. The lines quoted below are those of commit b45b72a43; CP-05,
    CP-06 and CP-07 have edited the journey since, so the test records the lines it finds.
    A row is in one of two states. Asserted now: a public constant or a default parameter
    value, which the test can read today. Pending: a private constant or a literal inside a
    method, which a test cannot read. A pending row lists the expected value and the id of
    the one CP item that moves the number behind the profile, its owner. The owner turns the
    row on by pointing it at the profile member, and it may not edit the expected value. No
    pending row is made without an owner, and a number that no item of this list moves gets
    no pending row. Where an owner moves a number whose row already asserts, it adds the
    profile-side check to that row.
    Rows asserted now, each with the item that adds its profile side: swarm 3 and heal 55/70
    (Sc/NaturalPriestCombatPolicy.cs:92-110; they stay inside the static policy behind the
    adapter, so no item moves them); emergency 35/45 (the same lines; CP-16); melee reach 3
    (the same lines; CP-18); pull 22 (Nav/NaturalPullPlanner.cs:35; CP-18); the 23 of
    NaturalFightThrough.FiringRange (Nav/NaturalFightThrough.cs:24; CP-18); the standoff's
    three defaults 25, 3 and 1 (Nav/NaturalCombatStandoff.cs:23; CP-18); restock 5 and 12
    (Sc/NaturalIshalgenPotionPolicy.cs:18-19; CP-24); and
    NaturalMauPolicyParameters.Baseline (no item moves it). Pending rows, each with its
    owner: rest heal below 90 (J:9695), sit below 50 until 80 (J:9650-9651) and 12 quiet
    sits (J:9721), owner CP-17; the 20 of the router's private RangedRadius
    (Nav/NavMesh/BotNavMeshRouter.cs:30) and of the grid fallback's arrivalRadius
    (Nav/BotNavigationGeometry.cs:217), the journey's 23 m and 30 m literals
    (J:5217-5274, 5976) and the readiness thresholds of the shared helpers (J:5457-5458,
    5631, 8232-8233, 8269-8277), owner CP-18; the fight loop's 25 m route threshold (J:9284)
    and its 10 m close-in (J:10040-10041), owner CP-19; the campaign's 22, 23 and 25 m
    literals (J:6433-6510, 6794, 6896-6913, 7093, 7127) and its readiness thresholds
    (J:6238, 7268, 7287, 7393, 7608-7610), owner CP-21. The 20 m rows stay because the code
    holds those two literals. There is no 21 m row: a search of tests/Aion.Bots on
    2026-10-06 found no stored 21, and the standoff's 21 m is derived from its three
    defaults (25 - 3 - 1). The later-leg sites that CP-18 leaves alone get no row. Rows for
    what CP-06 set, so the seam cannot lose it: asserted now, the help policy's public
    numbers (the 20 s refresh window, the 150 m long leg and the shield scroll at 50% HP,
    Sc/NaturalHelpItemPolicy.cs:44-46; no item moves them); pending, owner CP-16, the four
    help-item gates as CP-06 left them (the supply, the shield scroll, the mana potion and
    the scroll upkeep: on for the Priest at levels 1-9 and for the Cleric).
  - Proof: Unit test UT/NaturalClassSeamPinTests: green on the bot code as CP-07 left it,
    with the pending rows printed by name, each with its owner.
  - 2026-10-07: done. UT/NaturalClassSeamPinTests.cs is added and no other code file is
    touched. Its three tests pass on the bot code as CP-07a left it, at 68d4903f9
    (run/cp/CP-11/pin-a4.log, which prints every row). The table has 58 rows: 23 asserted
    and 35 pending.
    - **Asserted now (23).** No item moves them: swarm 3, heal 55 and 70, the Mau policy
      baseline (22 m, 3 patrol waits, 55, 70, potion at 90, reserve 0, finish at 15, no
      wounded preference), and what CP-06 set (last kit level 9, the 20 s refresh window,
      the 150 m long leg, the shield scroll at 50% HP). Profile side by CP-16: emergency
      35 and 45, and the enter and exit functions at their four corners (35 and 45; 55
      and 65 against a Seasoned target with two attackers). Profile side by CP-18: melee
      reach 3, pull 22, FiringRange 23, the standoff's defaults 25, 3 and 1 (read from
      the default parameter values). Profile side by CP-24: restock 5 and 12.
    - **Pending (35), by owner.** CP-16, 4: the four help-item gates. CP-17, 4: rest heal
      below 90, sit below 50 until 80, 12 quiet sits. CP-18, 14: the router's and the
      grid fallback's 20 m, the 23, 23, 23 and 30 of ApproachShippedCombatSpawnAsync,
      PullRange 30, and the HP and MP thresholds of ClearAroundSpotAsync (60, 40),
      MoveToPullSpotAsync (80) and PullAndKillAsync (60 and 40, twice each; 80 and 60).
      CP-19, 2: the 25 m route threshold and the 10 m close-in. CP-21, 11: the Sprigg
      hunt's 25, 22 and 25, Q2005's 25, 23 and 25, and the readiness thresholds (Return
      wait 0.75, Q2005 at 90 twice, Q2006 at 80, Q2007 at 80).
    - **A pending row is checked, not only listed.** Each one names the method and the
      source text that hold its literal today. The test searches tests/Aion.Bots for
      them, prints the file and line it finds, and fails when a literal is gone. So the
      lines in the log are those of HEAD, not of b45b72a43, and they follow a pure move
      such as CP-13 by themselves (the journey's rows search NaturalIshalgenJourney*.cs).
      An owner turns its row on in the commit that moves the literal: it sets the row's
      Profile reader and removes the sites. It does not edit Expected. For CP-41 a row is
      pending while it has neither an Actual nor a Profile reader.
    - The present lines, for the items that quote the old ones: rest rule J:9770-9771,
      9815, 9841; gates J:1521, 9243, 9246, 9716, with the rule itself at J:9685; shared
      helpers J:5319-5362, 5549-5550, 5723, 6068, 8345-8346, 8382-8390; fight loop
      J:9398, 10161; campaign J:6351, 6573-6588, 7021, 7206, 7240, 7381, 7400, 7506,
      7721. The journey is 10,452 lines long now, so every J: line this plan quotes from
      b45b72a43 has moved down by about 90 to 120 lines.
    - **Four things found while mapping the lines, written here and not fixed.** (1) Old
      J:6794, which CP-21 lists among Q2004's range literals, holds no literal: it is the
      call of ApproachShippedCombatSpawnAsync, whose 23 and 30 are CP-18's rows. It gets
      no row, and CP-21 has nothing to move there. (2) Old J:5631 is inside
      MoveToPullSpotAsync, not the patrol wait before it, and the fight loop of old J:9284
      is TryKillCoreAsync; the rows carry these names. (3) CP-06 left three more tests of
      level 9 or below outside the four gates: the town stock check (J:537), the loop's
      stock check (J:760) and the travel-leg scroll (J:4916). They test the level only,
      so every class line passes them, no item of this list moves them, and they get no
      row; the kit's last level, 9, is an asserted row. (4) The 55 of the Seasoned
      emergency is a literal inside EmergencyEnterPercent; it is pinned through the
      function's result.
    - Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,698 with 16 skipped), and
      Fast passes (run cp11-fast). No guard run: the commit edits neither tests/Aion.Bots
      nor the journey test, and rule (c) starts at CP-13.
- [x] **CP-12 - Golden gear test, committed on unchanged code.** Depends: CP-09, CP-11
  - Work: Write UT/NaturalGearGoldenTests and its golden file from the present code and
    commit nothing else: for the Priest at levels 1-9 and the Cleric at 9-26, ChooseReward
    for every quest with a selectable list; Decide over a fixed inventory of every reward
    item plus every item id carried in the baseline traces of m, c and ax, the help kit's
    items among them; and NaturalGearPolicy.SelectUpgrades through
    NaturalInventoryCheck.Describe. It sits here, before the first refactor item that edits
    bot code, so that the golden file is the behavior the baselines recorded.
  - Proof: Unit test UT/NaturalGearGoldenTests: green in a commit that holds only the test
    and its golden file.
  - 2026-10-07: done. UT/NaturalGearGoldenTests.cs and its golden file
    parity-artifacts/e2e/natural-gear-golden.txt are added on the bot code of 691663354;
    the commit holds those two files and this plan, and no bot code. The file was written
    once from the code (CP_GEAR_GOLDEN_WRITE=1), a second build of the text was identical
    byte for byte, and the test then passed reading it back (run/cp/CP-12/golden-a1.log).
    The file has 3,351 lines, 410,610 bytes, SHA-256 017672077496b7d1738ff5070614e586c4fd486ce631391fe0c1d44679a585f5.
    - **The fixed inventory:** 2,207 item ids, one stack of each, nothing worn: every
      selectable reward of the 765 quests the policy holds a list for (all 41 Ishalgen
      contract quests, every other Asmodian quest with a choice list, the ceremony's Priest
      list, and Q2008, Q2904 and Q24010, whose lists are empty) and the 260 item ids of
      the baseline traces of m, c and ax, the help kit's six among them. The 260 ids are
      typed into the test, because the traces are not in the repository. Every id has a
      shipped template.
    - **What the file holds.** For each item: the facts the policy reads (group, both class
      levels, race, quality, mask, item level, both slots, both scores) and its decision
      letter at every level, Priest 1-9 and Cleric 9-26, once alone in the cube and once
      in the whole inventory. For the Cleric, the letters that the coin-gear rules (108
      items) and the Haramel rules (106 items) change. For each of the 765 quests,
      ChooseReward at levels 1-26 with an empty cube and with the baseline items owned.
      SelectUpgrades through NaturalInventoryCheck.Describe at every level, over the whole
      inventory and over the baseline items, with nothing worn; then level after level over
      the baseline items with each upgrade worn before the next, and Decide over that worn
      state.
    - **Three things the item did not ask for, added because the giant inventory alone
      would miss them.** In one inventory of 2,207 items only the best item of a slot is
      ever equipped, so a change to a middle item's level test or score would not show:
      the per-item facts and the alone letters cover that. The item named no Decide call
      with the coin-gear or Haramel rules, which CP-22 also folds into one path: the two
      variant sections cover them. And nothing in a fixed unworn inventory reaches the
      currently-equipped branch or a replacing upgrade: the level-after-level section does.
    - **What the golden file shows about the code, written here and not changed.** (1)
      ChooseReward is class-blind and scores by the Priest's level test, which stops at
      level 9: of the 765 quests, 669 pick their first choice at every level with an empty
      cube and 34 have no list. (2) Most coin and event items carry a class level of 1, so
      SelectUpgrades offers a level-1 Priest the level-21 coin staff and level-26 armor
      when they are in the cube; in play the cube never holds them that early, and the
      server refuses a weapon without its mastery. (3) No letter for an unknown item, a
      multi-slot item or a quest-needed item occurs: the first two have no item in the
      inventory, and the third needs the journey's open-quest list.
    - Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,699 with 16 skipped), and
      Fast passes (run cp12-fast). No guard run: the commit edits neither tests/Aion.Bots
      nor the journey test.
- [x] **CP-13 - Lift the combat and navigator classes into their own files (pure move).**
  Depends: CP-08, CP-11
  - Work: Make NaturalIshalgenJourney partial (J:19). Cut NaturalJourneyNavigator
    (J:8496-8852) into Sc/NaturalIshalgenJourney.Navigator.cs and NaturalJourneyCombat
    (J:8853-10115) into Sc/NaturalIshalgenJourney.Combat.cs, still private nested classes,
    so the exception at J:29 and the statics Distance, ItemCount and
    PriorityForEngagedTarget (J:10249-10305) need no accessibility change. No rename, no
    reordering, no fix. NaturalIshalgenJourney.cs keeps its path.
  - Proof: Neutral gate, set p+c: both traces are identical to their CP-08 baselines.
  - 2026-10-07: done. Gate run gate-a1 on the moved code: verdict pass
    (run/cp/CP-13/gate-a1/verdict.json). p: 35,811 records, df5ad770..., and c: 96,166
    records, fce0e6a7..., each identical to its baseline of 200ec4c24.
    - NaturalIshalgenJourney is partial. NaturalJourneyNavigator (J:8609-8964 at
      fd37eddd0, 356 lines) is in Sc/NaturalIshalgenJourney.Navigator.cs and
      NaturalJourneyCombat (J:8966-10235, 1,270 lines) is in
      Sc/NaturalIshalgenJourney.Combat.cs, both still private nested classes.
      NaturalIshalgenJourney.cs keeps its path and is 8,824 lines long. The move was made
      by a script that checks that every line of the old file is in exactly one of the
      three files, in its old order and byte for byte; the only other change is the word
      partial on J:19. The two new files carry the old file's using lines unchanged.
    - **Every J: line this plan quotes above 8608 has changed its file.** A line of the
      navigator is now at its old number minus 8,588 in the Navigator file, and a line of
      the combat class at its old number minus 8,945 in the Combat file, both counted from
      fd37eddd0. Lines after the combat class (the statics Distance, ItemCount,
      PriorityForEngagedTarget and the loot helpers) are at their old number minus 1,628
      in NaturalIshalgenJourney.cs. Lines up to 8608 did not move.
    - The pin test of CP-11 needed no edit and found its literals in the new file: the
      rest rule, the fight loop's 25 m, the 10 m close-in, three of the four help-item
      gates and the gate's rule are now reported in NaturalIshalgenJourney.Combat.cs.
    - Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,699 with 16 skipped), and
      Fast passes (run cp13-fast). The guard of rule (c), gate p, is part of this item's
      proof run.
- [x] **CP-14 - Seam types, with the Priest and the Cleric as the first two profiles.**
  Depends: CP-11
  - Work: Add, all public: Sc/Classes/NaturalClassLine.cs (the record, the table of lines
    with priest-cleric as its first and default entry, Parse of CP_CLASS, which refuses an
    id the table does not hold; each later profile item adds its line to this table),
    NaturalClassProfile.cs with NaturalClassProfiles.For(observed class id, line)
    (unobserved gives the line's starter; a class outside the line throws),
    INaturalCombatPolicy.cs (Decide and CandidateActions, both taking the run's
    NaturalMauPolicyParameters, and PolicyVersion) and NaturalPriestProfile.cs: the Priest
    and Cleric profiles built from NaturalPriestSkills.All, NaturalClericSkills.All and
    Excluded, with an adapter that calls the static NaturalPriestCombatPolicy unchanged.
    Nothing calls the new types and no existing file changes.
  - Proof: Unit test UT/NaturalClassProfileTests: the 576-state sweep of
    UT/NaturalClericCombatPolicyTests.cs:355-376 through both adapters returns the same
    action, skill, reason and checks from Decide and the same candidate list from
    CandidateActions as the static policy; For gives the Priest profile for PRIEST and for
    unobserved, the Cleric profile for CLERIC, and throws for the other fifteen class ids.
  - 2026-10-07: done. Four files are added under Sc/Classes and no existing file changes;
    nothing calls the new types yet. UT/NaturalClassProfileTests: 6 tests pass
    (run/cp/CP-14/profile-a1.log).
    - **NaturalClassLine.cs:** the record (Id, Starter, Second, SimAccountId,
      CharacterName), the table All with priest-cleric (PRIEST, CLERIC, account 41,
      Asimnjour) as its first and default entry, the constant CP_CLASS, Parse and Holds.
      Parse gives the default for no id and refuses an id the table does not hold, with a
      message that names it and lists the known ids.
    - **INaturalCombatPolicy.cs:** PolicyVersion(parameters), Decide(state, now,
      parameters) and CandidateActions(state, now, chosen, parameters). PolicyVersion takes
      the run's parameters because today's version is the run's mauPolicy.Id.
    - **NaturalClassProfile.cs:** the profile (Class, Skills, Excluded, Combat) as a class
      with required init members, so that later items add members without touching the
      profiles already written, and NaturalClassProfiles.For(observed class id, line).
      An unobserved class gives the line's starter. For refuses a class id that is no
      player class, a class outside the line, and a class of the line that has no profile
      yet, each with a message that names the class.
    - **NaturalPriestProfile.cs:** the Priest profile (NaturalPriestSkills.All, nothing
      excluded) and the Cleric profile (NaturalClericSkills.All and Excluded). Skills is
      the same array object the journey reads today through NaturalClericSkills.ForClass,
      which the test asserts. Both use one private adapter that calls the static
      NaturalPriestCombatPolicy with the class's catalog and the run's parameters.
    - **The proof.** The 576 states of UT/NaturalClericCombatPolicyTests go through both
      adapters and the static policy: the same action, skill, target, reason and checks
      from Decide, and the same candidates field for field from CandidateActions. Beyond
      the item's text, the same sweep runs for a level-10 Cleric with its first eight
      Cleric skills learned, and with parameters other than the baseline (pull 20 m, heal
      60 and 75, finish at 10), so the parameters are shown to reach the static policy.
      For gives the Priest profile for PRIEST and for unobserved, the Cleric profile for
      CLERIC, and throws for the other fifteen classes and for id 200. Every line of the
      table names a starter and a second class that the class-line contract holds.
    - No Java was read: the item relies on no server behavior. The class ids come from
      PlayerClass.
    - Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,705 with 16 skipped),
      Fast passes (run cp14-fast), and the guard of rule (c) passes: gate p, run guard-a1,
      35,811 records identical to the baseline (run/cp/CP-14/guard-a1/verdict.json).
- [x] **CP-15 - Thread the line and the profile through the host and the fight loop.**
  Depends: CP-13, CP-14
  - Work: Add NaturalJourneyOptions.ClassLine as the last optional parameter
    (Sc/NaturalJourneyRuntime.cs:85-91); the journey passes it to the combat constructor at
    its four sites (J:107, 373, 2382, 4550); the session interface is not touched. The SIM
    session's own relog check still classifies with the Priest line after this item
    (SimT/SimulationFastScenarioTests.cs:1300); CP-27 makes it line-aware.
    SimT/SimulationNaturalIshalgenJourneyTests.cs reads CP_CLASS and takes account, name,
    created class, the resume name check, the class assertion and the creation step label
    from the line (lines 44, 55, 111, 118, 124, 129, 136); the default line gives the same
    bytes. In the combat class add ClassProfile, re-read from the observed class on every
    use; Catalog (J:8947), the Decide and CandidateActions calls (J:9152-9155) and the
    policyVersion fields (J:9165, 5689) go through it. The equipment fallback (J:423-426)
    uses the line's starter. The natural-run-context record gains the line id only when it
    is not the default. Add CP_CLASS to the cleared-variable lists in
    sim-snapshot.ps1:88-92, run-natural-complete.ps1:17-25 and run-natural-resume.ps1:17-32.
  - Proof: Neutral gate, set p+c. Set p alone stops right after the ceremony and holds no
    Cleric fight, so it cannot show the Cleric catalog still resolves.
  - 2026-10-07: done. Gate run gate-a1: verdict pass (run/cp/CP-15/gate-a1/verdict.json).
    p: 35,811 records and c: 96,166 records, each identical to its baseline, so the Priest's
    274 fight decisions and the Cleric's 1,590 came through the profile unchanged.
    - **The carrier.** NaturalJourneyOptions.ClassLine is the last optional parameter, null
      by default; the journey reads it as its ClassLine, the accepted line when the caller
      names none. The session interface is not touched, and tools/Aion.LiveBots builds
      unchanged.
    - **The combat class** takes the line as a constructor parameter after the policy
      parameters, at all four sites. Its new ClassProfile reads the observed class on every
      use. Catalog is the profile's skill table, the fight loop asks the profile's policy
      for Decide and CandidateActions, and both policyVersion fields (the combat decision
      and the pull plan) ask the policy too. The equipment check's fallback class is the
      line's starter.
    - **The run context** gains the key classLine only when the line is not the default.
    - **The journey test** reads CP_CLASS through NaturalClassLine.Parse and takes the
      trace account, the SIM account, the name, the created class, the resume name check,
      the class assertion and both step labels from the line. For the default line every
      one of them has the bytes it had. The SIM session's relog check still classifies
      with the Priest line; CP-27 owns that.
    - **Scripts:** CP_CLASS is cleared with the other variables in sim-snapshot.ps1,
      run-natural-complete.ps1 and run-natural-resume.ps1, and test-sim-snapshot.ps1
      asserts that a Replay's journey does not inherit it and that the caller gets it back.
    - **One difference in what is refused, written here because no baseline run meets
      it.** Before this item an observed class that was neither Priest nor Cleric played
      with the Priest's catalog; now NaturalClassProfiles.For refuses it by name. That is
      what the seam asks for (section 2). It matters to one caller outside the gates: the
      LIVE attach scenario, which would now stop on a character of another class instead
      of fighting it with Priest skills.
    - Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,705 with 16 skipped),
      test-sim-snapshot.ps1 and test_compare_traces.py pass, and Fast passes (run
      cp15-fast). The guard of rule (c), gate p, is part of this item's proof run.
- [x] **CP-16 - Sustain, upkeep, help-item and patrol reads from the profile.** Depends:
  CP-09, CP-15
  - Work: Replace class tests and direct table reads in the fight-loop inputs with profile
    members that return today's values: emergency enter and exit (J:9123-9124), shield
    scroll (J:9130), mana potion (J:9132), the blessing and rejuvenation ids (J:9064-9065,
    9125, 9148), the patrol readiness record (J:5783, 5790), the scroll part of
    BuffOurselfAsync (J:9596), the help-item supply gate (J:1505), the patrol rule choice
    (J:5636-5637, 5797; traced field names stay) and conservativeRangedHold as a pull rule
    that options.OptimizeHubs still decides for the Priest line. MaintainBuffsAsync
    (J:9543-9563) walks the profile's upkeep list; the Priest's list has one entry and
    traces the same bytes. CP-06 opened four of these sites to the Priest: the supply gate,
    the shield scroll, the mana potion and the scroll part of BuffOurselfAsync. The profile
    members return what CP-06 left, on for the Priest at levels 1-9 and for the Cleric, and
    the profile names its help kit by level band from the allowlist. A profile outside the
    Priest line gets the Priest's patrol baseline unless question 14 says otherwise. The
    scroll for the shared speed slot becomes an input of NaturalHelpItemPolicy.DecideBuffs
    (Sc/NaturalHelpItemPolicy.cs:84-98), with today's family as its default, so the Priest
    and Cleric stay neutral and the existing assertion that Blitzopan is never used
    (UT/NaturalHelpItemPolicyTests.cs:176) stays true for them; add a unit row for a profile
    that names the attack-speed scroll. Turn on the matching pin rows (emergency 35/45 and
    the four help-item gates).
  - Proof: Neutral gate, set m+c. Set m holds the emergency decisions and rest relocations
    that set p lacks.
  - 2026-10-07: done. Gate run gate-a1, set p+m+c (the proof set and the guard in one
    run): verdict pass (run/cp/CP-16/gate-a1/verdict.json). p: 35,811 records, m: 123,112
    and c: 96,166, each identical to its baseline.
    - **On the combat policy** (INaturalCombatPolicy): EmergencyEnterPercent and
      EmergencyExitPercent. The adapter calls the static policy, and the fight loop asks
      the observed class's policy on every turn.
    - **On the profile:** HelpItems (the kit as rows of the allowlist, the last level it
      applies to, the scroll of the shared slot, and the four gates Supplied,
      ShieldScroll, ManaPotion and ScrollUpkeep), Upkeep (the buffs kept up between
      fights), EffectIds(role), PatrolRule and RangedHold. The Priest has the level 1-9
      kit and no help items from level 10 on; the Cleric has every approved row at every
      level; both keep Awakening, hold one upkeep buff (blessing, traced as
      buff-blessing) and leave the ranged hold to the run's option. The Priest's patrol
      rule is the baseline, the Cleric's is NA-22's hold and assess.
    - **Sites moved** (the present lines): in Sc/NaturalIshalgenJourney.Combat.cs the
      emergency band, the blessing and rejuvenation effect ids, the shield scroll, the
      mana potion and the ranged hold of the fight loop, MaintainBuffsAsync, which walks
      the upkeep list, and the scroll part of BuffOurselfAsync; in
      NaturalIshalgenJourney.cs the supply gate with the kit it plans from, the patrol
      rule choice, and the patrol readiness record (its skill table, its buff check and
      its first field, whose traced name Cleric stays). UsesHelpItems is gone; IsCleric
      stays for the leg gates of section 6.
    - **Two public signatures gained an optional parameter,** each defaulting to what it
      did: NaturalHelpItemPolicy.DecideBuffs takes the shared slot's scroll (awakening
      by default, courage for an attack-speed class) and NaturalHelpItemSupply.Plan takes
      a kit (every approved row by default). For the default, DecideBuffs gives the same
      rule names and texts as before, asserted in UT/NaturalHelpItemPolicyTests with the
      new row for a profile that names Courage: it keeps Courage up, picks Blitzopan, and
      never swaps an active Awakening. The older assertion that Blitzopan is never used
      still holds for the default.
    - **Pin rows turned on:** the seven emergency rows now also read the profile, and the
      four help-item gates are no longer pending. The pin test has 27 rows asserted and
      31 pending (CP-17 4, CP-18 14, CP-19 2, CP-21 11).
    - **One input the Priest profile reads differently, with the same answer.** The fight
      loop's heal-over-time check used the Cleric's three rejuvenation ids for every
      class. The Priest profile's table has none, so a Priest no longer recognises a
      rejuvenation effect on itself. A Priest cannot cast one and plays alone, so the
      observation is false both ways, and scopes p and m are identical.
    - Question 14 (the patrol rule of a line outside the Priest line) has no answer, so
      its default stands: such a profile will take the baseline rule. No profile outside
      the line exists yet.
    - No Java was read: the item relies on no server behavior it did not already rely on.
    - Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,710 with 16 skipped),
      and Fast passes (run cp16-fast). The guard of rule (c), gate p, is in the proof run.
- [x] **CP-17 - Between-fight recovery as a rest plan.** Depends: CP-16
  - Work: Extract the decisions of RestAsync (J:9634-9760) into a pure
    NaturalRestRules.Decide(observation) on the profile that returns powder, cast-heal,
    sit-for-mana, done or blocked, with a state-sweep unit test written against J:9650-9651,
    9695 and 9721. The executor stays in the core: casting, NaturalRestCadence, defending
    during a rest, revive, BuffOurselfAsync(AfterRest) and the MaintainInventoryAsync hook.
    The Priest and Cleric plan is today's rule exactly, with the same two exception texts.
    Turn on the matching pin rows (rest heal below 90, sit below 50 until 80, 12 quiet
    sits).
  - Proof: Neutral gate, set m+c.
  - 2026-10-07: done. Gate run gate-a1, set p+m+c (the proof set and the guard in one
    run): verdict pass (run/cp/CP-17/gate-a1/verdict.json). p: 35,811 records, m: 123,112
    and c: 96,166, each identical to its baseline. Scope m holds the Priest's 15
    between-fight heals and 6 walks to a rest spot, and c the Cleric's 179 powder-rest
    decisions and 4 interrupted rests.
    - **Sc/Classes/NaturalRestRules.cs** is new: the record (the skill catalog,
      HealBelowPercent 90, ManaSitBelowPercent 50, ManaSitUntilPercent 80,
      MaximumQuietSits 12) with the pure Decide(observation). It returns powder,
      cast-heal, sit-for-mana, done or blocked, the skill to cast, the mana sit's state
      after the observation, whether the sit just ended, the powder policy's own choice
      for the trace, and the reason when blocked. The two exception texts are constants
      of the record, unchanged. The profile's new member Rest holds the rules; the
      Priest's and the Cleric's differ only in the catalog.
    - **RestAsync** (Sc/NaturalIshalgenJourney.Combat.cs) observes, asks the profile's
      rules, and carries the answer out. The executor is as it was: the powder cast and
      its interruption, the heal's cast gate, NaturalRestCadence, defending during a
      rest, the revive, BuffOurselfAsync(AfterRest) and the MaintainInventoryAsync hook.
      The powder-rest-decision record is traced before the action, as before.
    - **UT/NaturalRestRulesTests**, 3 tests: a sweep of 49,152 states (four characters,
      HP and MP at every threshold and one below it, the sit on and off, 0, 11, 12 and
      13 quiet sits, four powder counts, the shared cooldown, the last powder skill)
      against a transcription of the old inline decisions with their literals; all five
      actions occur. Two tests name the thresholds one by one for the Priest and the
      powder-first order for the Cleric.
    - **Pin rows turned on:** rest heal below 90, sit below 50, until 80, and 12 quiet
      sits read the profile. The pin test has 31 rows asserted and 27 pending (CP-18 14,
      CP-19 2, CP-21 11).
    - Carried over, not changed: the rest sit still writes no trace record of its own
      (the finding of CP-06), so the gate sees a sit only through its packets and the
      walk to the rest spot.
    - No Java was read: the item relies on no server behavior it did not already rely on.
    - Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,713 with 16 skipped),
      and Fast passes (run cp17-fast). The guard of rule (c), gate p, is in the proof run.
- [x] **CP-18 - Engage ranges and readiness thresholds in the shared helpers.** Depends:
  CP-17
  - Work: Give the profile named distances and named thresholds and have the shared helpers
    ask it: the NaturalPullPlanner.SpellRange uses at J:5510, 5956 and 8099, the 23 m and 30
    m literals at J:5217-5274 and 5976 (the label priest-spell-range-target keeps its
    bytes), the pull distance at J:5655-5674 (the Priest line still passes
    mauPolicy.PullDistanceMeters), and the thresholds at J:5457-5458, 5631, 8232-8233 and
    8269-8277. None of the three navigation helpers can be passed a range from the journey
    today. So add an optional range parameter, defaulting to today's number, to
    NaturalFightThrough.SelectNext (23, today the public constant FiringRange,
    Nav/NaturalFightThrough.cs:24), NaturalCombatStandoff.NextSegment (25, forwarded to
    Select, Nav/NaturalCombatStandoff.cs:11-23) and
    BotNavigationGeometry.FindRangedApproachPath (20, forwarded to the router and to the
    grid fallback's arrivalRadius, Nav/BotNavigationGeometry.cs:137-140, 214-217); the
    journey passes the profile's values. These are public signatures, so each addition must
    be optional. Move MeleeReach to shared combat geometry and keep the old constant as an
    alias. Every site keeps its present number; 20, 22, 23, 25 and 30 are not folded
    together, and the standoff's 21 m is derived (25 - 3 - 1), not stored, so its three
    inputs keep their values. Later-leg sites are left alone, among them J:1947-1956 (the
    Q2947 arena), 2457, 2894, 3058, 3073, 3391, 3755 and 4192. Turn on the matching pin rows
    (the two 20 m literals, the journey's 23 m and 30 m literals, the shared helpers'
    readiness thresholds, and the profile side of melee reach 3, pull 22, the 23 of
    FiringRange and the standoff's 25, 3 and 1).
  - Proof: Neutral gate, set m+c+hm. Scope hm is the only one that reaches the
    fight-at-the-target block of PullAndKillAsync (J:8200-8244), where this item changes the
    MeleeReach read (J:8206) and the thresholds at J:8232-8233. If CP-09 dropped hm, set
    m+c, and the doc says that this block then rests on the pin test only.
  - 2026-10-07: done. Gate run gate-a1, set p+m+c+hm (the proof set and the guard in one
    run): verdict pass (run/cp/CP-18/gate-a1/verdict.json). p: 35,811 records, m: 123,112,
    c: 96,166 and hm: 39,564, each identical to its baseline. Scope hm holds the 9
    at-target pulls of the fight-at-the-target block.
    - **Sc/Classes/NaturalEngageRules.cs** is new. NaturalEngageRanges names eleven
      distances: MeleeReach 3, SpellRange 22, PullDistance (none: the run's parameter),
      FiringRange 23, SpawnApproachRange 23, SpawnPullScanRange 30, FightThroughPullRange
      30, the standoff's 25, 3 and 1, and RangedApproachRadius 20. Two that are equal are
      still two values, and no 21 is stored. NaturalReadinessThresholds names four pairs
      of HP and MP percentages: BeforePull 80, BeforeUseBar 60/40, BetweenAdds 60/40 and
      BeforeNamedTarget 80/60, each with the integer comparison the helpers wrote inline.
      The profile gains Ranges, Readiness and PullDistance(run); the Priest and the Cleric
      share one set of each.
    - **The three navigation helpers** took an optional parameter each, defaulting to
      their number: NaturalFightThrough.SelectNext (firingRange, 23),
      NaturalCombatStandoff.NextSegment (spellRange 25, arrivalTolerance 3 and
      safetyMargin 1, all forwarded to Select; the item named only the first, and the
      profile could not pass the other two without them) and
      BotNavigationGeometry.FindRangedApproachPath (range, 20, forwarded to the router
      and to GridRangedApproachPath). The journey passes the profile's values; the
      navigator's FindRangedApproach takes the range from the fight loop.
    - **MeleeReach** is Nav/NaturalCombatGeometry.MeleeReach now, and
      NaturalPriestCombatPolicy.MeleeReach is an alias of it.
    - **Sites moved in the journey:** the three SpellRange uses of the shared helpers (the
      engaged-attacker check, the fight-through's out-of-reach test and PullAndKillAsync's
      approach stop), the four literals of ApproachShippedCombatSpawnAsync with its
      refusal text, the fight-through's PullRange, the pull distance handed to the
      planner, both MeleeReach reads of PullAndKillAsync, and the five readiness tests.
      The label priest-spell-range-target is untouched.
    - **Left alone, as the item says:** the later-leg sites (the four SpellRange uses of
      the Q2947 arena and five more in the Altgard legs), and the fight loop's own four
      MeleeReach reads, of which CP-19 moves three.
    - **Pin rows turned on:** all fourteen of CP-18, and the profile side of melee reach,
      pull 22, FiringRange and the standoff's three. The two 20 m rows also read the
      helpers' new default parameters. The pin test has 45 rows asserted and 13 pending
      (CP-19 2, CP-21 11).
    - Unit tests added: the firing range, the standoff inputs and the ranged approach
      range each change the helper's answer when given and leave it as it was when not;
      the readiness comparison equals the inline one over 252 states.
    - No Java was read: the item relies on no server behavior it did not already rely on.
    - Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,717 with 16 skipped),
      and Fast passes (run cp18-fast). The guard of rule (c), gate p, is in the proof run.
- [x] **CP-19 - Movement inside a fight, by pull style.** Depends: CP-18
  - Work: Add a pure helper the fight loop asks, with a unit test: where an approach stops
    (today a ranged route beyond 25 m, then ApproachNpcAsync up to the target, J:9283-9304),
    what counts as adjacent (J:9121-9122), how far to close in after NOT_ENOUGH_DISTANCE
    (today melee reach minus 1 for a skill with range 3 or less, else 10 m, J:10040-10041),
    and the answer to STR_SKILL_OBSTACLE (today close to melee, J:10046-10060). The Priest
    and Cleric profile returns today's numbers. A ranged style will stop at its hold
    distance and look for another sight line; a walk-in style will close to weapon reach;
    neither is used yet. This item also gives the profile its pull style (stand-off,
    weapon-range stand-off or walk-in), which CP-36 and CP-40 read. Turn on the matching pin
    rows (the 25 m route threshold and the 10 m close-in).
  - Proof: Neutral gate, set m+c.
  - 2026-10-07: done. Gate run gate-a1, set p+m+c (the proof set and the guard in one
    run): verdict pass (run/cp/CP-19/gate-a1/verdict.json). p: 35,811 records, m: 123,112
    and c: 96,166, each identical to its baseline.
    - **Sc/Classes/NaturalFightMovement.cs** is new: the pull style (StandOff,
      WeaponRangeStandOff, WalkIn) and the pure record the fight loop asks. Approach
      (distance) says ranged route, walk to the target or hold; Adjacent(distance, target
      ranged, time since its last hit) is the old test; CloseInAfterRangeRefusal(skill
      range) gives melee reach less 1 for a melee skill and 10 m otherwise;
      AfterObstacleRefusal says close to melee or find another sight line. The profile
      gains Movement and PullStyle. The Priest and the Cleric share one stand-off: melee
      reach 3, ranged route beyond 25 m, close-in 10 m.
    - **The fight loop** (Sc/NaturalIshalgenJourney.Combat.cs) asks it at four places: the
      adjacency test, the approach step, the close-in after STR_SKILL_NOT_ENOUGH_DISTANCE
      and the answer to STR_SKILL_OBSTACLE. Its executors are unchanged, and so is the
      bound of two obstacle repositions, which stays with the cast bookkeeping.
    - **The two other styles are defined and not played.** A walk-in style always walks
      to the target; a weapon-range style holds inside its hold distance and answers an
      obstacle with another sight line. No profile returns Hold or another sight line
      yet, and the fight loop has no executor for either: it throws NotSupportedException
      naming CP-36 if one is ever asked for, instead of guessing a movement.
    - **UT/NaturalFightMovementTests**, 2 tests: the Priest's and the Cleric's answers
      equal the old inline expressions over twelve distances, both target kinds and six
      hit ages, and over seven skill ranges; the other two styles answer as written above.
    - **Pin rows turned on:** the 25 m route threshold and the 10 m close-in. The pin test
      has 47 rows asserted and 11 pending, all CP-21's.
    - One MeleeReach read is left in the fight loop: a target counts as ranged when its
      attack range is above melee reach plus 1 (Combat.cs:234). No item names it, so it
      still reads the shared constant through the old alias.
    - No Java was read: the item relies on no server behavior it did not already rely on.
    - Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,719 with 16 skipped),
      and Fast passes (run cp19-fast). The guard of rule (c), gate p, is in the proof run.
- [x] **CP-20 - Q2132, the trainer and class names from the line.** Depends: CP-01, CP-19
  - Work: Q2132 (J:6689-6697) takes its var, trainer and step label from the class-line
    contract (Priest: var 4, npc 203530, the same label bytes). The navigation graph NPC
    list (J:311-320) adds the line's trainer. Step and message strings on the 1-9 path that
    name the class take the name from the line, byte-identical for the Priest; the parsed
    trace keys pull-plan and defend-before-pull are not renamed.
  - Proof: Neutral gate, set m.
  - 2026-10-07: done. Gate run gate-a1, set p+m (the proof set and the guard in one run):
    verdict pass (run/cp/CP-20/gate-a1/verdict.json). p: 35,811 records and m: 123,112,
    each identical to its baseline, so every step label of the 41 quests kept its bytes.
    - **Java read first:** _2132ANewSkill sets the var by the starter class at the level
      change (Warrior 1, Scout 2, Mage 3, Priest 4, Engineer 5, Artist 6) and answers
      only that class's trainer (203527 to 203530, 801218, 801219). The class-line
      contract of CP-01 holds both for every starter.
    - **Q2132** reads its var and its trainer from the contract's row for the line's
      starter, loaded once at the start of the run, and the navigation graph's NPC list
      names that trainer where it named 203530. For the Priest: var 4 and 203530.
    - **The class name comes from the line.** NaturalClassLine gains StarterLabel
      (priest) and StarterName (Priest). The journey's strings that named the class use
      them: nine step labels (the Q2001, Q2002, Q2005, Q2006 and Q2007 reward claims,
      the trainer walk, the Sprigg pull, the cube fight and the shipped-spawn search
      label), one traced plan reason in Q2005, and ten messages of refusals and
      requirements, three of them in the combat class. A character keeps its starter's
      name in these after its class change, as the Cleric of a bridge run always did.
    - **Not renamed:** the trace keys pull-plan and defend-before-pull; the two rest
      texts, which CP-17 pinned; and strings outside the journey that name the Priest
      (the patrol baseline's reason, the static policy's version and reasons, the
      inventory reason best-usable-priest-upgrade, the NI-01 identity scenario). The
      gear reason belongs to CP-22; the others are the Priest line's own and no other
      line reaches them.
    - Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,719 with 16 skipped),
      and Fast passes (run cp20-fast). The guard of rule (c), gate p, is in the proof run.
- [x] **CP-21 - Campaign ranges and readiness thresholds from the profile.** Depends: CP-20
  - Work: The Ishalgen campaign's range literals (the Q2002 Sprigg hunt at 22 and 25 m,
    J:6433-6510; Q2004 and Q2005 at 23 and 25 m, J:6794, 6896-6913, 7093, 7127) and its
    readiness thresholds (J:6238, 7268, 7287, 7393, 7608-7610) become named profile values
    with today's numbers. No executor is restructured: the Sprigg hunt and Q2005's
    firing-edge search keep their shape. Turn on the matching pin rows (the campaign's 22,
    23 and 25 m literals and its readiness thresholds).
  - Proof: Neutral gate, set m.
  - 2026-10-07: done. Gate run gate-a1, set p+m (the proof set and the guard in one run):
    verdict pass (run/cp/CP-21/gate-a1/verdict.json). p: 35,811 records and m: 123,112,
    each identical to its baseline.
    - **NaturalCampaignRules** (in Sc/Classes/NaturalEngageRules.cs) names ten values:
      the Sprigg hunt's route threshold 25, standoff 22 and selection range 25; Q2005's
      firing edge 25, stalker search range 23 and blocker threshold 25; the HP fraction
      0.75 of the wait for Return; and three HP thresholds, the stalker pull at 90,
      before a sack at 80 and before the camp at 80. The profile gains Campaign; the
      Priest and the Cleric share one.
    - **Eleven sites** in NaturalIshalgenJourney.cs read it: four in the Sprigg hunt (the
      standoff twice), three in Q2005's ranges, both sides of Q2005's 90% test, and one
      each in Q2006, Q2007 and UseLearnedReturnToBindAsync. No executor changed shape.
      The two sides of the 90% test read one value, as they were one threshold.
    - As CP-11 found, old J:6794 of this item's list holds no literal of its own; Q2004
      gets its 23 and 30 through ApproachShippedCombatSpawnAsync, which CP-18 moved.
    - **Every pin row is on.** The pin test has 58 rows asserted and none pending, so
      CP-41's check that no row is pending already holds.
    - A unit test names the ten values and shows that Return's float comparison is the
      75% line for every HP of a 669-HP Priest.
    - No Java was read: the item relies on no server behavior it did not already rely on.
    - Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,720 with 16 skipped),
      and Fast passes (run cp21-fast). The guard of rule (c), gate p, is in the proof run.
- [x] **CP-22 - Gear, keep-or-sell and reward scores behind one set of gear rules.**
  Depends: CP-12, CP-14
  - Work: Load the whole restrict row into NaturalItem and replace IsPriestGear and
    IsClericGear, the two Decide branches, GearScore and ClericGearScore,
    AutoLearnedPriestSkillsObserved and the two supply sets, the Priest's as CP-06 widened
    it (Sc/NaturalIshalgenInventoryPolicy.cs:17-55, 70, 132-136, 167-265) and the staff-rule
    test (Sc/NaturalGearPolicy.cs:61-78) with one path that takes NaturalGearRules. Existing
    public members stay as wrappers; the Priest and Cleric rules keep both score formulas,
    the staff rule and the expected masteries as they are. They also keep the reward choice
    as it is: ChooseReward is class-blind today and scores every choice by the Priest's
    UsableAt and GearScore, then price, for the Cleric too
    (Sc/NaturalIshalgenInventoryPolicy.cs:246-261). The golden test stays green in the unit
    run.
  - Proof: Neutral gate, set m+b+c+ax. Scope b is the only one that sells by the gear rules
    (the Altgard shop stop, whose Decide call is J:4667) and it also buys potions; ax holds
    the coin-armor purchases and the staff rule. CP-09 records b, and CP-12 already depends
    on CP-09. If CP-09 dropped b, set m+c+ax: the sell decisions then rest on the golden
    test only, and the doc says so. If CP-09 dropped ax, the set goes without it: the
    coin-armor purchases and the staff rule then rest on the golden test only, and the doc
    says so.
  - 2026-10-07: done. Gate run gate-a1, set p+m+b+c+ax (the proof set and the guard in one
    run): verdict pass (run/cp/CP-22/gate-a1/verdict.json). p: 35,811 records, m: 123,112,
    b: 128,937, c: 96,166 and ax: 15,762, each identical to its baseline. Scope b holds
    the Altgard shop stop's sale by the gear rules and ax the seven coin-armor purchases
    with their 14 equips. The golden gear test of CP-12 is green on the new code without
    an edit: 2,207 items, both classes, every level.
    - **Sc/Classes/NaturalGearRules.cs** is new: the class whose restrict column counts,
      the gear groups with the weapon and off-hand groups, an optional cap on the required
      level, the score, the equip reason, the supplies, whether the rules are those from
      Ascension on, the hand rule's group, and the expected level-1 skills with their
      catalog. Slot, Usable and AutoLearnedSkillsObserved are methods of the rules. The
      Priest's and the Cleric's rules are its two static instances, and the profile gains
      Gear.
    - **NaturalItem** holds the whole restrict and restrict_max rows, with
      RequiredLevelFor(class) and MaximumLevelFor(class). Every old member keeps its name
      and asks the rules: RequiredLevel, MaximumLevel, ClericLevel, ClericMaximumLevel,
      IsPriestGear, GearSlot, UsableAt, GearScore, IsClericGear, ClericGearSlot,
      UsableByClericAt and ClericGearScore. All 50,371 restrict rows of the shipped
      templates are 17 whole numbers, so reading the whole row cannot fail where reading
      two columns did not.
    - **One Decide path.** Decide(inventory, level, capacity, rules, ...) replaces the two
      branches. Its checks run in the Cleric branch's order; the six that only the Cleric
      had (the Haramel and coin-gear protections, the retained staff, the open quest's
      needs, the accessory kept for later and the surplus accessory) apply only to rules
      from Ascension on, and so does the bridge contract's protected item list as a
      supply. The old overload with the cleric flag picks the rules and calls it, so the
      six callers of the journey are untouched; CP-23 gives them the observed class's
      rules.
    - **ChooseReward** still scores every choice by the Priest's rules for every class,
      now by name. **AutoLearnedPriestSkillsObserved** asks the Priest's rules.
    - **The staff rule** of NaturalGearPolicy.SelectUpgrades reads the hand rule's group
      from an optional rules parameter, the Priest line's by default. Both rule sets name
      STAFF. Rules without one wear by item level, which no profile uses yet.
    - **One thing the Cleric's rules do not have.** No check of expected skills was ever
      written for the Cleric, so its rules expect none and its test always passes. The
      class-line contract of CP-01 holds every class's masteries; a profile item that
      wants the check reads them from there.
    - UT/NaturalGearRulesTests, 5 tests, cover what the rules add: the whole restrict
      row, the two rule sets' groups and supplies, slots, the level cap and both score
      formulas, the expected skills, and the hand rule with and without a group.
    - No Java was read: the item relies on no server behavior it did not already rely on.
    - Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,725 with 16 skipped),
      and Fast passes (run cp22-fast). The guard of rule (c), gate p, is in the proof run.
- [x] **CP-23 - The journey's reward, sell and equip sites use the observed class's gear
  rules.** Depends: CP-15, CP-22
  - Work: Decide(world) resolves the rules from the observed class in place of
    IsCleric(world) (Sc/NaturalIshalgenInventoryPolicy.cs:167-173), so its callers do not
    change: six in J (477-479, 1743-1744, 2499-2500, 3108-3109, 4508-4509, 4667), and
    Sc/NaturalCoinGearSteps.cs:59, two LiveBots scenarios and one SIM test; J:2801-2803
    hands the policy itself to NaturalCoinGearSteps. ChooseReward takes a reward rule as an
    input at its seven call sites (J:6322, 6388, 6618, 7358, 7481, 7660, 8026). For the
    Priest and for the Cleric that rule is today's class-blind one: the Priest's UsableAt
    and GearScore, then price, with the ceremony pick as a contract pin
    (Sc/NaturalIshalgenInventoryPolicy.cs:246-261). J:8026 also runs for the Cleric on the
    Altgard legs, so giving the Cleric its own rules there would change accepted picks. Only
    lines outside priest-cleric get a reward rule from their own gear rules.
    EquipUpgradesAsync (J:415-441) describes gear with the same rules. For lines other than
    priest-cleric, and if question 13 is answered yes, the equipment check also runs after
    each completed Ishalgen quest at the one dispatch point (J:816-853); the Priest line
    keeps its present check points.
  - Proof: Neutral gate, set m+b+c+ax. Scope b is the only one that sells by the gear rules
    (the Altgard shop stop, whose Decide call is J:4667) and it also buys potions; ax holds
    the coin-armor purchases and the staff rule. CP-09 records b. If CP-09 dropped b, set
    m+c+ax: the sell decisions then rest on the golden test only, and the doc says so. If
    CP-09 dropped ax, the set goes without it: the coin-armor purchases and the staff rule
    then rest on the golden test only, and the doc says so.
  - 2026-10-07: done. Gate run gate-a1, set p+m+b+c+ax (the proof set and the guard in one
    run): verdict pass (run/cp/CP-23/gate-a1/verdict.json). p: 35,811 records, m: 123,112,
    b: 128,937, c: 96,166 and ax: 15,762, each identical to its baseline. The golden gear
    test stays green.
    - **Decide(world)** takes the gear rules of the observed class, by the policy's class
      line, in place of the Cleric test. The policy's Load gained an optional line, the
      accepted one by default, and the journey's seven loads that lead to Decide(world)
      pass the run's line. NaturalCoinGearSteps, the two LiveBots scenarios and the SIM
      tests did not change. An unobserved class decides by the line's starter.
    - **ChooseReward** takes the rules it scores by as an optional last parameter, the
      Priest's by default. The journey's seven calls pass the profile's new RewardGear.
      Both profiles of the accepted line name the Priest's rules there, so the Cleric's
      picks on the Altgard legs are scored as before. The ceremony pick stays a contract
      pin whatever the rules.
    - **EquipUpgradesAsync** reads the tooltip for the class of the observed profile's
      gear rules and hands the rules to NaturalInventoryCheck.EquipAsync, which passes
      them to SelectUpgrades for the hand rule.
    - **CP-Q13 runs on its default.** NaturalClassLine.ChecksGearAfterIshalgenTurnIns is
      true for every line but priest-cleric, and the quest dispatch runs the equipment
      check after a completed quest for such a line. No such line exists yet, so this
      branch has not run; the first run that reaches it is the Chanter's or the Warrior's.
    - Unit tests added: the observed class picks the rules (Priest when unobserved, Cleric
      when observed), the reward choice equals the old one by default and with the
      Priest's rules over 1,000 quest ids at 26 levels and differs somewhere with the
      Cleric's, and the line's switch for CP-Q13.
    - The same refusal as in CP-15 applies here: an observed class outside the line stops
      Decide(world) by name, where it used to decide by the Priest's rules.
    - No Java was read: the item relies on no server behavior it did not already rely on.
    - Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,726 with 16 skipped),
      and Fast passes (run cp23-fast). The guard of rule (c), gate p, is in the proof run.
- [x] **CP-24 - Restock table per profile: potions only.** Depends: CP-09, CP-15
  - Work: Data first: both Ishalgen restock vendors carry trade lists 264 and 721
    (npc_trade_list.xml:375-378, 2209-2212). List 721 holds the Minor Life Elixir the Priest
    buys today. List 264 is the one that sells bandages
    (goodslists/goodslists.xml:16765-16769), and it is not opened: the operator ruled
    bandages out on 2026-10-07 (CP-Q11). Add pure NaturalRestockRules: per profile a table
    of item, threshold, target and trade list, plus a Kinah floor. The Priest line's table
    is today's rule (Minor Life Elixir 162000052, at 5 or fewer up to 12, list 721, no
    floor; Sc/NaturalIshalgenPotionPolicy.cs:18-19, 50-57). MaintainInventoryAsync
    (J:464-552) asks the rules what to buy and still opens list 721 only. It runs at the
    end of every completed rest (J:9717-9718), so the Priest runs this code in every scope
    with a rest, and the proof is a gate. Write UT/NaturalRestockRulesTests as part of the
    work; it runs in the bundle: the Priest table reproduces NeedsRestock and
    AffordablePurchaseCount over a grid of stock and Kinah, a table with a floor never
    spends below it, and no table names list 264. This item adds the mechanism and the
    Priest line's table. Each new class's table (elixirs with CP-Q11's numbers) is written
    by that class's profile item. With the help kit on, the kit's potion is supplied, so a
    vendor elixir is bought only when the stock still runs down between two stock checks.
    Read the Priest's Kinah ledger from the m baseline, which CP-09 records, and write it
    into the doc as the base for question 11's floor. Turn on the matching pin rows (restock
    5 and 12).
  - Proof: Neutral gate, set m: the Priest's restock decisions at every rest are unchanged.
    No gate scope reaches an Ishalgen vendor buy, so the trade itself is first played in a
    class journey; the doc says so.
  - 2026-10-07: done. Gate run gate-a1, set p+m (the proof set and the guard in one run):
    verdict pass (run/cp/CP-24/gate-a1/verdict.json). p: 35,811 records and m: 123,112,
    each identical to its baseline.
    - **Data, checked again and now by a test.** Both Ishalgen restock vendors (798038 and
      203542) carry trade lists 264 and 721. List 721 sells the Minor Life Elixir
      162000052 and the Minor Mana Elixir 162000057. List 264 sells 169000003, 165000001
      and 169300002 and no elixir; it is the one the operator's bandage ruling keeps shut.
    - **Sc/Classes/NaturalRestockRules.cs** is new and pure: a table of lines (item,
      threshold, target, trade list, and the items whose owned total is the stock) and a
      Kinah floor. Needed(inventory) gives the first line whose stock is at or below its
      threshold; PurchaseCount gives how many to buy at the displayed price, up to the
      target and never below the floor. A table that names list 264 cannot be built, nor
      one with nothing to buy. The profile gains Restock.
    - **The Priest line's table**, shared by the Priest and the Cleric: one line, Minor
      Life Elixir 162000052 at 5 or fewer up to 12 from list 721, counted with all six
      life potions and elixirs, no floor.
    - **MaintainInventoryAsync** asks the table whether to go, which line to buy and how
      many, and checks that the vendor offers the line's list, which is 721. Its walk,
      its sale, its trade and its trace record are as they were. It buys one line a
      visit; a table with two lines buys the second at a later rest.
    - **UT/NaturalRestockRulesTests**, 5 tests: the Priest table equals
      NaturalIshalgenPotionPolicy.NeedsRestock over 144 inventories and
      AffordablePurchaseCount over 504 states of stock, Kinah and price; a table with a
      floor of 1,000 never leaves the purse below it and buys as much as the floor allows;
      the first needed line of a two-line table is the one bought; no table names list
      264, checked against the shipped vendor and goods data.
    - **Pin rows:** restock 5 and 12 also read the profile's table. All 58 rows stay
      asserted.
    - **The Priest's Kinah ledger in the m baseline** (the base for question 11's floor;
      the purse at each of the run's 41 checkpoints, from its resume observations). Start
      1,000. After the four village quests, at level 3: 2,380. The village bind costs 43
      and the outpost bind 134, which leaves 2,203 at level 6, the lowest purse after the
      first minutes. Q2003 brings it to 4,093 at level 7, and it stays there through
      Q2004 to Q2006 into level 9. The one soul heal costs 71. The side quests of the
      second half then pay it up to 40,342 at the Munin stop. Spent in the whole run:
      248, on two binds and one soul heal. No vendor is visited and nothing is bought.
      A Minor Life Elixir costs 250 at the base price and the vendors sell at twice
      that, so a full restock from 0 to 12 is about 6,000: more than the Priest owns
      before level 9 is well under way, and a small part of what it owns at the end.
    - **No gate scope reaches an Ishalgen vendor buy**, as the item says: with the help
      kit on, the stock never falls to 5. The trade itself is first played in a class
      journey.
    - No Java was read: the item relies on no server behavior it did not already rely on.
    - Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,731 with 16 skipped),
      and Fast passes (run cp24-fast). The guard of rule (c), gate p, is in the proof run.
- [x] **CP-25 - Identity rules by line, and the Ascension contract by line and by choice.**
  Depends: CP-01, CP-08, CP-14
  - Work: This item edits existing bot files, so it waits for the first baselines (CP-08).
    Add NaturalJourneyIdentityRules.Classify(line, ...)
    (Sc/NaturalJourneyIdentityRules.cs:33-45): before Ascension the line's starter at level
    1-9 on maps 220010000, 320010000 and 320020000; after it the line's second class on the
    bridge maps; the Convent and the leg-scoped maps stay tied to the Cleric. The existing
    overloads delegate with the Priest line, so every present caller keeps today's result:
    three call sites in tools/Aion.LiveBots (LiveNaturalIshalgenIdentityScenario.cs:111 and
    131, LiveNaturalJourneySession.cs:69), Sc/NaturalIshalgenIdentityScenario.cs:141,
    J:4612, and the five tests that assert a Chanter or Warrior refusal
    (UT/NaturalJourneyIdentityRulesTests.cs:27-36,
    NaturalAltgardHaramelContractTests.cs:202, NaturalAbyssEntryContractTests.cs:275,
    NaturalAltgardLeg11ContractTests.cs:129 and, through the engine,
    NaturalAscensionDecisionEngineTests.cs:119). Callers made line-aware later: the bridge
    engine (Sc/NaturalAscensionDecisionEngine.cs:80, CP-26), and the SIM host
    (SimT/SimulationNaturalIshalgenJourneyTests.cs:122, 135) and the SIM session relog
    (SimT/SimulationFastScenarioTests.cs:1300), both in CP-27. Add
    NaturalAscensionContract.ForLine and ForChoice(core, classLines, starter, second class),
    which build a bridge in memory (class-choice action and page, Q2009 var, ceremony step
    and pick, dispatch quest and steps, endpoint class, protected items) and expose the four
    class-dependent steps by role. e2e/natural-ascension-contract.json is not edited and
    nothing calls the new members yet.
  - Proof: Unit test UT/NaturalClassLineSeamTests: Classify rows per line, accepted and
    refused, with a line that has no second class refused after Ascension; ForLine for
    priest-cleric equals the file-loaded contract record for record; the Chanter and the
    Templar overlays' action, class id, list name, Q2009 var and dispatch quest are
    recomputed from quest_data.xml and the handler source.
  - 2026-10-07: done. UT/NaturalClassLineSeamTests is new and passes, 60 tests (3 facts, 35
    identity rows, 22 bridge rows). Guard run guard-a1, set p: verdict pass
    (run/cp/CP-25/guard-a1/verdict.json), 35,811 records, identical to its baseline.
    - **Java, read first.** _2008Ascension.java:136-162: SETPRO6 sends the page that
      ClassChangeService gives for the race and the starter, and SETPRO7 to SETPRO17 each
      set one second class at var 6. _2009ACeremonyinPandaemonium.java:104-133: Balder's
      SETPRO3 sets the var and the reward group by starting class (10/0, 20/1, 30/2, 40/3,
      50/4, 60/5); lines 136-178: only that var's preceptor answers, with its own page.
      The six dispatch handlers (_2901 to _2904, _29070A, _29071A) are one handler with six
      quest ids: against _2904 the others differ in no line but the id, the class name and
      an author line. Each registers Doman 204191 and Meiyer 203559.
    - **quest_data.xml.** The six dispatch quests are IMPORTANT, level 10, 11,237
      experience, and differ in class_permitted, the Q2009 reward index that starts them
      and the work item. Q2009's six reward groups each pay 13,125 experience, 5 of item
      162001057 and 250,000 Kinah. So a pair's bridge needs no number the class-line
      contract does not hold.
    - **Classify(line, ...)** is new, for a class and for a wire class id. Before Ascension:
      the line's starter at level 1-9 on 220010000, 320010000 and 320020000. After it: the
      line's second class from level 9 on the four bridge maps; the Convent and the l11,
      l12 and ax maps only when that second class is the Cleric. A line with no second
      class has no state after Ascension. The two old overloads delegate with the Priest
      line. No caller was changed: the three in tools/Aion.LiveBots, the identity scenario,
      the journey, the bridge engine and the two SIM hosts still call the old overloads.
      The stage names IshalgenPriest and AscensionCleric stay as they are for every line.
    - **NaturalAscensionContract.ForChoice(core, classLines, starter, second, pick)** builds
      a pair's bridge in memory and runs it through the checks Load runs. It replaces: the
      start class; the class step's action and class page; the class choice (classes, page,
      action, masteries); the ceremony step (var, preceptor, position, talk range, page,
      pick action) and the ceremony reward (group, list, pick, its group); the dispatch
      quest in the quest list, its two steps, its start reward, permitted classes and work
      item; the endpoint's class, id, completed quests and equipped item; and the ceremony
      item among the protected items. All else is the reviewed bridge's own objects.
      **ForLine(line, pick)** does this for a line from the two checked-in files.
      **Step(role)** finds the four class-dependent steps by what they do: ClassChoice,
      Ceremony, DispatchStart, DispatchReward. Nothing calls the new members yet, and
      e2e/natural-ascension-contract.json is not edited.
    - **The pick.** With no pick named, the bridge keeps the reviewed pick, the Karmic
      Staff 101500498. A second class whose list does not offer it is refused by name, so
      every pair but the Cleric and the Chanter must name its pick. For the Chanter the
      unnamed pick is the staff, which is also CP-Q7's default; naming the mace 100100495
      gives SELECTED_QUEST_REWARD1.
    - **Step keys of another pair.** The class step keeps its key. The ceremony step keeps
      q2009-reward-lyfjaberga for the two Priest pairs and is
      q2009-reward-preceptor-<npc id> for another starter. The dispatch steps are
      q<quest id>-v0-doman and q<quest id>-reward-meiyer. A caller should ask by role.
    - **Proof.** (1) Over 10,080 rows (every class, 12 levels, 12 maps, 5 legs) the two old
      overloads and the Priest line each give the rule as it stood before, restated in the
      test. (2) 35 rows for a Chanter line, a Templar line and a Warrior line with no
      second class, accepted and refused: the Chanter and the Templar are refused on the
      Convent and the leg-scoped maps, and the Warrior-only line refuses both Warrior
      second classes after Ascension. (3) ForLine and ForChoice for priest-cleric serialize
      to the same text as the file-loaded record, from new objects. (4) For all eleven
      second classes and each of the 22 items their lists offer, the bridge's action, class
      page, Q2009 var, reward group, preceptor, page, list, pick action, dispatch quest,
      start reward, permitted classes, work item, quest row and endpoint are recomputed
      from quest_data.xml, QuestTemplate.cs and the ported handlers, the pair's dispatch
      handler is shown equal to Q2904's but for its id, and eleven steps and the instance,
      teleporter, bind, shop, movies and kept accessories are shown to be the reviewed
      bridge's own objects. (5) The Chanter's bridge differs from the Cleric's in the
      class, the action and the list name and nothing else; the Templar's has SETPRO8,
      class id 2, knight_selectable_reward, var 10, preceptor 204080 with page 2034, class
      page 3057 and dispatch 2901.
    - **Findings, not fixed.** (a) A line with no second class has no bridge: ForLine
      refuses it by name. CP-26 decides what such a line's runner loads. (b) The bridge's
      shop (162000053 up to 12 and 169300003 up to 30), its kept accessories and its other
      protected supplies are carried over as the Cleric's. Whether another class buys
      169300003 is that class's item to settle. (c) The stage names still say Priest and
      Cleric for every line; a rename is not this item's.
    - Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,791 with 16 skipped),
      and Fast passes (run cp25-fast).
- [x] **CP-26 - The bridge reads the line's contract.** Depends: CP-09, CP-15, CP-25
  - Work: The runner loads the line's contract once and passes it down: PlayBridgeTalkAsync
    stops calling LoadDefault (J:4825, 4841-4851) and asserts the line's ceremony item;
    ImplementedBridgeSteps (J:8477-8481), the engine's step keys and 2904 literals
    (Sc/NaturalAscensionDecisionEngine.cs:97-154; J:1429-1430, 1446, 4720, 4757-4758) and
    the ceremony list constant (Sc/NaturalIshalgenInventoryPolicy.cs:72, 121) use the role
    lookups. The engine's own identity check (Sc/NaturalAscensionDecisionEngine.cs:80-83,
    with the blocked "identity" stop at 85-89) classifies with the line, or with the
    contract's second class. It runs on every bridge decision, in the ceremony-only bridge
    too (J:1397 starts that bridge and J:1061 is its decision call), so without this a
    Chanter is stopped at the first decision after SETPRO13. The default line keeps the
    refusal that UT/NaturalAscensionDecisionEngineTests.cs:119 asserts. Add a unit row:
    under priest-chanter the engine accepts a level-9 Chanter in Ataxiar (map 320020000) at
    Q2008 REWARD. Reason and step strings stay byte-identical for the Cleric.
  - Proof: Neutral gate, set p+b+c. If CP-09 dropped b, set p+c: p plays the class choice
    and the ceremony; the dispatch-quest lookups (the Q2904 steps and literals) are then
    covered by the CP-25 unit test only, and the doc says so. Scope p returns before the
    Q2904 block (Sc/NaturalAscensionDecisionEngine.cs:132-135), and c plays none of it.
  - 2026-10-07: done. Gate run gate-a1, set p+b+c: verdict pass
    (run/cp/CP-26/gate-a1/verdict.json). p: 35,811 records, b: 128,937 and c: 96,166, each identical to its baseline.
    Scope b was not dropped by CP-09, so the dispatch-quest lookups are played, in b.
    - **Java.** Nothing new was read: the item relies on the handlers CP-25 read
      (_2008Ascension.java, _2009ACeremonyinPandaemonium.java and the six dispatch
      handlers), and it changes what the bot reads, not what the server does.
    - **The runner** holds the line's bridge in LineBridge(), built once by
      NaturalAscensionContract.ForLine(ClassLine) when the run first needs it. The item's
      line numbers had moved; the sites are these. RunAscensionBridgeAsync and
      TakeCeremonyTeleporterAsync take it in place of LoadDefault. PlayBridgeTalkAsync
      takes the instance, the Destiny Card steps and the ceremony reward from it, finds
      the ceremony step by role and asserts the line's ceremony item, by its id. Doman is
      found by role in both teleporter steps. The early-ceremony check and the bridge-stop
      receipt read the dispatch quest and the bridge's quest list from it.
      ImplementedBridgeSteps keeps the eleven keys that are the same for every pair, and
      PlaysBridgeStep adds the four class-dependent steps by role.
    - **The engine** reads the class pair, the four class-dependent steps and the dispatch
      quest from the contract it is given. Its identity check classifies with the
      contract's pair, through a new Classify(starter, second, ...) that the line overloads
      of CP-25 now delegate to. The reasons that name the class or the dispatch quest are
      built from the contract. The ceremony reason stays "Lyfjaberga: the Karmic Staff
      (REWARD2)." when the preceptor is Lyfjaberga and the pick is the Karmic Staff; any
      other ceremony reads "Preceptor <npc>: ceremony item <id> (REWARDn).". The contract
      gains StarterClass and SecondClass, left out of its JSON.
    - **The inventory policy** builds the line's bridge with ForChoice and reads the
      ceremony list and the quests with no reward choice from it; the constant
      priest_selectable_reward is gone. A line with no second class keeps the reviewed
      bridge's protected items and never reaches its quests.
    - **Unit rows.** NaturalAscensionDecisionEngineTests, 3 new: (1) twelve steps and
      reasons of the reviewed bridge, word for word, with the Q2904 texts and the identity
      and endpoint checks; (2) under the Chanter's bridge a level-9 Chanter in Ataxiar at
      Q2008 REWARD is sent on to Munin, in the ceremony-only bridge too, where the reviewed
      bridge still blocks it at "identity"; the Chanter's route reads SETPRO13, Lyfjaberga
      and Q2904; (3) a Templar's bridge uses preceptor 204080 and Q2901 and is not moved
      by a Q2904 in the journal. NaturalIshalgenInventoryPolicyTests, 1 new: the accepted
      line, a Chanter line and a line with no second class all pick the staff at Q2009 and
      have no choice at Q2008, Q2904 and Q24010. The refusal that
      NaturalAscensionDecisionEngineTests asserted for the default bridge still stands.
    - **Findings, not fixed.** (a) A pair whose list does not offer the Karmic Staff cannot
      load its inventory policy or its bridge until its pick is named: ForLine and the
      policy pass no pick. Only the Cleric and the Chanter take a second class in this
      plan, and both lists offer the staff, so nothing here is stopped; a later line needs
      its pick on the class line. (b) Left as they were, because they are the Cleric's
      gates of CP-27: VerifyCapitalPass, with its Q2904 text and the worn staff 101500498;
      the early ceremony's IsCleric test; and the endpoint's identity check in
      CompleteAscensionLegAsync, which classifies with the Priest line and says "the
      bridge's Cleric". No item lists that last check. The Chanter stops at the
      capital-stage stop in CP-33 and never reaches it; a line that plays the whole bridge
      would. (c) The two failure texts of the ceremony assertion now name the item id, not
      the Karmic Staff. They are written only when the assertion fails. (d) The engine
      still names Q2008 and Q2009 by number; they are the same for every pair.
    - Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,795 with 16 skipped),
      and Fast passes (run cp26-fast). The guard of rule (c), gate p, is in the proof run.
- [x] **CP-27 - Second-class checks by line; the Cleric-only leg gates stay.** Depends:
  CP-26
  - Work: The early-ceremony check (J:1398) uses the line's second class. Three SIM identity
    checks become line-aware, which removes the first hard stops for another starter and for
    the Chanter: the SIM host's two, on a resumed and on an entered character
    (SimT/SimulationNaturalIshalgenJourneyTests.cs:122, 135), and the one inside the SIM
    session, SimulationL0Session.ReloginExistingCharacterAsync
    (SimT/SimulationFastScenarioTests.cs:1290-1304, the Classify call at 1300-1301). The
    journey reaches that third check on every relog (J:903, 1370, 1612, 4451, 4630); J:1370
    is the capital-stage stop, the Chanter's endpoint in CP-33. So SimulationL0Session takes
    the class line as a constructor argument or a settable property on the concrete type,
    set by the SIM host where it builds the session
    (SimT/SimulationNaturalIshalgenJourneyTests.cs:54-56), and its relog check classifies
    with it; the default line gives today's result. INaturalJourneySession still gains no
    member. The LIVE session's relog check
    (tools/Aion.LiveBots/LiveNaturalJourneySession.cs:69) stays on the Priest line. These
    stay Cleric-only, each with a refusal text that names the class: the end-of-Ishalgen
    check (J:1006), the returned-Cleric test (Sc/NaturalIshalgenDecisionLoop.cs:83), the
    capital pass (Sc/NaturalCapitalDecisionEngine.cs:42-45) and J:1601, 2373 and 4540. A
    line without a second class is refused at the bridge with a clear message. Unit rows: a
    level-10 Chanter is blocked at the capital pass and at the Ishalgen return with the
    stated reason.
  - Proof: Neutral gate, set p+b+c (p+c if b was dropped).
  - 2026-10-07: done. Gate run gate-a1, set p+b+c: verdict pass
    (run/cp/CP-27/gate-a1/verdict.json). p: 35,811 records, b: 128,937 and c: 96,166, each identical to its baseline.
    - **Java.** None read: the item changes which class the bot's own checks accept and
      what its refusals say. It relies on no server behavior.
    - **By the line's second class now.** The early-ceremony check asks
      combat.IsLineSecondClass in place of IsCleric; its text is unchanged. The SIM host
      classifies a resumed and an entered character with the run's line, and
      SimulationL0Session has a settable IdentityClassLine, set by the host where it builds
      the session, that its relog check classifies with. The default is the accepted line.
      INaturalJourneySession gained no member, and the LIVE session's relog check is as it
      was, on the Priest line.
    - **Still the Cleric's, and each refusal now names the class it met.** New helper
      NaturalJourneyIdentityRules.ClassName(wire id). The capital pass blocks another
      class with "The capital pass requires the completed level-10 Cleric ceremony; the
      character is CHANTER."; a Cleric short of the level or the quests reads as before.
      The Ishalgen decision engine blocks a level-10 character of another class that has
      Q2008 and Q2009 with the new stop returned-class, "The Ishalgen return after the
      ceremony needs the Cleric; the character is CHANTER."; before, that character got
      the level-10 stop, which named no class. The four Require texts in the journey (the
      end of Ishalgen, the Abyss-entry leg, an Altgard leg and the NA-23 encounter) end
      with "; the character is level N CLASS.". None of these texts is written in a run
      that passes.
    - **A line with no second class** is refused where the bridge starts:
      RunAscensionBridgeAsync builds the line's bridge first, and ForLine says "Class line
      <id> takes no second class, so it has no Ascension bridge: run it with the bridge
      off, to the Munin stop.".
    - **Unit rows, 3 new tests.** NaturalCapitalDecisionEngineTests: a level-10 Chanter,
      a Templar and an unknown class id are blocked with the reason that names them, and
      the Cleric's two refusals read as before. NaturalIshalgenDecisionLoopTests: the
      Chanter and a Templar are blocked at the Ishalgen return by name; the Cleric goes
      on; without the early ceremony, without a class, or with Q2008 alone the old
      level-10 stop stands. NaturalClassLineSeamTests: ClassName for a class, an
      unobserved class and three unknown ids, and the full text of the no-second-class
      refusal.
    - **Findings, not fixed.** (a) The bridge observation reads an unobserved class as
      id 0, which is the Warrior's, so a capital-pass refusal before the class is observed
      would name the Warrior. The pass decides only on a synchronized view, as it did.
      (b) The endpoint identity check of the whole bridge (CompleteAscensionLegAsync) is
      still on the Priest line, as logged under CP-26.
    - Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,798 with 16 skipped),
      and Fast passes (run cp27-fast). The guard of rule (c), gate p, is in the proof run.
- [x] **CP-28 - Snapshot tooling carries the class line.** Depends: CP-03, CP-15
  - Work: scripts/sim/sim-snapshot.ps1 gains -Class <line id> for Capture and Replay. The
    script holds the list of the seven line ids of this plan (priest-cleric, priest-chanter,
    warrior, scout, mage, engineer, artist) and refuses any other. A line the C# table does
    not hold yet fails later, in NaturalClassLine.Parse, with a message that names it.
    Capture and Replay set CP_CLASS from it. Help items are on for every line, so no line
    sets NA_HELP_ITEMS: the runner clears it (CP-03), and the supply is on unless that
    variable is exactly 0 (Sc/NaturalHelpItemAllowlist.cs:76). A help-items.json is
    expected evidence for every line. All three metadata writers (lines 303-311, 343-355,
    420-431) record classLine only when it is not the default. Restore emits CP_CLASS only
    when the snapshot recorded one, and it never emits NA_HELP_ITEMS. Verify takes its
    environment from Restore. For a capital snapshot of a line that has no capital leg
    Restore emits PC_CAPITAL=start in place of first (lines 222-225), so Verify re-checks
    the endpoint and stops. The forty existing snapshots restore exactly as now.
    scripts/sim/audit-natural-complete.py is left alone.
  - Proof: pwsh -NoProfile -File scripts/sim/test-sim-snapshot.ps1 with new cases: a
    historical restore gains no selector, a recorded line round-trips through Capture and
    Restore, no Capture, Replay or Restore of any line sets NA_HELP_ITEMS, a Chanter capital
    snapshot restores with PC_CAPITAL=start, an unknown line is refused.
  - 2026-10-07: done. scripts/sim/test-sim-snapshot.ps1 passes with the new cases
    (run/cp/CP-28/checks.log). Guard run guard-a1, set p: verdict pass
    (run/cp/CP-28/guard-a1/verdict.json), 35,811 records, identical to its baseline; the
    gate replays through the changed script.
    - **Java.** None read: the item changes a script and relies on no server behavior.
    - **-Class <line id>** is new on Capture and Replay. The script holds the seven line
      ids and refuses any other, case-sensitively, before it touches MySQL; Restore, Verify
      and Drop refuse the switch. A fresh run of another line sets CP_CLASS. The accepted
      line sets nothing, named or not, so its child environment, its Replay receipts and
      its snapshots are as they were.
    - **A run that starts from a snapshot plays the snapshot's line.** Restore prints
      CP_CLASS for a snapshot that recorded a line, and an Altgard leg, the first capital
      pass and a Replay -From carry it into the journey. -Class may repeat the snapshot's
      line and may not change it ("Snapshot X holds class line A; -Class B cannot play
      it."); the restored copy is dropped. The item did not ask for this refusal; without
      it a wrong -Class would have played a character under another class's rules.
    - **Metadata.** All three writers (capital, leg, prefix) add classLine as the last
      property, and only for a line other than the accepted one. Restore refuses a
      snapshot that records a line outside the seven.
    - **Capital.** Restore prints PC_CAPITAL=start for a capital snapshot of a line with
      no capital leg, and first for the accepted line as before. Only priest-cleric has a
      capital leg. Capture and Replay refuse -CapitalStage first for any other line.
    - **NA_HELP_ITEMS.** No action sets or prints it. The runner still clears it, so the
      supply is on for every line.
    - **One change beside the item's list.** Capture and Verify wrote their evidence to
      run/snapshots/_capture and run/snapshots/_verify whatever -SnapshotRoot said. They
      now write under the snapshot root. The default root is run/snapshots, so every real
      path is the same; the test's captures stay in its temporary folder.
    - **New cases in the test,** against a fake Docker, a fake journey and a fake git:
      a historical restore prints its three variables and nothing else; the accepted line,
      named or not, captures with no CP_CLASS and no classLine and restores with no
      selector; a Mage capture sets CP_CLASS, records the line, restores with it, and a
      Replay and a leg capture from it play it and carry it on; a fresh Replay of a Warrior
      sets the line and one of the accepted line leaves its receipt empty; the accepted
      line's capital snapshot restores on first, a Chanter's on start with its line, and
      Verify of the Chanter's takes that environment and passes; no Restore and no Replay
      receipt holds NA_HELP_ITEMS and the parent's values come back; an unknown line, a
      line id in another case, a line on Restore or Verify, another line than the
      snapshot's, a Chanter first capital pass and a snapshot recording an unknown line
      are each refused, and each restored copy is dropped unplayed.
    - **The real snapshots.** The 40 snapshots under run/snapshots record no classLine, so each restores exactly as before.
    - scripts/sim/audit-natural-complete.py was not touched.
    - Bundle: the seven pre-commit checks pass, both script tests pass
      (test-sim-snapshot.ps1 and trace/test_compare_traces.py), and Fast passes (run
      cp28-fast). Aion.GameServer.Tests was not run: no C# changed.

### C. The Chanter branch at Ascension

- [x] **CP-29 - Table gear and reward rule, for every class but the Priest and the Cleric.**
  Depends: CP-01, CP-22
  - Work: It sits here, before the Chanter items, because the Chanter is its first user:
    CP-32 ranks the two ceremony weapons with the stat this item defines. Java first:
    Equipment.java's equip checks (C# twin
    src/Aion.GameServer/Model/GameObjects/Player/Equipment.cs:45-105, 333-346). Add the
    table form of NaturalGearRules: wearable weapon groups and armor types read from the
    class's mastery rows in the class-line contract; restrict column equal to the class id;
    weapon by the profile's groups and one defined ranking stat (physical: the mean of
    minimum and maximum damage plus the item's flat physical-attack bonus, per swing;
    magical: magic boost, then maximum damage); armor by the profile's type order and then
    item level; one score for equip, keep, sell and reward choice; off hand none. Candidates
    the class cannot wear are filtered out before the server is asked, so a refused item no
    longer blocks a wearable one. ChooseReward prefers the class's weapon group and armor
    type, then the profile's consumable order. At Q2100 the Warrior's pick is a weapon, not
    the shield (CP-Q10, answered 2026-10-07). The protected supplies are the life potions
    and the items of the help kit; bandages are not among them. The Priest and the Cleric
    keep the rules of CP-22 and never use the table form.
  - Proof: Unit test UT/NaturalClassGearRuleTests: for each of the five new starters'
    default rules, the pick at the ten class-dependent Ishalgen reward quests (2100, 2002,
    2134, 2001, 2005, 2006, 2007, 2129, 2117, 2124) is the class's weapon group, armor type
    or consumable, the Warrior's pick at Q2100 is a weapon, and no wearable item or
    protected supply is marked sell; and the physical stat as CP-Q7 defines it ranks the
    two Karmic ceremony weapons (by the default, per swing, the staff's mean of 73 is above
    the warhammer's 55 plus 7).
  - 2026-10-07: done. UT/NaturalClassGearRuleTests is new and passes, 16 tests. Gate run
    gate-a1, set all: verdict pass (run/cp/CP-29/gate-a1/verdict.json). All seven scopes are identical to their baselines: p 35,811 records, m 123,112, b 128,937, l1 30,693, c 96,166, hm 39,564 and ax 15,762.
    The item asks for the unit test only. The gate was run over every kept scope because
    the equipment check, the keep-or-sell plan and the reward choice are shared by every
    leg.
    - **Java, read first.** Equipment.java:85-115 (C# twin Equipment.cs:63-105, 333-346):
      an item is refused without a mastery skill of its group
      (checkAvailableEquipSkills, lines 323-334), without a level in the class's column of
      the restrict row (ItemTemplate.getRequiredLevel, 0 reads as never), above the
      restrict_max level, or for another race. SkillData.getMasterySkills gives the skills
      by weapon group, by armor type and for the shield; a group with no mastery skill is
      free. Every starter has the robe mastery 103 and the clothes mastery 40.
    - **Sc/Classes/NaturalClassGearTable.cs** is new. A table is five hand-written facts:
      the class, the weapon groups it holds, the stat that ranks a weapon, the armor types
      it wears, best first, and its consumable order. Rules(contract) reads the class's
      mastery rows from the class-line contract (for a second class its starter's rows
      with its own) and returns NaturalGearRules. A table that names a group or a type the
      class has no mastery for is refused by name.
    - **The defaults of the five starters (CP-Q10).** Warrior: SWORD or MACE by the
      physical stat; CHAIN, LEATHER, ROBE, CLOTHES. Scout: DAGGER, physical; LEATHER, ROBE,
      CLOTHES. Mage: SPELLBOOK, magical; ROBE, CLOTHES. Engineer: GUN, magical; LEATHER,
      ROBE, CLOTHES. Artist: HARP, magical; ROBE, CLOTHES. Each holds one weapon and
      nothing in the off hand, and each takes a life elixir, then a mana elixir, then a
      power shard at a consumable reward. The order of robe and clothes below a class's
      own type is this item's choice; no shipped reward decides it.
    - **The one score.** A weapon of the class's groups: physical is minimum plus maximum
      damage plus twice the flat physical-attack bonus (twice the per-swing value of
      CP-Q7, so it stays whole), then item level; magical is magic boost, then maximum
      damage. Armor: the type's place in the class's order, then item level. An accessory:
      item level. NaturalItem gains PhysicalAttack, read from the template's unconditional
      PHYSICAL_ATTACK lines, and the tooltip view gains the weapon's damage and the same
      bonus, so the plan and the equipment check compute the same number.
    - **The equipment check** (NaturalGearPolicy.SelectUpgrades) under a table rule: a
      candidate of a group the class has no mastery for is dropped before the server is
      asked; the hand takes the best weapon of the class's groups when it beats the held
      one; nothing goes in the off hand; every other slot is ranked by the score. Under
      the Priest's and the Cleric's rules it ranks by item level and asks, as before.
    - **Keep or sell** under a table rule adds two holds: every accessory
      (accessory-kept; the equipment check wears accessories by item level), and gear for
      a later level that beats the slot's best (gear-for-later). So "no wearable item is
      marked sell" is read as: what is sold is something the class will never wear, or
      something another owned item of its slot beats or equals. A replaced starter weapon
      is still sold as surplus-gear.
    - **The reward choice** under a table rule: a weapon of the class's groups that beats
      the held one, then armor of its types that beats the worn piece, an item for a later
      level counted; with no upgrade offered, the class's own gear before another class's,
      then the consumable order, then the sale price.
    - **The picks at the ten class-dependent rewards**, as the test plays them from the
      starter kit. Q2100, Q2002, Q2134: Warrior the three maces (100100024, 100100493,
      100100025; 50, 60 and 74 by the stat against the swords' 46, 56 and 70); Scout the
      daggers (100200125, 100200604, 100200126); Mage the spellbooks (100600047,
      100600531, 100600048); Engineer the pistols (101800194, 101800505, 101800195);
      Artist the harps (102000207, 102000522, 102000208). Q2001, Q2005, Q2006, Q2007,
      Q2129: Warrior chain (114500766, 113500762, 114500767, 110501156, 111500751); Scout
      and Engineer leather (114300804, 113300791, 114300805, 110301182, 111300768); Mage
      and Artist robe (114100794, 113100773, 114100795, 110101250, 111100763). Q2117 and
      Q2124: the Minor Life Elixir 162000052 for all five. At Q2100 the Warrior takes the
      mace and never the shield 115000024, also when it already holds the mace.
    - **Supplies.** The six life potions and elixirs and the items of the level 1-9 kit.
      The bandage 169300002 is not one and is sold at a vendor visit.
    - **Proof, UT/NaturalClassGearRuleTests.** The five defaults; for each starter, what
      Wears says for every item group equals the server's mastery rule over the class's
      level-1 mastery skills (SkillData.GetMasterySkills on the shipped skill data); the
      ten rewards played in level order for each starter, each pick the class's weapon
      group, top armor type or the life elixir, with the keep-or-sell plan checked after
      every pick and every equip; the Warrior at Q2100; a level-7 Mage takes Q2134's
      level-8 spellbook and holds it until level 8; the two Karmic weapons load as 58-88
      and 44-66 plus 7, and a Chanter table ranks the staff first (146 against 124); the
      plan's score equals the tooltip's for every gear item of the test; the equipment
      check for a Warrior, a Mage and a Scout, against the same bag without a rule; the
      refusals. The Priest's and the Cleric's rules are shown not to be tables.
    - **Findings, not fixed.** (a) The starter mana potion 162000007 and the Minor Mana
      Elixir are not supplies under a table rule, because the item names the life potions
      and the kit only, so a vendor visit sells them. The Priest's rules keep 162000007. A
      caster's profile item should decide this before its first vendor visit. (b) A
      second class's expected mastery ids drop the starter masteries the new ones replace
      (the Chanter: 40, 46, 48, 49, 50, 89, 106). No run has shown yet that the server
      removes a replaced mastery from the skill list. CP-32 uses it first. (c) Armor type
      comes before item level, as the item says: a class keeps a level-4 piece of its own
      type over a level-8 piece of a lower type.
    - Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,814 with 16 skipped),
      and Fast passes (run cp29-fast). The guard of rule (c), gate p, is in the gate run.
- [ ] **CP-29a - The Priest and the Cleric on the table gear rules.** Depends: CP-29
  - Work: The operator, 2026-10-07: "I'd like the Priest/Cleric to be no different from the
    others. Nothing special except of course what needs to be different (playstyle,
    mechanics, etc)." This replaces the last sentence of CP-29's Work. Give the Priest and
    the Cleric a NaturalClassGearTable each and build NaturalGearRules.Priest and .Cleric
    from them, as every other class's rules are built. The Priest: MACE, magical. The
    Cleric: STAFF before MACE, magical, so the operator's staff rule (AX-Q1: always a
    staff, the one with the most magic boost) becomes the table's weapon order and not a
    branch of its own. Then remove what only the two old rule sets use: HandRuleGroup and
    the staff branch of the equipment check, the class-blind reward choice, and the
    accessory branches that the table's holds replace. What stays different is data, not
    code: the two tables, the Cleric's supplies (the approved help items of OD-13), and
    the legs' own protected items (coin gear, Haramel), which are leg rules. The Cleric's
    shield waits for the off-hand mode of CP-68, as the Warrior's does; until then the
    staff fills both hands, as it does today.
  - The operator's gear rules, 2026-10-07, given with the answer to CP-Q22. They are the
    tables of this item and outrank the defaults of CP-Q7 and CP-Q10 where they differ:
    - "Port over where our cleric/priest uses the same as others, without needing special
      things. If our generic stuff can't compensate then we need a better system, as every
      class plays the game slightly differently." So where a table cannot say what a class
      needs (a two-handed weapon, a shield, a rule that changes at Ascension), the table
      form is widened for every class; no class gets a branch of its own.
    - Priest types (Priest, then Cleric or Chanter): leather before Ascension, chain
      after. After Ascension the staff with the most magic boost, for the Chanter as for
      the Cleric; this replaces the physical stat of CP-Q7's default for the Chanter, and
      the pick is the same Karmic Staff. Before Ascension the operator is not sure; the
      Priest keeps the mace by magic boost, as it plays today, until told otherwise.
    - Mage types (Mage, then Sorcerer or Spiritmaster): cloth, which is the robe in the
      item data, and the spellbook.
    - Warrior types (Warrior, then Gladiator or Templar): chain before Ascension, plate
      after. The Templar takes sword and shield; the Gladiator the two-handed sword (the
      GREATSWORD group). The shield and the second hand are built in CP-68; this item
      writes the tables so that CP-68 adds no class branch either.
    - Scout, Engineer and Artist: no new word; the defaults of CP-Q10 stand.
  - Proof: Neutral gate, set all. A changed pick changes a trace, so differing scopes are
    expected: today the Priest has no armor type order, and from this item on it takes
    leather first. List each scope's first difference in this item. The Priest and Cleric
    scopes are then re-recorded once (CP-Q22, answered yes on 2026-10-07), and the loop
    prompt's sentence that the journey plays exactly as CP-08 and CP-09 recorded it counts
    from the new baselines. The snapshots are not recaptured: a restored character keeps
    what it wears and what it picked, and the new rules decide from there.
  - 2026-10-07: the code is done and the gate has listed the differences; the re-record
    follows from this commit. No server behavior is new to this item: the mastery check
    of Java Equipment.equipItem was read for CP-29.
    - **One rule set for every class.** NaturalGearRules.Priest and .Cleric are built by
      NaturalClassGearTable.Priest and .Cleric (Sc/Classes/NaturalClassGearTable.cs), as
      every other class's rules are. The Chanter's table moved beside them. Removed:
      AfterAscension, HandRuleGroup, IsTable, OffHandGroups, the staff branch of
      NaturalGearPolicy.SelectUpgrades, ChooseStaffReward (no journey called it), the old
      reward choice, the profile's RewardGear (a class's rewards are scored by its own
      rules), and the accessory-kept hold.
    - **The table was widened three times, for every class.** (1) The weapon groups are
      an order: a weapon of an earlier group outranks every weapon of a later one, and
      the class's stat ranks within a group. The staff rule is the Cleric's order, STAFF
      then MACE. The Warrior's order is SWORD then MACE, so it now takes the three swords
      (100000107, 100000639, 100000108) where CP-29 named the maces by the stat; the
      operator named the sword for both Warrior second classes. (2) A table names the
      consumables the class keeps beside the life potions and its kit: the mana potions
      for the Priest, and for the Cleric and the Chanter also what is left of the level
      1-9 kit and every help item the help-item policy knows. This closes finding (a) of
      CP-29 for the Priest types; a Mage's table names its own in CP-46. (3) Armor is
      ranked by item level first and by the type's place second (CP-Q24). CP-29 ranked
      the type first, which kept a level-1 leather piece over a level-8 robe piece; the
      recorded human Priest wore the level-8 robe leggings over the level-1 leather. So
      the operator's "leather, then chain" decides between pieces of one item level,
      which is what every Ishalgen armor reward offers. This replaces finding (c) of
      CP-29.
    - **Keep or sell has one path.** A leg's protected items, its retained weapon and an
      open quest's needs are honored whenever the leg gives them; they are leg rules and
      no longer ask for a class. The retained weapon is every item that goes in the
      class's hands: its weapon groups, and a shield when it has the mastery. The bridge's
      supplies are kept at every level. An accessory is held for a later level or sold as
      surplus, for every class, as the Cleric did; gear for a later level is held while
      it beats the slot's best. A shield is no class's gear until CP-68, so outside the
      coin-gear and Haramel legs an unworn shield is sold.
    - **Gate, set all, run gate-a1** (run/cp/CP-29a/gate-a1/verdict.json), against the
      baselines of be9837cd8: every journey ran to its end. l1 and hm are identical.
      - p, m and b first differ at record 5,845, 7,012 and 5,845, at Q2001's reward: the
        Priest sends reward 2, Boromer's Boots 114300804 (leather), where it sent reward
        1, Boromer's Shoes (robe). After that it takes Ulgorn's Mace 100100493 at Q2002
        and wears it, where the old choice took Ulgorn's Dagger, which the server
        refused to a Priest; and the leather piece at Q2005, Q2006, Q2007 and Q2129
        (113300791, 114300805, 110301182, 111300768). The level-10 Cleric of b takes and
        wears chain at the four it claims as a Cleric: 113500762, 114500767, 110501156,
        111500751. Q2100, Q2134, Q2117 and Q2124 keep their picks. No other pick of any
        scope changes but c's.
      - m, the Priest to Munin: 112,397 records against 123,046; no death where the
        baseline has one; 36 life potions against 69; no emergency decision against 7;
        103 pull plans against 148. p: 37,222 records against 35,789. b: 144,048 against
        128,869, with 36 patrol waits against 21 and 132 pull plans against 112; its
        Cleric spent about 12 minutes of wall time on rejected route searches at Q2005's
        stalker step and then went on.
      - c first differs at record 23,764, Q2224's reward: reward 4, Altgard Legionary
        Handguards 111501698 (chain, level 16), where it sent reward 1, the robe gloves.
        Its record count and all 21 step counts are the baseline's.
      - ax first differs at record 94: the first inventory check wears the same items in
        another order (the chausses before the earrings). The 14 equips are the same
        items in the same slots, and the record count and step counts are the baseline's.
    - **Existing tests, edited and not extended (rule (n)).** NaturalGearPolicyTests: the
      tooltip rows carry their item group and the maces their magic boost, the staff
      rows pass the Cleric's rules, and the ChooseStaffReward test went with the method.
      NaturalIshalgenInventoryPolicyTests: a Priest keeps the Karmic Staff as the
      bridge's supply. SimulationAbyssEntryContractTests passes the Cleric's rules to the
      equipment check. The capital probe PC-04 built its own tooltip without the item
      group, so the first Fast run (cp29a-fast) failed there: the rules do not ask the
      server for an item whose group they cannot read. It now reads the tooltip the
      journey reads and passes the Cleric's rules.
    - Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,675 with
      16 skipped) and Fast passes on its second run (cp29a-fast-a2), after the probe edit.
- [x] **CP-30 - Parameterize the capital scenario by the contract.** Depends: CP-08, CP-25
  - Work: This item edits an existing scenario file, so it waits for the first baselines
    (CP-08). Parameterize CapitalAscensionScenario.RunAsmodianAsync by the contract: the
    dispatch id (literal 2904 at Sc/CapitalAscensionScenario.Asmodian.cs:21), the class
    check and its message (lines 107-108), the Q2009 var (literal 40 at line 133), the step
    keys by role, and an optional short endpoint that stops when Doman's SETPRO1 has moved
    the dispatch quest from var 0 to var 1 (after line 156, before the AIRLINE_SERVICE
    teleport of the same step). With the default contract and no short endpoint the scenario
    does what it does today. Fast does not run it: CAPITAL-ASMO is tier Full
    (parity-artifacts/e2e/scenarios.json:738-740). So the existing scenario, run alone, is
    this item's proof and not a second run beside one.
  - Proof: The existing CAPITAL-ASMO scenario still passes. The full command is
    AION_SIM_DB_INTEGRATION=1 AION_SIM_TIER=Full AION_SIM_SCENARIO=CAPITAL-ASMO
    AION_E2E_RUN_DIR=<a new folder under run/cp/CP-30 that holds a run.json> dotnet test
    tests/Aion.Simulation.Tests --filter
    "FullyQualifiedName~ManifestScenariosRunInFixedProcessOrder" (the pattern of
    docs/natural-ascension-altgard.md:2442-2445; run/na02/evidence-capital/run.json is an
    example of the file, with the fields run, gitSha and configProfile).
  - 2026-10-07: done. CAPITAL-ASMO, run alone with the item's command and the run folder
    run/cp/CP-30/capital-a1: passed, all 12 steps (s01 setup to s12 the Meiyer and Suthran
    turn-ins), and its problem policy asserted clean with no observation
    (sim-problems.jsonl). Guard run guard-a1, set p: verdict pass
    (run/cp/CP-30/guard-a1/verdict.json), 35,811 records, identical to its baseline.
    - **Java.** Nothing new was read: the scenario sends what it sent, and CP-25 read the
      handlers that decide the class choice, the ceremony and the dispatch quest.
    - **Read from the contract now** (Sc/CapitalAscensionScenario.Asmodian.cs): the
      dispatch quest id in place of the literal 2904; the class the choice must give, as
      the endpoint's class id, with a message that names the contract's second class
      ("Cleric" for the reviewed contract); the Q2009 var the ceremony waits for, as the
      ceremony step's var in place of the literal 40. The four class-dependent steps are
      found by role (class choice, ceremony, dispatch start, dispatch reward). The eleven
      other step keys stay literal: they are the same for every pair.
    - **The short endpoint** is the new optional argument stopAtDispatchStart. When set,
      the Doman step stops once SETPRO1 has moved the dispatch quest from var 0 to var 1,
      before AIRLINE_SERVICE is sent; the driver is synchronized, its own check runs, and
      the bind and the two turn-ins are not played. Nothing sets it yet; CP-31's probe is
      its first user.
    - **Unchanged on purpose.** The step labels the driver prints (choose-cleric-...,
      ...-lyfjaberga-ceremony) are the same for every contract: they are labels of the
      scenario's steps, not facts of the pair. The SIM host
      (SimT/SimulationCapitalScenarioTests.cs) still checks a Cleric at the end; it runs
      the reviewed contract only.
    - One failure text changed: the ceremony payout check names the contract's ceremony
      item id, not the Karmic Staff. It is written only when the check fails.
    - Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,814 with 16 skipped),
      and Fast passes (run cp30-fast). Fast does not run CAPITAL-ASMO, as the item says.
- [x] **CP-31 - Class choice on a prepared character: Cleric and Chanter rows.** Depends:
  CP-30
  - Work: First allocate SIM probe accounts. No id is known to be free: ids 77-82 belong to
    the gear scenarios (SimT/SimulationGearScenarioTests.cs:34) and 91-94 to the geo
    displacement scenarios (SimT/SimulationGeoDisplacementScenarioTests.cs:30-33; 94 is also
    used at SimT/SimulationNaturalRoadTests.cs:19). A grep for literals is not enough,
    because several tests compute their ids (account + 1, 62 + i, 101 + index). So either
    find free ids by reading every session construction, computed ids included, or add new
    ids to the fixture's list (the pool expression at
    SimT/SimulationWorldFixture.cs:204-206; 98 and 100 are not defined today, and ids above
    255 cannot log in). Record the assignment in the comment at
    SimT/SimulationWorldFixture.cs:176-203 and in the doc, and pick letters-only names. Java
    first: SETPRO13 (_2008Ascension.java:153-154), chanter_selectable_reward
    (quest_data.xml:9302-9357) and the Q2904 handler, which starts the quest itself when
    Q2009 completes (_2904DispatchtoAltgard.java:73-76); Doman's SETPRO1 only moves var 0 to
    1 (lines 43-52). Add the gated SIM theory NaturalClassChoiceProbe, a partial of
    SimulationFastScenarioTests that reuses SimCapitalDriver and the scenario as CP-30
    parameterized it, with its rows chosen by CP_PROBE_ROWS (section 8); it adds no manifest
    id, so parity-artifacts/e2e/scenarios.json and the system matrix that
    scripts/e2e/test-code-coverage.py:189-200 checks are untouched. The ceremony weapon the
    chanter row takes is the pick of CP-Q7; its default, the Karmic Staff, applies while no
    Answer line stands under it. This is the first item that uses that pick.
  - Proof: SIM probe NaturalClassChoiceProbe, rows cleric and chanter: SETPRO13 gives class
    id 11 and the six level-9 masteries, the chosen Karmic weapon is paid to a class-11
    character and the server template's chanter_selectable_reward for Q2009 holds that item,
    Q2904 starts at START/0 with the ceremony and reaches var 1 at Doman, and the cleric row
    passes on the same parameterized route. The payout cannot tell the two lists apart: for
    Q2009 priest_selectable_reward and chanter_selectable_reward hold the same two items in
    the same order (quest_data.xml:9351-9354). That a Chanter reads the second list is
    proven by reading the code
    (src/Aion.GameServer/Model/Templates/QuestTemplate.cs:184-187), not by the probe.
  - 2026-10-07: done. Probe run probe-a1, rows cleric and chanter, first attempt: both
    pass (run/cp/CP-31/probe-a1.log; the command is AION_SIM_DB_INTEGRATION=1
    CP_PROBE_ROWS=cleric,chanter dotnet test tests/Aion.Simulation.Tests --filter
    "FullyQualifiedName~NaturalClassChoiceProbe"). Guard run guard-a1, set p: verdict pass
    (run/cp/CP-31/guard-a1/verdict.json), 35,811 records, identical to its baseline.
    - **The probe accounts are 98 and 100.** Every session construction under
      tests/Aion.Simulation.Tests was read, computed ids included (the L0 actors take 1 to
      the scenario's bot count, combat 17-35, the quest plans 39 and 40, gathering 42 and
      43, cooking 47-50, social 62-76, gear 77-82, geo displacement 91-94, lifecycle 101 on).
      No accepted id was found free, so the two ids below 256 that the fixture did not
      accept were added to its list (SimT/SimulationWorldFixture.cs, with the comment). A
      SIM account must be fresh ("Fresh simulation account ... already has a character")
      and each test process has its own schema, so a probe plays two rows to a process:
      **a later probe with more rows (CP-34's six, CP-67's nine) runs them in several
      filtered processes.** The names are Asimpickcleric (98) and Asimpickchanter (100).
    - **Java, read first.** _2008Ascension.java:153-154: SETPRO13 sets CHANTER at var 6.
      quest_data.xml: Q2009's priest_selectable_reward and chanter_selectable_reward each
      hold 100100495 then 101500498. _2904DispatchtoAltgard.java:22 and 73-76: the handler
      registers on quest completion and starts itself by the default rule when Q2009
      completes; lines 43-52: Doman's SETPRO1 moves var 0 to 1 and closes the dialog; the
      turn-in at Meiyer comes after. SkillLearnService.java: only skill 30001 is removed
      on a level change; a mastery that a new one replaces in the skill tree is not
      removed, in Java as in C#.
    - **SimT/SimulationNaturalClassChoiceProbe.cs** is new: the gated theory
      NaturalClassChoiceProbe, a partial of SimulationFastScenarioTests. A row runs only
      when CP_PROBE_ROWS names it. It reuses SimCapitalDriver and the scenario of CP-30,
      with the row's bridge from NaturalAscensionContract.ForChoice and the short
      endpoint. It adds no manifest id; e2e/scenarios.json is untouched.
    - **What the probe prepared.** The character is created by packets as a Priest. The
      test then sets it to level 9 and places it by Munin (the scenario's own setup step,
      as CAPITAL-ASMO does). Every step after that is a client action. No director command
      is used.
    - **The chanter row** (account 100): SETPRO13 gave class CHANTER, id 11, on the server
      and in the client's view; the character is a Daeva at level 10; the six level-9
      masteries 46, 48, 49, 50, 89 and 106 are on the server and in the skill list the
      client was sent; Q2008 and Q2009 are complete; the Karmic Staff 101500498 is in the
      inventory, paid by SELECTED_QUEST_REWARD2, and the server template's list for a
      Chanter at Q2009 holds it at that place; Q2009's reward group is 3, the one that
      starts Q2904; Q2904 was at START/0 after the ceremony and is at START/1 after Doman;
      all four quest movies were played and answered; the log policy is clean, with
      protocol warnings and the audit log set to fail. The pick is CP-Q7's default; this
      is its first use.
    - **The cleric row** (account 98) passes on the same route with SETPRO14, class id 10
      and priest_selectable_reward.
    - **Seen on the way.** Both characters still hold the Priest's four masteries 39, 40,
      41 and 103 beside the six new ones. That is Java's behavior (above). It settles
      finding (b) of CP-29: a table's expected mastery ids, which leave out the replaced
      ones, are a subset of what the server keeps.
    - The step labels the driver prints say "choose-cleric" for the chanter row too; they
      are the scenario's fixed labels (CP-30).
    - Bundle: the seven pre-commit checks pass and Fast passes (run cp31-fast).
      Aion.GameServer.Tests was not run: tests/Aion.Bots did not change.
- [x] **CP-32 - Chanter line and profile.** Depends: CP-21, CP-23, CP-24, CP-27, CP-29
  - Work: Add line priest-chanter with the Priest line's account and name, and
    Sc/Classes/NaturalChanterProfile.cs: the Priest catalog through the Priest adapter and
    the Priest rest plan, because a Chanter keeps the Priest's learned skills; the Priest's
    named distances and thresholds (CP-18, CP-21) and the Priest line's restock table
    (CP-24); every level-10 Chanter active excluded with the reason that no Chanter leg
    exists yet, the powder skills 246 and 249 among them; gear rules in the table form of
    CP-29 that wear the chosen ceremony weapon by the stat of question 7, whose default
    applies while no Answer line stands under it (the staff-by-magic-boost rule stays the
    Cleric's). Help items are on, as for every line: at levels 1-9 the kit and its use are
    the Priest's (CP-05, CP-06), and at level 10 the supply follows the allowlist's level-10
    bands, which are keyed by level and not by class. The Chanter has no kit of its own
    yet. Write both ceremony weapons' numbers into the doc: Karmic Warhammer 44-66 at 1.5 s
    with +7 physical attack, Karmic Staff 58-88 at 2.0 s (item_templates.xml:21160-21164,
    110046-110050).
  - Proof: Unit test UT/NaturalChanterProfileTests: catalog ratchet to level 10 (every
    auto-learned active is cast or excluded with a reason); identity accepts a level 9-10
    Chanter on the bridge maps under priest-chanter and refuses it under priest-cleric and
    on every leg-scoped map; the gear rules wear the chosen weapon; the help kit at levels
    1-9 equals the Priest's.
  - 2026-10-07: done, without the unit test (rule (n)). Proven by the build, the bundle and
    the guard: run guard-a1, set p, verdict pass (run/cp/CP-32/guard-a1/verdict.json),
    35,811 records, identical to its baseline. CP-33's capture is the first run that plays
    the line and the profile.
    - **Java.** Nothing new: SETPRO13, the ceremony and the dispatch quest were read and
      probed for a Chanter in CP-25 and CP-31. skill_tree.xml gives a Chanter, by itself
      and up to level 10: the six level-9 masteries; the passives 141 and 359; and nine
      skills that are cast or toggled, 246, 249, 1562, 1638, 1685, 1699, 1715, 1778 and
      the toggle 1809.
    - **The line** priest-chanter is in NaturalClassLine.All: Priest, then Chanter, on
      account 41 as Asimnjour, the accepted line's. A run plays one line on its own
      schema, so the two never meet.
    - **Sc/Classes/NaturalChanterProfile.cs** is new. The Chanter fights, rests and moves
      as the Priest it was: the Priest catalog through the Priest adapter, the Priest rest
      plan, the Priest line's ranges, readiness, movement, campaign numbers and restock
      table, the Blessing upkeep, and the Priest's patrol rule (CP-Q14's default). All
      nine of its own cast or toggled skills are excluded with a reason: no Chanter leg
      exists yet; Light of Resurrection because the bot plays solo. Help items: the kit
      of every level, as the Cleric's, so the level-10 bands are supplied at the ceremony.
      To share the Priest's parts, seven members of NaturalPriestProfile and its adapter
      went from private to internal; nothing else in that file changed.
    - **Gear and reward picks, before its first run.** Table form (CP-29): MACE or STAFF
      by the physical stat; CHAIN, LEATHER, ROBE, CLOTHES; no off hand; consumables life
      elixir, mana elixir, power shard; supplies the six life potions and the kit of every
      level. The two ceremony weapons: Karmic Staff 101500498, 58-88 at 2.0 s, no
      physical-attack line (its bonus is 13 physical critical), 73 a swing; Karmic
      Warhammer 100100495, 44-66 at 1.5 s with 7 physical attack, 55 plus 7 a swing. By
      CP-Q7's default the Chanter takes and wears the staff, over the Aldelle Mace
      (29-45, 37 a swing) it holds at Munin. Before the class choice its picks are the
      accepted Priest's, by the Priest's rules.
    - **One rule made exact.** ChecksGearAfterIshalgenTurnIns was "every line but
      priest-cleric" (CP-23, CP-Q13's default). It is now "every line whose starter is not
      the Priest". CP-Q13's reason is a new class with another rest plan; the Chanter
      line's levels 1-9 are the accepted Priest's, and CP-33 expects its trace to equal
      baseline p up to the class-choice send. For priest-cleric the answer is unchanged.
    - **Not done here, by rule (n):** the catalog ratchet and the identity rows of the
      Proof line. CP-27's code and CP-31's probe cover the identity; the exclusions above
      were read from skill_tree.xml by hand.
    - Bundle: the seven pre-commit checks pass, Aion.GameServer.Tests passes (4,676 with 16 skipped),
      and Fast passes (run cp32-fast).
- [x] **CP-33 - The Chanter diverges at Munin, preserved after the ceremony.** Depends:
  CP-28, CP-31, CP-32
  - Work: With the code committed and the bundle green, run sim-snapshot.ps1 -Action Capture
    -CapitalStage start -Class priest-chanter -Name pandaemonium-chanter-start-s1 with its
    own -Run: a fresh Priest plays as the accepted line does, sends SETPRO13 at Munin, takes
    the ceremony reward and stops at the capital-start checkpoint (J:1400-1405). Record the
    comparer's first difference from baseline p (it should be the class-choice send) and the
    receipt. Acceptance, judged from the receipt before Verify: class id 11, level 10, Q2008
    and Q2009 complete, the chosen weapon worn, Q2904 at START/0, and help-items.json lists
    what was supplied. A capture that fails it is handled by rule (l): it keeps its name, is
    logged as rejected, and the second attempt is captured as
    pandaemonium-chanter-start-s1-a2. Then the evidence-only commit (rule (d)). Two attempts
    at most, then Blocked. No class scope is recorded for the Chanter: its run is the
    Priest's up to the class choice, and gate p guards that.
  - Proof: sim-snapshot.ps1 -Action Verify -Name <the accepted name> passes: the restore
    emits CP_CLASS=priest-chanter and PC_CAPITAL=start and no NA_HELP_ITEMS, the resumed
    character is accepted as a level-10 Chanter, and capital-stage-completion.json is
    verified again for stage start.
  - 2026-10-07: done, first attempt. Snapshot **pandaemonium-chanter-start-s1**, captured
    at da2a7d3b2 by run cp33-chanter-a1 (run/cp/CP-33/capture-a1.log; evidence
    run/snapshots/_capture/cp33-chanter-a1). Character 133276, 3,875,166 game ms, dump
    SHA-256 7eaa7c63faa333d12d77c58b90de1bf4dacc89ab0851ef923cf4c15b08779063, receipt
    SHA-256 867ac81be5aece0ead4b6a7e70186c117f30dac10c26fab21bdb144e90c39b3b, classLine
    priest-chanter in snapshot.json.
    - **Acceptance, from the receipt before Verify:** stage start, verified; class id 11;
      level 10; map 120010000; Q2008 and Q2009 complete; the Karmic Staff 101500498 worn
      in both hands; Q2904 at START/0; no death, two retreats. help-items.json lists the
      level 1-9 kit at the run's start (30 Major Life Potions, 30 Lesser Anti-Shock
      Scrolls, 20 Greater Running Scrolls) and the level-10 bands at the ceremony (40 mana
      serums 162000017, 8 of 160002273, 200 of the powder 169300003), as the per-class
      note foresaw. Accepted.
    - **The comparer against baseline p** (run/cp/CP-33/compare-p.txt): the first
      difference is record 35,065, the bridge decision at Munin, whose reason reads
      "Choose Chanter (SETPRO13)." where p reads "Choose Cleric (SETPRO14).". Every record
      before it is the accepted Priest's. The Chanter's trace has 35,809 records, p has
      35,811.
    - **Proof.** Restore prints NA_ASCENSION=1, PC_CAPITAL=start and
      CP_CLASS=priest-chanter and no NA_HELP_ITEMS (run/cp/CP-33/restore.txt; that copy
      was dropped). Verify, run cp33-chanter-verify-a1, passes
      (run/cp/CP-33/verify-a1.log): the resumed character is accepted as the level-10
      Chanter of the line, and capital-stage-completion.json is verified again for stage
      start, with Q2904 still at START/0.
    - This is the first run of the Chanter line, the Chanter profile and its table gear
      rules: the equipment check put the staff on over the Priest's mace.
    - No class scope is recorded for the Chanter, as the item says. No code changed, so
      this commit is evidence only and no bundle was run.

### D. What another class needs, and a Warrior in the field before the seam is closed

- [x] **CP-34 - Q2132 at the six trainers, on prepared characters.** Depends: CP-01, CP-31
  - Work: Java first: _2132ANewSkill.java:24-134 (register at 24-32, the level change that
    sets the var and reward group at 35-67, and the dialog with the refusal and the pages at
    72-134). Add the gated SIM theory NaturalNewSkillTrainerProbe, six rows on the probe
    accounts that CP-31 allocated, chosen by CP_PROBE_ROWS: a character of each starter is
    created by packets, raised to level 3 by the director (the level change starts Q2132 in
    REWARD), and placed by the trainers. It is refused at another class's trainer and paid
    at its own with the contract's var and page. It touches no journey code. A server defect
    is fixed Java-first under its own lettered item.
  - Proof: SIM probe NaturalNewSkillTrainerProbe: all six rows pass (Warrior at Minu 203527,
    Scout at Wiokan 203528, Mage at Jurwen 203529, Priest at Kirhen 203530, Engineer at
    801218, Artist at 801219).
  - 2026-10-07: done. All six rows pass on the final probe code, two to a process on the
    probe accounts 98 and 100 (run/cp/CP-34/probe-final-1.log warrior and scout,
    probe-final-2.log mage and priest, probe-final-3.log engineer and artist). The command
    is AION_SIM_DB_INTEGRATION=1 CP_PROBE_ROWS=<two rows> dotnet test
    tests/Aion.Simulation.Tests --filter "FullyQualifiedName~NaturalNewSkillTrainerProbe".
    Guard run guard-a1, set p: verdict pass (run/cp/CP-34/guard-a1/verdict.json), 35,811
    records, identical to its baseline.
    - **Java, read first.** _2132ANewSkill.java:24-32 registers the level change and the
      six trainers; 35-67: at the level change the quest starts, goes straight to REWARD
      and takes the var and the reward group of the starting class (1/0 Warrior, 2/1
      Scout, 3/2 Mage, 4/3 Priest, 5/4 Engineer, 6/5 Artist); 72-134: only the class's own
      trainer answers, with its page on USE_OBJECT (1011, 1352, 1693, 2034, 2375, 2716)
      and the quest end dialog on anything else; another class's trainer returns false.
      quest_data.xml: level 3, six reward groups of 275 experience each.
    - **SimT/SimulationNaturalNewSkillTrainerProbe.cs** is new: the gated theory
      NaturalNewSkillTrainerProbe, six rows chosen by CP_PROBE_ROWS. It touches no journey
      code and adds no manifest id.
    - **What the probe prepared.** The character is created by packets as the row's
      starter. The test then sets it to level 3, which starts Q2132, and places it two
      meters from a trainer, twice. Every talk is a client action. No director command is
      used: the level is set on the server object, as CAPITAL-ASMO sets level 9.
    - **Each row shows:** Q2132 in REWARD with the contract's var and reward group, on the
      client and on the server; the next class's trainer opens its ordinary page 10 for
      quest 0, not a Q2132 page, and the quest stays in REWARD; the class's own trainer
      opens the contract's page for Q2132 on the talk, and the journey's turn-in
      (QUEST_SELECT, then SELECTED_QUEST_NOREWARD) completes the quest for 275 experience.
      The log policy is clean, with protocol warnings and the audit log set to fail.
      Warrior at 203527 page 1011, Scout at 203528 page 1352, Mage at 203529 page 1693,
      Priest at 203530 page 2034, Engineer at 801218 page 2375, Artist at 801219 page
      2716. It is the first time a bot has talked to the Engineer's and the Artist's
      trainer.
    - **Attempts.** First attempt: warrior, scout and mage passed; priest, engineer and
      artist stopped in the probe's own lookup, which asked for exactly one object of the
      trainer's template (logs probe-a1-2 and probe-a1-3). The one small change: the probe
      takes the object nearest the contract's position and prints every one it saw. Second
      attempt: all pass.
    - **Finding, not fixed: two trainers exist twice.** The server holds two Npc objects
      for 801218 and two for 801219: the static spawn (objects 55500 and 55161, at z 278.5
      and 278.4) and a second one at the same x and y about 2.4 m higher (objects 132426
      and 132427, at z 281.0 and 280.7), with object ids allocated late in the world's
      start. Both answer the talk. The spawn data holds one entry for each, and no source
      names the two ids but the quest handler. The other four trainers exist once. What
      sets the two apart: their templates use ai passive_pattern, the other four general.
      The cause was not found and the Java server was not compared. It does not stop a
      journey that approaches the trainer by its shipped position. It is offered as a
      separate task, and it is a hazard for the Engineer's and the Artist's journeys
      (CP-60 on): an approach that asks for exactly one trainer object fails.
    - **Finding fixed the same day, in a separate task.** The second objects came from
      retail's gated spawns (`spawns/gated/gated_spawns.tsv`), which Java does not have.
      The table marks the placements this port already spawns, and the server skips
      those. It was generated on 2026-08-20; on 2026-09-26 the two static spawns were moved
      onto retail's x and y and the table was not regenerated, so its two rows still read
      "no static spawn here". Regenerated, both are skipped. The probe's engineer and
      artist rows now see one object each (55500 and 55161), and a census of the started
      SIM world holds no other npc doubled this way. The hazard for CP-60 on is gone.
      Details: `docs/retail-ai-fidelity.md`, "Two Ishalgen trainers stood twice".
      **The fix (6bdb96d66) is an outside commit that changes traces (CP-Q5).** Ten fewer
      objects are made at world start, so every later object id is ten lower. The gate for
      p at that commit fails on `fields.objectId` at record 1, with 35,789 records against
      the baseline's 35,811 (run/cp/CP34-dup/gate-p/verdict.json). The journey passes and
      all 21 step counts equal the baseline's. The other scopes were not run. The
      re-record rule of section 8 applies: the operator chooses between re-recording at
      HEAD and reverting.
      **Later the same day, by the operator's word, both trainers went back to their Java
      4.8 spots:** 801218 at (567.48, 2458.46, 278.249) and 801219 at (577.75, 2461.42,
      278.625). The class-lines file carries these positions, and retail's gated copies
      stay skipped. The engineer and artist rows see one object each at the new spots. This
      is a second outside commit that changes traces: any re-record should be taken at or
      after it.
    - Bundle: the seven pre-commit checks pass and Fast passes (run cp34-fast).
      Aion.GameServer.Tests was not run: tests/Aion.Bots did not change.
- [ ] **CP-35 - A skill catalog generated from the shipped data, and the profile
  validator.** Depends: CP-08, CP-14
  - Work: This item edits existing bot files, so it waits for the first baselines (CP-08).
    Java first: skillengine/properties/FirstTargetRangeProperty.java:19-64,
    model/ChainSkills.java:33-43, condition/ChainCondition.java:33-70 and
    model/Skill.java:160-161 (a cast with no chain category resets the open chain). Extend
    the skill record (Sc/NaturalPriestCombatPolicy.cs:7-21) with optional trailing fields:
    target kind, cast millis, required weapon groups, add-weapon-range, self count,
    activation, counter status, out-of-combat only. Chain time is not a new field: the
    record already has ChainWindowMillis (line 9), and the generator fills it. Give
    NaturalCombatObservation (Sc/NaturalPriestCombatPolicy.cs:58-68) two optional trailing
    fields as well, the main-hand weapon's attack range and its attack speed. They default
    to unset, no present caller passes them, and the Priest policy does not read them, so
    nothing changes for the accepted line; CP-39 fills them in the journey. They are added
    here because the table policy of CP-36 reads them. Add
    NaturalSkillCatalog.Build(StaticData, PlayerClass, roles, excluded), in memory. Range
    stays the raw template value; reach is range plus the main-hand weapon's attack range
    when the add-weapon-range flag is set, computed at decision time from the weapon fields
    of the observation. Add the validator every new profile must pass: each auto-learned
    ACTIVE or CHARGE skill has one role or an exclusion with a reason; a follow-up has an
    opener; counter, charge and out-of-combat skills are not in a rotation; no rotation line
    puts a non-chain cast between an opener and its learned follow-up; gear groups lie
    inside the class's masteries. The Priest and Cleric tables stay hand-typed and frozen.
  - Proof: Unit test UT/NaturalSkillCatalogTests: the generator reproduces
    NaturalPriestSkills.All and NaturalClericSkills.Cleric for the thirteen fields the
    record has today (id, level, role, mana, range, cooldown id, cooldown, chain category,
    required chain, chain window, DP cost, reagent id and reagent count; the role is
    supplied), so Hallowed Strike 1614 keeps range 1 although it adds weapon range. The new
    fields are asserted separately: the frozen hand-typed rows leave them at their defaults
    while the shipped data fills them (add-weapon-range and a weapon condition on 1614, a 2
    s cast on Healing Light), so whole-record equality cannot hold. The reach of Direct Shot
    2219 is 20 m with pistol 101800181 and of Ferocious Strike 2864 is 2.5 m with sword
    100000094; Surprise Attack 3196 is reported as a chain opener; the validator rejects one
    fixture profile per rule.
- [ ] **CP-36 - A table-driven combat policy for the new classes.** Depends: CP-19, CP-35
  - Work: Add Sc/Classes/NaturalRotationCombatPolicy.cs implementing INaturalCombatPolicy
    from one rule table per profile: ordered attack lists for adjacent and at range with
    opener and follow-up pairs; an upkeep list; a recovery ladder that may be empty, with HP
    percentages; a mana reserve only when a heal is learned; swarm and flee limits; an
    optional control skill before a retreat; the Cornered flag for scripted fights;
    auto-attack as filler or last resort, legal within the main-hand weapon's reach (the
    weapon fields CP-35 put on the observation); and a movement answer by the pull style of
    CP-19, so an unpulled target out of reach yields approach and never wait (the wait at
    Sc/NaturalPriestCombatPolicy.cs:338-345, which today ends only when the fight loop's
    1,000-action bound throws). Chain legality: a follow-up is cast only within its own
    chain time after the step before it (question 17); another first-step opener or any
    non-chain cast resets the chain; a self count allows a repeat, each within the step's
    time. The chain state the policy reads holds the current and the previous chain
    category, because Java accepts a required category that matches either
    (ChainCondition.java:40-46): Rage after Robust Blow is legal that way, and a state with
    one open category would never offer it. Decide and CandidateActions come from the same
    table. The actions it can return include the shield scroll, the life potion and the mana
    potion of the help kit, each with a fixture state in the unit test. The Priest, Cleric
    and Chanter do not use it.
  - Proof: Unit test UT/NaturalRotationCombatPolicyTests on synthetic profiles: across a
    state sweep the chosen action is always a legal candidate; a walk-in profile approaches
    an unpulled target; an empty ladder retreats at the flee limit; a ready follow-up is
    chosen before any non-chain cast; every rotation and ladder line of the fixture is hit
    by an example state.
- [ ] **CP-37 - Rest without a heal: potion, then sit.** Depends: CP-17
  - Work: The operator, 2026-10-07: "DO not use bandages, just use Potions, rest when potion
    is on cooldown if needed". Java first, and written into the doc: sitting restores HP
    every 6 s: (level + 3) x 8 x Health/100, cut to a whole number
    (JAVA/src/com/aionemu/gameserver/model/stats/container/PlayerGameStats.java:312-318;
    services/LifeStatsRestoreService.java:16). Health is 110 for the Warrior, 100 for the
    Scout and Engineer, 95 for the Priest and Artist and 90 for the Mage, so a tick is 32 HP
    at level 1 and 96 at level 9 only at Health 100. A life potion heals at once and then
    every 2 s for 20 s. It has a 30 s delay on use-delay group 11, which the mana potions
    share (item_templates.xml:830724-830759; skill_templates.xml:91905-91917). It is an
    item use, so it resets no chain. Add a second pure NaturalRestRules plan by question
    11's answer: when HP is below the HP target and a life potion is owned and ready, drink
    it; while the potion is on its delay and HP is still below the target, sit to the HP
    target, bounded like the mana sit; a mana target only for classes whose attacks need
    mana; a switch to the class's own heal once it is observed in the skill list. The
    potion is the one SelectOwnedPotion picks, so the kit's potion goes first. The plan has
    no bandage step. Write a state-sweep unit test for the new plan as part of the work,
    as CP-17 does for the Priest's plan; it runs in the bundle. The sweep goes over HP, MP,
    life potions owned, the delay of group 11, whether a potion's heal is still running,
    the quiet sits so far and whether the class's own heal is learned. It checks that the
    plan never asks for a potion when none is owned, when the group is not ready or while a
    potion's heal still runs; always ends in done or blocked inside the sit bound; sets a
    mana target only for a class that has one; and picks the class's own heal once that
    skill is in the list. Teach the rest executor the two new choices (drink a life potion,
    sit for health), so it never throws for a class without a heal. The Priest line's plan
    and code path do not change.
  - Proof: Neutral gate, set m+c. The new plan's decisions are covered by the state-sweep
    unit test; the executor's two new choices are first shown in play by CP-42.
- [ ] **CP-38 - Casting with any weapon.** Depends: CP-15
  - Work: Copy the ItemGroup to BotWeaponMotionType mapping of
    SimT/SimulationSkillSweepTests.cs:349-363 into Aion.Bots and leave the sweep's own code
    alone. CreateSpellCast (Sc/NaturalJourneyRuntime.cs:44-66) uses it and stops throwing
    for anything but mace, staff or bare hands. It also reads the off hand, as the sweep
    does at lines 359-360, so a dual-wielding Scout (Advanced Dual-Wielding I, skill 55,
    from level 5) gets the TwoWeapon motion. The TwoGun case is copied with it, but no
    starter reaches it at levels 1-9: the Engineer has no dual-wield skill, and the Gunner
    learns skill 55 at level 10. Today's main-hand read (equipped slot 1 or 3,
    Sc/NaturalJourneyRuntime.cs:48) does not match a shield or an off-hand weapon in slot 2,
    so neither can break it. Race and gender stay Asmodian male. Write a unit theory over
    every weapon group a starter or an Ishalgen reward can put in the main hand, with a
    check that motion_times.xml has the rows.
  - Proof: Neutral gate, set p+c: mace, staff and bare hands cast exactly as before.
- [ ] **CP-39 - Chain timing, swing and reach for table profiles.** Depends: CP-19, CP-36,
  CP-38
  - Work: Java first: PlayerController's attack rules (C# twin
    src/Aion.GameServer/Controllers/PlayerController.cs:391-429). For profiles on the table
    policy of CP-36 only: chain bookkeeping by the catalog's chain time and self count in
    place of the opener-never-expires rule (J:10090-10110), keeping the current and the
    previous chain category as Java's ChainSkills does (ChainSkills.java:33-43), so a second
    step such as Rage can follow another second step such as Robust Blow; swing interval and
    reach from the main-hand weapon in place of the fixed 2500 ms (J:9277-9281); an attack
    counter that wraps; the journey fills the two weapon fields that CP-35 added to the
    combat observation, the main-hand weapon's range and speed; approach and close-in
    through the movement helper of CP-19. The Priest line keeps its present swing and chain
    code.
  - Proof: Neutral gate, set m+c. This item edits the fight loop that CP-19 proves on m+c,
    and p holds few of its branches. The new paths are first shown in play by CP-43.
- [ ] **CP-40 - Walk-in pull for melee.** Depends: CP-19, CP-36
  - Work: Java first: AggroEventHandler.java:19, 52, the assist rule the planner mirrors.
    Add NaturalPullPlanner.WalkIn beside Plan (Nav/NaturalPullPlanner.cs:77-133) with unit
    facts: stage outside every aggro circle, list what AddsAt says would join at the
    target's position with melee reach (lines 58-69), nearest first, then close to reach.
    Plan keeps its signature, its 22 m cap (lines 85-86) and its filter. MoveToPullSpotAsync
    (J:5627-5775) picks the plan by the profile's pull style, which CP-19 added.
    PullAndKillAsync (J:8164-8282) gets a new branch for a walk-in profile; its existing
    fight-at-the-target block (J:8200-8244) is not moved or edited, because it leaves
    through returns, continues and a break and only scope hm reaches it (six at-target
    records in the hm07 capture trace; if CP-09 dropped hm, no gate scope reaches it). The
    fight-through in-reach test (J:5955-5957) and the 2 s spell-range wait in
    DefendAgainstEngagedAsync (J:5508-5517) branch on style.
  - Proof: Neutral gate, set m+c: the ranged path is unchanged. The walk-in itself is first
    played in CP-44.
- [ ] **CP-41 - Full gate after the refactor.** Depends: CP-16, CP-17, CP-18, CP-19, CP-20,
  CP-21, CP-22, CP-23, CP-24, CP-25, CP-26, CP-27, CP-28, CP-29, CP-37, CP-38, CP-39, CP-40
  - Work: No bot code changes. Every refactor item that edits code the Priest or the Cleric
    runs is ticked by now and no new class has fought yet (the Chanter line of CP-32 may
    land later; gate p guards it and CP-69 compares every scope again), so a difference
    found here belongs to the refactor alone. It does not wait for the Warrior: a stalled
    Warrior must not keep l1 and hm from being compared. Make the pin test fail if any row
    is still pending; every owner of a row is among this item's Depends. Run the whole check
    list of CLAUDE.md once. If a scope differs, follow the full-gate failure rule of section
    8; the item is not ticked until the gate passes.
  - Proof: Neutral gate, set all: every scope kept in CP-08 and CP-09 is identical to its
    baseline. Set all holds p, m, b, l1, c, hm and ax. Before the close-out l1 is compared
    nowhere else, and hm only in CP-18.
- [ ] **CP-42 - Starter probe harness, and the potion-and-sit rest shown in play.**
  Depends: CP-05, CP-21, CP-24, CP-29, CP-31, CP-36, CP-37, CP-38
  - Work: This is the first run of the rest executor of CP-37. A potion is an item use and
    needs no weapon motion, but every later row of this harness casts with the class's own
    weapon, which needs the mapping of CP-38. The rest is proven alone, so that a fault in
    the rest is not mixed with a fault in the fight. Add line warrior (starter id 0, no
    second class, the account and name of CP-Q19) and Sc/Classes/NaturalWarriorProfile.cs
    with everything but the fight: the generated catalog; the potion-and-sit rest plan with
    no mana target; potion and retreat by question 11; sword or mace by the physical stat,
    with the weapon and not the shield at Q2100 (CP-Q10); chain armor; no off hand; the
    help kit of CP-05, with Blitzopan in the shared speed slot unless the manifest says
    otherwise (CP-16 gives the help policy the speed-slot family as an input); the life
    potions and the kit's items protected, and the bandages not; the
    named distances and thresholds of CP-18 and CP-21; and its restock table in the form of
    CP-24 with question 11's numbers (elixirs from list 721, the Kinah floor). Its rule
    table holds one line for now, the weapon swing, and every active skill carries the
    exclusion reason "rotation added by CP-43", so the validator passes; no fight uses the
    table before CP-43. Add SimT/SimulationNaturalStarterProbeTests.cs, a partial of
    SimulationFastScenarioTests, with the theory NaturalStarterFieldProbe and the helpers
    every later row uses: a character created by packets on a probe account that CP-31
    allocated, the director's level change and HP cut, and the choice of rows by
    CP_PROBE_ROWS. A probe supplies no kit: the potion it drinks is one of the 100 Minor
    Life Potions a starter owns. Add a public entry beside RunObservedCombatAsync
    (J:96-124) that runs the journey's ordinary rest and nothing else. A fix to the rest
    executor made here follows rule (k): it came from CP-37, so gate m+c is run as the
    guard.
  - Proof: SIM probe NaturalStarterFieldProbe, row warrior-rest: a level-1 Warrior created
    by packets rests twice, and before each rest the director halves its HP. The first
    forced rest must show a life potion drunk. The director halves the HP again while the
    potion's 30 s delay still runs, so no potion is ready: that second rest must show a sit
    to the HP target and no potion. Neither rest throws.
- [ ] **CP-43 - Warrior profile, with the kill and chain rows.** Depends: CP-21, CP-24,
  CP-39, CP-40, CP-42
  - Work: Finish Sc/Classes/NaturalWarriorProfile.cs and take out the temporary exclusions
    of CP-42: walk-in pull; Ferocious Strike 2864/2865, then Robust Blow 2877/2878 within 3
    s, then Rage 2903 when hurt. Robust Blow and Rage are both second steps of Ferocious
    Strike's chain (precategory W_CHAINA_1TH_1; skill_templates.xml:48455, 48870). Rage may
    also follow Robust Blow, because Java accepts a match on the previous chain skill
    (ChainCondition.java:40-46), until Robust Blow's 3 s runs out (ChainSkills.java:42). So
    the table policy must keep the current and the previous chain category (CP-36, CP-39); a
    policy that keeps one open category, as the Priest's does (J:10106-10110), would never
    offer Rage after Robust Blow. Body Smash 2890 only when no follow-up is ready, because
    it is another opener and resets the chain; sword or mace swings as filler (a swing is
    not a skill and resets nothing). The rest of the profile stands as CP-42 wrote it:
    potion and retreat by question 11; sword or mace by the physical stat, chain armor, no
    off hand; the help kit; the potion-and-sit rest plan with no mana target.
    UT/NaturalWarriorProfileTests hold the catalog ratchet, the validator row and an example
    for every rotation and ladder line. Add rows warrior-1 and warrior-7 to the probe file.
    They are the first run of the chain and swing code of CP-39 and of the fight loop's
    approach to weapon reach; a fix to either follows rule (k), with gate m+c as the guard.
    If the probe's trace shows a swing the server refused in silence, decoding
    SM_ATTACK_RESPONSE becomes a lettered item under this one (rule (i)).
  - Proof: SIM probe NaturalStarterFieldProbe, rows warrior-1 and warrior-7. Row 1: a
    level-1 Warrior created by packets kills three Sprigg Workers (210363, 143 HP) through
    RunObservedCombatAsync (J:96-124) with Ferocious Strike and swings and never reaches the
    1000-action bound. The rests between its kills are the ordinary ones; the forced rests
    were shown by CP-42. Row 7: a director-leveled level-7 Warrior at half HP casts
    Ferocious Strike, Robust Blow and Rage in that order, Rage inside Robust Blow's 3 s, on
    one Fanged Karnif 210389 (478 HP, level 6) with no cast-start timeout. Karnif 210389 is
    the one that spawns in Ishalgen; 210655 (577 HP) has a template but no spawn.
- [ ] **CP-44 - Warrior to Q2132: the walk-in played in the journey.** Depends: CP-10,
  CP-20, CP-21, CP-23, CP-24, CP-27, CP-28, CP-34, CP-41, CP-43
  - Work: The maintainer's request asks for these fresh-create runs. It waits for the full
    gate of CP-41, so no new class journeys on a refactor that is not yet checked. Run
    sim-snapshot.ps1 -Action Replay -Class warrior -StopAt 2132:5:0 -Item CP-44 (seed 1,
    bridge off, help items on, the 45-minute deadline). A completed quest reads as status 5
    with var 0 (J:553; src/Aion.GameServer/Services/QuestService.cs:86-87) and the stop
    writes resume-receipt.json (J:875-891); CP-10 showed this stop on the Priest. On the way
    Q2132 is turned in at Minu 203527 with var 1, the Warrior is supplied its kit, and it
    binds at the village when it arrives there for work. Rule (e) applies. If walk-in fights
    draw adds, the body pull of question 15 is tried as a lettered item. This stop has no
    fallback: -StopAfterQuest exists only for Q2004 to Q2007 (J:141-142), so if the status-5
    stop does not land, the item goes under Blocked.
  - Proof: One contained run under run/cp/CP-44/<run-id>: resume-receipt.json shows class id
    0 and Ferocious Strike 2864 in the checkpoint's Skills, with Q2132 completed, and the
    trace holds at least one pull-plan record of the walk-in style followed by a kill. The
    ledger written into the doc says which kit items the Warrior used (the potion,
    Blitzopan, the shield scroll) and the lowest HP seen; a kit item never used is named as
    not shown. Class
    id 0 alone is not enough: it is also what the receipt holds when the class was not
    observed (Sc/NaturalJourneyCheckpoint.cs:12, 35).
- [ ] **CP-45 - Seam closed: the class-literal ratchet.** Depends: CP-41, CP-42, CP-43,
  CP-44
  - Work: No bot code changes. Add UT/NaturalClassSeamRatchetTests with
    e2e/natural-class-literals-baseline.json: per file under tests/Aion.Bots and
    tools/Aion.LiveBots, the count of PlayerClass.PRIEST, PlayerClass.CLERIC, IsCleric,
    NaturalPriestSkills., NaturalClericSkills. and NaturalPriestCombatPolicy. outside
    Sc/Classes and the frozen policy files; it fails on any rise. It waits for the Warrior
    items, so the counts are taken after a class unlike the Priest has rested, fought and
    journeyed on the seam and its fixes are in. List every remaining literal with its reason
    in the doc; the leg gates stay.
  - Proof: Unit test UT/NaturalClassSeamRatchetTests: green at the counts recorded in the
    baseline file, with one fixture case that shows it fails when a count rises.

### E. Profiles for Mage, Artist, Engineer and Scout

- [ ] **CP-46 - Mage profile.** Depends: CP-21, CP-24, CP-39, CP-42
  - Work: Add line mage and Sc/Classes/NaturalMageProfile.cs: stand-off pull at 22 m (its
    targeted skills reach 25 m, so the planner is unchanged); Flame Bolt 1282 then Blaze
    1403, Ice Chain 1363 then Frozen Shock 1226, each follow-up at once; Erosion 1447 only
    when no follow-up is ready; Stone Skin 1155 as upkeep before a pull (a 300 s shield for
    130 MP), never inside a pair; Root 1328 only on a second attacker or before a retreat,
    never on the monster being hit; ladder potion then retreat; no kiting; spellbook by
    magic boost, robe; the potion-and-sit rest plan with the sit for mana; mana potions by
    question 12; the help kit of CP-05, with Castafodin in the shared speed slot unless the
    manifest says otherwise; the named distances and thresholds of CP-18 and CP-21 with its
    own values; and its restock table in the form of CP-24 with question 11's numbers
    (elixirs from list 721, the Kinah floor). UT/NaturalMageProfileTests: catalog ratchet,
    validator row, an example for every rotation and ladder line at each level a skill
    arrives, reward picks. It adds its rows to the starter probe file that CP-42 made, and
    it uses the harness, the probe accounts and the proven rest of that item. A fix to
    shared code follows rule (k).
  - Proof: SIM probe NaturalStarterFieldProbe, rows mage-1 and mage-5. Row 1: a level-1 Mage
    created by packets kills three Sprigg Workers with Flame Bolt from range; before each of
    the two rests the director halves its HP. The first forced rest must show a life potion
    drunk. The director halves the HP again while the potion's 30 s delay still runs, so no
    potion is ready: that second rest must show a sit to the HP target and no potion. Row
    5: a director-leveled level-5 Mage casts Flame Bolt then Blaze on one Fanged Karnif
    210389 (478 HP, level 6) with no cast-start timeout. Karnif 210389 is the one that
    spawns in Ishalgen (spawns/Npcs/220010000_Ishalgen.xml:474); 210655 (577 HP) has a
    template but no spawn.
- [ ] **CP-47 - Artist profile.** Depends: CP-21, CP-24, CP-46
  - Work: Add line artist and Sc/Classes/NaturalArtistProfile.cs: stand-off at 22 m; Pulse
    4408/4409 as pull and filler, Song of Ice 4221/4222, Soothing Melody 4339 as the heal
    from level 5, so the ladder and the rest plan switch when the skill is observed in the
    skill list, not by level; Fiery Descant 4300 excluded because it is a charge skill and
    the bot has only the packet builder (tests/Aion.Bots/Protocol/GameClientPackets.cs:133);
    harp by magic boost, robe. Every active skill needs a harp in the main hand, so the gear
    rules never leave the main hand empty. Before level 5 it rests by potion and sit, as the
    Mage does. The help kit, named distances, thresholds and the restock table as for the
    Mage (CP-05, CP-18, CP-21, CP-24). Tests as for the Mage.
  - Proof: SIM probe NaturalStarterFieldProbe, rows artist-1 and artist-5. Row 1: a level-1
    Artist kills three Sprigg Workers with Pulse, with the two forced rests between kills as
    in CP-46 (the first shows the life potion drunk, the second, inside the potion's delay,
    the sit). Row 5: a director-leveled level-5 Artist at half HP heals itself with Soothing
    Melody 4339 and then kills one Fanged Karnif 210389 (478 HP, level 6).
- [ ] **CP-48 - Engineer profile.** Depends: CP-21, CP-24, CP-46
  - Work: Add line engineer and Sc/Classes/NaturalEngineerProfile.cs: stand-off at 18 m,
    inside the planner's bound and the pistol's 20 m; Direct Shot 2219/2220 as pull and
    filler; Gunshot 1957/1958 then Rapidfire 2142 twice, each within 2 s of the step before
    it, with no Direct Shot in between, because Direct Shot has no chain and would reset it;
    Hot Shot 1942; Bullet Resistance 2168 on the ladder, never inside a chain; pistol by
    magic boost (the training pistol is a magical weapon with magic boost 20,
    item_templates.xml:137287-137288), leather; the potion-and-sit rest plan. Every named
    distance of CP-18 and CP-21 is 20 m or less for this profile. The help kit and the
    restock table as for the Mage (CP-05, CP-24); the profile names the scroll for its
    shared speed slot from its own skills' cast times. Tests as for the Mage, plus: no gun
    skill counts as melee, and after a range refusal the movement helper keeps the bot at
    its hold distance.
  - Proof: SIM probe NaturalStarterFieldProbe, rows engineer-1 and engineer-5. Row 1: a
    level-1 Engineer kills three Sprigg Workers with Direct Shot from range with no
    not-enough-distance refusal, with the two forced rests between kills as in CP-46 (the
    first shows the life potion drunk, the second, inside the potion's delay, the sit). Row
    5: a director-leveled level-5 Engineer casts Gunshot then Rapidfire twice on one
    Vengeful Ghost 210593 (719 HP) with no cast-start timeout.
- [ ] **CP-49 - Scout profile.** Depends: CP-21, CP-24, CP-43, CP-46
  - Work: Add line scout and Sc/Classes/NaturalScoutProfile.cs: walk-in pull; Devotion 3235
    before the opener; Swift Edge 3182/3183 then Soul Slash 3223 at once; Focused Evasion
    3195 on the ladder, never between the two; dagger swings as filler; one dagger by the
    physical stat, leather; the potion-and-sit rest plan; the help kit of CP-05, with
    Blitzopan in the shared speed slot as for the Warrior. Excluded with reasons: Surprise
    Attack 3196/3197 (from the front it does little for 13 MP, 16 MP at rank 2; it opens
    its own chain SRA_CHAINN_1TH and so resets Swift Edge's, and its back damage needs a
    position the bot does not take; skill_templates.xml:53968-53996), Counterattack 3209
    (the bot does not observe its own dodge) and Stealth 3222 (not usable in combat). Named
    distances, thresholds and the restock table as for the Mage (CP-18, CP-21, CP-24). It
    reuses the approach to weapon reach and the swing that the Warrior proved in CP-43; the
    walk-in pull itself is first played in CP-44. Tests as for the Mage.
  - Proof: SIM probe NaturalStarterFieldProbe, rows scout-1 and scout-7. Row 1: a level-1
    Scout walks in and kills three Sprigg Workers with Swift Edge and dagger swings, with
    the two forced rests between kills as in CP-46 (the first shows the life potion drunk,
    the second, inside the potion's delay, the sit). Row 7: a director-leveled level-7 Scout
    casts Swift Edge then Soul Slash on one Fanged Karnif 210389 (478 HP, level 6) with no
    cast-start timeout.

### F. Levels 1-9 to Munin, one preserved snapshot per class

- [ ] **CP-50 - Mage to the Q2004 checkpoint.** Depends: CP-10, CP-20, CP-21, CP-23, CP-24,
  CP-27, CP-28, CP-34, CP-41, CP-46
  - Work: It waits for the full gate of CP-41, like every class journey. Run
    sim-snapshot.ps1 -Action Replay -Class mage -StopAt 2004:5:0 -Item CP-50 (seed 1, bridge
    off, help items on, the 45-minute deadline). CP-10 showed on the Priest that a status-5
    stop fires and writes its receipt. If the stop still does not land cleanly here, use
    -StopAfterQuest 2004 and say so. That fallback writes no receipt, exists only for Q2004
    to Q2007 and has its own shorter deadline, 6 minutes for Q2004, sized on the Priest; a
    run that needs longer fails and counts as an attempt under rule (e). On the way the Mage
    turns in Q2132 at Jurwen 203529 with var 3, is supplied its kit and binds at the hub it
    works from. Rule (e) applies. Write the ledger into the doc: deaths with where each
    revived and whether the soul heal ran, retreats, potions and scrolls used, help items
    supplied, bind fees and Kinah.
  - Proof: One contained run under run/cp/CP-50/<run-id>: resume-receipt.json shows class id
    6 with Q2132 and Q2004 completed. On the -StopAfterQuest fallback no resume-receipt.json
    is written (the return at J:854-861 only requires the quest completed; the receipt is
    written only by the NI08_STOP_AT stop at J:875-891). The proof is then the passing run
    (J:857 requires Q2004 completed) plus the trace's creation step and its Q2132 and Q2004
    turn-in records. The same holds for the Q2004 and Q2007 checkpoints of the other
    classes.
- [ ] **CP-51 - Mage to the Q2007 checkpoint.** Depends: CP-50
  - Work: Run Replay -Class mage -StopAt 2007:5:0 -Item CP-51. This passes the Q2005
    stalkers and the Q2007 generators, the first fights whose attempt budgets were sized on
    a class that heals itself. Rule (e) applies; a per-class override of one budget carries
    its reason. The fallback is -StopAfterQuest 2007, with its own deadline of 20 minutes.
  - Proof: One contained run under run/cp/CP-51/<run-id>: resume-receipt.json shows class id
    6 with Q2005 and Q2007 completed. On the -StopAfterQuest fallback the proof is the
    passing run plus the trace's creation step and its Q2005 and Q2007 turn-in records, as
    in CP-50.
- [ ] **CP-52 - Mage 1-9 at Munin, captured and verified as munin-mage-s1.** Depends: CP-51
  - Work: With the code committed and the bundle green, run sim-snapshot.ps1 -Action Capture
    -Class mage -Name munin-mage-s1 with its own -Run (bridge off, seed 1): all 41 quests,
    level 9, Q2008 at START/0, standing at Munin (J:1010-1027). Acceptance beyond the
    receipt, judged before Verify: completion.json shows a spellbook and robe pieces worn;
    the trace sold no item the class can wear and no item of its help kit; help-items.json
    lists what was supplied; and both Ishalgen binds are in the trace, or the skip of one is
    traced with its reason. A capture that fails acceptance is handled by rule (l):
    munin-mage-s1 keeps its name and is logged as rejected, and the second attempt is
    captured as munin-mage-s1-a2. Write the ledger and the elapsed time into the doc, as in
    CP-50. Then the evidence-only commit (rule (d)). Rule (e) applies. A class that ends the
    41 quests below level 9 goes under Blocked; it never hunts for the level. The class
    scope is recorded by the next item, not here.
  - Proof: sim-snapshot.ps1 -Action Verify -Name <the accepted name> passes: the restore
    emits CP_CLASS=mage and no NA_HELP_ITEMS, the resumed character is accepted as a level-9
    Mage, and the endpoint is reached again.
- [ ] **CP-53 - Record class scope mage, twice.** Depends: CP-52
  - Work: No code changes. On a clean tree run run-neutral-gate.ps1 -Set mage -Record -Item
    CP-53. The gate plays class scope mage (Replay -Class mage -StopAt 2004:5:0) twice,
    compares the two passes and writes the scope's row into
    e2e/natural-neutral-baseline.json. That turns the scope on for the guard of rule (c) and
    for the final gate. Commit that file; the commit is evidence only. If this class's Q2004
    checkpoint passed only on the -StopAfterQuest fallback, the class scope cannot be
    recorded as defined: list this item under Blocked and report. If the two passes differ,
    nothing is written: log the first differing record and list this item under Blocked. The
    ignore list is not narrowed for a class scope, because the Priest and Cleric baselines
    share it.
  - Proof: The record run: the two passes of class scope mage are identical after
    normalization (run/cp/CP-53/<run-id>/verdict.json).
- [ ] **CP-54 - Warrior to the Q2004 checkpoint.** Depends: CP-45, CP-53
  - Work: Run Replay -Class warrior -StopAt 2004:5:0 -Item CP-54. The Ishalgen code written
    for a ranged puller is the likely blocker: ApproachShippedCombatSpawnAsync
    (J:5217-5274), the Sprigg hunt (J:6433-6510), Q2004's approaches (J:6794) and
    fight-through (J:5806-6159). Each one that stops the run asks the pull style at that one
    site, with the Priest keeping its number, as its own lettered item, proven by the gate
    set of the item that owns that site plus the recorded class scopes. Such a fix should
    leave the Mage's scope identical; if it is meant to change a recorded class scope, rule
    (j) applies. Rule (e) applies.
  - Proof: One contained run under run/cp/CP-54/<run-id>: resume-receipt.json shows class id
    0 and Ferocious Strike (2864, or its rank 2, 2865, learned at level 6) in the
    checkpoint's Skills, with Q2004 completed. Class id 0 alone is not enough: it is also
    what the receipt holds when the class was not observed
    (Sc/NaturalJourneyCheckpoint.cs:12, 35). On the -StopAfterQuest fallback the proof is
    the passing run plus the trace's creation step and its turn-in records, as in CP-50.
- [ ] **CP-55 - Warrior to the Q2007 checkpoint.** Depends: CP-54
  - Work: Run Replay -Class warrior -StopAt 2007:5:0 -Item CP-55. The stalker (Q2005) and
    generator (Q2007) fights are the expected trouble for a melee class without a heal;
    Q2005's firing-edge search (J:6896-6913) is fixed as a lettered item if it stops the
    run. Rule (e) applies.
  - Proof: One contained run under run/cp/CP-55/<run-id>: resume-receipt.json shows class id
    0 and Ferocious Strike (2864, or its rank 2, 2865, learned at level 6) in the
    checkpoint's Skills, with Q2005 and Q2007 completed. Class id 0 alone is not enough: it
    is also what the receipt holds when the class was not observed
    (Sc/NaturalJourneyCheckpoint.cs:12, 35). On the -StopAfterQuest fallback the proof is
    the passing run plus the trace's creation step and its turn-in records, as in CP-50.
- [ ] **CP-56 - Warrior 1-9 at Munin, captured and verified as munin-warrior-s1.** Depends:
  CP-55
  - Work: As CP-52 with -Class warrior -Name munin-warrior-s1. Acceptance: a sword or mace
    and chain pieces worn, the weapon and not the shield taken at Q2100, nothing wearable
    and nothing of the help kit sold, the help items listed and both binds traced. The
    Hatata fight (Q2129) is the last expected trouble.
  - Proof: sim-snapshot.ps1 -Action Verify -Name <the accepted name> passes: the restore
    emits CP_CLASS=warrior and no NA_HELP_ITEMS, the resumed character is accepted as a
    level-9 Warrior and the endpoint is reached again.
- [ ] **CP-57 - Record class scope warrior, twice.** Depends: CP-56
  - Work: As CP-53 with -Set warrior -Item CP-57.
  - Proof: The record run: the two passes of class scope warrior are identical after
    normalization (run/cp/CP-57/<run-id>/verdict.json).
- [ ] **CP-58 - Artist to the Q2004 checkpoint.** Depends: CP-47, CP-53
  - Work: Run Replay -Class artist -StopAt 2004:5:0 -Item CP-58. Trainer Sona 801219 (var 6)
    and the harp rewards of Q2100, Q2002 and Q2134 have never been played by a bot; whatever
    fails is logged and becomes its own lettered item, not widened here. Rule (e) applies.
  - Proof: One contained run under run/cp/CP-58/<run-id>: resume-receipt.json shows class id
    15 with Q2132 and Q2004 completed. On the -StopAfterQuest fallback the proof is the
    passing run plus the trace's creation step and its turn-in records, as in CP-50.
- [ ] **CP-59 - Artist 1-9 at Munin, captured and verified as munin-artist-s1.** Depends:
  CP-58
  - Work: As CP-52 with -Class artist -Name munin-artist-s1. Acceptance: a harp and robe
    pieces worn, nothing wearable and nothing of the help kit sold, the help items listed
    and both binds traced, and the rest plan seen to switch to Soothing Melody once it is
    learned (level 5). If a second attempt fails, the quest where it stopped becomes a
    lettered checkpoint
    item.
  - Proof: sim-snapshot.ps1 -Action Verify -Name <the accepted name> passes as a level-9
    Artist, with CP_CLASS=artist and no NA_HELP_ITEMS emitted by the restore.
- [ ] **CP-60 - Record class scope artist, twice.** Depends: CP-59
  - Work: As CP-53 with -Set artist -Item CP-60.
  - Proof: The record run: the two passes of class scope artist are identical after
    normalization (run/cp/CP-60/<run-id>/verdict.json).
- [ ] **CP-61 - Engineer to the Q2004 checkpoint.** Depends: CP-48, CP-53
  - Work: Run Replay -Class engineer -StopAt 2004:5:0 -Item CP-61. Trainer Wild Wilhelm
    801218 (var 5) and the pistol rewards have never been played by a bot. A site that still
    stands the bot off beyond 20 m is fixed through its named profile distance, not by a new
    literal. Rule (e) applies.
  - Proof: One contained run under run/cp/CP-61/<run-id>: resume-receipt.json shows class id
    12 with Q2132 and Q2004 completed. On the -StopAfterQuest fallback the proof is the
    passing run plus the trace's creation step and its turn-in records, as in CP-50.
- [ ] **CP-62 - Engineer 1-9 at Munin, captured and verified as munin-engineer-s1.**
  Depends: CP-61
  - Work: As CP-52 with -Class engineer -Name munin-engineer-s1. Acceptance: a pistol and
    leather pieces worn, nothing wearable and nothing of the help kit sold, the help items
    listed and both binds traced. If a second attempt fails, the quest where it stopped
    becomes a lettered checkpoint item.
  - Proof: sim-snapshot.ps1 -Action Verify -Name <the accepted name> passes as a level-9
    Engineer, with CP_CLASS=engineer and no NA_HELP_ITEMS emitted by the restore.
- [ ] **CP-63 - Record class scope engineer, twice.** Depends: CP-62
  - Work: As CP-53 with -Set engineer -Item CP-63.
  - Proof: The record run: the two passes of class scope engineer are identical after
    normalization (run/cp/CP-63/<run-id>/verdict.json).
- [ ] **CP-64 - Scout to the Q2004 checkpoint.** Depends: CP-49, CP-57
  - Work: Run Replay -Class scout -StopAt 2004:5:0 -Item CP-64; Q2132 is turned in at Wiokan
    203528 with var 2. It reuses every melee fix the Warrior needed; anything new is a
    lettered item. Rule (e) applies.
  - Proof: One contained run under run/cp/CP-64/<run-id>: resume-receipt.json shows class id
    3 with Q2132 and Q2004 completed. On the -StopAfterQuest fallback the proof is the
    passing run plus the trace's creation step and its turn-in records, as in CP-50.
- [ ] **CP-65 - Scout 1-9 at Munin, captured and verified as munin-scout-s1.** Depends:
  CP-64
  - Work: As CP-52 with -Class scout -Name munin-scout-s1. Acceptance: a dagger and leather
    pieces worn, nothing wearable and nothing of the help kit sold, the help items listed
    and both binds traced. If a second attempt fails, the quest where it stopped becomes a
    lettered checkpoint item.
  - Proof: sim-snapshot.ps1 -Action Verify -Name <the accepted name> passes as a level-9
    Scout, with CP_CLASS=scout and no NA_HELP_ITEMS emitted by the restore.
- [ ] **CP-66 - Record class scope scout, twice.** Depends: CP-65
  - Work: As CP-53 with -Set scout -Item CP-66.
  - Proof: The record run: the two passes of class scope scout are identical after
    normalization (run/cp/CP-66/<run-id>/verdict.json).

### G. Close-out

- [ ] **CP-67 - The other nine second classes through the class choice, on prepared
  characters.** Depends: CP-31
  - Work: Add nine rows to NaturalClassChoiceProbe, built with ForChoice: Gladiator
    (SETPRO7), Templar (SETPRO8), Assassin (9), Ranger (10), Sorcerer (11), Spirit Master
    (12), Gunner (15), Bard (16) and Rider (17), each with its class page, Q2009 var,
    preceptor, reward list and dispatch quest from the class-line contract. The characters
    are prepared by the director, hold their starter weapon and fight the trial inside the
    scenario's existing swing bound; a row that cannot finish inside it is reported, not
    widened in silence. Rows beyond the probe ids CP-31 allocated run in a second filtered
    process. No journey code and no natural character is involved. It is the first run of
    the Engineer and Artist pages, preceptors and dispatch quests by any bot and is evidence
    for the next plan; a server defect is fixed Java-first under its own lettered item.
  - Proof: SIM probe NaturalClassChoiceProbe: the nine new rows pass, each ending with the
    chosen class id, the row's ceremony reward paid at its own preceptor and its own
    dispatch quest started at START/0 by the ceremony and moved to var 1 at Doman 204191.
    All six Asmodian dispatch handlers register the same quest-completed start and the same
    Doman talk as Q2904's (JAVA/data/handlers/quest/ascension/_2901 to _2904, _29070 and
    _29071, lines 23-25).
- [ ] **CP-68 - Off hand: shield and second weapon in the gear rules.** Depends: CP-29,
  CP-57, CP-66
  - Work: Java first: Equipment.java's rules for shields and dual wield. The table gear rule
    gains an off-hand mode: none, shield (Warrior, mastery 43 from level 1; Q2100 offers
    shield 115000024, the only shield an Ishalgen quest offers) or second one-hand weapon
    (Scout, only once skill 55 is observed at level 5). NaturalGearPolicy.SelectUpgrades
    (Sc/NaturalGearPolicy.cs:76-93) fills the off hand by that mode. Question 10 is
    answered (2026-10-07): the Warrior takes the weapon at Q2100, not the shield. So no
    Depends line is edited, the mode is built after the Warrior and the Scout are at Munin,
    and every profile keeps mode none; no snapshot is recaptured. Turning a mode on for a
    class, the Scout's second dagger included, waits for the operator's word. The guard of
    rule (c) runs with every class scope recorded so far, because SelectUpgrades is shared.
    With mode none in every profile those scopes must stay identical; an item that later
    turns a mode on for a class follows rule (j).
  - Proof: Unit test UT/NaturalGearPolicyTests: new cases for sword plus shield and for
    dagger plus dagger after skill 55; the staff-rule and Priest cases unchanged.
- [ ] **CP-69 - Final gate and close-out.** Depends: CP-33, CP-45, CP-53, CP-57, CP-60,
  CP-63, CP-66, CP-67, CP-68
  - Work: No code. Run the whole check list of CLAUDE.md, then the full gate on the
    committed tree with the five class scopes. Write the closing Status paragraph of
    docs/natural-class-profiles.md: the six new snapshots under their accepted names, each
    class's deaths and consumables, every lettered item, every rejected capture, the
    findings logged and not fixed, and what each class needs before its second class. Update
    docs/natural-ntc-readiness.md and list stray evidence directories for the operator. If a
    scope differs, follow the full-gate failure rule of section 8.
  - Proof: Neutral gate, set all plus the five class scopes: every scope is identical to its
    baseline.

## Operator decisions

The request at the top of this document is the operator's direction for this plan.
Twenty-one questions follow. Each has a recommended default.

How an answer is recorded: an `Answer (date): ...` line under the question in this file,
written by the operator, or by the loop quoting the operator's words. A question with no
Answer line runs on its default. Seven answers are recorded now. CP-Q1 and CP-Q8 were taken
from the request on 2026-10-06, and the operator confirmed them on 2026-10-07 ("Good").
CP-Q3, CP-Q10, CP-Q11, CP-Q12 and CP-Q21 were answered on 2026-10-07, after the operator
read the plan. Where an Answer disagrees with its question's Default, the Default is marked
as replaced and the Answer rules.

- **CP-Q1.** D25 authorizes one class, the Cleric, and supersedes the earlier Chanter note;
  D22 and D23 set the natural-play rules for the Priest; D38 is the highest decision id. Do
  you authorize, as D39: the class seam, a Priest-to-Chanter branch as far as the
  Pandaemonium ceremony, and level 1-9 Ishalgen play for Warrior, Scout, Mage, Engineer and
  Artist under the same natural-play rules?
  - Default: Yes, as worded, in SIM only. Nothing past those endpoints, no LIVE identity, no
    server change. CP-00 quotes your answer in the D39 row and adds one pointer row in
    CLAUDE.md.
  - Why it is asked: Earlier D rows record an explicit approval, and the loop rules allow no
    natural play of another class without a logged decision.
  - Answer (2026-10-06): Yes, from the request. It asks for the seam ("a bit of prep work
    refactoring what we have to be reusable"), for the Chanter diverging at Ascension ("a
    Chanter would just diverge at that point instead of being scoped just to Cleric") and
    for levels 1-9 to Munin for the other starter classes ("knock out any other
    starter-class types 1-9 profiles and rules"). SIM only is this plan's own limit, not the
    operator's words. Confirmed 2026-10-07: the operator read this answer and said "Good".
- **CP-Q2.** docs/natural-ntc-readiness.md:5 names Templar and Sorcerer profiles for an NTC
  party as the next goal. Does this plan replace that goal or come before it?
  - Default: It comes before it. Under answer (a) to question 8 the Templar and the Sorcerer
    start from the Warrior and Mage profiles with a fresh run in the early-Ascension order,
    not from the Munin snapshots; the readiness report gets one line that points here.
  - Why it is asked: Nothing in the files says which goal leads. Mage and Warrior are
    journeyed first for this reason.
- **CP-Q3.** The loop rules say one run per development item, no earlier leg rerun without
  need and no fresh-create run unless asked. This plan needs more: 14 baseline recordings
  (seven scopes twice); about 57 gate-scope replays, 38 across seventeen gated items and 19
  in the two full gates (seven scopes in CP-41, twelve in CP-69); the one-minute guard p on
  about thirty commits; from the first class-scope recording on, every recorded class scope
  again on each later commit that edits shared bot code (one such commit is planned, CP-68,
  and every lettered fix adds one); up to 16 checkpoint replays in the eight checkpoint
  items (CP-44, CP-50, CP-51, CP-54, CP-55, CP-58, CP-61 and CP-64), and the status-5 run
  of CP-10; two contained runs of scope m, one for the Priest's kit and one for the Ishalgen
  binds (CP-06 and CP-07), each with a second attempt at most; six captures with two
  attempts each at most; six verifies; ten class-scope recordings (five scopes twice); one
  Full-tier scenario run, CAPITAL-ASMO (CP-30); and nine probe items, each one filtered SIM
  process (CP-31, CP-34, CP-42, CP-43, CP-46 to CP-49, CP-67). Approved?
  - Default: none that the loop could use. The recommended answer was yes: SIM only, seed 1
    only, every run in its own throwaway schema, nothing existing captured over, no
    multi-seed batches, no LIVE runs. If you want strictly one run per item, say so: the
    guard of rule (c) then becomes an item of its own after each commit it covers, and
    question 4 decides whether a gate set of several scopes is split.
  - Why it is asked: No automated test asserts the journey's decisions, so a same-seed
    replay is the only proof that the Cleric still plays the same, and a 1-9 proof for a new
    class is by nature a fresh-create run. You should approve the real number, not a smaller
    one.
  - Answer (2026-10-07): "Your recommended". That is yes: SIM only, seed 1 only, every run
    in its own throwaway schema, nothing existing captured over, no multi-seed batches, no
    LIVE runs. CP-03 is no longer blocked. The two contained runs of CP-06 and CP-07 were
    added to the count the same day, for the decisions of the same message; they are runs
    of the kind this answer approves. Say so if they should be counted differently.
- **CP-Q4.** Does one gate invocation that names more than one scope (for example p+c or
  m+b+c+ax) count as the item's one proof?
  - Default: Yes: one command, one verdict, one evidence folder. If not, each such item is
    split into a Priest-side and a Cleric-side item.
  - Why it is asked: Set p holds no Cleric fight and few Priest branches, set m holds no
    Cleric, and only b (the Altgard shop stop: a sale and 12 Lesser Life Elixirs bought) and
    ax hold vendor buys; no scope reaches the Ishalgen vendor buy. So most seam items need
    two to four scopes to prove they changed nothing.
- **CP-Q5.** May other work commit to main while this list is worked: the three untracked
  docs/playtest-*.md plans, server fixes, upstream ports?
  - Default: Keep other bot plans paused until CP-45. A server fix or an upstream port may
    land between CP items. The loop then follows the re-record rule of section 8: it runs
    the full gate at HEAD, and if a scope fails it stops and asks you to choose between
    re-recording at HEAD and reverting. An upstream port may bump javaReference in the
    class-lines file.
  - Why it is asked: Every gate compares with traces recorded at one commit. The first
    outside commit that changes a trace fails every later gate until the baselines are
    re-recorded.
- **CP-Q6.** Where does the Chanter start and stop: (a) a fresh Priest through the existing
  capital-start scope, about one minute, stopping after the Pandaemonium ceremony; or (b) a
  restored copy of the old munin snapshot, which needs a new from-snapshot scope on the
  older late-Ascension order?
  - Default: (a). The new snapshot is pandaemonium-chanter-start-s1.
  - Why it is asked: (a) uses only a scope proven on recent commits and adds no code to the
    shared bridge path, and its trace should equal the Cleric baseline up to the
    class-choice send. Everything after the ceremony uses Cleric-only decisions.
- **CP-Q7.** Which ceremony weapon does a Chanter take from chanter_selectable_reward, and
  how is 'physical attack' ranked? Per swing the Karmic Staff wins (58-88 at 2.0 s, mean
  73); per second the Karmic Warhammer wins (44-66 plus 7 physical attack at 1.5 s, about 41
  a second against 36) and leaves the shield hand free.
  - Default: Rank physical weapons per swing (mean damage plus the flat physical-attack
    bonus), for every class; so the Chanter takes the Karmic Staff. The staff-by-magic-boost
    rule stays the Cleric's alone. CP-29 builds the stat, the probe of CP-31 is the first to
    use the pick, and CP-32 writes both weapons' numbers into the doc before the run.
  - Why it is asked: The same choice was an operator decision for the Cleric (OD-5),
    PlayerClass.cs lists the Chanter as a physical class, and the two readings of the stat
    pick different weapons, so the definition has to be yours.
  - Answer (2026-10-07): "We prefer magic boosted staffs after ascension", said of the
    Priest types, Cleric and Chanter alike. The Chanter takes the Karmic Staff and ranks
    its staffs by magic boost, as the Cleric does. The physical stat stays the rule of the
    Warrior and the Scout. CP-29a moves the Chanter's table to it; the snapshot of CP-33
    holds the same staff either way.
- **CP-Q8.** What is the 1-9 endpoint for the five new starters? (a) The existing Munin stop
  with the bridge off: all 41 quests, level 9, Q2008 at START/0. This is the order OD-16
  replaced: a non-Daeva is capped at 126,069 XP (level 9 starts at 82,982), so quest XP past
  the cap is discarded. (b) A new stop at the first level 9 with Ishalgen quests pending,
  the OD-16 branch point. (c) A new stop at Q2008 var 6, trial won and class page
  unanswered, the one state past var 4 that the enter-world hook does not reset to 4
  (_2008Ascension.cs:296); the die hook (lines 312-323) would still reset it on a death
  inside the instance. The character stands inside the Ataxiar instance, resuming a snapshot
  there is untested, and every starter must fight the trial.
  - Default: (a), named munin-<class>-s1, and recorded as proof that the class can play all
    of Ishalgen, not as the start of a second-class leg. A later second-class plan starts
    with a fresh run in the early-Ascension order, as the Cleric's did; the Priest reaches
    level 9 and the ceremony in about a minute of run time.
  - Why it is asked: Your request says '1-9 to Munin class select', (a) exists and has no
    class check, and it plays every Ishalgen quest with each class, which serves the
    play-tested-server goal. Snapshots are never recaptured, so the endpoint has to be
    settled before the first capture.
  - Answer (2026-10-06): (a), from the request, which says "we can create a '1-9' to Munin
    class select for the other types". The snapshots are named munin-<class>-s1. Confirmed
    2026-10-07: the operator read this answer and said "Good".
- **CP-Q9.** In what order are the starters done?
  - Default: Fights: Warrior first (before the seam is closed), then Mage, Artist, Engineer,
    Scout. Journeys: first the Warrior's short run to Q2132 (CP-44, before the seam is
    closed), then the journeys to Munin in the order Mage, Warrior, Artist, Engineer, Scout.
  - Why it is asked: The Warrior is least like the Priest, so it tests the seam before it is
    frozen. Its short run to Q2132 is the first fresh-create journey of a new class, and it
    meets the melee pull and recovery without a heal at once; that is the price of testing
    the seam early. To Munin the Mage goes first because it reuses the proven ranged pull,
    which separates recovery-without-a-heal problems from melee-pull problems over the long
    run.
- **CP-Q10.** Gear rule per starter, and the off hand. By the stat of question 7 a Warrior
  takes Raider's Mace over Raider's Sword at Q2100; Q2100 is also the only Ishalgen quest
  that offers a shield (115000024). Does the Warrior take a weapon or the shield there, and
  does the Scout wield a second dagger from level 5?
  - Default: Warrior: sword or mace by the physical stat, chain. Scout: dagger, leather.
    Mage: spellbook by magic boost, robe. Engineer: pistol by magic boost, leather. Artist:
    harp by magic boost, robe. At a reward choice take the weapon when it beats the held
    one, otherwise the class's armor type. First snapshots: the weapon at Q2100, one dagger,
    no off hand; CP-68 builds the off-hand mode afterwards. No gear is bought with Kinah.
  - Why it is asked: AX-Q1 said other classes get their own weapon rule when their profiles
    are planned. Under answer (a) to question 8 the Templar starts with a fresh run and
    makes its own Q2100 pick, so leaving the shield out of the first Warrior run costs the
    Templar nothing. If you answer 'shield', no item moves and no id changes. The loop's
    first act on reading the Answer line is to edit four Depends lines and log the edit:
    CP-68's becomes CP-29 alone, and CP-54's, CP-58's and CP-61's each gain CP-68. After
    CP-53 the pick rule then goes to CP-67 and CP-68 and back to CP-54, so the off-hand item
    runs before the Warrior's Q2004 checkpoint and the journey order of CP-Q9 holds. Those
    three items then depend on an item listed after them; that is the one allowed exception
    to the listed-earlier rule.
  - Answer (2026-10-07): "Warrior does take weapon". The Warrior takes the weapon at Q2100,
    not the shield. Everything else in the default stands: the picks per class, one dagger
    for the Scout, no off hand in the first snapshots, CP-68 after them. The answer is not
    'shield', so no Depends line is edited and the exception above is not used.
- **CP-Q11.** Recovery for classes without a heal (Warrior, Scout, Mage, Engineer, and the
  Artist before level 5). Between fights: (i) sit only, which Java makes workable (about 32
  HP every 6 s at level 1 and 96 at level 9 for a class with Health 100; the Warrior gets a
  tenth more and the Mage a tenth less), or (ii) bandage then sit, with bandages bought when
  they run low. Also the potion threshold, retreat rule, restock numbers and a Kinah floor.
  - Default (its between-fight half is replaced by the Answer; the rest stands): In a
    fight: timed life potion at or below 75% HP; retreat at three attackers (Mage at two),
    or at 25% HP while the potion is on its delay. Elixir restock stays at 5 or fewer up to
    12; no purchase takes Kinah below 500. Q2117 and Q2124: take the Minor Life Elixir.
    These are starting numbers, tuned from the first ledger and recorded in the doc; CP-24
    reads the Priest's Munin ledger for the Kinah figure. Replaced by the Answer: option
    (ii), with its Bandage Heal below 70% HP between fights and its bandage stock.
  - Why it is asked: The Priest's numbers were sized for a class that also heals itself.
    When it was asked, bandages looked like the between-fight heal: they are finite (20 at
    the start, a few quest rewards) and the bot buys none. The vendor elixir has twice the
    delay of the starter potion, and soul healing after a death draws on the same Kinah.
  - Answer (2026-10-07): "DO not use bandages, just use Potions, rest when potion is on
    cooldown if needed". No bandage is used, bought or kept as a supply, by any class.
    Between fights a class without a heal drinks a life potion when its HP is below the 90%
    target and the potion is ready, and sits to 90% while the potion is on its delay. In a
    fight the default above stands: the potion at or below 75% HP and the retreat numbers.
- **CP-Q12.** Mana for Mage and Artist, the three event scrolls every starter owns, and help
  items: are the 100 starter mana potions drunk in a fight, and is anything else used at
  levels 1-9?
  - Default (its last part is replaced by the Answer; the rest stands): A mana potion in a
    fight only when MP is below the main attack's cost and HP is above the life-potion
    threshold, so the life potion wins the shared delay. Sit below 40% MP until 80% between
    fights. Never buy mana potions at 1-9. Replaced by the Answer: the part that kept the
    event scrolls and every help item from all lines but priest-cleric.
  - Why it is asked: When it was asked, the Priest never drank mana potions (the gate is
    IsCleric at J:9132), life and mana potions share one use-delay group, and the help list
    had been approved for the Cleric only, with five of its bands starting at level 10.
  - Answer (2026-10-07): "help items on for everyone, and even change the priest defaults!
    Get better healing potions, the shield scroll, greater running scroll. Any consumable to
    make these more survivable and faster." Help items are on for every class line, the
    Priest's levels 1-9 included. "Any consumable" covers the three event scrolls and the
    starter mana potions too: they are used. The mana-potion and sit numbers of the default
    stand. CP-05 writes the kit down as a manifest and CP-06 turns it on for the Priest.
- **CP-Q13.** The standing rule is an inventory check after every quest turn-in. In Ishalgen
  the bot wears gear only at the end of a rest (J:9716-9718), and 'Don't change earlier
  runs' was said about the accepted Priest and Cleric legs. Do the new class lines run the
  equipment check after every Ishalgen turn-in?
  - Default: Yes for every line but priest-cleric, switched by the line so the Priest's
    trace stays identical (CP-23). The Priest line keeps its present check points.
  - Why it is asked: A class with a different rest plan reaches the Priest's check points at
    different times, and a new class's run is not an earlier run.
- **CP-Q14.** Patrols in the way: OD-14 was answered for the Cleric only (wait 15 s up to
  four times, then decide). What do the new classes do at levels 1-9?
  - Default: The Priest's Ishalgen baseline: 3 s waits for the baseline number of cycles,
    then the existing fallbacks. Revisit per class when it gets a leg past Munin.
  - Why it is asked: No decision covers another class, and the Priest baseline is the only
    patrol rule proven in Ishalgen.
- **CP-Q15.** How does a melee class pull an aggressive monster: walk in and fight at its
  position, or a body pull (step into its aggro circle from the staged spot, step back and
  let it come)?
  - Default: Walk in. The body pull is tried only if the Warrior's checkpoint runs show
    walk-in fights drawing adds, as a lettered item (rule (i)).
  - Why it is asked: The walk-in reuses the staging and adds logic the planner already has;
    the body pull keeps the fight on the clean spot but is new behavior nobody has run.
- **CP-Q16.** Skills left out of the first profiles: Artist Fiery Descant (a charge skill),
  Scout Surprise Attack (back damage and its own chain), Counterattack (needs the bot to
  observe its own dodge) and Stealth, any kiting for the Mage, Escape 302 for everyone; and
  Mage Root is used only on a second attacker or before a retreat. Acceptable?
  - Default: Yes, each excluded with its reason in the profile, as the Cleric's are. Revisit
    after the Munin snapshots exist.
  - Why it is asked: The validator requires every auto-learned active skill to be cast or
    excluded with a reason, so each omission is visible. Root is settled by the data: nine
    hits in ten break it.
- **CP-Q17.** Follow-up timing: Java lets Robust Blow follow Ferocious Strike at any time,
  because the opener carries no chain time. Should the bot use that, or cast a follow-up
  only within the follow-up's own chain time (3 s) after the step before it?
  - Default: Only within its own chain time. It is always legal on Java too.
  - Why it is asked: Natural play means a human player's eligibility rules, and how long the
    4.8 client keeps a follow-up lit is not in the repo. The stricter rule cannot be refused
    by either.
- **CP-Q18.** Should the per-class probes run in every Fast run or only when asked?
  - Default: The five level-1 first-kill rows always on while each stays under about ten
    seconds. The leveled rows, the Q2132 probe and the class-choice probe are gated and run
    alone by filter.
  - Why it is asked: Fast is the only automatic net for the new profiles, but no probe
    account id is known to be free: 77-82 are the gear scenarios' accounts
    (SimT/SimulationGearScenarioTests.cs:34) and 91-94 the geo-displacement scenarios'
    (SimT/SimulationGeoDisplacementScenarioTests.cs:30-33). CP-31 either finds free ids by
    reading every session construction, computed ids included, or adds new ids to the
    fixture's list (SimT/SimulationWorldFixture.cs:204-206; 98 and 100 are not defined, and
    ids above 255 cannot log in). Gated theories run alone in their own schema can share
    ids. Section 8 names the variable that picks the gated rows and the command that runs
    one probe alone.
- **CP-Q19.** SIM identity: one account for every line, which character names, and are all
  lines male?
  - Default: Account 41 for every line, because each run owns its schema. Names: Asimnjour
    for both Priest lines; Asimwar, Asimscout, Asimmage, Asimengi and Asimartist for the
    others, checked against Java's character-name pattern before first use. All male, as
    today. Snapshots: pandaemonium-chanter-start-s1 and munin-<class>-s1.
  - Why it is asked: The resume check compares the character name, Java's name pattern
    forbids digits, and male is fixed in three places in the bot today.
- **CP-Q20.** The list has 70 items, above the preferred 35, because every item must fit one
  iteration and carry one proof. Work it as one list, or in two batches?
  - Default: One list, with two points where the loop reports and then goes on unless the
    operator says otherwise: after CP-45 (the seam closed, with a Warrior in the field; the
    Chanter capture CP-33 does not hold this point up, so the report says whether it is
    ticked or blocked) and after CP-57 (Mage and Warrior at Munin, both class scopes
    recorded). Artist, Engineer, Scout and the close-out can wait without leaving anything
    half done.
  - Why it is asked: The critique asked for splits (pin test first, gear in four steps,
    bridge in two, checkpoint rungs per class). The loop-fit review added the status-5 item
    and nine more splits (Replay and gate, scenario and probe, harness and Warrior profile,
    full gate and ratchet, capture and class scope for five classes). Merging them back
    would bring back items too large for one iteration.
- **CP-Q21.** The standing rule is to bind at the working hub's obelisk and to use hub
  flight transporters. In Ishalgen the accepted Priest route does neither: the bind
  (BindAtAldelleIfNeededAsync, J:4877-4893) and the unforced hub flight (J:4897) run only
  when NI07_OPTIMIZE_HUBS is set, and no runner sets it (scripts/sim/sim-snapshot.ps1:91
  clears it). Should the new class lines bind at an Ishalgen obelisk?
  - Default (replaced by the Answer; it no longer applies): No, with Ishalgen played on the
    starting bind as the accepted Priest played it, because the recorded bind policy names
    "every leg from Leg 4 on" (docs/natural-altgard-leveling.md:5379-5381).
  - Why it is asked: The death rule sends a dead character to whichever obelisk it is bound
    to, and every Ishalgen obelisk has a Soul Healer beside it. So the answer decides where
    a class revives and whether it pays a bind fee. No CP item decided it when it was
    asked; CP-07 now builds the answer.
  - Answer (2026-10-07): "We should have been binding in Ishalgen the whole time, at each
    quest hub (the village and the outpost), and soul heal as discussed. Death is not
    failure." Yes, and for every line, the Priest included. The bot binds at the village
    obelisk 700063 and at the outpost obelisk 700064, each on first arrival there for work.
    After a death it revives at the working hub and soul heals at the Soul Healer beside
    that obelisk: Linevir 203512 at the village, Rusalka 203680 at the outpost. This changes
    the accepted Priest line's levels 1-9 on purpose. The answer says nothing about hub
    flight, so in Ishalgen that stays off.
- **CP-Q24** (asked 2026-10-07, in CP-29a). When two armor pieces of different types
  compete for a slot, which wins: the higher item level, or the class's better type?
  - Default: Item level first, the type second. A class takes its own type at a reward
    that offers one item level in several types (every Ishalgen armor reward does), and
    wears the higher piece when the levels differ. A Cleric in level-20 leather does not
    put on a level-16 chain piece.
  - Why it is asked: The operator's rule names a type for each class ("leather, then
    chain"), and CP-29 read it as the type first. That kept a level-1 leather piece over
    a level-8 robe piece, which the recorded human Priest did not do, and the legs from
    Altgard on were accepted with item level deciding. The other reading changes what a
    Cleric wears on those legs and needs the baselines re-recorded again.
  - Also asked with it: the Warrior now takes the sword before the mace (the table's
    weapon order), where the physical stat alone preferred the mace by 4 points.

## Blocked / questions for the operator

**The re-record rule stopped the loop on 2026-10-07; CP-Q23 is answered and the loop runs
again.** Outside commit 6bdb96d66 ("Stop spawning Ishalgen's two new-class trainers twice",
the fix of CP-34's finding, made in a separate task) changes every trace. The full gate at
HEAD 50db7f2d4, run gate-a1 of item rerecord-6bdb96d66
(run/cp/rerecord-6bdb96d66/gate-a1/verdict.json): all seven scopes differ from their
baselines, and every journey ran to its end.

- What differs: ten fewer objects are made at world start, so every object id made later
  is ten lower, and the two extra trainers are no longer seen. p, m and b first differ at
  record 1, in fields.objectId, and are shorter: 35,789 records against 35,811, 123,048
  against 123,112 and 128,869 against 128,937. l1, c, hm and ax start from snapshots and
  keep their record counts (30,693, 96,166, 39,564, 15,762); they first differ at records
  1,937, 125, 2,387 and 486, in an object id or an item list.
- What does not differ: all 21 step counts of the comparer are equal to the baseline's in
  every scope. Only the record totals of p, m and b moved.
- **The operator decides (CP-Q23): re-record the seven baselines at HEAD, or revert
  6bdb96d66?** The loop's recommendation is to re-record now, before CP-29a, so that
  CP-29a's list of differences shows gear picks and not object ids. CP-29a then re-records
  once more, as CP-Q22 allows. No item is taken until an Answer line stands here.
  Answer (2026-10-07): "Resume the loop, the carat you spawned to fix the trainers has
  been fixed." The fix stays, so the baselines are re-recorded at HEAD. This is the loop's
  reading of the answer: reverting would undo the fix the operator names as done.
- **Re-recorded on 2026-10-07 at be9837cd8** (be9837cd862a0f791bf32fef0c18f35955f4beef),
  item rerecord-23e370c6f, run record-a1, with -Record -ReRecord on a clean tree
  (run/cp/rerecord-23e370c6f/record-a1/verdict.json): verdict pass, all seven scopes
  repeat in their two passes. HEAD holds a second outside commit, 23e370c6f ("Put
  Ishalgen's two new-class trainers back on their Java 4.8 spots"), and be9837cd8, which
  is docs only; the record run is the full gate at HEAD for both.
  - The old rows were recorded at 200ec4c24 (p, c) and 5ffbac512 (m, b, l1, hm, ax). Every
    row now carries be9837cd8, which is the baseline sha of the re-record rule and of the
    guard of rule (c) from here on. The old traces stay under run/cp/baseline/ in their
    two folders and in the second copy.
  - Records: p 35,789 (was 35,811), m 123,046 (was 123,112), b 128,869 (was 128,937);
    l1 30,693, c 96,166, hm 39,564 and ax 15,762, as before. m is two records shorter than
    in gate-a1, which ran before the trainers moved.
  - All 21 step counts of every row equal the old row's. Only the record totals of p, m
    and b moved, and each row's line, replay, environment and ignore list are unchanged.
  - New digests: p d30f444e..., m 7d665ec0..., b 666ac960..., l1 de1137ed..., c f284becb...,
    hm 67286f62..., ax 824c178d....

No item is listed here as blocked.

- **CP-Q22** (asked 2026-10-07 for CP-29a: may the seven Priest and Cleric baselines be
  re-recorded once, after the differences are listed, when the Priest and the Cleric move
  to the table gear rules?). Answer (2026-10-07): "Yes, unblock this. All classes need to
  be able to run. Port over where our cleric/priest uses the same as others, without
  needing special things. If our generic stuff can't compensate then we need a better
  system, as every class plays the game slightly differently." The operator gave gear
  rules per class type with it; they are written into CP-29a. CP-29a is unblocked.
CP-08's first record run failed in scope p on 2026-10-07; by rule (i) that became lettered
item CP-07a, which stands in CP-08's Depends, so CP-08 waits for it and is not listed
here. CP-03
(Replay without capture) was listed until 2026-10-07, when the operator answered CP-Q3, the
question about the number of runs ("Your recommended"). An item id named in the notes below
is not a blocked item.

Answered, nothing more wanted:

- **CP-Q1** and **CP-Q8** carry Answer lines dated 2026-10-06, taken from the request: yes
  to the seam, to the Chanter diverging at Ascension and to levels 1-9 to Munin for the
  other starter classes; and endpoint (a), the existing Munin stop. The operator confirmed
  both on 2026-10-07 ("Good").
- **CP-Q3** (2026-10-07): the run counts are approved as recommended. SIM only, seed 1, a
  throwaway schema for every run, nothing captured over, no multi-seed batches, no LIVE.
- **CP-Q10** (2026-10-07): the Warrior takes the weapon at Q2100, not the shield.
- **CP-Q11** (2026-10-07): no bandages. Potions, and a sit while the potion is on its
  delay.
- **CP-Q12** (2026-10-07): help items are on for every class line, the Priest's levels 1-9
  included.
- **CP-Q21** (2026-10-07): every line binds at each Ishalgen quest hub, the village and the
  outpost, and soul heals there after a death.

CP-00 logs all seven in decision D39 with the operator's words quoted. Say so if D39 should
be worded differently or cover less.

Not blocking. These points were set while the answers of 2026-10-07 were written into the
plan, where the operator's words left room. They block no item. The loop works with them;
say so to change one.

- **The shield scroll's tier at levels 1-9.** Default: the Lesser Anti-Shock 164000067,
  which absorbs 158. By its restrict row a level-1 character may use up to the Major
  164000070, which absorbs 425.
- **Stock and use follow OD-13's numbers:** potions to 30 when below 10, shield scrolls to
  30 when below 8, running scrolls to 20 when below 5; the shield scroll at 50% HP, the
  Running scroll before a leg of 150 m or more.
- **The speed scroll per class.** Casting speed (Castafodin) for a class that casts, as
  OD-15 chose for the Cleric; attack speed (Blitzopan) for the Warrior and the Scout.
- **Three starter consumables are left out of the kit:** the fruit juice, the Lodas Amulet
  (an XP boost) and the Administrator's Boon (no death penalty for an hour). The reasons
  are in the "Every line" note.
- **Between fights the potion is drunk below the 90% rest target,** the Priest's own rest
  number. In a fight CP-Q11's 75% stands.
- **Each Ishalgen bind is made once.** A later visit to the village, after the bind has
  moved to the outpost, does not move it back.
- **Hub flight in Ishalgen stays off.** The answer to CP-Q21 was about the bind.
- **The Chanter at level 10** gets the same level-10 help bands as the Cleric, because the
  list is keyed by level. It stops right after, so it uses none of them.
- **The kit steps down at level 10** (hazard 27). The best potion a level 1-9 character
  may use is the Major Life Potion; the Cleric's approved level 10-19 band supplies the
  Minor. The Cleric's bands are not changed without the operator's word.
- **The event-scroll finding** (hazard 28), settled by CP-05 on 2026-10-07: the event
  scrolls are as strong as the Greater tier, +30% run speed and +9% casting speed, for 30
  min. The Cleric's bands are weaker than what it owns until level 30. At levels 20-29 the
  approved list supplies the Running Scroll (+20%) and the Awakening Scroll (+6%), and the
  help policy picks them over the owned Accelerox and Castafodin, because the catalog
  files the event scrolls under tier 10. At levels 10-19 it uses the event scrolls, which
  is the stronger choice. Default: left as it is, because OD-13 approved those bands and
  this plan does not change the Cleric's legs. The fix would be one catalog number (the
  event scrolls' tier) and would change the Cleric's baselines from level 20 on.
- **Where the Cleric's kit is weaker than the level 1-9 kit** (CP-05, part 4). The life
  potion: the level 10-19 band supplies the Minor (407 HP), the level 1-9 kit the Major
  (1,694 HP); the Cleric reaches a potion as strong only by leftover stock. The Running
  scroll: nothing is supplied at levels 10-19, the +20% Running Scroll at 20-29, and the
  Greater only from 30. The shield scroll is the same at levels 1-19. Not changed.
- **Accelerox against the Greater Running Scroll.** They give the same +30%, and a starter
  owns 50 Accelerox that last six times as long. Default: the supplied Greater Running
  Scroll is used first, because the operator named it, and Accelerox when none is owned;
  so while the supply runs, Accelerox is not used at levels 1-9. Say so if Accelerox
  should be used first, or kept up at all times and not only before a long leg.
- **The Priest drinks a Major Life Potion at 90% HP** (CP-06, run m-a1): 51 in one run of
  Ishalgen, 37 of them at level 9, each worth more than the Priest's whole HP bar. The
  threshold is the frozen Priest policy's, sized for the 407 HP starter potion. Default:
  left as it is; the supply keeps the stock up. A lower threshold for the stronger potion
  would be a change to the Priest's fight rule, and it must be decided before CP-08.
- **A recovery potion heals mana too.** The Major Recovery Potion 162000045 heals the same
  1,694 HP on the same 30 s delay and restores mana with it, and a level 1 character may
  use it. Default: the Major Life Potion, the family OD-13 approved, with the starter's
  own mana potions for mana.
- **A rest sit writes no trace record** (CP-02, 2026-10-07). The counts mode can only
  count the walks to a rest spot, so "how often does this scope sit" has a lower bound and
  no exact answer. Default: left as it is, because a trace comparison still catches a
  changed sit through the virtual clock. The other choice is one trace line in RestAsync
  (a record per sit), added before CP-08 records the baselines; after CP-08 it would need
  the baselines recorded again. Say so before CP-08 if you want it.

Every other question runs on its default until an Answer line stands under it.

## Out of scope

- Not yet: splitting RunAsync into per-leg runners, or removing its 45 captured locals, the
  three single-slot session hooks (BeforeSend, AfterSynchronize and ResolveForcedLanding)
  and the exception-based control flow. Only the two self-contained nested classes move.
- Not yet: anything past an endpoint. The Chanter stops after the Pandaemonium ceremony (no
  capital first pass, no bridge to Altgard, no Altgard, Haramel or Abyss leg). The five new
  starters stop at Munin with Q2008 untouched; no natural character of theirs fights the
  trial or chooses a second class.
- Not yet: second-class play. No Chanter rotation, no level-10 catalog, no Gladiator,
  Templar, Assassin, Ranger, Sorcerer, Spirit Master, Gunner, Rider or Bard profile.
- Not yet: making later-leg data class-aware (the Altgard and Abyss contracts, reward pins,
  coin-gear and Haramel manifests, air combat with Smite, Hand of Reincarnation, the destiny
  leg's stigma). Their Cleric requirements stay as refusing gates.
- Not yet: moving the Priest's and Cleric's fight, rest or gear decisions onto the table
  policy, or widening the frozen Mau bounds. They stay as code behind adapters.
- Not yet: LIVE. The NI-09 and NI-10 identities, the attach runner and its PowerShell twin
  stay Priest only.
- Not yet: Elyos, and female characters. Runtime hostility is fixed to the Asmodian side
  (Sc/NaturalJourneyRuntime.cs:37-38) and every line is male.
- Not done at all: renaming files, types, the SIM test method, the NaturalJourneyStage
  names, any parsed trace key or any Priest step string. Class names in strings come from
  the line.
- Not done at all: re-capturing or re-verifying any existing snapshot, regenerating the 26
  Ishalgen plans, editing the frozen Ishalgen and Ascension contract files, or re-freezing
  the Mau tools.
- In scope since 2026-10-07: help items at levels 1-9 for every class line, the event
  scrolls and mana potions a starter owns among them (CP-05, CP-06), and the bind at each
  Ishalgen quest hub (CP-07).
- Not yet: a class dimension in the allowlist, a help kit of its own past level 9 for any
  class but the Cleric (the Chanter gets the Cleric's level-10 bands at the ceremony and
  stops), stigma stones, skill books and buying gear.
- Not done: changing the Cleric's help bands from level 10 on, or any Cleric leg. A finding
  there is recorded for the operator.
- Not done at all: bandages. Bandage Heal 245 is not cast, and no bandage is bought or kept
  as a supply.
- Not yet: the rest of the Ishalgen hub optimizer (hub pickup order, safe work groups, hub
  flight). CP-07 takes only the two binds from it.
- Not yet: charge skills (Artist Fiery Descant), Scout Surprise Attack, Counterattack and
  Stealth, Mage kiting, Escape 302.
- Not yet, unless a probe shows the need: decoding SM_ATTACK_RESPONSE, SM_TARGET_SELECTED,
  SM_ABNORMAL_EFFECT or SM_ITEM_COOLDOWN. If the Warrior probe shows a silently refused
  swing, the first becomes a lettered item under CP-43.
- Not yet: a snapshot at the first level 9 or at Q2008 var 6. Those are (b) and (c) of
  question 8, and its answer is (a).
- Not yet: a per-class encounter tuning harness in the style of NA-23 or the Mau course. It
  is built only if a class cannot finish Ishalgen without it.
- Not done: a new manifest scenario id. The class-choice confidence runs are a gated probe,
  so scenarios.json and the system matrix are untouched.
- Not done: server and data changes. None is expected; the missing RIDER in class_permitted
  on 178 of the 197 quests that list GUNNER is recorded, not touched.
- Not done: multi-seed batches, two class runs side by side, the NTC party and its
  controller.
- Not done during the refactor: fixing the findings listed under Hazards (item 8) and in the
  PRIEST and CLERIC note.
- Not built: JSON rule sheets, checked-in generated kit files, and a class-choice capture
  scope that starts from a Munin snapshot. Rule tables are C#, catalogs are generated in
  memory, and the Chanter uses the existing capital-start scope.

## Combined goal prompt

The loop prompt for the session that implements this plan. Paste it after `/loop`.

```text
Work the CP checklist in docs/natural-class-profiles.md, one item per
iteration.

Repository: C:\Users\ryanf\Documents\GitHub\BeyondAionSharp, branch main.
The accepted line is the Asmodian Priest who becomes a Cleric. Its contracts
and snapshots stay as they are. Its levels 1-9 change once, on purpose, in
CP-05 to CP-07 (the operator, 2026-10-07: the help kit and the bind at each
Ishalgen quest hub). In a bridge run the level-10 Cleric finishes Ishalgen
with the same binds. The Cleric's kit and every leg from Altgard on do not
change. From
the baselines of CP-08 and CP-09 on, the journey must play exactly as they
recorded it.

EACH ITERATION
1. Orient. Read CLAUDE.md and AGENTS.md, then docs/natural-class-profiles.md:
   The seam (with "Rules of this list"), Standing rules, Per-class notes,
   Hazards, CP checklist, Operator decisions, Blocked / questions, and the
   last Progress log lines. Follow "How to work this list (loop protocol)" in
   docs/natural-ascension-altgard.md, with CP in place of NA. Where that
   protocol and this document differ, this document wins (rule (m)): the
   protocol's checks are rule (b), its one-run rule is CP-Q3, its OD-3 is the
   Git standing rule, and its full checklist is run at CP-41 and CP-69.
   Run git status. Check for running SIM or journey processes and the newest
   run/ folders. Never build, run checks or start a run while another process
   holds the build outputs. Then apply the re-record rule of "Proof tools and
   the neutral gate": look at git log <baseline sha>..HEAD for a commit from
   outside this list.
2. Pick by rule (h): the first unchecked CP item that is not listed under
   "Blocked / questions for the operator" and whose Depends are all ticked.
   Nothing is blocked at the start: CP-Q3 was answered on 2026-10-07. A
   question with no Answer line runs on its recorded default. CP-Q1, CP-Q3,
   CP-Q8, CP-Q10, CP-Q11, CP-Q12 and CP-Q21 are answered, and an Answer
   outranks its question's Default. If no item can be taken, stop and
   report.
3. Read the Java first (../aion-server, branch 4.8) for every server behavior
   the item relies on. Java wins; a retail correction needs an approved
   decision. No server or data change is expected in this plan.
4. Do only that item. A refactor item changes no behavior: no rename, no
   reordering, no fix. CP-05, CP-06 and CP-07 are not refactor items: they
   change the Priest's levels 1-9 on purpose, before the baselines. A
   finding met on the way is written into the doc, not fixed.
5. Run the item's one proof. For a journey item: two attempts at most; a fix
   inside the item is one small change (a profile number, a reward pick, one
   attempt-budget override with its reason). Anything larger, and any
   second failure, goes under Blocked and becomes a lettered item (rule
   (i)): it is written directly after its parent with its own Depends and
   Proof lines and added to the parent's Depends, which takes the parent off
   Blocked; the parent is then retried with a fresh two-attempt budget. A
   fix to shared code also re-runs the gate set of the item that introduced
   that code (rule (k)). An item that means to change how a new class plays
   re-records that class's scope twice (rule (j)); Priest and Cleric scopes
   are never re-recorded that way. Keep failed evidence under run/cp/.
6. Before committing, run the bundle of rule (b) in "Rules of this list": the
   seven pre-commit checks one after another (check-warning-baseline,
   check-null-loggers, check-clock-reads, check-custom-quest-drafts,
   check_fidelity.py, test-quest-plan-compiler.py,
   test-retail-quest-inventory.py); the Aion.GameServer.Tests project when
   tests/Aion.Bots changed; the script tests when scripts/sim changed; and
   scripts/e2e/run-fast.ps1 unless the commit is docs or evidence only. Once
   CP-08 is ticked, and from CP-13 on, also run the guard of rule (c).
7. Record: tick the box, add a dated evidence line with run ids and numbers,
   and append one Progress log line.
8. Commit on main: stage only the item's files, one commit per item,
   imperative subject, the CP id and the evidence in the body, no co-author
   or attribution trailer. Never push. A capture item makes two commits: the
   code commit, then the capture and its Verify, then one evidence-only
   commit (rule (d)).
9. If an item needs an operator decision or exposes a defect shared with Java,
   write it under "Blocked / questions for the operator" and pick again by
   rule (h). If no item can be taken, stop the loop and report.

OPERATOR RULES (every class and level)
- Natural play: no GM input in a journey, apart from the approved help
  items, which are on for every class line. GM setup only in probes on free
  SIM accounts (CP-31 allocates them), and each probe says what it prepared.
- Deaths, retreats, lost timers and failed attempts are recorded outcomes,
  not test failures. Death is not failure.
- The death rule is the bot's: whenever it dies it revives at the obelisk it
  is bound to, the working hub's, and soul heals at the Soul Healer beside
  it; in an instance it revives in the instance and heals after the next
  obelisk resurrection.
- Run the inventory check after every quest turn-in: wear better gear, open
  reward containers, discard what is to be discarded, check cube space
  (CP-Q13 for how it applies in Ishalgen).
- Gear is decided by each class's profile. The Cleric keeps its staff rule.
  The Warrior takes the weapon at Q2100, not the shield (CP-Q10). No gear is
  bought at levels 1-9.
- No level goals. Never hunt or soul heal for a level.
- Loot every kill.
- Bind at the working hub's obelisk everywhere, Ishalgen included: the
  village (obelisk 700063) and the outpost (obelisk 700064), each on first
  arrival there for work, for every line, the Priest included (CP-Q21,
  CP-07). Use hub flight transporters from Altgard on; in Ishalgen the hub
  flight stays off.
- Help items are on for every class line and every level, the Priest's
  levels 1-9 included: the best healing potion the level may use, the shield
  scroll, the Greater Running Scroll, and any other consumable that makes
  the class more survivable or faster (CP-Q12). Write each kit into the doc
  as a manifest before its first use; every supplied item is listed in the
  run's help-items.json. Never set NA_HELP_ITEMS=0. The Cleric's kit from
  level 10 on stays as approved (OD-13). No stigma, no skill books.
- No bandages. A class without a heal drinks a life potion when its HP is
  below the target and the potion is ready, in a fight and between fights,
  and sits while the potion is on its delay (CP-Q11).
- Write a class's gear and reward picks into the doc before its first run.

STANDING RULES
- SIM only, seed 1. Fresh-create runs are part of this plan (CP-Q3, answered
  2026-10-07); every run uses its own throwaway schema and drops it. No
  multi-seed batches. No LIVE run. Do not touch the operator's aion stack.
- Do not rename or move the files listed under "Not renamed or moved". Do not
  edit the frozen Ishalgen and Ascension contract files or regenerate the
  Ishalgen plans.
- Capture snapshots only from committed code, under new names. Never
  overwrite, recapture or re-verify an existing snapshot; the old snapshot
  munin stays as it is. A capture that fails its acceptance keeps its name
  and is logged as rejected; the second attempt is captured as <name>-a2
  (rule (l)).
- Never branch, use worktrees, spawn subagents or push. Never check out an
  older commit. Do not hand-edit generated data or raise the warning baseline.
- Keep the bot monitor at http://127.0.0.1:17880/ available during runs and
  tell me when a run can be watched.
- Re-record rule: if git log <baseline sha>..HEAD shows a commit from outside
  this list under src, tests, game-server or parity-artifacts, run the full
  gate at HEAD before taking an item. If a scope fails, stop and report: I
  decide between re-recording at HEAD and reverting. An outside commit already
  logged with a passing verdict is not checked again. An upstream port may
  bump javaReference in the class-lines file.
- Leave these untracked files alone: docs/playtest-aethertapping-plan.md,
  docs/playtest-crafting-alchemy-cooking-plan.md,
  docs/playtest-ground-essencetapping-plan.md.

STOP when CP-69 is ticked, when no item can be taken, or when an item or a
rule says stop and report (CP-08, CP-09, the re-record rule, the full-gate
failure rule). The two natural pauses are after CP-45 (the seam closed, with
a Warrior in the field; say whether the Chanter capture CP-33 is ticked or
blocked) and after CP-57 (Mage and Warrior at Munin, both class scopes
recorded): report there and go on unless I say otherwise. When you stop,
report what was done, what is blocked and what you need from me.
```

## Progress log

- 2026-10-06 — The plan was written from a read-only review of the bot (27 review agents:
  fifteen readers, one merge, three plans, three judges, two synthesis passes and three
  critics; nothing was built or run). No item is started. CP-00 commits this document.
- 2026-10-06 — A fact check and a loop-fit review were applied to the plan. The checklist
  was renumbered once and then ran from CP-00 to CP-66, 67 items (in the numbering of that
  day); ids quoted from the first draft no longer apply. Answer lines were recorded for
  CP-Q1 and CP-Q8 from the request. CP-Q3 was open, so CP-03 was listed under Blocked.
  Still nothing is built or run.
- 2026-10-07 — The operator read the plan and answered. Recorded as Answer lines: CP-Q3
  ("Your recommended"), CP-Q10 (the Warrior takes the weapon at Q2100), CP-Q11 (no
  bandages; potions, and a sit while the potion is on its delay), CP-Q12 (help items on
  for every class line, the Priest's levels 1-9 included) and CP-Q21 (bind at each
  Ishalgen quest hub, the village and the outpost, for every line). CP-Q1 and CP-Q8 were
  confirmed ("Good"). Three items were added in Phase A, before the baselines: CP-05 (the
  level 1-9 help kit: manifest and allowlist), CP-06 (the Priest plays levels 1-9 with the
  kit) and CP-07 (bind at each Ishalgen quest hub). The checklist was renumbered a second
  time: the items that were CP-05 to CP-66 are now CP-08 to CP-69, each id three higher,
  and CP-00 to CP-04 keep their ids. The list runs from CP-00 to CP-69, 70 items. Every
  bandage step was rewritten to potion and sit, and nothing is blocked any more. The item
  data, the help-item code and the Ishalgen bind code were read for this; nothing was
  built or run.
- 2026-10-07 — Loop: CP-00 done. The plan is committed with decision D39, the pointer row
  in CLAUDE.md and one dated line in each of the readiness report, the Ascension document
  (OD-13) and the Altgard leveling document (the standing bind policy). Six files; seven
  pre-commit checks pass (run/cp/CP-00/checks/). No baseline exists yet, so the re-record
  rule did not apply. Next by rule (h): CP-01.
- 2026-10-07 — Loop: CP-01 done. The class-line contract e2e/natural-class-lines.json is frozen
  for six starters and eleven second classes, with its loader and
  UT/NaturalClassLineContractTests (20 of 20). The 4.8 client's class pages are verified
  from the checked-in dialog map: each shows exactly its own starter's second classes.
  Bundle: seven checks, Aion.GameServer.Tests 4,675 passed, Fast 11 gates (cp01-fast). No
  baseline exists yet, so the re-record rule did not apply. Next by rule (h): CP-02.
- 2026-10-07 — Loop: CP-02 done. scripts/sim/trace/compare_traces.py compares two traces
  record by record, counts what one trace holds (--counts, 22 numbers) and prints the hash
  a baseline is stored by (--digest). Its test passes 10 of 10. Finding: a rest sit writes
  no trace record, so restSitsTraced is a lower bound; it is listed under "Not blocking" for
  the operator, to be decided before CP-08. Bundle: seven checks, both script tests, Fast 11
  gates (cp02-fast). No baseline exists yet, so the re-record rule did not apply. Next by
  rule (h): CP-03.
- 2026-10-07 — Loop: CP-03 done. sim-snapshot.ps1 has -Action Replay: one scope on a fresh or a
  restored owned schema, evidence under run/cp/<item>/<run>/, nothing captured, the schema
  always dropped; -StopAt takes status 5. new-sim-db.ps1 takes -Docker. The shared runner now
  clears NA_HELP_ITEMS, AION_BOT_DASHBOARD_PORT and AION_SIM_PROCESS_KEY for Capture and
  Verify too. Proof: test-sim-snapshot.ps1 with the Replay cases. No journey was played; CP-06
  is the first real Replay. Bundle: seven checks, both script tests, Fast 11 gates
  (cp03-fast). No baseline exists yet, so the re-record rule did not apply. Next by rule (h):
  CP-04.
- 2026-10-07 — Loop: CP-04 done. scripts/sim/run-neutral-gate.ps1 replays named scopes through
  Replay after one build and compares each trace with its baseline row; -Record plays each
  scope twice and writes the baseline file once. Verdicts: pass, fail, refused-dirty-record.
  Added beyond the Work line: -ReRecord for a Priest or Cleric scope that already has a row,
  -Ignore stored per row, and a per-row baseline commit. Both script tests are in CLAUDE.md's
  list. Proof: test-sim-snapshot.ps1 with the gate cases. No journey was played. Bundle: seven
  checks, both script tests, Fast 11 gates (cp04-fast). Phase A's tools are built; next by
  rule (h): CP-05.
- 2026-10-07 — Loop: CP-05 done. The level 1-9 kit is written down: the manifest table (three
  supplied items, nine owned ones, each used or left out with its reason) and
  NaturalHelpItemAllowlist.Starter beside the unedited Approved. The event-scroll finding is
  settled: the event scrolls are as strong as the Greater tier and the bot's catalog files
  them too low; not edited, recorded under "Not blocking" with three more points for the
  operator. Proof: UT/NaturalStarterHelpKitTests 6 of 6. Bundle: seven checks,
  Aion.GameServer.Tests 4,681 passed, Fast 11 gates (cp05-fast). Nothing supplies the kit
  yet. Next by rule (h): CP-06, the first journey run.
- 2026-10-07 — Loop: CP-06 done on the first attempt. The Priest is supplied and uses the level
  1-9 kit. Run m-a1 (scope m) passed in 7 min 51 s to the Munin stop with no death: 51 Major
  Life Potions, 3 Lesser Anti-Shock Scrolls, 21 Greater Running Scrolls and 7 Castafodin, all
  below level 10; no mana potion. Against the old munin capture: deaths 3 to 0, emergency
  decisions 14 to 0, between-fight heals 30 to 12. One point for the operator before CP-08:
  the Priest drinks the Major potion at 90% HP. Bundle: seven checks, Aion.GameServer.Tests
  4,690 passed, Fast 11 gates (cp06-fast). Next by rule (h): CP-07, the Ishalgen binds.
- 2026-10-07 — Loop: CP-07 done on the second attempt. Every line binds at the village obelisk
  (43 Kinah) and at the outpost obelisk (134 Kinah), each before the first quest of its hub.
  Run m-a2 (scope m) passed in 5 min 20 s to the Munin stop: both binds in the trace, one death
  revived at the outpost and soul healed at Rusalka for 71 Kinah, Q2129's Return branch ran,
  40,342 Kinah left. Attempt 1 failed and is kept: the client shows a bind point before any
  bind, which had held the village bind back (the one small fix); and a kill budget ran out on
  monsters walking home in Q2128 (hazard 30, logged). Bundle: seven checks,
  Aion.GameServer.Tests 4,695 passed, Fast 11 gates (cp07-fast). The Priest's levels 1-9 are
  now as the baselines will record them. Next by rule (h): CP-08.
- 2026-10-07 — Loop: CP-08 tried, not ticked. Record run rec-a1: scope c repeated (96,166
  records in both passes), scope p failed in its first pass at the walk to Munin for Q2008:
  the new levels 1-9 reach level 9 one fight later in the Mau field, and no checked route
  was left from there. Nothing was recorded. Written as lettered item CP-07a (Return to the
  hub bind and walk from there), added to CP-08's Depends. Docs only; seven checks. Next by
  rule (h): CP-07a.
- 2026-10-07 — Loop: CP-07a done on the first attempt. In the early-Ascension order the first
  walk to Munin now casts Return to the hub bind when no checked route is left. Run p-a1
  (scope p) passed in 38 s to the verified capital-start checkpoint: the fallback fired at
  (717, 1485), Return landed at the outpost, and the Priest walked to Munin from there.
  Bundle: seven checks, Aion.GameServer.Tests, Fast 11 gates (cp07a-fast). CP-08's Depends
  are all ticked again. Next by rule (h): CP-08.
- 2026-10-07 — Loop: CP-08 done. Record run rec-a2 at 200ec4c24: p (35,811 records, 38 s a
  pass) and c (96,166 records, 2 min 17 s a pass) each repeated record for record, with no
  ignore list. e2e/natural-neutral-baseline.json is written and the traces are kept in both
  places. The baseline sha is 200ec4c24: the re-record rule and the guard of rule (c) apply
  from here. Evidence only; seven checks. Next by rule (h): CP-09.
- 2026-10-07 — Loop: CP-09 done. Record run rec-a1 at 5ffbac512: m, b, l1, hm and ax each
  repeated record for record, so all seven scopes are kept and none is blocked. Scope b, the
  whole early-Ascension bridge with the kit and the binds, passed on its first run. The
  table "What each scope reaches" is written from the baselines; the check of every later
  item's scope against it found no Proof line to edit. Logged: the counts mode misses the
  Abyss-entry leg's coin-armor purchases (7 in ax). The baseline sha stays 200ec4c24.
  Evidence only; seven checks. Phase A is one item from done. Next by rule (h): CP-10.
- 2026-10-07 — Loop: CP-10 done on the first attempt, no code change. Run stop-a1 at
  a6fa1f0b4: the Priest stopped at 2132:5:0 at level 6 in Aldelle Village, 14 min 36 s of
  game time, and resume-receipt.json holds 2132 in CompletedQuestIds. Hazard 18 is answered.
  Evidence only; seven checks. Phase A is done. Next by rule (h): CP-11.
- 2026-10-07 — Loop: CP-11 done. UT/NaturalClassSeamPinTests pins 58 numbers: 23 asserted
  now and 35 pending (CP-16 4, CP-17 4, CP-18 14, CP-19 2, CP-21 11). A pending row is
  checked against the source: the test finds its literal, prints file and line, and fails
  when the literal is gone. Found and logged: old J:6794 holds no literal, so it has no row.
  Seven checks, 4,698 with 16 skipped and Fast (cp11-fast) pass. Next by rule (h): CP-12.
- 2026-10-07 — Loop: CP-12 done. UT/NaturalGearGoldenTests and
  e2e/natural-gear-golden.txt (3,351 lines) hold the Priest's and the Cleric's gear decisions
  on the code the baselines were recorded on: 2,207 items, 765 reward quests, levels 1-26,
  with the coin-gear and Haramel variants and a level-after-level worn state. No bot code is
  touched. Seven checks, 4,699 with 16 skipped and Fast (cp12-fast) pass. Next by rule (h): CP-13.
- 2026-10-07 — Loop: CP-13 done, the first refactor item. The navigator and combat classes
  are in NaturalIshalgenJourney.Navigator.cs and .Combat.cs, moved line for line; the journey
  class is partial. Gate gate-a1, set p+c: both traces identical to the baselines. The pin
  test followed the move without an edit. Seven checks, 4,699 with 16 skipped and Fast
  (cp13-fast) pass. Next by rule (h): CP-14.
- 2026-10-07 — Loop: CP-14 done. The seam types are added under Sc/Classes: NaturalClassLine
  with the table of lines (priest-cleric first and default), INaturalCombatPolicy,
  NaturalClassProfile with NaturalClassProfiles.For, and the Priest and Cleric profiles as
  adapters over the static policy. Nothing calls them yet and no existing file changes.
  UT/NaturalClassProfileTests, 6 tests: the 576-state sweep answers as the static policy
  does. Seven checks, 4,705 with 16 skipped, guard p (guard-a1) and Fast (cp14-fast)
  pass. Next by rule (h): CP-15.
- 2026-10-07 — Loop: CP-15 done. The class line rides on NaturalJourneyOptions.ClassLine and
  reaches the combat class at its four constructions; the fight loop's catalog, Decide,
  CandidateActions and policy version go through the observed class's profile. The journey
  test reads CP_CLASS; the three scripts clear it. Gate gate-a1, set p+c: identical. Seven
  checks, 4,705 with 16 skipped, the script tests and Fast (cp15-fast) pass. Next by rule (h):
  CP-16.
- 2026-10-07 — Loop: CP-16 done. The emergency band is on the combat policy; the help-item
  gates and kit, the upkeep list, the patrol rule and the ranged hold are on the profile, and
  the fight loop, MaintainBuffsAsync, BuffOurselfAsync, the supply and the patrol code read
  them. DecideBuffs takes the shared slot's scroll. Gate gate-a1, set p+m+c: identical. The
  pin test has 27 rows asserted, 31 pending. Seven checks, 4,710 with 16 skipped and
  Fast (cp16-fast) pass. Next by rule (h): CP-17.
- 2026-10-07 — Loop: CP-17 done. Sc/Classes/NaturalRestRules.cs holds the rest decisions as a
  pure Decide on the profile; RestAsync observes, asks and carries out. A 49,152-state sweep
  agrees with the old inline logic. Gate gate-a1, set p+m+c: identical. The pin test has 31
  rows asserted, 27 pending. Seven checks, 4,713 with 16 skipped and Fast (cp17-fast)
  pass. Next by rule (h): CP-18.
- 2026-10-07 — Loop: CP-18 done. Sc/Classes/NaturalEngageRules.cs names eleven engage
  distances and four readiness thresholds on the profile; the shared helpers ask it, and
  three navigation helpers take an optional range. MeleeReach is shared combat geometry.
  Gate gate-a1, set p+m+c+hm: identical. The pin test has 45 rows asserted, 13 pending.
  Seven checks, 4,717 with 16 skipped and Fast (cp18-fast) pass. Next by rule (h): CP-19.
- 2026-10-07 — Loop: CP-19 done. Sc/Classes/NaturalFightMovement.cs holds the pull style and
  the four movement answers of a fight; the fight loop asks the profile. The Priest line is a
  stand-off with today's numbers; walk-in and weapon-range styles are defined, not played.
  Gate gate-a1, set p+m+c: identical. The pin test has 47 rows asserted, 11 pending (CP-21).
  Seven checks, 4,719 with 16 skipped and Fast (cp19-fast) pass. Next by rule (h): CP-20.
- 2026-10-07 — Loop: CP-20 done. Q2132 takes its var and trainer from the class-line
  contract and the graph names the line's trainer; the journey's step labels and messages
  take the class name from the line (priest, Priest). Gate gate-a1, set p+m: identical.
  Seven checks, 4,719 with 16 skipped and Fast (cp20-fast) pass. Next by rule (h): CP-21.
- 2026-10-07 — Loop: CP-21 done. NaturalCampaignRules names the Ishalgen campaign's six
  ranges, Return's 75% and three HP thresholds on the profile; eleven sites read it. Gate
  gate-a1, set p+m: identical. The pin test has all 58 rows asserted and none pending.
  Seven checks, 4,720 with 16 skipped and Fast (cp21-fast) pass. Next by rule (h): CP-22.
- 2026-10-07 — Loop: CP-22 done. Sc/Classes/NaturalGearRules.cs holds what a class treats as
  gear, how it ranks it and what it keeps; the inventory policy has one Decide path, the item
  carries the whole restrict row, and the staff rule reads its group from the rules. The
  golden gear test is green without an edit. Gate gate-a1, set p+m+b+c+ax: identical. Seven
  checks, 4,725 with 16 skipped and Fast (cp22-fast) pass. Next by rule (h): CP-23.
- 2026-10-07 — Loop: CP-23 done. Decide(world) takes the observed class's gear rules by the
  policy's line, ChooseReward takes the profile's reward rules (the Priest's for both classes
  of the accepted line), and the equipment check describes gear by the same rules. CP-Q13's
  default is wired and has not run yet. Gate gate-a1, set p+m+b+c+ax: identical. Seven
  checks, 4,726 with 16 skipped and Fast (cp23-fast) pass. Next by rule (h): CP-24.
- 2026-10-07 — Loop: CP-24 done. Sc/Classes/NaturalRestockRules.cs is the profile's restock
  table with a Kinah floor; the Priest line's table is one line of Minor Life Elixirs (5,
  12, list 721) and no table can name the bandage list. MaintainInventoryAsync asks it. The
  Priest's Kinah ledger from the m baseline is written into the item. Gate gate-a1, set p+m:
  identical. Seven checks, 4,731 with 16 skipped and Fast (cp24-fast) pass. Next by rule
  (h): CP-25.
- 2026-10-07 — Loop: CP-25 done. NaturalJourneyIdentityRules.Classify takes a class line and the
  old overloads delegate with the Priest line. NaturalAscensionContract gains ForLine,
  ForChoice and Step(role); the Priest-Cleric bridge built in memory equals the reviewed file
  and all eleven pairs are recomputed from quest_data.xml and the handlers
  (UT/NaturalClassLineSeamTests, 60 tests). Nothing calls the new members yet. Guard guard-a1,
  set p: identical. Seven checks, 4,791 with 16 skipped and Fast (cp25-fast) pass. Next
  by rule (h): CP-26.
- 2026-10-07 — Loop: CP-26 done. The runner builds the line's bridge once (LineBridge) and the
  bridge engine, the talk handler, the teleporter steps and the inventory policy read the
  class pair, the four class-dependent steps and the dispatch quest from it. The engine's
  identity check uses the contract's pair, so a Chanter's bridge accepts a Chanter. Gate
  gate-a1, set p+b+c: identical. Seven checks, 4,795 with 16 skipped and Fast (cp26-fast)
  pass. Next by rule (h): CP-27.
- 2026-10-07 — Loop: CP-27 done. The early-ceremony check and the three SIM identity checks use
  the line's second class (SimulationL0Session.IdentityClassLine, set by the host). The
  capital pass, the Ishalgen return and the four leg gates stay the Cleric's and each refusal
  names the class it met. Gate gate-a1, set p+b+c: identical. Seven checks, 4,798 with 16 skipped
  and Fast (cp27-fast) pass. Next by rule (h): CP-28.
- 2026-10-07 — Loop: CP-28 done. sim-snapshot.ps1 takes -Class on Capture and Replay, records
  classLine for a line other than the accepted one, prints it back as CP_CLASS on Restore,
  and restores a capital snapshot of a line with no capital leg on the start stage. The
  accepted line's snapshots, restores and receipts are unchanged. test-sim-snapshot.ps1
  passes with the new cases; guard guard-a1, set p: identical. Seven checks and Fast
  (cp28-fast) pass. Phase B, the class seam, is complete. Next by rule (h): CP-29.
- 2026-10-07 — Loop: CP-29 done. Sc/Classes/NaturalClassGearTable.cs gives every class but the
  Priest and the Cleric gear rules in table form: wearable groups from the mastery rows, one
  score for the equipment check, keep or sell and the reward choice, no off hand. The five
  starters' defaults and their picks at the ten class-dependent rewards are written into the
  item. UT/NaturalClassGearRuleTests, 16 tests. Gate gate-a1, set all: identical. Seven
  checks, 4,814 with 16 skipped and Fast (cp29-fast) pass. Next by rule (h): CP-30.
- 2026-10-07 — Loop: CP-30 done. CapitalAscensionScenario.RunAsmodianAsync reads the dispatch
  quest, the chosen class, the Q2009 var and the four class-dependent steps from its contract,
  and has the short endpoint stopAtDispatchStart. CAPITAL-ASMO passes alone, 12 steps
  (run/cp/CP-30/capital-a1). Guard guard-a1, set p: identical. Seven checks, 4,814 with 16 skipped
  and Fast (cp30-fast) pass. Next by rule (h): CP-31.
- 2026-10-07 — Loop: CP-31 done. The probe accounts are 98 and 100, added to the fixture; two rows
  to a process. NaturalClassChoiceProbe, rows cleric and chanter, passes on the first attempt
  (run/cp/CP-31/probe-a1.log): SETPRO13 gives a Chanter, id 11, with the six masteries, the
  Karmic Staff from its own list, and Q2904 at START/1 after Doman. Guard guard-a1, set p:
  identical. Seven checks and Fast (cp31-fast) pass. Next by rule (h): CP-32.
- 2026-10-07 — Operator, mid-run: (1) the Priest and the Cleric are to use the table gear rules
  like every other class; item CP-29a is written after CP-29 and waits under Blocked for
  CP-Q22, the word on re-recording the Priest and Cleric baselines. (2) No new unit tests,
  and the plan's tests removed unless necessary: rule (n). Twelve test files and the golden
  gear file are removed, seven older test files are back at their text from before the plan,
  and three tests are kept. The build and the remaining Aion.GameServer.Tests pass (4,676 with 16 skipped).
- 2026-10-07 — Loop: CP-32 done. Line priest-chanter and Sc/Classes/NaturalChanterProfile.cs: the
  Priest's play with every Chanter skill excluded, and gear in table form (mace or staff by
  the physical stat, so the Karmic Staff is worn). A Priest-starter line keeps the accepted
  Priest's gear check points. No unit test (rule (n)). Guard guard-a1, set p: identical.
  Seven checks, 4,676 with 16 skipped and Fast (cp32-fast) pass. Next by rule (h): CP-33.
- 2026-10-07 — Loop: CP-33 done, first attempt. Snapshot pandaemonium-chanter-start-s1 (character
  133276, captured at da2a7d3b2): a level-10 Chanter in Pandaemonium with the Karmic Staff worn
  and Q2904 at START/0. Its trace first differs from baseline p at the class-choice decision,
  record 35,065. Verify passes with CP_CLASS=priest-chanter and PC_CAPITAL=start. Phase C, the
  Chanter branch, is complete but for CP-29a, which waits for CP-Q22. Next by rule (h): CP-34.
- 2026-10-07 — Loop: CP-34 done. NaturalNewSkillTrainerProbe, six rows two to a process: every
  starter is refused at another class's trainer and paid at its own with the contract's var
  and page (run/cp/CP-34/probe-final-1..3.log). Finding: the server holds the Engineer's and
  the Artist's trainer twice, the second about 2.4 m above the first; cause not found, offered
  as a separate task. Guard guard-a1, set p: identical. Seven checks and Fast (cp34-fast)
  pass. Next by rule (h): CP-35.
- 2026-10-07 — Operator: CP-Q22 answered yes; CP-29a is unblocked and is the next pick. Gear by
  class type, written into CP-29a: Priest types leather, then chain after Ascension, and the
  staff by magic boost after Ascension (CP-Q7 answered for the Chanter); Mage types cloth and
  the spellbook; Warrior types chain, then plate, Templar sword and shield, Gladiator the
  two-handed sword. No class gets a branch of its own: where the table cannot say it, the
  table is widened. The loop is holding builds while another session edits this tree.
- 2026-10-07 — Loop stopped by the re-record rule. Outside commit 6bdb96d66 (the trainer fix)
  shifts every object id by ten and removes the two extra trainers from view. Full gate at
  50db7f2d4 (run/cp/rerecord-6bdb96d66/gate-a1): all seven scopes differ, every journey ran,
  and all 21 step counts equal the baselines'. CP-Q23 asks the operator: re-record at HEAD or
  revert. CP-29a is unblocked and waits behind it.
- 2026-10-07 — Operator: CP-Q23 answered ("Resume the loop", the trainer fix stays). Outside
  commits since the stop: 23e370c6f (both trainers back on their Java spots) and be9837cd8
  (docs). Loop: the seven baselines re-recorded at be9837cd8, item rerecord-23e370c6f, run
  record-a1: pass, every scope repeats. p 35,789, m 123,046, b 128,869, l1 30,693, c 96,166,
  hm 39,564, ax 15,762 records; all 21 step counts of each row equal the old row's. The
  baseline sha is be9837cd8. Docs and evidence only: no bundle run. Next by rule (h): CP-29a.
- 2026-10-07 — Loop: CP-29a, code. The Priest and the Cleric are on the table gear rules; the
  staff branch, the class-blind reward choice and the Ascension flag are gone. Widened for
  every class: weapon groups are an order, a table names its kept consumables, armor ranks by
  item level and then type (CP-Q24, on its default). Gate gate-a1, set all: every journey ran;
  l1 and hm identical; c differs by one reward pick and ax by an equip order, both with the
  baseline's counts; p, m and b change from Q2001's reward (leather), and the Priest reaches
  Munin with no death and 36 potions against 69. Seven checks, unit suite and Fast
  (cp29a-fast-a2) pass. The re-record of the seven scopes follows from this commit.
