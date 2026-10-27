using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using PersonalFinance.ApiService.Features.CurrencyConverter.Validators;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.CurrencyConverter;
using PersonalFinance.Tests.Security;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.CurrencyConverter;

/// <summary>
/// The Currency Converter endpoint (#446): signed-in only, and its validator rejects a code that is not a Convertible
/// Currency, a missing or negative amount, and a date before 2015-01-01 or after today (UTC), each under its key.
/// </summary>
[Collection("ApiServiceHost")]
public class CurrencyConverterEndpointTests : IClassFixture<ApiServiceInternalKeyHttpTests.ApiServiceHost>
{
    private const string InternalKey = "http-test-shared-secret";
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 23, 30, 0, TimeSpan.Zero);

    private readonly ApiServiceInternalKeyHttpTests.ApiServiceHost _host;

    public CurrencyConverterEndpointTests(ApiServiceInternalKeyHttpTests.ApiServiceHost host)
    {
        _host = host;
    }

    // A 401 also proves the controller requires sign-in: ApiService has no fallback policy, so an [AllowAnonymous]
    // action would answer without the shared secret.
    [Fact]
    public async Task Get_OverHttp_WithoutTheSharedSecret_IsRejected()
    {
        HttpClient client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/CurrencyConverter?amount=1&from=JPY&to=VND");
        request.Headers.Add("X-User-Id", "account-1");

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_OverHttp_SignedIn_ConvertsAndBindsTheQuery()
    {
        HttpClient client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "/api/CurrencyConverter?amount=1000000&from=JPY&to=JPY&date=2020-02-03");
        request.Headers.Add("X-User-Id", "account-1");
        request.Headers.Add("X-Internal-Key", InternalKey);

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        CurrencyConversionDto? result = await response.Content.ReadFromJsonAsync<CurrencyConversionDto>();
        Assert.NotNull(result);
        Assert.Equal((1_000_000m, 1_000_000m, 1m, new DateOnly(2020, 2, 3)),
            (result.Amount, result.ConvertedAmount, result.Rate, result.Date));
    }

    [Fact]
    public async Task Get_OverHttp_Invalid_AnswersBadRequestWithTheKey()
    {
        HttpClient client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/CurrencyConverter?amount=-1&from=JPY&to=VND");
        request.Headers.Add("X-User-Id", "account-1");
        request.Headers.Add("X-Internal-Key", InternalKey);

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ApiErrorDto? error = await response.Content.ReadFromJsonAsync<ApiErrorDto>();
        ApiErrorEntryDto entry = Assert.Single(error!.Errors);
        Assert.Equal((ApiErrorKeys.CurrencyConverter.AmountNegative, "Amount"), (entry.Key, entry.Field));
    }

    [Theory]
    [InlineData("0", "USD", "VND", null)]
    [InlineData("1000000", "JPY", "VND", "2015-01-01")]
    [InlineData("1", "CAD", "KRW", "2026-10-26")]
    public void Validator_AcceptsAValidRequest(string amount, string from, string to, string? date)
    {
        Assert.True(Validator().Validate(Request(amount, from, to, date)).IsValid);
    }

    [Theory]
    [InlineData(null, "USD", "VND", null, ApiErrorKeys.CurrencyConverter.AmountRequired)]
    [InlineData("-0.01", "USD", "VND", null, ApiErrorKeys.CurrencyConverter.AmountNegative)]
    [InlineData("1", "XYZ", "VND", null, ApiErrorKeys.CurrencyConverter.FromUnsupported)]
    [InlineData("1", null, "VND", null, ApiErrorKeys.CurrencyConverter.FromUnsupported)]
    [InlineData("1", "USD", "jpy", null, ApiErrorKeys.CurrencyConverter.ToUnsupported)]
    [InlineData("1", "USD", "VND", "2014-12-31", ApiErrorKeys.CurrencyConverter.DateTooEarly)]
    [InlineData("1", "USD", "VND", "2026-10-27", ApiErrorKeys.CurrencyConverter.DateInFuture)]
    public void Validator_RejectsWithTheRulesKey(string? amount, string? from, string? to, string? date, string key)
    {
        FluentValidation.Results.ValidationResult result = Validator().Validate(Request(amount, from, to, date));

        Assert.Equal(key, Assert.Single(result.Errors).ErrorCode);
    }

    private static ConvertCurrencyRequestValidator Validator() => new(new FixedTimeProvider(Now));

    private static ConvertCurrencyRequestDto Request(string? amount, string? from, string? to, string? date) => new()
    {
        Amount = amount is null ? null : decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture),
        From = from,
        To = to,
        Date = date is null ? null : DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture)
    };
}
