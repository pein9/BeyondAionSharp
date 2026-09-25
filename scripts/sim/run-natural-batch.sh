#!/usr/bin/env bash
# Run the full natural Ishalgen Priest journey (NI-07) once per seed, sequentially, and print one summary
# line per run: verdict, duration, pulls (and how many were clean single pulls), defends, retreats,
# close-ins, deaths, quest updates, the last step reached and the first exception.
#
#   bash scripts/sim/run-natural-batch.sh <prefix> <seed>...
#   bash scripts/sim/run-natural-batch.sh smart43 1 3 4 5
#
# Runs land in $AION_NI07_BATCH_DIR (default run/natural-batch): <prefix>-full-s<seed>.log and the combat
# trace <prefix>-full-s<seed>/*.trace.jsonl (every packet and decision; see docs/natural-ishalgen-status.md
# for the trace queries used to compare batches). Each run is capped at 45 minutes; a single packet wait
# inside a run is capped at three minutes of real time, so a hang fails with a message.
set -u
prefix=${1:?usage: run-natural-batch.sh <prefix> <seed>...}; shift
root=$(cd "$(dirname "$0")/../.." && pwd)
S=${AION_NI07_BATCH_DIR:-$root/run/natural-batch}
mkdir -p "$S"
cd "$root"
dotnet build tests/Aion.Simulation.Tests -v q -nologo > /dev/null 2>&1 || { echo build failed; exit 1; }
for seed in "$@"; do
  run=$prefix-full-s$seed
  rm -rf "$S/$run"; mkdir -p "$S/$run"
  AION_SIM_DB_INTEGRATION=1 NI07_FULL_JOURNEY=1 AION_SIM_SEED=$seed AION_SIM_RUN_ID=$run AION_NI07_COMBAT_DIR="$S/$run" \
    timeout 45m dotnet test tests/Aion.Simulation.Tests --no-build -nologo \
    --filter "FullyQualifiedName~NaturalIshalgenPriestCompletesFrozenJourneyWithoutSetup" > "$S/$run.log" 2>&1
  t=$(ls "$S/$run"/*.jsonl 2>/dev/null | head -1)
  sum=$(grep -Eo "(Passed|Failed)! .*Duration: [0-9a-z ]+" "$S/$run.log" | head -1 | sed 's/ - Aion.*//')
  [ -z "$sum" ] && sum="NO VERDICT (timed out or the test host was killed: see $S/$run.log)"
  stats=$(python - "$t" <<'EOF'
import json,sys
p=sys.argv[1] if len(sys.argv)>1 else ''
c={'pull-plan':0,'defend-before-pull':0,'combat-retreat-route':0,'combat-retreat-cornered':0,'combat-range-close-in':0,'SM_DIE':0,'SM_QUEST_ACTION':0}
clean=0; last=''
try:
  for l in open(p,encoding='utf-8'):
    e=json.loads(l); k=e.get('packet')
    if k in c: c[k]+=1
    if k=='pull-plan' and not (e.get('fields') or {}).get('expectedHelpers'): clean+=1
    last=e.get('step',last)
except Exception: pass
print(f"pulls={c['pull-plan']} clean={clean} defends={c['defend-before-pull']} retreats={c['combat-retreat-route']} cornered={c['combat-retreat-cornered']} closeIns={c['combat-range-close-in']} deaths={c['SM_DIE']} quests={c['SM_QUEST_ACTION']} last=\"step\":\"{last}\"")
EOF
)
  echo "$run: $sum  $stats"
  grep -m1 -o "Exception : .\{0,400\}" "$S/$run.log" | sed 's/^/   /'
done
