# Phase 9 geodata loading measurements

Measured 2026-09-19 on the development i7-14700K / 64 GiB machine, Windows,
.NET 10.0.9. Java reference: `ce54b7931546cddafb970d20c9f71fec6d48c83b`.
These are fresh test-host processes with warm OS file caches, not cold-disk benchmarks.

## Method

`GeoWorldLoaderTests.ShippedGeoLoadMeasurementIncludesCollisionPreload` first loads
the real static data, then measures the geo loader, including PNG decoding, mesh
and placement loading, material-zone registration and completed parallel BIH
collision preloading. Production schedules that last operation in the background;
the measurement explicitly waits for it. No database is involved in this test.

Managed memory is measured after a full GC before and after loading. Working set
and peak working set are for the entire test host, including static data and the
test runner, not just geo. Retained managed delta is not peak allocation. Two
independent runs per profile produced the same object counts.

| Profile | Load/preload time (runs A / B) | Retained managed delta (A / B) | Total working set after (A / B) | Peak test-host working set (A / B) |
|---|---:|---:|---:|---:|
| Poeta + Ishalgen | 676 / 684 ms | 15.52 / 15.69 MiB | 642.28 / 618.68 MiB | 659.36 / 631.46 MiB |
| All maps | 2,407 / 2,317 ms | 455.18 / 455.17 MiB | 1,069.31 / 1,061.82 MiB | 1,071.09 / 1,064.46 MiB |

| Profile | Selected maps | Maps with placements | Heightmap maps | Material-map maps | Placed entities | Geometries | Distinct referenced meshes |
|---|---:|---:|---:|---:|---:|---:|---:|
| Poeta + Ishalgen | 2 | 2 | 2 | 0 | 9,513 | 10,726 | 1,086 |
| All maps | 161 | 151 | 89 | 16 | 420,626 | 485,030 | 25,437 |

The asset directory contains 151 placement files, 78 PNGs and `models.mesh`
(70,828,016 bytes). The latter indexes 20,028 names/aliases; a name can contain
multiple meshes, so that count differs from the final mesh count. Shared terrain
filenames can serve multiple maps. Not every world-map template requires terrain
or a placement file; Java's missing-file rules are retained.

Both all-map loads were warning/error-free. The filtered loads produced exactly
the inherited `No terrain materials were loaded` warning (`fe5c9d83`): neither
starter zone ships a material PNG. The measurement test permits only this exact
message in this exact profile, not a logger category. Owner: simulation maintainer;
review by 2026-10-19 when client asset extraction is assessed. This is not a claim
that missing terrain material data has been reconstructed. No missing meshes were
reported in either profile.

## Reproduce

From the repository root, in a fresh PowerShell process per profile:

```powershell
$env:AION_GEO_PROFILE = 'starter' # or 'all'
$env:AION_GEO_RECEIPT = Join-Path (Get-Location) 'run/geo-starter.json'
dotnet test tests/Aion.GameServer.Tests/Aion.GameServer.Tests.csproj `
  --filter FullyQualifiedName~GeoWorldLoaderTests.ShippedGeoLoadMeasurementIncludesCollisionPreload `
  --logger 'console;verbosity=normal'
```

Without that environment variable the measurement is visibly skipped. Receipts
contain exact byte counts and structured loader logs; original local runs are
`run/p9-03-{starter,all}-{a,b}.{json,log}`. Do not run profiles together in one
test-host process: material-zone and shield registries have process lifetimes.

## Deployment implication and limits

D11 permits production geo activation once measured. Budget roughly 455 MiB more
retained managed memory for all-map geo on these assets, plus transient allocation;
this is separate from a fully spawned world's memory. The empty
`gameserver.geodata.map.ids` default loads all maps. A comma-separated filter loads
only those maps' terrain and placements, while parsing the shared mesh catalog
once; unreferenced meshes become collectible. Unknown map IDs fail startup.

This proves loading and starter spawn-height queries, not general query parity,
collision-aware bot navigation or the C9 chase scenario. Those remain P9-02 and
P9-04. No client extraction is needed to load the checked-in assets; extraction
may still be needed if query validation exposes incorrect or missing content.

The ordinary real-spawn bootstrap test passes with geo enabled. A separate Docker
DB run also completes normal `StartAsync` and its populated-world assertions,
but the legacy test then explicitly invokes the D7-deferred siege/PvP boot tail
and fails in `SiegeService.UpdateFortressNextState`. That test is **not green**;
the failure is retained and tracked in the plan's §7 #65, outside geo activation.
