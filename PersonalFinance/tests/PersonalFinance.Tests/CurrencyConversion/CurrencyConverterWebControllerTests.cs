using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.CurrencyConverter;
using PersonalFinance.Shared.Helpers;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Models.CurrencyConverter;
using Refit;
using AuthorizeAttribute = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

namespace PersonalFinance.Tests.CurrencyConversion;

/// <summary>
/// The Web Currency Converter page (#446): signed-in only; before the first submit it shows the form with defaults from
/// the Account's Reporting Currency and calls nothing; a submit passes the inputs to the ApiService and shows its result;
/// a rejected field shows its message next to it; a failure never shows exception details.
/// </summary>
public class CurrencyConverterWebControllerTests
{
    private const string UserId = "converter-user";
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Controller_RequiresSignIn()
    {
        Assert.NotNull(Attribute.GetCustomAttribute(typeof(CurrencyConverterController), typeof(AuthorizeAttribute)));
    }

    [Theory]
    [InlineData("USD", "VND", "USD")]
    [InlineData("VND", "USD", "VND")]
    public async Task FirstVisit_DefaultsFromTheReportingCurrency_AndCallsNothing(string reporting, string from, string to)
    {
        var api = new FakeConverterApi();
        var preferences = new InMemoryUserPreferenceService();
        preferences.ReportingCurrencies[UserId] = reporting;

        CurrencyConverterViewModel model = Model(await CreateController(api, preferences).Index(null, null, null, null));

        Assert.Equal((from, to, (decimal?)null, (DateOnly?)null), (model.From, model.To, model.Amount, model.Date));
        Assert.Null(model.Result);
        Assert.Equal(0, api.Calls);
        Assert.Equal(new DateOnly(2026, 10, 26), model.MaxDate);
        Assert.Equal(ConvertibleCurrencies.All.Select(c => c.Code), model.Currencies.Select(c => c.Code));
    }

    [Fact]
    public async Task Submit_PassesTheInputs_AndShowsTheResult()
    {
        var conversion = new CurrencyConversionDto { Amount = 1_000_000m, From = "JPY", To = "VND", ConvertedAmount = 170_000_000m, Rate = 170m };
        var api = new FakeConverterApi { Result = conversion };
        var date = new DateOnly(2026, 10, 20);

        CurrencyConverterViewModel model = Model(await CreateController(api).Index(1_000_000m, "JPY", "VND", date));

        Assert.Same(conversion, model.Result);
        Assert.Equal((1_000_000m, "JPY", "VND", date), api.LastRequest);
    }

    [Fact]
    public async Task RejectedField_ShowsItsMessageUnderThatField_AndNoResult()
    {
        string body = """{"errors":[{"key":"CurrencyConverter.Validation.AmountNegative","message":"Amount cannot be negative.","field":"Amount"}]}""";
        var api = new FakeConverterApi { Failure = await ApiExceptionAsync(HttpStatusCode.BadRequest, body) };
        CurrencyConverterController controller = CreateController(api);

        CurrencyConverterViewModel model = Model(await controller.Index(-1m, "JPY", "VND", null));

        Assert.Null(model.Result);
        Assert.Equal(TestLocalization.Localizer[ApiErrorKeys.CurrencyConverter.AmountNegative].Value,
            Assert.Single(controller.ModelState["Amount"]!.Errors).ErrorMessage);
    }

    [Fact]
    public async Task ServerFailure_ShowsAGeneralMessage_WithoutDetails()
    {
        var api = new FakeConverterApi
        {
            Failure = await ApiExceptionAsync(HttpStatusCode.InternalServerError, "System.Exception: secret stack trace")
        };
        CurrencyConverterController controller = CreateController(api);

        CurrencyConverterViewModel model = Model(await controller.Index(1m, "JPY", "VND", null));

        Assert.Null(model.Result);
        string message = Assert.Single(controller.ModelState[string.Empty]!.Errors).ErrorMessage;
        Assert.DoesNotContain("stack trace", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("164.213472", "164.213")]
    [InlineData("25500", "25,500")]
    [InlineData("170", "170")]
    [InlineData("0.006097561", "0.00609756")]
    [InlineData("1234567.89", "1,234,568")]
    [InlineData("0.0000123456789", "0.0000123457")]
    public void Rate_IsShownToSixSignificantDigits(string rate, string expected)
    {
        Assert.Equal(expected, CurrencyConverterViewModel.FormatRate(
            decimal.Parse(rate, CultureInfo.InvariantCulture), CultureInfo.GetCultureInfo("en-US")));
    }

    [Theory]
    [InlineData("1000000", "JPY", "1,000,000 JPY")]
    [InlineData("1234.565", "EUR", "1,234.57 EUR")]
    [InlineData("1234.565", "USD", "$1,234.57")]
    [InlineData("170000000", "VND", "₫170,000,000")]
    public void ConvertedAmount_IsFormattedInTheToCurrencysMinorUnits(string amount, string code, string expected)
    {
        Assert.Equal(expected, MoneyFormatter.FormatConvertible(decimal.Parse(amount, CultureInfo.InvariantCulture), code,
            CultureInfo.GetCultureInfo("en-US")));
    }

    private static CurrencyConverterViewModel Model(IActionResult result) =>
        Assert.IsType<CurrencyConverterViewModel>(Assert.IsType<ViewResult>(result).Model);

    private static CurrencyConverterController CreateController(ICurrencyConverterApi api,
        InMemoryUserPreferenceService? preferences = null)
    {
        var controller = new CurrencyConverterController(api, preferences ?? new InMemoryUserPreferenceService(),
            TestLocalization.Localizer, new FixedTimeProvider(Now), NullLogger<CurrencyConverterController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, UserId)], "Test"))
            }
        };
        return controller;
    }

    private static async Task<ApiException> ApiExceptionAsync(HttpStatusCode statusCode, string content)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.local/api/CurrencyConverter");
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };
        return await ApiException.Create(request, HttpMethod.Get, response, new RefitSettings());
    }

    /// <summary>An <see cref="ICurrencyConverterApi"/> answering one fixed result or failure.</summary>
    internal sealed class FakeConverterApi : ICurrencyConverterApi
    {
        public CurrencyConversionDto Result { get; set; } = new();
        public Exception? Failure { get; init; }
        public int Calls { get; private set; }
        public (decimal?, string, string, DateOnly?) LastRequest { get; private set; }

        public Task<CurrencyConversionDto> ConvertAsync(decimal? amount, string from, string to, DateOnly? date,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequest = (amount, from, to, date);
            return Failure is null ? Task.FromResult(Result) : Task.FromException<CurrencyConversionDto>(Failure);
        }
    }
}
