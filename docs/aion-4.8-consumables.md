# Aion 4.8 consumables for the natural bot (research, 2026-09-28)

Research for NA-19/NA-20 and Appendix D of
[natural-ascension-altgard.md](natural-ascension-altgard.md). Nothing was changed or run.
Local paths are relative to the repository root. `items:` is
`game-server/data/static_data/items/item_templates.xml` and `skills:` is
`game-server/data/static_data/skills/skill_templates.xml`.

## Summary

- **C# data equals Java.** `item_templates.xml`, `skill_templates.xml`, `goodslists.xml`,
  the regional goodslists and `npc_trade_list.xml` match the Java checkout (4.8,
  `ce54b7931`) byte for byte, apart from line endings. So every item and skill below is
  shared upstream data. `npc_templates.xml` and `220010000_Ishalgen.xml` differ from Java
  (the known retail-AI and 5.8-placement work), but every vendor named here spawns in both.
- **The retail 4.8 client agrees too.** The user's client is at `C:\Program Files
  (x86)\Beyond Aion` (`client_items_etc.xml` was generated 2015-11-24). It was read with
  `tools/client-extract` into a scratch folder. For item ids 160xxxxxx–164xxxxxx, 1693000xx
  and 1840000xx, the client and the server agree on the skill id, skill level, use delay,
  delay group, item level and per-class restrict. The only mismatches are three unused
  test scrolls (164006003/006/009).
- **164000015 Lesser Physical Defense Scroll: not a bug. It did nothing in retail 4.8
  either.** The retail client item has no `activation_skill`. Its tooltip is the bare
  template `[%e1.StatUp.StatName] increases by [%e1.StatUp.Value]…`, with no skill to
  fill it in. No retail recipe, vendor list or quest produces it, and nothing in the server
  data does either. aioncodex's 4.x page shows the same unfilled template and no sources.
  164000018, 019 and 020 (the Lesser Physical Attack, Resist Magic and Magical Attack
  Scrolls) are the same. They are left-over 1.x alchemy scrolls. Leave them as they are.
- **Anti-Shock scrolls are damage shields, not stun resistance.** Each tier is its own skill,
  used at level 1:

  | Scroll | Item level | Skill | Absorbs, total and per hit |
  |---|---|---|---|
  | Lesser | 20 | 9953 | **158** |
  | plain | 30 | 9954 | 245 |
  | Greater | 40 | 9955 | 338 |
  | Major | 50 | 9956 | 425 |
  | Fine | 60 | 9964 | 550 |

  Every tier lasts 24 s, and they share delay group 32 (60 s). "148+10/level" is the
  formula; with the fixed skill level 1 it gives 158. The Altgard quest **2206 "Slinking
  About"** (level 10) already rewards 8 Lesser Anti-Shock Scrolls.
- **Courage and Awakening cannot both be up.** Both statups carry effect id 30184 in the
  same BUFF slot. The Java stacking rule (`EffectController.searchConflict`, mirrored in
  C#) ends the older one when the other is used. The retail client uses the same effect
  id. Appendix D's "both can be kept up, 15 s apart" is wrong: pick one. For a Cleric,
  **Awakening** (casting speed) is worth more.
- **Running does not add to other speed buffs.** Running, the Movement Speed Scroll and the
  Worg/Karnif soul transforms share effect id 30182, so each one replaces the others.
- **No NPC sells the Courage, Awakening, Running, Anti-Shock, Crit or Resist scrolls,** in
  the server data or in the retail client's goods lists. In 4.8 they came from Alchemy
  (Asmodian recipes 155006229–155007167), quests and the broker. Keeping them up for hours
  therefore needs supplied items.
- **Two more corrections to the existing appendices:**
  - Consumables do **not** need a weapon. The weapon start condition only applies to
    skills cast from the skill bar (`WeaponCondition.java:30`). Appendix A's "Every potion
    needs an equipped weapon" is wrong.
  - Using any item **cancels the current cast** (`CM_USE_ITEM.java:76`).

## Families

Common facts, unless a row says otherwise:
- Every scroll and potion has a cast time of 0, first target ME, and `move_casting
  allow=false`.
- "Restrict" is the `restrict` attribute. When it is absent, the Java default
  (`ItemTemplate.java:85`) makes it level 1 for every class. The retail client agrees for
  every item in these tables.
- `PlayerRestrictions.canUseItem` (Java line 332; C# `PlayerRestrictions.cs:329`) checks
  only the restrict, never the item level.
- Values are computed as `value + delta × skill level` (`BufEffect.java:74`,
  `ShieldEffect.java:39`). Durations are `duration2 + duration1 × level`
  (`Effect.java:903`).
- "Group" is `usedelayid` / `usedelay`. A group's cooldown is the delay of the item that
  was used (`Player.java:1009`).

### Attack and casting speed (group 34, 15 s)

**Courage:** skill 9959 `Attack Speed Increase` (skills:92851), stack `ITEM_SPEED_ATK`,
ATTACK_SPEED −3%/level, 300 s, dispel BUFF, effect id 30184.

| Id | Name | Item level | Skill level | Effect | Price | Local sources | Line |
|---|---|---|---|---|---|---|---|
| 164000071 | Lesser Courage Scroll | 10 | 1 | +3% attack speed | 1200 | Alchemy 180 (Asmodian recipe 155006236); quests 2277 ×5 (lvl 16), 2364 ×8 (lvl 31); Q2150 ×12 (selectable; no handler) | items:832763 |
| 164000072 | Courage Scroll | 20 | 2 | +6% | 2000 | Alchemy 280 (155006237) | items:832769 |
| 164000073 | Greater Courage Scroll | 30 | 3 | +9% | 3000 | Alchemy 380/480; one boss custom drop (`custom_drop.xml:1857`) | items:832775 |
| 164000331 | Greater Courage Scroll (second id) | 30 | 3 | +9% | 3000 | — | items:834276 |

**Awakening:** skill 9965 `Casting Speed Increase` (skills:92929), stack
`ITEM_SPEED_BOOSTCASTINGTIME`, BOOST_CASTING_TIME +3%/level, 300 s, **the same effect id,
30184**.

| Id | Name | Item level | Skill level | Effect | Price | Local sources | Line |
|---|---|---|---|---|---|---|---|
| 164000132 | Lesser Awakening Scroll | 10 | 1 | +3% casting speed | 1200 | Alchemy 180 (155007165); Q2150 ×12 (selectable; no handler) | items:833111 |
| 164000133 | Awakening Scroll | 20 | 2 | +6% | 2000 | Alchemy 280 | items:833117 |
| 164000134 | Greater Awakening Scroll | 30 | 3 | +9% | 3000 | Alchemy 380; custom drop | items:833123 |
| 164000334 | Greater Awakening Scroll (second id) | 30 | 3 | +9% | 3000 | — | items:834294 |

### Movement (group 35, 15 s)

**Running:** skill 9960 (skills:92865), stack `ITEM_SPEED_RUN`, SPEED +10%/level, 300 s,
effect id 30182.

| Id | Name | Item level | Effect | Price | Local sources | Line |
|---|---|---|---|---|---|---|
| 164000074 | Lesser Running Scroll | 10 | +10% run speed | 1200 | Alchemy 180 (155006239); quests 2912 ×2 (lvl 10), 2351 ×3, 4317 ×5 and 4318 ×5 (lvl 24); Q2150 ×12 (no handler) | items:832781 |
| 164000075 | Running Scroll | 20 | +20% | 2000 | quests 2525 ×8 (lvl 32), 2545 ×3 (lvl 33) | items:832787 |
| 164000076 | Greater Running Scroll | 30 | +30% | 3000 | custom drop | items:832793 |
| 164000033 | Movement Speed Scroll | 15 | SPEED +600 flat for **600 s** (skill 9943, stack `ITEM_SCROLL_SPEED`, same effect id 30182) | 800 | none | items:832541 |
| 164000077/078/079 | Lesser / plain / Greater Raging Wind | 10/20/30 | Flight speed +10/20/30%, 5 min (skill 9961). Only for flight | 1200–3000 | Alchemy | items:832799 |

### Anti-Shock: damage shields (group 32, 60 s)

All tiers:
- 24 s duration; hit type EVERYHIT, so the shield takes physical and magic damage.
- dispel BUFF; cooldown id 126; effect id 154.
- Each tier has its own stack, `ITEM_SHIELD_ALL_20A` … `_60A`.
- The absorbed total equals the per-hit cap, so one big hit can use it all up
  (`AttackShieldObserver.java:99`).

| Id | Name | Item level | Restrict | Skill (`basiclvl`) | Absorbs | Price | Local sources | Line |
|---|---|---|---|---|---|---|---|---|
| 164000067 | Lesser Anti-Shock Scroll | 20 | — | 9953 (0) | 158 | 600 | **Q2206 "Slinking About" ×8 (Altgard, lvl 10;** `quest_data.xml:10456`); Alchemy 60 (155006229) | items:832739 |
| 164000068 | Anti-Shock Scroll | 30 | — | 9954 (10) | 245 | 1200 | Alchemy 160 | items:832745 |
| 164000069 | Greater Anti-Shock Scroll | 40 | — | 9955 (30) | 338 | 2000 | quests 2342 ×30 and 2758 ×15 (lvl 28), 2521 ×23 and 4512 ×8 (lvl 31); Alchemy 260 | items:832751 |
| 164000070 | Major Anti-Shock Scroll | 50 | — | 9956 (50) | 425 | 3000 | Alchemy 360 | items:832757 |
| 164000131 | Fine Anti-Shock Scroll | 60 | 50 | 9964 (70) | 550 | 4200 | — | items:833105 |

- **Tiers conflict by `basiclvl`.** All tiers share effect id 154, so a lower tier cannot
  replace an active higher tier: it gets the CONFLICT result.
- **The Cleric's own shields conflict too, from level 40:**
  - Blessed Shield (3912+, `basiclvl` 100) blocks the scrolls.
  - Immortal Shroud and Impervious Veil also use effect id 154.
  - Life Curtain (level 57, `basiclvl` 0) and a scroll replace each other.
  - Nothing a Cleric has at levels 10–39 conflicts with the scrolls.

### Defensive and stat scrolls (group 31, 15 s; 10 min unless stated)

| Id | Name | Item level | Skill (level): effect | Local source |
|---|---|---|---|---|
| 164000015 | Lesser Physical Defense Scroll | 10 | **none (inert here and in the retail client)** | none |
| 164000018 / 019 / 020 | Lesser Physical Attack / Resist Magic / Magical Attack Scroll | 10 | **none (inert)** | none |
| 164000016 | Lesser Accuracy Scroll | 10 | 9926 (1): accuracy +10 | none |
| 164000017 | Lesser Evasion Scroll | 10 | 9927 (1): evasion +10 | none |
| 164000032 | Tree Sap | 1 | 9924 (1): physical defense +10 | none |
| 164000022 | Blessing of Elim | 10 | 9924 (2): physical defense +20 | none |
| 164000039 | King of Beasts' Shield Scroll | 20 | 9924 (2): physical defense +20, stack `ITEM_SCROLL_DEFEND_PHYSICAL` | **none (no vendor, recipe, quest or drop)** |
| 164000035 | Sparkie Sap | 10 | 9925 (1): magic resist +5 | none |
| 164000042 | Mau Protective Amulet | 20 | 9927 (2): evasion +20 | none |
| 164000047 | Resist Magic Scroll | 15 | 9950 (1): all elemental resists +10 | none |
| 164000048–063 | Fire / Earth / Water / Windproof Scrolls, Lesser to Major | 10/20/30/40 | +20/40/60/80 to one element | Alchemy |
| 164000123–126 | Lesser to Major Strike Resist Scroll | 30/40/50/60 | 9966: crit-strike resist +10/20/30/40, **5 min** | Alchemy |
| 164000127–130 | Lesser to Major Spell Resist Scroll | 30/40/50/60 | 9967: crit-spell resist +10…40, 5 min; the same effect id (30321) as Strike Resist, so the two replace each other | Alchemy |

The Strike Resist scrolls are **not shields.** Appendix D lists them under "Defense shield";
remove them from there.

### Critical scrolls (group 36, 15 s; 5 min)

| Ids | Name | Item level | Effect |
|---|---|---|---|
| 164000064/065/066/118 | Lesser, plain, Greater and Major Crit Strike | 30/40/50/60 | Physical crit +30/60/90/120 (skill 9957) |
| 164000119/120/121/122 | Lesser, plain, Greater and Major Crit Spell | 30/40/50/60 | Magical crit +10/20/30/40 (skill 9958) |

The two families share conflict id 31 and effect id 30171, so only one is active at a time.

### HP and MP recovery (group 11)

- "HoT" (heal over time) means one heal every 2 s for 20 s, ×10. Those rows are written as
  instant + tick, followed by the total.
- The elixirs' HoT stack is dispel BUFF; the serums are instant.
- HP and MP items share group 11. After an **elixir** (60 s delay), no potion of any kind
  can be used for 60 s.

| Family (source) | Delay | Level 10 | Level 20 | Level 30 |
|---|---|---|---|---|
| Life Elixir (**vendor**) | 60 s | 162000052: 31+31 HoT = 341 | 162000053: 56+56 HoT = 616 | 162000054: 81+81 HoT = 891 |
| Mana Elixir (**vendor**) | 60 s | 162000057: 50+50 HoT = 550 MP | 162000058: 82+82 HoT = 902 MP | 162000059: 107+107 HoT = 1177 MP |
| Life Serum (drop, 2.5% "Potions (Rare)", `rules_commons.xml:112`) | 30 s | 162000012: 340 | 162000013: 610 | 162000014: 890 |
| Mana Serum (drop) | 30 s | 162000017: 540 MP | 162000018: 900 MP | 162000019: 1170 MP |
| Panacea of Life / Mana (drop) | 60 s | 162000087 / 093: 270 HP / 440 MP | 088 / 094: 490 / 720 | 089 / 095: 720 / 940 |
| Life Potion / Mana Potion (crafted; starter item) | 30 s | 162000002 / 007: 37+37 HoT = 407 HP; 59+59 HoT = 649 MP | 003 / 008: 737 HP / 1078 MP | 004 / 009: 1067 HP / 1408 MP |
| Recovery Potion (HP and MP HoT) | 30 s | 162000041: 407 HP + 649 MP | 042 | 043 |
| Recovery Serum (legendary; instant HP and MP) | 30 s | 162000046: 340 HP + 540 MP | 047: 610 + 900 | 048: 890 + 1170 |

None of these has a `restrict` or a race limit. Item lines are items:830718–831312.

Other potions:
- 162000022 Lesser Healing Potion (vendor, 350, group 12 at 30 s) removes physical
  debuffs.
- 162000024 Lesser Wind Serum (group 13) restores flight time.

### Food (one stat food and one regen food at a time)

- Stat foods use conflict id 22 and delay group 22; regen foods use conflict id 21 and group
  21. Both groups have a 5 s delay.
- A new food of the same conflict id ends the old one (`EffectController.java:116`).
- The vendor foods below all last 15 min. Crafted foods (for example 160001002 Savory
  Liguri, +20 magic boost) last 30 min.

| Buff | Level 10 (Asmodian vendor, 200) | Level 20 (vendor, 400) |
|---|---|---|
| Magic boost | 160003502 Savory Micory +10 | 160003512 Savory Tikel +20 |
| Max HP | 160003503 Broiled Brax +40 | 160003513 Broiled Pluma +80 |
| Max MP | 160003504 Umeru Omelette +40 | 160003514 Horto Omelette +80 |
| Physical attack | 160003501 Roast Brax +2 | 160003511 Roast Slink +4 |
| MP regen (conflict 21) | 160003510 Neira Juice +3 | 160003520 Raydam Juice +6 |
| HP regen (conflict 21) | 160003509 Brax Soup +2 | 160003519 Mosbear Soup +4 |

- The foods are at items:827271–827385.
- Rally Serums 160003551–556 (HP and MP regen, conflict 21) have no source.
- No level 30 vendor food exists for Asmodians. The level 20 tier stays current until 40.

### DP, Tea of Repose and powder

| Id | Name | Item level | Restrict | Effect | Group | Source |
|---|---|---|---|---|---|---|
| 160002273 | Zeller Aether Jelly | 40 | **10** | DP +2000 (skill 10164, level 2) | 23, 30 min | Quests 2901–2904 and 29070/29071 "Dispatch to Altgard" ×5 each (lvl 10); 2707 ×15 (lvl 25). items:825732 |
| 160005061 | Zeraka Aether Jelly | 10 | 10 | DP +2000 (skill 11011) | 148, 15 min | none (an event copy, 160005060, exists) |
| 162001012–023 | Tea of Repose (1–100%, Random, Large, Regular) | 5–10 | 10 | Restores Energy of Repose (the XP bonus), not HP | 140, 2 s | Cash/event only |
| 169300003 | Lesser Odella Powder | 10 | 10 | Reagent (15 kinah, stack 10000) | — | Vendors; Q2211 "Karnif Threat" ×45 (lvl 11) |
| 169300004 | Odella Powder | 20 | 20 | Reagent (30) | — | Vendors (Morheim and later) |
| 169300005 | Greater Odella Powder | 30 | 30 | Reagent (45) | — | Vendors |

Salvation needs 2000 DP, and one jelly supplies exactly that.

**Which Cleric skills use which powder:**
- **Herb Treatment** uses 1 powder per cast; **MP Recovery** uses 2.
- Ranks learned at 10, 15 and 20 use Lesser Odella Powder: 246/247/251 and 249/250/252.
- Ranks learned at 25 and 30 use **Odella Powder**: 253/254 and 297/298. Sources:
  `skill_tree.xml:308–420` and skills:3085–3203 and 3664–3687.
- **The powder in the kit must change at level 25.**

### Resurrection, return and Kisks

- **Self-resurrection** (Java `Player.getSelfRezStone`, `Player.java:1398`):
  - The stone is chosen in this order: 161001001 Revival Stone (level 1, group 72, 30 min)
    → 161000003 Reviving Elemental Stone (level 25, group 53, 60 min) → 161000004
    Tombstone of Revival (level 25; Abyss-point vendors such as Dein 204272, `npc_type="ABYSS"`).
  - It revives in place at 15% HP and MP (`PlayerReviveService.java:215–234`). The item
    level is not checked.
  - None is sold for kinah at levels 10–30.
- **164000010 Elemental Stone of Resurrection** (level 20, 1000, target must be a PC,
  group 54) only raises *another* player. It is useless solo.
- **Return scrolls:**
  - 164000089 Pandaemonium (1500), 164000090 Altgard Fortress (1500), 164000091 Morheim
    Fortress (2500) and 164000092 Beluslan Fortress (4500) all have restrict 10.
  - Each has a 5 s cast, needs you on the ground, and uses group 56 (15 s).
  - Sources: quest 80515 "[Growth]" ×10 each (lvl 10); Q2244 ×2 (Altgard scroll); Q2923
    and Q2924 ×8 (Morheim scroll).
- **Kisks:**
  - 184000021 (A) Personal Kisk: 30,000, 10 s cast, group 50 (30 min), sold only in
    Enshar.
  - 184000022 (A) Group Kisk: sold widely.
  - A solo bot rarely benefits.

### Event, cash and timed variants (do not recommend)

- Legion Reward, level 1: 164001001–005.
- [Event]: 164002002–006, 010, 011 and 056–059.
- [Stamp]: 164000327–330. [Abbey]: 164000337–339.
- [Blackstar]: 164000345–348 and 164002296–298.
- Timed 1 hour and 10 min: 164002111–113 and 235–237. They use `CASH_ITEM_SPEED_*`
  stacks, but the effect ids are the same, so they still replace the normal scrolls.
- Arena-only: 164000154–159 and 189–191. The Abbey return stones, Tea of Repose,
  Infinite Bottles and candies are also excluded.
- These are variants of the families above and add nothing a supply list needs.

## Where to buy (Asmodian vendors by zone)

- The price is the template price (Appendix A). Npc ids come from `npc_trade_list.xml`;
  the lists come from `goodslists.xml`.
- The retail client's own goods lists also contain the vendor potions, powders and foods.
  They contain **none** of the scroll families.

| Zone | NPC (id, retail title) | Sells |
|---|---|---|
| Ishalgen | Denma 203542 and Crizpinerk 798038 (General Goods, list 721) | Minor Life and Mana Elixir |
| Ishalgen | Bacorerk 798037 (lists 274/275; the client titles him Cube Artisan) | Minor and Lesser Elixirs, Lesser Odella Powder |
| Ishalgen | Ungfu 203526 (Food) | Level 10 foods |
| Altgard | **Nirmirn 203576** (Potion Merchant, list 275) | Minor and Lesser Life and Mana Elixir |
| Altgard | **Donabe 203579**, Bergard 203615 and Japayerk 798030 (General Goods) | Lesser Odella Powder, Elemental Stone of Resurrection. Donabe also sells the Altgard Fortress Scroll; Bergard and Japayerk also sell elixirs |
| Altgard | Sarad 203657, Loriniah 203605 | Level 10 foods |
| Altgard | Gilungk 203613 (Food) | Level 20 foods |
| Pandaemonium | Nekai 204202 (General Goods) | Powders (10/20/30), the four fortress scrolls, Elemental Stone of Resurrection, Group Kisk |
| Pandaemonium | Maochinicherk 798068 (Potion Merchant) | Elixirs (levels 10–30), Lesser Healing Potion, Lesser Wind Serum |
| Pandaemonium | Kitanya 204128, Dorein 204129 | Level 10 and 20 stat foods |
| Pandaemonium | Jeckrow 204173, Malfaisante 204177 | Regen foods |
| Morheim | Bicorunerk 798082, Sarinerk 798080, Chicorerk 798106 | Elixirs (levels 10–30), Healing Potion |
| Morheim | Nicoyerk 798081 (General Goods), Grammati 790011, Aprily 204335, Liurerk 798109 | Powders (10/20/30), Morheim Fortress Scroll, Group Kisk |
| Morheim | Otis 204375, Valumcus 790012 | Level 10 and 20 foods |
| Beluslan | Mundilfari 204730 | Elixirs (levels 10–30), Healing Potion, Wind Serum |
| Beluslan | Lapion 204728, Baba Hun 204792, Reyrinirerk 798090, Kamochinicherk 798094 | Powders, Beluslan Fortress Scroll, Group Kisk |
| Beluslan | Aurvandil 204731, Furiga 204737, Baba Kun 204791 | Foods |

**Sold nowhere for kinah:**
- Courage, Awakening, Running, Raging Wind, Anti-Shock, Crit and Resist scrolls.
- Life and Mana Serums. The only vendor is Kampechi 833008, a token trade-in in Gelkmaros.
- Jellies, Tea of Repose, self-resurrection stones.

## Data bugs and divergences

| Item(s) | Finding | Java? | Retail client? | Verdict |
|---|---|---|---|---|
| 164000015 Lesser Physical Defense Scroll | No `<actions>` (items:832448) | Same | No `activation_skill`; unfilled tooltip template; no recipe or goods source | **Retail-inert. Not a port bug. Do not "fix".** |
| 164000018/019/020 | No actions | Same | Same | Retail-inert (the same 1.x left-overs) |
| 164000011–014 (elemental stones), 030, 034, 038, 083, 084 | No actions | Same | No skill | Inert in retail. Some are quest-use items |
| 164000039, 022, 032, 033, 042, 047 | They work, but no vendor, recipe, drop, quest or handler produces them, in Java or C# | Same | Not in any client goods list | Obtainable only by supply. Fine as supplied items |
| 164006003/006/009 test scrolls | The client has a skill; the server has no action | Same | — | Test data. Ignore |
| 164000033 Movement Speed Scroll | The server adds SPEED +600 (flat; the unit is 1/1000 m/s). The client stores 6 (flat) and aioncodex shows "6%" | Same | Scaling differs | Shared upstream conversion; magnitude unverified. Low priority |
| Anti-Shock per-hit cap | Server: `hitvalue + hitdelta × 1`, equal to the total (158/245/…) | Same | The client adds delta to both; aioncodex shows 245/245 | Consistent |
| Q2150 "Brotherly Love" (Ishalgen; 12 Courage or 12 Awakening, plus 12 Running) | Quest data exists; no handler (`quest_script_data/ishalgen.xml:51` "[TODO]") | Same TODO | Retail had it | Known parity gap (`natural-ishalgen-journey.md:69`). This is the natural source a level 3+ Asmodian would have had |
| Appendix D (this repo) | Five errors: Anti-Shock item levels and effect; Strike Resist listed as a shield; the "Courage + Awakening both up" claim; the "check whether Running and Movement Speed add" note; the shield rule's stack and group (Anti-Shock uses stacks `ITEM_SHIELD_ALL_*` and group 32, not `ITEM_SCROLL_DEFEND_PHYSICAL` / 31) | — | — | Doc errors. Correct them when NA-20 lands |
| Appendix A | "Every potion needs an equipped weapon" | — | — | Wrong: item skills bypass `WeaponCondition` (Java line 30, C# `WeaponCondition.cs:21`) |

The C# side of the skill-stacking and item-use paths was also checked. It mirrors Java:
- `EffectController.cs:79–178` (conflicts by effect id, conflict id and cooldown id);
- `PlayerRestrictions.cs:329`;
- `CM_USE_ITEM.cs:70` (cancelling the current cast);
- `SkillUseAction.cs:55`.

## Retail 4.8 cross-check (with URLs)

**Blocked sources.** aion.fandom.com returned 402, aionpowerbook.com and forum.euroaion.com
returned 403, and web.archive.org could not be fetched. So the web evidence below is
aioncodex's 4.x database and the 2010 ZAM wiki. The retail client data (above) is the
stronger evidence.

| Claim | Web says | Local data | URL |
|---|---|---|---|
| 164000015 in 4.x | Tooltip is the unfilled `[%e1.StatUp…]` template; no skill; no sold-by, recipe, quest or drop | Inert (agrees) | https://aioncodex.com/4x/item/164000015/ |
| 164000018 (a sibling) | Same unfilled template, no sources | Inert (agrees) | https://aioncodex.com/4x/item/164000018/ |
| Lesser Anti-Shock | Shield absorbing 158 per attack, up to 158 total; 1 min reuse; 600 kinah | 158/158, group 32 at 60 s (agrees) | https://aioncodex.com/4x/item/164000067/ |
| Anti-Shock | 245 per attack, 245 total; 1 min | 245 (agrees) | https://aioncodex.com/4x/item/164000068/ |
| Anti-Shock in 2010 | An absorption shield (quest reward); the Major tier absorbs 425 | A shield since 1.x (agrees) | https://legacy.fanbyte.com/wiki/Anti-shock_Scroll_(Aion_Item) |
| Lesser Courage | Attack speed +3% for 5 min, 15 s reuse, 1200 kinah | Agrees | https://aioncodex.com/4x/item/164000071/ |
| Greater Courage | +9% for 5 min | Agrees | https://aioncodex.com/4x/item/164000073/ |
| Lesser Awakening | Casting speed +3% for 5 min, 15 s | Agrees | https://aioncodex.com/4x/item/164000132/ |
| Lesser Running | Speed +10% for 5 min, 15 s | Agrees | https://aioncodex.com/4x/item/164000074/ |
| Movement Speed Scroll | "Speed by 6% for 10m", 15 s | +600 speed, 10 min (see the table above) | https://aioncodex.com/4x/item/164000033/ |
| King of Beasts' Shield | Physical defense +20 for 10 min; no sources listed | Agrees; no source | https://aioncodex.com/4x/item/164000039/ |
| Lesser Life Serum | Restores 610 HP; 30 s | Agrees | https://aioncodex.com/4x/item/162000013/ |
| Lesser Life Elixir | 56 HP every 2 s for 20 s; 1 min | Server and client also add a 56 instant heal (the tooltip omits it) | https://aioncodex.com/4x/item/162000053/ |
| Zeller Aether Jelly | DP +2000, 30 min reuse, level 10+, quest reward | Agrees | https://aioncodex.com/4x/item/160002273/ |
| Courage crafting | Asmodian Alchemy recipe at 180 points | Agrees (155006236) | https://aioncodex.com/us/recipe/155006236/ |
| Lesser Physical Defense on ZAM | The page does not exist | — | https://legacy.fanbyte.com/wiki/Lesser_Physical_Defense_Scroll_(Aion_Item) |

**Retail stacking of Courage and Awakening:** no web source found. The retail 4.8 client
gives both skills effect id 30184 in the Buff slot, the same pair of values the server's
retail-modelled conflict rule uses. Retail design also offers them as a *choice*: Q2150's
selectable reward is 12 of one or 12 of the other.

## Recommendations for the bot

### (a) Tier rule

`restrict` is absent on all of these scrolls and potions, so any tier works at level 10.
"Usable" is not "intended". Rule:

- **Courage, Awakening, Running, potions and food:** use the highest tier whose **item
  level ≤ character level**. This matches the tier names (Lesser 10, plain 20, Greater 30)
  and retail quest rewards, which hand these tiers out at or above their item level. For
  potions, the bot may still *buy* an above-level vendor item itself, since no `restrict`
  stops it. Appendix A's Lesser Life Elixir at level 10 is natural.
- **Anti-Shock:** use the highest tier whose **item level ≤ character level + 10.** The
  family starts at item level 20, and retail rewards it about 10 levels early: Q2206 gives
  Lesser (20) at level 10, and Q2342/2521/2758/4512 give Greater (40) at 28–31. Carry one
  tier at a time. A lower tier cannot replace an active higher one.
- **Powder follows the learned skill rank,** not the item level (see above).

| Level | Speed scroll (Courage / Awakening) | Running | Anti-Shock | HP (serum / elixir) | MP (serum / elixir) | Powder | Food (stat / regen) |
|---|---|---|---|---|---|---|---|
| 10 | Lesser 071 / 132 (+3%) | Lesser 074 (+10%) | Lesser 067 (158) | Minor Serum 012 (340) / Minor Elixir 052 | Minor 017 (540) / 057 | Lesser 169300003 | 160003502 or 504 / 510 |
| 15 | Lesser | Lesser | Lesser | Minor | Minor | Lesser | Level 10 foods |
| 20 | plain 072 / 133 (+6%) | plain 075 (+20%) | **plain 068 (245)** | Lesser 013 (610) / 053 | Lesser 018 (900) / 058 | Lesser | 160003512 or 514 / 520 |
| 25 | plain | plain | plain | Lesser | Lesser | **Odella 169300004** | Level 20 foods |
| 30 | Greater 073 / 134 (+9%) | Greater 076 (+30%) | **Greater 069 (338)** | Life Serum 014 (890) / 054 | Mana Serum 019 (1170) / 059 | Odella 169300004 | Level 20 foods |

Ids are abbreviated to their last three digits within 164000xxx or 162000xxx.

### (b) Starter kit for levels 10–20 (about a 4-hour run) and restock

A 5-minute buff kept up for 4 hours needs 48 uses. About +25% covers refreshes before
expiry, deaths and relogs.

| Item | Id at 10–19 | Id from 20 | Start | Restock: top up to N when below M |
|---|---|---|---|---|
| Speed scroll (**one family only**, see (c)) | 164000132 or 164000071 | 164000133 or 164000072 | 60 | 60 / 15 |
| Running | 164000074 | 164000075 | 20 | 20 / 5 |
| Anti-Shock | 164000067 | 164000068 | 30 | 30 / 8 |
| Life Serum | 162000012 | 162000013 | 30 | 30 / 10 |
| Mana Serum | 162000017 | 162000018 | 40 | 40 / 10 |
| Lesser Odella Powder | 169300003 | 169300003 (Odella 169300004 from 25) | 200 | 200 / 50 |
| Zeller Aether Jelly | 160002273 | 160002273 | 8 | 8 / 2 (one per 30 min) |
| *Optional:* Revival Stone | 161001001 | 161001001 | 3 | 3 / 1 |

- The foods are cheap and on the route (Ungfu in Ishalgen; Sarad, Loriniah and Gilungk in
  Altgard). Let the bot **buy** them rather than supply them: 16 of each per 4 h, one stat
  food and one regen food.
- The elixirs are bought from Nirmirn as today.
- The Anti-Shock count assumes at most 1 per minute (the delay), used only at ≤50% HP, so
  about one per 3–4 fights.
- Powder assumes Herb Treatment (1) plus MP Recovery (2) on most rests.

**Restock rule.**
- Check stock at run start, after every level-up, at every town visit and at each profile
  checkpoint.
- Top each allowlisted id up to N when it is below M.
- On a tier change (20, 25, 30), supply the new tier and let the old stock run out. The bot
  always uses the highest tier it owns that the rule allows.

### (c) Courage and Awakening

- They share group 34, and each use **ends the other** (the same effect id, 30184). Keep
  **one** family up; never alternate, or every 15 s swap wastes a scroll.
- For a Cleric, **Awakening** is the better one:
  - Smite (1.5 s), Healing Light (2 s), Earth's Wrath (1.5 s) and Light of Resurrection all
    carry `apply_casting_time_bonus="true"` (the Java `Skill.java:374–400` path).
  - Herb Treatment and MP Recovery do not.
  - Attack speed mainly shortens the Cleric's mace auto-attack.
- The gain is small either way (3% at Lesser, 9% at Greater).
- If the operator specifically wants attack speed, use Courage and never Awakening.

### (d) Other helpful consumables for a solo Cleric

- **Mana Serums** are the biggest help: MP gates a Cleric's damage, and they are instant
  with a 30 s delay.
- One **stat food**: Savory Micory/Tikel for damage, or Umeru/Horto Omelette for MP pool.
  One **regen food**: Neira or Raydam Juice.
- **Zeller jelly** for Salvation's DP.
- The **Altgard and Morheim Fortress Scrolls** can shorten restock trips (5 s cast; stop
  moving first).
- **Crit Spell** from level 30 (magical crit +10 for 5 min) is optional.
- Skip Tea of Repose in combat (OD-8), the Kisks, the Elemental Stone of Resurrection
  (others only) and the candies (transforms can block item use).

### (e) Interactions the NA-19 policy must encode

- **Delay groups:**
  - 11: every HP and MP potion. 30 s for potions and serums, 60 s for elixirs and
    Panaceas; the item used sets the lockout.
  - 31: stat scrolls. 32: Anti-Shock (60 s). 34: Courage and Awakening. 35: Running,
    Raging Wind and Movement Speed. 36: Crit.
  - 21 and 22: foods. 23: jellies. 56: return scrolls.
- **Buffs that replace each other:**
  - Courage and Awakening (30184).
  - Running, the Movement Speed Scroll and the Worg/Karnif souls (30182).
  - Crit Strike and Crit Spell (30171 and conflict 31).
  - Strike and Spell Resist (30321).
  - Food of the same conflict (21 or 22).
  - Anti-Shock tiers, and from level 40 the Cleric's Blessed Shield (154, by `basiclvl`).
- **Detect an active shield by any `ITEM_SHIELD_ALL_*` stack,** not by one stack name.
- **Using an item cancels the current cast** (`CM_USE_ITEM.java:76`). Use items between
  casts only.
- **Items with a cast time cannot start while moving.** These are the return scrolls (5 s),
  the Kisks (10 s) and the resurrection stones (5 s) (`Skill.java:171`). Instant scrolls
  and potions have no such check server-side.
- **Item use is blocked** while dead or dying, in any CANT_ATTACK_STATE (stun, sleep and
  so on), while transformed, and during the item's own cooldown
  (`PlayerRestrictions.java:286–309`). Return scrolls also need the ground.
- **No weapon is needed** for any consumable.
- `gameserver.items.ignore_potions_at_full_health` is `false`, so a potion used at full HP
  or MP is wasted.

## Supply mechanisms

- **SIM.** The Mau course fixture removes the inventory and then calls
  `ItemService.AddItem(player, id, count, allowInventoryOverflow: true)`
  (`tests/Aion.Simulation.Tests/SimulationMauCourseTests.cs:121–126`). The same call,
  driven by an allowlist, is the simplest SIM supply. `SimulationGmFacade`
  (`tests/Aion.Bots/Gm/GmFacade.cs:67–115`) can run the real `//add` handler in process,
  but it calls `Execute` directly, so no gmaudit line is written. The audit in SIM is
  therefore the run trace and profile.
- **LIVE, recommended.**
  - Use the seeded **director** account (`GmFacade.cs:124`) and the admin command
    **`//add <player> <itemId> [count]`** (`src/Aion.GameServer/Handlers/AdminCommands/Add.cs:17–68`).
  - The command needs access level **8** (`game-server/config/administration/commands.properties:7`).
  - A count may be at most 126 stacks, so any kit here fits.
  - `LiveQuestPlanScenario.cs:194–195` already does this through `gm.ExecuteAsync(new
    GmCommand("add", [name, id, "1"], "You gave"), …)`.
  - It leaves three records: the director's trace step; the server's gmaudit line
    `[Admin Command] > [Player: Director][Target: …]: //add …` (`AdminCommand.cs:43`,
    `logging.properties:28`); and the subject's "You received N x item from Director"
    message. **This is the cleanest audit trail.**
- **Mail.**
  - `//sysmail <recipient> <0|1|2 letter type> <itemId> <count> <kinah> [title] [message]`
    (`SysMail.cs`, access 8, `commands.properties:96`).
  - It sends one stack per letter, and the mail is kept in the database.
  - The bot would need a mailbox-collection behavior.
  - Not recommended.
- **Database insert before login** (isolated stack only): it bypasses server logic and
  leaves no in-game audit. Last resort. Never touch the operator's `aion` stack.

## Sources

**Local data (C# = Java):**
- `game-server/data/static_data/items/item_templates.xml` and `skills/skill_templates.xml`
  (lines as cited).
- `goodslists/goodslists.xml` (lists 118, 274, 275, 399, 721 and 1505 at lines 12325,
  16890, 16904, 20106, 27988 and 47260).
- `npc_trade_list.xml` (203526 at :372, 203542 :375, 203576 :379, 203579 :395, 203613
  :403, 203657 :416, 204202 :757, 798038 :2209, 798068 :2247).
- `quest_data/quest_data.xml` (Q2150 :10402, Q2206 :10456, Q2211 :10494, Q2901 :17347),
  `recipe/recipe_templates.xml` (46927, 46976, 46997, 54976),
  `global_drops/rules/rules_commons.xml:112`, `custom_drop/custom_drop.xml:1856–1858`,
  `skill_tree/skill_tree.xml:308–420`, `quest_script_data/altgard.xml:175/212`.

**Java** (`../aion-server/game-server/src/com/aionemu/gameserver/`):
- `restrictions/PlayerRestrictions.java:277–362`
- `model/templates/item/ItemTemplate.java:85,172–186`
- `model/templates/item/actions/SkillUseAction.java`
- `skillengine/condition/WeaponCondition.java:30`
- `skillengine/model/Skill.java:129–137,171,374–400`
- `skillengine/effect/BufEffect.java:74`, `ShieldEffect.java:39`,
  `skillengine/model/Effect.java:903`
- `controllers/effect/EffectController.java:39–157,193–240`
- `controllers/observer/AttackShieldObserver.java:41–100`
- `network/aion/clientpackets/CM_USE_ITEM.java:76–82`
- `model/gameobjects/player/Player.java:988–1021,1398–1427`
- `services/player/PlayerReviveService.java:215–234`

**C#:**
- `src/Aion.GameServer/Controllers/Effect/EffectController.cs:79–178`
- `Restrictions/PlayerRestrictions.cs:329`
- `Network/Aion/ClientPackets/CM_USE_ITEM.cs:70–90`
- `SkillEngine/Condition/WeaponCondition.cs:21`
- `Handlers/AdminCommands/Add.cs`, `SysMail.cs`
- `Utils/ChatHandlers/AdminCommand.cs:43`
- `tests/Aion.Bots/Gm/GmFacade.cs`, `tools/Aion.LiveBots/LiveQuestPlanScenario.cs:194`

**Retail 4.8 client (read-only):**
- `Data/Items/Items.pak` (`client_items_etc.xml`, `client_items_misc.xml`,
  `client_combine_recipe.xml`), `Data/skills/skills.pak` (`client_skills.xml`),
  `Data/Npcs/Npcs.pak` (`client_npc_goodslist.xml`, `client_npcs_npc.xml`) and
  `l10n/ENG/Data/Data.pak` (strings).
- All decoded with `tools/client-extract/aionpak.py` and `bxml.py`.

**Web:** the URLs in the cross-check table.
