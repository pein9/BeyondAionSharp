#!/usr/bin/env python3
"""Companion profiles (docs/companion-bots.md, step C1).

A companion is a throwaway character the server makes for a player who asks for one. What it wears, carries and has
learned is its profile. The profiles are generated from the group world, where every class stands at its level in the
gear, shards and supplies the natural runs gave it. They are generated data: change the world they come from and
generate them again, never edit them by hand.

  python scripts/sim/make-companion-profiles.py                 # write game-server/data/companions from the SOURCES
  python scripts/sim/make-companion-profiles.py --check         # the checked-in profiles against the item data, and
                                                                # against a fresh generation when the snapshots are here

Step M8 of docs/natural-morheim-leveling.md adds a second level: the level-31 profiles of the eight original classes, from
the world of the gear check-in at 31 (rule MR-h: the Rider, the Gunner and the Bard stay at 26).

A snapshot lives under run/snapshots, which is not checked in. Without it --check still reads every profile and checks
its items; it says that it could not compare.
"""
import argparse
import glob
import gzip
import hashlib
import io
import json
import os
import re
import sys

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
KINAH = 182400001
# What a companion carries beyond what its character of the group world carries (the operator, 2026-10-13: "add the mana
# potion threshold ... for companions only"). The Major Mana Potion restores mana over twenty seconds and has no restrict
# row; a companion drinks one at half its mana. The natural runs have no such rule, so the group world has no such potion.
COMPANION_EXTRAS = {162000011: 30}
# A companion takes no quest: what a quest gave its character of the group world, and only a quest uses, is left out.
QUEST_GROUPS = ('QUEST', 'KEY')
UNEQUIPPED_SLOT = 65535
# The worlds the profiles come from, each with the classes it gives (None: every character in it).
ORIGINAL_EIGHT = ('GLADIATOR', 'TEMPLAR', 'ASSASSIN', 'RANGER', 'SORCERER', 'SPIRIT_MASTER', 'CLERIC', 'CHANTER')
SOURCES = (('ntc-group-s5', None), ('morheim-g31-s1', ORIGINAL_EIGHT))
CUBE = 0
# PlayerClass ids, which index an item's restrict row.
CLASS_IDS = {'WARRIOR': 0, 'GLADIATOR': 1, 'TEMPLAR': 2, 'SCOUT': 3, 'ASSASSIN': 4, 'RANGER': 5, 'MAGE': 6, 'SORCERER': 7,
             'SPIRIT_MASTER': 8, 'PRIEST': 9, 'CLERIC': 10, 'CHANTER': 11, 'ENGINEER': 12, 'RIDER': 13, 'GUNNER': 14,
             'ARTIST': 15, 'BARD': 16}


def rows(dump, table):
    """The rows of a table in a mysqldump text, each a dict by column name."""
    created = re.search(r'CREATE TABLE `%s` \((.*?)\n\) ENGINE' % re.escape(table), dump, re.S)
    if not created:
        raise SystemExit('The dump has no table %s.' % table)
    columns = re.findall(r'^\s+`(\w+)` ', created.group(1), re.M)
    out = []
    for insert in re.finditer(r'INSERT INTO `%s` VALUES (.*?);\n' % re.escape(table), dump, re.S):
        for values in tuples(insert.group(1)):
            if len(values) != len(columns):
                raise SystemExit('A row of %s has %d values for %d columns.' % (table, len(values), len(columns)))
            out.append(dict(zip(columns, values)))
    return out


def tuples(text):
    """The value tuples of an INSERT: numbers, NULL, and quoted strings with their escapes."""
    at, end = 0, len(text)
    while at < end:
        if text[at] != '(':
            at += 1
            continue
        at += 1
        values = []
        while True:
            if text[at] == "'":
                at += 1
                value = []
                while True:
                    char = text[at]
                    if char == '\\':
                        value.append(text[at + 1])
                        at += 2
                    elif char == "'" and text[at + 1:at + 2] == "'":
                        value.append("'")
                        at += 2
                    elif char == "'":
                        at += 1
                        break
                    else:
                        value.append(char)
                        at += 1
                values.append(''.join(value))
            else:
                stop = at
                while text[stop] not in ',)':
                    stop += 1
                word = text[at:stop]
                values.append(None if word == 'NULL' else (float(word) if any(c in word for c in '.eE') else int(word)))
                at = stop
            if text[at] == ',':
                at += 1
                continue
            at += 1  # the closing bracket
            break
        yield values


def item_data():
    """Every item template's level and restrict row, by id."""
    items = {}
    for path in sorted(glob.glob(os.path.join(REPO, 'game-server/data/static_data/items/*.xml'))):
        text = io.open(path, encoding='utf-8').read()
        for match in re.finditer(r'<item_template id="(\d+)"([^>]*)>', text):
            attributes = match.group(2)
            level = re.search(r' level="(\d+)"', attributes)
            restrict = re.search(r' restrict="([\d ]+)"', attributes)
            race = re.search(r' race="(\w+)"', attributes)
            name = re.search(r' name="([^"]*)"', attributes)
            group = re.search(r' item_group="(\w+)"', attributes)
            items[int(match.group(1))] = {
                'level': int(level.group(1)) if level else 0,
                'restrict': [int(v) for v in restrict.group(1).split()] if restrict else None,
                'race': race.group(1) if race else None,
                'name': name.group(1) if name else '',
                'group': group.group(1) if group else None,
            }
    return items


def problems_of(profile, items):
    """What is wrong with a profile against the item data: an item that does not exist, or one its class cannot wear."""
    found = []
    class_id = CLASS_IDS[profile['playerClass']]
    for worn in profile['equipment']:
        item = items.get(worn['itemId'])
        if item is None:
            found.append('worn item %d does not exist' % worn['itemId'])
            continue
        # The server's gate is the item's restrict row: the level each class needs, 0 for a class that cannot use it. An
        # item without a row has no gate, whatever its own level (a power shard), as docs/natural-muster.md rule MR-c says.
        needed = item['restrict'][class_id] if item['restrict'] else 0
        if item['restrict'] and needed == 0:
            found.append('%s cannot wear %d %s' % (profile['playerClass'], worn['itemId'], item['name']))
        elif needed > profile['level']:
            found.append('%d %s needs level %d' % (worn['itemId'], item['name'], needed))
        if item['race'] not in (None, 'PC_ALL', profile['race']):
            found.append('%d %s is for %s' % (worn['itemId'], item['name'], item['race']))
    for carried in profile['inventory']:
        if carried['itemId'] not in items:
            found.append('carried item %d does not exist' % carried['itemId'])
    return found


def generate(snapshot, items, classes=None):
    folder = os.path.join(REPO, 'run', 'snapshots', snapshot + '-w1')
    record = json.load(io.open(os.path.join(folder, 'snapshot.json'), encoding='utf-8'))
    raw = io.open(os.path.join(folder, 'dump.sql.gz'), 'rb').read()
    digest = hashlib.sha256(raw).hexdigest()
    if digest != record['dumpSha256']:
        raise SystemExit('The dump of %s does not have the hash its record names.' % snapshot)
    dump = gzip.decompress(raw).decode('utf-8', errors='replace')
    players = {row['id']: row for row in rows(dump, 'players')}
    appearances = {row['player_id']: row for row in rows(dump, 'player_appearance')}
    inventory = rows(dump, 'inventory')
    skills = rows(dump, 'player_skills')
    socketed = {row['item_unique_id'] for row in rows(dump, 'item_stones')}
    profiles = {}
    for character in record['characters']:
        player = players[character['characterId']]
        if classes is not None and player['player_class'] not in classes:
            continue
        own = [row for row in inventory if row['item_owner'] == player['id'] and row['item_location'] == CUBE]
        if any(row['item_unique_id'] in socketed for row in own):
            raise SystemExit('%s has a socketed item; a profile does not carry stones yet.' % player['name'])
        appearance = dict(appearances[player['id']])
        del appearance['player_id']
        profile = {
            'schemaVersion': 1,
            'race': player['race'],
            'playerClass': player['player_class'],
            'level': character['level'],
            'gender': player['gender'],
            'appearance': appearance,
            'equipment': sorted(({'slot': row['slot'], 'itemId': row['item_id'], 'count': row['item_count'], 'enchant': row['enchant']}
                                 for row in own if row['is_equipped']), key=lambda worn: (worn['slot'], worn['itemId'])),
            'inventory': sorted([{'itemId': row['item_id'], 'count': row['item_count']}
                                 for row in own if not row['is_equipped'] and row['item_id'] != KINAH
                                 and row['item_id'] not in COMPANION_EXTRAS
                                 and items.get(row['item_id'], {}).get('group') not in QUEST_GROUPS]
                                + [{'itemId': item, 'count': count} for item, count in COMPANION_EXTRAS.items()],
                                key=lambda carried: carried['itemId']),
            'skills': sorted(({'id': row['skill_id'], 'level': row['skill_level']} for row in skills if row['player_id'] == player['id']),
                             key=lambda skill: skill['id']),
            'source': {'snapshot': record['name'], 'dumpSha256': digest, 'character': player['name']},
        }
        key = '%s-%s-%d' % (profile['race'].lower(), profile['playerClass'].lower().replace('_', '-'), profile['level'])
        if key in profiles:
            raise SystemExit('Two characters of %s would be the profile %s.' % (snapshot, key))
        profiles[key] = profile
    return profiles


def text_of(profile):
    return json.dumps(profile, indent=2, ensure_ascii=False) + '\n'


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--snapshot', help='only this round snapshot, every character in it (default: the SOURCES)')
    parser.add_argument('--out', default=os.path.join(REPO, 'game-server', 'data', 'companions'))
    parser.add_argument('--check', action='store_true', help='check the profiles that are there; write nothing')
    options = parser.parse_args()
    items = item_data()
    sources = ((options.snapshot, None),) if options.snapshot else SOURCES
    named = ', '.join(snapshot for snapshot, _ in sources)
    present = all(os.path.isfile(os.path.join(REPO, 'run', 'snapshots', snapshot + '-w1', 'dump.sql.gz')) for snapshot, _ in sources)

    def generate_all():
        profiles = {}
        for snapshot, classes in sources:
            for key, profile in generate(snapshot, items, classes).items():
                if key in profiles:
                    raise SystemExit('Two sources would give the profile %s.' % key)
                profiles[key] = profile
        return profiles
    failed = False
    if options.check:
        paths = sorted(glob.glob(os.path.join(options.out, '*.json')))
        if not paths:
            raise SystemExit('No companion profile is there to check.')
        fresh = generate_all() if present else None
        for path in paths:
            key = os.path.splitext(os.path.basename(path))[0]
            written = io.open(path, encoding='utf-8', newline='').read().replace('\r\n', '\n')
            for problem in problems_of(json.loads(written), items):
                print('%s: %s' % (key, problem))
                failed = True
            if fresh is not None and written != text_of(fresh.get(key, {})):
                print('%s: differs from a fresh generation from %s' % (key, named))
                failed = True
        if fresh is not None and set(fresh) != {os.path.splitext(os.path.basename(path))[0] for path in paths}:
            print('The profiles that are there are not the ones %s give.' % named)
            failed = True
        print('%d companion profiles checked%s.' % (len(paths), '' if present else
                                                   '; the snapshots are not here, so they were not compared with a fresh generation'))
        return 1 if failed else 0
    if not present:
        raise SystemExit('The snapshots %s are not all under run/snapshots.' % named)
    profiles = generate_all()
    for key, profile in sorted(profiles.items()):
        for problem in problems_of(profile, items):
            print('%s: %s' % (key, problem))
            failed = True
    if failed:
        return 1
    os.makedirs(options.out, exist_ok=True)
    for stale in glob.glob(os.path.join(options.out, '*.json')):
        os.remove(stale)
    for key, profile in sorted(profiles.items()):
        io.open(os.path.join(options.out, key + '.json'), 'w', encoding='utf-8', newline='\n').write(text_of(profile))
        print('%s: %d worn, %d carried, %d skills' % (key, len(profile['equipment']), len(profile['inventory']), len(profile['skills'])))
    return 0


if __name__ == '__main__':
    sys.exit(main())
