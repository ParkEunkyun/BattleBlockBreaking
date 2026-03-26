using System;

public static class RankedRecordCache
{
    public static bool HasLoaded { get; private set; }

    public static int CurrentMmr { get; private set; }
    public static int Victory { get; private set; }
    public static int Draw { get; private set; }
    public static int Defeat { get; private set; }

    public static string Nickname { get; private set; } = "";
    public static string UpdatedAt { get; private set; } = "";

    public static bool HasPreMatchSnapshot { get; private set; }
    public static int PreMatchMmr { get; private set; }
    public static int LastDelta { get; private set; }

    public static event Action Changed;

    public static void ApplyRecord(
        int mmr,
        int victory,
        int draw,
        int defeat,
        string nickname,
        string updatedAt)
    {
        CurrentMmr = mmr;
        Victory = victory;
        Draw = draw;
        Defeat = defeat;
        Nickname = nickname ?? "";
        UpdatedAt = updatedAt ?? "";

        if (HasPreMatchSnapshot)
            LastDelta = CurrentMmr - PreMatchMmr;
        else
            LastDelta = 0;

        HasLoaded = true;
        Changed?.Invoke();
    }

    public static void ApplyNoRecord()
    {
        CurrentMmr = 0;
        Victory = 0;
        Draw = 0;
        Defeat = 0;
        Nickname = "";
        UpdatedAt = "";
        LastDelta = 0;

        HasLoaded = true;
        Changed?.Invoke();
    }

    public static void MarkPreMatchFromCurrent()
    {
        if (!HasLoaded)
            return;

        PreMatchMmr = CurrentMmr;
        HasPreMatchSnapshot = true;
        LastDelta = 0;
        Changed?.Invoke();
    }

    public static string GetFormattedMmrWithDelta()
    {
        if (!HasLoaded)
            return "-";

        string deltaText = LastDelta > 0 ? $"+{LastDelta}" : LastDelta.ToString();
        return $"{CurrentMmr} ({deltaText})";
    }
}