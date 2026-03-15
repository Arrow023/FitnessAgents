using FitnessAgentsWeb.Core.Configuration;
using FitnessAgentsWeb.Core.Factories;
using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Core.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddSingleton<InBodyOcrService>();
builder.Services.AddSingleton<DieticianAgentService>();
builder.Services.AddSingleton<IAiOrchestratorService, AiOrchestratorService>();

// Register Background Service
builder.Services.AddHostedService<WorkoutEmailSchedulerService>();

var app = builder.Build();

// Enable routing and static files for the UI Dashboard
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();