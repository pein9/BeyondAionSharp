# Group NTC leg

Status: planned, 2026-10-12. Nothing of it is played yet. The step list at the end is worked one step at a time.

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

**The chests.** Q4734 wants four items from each member, 24 for six. The camp has 28 weapons chests, all inside
the fortress walls behind the gate. A chest is a `quest_use_item` object: the member that uses it gets its drop
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

No navmesh for 300030000 is checked in, only its mask (294 sectors). It is baked as Haramel's was
([bot-navigation.md](bot-navigation.md)):

```bash
dotnet run --project tools/Aion.NavBake -- bake  --maps 300030000
dotnet run --project tools/Aion.NavBake -- graph --maps 300030000 --race ASMODIANS
dotnet run --project tools/Aion.NavBake -- check --maps 300030000 --rebake
```

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
6. Inside the walls (y 270 to 355): the 28 chests, twenty recondos, ten runners, five pickets and two docs.
7. **The Teleporter** 256691 at (353, 271, 392), 22 metres from the General.
8. **The General** 256693 at (331, 269, 385). Then the rest of the chest items, then the disband.

### Bosses

All are fought by the same rules: the Templar pulls and holds, the Cleric heals, cleanses and resurrects, the
others assist F1's target as their class does. A boss gets a rule of its own only after a run shows the group needs
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

- [ ] **N1 Pathing.** Bake the navmesh and graph of 300030000; the regeneration check passes.
- [ ] **N2 Group and quests.** New stage `group-ntc` beside `group-coin`: the six log in, the Templar invites and
  leads, all six take Q4731 from Aegir, hand it in at Kelmar and take Q4733 and Q4734.
- [ ] **N3 Entrance.** All six gather at Vallack and stand in one instance.
- [ ] **N4 The lower camp and the Protector.** Together to the west end; the Protector dead, credit to all six.
- [ ] **N5 The Guard and the Aetheric Field Guard.**
- [ ] **N6 The gate.**
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
