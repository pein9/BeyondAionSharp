# Session recording

The game server can record every packet of chosen accounts, in both directions, to one file per session.
The goal is a record detailed enough to replay a human's play: the route walked, every target, cast, attack
and item use, and everything the server answered. The bot can then learn where a human pulled, rested and
walked through a camp.

The server never sees keystrokes, camera moves or mouse clicks. It sees the packets the client sends
because of them, and that is what a replay has to reproduce. A recording holds every one of those packets,
byte for byte, after decryption.

## Turning it on

Recording is off unless `AION_RECORD=true` is set for the game server.

| Variable | Meaning | Default |
|---|---|---|
| `AION_RECORD` | `true` records; anything else, or unset, does not | off |
| `AION_RECORD_ACCOUNTS` | comma-separated account names, case-insensitive; empty records every account | empty |
| `AION_RECORD_DIR` | output folder | `./log/recordings` |

With the Docker stack (`docker/docker-compose.yml`), set the variables in the shell or `.env` before
`docker compose up`. Recordings land on the host in `run/recordings/` (git-ignored):

```powershell
$env:AION_RECORD = 'true'; $env:AION_RECORD_ACCOUNTS = 'pilot'
docker compose -f docker/docker-compose.yml up -d gameserver
```

The bot stack (`docker/docker-compose.bots.yml`) passes the same variables; its recordings land in the run
folder under `logs/gs/recordings/`. The server logs `Session recorder enabled` at startup when it is on.

## What a recording holds

One JSONL file per connection, named `<account>-<UTC time>-<connection>.recording.jsonl`. A connection
is buffered from its first packet until its account is known (the login packets come first). It is written,
buffer included, when the account matches, or discarded. Every line has:

| Field | Meaning |
|---|---|
| `seq` | 1, 2, 3, ... across both directions, in the order the server handled them |
| `t` | server clock, epoch milliseconds (`SystemClock`) |
| `wall` | the same instant as UTC text (real time on a live server) |
| `dir` | `C` client packet, `S` server packet, `E` event |

**Client packets (`C`).** `payloadBase64` is the whole decrypted payload the client sent, header included:
`[opcode u16][0x65][~opcode u16]` then the body from `bodyOffset` 5. It is the exact input to the server's
decoder. Also recorded:

- `opcode`, `opcodeHex`, `packet` (the handler class, such as `CM_MOVE`), and `rawOpcode` (the encoded
  header value).
- `outcome`: `executed`, `read-failed`, `not-created` (unknown opcode, or not valid in the connection state)
  or `flood-disconnect`.
- `state`: the connection state before the packet.
- `fields`: every field the handler parsed, by name.
- `player`: the server's view of the character when the packet arrived. That is map, position, heading,
  level, HP, MP, dead, and target.

**Server packets (`S`).** `frameBase64` is the whole clear frame before encryption: `[length u16][opcode
u16][0x44][~opcode u16]` then the body from `bodyOffset` 7. Also recorded: `opcode`, `packet`, and `fields`,
every field of the packet object that wrote it. Game objects appear as type, object id and name.

**Events (`E`).** `session-open` (format version, connection, IP, run id), `account` (name and id),
`enter-world` / `leave-world` (with the character snapshot), and `session-close`.

## Reading one

```powershell
python tools/recording/recording.py summary  run/recordings/<file>.recording.jsonl
python tools/recording/recording.py timeline run/recordings/<file>.recording.jsonl --client-only
python tools/recording/recording.py path     run/recordings/<file>.recording.jsonl --out path.json
python tools/recording/recording.py client   run/recordings/<file>.recording.jsonl --out client-stream.json
python tools/recording/recording.py check    run/recordings/<file>.recording.jsonl
```

`path` pulls out the route, targets, experience gains (kills), deaths and rests. `client` exports the client
stream a replayer needs. For each packet it gives the time offset, the handler, and the body after the
header. The bot transport sends exactly that, as a `BotClientPacket(handler type, body)`.

## Replaying

The recording has what a replay needs: every client packet body with its timing and connection state.
Object ids are the catch. The server assigns them at spawn, so a replay against a different server run
sends the recorded ids, and targets, loot and dialogs point at the wrong objects. A replayer has to map
recorded ids to the new run's ids. The recording has what that mapping needs: every `SM_NPC_INFO` with
its template id and position. It also has to wait for the server's answers rather than trust the old
timing, since combat rolls differ between runs.

## Cost and safety

- Only matching accounts are written. Other connections are buffered only until their account is known,
  and at most 20,000 lines.
- Copies are taken synchronously, so the server's in-place encryption cannot change them. Files are
  written on a background task and flushed after each batch.
- Recordings hold the login session tokens (`CM_L2AUTH_LOGIN_CHECK`) and the client's MAC and disk serial.
  Account passwords go to the login server, never to the game server, so they are not in a recording.
  Treat the files as private.
- The recorder is C#-only instrumentation. The Java 4.8 server has none, and gameplay is unchanged whether
  it is on or off.
