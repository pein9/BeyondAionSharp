#!/usr/bin/env python3
"""Read-only aggregate Full evidence gate. A watcher/retention flag is not authority."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import sys

from packet_coverage import read

REPO = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("promotion_reporter", Path(__file__).with_name("report-run.py"))
reporter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(reporter)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def expected_plan(root):
    result = subprocess.run(["pwsh", "-NoProfile", "-File", str(Path(__file__).with_name("full-promotion-plan.ps1")),
                             "-PlanPath", str(root / "suite-plan.json")], capture_output=True, text=True, check=True)
    return json.loads(result.stdout)


def current_revision():
    return subprocess.run(["git", "-C", str(REPO), "rev-parse", "HEAD"],
                          capture_output=True, text=True, check=True).stdout.strip()


def validate(root):
    retained = read(root / "report.json")
    fresh = reporter.build_report(root)
    if retained != fresh or fresh["mode"] != "FULL" or fresh["status"] != "passed" or fresh["evidenceIssues"]:
        raise ValueError("Promotion requires a fresh, clean aggregate Full report (not failed, FLAKY or standalone)")
    plan = read(root / "suite-plan.json")
    runner = fresh["runner"]
    revision = current_revision()
    if (not re.fullmatch(r"[0-9a-f]{40}", revision) or
            plan["gitSha"] != revision or runner["gitSha"] != revision or plan["seed"] != runner["seed"] or
            plan["run"] != fresh["run"] or runner["status"] != "passed"):
        raise ValueError("Full plan/runner/current revision identity mismatch")
    if plan.get("retryPolicyVersion") != 1 or plan["suite"] not in ("Breadth", "All") or plan["steps"] != expected_plan(root):
        raise ValueError("Promotion requires the complete current breadth plan, including all gates")
    history = read(root / "flake-history.json")
    report_hash = sha(root / "report.json")
    if history["schemaVersion"] != 1 or history["run"] != fresh["run"] or history["reportSha256"] != report_hash:
        raise ValueError("Missing or stale terminal Full history receipt")
    # The report already rebuilds child journals, watcher/trace receipts, retry admission
    # and gates. Also pin SIM children (not just LIVE retries) to the aggregate build/seed.
    children = {row.get("selectedChild", row["child"]) for row in fresh["scenarios"]}
    fingerprints = {row["fingerprint"] for row in fresh["fingerprints"]}
    for child in children:
        directory = reporter.child_path(root, child)
        child_runner = read(directory / "runner-result.json")
        if child_runner["gitSha"] != revision or child_runner["seed"] != runner["seed"]:
            raise ValueError("Full child revision/seed mismatch")
        if child_runner["mode"] == "LIVE":
            # LIVE's classified report omits suppressed findings. Suppression does
            # not establish absence: an allowlisted occurrence must also prevent fixing.
            digest = reporter.child_path(directory, "digest.log").read_text(encoding="utf-8-sig")
            fingerprints.update(re.findall(r"^\S+ ALLOWLISTED \S+ \S+ fp=([a-f0-9]{8})(?:\s|$)", digest, re.MULTILINE))
    if any(not re.fullmatch(r"[0-9a-f]{8}", fp) for fp in fingerprints):
        raise ValueError("Invalid aggregate fingerprint")
    return dict(schemaVersion=1, status="passed", run=fresh["run"], gitSha=revision,
                reportSha256=report_hash, seenFingerprints=sorted(fingerprints))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", type=Path)
    args = parser.parse_args()
    try:
        print(json.dumps(validate(args.root.resolve())))
        return 0
    except (OSError, EOFError, ValueError, KeyError, TypeError, IndexError, AttributeError, subprocess.SubprocessError) as error:
        print(f"Full promotion evidence rejected: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
