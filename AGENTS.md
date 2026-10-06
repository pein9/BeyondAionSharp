# AGENTS.md

This is a **Java → C# port** of the Aion server (the `aionemu` codebase). The Java
source is the reference implementation; the C# port exists to match its behavior 1:1.

## Where this leads: the long-term goals

Set by the maintainer on 2026-10-06. Every bot leg, snapshot and fix serves these, in order:

1. **A server play-tested by our bots.** The bots play the game the way a player does. We
   find and fix bugs as we encounter them.
2. **An intelligent bot that can play the whole game:** all quests, gathering, crafting and
   the rest.
3. **Bots that can join a human group** as an extra healer or DPS. This is the far goal. It
   is not worked on until the first two are done.

A bug the bots find is fixed under the rules below and in `CLAUDE.md`: read the Java first,
and let 4.8 retail win only under a logged decision.

## Golden rule: the Java source is the spec

- **Before fixing or porting anything, read the corresponding Java implementation first**
  and mirror it. Do not infer intended behavior from the C# alone — the C# may be a
  faithful port of a Java quirk (keep it) or "reworked" code that diverged (fix it to
  match Java). The Java tree is the source of truth.
- When Java and C# disagree, **Java wins** — except pure infrastructure
  (DI, lifecycle, threading, sockets), where idiomatic C# is acceptable.

## Always watch for Java ↔ C# semantic gaps

These differ in ways that silently change behavior. Check them on every port/fix:

- **Null vs throw** — Java `ResultSet.getString()`, `Map.get()`, etc. return `null`;
  the C# equivalents (`GetString()`, dictionary indexer) **throw**. A throw mid-loop can
  abort an entire multi-row load.
- **enum ordinal** — Java `ordinal()` ≠ C# `(int)enum` when the C# enum has explicit /
  non-sequential values. Use `Array.IndexOf(Enum.GetValues(typeof(E)), e)`.
- **String hash on the wire** — the client expects Java `String.hashCode()` (`31*h + c`),
  not C# `string.GetHashCode()`.
- **DateTime Kind** — Java local/UTC handling vs C# `DateTimeKind`; mismatches corrupt
  timestamps.
- **Nullable enums from XML** — a missing `@XmlAttribute` enum defaults to ordinal-0 in
  C#, not `null`.
- **Numbers/char/division/overflow** — verify semantics whenever the value goes over the
  wire or into a calculation.

The Java checkout is expected at `../aion-server` by default. Set `BEYOND_AION_JAVA_ROOT`
when it lives elsewhere.

## Natural bot observation

Keep the read-only bot monitor at `http://127.0.0.1:17880/` running whenever the
active bot harness supports it. Tell the user when a run is available to watch.
Natural SIM journey runs enable it by default; `AION_BOT_DASHBOARD_PORT=0` disables
it when needed. Never build or run other checks while a journey batch holds the DLLs.

## Where things live

| What | Path |
|---|---|
| **Java reference (the spec)** | separate sibling `../aion-server` checkout, branch `4.8` |
| **C# port** | repository root (solution `AionServer.slnx`, target `net10.0`) |
| Authoritative parity backlog | `docs/Full-Parity-Backlog.md` — read before doing parity work |
| Upstream update queue | `docs/upstream-port-log.md` and `docs/upstream-porting.md` |
| Run / DB / setup guide | `RUNNING.md` |

## Build & test (C#)

```bash
dotnet build AionServer.slnx
dotnet test  AionServer.slnx        # golden/parity suite + unit tests
```

For upstream fixes, port one Java commit at a time and include an
`Upstream-Java-SHA` trailer. Never merge or cherry-pick Java history into `main`.

Run the stack (separate terminals, in order — details in `RUNNING.md`):

```bash
dotnet run --project src/Aion.LoginServer
dotnet run --project src/Aion.ChatServer
dotnet run --project src/Aion.GameServer
```
