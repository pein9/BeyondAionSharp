#!/usr/bin/env python3
import copy
import gzip
import hashlib
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
                 status=status, startedUtc=STAMP, finishedUtc=STAMP, durationSeconds=4.5, gitSha="contract", seed=1,
                 error=None if status == "passed" else "runner failure"), root)
        if mode != "FULL":
            self.journal([row for scenario in scenarios for row in self.scenario(scenario, mode=mode, run=run)], root)
        if mode == "SIM":
            self.sim_evidence(scenarios, run=run, root=root)

    def sim_evidence(self, scenarios=("Q1",), run="run-a", root=None, ledger=None, allowlist=None, observations=()):
        root = root or self.root
        header = dict(schemaVersion=1, event="run-started", run=run, mode="SIM", seed=1,
                      gitSha="contract", profile="sim-fast", startedUtc=STAMP)
        for name, snapshot in (("ledger", ledger or []), ("allowlist", allowlist or [])):
            path = f"sim-{name}-at-start.json"
            self.put(path, snapshot, root)
            header[name+"Sha256"] = hashlib.sha256((root/path).read_bytes()).hexdigest()
        rows = [header]
        for policy, scenario in enumerate(scenarios, 1):
            common = dict(run=run, policy=policy, scenario=scenario)
            rows.extend([dict(common, event="policy-started"), dict(common, event="policy-completed",
                assertedClean=True, assertionPassed=True, virtualMillis=100, observations=list(observations))])
        rows.append(dict(event="run-completed", run=run, policiesStarted=len(scenarios), policiesCompleted=len(scenarios), activePolicies=[]))
        self.write_sim_rows(rows, root)
        self.sim_resources(scenarios, run=run, root=root)
        return rows

    def sim_resources(self, scenarios=("Q1",), run="run-a", root=None):
        rows = [dict(schemaVersion=1, event="run-started", run=run, mode="SIM", seed=1, gitSha="contract",
            profile="sim-fast", startedUtc=STAMP, processId=42, sampling="run-policy-and-bot-action-boundaries",
            scope="post-bootstrap-test-process", timerSource="VirtualThreadPool.ArmedTimerCount", heartbeat="not-applicable")]
        boundaries = [("run-started", None, None)]
        for policy, scenario in enumerate(scenarios, 1):
            boundaries += [("policy-started", scenario, policy), ("policy-completed", scenario, policy)]
        boundaries += [("run-completed", None, None)]
        for sequence, (trigger, scenario, policy) in enumerate(boundaries, 1):
            rows.append(dict(event="sample", run=run, sequence=sequence, timestampUtc=STAMP, elapsedSeconds=sequence,
                virtualMillis=sequence*1000, trigger=trigger, scenario=scenario, policy=policy, bot=None, account=None,
                step=None, armedTimers=sequence%3, metrics=dict(workingSetBytes=100+sequence*10,
                    processLifetimePeakWorkingSetBytes=2000, lastGcIndex=0, lastGcHeapBytes=None), error=None))
        rows.append(dict(event="run-completed", run=run, attempts=len(boundaries), samples=len(boundaries), errors=0))
        self.write_resource_rows(rows, root)
        return rows

    def write_resource_rows(self, rows, root=None):
        (root or self.root).joinpath("sim-resources.jsonl").write_text("".join(json.dumps(row)+"\n" for row in rows), encoding="utf-8")

    def test_sim_resource_samples_preserve_scope_gaps_observed_peaks_and_heap_availability(self):
        self.fixture()
        result = reporter.build_report(self.root)
        self.assertEqual("passed", result["status"])
        peak = result["resourcePeaks"][0]
        self.assertTrue(peak["complete"])
        self.assertEqual(4, peak["samples"])
        self.assertEqual(140, peak["peakWorkingSetBytes"])
        self.assertEqual(2000, peak["processLifetimePeakWorkingSetBytes"])
        self.assertEqual(2, peak["peakArmedTimers"])
        self.assertEqual(1, peak["maximumObservedWallGapSeconds"])
        self.assertEqual(1000, peak["maximumObservedVirtualGapMillis"])
        self.assertIsNone(peak["peakLastGcHeapBytes"])
        self.assertEqual([], result["heartbeatPeaks"])
        self.assertEqual(hashlib.sha256((self.root/"sim-resources.jsonl").read_bytes()).hexdigest(), peak["evidenceSha256"])
        self.assertIn("sim-resources.jsonl", reporter.markdown(result))
        rows = self.sim_resources()
        rows[-2]["metrics"].update(lastGcIndex=3, lastGcHeapBytes=91)
        self.write_resource_rows(rows)
        peak = reporter.build_report(self.root)["resourcePeaks"][0]
        self.assertEqual(91, peak["peakLastGcHeapBytes"])
        self.assertEqual(3, peak["lastGcIndex"])

    def test_sim_failed_resource_observations_remain_failed_after_good_samples(self):
        self.fixture()
        rows = self.sim_resources()
        rows[2].update(event="sample-error", metrics=None, armedTimers=None, error="counter unavailable")
        rows[-1].update(samples=3, errors=1)
        self.write_resource_rows(rows)
        result = reporter.build_report(self.root)
        self.assertEqual("failed", result["status"])
        self.assertEqual(1, result["resourcePeaks"][0]["errors"])
        self.assertEqual(3, result["resourcePeaks"][0]["samples"])
        self.assertEqual(140, result["resourcePeaks"][0]["peakWorkingSetBytes"])
        for row in rows[1:-1]: row.update(event="sample-error", metrics=None, armedTimers=None, error="unavailable")
        rows[-1].update(samples=0, errors=4)
        self.write_resource_rows(rows)
        result = reporter.build_report(self.root)
        self.assertEqual("failed", result["status"])
        self.assertIsNone(result["resourcePeaks"][0]["peakWorkingSetBytes"])
        self.assertIsNone(result["resourcePeaks"][0]["peakArmedTimers"])

    def test_missing_incomplete_or_corrupt_sim_resources_fail_without_erasing_problem_evidence(self):
        for variant in ("missing", "empty", "no-footer", "partial"):
            with self.subTest(variant=variant):
                self.fixture()
                self.sim_evidence(ledger=[dict(fp="1234abcd", status="tracked")], observations=[dict(
                    fingerprint="1234abcd", disposition="KNOWN", allowlisted=False, server="gs")])
                rows = list(reporter.lines(self.root/"sim-resources.jsonl"))
                if variant == "missing": (self.root/"sim-resources.jsonl").unlink()
                elif variant == "empty": self.write_resource_rows([])
                else:
                    self.write_resource_rows(rows[:-1])
                    if variant == "partial":
                        with (self.root/"sim-resources.jsonl").open("a", encoding="utf-8") as f: f.write('{"partial":')
                result = reporter.build_report(self.root)
                self.assertEqual("failed", result["status"])
                self.assertEqual("KNOWN", result["fingerprints"][0]["disposition"])
                if variant in ("no-footer", "partial"):
                    self.assertFalse(result["resourcePeaks"][0]["complete"])
                    self.assertEqual(140, result["resourcePeaks"][0]["peakWorkingSetBytes"])

    def test_sim_resource_corrupt_counters_identity_scopes_or_order_cannot_pass(self):
        mutations = [lambda r: r[0].update(run="other"), lambda r: r[0].update(seed=True),
            lambda r: r[0].update(profile="other"), lambda r: r[0].update(processId=0),
            lambda r: r[0].update(sampling="continuous"), lambda r: r[0].update(scope="game-server-only"),
            lambda r: r[0].update(heartbeat="live"), lambda r: r[2].update(sequence=1),
            lambda r: r[2].update(elapsedSeconds=-1), lambda r: r[2].update(elapsedSeconds=0),
            lambda r: r[2].update(virtualMillis=0), lambda r: r[2].update(armedTimers=None),
            lambda r: r[2].update(armedTimers=-1), lambda r: r[2].update(armedTimers=True),
            lambda r: r[2]["metrics"].update(workingSetBytes=0), lambda r: r[2]["metrics"].update(workingSetBytes=1.5),
            lambda r: r[2]["metrics"].update(lastGcIndex=1), lambda r: r[2]["metrics"].update(lastGcHeapBytes=0),
            lambda r: r[2].update(policy=2), lambda r: r[3].update(scenario="other"),
            lambda r: r[-1].update(samples=99), lambda r: r.append(r[-1]),
            lambda r: r[2].update(trigger="bot-action", policy=None, bot="b01", account="a", step="s01")]
        for mutate in mutations:
            with self.subTest(mutate=mutate):
                self.fixture()
                rows = self.sim_resources()
                mutate(rows)
                self.write_resource_rows(rows)
                self.assertEqual("failed", reporter.build_report(self.root)["status"])
        self.fixture()
        # Plausible complete metrics for fewer policies cannot replace the selected run.
        rows = self.sim_resources(scenarios=())
        self.assertEqual("failed", reporter.build_report(self.root)["status"])
        self.fixture()
        outcome = reporter.read_json(self.root/"runner-result.json")
        outcome["seed"] = True  # Python's True == 1 must not establish provenance.
        self.put("runner-result.json", outcome)
        self.assertEqual("failed", reporter.build_report(self.root)["status"])

    def test_full_joins_sim_resource_provenance_without_mixing_it_with_live_heartbeats(self):
        self.suite(True)
        result = reporter.build_report(self.root)
        self.assertEqual("passed", result["status"])
        self.assertEqual(1, len(result["resourcePeaks"]))
        self.assertEqual("sim-a/sim-resources.jsonl", result["resourcePeaks"][0]["evidence"])
        self.assertEqual("sim-a", result["resourcePeaks"][0]["child"])
        self.assertEqual("gs", result["heartbeatPeaks"][0]["server"])
        (self.root/"sim-a/sim-resources.jsonl").unlink()
        self.assertEqual("failed", reporter.build_report(self.root)["status"])

    def write_sim_rows(self, rows, root=None):
        (root or self.root).joinpath("sim-problems.jsonl").write_text("".join(json.dumps(row)+"\n" for row in rows), encoding="utf-8")

    def sim_bundle(self, fp="1234abcd", **changes):
        bundle = self.root / "problems" / fp
        self.put("metadata.json", dict(dict(run="run-a", mode="SIM", scenario="Q1", fingerprint=fp,
            seed=1, gitSha="contract", configProfile="sim-fast"), **changes), bundle)
        for name in ("stack.txt", "server-context.log", "bot-trace.jsonl", "draft-backlog.md"):
            (bundle/name).write_text("retained evidence\n", encoding="utf-8")

    def test_sim_requires_export_and_a_completed_policy_for_every_passed_scenario(self):
        for variant in ("missing", "empty", "no-footer", "active", "missing-scenario", "unasserted"):
            with self.subTest(variant=variant):
                self.fixture(scenarios=["Q1", "Q2"])
                rows = list(reporter.lines(self.root/"sim-problems.jsonl"))
                if variant == "missing":
                    (self.root/"sim-problems.jsonl").unlink()
                else:
                    if variant == "empty": rows = []
                    if variant == "no-footer": rows.pop()
                    if variant == "active":
                        rows.pop(4)
                        rows[-1].update(policiesCompleted=1, activePolicies=[2])
                    if variant == "missing-scenario":
                        rows = rows[:3]+[dict(rows[-1], policiesStarted=1, policiesCompleted=1)]
                    if variant == "unasserted": rows[2]["assertedClean"] = False
                    self.write_sim_rows(rows)
                self.assertEqual("failed", reporter.build_report(self.root)["status"])

    def test_sim_dispositions_preserve_strict_policy_even_if_journal_claims_success(self):
        for status, disposition in (("new", "NEW"), ("tracked", "KNOWN"), ("fixed", "REGRESSED")):
            with self.subTest(status=status):
                self.fixture()
                self.sim_evidence(ledger=[dict(fp="1234abcd", status=status)], observations=[dict(
                    fingerprint="1234abcd", disposition=disposition, allowlisted=False, server="gs")])
                if disposition == "NEW": self.sim_bundle()
                result = reporter.build_report(self.root)
                self.assertEqual("failed", result["status"])
                self.assertEqual(disposition, result["fingerprints"][0]["disposition"])
                self.assertEqual(1, result["fingerprints"][0]["policyObservations"])
                if disposition == "NEW": self.assertIn("problems/1234abcd/metadata.json", reporter.markdown(result))

    def test_sim_allowances_require_frozen_owner_scope_expiry_and_count(self):
        allowance = dict(fp="1234abcd", reason="test", owner="tests", tracking="P10-10", modes=["SIM"], servers=["gs"],
                         scenarios=["Q1"], maxCount=1, expires="2099-01-01")
        observation = dict(fingerprint="1234abcd", disposition="ALLOWLISTED", allowlisted=True, server="gs")
        for change in ({}, {"owner": ""}, {"expires": "2000-01-01"}, {"modes": ["LIVE"]},
                       {"servers": ["ls"]}, {"scenarios": ["Q2"]}, {"maxCount": 0}):
            with self.subTest(change=change):
                self.fixture()
                self.sim_evidence(allowlist=[dict(allowance, **change)], observations=[observation])
                self.assertEqual("failed" if change else "passed", reporter.build_report(self.root)["status"])
        self.sim_evidence(allowlist=[allowance], observations=[observation, observation])
        self.assertEqual("failed", reporter.build_report(self.root)["status"])

    def test_sim_snapshot_provenance_sequence_and_repro_validation_fail_closed(self):
        mutations = [lambda r: r[0].update(run="foreign"), lambda r: r[0].update(seed=2),
                     lambda r: r[0].update(gitSha="other"), lambda r: r[0].update(ledgerSha256="bad"),
                     lambda r: r[1].update(policy=2), lambda r: r[2].update(policy=2),
                     lambda r: r[2].update(scenario="Q2"), lambda r: r[2].update(assertionPassed=None),
                     lambda r: r[-1].update(policiesCompleted=2), lambda r: r.append(r[-1])]
        for mutate in mutations:
            self.fixture()
            rows = self.sim_evidence()
            mutate(rows)
            self.write_sim_rows(rows)
            self.assertEqual("failed", reporter.build_report(self.root)["status"])
        self.fixture()
        self.sim_evidence(observations=[dict(fingerprint="1234abcd", disposition="NEW", allowlisted=False, server="gs")])
        self.assertIn("missing repro", " ".join(reporter.build_report(self.root)["evidenceIssues"]))
        self.sim_bundle(seed=2)
        self.assertIn("repro provenance mismatch", " ".join(reporter.build_report(self.root)["evidenceIssues"]))

    def test_sim_nested_policy_receipts_keep_counts_explicit_and_partial_export_keeps_findings(self):
        self.fixture()
        rows = self.sim_evidence(observations=[dict(fingerprint="1234abcd", disposition="KNOWN", allowlisted=False, server="gs")],
            ledger=[dict(fp="1234abcd", status="tracked")])
        rows = [rows[0], rows[1], dict(rows[1], policy=2), dict(rows[2], policy=2), rows[2],
                dict(rows[3], policiesStarted=2, policiesCompleted=2)]
        self.write_sim_rows(rows)
        report = reporter.build_report(self.root)
        self.assertEqual(2, report["fingerprints"][0]["policyObservations"])
        self.assertEqual(2, report["simulation"]["policiesCompleted"])
        with (self.root/"sim-problems.jsonl").open("a", encoding="utf-8") as f: f.write('{"partial":')
        report = reporter.build_report(self.root)
        self.assertEqual("failed", report["status"])
        self.assertEqual("KNOWN", report["fingerprints"][0]["disposition"])

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
