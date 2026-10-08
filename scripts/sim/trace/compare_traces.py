#!/usr/bin/env python3
"""Compare two natural-journey traces record by record, or count what one trace holds.

    python scripts/sim/trace/compare_traces.py <traceA> <traceB> [--ignore PATH]...
    python scripts/sim/trace/compare_traces.py --counts <trace> [--json]
    python scripts/sim/trace/compare_traces.py --digest <trace> [--ignore PATH]...

A trace is one bot's .trace.jsonl file, or a run folder that holds exactly one (trace_input.events).

Comparing drops what differs between two runs of the same code and seed: the wall time stamp `ts`, the run id `run`
and the whole `natural-run-context` record (run id, build and module id). Everything else must be equal, the virtual
clock `vt` included. --ignore names one more field to leave out, as a dotted path from the record (`fields.kinah`),
or for one packet only (`service-trade:fields.kinahAfter`). A list on the path is followed into every element.
Both traces are streamed, never loaded whole. The exit code is 0 only when the two are identical.

--counts says how often a trace runs the code a refactor item moves (docs/natural-class-profiles.md, section 8).
--digest prints the record count and the SHA-256 of the normalized records, the form a baseline is stored in.
"""
from __future__ import annotations

import argparse
import hashlib
import itertools
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from trace_input import events  # noqa: E402

DROPPED_KEYS = ('ts', 'run')
SKIPPED_PACKETS = ('natural-run-context',)

REVIVE_STEPS = ('accept-client-death-and-revive-at-bound-obelisk', 'accept-client-death-and-revive-inside-the-instance',
                'quest-instance-self-revival')
SPEED_FAMILIES = ('awakening', 'courage')
RUNNING_FAMILIES = ('running', 'movement-speed')

# The printed order. Each count names the trace records it reads, so an item can check that a scope runs its code.
COUNTS = (
    ('records', 'every record of the trace'),
    ('deaths', 'SM_DIE received'),
    ('reviveSteps', 'bind, instance and quest self revives (accept-client-death-..., quest-instance-self-revival)'),
    ('retreats', 'combat-decision with action retreat'),
    ('retreatRoutes', 'combat-retreat-route and combat-retreat-cornered'),
    ('restSitsTraced', 'rest-relocate and rest-relocate-none; a sit at a spot that is already clear writes no record'),
    ('restInterrupts', 'rest-interrupted-by-attack and powder-rest-interrupted'),
    ('betweenFightHeals', 'between-fights-heal'),
    ('powderRestCasts', 'powder-rest-decision that names a skill'),
    ('vendorBuys', 'service-trade with a purchase, inventory-maintenance with elixirs bought, coin-purchase-receipt'),
    ('pullPlans', 'pull-plan'),
    ('patrolWaits', 'pull-wait-for-patrol, patrol-decision with action wait, campaign-zone-patrol-wait'),
    ('emergencyDecisions', 'combat-decision with inEmergency'),
    ('atTargetPulls', 'adds-that-would-join whose purpose ends in -at-target'),
    ('lifePotions', 'combat-hot-potion'),
    ('restLifePotions', 'rest-life-potion: a life potion drunk between fights by a class with no heal of its own'),
    ('restSitsForHealth', 'rest-sit-for-health: a sit for HP while the potion is on its delay or none is owned'),
    ('manaPotions', 'combat-mana-potion'),
    ('shieldScrolls', 'help-item-used, family anti-shock, one item consumed'),
    ('speedScrolls', 'help-item-used, family awakening or courage, one item consumed'),
    ('runningScrolls', 'help-item-used, family running or movement-speed, one item consumed'),
    ('helpItemsSupplied', 'help-item-supplied'),
    ('binds', 'service-bind-result with outcome done'),
    ('soulHeals', 'service-soul-heal with outcome done'),
)


def parse_ignore(specs):
    """'packet:a.b' or 'a.b' -> (packet or None, ('a', 'b'))."""
    ignores = []
    for spec in specs:
        packet, _, path = spec.rpartition(':')
        keys = tuple(part for part in path.split('.') if part)
        if not keys:
            raise ValueError(f"--ignore '{spec}' names no field")
        ignores.append((packet or None, keys))
    return ignores


def remove_path(node, keys):
    if isinstance(node, list):
        for item in node:
            remove_path(item, keys)
    elif isinstance(node, dict):
        if len(keys) == 1:
            node.pop(keys[0], None)
        elif keys[0] in node:
            remove_path(node[keys[0]], keys[1:])


def normalized(source, ignores=()):
    """The records that must repeat: no run context record, no ts, no run, none of the ignored fields."""
    for event in events(source):
        packet = event.get('packet')
        if packet in SKIPPED_PACKETS:
            continue
        for key in DROPPED_KEYS:
            event.pop(key, None)
        for only, keys in ignores:
            if only is None or only == packet:
                remove_path(event, keys)
        yield event


def canonical(event):
    return json.dumps(event, sort_keys=True, separators=(',', ':'), ensure_ascii=False)


def compare(source_a, source_b, ignores=()):
    """(identical, records in A, records in B, index of the first difference or None, record A, record B)."""
    count_a = count_b = 0
    first = None
    for index, (a, b) in enumerate(itertools.zip_longest(normalized(source_a, ignores), normalized(source_b, ignores))):
        count_a += a is not None
        count_b += b is not None
        if first is None and a != b:
            first = (index, a, b)
    if first is None:
        return True, count_a, count_b, None, None, None
    return False, count_a, count_b, first[0], first[1], first[2]


def differing_paths(a, b, prefix=''):
    if isinstance(a, dict) and isinstance(b, dict):
        paths = []
        for key in sorted(set(a) | set(b)):
            if key not in a or key not in b:
                paths.append(prefix + key)
            else:
                paths.extend(differing_paths(a[key], b[key], prefix + key + '.'))
        return paths
    return [] if a == b else [prefix.rstrip('.') or '<record>']


def digest(source, ignores=()):
    sha = hashlib.sha256()
    count = 0
    for event in normalized(source, ignores):
        sha.update(canonical(event).encode('utf-8'))
        sha.update(b'\n')
        count += 1
    return count, sha.hexdigest()


def counts(source):
    totals = dict.fromkeys((name for name, _ in COUNTS), 0)
    for event in events(source):
        totals['records'] += 1
        packet = event.get('packet')
        fields = event.get('fields') or {}
        if event.get('dir') != 'action':
            if event.get('dir') == '<' and packet == 'SM_DIE':
                totals['deaths'] += 1
            continue
        if packet in REVIVE_STEPS:
            totals['reviveSteps'] += 1
        elif packet == 'combat-decision':
            totals['retreats'] += fields.get('action') == 'retreat'
            totals['emergencyDecisions'] += bool(fields.get('inEmergency'))
        elif packet in ('combat-retreat-route', 'combat-retreat-cornered'):
            totals['retreatRoutes'] += 1
        elif packet in ('rest-relocate', 'rest-relocate-none'):
            totals['restSitsTraced'] += 1
        elif packet in ('rest-interrupted-by-attack', 'powder-rest-interrupted'):
            totals['restInterrupts'] += 1
        elif packet == 'between-fights-heal':
            totals['betweenFightHeals'] += 1
        elif packet == 'powder-rest-decision':
            totals['powderRestCasts'] += fields.get('skillId') is not None
        elif packet == 'service-trade':
            totals['vendorBuys'] += bool(fields.get('bought'))
        elif packet == 'inventory-maintenance':
            totals['vendorBuys'] += (fields.get('elixirsBought') or 0) > 0
        elif packet == 'coin-purchase-receipt':
            totals['vendorBuys'] += 1
        elif packet == 'pull-plan':
            totals['pullPlans'] += 1
        elif packet in ('pull-wait-for-patrol', 'campaign-zone-patrol-wait'):
            totals['patrolWaits'] += 1
        elif packet == 'patrol-decision':
            totals['patrolWaits'] += fields.get('action') == 'wait'
        elif packet == 'adds-that-would-join':
            totals['atTargetPulls'] += str(fields.get('purpose') or '').endswith('-at-target')
        elif packet == 'combat-hot-potion':
            totals['lifePotions'] += 1
        elif packet == 'rest-life-potion':
            totals['restLifePotions'] += 1
        elif packet == 'rest-sit-for-health':
            totals['restSitsForHealth'] += 1
        elif packet == 'combat-mana-potion':
            totals['manaPotions'] += 1
        elif packet == 'help-item-used':
            before, after = fields.get('before'), fields.get('after')
            if before is not None and after == before - 1:
                family = fields.get('family')
                totals['shieldScrolls'] += family == 'anti-shock'
                totals['speedScrolls'] += family in SPEED_FAMILIES
                totals['runningScrolls'] += family in RUNNING_FAMILIES
        elif packet == 'help-item-supplied':
            totals['helpItemsSupplied'] += 1
        elif packet == 'service-bind-result':
            totals['binds'] += fields.get('outcome') == 'done'
        elif packet == 'service-soul-heal':
            totals['soulHeals'] += fields.get('outcome') == 'done'
    return totals


def shown(event):
    return '<end of trace>' if event is None else canonical(event)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    parser.add_argument('traces', nargs='+', help='two traces to compare, or one for --counts and --digest')
    parser.add_argument('--counts', action='store_true', help='print how often one trace runs each kind of step')
    parser.add_argument('--digest', action='store_true', help='print the record count and SHA-256 of one normalized trace')
    parser.add_argument('--json', action='store_true', help='print --counts or --digest as one JSON object')
    parser.add_argument('--ignore', action='append', default=[], metavar='PATH',
                        help="a field left out of the comparison: 'fields.x.y' or 'packet:fields.x.y'")
    arguments = parser.parse_args(argv)
    try:
        ignores = parse_ignore(arguments.ignore)
        if arguments.counts or arguments.digest:
            if arguments.counts and arguments.digest or len(arguments.traces) != 1:
                parser.error('--counts and --digest each take one trace')
            if arguments.counts:
                totals = counts(arguments.traces[0])
                print(json.dumps(totals) if arguments.json else '\n'.join(f'{totals[name]:8} {name}' for name, _ in COUNTS))
            else:
                records, sha = digest(arguments.traces[0], ignores)
                print(json.dumps({'records': records, 'sha256': sha}) if arguments.json else f'{records} records, sha256 {sha}')
            return 0
        if len(arguments.traces) != 2:
            parser.error('comparing takes two traces')
        identical, count_a, count_b, index, a, b = compare(arguments.traces[0], arguments.traces[1], ignores)
    except (OSError, ValueError) as error:
        print(f'compare_traces: {error}', file=sys.stderr)
        return 2
    if identical:
        print(f'identical: {count_a} records')
        return 0
    print(f'different: first at record {index} (counted from 0, after the run context record)')
    if a is not None and b is not None:
        print('fields: ' + ', '.join(differing_paths(a, b)))
    print(f'A: {shown(a)}')
    print(f'B: {shown(b)}')
    print(f'records: A {count_a}, B {count_b}')
    return 1


if __name__ == '__main__':
    sys.exit(main())
