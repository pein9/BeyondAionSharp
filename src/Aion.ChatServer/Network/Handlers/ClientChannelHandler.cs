using System.Buffers.Binary;
using System.Net.Sockets;
using Aion.ChatServer.Configuration;
using Aion.ChatServer.Handlers;
using Aion.ChatServer.Models;
using Aion.ChatServer.Models.Channels;
using Aion.ChatServer.Network.Packets;
using Aion.ChatServer.Network.Packets.Client;
using Aion.ChatServer.Network.Packets.Server;
using Aion.ChatServer.Services;
using Aion.Commons.Network;
using Aion.Commons.Network.Server;
using Microsoft.Extensions.Logging;

namespace Aion.ChatServer.Network.Handlers;

public sealed class ClientChannelHandler : BaseClientConnection, IChatClientConnection
{
	private readonly IChatService _chatService;
	private readonly ChatChannels _channels;
	private readonly IBroadcastService _broadcastService;
	private readonly ChatHandlerRegistry _handlerRegistry;
	private readonly ChatServerOptions _options;
	private readonly SemaphoreSlim _sendLock = new(1, 1);
	private readonly SemaphoreSlim _closeLock = new(1, 1);
	private volatile ChatClientConnectionState _state = ChatClientConnectionState.Connected;
	private ChatClient? _chatClient;
    private readonly object _flowGuard = new();
    private readonly ChatClientWriteState _writes = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly List<Task> _monitorTasks = new();
    private readonly TimeProvider _timeProvider;
    private CancellationTokenSource? _stallCancellation;
    private volatile bool _transportAborted;

	public ClientChannelHandler(
		ILogger logger,
		TcpClient client,
		string clientId,
		IChatService chatService,
		ChatChannels channels,
		IBroadcastService broadcastService,
		ChatHandlerRegistry handlerRegistry,
		ChatServerOptions options,
		TimeProvider? timeProvider = null)
		: base(logger, client, clientId)
	{
		_chatService = chatService;
		_channels = channels;
		_broadcastService = broadcastService;
		_handlerRegistry = handlerRegistry;
		_options = options;
        _timeProvider = timeProvider ?? TimeProvider.System;
        client.NoDelay = true;
        client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
	}

	protected override async Task<PacketBuffer?> ReadPacketAsync()
	{
		var header = await ReadExactOrNullAsync(2);
		if (header == null)
			return null;

		var frameLength = BinaryPrimitives.ReadUInt16LittleEndian(header);
		if (!ChatFrameLimits.IsValid(frameLength))
			return null;

		var payload = await ReadExactOrNullAsync(frameLength - 2);
		return payload == null ? null : new PacketBuffer(payload, strictReads: true);
	}

	protected override async Task ProcessPacketAsync(PacketBuffer packet)
	{
		using var connectionScope = _logger.BeginScope(CreateConnectionScope());
		var parsed = ClientPacketFactory.Create(packet, _state, out bool malformed);
        if (malformed)
        {
            await AbortTransportAsync();
            return;
        }
		using var packetScope = parsed == null
			? null
			: _logger.BeginScope(new Dictionary<string, string>
			{
				["packet"] = parsed.GetType().Name,
				["opcode"] = $"0x{parsed.OpCode:X2}",
			});
        try { await DispatchPacketAsync(parsed); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not execute chat client packet {Packet} from {ClientId}", parsed?.GetType().Name, _clientId);
        }
    }

    private async Task DispatchPacketAsync(AbstractClientPacket? parsed)
    {
		switch (parsed)
		{
			case CmChatIni:
				await SendPacketAsync(new SmChatIni());
				break;
			case CmPlayerAuth auth:
				await HandlePlayerAuthAsync(auth);
				break;
			case CmChannelRequest request:
				await HandleChannelRequestAsync(request);
				break;
			case CmChannelLeave leave:
				HandleChannelLeave(leave);
				break;
			case CmChannelMessage message:
				await HandleChannelMessageAsync(message);
				break;
			case CmChannelCreate or CmChannelJoin or CmPlayerInfo or CmPing:
				break;
			case null:
				_logger.LogWarning("Unknown chat client packet from {ClientId} in state {State}", _clientId, _state);
				break;
			default:
				_logger.LogDebug("Parsed chat client packet 0x{Opcode:X2} in state {State}", parsed.OpCode, _state);
				break;
		}
	}

	private Dictionary<string, string> CreateConnectionScope()
	{
		var values = new Dictionary<string, string>
		{
			["connection"] = _clientId,
		};
		if (_chatClient != null)
		{
			values["account"] = _chatClient.AccountName;
			values["player"] = _chatClient.Name;
			values["playerObjectId"] = _chatClient.ClientId.ToString(System.Globalization.CultureInfo.InvariantCulture);
		}
		return values;
	}

    public override async Task RunAsync()
    {
        lock (_flowGuard)
            TrackMonitor(EnforceAuthenticationDeadlineAsync(_lifetime.Token));
        try { await base.RunAsync(); }
        finally { await JoinMonitorsAsync(); }
    }

    private void TrackMonitor(Task task)
    {
        _monitorTasks.RemoveAll(t => t.IsCompleted);
        _monitorTasks.Add(task);
    }

    private async Task DelayAsync(long milliseconds, CancellationToken token)
    {
        while (milliseconds > 0)
        {
            long chunk = Math.Min(milliseconds, uint.MaxValue - 1L);
            await Task.Delay(TimeSpan.FromMilliseconds(chunk), _timeProvider, token);
            milliseconds -= chunk;
        }
    }

    private async Task EnforceAuthenticationDeadlineAsync(CancellationToken token)
    {
        long timeout = _options.ClientAuthTimeoutSeconds * 1000L;
        if (timeout <= 0) return;
        try
        {
            await DelayAsync(timeout, token);
            lock (_flowGuard)
            {
                if (!_isConnected || _transportAborted || _state == ChatClientConnectionState.Authed) return;
                _transportAborted = true;
            }
            _logger.LogInformation("{ClientId} didn't authenticate within {Millis} ms, disconnecting", _clientId, timeout);
            await AbortTransportAsync();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { _logger.LogError(ex, "Chat authentication deadline cleanup failed"); }
    }

    private async Task EnforceSendStallAsync(int change, CancellationToken token)
    {
        try
        {
            await DelayAsync(60000, token);
            lock (_flowGuard)
            {
                if (!_isConnected || _transportAborted || _writes.IsWritable || _writes.WritabilityChanges != change) return;
                _transportAborted = true;
            }
            _logger.LogWarning("{ClientId} hasn't received data for 60000 ms while its send buffer is full, disconnecting", _clientId);
            await AbortTransportAsync();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { _logger.LogError(ex, "Chat send-stall cleanup failed"); }
    }

    private async Task JoinMonitorsAsync()
    {
        Task[] tasks;
        lock (_flowGuard) { _lifetime.Cancel(); tasks = _monitorTasks.ToArray(); }
        await Task.WhenAll(tasks);
    }

    private async Task AbortTransportAsync()
    {
        lock (_flowGuard) _transportAborted = true;
        _client.Close();
        await CloseAsync();
    }

    public async Task SendPacketAsync(AbstractServerPacket packet, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_flowGuard) if (!_isConnected || _transportAborted) return;
        byte[] frame;
        try { frame = packet.SerializeFrame(); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not encode a packet for {ClientId}, disconnecting", _clientId);
            await AbortTransportAsync(); return;
        }
        bool admitted;
        lock (_flowGuard)
        {
            if (!_isConnected || _transportAborted) return;
            admitted = _writes.CanEnqueue;
            if (admitted)
            {
                if (_writes.Enqueue(frame.Length))
                {
                    _stallCancellation?.Cancel(); _stallCancellation?.Dispose();
                    _stallCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
                    TrackMonitor(EnforceSendStallAsync(_writes.WritabilityChanges, _stallCancellation.Token));
                }
            }
            else _transportAborted = true;
        }
        if (!admitted)
        {
            _logger.LogWarning("{ClientId} has more than 262144 bytes waiting to be sent, disconnecting", _clientId);
            await AbortTransportAsync(); return;
        }
        bool acquired = false, failed = false;
        try
        {
            await _sendLock.WaitAsync(cancellationToken); acquired = true;
            if (!_isConnected || _transportAborted) return;
            await WriteAsync(frame, 0, frame.Length);
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            failed = true;
            lock (_flowGuard) _transportAborted = true;
            _client.Close();
        }
        finally
        {
            if (acquired) _sendLock.Release();
            lock (_flowGuard)
                if (_writes.Complete(frame.Length)) _stallCancellation?.Cancel();
        }
        if (failed) await CloseAsync();
    }

    Task IChatClientConnection.CloseAsync(CancellationToken cancellationToken) => CloseAsync();

    public override async Task CloseAsync()
    {
        await _closeLock.WaitAsync();
        try
        {
            if (!_isConnected) return;
            lock (_flowGuard) { _transportAborted = true; _lifetime.Cancel(); }
            _client.Close(); // Socket close unblocks an in-flight write before the serialized cleanup.
            _state = ChatClientConnectionState.Disconnected;
            if (_chatClient != null) _broadcastService.RemoveClient(_chatClient);
            await _sendLock.WaitAsync();
            try { await base.CloseAsync(); }
            finally { _sendLock.Release(); }
        }
        finally { _closeLock.Release(); }
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await JoinMonitorsAsync();
        _stallCancellation?.Dispose();
    }

    private async Task HandlePlayerAuthAsync(CmPlayerAuth packet)
    {
        await _closeLock.WaitAsync();
        bool attached = false;
        try
        {
            lock (_flowGuard)
            {
                if (!_isConnected || _transportAborted) return;
                attached = _chatService.RegisterPlayerConnection(packet.PlayerId, packet.Token, packet.Identifier, packet.CharacterName, packet.AccountName, this);
                if (attached)
                {
                    _chatClient = _chatService.GetPlayer(packet.PlayerId);
                    _state = ChatClientConnectionState.Authed;
                }
            }
        }
        finally { _closeLock.Release(); }
        if (attached) await SendPacketAsync(new SmPlayerAuthResponse());
    }

	private async Task HandleChannelRequestAsync(CmChannelRequest packet)
	{
		if (_chatClient == null)
			return;

		if (_options.LogChannelRequests)
			_logger.LogInformation("{Client} requested channel: {Identifier}", _chatClient, packet.ChannelIdentifier);

		var channel = _chatService.RegisterPlayerWithChannel(_chatClient, packet.ChannelRequestId, packet.ChannelIdentifier);
		if (channel != null)
			await SendPacketAsync(new SmChannelResponse(channel.ChannelId, packet.ChannelRequestId));
	}

	private void HandleChannelLeave(CmChannelLeave packet)
	{
		if (_chatClient == null)
			return;

		var channel = _channels.GetChannelById(packet.ChannelId);
		if (!_chatClient.RemoveChannel(channel))
			_logger.LogWarning("{Client}, could not leave channel id {ChannelId}", _chatClient, packet.ChannelId);
	}

	private async Task HandleChannelMessageAsync(CmChannelMessage packet)
	{
		if (_chatClient == null)
			return;

		var channel = _channels.GetChannelById(packet.ChannelId);
		if (channel == null)
			return;

		var message = new Message(channel, packet.Content, _chatClient);
		if (_chatClient.IsGagged())
		{
			var gagTimeMinutes = (_chatClient.GagTime - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) / 1000 / 60;
			message.SetText($"You have been gagged for {gagTimeMinutes} minutes.");
			await SendPacketAsync(new SmChannelMessage(message));
			return;
		}

		try
		{
			await _handlerRegistry.ExecuteHandlersAsync(message, _chatClient, channel);
		}
		catch (HandlerVetoException ex)
		{
			message.SetText(ex.ResponseText);
			await SendPacketAsync(new SmChannelMessage(message));
			return;
		}

		await _broadcastService.BroadcastMessageAsync(message);
	}

	private async Task<byte[]?> ReadExactOrNullAsync(int length)
	{
		var buffer = new byte[length];
		var offset = 0;
		while (offset < length)
		{
            int read;
            try { read = await ReadAsync(buffer, offset, length - offset, TimeSpan.FromSeconds(30)); }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException) { return null; }
			if (read == 0)
				return null;
			offset += read;
		}

		return buffer;
	}
}
