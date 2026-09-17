using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.LoginServer;

/// <summary>
/// Transport-facing seam behind the Java-shaped <see cref="LoginServer"/> singleton facade.
/// Production uses the connector itself; SIM supplies an in-process implementation.
/// </summary>
public interface ILoginServerLink
{
	bool IsAuthed { get; }

	int GetGameServerCount();

	bool SendPacket(LoginServerPacket packet);

	void OnDisconnect(AionConnection connection);
}
