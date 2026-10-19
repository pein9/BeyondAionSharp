using Aion.GameServer.Model.Items.Storage;

namespace Aion.GameServer.Model.GameObjects.Players;

internal sealed record StorageManager(StorageType Type, int ObjectId)
{
    internal bool Matches(StorageType storageType) => Type == storageType
        || storageType == StorageType.ACCOUNT_WAREHOUSE && Type == StorageType.REGULAR_WAREHOUSE;
}
