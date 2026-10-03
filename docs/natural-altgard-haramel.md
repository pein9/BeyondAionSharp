# Leg 12: Haramel after the coin gear preparation

**Status (2026-10-03): CG-00..CG-06 and HM-00..HM-08 complete in SIM. The combined goal stops here.** Follow
[coin gear preparation CG-00..CG-06](natural-altgard-coin-gear.md) first, then do the Haramel lead-in,
every active Asmodian Haramel quest once, and the clears needed to finish them. Haramel remains
**Leg 12**; the preparation is a separate `cg` section. Recommended scope is **two fresh solo clears**,
with additional ordinary recovery visits only if an objective remains. Natural SIM, snapshot restore
and endpoint relog are verified. Continuous full-area SIM and isolated LIVE acceptance remain later work.

## Authority, start and finish

Use the parent loop in [natural-ascension-altgard.md](natural-ascension-altgard.md#how-to-work-this-list-loop-protocol)
with **HM** in place of NA. Orient with `CLAUDE.md`, the leveling doc's decisions, ND and CG evidence,
this section, git status and any running SIM. Implement one ready item, read its Java first, verify
once, record outcomes, run seven pre-commit checks and Fast for gameplay changes, tick with dated
evidence and commit on `main`. Stop when HM-08 is done or no item is unblocked. Never push, branch,
use a worktree, touch the operator `aion` stack or use natural-character GM except OD-13 help.

Start from **`altgard-coingear`**, character 133297, at least level 24, 145 completed journals,
Q2293 completed once, **19 Iron Coins**, coin chain gloves/shoulders/legs equipped alongside the
retained Altgard Legionary chest/boots, and **staff 101501357 still equipped in both hands**.
Retain the old cloth gloves and existing helmet/accessories; no coin weapon or shield was bought.
Bound at Heart obelisk 700067. CG-05 captured it from committed
`3afda849b006d02d0014b182e9a22e1b50cc7d5b`, UTC `2026-10-03T15:37:22.7117265Z`,
elapsed 67,800,001 ms, dump SHA256
`38e263f3fb9e95d5f500cce13ef7117e36401bc177098deba7991661085e953f`.
Owned SQL/hash and actual restore/relog pass (`run/cg05-support/audit.json`); no surviving owned
schemas. Preserve all eleven earlier legs and their completions.

Finish alive in Altgard, normally rebound at **fortress 700065**, all eleven active quests below
complete, all earlier journal entries preserved (**156 distinct completions** with recommended CG),
quest work/collection items consumed as Java specifies, actual coin and gear ledger retained across
relog. Snapshot **`altgard-haramel-l12`** (operator choice 2026-10-03; preserves Leg 1's `altgard-l12`). The three coin armour pieces must have been bought/equipped before
entry; during Haramel allow genuinely better owned class-compatible chain/accessory quest rewards
or loot, record armour changes and retain the purchased pieces/old cloth gloves. **Keep staff
101501357 equipped throughout, including the endpoint; do not auto-equip a looted/reward weapon.**
No extra shopping, coin weapon or shield purchase is authorized.
Keep Q2900's bundle 188053787 sealed, no temporary tutorial stigma, and no invented permanent stigma
skills. Regular automatic skill learning is allowed; stigma skills require their actual acquired and
socketed stones, and are outside this goal. Do not redo Q24014-Q24016: Leg 10 already completed them.

## Entrance, instance lifetime and navigation

- **Map 300200000**, solo, minimum level 16, **no maximum level in the shipped 4.8 data**. A level-24
  Cleric can enter. No prerequisite quest is on the entrance portal; do the lead-in for quest coverage.
- Asmodian secret passage **730319** is at (2909.833, 1456.150, 252.593), close to Shezen **804605**
  (2892.2, 1485.3, 249.24) on lower Impetusium ground. Use the ordinary portal; it sends the player
  to (172, 20, 144.22548) in a registered solo instance. Preserve map **and instance** identity.
- Shipped entry limit is **16 entries per daily cycle**, reset **09:00 server time**. `ent_cool_time=900`
  is HHMM for `DAILY`, not seconds/minutes of cooldown. Read Java `InstanceCooltimeData` and
  `PortalCooldownList`; do not add a fictional 15-minute lockout or reset entry counters.
- An empty solo instance ordinarily expires after **600 seconds**, checked by the instance cleanup
  task. Until it expires the entrance may return the character to the same cleared instance.
  After clear one, leave normally, hand in/accept the outdoor quests, wait through the actual empty
  expiry and confirm a fresh spawn set on re-entry. Waiting must be an explicit bounded decision
  with a retained deadline and normal game-clock advancement in SIM. Do not administratively destroy
  the instance, relog to force a reset, or reset recovery budgets. Observe actual entry count/deadline.
- Exit **730320** is at the entry; after Hamerun, spawned **700852** provides a second ordinary exit.
  Both lead to Altgard (2907.624, 1464.1887, 252.59264). Learned Return goes to the Heart while bound
  there. Prove death/relog/re-entry against the actual existing or expired instance; preserve quest
  counters/items and entry use, and record lost work or recovery visits honestly.
- HM-02 generates Haramel's mesh/Asmodian graph from the shipped `.geo`, walk mask and zone/spawn
  data; never hand-edit generated navigation. Keep the refinery/high entrance, tower, lower
  processing/plantation and high boss office separate. Static ground alone cannot connect every floor.
- Tower lift **730321** has a `portal_dialog` path, dialog **10000**, to (220, 213, 126.68472)
  in the same map. Read `PortalDialogAI`/portal data and observe the normal transition/acknowledgement.
  It does not directly put the character at the rusted box (z139.82). Prove the remaining tower path.
- The tower return is a checked **jump/glide** onto its lower connecting ramp, followed by ordinary
  ground movement. The office uses a different **automatic wooden elevator behind Gestanerk**.
  Its shipped 4.8 mission/CGA floor controller has a twenty-second cycle: board during the 1.7-second
  bottom stop, ride the real ascent, and leave during the upper stop with a checked hop across the
  small landing gap. Retain missed boarding windows and wait for the next cycle. SIM uses ordinary
  CM_MOVE samples from the extracted controller; rendered-client phase/vehicle metadata is later
  LIVE acceptance. Never invent a vehicle ID from the client entity ID or grant server flight here.
- The outdoor exit is east of the Heart FLY zone. Walk to the approved lower pillar launch before
  ascending; land on actual static ground at the approved upper point before walking to the hub
  transporter. After bind revival, wait for observed soul sickness to expire when its speed penalty
  would exhaust the flight reserve.
- Bind stays at the **Heart throughout both clears**, with ordinary Heart recovery. Take hub flight
  transporters for fortress/Heart trips; walk only the local ground/object/portal routes. After the
  final Morn hand-in at the fortress, bind at 700065 and check it across relog.

## Quest scope and dependencies

All listed quests are shipped, level-16 Asmodian IMPORTANT quests with Java handlers/template
registrations. Include their **first completion**. Q28504 and Q28509 permit five repetitions, but
five completions of each are outside this run; the first of each covers their mechanics. **Q28502**
is restricted, level 99 and unused; exclude it. Elyos Q18500-Q18511 are outside this character's scope.

| Quest | Prerequisite | Start -> hand-in | Required action |
|---|---|---|---|
| Q28500 Odella, Odella, Where Art Thou? | none | Morn 203560 -> Moorilerk 799522 | Gulkalla 203649; piece 730306; pile 730307; movie 217; enter Haramel and claim reward |
| Q28501 The Price of Loyalty | none | Moorilerk 799522 -> Moofrenerk 799523 | 5 x182212013 from 700833 and 5 x182212014 from 700951 |
| Q28508 Securing Moofrenerk's Retreat | none | Moorilerk -> Moofrenerk | 2 x216898, 3 x216901, 3 x216902; independent counters |
| Q28503 Odella for Shybanz | none | Moofrenerk -> Shezen 804605 outside | 5 x182212016 from 700834 |
| Q28506 Guess Who | Q28501 complete | Moofrenerk -> Gestanerk 799524 | report to Gestanerk in lower plantation |
| Q28509 What is Inside the Box? | none | Moofrenerk -> Gestanerk | Rusty Key + 3 Lubricating Oils; open tower box 700853 for 1 x182212020 |
| Q28507 Hammertime | Q28506 complete | Gestanerk -> Shezen outside | kill Hamerun 216922 |
| Q28504 Follow the Leaders--and Kill 'em | **Q28507 complete** | Shezen -> Shezen | **65** qualifying monsters; use the full shipped ID set |
| Q28505 Overseers Under Attack | **Q28507 complete** | Shezen -> Shezen | 1 each x182212017/18/19 from 216897/216907/216915 |
| Q28510 Destroy the Haramel Facilities | **Q28507 complete** | Shezen -> Morn outside | start gives 182212021; kill 3 carts 700950, then use 3 processed odella 700953 |
| Q28511 The Soup? Nutsy! | **Q28507 complete** | Moorilerk -> Chagarinerk 798031 outside | 5 ginseng 182212022 from 700954, cauldron 730359, receive soup 182212023 and hand in |

The three custom Java scripts are `_28500OdellaOdellaWhereArtThou`,
`_28510DestroytheHaramelFacilities` and `_28511TheSoupNutsy`. The other eight use
`MonsterHunt`, `ItemCollecting` or `ReportTo`; read their registrations and generic handlers too.

**Important packed counters:** Q28504's 65 cannot fit one six-bit quest counter. Java `MonsterHunt`
packs it across consecutive variables: count = var0 + (var1 << 6) here. Prove 63 -> 64 -> 65,
reward gating, relog persistence and normal target selection using full observed quest state.
Q2293's separate 6/16 counters and Q28508's 2/3/3 counters must never be flattened into one kill total.

Q28500's pile dialogue starts movie 217; the genuine movie-end event changes var 3 to REWARD.
Entering the sensory zone can play the movie again while at that step. Do not pretend that a zone
entry or a displayed movie alone completed it. Q28510's carts are killed, while processed odella are
used in order after the three kills. Q28511 checks/consumes the five ginseng at the cauldron and then
gives the soup, whose item event moves the quest to reward. These are custom protocols, not gathering:
none of the container/ginseng/odium steps require raising Essencetapping to 85.

The rusted tower box uses Java `ChestAI`, rather than the collection objects' `QuestItemNpcAI`.
Its `chest_templates.xml` requires **both** one Rusty Key **185000103** from Keymaster Mumu Dang
**217025** and three Lubricating Oils **185000107** from the three Mumu Mechanics **217108**.
The matching Java/C# Haramel global-drop rules supply those items from real kills/loot (base chance
100, no level-based reduction in these rules). An incomplete set receives monologue 1111301 and
consumes nothing; a successful opening consumes both sets before registering the quest drop.
Acquire them on the lower tower grounds before the lift/stairs detour. Do not grant keys or mistake
an alive chest's refusal for a random quest-item drop miss. Retain any actual drop misses and use
ordinary recovery if supplies remain missing.

Fixed rewards for the eleven first completions total **958,587 quest XP**, plus ordinary kill XP,
and **7 Bronze Coins** (3 from Q28504, 1 from Q28509, 3 from Q28511). These are distinct from Iron
Coins; do not spend/convert them during this goal. Final level can rise naturally; do not pin it to 24.

Recommended selectable rewards are **112501641** (Q28500's level-21 chain spaulders with
concentration and reduced hate), **123001440** (Q28507's magical-accuracy cloth band, an accessory),
and **113501720** (Q28505's level-21 chain chausses with concentration and reduced hate).
The "cloth band" name refers to the belt slot, not cloth body armour. The matching higher-hate
chain alternatives are not a more expensive/stronger quality tier. Q28505 also lists historical
class weapon rewards, but HM-00 confirms `use_class_reward` is absent/default 0 in matching Java/C#
data. Java `QuestTemplate` enables that list only at 1 or 2 and `QuestService` therefore uses the
ordinary selectable armour list. Q28500/Q28507 dialogue is proved in HM-03; verify Q28505 in HM-05
and choose the genuinely offered reward.
If a Lateni weapon is actually offered, it may be retained, but the operator's equipped-staff
instruction still applies. Do not grant an inactive XML class reward, presume a second selectable
prize or switch the current staff for it.

## Recommended itinerary

1. From the coin snapshot, fly **Heart -> fortress**, accept Q28500 from Morn, then fly
   **fortress -> Heart**. Speak to Gulkalla at the upper hub. Descend normally to the discarded
   piece at (2789.07, 1581.89, 248.858), then pile at (2877.55, 1534.87, 250.333). Finish movie 217,
   approach Shezen/the passage and enter alone. Keep the Heart bind.
2. **Clear one: entrance and Odium Refinery.** Claim Q28500 from Moorilerk; select a Cleric chain
   reward. Accept Q28501 and Q28508 before killing their targets. Loot all five odium objects and
   five cart objects, count the 2/3/3 required monsters separately, then kill Drudgelord Kakiti 216897
   normally and loot. The three destructible 700950 carts belong to the later Q28510; avoid spending
   time killing those for quest progress while that quest is unavailable.
3. **Clear one: processing/plantation, tower detour, office.** At Moofrenerk
   (228.416, 160.876, 137.059), hand in Q28501/Q28508 and accept Q28503/Q28509 and now-available
   Q28506. Clear MuMu Ham the Grey 216907 in processing, collect the five odella objects 700834
   in plantation, and visit Gestanerk (289.776, 422.453, 89.5563). Hand in Q28506 and accept Q28507
   **before** Hamerun. On the lower tower grounds, kill/loot Keymaster Mumu Dang and three Mumu
   Mechanics for the complete key/oil set. Walk to Tower Lift 730321, take dialog 10000, and walk
   the remaining stairs to rusted box 700853 at (231.031, 223.091, 139.82). Loot it for Q28509,
   jump/glide down to the connecting ramp and return to Gestanerk to hand it in. Board the automatic
   wooden elevator behind him, ride to the upper office and hop onto its checked landing. Clear
   Bossman Nukiti 216915 as needed before approaching Hamerun's raised stage.
4. **Clear one: Hamerun and outside unlocks.** Kill Hamerun the Bleeder **216922**, handle movie
   **457**, observe and loot the **Cleric chest 700832** and use spawned exit 700852. Java's instance
   handler selects the chest by class; it is not interchangeable with warrior/scout/mage chests.
   Hand in Q28503 and Q28507 to Shezen. Only now accept Q28504/Q28505/Q28510. Wait outside for
   natural empty-instance expiry while preserving deadlines, journals and the Heart bind.
5. **Clear two: all newly unlocked tasks.** Confirm a fresh instance on entry. Accept Q28511
   from Moorilerk before passing its sources. Kill/loot all three overseers for Q28505, destroy the
   **three** 700950 carts in the refinery, and collect Q28504's **65** qualifying kills throughout
   the dungeon. Static data supplies **74 qualifying spawn spots**, so a complete sweep should cover
   65 in one fresh clear; prove which are reachable/killable and individually creditable. Do not
   count non-target carts, chests, corpses or repeated selections as kills.
6. In processing, after Q28510's cart counter reaches three, use each of its three 700953 objects.
   Loot five ginseng 700954, use cauldron 730359 with the actual item-check/receive protocol and
   observe Q28511 REWARD. Finish the 65-kill sweep, including the office/fresh Hamerun if needed;
   clear Hamerun a second time and loot the class chest, then exit normally. Prefer the natural
   lift/tower detour when recovering a missed source over resetting the instance.
7. Hand Q28504/Q28505 in to Shezen, then take the hub flight **Heart -> fortress** after the
   normal ascent. Complete Q28510 at Morn and Q28511 at Chagarinerk. Bind at fortress 700065,
   audit all eleven completions, spent/remaining currencies, items, equipment and skills, then relog.

The second instance is required for the post-Q28507 overseer drops/carts even if the first instance
still exists. Do not assume normal monsters or objects will respawn in a cleared instance. If the
74-spawn audit reveals fewer than 65 attainable credits, or a death loses a source, record the outcome
and take another ordinary fresh visit within real entry/recovery limits. No failed timed quest, missed
window, death or escort loss becomes a test failure merely because it occurred; an uncompleted
objective, unsafe route or exhausted recovery still prevents claiming the endpoint complete.

## TODO list

- [x] **HM-00 — Freeze scope and eligibility.** Depends: CG-06. Audit the verified coin snapshot,
  eleven quest gates/handlers/items/rewards, level/solo/entry-cycle restrictions, the current server
  configuration, ordinary instance lifetime, exits/recovery, sealed stigma state and full quest vars.
  Read relevant Java and the parent decisions; record any authority gap before changing shared content.
  - 2026-10-03 evidence: `run/hm00-audit/audit.json` on base `91be42c07` independently verifies the
    incoming dump hash, Cleric client ID 10 / SQL CLERIC, level 24, 145 journals, equipped staff,
    sealed bundle and no existing Haramel journal or stored entry row. Java `ce54b7931` and C# XML
    agree on all eleven active quest nodes, eight registrations, gates, items/rewards, portal/lift/
    exit paths, restrictions, selected item definitions and all 74 qualifying spawn spots.
    The four post-Q28507 gates, Q28501 -> Q28506 -> Q28507, separate Q28508 counters and packed
    Q28504 var0 + (var1 << 6) are frozen. First rewards total 958,587 XP and seven Bronze Coins.
  - Read/compare the three custom handlers, template and reward services, quest vars, Haramel
    instance handler, portal/entry-count/expiry services and actual configuration. Normal same-instance
    re-entry does not spend a new entry; fresh entry does. DAILY/900 means 09:00, 16 entries, solo,
    minimum 16/no maximum. Haramel is non-personal; empty expiry uses 600 seconds and a 60-second
    checker. SIM copies the shipped instance configuration without changing those values; scaling
    is disabled. The lift still needs the upper stairs, and normal Hamerun death produces movie 457,
    Cleric chest 700832 and exit 700852. Those paths/credits/combat remain runtime proof in HM-02..HM-06.
  - Q28505's class-weapon list is inactive/default 0; zero-based selectable indexes are Q28500=3
    (112501641), Q28507=1 (123001440) and Q28505=3 (113501720). Q28500 declares work item 182212012
    but its handler gives none; do not invent a grant. Q28510 supplies 182212021 and removes it on
    completion; Q28511 checks/consumes five ginseng, gives soup and removes its work item at hand-in.
    No authority gap or server/content correction is established. The initial audit draft compared
    the numeric client class with a string; corrected against Java ID 10, original log retained.
    Seven prechecks pass (`run/hm00-audit/`); documentation/audit only, no natural instance run.
- [x] **HM-01 — Leg 12 contract, plans and decisions.** Depends: HM-00. Add `l12` to every shared
  registry/runner/snapshot selector; compile the eight template quests and encode the three custom
  protocols, two visits, fresh-instance wait, packed 65 counter, quest reward choices, protected items
  and final bind/ledger assertions. Explicit checkpoints must resume after a lost connection without
  duplicate accept/reward/purchase. Keep original recovery/time budgets across visits.
  - 2026-10-03: shared `l12` selectors, eight compiled plans, three custom protocols and two-visit
    decisions are frozen in `natural-altgard-l12-contract.json`. Q28501/Q28505 require every item
    set; Q28504 reads consecutive six-bit vars through 63/64/65 without wire flags. Reward choices,
    the equipped original staff, retained armour/accessories/cloth gloves, sealed bundle, 19 Iron,
    seven endpoint Bronze, 156 journals and final fortress bind are pinned.
  - Read Java custom/template handlers, `SM_INSTANCE_INFO`, `SM_CHANNEL_INFO`, `PortalService`,
    `PortalCooldownList` and `IDFactory` first. The channel packet carries instance ID minus one;
    NPC IDs can recycle. Receipts therefore retain actual copy/anchor IDs, entry use/reset deadline
    and fresh live-spawn evidence. Re-entry, daily reset and cold lost-copy handling preserve the
    original sixty-minute stall/twenty-revive budgets and ordinary expiry deadline. Q28511's
    successful five-ginseng payment is retained separately from soup receipt; new login state
    selects remaining work without duplicate accept/reward/purchase. Full belt slot 65536 is retained.
  - Fourteen Haramel contract cases plus earlier-leg/persistence/packet-fixture regressions pass
    (81 total); all seven prechecks pass. `hm01-fast-a2`: 78 passed, four existing skips, all eleven
    scenarios passed. Earlier compile/fixture/identity drafts and passing pre-refinement Fast are
    retained (`run/hm01-support/audit.json`). Runtime travel/quest/combat/recovery proof remains
    HM-02..HM-06; no server content or snapshot change. Base `a39b80dc0`.
- [x] **HM-02 — Navigation and travel probe.** Depends: HM-01. Generate/rebake Haramel mesh/graph,
  prove all outdoor objects, portal entry, every floor/quest source/boss, tower box, actual lift,
  both exits, hub flights/binds and Heart recovery. Use unused access-0 probe accounts, clear
  aggressive neighbours for setup and `BeginWorldReload` before setup teleports. Label setup paths;
  ordinary travel evidence must use real packets. Run the affected-map NavBake check.
  - 2026-10-03: Haramel's generated/rebaked mesh has 9,834 polygons; its Asmodian graph has 32
    nodes, 132 links and both static exits. Java portal/dialog/lift, movement/glide, instance and
    revive services were read first. The shipped 4.8 mission/CGA supplies the separate office
    elevator's twenty-second cycle and floor; regeneration is identical. Static navigation's
    tower/office gaps use checked ordinary glide/carry/hop packets, without server content changes.
  - `hm02-fast-a3` proves 62 checked ground legs, all 26 collection-source spots, actual tower lift
    and stairs/box, one tower glide, one office elevator, both exits, two hub flights, three pillar
    flights, same-copy re-entry without extra entry use, Heart revival and alive fortress bind.
    Account 223 is an unused access-0 probe. Class/level/Kinah/staff, 109 aggressive-neighbour
    clears, one controlled death and HP=1 Hamerun solely to expose travel endpoints are labelled
    setup; normal combat remains HM-04. No natural character or quest was modified.
  - Forty-six focused cases, affected-map rebake and all seven fresh prechecks pass; Fast passes
    79 tests/four existing skips and all eleven scenarios (`run/hm02-support/audit.json`, base
    `d2c3331c8`). Route failures and both earlier Fast drafts remain retained. The temporary glide
    log allowance/history experiment was reverted after reporter rejection; final CM_MOVE(GLIDE)
    enters actual glide state directly, omitting Java's unhandled optional animation emotion.
    Recovery waits for observed soul sickness; boarding misses remain recorded outcomes (zero in
    the final run). Owned schemas are gone, incoming snapshot hash unchanged. Rendered elevator
    phase/vehicle metadata still require later LIVE acceptance; no invented vehicle ID.
- [x] **HM-03 — Lead-in and first-clear quest protocols in SIM.** Depends: HM-02. Play Q28500's
  dialogue/movie/reward; Q28501/Q28508 sources and independent counters; Q28503/Q28509 loot;
  Q28506 -> Q28507 hand-ins. Prove all required start gates and selected Cleric rewards, including
  quest-item cleanup. No administrative completion of the natural character.
  - 2026-10-03: `hm03-quests-a5`, unused access-0 account 224, proves movie 217's separate
    acknowledgement, seven first completions, eight real start-gate refusals, three incomplete
    reward refusals, independent 2/3/3 counters, both collection sets, chain shoulders 112501641,
    belt 123001440, one Bronze Coin and exact item cleanup through a normal quit/login/world enter.
    Post-Q28507 accepts retain their work item and same-copy re-entry spends no extra entry.
  - Read Java custom/templates/start/reward gates, ChestAI, actual chest/global-drop data and
    default level-reduction semantics first. The rusted chest refuses twice without consuming
    incomplete supplies; actual Keymaster/Mechanic kills and loot supply one Rusty Key plus three
    Lubricating Oils, consumed only by successful opening. Sixteen actual source uses and twelve
    labelled HP=1 protocol kills are recorded; normal HP/AI remains HM-04. Probe-only level/class/
    staff, 54 aggressive clears and seven source replacements at shipped spots never modify the
    natural character or directly complete a quest. New shared helpers encode only Q28500's
    timed-object and movie protocol; no server/content or log-policy changes.
  - Forty-four focused regressions and all seven prechecks pass. `hm03-fast-a2` passes 80 tests,
    four existing skips and all eleven scenarios (`run/hm03-support/audit.json`, base `d630d8512`).
    Keep all probe drafts and original Fast: HM-03 passed there, but an earlier escort trace write
    failed with Windows insufficient system resources. Idle build servers were shut down and
    reuse disabled only in the new verification shell; the failed result remains failed. Both
    incoming/Leg 1 dump hashes remain unchanged and owned schemas are gone. The operator resolved
    the endpoint name to `altgard-haramel-l12`; HM-07 applies the capture-selector change.
- [x] **HM-04 — Normal combat and class loot in SIM.** Depends: HM-03. Level/gear/regular-skill
  appropriate fights against Kakiti, MuMu Ham, Nukiti, Hamerun and their neighbouring pulls; actual
  HP/AI, ordinary healing/retreat/death/revive. Prove movie 457, Cleric chest 700832, real loot and
  spawned exit. Record loot misses as outcomes; do not make random equipment drops a pass requirement.
  - 2026-10-03: `hm04-combat-a3`, unused access-0 account 225, proves four bosses at their shipped
    HP (2,030/2,138/2,223/2,223), six original neighbours and a native two-attacker pull. Two
    brainwashed workers really ship at 2 HP in Java/C#; their values were retained. Ordinary learned
    Cleric skills produce ten kills, four Rejuvenation choices and four confirmed approved potion
    debits, with zero combat deaths/retreats. Hamerun's two actual adds, movie 457, Cleric chest
    700832, every offered loot index, spawned exit 700852 and quit/login persistence are observed.
    Original staff/mask 3, sealed bundle and no unacquired stigma skill persist.
  - Read Java AI/aggro, HaramelInstance, DropService and shared skill-cooldown semantics first,
    retaining the existing retail AI exception. Class/level/loadout, aggressive clears and setup
    moves belong only to the disposable probe; BeginWorldReload precedes setup teleports. No
    HP/damage/server/content or natural-character writes. Diagnostic fights use separate combat
    budgets; natural shared-budget recovery remains HM-05/HM-06. Random equipment is not required.
  - All seven fresh prechecks and Fast pass (`hm04-fast`: 81 passed, four existing skips, eleven
    scenarios; `run/hm04-support/audit.json`, base `fc8a36791`). Initial single-attacker coverage
    and the failed native-tag draft remain: that draft attempted a higher Smite rank before the
    shared lower-rank cooldown expired. The probe now observes the actual cooldown before ordinary
    combat. Owned schemas are gone and the incoming snapshot hash is unchanged.
- [x] **HM-05 — Fresh-clear and post-boss protocols/recovery in SIM.** Depends: HM-04. Prove
  ordinary exit/empty expiry/fresh entry, Q28504 count 63/64/65 and the attainable 74-spawn set,
  Q28505's three overseer drops, Q28510 kill/use ordering and Q28511 ginseng/cauldron/soup. Include
  death, same-instance re-entry and saved-state/cold-restart recovery, actual entry count, lost-instance
  fallback and preserved counters/items. Allow another genuine fresh visit if evidence requires it.
  - *Done 2026-10-03.* Unused access-0 account 226 in `hm05-matrix-a9` credits all 74
    original qualifying spawns without replacements, preserves the packed 63/64/65 boundaries
    across real relogs, and completes Q28504/Q28505/Q28510/Q28511. The preliminary copy expires
    normally; same-copy re-entry spends no extra entry, fresh entry spends one. There are 78 native
    kills including the preliminary boss and three carts, 79 retained combat attempts, zero combat
    deaths and one controlled death with native Heart recovery. Java kill/use ordering, all three
    overseer drops, the actual chain-pants reward, six Bronze Coins and quest-item cleanup pass.
  - `hm05-cold-a2` uses actual processes 56452 and 20328. The inside-instance quit retains a
    partial packed count, overseer drop, cart work item and paid five-ginseng receipt without soup.
    Login after process loss uses the configured Altgard exit, takes a genuine fresh entry, retains
    the original revive/stall budget and receives/delivers soup without paying twice. Both owned
    cold schemas are dropped; numeric copy IDs can be reused and are not asserted to differ.
  - Java `SM_CHANNEL_INFO` can send its unspawned default before teleport spawn. This corrects
    HM-01/HM-02's earlier channel-based identity assumption: the bot observes the actual copy from
    `SM_PLAYER_SPAWN.worldChannel`, including personal-map sign and same-copy reload semantics.
    The server wire behavior is unchanged. All 34 focused world-model/Haramel contract cases and
    all seven current prechecks pass. The original eight matrix failures and original Fast failure
    remain retained. Refreshed `hm05-fast-a2` passes 82 tests/five explicit switch skips/all eleven
    scenarios, including the complete recovery matrix and corrected old travel probe. The final
    audit passes (`run/hm05-support/audit.json`, base `3df3643d5`): owned schemas are gone and both
    the CG incoming dump and preserved Leg 1 dump are unchanged. No natural character or snapshot
    writes; labelled probe setup clears and BeginWorldReload remain explicit. Normal shared-budget
    natural recovery remains HM-06; the probe's saved stall fingerprint is synthetic and retained.
- [x] **HM-06 — Natural Leg 12 runner and one smoke.** Depends: HM-05. Restore `altgard-coingear`
  with `sim-snapshot.ps1 -Action Restore`, run
  `NaturalIshalgenPriestCompletesFrozenJourneyWithoutSetup` with `AF_ALTGARD=l12` and the variables
  from `Invoke-NaturalJourney`. Prove both clears and all eleven first completions, 156 completed
  journals, ordinary transitions/loot/hand-ins and fortress bind through relog. Announce the read-only
  http://127.0.0.1:17880/ monitor; retain failures/outcomes and drop only the owned schema in finally.
  - *Done 2026-10-03.* `hm06-smoke-a22` passes from the untouched CG snapshot on base `83b715930`:
    two actual fresh copies (entry count 1→2), ordinary empty expiry, both bosses/movies/class
    chests, all eleven first completions and 156 journals. The level-25 Cleric finishes alive and
    bound at fortress 700065 through actual quit/login, with zero deaths, 19 Iron/7 Bronze Coins,
    the original staff equipped in both hands, incoming armour retained, sealed bundle and no
    unacquired stigma skill. No gear/book purchase or natural setup occurs; only OD-13 supplies.
  - Ordinary checked movement covers the tower lift/glide, wooden elevator ascent/descent and
    pillar ascent before the final hub flight. Three hub flights and 129 distinct native zero-HP
    monster objects are observed. Six unreachable cage firing approaches are retained outcomes;
    other shipped sources finish the actual packed 65-kill objective. Boss and handler-spawned
    chest observations remain scoped to the current entry's real packets.
  - Java automatically starts Q2945 at level 25; its zero counter/completion count remains
    deferred. Login omits completed entries from the active journal, so the stall fingerprint
    uses the completed journal consistently. Relog advances observation time while preserving
    the original progress time/fingerprint, revive budget, visits and soup payment. All 69 scoped
    world-model, Haramel, elevator, checkpoint and persistence cases pass.
  - All seven prechecks and `hm06-fast-a1` pass (82 tests/five explicit switch skips/all eleven
    scenarios). The audit passes (`run/hm06-support/audit.json`). Original failed/interrupted
    attempts a1..a21 are retained, including two earlier one-death recovery outcomes and the
    endpoint assertion drafts. Every owned smoke schema is dropped; incoming CG and preserved
    Leg 1 dump hashes are unchanged. No server code/content or snapshot was changed. HM-07
    captures the endpoint only after committing the runner and capture support.
- [x] **HM-07 — Committed-code Leg 12 snapshot.** Depends: HM-06. Commit capture-support changes
  before capture, then capture `altgard-haramel-l12` from `altgard-coingear` through `l12`. Update the
  shared endpoint contract/runner selectors to the resolved distinct name before capture. Record its actual
  capture SHA, metadata/hash, coin/quest/equipment/skill/bind ledger and completed count, restore and
  prove actual endpoint relog, then drop owned schemas. Preserve both earlier snapshots unedited.
  - *Capture support verified 2026-10-03, snapshot pending.* Shared l12 validation/endpoint now use
    `altgard-haramel-l12`. Capture keeps `haramel-progress.json` and its SHA256; restore checks
    both dump and receipt hashes before creating a schema and supplies the l12 selector/original
    receipt. Endpoint Verify reads the l12 completion proof. Historical snapshots keep their
    existing selectors. Missing/edited receipts, historical restore/time and ownership contracts
    pass, as do 22 Haramel cases, seven prechecks and `hm07-fast` (82 passed/five switch skips/
    eleven scenarios). The fixture log-cleanup draft and overlapping-build DLL/warning failures
    remain retained; sequential reruns pass (`run/hm07-support`). Commit this support before capture.
  - *Done 2026-10-03.* `hm07-capture` produces **`altgard-haramel-l12`** from committed
    `aea03588164bbf38f00b7b8a208d5e7623c7ca45`, UTC `2026-10-03T23:05:54.2740790Z`, elapsed
    **73,584,031 ms**. Dump SHA256
    `804c2a34b6c1b679fabf2539609602f261e74a62c7f2e8cc36719ff0c24dc075`; original receipt SHA256
    `72cd99980a4523666b2dce595350c847097878155e14fe9352e78eea438c74a1`.
  - Owned SQL and `hm07-restore-a1` actual endpoint quit/login pass: character 133297, Cleric 25,
    XP 7,647,849, alive at fortress bind (1660.4301,1813.4900,253.7260), 156 journals, all eleven
    Haramel first completions, Q2293 once, **19 Iron/7 Bronze/662,739 Kinah**. Staff object 137763
    stays in full slot 3; the three coin pieces and incoming armour/accessories remain equipped,
    including belt slot 65536. Old cloth gloves and sealed bundle remain owned. Every saved skill
    is checked against the shipped stigma entries: none are present; regular skills persist.
  - The original visit/payment/revive/stall receipts survive restore without another entry or
    purchase. Both natural capture and restore finish with zero deaths. Seven fresh proof prechecks
    and the audit pass (`run/hm07-support/audit.json`); owned schemas are dropped, dump/receipt
    hashes remain intact and both incoming CG and historical Leg 1 snapshots are unchanged.
- [x] **HM-08 — Final checklist and handoff.** Depends: HM-07. Run the full CLAUDE checklist once
  on the committed implementation (including Fast and the now-eight baked maps), reconcile both new
  snapshots, retained equipped staff/no weapon purchase and all outcomes, tick only proven
  objectives and commit the completion checkpoint.
  - *Checklist correction 2026-10-03, completion pending.* All 33 commands ran on `5f3e979b7`;
    32 pass, including all eight baked maps and final Fast (82 tests/five switch skips/eleven
    scenarios). The solution suite retains two failed old expectations: its decoder count was
    still 117 before HM-01 added `SM_INSTANCE_INFO`, and the soak quest test expected zero live
    completions before CG's Java-based transition bookkeeping. Java `QuestState.setStatus` counts
    COMPLETE once; the corrected test also rejects double counting a duplicate update. Both
    focused cases and seven fresh prechecks pass (`run/hm08-testfix`). These are test-only
    corrections; commit them before rerunning the failed full solution command. HM-08 stays
    unchecked until the corrected solution run and final handoff audit pass. Original results
    remain in `run/hm08-final-checklist`.
  - *Done 2026-10-03.* All **33 CLAUDE gates pass** after those test-only corrections. The
    committed `dca2081ae` full solution rerun passes **5,184 tests/112 guarded skips/zero failures**;
    original failures remain untouched. The other 32 commands passed on the identical runtime
    implementation in `5f3e979b7`, including **eight baked maps** and `hm08-final-fast`
    (**82 passed/five explicit switch skips/all eleven scenarios**). Seven additional final
    prechecks pass. The handoff audit joins the original run with the committed solution repair,
    verifies only test/docs changed and preserves every command's source SHA (`run/hm08-audit.json`).
  - Both committed captures and actual restore/relog proofs remain verified: CG buys only the
    three rare chain pieces for four Iron Coins, after one Q2293 coverage completion and zero
    funding repeats; Haramel completes all eleven quests in two fresh clears. The endpoint is
    Cleric 25, alive at fortress, 156 completed journals, 19 Iron/7 Bronze/662,739 Kinah,
    original staff equipped, original/coin armour retained and the stigma bundle sealed. No
    permanent stigma skill, coin weapon/shield or other gear/book purchase is introduced.
  - `altgard-coingear`, `altgard-haramel-l12` and historical Leg 1 `altgard-l12` hashes remain
    intact; original Haramel budgets and receipts persist. Natural successful capture/relog has
    zero deaths; earlier death/route/assertion outcomes remain recorded. No owned GUID snapshot
    schemas or active journey remain. The audit's initial TRX aggregate skip-counter assumption
    is retained and corrected using the five actual skipped test results, without editing the TRX.
    **Stop here:** continuous full-area SIM, isolated LIVE, later zones and permanent stigma
    selection need their own subsequent work. No push or operator-stack action occurred.
  **Stop the combined goal here.** Full-area continuous SIM and isolated LIVE acceptance are later work.

For every commit run the seven current pre-commit checks: warning baseline, null loggers, clock reads,
custom quest drafts, fidelity, quest-plan compiler and retail quest inventory. Run Fast before gameplay
changes; do not build or run checks while a journey holds DLLs. Keep pass evidence and original failures.

## Combined goal prompt

The combined goal prompt is:

```text
/goal Work CG-00..CG-06 in docs/natural-altgard-coin-gear.md, then HM-00..HM-08 in docs/natural-altgard-haramel.md, as one goal. Follow docs/natural-ascension-altgard.md's loop with CG/HM in place of NA. Start from the verified altgard-l11 SIM snapshot; cg produces altgard-coingear, which starts Haramel as l12 and produces altgard-haramel-l12. Preserve the earlier Leg 1 snapshot altgard-l12.

Use the revised armour scope: bind at Heart of Impetusium, complete Q2293 once for coverage, then buy and equip Lohaban's rare Cleric chain gloves, shoulders and legs (4 Iron Coins; expected remainder 19). Keep the equivalent current chest and boots, helmet/accessories and old cloth gloves. Keep the stronger existing level-21 staff 101501357 equipped throughout CG and HM; do not buy a coin weapon/shield or switch/auto-equip any other weapon. Always select the better genuine coin tier where a shop offers tiers. This authorizes only the three-item armour manifest. Keep the stigma reward bundle sealed; never confuse regular skills with unacquired/unsocketed stigma skills.

Then complete all eleven active Asmodian Haramel quests once, including the lead-in and post-Q28507 quests, using two fresh solo clears and ordinary instance expiry/recovery. Finish alive and rebound at Altgard Fortress, with journals, currencies, equipment and skills verified through relog. Do not redo completed Leg 10 campaigns.

Each iteration orient with CLAUDE.md, leveling decisions, preceding ND/CG/HM evidence, git status and active SIM runs; pick the first unchecked item with dependencies done, read its Java spec and applicable 4.8 evidence, implement only it, verify once plus seven pre-commit checks and Fast before gameplay-change commits, record dated evidence and commit on main. Use free probe accounts, clear aggressive monsters for labelled setup and BeginWorldReload before setup teleports. Natural smoke runs restore snapshots with sim-snapshot.ps1 and use NaturalIshalgenPriestCompletesFrozenJourneyWithoutSetup with AF_ALTGARD=cg or l12 and Invoke-NaturalJourney's variables; keep the dashboard running and drop owned schemas in finally. Take hub flight transporters. Record deaths, timed failures, missed windows and escort losses as outcomes. Capture snapshots only from committed code. Never push, branch, use worktrees, touch the operator aion stack, buy skill books/other gear or use natural-character GM except approved help. Add server content only under applicable D26-D33 authority. Record decision needs under Blocked / questions for the operator and continue independent ready work. Run the full checklist at HM-08; stop when it is done or nothing is unblocked. Full-area SIM/LIVE acceptance is outside this goal.
```

## Sources read for this plan

Java branch `4.8` SHA `ce54b7931546cddafb970d20c9f71fec6d48c83b`: Haramel quest scripts,
`quest_script_data/haramel.xml`, `quest_data.xml`, `spawns/Instances/300200000_Haramel.xml`, Altgard
spawns, portal templates/locations, instance cooltimes/exits, `HaramelInstance`, `MonsterHunt`,
`ItemCollecting`, `PortalAI`, `PortalDialogAI`, `PortalService`, `InstanceService`, `InstanceCooltimeData`
and `PortalCooldownList`. Shipped C# data confirms the same quest/reward/entrance/lifetime facts.
Read and compare the actual C# handlers before implementing each item. This plan uses shipped content;
it does not approve server data additions or a shared Java bug fix.

Historical NCSoft [Haramel quests](https://power.ncsoft.jp/aion/powerWiki/%E3%83%8F%E3%83%A9%E3%83%A1%E3%83%AB%EF%BC%9A%E9%96%A2%E9%80%A3%E3%82%AF%E3%82%A8%E3%82%B9%E3%83%88)
provide context for the outside lead-in and post-boss quest chain. That page's older names/rewards
are not version-pinned 4.8 authority for replacing the shipped IDs, entry limits or quest quantities.
Any proposed retail correction needs specific 4.8 evidence and an applicable D26-D33 decision.

## Blocked / questions for the operator

- 2026-10-03, HM-07 naming: `run/snapshots/altgard-l12/snapshot.json` already identifies the preserved
  **Leg 1** capture (`source: natural-altgard-l1`, SHA `5083bb385`, 2026-10-01,
  dump SHA256 `e439e7d604df626cc316c7ea9799fece5b944be15ec020f41c31fd9b1011091b`).
  The Haramel plan reused that historical level-12 name. Never overwrite or rename the earlier
  snapshot while existing legs depend on it. **Answered 2026-10-03:** the operator chose
  **`altgard-haramel-l12`**. HM-07 updates the endpoint contract/runner selectors before capture;
  HM-03..HM-06 can proceed independently. No decision remains pending.

Two clears and natural expiry are a dependency requirement, not a request to bypass
instance rules. If a probe finds a shared handler, quest-drop, packed-var or geometry defect that cannot
be corrected under existing authority, record it here and continue the remaining independent items.

## Progress log

- 2026-10-03 HM-08 complete: all 33 checklist gates pass after two Java-checked stale test
  expectations are corrected; committed solution rerun 5,184 passed/112 skips, final Fast 82
  passed/five switch skips/eleven scenarios, eight baked maps and seven final prechecks pass.
  Original failures/audit draft retained; combined snapshot/gear/coin/stigma/recovery handoff
  reconciled (`run/hm08-audit.json`, runtime base `5f3e979b7`, test repair `dca2081ae`). All CG/HM
  items complete; stop at `altgard-haramel-l12`. Continuous full-area SIM/LIVE remain later work.
- 2026-10-03 HM-08 correction: all 33 checklist commands executed; 32 pass, while the solution
  suite catches two outdated packet-inventory/completion-count expectations. Java-checked test
  corrections, duplicate-update regression and both focused cases/seven fresh prechecks pass.
  Commit the test-only fixes, then rerun the full solution command; HM-08 remains unchecked.
- 2026-10-03 HM-07 complete: `altgard-haramel-l12` captured from committed `aea035881`, hash/SQL/
  actual endpoint relog verified, original receipts retained and owned schemas dropped. Cleric 25,
  156 journals, zero deaths, 19 Iron/7 Bronze/662,739 Kinah, equipped original staff/coin chain and
  sealed bundle, no stigma skills. Seven proof prechecks pass; prior snapshots unchanged
  (`run/hm07-support/audit.json`). HM-08 runs the full checklist and stops at the handoff.
- 2026-10-03 HM-07 support: distinct endpoint naming and immutable original Haramel receipt
  capture/restore pass the snapshot contract, 22 scoped cases, seven sequential prechecks and
  Fast (82 passed/five switch skips/all eleven scenarios). Failed fixture/build drafts retained.
  Commit support before the natural capture; HM-07 remains unchecked until capture/SQL/relog pass.
- 2026-10-03 HM-06 complete: natural two-clear smoke `hm06-smoke-a22` and fortress relog pass;
  level 25, 156 journals, zero deaths, 19 Iron/7 Bronze, retained staff/armour/sealed bundle.
  Six cage-route misses and all earlier failures remain recorded. Sixty-nine focused cases,
  seven prechecks and Fast (82 passed/five switch skips/eleven scenarios) pass; schemas dropped
  and incoming dumps unchanged (`run/hm06-support/audit.json`, base `83b715930`). HM-07 is next.
- 2026-10-03 HM-05 complete: natural expiry/fresh entry, all 74 original sources, packed 63/64/65,
  four post-boss completions and actual death/relog/two-process recovery pass. Actual spawn packet
  identity replaces the unspawned channel fallback. Thirty-four scoped cases, seven checks and
  Fast (82 passed/five switch skips/all eleven scenarios) pass; failures retained, schemas dropped,
  incoming snapshots unchanged (`run/hm05-support/audit.json`). HM-06 natural runner is next.
- 2026-10-03 HM-04 complete: actual full-health bosses, neighbouring two-attacker pull, regular
  healing/potions, summons/movie/class chest/all offered loot/exit/relog pass without HP edits or
  natural setup. Seven fresh prechecks and Fast 81 tests/eleven scenarios pass; draft failures and
  cleanup retained in `run/hm04-support/audit.json`. HM-05 proves fresh-copy post-boss recovery.
- 2026-10-03 HM-03 complete: seven first-clear completions/movie/collections/counters/gates/rewards
  and actual tower key/oil consumption survive relog. Forty-four focused cases, seven prechecks
  and Fast 80 tests/11 scenarios pass (`run/hm03-support/audit.json`); original probe/resource
  failures retained, owned schemas dropped. HM-04 proves actual full-health combat and class loot.
- 2026-10-03 HM-02 complete: generated/rebaked Haramel navigation and actual portal/lift/tower
  glide/wooden elevator/exits/hub-flight/Heart-recovery travel pass. Forty-six focused cases,
  seven prechecks and Fast 79 tests/11 scenarios pass; retained drafts and cleanup are recorded
  in `run/hm02-support/audit.json`. HM-03 now proves first-clear quest protocols.
- 2026-10-03 HM-01 complete: l12 contract/selectors, eight plans, three custom protocols, two visits,
  packed/multiple objectives and retained cold receipts/budgets are implemented. Java channel-ID and
  recyclable-NPC-ID semantics are accounted for; soup payment and full belt slot persist. 81 focused/
  regression cases, seven prechecks and revised Fast pass (`run/hm01-support/audit.json`). Next HM-02
  generates navigation and proves ordinary travel; the natural Haramel smoke remains HM-06.
- 2026-10-03 HM-00 complete: verified CG incoming state and all eleven Java/C# quest/gate/reward,
  entrance/lift/exit, instance lifetime/configuration and 74-source facts. Inactive class weapons
  stay inactive; selected armour/belt indexes and packed counters are frozen. Initial audit class-ID
  draft retained, corrected audit and seven prechecks pass. Next HM-01 adds the shared l12 contract
  and persistent two-visit decisions; no Haramel runtime or content change has occurred.
- 2026-10-03 CG-06 handoff: actual committed `altgard-coingear` hash/SQL/restore/relog and tier/gear/
  currency/stigma ledger are reconciled in `run/cg06-handoff/audit.json`. All CG items complete;
  Haramel implementation begins with HM-00. No HM quest or natural instance run has happened yet.
- 2026-10-03: plan audited on `76c699cdc` against Java `ce54b7931`: eleven active Asmodian quests,
  four post-Q28507 unlocks, 65-kill packed counter, 74 qualifying spawn spots, two fresh clears,
  level-16 minimum/no maximum, 16 daily entries with 09:00 reset, 600-second empty expiry and
  actual lift/Cleric chest/exits. No gameplay implementation or natural run; all HM items unchecked.
  Machine-readable audit and all seven passing pre-commit checks are in `run/cg-hm-plan`;
  the initial shared-build DLL collision and passing sequential quest-draft retry are retained.
- 2026-10-03 armour review: incoming balance amended to 19 Iron Coins and three purchased chain
  pieces with retained chest/boots. The operator's current-staff instruction applies throughout both
  sections, including reward/loot auto-equipping. No coin weapon/shield purchase or staff swap.
  Snapshot SQL/item-stat audit and seven pre-commit checks pass (`run/cg-hm-armor-review`);
  base commit `5fef285bd`, planning only.
