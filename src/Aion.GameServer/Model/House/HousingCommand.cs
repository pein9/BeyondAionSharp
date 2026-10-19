namespace Aion.GameServer.Model.House;

/// <summary>Java house editor and renovation command IDs.</summary>
public enum HousingCommand
{
    BEGIN_EDIT = 1,
    END_EDIT = 2,
    REGISTER_ITEM = 3,
    REMOVE_ITEM = 4,
    PUT_OBJECT = 5,
    MOVE_OBJECT = 6,
    WITHDRAW_OBJECT = 7,
    BEGIN_BUILDING_EDIT = 14,
    END_BUILDING_EDIT = 15,
    CHANGE_BUILDING = 16,
}

public static class HousingCommandExtensions
{
    public static int GetId(this HousingCommand value) => (int)value;

    public static HousingCommand? GetById(int id)
    {
        foreach (HousingCommand value in Enum.GetValues<HousingCommand>())
            if (value.GetId() == id)
                return value;
        return null;
    }
}
