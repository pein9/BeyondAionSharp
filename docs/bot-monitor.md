# Bot monitor map

Natural SIM runs serve the read-only monitor at <http://127.0.0.1:17880/>.
LIVE runs use the same dashboard with `--dashboard-port 17880`.

The map uses the portal's Ishalgen artwork and `calibrated-game-y-x`
coordinates. It switches to the portal's Ataxiar coordinate grid during that
quest segment. Other maps report that artwork is unavailable.

- The cyan marker follows the selected character's observed/client-estimated
  position, refreshed once per second. Follow can be disabled by dragging or
  scrolling; Whole map fits the image and all spawn references.
- Hollow markers are all normal NPC, monster and gatherable placements from
  this checkout's shipped XML, including conditional and pooled placements.
  They are reference locations, not a claim that every creature is currently alive.
- Solid markers are nearby objects from the bot's packet-derived world model.
  Dashed segments show their received movement destinations, not confirmed
  arrival. Crosses indicate observed corpses. Objects disappear when the client
  forgets them; map/reconnect changes clear the character trail.
- Search filters by name or template ID. Layers, pan and zoom persist through
  state refreshes. Hover or tap a marker for its name, ID and coordinates.

Map references are used only by the browser display, never by bot decisions.
No server rules, portal source files or 5.8 comparison overlays are changed.

## Refreshing assets

```powershell
python scripts/sim/import-dashboard-maps.py
node scripts/sim/test-dashboard-map.cjs
```

The importer defaults to `../aion-portal` (override with `--portal PATH`), verifies
the portal manifest's artwork hashes, copies the two images and calibration,
and snapshots the local regular NPC/gatherable XML. Generated files are embedded
in Aion.Bots, so a running monitor needs neither the portal checkout nor its
HTTP server. Source hashes in `maps/catalog.json` make stale spawn snapshots
detectable by the check above (XML line endings are normalized); rerun the
importer after spawn or NPC/gatherable template changes.

## Preview without a bot

```powershell
python scripts/sim/preview-dashboard.py
# Or inspect a saved response captured from a running monitor's /api/state:
python scripts/sim/preview-dashboard.py --snapshot run/dashboard-map-state.json
```

This serves the current source assets and labels the page **Map preview — no bot
running**. Saved positions are frozen; they are never presented as a live run.
Stop the preview before starting a bot on the same port (or use `--port 17881`).

## Validation (2026-09-26)

Four dashboard tests passed, including HTTP map assets and immutable observed
object movement/reload coverage. The map check verifies calibration anchors,
artwork hashes and every shipped spawn placement. The SIM checkpoint
`dashboard-map-q2004-s1` passed through Q2004; the browser displayed the moving
Priest and packet-observed objects. This is a dashboard smoke run, not NI-09.

The full ordinary-rate LIVE run `ni09-live-a4` subsequently completed all 41
quests with zero deaths and final relog persistence verified. Its moving map was
observed during the run; the post-run preview uses a saved 41-quest snapshot and
explicitly indicates that no bot is running.
