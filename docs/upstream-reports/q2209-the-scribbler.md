# Q2209 "The Scribbler": Borender (203572) is never registered for the talk event

Branch `4.8`. The handler is byte-identical at `ce54b7931` and at the current upstream `4.8`
head (`a31c1dfc`, checked 2026-09-29).

## Symptom

Q2209 (Asmodian, Altgard, level 10) sends the player to Tulberg, then Borender, then Noroia,
then back to Thrud. After Tulberg (var 0 → 1), talking to **Borender** (203572, on the ice
rock above Altgard Fortress) does not advance the quest. It stays at var 1, so Noroia (who
expects var 2) and Thrud (who expects var 3) cannot continue it either.

## Root cause

`game-server/data/handlers/quest/altgard/_2209TheScribbler.java`

- `register()`, lines 22-27, registers talk events for 203555 (Thrud), 203562 (Tulberg) and
  203592 (Noroia) only.
- `onDialogEvent`, lines 54-62, handles `case 203572:` (Borender): at var 1, `QUEST_SELECT`
  shows page 1693 and `SETPRO2` calls `defaultCloseDialog(env, 1, 2)`. **This is the only code
  that moves var 1 → 2.**

How a click on an NPC reaches a quest handler:

1. `ai/handler/TalkEventHandler.java:26` calls `QuestEngine.onDialog` with questId 0 and
   `USE_OBJECT`.
2. `questEngine/QuestEngine.java:165-175`: with questId 0, the engine only tries the quest IDs
   in `getQuestNpc(npcId).getOnTalkEvent()`. That list is filled only by
   `registerQuestNpc(npcId).addOnTalkEvent(questId)`. `addOnQuestStart` fills a separate list
   (`QuestNpc.java:39-43` compared with `70-74`). 203572 is registered only by
   `_24011FunnyFloatingFungus.java:22,26-27`, so 2209 is never tried.
3. Then `model/DialogPage.java:113-125,186-192` chooses the first page. Page 10 (quest
   selection) appears only if the NPC has a function dialog, or if one of the player's
   uncompleted quests is in the NPC's talk list. Otherwise the player gets page 1011, the
   NPC's default chat.

No other code registers 203572 for 2209. `quest_script_data/altgard.xml:30` lists 2209 as
`[SCRIPT]`, with no XML script. `quest_data.xml:10481` holds only rewards and a work item.
No other handler mentions 2209.

The other steps are registered correctly:

| Step | NPC | Handler | Registered |
|---|---|---|---|
| start | Thrud 203555 | l.37-41 | l.23 (start), l.24 (talk) |
| var 0 → 1 | Tulberg 203562 | l.45-53 | l.25 |
| **var 1 → 2** | **Borender 203572** | **l.54-62** | **missing** |
| var 2 → 3 | Noroia 203592 | l.63-71 | l.26 |
| var 3 → REWARD, end | Thrud 203555 | l.72-86 | l.24 |

**Caveat: the stall can be masked.** A client `CM_DIALOG_SELECT` that already carries
`questId = 2209` goes directly to the handler, with no registration check
(`CM_DIALOG_SELECT.java:122` → `DialogService.java:282-291` → `QuestEngine.java:155-158`).
Borender is in Q24011's talk list, and `DialogPage.hasQuestInteraction` counts every non-COMPLETE
state, including a LOCKED campaign mission. So while Q24011 is not yet completed, Borender opens
page 10. If the client then lists Q2209 there, clicking it reaches the handler. We did not verify
whether the 4.8 client does this. Once Q24011 is completed, Borender always opens page 1011 and the
quest cannot advance. The fix makes the step independent of Q24011.

## Retail expectation

- Aion Codex, quest 2209: https://aioncodex.com/enc/quest/2209/?sl=1
  - Steps: "Talk with Tulberg. Talk with Borender. Talk with Noroia. Talk with Thrud."
  - Thrud's text names all three NPCs (Tulberg, Borender and Noroia) and gives the player the
    book to show them.
  - Linked NPCs are 203555, 203562, 203572 and 203592. The base reward is 9,780 XP, which
    matches `quest_data.xml`.
- The Aion wiki gives the same order and says Borender is on the ice above the Abyss Gate over
  the fortress: https://aion.fandom.com/wiki/The_Scribbler (the page returned HTTP 402 to our
  fetcher; we used the search-engine summary of it).

**Related difference (not included in the patch):** in retail, Thrud gives the player the book,
"Treasure House of Knowledge" (182203206). `quest_data.xml:10483-10485` declares it as the
quest's `quest_work_item`. The handler starts the quest with `sendQuestStartDialog(env)`
(l.41), which gives no item. `QuestService.finishQuest` already removes work items
(`QuestService.java:107`). No step checks for the book, so this is cosmetic. If you want it,
the one-line change is `return sendQuestStartDialog(env, workItems.getFirst());`, the same idiom
as `_2664AnAntidotetotheLepharists.java:42`.

## Fix

Register Borender for Q2209's talk event (`q2209-the-scribbler.patch`):

```diff
 		qe.registerQuestNpc(203562).addOnTalkEvent(questId);
+		qe.registerQuestNpc(203572).addOnTalkEvent(questId);
 		qe.registerQuestNpc(203592).addOnTalkEvent(questId);
```

This does not affect Q24011. Both handlers return `false` for dialogs that belong to the other
quest, so the order of the talk list does not matter.

Apply from the repo root with `git apply q2209-the-scribbler.patch`. `git apply --check` passes
against `4.8`, both for the working tree and with `--cached`.

## How to verify

1. Log in as an Asmodian of level 10 or higher. Complete Q24011, or use a character that has no
   Q24011 state, so the masking path above does not apply.
2. Accept Q2209 from Thrud (203555) and talk to Tulberg (203562): var becomes 1.
3. Before the fix: Borender (203572) shows only his default chat, and the var stays 1. After the
   fix: page 1693 appears, and `SETPRO2` sets var 2.
4. Talk to Noroia (203592): var becomes 3. Thrud gives the reward, and the quest completes.

## Upstream status (checked 2026-09-29)

- `beyond-aion/aion-server` default branch `4.8` (head `a31c1dfc`): the handler is unchanged and
  still has the defect:
  https://github.com/beyond-aion/aion-server/blob/4.8/game-server/data/handlers/quest/altgard/_2209TheScribbler.java#L22-L27
- The last commit touching the file is `9270bb2f72` "simplify folder structure" (2020-12-01).
  Branch `4.7.5` (`game-server/data/scripts/system/handlers/quest/altgard/_2209TheScribbler.java`)
  has the same `register()`, so the omission is older than the 4.8 rework.
- GitHub search of issues and PRs for "2209", "Scribbler" and "Borender": 0 results. Commit
  search for "2209" and "Scribbler": 0 results.

---

## Appendix: scan for the same pattern (supporting evidence)

We scanned `game-server/data/handlers/quest/**/*.java`. The script collects NPC ids (6 digits,
or int constants) used as `case <id>:` or `targetId == <id>` inside `onDialogEvent`. It then
compares them with the ids that the same handler's `register()` passes to
`registerQuestNpc(...)`: literals, constants, and `for (int id : array)` loops over int arrays.
There were 948 handlers with NPC ids in `onDialogEvent`, and every `register()` was resolved.

- **UNREGISTERED** (the id appears in no `registerQuestNpc` call): 10 handlers, listed below.
- **Registered only for a non-talk event:** 52 handlers. In 50 of them the id is registered
  only with `addOnQuestStart`. Those start NPCs are reached through the quest list (a dialog
  with questId ≠ 0), so they are expected, not defects. The other two are kill-only: `_11060`
  (218756) and `_26960` (802054). We did not review those two.

Classification of the 10 UNREGISTERED handlers. "Confirmed" means both the code and retail show
the step; we did not test any of them at runtime.

| Handler | Unregistered id | Effect | Status |
|---|---|---|---|
| `altgard/_2209TheScribbler` | 203572 Borender | var 1 → 2 unreachable | **Confirmed** (this report) |
| `pandaemonium/_2916ManInTheLongBlackRobe` | 204151 Annju | var 2 → 3 (`SETPRO3`) unreachable. Retail step 3 is "Interrogate Annju" ([codex](https://aioncodex.com/enc/quest/2916/?sl=1)). No other quest registers 204151. | **Confirmed**, same pattern |
| `altgard/_2223AMythicalMonster` | 203620 Lamir | var 0 → 1 (`SETPRO1`, gives 182203217) unreachable through the talk path. Retail step 1 is "Talk with Lamir" ([codex](https://aioncodex.com/enc/quest/2223/?sl=1)). | **Confirmed**, but masked while Q2231 is startable or active: Lamir is Q2231's start NPC, so page 10 appears |
| `ishalgen/_2002WheresRae` | 790002 Verdandi, 205020 Hagen | Branches at l.90 and l.174 unreachable | **Confirmed** from the code. A C# port has already registered both ids to make these branches reachable. |
| `bare_truth/_14031AHyperVention` | 730888, 730898 (quest-spawned teleporters) | var 10 → 11 → REWARD unreachable after the captain kill (l.122-139) | Likely (code only, not checked against retail) |
| `clash_of_destiny/_24031EnemyAtTheDoorstep` | 730888, 730898 | Same as 14031 (l.115-131) | Likely (code only) |
| `esoterrace/_18400TheVanishings` | 799585 Koray | Alternative end NPC. 799584 is registered and completes the quest. | Cosmetic |
| `morheim/_2493BringingUpTayga` | 204436-204438 | Decoy "Purra?" spots; they only show page 1353 and change no state | Cosmetic |
| `esoterrace/_18409GroupTiamatsPowerUnleashed` | 205232 Ukon | var 1 → 2 unreachable | Not actionable: the quest is `restricted="true"` |
| `reshanta/_1845OpeningDoors` | 204390 Bubu Kong | End branch | False positive / dead code: the quest is `restricted="true"`, and 204390 is an Asmodian Morheim NPC in an Elyos quest |
