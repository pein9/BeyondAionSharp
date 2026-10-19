namespace Aion.LoginServer.Tests;

// Once-per-opcode diagnostic counters are process-wide; isolate tests that reset
// and observe them from the socket and factory regression cases.
[CollectionDefinition("Login packet diagnostics", DisableParallelization = true)]
public sealed class LoginPacketDiagnosticsCollection;
