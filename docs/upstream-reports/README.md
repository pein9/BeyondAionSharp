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

## TODO: one combined pull request for the Java project (not started)

The operator wants **one** pull request to `beyond-aion/aion-server` (`4.8`) that fixes
every quest with this defect, instead of one per quest. **Another session will prepare it;
nothing has been submitted.** That session should:

1. Fetch the current upstream `4.8` and re-check each handler is still unfixed (the local
   checkout `ce54b7931` is behind upstream `a31c1dfc`).
2. Re-run the scan in the appendix of `q2209-the-scribbler.md` on the current upstream tree
   and decide each candidate:
   - confirmed: 2209, 2223, 2916 (patches here);
   - likely, code only: 14031 and 24031 (quest-spawned teleporters 730888 and 730898; after
     the captain kill, var 10 → 11 → reward is unreachable), which need a retail check;
   - cosmetic: 18400 (alternative end NPC 799585) and 2493 (decoy spots 204436-204438);
   - not actionable: 18409 and 1845 (both `restricted="true"`);
   - unreviewed: the two kill-only registrations, `_11060` (218756) and `_26960` (802054).
3. Optionally include the Q2209 work item (Thrud should give 182203206; see the report).
4. Combine the chosen fixes into one commit or branch in the operator's fork
   (`origin`, https://github.com/rrfarmer/aion-server), with one PR description built from
   these reports. The operator opens the PR.

The C# side does not change with the PR. The C# corrections are covered by D26 and D27, and
only for quests the natural bot plays.
