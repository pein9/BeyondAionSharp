namespace Aion.GameServer.Model.House;

/// <summary>Java registered, unused house item lists.</summary>
public enum HousingContainer
{
    OBJECTS = 1,
    DECORATIONS = 2,
}

public static class HousingContainerExtensions
{
    public static int GetId(this HousingContainer value) => (int)value;

    public static HousingContainer? GetById(int id)
    {
        foreach (HousingContainer value in Enum.GetValues<HousingContainer>())
            if (value.GetId() == id)
                return value;
        return null;
    }
}
