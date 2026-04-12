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
