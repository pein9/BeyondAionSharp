namespace Aion.LoginServer.Tests;

/// <summary>
/// How long a socket test waits for something it expects to happen: a frame, a handshake, a close, a server finishing its start or
/// stop. It only elapses when the test is failing anyway, so it is generous enough to ride out a slow CI runner. The GitHub runner
/// runs this assembly next to the GameServer suite's parallel phase, where 5 second waits have already timed out twice.
/// Deliberately short waits that prove something does NOT happen keep their own small values.
/// </summary>
internal static class LoopbackSocketTimeouts
{
    public static readonly TimeSpan Expected = TimeSpan.FromSeconds(30);
}
