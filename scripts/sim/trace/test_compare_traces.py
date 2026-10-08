#!/usr/bin/env python3
"""CP-02: the trace comparer and its counts mode, on small traces written here."""
import contextlib
import io
import json
import tempfile
import unittest
from pathlib import Path

import compare_traces


def record(index, packet, direction='action', step='ni07-q2001-kill-1', run='run-a', **fields):
    return {'ts': f'2026-10-07T01:00:{index:02d}.000Z', 'vt': f'00:00:{index:02d}.000', 'run': run, 'bot': 'b01',
            'account': 'sim-player-41', 'step': step, 'dir': direction, 'packet': packet, 'fields': fields}


def journey(run):
    """A few records of each direction, with the run context record a real trace starts with."""
    return [
        record(0, 'natural-run-context', run=run, step='ni08-run',
               context={'Run': run, 'Seed': 1, 'Build': 'b-' + run, 'ModuleId': 'm-' + run, 'ElapsedMilliseconds': 0}),
        record(1, 'create-natural-asmodian-priest', run=run, step='ni07-create'),
        record(2, 'SM_STATS_INFO', '<', run=run, level=1, maxHp=201, currentHp=201),
        record(3, 'pull-plan', run=run, purpose='quest-kill-210377', chosenAction='pull',
               observedState={'targets': [{'ObjectId': 55499, 'TemplateId': 210377}], 'monsters': []}),
        record(4, 'CM_CASTSPELL', '>', run=run, skillId=1802, targetObjectId=55499),
        record(5, 'combat-decision', run=run, action='cast-target', skillId=1802, hp=180, inEmergency=False),
        record(6, 'service-trade', run=run, vendor=70177, bought=['162000053x12'], kinahBefore=900, kinahAfter=600),
    ]


class CompareTracesTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)

    def write(self, name, records, in_run_folder=False):
        path = self.root / name / 'b01.trace.jsonl' if in_run_folder else self.root / (name + '.trace.jsonl')
        path.parent.mkdir(parents=True, exist_ok=True)
        # A real trace is compact JSON with a byte order mark; trace_input reads it as utf-8-sig.
        path.write_text(''.join(json.dumps(item, separators=(',', ':')) + '\n' for item in records), encoding='utf-8-sig')
        return str(path.parent if in_run_folder else path)

    def run_main(self, *arguments):
        output, errors = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(output), contextlib.redirect_stderr(errors):
            code = compare_traces.main(list(arguments))
        return code, output.getvalue(), errors.getvalue()

    def test_two_runs_that_differ_only_in_ts_run_and_the_context_record_are_identical(self):
        a = self.write('a', journey('run-a'))
        later = journey('run-b')
        for item in later:
            item['ts'] = item['ts'].replace('T01:', 'T09:')
        b = self.write('b', later, in_run_folder=True)
        code, output, _ = self.run_main(a, b)
        self.assertEqual(0, code)
        self.assertEqual('identical: 6 records\n', output)
        self.assertEqual(compare_traces.digest(a), compare_traces.digest(b))
        self.assertEqual(6, compare_traces.digest(a)[0])

    def test_one_changed_field_is_reported_with_its_index_path_and_both_sides(self):
        changed = journey('run-b')
        changed[5]['fields']['hp'] = 179
        code, output, _ = self.run_main(self.write('a', journey('run-a')), self.write('b', changed))
        self.assertEqual(1, code)
        lines = output.splitlines()
        self.assertEqual('different: first at record 4 (counted from 0, after the run context record)', lines[0])
        self.assertEqual('fields: fields.hp', lines[1])
        self.assertIn('"hp":180', lines[2])
        self.assertIn('"hp":179', lines[3])
        self.assertEqual('records: A 6, B 6', lines[4])

    def test_a_changed_virtual_time_is_a_difference(self):
        changed = journey('run-b')
        changed[4]['vt'] = '00:00:04.001'
        code, output, _ = self.run_main(self.write('a', journey('run-a')), self.write('b', changed))
        self.assertEqual(1, code)
        self.assertIn('fields: vt', output)

    def test_a_missing_record_is_reported_where_the_traces_part_and_both_counts_are_printed(self):
        shorter = journey('run-b')
        del shorter[4]
        code, output, _ = self.run_main(self.write('a', journey('run-a')), self.write('b', shorter))
        self.assertEqual(1, code)
        lines = output.splitlines()
        self.assertTrue(lines[0].startswith('different: first at record 3 '))
        self.assertIn('"packet":"CM_CASTSPELL"', lines[2])
        self.assertIn('"packet":"combat-decision"', lines[3])
        self.assertEqual('records: A 6, B 5', lines[-1])

    def test_a_trace_that_stops_early_is_different_at_its_end(self):
        code, output, _ = self.run_main(self.write('a', journey('run-a')), self.write('b', journey('run-b')[:-1]))
        self.assertEqual(1, code)
        lines = output.splitlines()
        self.assertTrue(lines[0].startswith('different: first at record 5 '))
        self.assertEqual('B: <end of trace>', lines[2])
        self.assertEqual('records: A 6, B 5', lines[3])

    def test_two_swapped_records_are_different_at_the_first_of_them(self):
        swapped = journey('run-b')
        swapped[3], swapped[4] = swapped[4], swapped[3]
        # The same records in another order: only the order differs, so each keeps its own vt.
        code, output, _ = self.run_main(self.write('a', journey('run-a')), self.write('b', swapped))
        self.assertEqual(1, code)
        lines = output.splitlines()
        self.assertTrue(lines[0].startswith('different: first at record 2 '))
        self.assertIn('"packet":"pull-plan"', lines[2])
        self.assertIn('"packet":"CM_CASTSPELL"', lines[3])
        self.assertEqual('records: A 6, B 6', lines[-1])
        self.assertNotEqual(compare_traces.digest(self.write('c', journey('run-a')))[1], compare_traces.digest(self.write('d', swapped))[1])

    def test_an_ignored_path_is_left_out_everywhere_or_for_one_packet(self):
        changed = journey('run-b')
        changed[6]['fields']['kinahAfter'] = 601
        changed[3]['fields']['observedState']['targets'][0]['ObjectId'] = 7
        a, b = self.write('a', journey('run-a')), self.write('b', changed)
        self.assertEqual(1, self.run_main(a, b)[0])
        self.assertEqual(1, self.run_main(a, b, '--ignore', 'fields.kinahAfter')[0])
        self.assertEqual(1, self.run_main(a, b, '--ignore', 'combat-decision:fields.kinahAfter',
                                          '--ignore', 'fields.observedState.targets.ObjectId')[0])
        code, output, _ = self.run_main(a, b, '--ignore', 'service-trade:fields.kinahAfter',
                                        '--ignore', 'fields.observedState.targets.ObjectId')
        self.assertEqual(0, code)
        self.assertEqual('identical: 6 records\n', output)

    def test_a_missing_trace_and_a_folder_with_two_traces_are_errors_not_differences(self):
        a = self.write('a', journey('run-a'))
        code, _, errors = self.run_main(a, str(self.root / 'absent'))
        self.assertEqual(2, code)
        self.assertIn('expected one bot trace, found 0', errors)
        self.write('two', journey('run-a'), in_run_folder=True)
        (self.root / 'two' / 'b02.trace.jsonl').write_text('', encoding='utf-8')
        self.assertEqual(2, self.run_main(a, str(self.root / 'two'))[0])

    def test_counts_name_every_kind_of_step_a_trace_holds(self):
        used = dict(itemId=164000067, before=30, after=29, hp=100, useDelayId=32, refused='STR_USE_ITEM')
        trace = [
            record(0, 'natural-run-context', step='ni08-run', context={'Run': 'run-a'}),
            record(1, 'SM_DIE', '<'),
            record(2, 'SM_DIE', '>'),  # only a received SM_DIE is a death
            record(3, 'accept-client-death-and-revive-at-bound-obelisk', step='ni07-bind-revive-1'),
            record(4, 'accept-client-death-and-revive-inside-the-instance', step='instance-revive-2'),
            record(5, 'quest-instance-self-revival', revives=3),
            record(6, 'rest-after-revive', revives=1),  # a rest, not a revive step
            record(7, 'combat-decision', action='retreat', inEmergency=True),
            record(8, 'combat-decision', action='cast-self', inEmergency=True),
            record(9, 'combat-decision', action='cast-target', inEmergency=False),
            record(10, 'combat-retreat-route'),
            record(11, 'combat-retreat-cornered'),
            record(12, 'rest-relocate'),
            record(13, 'rest-relocate-none'),
            record(14, 'rest-interrupted-by-attack'),
            record(15, 'powder-rest-interrupted'),
            record(16, 'between-fights-heal', skillId=1838),
            record(17, 'powder-rest-decision', action='done', skillId=None),
            record(18, 'powder-rest-decision', action='cast', skillId=2271),
            record(19, 'service-trade', vendor=1, sold=18, bought=[]),
            record(20, 'service-trade', vendor=2, sold=0, bought=['162000053x12']),
            record(21, 'inventory-maintenance', sold=2, elixirsBought=0),
            record(22, 'inventory-maintenance', sold=0, elixirsBought=7),
            record(23, 'coin-purchase-receipt', receipt={'ItemId': 111501065}),
            record(24, 'pull-plan', chosenAction='pull'),
            record(25, 'pull-plan', chosenAction='no-plan'),
            record(26, 'pull-wait-for-patrol', patience=0),
            record(27, 'patrol-decision', action='wait'),
            record(28, 'patrol-decision', action='fight'),
            record(29, 'campaign-zone-patrol-wait', milliseconds=15000),
            record(30, 'adds-that-would-join', purpose='kill-210418-at-target'),
            record(31, 'adds-that-would-join', purpose='kill-210418'),
            record(32, 'combat-hot-potion', itemId=162000002),
            record(33, 'combat-hot-potion', itemId=162000006),
            record(34, 'combat-mana-potion', itemId=162000007),
            record(35, 'help-item-used', family='anti-shock', **used),
            record(36, 'help-item-used', family='anti-shock', **{**used, 'after': 30}),  # refused: nothing consumed
            record(37, 'help-item-used', family='awakening', **used),
            record(38, 'help-item-used', family='courage', **used),
            record(39, 'help-item-used', family='running', **used),
            record(40, 'help-item-used', family='dp-jelly', **used),
            record(41, 'help-item-supplied', itemId=164000067, count=30),
            record(42, 'help-item-supplied', itemId=162000006, count=30),
            record(43, 'service-bind', outcome='ready'),
            record(44, 'service-bind-result', outcome='done'),
            record(45, 'service-bind-result', outcome='refused'),
            record(46, 'service-soul-heal', outcome='done'),
            record(47, 'service-soul-heal', outcome='refused'),
            record(48, 'rest-life-potion', itemId=162000006),
            record(49, 'rest-sit-for-health', hp=40),
            record(50, 'rest-sit-for-health', hp=55),
        ]
        path = self.write('counts', trace)
        expected = {
            'records': 51, 'deaths': 1, 'reviveSteps': 3, 'retreats': 1, 'retreatRoutes': 2, 'restSitsTraced': 2,
            'restInterrupts': 2, 'betweenFightHeals': 1, 'powderRestCasts': 1, 'vendorBuys': 3, 'pullPlans': 2,
            'patrolWaits': 3, 'emergencyDecisions': 2, 'atTargetPulls': 1, 'lifePotions': 2, 'restLifePotions': 1,
            'restSitsForHealth': 2, 'manaPotions': 1,
            'shieldScrolls': 1, 'speedScrolls': 2, 'runningScrolls': 1, 'helpItemsSupplied': 2, 'binds': 1, 'soulHeals': 1,
        }
        self.assertEqual(expected, compare_traces.counts(path))
        self.assertEqual([name for name, _ in compare_traces.COUNTS], list(expected))
        code, output, _ = self.run_main('--counts', path, '--json')
        self.assertEqual((0, expected), (code, json.loads(output)))
        code, output, _ = self.run_main('--counts', path)
        self.assertEqual(0, code)
        self.assertEqual([f'{value:8} {name}' for name, value in expected.items()], output.splitlines())

    def test_digest_prints_the_record_count_and_hash_of_the_normalized_trace(self):
        path = self.write('a', journey('run-a'))
        records, sha = compare_traces.digest(path)
        code, output, _ = self.run_main('--digest', path, '--json')
        self.assertEqual((0, {'records': 6, 'sha256': sha}), (code, json.loads(output)))
        self.assertEqual(64, len(sha))
        self.assertNotEqual(sha, compare_traces.digest(path, compare_traces.parse_ignore(['fields.hp']))[1])


if __name__ == '__main__':
    unittest.main()
