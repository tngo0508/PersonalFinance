namespace PersonalFinance.Shared.Constants;

/// <summary>Fixed wording for the Money Priorities page.</summary>
public static class MoneyPrioritiesMessages
{
    /// <summary>
    /// Shown under every Money Priorities result (spec #315, "Display"). Separate from
    /// <see cref="AiAssistantMessages.Disclaimer"/>, which goes under the AI-written Purpose Note.
    /// </summary>
    public const string EstimateDisclaimer =
        "Estimates assume a constant yearly return and monthly contributions. They leave out taxes, fees and inflation. "
        + "This is not financial advice.";
}
