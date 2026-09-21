#!/usr/bin/env python3
"""Promotion eligibility tests using real raw report/retry fixtures; no shared ledger writes."""
import json
from pathlib import Path
import subprocess
import sys
import unittest
from unittest.mock import patch

import importlib.util


def module(name, file):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(file))
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


helpers = module("promotion_fixtures", "test-full-flake.py")
gate = module("promotion_gate", "validate-full-promotion.py")
SHA = "a" * 40


class FullPromotionTests(unittest.TestCase):
    def setUp(self):
        self.fixture = helpers.FullFlakeTests()
        self.fixture.setUp()
        self.addCleanup(self.fixture.doCleanups)
        self.fixture.suite(("passed",))
        self.root = self.fixture.root
        self.put = self.fixture.put
        for path in self.root.rglob("*.json"):
            path.write_text(path.read_text().replace('"contract"', json.dumps(SHA)))
        self.plan = gate.read(self.root / "suite-plan.json")
        self.plan["simShards"] = 2
        self.put("suite-plan.json", self.plan)
        self.retain()
        self.addCleanup(patch.stopall)
        patch.object(gate, "current_revision", return_value=SHA).start()
        # The raw fixture has one LIVE child. Policy tests isolate completeness
        # via the production planner tests below; no reduced plan qualifies in CLI.
        patch.object(gate, "expected_plan", return_value=self.plan["steps"]).start()

    def retain(self):
        self.report = gate.reporter.build_report(self.root)
        self.put("report.json", self.report)
        self.put("flake-history.json", dict(schemaVersion=1, run="full", reportSha256=gate.sha(self.root / "report.json")))

    def test_clean_aggregate_pass_uses_raw_report_and_retained_history(self):
        proof = gate.validate(self.root)
        self.assertEqual("passed", proof["status"])
        self.assertEqual(SHA, proof["gitSha"])
        self.assertEqual([], proof["seenFingerprints"])

    def test_known_fingerprints_remain_seen_even_in_green_report(self):
        self.fixture.fixture.helper.watcher(("KNOWN",), root=self.root / "full-l0", run="full-l0")
        summary = gate.read(self.root / "full-l0/logwatch-summary.json")
        self.put("logwatch-summary.json", dict(summary, failed=False), self.root / "full-l0")
        self.retain()
        self.assertTrue(gate.validate(self.root)["seenFingerprints"])

    def test_allowlisted_live_occurrence_is_not_proof_of_absence(self):
        path = self.root / "full-l0/digest.log"
        path.write_text(helpers.STAMP + " ALLOWLISTED ERROR gs fp=1234abcd | scoped fixture\n")
        self.retain()
        self.assertEqual([], self.report["fingerprints"])
        self.assertEqual(["1234abcd"], gate.validate(self.root)["seenFingerprints"])

    def test_failed_or_flaky_aggregate_cannot_promote(self):
        for status in ("failed", "flaky"):
            with self.subTest(status=status):
                self.fixture.result["status"] = status
                self.fixture.results()
                self.retain()
                with self.assertRaisesRegex(ValueError, "clean aggregate"):
                    gate.validate(self.root)

    def test_stale_report_missing_child_or_changed_watcher_cannot_promote(self):
        path = self.root / "full-l0/scenario-results.jsonl"
        content = path.read_bytes()
        path.unlink()
        with self.assertRaisesRegex(ValueError, "clean aggregate"):
            gate.validate(self.root)
        path.write_bytes(content)
        self.put("logwatch-summary.json", dict(failed=True), self.root / "full-l0")
        with self.assertRaisesRegex(ValueError, "clean aggregate"):
            gate.validate(self.root)

    def test_changed_revision_or_seed_cannot_promote(self):
        for changes in (dict(gitSha="b" * 40), dict(seed=2)):
            self.put("suite-plan.json", dict(self.plan, **changes))
            self.retain()
            with self.assertRaises(ValueError):
                gate.validate(self.root)

    def test_standalone_and_soak_only_cannot_promote(self):
        for suite in ("Standalone", "Soak"):
            self.put("suite-plan.json", dict(self.plan, suite=suite))
            self.retain()
            with self.assertRaisesRegex(ValueError, "complete current breadth"):
                gate.validate(self.root)

    def test_partial_plan_or_missing_gate_cannot_promote(self):
        with patch.object(gate, "expected_plan", return_value=self.plan["steps"] + [dict(kind="PacketParity", id="l0-packet-parity")]):
            with self.assertRaisesRegex(ValueError, "complete current breadth"):
                gate.validate(self.root)

    def test_missing_or_changed_history_is_not_a_success(self):
        path = self.root / "flake-history.json"
        path.unlink()
        with self.assertRaises(OSError):
            gate.validate(self.root)
        self.put("flake-history.json", dict(schemaVersion=1, run="full", reportSha256="0" * 64))
        with self.assertRaisesRegex(ValueError, "history receipt"):
            gate.validate(self.root)

    def test_promotion_error_marker_keeps_rerendered_report_failed(self):
        self.put("problem-ledger-error.json", dict(error="promotion failed"))
        self.retain()
        self.assertEqual("failed", self.report["status"])
        self.assertIn("promotion failed", self.report["evidenceIssues"])
        with self.assertRaises(ValueError):
            gate.validate(self.root)

    def test_sim_child_must_match_aggregate_revision_even_if_its_own_report_passes(self):
        child = self.root / "sim-test"
        self.fixture.fixture.helper.fixture("SIM", ["Q1"], run="full", root=child)
        self.plan["steps"].append(dict(id="sim-test", kind="Sim", scenarios=["Q1"]))
        self.put("suite-plan.json", self.plan)
        with (self.root / "suite-results.jsonl").open("a") as stream:
            stream.write(json.dumps(dict(id="sim-test", kind="Sim", status="passed", durationSeconds=1)) + "\n")
        self.retain()
        self.assertEqual("passed", self.report["status"])
        with self.assertRaisesRegex(ValueError, "child revision/seed mismatch"):
            gate.validate(self.root)

    def test_actual_flaky_raw_evidence_is_not_eligible_even_after_history_is_saved(self):
        fixture = helpers.FullFlakeTests()
        fixture.setUp()
        self.addCleanup(fixture.doCleanups)
        fixture.suite(("failed", "passed"))
        report = fixture.retain_report()
        helpers.history.record(fixture.root, fixture.ledger)
        self.assertEqual(1, report["counts"]["flaky"])
        with self.assertRaisesRegex(ValueError, "clean aggregate"):
            gate.validate(fixture.root)

    def test_actual_planner_requires_all_manifest_breadth_and_all_gates(self):
        result = subprocess.run(["pwsh", "-NoProfile", "-File", str(Path(gate.__file__).with_name("full-promotion-plan.ps1")),
            "-PlanPath", str(self.root / "suite-plan.json")], capture_output=True, text=True)
        self.assertEqual(0, result.returncode, result.stderr)
        steps = json.loads(result.stdout)
        manifest = gate.read(gate.REPO / "parity-artifacts/e2e/scenarios.json")
        for mode, kind in (("Sim", "Sim"), ("Live", "Live")):
            actual = [name for step in steps if step["kind"] == kind for name in
                      (step["scenarios"] if kind == "Sim" else [step["scenario"]])]
            self.assertCountEqual([s["id"] for s in manifest if mode in s["modes"] and s["tier"] != "Soak"], actual)
        self.assertEqual({"QuestCoverage", "PacketParity", "PacketCoverage"},
                         {s["kind"] for s in steps} - {"Sim", "Live"})
        with patch.object(gate, "expected_plan", return_value=steps):
            with self.assertRaisesRegex(ValueError, "complete current breadth"):
                gate.validate(self.root)

    def test_cli_rejects_reduced_fixture_without_writing_any_ledger(self):
        result = subprocess.run([sys.executable, gate.__file__, str(self.root)], capture_output=True, text=True)
        self.assertEqual(2, result.returncode)
        self.assertEqual("", result.stdout)


if __name__ == "__main__":
    unittest.main()
