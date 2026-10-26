namespace PersonalFinance.Shared.Constants;

/// <summary>
/// The localization keys an <see cref="PersonalFinance.Shared.Contracts.ApiErrorEntryDto"/> carries (ADR 0017,
/// localized API errors spec §2.1), one nested class per Feature. Each value is the key of the message in the Web
/// app's <c>en.json</c>. A key is named <c>&lt;Feature&gt;.Validation.&lt;Rule&gt;</c> for a validator rule and
/// <c>&lt;Feature&gt;.Error.&lt;Rule&gt;</c> for any other rejection.
/// </summary>
public static class ApiErrorKeys
{
    /// <summary>The keys of the Money Priorities Feature's rejections.</summary>
    public static class MoneyPriorities
    {
        /// <summary>A submitted Priority Walk broke the <c>MonthlyAmountRequired</c> rule.</summary>
        public const string MonthlyAmountRequired = "MoneyPriorities.Validation.MonthlyAmountRequired";

        /// <summary>A submitted Priority Walk broke the <c>MonthlyAmountRange</c> rule.</summary>
        public const string MonthlyAmountRange = "MoneyPriorities.Validation.MonthlyAmountRange";

        /// <summary>A submitted Priority Walk broke the <c>WholeCents</c> rule.</summary>
        public const string WholeCents = "MoneyPriorities.Validation.WholeCents";

        /// <summary>A submitted Priority Walk broke the <c>PurposeTooLong</c> rule.</summary>
        public const string PurposeTooLong = "MoneyPriorities.Validation.PurposeTooLong";

        /// <summary>A submitted Priority Walk broke the <c>DebtAprRequired</c> rule.</summary>
        public const string DebtAprRequired = "MoneyPriorities.Validation.DebtAprRequired";

        /// <summary>A submitted Priority Walk broke the <c>DebtAprRange</c> rule.</summary>
        public const string DebtAprRange = "MoneyPriorities.Validation.DebtAprRange";

        /// <summary>A submitted Priority Walk broke the <c>DebtBalanceRequired</c> rule.</summary>
        public const string DebtBalanceRequired = "MoneyPriorities.Validation.DebtBalanceRequired";

        /// <summary>A submitted Priority Walk broke the <c>DebtBalanceRange</c> rule.</summary>
        public const string DebtBalanceRange = "MoneyPriorities.Validation.DebtBalanceRange";

        /// <summary>A submitted Priority Walk broke the <c>EmergencyFundMonthsRequired</c> rule.</summary>
        public const string EmergencyFundMonthsRequired = "MoneyPriorities.Validation.EmergencyFundMonthsRequired";

        /// <summary>A submitted Priority Walk broke the <c>EmergencyFundMonthsRange</c> rule.</summary>
        public const string EmergencyFundMonthsRange = "MoneyPriorities.Validation.EmergencyFundMonthsRange";

        /// <summary>A submitted Priority Walk broke the <c>MonthlyExpensesRequired</c> rule.</summary>
        public const string MonthlyExpensesRequired = "MoneyPriorities.Validation.MonthlyExpensesRequired";

        /// <summary>A submitted Priority Walk broke the <c>MonthlyExpensesRange</c> rule.</summary>
        public const string MonthlyExpensesRange = "MoneyPriorities.Validation.MonthlyExpensesRange";

        /// <summary>A submitted Priority Walk broke the <c>HasFullEmployerMatchRequired</c> rule.</summary>
        public const string HasFullEmployerMatchRequired = "MoneyPriorities.Validation.HasFullEmployerMatchRequired";

        /// <summary>A submitted Priority Walk broke the <c>EmployerMatchPercentRequired</c> rule.</summary>
        public const string EmployerMatchPercentRequired = "MoneyPriorities.Validation.EmployerMatchPercentRequired";

        /// <summary>A submitted Priority Walk broke the <c>EmployerMatchPercentRange</c> rule.</summary>
        public const string EmployerMatchPercentRange = "MoneyPriorities.Validation.EmployerMatchPercentRange";

        /// <summary>A submitted Priority Walk broke the <c>HorizonYearsRequired</c> rule.</summary>
        public const string HorizonYearsRequired = "MoneyPriorities.Validation.HorizonYearsRequired";

        /// <summary>A submitted Priority Walk broke the <c>HorizonYearsRange</c> rule.</summary>
        public const string HorizonYearsRange = "MoneyPriorities.Validation.HorizonYearsRange";

        /// <summary>A submitted Priority Walk broke the <c>AgeBandRequired</c> rule.</summary>
        public const string AgeBandRequired = "MoneyPriorities.Validation.AgeBandRequired";

        /// <summary>A submitted Priority Walk broke the <c>AtContributionLimitRequired</c> rule.</summary>
        public const string AtContributionLimitRequired = "MoneyPriorities.Validation.AtContributionLimitRequired";
    }

    /// <summary>The keys of the Community Feature's rejections.</summary>
    public static class Community
    {
    }
}
