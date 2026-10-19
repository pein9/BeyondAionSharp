using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Tests;

public sealed partial class SendQueuePortTests
{
    [Theory]
    [InlineData(60000, 60000, false, false)]
    [InlineData(60000, 60001, false, true)]
    [InlineData(60000, 60001, true, false)]
    [InlineData(0, 600000, false, false)]
    [InlineData(-1, 600000, false, false)]
    public void AuthenticationTimeoutIsStrictAndNonPositiveDisablesIt(long timeout, long elapsed, bool authed, bool closes)
    {
        WithClock(now =>
        {
            var con = new Link(authMillis: timeout, authenticated: authed);
            now.Value += elapsed;
            con.CloseIfTimedOut(now.Value);
            Assert.Equal(closes, con.IsClosed());
        });
    }

    [Fact]
    public void PeriodicCheckClosesStalledOrFullQueuesWithoutAnotherEnqueue()
    {
        WithClock(now =>
        {
            var stalled = new Link(10, 60000);
            stalled.SendPacket(new Packet()); now.Value += 60001;
            stalled.CloseIfTimedOut(now.Value);
            Assert.True(stalled.IsClosed()); Assert.Empty(stalled.Queue);
            var full = new Link(1, 60000);
            full.SendPacket(new Packet()); full.CloseIfTimedOut(now.Value);
            Assert.True(full.IsClosed()); Assert.Empty(full.Queue);
        });
    }

    [Theory]
    [InlineData(AionConnection.State.CONNECTED, false)]
    [InlineData(AionConnection.State.AUTHED, true)]
    [InlineData(AionConnection.State.IN_GAME, true)]
    public void GameAuthenticationExemptsOnlyAuthenticatedStates(AionConnection.State state, bool authed)
    {
        var con = new GameConnection(); con.SetState(state);
        Assert.Equal((authed, 60000L), con.Authentication);
    }
}
