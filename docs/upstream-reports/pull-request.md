# Pull request for `beyond-aion/aion-server` (`4.8`)

Branch `fix/unregistered-quest-talk-npcs` in the local `../aion-server` checkout, built on
`upstream/4.8` `a31c1dfcb`, one commit per quest. It is not pushed. To open the PR, push it to
the fork (`origin`, https://github.com/rrfarmer/aion-server) and open it against
`beyond-aion/aion-server` `4.8`. The same changes are in this folder as one `.patch` per
commit. Each patch passes `git apply --check`, with and without `--cached`, on `ce54b7931` and
on `a31c1dfcb`.

Suggested title and description:

---

**Fix quest handlers that never register an NPC they handle**

Several quest handlers handle an NPC in `onDialogEvent` but never register it with
`addOnTalkEvent`. When a player clicks an NPC, `TalkEventHandler` calls
`QuestEngine.onDialog` with questId 0. With questId 0, the engine only tries the quests in
that NPC's talk list, so these handlers never see the click and the step cannot be taken.
`DialogPage` then shows page 1011 (the NPC's default chat). The quest-selection page (10)
appears only if the NPC has a function dialog, or if another quest registers it and is
startable or active. That can hide the defect for a while.

Each commit adds the missing registration.

| Quest | NPC | Effect before the fix | Retail (4.8) |
|---|---|---|---|
| 2209 The Scribbler | Borender 203572 | var 1 → 2 unreachable once Q24011 is completed | [steps](https://aioncodex.com/48/quest/2209/?sl=1): Tulberg, Borender, Noroia, Thrud |
| 2223 A Mythical Monster | Lamir 203620 | var 0 → 1 (gives 182203217) unreachable unless Q2231 or Q2224, the quests Lamir starts, is startable or active | [step 1](https://aioncodex.com/48/quest/2223/?sl=1) "Talk with Lamir" |
| 2916 Man in the Long Black Robe | Annju 204151 | var 2 → 3 unreachable; no other quest registers him | [step 3](https://aioncodex.com/48/quest/2916/?sl=1) "Interrogate Annju" |
| 14031 A Hyper-vention | teleport devices 730888, 730898 | the mission stays at var 10 after the captain kill (it cannot be abandoned) | [steps 11–12](https://aioncodex.com/48/quest/14031/?sl=1) "Operate the Large Dimension Teleport Device", "Check the Shattered…" |
| 24031 Enemy at the Doorstep | 730888, 730898 | same as 14031 | [steps 11–12](https://aioncodex.com/48/quest/24031/?sl=1) |
| 2002 Where's Rae? | Verdandi 790002, Hagen 205020 | Verdandi's steps and Hagen's flight back unreachable unless Q2008, which registers both, is in the quest list | [steps 3, 5, 8](https://aioncodex.com/48/quest/2002/?sl=1) "Talk with Verdandi" |
| 18400 The Vanishings | Koray 799585 | Koray, one of the two end NPCs, cannot end the quest; Lanuaga (799584) still can | [step 3](https://aioncodex.com/48/quest/18400/?sl=1): either inspector ends it |
| 2493 Bringing up Tayga | "Purra?" spots 204436–204438 | the "not here" page (1353) never appears; no state change | [quest](https://aioncodex.com/48/quest/2493/?sl=1) |
| 26960 Face the Commander | Lundvarr 802054, Commander Pashid 233544 | copied from the Elyos twin 16960: registered 802055 (Demades) for talk and 802054 for kill, while it handles Lundvarr's dialog and Pashid's kill | [steps](https://aioncodex.com/48/quest/26960/?sl=1): Lundvarr, then Pashid |

One more commit, for quest 2209, has Thrud hand out the quest's work item (182203206, "Treasure
House of Knowledge"). `quest_data.xml` declares it, and retail gives it at the start. It is
cosmetic, because no step checks for it, so it can be dropped.

Not verified at runtime:

- **26960.** Commander Pashid (233544) has no spawn in 4.8, so the Elyos twin cannot finish
  either. Both twins also expect the kill at var 0, after the talk has already set var 1.
  This PR leaves that unchanged.
- **The others.** Only the registration changes. BeyondAionSharp, a C# port of this server,
  carries the same registrations for 2209, 2223, 2916, 14031, 24031, 2002 and 18400. It
  verified 2209 and 2223 with simulation tests: before the fix the NPC answers with page 1011;
  after it, page 10, then the quest page, then the next var.

Found with a scan of every handler's `onDialogEvent` NPC ids against its `register()`. The
other scan hits are quest-start NPCs reached through the quest list, or `restricted` quests
(1845, 11060, 18409).

---
