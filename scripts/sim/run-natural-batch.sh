#!/usr/bin/env bash
# Run the full natural Ishalgen Priest journey (NI-07) once per seed, sequentially, and print one summary
# line per run: verdict, duration, pulls (and how many were clean single pulls), defends, retreats,
# close-ins, deaths, quest updates, the last step reached and the first exception.
#
#   bash scripts/sim/run-natural-batch.sh <prefix> <seed>...
#   bash scripts/sim/run-natural-batch.sh smart43 1 3 4 5
#   NA_ASCENSION=1 bash scripts/sim/run-natural-batch.sh na25 1   # continue over the Ascension bridge (NA-25)
#
# With NA_ASCENSION=1 the summary also names the bridge's final outcome, whether its endpoint was verified across
# the relog, and the movies, help items and deaths the bridge recorded (docs/natural-ascension-altgard.md).
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
  if [ "${NA_ASCENSION:-}" = "1" ]; then
    stats="$stats $(python - "$S/$run" <<'EOF'
import json,sys,pathlib
d=pathlib.Path(sys.argv[1])
def load(name):
    try: return json.loads((d/name).read_text(encoding='utf-8'))
    except Exception: return None
stop=load('bridge-stop.json') or {}; done=load('bridge-completion.json') or {}; help=load('help-items.json') or {}
movies=supplied=used=0
for p in d.glob('*.trace.jsonl'):
    for l in open(p,encoding='utf-8'):
        k=json.loads(l).get('packet')
        movies+=k in ('movie-skipped','movie-watched'); used+=k=='help-item-used'
supplied=len((help.get('helpItems') or {}).get('supplied') or [])
after=done.get('after') or {}
print(f"bridge={(stop.get('stop') or {}).get('Outcome','none')} endpointVerified={bool(done.get('verified'))} "
      f"class={after.get('PlayerClass')} level={after.get('Level')} map={after.get('MapId')} "
      f"movies={movies} helpSupplied={supplied} helpUsed={used}")
EOF
)"
  fi
  echo "$run: $sum  $stats"
  grep -m1 -o "Exception : .\{0,400\}" "$S/$run.log" | sed 's/^/   /'
done
