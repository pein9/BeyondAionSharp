# Altgard coin gear preparation before Leg 12

**Status (2026-10-03): planned; CG-00..CG-06 are not implemented.** The operator requested Q2293,
binding at Heart of Impetusium, the best available coin chain gear and weapon, then Haramel as Leg 12.
This plan recommends one natural Q2293 completion for coverage, followed by the full Cleric coin
loadout below. The saved character already has enough coins: **zero repeats are needed to fund it**.
Starting this plan through the combined goal accepts that recommended loadout and single completion.

Continue with [Leg 12: Haramel](natural-altgard-haramel.md) only after CG-06. This preparation has the
separate selector `cg`; Haramel keeps `l12`. Both selectors and their snapshot support still need
implementation. No gameplay run was launched to write this plan.

## Authority and incoming state

- Work in the existing checkout on `main`, following the loop in
  [natural-ascension-altgard.md](natural-ascension-altgard.md#how-to-work-this-list-loop-protocol).
- The 2026-10-03 request authorizes the listed coin equipment purchases. This is a narrow exception
  to the natural journey's earlier gear-purchase ban. It does not authorize skill books, other gear,
  random coin chests, Lohaban's Treasure/Q2147, enchantment purchases or coin conversion.
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

| Slot | Recommended item | ID | Iron Coins |
|---|---|---:|---:|
| Torso | Rank 9 Asmodian Hauberk | 110501096 | 2 |
| Gloves | Rank 9 Asmodian Handguards | 111501065 | 1 |
| Shoulders | Rank 9 Asmodian Spaulders | 112501015 | 1 |
| Legs | Rank 9 Asmodian Chausses | 113501074 | 2 |
| Feet | Rank 9 Asmodian Brogans | 114501081 | 1 |
| Main hand | Rank 9 Asmodian Mace | 100100785 | 3 |
| Off hand | Rank 9 Asmodian Scale Shield | 115001118 | 2 |
| **Total** | **Five chain pieces, mace and scale shield** | | **12** |

The Heart sells no Iron Coin helmet, belt, earrings, rings, necklace, wings or plume. Keep the owned
items in those slots and record their actual equipment state. Do not describe this as replacement of
every character slot. The five chain pieces replace the mixed body armour, including cloth gloves,
level-8 cloth legs and the empty shoulder slot. Their base physical defence totals 403 versus the
incoming body's 224, before other equipment and character modifiers.

The current **Altgard Dark Legionary Staff 101501357 (level 21)** has 370 magic boost and 88-132 weapon
damage. The coin staff 101500810 (level 16, 3 coins) has 320 and 74-112; it would downgrade that weapon.
Recommend mace plus scale shield to fulfil the coin-weapon request and gain an off-hand shield; this
is a defensive choice, not a damage upgrade. The mace has 280 magic boost and 56-84 weapon damage.
Keep the current staff as a protected damage backup. Do not buy the coin staff as well.
The torso and boots have essentially the existing quest pieces' base bonuses; their extra socket
capacity is not an installed manastone bonus. No new manastone or stigma socketing is included.

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
`ceil(max(0, C - B) / 5)`. Here `ceil(max(0, 12 - 18) / 5) = 0`.

| Natural scope | Quest completions | Required kills | Before shopping | After spending 12 |
|---|---:|---:|---:|---:|
| Funding only | 0 | 0 | 18 | 6 |
| **Recommended: exercise Q2293 once** | **1** | **6 + 16 = 22** | **23** | **11** |

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

Buy one copy of each of the seven IDs from Lohaban, using the observed NPC object ID, real shop
dialogue, reward purchase and inventory receipts. Require sufficient cube space and a confirmed
12-coin debit, with no gear-related Kinah debit. Equip the mace in main hand and shield in off hand,
replacing the two-handed staff; equip all five chain body pieces through ordinary item packets.
Read Java `CM_EQUIP_ITEM`, item restrictions and `ItemSlot` first: the staff's mask 3 occupies both
hands, while mace and shield must end in masks 1 and 2. Protect the purchased set and old staff from
auto-selling/re-equipping during this endpoint check. Check the seven IDs/slots, stats and remaining
coins again across an actual relog; a sent packet alone is not proof of purchase or equipment.

## TODO list

Every item requires its one focused verification, dated evidence, progress line and commit on `main`.
Use the parent loop with **CG** in place of NA; read Leg 11 ND-00..ND-08 and this plan on orientation.
Before building, inspect any running SIM and its result; never contend for DLLs. Run all seven
pre-commit checks (warning, null logger, clock, quest drafts, fidelity, plan compiler, retail inventory),
plus `run-fast.ps1` before gameplay-change commits. Retain failures; do not rerun a passing check without cause.

- [ ] **CG-00 — Freeze the incoming audit and shopping manifest.** Depends: ND-08. Hash/metadata,
  observed inventory/journals, Java reward/repeat/trade/equip rules, all active regional catalogues,
  exact seven IDs/costs, protected items, one-completion limit and endpoint assertions. Save machine
  readable audit evidence; any conflict with the above becomes a named operator question.
- [ ] **CG-01 — Contract, decisions and owned probe support.** Depends: CG-00. Add `cg` to the leg
  registry, environment selector and snapshot script; compile Q2293's plan. Add the explicit shopping
  manifest and repeat completion/receipt state to shared SIM/LIVE decisions and persistence. Provision
  genuinely unused probe accounts starting at 221 after auditing all tests; current fixture stops at
  220, so extend it deliberately. Never borrow D32's reserved 151-200 or collide with another run.
- [ ] **CG-02 — Heart travel and Q2293 probe.** Depends: CG-01. Prove the actual hub flight, bind,
  lower-ground loop, return to the upper vendors and ordinary Heart recovery at level 24. Complete
  both distinct kill counters with normal combat, reward, reaccept and reset on the probe. Clear
  aggressive neighbours only for labelled setup; call `BeginWorldReload` before setup teleports.
- [ ] **CG-03 — Coin purchase and equipment probe.** Depends: CG-02. Prove the real reward-shop
  offer, seven purchases, exact debit, insufficient-funds refusal without partial equipment, correct
  two-hand to mace/shield change, all five chain slots and endpoint relog. Protect currencies/set/staff.
  Keep probe-created items separate from the natural journey; do not change the server's catalogue.
- [ ] **CG-04 — Natural runner and one smoke.** Depends: CG-03. Restore `altgard-l11` with
  `sim-snapshot.ps1 -Action Restore`; run `NaturalIshalgenPriestCompletesFrozenJourneyWithoutSetup`
  using `AF_ALTGARD=cg` and every variable set by the script's `Invoke-NaturalJourney`.
  One Q2293 completion, seven observed purchases/equips, **11 Iron Coins**, 145 distinct completed
  journals, alive/bound at the Heart, sealed stigma bundle and backup staff through relog. Keep the
  dashboard at http://127.0.0.1:17880/ running and announce it; drop only the owned schema in finally.
- [ ] **CG-05 — Committed-code snapshot.** Depends: CG-04. Commit any capture-support changes
  before capture; capture `altgard-coingear` from `altgard-l11` through `cg`, hash-verify and prove
  restore/relog with the same receipts/equipment/bind/balances. Record the actual capture SHA and
  drop owned schemas. Never capture from an uncommitted gameplay tree or edit a dump.
- [ ] **CG-06 — Preparation checkpoint.** Depends: CG-05. Reconcile evidence, chosen tier, journal,
  coins, actual equipped slots, backup staff, protected stigma bundle and all retained outcomes.
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

- 2026-10-03: plan audited on `76c699cdc` against Java `ce54b7931` and the verified `altgard-l11`
  inventory. Eighteen prior fixed coin rewards reconcile to 18 Iron Coins; seven-item manifest costs
  12, zero funding repeats, recommended one natural completion leaves 11. Implementation remains unchecked.
  `run/cg-hm-plan/audit.json` verifies the snapshot hash, selected Java item/price parity and all seven
  regional catalogues. All seven pre-commit checks pass. An initial quest-draft check collided with
  the warning build's extractor DLL; its failure log and the passing sequential retry are retained.
