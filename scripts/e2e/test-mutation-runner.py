#!/usr/bin/env python3
"""Mutation verdict/restoration contract; fake subprocesses, no Docker or bots."""
import contextlib
import importlib.util
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import Mock, patch
import xml.etree.ElementTree as ET

REPO = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("mutation_runner", REPO / "tools/client-extract/run_mutations.py")
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


class MutationRunnerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        self.project = self.root / "Sim.csproj"
        self.project.write_text("<Project />")
        self.target = self.root / "Game.cs"
        self.original = b"\xef\xbb\xbfclass Game { int value = 1; }\r\n"
        self.target.write_bytes(self.original)
        self.directory = self.root / "results"
        self.directory.mkdir()
        self.row = {"file": "Game.cs", "name": "wrong value", "old": "value = 1", "new": "value = 2"}
        self.passed = {"status": "passed", "tests": ["Aion.Simulation.Tests.Journey"], "failures": []}
        self.failed = {"status": "failed", "tests": self.passed["tests"], "failures": self.passed["tests"]}

    def trx(self, outcomes=("Passed",), summary="Completed", **counters):
        ns = runner.NS["t"]
        root = ET.Element("TestRun", xmlns=ns)
        results = ET.SubElement(root, "Results")
        for index, outcome in enumerate(outcomes):
            ET.SubElement(results, "UnitTestResult", testName=f"Aion.Simulation.Tests.Test{index}", outcome=outcome)
        result_summary = ET.SubElement(root, "ResultSummary", outcome=summary)
        counts = {"total": len(outcomes), "executed": len(outcomes), "passed": outcomes.count("Passed"),
                  "failed": outcomes.count("Failed"), "notExecuted": 0}
        counts.update(counters)
        ET.SubElement(result_summary, "Counters", {key: str(value) for key, value in counts.items()})
        path = self.root / "tests.trx"
        ET.ElementTree(root).write(path, encoding="utf-8")
        return path

    def execute(self, results):
        with patch.object(runner, "run_tests", side_effect=results), contextlib.redirect_stdout(io.StringIO()):
            return runner.execute([self.row], self.project, "FullyQualifiedName~Journey", self.directory,
                                  10, "Aion.Simulation.Tests.", self.root)

    def report(self):
        return json.loads((self.directory / "report.json").read_text())

    def test_full_non_ai_names(self):
        result = runner.read_results(self.trx(("Passed", "Failed")), 1)
        self.assertEqual(["Aion.Simulation.Tests.Test1"], result["failures"])

    def test_zero_tests_skips_aborts_and_errors_never_count(self):
        for outcomes, summary in [((), "Completed"), (("NotExecuted",), "Completed"),
                                  (("Failed",), "Aborted"), (("Error",), "Failed")]:
            with self.subTest(outcomes=outcomes, summary=summary), self.assertRaises(ValueError):
                runner.read_results(self.trx(outcomes, summary), 1)

    def test_exit_codes_must_match_actual_results(self):
        for outcomes, code in [(("Passed",), 1), (("Failed",), 0), (("Failed",), 2)]:
            with self.subTest(code=code, outcomes=outcomes), self.assertRaises(ValueError):
                runner.read_results(self.trx(outcomes), code)

    def test_summary_and_duplicate_names_are_rejected(self):
        with self.assertRaises(ValueError):
            runner.read_results(self.trx(("Passed",), "Failed"), 0)
        with self.assertRaises(ValueError):
            runner.read_results(self.trx(("Failed",), "Passed"), 1)
        path = self.trx(("Passed", "Passed"))
        tree = ET.parse(path)
        rows = tree.getroot().findall("t:Results/t:UnitTestResult", runner.NS)
        rows[1].set("testName", rows[0].get("testName"))
        tree.write(path)
        with self.assertRaises(ValueError):
            runner.read_results(path, 0)

    def test_counters_and_infrastructure_errors_rejected(self):
        for counters in [{"total": 3}, {"failed": 1}, {"executed": 0}, {"error": 1}]:
            with self.subTest(counters=counters), self.assertRaises(ValueError):
                runner.read_results(self.trx(**counters), 0)
        path = self.trx(("Failed",))
        tree = ET.parse(path)
        summary = tree.getroot().find("t:ResultSummary", runner.NS)
        info = ET.SubElement(summary, "RunInfos")
        ET.SubElement(info, "RunInfo", outcome="Error")
        tree.write(path)
        # Explicit namespace is needed for an element appended to a parsed document.
        text = path.read_text().replace("<RunInfos>", '<RunInfos xmlns="' + runner.NS["t"] + '">')
        path.write_text(text)
        with self.assertRaises(ValueError):
            runner.read_results(path, 1)

    def test_xunit_failure_announcement_must_name_an_actual_failed_result(self):
        for message, accepted in [
            ("[xUnit.net 00:00:58.55]     Aion.Simulation.Tests.Test0 [FAIL]", True),
            ("[xUnit.net 00:00:58.55]     Aion.Simulation.Tests.Unrelated [FAIL]", False),
            ("Test host crashed", False),
        ]:
            with self.subTest(message=message):
                path = self.trx(("Failed",))
                tree = ET.parse(path)
                summary = tree.getroot().find("t:ResultSummary", runner.NS)
                info = ET.SubElement(summary, "{" + runner.NS["t"] + "}RunInfos")
                row = ET.SubElement(info, "{" + runner.NS["t"] + "}RunInfo", outcome="Error")
                ET.SubElement(row, "{" + runner.NS["t"] + "}Text").text = message
                tree.write(path)
                if accepted:
                    self.assertEqual("failed", runner.read_results(path, 1)["status"])
                else:
                    with self.assertRaises(ValueError):
                        runner.read_results(path, 1)

    def test_missing_and_malformed_trx(self):
        with self.assertRaises(FileNotFoundError):
            runner.read_results(self.root / "absent.trx", 0)
        path = self.root / "bad.trx"
        path.write_text("invalid")
        with self.assertRaises(ET.ParseError):
            runner.read_results(path, 0)

    def test_build_failure_never_runs_tests(self):
        with patch.object(runner, "run_command", return_value=1) as command:
            result = runner.run_tests(self.project, "Journey", self.directory / "one", 10, self.root)
        self.assertEqual("did-not-compile", result["status"])
        self.assertEqual(1, command.call_count)

    def test_project_filter_and_unique_artifacts_reach_dotnet(self):
        def command(args, root, log, env, timeout):
            self.assertEqual(str(self.project), args[2])
            self.assertEqual(str(log.parent), env["AION_E2E_RUN_DIR"])
            if args[1] == "test":
                self.assertIn("--no-build", args)
                self.assertEqual("FullyQualifiedName~Journey", args[args.index("--filter") + 1])
                (log.parent / "tests.trx").write_bytes(self.trx().read_bytes())
            return 0
        with patch.object(runner, "run_command", side_effect=command):
            result = runner.run_tests(self.project, "FullyQualifiedName~Journey", self.directory / "one", 10, self.root)
        self.assertEqual("passed", result["status"])

    def test_missing_trx_and_timeout_are_inconclusive(self):
        for index, effect in enumerate(([0, 0], subprocess.TimeoutExpired("dotnet", 10))):
            with self.subTest(effect=effect), patch.object(runner, "run_command", side_effect=effect):
                result = runner.run_tests(self.project, "Journey", self.directory / str(index), 10, self.root)
                self.assertEqual("inconclusive", result["status"])

    def test_real_command_exit_and_bounded_timeout(self):
        log = self.directory / "command.log"
        self.assertEqual(7, runner.run_command([sys.executable, "-c", "print('evidence'); raise SystemExit(7)"],
                                              self.root, log, os.environ.copy(), 5))
        self.assertIn("evidence", log.read_text())
        with self.assertRaises(subprocess.TimeoutExpired):
            runner.run_command([sys.executable, "-c", "import time; time.sleep(30)"],
                               self.root, self.directory / "timeout.log", os.environ.copy(), 0.2)

    def test_uncertain_process_cleanup_stops_instead_of_continuing_mutations(self):
        process = Mock(pid=12345)
        process.wait.side_effect = subprocess.TimeoutExpired("dotnet", 10)
        cleanup = patch.object(runner.subprocess, "run", side_effect=OSError("cleanup failed")) if os.name == "nt" else \
            patch.object(runner.os, "killpg", side_effect=OSError("cleanup failed"))
        with patch.object(runner.subprocess, "Popen", return_value=process), cleanup:
            with self.assertRaisesRegex(RuntimeError, "Could not confirm process-tree cleanup"):
                runner.run_tests(self.project, "Journey", self.directory / "cleanup", 10, self.root)

    def test_caught_restores_exact_bytes_and_rebuilds(self):
        def run(*args):
            stage = args[2].name
            self.assertEqual(self.original.replace(b"value = 1", b"value = 2") if stage.startswith("mutant")
                             else self.original, self.target.read_bytes())
            return self.failed if stage.startswith("mutant") else self.passed
        self.assertEqual(0, self.execute(run))
        self.assertEqual(self.original, self.target.read_bytes())
        report = self.report()
        self.assertEqual("passed", report["status"])
        self.assertTrue(report["mutations"][0]["restored"])
        self.assertEqual(self.failed["failures"], report["mutations"][0]["failures"])
        self.assertEqual(self.original, (self.directory / "001-original.bin").read_bytes())

    def test_survivor_compile_error_and_inconclusive_exit_nonzero(self):
        for status in ["passed", "did-not-compile", "inconclusive"]:
            with self.subTest(status=status):
                mutant = {**self.passed, "status": status}
                self.assertEqual(1, self.execute([self.passed, mutant, self.passed]))
                self.assertEqual("survived" if status == "passed" else status, self.report()["mutations"][0]["status"])
                self.assertEqual(self.original, self.target.read_bytes())

    def test_baseline_failure_never_mutates(self):
        for status in ["failed", "did-not-compile", "inconclusive"]:
            with self.subTest(status=status):
                self.assertEqual(1, self.execute([{**self.passed, "status": status}]))
                self.assertEqual([], self.report()["mutations"])
                self.assertEqual(self.original, self.target.read_bytes())

    def test_changed_test_selection_is_not_caught(self):
        self.assertEqual(1, self.execute([self.passed, {**self.failed, "tests": ["Different"]}, self.passed]))
        self.assertEqual("inconclusive", self.report()["mutations"][0]["status"])

    def test_restored_baseline_must_pass_same_selection(self):
        for restored in [self.failed, {**self.passed, "tests": ["Different"]}]:
            with self.subTest(restored=restored):
                self.assertEqual(1, self.execute([self.passed, self.failed, restored]))

    def test_exceptions_and_interrupts_restore_exact_bytes(self):
        for error in [RuntimeError("failed"), KeyboardInterrupt()]:
            with self.subTest(error=error), self.assertRaises(type(error)):
                self.execute([self.passed, error])
            self.assertEqual(self.original, self.target.read_bytes())
            self.assertTrue(self.report()["mutations"][0]["restored"])

    def test_concurrent_edits_not_overwritten(self):
        def run(*args):
            if args[2].name.startswith("mutant"):
                self.target.write_bytes(b"concurrent user edit")
                return self.failed
            return self.passed
        with self.assertRaisesRegex(RuntimeError, "Concurrent edit preserved"):
            self.execute(run)
        self.assertEqual(b"concurrent user edit", self.target.read_bytes())
        self.assertFalse(self.report()["mutations"][0]["restored"])
        self.assertEqual(self.original, (self.directory / "001-original.bin").read_bytes())

    def test_spec_rejects_invalid_or_ambiguous_edits(self):
        for value in [[], {}, [self.row, self.row], [{**self.row, "old": ""}],
                      [{**self.row, "new": self.row["old"]}], [{**self.row, "file": "../escape"}],
                      [{**self.row, "old": "missing"}], [{**self.row, "old": " "}]]:
            with self.subTest(value=value):
                path = self.root / "spec.json"
                path.write_text(json.dumps(value))
                with self.assertRaises(ValueError):
                    runner.load_spec(path, self.root)

    def test_project_default_and_commandline_override(self):
        path = self.root / "spec.json"
        path.write_text(json.dumps([self.row]))
        default = self.root / runner.TEST_PROJECT
        default.parent.mkdir(parents=True)
        default.write_text("<Project />")
        with patch.object(runner, "ROOT", self.root), patch.object(runner, "execute", return_value=0) as execute:
            self.assertEqual(0, runner.main([str(path), "--filter", "Ai.Test"]))
            self.assertEqual(default, execute.call_args.args[1])
            self.assertEqual(0, runner.main([str(path), "--filter", "Journey", "--project", "Sim.csproj"]))
            self.assertEqual(self.project, execute.call_args.args[1])
        self.assertFalse((self.root / "run/mutation-runner.lock").exists())

    def test_lock_and_existing_results_fail_without_execution(self):
        path = self.root / "spec.json"
        path.write_text(json.dumps([self.row]))
        lock = self.root / "run/mutation-runner.lock"
        lock.parent.mkdir()
        lock.write_text("another owner")
        with patch.object(runner, "ROOT", self.root), patch.object(runner, "execute") as execute, contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(1, runner.main([str(path), "--filter", "Journey", "--project", "Sim.csproj"]))
            self.assertEqual(1, runner.main([str(path), "--filter", "Journey", "--project", "Sim.csproj",
                                             "--results-directory", str(self.directory)]))
            execute.assert_not_called()
        self.assertEqual("another owner", lock.read_text())


if __name__ == "__main__":
    unittest.main()
