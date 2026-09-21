#!/usr/bin/env python3
"""Freeze/admit/verify Full LIVE attempts and atomically retain verified Full history."""
import argparse
from contextlib import contextmanager
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import sys
import tempfile

import flake_policy as policy
from packet_coverage import read

DEFAULT_LEDGER = Path(__file__).resolve().parents[2] / "parity-artifacts/e2e/flaky.json"
spec = importlib.util.spec_from_file_location("flake_reporter", Path(__file__).with_name("report-run.py"))
reporter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(reporter)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_new(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("x", encoding="utf-8") as stream:
        json.dump(value, stream, indent=2, allow_nan=False)
        stream.write("\n")


def frozen(root, plan):
    path = root / "flaky-at-start.json"
    if plan.get("retryPolicyVersion") != 1 or sha(path) != plan["flakeLedgerSha256"]:
        raise ValueError("Full flake policy snapshot mismatch")
    return policy.validate(read(path))


@contextmanager
def ledger_lock(path):
    # Kernel locks release on process exit; an abandoned lock file is not authority.
    with path.with_suffix(".json.lock").open("a+b") as stream:
        if stream.seek(0, 2) == 0:
            stream.write(b"0")
            stream.flush()
        stream.seek(0)
        if os.name == "nt":
            import msvcrt
            msvcrt.locking(stream.fileno(), msvcrt.LK_NBLCK, 1)
        else:
            import fcntl
            fcntl.flock(stream.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
        try:
            yield
        finally:
            stream.seek(0)
            if os.name == "nt":
                msvcrt.locking(stream.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                fcntl.flock(stream.fileno(), fcntl.LOCK_UN)


def atomic_write(path, value):
    fd, temporary = tempfile.mkstemp(prefix=".flaky-", suffix=".tmp", dir=path.parent)
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as stream:
            json.dump(value, stream, indent=2, allow_nan=False)
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def record(root, ledger_path):
    plan = read(root / "suite-plan.json")
    frozen(root, plan)
    retained = read(root / "report.json")
    fresh = reporter.build_report(root)
    if retained != fresh or fresh["mode"] != "FULL" or fresh["run"] != plan["run"]:
        raise ValueError("Full report is stale or not this terminal Full invocation")
    runner = fresh["runner"]
    if runner is None or runner["status"] not in ("passed", "failed"):
        raise ValueError("Missing terminal Full runner receipt")
    # Several SOAK populations in one Full invocation still earn at most one flake.
    observations = {}
    for row in fresh["scenarios"]:
        if row["mode"] == "LIVE":
            observations.setdefault(row["id"], dict(scenario=row["id"], mode="LIVE", status=row["status"]))
    for observation in fresh.get("liveAttempts", []):
        if observation["status"] == "flaky":
            observations[observation["scenario"]] = observation
    report_hash = sha(root / "report.json")
    receipt_path = root / "flake-history.json"
    if receipt_path.exists():
        existing = read(receipt_path)
        if existing["run"] != plan["run"] or existing["reportSha256"] != report_hash:
            raise ValueError("Existing history receipt conflicts with Full result")
    with ledger_lock(ledger_path):
        before = sha(ledger_path)
        ledger = policy.validate(read(ledger_path))
        updated = policy.record_full(ledger, plan["run"], plan["suite"], runner["finishedUtc"],
                                     report_hash, list(observations.values()))
        if updated != ledger:
            atomic_write(ledger_path, updated)
        after = sha(ledger_path)
    receipt = dict(schemaVersion=1, run=plan["run"], reportSha256=report_hash,
                   ledgerBeforeSha256=before, ledgerAfterSha256=after)
    if not receipt_path.exists():
        write_new(receipt_path, receipt)
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("snapshot", "admit", "verify", "select", "record"))
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--ledger", type=Path, default=DEFAULT_LEDGER)
    parser.add_argument("--step")
    args = parser.parse_args()
    root = args.root.resolve()
    try:
        if args.action == "snapshot":
            with ledger_lock(args.ledger):
                write_new(root / "flaky-at-start.json", policy.validate(read(args.ledger)))
            return 0
        if args.action == "record":
            record(root, args.ledger)
            return 0
        plan = read(root / "suite-plan.json")
        ledger = frozen(root, plan)
        policy.identifier(args.step)
        matches = [s for s in plan["steps"] if s["id"] == args.step and s["kind"] in ("Live", "Soak")]
        if len(matches) != 1:
            raise ValueError("Expected one planned LIVE step")
        step = matches[0]
        if args.action == "admit":
            stamp = datetime.now(timezone.utc).isoformat()
            value = policy.admission(ledger, step.get("scenario", "SOAK"), stamp)
            value.update(step=step["id"], scenario=step.get("scenario", "SOAK"), checkedUtc=stamp,
                         ledgerSha256=plan["flakeLedgerSha256"])
            write_new(root / "live-admission" / (step["id"] + ".json"), value)
            return 0 if value["allowed"] else 1
        # verify runs inside dispatch before the terminal suite step is appended.
        path = root / "live-attempts" / (step["id"] + ".json")
        receipt = read(path)
        result = dict(status=receipt["status"], attemptReceipt=path.relative_to(root).as_posix(),
                      attemptReceiptSha256=sha(path))
        observation = policy.full_observation(root, plan, step, result, reporter.build_report)
        if observation["status"] not in ("passed", "flaky"):
            raise ValueError("LIVE attempt did not recover")
        if args.action == "select":
            print(observation["attempts"][-1]["child"])
        return 0
    except (OSError, EOFError, ValueError, KeyError, TypeError, IndexError, AttributeError) as error:
        print(f"Flake policy rejected {args.action}: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
