"""Extract Haramel's client-side moving platform, without changing server content.

The 4.8 mission's Moving_Collision entity uses a single looping CGA TCB3 controller.
CGF layout follows beyond-aion/aion-geobuilder's CgfLoader and niftools' cgf.xml.
This extractor refuses other controllers/transforms rather than approximating them.
Usage: python extract_haramel_elevator.py CLIENT_ROOT OUT_JSON
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import pathlib
import struct
import xml.etree.ElementTree as ET

from aionpak import read_pak
from index_paks import entry_names


def extract(client: pathlib.Path) -> dict:
    mission = next(e.data for e in read_pak(client / "Levels/idnovice/level.pak")
                   if e.name == "mission_mission0.xml")
    entities = [e for e in ET.fromstring(mission).iter("Entity") if
                e.get("Layer") == "Moving_Collision" and
                e.find("Properties").get("object_Model").lower().endswith("pr_g_elevator_03c_loop.cga")]
    assert len(entities) == 1
    entity = entities[0]
    assert entity.get("SyncAnimation") == "1" and entity.get("Angles") is None and entity.get("Scale") is None
    animation = entity.find("Properties/Animation")
    assert animation.get("bLoop") == "1" and animation.get("bPlaying") == "1" and animation.get("Speed") == "1"
    model = entity.find("Properties").get("object_Model").replace("\\", "/")
    name = model.removeprefix("levels/common/").lower()
    candidates = [p for p in sorted((client / "Levels/common").glob("Mesh_Meshes_*.pak"))
                  if any(n.lower() == name for n in entry_names(p))]
    assert len(candidates) == 1
    data = next(e.data for e in read_pak(candidates[0]) if e.name.lower() == name)
    assert data[:8] == b"NCAion\0\0"
    kind, version, table = struct.unpack_from("<3I", data, 8)
    assert (kind, version) == (0xFFFF0000, 0x750)
    chunks = {c[3]: c for c in (struct.unpack_from("<4I", data, table + 4 + 16 * i)
                              for i in range(struct.unpack_from("<I", data, table)[0]))}
    timing = next(c[2] for c in chunks.values() if c[0] == 0xCCCC000E)
    seconds_per_tick, ticks_per_frame = struct.unpack_from("<fI", data, timing + 16)
    first_frame, last_frame = struct.unpack_from("<2i", data, timing + 56)
    assert first_frame == 0 and ticks_per_frame > 0 and seconds_per_tick > 0
    node = next(c[2] for c in chunks.values() if c[0] == 0xCCCC000B)
    mesh_id, parent_id = struct.unpack_from("<2i", data, node + 80)
    assert parent_id == -1
    transform = struct.unpack_from("<16f", data, node + 100)
    assert transform[:12] == (1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0)
    position = struct.unpack_from("<3f", data, node + 164)
    assert struct.unpack_from("<3f", data, node + 192) == (1, 1, 1)
    position_id, rotation_id, scale_id = struct.unpack_from("<3i", data, node + 204)
    assert rotation_id == scale_id == -1
    controller = chunks[position_id]
    assert controller[0] == 0xCCCC000D
    offset = controller[2]
    controller_type, count, flags, controller_id = struct.unpack_from("<4I", data, offset + 16)
    assert controller_type == 9 and flags == 0 and controller_id == position_id
    keys = [struct.unpack_from("<i8f", data, offset + 32 + 36 * i) for i in range(count)]
    # Tension 1 gives zero Hermite tangents. Continuity/bias/ease are zero in this model.
    assert all(abs(k[4] - 1) < 1e-6 and all(abs(x) < 1e-6 for x in k[5:]) for k in keys)
    assert keys[0][1:4] == position and keys[-1][1:4] == position
    assert all(abs(k[1] - position[0]) < .001 and abs(k[2] - position[1]) < .001 for k in keys)
    mesh = chunks[mesh_id][2]
    vertex_count, _, face_count = struct.unpack_from("<3I", data, mesh + 20)
    vertices = [struct.unpack_from("<3f", data, mesh + 36 + 24 * i) for i in range(vertex_count)]
    floors: dict[float, tuple[float, list]] = {}
    for i in range(face_count):
        a, b, c, _, _ = struct.unpack_from("<5I", data, mesh + 36 + 24 * vertex_count + 20 * i)
        p, q, r = vertices[a], vertices[b], vertices[c]
        u, v = [q[j] - p[j] for j in range(3)], [r[j] - p[j] for j in range(3)]
        cross = (u[1]*v[2]-u[2]*v[1], u[2]*v[0]-u[0]*v[2], u[0]*v[1]-u[1]*v[0])
        length = math.sqrt(sum(x*x for x in cross))
        if length and cross[2] / length > .999:
            height = round(sum(x[2] for x in (p, q, r)) / 300, 4)
            area, points = floors.get(height, (0, []))
            floors[height] = (area + length / 20000, points + [p, q, r])
    floor_z = max(floors, key=lambda z: floors[z][0])
    area, floor_vertices = floors[floor_z]
    assert area > 20
    origin = [float(x) for x in entity.get("Pos").split(",")]
    world_keys = [{"millis": round(k[0] * seconds_per_tick * 1000),
                   "x": origin[0] + k[1] / 100, "y": origin[1] + k[2] / 100,
                   "z": origin[2] + k[3] / 100 + floor_z} for k in keys]
    return {"mapId": 300200000, "model": model, "entityId": int(entity.get("EntityId")),
            "entityGuid": int(entity.get("EntityGUID")), "interpolation": "TCB3 tension=1, zero tangents",
            "periodMillis": round(last_frame * ticks_per_frame * seconds_per_tick * 1000),
            "floorOffsetMetres": floor_z, "floorAreaSquareMetres": area,
            "floorBounds": [[min(v[j] for v in floor_vertices)/100, max(v[j] for v in floor_vertices)/100] for j in (0, 1)],
            "keys": world_keys, "sources": {"missionSha256": hashlib.sha256(mission).hexdigest(),
            "modelSha256": hashlib.sha256(data).hexdigest(), "modelPak": candidates[0].relative_to(client).as_posix()}}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("client_root", type=pathlib.Path)
    parser.add_argument("out_json", type=pathlib.Path)
    args = parser.parse_args()
    result = extract(args.client_root)
    args.out_json.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(f"{result['mapId']}: {len(result['keys'])} keys, {result['periodMillis']} ms loop, "
          f"floor {result['keys'][0]['z']:.3f} -> {max(k['z'] for k in result['keys']):.3f}")


if __name__ == "__main__":
    main()
