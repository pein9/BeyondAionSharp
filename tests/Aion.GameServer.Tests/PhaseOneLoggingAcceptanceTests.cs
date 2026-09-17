using System.Runtime.CompilerServices;
using System.Text.Json;
using Aion.Commons.Logging;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Network.Aion;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Tests;

public sealed class PhaseOneLoggingAcceptanceTests
{
	[Fact]
	public void ThrowingClientPacketWritesOneScopedJsonError()
	{
		var directory = Path.Combine(Path.GetTempPath(), $"aion-phase1-log-{Guid.NewGuid():N}");
		try
		{
			using var provider = new JsonLinesLoggerProvider(directory, "gs", "phase1-acceptance");
			using var factory = LoggerFactory.Create(builder =>
			{
				builder.ClearProviders();
				builder.SetMinimumLevel(LogLevel.Trace);
				builder.AddProvider(provider);
			});
			using var factoryOverride = AionLog.OverrideFactory(factory);

			var account = new Account(17);
			account.SetName("phase1-account");
			var connection = (AionConnection)RuntimeHelpers.GetUninitializedObject(typeof(AionConnection));
			connection.SetAccount(account);
			connection.SetState(AionConnection.State.CONNECTED);
			var packet = new ThrowingClientPacket();
			packet.SetConnection(connection);

			packet.Run();

			var line = Assert.Single(ReadAllLinesWhileWriterIsOpen(Path.Combine(directory, "gs.problems.jsonl")));
			using var record = JsonDocument.Parse(line);
			Assert.Equal("ERROR", record.RootElement.GetProperty("lvl").GetString());
			Assert.Equal("phase1-account", record.RootElement.GetProperty("acct").GetString());
			Assert.Equal(nameof(ThrowingClientPacket), record.RootElement.GetProperty("op").GetString());
			Assert.Contains("[378] ThrowingClientPacket", record.RootElement.GetProperty("msg").GetString(), StringComparison.Ordinal);
			Assert.Equal(typeof(InvalidOperationException).FullName, record.RootElement.GetProperty("exType").GetString());
		}
		finally
		{
			if (Directory.Exists(directory))
				Directory.Delete(directory, recursive: true);
		}
	}

	private static string[] ReadAllLinesWhileWriterIsOpen(string path)
	{
		using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		using var reader = new StreamReader(stream);
		var lines = new List<string>();
		while (reader.ReadLine() is { } line)
			lines.Add(line);
		return lines.ToArray();
	}

	private sealed class ThrowingClientPacket()
		: AionClientPacket(0x17A, new HashSet<AionConnection.State> { AionConnection.State.CONNECTED })
	{
		protected override void ReadImpl()
		{
		}

		protected override void RunImpl() => throw new InvalidOperationException("phase one probe");
	}
}
