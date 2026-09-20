#!/usr/bin/env python3
"""Aggregation controls use synthetic files; they are not LIVE capacity evidence."""
import contextlib
from datetime import date, datetime, timedelta, timezone
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock

spec = importlib.util.spec_from_file_location("acceptance", Path(__file__).with_name("soak-acceptance.py"))
acceptance = importlib.util.module_from_spec(spec)
spec.loader.exec_module(acceptance)


class SoakAcceptanceTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.start = datetime(2026, 9, 20, tzinfo=timezone.utc)
        self.end = self.start + timedelta(hours=2)
        self.metadata = dict(run="test", gitSha="a" * 40, seed=73, bots=50, soakSeconds=7200, scenarios=["SOAK"])
        self.write("bots-run.json", self.metadata)
        self.write("soak-window.json", dict(SchemaVersion=1, Run="test", BotCount=50, PlannedSeconds=7200, Status="completed",
                   StartedUtc=self.start.isoformat(), EndedUtc=self.end.isoformat(), CompletedUtc=self.end.isoformat(),
                   ElapsedSeconds=7200, WallClockDriftSeconds=0, ClockConsistent=True, OverallSoakAccepted=False))
        self.write("soak-execution.json", dict(schemaVersion=1, run="test", bots=50, seed=73, seconds=7200, success=True, failure=None,
                   startedUtc=self.start.isoformat(), completedUtc=self.end.isoformat()))
        for name in ("soak-runtime.json", "soak-economy.json"):
            self.write(name, {})  # The separately tested C# reader owns their semantic validation.
        (self.root / "bots").mkdir()
        for bot in ["gm"] + [f"b{i:02}" for i in range(1, 51)]:
            (self.root / f"bots/{bot}.trace.jsonl").write_text("")
        self.workload = dict(Policy="p10-02-workload-v2", Status="passed", Run="test", GitSha="a" * 40, Failures=[],
                CapacityConfiguration=True, OverallSoakAccepted=False, SourceSha256={}, Subjects=[dict(Bot=f"b{i:02}", ActivityProgressWindows=[5] * 8) for i in range(1, 51)],
                RecomputedEconomy=dict(Policy="p10-02-economy-v1", Status="passed", ImpossibleOutcomes=0, OverallSoakAccepted=False,
                                      Tests=[dict(Status="passed") for _ in range(4)]))
        self.rehash()
        self.write("logwatch-summary.json", dict(run="test", mode="enforce", failed=False, total=1, suppressed=1, new=0, known=0, regressed=0, repeated=0))
        (self.root / "bot.problems.jsonl").write_text("")
        self.allowlist = self.root / "allowlist.json"
        self.write("allowlist.json", [dict(fp="231c488f", modes=["LIVE"], servers=["gs"], owner="test", reason="fixture startup", tracking="test", expires="2027-09-20", maxCount=1)])
        dispatch = dict(Count=10, Abandoned=0, PendingConnections=0, MeanMilliseconds=1, MaximumMilliseconds=1,
                        OldestPendingMilliseconds=0, Buckets=[0, 0, 10] + [0] * 11)
        for service in ("ls", "cs", "gs"):
            folder = self.root / f"logs/{service}"
            folder.mkdir(parents=True)
            rows = []
            for second in range(0, 7201, 10):
                rows.append(dict(run="test", srv=service, cat="Aion.Commons.Diagnostics.ServerHeartbeatService", ts=(self.start + timedelta(seconds=second)).isoformat(),
                    msg="Server heartbeat: connections=50, packetQueueDepth=0, armedTimers=100, workingSetBytes=1000000000, "
                        "lastGcHeapBytes=500000000, lastGcIndex=1, dispatcherWrites=" + (json.dumps(dispatch) if service == "gs" else "null")))
            (folder / f"{service}.events.jsonl").write_text("".join(json.dumps(row) + "\n" for row in rows))
            (folder / f"{service}.problems.jsonl").write_text(json.dumps(dict(run="test", srv="gs", fp="231c488f")) + "\n" if service == "gs" else "")

    def write(self, name, value):
        (self.root / name).write_text(json.dumps(value), encoding="utf-8")

    def edit(self, name, change):
        value = json.loads((self.root / name).read_text())
        change(value)
        self.write(name, value)

    def rehash(self):
        files = ["bots-run.json", "soak-window.json", "soak-runtime.json", "soak-economy.json", "bots/gm.trace.jsonl"]
        files += [f"bots/b{i:02}.trace.jsonl" for i in range(1, 51)]
        self.workload["SourceSha256"] = {name: acceptance.digest(self.root / name) for name in files}
        self.write("soak-workload.json", self.workload)

    def analyze(self):
        return acceptance.analyze(self.root, self.allowlist, today=date(2026, 9, 20))

    def test_all_gates_required_for_synthetic_green(self):
        result = self.analyze()
        self.assertTrue(result["overallSoakAccepted"], result["failures"])
        self.assertEqual({"runner", "workload", "economy", "telemetry", "problems"}, set(result["gates"]))
        self.assertEqual(7200, result["telemetry"]["seconds"])

    def test_mutated_workload_and_invocation_controls_fail(self):
        mutations = [
            ("soak-execution.json", lambda v: v.update(success=False, failure="runner failed")),
            ("soak-execution.json", lambda v: v.update(run="other")),
            ("soak-execution.json", lambda v: v.update(completedUtc=self.start.isoformat())),
            ("soak-workload.json", lambda v: v.update(Status="failed")),
            ("soak-workload.json", lambda v: v.update(CapacityConfiguration=False)),
            ("soak-workload.json", lambda v: v["SourceSha256"].pop("bots/b01.trace.jsonl")),
            ("soak-workload.json", lambda v: v["Subjects"].pop()),
            ("soak-workload.json", lambda v: v.update(Policy="p10-02-workload-v1")),
            ("soak-workload.json", lambda v: v["Subjects"][0].update(ActivityProgressWindows=[40] + [0] * 7)),
            ("soak-workload.json", lambda v: v["Subjects"][0].update(ActivityProgressWindows=[True] * 8)),
            ("soak-workload.json", lambda v: v["Subjects"][0].update(ActivityProgressWindows=[5] * 7)),
            ("soak-workload.json", lambda v: v["RecomputedEconomy"].update(Status="insufficient")),
            ("soak-workload.json", lambda v: v["RecomputedEconomy"].update(ImpossibleOutcomes=1)),
            ("soak-workload.json", lambda v: v["RecomputedEconomy"]["Tests"][0].update(Status="failed")),
            ("logwatch-summary.json", lambda v: v.update(mode="record")),
            ("logwatch-summary.json", lambda v: v.update(regressed=1)),
            ("logwatch-summary.json", lambda v: v.update(suppressed=0)),
            ("allowlist.json", lambda v: v[0].update(expires="2026-09-19")),
            ("allowlist.json", lambda v: v[0].update(owner="")),
            ("allowlist.json", lambda v: v[0].update(scenarios=["canaries"])),
            ("allowlist.json", lambda v: v[0].update(maxCount=0)),
        ]
        for name, mutate in mutations:
            with self.subTest(name=name, mutation=mutate):
                original = (self.root / name).read_bytes()
                try:
                    self.edit(name, mutate)
                    self.assertFalse(self.analyze()["overallSoakAccepted"])
                finally:
                    (self.root / name).write_bytes(original)

    def test_changed_raw_source_is_not_saved_by_green_summary(self):
        (self.root / "bots/b01.trace.jsonl").write_text("changed")
        self.assertIn("source changed", " ".join(self.analyze()["failures"]))

    def test_raw_bot_and_unallowed_server_problems_fail(self):
        (self.root / "bot.problems.jsonl").write_text("{}\n")
        self.assertFalse(self.analyze()["overallSoakAccepted"])
        (self.root / "bot.problems.jsonl").write_text("")
        (self.root / "logs/gs/gs.problems.jsonl").write_text(json.dumps(dict(run="test", srv="gs", fp="ffffffff")))
        self.assertFalse(self.analyze()["overallSoakAccepted"])

    def test_telemetry_is_recomputed_not_read_from_old_green_report(self):
        self.write("soak-telemetry.json", dict(telemetryStatus="passed"))
        (self.root / "logs/gs/gs.events.jsonl").write_text("")
        self.assertFalse(self.analyze()["overallSoakAccepted"])

    def test_missing_inputs_and_stale_cli_green_fail_closed(self):
        (self.root / "soak-execution.json").unlink()
        self.write("soak-acceptance.json", dict(overallSoakAccepted=True))
        with mock.patch("sys.argv", ["soak-acceptance.py", str(self.root), "--allowlist", str(self.allowlist)]), \
                contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(1, acceptance.main())
        self.assertFalse(json.loads((self.root / "soak-acceptance.json").read_text())["overallSoakAccepted"])


if __name__ == "__main__":
    unittest.main()
