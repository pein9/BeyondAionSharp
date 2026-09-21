#!/usr/bin/env python3
import gzip
import importlib.util
import json
from pathlib import Path
import unittest

import flake_policy as policy

spec = importlib.util.spec_from_file_location("report_test_helpers", Path(__file__).with_name("test-run-report.py"))
helpers = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helpers)
STAMP = "2026-09-20T12:00:00+00:00"


class FlakePolicyTests(unittest.TestCase):
    def setUp(self):
        self.helper = helpers.RunReportTests()
        self.helper.setUp()
        self.addCleanup(self.helper.doCleanups)
        self.root = self.helper.root
        self.ledger = json.loads((Path(__file__).resolve().parents[2] / "parity-artifacts/e2e/flaky.json").read_text())
        # Existing real entries must never become fixtures or be modified by tests.
        self.ledger.update(fullRuns=[], quarantines=[])

    def attempt(self, number, status="passed", **runner_changes):
        name = "full-l0" + ("-retry1" if number == 2 else "")
        root = self.root / name
        self.helper.fixture("LIVE", ["L0"], status, name, root)
        self.helper.journal(self.helper.scenario("L0", status, "LIVE", name), root)
        self.helper.watcher(root=root, run=name)
        if runner_changes:
            runner = helpers.reporter.read_json(root / "runner-result.json")
            runner.update(runner_changes)
            self.helper.put("runner-result.json", runner, root)
        path = root / "bots/a.trace.jsonl"
        path.parent.mkdir()
        path.write_text(json.dumps(dict(run=name, dir="action", action="test")) + "\n")
        return root

    def observe(self, count=2):
        return policy.observe(self.root, "full-l0", "L0", count, helpers.reporter.build_report)

    def flake(self):
        self.attempt(1, "failed")
        self.attempt(2)
        return self.observe()

    def record(self, ledger, number, observations=None, **changes):
        args = dict(run=f"full-{number}", suite="Breadth", finished_utc=f"2026-09-20T12:{number:02}:00+00:00",
                    report_sha256=f"{number:064x}", observations=observations or [])
        args.update(changes)
        return policy.record_full(ledger, **args)

    def test_actual_report_builder_classifies_failure_then_pass_and_retains_both_traces(self):
        observed = self.flake()
        self.assertEqual("flaky", observed["status"])
        self.assertEqual(["failed", "passed"], [a["status"] for a in observed["attempts"]])
        self.assertEqual(["full-l0", "full-l0-retry1"], [a["child"] for a in observed["attempts"]])
        self.assertTrue(all(a["traces"][0]["sha256"] and len(a["evidence"]) == 2 for a in observed["attempts"]))
        self.assertEqual("failed", helpers.reporter.build_report(self.root / "full-l0")["status"])
        self.assertFalse((self.root / "full-l0/report.json").exists())

    def test_one_pass_and_one_failure_keep_their_verdicts(self):
        self.attempt(1)
        self.assertEqual("passed", self.observe(1)["status"])
        runner = self.root / "full-l0/runner-result.json"
        data = json.loads(runner.read_text())
        data.update(status="failed", error="cleanup failed")
        runner.write_text(json.dumps(data))
        self.assertEqual("failed", self.observe(1)["status"])

    def test_two_failures_stay_failed_and_keep_both_causes(self):
        self.attempt(1, "failed")
        self.attempt(2, "failed")
        observed = self.observe()
        self.assertEqual("failed", observed["status"])
        self.assertTrue(all(a["evidenceIssues"] for a in observed["attempts"]))

    def test_clean_scenario_with_failed_watcher_is_a_failed_attempt(self):
        root = self.attempt(1)
        self.attempt(2)
        summary = helpers.reporter.read_json(root / "logwatch-summary.json")
        summary["failed"] = True
        self.helper.put("logwatch-summary.json", summary, root)
        self.assertEqual("flaky", self.observe()["status"])

    def test_passing_attempt_cannot_be_retried(self):
        self.attempt(1)
        self.attempt(2)
        with self.assertRaisesRegex(ValueError, "passing LIVE"):
            self.observe()

    def test_seed_revision_mode_scenario_and_run_must_match(self):
        self.attempt(1, "failed")
        root = self.attempt(2)
        original = helpers.reporter.read_json(root / "runner-result.json")
        for changes in (dict(seed=2), dict(gitSha="other"), dict(mode="SIM"), dict(run="foreign"), dict(scenarios=["Q1"])):
            with self.subTest(changes=changes):
                self.helper.put("runner-result.json", dict(original, **changes), root)
                with self.assertRaises(ValueError):
                    self.observe()

    def test_missing_trace_or_original_journal_cannot_establish_flake(self):
        self.flake()
        path = self.root / "full-l0/bots/a.trace.jsonl"
        original = path.read_bytes()
        path.unlink()
        with self.assertRaisesRegex(ValueError, "Missing retained"):
            self.observe()
        path.write_bytes(original)
        (self.root / "full-l0/scenario-results.jsonl").unlink()
        with self.assertRaises(OSError):
            self.observe()

    def test_empty_foreign_and_corrupt_trace_rejected(self):
        self.flake()
        path = self.root / "full-l0/bots/a.trace.jsonl"
        for content in ("", '{"run":"other"}\n', '{"partial":'):
            with self.subTest(content=content):
                path.write_text(content)
                with self.assertRaises(ValueError):
                    self.observe()

    def test_compressed_traces_supported_but_duplicate_copies_rejected(self):
        self.flake()
        path = self.root / "full-l0/bots/a.trace.jsonl"
        path.with_suffix(".jsonl.gz").write_bytes(gzip.compress(path.read_bytes()))
        with self.assertRaisesRegex(ValueError, "Duplicate compressed"):
            self.observe()
        path.unlink()
        self.assertEqual("flaky", self.observe()["status"])

    def test_stale_cached_report_is_rejected(self):
        self.flake()
        def stale(directory):
            result = helpers.reporter.build_report(directory)
            result["runnerSourceSha256"] = "0" * 64
            return result
        with self.assertRaisesRegex(ValueError, "stale"):
            policy.observe(self.root, "full-l0", "L0", 2, stale)

    def test_invalid_attempt_count_and_path_identity(self):
        for count in (0, 3, True, "2"):
            with self.assertRaises(ValueError):
                self.observe(count)
        with self.assertRaises(ValueError):
            policy.observe(self.root, "../foreign", "L0", 1, helpers.reporter.build_report)

    def test_third_flake_in_ten_full_runs_quarantines_with_owner_and_expiry(self):
        flake = self.flake()
        ledger = self.ledger
        for number in range(1, 11):
            ledger = self.record(ledger, number, [flake] if number in (1, 5, 10) else [])
            self.assertEqual(number == 10, bool(ledger["quarantines"]))
        quarantine = ledger["quarantines"][0]
        self.assertEqual(["full-1", "full-5", "full-10"], quarantine["triggerRuns"])
        self.assertEqual(self.ledger["owner"], quarantine["owner"])
        self.assertEqual("2026-10-04T12:10:00+00:00", quarantine["expiresUtc"])
        self.assertEqual([], self.ledger["fullRuns"])
        self.assertFalse(policy.admission(ledger, "L0", STAMP)["allowed"])
        self.assertTrue(policy.admission(ledger, "Q1", STAMP)["allowed"])

    def test_eleven_run_boundary_drops_old_flake_without_dropping_history(self):
        flake = self.flake()
        ledger = self.ledger
        for number in range(1, 12):
            ledger = self.record(ledger, number, [flake] if number in (1, 5, 11) else [])
        self.assertEqual(11, len(ledger["fullRuns"]))
        self.assertEqual([], ledger["quarantines"])
        ledger = self.record(ledger, 12, [flake])
        self.assertEqual(["full-5", "full-11", "full-12"], ledger["quarantines"][0]["triggerRuns"])

    def test_expired_quarantine_never_silently_releases(self):
        flake = self.flake()
        ledger = self.ledger
        for number in range(1, 4):
            ledger = self.record(ledger, number, [flake])
        result = policy.admission(ledger, "L0", "2026-11-01T00:00:00Z")
        self.assertFalse(result["allowed"])
        self.assertTrue(result["expired"])

    def test_history_replay_idempotent_but_changed_receipts_cannot_overwrite(self):
        ledger = self.record(self.ledger, 1)
        self.assertEqual(ledger, self.record(ledger, 1))
        with self.assertRaisesRegex(ValueError, "rewritten"):
            self.record(ledger, 1, report_sha256="f" * 64)

    def test_sim_diagnostics_unknown_status_and_duplicate_scenarios_rejected(self):
        for observation in (dict(mode="SIM", scenario="L0", status="failed"),
                            dict(mode="LIVE", scenario="L0", status="unknown")):
            with self.assertRaises(ValueError):
                self.record(self.ledger, 1, [observation])
        with self.assertRaises(ValueError):
            self.record(self.ledger, 1, suite="diagnostic")
        with self.assertRaisesRegex(ValueError, "once"):
            self.record(self.ledger, 1, [dict(mode="LIVE", scenario="L0", status="passed")] * 2)

    def test_policy_drift_missing_owner_and_expiry_rejected(self):
        for change in (dict(window=9), dict(threshold=2), dict(owner=""), dict(expiryDays=0), dict(expiryDays=365)):
            with self.assertRaises(ValueError):
                policy.validate(dict(self.ledger, **change))
        flake = self.flake()
        ledger = self.record(self.ledger, 1, [flake])
        ledger["fullRuns"][0]["scenarios"][0]["expiresUtc"] = STAMP
        with self.assertRaises(ValueError):
            policy.validate(ledger)

    def test_order_and_time_zone_required(self):
        ledger = self.record(self.ledger, 2)
        with self.assertRaises(ValueError):
            self.record(ledger, 1)
        with self.assertRaises(ValueError):
            self.record(self.ledger, 1, finished_utc="2026-09-20T12:00:00")


if __name__ == "__main__":
    unittest.main()
