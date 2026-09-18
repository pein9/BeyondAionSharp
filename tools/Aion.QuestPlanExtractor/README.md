# Custom quest draft extractor

This tool uses the .NET SDK's Roslyn parser to reduce every obtainable custom
quest handler to deterministic bot-authoring evidence:

- `Register()` event and NPC registrations, including looped integer arrays;
- `OnDialogEvent` branch conditions, action names and return results;
- exact spawn, teleport and instance call sites that need non-dialog handling.

The checked-in output is `parity-artifacts/e2e/custom-quest-handler-drafts.json`.
Regenerate it from the repository root with:

```powershell
dotnet run --project tools/Aion.QuestPlanExtractor -- `
  --output parity-artifacts/e2e/custom-quest-handler-drafts.json
```

`scripts/ci/check-custom-quest-drafts.ps1` rejects drift. The SIM/LIVE bot
library combines this manifest with the retail client page/action map. Its
executable, hand-written special-handler scripts capture and compare
world-object/respawn state for spawn calls, player map/position for teleports,
and map/instance for instance creation or entry.
