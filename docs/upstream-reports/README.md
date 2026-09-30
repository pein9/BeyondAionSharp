# Upstream reports for the Java project

Fixes for defects that the Java 4.8 server (`beyond-aion/aion-server`, branch `4.8`) shares
with this port. Each patch is made against the Java repository root and checked with
`git apply --check` (and `--cached`) on the local `../aion-server` checkout at `ce54b7931`, and on an export of `upstream/4.8` `a31c1dfcb`.
Patches are kept with LF line endings (`.gitattributes`), as the Java repository stores them.

| Quest | Defect | Patch | C# decision |
|---|---|---|---|
| 2209 The Scribbler (Altgard) | Borender 203572 is never registered for the talk event; var 1 → 2 is unreachable | `q2209-the-scribbler.patch`, report `q2209-the-scribbler.md` | D26 |
| 2223 A Mythical Monster (Altgard) | Lamir 203620 is never registered for the talk event; var 0 → 1 (which gives item 182203217) is unreachable through the talk path. Masked while Q2231 or Q2224 is startable or active, because Lamir starts both and so opens the quest page. Retail step 1 is "Talk with Lamir" (https://aioncodex.com/enc/quest/2223/?sl=1) | `q2223-a-mythical-monster.patch` | D27, applied (deviation 141) |
| 2916 Man in the Long Black Robe (Pandaemonium) | Annju 204151 is never registered for the talk event; var 2 → 3 (`SETPRO3`) is unreachable. No other quest registers 204151. Retail step 3 is "Interrogate Annju" (https://aioncodex.com/enc/quest/2916/?sl=1) | `q2916-man-in-the-long-black-robe.patch` | D28, applied (deviation 142) |
| 14031 A Hyper-vention (Elyos mission) and 24031 Enemy at the Doorstep (Asmodian mission) | The Large Dimension Teleport Device 730888 and the Shattered one 730898, spawned by the handler in 320040000, are never registered; the mission stays at var 10 after the captain kill | `q14031-a-hyper-vention.patch`, `q24031-enemy-at-the-doorstep.patch` | D29, D30 |
| 2002 Where's Rae? (Ishalgen) | Verdandi 790002 and Hagen 205020 are never registered | `q2002-wheres-rae.patch` | already registered (deviation 33) |
| 18400 The Vanishings (Esoterrace) | Koray 799585, one of the two end NPCs, is never registered; Lanuaga 799584 still ends it | `q18400-the-vanishings.patch` | D31, applied (deviation 145) |
| 2493 Bringing up Tayga (Morheim) | The "Purra?" decoy spots 204436–204438 are never registered; cosmetic | `q2493-bringing-up-tayga.patch` | none |
| 26960 Face the Commander (Iron Wall Warfront) | Copy slip from the Elyos twin 16960: 802055 registered for talk and 802054 for kill, instead of 802054 for talk and 233544 for kill. Untestable: Pashid 233544 has no spawn in 4.8 | `q26960-face-the-commander.patch` | none |
| 2209 (work item) | Thrud does not hand out the quest work item 182203206; cosmetic | `q2209-the-scribbler-work-item.patch` | none |

## D32: retail 4.8 quests Java lacks

Quests the 4.8 client ships and 4.8 retail ran, which Java 4.8 never implemented (`docs/retail-quest-completion.md`). These are additions, not fixes: each patch adds the quest's
`quest_script_data` entry and its summary line. The Altgard patches touch the same file and are
cumulative: **apply them in the order of the table**. Checked in sequence against the
`altgard.xml` blob `b7c967b`, which is the same at `ce54b7931` and at `upstream/4.8` `267ce6033`.

| Quest | Entry | Patch | C# decision |
|---|---|---|---|
| 24113 Sword to Secrecy (Altgard) | `<item_collecting id="24113" start_npc_ids="203654"/>` | `q24113-sword-to-secrecy.patch` | D32, applied (deviation 146) |
| 24110 Control Altgard, Delete Revolutions (Altgard) | `<monster_hunt id="24110" start_npc_ids="203559"/>` | `q24110-control-altgard-delete-revolutions.patch` | D32, applied (deviation 147) |
| 24111 What's Up, Dock? (Altgard) | `<item_collecting id="24111" start_npc_ids="203606" end_npc_ids="203631"/>` | `q24111-whats-up-dock.patch` | D32, applied (deviation 148) |
| 24115 A Shugo Apropos (Altgard) | `<monster_hunt id="24115" start_npc_ids="798033" end_npc_ids="203673"/>` | `q24115-a-shugo-apropos.patch` | D32, applied (deviation 149) |

The Java branch, the checks and the PR text are in `pull-request.md`.
The evidence and the dialog-dispatch explanation that apply to all of them are in
`q2209-the-scribbler.md`, whose appendix lists every handler the scan found.
The scan tool and its output are in `scan/` (`scan_unregistered_talk.py`, read-only; `scan-ce54b7931.txt`).

## Handoff: fix the other quests with this defect (done in C#; PR ready to open)

**Status 2026-09-29.** Q2209 (Borender) is fixed in C# (D26, AF-00, commit `cfdebc04a`),
and its Java patch and report are ready here. Q2223 (Lamir) is fixed in C# (D27, commit
`8a8e54354`). Steps 1–5 are done. The Java branch `fix/unregistered-quest-talk-npcs` is
built in `../aion-server` and described in `pull-request.md`; it is not pushed, and
nothing has been submitted upstream. The approved C# fixes are committed: Q2916 (D28,
`64eaf0b28`), Q14031 (D29, `f5e60dc45`), Q24031 (D30, `390cd18aa`) and Q18400 (D31,
`99ae75a2a`). At the maintainer's request, D28–D31 have no new test. Their `run-fast.ps1`
runs had two failures that also fail without them: `AltgardMoslanCrossroadTalksWithThePlumaAlive`
and `AltgardScriptedQuestsPlayThroughTheContractSteps` ("too far to talk", from the Altgard
Leg 2 work, first seen at `2f6278b08`). The
operator asked for a separate session to fix **the other quests** that have the same
defect, in both directions:
- **Upstream Java:** **one** combined pull request to `beyond-aion/aion-server` (`4.8`)
  that fixes every confirmed quest, Q2209 included. The operator opens the PR.
- **This port (C#):** the same one-line registrations, but only where the operator approves
  each one as a decision in `docs/e2e-player-simulation-plan.md`, like D26 and D27. The
  Java-is-spec rule stands until then.

### The defect in one paragraph

A quest handler's `onDialogEvent` handles an NPC that its `register()` never registers
with `addOnTalkEvent`. A click on that NPC reaches `QuestEngine.onDialog` with questId 0,
which only tries the quests in that NPC's talk list, so the handler never sees the talk
and the step can never be taken. Another quest's quest-selection page can hide the defect
(the client then sends the questId directly), but only while that other quest is startable
or active. The full dispatch explanation, with file and line references, is in
`q2209-the-scribbler.md`.

### Candidates (scan at `ce54b7931`, re-run at upstream `a31c1dfcb`)

**Evidence refresh, 2026-09-29.** `upstream/4.8` is `a31c1dfcb`, 17 commits past the local
`ce54b7931`. The scan, re-run on a `git archive` export of `upstream/4.8` (no worktree, the
Java checkout untouched), prints output byte-identical to `scan/scan-ce54b7931.txt`: nothing
was fixed upstream and nothing new appeared. Upstream touched only two candidates
(`_14031`, `_24031`: the `ZoneName` cleanup in `4617c9fd2`, unrelated). All three patches
still pass `git apply --check` on the `a31c1dfcb` export.

Retail is checked on aioncodex's **4.8** pages, `https://aioncodex.com/48/quest/<id>/?sl=1`.
The `/enc/` pages used before show today's live game, where some quests were rewritten or
removed (11060 is level 99 there; 14031, 24031 and 26960 have no page).

| Handler | Unregistered NPC | Effect | Verdict | Java PR | C# |
|---|---|---|---|---|---|
| `altgard/_2209TheScribbler` | 203572 Borender | var 1 → 2 | **Confirmed** | include (patch ready) | fixed (D26, §7/57) |
| `altgard/_2223AMythicalMonster` | 203620 Lamir | var 0 → 1 (gives 182203217) | **Confirmed** (retail step 1 "Talk with Lamir") | include (patch ready) | fixed (D27, §7/141) |
| `pandaemonium/_2916ManInTheLongBlackRobe` | 204151 Annju | var 2 → 3 (`SETPRO3`) | **Confirmed** (retail step 3 "Interrogate Annju") | include (patch ready) | fixed (D28, §7/142) |
| `bare_truth/_14031AHyperVention` | 730888, 730898 (spawned by the handler in 320040000) | var 10 → 11 → REWARD after the captain kill; the quest cannot finish | **Confirmed** (4.8 retail steps 11 "Operate the Large Dimension Teleport Device" and 12 "Check the Shattered Large Dimension Teleport Device"). Both NPCs are `ai="general"` and no other handler registers them, so a click opens page 1011 | include | fixed (D29, §7/143) |
| `clash_of_destiny/_24031EnemyAtTheDoorstep` | 730888, 730898 | same as 14031 | **Confirmed** (4.8 retail steps 11 and 12, same text) | include | fixed (D30, §7/144) |
| `ishalgen/_2002WheresRae` | 790002 Verdandi, 205020 Hagen | Verdandi's var 2 → 3, 10 → 11, 11 → 12, 12 → 99 (instance) and 13 → 14 steps; Hagen's flight back from Ataxiar | **Confirmed** (4.8 retail steps 3, 5 and 8 "Talk with Verdandi"). Can be masked while Q2008 Ascension, which registers both, is in the quest list and not complete | include | already registered (deviation 33, P7-09) |
| `esoterrace/_18400TheVanishings` | 799585 Koray | the second of two end NPCs; 799584 Lanuaga also completes it | **Confirmed**, cosmetic in effect (4.8 retail step 3: either Sanctum Expedition Inspector ends it). *Correction:* an earlier version of this row said `register()` lists 799584 twice. It does not; the scan's original reading (Koray is simply unregistered) was right | include | fixed (D31, §7/145) |
| `morheim/_2493BringingUpTayga` | 204436–204438 "Purra?" decoys | page 1353 ("not here"); no state change | Cosmetic (4.8 retail: "search every rooftop") | include | no change |
| `iron_wall_warfront/_26960FacetheCommander` | 802054 Lundvarr (registered for **kill**, not talk) | var 0 → 1 unreachable; the Commander Pashid kill (233544) is not registered at all, and `register()` names 802055, the Elyos twin's NPC | **Different defect**: a copy slip from `_16960FacetheCommander` (which registers 802055 talk and 233544 kill). Not reachable in 4.8: Pashid 233544 has no spawn anywhere, so the Elyos twin cannot finish either | include (approved; the var 0 kill quirk it shares with 16960 is left alone) | no change |
| `inggison/_11060TheOrbsOrders` | 218756 Padmarashka (kill-only) | the REWARD branch checks 218756 (the boss) instead of Siaqua 799015, who is registered but never handled | **Different defect** (4.8 retail step 3 "Talk with Siaqua"), but `restricted="true"` | leave out | no change |
| `esoterrace/_18409GroupTiamatsPowerUnleashed` | 205232 Ukon | var 1 → 2 | `restricted="true"` | leave out | no change |
| `reshanta/_1845OpeningDoors` | 204390 Bubu Kong | end branch | `restricted="true"`, and an Asmodian NPC in an Elyos quest | leave out | no change |

The other 50 NO_TALK entries are quest-start NPCs reached through the quest list: they
are expected, not defects.

The C# decisions are D26–D31 in `docs/e2e-player-simulation-plan.md` §6. "include" means
the quest has a commit on the Java branch and a patch here.

### Steps

1. **Refresh the evidence.** `git -C ../aion-server fetch` (read-only for the working tree),
   then re-run `python docs/upstream-reports/scan/scan_unregistered_talk.py <java-root>` on
   the current upstream `4.8` (the local checkout `ce54b7931` is behind upstream `a31c1dfc`).
   Drop anything upstream already fixed, and add anything new.
2. **Settle the "likely" and unreviewed rows.**
   - Read each handler.
   - Check retail on aioncodex, `https://aioncodex.com/enc/quest/<id>/?sl=1`. The fandom
     wiki answered HTTP 402 to the fetcher last time.
   - Record the verdict in the table above.
3. **Java PR.** One commit per quest keeps the review easy, all in one PR. Make each patch
   the way the three here were made (LF endings, `git apply --check` and `--cached` on the
   Java checkout), and write one PR description from these reports.
   - **Ask the operator before making a branch in the Java fork**
     (`origin` https://github.com/rrfarmer/aion-server). The global rule forbids branches
     that are not asked for.
   - Never push without being told to.
   - Include the optional Q2209 work item (Thrud should hand out 182203206) only if the
     operator wants it.
4. **C# fixes, one quest per commit.**
   - **D27 (Q2223) is approved.** It was to go in when the natural bot reached Stop 4. The
     operator's request to fix the other quests covers it now; record that in D27's row.
   - For every other quest, write the proposed decision (D28, D29 and so on) under
     "Blocked / questions" in the e2e plan and ask. Change C# only after a yes.
   - For each approved fix:
     - register the NPC in the C# handler with a `// D<n>:` comment, as `_2209TheScribbler.cs` does;
     - add a SIM regression test like `SimulationQuestCorrectionTests.Q2209BorenderIsRegisteredAndAdvancesTheScribbler`.
       It must fail without the fix (the NPC answers with its default page, 1011) and pass
       with it;
     - add a deviation row to `docs/e2e-player-simulation-plan.md` (deviation 57 is Q2209);
     - add a sentence to the shared-defect paragraph in `CLAUDE.md`.
5. **Checks for every C# commit:**
   - **regenerate `parity-artifacts/e2e/custom-quest-handler-drafts.json`** with
     `dotnet run --project tools/Aion.QuestPlanExtractor -- --repo . --output parity-artifacts/e2e/custom-quest-handler-drafts.json`
     (AF-10 found that AF-00 forgot this);
   - `check-custom-quest-drafts.ps1`, the warning baseline, the null-logger and clock-read
     ratchets, and `scripts/e2e/run-fast.ps1`.

### Traps found on the way

- **SIM test accounts:** the fixture accepts accounts 1–94 and 101–140. Accounts 133–138
  belong to the Altgard AF probes. Many scenarios pick accounts in tuples, loops or
  variables, so a text search for a literal misses them; 139 and 140 are free. To add more,
  widen `SimulationWorldFixture`'s range again. A clash shows up as "Fresh simulation account
  sim-player-N already has a character" in `run-fast.ps1`.
- **A SIM test that adds a quest server-side** must also send `SM_QUEST_ACTION` (ADD) to the
  client, or the bot never sees it (AF-06).
- **Bash heredocs with apostrophes** break in this environment. Write scripts to files
  instead.
- The Java checkout must stay clean: no commits or branches there unless the operator asks.
  Patches are made with `git diff --no-index` on copies of the committed files.

### Done when

- The Java PR's branch or patch set is ready and verified, and the operator has opened it,
  or has it ready to open.
- Every C# fix the operator approved is committed with its regression test, its decision
  row, its deviation row, and green checks.
- This table records every candidate's final verdict.
