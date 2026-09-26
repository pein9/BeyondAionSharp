# Game-clock and wall time per quest, for one run or a pair (SIM and/or LIVE).
# Usage: python scripts/sim/trace/step_times.py <runA> [runB]
import sys, collections
from trace_input import events, seconds, wall_seconds


def profile(source):
    times = collections.OrderedDict()
    first = last = None
    clock = None
    for event in events(source):
        step = event.get('step', '')
        quest = step.split('-')[1] if step.startswith('ni07-') else step
        at = (seconds(event), wall_seconds(event))
        if first is None:
            first = at
            clock = 'SIM virtual' if event.get('vt') is not None else 'LIVE wall'
        if quest not in times:
            times[quest] = [at, at]
        times[quest][1] = at
        last = at
    if first is None:
        raise ValueError(f'{source}: empty trace')
    print(f'{source}: {clock} {last[0]-first[0]:.0f}s, wall {last[1]-first[1]:.0f}s')
    return {quest: (end[0]-start[0], end[1]-start[1]) for quest, (start, end) in times.items()}


if len(sys.argv) not in (2, 3):
    raise SystemExit('Usage: step_times.py <runA> [runB]')
profiles = [profile(source) for source in sys.argv[1:]]
print('quest           ' + ' | '.join(' clock s wall s' for _ in profiles))
for quest in dict.fromkeys(quest for report in profiles for quest in report):
    values = [report.get(quest) for report in profiles]
    if any(value and (value[0] > 120 or value[1] > 10) for value in values):
        print(f'{quest:14} ' + ' | '.join(f'{value[0]:7.0f} {value[1]:6.0f}' if value else ' ' * 14 for value in values))
