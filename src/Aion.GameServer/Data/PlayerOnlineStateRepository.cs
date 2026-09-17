using Aion.GameServer.Dao;

namespace Aion.GameServer.Data;

public interface IPlayerOnlineStateRepository
{
	ValueTask SetAllPlayersOfflineAsync(CancellationToken cancellationToken = default);
}

public sealed class NoOpPlayerOnlineStateRepository : IPlayerOnlineStateRepository
{
	public ValueTask SetAllPlayersOfflineAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.CompletedTask;
	}
}

public sealed class PlayerDaoOnlineStateRepository : IPlayerOnlineStateRepository
{
	public ValueTask SetAllPlayersOfflineAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		PlayerDAO.SetAllPlayersOffline();
		return ValueTask.CompletedTask;
	}
}
