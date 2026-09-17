using System.Reflection;
using System.Runtime.CompilerServices;
using Aion.Commons.Concurrent;
using Aion.Commons.Lang;
using Aion.Commons.Nio;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Players;

namespace Aion.GameServer.Tests;

public sealed class SocketlessAionConnectionTests
{
	[Fact]
	public void QuitReachesLeaveWorldAndClosesInlineWithOnlyTheClosePacket()
	{
		var connection = new RecordingSocketlessConnection();
		var player = AttachPlayer(connection);
		var calls = new List<(Player Player, bool Delayed)>();
		using var capture = PlayerLeaveWorldService.CaptureForCurrentContext((captured, delayed) =>
		{
			calls.Add((captured, delayed));
			captured.SetClientConnection(null!);
			connection.SetActivePlayer(null!);
		});
		connection.SendPacket(new SM_QUIT_RESPONSE());
		var packet = new CM_QUIT(3, new HashSet<AionConnection.State> { AionConnection.State.IN_GAME });
		packet.SetConnection(connection);
		packet.SetBuffer(ByteBuffer.Wrap([0]));
		Assert.True(packet.Read());

		packet.Run();

		var call = Assert.Single(calls);
		Assert.Same(player, call.Player);
		Assert.False(call.Delayed);
		Assert.True(connection.IsClosed());
		Assert.False(connection.IsConnected());
		Assert.Equal(Environment.CurrentManagedThreadId, connection.DisconnectThreadId);
		Assert.IsType<SM_QUIT_RESPONSE>(Assert.Single(connection.SentPackets));
		Assert.True(connection.IsBaseQueueEmpty);
	}

	[Fact]
	public void AbruptDropReachesDelayedLeaveWorldOnCallingThread()
	{
		var connection = new RecordingSocketlessConnection();
		var player = AttachPlayer(connection);
		var calls = new List<(Player Player, bool Delayed, int ThreadId)>();
		using var capture = PlayerLeaveWorldService.CaptureForCurrentContext((captured, delayed) =>
			calls.Add((captured, delayed, Environment.CurrentManagedThreadId)));
		var callingThreadId = Environment.CurrentManagedThreadId;

		connection.Disconnect(new FailingExecutor());

		var call = Assert.Single(calls);
		Assert.Same(player, call.Player);
		Assert.True(call.Delayed);
		Assert.Equal(callingThreadId, call.ThreadId);
		Assert.Equal(callingThreadId, connection.DisconnectThreadId);
		Assert.True(connection.IsClosed());
		Assert.False(connection.IsConnected());
		Assert.True(connection.IsBaseQueueEmpty);
	}

	private static Player AttachPlayer(AionConnection connection)
	{
		var account = new Account(1200);
		account.SetName("socketless-account");
		connection.SetAccount(account);
		var common = new PlayerCommonData(2200);
		common.SetName("SocketlessPlayer");
		var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
		SetField(player, "playerAccountData", new PlayerAccountData(common, new PlayerAppearance()));
		SetField(player, "playerAccount", account);
		player.SetClientConnection(connection);
		Assert.True(connection.SetActivePlayer(player));
		return player;
	}

	private static void SetField(object target, string fieldName, object value)
	{
		var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingFieldException(target.GetType().FullName, fieldName);
		field.SetValue(target, value);
	}

	private sealed class RecordingSocketlessConnection : AionConnection
	{
		private readonly Queue<AionServerPacket> baseQueue = new();

		public RecordingSocketlessConnection() : base("127.0.0.1")
		{
		}

		public List<AionServerPacket> SentPackets { get; } = [];
		public int DisconnectThreadId { get; private set; }
		public bool IsBaseQueueEmpty => baseQueue.Count == 0;

		protected override Queue<AionServerPacket> GetSendMsgQueue() => baseQueue;

		protected override void EnqueuePacket(AionServerPacket packet, bool closing) => SentPackets.Add(packet);

		protected override void ClearPendingPackets()
		{
			base.ClearPendingPackets();
			SentPackets.Clear();
		}

		protected override void ExecutePacket(AionClientPacket packet) => packet.Run();

		protected override void ResetPlayerPositionAfterDisconnect(Player player)
		{
		}

		protected override void OnDisconnect()
		{
			DisconnectThreadId = Environment.CurrentManagedThreadId;
			base.OnDisconnect();
		}
	}

	private sealed class FailingExecutor : Executor
	{
		public void Execute(Runnable command) => throw new Xunit.Sdk.XunitException("Socketless disconnect must run inline.");
	}
}
