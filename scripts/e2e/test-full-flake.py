#!/usr/bin/env python3
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import unittest
from unittest.mock import patch

import flake_policy as policy


def module(name, file):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(file))
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


helpers = module("full_flake_helpers", "test-flake-policy.py")
history = module("full_flake_history", "flake-history.py")
quest = module("full_flake_quest", "report-quest-coverage.py")
packets = module("full_flake_packet_gate", "report-packet-coverage.py")
reporter = history.reporter
STAMP = helpers.STAMP


class FullFlakeTests(unittest.TestCase):
    def setUp(self):
        self.fixture = helpers.FlakePolicyTests()
        self.fixture.setUp()
        self.addCleanup(self.fixture.doCleanups)
        self.root = self.fixture.root
        self.put = self.fixture.helper.put
        self.step = dict(id="live-l0", kind="Live", scenario="L0")
        self.ledger = self.root / "ledger.json"
        self.ledger.write_text(json.dumps(self.fixture.ledger))

    def suite(self, statuses=("failed", "passed"), fingerprints=False):
        for number, status in enumerate(statuses, 1):
            child = self.fixture.attempt(number, status)
            if fingerprints and number == 1:
                self.fixture.helper.watcher(("NEW",), root=child, run=child.name)
        self.fixture.helper.fixture("FULL", run="full")
        self.put("flaky-at-start.json", self.fixture.ledger)
        self.plan = dict(run="full", suite="Breadth", seed=1, gitSha="contract", retryPolicyVersion=1,
            flakeLedgerSha256=history.sha(self.root / "flaky-at-start.json"), steps=[self.step])
        self.put("suite-plan.json", self.plan)
        self.put("live-admission/live-l0.json", dict(allowed=True, quarantine=None, step="live-l0", scenario="L0",
            checkedUtc=STAMP, ledgerSha256=self.plan["flakeLedgerSha256"]))
        status = "flaky" if statuses == ("failed", "passed") else statuses[-1]
        self.receipt = dict(mode="LIVE", run="full-l0", status=status, retryBlockedError=None,
            attempts=[dict(number=n, run="full-l0" + ("-retry1" if n == 2 else ""), status=s,
                startedUtc=STAMP, durationSeconds=4.5, error="failure" if s == "failed" else None)
                for n, s in enumerate(statuses, 1)])
        self.put("live-attempts/live-l0.json", self.receipt)
        (self.root / "live-attempts/live-l0.jsonl").write_text("".join(json.dumps(a) + "\n" for a in self.receipt["attempts"]))
        if len(statuses) == 2:
            self.put("live-attempts/live-l0.retry-admission.json", dict(priorRun="full-l0", retryRun="full-l0-retry1",
                passed=True, checkedUtc=STAMP, containers=[]))
        self.result = dict(id="live-l0", kind="Live", status=status, durationSeconds=9, error=None,
            attemptReceipt="live-attempts/live-l0.json", attemptReceiptSha256=history.sha(self.root / "live-attempts/live-l0.json"))
        self.results()

    def results(self):
        (self.root / "suite-results.jsonl").write_text(json.dumps(self.result) + "\n")

    def retain_report(self):
        result = reporter.build_report(self.root)
        self.put("report.json", result)
        return result

    def cli(self, action, *extra):
        return subprocess.run([sys.executable, str(Path(history.__file__)), action, "--root", str(self.root),
            "--ledger", str(self.ledger), *extra], capture_output=True, text=True)

    def test_full_flake_keeps_two_attempts_fingerprints_and_trace_links_but_not_clean_pass(self):
        self.suite(fingerprints=True)
        result = reporter.build_report(self.root)
        self.assertEqual("failed", result["status"])
        self.assertEqual(dict(passed=0, failed=0, skipped=0, flaky=1), result["counts"])
        self.assertEqual("flaky", result["scenarios"][0]["status"])
        self.assertEqual("full-l0", result["scenarios"][0]["child"])
        self.assertEqual("full-l0-retry1", result["scenarios"][0]["selectedChild"])
        self.assertEqual("NEW", result["fingerprints"][0]["disposition"])
        self.assertEqual("full-l0", result["fingerprints"][0]["child"])
        rendered = reporter.markdown(result)
        self.assertIn("full-l0/bots/a.trace.jsonl", rendered)
        self.assertIn("full-l0-retry1/bots/a.trace.jsonl", rendered)

    def test_first_pass_stays_clean_and_selects_original_child(self):
        self.suite(("passed",))
        self.assertEqual("passed", reporter.build_report(self.root)["status"])
        self.assertEqual("full-l0", policy.selected_child(self.root, self.plan, self.step, self.result, reporter.build_report))

    def test_double_failure_never_becomes_flaky(self):
        self.suite(("failed", "failed"))
        report = reporter.build_report(self.root)
        self.assertEqual(1, report["counts"]["failed"])
        self.assertEqual(0, report["counts"]["flaky"])
        with self.assertRaisesRegex(ValueError, "No accepted"):
            policy.selected_child(self.root, self.plan, self.step, self.result, reporter.build_report)

    def test_forged_receipt_or_raw_attempt_change_cannot_keep_flaky_verdict(self):
        self.suite()
        for change in (dict(status="passed"), dict(run="foreign")):
            self.put("live-attempts/live-l0.json", dict(self.receipt, **change))
            self.assertEqual(0, reporter.build_report(self.root)["counts"]["flaky"])
        self.put("live-attempts/live-l0.json", self.receipt)
        (self.root / "full-l0/bots/a.trace.jsonl").unlink()
        self.assertEqual(0, reporter.build_report(self.root)["counts"]["flaky"])

    def test_missing_or_unsafe_cleanup_guard_blocks_recovered_credit(self):
        self.suite()
        path = self.root / "live-attempts/live-l0.retry-admission.json"
        for guard in (dict(priorRun="full-l0", retryRun="full-l0-retry1", checkedUtc=STAMP, passed=False, containers=[]),
                      dict(priorRun="full-l0", retryRun="full-l0-retry1", checkedUtc=STAMP, passed=True, containers=["still-running"])):
            self.put(str(path.relative_to(self.root)), guard)
            self.assertEqual(0, reporter.build_report(self.root)["counts"]["flaky"])
        path.unlink()
        self.assertEqual(0, reporter.build_report(self.root)["counts"]["flaky"])

    def test_missing_admission_or_changed_policy_rejects_execution(self):
        self.suite()
        (self.root / "live-admission/live-l0.json").unlink()
        self.assertEqual(0, reporter.build_report(self.root)["counts"]["flaky"])
        self.put("flaky-at-start.json", dict(self.fixture.ledger, threshold=2))
        self.assertNotEqual(0, self.cli("verify", "--step", "live-l0").returncode)

    def test_quarantine_cli_denies_before_execution_and_full_report_shows_skipped_not_passed(self):
        self.suite()
        flake = policy.observe(self.root, "full-l0", "L0", 2, reporter.build_report)
        ledger = self.fixture.ledger
        for number in range(1, 4):
            ledger = self.fixture.record(ledger, number, [flake])
        self.root = self.root / "quarantined"
        self.root.mkdir()
        self.fixture.helper.fixture("FULL", run="full", root=self.root, status="failed")
        self.put("flaky-at-start.json", ledger, self.root)
        self.plan["flakeLedgerSha256"] = history.sha(self.root / "flaky-at-start.json")
        self.put("suite-plan.json", self.plan, self.root)
        result = self.cli("admit", "--step", "live-l0")
        self.assertEqual(1, result.returncode, result.stderr)
        self.result = dict(id="live-l0", kind="Live", status="Failed", durationSeconds=0, error="quarantine admission denied")
        self.results()
        report = reporter.build_report(self.root)
        self.assertEqual("failed", report["status"])
        self.assertEqual(1, report["counts"]["skipped"])
        self.assertEqual(0, report["counts"]["passed"])
        self.assertIn("Quarantined", report["scenarios"][0]["reason"])
        self.assertEqual("e2e-simulation", report["scenarios"][0]["quarantine"]["owner"])
        self.assertFalse((self.root / "full-l0").exists())

    def test_missing_original_attempt_journal_cannot_establish_flake(self):
        self.suite()
        (self.root / "live-attempts/live-l0.jsonl").unlink()
        self.assertEqual(0, reporter.build_report(self.root)["counts"]["flaky"])

    def test_cli_verify_and_selection_use_raw_attempts(self):
        self.suite()
        result = self.cli("verify", "--step", "live-l0")
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("full-l0-retry1", self.cli("select", "--step", "live-l0").stdout.strip())
        (self.root / "full-l0-retry1/scenario-results.jsonl").unlink()
        self.assertNotEqual(0, self.cli("verify", "--step", "live-l0").returncode)

    def test_record_persists_actual_flake_once_and_keeps_failed_full_status(self):
        self.suite()
        before = self.retain_report()
        self.assertEqual("failed", before["status"])
        result = self.cli("record")
        self.assertEqual(0, result.returncode, result.stderr)
        first = self.ledger.read_bytes()
        self.assertEqual(0, self.cli("record").returncode)
        self.assertEqual(first, self.ledger.read_bytes())
        ledger = json.loads(first)
        self.assertEqual(1, len(ledger["fullRuns"]))
        row = ledger["fullRuns"][0]["scenarios"][0]
        self.assertEqual("flaky", row["status"])
        self.assertEqual(2, len(row["attempts"]))
        self.assertEqual("e2e-simulation", row["owner"])
        self.assertEqual(before, reporter.build_report(self.root))

    def test_stale_report_refuses_history_without_mutating_ledger(self):
        self.suite()
        report = self.retain_report()
        report["counts"]["passed"] = 99
        self.put("report.json", report)
        before = self.ledger.read_bytes()
        self.assertNotEqual(0, self.cli("record").returncode)
        self.assertEqual(before, self.ledger.read_bytes())

    def test_ledger_lock_conflict_is_fail_closed_and_atomic_replace_failure_preserves_original(self):
        self.suite()
        self.retain_report()
        before = self.ledger.read_bytes()
        with history.ledger_lock(self.ledger):
            result = self.cli("record")
            self.assertNotEqual(0, result.returncode)
        self.assertEqual(before, self.ledger.read_bytes())
        with patch.object(history.os, "replace", side_effect=OSError("injected replace failure")):
            with self.assertRaises(OSError):
                history.record(self.root, self.ledger)
        self.assertEqual(before, self.ledger.read_bytes())
        self.assertEqual([], list(self.root.glob(".flaky-*.tmp")))

    def test_history_failure_marker_prevents_green_report(self):
        self.suite(("passed",))
        self.assertEqual("passed", reporter.build_report(self.root)["status"])
        self.put("flake-history-error.json", dict(error="history not retained"))
        report = reporter.build_report(self.root)
        self.assertEqual("failed", report["status"])
        self.assertIn("history not retained", report["evidenceIssues"])

    def test_standalone_report_and_cli_history_never_advance_full_window(self):
        self.suite()
        self.plan["suite"] = "Standalone"
        self.put("suite-plan.json", self.plan)
        runner = reporter.read_json(self.root / "runner-result.json")
        runner["mode"] = "LIVE_RETRY"
        self.put("runner-result.json", runner)
        report = self.retain_report()
        self.assertEqual("LIVE_RETRY", report["mode"])
        self.assertEqual(1, report["counts"]["flaky"])
        result = self.cli("record")
        self.assertEqual(0, result.returncode, result.stderr)
        ledger = json.loads(self.ledger.read_text())
        self.assertEqual([], ledger["fullRuns"])
        self.assertEqual([], ledger["quarantines"])
        self.assertEqual("flaky", ledger["standaloneRuns"][0]["scenarios"][0]["status"])
        before = self.ledger.read_bytes()
        self.assertEqual(0, self.cli("record").returncode)
        self.assertEqual(before, self.ledger.read_bytes())
        self.plan["suite"] = "Breadth"
        self.put("suite-plan.json", self.plan)
        self.assertEqual("failed", reporter.build_report(self.root)["status"])
        self.assertNotEqual(0, self.cli("record").returncode)

    def test_standalone_flakes_do_not_trigger_or_expire_full_quarantine_window(self):
        self.suite()
        observation = policy.observe(self.root, "full-l0", "L0", 2, reporter.build_report)
        ledger = self.fixture.ledger
        for number in range(1, 13):
            ledger = policy.record_standalone(ledger, f"standalone-{number}", f"2026-09-20T12:{number:02}:00+00:00",
                f"{number:064x}", [observation])
        self.assertEqual(12, len(ledger["standaloneRuns"]))
        self.assertEqual([], ledger["fullRuns"])
        self.assertEqual([], ledger["quarantines"])
        self.assertTrue(policy.admission(ledger, "L0", STAMP)["allowed"])
        with self.assertRaisesRegex(ValueError, "both standalone and Full"):
            policy.record_full(ledger, "standalone-1", "Breadth", STAMP, "0" * 64, [])

    def test_quest_gate_selects_retry_without_duplicate_or_failed_receipts(self):
        self.suite()
        receipt = dict(schemaVersion=1, mode="LIVE", scenario="Q1", acceptedQuestIds=[1], completedQuestIds=[1])
        self.put("full-l0/quest-coverage/q1.json", dict(receipt, completedQuestIds=[2]))
        self.put("full-l0-retry1/quest-coverage/q1.json", receipt)
        result = quest.load_receipts(self.root, {1, 2})
        self.assertEqual([1], result["LIVE"]["Q1"]["completedQuestIds"])
        self.assertTrue(result["LIVE"]["Q1"]["source"].startswith("full-l0-retry1/"))

    def test_packet_gate_uses_only_accepted_retry_traffic(self):
        self.suite()
        for name, sent in (("full-l0", "CM_FIRST"), ("full-l0-retry1", "CM_RETRY")):
            catalog = dict(schemaVersion=1, run=name, mode="LIVE", protocol="aion-game-4.8",
                gameServerModule="00000000-0000-0000-0000-000000000001", botModule="00000000-0000-0000-0000-000000000002",
                client=[dict(name="CM_FIRST", opcode=1), dict(name="CM_RETRY", opcode=2)],
                server=[dict(name="SM_A", opcode=3, structuredDecoder=True)])
            self.put(f"{name}/packet-catalog.json", catalog)
            (self.root / name / "bots/a.trace.jsonl").write_text(json.dumps(dict(run=name, dir=">", packet=sent, fields={})) + "\n")
        # A minimal reviewed identity floor; comparison schema comes from the actual helper.
        measurement = reporter.build_report(self.root / "full-l0-retry1")["packetCoverage"][0]
        baseline = dict(schemaVersion=1, modes={})
        # Keep the comparison call injectable, but verify real selection, reports and raw packet parsing.
        with patch.object(packets, "compare", return_value=dict(errors=[], passed=True)) as comparison:
            result = packets.build(self.root, baseline)
        self.assertTrue(result["baselineComparison"]["passed"])
        self.assertEqual("full-l0-retry1", result["children"][0]["directory"])
        self.assertEqual(measurement["metrics"], comparison.call_args.args[0][0]["metrics"])
        self.assertEqual(["CM_RETRY"], comparison.call_args.args[0][0]["metrics"]["clientSent"]["packets"])


if __name__ == "__main__":
    unittest.main()
