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

    /// <summary>The extra monthly amount is missing.</summary>
    public const string MonthlyAmountRequired = "Enter the extra amount you have each month.";

    /// <summary>The extra monthly amount is out of range.</summary>
    public const string MonthlyAmountRange = "Enter a monthly amount over $0 and at most $1,000,000.";

    /// <summary>A dollar amount has more than two decimal places.</summary>
    public const string WholeCents = "Enter a dollar amount with at most two decimal places.";

    /// <summary>The Purpose is too long.</summary>
    public const string PurposeTooLong = "Keep your purpose to 300 characters or fewer.";

    /// <summary>The debt APR is missing.</summary>
    public const string DebtAprRequired = "Enter the APR of your highest-interest debt, or 0 if you have no debt.";

    /// <summary>The debt APR is out of range.</summary>
    public const string DebtAprRange = "Enter an APR from 0% to 100%, with at most two decimal places.";

    /// <summary>The debt balance is missing while the APR is 7% or more.</summary>
    public const string DebtBalanceRequired = "Enter the balance of that debt.";

    /// <summary>The debt balance is out of range.</summary>
    public const string DebtBalanceRange = "Enter a debt balance over $0 and at most $100,000,000.";

    /// <summary>The emergency fund months are missing while the question is asked.</summary>
    public const string EmergencyFundMonthsRequired = "Enter how many months of expenses your emergency fund covers.";

    /// <summary>The emergency fund months are out of range.</summary>
    public const string EmergencyFundMonthsRange = "Enter from 0 to 120 months, with at most one decimal place.";

    /// <summary>The monthly expenses are missing while the fund covers less than 3 months.</summary>
    public const string MonthlyExpensesRequired = "Enter your monthly expenses.";

    /// <summary>The monthly expenses are out of range.</summary>
    public const string MonthlyExpensesRange = "Enter monthly expenses over $0 and at most $1,000,000.";

    /// <summary>The employer match question is not answered while it is asked.</summary>
    public const string HasFullEmployerMatchRequired = "Say whether you get the full employer match.";

    /// <summary>The employer match percent is missing while the match is not full.</summary>
    public const string EmployerMatchPercentRequired = "Enter your employer's match, in percent of pay.";

    /// <summary>The employer match percent is out of range.</summary>
    public const string EmployerMatchPercentRange = "Enter a match from 0% to 100%, with at most two decimal places.";

    /// <summary>The horizon is missing while it is asked.</summary>
    public const string HorizonYearsRequired = "Enter how many years until you need this money.";

    /// <summary>The horizon is out of range.</summary>
    public const string HorizonYearsRange = "Enter from 1 to 50 years.";

    /// <summary>The age range is missing or unknown while the horizon is over 10 years.</summary>
    public const string AgeBandRequired = "Choose your age range.";

    /// <summary>The contribution limit question is not answered while the horizon is over 10 years.</summary>
    public const string AtContributionLimitRequired = "Say whether you already contribute the IRS limit for your age.";
}
