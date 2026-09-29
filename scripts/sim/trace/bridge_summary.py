# The Ascension bridge's run record (docs/natural-ascension-altgard.md NA-24): deaths and recoveries, movies,
# help items supplied and used, buffs cast, bridge decisions and the endpoint.
# Usage: python scripts/sim/trace/bridge_summary.py <run directory or trace>
import collections, json, sys
from pathlib import Path
from trace_input import events, stamp

if len(sys.argv) != 2:
    raise SystemExit('Usage: bridge_summary.py <run directory or trace>')
source = sys.argv[1]
deaths, recoveries, movies, supplied, used, buffs, decisions = [], [], [], [], [], collections.Counter(), []
for event in events(source):
    kind, fields, step = event.get('packet'), event.get('fields') or {}, event.get('step', '')
    if kind == 'SM_DIE' or step.startswith('ni07-bind-revive'):
        if kind == 'SM_DIE':
            deaths.append((stamp(event), step))
    # A recovery is a login (fresh, resumed or after an interruption) or a bind revive, counted once per step.
    if ('login' in step or 'bind-revive' in step or 'relog' in step) and (not recoveries or recoveries[-1][1] != step):
        recoveries.append((stamp(event), step, kind))
    if kind in ('movie-watched', 'movie-skipped'):
        movies.append((stamp(event), step, kind, fields.get('movieId'), fields.get('questId')))
    if kind == 'help-item-supplied':
        supplied.append((stamp(event), fields.get('trigger'), fields.get('itemId'), fields.get('count')))
    if kind == 'help-item-used':
        used.append((stamp(event), step, fields.get('itemId')))
    if kind in ('buff-blessing',):
        buffs[fields.get('skillId')] += 1
    if kind == 'ascension-bridge-decision':
        decisions.append(fields.get('action'))
print(f'deaths: {len(deaths)} {deaths}')
print(f'recoveries (logins, relogs, bind revives): {len(recoveries)}')
for row in recoveries:
    print('  ', row)
print(f'movies: {len(movies)}')
for row in movies:
    print('  ', row)
print(f'help items supplied: {len(supplied)}')
for row in supplied:
    print('  ', row)
print(f'help items used: {len(used)}')
for row in used:
    print('  ', row)
print(f'class buffs cast: {dict(buffs)}')
print(f'bridge decisions: {len(decisions)} {dict(collections.Counter(decisions))}')
directory = Path(source) if Path(source).is_dir() else Path(source).parent
for name in ('bridge-stop.json', 'bridge-completion.json', 'help-items.json'):
    path = directory / name
    print(f'{name}: {"present" if path.exists() else "missing"}')
