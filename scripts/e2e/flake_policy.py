"""P10-12 evidence and rolling-history contracts; no runner is retried on import.

Call observe with the raw-evidence report builder, not cached report.json. Ledger
updates consume these verified observations. Wiring/persistence belongs to the
Full runner; standalone diagnostics must not advance the ten-Full-run window.
"""
import copy
from datetime import datetime, timedelta, timezone
import re

from packet_coverage import evidence, rows, read


def identifier(value):
    if not isinstance(value, str) or not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_-]*", value):
        raise ValueError("Invalid flake identity")
    return value


def instant(value):
    if not isinstance(value, str):
        raise ValueError("Flake timestamp must be a timezone-qualified string")
    parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if parsed.tzinfo is None:
        raise ValueError("Flake timestamp requires timezone")
    return parsed.astimezone(timezone.utc)


def observe(root, base_run, scenario, attempt_count, build_report):
    """Revalidate one or two isolated LIVE attempts and retain both trace receipts.

    No failure is recovered when evidence is absent, stale or from another seed/
    revision/scenario. A failed watcher/cleanup report counts as a failed attempt
    even when its scenario journal itself passed.
    """
    identifier(base_run)
    identifier(scenario)
    if type(attempt_count) is not int or attempt_count not in (1, 2):
        raise ValueError("LIVE allows exactly one initial attempt and at most one retry")
    attempts = []
    identity = None
    for number in range(1, attempt_count + 1):
        name = base_run if number == 1 else base_run + "-retry1"
        directory = root / name
        if not directory.resolve().is_relative_to(root.resolve()):
            raise ValueError("Attempt escaped Full run root")
        report = build_report(directory)
        runner = report["runner"]
        if (report["run"] != name or report["mode"] != "LIVE" or report["status"] not in ("passed", "failed") or
                [row["id"] for row in report["scenarios"]] != [scenario] or runner is None or
                runner["run"] != name or runner["mode"] != "LIVE" or runner["scenarios"] != [scenario] or
                type(runner["seed"]) is not int or not runner["gitSha"]):
            raise ValueError("Attempt identity, scenario or terminal evidence mismatch")
        current = (runner["gitSha"], runner["seed"])
        if identity is not None and current != identity:
            raise ValueError("Retry changed revision or seed")
        identity = current
        receipts = [evidence(root, directory / "runner-result.json"),
                    evidence(root, directory / "scenario-results.jsonl")]
        if receipts[0]["sha256"] != report["runnerSourceSha256"]:
            raise ValueError("Attempt report is stale")
        traces, names = [], set()
        for path in sorted((directory / "bots").glob("*.trace.jsonl*")):
            if not path.name.endswith((".trace.jsonl", ".trace.jsonl.gz")):
                continue
            logical = path.name.removesuffix(".gz")
            if logical in names:
                raise ValueError("Duplicate compressed and uncompressed retry trace")
            names.add(logical)
            trace = evidence(root, path)
            count = 0
            for row in rows(path):
                if row.get("run") != name:
                    raise ValueError("Retry trace identity mismatch")
                count += 1
            if count == 0:
                raise ValueError("Empty retry trace")
            traces.append(dict(trace, observations=count))
        if not traces:
            raise ValueError("Missing retained attempt traces; cannot establish a flake")
        attempts.append(dict(number=number, child=name, status=report["status"],
            durationSeconds=runner["durationSeconds"], evidence=receipts, traces=traces,
            evidenceIssues=report["evidenceIssues"]))
    statuses = [row["status"] for row in attempts]
    if len(statuses) == 2 and statuses[0] != "failed":
        raise ValueError("A passing LIVE attempt must not be retried")
    status = "flaky" if statuses == ["failed", "passed"] else statuses[-1]
    return dict(scenario=scenario, mode="LIVE", status=status, gitSha=identity[0], seed=identity[1], attempts=attempts)


def validate(ledger):
    if ledger["schemaVersion"] != 1 or ledger["window"] != 10 or ledger["threshold"] != 3:
        raise ValueError("Unexpected flake policy; three flakes in ten Full runs is required")
    if not isinstance(ledger["owner"], str) or not ledger["owner"].strip():
        raise ValueError("Flake policy requires an owner")
    if type(ledger["expiryDays"]) is not int or not 1 <= ledger["expiryDays"] <= 30:
        raise ValueError("Flake expiry must be bounded to 1..30 days")
    runs = ledger["fullRuns"]
    if not isinstance(runs, list) or len({identifier(row["run"]) for row in runs}) != len(runs):
        raise ValueError("Duplicate or invalid Full-run history")
    previous = None
    for run in runs:
        stamp = instant(run["finishedUtc"])
        if previous is not None and stamp < previous:
            raise ValueError("Full-run history must be ordered by completion")
        previous = stamp
        if not re.fullmatch(r"[a-f0-9]{64}", run["reportSha256"]):
            raise ValueError("Full history requires its terminal report hash")
        if run["suite"] not in ("Breadth", "All", "Soak"):
            raise ValueError("Only Full invocations advance the flake window")
        scenarios = run["scenarios"]
        if len({identifier(s["scenario"]) for s in scenarios}) != len(scenarios):
            raise ValueError("A Full run must count a scenario only once")
        for scenario in scenarios:
            if scenario["status"] not in ("passed", "failed", "skipped", "flaky"):
                raise ValueError("Invalid historical scenario status")
            if scenario["status"] == "flaky":
                if not scenario["owner"].strip() or instant(scenario["expiresUtc"]) <= stamp:
                    raise ValueError("Every flake needs an owner and future expiry at recording time")
                attempts = scenario["attempts"]
                if ([a["status"] for a in attempts] != ["failed", "passed"] or
                        len({a["child"] for a in attempts}) != 2 or
                        any(not a["traces"] or not a["evidence"] for a in attempts)):
                    raise ValueError("Flake history must retain both distinct attempts and traces")
    seen = set()
    for quarantine in ledger["quarantines"]:
        scenario = identifier(quarantine["scenario"])
        if scenario in seen or not quarantine["owner"].strip() or not quarantine["reason"].strip():
            raise ValueError("Quarantine requires a distinct scenario, owner and reason")
        seen.add(scenario)
        if instant(quarantine["expiresUtc"]) <= instant(quarantine["createdUtc"]):
            raise ValueError("Quarantine expiry must follow creation")
        triggers = quarantine["triggerRuns"]
        if len(triggers) < 3 or len(set(triggers)) != len(triggers):
            raise ValueError("Quarantine requires three distinct triggering Full runs")
        history = {row["run"]: row for row in runs}
        if any(run not in history or not any(s["scenario"] == scenario and s["status"] == "flaky"
                for s in history[run]["scenarios"]) for run in triggers):
            raise ValueError("Quarantine triggers lack historical flakes")
    return ledger


def record_full(ledger, run, suite, finished_utc, report_sha256, observations):
    """Pure transition, applied only after Full reporting; retries are not extra runs.

    An identical replay is idempotent. Changed receipts for an existing run are
    rejected. All completed Full invocations, including failed/empty ones, count
    toward the window. SIM rows must never be supplied as LIVE observations.
    """
    validate(ledger)
    result = copy.deepcopy(ledger)
    stamp = instant(finished_utc)
    expiry = (stamp + timedelta(days=ledger["expiryDays"])).isoformat()
    scenarios = []
    for observation in observations:
        if observation["mode"] != "LIVE":
            raise ValueError("SIM failures are determinism bugs, never flake ledger entries")
        row = dict(scenario=observation["scenario"], status=observation["status"])
        if row["status"] == "flaky":
            row.update(owner=ledger["owner"], expiresUtc=expiry, attempts=copy.deepcopy(observation["attempts"]))
        scenarios.append(row)
    entry = dict(run=run, suite=suite, finishedUtc=finished_utc, reportSha256=report_sha256, scenarios=scenarios)
    existing = next((row for row in result["fullRuns"] if row["run"] == run), None)
    if existing is not None:
        if existing != entry:
            raise ValueError("Full-run history cannot be rewritten")
        return result
    result["fullRuns"].append(entry)
    validate(result)
    window = result["fullRuns"][-10:]
    quarantined = {row["scenario"] for row in result["quarantines"]}
    candidates = {s["scenario"] for row in window for s in row["scenarios"] if s["status"] == "flaky"}
    for scenario in sorted(candidates - quarantined):
        triggers = [row["run"] for row in window if any(s["scenario"] == scenario and s["status"] == "flaky" for s in row["scenarios"])]
        if len(triggers) >= 3:
            result["quarantines"].append(dict(scenario=scenario, owner=ledger["owner"],
                reason="At least three recovered LIVE failures in the last ten completed Full runs; investigation required.",
                createdUtc=stamp.isoformat(), expiresUtc=expiry, triggerRuns=triggers))
    return validate(result)


def admission(ledger, scenario, now_utc):
    """Quarantine never becomes a silent pass or silently expires into admission."""
    validate(ledger)
    identifier(scenario)
    now = instant(now_utc)
    quarantine = next((q for q in ledger["quarantines"] if q["scenario"] == scenario), None)
    if quarantine is None:
        return dict(allowed=True, quarantine=None)
    return dict(allowed=False, quarantine=copy.deepcopy(quarantine),
                expired=instant(quarantine["expiresUtc"]) <= now,
                reason="Quarantined scenario requires maintainer review; do not report as passed.")


def full_observation(root, plan, step, result, build_report):
    """Join a Full step's controller receipt to freshly validated child evidence."""
    if step["kind"] not in ("Live", "Soak") or plan.get("retryPolicyVersion") != 1:
        raise ValueError("Not a retry-enabled Full LIVE step")
    full_admission(root, plan, step, require_allowed=True)
    base = plan["run"] + "-" + (step["scenario"].lower() if step["kind"] == "Live" else step["id"])
    identifier(step["id"])
    path = root / "live-attempts" / (step["id"] + ".json")
    source = evidence(root, path)
    receipt = read(path)
    attempts = receipt["attempts"]
    journal_path = path.with_suffix(".jsonl")
    journal_source = evidence(root, journal_path)
    if list(rows(journal_path)) != attempts:
        raise ValueError("Original terminal-attempt journal disagrees with retry receipt")
    if (receipt["run"] != base or receipt["mode"] != "LIVE" or receipt["status"] != result["status"].lower() or
            result["attemptReceipt"] != source["path"] or result["attemptReceiptSha256"] != source["sha256"] or
            not attempts or len(attempts) > 2):
        raise ValueError("Full retry receipt disagrees with terminal step")
    for number, attempt in enumerate(attempts, 1):
        expected = base if number == 1 else base + "-retry1"
        if attempt["number"] != number or attempt["run"] != expected or attempt["status"] not in ("passed", "failed"):
            raise ValueError("Invalid Full retry sequence")
    if len(attempts) == 2:
        guard_path = root / "live-attempts" / (step["id"] + ".retry-admission.json")
        guard = read(guard_path)
        if (guard["priorRun"] != base or guard["retryRun"] != base + "-retry1" or
                guard["passed"] is not True or guard["containers"] != []):
            raise ValueError("Missing successful fresh-stack retry admission")
        instant(guard["checkedUtc"])
        guard_source = evidence(root, guard_path)
    else:
        guard_source = None
    observed = observe(root, base, step.get("scenario", "SOAK"), len(attempts), build_report)
    if (observed["status"] != receipt["status"] or
            [a["status"] for a in observed["attempts"]] != [a["status"] for a in attempts] or
            observed["seed"] != plan["seed"] or observed["gitSha"] != plan["gitSha"]):
        raise ValueError("Full attempt outcome/revision/seed disagrees with raw evidence")
    observed.update(step=step["id"], receipt=source, attemptJournal=journal_source, retryAdmission=guard_source)
    return observed


def full_admission(root, plan, step, require_allowed=False):
    snapshot = evidence(root, root / "flaky-at-start.json")
    if snapshot["sha256"] != plan["flakeLedgerSha256"]:
        raise ValueError("Full flake ledger snapshot mismatch")
    ledger = validate(read(root / "flaky-at-start.json"))
    decision = read(root / "live-admission" / (identifier(step["id"]) + ".json"))
    scenario = step.get("scenario", "SOAK")
    expected = admission(ledger, scenario, decision["checkedUtc"])
    if (decision["step"] != step["id"] or decision["scenario"] != scenario or
            decision["ledgerSha256"] != snapshot["sha256"] or
            any(decision.get(k) != v for k, v in expected.items())):
        raise ValueError("Full scenario admission contradicts its frozen policy")
    if require_allowed and expected["allowed"] is not True:
        raise ValueError("Quarantined scenario was executed")
    return decision


def selected_child(root, plan, step, result, build_report):
    """Select the accepted attempt for comparisons, never the failed one's traffic."""
    relative = step["id"] if step["kind"] == "Sim" else plan["run"] + "-" + (
        step["scenario"].lower() if step["kind"] == "Live" else step["id"])
    if step["kind"] in ("Live", "Soak") and plan.get("retryPolicyVersion") == 1:
        observed = full_observation(root, plan, step, result, build_report)
        if observed["status"] not in ("passed", "flaky"):
            raise ValueError("No accepted LIVE attempt available for comparison")
        return observed["attempts"][-1]["child"]
    if result["status"].lower() != "passed":
        raise ValueError("Child did not pass")
    return relative
