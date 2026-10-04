using System.Net;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PersonalFinance.Web.Services;
using Xunit;

namespace PersonalFinance.Tests;

public class FakeSmtpClient : ISmtpClient
{
    public MailMessage? LastMessageSent { get; private set; }
    public bool ShouldThrow { get; set; }

    public Task SendMailAsync(MailMessage message, CancellationToken cancellationToken = default)
    {
        if (ShouldThrow)
        {
            throw new SmtpException("SMTP connection failed.");
        }

        LastMessageSent = message;
        return Task.CompletedTask;
    }
}

public class FakeHttpMessageHandler : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }
    public HttpStatusCode ResponseStatusCode { get; set; } = HttpStatusCode.OK;
    public string ResponseContent { get; set; } = "{\"messageId\":\"test-msg-123\"}";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        if (request.Content != null)
        {
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        return new HttpResponseMessage(ResponseStatusCode)
        {
            Content = new StringContent(ResponseContent)
        };
    }
}

public class BrevoEmailSenderTests
{
    [Fact]
    public async Task SendEmailAsync_WhenCredentialsMissingAndClientIsNull_DoesNotThrowAndSkips()
    {
        var options = Options.Create(new BrevoOptions
        {
            ApiKey = null,
            Login = null,
            Password = null
        });

        var sender = new BrevoEmailSender(options, NullLogger<BrevoEmailSender>.Instance, httpClient: null, smtpClient: null);

        // Should return cleanly without throwing exceptions
        await sender.SendEmailAsync("user@example.com", "Confirm your account", "<p>Please confirm</p>");
    }

    [Fact]
    public async Task SendEmailAsync_WhenApiKeyConfigured_SendsViaBrevoRestApi()
    {
        var options = Options.Create(new BrevoOptions
        {
            ApiKey = "xkeysib-test-api-key",
            SenderEmail = "no-reply@personalfinance.local",
            SenderName = "PersonalFinance"
        });

        var handler = new FakeHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var sender = new BrevoEmailSender(options, NullLogger<BrevoEmailSender>.Instance, httpClient: httpClient);

        await sender.SendEmailAsync("user@example.com", "Confirm Email", "<p>Click here to confirm</p>");

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Equal("https://api.brevo.com/v3/smtp/email", handler.LastRequest.RequestUri?.ToString());
        Assert.True(handler.LastRequest.Headers.Contains("api-key"));
        Assert.Equal("xkeysib-test-api-key", handler.LastRequest.Headers.GetValues("api-key").First());

        Assert.NotNull(handler.LastRequestBody);
        using var jsonDoc = JsonDocument.Parse(handler.LastRequestBody);
        var root = jsonDoc.RootElement;
        Assert.Equal("PersonalFinance", root.GetProperty("sender").GetProperty("name").GetString());
        Assert.Equal("no-reply@personalfinance.local", root.GetProperty("sender").GetProperty("email").GetString());
        Assert.Equal("user@example.com", root.GetProperty("to")[0].GetProperty("email").GetString());
        Assert.Equal("Confirm Email", root.GetProperty("subject").GetString());
        Assert.Equal("<p>Click here to confirm</p>", root.GetProperty("htmlContent").GetString());
    }

    [Fact]
    public async Task SendEmailAsync_WhenApiReturnsError_HandlesGracefullyWithoutUncaughtException()
    {
        var options = Options.Create(new BrevoOptions
        {
            ApiKey = "xkeysib-test-api-key",
            SenderEmail = "no-reply@personalfinance.local"
        });

        var handler = new FakeHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.Unauthorized,
            ResponseContent = "{\"message\":\"Key not found\"}"
        };
        var httpClient = new HttpClient(handler);
        var sender = new BrevoEmailSender(options, NullLogger<BrevoEmailSender>.Instance, httpClient: httpClient);

        // Should log error and not throw uncaught exception
        await sender.SendEmailAsync("user@example.com", "Confirm Email", "<p>Click</p>");

        Assert.NotNull(handler.LastRequest);
    }

    [Fact]
    public async Task SendEmailAsync_WhenConfiguredWithSmtp_SendsHtmlEmailViaSmtp()
    {
        var options = Options.Create(new BrevoOptions
        {
            SmtpServer = "smtp-relay.brevo.com",
            Port = 587,
            Login = "bc723b001@smtp-brevo.com",
            Password = "fake-password",
            SenderEmail = "bc723b001@smtp-brevo.com",
            SenderName = "PersonalFinance",
            EnableSsl = true
        });

        var fakeClient = new FakeSmtpClient();
        var sender = new BrevoEmailSender(options, NullLogger<BrevoEmailSender>.Instance, smtpClient: fakeClient);

        await sender.SendEmailAsync("recipient@example.com", "Confirm Email", "<p>Click here</p>");

        Assert.NotNull(fakeClient.LastMessageSent);
        Assert.Equal("bc723b001@smtp-brevo.com", fakeClient.LastMessageSent.From?.Address);
        Assert.Equal("PersonalFinance", fakeClient.LastMessageSent.From?.DisplayName);
        Assert.Equal("recipient@example.com", fakeClient.LastMessageSent.To[0].Address);
        Assert.Equal("Confirm Email", fakeClient.LastMessageSent.Subject);
        Assert.Equal("<p>Click here</p>", fakeClient.LastMessageSent.Body);
        Assert.True(fakeClient.LastMessageSent.IsBodyHtml);
    }

    [Fact]
    public async Task SendEmailAsync_WhenSmtpThrows_HandlesGracefullyWithoutUncaughtException()
    {
        var options = Options.Create(new BrevoOptions
        {
            Login = "bc723b001@smtp-brevo.com",
            Password = "fake-password"
        });

        var fakeClient = new FakeSmtpClient
        {
            ShouldThrow = true
        };

        var sender = new BrevoEmailSender(options, NullLogger<BrevoEmailSender>.Instance, smtpClient: fakeClient);

        // Exception caught and logged cleanly
        await sender.SendEmailAsync("recipient@example.com", "Confirm Email", "<p>Click here</p>");
    }

    [Fact]
    public void DependencyInjection_RegistersIEmailSenderCorrectly()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Brevo:ApiKey", "xkeysib-secret-key" },
            { "Brevo:SenderEmail", "bc723b001@smtp-brevo.com" },
            { "Brevo:SenderName", "PersonalFinance" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<BrevoOptions>(configuration.GetSection("Brevo"));
        services.AddHttpClient<IEmailSender, BrevoEmailSender>();

        var provider = services.BuildServiceProvider();
        var emailSender = provider.GetService<IEmailSender>();

        Assert.NotNull(emailSender);
        Assert.IsType<BrevoEmailSender>(emailSender);

        var options = provider.GetRequiredService<IOptions<BrevoOptions>>().Value;
        Assert.Equal("xkeysib-secret-key", options.ApiKey);
        Assert.Equal("bc723b001@smtp-brevo.com", options.SenderEmail);
        Assert.Equal("PersonalFinance", options.SenderName);
    }
}
