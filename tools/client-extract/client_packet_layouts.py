#!/usr/bin/env python3
"""Make `out/client_packet_layouts.tsv`: what the 4.8 client reads from each server packet.

Runs Ghidra headless on the client's `bin64/game.dll` with `ghidra/DumpPacketLayouts.java`.
Ghidra and a JDK 21 are needed for this step only; `packet_layout_diff.py` reads the checked-in
TSV and needs neither.

First time, import the DLL (about twelve minutes of analysis):
    python client_packet_layouts.py --ghidra <ghidra dir> --project-dir <dir> \
        --import "C:/Program Files (x86)/Beyond Aion/bin64/game.dll"
Afterwards:
    python client_packet_layouts.py --ghidra <ghidra dir> --project-dir <dir>

`game.dll` is Themida-wrapped: its sections are merged into one, and Ghidra finds no classes until
`ghidra/SplitMergedSections.java` has restored them, so the import runs it as the pre-script.
"""
from __future__ import annotations

import argparse
import os
import pathlib
import subprocess
import sys
import tempfile

HERE = pathlib.Path(__file__).resolve().parent
SCRIPTS = HERE / "ghidra"


def headless(ghidra: pathlib.Path, args: list[str], log: pathlib.Path) -> None:
    launcher = ghidra / "support" / ("analyzeHeadless.bat" if os.name == "nt" else "analyzeHeadless")
    env = dict(os.environ)
    env.setdefault("GHIDRA_HEADLESS_MAXMEM", "12G")
    # stdin is closed so that the launcher's "press any key" after a failure cannot hang the run.
    with log.open("w", encoding="utf-8", errors="replace") as out:
        done = subprocess.run([str(launcher), *args], stdin=subprocess.DEVNULL, stdout=out, stderr=subprocess.STDOUT, env=env)
    text = log.read_text(encoding="utf-8", errors="replace")
    failed = [line for line in text.splitlines() if line.startswith("ERROR") and "SCRIPT ERROR" in line or "Abort due to" in line]
    if done.returncode != 0 or failed:
        sys.exit(f"Ghidra failed; see {log}\n" + "\n".join(failed[:5]))


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--ghidra", type=pathlib.Path, default=os.environ.get("GHIDRA_INSTALL_DIR"),
                    help="Ghidra install directory (default: GHIDRA_INSTALL_DIR)")
    ap.add_argument("--project-dir", type=pathlib.Path, required=True, help="directory that holds the Ghidra project")
    ap.add_argument("--project", default="aion48/bin64", help="project name and folder (default: aion48/bin64)")
    ap.add_argument("--program", default="game.dll", help="program name in the project (default: game.dll)")
    ap.add_argument("--import", dest="dll", type=pathlib.Path, help="import this bin64/game.dll first, replacing the program")
    ap.add_argument("--out", type=pathlib.Path, default=HERE / "out/client_packet_layouts.tsv")
    args = ap.parse_args()
    if not args.ghidra or not (pathlib.Path(args.ghidra) / "support").is_dir():
        ap.error("--ghidra (or GHIDRA_INSTALL_DIR) must be a Ghidra install directory")
    ghidra = pathlib.Path(args.ghidra)
    project = [str(args.project_dir), args.project]

    if args.dll:
        print(f"importing {args.dll} (auto-analysis takes about twelve minutes)")
        headless(ghidra, project + ["-import", str(args.dll), "-overwrite", "-scriptPath", str(SCRIPTS),
                                    "-preScript", "SplitMergedSections.java"], args.project_dir / "import.log")

    with tempfile.TemporaryDirectory() as tmp:
        raw = pathlib.Path(tmp) / "layouts.tsv"
        headless(ghidra, project + ["-process", args.program, "-noanalysis", "-readOnly", "-scriptPath", str(SCRIPTS),
                                    "-postScript", "DumpPacketLayouts.java", str(raw)], args.project_dir / "layouts.log")
        if not raw.is_file():
            sys.exit(f"the script wrote nothing; see {args.project_dir / 'layouts.log'}")
        text = raw.read_text(encoding="utf-8").replace("\r\n", "\n")
    rows = [line for line in text.splitlines() if line[:1].isdigit()]
    args.out.write_text(text, encoding="utf-8", newline="\n")
    handled = sum(1 for r in rows if r.split("\t")[1] != "default")
    print(f"{args.out}: {len(rows)} opcodes, {handled} handled by the client")
    return 0


if __name__ == "__main__":
    sys.exit(main())
