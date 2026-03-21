using Serilog;
using Microsoft.AspNetCore.Authentication.Cookies;
using FitnessAgentsWeb.Core.Factories;
using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Core.Services;
using FitnessAgentsWeb.Core.Configuration;
using FitnessAgentsWeb.Models;
using System.Threading.Channels;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
var logsFolder = Path.Combine(builder.Environment.ContentRootPath, "Logs");
if (!Directory.Exists(logsFolder)) Directory.CreateDirectory(logsFolder);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .Enrich.With(new FitnessAgentsWeb.Core.Logging.TimezoneTimestampEnricher())
    .WriteTo.Console(outputTemplate: "[{AppTimestamp}] {Level:u3} {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        Path.Combine(logsFolder, "fitness-assist-.log"), 
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "[{AppTimestamp}] {Level:u3} {Message:lj}{NewLine}{Exception}"));

// Add MVC services for upcoming Dashboard
builder.Services.AddControllersWithViews();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });

// Register Factories
builder.Services.AddSingleton<ConfigurationProviderFactory>();
builder.Services.AddSingleton<IAppConfigurationManager, FirebaseSettingsProvider>();
builder.Services.AddSingleton<IAppConfigurationProvider>(sp => sp.GetRequiredService<IAppConfigurationManager>());
builder.Services.AddSingleton<StorageRepositoryFactory>();

// Expose IStorageRepository directly for Controllers
builder.Services.AddScoped<IStorageRepository>(sp => 
{
    var factory = sp.GetRequiredService<StorageRepositoryFactory>();
    return factory.Create();
});
builder.Services.AddScoped<IHealthDataProcessor>(sp => 
{
    var factory = sp.GetRequiredService<HealthDataProcessorFactory>();
    return factory.Create();
});
builder.Services.AddSingleton<HealthDataProcessorFactory>();
builder.Services.AddSingleton<AiAgentServiceFactory>();
builder.Services.AddSingleton<NotificationServiceFactory>();
builder.Services.AddScoped<INotificationService>(sp => 
{
    var factory = sp.GetRequiredService<NotificationServiceFactory>();
    return factory.Create();
});
builder.Services.AddSingleton<InBodyOcrService>();
builder.Services.AddSingleton<IAiOrchestratorService, AiOrchestratorService>();

// Vector store and embedding services (optional — gracefully degrade if Qdrant is unreachable)
builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();
builder.Services.AddSingleton<IPlanVectorStore, QdrantPlanVectorStore>();

// Async plan generation infrastructure
builder.Services.AddSingleton<IPlanGenerationTracker, PlanGenerationTracker>();
builder.Services.AddSingleton(Channel.CreateBounded<PlanGenerationJob>(new BoundedChannelOptions(10)
{
    FullMode = BoundedChannelFullMode.Wait
}));
builder.Services.AddHostedService<PlanGenerationBackgroundService>();

// Register Background Service
builder.Services.AddHostedService<WorkoutEmailSchedulerService>();

var app = builder.Build();

// Initialize vector store collection on startup (non-blocking)
try
{
    var vectorStore = app.Services.GetService<IPlanVectorStore>();
    if (vectorStore is not null)
    {
        _ = Task.Run(async () =>
        {
            try { await vectorStore.InitializeAsync(); }
            catch (Exception ex) { app.Logger.LogWarning(ex, "Qdrant vector store initialization failed — AI memory features will be unavailable"); }
        });
    }
}
catch { /* Vector store not configured — gracefully degrade */ }

// Enable routing and static files for the UI Dashboard
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Overview}/{action=Index}/{id?}");

app.Run();