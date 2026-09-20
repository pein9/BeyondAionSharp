#!/usr/bin/env python3
import copy
import contextlib
from datetime import datetime, timedelta, timezone
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock

spec = importlib.util.spec_from_file_location("telemetry", Path(__file__).with_name("soak-telemetry.py"))
telemetry = importlib.util.module_from_spec(spec)
spec.loader.exec_module(telemetry)


class SoakTelemetryTests(unittest.TestCase):
    def setUp(self):
        self.start = datetime(2026, 9, 20, tzinfo=timezone.utc)
        self.end = self.start + timedelta(hours=2)
        self.dispatch = {"Count": 1000, "Abandoned": 1, "MeanMilliseconds": 2.5,
                         "MaximumMilliseconds": 5, "Buckets": [0, 0, 0, 0, 1000] + [0] * 9,
                         "PendingConnections": 0, "OldestPendingMilliseconds": 0}
        self.samples = [{"time": self.start + timedelta(seconds=second), "connections": 200,
                         "queue": 0, "timers": 650, "workingSetBytes": 2_000_000_000,
                         "lastGcHeapBytes": 1_000_000_000, "lastGcIndex": 1 + second // 60,
                         "dispatch": copy.deepcopy(self.dispatch)}
                        for second in range(0, 7201, 10)]

    def analyze(self):
        return telemetry.analyze_service(self.samples, "gs", self.start, self.end)

    def test_flat_full_window_passes_without_claiming_other_gates(self):
        result = self.analyze()
        self.assertEqual("passed", result["status"])
        self.assertEqual(720_000, result["dispatcher"]["samples"])
        self.assertEqual(720, result["dispatcher"]["abandoned"])
        self.assertEqual(5, result["dispatcher"]["p99UpperMilliseconds"])
        self.assertEqual(6, len(result["plateaus"]["workingSetBytes"]["windows"]))

    def test_first_partial_dispatch_window_is_excluded(self):
        self.samples[0]["dispatch"] = copy.deepcopy(self.dispatch)
        self.samples[0]["dispatch"].update(MaximumMilliseconds=10000, MeanMilliseconds=10000,
                                          Buckets=[0] * 13 + [1000])
        self.assertEqual("passed", self.analyze()["status"])

    def test_pending_age_does_not_require_completed_samples(self):
        self.samples[10]["dispatch"].update(Count=0, Buckets=[0] * 14, MeanMilliseconds=0,
                                           MaximumMilliseconds=0, PendingConnections=1,
                                           OldestPendingMilliseconds=1001)
        self.assertIn("pending write age", " ".join(self.analyze()["failures"]))

    def test_outlier_maximum_is_not_hidden_by_p99(self):
        self.samples[-1]["dispatch"].update(MaximumMilliseconds=5001, Buckets=[0, 0, 0, 0, 999] + [0] * 8 + [1])
        result = self.analyze()
        self.assertEqual(5, result["dispatcher"]["p99UpperMilliseconds"])
        self.assertEqual("failed", result["status"])

    def test_unbounded_quantile_is_json_safe_and_fails(self):
        for sample in self.samples:
            sample["dispatch"].update(MaximumMilliseconds=6000, Buckets=[0] * 13 + [1000])
        result = self.analyze()
        self.assertEqual("unbounded", result["dispatcher"]["p99UpperMilliseconds"])
        self.assertEqual("failed", result["status"])
        json.dumps(result, allow_nan=False)

    def test_no_completed_dispatch_observations_fails(self):
        for sample in self.samples:
            sample["dispatch"].update(Count=0, Buckets=[0] * 14, MeanMilliseconds=0, MaximumMilliseconds=0)
        self.assertEqual("failed", self.analyze()["status"])

    def test_missing_beginning_middle_or_end_heartbeat_fails(self):
        original = self.samples
        for samples in (original[4:], original[:-4], original[:50] + original[54:], []):
            with self.subTest(length=len(samples)):
                self.samples = samples
                self.assertEqual("failed", self.analyze()["status"])

    def test_short_diagnostic_is_not_a_plateau(self):
        self.end = self.start + timedelta(minutes=20)
        self.assertEqual("failed", self.analyze()["status"])

    def test_sustained_memory_or_timer_growth_fails(self):
        for metric, delta in (("workingSetBytes", 500_000), ("lastGcHeapBytes", 500_000), ("timers", 1)):
            with self.subTest(metric=metric):
                original = copy.deepcopy(self.samples)
                for i, sample in enumerate(self.samples):
                    sample[metric] += delta * i
                result = self.analyze()
                self.assertEqual("failed", result["plateaus"][metric]["status"])
                self.samples = original

    def test_warmup_and_isolated_memory_spike_do_not_define_plateau(self):
        for sample in self.samples[:180]:
            sample["workingSetBytes"] //= 2
        self.samples[500]["workingSetBytes"] *= 2
        self.assertEqual("passed", self.analyze()["status"])
        self.assertEqual(4_000_000_000, self.analyze()["peaks"]["workingSetBytes"])

    def test_fitted_growth_catches_late_recovery_that_masks_upward_trend(self):
        values = [2_000_000_000, 2_000_000_000, 2_100_000_000, 2_200_000_000, 2_300_000_000, 2_000_000_000]
        for sample in self.samples:
            window = int((sample["time"] - self.start).total_seconds() - 1800) // 900
            if 0 <= window < 6:
                sample["workingSetBytes"] = values[window]
        result = self.analyze()["plateaus"]["workingSetBytes"]
        self.assertEqual(0, result["endpointGrowth"])
        self.assertGreater(result["fittedGrowth"], result["limit"])
        self.assertEqual("failed", result["status"])

    def test_bad_dispatch_values_fail_closed(self):
        changes = ({"Count": 999}, {"Count": True}, {"Buckets": [1000]}, {"MeanMilliseconds": float("nan")},
                   {"MaximumMilliseconds": float("inf")}, {"MeanMilliseconds": 6}, {"MaximumMilliseconds": 100},
                   {"PendingConnections": 0, "OldestPendingMilliseconds": 1}, {"Abandoned": -1})
        for change in changes:
            with self.subTest(change=change):
                with self.assertRaises(ValueError):
                    telemetry.validate_dispatch(dict(self.dispatch, **change))

    def test_timestamp_requires_offset(self):
        with self.assertRaises(ValueError):
            telemetry.timestamp("2026-09-20T00:00:00")
        self.assertEqual(self.start, telemetry.timestamp("2026-09-20T00:00:00Z"))

    def row(self, sample, service="gs", run="test"):
        dispatch = json.dumps(sample["dispatch"] if service == "gs" else None)
        return {"cat": "Aion.Commons.Diagnostics.ServerHeartbeatService", "srv": service, "run": run,
                "ts": sample["time"].isoformat(), "msg": f"Server heartbeat: connections={sample['connections']}, "
                f"packetQueueDepth={sample['queue']}, armedTimers={sample['timers']}, workingSetBytes={sample['workingSetBytes']}, "
                f"lastGcHeapBytes={sample['lastGcHeapBytes'] or 0}, lastGcIndex={sample['lastGcIndex']}, dispatcherWrites={dispatch}"}

    def test_real_file_contract_and_source_hashes(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "bots-run.json").write_text(json.dumps({"run": "test", "gitSha": "1234567", "seed": 73}))
            for service in ("ls", "cs", "gs"):
                path = root / "logs" / service / f"{service}.events.jsonl"
                path.parent.mkdir(parents=True)
                path.write_text("".join(json.dumps(self.row(sample, service)) + "\n" for sample in self.samples))
            result = telemetry.analyze(root, self.start, self.end)
            self.assertEqual("passed", result["telemetryStatus"])
            self.assertFalse(result["overallSoakAccepted"])
            self.assertEqual(3, len(result["sourceSha256"]))
            self.assertTrue(all(len(digest) == 64 for digest in result["sourceSha256"].values()))
            self.assertEqual("failed", telemetry.analyze(root, self.start, self.end - timedelta(seconds=1))["telemetryStatus"])
            with self.assertRaises(ValueError):
                telemetry.analyze(root, self.end, self.start)

    def recorded_fixture(self, root):
        window = {"SchemaVersion": 1, "Status": "completed", "OverallSoakAccepted": False,
                  "Run": "test", "BotCount": 50, "PlannedSeconds": 7200,
                  "StartedUtc": self.start.isoformat(), "EndedUtc": self.end.isoformat(),
                  "CompletedUtc": (self.end + timedelta(minutes=5)).isoformat(),
                  "ElapsedSeconds": 7500, "ClockConsistent": True, "WallClockDriftSeconds": 0}
        (root / "bots-run.json").write_text(json.dumps({"run": "test", "bots": 50,
                                                       "soakSeconds": 7200, "scenarios": ["SOAK"]}))
        (root / "soak-window.json").write_text(json.dumps(window))
        for service in ("ls", "cs", "gs"):
            path = root / "logs" / service / f"{service}.events.jsonl"
            path.parent.mkdir(parents=True)
            path.write_text("".join(json.dumps(self.row(sample, service)) + "\n" for sample in self.samples))
        return window

    def test_recorded_window_excludes_cleanup_and_hashes_its_provenance(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.recorded_fixture(root)
            result = telemetry.analyze_recorded_window(root)
            self.assertEqual("passed", result["telemetryStatus"])
            self.assertEqual(7200, result["seconds"])
            self.assertEqual("runner-recorded", result["windowSource"])
            self.assertEqual(5, len(result["sourceSha256"]))
            self.assertFalse(result["overallSoakAccepted"])

    def test_recorded_window_rejects_incomplete_wrong_population_or_clock_and_cleanup_extension(self):
        changes = ({"Status": "running"}, {"Status": "failed"}, {"SchemaVersion": 2}, {"SchemaVersion": True},
                   {"Run": "other"}, {"BotCount": 200}, {"BotCount": True}, {"PlannedSeconds": 7500},
                   {"OverallSoakAccepted": True}, {"ElapsedSeconds": 7199}, {"ClockConsistent": False},
                   {"WallClockDriftSeconds": 3}, {"WallClockDriftSeconds": float("nan")},
                   {"CompletedUtc": (self.start - timedelta(seconds=1)).isoformat()},
                   {"EndedUtc": (self.end + timedelta(minutes=5)).isoformat()})
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            window = self.recorded_fixture(root)
            for change in changes:
                with self.subTest(change=change):
                    (root / "soak-window.json").write_text(json.dumps(dict(window, **change)))
                    with self.assertRaises(ValueError):
                        telemetry.analyze_recorded_window(root)

    def test_recorded_cli_rejects_missing_or_mixed_windows_and_replaces_stale_output(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            output = root / "telemetry.json"
            for options in ([], ["--recorded-window"], ["--start", self.start.isoformat()],
                            ["--recorded-window", "--start", self.start.isoformat(), "--end", self.end.isoformat()]):
                output.write_text('{"telemetryStatus":"passed"}')
                argv = ["soak-telemetry.py", str(root), "--output", str(output)] + options
                with mock.patch("sys.argv", argv), contextlib.redirect_stderr(io.StringIO()):
                    self.assertEqual(2, telemetry.main())
                self.assertEqual("failed", json.loads(output.read_text())["telemetryStatus"])

    def test_recorded_cli_accepts_full_telemetry_without_claiming_soak_acceptance(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.recorded_fixture(root)
            output = root / "telemetry.json"
            argv = ["soak-telemetry.py", str(root), "--recorded-window", "--output", str(output)]
            with mock.patch("sys.argv", argv), contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(0, telemetry.main())
            self.assertFalse(json.loads(output.read_text())["overallSoakAccepted"])

    def test_wrong_identity_duplicate_legacy_and_corrupt_rows_rejected(self):
        row = self.row(self.samples[0])
        cases = ([dict(row, run="other")], [dict(row, srv="ls")], [row, row],
                 [dict(row, msg="Server heartbeat: connections=1")])
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "events.jsonl"
            for rows in cases:
                path.write_text("".join(json.dumps(value) + "\n" for value in rows))
                with self.assertRaises(ValueError):
                    telemetry.read_samples(path, "gs", "test")
            path.write_text("{truncated")
            with self.assertRaises(ValueError):
                telemetry.read_samples(path, "gs", "test")

    def test_invalid_cli_replaces_stale_green_output(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            output = root / "telemetry.json"
            output.write_text('{"telemetryStatus":"passed"}')
            argv = ["soak-telemetry.py", str(root), "--start", self.start.isoformat(),
                    "--end", self.end.isoformat(), "--output", str(output)]
            with mock.patch("sys.argv", argv), contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(2, telemetry.main())
            report = json.loads(output.read_text())
            self.assertEqual("failed", report["telemetryStatus"])
            self.assertFalse(report["overallSoakAccepted"])

    def test_tolerances_allow_small_growth_but_report_it(self):
        for i, sample in enumerate(self.samples):
            sample["workingSetBytes"] += i * 1000
        plateau = self.analyze()["plateaus"]["workingSetBytes"]
        self.assertEqual("passed", plateau["status"])
        self.assertGreater(plateau["endpointGrowth"], 0)

    def test_no_collection_yet_is_missing_not_a_flat_zero_heap(self):
        for sample in self.samples:
            sample.update(lastGcHeapBytes=None, lastGcIndex=0)
        result = self.analyze()
        self.assertEqual("failed", result["status"])
        self.assertEqual("insufficient", result["plateaus"]["lastGcHeapBytes"]["status"])
        self.assertIsNone(result["peaks"]["lastGcHeapBytes"])
        self.assertEqual(0, result["lastGcObservations"]["distinctIndices"])

    def test_last_collection_index_exposes_staleness(self):
        for sample in self.samples:
            sample["lastGcIndex"] = 7
        result = self.analyze()
        self.assertEqual({"firstIndex": 7, "lastIndex": 7, "distinctIndices": 1}, result["lastGcObservations"])

    def test_parser_distinguishes_uncollected_and_measured_zero_heap(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "events.jsonl"
            for index, expected in ((0, None), (1, 0)):
                row = self.row(dict(self.samples[0], lastGcHeapBytes=0, lastGcIndex=index))
                path.write_text(json.dumps(row) + "\n")
                samples, _ = telemetry.read_samples(path, "gs", "test")
                self.assertEqual(expected, samples[0]["lastGcHeapBytes"])
            row = self.row(dict(self.samples[0], lastGcIndex=0))
            path.write_text(json.dumps(row) + "\n")
            with self.assertRaisesRegex(ValueError, "without a completed collection"):
                telemetry.read_samples(path, "gs", "test")

    def test_legacy_negative_estimate_is_not_reinterpreted_as_a_last_gc_snapshot(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "events.jsonl"
            row = self.row(self.samples[0])
            row["msg"] = row["msg"].replace("lastGcHeapBytes=1000000000, lastGcIndex=1", "managedHeapBytes=-907936")
            path.write_text(json.dumps(row) + "\n")
            with self.assertRaisesRegex(ValueError, "changed heartbeat metrics"):
                telemetry.read_samples(path, "gs", "test")


if __name__ == "__main__":
    unittest.main()
