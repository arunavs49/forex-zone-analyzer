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
var mcpEndpoint = app.MapMcp();
if (requireAuth)
{
    mcpEndpoint.RequireAuthorization();
}

app.Run();
