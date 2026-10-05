# Natural Altgard leveling leg

**2026-10-04 starting-route revision:** [OD-16](natural-ascension-altgard.md#operator-decisions)
now ascends at the first level 9, returns to finish Ishalgen, then enters Altgard.
AS-02 and the retained snapshots below record the earlier late Ascension order.
EA-02 tracks fresh proof of the revised Ishalgen/bridge segment; it does not replace
the historical full Altgard/Haramel acceptance evidence.
The revised segment's `ea02-create-s1-a2` SIM completes all 41 Ishalgen quests and the
bridge, entering Altgard at level 13 with zero recorded deaths and endpoint relog proved.

**2026-10-04 capital revision:** [PC-00..PC-08](natural-ascension-altgard.md#pandaemonium-capital-passes-approved-2026-10-04)
adds the ten-quest city/Convent pass immediately after Q2009, before retained Ishalgen
work. Its contained SIM and snapshots are proved at Cleric 11. The nine later additions
[scheduled by PC-08](natural-ascension-altgard.md#scheduled-additions-to-individual-legs-pc-08-2026-10-04)
are assigned to the individual legs below; their executors and segment proofs are tracked
by RC-00..RC-12. The completed historical TODOs and snapshots describe their original scopes.

**Next working checklist (2026-10-04):** [RC-00..RC-12](#revised-journey-consolidation-rc-working-checklist)
turns that existing schedule into contained implementation items followed by one revised
continuous SIM. This is the bounded Option A consolidation; Abyss access, other classes,
crafting and NTC remain subsequent contained legs.

**Status (2026-09-29): the list, the order and AL-1..AL-5 are approved**, with the
recommendations and the operator's notes below.

The zone is worked in **sub-legs**. The first one is
[Leg 1: Altgard Fortress, level 10–12](#leg-1-altgard-fortress-level-1012). It has its own
TODO list, worked in Loop mode like [the Ascension bridge](natural-ascension-altgard.md).

**Leg 1 is done (AF-00..AF-10, 2026-09-29).** [Leg 2: Moslan Crossroad](#leg-2-moslan-crossroad-level-1315--proposal) is done (AM-01..AM-09, 2026-09-29): `altgard-l2` starts Leg 3.
[Leg 3: Manir's Campsite and Dock](#leg-3-manirs-campsite-and-dock-level-15--proposal) is done (AC-00..AC-08,
2026-09-30): `altgard-l3` starts Leg 4 at Basfelt. The isolated Q2217 correction
[AC-C1..AC-C2](#leg-3-correction-q2217-approved-2026-10-03) is complete (2026-10-03):
`altgard-l3-q2217` includes the delivery; historical downstream snapshots omit it.
[Leg 4: Basfelt Village](#leg-4-basfelt-village-level-1617--proposal) is done (AB-01..AB-10, 2026-09-30; recaptured
2026-10-01): `altgard-l4` starts Leg 5.
[Leg 5: Kaibech's Campsite, Idun's Lake and MuMu Village](#leg-5-kaibechs-campsite-iduns-lake-and-mumu-village-level-1819--proposal)
is done (AK-00..AK-10, 2026-10-01): `altgard-l5` starts Leg 6.
[Leg 6: Gerger Village and Trader's Berth](#leg-6-gerger-village-and-traders-berth-level-20--proposal) is done
(AG-00..AG-09, 2026-10-02): `altgard-l6` starts Leg 7.
[Leg 7: Altgard Fortress and the East Gate](#leg-7-altgard-fortress-and-the-east-gate-level-21--proposal) is done
(AE-00..AE-08, 2026-10-02; AE-Q1..AE-Q3 as recommended): `altgard-l7` starts Leg 8 at the fortress.
[Leg 8: Altgard Observatory and Mahindel Swamp](#leg-8-altgard-observatory-and-mahindel-swamp-level-21)
is done (AO-00..AO-06, 2026-10-02): `altgard-l8` starts Leg 9 at the Observatory.
[Leg 9: Heart of Impetusium](#leg-9-heart-of-impetusium-level-21) is done (AH-00..AH-06, 2026-10-02):
`altgard-l9` starts Leg 10, the Black Claw ground, with a level-22 Cleric bound at the Heart.
[Leg 10: Black Claw ground and the remaining campaigns](#leg-10-black-claw-ground-and-the-remaining-campaigns-level-22)
is done (BC-00..BC-08, 2026-10-02): `altgard-l10` holds the level-24 Cleric alive and bound at the fortress,
all nine quests complete. Q2900 is completed in Leg 11 below; end-of-area acceptance follows later.
[Leg 11: No Escaping Destiny](#leg-11-no-escaping-destiny-and-the-altgard-endpoint-level-24--proposal)
is done (ND-00..ND-08, 2026-10-03): `altgard-l11` holds the relog-verified level-24 Cleric alive and bound
at the fortress, Q2900 complete, 144 completed journals and the stigma reward bundle sealed.
The operator's 2026-10-03 next request is [coin gear preparation](natural-altgard-coin-gear.md)
(CG-00..CG-06), then [Leg 12: Haramel](natural-altgard-haramel.md) (HM-00..HM-08).
Coin preparation is done (CG-00..CG-06, 2026-10-03): verified `altgard-coingear` holds the level-24
Cleric alive at Heart bind, 145 journals, 19 Iron Coins, three purchased chain pieces and the original
staff still equipped. Haramel scope/eligibility, shared contract, travel, first-clear quest protocols
and normal combat/class loot/recovery are complete (HM-00..HM-05; HM-06..HM-08 unchecked). Full-area SIM and isolated LIVE
acceptance follow those checkpoints.

## Goal

Take the level 10 Cleric from the bridge endpoint (bound at Altgard Fortress) through
Altgard's quests to the end of the zone, the way a player would:
- work one quest hub at a time, starting with the fortress, the main town;
- pick up every approved quest at a hub as soon as its level and prerequisites allow;
- come back for the ones that open later.

It starts from the saved snapshots, not from a new character:
- SIM: `sim-snapshot.ps1 -Action Restore -Name altgard`;
- LIVE: `run/na27-live/na27-live-a3/altgard-live-dump.sql.gz`.

It follows the same rules as the bridge: the Java source is the spec, no server content is
added, each step is proved once, and deaths are recorded but are not failures (OD-12).

## How the list was built

The data comes from:
- `parity-artifacts/e2e/obtainable-quests.json` (the checked-in availability classifier);
- `scripts/e2e/compile-quest-plans.py --zone Altgard` (gates, prerequisites, template
  NPCs and rewards);
- the custom C# handlers under `src/Aion.GameServer/Handlers/Quest/altgard/` (start NPCs,
  talk and kill events, item use, timers, escorts);
- the spawn, zone, NPC, item, gatherable and goods-list data.

C# matches Java for these handlers, since they are ports.

Altgard has **141 Asmodian quests**:
- **107 are obtainable** under the classifier;
- 20 have no handler;
- 12 are disabled (level 99 or event quests);
- 2 are unreachable.

Of the 107, this proposal:
- **includes 85**, among them the Altgard campaign missions Q24011–Q24013;
- lists 4 [Group] quests in the route, pending AL-2;
- asks you about 8 more: the 4 [Gather] quests, Q2147, and the level 20 missions
  Q24014–Q24016;
- excludes the other 10, with the reasons below.

That makes 89 quests in the route tables.

## Decisions (approved 2026-09-29)

The operator approved every recommendation. Their notes:
- **AL-3:** gathering must be levelled later. A gathering-training leg comes back for the four
  [Gather] quests (Essencetapping 45 to 85).
- **AL-4:** asked whether Lohaban is near the main town. **He is not.** Lohaban (203689) and
  Lateni (203659, the starter of Q2147 and of [Coin] Q2293) stand together in the **Heart of
  Impetusium** at (2665, 1660), about 1 km east of the fortress. Coins only matter at that
  stop. Q2147 stays skipped, and can be looked at again at Stop 12.
- **Flight** is a mechanic the bot does not have yet; see [Flight in Altgard](#flight-in-altgard).
- **Q24114 (2026-10-03):** the operator chose not to implement "You Gotta Stop Umkata".
  It is intentionally excluded and no longer an open Altgard blocker. Its 4.8 retail evidence remains valid.

| # | Question | Options | Decided |
|---|---|---|---|
| AL-1 | **Where does the leg end?** | (a) when every approved quest is done, at whatever level that is. Quest rewards total about 1.75M XP for the core list, so kills included the Cleric lands around **level 18–19**. (b) (a), then hunt to **level 20**. (c) (b), plus the level 20 missions Q24014–Q24016, which need the **Haramel** (300200000) and **Bregirun** (320030000) instances. | **(b)**: finish the zone, hunt to level 20; the instances start the next leg. |
| AL-2 | **[Group] quests** Q2277, Q2280, Q2281, Q2282. Their targets are EXPERT-rank "brutal Black Claw" monsters, level 16–20, with 2.7k–4.6k HP; ordinary SEASONED monsters here have 2.3k–3.1k. Q2283 "Report to Pandaemonium" needs Q2282. | include solo, or skip | **Include**, solo, at the target's level or above. Deaths are recorded. |
| AL-3 | **[Gather] quests** Q2250 Kandula (Essencetapping 45), Q2275 Krimer (55), Q2276 Horto (75), Q2297 Blicora (85). The bot has Essencetapping of about 15 from Ishalgen, so this means a gathering grind. | skip all; include Q2250 and Q2275 (grind to 55); include all four (grind to 85) | **Skip for now; come back.** A gathering-training leg (Essencetapping to 85) will do them. |
| AL-4 | **Q2147 "Treasure Seek"** needs Lohaban's Treasure, which only Lohaban (203689), a *reward* vendor paid in quest coins, sells. The only coin quest here is the repeatable [Coin] Q2293. | skip, or add a coin loop | **Skip.** Q2146 is still included. Lohaban is at Heart of Impetusium, not the main town. |
| AL-5 | **Pandaemonium trips:** Q2258 (Lindhelm), Q2278 (Cavalorn, Balder) and Q2283 (Vidar) need NPCs in Pandaemonium. | include (take the teleporter, which costs Kinah), or skip | **Include**, using the bridge's teleporter code. |

## Flight in Altgard

Flight is needed from the very first sub-leg:
- **Q24011 "Funny Floating Fungus"** (campaign, level 11) has you talk to **Borender**
  (203572), who stands on a floating rock at **z 406** above the fortress, whose floor is about
  z 254. You then kill five **Abyss Fungus** (700092), which float at **z 367–400** over the
  fortress (12 spawns). The handler counts kills from var 2 to its reward at var 6; it also
  counts a kill at var 1, which would skip Borender, so the bot talks to him first.
- **Q2209 "The Scribbler"** also has a Borender step (but see AF-Q1).

What the Java spec says (4.8, `ce54b7931`; C# matches):
- **Where:** Altgard's map and every ordinary area forbid flying. Areas use flags 55, which is
  bind, recall, glide, ride and fly-ride, with no FLY bit. Only **two FLY zones** exist:
  - `DF1_FZ_VERTERRON`, around the fortress (x 1392–1924, y 1569–2032, z 240–440);
  - `DF1A_FZ_TOWN3`, around Heart of Impetusium (x 2401–2818, y 1455–1873, z 200–440).
- **Taking off** (`CM_EMOTION` FLY → `FlyController.startFly`): the character must be a Daeva,
  inside a FLY zone and not a NO_FLY zone, with no NOFLY effect, no transform and no private
  store. There is a **10 s reuse cooldown** (`FLY_REUSE_TIME`).
- **Water:** the server has no water check. Refusing to take off from water is the
  **client's** rule, so the bot must copy it: never take off from a navmesh Water area or
  below the map water level (z 200).
- **Flight time (FP):** a level 10 Cleric has **60** (`maxFp` 60, observed). Flying drains 1
  per tick inside a FLY zone and 2 outside (`PlayerLifeStats.triggerFpReduce`). At 0 the
  server ends the flight (`LifeStatsRestoreService`) and the character falls. Over the
  fortress a fall is about 150 m. FP refills on the ground.
- **Leaving the FLY zone while flying** ends the flight (`FlyZoneInstance.onLeave` →
  `onLeaveFlyArea`).

So the bot needs an FP budget: take off from dry ground inside the zone, fly, and land
(on the ground or on Borender's rock) with a safety margin. Air fights against the fungus
have to fit inside that budget: kill one or two, land, let FP refill, and repeat.

## Proposed quest list and order

The route goes hub by hub. Within a hub the bot takes every approved quest it is eligible
for. Anything gated by level or a prerequisite is picked up on a later pass. The listed
order is a plan, not a script: at run time the decision engine chooses from the client's
view, as in Ishalgen. Levels are the shipped `minlevel_permitted`. "After" means another
quest must be finished first.

**The D32 quests (2026-10-01).** D32 (`docs/retail-quest-completion.md`, RQ-05) added six Altgard quests that 4.8 retail ran and Java lacks. They are marked **D32** in the stops below:
- Q24110 (Stop 1) and Q24111 (Stops 2–3) belonged to legs that were already done. They were added to Leg 1 and Leg 3, and Legs 1–4 were replayed from the `altgard` snapshot (the catch-up).
- Q24232 and Q24233 are in Leg 5.
- Q24115 (Stop 8) and Q24113 (Stop 10) wait for their legs.
- Q24114 is intentionally excluded: the maintainer chose not to implement it (2026-10-03; RQ-05).

### Stop 1: Altgard Fortress, the main town (arrive at level 10)

| Quest | Name | Level | Starter | After | Objective | XP |
|---|---|---|---|---|---|---|
| 2201 | The Crasaur Threat | 10 | Heidunn | | kill 10 ice crasaurs (L10–11) | 14,700 |
| 2202 | The Lobnite Problem | 10 | Morn | | kill 10 tentacled lobnites (L9–10) | 13,185 |
| 2203 | Ice Lake Crystals | 10 | Nemnef | | collect 6 Altgard Crystals | 5,715 |
| 2204 | Sparkie Sap Polish | 10 | Nirmirn | | collect 5 Sparkie Sap | 10,080 |
| 2205 | The Way to His Heart | 10 | Venoa | | collect 5 Airon Meat | 9,315 |
| 2206 | Slinking About | 10 | Tulberg | | collect 5 Slink Tails | 6,015 |
| 2209 | The Scribbler | 10 | Thrud | | talk: Thrud → Tulberg → Noroia | 9,780 |
| 2207 | Conversing With a Skurv | 11 | Emgata | | talk: Emgata → Itu → Suthran | 15,635 |
| 2208 | Mau in Ten Minutes a Day | 11 | Itu | 2207 | use the Mau Secret Remedy, talk to Mumu Bon | 16,283 |
| 2210 | Retrieving the Report | 12 | Rion | | deliver to Loriniah (Moslan Crossroad) | 16,283 |
| 24011 | Funny Floating Fungus (campaign) | 11 | auto | 24010 | kill Abyss Fungus | 31,309 |
| 24110 | Control Altgard, Delete Revolutions (**D32**) | 12 | Meiyer | | kill 4 secret agents and goons (L9–10), west of the fortress | 16,283 |

### Stop 2: Moslan Crossroad (level 11–12)

| Quest | Name | Level | Starter | After | Objective | XP |
|---|---|---|---|---|---|---|
| 2211 | Karnif Threat | 11 | Olenja | | kill 10 needletail karnifs (L10–11) | 16,950 |
| 2212 | A Better Trap | 11 | Olenja | 2211 | collect 6 Tog Ribs | 11,895 |
| 2215 | A Long-Lost Friend | 11 | Olenja | 2212 | deliver to Manir (Manir's Campsite) | 8,355 |
| 2213 | Poison Root, Potent Fruit | 11 | Tigg | | talk to the Okaru Tree (it poisons you), back to Tigg | 7,335 |
| 2218 | Frightcorn Seeds | 11 | Tigg | 2213 | collect 5 Frightcorn Seeds (MuMu farmers) | 15,750 |
| 2219 | Ripened Frightcorn | 11 | Tigg | 2218 | collect 3 Ripened Frightcorn | 10,605 |
| 2214 | No-Frills Quills | 11 | Loriniah | | collect 5 Pluma Feathers | 10,335 |
| 2220 | Picking off Frightcorn | 12 | Tigg | | kill 5 MuMu patrols (L11–13) | 16,283 |
| 24012 | An Ominous Crop (campaign) | 12 | auto | 24011 | Loriniah, the MuMu cart; collect hairpins and waist bands | 36,324 |

### Stop 3: Manir's Campsite and Dock (level 12)

| Quest | Name | Level | Starter | After | Objective | XP |
|---|---|---|---|---|---|---|
| 2221 | Manir's Uncle | 12 | Manir | | Groken, Groken's Safe | 8,910 |
| 2290 | Groken's Escape | 12 | Groken | 2221 | **escort** Groken to his boat, back to Manir | 21,300 |
| 2222 | Manir's Message | 12 | Manir | 2290 | talk: Manir → Karl → Nokir | 13,080 |
| 24111 | What's Up, Dock? (**D32**) | 10 | Olenja (Moslan Crossroad) | | the Lepharist Primer from the suspicious document at Manir's Dock; to Nokir (Basfelt) | 2,691 |
| 2217 | Gleaning the Meaning (**IMPORTANT**) | 12 | Olenja (Moslan Crossroad) | 24012 **or** legacy 2013 | deliver Olenja's Letter to Gefion (Basfelt); included in the Leg 3 correction approved 2026-10-03 | 17,678 |

### Stop 4: Basfelt Village (level 12–14)

| Quest | Name | Level | Starter | After | Objective | XP |
|---|---|---|---|---|---|---|
| 2225 | No-Good Slime | 12 | Hunmir | | kill 5 rainbow slimes (L14) | 19,950 |
| 2231 | Sibling Rivalry | 12 | Lamir | | talk: Karl, Gunmarson, Kaibech | 30,450 |
| 2232 | The Broken Honey Jar | 12 | Gilungk | | collect 9 Beehives (Tatural) | 48,600 |
| 2239 | Malodor Antidote | 12 | Gilungk | | collect 3 Ampha Membranes (L15–16), take to Vovetirn | 12,810 |
| 2288 | Money Where Your Mouth Is | 12 | Shania | | **timed** mosbear hunt (L13–15) | 23,700 |
| 2230 | A Friendly Wager | 12 | Shania | 2288 | **timed**: collect 10 Mosbear Tusks | 37,200 |
| 2289 | Rampaging Mosbears | 13 | Gefion | 2288 | mosbears, then Komu Silverclaw (L17): Komu's Horn | 43,350 |
| 2224 | Lamir's New Clothes | 13 | Lamir | | collect 6 Mosbear's Leather (cubs L13–15) | 17,678 |
| 2226 | A Cure for Crazy | 13 | Garuntat | | deliver to Gornak at Idun's Lake | 17,678 |
| 2223 | A Mythical Monster | 12 | Gefion | | talk to Lamir (D27 correction applied), burn the incense: Infernus (EXPERT, L13) spawns for 5 min; kill it | 23,250 |
| 24112 | No Laissez-faire for Lepharists | 14 | Nokir | | kill Comrade Sumarhon (SEASONED, L15), report to Brodir | 17,552 |

### Stop 5: Kaibech's Campsite and Gribade Canyon (level 13–16)

| Quest | Name | Level | Starter | After | Objective | XP |
|---|---|---|---|---|---|---|
| 2233 | Mantigar's Request | 13 | Mantigar | | kill 3 crested plumas (L15–16) | 8,010 |
| 2234 | An Irritating Problem | 13 | Mantigar | 2233 | kill 5 nimble arachnas (L15–16) | 11,850 |
| 2235 | Clearing the Path | 13 | Kaibech | 2231 | kill 3 bigfoot mosbears (L16) | 7,920 |
| 2241 | Glowing Mushroom | 13 | Vovetirn | | collect 5 Glowing Mushrooms (sprigg outlaws L15–16) | 19,350 |
| 2242 | A Nice Gesture | 13 | Vovetirn | 2241 | deliver to Gemyu (Gerger Village) | 16,350 |

### Stop 6: Idun's Lake (level 13–16)

| Quest | Name | Level | Starter | After | Objective | XP |
|---|---|---|---|---|---|---|
| 2227 | A Crazy Request | 13 | Gornak | 2226 | collect 3 Spirit Crystals (lake spirits L13) | 17,678 |
| 2291 | Report to Garuntat | 13 | Gornak | 2227 | deliver to Garuntat (Basfelt) | 17,678 |
| 24230 | A Grave Situation | 14 | Brodir | | kill 9 grave robbing sentries (L14–15) | 17,552 |
| 24231 | Take Back, Sword! | 14 | Brodir | | collect 8 Amalgam Swords (fencers L14–15) | 17,552 |
| 2236 | Rarified Tastes | 14 | Anmurnerk | 24112 | collect 5 MuMu Hairpins (L15–16) | 16,495 |
| 2237 | A Fertile Field | 14 | Anmurnerk | 24112 | collect 3 Fertilizer Sacks | 16,495 |
| 2238 | A Matter of Pride | 14 | Brodir | 24112 | collect 5 MuMu Belts (L15–16) | 16,495 |
| 2292 | Making a New Start | 14 | Anmurnerk | 24112 | collect the Passion, Jealousy and Love Rings (MuMu L16) | 16,495 |
| 24013 | Poison In the Waters (campaign) | 14 | auto | 24012 | Nokir, Shania; use the Hunter's Poison | 55,537 |
| 24232 | Little Help from a Daeva (**D32**) | 14 | Anmurnerk | 24112 | kill 9 MuMu looklooks or lookouts | 17,552 |
| 24233 | Adieu to You, Manumumu (**D32**) | 14 | Brodir | 24112 | kill Chieftain Manumumu (L17); to Suthran (fortress) | 16,495 |

### Stop 7: Gerger Village (level 14–17)

| Quest | Name | Level | Starter | After | Objective | XP |
|---|---|---|---|---|---|---|
| 2244 | A Drinking Problem | 14 | Gercus | | kill 5 green ribbits (L16–17) | 16,050 |
| 2245 | Ksellid Control | 14 | Gemyu | | kill 5 star metal ksellids (L16) | 17,550 |
| 2248 | The Secret Letter | 14 | Gemyu | 2245 | deliver to Neparinerk (Trader's Berth) | 13,095 |
| 2246 | The Gerger's Insignia | 14 | Germir | | collect the Gerger Insignia | 6,000 |
| 2247 | The Gerger's Disguise | 14 | Germir | 2246 | talk to Gogaerunerk (a disguise) | 16,050 |
| 2284 | Escaping Asmodae | 14 | Germir | 2247 | **escort** the disguised Germir to Babarunerk (Trader's Berth) | 50,130 |

### Stop 8: Trader's Berth (level 14–18)

| Quest | Name | Level | Starter | After | Objective | XP |
|---|---|---|---|---|---|---|
| 2249 | The Blue Crystals | 14 | Neparinerk | | collect 5 Angolem Crystals (L17) | 21,300 |
| 2251 | Securing the Trade Route | 14 | Babarunerk | | collect 5 Pecku Tail Feathers (L18) | 22,350 |
| 2262 | A Sneaky Delivery | 14 | Japayerk | | deliver to Mabrunerk (East Gate) | 45,750 |
| 2252 | Chasing the Legend | 14 | Sinood | | the Bone of Minushan spawns Minushan's Spirit or Drakie (L17–18); kill it | 45,255 |
| 24115 | A Shugo Apropos (**D32**) | 13 | Neparinerk | | kill 3 crimsontail amphas (L17–18); to Banatisai (Heart of Impetusium, Stop 12) | 16,066 |

### Stop 9: Altgard East Gate (level 14–17)

| Quest | Name | Level | Starter | After | Objective | XP |
|---|---|---|---|---|---|---|
| 2263 | Shugo Potion | 14 | Mabrunerk | | **timed**: collect 3 Malodor Pollen (malodors L16–17) | 27,750 |
| 2264 | The Sting of Poison | 14 | Eggther | | kill 3 poison arachnas (L16–17) | 15,075 |
| 2265 | A Lost Sword | 14 | Dellalont | | recover the Archon Sword (Black Claw warriors, SEASONED L16–17) | 31,050 |
| 2146 | Pass the Message | 16 | Eggther | | deliver to Lateni | 7,500 |

### Stop 10: Mahindel Swamp and the Altgard Observatory (level 15–18)

| Quest | Name | Level | Starter | After | Objective | XP |
|---|---|---|---|---|---|---|
| 2272 | The Lost Cube | 15 | Taora | | recover Taora's Cube (Black Claw sleekpaw, SEASONED L16) | 14,625 |
| 2273 | Emergency Rations | 15 | Taora | 2272 | collect 3 Emergency Provisions (veteran MuMus, SEASONED L17) | 14,220 |
| 2267 | A Monster in a Box | 15 | Urnir | | kill 5 big cargo boxes (L17) | 26,370 |
| 2268 | Urnir's Reasoning | 15 | Urnir | | kill 5 longnecked peckus (L17–18) | 25,335 |
| 2269 | Neifenmer's Reasoning | 15 | Neifenmer | | collect the Lepharists' Operation Order | 11,265 |
| 2270 | Creating a Delay | 15 | Neifenmer | 2269 | collect 3 Lepharist Insignia (watch L17–18) | 25,800 |
| 2271 | Aurtri's Letter | 15 | Neifenmer | 2270 | talk: Aurtri → Suthran | 23,100 |
| 24113 | Sword to Secrecy (**D32**) | 12 | Aurtri | 24112 | Tiamat's Sword from Commander Gattban (L18) | 16,066 |

### Stop 11: back to Altgard Fortress, and to Pandaemonium (level 15–20)

| Quest | Name | Level | Starter | After | Objective | XP |
|---|---|---|---|---|---|---|
| 2243 | A Crystal Hand Mirror | 15 | Chagarinerk | | deliver to Kagorinerk (Trader's Berth) | 4,305 |
| 2253 | Kagorinerk's Gift | 14 | Kagorinerk | 2243 | deliver back to Chagarinerk | 3,810 |
| 2261 | Failure to Report | 15 | Meiyer | | deliver to Dellalont (East Gate) | 5,070 |
| 2266 | A Trustworthy Messenger | 16 | Valurion | | talk: Neifenmer → Aurtri | 27,630 |
| 2278 | A Secret Proposal | 16 | Emgata | 2208 | talk: Suthran, **Pandaemonium** (Cavalorn, Balder) | 48,600 |
| 2279 | Solid Proof | 16 | Suthran | 2278 | talk: Emgata, Zemurru's Spirit | 38,100 |
| 2286 | The Brigade General's Order | 16 | Suthran | 2279 | deliver to Morn | 4,785 |
| 2287 | Order to Valurion | 16 | Suthran | 2279 | deliver to Valurion | 6,405 |
| 2277 | [Group] A Lucrative Endeavor | 16 | Nemnef | | collect 10 Mau Feather Trinkets (brutal hunters, EXPERT L16–17) — AL-2 | 39,600 |
| 2280 | [Group] Killing the Shamans | 16 | Morn | 2286 | kill 4 brutal spellshifters (EXPERT L17–18) — AL-2 | 33,690 |
| 2281 | [Group] Finishing off Fighters | 16 | Valurion | 2287 | kill 3 seekers, 5 bodyguards, 5 scratchers (EXPERT L18–19) — AL-2 | 83,400 |
| 2282 | [Group] Killing Kumbaron's Followers | 16 | Suthran | 2279 | kill 3 brutal warlocks (EXPERT L19–20) — AL-2 | 66,150 |
| 2283 | Report to Pandaemonium | 16 | Suthran | 2282 | deliver to Vidar in **Pandaemonium** | 36,000 |

### Stop 12: Heart of Impetusium (level 16–20)

| Quest | Name | Level | Starter | After | Objective | XP |
|---|---|---|---|---|---|---|
| 2254 | Soul Essence | 16 | Andgar | | collect 5 Soul Essence (hero and sorcerer spirits L18–19) | 30,720 |
| 2255 | Turbulent Mist Spirits | 16 | Grak | | kill 3 mist spirits (L18) | 14,880 |
| 2256 | Turbulent Splash Spirits | 16 | Grak | 2255 | kill 5 splash spirits (L18–19) | 24,720 |
| 2258 | An Important Announcement | 16 | Grak | 2256 | deliver to Lindhelm in **Pandaemonium** | 53,850 |
| 2257 | Rebuilding the Pillar | 16 | Banatisai | | collect 9 Metallic Fragments | 19,500 |
| 2259 | The Tayga Threat | 16 | Gulkalla | | collect the Wild Tayga Fang (L18) | 36,630 |
| 2260 | Reconstruction Supplies | 16 | Gulkalla | 2259 | deliver to Suthran (fortress) | 51,000 |

### Asked separately (not in the counts above)

| Quest | Name | Level | Why it needs your decision |
|---|---|---|---|
| 2250 | [Gather] Gathering Kandula | 13 | Essencetapping 45 (AL-3) |
| 2275, 2276 | [Gather] A Change of Diet, Branching out Further | 15 | Essencetapping 55 and 75 (AL-3) |
| 2297 | [Gather] Disgruntled Workers | 16 | Essencetapping 85 (AL-3) |
| 2147 | Treasure Seek | 16 | needs a coin-vendor item (AL-4) |
| 24014–24016 | the level 20 campaign missions | 20 | the Haramel and Bregirun instances (AL-1) |

## Excluded, with the evidence

| Quest | Name | Why |
|---|---|---|
| 2144, 2145 | [Manastone Bundle] Cook the Books, Cheap Crates | **Manastone** exchanges: they need manastones (167000258/260/265) that no source gives. Repeatable 255 times. |
| 2298, 2299 | [Manastone] Lakutu's Exchange, Lakutu's Trade | **Manastone** exchanges, repeatable 255 times, same problem. |
| 2216 | Knot Your Average Message | Starts from a random drop: the MuMu Grass Knot drops at 3% from MuMu Farmers ("Named Quest-Start Item" rule). Excluded as in Ishalgen; the drop is left alone. |
| 2228, 2274, 2296 | A Thorn in Its Side, Black Claw Baton, A Bill Found in a Box | Each starts from an item (the Tanning Knife, Chieftain's Baton, A Bill Found in a Box) that **nothing** in Java or C# produces. Unobtainable, like Ishalgen's Q2107. |
| 2293 | [Coin] Mutated Spirits | Repeatable 255 times and gives no XP (a coin loop). |
| 2285 | Leinolz's Request | Unreachable: Q2240 is disabled at level 99. Q2217's former exclusion was a classifier defect: Java accepts either Q24012 or legacy Q2013; the Leg 3 correction below includes it. |
| 2229, 2240, 2294, 2295, 80172–80210 | | Disabled: level 99 gate. |
| 80005, 80142, 80143, 80262 | | Event quests. |
| 2011–2022, 2200 | | No handler in Java or C#. Superseded pre-4.0 missions. |
| 24114 | You Gotta Stop Umkata | Live in 4.8 retail, but missing its custom handler and Umkata summon. **Intentionally excluded by the maintainer on 2026-10-03: do not implement.** RQ-05's proposal is declined; this is no longer an open Altgard blocker. |
| 24010 | Suthran's Orders | Already done by the bridge. |

## What the bot has to learn for this leg

Taken from the list above and from NA-23's findings. After approval these become the
TODO items.

1. **The quest-hub engine for Altgard.** A route planner over hubs driven by the approved
   list, like the Ishalgen decision engine, with level gates, prerequisites and "come back
   later". The zone becomes the journey contract.
2. **Leaving the fortress.** NA-23 found that a navmesh route from the obelisk to open
   ground is `GeometryRejected`, probably at the gate. This has to be solved first, because
   every hub except the fortress lies outside.
3. **New quest mechanics:**
   - escorts (Q2290, Q2284);
   - timed quests (Q2230, Q2263, Q2288);
   - spawned targets (Q2223 Infernus, Q2252 Minushan);
   - item use (Q2208 Mau Secret Remedy, Q24013 Hunter's Poison);
   - a poison effect (Q2213);
   - automatic campaign missions (Q24011–Q24013);
   - Pandaemonium round trips (Q2258, Q2278, Q2283).
4. **Cleric fighting at level 12–20.** This includes SEASONED and EXPERT targets, the
   first real use of the Anti-Shock shield, Salvation and the serums (NA-23 could not
   trigger them), and the patrol wait tuned for helpers that do not move (an NA-23 finding).
5. **Level 12–20 skills and gear.** New Cleric skills as they are learned, gear upgrades
   from quest rewards, the help-item bands at 20 (OD-13), and vendor restocks at hubs.
6. **Acceptance** once, as before: one SIM run from the `altgard` snapshot, the full
   checklist, then one isolated LIVE run from the LIVE dump.

## Leg 1: Altgard Fortress (level 10–12)

**PC-08 scheduled addition:** on the first eligible fortress visit, pick up Q2917
at Arekedil 203574 and visit Chauminerk 798029 to START/1. Protect supplied 182207008
until Lanse/Annemari's city half during Leg 5 preparation. Its Q2918 box delivery
returns to Arekedil afterward. Keep the original leg endpoint and prove this addition
in its own contained Leg 1 segment; the historical contract does not yet include it.

RC-02 proves this opt-in continuation in `altgard-rc-l1-a2` (2026-10-04): normal pickup,
START/1, supplied item retained through capture/restore/relog. Historical Leg 1 scopes stay frozen.

The operator's current focus is the fortress quests. **The bot does not go south
for Q2210 or Q24012 in this leg** (AF-Q2, decided 2026-09-29); they move to Leg 2 at Moslan
Crossroad. The fortress quests' targets on the Ice Lake around the fortress (mostly west of
it) count as the fortress area. It starts from the `altgard` snapshot, a level 10 Cleric bound at the fortress.

| Quest | Name | Level | Where it is done | Notes |
|---|---|---|---|---|
| 2201–2206 | Crasaur, Lobnite, Crystals, Sparkie Sap, Airon, Slink | 10 | fortress NPCs; targets on the **Ice Lake** around it, mostly west (z ≈ 247) | The first thing the bot needs is a way **out of the fortress** (NA-23: the route out was GeometryRejected). |
| 2207 | Conversing With a Skurv | 11 | fortress (Emgata, Itu, Suthran) | |
| 2208 | Mau in Ten Minutes a Day | 11 | Itu; **Mumu Bon in the Fortress Dungeon (z 203)** | Needs the dungeon route and the Mau Secret Remedy item use. |
| 2209 | The Scribbler | 10 | Thrud, Tulberg, **Borender (z 406, flight)**, **Noroia (dungeon, z 205)** | Needs the D26 correction (AF-Q1, approved): Java never registers Borender. |
| 24011 | Funny Floating Fungus (campaign) | 11 | Valurion, **Borender**, **5 Abyss Fungus in the air** | Flight. |
| ~~2210~~ | Retrieving the Report | 12 | Rion → Loriniah at Moslan Crossroad | **Moved to Leg 2** (AF-Q2). |
| ~~24012~~ | An Ominous Crop (campaign) | 12 | Moslan Crossroad and MuMu Farmland | **Moved to Leg 2** (AF-Q2). |

**Endpoint:** Q2201–Q2209 and Q24011 completed, back in the fortress, alive. These quests
give about 132k XP; with kills the Cleric should end around level 11–12. Leg 1 sets no level
target.

### Leg 1 TODO list

Each item is worked like the bridge's items: read the Java spec first, do only that item,
verify it once, commit it on main, never push (the [loop
protocol](natural-ascension-altgard.md#how-to-work-this-list-loop-protocol) applies,
with "AF" in place of "NA").

- [x] **AF-00 — The Q2209 correction (D26).** Apply the fix the upstream report prepared
  (`docs/upstream-reports/q2209-the-scribbler.patch`) to the C# handler, with a regression
  test that a registered Borender dialog advances var 1 → 2. Record it as a shared-defect
  correction.
  - *Done 2026-09-29.* `_2209TheScribbler.Register` adds Borender's talk event. The SIM test
    `Q2209BorenderIsRegisteredAndAdvancesTheScribbler` (Q24011 COMPLETE, Q2209 at var 1, on
    Borender's rock) passes: the click opens page 10, Q2209 shows page 1693, and SETPRO2 sets
    var 2 (`run/af00/af00-sim.log`). Without the fix it fails with page 1011, his default
    chat (`run/af00/af00-sim-without-fix.log`). The warning, logger, clock-read and fidelity
    checks pass; `run-fast.ps1` passed 24, skipped 3 (`run/af00/run-fast.log`). Recorded as
    deviation 57 in `docs/e2e-player-simulation-plan.md`.
- [x] **AF-01 — The Leg 1 contract and quest plans.** Write
  `parity-artifacts/e2e/natural-altgard-contract.json`: the Leg 1 quests, their order, level
  gates, hubs and endpoint. Compile the template quests' plans (`compile-quest-plans.py`)
  into `parity-artifacts/e2e/natural-altgard-plans/`. Add a loader and contract tests like
  NA-01's.
  - *Done 2026-09-29.* The contract holds the ten quests and their order, the fortress hub
    (160 m around the obelisk), four areas (Ice Lake, Fortress Dungeon, Borender's rock, the
    fungus air), the 15 scripted dialog steps of Q2207/2208/2209/24011, Q2208's remedy use,
    Q24011's air kills and reward (the Cleric's Altgard Legionary Brogans, 114501726), the
    two FLY zones, and the endpoint. `compile-quest-plans.py --zone Altgard --runnable-only`
    produced the six template plans (Q2201–2206). Loader `NaturalAltgardContract`; five
    `NaturalAltgardContractTests` pass against quest data, spawns, zones, world maps, config
    and the C# handlers, and all 291 Natural and plan tests pass. Findings: the level 10
    quests alone reach level 11; all ten quests give 132,017 XP, short of level 12; the Ice
    Lake targets surround the fortress, not only the west side.
- [x] **AF-02 — Leave and re-enter the fortress on foot.** Diagnose NA-23's
  `GeometryRejected` route (obelisk → Ice Lake / open ground): a gate, a static door, or a
  navmesh/geo mismatch. Fix it bot-side, with checked navigation data, never by editing
  generated nav data by hand. **Done when:** a SIM probe walks from the obelisk to the Ice
  Lake targets and back.
  - *Done 2026-09-29. No gate, no navigation change.* NA-23's failed route started on the
    obelisk's own spot, which is inside its collision (NA-04 finding b). From the ground 3 m
    beside it, routes leave the fortress. The bot never starts there: binding uses
    interaction routing, and a revive puts the player where they bound.
  - SIM probe `AltgardFortressExitWalksToTheIceLakeTargetsAndBack` (`run/af02/af02-sim.log`):
    it starts beside the obelisk and routes on the live server's geometry to the nearest spawn
    of each of the 11 Leg 1 hunt targets (Q2201–2206). It walks out and back: 22 legs of
    131–211 waypoints, every one ending within 3 m of its goal, with no death. Aggressive
    monsters near each route were despawned first; that is GM setup for a route probe.
  - The offline test `RoutesLeaveTheFortressFromBesideTheObeliskButNotFromInsideIt`
    (`AION_SOAK_NAV_INTEGRATION=1`, `run/af02/af02-offline.log`) pins the cause: from the
    obelisk's spot there is no route to NA-23's stage or the western Ice Lake, and from
    beside it there is, both ways. The warning, logger and clock-read checks pass.
- [x] **AF-03 — The Fortress Dungeon.** A route to Mumu Bon (z 203) and Noroia (z 205):
  the entrance, navmesh coverage, map context. **Done when:** a SIM probe talks to both.
  - *Done 2026-09-29.* The dungeon is on the Altgard map itself (no new map context), under
    the fortress, reached on foot down a ramp that starts south-west of the obelisk (about
    (1606, 1743, 251)) and ends at z 204. It lies below the FLY zone's floor (240).
  - SIM probe `AltgardFortressDungeonNpcsAreReachedAndTalkedTo` (`run/af03/af03-sim.log`):
    from beside the obelisk, interaction routes on the live server's geometry reach Noroia
    (218 waypoints) and Mumu Bon (245); both open their dialog (page 1011), and the bot walks
    back to the obelisk. No aggressive monster stands in the dungeon.
  - Mumu Bon stands on his own scrap of mesh: a plain journey route to him is
    `NotConnected`, and only interaction routing reaches him (as NA-04 found for Heimdall).
    The offline test `DungeonNpcsAreReachedOnFootDownTheRamp` (`run/af03/af03-offline.log`)
    checks every step of both routes and the way back. The warning, logger and clock-read
    checks pass.
- [x] **AF-04 — Flight policy (pure).** Covers:
  - the two FLY zones;
  - takeoff checks: a Daeva, inside a FLY zone and not NO_FLY, the 10 s reuse, and **not
    on water** (the navmesh Water area or below z 200);
  - the FP budget: 60 FP, 1 per tick inside the zone, landing reserved with a margin;
  - never leaving the zone while airborne;
  - landing targets (the ground, or Borender's rock).

  **Done when:** policy tests cover each rule, including the water refusal and the FP margin.
  - *Done 2026-09-29.* `NaturalFlightPolicy` and `NaturalFlyZone` (pure; zones parsed from
    `zones_220030000.xml`). The Java rules it copies:
    - takeoff: Daeva, inside a FLY zone and no NO_FLY zone, no NOFLY effect, no
      transformation, no private store, and 9.9 s since the last takeoff
      (`FLY_REUSE_TIME - 100`). The client's water rule is added: no takeoff from a navmesh
      water area or below the map water level.
    - FP: 1 per second in the zone (Java drains 1 FP a second, 2 outside); a flight's cost is
      its airborne seconds, including air-fight time. Every waypoint must stay inside the
      zone polygon, floor and ceiling. A flight is taken only when it lands with at least
      10 FP.
    - airborne: land as soon as the FP left would not cover the way to the landing plus
      that reserve; the nearest landing (ground or platform) the FP still reaches.
    - on the ground FP returns 3 every 6 s, the first 3 s after landing: 117 s from empty to
      60. That is the price of a long air fight.

    Five `NaturalFlightPolicyTests` pass: the two zones (Altgard has no NO_FLY zone), every
    takeoff refusal including water, the reuse boundary, the zone and reserve rules, landing
    choice and restore times. The warning, logger and clock-read checks pass.
- [x] **AF-05 — Flight protocol and movement.** Take off (`CM_EMOTION` FLY), fly (`CM_MOVE`
  in flight) to a point in the air, land (`LAND`) on ground or on a platform, watch
  `SM_FLY_TIME`, and let FP refill on the ground. **Done when:** a SIM probe flies from the
  fortress to Borender's rock, talks to him, and lands back safely with FP left.
  - *Done 2026-09-29.* `NaturalFlightProtocol`: takeoff (`CM_EMOTION` FLY, whose
    `SM_EMOTION` answer carries the flight speed, 9 m/s at level 10), flight in
    `CM_MOVE_IN_AIR` samples every 500 ms (the E2E plan's flight packet), landing
    (`CM_EMOTION` LAND), and route planning.
  - **Finding: a floating island hangs over the fortress.** It spans about
    x 1600–1680, y 1780–1860 at z 335–395, with Borender on top. The straight climb from
    the obelisk hits it, so the planner searches a 10 m grid within 100 m for a clear
    column. It flies low to the column, climbs, crosses at cruise height and descends, and
    mirrors that on the way down. The shortest clear route wins. Every leg is checked for
    collision in pieces of 40 m or less, because the server's sight check refuses rays
    over 80 m.
  - SIM probe `AltgardFlightToBorendersRockAndBack` (`run/af05/af05-sim.log`): a level 10
    Daeva Cleric (GM setup, as in NA-23) takes off beside the obelisk, where the AF-04
    policy allows it. It flies 216 m to the rock top (FP 60 → 37; the policy predicted
    36), lands, and Borender answers (page 1011). FP restores on the rock (37 → 40). It
    flies back (FP 40 → 17) and lands exactly beside the obelisk, alive.
  - The offline test `FlightToBorenderGoesAroundTheFloatingIsland`
    (`run/af05/af05-offline.log`) pins the blocked straight climb and the clear detour
    both ways. The warning, logger and clock-read checks pass.
- [x] **AF-06 — Air combat for Q24011.** Kill the Abyss Fungus from the air within the FP
  budget: fly to one, kill it, land to refill as needed. A fall or death is recorded
  (OD-12). **Done when:** a SIM probe completes Q24011's six kills.
  - *Done 2026-09-29.* The Abyss Fungus never fight back (`ai="noaction"`, 240 HP,
    respawn 20 s), so flight time is the only danger.
    - `NaturalAirCombatPolicy` takes the nearest fungus only when flying to it, killing it
      (8 s) and flying on to the landing still leaves the 10 FP reserve; otherwise the bot
      lands and refills.
    - `NaturalAirCombat` finds a hover point within Smite range, in sight of the fungus and
      reachable by the flight planner (fungus under the island's edge are seen from few
      angles). It shoots with Smite and counts a kill from the 0% status, the object's
      removal or the quest counter. Corpses stay visible until `SM_DELETE`, so shot-down
      fungus are skipped.
    - Air-to-air legs now fly straight when clear.
  - SIM probe `AltgardAirCombatCompletesTheFungusKills` (`run/af06/af06-sim.log` and its
    trace): a level 10 Daeva Cleric with Q24011 at var 2 (GM setup; Borender's talk is
    AF-07's) finds no fungus visible from the ground and lands on Borender's rock, where
    they come into view. It kills four, lands to refill (the fifth would have cost 16 of
    25 FP), and kills a respawned fifth. Q24011 goes to REWARD with 5 kills in 3 sorties,
    5.7 s of shooting each (two Smites), no death and no fall.
  - The policy test covers the reserve edge and the no-target wait. The warning, logger and
    clock-read checks pass.
- [x] **AF-07 — Leg 1 quest mechanics.** Talk chains (Q2207), item use in the dungeon
  (Q2208, Mau Secret Remedy), Q2209's chain (Thrud → Tulberg → Borender → Noroia), and the
  template kill and collect quests (Q2201–2206, including the crystal stone objects for
  Q2203). It reuses the Ishalgen quest machinery where it fits.
  - *Done 2026-09-29.* `NaturalAltgardQuestSteps`:
    - `TalkAsync` plays any contract step: it opens the dialog, sends the step's actions,
      waits for each page, finishes a movie, and checks that the quest moved on (offer to
      START, var + 1 or REWARD, reward to complete).
    - `UseQuestItemAsync` uses the Mau Secret Remedy and waits for var 1.
    - The AF-06 air loop moved into `NaturalAirCombat.RunAsync`, shared with the runner;
      it reads flight time from `SM_FLY_TIME`.
  - SIM probe `AltgardScriptedQuestsPlayThroughTheContractSteps` (`run/af07/af07-sim.log`
    and its trace): a level 10 Daeva Cleric set up like the `altgard` snapshot (Q24010
    done, Q24011 LOCKED; GM setup) plays all 15 scripted steps in the contract's order.
    - Q2209: Thrud, Tulberg, Borender by flight, Noroia down the dungeon ramp, Thrud.
    - Level 11 is set in place of the Ice Lake hunting, and Q24011 unlocks by itself.
    - Q2207: Emgata, Itu, Suthran, Itu.
    - Q2208: Itu hands over the remedy; it is used; Mumu Bon puts the quest to REWARD; Itu.
    - Q24011: Valurion; Borender with his movie; 5 air kills in 2 sorties; Valurion pays
      the Altgard Legionary Brogans (REWARD4).
    - All four quests complete, alive, at level 11.
  - **Findings for AF-08.**
    - **Tulberg walks:** he was met at (1697, 1778), not at his spawn point, so NPCs must be
      approached where the client sees them.
    - **Itu has no interaction approach point** from Suthran (NA-04 finding a), so the
      approach falls back to open ground within talk range.
    - **No fungus is visible from the ground:** the air fight starts from Borender's rock.
  - **The template quests (Q2201–2206) run on the Ishalgen runner unchanged.** It already
    handles `quest_use_item` sources such as Q2203's crystal stones (700055, through
    `UseAndLootQuestObjectAsync`), and AF-01 checked that their run books use only
    supported operations. The runner needs the journey's navigator for Altgard, so they
    are exercised in AF-09's run once AF-08 adds that map context.
  - The warning, logger and clock-read checks pass; all 292 Natural unit tests pass.
- [x] **AF-08 — The Leg 1 runner and decision engine.** From the `altgard` snapshot: an
  Altgard map context, the decision engine over the contract (eligibility, level gates,
  "come back later"), rests and restocks at the fortress, help items (NA-21), and the
  missions as they auto-start. Trace every decision.
  - *Done 2026-09-29.*
    - `NaturalAltgardDecisionEngine` (pure) picks one move from the client's view. Template
      quests go hub-style (accept every eligible one at the fortress, work them on the Ice
      Lake, claim each). Then come the scripted steps, the remedy, and the air kills from
      var 2 to 6. Gated quests are left for later ("come back"), and it hunts for level
      when only gated quests remain.
    - A hunt quest is done when its kill counter is full: Java's monster_hunt keeps it at
      START until it is turned in.
    - The journey's `AltgardLeg1` mode (`AF_ALTGARD=1`) rebinds navigator, geometry, map and
      combat to Altgard (as for NA-23), and puts the Leg 1 NPCs and hunt targets into the
      waypoint graph. It runs template quests on the unchanged Ishalgen runner, scripted
      steps on `NaturalAltgardQuestSteps`, and flight and air kills on the AF-04..06 code;
      help items (NA-21) are topped up at run start, level-up and claims.
    - Every decision, step and flight is traced. The endpoint is checked across a relog and
      written to `altgard-l1-completion.json`.
  - Three engine tests pass. Smoke runs from the `altgard` snapshot:
    - `af08-smoke1` stopped with Q2201's counter full at START, which found the monster_hunt
      rule above;
    - `af08-smoke2` (`run/af-l1/af08-smoke2`) completed all of Leg 1 in 29 game minutes
      (1 m 26 s real): the six template quests, Q2209, Q2207, Q2208 and Q24011 (5 air
      kills). 51 fights, no death, no retreat, four flights. It ended at level 13 in the
      fortress with the endpoint verified across the relog.
  - The warning, logger and clock-read checks and all 295 Natural unit tests pass. No help
    item was needed.
- [x] **AF-09 — One SIM run of Leg 1** from the `altgard` snapshot to the Leg 1 endpoint,
  saved as the `altgard-l12` snapshot. Deaths are recorded (OD-12).
  - *Done 2026-09-29.* `scripts/sim/sim-snapshot.ps1 -Action Capture -Name altgard-l12
    -AltgardLeg1` does the capture:
    - it restores `altgard` into a fresh owned schema and resumes character 133297 with
      `AF_ALTGARD=1`;
    - it dumps only a verified `altgard-l1-completion.json` for that character;
    - its clock is the resume offset plus the run's own time.
  - Run `af09-l1-s1` (`run/snapshots/_capture/af09-l1-s1`, seed 1) passed in 1 m 29 s real
    time and 29 game minutes. All ten Leg 1 quests completed: 15 scripted steps, 36
    decisions, 51 fights, 4 flights, and the five air kills in one sortie (at level 13 one
    Smite, 2.1 s, drops a fungus). **No death** (OD-12 records deaths; there were none).
    It ends at level 13 in the fortress, beside Valurion, with the endpoint verified across
    the relog. It repeats the AF-08 smoke run exactly, as a deterministic SIM should.
  - Snapshot `altgard-l12`: `run/snapshots/altgard-l12` (git-ignored), character 133297,
    elapsed 17,736,346 ms, dump SHA-256 `be7d0750…09`. A test restore showed the Cleric at
    XP 432,438 in Altgard with Q2201–2209 and Q24011 COMPLETE, and was dropped. Every
    owned schema was dropped.
  - **The level target is exceeded:** Leg 1 ends at 13, not 11–12, because Ice Lake kills
    pay well. Leg 2 starts at Moslan Crossroad with Q2210 and Q24012 (level 12) already
    open.
- [x] **AF-10 — The full `CLAUDE.md` checklist and a checkpoint.** The isolated LIVE run comes
  at the end of the Altgard leg, not after each sub-leg (AF-Q3, decided).
  - *Done 2026-09-29.* The whole `CLAUDE.md` build-and-test list was run in order
    (`run/af10/summary.txt` and one log per check):
    - the build and `dotnet test AionServer.slnx`: GameServer 4,374 passed and 16 skipped,
      Commons 303, LoginServer 135 (7 skipped), ChatServer 41 (1 skipped), Simulation 145
      (33 skipped);
    - the warning baseline and the null-logger, clock-read and fidelity checks;
    - every Python and PowerShell contract test;
    - the NavBake check.
    30 of 32 passed at first. The two failures were my own:
    - **Custom-quest drafts drifted.** AF-00 added Borender's registration to Q2209 without
      regenerating `parity-artifacts/e2e/custom-quest-handler-drafts.json`. It was
      regenerated with `tools/Aion.QuestPlanExtractor`; the only change is that
      registration.
    - **`run-fast.ps1` failed:** "Fresh simulation account sim-player-42 already has a
      character". The new AF probes used accounts 42, 43 and 47, which the gathering and
      cooking scenarios also use (through tuples the earlier search missed). The SIM
      fixture now accepts accounts 101–140, and the six AF probes use 133–138.
    The rerun passed: drafts check, warning baseline, and `run-fast.ps1` (29 passed,
    3 skipped, `run/fast-20260929-142741`).
  - **Checkpoint.** Leg 1 is done in SIM. Snapshot `altgard-l12` (level 13) is the start of
    Leg 2. LIVE is run once at the end of the Altgard leg (AF-Q3). The Java fixes wait
    for one combined upstream PR (`docs/upstream-reports/README.md`).

## Leg 2: Moslan Crossroad (level 13–15) — proposal

**Status (2026-09-29): approved** (AM-Q1..AM-Q3 as recommended; see "Blocked /
questions"). It
starts from the `altgard-l12` snapshot: character 133297, a level 13 Cleric at 432,438 XP,
bound at Altgard Fortress, beside Valurion. Q24012 is already at START var 0 in that
snapshot (it followed Q24011's completion at level 12+).

### The hub and its grounds

Moslan Crossroad is a guard post about **365 m south of the fortress**: Olenja (203606),
Tigg (203604) and Loriniah (203605) stand within 7 m of each other at about
(1628, 1452, 256). There is **no obelisk and no vendor** there. The bind stays at the
fortress, and so do rests in town and restocks.

| Ground | Where | Distance from the obelisk | What lives there |
|---|---|---|---|
| The crossroad itself | around (1628, 1452) | 365 m | grove pluma (L10–11) 21–56 m from the three NPCs, **passive to Asmodians** (AM-03); **needletail karnifs 43–57 m away, which do attack** (6 m circles); elroco (L1) |
| Karnif and pluma slopes | x 1400–1705, y 1125–1524 | 380–520 m | needletail karnifs (L10–11), grove pluma (L10–11) |
| Okaru Tree | (1413, 1442, 282) | about 215 m west of the crossroad | the tree only |
| MuMu Farmland (zone `MUMU_FARMLAND`, x 1624–1997, y 889–1475) | farmers, carts and frightcorn at y 1000–1400 | 470–810 m | MuMu farmers and gatherers (non-aggressive), **aggressive MuMu patrols (L11–13, 46 spots)**, **SEASONED black claw patrols (L11–13, 15 spots)**, MuMu highsitters (L13, aggro range 15 m), six MuMu Carts, 20 frightcorn stalks |
| Wild tog grounds | x 1404–1881, y 673–1269 | 620–950 m | **aggressive wild togs (L11–13, 46 spots)** |
| Manir's Campsite | Manir (1460, 1193, 259) | 654 m, about 310 m south-west of the crossroad | Q2215's end; Stop 3's hub |

No FLY zone covers any of this; Leg 2 is on foot.

### Quests

| Quest | Name | Level | Kind | From → to | Objective | XP |
|---|---|---|---|---|---|---|
| 2210 | Retrieving the Report | 12 | template `report_to` | Rion (fortress, 1724,1677) → Loriniah | deliver | 16,283 |
| 2211 | Karnif Threat | 11 | template `monster_hunt` | Olenja | kill 10 needletail karnifs (L10–11) | 16,950 |
| 2212 | A Better Trap | 11 | template `item_collecting`, after 2211 | Olenja | 6 Tog Ribs, 80% from wild togs (L11–13, far south) | 11,895 |
| 2215 | A Long-Lost Friend | 11 | template `report_to`, after 2212 | Olenja → **Manir** | deliver to Manir's Campsite (see AM-Q1) | 8,355 |
| 2213 | Poison Root, Potent Fruit | 11 | **custom** | Tigg | loot the Okaru Tree (700057): the log (182203208) **poisons** the looter; Tigg takes it and removes the poison | 7,335 |
| 2218 | Frightcorn Seeds | 11 | template `item_collecting`, after 2213 | Tigg | 5 seeds, 80% from MuMu farmers and gatherers | 15,750 |
| 2219 | Ripened Frightcorn | 11 | template `item_collecting`, after 2218 | Tigg | 3 from frightcorn stalks (700052, `quest_use_item` objects) | 10,605 |
| 2214 | No-Frills Quills | 11 | template `item_collecting` | Loriniah | 5 Pluma Feathers, 80% from grove pluma | 10,335 |
| 2220 | Picking off Frightcorn | 12 | template `monster_hunt` | Tigg | kill 5 MuMu patrols (L11–13) | 16,283 |
| 24012 | An Ominous Crop | 12 | **custom campaign** (already START) | Loriniah | see below | 36,324 |

Total quest XP 150,115: from 432,438 that is 582,553, level 14 (490,331), with level 15
(649,169) within reach of the kills.

**Q2213 (Java `_2213PoisonRootPotentFruit`):**
- Tigg offers it.
- Using the Okaru Tree (USE_OBJECT) loots the Okaru Log. `onGetItemEvent` applies skill 255
  **Okaru Poison**: 20 HP every 6 s and −1 m/s speed for 10 minutes. It cannot be dispelled
  (`req_dispel_level` 99). The quest moves to var 1.
- At Tigg, QUEST_SELECT shows page 2375, and SELECT_QUEST_REWARD takes the log, removes the
  poison and sets REWARD.

**Q24012 (Java `_24012AnOminousCrop`):**
1. Loriniah, var 0: QUEST_SELECT (page 1011), SELECT1_1_1 (movie 61, page 1013), SETPRO1 → var 1.
2. Entering the `MUMU_FARMLAND` zone moves var 1 → 2 by itself.
3. Using a **MuMu Cart** (700096) moves var 2 → 3 → 4 → 5, one step per use. Each cart used
   disappears (`useQuestObject(…, die)`); six carts stand in the farmland and respawn after
   295 s, so the three uses take three carts.
4. **Only at var 5** do the drops start (`collecting_step` 5): 3 **Hairpins** (182215357)
   from MuMu patrols, highsitters and black claw patrols, and 5 **Waist Bands** (182215358)
   from MuMu gatherers and farmers.
5. Loriniah, var 5: QUEST_SELECT (page 2716), CHECK_USER_HAS_QUEST_ITEM → REWARD. The reward
   choice for the Cleric is the **Altgard Legionary Hauberk** 110551139 (chain, heal boost),
   SELECTED_QUEST_REWARD4.

The C# handlers of Q2213 and Q24012 register the same NPCs and items as Java: **no
Borender-style defect** in Leg 2.

**Chains:** 2211 → 2212 → 2215 and 2213 → 2218 → 2219. Q2214, Q2220 and Q2210 stand
alone. Q24012's drops depend on its own cart steps, and they share the farmland with
Q2218, Q2219 and Q2220, so the farmland is best worked once, with all four in hand.

### What is new for the bot

1. **A hub without an obelisk or vendor**, 365 m from town. Long legs (365–950 m) use the
   Altgard travel planner. A death revives at the fortress, and the bot must run back.
2. **Monsters near the quest NPCs.** Needletail karnifs 43–57 m from Olenja, Tigg and
   Loriniah attack Asmodians within 6 m. The grove pluma 21–56 m away do not: their tribe
   is MONSTER, which, like NA-23's lobnites, never aggroes Asmodians (AM-03).
3. **A poison that lasts 10 minutes** (Q2213). The rest policy must not wait for full HP
   against a damage-over-time it cannot remove; the heals and potions handle it, and Tigg
   ends it.
4. **Q24012's objects and zone:** a zone-entry step, three cart uses (a new cart each time,
   since a used one disappears), and drops that only start at var 5.
5. **Farmland combat:** mixed groups of passive farmers and aggressive patrols, archer-like
   highsitters (aggro 15 m), and the first **SEASONED** targets (black claw patrols). This is
   the first real test of the Anti-Shock shield and Salvation (NA-23 could not trigger them).
6. **Chains accepted as they open**, hub-style, while the grounds are far apart. The order
   should batch the farmland (Q2218, Q2219, Q2220, Q24012) and the tog grounds (Q2212).

### Leg 2 TODO list (proposed)

The same loop protocol, with "AM" in place of "NA".

- [x] **AM-01 — The Leg 2 contract and plans.** `parity-artifacts/e2e/natural-altgard-l2-contract.json`
  (the Leg 1 schema, generalised to name its hub and grounds), the eight template plans
  (Q2210, 2211, 2212, 2214, 2215, 2218, 2219, 2220), the scripted steps of Q2213 and Q24012,
  and contract tests like AF-01's. The loader takes a leg id instead of one default file.
  - *Done 2026-09-29.*
    - `natural-altgard-l2-contract.json` holds:
      - the ten quests and their order: Q2210 is taken at the fortress on the way out,
        and every chain goes in order;
      - the crossroad hub and the fortress as the town;
      - six areas (the crossroad, the karnif slopes, the Okaru Tree, the farmland, the tog
        grounds, and Manir's Campsite);
      - the four scripted steps of Q2213 and Q24012;
      - two object uses: the Okaru Tree loot, and the three cart uses of six respawning
        carts;
      - the farmland zone step, the var-5 collections, the Okaru poison, and the
        hauberk reward;
      - the endpoint at Manir's Campsite (AM-Q1).
    - `compile-quest-plans.py --runnable-only` wrote the eight template plans to
      `natural-altgard-l2-plans/`.
    - `NaturalAltgardContract` now loads by leg (`LoadLeg("l1")` or `"l2"`,
      `LoadPlans(leg)`). Leg 1's flight, remedy and air kills are optional, and Leg 1
      code reads them through `Required…` accessors.
  - Three `NaturalAltgardLeg2ContractTests` check the contract against quest data, spawns,
    zones, the skill template of poison 255, and the C# handlers of Q2213 and Q24012.
    They found that the waist bands also drop from the level 13 MuMu farmer (210469). All
    298 Natural tests pass, and so do the warning, logger and clock checks and
    `test-quest-plan-compiler.py`.
- [x] **AM-02 — Travel to the crossroad and its grounds.** Fortress ↔ crossroad ↔ Okaru Tree,
  farmland, tog grounds and Manir, on the Altgard navmesh and travel planner. **Done when:**
  a SIM probe walks each leg and back, and reports what aggroes on the way.
  - *Done 2026-09-29.* SIM probe `AltgardMoslanTravelWalksEveryGroundAndBack`
    (`run/am02/am02-sim.log`, account 139). Every leg is planned the way the journey
    plans a long leg (Altgard travel planner, level 13; the navmesh route for legs under
    100 m) and walked on the live server. All 12 legs arrive within 5 m, with no death:

    | Leg (out and back) | Length | Aggressive spawns the plan crosses | Live aggressive within 30 m of the route |
    |---|---|---|---|
    | fortress ↔ crossroad | 469 / 471 m | none | none |
    | crossroad ↔ Okaru Tree | 234 / 242 m | a karnif (L10–11) | 6 needletail karnifs L11, 3 L10, a blackpaw karnif L12 |
    | crossroad ↔ karnif slopes | 47 / 49 m | (navmesh) | 2 karnifs |
    | crossroad ↔ farmland (nearest patrol) | 92 / 93 m | (navmesh) | 3 MuMu patrols, a highsitter |
    | crossroad ↔ tog grounds (nearest tog) | 219 / 206 m | a wild tog L12, a veteran patrol L13 | 3 wild togs, a blackpaw karnif L13, 3 karnifs, a veteran patrol |
    | crossroad ↔ Manir's Campsite | 346 / 328 m | a veteran patrol L13 (and a karnif back) | 7 wild togs, a fierce tog L11 |

    The "live" column is what would have come for the bot: those monsters were despawned
    before the walk (GM setup for a route probe), so the return legs show none.
  - **Findings:**
    - The fortress road is clean.
    - The MuMu Farmland starts under 100 m from the crossroad.
    - The way to Manir crosses the tog herds: the AM-06 fights, or a wider route, are
      needed before Q2215's delivery.
    - Blackpaw karnifs (L12–13) and fierce togs (L11) are extra aggressive species that
      the quest data does not name.
    - The warning, logger and clock-read checks pass.
- [x] **AM-03 — Talking at a guarded hub.** Clear or avoid the pluma near the three NPCs before
  a talk, with the NA-22 patrol and pull policy. **Done when:** a SIM probe takes and hands in
  a quest at the crossroad with no pluma joining.
  - *Done 2026-09-29.*
    - `NaturalGuardedTalkPolicy` (pure) picks a talk spot within talk range and at least
      3 m outside every observed aggro circle. When every spot is covered, it names the
      monster covering the most spots to pull first.
    - The route to the spot keeps out of the circles (`FindJourneyPathAvoiding`). Java
      aggroes a monster only on a player less than 10 levels above it
      (`CreatureEventHandler.validateAggro`), so level 10–11 monsters still aggro a level 13
      Cleric.
    - Three policy tests pass.
  - SIM probe `AltgardMoslanCrossroadTalksWithThePlumaAlive` (`run/am03/am03-sim.log`,
    account 140), with nothing despawned:
    - a level 13 Cleric (GM setup) takes Q2210 from Rion and travels to a staging point
      30 m short of the crossroad;
    - it hands Q2210 in to Loriniah and takes Q2211 from Olenja;
    - **no attack reached the bot.**
  - **Correction: the grove pluma do not attack Asmodians.** Their tribe is MONSTER, whose
    relations aggro nobody but YUN_GUARD. The only attackers in view within 60 m were four
    needletail karnifs (tribe KALNIF_AMINX), 43–57 m out with 6 m circles, and the talk
    spots cleared them by 41–45 m.
  - **For AM-06:** in the farmland,
    - MuMu patrols (RATMAN) and highsitters (TOWERMAN) attack;
    - MuMu farmers and gatherers (RATMANWORKER) do not, but they support RATMAN and LYCAN;
    - black claw patrols (LYCAN) call RATMANWORKER and TOWERMAN for support.
    A pull there can bring a group.
  - The warning, logger and clock-read checks pass.
- [x] **AM-04 — Q2213 and the poison.** Loot the Okaru Tree, observe skill 255 on the bot, keep
  resting sane under the damage-over-time (heal, never wait for full HP while poisoned), hand in
  to Tigg, and see the poison removed. **Done when:** a SIM probe completes Q2213.
  - *Done 2026-09-29.*
    - `NaturalAltgardQuestSteps.UseObjectAsync` uses a quest object as the client does:
      it opens it, waits out the `SM_USE_OBJECT` bar and loots the named item, through
      the journey's loot routine (now `internal`). AM-05's carts use it too.
    - `HasEffect` reads the bot's visible effects.
    - The SIM fixture accepts accounts up to 150; the Leg 2 probes use 139–141.
  - SIM probe `AltgardPoisonRootLootsTheOkaruLogAndTiggRemovesThePoison`
    (`run/am04/am04-sim.log`, account 141). A level 13 Cleric (GM setup) plays:
    - Tigg's offer;
    - the walk to the Okaru Tree (aggressive monsters beside the route despawned for
      this probe);
    - the use and loot: the Okaru Log is in the bag, Q2213 is at var 1, and the client
      sees skill 255 on the bot;
    - the walk back, and Tigg's hand-in: the quest completes, the log is taken, and the
      poison is gone on both server and client.
  - **The poison is harmless in practice.** HP read 794 of 794 both at the loot and
    30 s later, sampled at those two points, with 571 s of poison left. Natural
    regeneration seems to keep up with 20 HP per 6 s at level 13. Resting stops at 90%
    HP, so a rest cannot stall on it, and nothing in the rest policy needed changing.
  - The warning, logger and clock-read checks and 301 Natural tests pass.
- [x] **AM-05 — Q24012's mechanics.** Loriniah with movie 61, the farmland zone step, three
  cart uses on three different carts, the drops from var 5, CHECK_USER_HAS_QUEST_ITEM and
  the hauberk reward. **Done when:** a SIM probe completes Q24012.
  - *Done 2026-09-29.* SIM probe `AltgardOminousCropUsesTheCartsCollectsAndIsRewarded`
    (`run/am05/am05-sim.log` and its trace, account 142). A level 13 Cleric with the Karmic
    Staff and Q24012 at START var 0, as in the snapshot (GM setup), plays the quest:
    - Loriniah with movie 61: var 0 → 1;
    - the walk into the farmland: the zone step takes var 1 → 2 on entry (the crossroad
      NPCs stand outside the zone polygon);
    - three carts, three different ones: var 2 → 5;
    - 3 Hairpins from three MuMu patrols, and 5 Waist Bands from farmers and gatherers:
      **every kill dropped its item**, the 100% that Java's `QuestDrop.getChance()` gives
      when `chance` is absent, and the drops come only at var 5;
    - Loriniah's hand-in (`CHECK_USER_HAS_QUEST_ITEM`, the reward window, REWARD4): the
      quest completes, the items are taken, and the hauberk is in the bag.
  - **GM setup, on the monsters and never on the bot:** each target was set to 1 HP and
    killed with one Smite, and the farmland monsters within 25 m of it were despawned. This
    probe is about the mechanics; AM-06 is about the fights.
  - **Findings:**
    - A used cart dies but stays in view. The next cart must be chosen among the living
      ones.
    - A MuMu farmer joined the first patrol fight, as AM-03's support relations predicted.
      With no gear and Smite alone the bot died there (the attempt before the GM setup).
    - Probe lessons for the runner: never pick a corpse as a target (the client keeps it
      in view until it decays), and never despawn or disturb one before looting.
    - `TalkAsync` now counts completion as progress for a START step that ends the quest.
    - `ShootDownAsync` stops when the bot dies or the target leaves view.
  - The warning, logger and clock-read checks and 301 Natural tests pass.
- [x] **AM-06 — Farmland and tog combat.** Mixed farmer and patrol groups, highsitters, SEASONED
  black claw patrols (fight them only when the pull planner calls it winnable, as NA-22 does),
  wild tog packs. Trace the first shield, Salvation and serum uses. **Done when:** a SIM probe
  hunts the Q2220 patrols and the Q2212 togs with deaths recorded (OD-12).
  - *Done 2026-09-29.* The fights use the journey's own Cleric combat (pull planner, patrol
    policy, heals, rests), through the Leg runner, which now plays any leg:
    - `AF_ALTGARD=l2` selects Leg 2;
    - `AF_ONLY` limits a diagnostic run to listed quests, and the engine's `only` filter
      completes the run when they are done;
    - Leg 1's flight parts are set up only for a leg with flight steps;
    - trace and completion names carry the leg id.
  - **Bug found and fixed: the Cleric's chain.** Java `Skill.useSkill` resets the player's
    chain whenever a skill without a chain category is cast. Light of Rejuvenation between
    Smite and Flashbolt therefore broke the chain, the server refused Flashbolt without a
    message, and the first run (`am06-hunt1`) stopped: "Cast 4025 had no start". The combat
    now clears its open chain on every non-chain cast. The bug was latent since NA-18; the
    level 10 fights never cast the heal-over-time in that gap.
  - Run `am06-hunt2` (`run/af-l1/am06-hunt2`, from `altgard-l12`, only Q2211, Q2212 and
    Q2220) passed in 34 game minutes (1 m 45 s real):
    - it accepted, worked and claimed all three;
    - 29 fights and 20 planned pulls, 7 defends before a pull, **no death, no retreat**;
    - the level went 13 → 14;
    - Flashbolt landed 22 times after Smite;
    - the tog hunt for Q2212, the farthest ground, took about 20 minutes.
  - No fight was hard enough for the Anti-Shock shield or Salvation. The first real use of
    those still waits for a harder pull.
  - An engine test covers the filter. The warning, logger and clock-read checks pass, as
    do 302 Natural tests. The first warning-baseline attempt failed on a `.pdb` copy while
    another session built at the same moment; the rerun passed.
- [x] **AM-07 — The Leg 2 runner.** Generalise the Leg 1 engine and the journey's `AltgardLeg1`
  mode to a leg id (`AF_ALTGARD=l2`). The hub is the crossroad; rests and restocks go back
  to the fortress; grounds are batched as above; the endpoint is checked across a relog.
  - *Done 2026-09-29.* The engine and the runner play Leg 2 from its contract:
    - `use-object`: the Okaru Tree loot, and the carts (a used cart is marked unavailable);
    - `enter-zone`: walk to the quest's first object, which stands inside the farmland;
    - `collect`: the var-5 hairpins and waist bands, killing and looting the sources, with
      SEASONED monsters skipped per AM-Q3;
    - `return-to-endpoint`: the NPC of the leg's last hand-in.
  - Q2215's hand-in at Manir is held back until everything else is done (the endpoint's
    most specific area decides that), and template quests stay out of the scripted loop.
  - An engine test walks the Leg 2 moves.
  - Smoke run `am07-smoke1` (`run/af-l1/am07-smoke1`, from `altgard-l12`) completed all of
    Leg 2 in 82 game minutes (6 m 8 s real):
    - Q2210 and the karnif, pluma, patrol and tog quests;
    - Q2213 (the tree and the poison), Q2218 and Q2219 (40 minutes for the frightcorn
      seeds);
    - Q24012 (movie, zone, three carts, 3 hairpins and 5 waist bands, the hauberk);
    - Q2215 last, at Manir.
    - 71 fights, **no death**, one retreat. It ends at level 15 beside Manir, with the
      endpoint verified across the relog (`altgard-l2-completion.json`).
  - The warning, logger and clock-read checks and 303 Natural tests pass.
- [x] **AM-08 — One SIM run of Leg 2** from `altgard-l12`, saved as the snapshot `altgard-l2`.
  - *Done 2026-09-29.* `sim-snapshot.ps1 -Action Capture -Name altgard-l2 -AltgardLeg1 -Leg l2
    -From altgard-l12` restores `altgard-l12`, resumes character 133297 with `AF_ALTGARD=l2`,
    and dumps only a verified `altgard-l2-completion.json` for that character. The new
    `-Leg` parameter selects the leg; the script's local result variable was renamed, since
    PowerShell names ignore case and `$leg` would have overwritten `$Leg`.
  - Run `am08-l2-s1` (`run/snapshots/_capture/am08-l2-s1`, seed 1) passed in 6 m 37 s real
    time:
    - all ten Leg 2 quests: 34 decisions, 4 scripted steps, 4 object uses;
    - 71 fights, **no death**, one retreat;
    - it ends at level 15 at Manir's Campsite, with the endpoint verified across the relog.
    It repeats the AM-07 smoke run, as a deterministic SIM should.
  - Snapshot `altgard-l2`: `run/snapshots/altgard-l2` (git-ignored), character 133297,
    elapsed 22,689,001 ms, dump SHA-256 `fd4e57cc…fc`.
    - A test restore showed the Cleric at XP 778,817 at (1459.56, 1192.68) in Altgard, with
      Q2210–2215, Q2218–2220 and Q24012 COMPLETE, and was dropped.
    - Every owned schema was dropped.
- [x] **AM-09 — The full `CLAUDE.md` checklist and a checkpoint.** LIVE stays at the end of the
  Altgard leg (AF-Q3).
  - *Done 2026-09-29.* The whole `CLAUDE.md` build-and-test list was run in order
    (`run/am09/summary.txt`, one log per check). 31 of 32 passed at first:
    - the build and `dotnet test AionServer.slnx`: GameServer 4,382 passed and 16 skipped,
      Commons 303, LoginServer 135 (7 skipped), ChatServer 41 (1 skipped), Simulation 145
      (38 skipped);
    - the warning baseline, the logger, clock-read and custom-quest-drafts checks,
      fidelity, every Python and PowerShell contract test, and the NavBake check.
  - **`run-fast.ps1` failed, on two of my probes** (the AF-07 scripted-quests probe and
    the AM-03 crossroad probe): "too far to talk". Both pass alone. In the shared Fast
    world the walking NPCs (Tulberg, Rion) have moved on by the time they run, and the
    probes had assumed an NPC stands where it was first seen. They now walk to the NPC's
    current position and talk again, as the journey does.
  - The rerun passed: `run-fast.ps1` 34 passed and 3 skipped (`run/fast-20260929-185711`),
    and the warning baseline and the logger, clock-read and drafts checks.
  - **Checkpoint.** Leg 2 is done in SIM. Snapshot `altgard-l2` (level 15, at Manir's
    Campsite) is the start of Leg 3, Stop 3: Manir's Campsite and Dock, with Q2221, the
    Q2290 escort and Q2222. LIVE is still run once, at the end of the Altgard leg (AF-Q3).
    The first real use of the Anti-Shock shield and Salvation is still to come.

**Endpoint:** Q2210–Q2215, Q2218–Q2220 and Q24012 completed, alive, at Manir's Campsite
(AM-Q1). No level target: level 14–15 is
expected.

## Leg 3: Manir's Campsite and Dock (level 15) — proposal

**Status (2026-09-29): approved** (AC-Q1..AC-Q3 as recommended; see "Blocked /
questions").
It starts from the `altgard-l2` snapshot: character 133297, a level 15 Cleric at
778,817 XP, standing beside Manir, bound at Altgard Fortress. Q2215 is COMPLETE, so all
three Stop 3 quests are open (minimum level 12). No other quest starts at Manir, Groken or
Karl.

### The hub and its grounds

Manir's Campsite is Manir (203607) at (1460, 1193, 259). There is no obelisk and no
vendor. Everything else in the leg lies west, at the grave robbers' camp, or
east, on the way to Basfelt.

| Ground | Where | From Manir | What lives there |
|---|---|---|---|
| Manir's Campsite | Manir (1460, 1193, 259) | — | qooqoo (L1), grove pluma (L11, passive), ice airons (L10) |
| Groken's Safe (700214) | (1266, 1110, 254) | 210 m west | **Commander Mohen, EXPERT L13, 5 m from the safe** (respawn 1,200 s); four grave robbing fencers (L14) 14–23 m away; smugglers (L13) 25–31 m; a duellist (L12) 40 m |
| Groken (203608) | (1224, 1096, 248) | 250 m west | one fencer 31 m away; the safe's group 41–57 m |
| Groken's Sailing Boat (700178), the escort's goal | (1216, 1217, 247) | 245 m west | **a duellist (L12) 7 m from the boat**; three fencers (L14) 19–32 m; two smugglers (L13) 23–25 m; ice airons |
| Karl (203609) | (1749, 901, 261) | 411 m south-east, at the south edge of the tog grounds | wild togs (L11–13, aggressive) on the way |
| Nokir (203631), Basfelt Village | (1780, 690, 264) | 213 m south of Karl | Basfelt's guards (Hunmir, Mirokin, a tamed worg); striped gumi (L2) |

All the grave robbers are tribe **LEHPAR**. It aggroes `PC_DARK` and supports every
LEHPAR tribe, so a pull near the safe or the boat brings the group. Level 12–14 is within
the Java aggro gap for a level 15 player. Groken is `GENERAL_DARK`, which LEHPAR does not
aggro, and the escort makes him PEACE, so **the robbers attack the player, never Groken**.
Every robber but Mohen respawns after 295 s. No FLY zone covers any of this; Leg 3 is on
foot.

### Quests

| Quest | Name | Kind | From → to | Objective | XP |
|---|---|---|---|---|---|
| 2221 | Manir's Uncle | custom | Manir → Groken | Groken; open Groken's Safe and loot the item; back to Groken | 8,910 |
| 2290 | Groken's Escape | custom, **escort**, after 2221 | Groken → Manir | escort Groken to his boat; report to Manir | 21,300 |
| 2222 | Manir's Message | custom, after 2290 | Manir → Karl → **Nokir** | talk; Nokir is at Basfelt (AC-Q1) | 13,080 |
| 24111 | What's Up, Dock? | template (D32) | Olenja → **Nokir** | the suspicious document at Manir's Dock gives the Lepharist Primer; hand in at Basfelt | 2,691 |
| 2217 | Gleaning the Meaning | template **IMPORTANT**, after 24012 or legacy 2013 | Olenja → **Gefion** | take the letter alongside Q24111 at Moslan in Leg 3; deliver on the first arrival in Basfelt, before Leg 4 starts | 17,678 |

The original three quests give 43,290 XP. Including Q24111 and the Q2217 correction, all five give
63,659 XP, plus combat XP. The endpoint requires at least level 15.

**Q2221 (Java `_2221ManirsUncle`):**
1. Manir starts it (page 1011).
2. Groken, var 0: QUEST_SELECT (page 1352), SETPRO1 → var 1.
3. Groken's Safe, var 1: USE_OBJECT (page 1693), SETPRO2 closes the dialog. The safe is a
   `quest_use_item` object. It drops item 182203215 (`quest_drop`, `collecting_step` 1, no
   chance, so 100%), and `onGetItem` moves var 1 → 2. This is AM-04's use-and-loot shape.
4. Groken, var 2: QUEST_SELECT (page 2375). SELECT_QUEST_REWARD takes the item and sets
   REWARD (page 5); the end dialog follows.

**Q2290 (Java `_2290GrokensEscape`), the escort:**
1. Groken offers it: QUEST_SELECT 1011, ASK_QUEST_ACCEPT 4, QUEST_ACCEPT_1 1003.
2. **SELECT1_1** starts the quest and calls `defaultStartFollowEvent(env, Groken, 700178, 0, 1)`:
   - Groken becomes PEACE and receives FOLLOW_ME;
   - var 0 → 1;
   - a `QUEST_FOLLOW` task starts: `FollowingNpcCheckTask`, every 1,000 ms.
3. Each check, in this order:
   - **fail** if the player or Groken is dead;
   - **fail** if the player and Groken are more than **50 m** apart;
   - **succeed** if Groken is within **20 m of the first spawn of 700178** (the boat).
     The player does not have to reach or use the boat, although 700178 has a talk
     registration.
4. Success (`onNpcReachTarget`): var 1 → 3 and **movie 69**.
   Failure (`onNpcLostTarget`): var 1 → 0.
   **A logout at var 1 also sets var 0**, and it costs Groken too (AC-04): the logout clears his
   target (`CreatureController.notSee`), and the next `CREATURE_MOVED` near him takes `FollowingNpcAI`'s
   no-target branch, `stopFollow`, so he is deleted and respawns 295 s later.
5. Either way `FollowEventHandler.stopFollow` **deletes Groken and schedules his respawn**
   (295 s). After a failure the bot has to wait for Groken at his spawn. At START var 0, a
   QUEST_SELECT on Groken restarts the follow directly; there is no dialog to click
   through.
6. Manir, var 3: QUEST_SELECT (page 1693). SELECT_QUEST_REWARD sets REWARD; then page 5 and
   the end dialog.

How Groken follows (Java `FollowingNpcAI` and `FollowManager`):
- he moves only when the player moves (CREATURE_MOVED);
- he closes to 2 m;
- outside walk mode his speed is his template run speed, 6 m/s (AC-04 measures it).

A running player therefore does not outpace him on open ground. Obstacles, a fight, or a
path he cannot follow can.

**Q2222 (Java `_2222ManirsMessage`):**
1. Manir starts it (page 1011).
2. Karl, var 0: QUEST_SELECT (page 1352), SETPRO1 → var 1.
3. Nokir: QUEST_SELECT (page 2375), SELECT_QUEST_REWARD → var 2, REWARD, end dialog.

Nokir does not check var 1, so Java would let a player skip Karl. **This quirk is kept.**
The bot talks to Karl anyway, as the quest text asks.

The C# handlers of all three register exactly what Java registers:
- Q2221: Manir, Groken, the safe, and the get-item event;
- Q2290: Groken, the boat, Manir, logout, and reach/lost target;
- Q2222: Manir, Karl and Nokir.

`FollowingNpcCheckTask`, `FollowingNpcAI` and the destination checker are line-for-line
ports. **No Borender-style defect** in Leg 3.

### The escort handler

The operator asked for a dedicated escort handler. Q2290 is the first escort. Q2284
(Germir to Babarunerk, Stop 8, target npc 798034, steps 1 → 2 with the reward on reach) is
the second, and has the same shape. The handler is therefore **data-driven, not written
for Q2290 alone**.

Each escort has one entry in a new contract block, `escorts`:

| Field | What it holds | Q2290 |
|---|---|---|
| quest, follower | the quest and the follower npc | 2290, Groken 203608 |
| goal | the target npc, resolved to its first spawn, and the success radius | 700178 (1216, 1217, 247.4), 20 m |
| start | the dialog that starts the follow, and the var it expects | SELECT1_1 on offer; QUEST_SELECT at START var 0 |
| steps | the var while following, on success, and on loss | 1 / 3 / 0 |
| leash | the Java fail distance and the check period | 50 m, 1,000 ms |
| movie | the success movie, if any | 69 |
| follower respawn | from the spawn data | 295 s |
| goal stand | where the player stops, so the follower ends inside the radius | computed; see the policy |
| clear | the grounds to clear before starting | the boat's robbers and the line (AC-Q3) |

The handler has two parts, like flight (AF-04 and AF-05).

**`NaturalEscortPolicy`** is pure and unit-tested. Each tick it takes:
- the player's position and HP;
- the follower's last seen position and whether it is visible;
- the goal, the leash, threats, and the virtual time.

It returns one of **Advance**, **WaitForFollower**, **HoldAndFight**, **WaitForRespawn**,
**Restart**, **Done** or **GiveUp**. Its rules:
1. **Leash.** Advance only while the follower is within 8 m. Stop and wait beyond 8 m.
   Beyond 25 m, walk back toward him. This leaves a wide margin under Java's 50 m.
2. **Hops.** The route is cut into hops of at most 10 m, so the gap is checked often.
   Groken moves only on CREATURE_MOVED, so a standing player does not pull him along.
3. **Goal.** Stop at a stand point on Groken's side of the boat, at most 12 m from the
   goal. A trailing Groken is then inside 20 m, and the player stays out of the duellist's
   reach at the boat.
4. **Forbidden while following** (they reset or fail the quest):
   - logout or relog;
   - a return scroll, a teleport, flight or gliding;
   - rest trips and restock trips.
5. **Fights.** Fight in place: Groken cannot be hit, and he waits within 2 m. Heal
   through Salvation and the Anti-Shock shield first. A retreat must stay inside the leash.
   Retreat past it only when death is predicted; that fails the escort, and the failure is
   recorded, not hidden.
6. **Respawn window.** Clearing, the walk back to Groken, and the escort itself must all
   fit before the first cleared robber respawns (295 s). If they would not, clear again
   first.
7. **After a loss:**
   - var back at 0, Groken gone: go to his spawn and wait for his respawn on the virtual
     clock;
   - clear again;
   - QUEST_SELECT restarts the follow.

   After the attempt budget (AC-Q2), return **GiveUp**.

**`NaturalEscortProtocol`** is the executor. It:
- drives the policy from real packets: the follower's `SM_MOVE`, the `SM_QUEST_ACTION`
  var updates, and `SM_PLAY_MOVIE` 69;
- detects success by **var 3 plus the movie**, never by distance alone;
- confirms the follower is gone and records it;
- writes an `escort` trace: per tick, the gap, the goal distance and the action; plus
  every attempt, its outcome, and the longest gap seen.

### What is new for the bot

1. **The escort** (above).
2. **An EXPERT at a quest object.** Commander Mohen (L13, EXPERT, 1,200 s respawn) stands
   5 m from Groken's Safe, with four fencers within 23 m, all supporting each other. The
   pull planner has to take the camp one or two at a time from the outside in:
   - the smugglers and outer fencers first;
   - then Mohen, alone.

   This is the likeliest first real use of the Anti-Shock shield, Salvation and the
   serums. Deaths are recorded, not failures (OD-12).
3. **A hub with nothing at it.** Manir has no vendor. Rests are in the field
   (AM-Q2 (a)). A death revives at the fortress, about 650 m away.
4. **The trip to Basfelt** (if AC-Q1 (a)): 624 m through the tog grounds that Leg 2 already
   crossed, ending among Basfelt's guards.

### Leg 3 TODO list

The same loop protocol, with "AC" in place of "NA".

- [x] **AC-00 — Cleric skills through level 20.** The operator asked on 2026-09-29 whether the
  combat and healing rotation keeps up as the bot levels. **It does not.**
  - The catalog (`NaturalClericSkills.Cleric` and `NaturalPriestSkills.All`) is a fixed
    list of ids up to level 10. `Best` only filters it by the learned `SM_SKILL_LIST`.
  - The server auto-learns every new rank on level-up (Java `SkillLearnService.learnNewSkills`
    from `skill_tree.xml`), and keeps the old ids. So the level 15 bot still casts its
    level 6–10 ranks. It has never cast:
    - the level 11–15 ranks: Healing Light III 1840, Smite III 4014, Infernal Blaze II 1815,
      Hallowed Strike III 1616;
    - the level 15 second ranks of Flashbolt, Slashing Wind, Earth's Wrath, Light of
      Rejuvenation, Herb Treatment and MP Recovery;
    - the new skills: Cleanse 3935 (13), Penance 3867 (15) and Summon Holy Servant 4106 (15).

  The work:
  - Add every autolearn Asmodian Cleric rank up to level 20 to the catalog under its
    existing role. For each rank, take the MP, cooldown group, range, chain and reagent
    from `skill_templates.xml`. `Best` already prefers the highest learned rank.
  - Decide from the templates whether each new skill is used (a role in `Decide`) or
    excluded, and record the reason. The skills are Cleanse, Penance, Summon Holy Servant,
    Stability (16), Divine Touch (17) and Healing Grace (19).
  - The level 16 skill books (Summon Divine Mirror and Summon Divine Crystal) and the
    level 20 stigmas are not auto-learned. They stay out unless the operator says
    otherwise.
  - Add a **ratchet test**. Every autolearn Cleric skill at or below level 20 in
    `skill_tree.xml` must be either in the catalog or on a recorded exclusion list. The
    rotation then cannot fall behind again as the leveling legs go on.

  **Done when:** the test passes, and a SIM fight from `altgard-l2` casts the level 15
  ranks (seen in the combat trace).
  - *Done 2026-09-29.* `NaturalClericSkills.Cleric` now holds every auto-learned Asmodian Cleric
    rank up to level 20, with the MP, cooldown group, range, chain and reagent of each template:
    - new ranks of the Priest roles: Healing Light III/IV, Smite III/IV, Infernal Blaze II/III and
      Hallowed Strike III/IV. The Priest catalog stays frozen, so Ishalgen is unchanged;
    - new ranks of the Cleric roles at 15 and 20: Herb Treatment, MP Recovery, Light of
      Rejuvenation, Flashbolt, Slashing Wind and Earth's Wrath. Ranks 2 and 3 of the powder
      skills still use Lesser Odella Powder;
    - four new roles:
      - **servant**: Summon Holy Servant I/II, summoned on a target above 50% HP, after the
        chains;
      - **touch**: Divine Touch, which follows Slashing Wind's chain like Flashbolt follows
        Smite;
      - **grace**: Healing Grace, the urgent heal while it is ready (1,298 HP for 114 MP);
      - **penance**: Penance I/II, a rest-only skill. When mana is needed and HP is at or above
        70%, it trades about 570 HP for about 1,150 MP over 30 s. It is never cast in a fight.
    - `NaturalClericSkills.Excluded` names the four left out, each with a reason: Light of
      Resurrection, Cleanse I/II (the client cannot tell which debuffs can be dispelled, and
      the Okaru poison cannot) and Stability (enmity, for a group tank).
    - The skill books (Summon Divine Mirror and Summon Divine Crystal, level 16) and the level
      20 stigmas are not auto-learned, so they stay out.
    - The class-less auto-learned skills (Return, Bandage Heal, Escape) are outside the ratchet
      and unchanged.
  - Also fixed: the between-fights heal in `RestSafelyAsync` looked only at the Priest catalog,
    so it stayed on Healing Light II. The air combat now takes the highest learned Smite rank.
  - The ratchet `EveryAutoLearnedActiveClericSkillToLevel20IsCastOrExcludedWithAReason` reads
    `skill_tree.xml` and `skill_templates.xml`. Five more tests cover:
    - the rank pick-up (including a rank not yet observed);
    - Divine Touch;
    - Holy Servant;
    - Healing Grace;
    - Penance at rest and never in a fight.
    All 309 Natural tests pass.
  - **SIM** (`run/ac00/ac00-cleric-encounter-l15-s1`): the NA-23 encounter takes a new
    `AC00_CLERIC_LEVEL=15`. It prepares a level 15 probe Cleric (GM on the probe only) against
    Leg 3's grave robbing fencers (L14): 14 kills, **no death**. The server confirmed every
    level 15 cast:
    - Smite III ×8, Flashbolt II ×5 and Hallowed Strike III ×5;
    - Earth's Wrath II ×4, **Holy Servant ×3**, Infernal Blaze II ×3;
    - Slashing Wind II ×2 and Light of Rejuvenation II ×2.
    Healing Light III and Penance were not needed in this run: HP never fell below the heal
    line (one timed potion covered it), and mana never below 50%.
  - The warning baseline and the null-logger and clock-read checks pass.
- [x] **AC-01 — The Leg 3 contract.** `parity-artifacts/e2e/natural-altgard-l3-contract.json`,
  with:
  - the hub (Manir) and the fortress as town;
  - areas: the campsite, the safe camp, Groken, the dock, Karl, and Basfelt;
  - the three quests and their order;
  - the scripted steps of Q2221 and Q2222, and the safe as an object use with its loot;
  - the new `escorts` block for Q2290;
  - the endpoint (AC-Q1).

  `NaturalAltgardContract.LoadLeg("l3")` adds an `Escorts` record. Contract tests check
  it against:
  - quest data and the spawns: the goal is the *first* spawn of 700178;
  - Groken's template: AI `following`, respawn 295 s;
  - the C# handlers' registrations and follow call.

  There are no template plans: all three quests are custom.
  - *Done 2026-09-29.* `parity-artifacts/e2e/natural-altgard-l3-contract.json` holds:
    - the start: `altgard-l2`, level 15, with Q24013 START and Q24014–24016 LOCKED, as the
      snapshot has them;
    - Manir as the hub and the fortress as town;
    - seven areas: the campsite, the safe camp, Groken, the escort line, the dock, Karl and
      Basfelt;
    - the three quests in order, and nine dialog steps: the two offers at Manir, the Q2290
      offer ending in SELECT1_1, the var 0 restart at Groken, and the three hand-ins;
    - the safe as a one-shot object use: page 1693 closed with SETPRO2, loot 182203215 at
      100%, the safe gone for 295 s;
    - the `escorts` entry for Q2290: goal 700178 at its only spawn, 20 m radius, 50 m leash,
      1,000 ms checks, vars 1/3/0, movie 69, 295 s respawn, 6 m/s, the dock and the line to
      clear, three attempts;
    - the endpoint beside Nokir at Basfelt (AC-Q1), snapshot `altgard-l3`.
  - `NaturalAltgardContract`:
    - loads `l3`;
    - adds the `NaturalAltgardEscort` record, checked on load against the quests, steps and
      areas;
    - gives object uses an optional dialog page and close action;
    - makes the reward choice optional (Leg 3 has none), read through `RequiredRewardChoice`;
    - lets a leg have no plan directory, since all three quests are custom.
  - Four `NaturalAltgardLeg3ContractTests` check it against quest data, spawns, NPC templates, the
    three C# handlers and the C# follow engine: 50 m, 20 m, 1,000 ms, and the goal as the
    first spawn. All 313 Natural tests pass, and so do the warning baseline, the logger and
    clock checks, and `test-quest-plan-compiler.py`.
- [x] **AC-02 — Travel.** Route and walk in SIM (a new account, 143):
  - Manir ↔ safe ↔ Groken ↔ goal stand ↔ Manir;
  - Manir ↔ Karl ↔ Nokir.

  Record the danger on each, as AM-02 did. **Done when:** the dock at z 247 is on the
  navmesh, and the Groken → boat line has a walkable route with no leg longer than the
  10 m hop.
  - *Done 2026-09-29.* `SimulationAltgardLeg3TravelTests` (account 143, `run/ac02/travel.log`)
    plans each leg with the Altgard travel planner at level 15 and walks it on the live SIM
    server. All eight arrive within 5 m:

    | Leg | Metres | Plan crosses | Live aggressive within 30 m |
    |---|---|---|---|
    | Manir → safe | 245 | one each of the smuggler L12/13, duellist, **Mohen**, fencer | 23 robbers, among them Mohen, 7 duellists and 5 fencers |
    | safe → Groken | 149 | (navmesh route) | none |
    | **Groken → goal stand** (the escort) | 111 | none | a duellist, a fencer, 2 smugglers (the dock, AC-Q3) |
    | goal stand → Manir | 245 | a smuggler, a duellist | 3 robbers |
    | Manir → Karl | 467 | a wild tog | 13 wild togs |
    | Karl → Nokir | 242 | a wild tog | 4 wild togs |
    | Nokir → Karl → Manir | 236 + 470 | wild togs | none (already cleared) |

  - **The dock is on the navmesh:** ground at z 247.75, against the boat's 247.375.
  - **The goal stand** is 10 m from the boat, on Groken's side, at (1219.9, 1207.0, 247.5).
    A Groken trailing within 2 m ends well inside the 20 m radius.
  - **The escort line** is 56 navmesh waypoints no more than 2 m apart, so it cuts into
    hops of 10 m or less anywhere.
  - The aggressive monsters were despawned before each walk: GM setup on a probe character.
    This item is the routes, not the fights.
  - The warning baseline and the logger and clock checks pass.
- [x] **AC-03 — `NaturalEscortPolicy`.** The pure policy and its tests:
  - leash bands;
  - hops;
  - the goal stand point;
  - the forbidden actions at var 1;
  - fight-in-place and the retreat limit;
  - the respawn-window budget;
  - loss, then respawn wait, then restart, then the attempt budget.

  Also one test per Java rule it relies on (50 m, 20 m, fail before success in the same
  tick).
  - *Done 2026-09-29.* `tests/Aion.Bots/Scenarios/NaturalEscortPolicy.cs`, pure. `Decide`
    takes a `NaturalEscortObservation`: the player, the follower's last seen position (null once
    he is gone), the quest status and var, attackers, whether death is predicted, the clear
    areas, the first cleared respawn, the route and the speed. It returns one of:
    - while following: **advance** (the next hop, 10 m or less along the route, while he is
      within 8 m), **wait-for-follower**, **close-gap** (beyond 25 m), **hold-and-fight**,
      **retreat** (only when death is predicted), **wait-at-goal**;
    - before a start: **clear** (aggressors in the clear areas, or a cleared monster back
      before the escort would end, with 30 s margin), **wait-for-respawn** (295 s after he
      was seen to go), **approach-follower**, **start** (the offer step, or the var 0 restart
      step), **give-up** after three attempts;
    - **done** at var 3, REWARD or COMPLETE; **revive**; **blocked** on any other state.
  - The actions that would lose Groken are forbidden while he follows: logout, relog,
    return scroll, teleport, fly, glide, rest trip and restock trip.
  - `JavaCheck` mirrors `FollowingNpcCheckTask`: 3-D and strictly inside (Java
    `PositionUtil.isInRange`, `<`), and the loss wins when both fire in one tick.
  - Six `NaturalEscortPolicyTests` pin:
    - the bands: 8 + 10 ≤ 25, 25 × 2 ≤ 50, and stand 10 + gap 8 < 20;
    - Java's check at exactly 50 m, 50 m in 3-D, 20 m, and a loss and an arrival in one tick;
    - the following moves;
    - the forbidden actions;
    - the start: steps, clear, respawn window, respawn wait, give-up;
    - the end states.
    A walk of the 111 m line with the follower close takes 14 hops of at most 10 m.
  - All 319 Natural tests pass, and so do the warning baseline and the logger and clock checks.
- [x] **AC-04 — The escort protocol, probed in SIM** (account 144; a probe character with
  Q2221 set COMPLETE server-side, like the earlier probes; no GM on the natural character).
  **Done when** one test shows:
  - **success:** SELECT1_1, Groken follows (his speed and the largest gap are recorded),
    var 1 → 3, movie 69, Groken deleted, and the hand-in to Manir gives REWARD then
    COMPLETE;
  - **loss by distance:** run past 50 m, var → 0, Groken gone, his respawn after 295
    virtual seconds, the QUEST_SELECT restart, then success;
  - **loss by logout:** a relog at var 1 → var 0.

  The robbers at the boat are removed for this probe, so it tests the escort alone.
  - *Done 2026-09-29.* `tests/Aion.Bots/Scenarios/NaturalEscortProtocol.cs` drives
    `NaturalEscortPolicy` from the client's own packets. Each tick it reads:
    - Groken's `SM_MOVE` position;
    - the quest status and var;
    - `SM_PLAY_MOVIE` cutscene 69.

    It then walks a hop, waits, talks through the contract's start or restart step, fights,
    clears, or waits out his respawn on the game clock. Success needs var 3 and the movie.
    Every tick and attempt is traced (`escort-tick`, `escort-attempt`).
  - **A fix the probe found:** Java deletes the follower at every end, so the protocol retires
    the ended follower's object id. The probe's GM teleport away and back never refreshed the
    client's view, so the client kept a deleted Groken, and the first run talked to it and
    stalled.
  - The shared `TalkAsync` now accepts an escort offer that lands at START var 1.
  - SIM `SimulationAltgardEscortTests` (account 144, `run/ac04/`) is a level 15 probe Cleric
    with Q2221 set COMPLETE. The 6 robbers on the line and at the dock were despawned (GM,
    probe only). The run:
    1. **The offer** (QUEST_SELECT, QUEST_ACCEPT_1, SELECT1_1) takes the quest to var 1. Groken
       follows at 6 m/s, 1.6 m behind after an 8 m walk.
    2. **Loss by logout:** after the relog, var 0 on the client and the server, and Groken
       deleted. He was back after 290 game s. This is Java's behaviour (see Q2290 above).
    3. **Loss by distance:** the restart (QUEST_SELECT at var 0) follows again. The probe
       teleported 111 m away: var 0 within 3 s, and Groken deleted on the server.
    4. **The protocol** (attempt 3 of 3): it waited out the 295 s respawn, restarted, and walked
       Groken to the goal stand in **19 game s**, largest gap **6.3 m**, **12 hops**. That gave
       var 3 and movie 69, and Groken gone.
    5. Manir's hand-in completed Q2290.
  - All 319 Natural tests pass, and so do the warning baseline and the logger and clock checks.
- [x] **AC-05 — Q2221 at the safe, probed in SIM** (account 145). Groken's talk, the safe's
  USE_OBJECT, the loot (182203215 at 100%), and the hand-in, with the camp's robbers set
  to 1 HP as in AM-05. **Done when:** Q2221 is COMPLETE and Q2290 is offered.
  - *Done 2026-09-29.* `NaturalAltgardQuestSteps.UseContractObjectAsync` uses a contract object.
    When the object opens its dialog as it is used (Java `QuestItemNpcAI.handleUseItemFinish`
    sends it before the drop), the client answers it after the loot with the contract's close
    action (SETPRO2), as a player clicks it away.
  - SIM `SimulationAltgardGrokensSafeTests` (account 145, `run/ac05/safe.log`) is a level 15
    probe Cleric:
    1. the offer at Manir, var 0;
    2. Groken, var 1;
    3. **the safe:** a 3,000 ms use bar, page 1693, 182203215 looted, var 2, and the safe gone on
       the server;
    4. Groken's hand-in: COMPLETE, the item taken back;
    5. Groken's QUEST_SELECT for Q2290 opens page 1011, so **the escort is offered**. It is not
       taken.
  - A deviation from the item text: the 11 robbers in the camp and around Groken were
    **despawned** (GM, probe only), not set to 1 HP. The camp fight, Commander Mohen
    included, is played for real in AC-06. The travel was a setup teleport, since AC-02 walked
    it.
  - The warning baseline and the logger and clock checks pass.
- [x] **AC-06 — The Leg 3 runner.** Engine action `escort` and its executor (the protocol).
  Also the talk chain to Karl and Nokir, with Nokir's guarded spot (AM-03's policy). A
  smoke run from `altgard-l2` with real combat:
  - the safe camp and Commander Mohen;
  - the dock clear (AC-Q3);
  - the escort, and Q2222.

  Record every fight, death, escort attempt, and first use of the shield or Salvation.
  - *Done 2026-09-29.* The decision engine plans **`escort`** for a quest with an escort
    entry: from the offer, while following (var 1), and after a loss (var 0). The success var
    (3) goes to its own talk step at Manir.
  - The runner's `RunEscortAsync` gives `NaturalEscortProtocol` the navigator's walk, the
    engaged-attacker fight, and the **clear** (AC-Q3). The clear approaches the boat and then
    Groken, and pulls and kills every aggressive monster seen in the dock and escort-line
    areas. It reports the first kill's 295 s respawn, so the policy keeps the escort inside
    that window.
  - The attempts, the ended followers and the last loss carry across protocol runs, since a
    death ends a run. Three attempts write `altgard-l3-escort-given-up.json` and stop the
    leg (AC-Q2).
  - Object uses now go through `UseContractObjectAsync`.
  - A new engine test walks Leg 3 from the offer to `leg-complete` at Nokir.
  - **Smoke run `ac06-l3-smoke`** (`run/af-l1/ac06-l3-smoke`, from `altgard-l2`, seed 1) passed
    in 39 s real time, 15 game minutes:
    - Q2221: the safe was used after the camp fights. Page 1693 was answered and the loot
      taken.
    - **Q2290:** the clear killed **16 robbers** at the dock and along the line (the first
      respawn at 594.7 s). Then **escort attempt 1 succeeded**: 18 game s, largest gap
      **5.8 m**, 11 hops, movie 69. Manir handed it in.
    - Q2222: Karl, then Nokir at Basfelt.
    - **21 fights, no death, no retreat:** 16 smugglers, 3 fencers and 2 duellists. The
      lowest HP was 1,017 of 1,122.
    - It ends at **level 16** beside Nokir, with the endpoint verified across the relog.
  - The level 15 ranks carried the fighting: Smite III, Flashbolt II, Hallowed Strike III,
    Earth's Wrath II, Infernal Blaze II, Slashing Wind II, Holy Servant ×3 and Light of
    Rejuvenation II.
  - Not needed, so still unexercised: the Anti-Shock shield, Salvation and any help item.
    **Commander Mohen was never fought.** His aggro range is 6 m (`srange`), and the bot used
    the safe from outside it.
  - The warning baseline and the logger and clock checks pass.
- [x] **AC-07 — One SIM run of Leg 3 and the snapshot.** `sim-snapshot.ps1 -Leg l3 -From
  altgard-l2` captures `altgard-l3` after a clean completion, and a restore check.
  - *Done 2026-09-29.* `sim-snapshot.ps1` accepts `-Leg l3`.
    `-Action Capture -Name altgard-l3 -AltgardLeg1 -Leg l3 -From altgard-l2` restored
    `altgard-l2`, resumed character 133297 with `AF_ALTGARD=l3`, and dumped only a verified
    `altgard-l3-completion.json`.
  - Run `snapshot-altgard-l3-s1` (`run/snapshots/_capture/snapshot-altgard-l3-s1`, seed 1) repeats
    the AC-06 smoke run exactly, as a deterministic SIM should:
    - all three quests;
    - 21 fights, **no death**, no retreat;
    - the clear killed 16 robbers, and the escort succeeded on attempt 1 (18 s, gap 5.8 m,
      movie 69);
    - level 16 beside Nokir, the endpoint verified across the relog, 911 game s.
  - Snapshot `altgard-l3`: `run/snapshots/altgard-l3` (git-ignored), character 133297, elapsed
    23,620,001 ms, dump SHA-256 `47992b37…f3`.
    - A test restore into an owned schema succeeded and was dropped; no owned schema is left.
    - The dump shows Q2221, Q2290 and Q2222 COMPLETE, Q24013 START, 848,030 XP, at
      (1779.88, 690.477) in Altgard.
  - The warning baseline and the logger and clock checks pass.
- [x] **AC-08 — The full `CLAUDE.md` checklist and a checkpoint.**
  - *Done 2026-09-30.* The whole `CLAUDE.md` build-and-test list ran in order
    (`run/ac08/summary.txt`, one log per check). 31 of 32 passed at first:
    - the build and `dotnet test AionServer.slnx`: GameServer 4,399 passed and 16 skipped,
      Commons 303, LoginServer 135 (7 skipped), ChatServer 41 (1 skipped), Simulation 145
      (41 skipped);
    - the warning baseline, the logger, clock-read and custom-quest-drafts checks, fidelity,
      every Python and PowerShell contract test, and the NavBake check.
  - **`run-fast` failed at first.** Three reruns found and fixed, in turn, tests that assumed
    a walking or perched NPC stood where they expected. The new Leg 3 probes add game time to
    the shared Fast world, so the patrols had moved on. Each test passed alone:
    - **A real bug:** `NaturalAirCombat.ShootDownAsync` (AF-06) never released the client's
      casting gate after a refused cast, so the next cast threw. It now does, as the journey's
      `CastAsync` does, and it returns on an out-of-range or out-of-sight refusal so the caller
      can move.
    - The AM-05 crop probe follows a walking patrol, closer each time. It sets aside a
      source it cannot see from the ground (a MuMu highsitter, 210610, 7 m up on its
      platform) and takes another.
    - The Q2223 correction test (D27, another session's) stands beside where the client sees
      Lamir on his route, instead of at his spawn point.
    - The AM-03 crossroad probe's setup teleport goes beside Rion's current position; he
      walks the fortress.
  - The fourth `run-fast` passed: 37 passed and 3 skipped (`run/fast-20260930-001408`). So did
    the warning baseline and the logger and clock checks.
  - **Checkpoint.** Leg 3 is done in SIM:
    - Q2221, the Q2290 escort (first attempt) and Q2222;
    - 21 fights, no death, level 16 beside Nokir at Basfelt;
    - snapshot `altgard-l3` starts Leg 4, Stop 4: Basfelt Village.
    LIVE is still run once, at the end of the Altgard leg (AF-Q3). The Anti-Shock shield,
    Salvation and the help items have still never been needed in a real fight. The escort
    handler is ready for Q2284 (Stop 8).

**Endpoint (AC-Q1 (a)):**
- Q2221, Q2290 and Q2222 completed;
- alive, at Basfelt Village beside Nokir;
- the endpoint verified across a relog.

The escort trace shows at least one successful attempt.

### Leg 3 correction: Q2217 (approved 2026-10-03)

The operator authorized modifying and rerunning only the individual owning leg. Leg 3 already visits
Olenja for Q24111 and ends in Basfelt, where Gefion receives Q2217. The incoming `altgard-l2` has Q24012
COMPLETE. Java `QuestTemplate.getRequiredConditionCount` requires one of the finished-quest condition
groups, and `XMLStartCondition.checkFinishedQuests` requires every quest inside that chosen group.
Q2217 therefore accepts Q24012 **or** legacy Q2013; the old classifier incorrectly required both groups.
The shipped handler, data and C# runtime already match Java and need no server correction.

**Pickup and first village arrival (confirmed 2026-10-03).** Leg 3 takes Q2217 from Olenja at
Moslan alongside Q24111, carries the letter through the Manir/Groken work, and delivers it to
Gefion on the first planned arrival in Basfelt. This is Leg 3's endpoint visit, before Leg 4
starts; "final Basfelt visit" in the original correction evidence means the end of Leg 3,
not a later return to the village. The retained `ac-q2217-capture-a1` trace confirms the first
Basfelt entry during `af-012-talk`, followed by Nokir's Q2222/Q24111 hand-ins and Gefion's
Q2217 COMPLETE during that same visit. The existing route already meets the requested order.

- [x] **AC-C1 — Correct the classifier and Leg 3 route.** Regenerate the classifier and Q2217's plan;
  accept beside Q24111, retain the final Basfelt hand-in order, and require all five Leg 3 completions.
  Verify prerequisite alternatives, the contract/decision sequence, pre-commit checks and Fast.
  - *Done 2026-10-03, from `8fc13fd68`.* Q2217 is an obtainable `report_to` plan, accepted alongside
    Q24111 and held until the final Basfelt visit; all five completions are required. Classifier conditions
    now mirror Java (OR between finished groups, AND inside each); 4,351 quests are obtainable and 250
    unreachable. Dependent coverage counts and the custom-handler drafts are regenerated (11 added,
    every existing draft identical). Twelve compiler/drift tests and 149 Altgard/dialog unit cases pass;
    all seven pre-commit checks pass (`run/ac-q2217/checks-a2`), and Fast passes 82 cases/five explicit
    switch skips/all eleven scenarios (`run/ac-q2217-fast`). Initial stale-count/compiler and stale-draft
    check failures are retained under `run/ac-q2217`. No server behavior, quest data or historical snapshot
    changed. The isolated committed-code replay is AC-C2.
- [x] **AC-C2 — Rerun only Leg 3.** Restore `altgard-l2`, run the natural journey with `AF_ALTGARD=l3`
  and the snapshot runner's environment, verify Q2217 once, consumed letter, all incoming quests and
  the Basfelt endpoint/relog, then drop the owned schema. Capture from committed code under the distinct
  name `altgard-l3-q2217`, restore-check it, and record results. Never overwrite historical snapshots or
  rerun other legs. The historical `altgard-l3` and downstream/Haramel snapshots still omit Q2217; this
  correction supplies separate Leg 3 proof, not a rebased downstream character.
  - *Done 2026-10-03, committed code `3eebf55cf`.* `ac-q2217-capture-a1` restores `altgard-l2` and
    passes the natural Leg 3 test on its first attempt. Decisions 1/2 take Q24111/Q2217 at Olenja;
    decisions 13/14 hand them in at Nokir/Gefion after the Manir/Groken/Karl work. All 66 incoming
    completions survive, exactly five quests are added (71 journals), Q2217 has completion count one,
    its letter is consumed and 100 Lesser Power Shards are retained. Cleric 16 is alive beside Nokir
    (1779.88, 690.477, 264.309), still bound at the fortress; Q24013 START/0 and Q24014–Q24016 LOCKED/0
    are unchanged. Zero deaths; Groken reaches his boat on attempt one (18.051 s, longest gap 7.014 m).
    Elapsed 944,879 game ms. There are no purchases.
  - Snapshot `altgard-l3-q2217` is captured from compiled/committed `3eebf55cf`, source
    `natural-altgard-l3`, dump SHA256 `777a239a8758990e4b88a9fadb69289f119b69dc5526529fdf80818fddd8a4a5`.
    `ac-q2217-restore-a1` restores that dump with `sim-snapshot.ps1 -Action Restore`, selects only
    `AF_ALTGARD=l3`, and passes the actual endpoint relog with the same 71 journals and consumed letter.
    The capture and restore schemas are dropped; the owned-schema audit is empty. All 13 pinned files
    in `altgard-l2`, historical `altgard-l3`/`altgard-l12`, and `altgard-haramel-l12` retain their hashes.
    Audit: `run/ac-q2217/audit.json`; original logs/traces: `run/snapshots/_capture/ac-q2217-capture-a1`
    and `run/ac-q2217/ac-q2217-restore-a1`. All seven final pre-commit checks pass
    (`run/ac-q2217/proof-checks`); AC-C1's Fast evidence applies to the unchanged implementation.

## Leg 4: Basfelt Village (level 16–17) — proposal

**Status (2026-09-30): approved** (AB-Q1..AB-Q5 as recommended; see "Blocked / questions"). Leg 4
also takes in Q2227, Q2291 and the Q24013 campaign (AB-Q4). It binds at Basfelt on arrival
(AB-Q5, the standing bind policy).
It starts from the `altgard-l3` snapshot: character 133297, a level 16 Cleric at 848,030 XP,
beside Nokir in Basfelt Village, bound at Altgard Fortress. Q24013 (Stop 6's campaign) is
started and stays outside this leg.

### The hub and its grounds

Basfelt Village (zone `BASFELT_VILLAGE`) has guards, vendors and **an obelisk (700066)**, so the bot
can bind here (AB-Q5).

- **Within 55 m of Nokir** (1780, 690): Hunmir, Garuntat, Gilungk and Gefion.
- **Further out:**
  - **Lamir** (75 m) and **Shania** (102 m) are **walkers**: talk to them where the client sees
    them, as with Tulberg and Rion.
  - Tatural stands 106 m east and Skanin 168 m east.

The quests reach out in four directions:

| Ground | Where (distance from Nokir) | Quest use | What lives there |
|---|---|---|---|
| East: Vovetirn and the amphas | Vovetirn (1990, 640), 216 m; poisonsac amphas (L15–16, 30 spots) 160–620 m | Q2239 | sprigg outlaws (L15) beside Vovetirn |
| South: the starved and fierce mosbears | (1732, 526) and (1702, 488), 60–460 m | Q2288, Q2230, Q2289, Q2224 | lake spirits (L13) |
| South-west: Gornak, Brodir and Sumarhon | Gornak (1790, 576), 115 m; Brodir (1512, 528), 313 m; **Comrade Sumarhon** (SEASONED L15) at (1305, 483), on a height (z 333), 518 m | Q2226, Q24112 | Sumarhon's camp: 5 grave robbing fencers and 4 sentries (L14, LEHPAR); soil spirits (L13) by Brodir |
| West, near: the swamp and tusked mosbears and cubs | centred at (1565, 670) and (1426, 772), 160–440 m | Q2224, Q2230, Q2288 | mosbears L13–15 (MOSBEARFATHER), cubs (MOSBEARBABY) |
| West, middle: the rainbow slimes and the Old Incense Burner | slimes (L14, 11 spots) about (1515, 863); burner (1543, 882); Infernus spawns at (1547, 894); about 300 m | Q2225, Q2223 | rainbow slimes (3 beside the burner) |
| West, far: the beehives, Komu and Kaibech | 20 beehives (1148–1261, 873–922), 475–660 m; **Komu Silverclaw** (SEASONED L17, **respawn 1 hour**) at (1287, 987); Kaibech (1201, 985), 649 m, Stop 5's hub | Q2232, Q2289, Q2231 | angry mosbears (L17), bigfoot mosbears (L16), ruthless mosbears (L15), grove malodors (L16), crested pluma (L16), nimble arachnalings (L15) |
| North-west: Karl and Gunmarson | Karl (1749, 901), 213 m; Gunmarson (1621, 747), 168 m | Q2231 | wild togs on the way (AC-02) |

Almost everything in the west is level 15–17 and aggressive to a level 16 Cleric. This is the
first leg where most fights are at or above the bot's level.

### Quests

| Quest | Name | Kind | From → to | Objective | XP |
|---|---|---|---|---|---|
| 2226 | A Cure for Crazy | template `report_to` | Garuntat → **Gornak** (Stop 6, 115 m) | deliver the Memory Quartz | 17,678 |
| 2225 | No-Good Slime | template `monster_hunt` | Hunmir | kill 5 rainbow slimes (L14) | 19,950 |
| 2224 | Lamir's New Clothes | template `item_collecting` | Lamir | 6 Mosbear's Leather, 100% from cubs and from starved and fierce mosbears | 17,678 |
| 2232 | The Broken Honey Jar | custom | Gilungk; Tatural; beehives | 9 Beehives (`quest_use_item`, 100% from var 1) | 48,600 |
| 2239 | Malodor Antidote | custom | Gilungk; Vovetirn | 3 Ampha Membranes (80%) | 12,810 |
| 2231 | Sibling Rivalry | custom | Lamir → Karl → Gunmarson → **Kaibech** (Stop 5, 649 m) | talk | 30,450 |
| 2288 | Money Where Your Mouth Is | custom, **timed 10 min** | Shania | kill 3 mosbears | 23,700 |
| 2230 | A Friendly Wager | custom, **timed 30 min**, after 2288 | Shania | 10 Mosbear Tusks (80–85%) | 37,200 |
| 2289 | Rampaging Mosbears | custom, after 2288 | Gefion; Skanin; Komu | kill 5 starved or fierce mosbears; movie 62; Skanin; Komu's Horn | 43,350 |
| 2223 | A Mythical Monster | custom (D27) | Gefion; Lamir; burner | burn the incense: **Infernus** (EXPERT L13) for 5 minutes | 23,250 |
| 24112 | No Laissez-faire for Lepharists | custom | Nokir → **Brodir** (Stop 6, 313 m) | kill Comrade Sumarhon (SEASONED L15) | 17,552 |

Quest XP is 292,218, so the Cleric ends at about 1.14M XP plus kills. That is **level 17**
(1,083,018), where Divine Touch and Infernal Blaze III arrive; AC-00's catalog already covers
them.

**The custom handlers (Java, read 2026-09-30).** The C# ports register exactly what Java
registers. The one exception is the approved D27 correction: Lamir is registered for Q2223.

- **Q2232:**
  - Tatural at var 0: SETPRO1 → var 1.
  - The beehives drop only from var 1 (`collecting_step` 1, no chance, so 100%). Each hive is
    a `quest_use_item` object that dies when used and respawns after 295 s. 20 stand in the
    far west.
  - Gilungk at var 1: CHECK_USER_HAS_QUEST_ITEM → REWARD (page 5); the item reward is 5
    Tempering Solutions.
- **Q2239:**
  - Vovetirn at var 0: SETPRO1 sets var 1 and re-sends page 10. It is **not** a dialog close.
  - At var 1, CHECK_USER_HAS_QUEST_ITEM takes the 3 membranes, gives the Ampha Antidote
    (182203227), sets var 3 and shows page 1779.
  - Gilungk at var 3: SETPRO3 → REWARD.
- **Q2231:** Karl var 0 → SETPRO1; Gunmarson var 1 → SETPRO2; Kaibech → SELECT_QUEST_REWARD
  sets REWARD. As in Q2222, the last NPC does not check the var; the bot talks to all three
  anyway.
- **Q2288, timed:**
  - Take it from Shania.
  - At var 0, QUEST_SELECT shows page 1003, and **SETPRO1 starts a 600 s timer** and sets var 1.
  - Each kill of 210436/437/440/564/581/584 moves var 1 → 4.
  - At var 4, QUEST_SELECT shows page 1352, and SELECT_QUEST_REWARD sets REWARD and ends the
    timer.
  - **The timer's end or a logout abandons the quest.**
  - A Java quirk, kept: SELECT_QUEST_REWARD is not gated by the var. The bot does the kills.
- **Q2230, timed:**
  - **QUEST_ACCEPT_1 starts a 1,800 s timer.**
  - 10 Mosbear Tusks drop at 80% from 210564/581/584 and 85% from 210436/437/440.
  - CHECK_USER_HAS_QUEST_ITEM with the timer running and the tusks gives REWARD.
  - **After the timer ends**, the check takes every tusk away and shows page 3057. SETPRO1 then
    starts a new 1,800 s chance.
  - A logout takes the tusks and abandons the quest.
- **Q2289:**
  - Kills of 210564/210584 move var 0 → 5.
  - Gefion at var 5: SELECT2_1_1 plays **movie 62** (page 1354), and SETPRO2 → var 6.
  - Skanin at var 6: SETPRO3 → var 7 and gives the Hunter's Secret Remedy (182203017).
  - **Komu's Horn drops only from var 7** (`collecting_step` 7, 100%) from Komu Silverclaw.
    He respawns only once an hour.
  - Gefion at var 7: CHECK_USER_HAS_QUEST_ITEM → REWARD.
- **Q2223 (D27):**
  - Gefion starts it.
  - Lamir at var 0: SETPRO1 gives the Bundle of Incense (182203217) and sets var 1. At var 1
    without incense, Lamir gives another (page 1779).
  - The Old Incense Burner (700134) with the incense: `useQuestObject(…, movie 67, die)`.
  - The movie's end **spawns Infernus (211621, EXPERT L13, aggro 15 m) at (1547.1, 894.3) for
    five minutes**. Killing him sets REWARD.
  - At REWARD, Gefion's talk shows page 2375, then page 5.
  - The shipped data has no start condition. The D27 test ran with Q2231 and Q2224 COMPLETE.
    AL-Q6 noted that Lamir's own quests can mask Q2223's page; AB-05 settles it.
- **Q24112:** Nokir starts it. Killing Sumarhon (210510) → var 1. Brodir (832821) at var 1:
  QUEST_SELECT shows page 2375, and SELECT_QUEST_REWARD sets REWARD.

### Follow-ups, level gates, and what this port does not have

The operator recalled that Basfelt has many follow-up quests, which open after turn-ins or at
a level, especially the gold (IMPORTANT) and campaign quests. A sweep found them. It covered
every quest in the shipped data whose giver stands within 130 m of Nokir (any zone), and every
quest whose prerequisite is a Stop 4 quest.

| Opens after | Quest | Giver | Where the work is | Where it sits |
|---|---|---|---|---|
| Q2288 | Q2230, Q2289 | Shania, Gefion | the mosbears | Leg 4 |
| Q2226 (at Gornak) | **Q2227** A Crazy Request (IMPORTANT, L13) | Gornak, 115 m | 3 Spirit Crystals, 100%, from lake spirits (L13, tribe MONSTER, so passive to Asmodians; 22 spots at (1747, 510), beside the starved mosbears) | Stop 6; **AB-Q4** |
| Q2227 | **Q2291** Report to Garuntat (IMPORTANT, L13) | Gornak | deliver to Garuntat in Basfelt | Stop 6; **AB-Q4** |
| Q2231 (at Kaibech) | Q2235 Clearing the Path | Kaibech, 649 m | bigfoot mosbears | Stop 5, Leg 5 |
| Q24112 (at Brodir) | Q2236, Q2237, Q2292 (Anmurnerk); Q2238 (Brodir) | Idun's Lake, 311 m | MuMu at L15–16 | Stop 6, Leg 6 |
| Q24012 (already started) | **Q24013** Poison In the Waters (campaign, L14; 55,537 XP and a weapon) | automatic | 1. Nokir; 2. Shania gives the Hunter's Poison; 3. use it in zone `DF1A_ITEMUSEAREA_Q2016` (1675, 234), 468 m south: two Feral Black Claw Sharpeyes (L17, SEASONED) spawn; 4. four kills of SEASONED black claws (L15–16, LYCAN) there; 5. Nokir | Stop 6; **AB-Q4** |

**Level gates:** nothing at Basfelt opens at a level between 16 and 20. The campaign's next
missions, Q24014–Q24016, open at level 20, and AL-1 puts them in the next leg. Q2289 (13),
Q24112 (14) and Q24013 (14) are already open.

**Not in this port yet, and what D32 added.** The data names more Basfelt follow-ups than the
server had handlers for; Java has none of them. D32 (`docs/retail-quest-completion.md`) adds the
ones 4.8 retail ran:
- **Added by RQ-05 (2026-09-30), so a leg can take them in:** **Q24113** Sword to Secrecy,
  **Q24232** Little Help from a Daeva and **Q24233** Adieu to You, Manumumu, all after Q24112;
  and Q24110, Q24111 and Q24115. Each is a template quest with a SIM test
  (`RetailQuestPlaysEndToEnd`) and a compiled plan in `parity-artifacts/e2e/retail-quest-plans/`.
- **Intentionally excluded (2026-10-03):** the maintainer chose not to implement Q24114's
  missing custom handler and Umkata summon. RQ-05's proposal is declined.
- The older campaign missions Q2011–Q2022 stay out: they are pre-4.0, and the 4.x quests exclude
  a character who did them.

These are probably much of what the operator remembers. As before, the hub-style engine looks
again after every hand-in, so a follow-up inside the leg's scope is taken as soon as it opens.

### What is new for the bot

1. **Timed quests** (Q2288, Q2230). The timer runs on the server, and the client sees it in
   `SM_QUEST_ACTION`. Nothing may lose the quest: no logout, relog, long rest, or restock trip
   while a timer runs. The bot:
   - starts a timer only at the edge of its grounds (Q2288's SETPRO1) or with everything else
     ready (Q2230's accept);
   - hunts to a budget: the kills or drops still needed, times the expected seconds per fight
     and rest, plus the walk back, must fit the time left;
   - on Q2230's expiry, spends the "new chance" deliberately (AB-Q2).
2. **A spawned EXPERT** (Q2223): burn only at full HP and MP, buffed, with the shield ready,
   then fight Infernus inside his five minutes. A timeout or death means fresh incense from
   Lamir and 295 s for the burner. This is the most likely first real use of the Anti-Shock
   shield and Salvation.
3. **Kill counters and var-gated drops** shared across quests. The south mosbears serve Q2288,
   Q2230, Q2289 and Q2224 at once, so they are hunted as one batch after Q2288.
4. **A monster to leave alone** (AB-Q3): Komu Silverclaw's horn only drops at Q2289 var 7, and
   he respawns hourly. Killing him early costs an hour.
5. **Hand-ins at other hubs** (AB-Q1): Kaibech (Stop 5), Gornak and Brodir (Stop 6).
6. **SEASONED targets in camps:**
   - Comrade Sumarhon (L15) among 9 Lehpar on a height;
   - Komu (L17) among level 16 bigfoot mosbears and malodors.
7. **Walking NPCs at the hub:** Lamir and Shania.

### Leg 4 TODO list (proposed)

The same loop protocol, with "AB" in place of "NA".

- [x] **AB-01 — The Leg 4 contract and plans.** `natural-altgard-l4-contract.json` with:
  - the hub (Basfelt, Nokir), the fortress as town, and the grounds above as areas;
  - the eleven quests, Q2227, Q2291 and the Q24013 campaign (AB-Q4), and their order;
  - the bind at the Basfelt obelisk on arrival (AB-Q5);
  - the three compiled template plans (Q2225, Q2224, Q2226);
  - the scripted steps of the eight custom quests, including Q2239's page-10 SETPRO1;
  - the object uses: the beehives, and the incense burner with its movie;
  - the var-gated collections: the membranes, and Komu's Horn at var 7;
  - a new **`timers`** block (Q2288: 600 s from SETPRO1, abandoned on expiry or logout; Q2230:
    1,800 s from the accept, a new chance by SETPRO1, the tusks taken at the late check);
  - a new **`spawns`** block (Q2223: Infernus at (1547.1, 894.3) for 300 s after movie 67);
  - an **`avoid`** entry (Komu until Q2289 var 7);
  - the endpoint (AB-Q1).

  Contract tests against quest data, spawns, NPC templates and the eight C# handlers.
  - *Done 2026-09-30.* `parity-artifacts/e2e/natural-altgard-l4-contract.json` is generated from the
    shipped spawn and NPC data, so positions and talk ranges (6 m) are copied, not typed. It holds:
    - the start (`altgard-l3`, level 16, Q24013 started);
    - Basfelt as hub **and** town, and the bind at the Basfelt obelisk 700066 on arrival (AB-Q5);
    - 14 areas;
    - the 14 quests in order: Q2226 → Q2227 → Q2291 first, then Q2225, Q2224, Q24013, the Q2288
      batch, Q2232, Q2239, Q2231, Q2223 (after Q2231 and Q2224) and Q24112;
    - 28 dialog steps for the 9 custom quests;
    - the beehive object use;
    - four var-gated collections, among them Komu's Horn at var 7;
    - the new sections:
      - **`hunts`**: the custom kill counters of Q2288 (vars 1→4), Q2289 (0→5), Q24112 (0→1)
        and Q24013 (3→7, then a fifth kill);
      - **`timers`**: Q2288 600 s from SETPRO1, abandoned; Q2230 1,800 s from the accept, with
        a SETPRO1 new chance and the tusks taken at page 3057; both abandoned on logout;
      - **`spawns`**: Infernus 211621 for 300 s after movie 67 at the burner, and Lamir's
        refill incense;
      - **`avoid`**: Komu 210442 until Q2289 var 7, with a 3,600 s respawn;
      - the Q24013 poison as an item use bound to zone `DF1A_ITEMUSEAREA_Q2016_220030000`,
        which spawns two 210457;
    - three reward choices (see below);
    - the endpoint: every quest, in the hub, bound at 700066.
  - **The reward choices:**
    - Q24013: the Altgard Legionary Staff (the Cleric's weapon kind), SELECTED_QUEST_REWARD8;
    - Q2288: Shania's Crystal Ring (+28 MP);
    - Q2223: Gefion's Crystal Earrings (+10 magic boost).
    Q2225's belt is left to the template runner's own choice.
  - The five template plans (Q2226, Q2227, Q2291, Q2225, Q2224) are compiled into
    `natural-altgard-l4-plans/`.
  - `NaturalAltgardContract` loads `l4` with new records, each checked on load against the
    quests, steps and areas: `Bind`, `Hunt`, `Timer`, `Spawn` and `Avoid`, a zone-bound
    `ItemUse`, `Endpoint.BindNpcId`, and a list of reward choices.
  - Five `NaturalAltgardLeg4ContractTests` check it against quest data, spawns, NPC templates, zones,
    items and the nine C# handlers: registrations (constants included), pages, movies,
    given items, timers, kill counters, the Infernus spawn, the poison's zone and spawns,
    drops and chances, and the beehives. All 325 Natural tests pass, and so do the warning
    baseline, the logger and clock checks, and `test-quest-plan-compiler.py`.
  - The D32 quests that now exist (Q24110, Q24111, Q24113, Q24115, Q24232 and Q24233) do not
    start in Basfelt: they start at the fortress, Moslan Crossroad, Idun's Lake, Trader's
    Berth and the Observatory. So Leg 4's scope is unchanged; their stops' legs take them in.
- [x] **AB-02 — Travel.** Every ground above, from the hub and back, in SIM, with the danger
  recorded. **Done when:** every leg walks. That includes Sumarhon's height (z 333) and the
  beehive grove, whose navmesh is not proven yet.
  - *Done 2026-09-30.* `SimulationAltgardLeg4TravelTests` (account 149, `run/ab02/travel.log`) plans
    each leg with the Altgard travel planner at level 16 and walks it on the live SIM server.
    All **19 legs** arrive within 5 m:

    | Trip | Legs (metres) | Live aggressive within 30 m of the route |
    |---|---|---|
    | South | Basfelt → Gornak 273, → starved mosbears 53, → Basfelt 227 | starved mosbears |
    | East | → Vovetirn 297, → amphas 26, → Basfelt 305 | poisonsac amphas |
    | West, near | → swamp mosbears 463, → the burner 255, → Basfelt 621 | swamp mosbears and cubs, rainbow slimes, wild togs |
    | West, far | → Karl 236, → Gunmarson 291, → a beehive 365, → Komu 127, → Kaibech 85, → Basfelt 935 | brownbristle, tusked, ruthless, angry (L17) and bigfoot (L16) mosbears, grove malodors, nimble arachnas, **Komu** |
    | South-west | → Brodir 545, → Sumarhon's camp 269 (**ends at z 333.2**, on the height), → the Q24013 zone 1,107, → Basfelt 879 | Sumarhon's 16 fencers and 13 sentries; around the Q24013 zone, **11 Feral Black Claw Sharpeyes (L17, SEASONED) already spawned**, black claws, and MuMu looklooks, lookouts, herb gatherers and a highsitter (L15–16) |

  - **The beehive grove, Kaibech and Sumarhon's height are on the navmesh.**
  - Two findings for the runner (AB-08):
    - A leg should start where the last walk ended. A ground spot planned beside the burner
      was rejected as a start (`GeometryRejected`); the real stopping point was not.
    - The Q24013 ground is crowded with SEASONED Feral Sharpeyes even before the poison adds
      two more, and it lies 1.1 km from Sumarhon's camp by road. The poison step will be the
      hardest fight of the leg.
  - The probe character is raised to level 30 (GM, probe only). Java's aggro rule
    (`CreatureEventHandler`: a monster attacks only a player fewer than 10 levels above it) then
    leaves it alone, so **nothing is despawned**. The shared Fast world keeps Komu (hourly
    respawn) and Sumarhon for AB-05..AB-07.
  - The warning baseline and the logger and clock checks pass.
- [x] **AB-03 — `NaturalTimedQuestPolicy`.** A pure policy and tests: when to start a timer,
  the kill and drop budget against the seconds left (client `TimerSeconds`), actions forbidden
  while a timer runs, when to turn back, and the expiry paths (Q2288 retake; Q2230 new chance
  after the tusks are taken). Also the retry budget (AB-Q2).
  - *Done 2026-09-30.* `tests/Aion.Bots/Scenarios/NaturalTimedQuestPolicy.cs`, pure, over the contract's
    `NaturalAltgardTimer`. `Decide` returns one of:
    - **take**: for Q2230 only when ready, since its accept starts the timer;
    - **start-timer**: Q2288's SETPRO1, only when ready;
    - **hunt**: while the budget fits, and on after it runs short, since stopping gains nothing;
    - **turn-in**: as soon as the work is done;
    - **new-chance**: after Q2230 expires, the check takes the tusks (page 3057), then SETPRO1;
    - **wait-for-journal**: after Q2288 expires, the server abandons the quest;
    - **wait-until-ready**, **give-up** (after three timers, AB-Q2) and **done**.
  - `Slack` is the time left minus the remaining work (units ÷ units per kill × seconds per kill),
    the walk back and a 30 s margin. `MayRest` lets a rest happen only inside that slack.
  - While a timer runs, the policy forbids logout, relog, a return scroll, teleport, and rest
    or restock trips.
  - A Java quirk, noted but not used: after Q2230 expires, pressing SETPRO1 **before** the
    check would start a new timer and keep the tusks. The page flow (check → 3057 → new
    chance) is what a player follows, so the bot does too.
  - Five `NaturalTimedQuestPolicyTests` cover:
    - one attempt fitting each timer (Q2288: 3 kills and the walk back in 600 s, over 300 s
      spare; Q2230: 10 tusks at 80% in 1,800 s, over 1,000 s spare);
    - Q2288's start, hunt, turn-in, expiry, retake and give-up;
    - Q2230's readiness gate, hunt, turn-in, new chance and give-up;
    - the forbidden actions and the rest gate.
  - All 330 Natural tests pass, and so do the warning baseline, the logger and clock checks and
    the fidelity check.
- [x] **AB-04 — The timed quests in SIM** (a level 16 probe, account 146):
  - Q2288 done inside 600 s;
  - Q2288 let expire, so the quest is abandoned, then retaken;
  - Q2230 done inside 1,800 s;
  - Q2230 let expire, so the late check takes the tusks (page 3057), then a SETPRO1 new chance;
  - a logout while timed abandons the quest.
  - *Done 2026-09-30.* `SimulationAltgardTimedQuestTests` (account 146, `run/ab04/timed.log`) is a
    level 16 probe Cleric; GM on the probe only, with the level and each target set to 1 HP. It
    proves every path:
    1. **Q2288, logout:** SETPRO1 starts the timer (the client sees 600 s in `SM_QUEST_ACTION`).
       A relog abandons the quest.
    2. **Q2288, expiry:** the quest is abandoned at 605 game s after SETPRO1.
    3. **Q2288, success:** three mosbear kills move var 1 → 4. Shania's reward came 50 game s
       after SETPRO1, and the Crystal Ring is in the bag.
    4. **Q2230, expiry:** the accept starts 1,800 s. It expired with 2 tusks; the check took
       them (page 3057), and SETPRO1 started a new timer.
    5. **Q2230, success:** 10 tusks from 10 kills in 157 game s, then COMPLETE with the tusks
       taken.
  - Shania walks, so the hand-in follows her to where the client sees her.
  - **A defect found (AB-Q6):** the timer vanished mid-hunt in the first runs. Any
    `CM_LEVEL_READY` (sent after a login, a respawn-style teleport or a revive) runs every
    enter-world quest hook. Q1044's and Q2042's hooks, and their death hooks, call
    `QuestService.questTimerEnd` unconditionally. The timer is one slot per player, so they end
    whatever timed quest is running, even for a player who never took Q1044 or Q2042. Java has
    the same code (`abyss_entry/_1044TestingFlightSkills.java`, `_2042TheLastCheckpoint.java`).
    Evidence: `run/ab04/timed-debug.log`, where a same-map setup teleport ended Q2230's timer
    and the server sent timer-0 packets for Q1044 and Q2042. The probe now walks instead of
    teleporting while a timer runs, as the bot will.
  - The warning baseline and the logger and clock checks pass.
- [x] **AB-05 — Q2223 in SIM** (account 147):
  - Lamir's step with Q2231 **still open**, to settle the masking question;
  - the burner (the use, movie 67);
  - Infernus appearing at (1547.1, 894.3) and gone after 300 s untouched;
  - a second incense from Lamir, and a real kill of Infernus → REWARD → Gefion.
  - *Done 2026-09-30.* `SimulationAltgardMythicalMonsterTests` (account 147, `run/ab05/infernus.log`) is a
    level 16 probe Cleric:
    1. **The masking question is settled: there is none.** With Q2231 taken from Lamir and left
       open, Lamir's Q2223 step (QUEST_SELECT for Q2223, page 1352, SETPRO1) still moves var 0
       → 1 and gives the Bundle of Incense. The client picks the quest on Lamir's list, so his
       own quests do not hide it. Q2223 need not wait for Q2231 and Q2224.
    2. **The burner:** the use plays movie 67 and takes the incense. The burner dies, and
       Infernus spawns **at the handler's point** (1547.1, 894.3); two seconds later he is
       walking to the probe (aggro 15 m). Left alone, he is **gone after 305 game s**, and the
       quest stays at var 1.
    3. **The refill:** at var 1 with no incense, Lamir's QUEST_SELECT gives a new one
       (page 1779).
    4. **The second burn:** the burner was back (295 s respawn), movie 67 played, and Infernus
       spawned again. His HP was set low (GM on the monster, the fight is AB-07's), and a
       Smite killed him → REWARD.
    5. **Gefion:** USE_OBJECT (page 2375), SELECT_QUEST_REWARD (page 5), SELECTED_QUEST_REWARD2
       → COMPLETE, with the Crystal Earrings in the bag.
  - The warning baseline and the logger and clock checks pass.
- [x] **AB-06 — The other scripted quests in SIM** (account 148):
  - Q2232's Tatural step and a beehive loot;
  - Q2239's Vovetirn page-10 SETPRO1 and the antidote check;
  - Q2289's movie 62, Skanin's remedy, and the horn at var 7 (Komu's HP set low; the fight is
    AB-08's);
  - Q2231's three talks.
  - *Done 2026-09-30.* `SimulationAltgardBasfeltScriptTests` (account 148, `run/ab06/scripts.log`) is a
    level 16 probe Cleric. Every contract step moved its quest:
    - **Q2231:** Lamir → Karl (var 1) → Gunmarson (var 2) → Kaibech → COMPLETE.
    - **Q2232:** Tatural (var 1), then **nine beehives**, each a 3 s use and a loot at 100%, then
      Gilungk's check → COMPLETE.
    - **Q2239:** Vovetirn's SETPRO1 (page 10, not a close) → var 1. Three Ampha Membranes
      came from 4 kills (80%). The check sets **var 1 → 3** and gives the antidote, and
      Gilungk's SETPRO3 → COMPLETE.
    - **Q2289:** five starved or fierce mosbears (var 0 → 5), then Gefion's SELECT2_1_1 with
      **movie 62** (var 6), then Skanin's SETPRO3 (var 7 and the Hunter's Secret Remedy).
      **Komu Silverclaw** was killed at var 7 and **dropped the horn**, and Gefion's check →
      COMPLETE.
    - **Q24013:** Nokir (var 1), then Shania (var 2, the Hunter's Poison). The poison used
      inside `DF1A_ITEMUSEAREA_Q2016` sets **var 3**, and the Feral Black Claw Sharpeyes within
      20 m went from 5 to 7: the handler's two.
  - GM on the probe only: its level, Q2288 set COMPLETE, targets set to 1 HP, and setup
    teleports. Q24013 was started server-side and sent with `SM_QUEST_ACTION` ADD, as the
    snapshot has it started.
  - The shared `TalkAsync` now accepts a step that moves the var by more than one (Q2239's
    check), and a movie on any `SELECTn_n…` action (Q2289's SELECT2_1_1).
  - All 330 Natural tests pass, and so do the warning baseline and the logger and clock checks.
- [x] **AB-07 — Combat at level 16–17.** A focused SIM encounter (the NA-23/AC-00 harness)
  against the Leg 4 groups:
  - a bigfoot mosbear with a malodor;
  - Komu Silverclaw;
  - Comrade Sumarhon with his camp;
  - Infernus;
  - Q24013's two spawned Feral Sharpeyes (L17 SEASONED) and a SEASONED black claw.

  Record the shield, Salvation, Divine Touch and every death. **Done when:** each fight runs
  once and the findings are written here. Deaths are recorded, not failed.
  - *Done 2026-09-30.* The NA-23/AC-00 encounter takes `AB07_STAGES=1`. The runtime's new
    `EncounterStages` names five Leg 4 stages on the open ground outside the fortress, spawned by
    GM on the probe world. The Cleric fights with the journey's own combat, pull planner, rests
    and help items. Runs `run/ab07/ab07-cleric-encounter-l16-s1` and `…-l17-s1b`:

    | Stage | Level 16 | Level 17 |
    |---|---|---|
    | bigfoot mosbear + grove malodor (L16) | 2 kills, lowest HP 80% | 2 kills, 81% |
    | Komu Silverclaw (SEASONED L17) | 1 kill, 69% | 1 kill, 76%, Divine Touch |
    | Sumarhon (SEASONED L15) + 2 fencers | 6 kills, 58% | 6 kills, 47%: **Salvation** (the first real use), the shield, Divine Touch ×2 |
    | Infernus (EXPERT L13) | 1 kill, 91% | 1 kill, 80%, Divine Touch |
    | 2 Feral Sharpeyes (SEASONED L17) + a black claw warrior (SEASONED L16) | 5 kills in 283 s, 22%: **the Anti-Shock shield** (the first real use), then Root and a retreat | 1 kill, **1 death** at 19%; the shield and Divine Touch ×2 |

  - **Findings:**
    - The shield and Salvation finally fired in real fights, and Divine Touch arrived at
      level 17 (AC-00).
    - Komu, Sumarhon's camp and Infernus are safe at level 16–17.
    - **The Sharpeye group is the dangerous one**: a retreat at level 16, a death at level 17.
      Q24013's poison spawns two Sharpeyes 13 m away, among the 11 already standing there, so
      the runner (AB-08) should pull them apart where it can. Deaths there are expected and
      recorded (OD-12).
  - **A probe fix:** the encounter set the bind point on the server only. The client still
    believed its bind was in Ishalgen, and after the level 17 death the revive waited for a
    world reload that never came. The test now sends `SendObeliskBindPoint`, as binding at an
    obelisk does. The natural bot binds through the obelisk dialog, so it never had this
    problem.
  - All 330 Natural tests pass, and so do the warning baseline and the logger and clock checks.
- [x] **AB-08 — The Leg 4 runner.** Engine actions for:
  - timed hunts (AB-03's policy);
  - the spawn-and-kill (burn when ready, fight inside the window);
  - the Komu avoidance;
  - the batches: the mosbears after Q2288, the west trip, the south-west trip.

  A smoke run from `altgard-l3` with real combat, recording every timer, spawn, fight and death.
  - *Done 2026-09-30.* Run `run/af-l1/ab08-l4-smoke` (seed 1, from `altgard-l3`) plays the whole leg in
    67 decisions and 4 h 32 min of game time. It binds at Basfelt first (AB-Q5), then does
    all 14 quests. It ends at the Basfelt obelisk at level 19 (from 16), and the relog check passes.

    | What | Outcome |
    |---|---|
    | Q2288 (600 s) | handed in on the first timer, 3 s to spare |
    | Q2230 (1,800 s) | the first timer ran out at 7 tusks to go; the new chance (page 3057, SETPRO1) started a second, handed in with 89 s to spare |
    | Q2223 Infernus | burned and killed on the first try |
    | Q24013 poison | used inside the zone on the second walk in |
    | Q24112 Sumarhon | killed on the seventh try, in a fight-through in his camp |
    | Deaths (recorded, OD-12) | 10: Sumarhon's camp 6, the Q24013 black claws 3, the Q2230 grounds 1 |

  - **What the runner does:**
    - binds at the hub;
    - drives both timers through `NaturalTimedQuestPolicy`;
    - burns and kills Infernus;
    - counts the custom kills;
    - waits out Komu (the avoid list);
    - uses Q2232's beehives until nine jars are looted;
    - claims Q2227's and Q24013's chosen rewards.
  - **What the smoke runs found, and fixed:**
    - **The Q24013 zone is packed with Feral Sharpeyes**: 18 spawns, 295 s respawn, and no route
      keeps clear of them. The contract now carries the zone's polygon and height band, exactly
      as in `zones_220030000.xml`, with a test. The runner walks the travel planner's road,
      fights what engages, and uses the poison at the first step 3 m inside the zone.
    - **A decision repeated because the Cleric died on it is a retry, not a stall**: up to six per
      decision.
    - **Each beehive use is progress**: the reason now carries the looted count.
    - **A kill that counts in a fight-through ends the hunt**: Sumarhon fell as an attacker on
      the way to his spawn.
    - **An attacker a retreat left out of reach is waited for, then left to the next plan.**
    - **No navigator route to a talk NPC**: the runner takes the planner's road. When the ground
      the fight left it on has no road at all, it casts Return to the bound obelisk first.
    - **The leg ends at the hub's own obelisk**, not the one it started bound at.
    - **The inventory policy now knows every Asmodian quest's selectable rewards**: Q2227's
      claim had failed without them.
  - **AB-Q6 in practice:** every revive sent the Q1044/Q2042 timer reset. Neither timed quest
    lost its timer that way: the Q2230 death came at 01:45:00, after its first timer had already
    run out (01:42:52), and no death fell inside a running timer.
  - All 332 Natural tests pass, and so do the warning baseline and the logger, clock and
    fidelity checks.
- [x] **AB-09 — One SIM run of Leg 4 and the snapshot.** `sim-snapshot.ps1 -Leg l4 -From
  altgard-l3` captures `altgard-l4` after a clean run, with a restore check.
  - *Done 2026-09-30.* `sim-snapshot.ps1` accepts `-Leg l4`.
    `-Action Capture -Name altgard-l4 -AltgardLeg1 -Leg l4 -From altgard-l3` restored `altgard-l3`,
    resumed character 133297 with `AF_ALTGARD=l4`, and dumped only a verified
    `altgard-l4-completion.json`.
  - Run `snapshot-altgard-l4-s1` (seed 1) repeats the AB-08 smoke run exactly, as a deterministic SIM
    should: all 14 quests, level 19, 10 recorded deaths, bound at Basfelt, 16,358 game s, and the
    endpoint verified across the relog.
  - Snapshot `altgard-l4`: `run/snapshots/altgard-l4` (git-ignored), character 133297, elapsed
    39,998,229 ms, dump SHA-256 `04107500…7e427`, from commit `9864edc7f`.
    - A test restore into an owned schema succeeded and was dropped; no owned schema is left.
    - `-Action Verify` does not apply here: it replays the Ishalgen journey, so it fails on any
      Altgard snapshot. Altgard legs are checked by Restore and Drop, as AC-07 was.
- [x] **AB-10 — The full `CLAUDE.md` checklist and a checkpoint.**
  - *Done 2026-09-30.* Every command in the `CLAUDE.md` checklist was run in order, on `09dd6c8df`:
    - `dotnet build` passed.
    - `dotnet test` passed: GameServer 4,411 (16 skipped), Commons 303, LoginServer 135, ChatServer 41,
      Simulation 145.
    - The warning baseline passed, and so did the logger, clock-read, custom-quest-draft and
      fidelity checks.
    - All twelve Python e2e contract tests and all twelve PowerShell contract tests passed.
    - The NavBake `check --maps baked` passed.
  - **`run-fast` found one AB-04 probe defect, fixed here.**
    - In the shared Fast world the probe waited out Q2230's 1,805 s timer on the mosbear
      grounds. The mosbears it had killed respawned beside it and killed it, so step 5 could
      not shoot anything down.
    - The probe now walks to Shania before the wait (a walk, not a teleport, because of AB-Q6).
      Each failed kill try now says why.
    - Run `fast-20260930-232231`: AB-04 passed, with 10 tusks from 14 kills in 123 game s.
  - **`run-fast` still fails one scenario, which is not Leg 4's.**
    - E1 fails: "Fresh simulation account sim-player-42 already has a character."
    - The D32 register `parity-artifacts/e2e/retail-quest-implemented.json` gives Q24110
      `simAccount` 42. That is E1's gathering account, and `RetailQuestPlaysEndToEnd` runs
      first in the same shard.
    - It is left to the D32 work, which owns that register; a separate task was offered for
      it. Every other Fast test passed (8 passed, 1 failed, 2 not started after E1).
  - **Checkpoint:**
    - Leg 4 plays end to end in SIM from `altgard-l3`.
    - Snapshot `altgard-l4` is the start of the next leg: level 19, bound at Basfelt.
    - AB-Q6 is still open for the operator (answered 2026-10-01: D33).
    - LIVE runs once, at the end of the whole Altgard leg.

**Leg 4 follow-ups (2026-10-01, the maintainer).** Three bot defects behind most of the AB-08 deaths
and the slow Q2230. Each was fixed in the runner, then all three were proven together in one run.

- **Rest after every bind revive.**
  - The defect: `RestAsync` revived a dead Cleric and returned without resting. The walk then
    carried on at 25% HP, with soul sickness lowering the maximum. Deaths 7–10 of AB-08 came this
    way, all at (1580, 575): one loop against level 13 swamp mosbears.
  - The fix: `ReviveAtBindAsync` now rests at the obelisk before it returns (`rest-after-revive`
    in the trace).
- **The general quest-loot rule** (the maintainer's).
  - After any kill, once nothing is attacking, the bot opens every nearby corpse the server
    marked lootable for it (`SM_LOOT_STATUS` LOOT_ENABLE, within 35 m, on a safe path).
  - It takes every QUEST-group item. A quest item drops only while its quest needs it.
  - It covers the target, adds, fight-throughs, patrols and navigation defence, on every leg.
  - The defect it fixes: only the monster a step aimed at was looted. Q2230 got 6 tusks from 21
    mosbears that drop them 85% of the time, and gave up after three timers.
  - `TryLootCorpseItemAsync` counts an item the sweep already took.
- **A timed hunt goes for the nearest source kind.** It takes a live one in view, otherwise the
  kind whose spawn is nearest, not always the first of Q2230's six mosbear kinds.
- **A timed quest waits for mana before the timer starts.** The powder rest stops at 65% mana and
  the timer wants 80%. The wait spun 300 rounds without the clock moving; it now lets mana come
  back.
- **Evidence: run `run/af-l1/ab08-l4-smoke`** (seed 1, from `altgard-l3`):
  - all 14 quests done, the endpoint verified across the relog;
  - **0 deaths** (AB-08: 10);
  - Q2288 and Q2230 each on their first timer, Q2230 in 5 game min;
  - 2 h 27 m of game time (AB-08: 4 h 32 m), ending at level 18.
  - On the way: the revive fix alone took a run to 3 deaths, and the general sweep plus the
    nearest kind finished Q2230 on its first timer in a focused run.
- **`altgard-l4` recaptured (2026-10-01)**, after the maintainer's Altgard spawn refinements
  (`fd4b2a3cb`, MuMu Village and Mohen). The Altgard travel graph and geo golden were regenerated
  for them in `70ec513b7`.
  - The capture found one more bot defect. Smite on a grave-robber fencer that had just stopped
    being a valid target was refused with `STR_SKILL_TARGET_IS_NOT_VALID`, which the cast code did
    not know. The cast timed out, and the client's casting gate stayed shut, so the next walk threw
    "CM_MOVE is blocked while Casting".
  - The fix: `BotCastProtocol` counts that message as a refusal, and a cast that times out always
    releases the gate.
  - Run `snapshot-altgard-l4-s1` (seed 1, from `altgard-l3`):
    - all 14 quests, level 18, bound at Basfelt, the endpoint verified across the relog;
    - 3 deaths, all in Q24112: one in Sumarhon's camp, two on the way back past the swamp mosbears
      and shade togs, each followed by a rest at the obelisk;
    - Q2288 and Q2230 on their first timer;
    - 2 h 49 m of game time.
  - Snapshot `altgard-l4` (git-ignored): character 133297, elapsed 33,774,001 ms, dump SHA-256
    `ddc5accc…0720f`, from `70ec513b7`. A test restore succeeded and was dropped. The AB-09 snapshot is
    kept locally as `altgard-l4-ab09`.
  - `run-fast` (`fast-20261001-093208`) passes: 11 of 11, E1 included after the D32 account fix.
    - Its AB-06 probe needed one change: its shoot-down reported Komu killed while the server still
      had him alive, so there was no horn to loot.
    - The probe now counts a kill only when the server has the monster dead.

**The D32 catch-up (2026-10-01, the maintainer).** The six D32 quests were added to the route. Two of
them belonged to legs that were already done, so those legs were replayed:
- Q24110 joined Leg 1 (`c0a54fd8f`);
- Q24111 joined Leg 3 (`c0a54fd8f`);
- every leg was then replayed in order from the `altgard` snapshot on one commit, `5083bb385`.

The replay of Leg 4 found three more bot defects, fixed in `5083bb385`:
- **A revive in the middle of a walk** left the movement on its old route. It then crossed 270 m in a
  straight line from the obelisk, back into the swamp mosbear family that had just killed the
  Cleric: six deaths on one spot. The navigator now refuses a segment that starts more than 30 m
  from the bot, and plans again from where it stands.
- **The places the Cleric died** are now 20 m hazards on that leg's routes.
- **A hunt whose approach finds no route** takes the planner road, as talk steps do.

Two `run-fast` probes now follow a walking NPC to its server position. `run-fast` passes 11 of 11.

| Snapshot | Level | Deaths | Leg game time | Quests added | Elapsed (ms) | Dump SHA-256 |
|---|---|---|---|---|---|---|
| `altgard-l12` (Leg 1) | 13 (was 13) | 0 | 2,392 s (was 1,740) | Q24110 | 18,388,001 | `e439e7d6…1091b` |
| `altgard-l2` (Leg 2) | 15 (was 15) | 0 | 4,530 s (was 4,933) | — | 22,938,122 | `3c823d24…3c9a8` |
| `altgard-l3` (Leg 3) | 16 (was 16) | 0 | 1,291 s (was 911) | Q24111 | 24,249,169 | `7c9564f1…e21e9` |
| `altgard-l4` (Leg 4) | **19** (was 18) | 3 (2 in Q2224, 1 at Q24013) | 9,790 s (was 10,134) | — | 34,059,264 | `4a974165…9c128` |

What the catch-up changed:
- Each snapshot was verified across the relog, restored and dropped.
- The earlier snapshots are kept locally with `-pre-d32`, and the first catch-up round with `-catchup1`.
- **Leg 5 now starts at level 19**, at 30,020 of 506,158 XP, with Healing Grace (4203). It started at
  level 18 before.
- Legs 1–3 end at the same levels and with the same skills as before.

**Endpoint (AB-Q1 (a)):**
- all eleven quests, Q2227, Q2291 and Q24013 completed (AB-Q4);
- bound at the Basfelt obelisk (AB-Q5);
- alive, at Basfelt Village within 60 m of Nokir;
- the endpoint verified across a relog.

## Leg 5: Kaibech's Campsite, Idun's Lake and MuMu Village (level 18–19) — proposal

**PC-08 scheduled additions:** before field work, batch a capital preparation visit
with the existing cube errand (still make the quest visit if the cube is expanded).
Finish Q2917 at Lanse/Annemari, use its box to accept Q2918, return by Doman and hand
the box in at Arekedil before the hub flight back to Basfelt; preserve the Basfelt bind.
At observed level >=15, prepare Q2916 through Deyla/Zerpi/Osvi/Annju to START/3 for
Neparinerk in Leg 6. At >=19, do Q2954 once through Doman/Haven/Doman; defer it to
Leg 7 if below its gate. RC-04 proves this city preparation and return in the revised
Leg 5 checkpoint; the remaining field material is RC-05's Slime Sap.

Q2919 should already be START/4 from the post-Ishalgen city/Ishalgen preparation in
[PC-08](natural-ascension-altgard.md#scheduled-additions-to-individual-legs-pc-08-2026-10-04).
If not, prepare it and collect two Thorned Ampha Tails in Ishalgen before this field
segment. Then collect three Malodor Stamens 182207010 from Grove Malodors 210443/210444
on Kaibech's grounds and one Slime Sap 182207012 from Rainbow Slime 210435 at Idun's
Lake. Obtain every drop after acceptance at START/4. Protect all items until the
Neusa/book/Cavalorn finish in Leg 7; do not confuse these sources with Mahindel Swamp.

**Status (2026-10-01): approved** (AK-Q1..AK-Q3 as recommended; see "Blocked / questions").
Leg 5 covers Stops 5 and 6 together (AK-Q1 (b)). Q2242 and Q24233 are held for Gerger and the
fortress (AK-Q2 (a)). The bot waits for the game's night for Q2292's Love Ring (AK-Q3 (a)).

**Where it starts.** It starts from the `altgard-l4` snapshot, as replayed in the D32 catch-up
(2026-10-01): character 133297, a **level 19** Cleric at 30,020 of 506,158 XP, with Healing Grace.
It stands at the Basfelt obelisk and is bound there. Leg 4 already did Stop 6's Q2227, Q2291 and
Q24013.

**What it covers (if AK-Q1 (b)).** Stop 5, Kaibech's Campsite and Gribade Canyon, has only five
quests worth 63k XP. So this proposal also takes in the rest of Stop 6, Idun's Lake: eight
quests that opened when Leg 4 finished Q24112. Two of them are D32 quests (RQ-05).

**The bind.** Neither hub has an obelisk. The only ones in Altgard are the fortress, Basfelt,
Heart of Impetusium (700067), the east (700821) and the north (700822). So **the bot stays bound
at Basfelt**: it is the working hub under the standing bind policy, and it sits in the middle of
everything below.

### The hubs and their grounds

| Ground | Where (distance from Nokir) | Quests | What lives there |
|---|---|---|---|
| Idun's Lake: Brodir and Anmurnerk | (1512, 528), 313 m south-west | gives Q24230, Q24231, Q2238, Q24233 (Brodir); Q2236, Q2237, Q2292, Q24232 (Anmurnerk) | soil spirits (L13) |
| Sumarhon's camp: grave robbing sentries | 22 spots around (1391, 486), 439 m | Q24230 (9 kills) | Comrade Sumarhon (SEASONED L15) is back after 295 s; **3 deaths here in the Leg 4 recapture** |
| Grave robbing fencers | 31 spots around (1350, 663), 431 m | Q24231 (8 Amalgam Swords, 100%) | |
| **MuMu Village** | (1360–1650, 150–380), 480–680 m south | Q24232 (9 looklooks or lookouts), Q2238 (5 MuMu Belts, 100%, from the same), Q2236 (5 Hairpins), Q2237 (3 Fertilizer Sacks), Q2292 (three rings), Q24233 (Manumumu) | **55 looklooks** (L15–16), lookouts, herb gatherers, the six ring carriers; **Chieftain Manumumu** (L17, respawn 30 min) at (1576, 218), only **64 m from the Q24013 black claw zone** (Feral Sharpeyes, SEASONED L17) |
| Kaibech's Campsite and Gribade Canyon | Kaibech and Mantigar (1201, 983), 650 m west; plumas, arachnas and bigfoot mosbears 570 m | Q2233, Q2234, Q2235 | Komu Silverclaw (SEASONED L17, hourly) at (1287, 987); angry and ruthless mosbears; grove malodors |
| East: Vovetirn and the sprigg outlaws | Vovetirn (1990, 640), 216 m; outlaws at (2024, 542), 285 m, and (2312, 585), 542 m | Q2241 (5 Glowing Mushrooms, 80%), Q2242 | the poisonsac amphas of Q2239 |
| Far hand-ins | **Gemyu** in Gerger Village (2360, 916), 622 m east (Stop 7); **Suthran** in the fortress (1663, 1749), 1,065 m north | Q2242, Q24233 (AK-Q2) | |

**Monster levels.** Every target is level 14–17, below the level 19 Cleric. This is the first
leg where that is true. The danger is density, not level:
- MuMu Village packs dozens of RATMAN that assist each other;
- Sumarhon's camp packs LEHPAR the same way;
- Manumumu stands beside the black claws.

### Quests

All thirteen are **template quests**: `monster_hunt`, `item_collecting` and `report_to`. Java has
no custom handler for any of them; the C# templates match. Q24232 and Q24233 are D32's
(`docs/retail-quest-completion.md`, deviations 150 and 151, SIM test `RetailQuestPlaysEndToEnd`).

| Quest | Name | Level | From → to | After | Objective | XP |
|---|---|---|---|---|---|---|
| 2233 | Mantigar's Request | 13 | Mantigar | | kill 3 crested plumas (L15–16) | 8,010 |
| 2234 | An Irritating Problem | 13 | Mantigar | 2233 | kill 5 nimble arachnas (L15–16) | 11,850 |
| 2235 | Clearing the Path | 13 | Kaibech | 2231 (done) | kill 3 bigfoot mosbears (L16) | 7,920 |
| 2241 | Glowing Mushroom | 13 | Vovetirn | | 5 Glowing Mushrooms, 80%, from sprigg outlaws (L15–16) | 19,350 |
| 2242 | A Nice Gesture | 13 | Vovetirn → **Gemyu** (Gerger) | 2241 | deliver the work item 182203258 | 16,350 |
| 24230 | A Grave Situation | 14 | Brodir | | kill 9 grave robbing sentries (L14) | 17,552 |
| 24231 | Take Back, Sword! | 14 | Brodir | | 8 Amalgam Swords, 100%, from fencers (L14) | 17,552 |
| 2238 | A Matter of Pride | 14 | Brodir → **Gefion** (Basfelt) | 24112 (done) | 5 MuMu Belts, 100%, from looklooks and lookouts | 16,495 |
| 24232 | Little Help from a Daeva (D32) | 14 | Anmurnerk | 24112 (done) | kill 9 looklooks or lookouts | 17,552 |
| 2236 | Rarified Tastes | 14 | Anmurnerk | 24112 (done) | 5 MuMu Hairpins, 100%, from herb gatherers and workers | 16,495 |
| 2237 | A Fertile Field | 14 | Anmurnerk | 24112 (done) | 3 Fertilizer Sacks from the sack objects (700145, 20 spots, respawn 295 s) | 16,495 |
| 2292 | Making a New Start | 14 | Anmurnerk | 24112 (done) | the Passion, Jealousy and Love Rings from six named MuMu; a choice of two rewards (120001520, 120001521) | 16,495 |
| 24233 | Adieu to You, Manumumu (D32) | 14 | Brodir → **Suthran** (fortress) | 24112 (done) | kill Chieftain Manumumu (L17, respawn 1,800 s) | 16,495 |

Quest XP is 198,611. With kills the level 19 Cleric gets most of the way to 20, or reaches it.
AL-1's "hunt to 20" belongs to the end of the zone, not to this leg.

**Q2292's rings come by the game clock.** Each ring drops (100%) from either of two named MuMu
(BROWNIE, L16), and each named one is a single spawn that exists only in its own game hours
(`temporary_spawn`, Java `TemporarySpawnEngine`, ported):

| Ring | Carrier, hours | Carrier, hours |
|---|---|---|
| Passion | MuMu Lu (1496, 272), 05:00–22:00 | MuMu Zen (1374, 376), 04:00–21:00 |
| Jealousy | MuMu Ang (1361, 155), 05:00–22:00 | MuMu Ring (1531, 233), 22:00–05:00 |
| Love | MuMu Zoo (1643, 270), 22:00–05:00 | MuMu Di (1467, 204), 22:00–05:00 |

**The Love Ring exists only at night**:
- The game clock runs at 1 game minute per real 5 s, so night is 22:00–05:00, about 35 real minutes.
- The server announces the time in `SM_GAME_TIME`. **The bot does not decode it yet.**
- Four of the six carriers were moved in the maintainer's spawn refinement (`fd4b2a3cb`).

**Shared kills.** Looklooks and lookouts count for Q24232 and drop Q2238's belts on the same
kill, so one batch does both. Q2236's hairpins come from the herb gatherers and the MuMu workers, both
inside the village; the nearest workers are on its west edge, 480–525 m from Basfelt. The general quest-loot sweep
(`6688ba04d`) picks up every drop on the way.

### What is new for the bot

1. **Spawns by the hour** (Q2292).
   - Decode `SM_GAME_TIME`, and know which carriers exist now.
   - Hunt the day ones by day. Wait for night for the Love Ring, using the wait for other work.
   - First prove that the SIM's virtual clock drives `TemporarySpawnEngine` hour changes.
2. **A rare named target in a dense village** (Q24233). Manumumu respawns every 30 minutes,
   among RATMAN that assist each other, 64 m from the Feral Sharpeyes. Pull him away from the
   village edge.
3. **Batches by ground across quests.** For example: MuMu Village is one trip for Q24232,
   Q2238, Q2236, Q2237 and the day rings, and Sumarhon's camp is one trip for Q24230 and Q24231.
4. **Held hand-ins** (AK-Q2): a quest finished but not handed in at the leg's end, for Gemyu and
   Suthran in a later leg.
5. **Fertilizer sacks**: quest objects like Leg 4's beehives.
6. **A reward choice** for Q2292, as for Q2227 and Q24013.

### Leg 5 TODO list

The same loop protocol, with "AK" in place of "NA".

- [x] **AK-00 — The game clock in SIM and in the bot.** Decode `SM_GAME_TIME` into the world
  model. A SIM probe advances the virtual clock and shows the carriers appear and despawn at
  their hours: MuMu Lu by day, MuMu Zoo by night. If the SIM clock does not drive the hour
  change, fix the fixture (infrastructure), not the server.
  - *Done 2026-10-01.* The Java spec:
    - `SM_GAME_TIME` writes one `writeD`, the minutes since 01.01.0000;
    - `GameTime.getHour()` is (minutes % 1440) / 60;
    - `GameTimeService` adds a minute every 5 s and broadcasts the time every 3 minutes;
    - an hour change runs `TemporarySpawnEngine` (`TemporarySpawn.isInSpawnTime`).
  - **The bot:**
    - `BotServerPacketDecoder` decodes `SM_GAME_TIME`.
    - `BotWorldModel` keeps `GameMinutes`, `GameHour` and `GameTimeUpdates`.
    - There is no Java fixture: Java's packet reads the database-backed singleton. As for `SM_PRICES`,
      `GameTimePacketIsTheMinutesSinceYearZero` pins the one-field layout, and the probe checks the
      value against the running server.
  - **The SIM needed no fixture change.** `GameServerBootstrapService` starts the clock on the virtual
    thread pool and hooks `TemporarySpawnEngine.OnHourChange`.
  - **Probe `GameClockDrivesTheHourlyRingCarriers`** (account 72):
    - On entering the world, the client's minutes match the server's (within 1) and its hour is the
      server's.
    - Advanced through the clock: at 22:00 MuMu Zoo is alive and MuMu Lu is gone; at 05:00 they swap; at
      22:00 they swap back.
    - Teleported into the village, the client sees Zoo and not Lu. 77 `SM_GAME_TIME` packets arrived.
  - **Checks:**
    - `run-fast` passes 11 of 11 with the probe, which moves the shared world's clock about 43 game hours.
    - The GameServer suite passes (4,412), and so do the warning baseline and the logger, clock and
      fidelity checks.
- [x] **AK-01 — The Leg 5 contract and plans.** `natural-altgard-l5-contract.json` with:
  - the hub (Basfelt, bound), and the grounds above as areas;
  - the thirteen quests and their order;
  - compiled template plans for all of them;
  - a new **`timedSpawns`** block for Q2292's carriers and their hours;
  - the reward choice for Q2292;
  - the held hand-ins (AK-Q2).

  Contract tests check every position, drop, count and hour against the shipped data.
  - *Done 2026-10-01.* `parity-artifacts/e2e/natural-altgard-l5-contract.json` is generated from the shipped data.
    It starts where Leg 4 ends, at level 19 and bound at Basfelt, and holds:
    - 9 areas; the fencer and MuMu Village areas are cut to the spots near Basfelt;
    - the 13 quests in ground order: Idun's Lake and Sumarhon's camp, then MuMu Village, then Kaibech, then the east;
    - Q2292's choice: the Turquoise Earrings, `SELECTED_QUEST_REWARD2`, the magic-boost one;
    - a new **`timedSpawns`** block: the six carriers, with hours, drops and positions read from the spawns;
    - a new **`held`** block, with the endpoint's `heldQuestIds` [2242, 24233] (AK-Q2).
  - **Plans:** `natural-altgard-l5-plans/` holds the 13 compiled plans. The compiler reproduces the committed
    Leg 4 plans exactly.
  - **Two D32 plans were stale.** Q24232 and Q24233 still had the MuMu positions from before the spawn
    refinements, so they were regenerated (`f1e7cddbf`). `test-quest-plan-compiler.py` now fails when a plan in
    `retail-quest-plans/` or a `natural-altgard*-plans/` folder differs from a fresh compile.
  - **The loader** accepts a leg with no scripted steps when every quest is a template, and it validates the
    new blocks. `NaturalAltgardTimedSpawn.PresentAt` copies Java `TemporarySpawn.checkHour`.
  - **Correction to the plan above:** the nearest MuMu workers are on the village's west edge (480–525 m), not
    a quiet ground north-east of Basfelt. They are part of the MuMu Village area.
  - **`NaturalAltgardLeg5ContractTests`** pins:
    - the start against Leg 4's endpoint;
    - every quest's data, its template and start NPC, and its plan;
    - each target's spawn inside its area;
    - the carriers' hours, drops and positions, and that both Love Ring carriers are night-only;
    - the reward choice, and the held quests' end NPCs.
  - All 335 Natural tests pass, and so do the warning baseline and the logger, clock and fidelity checks.
- [x] **AK-02 — Walk every Leg 5 route in SIM.** As AB-02 did: Basfelt to Brodir, to each
  MuMu Village ground, to the sentries and the fencers, to Kaibech's grounds, and to Vovetirn
  and both outlaw grounds. A level 30 probe, so nothing aggroes and nothing is despawned.
  - *Done 2026-10-01.* `AltgardLeg5TravelWalksEveryGroundAndBack` (account 73, a level 30 probe; plans at
    level 19) walks 20 legs in four round trips from Nokir. Every leg ends within 5 m of its ground. Notable:
    - Basfelt to Brodir is 543 m; the sentries and fencers lie 17–52 m beyond Brodir.
    - Basfelt to MuMu Village is 596 m. Inside the village, the plans cross up to seven aggressive kinds, and
      10 or more MuMu stand within 30 m. Manumumu's ground has black claws (214032, 214039) close by.
    - Basfelt to Kaibech is 902 m, across the bigfoot (210441), ruthless (210443) and angry (210444) mosbears.
    - Basfelt to Vovetirn is 297 m. The L16 outlaw ground is 583 m further, among 14 L16 poisonsac amphas.
  - **`run-fast` found two probe defects**, both fixed:
    - **The AK-00 clock probe** advanced 400 s in one call. That can exceed the virtual pool's
      100,000-tick budget in a busy shared world, so it now advances in 30 s slices.
    - **The AB-05 probe** shot Infernus from beside him. His hits cancelled each Smite while it was cast,
      killed the probe, and he was back to full health by the next try. It had passed only when the
      first cast landed first.
      - It now shoots from 16–20 m with line of sight, at the Infernus the client targets, and logs each try.
  - `run-fast` passes 11 of 11, and so do the warning baseline and the logger, clock and fidelity checks.
- [x] **AK-03 — The carrier policy (pure).** Given the game time and what is in view, choose the
  carrier to hunt, or the work to do while waiting for the night. Unit tests at day, night and
  the hour edges.
  - *Done 2026-10-01.* `tests/Aion.Bots/Scenarios/NaturalCarrierPolicy.cs`:
    - `NaturalGameClock` keeps the client's game time: the last `SM_GAME_TIME` plus one minute per real 5 s.
      `HourOf` is Java's `getHour`. It also gives the minutes until an hour and the minutes left in a window.
    - `NaturalCarrierPolicy.Decide` hunts a carrier of a needed ring that is present now. It prefers one in
      view, and otherwise one whose window outlasts the walk there.
    - When none fits, it waits for the soonest window of a needed ring and says so, so the runner can spend
      the wait on other work.
  - `NaturalCarrierPolicyTests` (5 tests, on the Leg 5 contract's carriers):
    - the clock's arithmetic;
    - by day, Lu or Zen for Passion and Ang for Jealousy, while Love waits 10 game hours for 22:00;
    - by night, Zoo or Di for Love and Ring for Jealousy;
    - at 21:30, Lu's 30 minutes are too short for a 40-minute walk, so it waits for Zen at 04:00;
    - a carrier in view is hunted even at its window's edge;
    - every ring has a plan at every hour.
  - All Natural tests pass, and so do the warning baseline and the logger, clock and fidelity checks.
- [x] **AK-04 — Q2292 in SIM.** A probe takes all three rings at their hours (day carriers,
  then night), claims the chosen reward, and records what each carrier drops.
  - *Done 2026-10-01.* `RingCarriersByTheHourCompleteMakingANewStart` (account 74, a level 19 Cleric probe)
    accepts Q2292 at Anmurnerk and asks `NaturalCarrierPolicy` what to do from the client's own game clock.
    - The probe reached the rings at 00:00 game time. Ring (210620) and Di (210624) were present and were
      shot down; they dropped the Jealousy and Love Rings.
    - At 00:01, no Passion carrier was present, so the policy waited 239 game minutes for Zen (210622) at
      04:00. The probe advanced the virtual clock through the wait. Zen was there at 04:02 and dropped the
      Passion Ring.
    - Each carrier dropped its ring on the first kill (100%, as the data says). The probe then claimed the
      contract's choice (REWARD2, Turquoise Earrings 120001521), and the three rings were taken.
    - **For AK-08:** the runner's own reward chooser (`NaturalIshalgenInventoryPolicy.ChooseReward`) would
      take index 0 here. The runner must honour the contract's `rewardChoices` instead.
  - **Fixed: the client's game clock lagged by 25 game minutes.** The bot applied `SM_GAME_TIME` only when
    it next read its packets, and stamped it then.
    - Both transports now stamp each packet as it arrives (`DecodedBotServerPacket.ReceivedAt`).
    - The probe asserts that the client's clock is within one advance slice (8 game minutes) of the server's.
  - Line-of-sight checks for the shooting spots now look from eye height (1.6 m). A ground-level check had
    passed a spot that the server refused with `STR_SKILL_OBSTACLE`. A refused spot is skipped on the next try.
  - The decoder, world and Natural tests (425) pass, and so do `run-fast`, the warning baseline and the
    logger, clock and fidelity checks.
- [x] **AK-05 — MuMu Village and Manumumu in SIM.** One batch for Q24232 and Q2238; Q2236
  hairpins; Q2237 sacks; Manumumu killed and Q24233's kill made (see below: it stays at START).
  - *Done 2026-10-01.* `MumuVillageQuestsAndManumumuPlayThroughTheirPlans` (account 75, a level 19 Cleric probe)
    works the five quests from their compiled plans:
    - **The batch:** nine looklooks and lookouts filled Q24232's counter (9). The first five each dropped a
      MuMu Belt for Q2238 (100%).
    - **Q2236:** five herb gatherers and workers, five MuMu Hairpins (100%).
    - **Q2237:** three Fertilizer Sacks, each used and looted on the first try.
    - **Manumumu** was shot down. Q24232, Q2236 and Q2237 were then claimed at Anmurnerk, and Q2238 at Gefion;
      the quest items were taken.
  - **Q24232 and Q24233 stay at START with their counters full; they are not at REWARD.**
    - Neither is data-driven, and neither has the `reward` flag. For such quests, Java
      `MonsterHunt.onKillEvent` only fills the counter.
    - The end NPC's `SELECT_QUEST_REWARD` then moves the quest to REWARD (`onDialogEvent`), as in the
      other legs.
    - So the held Q24233 is (START, var 1). The endpoint below now says so. AK-08's endpoint check must use
      `NaturalTemplateObjective.IsDone`, not the status.
  - GM on the probe only: its level, Q24112 complete, setup teleports, and targets set to 1 HP with their
    aggressive neighbours despawned.
    - **Manumumu is spared from that clearing.** An earlier try despawned him with the looklooks around him.
      He is unique, with a 1,800 s respawn.
  - The warning baseline and the logger, clock and fidelity checks pass.
- [x] **AK-06 — Stop 5 and the east in SIM.** Q2233, Q2234 and Q2235 at Kaibech; Q2241 at the
  outlaws; Q2242 taken. Sumarhon's camp: Q24230 and Q24231.
  - *Done 2026-10-01.* `EastAndSumarhonQuestsPlayThroughTheirPlans` (account 76, a level 19 Cleric probe at
    the Leg 5 start) works the seven quests from their compiled plans:
    - **Kaibech:** Q2233 took 3 kills (210597) and Q2235 took 3 (210441). Both were claimed, then Q2234 (it
      needs Q2233) took 5 kills (210446).
    - **The outlaws:** Q2241 took 6 kills (210484) for 5 Glowing Mushrooms. One missed its 80% drop.
    - **Q2242** was taken at Vovetirn and is held at (START, 0), with its work item 182203258, for Gemyu.
    - **Sumarhon's camp:** Q24230 took 9 kills (210504). Q24231 took 8 kills (210506) for 8 Grave Robber
      Band's Amalgam Swords (100%). Both were claimed at Brodir.
  - Q24230 went to REWARD on its ninth kill. Its `monster_hunt` has `end_reward="true"`, unlike Q24232 and
    Q24233 (AK-05), so Java's `onKillEvent` sets REWARD.
    - The other monster hunts stayed at START with their counters full until their end NPC's
      `SELECT_QUEST_REWARD`.
  - The probe clears aggressive neighbours, but no longer the kinds it hunts. Clearing them had emptied
    Sumarhon's camp of its 210504s, since every camp member is aggressive.
  - The warning baseline and the logger, clock and fidelity checks pass.
- [x] **AK-07 — Combat at level 18–19.** The AB-07 encounter harness, staged:
  - MuMu Village pulls (a looklook pair with a lookout);
  - Manumumu with what assists him;
  - a sentry and fencer group.

  Record deaths and retreats.
  - *Done 2026-10-01.* The NA-23/AC-00 encounter takes `AK07_STAGES=1` (level 19, or `AC00_CLERIC_LEVEL`).
    Its three Leg 5 stages are GM-spawned on the AB-07 ground and placed as the spawn file has them. The
    Cleric fights with the journey's own combat, pull planner, rests and help items, wearing the bridge's
    Karmic Staff as in AB-07. Runs `run/ak07/ak07-cleric-encounter-l19-s1` and `…-l18-s1`:

    | Stage | Level 19 | Level 18 |
    |---|---|---|
    | MuMu pull: looklooks 210452 (SEASONED L16) and 210451 (L15), lookout 210454 (L16) | 3 kills, lowest HP 77% | 3 kills, 75% |
    | Manumumu (EXPERT L17), his lookout 4 m away, two looklooks 11 m away | 4 kills, 44%: **Salvation**, the Anti-Shock scroll, 3 potions | 3 kills (one looklook never joined), **25%**: Salvation, Healing Light ×4 |
    | A sentry (210504) and two fencers (L14) | 3 kills, 64% | 3 kills, 51% |

  - **Findings:**
    - No deaths and no retreats at either level.
    - **Manumumu is the dangerous fight.** At level 18 he took the Cleric to 25%; at 19, to 44%. The runner
      (AK-08) should take him at level 19 if it can, and pull his lookout first.
    - The MuMu Village pulls and Sumarhon's sentries and fencers are safe at level 18–19.
    - Awakening and a DP jelly were used before the first pull.
  - **How to read the trace:**
    - Each kill ends two nested encounters with the same target; count kills by target.
    - `help-item-used`'s `refused: STR_USE_ITEM` is the client's "you used" message, not a refusal (the
      count drops by one).
  - The warning baseline and the logger, clock and fidelity checks pass.
- [x] **AK-08 — The Leg 5 runner.** The engine and runner for the carriers' hours (AK-03),
  batches by ground and the held hand-ins. A smoke run from `altgard-l4` with real combat,
  recording every fight and death.
  - *Done 2026-10-01.* **Smoke run 8** (`run/af-l1/ak08-l5-smoke8`, seed 1, from `altgard-l4`) plays the whole leg
    in 46 decisions and 2 h 31 min of game time. It ends at Basfelt at level 20 (from 19), with 1 death (Q24231's
    fencers), and the relog check passes.
    - **The cube errand (AK-Q4 (a)):**
      - the road to the fortress teleporter (203581), then Pandaemonium for 706 Kinah (the 500 base after
        `SM_PRICES`);
      - expansion levels 1–4 at 798058: 27 → 36 → 45 → 54 → 63 slots for 273,000 Kinah of the Cleric's own;
      - Return to the Basfelt bind.
    - **All eleven claims**, and Q2234 and Q2242 taken as Q2233 and Q2241 finished.
    - **Q2292 by the clock:** Lu (Passion) at 09:08 and Ang (Jealousy) at 09:40 game time. Then five hour-long
      waits at Basfelt from 16:53, and Zoo (Love) at 22:45. The claim took the Turquoise Earrings (120001521).
    - **Held, as AK-Q2 asks:** Q2242 at (START, 0) with its work item, and Q24233 at (START, 1), Manumumu killed.
      The endpoint check requires both.
    - Level 20 opened Q2900, Q24014 and Q24015 (the campaign).
  - **Flight transporters (the maintainer's 2026-10-01 note: hubs have them, so do not walk).** Smoke run 9
    (`ak08-l5-smoke9`) flies Basfelt to the fortress on Hrold's route (203683, location 19, 40 s, 565 Kinah
    after `SM_PRICES`) instead of walking the 1.1 km road.
    - It plays the leg in 41 decisions and 1 h 45 min of game time: level 20, **no deaths**.
    - Arriving earlier put it in MuMu Village at 04:16 game time. Ring and Di, the night carriers, were still
      out and Zen had appeared at 04:00, so all three rings came with no wait.
    - **How it works.** Java's `TeleportService` sends `START_FLYTELEPORT`. The client flies a route from its
      own data, reporting `CM_MOVE_IN_AIR`, and lands with `LAND_FLYTELEPORT`.
      - The server's `flypath_template.xml` has no 4.8 Altgard entry (its id 19 is an Eltnen path), so the
        routes come from the client: `tools/client-extract/extract_flight_routes.py` reads the
        `FlightPath.pak` sequences.
      - It matches each route to a server FLIGHT location by its origin pad and landing teleporter, and
        writes `parity-artifacts/e2e/natural-flight-routes.json`: 140 routes, 2 locations refused.
      - Altgard has 8: the fortress (203561) to Urtumheim, Basfelt, Impetusium and the Sanctuary, and each
        back.
    - The runner takes a flight when walking to its pad and on from its landing is shorter than walking
      (`NaturalAirlineRoutes.Toward`). `NaturalServiceSteps.FlyAsync` flies the route's keys every 500 ms and
      checks the fare and the landing. `NaturalAirlineRoutesTests` pins the routes, the choice and the timing.
  - What the smoke runs before it found (below) and what was fixed. Smoke run 8 also ran with the separate
    investigation's fix for the fleeing sentry in `PatternAi.cs`, uncommitted at the time and not this item's.
    - Smoke run 5: the 1.1 km to the fortress exceeded the navigator's segment budget. The errand takes the travel
      planner's road to the teleporter.
    - Smoke run 6: a Return to a bind on another map gets `SM_PLAYER_INFO` only after the client enters the
      map. `UseLearnedReturnToBindAsync` now waits for it when it sees `SM_PLAYER_SPAWN`.
    - Smoke run 7: a fight left the Cleric 20 m above MuMu Village, where no sack had a route. When no hint has a
      route at all, the approach casts Return once and tries again from the bind.
    - **AK-Q4 (b):** an accessory still in the cube after the upgrades are worn is sold, unless the Cleric is not
      yet the level to wear it. Items an open quest needs are never worn and never sold. The gear routine had worn
      Q2292's rings.
  - All 384 Natural and decoder tests pass, and so do the warning baseline and the logger, clock and fidelity
    checks.
    - `run-fast`: every AK probe passes. Its one failure is the separate investigation's untracked, unfinished
      `FleeingUphillEndsOnTheGroundInSight`.
  - The work, in the order it was built:
    - **The engine** (`NaturalAltgardDecisionEngine`, tests in `Leg5HuntsTheRingCarriersByTheHourAndHoldsTwoHandIns`):
      - `carrier-hunt` when `NaturalCarrierPolicy` finds a ring carrier present long enough to reach;
      - a carrier wait gives way to other work, and becomes `wait-for-carrier` only when nothing else is left;
      - held quests (Q2242, Q24233) count as done once taken and worked, and are never claimed;
      - a claim needs the work done, and the leg stops (`no-game-clock`) rather than guess without the clock;
      - `town-service` when the cube has fewer than 3 free slots and the town names a merchant.
    - **The runner** executes those actions:
      - it reads the observation's game clock from `SM_GAME_TIME` as it arrived;
      - for `carrier-hunt` it kills the carrier with the journey's combat and loots its ring;
      - for `wait-for-carrier` it rests at the Basfelt obelisk, five real minutes (an hour of game time) at a time;
      - for `town-service` it sells to Gilungk (203613), the Basfelt merchant the l5 town now names, and wears
        upgrades;
      - it claims with the contract's reward choice (Q2292's earrings) over the inventory policy's;
      - and the endpoint check requires the held quests taken and worked.
    - **The cube count now follows Java.** `ItemStorage.getCubeItems` leaves out items whose template names
      an extra inventory (`<inventory id="2"/>`, the quest tab). The inventory policy's free slots now count
      only the main cube.
    - **An unreachable monster is left alone.** One that fails twice with no death or retreat goes on the
      navigator's unavailable list, and another of its kind is pulled.
  - **Smoke runs from `altgard-l4`** (`run/af-l1/ak08-l5-smoke`…`smoke4`, seed 1):
    1. Stopped at the first carrier: Lu's Passion Ring was refused with `STR_MSG_DICE_INVEN_ERROR`. The
       main cube held 35 stacks against its 27 (no expansions). Quest rewards and the help-item supply add
       past the limit; a loot does not. This led to the cube count and `town-service`.
    2. `town-service` worked: the Cleric wore its reward gear and sold 12 stacks, going from −5 to 7 free
       slots. The run then stopped in Sumarhon's camp on sentry 70357.
       - The sentry chased the Cleric and stopped 2.8 m under the ground the Cleric stood on, at the same
         XY. Its `SM_MOVE` `targetZ` was its own starting z, 284.67.
       - Every cast was refused with `STR_SKILL_OBSTACLE`.
       - The C# `NpcMoveController` matches Java's line for line here. **The cause was elsewhere, and is
         fixed (2026-10-01).** It was not a chase. At 11% HP the sentry *fled* (retail `flee_from`, the
         C#-only `PatternAi.FleeFrom`). The flee aimed at the sentry's own starting z, up a slope that rises
         from 284.67 to 288.2 (one terrain surface; C# `GetZ` finds it). The move controller snaps z to the
         ground only once a second, searching 2 m above the height it is moving towards, so it lost the
         ground for the last 6 m. The bot sent no FALL bit (0x08); its masks were 0xC0, 0x80 and 0x00. The
         flee now ends where `GeoService.FindMovementCollision` puts it, as Java's FearTask and ConfuseTask
         do. Test: `FleeingUphillEndsOnTheGroundInSight`; the old code aims at 284.67206 there.
    3. A step to sighted ground found no spot (the sentry is under the terrain), so that idea was dropped.
    4. The sentry was abandoned, and the leg ran 24 decisions:
       - Q24230, Q24231, the MuMu batch, the hairpins and the sacks;
       - **both day rings at their hours**: Lu at 05:16 game time, Ang at 05:54;
       - Manumumu, and the Kaibech and outlaw work;
       - **level 20**, no deaths up to there.
       - At decision 25 the cube was down to 1 free slot, with nothing the Cleric rules sell, and the run
         stopped (AK-Q4).
- [x] **AK-09 — One SIM run of Leg 5 and the snapshot.** `sim-snapshot.ps1 -Leg l5 -From
  altgard-l4` captures `altgard-l5` after a clean run, with a restore check.
  - *Done 2026-10-01.* `sim-snapshot.ps1` accepts `-Leg l5`.
    `-Action Capture -Name altgard-l5 -AltgardLeg1 -Leg l5 -From altgard-l4` restored `altgard-l4`, resumed
    character 133297 with `AF_ALTGARD=l5`, and dumped only a verified `altgard-l5-completion.json`.
  - Run `snapshot-altgard-l5-s1` (seed 1) repeats smoke run 9 to the millisecond: 6,295,045 ms of game time,
    level 20, no deaths. The two Q2292 night carriers and Zen came with no wait, and Q2242 (START, 0) and
    Q24233 (START, 1) are held.
  - Snapshot `altgard-l5`: `run/snapshots/altgard-l5` (git-ignored), character 133297, elapsed 40,374,309 ms,
    dump SHA-256 `d5230906…2de9ce`, from commit `53d488402`.
    - The cube has 63 slots (four NPC expansions); the Cleric has 199,603 Kinah.
    - A test restore into an owned schema succeeded and was dropped; no owned schema is left.
  - **The `PatternAi` dependency (the maintainer asked for it to be noted):**
    - Smoke runs 8 and 9 ran with the separate investigation's fleeing-sentry fix in `PatternAi.cs` still
      uncommitted in the shared checkout.
    - It was committed as `c379a18e1` ("Run flee_from over the ground, not at its starting height") before
      `53d488402`, so this snapshot is reproducible from its commit.
    - Without the fix, a Sumarhon sentry fled into the slope, out of sight (smoke runs 2–4). The runner then
      abandoned it and finished Q24230 on other sentries.
- [x] **AK-10 — The full `CLAUDE.md` checklist and a checkpoint.**
  - *Done 2026-10-01.* Every command in the `CLAUDE.md` checklist was run in order, on `72824ee0f`:
    - `dotnet build` passed, and so did `dotnet test` (GameServer 4,426 passed, 16 skipped).
    - The warning baseline passed, and so did the logger, clock-read, custom-quest-draft and fidelity checks.
    - All twelve PowerShell contract tests passed, and so did the NavBake `check --maps baked`.
    - Eleven of the twelve Python e2e contract tests passed.
  - **Two failures, both one defect that is not Leg 5's:**
    - The flee probe `FleeingUphillEndsOnTheGroundInSight` (`SimulationFleeGeoTests.cs`) came with the separate
      investigation's `c379a18e1`. It logs in as simulation account 151, which belongs to the D32 register
      (151–200, Q24110's).
    - `test-retail-quest-inventory.py` therefore fails its "no other SIM test uses the D32 accounts" check.
    - In `run-fast`, `RetailQuestPlaysEndToEnd` makes the account's character first, so the flee probe fails
      with "Fresh simulation account sim-player-151 already has a character".
    - All 11 `run-fast` scenarios and every AK probe passed.
    - The fix is a free account for the flee probe. It is left to that file's owner, as AB-10 left E1's clash
      to the D32 work.
  - **Checkpoint:**
    - Leg 5 plays end to end in SIM from `altgard-l4`.
    - Snapshot `altgard-l5` (AK-09) is the start of the next leg: level 20, bound at Basfelt, with a 63-slot
      cube and 199,603 Kinah.
    - Q2242 (Gemyu, Gerger Village) and Q24233 (Suthran, the fortress) are held for their hubs (AK-Q2). Basfelt
      flies to the fortress, and the fortress's flight to Urtumheim lands about 340 m from Gerger Village.
    - Q2900, Q24014 and Q24015 (the campaign) opened at level 20.
    - LIVE runs once, at the end of the whole Altgard leg.

**Endpoint (AK-Q1 (b), AK-Q2 (a)):**
- Q2233–Q2235, Q2241, Q24230, Q24231, Q2238, Q24232, Q2236, Q2237 and Q2292 completed;
- Q2242 started (a report_to, carrying its work item), and Q24233 started with Manumumu killed (its counter full; an old-style
  MonsterHunt stays at START until its end NPC, AK-05), both held for their hubs;
- bound at the Basfelt obelisk;
- alive, in Basfelt within 60 m of Nokir;
- the endpoint verified across a relog.

## Leg 6: Gerger Village and Trader's Berth (level 20) — proposal

**PC-08 scheduled addition:** with Q2916 START/3 from Leg 5 city preparation, visit
Neparinerk 798033 at (2693.71,1026.61,313) beside the Berth work and retain START/4
for Banatisai in Leg 9. It belongs alongside the existing Neparinerk deliveries,
not at Manir. RC-06 proves the opt-in executor and carried-state receipt alongside
the historical route; the original contained contract and snapshots retain their scope.

**Status (2026-10-01): approved** (AG-Q1..AG-Q3 as recommended; see "Blocked / questions"). Leg 6 covers
Stops 7 and 8 from the Trader's Berth bind (AG-Q1 (a)). Q24115 and Q2262 are held for their hubs (AG-Q2 (a)). Outside
04:00–21:00 the bot works other quests before Q2284's escort, then waits for 04:00 (AG-Q3 (a)).

**Where it starts.** From the `altgard-l5` snapshot (AK-09):
- character 133297, a **level 20** Cleric at 139,699 of 627,122 XP, bound at Basfelt;
- 63 cube slots and 199,603 Kinah;
- Q2242 held for Gemyu here, and Q24233 held for Suthran in the fortress.

**What it covers (if AG-Q1 (a)).** The east: Stop 7, Gerger Village, and Stop 8, Trader's Berth.
- Their quests chain: Q2248 runs from Gemyu to Neparinerk, and Q2284's escort runs from Germir to Babarunerk.
- Stop 9, the East Gate, stands at the fortress gate (1,000 m from the Berth). It belongs with the
  fortress-side Stops 10 and 11 in Leg 7, together with Q24233 (Suthran) and Q2262 (Mabrunerk at the East Gate).

**The bind.** Gerger has no obelisk. **Trader's Berth does**: 700821 at (2687, 1021), 345 m from Gerger and
within 400 m of every ground below. The bot binds there on arrival (the standing bind policy).

**Getting there: the hub flights.** The maintainer's 2026-10-01 note: fly between hubs, do not walk.
- Basfelt to the fortress is Hrold's flight (203683, 40 s, 400 Kinah base).
- The fortress to Trader's Berth is 203561's flight to "Urtumheim" (location 20, 48.5 s, 560 base). It
  lands by Trader's Berth's own transporter, 203678.
- So the trip is **two flights**, about 90 s and about 1,200 Kinah after `SM_PRICES`. Walking is 1 km by
  road (Basfelt to Gerger is 625 m), through the ksellid and ribbit grounds.

### The hubs and their grounds

| Ground | Where (from the Berth obelisk) | Quests | What lives there |
|---|---|---|---|
| **Trader's Berth** | (2676–2708, 1019–1048) | Neparinerk: Q2249, Q24115; Babarunerk: Q2251, end of Q2284; Japayerk: Q2262 (also the merchant); Sinood: Q2252 | obelisk 700821, flight transporter 203678 |
| **Gerger Village** | (2350–2395, 889–916), 345 m west | Gemyu: Q2245, Q2248, **Q2242's hand-in**; Gercus: Q2244; Germir: Q2246, Q2247, Q2284 | |
| Green ribbits | 8 + 6 spots around (2455, 795), 130 m from Gerger | Q2244 (5 kills) | L16–17, aggressive |
| Star metal ksellids | 27 spots around (2419, 686), 220 m from Gerger | Q2245 (5 kills) | L16, aggressive |
| Insignia box | (2377, 792), 108 m from Gerger | Q2246 (an object use) | |
| Gogaerunerk | (2459, 667), 230 m from Gerger | Q2247 (the disguise) | |
| Blackened angolems | 14 + 9 spots around (2550, 955), 160 m | Q2249 (5 Angolem Crystals, 80%) | L17; the shardlings (`angolem_fragment` AI) split off |
| Peckus | 34 spots around (2510, 1193), 240 m | Q2251 (5 Pecku Tail Feathers, 80%) | L18, aggressive |
| Crimsontail amphas | 10 + 7 spots, (2488, 1312) and (2517, 1120), 190–350 m | Q24115 (3 kills) | L17–18 |
| Bones of Minushan | (2407, 1268), 400 m | Q2252: Sinood's item, used at the bones, spawns Minushan's Spirit (SEASONED L18, 95%) or Drakie (L17, 5%) for 3 minutes | |
| The escort | from the disguised Germir 798041 at (2588, 953) to Babarunerk, 120 m | Q2284 | **798041 exists only 04:00–21:00** game time (`temporary_spawn`) |

Every target is level 16–18 against a level 20 Cleric: this leg is about mechanics, not danger.

### Quests

All are in Java; the C# templates and handlers are ports.

| Quest | Name | Handler | From → to | After | Objective | XP |
|---|---|---|---|---|---|---|
| 2242 | A Nice Gesture (held, Leg 5) | report_to | → **Gemyu** | | hand it in | 16,350 |
| 2244 | A Drinking Problem | monster_hunt | Gercus | | kill 5 green ribbits | 16,050 |
| 2245 | Ksellid Control | monster_hunt | Gemyu | | kill 5 star metal ksellids | 17,550 |
| 2248 | The Secret Letter | report_to | Gemyu → Neparinerk | 2245 | deliver | 13,095 |
| 2246 | The Gerger's Insignia | item_collecting | Germir | | the insignia box (700147) | 6,000 |
| 2247 | The Gerger's Disguise | custom | Germir → Gogaerunerk → Germir | 2246 | SETPRO1 gives the disguise (182203231); Germir takes it back | 16,050 |
| 2284 | Escaping Asmodae | custom (escort) | Germir; disguised Germir 798040 (SETPRO2), then 798041 (SETPRO3, the follow) → Babarunerk | 2247 | **escort**; a lost follower sets var 1 back, a logout too | 50,130 |
| 2249 | The Blue Crystals | item_collecting | Neparinerk | | 5 Angolem Crystals, 80% | 21,300 |
| 2251 | Securing the Trade Route | item_collecting | Babarunerk | | 5 Pecku Tail Feathers, 80% | 22,350 |
| 2252 | Chasing the Legend | custom (spawn) | Sinood | | the bones spawn Minushan's Spirit or Drakie; the kill picks the reward group | 24,900 / 20,355 |
| 24115 | A Shugo Apropos (D32) | monster_hunt | Neparinerk → **Banatisai** (Heart of Impetusium, Stop 12) | | kill 3 crimsontail amphas; **held** (AG-Q2) | 16,066 |

The ten hand-ins are worth 203,775 XP (33% of level 20; Q2252 at its Spirit reward). Kills of grey monsters add little.

**Not in this leg:**
- Q2900 "No Escaping Destiny" (Pandaemonium) opened at level 20. **The maintainer (2026-10-01): it is done at the
  very end of the Altgard area.**
- The missions Q24014–Q24016 (the Haramel and Bregirun instances) opened at level 20 too. **The maintainer
  (2026-10-01): they are done in Altgard when convenient, as one of its last legs, near the highest level the
  Cleric reaches here.** This refines AL-1, which had put them after the zone.
- Q2262 starts at Japayerk here but ends at Mabrunerk at the East Gate. Under AG-Q1 (a) it is taken and
  held for Leg 7, like Q2242 in Leg 5.

### What is new for the bot

1. **Chained flights.** The planner has only taken one flight so far. Basfelt to the Berth is two
   (`NaturalAirlineRoutes` gains a route search over the 140 routes).
2. **An escort whose follower keeps hours.** The AC-06 escort executor (Q2290), started from a
   `temporary_spawn` follower. It is present 04:00–21:00, so outside that window the bot works other
   quests first, as Q2292's carriers did (`NaturalCarrierPolicy`'s clock).
3. **A spawn from an item at an object** (Q2252): use the Bones of Minushan with Sinood's item,
   then kill what appears inside its 3 minutes. Sinood gives the item again on a miss (page 1693). This is
   the Q2223 Infernus executor with an object in place of an item use.
4. **A disguise** (Q2247) is a quest item handed over by a talk, then taken back. It is contract steps, not
   a transformation.
5. **Template quests at a new hub**, with Japayerk as the town merchant for the cube service.

### Leg 6 TODO list

The same loop protocol, with "AG" in place of "NA".

- [x] **AG-00 — Chained hub flights.** A route search over `natural-flight-routes.json` (flights plus the
  walks between landings and pads), with unit tests. A SIM probe flies Basfelt → fortress → Trader's Berth.
  - *Done 2026-10-01.* `NaturalAirlineRoutes.Journey` finds the quickest way on a map by walks and flight transporters.
    - It is a shortest-time search: straight-line walks at 6 m/s (a road is never shorter, so this favours walking),
      and a flight costs its route's time plus 10 s for the dialog and fare.
    - It returns no journey unless the flights save at least 20 s over walking all the way.
    - `Toward` is now the journey's first flight. The runner's `FlyTowardAsync` flies one leg at a time and plans
      again from each landing (at most four flights).
  - `BasfeltToTradersBerthIsTwoFlightsAndBackAgain`:
    - Nokir to the Berth obelisk is Hrold's flight (203683, location 19), then the fortress transporter's flight
      to "Urtumheim" (203561, location 20), 960 Kinah at base prices.
    - Back is 203678 (location 19), then 203561 (location 32). The fortress to Basfelt is one flight. Gerger, 345 m
      from the Berth, is a walk.
  - **SIM probe `BasfeltToTradersBerthTakesTwoHubFlights`** (account 68, a level 20 Cleric):
    - The journey picks each hop: `df1a_sub_basfelt` for 565 Kinah and `DF1A_SUB_Altgard` for 791 (`SM_PRICES`).
    - It lands 8.0 m from the Trader's Berth obelisk, where the search then says walk. The server agrees with each
      landing.
  - **`run-fast` found a talk defect, fixed here.** The new probe shifts the shared world's clock, and after it
    `AltgardScriptedQuestsPlayThroughTheContractSteps` timed out on Q2209's Tulberg, a walking NPC.
    - The dialog opened while he walked on; he stopped 6.6 m from the Cleric, outside talk range.
    - Java `NpcController.onDialogSelect` drops a choice made from out of range without a word, so the talk waited
      for page 1352 until it timed out.
    - `NaturalAltgardQuestSteps.TalkAsync`, which the runner shares, now waits up to 3 s for a moving NPC to stop.
      If it stopped beyond the step's talk range, it raises `NaturalDialogTooFarException`, and the callers already
      follow and talk again.
  - All 347 Natural tests pass, and so do `run-fast` (every test), the warning baseline and the logger, clock and
    fidelity checks.
- [x] **AG-01 — The Leg 6 contract and plans.** `natural-altgard-l6-contract.json` from the l5 endpoint:
  - bind 700821, the town and its merchant (Japayerk);
  - the escort (Q2284) with the follower's hours, the Q2252 spawn, and the Q2247 and Q2246 steps;
  - held quests (Q24115, and Q2262 under AG-Q1 (a)), the order and the endpoint.
  - *Done 2026-10-02.* `natural-altgard-l6-contract.json` (generated from the shipped data) starts from the l5 endpoint:
    - level 20, bound at Basfelt;
    - Q2242, Q24233, Q2900, Q24014 and Q24015 started, and Q24016 locked.
  - **The hub:** the bind is the Trader's Berth obelisk 700821 (2,035 Kinah, `bind_points.xml`). Japayerk (798030) is
    the town's merchant.
  - **Areas:** 15, from the Berth and Gerger to each ground. Three are for held hand-ins: the Heart of Impetusium, the
    East Gate and Suthran, still held since Leg 5.
  - **Quests:** twelve, in the order Q2242, Q2245, Q2244, Q2246, Q2247, Q2284, Q2248, Q2249, Q2251, Q24115, Q2252,
    Q2262.
    - The nine template quests have compiled plans in `natural-altgard-l6-plans/`.
    - The three scripted quests have contract steps read from their C# handlers (Java ports):
      - **Q2247:** Gogaerunerk's SETPRO1 gives the disguise 182203231; Germir sets REWARD at var 1 and takes it back.
      - **Q2284:** the first disguised Germir's SETPRO2 sets var 1; the second's SETPRO3 starts the follow.
      - **Q2252:** Sinood's QUEST_ACCEPT_1 gives the Bones of Minushan; the kill sets REWARD; Sinood takes it.
  - **The escort `q2284-germir`:**
    - The second disguised Germir (798041) follows to Babarunerk from var 1 (`startVar`, new); the follow sets var 2.
    - It succeeds within 20 m of Babarunerk's spawn (REWARD at var 2), and is lost beyond 50 m (var 1). Those are the
      Java values: `CoordinateDestinationChecker` and `FollowingNpcCheckTask`.
    - The follower exists only 04:00–21:00 (`followerSpawnHour`/`followerDespawnHour`, new).
  - **The spawn `q2252-minushan`:** the bones (700060) spawn Minushan's Spirit (210634) or Drakie (210635,
    `alternateNpcIds`, new) for 180 s.
    - It has no movie: `MovieId` is now optional. Sinood's page 1693 is the new item.
  - **Held:** Q24115 (Banatisai) and Q2262 (Mabrunerk), as AG-Q2 asks. The endpoint is the ten other quests completed,
    bound at the Berth.
  - **`NaturalAltgardLeg6ContractTests`** (5 tests) pins:
    - the start against Leg 5's endpoint;
    - quest data, the bind price, the merchant and the targets in their areas;
    - every scripted step against its handler (its NPC, dialog pages, SETPRO actions and items);
    - the escort and spawn against their handlers and `temporary_spawn`;
    - the two hub flights from the Basfelt bind.
  - All 352 Natural tests pass, and so do the warning baseline and the logger, clock and fidelity checks.
- [x] **AG-02 — Walk every Leg 6 route in SIM**, from the Berth obelisk to each ground and Gerger, and back.
  - *Done 2026-10-02.* `AltgardLeg6TravelWalksEveryGroundAndBack` (account 67, a level 30 probe; plans at level 20) walks
    16 legs in three round trips from the Trader's Berth obelisk. Every leg ends within 5 m of its ground. Notable:
    - **Gerger by road.** The Berth to Gemyu is 466 m by road (345 m straight), across the angolem ground (seven
      blackened angolems within 30 m).
      - Around Gerger the grounds are close together: the ribbits 120 m away, the insignia box 98 m, the first
        disguised Germir 134 m.
      - Gogaerunerk is 306 m from that Germir, among angolem fragments (280100, NOVICE L18), as are the ksellids
        beyond him: ten fragments within 30 m.
    - **The escort line.** The second disguised Germir is 165 m from the angolems, and the line on to Babarunerk is
      197 m, across the same angolem ground (four angolems and three shardlings within 30 m).
    - **The far grounds.** The peckus are 63 m from the obelisk and the crimsontails 95–146 m. The Bones of Minushan
      are 365 m beyond them, and the way back is 520 m through the pecku ground (21 peckus within 30 m).
  - `run-fast` passes (every test), and so do the warning baseline and the logger, clock and fidelity checks.
- [x] **AG-03 — Gerger's scripted quests in SIM:** Q2246's insignia box, Q2247's disguise, and Q2284's two
  Germirs and the follow to Babarunerk. Include a run started after 21:00, which must wait for 798041.
  - *Done 2026-10-02.* `GergerScriptedQuestsAndTheNightEscortPlayThroughTheContract` (account 66, a level 20 Cleric)
    plays all three on their contract steps:
    - **Q2246:** Germir, the insignia box (700147), then Germir's check. Java lets a quest use any object among its
      quest drops (`AbstractQuestHandler.loadActionItems`), so the box needs no script entry.
    - **Q2247:** Gogaerunerk's SETPRO1 gives the disguise (182203231). Germir takes it back and ends the quest.
    - **Q2284:** the first disguised Germir sets var 1.
      - The clock is run on to 21:00, and the second disguised Germir (798041) is gone. At 04:00 he is back.
      - The escort protocol restarts the follow at var 1 (SETPRO3). Germir reaches Babarunerk in 47 s, on the first
        attempt, never more than 11.2 m behind. That sets REWARD at var 2, and Babarunerk ends the quest.
  - **The escort policy, fixed here.** Q2284 follows at var 2 and succeeds at var 2 (`DefaultFollowEndEvent(env, 2, 2,
    true)`), so the policy called the escort done as soon as it started. An escort whose success var is its follow
    var is now done only at REWARD. `AnEscortWhoseSuccessKeepsTheFollowVarEndsOnlyOnReward` pins it.
  - **For AG-06 and AG-07: every Gerger spot has aggressive monsters.**
    - On the first try an attack cancelled the box's use bar.
    - The fights are AG-06's, so the probe now clears each spot first (within 40 m) and logs what it cleared:
      - at the insignia box: four angolem fragments (280100), two blackened angolems (210486, 210487), a shardling
        (210489) and two green ribbits (210573, 210574);
      - at Gogaerunerk: two fragments and two blackened angolems (210486);
      - on the escort line: five;
      - at Babarunerk: a blackened angolem (210487), one or two shardlings and two peckus (212345).
  - All 353 Natural tests pass, and so do `run-fast` (every test), the warning baseline and the logger, clock and
    fidelity checks.
- [x] **AG-04 — Q2252 in SIM:** Sinood, the bones, the spawn, the kill and the reward group, and the new
  item after a miss.
  - *Done 2026-10-02.* `ChasingTheLegendMissesOnceRefillsAndPaysTheKillsRewardGroup` (account 65, a level 20 Cleric):
    - **The start.** Sinood's QUEST_ACCEPT_1 starts Q2252 and gives the Bones of Minushan (182203235).
    - **The first use and a miss.** The bones (700060) take the item and raise Minushan's Spirit (210634) on their own
      spot, with the shout 1100630.
      - Left alone, the Spirit is gone after its 180 s (185 s seen), and Q2252 stays at var 0.
      - The bones object stays for the next try.
    - **New bones.** Sinood's QUEST_SELECT at var 0 gives new bones (page 1693). Java's `giveQuestItem` returns true
      even when the bones are already in the bag (with STR_CAN_NOT_GET_LORE_ITEM), so page 2034 never shows.
    - **The kill and the reward group.** A second Spirit, set to 1 HP on the server (the fight is AG-06's), is shot
      down from 18 m: REWARD at var 1. Sinood pays reward group 0: three Drake's Memory (164000041) and 24,900 XP.
      - The Drakie (210635, 5%) did not come up in this run. When it does, the probe checks that branch the same way:
        var 2, reward group 1 and its 1,900 Kinah (the XP is only logged).
    - Cleared first (within 40 m): one or two peckus (212345) by Sinood, and six at the bones.
  - **For AG-07: a ghost Spirit.** Java sends the client no SM_DELETE for what it saw while it teleports
    (`PlayerController.notSee`).
    - The probe met it on its first run. Its setup teleport kept the first Spirit in the client's view, and
      `WaitForNpcAsync` took that long-gone Spirit for the second. The probe now drops the client's view before each
      setup teleport, as the client does.
    - The runner drops the view for a Return and for a bind on another map. It does not for a bind revive on the same
      map, though Java's `moveToBindLocation` is the same teleport. After a miss and a death, the Berth revive would leave
      the first Spirit in view at the bones. So AG-07's spawn kill must take the monster whose SM_NPC_INFO follows the use.
  - All 353 Natural tests pass, and so do `run-fast` (every test), the warning baseline and the logger, clock and
    fidelity checks.
- [x] **AG-05 — The template quests in SIM:** Q2244, Q2245, Q2249 (with the shardlings), Q2251, Q24115
  held, and Q2242's hand-in at Gemyu.
  - *Done 2026-10-02.* `GergerAndBerthTemplateQuestsPlayThroughTheirPlans` (account 64, a level 20 Cleric) plays every
    template quest of the leg from its plan. Each target is set to 1 HP and shot from a stand-off (the fights are AG-06's).
    - **Gerger.** Q2242 is handed in at Gemyu (the probe takes it at Vovetirn first, as Leg 5 left it).
      - Q2245 (star metal ksellids) and Q2244 (green ribbits) take five kills each.
      - Then Gemyu gives Q2248's letter (it needs Q2245), and Neparinerk takes it.
    - **Trader's Berth.**
      - Q2249: five Angolem Crystals in six kills, four from blackened angolems (210487) and one from a shardling
        (210489); the other shardling dropped none.
      - Q2251: five Pecku Tail Feathers in five or six kills.
    - **Held (AG-Q2 (a)).**
      - Q24115: three crimsontail amphas leave it at START with the counter full (3 of 3). It is held for Banatisai
        with its work item (182215477).
      - Its two start conditions both hold at the Leg 6 start: Q2200 and Q2019 are neither started nor finished.
        Java needs both, since neither condition names a finished quest (`QuestTemplate.getRequiredConditionCount`).
      - Q2262 is taken from Japayerk and held for Mabrunerk with its delivery (182203241).
    - Q2248 and Q2262 are not on the item's list. They are the leg's other two template quests, so the probe plays them
      too.
  - The probe drops the client's view before each setup teleport, as AG-04 found it must.
  - All 353 Natural tests pass, and so do `run-fast` (every test), the warning baseline and the logger, clock and
    fidelity checks.
- [x] **AG-06 — Combat at level 20.** The encounter harness against Minushan's Spirit (SEASONED L18), a
  pecku pack and an angolem with its shardlings. Record deaths and retreats.
  - *Done 2026-10-02.* The NA-23/AC-00 encounter takes `AG06_STAGES=1` (level 20, or `AC00_CLERIC_LEVEL`). Its four Leg 6
    stages are GM-spawned on the AB-07 ground, placed as the spawn file has them. The Cleric fights with the journey's
    own combat, pull planner, rests and help items, with the bridge's Karmic Staff as in AK-07. Run
    `run/ag06/ag06-cleric-encounter-l20-s1`:

    | Stage | Level 20 |
    |---|---|
    | Minushan's Spirit (210634, SEASONED L18) alone | 1 kill, lowest HP 79%: Awakening and a DP jelly before the first pull, 1 potion |
    | Three peckus (L18) placed as the three around the bones, walking (random_walk 8) | 3 kills, one at a time, 82% |
    | A blackened angolem (L17) with the shardling 11.6 m from it | 2 kills, 87%, 1 potion |
    | The Spirit at the bones with those three peckus around it, as Q2252 brings it | the Spirit and 2 peckus (the third never joined), **48%, one retreat**: Salvation, Root, Awakening, 2 potions |

  - **Findings:**
    - No deaths at level 20.
    - **The bones are the dangerous fight.**
      - The Spirit (aggro 16 m) and two of the peckus that walk around the bones (8 m) came at once. The Cleric
        retreated 150 m, killed a pecku that followed, then the Spirit and the other pecku, at 48% at worst.
      - AG-07 should clear the peckus around the bones before the use, and use them on full HP and MP. The peckus
        respawn 295 s after a kill, and the Spirit stays 180 s, so the window is tight.
    - **The pecku pack does not form on its own.** Peckus stand at least 17.6 m apart. Below 35% a pecku casts 16546
      and sends message 1001 to the npcs within 15 m of its target (`battle_cycles.xml` variant 99). No pecku got
      there in either stage: the Cleric's burst killed each first.
    - **The shardling is only a neighbour.** Its `angolem_fragment` AI answers a stoneskin stoffu's call, and the
      spawn file gives each blackened angolem at most one shardling, 11.6–19.4 m away.
  - **How to read the trace.** `combat-encounter-end`'s `retreats` is the run's running total, not the encounter's:
    this stage's nine encounters each say 1. Count kills by target, as AK-07 notes.
  - The warning baseline and the logger, clock and fidelity checks pass.
- [x] **AG-07 — The Leg 6 runner.** The engine and runner for the chained flights, the escort's hours, the
  spawn and the held quests. A smoke run from `altgard-l5` with real combat, recording every fight and death.
  - *Done 2026-10-02.* **Smoke run 5** (`run/af-l1/ag07-l6-smoke5`, seed 1, from `altgard-l5`) plays the whole leg in 33
    decisions and 1 h 41 min of simulated time. It ends at Trader's Berth at level 21 (from 20), with 1 death, and the
    relog check passes.
    - **The hub flights (AG-00):** Hrold's flight from Basfelt to the fortress (203683), then 203561's to the Berth. The
      Cleric binds at the Berth obelisk for 2,035 Kinah.
    - **The templates, hub-style:** seven accepted, six worked, then seven claimed. Q2242 is handed in at Gemyu, and
      Q2248 is taken once Q2245 is claimed.
    - **Q2247 and Q2284 by their talk steps.** The escort clears 17 monsters off the line, then 2 more, and brings
      Germir to Babarunerk on the first attempt: 46 s, 19 hops, never more than 11.2 m behind.
    - **Q2252:** the bones raise the Spirit, killed on the first try; Sinood pays reward group 0.
    - **Held:** Q24115 at 3 of 3 for Banatisai, Q2262 for Mabrunerk, and Q24233 for Suthran (from Leg 5).
    - **Fights:** 82 kills, 16 retreats and 1 death, with 39 potions, 11 Awakenings and a DP jelly.
      - The kills: 22 shardlings and 20 blackened angolems, 18 peckus, 9 ribbits, 6 crimsontails, 5 ksellids, an
        angolem fragment and the Spirit.
      - Six retreats on the road from the Berth to Gemyu for Q2242's hand-in (angolems, shardlings, peckus and
        crimsontails).
      - Six around Gogaerunerk (Q2247), five of them from angolem fragments four or five at a time, and the death there.
      - One while clearing the escort line, two at the bones, and one on the way back to Sinood (three peckus).
    - **Germir's hours (AG-Q3 (a)), from smoke run 3.** At decision 28 the escort could not end inside his window, so
      Q2252 was taken, worked and claimed first (`escort-hours:wait`). The escort came at decision 31, inside the
      window. Smoke run 5 reached the escort inside it, so `wait-for-escort` runs only in the engine test.
  - **What changed:**
    - **The engine.** An escort with a start var (Q2284 at var 1) takes its talk steps until then.
      - A follower that keeps hours is fetched only when the escort can end inside them. The estimate is the walk
        there plus the escort line at half speed (`EscortGameMinutes`).
      - Otherwise the other quests come first, then `wait-for-escort` at the hub.
      - The protocol ends with `off-hours` if the window closes while nobody follows.
    - **The bind** flies to a hub the Cleric is not at (`FlyTowardAsync`).
    - **Far approaches** (over 100 m) walk the leg's road first, to 40 m short, then the navigator takes over.
      - Smoke run 1: the navigator's hazard replanning circled for its 1,000 segments on the angolem ground between
        the Berth and Gerger.
      - Smoke run 4: the same after a retreat cut a road walk short. A walk cut short by a fight is now walked again,
        three walks at most.
    - **The spawn kill** clears around the trigger first (AG-06), rests, and fights the monster whose SM_NPC_INFO
      follows the use: the Spirit or the Drakie, not AG-04's ghost.
    - **The escort policy** walks to where an unseen follower starts before waiting for him. In smoke run 3, Germir was
      out of view 150 m from the Berth, and the bot waited an hour for a respawn.
    - **Combat:** a Smite cut short leaves no chain. Java's `ChainCondition.shouldReset` clears the chain when an
      opener starts, and only a completed cast opens it again. In smoke run 2 the server refused the Flashbolt after a
      cancelled Smite without a word, and the cast timed out.
  - New tests: `Leg6BindsAtTheBerthAndEscortsGermirOnlyInsideHisHours`, and the escort policy's far-follower case.
  - All 354 Natural tests pass, and so do `run-fast` (every test), the warning baseline and the logger, clock and
    fidelity checks.
- [x] **AG-08 — One SIM run of Leg 6 and the snapshot.** `sim-snapshot.ps1 -Leg l6 -From altgard-l5`
  captures `altgard-l6` after a clean run, with a restore check.
  - *Done 2026-10-02.* `sim-snapshot.ps1` accepts `-Leg l6` (`7d10ef4c1`).
    `-Action Capture -Name altgard-l6 -AltgardLeg1 -Leg l6 -From altgard-l5` restored `altgard-l5`, resumed
    character 133297 with `AF_ALTGARD=l6`, and dumped only a verified `altgard-l6-completion.json`.
  - Run `snapshot-altgard-l6-s1` (seed 1) repeats AG-07's smoke run 5 to the millisecond: 6,042,068 ms, level 21, one
    death (at Gogaerunerk) and 16 retreats.
  - Snapshot `altgard-l6`: `run/snapshots/altgard-l6` (git-ignored), character 133297, elapsed 46,436,377 ms,
    dump SHA-256 `79a2667e…b61b82`, from commit `7d10ef4c1`.
    - Level 21 at 143,590 of 827,364 XP, bound at the Trader's Berth obelisk, with 228,032 Kinah.
    - Held: Q24115 (START, 3) for Banatisai, Q2262 (START, 0) for Mabrunerk, and Q24233 (START, 1) for Suthran.
      Q2900, Q24014 and Q24015 are started, and Q24016 is locked.
    - A test restore into an owned schema succeeded and was dropped; no owned schema is left.
- [x] **AG-09 — The full `CLAUDE.md` checklist and a checkpoint.**
  - *Done 2026-10-02.* Every command in the `CLAUDE.md` checklist was run in order, on `f0f364b60`:
    - `dotnet build` passed, and so did `dotnet test`, with no failures:
      - GameServer: 4,435 passed, 16 skipped.
      - Simulation: 145 passed, 64 skipped (they need their switches).
      - LoginServer: 135 passed. Commons: 303 passed. ChatServer: 41 passed.
    - The warning baseline passed, and so did the logger, clock-read, custom-quest-draft and fidelity checks.
    - All twelve Python e2e contract tests passed.
      - The quest plan compiler recompiles the Leg 6 plans identically.
      - The D32 inventory's account check passes, now that the flee probe uses account 69.
    - All twelve PowerShell contract tests passed, and so did the NavBake `check --maps baked`.
    - `run-fast` passed: 60 tests (3 skipped by their switches), all 11 scenarios.
  - **Checkpoint:**
    - Leg 6 plays end to end in SIM from `altgard-l5`.
    - Snapshot `altgard-l6` (AG-08) is the start of the next leg. The Cleric is level 21, bound at the Trader's Berth
      obelisk (700821), with a 63-slot cube and 228,032 Kinah.
    - Three hand-ins are held for their hubs: Q24233 (Suthran, the fortress), Q2262 (Mabrunerk, the East Gate) and
      Q24115 (Banatisai, the Heart of Impetusium).
    - Q2900, Q24014 and Q24015 (the campaign) are started.
      - Q2900 comes at the very end of the Altgard area.
      - Q24014–Q24016 come as one of its last legs.
    - Next is Leg 7, still to be planned: the East Gate (Stop 9) with the fortress-side Stops 10 and 11 (AG-Q1 (a)).
      The Berth's transporter (203678) flies to the fortress.
    - LIVE runs once, at the end of the whole Altgard area.
    - **Carried forward:** the runner does not drop its view on a bind revive on the same map (AG-04). The spawn
      kill no longer depends on that, but another target could still meet a ghost of an object gone while away.

**Endpoint (if AG-Q1 (a), AG-Q2 (a)):**
- Q2242, Q2244–Q2249, Q2251, Q2252 and Q2284 completed;
- Q24115 (Banatisai) and Q2262 (Mabrunerk) taken and held, Q24233 still held;
- bound at the Trader's Berth obelisk (700821);
- alive, within 60 m of it;
- the endpoint verified across a relog.

## Leg 7: Altgard Fortress and the East Gate (level 21) — proposal

**PC-08 scheduled capital additions:** batch Q2919's Neusa/book/Cavalorn finish with
Q2278's Cavalorn/Balder visit once all three item types are owned. At >=20, do Q2959
Moreinen 204211 -> Eramir 204164 and Q2984 Lusena 204138 -> Kinot 204121; keep the dyes
unapplied. Accept Q2938 at Oubliette and carry START/0 until Q24016 is complete in
Leg 10. Do Q2954's single completion here if it was below its gate in Leg 5.
Q2920 waits: its two prerequisites belong to Leg 8. These additions are scheduled,
not completions in the historical Leg 7 contract/snapshot.

**Status (2026-10-02): approved** (AE-Q1..AE-Q3 as recommended; see "Blocked / questions").
- Leg 7 covers Stop 9 and Stop 11's fortress quests, from the fortress bind (AE-Q1 (a)).
- Q2146 is held for Lateni (AE-Q2 (a)).
- Q2273, the [Group] quests and the missions wait for the Black Claw leg, Leg 10 (AE-Q3 (a)).

**Where it starts.** From the `altgard-l6` snapshot (AG-08):
- character 133297, a **level 21** Cleric at 143,590 of 827,364 XP, bound at the Trader's Berth;
- 63 cube slots and 228,032 Kinah;
- three hand-ins held: Q24233 for Suthran in the fortress (since Leg 5), Q2262 for Mabrunerk at the East Gate (since
  Leg 6) and Q24115 for Banatisai in the Heart of Impetusium;
- the campaign's Q2900, Q24014 and Q24015 started.

**What it covers (AE-Q1 (a)).** Stop 9, the East Gate, and Stop 11's fortress quests, worked from the fortress bind.
- **The East Gate is not a hub of its own.** Mabrunerk, Eggther and Dellalont stand at the fortress's south gate, 200 m
  from the flight pad.
- Their quests are worked in the swamp beyond the gate, where Taora (Stop 10's Q2272) stands, 160 m further south.
- **Two held hand-ins come home:** Q24233 at Suthran and Q2262 at Mabrunerk.

**The bind.** The fortress obelisk 700065 at (1658, 1815), 94 m from the flight pad, for 451 Kinah. Every ground of
the leg lies within 450 m of it.

**Getting there.** The Berth's transporter (203678, "DF1A_SUB_Urtumheim", 43.5 s, 560 Kinah base) lands on the
fortress pad.

### The hubs and their grounds

| Ground | Where | Quests | What lives there |
|---|---|---|---|
| **Altgard Fortress** | the obelisk (1658, 1815); Suthran, Meiyer, Morn, Nemnef, Valurion and Emgata 90–120 m from the pad; Chagarinerk 200 m | Suthran: Q24233's hand-in, Q2279, Q2286, Q2287, and Q2278's turns; Emgata: Q2278; Meiyer: Q2261; Chagarinerk: Q2243, and Q2253's end | the teleporter to Pandaemonium (203581) and the flight transporter (203561) |
| **The East Gate** | (1744–1755, 2007–2010), 200 m south of the pad | Mabrunerk: Q2262's hand-in, Q2263; Eggther: Q2264, Q2146; Dellalont: Q2265, Q2261's end | one grove malodor within 40 m |
| **The swamp south of the gate** | around Taora at (1720, 2167) | Taora: Q2272. Swamp malodors (Q2263, 6 spots, 230 m from Mabrunerk), poison arachnas (Q2264, 23), feral black claw warriors (Q2265, 14, SEASONED L16–17), black claw sleekpaws (Q2272, 5, SEASONED L16), Zemurru's Spirit (Q2279, at one of two spots) | all mixed within 35 m of each other, with MuMu lookouts and looklooks, a black claw secret patrol (SEASONED), and **Shaman Gabacha (EXPERT L19)**, Q24014's target |
| **Pandaemonium** | the fortress teleporter (location 7, 500 Kinah base), as the cube errand (AK-08) | Q2278: Cavalorn (1369, 1051) and Balder (1469, 1466) | back by Return to the fortress bind |
| **Trader's Berth** | the flight, about 45 s each way | Kagorinerk (2702, 1022): Q2243's end, Q2253's start | |

Every target is level 16–17, some of them SEASONED, against a level 21 Cleric. The swamp is crowded, though, and
Gabacha is EXPERT.

### Quests

All are in Java; the C# templates and handlers are ports.

| Quest | Name | Handler | From → to | After | Objective | XP |
|---|---|---|---|---|---|---|
| 24233 | Adieu to You, Manumumu (held, Leg 5) | monster_hunt | → **Suthran** | | hand it in (Manumumu was killed) | 16,495 |
| 2262 | A Sneaky Delivery (held, Leg 6) | report_to | → **Mabrunerk** | | hand it in | 45,750 |
| 2261 | Failure to Report | report_to | Meiyer → Dellalont | | deliver | 5,070 |
| 2263 | Shugo Potion | custom | Mabrunerk | | **timed, 300 s** from the offer: 3 Malodor Pollen (80%) from swamp malodors (L17); a logout or the timer abandons it | 27,750 |
| 2264 | The Sting of Poison | monster_hunt | Eggther | | kill 3 poison arachnas (L16–17) | 15,075 |
| 2265 | A Lost Sword | item_collecting | Dellalont | | the Archon Sword (85%) from feral black claw warriors (SEASONED L16–17) | 31,050 |
| 2146 | Pass the Message | report_to | Eggther → Lateni (Heart of Impetusium) | | **held** for Stop 12 (AE-Q2) | (7,500) |
| 2272 | The Lost Cube | item_collecting | Taora | | Taora's Cube from black claw sleekpaws (SEASONED L16) | 14,625 |
| 2243 | A Crystal Hand Mirror | report_to | Chagarinerk → Kagorinerk (Trader's Berth) | | deliver, by flight | 4,305 |
| 2253 | Kagorinerk's Gift | report_to | Kagorinerk → Chagarinerk | 2243 | deliver back, by flight | 3,810 |
| 2278 | A Secret Proposal | custom | Emgata; Suthran (SETPRO1); Cavalorn (SETPRO2) and Balder (SETPRO3) in **Pandaemonium**; Suthran | 2208 | talks | 48,600 |
| 2279 | Solid Proof | custom | Suthran; Emgata (SETPRO1); Zemurru's Spirit (SETPRO2); Suthran | 2278 | talks | 38,100 |
| 2286 | The Brigade General's Order | report_to | Suthran → Morn | 2279 | deliver | 4,785 |
| 2287 | Order to Valurion | report_to | Suthran → Valurion | 2279 | deliver | 6,405 |

The thirteen hand-ins are worth 261,820 XP, 32% of level 21.

**Not in this leg:**
- **Q2273** "Emergency Rations" (Taora, after Q2272). Its veteran MuMus (SEASONED L17) stand inside the Black Claw
  ground, 530 m east of Taora: twelve brutal black claw bodyguards and 35 other EXPERT spawns (L17–19) stand within
  35 m of its spots. It goes with the [Group] quests (AE-Q3).
- **The [Group] quests:** Q2277 (Nemnef, nothing first), Q2280 (Morn, after Q2286), Q2281 (Valurion, after Q2287),
  Q2282 (Suthran, after Q2279) and Q2283 (to Vidar in Pandaemonium, after Q2282). Their EXPERT targets (L16–20) stand
  550–1,100 m southeast of the pad (AE-Q3). Leg 7 opens Q2280–Q2282 without taking them.
- **Q2266** (Valurion, then Neifenmer and Aurtri at the Observatory) belongs with Stop 10. That leg starts at the
  fortress and takes it there.
- **The missions Q24014–Q24016** (Q24014 and Q24015 started): one of the last Altgard legs, the maintainer's
  placement. Q24014's Shaman Gabacha stands in this leg's swamp.
- **Q2900**, at the very end of the Altgard area. **Q24114** (Neparinerk) is intentionally excluded
  by the maintainer's 2026-10-03 decision not to implement it.

### The rest of Altgard

After Leg 7, four groups of work are left. The fortress pad flies to each hub (`natural-flight-routes.json`), and
each has an obelisk:

| Leg (AE-Q3 (a)) | Where | Bind | From the fortress pad | Quests | XP |
|---|---|---|---|---|---|
| 8 | the Altgard Observatory and Mahindel Swamp (Stop 10) | 700822 (2,035 Kinah) | "df1a_altgardtosanctuary", 34 s, 250 Kinah | Q2266 (from Valurion), Q2267–Q2270, Q2271 (to Suthran), and Q24113 (**D32**, Commander Gattban, EXPERT L18) | 155,566 |
| 9 | the Heart of Impetusium (Stop 12) | 700067 (813) | "df1a_altgardtoimpetosium", 52.8 s, 400 | Q2254–Q2257, Q2259, Q2260 (to Suthran) and Q2258 (to Pandaemonium); the hand-ins of Q24115 and Q2146 | 254,866 |
| 10 | the Black Claw ground, southeast of the gate | the fortress | | the [Group] quests Q2277 and Q2280–Q2283, Q2273, and the missions Q24014–Q24016 | about 960,000 |
| — | Pandaemonium | | | Q2900, the very end | 228,880 |

On quest XP alone, the Cleric ends Leg 9 about 11,500 XP short of level 22, and Leg 10 about 90% of the way
through level 22. Kills make up the rest, and Q2900 passes level 23. That puts the missions near the highest level
of the area, as the maintainer placed them.

### What is new for the bot

1. **A quest step on another map.** Q2278's Cavalorn and Balder stand in Pandaemonium.
   - The cube errand (AK-08) already goes there and back, by the fortress teleporter and Return.
   - Now a contract step names its map, and the runner makes the trip for it.
2. **A timed collection in a crowded swamp.** Q2263 gives 300 s from the offer, and its malodors stand among SEASONED
   lycans and near Gabacha.
   - The bot clears around them before it takes the quest, then rests (AB-08's timed policy: rest and mana first).
   - A timer that runs out is recorded and retried, not failed (AB-Q2's three tries).
3. **A delivery round trip by flight.** Q2243 and Q2253 go to the Trader's Berth and back on the fortress's
   transporter (AG-00's chained flights).
4. **An NPC in a pool.** Zemurru's Spirit stands at one of two spots (`pool="1"`). The bot looks at both.

### Leg 7 TODO list

The same loop protocol, with "AE" in place of "NA".

- [x] **AE-00 — Quest steps on another map.** The contract carries a step's map, and the engine plans it. The runner
  travels to Pandaemonium by the fortress teleporter, as the cube errand does, talks there, and comes back by Return.
  Unit tests, and a SIM probe of Q2278 end to end.
  - *Done 2026-10-02.* Steps have an optional `mapId` (the hub map by default), and the contract names its teleporter
    trips. The engine plans `travel-to-map`, keeps a started quest's city talks together, and returns for the hub talk.
    The runner uses the existing teleporter, per-map navigator and learned Return.
  - `SecretProposalTravelsToPandaemoniumAndReturnsToSuthran` (`ae00-proposal200`, account 200) completes Q2278:
    Emgata, Suthran, Cavalorn and Balder in Pandaemonium, Return to the fortress bind, and Suthran's reward.
    The fare is 706 Kinah after `SM_PRICES`. Local approaches are setup teleports on the probe only, with hostile
    neighbours cleared and `BeginWorldReload` before each; the map trip and Return are ordinary client actions.
  - Six map-step unit cases pass; all 41 NaturalAltgard tests pass. `ae00-fast` passes all 11 scenarios (61 tests,
    three switch-dependent skips). Warning baseline, logger, clock-read and fidelity checks pass.
  - Initial probe failures are retained in `run/ae00-probe*.log`: missing client prerequisite evidence (including
    Q2208 in the probe fixture), then a missing reward confirmation. Both were probe setup/contract defects, fixed
    before the passing run. The initial account 63 overlapped Full's alliance scenario; the passing probe used 200.
    AE-08 found that 195–200 were also reserved for D32 and moved all Leg 7 probes to unused accounts 201–206.
- [x] **AE-01 — The Leg 7 contract and plans.** `natural-altgard-l7-contract.json` from the l6 endpoint:
  - the fortress bind (700065) and a fortress merchant for the town service;
  - the held hand-ins Q24233 and Q2262, and Q2146 held for Lateni;
  - the timed Q2263, and the Pandaemonium and Zemurru's Spirit steps;
  - the order and the endpoint.
  - *Done 2026-10-02.* The contract starts at level 21, bound at Trader's Berth, with Leg 6's 64 completed quests;
    Q24233, Q2262 and Q24115 are held, the three campaign quests are started, and Q24016 is locked.
  - Fourteen quests: thirteen hand-ins and Q2146 held for Lateni. Eleven compiled template plans, fourteen scripted
    dialog steps, and eleven areas cover the fortress, East Gate, swamp grounds, both Spirit spots, Berth and city.
    The bind is 700065 (451 Kinah); Donabe (203579) is the town merchant. The endpoint is alive within 60 m of the
    fortress bind, with Q2146 and Q24115 still held.
  - Q2263 has its Java 300-second timer from QUEST_ACCEPT_1, three Malodor Pollen (182203242), abandonment on expiry
    or logout, and its check/reward dialogs. Q2278 uses AE-00's map steps; Q2279 searches the two-spot Spirit pool.
    Q24233's existing D32 template is retained (4.8 evidence in the D32 register, deviation 151).
  - `NaturalAltgardLeg7ContractTests` passes all three checks against shipped quest, NPC, bind, dialog, pool and
    transport data. `test-quest-plan-compiler.py` passes all eleven tests, including identical recompilation of the
    new plans. `ae01-fast` passes 61 tests (three switch skips), all eleven scenarios. The warning, logger, clock
    and fidelity checks pass. The code base for this item is AE-00's `62863fcca`.
- [x] **AE-02 — Walk every Leg 7 route in SIM:** from the fortress obelisk to the East Gate, each swamp ground, Taora
  and Chagarinerk, and the Berth round trip by flight.
  - *Done 2026-10-02.* `AltgardLeg7TravelWalksTheGateSwampAndBothSpiritSpotsAndFliesToTheBerth` (`ae02-travel`,
    account 199) walks 27 ground legs from AE-01's `be166588c` contract. The probe is level 30; plans are level 21.
    Each server endpoint is within 5 m of its goal (0.0 m reported in this run).
    - The fortress circuit visits Suthran, Meiyer, Emgata, Morn, Valurion, Donabe and Chagarinerk.
    - The gate/swamp circuit visits Mabrunerk, Eggther and Dellalont, both arachna kinds, both malodor kinds, both
      feral warrior kinds, sleekpaws and Taora, then both Zemurru's Spirit pool spots and the fortress.
    - The flight round trip uses 203561 to Trader's Berth, walks to Kagorinerk and the pad, and uses 203678 back.
      Each fare is 791 Kinah after `SM_PRICES`. The last walk ends at (1661.44, 1815.30, 253.72), by the obelisk.
    - Aggressive monsters within 30 m of each walk are retained in the route log; these are AE-05/AE-06's fights.
  - Warning baseline, logger, clock-read and fidelity checks pass. `ae02-fast` passes all eleven scenarios and
    62 tests (three switch-dependent skips).
- [x] **AE-03 — The scripted quests in SIM:** Q2278 with the Pandaemonium trip, Q2279 with Zemurru's Spirit at either
  spot, and Q2263 twice: once in time, once with the timer running out.
  - *Done 2026-10-02.* `ae03-scripts` passes both Spirit pool spots through offer, Emgata, Spirit and Suthran's reward
    on accounts 197/198. Q2263 expires after 305 game seconds with one pollen: abandonment and item removal verified;
    the retake collects three pollen in four kills and completes in 18 game seconds. Expiry is a recorded outcome.
  - Q2278's end-to-end city trip remains covered by AE-00's probe, also passing in `ae03-fast`. The pollen area now
    covers the local grove/swamp spawns, excluding distant grove grounds. No server behavior changed; Java Q2263/Q2279
    and their shipped spawn/drop data were read first. Probe setup clears hostile neighbours and uses `BeginWorldReload`.
  - Three contract tests and warning, logger, clock-read and fidelity checks pass. `ae03-fast` passes all eleven
    scenarios, 64 tests with three switch-dependent skips. Base commit: AE-02's `b119b5eb6`.
- [x] **AE-04 — The template quests in SIM:**
  - Q2264, Q2265, Q2272, Q2261, Q2286 and Q2287;
  - Q2243, then Q2253, by flight;
  - the hand-ins of Q24233 and Q2262, and Q2146 taken and held.
  - *Done 2026-10-02.* `FortressAndEastGateTemplateQuestsPlayThroughTheirPlans` (`ae04-templates`, account 196)
    passes all eleven plans: ten completions and Q2146 START/0 held for Lateni (203659). Incoming Q24233 START/1 and
    Q2262 START/0 are set up as Leg 6 left them, with Q2262's work item; both hand-ins pass and consume their work items.
  - Q2264 counts three arachnas; Q2265's sword and Q2272's cube each drop on the first controlled kill. Q2261, Q2286
    and Q2287 report correctly. Q2243 finishes at Kagorinerk, then Q2253 returns to Chagarinerk, using 203561/203678
    hub flights (791 Kinah each). All collections are consumed on completion; Q2146 is not handed in.
  - Java MonsterHunt, ItemCollecting, ReportTo and Altgard template registrations were read first; Q24233 retains D32.
    Probe-only setup clears hostile neighbours and drops the client view before teleports. Warning, logger, clock-read
    and fidelity checks pass. This item changes only the probe; base commit `f41204c9d`.
- [x] **AE-05 — Combat at level 21.** The encounter harness against the swamp's mixes: feral black claw warriors
  (SEASONED) with sleekpaws and arachnas, and Shaman Gabacha (EXPERT L19). Record deaths and retreats.
  - *Done 2026-10-02.* `AE05_STAGES=1` used account 195, a level-21 Cleric and the journey's existing combat code,
    with shipped relative spawn spacing. `ae05-encounter-distinct` records warrior + two arachnas (3 kills, 186 game s),
    warrior + sleekpaw (2, 98 s), Gabacha alone (1, 32 s), and Gabacha with his nearby camp (3, 65 s). Nine distinct
    client-confirmed kills, zero deaths and zero retreats. The camp's L16 warrior was not selected; that kind was fought
    in the first stage. Java NPC stats/AI and the retained retail AI exception were read first; no server data changed.
  - The harness now excludes already defeated targets and records retreats. The initial `ae05-encounter` is retained:
    its older summary counted repeated corpse selections, so its kill count is not evidence of distinct kills.
    Setup teleports drop the client view; the read-only dashboard runs while AE-05 is active.
  - `ae05-fast` found an older Basfelt probe assuming a surviving outlaw in the shared world. Its controlled kill helper
    now replenishes a shipped kind at a shipped spot, as Leg 6's does. `ae05-fast-fixed` passes 65 tests (three switch
    skips) and all eleven scenarios. Warning, logger, clock-read and fidelity checks pass. Base commit `95c9bf58d`.
- [x] **AE-06 — The Leg 7 runner.** The engine and runner for:
  - the steps on another map;
  - the timed quest in the swamp;
  - the delivery flights.

  Then a smoke run from `altgard-l6` with real combat, recording every fight and death.
  - *Done 2026-10-02.* `ae06-smoke-final` restores `altgard-l6`, resumes 133297 through the real journey with
    `AF_ALTGARD=l7`, and verifies all thirteen completions plus Q2146/Q24115 held across relog. Alive at the fortress
    bind, level 21; 52 min 17 game seconds, 35 distinct confirmed kills, zero deaths and zero retreats.
  - The route audit confirms three hub flights (791 Kinah each): Berth to fortress, fortress to Berth for Q2243,
    Berth to fortress after taking Q2253. Q2278 takes the fortress teleporter to Pandaemonium, talks to both NPCs,
    and Returns to the fortress bind. Q2279 searches the shipped Spirit pool hints.
  - Q2263 clears companions around three local pollen spots with ordinary combat, preserves the malodors, and rests
    before acceptance. Its offer key now matches the timer policy's accept convention (contract check added). The first
    timer completes in about 59 game seconds. Java Q2263/Q2278/Q2279 and TeleportService were read first.
  - Java DropService rejects a second limit-one item. The smoke exposed the bot waiting for an inventory update after
    attempting a second Archon Sword; its general quest sweep now skips owned limit-one items, and the template loop
    rechecks collection after defensive combat. No server behavior or content changed.
  - Earlier evidence is retained: `ae06-smoke` was stopped to correct the leg identifier in the flight guard;
    `ae06-smoke-flight` was stopped at the duplicate-sword loot stall. `ae06-smoke-loot` completed the quests with one
    death and two retreats, but walked from the Berth during pollen preparation, so it is not checkpoint evidence.
    The final run corrects that approach with the return flight. Each restore's owned schema was dropped.
  - Three contract checks, warning/logger/clock-read/fidelity checks, and `ae06-fast-final` pass (65 tests, three
    switch skips, all eleven scenarios). Base commit `0ae28ce36`; no snapshot was captured from this uncommitted work.
- [x] **AE-07 — One SIM run of Leg 7 and the snapshot.** `sim-snapshot.ps1 -Leg l7 -From altgard-l6` captures
  `altgard-l7` after a clean run, with a restore check.
  - *Done 2026-10-02.* The script accepts `l7`. Its implementation was committed as `2df0d2cad` before capture;
    `-Action Capture -Name altgard-l7 -AltgardLeg1 -Leg l7 -From altgard-l6 -Run ae07-capture` plays from the committed
    AE-06 runner and dumps only the relog-verified endpoint. This evidence is added to the same checkpoint commit.
  - `ae07-capture` (seed 1) repeats the final smoke to the millisecond: 3,137,019 ms, 35 distinct confirmed kills,
    zero deaths/retreats, all three hub flights, first pollen timer, thirteen completions and both held quests.
  - Snapshot `run/snapshots/altgard-l7` (git-ignored): retained character 133297, elapsed 49,593,396 ms,
    dump SHA-256 `f0f91c60…15f6d12`, captured from committed code `2df0d2cad`.
    - Level 21, alive at (1662.09, 1811.69, 253.84), bound at the fortress, 264,842 Kinah, 63-slot cube.
    - Q2146 START/0 for Lateni and Q24115 START/3 for Banatisai; the thirteen endpoint quests COMPLETE.
      Q2900/Q24014/Q24015 remain START/0, Q24016 LOCKED/0.
    - A hash-checked restore into a fresh owned schema verifies the retained character, position, bind, life stats,
      inventory currency and quest states (`run/ae07-restore-state.tsv`); counts are 1 character / 13 complete / 2 held.
      Both the capture and restore schemas were dropped. Warning, logger and clock-read checks pass.
- [x] **AE-08 — The full `CLAUDE.md` checklist and a checkpoint.**
  - *Done 2026-10-02.* All 33 checklist commands ran from base commit `a0383c37e`; build, solution tests, all
    ratchets, fidelity, Python/PowerShell contracts, navmesh checks and Fast pass after the account correction below.
    Solution tests: 5,068 passed and 93 optional skips (GameServer 4,444/16, Commons 303/0, Login 135/7, Chat 41/1,
    Simulation 145/69). Logs and original results are retained under `run/ae08-checklist`, with separate indexed logs
    for Python/PowerShell commands sharing a name. `ae08-fast` passes 65 tests, three switch skips, all eleven scenarios.
  - The initial retail-inventory check failed because the Leg 7 probes used 195–200, which D32 reserves even before
    its register allocates them. The fixture now accepts six unused accounts: Q2278 201, travel 202, Spirit spots
    203/204, templates 205 and combat 206. Historical run accounts above remain unchanged. The encounter trace now
    names its actual account. No natural-character state, server behavior or content changed.
  - The corrected inventory check passes all 29 tests (`run/ae08-account-reservation.log`).
    `ae08-encounter-accounts` passes on 206 with nine distinct kills, zero deaths/retreats and the same stage timings.
    `ae08-fast-accounts` verifies the other five accounts in the shared suite: 65 tests passed, three switch skips,
    eleven scenarios passed, zero failures/flakes. Warning, logger, clock-read, custom-draft and fidelity checks
    pass again (`run/ae08-accounts`). The original failed check is retained; there are no unblocked AE items left.
  - `altgard-l7` remains the committed-code, hash-checked, relog-verified and restore-checked endpoint from AE-07.
    Leg 7 is done; Leg 8 is the Observatory, from the fortress bind, with Q2146 and Q24115 held.

**Endpoint (AE-Q1 (a), AE-Q2 (a)):**
- Q24233, Q2262, Q2261, Q2263, Q2264, Q2265, Q2272, Q2243, Q2253, Q2278, Q2279, Q2286 and Q2287 completed;
- Q2146 taken and held for Lateni, and Q24115 still held;
- bound at the fortress obelisk (700065);
- alive, within 60 m of it;
- the endpoint verified across a relog.

## Leg 8: Altgard Observatory and Mahindel Swamp (level 21)

**PC-08 scheduling gate:** this leg completes Q2268 and Q2269, opening Q2920 at
level >=16. Hand that conversation to the next capital visit, Leg 9's Q2258 trip.
Q2919's Rainbow Slime Sap belongs to Idun's Lake in Leg 5, not this swamp.

**Status (2026-10-02): done (AO-00..AO-06).** Implementation was authorized by the maintainer's "Implement Leg 8 as needed."
This works the seven-quest outline approved with AE-Q3 (a), using the existing loop protocol with **AO** in place
of NA. The standing bind, hub-flight, held-quest, help-item and death policies apply. No new server content is needed.

**Start:** `altgard-l7`, character 133297, level 21, alive and bound at the fortress, 264,842 Kinah and a 63-slot cube.
The previous 77 quests are completed; Q2146 START/0 and Q24115 START/3 remain held for the Heart of Impetusium.
Q2900/Q24014/Q24015 remain START/0, Q24016 LOCKED/0. Leg 9 and the later Black Claw work stay outside this leg.

**Route and bind.** Take Q2266 at Valurion before departure. The fortress transporter 203561 flies
`df1a_altgardtosanctuary` (34 s, 250 Kinah base) to the Observatory; bind at 700822
(1461.822, 2553.606, 299.214), 2,035 Kinah. Urnir, Neifenmer and Aurtri are within 40 m; Sarad (203657) is the
town merchant. Work the swamp and Lepharist camp from this hub. Q2271 goes to Suthran: take the Observatory
transporter 205259 (`df1a_sanctuarytoaltgard`, 34 s, 250 base), report, then fly back to the Observatory endpoint.
Prices are observed after SM_PRICES. Do not walk between hubs.

| Quest | Source and objective | XP |
|---|---|---|
| Q2266 | Java script: Valurion gives 182203244; Neifenmer at var 0 (SETPRO1), Aurtri at var 1, reward | 27,630 |
| Q2267 | Java MonsterHunt: Urnir, five big cargo boxes 210525 (L17) | 26,370 |
| Q2268 | Java MonsterHunt: Urnir, five longnecked peckus 210526/210527 (L17–18) | 25,335 |
| Q2269 | Java ItemCollecting: Neifenmer, one Operation Order 182203245 from object 700010 | 11,265 |
| Q2270 | After Q2269: three insignia 182203246, 80% from watch bodyguards/shamans | 25,800 |
| Q24113 | Existing D32 template, after Q24112: Aurtri, Tiamat's Sword 182215473 from Commander Gattban | 16,066 |
| Q2271 | After Q2270: Neifenmer gives 182203247; Aurtri at var 0 (SETPRO1), Suthran at var 1, reward | 23,100 |

**Java and retail evidence.** Java `ce54b7931`: `_2266ATrustworthyMessenger`, `_2271AurtrisLetter`,
`quest_script_data/altgard.xml`, MonsterHunt, ItemCollecting, QuestItemNpcAI and TeleportService. Q24113 retains
the approved D32 register, compiled plan and upstream patch; the local 4.8 client and evidence described in
`retail-quest-completion.md` remain its spec. No new deviation is introduced.

**Grounds and hazards.** Eight cargo boxes lie at x 1480–1566, y 2205–2496, z 247, below the Observatory.
Seventeen peckus lie at x 1560–1665, y 2317–2674, z 259–285; some walk randomly. The Operation Order has five
spots at x 1692–1717, y 2589–2658, z 287–293 and a three-second use bar, then a drop list. The Lepharist camp is
crowded with L17 bodyguards/shamans; Q2270's 210529/210531 alternatives have no shipped spots, so hunt 210528/210530.
Gattban (210532) is EXPERT L18, 3,383 HP, patrolling from (1721.12, 2636.07, 291.18), with a bodyguard 6.7 m away
and another three bodyguards/four shamans within 35 m. The bot fights ordinary blockers and clears around an
object before using it. Deaths, retreats and interrupted uses are recorded outcomes.

### Leg 8 TODO list

- [x] **AO-00 — Contract and plans.** Seven quests, five compiled template plans, Java scripted dialog steps;
  Q2266 before the new bind, Observatory town service, retained held quests and endpoint. Verify shipped-data and
  decision tests, plan recompilation and pre-commit checks; Fast for the decision change.
  - *Done 2026-10-02.* The l8 contract starts from AE-08's `734f82eae`, preserves the 77 completions, both held
    quests and campaign states, and ends at Observatory 700822. Five compiled plans and eight script steps cover
    the seven quests. A bind prerequisite offer takes Q2266 before leaving Valurion; already taken/rewarded/completed
    states proceed directly to the new bind. Six shipped-data/decision cases pass (`ao00-contract-fixed.log`).
    The initial build's undefined completion constant was corrected; its log is retained. Plan recompilation,
    D32 inventory, warning/logger/clock/custom-draft/fidelity checks pass. `ao00-fast`: 65 passed, three switch
    skips, all eleven scenarios passed. Q24113's plan matches the existing D32 plan byte for byte.
- [x] **AO-01 — Travel in SIM.** Free probe account, level-21 plans; walk every hub, objective ground and camp/object
  route, including Gattban's spawn, and the fortress/Observatory flight round trip. Record geometry and hazards.
  - *Done 2026-10-02.* `ao01-travel`, free account 207, level-30 controlled probe with level-21 plans: 23 ground legs
    pass, maximum miss 2.5 m. All five Operation Order spots, both pecku kinds, boxes, bodyguards/shamans, Gattban,
    Urnir/Neifenmer/Aurtri/Sarad and both fortress givers route. Three actual flights (352 Kinah each after prices):
    fortress to Observatory, Observatory to Suthran's fortress, then back. Alive at (1464.822, 2553.606, 298.875).
    Route hazards are retained in `run/ao01-travel.log`. The fixture reserves unused 207–209 for Leg 8 probes;
    D32 inventory and warning/logger/clock/custom-draft/fidelity/compiler checks pass. Base `bd7ab9f8c`.
- [x] **AO-02 — Quests in SIM.** A free controlled probe plays both scripted chains and all five templates,
  including the three-second Operation Order use/loot and Gattban's sword. Preserve shared-world probe isolation.
  - *Done 2026-10-02.* `ao02-quests-guard-fixed`, free account 208: both scripts and five templates COMPLETE;
    Q2267/Q2268 five kills each, Q2270 three insignia from four kills, Q24113 Gattban's sword from one kill.
    Operation Order uses the observed 3,000 ms bar and ordinary loot; items are consumed on template hand-in.
    Three actual hub flights cost 352 Kinah each. Q2146 START/0, Q24115 START/3 with 182215477, and all four
    campaign states are preserved. Six contract cases pass (`ao02-contract.log`). Base `4f60569bf`.
  - The failed drafts and setup/dialog attempts are retained under `run/ao02-quests*.log`: reuse campaign states
    already created by class change; Java's shared acceptance page is 1003 and Aurtri's close page is 0.
    Neifenmer is a guard: Java refuses talks during FIGHT/RETURNING. Controlled setup clears his hostile target
    and lets him return before positioning the probe. The packet trace and bounded dialog wait retain the evidence.
  - `ao02-fast` exposed AG-04's assumption that Minushan always pays the Spirit reward page. Its probe now expects
    page 6 for Java's rare Drakie outcome. `ao02-final-fast` exercises that branch (REWARD/2, group 1, 1,900 Kinah)
    and passes all eleven scenarios: 67 tests, three switch skips. Warning/logger/clock/custom-draft/fidelity,
    compiler and D32 inventory checks pass (`ao02-final-checks`). No server content or handler changes.
- [x] **AO-03 — Combat at level 21.** The encounter harness: pecku mix, bodyguard/shaman, Gattban alone and his nearby
  camp at shipped relative spacing. Record distinct kills, deaths and retreats; verify once plus pre-commit checks.
  - *Done 2026-10-02.* `AO03_STAGES=1`, `ao03-encounter`, free account 209, level 21, ordinary journey combat and
    approved help supplies. Four stages at shipped relative spacing: closest L17/L18 peckus (27.93, 4.33 m offset),
    the bodyguard/shaman sharing a patrol origin, Gattban alone, then his nearest bodyguard (-3.43, -5.71) and shaman
    (-22.2, 3.19). Eight distinct confirmed kills in the packet trace (2/2/1/3), zero deaths and retreats; stage times
    55/86/33/99 game seconds. The summary's six kills count planned pulls; two more are defensive kills in the trace.
    Setup clears aggressive neighbours and drops the client view. The read-only dashboard was enabled on 17880.
    Java skills/data and the retail-AI exception were read; no server behavior or content changed.
    Warning/logger/clock/custom-draft/fidelity/compiler/inventory checks pass (`ao03-checks`). Base `0c31a66e6`.
- [x] **AO-04 — Natural runner and smoke.** Restore `altgard-l7`; play `AF_ALTGARD=l8` with the snapshot runner's
  environment, ordinary combat, hub flights and relog verification; drop the owned schema. Fix only demonstrated
  runner gaps. Pre-commit checks and Fast before committing gameplay changes. Never capture uncommitted code.
  - *Done 2026-10-02.* `ao04-smoke-collection` restores `altgard-l7`, completes all seven quests in 1,882,850 game ms
    and verifies the endpoint across relog: level 21, alive at (1484.9, 2539.83, 298.988), Observatory bind 700822,
    317,511 Kinah. Q2146 START/0, Q24115 START/3 with 182215477, and all four campaign states are preserved.
    Q2266 is taken before the first flight; the three actual hub flights cost 352 Kinah each; the Order's observed
    use bar is 3,000 ms. The trace records 23 distinct confirmed kills, one death and two retreats. Schema dropped.
  - `ao04-smoke` and `ao04-smoke-loot` retain the failed source searches. Gattban died during camp clearing; his
    corpse fell outside the 35 m sweep, and the runner searched for a live source until its wait budget expired
    before Java's 295 s respawn. Recovery now revisits deferred corpses on checked safe paths within 60 m, and an
    approach stops when client inventory and its ordinary loot record complete the collection. The natural letter
    delivery uses the existing hub flight selection. No server content or behavior changed. Base `d49a8c722`.
  - Warning/logger/clock/custom-draft/fidelity/compiler/inventory checks pass (`ao04-checks`); `ao04-fast` passes
    67 tests with three switch skips and all eleven scenarios. The read-only bot dashboard was enabled on 17880.
- [x] **AO-05 — Snapshot.** Enable `-Leg l8`, commit before capture, then capture `altgard-l8` from `altgard-l7` and
  hash-check/restore-check it in an owned schema. Record the exact state, source commit and drop both schemas.
  - *Done 2026-10-02.* `ao05-capture`, seed 1, plays from `altgard-l7` on committed code
    `81774eeca11db81c1d34a3bc4948aa89fda8d9fb` and saves the relog-verified `altgard-l8` endpoint.
    Character 133297: Cleric 21, EXP 3,719,444, alive at (1484.9, 2539.83, 298.988), HP/MP/FP 1,568/2,633/60,
    Observatory bind (1462.8, 2553.61, 298.875), 317,511 Kinah, 63-slot cube. All seven quests COMPLETE;
    the 125-entry completed journal, held Q2146 START/0 and Q24115 START/3 with 182215477, and campaign states
    persist in the restored database (`ao05-restore-state.tsv`). One death and two retreats are recorded outcomes.
  - Capture UTC `2026-10-02T16:56:21.2176500Z`, elapsed 51,496,246 ms. SHA256
    `180091adb55d194c11fd846e713018951b7866ef74d7ade65d1b972afa1ae7f7` matches on restore; the snapshot runner's
    resume clock is 51,516,246 ms. Both owned capture/restore schemas are dropped. Pre-commit checks pass
    (`ao05-checks`); the evidence amendment changes only this document, retaining the captured code.
- [x] **AO-06 — Full CLAUDE checklist and checkpoint.** Run every listed command, retain any failures and their
  correction evidence, tick the item and commit on main. Stop here.
  - *Done 2026-10-02.* All 33 CLAUDE checklist commands exit 0 on `23537bb24` (`run/ao06-checklist/results.json`).
    Each command has its own indexed log, including separate Python/PowerShell report, coverage and flake checks.
    Solution: 5,074 passed, 95 reported skips; GameServer 4,450 passed/16 skipped, Simulation 145 passed/71 skipped.
    Warning/logger/clock/custom-draft/fidelity, quest compiler/inventory, evidence/report/coverage/flake/soak checks
    and controller contract tests pass. The live controller checks use their mock Docker harnesses.
    NavBake validates all five baked maps. `ao06-fast`: 67 passed, three switch skips, all eleven scenarios passed.
  - No checklist failures; AO-02's failed drafts and AO-04's failed smoke traces remain retained with their
    correction evidence. The committed `altgard-l8` capture and owned restore check remain valid. Leg 8 is done;
    Leg 9 starts from that snapshot and hands in Q2146/Q24115 at the Heart of Impetusium. Stop at this checkpoint.

**Endpoint:** all seven quests COMPLETE; Q2146 START/0 and Q24115 START/3 held; campaign states preserved;
alive, level at least 21, bound at Observatory 700822 and within 60 m, verified across relog. `altgard-l8` starts
Leg 9. LIVE acceptance remains once at the end of the whole Altgard area.

## Leg 9: Heart of Impetusium (level 21)

**PC-08 scheduled additions:** resume Q2916 START/4 with Banatisai 203673 at
(2614.59,1642.29,254), then approach Suspicious Clothing 700211 at
(2579.04,2190,284.75). Its distance event changes START/5 -> START/6; obtain item
182207007 through ordinary object loot and keep it for Deyla in Leg 10's city visit.
Use the existing Heart bind/recovery and hub flight transporters. During the Q2258
Lindhelm trip, do Q2920 only with both Q2268/Q2269 complete and level >=16; follow the
actual client answer sequence and record its reward group/payout. RC-08 proves both
continuations and the carried clothing through the revised Leg 9 endpoint relog.

**Status (2026-10-02): done (AH-00..AH-06).** The maintainer's goal, "Implement Leg 9 as needed",
works Stop 12 as approved with AE-Q3 (a). Use the loop protocol with **AH** in place of NA. The existing
bind, hub-flight, help-item, inventory and death policies apply. No new server content is needed.

**Start:** `altgard-l8`, character 133297, Cleric 21 alive and bound at the Observatory; 317,511 Kinah, a 63-slot
cube and 84 contract completions. Q2146 is START/0; Q24115 START/3 with its work item 182215477, held for
Lateni and Banatisai. Q2900/Q24014/Q24015 remain START/0 and Q24016 LOCKED/0.

**Route and bind.** Fly Observatory to fortress (205259, 34 s, 250 base), then fortress to the Heart
(203561, `df1a_altgardtoimpetosium`, 52.8 s, 400 base). Bind at 700067 (2656.192, 1660.590, 325.052), 813 Kinah.
The upper pillar holds Andgar, Grak, Gulkalla and Lateni. Banatisai stands below at (2614.59, 1642.29, 254).
Travel verification must prove the height transition before the natural runner uses it. Work the spirit grounds,
four debris objects and wild taygas. Q2260 reports to Suthran: take 205258's Heart-to-fortress flight
(`df1a_impetosiumtoaltgard`, 61.4 s, 400 base). Q2258 uses the existing fortress teleporter 203581, location 7,
500 base, to reach Lindhelm's ordinary Pandaemonium spawn (1275.69, 1290.25, 209.052); learned Return goes to
the Heart bind. Prices are observed after SM_PRICES. Use fortress merchant Donabe (203579) for surplus sales
if needed; the Heart's two merchants take quest coins. Take transporters between hubs.

| Quest | Source and objective | XP |
|---|---|---|
| Q2146 | Held Java ReportTo: report to Lateni | 7,500 |
| Q24115 | Held approved D32 MonsterHunt: report to Banatisai; consume the work item | 16,066 |
| Q2254 | Java ItemCollecting: Andgar, five Soul Essence 182203237, 80% from hero/sorcerer spirits L18–19 | 30,720 |
| Q2255 | Java MonsterHunt: Grak, three mist spirits 210575, L18 | 14,880 |
| Q2256 | After Q2255: Grak, five splash spirits 210577/210522, L18–19 | 24,720 |
| Q2257 | Java ItemCollecting: Banatisai, nine Metallic Fragments 182203238 from debris 700144 | 19,500 |
| Q2259 | Java ItemCollecting: Gulkalla, one Wild Tayga Fang 182203240, 80% from wild tayga 210547, L18 | 36,630 |
| Q2260 | After Q2259: Java ReportTo, Gulkalla to Suthran | 51,000 |
| Q2258 | After Q2256: Java ReportTo, Grak to Lindhelm in Pandaemonium | 53,850 |

**Spec.** Java `ce54b7931`, `quest_script_data/altgard.xml`, ReportTo, ItemCollecting, MonsterHunt,
QuestItemNpcAI, ResurrectAI and TeleportService. Q24115 keeps its approved D32 plan and evidence unchanged.
Debris has a three-second use bar, followed by a drop list; four objects must supply nine fragments through
their ordinary respawns. Hostile L18–19 spirits and taygas patrol the work grounds. Guard NPCs must finish
their fights before dialogs. Q2147/coin loops, gathering, Q2273/[Group], Q24014–Q24016 and Q2900 stay deferred.

**Endpoint:** all nine hand-ins complete, no held quests, campaigns unchanged, alive on Altgard near the
Heart obelisk and bound there. Relog and save `altgard-l9` from committed code; Leg 10 follows from it.

### Leg 9 TODO list

- [x] **AH-00 — Contract and plans.** Register l9, compile all nine template plans, preserve the incoming states,
  bind and city trip; prove shipped source/gate/reward facts and retain Q24115's D32 plan. Depends: AO-06.
  - *Done 2026-10-02.* From AO-06's `37146605b`: 84 incoming completions, seven new quests and both held hand-ins,
    254,866 quest XP, Heart bind 700067 and Lindhelm's ordinary city spawn. All nine plans compile identically;
    Q24115 is byte-identical to its D32 plan. Three contract cases pass (`run/ah00-contract-corrected.log`); an
    analyzer warning in the first passing draft was corrected, with the original log retained. Warning, logger,
    clock, draft, fidelity, compiler and inventory checks pass (`run/ah00-checks`). `ah00-fast`: all eleven
    scenarios, 67 tests pass with three switch-dependent skips. Pillar travel and city template routing await
    AH-01/AH-04; neither is claimed as proved by the contract tests.
- [x] **AH-01 — Travel in SIM.** Free controlled probe; prove upper/lower pillar travel, every objective kind,
  all four debris spots, both hand-ins, merchant and teleporter approaches, and ordinary hub flights.
  Level-21 route plans, logged hazards; no setup teleport used as route evidence. Depends: AH-00.
  - *Done 2026-10-02.* `ah01-travel-final`, free account 210, level-30 probe with level-21 route plans: 30 route legs,
    four actual hub flights (352 Kinah Observatory–fortress; 565 each for the three Heart/fortress flights),
    two ordinary pillar flights, alive endpoint (2659.192, 1660.59, 324.690). Maximum ground miss 2.5 m.
    Upper/lower transition: use the debris landing (2564.68, 1559.74, 249.920), then walk to Banatisai; both
    roughly 240 m free flights land with 40 FP. All four debris spots, all eight spirit/tayga kinds, every
    giver/held recipient, Suthran, Donabe and the city teleporter route. Hazards logged before controlled clearing.
  - Failed drafts `run/ah01-travel*.log` are retained: direct descent to Banatisai is blocked by the upper structure,
    even at higher cruise heights; land outside it first. The nearest L19 sorcerer can be on a disconnected ledge;
    select a shipped source on the current ground island. Applying that source filter to fortress NPCs was too
    strict, so ordinary NPC approaches use their tested ground routes. No natural teleport or server change.
    The fixture accepts free accounts 210–212 for Leg 9. All pre-commit checks and D32 inventory pass
    (`run/ah01-checks`); base `9099d021c`. The natural runner uses this proven landing in AH-04.
- [x] **AH-02 — Quests in SIM.** Free controlled probe; all seven new quests, held Q2146/Q24115, debris use/loot
  and ordinary respawn, Q2260 delivery and Q2258's real city trip/Return. Clear hostiles only in the probe,
  drop the client view before setup teleports and retain any failed drafts. Depends: AH-00, AH-01.
  - *Done 2026-10-02.* `ah02-quests`, free account 211, passes on its first run from `a742d86ca`.
    All nine server/client hand-ins complete; Q24115's work item and collection items are consumed. Fourteen
    controlled 1-HP kills prove the collection/counter contracts. Nine debris uses show three-second bars;
    two 296-second waits restore all four shipped objects through their ordinary 295-second respawns.
    Three actual hub flights cost 352/565/565 Kinah; the city teleporter costs 706. Q2258 completes at Lindhelm's
    ordinary Pandaemonium spawn, then learned Return reaches the Heart bind alive. Campaign states remain
    START/0 for Q2900/Q24014/Q24015 and LOCKED/0 for Q24016. Log and packet trace:
    `run/ah02-quests.log`, `run/ah02-quests.trace.jsonl`. All pre-commit checks pass (`run/ah02-checks`).
- [x] **AH-03 — Combat at level 21.** Encounter harness against hero/sorcerer and mist/splash mixes plus wild
  taygas; use the natural combat policy and approved help items, record kills/deaths/retreats. Depends: AH-01.
  - *Done 2026-10-02.* `ah03-encounter-final`, free account 212, Cleric 21 with the Karmic Staff, learned skills
    and approved supplies: both L18/L19 hero–sorcerer pairs, the mist/L18/L19 splash mix and two wild taygas.
    Shipped relative spots and random walks; four stages, nine distinct client-observed kills, zero deaths and
    retreats. Summary and packet trace are under `run/ah03/ah03-encounter-final`; the dashboard ran at 17880.
  - Retained `ah03-encounter` omitted the neutral mist spirit, so diagnostics now nominate their target kinds
    explicitly. `ah03-encounter-corrected` killed it but stalled against a moving splash spirit: navigation had
    reached its announced destination while combat used its last reported position, repeatedly spending no
    game time. A 350 ms tick on an already-arrived combat approach lets motion/cooldowns advance before the
    next observation; the final probe covers this correction. Base `1af1532a7`; all pre-commit checks and Fast
    pass (`run/ah03-corrected-checks`, `ah03-corrected-fast`: all eleven scenarios, 69 passes/three switch skips).
- [x] **AH-04 — Natural runner and smoke.** Extend template hand-ins to another map, implement only proven
  travel needs, restore `altgard-l8` and run `AF_ALTGARD=l9` once through relog. All nine completions, consumed
  Q24115 work item, campaigns unchanged, transporter hub travel and alive Heart endpoint. Drop owned schema;
  retain failed runs and recorded outcomes. Depends: AH-02, AH-03.
  - *Done 2026-10-02.* `ah04-smoke` restores `altgard-l8` and passes on its first run from `179017b7a` plus the
    AH-04 runner changes. All nine hand-ins and consumed quest work items are verified; Q2900/Q24014/Q24015
    stay START/0 and Q24016 LOCKED/0. Relog preserves Cleric 22, alive at (2663, 1663, 324.69), Heart bind 700067,
    378,680 Kinah and 134 completed journal entries. Elapsed 2,467,828 game ms; 35 distinct confirmed kills
    (each also has a zero-HP status), no deaths and one retreat. The owned restore schema is dropped.
  - Template objectives carry their ordinary recipient's map: travel, hand in Q2258 in Pandaemonium, then
    learned Return to the Heart. Three actual hub flights, eight pillar flights through AH-01's upper/debris
    landings (60 FP, about 27 cost each), nine three-second debris uses and bounded waits through shipped
    respawns. Ground sources prefer connected islands; the runner keeps AO-04's deferred-corpse recovery.
    Deferred incoming quests and completed work-item consumption are checked before the endpoint relog.
    Twenty-one contract/decision cases pass (`run/ah04-decisions.log`); smoke trace and completion record are
    under `run/ah04-smoke`, dashboard 17880. All pre-commit checks and Fast pass (`run/ah04-checks`, `ah04-fast`:
    all eleven scenarios, 69 passes/three switch skips). No server behavior/content changes or natural GM setup.
- [x] **AH-05 — Snapshot.** Enable `-Leg l9`, commit before capture, then capture `altgard-l9` from `altgard-l8`;
  check dump hash, relog endpoint and owned restore, then drop owned schemas. Depends: AH-04.
  - *Done 2026-10-02.* `ah05-capture`, seed 1, plays from `altgard-l8` on committed code
    `96545cd84beeae8b1c050c49d25f2e353c9b376e` and saves the relog-verified `altgard-l9` endpoint.
    Character 133297: Cleric 22, EXP 4,122,844, alive at (2663, 1663, 324.69), HP/MP/FP 1,649/2,738/60;
    Heart bind (2656.19, 1660.59, 325.052), 378,680 Kinah, 63-slot cube and 134 completed journal entries.
    All nine quests COMPLETE, work/collection items absent, Q2900/Q24014/Q24015 START/0 and Q24016 LOCKED/0
    in the restored database (`run/ah05-restore-state.tsv`). No deaths and one retreat are recorded outcomes.
  - Capture UTC `2026-10-02T18:52:32.6647466Z`, elapsed 53,984,074 ms. SHA256
    `8520730dd512758a5c419bd569606f0e6e73c5de9c2f12b4285dc2ba9454793e` matches on restore; resume clock
    54,004,074 ms. Both owned capture/restore schemas are dropped. Pre-commit checks pass (`run/ah05-checks`,
    `run/ah05-evidence-checks`); the evidence amendment changes only this document, retaining the captured code.
- [x] **AH-06 — Full CLAUDE checklist and checkpoint.** Every listed command, evidence and correction of any
  failures, then checkpoint and stop. LIVE acceptance stays at the end of the Altgard area. Depends: AH-05.
  - *Done 2026-10-02.* All 33 CLAUDE checklist commands exit 0 on `da427b420`
    (`run/ah06-final-checklist/results.json`), with indexed command logs. Solution: 5,080 passed, 97 reported
    skips; GameServer 4,456 passed/16 skipped, Simulation 145 passed/73 skipped. Warning/logger/clock/draft,
    fidelity, quest compiler/inventory, evidence/report/coverage/flake/soak and controller contract checks pass.
    Controller tests use their mock Docker harnesses. NavBake validates all five baked maps.
    `ah06-final-fast`: 69 passed, three switch skips, all eleven scenarios passed.
  - The first temporary checklist runner dropped command arguments; its invalid attempt and runner are retained
    under `run/ah06-checklist` and `run/ah06-checklist-draft.ps1`. The corrected runner uses strict mode and
    commands checked against CLAUDE; no actual checklist failures. AH-01's travel and AH-03's combat failures
    remain retained with their correction evidence. The travel log covers eight source kinds, correcting the
    earlier seven-kind evidence count. The committed `altgard-l9` capture and owned restore remain valid.
    Leg 9 is done; Leg 10 starts from that snapshot. Stop at this checkpoint; Q2900 remains last.

## Leg 10: Black Claw ground and the remaining campaigns (level 22)

**PC-08 scheduled additions:** finish Q2916 at Deyla with clothing 182207007 during
Q2283's existing Vidar/capital trip. After Q24016 COMPLETE, obtain Q2938 permission
from Suthran only after seeing page 1011; page 1097 refuses permission. Protect the
granted 182207026 at REWARD/0 for Leg 11. Keep the original campaigns/recovery and
fortress endpoint. RC-09 proves the robe finish, permission pages, carried item
and corrected fortress position through the contained Leg 10 relog.

**Status (2026-10-02): done (BC-00..BC-08).** The maintainer's goal, "Implement Leg 10 as needed",
works AE-Q3 (a) and AL-2's solo group quests. Use the loop protocol with **BC** in place of NA, one item and
one commit at a time on main. The existing hub-flight, bind, help-item, inventory and recorded-outcome policies
apply. Q2900 remains the last Altgard work, after this leg. LIVE acceptance remains at the end of the area.

**Start:** `altgard-l9`, character 133297, Cleric 22, alive and bound at Heart 700067; 378,680 Kinah, 63-slot cube,
93 contract completions and 134 completed journal entries. Q2900/Q24014/Q24015 START/0; Q24016 LOCKED/0.
Fly Heart transporter 205258 to the fortress (`df1a_impetosiumtoaltgard`, 61.4 seconds, 400 Kinah base), then
bind at fortress 700065. Donabe remains the surplus merchant. Accept every eligible fortress quest before
working the Black Claw ground; Taora, Dellalont and Jolk's campaign dialogs are reached on their ordinary
ground routes. Q2283 takes the fortress teleporter to Vidar in Pandaemonium, then learned Return to the bind.

| Quest | Java objective and gate | XP |
|---|---|---|
| Q2273 | After Q2272: Taora; three Emergency Provisions 182203252, 80% from veteran MuMus 210498/210499 | 14,220 |
| Q2277 | Nemnef; ten Mau Feather Trinkets 182203251 from brutal hunters 210551/210552 | 39,600 |
| Q2280 | After Q2286: Morn; four brutal spellshifters 210508/210509 | 33,690 |
| Q2281 | After Q2287: Valurion; all three counters: three seekers, five bodyguards and five scratchers | 83,400 |
| Q2282 | After Q2279: Suthran; three brutal warlocks 210538/210539 | 66,150 |
| Q2283 | After Q2282: Suthran's work item 182203255 to Vidar 204052 in Pandaemonium | 36,000 |
| Q24014 | Dellalont 203665, Jolk 203668; var 2 to 5 through three 210562/216914 kills; at var 5 collect one 182215360 from 210751/216893, return to Jolk | 228,880 |
| Q24015 | Taora; movie 218, var 1; enter `DF1A_SENSORYAREA_Q2021_206013_2_220030000` for var 2, kill three Zemurru's Totems 700099, Suthran reward | 228,880 |
| Q24016 | Opens after Q24011–Q24015; Suthran's Cleric dialog/movie 66 teleports to the Altgard gate; ordinary portal into Bregirun, guardian stone, spawned Kuninasha, Abyss Gate/movie 154 and Suthran reward | 228,880 |

Total: **959,700 quest XP**, six templates and three existing campaign handlers. Java `ce54b7931` is the spec:
`quest_script_data/altgard.xml`, ReportTo, ItemCollecting, MonsterHunt, `_24014StompOutThePlot`,
`_24015TotemPlowed`, `_24016AStrangeNewThread`, PortalAI, ActionItemNpcAI and PortalService.
The older outline describes Haramel with these campaigns: shipped 216914 is there, but 210562 is a shipped
Altgard alternative and both Orb sources are on Altgard. Q24014 can therefore finish entirely on Altgard.
Q24015 is on Altgard; Q24016 requires Bregirun 320030000. No new server content is planned.

**Bregirun contract to prove.** Suthran SETPRO1 puts the quest at var 1 and teleports to
(2467.6052, 2548.0076, 316.12375). Portal 700089 at (2451.53, 2546.45, 317.129) has a three-second use bar
and admits Q24016 at var 1 or above (or COMPLETE); the natural entry is at var 1. Entry is (274.2, 171.7, 204.3),
setting var 2 on enter-world. Guardian stone 700140
at (261.418, 229.531, 213.918) spawns Kuninasha 210753 at (260.12, 234.93, 216), moving the Cleric branch
to var 13; its kill gives var 14. Gate 700141 at (250.91148, 270.45502, 230.087) sets REWARD and movie 154
returns to the fortress. A death, or leaving the instance while START at var 2 or above, resets to var 1.
The level-22 Cleric pre-casts auto-learned Hand of Reincarnation (4005): after a death, self revive in place,
leave through Dimension Exit 700184 and re-enter the ordinary portal for var 2. Respect its ten-minute reuse
before another protected attempt; its buff lasts thirty minutes. The instance offers no instance-revive button.
An unprotected bind revival returns to the fortress, whose ground cannot currently route to the gate; retain
that recovery boundary rather than inventing a repeat Suthran teleport. Dimension Exit is also the fallback exit.
Instance ground routes, local enemies and movie acknowledgements must be proved before the natural run.

**Endpoint:** all nine quests COMPLETE, their work/collection items consumed, Q2900 START/0, no held quests,
alive on Altgard within 60 m of fortress 700065 and bound there, level at least 22, verified across relog.
Capture `altgard-l10` only from committed code, restore-check the dump in an owned schema and drop it.
Gathering, coin loops and unapproved D32 quests stay deferred.

### Leg 10 TODO list

- [x] **BC-00 — Scope and Java/data audit.** Pin the nine quests, campaign gates, portal/instance/death/movie
  behavior, start, endpoint and exclusions; establish this list. Read Java first, inspect shipped data and
  Leg 9 evidence; verify the facts once and run the pre-commit checks. Depends: AH-06.
  - *Done 2026-10-02.* From AH-06's `63d23d4d1`: audited Java `ce54b7931`, all nine shipped quest definitions,
    sources/spawns, bind, portal paths/locations and the Bregirun solo restriction (`run/bc00-spec.txt`).
    Six templates and three campaigns, 959,700 XP; Q2281 has three independent counters and Q24016 opens
    after all five preceding campaigns. Existing Altgard sources avoid a mandatory Haramel trip. The portal
    uses the Java minimum-step gate; death/leave resets the Cleric's instance branch to var 1. Scope and
    BC-01..BC-08 are recorded; no server changes. All pre-commit checks pass (`run/bc00-checks`).
- [x] **BC-01 — Contract, plans and decisions.** Register l10, six compiled template plans, all campaign
  steps, collections, kill/zone/instance transitions and rewards. Prove Q2281 requires all three counters,
  Q24016's complete gate and map transitions, preserved Q2900 and fortress endpoint. Depends: BC-00.
  - *Done 2026-10-02.* From `cabc7fb6e`: l10 contract and six reproducible template plans; all nine quests,
    independent six-bit Q2281 counters, all five Q24016 prerequisites, ordinary city and instance trips,
    gated Orb, zone sphere, guardian/gate use and movie facts, three chosen rewards and fortress endpoint.
    Thirty-six scoped contract/decision cases pass (`run/bc01-contract-final.log`); the earlier nullable-count
    compile error and conditional Vidar-spawn test assumption are corrected, with both draft logs retained.
    Every pre-commit check passes (`run/bc01-checks`); `bc01-fast` passes all eleven scenarios, 69 tests and
    three switch skips. No server changes. Travel and real portal behavior remain BC-02's proof.
- [x] **BC-02 — Travel in SIM.** Free controlled probe, level-22 route plans: every giver/objective kind,
  campaign source, totem, city recipient and Bregirun gate; actual Heart-to-fortress flight. Prove real portal
  entry/exit and the instance's checked ground routes. Clear aggressive neighbours only in the probe;
  drop the client view before setup teleports. Record failed routes and hazards. Depends: BC-01.
  - *Done 2026-10-02.* From `b41e1b19f`, `bc02-travel-city` passes on free account 213, Cleric 22: 54 ground legs,
    all eight givers/services, 20 shipped source kinds (including Q2281's three alternate kinds), the sensory
    sphere and all ten totems. One actual Heart-to-fortress flight costs 565 Kinah, followed by fortress bind.
    Suthran's ordinary dialog/movie/quest teleport reaches the gate ground; portal 700089 has its three-second
    bar and enters Bregirun at var 2. Both enemy kinds, guardian and Abyss Gate route; return through the
    guardian/entry checkpoints reaches Dimension Exit at 6.1 m, within its shipped 7 m talk distance. Its
    five-second use returns to Altgard and resets the quest to var 1. Learned Return reaches the fortress.
  - The city teleporter costs 706 Kinah; 304 checked points reach ordinary Vidar within his 5 m talk distance
    (4.0 m observed), open his dialog and Return again after respecting the 1,200-second reuse. Alive endpoint
    (1660.44, 1815.301, 253.724), fortress bind; ordinary ground miss at most 3.0 m. Hazards logged before probe
    clearing; no natural-character setup. Bregirun's 823-polygon mesh is generated into the repository and
    passes a rebake check (`run/bc02-navbake.log`, `bc02-navcheck.log`), replacing reliance on ignored `run/nav`.
  - Earlier `run/bc02-travel*.log` drafts are retained: the L20 warlock's ground is disconnected from the
    general camp but routes after the quest teleport; spell-range approaches replace the six-metre source
    assumption. A small final-position sight change is recorded separately from reachability. Already-in-range
    interaction, the exit/recipient talk ranges, a misplaced spell sight requirement and an incorrect static
    data accessor were corrected. All pre-commit checks and `bc02-fast` pass (70 tests, three switch skips,
    all eleven scenarios). The fixture reserves free accounts 213–216 for Leg 10. No server behavior/content changes.
- [x] **BC-03 — Template quests in SIM.** Free controlled probe: all six templates, all three Q2281 counters,
  work-item consumption, real Vidar trip/Return and ordinary source respawns when needed. Depends: BC-02.
  - *Done 2026-10-02.* From `86c76155b`, `bc03-templates-final` passes on free account 214, Cleric 22:
    all six server/client hand-ins complete and every collection/work item is consumed. Q2281 refuses hand-in
    at (3, 0, 0) and (3, 5, 0), preserving its packed counters and START status, then completes at (3, 5, 5).
    Thirty-four controlled 1-HP kills, three ordinary source waits and no controlled replenishments; the first
    hunter returns at its shipped spot after 301 seconds. Paid Pandaemonium transport costs 706 Kinah,
    Vidar consumes 182203255 and learned Return reaches the fortress bind alive. Campaign states are preserved.
  - The first probe's deliberate partial rejection tripped the unexpected-dialog-echo detector. It now uses
    the existing explicit expected-rejection API and asserts the exact response; server behavior is unchanged.
    The passing corrected draft's nullable spawn warnings are fixed and the final probe passes without new
    warnings. Logs/traces retained under `run/bc03-templates*`; all pre-commit checks pass (`run/bc03-checks`).
- [x] **BC-04 — Campaigns in SIM.** Free controlled probe: Q24014's kills and gated Orb, Q24015 zone and three
  totems, Q24016 unlock, quest teleport, real instance portal, guardian use/spawn, kill, movie exit and reward.
  Prove death/leave recovery and retained Q2900; setup is never route evidence. Depends: BC-02.
  - *Done 2026-10-02.* From `9a4fdceb2`, `bc04-campaigns-rebirth-final` passes on free account 215, Cleric 22:
    nine controlled 1-HP kills, all three server/client campaign completions, consumed Orb and retained Q2900
    START/0. Abija drops no Orb at var 2 and an ordinary shipped source drops it at var 5. Three stalker kills,
    the sensory zone and three totems complete Q24014/Q24015, naturally unlocking Q24016 only after all five
    prerequisites. Suthran's dialog/movie 66/quest teleport, three-second portal, guardian use and spawned
    Kuninasha, kill to var 14, gate/movie 154/fortress exit and chosen reward all pass. No source replenishment.
  - Ordinary leave/re-entry is proved, plus one controlled death after casting auto-learned 4005. The real die
    hook resets to var 1; the client offers self revival and no instance revival. Rebirth revives in place,
    Dimension Exit's five-second use returns to the gate, and ordinary portal re-entry restores var 2 before
    completing the campaign. The natural runner must protect instance attempts with this learned skill.
  - Earlier bind-recovery probes (`bc04-campaigns`, `bc04-campaigns-ground`) retain the fortress/gate mesh
    disconnection and three empty bounded geometry searches via Taora. Java only teleports at var 0, so no
    repeat teleport or server change is added. A passing completion-only probe is retained separately; the
    first Rebirth draft's pre-death availability assertion was corrected to observe the buff (Java stores its
    revival information on death). The analyzer warning is fixed. Logs/traces: `run/bc04-campaigns*`; every
    final pre-commit check passes (`run/bc04-final-checks`). Deaths remain recorded outcomes, not failures.
- [x] **BC-05 — Combat at level 22.** Encounter harness at shipped relative spacing against the group target
  mixes, campaign sources and Bregirun enemies/Kuninasha. Natural combat, learned skills and approved supplies;
  record distinct kills, deaths and retreats. Read Java and retail AI evidence first. Depends: BC-02.
  - *Done 2026-10-02.* From `298cfb918`, free account 216 completes thirteen stages at shipped relative spacing:
    both veteran/hunter/spellshifter/warlock ranks, the seeker/bodyguard/scratcher camp, stalkers, Abija,
    Gabacha, Bregirun's pair and Kuninasha. Nineteen distinct monsters reach client-observed zero HP; no deaths
    or retreats (`run/bc05/bc05-encounter-final`). Natural attacks and approved supplies, with no 1-HP targets.
  - Java skills/AI and the retail hunter/battle-cycle evidence were read first. The bot catalog now includes
    the observed auto-learned level 21–22 ranks and Divine Spark's third chain step; the final encounter casts
    all five additions. Seventeen policy/catalog tests pass (`bc05-skills-final.log`). The first passing
    encounter is retained; an incorrect extra-mana-reserve assertion is corrected with its failed log retained.
    All pre-commit checks and `bc05-fast` pass. No server changes; actual walking routes remain the natural smoke.
- [x] **BC-06 — Natural runner and smoke.** Implement the proven gaps only; restore `altgard-l9` and run
  `AF_ALTGARD=l10` with the snapshot runner environment through all nine quests and relog. Hub transporter,
  ordinary city/instance travel, campaign recovery, consumed items and unchanged Q2900; dashboard enabled.
  Retain failures/outcomes and drop the owned schema. Depends: BC-03, BC-04, BC-05.
  - *Done 2026-10-02.* From `efb97ae58` plus the runner changes, `bc06-smoke-guardian-defense` restores
    `altgard-l9` with the snapshot runner environment and completes all nine quests through relog in
    12,035,927 game ms. The trace records 185 distinct zero-HP monsters, three deaths and eight retreat
    choices. Alive Cleric 24, HP 1,817/MP 2,949, (1658.57, 1818.39, 253.72), fortress bind and 495,028 Kinah;
    143 completed journal entries, no held quests, all collection/work items consumed and Q2900 START/0.
  - Three actual hub flights (Heart to fortress, then two fortress-to-Heart approaches) and two checked
    pillar descents reuse Leg 9's route; each descent costs 27 of 60 FP and lands with 33. After a bind death,
    the runner waits for the observed Soul Sickness icon to expire before flying. Two successful learned
    Returns, paid Pandaemonium travel and Vidar's ordinary dialog deliver and consume the work item.
    Suthran's quest teleport/movie 66, the three-second Bregirun portal, learned 4005 protection, local
    combat, guardian use/spawn, Kuninasha's kill, gate/movie 154 and fortress reward all succeed. This final
    run has no instance death; BC-04 remains the controlled self-revival/ordinary exit/re-entry proof.
  - The runner now rebinds navigation/combat to the observed map, reloads its view before same-map bind
    revival, and follows real instance use bars and movie returns. Defensive approaches hand actual kills
    and looted items back to their objectives, including the Orb; roads stop once that source is earned
    and replan after revival. The lower hunter/spellshifter/warlock sources avoid disconnected upper camps.
    Observed engagement history, shorter Healing Light after a cancelled Grace, responsive patrol/respawn
    waits, consistent destination hazards and defense before guardian use correct the proven stalls.
    Collection checks follow Java's at-least count and avoid extra deferred corpse loot. Limits are unchanged.
  - All 24 draft traces are retained: fifteen failed and eight deliberately stopped attempts precede the
    passing run (`run/bc06-trial-history.json`). They preserve camp/route stalls, partial source recovery,
    cancelled-heal loops, surplus deferred loot, stale bind views, depleted-flight recovery, Return assertions,
    the already-looted Orb search and the guardian's attack-interrupted bars. No server behavior/content
    changes or natural-character setup. Every owned smoke schema is dropped. Forty-one scoped cases pass
    (`run/bc06-contract-final.log`), all pre-commit checks pass (`run/bc06-checks`), and `bc06-fast` passes
    72 tests, three switch skips and all eleven scenarios.
- [x] **BC-07 — Snapshot.** Enable `-Leg l10`, commit before capture, then capture `altgard-l10` from `altgard-l9`.
  Verify hash, relog endpoint and owned restore; drop both schemas. Depends: BC-06.
  - *Done 2026-10-02.* From BC-06's `55f1a9d0d`, snapshot support was committed as
    `6986e3539b70a2dea518072cf05b65f6807b7950` before `bc07-capture`, seed 1, played from `altgard-l9`.
    The relog-verified `altgard-l10` endpoint is character 133297, Cleric 24, EXP 6,136,722, alive at
    (1658.57, 1818.39, 253.72), HP/MP/FP 1,817/2,949/60; fortress bind (1660.43, 1813.49, 253.726),
    495,028 Kinah, 63-slot cube and 143 completed journal entries. All nine quests COMPLETE, collection/work
    items absent, Q2900 START/0 and no held quests. Three deaths/eight retreat choices remain recorded outcomes.
  - Capture UTC `2026-10-03T02:08:57.8037837Z`, elapsed 66,040,001 ms. SHA256
    `e8a5b75b92b03ca4fffe5dd42b17cf0382fc51b005944f2b5a37cd0ced832d16` matches the dump and passes restore;
    resume clock 66,060,001 ms. The owned restore's SQL state and an actual endpoint relog both pass
    (`run/bc07-restore-state.tsv`, `run/bc07-restore-relog`). Capture and restore schemas are dropped, verified
    by read-only schema queries. Pre-commit checks pass (`run/bc07-checks`, `run/bc07-evidence-checks`);
    this evidence amendment changes only the document, preserving the captured code.
- [x] **BC-08 — Full CLAUDE checklist and checkpoint.** Every listed command with retained evidence and
  corrections; tick, commit on main and stop. Q2900 and end-of-area LIVE follow later. Depends: BC-07.
  - *Done 2026-10-02.* All 33 CLAUDE checklist commands exit 0 on `e43228041`
    (`run/bc08-final-checklist/results.json`), with the exact arguments, source SHA and indexed command logs.
    Solution: 5,105 passed, 100 reported skips; GameServer 4,481 passed/16 skipped, Simulation 145 passed/76
    skipped. Warning/logger/clock/draft, fidelity, quest compiler/inventory, evidence/report/coverage/flake/soak
    and controller contract checks pass. Controller tests use their mock Docker harnesses; the warning baseline
    is unchanged. NavBake validates all six baked maps. `bc08-final-fast`: 72 passed, three switch skips,
    all eleven scenarios passed (`run/bc08-final-fast/report.md`). No checklist failures or corrections.
  - BC-06's failed and stopped trials remain retained with their correction evidence. The committed
    `altgard-l10` capture, hash check, owned restore and endpoint relog remain valid; both snapshot schemas
    are dropped. This checkpoint changes only the document. Leg 10 is done; stop here, with Q2900 still
    START/0 and end-of-area LIVE acceptance later. No push.

## Leg 11: No Escaping Destiny and the Altgard endpoint (level 24 — proposal)

**PC-08 scheduled addition:** turn Q2938 REWARD/0 in at Oubliette with permission
182207026 during Q2900's capital reward visit, then complete the existing fortress
return/relog. Avoid reopening completed Q2938, which teleports to the secret library.
Keep Q2900's stigma bundle sealed; unlocked slots and an owned/socketed stone remain
necessary. RC-09 proves the ordinary Q2938 finish and final relog; the historical Destiny
completion remains valid for its original scope.

**Status (2026-10-03): done (ND-00..ND-08).** The maintainer's goal, "Implement the No
Escaping Destiny", approves ND-00..ND-08. Finish Q2900 last, as the maintainer decided on
2026-10-01. This is one existing campaign across four maps. Work the loop protocol with **ND** in place of NA,
one item and one commit on main. Keep the fortress bind throughout, then return there for the area endpoint.
Permanent stigma selection is deferred: retain the reward bundle unopened; the inactive legacy class item
must remain absent (ND-Q1(a), approved 2026-10-03).
This proposal adds no quests, server content, gear purchases, skill books or natural-character setup.

**Start:** `altgard-l10`, character 133297, Cleric 24, EXP 6,136,722, HP/MP/FP 1,817/2,949/60,
alive at (1658.57, 1818.39, 253.72), fortress bind (1660.43, 1813.49, 253.726), 495,028 Kinah,
63-slot cube, 143 completed journal entries. All Leg 10 quests COMPLETE; Q2900 START/0, no held quests.
The snapshot was captured from committed `6986e3539`; BC-08's `de432d7ba` is the completed-leg checkpoint.
Level 20 and Asmodian are Q2900's shipped gates; it cannot be abandoned. There is no gathering,
coin, shard or level grind in this leg. Q2008 COMPLETE also permits Doman's ordinary Ishalgen service.

### Areas, paths and transport

| Area | Required ground and destination | Transport and route policy |
|---|---|---|
| Altgard 220030000 | Fortress endpoint to Teleporter **Ukin 203581**, (1754.08, 1805.14, 255.917), about 97 m horizontally | AIRLINE_SERVICE, location 7: Pandaemonium, 500 Kinah base. **Lenat 203561 is the hub-flight NPC**, not the capital teleporter. Preserve fortress 700065 bind. |
| Pandaemonium 120010000, first visit | Landing (1685.7, 1400.5, 195.486) to **Heimdall 204182**, (1614, 1397.96, 193.127), about 72 m | Reuse the proved city approach; his Q2900 dialog teleports to Munin's prison. No Doman trip is needed on the successful first pass. |
| Ishalgen 220010000: Munin/prison and cemetery | Arrival (389, 1896, 327.5); approach **Munin 203550**, (378.74, 1895.46, 328.838), then the connected cemetery road to **Garhara 203545** at Anturoon (942.12, 1707.24, 259.8) | Saved graph estimates about **676 m** from Munin to Garhara. Fly Anturoon to Aldelle: location 17, `DF1_SUB_Anturn`, 34 s, 160 Kinah base. Do not walk between these hubs. |
| Ishalgen: Aldelle and Urd | Flight lands (526.25, 2449.65, 281.599); **Urd 790003**, (587.621, 2412.88, 278.557), about 77 m by saved graph from Sheofin's pad | Checked ground approach through Aldelle. Munin to Urd is 558 m straight but **1,947 m by graph**; the flight avoids that long ground circuit. Q2900 does not provide Q2008's inter-NPC teleports. |
| Ishalgen: Verdandi and the Mau farm ground | Urd to **Verdandi 790002**, (938, 2298, 266.125): saved graph about **774 m** to Blabbermouth Angmu, whose node is 2.3 m from Verdandi | Follow the connected road, then prove Verdandi's own interaction approach. She has no named graph node. An NPC position or a neighbouring node is not proof of a usable talk point. This quest-NPC leg is ground travel, not an Aldelle–Anturoon hub crossing. |
| Ishalgen: Skuld east of Anturoon | Verdandi to **Skuld 203546**, (1114.31, 1718.32, 271.179): saved graph about **782 m** | Guarded ground through the road network, actual observed hostiles and checked height/LOS. Her dialog creates the Space of Destiny instance. On returning from the fight, the quest puts the player just beside her. |
| Space of Destiny 320070000 | Entry (270.8424, 249.1182, 125.8369); **instance Skuld 204264**, (245.815, 248.099, 125.837); spawned **Hellion 204263**, (257.5, 245, 125) | Quest teleport, not a portal use bar. Solo instance, shipped entry cooldown 0; no static Hellion or ordinary exit NPC. Generate its navmesh from shipped geometry/mask. Keep inside the platform; map death level is 42. |
| Ishalgen, after the fight | Kill teleport (1112.492, 1718.974, 270.45917) to Skuld; then her quest teleport (383, 1896, 327.625) to Munin | Reacquire the world view even for these same-map moves; approach both NPCs afresh. The killed enemy is not a loot objective. |
| Pandaemonium, reward visit | Munin teleports to (1294.8, 1213.8, 214.34), next to **Aud 204061**, (1294.67, 1216.62, 214.355) | Aud, not Heimdall or Skuld, completes the quest. Claim the fixed rewards normally, then learned Return to the fortress bind. If Return is cooling down, wait safely or walk to Doman for location 9 (500 Kinah base); prove that alternative before relying on it. |

The Ishalgen figures are shortest-length estimates over the existing undirected, checked travel graph
(`run/nd-route-audit.txt`), not a new SIM travel proof or a claim that the straight segment is walkable.
The Munin/Anturoon corridor crosses graph-recorded level 6–8 monsters (210389, 210592–210594, 210404);
the Verdandi/Skuld and Anturoon/Skuld links record 210389. Reuse guarded approaches, defend against real
aggro, rest and re-plan after any revive. Grey monsters can still attack: use observed aggression and LOS,
not expected XP, to decide whether the road is clear. Hub flights send real timed flight frames and land normally.
All fares use observed `SM_PRICES`; earlier SIM capital trips charged 706 for a 500-base service.
The tutorial install below is another service charge, so measure actual Kinah deltas rather than pinning totals.

**Offline planning evidence:** the Space of Destiny bake in `run/nd-plan-nav` has 35 polygons in nine tiles,
SHA256 `c9243fbbc4f11b20d2debc8e8f8801b7bfa5b2ea56be64c1c88ac214c89b2b89`.
Entry to Skuld routes in 13 checked points; entry to Hellion and Skuld to Hellion in seven, all on island 0
(`run/nd-space-entry-routes.log`, `run/nd-space-fight-route.log`). This is geometry evidence only.
No nav files were added to the checked-in folder. The first `points` invocation omitted NAV_FROM/NAV_TO;
its invalid diagnostic is retained at `run/nd-space-points.log` and excluded from route evidence.

### The campaign's unusual state and equipment requirements

Java `ce54b7931` is the default spec: `_2900NoEscapingDestiny`, AbstractQuestHandler, Equipment,
CM_EQUIP_ITEM, StigmaService, QuestService, TeleportService, InstanceService, GeneralInstanceHandler,
SM_DIE, PlayerReviveService and AggressiveNpcAI. The C# quest handler mirrors the Java flow.
Use **full quest vars** for 95–99; the low six-bit var is not their value.

| Observed state | Actual client action | Expected result |
|---|---|---|
| START/0, Heimdall | QUEST_SELECT → 1011; SETPRO1 | START/1 and quest teleport to Ishalgen, channel 1, beside Munin |
| START/1, Munin | QUEST_SELECT → 1352; SETPRO2 | START/2; stay on Ishalgen |
| START/2, Urd | QUEST_SELECT → 1693; SETPRO3 | START/3; stay on Ishalgen |
| START/3, Verdandi | QUEST_SELECT → 2034; SETPRO4 | START/4; stay on Ishalgen |
| START/4, outdoor Skuld | QUEST_SELECT → 2375; SETPRO5 | START/95; new registered solo instance, quest teleport to its entry |
| START/95, instance Skuld | QUEST_SELECT → 2716; SETPRO6; finish/skip **movie 156** with CM_PLAY_MOVIE_END | START/96 only after the movie-end event; movement remains gated while watching |
| START/96, instance Skuld | QUEST_SELECT → 3057; **SELECT7_1** | One temporary Cleric stone **140000001**, Healing Light II; START/99 and page 3058 |
| START/99 | SETPRO7 to open the STIGMA dialog; **CM_EQUIP_ITEM**, action 0, actual item object id, **STIGMA1 = 1L << 30** | Observe inventory-to-equipment move, service charge, skill **11504** and START/97. Sending the dialog alone does not satisfy the equip event. |
| START/97, instance Skuld | QUEST_SELECT → 3398; SETPRO8, after resting/buffing | START/98 and real Hellion spawn, **five-minute lifetime** |
| START/98 | Ordinary learned-skill fight against the observed 204263 | Kill credit gives START/9 and immediately teleports beside outdoor Skuld |
| START/9, outdoor Skuld | QUEST_SELECT → 3739; SETPRO9 | START/10; quest teleport to Munin |
| START/10, Munin | QUEST_SELECT → 4080; SETPRO10 | REWARD/10 and quest teleport beside Aud |
| REWARD, Aud | Ordinary fixed-reward claim, SELECTED_QUEST_NOREWARD | COMPLETE; bundle and fixed rewards observed, inactive legacy item absent; first permanent stigma slot usable |

The level-24 Cleric temporarily qualifies for **one normal stigma slot at START/99**, despite not having
completed Q2900. Installation in Space of Destiny has a **1,000 Kinah base fee**, price-modified by the service.
No Stigma Shards are supplied or consumed. The temporary stone grants 11504; it is separate from the
ordinary learned Healing Light ranks. Protect it from junk selling and automatic equipment rules, equip it
once from observed inventory, and keep enough cube space for cleanup and all reward types (six free slots
is a conservative readiness gate; consolidate/sell genuine surplus at the fortress before leaving).
Do not repeatedly send SELECT7_1: Java's item-give branch is not guarded against duplicate requests.

On departure from the instance at START/9, the enter-world hook unequips and removes the temporary stone.
Prove both inventory and equipment absence and **11504 removal**, while ordinary learned healing remains.
The fight target is level 21, NORMAL/DISCIPLINED, 3,169 HP, ordinary aggressive AI and real damage;
it is **not** Ascension's one-damage Hellion 205041. No extra movie on its kill is specified here.
Rest/buff before SETPRO8, record the spawn deadline, target the current observed object id and react to the
map change immediately after kill credit. Do not wait for loot or approach the old instance corpse.

The shipped reward is 228,880 XP and 25,000 Kinah before configured quest rates, ten Life Potions 162000004,
ten Mana Potions 162000009, Stigma Support Bundle 188053787 and five Tea of Repose 162001057 (Java's
marked custom reward). The listed Cleric legacy item 140000098 is inactive: Q2900 omits `use_class_reward`,
whose default is 0. It is not granted and must be absent at the endpoint. It also has no `<stigma>` metadata;
retain this Java/data boundary without enabling the list or inventing metadata (ND-Q1(a)).
The bundle has class-filtered selectable inert stigmas; retain it sealed for a later permanent-stigma choice.
Do not turn this quest into a stigma shopping/build-selection leg or silently repair the legacy reward.

**Retail version guard:** [NCSoft's 4.8 Upheaval notes](https://static.ncsoft.com/aion/store/PatchNotes/AION_Patch_Notes_061715.pdf),
Stigmas on printed page 26, and [Gameforge's 4.8 notes](https://cmsstatic.aion.gameforge.com/AION_4_8v_Patch%20Notes_EN_30092015.pdf),
pages 21 and 32, confirm the level-20 mission gate, one normal slot at 20, Kinah replacing shards, and the
need to visit a Stigma Master. They do not establish a replacement for Java's tutorial stone; Java's TODO
about class stone ids is not permission to invent one. D32 does not authorize rewriting this existing handler.
Any blocking shared defect needs exact 4.8 evidence and a separate logged decision.

### Deaths, missed spawns and interrupted trips

- **Death at full vars 95–99:** Java removes the temporary stone/skill and resets Q2900 to START/4.
  Prefer the ordinary **fortress bind revival** for this quest. The generic instance policy's in-place
  Hand of Reincarnation does not restore var 95 and cannot resume at the instance Skuld; if a controlled
  probe exercises it, prove the subsequent ordinary exit and new outdoor-Skuld entry separately.
  GeneralInstanceHandler supplies no instance-revive button; do not manufacture one.
- **Leave or cold re-entry outside Space of Destiny at 95–99:** the enter-world hook cleans the stone
  and resets to 4. At 9 outside, cleanup happens but progression stays 9. Relogging within a still-existing
  instance must be observed separately from restarting the server, which loses that ephemeral instance.
- **Recovery from the fortress:** Ukin location 7 → Pandaemonium; checked approach to Doman 204191 →
  location 8 (100 Kinah base, Q2008 COMPLETE) → Aldelle. Use **Sheofin 203513**, location 18,
  `DF1_SUB_Alder`, 41 s, 160 Kinah base, to Anturoon, then its roughly 178 m checked ground to Skuld.
  Repeat her var-4 quest dialog for a new instance. Never replay Heimdall's var-0 teleport at later vars,
  skip directly to var 95, bind in Ishalgen or use a setup teleport on the natural character.
- **Hellion expires:** Java leaves START/98 and its SETPRO8 only works at 97, so a repeat click cannot
  resummon it. Record the missed five-minute window, use learned Return out of combat (respect its real
  cooldown), observe the outside reset to 4, and take the recovery trip. No abandoned-quest shortcut exists.
- **Restart at vars 1–3:** from the ordinary Doman/Aldelle entry, route to the recipient for the observed
  var; use the hub flight when the route crosses Anturoon/Aldelle. At var 10 reach Munin through the same
  checked prison road; at REWARD reach Aud normally. A failed relog must not restart already-completed talks.
- At every teleport, clear the client's old view before the initiating action, perform actual movie/world/
  channel acknowledgements, rebind navigator/geometry/combat to the observed map and instance, and reacquire
  targets. Preserve counters and death spots. Keep all existing revive, progress and approach limits.
  Deaths, expired spawns and lost attempts are recorded outcomes; stalls/unmet objectives remain failures.

**Endpoint:** Q2900 COMPLETE, all 143 incoming completed quests preserved (144 journal completions),
all Leg 10 quests still COMPLETE, no held Q2900 work, temporary 140000001 and 11504 absent, reward bundle
retained and inactive legacy item 140000098 absent, permanent first normal stigma slot available, Cleric level at least 24, alive
on Altgard within 60 m of fortress 700065 and still bound there. Verify across relog, including learned
skills, inventory/equipment, quest and completed journals. Pin the actual EXP/Kinah deltas, not an invented
level or an exact fee copied from another service. Capture `altgard-l11` only from committed code.

### Leg 11 TODO list

- [x] **ND-00 — Confirm scope and spec.** Re-read decisions, BC-00..BC-08 and the sources above; confirm the
  start dump, endpoint, reward boundary and every recovery branch. Log any decision under Blocked; no
  server-content authority beyond D26–D33. Depends: BC-08 and approval of this proposal.
  - *Done 2026-10-02.* From `eebceefa7`, the implementation goal approves this scope. Re-read AL-1..AL-5,
    BC-00..BC-08, the loop protocol, Java `ce54b7931` Q2900 and the full-var/stigma helpers; C# preserves
    the handler's transitions, death/outside cleanup and unguarded dialog quirks. The `altgard-l10` dump
    hash matches; its verified endpoint is Cleric 24, 143 completed quests, all nine Leg 10 quests complete,
    Q2900 START/0 and fortress bind (`run/nd00-audit-final.log`). Four maps, five quest teleports, actual
    movie/equip events, five-minute enemy, ordinary bind recovery and sealed/legacy rewards are pinned above.
    No new server-content authority or blocking decision. The first audit assertion used names instead of
    the checkpoint's numeric status/StepAndFlags representation; corrected without changing the dump,
    with the draft retained (`run/nd00-audit-draft.log`). All seven pre-commit checks pass (`run/nd00-checks`).
- [x] **ND-01 — Contract and decisions.** Register l11 with Q2900's full vars, four allowed maps, named
  transitions, movie, actual stigma equip event, timed dynamic enemy, cleanup and fortress endpoint.
  Cover late-var and REWARD resumes. Add only narrowly scoped Cleric identity support for the quest instance;
  the current identity rules do not accept 320070000. Preserve Ishalgen/Ascension and l1–l10 decisions.
  Depends: ND-00.
  - *Done 2026-10-02.* From `588f15ea3`: l11 registers Q2900, all 143 incoming completions, four allowed
    maps, twelve named dialog/equip steps, four dialog teleports plus the kill teleport, movie 156,
    actual stigma slot/stone/skill/fee, the five-minute Hellion and fortress/reward/cleanup endpoint.
    Its full-variable reader strips the wire flag byte; other quests retain six-bit counters. Decisions
    cover every phase, late-var/REWARD trips, bind death, inside/outside reset and duplicate/missing stone
    recovery. Space identity requires an explicit l11 scope and a level-20 Cleric; default bridge maps stay
    unchanged. No server behavior or content changes.
  - Eighty-one scoped contract/decision/Leg 10/identity cases pass (`run/nd01-contract-final.log`). Two
    draft assertions counted every teleport as a dialog and expected a different existing return action;
    corrected with the kill destination explicit, draft retained (`run/nd01-contract.log`). All seven
    pre-commit checks pass (`run/nd01-checks`); `nd01-fast` passes 72 tests, three switch skips and all eleven
    scenarios. Actual travel and packet protocol remain ND-02/ND-03's proof.
- [x] **ND-02 — Paths and map protocol.** Generate/check in/rebake the Space navmesh from existing inputs;
  prove city approaches, prison/cemetery road, Urd, Verdandi's own talk point, Skuld and all instance ground.
  Actually fly Anturoon→Aldelle and recovery Aldelle→Anturoon. Prove Ukin→capital, Doman→Ishalgen,
  Doman→Altgard fallback and all five quest teleports with view invalidation. Free probe account, clear
  aggressive monsters at setup spots, BeginWorldReload before setup teleport; setup is not travel evidence.
  Depends: ND-01.
  - *Done 2026-10-03.* From `da332d249`, free account 217 passes `nd02-travel`: 24 checked ground legs,
    fortress/Ukin/Heimdall, Munin's connected prison/cemetery road in both directions, Urd, Verdandi's own
    talk point, both Skulds, Aud/Doman and the instance entry/boss ground. Actual Anturoon→Aldelle (34 s)
    and Aldelle→Anturoon (41 s) flights cost 226 Kinah each. Ukin→capital twice costs 706 each, Doman→Ishalgen
    costs 141 and Doman→Altgard costs 706; learned Return preserves the fortress bind. Five quest teleports
    and full vars pass, including same-map Skuld return and no old Hellion object after kill teleport.
    Alive endpoint (1665.44, 1815.301, 253.6685), with no probe deaths.
  - The controlled var-97 setup and 1-HP enemy prepare transport triggers only; movie/equip progression
    remains ND-03's proof. Aggressive route neighbours are cleared, setup teleports invalidate the client
    view first, and setup is not route evidence. The actual solo instance registers only this probe.
    Generated Space mesh: 35 polygons, nine occupied tiles, SHA256
    `c9243fbbc4f11b20d2debc8e8f8801b7bfa5b2ea56be64c1c88ac214c89b2b89`; rebake check passes
    (`run/nd02-navbake.log`, `run/nd02-navcheck.log`). Two compile drafts corrected private-member/owner
    access and the object callback's array type, retained as `run/nd02-build*.log`; travel passes on its
    first run. Every pre-commit check and `nd02-fast` pass: 73 tests, three switch skips, all eleven scenarios;
    Fast also verifies explicit solo registration. Owned probe/Fast schemas are dropped, confirmed by the
    empty read-only schema query (`run/nd02-schema-check.log`). No server changes or new content.
- [x] **ND-03 — Movie, stigma and campaign protocol.** Controlled probe plays every dialog, movie 156,
  SELECT7_1, STIGMA dialog and CM_EQUIP_ITEM. Observe full vars 95→96→99→97, real fee and 11504;
  summon/kill the real enemy with controlled HP, cleanup, Skuld/Munin teleports and Aud's actual reward.
  Prove complete journals and cleanup. On the probe only, use a valid inert stigma from its real reward
  bundle to demonstrate the one-slot post-quest rule; the natural endpoint keeps its bundle unopened.
  Depends: ND-02.
  - *Done 2026-10-03.* From `ffd33ce5e`, ND-Q1(a) corrects the contract, normal Aud action and endpoint:
    SELECTED_QUEST_NOREWARD claims the shipped fixed rewards and sealed bundle; inactive 140000098 must
    be absent. The campaign now uses that exact contract without a probe-only reward override. Movie,
    equip, fee, full vars, cleanup, 144 journal completions, actual rewards and the probe-only normal-slot
    installation all pass in `nd03-approved-fast`. Eighty-one scoped contract/decision/Leg 10/identity
    cases pass (`run/nd03-approved-contract.log`), all seven pre-commit checks pass
    (`run/nd03-approved-checks`), and Fast passes 74 tests, three switch skips and all eleven scenarios
    (`run/nd03-approved-fast/report.md`). The owned schemas are dropped (`run/nd03-approved-schema-check.log`).
    Earlier failures and their corrections below remain retained. No server code/data or natural-character setup changes.
  - *Initial draft 2026-10-03; reward blocker resolved by ND-Q1(a).* From `ffd33ce5e`, free account 218 proves movie 156's real
    acknowledgement, full vars 95→96→99→97, actual STIGMA1 equipment/skill 11504 and the 1,412-Kinah
    tutorial fee. A controlled-HP real kill triggers var 9 and the normal exit cleanup; temporary item,
    equipment and skill are absent while learned healing remains. Skuld/Munin's teleports and Aud's reward
    preserve the 143 incoming completed journal entries and add Q2900 (`nd03-campaign-a4`). Actual rewards
    are +228,880 EXP, +25,000 Kinah, ten Life/Mana Potions each, five Tea and the sealed bundle. On this
    probe only, the actual bundle yields Saving Grace (Inert) 140001431, equipped in the normal slot for
    35,312 Kinah. The expected legacy class item is absent; Java and C# both disable that list. The
    approved endpoint and contract cannot pass as written; see ND-Q1. No natural character was run.
  - The first probe checked the var before its movie-end acknowledgement was processed; now the helper
    observes the movie before the synchronization barrier. The second exposed the inactive class reward;
    the third had a wrong static-data property name. All attempts remain retained (`run/nd03-campaign.log`,
    `nd03-campaign-a2.log`, `nd03-campaign-a3.log`); the fourth claims the actual fixed reward without a
    class selection. Controlled prerequisites now also complete existing starter quest rows, with both
    server and client journal assertions; both Destiny probes pass again in `nd03-fast`. All seven
    pre-commit checks pass (`run/nd03-checks`), and Fast passes 74 tests, three switch skips and all eleven
    scenarios (`run/nd03-fast/report.md`). Owned ND-03 schemas are gone (`run/nd03-schema-check.log`).
    This draft preceded the ND-Q1(a) approval below; its failed attempts and evidence remain retained.
- [x] **ND-04 — Loss and resume recovery.** Controlled probes record a five-minute miss, death before/after
  equip, outside exit, live-instance relog and cold restart. Verify resets, no leaked stone/skill, and
  recover through the ordinary Doman/flight/outdoor-Skuld route. Test safe resumes at 1–4, 9, 10 and REWARD;
  no fast-forwarding the natural character. Keep each failure and correction. Depends: ND-03.
  - *Done 2026-10-03.* From `8737b6b66`, free account 219 passes
    `nd04-recovery-a5`: two controlled deaths before/after equip reset to 4, remove the temporary stone/skill
    and revive by the actual fortress bind; observed Soul Sickness expires before recovery flights. Real
    outside exits, Ukin/Doman services, Aldelle→Anturoon flights and fresh Skuld entries recover every loss.
    The real five-minute Hellion remains before its deadline and disappears afterward during an offline
    interruption; START/98 persists, SETPRO8 is refused, and ordinary Return/reset/re-entry recovers it.
    Saved START/1–4, 9, 10 and REWARD resume only the remaining recipient; 65 ground legs, all 144 journal
    completions, sealed bundle and fortress bind retained. No Hellion immunity, HP edits or natural setup.
  - The var-97 live relog preserves progression but unequips the stone, removes 11504 and emits Java's
    slot-gate audit. After several attempts, it selects registered instance 2 from saved instance 4:
    Java/C# `getRegisteredInstance` returns the first still-registered instance, including an older attempt.
    The probe records that observed instance and then proves outside cleanup and a fresh attempt; no server
    correction is needed or authorized. Known audit/warning boundaries are explicitly asserted and recorded.
    Two wrong-property compile drafts, a stale generic persistence expectation and a same-instance assumption
    are corrected with all evidence retained (`run/nd04-recovery.log`, `nd04-recovery-a2.log`,
    `nd04-recovery-a3.log`, `nd04-recovery-a4.log`). The ground-position assertion now uses the actual walked
    position; it does not weaken the saved-position comparison.
  - `nd04-cold-a2` passes in two actual server/test-host processes against one owned GUID schema. The same
    character 133276 persists equipped at START/97 inside Space, then observes the missing-exit warning,
    stigma-login audit, fortress fallback and START/4 cleanup in the new process. Ordinary Ukin/Doman/flight/
    outdoor-Skuld recovery reaches a new START/95 attempt. Both phases' logs and packet traces are retained;
    the schema is dropped in finally. The first cold run reached/recovered correctly but failed log policy
    because the bot acknowledged two identical spawn announcements, causing AlreadySpawnedException.
    Java emits both announcements on this fallback; the bot now coalesces an identical pending map load
    until SM_PLAYER_INFO confirms it, while distinct destinations, later loads and new logins still acknowledge.
    Thirty-nine reflex/API cases pass (`run/nd04-reflexes.log`); no server fix or allowlist change.
    Failed cold evidence and its cleanup remain at `run/nd04-cold`, `run/nd04-cold-runner.log`.
  - Every pre-commit check passes (`run/nd04-final-checks`); `nd04-final-fast` passes 75 tests, four
    switch skips and all eleven scenarios. Its restart guard skip is covered by the separate two-process
    proof above. Fast re-verifies both earlier Destiny probes and the full recovery matrix with the new
    acknowledgement behavior. The empty read-only query confirms all owned matrix/Fast/cold schemas are
    dropped (`run/nd04-schema-check.log`). No server changes or new content; ND-05 normal combat is next.
- [x] **ND-05 — Normal combat.** One level-24 probe fights normal-HP Hellion, learned skills/approved
  supplies, temporary skill available, real deadline, no invulnerability or damage edits. Exercise cleanup
  while preserving ordinary heals. Record kills, deaths/retreats and duration; prove no stale corpse action
  after kill teleport. Depends: ND-03 and ND-04.
  - *Done 2026-10-03.* From `809f304dd`, free account 220 copies the seven worn `altgard-l10` items and
    receives only the approved level-24 supplies; its HP/MP are 1,809/2,935. The actual movie, stone,
    socket, Skuld spawn and the journey's ordinary rest/buff/combat policy kill unmodified 3,169-HP
    Hellion in 16,371 ms from the spawn dialog, with zero deaths/retreats. Ordinary attacks deal
    175–200 damage. Kill credit gives Ishalgen START/9, no corpse sweep, no old object/loot state,
    tutorial item/equipment/11504 cleanup, all regular learned skills preserved, then real Skuld START/10
    (`run/nd05-combat-a4.log`, its trace/summary/packet evidence). No immunity, damage edits or natural setup.
  - The Java level-23/24 audit adds regular auto-learned Hallowed Strike 1618, Healing Grace 4204 and
    instant Flash of Recovery 3951; Cleanse III 3937 is explicitly excluded like the earlier ranks.
    These are regular skills, not stigma grants. Tutorial 11504 is available only through the actual
    140000001 socket and disappears on cleanup; the natural reward bundle stays sealed.
    Java NpcController sends quest-kill teleport before finishing drop registration, so the client must
    clear old map objects on the teleport announcement and again on destination spawn to discard a late
    loot-status packet. Combat skips its ordinary corpse sweep when the kill changes maps.
  - Retained failed drafts: `run/nd05-combat.log` omitted the ItemService namespace; a2/a3 killed
    normally but caught the late old-corpse loot status. The final view regression and level-24 skill
    checks pass (33 cases, `run/nd05-unit-final.log`). Seven pre-commit checks pass (`run/nd05-final-checks`);
    Fast passes (76 tests/four switch skips, all eleven scenarios, `run/nd05-final-fast/report.md`).
    All owned ND-05 schemas are gone (`run/nd05-schema-check.log`). ND-06's natural runner is next.
- [x] **ND-06 — Natural runner and smoke.** Implement only the required l11 behavior, inventory protection,
  normal reward and observed-var recovery. Restore `altgard-l10` with sim-snapshot.ps1 -Action Restore;
  run NaturalIshalgenPriestCompletesFrozenJourneyWithoutSetup with `AF_ALTGARD=l11` and Invoke-NaturalJourney's
  environment, dashboard enabled; play once through endpoint relog and drop the owned schema in finally.
  No setup on character 133297 beyond OD-13 supplies. Depends: ND-01..ND-05.
  - *Done 2026-10-03.* From `25ee13686`, l11 executes the proved dialog/movie/socket/fight/reward sequence,
    forces the Anturoon–Aldelle hub flight, protects the tutorial stone and sealed bundle, and rebinds navigation
    after quest teleports and bind revival. Observed vars drive ordinary Return/capital recovery; missing
    socketed skill at 97/98 leaves normally rather than treating a stigma as a learned skill. SIM and LIVE
    relog identity validation receives the explicit leg scope; ordinary identity guards remain scoped.
  - `run/nd06-smoke/journey.log` passes once from an owned `altgard-l10` restore with Invoke-NaturalJourney's
    environment and the dashboard enabled. Character 133297 completes all 144 journal entries and relogs alive,
    level 24, at fortress 700065 with its bind preserved. Nine distinct kills, no deaths/retreats, one paid hub
    flight, actual movie 156, one tutorial socket (1,412 Kinah), normal Hellion kill in 12.021 game seconds,
    immediate exit without corpse looting, Aud's normal reward and learned Return are retained in the trace.
    Elapsed process clock 593,000 ms; the restored clock offset is 66,060,001 ms. EXP increases 239,148
    (wire progress 26,524 → 265,672); Kinah 495,028 → 536,193 includes ordinary sales, fares and fixed reward.
  - Endpoint checks before/after relog preserve incoming skills and all 143 incoming completions, require
    bundle 188053787 ×1 sealed and temporary stone/skill plus inactive legacy item absent. Regular learned
    heals 1842/4204/3951 remain available. No gear or skill-book purchases, permanent stigma choice, server
    change or natural setup beyond OD-13. Owned schema dropped in finally and read-only SQL confirms absence
    (`run/nd06-schema-check.log`). Build and 81 scoped cases pass (`run/nd06-build.log`, `run/nd06-unit.log`);
    seven pre-commit checks pass (`run/nd06-checks`); Fast passes 76/four switch skips/all eleven scenarios
    (`run/nd06-fast/report.md`). ND-07 committed snapshot capture is next.
- [x] **ND-07 — Snapshot.** Enable `-Leg l11`, commit before capture, then capture `altgard-l11` from
  `altgard-l10`; hash-check, owned restore, SQL state and actual endpoint relog; drop both owned schemas.
  Depends: ND-06.
  - *Done 2026-10-03.* After ND-06's `3201d79c4`, snapshot support was committed as
    `e23d5a5112b852b1481fe6cc665ab9dd9a3e4979` before `nd07-capture`, seed 1, played from `altgard-l10`.
    `run/snapshots/altgard-l11` retains character 133297, Cleric 24, EXP 6,375,870, alive at fortress bind
    (1660.43, 1813.49, 253.726), HP/MP/FP 1,929/2,446/60, 536,193 Kinah, 63-slot cube and 144 completed
    journal entries. Q2900 and all nine Leg 10 quests are COMPLETE, work items absent, temporary stone/11504
    and inactive legacy item absent; bundle 188053787 ×1 remains sealed. No deaths or retreats in capture.
  - Capture UTC `2026-10-03T05:34:22.0081713Z`, elapsed 66,653,001 ms. Dump SHA256
    `3be190f37f9c39f716de7ab1fac6a655a9da5ea80f553c399f009cddb2bd4029` matches metadata and passes restore
    (`run/nd07-hash-check.json`); resume offset 66,673,001 ms. Owned restore SQL and actual endpoint relog pass
    (`run/nd07-restore-state.tsv`, `run/nd07-restore-relog/journey.log`), including ordinary learned skills and
    inventory/equipment/journal/bind persistence. Both owned schemas are dropped; read-only schema query is
    empty (`run/nd07-schema-check.log`). Java quest/skill persistence and stigma login gates were re-read.
    PowerShell parse and all seven pre-commit checks pass (`run/nd07-support-checks`, `nd07-evidence-checks`).
    This evidence amendment changes only the document, preserving captured code. ND-08 full checklist is next.
- [x] **ND-08 — Full CLAUDE checklist and checkpoint.** Retain every listed command/verdict and corrections,
  tick and commit on main, then stop. Pre-commit checks on every item; Fast before gameplay-change commits.
  No push. Depends: ND-07.
  - *Done 2026-10-03.* All 33 CLAUDE checklist commands exit 0 on `7f3289b4f`
    (`run/nd08-final-checklist/results.json`), retaining exact arguments, source SHA and indexed logs.
    Solution: 5,144 passed, 105 reported skips; GameServer 4,520 passed/16 skipped, Simulation 145 passed/81
    skipped. Warning/logger/clock/draft, fidelity, quest compiler/inventory, reporting/coverage/flake/soak
    and controller contract checks pass. Controller tests use mock Docker; warning baseline unchanged.
    NavBake validates all seven baked maps, including Space of Destiny. `nd08-final-fast` passes 76 tests,
    four explicit switch skips and all eleven scenarios (`run/nd08-final-fast/report.md`).
  - The initial checklist launcher failed before dispatching any command because a copied PowerShell splat
    name was not renamed; corrected launcher passes the complete list, retaining the original diagnostic
    (`run/nd08-checklist-launch-draft.log`). Earlier ND-03..ND-05 failed drafts and their correction evidence
    remain retained. ND-06's first natural smoke and ND-07's committed capture/hash/SQL/restore/relog all pass.
    Owned snapshot and final Fast schemas are gone (`run/nd08-schema-check.log`). This checkpoint changes
    only the document. Leg 11 is done; stop here. Permanent stigma selection and full-area acceptance remain
    the separate later work described below. No push.

### ND-04 recovery coverage

ND-04 uses free probe account 219 for the loss/resume matrix. Read Java's
die, revival, enter-world, instance lifetime and saved-position rules before preparing each case. Keep
the fortress bind, complete the controlled prerequisites on the probe, and preserve the client view
invalidation and real acknowledgements already proved in ND-02/ND-03.

| Case | State to observe | Ordinary path back to progression |
|---|---|---|
| Five-minute Hellion miss | A real SETPRO8 spawn remains before its deadline and expires afterward; START/98 remains and SETPRO8 cannot spawn again | Learned Return out of combat, respecting cooldown; outside entry cleans to START/4; take the fortress recovery circuit below |
| Death before equip and after equip | Die hook resets 95–99 to START/4; no temporary stone in inventory/equipment and no 11504; learned heals remain | Actual fortress bind revival, then the recovery circuit; record the death, never count it as a failed quest |
| Ordinary outside exit at 95–99 | Enter-world cleanup resets to 4 exactly once; no duplicate stone or leaked skill | Return, recovery circuit, fresh outdoor-Skuld entry; the indoor var-4 dialog cannot repair the old attempt |
| Relog with a live instance | Observe the real registered instance, retained var and item/skill consistency across quit/login; distinguish it from an instance that has already expired | Continue only from the observed map/var; if outside, use the reset/recovery rule |
| Cold restart | Two real SIM processes against one owned probe schema; persist inside, stop the first process, then reload without the old ephemeral instance | Observe Java's saved-position fallback and quest cleanup, then recover by normal transport; no setup teleport on resume |
| Late vars 1–4 | Recipient is Munin, Urd, Verdandi or outdoor Skuld for the observed var; completed talks stay completed | Doman to Aldelle; for Munin or Skuld take Sheofin to Anturoon and the checked ground; Urd/Verdandi use their proved ground approaches |
| Vars 9, 10 and REWARD | Var 9 reaches outdoor Skuld; var 10 reaches Munin; REWARD reaches Aud; no early dialog replay | Doman/Aldelle flight and Skuld/prison ground as needed, then only the remaining quest teleports; reach Aud normally when already at REWARD |

Two Java login boundaries are proved by ND-04. Space has no `instance_exit.xml`
row; `InstanceService.onPlayerLogin`/`TeleportService.moveToInstanceExit` produce the observed bind
fallback, with a missing-exit warning, after a cold restart. Also `PlayerEnterWorldService` calls
`StigmaService.onPlayerLogin` before instance relocation: its quest-slot gate accepts START/99 or
COMPLETE, while the actual equip changes Q2900 to 97. Its revalidation consequently implies an
observed unequip/audit on a var-97 relog, even when an instance survives. After multiple attempts, login
can also choose an older registered instance, as Java's first-match lookup specifies. These behaviors
are recorded boundaries, not authority to change the server. The bot observes the resulting map/instance
and uses the proved ordinary outside reset/new-entry recovery when its temporary skill is lost.
Any necessary server correction goes to a separate operator decision.

The recovery circuit is **fortress → Ukin → Pandaemonium → Doman → Aldelle → Sheofin's 41-second
flight → Anturoon → outdoor Skuld → new Space of Destiny**. The successful first trip is already
proved: **Ukin → Heimdall → Munin → Anturoon flight → Aldelle/Urd → Verdandi → Skuld → Space →
Skuld → Munin → Aud → fortress**. These use the paid services and checked ground, retaining the
fortress bind. Cold-restart schemas belong only to the probe and are dropped in finally. Each case
records the map, instance, full quest var, temporary item/skill, fees and outcome; setup is labelled as
setup, and the recovery journey itself uses ordinary packets. Keep existing revive/progress/approach
limits. ND-05 then proves the normal-HP fight; ND-06 runs the natural character only after these proofs.

### Coin gear preparation and Leg 12: Haramel (SIM complete 2026-10-03)

The operator requested Q2293 at the Heart while bound there, the better-tier coin chain gear,
then the quests for Haramel and the dungeon as **Leg 12**. The later armour review instruction
retains the equipped level-21 staff and excludes coin weapons; shields are excluded because the
staff occupies both hands. Only the revised three-item coin armour manifest is recommended;
other shopping and permanent stigma selection remain excluded.

The [coin plan](natural-altgard-coin-gear.md) audits the saved **18 Iron Coins** against eighteen
completed quest rewards. Recommend Lohaban's **chain gloves, shoulders and legs for 4 coins**,
retaining the equal-stat chest and boots; **zero funding repeats** are needed. One natural Q2293
completion for coverage leaves **19 Iron Coins** after purchase. Chain gloves trade some casting
stats for defence/healing; retain the old cloth gloves. There is no higher-priced Heart chain tier
or coin helmet/accessories. Keep staff 101501357 equipped throughout preparation and Haramel;
do not buy a coin weapon/shield or switch the staff for reward/loot weapons.

The [Haramel plan](natural-altgard-haramel.md) completes eleven active Asmodian quests once, including
the outside lead-in and four quests that unlock after Q28507. Two fresh clears prove ordinary empty
instance expiry, all floors/lift/objects, the 65-kill packed counter and Cleric boss chest. Its incoming
snapshot is `altgard-coingear`; its final snapshot is `altgard-haramel-l12`, alive/bound at the fortress.
CG-00..CG-06 and HM-00..HM-08 are complete in SIM. Stop at this verified checkpoint: Cleric 25,
156 journals, 19 Iron/7 Bronze, retained equipped staff/coin chain, sealed bundle and no stigma skills.
Both captures and actual endpoint restore/relog are verified; historical Leg 1 `altgard-l12` is unchanged.
The continuous full-area SIM is verified below; isolated LIVE acceptance remains later work.

### End-of-area acceptance after HM-08

**SIM scope updated by the operator, 2026-10-03:** run once from character creation through
Ishalgen, Ascension, l1–l11, CG and l12 in a single SIM world/process and virtual clock. This
extends the original level-10 acceptance start below. Retain Q2217's corrected delivery, the
staff and three approved coin purchases; Q24114 remains intentionally excluded. No LIVE run
is authorized by this request.

- [x] **AS-01 — Join the existing journeys.** Add explicit SIM-only `AF_ALTGARD=all` and a runner;
  create a fresh Priest, then execute the existing bridge and thirteen approved Altgard legs.
  Keep character/world/clock/journals and the original cumulative twenty-revive limit across
  transitions. Bind endpoint counts and retained staff identity to observed incoming state so
  Q2217 and fresh object IDs survive; preserve historical snapshot contracts. Verify focused
  continuation/Altgard tests, seven pre-commit checks and Fast, then commit before the full run.
  Evidence (2026-10-03): `NaturalAltgardContinuation` binds the observed incoming journal and staff
  to each shipped contract without rewriting it. The shared runner creates once, uses the existing
  bridge and thirteen legs, retains the cumulative twenty-revive budget, and writes per-leg relog
  proofs plus a continuous stage report. Focused continuation/Altgard/Haramel tests: 150 passed.
  All seven pre-commit checks passed (`run/as01-checks`); Fast: 82 passed / five explicit switch skips,
  all eleven scenarios passed (`run/as01-continuation-fast`, base `8a531614f`).
- [x] **AS-02 — One complete SIM run.** Run `scripts/sim/run-natural-complete.ps1` once with seed 1,
  from committed code, with dashboard 17880. Retain stage/relog reports, the full journey trace,
  every recorded outcome and any original failure. Verify the final staff/coin/stigma ledger,
  Q2217, campaigns, both Haramel clears, one character and monotonic clock; audit schema cleanup.
  Depends: AS-01. Stop at the relog-verified fortress endpoint; do not launch LIVE.
  Attempt (2026-10-03): `run/natural-complete/as02-full-create-s1-a1`, code `cc4692ee4`,
  stopped before creation because PowerShell's null environment assignment produced empty strings
  for optional checkpoint paths. Preserve its original log; remove absent variables through the
  environment provider and restore their original absence before a fresh attempt.
  Attempt (2026-10-03): `run/natural-complete/as02-full-create-s1-a2`, code `eef2ec3f7`,
  created one Priest and passed Ishalgen/Ascension and l1–l4, then reached l5's complete decision
  with 96 completions. Its deferred-journal assertion rejected Q24014/Q24015's automatic level-20
  unlock (LOCKED/0 to START/0, zero completion count), not an objective action. Java's two handlers
  register level-change hooks and call `defaultOnLevelChangedEvent(player, 24010)`; the helper
  starts eligible campaigns when the minimum level is met. Permit only that verified transition
  in the continuous guard; preserve the trace/failure package and three recorded deaths.
  Guard correction evidence (2026-10-03): the trace places both START/0 updates immediately after
  the level-20 `SM_STATS_INFO` during Q2233; no Q24014/Q24015 `CM_DIALOG_SELECT` was sent in l5
  (`deferred-guard-audit.json`). Eight regression cases reject missing prerequisites, under-level
  activation, changed flags, reward status, completion credit and Q24016. Focused tests: 158 passed;
  all seven pre-commit checks passed (`run/as02-unlock-checks`). Both failed attempts' owned schemas
  were dropped by the fixture, as verified through `INFORMATION_SCHEMA`.
  Attempt (2026-10-03): `run/natural-complete/as02-full-create-s1-a3`, code `5ae693c23`,
  passed l5 and entered l6, then exhausted navigation's 1,000 segments on Q2242's Gemyu hand-in.
  Java and C# ship the same stationary Gemyu spawn (2359.64, 916.004, 326). The trace records
  80 route searches and repeated returns to (2585.6, 1118.2357, 288.46027), not a missing NPC or
  an escort/timer loss (`run/as02-gemyu-audit.json`). Detect repeated route starts after actual
  movement and hand back to the existing observed-blocker recovery. Keep all segment, replan,
  guard-clear and cumulative revival limits; preserve the original failed trace and package.
  Route correction evidence (2026-10-03): cycle detection returns repeated checked walks to the
  existing blocker recovery. Regression cases preserve short forward routes and moving targets;
  focused navigation/Altgard/Haramel tests: 178 passed. All seven pre-commit checks passed
  (`run/as02-route-checks`); Fast: 82 passed / five explicit switch skips, all eleven scenarios
  passed (`run/as02-route-fast`). The failed attempt's owned schema was dropped.
  Attempt (2026-10-03): `run/natural-complete/as02-full-create-s1-a4`, code `7527438c8`,
  passed Ishalgen/Ascension and l1–l3, then stopped in l4 at Lamir's incense refill. An observed
  defensive Infernus kill during the approach moved Q2223 from START/1 to REWARD/1, but the
  stale planned refill still sent QUEST_SELECT to Lamir. Java accepts that page only at START;
  recheck the required journal state after approaching, then return to the ordinary decision
  if it advanced. Preserve the original dialog-echo failure and trace; its owned schema was dropped.
  Refill correction evidence (2026-10-03): `stale-refill-audit.json` proves an observed Infernus
  kill and REWARD/1 update before Lamir's obsolete QUEST_SELECT. The optional journal guard runs
  after every approach/reapproach and applies to summoned-monster refills. Focused tests:
  178 passed; seven pre-commit checks passed (`run/as02-refill-checks`, draft check rerun alone
  after an extractor build lock, original check log retained). Fast: 82 passed / five explicit
  switch skips, all eleven scenarios passed (`run/as02-refill-fast`). No server handler changed.
  Attempt (2026-10-03): `run/natural-complete/as02-full-create-s1-a5`, code `36f1c88d8`,
  passed Ishalgen/Ascension and l1–l9 (135 completions), including corrected Q2217, Gemyu and
  the escort. It stopped in l10 at the unchanged one-hour game-time progress guard while
  pursuing Q2282's second lower warlock. Q2281's packed 3/5/5 counters were already satisfied;
  twelve cumulative deaths remain recorded. The earlier BC-07 capture proves two ordinary
  210538 kills at (2400.88, 2171.88, 270.328), including its shipped 300-second respawn.
  Restrict this hunt to that proved lower spawn and reach spell range by the existing Heart
  flight/descent and checked defended road. Retain all budgets and the original stalled trace;
  the owned full-run schema was dropped (`black-claw-stall-audit.json`).
  Warlock correction evidence (2026-10-03): `run/as02-warlock-smoke-a2` restores the verified
  `altgard-l9` and plays Q2273 plus Q2282 without setup. Three real warlock kills and both
  normal hand-ins pass (`warlock-source-audit.json`); both diagnostic schemas were dropped.
  The earlier Q2282-only draft stopped before the hunt on the saved Return cooldown and is
  retained. Focused tests: 178 passed; seven pre-commit checks passed (`run/as02-warlock-checks`).
  Fast: 82 passed / five explicit switch skips, all eleven scenarios passed (`run/as02-warlock-fast`).
  The joined process gets a 90-minute wall watchdog for its fourteen stages; ordinary individual
  runs keep 45 minutes, and every in-game stall, navigation and cumulative revival limit is unchanged.
  The scoped smoke records one bind death; its normal recovery and final reward both pass.
  All seven checks pass again after the aggregate-watchdog adjustment (`run/as02-complete-timeout-checks`).
  Final evidence (2026-10-04): `run/natural-complete/as02-full-create-s1-a6` passes from committed
  `1d5dad7a04518c472a6d9f3095f1f9364ae7620d`, seed 1. One newly created Priest (133276) becomes a
  Cleric and completes Ishalgen, Ascension, l1–l11, CG and l12 in the same SIM world/schema/clock;
  all fourteen stages and thirteen Altgard endpoint relogs pass, with no snapshot restores.
  Final fortress endpoint: level 25, alive at full HP/MP, 157 completed quests, 666,444 Kinah,
  19 Iron Coins and 7 Bronze Coins. Q2217 completes exactly once in l3 and remains complete;
  Q24010–Q24016, Q2900 and all eleven scoped Haramel quests complete exactly once. Q24114 stays
  intentionally excluded; level-25 Q2945 is automatically START/0 outside this scope.
  Q2293 completes once: 18 incoming Iron becomes 23, then the three approved better-tier chain
  pieces cost 1 + 1 + 2 and are equipped, leaving 19. Zero extra repeats; no weapon/shield purchase.
  The earned staff 101501357/object 155674 is acquired in l10, equipped by l11 and retained through
  CG/Haramel. Both distinct Haramel copies prove fresh spawns, boss movie and class chest; the
  second includes the post-boss quests. Tutorial stone/skill 11504 is removed, the sealed reward
  bundle remains unopened, and no permanent stigma is installed.
  Eleven cumulative deaths are recorded (Ishalgen/bridge 1, l4 3, l5 2, l10 5); ordinary recovery
  succeeds within the original twenty-revive limit. Both escorts succeed on attempt one; Q2288
  and Q2230 finish on their first timed attempt, Q2263 on its second, and all three ring carriers
  are looted. `recorded-outcomes.json` retains the timed, escort and carrier decisions, including waits.
  The run advances 70,210,001 game ms
  (19h 30m 10.001s) in 55.8720 real minutes. `audit.json` verifies create-once/build identity,
  ordered stages, retained journals/staff, coin/stigma ledger and both clears against the full trace;
  `schema-cleanup.json` confirms no owned AS-02 schemas remain. The audit's draft expectation that
  the new staff was already equipped at l10 is retained with its correction to the observed l11
  equip timing. All earlier failed runs and their outcomes remain intact. Seven final pre-commit
  checks pass (`run/as02-final-checks`). No LIVE run, snapshot capture or push.

The complete SIM journey starts at character creation and plays Ishalgen/Ascension, then **l1 through
l11, cg and l12 in order**, retaining one character, database, clock, journals and aggregate outcomes
between legs. The operator's fresh-character scope supersedes the original level-10 `altgard` snapshot
start. AS-02 proves hub flights, binds, timed quests, escorts, city errands, pillar flights, coin preparation,
Bregirun, Space of Destiny and both Haramel visits without administrative progress. Its full trace and
first complete passing run are retained alongside the original failures; the existing gameplay limits
remain cumulative. Historical endpoint snapshots are unchanged. Do not run builds while a journey
holds its DLLs.

Then run the area **once on an owned isolated LIVE stack**, starting with a copy of the retained
NA-27 level-10 Altgard LIVE endpoint (`run/na27-live/na27-live-a3/altgard-live-dump.sql.gz`), not importing
the SIM character. Verify source ownership/identity and keep the original dump and world untouched.
The existing shared SIM/LIVE session must support every new map, stigma action and reset before launch;
keep the read-only monitor running and announce its URL. No operator `aion` stack or GM progression;
gear purchases are limited to the approved coin manifest, and skill-book purchases remain excluded.
Stop at the same relog-verified fortress endpoint and retain its LIVE evidence
and snapshot. These acceptance items need their own concrete run plan after the SIM implementation is
committed; LIVE remains a later task requiring its own authorization.

Gathering AL-3, Q2147, coin loops beyond CG's selected limit, unapproved D32 quests, a capital quest sweep,
permanent stigma selection, Morheim and Abyss entry remain outside this completion scope. The old
Q2293 exclusion below Stop 12 described the original leveling list; the new request adds only the
coin preparation above. Record a shared Java/retail defect for a separate decision before correction.

## Revised journey consolidation: RC working checklist

Prepared 2026-10-04 after the operator asked whether work needed another document.
Use this existing document and the [PC-08 schedule](natural-ascension-altgard.md#scheduled-additions-to-individual-legs-pc-08-2026-10-04)
as the working plan. The early Ascension order, first ten capital quests and nine later
quest completions already have approved route decisions. This checklist organizes that
work; it does not authorize the subsequent Abyss, class, crafting or instance contracts.

**Start and finish.** Historical AS-02 reached Cleric 25 with 157 completed quests,
Q2945 START/0, the earned staff 101501357 equipped, 19 Iron Coins, seven Bronze Coins
and eleven recorded deaths. It predates the revised early Ascension/capital order.
EA-02 proves the revised Ishalgen/bridge; PC-07 proves the contained first capital pass.
Neither proves the combined journey through Altgard and Haramel. Retain all three as
historical evidence. Finish RC with one fresh-create SIM of the revised route, an ordinary
fortress endpoint relog, an immutable committed-code snapshot under a distinct name,
and a measured readiness report for the next leg. Do not assume the revised final level.

**Scope.** Preserve the original 41 Ishalgen quests, Q2008/Q2009, Q2904, l1-l11, approved
three-piece coin purchase and both Haramel visits. Ascend on the first observed level 9,
complete the ten-quest capital pass, then finish retained Ishalgen. Add exactly the nine
PC-08 completions: Q2917, Q2918, Q2919, Q2916, Q2954 once, Q2959, Q2984, Q2920 and Q2938.
Q2919's two Ampha Tails are collected after acceptance on its small Ishalgen return;
its Malodor/Slime sources remain Leg 5. No repeatable grind or broad capital sweep.
Keep Q24114 intentionally excluded and retain all other documented exclusions.

Use **RC** in place of **NA** in the [loop protocol](natural-ascension-altgard.md#how-to-work-this-list-loop-protocol).
At every iteration read CLAUDE.md, the decisions, the affected leg's historical evidence
and PC-08 assignment, this checklist and the latest progress; run git status and check
running SIM processes before building. Pick the first unchecked unblocked item with
completed dependencies. Read its Java handlers and shipped data before implementation;
apply retail corrections only under an approved decision. One item per commit on main,
only its files staged, never push. Verify once, retain failures and dated evidence, run
the seven established pre-commit checks, and run-fast before gameplay-change commits.
Never run builds/checks while a journey holds DLLs. Record a required operator decision
under "Blocked / questions for the operator" and move to an unblocked item.

For a contained proof, restore through sim-snapshot.ps1 -Action Restore, use the
environment set by Invoke-NaturalJourney and the appropriate individual leg, then drop
the owned schema. Historical snapshots missing a carried quest/item cannot prove its
downstream work: obtain it through ordinary gameplay and preserve a distinct committed
prefix checkpoint. Controlled probes use a free account, clear aggressive monsters at
scripted contacts and call BeginWorldReload before setup teleports. Natural subjects
receive no administrative quest state, level, skills, gear or movement. Keep the bot
monitor enabled and announce its URL when a journey starts.

- [x] **RC-00 - Prepare the working checklist.** Record the bounded scope, ordered
  segment dependencies, inherited restrictions, historical evidence and stopping point.
  - 2026-10-04: prepared from PC-08, EA-02, PC-07 and AS-02 evidence. Documentation
    preparation only; no revised gameplay completion, new snapshot or group run claimed.
    Java Q2042 and the shipped portal requirements confirm the full Abyss chain. Source
    and whitespace review plus all seven pre-commit checks pass on base b66d83242
    (`run/rc00-plan-checks/`). RC-01 is the first implementation item.
- [x] **RC-01 - Carry later capital state across natural segments.** Depends RC-00.
  Freeze the nine additions, protected work/reward items and completion inventory in
  the revised continuation contract. Support unfinished pickups, actual level gates,
  per-leg/city-return decisions, relog and immutable prefix checkpoints while preserving
  historical contained scopes. Do not synthesize accepted quests/items on restore.
  - 2026-10-04: the opt-in SIM continuation freezes nine quests, their level/prerequisite
    gates, twenty segment assignments and protected items. Ordinary endpoint relogs write
    verified carried-state receipts; committed prefix captures hash them and restore refuses
    missing, changed or wrong-segment receipts before touching MySQL. Historical scopes retain
    their selectors. Forty-six focused tests, the snapshot contract (including Windows child
    environment isolation), all seven pre-commit checks and Fast (89 passed/five guarded skips,
    all eleven scenarios) pass on base 51bb43048 (`run/rc01-carry/`,
    `run/rc01-capital-fast/rc01-capital-carry/`). No later quest executor runs yet.
- [x] **RC-02 - Start Arekedil's Heritage in Leg 1.** Depends RC-01. Advance Q2917
  through Arekedil and Chauminerk to START/1 and preserve the supplied item for the city.
  - 2026-10-04: the opt-in Leg 1 pickup uses
    Java's Arekedil acceptance and Chauminerk SETPRO1, with no reward action. Free probe
    account 245 confirms START/1 and the same supplied item through ordinary logout/relog;
    resumed pickup sends no duplicate dialogs (`run/rc02-heritage/probe-a4.log`). The first
    Fast attempt and probe drafts remain: account registration and the probe's incomplete
    logout/entry sequence were corrected. Thirty-seven focused continuation/persistence
    tests, seven pre-commit checks and final Fast (90 passed/five guarded skips/all eleven
    scenarios, `run/rc02-fast/rc02-heritage-a2/`) pass.
    First committed capture `altgard-rc-l1` completed naturally at level 13 with zero deaths,
    but its restore correctly refused the descriptive contract name in the segment field
    before MySQL. That immutable failed snapshot remains intact.
    Canonical receipt repair passes 37 focused tests, snapshot contract and seven checks
    (`run/rc02-heritage/receipt-*`); this repair changes artifact metadata only, leaving the
    already Fast-verified gameplay actions unchanged.
    Distinct `altgard-rc-l1-a2` from committed 75fced712 passes capture, public Restore,
    ordinary endpoint relog, SQL/item identity and hash audits: Cleric 13 alive, 56 completed
    quests, Q2917 START/1 with supplied item object 133219, zero deaths. Historical dumps
    are unchanged, owned schemas are dropped and seven final proof checks pass
    (`run/rc02-heritage/audit.json`, `proof-checks/`). This is the retained Leg 1 prefix.
- [x] **RC-03 - Prepare Book of Oblivion before its field collections.** Depends
  RC-01. At observed level >=13, perform Cavalorn/Gantrug/Araison/book/Cavalorn to
  Q2919 START/4 and naturally collect two Ampha Tails. Use the pre-Q2904 return when
  eligible, otherwise the scheduled Leg 5 preparation; retain both items across relog.
  - 2026-10-04: implementation follows Java Q2919 and QuestItemNpcAI. Free probe
    account 246 defers below level 13, then plays all five preparation contacts,
    including the actual three-second library-book use, before either native tail drop.
    Two normal-HP Ampha kills obtain both tails; paid Doman/Aldelle teleports cost
    141 + 141 Kinah. The source lies between hubs and the flight planner chooses walking;
    actual hub crossings use the existing flight service. Ordinary relog retains
    Q2919 START/4, zero completions and the same two-tail item object, with no deaths.
    Probe drafts remain: the immediate-dialog wait, the already-consumed page wait and
    a visible old corpse were corrected; both interrupted owned schemas were dropped.
    Thirty-seven focused continuation/persistence tests and all seven checks pass
    (`run/rc03-book/`, base 5ff042763). The trace/item/fare audit and final Fast pass
    (91 passed/five guarded skips/all eleven scenarios, `run/rc03-fast/rc03-book/`).
    Natural pre-dispatch placement
    will be proved by RC-11; the contained Leg 5 fallback proof belongs to RC-04.
- [x] **RC-04 - Add Leg 5's capital preparation.** Depends RC-02 and RC-03. Finish
  Q2917 at Lanse/Annemari, use the actual jewelry box to start Q2918, and finish at
  Arekedil on the ordinary return to Altgard. Prepare Q2916 through Annju at >=15.
  Complete Q2954 once if >=19; otherwise leave it for the scheduled Leg 7 visit.
  - 2026-10-04 implementation: Java Q2917/Q2918/Q2916/Q2954 and item-dialog dispatch
    read; Annju uses the already approved D28 registration. Free probe account 247
    completes heritage through Lanse/Annemari, accepts Q2918 using the actual awarded
    box, prepares robe START/3, completes juice exactly once and consumes the box at
    Arekedil. Ordinary relog and resumed no-dialog checks pass (`probe-a1.log`).
    Committed a413b0ce5 naturally carries the original Q2917 item through distinct
    `altgard-rc-l2`, `altgard-rc-l3` and `altgard-rc-l4` checkpoints, at levels 16/16/19
    with zero deaths; Q2217 completes once in Leg 3. Each prefix has its verified
    continuation receipt and its owned capture schema is dropped. No historical dump
    is replaced. Forty-one focused tests and seven checks pass (`run/rc04-capital/`).
    Both initial Fast attempts retain a controlled East/Sumarhon probe timeout. The
    diagnostic replay confirms 6 MP and STR_SKILL_NOT_ENOUGH_MP; that protocol probe
    now labels its MP refill, and its setup teleports drop the stale client view. No
    natural resource or server cast behavior changes. The standalone original probe
    passes; the wrong-project focused-test invocation is retained and the corrected
    GameServer invocation passes all 41 tests. Seven checks pass again, and final Fast
    passes 92 tests/five guarded skips/all eleven scenarios (`rc04-capital-a3`). The
    committed Leg 5 route proof then exposed the two natural-run issues below.
    The first committed natural attempt (`rc04-l5-natural-a1`, 7848d43fd) preserves
    Deyla's walking-NPC refusal: the announced next waypoint was still about 12 m
    ahead of her. The RC dialog retry now intercepts her latest observed position.
    The next smoke completed gameplay but retained the deferred-campaign assertion:
    Java unlocks Q24014/Q24015 at level 20. RC contained runs now use the existing
    narrow continuous-run guard (START/0, zero completion credit, Q24010 required).
    Corrected `rc04-l5-smoke-a3` passes gameplay and ordinary relog: Cleric 20, 99
    completed quests, one recovered death, 7,544,000 simulated milliseconds. The three
    city quests complete once, robe is START/3, book is START/4, and the same two-tail
    and three-stamen item objects survive relog. Both hub flights, actual box use and
    four normal cube purchases are retained in the trace. Source audit, 41 focused
    tests and seven checks pass again. Final Fast passes 92 tests/five guarded skips/
    all eleven scenarios (`rc04-capital-a4`).
    Final committed d0f6b9e60 capture `altgard-rc-l5-preparation-a4` passes its endpoint
    relog and public Restore: Cleric 20, 99 completed quests, 214,590 Kinah, one recovered
    death. Dump SHA256 f0314ddb9b00...97a4fa9ca and continuation receipt hashes verify.
    SQL preserves every quest, equipment slot and carried item object; one owned
    Awakening scroll is used after relog (55 -> 54), matched to its trace/CM_USE_ITEM.
    The initially overbroad unchanged-inventory audit is retained, then corrected to
    account for that exact ordinary consumption. Both hub routes, the proper awarded
    box and the book fallback are proved. Three Malodor Stamens were acquired during
    the existing native fights after START/4; RC-05 still needs Slime Sap. Historical
    dump hashes are unchanged and the owned-schema count is zero. Final evidence:
    `run/rc04-capital/audit.json`, `rc04-restore-a1/`, `proof-checks/`.
- [x] **RC-05 - Collect the Leg 5 book materials.** Depends RC-04. After Q2919
  START/4, obtain three Malodor Stamens at Kaibech and one Slime Sap at Idun's Lake
  through ordinary combat/loot. Protect all collection items for the capital finish.
  - 2026-10-04: Java Q2919 and its collecting_step=4 drops read. The RC Leg 5
    endpoint fills only missing material counts through the existing normal combat,
    approach, loot and recovery path. The contained `rc05-smoke-a1` restores
    `altgard-rc-l5-preparation-a4` publicly, kills native Rainbow Slime 210435 and
    takes its actual Slime Sap loot. Ordinary endpoint relog preserves Q2919 START/4,
    zero completion credit, three stamens, two tails and one sap; original tail object
    134026 and stamen object 134343 remain unchanged. Cleric 20, 99 completed quests,
    214,590 Kinah, +4,922 XP, zero deaths in this added segment, 145,114 simulated
    milliseconds. All quests/equipment and historical dump hashes are unchanged;
    the owned schema is dropped. Trace/SQL/receipt audit passes on base 3a3eacd6b
    (`run/rc05-materials/audit.json`). The existing RC-04 death remains recorded.
    Seven pre-commit checks pass; Fast passes 92 tests/five guarded skips/all eleven
    scenarios (`run/rc05-fast/rc05-materials/`). No server or quest data changes.
- [x] **RC-06 - Continue the robe quest at Trader's Berth.** Depends RC-04. During
  Leg 6, advance Q2916 at Neparinerk 798033 to START/4 and retain it across relog.
  - 2026-10-04: Java Q2916's Neparinerk page 2375/SETPRO4 is performed when the
    ordinary route first reaches the Berth. The revised endpoint requires START/4.
    Committed RC-05 `b92ad512c` prefix `altgard-rc-l5-materials` retains all six
    book materials, with verified dump/receipt hashes and matching observed assembly
    commit. The capture began before the rebuild's exit was confirmed; successful
    build and actual assembly identity were then verified (`capture-audit.json`).
    Public restore and contained `rc06-smoke-a2` play the existing full Leg 6:
    Cleric 21, 109 completed quests, 244,919 Kinah, 4,192,061 simulated milliseconds, one recovered
    death; all book material objects remain unchanged and ordinary endpoint relog
    preserves robe START/4 and book START/4 with zero completion credit.
    First `rc06-smoke-a1` retains Q2252's reward-page timeout: the native Drakie
    variant sets REWARD/2 and Java selects reward group 1/page 6, while the old
    contract expected Spirit group 0/page 5. The bot now follows the observed
    alternate branch; corrected proof observes page 6 and pays Q2252 exactly once.
    No server/quest data or random-spawn behavior changes. Forty-two focused tests,
    seven pre-commit checks and trace/SQL/historical-hash audit pass on base b92ad512c;
    both owned schemas are dropped (`run/rc06-robe/audit.json`).
    Final Fast passes 92 tests/five guarded skips/all eleven scenarios
    (`run/rc06-fast/rc06-robe/`).
- [x] **RC-07 - Finish the scheduled Leg 7 capital visit.** Depends RC-05 and RC-06.
  Complete Q2919 through Neusa/book/Cavalorn, do Q2959 and Q2984 at >=20, retain both
  dye rewards unapplied, accept Q2938 at Oubliette, and do the single Q2954 completion
  here if it was previously below its gate. Resume the original Leg 7 endpoint.
  - 2026-10-04: Java Q2919/Q2938, ReportTo and the shipped Q2959/Q2984 data read.
    Committed d8b2f1d13 `altgard-rc-l6` has matching assembly/dump/receipt hashes,
    robe START/4 and all book material objects. Public restore and contained
    `rc07-smoke-a2` prove the original full Leg 7 with the scheduled city batch:
    Cleric 22, 125 completed quests, 285,039 Kinah, zero segment deaths, 2,670,221
    simulated milliseconds. Neusa's actual check consumes the six materials, the
    three-second book use supplies item 182207013/object 134259, and Cavalorn consumes
    it on the ordinary finish. Q2919/Q2959/Q2984 complete exactly once; Q2954 remains
    completed once. Both eight-item dye/bleach rewards stay unapplied and survive
    ordinary fortress endpoint relog; Q2938 START/0 and Q2916 START/4 are carried.
    First `rc07-smoke-a1` retains Lusena's missing-anchor failure after the successful
    book and family-letter work. Java WalkManager and her shipped patrol were read;
    the RC capital approach now searches the finite patrol hints in reverse when she
    is absent at her static anchor, identifying her only through client observations.
    Corrected proof completes the contact. No server/quest data or administrative
    natural setup changes. Forty focused tests, seven pre-commit checks and the
    trace/SQL/historical-hash audit pass on base d8b2f1d13; both owned schemas are
    dropped (`run/rc07-city/audit.json`).
    Final Fast passes 92 tests/five guarded skips/all eleven scenarios
    (`run/rc07-fast/rc07-city/`).
- [x] **RC-08 - Add Leg 9's city answer and clothing collection.** Depends RC-07.
  With both Leg 8 prerequisites Q2268/Q2269 complete, finish Q2920 during Q2258's city
  return using the actual answer sequence and observed reward group. Continue Q2916
  through Banatisai, the clothing distance trigger and ordinary object loot.
  - 2026-10-05: Java Q2920/Q2916, DialogService, QuestItemNpcAI and the 4.8 client
    answer topology read. Both revised committed prefixes pass hashes, matching assembly
    SHA and ordinary relog: `altgard-rc-l7` and `altgard-rc-l8` on 6aaccc18a; Leg 8
    carries all city state, completes Q2268/Q2269 and records one recovered death.
    Public restore `rc08-smoke-a1` passes full natural Leg 9 on its first attempt:
    Cleric 22, 142 completed quests, 401,257 Kinah, zero segment deaths and 3,597,038
    simulated milliseconds. Deyla's introduction follows pages 4762/4763/4764/4/1003;
    the actual answer follows 1011/1352/1353/5 and pays group 0's 2,380 Kinah once.
    Banatisai advances START/4 -> 5; the observed five-metre clothing event advances
    to 6, then ordinary positive-duration use/loot supplies 182207007/object 134375
    on attempt one. Clothing, library START/0 and both unapplied dye rewards survive
    Heart endpoint relog. Forty-three focused tests, seven pre-commit checks and
    trace/SQL/historical-hash audit pass; all owned schemas are dropped
    (`run/rc08-elementary/audit.json`), base 6aaccc18a. Final Fast passes
    92 tests/five guarded skips/all eleven scenarios (`run/rc08-fast/rc08-elementary/`).
- [x] **RC-09 - Complete the Leg 10/11 capital hand-ins.** Depends RC-08. Finish
  Q2916 at Deyla on the Q2283 visit. Require Q24016 COMPLETE and Suthran's permission
  page before advancing Q2938; retain the permission and finish at Oubliette during
  the existing Q2900 capital trip. Preserve the stigma tutorial's ordinary completion,
  removal of its temporary stone/skill and the sealed permanent reward bundle.
  - 2026-10-05: Java Q2916/Q2938/Q2900, QuestService completion/reset and the
    shipped 4.8 dialog topology read. Committed 37e199733 `altgard-rc-l9` passes
    dump/receipt/assembly hashes with the actual clothing carried. Public restore
    `rc09-smoke-a2` plays contained Leg 10, ordinary relog, then Leg 11 from that
    same owned state with a forward clock. Leg 10: Cleric 24, 152 completions,
    535,404 Kinah, three recovered deaths, 12,388,061 simulated milliseconds.
    Deyla consumes the clothing and awards her hood once. Q24016 COMPLETE precedes
    Suthran's actual 1011/1012 permission pages; item 182207026/object 137752 is
    retained at REWARD/0 across relog. The normal obelisk approach restores the
    original fortress endpoint after Suthran. Leg 11: 154 completions, 558,060 Kinah,
    zero segment deaths, 680,901 simulated milliseconds. Oubliette's 10002/5 pages
    and SELECTED_QUEST_NOREWARD consume that same permission once during Q2900's
    reward-city visit. Both quests stay COMPLETE/0 across the final relog; the
    temporary stone/11504 are absent and the one permanent bundle remains sealed.
    First `rc09-smoke-a1` retains the successful hand-ins and old Destiny journal
    assertion (153 expected, 154 observed); the RC endpoint now checks the exact
    incoming journal plus the two scheduled finishes. Its Suthran endpoint was
    outside the original 60-metre radius and is also corrected. The audit draft's
    COMPLETE/1 expectation is corrected from Java's normal finish reset to zero,
    retaining the draft and failure. Sixty focused tests, seven pre-commit checks
    and trace/SQL/historical-hash audit pass on base 37e199733; both owned proof
    schemas are dropped (`run/rc09-finishes/audit.json`). Final Fast passes
    92 tests/five guarded skips/all eleven scenarios (`run/rc09-fast/rc09-finishes/`).
- [x] **RC-10 - Join the revised stages and audit their evidence.** Depends RC-09.
  Integrate early Ascension, first capital, retained Ishalgen, the nine additions and
  existing Altgard/coin/Haramel stages in the continuous harness. Verify the carried
  prerequisites and distinct segment receipts; include all city maps, protected items,
  exact one-time completions and cumulative outcomes in its audit. Complete the focused
  continuation/checkpoint checks and required pre-commit checks before the full run.
  - 2026-10-05: the continuous opt-in retains separate bridge/l1-l12/cg carried-state
    receipts, the ten first-capital payments and nine later-capital payments, and each
    stage's observed level/XP/Kinah. Its audit requires the exact 176-quest combined
    scope once, all eight visited maps, protected items, retained staff/three chain
    purchases and both Haramel visits; deaths and timed/escort/carrier outcomes remain
    recorded outcomes. Consumable supply provenance, use attempts and inventory removals
    are separate fields because mixed stacks cannot identify a supplied unit's use.
    `run-natural-complete.ps1 -LaterCapital -SnapshotName <unused-name>` runs a fresh
    owned schema without restores and captures that same committed-code endpoint before
    dropping it. The continuous receipt is hashed and required on restore. Java's XP
    table was read; no server behavior/content changes. Sixty focused tests, snapshot
    contract, audit syntax/scope review and seven pre-commit checks pass on 597797164
    (`run/rc10-join/`); Fast passes 92 tests/five guarded skips/all eleven scenarios
    (`run/rc10-fast/rc10-join/`). The revised full gameplay proof belongs to RC-11.
- [ ] **RC-11 - Prove one revised continuous fresh-create SIM.** Depends RC-10.
  Run seed 1 from character creation through both Haramel visits in one owned schema,
  character and game clock, without restores. Record per-stage XP/level, actual regular
  skills, gear, gross rewards/fares/purchases, consumable provenance and usage, deaths,
  recovery, timed attempts, escorts and carrier windows. Record outcomes rather than
  requiring zero deaths. Keep failures; fix only an evidenced blocker and rerun if needed.
  - 2026-10-05, first retained attempt: `rc11-full-create-s1-a1` on 87a1e6d49
    completed all 41 Ishalgen quests and the ten first-capital quests, then failed
    Q2919's bounded tail collection at level 13. Thirty reported collection encounters
    reused corpse 134283 after its one native tail had been looted; the owned schema
    was dropped (`run/snapshots/_capture/rc11-full-create-s1-a1/failure-audit.json`).
    Java Q2919, its drops and QuestService's collecting-step/needed-count rules were
    read. The bot now marks each successful collection kill unavailable so a partial
    stack seeks a new live source. Sixty focused tests and seven pre-commit checks
    pass (`run/rc11-ampha-repair/`); Fast passes 92 tests/five guarded skips/all
    eleven scenarios (`run/rc11-fast/rc11-ampha-repair/`). Full revised gameplay
    proof remains pending; this repair is committed before its fresh capture.
  - 2026-10-05, second retained attempt: `rc11-full-create-s1-a2` on f45b4b48e
    proves distinct native tail sources 55354/134283 and both carried tails, then
    completes the bridge and Legs 1-6. In Leg 7 Q2919 completes once and pays 38,700 XP,
    consuming the required 3/2/1 materials from incoming 3/2/2 stacks. Java legitimately
    leaves one surplus sap; the bot's zero-surplus assertion stopped the run. That
    assertion and the audit now verify exact required consumption and retain surplus.
    The failed trace/consumption audit and dropped-schema receipt remain intact.
    The capture was dispatched before the post-commit build's final acknowledgement;
    its successful build, matching loaded MVID and matching source/test-copy SHA256
    confirm f45b4b48e's actual repaired assembly (`runtime-identity.json`).
    Free probe account 248 uses normal acceptance/Neusa/book/reward packets, labelled
    3/2/2 setup stacks and ordinary relog to prove the one sap and one completion survive.
    That probe, sixty focused tests and seven pre-commit checks pass
    (`run/rc11-book-surplus/`); Fast passes 93 tests/five guarded skips/all eleven
    scenarios (`run/rc11-fast/rc11-book-surplus/`). Full revised gameplay proof
    remains pending; the verified assertion repair is committed before its fresh capture.
  - 2026-10-05, third retained attempt: `rc11-full-create-s1-a3` on 5e20ba6f4
    completes the bridge and Legs 1-6, then finishes the book, family and dye quests
    at level 22. Its return from the dye vendor to Oubliette has no collision-checked
    route; the failed trace and dropped-schema receipt remain intact (`failure-audit.json`).
    Java Q2938 and the family/dye templates/data permit the independent library pickup
    beside the final book contact before leaving for those errands. The city order now
    takes that pickup first. Free account 248's strengthened native probe walks the
    whole book/library/family/dye batch and back to Hoder, completes the juice fallback
    and preserves the exact book consumption, surplus and completions through relog
    (`run/rc11-library-route/probe-a5.log`). Earlier probe failures are retained:
    overly close ground endpoints could not reach Kvasir, and chasing Lusena's old
    position lost her; intercepting her announced waypoint uses checked ground movement.
    Sixty-six focused tests and seven pre-commit checks pass (`run/rc11-library-route/`).
    Fast passes 93 tests/five guarded skips/all eleven scenarios
    (`run/rc11-fast/rc11-library-route/`). Full revised proof remains pending;
    this verified order repair is committed before the next fresh capture.
  - 2026-10-05, fourth retained attempt: `rc11-full-create-s1-a4` on f76b3f044
    proves the earlier library pickup, book finish and family completion, then stops
    at Lusena (204138) for Q2984. All three retries chase the same retained move start,
    24.2 metres behind her waypoint; the failed trace/audit and dropped-schema receipt
    remain intact. Java's dialog/range hooks and the shipped patrol were read: 21 points,
    longest leg 36.16 metres, walk speed 1.5 m/s. Lusena now receives bounded paced
    requests while the bot intercepts her waypoint over checked ground, allowing her
    to reach talk range; other city NPCs retain their existing target choice.
    Native probes and production share the checked ground candidate search inside the
    actual NPC talk range. Four contained Leg 7 failures are retained: arriving before
    Lusena, the dye-to-Cavalorn return, the attempted detour and its direct-route retry.
    The final city sequence groups Q2278's Cavalorn contact with the book/library/family
    pickups, visits Balder, finishes the market hand-ins, then uses learned Return.
    `run/rc11-mima-interception/l7-smoke-a5/` passes from public Restore of altgard-rc-l6:
    character 133297, level 22, 125 completions, zero deaths, ordinary endpoint relog
    and schema dropped. Nine paced Lusena reapproaches are observed. The joined native
    consumption/surplus/relog probe, 66 focused tests and seven pre-commit checks pass
    (`run/rc11-mima-interception/`, `*-joined.log`). Full revised proof remains pending.
    Final Fast passes 93 tests/five guarded skips/all eleven scenarios
    (`run/rc11-fast/rc11-city-join-final/`). Commit and complete the rebuild before
    the next fresh capture; no revised full endpoint is claimed by this contained proof.
  - 2026-10-05, fifth retained attempt: `rc11-full-create-s1-a5` on 43cadfca6
    passes the joined city visit and reaches Leg 8 at level 22 with 138 completions.
    Q2270's collection has four insignia, but the bot requires exactly three and stops.
    Its failure audit, eight deaths (seven in Leg 4, one in Leg 5), trace and dropped-schema
    receipt remain intact. Java ItemCollecting and QuestService.collectItemCheck accept
    surplus and consume exactly the required count. The generic collection assertion now
    uses that minimum for every leg, extending Leg 10's existing behavior.
    Account 208's native Observatory probe labels one extra insignia after three native
    drops, proves three-of-four consumption, and retains the spare and one completion
    through ordinary relog. The public-restore Leg 8 smoke from altgard-rc-l7 passes
    at level 22 with 132 completions, one recorded death, endpoint relog and schema dropped.
    The native proof, 82 focused tests and seven pre-commit checks pass
    (`run/rc11-insignia-surplus/`). Fast passes 93 tests/five guarded skips/all eleven
    scenarios (`run/rc11-fast/rc11-insignia-surplus/`). The revised full proof remains
    pending; commit and complete the rebuild before the next continuous capture.
  - 2026-10-05, sixth retained attempt: `rc11-full-create-s1-a6` on 5b0953176
    completes Legs 1-9, including both corrected city visits, at level 23 with 152
    completions. In Leg 10 Q2281 reaches packed counter 20803 (3/5/5) at game time
    16:20:31.525, but the ongoing approach keeps seeking another physical kill. The
    ordinary one-hour progress watchdog eventually stops it; twelve deaths (seven
    in Leg 4, one in Leg 5, four in Leg 10), trace, failure audit and dropped-schema
    receipt are retained. Java MonsterHunt counts qualifying defensive kills and
    permits reporting once all counters are satisfied. The bot now checks the current
    quest counter during checked navigation and stops an unfinished approach when it
    completes, without inventing another kill or NPC arrival or relaxing the watchdog.
    The regression exercises an actual Q2281 client-counter update during navigation;
    111 focused tests pass. Public Restore of altgard-rc-l9 proves the contained Leg 10
    at level 24 with 152 completions, Q2281 completed once, three recorded deaths,
    ordinary endpoint relog and schema dropped (`run/rc11-hunt-completion/`). This
    smoke did not exercise the new early-stop diagnostic; the regression covers it.
    The audit also records newly added item identities from SM_INVENTORY_ADD_ITEM,
    matching Java's object/template fields; historical AS-02 supplies the evidenced
    first-use case for a DP jelly absent from its initial inventory packet.
    Seven pre-commit checks pass; Fast passes 93 tests/five guarded skips/all eleven
    scenarios (`run/rc11-fast/rc11-hunt-completion/`). The revised full proof remains
    pending; commit and finish the rebuild before capture.
  - 2026-10-05, seventh retained attempt: `rc11-full-create-s1-a7` on 6ab81fb97
    completes Legs 1-7, then stops in Leg 8 at level 22 with 138 completions when
    Smite 4016 is rejected for retained assassin 149588/210720. Its only NPC spawn
    packet is at 12:35:21.543; the same-map bind revive at 12:37:47.759 does not clear
    that view, and the cast at 12:40:42.493 has no fresh target observation. Six deaths
    (three in Leg 4, one in Leg 5, two in Leg 8), failed trace/audit and dropped-schema
    receipt are retained. Java TeleportService despawns and rebuilds the player's
    known list; PlayerController.notSee suppresses deletion packets while despawned,
    and FirstTargetProperty/Skill reject invalid targets. The bot now drops its view
    before every bind revive, extending the existing late-leg reset to the same-map
    revives in earlier legs. No server target validation is changed or ignored.
    Free account 249's native controlled death and ordinary CM_REVIVE prove the old
    assassin disappears, no kill is invented, and inventory, skills and completed
    counts survive the reset. Two probe assertion failures are retained: their broad
    before/after interval included legitimate recovery shield-scroll consumption.
    The passing probe inspects immediately before death and after reload, before
    post-revive supplies (`run/rc11-bind-view/probe-a3.log`). Eighty-nine focused
    tests and seven pre-commit checks pass. Public Restore of altgard-rc-l7 also proves
    the contained Leg 8 at level 22 with 132 completions, five recorded deaths, ordinary
    endpoint relog and schema dropped (`run/rc11-bind-view/l8-smoke/`). Fast passes
    94 tests/five guarded skips/all eleven scenarios (`run/rc11-fast/rc11-bind-view/`).
    Full revised proof remains pending; commit and finish the rebuild before capture.
  - 2026-10-05, eighth retained attempt: `rc11-full-create-s1-a8` on 97612916d
    completes Legs 1-5 and reaches Leg 6 at level 21 with 117 completions and two
    deaths, both in Leg 4. Q2284 remains START/1: no Germir follow attempt has begun.
    Eight pre-escort clear passes make 237 kill claims across 48 sources; looted
    corpses remaining in view are selected again, and the ordinary one-hour watchdog
    stops the clearing loop. The failed trace/audit and dropped-schema receipt remain
    intact. Java Q2284 permits the
    ordinary 1->2 follow start, then reach/loss hooks produce reward or restart;
    the trace proves real HP-zero/loot evidence before repeated corpse claims.
    Successful escort-clear kills now retire their source from navigation, just as
    other successful pulls do. Public Restore of altgard-rc-l5-materials proves the
    contained Leg 6 at level 21 with 109 completions, zero deaths, three distinct
    retired sources, one successful 47.138-second escort, endpoint relog and schema
    dropped (`run/rc11-escort-corpses/l6-smoke/`). Fifty-six focused tests and seven
    pre-commit checks pass. Fast passes 94 tests/five guarded skips/all eleven
    scenarios (`run/rc11-fast/rc11-escort-corpses/`). Full revised proof remains
    pending; commit and finish the rebuild before capture.
- [ ] **RC-12 - Preserve the endpoint and report NTC readiness.** Depends RC-11.
  Verify ordinary endpoint relog, save the committed-code endpoint under a distinct
  unused snapshot name, verify its hashes/identity and drop every owned schema. Report
  actual level/XP headroom, unresolved blockers, class/gear/regular-skill/stigma state,
  consumable ledger and costs against historical AS-02. Complete the final required
  checks. Stop when this item is done or no item remains unblocked.

**Inherited limits.** Use hub flight transporters. Never push, branch, use worktrees,
touch the operator's aion stack or supply natural progress with GM. Approved help
consumables retain their provenance; other gear/skill-book purchases remain excluded
except the existing three-piece coin manifest. Keep the equipped earned staff through
coin preparation/Haramel. A regular skill is not a stigma skill: a permanent stigma
needs its ordinary unlocked slot, owned stone and socketing, under a separate decision.
Capture only committed code and preserve every historical snapshot, including the
Leg 1 snapshot named altgard-l12 and Haramel's altgard-haramel-l12.

**Next contracts after RC.** Specify Abyss access separately as Q2945 -> Q2946 ->
Q2947 -> Q2042 (The Last Checkpoint, the Morheim timed flying-ring course), including
the Q2947 branch and ordinary transport. Then specify natural Warrior/Templar and
Mage/Sorcerer profiles, synchronized party play and NTC around levels 25-27. Existing
invitation/loot/shared-credit scenarios are foundations, not a completed natural group
controller. NTC pickup/reward eligibility, individual unshareable quests, gate/siege
mechanics, instance ownership/reentry and recovery need their own evidence. The group's
highest eligible nearby level affects XP/drop penalties; level 32 is not a universal
zero-reward cutoff. Use the observed RC endpoint to budget further XP before expansion.
The existing Cooking/Alchemy plan remains a separate bounded profession/supply goal.

## Blocked / questions for the operator

**ND-Q1 — Correct the Leg 11 reward endpoint (answered 2026-10-03: Option (a)).** The maintainer chose
Option A: amend the contract, normal reward action and endpoint to match shipped Java, retain the sealed
bundle and fixed rewards, require the inactive legacy item absent, and keep server behavior unchanged.
The original approved plan assumed that
Aud gives the listed Cleric legacy item 140000098. In both Java `ce54b7931` and this checkout, Q2900
omits `use_class_reward`; its schema/default is 0, and `QuestTemplate` enables class rewards only at
1 or 2. `QuestService.getRewardItems` therefore does not grant that list. The ordinary fixed-reward
claim is SELECTED_QUEST_NOREWARD, not SELECTED_QUEST_REWARD1. Probe `nd03-campaign-a4` confirms the
bundle, all fixed rewards, completion and actual normal-stigma installation; the legacy item stays absent.
This is a mismatch in the approved plan, with no C#/Java divergence to fix. The old item also has no
stigma metadata. [NCSoft's 4.8 notes](https://static.ncsoft.com/aion/store/PatchNotes/AION_Patch_Notes_061715.pdf),
printed pages 26–29, establish the new mission/slot rules and unusable older stigmas; they do not authorize
enabling Q2900's inactive legacy reward list.

- **(a), recommended:** amend the contract, normal reward action and endpoint to match shipped Java:
  retain the sealed bundle and fixed rewards, require no temporary stone/11504, and expect the inactive
  legacy item to be absent. Keep the server data and handler unchanged; permanent stigma selection still
  waits for a later leg. Then complete ND-03 and continue ND-04..ND-08.
- **(b):** keep the legacy-item endpoint requirement and stop this leg while a separate, narrowly scoped
  retail-evidence/decision task establishes whether a server correction is wanted. D26–D33 do not approve
  enabling this reward or inventing stigma metadata.

The choice resolves the reward blocker. Finish and verify ND-03 against the amended contract, commit it,
then continue ND-04..ND-08 in dependency order.

**Leg 7, answered 2026-10-02:** AE-Q1 **(a)**, AE-Q2 **(a)** and AE-Q3 **(a)**, all as recommended:
- Leg 7 does Stop 9 and Stop 11's fortress quests, worked from the fortress bind. The Observatory is Leg 8.
- Q2146 is taken and held for Lateni, and handed in on the Heart of Impetusium leg (Leg 9).
- Q2273, the [Group] quests and the missions Q24014–Q24016 make one later leg on the Black Claw ground (Leg 10), after
  the Observatory (Leg 8) and the Heart of Impetusium (Leg 9). Q2900 comes last.

The original questions:
- **AE-Q1 — How big is Leg 7?**
  - (a) The fortress and the East Gate: thirteen quests, two of them the held hand-ins, worked from the fortress bind.
    Q2273 and the [Group] quests wait for the Black Claw leg (AE-Q3).
  - (b) (a), plus the Observatory (Stop 10): a second working hub 800 m south, with its own obelisk (2,035 Kinah) and
    flight. That is seven more quests and 155,566 XP.

  **Recommendation: (a).** Every ground lies within 450 m of one obelisk. The Observatory makes a short leg of its own
  (Leg 8).
- **AE-Q2 — Q2146's hand-in at the Heart of Impetusium.** Eggther's message goes to Lateni, 922 m east of the pad.
  - (a) Take it, hold it, and hand it in on the Impetusium leg (as AG-Q2).
  - (b) Fly there during Leg 7 (52.8 s and 400 Kinah each way).

  **Recommendation: (a).** It is worth 7,500 XP, and Lateni's hub is a later leg.
- **AE-Q3 — The Black Claw ground.** Three sets of work share the EXPERT-thick ground southeast of the gate: Q2273's
  veteran MuMus, the [Group] quests' targets (Q2277 and Q2280–Q2283), and the targets of the missions Q24014–Q24016.
  - (a) One later leg for all of them, as one of the last Altgard legs, with the missions near the highest level (the
    maintainer's placement for Q24014–Q24016). The order for the rest: Leg 8 the Observatory, Leg 9 the Heart of
    Impetusium, Leg 10 the Black Claw ground with the missions, then Q2900.
  - (b) Q2273 and Q2277 in Leg 7. Their givers are in the fortress and at Taora, and Q2277 needs nothing first.

  **Recommendation: (a).** 47 EXPERT spawns stand within 35 m of the MuMus' spots. After Legs 8 and 9 the Cleric is
  about level 22, and the [Group] quests are then done in one sweep (AL-2: solo, at the target's level or above).


**Leg 6, answered 2026-10-01:** AG-Q1 **(a)**, AG-Q2 **(a)** and AG-Q3 **(a)**, all as recommended:
- Leg 6 does Stops 7 and 8, worked from the Trader's Berth bind; the East Gate goes to the fortress-side leg.
- Q24115 (Banatisai) and Q2262 (Mabrunerk) are taken, worked and held for their hubs.
- Outside 04:00–21:00 the bot does the other quests first, then waits for Q2284's follower.

Two more decisions from the maintainer the same day:
- **Q2900** "No Escaping Destiny" is done at the very end of the Altgard area.
- **Q24014–Q24016**, the level 20 missions with the Haramel and Bregirun instances, are done when
  convenient, as one of the last Altgard legs, near the highest level the Cleric reaches here. This refines
  AL-1 (b), which had put them after the zone.

The original questions:
- **AG-Q1 — How big is Leg 6?**
  - (a) Stops 7 and 8: Gerger Village and Trader's Berth, twelve quests (ten handed in, two held), worked from the Berth bind. Q2262
    is taken and held for the East Gate.
  - (b) Stops 7, 8 and 9: the East Gate quests too (Q2263 timed, Q2264, Q2265, Q2146). That means a second
    working hub at the fortress gate, 1,000 m away by flight, with a bind move.

  **Recommendation: (a).** One working hub with everything inside 400 m. The East Gate is at the fortress,
  beside Stops 10 and 11, where Leg 7 also hands in Q24233 and Q2262.
- **AG-Q2 — The held hand-ins.** Q24115 ends at Banatisai in the Heart of Impetusium (Stop 12), and Q2262 at
  Mabrunerk at the East Gate.
  - (a) Take them, finish the work, and hand them in when the route reaches those hubs (as AK-Q2).
  - (b) Fly there during this leg.

  **Recommendation: (a).** A held quest loses nothing, and each hub is a later leg's.
- **AG-Q3 — Q2284's day-only follower.** The second disguised Germir exists only 04:00–21:00 game time.
  - (a) Outside those hours, do the other quests first, then wait for 04:00 if nothing else is left (as
    Q2292, AK-Q3).
  - (b) Skip Q2284 if the hour is wrong.

  **Recommendation: (a).** It is the leg's biggest quest (50,130 XP), and the waiting machinery exists.

**Leg 5, answered 2026-10-01:** AK-Q4 **(a) and (b)**. The maintainer approved buying the expansions and asked
that extra gear the Cleric does not need be sold.
- The Cleric buys the four Pandaemonium cube expansions with its own kinah, at the start of Leg 5.
- An accessory still in the cube once the upgrades are worn is sold, unless the Cleric is not yet the level to wear it.
  NA-09 had kept every accessory, because the inventory rules compared only weapons and armor; the gear
  routine already wore the better accessories, but nothing sold the outgrown ones (the Spirit Ring, Munin's Belt).
- **Found while answering:** the gear routine had worn Q2292's level 16 Passion and Jealousy Rings as upgrades.
  Java's hand-in counts the cube, not what is worn, so the claim would have failed. Items an open quest still
  needs are now never worn and never sold.

The question:
- **AK-Q4 — The cube is full of things the Cleric keeps.** The fourth AK-08 smoke run reached level 20.
  It then had 1 free slot of 27 (no expansions) and nothing the rules sell. A loot of any normal item
  (Q2292's rings, drops) is refused when the cube is full. What fills it:
  - the approved help items in two tiers: the level 20 top-up added the Awakening, Running and Anti-Shock
    Scrolls, Lesser Life Potions and Lesser Mana Serum, while the older tiers still hold slots;
  - six unsellable event stacks;
  - five unworn accessories, which the Cleric rules keep (NA-09);
  - the protected supplies and the Karmic Staff.

  The options:
  - (a) **Buy cube expansions**, the maintainer's suggestion. Pandaemonium sells levels 1–4 (798011,
    798012, 798058, 798059) for 1,000, 12,000, 80,000 and 180,000 kinah: 273,000 in all, for 36 more
    slots. Ishalgen sells only level 1.
    - The Cleric has 437,720 kinah of its own, so no GM kinah is needed.
    - It is one errand at the start of Leg 5: walk to the fortress, teleport to Pandaemonium, buy, come
      back, and bind again at Basfelt.
    - Replaying from Leg 1 is not needed: Legs 1–4 never hit the limit.
  - (b) **Sell surplus accessories**: those no better than the ones worn (5 slots here). This changes
    NA-09's "keep every accessory".
  - (c) **Use up the older help-item tier first, and stop topping it up** once a newer tier is supplied.
  - (d) Several of these.

  **Recommendation: (a), with (b).** A 4.8 player at level 20 has bought the cheap expansions and sells
  rings they will never wear. (a) alone gives the room for Legs 6+; (b) keeps the cube from filling again.
  The expansion is spending kinah on something new, which the standing rule (never buy gear or skill
  books) does not cover, so it needs a yes.

**Leg 5, answered 2026-10-01:** AK-Q1 **(b)**, AK-Q2 **(a)** and AK-Q3 **(a)**, all as recommended:
- Leg 5 does Stops 5 and 6 together, thirteen quests, worked from the Basfelt bind.
- Q2242 (Gemyu) and Q24233 (Suthran) are finished but held. They are handed in when the route
  reaches Gerger Village and the fortress.
- The bot waits for the game's night for the Love Ring, doing other work meanwhile.

The original questions:
- **AK-Q1 — How big is Leg 5?**
  - (a) Stop 5 only: five quests, 63k XP, mostly at Kaibech, 650 m west.
  - (b) Stops 5 and 6 together: thirteen quests, 199k XP, all worked from the Basfelt bind.

  **Recommendation: (b).** Stop 5 alone is a short trip. Stop 6's quests opened with Q24112 in
  Leg 4, and Leg 4 already did three of them.
- **AK-Q2 — The two hand-ins at far hubs.** Q2242 ends at Gemyu in Gerger Village (622 m east,
  Stop 7). Q24233 ends at Suthran in the fortress (1,065 m north, Stop 11).
  - (a) Take both, finish the work, and hand them in when the route reaches those hubs: Gerger
    next, the fortress at Stop 11.
  - (b) Walk to both during this leg.
  - (c) (a) for Q24233, (b) for Q2242.

  **Recommendation: (a).** A held quest loses nothing, and Gerger is the next stop anyway. The
  fortress round trip costs about 2 km of walking through the Moslan ground for 16k XP.
- **AK-Q3 — Q2292's night-only Love Ring.**
  - (a) Wait for the game's night (22:00–05:00, about 35 real minutes after it starts), doing
    other Leg 5 work meanwhile.
  - (b) Skip Q2292.

  **Recommendation: (a).** It is a real 4.8 mechanic worth proving, the carriers are in data
  the maintainer just refined, and the wait costs nothing if it overlaps other work.

**Leg 4, answered 2026-09-30:** AB-Q1 **(a)**, AB-Q2 **(a)** and AB-Q3 **(a)**, all as
recommended:
- All eleven quests are done in Leg 4, the three hand-ins at other hubs included, and the leg
  ends at Basfelt.
- The timed quests and Infernus get three tries each.
- Komu is left alone until Q2289 var 7.

**Leg 4, answered 2026-10-01:** AB-Q6 **(a)**, narrowed. The maintainer asked for the narrow fix
(D33): Q1044 and Q2042 end the timer only for a player flying their own ring course (var 2–7, the
timer's span), so a death or world entry still fails that course as in Java. Applied in C# with
the SIM test `RingCourseQuestsEndOnlyTheirOwnTimer` (deviation 152) and reported upstream
(`docs/upstream-reports/q1044-q2042-ring-course-timer.patch`). What 4.8 retail did to a timed
quest's own timer on a death is not known and is not changed.

- **AB-Q6 — Q1044 and Q2042 end any quest timer** (found in AB-04). Their enter-world and death
  hooks call `questTimerEnd` for every player, with no check that the player is on those
  quests. The timer is one slot per player, so a revive, a relog, or a teleport that respawns
  the player ends Q2288's or Q2230's timer. This is shared with Java.

  For the bot, a death during a timed quest costs:
  - Q2230's tusks, at the next check;
  - Q2288's timer, which then never runs out, so the quest sits at its var with no timer.
    Worse, a new SETPRO1 would reset its kills to var 1.

  Options:
  - (a) approve a narrowly scoped C# correction, as D26–D31 were: Q1044 and Q2042 end the
    timer only while their own quest is START. The same fix goes into the upstream report.
  - (b) keep the shared behaviour. The bot then treats a death during a timed quest as the
    timer's end, and AB-03's policy learns to spot a timer that is gone while its quest is
    still START.

  **Recommendation: (a).** A timer that belongs to another quest should not end because a
  flight-training quest the player never took runs its hooks. That is what the 4.8 retail
  goal asks for. Either way, AB-08 makes the policy tell "timer gone" from "timer running" by
  the client's view, and never sends SETPRO1 to Q2288 past var 0.

**Answered 2026-09-30:** AB-Q4 **(a)** and AB-Q5 **(a)**.

**The standing bind policy (the operator, 2026-09-30):** bind at the obelisk of the quest hub
the bot is working out of. A return scroll or a death respawn then brings it back to the
work, not to a town it has left. It applies to every leg from Leg 4 on.

The original questions, from the follow-up sweep:
- **AB-Q4 — Pull Basfelt's own follow-ups into Leg 4?** Q2227 → Q2291 open at Gornak once Q2226
  is handed in there. Their crystals come from passive lake spirits beside the south
  mosbears, and Q2291 ends at Garuntat in Basfelt. The campaign Q24013 is already started.
  Its talks are Nokir and Shania in Basfelt, but its fights are four SEASONED black claws
  (L15–16) and two spawned Feral Sharpeyes (L17, SEASONED), 430–540 m south. Options:
  - (a) both: Q2227, Q2291 and the whole of Q24013 in Leg 4;
  - (b) Q2227 and Q2291 only; Q24013 stays at Stop 6;
  - (c) neither.

  **Recommendation: (a).** A player does the campaign as soon as it is in the journal. Every
  step but the fight is in Basfelt. The black claw ground is next to Sumarhon's, which Leg 4
  visits anyway. AL-2 already accepts SEASONED fights solo, with deaths recorded.
- **AB-Q5 — Bind at Basfelt?** The bind is still the fortress obelisk, about 1.1 km north. A
  death in the west or south then means a long walk back. Options:
  - (a) bind at the Basfelt obelisk on arrival. The bot has bound at an obelisk before, for
    the bridge.
  - (b) keep the fortress bind.

  **Recommendation: (a)**, as a player would. Rests and restocks can then use Basfelt's
  vendors too.

The original questions follow.
- **AB-Q1 — The hand-ins at other hubs, and where Leg 4 ends.** Q2231 ends at Kaibech (Stop 5,
  649 m west). Q2226 ends at Gornak and Q24112 at Brodir (Stop 6, 115 m and 313 m south).
  Options:
  - (a) do all eleven in Leg 4, hand-ins included, and end back at Basfelt. Each hand-in lies
    on a trip Leg 4 makes anyway: Kaibech beside the beehives and Komu, Gornak 115 m away,
    Brodir beside Sumarhon.
  - (b) do Basfelt's own work, and leave those three hand-ins to Legs 5 and 6;
  - (c) (a), but end at Kaibech, where Leg 5 starts.

  **Recommendation: (a).**
- **AB-Q2 — Retry budgets for the timed quests and Infernus.** A lost timer abandons Q2288;
  Q2230 loses its tusks and needs a new chance. An Infernus timeout or death needs fresh
  incense and the burner's 295 s respawn. Options:
  - (a) up to three tries each, recorded like deaths (OD-12); three failures stop the leg as a
    finding, as AC-Q2 does for the escort;
  - (b) keep trying.

  **Recommendation: (a).**
- **AB-Q3 — Komu Silverclaw respawns once an hour**, and his horn only drops at Q2289 var 7.
  Options:
  - (a) leave him alone until var 7. If he is killed early (he aggroes 7 m), hunt the west
    grounds until he respawns, up to the hour.
  - (b) if he is dead when needed, record Q2289 as not done and move on.

  **Recommendation: (a).** An hour of game time is cheap in SIM, and the hunting pays XP.

**Leg 3, answered 2026-09-29:** AC-Q1 **(a)**, AC-Q2 **(a)** and AC-Q3 **(a)**, all as
recommended:
- Leg 3 ends at Basfelt after Q2222 is handed in to Nokir.
- The escort gets up to three attempts, each after Groken's respawn and a fresh clear.
- The dock and the line are cleared before each start.

The operator also asked that the combat and healing rotation keep up with the new skills;
that is AC-00.

The original questions follow.
- **AC-Q1 — Where does Leg 3 end?** Q2222 is a Stop 3 quest, but it is handed in to Nokir at
  Basfelt Village, Stop 4's hub, about 624 m from Manir, by way of Karl. Options:
  - (a) deliver it as Leg 3's last step, so Leg 3 ends at Basfelt, where Leg 4 starts (like
    AM-Q1);
  - (b) take it and talk to Karl in Leg 3, and hand it in at the start of Leg 4;
  - (c) end at Manir, and leave Q2222 to Leg 4.

  **Recommendation: (a).**
- **AC-Q2 — How many escort attempts?** A failure resets Q2290 to var 0 and removes Groken
  for 295 s. Options:
  - (a) up to three attempts, each after Groken's respawn and a fresh clear. Then stop and
    record the escort as not done; Q2222 is locked behind it, so the leg ends at Manir
    without either;
  - (b) keep trying until it succeeds.

  **Recommendation: (a).** A failed attempt is recorded like a death (OD-12). Three failures
  are a finding to fix, not something to wait out.
- **AC-Q3 — The robbers at the boat.** A duellist stands 7 m from the boat, and three
  fencers and two smugglers stand within 32 m. Options:
  - (a) clear the dock and the line before each start, inside the 295 s respawn window;
  - (b) start the escort and fight whatever comes, in place.

  **Recommendation: (a).** A careful player does this, and it keeps the leash and death
  failures for real surprises.

**Leg 2, answered 2026-09-29:** AM-Q1 **(a)**, AM-Q2 **(a)** and AM-Q3 **(a)**, all as
recommended. Leg 2 ends at Manir's Campsite after Q2215 is handed in. It rests in the
field and restocks at the fortress only when the potion or powder policy runs low. It
fights black claw patrols only when they block the way or the pull planner calls the
fight winnable.

The original questions follow.
- **AM-Q1 — Where does Leg 2 end?** Q2215 is a Moslan quest, but it is handed in to Manir at
  Manir's Campsite, Stop 3's hub, about 310 m south-west of the crossroad. Options:
  - (a) deliver it as Leg 2's last step, so Leg 2 ends at Manir's Campsite, where Leg 3 starts;
  - (b) take it in Leg 2 and hand it in at the start of Leg 3;
  - (c) leave it for Leg 3.

  **Recommendation: (a).**
- **AM-Q2 — Rests and restocks without a vendor at the crossroad.** Options:
  - (a) rest in the field, as in Ishalgen, and walk back to the fortress (365 m) only when
    the potion or powder policy runs low;
  - (b) go back to the fortress after each ground.

  **Recommendation: (a).**
- **AM-Q3 — The SEASONED black claw patrols.** Q24012's hairpins also drop from ordinary MuMu
  patrols and highsitters. Options:
  - (a) fight black claws only when they block the way or the pull planner calls the fight
    winnable;
  - (b) seek them out, to exercise the shield, Salvation and the serums on purpose.

  **Recommendation: (a)**, recording the first real uses of those tools whenever they happen.

**All three were answered on 2026-09-29:**
- AF-Q1: **approved** (D26). The report and patch for the Java project are in
  `docs/upstream-reports/q2209-the-scribbler.{md,patch}` (prepared 2026-09-29, not yet
  submitted; the operator submits it).
- AF-Q2: **exclude** Q2210 and Q24012 from Leg 1; the bot does not leave the fortress area
  for them.
- AF-Q3: **LIVE once, at the end** of the Altgard leg.

**Answered 2026-09-29:** AL-Q6 **approved (a)** as D27. The upstream patch is
`docs/upstream-reports/q2223-a-mythical-monster.patch`. *Applied 2026-09-29*, ahead of Stop 4:
`_2223AMythicalMonster.Register` adds Lamir's talk event, and the SIM test
`Q2223LamirIsRegisteredAndAdvancesAMythicalMonster` (Q2231 and Q2224 COMPLETE, Q2223 at var 0)
passes: the click opens page 10, Q2223 shows page 1352, and SETPRO1 gives 182203217 and sets
var 1. Without the fix it fails with page 1011. Deviation 141.
All the Java fixes go upstream later as one combined PR (`docs/upstream-reports/README.md`).

- **AL-Q6 — Q2223 "A Mythical Monster" has the same defect.** The upstream scan found that
  its handler never registers Lamir (203620), whose talk is the var 0 → 1 step (retail step
  1, "Talk with Lamir"). While Q2231 is startable or active, Lamir's quest page can mask it;
  after that the quest stalls. Q2223 is on the approved list at Stop 4 (level 12). D26 covers
  only Q2209. Options: (a) approve the same one-line C# correction as D27 when the bot
  reaches Stop 4; (b) skip Q2223 as a recorded boundary. **Recommendation: (a)**, with the
  fix added to the upstream report.

The original questions follow.

- **AF-Q1 — Q2209 "The Scribbler" is broken in Java too.** Its handler's dialogue code has
  a Borender (203572) step at var 1, but `register()` never registers Borender as a talk NPC
  (Java `_2209TheScribbler.java` and the C# port alike). Talking to Borender never reaches
  the quest, so it stalls at var 1. Options:
  - (a) skip Q2209 as a recorded boundary;
  - (b) approve a narrowly scoped C# correction, registering 203572 for Q2209's talk event,
    like D19's approved corrections.

  **Recommendation: (b)**, logged as a shared-defect correction.
- **AF-Q2 — Q2210 and Q24012 are not at the fortress.** Q2210 ends at Loriniah at Moslan
  Crossroad, about 370 m south. Q24012 is at Moslan Crossroad and MuMu Farmland, about
  400–700 m south. Options: include them as Leg 1's last stop, or move them to Leg 2
  (Moslan). **Recommendation: include**, since you asked for the golds up to level 12 and
  Leg 2 starts there anyway.
- **AF-Q3 — LIVE per sub-leg or per zone?** Recommendation: one isolated LIVE run at the end of
  the Altgard leg, with SIM runs for each sub-leg.


## Progress log

- 2026-10-05 RC-11 pending: attempt eight stalls before Germir's escort starts,
  repeatedly selecting looted corpses during route clearing. Retire each successful
  clear source. The contained Leg 6 passes with three distinct sources, one successful
  escort and zero deaths; retain the full failure's two deaths and cleanup. RC-11 stays open.
- 2026-10-05 RC-11 pending: attempt seven retains a pre-bind-revive assassin in
  Leg 8 and receives an invalid-target cast rejection. Drop the view on every natural
  bind revive; a free-account native recovery probe and 89 focused tests pass.
  Retain the failed run's six deaths and cleanup; RC-11 remains open.
- 2026-10-05 RC-11 pending: the sixth continuous run completes Legs 1-9, then a
  completed Q2281 hunt counter fails to interrupt an approach. Retain its twelve deaths
  and watchdog failure. The client-counter regression and contained Leg 10 pass;
  commit the completion check and rebuild before another full capture. RC-11 remains open.
- 2026-10-05 RC-11 pending: the fifth continuous run passes Leg 7, then exposes a
  surplus-insignia assertion in Q2270. Java accepts four and consumes three. The native
  reward/relog proof, contained Leg 8 smoke (132 completions, one recorded death), 82
  focused tests and seven checks pass (`run/rc11-insignia-surplus/`). Failure and cleanup
  receipts are preserved. Fast passes 93 tests/five guarded skips/all eleven scenarios
  (`run/rc11-fast/rc11-insignia-surplus/`). Commit/rebuild and the revised full endpoint remain pending.
- 2026-10-05 RC-11 pending: the fourth full attempt's Lusena refusal and four contained
  city-sequence failures are preserved. The joined Leg 7 smoke now passes Q2278 plus
  book/library/family/dye work, ordinary fortress relog and cleanup at level 22 with
  125 completions/zero deaths. Shared checked approach and paced Lusena interception
  pass native proof, 66 focused tests, seven checks and final Fast 93 passed/five guarded
  skips/all eleven scenarios (`run/rc11-mima-interception/`, `run/rc11-fast/rc11-city-join-final/`).
  Commit/rebuild before the next continuous capture; RC-11 remains unchecked.
- 2026-10-05 RC-11 pending: three continuous failures are retained with dropped-schema
  receipts. Distinct tail sources, exact book consumption with legitimate surplus and
  the library-before-dye walking batch now pass focused/native proofs. Latest repair:
  `run/rc11-library-route/`, 66 focused tests, seven pre-commit checks and Fast
  93 passed/five guarded skips/all eleven scenarios. Commit/rebuild before fresh capture;
  no revised full endpoint has been proved yet.
- 2026-10-05 RC-09: contained Leg 10/11 finishes pass from the revised Heart state,
  with the same permit retained then consumed, 154 final completions, robe/library
  paid once and temporary stigma removed/permanent bundle sealed. Three Leg 10 deaths
  and the first proof's outdated endpoint assertion remain recorded. Exact journal
  and fortress-position checks, 60 focused tests, seven prechecks, Fast (92 passed/
  five guarded skips/all eleven scenarios) and trace/SQL/hash audit pass; owned
  schemas dropped. Evidence `run/rc09-finishes/`; continuous integration is next.

- 2026-10-05 RC-08: revised committed Leg 7/8 prefixes carry the city work and both
  Observatory prerequisites; natural Leg 9 finishes Deyla's full answer path once
  for 2,380 Kinah and loots the clothing for Deyla's later robe hand-in. Cleric 22,
  142 completions, zero segment deaths and ordinary relog pass. Forty-three focused
  tests, seven prechecks, Fast (92 passed/five guarded skips/all eleven scenarios)
  and trace/SQL/hash audit pass; owned schemas dropped. Evidence
  `run/rc08-elementary/`; RC-09's two remaining capital finishes are next.

- 2026-10-04 RC-07: the scheduled Leg 7 book, family letter and dye hand-ins pass
  naturally once, with library START/0, robe START/4 and both dye rewards carried
  through relog. Lusena's retained missing-anchor failure is corrected by observed
  patrol contact. Forty focused tests, seven prechecks, Fast (92 passed/five guarded
  skips/all eleven scenarios) and trace/SQL/hash audit pass; both owned schemas dropped.
  Evidence `run/rc07-city/`; revised Leg 7/8 checkpoints precede RC-08.

- 2026-10-04 RC-06: Neparinerk's ordinary contact and Berth endpoint relog preserve
  robe START/4 and all book materials. Contained Leg 6 ends Cleric 21, 109 completions,
  244,919 Kinah, one recovered death. The retained Q2252 alternate-reward timeout is
  fixed by following Drakie's Java group 1/page 6. Forty-two focused tests, seven checks,
  trace/SQL audit and Fast (92 passed/five skips/all eleven scenarios) pass; owned
  schemas dropped. Evidence `run/rc06-robe/`; RC-07's scheduled city finish is next.

- 2026-10-04 RC-05: all Q2919 materials are naturally carried at START/4 and survive
  ordinary relog. Native Slime Sap proof gains 4,922 XP, zero segment deaths; original
  tails/stamens, quests, equipment and historical snapshots remain intact. Seven checks,
  trace/SQL audit and Fast (92 passed/five skips/all eleven scenarios) pass; owned schema
  dropped. Evidence `run/rc05-materials/`; RC-06's Neparinerk contact is next.

- 2026-10-04 RC-04: the Leg 5 capital preparation, book fallback and both hub flights
  pass natural play and relog. Committed d0f6b9e60 snapshot `altgard-rc-l5-preparation-a4`
  restores with matching hashes/quest/item identity: Cleric 20, 99 completions, 214,590
  Kinah, one recovered death, robe START/3, book START/4 with two tails/three stamens.
  Controlled probe, 41 focused tests, seven checks and Fast (92 passed/five skips/all
  eleven scenarios) pass. Failures remain; historical snapshots are intact, owned
  schemas are dropped. Evidence `run/rc04-capital/`; RC-05's Slime Sap is next.

- 2026-10-04 RC-03: Q2919 preparation, native two-tail collection and ordinary relog
  pass on free account 246; below-13 deferral and resumed preparation send no dialogs.
  Actual book use, item identity and 282 Kinah fares are audited; failed probe drafts
  remain, owned schemas are dropped. Thirty-seven focused tests, seven checks and Fast
  (91 passed/five guarded skips/all eleven scenarios) pass. Evidence `run/rc03-book/`;
  natural Leg 5 preparation and its carried heritage proof are next in RC-04.

- 2026-10-04 RC-02: Arekedil/Chauminerk pickup and carried START/1 pass controlled and
  committed natural proof. Verified `altgard-rc-l1-a2` holds Cleric 13, 56 completions,
  the original supplied item and zero deaths; public restore/relog/SQL/hashes/cleanup pass.
  Seven final checks and Fast (90 passed/five skips/all eleven scenarios) pass; probe drafts
  and the refused first receipt remain. Evidence `run/rc02-heritage/`; RC-03 is next.

- 2026-10-04 RC-01: nine-quest continuation, protected items, observed gates and immutable
  prefix receipt support pass 46 focused tests, snapshot contract, seven prechecks and Fast
  (89 passed/five guarded skips/all eleven scenarios). Evidence in `run/rc01-carry/` and
  `run/rc01-capital-fast/rc01-capital-carry/`; historical selectors preserved. RC-02 is next.

- 2026-10-04 RC-00: prepared the revised journey's RC-00..RC-12 working checklist in
  this existing document: nine scheduled capital additions, contained segment evidence,
  one fresh-create continuous SIM and a distinct endpoint/readiness report. Corrected
  the later Abyss schedule to include Q2042. Source review and seven pre-commit checks
  pass (`run/rc00-plan-checks/`, base b66d83242); implementation and gameplay remain pending.

- 2026-10-04 AS-02: the fresh-create continuous seed-1 SIM passes on committed `1d5dad7a0` in
  55.8720 real minutes (19h 30m 10.001s game time): all fourteen stages, one character/schema/clock,
  level 25 alive at fortress, 157 quests including corrected Q2217 and all scoped campaigns, two fresh
  Haramel clears, eleven recorded deaths and thirteen endpoint relogs. The earned staff is retained,
  three approved chain pieces cost 4 Iron, Q2293 runs once with zero extra repeats, and the endpoint
  holds 19 Iron/7 Bronze, 666,444 Kinah, a sealed stigma bundle and no permanent stigma. Full audit,
  schema cleanup and seven final pre-commit checks pass (`run/natural-complete/as02-full-create-s1-a6`,
  `run/as02-final-checks`); all earlier failures remain. AS-01/AS-02 complete; no LIVE, capture or push.

- 2026-10-03: confirmed Q2217's pickup and first-arrival delivery from the retained natural Leg 3
  trace: accepted at Olenja, held through Manir/Groken/Karl, completed at Gefion during the first
  Basfelt visit, before Leg 4. Clarified route wording; the implementation and proven snapshot are unchanged.
  Q2217 remains COMPLETE exactly once across the endpoint relog; all seven pre-commit checks pass.

- 2026-10-03: the maintainer chose not to implement Q24114 "You Gotta Stop Umkata". Recorded as
  intentionally excluded here and Rejected in the D32 work list; its retail evidence and missing-content
  assessment remain intact. No server content or natural-character state changed.

- 2026-10-03 AC-C2: the isolated committed-code Leg 3 replay and corrected snapshot restore/relog pass
  first try. Q2217 once, consumed letter/100 shards, all five leg quests and 66 prior completions (71 total),
  Cleric 16 alive at Basfelt, zero deaths and escort success on attempt one. New `altgard-l3-q2217` proof
  preserves all historical snapshot hashes, drops both schemas and passes seven final pre-commit checks.
  No other natural leg was rerun, no purchases, no server changes, no push.

- 2026-10-03 AC-C1: Q2217's Java prerequisite alternatives correct the classifier's false exclusion;
  regenerated plan and Leg 3 contract/decision sequence include Olenja's pickup and Gefion's final delivery.
  Twelve compiler tests, 149 focused unit cases, seven pre-commit checks and Fast (82 passed/five switch
  skips/all eleven scenarios) pass. Original draft failures retained; committed-code isolated replay is next.

- 2026-10-03 HM-08: all 33 checklist gates pass, including the committed solution repair
  (5,184 passed/112 guarded skips), final Fast (82 passed/five switch skips/eleven scenarios)
  and eight baked maps. Two outdated packet/count test expectations were corrected from Java;
  original failures and audit draft remain. Seven final checks and the combined snapshot/coin/
  gear/stigma/recovery audit pass (`run/hm08-audit.json`, runtime `5f3e979b7`, test repair `dca2081ae`).
  All CG/HM items complete. Stop at verified `altgard-haramel-l12`; full-area continuous SIM,
  isolated LIVE acceptance and permanent stigma selection remain later work. No push.
- 2026-10-03 HM-07: distinct `altgard-haramel-l12` captured from committed `aea035881`; dump and
  original receipt hashes, owned SQL and actual restore/relog pass. Cleric 25 alive at fortress,
  156 journals, 19 Iron/7 Bronze/662,739 Kinah, unchanged staff/coin armour and no stigma skills.
  Seven proof checks pass, owned schemas dropped and CG/historical Leg 1 snapshots unchanged
  (`run/hm07-support/audit.json`). HM-08 is the final checklist and handoff.
- 2026-10-03 HM-06: natural two-fresh-clear Haramel smoke and fortress endpoint relog pass;
  level 25, 156 journals, zero deaths, 19 Iron/7 Bronze, retained staff/armour/sealed bundle.
  Six unreachable cage approaches and failed drafts a1..a21 remain recorded. Sixty-nine scoped
  tests, seven prechecks and Fast (82 passed/five switch skips/all eleven scenarios) pass;
  all owned smoke schemas dropped and incoming dumps unchanged (`run/hm06-support/audit.json`).
  The runner now awaits the committed-code `altgard-haramel-l12` capture in HM-07.
- 2026-10-03 HM-05: ordinary expiry/fresh entry, 74 original qualifying sources, packed 63/64/65,
  four follow-up quests and native death/relog/cold recovery pass. Java's unspawned channel default
  is retained; actual spawn identity drives copy observation. Scoped cases, seven checks and Fast
  (82 passed/five switch skips/eleven scenarios) pass; failed drafts retained, owned schemas gone,
  both incoming snapshot dumps unchanged. HM-06 implements the natural Haramel runner.
- 2026-10-03 HM-04: actual full-health bosses, native neighbouring pair, regular heals/potions,
  summons/movie/Cleric chest/loot/exit/relog pass. Seven prechecks and Fast 81 tests/eleven scenarios
  pass; retained drafts/cleanup in `run/hm04-support/audit.json`. Fresh post-boss recovery is next.
- 2026-10-03 HM-03: seven Haramel first-clear completions/movie/collections/counters/gates/rewards
  and actual tower key/oil consumption pass through relog; 44 focused cases, seven prechecks and
  Fast 80 tests/11 scenarios pass. Original probe/Windows resource failures retained, schemas
  dropped; normal combat is next. New endpoint name approved as altgard-haramel-l12, preserving Leg 1.
- 2026-10-03 HM-02: Haramel navigation/rebake, both elevators, tower glide, all sources/exits,
  hub flights and Heart recovery pass on free probe 223; 46 focused cases, seven prechecks and
  Fast 79 tests/11 scenarios pass (`run/hm02-support/audit.json`). First-clear protocols are next.
- 2026-09-29 AF-00: the D26 Q2209 correction is in the C# handler, with a SIM regression test
  that fails without it (page 1011) and passes with it (var 1 → 2).
- 2026-09-29 AF-01: Leg 1 contract, six compiled template plans, loader and five contract tests.
- 2026-09-29 AF-02: the fortress exit works; NA-23 started inside the obelisk. SIM probe walked 22 legs to the Ice Lake targets and back.
- 2026-09-29 AF-03: the Fortress Dungeon is a ramp walk; SIM probe talked to Noroia and Mumu Bon and walked back.
- 2026-09-29 AF-04: pure flight policy (takeoff, water, reuse, FP budget and reserve, zone bounds, landing, restore) with five tests.
- 2026-09-29 AF-05: flight protocol; the bot flies around the floating island to Borender's rock, talks, and flies back (FP 60 → 37, 40 → 17).
- 2026-09-29 AF-06: air combat; Q24011 reached REWARD in SIM with 5 fungus kills in 3 sorties, refilling on Borender's rock.
- 2026-09-29 AF-07: contract talk steps and the remedy use; SIM played Q2209, Q2207, Q2208 and Q24011 to completion (15 steps, air kills, dungeon, flight).
- 2026-09-29 AF-08: Leg 1 decision engine and journey mode; smoke run completed Leg 1 from the snapshot at level 13 with no death.
- 2026-09-29 AF-09: SIM run af09-l1-s1 completed Leg 1 with no death (level 13); snapshot altgard-l12 captured and restore-checked.
- 2026-09-29 AF-10: full checklist green after regenerating the quest drafts and moving the AF probes to their own SIM accounts; Leg 1 is done.
- 2026-09-29 AM-01: Leg 2 contract, eight template plans, the loader by leg, and three contract tests.
- 2026-09-29 AM-02: all 12 Leg 2 travel legs route and walk in SIM; the danger on each is recorded.
- 2026-09-29 AM-03: guarded talk spots; SIM handed Q2210 in and took Q2211 at the crossroad with no attack; the pluma are passive to Asmodians.
- 2026-09-29 AM-04: Q2213 played in SIM: the Okaru log looted, the poison seen and removed at Tigg; the drain is covered by natural regeneration.
- 2026-09-29 AM-05: Q24012 played in SIM: movie, zone step, three carts, var-5 drops at 100%, hauberk reward.
- 2026-09-29 AM-06: real Cleric combat for Q2211/2212/2220 from altgard-l12: 29 fights, no death; fixed the chain reset on non-chain casts.
- 2026-09-29 AM-07: the Leg 2 runner; the smoke run completed Leg 2 from altgard-l12 with no death, level 15, at Manir.
- 2026-09-29 AM-08: SIM run am08-l2-s1 completed Leg 2 with no death (level 15); snapshot altgard-l2 captured and restore-checked.
- 2026-09-29 AM-09: full checklist green after the two probes learned to follow walking NPCs; Leg 2 is done.
- 2026-09-29 AC-00: the Cleric catalog covers every auto-learned rank to level 20 (ratchet test); a level 15 SIM encounter cast every level 15 rank and the Holy Servant, no death.
- 2026-09-29 AC-01: Leg 3 contract with the escort block, the loader by leg, and four contract tests against data, handlers and the follow engine.
- 2026-09-29 AC-02: all eight Leg 3 travel legs route and walk in SIM; the dock is on the navmesh and the escort line is 111 m.
- 2026-09-29 AC-03: the pure escort policy (hops, leash bands, forbidden actions, clear/respawn/restart/give-up) and six tests pinning Java's follow check.
- 2026-09-29 AC-04: the escort protocol in SIM: logout and leash losses reset var 0 and delete Groken (295 s); the protocol restarted and delivered him in 19 s, gap 6.3 m, movie 69.
- 2026-09-29 AC-05: Q2221 in SIM: the safe (3 s bar, page 1693, loot, var 2, gone), Groken's hand-in, and Q2290 offered.
- 2026-09-29 AC-06: the Leg 3 runner; the smoke run completed Leg 3 from altgard-l2 with no death (level 16, at Nokir); the escort succeeded on attempt 1 after clearing 16 robbers.
- 2026-09-29 AC-07: SIM run snapshot-altgard-l3-s1 completed Leg 3 with no death (level 16); snapshot altgard-l3 captured and restore-checked.
- 2026-09-30 AC-08: full checklist green after run-fast's walker-dependent probes learned to follow or skip, and the air-combat cast gate is released on a refusal; Leg 3 is done.
- 2026-09-30 AB-01: the Leg 4 contract (hunts, timers, the Infernus spawn, Komu, the zone-bound poison, the Basfelt bind), five template plans, and five contract tests.
- 2026-09-30 AB-02: all 19 Leg 4 travel legs route and walk in SIM, Sumarhon's height and the beehive grove included; the Q24013 ground holds 11 Feral Sharpeyes.
- 2026-09-30 AB-03: the pure timed-quest policy (readiness gate, budget, turn-in, abandon/new-chance, three tries, forbidden actions) with five tests.
- 2026-09-30 AB-04: both timed quests proven in SIM (logout, expiry, success; Q2230's new chance); found the Q1044/Q2042 timer defect (AB-Q6).
- 2026-09-30 AB-05: Q2223 in SIM: no masking by Lamir's own quests; the burner, movie 67, Infernus for 300 s, the refill incense, the second burn and the reward.
- 2026-09-30 AB-06: Q2231, Q2232 (nine beehives), Q2239 (var 1->3), Q2289 (movie 62, Komu's Horn) and Q24013's poison in its zone played in SIM.
- 2026-09-30 AB-07: Leg 4 combat at L16 and L17: first real Anti-Shock shield and Salvation, Divine Touch at L17; the Feral Sharpeye group retreats the L16 Cleric and kills the L17 one.
- 2026-09-30 AB-08: Leg 4 runs end to end in SIM from altgard-l3: 14 quests, L16 to L19, 10 recorded deaths, both timers handed in (Q2230 on its new chance), Infernus on the first try.
- 2026-09-30 AB-09: SIM run snapshot-altgard-l4-s1 repeated the AB-08 run exactly (14 quests, L19, 10 deaths); snapshot altgard-l4 captured and restore-checked.
- 2026-09-30 AB-10: full checklist green except run-fast E1 (D32 register gives Q24110 account 42, E1's); AB-04 probe fixed to wait out Q2230 in the village. Leg 4 done in SIM.
- 2026-10-01 Leg 5 planned (Kaibech, Idun's Lake, MuMu Village; AK-00..AK-10); AK-Q1..AK-Q3 approved as recommended.
- 2026-10-01 D32 catch-up: Q24110 in Leg 1, Q24111 in Leg 3; Legs 1–4 replayed from `altgard` on 5083bb385 (stale-route refusal, death-spot hazards, hunt road fallback); levels 13/15/16/19; Leg 5 starts at 19.
- 2026-10-01 AK-00: the bot decodes SM_GAME_TIME (GameMinutes, GameHour); the SIM clock drives the hourly ring carriers (MuMu Zoo at 22:00, Lu at 05:00).
- 2026-10-01 AK-01: the Leg 5 contract (13 template quests, timedSpawns, held) and its 13 plans; the stale D32 plans for Q24232/Q24233 regenerated.
- 2026-10-01 AK-02: Leg 5's 20 travel legs walked in SIM; the AK-00 and AB-05 probes made robust in run-fast.
- 2026-10-01 AK-03: the pure carrier policy and the client game clock (NaturalGameClock, NaturalCarrierPolicy) with unit tests.
- 2026-10-01 AK-04: Q2292 taken by the game clock in SIM (Ring and Di at 00:00, a wait for Zen at 04:00); the client stamps SM_GAME_TIME on arrival.
- 2026-10-01 AK-05: MuMu Village's five quests in SIM (batch, hairpins, sacks, Manumumu); Q24233 is held at START with its counter full, as Java has it.
- 2026-10-01 AK-06: Kaibech, the outlaws and Sumarhon's camp in SIM (Q2233-Q2235, Q2241, Q24230, Q24231 claimed; Q2242 held).
- 2026-10-01 AK-07: Leg 5 combat at level 18 and 19 (MuMu pull, Manumumu, sentry and fencers): no deaths; Manumumu reached 25% at level 18.
- 2026-10-01 AK-08 (in progress): the carrier, held and town-service engine and runner; four smoke runs reach level 20 with both day rings; blocked on AK-Q4 (the full cube).
- 2026-10-01 AK-08: the whole of Leg 5 in SIM (smoke run 8): the Pandaemonium cube expansions, all eleven claims, Q2292 by the clock, Q2242 and Q24233 held; level 20, 1 death.
- 2026-10-01 AK-08 (flights): hub flight transporters from the client routes; smoke run 9 flies Basfelt to the fortress and plays Leg 5 with no deaths.
- 2026-10-01 AK-09: `altgard-l5` captured from `altgard-l4` at 53d488402 (level 20, no deaths, Q2242/Q24233 held), restore-checked; it includes the PatternAi flee fix c379a18e1.
- 2026-10-01 AK-10: the full checklist on 72824ee0f; everything passes except the flee probe's D32 account clash (c379a18e1, not Leg 5's). Leg 5 is done.
- 2026-10-01 AK-10 follow-up (the maintainer asked): the flee probe moved to account 69 (5297762ce); bisecting its run-fast failure found a server gap, a flee counted as arrived while its target was in reach, fixed in PatternAi (6cc174b33). run-fast passes in full.
- 2026-10-01 AG-00: chained hub flights (NaturalAirlineRoutes.Journey); a SIM probe flies Basfelt to the fortress to Trader's Berth; TalkAsync waits for a walking NPC to stop.
- 2026-10-02 AG-01: the Leg 6 contract (12 quests, the Q2284 escort from var 1 with its follower's hours, the Q2252 spawn, Q24115 and Q2262 held) and its 9 template plans.
- 2026-10-02 AG-02: Leg 6's 16 travel legs walked in SIM from the Trader's Berth obelisk.
- 2026-10-02 AG-03: Gerger's scripted quests and the night wait for Q2284's escort played in SIM.
- 2026-10-02 AG-04: Q2252's bones, a miss, new bones and the kill played in SIM.
- 2026-10-02 AG-05: Leg 6's template quests played from their plans in SIM, with Q24115 and Q2262 held.
- 2026-10-02 AG-06: Leg 6's fights at level 20; no deaths, and one retreat at the Bones of Minushan.
- 2026-10-02 AG-07: the Leg 6 runner; smoke run 5 plays the whole leg from altgard-l5 to level 21 with one death.
- 2026-10-02 AG-08: snapshot altgard-l6 captured from 7d10ef4c1 (level 21, bound at the Trader's Berth).
- 2026-10-02 AG-09: the full checklist passes on f0f364b60; Leg 6 is done, and Leg 7 is next to plan.
- 2026-10-02 Leg 7 proposed: Altgard Fortress and the East Gate, thirteen quests from the fortress bind (AE-Q1..AE-Q3 open).
- 2026-10-02 Leg 7 approved: AE-Q1..AE-Q3 as recommended; ready for Loop mode (AE-00..AE-08).
- 2026-10-02 AE-00: quest steps carry their map; Q2278's Pandaemonium teleporter trip, city talks and Return pass in SIM; Fast and pre-commit checks pass (`62863fcca`).
- 2026-10-02 AE-01: the Leg 7 contract and eleven plans; fortress bind, held hand-ins, Q2146 held, Q2263 timer and cross-map/pool steps; contract, compiler, Fast and pre-commit checks pass.
- 2026-10-02 AE-02: 27 Leg 7 ground legs and the fortress–Berth hub flights pass in SIM; both Spirit spots route; Fast and pre-commit checks pass.
- 2026-10-02 AE-03: both Spirit spots, the pollen timer's expiry/cleanup and successful retake pass; Q2278 retained; Fast and pre-commit checks pass.
- 2026-10-02 AE-04: eleven template plans pass, including both incoming hand-ins, Q2146 held, and the Berth round trip by flight; pre-commit checks pass.
- 2026-10-02 AE-05: level-21 swamp encounters record nine distinct kills, no deaths or retreats; corpse overcount and a shared-probe assumption corrected; Fast and pre-commit checks pass.
- 2026-10-02 AE-06: Leg 7 smoke and relog pass from altgard-l6; 35 distinct kills, no deaths/retreats, first pollen timer, all hub flights; earlier failures/outcomes retained; Fast and pre-commit checks pass.
- 2026-10-02 AE-07: altgard-l7 captured from committed code 2df0d2cad and restored successfully; level 21, fortress bind, 264,842 Kinah, thirteen completions and two held quests; owned schemas dropped.
- 2026-10-02 AE-08: all 33 CLAUDE checklist commands completed; the D32 account reservation failure corrected with free accounts 201–206; inventory, affected encounters, Fast and pre-commit checks pass. Leg 7 is done; altgard-l7 starts Leg 8.
- 2026-10-02 AO-00: Leg 8 contract, five plans and eight scripted steps; Q2266 precedes the Observatory bind; six contract/decision cases, Fast and pre-commit checks pass, from 734f82eae.
- 2026-10-02 AO-01: 23 Observatory/camp/object ground legs and three fortress/Observatory flights pass on free account 207; maximum miss 2.5 m, pre-commit checks pass.
- 2026-10-02 AO-02: seven quests, Operation Order use/loot and three hub flights pass on free account 208; Java helper pages and guard setup corrected; the Drakie reward probe correction, six contract cases, Fast and pre-commit checks pass.
- 2026-10-02 AO-03: level-21 Observatory encounters on free account 209 record eight distinct kills, zero deaths/retreats; all four stages and pre-commit checks pass.
- 2026-10-02 AO-04: natural Leg 8 smoke and relog pass from altgard-l7; seven completions, three flights, 23 distinct kills, one death/two retreats; deferred corpse loot and completed-source recovery fixed; Fast and pre-commit checks pass, failed runs retained.
- 2026-10-02 AO-05: altgard-l8 captured from committed 81774eeca, hash-checked and restored; Cleric 21 alive at Observatory, seven completions/two held quests, 317,511 Kinah, campaigns unchanged; both owned schemas dropped.
- 2026-10-02 AO-06: all 33 CLAUDE commands pass on 23537bb24, solution 5,074 passed/95 skips, Fast 67 passed/three switch skips/all eleven scenarios; five baked maps validate. Leg 8 is done; altgard-l8 starts Leg 9. No push.
- 2026-10-02 AH-00: Leg 9 contract and nine compiled plans from 37146605b; seven new quests and two held hand-ins, 254,866 XP, Heart bind and city recipient; three contract cases, Fast and pre-commit checks pass. Travel is next.
- 2026-10-02 AH-01: 30 Heart travel legs, four hub flights and two pillar flights pass on free account 210; debris landing avoids the pillar structure, source selection avoids disconnected ledges; failed drafts retained, pre-commit checks pass. Quests are next.
- 2026-10-02 AH-02: nine hand-ins, consumed work/collection items, nine debris uses and two ordinary respawn rounds pass on free account 211; three hub flights, real city delivery and learned Return, campaigns unchanged; first run and pre-commit checks pass. Combat is next.
- 2026-10-02 AH-03: four level-21 Heart combat stages record nine distinct kills, no deaths/retreats on free account 212; neutral diagnostic targeting and a zero-time moving-target approach stall corrected, earlier traces retained; final encounter, Fast and pre-commit checks pass. Natural runner is next.
- 2026-10-02 AH-04: first natural l9 smoke from altgard-l8 passes through relog, nine completions/work items consumed/campaigns unchanged; Cleric 22 alive at Heart, 35 distinct kills, no deaths/one retreat, three hub/eight pillar flights and nine debris uses; 21 decision cases, Fast and pre-commit checks pass, owned schema dropped. Snapshot is next.
- 2026-10-02 AH-05: altgard-l9 captured from committed 96545cd84, hash-checked and restored; Cleric 22 alive at Heart, nine completions/no held quests, work items consumed, 378,680 Kinah, campaigns unchanged; both owned schemas dropped. Full checklist is next.
- 2026-10-02 AH-06: all 33 CLAUDE commands pass on da427b420, solution 5,080 passed/97 skips, Fast 69 passed/three switch skips/all eleven scenarios; five baked maps validate. Temporary runner argument typo corrected with invalid attempt retained. Leg 9 is done; altgard-l9 starts Leg 10. No push.
- 2026-10-02 BC-00: Leg 10 scope and Java/data audit complete from 63d23d4d1; six templates/three campaigns, 959,700 XP, Q2281's three counters, real Bregirun entry/recovery/movie exit, fortress bind/endpoint and retained Q2900; pre-commit checks pass. Contract and plans are next.
- 2026-10-02 BC-01: l10 contract and six plans, all independent Q2281 counters and Q24016 prerequisites/map decisions; 36 scoped cases, Fast and pre-commit checks pass, earlier drafts retained. Travel is next.
- 2026-10-02 BC-02: 54 level-22 travel legs, 20 source kinds/ten totems, actual Heart flight, quest teleport, both Bregirun portals, Vidar dialog and learned Returns pass on free account 213; generated Bregirun mesh rebakes identically. Earlier drafts retained; Fast and pre-commit checks pass. Template quests are next.
- 2026-10-02 BC-03: six template hand-ins/work items consumed, both partial Q2281 hand-ins rejected, 34 controlled kills/three ordinary respawn waits and real Vidar delivery/Return pass on free account 214; expected-rejection labeling and nullable warnings corrected, draft evidence retained; pre-commit checks pass. Campaigns are next.
- 2026-10-02 BC-04: all three campaigns, gated/consumed Orb, natural unlock, real portals/guardian/boss/movies and ordinary self-revival/exit/re-entry pass on free account 215; bind-route boundary and assertion drafts retained, pre-commit checks pass. Learned Hand of Reincarnation protects Bregirun attempts; no server changes. Combat is next.
- 2026-10-02 BC-05: thirteen level-22 combat stages record 19 distinct kills, no deaths/retreats on free account 216; learned 21–22 ranks/Divine Spark added after Java audit, seventeen policy tests and final encounter pass; reserve-assertion draft retained, Fast and pre-commit checks pass. Natural runner is next.
- 2026-10-02 BC-06: natural l10 smoke and relog from altgard-l9 complete all nine quests at Cleric 24, alive/bound at fortress, work items consumed and Q2900 START/0; 185 distinct kills, three deaths/eight retreat choices, three hub/two pillar flights and real city/Bregirun trips. Fifteen failed/eight stopped drafts retained; 41 scoped cases, pre-commit checks and Fast pass, owned schemas dropped. Snapshot is next.
- 2026-10-02 BC-07: altgard-l10 captured from committed 6986e3539, hash-checked and restored through an actual endpoint relog; Cleric 24 alive at fortress bind, all nine quests complete, work items absent, 495,028 Kinah and Q2900 START/0. Both owned schemas dropped; pre-commit checks pass. Full checklist is next.
- 2026-10-02 BC-08: all 33 CLAUDE commands pass on e43228041, solution 5,105 passed/100 skips, Fast 72 passed/three switch skips/all eleven scenarios; six baked maps validate. Leg 10 is done; altgard-l10 is retained for Q2900, then end-of-area LIVE acceptance. Stop at this checkpoint. No push.
- 2026-10-02 Leg 11 proposed: Q2900 from altgard-l10, four maps, Anturoon/Aldelle hub flights, movie/equip/spawn/cleanup and ordinary bind recovery; ND-00..ND-08 and full-area SIM/LIVE handoff planned. Java/data and official 4.8 stigma audit; Space offline mesh/three routes pass, initial diagnostic omission retained. Seven pre-commit checks pass (`run/nd-plan-checks`). No gameplay implementation or natural run; no push.
- 2026-10-02 ND-00: implementation approved; Java/full-var/stigma and snapshot audit pass from eebceefa7, reward boundary and ordinary recovery pinned, numeric checkpoint assertion draft retained. Seven pre-commit checks pass; contract and decisions are next.
- 2026-10-02 ND-01: l11 contract/full vars/four maps and phase/recovery decisions committed from 588f15ea3; explicit l11 Cleric instance identity, protected reward/cleanup endpoint and all 143 incoming completions. Eighty-one scoped cases, seven pre-commit checks and Fast pass; two assertion drafts retained. Paths and map protocol are next.
- 2026-10-03 ND-02: generated/rebaked Space mesh and free-account-217 travel pass: 24 ground legs, both Ishalgen flights, four paid city trips, five quest teleports and learned Return, alive fortress endpoint. Compile drafts retained; seven pre-commit checks and Fast pass, owned schemas dropped. Movie/stigma/campaign protocol is next.
- 2026-10-03 ND-03 partial: free-account-218 movie/equip/cleanup/Aud reward and real bundle installation pass; the inactive legacy class reward contradicts the approved endpoint, recorded as pending ND-Q1. Seven pre-commit checks and Fast (74 passed/three switch skips/all eleven scenarios) pass; failed drafts retained and owned schemas dropped. ND-04's four-map loss/resume plan includes cold-restart exit and stigma-login traps. Stop with ND-03 unticked/uncommitted; every later ND item depends on the pending decision.
- 2026-10-03 ND-Q1 answered: the maintainer chose Option A. Correct the approved reward action and endpoint to shipped Java/C#, keep the bundle sealed and inactive legacy item absent, then finish ND-03 and continue the remaining items. No server correction is authorized or required for this reward boundary.
- 2026-10-03 ND-03: the amended contract's exact reward path, movie/equip/cleanup and 144 journal completions pass; probe-only bundle installation confirms the normal slot. Eighty-one scoped tests, seven pre-commit checks and Fast (74 passed/three switch skips/all eleven scenarios) pass; failed drafts retained and owned schemas dropped. ND-04 loss/resume recovery is next.
- 2026-10-03 ND-04: free-account-219 loss/resume matrix and two-process cold restart pass; two controlled deaths, one real missed spawn, 65 ground legs, seven saved-state resumes and fortress recovery. Java's older-instance selection, stigma-login audit and missing-exit fallback are recorded. Duplicate pending map-load acknowledgements are coalesced; 39 scoped tests, seven pre-commit checks and Fast (75 passed/four switch skips/all eleven scenarios) pass. Failed drafts retained, owned schemas dropped. Normal-HP Hellion combat is next.
- 2026-10-03 ND-05: free-account-220 normal-HP Hellion fight passes in 16.371 seconds, zero deaths/retreats, real 175–200 damage, actual tutorial socket/cleanup and ordinary heals preserved. Regular level-23/24 skill catalog and late quest-kill loot-state invalidation verified; 33 scoped tests, seven pre-commit checks and Fast (76 passed/four switch skips/all eleven scenarios) pass. Failed drafts retained, owned schemas dropped. Natural l11 runner and smoke are next.
- 2026-10-03 ND-06: natural l11 smoke passes once from altgard-l10; normal Hellion kill in 12.021 seconds, nine distinct kills, no deaths/retreats, real hub flight and movie/socket/reward/Return, 144 completed journals and fortress endpoint relog. Tutorial stigma removed, regular skills preserved, reward bundle sealed. Build, 81 scoped cases, seven pre-commit checks and Fast (76 passed/four switch skips/all eleven scenarios) pass; owned schema dropped. Snapshot is next.
- 2026-10-03 ND-07: altgard-l11 captured from committed e23d5a511, hash-checked and restored through actual endpoint relog; Cleric 24 alive at fortress bind, Q2900/all prior quests complete, 144 journals, sealed bundle, no temporary stigma/legacy item, ordinary skills preserved and 536,193 Kinah. Both owned schemas dropped; PowerShell parse and pre-commit checks pass. Full checklist is next.
- 2026-10-03 ND-08: all 33 CLAUDE commands pass on 7f3289b4f; solution 5,144 passed/105 skips, final Fast 76 passed/four switch skips/all eleven scenarios, seven baked maps valid. Launcher diagnostic retained with its correction; committed altgard-l11 capture/hash/SQL/endpoint relog remain verified, owned schemas dropped. Leg 11 done; stop at this checkpoint. Continuous full-area SIM, isolated LIVE and permanent stigma choice remain later work. No push.
- 2026-10-03 coin/Haramel plan: the operator requested Q2293/better coin chain gear before Haramel as Leg 12. Verified altgard-l11 holds 18 Iron Coins from eighteen prior rewards; the seven-item rare Cleric chain/mace/scale-shield manifest costs 12 in all seven catalogues, zero funding repeats, recommended one quest completion leaves 11. CG-00..CG-06 and HM-00..HM-08 are planned and unchecked; Haramel covers eleven active quests in two fresh clears, 65 packed kills, lift/class chest/ordinary expiry, then fortress endpoint. Machine audit and seven pre-commit checks pass (`run/cg-hm-plan`); the shared-build collision and sequential retry are retained. No natural run or push.
- 2026-10-03 armour review: verified actual worn IDs and unenchanted/unsocketed snapshot SQL against matching Java/C# item stats. Keep the equipped level-21 staff as instructed; exclude weapon/shield purchases and swaps. Revised recommendation buys chain gloves/shoulders/legs for 4 coins, keeps identical-stat chest/boots and old cloth gloves, and records the glove casting-stat tradeoff. One Q2293 completion leaves 19 coins. Both plans and combined prompt amended; machine audit and seven pre-commit checks pass (`run/cg-hm-armor-review`), base `5fef285bd`. No natural run or purchase.
