using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using GeriRemenyi.Oanda.V20.Client.Model;
using GeriRemenyi.Oanda.V20.Sdk;
using GeriRemenyi.Oanda.V20.Sdk.Common.Types;

namespace ForexZoneAnalyzer.Worker.Services;

public class OandaConnectionService
{
    private readonly ILogger<OandaConnectionService> _logger;
    private readonly IConfiguration _configuration;
    private IOandaApiConnection? _connection;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public OandaConnectionService(ILogger<OandaConnectionService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<IOandaApiConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection != null)
            return _connection;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_connection != null)
                return _connection;

            var token = await GetOandaTokenAsync(cancellationToken);
            var connectionType = Enum.Parse<OandaConnectionType>(
                _configuration["Oanda:ConnectionType"] ?? "FxPractice");

            _logger.LogInformation("Creating OANDA API connection ({ConnectionType})", connectionType);
            _connection = new OandaApiConnection(connectionType, token, DateTimeFormat.RFC3339);
            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<string> GetOandaTokenAsync(CancellationToken cancellationToken)
    {
        var keyVaultUri = _configuration["KeyVault:Uri"];
        var secretName = _configuration["KeyVault:OandaTokenSecretName"] ?? "oanda-api-token";

        if (!string.IsNullOrEmpty(keyVaultUri))
        {
            _logger.LogInformation("Retrieving OANDA token from Key Vault: {KeyVaultUri}", keyVaultUri);
            var client = new SecretClient(new Uri(keyVaultUri), new DefaultAzureCredential());
            var secret = await client.GetSecretAsync(secretName, cancellationToken: cancellationToken);
            return secret.Value.Value;
        }

        var localToken = _configuration["Oanda:ApiToken"];
        if (!string.IsNullOrEmpty(localToken))
        {
            _logger.LogWarning("Using local OANDA token from configuration (not recommended for production)");
            return localToken;
        }

        throw new InvalidOperationException(
            "No OANDA API token configured. Set KeyVault:Uri or Oanda:ApiToken for local development.");
    }
}
