using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Infrastructure.Localization;

namespace PersonalFinance.Tests.Web.Localization;

/// <summary>
/// Request localization as <c>AddAppLocalization</c> configures it, over HTTP (spec §2.2–2.3): the culture cookie, then
/// Accept-Language, then English; no query-string provider; formatting stays en-US in slice L1. Also the switcher
/// endpoint refusing a POST without an antiforgery token.
/// </summary>
public sealed class RequestLocalizationHttpTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = RepositoryRoot.SourceProject("PersonalFinance.Web"),
            EnvironmentName = "Testing"
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddAppLocalization(builder.Environment);
        builder.Services.AddControllersWithViews(options =>
                options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
            .AddApplicationPart(typeof(LanguageController).Assembly);

        _app = builder.Build();
        _app.UseRequestLocalization();
        _app.UseRouting();
        _app.UseAntiforgery();
        _app.MapGet("/culture", () => $"{CultureInfo.CurrentCulture.Name}|{CultureInfo.CurrentUICulture.Name}");
        _app.MapControllerRoute("default", "{controller}/{action}");
        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task NoCookieOrHeader_IsEnglish()
    {
        Assert.Equal("en-US|en", await CultureAsync());
    }

    [Theory]
    [InlineData("vi", "en-US|vi")]
    [InlineData("vi-VN,vi;q=0.9,en;q=0.8", "en-US|vi")]
    [InlineData("fr-FR,fr;q=0.9", "en-US|en")]
    [InlineData("en-GB", "en-US|en")]
    public async Task AcceptLanguage_PicksASupportedLanguage(string acceptLanguage, string expected)
    {
        Assert.Equal(expected, await CultureAsync(request => request.Headers.Add("Accept-Language", acceptLanguage)));
    }

    [Fact]
    public async Task Cookie_WinsOverAcceptLanguage_AndFormattingStaysEnUs()
    {
        string culture = await CultureAsync(request =>
        {
            request.Headers.Add("Accept-Language", "en-US");
            request.Headers.Add("Cookie", ".AspNetCore.Culture=c%3Dvi-VN%7Cuic%3Dvi");
        });

        Assert.Equal("en-US|vi", culture);
    }

    [Fact]
    public async Task QueryString_DoesNotChangeTheLanguage()
    {
        HttpResponseMessage response = await _client.GetAsync("/culture?culture=vi&ui-culture=vi");

        Assert.Equal("en-US|en", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Switcher_PostWithoutAntiforgeryToken_IsRejected_AndSetsNoCookie()
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["language"] = "vi",
            ["returnUrl"] = "/"
        });

        HttpResponseMessage response = await _client.PostAsync("/Language/Set", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    private async Task<string> CultureAsync(Action<HttpRequestMessage>? configure = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/culture");
        configure?.Invoke(request);
        HttpResponseMessage response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
