# Seeded SIM regressions

These specs temporarily break existing C# behavior to prove scenario sensitivity.
They do not change the gameplay specification or introduce persistent divergences.
Run them exclusively in the existing checkout: no concurrent builds, other tests,
source edits, LIVE image builds or other mutation runners. Never commit a mutant.

## M1: real movement and starter quest

The two seeds in `m1.json` alter the ordinary position update in `CM_MOVE` and
the serialized quest status in `SM_QUEST_ACTION`. Java reference:
`../aion-server/game-server/src/com/aionemu/gameserver/network/aion/` at
`ce54b7931546cddafb970d20c9f71fec6d48c83b`, respectively
`clientpackets/CM_MOVE.java:runImpl` and
`serverpackets/SM_QUEST_ACTION.java`'s quest-state constructor / `writeImpl`.
Java applies client coordinates and sends `QuestStatus.value()` unchanged; the
unmodified C# does the same. No Java server is run.

Run in a disposable PowerShell session from the repository root:

```powershell
$env:AION_SIM_DB_INTEGRATION = '1'
$env:AION_SIM_RUN_ID = 'p10-08-m1-example' # choose a new id
$env:AION_SIM_SHARD = 'shard-02'
$env:AION_SIM_SHARD_COUNT = '11'
$env:AION_SIM_PROCESS_KEY = 'shard-02'
$env:AION_SIM_TIER = 'Fast'
$env:AION_SIM_SEED = '73'
python tools/client-extract/run_mutations.py parity-artifacts/e2e/mutations/m1.json `
  --project tests/Aion.Simulation.Tests/Aion.Simulation.Tests.csproj `
  --filter FullyQualifiedName~SimulationFastScenarioTests.ManifestScenariosRunInFixedProcessOrder `
  --results-directory run/p10-08-m1-example `
  --name-prefix Aion.Simulation.Tests.
```

With the current manifest, `Fast`, eleven shards and `shard-02` select **M1 only**:
one ordinary Asmodian bot, real encrypted packet processing, shipped geometry,
movement to Asak/Vandar and quest 2101 completion. Shard count is a partitioning
parameter, not a bot count; only this one process is started. Recheck selection
against `scenarios.json` if the manifest changes. The fixture creates/drops its
throwaway `aion_gs_sim_*` database on Docker MySQL; no local MySQL is allowed.
Virtual epoch is `2026-09-16T08:59:00Z`, zone UTC, normal geo-on SIM configuration.

The runner builds first, then tests with `--no-build`. A passing baseline is required;
each mutation gets its own logs/TRX/scenario artifacts and the exact same test set
must execute without skips. Both source files are restored byte-for-byte, then a
final rebuild and clean baseline test clear mutated binaries. Inspect the failed
TRX messages/stacks to confirm the intended assertion caught the regression, not
an unrelated fixture failure. This is sensitivity evidence for M1, not full-game
mutation coverage or a substitute for real-client capture.

The default project remains `Aion.GameServer.Tests` for existing AI callers. Full
test names are preserved; `--name-prefix` only shortens console output. All mutations
must be caught and the restored baseline green for exit 0. Survived, did-not-compile,
skipped/empty/aborted/inconsistent test runs and prerequisite failures exit nonzero.
Artifacts are never reused. A checkout-local lock excludes cooperating mutation
runners; it cannot block other tools or editors. After a hard process kill, inspect
the lock, source hashes and `*-original.bin` backups before manual recovery. A
concurrent edit stops restoration rather than overwriting someone else's work;
reconcile it and rebuild before running other tests.

The report records the base Git revision, runner hash, selected SIM environment,
original/mutated source hashes and restoration flags. Failed TRX files are expected
for caught mutations; they are not green scenario runs. The runner's final status
is green only for a passing baseline, all mutations caught, exact source restoration
and a passing rebuilt baseline with the same test selection. Timeout cleanup must
terminate the owned process tree; uncertain cleanup aborts the batch.
