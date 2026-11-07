using UnityEngine;

// Which day of the run the player is on. Static so it survives reloading MainMap for the next day;
// starting a game from the main menu resets it.
public static class DayProgress
{
    public static int CurrentDay { get; private set; } = 1;

    private static bool debugStartDayApplied;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        CurrentDay = 1;
        debugStartDayApplied = false;
    }

    // Testing only: jumps to the given day once per play session, so advancing days afterwards still works.
    public static void ApplyDebugStartDay(int day)
    {
        if (debugStartDayApplied || day < 1)
        {
            return;
        }

        debugStartDayApplied = true;
        CurrentDay = day;
    }

    public static void StartNewGame()
    {
        CurrentDay = 1;
    }

    public static void AdvanceDay()
    {
        CurrentDay++;
    }
}

public struct DaySummary
{
    public int Day;
    public int Goal;
    public int Money;
    public int DeliveredOrders;
    public int PerfectCakes;
    public int PrepMistakes;
    public int WrongDeliveries;
    public int UndeliveredOrders;

    public bool GoalReached
    {
        get { return Money >= Goal; }
    }
}
