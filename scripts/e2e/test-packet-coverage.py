#!/usr/bin/env python3
import base64
import copy
import gzip
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import subprocess
import sys
import unittest
from unittest.mock import patch

import packet_coverage as packets

spec = importlib.util.spec_from_file_location("packet_gate", Path(__file__).with_name("report-packet-coverage.py"))
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)


class PacketCoverageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="aion-packets-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.outcome = dict(run="test", mode="SIM")
        self.catalog = dict(schemaVersion=1, run="test", mode="SIM", protocol="aion-game-4.8",
            gameServerModule="00000000-0000-0000-0000-000000000001", botModule="00000000-0000-0000-0000-000000000002",
            client=[dict(name="CM_A", opcode=1), dict(name="CM_B", opcode=2)],
            server=[dict(name="SM_A", opcode=3, structuredDecoder=True), dict(name="SM_B", opcode=4, structuredDecoder=False)])
        self.put("packet-catalog.json", self.catalog)
        self.trace = [dict(run="test", dir="action", packet="login", fields={}),
            dict(run="test", dir=">", packet="CM_A", fields=dict(bodyHex="00")),
            dict(run="test", dir="<", packet="SM_A", fields=dict(value=1)),
            dict(run="test", dir="<", packet="SM_B", fields=dict(bodyHex="AA"))]
        self.jsonl("bots/b01.trace.jsonl", self.trace)

    def put(self, name, value):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(value), encoding="utf-8")

    def jsonl(self, name, rows):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text("".join(json.dumps(row)+"\n" for row in rows), encoding="utf-8")

    def collect(self):
        return packets.collect(self.root, self.outcome)

    def baseline(self):
        measured = self.collect()
        return dict(schemaVersion=1, referenceByMode=dict(SIM=dict(inventorySha256=measured["inventorySha256"], clientSent=["CM_A"], serverDecoded=["SM_A"])))

    def test_unique_game_opcodes_distinguish_decoded_raw_received_and_no_tap(self):
        self.jsonl("bots/b02.trace.jsonl", self.trace)
        result = self.collect()
        self.assertEqual(1, result["metrics"]["clientSent"]["count"])
        self.assertEqual(2, result["metrics"]["clientSent"]["records"])
        self.assertEqual(["SM_A"], result["metrics"]["serverDecoded"]["packets"])
        self.assertEqual(["SM_B"], result["metrics"]["serverRaw"]["packets"])
        self.assertEqual(2, result["metrics"]["serverReceived"]["count"])
        self.assertEqual(.5, result["metrics"]["serverDecoded"]["fraction"])
        self.assertFalse(result["tap"]["available"])
        self.assertEqual(3, len(result["sources"]))

    def test_raw_fallback_even_for_supported_packet_never_counts_as_decoded(self):
        self.trace[2]["fields"] = dict(bodyHex="00")
        self.jsonl("bots/b01.trace.jsonl", self.trace)
        self.assertEqual(0, self.collect()["metrics"]["serverDecoded"]["count"])

    def test_tap_and_loss_are_separate_from_decoded_coverage(self):
        frame = bytes([7, 0, 0, 0, 0, 0, 0])
        self.jsonl("logs/gs/packet-tap.jsonl", [dict(run="test", packet="SM_B", opcode="0x004", length=7, frameBase64=base64.b64encode(frame).decode()), dict(run="test", kind="dropped", count=3)])
        result = self.collect()
        self.assertEqual(["SM_B"], result["metrics"]["serverTapped"]["packets"])
        self.assertEqual(["SM_A"], result["metrics"]["serverDecoded"]["packets"])
        self.assertEqual(dict(available=True, droppedRecords=3, complete=False), result["tap"])

    def test_compressed_sources_are_hashed_and_not_double_counted(self):
        path = self.root / "bots/b01.trace.jsonl"
        compressed = path.with_suffix(".jsonl.gz")
        compressed.write_bytes(gzip.compress(path.read_bytes()))
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            self.collect()
        path.unlink()
        self.assertEqual(1, self.collect()["metrics"]["clientSent"]["count"])
        self.assertTrue(self.collect()["sources"][1]["path"].endswith(".gz"))

    def test_catalog_run_mode_inventory_and_decoder_types_are_validated(self):
        for key, value in (("run", "wrong"), ("mode", "LIVE"), ("protocol", "unknown"), ("botModule", "bad")):
            catalog = dict(self.catalog, **{key: value})
            self.put("packet-catalog.json", catalog)
            with self.assertRaises(ValueError): self.collect()
        for client in ([self.catalog["client"][0]]*2, [], [dict(name="CM_A", opcode=True)]):
            self.put("packet-catalog.json", dict(self.catalog, client=client))
            with self.assertRaises(ValueError): self.collect()
        self.put("packet-catalog.json", dict(self.catalog, server=[dict(name="SM_A", opcode=3, structuredDecoder=1)]))
        with self.assertRaises(ValueError): self.collect()

    def test_trace_foreign_run_unknown_packet_and_fake_structured_decoder_fail(self):
        for changes in (dict(run="foreign"), dict(packet="SM_UNKNOWN"), dict(dir="bogus"), dict(packet="SM_B", fields=dict(value=2)), dict(fields=dict(bodyHex="Z"))):
            trace = copy.deepcopy(self.trace)
            trace[2].update(changes)
            self.jsonl("bots/b01.trace.jsonl", trace)
            with self.assertRaises(ValueError): self.collect()

    def test_missing_or_truncated_trace_does_not_establish_zero_coverage(self):
        path = self.root / "bots/b01.trace.jsonl"
        path.write_text('{"run":', encoding="utf-8")
        with self.assertRaises(ValueError): self.collect()
        path.unlink()
        with self.assertRaisesRegex(ValueError, "No retained"): self.collect()

    def test_tap_foreign_identity_registration_lengths_and_loss_fail(self):
        valid = dict(run="test", packet="SM_A", opcode="0x003", length=7, frameBase64="BwAAAAAAAA==")
        for changes in (dict(run="wrong"), dict(opcode="0x004"), dict(length=8), dict(frameBase64="!!!")):
            self.jsonl("logs/gs/packet-tap.jsonl", [dict(valid, **changes)])
            with self.assertRaises(ValueError): self.collect()
        self.jsonl("logs/gs/packet-tap.jsonl", [dict(run="test", kind="dropped", count=True)])
        with self.assertRaises(ValueError): self.collect()

    def test_identity_loss_fails_even_if_count_is_replaced_and_growth_passes(self):
        baseline, measured = self.baseline(), self.collect()
        measured["metrics"]["clientSent"]["packets"] = ["CM_B"]
        result = packets.compare([measured], baseline)
        self.assertFalse(result["passed"])
        self.assertEqual(["CM_A"], result["metrics"][0]["missing"])
        measured["metrics"]["clientSent"]["packets"].append("CM_A")
        self.assertTrue(packets.compare([measured], baseline)["passed"])

    def test_denominator_or_decoder_drift_requires_explicit_review(self):
        baseline, measured = self.baseline(), self.collect()
        measured["inventory"]["server"][1]["structuredDecoder"] = True
        self.assertFalse(packets.compare([measured], baseline)["passed"])
        self.assertFalse(packets.compare([], baseline)["passed"])
        baseline["referenceByMode"]["SIM"]["clientSent"] = []
        with self.assertRaises(ValueError): packets.compare([self.collect()], baseline)

    def test_full_gate_revalidates_children_and_records_identity_deltas(self):
        self.put("suite-plan.json", dict(run="test", suite="Breadth", steps=[dict(id="sim-a", kind="Sim", scenarios=["L0"]), dict(id="packet-coverage", kind="PacketCoverage")]))
        self.jsonl("suite-results.jsonl", [dict(id="sim-a", kind="Sim", status="Passed")])
        child = dict(run="test", mode="SIM", status="passed", scenarios=[dict(id="L0")], packetCoverage=[self.collect()], runnerSourceSha256="abc")
        with patch.object(gate.reporter, "build_report", return_value=child):
            self.assertTrue(gate.build(self.root, self.baseline())["baselineComparison"]["passed"])
            for field, value in (("status", "failed"), ("run", "stale"), ("packetCoverage", []), ("scenarios", [dict(id="wrong")])):
                with patch.object(gate.reporter, "build_report", return_value=dict(child, **{field: value})):
                    self.assertFalse(gate.build(self.root, self.baseline())["baselineComparison"]["passed"])

    def test_report_renders_measurements_and_keeps_malformed_packet_evidence_fatal(self):
        self.put("runner-result.json", dict(self.outcome, schemaVersion=1, scenarios=["L0"], status="failed", durationSeconds=1,
            startedUtc="2026-09-20T12:00:00Z", finishedUtc="2026-09-20T12:00:01Z", error="original failure"))
        self.jsonl("scenario-results.jsonl", [])
        report = gate.reporter.build_report(self.root)
        self.assertEqual("failed", report["status"])
        self.assertEqual(1, len(report["packetCoverage"]))
        self.assertIn("1/2", gate.reporter.markdown(report))
        self.put("packet-catalog.json", dict(self.catalog, run="wrong"))
        report = gate.reporter.build_report(self.root)
        self.assertTrue(any("Invalid packet coverage" in issue for issue in report["evidenceIssues"]))

    def test_cli_retains_failing_gate_and_frozen_baseline_without_overwriting(self):
        self.put("suite-plan.json", dict(run="test", suite="Breadth", steps=[dict(id="sim-a", kind="Sim", scenarios=["L0"])]))
        self.jsonl("suite-results.jsonl", [dict(id="sim-a", kind="Sim", status="Failed")])
        self.put("reference.json", self.baseline())
        command = [sys.executable, str(Path(__file__).with_name("report-packet-coverage.py")), "--run-root", str(self.root), "--baseline", str(self.root/"reference.json")]
        result = subprocess.run(command, capture_output=True, text=True)
        self.assertEqual(1, result.returncode, result.stderr)
        retained = (self.root/"packet-coverage-comparison.json").read_bytes()
        self.assertFalse(json.loads(retained)["baselineComparison"]["passed"])
        self.assertEqual((self.root/"reference.json").read_bytes(), (self.root/"packet-coverage-baseline.json").read_bytes())
        self.put("reference.json", dict(schemaVersion=999))
        self.assertNotEqual(0, subprocess.run(command, capture_output=True).returncode)
        self.assertEqual(retained, (self.root/"packet-coverage-comparison.json").read_bytes())

    def test_full_report_rechecks_packet_floor_instead_of_trusting_stale_gate_success(self):
        steps = [dict(id="sim-a", kind="Sim", scenarios=["L0"]), dict(id="packet-coverage", kind="PacketCoverage")]
        self.put("suite-plan.json", dict(run="test", suite="Breadth", steps=steps))
        self.jsonl("suite-results.jsonl", [dict(id=step["id"], kind=step["kind"], status="Passed", durationSeconds=1) for step in steps])
        (self.root/"sim-a").mkdir()
        self.put("packet-coverage-baseline.json", self.baseline())
        self.put("packet-coverage-comparison.json", dict(run="test", baselineComparison=dict(passed=True),
            baselineSourceSha256=hashlib.sha256((self.root/"packet-coverage-baseline.json").read_bytes()).hexdigest()))
        child = dict(run="test", mode="SIM", status="passed", scenarios=[dict(id="L0")], packetCoverage=[self.collect()],
            fingerprints=[], heartbeatPeaks=[], resourcePeaks=[], coverage=[], limitations=[], evidenceIssues=[])
        def render():
            result = {key: [] for key in ("steps", "scenarios", "packetCoverage", "fingerprints", "heartbeatPeaks", "resourcePeaks", "coverage", "limitations", "evidenceIssues")}
            with patch.object(gate.reporter, "build_report", return_value=child):
                gate.reporter.collect_suite(self.root, dict(run="test"), result)
            return result
        self.assertEqual([], render()["evidenceIssues"])
        child["packetCoverage"][0]["metrics"]["clientSent"]["packets"] = ["CM_B"]
        self.assertTrue(any("no longer satisfies" in issue for issue in render()["evidenceIssues"]))
        self.put("packet-coverage-baseline.json", dict(schemaVersion=999))
        with self.assertRaisesRegex(ValueError, "provenance mismatch"): render()


if __name__ == "__main__":
    unittest.main()
