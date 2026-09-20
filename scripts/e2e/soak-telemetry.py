#!/usr/bin/env python3
"""P10-02 heartbeat evidence gate; passing this alone never accepts a soak."""
import argparse
from datetime import datetime, timedelta
import hashlib
import json
import math
from pathlib import Path
import re
import statistics
import sys


BUCKETS_MS = (0.1, 0.5, 1, 2, 5, 10, 20, 50, 100, 250, 500, 1000, 5000, math.inf)
POLICY = {
    "id": "p10-02-telemetry-v1",
    "minimumSeconds": 7200,
    "maximumHeartbeatGapSeconds": 30,
    "warmupSeconds": 1800,
    "plateauWindowSeconds": 900,
    "memoryGrowthBytes": 64 * 1024 * 1024,
    "memoryGrowthFraction": 0.05,
    "timerGrowthCount": 10,
    "timerGrowthFraction": 0.02,
    "dispatchP99UpperMilliseconds": 100,
    "dispatchP999UpperMilliseconds": 1000,
    "dispatchMaximumMilliseconds": 5000,
    "oldestPendingMilliseconds": 1000,
}
HEARTBEAT = re.compile(
    r"Server heartbeat: connections=(\d+), packetQueueDepth=(\d+), armedTimers=(\d+), "
    r"workingSetBytes=(\d+), managedHeapBytes=(\d+), dispatcherWrites=(.*)")


def timestamp(value):
    result = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if result.utcoffset() is None:
        raise ValueError("Telemetry timestamps require an explicit UTC offset")
    return result


def nonnegative(value, label, integer=False):
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value) or value < 0:
        raise ValueError(f"Invalid {label}: expected a finite nonnegative number")
    if integer and not isinstance(value, int):
        raise ValueError(f"Invalid {label}: expected an integer")
    return value


def validate_dispatch(value):
    if not isinstance(value, dict):
        raise ValueError("Game heartbeat needs dispatcher observations")
    for key in ("Count", "Abandoned", "PendingConnections"):
        nonnegative(value[key], key, integer=True)
    for key in ("MeanMilliseconds", "MaximumMilliseconds", "OldestPendingMilliseconds"):
        nonnegative(value[key], key)
    buckets = value["Buckets"]
    if not isinstance(buckets, list) or len(buckets) != len(BUCKETS_MS):
        raise ValueError("Dispatcher histogram shape changed")
    for count in buckets:
        nonnegative(count, "bucket", integer=True)
    if sum(buckets) != value["Count"]:
        raise ValueError("Dispatcher sample count does not match histogram")
    if value["MeanMilliseconds"] > value["MaximumMilliseconds"]:
        raise ValueError("Dispatcher mean exceeds maximum")
    if value["Count"] == 0 and (value["MeanMilliseconds"] or value["MaximumMilliseconds"]):
        raise ValueError("Empty dispatcher window has latency observations")
    if value["PendingConnections"] == 0 and value["OldestPendingMilliseconds"] != 0:
        raise ValueError("Pending age without a pending connection")
    if value["Count"]:
        last = max(i for i, count in enumerate(buckets) if count)
        maximum = value["MaximumMilliseconds"]
        if maximum > BUCKETS_MS[last] or (last > 0 and maximum <= BUCKETS_MS[last - 1]):
            raise ValueError("Dispatcher maximum contradicts histogram")
    return value


def read_samples(path, service, run):
    samples = []
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for number, raw in enumerate(source, 1):
            digest.update(raw)
            try:
                row = json.loads(raw)
                if not isinstance(row, dict):
                    raise ValueError("Event must be a JSON object")
                if row.get("cat") != "Aion.Commons.Diagnostics.ServerHeartbeatService":
                    continue
                if row.get("run") != run or row.get("srv") != service:
                    raise ValueError("Heartbeat belongs to another run/server")
                match = HEARTBEAT.fullmatch(row["msg"])
                if match is None:
                    raise ValueError("Missing or changed heartbeat metrics")
                connections, queue, timers, working, managed = map(int, match.groups()[:5])
                if working == 0 or managed == 0:
                    raise ValueError("Heartbeat is missing process-memory observations")
                dispatch = json.loads(match.group(6))
                if service == "gs":
                    dispatch = validate_dispatch(dispatch)
                elif dispatch is not None:
                    raise ValueError("Unexpected dispatcher metrics outside game server")
                sample = {"time": timestamp(row["ts"]), "connections": connections, "queue": queue,
                          "timers": timers, "workingSetBytes": working, "managedHeapBytes": managed,
                          "dispatch": dispatch}
                if samples and sample["time"] <= samples[-1]["time"]:
                    raise ValueError("Duplicate or nonmonotonic heartbeat; possible process restart")
                samples.append(sample)
            except (ValueError, KeyError, TypeError) as error:
                raise ValueError(f"{path}:{number}: {error}") from error
    return samples, digest.hexdigest()


def quantile_upper(buckets, fraction):
    total = sum(buckets)
    if total == 0:
        return None
    target = math.ceil(total * fraction)
    count = 0
    for upper, value in zip(BUCKETS_MS, buckets):
        count += value
        if count >= target:
            return upper if math.isfinite(upper) else "unbounded"
    raise AssertionError("Invalid histogram")


def plateau(samples, start, end, metric, absolute, fraction, failures):
    beginning = start + timedelta(seconds=POLICY["warmupSeconds"])
    width = POLICY["plateauWindowSeconds"]
    windows = []
    cursor = beginning
    while cursor + timedelta(seconds=width) <= end:
        values = [sample[metric] for sample in samples if cursor <= sample["time"] < cursor + timedelta(seconds=width)]
        minimum_samples = math.ceil(width / POLICY["maximumHeartbeatGapSeconds"])
        if len(values) < minimum_samples:
            failures.append(f"{metric}: insufficient plateau-window samples at {cursor.isoformat()}")
        windows.append({"start": cursor.isoformat(), "samples": len(values),
                        "median": statistics.median(values) if values else None})
        cursor += timedelta(seconds=width)
    if len(windows) < 6 or any(window["median"] is None for window in windows):
        failures.append(f"{metric}: fewer than six complete post-warmup plateau windows")
        return {"windows": windows, "status": "insufficient"}
    values = [window["median"] for window in windows]
    limit = max(absolute, values[0] * fraction)
    center = (len(values) - 1) / 2
    slope = sum((i - center) * value for i, value in enumerate(values)) / sum((i - center) ** 2 for i in range(len(values)))
    fitted_growth = max(0, slope * (len(values) - 1))
    endpoint_growth = max(0, values[-1] - values[0])
    passed = max(fitted_growth, endpoint_growth) <= limit
    if not passed:
        failures.append(f"{metric}: plateau growth exceeds {limit:g}")
    return {"windows": windows, "limit": limit, "endpointGrowth": endpoint_growth,
            "fittedGrowth": fitted_growth, "status": "passed" if passed else "failed"}


def analyze_service(samples, service, start, end):
    failures = []
    selected = [sample for sample in samples if start <= sample["time"] <= end]
    if not selected:
        return {"status": "failed", "failures": ["No heartbeat samples in workload window"]}
    times = [start] + [sample["time"] for sample in selected] + [end]
    gap = max((b - a).total_seconds() for a, b in zip(times, times[1:]))
    if gap > POLICY["maximumHeartbeatGapSeconds"]:
        failures.append(f"Heartbeat gap {gap:g}s exceeds policy (including window edges)")
    plateaus = {}
    for metric in ("workingSetBytes", "managedHeapBytes", "timers"):
        memory = metric != "timers"
        plateaus[metric] = plateau(selected, start, end, metric,
            POLICY["memoryGrowthBytes" if memory else "timerGrowthCount"],
            POLICY["memoryGrowthFraction" if memory else "timerGrowthFraction"], failures)
    result = {"samples": len(selected), "maximumHeartbeatGapSeconds": gap,
              "peaks": {metric: max(sample[metric] for sample in selected) for metric in
                        ("connections", "queue", "timers", "workingSetBytes", "managedHeapBytes")},
              "plateaus": plateaus}
    if service == "gs":
        # Samples drain disjoint windows. Exclude the first sample's batch because
        # its previous boundary is outside the requested workload interval.
        complete = [sample["dispatch"] for sample in selected[1:]]
        buckets = [sum(value["Buckets"][i] for value in complete) for i in range(len(BUCKETS_MS))]
        count = sum(buckets)
        p99, p999 = quantile_upper(buckets, 0.99), quantile_upper(buckets, 0.999)
        maximum = max((value["MaximumMilliseconds"] for value in complete), default=0)
        oldest = max(sample["dispatch"]["OldestPendingMilliseconds"] for sample in selected)
        for label, observed, limit in (
            ("dispatch p99 upper bound", p99, POLICY["dispatchP99UpperMilliseconds"]),
            ("dispatch p99.9 upper bound", p999, POLICY["dispatchP999UpperMilliseconds"]),
            ("dispatch maximum", maximum, POLICY["dispatchMaximumMilliseconds"]),
            ("pending write age", oldest, POLICY["oldestPendingMilliseconds"])):
            if observed is None or observed == "unbounded" or observed > limit:
                failures.append(f"{label}: {observed} exceeds/does not establish {limit}ms")
        result["dispatcher"] = {"samples": count, "buckets": buckets,
            "meanMilliseconds": sum(value["MeanMilliseconds"] * value["Count"] for value in complete) / count if count else None,
            "maximumMilliseconds": maximum, "p99UpperMilliseconds": p99, "p999UpperMilliseconds": p999,
            "abandoned": sum(value["Abandoned"] for value in complete),
            "peakPendingConnections": max(sample["dispatch"]["PendingConnections"] for sample in selected),
            "oldestPendingMilliseconds": oldest}
    result.update(status="failed" if failures else "passed", failures=failures)
    return result


def analyze(run_directory, start, end):
    if end <= start:
        raise ValueError("Workload window must have positive duration")
    metadata = json.loads((run_directory / "bots-run.json").read_text(encoding="utf-8-sig"))
    if not isinstance(metadata.get("run"), str) or not metadata["run"]:
        raise ValueError("Missing run provenance")
    servers, sources = {}, {}
    for service in ("ls", "cs", "gs"):
        path = run_directory / "logs" / service / f"{service}.events.jsonl"
        samples, digest = read_samples(path, service, metadata["run"])
        servers[service] = analyze_service(samples, service, start, end)
        sources[str(path.relative_to(run_directory))] = digest
    duration = (end - start).total_seconds()
    failures = [] if duration >= POLICY["minimumSeconds"] else ["Workload window is shorter than two hours"]
    passed = not failures and all(server["status"] == "passed" for server in servers.values())
    return {"schemaVersion": 1, "run": metadata["run"], "gitSha": metadata.get("gitSha"),
            "seed": metadata.get("seed"), "policy": POLICY, "sourceSha256": sources,
            "startedUtc": start.isoformat(), "endedUtc": end.isoformat(), "seconds": duration,
            "telemetryStatus": "passed" if passed else "failed", "failures": failures, "servers": servers,
            "overallSoakAccepted": False,
            "scope": "Telemetry gate only: workload, population, persistence, errors and economic statistics require separate validation. "
                     "Dispatcher latency is request-to-buffer-preparation, not socket flush or client RTT. "
                     "Sampled plateaus do not prove the absence of all memory leaks."}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run_directory", type=Path)
    parser.add_argument("--start", required=True, type=timestamp)
    parser.add_argument("--end", required=True, type=timestamp)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    try:
        result = analyze(args.run_directory, args.start, args.end)
    except (OSError, ValueError, KeyError, TypeError) as error:
        # A failed rerun must not leave a previous green report at the output path.
        result = {"schemaVersion": 1, "telemetryStatus": "failed", "overallSoakAccepted": False,
                  "failures": [f"Invalid soak telemetry evidence: {error}"]}
        args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
        print(result["failures"][0], file=sys.stderr)
        return 2
    args.output.write_text(json.dumps(result, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    print(f"Telemetry {result['telemetryStatus']}; this is not overall soak acceptance. {args.output}")
    return 0 if result["telemetryStatus"] == "passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
