using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PersonalFinance.Web.Infrastructure;
using Xunit;

namespace PersonalFinance.Tests.Web;

public class FakeHttpMessageHandler : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }
    public HttpStatusCode ResponseStatusCode { get; set; } = HttpStatusCode.OK;
    public string ResponseContent { get; set; } = "{\"messageId\":\"test-msg-123\"}";
    public bool ShouldThrow { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (ShouldThrow)
        {
            throw new HttpRequestException("Network failure");
        }

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
    public async Task SendEmailAsync_WhenApiKeyMissing_DoesNotThrowAndSkips()
    {
        IOptions<BrevoOptions> options = Options.Create(new BrevoOptions
        {
            ApiKey = null,
            SenderEmail = "no-reply@personalfinance.local"
        });

        var sender = new BrevoEmailSender(options, NullLogger<BrevoEmailSender>.Instance);

        // Should return cleanly without throwing exceptions
        await sender.SendEmailAsync("user@example.com", "Confirm your account", "<p>Please confirm</p>");
    }

    [Fact]
    public async Task SendEmailAsync_WhenSenderEmailMissing_DoesNotThrowAndSkips()
    {
        IOptions<BrevoOptions> options = Options.Create(new BrevoOptions
        {
            ApiKey = "xkeysib-test-api-key",
            SenderEmail = null
        });

        var sender = new BrevoEmailSender(options, NullLogger<BrevoEmailSender>.Instance);

        await sender.SendEmailAsync("user@example.com", "Confirm your account", "<p>Please confirm</p>");
    }

    [Fact]
    public async Task SendEmailAsync_WhenApiKeyConfigured_SendsViaBrevoRestApi()
    {
        IOptions<BrevoOptions> options = Options.Create(new BrevoOptions
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
        JsonElement root = jsonDoc.RootElement;
        Assert.Equal("PersonalFinance", root.GetProperty("sender").GetProperty("name").GetString());
        Assert.Equal("no-reply@personalfinance.local", root.GetProperty("sender").GetProperty("email").GetString());
        Assert.Equal("user@example.com", root.GetProperty("to")[0].GetProperty("email").GetString());
        Assert.Equal("Confirm Email", root.GetProperty("subject").GetString());
        Assert.Equal("<p>Click here to confirm</p>", root.GetProperty("htmlContent").GetString());
    }

    [Fact]
    public async Task SendEmailAsync_WhenApiReturnsError_HandlesGracefullyWithoutUncaughtException()
    {
        IOptions<BrevoOptions> options = Options.Create(new BrevoOptions
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
    public async Task SendEmailAsync_WhenHttpThrows_HandlesGracefullyWithoutUncaughtException()
    {
        IOptions<BrevoOptions> options = Options.Create(new BrevoOptions
        {
            ApiKey = "xkeysib-test-api-key",
            SenderEmail = "no-reply@personalfinance.local"
        });

        var handler = new FakeHttpMessageHandler
        {
            ShouldThrow = true
        };
        var httpClient = new HttpClient(handler);
        var sender = new BrevoEmailSender(options, NullLogger<BrevoEmailSender>.Instance, httpClient: httpClient);

        await sender.SendEmailAsync("user@example.com", "Confirm Email", "<p>Click</p>");
    }
}
