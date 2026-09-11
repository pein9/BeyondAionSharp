using System;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Items;

namespace Aion.GameServer.Model.Items;

/// <summary>
/// A stone socketed into an item (manastone/godstone/etc.).
/// Java parity: model/items/ItemStone (implements StatOwner, Persistable, L10n).
/// </summary>
public class ItemStone : IStatOwner, IPersistable, IL10n
{
    private readonly int _itemObjId;
    private readonly int _itemId;
    private int _slot;
    private IPersistable.PersistentState _persistentState;

    // Java parity: nested enum ItemStone.ItemStoneType
    public enum ItemStoneType
    {
        MANASTONE,
        GODSTONE,
        FUSIONSTONE,
        IDIANSTONE,
    }

    public ItemStone(int itemObjId, int itemId, int slot, IPersistable.PersistentState persistentState)
    {
        _itemObjId = itemObjId;
        _itemId = itemId;
        _slot = slot;
        _persistentState = persistentState;
        // Java parity: Objects.requireNonNull(getItemTemplate(), () -> "Invalid item ID: " + itemId)
        if (GetItemTemplate() == null)
            throw new ArgumentNullException(null, "Invalid item ID: " + itemId);
    }

    public int GetItemObjId() => _itemObjId;
    public int GetItemId() => _itemId;

    public ItemTemplate GetItemTemplate() => DataManager.ITEM_DATA.GetItemTemplate(_itemId);
    public int GetSlot() => _slot;

    public void SetSlot(int slot)
    {
        _slot = slot;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    // Java parity: getPersistentState()
    public IPersistable.PersistentState GetPersistentState() => _persistentState;

    // Java parity: setPersistentState(PersistentState) — note the Java fallthrough on UPDATE_REQUIRED.
    public void SetPersistentState(IPersistable.PersistentState persistentState)
    {
        switch (persistentState)
        {
            case IPersistable.PersistentState.DELETED:
                _persistentState = _persistentState == IPersistable.PersistentState.NEW
                    ? IPersistable.PersistentState.NOACTION
                    : IPersistable.PersistentState.DELETED;
                break;
            case IPersistable.PersistentState.UPDATE_REQUIRED:
                if (_persistentState == IPersistable.PersistentState.NEW)
                    break;
                _persistentState = persistentState; // Java falls through to default
                break;
            default:
                _persistentState = persistentState;
                break;
        }
    }

    // Java parity: L10n::getL10nId()
    public int GetL10nId() => GetItemTemplate().GetL10nId();

    // Java parity: L10n default getL10n(), declared here so callers keep the template's non-null string type like Item.GetL10n().
    public string GetL10n() => GetItemTemplate().GetL10n()!;
}
