using System.Globalization;
using System.Reflection;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Handlers.AdminCommands;
using Aion.GameServer.Network.LoginServer;
using Aion.GameServer.Network.LoginServer.ServerPackets;
using Aion.GameServer.Services.Ban;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging.Abstractions;
using GameLoginServer = Aion.GameServer.Network.LoginServer.LoginServer;

namespace Aion.GameServer.Tests;

public sealed class BanHddJavaArithmeticTests
{
	// Java BanHdd: int minutes, zero -> 5,256,000; int multiplication precedes long epoch addition.
	// Expected offsets independently evaluated with Python ctypes.c_int32(minutes * 60000).
	[Theory]
	[InlineData(int.MinValue, 0L)]
	[InlineData(-35792, 2147447296L)]
	[InlineData(-35791, -2147460000L)]
	[InlineData(-1, -60000L)]
	[InlineData(0, 1827387392L)]
	[InlineData(1, 60000L)]
	[InlineData(35791, 2147460000L)]
	[InlineData(35792, -2147447296L)]
	[InlineData(71582, -47296L)]
	[InlineData(71583, 12704L)]
	[InlineData(int.MaxValue, -60000L)]
	public async Task ActualCommandStoresAndSendsJavaWrappedEpoch(int minutes, long offset)
	{
		const long now = 1_800_032_400_000;
		const string serial = "unit-banhdd-overflow";
		var service = HDDBanService.GetInstance();
		var map = (Dictionary<string, DateTimeOffset>)typeof(HDDBanService).GetField("bannedSerials", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!;
		bool hadPrevious = map.TryGetValue(serial, out var previousBan);
		var instanceField = typeof(GameLoginServer).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
		object? previousInstance = instanceField.GetValue(null);
		var clock = (AsyncLocal<Func<long>?>)typeof(SystemClock).GetField("Source", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
		var previousClock = clock.Value;
		SimulationLoginServerLink? link = null;
		var connector = new GameLoginServer(NullLogger<GameLoginServer>.Instance, new GameServerOptions(), null,
			_ => link = new SimulationLoginServerLink(new Dictionary<int, SimulationLoginAccount>(), _ => { }));
		try
		{
			SystemClock.UseSource(() => now);
			new BanHdd().Execute(null!, serial, minutes.ToString(CultureInfo.InvariantCulture));
			Assert.Equal(now + offset, map[serial].ToUnixTimeMilliseconds());
			Assert.Equal(offset > 0, service.IsBanned(serial));
			var packet = Assert.IsType<SM_HDDBAN_CONTROL>(Assert.Single(link!.SentPackets));
			using var payload = new PacketBuffer(packet.SerializePayload());
			Assert.Equal(10, payload.ReadC()); Assert.Equal(1, payload.ReadC());
			Assert.Equal(serial, payload.ReadS()); Assert.Equal(now + offset, payload.ReadQ());
			Assert.Equal(0, payload.Remaining);
		}
		finally
		{
			clock.Value = previousClock;
			if (hadPrevious) map[serial] = previousBan; else map.Remove(serial);
			await connector.DisposeAsync(); instanceField.SetValue(null, previousInstance);
		}
	}
}
