using Microsoft.Extensions.Hosting;
using ForexZoneAnalyzer.Worker.Configuration;
using ForexZoneAnalyzer.Worker.Services;

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

// Zone store and config store: Table Storage in production, in-memory for development
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IZoneStore, InMemoryZoneStore>();
    builder.Services.AddSingleton<IConfigStore, InMemoryConfigStore>();
}
else
{
    builder.Services.AddSingleton<IZoneStore, TableStorageZoneStore>();
    builder.Services.AddSingleton<IConfigStore, TableStorageConfigStore>();
}

// Notification service: email if configured, otherwise console
var acsConnectionString = builder.Configuration["Notification:AcsConnectionString"];
if (!string.IsNullOrEmpty(acsConnectionString))
{
    builder.Services.AddSingleton<INotificationService, EmailNotificationService>();
}
else
{
    builder.Services.AddSingleton<INotificationService, ConsoleNotificationService>();
}

// Background worker
builder.Services.AddHostedService<ZoneMonitorService>();

var host = builder.Build();

// Seed config store if empty (EUR_USD H1 with defaults)
using (var scope = host.Services.CreateScope())
{
    var configStore = scope.ServiceProvider.GetRequiredService<IConfigStore>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    if (configStore is TableStorageConfigStore tableStore && await tableStore.IsEmptyAsync())
    {
        logger.LogInformation("Config store is empty — seeding EUR_USD H1 with default settings");
        await configStore.UpsertConfigAsync(new PairConfig
        {
            Instrument = "EUR_USD",
            ZoneGranularity = "H1",
            TrendGranularity = "H8",
            Enabled = true,
            EmailEnabled = true,
            MinBaseLength = 1,
            MaxBaseLength = 6,
            MinLegInToBaseRangeRatio = 1.0,
            MinLegOutToBaseRangeRatio = 1.0,
            SwingLookback = 3,
            TrendCandleCount = 60,
            MinSwingPoints = 2
        });
    }
    else if (configStore is InMemoryConfigStore)
    {
        // In development, always seed for local testing
        var existing = await configStore.GetConfigAsync("EUR_USD", "H1");
        if (existing == null)
        {
            logger.LogInformation("Dev mode — seeding EUR_USD H1 config");
            await configStore.UpsertConfigAsync(new PairConfig
            {
                Instrument = "EUR_USD",
                ZoneGranularity = "H1",
                TrendGranularity = "H8",
                Enabled = true,
                EmailEnabled = false,
                MinBaseLength = 1,
                MaxBaseLength = 6,
                MinLegInToBaseRangeRatio = 1.0,
                MinLegOutToBaseRangeRatio = 1.0,
                SwingLookback = 3,
                TrendCandleCount = 60,
                MinSwingPoints = 2
            });
        }
    }
}

host.Run();
