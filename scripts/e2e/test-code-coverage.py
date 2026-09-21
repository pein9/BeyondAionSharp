#!/usr/bin/env python3
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import xml.etree.ElementTree as ET
from unittest.mock import patch

import code_coverage as coverage

spec = importlib.util.spec_from_file_location("report_run", Path(__file__).with_name("report-run.py"))
reporter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(reporter)


class CodeCoverageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="aion-code-coverage-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.outcome = dict(schemaVersion=1, run="run-a", mode="SIM", gitSha="abc", seed=1, scenarios=["L0"], status="failed",
            startedUtc="2026-09-21T00:00:00Z", finishedUtc="2026-09-21T00:01:00Z", durationSeconds=60, error="original failure")
        settings = ET.Element("RunSettings")
        config = ET.SubElement(ET.SubElement(ET.SubElement(ET.SubElement(settings, "DataCollectionRunSettings"), "DataCollectors"), "DataCollector", friendlyName="XPlat Code Coverage"), "Configuration")
        for key, value in coverage.SETTINGS.items(): ET.SubElement(config, key).text = value
        ET.ElementTree(settings).write(self.root/"coverage.runsettings", encoding="utf-8")
        self.request = dict(schemaVersion=1, run="run-a", mode="SIM", gitSha="abc", seed=1, tier="Fast", scenarios=["L0"],
            sourceRoot="C:/project/src/Aion.GameServer", collector="coverlet.collector/6.0.4", testFilter="FullyQualifiedName~SimulationFastScenarioTests",
            sourceFiles={}, binaries={name: "a"*64 for name in ("Aion.GameServer.dll", "Aion.GameServer.pdb")},
            settingsSha256=self.hash("coverage.runsettings"), baselineSha256=None)
        self.raw = {"Aion.GameServer.dll": {}}
        for directory in coverage.DIRECTORIES:
            path = f"{directory}/Example.cs"
            self.request["sourceFiles"][path] = "b"*64
            method = dict(Lines={"10": 1, "11": 0}, Branches=[dict(Line=10, Offset=1, EndOffset=2+i, Path=i, Ordinal=i, Hits=1-i) for i in range(2)])
            # Two classes share a physical source line. Never double-count that line.
            self.raw["Aion.GameServer.dll"]["C:/project/src/Aion.GameServer/"+path] = {"Example": {"M()": method}, "Example/Generated": {"MoveNext()": dict(Lines={"10": 1}, Branches=[])}}
        self.request["sourceFiles"]["Properties/NoSequencePoints.cs"] = "c"*64
        self.put("attachment/coverage.json", self.raw)
        (self.root/"attachment/coverage.cobertura.xml").write_text("<coverage />", encoding="utf-8")
        self.result = dict(schemaVersion=1, run="run-a", requestSha256="", errors=[], binariesRestored=True,
            artifacts={name: dict(path="attachment/"+name, copies=["attachment/"+name], sha256=self.hash("attachment/"+name)) for name in ("coverage.json", "coverage.cobertura.xml")})
        self.save()

    def hash(self, name):
        return hashlib.sha256((self.root/name).read_bytes()).hexdigest()

    def put(self, name, value):
        path = self.root/name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(value), encoding="utf-8")

    def save(self):
        self.put("code-coverage-request.json", self.request)
        self.result["requestSha256"] = self.hash("code-coverage-request.json")
        self.put("code-coverage-result.json", self.result)

    def raw_changed(self):
        self.put("attachment/coverage.json", self.raw)
        self.result["artifacts"]["coverage.json"]["sha256"] = self.hash("attachment/coverage.json")
        self.save()

    def collect(self):
        return coverage.collect(self.root, self.outcome)

    def test_unique_lines_branch_paths_and_uninstrumented_source_counts(self):
        summary = self.collect()
        total = summary["directories"][0]
        self.assertEqual(dict(covered=5, total=10, fraction=.5), total["lines"])
        self.assertEqual(dict(covered=5, total=10, fraction=.5), total["branches"])
        self.assertEqual(5, total["instrumentedFiles"])
        self.assertEqual(6, total["sourceFiles"])
        self.assertEqual(6, len(summary["directories"]))
        self.assertEqual(5, len(summary["processes"][0]["sources"]))

    def test_union_across_shards_never_adds_denominators_or_percentages(self):
        first = coverage.load(self.root, self.outcome)
        second = copy.deepcopy(first)
        second["points"]["hitLines"] = second["points"]["lines"] - second["points"]["hitLines"]
        second["points"]["hitBranches"] = second["points"]["branches"] - second["points"]["hitBranches"]
        total = coverage.summarize([first, second])["directories"][0]
        self.assertEqual(dict(covered=10, total=10, fraction=1), total["lines"])
        self.assertEqual(dict(covered=10, total=10, fraction=1), total["branches"])
        second["pointInventorySha256"] = "d"*64
        with self.assertRaisesRegex(ValueError, "unlike"): coverage.summarize([first, second])

    def test_request_identity_scope_and_collector_are_checked(self):
        original = copy.deepcopy(self.request)
        for key, value in (("run", "other"), ("seed", True), ("gitSha", "stale"), ("scenarios", []), ("collector", "other"), ("testFilter", "subset")):
            self.request = dict(original, **{key: value})
            self.save()
            with self.assertRaisesRegex(ValueError, "identity/scope"): self.collect()

    def test_missing_changed_duplicate_or_empty_attachments_cannot_pass(self):
        self.result["artifacts"].pop("coverage.cobertura.xml")
        self.save()
        with self.assertRaisesRegex(ValueError, "Missing coverage"): self.collect()
        self.result["artifacts"]["coverage.cobertura.xml"] = dict(path="attachment/coverage.cobertura.xml", sha256="a"*64)
        self.save()
        with self.assertRaisesRegex(ValueError, "hash mismatch"): self.collect()

    def test_unrestored_binary_and_collector_errors_fail(self):
        self.result["binariesRestored"] = False
        self.save()
        with self.assertRaisesRegex(ValueError, "restoration failed"): self.collect()
        self.result.update(binariesRestored=True, errors=["Missing coverage.json"])
        self.save()
        with self.assertRaisesRegex(ValueError, "Missing coverage.json"): self.collect()

    def test_identical_trx_copy_is_retained_once_per_source_but_not_counted_twice(self):
        self.put("trx/In/host/coverage.json", self.raw)
        self.result["artifacts"]["coverage.json"]["copies"].append("trx/In/host/coverage.json")
        self.save()
        summary = self.collect()
        self.assertEqual(10, summary["directories"][0]["lines"]["total"])
        self.assertEqual(6, len(summary["processes"][0]["sources"]))
        self.put("trx/In/host/coverage.json", {})
        with self.assertRaisesRegex(ValueError, "hash mismatch"): self.collect()

    def test_settings_cannot_narrow_the_source_filter(self):
        path = self.root/"coverage.runsettings"
        path.write_text(path.read_text().replace("[Aion.GameServer]*", "[Aion.GameServer]Aion.GameServer.Services.*"), encoding="utf-8")
        self.request["settingsSha256"] = self.hash("coverage.runsettings")
        self.save()
        with self.assertRaisesRegex(ValueError, "narrow"): self.collect()

    def test_source_outside_frozen_inventory_and_parent_traversal_fail(self):
        source, classes = next(iter(self.raw["Aion.GameServer.dll"].items()))
        self.raw["Aion.GameServer.dll"][source.replace("Services/Example.cs", "../Other.cs")] = classes
        self.raw_changed()
        with self.assertRaises(ValueError): self.collect()
        self.raw = {"Aion.GameServer.dll": {"C:/different/Example.cs": classes}}
        self.raw_changed()
        with self.assertRaisesRegex(ValueError, "outside frozen"): self.collect()

    def test_invalid_hit_and_branch_values_fail(self):
        classes = next(iter(self.raw["Aion.GameServer.dll"].values()))
        for invalid in (-1, True, 1.5):
            classes["Example"]["M()"]["Lines"]["10"] = invalid
            self.raw_changed()
            with self.assertRaisesRegex(ValueError, "integer"): self.collect()
        classes["Example"]["M()"]["Lines"]["10"] = 1
        classes["Example"]["M()"]["Branches"][0]["Line"] = 0
        self.raw_changed()
        with self.assertRaisesRegex(ValueError, "integer"): self.collect()

    def test_absent_required_directory_is_not_zero_percent(self):
        self.raw["Aion.GameServer.dll"].pop("C:/project/src/Aion.GameServer/Handlers/AI/Example.cs")
        self.raw_changed()
        with self.assertRaisesRegex(ValueError, "missing required directory"): self.collect()

    def test_retained_baseline_deltas_and_incomparable_workload_are_explicit(self):
        baseline = dict(schemaVersion=1, measurement=self.collect())
        legacy = copy.deepcopy(baseline)
        legacy["measurement"]["processes"][0].pop("seed")
        self.assertFalse(coverage.compare(self.collect(), legacy)["comparable"])
        self.put("code-coverage-baseline.json", baseline)
        self.request["baselineSha256"] = self.hash("code-coverage-baseline.json")
        self.save()
        compared = self.collect()["baselineComparison"]
        self.assertTrue(compared["comparable"])
        self.assertTrue(all(row["lines"]["coveredDelta"] == 0 for row in compared["directories"]))
        self.request["tier"] = "Full"
        self.save()
        self.assertFalse(self.collect()["baselineComparison"]["comparable"])
        self.request.update(tier="Fast", seed=2)
        self.outcome["seed"] = 2
        self.save()
        self.assertFalse(self.collect()["baselineComparison"]["comparable"])
        self.put("code-coverage-baseline.json", {})
        with self.assertRaisesRegex(ValueError, "hash mismatch"): self.collect()

    def test_report_preserves_failed_scenario_and_shows_real_coverage(self):
        self.put("runner-result.json", self.outcome)
        self.put("run.json", dict(codeCoverageEnabled=True))
        (self.root/"scenario-results.jsonl").write_text("", encoding="utf-8")
        report = reporter.build_report(self.root)
        self.assertEqual("failed", report["status"])
        self.assertEqual(5, report["codeCoverage"]["directories"][0]["lines"]["covered"])
        self.assertIn("5 / 10 (50.00%)", reporter.markdown(report))
        (self.root/"code-coverage-request.json").unlink()
        report = reporter.build_report(self.root)
        self.assertIsNone(report["codeCoverage"])
        self.assertTrue(any("Invalid or missing SIM code coverage" in e for e in report["evidenceIssues"]))

    def test_system_matrix_names_every_current_manifest_scenario(self):
        root = Path(__file__).resolve().parents[2]
        document = (root/"docs/e2e-player-simulation-plan.md").read_text(encoding="utf-8")
        section = document.split("### System-to-scenario matrix (P10-11)", 1)[1].split("## 2.", 1)[0]
        ids = []
        for line in section.splitlines():
            if not line.startswith("| "): continue
            cells = [cell.strip() for cell in line.split("|")[1:-1]]
            if cells[1] in ("Scenario ids", "No current manifest scenario"): continue
            ids.extend(cells[1].split(", "))
        manifest = json.loads((root/"parity-artifacts/e2e/scenarios.json").read_text())
        self.assertEqual(sorted(row["id"] for row in manifest), sorted(ids))

    def test_checked_in_collector_and_settings_match_receipt_contract(self):
        root = Path(__file__).resolve().parents[2]
        project = ET.parse(root/"tests/Aion.Simulation.Tests/Aion.Simulation.Tests.csproj").getroot()
        reference = project.find(".//PackageReference[@Include='coverlet.collector']")
        self.assertEqual("6.0.4", reference.get("Version"))
        self.assertEqual("all", reference.findtext("PrivateAssets"))
        settings = ET.parse(root/"scripts/sim/coverage.runsettings").getroot()
        self.assertEqual(coverage.SETTINGS, {e.tag: e.text for e in settings.find(".//Configuration")})

    def test_full_report_unions_raw_children_and_prefixes_their_evidence(self):
        import shutil
        root = self.root/"full"
        root.mkdir()
        steps, children = [], {}
        for index, scenario in enumerate(("L0", "M1")):
            name = f"sim-{index}"
            directory = root/name
            directory.mkdir()
            for source in ("code-coverage-request.json", "code-coverage-result.json", "coverage.runsettings"):
                shutil.copyfile(self.root/source, directory/source)
            shutil.copytree(self.root/"attachment", directory/"attachment")
            request = dict(self.request, scenarios=[scenario])
            (directory/"code-coverage-request.json").write_text(json.dumps(request), encoding="utf-8")
            receipt = copy.deepcopy(self.result)
            receipt["requestSha256"] = hashlib.sha256((directory/"code-coverage-request.json").read_bytes()).hexdigest()
            if index == 1:
                raw = copy.deepcopy(self.raw)
                for classes in raw["Aion.GameServer.dll"].values():
                    method = classes["Example"]["M()"]
                    method["Lines"]["11"] = 1
                    method["Branches"][1]["Hits"] = 1
                (directory/"attachment/coverage.json").write_text(json.dumps(raw), encoding="utf-8")
                receipt["artifacts"]["coverage.json"]["sha256"] = hashlib.sha256((directory/"attachment/coverage.json").read_bytes()).hexdigest()
            (directory/"code-coverage-result.json").write_text(json.dumps(receipt), encoding="utf-8")
            outcome = dict(self.outcome, status="passed", scenarios=[scenario])
            children[directory] = dict(run="run-a", mode="SIM", status="passed", runner=outcome, scenarios=[dict(id=scenario)],
                codeCoverage=coverage.collect(directory, outcome), fingerprints=[], heartbeatPeaks=[], resourcePeaks=[], packetCoverage=[], coverage=[], limitations=[])
            steps.append(dict(id=name, kind="Sim", scenarios=[scenario]))
        (root/"suite-plan.json").write_text(json.dumps(dict(run="run-a", steps=steps)), encoding="utf-8")
        (root/"suite-results.jsonl").write_text("".join(json.dumps(dict(id=s["id"], kind="Sim", status="Passed", durationSeconds=1))+"\n" for s in steps), encoding="utf-8")
        report = {key: [] for key in ("steps", "scenarios", "fingerprints", "heartbeatPeaks", "resourcePeaks", "packetCoverage", "coverage", "limitations", "evidenceIssues")}
        with patch.object(reporter, "build_report", side_effect=lambda directory: children[directory]):
            reporter.collect_suite(root, dict(run="run-a"), report)
        self.assertEqual(dict(covered=10, total=10, fraction=1), report["codeCoverage"]["directories"][0]["lines"])
        self.assertEqual(2, report["codeCoverage"]["collectedProcesses"])
        for index, process in enumerate(report["codeCoverage"]["processes"]):
            self.assertTrue(all(source["path"].startswith(f"sim-{index}/") for source in process["sources"]))


if __name__ == "__main__":
    unittest.main()
