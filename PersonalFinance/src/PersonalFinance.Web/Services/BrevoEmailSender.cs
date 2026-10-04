using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PersonalFinance.Web.Services;

public class BrevoEmailSender : IEmailSender
{
    private readonly BrevoOptions _options;
    private readonly ILogger<BrevoEmailSender> _logger;
    private readonly HttpClient _httpClient;

    public BrevoEmailSender(
        IOptions<BrevoOptions> optionsAccessor,
        ILogger<BrevoEmailSender> logger,
        HttpClient? httpClient = null)
    {
        _options = optionsAccessor.Value;
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("Brevo API key is not configured. Email to {ToEmail} skipped.", toEmail);
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.SenderEmail))
        {
            _logger.LogWarning("Sender email is not configured. Email to {ToEmail} skipped.", toEmail);
            return;
        }

        var payload = new
        {
            sender = new { name = _options.SenderName ?? "PersonalFinance", email = _options.SenderEmail },
            to = new[] { new { email = toEmail } },
            subject = subject,
            htmlContent = htmlMessage
        };

        var requestJson = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email")
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("api-key", _options.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Email sent successfully to {ToEmail} via Brevo API.", toEmail);
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to send email to {ToEmail} via Brevo API. Status: {StatusCode}, Details: {Details}",
                    toEmail, response.StatusCode, errorBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToEmail} via Brevo API.", toEmail);
        }
    }
}
