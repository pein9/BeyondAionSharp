"""Game-protocol observations from frozen catalogs, bot traces and optional clear-frame taps.

Receiving a raw body or serializing a server frame never earns structured-decoder coverage.
These measurements do not establish successful scenario, cleanup or watcher acceptance.
"""
import base64
import gzip
import hashlib
import json
from pathlib import Path
import re
from uuid import UUID


def read(path):
    def reject(value):
        raise ValueError(f"Nonfinite JSON: {value}")
    return json.loads(path.read_text(encoding="utf-8-sig"), parse_constant=reject)


def evidence(root, path):
    if not path.resolve().is_relative_to(root.resolve()):
        raise ValueError("Packet evidence escaped its run directory")
    with path.open("rb") as stream:
        digest = hashlib.file_digest(stream, "sha256").hexdigest()
    return dict(path=path.relative_to(root).as_posix(), sha256=digest, bytes=path.stat().st_size)


def rows(path):
    opener = gzip.open if path.suffix == ".gz" else open
    with opener(path, "rt", encoding="utf-8-sig") as stream:
        for index, line in enumerate(stream, 1):
            if not line.strip():
                continue
            try:
                value = json.loads(line)
                if not isinstance(value, dict):
                    raise ValueError("Expected an object")
                yield value
            except ValueError as error:
                raise ValueError(f"{path.name}:{index}: {error}") from error


def inventory(catalog, direction):
    result = {}
    opcodes = set()
    for row in catalog[direction]:
        name, opcode = row["name"], row["opcode"]
        if not isinstance(name, str) or not re.fullmatch(r"[A-Za-z_][A-Za-z_0-9]*", name):
            raise ValueError("Invalid packet name")
        if type(opcode) is not int or not 0 <= opcode <= 65535 or opcode in opcodes or name in result:
            raise ValueError("Invalid or duplicate packet registration")
        if direction == "server" and type(row["structuredDecoder"]) is not bool:
            raise ValueError("Invalid decoder inventory")
        result[name] = row
        opcodes.add(opcode)
    if not result:
        raise ValueError("Empty protocol inventory")
    return result


def inventory_hash(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(",", ":")).encode("utf-8")).hexdigest()


def collect(root, outcome):
    root = Path(root)
    catalog_path = root / "packet-catalog.json"
    catalog_evidence = evidence(root, catalog_path)
    catalog = read(catalog_path)
    if (catalog["schemaVersion"] != 1 or catalog["protocol"] != "aion-game-4.8" or
            catalog["run"] != outcome["run"] or catalog["mode"] != outcome["mode"] or
            catalog["mode"] not in ("SIM", "LIVE")):
        raise ValueError("Packet catalog identity/schema mismatch")
    for key in ("gameServerModule", "botModule"):
        UUID(catalog[key])
    client, server = inventory(catalog, "client"), inventory(catalog, "server")
    seen = {key: set() for key in ("clientSent", "serverReceived", "serverDecoded", "serverRaw", "serverTapped")}
    counts = dict.fromkeys(seen, 0)
    sources = [catalog_evidence]
    traces = sorted((root / "bots").glob("*.trace.jsonl*"))
    if not traces:
        raise ValueError("No retained bot packet traces")
    for path in traces:
        if not (path.name.endswith(".trace.jsonl") or path.name.endswith(".trace.jsonl.gz")):
            continue
        sources.append(evidence(root, path))
        if path.suffix == ".gz" and path.with_suffix("").exists():
            raise ValueError("Duplicate compressed and uncompressed bot trace")
        for row in rows(path):
            if row["run"] != outcome["run"]:
                raise ValueError("Bot trace run mismatch")
            direction = row["dir"]
            if direction == "action":
                continue  # Login/Chat actions are not game opcodes.
            if direction not in (">", "<"):
                raise ValueError("Unknown bot trace direction")
            name, fields = row["packet"], row["fields"]
            table = client if direction == ">" else server
            if name not in table or not isinstance(fields, dict):
                raise ValueError(f"Unknown packet or invalid fields: {name}")
            keys = ["clientSent"] if direction == ">" else ["serverReceived"]
            if direction == "<":
                raw = "bodyHex" in fields
                if raw:
                    if set(fields) != {"bodyHex"} or not isinstance(fields["bodyHex"], str) or not re.fullmatch(r"(?:[0-9A-Fa-f]{2})*", fields["bodyHex"]):
                        raise ValueError("Malformed raw server body")
                    keys.append("serverRaw")
                elif server[name]["structuredDecoder"]:
                    keys.append("serverDecoded")
                else:
                    raise ValueError(f"Structured trace has no registered decoder: {name}")
            for key in keys:
                seen[key].add(name)
                counts[key] += 1
    dropped = 0
    taps = sorted((root / "logs" / "gs").glob("packet-tap*.jsonl*"))
    for path in taps:
        if not (path.name.endswith(".jsonl") or path.name.endswith(".jsonl.gz")):
            continue
        if path.suffix == ".gz" and path.with_suffix("").exists():
            raise ValueError("Duplicate compressed and uncompressed packet tap")
        sources.append(evidence(root, path))
        for row in rows(path):
            if row["run"] != outcome["run"]:
                raise ValueError("Packet tap run mismatch")
            if row.get("kind") == "dropped":
                if type(row["count"]) is not int or row["count"] <= 0:
                    raise ValueError("Invalid packet tap loss count")
                dropped += row["count"]
                continue
            name = row["packet"]
            if name not in server or int(row["opcode"], 16) != server[name]["opcode"]:
                raise ValueError("Packet tap registration mismatch")
            frame = base64.b64decode(row["frameBase64"], validate=True)
            if type(row["length"]) is not int or row["length"] != len(frame) or len(frame) < 7 or int.from_bytes(frame[:2], "little") != len(frame):
                raise ValueError("Invalid retained tap frame length")
            seen["serverTapped"].add(name)
            counts["serverTapped"] += 1
    metrics = {}
    if counts["clientSent"] + counts["serverReceived"] == 0:
        raise ValueError("Retained traces contain no game packet observations")
    for key, names in seen.items():
        table = client if key == "clientSent" else server
        metrics[key] = dict(count=len(names), total=len(table), fraction=len(names)/len(table),
                            packets=sorted(names), missing=sorted(set(table)-names), records=counts[key])
    frozen = dict(client=sorted(client.values(), key=lambda r: r["opcode"]), server=sorted(server.values(), key=lambda r: r["opcode"]))
    return dict(schemaVersion=1, run=outcome["run"], mode=outcome["mode"], protocol=catalog["protocol"],
                inventory=frozen, inventorySha256=inventory_hash(frozen),
                structuredDecoders=sum(row["structuredDecoder"] for row in server.values()),
                metrics=metrics, tap=dict(available=bool(taps), droppedRecords=dropped, complete=bool(taps) and dropped == 0),
                sources=sources, limitations=["Observations only; runner, scenario, watcher and cleanup acceptance are separate.",
                    "Client sent means recorded send attempt, not server handler execution or delivery acknowledgement.",
                    "Raw received bodies and server-side serialized frames do not earn structured decoder coverage.",
                    "Tap is optional and covers all captured connections, not just bots. Drops make tap totals lower bounds.",
                    "Login and Chat protocols are excluded from the game opcode denominator."])


def compare(measurements, baseline):
    """Exact identity floors, not counts: replacing a formerly exercised opcode is a regression."""
    if baseline["schemaVersion"] != 1 or not baseline["referenceByMode"]:
        raise ValueError("Invalid/empty packet baseline")
    errors, comparisons = [], []
    for mode, expected in baseline["referenceByMode"].items():
        if mode not in ("SIM", "LIVE"):
            raise ValueError("Invalid baseline mode")
        if not re.fullmatch(r"[a-f0-9]{64}", expected["inventorySha256"]):
            raise ValueError("Invalid baseline inventory hash")
        selected = [row for row in measurements if row["mode"] == mode]
        if not selected:
            errors.append(f"No {mode} packet observations")
        for row in selected:
            if inventory_hash(row["inventory"]) != expected["inventorySha256"]:
                errors.append(f"{mode} protocol/decoder inventory changed; review baseline explicitly")
        for key in ("clientSent", "serverDecoded"):
            wanted = expected[key]
            if not isinstance(wanted, list) or not wanted or len(wanted) != len(set(wanted)):
                raise ValueError("Packet baseline requires distinct nonempty identity floors")
            if any(not isinstance(name, str) or not re.fullmatch(r"[A-Za-z_][A-Za-z_0-9]*", name) for name in wanted):
                raise ValueError("Invalid packet baseline identity")
            observed = {name for row in selected for name in row["metrics"][key]["packets"]}
            missing = sorted(set(wanted) - observed)
            added = sorted(observed - set(wanted))
            comparisons.append(dict(mode=mode, metric=key, missing=missing, added=added, observed=len(observed), baseline=len(wanted)))
            if missing:
                errors.append(f"{mode} {key} lost: {', '.join(missing)}")
    return dict(passed=not errors, errors=errors, metrics=comparisons)
