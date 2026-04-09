using ForexZoneAnalyzer.McpServer.Services;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

// Entra ID (Azure AD) JWT Bearer authentication
builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration, "AzureAd");
builder.Services.AddAuthorization();

// OANDA connection service (singleton — caches the API connection)
builder.Services.AddSingleton<IOandaConnectionService, OandaConnectionService>();

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

// Health check endpoint (unauthenticated)
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

// MCP endpoint (authenticated)
app.MapMcp().RequireAuthorization();

app.Run();
