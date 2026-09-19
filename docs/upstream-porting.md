# Upstream Java porting

Port merged Java changes in strict `4.8` history order. One upstream Java commit produces one completed C# commit or one explicit blocked record. Never merge or cherry-pick Java history into C#.

## Repository policy

- C# work happens locally on `main`; do not create port branches or pull requests.
- The C# repository must contain only `main` and must be clean before a package is prepared.
- The Java checkout is read-only behavioral reference data.
- `docs/upstream-port-state.json` is the queue cursor. Do not select a later commit manually.
- `docs/upstream-port-log.md` is the human ledger.
- Generated patches, prompts, reports, and logs belong under ignored `artifacts/upstream/`.

## Per-commit workflow

1. Run `scripts/upstream/scan-upstream.ps1` and confirm the first pending merged commit.
2. Run `scripts/upstream/prepare-next.ps1`; use its generated `prompt.md` and exact Java patch.
3. Explain the behavior, map every affected Java artifact to C#, and port only that commit.
4. Add focused regression coverage and run `scripts/upstream/validate-port.ps1`.
5. Run `scripts/upstream/complete-port.ps1` with the reviewed status and evidence-based notes.
6. Review and stage only the intended files, then commit with the generated `commit-message.txt`.
7. Run `scripts/upstream/verify-port.ps1` before considering the queue item complete.

`scripts/upstream/test-upstream-automation.ps1` self-tests these scripts against throwaway repositories; run it after changing any of them.

## Status rules

| CLI status | Ledger status | Advance cursor | Validation required |
|---|---|---:|---:|
| `ported` | Ported | Yes | Yes |
| `direct-data` | Direct data carryover | Yes | Yes |
| `not-applicable` | Not applicable | Yes | No, but precise evidence is required |
| `blocked` | Blocked | No | No, but the missing prerequisite is required |

A blocked record may be resolved later. The later completed C# commit uses the same Java SHA, replaces the ledger row, and advances the cursor. Later Java commits remain ineligible until then.

## Commit contract

Every tracker decision is a local C# commit with exactly one trailer pair:

```text
Upstream-Java-SHA: <40-character-sha>
Port-Status: ported|direct-data|not-applicable|blocked
```

## Review rules

- Java describes intent and observable behavior; C# uses established local infrastructure.
- Preserve ordering, null behavior, enum and ordinal semantics, numeric overflow, timing, packet layouts, persistence effects, and data compatibility.
- Do not bundle cleanup, redesign, or adjacent Java commits.
- Language-neutral XML, SQL, and configuration may be carried directly only after C# loader and model compatibility is verified.
- A green build alone is insufficient. Require focused regression coverage or a concrete explanation of the validation boundary.

## Mechanical Java-to-C# mappings

| Java | C# | Notes |
|---|---|---|
| `LoggerFactory.getLogger(X.class)` | `AionLog.For(nameof(X))` | The returned logger is late-bound: it resolves the current `ILoggerFactory` when each message is written. |
| `LoggerFactory.getLogger("CATEGORY")` | `AionLog.For("CATEGORY")` | Preserve named Java categories exactly, including dedicated audit and gameplay log channels. |
| `System.currentTimeMillis()` | `SystemClock.CurrentMillis()` | Gameplay time must remain on the shared overridable clock; use `SystemClock.UtcNow()` when the C# API requires an instant. |
| `ImageIO.read` terrain rasters in `GeoWorldLoader` | `GeoEngine.PngReader` | Shipped PNGs are non-interlaced 16-bit gray heights or 8-bit gray/indexed materials. Preserve raw unsigned samples/palette indices, not converted colors. PNG filters 0–4 and split IDAT chunks are tested. |
| `GeoWorldLoader.load` / `GeoService.init` | `GeoWorldLoader.Load` / `GeoService.Init` | Load real binary meshes/placements, terrain, material zones and background parallel collision data. Empty C# `gameserver.geodata.map.ids` preserves all-map production loading; the optional explicit map filter is a SIM infrastructure seam. Measurement tests wait for collision preloading; production retains Java's background behavior. |
| `services.craft.CraftSkillUpdateService` | `Services.Craft.CraftSkillUpdateService` | Single canonical implementation; `getProfessionByNpc` maps to `Profession?` (unknown NPC is null, not enum ordinal zero). `EconomyServiceParityTests` and `NullableEnumEdgeParityTests` guard consolidation and all 36 trainer mappings. |
| `InventoryDAO.store` catches `SQLException` | `InventoryDAO.Store` catches `MySqlException` | Do not catch programming/infrastructure exceptions as SQL failures; `EconomyServiceParityTests` pins the catch contract and non-SQL propagation. |
