using System.Buffers.Binary;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.Commons.Network.Server;
using Aion.LoginServer.Network.Aion;
using Aion.LoginServer.Network.Aion.ClientPackets;
using Aion.LoginServer.Network.Aion.ServerPackets;
using Aion.LoginServer.Network.Crypto;
using Aion.LoginServer.Services;
using Microsoft.Extensions.Logging;

namespace Aion.LoginServer.Network;

public sealed class LoginClientConnection : BaseClientConnection, ILoginClientSession
{
	private readonly LoginRsaKeyPair _rsaKeyPair;
	private readonly byte[] _blowfishKey;
	private readonly LoginCryptEngine _cryptEngine = new();
	private readonly ILoginAuthService _authService;
	private readonly ILoginSessionRegistry _sessionRegistry;
	private readonly IGameServerRegistry _gameServerRegistry;
	private readonly SemaphoreSlim _sendLock = new(1, 1);
	private readonly object _sendQueueGuard = new();
	private readonly TimeProvider _timeProvider;
	private readonly long _connectedAtMillis;
	private readonly long _authTimeoutMillis;
	private int _pendingServerPackets;
	private long _lastWriteProgressMillis;
	private volatile bool _sendQueueClosed;
	private readonly SemaphoreSlim _closeLock = new(1, 1);
	private volatile LoginClientState _state = LoginClientState.Connected;
	private readonly int _sessionId;
	private Model.Account? _account;
	private SessionKey? _sessionKey;
	private bool _joinedGameServer;

	public LoginClientConnection(
		ILogger logger,
		TcpClient client,
		string clientId,
		ILoginKeyGenerator keyGenerator,
		ILoginAuthService authService,
		ILoginSessionRegistry sessionRegistry,
		IGameServerRegistry gameServerRegistry,
		TimeProvider? timeProvider = null,
		int authTimeoutSeconds = 60)
		: base(logger, client, clientId)
	{
		_timeProvider = timeProvider ?? TimeProvider.System;
		_connectedAtMillis = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
		_authTimeoutMillis = authTimeoutSeconds * 1000L;
		_authService = authService;
		_sessionRegistry = sessionRegistry;
		_gameServerRegistry = gameServerRegistry;
		_sessionId = GetHashCode();
		_rsaKeyPair = keyGenerator.GetEncryptedRsaKeyPair();
		_blowfishKey = keyGenerator.GenerateBlowfishKey();
		_cryptEngine.UpdateKey(_blowfishKey);
	}

	public Model.Account Account => _account ?? throw new InvalidOperationException("Login session does not have an account yet.");

	public SessionKey SessionKey => _sessionKey ?? throw new InvalidOperationException("Login session does not have a session key yet.");

	public bool JoinedGameServer => _joinedGameServer;

	public override async Task RunAsync()
	{
		using var timeoutCancellation = new CancellationTokenSource();
		Task monitor = WatchTimeoutsAsync(timeoutCancellation.Token);
		try
		{
			await SendPacketAsync(new SmInit(_rsaKeyPair.EncryptedModulus, _blowfishKey, _sessionId));
			await base.RunAsync();
		}
		finally
		{
			timeoutCancellation.Cancel();
			try { await monitor; } catch (OperationCanceledException) when (timeoutCancellation.IsCancellationRequested) { }
		}
	}

	private async Task WatchTimeoutsAsync(CancellationToken cancellationToken)
	{
		using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), _timeProvider);
		while (await timer.WaitForNextTickAsync(cancellationToken))
			await CloseIfTimedOutAsync();
	}

	internal async Task CloseIfTimedOutAsync()
	{
		bool abort;
		lock (_sendQueueGuard)
		{
			if (!_isConnected || _sendQueueClosed) return;
			long now = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
			abort = _authTimeoutMillis > 0 && unchecked(now - _connectedAtMillis) > _authTimeoutMillis && _state != LoginClientState.AuthedLogin;
			if (abort)
				_logger.LogInformation("{ClientId} didn't authenticate within {Millis} ms, disconnecting", _clientId, _authTimeoutMillis);
			else if (_pendingServerPackets != 0)
				abort = IsSendQueueStuck(now);
			if (abort) _sendQueueClosed = true;
		}
		if (abort) await AbortSendQueueAsync();
	}

	private async Task AbortSendQueueAsync()
	{
		_client.Close(); // Unblock the active sender before waiting for its lock during cleanup.
		await CloseAsync();
	}

	// Called under _sendQueueGuard for enqueue checks and periodic idle checks.
	private bool IsSendQueueStuck(long now)
	{
		if (_pendingServerPackets >= 100)
		{
			_logger.LogWarning("{ClientId} has {Count} server packets waiting to be sent, disconnecting", _clientId, _pendingServerPackets);
			return true;
		}
		if (unchecked(now - _lastWriteProgressMillis) > 60_000)
		{
			_logger.LogWarning("{ClientId} hasn't received any data for {Millis} ms while {Count} server packets are waiting to be sent, disconnecting",
				_clientId, unchecked(now - _lastWriteProgressMillis), _pendingServerPackets);
			return true;
		}
		return false;
	}

	protected override async Task<PacketBuffer?> ReadPacketAsync()
	{
		var header = await ReadExactOrNullAsync(2);
		if (header == null)
			return null;

		var frameLength = BinaryPrimitives.ReadUInt16LittleEndian(header);
		if (frameLength < 3 || frameLength > 8192 * 2) // Java LoginConnection read-buffer capacity
			return null;

		var payload = await ReadExactOrNullAsync(frameLength - 2);
		if (payload == null)
			return null;

		if (!_cryptEngine.Decrypt(payload, 0, payload.Length))
		{
			var (opCode, xorBeforeLastWord, ignoredLastWord) = GetChecksumDebugInfo(payload);
			_logger.LogWarning(
				"Wrong checksum from login client {ClientId}. Length={Length}, Opcode=0x{Opcode:X2}, XorBeforeLastWord=0x{XorBeforeLastWord:X8}, IgnoredLastWord=0x{IgnoredLastWord:X8}",
				_clientId,
				payload.Length,
				opCode,
				xorBeforeLastWord,
				ignoredLastWord);
			await CloseAsync();
			return null;
		}

		return new PacketBuffer(payload[..^8]); // client checksum and final unused word are not packet fields
	}

	protected override async Task ProcessPacketAsync(PacketBuffer packet)
	{
		using var connectionScope = _logger.BeginScope(CreateConnectionScope());
		var parsed = AionClientPacketFactory.Create(packet, _state, out bool readFailed);
		if (readFailed)
		{
			await CloseAsync();
			return;
		}
		using var packetScope = parsed == null
			? null
			: _logger.BeginScope(new Dictionary<string, string>
			{
				["packet"] = parsed.GetType().Name,
				["opcode"] = $"0x{parsed.OpCode:X2}",
			});
		switch (parsed)
		{
			case CmUpdateSession updateSession:
				bool reconnected = false;
				await _closeLock.WaitAsync();
				try
				{
					lock (_sendQueueGuard)
					{
						if (!_isConnected || _sendQueueClosed) break;
						if (_sessionRegistry.TryConsumeReconnectingAccount(updateSession.AccountId, updateSession.ReconnectKey, out var reconnectingAccount) && reconnectingAccount != null)
						{
							_account = reconnectingAccount.Account;
							_state = LoginClientState.AuthedLogin;
							_sessionKey = new SessionKey(_account);
							_sessionRegistry.RegisterReconnectedSession(this);
							reconnected = true;
						}
					}
				}
				finally { _closeLock.Release(); }
				if (reconnected) await SendPacketAsync(new SmUpdateSession(_sessionKey!));
				else await CloseAsync();
				break;
			case CmAuthGameGuard auth when auth.SessionId == _sessionId:
				_state = LoginClientState.AuthedGameGuard;
				await SendPacketAsync(new SmAuthGameGuard());
				break;
			case CmAuthGameGuard:
				await SendPacketAsync(new SmLoginFail(AionAuthResponse.STR_L2AUTH_S_SYSTEM_ERROR));
				await CloseAsync();
				break;
			case CmLogin login:
				if (login.SessionId != _sessionId)
				{
					await SendPacketAsync(new SmLoginFail(AionAuthResponse.STR_L2AUTH_S_SYSTEM_ERROR));
					break;
				}

				var credentials = LoginCredentialDecryptor.Decrypt(login.EncryptedLoginData, _rsaKeyPair);
				if (credentials == null)
				{
					await SendPacketAsync(new SmLoginFail(AionAuthResponse.STR_L2AUTH_S_SYSTEM_ERROR));
					break;
				}
				var authResult = await _authService.LoginAsync(credentials.Username, credentials.Password, GetRemoteIp());
				if (!_isConnected || _sendQueueClosed) break;
				if (authResult.SendAccountBannedPacket)
				{
					await SendPacketAsync(new SmAccountBanned2());
					await CloseAsync();
				}
				else if (authResult.Response == AionAuthResponse.STR_L2AUTH_S_ALL_OK && authResult.Account != null)
				{
					if (await _gameServerRegistry.KickAccountFromGameServerAsync(authResult.Account.Id, notifyDoubleLogin: true))
					{
						await SendPacketAsync(new SmLoginFail(AionAuthResponse.STR_L2AUTH_S_ALREADY_LOGIN));
						break;
					}

					bool? registered = await CompleteLoginUnlessClosedAsync(authResult.Account);
					if (registered == null) break;
					if (registered.Value) await SendPacketAsync(new SmLoginOk(_sessionKey!));
					else await SendPacketAsync(new SmLoginFail(AionAuthResponse.STR_L2AUTH_S_ALREADY_LOGIN));
				}
				else
				{
					await SendPacketAsync(new SmLoginFail(authResult.Response ?? AionAuthResponse.STR_L2AUTH_S_SYSTEM_ERROR));
					if (authResult.CloseAfterResponse)
						await CloseAsync();
				}
				break;
			case CmServerList serverList:
				if (_sessionKey == null || _account == null || !_sessionKey.CheckLogin(serverList.AccountId, serverList.LoginOk))
				{
					await SendPacketAsync(new SmLoginFail(AionAuthResponse.STR_L2AUTH_S_SYSTEM_ERROR));
					await CloseAsync();
					break;
				}

				var servers = _gameServerRegistry.GetGameServers();
				if (servers.Count == 0)
				{
					await SendPacketAsync(new SmLoginFail(AionAuthResponse.STR_L2AUTH_S_NO_SERVER_LIST));
					await CloseAsync();
					break;
				}

				_sessionRegistry.BeginGameServerCharacterCountLoad(serverList.AccountId, _gameServerRegistry.GetOfflineGameServerCharacterCounts());
				await _gameServerRegistry.RequestOnlineGameServerCharacterCountsAsync(serverList.AccountId);
				if (_sessionRegistry.HasAllGameServerCharacterCounts(serverList.AccountId, servers.Count))
				{
					await SendPacketAsync(
						new SmServerList(
							servers,
							_sessionRegistry.GetGameServerCharacterCounts(serverList.AccountId),
							(byte)_account.LastServer));
				}
				break;
			case CmPlay play:
				if (_sessionKey == null || _account == null || !_sessionKey.CheckLogin(play.AccountId, play.LoginOk))
				{
					await SendPacketAsync(new SmLoginFail(AionAuthResponse.STR_L2AUTH_S_SYSTEM_ERROR));
					await CloseAsync();
					break;
				}

				var gameServer = _gameServerRegistry.GetGameServer(play.ServerId);
				if (gameServer == null || !gameServer.IsOnline)
				{
					await SendPacketAsync(new SmPlayFail(AionAuthResponse.STR_L2AUTH_S_SERVER_DOWN));
				}
				else if (gameServer.MinAccessLevel > _account.AccessLevel)
				{
					await SendPacketAsync(new SmPlayFail(AionAuthResponse.STR_L2AUTH_S_SEVER_CHECK));
				}
				else if (gameServer.IsFull)
				{
					await SendPacketAsync(new SmPlayFail(AionAuthResponse.STR_L2AUTH_S_LIMIT_EXCEED));
				}
				else
				{
					_joinedGameServer = true;
					await SendPacketAsync(new SmPlayOk(_sessionKey, play.ServerId));
				}
				break;
			case null:
				// AionClientPacketFactory distinguishes and logs unknown opcodes and packet-read failures with wire data.
				break;
			default:
				_logger.LogDebug("Parsed login packet 0x{Opcode:X2} in state {State}", parsed.OpCode, _state);
				break;
		}
	}

	private async Task<bool?> CompleteLoginUnlessClosedAsync(Model.Account account)
	{
		await _closeLock.WaitAsync();
		try
		{
			lock (_sendQueueGuard)
			{
				if (!_isConnected || _sendQueueClosed) return null;
				_account = account;
				_state = LoginClientState.AuthedLogin;
				_sessionKey = new SessionKey(account);
			}
			var registered = await _sessionRegistry.RegisterLoginSessionAsync(this);
			if (registered == LoginSessionRegisterResult.AlreadyLoggedIn)
			{
				_account = null;
				_sessionKey = null;
				_state = LoginClientState.AuthedGameGuard;
				return false;
			}
			await _authService.CompleteSuccessfulLoginAsync(account, GetRemoteIp());
			return true;
		}
		finally { _closeLock.Release(); }
	}

	private Dictionary<string, string> CreateConnectionScope()
	{
		var values = new Dictionary<string, string>
		{
			["connection"] = _clientId,
		};
		if (_account != null)
			values["account"] = _account.Name;
		return values;
	}

	private string GetRemoteIp()
	{
		return _client.Client.RemoteEndPoint is System.Net.IPEndPoint endPoint
			? endPoint.Address.ToString()
			: string.Empty;
	}

	public async Task SendPacketAsync(AionServerPacket packet)
	{
		bool abort = false;
		lock (_sendQueueGuard)
		{
			if (!_isConnected || _sendQueueClosed) return;
			long now = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
			if (_pendingServerPackets == 0)
				_lastWriteProgressMillis = now;
			else abort = IsSendQueueStuck(now);
			if (abort) _sendQueueClosed = true;
			else _pendingServerPackets++;
		}
		if (abort)
		{
			await AbortSendQueueAsync();
			return;
		}

		await _sendLock.WaitAsync();
		try
		{
			lock (_sendQueueGuard) _pendingServerPackets--;
			if (!_isConnected || _sendQueueClosed) return;
			var frame = packet.SerializeEncryptedFrame(_cryptEngine);
			int offset = 0;
			while (offset < frame.Length)
			{
				int sent = await _client.Client.SendAsync(frame.AsMemory(offset), SocketFlags.None);
				if (sent <= 0) throw new IOException("Login client socket write made no progress.");
				offset += sent;
				lock (_sendQueueGuard) _lastWriteProgressMillis = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
			}
		}
		catch (Exception ex) when (_sendQueueClosed && ex is IOException or SocketException or ObjectDisposedException)
		{
			// The send-queue guard deliberately aborted this connection; cleanup owns the close.
		}
		finally { _sendLock.Release(); }
	}

	public async Task CloseWithPacketAsync(AionServerPacket packet)
	{
		await SendPacketAsync(packet);
		await CloseAsync();
	}

	public override async Task CloseAsync()
	{
		await _closeLock.WaitAsync();
		try
		{
			if (!_isConnected)
				return;

			if (_account != null && !_joinedGameServer)
			{
				_sessionRegistry.RemoveLoginSession(_account, this);
				await _authService.UpdateOnLogoutAsync(_account);
			}

			await _sendLock.WaitAsync();
			try
			{
				await base.CloseAsync();
			}
			finally
			{
				_sendLock.Release();
			}
		}
		finally
		{
			_closeLock.Release();
		}
	}

	private async Task<byte[]?> ReadExactOrNullAsync(int length)
	{
		var buffer = new byte[length];
		var offset = 0;
		while (offset < length)
		{
			int read;
			try { read = await ReadAsync(buffer, offset, length - offset, TimeSpan.FromSeconds(30)); }
			catch (Exception ex) when (_sendQueueClosed && ex is IOException or SocketException or ObjectDisposedException)
			{
				return null; // The timeout/send guard deliberately closed this socket.
			}
			if (read == 0)
				return null;
			offset += read;
		}

		return buffer;
	}

	private static (byte OpCode, uint XorBeforeLastWord, uint IgnoredLastWord) GetChecksumDebugInfo(byte[] payload)
	{
		if (payload.Length == 0)
			return (0, 0, 0);

		uint xor = 0;
		uint ignoredLastWord = 0;
		if ((payload.Length & 3) == 0 && payload.Length > 4)
		{
			var count = payload.Length - 4;
			for (var offset = 0; offset < count; offset += 4)
				xor ^= BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(offset, 4));
			ignoredLastWord = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(count, 4));
		}

		return (payload[0], xor, ignoredLastWord);
	}
}
