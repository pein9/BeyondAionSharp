using System.Net;

namespace Aion.GameServer.Commons.Network;

/// <summary>
/// Java parity: commons/network/ServerCfg (-Nemesiss-, Neon). NioServer listen configuration.
/// java.net.InetSocketAddress -> System.Net.IPEndPoint.
/// </summary>
public sealed record ServerCfg(IPEndPoint Address, string ClientDescription, ConnectionFactory ConnectionFactory)
{
    public string GetIP() => Address.Address.ToString();

    public int GetPort() => Address.Port;

    public string GetAddressInfo() => Aion.GameServer.Commons.Utils.NetworkUtils.GetAddressInfo(Address);
}
