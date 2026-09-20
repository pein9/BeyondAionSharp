#!/usr/bin/env python3
"""Run seeded source regressions against an explicitly selected test set.

Usage: python tools/client-extract/run_mutations.py spec.json --filter EXPRESSION
       [--project tests/Aion.Simulation.Tests/Aion.Simulation.Tests.csproj]
       [--results-directory run/my-mutations] [--name-prefix Aion.Simulation.Tests.]

Specs are JSON lists of {file, name, old, new}, with file paths relative to the
repository. Anchors match exactly once, including line endings. Run exclusively:
no concurrent builds, tests or source edits. Source bytes are backed up and restored
in finally; concurrent edits are preserved and stop the run. After all mutations,
rebuild and rerun the original baseline to clear mutant binaries.

Only a completed TRX with the baseline's exact test set and actual failed tests
counts as caught. Build errors, zero tests, skips, aborts, missing/malformed TRX,
and unexplained nonzero exits prove nothing. Survivors and inconclusive runs exit 1.
An all-caught run exits 0. Logs, TRX and report.json retain full test names;
name-prefix only abbreviates console output.

Test prerequisites are inherited from the environment. For SIM use Docker MySQL,
AION_SIM_DB_INTEGRATION=1, and the usual tier/shard/seed settings. Each invocation
gets its own AION_E2E_RUN_DIR so scenario artifacts do not overwrite one another.
This runner never starts a host database or hides missing prerequisites.
"""
import argparse
from collections import Counter
import hashlib
import json
import os
from pathlib import Path
import re
import signal
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
TEST_PROJECT = "tests/Aion.GameServer.Tests/Aion.GameServer.Tests.csproj"
NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def run_command(command, cwd, log, env, timeout):
    """Stream logs to disk; terminate the owned process tree on timeout/interrupt."""
    with log.open("w", encoding="utf-8") as output:
        options = ({"start_new_session": True} if os.name != "nt" else
                   {"creationflags": subprocess.CREATE_NEW_PROCESS_GROUP})
        process = subprocess.Popen(command, cwd=cwd, env=env, stdout=output,
                                   stderr=subprocess.STDOUT, **options)
        try:
            return process.wait(timeout=timeout)
        except BaseException:
            try:
                if os.name == "nt":
                    subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"],
                                   stdout=output, stderr=subprocess.STDOUT, timeout=30, check=True)
                else:
                    os.killpg(process.pid, signal.SIGKILL)
                process.wait(timeout=30)
            except (OSError, subprocess.SubprocessError) as error:
                # Do not let run_tests classify uncertain cleanup as an ordinary
                # inconclusive result and start another build beside surviving children.
                raise RuntimeError(f"Could not confirm process-tree cleanup for PID {process.pid}; "
                                   "stop and inspect processes before rebuilding") from error
            raise


def read_results(path, returncode):
    """No console-text heuristics: completed, non-skipped, consistent TRX only."""
    root = ET.parse(path).getroot()
    summary = root.find("t:ResultSummary", NS)
    if summary is None or summary.get("outcome") not in {"Completed", "Passed", "Failed"}:
        raise ValueError("test run did not complete")
    rows = root.findall("t:Results/t:UnitTestResult", NS)
    names = [row.get("testName") for row in rows]
    if not rows or any(not name for name in names) or len(set(names)) != len(names):
        raise ValueError("empty, unnamed or ambiguous test results")
    outcomes = Counter(row.get("outcome") for row in rows)
    if set(outcomes) - {"Passed", "Failed"}:
        raise ValueError("skipped/incomplete tests are not mutation evidence")
    counters = summary.find("t:Counters", NS)
    expected = {"total": len(rows), "executed": len(rows),
                "passed": outcomes["Passed"], "failed": outcomes["Failed"]}
    if counters is None or any(int(counters.get(key, "-1")) != value
                               for key, value in expected.items()):
        raise ValueError("TRX counters disagree with test results")
    if any(int(value) != 0 for key, value in counters.attrib.items() if key not in expected):
        raise ValueError("TRX contains skipped, aborted or errored tests")
    failures = sorted(row.get("testName") for row in rows if row.get("outcome") == "Failed")
    # xUnit's VSTest adapter emits ordinary failure announcements as RunInfo errors.
    # Accept only that exact notification for a failed TRX test; other errors stay fatal.
    for info in summary.findall("t:RunInfos/t:RunInfo[@outcome='Error']", NS):
        announcement = re.fullmatch(r"\[xUnit\.net \d+:\d+:\d+\.\d+\]\s+(.+) \[FAIL\]",
                                    (info.findtext("t:Text", "", NS)).strip())
        if announcement is None or announcement[1] not in failures:
            raise ValueError("test runner reported an infrastructure error")
    if (summary.get("outcome") == "Passed" and failures or
            summary.get("outcome") == "Failed" and not failures):
        raise ValueError("TRX summary disagrees with test results")
    if returncode != (1 if failures else 0):
        raise ValueError(f"test exit {returncode} disagrees with TRX")
    return {"status": "failed" if failures else "passed", "tests": sorted(names),
            "failures": failures}


def run_tests(project, test_filter, directory, timeout, root=ROOT):
    directory.mkdir()
    env = os.environ.copy()
    env["AION_E2E_RUN_DIR"] = str(directory)
    result = {"status": "inconclusive", "tests": [], "failures": []}
    try:
        build = run_command(["dotnet", "build", str(project), "--nologo", "-v", "q"],
                            root, directory / "build.log", env, timeout)
        if build != 0:
            return {**result, "status": "did-not-compile", "reason": f"build exit {build}"}
        code = run_command(["dotnet", "test", str(project), "--no-build", "--no-restore",
                            "--filter", test_filter, "--nologo", "-v", "q",
                            "--results-directory", str(directory),
                            "--logger", "trx;LogFileName=tests.trx"],
                           root, directory / "test.log", env, timeout)
        return read_results(directory / "tests.trx", code)
    except (OSError, ValueError, ET.ParseError, subprocess.SubprocessError) as error:
        return {**result, "reason": str(error)}


def inside_root(path, root):
    resolved = (root / path).resolve()
    if not resolved.is_relative_to(root) or resolved == root or not resolved.is_file():
        raise ValueError(f"not an existing repository file: {path}")
    return resolved


def load_spec(path, root):
    spec = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(spec, list) or not spec:
        raise ValueError("spec must contain at least one mutation")
    seen = set()
    for row in spec:
        if not isinstance(row, dict) or any(not isinstance(row.get(key), str) or not row[key]
                                            for key in ("file", "name", "old")):
            raise ValueError("each mutation needs nonempty file, name and old strings")
        if not isinstance(row.get("new"), str) or row["old"] == row["new"]:
            raise ValueError("mutation must change its anchor")
        if row["name"] in seen:
            raise ValueError("duplicate mutation name")
        seen.add(row["name"])
        target = inside_root(row["file"], root)
        if target.read_bytes().count(row["old"].encode("utf-8")) != 1:
            raise ValueError(f"{row['name']}: anchor must match exactly once (including line endings)")
    return spec


def write_report(directory, report):
    (directory / "report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")


def execute(spec, project, test_filter, directory, timeout, prefix="", root=ROOT):
    revision = subprocess.run(["git", "rev-parse", "HEAD"], cwd=root,
                              capture_output=True, text=True, timeout=10)
    report = {"project": str(project.relative_to(root)), "filter": test_filter,
              "gitSha": revision.stdout.strip() if revision.returncode == 0 else None,
              "runnerSha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
              "environment": {key: os.environ.get(key) for key in (
                  "AION_SIM_DB_INTEGRATION", "AION_SIM_RUN_ID", "AION_SIM_SHARD",
                  "AION_SIM_SHARD_COUNT", "AION_SIM_PROCESS_KEY", "AION_SIM_TIER", "AION_SIM_SEED")},
              "status": "incomplete", "mutations": []}
    baseline = run_tests(project, test_filter, directory / "baseline", timeout, root)
    report["baseline"] = baseline
    write_report(directory, report)
    if baseline["status"] != "passed":
        report["status"] = "baseline-failed"
        write_report(directory, report)
        print(f"BASELINE NOT GREEN: {baseline}", flush=True)
        return 1
    print(f"baseline: {len(baseline['tests'])} tests passed; {len(spec)} mutations", flush=True)
    try:
        for index, row in enumerate(spec, 1):
            target = inside_root(row["file"], root)
            original = target.read_bytes()
            old = row["old"].encode("utf-8")
            if original.count(old) != 1:
                raise ValueError(f"{row['name']}: anchor changed since preflight")
            mutated = original.replace(old, row["new"].encode("utf-8"), 1)
            backup = directory / f"{index:03d}-original.bin"
            backup.write_bytes(original)
            entry = {"name": row["name"], "file": row["file"], "status": "incomplete",
                     "originalSha256": hashlib.sha256(original).hexdigest(),
                     "mutatedSha256": hashlib.sha256(mutated).hexdigest(), "restored": False}
            report["mutations"].append(entry)
            write_report(directory, report)
            try:
                target.write_bytes(mutated)
                result = run_tests(project, test_filter, directory / f"mutant-{index:03d}", timeout, root)
                entry.update(result)
                if result["status"] in {"passed", "failed"}:
                    if result["tests"] != baseline["tests"]:
                        entry.update(status="inconclusive", reason="test selection changed from baseline")
                    else:
                        entry["status"] = "caught" if result["failures"] else "survived"
            finally:
                current = target.read_bytes()
                if current not in (original, mutated):
                    raise RuntimeError(f"Concurrent edit preserved in {target}; original backup: {backup}. "
                                       "Stop and reconcile manually before building again.")
                target.write_bytes(original)
                entry["restored"] = target.read_bytes() == original
                write_report(directory, report)
                if not entry["restored"]:
                    raise RuntimeError(f"Source changed during restoration: {target}; inspect {backup}")
            failures = [name.removeprefix(prefix) for name in entry.get("failures", [])]
            print(f"{row['name']}: {entry['status']}" +
                  (f"; caught by: {', '.join(failures)}" if entry["status"] == "caught" else ""), flush=True)
        report["restoredBaseline"] = run_tests(project, test_filter, directory / "restored", timeout, root)
        restored = report["restoredBaseline"]
        passed = (all(row["status"] == "caught" and row["restored"] for row in report["mutations"])
                  and restored["status"] == "passed" and restored["tests"] == baseline["tests"])
        report["status"] = "passed" if passed else "failed"
        return 0 if passed else 1
    except BaseException as error:
        report.update(status="interrupted" if isinstance(error, KeyboardInterrupt) else "failed",
                      reason=str(error), recovery="Inspect source restoration flags/backups; rebuild before other tests.")
        raise
    finally:
        write_report(directory, report)
        print(f"Report: {directory / 'report.json'}", flush=True)


def positive_seconds(value):
    seconds = int(value)
    if seconds < 1:
        raise argparse.ArgumentTypeError("timeout must be positive")
    return seconds


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("spec", type=Path, help="JSON list of {file, name, old, new}")
    parser.add_argument("--filter", required=True, help="dotnet test --filter expression")
    parser.add_argument("--project", default=TEST_PROJECT, help="test csproj relative to repository")
    parser.add_argument("--name-prefix", default="", help="optional display-only prefix to strip")
    parser.add_argument("--results-directory", type=Path, help="new directory; never reuse old evidence")
    parser.add_argument("--timeout-seconds", type=positive_seconds, default=900,
                        help="deadline for each build/test process (default 900)")
    args = parser.parse_args(argv)
    try:
        project = inside_root(args.project, ROOT)
        if project.suffix != ".csproj" or not args.filter.strip():
            raise ValueError("select a .csproj and a nonempty test filter")
        spec = load_spec(args.spec, ROOT)
        directory = (args.results_directory or ROOT / "run" / f"mutations-{uuid.uuid4().hex[:12]}").resolve()
        directory.mkdir(parents=True, exist_ok=False)
        # Lock covers cooperating mutation runners, not arbitrary builds/editors.
        lock = ROOT / "run" / "mutation-runner.lock"
        lock.parent.mkdir(exist_ok=True)
        with lock.open("x", encoding="utf-8") as handle:
            handle.write(f"pid={os.getpid()}\nresults={directory}\n")
        try:
            (directory / "spec.json").write_text(json.dumps(spec, indent=2) + "\n", encoding="utf-8")
            return execute(spec, project, args.filter, directory, args.timeout_seconds, args.name_prefix)
        finally:
            lock.unlink()
    except KeyboardInterrupt:
        print("Interrupted. Inspect report/backups; rebuild before other tests.", file=sys.stderr)
        return 130
    except (OSError, ValueError, RuntimeError) as error:
        print(str(error), file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
