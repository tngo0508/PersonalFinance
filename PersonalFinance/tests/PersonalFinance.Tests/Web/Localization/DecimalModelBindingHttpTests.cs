using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Infrastructure.Localization;
using PersonalFinance.Web.Models.Budget;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Tests.Web.Localization;

/// <summary>
/// Decimal model binding of posted forms follows the request culture (spec §2.2, §5): a Vietnamese user types
/// <c>1.234,56</c>. Grouping is checked strictly, so a value written in the other culture is rejected rather than read
/// as a number 100 or 1,000 times larger or smaller.
/// </summary>
public sealed class DecimalModelBindingHttpTests : IAsyncLifetime
{
    private const string English = ".AspNetCore.Culture=c%3Den-US%7Cuic%3Den";
    private const string Vietnamese = ".AspNetCore.Culture=c%3Dvi-VN%7Cuic%3Dvi";

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
        builder.Services.AddSingleton<IUserPreferenceService, InMemoryUserPreferenceService>();
        builder.Services.AddAppLocalization(builder.Environment);
        builder.Services.AddControllersWithViews()
            .AddApplicationPart(typeof(DecimalProbeController).Assembly);

        _app = builder.Build();
        _app.UseRequestLocalization();
        _app.UseRouting();
        _app.MapControllerRoute("default", "{controller}/{action}");
        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Theory]
    [InlineData("0", "0")]
    [InlineData("1234.56", "1234.56")]
    [InlineData("1,234.56", "1234.56")]
    [InlineData("1,234,567", "1234567")]
    [InlineData("-1,234.56", "-1234.56")]
    [InlineData(".5", "0.5")]
    [InlineData(" 1,234.50 ", "1234.50")]
    [InlineData("1,234.", "1234")] // a decimal point typed with no digits yet
    [InlineData("1234.565", "1234.565")]
    [InlineData("12345678901234567.89", "12345678901234567.89")]
    public async Task EnUs_BindsEnglishNumbers(string posted, string expected)
    {
        Assert.Equal(expected, await PostAsync(English, posted));
    }

    [Theory]
    [InlineData("0", "0")]
    [InlineData("1234,56", "1234.56")]
    [InlineData("1.234,56", "1234.56")]
    [InlineData("1.234.567", "1234567")]
    [InlineData("-1.234,56", "-1234.56")]
    [InlineData(",5", "0.5")]
    [InlineData("1.234,", "1234")]
    [InlineData("1.234,565", "1234.565")]
    [InlineData("12.345.678.901.234.567,89", "12345678901234567.89")]
    public async Task ViVn_BindsVietnameseNumbers(string posted, string expected)
    {
        Assert.Equal(expected, await PostAsync(Vietnamese, posted));
    }

    [Theory]
    [InlineData("1234.56")] // English decimal point: the default binder reads 123456
    [InlineData("1.5")] // the default binder reads 15
    [InlineData("1,234.56")]
    [InlineData("12.34")]
    [InlineData("1.2345")]
    public async Task ViVn_RejectsANumberWrittenTheEnglishWay(string posted)
    {
        Assert.Equal("invalid", await PostAsync(Vietnamese, posted));
    }

    [Theory]
    [InlineData("1234,56")] // Vietnamese decimal comma: the default binder reads 123456
    [InlineData("1,5")]
    [InlineData("1.234,56")]
    [InlineData("12,34")]
    public async Task EnUs_RejectsANumberWrittenTheVietnameseWay(string posted)
    {
        Assert.Equal("invalid", await PostAsync(English, posted));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1e3")]
    [InlineData("$1,234.56")]
    [InlineData("(1,234.56)")]
    [InlineData(".")]
    [InlineData("1,,234")]
    [InlineData(",234")]
    [InlineData("--1")]
    [InlineData("1-")]
    [InlineData("99999999999999999999999999999")]
    public async Task EnUs_RejectsWhatIsNotAPlainNumber(string posted)
    {
        Assert.Equal("invalid", await PostAsync(English, posted));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Vietnamese)]
    public async Task Empty_IsAnErrorForADecimal_AndNullForANullableDecimal(string cookie)
    {
        Assert.Equal("invalid", await PostAsync(cookie, string.Empty));
        Assert.Equal("null", await PostAsync(cookie, string.Empty, "Nullable"));
    }

    [Fact]
    public async Task ViVn_InvalidValue_HasTheLocalizedBindingMessage()
    {
        string message = await PostAsync(Vietnamese, "1234.56", "Message");

        Assert.Contains("1234.56", message);
        Assert.DoesNotContain("is not valid", message);
    }

    [Fact]
    public async Task ViVn_QueryString_StaysInvariant()
    {
        // Query strings and route values are machine-written (links, fetch URLs), so they keep the invariant format.
        using var request = new HttpRequestMessage(HttpMethod.Get, "/DecimalProbe/Query?amount=1234.56");
        request.Headers.Add("Cookie", Vietnamese);

        HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal("1234.56", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ViVn_BudgetViewModel_BindsEveryAmountInTheRequestCulture()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/DecimalProbe/Budget")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                [nameof(BudgetViewModel.MonthlyIncome)] = "12.500,75",
                [nameof(BudgetViewModel.Housing)] = "3.000",
                [nameof(BudgetViewModel.Food)] = "0"
            })
        };
        request.Headers.Add("Cookie", Vietnamese);

        HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal("12500.75|3000|0", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task JsonBody_IsStillReadByTheInputFormatter()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/DecimalProbe/Json")
        {
            Content = new StringContent("{\"amount\":1234.56}", System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Cookie", Vietnamese);

        HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal("1234.56", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public void Provider_SitsJustAheadOfTheFrameworksFloatingPointProvider()
    {
        IList<IModelBinderProvider> providers = _app.Services.GetRequiredService<IOptions<MvcOptions>>().Value
            .ModelBinderProviders;
        int ours = providers.IndexOf(providers.Single(p => p.GetType().Name == "CultureDecimalModelBinderProvider"));

        Assert.IsType<FloatingPointTypeModelBinderProvider>(providers[ours + 1]);
        Assert.True(ours > providers.IndexOf(providers.OfType<BodyModelBinderProvider>().Single()));
    }

    private async Task<string> PostAsync(string cookie, string amount, string action = "Bind")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/DecimalProbe/{action}")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["amount"] = amount })
        };
        request.Headers.Add("Cookie", cookie);
        HttpResponseMessage response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }
}

/// <summary>Echoes what MVC bound, in the invariant culture, or "invalid" when binding failed.</summary>
public sealed class DecimalProbeController : Controller
{
    [HttpPost]
    public IActionResult Bind([FromForm] decimal amount) =>
        Content(ModelState.IsValid ? amount.ToString(CultureInfo.InvariantCulture) : "invalid");

    [HttpPost]
    public IActionResult Nullable([FromForm] decimal? amount) =>
        Content(!ModelState.IsValid ? "invalid" : amount?.ToString(CultureInfo.InvariantCulture) ?? "null");

    [HttpPost]
    public IActionResult Message([FromForm] decimal amount) =>
        Content(string.Join(" ", ModelState[nameof(amount)]?.Errors.Select(e => e.ErrorMessage) ?? []));

    [HttpPost]
    public IActionResult Json([FromBody] JsonAmount body) =>
        Content(ModelState.IsValid ? body.Amount.ToString(CultureInfo.InvariantCulture) : "invalid");

    [HttpGet]
    public IActionResult Query([FromQuery] decimal amount) =>
        Content(ModelState.IsValid ? amount.ToString(CultureInfo.InvariantCulture) : "invalid");

    [HttpPost]
    public IActionResult Budget(BudgetViewModel model) =>
        Content(ModelState.IsValid
            ? string.Join("|", new[] { model.MonthlyIncome, model.Housing, model.Food }
                .Select(value => value.ToString(CultureInfo.InvariantCulture)))
            : "invalid");
}

/// <summary>A JSON request body with one amount.</summary>
public sealed class JsonAmount
{
    public decimal Amount { get; set; }
}
