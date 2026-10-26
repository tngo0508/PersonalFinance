using System.Text.Json;
using PersonalFinance.Shared.Contracts;

namespace PersonalFinance.Tests.Shared;

/// <summary>
/// The JSON shape of an API rejection (#385, spec §2.1) as the Web app's Refit client reads it: camelCase names, the
/// arg kind as its name, and no <c>field</c>, <c>args</c> or <c>currency</c> when they are not set.
/// </summary>
public class ApiErrorDtoTests
{
    private static readonly JsonSerializerOptions CamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public void Serializes_KindAsText_AndOmitsNullFieldAndArgs()
    {
        ApiErrorEntryDto entry = new() { Key = "MoneyPriorities.Validation.Purpose", Message = "Purpose is too long." };

        string json = JsonSerializer.Serialize(entry, CamelCase);

        Assert.Contains("\"key\":\"MoneyPriorities.Validation.Purpose\"", json);
        Assert.Contains("\"message\":\"Purpose is too long.\"", json);
        Assert.DoesNotContain("\"field\"", json);
        Assert.DoesNotContain("\"args\"", json);
    }

    [Fact]
    public void Serializes_MoneyArg_WithKindNameAndCurrency()
    {
        ApiErrorEntryDto entry = new()
        {
            Key = "MoneyPriorities.Validation.DebtBalance",
            Message = "Debt balance must be at most $10,000,000.",
            Args = new Dictionary<string, ApiErrorArgDto>
            {
                ["max"] = new() { Kind = ApiErrorArgKind.Money, Value = "10000000", Currency = "USD" },
            },
        };

        string json = JsonSerializer.Serialize(entry, CamelCase);

        Assert.Contains("\"kind\":\"Money\"", json);
        Assert.Contains("\"currency\":\"USD\"", json);
    }

    [Fact]
    public void RoundTrips()
    {
        ApiErrorDto original = new()
        {
            Errors =
            [
                new ApiErrorEntryDto
                {
                    Key = "MoneyPriorities.Validation.DebtBalance",
                    Message = "Debt balance must be at most $10,000,000.",
                    Field = "debtBalance",
                    Args = new Dictionary<string, ApiErrorArgDto>
                    {
                        ["max"] = new() { Kind = ApiErrorArgKind.Money, Value = "10000000", Currency = "USD" },
                    },
                },
            ],
        };

        string json = JsonSerializer.Serialize(original, CamelCase);
        ApiErrorDto? roundTripped = JsonSerializer.Deserialize<ApiErrorDto>(json, CamelCase);

        Assert.NotNull(roundTripped);
        ApiErrorEntryDto entry = Assert.Single(roundTripped.Errors);
        Assert.Equal("MoneyPriorities.Validation.DebtBalance", entry.Key);
        Assert.Equal("debtBalance", entry.Field);
        Assert.NotNull(entry.Args);
        ApiErrorArgDto max = entry.Args["max"];
        Assert.Equal(ApiErrorArgKind.Money, max.Kind);
        Assert.Equal("10000000", max.Value);
        Assert.Equal("USD", max.Currency);
    }
}
