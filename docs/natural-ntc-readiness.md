# Natural NTC readiness after the revised Altgard journey

Prepared 2026-10-06 for RC-12. The accepted run is `rc11-full-create-s1-a25`, seed 1, on runtime `242166ebc8a96594a4ba35df83d99295d234680d`. It creates one character and completes the approved route without restores. The immutable endpoint is `altgard-rc-complete-s1` (character 133276, Cleric 25, alive and bound at Altgard Fortress).

The next bounded goal should complete Abyss entry, then establish natural Templar and Sorcerer profiles and a coordinated Templar/Cleric/Sorcerer NTC party around levels 25–27. Park the Cleric at this endpoint while the other profiles catch up. Abyss execution, additional classes, the party controller, NTC and crafting remain separate contracts.

**Update 2026-10-06 (AX-14): Abyss entry is complete.** The Cleric's endpoint is now `morheim-abyss-entry-s1`: Cleric 26 at Morheim Ice Fortress, 166,405 XP into the level. See [Abyss entry: measured endpoint](#abyss-entry-measured-endpoint). The rest of this report describes `altgard-rc-complete-s1`, the endpoint that leg started from, and is left as written.

These contracts are steps toward the maintainer's long-term goals (2026-10-06, in order): a server play-tested by our bots, with bugs fixed as we encounter them; an intelligent bot that can play the whole game (all quests, gathering, crafting and the rest); and, only after those two, bots that can join a human group as an extra healer or DPS. See [CLAUDE.md](../CLAUDE.md#where-this-leads-the-long-term-goals). The Abyss-entry leg is planned in [natural-abyss-entry.md](natural-abyss-entry.md).

**Update 2026-10-07 (D39): class profiles come before the Templar and Sorcerer profiles named above.** The class seam, a Chanter branch at Ascension and levels 1-9 for the five other starter classes are planned in [natural-class-profiles.md](natural-class-profiles.md); the Templar and the Sorcerer later start from its Warrior and Mage profiles with a fresh run in the early-Ascension order, not from its Munin snapshots.

## Measured endpoint and historical comparison

| Measure | Historical AS-02 | Revised RC |
|---|---:|---:|
| Level | 25 | 25 |
| Completed quests | 157 | 176 |
| Cumulative XP | 7,882,373 | 8,497,039 |
| XP to next level | 1,495,353 | 880,687 |
| XP to level 32 | 19,703,470 | 19,088,804 |
| Kinah | 666,444 | 748,485 |
| Iron Coins | 19 | 19 |
| Bronze Coins | 7 | 7 |
| Recorded deaths | 11 | 13 |
| Game milliseconds | 70,210,001 | 73,044,001 |

The revised run completes 19 additional capital quests, each once. Both Haramel visits include the native boss movie, class-chest outcome and fresh-spawn evidence. The active journal contains only Q2945 at START/0; Abyss entry is unfinished. The full-run test takes 51.0851 wall minutes; game time is 20 h 17 m 24.001 s.

Deaths are outcomes. RC records Leg 4: 2, Leg 6: 1, Leg 8: 1 and Leg 10: 9; Ishalgen and Haramel record zero. AS-02 recorded Ishalgen: 1, Leg 4: 3, Leg 5: 2 and Leg 10: 5. This single-seed comparison has a changed route and does not establish a causal death reduction from early Ascension. The new route has two more total deaths.

Recoverable XP is 97,328 versus 56,591 historically. Ordinary paid soul healing can add XP and cost Kinah; it is not included in the fixed-reward projection below.

## Preserved gear and skills

The full native item-detail slot is used here. Fourteen equipped objects comprise thirteen durable gear objects and one Lesser Power Shard stack. The legacy ushort checkpoint lists thirteen equipped objects because the belt mask 65536 truncates to zero; native details and the retained Haramel receipt preserve its real slot.

| Slot mask | Item | Item ID | Object ID | Count |
|---:|---|---:|---:|---:|
| 3 | Altgard Dark Legionary Staff | 101501357 | 156530 | 1 |
| 4 | Altgard Dark Legionary Chain Helm | 125004139 | 156486 | 1 |
| 8 | Altgard Legionary Hauberk | 110551139 | 140185 | 1 |
| 16 | Rank 9 Asmodian Handguards | 111501065 | 157109 | 1 |
| 32 | Altgard Legionary Brogans | 114501726 | 139390 | 1 |
| 64 | Deyla's Earrings | 120000833 | 155670 | 1 |
| 128 | Gefion's Crystal Earrings | 120001132 | 143653 | 1 |
| 256 | Guardian's Ring | 122000871 | 147312 | 1 |
| 512 | Altgard Legionary Corundum Ring | 122001664 | 141149 | 1 |
| 1024 | Altgard Medal of Honor | 121000751 | 155635 | 1 |
| 2048 | Rank 9 Asmodian Spaulders | 112501015 | 157110 | 1 |
| 4096 | Rank 9 Asmodian Chausses | 113501074 | 157111 | 1 |
| 8192 | Lesser Power Shard | 169000004 | 140465 | 375 |
| 65536 | Hunmir's Leather Belt | 123001109 | 141160 | 1 |

Only the three approved better-tier chain pieces were bought: Handguards 111501065 for one Iron Coin, Spaulders 112501015 for one, and Chausses 113501074 for two. One native Q2293 completion pays five coins (18 → 23); purchases leave 19. The earned staff 101501357/156530 remains equipped in both hands. No coin weapon, other gear or skill book was bought. Deyla’s earned earring is the actual incoming accessory; historical contracts are preserved.

The character has 91 type-0 skill entries: 88 combat/passive entries, including learned rank IDs, and three profession entries. These are not a count of combat buttons. The full ID/rank/type list is in the audit. Essencetapping is 16, Aethertapping 1 and Morphing 1.

Q2900 is complete. This ordinary account has one unlocked normal stigma slot at level 25, zero advanced slots and zero socketed stigma skills. Bundle 188053787 remains sealed. Tutorial stone 140000001 and skill 11504 are absent, as is the inactive legacy reward 140000098. A permanent stigma requires an available ordinary slot, an owned stone and ordinary socketing under a future decision. Regular skills and stigma skills remain separate.

## Costs and consumable accounting

Observed currency updates record 1,054,733 Kinah received and 307,248 spent, versus 971,422/305,978 historically. Starting Kinah is 1,000; the closing balance is 748,485. Native service receipts record eleven teleports, nine binds and twenty-one hub flights, versus six/nine/twenty-one historically. The three coin purchases spend four Iron Coins and no Kinah.

The following ledger separates supplied help items, native use attempts, observed units removed and closing quantities. Removed units also include NPC sales and other removals; mixed stacks do not reveal which supplied or earned unit was consumed. A use attempt can be refused. The complete supply deliveries and transaction events remain in the JSON ledgers.

| Item | ID | Supplied | Use attempts | Units removed | Closing |
|---|---:|---:|---:|---:|---:|
| Zeller Aether Jelly | 160002273 | 16 | 15 | 14 | 6 |
| Minor Life Potion | 162000002 | 0 | 118 | 116 | 0 |
| Lesser Life Potion | 162000003 | 173 | 161 | 158 | 15 |
| Life Potion | 162000004 | 0 | 40 | 37 | 10 |
| Minor Mana Serum | 162000017 | 40 | 0 | 0 | 40 |
| Lesser Mana Serum | 162000018 | 40 | 8 | 8 | 35 |
| Minor Life Elixir | 162000052 | 0 | 10 | 9 | 0 |
| Lesser Life Elixir | 162000053 | 0 | 12 | 11 | 0 |
| Tea of Repose - 100% Recovery | 162001057 | 0 | 1 | 1 | 14 |
| Lesser Anti-Shock Scroll | 164000067 | 30 | 5 | 5 | 33 |
| Anti-Shock Scroll | 164000068 | 60 | 31 | 29 | 30 |
| Running Scroll | 164000075 | 20 | 7 | 7 | 13 |
| Awakening Scroll | 164000133 | 153 | 120 | 120 | 33 |
| [Event] Rx: Accelerox | 164002116 | 0 | 2 | 2 | 48 |
| [Event] Rx: Castafodin | 164002118 | 0 | 14 | 14 | 36 |
| Lesser Odella Powder | 169300003 | 517 | 0 | 393 | 175 |
| Odella Powder | 169300004 | 200 | 0 | 0 | 200 |

Book materials are consumed exactly once in the required 3/2/1 quantities, retaining one surplus sap. The eight-unit dye stacks remain unapplied. Timed, escort and carrier observations have their own outcome ledger; an adverse gameplay outcome is not rewritten into a test failure.

## Next bounded contracts

| Quest | Minimum level | Fixed XP | Fixed Kinah |
|---|---:|---:|---:|
| Q2945 Honing Your Skills | 25 | 20,110 | 0 |
| Q2946 Abyss General Knowledge | 25 | 20,110 | 0 |
| Q2947 Following Through | 25 | 301,641 | 31,320 |
| Q2042 The Last Checkpoint | 25 | 301,641 | 0 |

**Correction (2026-10-06, AX-00):** the Q2947 row above is the quest's first reward group. The handler sets reward group 1, which Java and C# `QuestService` read as a zero-based index: the second group, 403,012 XP and 4,000 Kinah with no AP. On that reading the fixed rewards total 744,873 XP and 4,000 Kinah, leaving 135,814 XP to level 26 and 18,343,931 XP to level 32. The figures in the rest of this section are the original ones. See [the Abyss-entry plan](natural-abyss-entry.md#rewards); AX-01 observed those amounts as paid on 2026-10-06. The leg now begins with Q24020 "Aegir's Orders" at Morheim Ice Fortress, which pays 293,759 XP; with it the quests reach level 26 with 157,945 XP to spare, before any XP lost to deaths.

Use Q2945 → Q2946 → Q2947 → Q2042. Fixed rewards total 643,502 XP and 31,320 Kinah. Fixed rewards alone project level 25, leaving 237,185 XP to 26 and 18,445,302 XP to level 32. Qualifying kills, recovery and later rewards add XP; this is a budget, not a played Abyss proof.

Q2947’s shipped handler uses the arena route and forces reward group 1; its old multi-route description is not authority for unimplemented branches. Q2042 is the Morheim six-ring timed flight course. Plan its actual flight-time budget, ordinary travel and failure/retry receipts; preserve D33’s own-timer guard.

The shipped NTC instance admits level 25 and up to six players. Vallack 800510 has portal dialog 10000 to map 300030000. The local portal definition does not itself enforce the complete Abyss chain; the operator’s required Abyss-entry contract still applies to each party member.

Keep the first coordinated proof around levels 25–27 and budget XP before further Morheim/Brusthonin questing. Level 32 is the operational ceiling requested here. Rewards depend on the NPC and the highest eligible nearby member; it is not a universal zero-XP/drop boundary. Java XPRewardEnum, for example, still assigns nonzero XP to level differences −4 through −10.

Each new class profile must create and ascend naturally, select its own class rewards and respect gear/book purchase and stigma limits. Templar and Sorcerer need their own supply, potion cooldown, rest, retreat and Kinah budgets. A Cleric healing branch cannot serve as their recovery policy. The existing Cooking/Alchemy plan is a separate supply goal.

Party work must prove one coordinated SIM clock, per-member quest and entry eligibility, individual/unshareable quest steps, invitations and following, role-aware pulls/defense/healing, shared XP/loot, gates and siege behavior, instance ownership/reentry, and regrouping after death. Existing party/loot/shared-credit scenarios provide foundations; the natural party controller and NTC proof are still open.

## Abyss entry: measured endpoint

Captured 2026-10-06 by AX-14 from `altgard-rc-complete-s1`, seed 1, run `ax14-capture-a2`, on runtime `37328409d9c5df12666bb1ea6a43dbd678aa98f3`. The leg, its decisions and its evidence are in [natural-abyss-entry.md](natural-abyss-entry.md). The plan and budget under "Next bounded contracts" above are as written before the leg was played.

| Measure | Value |
|---|---|
| Snapshot | `morheim-abyss-entry-s1`, character 133276, clock 74,054,143 ms |
| Level and XP | Cleric 26, 166,405 of 2,017,917 XP into the level |
| XP gained in the leg | 1,047,092: 1,038,632 from five quests, 8,460 from ten arena spirits |
| XP still needed | 1,851,512 to level 27; 18,041,712 to level 32 |
| Recoverable XP carried | 97,328, from deaths in the earlier legs. The death rule heals it after the next obelisk resurrection (22,911 Kinah at this amount) |
| Kinah | 743,158. The leg started with 748,485, paid 6,637 in fares and 2,690 for the bind, and was paid 4,000 by Q2947 |
| Quests | Q24020, Q2945, Q2946, Q2947 and Q2042 complete, each once. Q24021 to Q24026 are in the journal, locked |
| Place | Morheim Ice Fortress, beside Vebna; bound at obelisk 700231 |
| Worn | Elite Rank 7 Asmodian Staff 101500818 (470 magic boost); Morheim Dark Legionary Hauberk 110551147; Elite Rank 7 handguards, spaulders, chausses and brogans |
| In the cube | Altruist's Staff, the Altgard Dark Legionary Staff, the replaced armor, ten Greater Raging Wind Scrolls from Q2042, the sealed stigma bundle 188053787 |
| Supplied help | one Greater Raging Wind Scroll and 32 Bronze Coins, both approved for this leg |
| Deaths and attempts | no death; one arena try (ten spirits in 59 s) and one ring-course try (landed after 45 s) |
| Game time | 16 min 30 s for the leg |

Stigma and skills are as at the start: no stigma is socketed, no skill book was bought and the bundle is sealed.

Public Verify `ax14-verify-a1` restores this snapshot, checks the endpoint from the fresh login, relogs once more and finds the same endpoint. No owned schema is left afterwards, and the 39 earlier snapshot dumps keep their hashes.

What changed on the way, all logged in [the E2E plan](e2e-player-simulation-plan.md) and [the leg's document](natural-abyss-entry.md):

- **Server, under retail decisions:** Garm sends the player into a new arena (D35); a death fails the arena attempt at once and the arena has twelve spirits (D36); the two quest arenas revive a dead player inside (D38). D34 scoped Q2947's timer hook before the leg.
- **Bot rules, for every leg:** whenever the bot dies it revives at the obelisk and soul heals at the nearest Soul Healer, and in an instance it revives in the instance; the equipment check wears the owned staff with the most magic boost; the inventory check runs after every quest turn-in.
- **This leg:** the commander first; coin gear at each wearable tier, armor and staff, only where it beats what is worn, with supplied coins.

Not proven by a run: the Elyos arena's revive (no Elyos character), and the instance revive in Haramel (no death there since the rule).

## Endpoint verification and evidence

Public Verify `rc12-endpoint-verify-a2` restores and relogs this endpoint at level 25/176 alive. Initial restoration inventory exactly matches the capture. Native trace lines 77–78 explain the sole later inventory change: approved Awakening Scroll 164000133/143950 is used on AfterRelog, reducing 33 → 32 in the disposable clone. The immutable captured stack remains 33. All object identities, equipment, journal, completed counts, class, bind and Haramel receipts are retained.

Native non-profession timestamp flags advance for 88 ordinary skill entries, as Java PlayerSkillEntry.getDateLearned specifies; skill IDs/ranks/types and profession XP flags match. The four snapshot hashes match metadata, and all 27 historical dump hashes remain unchanged.

The initial Verify clone loads correctly but rejects a watchdog-clock rollback. The resume fix derives the original progress origin from the saved absolute observation and relative budget, covering both continuous and contained journeys. The passing clone advances the absolute clock 30,001 ms (20,000 ms restore offset plus 10,001 local ms); fingerprint and last-progress time are unchanged. Both owned Verify schemas are observed absent afterward. The original failure and comparison drafts are retained.

Required final Fast and pre-commit checks are recorded in the RC-12 evidence line in the working checklist.

- [Working checklist and decisions](natural-altgard-leveling.md#revised-journey-consolidation-rc-working-checklist).
- [Accepted full audit](../run/snapshots/_capture/rc11-full-create-s1-a25/audit.json), [stage ledger](../run/snapshots/_capture/rc11-full-create-s1-a25/stage-ledger.json), [death ledger](../run/snapshots/_capture/rc11-full-create-s1-a25/death-ledger.json), [consumable ledger](../run/snapshots/_capture/rc11-full-create-s1-a25/consumable-ledger.json), [cost ledger](../run/snapshots/_capture/rc11-full-create-s1-a25/cost-ledger.json), [outcomes](../run/snapshots/_capture/rc11-full-create-s1-a25/recorded-outcomes.json).
- [Immutable snapshot metadata](../run/snapshots/altgard-rc-complete-s1/snapshot.json), [endpoint comparison](../run/rc12/endpoint-verification.json), [readiness metrics](../run/rc12/readiness.json), [full native equipped slots](../run/rc12/native-equipped.json), [Verify schema provenance](../run/rc12/verify-schema-provenance-a2.json).
- Primary local spec: Java Q2945/Q2946/Q2947/Q2042, StigmaService, PlayerSkillEntry, StatFunctions and XPRewardEnum; shipped quest rewards, player_experience_table.xml, instance_cooltimes.xml, portal_template2.xml and world_maps.xml.

Q24114 remains intentionally excluded. Gathering quests Q2250/Q2275/Q2276/Q2297 and Q2147 remain deferred under the documented decisions; unresolved operator questions are preserved in the working checklist. No new server content, Abyss execution, additional class or party run is claimed for the `altgard-rc-complete-s1` endpoint. Abyss entry was executed afterwards by the AX leg; see [Abyss entry: measured endpoint](#abyss-entry-measured-endpoint).
