# Retail 4.8 quest completion (D32)

**Status (2026-09-30): RQ-01 to RQ-04 done and approved; the per-quest review is done. The work list is [retail-quest-worklist.md](retail-quest-worklist.md); RQ-05 (the Altgard pilot) is in progress.** This is the handoff for a separate session.
Read this file, then `CLAUDE.md`, before doing anything.

## Goals (the maintainer, 2026-09-30)

- **Playtest every quest and mechanic of Aion** as the game had them. The natural bots
  (`docs/natural-altgard-leveling.md` and the legs before it) play the quests the way a player
  does. A quest the server cannot run is a quest nobody can playtest.
- **Parity with Aion 4.8 retail, as it was historically.** The Java `aionemu` 4.8 source
  (`../aion-server`, branch `4.8`) is the base this port started from, **not the target**. It
  has bugs and missing content of its own. Where 4.8 retail evidence shows Java wrong or
  missing, retail wins, under a logged decision.
- **This workstream:** every quest that the 4.8 client ships and 4.8 retail ran, and that has
  no handler here, gets one. Each is tested, logged, and offered upstream.

## The decision

**D32** (`docs/e2e-player-simulation-plan.md`, decision table), authorized by the maintainer
on 2026-09-30.

**In scope:** C# quest handlers, or `quest_script_data` template entries, for quests that
meet all three of:
- the 4.8 client ships the quest (`Data\Quest\Quest.pak`, `quest.xml`);
- 4.8 retail ran it (aioncodex `/48/`, or the fandom wiki);
- Java 4.8 has no handler for it.

Quest data corrections and quest-specific spawns or NPC templates are in scope when a quest
cannot run without them and 4.8 evidence shows them. They are listed per batch so the
maintainer sees them.

**Out of scope:**
- **Superseded quests.** The pre-4.0 Altgard missions Q2011–Q2022 are an example: the 4.x
  quests exclude a character who did them (Q24013 needs Q2016 and Q2200 unfinished and not
  acquired; Q24113 the same for Q2017 and Q2200), so a new 4.8 character was never offered
  them.
- Test-zone, event and level-99 quests.
- Anything only 5.8 or later has. The `CLAUDE.md` 5.8 boundary still holds.

**Every quest added:**
- a deviation row in §7 of `docs/e2e-player-simulation-plan.md`, with the evidence;
- a SIM test that plays it;
- a Java patch in `docs/upstream-reports/`, for the maintainer's combined upstream PR.

## What is known (2026-09-30)

- **440 quests have no handler** in Java or C# (the checked-in classifier
  `parity-artifacts/e2e/obtainable-quests.json`, availability `no_handler`):
  - 83 are "Test zone" entries;
  - of the rest, 164 are Asmodian-only, 214 Elyos-only and 62 for both races.
  - The largest zones: Eltnen 26, Beluslan 23, Sandstorm 23, Altgard 20, Verteron 19,
    Heiron 18, Morheim 17, Inggison 17, Gelkmaros 15; "Hero" has 32.
- **Altgard's 20** (found while planning the natural bot's Leg 4, Basfelt Village):
  - **Q24113** Sword to Secrecy (after Q24112), **Q24232** Little Help from a Daeva and
    **Q24233** Adieu to You, Manumumu (both IMPORTANT, after Q24112);
  - **Q24110** Control Altgard, Delete Revolutions (IMPORTANT), **Q24111** What's Up, Dock?,
    **Q24114** You Gotta Stop Umkata, **Q24115** A Shugo Apropos;
  - the missions Q2011–Q2022, which are superseded (above).
- **A worked example, Q24113.**
  - The client's `quest.xml` has it: level 12 (client level 17), after Q24112, excluding Q2200
    and Q2017. It collects `quest_24113a`, 100% from `LehparWaChD_18_An`. The reward is 16,066
    XP, 21,600 Kinah, 5 `remedy_hp_mp_40a` and 3 `wrap_d_coin_copper_10_0`.
  - aioncodex `/48/quest/24113` gives the rest: it starts and ends with **Aurtri** (Altgard
    Observatory). Kill Lepharist Revolutionaries, take Tiamat's Sword from their leader, and
    return.
  - The world is ready: Aurtri (203654) and Commander Gattban (210532, level 18) are both
    spawned in `220030000_Altgard.xml`. **Only the handler is missing.**
  - The server's `quest_data.xml` already carries the quest's facts.

## Sources

**The 4.8 client (local, the strongest evidence).** It is at
`C:\Program Files (x86)\Beyond Aion`. The tools are in `tools/client-extract/`: `aionpak.py`
(the `.pak` layer) and `bxml.py` (the binary-XML layer); the formats are in its `README.md`.
- **`Data\Quest\Quest.pak`** has six entries:
  - **`quest.xml`** (8,370 quests): `id`, `category1` (quest, important, mission), `category2`
    (the zone string), `client_level`, `minlevel_permitted`, `finished_quest_cond*`,
    `unfinished_quest_cond*`, `noacquired_quest_cond*`, `collect_item*`, `drop_monster_*` (client
    NPC names), `drop_prob_*`, the rewards, `class_permitted` and `race_permitted`;
  - **`quest_monster.csv`** and **`quest_script_monster.csv`**: per quest and progress step,
    the kill targets (`killedByUser`), drop sources (`questItemDropMonster`) and gather sources,
    as client NPC names;
  - `challenge_task.xml`, `combine_task.xml` and `data_driven_quest.xml`.
- **Client NPC names map to ids** through `Data\Npcs\Npcs.pak`
  (`client_npcs_npc.xml`, `client_npcs_monster.xml`). `tools/client-extract/client_npc_names.py`
  already reads them.
- **The quest dialogs** (`quest_q<ID>.html`) give each dialog page and its buttons: the step
  topology. After the `.pak` layer they have a second encryption layer (they start with
  `0x81`), keyed by the original file name. `tools/client-extract/extract_quest_dialog_map.py`
  parses them once decoded. Find the `.pak` with `index_paks.py`.
- **The strings** (`Data\Strings`) have the quest names and the journal step texts
  (`STR_QUEST_*`).

**aioncodex `/48/` (online):** `https://aioncodex.com/48/quest/<id>/?sl=1`.
- It gives the start and end NPCs with their places, the steps in order, the prerequisites and
  the rewards.
- Use `/48/`, never `/enc/`, which is today's live game: some quests were rewritten or
  removed.
- WebFetch reads it.

**The fandom wiki (online):** aion.fandom.com answers HTTP 402 to WebFetch. Read it in the
built-in browser pane (`get_page_text`). The maintainer cites it as a retail source.

**Upstream Java (online):** `beyond-aion/aion-server`, branch `4.8`.
- This port's base is `ce54b7931`. `docs/upstream-reports/` already checked patches against an
  export of `upstream/4.8` at `a31c1dfcb`, which is newer.
- Before writing a handler, check whether upstream has added it since. If it has, port it
  through the upstream queue instead (`docs/upstream-porting.md`, `docs/upstream-port-log.md`;
  one Java commit at a time, with an `Upstream-Java-SHA` trailer).

**This server's own data:**
- `game-server/data/static_data/quest_data/quest_data.xml` (the quest facts);
- `quest_script_data/*.xml` (template quests);
- `spawns/`, `npcs/` and `items/`;
- the classifier `scripts/e2e/compile-quest-plans.py`.

## Classification

Each no-handler quest lands in exactly one class:

| Class | Meaning | What happens |
|---|---|---|
| **A** | Live in 4.8 retail; every NPC, monster and item it needs exists and is spawned | Implement: a template entry when a template fits, else a C# handler |
| **B** | Live in 4.8 retail; something it needs is missing (a spawn, a template, an item or drop) | List the missing pieces with their evidence; the maintainer approves per batch |
| **C** | Superseded in 4.8 (a 4.x quest excludes it, or retail no longer offered it) | Exclude, with the evidence |
| **D** | Test zone, event, level 99, or 5.8-only | Out of scope |
| **E** | The evidence disagrees or is missing | Ask the maintainer |

## TODO list

The same loop discipline as the natural legs: one item at a time, verify, then commit on
`main` with the evidence.

- [x] **RQ-01 — The inventory (read-only).** A script under `tools/client-extract/` (or
  `scripts/e2e/`) that joins, for every `no_handler` quest:
  - the classifier;
  - the client's `quest.xml` entry;
  - its `quest_monster.csv` and `quest_script_monster.csv` rows, mapped to NPC ids;
  - whether each NPC, monster and item exists and is spawned in this server's data.

  Output: `parity-artifacts/e2e/retail-quest-inventory.json`, plus a short Markdown summary
  by zone and class, with a Python test like the others.

  **Done (2026-09-30).** `tools/client-extract/retail_quest_inventory.py --write` (or `--check`)
  builds `parity-artifacts/e2e/retail-quest-inventory.json` and `.md`; the test is
  `scripts/e2e/test-retail-quest-inventory.py` (its staleness check skips without the client).
  The Markdown summary is by zone and readiness; the class columns arrive with RQ-04.
  - **All 440** no-handler quests are in the client's `quest.xml`; **none** has a Java handler
    at `ce54b7931` (the inventory re-checks `../aion-server`, so the classifier's C#-only view
    holds for Java too).
  - **421** ship a client dialog file. The 19 that do not are Q2010 (Ascension), Q9615 and
    Q9673–Q9684 (Test zone), Q11295 (Inggison), Q12999 (Katalam), Q16989 (Danuar Sanctuary),
    Q18412 and Q28412 (Esoterrace): a strong hint that 4.8 never offered them.
  - **328** are *world ready*: every client npc and item has a template here, and every kill or
    drop source has at least one spawned alternative. Not a class: RQ-03 still has to show retail
    ran them, and a talk-only quest is trivially ready.
  - **53** disagree with `quest_data.xml`: 42 on the quest drop (npc or item), 6 on the finished
    prerequisite, 5 on the collect items. Evidence for class B, not corrections.
  - **327 client quests are absent from `quest_data.xml` altogether** (86 of them with a dialog
    file), so the classifier cannot see them: Reshanta 89, Test zone 54, Sanctum 33,
    Pandaemonium 23, Alabaster Order 24, Field Wardens 24, Blood Crusade 12, Radiant Ops 12 and
    others. They are outside the 440 and are a scope question for the maintainer (RQ-04).
  - The client carries no start or end npc for a quest outside the dialogs, whose second
    encryption layer this repository cannot decode; RQ-03 takes them from aioncodex.
  - `client_npc_names.py` reads the 5.8 server's `Map/XML`, not the 4.8 client. The inventory
    maps devnames through the 4.8 client's own `Npcs.pak` instead.
- [x] **RQ-02 — The upstream check.** List the quests that `upstream/4.8` gained handlers for
  after `ce54b7931`. Those go through the upstream queue, not D32.

  **Done (2026-09-30): none.** `upstream/4.8` was fetched in `../aion-server` and is `267ce6033`,
  18 commits past `ce54b7931` (one past the `a31c1dfcb` the upstream reports checked against).
  - `git diff --diff-filter=ADR ce54b7931 upstream/4.8 -- game-server/data/handlers/quest
    game-server/data/static_data/quest_script_data game-server/data/static_data/quest_data` is
    empty: no handler, template entry or quest data was added, renamed or removed.
  - The only commit touching quest handlers is `4617c9fd2` ("ZoneName & zone templates
    cleanup"), which moves 63 existing handlers from `ZoneName` to `ZoneInstance`. None of the
    440 ids appears anywhere in its diff. It belongs to the ordinary upstream queue, not to D32.
  - So all 440 stay D32 candidates. Repeat this check before each RQ-06 batch; upstream moves.
- [x] **RQ-03 — Retail evidence.** For each quest, the aioncodex `/48/` facts (start and end
  NPC, steps, live or not), cached in the inventory with the URL. Fetch politely: one page at
  a time, cached, never re-fetched once stored.

  **Done (2026-09-30).** `tools/client-extract/retail_quest_evidence.py` fetched all 440 pages,
  one every ~4.5 s. **All 440 have a `/48/` page.** The raw HTML is kept in `run/aioncodex-48/`
  (ignored by git); the parsed facts, each with its URL, are in
  `parity-artifacts/e2e/retail-quest-evidence.json`, which the inventory merges into each row
  as `retail` (title, category, level, quest givers, the task text with its npc and item links,
  prerequisites, reward items). A stored page is never fetched again; `--reparse` rebuilds the
  facts from the local HTML. The summary and the full dialog text are not kept.
  - The quest giver comes from aioncodex; the end npc is the one the task text names last
    (`descriptionLinks`, `retailNpcs`). Missions have no giver: the game grants them.
  - aioncodex `/48/` is built from the 4.8 client, so a page proves the client shipped the
    quest, not that retail offered it. "Live" is therefore judged from the client's own
    retirement marks (below), the task text, and the fandom wiki where those are silent.
- [x] **RQ-04 — Classify and report.** Assign classes A–E, write the report, and **stop for
  the maintainer's review** of classes B, C and E and of the batch order.

  **Done (2026-09-30); approved as proposed by the maintainer the same day.** The report is below;
  every quest's class and reasons are in the inventory rows and in the per-class tables of
  `parity-artifacts/e2e/retail-quest-inventory.md`.
- [x] **RQ-04b — The per-quest review and the work list** (asked by the maintainer with the approval).
  Every A, B and E quest was checked on its own for 4.8, and the result is
  **[retail-quest-worklist.md](retail-quest-worklist.md)**: one line per quest with a Status column
  the maintainer owns (blank, `Rejected`, `Done`), a link to its aioncodex `/48/` page, its batch, what
  it still needs and its 4.8 evidence. `tools/client-extract/retail_quest_worklist.py` builds it and
  keeps the Status marks; `--check` is in the test.
  - **The sources, per quest:** the 4.8 client; aioncodex `/48/`; the **5.8 retail server** (its
    `quest.xml` and `Quest_Simple*.xml`: still live in 5.8 or not, NCSoft's own Korean `dev_name`
    note, and the simple-quest type retail used), cached in
    `parity-artifacts/e2e/retail-quest-crosscheck.json`; the fandom wiki (API; its pages on removed
    maps give their removal version, and 8 quests have pages); and the aioncodex `/48/` npc pages of
    the unspawned givers.
  - **Maps gone in 4.8** (fandom removal pages): Tiamaranta, Sarpan, North and South Katalam
    (Katalam, Danaria), the Idian Depths, Idgel Research Center, Void Cube, Argent Manor (back in
    4.9) and Muada's Trencher. Every other removed map or instance went in 6.0 or later. No A or B
    quest needs an npc that stands only in one of them.
  - **Rejected by the review** (listed in the work list, with reasons):
    - the 14 stub or cutscene entries of class E, as approved; Q2590 by its dialog, a test script;
    - Q2150 and Q2151: server-transfer quests (5.8 note "to the integrated beginner server"; the
      Fast-Track Server brothers), which a single server has no use for;
    - Q39600, Q49600 (Silverine Ltd.) and Q39700, Q49700 (The Merry and Green): Katalam faction
      quests, and Katalam sank in the 4.8 Upheaval;
    - Q1096–Q1099, the Elyos "Hidden Truth" final missions: replaced by Q14030 "Regained Memory" and
      Q14031, as their Asmodian twins were by Q24030 and Q24031 (the client excludes only the
      Asmodian ones);
    - the 23 Tiamaranta quests and Q9572, as before.
  - **Kept by the review:** Q14251 and Q11319 (the two E questions) are live: retail 5.8 still runs
    them (Q14251 as a simple talk quest; Q11319 is the Elyos twin of the 4.8 guide quest Q21320). The
    2.x Reshanta missions stay: nothing in 4.x replaced them (the client has no newer Reshanta
    campaign), the 4.8 client keeps them switched on, and their Java roots Q1701 and Q2701 are
    one-step "report to the Governor" stubs that once led into them.
  - **Classes now:** A **129**, B **63**, C **139**, D **109**, E **0**: 192 to implement.
- [ ] **RQ-05 — The pilot: Altgard's seven.** Those of Q24110, Q24111, Q24113, Q24114,
  Q24115, Q24232 and Q24233 that land in class A. Each needs:
  - a handler;
  - a SIM test that plays it;
  - a deviation row;
  - an upstream patch;
  - an update to the natural leveling doc, whose exclusion list names them. Q24113, Q24232
    and Q24233 follow Q24112, which Leg 4 completes.

  **Progress (RQ-05, 2026-09-30).** Done: Q24113, Q24110; next: Q24111, Q24115, Q24232, Q24233.
  Q24114 is class B (below).
  - **Form.** All six are `quest_script_data` template entries in `altgard.xml`; no C# handler is
    needed. Retail 5.8 runs each as a simple quest (`Quest_SimpleHunt.xml`, `Quest_SimpleTalk.xml`
    with an item check, `Quest_SimpleCollectItem.xml`), and of the Java template quests retail runs
    the same way, SimpleHunt ones are `monster_hunt` (990 of 991) and SimpleTalk-with-item and
    SimpleCollectItem ones `item_collecting`. `quest_data.xml` already carried each quest's kills,
    drops and work item. The default ending (talk to report) is kept: most 4.x SimpleHunt quests in
    Java use it, and the client does not mark the `end_reward` exceptions.
  - **Test.** `RetailQuestPlaysEndToEnd` (`tests/Aion.Simulation.Tests/SimulationRetailQuestTests.cs`)
    plays each quest from its compiled plan (`parity-artifacts/e2e/retail-quest-plans/`) with the
    quest-plan driver: accept at the giver, kill or loot, report, reward. Its cases are the D32
    register `parity-artifacts/e2e/retail-quest-implemented.json`, which also gives each quest its
    fixture account: **42, 43 and 47–50 are D32's** (see there). The probe is ascended first,
    since a non-Daeva is capped at level 9. `test-retail-quest-inventory.py` checks the register,
    that each plan is current, and that each registered quest now has a handler.
  - **Evidence.** All six pass (`run/rq05/rq05-sim-b.log`); with the pre-D32 `altgard.xml`, Q24113
    fails at its start step, because Aurtri offers no quest (`run/rq05/rq05-sim-without-entry.log`).
  - **Records.** Deviations 146–147, the register table below, the patches in
    `docs/upstream-reports/` (cumulative, applied in order), and Done lines in the work list.
  - **Q24114 "You Gotta Stop Umkata" (class B), for approval.** Not a template quest: retail 5.8
    has no simple-quest entry for it. The client steps are: kill 3 Hero Spirits (210588, 210722;
    vars 0-2), report to Gulkalla (203649), collect Umkata's three tokens (182215474-182215476,
    already in `quest_data.xml` as step-4 drops from the Black Claw lycans, Umkata's Jewel Box
    700097 and the wind spirits), use them at Umkata's Grave (700098) to wake **Umkata the
    Restless (210752)**, kill him, report to Gulkalla. What is missing is a custom C# handler and
    Umkata's summon: 210752 has a template but no spawn, because the quest spawns him. Java's own
    2.x version of the same quest, Q2018 "Reconstructing Impetusium" (deleted in Java commit
    `b4b01f75d`, 2016, with the other 2.x campaigns), does exactly this: `CHECK_USER_HAS_QUEST_ITEM`
    at the grave spawns 210752 at (2889.98, 1741.31, 254.75) in 220030000, and his kill ends the
    step. Proposal: write `_24114YouGottaStopUmkata` from that handler, with Q24114's own variable
    layout from the client (kills at 0-2, Umkata at 4), and test it like the others.
  - **Question raised by the same Java commit: the 2.x Reshanta missions.** `b4b01f75d` deleted the
    handlers of Q1071-Q1077 and Q2071-Q2076 together with the retired Altgard, Morheim, Beluslan,
    Verteron, Eltnen and Heiron campaigns, and kept only the one-step Governor quests Q1701 and
    Q2701. So aionemu treated the Reshanta campaign as retired in 4.x too, although the 4.8 client
    still has it switched on and nothing in the client excludes it. The review kept them (batch 5);
    this is new evidence the other way. Nothing is implemented there yet; the maintainer decides.
- [ ] **RQ-06 — Batches.** Zone by zone in the maintainer's order, the classifier regenerated
  after each batch.

## RQ-04 report (2026-09-30)

**Classes:** A **127**, B **73**, C **129**, D **95**, E **16** (440).

The rules live in `retail_quest_inventory.py` (`rule_class`, `classify`). Decisions the rules
cannot make are in `parity-artifacts/e2e/retail-quest-class-overrides.json`, each with its reason;
a row keeps the rules' own verdict beside an override.

### How the classes were decided

- **D** (95): the "Test zone" category (83), `[Test]` or "Data Driven Empty" in a client or
  aioncodex name (11), and Q9572 by review (a level-1 "SimpleTalk function expansion test" beside
  the `[Test]` Q9570 and Q9571). No no-handler quest is an event or level 99 in the client.
  - Q9688–Q9690 (Legion Mission) are "Eliminate High Priest Esras" and so on in
    `quest_data.xml`, but "[Test] Legion Mission Number 1–3" in the client. D on that evidence.
- **C** (129): three kinds of client evidence and one of wiki evidence.
  - **Retired by a newer quest** (64): another client quest requires this one *both* unfinished
    and not acquired, one way, and this one is an old four-digit quest. That is how the 4.x
    campaigns retired the 2.x ones: Q24113 excludes Q2017 and Q2200; Q14010–Q14016 and
    Q14110–Q14114 exclude Verteron's root Q1130.
  - **The rest of the same 2.x campaign** (25): the other four-digit missions of a zone whose
    missions were retired, such as Q2015 in Altgard or Q1014 in Verteron. Their roots (Q1130,
    Q1300, Q1500, Q2200, Q2300, Q2500) are all retired. Reshanta is the exception: its roots
    Q1701 and Q2701 have handlers and nothing retires its missions, so they are not C.
  - **Prerequisite chain disabled in the client** (17): every way in passes a quest the 4.8
    client sets to level 99. These are the Tiamat Stronghold missions Q10070–Q10073 and
    Q20070–Q20073 after Q10064 and Q20064; Tiamaranta's Q14080, Q14081, Q24080 and Q24081 after
    Q14071 and Q24071, and the Q14090, Q14091, Q24090 and Q24091 that follow them; and Q4963,
    which needs the retired Q2099.
  - **Tiamaranta was gone in 4.8** (23, by review): the "Sandstorm" quests Q41600–Q41622 are set
    in Tiamaranta's Land of Fissure (devnames `LDF4b_*`). aion.fandom.com/wiki/Tiamaranta
    (archived) says the map was destroyed and sank in the 4.8 Upheaval and was no longer
    accessible. This server has no Tiamaranta map either.
- **E** (16): the questions below.
- **B** (73): live, but something is missing here. By kind:
  - 41 only have an npc **named in the task text** without a static spawn. That is weak
    evidence: many are spawned by an instance, a siege or another handler (Kromede, Kahrun,
    Pashid, the Hero-quest bosses). Each needs a look, not necessarily data.
  - 7 have a **quest giver** without a spawn: Q2150 and Q2151 in Ishalgen (Rian 801034, Nowlan
    801035), Q18917 (Tribunus Pippus 801026), Q39600 and Q49600 (Silverine), Q39700 and Q49700
    (Merry and Green).
  - 15 have a **kill or drop source** with nothing spawned, such as Q24114's Lycan and elemental
    targets, Q16942 and Q26942's Linkgate bosses, and Q16976 and Q26976's Ophidan boss variants.
  - 12 have a `quest_data.xml` **drop or collect item** that differs from the client: evidence
    for a data correction, never a correction by itself.
- **A** (127): live (a `/48/` page with real task text, a client dialog, not retired), and every
  giver, task-text npc, kill or drop source and item exists and is spawned.

### What I need from the maintainer

1. **Class B (73):** approve per batch which missing pieces to add, or send a quest to E. The
   per-quest reasons, with npc ids and devnames, are in the Class B table of
   `retail-quest-inventory.md`.
2. **Class C (129):** confirm the three client rules and the Tiamaranta decision above. The
   whole list is in the Class C table.
3. **Class E (16), the open questions:**
   - **Stub or internal client entries** (14): the aioncodex task text is empty, "Player", a
     placeholder ("Collect XXX and take them to XXX", "Talk again. XP Test") or "View
     Cutscene": Q1489, Q2010, Q2590, Q3959, Q12999, Q15097, Q16984 (by review, twin of Q26984),
     Q16989, Q18744, Q26984, Q28744; and the "Hidden Quest to Play Cut Scenes" Q11295, Q18412
     and Q28412. Recommendation: out of scope (treat as D) unless you know one was offered.
   - **Replaced, or a branch?** (2): Q14251 (Heiron, "[Instance] The Balaur Headquarters") is
     excluded one way by Q14270 (Draupnir Cave), and Q11319 ("Tour de Cygnea") by
     Q11320–Q11322. Between 4.x quests that can be a choice rather than a retirement.
4. **A race asymmetry:** the Asmodian "Carving out a Fortune" missions Q2096–Q2099 are retired by
   Q24030 and Q24031 (C), but the Elyos "The Hidden Truth" missions Q1096–Q1099 are retired by
   nothing in the client and need only Q1929, so they are B (their Elyos epilogue Q3959 is a
   stub, E). Keep the Elyos chain in scope?
5. **Scope: 327 client quests that `quest_data.xml` does not carry at all** (86 with a dialog
   file). They are outside the 440, and the classifier cannot see them: Reshanta 89, Test zone
   54, Sanctum 33, Pandaemonium 23, Alabaster Order 24, Field Wardens 24, Blood Crusade 12,
   Radiant Ops 12 and others. Should RQ-01 to RQ-04 run on them too, or are they outside D32?
6. **The batch order** (a proposal):
   1. **RQ-05, the Altgard pilot:** Q24110, Q24111, Q24113, Q24115, Q24232 and Q24233 are A.
      **Q24114 is B**: its Lycan (`LycanWarriorS_18/19_Ae`, `LycanHunterS_17_An`) and elemental
      (`AElemental1stD/2ndD_18/19_An`) targets have no template here. Pilot the six and bring
      Q24114 as a B item.
   2. The Asmodian leveling path the natural bot walks next: Morheim (Q24120, Q24240), Beluslan
      (7 A), Brusthonin (Q24200, Q24201).
   3. The Elyos counterparts: Verteron, Eltnen, Heiron, Theobomos.
   4. The instance-entry quests: Fire Temple, Nochsana, Dark Poeta, Draupnir Cave, Theobomos
      Lab, and Beshmundir (Q30250 and Q30350; Q30250 needs Q30041, also A).
   5. The crafting and city quests (Sanctum, Pandaemonium), then the Reshanta missions.
   6. The 4.x level-cap content: Inggison and Gelkmaros, Hero (22 A), Cygnea and Enshar,
      Wisplight and Fatebound Abbey, Ophidan Bridge, Sauro, and the rest.

## How to implement one quest

1. **Read the evidence:** the inventory row, the client dialog map, and the aioncodex page. The
   dialog map's pages (1011, 1352, 1693, 2034, 2375, 2716, …) and buttons (SETPRO*,
   SELECT_QUEST_REWARD, CHECK_USER_HAS_QUEST_ITEM, …) are the step topology. The handler
   should follow them.
2. **Choose the form:**
   - if a `quest_script_data` template fits (`report_to`, `monster_hunt`, `item_collecting`,
     …), add a template entry;
   - otherwise, write a custom handler in `src/Aion.GameServer/Handlers/Quest/<zone>/` in the
     style of the Java handlers around it, using the `AbstractQuestHandler` helpers.

   **Register every NPC, object and item the steps use.** D26–D31 were all quests whose
   handler forgot one.
3. **Data:** the quest is usually already in `quest_data.xml`. Check its drops, collect items
   and start conditions against the client, and correct them only with evidence.
4. **Test:** a SIM test that plays the quest end to end on a probe character, like
   `SimulationQuestCorrectionTests.cs`. Use a free fixture account (1–94, 101–150); the
   natural legs use 133–148.
5. **Record:**
   - a §7 deviation row (the evidence and the test);
   - the D32 register table below;
   - a Java patch in `docs/upstream-reports/`, with a line in its README.
6. **Check:** the whole `CLAUDE.md` build-and-test list. Two checks are likely to move:
   - `check-custom-quest-drafts.ps1` compares `parity-artifacts/e2e/custom-quest-handler-drafts.json`,
     which new handlers change. Regenerate it as a reviewed change.
   - `test-quest-plan-compiler.py` and the classifier output, since a quest leaves `no_handler`.

   `check_fidelity.py` only rejects banned words in new type names (Plan, Policy, Executor
   and the like). Quest handler names like `_24113SwordToSecrecy` pass.

## Rules

- Never create a branch or a worktree; commit on `main`. Never push; the maintainer opens the
  upstream PR.
- Another session shares this checkout (the natural bot's leveling legs). Stage only your own
  files, never `git add -A`, and expect concurrent builds. Never build while a SIM run holds
  the DLLs.
- Never touch the operator's `aion` compose stack.
- One quest, or a small family that only works together, per commit.
- Keep the Java-shipped handlers as they are unless a separate decision says otherwise. D32
  adds missing quests; it does not rewrite existing ones.

## D32 register

| Quest | Name | Zone | Class | Form | Evidence | Test | Deviation | Commit |
|---|---|---|---|---|---|---|---|---|
| Q24113 | Sword to Secrecy | Altgard | A | `item_collecting` template | the 4.8 client ships it (level 12, after Q24112, excluding Q2200 and Q2017); aioncodex `/48/quest/24113` names Aurtri as giver; retail 5.8 runs it as a SimpleTalk quest with an item check, Aurtri to Aurtri | `RetailQuestPlaysEndToEnd(24113)` | 146 | `e73e2d425` |
| Q24110 | Control Altgard, Delete Revolutions | Altgard | A | `monster_hunt` template | the 4.8 client ships it (level 12, excluding Q2200 and Q2012); aioncodex `/48/quest/24110` names Meiyer as giver; retail 5.8 runs it as a SimpleHunt quest, Meiyer to Meiyer, 4 of LehparAsD_9_An and LehparWaD_10_An | `RetailQuestPlaysEndToEnd(24110)` | 147 | (next commit) |
