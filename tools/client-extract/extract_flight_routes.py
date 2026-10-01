"""Extract the client's flight-transporter routes for the natural bot (AK-08, the maintainer's 2026-10-01 note).

A flight transporter (``npc_teleporter.xml`` ``type="FLIGHT"``) starts a flight with ``START_FLYTELEPORT``; the *client*
then flies a route from its own data (``Data/FlightPath/FlightPath.pak``, CryEngine ``.seq`` track sequences), reporting
``CM_MOVE_IN_AIR``, and lands with ``LAND_FLYTELEPORT`` (Java ``TeleportService.teleport``, ``CM_EMOTION``). The server's
``flypath_template.xml`` keys its entries by location id and does not match the 4.8 Altgard routes, so the routes come
from the client:

* each route is the ``SoloSelf`` (or ``soloself``) node's position track (ParamId 1): timed keys from the origin pad to the landing;
* a server FLIGHT location is matched to the route whose first key lies at the origin teleporter's spawn and whose last
  key lies at a teleporter of the destination airport (client_airports id == the server's loc_id; client_airline id ==
  the server's ``teleportId``, its ``cur_airport_name`` names the airport it stands at).

Locations the server has but no route matches are refused and counted; routes the server never offers are not emitted.

Usage:
    python extract_flight_routes.py "C:/Program Files (x86)/Beyond Aion" ../../game-server/data/static_data \
        ../../parity-artifacts/e2e/natural-flight-routes.json
"""

from __future__ import annotations

import argparse
import json
import math
import pathlib
import re
import xml.etree.ElementTree as ET

import aionpak
import bxml

PAD_RADIUS = 15.0   # a route starts at the origin teleporter's pad
LANDING_RADIUS = 30.0  # and lands by a teleporter of the destination airport


def _spawns(static_data: pathlib.Path) -> dict[int, list[tuple[int, tuple[float, float, float]]]]:
    """npc_id -> [(map_id, (x, y, z))] over every shipped NPC spawn file."""
    found: dict[int, list[tuple[int, tuple[float, float, float]]]] = {}
    for path in sorted((static_data / "spawns" / "Npcs").glob("*.xml")):
        match = re.match(r"(\d+)_", path.name)
        if not match:
            continue
        map_id = int(match.group(1))
        for spawn in ET.parse(path).getroot().iter("spawn"):
            npc = int(spawn.get("npc_id"))
            for spot in spawn.findall("spot"):
                found.setdefault(npc, []).append((map_id, (float(spot.get("x")), float(spot.get("y")), float(spot.get("z")))))
    return found


def _routes(client_root: pathlib.Path) -> tuple[dict[str, list[tuple[float, tuple[float, float, float]]]], ET.Element, ET.Element]:
    routes: dict[str, list[tuple[float, tuple[float, float, float]]]] = {}
    airline = airports = None
    for entry in aionpak.read_pak(client_root / "Data" / "FlightPath" / "FlightPath.pak"):
        if entry.name == "client_airline.xml":
            airline = bxml.decode(entry.data)
        elif entry.name == "client_airports.xml":
            airports = bxml.decode(entry.data)
        elif entry.name.lower().endswith(".seq"):
            root = ET.fromstring(entry.data.decode("utf-8", errors="replace"))
            for node in root.iter("Node"):
                if (node.get("Name") or "").lower() != "soloself":  # both spellings ship
                    continue
                keys = [(float(key.get("time")), tuple(float(v) for v in key.get("value").split(",")))
                        for track in node.findall("Track") if track.get("ParamId") == "1" for key in track.findall("Key")]
                if len(keys) >= 2:
                    routes[entry.name[:-4]] = sorted(keys)
    if airline is None or airports is None:
        raise SystemExit("FlightPath.pak has no client_airline.xml or client_airports.xml")
    return routes, airline, airports


def _distance(a, b) -> float:
    return math.dist(a[:2], b[:2])


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("client_root")
    ap.add_argument("static_data")
    ap.add_argument("out_json")
    args = ap.parse_args()
    static_data = pathlib.Path(args.static_data)
    routes, airline, airports = _routes(pathlib.Path(args.client_root))
    spawns = _spawns(static_data)
    airport_names = {int(a.findtext("id")): a.findtext("name") for a in airports}
    airline_at = {int(a.findtext("id")): a.findtext("cur_airport_name") for a in airline}

    teleporters = ET.parse(static_data / "npc_teleporter.xml").getroot()
    npcs_by_teleport: dict[int, list[int]] = {}
    for template in teleporters.iter("teleporter_template"):
        if template.get("npc_ids") and template.get("teleportId"):
            npcs_by_teleport.setdefault(int(template.get("teleportId")), []).extend(int(n) for n in template.get("npc_ids").split())

    def pads(airport_id: int) -> list[tuple[int, tuple[float, float, float]]]:
        name = airport_names.get(airport_id)
        return [spot for teleport, at in airline_at.items() if at == name
                for npc in npcs_by_teleport.get(teleport, []) for spot in spawns.get(npc, [])]

    emitted, refused = [], []
    for template in teleporters.iter("teleporter_template"):
        if not template.get("npc_ids"):
            continue
        for location in template.iter("telelocation"):
            if location.get("type") != "FLIGHT":
                continue
            loc_id, teleport_id = int(location.get("loc_id")), int(location.get("teleportid"))
            for npc in (int(n) for n in template.get("npc_ids").split()):
                for map_id, origin in spawns.get(npc, []):
                    landings = [spot for spot in pads(loc_id) if spot[0] == map_id]
                    best = None
                    for name, keys in routes.items():
                        start, end = keys[0][1], keys[-1][1]
                        if _distance(start, origin) > PAD_RADIUS:
                            continue
                        near = min((_distance(end, spot) for _, spot in landings), default=math.inf)
                        if near <= LANDING_RADIUS and (best is None or near < best[0]):
                            best = (near, name, keys)
                    if best is None:
                        refused.append(f"{npc}/{loc_id}")
                        continue
                    _, name, keys = best
                    emitted.append({
                        "npcId": npc, "mapId": map_id, "locationId": loc_id, "teleportId": teleport_id,
                        "price": int(location.get("price")), "route": name, "seconds": keys[-1][0],
                        "keys": [{"t": round(t, 3), "x": round(p[0], 3), "y": round(p[1], 3), "z": round(p[2], 3)} for t, p in keys],
                    })
    emitted.sort(key=lambda route: (route["mapId"], route["npcId"], route["locationId"]))
    out = pathlib.Path(args.out_json)
    out.write_text(json.dumps({
        "schemaVersion": 1,
        "generatedBy": "tools/client-extract/extract_flight_routes.py (do not hand-edit)",
        "routes": emitted,
    }, indent=1) + "\n", encoding="utf-8")
    print(f"emitted {len(emitted)} flight routes; refused {len(refused)} server FLIGHT locations with no matching client route")


if __name__ == "__main__":
    main()
