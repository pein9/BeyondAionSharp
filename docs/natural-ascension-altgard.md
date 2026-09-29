# Natural Ascension bridge: Ishalgen to Altgard as a Cleric

Status (2026-09-28): **planned. NA-00 is done and the operator decisions are answered.**

This is the Ascension bridge. It takes the natural player bot from the end of
[the Ishalgen journey](natural-ishalgen-journey.md) to the start of Altgard. The list is
worked one TODO at a time in Loop mode; read
[How to work this list](#how-to-work-this-list-loop-protocol) first.

**Why this exists.** The long-term goal is a bot that playtests the whole game, from
account creation to endgame, including **all quests**. Ishalgen (41 quests) was the first
area. This bridge covers Ascension only: it gets the character from Ishalgen to Altgard as
a Cleric, bound, geared and restocked. Altgard's own quests are the next milestone; see
[After the bridge](#after-the-bridge).

**Prove it once.** The bridge is an intermediate step that sets up the next real bot runs.
- Each development item is verified by **one** snapshot run.
- Acceptance is:
  - one clean run starting from the Munin snapshot;
  - one full SIM run from character creation;
  - one isolated LIVE run.
- A failed run is fixed and rerun, and the retry is recorded. There are no multi-seed
  batches.
- The endpoint is saved as an **Altgard start snapshot**, so later milestones start there
  instead of replaying Ishalgen and the bridge.

**Deaths are not failures (operator rule, 2026-09-28).**
- We are not trying to reach "zero deaths". A death usually means a patrol arrived at the
  wrong moment. The bot revives, recovers and carries on, and the trace records the death.
- A run fails only if:
  - an objective is not met;
  - the bot stalls;
  - it exhausts a bounded recovery;
  - the server misbehaves.
- The Cleric may **wait 15 seconds and path again**, or **decide to take on a patrol**. It
  has far more healing and buffs than the Priest had. See NA-22.

**Help items.** The bot keeps itself buffed with scrolls:
- the correct **Awakening** (casting-speed) scroll, always (OD-15);
- an **Anti-Shock** scroll at 50% HP (the level-appropriate tier). Despite its name, it is a damage shield;
- running scrolls on long journeys.

The consumables may be supplied to the bot directly (by "cheating"). NA-20 explores what
can be added that way, and NA-19 adds a **"buff ourself"** check. Supplied items are
recorded in every run's profile, so a run with help items is never mistaken for one
without.

**Authority and spec.** Decision **D25** in [the e2e plan](e2e-player-simulation-plan.md)
authorizes this leg. Every step below comes from the Java spec in `../aion-server` at
`ce54b7931`. The C# code for these parts was compared with it and matches:
- the quest handlers;
- class change;
- the instance;
- teleport and bind;
- trading;
- skill effects and cutscenes.

This is bot work; see [Readiness](#readiness-what-exists-and-what-is-missing).

## Goal

The retained natural Priest finishes Ishalgen at Munin. Per the NI contract, that means 41
quests done, level 9, and Q2008 at START/0. From there, using only ordinary client actions:

1. **Ascension (Q2008).** Talk to Munin and complete Q2008, choosing **Cleric**. This
   covers the three Norns, the Ataxiar instance, Hagen's flight and the scripted trial
   fight.
2. **The ceremony (Q2009).** Take Munin's Q2009 teleport to **Pandaemonium** and finish
   "A Ceremony in Pandaemonium".
   - Choose the **Karmic Staff**.
   - The reward makes the character level 10.
3. **Dispatch to Altgard (Q2904).** Complete the Priest-born version of this quest.
   - It starts by itself when Q2009 completes.
   - Doman in Pandaemonium advances it.
4. **Arrive in Altgard.** Travel there by Doman's teleporter.
   - **Bind at the Altgard Fortress obelisk (700065).**
   - Turn in Q2904 at Meiyer.
   - Complete **Q24010 "Suthran's Orders"**, the first Altgard mission. It starts when
     the character enters Altgard.
5. **Stop at the Altgard shops.**
   - **Equipment:** equip the staff and any unworn gear or accessory from earlier quests.
   - **Sell:** replaced gear, old loot and junk.
   - **Buy:** potions only (Lesser Life Elixirs). **No gear is bought.**
6. **Watch or skip the four quest movies** (57, 152, 121 and 122) the way a client does.
7. **Start the level 10 Cleric work.**
   - Teach the bot its new skills, with powder-based rest (see
     [Appendix C](#appendix-c-level-910-cleric-skills)).
   - Add a "buff ourself" check and help-item scroll use (see
     [Appendix D](#appendix-d-help-items-scrolls)).
   - Add patrol handling: wait and retry, or fight.

### Endpoint contract (what "done" means)

**Quest and character state**
- Q2008, Q2009, Q2904 and Q24010 are complete in the client-observed completed journal.
- Q2009 was completed with reward group 3 and **REWARD2**, the Karmic Staff 101500498.
- Class is **Cleric** (id 10). The character is a Daeva.
- The character is **at least level 10**, which Q2904 requires. Exact XP is not tracked:
  after Ascension there is no level cap.
- The bind point is 700065 on map 220030000. `SM_BIND_POINT_INFO` was observed and
  451 Kinah was charged.

**Equipment**
- The **Karmic Staff is equipped** in the main hand; it is two-handed.
- Every owned gear piece and **accessory** that the Cleric can use and that beats what is
  worn in its slot is equipped. Those slots include:
  - ring ×2, earring ×2, necklace, belt, head;
  - the armor slots.
- The Ishalgen accessories must end up worn:
  - Spirit Ring 122000869 (Q2004);
  - Black Opal 121000749 (Q2119);
  - Munin's Belt 123000864 (Q2123, the alternative to Munin's Hat 125001757).

**Shop stop**
- Replaced gear (at least the Aldelle Mace), old loot and junk were sold.
- Nothing protected was sold.
- HP potion stock was refilled toward the target with Lesser Life Elixirs.
- No gear was bought.
- Every Kinah change matches an observed price.

**Final checks and hand-off**
- The character is alive, out of combat, and standing inside Altgard Fortress.
- After a relog, the same class, level, quests, bind point, equipment, inventory and
  position are observed.
- Every step, movie, decision, death and recovery is in the bot trace.
- No GM input touched the character, apart from any help items approved under OD-13. Those
  are listed in the run profile.
- Deaths are counted and reported, but they are not a failure (OD-12).
- The endpoint state is saved as the Altgard start snapshot (SIM, and the isolated LIVE
  run's database).

The Ishalgen leg's contract (`parity-artifacts/e2e/natural-ishalgen-contract.json`) stays
**frozen** as the regression baseline. This leg starts exactly where it ends.

## Operator decisions

| # | Question | Answer | Status |
|---|---|---|---|
| OD-1 | Class at Ascension | **Cleric** (SETPRO14). This supersedes the older "intended to become a Chanter" note. | Decided 2026-09-28 (D25) |
| OD-2 | Include Q24010 at Suthran? | **Yes.** The bridge ends with Altgard's first mission done. Q24011 needs level 11 and belongs to the Altgard milestone. | Decided 2026-09-28 |
| OD-3 | Commit policy while the loop runs | **Commit each TODO on `main`** once its verification passes, with its evidence line in this doc. Never push. | Decided 2026-09-28 |
| OD-4 | LIVE acceptance subject | **An isolated LIVE stack with a fresh Priest** (NI-09 style, its own compose project). The run covers creation, all of Ishalgen and the bridge, about 4.5 hours. The operator's `aion` world and the retained `Ishalgenbot` are **not** used. | Decided 2026-09-28 |
| OD-5 | Q2009 weapon | **Karmic Staff** (101500498, two-handed, 58–88 damage, magic boost 260), taken with REWARD2 (9). | Decided 2026-09-28 |
| OD-6 | Optional Pandaemonium quests (e.g. 2911 "Song of Blessing", craft quests) | Not part of the bridge. The all-quests goal picks them up in a later capital pass. | Default |
| OD-7 | Buy gear at the shop stop? | **No. Only potions are bought.** Equip what the character already owns, and sell what is replaced. | Decided 2026-09-28 |
| OD-8 | Drink Tea of Repose? | Yes, once, out of combat, at level 10 in Altgard. It is an XP buff only. | Default |
| OD-9 | Buy Lesser Odella Powder (15 Kinah; the reagent for the Cleric's Herb Treatment and MP Recovery)? | **Yes.** It is a mana-recovery consumable, so it becomes part of the **rest** routine: MP Recovery and Herb Treatment replace most sitting (NA-18). | Decided 2026-09-28 |
| OD-10 | Movies: watch or skip? | **Skip by default**, the way a player who presses Esc does. That is the bot's current behavior (an immediate `CM_PLAY_MOVIE_END`). A `watch` mode that holds for the movie's length exists for real-client observation (NA-28). | Default |
| OD-11 | How many runs prove it? | **One each**: one snapshot run per development item, then one clean snapshot run, one full SIM run and one isolated LIVE run for acceptance. | Decided 2026-09-28 |
| OD-12 | Are deaths failures? | **No.** "Zero deaths" is not a goal. A death is recorded and recovered from, and is never a pass/fail criterion by itself. What fails a run: an unmet objective, a stall, an exhausted bounded recovery, or a server defect. | Decided 2026-09-28 (rule for all natural runs from now on) |
| OD-13 | Help items by cheating | **Allowed, after exploration.** NA-20 proposes the consumables and the supply mechanism. Only consumables the operator approves are supplied (no gear, quest items, XP or levels), and every run lists them in its profile. This amends D25's "no GM input" rule for those items only. | Decided in principle 2026-09-28; the item list needs approval at NA-20 |
| OD-14 | Patrols in the way | **Cleric:** wait 15 s (game time) and path again; or decide to take on the patrol when the fight is winnable (NA-22). Both are bounded and traced. The frozen Ishalgen Priest keeps its current rules unless a later decision changes them. | Decided 2026-09-28 |
| OD-15 | Which speed scroll stays up: Courage (attack speed) or Awakening (casting speed)? | **Awakening** for the Cleric. The two scrolls **replace each other** (both use effect id 30184), so only one can be up. Awakening shortens the casts of Smite, Healing Light and Earth's Wrath; Courage mostly speeds up the staff swing. The bot never uses Courage. | Decided 2026-09-28 |

## Route at a glance (Java spec, verified in C#)

**Dialog action ids** (`src/Aion.GameServer/Model/DialogAction.cs`):

| Action | Id |
|---|---|
| USE_OBJECT (talk) | −1 |
| SELECTED_QUEST_REWARD1 / REWARD2 | 8 / 9 |
| SELECTED_QUEST_NOREWARD | 23 |
| QUEST_SELECT | 31 |
| AIRLINE_SERVICE | 44 |
| SELECT_QUEST_REWARD | 1009 |
| SELECT2_1 / SELECT3_1 / SELECT5_1 | 1353 / 1694 / 2376 |
| SETPRO n | 9999 + n (SETPRO1 = 10000, SETPRO5 = 10004, SETPRO6 = 10005, SETPRO14 = 10013) |

**Packet formats**
- `CM_DIALOG_SELECT` = `D target, UH action, UH rewardIndex, UH lastPage, D questId, UH unk`.
- `CM_PLAY_MOVIE_END` = `C type, D target, D quest, D movie, C unk, C canSkip`.

**Talk and bind ranges.** Talk range is the `talk_info` distance + 1:
- **6 m** for every quest NPC here;
- **4 m for Doman**.
The obelisk dialog opens at 9 m, but binding is accepted only **within 5 m**.

| # | Quest / var | Map and position | NPC | Client sends | Server does / what to observe |
|---|---|---|---|---|---|
| 1 | 2008 v0 | Ishalgen 220010000 (378.74, 1895.46, 328.84) | Munin 203550 | QUEST_SELECT → page 1011; SETPRO1 | var 1; teleport on the same map to (585.51, 2416.03, 278.63) |
| 2 | 2008 v1 | (587.62, 2412.88, 278.56) | Urd 790003 | QUEST_SELECT → 1352; SETPRO2 | item 182203009; var 2; teleport to (940.74, 2295.53, 265.66) |
| 3 | 2008 v2 | (938.00, 2298.00, 266.13) | Verdandi 790002 | QUEST_SELECT → 1693; SETPRO3 | item 182203010; var 3; teleport to (1111.56, 1719.27, 270.11) |
| 4 | 2008 v3 | (1114.31, 1718.32, 271.18) | Skuld 203546 | QUEST_SELECT → 2034; SETPRO4 | item 182203011; var 4; teleport to (383.10, 1895.31, 327.63) |
| 5 | 2008 v4 | Munin (as row 1) | Munin 203550 | QUEST_SELECT → 2375; SELECT5_1 → **movie 57** (end or skip it), then the cards are removed and page 2376 appears; SETPRO5 | var 99; a **new instance of 320020000** (ATAXIAR_B); arrive at (457.65, 426.8, 230.4); `SM_ASCENSION_MORPH(1)` |
| 6 | 2008 v99 | instance (434.75, 399.5, 233.57) | Hagen 205020 | QUEST_SELECT (no dialog comes back) | skill 257 "Shield of Hagen"; the player is FLYING; `SM_EMOTION START_FLYTELEPORT` with teleport 3001 (flypath 3: 45 s to (308.21, 275.45, 206.27)); var 50 |
| 7 | flight | along flypath 3 | — | `CM_MOVE_IN_AIR` frames along the path, then `CM_EMOTION LAND_FLYTELEPORT (7)` | At 43 s: var 51, and **four 205040** spawn at (294, 277, 207), (305, 279, 206.5), (298, 253, 205.7) and (306, 251, 206), each with hate on the player |
| 8 | 2008 v51→54→5 | landing area | 205040 ×4, then Hellion 205041 at (301, 259, 205.5) | kill them. Their AI deals **1 damage** and they drop nothing. | The 4th kill sets var 5 and spawns Hellion. Hellion's death plays **movie 152** (end or skip it), deletes every NPC and **spawns Munin at (301.93, 274.26, 205.7)**; var 6 |
| 9 | 2008 v6 | instance | Munin 203550 | QUEST_SELECT → 2716; SETPRO6 → **class page 4080**; **SETPRO14 = Cleric** | `SM_ACTION_ANIMATION(CLASS_CHANGE)`; `SM_PLAYER_INFO` with class 10; skills 46/48/49/50/89/106; status REWARD, page 5 |
| 10 | 2008 REWARD | instance | Munin | **SELECTED_QUEST_NOREWARD (23)**. No other action leaves the instance. | teleport to Ishalgen (386.03, 1893.93, 327.62); the quest completes; the character is a Daeva; Q2009 starts by itself |
| 11 | 2009 v0 | Ishalgen. Munin is about 7.4 m away, so step in first. | Munin | QUEST_SELECT → 1011; SETPRO1 | var 1; teleport to **Pandaemonium 120010000 (1685, 1400, 195)** |
| 12 | 2009 v1 | (1614.00, 1397.96, 193.13), about 71 m | Heimdall 204182 | QUEST_SELECT → 1352; SELECT2_1 → **movie 121** (end or skip it); SETPRO2 | var 2 |
| 13 | 2009 v2 | (1469.10, 1466.08, 177.82), about 160 m and 18 m lower | Balder 204075 | QUEST_SELECT → 1693; SELECT3_1 → **movie 122** (end or skip it); SETPRO3 | For a character that started as a Priest: var 40, **reward group 3**, REWARD |
| 14 | 2009 REWARD | (1479.01, 1431.83, 177.32), about 36 m | Lyfjaberga 204083 | talk → page 3057; SELECT_QUEST_REWARD → page 8; **REWARD2 (9) = Karmic Staff 101500498** | +13,125 XP → **level 10**; 250,000 Kinah; 5× 162001057; level 10 skills auto-learned; Q2009 completes and **Q2904 starts at var 0** |
| 15 | 2904 v0 | (1682.45, 1397.31, 195.36), about 206 m back up | Doman 204191 (4 m) | QUEST_SELECT → 1352; SETPRO1 | var 1 |
| 16 | travel | Doman | Doman | AIRLINE_SERVICE (44) → `SM_TELEPORT_MAP` (teleport 50); `CM_TELEPORT_SELECT(npc, loc 9)` | −500 Kinah before price modifiers. It went through `PricesService`, and **706** was observed in SIM (NA-02); arrive at **Altgard 220030000 (1752.53, 1806.61, 254.66)**; **Q24010 starts by itself** on `CM_LEVEL_READY` |
| 17 | bind | Altgard Fortress (1658.44, 1815.30, 254.10), about 94 m west | obelisk 700065 | talk → `SM_QUESTION_WINDOW` 160012 (price 451); `CM_QUESTION_RESPONSE(160012, 1)` from within 5 m | −451 Kinah; `SM_BIND_POINT_INFO`; `BIND_KISK` animation |
| 18 | 2904 v1 | (1667.09, 1747.48, 260.28), about 68 m south | Meiyer 203559 | QUEST_SELECT → REWARD, page 2375; SELECT_QUEST_REWARD → page 5; NOREWARD (23) | quest complete: 11,237 XP, 5× 160002273 |
| 19 | 24010 | (1662.63, 1748.56, 260.24), beside Meiyer | Suthran 203557 | QUEST_SELECT → REWARD, page 1011; SELECT_QUEST_REWARD → page 5; NOREWARD (23) | quest complete: 22,473 XP, 2,170 Kinah. Q24011 stays locked below level 11 |
| 20 | shop stop | Altgard Fortress | see [Appendix A](#appendix-a-altgard-shops-and-consumables) | equip, sell, buy potions | see the endpoint contract |

**Java sources**
- Quest handlers:
  - `aion-server/game-server/data/handlers/quest/ascension/_2008Ascension.java` (movies at L81 and L114)
  - `_2009ACeremonyinPandaemonium.java` (movies at L83 and L98)
  - `_2904DispatchtoAltgard.java`
  - `…/quest/altgard/_24010SuthransOrders.java`
- AI:
  - `…/handlers/ai/ResurrectAI.java` (bind)
  - `…/ai/quests/AscensationNpcAI.java` (the trial NPCs that deal 1 damage)
- Services and packets:
  - `services/ClassChangeService.java`
  - `services/teleport/TeleportService.java`
  - `network/aion/clientpackets/CM_PLAY_MOVIE_END.java`
  - `serverpackets/SM_PLAY_MOVIE.java`
  - `questEngine/handlers/AbstractQuestHandler.java:662-670` (`playQuestMovie`, always skippable)
- Static data:
  - `quest_data.xml`: 2008 at L9299, 2009 at L9302, 2904 at L17381, 24010 at L56915
  - `npc_teleporter.xml`: teleport 50 at L68–79
  - `teleport_location.xml:10`
  - `bind_points/bind_points.xml:24`
  - `flypath_template.xml:5`
  - `world_maps.xml`: L6, L22, L125
  - `npc_trade_list.xml`, `goodslists/goodslists.xml`, `skill_tree/skill_tree.xml`
- Spawns:
  - `spawns/Npcs/220010000_Ishalgen.xml`, `120010000_Pandaemonium.xml`, `220030000_Altgard.xml`
  - `spawns/Instances/320020000_Ataxiar.xml`

The C# twins live under `src/Aion.GameServer/Handlers/Quest/ascension/` and `…/altgard/`.

## Hazards (read before touching the leg)

1. **The character stays level 9 through Q2008.**
   - Q2008's 73,200 XP is paid before Daeva status is set.
   - A non-Daeva is capped at the start of level 10 (126,069 XP), which the client shows as
     a full bar at level 9. Nothing past Ascension is capped.
   - Level 10 comes only with the Q2009 payout.
   - The Ishalgen engine's `pre-ascension-level` guard (level ≥ 10 → blocked) must keep
     applying to the Ishalgen leg, and must not apply to this one.
2. **Only NOREWARD (23) leaves the Ascension instance.** Any other reward action completes
   the quest inside 320020000 and strands the bot. The instance has no exit, portal or
   instance handler.
3. **Four assassins attack at once.**
   - The Priest policy's `SwarmedAttackers = 3` (`NaturalPriestCombatPolicy.cs:75`) would
     make the bot retreat, and there is nowhere to go.
   - The trial NPCs deal 1 damage (`AscensationNpcAI`).
   - The leg needs a scripted-trial combat mode:
     - no swarm retreat;
     - stay at the landing area;
     - keep the 30% emergency rule as a safety valve.
   - Walkthrough knowledge is allowed ("static quest knowledge may guide planning").
   - Trigger the mode from observed state: map 320020000, Q2008 var 51–5, and the observed
     damage per hit.
4. **The character cannot attack in flight.** The assassins spawn about 2 s before the
   landing. Land first (`LAND_FLYTELEPORT`), then fight.
5. **Hostile Elyos legionaries (205038, aggressive, sight 10) stand 40–50 m west of the
   landing.** Do not wander there. Load the instance's hostile spawn list.
6. **A death in the instance while var > 4 resets var to 4.**
   - Logging in outside the instance with var > 4 and var ≠ 6 does the same.
   - To recover: revive at the Ishalgen bind, walk to Munin, and repeat SELECT5_1 and
     SETPRO5. That creates a new instance.
7. **No NPC offers Q2904.**
   - It is added automatically when Q2009 completes.
   - It requires Q2009 finished with reward group 3, class Chanter or Cleric, and level ≥ 10.
     It is a non-mission quest, so there is no level slack.
   - The item 182207004 "Lyfjaberga's Recommendation" is never handed out; do not wait
     for it.
8. **Q24010 starts on every `CM_LEVEL_READY` in Altgard**, whatever state Q2904 is in.
   Expect it in the journal on arrival.
9. **Kinah is only available after Q2009.**
   - The teleport (500), the bind (451) and the potions are affordable only after the
     250,000 Kinah reward.
   - Keep the order: 2009 → Doman → bind → shops.
   - **Pandaemonium has no potion shop.** Its potion lists are on a broker NPC
     (`func_dialogs 33`).
10. **Talk ranges:** 6 m for quest NPCs, 4 m for Doman, 5 m to accept a bind. Munin is
    about 7.4 m from the Q2008 return point.
11. **Pandaemonium (world_maps.xml:6):**
    - there is no BIND flag;
    - the death level is 150, and the floors are at z 177–215;
    - the route to Balder and Lyfjaberga drops about 18 m;
    - there are no doors, and the teleport statues and Gaminart are not needed.
12. **Altgard has `twin_count = 3`.** Refresh the geometry's instance/channel id after entry.
13. **Hard-coded Ishalgen and Priest assumptions in the bot** (audits of 2026-09-28):
    - **Decision loop** (`NaturalIshalgenDecisionLoop.cs:69-86`): stops on the wrong map,
      on level ≥ 10, and on Q2008 beyond START/0.
    - **Identity validators** reject anything other than PRIEST, level > 9, or world ≠
      220010000:
      - `NaturalIshalgenIdentityScenario.cs:32/40/114-117`
      - `LiveNaturalIshalgenIdentityScenario.cs:108-128`
      - `LiveNaturalJourneySession.cs:65`
      - SIM `SimulationNaturalIshalgenJourneyTests.cs:84-96`
    - **Navigation:** `BotNavigationAssets.NaturalJourneyGeometry` throws for every map
      except 220010000 and 320010000 (`:55-59`).
    - **Bind:** `BindAtAldelleIfNeededAsync` and `NaturalJourneyTravelPolicy.cs:27` require
      map 220010000.
    - **World reload:** `BotWorldModel.ApplyPlayerSpawn` does not clear stale objects, and
      bind revive waits only for `SM_CHANNEL_INFO`.
    - **Gear and inventory:** see [Appendix B](#appendix-b-equipment-and-what-the-gear-code-gets-wrong).
      The worst case is that the inventory policy would **sell the ceremony staff** as
      unusable (`IsPriestGear` excludes STAFF), and `GearScore` would rank it below the
      Aldelle Mace.
    - **After the class change, every relog, reconnect or verify step fails until NA-07
      lands.**
14. **Navigation data for the new maps is not checked in.**
    - The git-ignored `run/nav/` on this machine has local bakes of 120010000, 220030000
      and 320020000, and `BotNavMeshSet` silently falls back to it.
    - Acceptance must use checked-in data (NA-04).
    - None of the three maps has a travel graph.
15. **Doc drift.** `docs/bot-navigation.md` points road extraction at
    `../aion-portal/assets/`, which is now empty. The map art is in
    `../aion-spawn-editor/assets/maps/`.
16. **The class choice is irreversible.** Every attempt past SETPRO14 uses up that
    character, so iterate from a restored snapshot (NA-03).
17. **Four quest movies are on this route:**
    - 57 at Munin, Q2008 SELECT5_1;
    - 152 when Hellion dies;
    - 121 at Heimdall;
    - 122 at Balder.

    `SM_PLAY_MOVIE` puts the player in `WATCHING_CUTSCENE`.
    - **While in that state the server silently drops `CM_MOVE`** (`CM_MOVE.handleBogusPacket`).
    - Only `CM_PLAY_MOVIE_END` clears it. The client sends that packet when the cutscene
      finishes or is skipped.
    - All four movies are sent as skippable (`playQuestMovie` → `canSkip = true`).

    `BotReflexes` already answers every `SM_PLAY_MOVIE` at once with `CM_PLAY_MOVIE_END`
    (an instant skip). The bridge must not move or act between the movie and its end
    packet.

    Movie 152 arrives **mid-fight**: it plays when Hellion dies, and the NPCs vanish.

    `SM_ASCENSION_MORPH` and the class-change animation are not movies, but they must
    decode or be tolerated.
18. **Potions share delay group 11.**
    - HP and MP elixirs block each other for 60 s.
    - **No consumable needs a weapon.** The weapon start condition only applies to cast skills
      (`WeaponCondition.java:30`).
    - **Using any item cancels the current cast** (`CM_USE_ITEM.java:76`), so use items only
      between casts.
    - Items can't be used while dead, stunned or transformed.
    - A potion used at full HP or MP is wasted.
    - The journey never sets `HasManaPotion`, `ManaPotionReady` or `HasLifePotion`
      (`NaturalIshalgenJourney.cs:4308-4318`), so the policy's mana-potion branch is dead
      today. Leave it alone for the frozen Ishalgen leg.
19. **The staff is two-handed.**
    - It fills the main hand and clears the off hand. The Priest never had a shield, so
      only the mace is replaced.
    - It needs Advanced Staff (skill 89), which is learned at the class change. The
      ceremony comes after that, so it is equippable.

## Readiness: what exists and what is missing

**Server (C#): ready.**
- These are faithful ports and registered: `_2008Ascension`,
  `_2009ACeremonyinPandaemonium`, `_2901..2904`, `_29070/29071` and `_24010`.
- These match Java too:
  - `ClassChangeService`
  - the ATAXIAR_B data and spawns
  - `AscensationNpcAI`
  - `TeleportService`, including the map change
  - `ResurrectAI` binding
  - the cutscene state and `CM_PLAY_MOVIE_END`
  - the Altgard trade lists
  - the Cleric skill-tree rows and templates, and every effect type they use
- The teleport sweep passed loc 9, and the bind sweep passed 700065.
- The quest path is active because `gameserver.simple.secondclass.enable=false`.
- **No test has ever run the Asmodian 2008/2009/2904 chain.** NA-02 fixes that.

**Bot: the foundations exist; the leg does not.**

What exists:
- `BotApi.SelectDialog` sends any action.
- `BotReflexes` handles map changes and skips movies.
- Q2002's Ataxiar round trip is the template for a map change
  (`NaturalIshalgenJourney.cs:2028-2078`).
- There is code for an obelisk bind (`:718-736`), teleporter flights (`:760-788`),
  vendor restock (`:231-319`) and gear equip (`:170-208`).
- `CapitalAscensionScenario.cs` already does the Elyos flight, trial, class choice and
  NOREWARD exit.
- **Accessories are already equipped by the Ishalgen leg.** Both LIVE runs (`ni09-live-a4`
  and `ni10-a2`) wore the Spirit Ring, the Black Opal and Munin's Belt.

What is missing:
- a context that spans multiple maps
- identity, gear and inventory that know the class and level (including the staff)
- the leg's contract, engine and handlers
- the scripted-trial mode
- a movie policy option
- Altgard shopping
- the Cleric skill catalog and rotation
- snapshots

**Navigation: inputs exist but nothing is checked in.**
- Geo inputs and walk masks exist for all three maps, and none of them has doors.
- Local bakes with the same settings exist.
- There are no graphs, roads or monitor art.

**Unrelated but noticed.** `upstream/4.8` has 11 unscanned Java commits (up to `558677569`).
None touch this route; they belong in the normal upstream queue.

## TODO list

Each item gives **Depends**, **Do**, **Done when** and **Verify**. When an item is done:
tick it, add an evidence line (date, run ids, numbers, commit), and commit (OD-3). Per
OD-11, a development item is verified by **one** run.

### Phase 0: Governance

- [x] **NA-00 — Record the decision.** Done in the prep session on 2026-09-28; not yet
  committed, so the loop's first commit includes it.
  - D25 was added to the e2e plan's decision table.
  - The journey rules say Cleric instead of Chanter.
  - Pointers are in `CLAUDE.md` and the status handoff.

- [x] **NA-00a — Restore the warning baseline (pre-existing breach).**
  - **Depends:** none. Do this first: every loop commit runs the baseline check.
  - **The breach:** HEAD `e2f0599e9` (the Mau pilot checkpoint) raised the count from
    4,243 to 4,250 warning sites. All seven new sites are in
    `tests/Aion.Simulation.Tests/SimulationMauCourseTests.cs`:
    - `:163`, xUnit2031: `Assert.Single(instance.GetNpcs().Where(...))` should use the
      predicate overload;
    - `:245-247`, six CS8602: possibly-null `npc.GetSpawn()` dereferences in
      `CaptureCourseResetAsync`.
  - **Do:** fix them in the file's own style. Do not raise the baseline.
  - **Done when:** `pwsh -NoProfile -File scripts/ci/check-warning-baseline.ps1` passes at
    4,243 or fewer.
  - **Verify:** the baseline script, plus
    `dotnet test tests/Aion.Simulation.Tests --filter "FullyQualifiedName~MauCourse"`.
    That test is env-gated; it must still compile and skip or pass as before.

  - **Evidence (2026-09-28):**
    - Fixed `SimulationMauCourseTests.cs`: at `:163`, `Assert.Single` now uses the
      predicate overload; at `:245-247`, `GetSpawn()!`, the repo's existing test idiom.
    - `check-warning-baseline.ps1`, `check-null-loggers.ps1` and `check-clock-reads.ps1`
      pass.
    - The Mau filter builds and skips behind its environment gate, as before.

### Phase 1: Contract and server confidence

- [x] **NA-01 — Bridge contract fixture.**
  - **Depends:** NA-00.
  - **Do:**
    - Write `parity-artifacts/e2e/natural-ascension-contract.json` with:
      - `schemaVersion` and `javaReference` (`ce54b7931…`);
      - the start state, which is the Ishalgen `ascensionStop`;
      - the ordered quests 2008 → 2009 → 2904 → 24010, with each var step's NPC id,
        map, position, dialog actions and pages, and movie ids (from the route table);
      - the class choice: CLERIC via SETPRO14 on page 4080;
      - reward group 3 with REWARD2 (the Karmic Staff);
      - teleport 50 / loc 9;
      - bind 700065 and its price;
      - the instance map and trial spawns;
      - the shop stop: vendors, goods lists and supply targets from Appendix A;
      - the accessories to keep worn;
      - the endpoint.
    - Add a loader record like `NaturalIshalgenContract`.
    - Add `tests/Aion.GameServer.Tests/NaturalAscensionContractTests.cs`, modeled on
      `NaturalIshalgenContractTests`:
      - It checks every id, position, price, level gate, start condition, reward, trade
        list, movie id and skill id against the shipped static data.
      - It checks the C# handler sources: the registration strings, SETPRO14 → CLERIC,
        the NOREWARD teleport and the `playQuestMovie` calls.
      - It computes the expected endpoint level from the XP table.
  - **Done when:** the fixture and its tests pass. Any mismatch with the data is resolved
    Java-first.
  - **Verify:** `dotnet test tests/Aion.GameServer.Tests --filter "FullyQualifiedName~NaturalAscensionContract"`.

  - **Evidence (2026-09-28):** added:
    - `parity-artifacts/e2e/natural-ascension-contract.json`;
    - the loader `tests/Aion.Bots/Scenarios/NaturalAscensionContract.cs`, which resolves
      action names to `DialogAction` ids;
    - `tests/Aion.GameServer.Tests/NaturalAscensionContractTests.cs`, whose 4 tests pass.

    The tests check, against the shipped data:
    - every step's NPC spawn, talk range, dialog branch, item, teleport and movie;
    - the class page 4080 and SETPRO14 → Cleric, plus the masteries;
    - that only NOREWARD exits the instance;
    - the trial spawns and the 1-damage AI;
    - the flypath, teleporter loc 9 and its price, and the 700065 bind price;
    - the shop goods lists and buy/sell dialogs;
    - the accessories and protected items.

    On XP, only one fact is pinned: the Q2009 payout reaches level 10, which Q2904 requires.
    The operator said exact XP does not matter, and that is right: there is no cap after
    Ascension. This also corrected hazard 1: the non-Daeva cap is 126,069, not 182,252.

- [x] **NA-02 — Server proof: a focused SIM scenario for the Asmodian chain.**
  - **Depends:** NA-01.
  - **Do:**
    - Add a GM-prepared focused scenario, the Asmodian twin of `CAPITAL` (e.g. manifest id
      `CAPITAL-ASMO`).
    - Setup is allowed here, as for CAPITAL: an Asmodian Priest at level 9, at Munin, with
      Q2008 START/0.
    - Play the whole route with ordinary packets:
      - the Norn teleports;
      - the instance, flight and landing, and the kills;
      - the Cleric choice and the NOREWARD exit;
      - the ceremony, with REWARD2;
      - Doman's SETPRO1 and teleport loc 9;
      - the 700065 bind;
      - Meiyer, then Suthran.
    - Reuse the `CapitalAscensionScenario` pieces (flight, dialog and page waits, reward
      delta) behind race-specific data rather than copying them.
    - Assert:
      - the quest states;
      - class 10, Daeva status and level 10;
      - the four `SM_PLAY_MOVIE`s, each answered;
      - the ceremony reward delta (the staff);
      - the Kinah changes;
      - the bind point;
      - that 2904 and 24010 complete;
      - the final map.
    - Add a SIM manifest entry (Full tier, like CAPITAL). Add the id to the
      "System-to-scenario matrix (P10-11)" in `docs/e2e-player-simulation-plan.md`;
      `scripts/e2e/test-code-coverage.py` fails without it.
  - **Done when:** it passes in SIM. Any server divergence it finds is fixed Java-first,
    with a regression test and a log entry.
  - **Verify:**
    - the focused SIM test;
    - `dotnet test tests/Aion.GameServer.Tests --filter "FullyQualifiedName~ScenarioManifest"`;
    - `python scripts/e2e/test-code-coverage.py`;
    - `pwsh -NoProfile -File scripts/e2e/test-full-suite.ps1`.
  - **Note:** this is server coverage, not natural-play evidence.

  - **Evidence (2026-09-28): CAPITAL-ASMO passes in SIM.**
    - **Run and result:** run `na02-capital-asmo-5`, evidence in `run/na02/`. All 12 steps
      passed. Problem ledger clean (0 observations), about 30.5 virtual minutes.
    - **What it proves:** the C# server carries the whole Asmodian chain:
      - Norn teleports;
      - Ataxiar instance and flight;
      - four assassins, then Hellion;
      - Cleric, and the NOREWARD exit;
      - Pandaemonium ceremony with the staff, reaching level 10;
      - Q2904 auto-start; the Doman teleport; the 700065 bind;
      - Meiyer, and Suthran's Q24010.

      All four movies were seen, and the cutscene state was cleared. No server divergence
      was found.
    - **What was built:**
      - `CapitalAscensionScenario.Asmodian.cs` reuses the Elyos helpers, and takes every id,
        position and dialog from the NA-01 contract.
      - The SIM driver now takes race-specific setup and verification.
      - Manifest entry `CAPITAL-ASMO` (Sim, Full).
      - The id is added to the P10-11 matrix.
      - A new `AION_SIM_SCENARIO=<id>` filter lets a local SIM run pick named scenarios.
    - **Regression checks passed:**
      - Elyos `CAPITAL` re-run;
      - `ScenarioManifest` tests (86 + 7);
      - `test-code-coverage.py`;
      - `test-full-suite.ps1` (73 SIM scenarios);
      - warning, null-logger and clock ratchets.
    - **Findings for later items:**
      - (a) **The Doman fare goes through `PricesService`.** Global prices, the modifier
        and influence taxes all apply, and it cost **706**, not 500, in SIM. NA-08 must
        check Kinah against the client-observed
        `World.VendorPrices.ServicePrice(base)`. The bind's 451 is charged raw.
      - (b) **The Priest's plain mace swings are slow against Hellion** (1,461 HP,
        about 5 per hit, with stuns). The focused test needed up to 600 swings. The natural
        trial (NA-13) should use the Priest's skills.
      - (c) **Movie 152 arrives inside the kill loop.** So check for movies in the packet
        history, not by waiting for them afterwards (relevant to NA-10/NA-13).

### Phase 2: Snapshots and navigation data

- [x] **NA-03 — SIM snapshot save and restore, starting with Munin.**
  - **Depends:** none. It can run alongside NA-01 and NA-02.
  - **Do:**
    - Add `scripts/sim/sim-snapshot.ps1`, with two modes:
      - `-Save <name> -Run <run id>` dumps a finished SIM run's throwaway schema before
        it is dropped.
      - `-Restore <name>` loads it into a fresh `aion_gs_sim_<run>` schema on the
        development MySQL (D2 allows this).
    - Snapshots live under `run/snapshots/` (git-ignored), with each snapshot's hash,
      source run id and character recorded in this doc.
    - Restores go through the NI-08 resume path: `AION_SIM_NI08_DATABASE`,
      `NI08_RESUME_CHARACTER`, `AION_SIM_NI08_ELAPSED_MS`, with the bridge enabled.
    - Capture **`munin`** from a new seed-1 Ishalgen run that *finished naturally*
      (41/41, at Munin, Q2008 START/0).
    - Every attempt restores a fresh copy, because the class choice is irreversible.
    - Never GM-edit a snapshot (no Mau-course-style preparation).
    - Never touch the `aion` stack's game database.
    - NA-25 saves the second snapshot, **`altgard`**.
  - **Done when:** a restore enters the world as the level 9 Priest at Munin, with 41
    completed quests observed and Q2008 START/0.
  - **Verify:** a script contract test in the style of `scripts/sim/test-*.ps1`, plus one
    manual restore-and-enter.

  - **Evidence (2026-09-28):**
    - **Tooling.** `scripts/sim/sim-snapshot.ps1` supports Capture, Restore, Verify and
      Drop. Its contract test, `scripts/sim/test-sim-snapshot.ps1` (fake Docker), passes and
      checks four things:
      - only owned `aion_gs_sim_ni08_*` schemas are touched;
      - a snapshot is never overwritten;
      - an edited dump is refused;
      - every restore gets a fresh schema.
    - **Clock.** The journey now writes `completion-clock.json` at its endpoint, so a
      restored copy's game time keeps moving forward.
    - **Snapshot `munin`.** Captured from natural SIM run `snapshot-munin-s1` (seed 1,
      git `e838e6003`) and stored in `run/snapshots/munin/`:
      - character 133297 "Asimnjour";
      - endpoint reached at 15,674,212 virtual ms;
      - dump SHA-256 `93617690…dacb9`;
      - level 9 at exactly 126,069 XP, which confirms the non-Daeva cap.
    - **Verify.** `-Action Verify -Name munin` (`na03-verify-munin`) restored a fresh copy
      and resumed the same character. The journey's own endpoint check passed: 41 quests,
      level 9 at Munin, Q2008 START/0. The owned schema was dropped afterwards.
    - **Usage for later items.** `pwsh -File scripts/sim/sim-snapshot.ps1 -Action Restore
      -Name munin` prints the database and the resume environment
      (`AION_SIM_NI08_DATABASE`, `NI08_RESUME_CHARACTER`, `AION_SIM_NI08_ELAPSED_MS`).
      Drop the schema with `-Action Drop -Database <db>` when done.

- [x] **NA-04 — Bake and check in navigation for 320020000, 120010000 and 220030000.**
  - **Depends:** none.
  - **Do:**
    - Run `dotnet run --project tools/Aion.NavBake -- bake --maps 320020000,120010000,220030000`,
      then `… graph --maps 320020000,120010000,220030000`.
    - Review with `render` and `measure --maps <id>` (see `docs/bot-navigation.md:376-387`).
      Confirm that:
      - in Pandaemonium, arrival → Heimdall → Balder → Lyfjaberga → Doman is connected
        and never drops off a ledge or below z 150;
      - in Altgard, landing → 700065 → Meiyer/Suthran → Donabe/Nirmirn is connected;
      - in 320020000, the landing arena around (300, 265) is walkable to Munin's spawn.
    - Roads are optional.
    - Update the files table in `docs/bot-navigation.md` and fix the stale
      `../aion-portal/assets` path.
    - Never hand-edit `game-server/data/nav/`.
  - **Done when:** the navmeshes and graphs are checked in, and `check` passes on all five
    baked maps.
  - **Verify:** `dotnet run --project tools/Aion.NavBake -- check --maps baked`.

  - **Evidence (2026-09-28):**
    - **Baked and checked in** under `game-server/data/nav/`:

      | Map | Navmesh | Travel graph |
      |---|---|---|
      | 320020000 | 1,052 polygons, 66 KiB | 9 nodes |
      | 120010000 | 1.2 MB | 260 nodes, 985 links |
      | 220030000 | 86,537 polygons, 5.5 MiB | 649 nodes, 2,601 links |

      `check --maps baked` passes on all five maps.
    - **Routing** (`NavBake points` through the real router):
      - Pandaemonium: arrival (1685, 1400, 195) → Heimdall, Balder, Lyfjaberga and Doman
        all routed.
      - Altgard: landing → obelisk 700065, Meiyer, Suthran, Donabe and Nirmirn all routed.
      - Ataxiar: landing (308, 275) → Munin's spawn and all five trial spawns routed.
    - **Render** (`run/na04-render/`): the Pandaemonium mesh covers the city only, as a
      single main network.
    - **Other checks:** `docs/bot-navigation.md` gained a table of the checked-in maps and
      the fixed map-art path. 211 navigation/natural unit tests pass, as do the warning,
      null-logger and clock ratchets.
    - **Findings for NA-06/13/14:**
      - (a) For Heimdall, Balder and Lyfjaberga, `FindInteractionPath` returns
        `NoApproachPoint` although the plain route reaches them. They probably stand on a
        dais or behind a counter, so the navigator needs a fallback to the plain route that
        stops within talk range.
      - (b) The plain route to obelisk 700065 is `GeometryRejected`, because the obelisk
        itself is solid, but the interaction route works. Use interaction routing for the
        bind.

- [x] **NA-05 — Monitor map art for the three maps.**
  - **Depends:** none.
  - **Do:** add catalog entries and art (`tests/Aion.Bots/Dashboard/maps/catalog.json`,
    `scripts/sim/import-dashboard-maps.py`):
    - 120010000 and 220030000 from `../aion-spawn-editor/assets/maps/`;
    - the grid fallback for 320020000.
  - **Done when:** the monitor renders all three maps.
  - **Verify:**
    - `node scripts/sim/test-dashboard-map.cjs`
    - a preview with `scripts/sim/preview-dashboard.py`

  - **Evidence (2026-09-28):** `scripts/sim/import-dashboard-maps.py` now reads from
    `../aion-spawn-editor` and imports five maps:
    - Ishalgen (1,415 placements), and Ataxiar 320010000 (47);
    - the bridge maps: Pandaemonium (400, including its `Custom/` stigma, warehouse and
      trainer NPCs), Altgard (3,143) and Ataxiar 320020000 (60).

    The images are embedded automatically through the existing `Dashboard\maps\*` wildcard.

    Test updates:
    - `scripts/sim/test-dashboard-map.cjs` now counts only a map's own `spawn_map`
      blocks, because the shared `Custom/` files list several maps. It passes for all five
      maps.
    - `LiveBotDashboardTests` now serves and checks the three bridge maps; all 4 tests pass.

    Other notes:
    - `docs/bot-monitor.md` is updated.
    - The committed Ishalgen catalog had been stale since `49663a7a8` (the spawn-baseline
      restore), so the dashboard map check was already failing on main. The re-import
      fixes it.
    - The warning, null-logger and clock ratchets pass.

### Phase 3: Bot foundations

- [x] **NA-06 — Journey context across multiple maps.**
  - **Depends:** NA-04.
  - **Do:**
    - Generalize Q2002's map-change pattern into one per-map context, keyed by (map,
      instance/channel). It holds the geometry, navmesh router, travel planner, graph,
      navigator and hostile spawn list.
    - Re-select the context on every `SM_PLAYER_SPAWN` with a new world id, and clear stale
      world objects at the same time.
    - Move Q2002 onto it without changing its behavior.
    - Extend `BotNavigationAssets.NaturalJourneyGeometry` so LIVE accepts 320020000,
      120010000 and 220030000.
    - Handle Altgard's twin channels.
    - When the bind is on another map, bind revive must wait for a world reload.
  - **Done when:**
    - unit tests cover context switches across maps and instances, and the clearing of
      stale objects;
    - one SIM Ishalgen run still completes Q2002 and all 41 quests.
  - **Verify:**
    - the focused `Natural*`, `BotWorldModel*` and `BotNav*` tests
    - `bash scripts/sim/run-natural-batch.sh na06 1`

  - **Evidence (2026-09-28):**
    - **Map contexts.** `NaturalJourneyMapContexts<T>` and `NaturalMapKey` (map + observed
      channel) hold per-map navigation state:
      - Ishalgen keeps the journey's own navigator.
      - Any other map builds its geometry, graph and planner on entry.
      - An instance (3xxxxxxxx) is rebuilt on every entry but reused within one stay.
      - Twin channels are separate worlds.
    - **World model.** On `SM_PLAYER_SPAWN` to another world it forgets the old world's
      objects and dialogs, but keeps the channel that `SM_CHANNEL_INFO` announced just
      before the spawn.
    - **Offline geometry.** `BotNavigationAssets.NaturalJourneyMapIds` now accepts
      320020000, 120010000 and 220030000, so LIVE and attach runs can use these maps.
    - **Q2002.** Its instance navigator now comes from the cache; behaviour is unchanged.
    - **Bind revive.** A revive to an obelisk on another map now waits for the world reload
      (`SM_PLAYER_SPAWN`, then the player's `SM_PLAYER_INFO`).
    - **Tests.** 6 new tests pass: `NaturalJourneyMapContextsTests` ×5 and a
      `BotWorldModelTests` map-change case. 218 focused tests pass in total.
    - **Regression run.** `na06-full-s1` (seed 1) passed, reaching the Munin stop with 211
      quest updates. It entered 320010000 for Q2002 through the cache. It also had 3 deaths,
      which are recorded, not failures (OD-12).
    - **Checks.** The warning, null-logger and clock ratchets pass.
    - **Deferred.** The hostile-spawn list per map is left to the leg items that fight
      there (NA-13).

- [x] **NA-07 — Identity, persistence and world model that know the class and level.**
  - **Depends:** NA-01.
  - **Do:**
    - Identity accepts exactly two states:
      - PRIEST, level ≤ 9, Q2008 not complete (Ishalgen leg);
      - CLERIC, a Daeva, Q2008 complete (the bridge).

      Either state may be on any contract map for its stage.
    - Apply the rule in:
      - the NI-01 validator;
      - the LIVE identity scenario;
      - LIVE relogin (`LiveNaturalJourneySession.cs:65`);
      - the SIM entry checks.
    - The world model tracks:
      - class, from `SM_PLAYER_INFO`;
      - bind point, from `SM_BIND_POINT_INFO`;
      - DP, for Salvation.
    - The persistence comparison includes class, bind point, equipment and map.
    - Keep rejecting everything else. A Chanter, for example, is an error: never "fix" a
      character.
  - **Done when:** tests cover the pre and post states, a relog after the class change, and
    the rejections.
  - **Verify:**
    - the focused tests
    - `pwsh -NoProfile -File scripts/live/test-attach-live.ps1` if attach code changes

  - **Evidence (2026-09-28):**
    - **Rules.** `NaturalJourneyIdentityRules` accepts exactly two states and refuses
      everything else (a Chanter, a Priest above level 9 or outside its maps, any other
      class):
      - Priest, levels 1–9, on 220010000, 320010000 or 320020000;
      - Cleric, level 9 or above, on 320020000, 220010000, 120010000 or 220030000.

      `RequireJournal` also accepts a Cleric at Q2008 REWARD inside Ataxiar: the class is
      set before NOREWARD completes the quest.
    - **Where it applies:**
      - the NI-01 `ValidateCharacter` check;
      - LIVE admin and attach observed identity;
      - LIVE relogin (`LiveNaturalJourneySession`);
      - the SIM retained-character entry (a new character must still be a Priest).
    - **Checkpoint and persistence.** `NaturalJourneyCheckpoint` now records the class and
      the obelisk bind point. `NaturalJourneyPersistence` fails a relog that changes either.
      Equipment was already covered through the inventory slots, DP was already observed, and
      the map was already compared.
    - **Tests:** `NaturalJourneyIdentityRulesTests` has 18 cases. 202 focused tests and the
      NI-10 attach contract (46 assertions) pass.
    - **Runtime:** a `munin` snapshot Verify (`na07-verify-munin`) resumed through the new
      checks.
    - **Ratchets:** warning, null-logger and clock all pass.

- [x] **NA-08 — Bind, teleporter and vendor steps that work on any map.**
  - **Depends:** NA-06.
  - **Do:**
    - **Bind:** turn `BindAtAldelleIfNeededAsync` into a bind step that takes a map and an
      obelisk npc. It:
      - moves within 5 m;
      - checks observed Kinah against the price;
      - answers question 160012;
      - verifies `SM_BIND_POINT_INFO` and the Kinah change.
    - **Teleporter:** a step that:
      - moves within talk range;
      - sends AIRLINE_SERVICE (44) and waits for `SM_TELEPORT_MAP`;
      - checks Kinah, then sends `TeleportSelect(npc, loc)`;
      - hands the map change to NA-06.
    - **Vendor:** generalize the Ishalgen restock session into a vendor step for any NPC
      and trade tab. It sells a list, and buys a list of items with counts, both checked
      against observed Kinah and prices. Tab 721 (Ishalgen) and tab 275 (Altgard) must
      both work.
    - Keep the Ishalgen behavior identical.
  - **Done when:** unit tests cover each step, including "too far", "not enough Kinah",
    "refused" and "item not in trade list". The Ishalgen restock and the Aldelle bind still
    work.
  - **Verify:** the focused tests.

  - **Evidence (2026-09-28):**
    - **`NaturalServicePolicy`** (pure) decides from client-observed state:
      - bind: `already-bound`, `wrong-map`, `too-far` (5 m), `not-enough-kinah`;
      - teleport: `flying`, `too-far` (talk range), `not-enough-kinah` against the fare;
      - vendor plan: `not-in-trade-list`, and purchases capped by Kinah.
    - **`NaturalServiceSteps`** is the client-side executor, sharing one packet flow for all
      three services:
      - bind answers question 160012, then checks `SM_BIND_POINT_INFO` and the raw price;
      - teleport opens AIRLINE_SERVICE, sends `CM_TELEPORT_SELECT` and follows the map
        change. It checks the fare against `VendorPrices.ServicePrice(base)`;
      - trade sells, reads the observed trade window, then buys and verifies the stock;
      - every step records a `service-*` trace diagnostic.
    - **Ishalgen behaviour:**
      - The Aldelle bind now goes through the new bind step.
      - The Ishalgen potion restock is left byte-for-byte unchanged. Its vendor packet flow is
        the same one the new trade step uses, and it could move onto it later.
    - **Tests:** `NaturalServicePolicyTests` (5) cover:
      - too far, not enough Kinah, wrong map and already bound;
      - flying;
      - not in the trade list, and Kinah caps;
      - Ishalgen tabs 264/721, Altgard tab 275 and Donabe's powder, resolved from shipped
        goods lists.
    - **Runtime proof:** the focused SIM test
      `NaturalServiceStepsTeleportBindAndTradeOnTheBridgeMaps` passed with a clean ledger
      (GM setup, not natural evidence). It showed:
      - a teleport refused as too far;
      - Doman → Altgard at the price-adjusted fare;
      - the 700065 bind for 451, then `already-bound`;
      - at Nirmirn, a sale plus 3 Lesser Life Elixirs, with powder refused as not in the
        trade list;
      - 5 powder bought at Donabe.
    - **Regression:** 207 focused tests pass, and the warning, null-logger and clock ratchets
      pass.

- [x] **NA-09 — Gear, accessories, inventory and rewards for a Cleric.**
  - **Depends:** NA-07.
  - **Do:** fix everything in [Appendix B](#appendix-b-equipment-and-what-the-gear-code-gets-wrong).
    - **Class awareness:**
      - Read the restrict column for the *current* class (Cleric = 10).
      - Gate armor and weapon types by the observed mastery skills (49 chain, 50 shield,
        89 staff).
      - Allow item level up to the current level.
    - **Weapons:**
      - Handle two-handed weapons: a staff takes the main hand and clears the off hand.
        Add the sub-hand slot.
      - Rank Cleric weapons by magic boost, then damage, so the Karmic Staff (260) beats
        the Aldelle Mace.
      - Clear `refusedGear` when the class or level changes.
      - Keep `CreateSpellCast` safe for mace and staff.
    - **Accessories:** the comparison covers every slot, including ring ×2, earring ×2,
      necklace, belt and head.
      - Unworn accessories are compared, never sold as "unusable".
      - The upgrade pass runs after the class change and again at the shop stop.
      - It keeps the Ishalgen rings, necklace and belt worn.
    - `Describe` takes race and gender from observation.
    - **Rewards:** `ChooseReward` reads `priest_selectable_reward` for Q2009 and picks
      REWARD2 (the staff, per OD-5). It also handles the NOREWARD finishes.
    - **Protected from selling:**
      - the Destiny Cards (182203009–011);
      - the staff;
      - the worn accessories;
      - 162000053 and 162000057/058;
      - 169300003;
      - 160002273;
      - 162001057.
    - The skill list must not throw on Cleric skills. The full rotation is NA-18.
  - **Done when:** policy tests pass for both classes, including:
    - staff vs Aldelle Mace;
    - an unworn accessory is equipped rather than sold;
    - two-handed clearing;
    - no Priest regression.
  - **Verify:** the focused tests.

  - **Evidence (2026-09-28):**
    - **`NaturalItem` fields.** It now reads the Cleric restrict column (index 10) and the
      item level. Its Cleric-only rules:
      - `IsClericGear` adds staff, chain, shield, head and the accessories;
      - `ClericGearSlot` maps staff to WEAPON and shield to SUB, and each accessory keeps its
        own slot;
      - `UsableByClericAt` checks level and race;
      - `ClericGearScore` ranks weapons by magic boost, then damage, and armor by item
        level.
    - **Inventory rules.** `Decide(world)` picks the rules from the client-observed class
      (`IsCleric`). The Cleric rules:
      - never sell accessories (`accessory-kept`);
      - protect the bridge supplies from the contract;
      - treat the Destiny Cards and the dispatch work item as quest items;
      - offer the best usable Cleric upgrade per slot;
      - sell the replaced Aldelle Mace as surplus.

      **The Priest path is unchanged.** A new test shows that a Priest still treats the staff as
      unusable.
    - **Rewards.** `ChooseReward` returns REWARD index 1 for Q2009, which is the Karmic
      Staff (OD-5). Q2008, Q2904 and Q24010 have no choice (-1).
    - **Equip step.** `EquipUpgradesAsync` takes class and race from the observed self
      object. The character is created male, and the client model has no gender. Refused gear
      is forgotten only when the class changes, so the Priest does not retry the dagger it
      was refused.
    - **Ranking.** Item level already ranks the level-10 staff above the level-8 mace. The
      server itself clears the off hand for a two-handed staff.
    - **Deferred:** a fuller two-handed-versus-shield model, left to a later milestone because
      no shield is owned or bought (OD-7).
    - **Tests.** Four new tests pass:
      - the ceremony choice;
      - the Cleric keep/sell plan;
      - staff Cleric-versus-Priest;
      - class detection from `SM_STATS_INFO` plus `SM_PLAYER_INFO`.

      274 focused tests, and the warning, null-logger and clock ratchets pass.

- [x] **NA-10 — Movie policy: watch or skip.**
  - **Depends:** none.
  - **Do:**
    - Make the existing movie reflex a policy with two modes:
      - **`skip`** (default, OD-10): the current immediate `CM_PLAY_MOVIE_END`;
      - **`watch`**: hold the end packet for the movie's length, then send it.

      Movie lengths come from the client data if `tools/client-extract` can read them;
      otherwise use a configured per-id table that NA-28 can measure.
    - If a movie is ever marked unskippable, always watch it.
    - Between `SM_PLAY_MOVIE` and the end packet, send no movement or actions; the server
      drops `CM_MOVE` in that state.
    - Movie 152 arrives mid-fight: after it, re-observe the target list, because every NPC
      is deleted.
    - Trace each movie: id, quest, mode, delay.
    - The Ishalgen leg keeps `skip`, so its behavior is unchanged.
  - **Done when:** unit tests cover both modes, the movement hold, the unskippable case,
    and that Ishalgen is unchanged.
  - **Verify:** the focused tests (`BotReflexes*` and `Natural*`).

  - **Evidence (2026-09-28):**
    - **Policy.** `BotMoviePolicy` (in `Reflexes`) has two modes:
      - `Skip` (the default, OD-10) answers at once, which is the unchanged Ishalgen
        behaviour;
      - `Watch` holds the `CM_PLAY_MOVIE_END` in `BotReflexes.PendingMovie` for the
        configured length.

      An unskippable movie is always held.
    - **Movement block.** `BotApi.Observe` sets the new `BotBlockingActivity.Cutscene`
      while a movie is held. It is also in the timing rule set's movement blockers, so
      `EnsureCanMove` refuses `CM_MOVE` until the movie ends, mirroring Java
      `CM_MOVE.handleBogusPacket`. `FinishPendingMovie` clears it.
    - **Gate.** `NaturalMovieGate.FinishAsync` waits the length in session time (game time in
      SIM, real time in LIVE), sends the end, and traces `movie-watched`.
    - **Lengths.** Movie lengths come from a per-id table, defaulting to 20 s until NA-28
      measures them in the real client. Reading them from the client data was not attempted.
    - **Tests:** two new reflex/API tests pass, and 243 focused tests pass (including the
      timing contract's movement-blocker set).
    - **Regression:** CAPITAL-ASMO re-ran green, answering all four bridge movies on the
      skip path. The warning, null-logger and clock ratchets pass.
    - **For NA-12/13/14 in watch mode:** call `NaturalMovieGate.FinishAsync` after SELECT5_1,
      the Hellion kill (then re-observe the NPCs, which are all deleted), SELECT2_1 and
      SELECT3_1.

### Phase 4: The bridge

- [x] **NA-11 — Decision engine for the bridge.**
  - **Depends:** NA-01, NA-07.
  - **Do:**
    - Write a pure `NaturalAscensionDecisionEngine` over the NA-01 contract. It uses the
      same rule-tree trace and dashboard shape as `NaturalIshalgenDecisionEngine`.
    - It maps observed state to exactly one next action from the route table, or to a
      precise `blocked` / `awaiting-capability` stop. The observed state is: map, instance,
      quest statuses and vars, class, level, Kinah, bind point, inventory, equipment and
      position.
    - Allowed maps per stage:
      - Ishalgen for Q2008 v0–4 and Q2009 v0;
      - 320020000 for v99–6;
      - Pandaemonium for Q2009 v1–REWARD and Q2904 v0;
      - Altgard after that.
    - When the bridge is enabled (option or env, e.g. `NA_ASCENSION=1`), the Ishalgen leg's
      `journey-complete` hands off to this engine instead of quitting. With the flag off,
      behavior is exactly the same as today.
  - **Done when:** table tests cover:
    - every route row;
    - the reset states (var 4 after a death or relog);
    - a wrong map;
    - missing Kinah;
    - a pending shop stop;
    - the endpoint.
  - **Verify:** the focused tests.

  - **Evidence (2026-09-28):**
    - **Engine.** `NaturalAscensionDecisionEngine.Decide(contract, NaturalAscensionObservation,
      sequence)` is pure and records its checks in the same shape as the Ishalgen engine.
      - **Guards:** it first checks identity and journal (NA-07 rules).
      - **Steps:** it follows every route row by contract step key, with a map allow-list
        per step.
      - **Activities:** revive at bind, wait on the flight, the scripted trial,
        `teleport`, `bind` (first in Altgard), `shop`, and finally `bridge-complete`.
      - **Stops:**
        - `wrong-map` / `unexpected-var` / `identity` / `level` / `not-enough-kinah` stop as
          `blocked`;
        - the auto-started quests (Q2008, Q2009, Q2904, Q24010) stop as
          `awaiting-capability` until they appear in the journal.
    - **Handoff.** `NaturalJourneyOptions.AscensionBridge` (env `NA_ASCENSION=1` in SIM) is
      off by default, so today's journey is unchanged. When it is on, the Ishalgen
      `journey-complete` asks the engine for the first bridge move and records it in the
      trace. NA-12 replaces the quit that follows with the bridge runner.
    - **Tests.** `NaturalAscensionDecisionEngineTests` covers 16 cases: every route row, the
      trial, the var-4 resets after a death or relog, wrong map, missing Kinah,
      journal waits, identity, the shop stop and the endpoint.
    - **Runtime proof.** A `munin` snapshot run with `NA_ASCENSION=1` (`na11-handoff`) had
      the engine choose `talk → q2008-v0-munin` from real observed state.
    - **Checks.** The warning, null-logger and clock ratchets pass.

- [x] **NA-12 — Q2008 Norn circuit (rows 1–5).**
  - **Depends:** NA-06, NA-10, NA-11.
  - **Do:**
    - Handlers for Munin v0, Urd, Verdandi, Skuld and Munin v4.
    - After each same-map teleport, wait for `SM_TELEPORT_LOC` or the new position, then
      `AcceptTeleportPosition`.
    - Wait for each Destiny Card to appear.
    - At Munin v4:
      - send SELECT5_1;
      - handle movie 57 through NA-10;
      - send SETPRO5;
      - enter the instance through NA-06.
  - **Done when:** one run from the `munin` snapshot reaches 320020000 at var 99, with the
    cards consumed and movie 57 answered.
  - **Verify:** that snapshot run's trace.

  - **Evidence (2026-09-28):**
    - **Bridge runner.** With `AscensionBridge` on, the Ishalgen endpoint no longer quits.
      `RunAscensionBridgeAsync` asks the NA-11 engine for each move, traces every
      `ascension-bridge-decision`, and stops on a non-planned outcome or on a move not built
      yet, writing `bridge-stop.json`. It fails if the same move repeats three times with no
      progress.
    - **Generic step handler.** `PlayBridgeTalkAsync` plays any contract talk step:
      - approaches the NPC through the per-map navigator (NA-06) and opens the dialog,
        re-approaching if "too far";
      - sends the step's actions and waits for each contract page;
      - finishes movies (NA-10);
      - follows same-map teleports (`SM_CHANNEL_INFO` + self `SM_PLAYER_INFO`) and
        cross-map ones (`SM_PLAYER_SPAWN` + self `SM_PLAYER_INFO`, then a map-context
        change);
      - checks received items, and that Munin takes the Destiny Cards back.
    - **Steps enabled.** `ImplementedBridgeSteps` holds q2008-v0 to v4.
    - **Result.** From the `munin` snapshot with `NA_ASCENSION=1` (`na12-norns`, confirmed by
      `na12-norns-b` with the card check), the bot played the whole circuit:
      - Munin, then the teleport to Urd (card of the past, then teleport);
      - Verdandi (card, teleport), then Skuld (card, teleport);
      - Munin again: SELECT5_1 (the page after movie 57), then SETPRO5.

      It arrived in **Ataxiar 320020000 at Q2008 var 99** with the cards consumed. It then
      stopped as planned at `q2008-v99-hagen` ("not built yet", NA-13).
    - **Checks.** 214 Natural tests and the warning, null-logger and clock ratchets pass.
      The flag-off journey is unchanged.

- [x] **NA-13 — Ascension instance: flight, trial, Cleric, exit (rows 6–10).**
  - **Depends:** NA-12, NA-09.
  - **Do:**
    - Send QUEST_SELECT to Hagen.
    - Fly flypath 3 with `CM_MOVE_IN_AIR`, reusing the Capital / `CreateQuestFlight` code,
      then send `LAND_FLYTELEPORT (7)`.
    - Fight in scripted-trial mode (hazard 3): four 205040, then 205041.
      - No swarm retreat.
      - Stay within about 15 m of the landing.
      - Keep the emergency rule.
    - Handle movie 152 and the NPC wipe (NA-10).
    - Approach the Munin who spawns:
      - SETPRO6, then wait for page 4080;
      - **SETPRO14**, then observe class 10 and the new skills;
      - equipment pass (NA-09).
    - At page 5, send **NOREWARD (23)**, then wait for the map change to Ishalgen (386.03,
      1893.93).
    - Recovery, bounded and traced:
      - a death in the instance → revive at the Ishalgen bind → back to Munin at var 4;
      - a relog in the instance → follow the observed var.
  - **Done when:** one snapshot run completes Q2008 as a Cleric and is back in Ishalgen
    with Q2009 START/0.
  - **Verify:** that snapshot run's trace.

  - **Evidence (2026-09-28):** the run from the `munin` snapshot with `NA_ASCENSION=1`
    (`na13-trial`) played the whole instance.
    - **Flight.** Hagen's QUEST_SELECT, then `START_FLYTELEPORT` observed, flypath 3 flown with
      `CM_MOVE_IN_AIR` (45 s), and `LAND_FLYTELEPORT`.
    - **Trial.** Four guardian assassins and then Hellion, one kill per decision.
      - `NaturalJourneyCombat.ScriptedTrial` makes the policy treat the fight as cornered: no
        swarm retreat, heals stay on.
      - The Priest ended at about 767 of 769 HP, with no retreats and no deaths.
      - Q2008 var went 51 → 52 → 53 → 54 → 5 → 6.
    - **Cleric choice.** The spawned Munin was approached through the instance navigator.
      QUEST_SELECT, SETPRO6 (page 4080) and SETPRO14 made the class go from 9 to 10, as the
      client observed. NOREWARD then teleported to Ishalgen (386.03, 1893.93).
    - **Result.** **Q2008 COMPLETE, Q2009 START/0**, level 9 (the cap), stopping at
      `q2009-v0-munin` for NA-14.
    - **Loop guard.** The progress signature now includes the decision reason, so successive
      trial kills count as progress.
    - **Checks.** 214 Natural tests and the warning, null-logger and clock ratchets pass.
    - **Note.** Normal attacks alone were too slow against Hellion in NA-02; the natural
      Priest rotation killed it without trouble.

- [x] **NA-14 — Q2009 Pandaemonium ceremony (rows 11–14).**
  - **Depends:** NA-13.
  - **Do:**
    - Step within 6 m of Munin and send SETPRO1. Handle the map change to 120010000.
    - Walk the city on the NA-04 navmesh.
    - Heimdall: SELECT2_1, movie 121, SETPRO2.
    - Balder: SELECT3_1, movie 122, SETPRO3.
    - Lyfjaberga:
      - talk, then SELECT_QUEST_REWARD;
      - wait for page 8, then send **REWARD2**.
    - Observe level 10, +250,000 Kinah, the 5 teas, the staff, the new skills and Q2904
      START/0.
    - **Equip the staff** (NA-09).
  - **Done when:** one snapshot run finishes Q2009 with the staff worn, with no fall damage
    and nowhere near the death level.
  - **Verify:** that snapshot run's trace.

  - **Evidence (2026-09-28):** from the `munin` snapshot with `NA_ASCENSION=1`
    (`na14-ceremony`), the generic step handler played the four Q2009 steps from contract
    data:
    - Munin's SETPRO1: the cross-map teleport to 120010000, then a map-context switch;
    - Heimdall: SELECT2_1 (movie 121), then SETPRO2;
    - Balder: SELECT3_1 (movie 122), then SETPRO3, reaching REWARD var 40;
    - Lyfjaberga: talk, SELECT_QUEST_REWARD (page 8), then REWARD2.

    **Result:** Q2009 is COMPLETE, **level 10**, and Q2904 START/0 was observed. The
    **Karmic Staff was equipped** by the NA-09 equip pass right after the reward, which the
    run requires. The run stopped at `q2904-v0-doman` for NA-15.
    - **Safety:** the city walk stayed on the navmesh, and the lowest of 328 moves was at
      z 176.9 (death level 150). There were no deaths and no fall messages.
    - **Finding:** the `NoApproachPoint` noted in NA-04 did not block. The navigator reached
      Heimdall, Balder and Lyfjaberga without the interaction-path fallback.
    - **Checks:** 214 Natural tests and the warning, null-logger and clock ratchets pass.

- [x] **NA-15 — Dispatch to Altgard, bind, Meiyer and Suthran (rows 15–19).**
  - **Depends:** NA-14, NA-08.
  - **Do:**
    - Doman within 4 m: QUEST_SELECT, then SETPRO1.
    - Take Doman's teleporter to loc 9, then handle the map change to 220030000. Observe
      Q24010.
    - Bind at 700065 (NA-08).
    - Meiyer: QUEST_SELECT, SELECT_QUEST_REWARD, NOREWARD.
    - Suthran: the same three.
  - **Done when:** one snapshot run completes all four quests and the bind, and the Kinah
    changes match the contract.
  - **Verify:** that snapshot run's trace.

  - **Evidence (2026-09-28):** the runner now plays Doman, Meiyer and Suthran as contract
    talk steps, plus two new actions:
    - `teleport` uses the NA-08 teleporter step. It charged 706 Kinah: the price-adjusted
      fare (500 base) that the client observes through SM_PRICES. The bot landed at
      (1752.5, 1806.6).
    - `bind` approaches obelisk 700065 through the Altgard navigator, then uses the NA-08
      bind step. The bind happened 2.7 m from the obelisk and cost 451 Kinah.

    From the `munin` snapshot with `NA_ASCENSION=1` (`na15-altgard`), the bot ran in order:
    1. Doman: QUEST_SELECT, SETPRO1;
    2. the teleport to Altgard, where Q24010 appeared in the journal;
    3. the bind;
    4. Meiyer: QUEST_SELECT (page 2375), SELECT_QUEST_REWARD (page 5), NOREWARD;
    5. Suthran: the same three.

    **All four bridge quests (2008, 2009, 2904 and 24010) are complete**, at level 10 on
    220030000, with no deaths. The run stopped at the `shop` action for NA-16.
    - **Checks:** 214 Natural tests and the warning, null-logger and clock ratchets pass.

- [x] **NA-16 — The Altgard shop stop (row 20).**
  - **Depends:** NA-15, NA-08, NA-09.
  - **Do:** after the turn-ins, plan one walk inside the fortress, with the stops ordered
    by route (see [Appendix A](#appendix-a-altgard-shops-and-consumables)).
    1. **Equip.** Run the NA-09 comparison over everything owned, accessories included.
       The staff should already be worn. Verify with the equipment and inventory packets.
       **Buy no gear (OD-7).**
    2. **Sell** at the nearest buyer: replaced gear (the Aldelle Mace and anything
       outclassed), level 1–9 leftovers with no further use, and ordinary loot and junk.
       Donabe is 28 m from arrival; every fortress general merchant buys. Never sell
       protected items.
    3. **Restock HP.** At Nirmirn (goods list 275), bring combined life-potion stock
       toward the NI target of 12 by buying Lesser Life Elixirs (162000053). Keep the
       Minor potions and elixirs already owned. Add 162000053 and skill 10203 to the
       potion policy.
    4. **Reagent (OD-9).** At Donabe, buy Lesser Odella Powder 169300003 (about 30).
    5. **Tea (OD-8).** Drink one Tea of Repose, out of combat.

    Every purchase is checked against observed Kinah and price, and verified by the stock
    the packets show.
  - **Done when:** one snapshot run completes the shop stop, and the endpoint's equipment
    and shop evidence is in the trace.
  - **Verify:**
    - that snapshot run
    - policy tests for the Altgard supply targets and the no-gear-purchase rule

  - **Evidence (2026-09-28): the whole bridge now completes.**
    - **Run.** From the `munin` snapshot with `NA_ASCENSION=1` (`na16-shop`, confirmed by
      `na16-shop-b` with sold/worn/supplies traced), the engine reached **`bridge-complete`**:
      a Cleric at level 10 with Q2008, Q2009, Q2904 and Q24010 complete, bound at 700065,
      standing in Altgard Fortress.
    - **Shop stop, in order:**
      1. The equip pass swapped the Karmic Staff for the Aldelle Mace and put on the unworn
         Fighter's Gloves.
      2. Vendors were visited in route order.
         - **Donabe:** 13 stacks sold for Kinah — old weapons (starter, Raider's and Aldelle
           maces, the refused dagger), outgrown armor, bandages, foods, and the Ghost Brooch
           (vendor junk). She bought us 30 Lesser Odella Powder; elixirs were refused as not on
           her list.
         - **Nirmirn:** 12 Lesser Life Elixirs bought.
      3. One Tea of Repose drunk.
    - **What stayed:** nothing gear-related was bought (the contract and template slot are
      asserted).
      - **Worn:** staff, tunic, gloves, pants, shoes, and the three Ishalgen accessories
        (ring, necklace, belt).
      - **Kept:** 12 elixirs, 30 powder, 5 Zeller jellies, 4 teas, 32 starter potions.
    - **Contract change.** The elixir purchase is now "12 Lesser Life Elixirs" rather than
      combined life potions toward 12. The Priest still carried 32 or more starter potions,
      and the operator asked for better potions.
    - **Checks.** The Natural tests and the warning, null-logger and clock ratchets pass.
    - **Note for NA-19.** Foods are sold as unneeded today. The buff-ourself check could use
      food buffs (see aion-4.8-consumables.md), so revisit protecting them there.

- [x] **NA-17 — Endpoint, persistence, and resume within the bridge.**
  - **Depends:** NA-16.
  - **Do:**
    - Add `CompleteAscensionLegAsync`. It asserts the endpoint contract, relogs,
      re-verifies (class, level, quests, bind, equipment, inventory, position) and quits.
    - Extend NI-08 resume to this leg:
      - Decision-engine tests cover resuming from every stage.
      - **One** snapshot run with two injected relogs proves it at runtime: one inside the
        instance before var 6, and one in Pandaemonium partway through Q2009.
  - **Done when:** that run reaches the endpoint. Any retained failure keeps its evidence
    package.
  - **Verify:** that snapshot run (see `run-natural-resume.ps1` for the NI-08 injection
    pattern).

  - **Evidence (2026-09-28):**
    - **Endpoint.** `CompleteAscensionLegAsync` runs on `bridge-complete`. It checks the
      endpoint contract from the client's view, then quits, logs back in and requires
      `NaturalJourneyPersistence.Verify` to pass before writing `bridge-completion.json`.
      The contract checks: Cleric, level at least 10, all four quests, Altgard, bound at
      700065, the staff worn, and alive. The relog check compares class, level, quests,
      bind point, equipment, inventory and position.
    - **Resume.** Any login past the Munin stop resumes the bridge runner, whether fresh or
      after an interruption. That covers Q2008 moved or done, the class, and a bridge-only
      map.
    - **Relog injection.** `NI08_RELOG_AT` now takes several boundaries (`,` or `;`), and
      the bridge endpoint checks that each one fired.
    - **SIM relogin.** It now uses the identity rules; it had been missed in NA-07.
    - **The run.** One run (`run/na17-resume/`, a restored `munin` copy with
      `NI08_RELOG_AT=2008:3:52,2009:3:2`) disconnected twice:
      - inside the Ataxiar trial (Q2008 var 52); on reconnect it re-entered and finished the
        trial;
      - in Pandaemonium mid-ceremony (Q2009 var 2); it resumed at Balder.

      It completed the whole bridge and passed the endpoint relog: class 10, level 10,
      Altgard, bind persisted, connection generation 3 → 4. Both disconnects left
      `failure.json` receipts of kind `disconnect`.
    - **Checks.** 214 Natural tests and the warning, null-logger and clock ratchets pass.
    - **For NA-23.** The LIVE journey scenario's post-journey relog check still expects the
      Ishalgen endpoint, so it needs the bridge endpoint when the bridge is enabled.

### Phase 5: Level 10 Cleric play (the start of that work)

- [x] **NA-18 — Cleric skill catalog, combat policy and resting with powder.**
  - **Depends:** NA-09.
  - **Do:** build on [Appendix C](#appendix-c-level-910-cleric-skills). Every effect type
    it needs already exists in C# and matches Java.
    - Add a Cleric catalog, always intersected with the observed `SM_SKILL_LIST`. Level
      alone grants nothing.
    - **Rotation:**
      - Smite → **Flashbolt** (the chain follow-up), before any other `_1TH` opener.
      - Then **Earth's Wrath**, **Slashing Wind**, Hallowed Strike and Infernal Blaze,
        chosen by cooldown and MP.
      - Then the staff.
    - **Heals:**
      - keep **Light of Rejuvenation** (a heal over time) up during fights;
      - Healing Light at the existing thresholds;
      - **Salvation** as the emergency heal when observed DP ≥ 2000. The Zeller Aether
        Jelly gives exactly 2000 DP.
    - **Resting with powder (OD-9).** The rest routine (`NaturalRestCadence` /
      `RestSafelyAsync`) uses **MP Recovery** (2 powder, about 299 MP) to recover mana and
      **Herb Treatment** (1 powder, 281 HP) to recover HP.
      - Use them only when no monster is engaged: any hit cancels the 4 s cast.
      - They share cooldown group 1153 (16 s), so alternate them, starting with whichever
        deficit is larger.
      - Sit only as a fallback: when out of powder, or when both are on cooldown and more
        recovery is still needed.
      - Keep about 30 powder, restocked at Donabe or through help items.
      - From level 25 the skills use Odella Powder 169300004 instead. The skill's rank
        decides the reagent, not the item level.
    - **Root** is a tool for retreating and kiting.
    - Ignore Light of Resurrection (the bot plays solo), the passives and Winged Recovery.
    - The observed class chooses the catalog. The Priest rotation and rest rules for the
      Ishalgen leg are unchanged.
  - **Done when:** pure policy tests cover:
    - chain order;
    - the shared 1153 cooldown;
    - the DP gate;
    - powder-gated rest and the fall back to sitting;
    - avoiding cast cancels;
    - Priest behavior unchanged.
  - **Verify:** the focused tests.
  - **Evidence (2026-09-28):**
    - `NaturalClericSkills` holds the eight level 10 actives, with ids, cooldown groups,
      ranges, MP, chains, DP and reagents checked against the shipped tree and templates.
      `ForClass` picks the catalog from the observed class; the Priest keeps
      `NaturalPriestSkills.All`.
    - `NaturalPriestCombatPolicy` gains, only for roles the Priest catalog lacks:
      - Salvation at an emergency when observed DP ≥ 2000;
      - Light of Rejuvenation kept up while being hit;
      - Smite opening for a ready Flashbolt, and Flashbolt first on an open chain;
      - Slashing Wind and Earth's Wrath (last at melee: a 1.5 s cast);
      - Root before a retreat.
    - The journey's combat tracks the chain from the cast result's chain flag (32; Java
      `SM_CASTSPELL_RESULT`) and observes DP and the heal-over-time effect.
    - `NaturalPowderRestPolicy` runs first in `RestAsync` for a Cleric. Herb Treatment and
      MP Recovery alternate on group 1153, starting with the larger deficit. The bot sits on
      the shared cooldown or without powder, and fights first when a hit cancels the cast
      (the defend code is shared with sitting).
    - Tests: `NaturalClericCombatPolicyTests`, 10 of 10. They include a Priest grid of 576
      states with identical choices, reasons and checks under both catalogs. Focused
      `Natural*` suite: 234 of 234 passed. The warning, null-logger and clock-read checks
      pass.
    - No SIM run: none of this can fire before the bridge ends. NA-23 is the first run
      that fights as a Cleric.

- [x] **NA-19 — A "buff ourself" check and help-item use.**
  - **Depends:** NA-18.
  - **Do:** add one **buff-ourself check**.
    - **When it runs:** before each pull or engage, after each rest, after a revive or
      relog, and at the start of each travel leg.
    - **What it reads:** the client-observed effects (`SM_ABNORMAL_STATE` and the effect
      list, by stack group), and the owned items with their use-delay groups.
    - **What it does:** applies whatever is missing or about to expire, and traces each
      decision.

    The rules use the items in [Appendix D](#appendix-d-help-items-scrolls):
    1. **Class buffs.** Keep Blessing of Guardianship up, as today.
    2. **Awakening, always (OD-15).** Whenever an **Awakening** scroll (casting speed,
       stack `ITEM_SPEED_BOOSTCASTINGTIME`, 5 min) is owned, keep one active.
       - Courage replaces it (both use effect id 30184), so the bot never uses Courage. If
         Courage is ever active, let it expire rather than wasting a scroll to swap.
       - Use the highest tier whose item level is at or below the character's level.
       - Refresh just before it expires, between casts.
    3. **Anti-Shock scroll at 50% HP (the "shield scroll").** Despite the name, the
       Anti-Shock family is **"All Damage Absorption"**: a damage shield.
       - Tiers, by item level: Lesser (20), plain (30), Greater (40), Major (50) and Fine
         (60). They absorb 158, 245, 338, 425 and 550 damage respectively and last 24 s.
         The use delay is 60 s, in group 32.
       - Detect an active shield by any `ITEM_SHIELD_ALL_*` stack. A lower tier can't
         replace an active higher one.
       - In combat, at or below 50% HP, use the highest owned tier whose item level is at or
         below the character's level + 10, if it is ready and no shield is active. At level
         10 that means Lesser (164000067). Altgard quest Q2206 also rewards 8 of these.
       - From level 40 the Cleric's own Blessed Shield conflicts with these scrolls. That
         only matters in later milestones.
       - The 30% retreat and emergency rules still take priority.
       - The potion (80%) and heal thresholds do not change.
       - If both the scroll and Salvation apply, use the scroll first, because Salvation
         spends DP.
    4. **Running speed on long journeys.** Use the right-tier **Running** scroll (stack
       `ITEM_SPEED_RUN`, 5 min, group 35) before a travel leg over a threshold, if one is
       owned and not already active.
       - Start the threshold at 150 m of planned route; any walk to another map also
         counts.
       - Don't waste one on a short hop.
    5. **Tea of Repose** stays out of combat only (OD-8).

    Constraints:
    - Respect every use-delay group from the item data:
      - 34 is shared by Courage and Awakening.
      - 35 is shared by Running and Movement Speed.
      - 32 is the Anti-Shock group, with a 60 s delay.
      - 31 is shared by the defense scrolls.
    - Use items only between casts, because any item use cancels the current cast.
    - Never use one while `WATCHING_CUTSCENE`, in flight, dead or stunned.
    - Running replaces the Movement Speed scroll and speed transforms (effect id 30182).
      Their effects never add together.
    - With no help items owned, the check finds nothing to use. The bot must work without
      them.
  - **Done when:** pure policy tests cover:
    - tier selection by level, with level + 10 for Anti-Shock;
    - one speed-scroll family only;
    - no item use in the middle of a cast;
    - the refresh window;
    - shared delay groups;
    - the 50% Anti-Shock trigger, ordered against potion, retreat and Salvation;
    - the long-journey threshold;
    - no use during cutscenes or flight;
    - unchanged behavior when nothing is owned.
  - **Verify:** the focused tests.
  - **Evidence (2026-09-28):**
    - `NaturalHelpItemPolicy` freezes the 15 Appendix D scrolls: item level, skill,
      use-delay group and time, duration, and the Java effect id that decides replacement.
      A test checks them against the shipped item and skill templates.
    - `DecideBuffs` keeps Awakening up (refreshed inside the last 20 s, with the snapshot's
      time aged) and never uses Courage, letting an active one expire. It uses Running only
      before a leg of 150 m or more, or to another map, and never on top of another speed
      effect.
    - `DecideShield` picks the Anti-Shock tier at or below level + 10, at 50% HP or lower,
      when no shield is active.
    - Every rule honors the use-delay groups and refuses mid-cast, in a cutscene, in flight,
      dead, stunned, or with effects unobserved. Each decision records one check per rule.
    - `NaturalPriestCombatPolicy` has a `shield-scroll` action. It comes after the swarm and
      30% retreats and before Salvation and the potions.
    - Runtime (Cleric only; the Priest's calls are identical): `BuffOurselfAsync` runs
      before pulls and after rest (with the Blessing upkeep, as before), after a revive, on
      a login or resume into the bridge, and at each bridge travel leg (the straight line is
      a lower bound of the route).
      - Each decision is traced as `buff-ourself` and each use as `help-item-used`.
      - The combat turn offers the shield when the policy chose one.
      - No flight or stun flag is observed yet: flights are scripted, and a stunned use is
        refused by the server and retried.
    - Tests: `NaturalHelpItemPolicyTests`, 15 of 15. Focused `Natural*` suite: 249 of 249
      passed. The warning, null-logger and clock-read checks pass.
    - Regression run from the Munin snapshot, `run/snapshots/_verify/na19-bridge`: the bridge
      completed and was verified across the relog (a level 10 Cleric bound in Altgard), with
      no deaths. There were eight `buff-ourself` TravelLeg decisions, and none used an item
      because none is owned. The login check is silent because the bot is still a Priest
      when the bridge starts.
    - Appendix D is corrected: Fine Anti-Shock has a required level of 50.

- [x] **NA-20 — Propose the help-item allowlist and how it is supplied (from the research).**
  - **Depends:** none.
  - **Research done:** [aion-4.8-consumables.md](aion-4.8-consumables.md) (2026-09-28)
    surveyed every consumable in the Java and C# data (they match byte for byte), the retail
    4.8 client on this machine, and aioncodex. Its findings:
    - **No NPC sells the buff scrolls** (Courage, Awakening, Running, Anti-Shock, Crit,
      Resist). In retail they came from Alchemy, quests and the broker, so keeping them up
      for hours needs supplied items.
    - **Lesser Physical Defense Scroll 164000015 is not a bug.** It did nothing in retail
      4.8 either: the retail client item has no skill, and nothing produces it. The same is
      true of 164000018–020. Leave them alone.
    - **Tier rule, stacking/delay conflicts, and a level 10–20 starter kit with a restock
      rule:** see its "Recommendations for the bot" and "Supply mechanisms".
  - **Do:** turn the report into a short proposal (Appendix D.2 of this doc) and ask the
    operator. It covers:
    1. **Allowlist.** Start from the report's kit and give ids per level band:
       - Awakening (OD-15), not Courage
       - Running
       - Anti-Shock
       - Life and Mana Serums
       - Lesser Odella Powder
       - Zeller Aether Jelly
       - optionally Revival Stones

       Foods and elixirs are still bought from vendors: foods are on the route, and elixirs
       come from Nirmirn.
    2. **Counts and restock.**
       - A starter kit, then "top up to N when below M".
       - Check stock at run start, each level-up, each town visit and each checkpoint.
       - At levels 20, 25 and 30, supply the new tier.
    3. **Mechanism.**
       - SIM: `ItemService.AddItem` in the fixture, limited to the allowlist.
       - LIVE, isolated stack only: the director account's `//add <player> <itemId> [count]`
         (access level 8). It leaves a gmaudit line, the director's trace and the
         subject's "received" message.
       - Never on the operator's `aion` stack.
    4. **Recording.** A `helpItems` block in the run profile and trace, and a dashboard
       badge.
  - **Done when:** the proposal is in this doc, and the operator's approval of the list,
    counts and mechanism is requested under "Blocked / questions for the operator".
  - **Verify:** a focused test pins the proposed ids against the static data: each exists,
    and its skill, delay group and item level match.
  - **Evidence (2026-09-28):**
    - The proposal is [Appendix D.2](#appendix-d2-proposed-help-items-na-20-awaiting-approval),
      and the approval request is under "Blocked / questions for the operator".
    - The ids are data in `NaturalHelpItemAllowlist.Proposed`. Nothing supplies them yet.
    - `NaturalHelpItemAllowlistTests` (3 of 3) pins each id's existence, skill, delay
      group, item level and required level. It also checks that the tier bands match the
      NA-19 tier rule and never overlap, and that Courage is absent.
    - **Found while checking the natural Cleric's endpoint inventory (`na19-bridge`):**
      - it already owns 50 **[Event] Rx: Castafodin** (casting speed) and 50 **Accelerox**
        (run speed) from the server's veteran rewards (Java `VeteranRewardService`, months
        26 and 30). They have the Lesser tiers' effect, last 30 min and need no level. That
        removes the need to supply Awakening or Running at levels 10–19.
      - It also owns things nothing uses yet: 106 Minor Mana Potions, 5 Zeller Aether
        Jellies (the Q2904 reward) and the 12 Lesser Life Elixirs the shop stop bought (the
        potion policy only knows 162000052).
      - Munin's Belt is listed as a kept accessory but is not worn.
      - All of these are about what the bot already owns, so they need no approval. They
        are the new item NA-20a.

- [ ] **NA-20a — Use what the natural Cleric already owns (found by NA-20).**
  - **Depends:** NA-19.
  - **Do:** Cleric only; the Ishalgen Priest is unchanged.
    - Add the veteran-reward event scrolls to the NA-19 catalog as the Awakening and Running
      families: Castafodin 164002118 (skill 10467, group 34, 30 min) and Accelerox
      164002116 (10465, group 35, 30 min).
      - They have the Lesser tiers' effect and no required level, so they count as the
        Lesser tier at any level.
      - A real tier at or below the level beats them from level 20.
      - Blitzopan (attack speed) is Courage's twin and is never used.
    - Recognize every owned life potion or elixir tier the bot bought, including Lesser
      Life Elixir 162000053. Use the owned Minor Mana Potions through the policy's existing
      `mana-potion` action.
    - Drink a Zeller Aether Jelly (group 23, 30 min delay) out of combat when observed DP is
      below 2000 and Salvation is learned, so that Salvation has its DP.
    - Wear Munin's Belt (the contract's `keptAccessories`): find why the gear code leaves
      the belt slot empty, and require every kept accessory to be worn at the endpoint.
  - **Done when:** policy tests cover each of these, and one Munin-snapshot bridge run
    shows the belt worn and the endpoint unchanged otherwise.
  - **Verify:** the focused tests and that one run.

- [ ] **NA-21 — Supply the approved help items.**
  - **Depends:** NA-20, **plus operator approval of its list (OD-13)**.
  - **Do:**
    - Implement the approved supply mechanism for SIM and for the isolated LIVE stack.
    - Supply only the approved ids and counts, and refuse any other item.
    - Record what was supplied in the run profile and trace.
    - Keep it switchable (e.g. `NA_HELP_ITEMS=0` for a clean natural run).
  - **Done when:** tests cover the allowlist, the counts and the profile record. One
    snapshot run shows the items arriving and NA-19 using them.
  - **Verify:** the focused tests and that one run.

- [ ] **NA-22 — Patrol timing as a Cleric: wait and retry, or take the fight (OD-14).**
  - **Depends:** NA-18, NA-19.
  - **Do:** replace the rule "wait up to a minute, then clear adds or walk in" with an
    explicit, traced decision whenever a patrol blocks a route, a pull spot or an
    objective. This applies to the Cleric; the frozen Ishalgen Priest is unchanged.
    - **Wait and retry.**
      - Hold at a safe spot outside every aggro circle for **15 s of game time**, then plan
        the path again. The patrol's learned path (`BotPatrolPath`) usually carries it away.
      - Bound it, e.g. four waits per blockage, then escalate.
    - **Take the fight.** Engage deliberately when the assessment says the patrol is
      winnable. The assessment weighs:
      - how many members the patrol has, their levels, and the adds at the pull spot
        (`NaturalPullPlanner.AddsAt`);
      - the bot's HP and MP;
      - heals and the heal over time ready, and Salvation or DP available;
      - buffs up (run NA-19 first);
      - potion stock.

      Pull at the best planned spot rather than letting the patrol walk into the bot.
    - **Otherwise, reroute** through another corridor.
    - **If it goes wrong,** the normal death recovery applies. The death is recorded, not
      failed (OD-12), and the retry budget is unchanged.
  - **Done when:** pure policy tests cover:
    - the wait timer (game clock);
    - the retry bound;
    - the winnable/not-winnable assessment;
    - the order of escalation;
    - Priest unchanged.
  - **Verify:** the focused tests.

- [ ] **NA-23 — Focused Cleric encounter check (diagnostic, one run).**
  - **Depends:** NA-22 (and NA-21 if the help items are approved by then).
  - **Do:**
    - Add a GM-prepared SIM encounter in the style of the Mau short starts. Diagnostic setup
      is allowed here.
    - The setup is a level 10 Cleric with the staff, the Appendix A supplies and any
      approved help items, placed near ordinary level 10–11 Altgard monsters outside the
      fortress (picked from the spawn data).
    - Run a single-attacker fight, a two-attacker fight and a patrol crossing.
    - Record from the trace:
      - skill use, including Flashbolt on an open Smite chain;
      - uptime of the heal over time, the buffs and the scrolls;
      - the 50% shield scroll;
      - Salvation;
      - powder rest;
      - the patrol decision;
      - deaths (recorded, not failed).
  - **Done when:** it runs cleanly once, and the findings are written here as the starting
    point for the Altgard milestone.
  - **Verify:** that run.

### Phase 6: Acceptance (once each; OD-11)

- [ ] **NA-24 — One clean bridge run from Munin.**
  - **Depends:** NA-17, NA-22.
  - **Do:** restore `munin` and run the whole bridge to the endpoint. Include the help
    items if they were approved by then (NA-21), and record them.
  - **Done when:** the run reaches the endpoint contract with no bot problems. Record the
    deaths (not a failure), recoveries, movies, buffs used, and time per quest
    (`scripts/sim/trace/step_times.py`, extended if needed).

- [ ] **NA-25 — One full SIM journey from creation to Altgard, saved as the `altgard` snapshot.**
  - **Depends:** NA-24.
  - **Do:**
    - Extend `scripts/sim/run-natural-batch.sh`, or add a flag, so it runs with the bridge
      enabled and reports its steps in the summary line.
    - Run seed 1.
    - Save the endpoint with `sim-snapshot.ps1 -Save altgard`.
  - **Done when:**
    - both legs complete (deaths recorded, not failed);
    - the Ishalgen leg completes all 41 quests;
    - the `altgard` snapshot restores.

    The results go into the status handoff.
  - **Verify:** `bash scripts/sim/run-natural-batch.sh na25 1` (or the new flag), and one
    restore of `altgard`.

- [ ] **NA-26 — Full checklist and checkpoint.**
  - **Depends:** NA-25.
  - **Do:**
    - Run every `CLAUDE.md` check, including Docker Fast (`scripts/e2e/run-fast.ps1`) and
      `NavBake check --maps baked`.
    - Update `docs/natural-ishalgen-status.md`, `docs/natural-ishalgen-journey.md` and this
      doc.
    - Commit.
  - **Done when:** all checks pass and the warning baseline has not risen.

- [ ] **NA-27 — One isolated LIVE run (OD-4).**
  - **Depends:** NA-26.
  - **Do:**
    - Run it on the NI-09-style isolated LIVE stack in its own compose project (D13): a
      fresh Priest, ordinary rates, the approved help items, the map monitor, and a final
      relog.
    - The run covers creation → Ishalgen → the bridge endpoint, about 4.5 hours.
    - Give it a manifest scenario id (e.g. `NA-LIVE`), or add a flag to NI-09. If it is a
      new id, add it to the P10-11 matrix.
    - At the endpoint, keep a dump of the isolated stack's database as the LIVE starting
      point in Altgard.
    - Never touch the `aion` stack.
  - **Done when:** the endpoint contract holds in LIVE, persistence is verified, and the
    evidence and the dump are kept under `run/`.
  - **Verify:** the `run-live.ps1` report and the kept evidence.

- [ ] **NA-28 — (Optional) watch it in the real client.**
  - With the authorized Computer Use workflow (NI-11 style), follow a LIVE bridge run
    with the `watch` movie mode: the flight, the trial, the ceremony movies and the Altgard
    bind.
  - Measure the movie lengths for NA-10's table, and keep screenshots.
  - This is not a gameplay oracle.

## Out of scope and boundaries

- **No new server content.** Do not add quests, spawns, rewards, vendors or teleports to
  make a step pass; D22's boundary carries over. Server defects are fixed Java-first, and
  the fix cites the Java lines.
- **No GM input to the natural character.** GM setup is allowed only in the focused
  scenarios NA-02 and NA-23, as in CAPITAL and the Mau starts. The exception is the approved help items (OD-13, NA-21), which are listed in the run profile.
- **No gear purchases** (OD-7).
- **Left for later milestones:**
  - Altgard quests beyond Q24010
  - Cleric play above level 10
  - gliding
  - the optional Pandaemonium quests
  - crafting
- **Other plans stay separate.**
  - NI-10's human-coexistence proof and NI-11 stay open in the Ishalgen plan; this bridge
    does not depend on them.
  - The level 9 Mau learning pilot stays paused, and its seeds 101–120 are spent.

## After the bridge

The next milestone applies the Ishalgen method to Altgard:
1. **Contract.** Freeze an Altgard quest contract from the shipped data, like NI-00: all
   naturally obtainable quests, with exclusions.
2. **Quest support.** Extend the plan compiler and the handlers.
3. **Leveling.** Level the Cleric on the NA-18–NA-23 policy: rotation, powder rest, buffs and help items, and patrol decisions.
4. **Starting point.** Start from the `altgard` snapshot (SIM) and the NA-27 database dump
   (LIVE) rather than replaying Ishalgen and the bridge.

Pandaemonium's optional quests fit a later capital pass. Each area milestone moves the
all-quests, account-to-endgame goal forward.

## How to work this list (loop protocol)

Each iteration:

1. **Orient.**
   - Read `CLAUDE.md`, then this doc's Operator decisions, Hazards and TODO list, then the
     latest Progress log lines.
   - Run `git status`.
   - Check whether a SIM or LIVE run from an earlier iteration is still going: look for
     `dotnet` processes and the newest `run/` folders.
   - **Never build or run the checks while a run holds the DLLs.** If a run is going, read
     its result or wait.
2. **Pick** the first unchecked item whose Depends are all ticked.
3. **Read the spec first.** Read the Java for any server behavior the item relies on. When
   C# and Java disagree, Java wins, subject to the retail-AI exception in `CLAUDE.md`.
4. **Do only that item.** Keep the Ishalgen leg unchanged unless the item says otherwise.
5. **Verify** with the item's commands, using one run per OD-11.
   - Before each commit, run `scripts/ci/check-warning-baseline.ps1`,
     `check-null-loggers.ps1` and `check-clock-reads.ps1`.
   - Run `scripts/e2e/run-fast.ps1` when an item changes server code.
   - Leave the full checklist for NA-26.
6. **Record.**
   - Tick the box and add a dated evidence line: run ids, numbers and the commit SHA.
   - Append one line to the Progress log.
   - Record failures honestly and keep the evidence under `run/`.
   - Never mark a skipped or blocked objective as done.
7. **Commit on `main` (OD-3).**
   - Stage only the files your item changed. Never `git add -A`, because other research
     files may be in the tree.
   - One commit per item, with an imperative subject line.
   - The body holds the evidence and ends with
     `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
   - Never push.
8. **If blocked** by an operator decision, a Java/C# divergence that needs judgment, or a
   design fork:
   - write it under "Blocked / questions for the operator";
   - move on to another unblocked item;
   - if none remain, stop the loop and report.

**Never:**
- create branches or worktrees;
- use GM commands on the natural character (the only exception is supplying the approved help items, OD-13, through NA-21's mechanism);
- touch the operator's `aion` compose stack (containers, images, database or volume);
- delete or replace retained characters;
- hand-edit generated data (`game-server/data/nav/`, pattern tables);
- raise the warning baseline;
- add server content;
- buy gear.

**One SIM scenario:** use `AION_SIM_DB_INTEGRATION=1 AION_SIM_TIER=Full AION_SIM_SCENARIO=<id>
AION_E2E_RUN_DIR=<dir with run.json> dotnet test tests/Aion.Simulation.Tests --filter
"FullyQualifiedName~ManifestScenariosRunInFixedProcessOrder"`. NA-02 has the pattern and `run/na02/`
has examples.

**Long runs:**
- A full SIM journey takes about 6–10 minutes of real time.
- A snapshot bridge run should take a few minutes.
- The isolated LIVE run takes hours.
- Run them in the background and check back rather than blocking.

## Blocked / questions for the operator

- **NA-20 → OD-13 (asked 2026-09-28):** please approve, change or reject [Appendix D.2](#appendix-d2-proposed-help-items-na-20-awaiting-approval):
  1. **The list and bands:**
     - Anti-Shock from level 10;
     - Life and Mana Serums;
     - Zeller Aether Jelly;
     - Awakening and Running from level 20 only, because the owned event scrolls cover 10–19;
     - optional: powder and Revival Stones.
  2. **The counts:** "top up to N when below M", checked at run start, level-up, town
     visits and checkpoints.
  3. **The mechanism:**
     - SIM: `ItemService.AddItem` in the fixture;
     - isolated LIVE: the director's `//add`;
     - never on `aion`.
  4. **The switch:** `NA_HELP_ITEMS=0` for a clean natural run.

  NA-21 waits for this. NA-20a needs no approval.
- The defaults for OD-8 and OD-10 stand unless the operator vetoes them.

## Progress log

- 2026-09-28 — Plan written from:
  - the Java 4.8 spec (`ce54b7931`);
  - C# parity audits: the route, trade lists, skill effects and cutscenes all match, and no
    test covers the Asmodian chain;
  - bot, navigation, shop, gear and skill audits.

  D25 is recorded and NA-00 is done (not committed).
- 2026-09-28 — Operator revisions:
  - equip accessories from earlier quests (already worn by the Ishalgen leg; now part of
    the contract);
  - no gear purchases, potions only;
  - the Karmic Staff instead of the Warhammer;
  - the four movies are handled by a watch/skip policy;
  - prove each step once, and save Altgard start snapshots for the next runs.

  NA-10 (movies) was added and later items renumbered to NA-24.
- 2026-09-28 — Operator revisions:
  - powder is kept and made part of resting (OD-9);
  - deaths are not failures (OD-12);
  - a Cleric may wait 15 s and retry, or take on a patrol (OD-14);
  - help items: a buff-ourself check, Courage always, a shield scroll at 50% HP, and
    Running on long journeys;
  - consumables may be supplied by cheating after an exploration and approval (OD-13).

  Phase 5 is now NA-18 to NA-23, and acceptance is NA-24 to NA-28.

- 2026-09-28 — The "shield scroll" is the **Anti-Shock** family (a damage-absorption
  shield), not a physical-defense scroll. Lesser Physical Defense Scroll 164000015 does
  nothing in both the C# and Java data (a possible upstream bug). A consumables research
  sub-agent was started; its output is [aion-4.8-consumables.md](aion-4.8-consumables.md)
  and it feeds NA-20. NA-00a was added: HEAD `e2f0599e9` broke the warning baseline
  (4,243 → 4,250, all in `SimulationMauCourseTests.cs`).

- 2026-09-28 — The consumables research landed in
  [aion-4.8-consumables.md](aion-4.8-consumables.md). Folded in:
  - 164000015 does nothing in retail 4.8 too, so it is not a bug.
  - Anti-Shock tiers absorb 158, 245, 338, 425 and 550, and use the level + 10 tier rule.
  - Courage and Awakening replace each other, which raises the new question OD-15.
  - Running does not add to other speed buffs.
  - No vendor sells the buff scrolls.
  - No consumable needs a weapon, and using an item cancels the current cast.
  - Powder becomes Odella Powder at level 25.

  NA-20 is now reduced to writing the proposal and requesting approval.

- 2026-09-28 — OD-15 decided: the Cleric keeps **Awakening** (casting speed) up and
  never uses Courage.

- 2026-09-28 — Loop: NA-00a done. The warning baseline passes again after 7 test-only fixes in `SimulationMauCourseTests.cs`.

- 2026-09-28 — Loop: NA-01 is done, with the contract fixture, its loader and 4 passing tests. The endpoint pins a minimum level of 10, not exact XP.

- 2026-09-28 — Loop: NA-02 done. CAPITAL-ASMO passes in SIM on the first clean run. The server carries the whole Asmodian bridge. The Doman fare is price-adjusted: 706 in SIM.

- 2026-09-28 — Loop: NA-03 is done. The `munin` snapshot was captured from a natural seed-1 run, then restored and verified. Snapshot tooling and its contract test were added.

- 2026-09-28 — Loop: NA-04 done. Navmeshes and travel graphs for the Ataxiar instance, Pandaemonium and Altgard are checked in; every route leg routes. Pandaemonium NPCs report `NoApproachPoint` for the interaction route (noted for NA-14).

- 2026-09-28 — Loop: NA-05 done. The monitor shows Pandaemonium, Altgard and the Ascension instance. The stale Ishalgen map catalog was refreshed.

- 2026-09-28 — Loop: NA-06 done. Per-map navigation contexts are in place, and the world model clears objects on map change. Cross-map bind revive works. The seed-1 Ishalgen regression passed.

- 2026-09-28 — Loop: NA-07 done. Identity accepts only the Ishalgen Priest or the bridge Cleric. Relog keeps the class and bind point.

- 2026-09-28 — Loop: NA-08 done. Bind, teleporter and vendor steps work on any map. The focused SIM test ran Doman's teleport, the Altgard bind and the Nirmirn/Donabe trades.

- 2026-09-28 — Loop: NA-09 done. There are now class-aware inventory rules: the Cleric keeps the staff, accessories and bridge supplies, and sells the replaced mace. The Q2009 reward is the staff. The Priest path is unchanged.

- 2026-09-28 — Loop: NA-10 done. Movie policy added: skip by default; watch holds the end packet and blocks movement until `NaturalMovieGate` finishes it.

- 2026-09-28 — Loop: NA-11 done. The bridge decision engine (16 table tests) is in place behind the `AscensionBridge` handoff flag. From the Munin snapshot it picks Munin as the first move.

- 2026-09-28 — Loop: NA-12 done. The bridge runner plays the Norn circuit from the Munin snapshot into Ataxiar (Q2008 var 99). It stops at Hagen for NA-13.

- 2026-09-28 — Loop: NA-13 done. From the Munin snapshot the bot flies Hagen's path, wins the trial (no retreats, no deaths), becomes a Cleric and leaves Ataxiar. Q2008 is complete.

- 2026-09-28 — Loop: NA-14 done. The Pandaemonium ceremony is complete: level 10, Karmic Staff equipped, Q2904 started. The run never came near the death level.

- 2026-09-28 — Loop: NA-15 done. From the Munin snapshot the bot finishes all four bridge quests. It took Doman's teleporter (706 Kinah), bound at Altgard Fortress (451 Kinah), then turned in to Meiyer and Suthran.

- 2026-09-28 — Loop: NA-16 done. **The bridge completes end to end from the Munin snapshot**: a level 10 Cleric bound in Altgard, all four quests done, staff and accessories worn, junk sold, 12 elixirs and 30 powder bought, and the tea drunk.

- 2026-09-28 — Loop: NA-17 done. The bridge endpoint is verified across a relog. The bridge resumed after interruptions inside the trial and mid-ceremony, then finished. **Phase 4 is complete.**

- 2026-09-28 — Loop: NA-18 done. The Cleric has a level 10 catalog and rotation (Smite → Flashbolt, Earth's Wrath, Slashing Wind), Salvation, the heal over time, Root before a retreat, and powder rest. The Priest's choices are unchanged. Phase 5 has started.

- 2026-09-28 — Loop: NA-19 done. The buff-ourself check keeps Awakening up, uses Running before long legs, and the Anti-Shock shield at 50% HP in combat (ordered before Salvation and potions). All are Cleric-only and do nothing when no scrolls are owned.

- 2026-09-28 — Loop: NA-20 done. The help-item proposal is Appendix D.2, and approval is asked under "Blocked". NA-21 waits for it.
  - The Cleric already owns veteran-reward event scrolls equivalent to Lesser Awakening and Running.
  - It also owns potions and jellies it never uses, and Munin's Belt is unworn.
  - These gaps became NA-20a, which needs no approval and is next.

## Appendix A: Altgard shops and consumables

**Merchants.** The fortress merchants below are all GENERAL, level 10 and Asmodian, with
`func_dialogs "2 3"`, so they sell and also buy any sellable item.

**Prices.**
- The buy price is the template price.
- Selling pays 20% of the template price.
- There are no sell or buy limits.
- The C# trade data is identical to Java.

| NPC | Name | Position | Distance from arrival / obelisk / Meiyer | Use on the bridge |
|---|---|---|---|---|
| 203579 | Donabe | 1758.07, 1779.11, 256.17 | 28 / 106 / 96 m | **Sell here.** Buy **Lesser Odella Powder 169300003 (15)** (OD-9). Lists 399, 1505 and 118 also hold Bandages, Power Shards, the Elemental Stone of Resurrection 164000010 and the Altgard Fortress Scroll 164000090; not needed. |
| 203576 | **Nirmirn** | 1618.83, 1916.97, 262.94 | 173 / 109 / 176 m | **Buy potions** (list 275): 162000052, **162000053**, 162000057, 162000058 |
| 203577 | Dortos | 1741.55, 1868.34, 257.09 | 63 / 99 / 142 m | Level 10 armor and the Archon Shield. **Not used: no gear purchases (OD-7).** Reference only. |
| 203578 | Lunber | 1739.34, 1873.50, 257.15 | 68 / 100 / 145 m | Archon Mace and Archon Staff. **Not used (OD-7).** |
| 203657 | Sarad | 1555.46, 1861.74, 272.61 | 205 / 113 / 160 m | 15-minute food buffs. Not used. |

Pandaemonium has no potion vendor. Its gear vendors are off the ceremony route and not
used.

**Consumables.** No consumable needs a weapon; `WeaponCondition` only checks cast
skills. Using an item cancels the current cast. A heal over time ticks 10 times in
20 s.

| Item | Price | Effect | Delay |
|---|---|---|---|
| 162000002 Minor Life Potion (skill 9889) | starter item | 407 HP | 30 s, group 11 |
| 162000052 Minor Life Elixir (10202) | 250 | 341 HP | 60 s, group 11 |
| **162000053 Lesser Life Elixir (10203)** | 450 | **616 HP**. It has no `restrict`, so a level 10 character can use it. | 60 s, group 11 |
| 162000057 Minor Mana Elixir (10207) | 250 | 550 MP. Not bought: it blocks HP potions. | 60 s, group 11 |
| 162000058 Lesser Mana Elixir (10208) | 450 | 902 MP. Not bought. | 60 s, group 11 |
| 162001057 Tea of Repose (10491) | reward, cannot be sold | +40% XP from Energy of Repose, from level 10 | 2 s, group 140 |
| 160002273 Zeller Aether Jelly (10164) | reward, sells for 970 | **+2000 DP**, exactly what Salvation needs. Keep it. | 30 min, group 23 |
| 169300003 Lesser Odella Powder | 15 | reagent: Herb Treatment uses 1, MP Recovery uses 2 | — |

## Appendix B: Equipment and what the gear code gets wrong

### What the character owns at the bridge

- **Ishalgen quest gear** (the bot's choices, from the `ni09-live-a4` and `ni10-a2`
  traces):
  - weapon: the Aldelle Mace 100100025;
  - armor: robe/cloth pieces up to Anturoon's Tunic 110101250;
  - accessories: the **Spirit Ring 122000869** (Q2004), the **Black Opal necklace
    121000749** (Q2119) and **Munin's Belt 123000864** (Q2123, chosen over Munin's Hat).
  - The accessories are already worn, and they must stay worn.
  - No Ishalgen quest gives earrings, and only one ring, so a second ring slot stays empty
    unless loot fills it.
- **The ceremony weapon:** Karmic Staff 101500498 (staff, two-handed, rare, level 10,
  58–88 damage, magic boost 260). It has no `restrict`, so its required level defaults to 1.
- **No other gear drops.** The trial NPCs drop nothing: Ataxiar is
  `drop_type="NONE"`.

### Class gating

- `restrict` has 17 per-class entries in `PlayerClass` order; **9 is PRIEST, 10 is CLERIC**.
- The weapon or armor *type* is gated by mastery skills
  (`Equipment.checkAvailableEquipSkills`).
- At level 9 the Cleric adds these masteries:
  - 46 Advanced Mace
  - 48 Advanced Leather
  - **49 Advanced Chain**
  - **50 Advanced Shield**
  - **89 Advanced Staff**
  - 106 Advanced Robe
- The armor-mastery bonus scales with the pieces that match: torso 30%, pants 25%,
  shoulders/gloves/boots 15% each.

### Bugs to fix in NA-09

**`NaturalIshalgenJourney.cs`**
- `:178-180` hard-codes PRIEST, ASMODIANS and MALE in `Describe`.
- `:169` never clears `refusedGear`.
- `:277` requires trade tab 721.

**`NaturalGearPolicy.cs`**
- `:86-103` ranks by `ItemLevel` only.
- It does not model two-handed weapons clearing the off hand.
- It collapses every main-hand-capable item to MainHand.
- It has no armor-type awareness.

**`NaturalIshalgenInventoryPolicy.cs`**
- `:13-15` `IsPriestGear` excludes STAFF, chain, shields and every accessory group, so
  **the staff would be sold**.
- `:16-26` has no sub-hand slot and no accessory slots.
- `:27` `UsableAt` caps the required level at 9.
- `:29-30` `GearScore` is dominated by restrict level.
- `:45` `Supplies` lacks 162000053, 162000057/058, 169300003 and 160002273.
- `:60-73` `ChooseReward` reads only `selectable_reward_item`, but Q2009 uses
  `priest_selectable_reward`.
- `:88-89` hard-codes restrict index 9.
- `:147-149` checks Priest skills only.

**`NaturalIshalgenPotionPolicy.cs`**
- `:8-18` knows only 162000002 and 162000052, their skills 9889 and 10202, and the Ishalgen
  vendors.

## Appendix C: Level 9–10 Cleric skills

**How they arrive.**
- All are autolearned; no books are needed.
- `learnNewSkills(9..level)` runs on the class change and on every level-up.
- Priest skills carry over, and the upgraded masteries do not remove the originals.

**Server support.**
- The skill-tree rows and templates in C# are identical to Java.
- Every effect, condition and action type used here exists in the C# skill engine and was
  diffed as faithful.
- Nothing is missing or stubbed.

**Priest skills already in `NaturalPriestSkills`:**
- 1838/1839 Healing Light
- 4012/4013 Smite: chain opener P_CHAINA_1TH_1; the chain never expires
- 1614/1615 Hallowed Strike
- 1684 Blessing of Guardianship
- 1814 Infernal Blaze

**New at level 9–10** (cooldowns in seconds):

| Id | Name | Kind | Key facts | Bot role |
|---|---|---|---|---|
| 46, 48, 49, 50, 89, 106 | Masteries | passive | weapon, armor and shield mastery | Gear gating only |
| 233 | Boost Magic Suppression I | passive | +100 magic resist | — |
| 363 | Winged Recovery I | passive | +10% healing while flying | — |
| 366 | Boost Healing I | passive | +4% healing | — |
| 246 | Herb Treatment | self | 4 s cast; cooldown 16 s (group 1153); heals 281; **1 powder**; any hit cancels it; no moving | Heal between fights |
| 249 | MP Recovery | self | 4 s cast; 16 s cooldown (**shares group 1153**); about 299 MP; **2 powders** | Recover MP between fights |
| 1699 | Light of Resurrection | dead player | range 15; 6 s cast | — (solo play) |
| 3922 | Salvation | self | instant; 60 s cooldown; **needs 2000 DP**; restores 50% MP, then 50% HP | Emergency heal; the Zeller jelly supplies the DP |
| 3939 | Light of Rejuvenation | self or target | range 23; instant; 24 MP; 5 s cooldown; heals 30 every 2 s ×15 = 450; a buff that can be dispelled | Keep it up in fights |
| 4025 | Flashbolt | attack | **chain follow-up to Smite**; range 25; instant; 34 MP; 10 s cooldown; 221 wind damage | The "followup" slot |
| 4061 | Slashing Wind | attack | opener C_CHAINC_1TH_1; range 25; instant; 39 MP; 16 s cooldown; 202 wind damage | Attack |
| 4083 | Earth's Wrath | attack | opener P_CHAIND_1TH_1; 1.5 s cast; 85 MP; 12 s cooldown; 110 earth damage + 129 every 3 s ×3 | Attack plus damage over time |
| 4127 | Root | control | range 25; instant; 47 MP; 10 s cooldown; roots for about 10 s; each hit has a 90% chance to break it | Retreat and kiting |

**Chain rule.** Hallowed Strike, Infernal Blaze, Slashing Wind and Earth's Wrath are all
`_1TH` openers, and each one resets an open Smite chain. To land Flashbolt, cast it
straight after Smite. Heals do not touch the chain.

## Appendix D: Help items (scrolls)

These are from the shipped data, and C# matches Java.
- **Required level:** only the Fine Anti-Shock Scroll (164000131) has a `restrict`, of
  level 50. That matches the level + 10 rule, which first picks it at level 50. The others
  default to 1 (corrected by NA-19). The tier names still map to intended levels (Lesser 10, plain 20, Greater
  30). NA-19 picks the highest tier whose item level is at or below the character's level.
  NA-20 confirms that choice and the stacking.
- **Use delay:** 15 s per use, shared within its use-delay group.

| Family | Tier (item level) | Id | Skill (level) | Effect | Duration | Stack group | Use-delay group |
|---|---|---|---|---|---|---|---|
| **Courage** (attack speed) | Lesser (10) | 164000071 | 9959 (1) | Attack speed up; scales with the tier | 5 min | `ITEM_SPEED_ATK` | 34 |
| | Courage (20) | 164000072 | 9959 (2) | | 5 min | | 34 |
| | Greater (30) | 164000073 | 9959 (3) | | 5 min | | 34 |
| Awakening (casting speed) | Lesser (10) / plain (20) / Greater (30) | 164000132 / 164000133 / 164000134 | 9965 (1–3) | Casting time down | 5 min | `ITEM_SPEED_BOOSTCASTINGTIME` (its own stack) | 34 (shared with Courage) |
| **Running** (run speed) | Lesser (10) / plain (20) / Greater (30) | 164000074 / 164000075 / 164000076 | 9960 (1–3) | Movement speed +% | 5 min | `ITEM_SPEED_RUN` | 35 |
| Movement Speed Scroll | (15) | 164000033 | 9943 (1) | +600 speed (a flat bonus) | 10 min | `ITEM_SCROLL_SPEED` | 35 |
| Defense | Lesser Physical Defense (10) | 164000015 | none | Also does nothing in retail 4.8: a leftover 1.x item that nothing produces. Not a bug. | — | — | 31 |
| | King of Beasts' Shield (20) | 164000039 | 9924 (2) | Physical defense +20 | 10 min | `ITEM_SCROLL_DEFEND_PHYSICAL` | 31 |
| | Lesser / plain Strike Resist (30 / 40) | 164000123 / 164000124 | 9966 (1–2) | Strike resist | — | — | 31 |
| **Anti-Shock (the "shield scroll")** | Lesser (20) / plain (30) / Greater (40) / Major (50) / Fine (60) | 164000067 / 068 / 069 / … | 9953 / 9954 / 9955 / 9956 / 9964 | **"All Damage Absorption"** shield absorbing 158 / 245 / 338 / 425 / 550 | 24 s | `ITEM_SHIELD_ALL_*` (one per tier) | 32, 60 s |

Notes:
- **Stacking (corrected by the research).**
  - Courage and Awakening **replace each other**: both use effect id 30184 in the same buff
    slot, so only one can be active (OD-15).
  - Running, the Movement Speed Scroll and the speed transforms also replace each other
    (effect id 30182).
- **Tier rule.** Use the highest tier whose item level is at or below the character's
  level. For Anti-Shock, allow up to level + 10, which matches when retail quests hand them
  out.
- **Sources.** No NPC sells any of these scrolls. They come from quests or are supplied
  (OD-13).
- **The "shield scroll" is Anti-Shock.** This was clarified 2026-09-28. The defense
  scrolls (group 31) are only a flat defense buff and are not the 50% HP tool.
- **Timed and event variants.** The Legion-reward, Coliseum, Blackstar, Abbey and Stamp
  variants exist with other ids and limits. NA-20 decides whether any of them is a better
  supply choice.

## Appendix D.2: Proposed help items (NA-20, awaiting approval)

**Status: proposed, not approved (OD-13).** Nothing supplies these items until the operator
approves them. The ids are pinned in `NaturalHelpItemAllowlist.Proposed` and its test.

**What the natural Cleric already owns at the bridge endpoint** (`na19-bridge`), without any
cheating:
- 50 [Event] Rx: Castafodin 164002118: casting speed +3% for 30 min, group 34, the same
  effect as Lesser Awakening.
- 50 [Event] Rx: Accelerox 164002116: run +10% for 30 min, group 35, the same effect as
  Lesser Running.
- 50 [Event] Rx: Blitzopan 164002117: attack speed, the Courage twin; unused under OD-15.

These all come from the Java veteran rewards, and together they cover 25 hours of each
buff. It also owns 106 Minor Mana Potions, 32 Minor Life Potions, 10 Minor and 12 Lesser
Life Elixirs, 5 Zeller Aether Jellies (from Q2904) and 30 Lesser Odella Powder. NA-20a
makes the bot use these.

**1. Allowlist and counts.** Stock is checked at run start, after each level-up, at each
town visit and at each checkpoint. Each id is topped up to N when the bot owns fewer than M.
When a band changes, the new tier is supplied and the old stock is left to run out.

| Family | 10–19 | 20–29 | 30–39 | Top up to N / when below M | Why |
|---|---|---|---|---|---|
| Awakening (OD-15) | — (use Castafodin) | 164000133 | 164000134 | 60 / 15 | +6% and +9% beat the event scroll's +3% |
| Running | — (use Accelerox) | 164000075 | 164000076 | 20 / 5 | +20% and +30% beat +10% |
| Anti-Shock | 164000067 | 164000068 | 164000069 | 30 / 8 | The 50% HP shield; no vendor sells it |
| Life Serum (instant, 30 s delay) | 162000012 | 162000013 | 162000014 | 30 / 10 | Drops only |
| Mana Serum (instant, 30 s delay) | 162000017 | 162000018 | 162000019 | 40 / 10 | MP gates a Cleric's damage; drops only |
| Zeller Aether Jelly (DP +2000, 30 min delay) | 160002273 | 160002273 | 160002273 | 8 / 2 | Salvation's DP; Q2904 gives only 5 |
| *Optional:* powder | 169300003 | 169300003, then 169300004 from 25 | 169300004 | 200 / 50 | Vendors sell it (OD-9 buys it); supply only to save shop trips |
| *Optional:* Revival Stone | 161001001 | 161001001 | 161001001 | 3 / 1 | Not recommended: bind revive is fine (OD-12) |

Not proposed:
- Courage (OD-15).
- Foods and elixirs: they are bought on the route.
- The defense, crit and resist scrolls.
- The Kisks, candies and Tea of Repose.
- The other event, cash and timed variants.

**2. Mechanism.**
- **SIM:** after login, the fixture calls `ItemService.AddItem(player, id, count,
  allowInventoryOverflow: true)` for the allowlisted ids only, in the same way the Mau
  course fixture does.
- **LIVE, isolated stack only:** the director account runs `//add <player> <itemId>
  [count]` (access level 8). This leaves the director's trace step, the gmaudit line and
  the subject's "received" message.
- **Never on the operator's `aion` stack.** Any id outside the approved list is refused.
- **Switch:** `NA_HELP_ITEMS=0` gives a clean natural run with no supply.

**3. Recording.**
- A `helpItems` block in the run profile: each supplied id, the count, the level and the
  trigger.
- A `help-item-supplied` trace line for each top-up.
- A dashboard badge showing "supplied help items: on".
