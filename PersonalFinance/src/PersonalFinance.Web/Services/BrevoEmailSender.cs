using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
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
    private readonly HttpClient? _httpClient;
    private readonly ISmtpClient? _smtpClient;

    public BrevoEmailSender(
        IOptions<BrevoOptions> optionsAccessor,
        ILogger<BrevoEmailSender> logger,
        HttpClient? httpClient = null,
        ISmtpClient? smtpClient = null)
    {
        _options = optionsAccessor.Value;
        _logger = logger;
        _httpClient = httpClient;
        _smtpClient = smtpClient;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
    {
        var senderEmail = !string.IsNullOrWhiteSpace(_options.SenderEmail)
            ? _options.SenderEmail
            : _options.Login;

        if (string.IsNullOrWhiteSpace(senderEmail))
        {
            _logger.LogWarning("Sender email is not configured. Email to {ToEmail} skipped.", toEmail);
            return;
        }

        // 1. If Brevo API Key is configured, prioritize REST API over HTTPS
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            await SendViaApiAsync(toEmail, subject, htmlMessage, senderEmail);
            return;
        }

        // 2. Fall back to SMTP Relay if credentials or mock client are provided
        if (!string.IsNullOrWhiteSpace(_options.Login) && !string.IsNullOrWhiteSpace(_options.Password) || _smtpClient != null)
        {
            await SendViaSmtpAsync(toEmail, subject, htmlMessage, senderEmail);
            return;
        }

        _logger.LogWarning("Brevo credentials (ApiKey or SMTP Password) are not configured. Email to {ToEmail} skipped.", toEmail);
    }

    private async Task SendViaApiAsync(string toEmail, string subject, string htmlMessage, string senderEmail)
    {
        var payload = new
        {
            sender = new { name = _options.SenderName ?? "PersonalFinance", email = senderEmail },
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
            var client = _httpClient ?? new HttpClient();
            var response = await client.SendAsync(request);

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

    private async Task SendViaSmtpAsync(string toEmail, string subject, string htmlMessage, string senderEmail)
    {
        var mailMessage = new MailMessage
        {
            From = new MailAddress(senderEmail, _options.SenderName ?? "PersonalFinance"),
            Subject = subject,
            Body = htmlMessage,
            IsBodyHtml = true
        };
        mailMessage.To.Add(toEmail);

        try
        {
            if (_smtpClient != null)
            {
                await _smtpClient.SendMailAsync(mailMessage);
            }
            else
            {
                using var client = new SmtpClient(_options.SmtpServer, _options.Port)
                {
                    Credentials = new NetworkCredential(_options.Login, _options.Password),
                    EnableSsl = _options.EnableSsl
                };

                await client.SendMailAsync(mailMessage);
            }

            _logger.LogInformation("Email sent successfully to {ToEmail} via Brevo SMTP.", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToEmail} via Brevo SMTP.", toEmail);
        }
    }
}
