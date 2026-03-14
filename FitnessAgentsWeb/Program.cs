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

        // A. Read the incoming Health Connect JSON
        using var reader = new StreamReader(context.Request.Body);
        var incomingJson = await reader.ReadToEndAsync();
        string healthFilePath = Path.Combine(appDataFolder, "health_connect_today.json");

        var newPayload = JsonSerializer.Deserialize<HealthExportPayload>(incomingJson);

        if (newPayload != null)
        {
            if (File.Exists(healthFilePath))
            {
                var existingJson = await File.ReadAllTextAsync(healthFilePath);
                var existingPayload = JsonSerializer.Deserialize<HealthExportPayload>(existingJson);

                if (existingPayload != null)
                {
                    DateTime cutoff = DateTime.UtcNow.AddDays(-7);

                    // 1 & 2. Create a BRAND NEW object, merging and pruning inside the initializer
                    var mergedPayload = new HealthExportPayload
                    {
                        Sleep = existingPayload.Sleep.Concat(newPayload.Sleep)
                            .GroupBy(s => s.SessionEndTime).Select(g => g.First())
                            .Where(s => s.SessionEndTime >= cutoff).ToList(),

                        RestingHeartRate = existingPayload.RestingHeartRate.Concat(newPayload.RestingHeartRate)
                            .GroupBy(r => r.Time).Select(g => g.First())
                            .Where(r => r.Time >= cutoff).ToList(),

                        Steps = existingPayload.Steps.Concat(newPayload.Steps)
                            .GroupBy(s => s.EndTime).Select(g => g.First())
                            .Where(s => s.EndTime >= cutoff).ToList(),

                        Exercise = existingPayload.Exercise.Concat(newPayload.Exercise)
                            .GroupBy(e => e.StartTime).Select(g => g.First())
                            .Where(e => e.StartTime >= cutoff).ToList()
                    };

                    // 3. Serialize the newly merged object back to JSON text
                    incomingJson = JsonSerializer.Serialize(mergedPayload, new JsonSerializerOptions { WriteIndented = true });
                }
            }

            // 4. Save the fully merged data back to the disk
            await File.WriteAllTextAsync(healthFilePath, incomingJson);
            Console.WriteLine("[System] Health data smartly merged and pruned.");
        }

        // 2. Debouncing (The Cooldown Timer)
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
        Console.WriteLine("[System] Data saved. Acknowledging phone and booting AI in background...");

        // 3. Fire-and-Forget Background Task
        // Task.Run detaches the AI logic from the web request so the phone doesn't wait
        _ = Task.Run(async () =>
        {
            try
            {
                // 1. Generate the AI Workout Plan
                string workoutMarkdown = await RunFitnessAgentAsync(appDataFolder, aiKey, aiEndpoint, aiModel);

                // 2. Fetch the raw numbers for the UI Dashboard
                var metrics = await GetDashboardMetricsAsync(appDataFolder);

                // 3. Send it all to the email compiler
                SendWorkoutEmail(workoutMarkdown, appPassword, metrics, appDataFolder);
            }
            catch (ClientResultException apiEx)
            {
                Console.WriteLine($"\n[NVIDIA API ERROR] Status: {apiEx.Status}");
                Console.WriteLine($"[NVIDIA API ERROR] Details: {apiEx.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[SYSTEM ERROR] {ex.GetType().Name}: {ex.Message}");
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

static async Task<string> RunFitnessAgentAsync(string appDataFolder, string aiKey, string aiEndpoint, string aiModel)
{
    // Initialize NVIDIA NIM Client
    var openAiClient = new OpenAIClient(
        new ApiKeyCredential(aiKey), 
        new OpenAIClientOptions { Endpoint = new Uri(aiEndpoint) }
    );
    IChatClient chatClient = openAiClient.GetChatClient(aiModel).AsIChatClient();

    // Tell the tools where to find the files on the server
    HealthDataTools.AppDataPath = appDataFolder;

    // Create Analyst Agent
    AIAgent analystAgent = chatClient.AsAIAgent(
        name: "Physiological_Analyst",
        instructions: @"You are an elite sports scientist. Your client is Piyush, a Software Engineer. 
            Because he works a desk job, he is highly susceptible to sedentary physiological issues (tight hip flexors, weak glutes, rounded shoulders). 

            Execute your tools to gather:
            1. Today's intended Workout Schedule.
            2. Current Physical Conditions/Injuries.
            3. Biological Readiness (Sleep, RHR, Strain).
            4. InBody Baseline (Muscle mass, body fat).

            Output a structured, clinical summary analyzing if Piyush is capable of performing his scheduled workout. Highlight any critical red flags (like injuries or high CNS fatigue) that the Coach must work around. Do NOT suggest specific exercises.",
        tools: [
            AIFunctionFactory.Create(HealthDataTools.GetDailyReadiness),
        AIFunctionFactory.Create(HealthDataTools.GetInBodyBaseline),
        AIFunctionFactory.Create(HealthDataTools.GetUserConditions),
        AIFunctionFactory.Create(HealthDataTools.GetWorkoutSchedule)
        ]
    );

    // Create Coach Agent
    AIAgent coachAgent = chatClient.AsAIAgent(
        name: "Strength_Coach",
        instructions: @"You are a world-class personal trainer specializing in biomechanics and adaptive programming. You are writing an email directly to your client, Piyush. 
            You will receive a physiological brief from the Analyst. Your task is to design today's exact workout plan based on the Analyst's report. 

            FOLLOW THESE STRICT RULES:
            1. Speak directly to Piyush in an encouraging, professional tone. 
            2. Acknowledge his Intended Schedule, but ADAPT if there is localized strain or pain.
            3. Because Piyush is a Software Engineer, always include 1-2 specific mobility movements in the warm-up to counteract 'desk posture' (e.g., thoracic extensions, hip flexor stretches).
            4. If an injury/pain (e.g., piriformis pain) is reported, completely remove exercises that aggravate that area. Pivot to isolation movements, mobility, or a different muscle group if needed.
            5. If CNS fatigue is high, reduce total sets and reps by 20% and avoid heavy compound max-outs.
            6. Provide a structured routine: Warm-up, Main Working Sets (with sets/reps), and a Cooldown.
            7. Output ONLY clean Markdown so it formats nicely in an email."
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

    Console.WriteLine("********** Final Workout Summary ***********");
    Console.WriteLine(finalWorkout);

    return finalWorkout;
}

static void SendWorkoutEmail(string markdownWorkout, string appPassword, (string W, string Bf, string Smm, string Slp, string Rhr, string Stp) metrics, string appDataFolder)
{
    string fromEmail = "piyushchohan48@gmail.com";
    string toEmail = "piyushchohan48@gmail.com";

    // 1. Convert Markdown to HTML
    var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
    string aiHtmlContent = Markdown.ToHtml(markdownWorkout, pipeline);

    // 2. Load the HTML Template from the hard drive
    string templatePath = Path.Combine(AppContext.BaseDirectory, @"Templates\\EmailTemplate.html");
    string finalHtmlBody = "";

    try
    {
        finalHtmlBody = File.ReadAllText(templatePath);

        // Replace the AI Markdown
        finalHtmlBody = finalHtmlBody.Replace("{{DATE}}", DateTime.Now.ToString("dddd, MMM d"));
        finalHtmlBody = finalHtmlBody.Replace("{{WORKOUT_CONTENT}}", aiHtmlContent);

        // Replace the Dashboard Metrics
        finalHtmlBody = finalHtmlBody.Replace("{{INBODY_WEIGHT}}", metrics.W);
        finalHtmlBody = finalHtmlBody.Replace("{{INBODY_BF}}", metrics.Bf);
        finalHtmlBody = finalHtmlBody.Replace("{{INBODY_SMM}}", metrics.Smm);
        finalHtmlBody = finalHtmlBody.Replace("{{VITALS_SLEEP}}", metrics.Slp);
        finalHtmlBody = finalHtmlBody.Replace("{{VITALS_RHR}}", metrics.Rhr);
        finalHtmlBody = finalHtmlBody.Replace("{{VITALS_STEPS}}", metrics.Stp);
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

static async Task<(string W, string Bf, string Smm, string Slp, string Rhr, string Stp)> GetDashboardMetricsAsync(string appDataFolder)
{
    // Default placeholders if data is missing
    string w = "--", bf = "--", smm = "--", slp = "--", rhr = "--", stp = "--";

    // 1. Fetch InBody Data (From your GitHub Gist)
    try
    {
        using var client = new HttpClient();
        // ⚠️ PASTE YOUR RAW INBODY GIST URL HERE:
        string gistUrl = "";
        var scan = JsonSerializer.Deserialize<InBodyExport>(await client.GetStringAsync(gistUrl));
        if (scan != null)
        {
            w = scan.Core.WeightKg.ToString("0.0");
            bf = scan.Core.Pbf.ToString("0.0");
            smm = scan.Core.SmmKg.ToString("0.0");
        }
    }
    catch { }

    // 2. Fetch Health Connect Vitals (From local merged file)
    try
    {
        string hcPath = Path.Combine(appDataFolder, "health_connect_today.json");
        if (File.Exists(hcPath))
        {
            var hc = JsonSerializer.Deserialize<HealthExportPayload>(await File.ReadAllTextAsync(hcPath));
            if (hc != null)
            {
                // Get most recent sleep duration
                if (hc.Sleep.Any())
                {
                    int sec = hc.Sleep.OrderByDescending(s => s.SessionEndTime).First().DurationSeconds;
                    slp = $"{sec / 3600}h {(sec % 3600) / 60}m";
                }
                // Get most recent Resting HR
                if (hc.RestingHeartRate.Any())
                {
                    rhr = hc.RestingHeartRate.OrderByDescending(r => r.Time).First().Bpm.ToString();
                }
                // Sum steps from the last 24 hours
                if (hc.Steps.Any())
                {
                    DateTime yesterday = DateTime.UtcNow.AddDays(-1);
                    stp = hc.Steps.Where(s => s.EndTime >= yesterday).Sum(s => s.Count).ToString("N0"); // "N0" adds commas to thousands!
                }
            }
        }
    }
    catch { }

    return (w, bf, smm, slp, rhr, stp);
}