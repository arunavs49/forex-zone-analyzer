using Microsoft.Extensions.Hosting;
using ForexZoneAnalyzer.Worker.Configuration;
using ForexZoneAnalyzer.Worker.Services;
using Microsoft.Azure.NotificationHubs;

var builder = Host.CreateApplicationBuilder(args);

// Bind configuration sections
builder.Services.Configure<MonitorSettings>(builder.Configuration.GetSection("MonitorSettings"));
builder.Services.Configure<ZoneAnalyzer.PatternAnalysis.ZoneConfiguration>(builder.Configuration.GetSection("ZoneConfiguration"));
builder.Services.Configure<ZoneAnalyzer.PatternAnalysis.TrendConfiguration>(builder.Configuration.GetSection("TrendConfiguration"));

// OANDA connection service
builder.Services.AddSingleton<OandaConnectionService>();

// Candle cache
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<CandleCacheService>();

// Zone store: Table Storage in production, in-memory for development
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IZoneStore, InMemoryZoneStore>();
}
else
{
    builder.Services.AddSingleton<IZoneStore, TableStorageZoneStore>();
}

// Notification services: build a list and wrap in CompositeNotificationService
var notificationServices = new List<Func<IServiceProvider, INotificationService>>();

var acsConnectionString = builder.Configuration["Notification:AcsConnectionString"];
if (!string.IsNullOrEmpty(acsConnectionString))
{
    builder.Services.AddSingleton<EmailNotificationService>();
    notificationServices.Add(sp => sp.GetRequiredService<EmailNotificationService>());
}

var nhConnectionString = builder.Configuration["Notification:NotificationHubConnectionString"];
var nhName = builder.Configuration["Notification:NotificationHubName"];
if (!string.IsNullOrEmpty(nhConnectionString) && !string.IsNullOrEmpty(nhName))
{
    builder.Services.AddSingleton<INotificationHubClient>(
        NotificationHubClient.CreateClientFromConnectionString(nhConnectionString, nhName));
    builder.Services.AddSingleton<PushNotificationService>();
    notificationServices.Add(sp => sp.GetRequiredService<PushNotificationService>());
}

if (notificationServices.Count > 0)
{
    builder.Services.AddSingleton<INotificationService>(sp =>
    {
        var services = notificationServices.Select(factory => factory(sp));
        var logger = sp.GetRequiredService<ILogger<CompositeNotificationService>>();
        return new CompositeNotificationService(services, logger);
    });
}
else
{
    builder.Services.AddSingleton<INotificationService, ConsoleNotificationService>();
}

// Background worker
builder.Services.AddHostedService<ZoneMonitorService>();

var host = builder.Build();
host.Run();
