using Azure.Data.Tables;
using Azure.Identity;
using ForexZoneAnalyzer.McpServer.Services;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

// Entra ID (Azure AD) JWT Bearer authentication — only in non-Development environments
var requireAuth = !builder.Environment.IsDevelopment();

if (requireAuth)
{
    builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration, "AzureAd");
}
else
{
    builder.Services.AddAuthentication();
}
builder.Services.AddAuthorization();

// OANDA connection service (singleton — caches the API connection)
builder.Services.AddSingleton<IOandaConnectionService, OandaConnectionService>();

// Azure Table Storage client for stored zones
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connectionString = config["Storage:ConnectionString"];
    var tableName = config["Storage:TableName"] ?? "zones";

    var clientOptions = new TableClientOptions();
    clientOptions.Retry.MaxRetries = 5;
    clientOptions.Retry.Mode = Azure.Core.RetryMode.Exponential;
    clientOptions.Retry.Delay = TimeSpan.FromSeconds(1);
    clientOptions.Retry.MaxDelay = TimeSpan.FromSeconds(30);

    TableClient client;
    if (!string.IsNullOrEmpty(connectionString))
    {
        client = new TableClient(connectionString, tableName, clientOptions);
    }
    else
    {
        var accountName = config["Storage:AccountName"];
        var endpoint = new Uri($"https://{accountName}.table.core.windows.net");
        client = new TableClient(endpoint, tableName, new DefaultAzureCredential(), clientOptions);
    }
    return client;
});

// Azure Table Storage clients for pair configs and status
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connectionString = config["Storage:ConnectionString"];

    var clientOptions = new TableClientOptions();
    clientOptions.Retry.MaxRetries = 5;
    clientOptions.Retry.Mode = Azure.Core.RetryMode.Exponential;
    clientOptions.Retry.Delay = TimeSpan.FromSeconds(1);
    clientOptions.Retry.MaxDelay = TimeSpan.FromSeconds(30);

    TableClient configClient, statusClient;
    if (!string.IsNullOrEmpty(connectionString))
    {
        configClient = new TableClient(connectionString, "pairconfigs", clientOptions);
        statusClient = new TableClient(connectionString, "pairstatus", clientOptions);
    }
    else
    {
        var accountName = config["Storage:AccountName"];
        var endpoint = new Uri($"https://{accountName}.table.core.windows.net");
        var credential = new DefaultAzureCredential();
        configClient = new TableClient(endpoint, "pairconfigs", credential, clientOptions);
        statusClient = new TableClient(endpoint, "pairstatus", credential, clientOptions);
    }

    configClient.CreateIfNotExists();
    statusClient.CreateIfNotExists();

    return new ForexZoneAnalyzer.McpServer.Services.ConfigTableClient(configClient, statusClient);
});

// Candle cache reader (reads worker's cached candles from Table Storage)
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connectionString = config["Storage:ConnectionString"];

    var clientOptions = new TableClientOptions();
    clientOptions.Retry.MaxRetries = 5;
    clientOptions.Retry.Mode = Azure.Core.RetryMode.Exponential;
    clientOptions.Retry.Delay = TimeSpan.FromSeconds(1);
    clientOptions.Retry.MaxDelay = TimeSpan.FromSeconds(30);

    TableClient candleClient, metaClient;
    if (!string.IsNullOrEmpty(connectionString))
    {
        candleClient = new TableClient(connectionString, "candlecache", clientOptions);
        metaClient = new TableClient(connectionString, "candlecachemeta", clientOptions);
    }
    else
    {
        var accountName = config["Storage:AccountName"];
        var endpoint = new Uri($"https://{accountName}.table.core.windows.net");
        var credential = new DefaultAzureCredential();
        candleClient = new TableClient(endpoint, "candlecache", credential, clientOptions);
        metaClient = new TableClient(endpoint, "candlecachemeta", credential, clientOptions);
    }

    return new ForexZoneAnalyzer.McpServer.Services.CandleCacheReader(candleClient, metaClient);
});

// Azure Storage clients for strategy runs
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connectionString = config["Storage:ConnectionString"];

    var clientOptions = new TableClientOptions();
    clientOptions.Retry.MaxRetries = 5;
    clientOptions.Retry.Mode = Azure.Core.RetryMode.Exponential;
    clientOptions.Retry.Delay = TimeSpan.FromSeconds(1);
    clientOptions.Retry.MaxDelay = TimeSpan.FromSeconds(30);

    TableClient runsClient;
    Azure.Storage.Queues.QueueClient queueClient;

    if (!string.IsNullOrEmpty(connectionString))
    {
        runsClient = new TableClient(connectionString, "strategyruns", clientOptions);
        queueClient = new Azure.Storage.Queues.QueueClient(connectionString, "strategy-jobs");
    }
    else
    {
        var accountName = config["Storage:AccountName"];
        var endpoint = new Uri($"https://{accountName}.table.core.windows.net");
        var credential = new DefaultAzureCredential();
        runsClient = new TableClient(endpoint, "strategyruns", credential, clientOptions);
        queueClient = new Azure.Storage.Queues.QueueClient(
            new Uri($"https://{accountName}.queue.core.windows.net/strategy-jobs"),
            credential);
    }

    runsClient.CreateIfNotExists();
    queueClient.CreateIfNotExists();

    return new ForexZoneAnalyzer.McpServer.Services.StrategyTableClient(runsClient, queueClient);
});

// MCP server with HTTP transport + auto-discovered tools
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "forex-zone-analyzer",
            Version = "1.0.0"
        };
    })
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Health check endpoint (always unauthenticated)
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

// MCP endpoint — authenticated in production, open in development
var mcpEndpoint = app.MapMcp("/mcp");
if (requireAuth)
{
    mcpEndpoint.RequireAuthorization();
}

app.Run();
