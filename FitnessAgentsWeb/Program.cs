using FitnessAgentsWeb.Models;
using FitnessAgentsWeb.Tools;
using Markdig;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using OpenAI;
using System;
using System.ClientModel;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

string aiModel = builder.Configuration["AiSettings:Model"];
string aiKey = builder.Configuration["AiSettings:ApiKey"];
string aiEndpoint = builder.Configuration["AiSettings:Endpoint"];
string appPassword = builder.Configuration["SMTP:AppPassword"];

// --- DEBOUNCING STATE ---
// We keep track of the last time the AI was triggered in the server's memory
DateTime lastRunTime = DateTime.MinValue;
object runLock = new object();

// 1. Establish the secure App_Data path for MonsterASP
string appDataFolder = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(appDataFolder); // Ensures the folder exists

// 2. Define the Webhook Endpoint
app.MapPost("/api/generate-workout", async (HttpContext context) =>
{
    try
    {
        Console.WriteLine("[System] Webhook triggered by Android app...");

        // 1. Read the incoming webhook data
        using var reader = new StreamReader(context.Request.Body);
        var incomingJson = await reader.ReadToEndAsync();
        string healthFilePath = Path.Combine(appDataFolder, "health_connect_today.json");

        var newPayload = JsonSerializer.Deserialize<HealthExportPayload>(incomingJson);

        // Declare the final payload outside the IF blocks so the background task can use it!
        HealthExportPayload finalPayloadToProcess = newPayload;

        if (newPayload != null)
        {
            if (File.Exists(healthFilePath))
            {
                var existingJson = await File.ReadAllTextAsync(healthFilePath);
                var existingPayload = JsonSerializer.Deserialize<HealthExportPayload>(existingJson);

                if (existingPayload != null)
                {
                    DateTime cutoff = DateTime.UtcNow.AddDays(-7);

                    // Merge and prune into our outer variable
                    finalPayloadToProcess = new HealthExportPayload
                    {
                        // 1. Sleep (Unchanged logic, updated model)
                        Sleep = existingPayload.Sleep.Concat(newPayload.Sleep)
                            .GroupBy(s => s.SessionEndTime).Select(g => g.First())
                            .Where(s => s.SessionEndTime >= cutoff).ToList(),

                        // 2. Steps (Unchanged logic)
                        Steps = existingPayload.Steps.Concat(newPayload.Steps)
                            .GroupBy(s => s.EndTime).Select(g => g.First())
                            .Where(s => s.EndTime >= cutoff).ToList(),

                        // 3. Heart Rate (Detailed list)
                        HeartRate = existingPayload.HeartRate.Concat(newPayload.HeartRate)
                            .GroupBy(r => r.Time).Select(g => g.First())
                            .Where(r => r.Time >= cutoff).ToList(),

                        // 4. Resting Heart Rate (Specific metrics)
                        RestingHeartRate = existingPayload.RestingHeartRate.Concat(newPayload.RestingHeartRate)
                            .GroupBy(r => r.Time).Select(g => g.First())
                            .Where(r => r.Time >= cutoff).ToList(),

                        // 5. HRV (CRITICAL ADDITION)
                        HRV = existingPayload.HRV.Concat(newPayload.HRV)
                            .GroupBy(h => h.Time).Select(g => g.First())
                            .Where(h => h.Time >= cutoff).ToList(),

                        // 6. Active Calories
                        ActiveCalories = existingPayload.ActiveCalories.Concat(newPayload.ActiveCalories)
                            .GroupBy(c => c.EndTime).Select(g => g.First())
                            .Where(c => c.EndTime >= cutoff).ToList(),

                        // 7. Total Calories (CRITICAL ADDITION)
                        TotalCalories = existingPayload.TotalCalories.Concat(newPayload.TotalCalories)
                            .GroupBy(c => c.EndTime).Select(g => g.First())
                            .Where(c => c.EndTime >= cutoff).ToList(),

                        // 8. Distance
                        Distance = existingPayload.Distance.Concat(newPayload.Distance)
                            .GroupBy(d => d.EndTime).Select(g => g.First())
                            .Where(d => d.EndTime >= cutoff).ToList(),

                        // 9. Exercise
                        Exercise = existingPayload.Exercise.Concat(newPayload.Exercise)
                            .GroupBy(e => e.StartTime).Select(g => g.First())
                            .Where(e => e.StartTime >= cutoff).ToList()
                    };

                    // Re-serialize the newly merged object back to JSON text
                    incomingJson = JsonSerializer.Serialize(finalPayloadToProcess, new JsonSerializerOptions { WriteIndented = true });
                }
            }

            lock (runLock)
            {
                // If it has been less than 10 minutes since the last run, ignore the AI trigger
                if ((DateTime.UtcNow - lastRunTime).TotalMinutes < 2)
                {
                    Console.WriteLine("[System] Ignored duplicate trigger (Cooldown active).");
                    // We still return OK so the phone knows the data was received
                    return Results.Ok("Data saved. AI skipped due to recent execution.");
                }

                // Otherwise, update the timer to NOW
                lastRunTime = DateTime.UtcNow;
            }

            // Save the fully merged data back to the disk
            await File.WriteAllTextAsync(healthFilePath, incomingJson);
            Console.WriteLine("[System] Health data smartly merged and pruned.");
        }

        // 2. Debouncing (The Cooldown Timer)
       
        Console.WriteLine("[System] Data saved. Acknowledging phone and booting AI in background...");

        // 3. Fire-and-Forget Background Task
        // Task.Run detaches the AI logic from the web request so the phone doesn't wait
        IConfiguration config = builder.Configuration;
        _ = Task.Run(async () =>
        {
            try
            {
                // Determine timezone once
                TimeZoneInfo istZone;
                try { istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); }
                catch { istZone = TimeZoneInfo.Local; }

                // LOAD ALL DATA INTO MEMORY ONCE
                await LoadHealthStateAsync(finalPayloadToProcess, config);
                await LoadWeeklyHistoryAsync(appDataFolder, istZone);

                // Run the AI (it will instantly read the RAM)
                string workoutMarkdown = await RunFitnessAgentAsync(aiKey, aiEndpoint, aiModel);

                await SaveTodayWorkoutToHistoryAsync(appDataFolder, workoutMarkdown, istZone);

                // Send the Email (it will also instantly read the RAM)
                SendWorkoutEmail(workoutMarkdown, appPassword);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error in Background AI Task] {ex.Message}");
            }
        });

        return Results.Ok("Data received successfully! Your AI Coach is generating the workout.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] {ex.Message}");
        return Results.Problem($"Internal Server Error: {ex.Message}");
    }
});

// Start the web server
app.Run();

// ------------------------------------------------------------------
// AI AGENT & EMAIL LOGIC
// ------------------------------------------------------------------

static async Task<string> RunFitnessAgentAsync(string aiKey, string aiEndpoint, string aiModel)
{
    // Initialize NVIDIA NIM Client
    var openAiClient = new OpenAIClient(
        new ApiKeyCredential(aiKey), 
        new OpenAIClientOptions { Endpoint = new Uri(aiEndpoint) }
    );
    IChatClient chatClient = openAiClient.GetChatClient(aiModel).AsIChatClient();

    // Create Analyst Agent
    AIAgent analystAgent = chatClient.AsAIAgent(
        name: "Physiological_Analyst",
        instructions: @"You are an elite sports scientist. Your client is Piyush, a Software Engineer. 
            Because he works a desk job, he is highly susceptible to sedentary physiological issues. 

            Execute your tools to gather:
            1. Today's intended Workout Schedule.
            2. Current Physical Conditions/Injuries.
            3. Biological Readiness (Sleep, RHR, HRV, Total Burn).
            4. InBody Baseline (Muscle mass, body fat).
            5. Current Week's Workout History.

            ANALYSIS GUIDELINES:
            - HRV (RMSSD): This is the primary indicator of CNS recovery. If HRV is below 40ms or significantly lower than his baseline, flag 'Low Recovery' and recommend reduced volume.
            - Total vs Active Burn: Compare today's total calories to active calories. If the gap is small, he has been sedentary; if the total is high, he may need higher caloric intake for the session.
            - Red Flags: Highlight injuries or high CNS fatigue based on the combination of low HRV and high RHR.

            Output a structured, clinical summary analyzing if Piyush is capable of performing his scheduled workout. List exercises he has already done this week to avoid repetition. Do NOT suggest specific exercises.",
        tools: [
            AIFunctionFactory.Create(HealthDataTools.GetDailyReadiness),
            AIFunctionFactory.Create(HealthDataTools.GetInBodyBaseline),
            AIFunctionFactory.Create(HealthDataTools.GetUserConditions),
            AIFunctionFactory.Create(HealthDataTools.GetWorkoutSchedule),
            AIFunctionFactory.Create(HealthDataTools.GetWeeklyWorkoutHistory)
        ]
    );

    // Create Coach Agent
    AIAgent coachAgent = chatClient.AsAIAgent(
        name: "Strength_Coach",
        instructions: @"You are an elite personal trainer specializing in biomechanics and adaptive programming. You are writing an email directly to your client, Piyush. 
            You will receive a physiological brief from the Analyst. Your task is to design today's exact workout plan based on the Analyst's report. 

            FOLLOW THESE STRICT RULES:
            1. Speak directly to Piyush in an encouraging, professional tone. 
            2. Acknowledge his Intended Schedule, but ADAPT if there is localized strain or pain.
            3. Because Piyush is a Software Engineer, always include 1-2 specific mobility movements in the warm-up to counteract 'desk posture'.
            4. Review the Weekly History. DO NOT repeat main working exercises he has already performed this week.
            5. Review the User Conditions closely. If he states he hated an exercise previously, DO NOT program it. If he reports pain (e.g., piriformis), completely remove exercises that aggravate that area.
            6. Provide a structured routine: Warm-up, Main Working Sets (with sets/reps), and a Cooldown.
            7. Output ONLY clean Markdown so it formats nicely in an email.
        
            Always write in a highly personalized, encouraging tone. End the email by signing off strictly as: 'Stay strong, <br><br>**Apex** <br>*Your AI Biomechanics Specialist*'. Never use generic placeholders."
    );

    // Build the Sequential Pipeline
    var workflow = AgentWorkflowBuilder.BuildSequential([analystAgent, coachAgent]);
    AIAgent workflowAgent = workflow.AsAIAgent(name: "DailyWorkoutEngine");
    AgentSession session = await workflowAgent.CreateSessionAsync();

    Console.WriteLine("[System] Agents are deliberating...");

    // Run the agent and capture the final Coach output
    string finalWorkout = "";
    await foreach (var update in workflowAgent.RunStreamingAsync("Generate today's workout plan.", session))
    {
        // ONLY capture the text if it's coming from the Coach Agent!
        if (update.Text != null && update.AuthorName == "Strength_Coach")
        {
            finalWorkout += update.Text;
        }
    }
    return finalWorkout;
}

static void SendWorkoutEmail(string markdownWorkout, string appPassword)
{
    string fromEmail = "piyushchohan48@gmail.com";
    string toEmail = "piyushchohan48@gmail.com";

    // 1. Convert Markdown to HTML
    var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
    string aiHtmlContent = Markdown.ToHtml(markdownWorkout, pipeline);

    // Headers
    aiHtmlContent = aiHtmlContent.Replace("<h2>", "<h2 style='color: #000000; font-size: 18px; font-weight: 800; border-bottom: 2px solid #e5e7eb; padding-bottom: 8px; margin-top: 36px; margin-bottom: 16px; text-transform: uppercase; letter-spacing: 0.5px;'>");
    aiHtmlContent = aiHtmlContent.Replace("<h3>", "<h3 style='color: #111827; font-size: 16px; font-weight: 700; margin-top: 24px; margin-bottom: 12px; text-transform: uppercase;'>");
    aiHtmlContent = aiHtmlContent.Replace("<strong>", "<strong style='color: #111827; font-weight: 700;'>");
    aiHtmlContent = aiHtmlContent.Replace("<ul>", "<ul style='padding-left: 20px; color: #4b5563; margin-bottom: 28px;'>");
    aiHtmlContent = aiHtmlContent.Replace("<li>", "<li style='margin-bottom: 12px; line-height: 1.6; color: #374151; font-size: 15px;'>");
    aiHtmlContent = aiHtmlContent.Replace("<p>", "<p style='color: #4b5563; line-height: 1.7; font-size: 15px; margin-bottom: 20px;'>");
    aiHtmlContent = aiHtmlContent.Replace("<table>", "<table width='100%' style='border-collapse: collapse; margin-bottom: 30px; font-size: 14px; background-color: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,0.05); border: 1px solid #e5e7eb;'>");
    aiHtmlContent = aiHtmlContent.Replace("<thead>", "<thead style='background-color: #f9fafb; border-bottom: 2px solid #e5e7eb;'>");
    aiHtmlContent = aiHtmlContent.Replace("<th>", "<th style='padding: 12px 15px; text-align: left; color: #111827; font-weight: 700; text-transform: uppercase; font-size: 12px; letter-spacing: 0.5px;'>");
    aiHtmlContent = aiHtmlContent.Replace("<tbody>", "<tbody>");
    aiHtmlContent = aiHtmlContent.Replace("<tr>", "<tr style='border-bottom: 1px solid #f3f4f6;'>");
    aiHtmlContent = aiHtmlContent.Replace("<td>", "<td style='padding: 12px 15px; color: #4b5563; vertical-align: top;'>");

    // 3. Load the HTML Template from the hard drive
    string templatePath = Path.Combine(AppContext.BaseDirectory, @"Templates\\EmailTemplate.html");
    string finalHtmlBody = "";

    try
    {
        finalHtmlBody = File.ReadAllText(templatePath);

        // Replace the AI Markdown
        finalHtmlBody = finalHtmlBody.Replace("{{DATE}}", DateTime.Now.ToString("dddd, MMM d"));
        finalHtmlBody = finalHtmlBody.Replace("{{WORKOUT_CONTENT}}", aiHtmlContent);

        // Replace the Dashboard Metrics directly from RAM
        finalHtmlBody = finalHtmlBody.Replace("{{INBODY_WEIGHT}}", HealthState.InBodyWeight);
        finalHtmlBody = finalHtmlBody.Replace("{{INBODY_BF}}", HealthState.InBodyBf);
        finalHtmlBody = finalHtmlBody.Replace("{{INBODY_SMM}}", HealthState.InBodySmm);
        finalHtmlBody = finalHtmlBody.Replace("{{INBODY_BMR}}", HealthState.InBodyBmr);
        finalHtmlBody = finalHtmlBody.Replace("{{INBODY_WEAK}}", HealthState.InBodyImbalances);
        finalHtmlBody = finalHtmlBody.Replace("{{INBODY_VISCERAL}}", HealthState.InBodyVisceral);
        finalHtmlBody = finalHtmlBody.Replace("{{INBODY_BMI}}", HealthState.InBodyBmi);

        finalHtmlBody = finalHtmlBody.Replace("{{VITALS_SLEEP}}", HealthState.VitalsSleepTotal);
        finalHtmlBody = finalHtmlBody.Replace("{{VITALS_DEEP}}", HealthState.VitalsSleepDeep);
        finalHtmlBody = finalHtmlBody.Replace("{{VITALS_RHR}}", HealthState.VitalsRhr);
        finalHtmlBody = finalHtmlBody.Replace("{{VITALS_STEPS}}", HealthState.VitalsSteps);
        finalHtmlBody = finalHtmlBody.Replace("{{VITALS_DIST}}", HealthState.VitalsDistance);
        finalHtmlBody = finalHtmlBody.Replace("{{VITALS_CALS}}", HealthState.VitalsCalories);
        finalHtmlBody = finalHtmlBody.Replace("{{VITALS_HRV}}", HealthState.VitalsHrv);
        finalHtmlBody = finalHtmlBody.Replace("{{VITALS_TOTAL_CALS}}", HealthState.VitalsTotalCalories);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] Could not load EmailTemplate.html: {ex.Message}");
        return; 
    }

    var smtpClient = new SmtpClient("smtp.gmail.com")
    {
        Port = 587,
        Credentials = new NetworkCredential(fromEmail, appPassword),
        EnableSsl = true,
    };

    var mailMessage = new MailMessage
    {
        From = new MailAddress(fromEmail, "AI Strength Coach"),
        Subject = $"🏋️‍♂️ Piyush's Daily Workout - {DateTime.Now:dddd, MMM d}",
        Body = finalHtmlBody,
        IsBodyHtml = true
    };

    mailMessage.To.Add(toEmail);

    try
    {
        smtpClient.Send(mailMessage);
        Console.WriteLine("[System] Professional HTML Workout successfully emailed to Piyush!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] Failed to send email: {ex.Message}");
    }
    finally
    {
        mailMessage.Dispose();
        smtpClient.Dispose();
    }
}

static async Task LoadHealthStateAsync(HealthExportPayload hc, IConfiguration config)
{
    if (hc != null)
    {
        // 1. Setup India Standard Time Zone
        TimeZoneInfo istZone;
        try { istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); }
        catch { 
            try { istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
            catch { istZone = TimeZoneInfo.Local; } 
        }

        // 2. SMART TARGET DAY (Sync to latest data point)
        var allDates = hc.Sleep.Select(s => s.SessionEndTime)
            .Concat(hc.Steps.Select(s => s.EndTime))
            .Concat(hc.ActiveCalories.Select(c => c.EndTime))
            .Concat(hc.HRV.Select(h => h.Time))
            .ToList();

        DateTime latestDataUtc = allDates.Any() ? allDates.Max() : DateTime.UtcNow;
        DateTime targetDateIst = TimeZoneInfo.ConvertTimeFromUtc(latestDataUtc, istZone).Date;

        // 3. DEFINE WINDOWS (IST)
        DateTime activityStart = targetDateIst;
        DateTime activityEnd = targetDateIst.AddDays(1);
        DateTime sleepStart = targetDateIst.AddHours(-12);
        DateTime sleepEnd = targetDateIst.AddHours(12);

        // Helper Functions
        bool IsTargetActivity(DateTime utcTime) => TimeZoneInfo.ConvertTimeFromUtc(utcTime, istZone) >= activityStart && TimeZoneInfo.ConvertTimeFromUtc(utcTime, istZone) < activityEnd;
        bool IsTargetSleep(DateTime utcTime) => TimeZoneInfo.ConvertTimeFromUtc(utcTime, istZone) >= sleepStart && TimeZoneInfo.ConvertTimeFromUtc(utcTime, istZone) < sleepEnd;

        // --- THE NEW MATH PIPELINE ---

        // 1. Sleep: Mapper for numeric stages (4=Deep, 5/6=REM/Light depending on Health Connect version)
        var targetSleepSessions = hc.Sleep.Where(s => IsTargetSleep(s.SessionEndTime)).ToList();
        int totalSleepSecs = targetSleepSessions.SelectMany(s => s.Stages).Where(st => st.Stage != "1" && st.Stage != "2").Sum(st => st.DurationSeconds);
        HealthState.VitalsSleepTotal = $"{totalSleepSecs / 3600}h {(totalSleepSecs % 3600) / 60}m";

        // 2. Deep Sleep: In mcnaveen's payload, Stage '4' represents Deep Sleep
        int deepSleepSecs = targetSleepSessions.SelectMany(s => s.Stages).Where(st => st.Stage == "4").Sum(st => st.DurationSeconds);
        HealthState.VitalsSleepDeep = $"{deepSleepSecs / 3600}h {(deepSleepSecs % 3600) / 60}m";

        // 3. HRV: Pull the latest RMSSD for the target day
        var targetHrv = hc.HRV.Where(h => IsTargetActivity(h.Time)).OrderByDescending(h => h.Time).FirstOrDefault();
        HealthState.VitalsHrv = targetHrv != null ? Math.Round(targetHrv.Rmssd, 0).ToString() : "--";

        // --- 4. Activity: Steps & Distance (Pick the latest daily total instead of summing) ---
        var latestSteps = hc.Steps.Where(s => IsTargetActivity(s.EndTime)).OrderByDescending(s => s.EndTime).FirstOrDefault();
        HealthState.VitalsSteps = latestSteps != null ? latestSteps.Count.ToString("N0") : "0";

        var latestDist = hc.Distance.Where(d => IsTargetActivity(d.EndTime)).OrderByDescending(d => d.EndTime).FirstOrDefault();
        HealthState.VitalsDistance = latestDist != null ? (latestDist.Meters / 1000.0).ToString("0.00") + " km" : "0.00 km";

        // --- 5. Metabolic: Active Burn vs Total Burn (Pick the latest daily total) ---
        var latestActiveCals = hc.ActiveCalories.Where(c => IsTargetActivity(c.EndTime)).OrderByDescending(c => c.EndTime).FirstOrDefault();
        var latestTotalCals = hc.TotalCalories.Where(c => IsTargetActivity(c.EndTime)).OrderByDescending(c => c.EndTime).FirstOrDefault();

        HealthState.VitalsCalories = latestActiveCals != null ? Math.Round(latestActiveCals.Calories, 0).ToString("N0") + " kcal" : "0 kcal";
        HealthState.VitalsTotalCalories = latestTotalCals != null ? Math.Round(latestTotalCals.Calories, 0).ToString("N0") + " kcal" : "0 kcal";

        // 6. RHR
        var targetRhr = hc.RestingHeartRate.Where(r => IsTargetActivity(r.Time)).OrderByDescending(r => r.Time).FirstOrDefault();
        HealthState.VitalsRhr = targetRhr != null ? targetRhr.Bpm.ToString() : "--";

        // 7. Construct Final AI Readiness Brief (Now with HRV and Total Metabolic Load)
        HealthState.ReadinessBrief = $"[TARGET DAY: {targetDateIst:MMM dd}] Sleep: {HealthState.VitalsSleepTotal} (Deep: {HealthState.VitalsSleepDeep}). RHR: {HealthState.VitalsRhr} bpm. HRV: {HealthState.VitalsHrv}. Steps: {HealthState.VitalsSteps}. Active Burn: {HealthState.VitalsCalories} (Total: {Math.Round(latestTotalCals.Calories, 0)} kcal).";
    }

    // --- GIST DATA (INBODY & CONDITIONS) ---
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    try
    {
        string inBodyUrl = config["ExternalData:InBodyUrl"];
        string conditionsUrl = config["ExternalData:ConditionsUrl"];

        HealthState.ConditionsBrief = await client.GetStringAsync(conditionsUrl);
        var scan = JsonSerializer.Deserialize<InBodyExport>(await client.GetStringAsync(inBodyUrl));
        
        if (scan != null)
        {
            HealthState.InBodyWeight = scan.Core.WeightKg.ToString("0.0");
            HealthState.InBodyBf = scan.Core.Pbf.ToString("0.0");
            HealthState.InBodySmm = scan.Core.SmmKg.ToString("0.0");
            HealthState.InBodyBmr = scan.Metabolism.Bmr.ToString();
            HealthState.InBodyVisceral = scan.Metabolism.VisceralFatLevel.ToString();
            HealthState.InBodyBmi = scan.Core.Bmi.ToString();

            var weak = new List<string>();
            if (scan.LeanBalance.LeftLeg == "Under" || scan.LeanBalance.RightLeg == "Under") weak.Add("Legs");
            if (scan.LeanBalance.LeftArm == "Under" || scan.LeanBalance.RightArm == "Under") weak.Add("Arms");
            if (scan.LeanBalance.Trunk == "Under") weak.Add("Core");
            HealthState.InBodyImbalances = weak.Any() ? string.Join(", ", weak) : "Balanced";

            HealthState.InBodyBrief = $"Weight: {HealthState.InBodyWeight}kg. Body Fat: {HealthState.InBodyBf}%. BMR: {HealthState.InBodyBmr} kcal. SMM: {HealthState.InBodySmm}kg VisceralFat: {HealthState.InBodyVisceral} BMI: {HealthState.InBodyBmi} Focus: {HealthState.InBodyImbalances}.";
        }
    }
    catch { /* Defaults remain if HTTP fails */ }
}

static async Task LoadWeeklyHistoryAsync(string appDataFolder, TimeZoneInfo istZone)
{
    string historyPath = Path.Combine(appDataFolder, "weekly_history.json");
    DateTime nowIst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);

    // Calculate the most recent Sunday
    int diff = (7 + (nowIst.DayOfWeek - DayOfWeek.Sunday)) % 7;
    DateTime currentWeekSunday = nowIst.AddDays(-1 * diff).Date;

    WeeklyWorkoutHistory history = new() { WeekStartDate = currentWeekSunday };

    if (File.Exists(historyPath))
    {
        try
        {
            var existingHistory = JsonSerializer.Deserialize<WeeklyWorkoutHistory>(await File.ReadAllTextAsync(historyPath));
            // If the saved history belongs to the current week, keep it. Otherwise, it resets.
            if (existingHistory != null && existingHistory.WeekStartDate == currentWeekSunday)
            {
                history = existingHistory;
            }
        }
        catch { /* Corrupted file, will overwrite */ }
    }

    // Build the AI Brief
    if (history.PastWorkouts.Any())
    {
        var summaries = history.PastWorkouts.Select(kvp => $"{kvp.Key}: {kvp.Value}");
        HealthState.WeeklyHistoryBrief = "This week's completed workouts:\n" + string.Join("\n", summaries);
    }
    else
    {
        HealthState.WeeklyHistoryBrief = "It's the start of a new week. No workouts completed yet.";
    }

    // Save the initialized/reset state back to disk
    await File.WriteAllTextAsync(historyPath, JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true }));
}

static async Task SaveTodayWorkoutToHistoryAsync(string appDataFolder, string generatedMarkdown, TimeZoneInfo istZone)
{
    string historyPath = Path.Combine(appDataFolder, "weekly_history.json");
    if (!File.Exists(historyPath)) return;

    DateTime nowIst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
    string todayString = nowIst.DayOfWeek.ToString();

    var history = JsonSerializer.Deserialize<WeeklyWorkoutHistory>(await File.ReadAllTextAsync(historyPath));
    if (history != null)
    {
        // We only save the first 300 characters of the markdown to keep the AI's context window light and fast.
        // The AI only needs to know the exercises, not the intro/outro fluff.
        int maxLength = generatedMarkdown.Length > 300 ? 300 : generatedMarkdown.Length;
        history.PastWorkouts[todayString] = generatedMarkdown.Substring(0, maxLength) + "...";

        await File.WriteAllTextAsync(historyPath, JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true }));
    }
}