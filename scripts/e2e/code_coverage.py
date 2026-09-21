"""Validate retained Coverlet inputs and union source lines / IL branch paths across SIM processes."""
import hashlib
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET

from packet_coverage import evidence, read

DIRECTORIES = ("Services", "Handlers/Instance", "Handlers/AI", "Handlers/AdminCommands", "Network/Aion/ClientPackets")
SETTINGS = dict(Format="json,cobertura", Include="[Aion.GameServer]*", IncludeTestAssembly="false",
                UseSourceLink="false", SingleHit="false", SkipAutoProps="false", ExcludeByFile="**/obj/**")


def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(",", ":")).encode()).hexdigest()


def sha(value):
    if not isinstance(value, str) or not re.fullmatch(r"[a-f0-9]{64}", value):
        raise ValueError("Invalid code coverage hash")
    return value


def count(value, minimum=0):
    if type(value) is not int or value < minimum:
        raise ValueError("Invalid code coverage integer")
    return value


def relative(value):
    if not isinstance(value, str) or not value or "\\" in value or ":" in value or value.startswith("/") or any(p in ("", ".", "..") for p in value.split("/")):
        raise ValueError("Invalid relative code coverage path")
    return value


def retained(root, name, expected=None):
    path = root / relative(name)
    source = evidence(root, path)
    if expected is not None and source["sha256"] != sha(expected):
        raise ValueError(f"Code coverage artifact hash mismatch: {name}")
    return path, source


def load(root, outcome):
    root = Path(root)
    request_path, request_source = retained(root, "code-coverage-request.json")
    result_path, result_source = retained(root, "code-coverage-result.json")
    request, result = read(request_path), read(result_path)
    if (request["schemaVersion"] != 1 or result["schemaVersion"] != 1 or request["mode"] != "SIM" or
            outcome["mode"] != "SIM" or request["run"] != outcome["run"] or result["run"] != outcome["run"] or
            request["gitSha"] != outcome["gitSha"] or request["seed"] != outcome["seed"] or
            type(request["seed"]) is not int or request["scenarios"] != outcome["scenarios"] or
            request["tier"] not in ("Fast", "Full") or request["collector"] != "coverlet.collector/6.0.4" or
            request["testFilter"] != "FullyQualifiedName~SimulationFastScenarioTests"):
        raise ValueError("Code coverage request identity/scope mismatch")
    if result["requestSha256"] != request_source["sha256"] or result["binariesRestored"] is not True or result["errors"] != []:
        raise ValueError(f"Code coverage collection/restoration failed: {result.get('errors')}")
    if set(request["binaries"]) != {"Aion.GameServer.dll", "Aion.GameServer.pdb"}:
        raise ValueError("Missing compiled code coverage input hashes")
    for value in request["binaries"].values(): sha(value)
    settings_path, settings_source = retained(root, "coverage.runsettings", request["settingsSha256"])
    try:
        settings = ET.parse(settings_path).getroot()
    except ET.ParseError as error:
        raise ValueError(f"Invalid coverage settings XML: {error}") from error
    collectors = settings.findall("./DataCollectionRunSettings/DataCollectors/DataCollector")
    if (len(collectors) != 1 or collectors[0].get("friendlyName") != "XPlat Code Coverage" or
            {e.tag: e.text for e in collectors[0].find("Configuration")} != SETTINGS):
        raise ValueError("Coverage settings narrow or change the agreed measurement")
    sources = [request_source, result_source, settings_source]
    baseline = None
    if request["baselineSha256"] is not None:
        baseline_path, baseline_source = retained(root, "code-coverage-baseline.json", request["baselineSha256"])
        sources.append(baseline_source)
        baseline = read(baseline_path)
    raw = None
    if set(result["artifacts"]) != {"coverage.json", "coverage.cobertura.xml"}:
        raise ValueError("Missing coverage attachments")
    for name, receipt in result["artifacts"].items():
        path, source = retained(root, receipt["path"], receipt["sha256"])
        if path.name != name:
            raise ValueError("Unexpected coverage attachment name")
        copies = receipt["copies"]
        if not isinstance(copies, list) or not copies or len(copies) != len(set(copies)) or receipt["path"] not in copies:
            raise ValueError("Invalid coverage attachment copies")
        for copy in copies:
            copy_path, copy_source = retained(root, copy, receipt["sha256"])
            if copy_path.name != name:
                raise ValueError("Unexpected coverage copy name")
            sources.append(copy_source)
        if name == "coverage.json": raw = read(path)
    if set(raw) != {"Aion.GameServer.dll"} or not raw["Aion.GameServer.dll"]:
        raise ValueError("Coverage must include exactly the nonempty game-server module")
    source_files = request["sourceFiles"]
    if not isinstance(source_files, dict) or not source_files:
        raise ValueError("Missing frozen source inventory")
    for name, value in source_files.items():
        relative(name)
        sha(value)
    source_root = request["sourceRoot"].replace("\\", "/").rstrip("/") + "/"
    points = dict(lines=set(), hitLines=set(), branches=set(), hitBranches=set(), files=set())
    for source, classes in raw["Aion.GameServer.dll"].items():
        normalized = source.replace("\\", "/")
        if not normalized.casefold().startswith(source_root.casefold()):
            raise ValueError(f"Coverage source outside frozen game-server root: {source}")
        filename = relative(normalized[len(source_root):])
        if filename not in source_files or filename in points["files"]:
            raise ValueError(f"Unfrozen or duplicate coverage source: {filename}")
        points["files"].add(filename)
        for class_name, methods in classes.items():
            for method_name, method in methods.items():
                for line, hits in method["Lines"].items():
                    if not re.fullmatch(r"[1-9][0-9]*", line):
                        raise ValueError("Invalid source line number")
                    key = (filename, int(line))
                    points["lines"].add(key)
                    if count(hits) > 0: points["hitLines"].add(key)
                for branch in method["Branches"]:
                    key = (filename, class_name, method_name, count(branch["Line"], 1), count(branch["Offset"]),
                           count(branch["EndOffset"]), count(branch["Path"]), count(branch["Ordinal"]))
                    points["branches"].add(key)
                    if count(branch["Hits"]) > 0: points["hitBranches"].add(key)
    if not points["lines"] or not points["branches"]:
        raise ValueError("Coverage has no measurable lines or branches")
    return dict(points=points, sourceFiles=source_files, sources=sources, request=request, baseline=baseline,
                sourceInventorySha256=digest(source_files), settingsSha256=settings_source["sha256"],
                pointInventorySha256=digest(dict(lines=sorted(points["lines"]), branches=sorted(points["branches"]))))


def summarize(measurements):
    if not measurements:
        raise ValueError("Cannot summarize absent code coverage")
    first = measurements[0]
    for value in measurements[1:]:
        for key in ("sourceInventorySha256", "pointInventorySha256", "settingsSha256"):
            if value[key] != first[key]:
                raise ValueError(f"Cannot merge unlike code coverage inputs: {key}")
        if value["request"]["baselineSha256"] != first["request"]["baselineSha256"]:
            raise ValueError("Cannot merge differently baselined coverage processes")
    points = {key: set().union(*(row["points"][key] for row in measurements)) for key in first["points"]}
    def within(filename, directory):
        return directory == "ALL" or filename.startswith(directory + "/")
    def metric(all_points, hits):
        total, covered = len(all_points), len(hits)
        return dict(covered=covered, total=total, fraction=covered/total if total else None)
    directories = []
    for directory in ("ALL",) + DIRECTORIES:
        select = lambda values: {p for p in values if within(p[0], directory)}
        lines, branches = select(points["lines"]), select(points["branches"])
        if not lines:
            raise ValueError(f"Coverage missing required directory: {directory}")
        directories.append(dict(directory=directory, lines=metric(lines, select(points["hitLines"])),
            branches=metric(branches, select(points["hitBranches"])),
            instrumentedFiles=sum(within(p, directory) for p in points["files"]),
            sourceFiles=sum(within(p, directory) for p in first["sourceFiles"])))
    summary = dict(schemaVersion=1, mode="SIM", collector=first["request"]["collector"],
        sourceInventorySha256=first["sourceInventorySha256"], pointInventorySha256=first["pointInventorySha256"],
        settingsSha256=first["settingsSha256"], directories=directories,
        processes=[dict(run=row["request"]["run"], tier=row["request"]["tier"], seed=row["request"]["seed"], scenarios=row["request"]["scenarios"],
                        binaries=row["request"]["binaries"], sources=row["sources"]) for row in measurements],
        limitations=["Coverlet sequence points and IL branch paths, not every textual source line or gameplay behavior.",
            "Includes test-process bootstrap and auxiliary selected tests as well as scenario execution.",
            "Lines are unioned by source file/line; branches by source file, class, method and branch identity. Shards are never added as percentages.",
            "Instrumentation changes timing and resource usage; this run is not capacity/performance evidence.",
            "Only build-generated obj files are explicitly excluded. Files without sequence points remain visible in source-file counts."])
    summary["baselineComparison"] = compare(summary, first["baseline"]) if first["baseline"] is not None else dict(comparable=False, reason="No baseline was retained at run start.")
    return summary


def collect(root, outcome):
    return summarize([load(root, outcome)])


def compare(summary, baseline):
    if baseline["schemaVersion"] != 1:
        raise ValueError("Unsupported code coverage baseline")
    reference = baseline["measurement"]
    comparable = all(summary[key] == reference[key] for key in ("sourceInventorySha256", "pointInventorySha256", "settingsSha256"))
    scopes = lambda value: sorted((p["tier"], p["seed"], tuple(p["scenarios"])) for p in value["processes"])
    # Early draft receipts did not retain the baseline RNG seed. Keep their observations,
    # but never infer that their workload matches a seed-sensitive run.
    has_seeds = all("seed" in p for value in (summary, reference) for p in value["processes"])
    if has_seeds and any(type(p["seed"]) is not int for value in (summary, reference) for p in value["processes"]):
        raise ValueError("Invalid code coverage workload seed")
    comparable = comparable and has_seeds and scopes(summary) == scopes(reference)
    result = dict(comparable=comparable, policy="Informational deltas, not a line/branch regression gate.", directories=[])
    if not comparable:
        result["reason"] = "Source, instrumentation, point inventory, RNG seed or selected workload differs or is unavailable; no delta is inferred."
        return result
    prior = {row["directory"]: row for row in reference["directories"]}
    for row in summary["directories"]:
        previous = prior[row["directory"]]
        delta = dict(directory=row["directory"])
        for key in ("lines", "branches"):
            if row[key]["total"] != previous[key]["total"]:
                raise ValueError("Matching code inventory has inconsistent baseline totals")
            delta[key] = dict(coveredDelta=row[key]["covered"]-previous[key]["covered"],
                fractionDelta=row[key]["fraction"]-previous[key]["fraction"] if row[key]["fraction"] is not None else None)
        result["directories"].append(delta)
    return result
