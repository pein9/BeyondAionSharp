#!/usr/bin/env python3
"""The quest list of a zone for a natural leg, by hub (docs/natural-morheim-leveling.md, step M1).

Every quest of the zone an Asmodian can take, put with the obelisk its giver stands nearest to, and sorted into what
the bots can play as it is, what needs steps written for it, and what waits for something the bots do not do yet and
why. It reads the quest data, the compiled quest plans and their classifier (scripts/e2e/compile-quest-plans.py), the
spawns and zones of the map, and the group world's snapshot for what its characters have done. It also follows the
quests' own experience from the least experienced character on, to say what level the zone carries a character to.
It writes Markdown to stdout.

  python scripts/sim/zone-quest-list.py --zone Morheim --map 220020000 --snapshot ntc-group-s5 --until-level 31 \
      --classes GLADIATOR,TEMPLAR,ASSASSIN,RANGER,SORCERER,SPIRIT_MASTER,CLERIC,CHANTER > docs/natural-morheim-quests.md
  python scripts/sim/zone-quest-list.py --zone Brusthonin --map 220050000 --snapshot nl-g31-s2 --until-level 36       --classes GLADIATOR,TEMPLAR,ASSASSIN,RANGER,SORCERER,SPIRIT_MASTER,CLERIC,CHANTER       --step 'step NL-2 of [natural-leg-36.md](natural-leg-36.md)' > docs/natural-brusthonin-quests.md
"""
import argparse
import glob
import gzip
import io
import json
import math
import os
import re
import subprocess
import sys
import tempfile

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
STATIC = os.path.join(REPO, 'game-server', 'data', 'static_data')
# A zone name is the client's key; these are the ones whose title case is not the name a player sees.
NAMES = {'Kellans Cabin': "Kellan's Cabin"}
TAGS = {'[Group]': 'group', '[Gather]': 'gather', '[Spy]': 'spy'}
REPEATABLE_TAGS = ('[Coin]', '[Spend Coin]', '[Daily]', '[Manastone]', '[Event]')
# The verdicts, in the order a hub's table lists them.
LABELS = (
    ('plays', "its handler is one of the server's templates; the compiled plan is what the bot follows"),
    ('steps', 'its handler is written by hand, so its steps are written for the bot too, as for the Altgard legs'),
    ('group', 'a [Group] quest: played by a group, after the hubs'),
    ('spy', 'a [Spy] quest: not played (the operator, 2026-10-13: "Don\'t do spy quests")'),
    ('gather', 'it asks for gathering, which waits for the gathering work'),
    ('blocked', 'it cannot be taken as the server is: no handler, or a quest before it that cannot be had'),
    ('repeat', 'repeatable or an event: not part of playing the zone through once'),
    ('later', 'it starts at the level the leg ends on, or above: a later leg'),
    ('done', 'finished by every one of the classes'),
)
PLAYED = ('plays', 'steps')


def read(path):
    return io.open(path, encoding='utf-8').read()


def snapshot_rows(dump, table):
    head = re.search(r"CREATE TABLE `%s` \((.*?)\n\) ENGINE" % table, dump, re.S)
    columns = re.findall(r"\n\s+`(\w+)`", head.group(1))
    found = []
    for insert in re.finditer(r"INSERT INTO `%s` VALUES (.*?);\n" % table, dump, re.S):
        for row in re.finditer(r"\(((?:[^()']|'(?:[^'\\]|\\.)*')*)\)", insert.group(1)):
            values = [v.strip().strip("'") for v in re.split(r",(?=(?:[^']*'[^']*')*[^']*$)", row.group(1))]
            found.append(dict(zip(columns, values)))
    return found


def inside(point, polygon):
    x, y = point
    hit = False
    for i in range(len(polygon)):
        x1, y1 = polygon[i]
        x2, y2 = polygon[i - 1]
        if (y1 > y) != (y2 > y) and x < (x2 - x1) * (y - y1) / (y2 - y1) + x1:
            hit = not hit
    return hit


def area(polygon):
    return abs(sum(polygon[i][0] * polygon[i - 1][1] - polygon[i - 1][0] * polygon[i][1] for i in range(len(polygon)))) / 2


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--zone', required=True, help='the quest zone, as quest_data.xml names it')
    parser.add_argument('--map', required=True, type=int, help='the map id of the zone')
    parser.add_argument('--snapshot', required=True, help='the round snapshot whose characters play the leg')
    parser.add_argument('--until-level', required=True, type=int, help='the level the leg ends on: a quest that starts there or above is a later leg')
    parser.add_argument('--classes', required=True, help='the classes that play, comma separated')
    parser.add_argument('--step', default='step M1 of [natural-morheim-leveling.md](natural-morheim-leveling.md)',
                        help='the plan step the list is for, as Markdown')
    options = parser.parse_args()
    classes = options.classes.split(',')

    # The zone's quests, and every quest's name for the notes.
    quests = {}
    every_name = {}
    for match in re.finditer(r'<quest id="(\d+)"([^>]*?)(?:/>|>(.*?)</quest>)', read(os.path.join(STATIC, 'quest_data', 'quest_data.xml')), re.S):
        head, body = match.group(2), match.group(3) or ''
        name = re.search(r' name="([^"]*)"', head)
        every_name[int(match.group(1))] = name.group(1) if name else ''
        if 'quest_zone="%s"' % options.zone not in head:
            continue
        race = re.search(r'race_permitted="(\w+)"', head)
        if race and race.group(1) not in ('ASMODIANS', 'PC_ALL'):
            continue

        def number(key, default):
            found = re.search(r'%s="(\d+)"' % key, head)
            return int(found.group(1)) if found else default
        experience = re.search(r'<rewards[^>]*exp="(\d+)"', body)
        quests[int(match.group(1))] = {
            'id': int(match.group(1)), 'name': every_name[int(match.group(1))], 'level': number('minlevel_permitted', 0),
            'repeats': number('max_repeat_count', 1), 'category': (re.search(r'category="(\w+)"', head) or [0, 'QUEST'])[1],
            'cycle': 'repeat_cycle' in head, 'exp': int(experience.group(1)) if experience else 0,
        }

    # The compiled plans and the classifier.
    plans = {}
    with tempfile.TemporaryDirectory() as folder:
        subprocess.run([sys.executable, os.path.join(REPO, 'scripts', 'e2e', 'compile-quest-plans.py'), '--zone', options.zone, '--output', folder],
                       check=True, stdout=subprocess.DEVNULL)
        for path in glob.glob(os.path.join(folder, '*.json')):
            plan = json.load(io.open(path, encoding='utf-8'))
            plans[plan['quest']['id']] = plan
    classified = {entry['id']: entry for entry in json.load(io.open(os.path.join(REPO, 'parity-artifacts', 'e2e', 'obtainable-quests.json'), encoding='utf-8'))['quests']}

    # The map: where each npc stands, the obelisks, and the named places.
    spawn_file = glob.glob(os.path.join(STATIC, 'spawns', 'Npcs', '%d_*.xml' % options.map))[0]
    places = {}
    for match in re.finditer(r'<spawn npc_id="(\d+)"[^>]*>\s*<spot x="([^"]*)" y="([^"]*)"', read(spawn_file)):
        places.setdefault(int(match.group(1)), (float(match.group(2)), float(match.group(3))))
    npc_names = {int(m.group(1)): m.group(2) for m in re.finditer(r'<npc_template npc_id="(\d+)"[^>]*? name="([^"]*)"', read(os.path.join(STATIC, 'npcs', 'npc_templates.xml')))}
    zones = []
    for match in re.finditer(r'<zone ([^>]*)>(.*?)</zone>', read(os.path.join(STATIC, 'zones', 'zones_%d.xml' % options.map)), re.S):
        kind = re.search(r'zone_type="(\w+)"', match.group(1))
        points = [(float(x), float(y)) for x, y in re.findall(r'<point x="([^"]*)" y="([^"]*)"', match.group(2))]
        if len(points) >= 3 and (not kind or kind.group(1) == 'SUB'):
            zones.append((re.search(r'name="([^"]*)"', match.group(1)).group(1), points))
    hubs = []
    for npc in (int(n) for n in re.findall(r'<bind_point npcid="(\d+)"', read(os.path.join(STATIC, 'bind_points', 'bind_points.xml')))):
        if npc not in places:
            continue
        around = sorted((area(points), name) for name, points in zones if inside(places[npc], points))
        title = around[0][1].replace('_%d' % options.map, '').replace('_', ' ').title() if around else 'obelisk %d' % npc
        hubs.append((NAMES.get(title, title), places[npc]))

    def hub_of(npcs):
        for npc in npcs:
            if npc in places:
                x, y = places[npc]
                return min(hubs, key=lambda hub: math.hypot(hub[1][0] - x, hub[1][1] - y))[0], npc
        return None, None

    # What the playing characters have done, and where the least experienced of them stands.
    dump = gzip.open(os.path.join(REPO, 'run', 'snapshots', options.snapshot + '-w1', 'dump.sql.gz'), 'rt', encoding='utf-8', errors='replace').read()
    characters = [row for row in snapshot_rows(dump, 'players') if row['player_class'] in classes]
    playing = {row['id']: row['player_class'] for row in characters}
    done_by = {}
    for row in snapshot_rows(dump, 'player_quests'):
        if row['player_id'] in playing and row['status'] == 'COMPLETE':
            done_by.setdefault(int(row['quest_id']), set()).add(playing[row['player_id']])
    done = {quest for quest, who in done_by.items() if len(who) == len(playing)}
    least = min(int(row['exp']) for row in characters)
    table_path = glob.glob(os.path.join(STATIC, '**', 'player_experience_table.xml'), recursive=True)[0]
    table = [int(v) for v in re.findall(r'<exp>(\d+)</exp>', read(table_path))]

    def level_at(exp):
        return max(level for level in range(1, len(table) + 1) if table[level - 1] <= exp)

    # Each quest: its giver's hub, how its plan is made, and what it is for the leg.
    rows = []
    for quest in quests.values():
        plan = plans.get(quest['id'], {})
        entry = classified.get(quest['id'], {})
        handler = plan.get('handler', {})
        givers = [npc['id'] for npc in plan.get('startNpcs', [])]
        if not givers and handler.get('kind') == 'custom':
            # A hand-written handler names its npcs in its source; the first one that stands on this map is taken.
            givers = [int(n) for n in re.findall(r'\b(\d{6})\b', read(os.path.join(REPO, handler['source'])))]
        hub, giver = hub_of(givers)
        steps = []
        for step in plan.get('steps', []):
            if step['kind'] == 'kill':
                steps.append('kill %d' % step.get('count', 0))
            elif step['kind'] == 'collect':
                gathered = any(source.get('kind') == 'gatherable' for source in step.get('sources', []))
                steps.append('%s %d' % ('gather' if gathered else 'collect', step.get('count', 0)))
            elif step['kind'] != 'report':
                steps.append(step['kind'])
        waits = [condition['quest_id'] for group in plan.get('gates', {}).get('startConditions', []) for condition in group.get('finished', [])
                 if isinstance(condition, dict) and condition.get('quest_id') not in done]
        availability = entry.get('availability', '?')
        support = entry.get('planSupport', '?')
        tag = next((kind for prefix, kind in TAGS.items() if quest['name'].startswith(prefix)), None)
        why = ''
        if quest['id'] in done:
            verdict = 'done'
        elif quest['repeats'] > 1 or quest['cycle'] or quest['name'].startswith(REPEATABLE_TAGS) or quest['category'] in ('EVENT', 'COIN_QUEST'):
            verdict = 'repeat'
        elif quest['level'] > 65:
            verdict, why = 'blocked', 'switched off in the quest data (level %d)' % quest['level']
        elif quest['level'] >= options.until_level:
            verdict = 'later'
        elif availability != 'obtainable':
            verdict = 'blocked'
            why = 'no handler' if availability == 'no_handler' else '%s (%s)' % (availability, ', '.join(reason.get('code', '') for reason in entry.get('reasons', [])))
        elif tag:
            verdict = tag
        elif any(step.startswith('gather') for step in steps):
            verdict = 'gather'
        elif support == 'template':
            verdict = 'plays'
        else:
            verdict, why = 'steps', '' if support == 'custom' else support
        quest.update(hub=hub, giver=giver, handler=handler.get('kind', 'none'), steps=', '.join(steps), waits=waits, verdict=verdict, why=why)
        rows.append(quest)

    # A quest that follows one the bots do not play alone yet waits with it.
    by_id = {quest['id']: quest for quest in rows}
    changed = True
    while changed:
        changed = False
        for quest in rows:
            if quest['verdict'] not in PLAYED:
                continue
            blocked = [need for need in quest['waits'] if need not in by_id or by_id[need]['verdict'] not in PLAYED + ('done',)]
            if blocked:
                first = by_id.get(blocked[0])
                quest['verdict'] = first['verdict'] if first else 'blocked'
                quest['why'] = 'follows ' + ', '.join('Q%d %s' % (need, every_name.get(need, '')) for need in blocked)
                if not first:
                    quest['why'] += ', a quest of another zone that is not done'
                changed = True

    # How far the zone's own quests carry the least experienced character: it takes every quest it has the level for, and
    # the rewards give it the level for more. Kills are not counted, so this is the least a character ends with.
    exp, taken, marks = least, set(), []
    while True:
        level = level_at(exp)
        ready = [quest for quest in rows if quest['verdict'] in PLAYED and quest['id'] not in taken and quest['level'] <= level]
        if not ready:
            break
        for quest in sorted(ready, key=lambda q: (q['level'], q['id'])):
            taken.add(quest['id'])
            exp += quest['exp']
            if level_at(exp) > level:
                level = level_at(exp)
                marks.append((level, len(taken)))
    beyond = [quest for quest in rows if quest['verdict'] in PLAYED and quest['id'] not in taken]

    def total(kind, where='everywhere'):
        chosen = [quest for quest in rows if quest['verdict'] == kind and (where == 'everywhere' or quest['hub'] == where)]
        return len(chosen), sum(quest['exp'] for quest in chosen)

    def middle(hub):
        levels = sorted(quest['level'] for quest in rows if quest['hub'] == hub and quest['verdict'] in PLAYED)
        return levels[len(levels) // 2] if levels else 99

    out = []
    out.append('# %s: the quest list' % options.zone)
    out.append('')
    out.append('Generated by `scripts/sim/zone-quest-list.py` from the quest data, the compiled quest plans and `%s`; do not edit by' % options.snapshot)
    out.append('hand. It is %s.' % options.step)
    out.append('')
    out.append('The classes: %s. A quest is done when all of them have finished it.' % ', '.join(c.replace('_', ' ').title() for c in classes))
    out.append('')
    out.append('| For the leg | Quests | Experience | What it means |')
    out.append('|---|---:|---:|---|')
    for kind, meaning in LABELS:
        count, worth = total(kind)
        out.append('| %s | %d | %s | %s |' % (kind, count, format(worth, ','), meaning))
    out.append('')
    out.append('**The leg ends on level %d.** The least experienced of them starts at level %d. Taking every quest of the leg it has the' % (options.until_level, level_at(least)))
    out.append('level for, by the experience of the rewards alone and none for a kill, it ends at level %d with %d quests played' % (level_at(exp), len(taken)))
    out.append('(%s). %s' % (
        ', '.join('level %d after %d' % mark for mark in marks),
        'Every quest of the leg is within its reach.' if not beyond else
        '%d quests start above that level, the lowest at %d: they wait for experience from elsewhere.' % (len(beyond), min(q['level'] for q in beyond))))
    out.append('')
    order = {kind: index for index, (kind, _) in enumerate(LABELS)}
    for hub in [name for name, _ in sorted(hubs, key=lambda hub: middle(hub[0]))] + [None]:
        here = [quest for quest in rows if quest['hub'] == hub]
        if not here:
            continue
        plays, plays_exp = total('plays', hub)
        steps, steps_exp = total('steps', hub)
        levels = sorted(quest['level'] for quest in here if quest['verdict'] in PLAYED)
        out.append('## %s' % (hub or 'No giver on this map'))
        out.append('')
        out.append('%d play as they are (%s experience), %d need steps (%s)%s.' % (
            plays, format(plays_exp, ','), steps, format(steps_exp, ','), ', levels %d to %d' % (levels[0], levels[-1]) if levels else ''))
        out.append('')
        out.append('| Quest | Level | Experience | Giver | What it asks | For the leg |')
        out.append('|---|---:|---:|---|---|---|')
        for quest in sorted(here, key=lambda q: (order[q['verdict']], q['level'], q['id'])):
            asks = quest['steps'] or ('by its handler' if quest['handler'] == 'custom' else '')
            verdict = quest['verdict']
            if quest['why']:
                verdict += ': ' + quest['why']
            if quest['waits'] and quest['verdict'] in PLAYED:
                verdict += '; after ' + ', '.join('Q%d' % need for need in quest['waits'])
            out.append('| Q%d %s | %d | %s | %s | %s | %s |' % (quest['id'], quest['name'], quest['level'], format(quest['exp'], ','),
                                                          npc_names.get(quest['giver'], '') if quest['giver'] else '', asks, verdict))
        out.append('')
    sys.stdout.reconfigure(encoding='utf-8', newline='\n')
    sys.stdout.write('\n'.join(out) + '\n')
    return 0


if __name__ == '__main__':
    sys.exit(main())
