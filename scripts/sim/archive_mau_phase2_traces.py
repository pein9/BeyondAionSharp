#!/usr/bin/env python3
"""Preserve complete paired run directories for notable held-out outcomes."""

import gzip
import hashlib
import io
import json
from pathlib import Path
import tarfile


ROOT = Path(__file__).resolve().parents[2]
REPORT = ROOT / "docs/bot-learning-phase2-evaluation.json"
RUN_ROOT = ROOT / "run/bot-learning-phase2/current"
ARCHIVE_ROOT = ROOT / "docs/bot-learning-phase2-traces"


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    report = json.loads(REPORT.read_text(encoding="utf-8"))
    if not report["complete"] or len(report["pairs"]) != 40:
        raise ValueError("The held-out evaluation is incomplete")

    selected = [
        pair for pair in report["pairs"]
        if (pair["course"] == "GeneratorToRae" and pair["seed"] in (108, 113, 115, 118))
    ]
    if len(selected) != 4:
        raise ValueError("Missing notable held-out generator pairs")
    ARCHIVE_ROOT.mkdir(parents=True, exist_ok=True)
    manifest = {"schemaVersion": 1, "sourceReport": str(REPORT.relative_to(ROOT)).replace("\\", "/"),
                "sourceReportSha256": sha(REPORT), "archives": []}

    for pair in selected:
        seed = pair["seed"]
        archive_path = ARCHIVE_ROOT / f"generator-seed-{seed}-paired.tar.gz"
        entries = []
        buffer = io.BytesIO()
        with gzip.GzipFile(fileobj=buffer, mode="wb", mtime=0, filename="") as zipped:
            with tarfile.open(fileobj=zipped, mode="w") as tar:
                for policy in ("baseline", "candidate"):
                    run = pair[policy]["run"]
                    folder = RUN_ROOT / run
                    trace = folder / f"{run}.trace.jsonl"
                    if sha(trace) != pair[policy]["traceSha256"]:
                        raise ValueError(f"Trace hash mismatch: {trace}")
                    for path in sorted(folder.iterdir()):
                        if not path.is_file():
                            raise ValueError(f"Unexpected run entry: {path}")
                        data = path.read_bytes()
                        member = f"{run}/{path.name}"
                        info = tarfile.TarInfo(member)
                        info.size = len(data)
                        info.mode = 0o644
                        info.mtime = 0
                        tar.addfile(info, io.BytesIO(data))
                        entries.append({"path": member, "sha256": hashlib.sha256(data).hexdigest(),
                                        "bytes": len(data)})
        archive_path.write_bytes(buffer.getvalue())
        manifest["archives"].append({
            "course": pair["course"], "seed": seed,
            "file": str(archive_path.relative_to(ROOT)).replace("\\", "/"),
            "sha256": sha(archive_path), "bytes": archive_path.stat().st_size,
            "entries": entries,
        })
    (ARCHIVE_ROOT / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"Archived {len(selected)} paired outcomes: {ARCHIVE_ROOT}")


if __name__ == "__main__":
    main()
