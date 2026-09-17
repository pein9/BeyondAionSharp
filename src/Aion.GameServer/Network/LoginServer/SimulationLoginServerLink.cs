using System.Collections.Concurrent;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.LoginServer.ServerPackets;

namespace Aion.GameServer.Network.LoginServer;

/// <summary>In-process login-server link used by deterministic player simulations.</summary>
public sealed class SimulationLoginServerLink : ILoginServerLink
{
	private readonly IReadOnlyDictionary<int, SimulationLoginAccount> _accounts;
	private readonly Action<SimulationAccountAuthenticationResponse> _authenticationResponse;
	private readonly Action<AionConnection> _onDisconnect;
	private readonly ConcurrentQueue<LoginServerPacket> _sentPackets = new();

	public SimulationLoginServerLink(
		LoginServer owner,
		IReadOnlyDictionary<int, SimulationLoginAccount> accounts,
		int gameServerCount = 1)
		: this(
			accounts,
			response => owner.AccountAuthenticationResponse(
				response.AccountId,
				response.AccountName,
				response.Accepted,
				response.CreationDate,
				response.AccountTime,
				response.AccessLevel,
				response.Membership,
				response.AllowedHddSerial),
			owner.OnDisconnectCore,
			gameServerCount)
	{
	}

	internal SimulationLoginServerLink(
		IReadOnlyDictionary<int, SimulationLoginAccount> accounts,
		Action<SimulationAccountAuthenticationResponse> authenticationResponse,
		Action<AionConnection>? onDisconnect = null,
		int gameServerCount = 1)
	{
		ArgumentNullException.ThrowIfNull(accounts);
		ArgumentNullException.ThrowIfNull(authenticationResponse);
		if (gameServerCount <= 0)
			throw new ArgumentOutOfRangeException(nameof(gameServerCount));

		_accounts = accounts;
		_authenticationResponse = authenticationResponse;
		_onDisconnect = onDisconnect ?? (_ => { });
		GameServerCount = gameServerCount;
	}

	public bool IsAuthed => true;

	public int GameServerCount { get; }

	public IReadOnlyList<LoginServerPacket> SentPackets => _sentPackets.ToArray();

	public int GetGameServerCount() => GameServerCount;

	public bool SendPacket(LoginServerPacket packet)
	{
		ArgumentNullException.ThrowIfNull(packet);
		_sentPackets.Enqueue(packet);
		if (packet is SmAccountAuth request)
		{
			bool accepted = _accounts.TryGetValue(request.AccountId, out SimulationLoginAccount? account);
			_authenticationResponse(new SimulationAccountAuthenticationResponse(
				request.AccountId,
				account?.AccountName ?? string.Empty,
				accepted,
				account?.CreationDate ?? 0,
				new AccountTime(),
				account?.AccessLevel ?? 0,
				account?.Membership ?? 0,
				account?.AllowedHddSerial ?? string.Empty));
		}
		return true;
	}

	public void OnDisconnect(AionConnection connection) => _onDisconnect(connection);
}

public sealed record SimulationLoginAccount(
	string AccountName,
	sbyte AccessLevel,
	sbyte Membership = 0,
	long CreationDate = 0,
	string AllowedHddSerial = "");

public sealed record SimulationAccountAuthenticationResponse(
	int AccountId,
	string AccountName,
	bool Accepted,
	long CreationDate,
	AccountTime AccountTime,
	sbyte AccessLevel,
	sbyte Membership,
	string AllowedHddSerial);
