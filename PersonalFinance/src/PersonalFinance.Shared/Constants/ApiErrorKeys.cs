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
        /// <summary>A vote named no poll.</summary>
        public const string PollCodeRequired = "Community.Validation.PollCodeRequired";

        /// <summary>A vote named no option.</summary>
        public const string OptionIdRequired = "Community.Validation.OptionIdRequired";

        /// <summary>The poll does not exist or is no longer active.</summary>
        public const string PollNotFound = "Community.Error.PollNotFound";

        /// <summary>The voted option does not belong to the poll.</summary>
        public const string InvalidOption = "Community.Error.InvalidOption";

        /// <summary>A comment's text was empty.</summary>
        public const string CommentEmpty = "Community.Validation.CommentEmpty";

        /// <summary>A comment's text was longer than the limit, given as the <c>max</c> argument.</summary>
        public const string CommentTooLong = "Community.Validation.CommentTooLong";

        /// <summary>A comment named a poll that does not exist.</summary>
        public const string InvalidPoll = "Community.Error.InvalidPoll";

        /// <summary>A comment named a story that does not exist or is hidden.</summary>
        public const string InvalidStory = "Community.Error.InvalidStory";

        /// <summary>The liked comment does not exist or is hidden.</summary>
        public const string CommentNotFound = "Community.Error.CommentNotFound";

        /// <summary>The liked story does not exist or is hidden.</summary>
        public const string StoryNotFound = "Community.Error.StoryNotFound";

        /// <summary>A story's title was empty or longer than the <c>max</c> argument.</summary>
        public const string StoryTitle = "Community.Validation.StoryTitle";

        /// <summary>A story's summary was empty or longer than the <c>max</c> argument.</summary>
        public const string StorySummary = "Community.Validation.StorySummary";

        /// <summary>A story's body was shorter than the <c>min</c> argument.</summary>
        public const string StoryBodyMin = "Community.Validation.StoryBodyMin";

        /// <summary>A story's body was longer than the <c>max</c> argument.</summary>
        public const string StoryBodyMax = "Community.Validation.StoryBodyMax";

        /// <summary>A story's category was empty or longer than the <c>max</c> argument.</summary>
        public const string StoryCategory = "Community.Validation.StoryCategory";
    }

    /// <summary>The keys of the Budget Tracker Feature's rejections.</summary>
    public static class BudgetTracker
    {
        /// <summary>A month request named a year outside the <c>min</c> to <c>max</c> arguments.</summary>
        public const string YearRange = "BudgetTracker.Validation.YearRange";

        /// <summary>A month request named a month outside the <c>min</c> to <c>max</c> arguments.</summary>
        public const string MonthRange = "BudgetTracker.Validation.MonthRange";

        /// <summary>A planned amount was outside the <c>min</c> to <c>max</c> amount arguments.</summary>
        public const string PlannedAmountRange = "BudgetTracker.Validation.PlannedAmountRange";

        /// <summary>A planned amount had more than two decimal places.</summary>
        public const string PlannedAmountWholeCents = "BudgetTracker.Validation.PlannedAmountWholeCents";

        /// <summary>An Actual Override was outside the <c>min</c> to <c>max</c> amount arguments.</summary>
        public const string ActualOverrideRange = "BudgetTracker.Validation.ActualOverrideRange";

        /// <summary>An Actual Override had more than two decimal places.</summary>
        public const string ActualOverrideWholeCents = "BudgetTracker.Validation.ActualOverrideWholeCents";

        /// <summary>A category's name was empty.</summary>
        public const string CategoryNameRequired = "BudgetTracker.Validation.CategoryNameRequired";

        /// <summary>A category's name was longer than the <c>max</c> argument.</summary>
        public const string CategoryNameTooLong = "BudgetTracker.Validation.CategoryNameTooLong";

        /// <summary>A category's kind was neither Expense nor Income.</summary>
        public const string CategoryKindInvalid = "BudgetTracker.Validation.CategoryKindInvalid";

        /// <summary>A category's sort order was negative.</summary>
        public const string SortOrderNegative = "BudgetTracker.Validation.SortOrderNegative";

        /// <summary>A transaction had no date.</summary>
        public const string TransactionDateRequired = "BudgetTracker.Validation.TransactionDateRequired";

        /// <summary>A transaction's amount was outside the <c>min</c> to <c>max</c> amount arguments.</summary>
        public const string TransactionAmountRange = "BudgetTracker.Validation.TransactionAmountRange";

        /// <summary>A transaction's amount had more than two decimal places.</summary>
        public const string TransactionAmountWholeCents = "BudgetTracker.Validation.TransactionAmountWholeCents";

        /// <summary>A transaction's description was longer than the <c>max</c> argument.</summary>
        public const string DescriptionTooLong = "BudgetTracker.Validation.DescriptionTooLong";

        /// <summary>A transaction named no category.</summary>
        public const string CategoryRequired = "BudgetTracker.Validation.CategoryRequired";

        /// <summary>A Data Source preference named an unknown source.</summary>
        public const string DataSourceInvalid = "BudgetTracker.Validation.DataSourceInvalid";

        /// <summary>A sheet or line was addressed with a year or month outside the supported range.</summary>
        public const string InvalidYearOrMonth = "BudgetTracker.Error.InvalidYearOrMonth";

        /// <summary>The category does not exist, belongs to another user, or is archived where that is not allowed.</summary>
        public const string CategoryNotFound = "BudgetTracker.Error.CategoryNotFound";

        /// <summary>The transaction does not exist or belongs to another user.</summary>
        public const string TransactionNotFound = "BudgetTracker.Error.TransactionNotFound";

        /// <summary>A category of the same kind already has the <c>name</c> argument as its name.</summary>
        public const string DuplicateCategory = "BudgetTracker.Error.DuplicateCategory";

        /// <summary>A category update tried to change its kind.</summary>
        public const string CategoryKindImmutable = "BudgetTracker.Error.CategoryKindImmutable";

        /// <summary>A transaction was given to an archived category.</summary>
        public const string ArchivedCategory = "BudgetTracker.Error.ArchivedCategory";
    }

    /// <summary>The keys of the Budget Planner Feature's rejections.</summary>
    public static class BudgetPlanner
    {
        /// <summary>The monthly income was outside the <c>min</c> to <c>max</c> amount arguments.</summary>
        public const string MonthlyIncomeRange = "BudgetPlanner.Validation.MonthlyIncomeRange";

        /// <summary>A category's allocation was outside the <c>min</c> to <c>max</c> amount arguments.</summary>
        public const string AllocationRange = "BudgetPlanner.Validation.AllocationRange";

        /// <summary>A category's allocation had more than two decimal places.</summary>
        public const string AllocationWholeCents = "BudgetPlanner.Validation.AllocationWholeCents";
    }
}
