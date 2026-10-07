# Spawn review, October 2026: 5.8 conditional spawns and repeated spots

Research only. Nothing in this document is decided, and nothing in it has been changed except
finding 1, which is fixed. It exists so that the maintainer can decide and a later session can do
the work. Written 2026-10-07 from a census of a freshly started SIM world at commit `23e370c6f`.

Read first: `CLAUDE.md` (Java is the spec; retail wins only under a logged decision; the retail
dump is 5.8 and this port is 4.8), then the log entry "Two Ishalgen trainers stood twice" in
`docs/retail-ai-fidelity.md`.

## 1. Summary

The maintainer's intent for spawns: use NCSoft's 5.8 data to **remove bad duplicate spawns from the
Java 4.8 data**. Only Ishalgen and part of Altgard were edited that way.

A second thing puts 5.8 data into the world, and it was not part of that intent. On 2026-08-20 the
retail AI work added a loader for the **conditional spawn groups** of the 5.8 world files. It runs
on every map. In a fresh world it places 262 npc objects on 14 maps, and
more when a counter changes or an instance is created.

| # | Finding | Size | State |
|---|---|---|---|
| 1 | Ishalgen's two new-class trainers stood twice | 2 npcs, 10 objects | Fixed, at Java's spots |
| 2 | Rian (Fast Track npc) floats in Ishalgen's village | 1 npc, 5 channels | Open |
| 3 | Named npcs stand twice, at Java's spot and at retail's | 13 npcs, 14 extra objects | Open |
| 4 | Other 5.8 npcs placed in a fresh world | 243 objects on 9 maps | Open |
| 5 | Npcs that will stand twice when their gate opens | 97 npc and map pairs | Open, not visible yet |
| 6 | Housing merchants not marked as already spawned | 46 objects | Open, not visible yet |
| 7 | Instances get conditional spawns too | 5,734 rows on instance maps | Not measured |
| 8 | Java's own data lists one npc more than once on one spot | 51 spots live at start; 1,532 in the files | Open; this is the original goal |
| 9 | Tooling gaps that let finding 1 happen | 4 | One closed, three open |

## 2. Where an npc in the world can come from

1. **Java's static spawns**, ported: `game-server/data/static_data/spawns/**/*.xml`. Loaded by
   `SpawnEngine.SpawnAll()`. Equal to Java's files except where a logged change says otherwise.
2. **5.8 placement passes over those files**, by hand and by script, in Ishalgen (2026-09-23 to
   2026-09-29) and Altgard (2026-10-01). These edit the files of source 1. History is in
   `docs/natural-ishalgen-status.md`.
3. **5.8 conditional spawn groups**: `spawns/gated/gated_spawns.tsv`, made by
   `tools/client-extract/extract_gated_spawns.py` from the `<condition_info>` blocks of NCSoft's
   world files (`D:/Aion58ServerTesting/Server/Map/Worlds/*/world.xml`). Loaded by
   `GatedSpawnService.Start()` from `GameServerBootstrapService`, straight after source 1. Java has
   nothing like it. Logged in `docs/retail-ai-fidelity.md` ("The conditional spawn engine runs" and
   the three entries after it).
4. **Java's town spawns** (housing villages, by town level) and its siege, base, rift and event
   spawns. Not examined here except where they touch source 3.

### How source 3 decides what to place

- A row is one npc at one point behind one gate, for example `SpecialServer_Cond == 0`.
- A gate reads named counters. A counter nobody has written is 0. So `X == 0` holds from the start,
  and `X == 1` holds once something writes 1.
- Counters are written by retail patterns: when an npc wakes (`WakeVariables.cs`), on a timer
  (`IdleCycles.cs`) or when an npc dies.
- `despawn_at_other = TRUE` means the npc is removed again when the gate stops holding.
- **The duplicate guard is one column, `overlaps_static`.** The extractor sets it to TRUE when this
  port's static files already have the same npc id within 5 m in x and in y. The loader skips TRUE
  rows. The column is computed when the file is generated, not when the server starts.

| | Rows |
|---|---|
| In the file | 21,096 |
| Marked as already spawned, skipped | 6,802 |
| Not marked | 14,294 on 91 maps (four sit behind gates that do not parse, so 14,290 load) |
| of those on open-world maps | 8,560 |
| of those on instance maps | 5,734 |
| Gate holds with every counter at 0 | 617 |
| Placed in a fresh SIM world, all channels | 262 |

Loadable rows on open-world maps:

| Map | Loadable rows |
|---|---|
| Levinshor 600100000 | 1979 |
| Disillon 400060000 | 1127 |
| Aspida 400040000 | 1126 |
| Atanatos 400050000 | 1126 |
| Oriel 700010000 | 775 |
| Pernon 710010000 | 775 |
| Kaldor 600090000 | 415 |
| Belus 400020000 | 237 |
| Eltnen 210020000 | 164 |
| Beluslan 220040000 | 135 |
| Enshar 220080000 | 115 |
| Cygnea 210070000 | 105 |
| Theobomos 210060000 | 100 |
| Brusthonin 220050000 | 98 |
| Reshanta 400010000 | 77 |
| Heiron 210040000 | 62 |
| Verteron 210030000 | 39 |
| Altgard 220030000 | 39 |
| Transidium Annex 400030000 | 16 |
| Inggison 210050000 | 9 |
| Sanctum 110010000 | 8 |
| Kaisinel Academy 110070000 | 8 |
| Pandaemonium 120010000 | 8 |
| Marchutan Priory 120080000 | 8 |
| Morheim 220020000 | 3 |
| Live Party Concert Hall 600080000 | 3 |
| Ishalgen 220010000 | 2 |
| Gelkmaros 220070000 | 1 |

## 3. Findings

### Finding 1. Ishalgen's two new-class trainers stood twice (fixed)

Wild Wilhelm 801218 and Sona 801219. The file was generated on 2026-08-20 with the trainers at
Java's spots, so their 5.8 rows were not marked. On 2026-09-26 the static spawns were moved onto
retail's spots (`49663a7a8`) and the file was not regenerated: two objects on one spot, the second
2.4 m in the air.

Fixed in `6bdb96d66` and `23e370c6f`. Both are at Java's 4.8 spots again. The extractor lists them
in `KEPT_AT_JAVA_SPOT`, so the 5.8 rows stay marked although they are 33 m and 39 m away.

### Finding 2. Rian floats in Ishalgen's village

- Npc 801034, "Rian (Fast Track Spawn)". His static spawn at (558.53, 2414.27, 278.69) is commented
  out in Java and in this port.
- The 5.8 row places him in every channel at (573.956, 2416.837, 284.301), behind
  `(InterServer_Cond == 0) && (SpecialServer_Cond == 0)`.
- The village floor nearby is at about 278.5 to 278.7, so he stands about 5.6 m above it.
- Options: mark the row as not wanted; or place him on the floor; or leave him.

### Finding 3. Named npcs that stand twice

Each has exactly one static spawn, at Java's spot, and one 5.8 row somewhere else on the same map
that is placed at world start. Both objects are in the world now.

| Map | Npc | Name | Java's spot (static) | 5.8 spot (conditional) | Apart | Gate |
|---|---|---|---|---|---|---|
| Kaisinel Academy 110070000 | 207019 | Scobi | (547.07, 246.94, 127.29) | (561.53, 316.71, 126.81) | 71.3 m | `SpecialServer_Cond == 0` |
| Kaisinel Academy 110070000 | 207020 | Marre | (547.08, 259.10, 127.29) | (559.58, 335.09, 126.81) | 77.0 m | `SpecialServer_Cond == 0` |
| Kaisinel Academy 110070000 | 207021 | Liyre | (548.82, 246.92, 127.29) | (563.09, 318.39, 126.78) | 72.9 m | `SpecialServer_Cond == 0` |
| Kaisinel Academy 110070000 | 207022 | Marion | (548.81, 259.06, 127.29) | (562.01, 332.80, 126.78) | 74.9 m | `SpecialServer_Cond == 0` |
| Pandaemonium 120010000 | 832827 | Peja | (1403.28, 1439.12, 208.10) | (1412.85, 1439.31, 208.90) | 9.6 m | `(InterServer_Cond == 0) && (SpecialServer_Cond == 0)` |
| Marchutan Priory 120080000 | 207025 | Shalvia | (554.40, 292.70, 93.51) | (497.32, 307.19, 94.17) | 58.9 m | `SpecialServer_Cond == 0` |
| Marchutan Priory 120080000 | 207026 | Lonkus | (543.90, 292.87, 93.51) | (475.58, 307.24, 94.17) | 69.8 m | `SpecialServer_Cond == 0` |
| Marchutan Priory 120080000 | 207027 | Nodor | (554.41, 294.52, 93.51) | (495.55, 308.71, 94.11) | 60.5 m | `SpecialServer_Cond == 0` |
| Marchutan Priory 120080000 | 207028 | Dorsi | (543.96, 294.60, 93.51) | (477.10, 308.66, 94.11) | 68.3 m | `SpecialServer_Cond == 0` |
| Brusthonin 220050000 | 214392 | Elder Spirit Warrior | (1555.32, 2867.82, 76.53) | (1542.00, 2866.95, 82.00) | 13.3 m | `DirectPortalDest_41 == 0` |
| Brusthonin 220050000 | 214392 | Elder Spirit Warrior | (1555.32, 2867.82, 76.53) | (1656.46, 2893.73, 72.00) | 104.4 m | `DirectPortalDest_41 == 0` |
| Reshanta 400010000 | 206314 | Varina | (2979.64, 882.54, 1546.09) | (2368.81, 513.99, 1547.00) | 713.4 m | `SpecialServer_Cond == 0` |
| Reshanta 400010000 | 206315 | Kanzat | (928.40, 2985.69, 1646.40) | (434.42, 2680.72, 1647.00) | 580.5 m | `SpecialServer_Cond == 0` |
| Reshanta 400010000 | 700551 | Rift | (2245.72, 2194.77, 2191.17) | (2417.33, 2521.64, 1436.40) | 369.2 m | `SpecialServer_Cond == 0` |

For each, the question is the one answered for the trainers: keep Java's spot (add the pair to
`KEPT_AT_JAVA_SPOT`), or move to retail's spot (move the static spawn; the row is then marked by the
5 m check). 214392 may be different: retail may simply have three of them.

### Finding 4. Everything else the 5.8 table places in a fresh world

Channel 1. "Static" is how many static spawn objects of the same npc id the map already has.

| Map | Npc | Name | Objects | Static | Gate |
|---|---|---|---|---|---|
| Enshar 220080000 | 219693 | Skyscale Cellatu | 1 | 11 | `directpotal_p1 == 1` |
| Enshar 220080000 | 219694 | Adolescent Skyscale Cellatu | 2 | 11 | `directpotal_p1 == 1` |
| Enshar 220080000 | 219695 | Mature Skyscale Cellatu | 6 | 17 | `directpotal_p1 == 1` |
| Enshar 220080000 | 219696 | Bluefin Skyray | 2 | 5 | `directpotal_p1 == 1` |
| Enshar 220080000 | 219697 | Hungry Bluefin Skyray | 2 | 0 | `directpotal_p1 == 1` |
| Enshar 220080000 | 219698 | Mature Bluefin Skyray | 14 | 7 | `directpotal_p1 == 1` |
| Enshar 220080000 | 219774 | Petrified Jotun Worker | 10 | 0 | `directpotal_i == 1` |
| Enshar 220080000 | 219775 | Petrified Jotun Craftsman | 10 | 0 | `directpotal_i == 1` |
| Enshar 220080000 | 219779 | Young Roundshell Spiner | 3 | 0 | `directpotal_p2 == 1` |
| Enshar 220080000 | 219780 | Adolescent Roundshell Spiner | 2 | 0 | `directpotal_p2 == 1` |
| Enshar 220080000 | 219781 | Roundshell Spiner | 15 | 0 | `directpotal_p2 == 1` |
| Enshar 220080000 | 219782 | Timeworn Sea Giant | 2 | 0 | `directpotal_p2 == 1` |
| Enshar 220080000 | 219783 | Ancient Sea Giant | 2 | 0 | `directpotal_p2 == 1` |
| Enshar 220080000 | 219784 | Venerable Sea Giant | 16 | 0 | `directpotal_p2 == 1` |
| Enshar 220080000 | 219998 | Prime Tarmat I | 1 | 0 | `WORLDRAID_01 == 1` |
| Enshar 220080000 | 219999 | Prime Benoid I | 1 | 0 | `WORLDRAID_02 == 1` |
| Enshar 220080000 | 220000 | Prime Eiter I | 1 | 0 | `WORLDRAID_03 == 1` |
| Enshar 220080000 | 702548 | (no name) | 25 | 0 | `WORLDRAID_01 == 1`; `WORLDRAID_02 == 1`; `WORLDRAID_03 == 1` |
| Cygnea 210070000 | 220001 | Prime Tarmat I | 1 | 0 | `WORLDRAID_01 == 1` |
| Cygnea 210070000 | 220002 | Prime Benoid I | 1 | 0 | `WORLDRAID_02 == 1` |
| Cygnea 210070000 | 220003 | Prime Eiter I | 1 | 0 | `WORLDRAID_03 == 1` |
| Cygnea 210070000 | 235829 | Gatorback Skilex | 20 | 29 | `directpotal_p1 == 1` |
| Cygnea 210070000 | 235911 | Stygian Mist Warrior | 10 | 0 | `directpotal_i == 1` |
| Cygnea 210070000 | 235913 | Stygian Mist Protector | 10 | 0 | `directpotal_i == 1` |
| Cygnea 210070000 | 702548 | (no name) | 24 | 0 | `WORLDRAID_01 == 1`; `WORLDRAID_02 == 1`; `WORLDRAID_03 == 1` |
| Eltnen 210020000 | 219618 | Elyos Gazing Ashulagen | 6 | 0 | `SpecialServer_Cond == 0` |
| Eltnen 210020000 | 800509 | Rogan | 1 | 0 | `(InterServer_Cond == 0) && (SpecialServer_Cond == 0)` |
| Eltnen 210020000 | 831338 | Elyos Field Gun | 13 | 0 | `SpecialServer_Cond == 0` |
| Beluslan 220040000 | 213010 | Blue Jaw Monitor | 1 | 0 | `DirectPortalDest_19 == 0` |
| Beluslan 220040000 | 213074 | Pretor Scout | 2 | 0 | `DirectPortalDest_22 == 0` |
| Beluslan 220040000 | 213315 | Baby Crystalhorn | 2 | 10 | `DirectPortalDest_24 == 0` |
| Beluslan 220040000 | 700565 | Indratu Fortress Abyss Gate | 1 | 0 | `(InterServer_Cond == 0) && (SpecialServer_Cond == 0)` |
| Beluslan 220040000 | 730070 | Draupnir Cave Back Exit | 1 | 0 | `SpecialServer_Cond == 0` |
| Beluslan 220040000 | 831339 | Asmodian Field Gun | 7 | 0 | `SpecialServer_Cond == 0` |
| Brusthonin 220050000 | 214383 | Grave Bloodwing | 1 | 14 | `DirectPortalDest_41 == 0` |
| Brusthonin 220050000 | 214385 | Unfest Grave Keeper | 1 | 6 | `DirectPortalDest_41 == 0` |
| Brusthonin 220050000 | 214391 | Elder Spirit Warrior | 1 | 10 | `DirectPortalDest_41 == 0` |
| Brusthonin 220050000 | 214395 | Elder Spirit Healer | 1 | 0 | `DirectPortalDest_41 == 0` |
| Brusthonin 220050000 | 214397 | Elder Spirit Archer | 1 | 5 | `DirectPortalDest_41 == 0` |
| Brusthonin 220050000 | 214398 | Elder Spirit Archer | 1 | 0 | `DirectPortalDest_41 == 0` |
| Brusthonin 220050000 | 214400 | Elder Spirit Mage | 1 | 2 | `DirectPortalDest_41 == 0` |
| Brusthonin 220050000 | 214402 | Undead Grunt | 2 | 70 | `DirectPortalDest_41 == 0` |
| Brusthonin 220050000 | 214405 | Weathered Dracolich | 1 | 0 | `DirectPortalDest_41 == 0` |
| Levinshor 600100000 | 235174 | Sheban Spellslinger | 1 | 8 | `v01..v11 == 1` |
| Levinshor 600100000 | 235177 | Sheban Hidestitcher | 8 | 8 | `v01..v11 == 1` |
| Transidium Annex 400030000 | 804106 | Belus Staging Area | 1 | 0 | `DirectPortalDest_69 < 1` |
| Transidium Annex 400030000 | 804108 | Aspida Staging Area | 1 | 0 | `DirectPortalDest_70 < 1` |
| Transidium Annex 400030000 | 804110 | Atanatos Staging Area | 1 | 0 | `DirectPortalDest_71 < 1` |
| Transidium Annex 400030000 | 804112 | Disillon Staging Area | 1 | 0 | `DirectPortalDest_72 < 1` |
| Heiron 210040000 | 211871 | Fanged Worg | 1 | 2 | `DirectPortalDest_26 == 0` |
| Heiron 210040000 | 219100 | Skinripper | 1 | 0 | `DirectPortalDest_32 == 0` |
| Heiron 210040000 | 700360 | Heiron Abyss Gate | 1 | 0 | `InterServer_Cond == 0` |
| Inggison 210050000 | 215735 | Sandstorm Obscura | 1 | 35 | `WORLDRAID_01 == 0` |

What opens the gates that read `== 1`:

- `directpotal_p1`, `directpotal_p2`, `directpotal_i`: written to 1 when npc 804819
  (`F5_DirectPotalMonster_Set_01`) wakes. It has a static spawn in Cygnea and in Enshar.
- `WORLDRAID_01` to `WORLDRAID_03`: written to 1 by wake patterns in
  `pattern_tables/wake_idle_patterns.xml`; `IdleCycles.cs` later writes 2.
- `v01` to `v11` in Levinshor: written to 1 when 855138 (`LDF4_Advance_condition_14`) wakes. It has
  a static spawn in Levinshor.

So all three world raids and all three portal waves are up at once from the first second, on both
maps. Whether retail runs them together or one at a time on a schedule was not established.

Options, per map or per group: keep; mark as not wanted in a 4.8 world; or keep but leave the
counter unwritten until a schedule exists.

### Finding 5. Npcs that will stand twice when their gate opens

The same shape as finding 3, but the gate does not hold yet, so nothing is visible in a fresh
world. An npc id with exactly one static spot on a map, and at least one loadable 5.8 row for it on
that map more than 5 m away: **97 pairs, 172 rows.** 14
rows are placed at start (finding 3). 84 rows are on instance maps.

Open-world maps, not placed at start:

| Map | Npc | Name | 5.8 rows | From the static spot | Static file | Gates |
|---|---|---|---|---|---|---|
| Heiron 210040000 | 801187 | Kalamis | 1 | 14 m | Bases | `LF3_2_Village==2` |
| Heiron 210040000 | 801188 | Kariton | 1 | 17 m | Bases | `LF3_2_Village==2` |
| Inggison 210050000 | 799010 | Wivius | 1 | 89 m | Npcs | `[SAVE]lf4_v06 == 2` |
| Inggison 210050000 | 799017 | Sulinia | 1 | 22 m | Npcs | `[SAVE]lf4_v07 == 2` |
| Inggison 210050000 | 799019 | Yereos | 1 | 11 m | Npcs | `[SAVE]lf4_v07 == 2` |
| Beluslan 220040000 | 207800 | Garrison Patrol | 1 | 1738 m | Npcs | `(DF3_RVR_Control == 1) || (DF3_RVR_Control == 2)` |
| Beluslan 220040000 | 801200 | Iophon | 1 | 12 m | Bases | `DF3_2_Village==2` |
| Beluslan 220040000 | 801201 | Alectas | 1 | 7 m | Bases | `DF3_2_Village==2` |
| Beluslan 220040000 | 801204 | Sigrid | 1 | 15 m | Bases | `DF3_2_Village==3` |
| Brusthonin 220050000 | 209472 | Baltasar Hill Field Gun | 1 | 178 m | Rifts | `DirectPortalDest_41 == 1` |
| Reshanta 400010000 | 730892 | Oseanos | 1 | 714 m | Sieges | `(L_BuildupLevel >= 6) && (Crotan_Li <= 0)` |
| Reshanta 400010000 | 730893 | Grendyr | 1 | 714 m | Sieges | `(D_BuildupLevel >= 6) && (Crotan_Da <= 0)` |
| Reshanta 400010000 | 730894 | Ombrios | 1 | 731 m | Sieges | `(L_BuildupLevel >= 6) && (Dkisas_Li <= 0)` |
| Reshanta 400010000 | 730895 | Danan | 1 | 731 m | Sieges | `(D_BuildupLevel >= 6) && (Dkisas_Da <= 0)` |
| Reshanta 400010000 | 730896 | Iaphetus | 1 | 714 m | Sieges | `(L_BuildupLevel >= 6) && (Lamiren_Li <= 0)` |
| Reshanta 400010000 | 730897 | Bress | 1 | 714 m | Sieges | `(D_BuildupLevel >= 6) && (Lamiren_Da <= 0)` |
| Aspida 400040000 | 277751 | Aspamon Scout Slayer | 3 | 194 m | Npcs | `(v01 == 1) && (GAb1_PvPStatus == 3)`; `(v01 ==11) && (GAb1_PvPStatus == 3)` and 1 more |
| Aspida 400040000 | 880672 | Aspatyr Slayer | 3 | 192 m | Npcs | `(v02 == 1) && (GAb1_PvPStatus == 3)`; `(v02 == 11) && (GAb1_PvPStatus == 3)` and 1 more |
| Aspida 400040000 | 880700 | Aspar Scout Slayer | 3 | 207 m | Npcs | `(v03 == 1) && (GAb1_PvPStatus == 3)`; `(v03 == 11) && (GAb1_PvPStatus == 3)` and 1 more |
| Aspida 400040000 | 880728 | Aspal Scout Slayer | 3 | 182 m | Npcs | `(v04 == 1) && (GAb1_PvPStatus == 3)`; `(v04 == 11) && (GAb1_PvPStatus == 3)` and 1 more |
| Atanatos 400050000 | 277758 | Atasin Scout Slayer | 3 | 187 m | Npcs | `(v01 == 1) && (GAb1_PvPStatus == 3)`; `(v01 == 6) && (GAb1_PvPStatus == 3)` and 1 more |
| Atanatos 400050000 | 880680 | Atair Slayer | 3 | 216 m | Npcs | `(v02 == 1) && (GAb1_PvPStatus == 3)`; `(v02 == 16) && (GAb1_PvPStatus == 3)` and 1 more |
| Atanatos 400050000 | 880708 | Atangal Scout Slayer | 3 | 198 m | Npcs | `(v03 == 1) && (GAb1_PvPStatus == 3)`; `(v03 == 16) && (GAb1_PvPStatus == 3)` and 1 more |
| Atanatos 400050000 | 880736 | Ataj Scout Slayer | 3 | 197 m | Npcs | `(v04 == 1) && (GAb1_PvPStatus == 3)`; `(v04 == 16) && (GAb1_PvPStatus == 3)` and 1 more |
| Disillon 400060000 | 277765 | Disilgot Scout Slayer | 3 | 162 m | Npcs | `(v01 == 1) && (GAb1_PvPStatus == 3)`; `(v01 == 6) && (GAb1_PvPStatus == 3)` and 1 more |
| Disillon 400060000 | 880688 | Disilveir Scout Slayer | 3 | 199 m | Npcs | `(v02 == 1) && (GAb1_PvPStatus == 3)`; `(v02 == 11) && (GAb1_PvPStatus == 3)` and 1 more |
| Disillon 400060000 | 880716 | Disiltroll Scout Slayer | 3 | 208 m | Npcs | `(v03 == 1) && (GAb1_PvPStatus == 3)`; `(v03 == 11) && (GAb1_PvPStatus == 3)` and 1 more |
| Disillon 400060000 | 880744 | Disilheim Scout Slayer | 3 | 182 m | Npcs | `(v04 == 1) && (GAb1_PvPStatus == 3)`; `(v04 == 11) && (GAb1_PvPStatus == 3)` and 1 more |
| Kaldor 600090000 | 234087 | Stonereach Garrison Healer | 3 | 367 to 422 m | Bases | `(v01 == 2) && (7011on == 1)` |
| Kaldor 600090000 | 234088 | Stonereach Squad Attendant | 3 | 367 to 422 m | Bases | `(v01 == 3) && (7011on == 1)` |
| Kaldor 600090000 | 234102 | Flamecrest Garrison Healer | 3 | 374 to 430 m | Bases | `(v02 == 2) && (7011on == 1)` |
| Kaldor 600090000 | 234103 | Flamecrest Squad Attendant | 3 | 374 to 430 m | Bases | `(v02 == 3) && (7011on == 1)` |
| Kaldor 600090000 | 252310 | Elyos Gate | 1 | 434 m | Mercenaries | `gb01 == 2` |
| Kaldor 600090000 | 252311 | Asmodian Gate | 2 | 434 to 518 m | Mercenaries | `gb01 == 3`; `gb02 == 3` |
| Kaldor 600090000 | 297535 | (no name) | 1 | 23 m | Sieges | `Li_heal01 == 2` |
| Kaldor 600090000 | 297536 | (no name) | 1 | 22 m | Sieges | `Li_heal01 == 3` |
| Kaldor 600090000 | 297537 | (no name) | 1 | 22 m | Sieges | `Da_heal01 == 1` |
| Kaldor 600090000 | 297538 | (no name) | 1 | 22 m | Sieges | `Da_heal01 == 2` |
| Kaldor 600090000 | 297539 | (no name) | 1 | 23 m | Sieges | `Da_heal01 == 3` |
| Levinshor 600100000 | 235284 | Guardian Windblade | 1 | 797 m | Bases | `(da_killer_v08 == 9) || (da_killer_v08 == 11)` |
| Levinshor 600100000 | 235297 | Archon Quickblade | 1 | 797 m | Bases | `(li_killer_v08 == 9) || (li_killer_v08 == 11)` |

Instance maps (the gate is evaluated when the instance is created; not measured):

| Instance map | Npc ids | 5.8 rows |
|---|---|---|
| Beshmundir Temple 300170000 | 4 | 4 |
| Taloc's Hollow 300190000 | 4 | 4 |
| Abyssal Splinter 300220000 | 1 | 2 |
| Esoterrace 300250000 | 1 | 1 |
| Discipline Training Grounds 300430000 | 1 | 3 |
| Sealed Danuar Mysticarium 300480000 | 1 | 2 |
| Tiamat Stronghold 300510000 | 1 | 1 |
| Dragon Lord's Refuge 300520000 | 2 | 2 |
| The Eternal Bastion 300540000 | 3 | 12 |
| Ophidan Bridge 300590000 | 3 | 3 |
| Occupied Rentus Base 300620000 | 1 | 1 |
| Anguished Dragon Lord's Refuge 300630000 | 1 | 1 |
| Danuar Reliquary 301110000 | 3 | 3 |
| Sauro Supply Base 301130000 | 4 | 7 |
| Iron Wall Warfront 301220000 | 1 | 19 |
| Illuminary Obelisk 301230000 | 1 | 1 |
| Linkgate Foundry 301270000 | 2 | 8 |
| Lucky Danuar Reliquary 301330000 | 3 | 3 |
| Infernal Danuar Reliquary 301360000 | 3 | 3 |
| Drakenspire Depths 301390000 | 3 | 4 |

Not every pair is a duplicate. An npc with one static spawn may truly have a second, conditional
one in retail. Each needs a look.

### Finding 6. Housing merchants are not marked

The 46 guestbloom merchants in Oriel (831198, 25) and Pernon (831223, 21) come from Java's town
spawns. The extractor reads only `spawns/**/*.xml`, so it does not see them, and the 5.8 rows
on the very same points stay loadable. Their gates read `HLFP_ID_TOWN_*_LV == 1` and
`HDFP_ID_TOWN_*_LV == 1`. Nothing writes those counters today. If anything ever does, every
merchant doubles. Oriel and Pernon have 775 loadable rows each; how many of them repeat a town spawn
was not counted.

### Finding 7. Instances

`InstanceService` gives each new instance its conditional groups. 5,734 loadable rows are on
instance maps. No instance exists in a fresh world, so the census saw none of this. Checking one
means creating the instance in the SIM and listing its npcs the same way.

### Finding 8. Java's own data lists one npc more than once on one spot

This is the kind of spawn the 5.8 clean-up was meant to remove.

- In the spawn files: 1,532 spots where one npc id is listed more than once at the same x and y (to
  0.1 m) on one map, 2,160 surplus entries. Java's files have the same 1,532 but for two known
  differences (the twelfth Triniel arena spirit, D36; one Altgard spot removed on 2026-10-01).
- By spawn folder: Sieges 787, Rifts 239, Npcs 237, Bases 91, Instances 73, Npcs with Sieges 61,
  Gather 24, Ahserion's Flight 12, Custom with Npcs 8. Siege, rift and base entries are conditional
  and mostly do not stand together.
- **Live in a fresh world: 51 spots, 111 groups over all channels, 127 surplus objects.** These do
  stand together.

For each live spot, the last column is what NCSoft's 5.8 world file has for that npc id in an
unconditional territory at that place: distance from the spot to the territory's centre, and the
territory's count. "none" means no 5.8 territory with that npc was found within its own radius plus
6 m.

| Map | Npc | Name | x | y | z of each object | 5.8 territories with this npc here |
|---|---|---|---|---|---|---|
| Verteron 210030000 | 210073 | Shipwrecked Deckhand | 2116.4 | 630.4 | 114.45, 114.45 | 0.0 m, count 1; 4.0 m, count 1 |
| Verteron 210030000 | 210074 | Shipwrecked Crewmember | 2172.0 | 684.1 | 98.15, 100.51 | 0.1 m, count 1 |
| Verteron 210030000 | 210076 | Spy | 1499.5 | 1523.9 | 100.88, 101.29 | 0.1 m, count 1 |
| Verteron 210030000 | 210076 | Spy | 1625.1 | 1295.7 | 99.29, 101.41 | 0.1 m, count 1; 3.1 m, count 1 |
| Verteron 210030000 | 210088 | Dukaki Tracker | 952.4 | 2385.4 | 183.64, 183.64 | 0.0 m, count 1 |
| Verteron 210030000 | 210090 | Dukaki Watcher | 795.9 | 2459.6 | 213.66, 214.25 | 0.1 m, count 1 |
| Verteron 210030000 | 210105 | Cracked Kalgolem | 1629.8 | 2845.0 | 231.21, 239.37 | 0.0 m, count 1 |
| Verteron 210030000 | 210105 | Cracked Kalgolem | 1715.4 | 2827.0 | 239.77, 247.23 | 0.0 m, count 1 |
| Verteron 210030000 | 210126 | Rakeclaw Frillneck | 1479.6 | 2105.4 | 141.58, 143.76 | 0.0 m, count 1 |
| Verteron 210030000 | 210126 | Rakeclaw Frillneck | 1662.6 | 2069.1 | 170.74, 171.58 | 0.0 m, count 1 |
| Verteron 210030000 | 210145 | Dukaki Miner | 930.0 | 2277.3 | 176.58, 177.66 | 0.0 m, count 1 |
| Verteron 210030000 | 210150 | Dukaki Peon | 972.4 | 2256.1 | 159.96, 159.96 | 0.0 m, count 1 |
| Verteron 210030000 | 210165 | Veteran Tursin Watcher | 1801.0 | 878.4 | 124.43, 126.12 | 0.1 m, count 1 |
| Verteron 210030000 | 210185 | Crack Tursin Tracker | 2350.2 | 1078.1 | 169.62, 170.84 | 0.0 m, count 1 |
| Verteron 210030000 | 210320 | Sharphorn Starcrab | 1511.8 | 1536.0 | 99.42, 100.38 | 2.2 m, count 1 |
| Verteron 210030000 | 210337 | Porgus | 1308.7 | 2092.3 | 138.04, 140.59 | 0.0 m, count 1; 7.6 m, count 2 |
| Verteron 210030000 | 210688 | Thief | 1240.7 | 1819.5 | 113.68, 113.68 | 0.0 m, count 1 |
| Verteron 210030000 | 210698 | Peon | 1175.9 | 1780.4 | 102.78, 102.79 | 0.0 m, count 1 |
| Verteron 210030000 | 210700 | Shaman | 1201.0 | 1761.5 | 104.22, 104.25 | 0.0 m, count 1 |
| Verteron 210030000 | 700116 | Crystal | 1466.7 | 1333.2 | 98.75, 98.75 | 0.0 m, count 1 |
| Verteron 210030000 | 700116 | Crystal | 1885.8 | 1428.7 | 96.86, 100.79 | 0.0 m, count 1 |
| Verteron 210030000 | 700117 | Stolen Items Of Tolbas Village | 919.0 | 2149.2 | 157.86, 157.86 | 0.0 m, count 1 |
| Heiron 210040000 | 219043 | Kishar Guard | 2379.9 | 1649.1 | 124.00, 124.00 | 0.0 m, count 1 |
| Idian Depths 210090000 | 231060 | Infused Lapilima | 1129.0 | 1197.7 | 518.65, 518.65, 518.65 | none |
| Idian Depths 210090000 | 231060 | Infused Lapilima | 1176.8 | 1031.7 | 517.57, 517.57 | none |
| Idian Depths 220100000 | 231060 | Infused Lapilima | 1129.0 | 1197.7 | 518.65, 518.65, 518.65 | none |
| Idian Depths 220100000 | 231060 | Infused Lapilima | 1176.8 | 1031.7 | 517.57, 517.57 | none |
| Belus 400020000 | 881021 | Ruddyclaw Sparkle | 273.2 | 1430.8 | 1509.30, 1509.30 | 2.5 m, count 1; 2.5 m, count 1; 5.7 m, count 1 |
| Belus 400020000 | 881021 | Ruddyclaw Sparkle | 292.6 | 1462.6 | 1509.89, 1509.89 | 2.6 m, count 1; 2.6 m, count 1 |
| Belus 400020000 | 881022 | Ruddyclaw Sparkle | 1229.2 | 1705.5 | 1494.09, 1494.09 | 2.3 m, count 1; 2.3 m, count 1; 5.8 m, count 1 |
| Belus 400020000 | 881022 | Ruddyclaw Sparkle | 1285.3 | 1724.9 | 1495.89, 1495.89 | 2.2 m, count 1; 2.2 m, count 1; 5.6 m, count 1 |
| Belus 400020000 | 881022 | Ruddyclaw Sparkle | 1686.7 | 1332.8 | 1495.56, 1495.56 | 2.4 m, count 1; 4.4 m, count 1; 6.6 m, count 1 |
| Belus 400020000 | 881022 | Ruddyclaw Sparkle | 1737.7 | 1448.1 | 1513.14, 1513.14 | 2.4 m, count 1; 4.1 m, count 1; 6.1 m, count 1 |
| Belus 400020000 | 881022 | Ruddyclaw Sparkle | 1745.7 | 1473.8 | 1514.23, 1514.23 | 0.1 m, count 1; 4.3 m, count 1; 6.4 m, count 1 |
| Belus 400020000 | 881022 | Ruddyclaw Sparkle | 1754.5 | 1929.3 | 1490.94, 1490.94, 1490.94 | 1.6 m, count 1; 1.9 m, count 1; 2.2 m, count 1 |
| Belus 400020000 | 881023 | Ruddyclaw Sparkle | 1278.4 | 317.0 | 1494.29, 1494.29 | 2.0 m, count 1; 3.1 m, count 1; 4.6 m, count 1 |
| Belus 400020000 | 881023 | Ruddyclaw Sparkle | 1334.6 | 347.4 | 1495.48, 1495.48, 1495.48 | 2.6 m, count 1; 2.6 m, count 1; 2.6 m, count 1 |
| Belus 400020000 | 881023 | Ruddyclaw Sparkle | 1718.1 | 839.9 | 1494.96, 1494.96 | 2.4 m, count 1; 2.4 m, count 1 |
| Belus 400020000 | 881023 | Ruddyclaw Sparkle | 1719.8 | 809.7 | 1494.86, 1494.86 | 2.3 m, count 1; 2.3 m, count 1 |
| Belus 400020000 | 881023 | Ruddyclaw Sparkle | 1755.4 | 618.2 | 1509.16, 1509.16 | 2.3 m, count 1; 2.3 m, count 1 |
| Belus 400020000 | 881023 | Ruddyclaw Sparkle | 1760.7 | 581.3 | 1509.83, 1509.83, 1509.83 | 2.4 m, count 1; 2.4 m, count 1; 2.4 m, count 1 |
| Belus 400020000 | 881027 | Aureolus Sparkie | 1678.9 | 150.4 | 1489.67, 1489.67, 1489.67 | 2.1 m, count 1; 2.2 m, count 1; 2.2 m, count 1 |
| Belus 400020000 | 881027 | Aureolus Sparkie | 1920.4 | 303.7 | 1489.87, 1489.87, 1489.87 | 2.8 m, count 1; 2.8 m, count 1; 2.8 m, count 1 |
| Belus 400020000 | 881037 | Landleap Snuffler | 262.7 | 1481.2 | 1507.21, 1507.21 | 2.5 m, count 1; 2.5 m, count 1; 5.6 m, count 1 |
| Belus 400020000 | 881045 | Lithe Wriggot | 98.9 | 1317.5 | 1487.79, 1487.79 | 0.0 m, count 1; 2.6 m, count 1; 5.5 m, count 1 |
| Belus 400020000 | 881045 | Lithe Wriggot | 144.2 | 1324.1 | 1484.30, 1484.30 | 2.3 m, count 1; 3.4 m, count 1; 5.1 m, count 1 |
| Belus 400020000 | 881045 | Lithe Wriggot | 160.3 | 1275.4 | 1484.55, 1484.55, 1484.55 | 2.3 m, count 1; 2.3 m, count 1; 2.4 m, count 1 |
| Belus 400020000 | 881054 | Lushtimber Fungie | 1266.5 | 1701.0 | 1493.27, 1493.27 | 0.5 m, count 1; 1.9 m, count 1; 2.6 m, count 1; 2.7 m, count 1 |
| Belus 400020000 | 881054 | Lushtimber Fungie | 1267.1 | 1706.2 | 1493.44, 1493.44 | 2.6 m, count 1; 5.3 m, count 1; 6.7 m, count 1; 7.0 m, count 1 |
| Belus 400020000 | 881054 | Lushtimber Fungie | 1706.3 | 1302.3 | 1493.27, 1493.27 | 2.7 m, count 1; 5.1 m, count 1; 6.4 m, count 1; 7.1 m, count 1 |
| Belus 400020000 | 881054 | Lushtimber Fungie | 1707.2 | 1307.5 | 1493.44, 1493.44 | 0.2 m, count 1; 2.3 m, count 1; 2.6 m, count 1; 2.6 m, count 1 |

Reading it, by map:

- **Verteron, 22 spots, and Heiron, 1.** For most, 5.8 has exactly one of that npc on the very
  point. Java's second entry is then a duplicate by retail's evidence. Where Java's two z values
  differ by metres (the Cracked Kalgolems, 8 m), one of the two is also off the ground. Three spots
  have a second 5.8 territory 3 to 8 m away, so Java's pair may be retail's two npcs put on one
  point.
- **Belus, 24 spots.** 5.8 has two or three territories of that npc within a few metres, each with
  count 1, and seldom one on the point itself. These look like retail's separate npcs collapsed
  onto one point, not surplus npcs: the fix would be to spread them, not to delete.
- **Idian Depths, 4 spots.** Npc 231060 on the same coordinates in both 210090000 and 220100000. No
  5.8 territory with that npc was found there.

26 of the 51 have a 5.8 territory with count 1 within 0.5 m. 4 have none.

Not examined: the 73 repeated spots in `Instances`, and the repeats that are not live at start.

### Finding 9. Tooling

1. **Closed.** `regen_check.py` declared the conditional-spawn extractor and never ran it. It runs
   now, so an edit to a static spawn file that changes the column is reported as drift.
2. **Open.** `regen_check.py` also declares `WORLD_EXTRACTORS` and `STRING_EXTRACTORS` and runs
   neither.
3. **Open.** `regen_check.py` is not in `CLAUDE.md`'s check list and needs the 5.8 dump on `D:`.
   Nothing that runs without the dump compares the column with the static files.
4. **Open, a design point.** The 5 m rule cannot tell "retail moved this npc" from "retail has a
   second one". `KEPT_AT_JAVA_SPOT` is a hand list of two.

## 4. What moves when a spawn file changes

A later session that edits spawns must also do these, or a check fails:

| When | Do |
|---|---|
| Any static spawn file changes | `python tools/client-extract/regen_check.py`; if it reports drift on `gated_spawns.tsv`, regenerate with `extract_gated_spawns.py` and update the three counts in `GatedSpawnDataTests.cs` |
| A Poeta or Ishalgen spawn or walker changes | `pwsh -NoProfile -File scripts/parity/regen-geo-golden.ps1` (Java geo golden; switches the Java checkout to its generator branch and back) |
| A spawn on a baked nav map changes | `dotnet run --project tools/Aion.NavBake -- check --maps baked`; the travel graphs list aggressive spawns |
| A class trainer moves | `parity-artifacts/e2e/natural-class-lines.json` (`NaturalClassLineContractTests`) |
| Anything that adds or removes an npc at world start | Object ids shift for everything made later, so every neutral-gate baseline differs (`docs/natural-class-profiles.md`, CP-Q5: re-record or revert) |
| Always | The full list in `CLAUDE.md`, and `scripts/e2e/run-fast.ps1` |

## 5. Decisions needed

1. **Should the 5.8 conditional spawns load at all on this 4.8 server?** All of them, none, or a
   reviewed list of maps or gates. `CLAUDE.md` says 5.8 content is a boundary to record, not a gap
   to close; the retail AI log treats these groups as behaviour. Turning them off also turns off
   whatever patterns expect to find them; that was not examined.
2. **Rian (finding 2):** remove, ground, or leave.
3. **The thirteen named npcs (finding 3):** Java's spot or retail's, each.
4. **World raids and portal waves in Cygnea and Enshar (finding 4):** up from start as now, off, or
   held until a schedule exists.
5. **Field guns, gates and added creatures in Eltnen, Beluslan, Brusthonin, Heiron, Levinshor,
   Inggison, Transidium Annex (finding 4):** keep or drop, per map.
6. **Findings 5 to 7:** review now, or only when a gate or instance is first exercised.
7. **Java's repeated spots (finding 8):** which maps to clean, in what order, and what counts as
   proof (5.8 territory count 1 at the spot, as in the table). Each removal is a deviation from
   Java and needs logging like the Ishalgen and Altgard passes.
8. **Tooling (finding 9):** compute the overlap at server start instead of in the file; run the
   two unrun extractor lists; add a dump-free check to `CLAUDE.md`.

## 6. How this was measured

**Census.** A temporary test in `tests/Aion.Simulation.Tests` (not committed) wrote every `Npc` of
the started world to a file. It needs the development MySQL container, as every SIM test does.

```csharp
public sealed partial class SimulationFastScenarioTests
{
	[SkippableFact]
	public void TempNpcCensusProbe()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		string? path = Environment.GetEnvironmentVariable("TEMP_NPC_CENSUS_OUT");
		Skip.If(string.IsNullOrEmpty(path), "TEMP_NPC_CENSUS_OUT is not set.");
		var lines = new List<string> { "object\tnpc\tmap\tinstance\tx\ty\tz\ttype" };
		fixture.World.ForEachObject(obj =>
		{
			if (obj is Npc npc)
				lines.Add(string.Join('\t', npc.GetObjectId(), npc.GetNpcId(), npc.GetWorldId(), npc.GetInstanceId(),
					npc.GetX().ToString("F3", CultureInfo.InvariantCulture), npc.GetY().ToString("F3", CultureInfo.InvariantCulture),
					npc.GetZ().ToString("F3", CultureInfo.InvariantCulture), npc.GetType().Name));
		});
		File.WriteAllLines(path!, lines);
		Console.WriteLine($"CENSUS: {lines.Count - 1} npcs, gated placed {Aion.GameServer.World.Spawns.GatedSpawnService.Placed}");
	}
}
```

```bash
AION_SIM_DB_INTEGRATION=1 TEMP_NPC_CENSUS_OUT=<file> dotnet test tests/Aion.Simulation.Tests --artifacts-path run/census --filter "FullyQualifiedName~TempNpcCensusProbe"
```

Run it alone. After other tests have moved the virtual clock, about 2,000 npcs have walked and some
200 extra same-spot groups appear; those are walkers on a shared route point, not spawns.
`--artifacts-path` keeps the build out of `bin/`, so it does not collide with another session's run.

**Reading the census.**

- A conditional-spawn object is one whose npc id, x, y and z equal a loadable row of
  `gated_spawns.tsv` to 0.01. `GatedSpawnService.Placed` gives the total to check against (262).
- A repeated spot is two or more objects with one npc id, map, channel, and x and y equal to 0.1.
- Static spots come from `spawns/**/*.xml` with XML comments removed first (Rian's entry is inside
  one).
- 5.8 unconditional spawns: each `<territory>` in a world file has `<points>` and an `<npcs>` list
  with a `<count>` each. Npc names become ids through `client_npc_names.npc_names`, and world folders
  become map ids through `extract_client_waypoints.map_ids_by_world`, as the extractor does.

**Numbers of the census.** 110,253 npc objects on 41 maps; 262 from the conditional table; SIM runs
five channels on the starter maps.

**The trainers.** `AION_SIM_DB_INTEGRATION=1 CP_PROBE_ROWS=engineer,artist dotnet test
tests/Aion.Simulation.Tests --filter "FullyQualifiedName~NaturalNewSkillTrainerProbe"` prints every
object it saw for each trainer.
