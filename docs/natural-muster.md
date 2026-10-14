# One world, and the muster at the Abyss entry's end

Status: done, 2026-10-12; the stigma added 2026-10-13. The eleven Asmodian second classes stand in one world database,
the round snapshot `ntc-group-s5-w1`: each on an account of its own, level 26, in the best coin gear it can wear, the
best power shard in both shard slots and switched on, its class's stigma stone in its slot, its kit full, and nothing
in its cube that no rule uses. What is not done is at the end.

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

After the first report, the same day:

1. On recording the stale gate scopes again: "No we don't want to waste time on full reruns right now."
2. "Yes, they should use the best power shard possible (again like the other consumables) and we need to activate
   them at all times (when logging in, after death, when running out, etc). Do not rerun anything, just update our
   rules/scripts."
3. On the Gladiator's greatsword: "No, you said the quest reward was already better."
4. "Extra credit: We need to trash anything no longer needed from all inventories any time we do a gear check or
   restock supplies."

After the second report, the same day:

1. On the Ancient Power Shard at level 10: "We want the best they can use. This just keeps it simple."
2. On what still lay in the cubes: "We need to clean up things we don't need. We either need to use it or get rid of
   it. (this will change when we start training crafting/gathering skills, but not now)"

On 2026-10-13, before he went away: "Figure out what stigma we should select for each class at the stigma quest (we
are throwing away the bundle we get as the quest reward), and do the steps to socket the stigma on each class. Save
at the appropirate 'leg', maybe after our abyss entry? Update profiles as needed."

The same evening, back at the keyboard:

1. "New directive on what classes to have available: Only the original 8. So keep what we have up to level 26, but
   no more anything for Rider, Gunner, Bard."
2. "I want a standing rule for gear 'check ins' at 31, 36, 41, 46, all based on coin gear if any is an upgrade."

Later that evening, on the Morheim plan (`docs/natural-morheim-leveling.md`):

1. "the bots have 2 goals, One is to play every quest in the game, to see if anything is broken (and later we will do
   all other mechanics for crafting/gathering), and the other is for our companion bots."
2. "we want to only do legs until level 31, then make sure we do an equipment check (level 31 best coin gear). Then
   save a profile for level 31 to spawn a companion as (so we can look at Fire Temple). Don't do 'spy' quests. Group
   quests we might actually need to do in a group. Remember to spawn in the same world, 10 minutes apart so we can
   parallel training, maybe 5 at a time?"

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
3. The bags: the leg's containers, the coin quest's chest and the Supply Scroll Bundles are opened, and what no rule
   uses (MR-f) is sold to the fortress's nearest merchant, as far as a merchant takes it. The legs behind the muster
   are over, so what they protected is kept only if a rule uses it. The sale comes first, before anything is supplied
   or opened: a character can arrive with a full cube, and the cleanup would throw away what a merchant three steps
   off pays for. It is done again after the coin gear, for the pieces that gear replaced.
4. The kit, every row at its full count; the power shards in their slots and switched on (MR-e); the cube clean
   (MR-f).
5. Three free cube slots or more, a relog, the same character again, and the receipt
   `altgard-muster-completion.json`.

Deaths, a soul heal and the walk back are outcomes, as in every leg.

**(MR-e) The best power shard, always on.** A shard is worn like gear and used up like a consumable. The server
gates it by its restrict row, as it does a potion, and no shard has one. So from level 10 the kit supplies the shard
with the most damage, the Ancient Power Shard (+45 a hit): two stacks of 10,000, one for each shard slot. The right
slot's shard is used by the main hand, the left slot's by the off hand, and a two-hand weapon uses both at every hit
(Java `PlayerGameStats.getPowerShardDamage`), so both slots are filled for every class. Wherever the buff check runs
(before a pull, after a rest, after a revive, after a login, before a walk) the bot puts the best shard it owns into
both slots and switches the mode on when the server last said it was off. It reads the mode from the server's
messages (started, ended, burnt out) and from the state its own emotions carry. The toggle ends a cast or an item
use (Java `CM_EMOTION`), so it is sent only between actions. This server keeps the mode across a death and drops it
at a login. Below level 10 nothing changes: play there is as it was recorded.

**(MR-f) The bag cleanup: use it or get rid of it.** At every gear check and every restock the bot goes through its
cube. A stack stays only when a rule uses it:

- the kit of the level;
- a potion or scroll the class's policies reach for, while the kit's own tier is not owned in its place (a class
  that is supplied no mana serum drinks the mana potions it owns);
- the Tea of Repose and the experience amulet, which the bot now uses (below);
- gear the gear rules will wear, now or at a later level;
- what the leg being played names: the muster's coins and the containers it opens;
- an item of a quest that is being worked: started or ready to hand in, and not one the class line leaves out.

Everything else goes: sold where a merchant is at hand and takes it, discarded otherwise. That is the lower tiers
of a kit family, the speed scroll of the other kind, a weaker power shard once it is out of its slot, the powder of a
skill level left behind, gear and accessories the character has outgrown, the reward weapons that legs now over
protected, the items of finished quests, and whatever no bot rule touches: the Stigma Support Bundle and the
manastone boxes (no bot sockets either), the flight scrolls, the Iron Coins, dyes, juice, Azpha and the expiring
Administrator's Boon. What is worn is never touched. A stack the server will not discard is left and not asked for
again. Any of it can be supplied again on the day a rule uses it. The operator: this changes when crafting and
gathering are trained; their materials have no use until then.

Two things are used rather than thrown away, from level 10 and where the cleanup is on:

- **The Tea of Repose** refills the Energy of Repose, out of which a kill pays extra experience. It is drunk when
  no more than a twentieth of that energy is left. The bot reads the energy from the experience packet.
- **The Lodas Amulet III** adds a fifth to hunting experience for two hours, to level 49, one use in four hours. One
  is used before a pull when none is at work.

The rule is on in the muster and in every leg written from here on. It is off in the legs already played, Ishalgen
to the Abyss entry: their receipts count the items a leg ends with (the bridge's shop targets, the coin leg's
loadout, Haramel's ledger), and none was replayed with it.

**(MR-g) The stigma.** Q2900 "No Escaping Destiny" opens a character's stigma slots and pays the Stigma Support
Bundle, which gives its opener one stone of its class, of eight or nine. One slot is open below level 30 (Java
`StigmaService.getPossibleStigmaCount`), so a class wears one stone. It is the stone whose skill the class's fight
table can cast as it stands: on one target or on the character itself, no charge skill, no area (a bot pulls one
monster at a time), and with the weapon the class holds. Among those, the one that adds most to what the class does
alone and in a group. The table is below.

The muster opens the bundle for that stone and puts it in at the fortress's Stigma Master (Herner, 798382, ten metres
from Nott). The server asks for no Stigma Master; his dialog is what opens the client's window. The price is 25,000
Kinah at the server's service rate, 35,312 at the fortress (Java `StigmaService.notifyEquipAction`). The cleanup
(MR-f) keeps the bundle and the stone until the stone is in its slot; a character whose bundle the cleanup threw
away before this rule is supplied one. The skills come back at every login while the stone is worn. The legs before
the muster are as they were played: they keep the bundle sealed.

The quest data also lists a stone by class as a reward to choose. It is never given: the list is not switched on
(`use_class_reward`), in Java as here, and no skill of the 4.8 data comes with its stones.

| Class | Stone | Its skill at level 26 | In the class's table |
|---|---|---|---|
| Gladiator | Crippling Cut (140001304) | a weapon hit with 473 more, on a target that is thrown down; every 18 s | first, while the target is seen thrown down, which Wrathful Strike does seven times in ten |
| Templar | Inquisitor's Blow (140001328) | a weapon hit with 438 more from 15 m; every 30 s | its pull, before Taunt, and an attack; its attack in flight |
| Ranger | Bow of Blessing (140001363) | a fifth more attack with a bow for 60 s; every 3 min | cast on itself as Devotion is, while an attack reaches the target |
| Assassin | Searching Strike (140001345) | a weapon hit with 720 more, likelier to be critical, and a stun of 1.5 s; every 30 s | before a chain is opened |
| Sorcerer | Ice Harpoon (140001384) | 810 magic damage from 25 m, a cast of 2.5 s; every 6 s | before the harpoon it learns |
| Spirit Master | Cyclone of Wrath (140001399) | 225 magic damage and 225 every 2 s for 8 s, a cast of 2 s; every 12 s | with its other damage that lasts |
| Cleric | Saving Grace (140001431) | for 60 s, 1,917 health once its target is at half; no cast; every 2 min | at half its own health; in the healing role, on a member at half |
| Chanter | Word of Life (140001414) | 250 health every 2 s for 10 s, for the group within 20 m; no cast; every 60 s | at 70% of its health |
| Bard | Hymn of Rejuvenation (140001484) | 1,166 health and 145 every 2 s for 8 s; no cast; every 60 s | at 55% of its health, before the heals that have a cast |
| Gunner | Power Grab (140001452) | 254 magic damage with two pistols, all of it back as health and half as mana; every 30 s | after its chains |
| Rider | Drillbore (140001466) | 374 magic damage from the mech and a stun of 3 s, from 6 m; every 60 s | opens the fight at the target |

What was left out and why: the Gunner's other attacks need a cannon or are a chain of their own beside its two, and
the Rider's are charges or areas. For the Bard, Freestyle is a 15 s sleep from 25 m: it would make the Bard a second
class for crowd control (the Zzz brand), and gives it nothing alone. For the Cleric, Sympathetic Heal is the quicker
heal for a group and cannot be cast on the Cleric itself. A stone is changed by supplying another bundle and playing
the muster again.

**(MR-h) The original eight classes from here on.** Every leg after the muster, and everything new that is built for
a class, is for the Gladiator, the Templar, the Assassin, the Ranger, the Sorcerer, the Spirit Master, the Cleric and
the Chanter. The Rider, the Gunner and the Bard stay as they stand at level 26: their characters in the group world,
their fight tables, their stones and their companion profiles are kept and not taken further. A round of the next
legs has no bot for them; a capture carries them over as they are (`lastPlayedIn`). All three classes are in the 4.8
data; this is the operator's choice of what the bots play.

**(MR-i) Gear check-ins at levels 31, 36, 41 and 46.** At each of these levels a character goes to the seller of
that level's coin gear and takes every piece that beats what it wears, by rule (MR-b): the armor, the weapon, the
shield or the second weapon of its class, no coin limit, the coins the manifest is short supplied, a tie not
bought. It is the muster's coin step at four more levels, with the sale of what was replaced and the cleanup after
it. The leg that reaches a level plays its check-in. What the item data gives an Asmodian:

| Level | Gear | Coin | Sold in |
|---:|---|---|---|
| 21, 26 (played) | Rank 8, Elite Rank 7 | Bronze Coin (186000007) | Morheim Ice Fortress |
| 31 | Elite Rank 6 | Silver Coin (186000008) | Morheim, Beluslan, Reshanta |
| 36 | Elite Rank 5 | Silver Coin | the same sellers |
| 41 | Elite Rank 4 | Gold Coin (186000009) | Beluslan, Reshanta |
| 46 | Elite Rank 3 | Platinum Coin (186000010) | Brusthonin, Pandaemonium |

The next tier, Elite Rank 2 for Mithril Coins, is level 51 and outside the rule as it was given.

**(MR-j) A leg, from here on.** A leg runs to the next check-in level and ends on that check-in: to 31, then to 36,
41 and 46. It plays every quest of its zone that starts below that level, since the bots are there to play every
quest in the game and find what is broken; experience is never the reason to leave a quest or a hub out. Group
quests are played in a group. Spy quests are not played. Gathering quests wait for the gathering work. A quest that
cannot be taken is written down with its reason. A round is up to five bots in the one world, ten game minutes
apart. At a leg's end the profiles of its level are generated, so that a companion can be brought at that level.

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
| Power shard | never supplied; a Lesser stack worn and never on | Ancient Power Shard (169000011), 20,000 | every class |
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

- [x] **MR-5** The best power shard, in both slots and on (MR-e).
- [x] **MR-6** The bag cleanup at every gear check and restock (MR-f).
  - 2026-10-12, commit `346edbf27`, for both. The bundle (run/mr/MR-5, chain): `Aion.GameServer.Tests` 4,632 passed, 16
    skipped; the seven pre-commit checks, the three script tests and Fast pass; the five starter scopes are identical.
  - Round muster3 (run/mr/MR-5/muster3), from `ntc-group-s2`, captured as `ntc-group-s3`: all eleven played the muster
    again, ten game minutes apart, and all reached its end. No leg was replayed. The table is under "After the shards
    and the cleanup".

- [x] **MR-7** Use it or get rid of it (MR-f as it now stands).
  - 2026-10-12, commit `5cbe80a34`. The bundle (run/mr/MR-7b, chain): `Aion.GameServer.Tests` 4,632 passed, 16 skipped;
    the seven pre-commit checks, the three script tests and Fast pass; the five starter scopes are identical.
  - Round trial7 (run/mr/MR-5/trial7), the Templar's catch-up once more from `ntc-group-joined-s1`, not captured: the
    shards went on at login, the amulet was used at the first pull, and the nine kills of one turn gave 72,257 XP
    where they had given 61,491. Its Energy of Repose was at 194,729 of 436,346, so no tea was due.
  - Round muster4 (run/mr/MR-7/muster4), from `ntc-group-s3`, captured as `ntc-group-s4`: all eleven played the muster
    again and reached its end. The table is under "After use it or get rid of it".

- [x] **MR-8** The stigma (MR-g): a stone for each class, the muster's step, the group's capture, the companions.
  - 2026-10-13, commit `e03d71120`. The stone of each class is in `NaturalStigma`; a class's catalog takes the
    stone's skills as rows, and its table gives them a place. Rounds trial9 (from `ntc-group-s4`, the bundle
    supplied) and trial10 (from `ntc-group-s3`, the character's own bundle), neither captured: all eleven put their
    stone in. Round muster5 (run/mr/MR-8/muster5), from `ntc-group-s3`, captured as `ntc-group-s5`.
  - The checks (run/mr/MR-8, chain1 and chain4): `Aion.GameServer.Tests` 4,646 passed, 16 skipped; the pre-commit
    checks; the companion server tests; the five starter scopes identical; Fast passes.
  - Commit `ffac3e513`: the companions' profiles from `ntc-group-s5`, without what only a quest uses. The server test
    holds a made Cleric to its stone, the stigma quest and the two skills its login teaches.
  - The rounds from `ntc-group-s5` are in the table under "After the stigma". The second companion round found a
    fault of the companions that has nothing to do with stones, fixed in commit `4c5d9e435` and written down in
    `docs/companion-live-host.md`: a player that went into the camp and stood still was not followed.

## The group, as the muster left it (`ntc-group-s2-w1`)

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

## After the shards and the cleanup (`ntc-group-s3-w1`)

Round muster3 changed no level, no gear and no coin. What it did for each character:

| Class | Shards worn, each slot | Mode | Stacks discarded | Items sold | Cube stacks | Free cube slots |
|---|---|---|---:|---:|---|---:|
| Cleric | Ancient Power Shard, 10,000 | on | 18 | 6 | 65 to 43 | 43 |
| Templar | Ancient Power Shard, 10,000 | on | 13 | 11 | 66 to 44 | 43 |
| Sorcerer | Ancient Power Shard, 10,000 | on | 20 | 5 | 63 to 40 | 44 |
| Chanter | Ancient Power Shard, 10,000 | on | 18 | 5 | 63 to 42 | 44 |
| Spirit Master | Ancient Power Shard, 10,000 | on | 20 | 6 | 67 to 43 | 44 |
| Bard | Ancient Power Shard, 10,000 | on | 20 | 6 | 64 to 40 | 44 |
| Gladiator | Ancient Power Shard, 10,000 | on | 11 | 5 | 55 to 41 | 44 |
| Ranger | Ancient Power Shard, 10,000 | on | 12 | 5 | 57 to 42 | 43 |
| Rider | Ancient Power Shard, 10,000 | on | 11 | 5 | 57 to 43 | 43 |
| Gunner | Ancient Power Shard, 10,000 | on | 12 | 5 | 57 to 42 | 43 |
| Assassin | Ancient Power Shard, 10,000 | on | 11 | 5 | 56 to 42 | 43 |

In all 166 stacks were discarded and 64 items sold. Each character sold its Mana Potions, Recovery
Serums, Aether Powder and enchantment stones, which no bot uses; five of them a hat they had outgrown; the Templar
also the six pieces its coin gear had replaced. Each discarded its Lesser Power Shards, the lower tiers of its potions and scrolls, the veteran event
scrolls, the Zeller jellies, the Lesser Odella Powder and the speed scrolls of the other kind. The server refused no
discard.

What a cube still holds beside the kit, about twenty stacks: the reward weapons the earlier legs protect (the
ceremony weapon, the Altruist's and the Dark Legionary weapon), the Greater Raging Wind Scrolls of the ring course,
the Stigma Support Bundle and the three manastone boxes, the Tea of Repose, Azpha, the Mercenary's Fruit Juice, the
dyes, the event amulets and pass, the Iron Coins and a few leftovers of finished quests.

`ntc-group-s3` is this state. The next round, below, cleared the cubes further.

## After use it or get rid of it (`ntc-group-s4-w1`)

Round muster4 changed no level, no gear, no coin and no shard. What it did for each character:

| Class | Items sold | Stacks discarded | Stacks left in the cube | Free cube slots |
|---|---:|---:|---:|---:|
| Cleric | 6 | 12 | 9 | 54 |
| Templar | 5 | 12 | 10 | 53 |
| Sorcerer | 5 | 10 | 9 | 54 |
| Chanter | 5 | 12 | 9 | 54 |
| Spirit Master | 5 | 13 | 9 | 54 |
| Bard | 5 | 10 | 9 | 54 |
| Gladiator | 5 | 10 | 10 | 53 |
| Ranger | 5 | 11 | 10 | 53 |
| Rider | 5 | 12 | 10 | 53 |
| Gunner | 5 | 10 | 10 | 53 |
| Assassin | 5 | 10 | 10 | 53 |

In all 56 items were sold and 122 stacks discarded; the server refused no discard. Sold: the Karmic
ceremony weapon, the Altgard Dark Legionary weapon where one was kept, the Greater Raging Wind Scrolls, Azpha and the
dyes. Discarded: the Altruist's weapon where it is not worn (it cannot be sold), the Stigma Support Bundle, the three
manastone boxes, the Iron Coins, the Mercenary's Fruit Juice, the Administrator's Boon and the items of finished
quests.

What a cube holds now, 9 to 10 stacks, each one used by a rule: the kit (the Major Life Potion, the
Major Anti-Shock Scroll, the Greater Running Scroll, the Cippo Aether Jelly, the Odella Powder, with the Major Mana
Serum and the Greater Awakening Scroll for a class that casts and the Greater Courage Scroll for one that does not),
the mana potions a class without a serum still drinks, the Tea of Repose and two Lodas Amulets. The Kinah is beside
it. Every cube has 53 or 54 free slots.

That was the world to start the next legs from until 2026-10-13. It is `ntc-group-s5` now, below.

## After the stigma (`ntc-group-s5-w1`)

Round muster5 (run/mr/MR-8/muster5) changed no level, no gear, no coin and no shard. It was played from
`ntc-group-s3`, where every cube still held the bundle Q2900 paid, and did what muster4 did before its own step. Each
of the eleven opened its own bundle for its class's stone and had it put in at Herner for 35,312 Kinah. No bundle was
supplied, nobody died, and every character reached the muster's end with the level, the gear, the shards, the kit
and the free cube slots it has in `ntc-group-s4`, and with that world's Kinah less the price. The stone and both
ranks of its skill are there again after a relog. From `ntc-group-s4`, where the bundle is gone, the muster supplies
one and ends the same to the Kinah (round trial9).

One thing differs from `ntc-group-s4`. The server gave five characters the daily quest Q49020 "[Daily] Aegir's
Order" at this login (the Gladiator, the Ranger, the Rider, the Gunner and the Assassin, the last five to log in).
The cleanup keeps what a quest's script gave while any quest is worked, so these five still hold the Hunter's Secret
Remedy, the Rusty Key and the Lubricating Oil that muster4 threw away. They take no cube slot.

The stones in play, in rounds from `ntc-group-s5` that were not captured. The number is how often the skill was cast:

| Round | Who | Casts |
|---|---|---|
| `s5-n9`, the Group NTC leg, to its end | the six of that leg, the Cleric healing | Inquisitor's Blow 41, Ice Harpoon 35, Bow of Blessing 7, Crippling Cut 1, Saving Grace 1, Word of Life 0 |
| `s5-c9`, companions to the General | a stand-in Templar leading a Cleric, a Gladiator, a Ranger, a Sorcerer and a Chanter | Inquisitor's Blow 16, Ice Harpoon 24, Bow of Blessing 4, Crippling Cut 1, Saving Grace 1, Word of Life 1 |
| `s5-c9b-3`, companions to the General | the stand-in leading an Assassin, a Spirit Master, a Bard, a Gunner and a Rider | Searching Strike 9, Cyclone of Wrath 15, Power Grab 9, Drillbore 6, Hymn of Rejuvenation 0 |

Ten of the eleven stones were cast. The Bard was never at 55% of its health, so its Hymn was not needed. The
Gladiator's Crippling Cut came once a round: in a group its fights end before Wrathful Strike is ready a second
time. The Chanter's Word of Life and the Cleric's Saving Grace wait for somebody to be hurt.

**The world to start the next legs from is `ntc-group-s5`.** A single character's record is `ntc-group-s5-<class>`.
The companions' profiles are generated from it (`scripts/sim/make-companion-profiles.py`): each wears its stone, and
the server makes a companion with the stigma quest done, without which the stone would be taken out at its login.

## Not done, and what the operator is asked

1. **Seventeen recorded gate scopes are stale and stay so.** Every scope that reaches level 10 is supplied the new
   kit at its first stock check from there, and now turns its shards on: the Priest's and the Cleric's seven (`p`,
   `m`, `b`, `l1`, `c`, `hm`, `ax`) and the ten of the second classes. The operator, 2026-10-12: no full reruns now.
   Until they are recorded again the neutral gate guards levels 1 to 9 only, the five starter scopes.
2. **The best shard is the Ancient Power Shard at every level from 10**, because no shard in the item data has a
   restrict row. The operator, 2026-10-12: "We want the best they can use. This just keeps it simple."
3. **Shards, the tea, the amulet and the cleanup start at level 10**, like the kit. Below it a character keeps what
   it has and switches nothing on, so the starter scopes stay valid.
4. **The cleanup, the tea and the amulet are off in the legs already played** (MR-f). A leg played again from an
   earlier snapshot behaves as it did; turning them on there means replaying that leg.
5. **Two systems no bot uses yet, whose items were therefore thrown away**: manastones (the three boxes) and
   enchanting (the enchantment stones, sold in muster3). When a bot rule uses one of them its items are kept, and
   what is gone can be supplied. Stigmas are used since 2026-10-13 (MR-g). The Administrator's Boon (an hour without the death
   penalty, reusable until it expires three days after it was given) was discarded and not used: it would change
   what a death costs, and it was about to expire.
6. **The tea has not been drunk in a run yet.** No character's Energy of Repose was low enough in a round that
   hunted. The rule is the amulet's, which was used, with another condition.
7. **The Gladiator keeps Altruist's Greatsword (25).** The operator, 2026-10-12: the quest reward is the better
   weapon.
8. **Levels 50 and up.** The kit's bands end at 49. The Fine tiers (restrict row 50) are for the leg that gets there.
9. **The earlier snapshots are superseded, not removed.** `ntc-ready-<class>-s1`, the round worlds they point at,
   `ntc-group-joined-s1-w1`, `ntc-group-s1-w1` to `ntc-group-s4-w1` and `mr-trial1-w1` are still under run/snapshots.
   With the second weapon in a tier's manifest, the Assassin's and the Gunner's `ntc-ready` snapshots no longer verify
   as an Abyss-entry endpoint; their characters in the group world do.
10. **One stone a class, chosen by me** (MR-g and its table). The operator asked for the choice; it is his to change.
    The two I would ask about: the Bard's Freestyle, a sleep, in place of its heal, which would give the companions a
    second class for crowd control; and the Gladiator, whose stone is cast once a round in a group.
11. **The second slot opens at level 30.** The bundle gives one stone. The next one is for the leg that gets there.
12. **The legs before the muster keep the bundle sealed**, as they were played and recorded. A class played again
    from the start puts its stone in at the muster, from its own bundle.
