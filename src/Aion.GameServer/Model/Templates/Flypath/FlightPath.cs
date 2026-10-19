using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Templates.Flypath;

/// <summary>Java parity: model/templates/flypath/FlightPath.</summary>
public class FlightPath
{
    private const float BOOST_SPEED = 1.5f;
    private const int BOOST_COOLDOWN = 15000;
    // Refuse progress more than two seconds ahead of elapsed time, as in Java 332d54885.
    private const int MAX_PROGRESS_LEAD = 2000;
    private readonly Type type;
    private readonly int id;
    private int distance;
    private int boostStart = -1, boostEnd = -1, boostedTime;
    private readonly int startDistance;
    private readonly long startTime = SystemClock.CurrentMillis();

    public FlightPath(Type type, int id, int distance)
    {
        this.type = type;
        this.id = id;
        this.distance = distance;
        startDistance = distance;
    }

    public Type GetType_()
    {
        return type;
    }

    public int GetId()
    {
        return id;
    }

    public int GetDistance()
    {
        return distance;
    }

    public void SetDistance(int distance)
    {
        this.distance = distance;
    }

    public long GetElapsedTime() => SystemClock.CurrentMillis() - startTime;

    public bool IsProgressReachable(int progress) =>
        unchecked(progress - startDistance) <= SystemClock.CurrentMillis() - startTime + MAX_PROGRESS_LEAD;

    public bool StartBoost(int progress)
    {
        if (boostStart >= 0 && progress < unchecked(boostStart + BOOST_COOLDOWN))
            return false;
        boostStart = progress;
        boostEnd = -1;
        return true;
    }

    public bool EndBoost(int progress)
    {
        if (boostStart < 0 || boostEnd >= 0)
            return false;
        boostEnd = progress;
        boostedTime = unchecked(boostedTime + progress - boostStart);
        return true;
    }

    public int GetBoostGain(int progress)
    {
        int time = boostStart < 0 || boostEnd >= 0 || progress <= boostStart ? boostedTime : unchecked(boostedTime + progress - boostStart);
        return JavaMath.ToInt(time * (BOOST_SPEED - 1));
    }

    public enum Type
    {
        FLIGHT_TRANSPORTER,
        WINDSTREAM,
    }
}
