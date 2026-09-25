using UnityEngine;

// Which day of the run the player is on. Static so it survives reloading MainMap for the next day;
// starting a game from the main menu resets it.
public static class DayProgress
{
    public static int CurrentDay { get; private set; } = 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        CurrentDay = 1;
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
