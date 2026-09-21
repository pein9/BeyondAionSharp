#!/usr/bin/env python3
"""Render retained runner evidence; never infer success from missing artifacts."""
import argparse
from collections import Counter
from datetime import datetime
import gzip
import hashlib
import json
import math
from pathlib import Path
import re
import sys
from packet_coverage import collect as collect_packets, compare as compare_packets

ID = re.compile(r"^[A-Za-z0-9][A-Za-z0-9_-]*$")
DIGEST = re.compile(r"^\S+ (NEW|KNOWN|REGRESSED) \S+ \S+ fp=([a-f0-9]{8})(?:\s|$)")
HEARTBEAT = re.compile(r"^Server heartbeat: connections=(\d+), packetQueueDepth=(\d+), armedTimers=(\d+), "
                       r"workingSetBytes=(\d+), lastGcHeapBytes=(\d+), lastGcIndex=(\d+), dispatcherWrites=")


def read_json(path):
    return decode(path.read_text(encoding="utf-8-sig"))


def decode(value):
    def reject_constant(constant):
        raise ValueError(f"invalid JSON number: {constant}")
    def finite_float(raw):
        value = float(raw)
        if not math.isfinite(value):
            raise ValueError(f"nonfinite JSON number: {raw}")
        return value
    return json.loads(value, parse_constant=reject_constant, parse_float=finite_float)


def timestamp(value):
    result = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if result.utcoffset() is None:
        raise ValueError("timestamp requires an offset")
    return result


def number(value):
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value) or value < 0:
        raise ValueError("duration/count must be a finite nonnegative number")
    return value


def identifier(value):
    if not isinstance(value, str) or not ID.fullmatch(value):
        raise ValueError(f"invalid identity: {value!r}")
    return value


def child_path(root, relative):
    path = root / relative
    if path.resolve() == root.resolve() or not path.resolve().is_relative_to(root.resolve()):
        raise ValueError(f"artifact escapes run directory: {relative}")
    return path


def lines(path):
    opener = gzip.open if path.suffix == ".gz" else open
    with opener(path, "rt", encoding="utf-8-sig") as source:
        for index, line in enumerate(source, 1):
            if not line.strip():
                continue
            try:
                yield decode(line)
            except ValueError as error:
                raise ValueError(f"{path.name}:{index}: {error}") from error


def scenario_outcomes(root, outcome, issues):
    planned = outcome["scenarios"]
    if not isinstance(planned, list) or not planned or len(set(planned)) != len(planned):
        raise ValueError("runner requires distinct planned scenarios")
    for scenario in planned:
        identifier(scenario)
    starts, terminals = {}, {}
    active = None
    path = child_path(root, "scenario-results.jsonl")
    if path.exists():
        for row in lines(path):
            scenario = row["scenario"]
            if row["schemaVersion"] != 1 or row["run"] != outcome["run"] or row["mode"] != outcome["mode"] or scenario not in planned:
                raise ValueError("scenario journal identity does not match runner plan")
            timestamp(row["timestampUtc"])
            timestamp(row["startedUtc"])
            if row["event"] == "started":
                if active is not None or scenario in starts or row["status"] != "running" or row["durationSeconds"] is not None:
                    raise ValueError("duplicate/interleaved/invalid scenario start")
                if planned[len(starts)] != scenario:
                    raise ValueError("scenario execution order disagrees with runner plan")
                starts[scenario] = row
                active = scenario
            elif row["event"] == "completed":
                if active != scenario or scenario in terminals or row["startedUtc"] != starts[scenario]["startedUtc"]:
                    raise ValueError("scenario completion without matching active start")
                if row["status"] not in ("passed", "failed"):
                    raise ValueError("unknown scenario terminal status")
                number(row["durationSeconds"])
                if row["status"] == "passed" and (type(row["exitCode"]) is not int or row["exitCode"] != 0 or row["error"] is not None):
                    raise ValueError("successful scenario contradicts failure evidence")
                if row["status"] == "failed" and not row["error"]:
                    raise ValueError("failed scenario requires a reason")
                terminals[scenario] = row
                active = None
            else:
                raise ValueError("unknown scenario journal event")
    result = []
    for scenario in planned:
        row = terminals.get(scenario)
        if row:
            status, duration, reason = row["status"], row["durationSeconds"], row["error"]
        elif scenario in starts:
            status, duration, reason = "failed", None, "Started without terminal evidence (interrupted/incomplete)."
        else:
            status, duration, reason = "skipped", None, "Not started; see runner failure or earlier scenario."
        if status != "passed" and outcome["status"] == "passed":
            issues.append(f"Runner claimed success without successful scenario {scenario}.")
        result.append(dict(id=scenario, mode=outcome["mode"], status=status, durationSeconds=duration, reason=reason,
                           evidence="scenario-results.jsonl" if path.exists() else None))
    return result


def live_observations(root, outcome, issues):
    summary_path = child_path(root, "logwatch-summary.json")
    if not summary_path.exists():
        if outcome["status"] == "passed":
            issues.append("Successful LIVE runner has no watcher summary.")
        return None, [], []
    summary = read_json(summary_path)
    if summary["run"] != outcome["run"]:
        raise ValueError("watcher belongs to another run")
    if not isinstance(summary["servers"], list) or not summary["servers"] or len(set(summary["servers"])) != len(summary["servers"]):
        raise ValueError("watcher requires distinct producers")
    if summary["mode"] != "enforce":
        issues.append("Watcher was not enforcing; this run cannot establish acceptance.")
    if type(summary["failed"]) is not bool:
        raise ValueError("watcher failure verdict must be a boolean")
    if summary["failed"]:
        issues.append("Enforced watcher failed.")
    fingerprints = {}
    digest_path = child_path(root, "digest.log")
    if digest_path.exists():
        for line in digest_path.read_text(encoding="utf-8-sig").splitlines():
            match = DIGEST.match(line)
            if not match:
                continue
            disposition, fp = match.groups()
            if fp in fingerprints:
                raise ValueError("duplicate first-occurrence fingerprint in digest")
            bundle = f"problems/{fp}"
            missing = []
            if disposition == "NEW":
                for name in ("metadata.json", "stack.txt", "server-context.log", "bot-trace.jsonl", "draft-backlog.md"):
                    if not child_path(root, f"{bundle}/{name}").is_file():
                        missing.append(name)
                if not missing:
                    metadata = read_json(child_path(root, f"{bundle}/metadata.json"))
                    if metadata["run"] != outcome["run"] or metadata["fingerprint"] != fp:
                        raise ValueError("repro bundle identity mismatch")
                else:
                    issues.append(f"NEW {fp} missing repro files: {', '.join(missing)}")
            fingerprints[fp] = dict(fingerprint=fp, disposition=disposition,
                                    repro=bundle if disposition == "NEW" else None, missingReproFiles=missing)
    counts = Counter(row["disposition"].lower() for row in fingerprints.values())
    for kind in ("new", "known", "regressed"):
        if number(summary[kind]) != counts[kind]:
            issues.append(f"Watcher {kind} count disagrees with retained digest.")
    # KNOWN is a recorded classification, not an additional failure policy.
    # The watcher permits ordinary tracked problems but still fails known
    # heartbeat/fault-window problems via summary.failed. Preserve that verdict.
    if any(row["disposition"] in ("NEW", "REGRESSED") for row in fingerprints.values()):
        issues.append("NEW or REGRESSED watcher fingerprints remain.")
    peaks = []
    for server in summary["servers"]:
        identifier(server)
        samples = []
        directory = child_path(root, f"logs/{server}")
        for path in sorted(directory.glob("*.events*.jsonl*")):
            child_path(root, path.relative_to(root))
            for row in lines(path):
                if row.get("cat") != "Aion.Commons.Diagnostics.ServerHeartbeatService":
                    continue
                message = row.get("msg", "")
                if not message.startswith("Server heartbeat:"):
                    continue
                if row.get("run") != outcome["run"] or row.get("srv") != server:
                    raise ValueError("heartbeat belongs to another run/server")
                match = HEARTBEAT.match(message)
                if not match:
                    raise ValueError("unrecognized heartbeat metrics")
                samples.append((timestamp(row["ts"]), [int(value) for value in match.groups()]))
        samples.sort(key=lambda row: row[0])
        gaps = [(b[0] - a[0]).total_seconds() for a, b in zip(samples, samples[1:])]
        if not samples and outcome["status"] == "passed":
            issues.append(f"Successful LIVE run has no {server} heartbeat samples.")
        peaks.append(dict(server=server, samples=len(samples), maximumObservedGapSeconds=max(gaps) if gaps else None,
                          peakConnections=max(s[1][0] for s in samples) if samples else None,
                          peakPacketQueueDepth=max(s[1][1] for s in samples) if samples else None,
                          peakArmedTimers=max(s[1][2] for s in samples) if samples else None,
                          peakWorkingSetBytes=max(s[1][3] for s in samples) if samples else None,
                          peakLastGcHeapBytes=max(s[1][4] for s in samples) if samples else None))
    return summary, list(fingerprints.values()), peaks


def coverage(root):
    result = []
    # Use only comparisons retained with this run, never today's mutable baseline.
    for name in ("quest-coverage.json", "coverage.json", "packet-coverage-comparison.json"):
        path = child_path(root, name)
        if path.exists():
            result.append(dict(evidence=name, comparison=read_json(path)))
    return result


def sim_observations(root, outcome, report):
    """Validate retained policy decisions against frozen inputs, not today's ledger."""
    issues = report["evidenceIssues"]
    rows = iter(lines(child_path(root, "sim-problems.jsonl")))
    header = next(rows, {})
    if (header.get("event") != "run-started" or header["schemaVersion"] != 1 or
            header["run"] != outcome["run"] or header["mode"] != "SIM" or
            header["gitSha"] != outcome["gitSha"] or type(header["seed"]) is not int or header["seed"] != outcome["seed"]):
        raise ValueError("SIM problem export identity/provenance mismatch")
    started = timestamp(header["startedUtc"])
    identifier(header["profile"])
    snapshots = {}
    for name in ("ledger", "allowlist"):
        path = child_path(root, f"sim-{name}-at-start.json")
        if hashlib.sha256(path.read_bytes()).hexdigest() != header[f"{name}Sha256"]:
            raise ValueError(f"SIM {name} snapshot hash mismatch")
        snapshot = read_json(path)
        if not isinstance(snapshot, list):
            raise ValueError(f"SIM {name} snapshot must be an array")
        entries = {}
        for entry in snapshot:
            fp = entry["fp"]
            if not isinstance(fp, str) or not re.fullmatch("[a-f0-9]{8}", fp) or fp in entries:
                raise ValueError(f"SIM {name} fingerprint invalid/duplicated")
            if name == "ledger" and entry["status"] not in ("new", "tracked", "fixed"):
                raise ValueError("SIM ledger status invalid")
            if name == "allowlist":
                for field in ("reason", "owner", "tracking"):
                    if not isinstance(entry[field], str) or not entry[field].strip():
                        raise ValueError(f"SIM allowance requires {field}")
                if (type(entry["maxCount"]) is not int or entry["maxCount"] <= 0 or
                        datetime.fromisoformat(entry["expires"]).date() < started.date()):
                    raise ValueError("SIM allowance expired or has invalid count")
                for field in ("modes", "servers", "scenarios"):
                    values = entry.get(field)
                    if field == "scenarios" and values is None:
                        continue
                    if not isinstance(values, list) or not values or any(not isinstance(v, str) or not v.strip() for v in values):
                        raise ValueError(f"SIM allowance requires {field}")
            entries[fp] = entry
        snapshots[name] = entries
    active, completed, seen = {}, {}, {}
    started_policies = 0
    footer = None
    for row in rows:
        if row["run"] != outcome["run"] or footer is not None:
            raise ValueError("SIM policy export identity/order mismatch")
        event = row["event"]
        if event == "run-completed":
            if (type(row["policiesStarted"]) is not int or type(row["policiesCompleted"]) is not int or
                    row["policiesStarted"] != started_policies or row["policiesCompleted"] != len(completed) or
                    not isinstance(row["activePolicies"], list) or any(type(p) is not int for p in row["activePolicies"]) or
                    row["activePolicies"] != list(active)):
                raise ValueError("SIM policy footer disagrees with observations")
            footer = row
            continue
        scenario, policy = row["scenario"], row["policy"]
        if scenario not in outcome["scenarios"] or type(policy) is not int or policy <= 0:
            raise ValueError("SIM policy identity invalid")
        if event == "policy-started":
            if policy != started_policies + 1:
                raise ValueError("SIM policy sequence duplicated or incomplete")
            active[policy] = scenario
            started_policies += 1
            continue
        if event != "policy-completed" or active.get(policy) != scenario:
            raise ValueError("SIM policy completion without matching start")
        del active[policy]
        completed[policy] = scenario
        number(row["virtualMillis"])
        if type(row["assertedClean"]) is not bool or type(row["assertionPassed"]) is not bool:
            raise ValueError("SIM policy assertion verdict must be boolean")
        if not row["assertedClean"] or not row["assertionPassed"]:
            issues.append(f"SIM {scenario} policy {policy} did not pass its clean-log assertion.")
        counts = Counter()
        if not isinstance(row["observations"], list):
            raise ValueError("SIM observations must be an array")
        for observation in row["observations"]:
            fp = observation["fingerprint"]
            if not isinstance(fp, str) or not re.fullmatch("[a-f0-9]{8}", fp):
                raise ValueError("SIM observation fingerprint invalid")
            counts[fp] += 1
            allowance = snapshots["allowlist"].get(fp)
            allowed = bool(allowance and "sim" in [m.lower() for m in allowance["modes"]] and
                           observation["server"].lower() in [s.lower() for s in allowance["servers"]] and
                           (allowance.get("scenarios") is None or scenario in allowance["scenarios"]) and counts[fp] <= allowance["maxCount"])
            disposition = "ALLOWLISTED" if allowed else {"tracked": "KNOWN", "fixed": "REGRESSED"}.get(
                snapshots["ledger"].get(fp, {}).get("status"), "NEW")
            if type(observation["allowlisted"]) is not bool or observation["allowlisted"] != allowed or observation["disposition"] != disposition:
                raise ValueError("SIM observation contradicts frozen classification/allowance")
            key = (fp, disposition)
            if key not in seen:
                bundle = f"problems/{fp}" if disposition == "NEW" else None
                item = dict(fingerprint=fp, disposition=disposition, repro=bundle, missingReproFiles=[], policyObservations=0)
                seen[key] = item
                report["fingerprints"].append(item)
                if not allowed:  # Unlike LIVE, SIM never exempts ordinary KNOWN problems.
                    issues.append(f"SIM {scenario} has unallowlisted {disposition} fingerprint {fp}.")
                if bundle:
                    for name in ("metadata.json", "stack.txt", "server-context.log", "bot-trace.jsonl", "draft-backlog.md"):
                        if not child_path(root, f"{bundle}/{name}").is_file():
                            item["missingReproFiles"].append(name)
                    if item["missingReproFiles"]:
                        issues.append(f"NEW {fp} missing repro files: {', '.join(item['missingReproFiles'])}")
                    else:
                        metadata = read_json(child_path(root, f"{bundle}/metadata.json"))
                        if any(metadata.get(k) != v for k, v in dict(run=outcome["run"], mode="SIM", scenario=scenario,
                                fingerprint=fp, seed=header["seed"], gitSha=header["gitSha"], configProfile=header["profile"]).items()):
                            raise ValueError("SIM repro provenance mismatch")
            seen[key]["policyObservations"] += 1
    if footer is None or active:
        issues.append("SIM export has no complete policy footer or has unfinished policy scopes.")
    for scenario in report["scenarios"]:
        if scenario["status"] == "passed" and scenario["id"] not in completed.values():
            issues.append(f"SIM passed scenario {scenario['id']} has no completed log policy.")
    report["simulation"] = dict(evidence="sim-problems.jsonl", policiesStarted=started_policies,
        policiesCompleted=len(completed), activePolicies=list(active), provenance=header)


def planned_rows(root, outcome):
    if outcome["mode"] != "FULL":
        return [dict(id=identifier(scenario), mode=outcome["mode"]) for scenario in outcome["scenarios"]]
    result = []
    plan = read_json(child_path(root, "suite-plan.json"))
    if plan["run"] != outcome["run"]:
        raise ValueError("suite plan identity mismatch")
    for step in plan["steps"]:
        identifier(step["id"])
        if step["kind"] not in ("Sim", "Live", "Soak"):
            continue
        mode = "SIM" if step["kind"] == "Sim" else "LIVE"
        scenarios = step["scenarios"] if mode == "SIM" else [step.get("scenario", "SOAK")]
        relative = step["id"] if mode == "SIM" else outcome["run"] + "-" + (step["scenario"].lower() if step["kind"] == "Live" else step["id"])
        child_path(root, relative)
        result.extend(dict(id=identifier(scenario), mode=mode, child=relative) for scenario in scenarios)
    return result


def sim_resources(root, outcome, report):
    path = child_path(root, "sim-resources.jsonl")
    rows = iter(lines(path))
    header = next(rows, {})
    if (header.get("event") != "run-started" or type(header["schemaVersion"]) is not int or header["schemaVersion"] != 1 or
            any(header[k] != outcome[k] for k in ("run", "mode", "seed", "gitSha")) or
            type(header["seed"]) is not int or type(outcome["seed"]) is not int or
            type(header["processId"]) is not int or header["processId"] <= 0 or
            header["sampling"] != "run-policy-and-bot-action-boundaries" or header["scope"] != "post-bootstrap-test-process" or
            header["timerSource"] != "VirtualThreadPool.ArmedTimerCount" or header["heartbeat"] != "not-applicable"):
        raise ValueError("SIM resource identity/provenance/sampling contract mismatch")
    timestamp(header["startedUtc"])
    identifier(header["profile"])
    if report["simulation"] and header["profile"] != report["simulation"]["provenance"]["profile"]:
        raise ValueError("SIM resource profile disagrees with problem export")
    summary = dict(mode="SIM", processId=header["processId"], scope=header["scope"], sampling=header["sampling"],
        evidence="sim-resources.jsonl", evidenceSha256=hashlib.sha256(path.read_bytes()).hexdigest(), complete=False,
        attempts=0, samples=0, errors=0, maximumObservedWallGapSeconds=None, maximumObservedVirtualGapMillis=None,
        peakWorkingSetBytes=None, processLifetimePeakWorkingSetBytes=None, peakLastGcHeapBytes=None,
        peakArmedTimers=None, firstVirtualMillis=None, lastVirtualMillis=None, lastGcIndex=None)
    # Keep validated observations available even if a later row is corrupt or truncated.
    report["resourcePeaks"].append(summary)
    starts, ends, active = {}, {}, {}
    footer, terminal, previous = None, False, None
    for row in rows:
        if row["run"] != outcome["run"] or footer is not None:
            raise ValueError("SIM resource stream identity/order mismatch")
        event = row["event"]
        if event == "run-completed":
            if not terminal or any(type(row[k]) is not int or row[k] != summary[k] for k in ("attempts", "samples", "errors")):
                raise ValueError("SIM resource footer disagrees with observations")
            footer = row
            continue
        if event not in ("sample", "sample-error") or terminal:
            raise ValueError("SIM resource event out of order")
        if type(row["sequence"]) is not int or row["sequence"] != summary["attempts"] + 1:
            raise ValueError("SIM resource sample sequence incomplete/duplicated")
        elapsed = number(row["elapsedSeconds"])
        vt = row["virtualMillis"]
        if type(vt) is not int or vt < 0:
            raise ValueError("SIM resource virtual time invalid")
        timestamp(row["timestampUtc"])
        if previous:
            if elapsed < previous[0] or vt < previous[1]:
                raise ValueError("SIM resource elapsed/virtual time moved backwards")
            summary["maximumObservedWallGapSeconds"] = max(summary["maximumObservedWallGapSeconds"] or 0, elapsed - previous[0])
            summary["maximumObservedVirtualGapMillis"] = max(summary["maximumObservedVirtualGapMillis"] or 0, vt - previous[1])
        else:
            summary["firstVirtualMillis"] = vt
        previous = (elapsed, vt)
        summary["lastVirtualMillis"] = vt
        trigger, policy, scenario = row["trigger"], row["policy"], row["scenario"]
        if (summary["attempts"] == 0) != (trigger == "run-started"):
            raise ValueError("SIM resource stream requires exactly one initial sample")
        if trigger in ("run-started", "run-completed"):
            if any(row[k] is not None for k in ("policy", "scenario", "bot", "account", "step")):
                raise ValueError("SIM run-boundary sample has scenario identity")
            terminal = trigger == "run-completed"
        elif trigger in ("policy-started", "policy-completed"):
            if type(policy) is not int or policy <= 0 or scenario not in outcome["scenarios"] or any(row[k] is not None for k in ("bot", "account", "step")):
                raise ValueError("SIM resource policy identity invalid")
            if trigger == "policy-started":
                if policy != len(starts) + 1:
                    raise ValueError("SIM resource policy start duplicated/incomplete")
                starts[policy] = active[policy] = scenario
            else:
                if active.pop(policy, None) != scenario:
                    raise ValueError("SIM resource policy completion without matching start")
                ends[policy] = scenario
        elif trigger == "bot-action":
            if policy is not None or scenario not in active.values() or any(not isinstance(row[k], str) or not row[k].strip() for k in ("bot", "account", "step")):
                raise ValueError("SIM resource bot action has no active scenario/identity")
        else:
            raise ValueError("Unknown SIM resource sample trigger")
        if event == "sample-error":
            if row["metrics"] is not None or row["armedTimers"] is not None or not isinstance(row["error"], str) or not row["error"].strip():
                raise ValueError("SIM failed resource sample fabricated counters or lost its error")
            summary["errors"] += 1
        else:
            metrics = row["metrics"]
            for field in ("workingSetBytes", "processLifetimePeakWorkingSetBytes", "lastGcIndex"):
                if type(metrics[field]) is not int or metrics[field] < (0 if field == "lastGcIndex" else 1):
                    raise ValueError("Invalid SIM resource counter")
            heap, index, timers = metrics["lastGcHeapBytes"], metrics["lastGcIndex"], row["armedTimers"]
            if ((index == 0) != (heap is None) or heap is not None and (type(heap) is not int or heap < 0) or
                    type(timers) is not int or timers < 0 or row["error"] is not None or
                    summary["lastGcIndex"] is not None and index < summary["lastGcIndex"]):
                raise ValueError("Invalid SIM collection availability/timer/error counters")
            for target, value in (("peakWorkingSetBytes", metrics["workingSetBytes"]),
                    ("processLifetimePeakWorkingSetBytes", metrics["processLifetimePeakWorkingSetBytes"]),
                    ("peakLastGcHeapBytes", heap), ("peakArmedTimers", timers)):
                if value is not None:
                    summary[target] = max(summary[target] or 0, value)
            summary["lastGcIndex"] = index
            summary["samples"] += 1
        summary["attempts"] += 1
    if footer is None or active:
        report["evidenceIssues"].append("SIM resource export lacks terminal evidence or has unfinished policy scopes.")
    if summary["errors"]:
        report["evidenceIssues"].append(f"SIM resource sampling failed {summary['errors']} time(s); missing counters are not zero.")
    if not summary["samples"]:
        report["evidenceIssues"].append("SIM has no successful resource samples.")
    # Join every scope to the independent problem receipts, not just a total count.
    expected_starts, expected_ends = {}, {}
    for row in lines(child_path(root, "sim-problems.jsonl")):
        if row["event"] == "policy-started": expected_starts[row["policy"]] = row["scenario"]
        if row["event"] == "policy-completed": expected_ends[row["policy"]] = row["scenario"]
    if starts != expected_starts or ends != expected_ends:
        raise ValueError("SIM resource policy scopes disagree with retained problem receipts")
    summary["complete"] = footer is not None and not active


def build_report(root):
    root = root.resolve()
    issues = []
    expected = []
    report = dict(schemaVersion=1, run=root.name, mode=None, status="failed", runner=None,
                  scenarios=[], steps=[], fingerprints=[], watcher=None, simulation=None, heartbeatPeaks=[], resourcePeaks=[], packetCoverage=[], coverage=[], evidenceIssues=issues,
                  runnerSourceSha256=None, limitations=["Scenario completion alone does not prove watcher, cleanup, coverage or capacity acceptance.",
                               "Heartbeat peaks are observed samples, not a continuous resource maximum."])
    try:
        outcome = read_json(child_path(root, "runner-result.json"))
        report["runnerSourceSha256"] = hashlib.sha256((root / "runner-result.json").read_bytes()).hexdigest()
        if outcome["schemaVersion"] != 1 or outcome["mode"] not in ("SIM", "LIVE", "FULL") or outcome["status"] not in ("passed", "failed"):
            raise ValueError("invalid terminal runner result")
        identifier(outcome["run"])
        number(outcome["durationSeconds"])
        timestamp(outcome["startedUtc"])
        timestamp(outcome["finishedUtc"])
        report.update(run=outcome["run"], mode=outcome["mode"], runner=outcome)
        expected = planned_rows(root, outcome)
        if not expected:
            raise ValueError("run has no planned scenarios")
        if outcome["mode"] == "FULL":
            collect_suite(root, outcome, report)
        else:
            report["scenarios"] = scenario_outcomes(root, outcome, issues)
            if child_path(root, "packet-catalog.json").exists():
                try:
                    report["packetCoverage"].append(collect_packets(root, outcome))
                except (OSError, EOFError, ValueError, KeyError, TypeError, IndexError, AttributeError) as error:
                    issues.append(f"Invalid packet coverage evidence: {error}")
            else:
                report["limitations"].append("No frozen packet catalog; opcode coverage is unavailable for this run.")
            if outcome["mode"] == "LIVE":
                report["watcher"], report["fingerprints"], report["heartbeatPeaks"] = live_observations(root, outcome, issues)
            else:
                report["limitations"].extend([
                    "SIM policy observation counts can overlap in nested scopes.",
                    "SIM resources are boundary samples of the whole test process after bootstrap, not server-only or continuous maxima. The OS lifetime peak can predate the first sample.",
                    "SIM omits hosted heartbeats; resource sample gaps are not liveness checks. Last-GC heap is unavailable until a natural collection; sampling never forces GC."])
                for collect in (sim_observations, sim_resources):
                    try:
                        collect(root, outcome, report)
                    except (OSError, EOFError, ValueError, KeyError, TypeError, IndexError, AttributeError) as error:
                        issues.append(f"Invalid or missing SIM evidence: {error}")
        report["coverage"].extend(coverage(root))
        for item in report["coverage"]:
            if item["comparison"].get("baselineComparison", {}).get("passed") is False:
                issues.append(f"Coverage comparison failed: {item['evidence']}.")
        if not report["coverage"]:
            report["limitations"].append("No retained coverage comparison for this run; coverage deltas are unavailable.")
        if outcome["status"] == "failed":
            issues.append(outcome.get("error") or "Runner failed without a recorded exception.")
        if any(row["status"] != "passed" for row in report["scenarios"]):
            issues.append("Not all planned scenarios passed.")
        if not issues:
            report["status"] = "passed"
    except (OSError, EOFError, ValueError, KeyError, TypeError, IndexError, AttributeError) as error:
        issues.append(f"Invalid or missing evidence: {error}")
    # A corrupt child/gate/journal must not hide the rest of the selected matrix.
    keys = {(row["id"], row["mode"], row.get("child")) for row in report["scenarios"]}
    for row in expected:
        if (row["id"], row["mode"], row.get("child")) not in keys:
            report["scenarios"].append(dict(row, status="skipped", durationSeconds=None, evidence=None,
                reason="No validated outcome; see evidence error (execution is not credited)."))
    report["counts"] = {status: sum(row["status"] == status for row in report["scenarios"])
                        for status in ("passed", "failed", "skipped", "flaky")}
    return report


def collect_suite(root, outcome, report):
    plan = read_json(child_path(root, "suite-plan.json"))
    if plan["run"] != outcome["run"]:
        raise ValueError("suite plan identity mismatch")
    steps = plan["steps"]
    ids = [identifier(step["id"]) for step in steps]
    if not ids or len(ids) != len(set(ids)):
        raise ValueError("suite plan must contain distinct steps")
    results_path = child_path(root, "suite-results.jsonl")
    results = list(lines(results_path)) if results_path.exists() else []
    if [row["id"] for row in results] != ids[:len(results)] or len(results) > len(ids):
        raise ValueError("suite outcomes are not an execution prefix")
    for index, step in enumerate(steps):
        if step["kind"] not in ("Sim", "Live", "Soak", "QuestCoverage", "PacketParity", "PacketCoverage"):
            raise ValueError("unknown suite step kind")
        result = results[index] if index < len(results) else None
        status = result["status"].lower() if result else "skipped"
        if status not in ("passed", "failed", "skipped") or result and result["kind"] != step["kind"]:
            raise ValueError("invalid suite outcome")
        duration = number(result["durationSeconds"]) if result else None
        report["steps"].append(dict(id=step["id"], kind=step["kind"], status=status, durationSeconds=duration,
                                    error=result.get("error") if result else "Not reached (fail-fast)."))
        if status != "passed":
            report["evidenceIssues"].append(f"Suite step {step['id']}: {status}.")
        if status == "passed" and step["kind"] in ("QuestCoverage", "PacketParity", "PacketCoverage"):
            name = {"QuestCoverage": "quest-coverage.json", "PacketParity": "l0-packet-parity.json", "PacketCoverage": "packet-coverage-comparison.json"}[step["kind"]]
            gate = read_json(child_path(root, name))
            passed = gate.get("baselineComparison", {}).get("passed") is True if step["kind"] != "PacketParity" else gate.get("status") == "passed"
            if not passed:
                report["evidenceIssues"].append(f"Suite gate {step['id']} contradicts its retained evidence.")
            if step["kind"] == "PacketCoverage":
                baseline_path = child_path(root, "packet-coverage-baseline.json")
                if gate.get("run") != outcome["run"] or gate.get("baselineSourceSha256") != hashlib.sha256(baseline_path.read_bytes()).hexdigest():
                    raise ValueError("Packet coverage baseline provenance mismatch")
                comparison = compare_packets(report["packetCoverage"], read_json(baseline_path))
                if not comparison["passed"]:
                    report["evidenceIssues"].append("Packet coverage no longer satisfies retained baseline: " + "; ".join(comparison["errors"]))
        if step["kind"] not in ("Sim", "Live", "Soak"):
            continue
        scenario_ids = step["scenarios"] if step["kind"] == "Sim" else [step.get("scenario", "SOAK")]
        for scenario in scenario_ids:
            identifier(scenario)
        mode = "SIM" if step["kind"] == "Sim" else "LIVE"
        relative = step["id"] if mode == "SIM" else outcome["run"] + "-" + (step["scenario"].lower() if step["kind"] == "Live" else step["id"])
        directory = child_path(root, relative)
        if step["kind"] == "Soak" and status == "passed":
            acceptance = read_json(child_path(directory, "soak-acceptance.json"))
            if acceptance.get("status") != "passed" or acceptance.get("overallSoakAccepted") is not True:
                report["evidenceIssues"].append(f"Soak step {step['id']} lacks capacity acceptance.")
        if result and directory.is_dir():
            child = build_report(directory)
            expected_run = outcome["run"] if mode == "SIM" else relative
            if child["run"] != expected_run or child["mode"] != mode or [row["id"] for row in child["scenarios"]] != scenario_ids:
                report["evidenceIssues"].append(f"Child {relative} evidence is incomplete or mismatches its plan.")
                child = None
        else:
            child = None
        if child:
            report["scenarios"].extend(dict(row, child=relative) for row in child["scenarios"])
            report["fingerprints"].extend(dict(row, child=relative,
                repro=f"{relative}/{row['repro']}" if row["repro"] else None) for row in child["fingerprints"])
            report["heartbeatPeaks"].extend(dict(row, child=relative) for row in child["heartbeatPeaks"])
            report["resourcePeaks"].extend(dict(row, child=relative, evidence=f"{relative}/{row['evidence']}") for row in child["resourcePeaks"])
            report["packetCoverage"].extend(dict(row, child=relative) for row in child["packetCoverage"])
            report["coverage"].extend(dict(row, evidence=f"{relative}/{row['evidence']}") for row in child["coverage"])
            report["limitations"].extend(f"{relative}: {value}" for value in child["limitations"] if value not in report["limitations"][:2])
            if child["status"] != "passed":
                report["evidenceIssues"].append(f"Child {relative} failed: " + "; ".join(child["evidenceIssues"]))
        else:
            report["scenarios"].extend(dict(id=scenario, mode=mode, status="skipped", durationSeconds=None,
                reason="No valid child outcome; not credited as executed.", evidence=None, child=relative) for scenario in scenario_ids)
            if status == "passed":
                report["evidenceIssues"].append(f"Passed step {step['id']} lacks valid child evidence.")


def markdown(report):
    def cell(value):
        return str(value).replace("|", "\\|").replace("\r", " ").replace("\n", " ").replace("<", "&lt;").replace(">", "&gt;")
    rows = [f"# Run {cell(report['run'])}", "", f"Status: **{report['status'].upper()}**. Counts: " +
            ", ".join(f"{count} {status}" for status, count in report["counts"].items()), "",
            "## Scenarios", "", "| Scenario | Mode | Status | Seconds | Reason |", "|---|---|---|---:|---|"]
    for row in report["scenarios"]:
        duration = "unavailable" if row["durationSeconds"] is None else f"{row['durationSeconds']:.3f}"
        rows.append(f"| {cell(row['id'])} | {row['mode']} | {row['status']} | {duration} | {cell(row.get('reason') or '')} |")
    rows += ["", "## Game packet observations", "", "| Mode / child | Client send attempts | Server received | Structured decoded | Raw received | Tap serialized / dropped |", "|---|---:|---:|---:|---:|---:|"]
    for packet in report["packetCoverage"]:
        def ratio(key):
            metric = packet["metrics"][key]
            return f"{metric['count']}/{metric['total']}"
        tap = f"{ratio('serverTapped')} / {packet['tap']['droppedRecords']}" if packet["tap"]["available"] else "unavailable"
        rows.append(f"| {cell(packet['mode'] + ' / ' + packet.get('child', packet['run']))} | {ratio('clientSent')} | {ratio('serverReceived')} | {ratio('serverDecoded')} | {ratio('serverRaw')} | {tap} |")
    rows += ["", "Distinct game opcodes, not packet volume or behavior coverage. Raw/tapped packets do not count as structured decoded. Frozen catalogs and input hashes are retained in report.json.", "", "## Problems and repros", ""]
    for row in report["fingerprints"]:
        suffix = f" — [repro bundle]({row['repro']}/metadata.json)" if row["repro"] else ""
        rows.append(f"- {row['disposition']} `{row['fingerprint']}`{suffix}")
    if not report["fingerprints"]:
        rows.append("No classified fingerprints retained. See evidence limitations; this is not proof of log coverage.")
    rows += ["", "## Observed heartbeat peaks", "", "| Producer | Samples | Gap (s) | Working set (bytes) | Timers | Queue |", "|---|---:|---:|---:|---:|---:|"]
    for row in report["heartbeatPeaks"]:
        rows.append("| " + " | ".join(cell(row.get(key) if row.get(key) is not None else "unavailable") for key in
                    ("server", "samples", "maximumObservedGapSeconds", "peakWorkingSetBytes", "peakArmedTimers", "peakPacketQueueDepth")) + " |")
    if report["resourcePeaks"]:
        rows += ["", "## SIM resource samples", "", "Boundary samples of the test process, not heartbeats or continuous maxima.", "",
                 "| Evidence | Samples / errors | Complete | Working set peak (bytes) | Last-GC heap peak (bytes) | Timers peak |",
                 "|---|---:|---|---:|---:|---:|"]
        for item in report["resourcePeaks"]:
            metrics = ["unavailable" if item[k] is None else str(item[k]) for k in ("peakWorkingSetBytes", "peakLastGcHeapBytes", "peakArmedTimers")]
            rows.append(f"| [{cell(item['evidence'])}]({item['evidence']}) | {item['samples']} / {item['errors']} | {item['complete']} | " + " | ".join(metrics) + " |")
        rows += ["", "Process-lifetime OS peaks and observed wall/virtual sample gaps are retained separately in report.json. The lifetime peak may include bootstrap."]
    rows += ["", "## Coverage comparisons", ""]
    rows.extend(f"- [{item['evidence']}]({item['evidence']}) (full retained comparison in report.json)" for item in report["coverage"])
    if not report["coverage"]:
        rows.append("Unavailable; no delta is inferred.")
    rows += ["", "## Evidence issues", ""]
    rows.extend("- " + cell(issue) for issue in report["evidenceIssues"])
    if not report["evidenceIssues"]:
        rows.append("None recorded.")
    rows += ["", "## Limits", ""] + ["- " + cell(limit) for limit in report["limitations"]]
    return "\n".join(rows) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run_directory", type=Path)
    args = parser.parse_args()
    root = args.run_directory.resolve()
    if not root.is_dir():
        parser.error("run directory does not exist")
    report = build_report(root)
    for name in ("report.json", "report.md"):
        child_path(root, name)
    (root / "report.json").write_text(json.dumps(report, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    (root / "report.md").write_text(markdown(report), encoding="utf-8")
    print(f"Run {report['run']}: {report['status'].upper()}; " + ", ".join(f"{n} {s}" for s, n in report["counts"].items()))
    print(f"Report: {root / 'report.md'}")
    for row in report["fingerprints"]:
        if row["disposition"] == "NEW":
            print(f"NEW {row['fingerprint']} repro: {root / row['repro']}")
    return 0 if report["status"] == "passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
