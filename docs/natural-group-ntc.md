# Group NTC leg

Status: planned 2026-10-12; N1 to N6 are done: the six take the quests, go into one instance of the camp, fight
through it, kill the Protector, the Guard and the Aetheric Field Guard, and destroy the fortress gate. The step list
at the end is worked one step at a time.

It follows [natural-group-healer.md](natural-group-healer.md): group formation, F1 following, the Cleric's
healing-only role, resurrection waiting and round-robin loot are there and are reused. Gear, supplies, shards and the
bag cleanup stay under [natural-muster.md](natural-muster.md), rules MR-a to MR-f.

## The goal

The operator, 2026-10-12, on what comes after the coin-quest groups:

1. Whole group logs in.
2. Groups up with the Templar as party leader.
3. Accept all NTC quests.
4. Make sure pathing is ready for NTC. Plan an order of where to go.
5. Make sure we can handle bosses.
6. Group stays together at all times.
7. Do NTC quests, all the way to the end boss.
8. Disband group when the boss is dead.
9. Handle any quests for the group and individual. "There's one where individuals need to pick up chests."

"Be able to roll back the world on each iteration."

## Roster and rollback

Nochsana Training Camp (map 300030000) admits six from level 25. The roster is the six of the last coin run:
Asimtemplar (leader, F1), Asimnjour (Cleric, `"role": "healer"`), Asimgladiator, Asimranger, Asimsorcerer and
Asimchanter. The other five characters of the world stay logged out.

Every attempt restores `ntc-group-s4` into a throwaway schema and is not captured:

```powershell
pwsh -NoProfile -File scripts/sim/run-round.ps1 -Round run/ntc/<round>.json -Item ntc -Run <fresh id> -From ntc-group-s4 -Seed 1 -ReplayRoot C:/Users/ryanf/Documents/GitHub/BeyondAionSharp/run/ntc
```

The stage is `"group-ntc"`, with the same `groupLeader`, `groupRoster`, `groupLoot` and `"role": "healer"` as
`group-coin`, and `groupUntil` naming how far the leg is played (so far `"quests"`, `"entrance"`, `"protector"`,
`"field-guard"` or `"gate"`). `run/ntc/n2.json` to `n6.json` are the round files of those steps. Each member writes
`group-ntc-completion.json`.

Code changes stay; the world does not. `run/ntc/baseline.json` holds the snapshot's hashes, checked 2026-10-12:
dump `8d0787cf…cf8f97`, manifest `26fb3931…68ce96`, the same as `run/group/baseline.json`. They are checked again
when the work stops. If the walk to the door costs too much, that state may be captured once under a new name; the
last run plays the whole leg from `ntc-group-s4`.

## The plan

### Quests

All eight Asmodian quests of the zone, from `quest_data.xml` and `quest_script_data/reshanta.xml`. The 4.8 set is
given in Morheim Ice Fortress; the older set is given at Primum Landing in Reshanta. Each old quest and its 4.8 twin
shut each other out by their start conditions, so a character holds one of a pair, never both.

| Quest | Giver | Asks | Handler | In the leg |
|---|---|---|---|---|
| Q4731 Nochsana Calls | Aegir 204301 (225, 2415, 454) | report to Kelmar 800519 | data (`report_to`) | yes |
| Q4733 [Group] Training Terminated, 5 times | Kelmar 800519 (266, 2371, 445) | kill the Protector 256690, the Guard 256689, the Aetheric Field Guard 256692 and the Teleporter 256691 | data (`monster_hunt`) | yes |
| Q4734 [Group] Weapons Wanted | Kelmar | four of item 182205680 from the weapons chests 700415 | data (`item_collecting`) | yes |
| Q4732 [Group] General Malevolence | Kelmar, by the 4.8 codex page | the gate and the General, as its twin Q4702 | none in Java or C# | after the leg, under D32 |
| Q24241 You Have a Choice... | Lif 204379 | the same report and rewards as Q4731 | none | no: Q4731 shuts it out |
| Q4701 An Important Message | 278035 in Reshanta | report to Lisya 278016 | data | no: Reshanta, and Q4731 shuts it out |
| Q4702 [Group] General Death | Lisya 278016 in Reshanta | kill the gate 256694, then the General 256693 | Java and C# | no: Reshanta |
| Q4703, Q4704 | Lisya, Leban 278126 in Reshanta | the twins of Q4733 and Q4734 | data | no: Reshanta |

Q27000 is a legion mission and is not part of this. Until Q4732 has a handler the General is killed without a
quest: he is the end of the run all the same. Kill credit reaches every member in range, as Q2318 showed.

**The chests.** Q4734 wants four items from each member, 24 for six. The camp has 28 weapons chests: 21 inside
the fortress walls behind the gate, on three levels, and 7 on the camp's own ground along the outside of the walls
(N1 found that from the mesh). A chest is a `quest_use_item` object: the member that uses it gets its drop
registered to itself alone (the quest drop is not an each-member drop), and the chest dies
(`ai/quests/QuestItemNpcAI.java`). So every member opens four chests itself, and only four are to spare: a member
uses a chest only while it still needs an item.

### Entrance and exit

- **In.** Each member talks to Vallack 800510 (264, 2371, 445), beside Kelmar, with dialog 10000. Java
  (`PortalService.port`) asks for a group, level 25 and no entry cooldown. The first member in makes the instance
  and registers it to the group; the others are sent into that same instance while fewer than six are inside. They
  land at (513, 668, 331). Nobody talks to Vallack until all six stand there.
- **Out.** The abyss gate 700438 by the landing sends an Asmodian to Reshanta (877, 3079, 1645). It is never used.
  The instance's exit point is Morheim (264.7, 2366.4, 445.1), four metres from Kelmar. Java moves a member there
  30 seconds after it leaves or loses its group inside (`PlayerLeavedEvent`; a disband runs it for every member).
  The C# port has the same (`PlayerLeavedEvent.cs`).
- **So the disband is the way home, and it comes last inside.** After the General is dead and looted, and every
  member has its four chest items, F1 disbands. Thirty seconds later all six stand beside Kelmar, each alone, and
  each turns in its own quests. The operator's point 9 names the chests after the disband; they are done before it
  because an ungrouped member has those 30 seconds and no more.

### Pathing

The navmesh of 300030000 is baked (N1) and checked in: 4,167 polygons, no doors, no travel graph
([bot-navigation.md](bot-navigation.md)):

```bash
dotnet run --project tools/Aion.NavBake -- bake  --maps 300030000
dotnet run --project tools/Aion.NavBake -- check --maps 300030000 --rebake
```

The camp is one mesh island from the landing to the ground before the gate. The fortress interior is another: the
bake splits the ground in the gateway, although every one-metre step through it passes the server's own ground
check. The router crosses that seam on a route to an exact point (it steps off the island by itself); a route "to
within reach of an NPC" does not cross it. So F1 first walks to a point inside the gateway, (346.4, 350, 379.1),
and routes to monsters and chests from there.

Doors are baked open. The fortress gate 256694 is not a door but a monster standing in the gateway at
(346, 357, 378): level 27, HERO, 52,909 HP, no attack of its own. It is destroyed, which is also what Q4732 and
Q4702 count first. Its "pass by the gate" dialog is not used. In Morheim, Aegir, Kelmar and Vallack stand within
60 metres of each other inside the fortress, on the mesh that is there.

### Order inside

From the spawn file; the walkable way between the points comes from the baked mesh.

1. The landing (513, 668, 331), then west through the lower camp (y 500 to 690): drakes, recruits, recondos and
   runners, level 25 and 26, ordinary.
2. **The Protector** 256690 at (307, 590, 373), at the west end. The Nochsana artifact 700437 stands beside him.
3. North to **the Guard** 256689 at (357, 439, 362).
4. **The Aetheric Field Guard** 256692 at (382, 409, 377).
5. **The gate** 256694 at (346, 357, 378).
6. Inside the walls (y 270 to 355): 21 of the chests, twenty recondos, ten runners, five pickets and two docs.
7. **The Teleporter** 256691 at (353, 271, 392), 22 metres from the General.
8. **The General** 256693 at (331, 269, 385). Then the rest of the chest items, then the disband.

### Bosses

All are fought by the same rules: the Templar pulls and holds, the Cleric heals, cleanses and resurrects, the
others assist F1's target as their class does.

**How F1 leads inside (N4).** It walks the checked way to the next named monster eight metres at a time and looks
sixteen ahead. A monster that struck a member is fought first, where it stands, at once. Otherwise the nearest
monster whose aggro circle the next stretch would cross is named and killed by all. Before a fresh pull or a walk
F1 rests and waits until every member is alive, within twelve metres and has half its mana; a monster on a member
ends that wait. Inside the camp nobody plans a way around other monsters' circles, as a member does in the open
field: the group fights the packs. The named monster is done when every member's journal has its kill. A boss gets a rule of its own only after a run shows the group needs
one; if a boss leaves the Templar and that kills the group, the Templar's learned taunt comes first.

| Monster | Level, rating, HP | What it does (shipped skills; AI) |
|---|---|---|
| Protector 256690 | 26 ELITE, 14,421 | Caster to 37 m: Flame Attack and Fireball (physical debuffs), Stun Wave, Strike Heat. Retail AI: calls help to 25 m, naming whoever pulled. |
| Guard 256689 | 26 ELITE, 16,895 | Bone drake, plain aggressive, nine metres across: melee stands well out from its centre. Wing Flapping, Fire Burst (a burn), Wide Area Flame Emission. |
| Aetheric Field Guard 256692 | 26 ELITE, 17,375 | Earth Axis Strike (stun), Leg Root, Blinding Energy, Stone Skin (shield). Root and blind are for Cleanse. |
| Gate 256694 | 27 HERO, 52,909 | Does not fight. Everyone hits it. |
| Teleporter 256691 | 26 ELITE, 14,421 | Winter Binding (snare), Hydro Eruption, Wide Area Stun. Retail AI: calls to 20 m; a reservist lands on its target at the pull and another 30 s later while it is above 70%. |
| General 256693 | 27 ELITE, 26,481 | Power Attack, Smash and Wide Smash (stat down), Stun Wave, Fear Casting, Wide Area Pull, Wide Area Wrathful Strike, Wide Area Rage (shield and buff on himself). |

The artifact 700437 casts Shield of Compassion (276) on up to six players within 25 metres: 3,000 damage taken
off, for a minute. It is left alone unless a run shows a boss needs it.

## Steps

One step per iteration. A step is ticked when a run from `ntc-group-s4` shows it; the dated line under it names
the run, how far the group got, the deaths and what stopped it.

- [x] **N1 Pathing.** Bake the navmesh of 300030000; the regeneration check passes.
  - 2026-10-12. Baked: 4,167 polygons in 99 tiles, 263 KiB, no doors. `check --maps 300030000 --rebake` and
    `check --maps baked` (twelve maps) say ok. Routes, by the tool's own router (`diag`, `points`; logs under
    `run/ntc/N1`): landing to the Protector 120 points; Protector to the Guard 135; Guard to the Aetheric Field
    Guard 50; from there to the gate 46; from the gateway to the General 47 and to the Teleporter 139. From the
    point inside the gateway all 21 chests inside the walls are reached, and the 7 outside by going back out.
  - No travel graph: the group goes from point to point, as on Bregirun and in the arena. The `graph` command
    also crashed on a thread race in the edge memory of NR-46a; that is flagged as a task of its own and not fixed
    here. It does not touch a journey: a bot's own routing is single-threaded.
- [x] **N2 Group and quests.** New stage `group-ntc` beside `group-coin`: the six log in, the Templar invites and
  leads, all six take Q4731 from Aegir, hand it in at Kelmar and take Q4733 and Q4734.
  - 2026-10-12, round `n2-3` (`run/ntc/ntc/n2-3`): all six reached the end in 2 min 43 s of game time, no death.
    Every journal has Q4731 complete and Q4733 and Q4734 started. Each member chose Q4731's piece of its own armor:
    plate (Templar, Gladiator), chain (Cleric, Chanter), leather (Ranger), cloth (Sorcerer).
  - The group stage now plays several quests: the leader names the quest and the NPC in its command, and a member's
    report carries its whole journal. The three quest plans are in `parity-artifacts/e2e/natural-group-ntc-plans`.
  - Rounds `n2-1` and `n2-2` stopped on the stairs between Aegir and Kelmar: all six had accepted Q4731, then the
    Gladiator and the Sorcerer stood still and the Templar's wait for them ran out after two minutes. Two faults of
    the follow rule, both fixed for every group stage: a follower kept asking for the same trail point it could
    not step to (it now tries the points after it, then a checked route to where it sees F1), and a follow step
    ended three metres along its path wherever that was, which can be the face of a stair (it now ends on a checked
    point of the path).
  - The coin stage runs on the same code: round `six-n2` (`run/group/coin/six-n2`), six members from `ntc-group-s4`,
    completed its Q2318 cycle for all six in 10 min 15 s with no death.
- [x] **N3 Entrance.** All six gather at Vallack and stand in one instance.
  - 2026-10-12, round `n3-1` (`run/ntc/ntc/n3-1`), first attempt: after N2's quests the Templar waited before
    Vallack until all six stood there, then each went through his dialog. All six stand on map 300030000 in
    instance 2, at the landing (513, 668, 331), 2 min 44 s after login, no death.
  - Each member tells the group its map and instance, and the Templar goes on only when all six name its own. A
    member that changes maps rebuilds its checked movement for the map it stands on; the follow rule drops its
    trail with the map.
  - The leader's "regroup at the bind" rule now holds on the bind's own map only. Inside the camp it does nothing:
    what a member does after a death in there is left for the step in which a run first shows one.
- [x] **N4 The lower camp and the Protector.** Together to the west end; the Protector dead, credit to all six.
  - 2026-10-12, round `n4-2` (`run/ntc/ntc/n4-2`): the Protector is dead 3 min 53 s after the landing (6 min 38 s
    after login), and all six journals count him for Q4733. Ten pulls, nine kills before him (three recruits, four
    recondos, a doc, a picket), no death. The Templar's lowest health was 2,793 of 3,322. Round-robin loot: 32
    entries over the six.
  - The Cleric sent no attack. Its support decisions: 6 cleanses, the first a run has shown, 3 heals, 1 top-up, 67
    Lights of Rejuvenation and 4 blessings; the lowest health it acted on was 65%.
  - Round `n4-1` reached the Protector's camp, twenty metres from him, with no death, and stopped there. It showed
    four things, all changed: a killed monster was named again four times a second until its corpse vanished, 1,200
    times over five minutes (a killed target is now remembered and a name is spoken once); the fight would not
    approach the Protector because other monsters' circles lay across the approach (inside the camp nobody plans
    around them); F1 stood waiting for the Cleric's mana while a picket was on the Cleric (a monster on a member ends
    the wait and is fought); and the dead-Cleric regroup at the bind was not yet tied to the bind's map.
- [x] **N5 The Guard and the Aetheric Field Guard.**
  - 2026-10-12, round `n5-2` (`run/ntc/ntc/n5-2`): the Guard is dead 2 min 3 s after the Protector and the
    Aetheric Field Guard a minute after that, 9 min 39 s after login. All six journals count the three for Q4733.
    Seventeen pulls in all, no death. Round-robin loot: 49 entries over the six.
  - The Cleric sent no attack: 9 cleanses, 4 heals, 9 top-ups, 98 Lights of Rejuvenation, 4 blessings, and its
    mana recovery twice.
  - Round `n5-1` is void. It ran a bot binary that lacked the step's code and stopped at login: another session
    built in this checkout at the same second, and its older compile landed over the newer source. From here a
    round is started only after the binary is seen to hold the step's new code.
- [x] **N6 The gate.**
  - 2026-10-12, round `n6-2` (`run/ntc/ntc/n6-2`): the gate fell 46 seconds after the first blow, 11 min 26 s after
    login. Twenty-four pulls since the landing, no death, and no member's client sees the gate standing at the end.
    The Cleric sent no attack (9 cleanses, 5 heals). Round-robin loot: 74 entries over the six. Two members reached
    level 27 on the way.
  - The gate comes at nobody, so the rule of "what would wake on the way" never names it: at the gateway F1 names
    the gate itself. No quest of the leg counts it until Q4732 has a handler (N10), so its fall is what F1 saw.
  - Round `n6-1` destroyed the gate the same way, and five members ended well. The Sorcerer did not: it was resting
    for mana when F1 spoke its last command and logged out, read the words when F1 was no longer the leader, and
    ignored them. A member now takes a command from the leader it last saw leading, also when that leader has left.
    The leg's own last command is the disband, where the same would have happened.
- [ ] **N7 Inside the walls.** The Teleporter dead; every member has its four chest items.
- [ ] **N8 The General.** Dead and looted.
- [ ] **N9 Disband and turn in.** F1 disbands; all six arrive at the exit point; each hands in Q4733 and Q4734.
- [ ] **N10 Q4732 under D32.** Handler, test, deviation log and upstream offer by
  [retail-quest-completion.md](retail-quest-completion.md); the six take it with the others and hand it in.
- [ ] **N11 The whole leg in one run** from `ntc-group-s4`, with the report the operator asked for: how far the
  group gets, quests per member, deaths, the healing, dispels and resurrections seen, loot, and what was not
  exercised.

## Working rules

Those of [natural-group-healer.md](natural-group-healer.md#verification-and-working-rules), unchanged: SIM only,
seed 1, one world; Java first; decisions from client observations; no new bot unit tests and no proof programs; the
healer never attacks and the others fight just the same; a death, a wipe or a refused cast is an outcome, and no
death is forced. Checks before a commit are the build, the warning baseline and what `CLAUDE.md` lists for the
files changed, the existing `Aion.Bots` regressions, and Fast when gameplay or server code changed.
