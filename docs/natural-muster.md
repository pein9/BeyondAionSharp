# One world, and the muster at the Abyss entry's end

Status: done, 2026-10-12. The eleven Asmodian second classes stand in one world database, the round snapshot
`ntc-group-s2-w1`: each on an account of its own, level 26, in the best coin gear it can wear, with its surplus gear
sold and its kit full. What is not done, and what the operator is asked, is at the end.

This document holds three decisions the operator made on 2026-10-12, after the all-classes plan
(`docs/natural-all-classes-ntc.md`, D40) closed, and the work that followed from them. It outranks that plan where
the two differ: the plan's defaults NR-Q11 (up to eight worlds for a round) and NR-Q16 (a class's level at the finish is
the level it reached) are withdrawn.

## What the operator said

On 2026-10-12, first: "For the templar, we need him at level 26 at the end of our legs, same for anyone else. [...]
Can we have any character, just do the skipped 'repeatable coin quest' that is given in Morheim fortress, until they
reach level 26?" He asked whether every class had the best level-26 coin gear and weapon from supplied coins, whether
the bags are managed at the end, and whether the consumables are stocked by supplying them. "Goal is: The group to be
100% ready for our next legs."

Then, the same day:

1. "We shouldn't have a coin limit."
2. "We need to give our characters the BEST they can use (as some are level limited) for consumables. This includes
   all the way down to level 10. (Do not rerun anything, just change the rules/scripts)."
3. "I don't know why we have 11 separate world databases. I have told you at least 4 times to use the same world
   database because the huge goal is for group play!"

## Rules

**(MR-a) One world.** Every bot character lives in one world database, each on an account of its own. A run that
makes a character the group will use starts from that world and is captured as that world. No class has a schema of
its own, a round is one world, and no capture is made by playing one character alone. The neutral gate's scopes stay
as they are: they replay one recorded character each to prove that play has not changed, and make no character.

**(MR-b) No coin limit.** The Bronze Coins supplied on the Abyss-entry leg are what a coin manifest is short,
whatever that comes to. A tier's manifest is the armor, the weapon, the shield for a class that holds one, and the
weapon a second time for a class that holds two. Each piece is still bought only when it beats the piece worn; a tie
is not better.

**(MR-c) The best consumable a character can use, from level 10.** The server gates a consumable by its restrict
row, not by its item level (Java `PlayerRestrictions.canUseItem`). The kit's row for a family is the best tier whose
restrict row the character meets, and the bot uses the best tier it owns by the same gate. The level 1-9 kit is as
the operator approved it on 2026-10-07 and is not changed.

**(MR-d) The muster.** A character that stands at the Abyss entry's end plays the muster in the one world:

1. Level 26, by the fortress's repeatable coin quest, Q2318 "[Coin] Foes of the Fortress" (Nott, 204360), as often as
   it takes. The quest pays no experience; the level comes from its nine kills a turn.
2. The best coin tier it can wear, by rule (MR-b).
3. The bags: the leg's containers, the coin quest's chest and the Supply Scroll Bundles are opened, and the gear and
   accessories its class rules call surplus are sold to the fortress's nearest merchant. What the leg protects is
   kept. Nothing that is not gear is sold. The sale comes first, because a character can arrive with a full cube and
   a container opens only into room; it is done again after the coin gear, for the pieces that gear replaced.
4. The kit, every row at its full count.
5. Three free cube slots or more, a relog, the same character again, and the receipt
   `altgard-muster-completion.json`.

Deaths, a soul heal and the walk back are outcomes, as in every leg.

## The kit from level 10 (MR-c)

| Family | Until 2026-10-12 | Now, levels 10 to 49 | For |
|---|---|---|---|
| Life potion (heals over 20 s) | Minor at 10, Lesser at 20, Life Potion at 30 | Major Life Potion (162000006), 30 | every class |
| Mana serum | Minor at 10, Lesser at 20, Mana Serum at 30 | Major Mana Serum (162000021), 40 | a class that casts from mana |
| Anti-Shock scroll | Lesser at 10, Anti-Shock at 20, Greater at 30 | Major Anti-Shock Scroll (164000070), 30 | every class |
| Running scroll | none at 10, Running at 20, Greater at 30 | Greater Running Scroll (164000076), 20 | every class |
| Awakening scroll | none at 10, Awakening at 20, Greater at 30 | Greater Awakening Scroll (164000134), 60 | a class that casts from mana |
| Courage scroll | never supplied | Greater Courage Scroll (164000073), 60 | a class that keeps Courage up |
| DP jelly | Zeller Aether Jelly | Cippo Aether Jelly (160002274), 8 | every class |
| Powder | Lesser Odella at 10, Odella at 25 | unchanged | a class that rests with the powder |

The number is the full count. The powder is not a choice: it is the reagent the learned level of Herb Treatment and MP
Recovery consumes. The next tiers of the potions and the shield scroll (Fine) have a restrict row of level 50, so the
bands end at 49; a leg that reaches level 50 adds them. `NaturalHelpItemAllowlistTests` holds the table to the item
data: a row fails when a better tier of its family is usable anywhere in its band.

The use side changed with it. The bot used a scroll only when its item level was at or below the character's level
(ten above, for the shield); it now uses the best tier it owns that the restrict row allows. The reason texts of
those checks are in the recorded traces and keep their wording.

## The one world (MR-a)

The eleven finished characters stood in eleven dumps, most with the same character id and several on the same
account. `scripts/sim/join-world.py` brings them into one:

```bash
python scripts/sim/join-world.py --plan <plan.json> --name <name>
```

The plan names each character by its snapshot and class line, and gives a new name where two share one (the Cleric
and the Chanter were both Asimnjour; the Chanter is Asimchanter in the joined world). The result is the round
snapshot `<name>-w1`, which `scripts/sim/run-round.ps1 -From <name>` plays like any other.

What the script changes, and nothing else: a character's id and the ids of its items move to a range of their own
(seat number times 1,000,000 added), its account is its seat's (111 and up), and its name is the plan's when the plan
gives one. The world's own tables and its clock are those of the source with the latest clock. A source is checked
against the hash its record holds, restored into a throwaway schema, read and dropped; no source is written to. The
joined snapshot's record lists every source with its hash and the rows carried.

This is the one place a snapshot is not made by natural play alone. Each character in it was made by natural play
and verified in its own capture; the muster then takes each one up from the client's view.

## Running the muster

```bash
pwsh -NoProfile -File scripts/sim/run-round.ps1 -Round <round.json> -Item MR-4 -Run <id> -From <joined name> -Capture <name> -ReplayRoot <absolute folder>
```

The round file has one world, a bot for each character with `"stage": "muster"`, ten game minutes apart. The
capture is one round snapshot with every character in it.

## Checklist

- [x] **MR-1** No coin limit: the allowlist, the leg's contract and its checks; the shield and the second weapon in a
  tier's manifest.
  - 2026-10-12, commit `b8c3dec80`. Proof under MR-4's.
- [x] **MR-2** The kit and the use rule of (MR-c); the tests that pin them.
  - 2026-10-12, commit `b8c3dec80`. `Aion.GameServer.Tests`: 4,632 passed, 16 skipped. No leg was replayed for it.
  - The gate on the scopes of levels 1 to 9 (run/mr/MR-1, guard-starters): the five starter scopes are identical. Scope
    `p` is identical to record 35,405 of 35,463; there the Priest becomes level 10 at the ceremony and its stock check
    supplies the new kit.
- [x] **MR-3** `scripts/sim/join-world.py`, and the joined world of the eleven characters.
  - 2026-10-12: `ntc-group-joined-s1-w1`, from the plan `run/mr/MR-3/join-plan.json` at commit `b8c3dec80`
    (run/mr/MR-3/join.log). Eleven characters, 784 item rows, every source checked by its hash. The Chanter is
    Asimchanter. `mr-trial1-w1` is the same join made before the commit, for the trial rounds.
- [x] **MR-4** The muster stage, played by every character in the joined world, and the group's capture.
  - 2026-10-12, round muster1 (run/mr/MR-4/muster1), from `ntc-group-joined-s1`, captured as `ntc-group-s1`: eleven bots
    in one world, ten game minutes apart. Nine reached the muster's end. The Spirit Master and the Bard stopped: they
    arrived with a full cube, a Supply Scroll Bundle did not open, and the next check asked for it inside its use delay.
  - **MR-4a**, commit `8a9d9f13c`: the surplus gear is sold before a container is opened, and again after the coin gear;
    a refused open waits its delay out; the muster's end asks for three free cube slots. A round's capture carries over
    the characters that had no bot in it. Round muster2 (run/mr/MR-4/muster2), from `ntc-group-s1`, captured as
    `ntc-group-s2`: the Spirit Master and the Bard reached the end; the other nine stayed in the world as they were.
  - The bundles of the two code commits (run/mr/MR-1 and run/mr/MR-4a, chain): the unit suite, the seven pre-commit
    checks, the three script tests and Fast pass; the starter scopes are identical.

## The group, as `ntc-group-s2-w1` holds it

| Class | Character, account | Level | The coin quest | Coins supplied | Bought | Weapon held now | Gear sold | Containers opened | Free cube slots | Round |
|---|---|---|---|---:|---|---|---:|---:|---:|---|
| Cleric | Asimnjour 1133266, 111 | 26 | not needed | 0 | nothing | Elite Rank 7 Asmodian Staff (26) | 16 | 4 | 20 | muster1 |
| Templar | Asimtemplar 2133266, 112 | 25 to 26 | 2 turns, 126,414 XP, 26 game minutes | 41 | Gauntlets for the gloves, Shoulderplates for the shoulders, Greaves for the legs, Sabatons for the shoes, Sword for the main hand, Shield for the off hand | Elite Rank 7 Asmodian Sword (26) and Elite Rank 7 Asmodian Shield (26) | 12 | 6 | 20 | muster1 |
| Sorcerer | Asimsorcerer 3133266, 113 | 26 | not needed | 0 | nothing | Elite Rank 7 Asmodian Spellbook (26) | 18 | 4 | 20 | muster1 |
| Chanter | Asimchanter 4133266, 114 | 26 | not needed | 0 | nothing | Elite Rank 7 Asmodian Staff (26) | 15 | 4 | 22 | muster1 |
| Spirit Master | Asimspirit 5133266, 115 | 26 | not needed | 0 | nothing | Elite Rank 7 Asmodian Spellbook (26) | 19 | 4 | 19 | muster2 |
| Bard | Asimbard 6133266, 116 | 26 | not needed | 0 | nothing | Elite Rank 7 Asmodian Harp (26) | 18 | 2 | 19 | muster2 |
| Gladiator | Asimgladiator 7133266, 117 | 26 | not needed | 0 | nothing | Altruist's Greatsword (25) | 13 | 4 | 29 | muster1 |
| Ranger | Asimranger 8133266, 118 | 26 | not needed | 0 | nothing | Elite Rank 7 Asmodian Bow (26) | 14 | 4 | 27 | muster1 |
| Rider | Asimrider 9133702, 119 | 26 | not needed | 0 | nothing | Elite Rank 7 Asmodian Cipher-Blade (26) | 13 | 4 | 28 | muster1 |
| Gunner | Asimgunner 10133266, 120 | 26 | not needed | 19 | Pistol for the off hand | Elite Rank 7 Asmodian Pistol (26) in each hand | 15 | 4 | 27 | muster1 |
| Assassin | Asimassassin 11133266, 121 | 26 | not needed | 19 | Dagger for the off hand | Elite Rank 7 Asmodian Dagger (26) in each hand | 16 | 4 | 28 | muster1 |

The containers are the Supply Scroll Bundles, four a character, and for the Templar the two Bronze Coin Chests its
turns of the coin quest paid. The Bard opened two of its bundles in muster1, before it stopped, and the other two in
muster2. Every character is at Morheim Ice Fortress, alive, and died in no muster. No character has a Bronze Coin left. The
kit of each is at its full count: the Major Life Potion, the Major Anti-Shock Scroll, the Greater Running Scroll, the
Cippo Aether Jelly and the Odella Powder for all; the Major Mana Serum and the Greater Awakening Scroll for the five
that cast from mana; the Greater Courage Scroll for the six that keep Courage up. In all 169 pieces of gear
were sold and 79 Bronze Coins supplied.

- **The Templar** was 63,822 XP short of level 26. Two turns of Q2318 gave 126,414 XP, about 61,000 to 65,000 a turn
  from the nine kills. It then bought the four armor pieces it lacked, the sword and the shield.
- **The Assassin and the Gunner** held the tier's weapon in the main hand and an Altruist's weapon of level 25 in the
  other. Each bought the tier's weapon a second time.
- **The Gladiator keeps Altruist's Greatsword (25).** Its weapon number, the one its gear rules wear by, is 2000390
  against 2000378 for the coin greatsword of level 26, so the rule does not buy it. This is as at the plan's finish.
- **The torso is the Morheim Dark Legionary piece of Q24020 for every class.** The coin tier's torso has the same
  physical defence, and a tie is not bought.
- A single character's record, for a run of one bot: `ntc-group-s1-<class>` for the nine of muster1 and
  `ntc-group-s2-spirit-master` and `ntc-group-s2-bard`. The world to start the next legs from is `ntc-group-s2`.

## Not done, and what the operator is asked

1. **Seventeen recorded gate scopes are stale and were not replayed** ("Do not rerun anything"). Every scope that
   reaches level 10 is supplied the new kit at its first stock check from there: the Priest's and the Cleric's seven
   (`p`, `m`, `b`, `l1`, `c`, `hm`, `ax`) and the ten of the second classes. The five starter scopes are unchanged.
   Until they are recorded again the neutral gate guards levels 1 to 9 only. Recording them is a replay of every
   leg twice, some hours; a Priest or Cleric scope needs `-ReRecord`, which is the operator's decision.
2. **Power shards.** Every character wears a stack of Lesser Power Shards (level 10) in a shard slot, and no bot ever
   switches shards on, so they do nothing. A shard is a consumable with a level (the Power Shard at 20). Switching
   them on and supplying the best one is not done: it is new bot behavior, and it changes every fight.
3. **The Lesser Supply Manastone Box** (three each) stays closed. It asks the player to choose one of three
   manastones, and no bot sockets a manastone.
4. **Only gear is sold.** Lower tiers of potions and scrolls, crafting materials, dyes and event items stay in the
   cube. Each character ends with 19 or more free slots.
5. **The Gladiator's greatsword**, above: say so if the coin greatsword is to be bought all the same.
6. **Levels 50 and up.** The kit's bands end at 49. The Fine tiers (restrict row 50) are for the leg that gets there.
7. **The earlier snapshots are superseded, not removed.** `ntc-ready-<class>-s1`, the round worlds they point at,
   `ntc-group-joined-s1-w1`, `ntc-group-s1-w1` and `mr-trial1-w1` are still under run/snapshots. With the second
   weapon in a tier's manifest, the Assassin's and the Gunner's `ntc-ready` snapshots no longer verify as an
   Abyss-entry endpoint; their characters in the group world do.
