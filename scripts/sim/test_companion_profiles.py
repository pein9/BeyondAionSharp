"""Companion export and offline checks must apply Java's migration without flattening profession ranks."""
import contextlib
import gzip
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location('companion_profiles', Path(__file__).with_name('make-companion-profiles.py'))
PROFILES = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(PROFILES)


class CompanionProfilesTests(unittest.TestCase):
    def test_export_migrates_old_grades_and_keeps_profession_ranks(self):
        saved = [(1, 9), (29999, 5), (30000, 49), (30001, 99), (30002, 399), (30003, 499),
                 (40001, 399), (40008, 99), (40009, 399), (40010, 199)]
        expected = [(1, 1), (29999, 1), *saved[2:8], (40009, 1), (40010, 199)]
        tables = {
            'players': [{'id': 7, 'race': 'ASMODIANS', 'player_class': 'CLERIC', 'gender': 'FEMALE', 'name': 'Cleric'}],
            'player_appearance': [{'player_id': 7, 'height': 1.0}],
            'inventory': [], 'item_stones': [],
            'player_skills': [{'player_id': 7, 'skill_id': key, 'skill_level': level} for key, level in saved]
                             + [{'player_id': 8, 'skill_id': 42, 'skill_level': 8}],
        }
        with tempfile.TemporaryDirectory() as root:
            source = Path(root, 'run', 'snapshots', 'old-w1')
            source.mkdir(parents=True)
            dump = gzip.compress(b'old snapshot')
            (source / 'dump.sql.gz').write_bytes(dump)
            record = {'name': 'old', 'dumpSha256': hashlib.sha256(dump).hexdigest(),
                      'characters': [{'characterId': 7, 'level': 26}]}
            (source / 'snapshot.json').write_text(json.dumps(record), encoding='utf-8')
            with patch.object(PROFILES, 'REPO', root), patch.object(PROFILES, 'rows', side_effect=lambda _, table: tables[table]):
                profile = PROFILES.generate('old', {})['asmodians-cleric-26']

            self.assertEqual(expected, [(skill['id'], skill['level']) for skill in profile['skills']])
            self.assertEqual(record['dumpSha256'], profile['source']['dumpSha256'])
            self.assertEqual(dump, (source / 'dump.sql.gz').read_bytes())
            self.assertEqual(saved, [(row['skill_id'], row['skill_level']) for row in tables['player_skills'][:-1]])

    def test_check_without_snapshots_rejects_legacy_combat_and_morph_levels(self):
        for skill_id, level in [(1, 2), (29999, 9), (40009, 399), (1001, 0)]:
            with self.subTest(skill_id=skill_id, level=level):
                result, output = self.check_without_snapshots([{'id': skill_id, 'level': level}])
                self.assertEqual(1, result)
                self.assertIn('Java requires level 1', output)

    def test_check_without_snapshots_accepts_combat_morph_and_profession_levels(self):
        skills = [{'id': key, 'level': level} for key, level in
                  [(1001, 1), (29999, 1), (30000, 49), (30001, 99), (30002, 399),
                   (30003, 499), (40001, 399), (40008, 99), (40009, 1), (40010, 199)]]
        result, output = self.check_without_snapshots(skills)
        self.assertEqual(0, result, output)

    def check_without_snapshots(self, skills):
        with tempfile.TemporaryDirectory() as root:
            folder = Path(root, 'profiles')
            folder.mkdir()
            (folder / 'cleric.json').write_text(json.dumps({'playerClass': 'CLERIC', 'skills': skills,
                                                           'equipment': [], 'inventory': []}), encoding='utf-8')
            output = io.StringIO()
            with patch.object(PROFILES, 'REPO', root), patch.object(PROFILES, 'item_data', return_value={}), \
                    patch('sys.argv', ['make-companion-profiles.py', '--check', '--out', str(folder)]), \
                    contextlib.redirect_stdout(output):
                result = PROFILES.main()
            self.assertIn('snapshots are not here', output.getvalue())
            return result, output.getvalue()


if __name__ == '__main__':
    unittest.main()
