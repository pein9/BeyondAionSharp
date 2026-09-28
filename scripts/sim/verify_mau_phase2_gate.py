#!/usr/bin/env python3
"""Audit the frozen Phase 2 evidence and report the adoption decision."""

import hashlib
import json
from pathlib import Path
import tarfile


ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT / "docs"
RUN_ROOT = ROOT / "run/bot-learning-phase2/current"
CHECKOUT_HASHES = DOCS / "bot-learning-phase2-checkout-hashes.json"


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


def sha_lf(path):
    return hashlib.sha256(path.read_bytes().replace(b"\r\n", b"\n")).hexdigest()


def matches_receipt(path, original_digest, checkout_hashes):
    if sha(path) == original_digest:
        return True
    relative = str(path.relative_to(ROOT)).replace("\\", "/")
    alternate = checkout_hashes.get(relative)
    return (alternate is not None and alternate["originalSha256"] == original_digest
            and sha_lf(path) == alternate["normalizedLfSha256"])


def expect(condition, message):
    if not condition:
        raise ValueError(message)


def main():
    freeze_path = DOCS / "bot-learning-phase2-frozen.json"
    freeze = read(freeze_path)
    checkout_hashes = read(CHECKOUT_HASHES)["files"]
    lock = read(DOCS / "bot-learning-phase0-lock.json")
    expect(freeze["status"] == "frozen", "Candidate was not frozen")
    expect(matches_receipt(ROOT / freeze["policyFile"], freeze["policySha256"], checkout_hashes),
           "Policy source changed")
    expect(matches_receipt(ROOT / freeze["developmentReport"], freeze["developmentReportSha256"],
                           checkout_hashes),
           "Development report changed")
    for path, digest in freeze["sourceHashes"].items():
        expect(matches_receipt(ROOT / path, digest, checkout_hashes),
               f"Frozen source changed: {path}")

    reports = {}
    local_traces_checked = 0
    for split, expected_seeds in (("evaluation", list(range(101, 121))),
                                  ("regression", [1, 3, 4, 5])):
        report = read(DOCS / f"bot-learning-phase2-{split}.json")
        expect(report["complete"] and report["split"] == split, f"Incomplete {split}")
        expect(matches_receipt(freeze_path, report["freezeSha256"], checkout_hashes),
               f"Wrong {split} freeze")
        expect(report["moduleId"] == freeze["moduleId"], f"Wrong {split} module")
        expect(report["seeds"] == expected_seeds == lock["seedSplit"][split],
               f"Wrong {split} seeds")
        expect(len(report["pairs"]) == len(expected_seeds) * 2, f"Wrong {split} pair count")
        seen = set()
        for pair in report["pairs"]:
            key = pair["course"], pair["seed"]
            expect(key in {(course, seed) for course in ("GeneratorToRae", "RaeToHatata")
                           for seed in expected_seeds} and key not in seen,
                   f"Unexpected or duplicate {split} pair: {key}")
            seen.add(key)
            base, cand = pair["baseline"], pair["candidate"]
            expect(base["moduleId"] == cand["moduleId"] == freeze["moduleId"],
                   f"Unpaired {split} module: {key}")
            expect(base["courseResetSha256"] == cand["courseResetSha256"],
                   f"Unpaired {split} reset: {key}")
            expect(base["policyBaseline"] and not cand["policyBaseline"]
                   and cand["policyId"] == freeze["policyId"]
                   and cand["policySha256"] == freeze["policyReceiptSha256"],
                   f"Wrong {split} policy: {key}")
            for side in (base, cand):
                run = side["run"]
                trace = RUN_ROOT / run / f"{run}.trace.jsonl"
                if trace.exists():
                    expect(sha(trace) == side["traceSha256"], f"Trace changed: {run}")
                    local_traces_checked += 1
        reports[split] = report

    manifest = read(DOCS / "bot-learning-phase2-traces/manifest.json")
    expect(matches_receipt(DOCS / "bot-learning-phase2-evaluation.json",
                           manifest["sourceReportSha256"], checkout_hashes),
           "Archive source report changed")
    expect({item["seed"] for item in manifest["archives"]} == {108, 113, 115, 118},
           "Notable run archive missing")
    for item in manifest["archives"]:
        archive = ROOT / item["file"]
        expect(sha(archive) == item["sha256"], f"Archive changed: {archive}")
        with tarfile.open(archive, "r:gz") as packed:
            expect(set(packed.getnames()) == {entry["path"] for entry in item["entries"]},
                   f"Archive entries changed: {archive}")
            for entry in item["entries"]:
                data = packed.extractfile(entry["path"]).read()
                expect(len(data) == entry["bytes"] and
                       hashlib.sha256(data).hexdigest() == entry["sha256"],
                       f"Archive entry changed: {entry['path']}")

    eval_pairs = reports["evaluation"]["pairs"]
    regression_pairs = reports["regression"]["pairs"]
    completion_regression = [p for p in eval_pairs if p["baseline"]["status"] == "complete"
                             and p["candidate"]["status"] != "complete"]
    death_regression = [p for p in eval_pairs if p["candidate"]["deaths"] > p["baseline"]["deaths"]]
    regression_differences = [p for p in regression_pairs if any(
        p["baseline"][key] != p["candidate"][key]
        for key in ("status", "deaths", "maxObservedAttackers", "routeFailures",
                    "potions", "gameSeconds"))]
    expect(not regression_differences, "Familiar regression pairs differ")
    print(f"audited {len(eval_pairs)} unseen and {len(regression_pairs)} familiar pairs; "
          f"{local_traces_checked} local traces and {len(manifest['archives'])} complete paired archives")
    print(f"unseen completion regressions: {[(p['course'], p['seed']) for p in completion_regression]}")
    print(f"unseen death regressions: {[(p['course'], p['seed']) for p in death_regression]}")
    print("adoption decision: REJECT" if completion_regression or death_regression
          else "adoption safety gate: PASS (practical benefit still requires review)")


if __name__ == "__main__":
    main()
