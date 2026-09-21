#!/usr/bin/env python3
"""Validate planned Full breadth children, then compare packet identities with a reviewed floor."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import sys

from packet_coverage import compare, read
from flake_policy import selected_child

spec = importlib.util.spec_from_file_location("report_run", Path(__file__).with_name("report-run.py"))
reporter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(reporter)
DEFAULT_BASELINE = Path(__file__).resolve().parents[2] / "parity-artifacts/e2e/packet-coverage-baseline.json"


def build(root, baseline):
    plan = read(root / "suite-plan.json")
    if plan["suite"] not in ("Breadth", "All"):
        raise ValueError("Packet coverage gate requires the breadth plan")
    results = list(reporter.lines(root / "suite-results.jsonl"))
    ids = [step["id"] for step in plan["steps"]]
    if not ids or len(ids) != len(set(ids)) or [row["id"] for row in results] != ids[:len(results)] or len(results) > len(ids):
        raise ValueError("Suite outcomes do not match planned execution prefix")
    by_id = {row["id"]: row for row in results}
    measurements, issues, children = [], [], []
    for step in plan["steps"]:
        if step["kind"] not in ("Sim", "Live"):
            continue
        result = by_id.get(step["id"])
        if not result or result["status"].lower() not in ("passed", "flaky") or result["kind"] != step["kind"]:
            issues.append(f"Breadth step {step['id']} did not pass")
            continue
        mode = "SIM" if step["kind"] == "Sim" else "LIVE"
        relative = selected_child(root, plan, step, result, reporter.build_report)
        directory = reporter.child_path(root, relative)
        child = reporter.build_report(directory)
        expected_run = plan["run"] if mode == "SIM" else relative
        scenarios = step["scenarios"] if mode == "SIM" else [step["scenario"]]
        children.append(dict(directory=relative, status=child["status"], runnerSourceSha256=child["runnerSourceSha256"]))
        if (child["run"] != expected_run or child["mode"] != mode or child["status"] != "passed" or
                [row["id"] for row in child["scenarios"]] != scenarios or len(child["packetCoverage"]) != 1):
            issues.append(f"Breadth child {relative} failed acceptance, identity or packet evidence validation")
            continue
        measurements.append(dict(child["packetCoverage"][0], child=relative))
    comparison = compare(measurements, baseline)
    comparison["errors"].extend(issues)
    comparison["passed"] = not comparison["errors"]
    return dict(schemaVersion=1, run=plan["run"], children=children, measurements=measurements, baselineComparison=comparison)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run-root", type=Path, required=True)
    parser.add_argument("--baseline", type=Path, default=DEFAULT_BASELINE)
    args = parser.parse_args()
    root = args.run_root.resolve()
    output = reporter.child_path(root, "packet-coverage-comparison.json")
    snapshot = reporter.child_path(root, "packet-coverage-baseline.json")
    # Never silently rebase or overwrite the inputs of an earlier comparison.
    with snapshot.open("xb") as stream:
        stream.write(args.baseline.read_bytes())
    try:
        result = build(root, read(snapshot))
    except (OSError, EOFError, ValueError, KeyError, TypeError, IndexError, AttributeError) as error:
        result = dict(schemaVersion=1, baselineComparison=dict(passed=False, errors=[str(error)]))
    result["baselineSourceSha256"] = hashlib.sha256(snapshot.read_bytes()).hexdigest()
    with output.open("x", encoding="utf-8") as stream:
        json.dump(result, stream, indent=2, allow_nan=False)
        stream.write("\n")
    print(f"Packet coverage: {'PASS' if result['baselineComparison']['passed'] else 'FAIL'}; {output}")
    for error in result["baselineComparison"]["errors"]:
        print(error, file=sys.stderr)
    return 0 if result["baselineComparison"]["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
