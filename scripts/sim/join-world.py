#!/usr/bin/env python3
"""One world for every character (the operator, 2026-10-12: one world database, because the goal is group play).

    python scripts/sim/join-world.py --plan <file> --name <name>

The plan names the characters to bring together, each by the snapshot that holds it:

    { "characters": [ { "snapshot": "ntc-ready-cleric-s1", "line": "priest-cleric" },
                      { "snapshot": "ntc-ready-chanter-s1", "line": "priest-chanter", "name": "Asimchanter" } ] }

    snapshot  a capture of one character (its own dump), or a round character (a record that points at a round snapshot).
    line      the class line the character plays (NaturalClassLine), as a round file names it.
    name      a new name, when two characters of the plan have the same one; a world holds a name once.

The result is the round snapshot <name>-w1 under run/snapshots: one dump with every character in it, and a record of
each as scripts/sim/run-round.ps1 -From <name> reads it. The plan's order is the seat order: the first character is seat
b01 on account 111, the next b02 on 112, and so on.

What is changed, and nothing else: a character's id and the ids of its items are moved to a range of their own (the
source worlds gave the same ids to different characters), its account is its seat's, and its name is the plan's when the
plan gives one. Every other column is copied as it is. The world's own tables (the clock, the sieges, the towns) are
those of the source with the latest clock, and the joined world's clock is that clock.

The sources are never written to. Each is checked against the hash its record holds, restored into a throwaway schema,
read, and dropped. Only aion_gs_sim_ni08_* schemas on the development MySQL are touched.
"""
from __future__ import annotations

import argparse
import datetime
import hashlib
import json
import pathlib
import re
import subprocess
import sys
import uuid

REPO = pathlib.Path(__file__).resolve().parents[2]
OWNED = re.compile(r'^aion_gs_sim_ni08_[a-z0-9_]+$')
NAME = re.compile(r'^[a-z0-9][a-z0-9-]*$')
# The world's own tables. Every other table holds a character's rows or an account's, or must be empty.
WORLD_TABLES = {'server_variables', 'siege_locations', 'towns', 'legion_dominion_locations'}
# A character's ids move up by its seat number times this; every source id has to be below it.
ID_RANGE = 1_000_000
FIRST_ACCOUNT = 111
# Java IDFactory: an id with these bits is never given out.
INVALID_MASK, INVALID_CHECK = 0b0010011111100110101111111111100, 0b0000000000000000001100101010100


class Database:
    def __init__(self, docker: str, container: str, password: str):
        self.docker, self.container, self.password = docker, container, password
        self.created: list[str] = []

    def _run(self, arguments: list[str], data: bytes | None = None) -> str:
        done = subprocess.run([self.docker, *arguments], input=data, capture_output=True)
        if done.returncode != 0:
            raise SystemExit(f"docker {' '.join(arguments[:6])} ... exited with {done.returncode}: {done.stderr.decode(errors='replace').strip()}")
        return done.stdout.decode('utf-8', errors='replace')

    def sql(self, statements: str, schema: str | None = None) -> list[list[str]]:
        arguments = ['exec', '-i', '-e', f'MYSQL_PWD={self.password}', self.container, 'mysql', '-uroot', '-N', '-B']
        out = self._run(arguments + ([schema] if schema else []), statements.encode('utf-8'))
        return [line.split('\t') for line in out.splitlines() if line]

    def new_schema(self) -> str:
        name = 'aion_gs_sim_ni08_' + uuid.uuid4().hex
        self.sql(f'CREATE DATABASE `{name}`;')
        self.created.append(name)
        return name

    def restore(self, dump: pathlib.Path) -> str:
        schema = self.new_schema()
        remote = f'/tmp/{schema}.sql.gz'
        self._run(['cp', str(dump), f'{self.container}:{remote}'])
        self._run(['exec', '-e', f'MYSQL_PWD={self.password}', self.container, 'sh', '-c', f'gunzip < {remote} | mysql -uroot {schema} && rm -f {remote}'])
        return schema

    def dump(self, schema: str, target: pathlib.Path) -> None:
        remote = f'/tmp/{schema}.sql.gz'
        self._run(['exec', '-e', f'MYSQL_PWD={self.password}', self.container, 'sh', '-c',
                   f'mysqldump -uroot --single-transaction --no-tablespaces --routines --triggers {schema} | gzip > {remote}'])
        self._run(['cp', f'{self.container}:{remote}', str(target)])
        self._run(['exec', self.container, 'rm', '-f', remote])

    def drop_all(self) -> None:
        for schema in self.created:
            if not OWNED.match(schema):
                raise SystemExit(f'Refusing to drop a schema this script does not own: {schema}')
            self.sql(f'DROP DATABASE IF EXISTS `{schema}`;')
        self.created.clear()

    def columns(self, schema: str) -> dict[str, list[str]]:
        tables: dict[str, list[str]] = {}
        for table, column in self.sql(
                f"SELECT TABLE_NAME, COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = '{schema}' ORDER BY TABLE_NAME, ORDINAL_POSITION;"):
            tables.setdefault(table, []).append(column)
        return tables

    def counts(self, schema: str, tables: list[str], where: dict[str, str] | None = None) -> dict[str, int]:
        rows = self.sql(''.join(f"SELECT '{table}', COUNT(*) FROM `{table}`{' WHERE ' + where[table] if where and table in where else ''};" for table in tables), schema)
        return {table: int(count) for table, count in rows}


def sha256(path: pathlib.Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read_record(root: pathlib.Path, name: str) -> dict:
    path = root / name / 'snapshot.json'
    if not path.is_file():
        raise SystemExit(f'Snapshot {name} is missing: {path}')
    return json.loads(path.read_text(encoding='utf-8-sig'))


def source_of(root: pathlib.Path, snapshot: str) -> dict:
    """The dump that holds a snapshot's character, checked against the hash its record holds."""
    record = read_record(root, snapshot)
    holder, held = snapshot, record
    if record.get('source') == 'natural-round-character':
        holder = record['roundSnapshot']
        held = read_record(root, holder)
        if held.get('source') != 'natural-round':
            raise SystemExit(f'Snapshot {snapshot} points at {holder}, which is not a round snapshot.')
    dump = root / holder / 'dump.sql.gz'
    if sha256(dump) != held['dumpSha256']:
        raise SystemExit(f'The dump of {holder} differs from the hash its record holds; refusing to read an edited snapshot.')
    return {'snapshot': snapshot, 'holder': holder, 'dump': dump, 'dumpSha256': held['dumpSha256'], 'characterId': int(record['characterId']),
            'elapsedMillis': int(held['elapsedMillis']), 'level': record.get('level'), 'sourceCommit': record.get('gitSha')}


def select_list(columns: list[str], changed: dict[str, str]) -> str:
    return ', '.join(changed.get(column, f'`{column}`') for column in columns)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument('--plan', required=True, type=pathlib.Path)
    parser.add_argument('--name', required=True)
    parser.add_argument('--snapshot-root', type=pathlib.Path, default=REPO / 'run/snapshots')
    parser.add_argument('--docker', default='docker')
    parser.add_argument('--container', default='aion-mysql')
    parser.add_argument('--root-password', default='aion')
    parser.add_argument('--git', default='git')
    parser.add_argument('--seed', type=int, default=1)
    options = parser.parse_args()
    if not NAME.match(options.name):
        raise SystemExit(f'Not a snapshot name: {options.name}')
    root: pathlib.Path = options.snapshot_root
    target_name = f'{options.name}-w1'
    target_directory = root / target_name
    if target_directory.exists():
        raise SystemExit(f'Snapshot already exists: {target_directory} (snapshots are never overwritten)')
    plan = json.loads(options.plan.read_text(encoding='utf-8-sig'))['characters']
    if not plan or len({entry['line'] for entry in plan}) != len(plan):
        raise SystemExit('The plan needs at least one character, and one character for a class line.')
    sources = [source_of(root, entry['snapshot']) for entry in plan]
    commit = subprocess.run([options.git, '-C', str(REPO), 'rev-parse', 'HEAD'], capture_output=True, text=True).stdout.strip()

    database = Database(options.docker, options.container, options.root_password)
    try:
        restored: dict[pathlib.Path, str] = {}
        for source in sources:
            if source['dump'] not in restored:
                restored[source['dump']] = database.restore(source['dump'])
            source['schema'] = restored[source['dump']]
        # The world's own tables and its clock: the source with the latest clock.
        base = max(sources, key=lambda source: source['elapsedMillis'])
        target = database.restore(base['dump'])
        layout = database.columns(target)
        character_tables = sorted(table for table, columns in layout.items() if 'player_id' in columns and table != 'players')
        known = WORLD_TABLES | set(character_tables) | {'players', 'inventory', 'account_stamps'}
        database.sql('SET FOREIGN_KEY_CHECKS = 0;' + ''.join(f'DELETE FROM `{table}`;' for table in [*character_tables, 'inventory', 'account_stamps', 'players'])
                     + 'SET FOREIGN_KEY_CHECKS = 1;', target)
        left = {table: count for table, count in database.counts(target, sorted(layout)).items() if count and table not in WORLD_TABLES}
        if left:
            raise SystemExit(f'The base world holds rows this script does not know how to carry: {left}')

        characters, joined = [], []
        names: set[str] = set()
        for seat, (entry, source) in enumerate(zip(plan, sources), start=1):
            schema, old_id, offset, account = source['schema'], source['characterId'], seat * ID_RANGE, FIRST_ACCOUNT + seat - 1
            if database.columns(schema) != layout:
                raise SystemExit(f"{source['holder']} has another table layout than the base world; it cannot be joined.")
            unknown = {table: count for table, count in database.counts(schema, sorted(layout)).items() if count and table not in known}
            if unknown:
                raise SystemExit(f"{source['holder']} holds rows this script does not know how to carry: {unknown}")
            player = database.sql(f'SELECT name, account_id, account_name, player_class, exp, world_id FROM players WHERE id = {old_id};', schema)
            if len(player) != 1:
                raise SystemExit(f"{source['holder']} does not hold character {old_id}.")
            old_name, old_account, old_account_name, player_class, experience, world = player[0]
            name = entry.get('name') or old_name
            if not re.fullmatch(r'[A-Za-z]{2,16}', name) or name in names:
                raise SystemExit(f"Seat {seat}: '{name}' is no name for this world, or another seat has it. Give the plan a name for it.")
            names.add(name)
            highest = int(database.sql(f'SELECT GREATEST({old_id}, COALESCE(MAX(item_unique_id), 0)) FROM inventory WHERE item_owner IN ({old_id}, {old_account});', schema)[0][0])
            if highest >= ID_RANGE:
                raise SystemExit(f"{source['holder']} has the id {highest}; the ranges of {ID_RANGE} are too small.")
            own_items = f'(item_owner = {old_id} AND item_location <> 2) OR (item_owner = {old_account} AND item_location = 2)'
            others = int(database.sql(f'SELECT COUNT(*) FROM inventory WHERE item_owner = {old_account} AND item_location = 2 AND '
                                      f'(SELECT COUNT(*) FROM players WHERE account_id = {old_account}) > 1;', schema)[0][0])
            if others:
                raise SystemExit(f"{source['holder']}: account {old_account} holds a warehouse that two characters share; it cannot be split.")
            invalid = database.sql(f'SELECT item_unique_id + {offset} FROM inventory WHERE ({own_items}) AND ((item_unique_id + {offset}) & {INVALID_MASK}) = {INVALID_CHECK} '
                                   f'UNION SELECT {old_id + offset} FROM DUAL WHERE ({old_id + offset} & {INVALID_MASK}) = {INVALID_CHECK};', schema)
            if invalid:
                raise SystemExit(f'Seat {seat}: the moved ids {invalid} are ids the server never gives out.')
            copy = [f"INSERT INTO `{target}`.players SELECT " + select_list(layout['players'], {
                        'id': f'id + {offset}', 'name': f"'{name}'", 'account_id': str(account), 'account_name': f"'sim-player-{account}'"})
                    + f' FROM players WHERE id = {old_id};']
            for table in character_tables:
                copy.append(f'INSERT INTO `{target}`.`{table}` SELECT ' + select_list(layout[table], {'player_id': f'player_id + {offset}'})
                            + f' FROM `{table}` WHERE player_id = {old_id};')
            copy.append(f'INSERT INTO `{target}`.inventory SELECT ' + select_list(layout['inventory'], {
                'item_unique_id': f'item_unique_id + {offset}', 'item_owner': f'IF(item_location = 2, {account}, {old_id + offset})'})
                + f' FROM inventory WHERE {own_items};')
            copy.append(f'INSERT INTO `{target}`.account_stamps SELECT ' + select_list(layout['account_stamps'], {'account_id': str(account)})
                        + f' FROM account_stamps WHERE account_id = {old_account};')
            database.sql('START TRANSACTION;' + ''.join(copy) + 'COMMIT;', schema)

            # Every row of the character arrived, and only those.
            where_old = {table: f'player_id = {old_id}' for table in character_tables} | {'players': f'id = {old_id}', 'inventory': own_items,
                                                                                          'account_stamps': f'account_id = {old_account}'}
            where_new = {table: f'player_id = {old_id + offset}' for table in character_tables} | {
                'players': f'id = {old_id + offset}', 'inventory': f'item_owner = {old_id + offset} OR (item_owner = {account} AND item_location = 2)',
                'account_stamps': f'account_id = {account}'}
            carried = [*character_tables, 'players', 'inventory', 'account_stamps']
            before, after = database.counts(schema, carried, where_old), database.counts(target, carried, where_new)
            if before != after or after['players'] != 1:
                raise SystemExit(f'Seat {seat}: the rows that arrived differ from the rows read: {before} -> {after}')
            completed = int(database.sql(f"SELECT COUNT(*) FROM player_quests WHERE player_id = {old_id + offset} AND status = 'COMPLETE';", target)[0][0])
            characters.append({'seat': f'b{seat:02d}', 'line': entry['line'], 'stage': 'ax', 'account': account, 'name': name, 'characterId': old_id + offset,
                               'outcome': 'reached', 'level': source['level'], 'completedQuests': completed, 'step': None, 'message': None,
                               'playerClass': player_class, 'experience': int(experience), 'mapId': int(world)})
            joined.append({'seat': f'b{seat:02d}', 'snapshot': source['snapshot'], 'dump': source['holder'], 'dumpSha256': source['dumpSha256'],
                           'sourceCommit': source['sourceCommit'], 'characterId': old_id, 'account': int(old_account), 'accountName': old_account_name,
                           'name': old_name, 'rows': after})
            print(f"  b{seat:02d} {entry['line']}: {old_name} {old_id} of {source['holder']} -> {name} {old_id + offset} on account {account}, "
                  f"{after['inventory']} items, {completed} quests")

        total = database.counts(target, ['players', 'inventory'])
        if total['players'] != len(plan) or total['inventory'] != sum(entry['rows']['inventory'] for entry in joined):
            raise SystemExit(f'The joined world holds {total}; the plan has {len(plan)} characters.')
        target_directory.mkdir(parents=True)
        database.dump(target, target_directory / 'dump.sql.gz')
        record = {
            'schemaVersion': 1, 'name': target_name, 'source': 'natural-round', 'round': options.name, 'world': 1, 'from': None,
            'run': 'join-world', 'seed': options.seed, 'gitSha': commit,
            'capturedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat().replace('+00:00', 'Z'),
            'elapsedMillis': base['elapsedMillis'], 'dumpSha256': sha256(target_directory / 'dump.sql.gz'),
            'joinedBy': 'scripts/sim/join-world.py', 'worldTablesFrom': base['holder'], 'idRange': ID_RANGE, 'joined': joined, 'characters': characters,
        }
        (target_directory / 'snapshot.json').write_text(json.dumps(record, indent=2) + '\n', encoding='utf-8')
        print(f'Joined {len(plan)} characters into {target_directory}')
        return 0
    finally:
        database.drop_all()


if __name__ == '__main__':
    sys.exit(main())
