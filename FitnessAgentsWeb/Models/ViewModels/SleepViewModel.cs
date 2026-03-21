namespace FitnessAgentsWeb.Models.ViewModels;

public class SleepViewModel
{
    public required string UserId { get; set; }
    public required string Date { get; set; }

    // Overall sleep score (0–100)
    public int SleepScore { get; set; }
    public string SleepScoreLabel { get; set; } = "N/A";

    // Key metrics
    public string TotalSleepDuration { get; set; } = "--";
    public string TotalSleepDurationLabel { get; set; } = "--";
    public string SleepDebt { get; set; } = "--";
    public string SleepDebtLabel { get; set; } = "--";
    public int ConsistencyPercent { get; set; }
    public string ConsistencyLabel { get; set; } = "--";
    public string Waso { get; set; } = "--";
    public string WasoLabel { get; set; } = "--";

    // Primary sleep window
    public string Bedtime { get; set; } = "--";
    public string BedtimeLabel { get; set; } = "--";
    public string WakeTime { get; set; } = "--";
    public int RestoredPercent { get; set; }
    public string RestoredLabel { get; set; } = "--";

    // Sleep stages (durations in seconds + percentages)
    public int AwakeDurationSecs { get; set; }
    public int RemDurationSecs { get; set; }
    public int LightDurationSecs { get; set; }
    public int DeepDurationSecs { get; set; }
    public double AwakePercent { get; set; }
    public double RemPercent { get; set; }
    public double LightPercent { get; set; }
    public double DeepPercent { get; set; }

    // Sleep stage timeline (JSON for chart rendering)
    public string StageTimelineJson { get; set; } = "[]";

    // Vitals during sleep
    public string HrvDuringSleep { get; set; } = "--";
    public string AvgHeartRate { get; set; } = "--";
    public string MinHeartRate { get; set; } = "--";
    public string MaxHeartRate { get; set; } = "--";

    // Heart rate timeline during sleep (JSON)
    public string HeartRateTimelineJson { get; set; } = "[]";

    // Score contributors (0–100 each)
    public int ScoreDuration { get; set; }
    public string ScoreDurationLabel { get; set; } = "--";
    public int ScoreConsistency { get; set; }
    public string ScoreConsistencyLabel { get; set; } = "--";
    public int ScoreDeepSleep { get; set; }
    public string ScoreDeepSleepLabel { get; set; } = "--";
    public int ScoreRemSleep { get; set; }
    public string ScoreRemSleepLabel { get; set; } = "--";
    public int ScoreWaso { get; set; }
    public string ScoreWasoLabel { get; set; } = "--";
    public int ScoreHrv { get; set; }
    public string ScoreHrvLabel { get; set; } = "--";
    public int ScoreHeartRateDip { get; set; }
    public string ScoreHeartRateDipLabel { get; set; } = "--";

    // Dynamic insight callout
    public string InsightTitle { get; set; } = "Sleep analysis";
    public string InsightDescription { get; set; } = "No sleep data available for analysis.";

    // 7-day sleep trend (JSON arrays)
    public string SleepTrendJson { get; set; } = "[]";
    public string SleepScoreTrendJson { get; set; } = "[]";
    public string DayLabelsJson { get; set; } = "[]";

    public static (string Title, string Description) BuildSleepInsight(
        int totalSleepSecs, int deepSleepSecs, int remSleepSecs, int wasoSecs, int totalTimeInBedSecs)
    {
        // ── Classify dimensions ──
        // Duration: Ideal >=7h, Short 5-7h, VeryShort <5h
        bool isIdealDuration = totalSleepSecs >= 25200;   // 7h
        bool isShortDuration = totalSleepSecs >= 18000;   // 5h
        bool isVeryShort = totalSleepSecs < 18000;

        // Continuity: Continuous WASO<=10min, Moderate 10-30min, Fragmented >30min
        bool isContinuous = wasoSecs <= 600;
        bool isModerateWake = wasoSecs <= 1800;

        // Deep sleep: Plenty >=1.5h, Adequate 45min-1.5h, Low <45min
        bool isPlentyDeep = deepSleepSecs >= 5400;   // 1.5h
        bool isAdequateDeep = deepSleepSecs >= 2700;  // 45m

        // REM: Good >=1.5h, Adequate 45min-1.5h
        bool isGoodRem = remSleepSecs >= 5400;

        // Efficiency
        double efficiency = totalTimeInBedSecs > 0 ? (double)totalSleepSecs / totalTimeInBedSecs * 100 : 0;
        bool isEfficient = efficiency >= 85;

        // ── Build title ──
        var titleParts = new List<string>();

        if (isIdealDuration)
            titleParts.Add("Ideal duration");
        else if (isShortDuration)
            titleParts.Add("Shorter than ideal");
        else
            titleParts.Add("Short");

        if (isContinuous)
            titleParts.Add("continuous");
        else if (isModerateWake)
            titleParts.Add("slightly interrupted");
        else
            titleParts.Add("fragmented");

        // Append notable positive trait
        if (isPlentyDeep)
            titleParts.Add("plenty of deep sleep");
        else if (isGoodRem)
            titleParts.Add("good REM sleep");
        else if (isEfficient && !isIdealDuration)
            titleParts.Add("but efficient");

        string title = string.Join(" and ", titleParts.Take(2));
        if (titleParts.Count > 2)
            title += " with " + titleParts[2];

        // Capitalize first letter
        title = char.ToUpper(title[0]) + title[1..];

        // ── Build description ──
        var desc = new List<string>();

        if (isIdealDuration && isContinuous)
            desc.Add("You slept an ideal duration and it was continuous & uninterrupted, which contributes to a better mood and fewer sleep issues.");
        else if (isIdealDuration)
            desc.Add($"You reached an ideal sleep duration, but woke up during the night ({FormatDuration(wasoSecs)} awake).");
        else if (isShortDuration && isContinuous)
            desc.Add("Your sleep was shorter than recommended but continuous & uninterrupted.");
        else if (isShortDuration)
            desc.Add($"Your sleep was shorter than recommended and had some interruptions ({FormatDuration(wasoSecs)} awake).");
        else
            desc.Add($"You slept significantly less than the recommended 7-8 hours. Try to aim for an earlier bedtime.");

        if (isPlentyDeep)
            desc.Add("Excellent deep sleep supports physical recovery and immune function.");
        else if (isAdequateDeep)
            desc.Add("Adequate deep sleep for basic recovery.");
        else if (deepSleepSecs > 0)
            desc.Add("Deep sleep was lower than ideal — regular exercise and a cool bedroom may help.");

        if (isGoodRem)
            desc.Add("Strong REM sleep supports memory consolidation and emotional regulation.");

        return (title, string.Join(" ", desc));
    }

    public static string FormatDuration(int totalSeconds)
    {
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        return hours > 0 ? $"{hours}h {minutes}m" : $"{minutes}m";
    }
}
