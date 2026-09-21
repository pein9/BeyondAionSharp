#!/usr/bin/env python3
import copy
import gzip
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("report_run", Path(__file__).with_name("report-run.py"))
reporter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(reporter)
STAMP = "2026-09-20T12:00:00+00:00"


class RunReportTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="aion-report-test-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def put(self, name, value, root=None):
        path = (root or self.root) / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(value), encoding="utf-8")

    def journal(self, rows, root=None):
        path = (root or self.root) / "scenario-results.jsonl"
        path.write_text("".join(json.dumps(row) + "\n" for row in rows), encoding="utf-8")

    def scenario(self, scenario="Q1", status="passed", mode="SIM", run="run-a"):
        common = dict(schemaVersion=1, run=run, mode=mode, scenario=scenario, startedUtc=STAMP, timestampUtc=STAMP)
        return [dict(common, event="started", status="running", durationSeconds=None, exitCode=None, error=None),
                dict(common, event="completed", status=status, durationSeconds=1.25,
                     exitCode=0 if status == "passed" else 7, error=None if status == "passed" else "original exception")]

    def fixture(self, mode="SIM", scenarios=None, status="passed", run="run-a", root=None):
        scenarios = scenarios or ["Q1"]
        self.put("runner-result.json", dict(schemaVersion=1, run=run, mode=mode, scenarios=scenarios,
                 status=status, startedUtc=STAMP, finishedUtc=STAMP, durationSeconds=4.5, error=None if status == "passed" else "runner failure"), root)
        if mode != "FULL":
            self.journal([row for scenario in scenarios for row in self.scenario(scenario, mode=mode, run=run)], root)

    def watcher(self, dispositions=(), root=None, run="run-a"):
        root = root or self.root
        self.put("logwatch-summary.json", dict(run=run, mode="enforce", failed=bool(dispositions), servers=["gs"],
                 **{kind: sum(d.lower() == kind for d in dispositions) for kind in ("new", "known", "regressed")}), root)
        (root / "digest.log").write_text("".join(f"{STAMP} {d} ERROR gs fp={i:08x} | retained problem\n" for i, d in enumerate(dispositions)), encoding="utf-8")
        events = []
        for second, memory, timers in ((0, 100, 3), (10, 200, 2), (30, 150, 5)):
            events.append(dict(ts=STAMP.replace("12:00:00", f"12:00:{second:02}"), run=run, srv="gs",
                cat="Aion.Commons.Diagnostics.ServerHeartbeatService",
                msg=f"Server heartbeat: connections=2, packetQueueDepth=1, armedTimers={timers}, workingSetBytes={memory}, lastGcHeapBytes=90, lastGcIndex=1, dispatcherWrites=null"))
        path = root / "logs/gs/gs.events.jsonl"
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text("".join(json.dumps(row) + "\n" for row in events), encoding="utf-8")
        for i, disposition in enumerate(dispositions):
            if disposition != "NEW":
                continue
            bundle = root / f"problems/{i:08x}"
            self.put("metadata.json", dict(run=run, fingerprint=f"{i:08x}"), bundle)
            for name in ("stack.txt", "server-context.log", "bot-trace.jsonl", "draft-backlog.md"):
                (bundle / name).write_text("evidence\n", encoding="utf-8")

    def test_success_lists_all_scenarios_and_real_durations(self):
        self.fixture(scenarios=["Q1", "Q2"])
        result = reporter.build_report(self.root)
        self.assertEqual("passed", result["status"])
        self.assertEqual(2, result["counts"]["passed"])
        self.assertEqual([1.25, 1.25], [r["durationSeconds"] for r in result["scenarios"]])
        self.assertEqual([], result["heartbeatPeaks"])
        self.assertIn("unavailable", reporter.markdown(result))

    def test_failed_attempt_and_unreached_scenarios_are_not_credited(self):
        self.fixture(scenarios=["Q1", "Q2", "Q3"], status="failed")
        self.journal(self.scenario() + self.scenario("Q2", "failed"))
        result = reporter.build_report(self.root)
        self.assertEqual(["passed", "failed", "skipped"], [r["status"] for r in result["scenarios"]])
        self.assertIsNone(result["scenarios"][2]["durationSeconds"])
        self.assertIn("runner failure", result["evidenceIssues"])

    def test_cleanup_failure_does_not_erase_successful_scenario_or_turn_run_green(self):
        self.fixture(status="failed")
        result = reporter.build_report(self.root)
        self.assertEqual("failed", result["status"])
        self.assertEqual("passed", result["scenarios"][0]["status"])

    def test_started_without_terminal_is_failed_not_flaky_or_skipped(self):
        self.fixture(status="failed")
        self.journal(self.scenario()[:1])
        result = reporter.build_report(self.root)
        self.assertEqual("failed", result["scenarios"][0]["status"])
        self.assertIsNone(result["scenarios"][0]["durationSeconds"])

    def test_missing_prerequisite_or_journal_is_not_pass(self):
        for status in ("passed", "failed"):
            with self.subTest(status=status):
                self.fixture(status=status)
                (self.root / "scenario-results.jsonl").unlink()
                result = reporter.build_report(self.root)
                self.assertEqual("failed", result["status"])
                self.assertEqual("skipped", result["scenarios"][0]["status"])

    def test_bad_journal_inputs_fail_closed(self):
        original = self.scenario()
        mutations = [lambda r: r + r, lambda r: [r[1]], lambda r: [r[1], r[0]],
                     lambda r: [r[0], dict(r[1], run="foreign")],
                     lambda r: [r[0], dict(r[1], mode="LIVE")],
                     lambda r: [r[0], dict(r[1], startedUtc="2026-09-21T12:00:00+00:00")],
                     lambda r: [r[0], dict(r[1], durationSeconds=-1)],
                     lambda r: [r[0], dict(r[1], durationSeconds=float("nan"))],
                     lambda r: [r[0], dict(r[1], exitCode=7)],
                     lambda r: [r[0], dict(r[1], exitCode=0.0)],
                     lambda r: [r[0], dict(r[1], status="flaky")]]
        for mutate in mutations:
            with self.subTest(mutate=mutate):
                self.fixture()
                self.journal(mutate(copy.deepcopy(original)))
                result = reporter.build_report(self.root)
                self.assertEqual("failed", result["status"])
                self.assertTrue(result["evidenceIssues"])
                self.assertEqual(["Q1"], [r["id"] for r in result["scenarios"]])

    def test_invalid_or_missing_runner_result_still_yields_failed_report(self):
        self.assertEqual("failed", reporter.build_report(self.root)["status"])
        self.put("runner-result.json", {"status": "passed"})
        self.assertEqual("failed", reporter.build_report(self.root)["status"])

    def test_live_clean_watcher_and_observed_peaks(self):
        self.fixture("LIVE")
        self.watcher()
        result = reporter.build_report(self.root)
        self.assertEqual("passed", result["status"])
        peak = result["heartbeatPeaks"][0]
        self.assertEqual(200, peak["peakWorkingSetBytes"])
        self.assertEqual(5, peak["peakArmedTimers"])
        self.assertEqual(20, peak["maximumObservedGapSeconds"])
        self.assertEqual(3, peak["samples"])

    def test_fingerprints_use_recorded_classification_and_link_each_new_bundle(self):
        self.fixture("LIVE")
        self.watcher(("NEW", "KNOWN", "REGRESSED"))
        self.put("unrelated-current-ledger.json", {"00000000": "fixed"})
        result = reporter.build_report(self.root)
        self.assertEqual("failed", result["status"])
        self.assertEqual(["NEW", "KNOWN", "REGRESSED"], [r["disposition"] for r in result["fingerprints"]])
        self.assertIn("problems/00000000/metadata.json", reporter.markdown(result))
        (self.root / "problems/00000000/stack.txt").unlink()
        self.assertTrue(any("missing repro" in i for i in reporter.build_report(self.root)["evidenceIssues"]))

    def test_known_classification_preserves_watchers_existing_verdict(self):
        for watcher_failed in (False, True):
            with self.subTest(watcher_failed=watcher_failed):
                self.fixture("LIVE")
                self.watcher(("KNOWN",))
                summary = reporter.read_json(self.root / "logwatch-summary.json")
                # Ordinary tracked logs are nonfatal; known heartbeat and declared
                # server-fault-window problems remain fatal in the real watcher.
                summary["failed"] = watcher_failed
                self.put("logwatch-summary.json", summary)
                result = reporter.build_report(self.root)
                self.assertEqual("failed" if watcher_failed else "passed", result["status"])
                self.assertEqual("KNOWN", result["fingerprints"][0]["disposition"])
                self.assertIsNone(result["fingerprints"][0]["repro"])

    def test_new_or_regressed_cannot_be_hidden_by_inconsistent_clean_summary(self):
        for disposition in ("NEW", "REGRESSED"):
            with self.subTest(disposition=disposition):
                self.fixture("LIVE")
                self.watcher((disposition,))
                summary = reporter.read_json(self.root / "logwatch-summary.json")
                summary["failed"] = False
                self.put("logwatch-summary.json", summary)
                result = reporter.build_report(self.root)
                self.assertEqual("failed", result["status"])

    def test_watcher_verdict_requires_a_boolean_not_a_falsey_placeholder(self):
        for value in (None, 0, "", [], {}, "false"):
            with self.subTest(value=value):
                self.fixture("LIVE")
                self.watcher(("KNOWN",))
                summary = reporter.read_json(self.root / "logwatch-summary.json")
                summary["failed"] = value
                self.put("logwatch-summary.json", summary)
                result = reporter.build_report(self.root)
                self.assertEqual("failed", result["status"])
                self.assertTrue(any("boolean" in issue for issue in result["evidenceIssues"]))

    def test_watcher_count_identity_mode_and_heartbeat_inconsistencies_fail(self):
        for mutation in ({"run": "foreign"}, {"mode": "record"}, {"new": 1}, {"servers": []}):
            with self.subTest(mutation=mutation):
                self.fixture("LIVE")
                self.watcher()
                path = self.root / "logwatch-summary.json"
                data = reporter.read_json(path)
                data.update(mutation)
                self.put("logwatch-summary.json", data)
                self.assertEqual("failed", reporter.build_report(self.root)["status"])
        self.watcher()
        (self.root / "logs/gs/gs.events.jsonl").write_text('{"partial":', encoding="utf-8")
        self.assertEqual("failed", reporter.build_report(self.root)["status"])

    def test_truncated_compressed_heartbeat_does_not_crash_report_generation(self):
        self.fixture("LIVE")
        self.watcher()
        path = self.root / "logs/gs/gs.events.jsonl"
        path.with_suffix(".jsonl.gz").write_bytes(gzip.compress(path.read_bytes())[:-4])
        path.unlink()
        self.assertEqual("failed", reporter.build_report(self.root)["status"])

    def test_numeric_overflow_in_retained_comparison_fails_without_nonfinite_output(self):
        self.fixture()
        (self.root / "coverage.json").write_text('{"delta":1e9999}', encoding="utf-8")
        result = reporter.build_report(self.root)
        self.assertEqual("failed", result["status"])
        json.dumps(result, allow_nan=False)

    def test_failed_live_scenario_cannot_be_hidden_by_clean_watcher(self):
        self.fixture("LIVE", status="failed")
        self.journal(self.scenario(status="failed", mode="LIVE"))
        self.watcher()
        result = reporter.build_report(self.root)
        self.assertFalse(result["watcher"]["failed"])
        self.assertEqual("failed", result["status"])
        self.assertEqual(1, result["counts"]["failed"])

    def test_retained_coverage_comparison_is_not_recomputed_from_current_baseline(self):
        self.fixture()
        comparison = {"baselineComparison": {"passed": False, "missing": [123]}}
        self.put("coverage.json", comparison)
        result = reporter.build_report(self.root)
        self.assertEqual(comparison, result["coverage"][0]["comparison"])
        self.assertEqual("failed", result["status"])

    def suite(self, complete):
        self.fixture("FULL")
        steps = [dict(id="sim-a", kind="Sim", scenarios=["Q1", "Q2"]),
                 dict(id="live-l0", kind="Live", scenario="L0"), dict(id="quest-coverage", kind="QuestCoverage")]
        self.put("suite-plan.json", dict(run="run-a", steps=steps))
        self.fixture(root=self.root / "sim-a", scenarios=["Q1", "Q2"])
        results = [dict(id="sim-a", kind="Sim", status="Passed", durationSeconds=4, error=None)]
        if complete:
            self.put("quest-coverage.json", {"baselineComparison": {"passed": True}})
            self.fixture("LIVE", ["L0"], run="run-a-l0", root=self.root / "run-a-l0")
            self.watcher(root=self.root / "run-a-l0", run="run-a-l0")
            results += [dict(id=s["id"], kind=s["kind"], status="Passed", durationSeconds=4, error=None) for s in steps[1:]]
        (self.root / "suite-results.jsonl").write_text("".join(json.dumps(row) + "\n" for row in results), encoding="utf-8")

    def test_full_success_joins_child_evidence_and_does_not_require_stale_child_reports(self):
        self.suite(True)
        self.put("report.json", {"status": "failed"}, self.root / "sim-a")
        result = reporter.build_report(self.root)
        self.assertEqual("passed", result["status"])
        self.assertEqual(3, result["counts"]["passed"])

    def test_full_fail_fast_exposes_unexecuted_children_and_gates(self):
        self.suite(False)
        result = reporter.build_report(self.root)
        self.assertEqual("failed", result["status"])
        self.assertEqual(["passed", "passed", "skipped"], [r["status"] for r in result["scenarios"]])
        self.assertEqual("skipped", result["steps"][-1]["status"])

    def test_full_preserves_known_classification_and_child_watchers_verdict(self):
        for failed in (False, True):
            with self.subTest(failed=failed):
                self.suite(True)
                child = self.root / "run-a-l0"
                self.watcher(("KNOWN",), root=child, run="run-a-l0")
                summary = reporter.read_json(child / "logwatch-summary.json")
                summary["failed"] = failed
                self.put("logwatch-summary.json", summary, child)
                result = reporter.build_report(self.root)
                self.assertEqual("failed" if failed else "passed", result["status"])
                self.assertEqual("KNOWN", result["fingerprints"][0]["disposition"])

    def test_full_pass_without_child_journal_fails(self):
        self.suite(True)
        (self.root / "sim-a/scenario-results.jsonl").unlink()
        result = reporter.build_report(self.root)
        self.assertEqual("failed", result["status"])

    def test_full_cannot_credit_a_gate_without_its_artifact(self):
        self.suite(True)
        (self.root / "quest-coverage.json").unlink()
        self.assertEqual("failed", reporter.build_report(self.root)["status"])

    def test_corrupt_suite_results_do_not_hide_selected_scenarios(self):
        self.suite(True)
        (self.root / "suite-results.jsonl").write_text('{"partial":', encoding="utf-8")
        result = reporter.build_report(self.root)
        self.assertEqual("failed", result["status"])
        self.assertEqual(["Q1", "Q2", "L0"], [row["id"] for row in result["scenarios"]])
        self.assertEqual(3, result["counts"]["skipped"])

    def test_path_traversal_is_rejected(self):
        self.suite(True)
        plan = reporter.read_json(self.root / "suite-plan.json")
        plan["steps"][0]["id"] = "../escape"
        self.put("suite-plan.json", plan)
        self.assertEqual("failed", reporter.build_report(self.root)["status"])

    def test_cli_writes_both_reports_on_success_and_failure(self):
        for status in ("passed", "failed"):
            self.fixture(status=status)
            child = subprocess.run([sys.executable, str(Path(reporter.__file__)), str(self.root)], capture_output=True, text=True)
            self.assertEqual(0 if status == "passed" else 1, child.returncode, child.stderr)
            self.assertEqual(status, reporter.read_json(self.root / "report.json")["status"])
            self.assertTrue((self.root / "report.md").exists())
            self.assertIn("Report:", child.stdout)


if __name__ == "__main__":
    unittest.main()
