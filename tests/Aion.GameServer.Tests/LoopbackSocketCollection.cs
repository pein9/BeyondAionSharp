using Xunit;

namespace Aion.GameServer.Tests;

/// <summary>
/// Runs the test classes that talk to a real loopback TCP peer by themselves, after the parallel part of the run.
/// </summary>
/// <remarks>
/// These tests wait for handshakes that take tens of milliseconds locally. On the 4-core GitHub runner they used to run in the
/// parallel phase, next to thousands of CPU-heavy tests, and twice a handshake missed its 5 second deadline and failed CI
/// (OutboundLinkLifecycleTests on 2026-08-24, GameServerBridgeConnectorTests on 2026-09-11), while passing every time locally.
/// <para>
/// xUnit 2.9 runs every collection marked <c>DisableParallelization</c> after all parallel collections have finished, one collection
/// at a time. That was checked with a throwaway probe project rather than assumed, because other comments in this suite describe it
/// differently. So a class in this collection never shares the runner with the rest of the assembly.
/// </para>
/// <para>
/// A class that also swaps a global singleton belongs in <c>GoldenDataManager</c> instead (see SingletonIsolationTests); that
/// collection is non-parallel too, so the socket tests in it get the same protection.
/// </para>
/// </remarks>
[CollectionDefinition("LoopbackSockets", DisableParallelization = true)]
public sealed class LoopbackSocketCollection
{
}

internal static class LoopbackSocketTimeouts
{
    /// <summary>
    /// How long a socket test waits for something it expects to happen: a frame, a handshake, a close. It only elapses when the test
    /// is failing anyway, so it is generous enough to ride out a slow CI runner.
    /// </summary>
    public static readonly TimeSpan Expected = TimeSpan.FromSeconds(30);
}
