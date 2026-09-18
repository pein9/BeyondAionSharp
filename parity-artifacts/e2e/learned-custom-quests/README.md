# SIM-learned custom quest dialogs

Each JSON file here is a complete dialog action sequence learned through real
SIM packet ingress and approved for deterministic LIVE replay. The LIVE loader
rejects partial explorer output, unsupported schema versions, and scripts that
do not match the quest being run.

`1100.json` is the first checked-in proof: SIM learned its two accepted actions
from the Roslyn handler draft plus the 4.8 client page/action map, then the LIVE
Q3 bot replayed the saved sequence against the Docker socket stack.
