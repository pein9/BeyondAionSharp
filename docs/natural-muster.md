# One world, and the muster at the Abyss entry's end

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
   kept. Nothing that is not gear is sold.
4. The kit, every row at its full count.
5. A relog, the same character again, and the receipt `altgard-muster-completion.json`.

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

- [ ] **MR-1** No coin limit: the allowlist, the leg's contract and its checks; the shield and the second weapon in a
  tier's manifest.
- [ ] **MR-2** The kit and the use rule of (MR-c); the tests that pin them.
- [ ] **MR-3** `scripts/sim/join-world.py`, and the joined world of the eleven characters.
- [ ] **MR-4** The muster stage, played by every character in the joined world, and the group's capture.

## Not done, and what the operator is asked

See the closing section, written when MR-4 is played.
