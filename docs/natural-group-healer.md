# Cleric healing and initial group mechanics

Status: initial group goal completed, 2026-10-12. Group formation, F1 following, healer support, the coin-quest
controller, resurrection waiting and rights-aware round-robin looting are implemented. Groups of two, three,
four, five and six completed one new coin-quest cycle each from the same rollback baseline. Unexercised paths
and the validation limits are recorded below.

**Update, 2026-10-12, after the Group NTC leg ([natural-group-ntc.md](natural-group-ntc.md)).** The same six played
Nochsana Training Camp to the General on this code. Of what is listed below as unexercised, the leg showed the
dispel (40 cleanses in its final round, `n9-3`), urgent and ordinary healing (10 heals, 17 top-ups) and mana
recovery, with no attack sent by the Cleric in any round. Still shown by no run: a resurrection, and common-bind
recovery inside an instance. The leg also changed code this document describes: the group stage plays several
quests and takes its quest NPC from the leader's command; a follower goes around a trail point it cannot step to,
ends a step on a checked point, and walks to a leader that stands still; a member hears its leader's last command
after the leader has left.

This scope replaces the old GH-01–GH-13 checklist and its copied goal prompts. Encounter qualification and the
stop-after-two-proof-attempts rule are withdrawn. There is no pending decision on accepting the earlier solo runs.

The group starts from `ntc-group-s4`: eleven Asmodian second classes at level 26, accounts 111 to 121, in one world
database. [natural-muster.md](natural-muster.md), rules MR-a to MR-f, still governs their gear, supplies, shards and
bag cleanup.

## The operator's clarified goal

2026-10-12:

> The only goal here should have been: We are getting ready to do group instances (like NTC), and our Cleric needs
> to have a "healing only" mode. That means, it constantly watches every group member, dispels and heals, and follows
> the group member 1, and loots when it's their turn (looting might have to come later under different group
> mechanics training).

The operator also clarified: "This is just rules on group healing. This needs no crazy proof work or any solo
fighting."

The earlier decisions still apply: "Heal yourself is the priority"; "Yes, buff"; and "others fight just the same,
healer is just there to make sure we survive thru bosses." Resurrection was initially a low-priority follow-up
to the core healer task; waiting for it is now part of the group-mechanics scope below.

Earlier steering, 2026-10-12: group mechanics are not built yet, so following member 1 can wait. Implement group HP
checking, dispelling and healing now, and keep a HoT on every member when possible by cycling through them while
otherwise idle. "Do what you can, then we will work on Group mechanics. Don't over-do anything."

## Group-mechanics goal

Latest steering, 2026-10-12: form a group with the Templar as leader and the Cleric following while continuing to
heal and dispel. Both start together, bound at Morheim, accept Q2318 "[Coin] Foes of the Fortress" from Nott, and
complete and turn in one cycle. The Templar does the killing. F1 always means the current observed group leader,
including a future human leader; the leader chooses the route and everyone else follows.

Preserve an immutable starting world and restore it for retries and roster expansions. After the two-member
success, add one existing character at a time and repeat through six members. Additional fighters retain their
normal class combat and recovery rules. A fallen member waits for a living Cleric's resurrection; a Cleric death
uses the existing bind-revive and regrouping flow. Add round-robin looting after the first two-member success,
respecting initial ownership, later group access and separate quest-drop eligibility.

Q2318 has kill objectives. It can exercise group quest credit, but its completion does not establish shared or
unshared quest-item behavior. Record unexercised paths without expanding into another quest survey.
The [goal prompt](#goal-prompt) below is retained for repeating this setup and its rollback iterations.

## The mode

When the Cleric is assigned `"role": "healer"`, its existing action loop runs support rules:

1. Continuously observe every group member's HP and debuffs, including its own.
2. Heal itself first when it needs healing, then the member most in need.
3. Remove debuffs that its learned dispel can remove.
4. Cycle Light of Rejuvenation through living group members while otherwise idle, skipping members already covered.
5. Keep Blessing of Guardianship on itself and the group between fights.
6. Never attack, pull, retaliate or cast at an enemy, including when a monster attacks it.

Use the Cleric's learned support skills and the existing cast, cooldown and mana-recovery
machinery. The role and policy should be generic, with the Cleric's support skills as profile data. Only the
Cleric gets a support table in this task. Its ordinary solo mode and the other classes retain their existing rules.

The standalone support driver stays in place. The `group-coin` controller runs its support decisions alongside
`NaturalGroupFollowDriver`, which follows the observed `GroupLeaderId`, including leadership changes, along
ground-checked movement samples. The leader announces stops and waits for lagging or recovering members.
Followers make only short local approaches for NPC dialogue and entitled loot. Fighter followers assist after
observing F1 engage the announced target, using their existing combat and recovery rules.

## Implemented integration

`BotWorldModel` holds group identity, leader and loot rule, plus each member's id, name, map, position,
level, HP and online state. It now also retains MP, class, flight state, instance identity and group effect updates.
Visible effects are available in `EffectsOf`. The bot API already provides group invitations and answers,
movement, targeting and skill casting.

The role uses a separate support catalog in the class profile. The support loop uses the freshest client-observed
health: sighted packets carry HP percentages, while group packets carry exact HP. An older group update does not
overwrite a newer sighted observation. An online member at HP 0 is dead; an offline member's zero vitals are not
treated as a healable target. Observed effect durations age with the session clock for cleanse and HoT decisions.

`NaturalGroupHealDriver.StepAsync` is used by the group controller; `RunAsync` watches continuously when used alone.
The follow driver resolves F1 from group packets rather than roster or scheduling order. It ages moving leader
observations; a visible stopped leader needs no movement heartbeat. Slope samples are snapped to nearby checked
ground so a follower does not stall on an interpolated height.
Buff coverage recognizes an equal or stronger effect in the same shipped stack. In particular, the Cleric does
not repeatedly attempt Blessing of Guardianship over the Chanter's stronger Protectorate's Prayer.

The role is enforced at the **shared action-sending boundary**: no attack packet or enemy-targeted skill may be sent.
Navigation defense, retaliation, reflexes and idle fallbacks must obey the guard. A blocked offensive
action is traced as a mode violation. Under aggro the Cleric continues self-healing and group support; it does not switch
to its fighting table or flee from the group.
NPC target selection for dialogue is permitted. Attacks and enemy-targeted casts remain blocked; the support
catalog also permits learned resurrection and self-targeted Return for group recovery.

### Learned support skills at level 26

| Skill | Cast / cooldown | Role |
|---|---|---|
| Healing Light VI (1843) | 2 s / none | Ordinary single-member heal; 23 m |
| Light of Rejuvenation IV (3942) | Instant / 5 s | 30 s HoT, 39 MP; 23 m |
| Healing Grace II (4204) | 3 s / 6 s | Larger single-member heal; 23 m |
| Flash of Recovery I (3951) | Instant / 30 s | Emergency heal; 23 m |
| Saving Grace (3924, 3925), the Cleric's stigma since 2026-10-13 | Instant / 2 min | On a member at half its health or less, before the other heals: it heals 1,917 once its target is at half (`docs/natural-muster.md`, MR-g) |
| Cleanse III (3937) | Instant / 0.5 s | Remove eligible debuffs; 25 m |
| Blessing of Guardianship (1684) | Instant | Group blessing between fights; 15 m |
| Light of Resurrection (1699) | 6 s / none | Offer resurrection to a fallen member; 15 m; 10% of maximum MP |

Use the highest learned rank for each support role. At level 26, group healing means healing members one at a
time. Healing Wind is learned at 28; adding it to the support catalog can follow when the group reaches that level.
A whole-group heal is not a prerequisite for this mode. Stigmas and skill-book training are outside the task.

## Initial support rules

Make one decision at a time from client observations and trace its reason. Watch the group about every 250 ms
when free to act, respecting existing cast and animation timing. These are starting defaults, not proof metrics:

1. **Emergency healing, at or below 35% HP:** itself first, then the most injured member. Prefer ready Flash of
   Recovery, otherwise a legal cast heal. Self-consumable upkeep can be connected later; this driver uses its support skills.
2. **Other needed healing, at or below 70% HP:** itself first, then the most injured member. Prefer Healing Grace
   when enough HP is missing to use it well; otherwise Healing Light.
3. **Cleanse:** an eligible debuff on itself, then the most injured member with an eligible debuff.
   Skip a debuff with less than 3 s left. Needed healing comes first.
4. **Between fights:** maintain the group blessing, top members up toward 90% HP, and recover its own mana with
   Penance or MP Recovery when needed and legal.
5. **Idle HoT upkeep:** cycle through every living, reachable member, including itself, using the highest learned
   Light of Rejuvenation rank. Skip a member whose HoT still has more than 2 s left. Respect its shared 5 s cooldown
   and retain enough mana for an ordinary direct heal. Higher-priority support can interrupt the cycle; full-group
   uptime is best effort, not a guarantee.
6. **Resurrection:** after needed healing and cleansing, offer learned Light of Resurrection to an eligible fallen
   member within range while the group is quiet. Allow time for the recipient to accept before offering again.
7. **Otherwise:** keep watching; the group controller supplies following between support decisions.

Choose only a support skill whose target, range, line of sight, rank, cooldown and mana make it legal. Derive
Cleanse eligibility from shipped skill data and Java's dispel rules. Record a refused cast and re-observe before
the next decision; never fall back to attacking.

## Core healer implementation checklist

These are parts of **one implementation task**, not separate proof-and-commit rounds:

- [x] Retain the necessary group observations and freshness information.
- [x] Add the Cleric's learned support roles and dispel eligibility to its profile data.
- [x] Integrate the healer role and support rules into the existing driver, including idle HoT cycling.
- [x] Maintain the group blessing and enforce the attack/enemy-cast guard at the API and SIM/LIVE send boundaries.
- [x] Build and perform a brief grouped smoke check; record the exercised paths below.
- [x] Group goal: formation, F1 following, group quest play, resurrection waiting and round-robin loot.

The class profile has a separate `SupportSkills` catalog, leaving its solo fight catalog unchanged. A natural round
can select `"role": "healer"`; the bounded `"stage": "support"` watches the group without playing a quest leg.
Use `stopAfterMinutes` to bound such a check. The smoke-only invitation/answer options reuse existing client group
packets; they are not group-training or following rules.

## Group-mechanics progress

- [x] Verify the shared Morheim location and bind; form the group through invitations with the Templar as leader.
- [x] Integrate F1 following with healer support and normal fighter combat/recovery.
- [x] Complete one new Q2318 cycle with the Templar and Cleric, including both turn-ins.
- [x] Restore the starting world and complete the three-member cycle, adding the Gladiator.
- [x] Restore and complete the four- and five-member cycles, adding Ranger and Sorcerer.
- [x] Restore and complete the six-member cycle, adding Chanter.
- [x] Implement rights-aware round-robin loot; the three-member cycle exercised nine corpse windows.
- [x] Implement resurrection offers, recipient waiting and common-bind recovery when unavailable or the Cleric dies.
  No natural death has exercised that path yet; no death is forced for validation.

The rollback baseline is `ntc-group-s4-w1`, containing all eleven characters. Its SQL dump SHA-256 is
`8d0787cf4021e71e5e314cb060bd081fea316d9fdb09cd8a0ae7c76d88cf8f97`; the initial fingerprint is recorded in
`run/group/baseline.json`. Every attempt restores that snapshot to an owned disposable schema. No new capture
has been made and the source snapshot is retained. The dump and manifest hashes were checked again after the runs
and are unchanged. Every successful run's schema-cleanup record confirms its disposable database was dropped.
Rollback does not discard code changes.

The stage is `"group-coin"`, with `groupLeader`, the same `groupRoster` on each bot, and `"role": "healer"` for
the Cleric. Set `groupLoot: true` for the expanded runs. Local round files are `run/group/two.json` and
`run/group/3.json` through `6.json`. Run with `scripts/sim/run-round.ps1 -From ntc-group-s4 -Seed 1`, and an absolute
`-ReplayRoot` pointing to `run/group`; leave `-Capture` unset. Each member writes `group-coin-completion.json`
only after observing one new completion count and its reward items. The controller checks the existing runtime
problem policy before writing a verified receipt.

| Roster size | Successful run under `run/group/coin` | All members turned in | Game duration | Deaths |
|---|---|---|---|---|
| 2: Templar, Cleric | `two-final2` | Yes, with reward items observed | 9 min 59 s | 0 |
| 3: add Gladiator | `three-5` | Yes, with reward items observed | 11 min 14 s | 0 |
| 4: add Ranger | `four-1` | Yes, with reward items observed | 10 min 58 s | 0 |
| 5: add Sorcerer | `five-1` | Yes, with reward items observed | 10 min 26 s | 0 |
| 6: add Chanter | `six-final` | Yes, with reward items observed | 10 min 34 s | 0 |

Iterations fixed newly observed hazards during travel, slope-height mismatches, stationary-leader following,
NPC dialogue spacing, shared-target completion races, repeated blocked-target approaches and protection-buff
conflicts. Failed attempts remain under the same evidence root. The final pair and final six-member runs use the
final implementation; the intervening roster successes record the incremental expansion.

The final six-member Cleric made **4 blessing and 119 HoT casts**, sent **zero attacks**, and every outgoing spell
targeted its own character or a member of that roster. The final pair likewise sent no attacks. The expanded
successes collected 18, 24, 23 and 22 loot entries respectively, using client-granted rights and client-filtered
item lists. Each member observed two Bronze Coins (186000007) and one Bronze Coin Chest (188050866) at turn-in.
`run/group/completed-runs.json` records the completion, targeting, source and cleanup review.

Existing API, world-model, social/profile, movement, cast-protocol and group-packet regressions: **115 passed**.
The required Fast run `group-mechanics-commit` also passed: **11 manifest scenarios**, with **115 tests passed**
and 96 optional probes skipped; zero failures. Its artifacts are under `run/group-mechanics-commit`.
No new unit tests were added. The final solution build and warning baseline passed. Build servers were shut down
after the checks; no group SIM run remains active. The implementation and this document use the existing checkout
on `main`; no changes were pushed.

No successful roster run had a death. Resurrection and common-bind group recovery are implemented but were not
exercised naturally. An earlier six-member iteration exercised two direct heals and mana recovery; the final
six-member run needed only blessing and HoT upkeep. No eligible dispel or shared/unshared quest-item drop was
exercised. Q2318 completion establishes kill credit, not quest-item sharing; those paths remain for ordinary group
play without a new proof hunt. Initial round-robin corpse rights and collection were exercised; there was no
mandatory leftover-item or four-minute free-for-all demonstration.

To repeat from the original world with six members, use a fresh run id:

```powershell
pwsh -NoProfile -File scripts/sim/run-round.ps1 -Round run/group/6.json -Item coin -Run <fresh-run-id> -From ntc-group-s4 -Seed 1 -ReplayRoot C:/Users/ryanf/Documents/GitHub/BeyondAionSharp/run/group
```

Use `run/group/two.json` for the original pair or `3.json` through `5.json` for intermediate rosters. The harness
restores the immutable starting world, so quest completion and loot from the previous attempt do not carry over.

## Verification and working rules

- Use the existing checkout on `main`; no branches, worktrees, subagents or pushes.
- Read corresponding Java first (`../aion-server`, branch `4.8`) and preserve the repository's parity rules.
  Make decisions from client observations, not live server state.
- Build and use existing checks required by repository guidance for the final code change. Do not repeat a full
  bundle for every support rule or add new unit tests for this bot work.
- The completed core healer task used **one brief grouped SIM smoke check**, seed 1, from `ntc-group-s4`, without a
  capture. Check legal support targeting, blessing, HoT cycling, any healing/cleansing exercised, and the absence
  of attacks or enemy casts. Fighters may fight; the healer may not. Report unexercised paths plainly and exercise
  them during group play. The new goal uses quest runs with progressively larger rosters as specified below.
- No hard-fight survey, solo comparison, boss qualification, hour-long session, survival benchmark, forced death
  or mandatory trace score is required. Deaths and refused casts are outcomes, not new qualification projects.
- Keep the read-only monitor available when the harness supports it. Consult `scripts/sim/sim-run-marker.ps1`;
  do not build or run other checks while a SIM run holds the outputs. Shut down build servers after a chain.
- Keep every group character in one world. No LIVE run, operator-stack modification, snapshot overwrite,
  generated-data edit or warning-baseline increase.
- Leave `docs/playtest-aethertapping-plan.md`, `docs/playtest-crafting-alchemy-cooking-plan.md` and
  `docs/playtest-ground-essencetapping-plan.md` alone.

Formation, F1 following, assisting the leader's target, loot turns and group death handling belong to the
implemented group goal. Instance progression, specialized pull/taunt coordination, other classes' support tables and
healing players outside the group remain outside this task.

### Brief validation (2026-10-12)

Solution build and warning baseline passed. Existing focused API, world-model, social-packet and profile
regression checks: **71 passed**.
Round `idle-hot1`, under `run/gh/healer-mode`, used the Cleric and Templar from `ntc-group-s4`, seed 1, for one game
minute. Both stopped at their configured duration; neither fought, and no snapshot was captured.

The Cleric cast one Blessing of Guardianship and five Light of Rejuvenation IV casts, alternating HoTs between
itself and the Templar and refreshing them near expiry. It sent **zero attacks**; all six casts targeted self or
the grouped Templar. Both were healthy and had no eligible debuff, so urgent healing and cleansing were not exercised
in this smoke check. Their rules are implemented; exercise them during later group play instead of adding a proof hunt.

## Java references already checked

Paths are relative to `../aion-server/game-server/src/com/aionemu/gameserver/`, branch `4.8`:

| Fact | Reference |
|---|---|
| Group member packets carry exact HP/MP, flight points, position, class and effects. Effects updates replace the specified slot mask. | `network/aion/serverpackets/SM_GROUP_MEMBER_INFO.java` |
| HP/MP updates use 500 ms; movement uses 2 s; nonpassive effect changes send immediate group effects updates. | `model/stats/container/PlayerLifeStats.java`; `taskmanager/tasks/TeamStatUpdater.java`; `TeamMoveUpdater.java`; `controllers/effect/PlayerEffectController.java` |
| Sighted HP packets carry a percentage. SM_ATTACK_STATUS types 19–23 carry MP and must not be mistaken for HP or deaths. | `model/stats/container/CreatureLifeStats.java`; `network/aion/serverpackets/SM_ATTACK_STATUS.java`; `SM_ATTACK.java`; `SM_CASTSPELL_RESULT.java` |
| Indirect effects add hate to nearby creatures already hating the caster or recipient; healing can draw aggro. | `skillengine/model/Effect.java:622` |
| Cleanse III permits up to 100 eligible DEBUFF-slot effects in physical/mental/ALL categories at dispel level 1, with power 30 per effect. Higher resistance is reduced; STUN is a separate category. | `skillengine/effect/AbstractDispelEffect.java`; `DispelDebuffEffect.java`; `controllers/effect/EffectController.java:528`; Java skill data, skill 3937 |

For later group mechanics, shared quest drops are assigned separately from ordinary round-robin loot
(`services/QuestService.java:666`). Resurrection offers use SM_RESURRECT and CM_REVIVE / SKILL_REVIVE id 3
(`skillengine/effect/ResurrectEffect.java`; `services/player/PlayerReviveService.java:42`). Neither flow gates this mode.

## Earlier work retained as history

Local commits `3dd8f2b41` and `d5b03687b` added the `gh-survey` diagnostic and recorded two uncaptured Templar runs
under `run/gh/GH-01`. Zugog caused one death after a lowest living HP of 19.86%, with a consumed potion and refused
Anti-Shock; Seku was killed with no death and a lowest HP of 74.65%. The accompanying repository checks passed.
Both schemas were dropped and no snapshot changed. This evidence remains available; it is not an active checklist,
does not require another survey, and does not gate healing-mode implementation.

## Goal prompt

```text
Implement and iterate on basic group mechanics using the Morheim repeatable coin quest.

Repository: C:\Users\ryanf\Documents\GitHub\BeyondAionSharp, existing checkout on main.
Read CLAUDE.md, docs/natural-muster.md and docs/natural-group-healer.md.
This replaces the old GH proof program. Reuse the implemented Cleric healer mode.

STARTING WORLD AND ROLLBACK

Use the one-world snapshot ntc-group-s4. Restore it into a throwaway SIM schema for every attempt.
Keep the original snapshot immutable. All characters belong to the same world database.

Verify the starting snapshot and record its identity before running. If preparation requires a new
baseline, capture it under a fresh name before accepting the quest, retaining the original.
Every retry and roster expansion starts from that same baseline, including character positions,
binds, inventories and quest states. Rollback restores world state; code improvements remain.

FIRST RUN: TEMPLAR AND CLERIC

1. Log in Asimtemplar (account 112) and Asimnjour, the Cleric (account 111).
2. Verify both are together at Morheim Ice Fortress and bound to the same Morheim obelisk.
   Correct discrepancies through ordinary movement and binding interactions.
3. Have the Templar invite the Cleric through client group packets. Accept the invitation and
   verify both observe the group with the Templar as leader.
4. Travel together to Nott (204360). Both accept Q2318, "[Coin] Foes of the Fortress."
   Use the existing repeatable quest route and interactions.
5. The Templar leads travel and does the killing using its existing combat and recovery rules.
   The Cleric follows and continuously supports the group: self-healing first, group healing,
   eligible dispels, blessing and idle HoT upkeep. It never attacks or casts offensive skills.
6. Keep hunting until BOTH characters have the required quest progress. Return together;
   each character turns in the quest and receives its own reward.

F1 AND FOLLOWING

F1 always means the current group leader, resolved from observed group packets.
Do not hard-code the Templar as the permanent leader or infer leadership from bot scheduling.
The same follower behavior must work later when F1 is a human player.

Only F1 chooses the travel route and hunt destination. Followers track F1's observed movement
and follow its traversed path with a small spacing allowance. Use existing collision and movement
checks; do not cut through walls or run separate quest routes.

Keep followers close enough for healing and quest credit. Stop movement when a cast requires it,
then resume following. Let the bot leader pause for regrouping, recovery or a fallen member.
If leader observations are missing or stale, stop safely and wait to reacquire F1.

EXPAND AFTER SUCCESS

After the two-member quest succeeds, restore the baseline and add another existing character.
Repeat with three, four, five and finally six members. Additional classes are your choice.
Keep the Templar as F1 for these runs and the Cleric in healer/follow mode.

Other members follow F1 and assist its observed combat target using their existing class combat,
resting, recovery and healing rules. They do not independently choose pulls or travel routes.
Every participating member must accept, progress and turn in the quest.

DEATH AND RESURRECTION

A dead member waits for the living Cleric to resurrect it through normal skill and offer/answer
packets. Prioritize keeping living members alive before resurrection.
If the Cleric dies, use the existing bind-revive and soul-healing flow, then regroup.
Handle an unavailable resurrection with a bounded, reported fallback rather than waiting forever.
Do not force deaths just to demonstrate this path.

LOOTING: AFTER THE FIRST TWO-MEMBER SUCCESS

Add round-robin looting without delaying the initial formation/following work.
Read the corresponding Java group, drop and quest implementations first.

Respect the designated initial looter and subsequent group access as the server actually allows.
Followers loot only when entitled, then resume following. Treat personal or shared quest drops
separately from ordinary corpse ownership. Track each member's journal and inventory so nobody
is left without required quest items.

Q2318 uses kill objectives, so completing it does not establish shared/unshared quest-item behavior.
Report any loot or quest-drop paths this run does not exercise; do not claim they passed or expand
into an unrelated quest survey.

WORKING RULES AND COMPLETION

Keep changes small and reuse existing drivers. Make gameplay decisions from client observations.
Read relevant Java first; fix encountered server defects under the repository's parity rules.

SIM only, seed 1, one world. No LIVE changes, branches, worktrees, subagents, pushes, snapshot
overwrites, generated-data edits or warning-baseline increases. Preserve existing local changes.
Leave the three playtest gathering/crafting documents untouched.

Keep the read-only monitor available. Never build while a SIM run holds the outputs.
Run checks appropriate to the changes; do not add new bot unit tests or repeat broad proof bundles.

Finish when the two-member and progressively expanded groups through six members complete one
quest cycle each from the preserved baseline, with F1-led travel and the Cleric remaining support-only.
Record concise results: roster, quest completion per member, following problems, healing/dispels,
deaths, loot behavior and unexercised paths. Leave the baseline available for another rollback.
```
