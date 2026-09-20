#!/usr/bin/env python3
"""Aggregate P10-02 soak gates. A component's diagnostic success is never sufficient."""
import argparse
from collections import Counter
from datetime import date, datetime, timezone
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import sys

spec = importlib.util.spec_from_file_location("soak_telemetry", Path(__file__).with_name("soak-telemetry.py"))
telemetry = importlib.util.module_from_spec(spec)
spec.loader.exec_module(telemetry)
POLICY = "p10-02-acceptance-v1"


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def analyze(directory, allowlist_path, today=None):
    directory = directory.resolve()
    today = today or datetime.now(timezone.utc).date()
    failures, sources, gates = [], {}, {}
    result = {"schemaVersion": 1, "policy": POLICY, "overallSoakAccepted": False,
              "status": "failed", "failures": failures, "gates": gates, "sourceSha256": sources}

    def require(condition, message):
        if not condition:
            raise ValueError(message)

    def read(name):
        path = directory / name
        raw = path.read_bytes()
        sources[name] = hashlib.sha256(raw).hexdigest()
        return json.loads(raw)

    try:
        metadata = read("bots-run.json")
        run, sha = metadata["run"], metadata["gitSha"]
        result.update(run=run, gitSha=sha, seed=metadata["seed"], bots=metadata["bots"])
        execution = read("soak-execution.json")
        require(execution["schemaVersion"] == 1 and execution["run"] == run and
                execution["bots"] == metadata["bots"] and execution["seed"] == metadata["seed"] and
                execution["seconds"] == metadata["soakSeconds"] and execution["success"] is True and
                execution["failure"] is None, "The owning LIVE invocation did not complete successfully")
        gates["runner"] = "passed"
        workload = read("soak-workload.json")
        require(workload["Policy"] == "p10-02-workload-v1" and workload["Run"] == run and workload["GitSha"] == sha and
                workload["Status"] == "passed" and workload["Failures"] == [] and workload["CapacityConfiguration"] is True and
                workload["OverallSoakAccepted"] is False, "Workload evidence does not establish the complete capacity configuration")
        require(metadata["bots"] in (50, 200, 500) and metadata["soakSeconds"] == 7200, "Unsupported population or duration")
        expected_sources = {"bots-run.json", "soak-window.json", "soak-runtime.json", "soak-economy.json", "bots/gm.trace.jsonl"}
        expected_sources.update(f"bots/b{index:02}.trace.jsonl" for index in range(1, metadata["bots"] + 1))
        require(set(workload["SourceSha256"]) == expected_sources, "Incomplete workload source-hash inventory")
        for name, recorded in workload["SourceSha256"].items():
            require(re.fullmatch(r"[0-9a-f]{64}", recorded) is not None and digest(directory / name) == recorded,
                    f"Workload source changed since validation: {name}")
            sources[name] = recorded
        require(len(workload["Subjects"]) == metadata["bots"] and
                {subject["Bot"] for subject in workload["Subjects"]} == {f"b{i:02}" for i in range(1, metadata["bots"] + 1)},
                "Workload subject inventory is incomplete")
        gates["workload"] = "passed"
        economy = workload["RecomputedEconomy"]
        require(economy["Policy"] == "p10-02-economy-v1" and economy["Status"] == "passed" and economy["ImpossibleOutcomes"] == 0 and
                economy["OverallSoakAccepted"] is False and len(economy["Tests"]) == 4 and
                all(test["Status"] == "passed" for test in economy["Tests"]), "Economic exposure/statistical gate did not pass")
        gates["economy"] = "passed"
        observed = telemetry.analyze_recorded_window(directory)
        result["telemetry"] = observed
        for name, recorded in observed["sourceSha256"].items():
            require(name not in sources or sources[name] == recorded, f"Evidence changed during aggregation: {name}")
            sources[name] = recorded
        window = read("soak-window.json")
        require(telemetry.timestamp(execution["startedUtc"]) <= telemetry.timestamp(window["StartedUtc"]) and
                telemetry.timestamp(execution["completedUtc"]) >= telemetry.timestamp(window["CompletedUtc"]),
                "Invocation lifetime does not contain the workload")
        require(observed["telemetryStatus"] == "passed", "Two-hour telemetry gate did not pass; see telemetry details")
        gates["telemetry"] = "passed"
        watcher = read("logwatch-summary.json")
        require(watcher["run"] == run and watcher["mode"] == "enforce" and watcher["failed"] is False and
                all(watcher[key] == 0 for key in ("new", "known", "regressed", "repeated")), "Enforced problem watcher did not pass")
        require(not (directory / "bot.problems.jsonl").read_text(encoding="utf-8").strip(), "Bot problems exist despite watcher success")
        sources["bot.problems.jsonl"] = digest(directory / "bot.problems.jsonl")
        allowances = json.loads(allowlist_path.read_bytes())
        sources["allowlist"] = digest(allowlist_path)
        counts = Counter()
        for service in ("ls", "cs", "gs"):
            name = f"logs/{service}/{service}.problems.jsonl"
            with (directory / name).open(encoding="utf-8") as stream:
                for line in stream:
                    problem = json.loads(line)
                    require(problem["run"] == run and problem["srv"] == service, "Problem log identity mismatch")
                    fp = problem["fp"]
                    matches = [entry for entry in allowances if entry["fp"] == fp and "LIVE" in entry["modes"] and
                               service in entry["servers"] and ("scenarios" not in entry or "SOAK" in entry["scenarios"])]
                    require(len(matches) == 1, f"Problem lacks an exact applicable allowance: {fp}")
                    entry = matches[0]
                    require(all(isinstance(entry.get(key), str) and entry[key].strip() for key in ("owner", "reason", "tracking")) and
                            date.fromisoformat(entry["expires"]) >= today, f"Allowance is expired or unowned: {fp}")
                    counts[fp] += 1
                    require(type(entry["maxCount"]) is int and 0 < counts[fp] <= entry["maxCount"], f"Allowance count exceeded: {fp}")
            sources[name] = digest(directory / name)
        require(sum(counts.values()) == watcher["suppressed"] == watcher["total"], "Watcher summary disagrees with retained problem logs")
        gates["problems"] = "passed"
        result.update(status="passed", overallSoakAccepted=True)
    except (OSError, ValueError, KeyError, TypeError, OverflowError) as error:
        failures.append(str(error))
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run_directory", type=Path)
    parser.add_argument("--allowlist", type=Path, default=Path(__file__).resolve().parents[2] / "parity-artifacts/e2e/log-allowlist.json")
    args = parser.parse_args()
    report = analyze(args.run_directory, args.allowlist)
    output = args.run_directory / "soak-acceptance.json"
    output.write_text(json.dumps(report, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    print(f"Soak acceptance {report['status']}: {output}")
    for failure in report["failures"]:
        print(failure, file=sys.stderr)
    return 0 if report["overallSoakAccepted"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
