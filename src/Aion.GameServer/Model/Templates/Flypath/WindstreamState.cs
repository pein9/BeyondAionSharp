namespace Aion.GameServer.Model.Templates.Flypath;

/// <summary>Explicit wire IDs from Java 332d54885; unknown IDs resolve to null.</summary>
public enum WindstreamState
{
    ENTER = 0,
    ENTERED = 1,
    LEAVE_GLIDING = 2,
    LEAVE = 3,
    LEFT = 4,
    RESET = 5,
    START_BOOST = 7,
    END_BOOST = 8
}

public static class WindstreamStateExtensions
{
    public static WindstreamState? GetById(int id) => id switch
    {
        0 or 1 or 2 or 3 or 4 or 5 or 7 or 8 => (WindstreamState)id,
        _ => null
    };
}
