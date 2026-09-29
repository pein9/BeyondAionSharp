# Natural Altgard leveling leg

**Status (2026-09-29): the list, the order and AL-1..AL-5 are approved**, with the
recommendations and the operator's notes below.

The zone is worked in **sub-legs**. The first one is
[Leg 1: Altgard Fortress, level 10–12](#leg-1-altgard-fortress-level-1012). It has its own
TODO list, worked in Loop mode like [the Ascension bridge](natural-ascension-altgard.md).

**Leg 1 is done (AF-00..AF-10, 2026-09-29).** [Leg 2: Moslan Crossroad](#leg-2-moslan-crossroad-level-1315--proposal) is done (AM-01..AM-09, 2026-09-29): `altgard-l2` starts Leg 3.

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
| 2011–2022, 2200, 24110, 24111, 24113–24115, 24232, 24233 | | No handler (unimplemented). Not to be added. |
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

## Blocked / questions for the operator

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
