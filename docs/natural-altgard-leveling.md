# Natural Altgard leveling leg

**Status (2026-09-29): the list, the order and AL-1..AL-5 are approved**, with the
recommendations and the operator's notes below.

The zone is worked in **sub-legs**. The first one is
[Leg 1: Altgard Fortress, level 10–12](#leg-1-altgard-fortress-level-1012). It has its own
TODO list, worked in Loop mode like [the Ascension bridge](natural-ascension-altgard.md).

**Leg 1 is done (AF-00..AF-10, 2026-09-29).** [Leg 2: Moslan Crossroad](#leg-2-moslan-crossroad-level-1315--proposal) is done (AM-01..AM-09, 2026-09-29): `altgard-l2` starts Leg 3.
[Leg 3: Manir's Campsite and Dock](#leg-3-manirs-campsite-and-dock-level-15--proposal) is done (AC-00..AC-08,
2026-09-30): `altgard-l3` starts Leg 4 at Basfelt.
[Leg 4: Basfelt Village](#leg-4-basfelt-village-level-1617--proposal) is approved (AB-Q1..AB-Q5 as recommended)
and ready for Loop mode (AB-01..AB-10).

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
| 2217, 2285 | | Unreachable: the classifier finds their prerequisites unobtainable. |
| 2229, 2240, 2294, 2295, 80172–80210 | | Disabled: level 99 gate. |
| 80005, 80142, 80143, 80262 | | Event quests. |
| 2011–2022, 2200, 24114 | | No handler in Java or C#. Q2011–Q2022 (and Q2200) are superseded pre-4.0 missions. Q24114 was live in 4.8 retail; it needs a custom handler and Umkata's summon, which D32 (`docs/retail-quest-completion.md`, RQ-05) has proposed and the maintainer has yet to approve. **Q24110, Q24111, Q24113, Q24115, Q24232 and Q24233 exist since RQ-05 (2026-09-30)**: a leg can take them in now. |
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

Quest XP is 43,290, plus the robbers' kills. The leg stays at level 15.

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
- **Still missing:** Q24114 needs a custom handler and Umkata's summon, proposed in RQ-05 and
  waiting for the maintainer's approval.
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
- [ ] **AB-10 — The full `CLAUDE.md` checklist and a checkpoint.**

**Endpoint (AB-Q1 (a)):**
- all eleven quests, Q2227, Q2291 and Q24013 completed (AB-Q4);
- bound at the Basfelt obelisk (AB-Q5);
- alive, at Basfelt Village within 60 m of Nokir;
- the endpoint verified across a relog.

## Blocked / questions for the operator

**Leg 4, answered 2026-09-30:** AB-Q1 **(a)**, AB-Q2 **(a)** and AB-Q3 **(a)**, all as
recommended:
- All eleven quests are done in Leg 4, the three hand-ins at other hubs included, and the leg
  ends at Basfelt.
- The timed quests and Infernus get three tries each.
- Komu is left alone until Q2289 var 7.

**Leg 4, open (2026-09-30):**
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
