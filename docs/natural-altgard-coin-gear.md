# Altgard coin gear preparation before Leg 12

**Status (2026-10-03): CG-00..CG-05 complete; CG-06 remains.** The operator requested Q2293,
binding at Heart of Impetusium, the best available coin chain gear and weapon, then Haramel as Leg 12.
The operator amended the weapon scope on 2026-10-03: **keep the equipped level-21 staff; do not switch
it or buy a coin weapon**. A shield cannot be worn with that two-handed staff and is excluded too.
After auditing the worn armour, this plan recommends one natural Q2293 completion for coverage and
three chain purchases: gloves, shoulders and legs, **4 coins**. Keep the equivalent chest/boots,
helmet/accessories and the old cloth gloves as an alternative. **Zero funding repeats are needed**.
The operator explicitly confirmed the revised three-piece, four-coin scope on 2026-10-03 while the
combined goal was active. This supersedes the pasted goal's older five-piece/mace/shield clause.

Continue with [Leg 12: Haramel](natural-altgard-haramel.md) only after CG-06. This preparation has the
separate selector `cg`, with its natural runner and owned SIM smoke verified in CG-04. Its committed
snapshot and actual restore/relog are verified in CG-05. Haramel keeps `l12`, to be implemented in HM-01.

## Authority and incoming state

- Work in the existing checkout on `main`, following the loop in
  [natural-ascension-altgard.md](natural-ascension-altgard.md#how-to-work-this-list-loop-protocol).
- The 2026-10-03 request authorizes the listed coin armour purchases. This is a narrow exception
  to the natural journey's earlier gear-purchase ban. It does not authorize skill books, other gear,
  coin weapons/shields, random coin chests, Lohaban's Treasure/Q2147, enchantment purchases or coin conversion.
- Reuse ordinary travel, combat, bind, Return, inventory and recovery. No natural-character GM
  progression, no operator `aion` stack, branches, worktrees or push. OD-13 help remains the only GM exception.
- Java `../aion-server`, branch `4.8`, SHA `ce54b7931546cddafb970d20c9f71fec6d48c83b` is the default spec.
  Read the applicable Java before implementation. Shared defects/content changes require an applicable
  approved decision D26-D33; otherwise record the question and continue independent work.
- Incoming `run/snapshots/altgard-l11`: character 133297, Cleric 24, alive and bound at fortress 700065,
  144 completed journals, 536,193 Kinah, cube 63 slots, **Iron Coin 186000006 x18, Bronze Coin 186000007 x0**.
  The relog-verified `after.Inventory` in `altgard-l11-completion.json` is the balance authority.
  Snapshot capture SHA is `e23d5a5112b852b1481fe6cc665ab9dd9a3e4979`; planning checkout is `76c699cdc`.
- Q2900 is complete. Keep bundle 188053787 sealed. A normal skill is not a stigma: do not select,
  buy, open, socket or assume any permanent stigma skill in either new section. Use actually learned
  regular skills and ordinary automatic learning as levels change.

## Coins already earned

These completed quests each have a fixed reward of one Iron Coin in shipped Java/C# quest data.
Their total, **18**, reconciles exactly with the saved balance; Q2293 is not completed in the snapshot.

| Quest | Quest | Quest |
|---|---|---|
| Q2146 Pass the Message | Q2210 Retrieving the Report | Q2220 Picking off Frightcorn |
| Q2236 Rarified Tastes | Q2237 A Fertile Field | Q2239 Malodor Antidote |
| Q2241 Glowing Mushroom | Q2242 A Nice Gesture | Q2243 A Crystal Hand Mirror |
| Q2244 A Drinking Problem | Q2245 Ksellid Control | Q2248 The Secret Letter |
| Q2253 Kagorinerk's Gift | Q2261 Failure to Report | Q2262 A Sneaky Delivery |
| Q2263 Shugo Potion | Q2264 The Sting of Poison | Q2265 A Lost Sword |

Recount actual inventory on restore; do not infer available funds only from journal completion.
Coins are protected from the surplus-selling policy throughout these sections.

## What the Heart actually sells

Lohaban **203689**, at (2665.30, 1659.18, 324.69), is the Cleric vendor. His reward-shop tabs are
983 (weapons), 985 (Cleric chain/cloth), 987 (scale shield), 2124 (coin chests) and 1149 (Q2147 material).
Lateni **203659**, alongside him, offers Q2293 and a different equipment variant.

The shipped Heart catalogue contains **one level-16 RARE equipment tier**, with two manastone slots.
It does not contain an extra cheap/common and expensive/legendary chain tier. Lateni's "Magic" chain
is an equal-price alternative with different bonuses, not a higher tier. Recommend Lohaban's chain
with healing/concentration bonuses for this Cleric. If a later coin shop offers multiple genuine
quality tiers, choose its better tier; never substitute a cheaper tier to shorten a grind. Any
unexpected Heart catalogue/price difference must be reconciled before spending, not filled with new content.

| Slot | Available coin armour | ID | Iron Coins | Revised recommendation |
|---|---|---:|---:|---|
| Torso | Rank 9 Asmodian Hauberk | 110501096 | 2 | Keep current equivalent chest |
| Gloves | Rank 9 Asmodian Handguards | 111501065 | 1 | Buy for the requested chain set; retain cloth gloves |
| Shoulders | Rank 9 Asmodian Spaulders | 112501015 | 1 | Buy; current slot is empty |
| Legs | Rank 9 Asmodian Chausses | 113501074 | 2 | Buy; replaces level-8 cloth |
| Feet | Rank 9 Asmodian Brogans | 114501081 | 1 | Keep current equivalent boots |
| **Selected total** | **Gloves, shoulders and legs** | | **4** | **No weapon or shield** |

Buying all five body pieces would cost 7 coins, but the extra chest/boot purchases would add only
socket capacity. They are excluded from the revised manifest; this goal does not install manastones.

## Equipped armour versus coin armour

`altgard-l11-completion.json` records these worn IDs/slots. The saved SQL independently confirms
`is_equipped=1`, enchant 0, no random/enchant/fusion bonuses and no installed manastones on these
pieces or the staff. Java and C# item definitions match, so the unsocketed comparison is direct.

| Slot | Currently worn | Type / level | Current -> coin physical defence | Assessment |
|---|---|---|---|---|
| Torso | Altgard Legionary Hauberk 110551139 | Chain / 16 rare | 112 -> 112 | Identical modifiers; coin has 2 sockets instead of 1 |
| Gloves | Altgard Legionary Gloves 111101650 | Cloth / 16 rare | 27 -> 67 | Chain trades casting/mana stats for defence and healing boost |
| Shoulders | Nothing | Empty | 0 -> 67 | Clear gain; adds 3 healing boost and 3 physical attack |
| Legs | Anturoon Leggings 113100773 | Cloth / 8 common | 18 -> 90 | Clear gain; more defence, resistance, evasion, MP and 5 healing boost |
| Feet | Altgard Legionary Brogans 114501726 | Chain / 16 rare | 67 -> 67 | Identical modifiers; coin has 2 sockets instead of 1 |
| Head | Altgard Dark Legionary Chain Helm 125004139 | Chain helm / 18 common | 45 -> no coin equivalent | Keep it |

The glove trade is explicit: **+40 physical defence, +3 healing boost, +10 parry**, but
**-6 magic boost, -27 HP, -63 MP, -8 magical resistance and -8 evasion** compared with the cloth
pair. Concentration and hate reduction are unchanged. Recommend chain gloves for the operator's
chain preference, not because they improve every stat; retain the cloth pair rather than sell it.

With the three purchases and retained chest/boots, five body slots total **403 physical defence
instead of 224** (+179; the helmet stays separate). Combined body-item changes are +11 healing
boost, +27 magical resistance, +44 evasion and +7 concentration, with -6 magic boost, -27 HP and
-55 MP. These are item totals before character/skill modifiers, not a promised damage/healing rate.

The Heart sells no Iron Coin helmet, belt, earrings, rings, necklace, wings or plume. Keep the owned
items in those slots and record their actual equipment state. Do not describe this as replacement of
every character slot. Replacing just the three selected slots achieves the same body defence as
buying all five, while preserving the equivalent owned torso and boots.

The current **Altgard Dark Legionary Staff 101501357 (level 21)** has 370 magic boost and 88-132 weapon
damage. The coin staff 101500810 (level 16, 3 coins) has 320 and 74-112; it would downgrade that weapon.
The coin mace has 280 magic boost and 56-84 weapon damage. **Keep staff 101501357 equipped in both
hands throughout CG and HM**, as the operator instructed. No coin mace, staff or shield purchase,
weapon swap, or weapon auto-equipping is allowed. Ordinary loot may be retained without replacing
the staff. Extra socket capacity is not an installed bonus; no manastone or stigma socketing is included.

Prices come from `item_templates.xml` acquisition `REWARD`, item 186000006 and count, not the
templates' Kinah `price`. Java `TradeService` charges reward shops without Kinah; `TradeList`
sums the token requirements. `CM_BUY_ITEM` action **15** is the reward-shop purchase action.

## Q2293 budget and route

**[Coin] Mutated Spirits**, Lateni 203659, minimum level 16:

- Kill **6 Bladestorm Spirits 210576** (counter 0).
- Kill **16 Cyclone Spirits 210578 or 210523 combined** (counter 1).
- Return to Lateni for **5 Iron Coins**. No quest XP is specified; normal kill XP still applies.
- `max_repeat_count=255` is Java's **unlimited-repeat sentinel**, not a 255-completion ceiling.
  Q2293 has no timed repeat cycle. Prove normal reacceptance and both reset counters on a probe.

For observed balance B and remaining shopping cost C, funding repeats are
`ceil(max(0, C - B) / 5)`. Here `ceil(max(0, 4 - 18) / 5) = 0`.

| Natural scope | Quest completions | Required kills | Before shopping | After spending 4 |
|---|---:|---:|---:|---:|
| Funding only | 0 | 0 | 18 | 14 |
| **Recommended: exercise Q2293 once** | **1** | **6 + 16 = 22** | **23** | **19** |

The selected natural scope is the second row. Do not grind extra coins after that completion.
Unexpected restored funds or catalogue changes require updating CG-00's audit first. Resume a saved
active Q2293 from its counters; never re-award a completed quest merely because it can repeat.
Check its **CompleteCount=1** and 5-coin receipt across relog, rather than using presence in the
completed-ID set to decide whether a repeat has already been rewarded. Java sends repeatability
separately in `SM_QUEST_COMPLETED_LIST`; the shared bot already records repeatable completions.

Take the existing **fortress to Heart hub flight**, then bind normally at obelisk **700067**
(2656.192, 1660.59, 325.052). Observe the actual quoted bind fee (Leg 9 recorded 813 Kinah).
The hub and both vendors are on the **upper pillar at z325**; the spirits are on lower ground around
z250. Reuse Leg 9's proved descent/ascent and recovery routes; a straight ground path to the vendor
is not valid. Walk the lower spirit circuit, select reachable living targets by the two counters,
take ordinary respawns if a local pool is exhausted (shipped respawn 295 seconds), then climb/fly
back using the proved route. Do not move to a distant hub on foot. Stay bound at the Heart through
the grind, purchases, endpoint relog and coin snapshot. Deaths/retreats are recorded outcomes.

Buy one copy each of **111501065, 112501015 and 113501074** from Lohaban using the observed NPC
object ID, real shop dialogue, reward purchase and inventory receipts. Require sufficient cube space
and a confirmed **4-coin debit**, with no gear-related Kinah debit. Equip only those three armour
pieces through ordinary item packets. Read Java `CM_EQUIP_ITEM`, restrictions and `ItemSlot` first;
the retained staff's mask 3 occupies both hands. Protect the purchased pieces, retained torso/boots,
old cloth gloves and equipped staff from selling or unwanted auto-equipping. Check all five body
slots, the unchanged staff, stats and remaining coins across an actual relog; a sent packet alone
is not proof. Retain the existing accessories and helmet too.

## TODO list

Every item requires its one focused verification, dated evidence, progress line and commit on `main`.
Use the parent loop with **CG** in place of NA; read Leg 11 ND-00..ND-08 and this plan on orientation.
Before building, inspect any running SIM and its result; never contend for DLLs. Run all seven
pre-commit checks (warning, null logger, clock, quest drafts, fidelity, plan compiler, retail inventory),
plus `run-fast.ps1` before gameplay-change commits. Retain failures; do not rerun a passing check without cause.

- [x] **CG-00 — Freeze the incoming audit and shopping manifest.** Depends: ND-08. Hash/metadata,
  observed inventory/journals, Java reward/repeat/trade/equip rules, all active regional catalogues,
  exact three purchase IDs/costs, worn armour comparison, protected/equipped items, one-completion
  limit and endpoint assertions. Save machine
  readable audit evidence; any conflict with the above becomes a named operator question.
  - 2026-10-03 evidence: `run/cg00-audit/audit.json` and `audit.py` independently verify the dump hash,
    actual SQL/packet inventory, 144 completed journals, absent Q2293, 18 fixed Iron Coin rewards,
    seven regional catalogues, matching Java/C# item and quest data and the approved three-item
    manifest costing 4. Country code 99 selects `goodslists.xml`; Java reward purchase action 15,
    pre-debit funds validation, equip action 0/64-bit slots and unlimited-repeat sentinel were read.
    All 13 worn SQL rows (including accessories and power shards) have no enchant/fusion/random
    bonuses or installed stones. Freeze staff object 137763/mask 3, torso/boots/helmet/accessories,
    old cloth gloves and sealed 188053787. Endpoint: Q2293 CompleteCount 1, 145 distinct journals,
    19 Iron/0 Bronze Coins, three new chain slots, alive and bound at 700067 through actual relog.
    All seven pre-commit checks pass; no gameplay or Fast required for this audit-only item.
    Base commit `86eb2e021`; snapshot capture `e23d5a511` remains unchanged.
- [x] **CG-01 — Contract, decisions and owned probe support.** Depends: CG-00. Add `cg` to the leg
  registry, environment selector and snapshot script; compile Q2293's plan. Add the explicit shopping
  manifest and repeat completion/receipt state to shared SIM/LIVE decisions and persistence. Provision
  genuinely unused probe accounts starting at 221 after auditing all tests; current fixture stops at
  220, so extend it deliberately. Never borrow D32's reserved 151-200 or collide with another run.
  - 2026-10-03 evidence: `natural-altgard-cg-contract.json`, compiled `2293.json`, shared coin
    policy/decision observation, protected inventory policy and checkpoint/relog counts/receipts.
    Accounts 221/222 were absent from all SIM account uses and are provisioned at access level 0
    for CG-02/03 only. Java MonsterHunt, QuestState, completed-list packet and CG-00 trade/equip
    rules govern the two counters, one-completion limit, exact debit receipts and unchanged staff
    object. A completed ID without its count cannot restart Q2293 or authorize shopping; an owned
    piece without its debit receipt cannot cause a duplicate purchase. `run/cg01-contract/`:
    65 focused tests pass and all seven pre-commit checks pass; `run/cg01-fast/` has 11/11 Fast
    scenarios passing (76 tests passed, four explicitly skipped). The initial full-cube test expected
    a direct block instead of existing town service; failure/detail logs and corrected verification
    are retained. No server content, natural purchases or snapshot capture. Base `f58ccfce0`.
- [x] **CG-02 — Heart travel and Q2293 probe.** Depends: CG-01. Prove the actual hub flight, bind,
  lower-ground loop, return to the upper vendors and ordinary Heart recovery at level 24. Complete
  both distinct kill counters with normal combat, reward, reaccept and reset on the probe. Clear
  aggressive neighbours only for labelled setup; call `BeginWorldReload` before setup teleports.
  - 2026-10-03 evidence: `run/cg02-quest/probe-counts.log` and
    `run/cg02-probe-a5-cg02.trace.summary.json`, free account 221, level-24 incoming equipment and
    actual learned skills. Normal fortress flight from 203561 costs 565 Kinah; Heart bind costs
    813. Three checked pillar flights and 29 travel action legs cover the upper vendors, full lower
    spirit fields and bind recovery. Both counters finish at 6/16, including both Cyclone variants;
    22 combat attempts have 21 successful targeted kills, zero deaths/retreats, and the complete
    22-credit objective. Targets' normal HP is retained. Twenty-six aggressive neighbours were
    cleared as labelled probe setup, preserving quest spirits. Reward 18 -> 23 coins, actual relog
    confirms CompleteCount 1/repeatable, reaccept resets both counters without another reward.
    One controlled death uses real CM_REVIVE to the upper Heart bind; staff object remains equipped.
  - Java `QuestService.finishQuest` sends completion status without a fresh completed-list count.
    Corrected the shared bot to count observed transitions once, retain repeat counts, saturate the
    wire count at 255 and reconcile against login; repeatability is still taken only from its own
    packet. Checkpoints now record counts for newly completed quests too. Thirty-five focused
    count/contract/persistence tests, all seven prechecks and Fast 11/11 pass (`run/cg02-fast/`:
    77 tests passed, four explicit skips). Retain build, wrong-pad, platform approach, five-metre
    bind-range and missing-live-count failures plus their corrections in `run/cg02-quest/`.
    Base commit `0f7fdeba8`; no server/content change or natural-character setup.
- [x] **CG-03 — Coin purchase and equipment probe.** Depends: CG-02. Prove the real reward-shop
  offer, three armour purchases, exact debit, insufficient-funds refusal without partial equipment,
  all five final chain body slots and unchanged two-handed staff across endpoint relog. Prove no
  weapon/shield purchase or weapon swap, and protect currencies/armour/old cloth gloves/staff.
  Keep probe-created items separate from the natural journey; do not change the server's catalogue.
  - 2026-10-03 evidence: `run/cg03-equipment/probe-journal-fixed.log` and
    `run/cg03-probe-a2-cg03.trace.summary.json`, disposable account 222. Real Lohaban trade type 4
    (Java `index()`, not ordinal), observed tab 985 and acquisition counts 1/1/2 match the manifest.
    A real action-15 batch with only three coins refuses all three pieces, leaving inventory,
    equipment, Kinah and coins unchanged. A labelled hunt-counter setup follows normal acceptance;
    the real reward produces 18 -> 23, then three single purchases produce 23 -> 22 -> 21 -> 19
    without a Kinah debit. Shared SIM/LIVE `NaturalCoinGearSteps` verifies each debit/object and
    action-0 armour equip. Five final chain slots, old cloth gloves, sealed bundle, protected
    inventory, unchanged staff object/mask 3 and completion count 1 survive actual endpoint relog.
  - `run/cg03-equipment/packet-audit.json` decodes Fast's real outgoing packets: one refused
    three-piece order, three approved single buys, only slots 16/2048/4096, no weapon/shield
    purchases or weapon swaps. SIM's trace allowlist now includes buy/equip packets. Seven
    prechecks pass; `run/cg03-fast/` passes 11/11 scenarios, 78 tests, four explicit skips.
    Retain the initial compile failure, probe-only partial-journal setup failure and missing
    buy/equip trace diagnostic with their corrections in `run/cg03-equipment/`. Base `520062b73`;
    no server/catalogue change or natural-character setup/purchase. CG-04 is next.
- [x] **CG-04 — Natural runner and one smoke.** Depends: CG-03. Restore `altgard-l11` with
  `sim-snapshot.ps1 -Action Restore`; run `NaturalIshalgenPriestCompletesFrozenJourneyWithoutSetup`
  using `AF_ALTGARD=cg` and every variable set by the script's `Invoke-NaturalJourney`.
  One Q2293 completion, three observed armour purchases/equips, **19 Iron Coins**, 145 distinct
  completed journals, alive/bound at the Heart, sealed stigma bundle and unchanged equipped staff
  through relog. Keep the
  dashboard at http://127.0.0.1:17880/ running and announce it; drop only the owned schema in finally.
  - 2026-10-03 evidence: `run/cg04-smoke/journey-a2/altgard-cg-completion.json` and
    `run/cg04-smoke/audit.json`, `cg04-smoke-a2`, seed 1, character 133297 restored by the snapshot
    script with every Invoke-NaturalJourney variable. Real hub flight costs 565, Heart bind 813;
    one normal 6/16 hunt and real reward produce 18 -> 23, exact approved purchases produce
    23 -> 22 -> 21 -> 19 with Kinah fixed at 534,815. Thirty-three encounters are successful,
    no deaths, four ordinary silence refusals handled; elapsed game time 1,127,000 ms.
    Actual endpoint relog verifies 145 journals, Q2293 count 1, 19 Iron/0 Bronze, five chain body
    slots, old cloth gloves, sealed bundle, retained helmet/accessory/torso/boot objects and
    original staff 137763/mask 3. Outgoing audit has only the three armour buys/equips.
  - Shared runner uses receipts in decisions/checkpoints, protected inventory, explicit CG-only
    armour equips, hub flights and ordinary bind-view reload. The initial smoke completed the hunt
    but exposed an unrecognized silence refusal on its return path; Java PlayerRestrictions/SilenceEffect
    confirms no cast starts. BotCastProtocol now recognizes that refusal and combat advances/re-evaluates
    normally. Fourteen cast tests, seven prechecks and Fast 11/11 pass (`run/cg04-fast`: 78 passed,
    four explicit skips). Original failure/trace and correction logs remain; both owned smoke schemas
    are dropped in finally. Base `16d276d51`; no server/content correction or natural setup.
- [x] **CG-05 — Committed-code snapshot.** Depends: CG-04. Commit any capture-support changes
  before capture; capture `altgard-coingear` from `altgard-l11` through `cg`, hash-verify and prove
  restore/relog with the same receipts/equipment/bind/balances. Record the actual capture SHA and
  drop owned schemas. Never capture from an uncommitted gameplay tree or edit a dump.
  - 2026-10-03 support checkpoint: `AF_CG_RECEIPTS` restores only verified endpoint diagnostics
    belonging to the actual logged-in character. The existing coin policy requires every purchased
    object/slot, exact balance, original staff and single quest completion before accepting them;
    it grants no state and cannot authorize another purchase. Snapshot invocation clears inherited
    receipt settings. Eight focused contract tests, seven prechecks and `cg05-fast` pass (78 tests,
    four explicit skips, 11/11 scenarios). Evidence: `run/cg05-support/`, base `c904afd75`.
    Commit this support before capture; CG-05 remains unchecked until hash/SQL/restore/relog proof.
  - 2026-10-03 capture draft: `cg05-capture` on committed `4bb64981b` failed at receipt loading
    before natural actions because the cleared environment option arrived as an empty string.
    Ignore the empty optional path just as the existing selectors do. Failure log/trace/package
    remain in `run/snapshots/_capture/cg05-capture/`; no snapshot was created and the owned schema
    was dropped in finally. All seven fresh prechecks and `cg05-fast-a2` pass (78 tests,
    four explicit skips, 11/11 scenarios); commit the correction before the capture retry.
  - 2026-10-03 complete: `cg05-capture-a2`, seed 1, captures `run/snapshots/altgard-coingear/`
    from committed `3afda849b006d02d0014b182e9a22e1b50cc7d5b`, UTC
    `2026-10-03T15:37:22.7117265Z`, total elapsed 67,800,001 ms. Dump SHA256
    `38e263f3fb9e95d5f500cce13ef7117e36401bc177098deba7991661085e953f` is independently verified.
    `cg05-restore-relog` restores through the snapshot script, audits owned SQL and performs a real
    endpoint relog: 145 journals, Q2293 count 1, 19 Iron/0 Bronze, Kinah 534,815, all five chain
    body pieces, retained cloth gloves/bundle and original staff 137763/mask 3 persist. Full item
    blobs and SQL also verify every equipped accessory, including belt 134334/slot 65536: Java's
    separate short slot field masks it to zero. The initial audit drafts missed that mask and
    expected bought objects at the pre-purchase login; corrected audit evidence is retained.
    Capture takes 1,127,000 game ms with zero deaths; restore takes 10,001 ms with zero deaths.
    `run/cg05-support/audit.json` proves exactly three approved buys/equips at capture and none
    on restore. Capture/restore schemas are dropped in finally; the read-only schema check is empty.
    Seven fresh proof prechecks pass; support gameplay already passes `cg05-fast-a2`.
- [ ] **CG-06 — Preparation checkpoint.** Depends: CG-05. Reconcile evidence, chosen tier, journal,
  coins, actual equipped slots, unchanged staff, retained cloth gloves, protected stigma bundle and outcomes.
  Mark only demonstrated objectives complete; commit the handoff to HM-00. The combined goal
  continues directly into Leg 12; this item does not launch full-area SIM or LIVE acceptance.

## Sources read for this plan

Java/C# `static_data/quest_data/quest_data.xml`, `quest_script_data/altgard.xml`,
`npc_trade_list.xml`, main `goodslists/goodslists.xml`, `items/item_templates.xml`, Altgard spawns;
Java `MonsterHunt`, `QuestState.canRepeat`, `QuestService`, `TradeService`, `TradeList`,
`CM_BUY_ITEM` and `ItemSlot`; the Leg 9 contract/routes and Leg 11 snapshot completion JSON.
The implementation audit must also read equip handlers and confirm the configured regional goods
list rather than assuming the main XML alone. Historical NCSoft
[coin-reward guide](https://power.ncsoft.jp/aion/powerWiki/%E3%82%B3%E3%82%A4%E3%83%B3%E5%A0%B1%E9%85%AC%E3%82%AF%E3%82%A8%E3%82%B9%E3%83%88)
is context for coin exchange, not version-pinned authority to change this shop's prices or tiers.

## Blocked / questions for the operator

None currently. Missing non-vendor slots and the weaker coin weapon are explicit scope limits above,
not permission to invent higher equipment. Record any new shared defect or unsupported retail change here.

## Progress log

- 2026-10-03 CG-05 complete: committed `3afda849b` capture/hash, owned SQL and actual restore/relog
  pass with 145 journals, 19 Iron, unchanged staff, chain body/retained accessories and sealed bundle.
  Three exact buys/equips at capture, zero on restore, no deaths and no surviving owned schemas.
  Corrected Java short-slot audit drafts and the earlier empty-option capture failure remain retained;
  seven proof prechecks pass. Next: CG-06 handoff, then continue directly with HM-00.
- 2026-10-03 CG-05 correction: the first committed capture exposed the empty optional environment
  path at login, before any hunt or transaction. The guard now ignores an empty option; seven fresh
  prechecks and Fast pass. Original evidence and cleanup retained; committed capture retry is next.
- 2026-10-03 CG-05 support: verified-endpoint receipt loading passes identity/inventory/count refusal
  cases, eight focused tests, seven prechecks and Fast. Commit support before natural capture;
  the snapshot, owned SQL audit and actual restore/relog are still pending.
- 2026-10-03 CG-04: natural CG smoke passes from altgard-l11, one completion and three purchases,
  145 journals/19 Iron, original staff and retained equipment, sealed bundle and Heart bind through
  relog. Thirty-three successful encounters, no deaths, four silence refusals; the first failure
  exposed missing client-side refusal handling, now corrected from Java. Cast tests, seven prechecks
  and Fast pass; both owned schemas cleaned up. Next: committed altgard-coingear capture/restore.
- 2026-10-03 CG-03: real reward offer, atomic underfunded-order refusal, three exact four-coin
  purchases/equips and endpoint relog pass on free account 222. Outgoing packet audit proves
  armour-only requests and no weapon swap. Shared coin steps and buy/equip trace support,
  all seven prechecks and Fast 11/11 pass; failed drafts remain. Next: CG-04 natural smoke.
- 2026-10-03 CG-02: normal spirit combat, both counters, reward/relog/repeat reset and ordinary Heart
  revival pass on account 221; shared live completion-count bookkeeping corrected from Java and
  verified against login. Zero combat deaths/retreats, one controlled recovery death. Seven prechecks,
  focused 35/35 and Fast 11/11 pass. Next CG-03 proves real coin purchases and equipment persistence.
- 2026-10-03 CG-01: committed the cg contract/selector, compiled Q2293, shared transaction and
  repeat-count state, protected loadout policy and free accounts 221/222. Focused 65/65, seven
  prechecks and Fast 11/11 pass. Next CG-02 proves travel/bind, both counters and ordinary recovery.
- 2026-10-03 CG-00: froze the incoming SQL/packet audit, exact three-piece purchase manifest and
  endpoint receipts under `run/cg00-audit/`. The operator explicitly chose four coins and retaining
  the staff, superseding the older goal clause. Java trade/equipment and configured region audited;
  seven pre-commit checks pass. Next: CG-01 shared contract, decisions, persistence and free probes.
- 2026-10-03: plan audited on `76c699cdc` against Java `ce54b7931` and the verified `altgard-l11`
  inventory. Eighteen prior fixed coin rewards reconcile to 18 Iron Coins; seven-item manifest costs
  12, zero funding repeats, recommended one natural completion leaves 11. Implementation remains unchecked.
  `run/cg-hm-plan/audit.json` verifies the snapshot hash, selected Java item/price parity and all seven
  regional catalogues. All seven pre-commit checks pass. An initial quest-draft check collided with
  the warning build's extractor DLL; its failure log and the passing sequential retry are retained.
- 2026-10-03 armour review: the operator instructed retaining the equipped staff and excluding the
  coin weapon. The revised recommended manifest is chain gloves/shoulders/legs only (4 coins),
  retaining equal-stat torso/boots, helmet/accessories, staff and old cloth gloves. SQL confirms
  no enchantments/socketed stones; the glove casting-stat tradeoff and whole-body changes are
  recorded above. One Q2293 completion now leaves 19 Iron Coins. Machine-readable evidence is
  `run/cg-hm-armor-review/audit.json`; all seven pre-commit checks pass. Base commit `5fef285bd`.
  No gameplay run or purchase.
