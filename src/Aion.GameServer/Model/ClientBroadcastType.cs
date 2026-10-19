namespace Aion.GameServer.Model;

/// <summary>Java 6dd3aa507: client broadcast destinations, with explicit wire IDs.</summary>
public enum ClientBroadcastType
{
    TEAM = 0,
    HOUSE = 1,
    HOUSE_LAND = 2,
    HOUSE_UNCHECKED = 3
}

public static class ClientBroadcastTypeExtensions
{
    public static ClientBroadcastType? GetById(int id) => id switch
    {
        0 or 1 or 2 or 3 => (ClientBroadcastType)id,
        _ => null
    };
}
