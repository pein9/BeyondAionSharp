using Aion.Commons.Network;

namespace Aion.Commons.Tests;

public sealed class PacketReadBoundsPortTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(4 * 1024 * 1024)]
    public void StrictBlobReadRejectsInvalidLengthBeforeAllocation(int length)
    {
        using var buffer = new PacketBuffer(Array.Empty<byte>());
        long before = GC.GetAllocatedBytesForCurrentThread();
        Assert.Throws<EndOfStreamException>(() => buffer.ReadB(length));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 1024 * 1024, $"Rejected blob allocated {allocated} bytes.");
        Assert.Equal(0, buffer.Position);
    }

    [Fact]
    public void ExactAndEmptyBlobReadsKeepTheirValues()
    {
        using var buffer = new PacketBuffer(new byte[] { 1, 2, 3 });
        Assert.Empty(buffer.ReadB(0));
        Assert.Equal(new byte[] { 1, 2, 3 }, buffer.ReadB(3));
        Assert.Empty(buffer.ReadB(0));
    }
}
