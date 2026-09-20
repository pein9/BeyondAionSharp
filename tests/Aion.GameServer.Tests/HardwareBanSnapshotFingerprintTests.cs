using System.Reflection;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Network;
using Aion.GameServer.Network.LoginServer;
using Aion.GameServer.Services.Ban;
using Microsoft.Extensions.Logging;
using GameLoginServer = Aion.GameServer.Network.LoginServer.LoginServer;

namespace Aion.GameServer.Tests;

public sealed class HardwareBanSnapshotFingerprintTests
{
	private const long Winter = 1_800_032_400_000; // 2027-01-15 12:00 America/New_York
	private const long Summer = 1_815_667_200_000; // 2027-07-15 12:00 America/New_York
	private static readonly MacBanListEntry[] Mac = [new("02-00-00-00-00-01", Winter, "winter"), new("02-00-00-00-00-02", Summer, "summer")];
	private static readonly HddBanListEntry[] Hdd = [new("E2E-WINTER", Winter), new("E2E-summer", Summer)];

	[Fact]
	public void CanonicalVectorsMatchIndependentPythonStructAndHashlib()
	{
		Assert.Equal("0d63b827ce1f30e9a628509a57399e26e4f420efa3907f88d51d2228c4761998", HardwareBanSnapshotFingerprint.ForMac([]).Sha256);
		Assert.Equal("27ee9f9b7453d7360da3f995291e84e7fa0099d1758cc10e306d0b12250a6dc6", HardwareBanSnapshotFingerprint.ForHdd([]).Sha256);
		Assert.Equal("2bf4448388d3209a635447f9f117fa4b6e1b74eb96d7582f12661dcf21fa3aec", HardwareBanSnapshotFingerprint.ForMac(Mac).Sha256);
		Assert.Equal("70cadb130b5954e9a89b90039e103ae5181aee8a83bb61a924af9bf907eb50d5", HardwareBanSnapshotFingerprint.ForHdd(Hdd).Sha256);
		Assert.Equal("fee9b84cc31b977d53639120f419154ea127dc2be4c20c40d15c1063a0a9a302",
			HardwareBanSnapshotFingerprint.ForHdd([new(new string('x', 129) + "\uD800", -1)]).Sha256);
	}

	[Fact]
	public void OrderDoesNotMatterButLastDuplicateWins()
	{
		Assert.Equal(HardwareBanSnapshotFingerprint.ForMac(Mac), HardwareBanSnapshotFingerprint.ForMac(Mac.Reverse().ToArray()));
		Assert.Equal(HardwareBanSnapshotFingerprint.ForHdd(Hdd), HardwareBanSnapshotFingerprint.ForHdd(Hdd.Reverse().ToArray()));
		var duplicates = HardwareBanSnapshotFingerprint.ForMac([Mac[0] with { Time = Summer }, ..Mac]);
		Assert.Equal(3, duplicates.Entries); Assert.Equal(2, duplicates.DistinctEntries);
		Assert.Equal(HardwareBanSnapshotFingerprint.ForMac(Mac).Sha256, duplicates.Sha256);
		Assert.NotEqual(duplicates.Sha256, HardwareBanSnapshotFingerprint.ForMac([..Mac, Mac[0] with { Time = Summer }]).Sha256);
		Assert.Equal(HardwareBanSnapshotFingerprint.ForHdd(Hdd).Sha256,
			HardwareBanSnapshotFingerprint.ForHdd([Hdd[0] with { Time = Summer }, ..Hdd]).Sha256);
		Assert.NotEqual(HardwareBanSnapshotFingerprint.ForHdd(Hdd).Sha256,
			HardwareBanSnapshotFingerprint.ForHdd([..Hdd, Hdd[0] with { Time = Summer }]).Sha256);
	}

	[Fact]
	public void EveryIdentityTimeAndDetailsFieldAffectsTheFingerprint()
	{
		var original = HardwareBanSnapshotFingerprint.ForMac(Mac).Sha256;
		foreach (var changed in new[] { Mac[0] with { Address = "other" }, Mac[0] with { Time = Winter + 1 }, Mac[0] with { Details = "changed" } })
			Assert.NotEqual(original, HardwareBanSnapshotFingerprint.ForMac([changed, Mac[1]]).Sha256);
		Assert.NotEqual(HardwareBanSnapshotFingerprint.ForHdd([new("ab", 1), new("c", 2)]).Sha256,
			HardwareBanSnapshotFingerprint.ForHdd([new("a", 1), new("bc", 2)]).Sha256);
		Assert.NotEqual(HardwareBanSnapshotFingerprint.ForHdd([new("x\uD800", 1)]).Sha256,
			HardwareBanSnapshotFingerprint.ForHdd([new("x\uD801", 1)]).Sha256);
		Assert.NotEqual(HardwareBanSnapshotFingerprint.ForHdd([new("x", long.MinValue)]).Sha256,
			HardwareBanSnapshotFingerprint.ForHdd([new("x", long.MaxValue)]).Sha256);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RuntimeDispatchLogsOnlyAfterTheWholeBatchWasApplied(bool failSecondEntry)
	{
		const string first = "snapshot-unit-first", second = "snapshot-unit-second";
		var macMap = Field<Dictionary<string, BannedMacEntry>>(BannedMacManager.GetInstance(), "bannedList");
		var hddMap = Field<Dictionary<string, DateTimeOffset>>(HDDBanService.GetInstance(), "bannedSerials");
		var oldMac = new Dictionary<string, BannedMacEntry>(macMap);
		var oldHdd = new Dictionary<string, DateTimeOffset>(hddMap);
		var instanceField = typeof(GameLoginServer).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
		object? previousInstance = instanceField.GetValue(null);
		var logger = new SnapshotLogger();
		var connector = new GameLoginServer(logger, new GameServerOptions());
		try
		{
			typeof(GameLoginServer).GetField("_sessionGeneration", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(connector, 17);
			foreach (byte kind in new byte[] { 9, 10 })
			{
				logger.Snapshots.Clear();
				logger.OnSnapshot = () =>
				{
					if (kind == 9)
					{
						Assert.Equal(Winter, new DateTimeOffset(macMap[first].GetTime()!.Value).ToUnixTimeMilliseconds());
						Assert.Equal(Summer, new DateTimeOffset(macMap[second].GetTime()!.Value).ToUnixTimeMilliseconds());
					}
					else { Assert.Equal(Winter, hddMap[first].ToUnixTimeMilliseconds()); Assert.Equal(Summer, hddMap[second].ToUnixTimeMilliseconds()); }
				};
				using var write = new PacketBuffer(); write.WriteC(kind); write.WriteD(2);
				write.WriteS(first); write.WriteQ(Winter); if (kind == 9) write.WriteS("winter");
				write.WriteS(second); write.WriteQ(failSecondEntry ? long.MaxValue : Summer); if (kind == 9) write.WriteS("summer");
				using var read = new PacketBuffer(write.ToArray());
				Assert.True(LoginServerInboundPacketFactory.TryCreate(read, LoginServerState.Authed, out var packet, out _));
				var dispatch = typeof(GameLoginServer).GetMethod("DispatchRuntimePacket", BindingFlags.Instance | BindingFlags.NonPublic)!;
				if (failSecondEntry)
				{
					Assert.IsType<ArgumentOutOfRangeException>(Assert.Throws<TargetInvocationException>(() => dispatch.Invoke(connector, [packet])).InnerException);
					Assert.Empty(logger.Snapshots); // A partial manager update must never announce a complete batch.
					continue;
				}
				dispatch.Invoke(connector, [packet]);
				var snapshot = Assert.Single(logger.Snapshots);
				Assert.Equal(kind == 9 ? "mac" : "hdd", snapshot["Kind"]);
				Assert.Equal(17, snapshot["Generation"]); Assert.Equal(2, snapshot["Entries"]); Assert.Equal(2, snapshot["DistinctEntries"]);
				string expected = kind == 9
					? HardwareBanSnapshotFingerprint.ForMac([new(first, Winter, "winter"), new(second, Summer, "summer")]).Sha256
					: HardwareBanSnapshotFingerprint.ForHdd([new(first, Winter), new(second, Summer)]).Sha256;
				Assert.Equal(expected, snapshot["Sha256"]);
				Assert.DoesNotContain(snapshot.Values, value => value is string text && text.Contains(first, StringComparison.Ordinal));
			}
		}
		finally
		{
			await connector.DisposeAsync(); instanceField.SetValue(null, previousInstance);
			foreach (string key in new[] { first, second })
			{
				if (oldMac.TryGetValue(key, out var mac)) macMap[key] = mac; else macMap.Remove(key);
				if (oldHdd.TryGetValue(key, out var hdd)) hddMap[key] = hdd; else hddMap.Remove(key);
			}
		}
	}

	private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

	// Test access to the connector's private diagnostic helpers, not a separate production service.
	private static class HardwareBanSnapshotFingerprint
	{
		internal static (int Entries, int DistinctEntries, string Sha256) ForMac(IReadOnlyList<MacBanListEntry> entries) => Invoke("FingerprintMacBans", entries);
		internal static (int Entries, int DistinctEntries, string Sha256) ForHdd(IReadOnlyList<HddBanListEntry> entries) => Invoke("FingerprintHddBans", entries);
		private static (int Entries, int DistinctEntries, string Sha256) Invoke(string method, object entries) =>
			((int, int, string))typeof(GameLoginServer).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [entries])!;
	}

	private sealed class SnapshotLogger : ILogger<GameLoginServer>
	{
		internal List<Dictionary<string, object?>> Snapshots { get; } = [];
		internal Action? OnSnapshot { get; set; }
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
		public bool IsEnabled(LogLevel level) => true;
		public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter)
		{
			if (state is not IEnumerable<KeyValuePair<string, object?>> values) return;
			var fields = values.ToDictionary(p => p.Key, p => p.Value);
			if (fields.GetValueOrDefault("{OriginalFormat}") is not string template || !template.StartsWith("Applied Login hardware-ban snapshot:", StringComparison.Ordinal)) return;
			Assert.Equal(LogLevel.Information, level); OnSnapshot?.Invoke(); Snapshots.Add(fields);
		}
	}
}
