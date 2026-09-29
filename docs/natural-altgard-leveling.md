# Natural Altgard leveling leg

**Status (2026-09-29): the list, the order and AL-1..AL-5 are approved**, with the
recommendations and the operator's notes below.

The zone is worked in **sub-legs**. The first one is
[Leg 1: Altgard Fortress, level 10–12](#leg-1-altgard-fortress-level-1012). It has its own
TODO list, worked in Loop mode like [the Ascension bridge](natural-ascension-altgard.md).

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
  z 254. You then kill six **Abyss Fungus** (700092), which float at **z 367–400** over the
  fortress (12 spawns).
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
| 2223 | A Mythical Monster | 12 | Gefion | | talk to Lamir (**needs the D27 correction**), burn the incense: Infernus (EXPERT, L13) spawns for 5 min; kill it | 23,250 |
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
Crossroad. Their targets on the Ice Lake just west of the fortress are part of the fortress
area. It starts from the `altgard` snapshot, a level 10 Cleric bound at the fortress.

| Quest | Name | Level | Where it is done | Notes |
|---|---|---|---|---|
| 2201–2206 | Crasaur, Lobnite, Crystals, Sparkie Sap, Airon, Slink | 10 | fortress NPCs; targets on the **Ice Lake** just west (z ≈ 247) | The first thing the bot needs is a way **out of the fortress** (NA-23: the route out was GeometryRejected). |
| 2207 | Conversing With a Skurv | 11 | fortress (Emgata, Itu, Suthran) | |
| 2208 | Mau in Ten Minutes a Day | 11 | Itu; **Mumu Bon in the Fortress Dungeon (z 203)** | Needs the dungeon route and the Mau Secret Remedy item use. |
| 2209 | The Scribbler | 10 | Thrud, Tulberg, **Borender (z 406, flight)**, **Noroia (dungeon, z 205)** | Needs the D26 correction (AF-Q1, approved): Java never registers Borender. |
| 24011 | Funny Floating Fungus (campaign) | 11 | Valurion, **Borender**, **6 Abyss Fungus in the air** | Flight. |
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
- [ ] **AF-01 — The Leg 1 contract and quest plans.** Write
  `parity-artifacts/e2e/natural-altgard-contract.json`: the Leg 1 quests, their order, level
  gates, hubs and endpoint. Compile the template quests' plans (`compile-quest-plans.py`)
  into `parity-artifacts/e2e/natural-altgard-plans/`. Add a loader and contract tests like
  NA-01's.
- [ ] **AF-02 — Leave and re-enter the fortress on foot.** Diagnose NA-23's
  `GeometryRejected` route (obelisk → Ice Lake / open ground): a gate, a static door, or a
  navmesh/geo mismatch. Fix it bot-side, with checked navigation data, never by editing
  generated nav data by hand. **Done when:** a SIM probe walks from the obelisk to the Ice
  Lake targets and back.
- [ ] **AF-03 — The Fortress Dungeon.** A route to Mumu Bon (z 203) and Noroia (z 205):
  the entrance, navmesh coverage, map context. **Done when:** a SIM probe talks to both.
- [ ] **AF-04 — Flight policy (pure).** Covers:
  - the two FLY zones;
  - takeoff checks: a Daeva, inside a FLY zone and not NO_FLY, the 10 s reuse, and **not
    on water** (the navmesh Water area or below z 200);
  - the FP budget: 60 FP, 1 per tick inside the zone, landing reserved with a margin;
  - never leaving the zone while airborne;
  - landing targets (the ground, or Borender's rock).

  **Done when:** policy tests cover each rule, including the water refusal and the FP margin.
- [ ] **AF-05 — Flight protocol and movement.** Take off (`CM_EMOTION` FLY), fly (`CM_MOVE`
  in flight) to a point in the air, land (`LAND`) on ground or on a platform, watch
  `SM_FLY_TIME`, and let FP refill on the ground. **Done when:** a SIM probe flies from the
  fortress to Borender's rock, talks to him, and lands back safely with FP left.
- [ ] **AF-06 — Air combat for Q24011.** Kill the Abyss Fungus from the air within the FP
  budget: fly to one, kill it, land to refill as needed. A fall or death is recorded
  (OD-12). **Done when:** a SIM probe completes Q24011's six kills.
- [ ] **AF-07 — Leg 1 quest mechanics.** Talk chains (Q2207), item use in the dungeon
  (Q2208, Mau Secret Remedy), Q2209's chain (Thrud → Tulberg → Borender → Noroia), and the
  template kill and collect quests (Q2201–2206, including the crystal stone objects for
  Q2203). It reuses the Ishalgen quest machinery where it fits.
- [ ] **AF-08 — The Leg 1 runner and decision engine.** From the `altgard` snapshot: an
  Altgard map context, the decision engine over the contract (eligibility, level gates,
  "come back later"), rests and restocks at the fortress, help items (NA-21), and the
  missions as they auto-start. Trace every decision.
- [ ] **AF-09 — One SIM run of Leg 1** from the `altgard` snapshot to the Leg 1 endpoint,
  saved as the `altgard-l12` snapshot. Deaths are recorded (OD-12).
- [ ] **AF-10 — The full `CLAUDE.md` checklist and a checkpoint.** The isolated LIVE run comes
  at the end of the Altgard leg, not after each sub-leg (AF-Q3, decided).

## Blocked / questions for the operator

**All three were answered on 2026-09-29:**
- AF-Q1: **approved** (D26). The report and patch for the Java project are in
  `docs/upstream-reports/q2209-the-scribbler.{md,patch}` (prepared 2026-09-29, not yet
  submitted; the operator submits it).
- AF-Q2: **exclude** Q2210 and Q24012 from Leg 1; the bot does not leave the fortress area
  for them.
- AF-Q3: **LIVE once, at the end** of the Altgard leg.

**Answered 2026-09-29:** AL-Q6 **approved (a)** as D27. The C# correction is applied when the
bot reaches Q2223 at Stop 4; the upstream patch is `docs/upstream-reports/q2223-a-mythical-monster.patch`.
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
