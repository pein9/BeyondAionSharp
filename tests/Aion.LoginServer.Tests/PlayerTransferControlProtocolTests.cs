using Aion.Commons.Network;
using Aion.LoginServer.Network.GameServer;
using Aion.LoginServer.Network.GameServer.ClientPackets;

namespace Aion.LoginServer.Tests;

public sealed class PlayerTransferControlProtocolTests
{
	[Theory]
	[InlineData(5)]
	[InlineData(6)]
	[InlineData(7)]
	[InlineData(8)]
	[InlineData(9)]
	public void GsFactoryReadsEveryTransferSectionWithoutDroppingItsPayload(byte action)
	{
		using var buffer = new PacketBuffer();
		buffer.WriteC(13);
		buffer.WriteC(action);
		buffer.WriteD(0x11223344);
		byte[] data = [0, 1, 0xff, 0x80, action];
		buffer.WriteB(data);
		var packet = Assert.IsType<CmPlayerTransferControl>(GsClientPacketFactory.Create(
			new PacketBuffer(buffer.ToArray()), GameServerConnectionState.Authed));
		Assert.Equal(action, packet.ActionId);
		Assert.Equal(0x11223344, packet.TaskId);
		Assert.Equal(data, packet.Db);
		Assert.Empty(packet.Name);
	}

	[Fact]
	public void GsFactory_DispatchesJavaGoldenPlayerTransferPayload()
	{
		var payload = Convert.FromHexString("0D02443322116E006F00700065000000");

		var packet = Assert.IsType<CmPlayerTransferControl>(
			GsClientPacketFactory.Create(new PacketBuffer(payload), GameServerConnectionState.Authed));
		Assert.Equal(2, packet.ActionId);
		Assert.Equal(0x11223344, packet.TaskId);
		Assert.Equal("nope", packet.Reason);
	}
}
