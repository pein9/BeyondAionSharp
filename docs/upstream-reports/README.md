# Upstream reports for the Java project

Fixes for defects that the Java 4.8 server (`beyond-aion/aion-server`, branch `4.8`) shares
with this port. Each patch is made against the Java repository root and checked with
`git apply --check` (and `--cached`) on the local `../aion-server` checkout at `ce54b7931`.
Patches are kept with LF line endings (`.gitattributes`), as the Java repository stores them.

| Quest | Defect | Patch | C# decision |
|---|---|---|---|
| 2209 The Scribbler (Altgard) | Borender 203572 is never registered for the talk event; var 1 → 2 is unreachable | `q2209-the-scribbler.patch`, report `q2209-the-scribbler.md` | D26 |
| 2223 A Mythical Monster (Altgard) | Lamir 203620 is never registered for the talk event; var 0 → 1 (which gives item 182203217) is unreachable through the talk path. Masked while Q2231 is startable or active, because Lamir is Q2231's start NPC and so opens the quest page. Retail step 1 is "Talk with Lamir" (https://aioncodex.com/enc/quest/2223/?sl=1) | `q2223-a-mythical-monster.patch` | D27 |
| 2916 Man in the Long Black Robe (Pandaemonium) | Annju 204151 is never registered for the talk event; var 2 → 3 (`SETPRO3`) is unreachable. No other quest registers 204151. Retail step 3 is "Interrogate Annju" (https://aioncodex.com/enc/quest/2916/?sl=1) | `q2916-man-in-the-long-black-robe.patch` | none (not on the bot's route) |

The evidence and the dialog-dispatch explanation that apply to all three are in
`q2209-the-scribbler.md`, whose appendix lists every handler the scan found.
The scan tool and its output are in `scan/` (`scan_unregistered_talk.py`, read-only; `scan-ce54b7931.txt`).

## Handoff: fix the other quests with this defect (next session, not started)

**Status 2026-09-29.** Q2209 (Borender) is fixed in C# (D26, AF-00, commit `cfdebc04a`),
and its Java patch and report are ready here. Nothing has been submitted upstream. The
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

### Candidates (from `scan/scan-ce54b7931.txt`)

| Handler | Unregistered NPC | Effect | Status | Next step |
|---|---|---|---|---|
| `altgard/_2209TheScribbler` | 203572 Borender | var 1 → 2 | **Confirmed**, fixed in C# (D26) | patch ready |
| `altgard/_2223AMythicalMonster` | 203620 Lamir | var 0 → 1 (gives 182203217) | **Confirmed** (aioncodex step 1 "Talk with Lamir"); C# fix approved (**D27**) | patch ready; apply D27 in C# |
| `pandaemonium/_2916ManInTheLongBlackRobe` | 204151 Annju | var 2 → 3 (`SETPRO3`) | **Confirmed** (aioncodex step 3 "Interrogate Annju") | patch ready; C# needs a decision |
| `bare_truth/_14031AHyperVention` | 730888, 730898 (quest-spawned teleporters) | var 10 → 11 → reward after the captain kill | Likely, from the code only | check retail, then patch |
| `clash_of_destiny/_24031EnemyAtTheDoorstep` | 730888, 730898 | same as 14031 | Likely, from the code only | check retail, then patch |
| `esoterrace/_18400TheVanishings` | 799585 Koray | an alternative end NPC (799584 completes it) | Cosmetic | decide whether to include |
| `morheim/_2493BringingUpTayga` | 204436–204438 | decoy "Purra?" spots, no state change | Cosmetic | decide whether to include |
| `ishalgen/_2002WheresRae` | 790002, 205020 | two unreachable branches | Code only; the C# port already registers both (deviation 33) | include in the Java PR |
| `esoterrace/_18409GroupTiamatsPowerUnleashed` | 205232 | var 1 → 2 | `restricted="true"` | not actionable |
| `reshanta/_1845OpeningDoors` | 204390 | end branch | `restricted`, and an Asmodian NPC in an Elyos quest | not actionable |
| `inggison/_11060`, `iron_wall_warfront/_26960` | 218756, 802054 | registered only for kills | Unreviewed | review |

The other 50 NO_TALK entries are quest-start NPCs reached through the quest list: they
are expected, not defects.

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
