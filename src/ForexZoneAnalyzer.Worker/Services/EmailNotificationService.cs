using Azure.Communication.Email;
using GeriRemenyi.Oanda.V20.Client.Model;

namespace ForexZoneAnalyzer.Worker.Services;

public class EmailNotificationService : INotificationService
{
    private readonly EmailClient _emailClient;
    private readonly string _toEmail;
    private readonly string _fromEmail;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(IConfiguration configuration, ILogger<EmailNotificationService> logger)
    {
        _logger = logger;
        var connectionString = configuration["Notification:AcsConnectionString"]
            ?? throw new InvalidOperationException("Notification:AcsConnectionString is required for email notifications.");
        _toEmail = configuration["Notification:EmailTo"]
            ?? throw new InvalidOperationException("Notification:EmailTo is required for email notifications.");
        _fromEmail = configuration["Notification:EmailFrom"]
            ?? "DoNotReply@forex-zone-analyzer.com";
        _emailClient = new EmailClient(connectionString);
    }

    public async Task SendZoneAlertAsync(string instrument, string granularity, Zone zone, string trend, CancellationToken cancellationToken)
    {
        var subject = $"[Zone Alert] {instrument} {granularity} - New {zone.Type} Zone";

        var htmlContent = $"""
            <h2>New {zone.Type} Zone Detected</h2>
            <table style="border-collapse: collapse; font-family: monospace;">
                <tr><td><b>Instrument</b></td><td>{instrument}</td></tr>
                <tr><td><b>Timeframe</b></td><td>{granularity}</td></tr>
                <tr><td><b>Type</b></td><td>{zone.Type}</td></tr>
                <tr><td><b>Freshness</b></td><td>{zone.Freshness}</td></tr>
                <tr><td><b>Sub-Zone</b></td><td>{(zone.SubZone ? "Yes" : "No")}</td></tr>
                <tr><td><b>Base Range</b></td><td>{zone.BaseRangeLow:F5} – {zone.BaseRangeHigh:F5}</td></tr>
                <tr><td><b>Base Candles</b></td><td>{zone.BaseCandleCount}</td></tr>
                <tr><td><b>Trend ({granularity})</b></td><td>{trend}</td></tr>
                <tr><td><b>Zone Start</b></td><td>{zone.StartTime:O}</td></tr>
                <tr><td><b>Zone End</b></td><td>{zone.EndTime:O}</td></tr>
            </table>
            """;

        var plainText = ConsoleNotificationService.FormatAlert(instrument, granularity, zone, trend);

        var emailMessage = new EmailMessage(
            senderAddress: _fromEmail,
            recipientAddress: _toEmail,
            content: new EmailContent(subject)
            {
                Html = htmlContent,
                PlainText = plainText
            });

        try
        {
            await SendWithRetryAsync(emailMessage, cancellationToken);
            _logger.LogInformation("Email sent for {Instrument} {Type} zone", instrument, zone.Type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email notification for {Instrument} {Type} zone after retries", instrument, zone.Type);
        }
    }

    private async Task SendWithRetryAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        const int maxRetries = 3;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                await _emailClient.SendAsync(Azure.WaitUntil.Completed, message, cancellationToken);
                return;
            }
            catch (Exception) when (attempt < maxRetries && !cancellationToken.IsCancellationRequested)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt + 1)); // 2s, 4s, 8s
                _logger.LogWarning("Email send attempt {Attempt} failed, retrying in {Delay}s", attempt + 1, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
