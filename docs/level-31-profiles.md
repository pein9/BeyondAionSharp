# Level-31 profiles: the best coin gear and each class's rotation

Status: a plan, 2026-10-16. The operator, the same day, after Fire Temple: "Next up: Level 31 profile templates (just
like the level 26 ones) where we have the best coin gear for each class, and make sure we have our correct skill
rotations for each class. Plan this out."

## What is there now

- **The level-26 profiles** (`game-server/data/companions/asmodians-<class>-26.json`, eleven classes) come from the
  group world `ntc-group-s5-w1`: each class at 26 in the best coin gear it can wear, both power shards, its stigma
  stone (MR-g) and its kit. `scripts/sim/make-companion-profiles.py` writes them from the world's dump; they are never
  edited by hand.
- **The level-31 profiles** of the original eight (step M8 of [natural-morheim-leveling.md](natural-morheim-leveling.md))
  come from the check-in world `morheim-g31-s1-w1`, the same way. They are **not** in the best coin gear. The fortress's
  two Silver Coin sellers, Alfhild (804520) and Gyrda (804521), sell all 46 Elite Rank 6 pieces, but the check-in
  (rule MR-i) bought only some of them, and level-26 pieces are still worn:

  | Class | Not Elite Rank 6 at 31 |
  |---|---|
  | Gladiator, Templar | chest (level-26 quest piece), gauntlets and sabatons (Elite Rank 7, level 26), shoulders and legs (level-31 quest pieces) |
  | Assassin, Ranger | chest (level 26), boots (Elite Rank 7), shoulders and legs (level-31 quest pieces) |
  | Sorcerer, Spirit Master | shoulders and legs (level-31 quest pieces) |
  | Cleric, Chanter | brogans (Elite Rank 7), shoulders and legs (level-31 quest pieces) |

- **One stigma stone each.** Java opens a second regular slot at level 30 (`StigmaService.getPossibleStigmaCount`: one
  below 30, two from 30 to 39), and Q2900's bundle gave one stone. Stones are sold in Pandaemonium (204289, 204290),
  which the bots reach by the fortress teleporter.
- **Rotations.** M8 gave every class the highest rank of the skills it already casts, and the Cleric its two new heals
  (Healing Wind, Light of Recovery). Those two apart, the skills a class learns new between 27 and 31 are in no
  class's table:

  | Class | New at 27 to 31 (skill tree, auto-learned) |
  |---|---|
  | Gladiator | Seismic Billow, Springing Slice (28), Crushing Blow (30), Daevic Fury, Reckless Strike (31) |
  | Templar | Unwavering Devotion (28), Seal of Protection I, Face Smash, Hand of Healing (31) |
  | Assassin | Searching Eye, Rune Burst (28), Assassination (30), Wind Walk (31) |
  | Ranger | Spiral Arrow, Tactical Retreat (28), Bestial Fury (30), Misery Shot, Hunter's Eye (31) |
  | Sorcerer | Aether's Hold, Empyrean Wisdom, Boon of Iron-Clad, Boon of Clairvoyance (28), Refracting Shard (30), Robe of Cold, Tranquilizing Cloud (31) |
  | Spirit Master | Fear, Nightmare (28-29), Summoning Alacrity, Disenchant (31) |
  | Cleric | Boost Spell Resist I, Punishing Earth (30), Acquittal (31) |
  | Chanter | Invincibility Mantra, Pentacle Shock (28), Soul Crush (30), Booming Assault, Word of Wind (31) |

## The goal

Eight level-31 profiles made as the level-26 ones were: each of the original eight at exactly 31, in the best Elite
Rank 6 coin gear its class can wear, with two stigma stones, both power shards and its kit; and each class's fight
table casting what a level-31 character of its class has learned, each skill in its role. The Rider, the Gunner and
the Bard stay at 26 (MR-h).

## Rules

1. **Profiles come from a world,** written by `make-companion-profiles.py` from a snapshot where the eight stand at 31.
   No hand edit.
2. **The Java is the spec** for what a character can wear, socket and learn.
3. **Gear** follows MR-b and MR-i: the armor, the weapon and the shield or second weapon of the level's coin tier, no
   coin limit. A piece is bought when it beats what is worn, by a comparison L1 writes down and the check-in then
   uses. A lower coin tier never beats a higher one.
4. **The second stone** is chosen by MR-g's rule, as the first was: a skill the class's table can cast, and among those
   the one that adds most alone and in a group.
5. **A rotation** puts each new skill in the role it plays (damage, opener, stun, chain, buff, heal, escape), or names
   it as left out with the reason (a passive, a skill no bot rule uses yet). The companion and the bot of a class use
   the same table.
6. **Proof is the round:** the build and the checks of `CLAUDE.md`, an uncaptured trial before every captured round,
   the neutral gate and the Group NTC replay when shared bot code changes. No new unit tests for bot code.
7. **The bots' own world is not changed.** `morheim-ft-s1-w1` (the six at 35, the other two at 34) goes on as it is,
   and its next check-in is at 36, which uses the fixed rule.

## Steps

One step per iteration. A step is ticked when its round shows it, with a dated line under it.

- [x] **L1 The audit.** For each class: the Elite Rank 6 piece for each slot against what it wears at 31, and why the
  check-in skipped it; the second stone, its price and seller, and the choice by MR-g's rule; the skills new at 27 to
  31 and the second stone's skill, each with its role or the reason it stays out. Written into this document as
  tables, for the operator to read before L3.
  - 2026-10-16, the gear, from the check-in's own manifests (round `m7-g31`, trace `coin-armor-manifest`) and the
    item data. The check-in compares a piece with the worn one by one number: physical defence for armor, the weapon's
    number for a weapon, magic boost for a staff. An Elite Rank 7 piece of 26 carries a +16 defence bonus that brings it
    level with the Elite Rank 6 piece of 31, and a tie keeps what is worn; the level-26 quest chests have a little more
    defence and none of the Elite Rank 6 chest's HP, parry and magic resist. So twelve pieces of level 26 stayed on:

    | Class | Kept at 31, though Elite Rank 6 is the better piece (cost in Silver Coins) |
    |---|---|
    | Gladiator, Templar | breastplate (18), gauntlets (9), sabatons (9): the Elite Rank 6 ones add 6 to 107 HP, 6 to 13 magic resist, evasion, parry or block |
    | Assassin, Ranger | jerkin (18), boots (9): 107 and 53 HP, 9 and 5 magic resist, 41 parry |
    | Cleric, Chanter | brogans (9): 13 magic boost, 53 MP, 5 magic resist, 2 speed |
    | Sorcerer, Spirit Master | nothing: every Elite Rank 6 piece but two was bought |

    The shoulders and legs every class wears are the level-31 quest pieces (Morheim Shade Legionary), with the same
    numbers as the Elite Rank 6 ones (the Cleric's spaulders trade 12 hate and 11 magic attack for 6 concentration),
    so they stay. All of these pieces are of the same grade (Legend). The rule L2 gives the check-in: **a piece of a
    higher level and no lower grade beats the worn one; at the same level the check-in's number decides, as now.**
    With it the eight buy those twelve pieces and nothing else changes.
  - The second stone. Giles (204289) and Sinamara (204290) in Pandaemonium sell nine level-20 stones to each class, for
    Kinah only: 695,500 each in the SIM world, and 35,312 more to socket it. A stone uses no Stigma Shards. The second
    regular slot (`STIGMA2`, `1L << 31`) needs level 30 and Q2900 done, nothing more; a stone sent to any other slot
    goes in, is paid for and comes out at the next login. Each bot ends the check-in with 1.0 to 1.2 million Kinah, so
    stone and fee are within reach. My choices by MR-g's rule, his to change:

    | Class | First stone | Second stone | Why |
    |---|---|---|---|
    | Gladiator | Crippling Cut | Draining Blow (140001305) | hits a thrown-down target and gives the damage back as HP, in the window Crippling Cut uses |
    | Templar | Inquisitor's Blow | Punishing Thrust (140001325) | its hardest hit, weapon +986 a minute; hate for the tank |
    | Assassin | Searching Strike | Rune Knife (140001344) | 20 m, carves the rune its rune attacks burst; a pull that hits |
    | Ranger | Bow of Blessing | Dilation Arrow (140001357) | halves the target's attack speed |
    | Sorcerer | Ice Harpoon | Storm Strike (140001375) | 2,716 damage behind a 4 s cast, as the opener only |
    | Spirit Master | Cyclone of Wrath | Magic's Freedom (140001396) | +500 magic boost for every caster on the target |
    | Cleric | Saving Grace | Noble Grace (140001439) | 15 s of +35% healing received and +1,200 HP for the group, a party word as Word of Life is; a line of its own in the group heal driver |
    | Chanter | Word of Life | Word of Inspiration (140001412) | 15 s of +12% physical attack, +80 accuracy and +80 magic boost for the group, kept up in a fight; Splash Swing, the best plain hit, is a parry counter (`counter_skill="PARRY"`), which no table casts |

  - The rotations. Every class's validator stops at level 26 (`TopLevel`), so the skills new at 27 to 31 are left out
    without a word; L3 raises it to 31, and each new skill then has a role or a written reason:

    | Class | In, with its role | Out, and why |
    |---|---|---|
    | Gladiator | Daevic Fury (self, first), Crushing Blow (a sure stumble after Explosion of Rage), Springing Slice (on a thrown-down target, adjacent only), Reckless Strike (after Rupture), Draining Blow (stone) | Seismic Billow: an area hit, and the chain steps before it are excluded area skills |
    | Templar | Unwavering Devotion (in-fight upkeep under attack), Hand of Healing (emergency at 35%, with 2,000 DP kept for it), Punishing Thrust (stone) | Face Smash: both its openers are counter skills; Seal of Protection: a PvP passive |
    | Assassin | Rune Burst (three runes, between Pain Rune and Binding Rune), Assassination (right after Searching Strike, into its stun), Rune Knife (stone, the pull) | Searching Eye: sees hidden players; Wind Walk: stealth |
    | Ranger | Spiral Arrow (after Arrow Strike), Misery Shot (on a stunned, staggered or thrown-down target), Dilation Arrow (stone) | Tactical Retreat: a kiting aid; Hunter's Eye: detection; Bestial Fury: cuts the bow's reach under the 18 m stand-off |
    | Sorcerer | Empyrean Wisdom (self, first), Refracting Shard (free, mana back), Boon of Iron-Clad (emergency), Storm Strike (stone, opener) | Aether's Hold: dear for its damage; Boon of Clairvoyance: detection; Tranquilizing Cloud: area; Robe of Cold: shares Robe of Flame's cooldown id, so each would end the other |
    | Spirit Master | Summoning Alacrity (before the servant), Magic's Freedom (stone) | Fear and Nightmare: a feared monster runs among others; Disenchant: area, and monsters carry no buffs |
    | Cleric | Punishing Earth (solo), Light of Recovery (also in the solo ladder), Acquittal (group: three members at 50% or less), Noble Grace (group: before the heals when two are at 50% or less) | Boost Spell Resist: a passive |
    | Chanter | Invincibility Mantra (kept, in place of Revival Mantra: three mantras at most), Pentacle Shock (after Incandescent Blow), Soul Crush (on a stunned target), Word of Wind (group only), Word of Inspiration (stone, in-fight upkeep) | Booming Assault: its opener Booming Smash is left out of the table |

    New rules the tables need: a DP reserve (the Templar's Hand of Healing against Empyrean Chastisement), a recovery
    step that keeps no mana back (Boon of Iron-Clad), group-only rules (Word of Wind, Acquittal, Noble Grace) and the
    mantra swap. A ready follow-up before a state-bound skill that resets its chain, Assassination only on a stunned or
    poisoned target and a cast just before a summon are done by list order for now.
- [x] **L2 The gear and the stones.** The check-in buys every Elite Rank 6 piece that beats the worn one, and at 30 or
  above it buys the second stone in Pandaemonium and sockets it.
  - 2026-10-16 (`aecc52820`): the muster and its check-ins compare by level and grade first, and the number only at the
    same level; from level 30 the check-in takes the fortress's teleporter to Pandaemonium, buys the class's second stone
    with the character's own Kinah, comes home by Return and has it put in at the fortress's Stigma Master
    (`NaturalStigma.Seconds`). Trial `l2-t1` from `morheim-mg-s1-w1` (the world the first check-in started from, where
    leg `mg` had brought each of the eight to the fortress at 31, so L4 starts there too): every class bought the Elite
    Rank 6 chest, gloves and boots it lacked, with its weapon (63 to 90 Silver Coins), and its second stone (695,500
    Kinah, 35,312 to put in), and learned the stone's skill. The Assassin ends with the least Kinah, 321,742.
- [ ] **L3 The rotations.** The new skills in the eight tables, tried in a trial round per class line on its own
  fights.
- [ ] **L4 The check-in world.** Stage `check-in` played again for all eight from `morheim-mg-s1-w1`, where every one is
  exactly 31 and stands at the fortress: a trial, then the captured round `morheim-g31-s2`.
- [ ] **L5 The profiles.** The eight level-31 profiles written again from `morheim-g31-s2-w1`, `--check` passing; a
  companion round with the Fire Temple six at 31 (the Templar as the player's stand-in and five companions) shows the
  gear, the stones and the rotations in play. The Docker stack's companion host takes the new files at its next
  nightly build.
- [ ] **L6 The report.** Per class what it wears, its two stones and its rotation's new skills; what was left out and
  why.

## Decisions

Mine, where the operator gave none; each is his to change:

1. **The source world** is the check-in played again from `morheim-mc-s1-w1`, where the eight are exactly 31, as M8
   used the first check-in. A profile made from the bots of today (34 and 35) would carry ranks and stigma ranks
   learned after 31.
2. **The bots' own world keeps its gear** until its check-in at 36; this plan does not re-gear it at 35.
3. **The rotations change for every level:** a bot of 35 in the world uses the new skills too, since its table is the
   same.

## Loop prompt

```
Work the L checklist in docs/level-31-profiles.md, one step per iteration, through L6. Repository
C:\Users\ryanf\Documents\GitHub\BeyondAionSharp, existing checkout on main. Read CLAUDE.md, docs/level-31-profiles.md,
docs/natural-muster.md (rules MR-b, MR-g, MR-i) and docs/companion-bots.md first. Each iteration: orient (git status,
the step list, scripts/sim/sim-run-marker.ps1, running background tasks); never build while a run holds the build
outputs; take the first open step; small change, build, uncaptured trial round, then the captured round on committed
code; the checks of CLAUDE.md before each commit (unit suite, warning baseline, neutral gate on the five starter scopes
and one replay of run/ntc/n9.json when shared bot code changed; make-companion-profiles.py --check when profiles
change); a dated line under the step, tick it when a round shows it, commit code then document by path with no
attribution line. Never push, never branch, no worktree; leave the three docs/playtest-*.md files alone; no hand-edit of
generated data; never raise the warning baseline; SIM env AION_SIM_DB_CONTAINER=aion-sim-mysql AION_SIM_DB_PORT=3307
AION_BOT_DASHBOARD_PORT=17881. STOP when L6 is ticked, or a step has failed three iterations running without progress,
or a decision is needed that the document does not give; on stopping send one push notification.
```
