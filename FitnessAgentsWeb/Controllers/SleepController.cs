using System.Text.Json;
using FitnessAgentsWeb.Core.Helpers;
using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Models;
using FitnessAgentsWeb.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessAgentsWeb.Controllers;

[Authorize]
public class SleepController : Controller
{
    private readonly IStorageRepository _storageRepository;
    private static readonly TimeZoneInfo IstZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

    public SleepController(IStorageRepository storageRepository)
    {
        _storageRepository = storageRepository;
    }

    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Index(string? userId = null)
    {
        userId = ResolveUserId(userId);
        ViewData["ActiveNav"] = "sleep";

        var healthData = await _storageRepository.GetTodayHealthDataAsync(userId);
        var model = BuildSleepViewModel(userId, healthData);
        return View(model);
    }

    private SleepViewModel BuildSleepViewModel(string userId, HealthExportPayload? hc)
    {
        var nowIst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IstZone);
        var targetDate = nowIst.Date;

        var model = new SleepViewModel
        {
            UserId = userId,
            Date = targetDate.ToString("ddd, dd MMM")
        };

        if (hc?.Sleep == null || hc.Sleep.Count == 0)
            return model;

        // Determine target sleep window (previous night's sleep for today)
        var sleepWindowStart = targetDate.AddHours(-12);
        var sleepWindowEnd = targetDate.AddHours(12);

        bool IsInSleepWindow(DateTime utcTime)
        {
            var ist = TimeZoneInfo.ConvertTimeFromUtc(utcTime, IstZone);
            return ist >= sleepWindowStart && ist < sleepWindowEnd;
        }

        var sessions = hc.Sleep.Where(s => IsInSleepWindow(s.SessionEndTime)).ToList();
        if (sessions.Count == 0)
            return model;

        var allStages = sessions.SelectMany(s => s.Stages).OrderBy(st => st.StartTime).ToList();
        if (allStages.Count == 0)
            return model;

        // ── Stage durations ──
        // Stage codes: "1" = Awake, "6" = REM, "5" = Light, "4" = Deep, "2" = Out of bed
        int awakeSecs = allStages.Where(st => st.Stage == "1").Sum(st => st.DurationSeconds);
        int remSecs = allStages.Where(st => st.Stage == "6").Sum(st => st.DurationSeconds);
        int lightSecs = allStages.Where(st => st.Stage == "5").Sum(st => st.DurationSeconds);
        int deepSecs = allStages.Where(st => st.Stage == "4").Sum(st => st.DurationSeconds);

        int totalSleepSecs = remSecs + lightSecs + deepSecs; // actual sleep (excludes awake)
        int totalTimeInBedSecs = sessions.Sum(s => s.DurationSeconds);
        int totalStageSecs = awakeSecs + remSecs + lightSecs + deepSecs;

        model.AwakeDurationSecs = awakeSecs;
        model.RemDurationSecs = remSecs;
        model.LightDurationSecs = lightSecs;
        model.DeepDurationSecs = deepSecs;

        if (totalStageSecs > 0)
        {
            model.AwakePercent = Math.Round((double)awakeSecs / totalStageSecs * 100, 0);
            model.RemPercent = Math.Round((double)remSecs / totalStageSecs * 100, 0);
            model.LightPercent = Math.Round((double)lightSecs / totalStageSecs * 100, 0);
            model.DeepPercent = Math.Round((double)deepSecs / totalStageSecs * 100, 0);
        }

        // ── Total sleep duration ──
        model.TotalSleepDuration = SleepViewModel.FormatDuration(totalSleepSecs);
        model.TotalSleepDurationLabel = totalSleepSecs >= 25200 ? "Excellent" : totalSleepSecs >= 21600 ? "Good" : totalSleepSecs >= 18000 ? "Fair" : "Poor";

        // ── Sleep debt (difference from 8h target) ──
        int targetSleepSecs = 8 * 3600;
        int debtSecs = Math.Max(0, targetSleepSecs - totalSleepSecs);
        model.SleepDebt = SleepViewModel.FormatDuration(debtSecs);
        model.SleepDebtLabel = debtSecs == 0 ? "Excellent" : debtSecs <= 1800 ? "Good" : debtSecs <= 3600 ? "Fair" : "Poor";

        // ── WASO (Wake After Sleep Onset) ──
        // Awake time within the sleep session (excluding initial falling asleep)
        var sleepOnset = allStages.FirstOrDefault(st => st.Stage != "1" && st.Stage != "2");
        var lastSleep = allStages.LastOrDefault(st => st.Stage != "1" && st.Stage != "2");
        int wasoSecs = 0;
        if (sleepOnset != null && lastSleep != null)
        {
            wasoSecs = allStages
                .Where(st => st.Stage == "1" && st.StartTime >= sleepOnset.StartTime && st.EndTime <= lastSleep.EndTime)
                .Sum(st => st.DurationSeconds);
        }
        model.Waso = SleepViewModel.FormatDuration(wasoSecs);
        model.WasoLabel = wasoSecs <= 300 ? "Excellent" : wasoSecs <= 900 ? "Good" : wasoSecs <= 1800 ? "Fair" : "Poor";

        // ── Sleep efficiency / consistency ──
        double efficiency = totalTimeInBedSecs > 0 ? (double)totalSleepSecs / totalTimeInBedSecs * 100 : 0;
        model.ConsistencyPercent = (int)Math.Round(efficiency);
        model.ConsistencyLabel = efficiency >= 90 ? "Excellent" : efficiency >= 80 ? "Good" : efficiency >= 70 ? "Fair" : "Poor";

        // ── Restored percent (sleep efficiency as proxy) ──
        model.RestoredPercent = (int)Math.Round(efficiency);
        model.RestoredLabel = efficiency >= 90 ? "Excellent" : efficiency >= 80 ? "Good" : efficiency >= 70 ? "Fair" : "Poor";

        // ── Primary sleep window ──
        var earliestStage = allStages.OrderBy(st => st.StartTime).First();
        var latestStage = allStages.OrderByDescending(st => st.EndTime).First();
        model.Bedtime = TimeZoneInfo.ConvertTimeFromUtc(earliestStage.StartTime, IstZone).ToString("hh:mm tt");
        model.BedtimeLabel = TimeZoneInfo.ConvertTimeFromUtc(earliestStage.StartTime, IstZone).Hour >= 21 &&
                             TimeZoneInfo.ConvertTimeFromUtc(earliestStage.StartTime, IstZone).Hour <= 23 ? "Good" : "Fair";
        model.WakeTime = TimeZoneInfo.ConvertTimeFromUtc(latestStage.EndTime, IstZone).ToString("hh:mm tt");

        // ── Stage timeline for chart ──
        var stageTimeline = allStages.Select(st => new
        {
            time = TimeZoneInfo.ConvertTimeFromUtc(st.StartTime, IstZone).ToString("HH:mm"),
            stage = st.Stage switch { "1" => "Awake", "6" => "REM", "5" => "Light", "4" => "Deep", _ => "Light" },
            value = st.Stage switch { "1" => 4, "6" => 3, "5" => 2, "4" => 1, _ => 2 },
            durationMin = st.DurationSeconds / 60
        });
        model.StageTimelineJson = JsonSerializer.Serialize(stageTimeline);

        // ── Vitals during sleep ──
        if (hc.HeartRate?.Count > 0)
        {
            var sleepHr = hc.HeartRate
                .Where(hr => IsInSleepWindow(hr.Time))
                .Where(hr =>
                {
                    var hrIst = TimeZoneInfo.ConvertTimeFromUtc(hr.Time, IstZone);
                    return hrIst >= TimeZoneInfo.ConvertTimeFromUtc(earliestStage.StartTime, IstZone) &&
                           hrIst <= TimeZoneInfo.ConvertTimeFromUtc(latestStage.EndTime, IstZone);
                })
                .OrderBy(hr => hr.Time)
                .ToList();

            if (sleepHr.Count > 0)
            {
                model.AvgHeartRate = Math.Round(sleepHr.Average(hr => hr.Bpm)).ToString();
                model.MinHeartRate = sleepHr.Min(hr => hr.Bpm).ToString();
                model.MaxHeartRate = sleepHr.Max(hr => hr.Bpm).ToString();

                var hrTimeline = sleepHr.Select(hr => new
                {
                    time = TimeZoneInfo.ConvertTimeFromUtc(hr.Time, IstZone).ToString("HH:mm"),
                    bpm = hr.Bpm
                });
                model.HeartRateTimelineJson = JsonSerializer.Serialize(hrTimeline);
            }
        }

        if (hc.HRV?.Count > 0)
        {
            var sleepHrv = hc.HRV
                .Where(h => IsInSleepWindow(h.Time))
                .Where(h =>
                {
                    var hIst = TimeZoneInfo.ConvertTimeFromUtc(h.Time, IstZone);
                    return hIst >= TimeZoneInfo.ConvertTimeFromUtc(earliestStage.StartTime, IstZone) &&
                           hIst <= TimeZoneInfo.ConvertTimeFromUtc(latestStage.EndTime, IstZone);
                })
                .ToList();

            if (sleepHrv.Count > 0)
                model.HrvDuringSleep = Math.Round(sleepHrv.Average(h => h.Rmssd)).ToString();
        }

        // ── Compute overall sleep score ──
        model.SleepScore = ComputeDetailedSleepScore(model, totalSleepSecs, deepSecs, remSecs, wasoSecs, totalTimeInBedSecs);
        model.SleepScoreLabel = model.SleepScore >= 80 ? "Good" :
                                model.SleepScore >= 60 ? "Fair" :
                                model.SleepScore >= 40 ? "Poor" : "Bad";

        // ── Dynamic insight callout ──
        var (insightTitle, insightDesc) = SleepViewModel.BuildSleepInsight(totalSleepSecs, deepSecs, remSecs, wasoSecs, totalTimeInBedSecs);
        model.InsightTitle = insightTitle;
        model.InsightDescription = insightDesc;

        // ── Score contributors ──
        ComputeScoreContributors(model, totalSleepSecs, deepSecs, remSecs, wasoSecs, totalTimeInBedSecs);

        // ── 7-day sleep trend ──
        Compute7DayTrend(model, hc, targetDate);

        return model;
    }

    private static int ComputeDetailedSleepScore(SleepViewModel model, int totalSleepSecs, int deepSecs, int remSecs, int wasoSecs, int totalTimeInBedSecs)
    {
        // Duration (25%): <5h = 0, 8h = 100
        double durationScore = Math.Clamp((totalSleepSecs - 18000) / 10800.0 * 100, 0, 100);

        // Deep sleep (20%): 0 = 0, >=1.5h = 100
        double deepScore = Math.Clamp(deepSecs / 5400.0 * 100, 0, 100);

        // REM (15%): 0 = 0, >=1.5h = 100
        double remScore = Math.Clamp(remSecs / 5400.0 * 100, 0, 100);

        // Efficiency (15%): 60% = 0, 95% = 100
        double efficiencyScore = 0;
        if (totalTimeInBedSecs > 0)
        {
            double eff = (double)totalSleepSecs / totalTimeInBedSecs * 100;
            efficiencyScore = Math.Clamp((eff - 60) / 35 * 100, 0, 100);
        }

        // WASO (10%): >30min = 0, 0min = 100
        double wasoScore = Math.Clamp((1800 - wasoSecs) / 1800.0 * 100, 0, 100);

        // HRV (10%): use model data if available
        double hrvScore = 50; // default mid
        if (double.TryParse(model.HrvDuringSleep, out double hrv) && hrv > 0)
            hrvScore = Math.Clamp((hrv - 20) / 40 * 100, 0, 100);

        // HR dip (5%): approximation from avg HR
        double hrDipScore = 50;

        return (int)Math.Round(
            durationScore * 0.25 +
            deepScore * 0.20 +
            remScore * 0.15 +
            efficiencyScore * 0.15 +
            wasoScore * 0.10 +
            hrvScore * 0.10 +
            hrDipScore * 0.05
        );
    }

    private static void ComputeScoreContributors(SleepViewModel model, int totalSleepSecs, int deepSecs, int remSecs, int wasoSecs, int totalTimeInBedSecs)
    {
        // Total sleep duration
        model.ScoreDuration = (int)Math.Round(Math.Clamp((totalSleepSecs - 18000) / 10800.0 * 100, 0, 100));
        model.ScoreDurationLabel = SleepViewModel.FormatDuration(totalSleepSecs);

        // Sleep consistency (efficiency)
        if (totalTimeInBedSecs > 0)
        {
            double eff = (double)totalSleepSecs / totalTimeInBedSecs * 100;
            model.ScoreConsistency = (int)Math.Round(Math.Clamp((eff - 60) / 35 * 100, 0, 100));
            model.ScoreConsistencyLabel = Math.Round(eff).ToString("0") + "%";
        }

        // Deep sleep
        model.ScoreDeepSleep = (int)Math.Round(Math.Clamp(deepSecs / 5400.0 * 100, 0, 100));
        model.ScoreDeepSleepLabel = SleepViewModel.FormatDuration(deepSecs);

        // REM sleep
        model.ScoreRemSleep = (int)Math.Round(Math.Clamp(remSecs / 5400.0 * 100, 0, 100));
        model.ScoreRemSleepLabel = SleepViewModel.FormatDuration(remSecs);

        // WASO
        model.ScoreWaso = (int)Math.Round(Math.Clamp((1800 - wasoSecs) / 1800.0 * 100, 0, 100));
        model.ScoreWasoLabel = SleepViewModel.FormatDuration(wasoSecs);

        // HRV
        if (double.TryParse(model.HrvDuringSleep, out double hrv) && hrv > 0)
        {
            model.ScoreHrv = (int)Math.Round(Math.Clamp((hrv - 20) / 40 * 100, 0, 100));
            model.ScoreHrvLabel = hrv.ToString("0") + " ms";
        }

        // Heart rate dip
        if (int.TryParse(model.AvgHeartRate, out int avgHr) && int.TryParse(model.MinHeartRate, out int minHr) && avgHr > 0)
        {
            double dipPercent = (double)(avgHr - minHr) / avgHr * 100;
            model.ScoreHeartRateDip = (int)Math.Round(Math.Clamp(dipPercent / 20 * 100, 0, 100));
            model.ScoreHeartRateDipLabel = Math.Round(dipPercent).ToString("0") + "%";
        }
    }

    private void Compute7DayTrend(SleepViewModel model, HealthExportPayload hc, DateTime targetDate)
    {
        var days = Enumerable.Range(0, 7).Select(i => targetDate.AddDays(-6 + i)).ToList();

        var sleepHours = new List<double>();
        var sleepScores = new List<int>();
        var dayLabels = new List<string>();

        foreach (var day in days)
        {
            dayLabels.Add(day.ToString("ddd"));

            var windowStart = day.AddHours(-12);
            var windowEnd = day.AddHours(12);

            var daySessions = hc.Sleep.Where(s =>
            {
                var ist = TimeZoneInfo.ConvertTimeFromUtc(s.SessionEndTime, IstZone);
                return ist >= windowStart && ist < windowEnd;
            }).ToList();

            var dayStages = daySessions.SelectMany(s => s.Stages).ToList();
            int sleepSecs = dayStages.Where(st => st.Stage != "1" && st.Stage != "2").Sum(st => st.DurationSeconds);
            int deepSecs = dayStages.Where(st => st.Stage == "4").Sum(st => st.DurationSeconds);
            int timeInBed = daySessions.Sum(s => s.DurationSeconds);

            sleepHours.Add(Math.Round(sleepSecs / 3600.0, 1));

            // Simple score for trend
            if (sleepSecs > 0)
            {
                double durationScore = Math.Clamp((sleepSecs - 18000) / 10800.0 * 100, 0, 100);
                double deepScore = Math.Clamp(deepSecs / 5400.0 * 100, 0, 100);
                double effScore = timeInBed > 0 ? Math.Clamp(((double)sleepSecs / timeInBed * 100 - 60) / 35 * 100, 0, 100) : 0;
                sleepScores.Add((int)Math.Round(durationScore * 0.40 + deepScore * 0.30 + effScore * 0.30));
            }
            else
            {
                sleepScores.Add(0);
            }
        }

        model.SleepTrendJson = JsonSerializer.Serialize(sleepHours);
        model.SleepScoreTrendJson = JsonSerializer.Serialize(sleepScores);
        model.DayLabelsJson = JsonSerializer.Serialize(dayLabels);
    }

    private string ResolveUserId(string? userId)
    {
        if (User.IsInRole("User"))
            return User.Identity?.Name ?? "default_user";

        return string.IsNullOrEmpty(userId) ? User.Identity?.Name ?? "default_user" : userId;
    }
}
