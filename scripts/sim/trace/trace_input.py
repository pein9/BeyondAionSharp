"""Read one bot's SIM or LIVE trace without mistaking a problem ledger for a trace."""
from datetime import datetime
from pathlib import Path
import json


def events(source):
    path = Path(source)
    candidates = [path] if path.is_file() else sorted(path.glob('*.trace.jsonl'))
    if not candidates:
        candidates = sorted((path / 'bots').glob('*.trace.jsonl'))
    if len(candidates) != 1:
        raise ValueError(f'{source}: expected one bot trace, found {len(candidates)}; specify its file explicitly')
    with candidates[0].open(encoding='utf-8-sig') as stream:
        for line in stream:
            if line.strip():
                yield json.loads(line)


def wall_seconds(event):
    return datetime.fromisoformat(event['ts'].replace('Z', '+00:00')).timestamp()


def seconds(event):
    """SIM uses its virtual clock; LIVE has vt=null and uses recorded wall time."""
    virtual = event.get('vt')
    if virtual is None:
        return wall_seconds(event)
    hours, minutes, seconds_part = virtual.split(':')
    days = 0
    if '.' in hours:
        days, hours = hours.split('.')
    return int(days) * 86400 + int(hours) * 3600 + int(minutes) * 60 + float(seconds_part)


def stamp(event):
    return event.get('vt') or event['ts']
