using System.Text.Json.Serialization;

namespace PersonalFinance.Shared.Enums;

/// <summary>How paying the whole monthly amount toward a high-interest debt ends (Money Priorities leaf L1).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PriorityDebtOutcome
{
    /// <summary>The payments clear the debt; months, total interest and the final payment are set.</summary>
    PaidOff = 1,

    /// <summary>The monthly amount is at or below a month's interest, so the debt never shrinks. No figures.</summary>
    AmountDoesNotCoverInterest = 2,

    /// <summary>The debt shrinks, but takes longer than the maximum months to clear. No figures.</summary>
    LongerThanMaximum = 3
}
