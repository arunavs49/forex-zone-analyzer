using Microsoft.Extensions.Hosting;
using ForexZoneAnalyzer.Worker.Configuration;
using ForexZoneAnalyzer.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

// Bind configuration sections
builder.Services.Configure<MonitorSettings>(builder.Configuration.GetSection("MonitorSettings"));
builder.Services.Configure<ZoneAnalyzer.PatternAnalysis.ZoneConfiguration>(builder.Configuration.GetSection("ZoneConfiguration"));

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

// Notification: console in development, email in production
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<INotificationService, ConsoleNotificationService>();
}
else
{
    builder.Services.AddSingleton<INotificationService, EmailNotificationService>();
}

// Background worker
builder.Services.AddHostedService<ZoneMonitorService>();

var host = builder.Build();
host.Run();
