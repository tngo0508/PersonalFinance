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

        /// <summary>A submitted Priority Walk named a currency that is not supported.</summary>
        public const string CurrencyUnsupported = "MoneyPriorities.Validation.CurrencyUnsupported";
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

        /// <summary>A comment named a Strategy that does not exist or is not Approved.</summary>
        public const string InvalidStrategy = "Community.Error.InvalidStrategy";

        /// <summary>The liked comment does not exist or is hidden.</summary>
        public const string CommentNotFound = "Community.Error.CommentNotFound";

        /// <summary>A Strategy's category was not one of the board's categories.</summary>
        public const string StrategyCategory = "Community.Validation.StrategyCategory";

        /// <summary>A Strategy's title was empty or longer than the <c>max</c> argument.</summary>
        public const string StrategyTitle = "Community.Validation.StrategyTitle";

        /// <summary>A Strategy's "What I tried" was empty or longer than the <c>max</c> argument.</summary>
        public const string StrategyTried = "Community.Validation.StrategyTried";

        /// <summary>A Strategy's "What happened" was empty or longer than the <c>max</c> argument.</summary>
        public const string StrategyHappened = "Community.Validation.StrategyHappened";

        /// <summary>A Strategy's "What I'd keep doing" was empty or longer than the <c>max</c> argument.</summary>
        public const string StrategyKeeping = "Community.Validation.StrategyKeeping";

        /// <summary>An Admin's review named a status other than Approved or Rejected.</summary>
        public const string StrategyReviewStatus = "Community.Validation.StrategyReviewStatus";
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

        /// <summary>A transaction or budget line named a currency that is not supported.</summary>
        public const string CurrencyUnsupported = "BudgetTracker.Validation.CurrencyUnsupported";
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

        /// <summary>A budget allocation named a currency that is not supported.</summary>
        public const string CurrencyUnsupported = "BudgetPlanner.Validation.CurrencyUnsupported";
    }

    /// <summary>The keys of the Feedback Feature's rejections.</summary>
    public static class Feedback
    {
        /// <summary>The category was not Bug, Idea or Other.</summary>
        public const string CategoryUnknown = "Feedback.Validation.CategoryUnknown";

        /// <summary>The message was empty or only whitespace.</summary>
        public const string MessageRequired = "Feedback.Validation.MessageRequired";

        /// <summary>The message was longer than the <c>max</c> argument.</summary>
        public const string MessageLength = "Feedback.Validation.MessageLength";

        /// <summary>The page was longer than the <c>max</c> argument.</summary>
        public const string PageTooLong = "Feedback.Validation.PageTooLong";

        /// <summary>The page was not a path within the app.</summary>
        public const string PageNotAppRelative = "Feedback.Validation.PageNotAppRelative";

        /// <summary>A Feedback Status was not New, Reviewed or Archived.</summary>
        public const string StatusUnknown = "Feedback.Validation.StatusUnknown";

        /// <summary>The Account sent too much Feedback within the last hour.</summary>
        public const string RateLimited = "Feedback.Error.RateLimited";
    }

    /// <summary>The keys of the Account Lifecycle Feature's rejections.</summary>
    public static class AccountLifecycle
    {
        /// <summary>The only Admin asked to delete their Account.</summary>
        public const string LastAdmin = "AccountLifecycle.Error.LastAdmin";

        /// <summary>The Account already has a pending Deletion Request.</summary>
        public const string AlreadyPending = "AccountLifecycle.Error.AlreadyPending";

        /// <summary>A cancel link was malformed, unknown, used or expired.</summary>
        public const string InvalidCancelLink = "AccountLifecycle.Error.InvalidCancelLink";
    }

    /// <summary>The keys of the Savings Goals Feature's rejections.</summary>
    public static class SavingsGoals
    {
        /// <summary>A goal's name was empty.</summary>
        public const string NameRequired = "SavingsGoals.Validation.NameRequired";

        /// <summary>A goal's name was longer than the <c>max</c> argument.</summary>
        public const string NameTooLong = "SavingsGoals.Validation.NameTooLong";

        /// <summary>A goal's target amount was zero or less.</summary>
        public const string TargetAmountPositive = "SavingsGoals.Validation.TargetAmountPositive";

        /// <summary>A goal's target amount was over the <c>max</c> amount argument.</summary>
        public const string TargetAmountTooLarge = "SavingsGoals.Validation.TargetAmountTooLarge";

        /// <summary>A goal's starting amount was negative.</summary>
        public const string StartingAmountNegative = "SavingsGoals.Validation.StartingAmountNegative";

        /// <summary>A goal's starting amount was over the <c>max</c> amount argument.</summary>
        public const string StartingAmountTooLarge = "SavingsGoals.Validation.StartingAmountTooLarge";

        /// <summary>A goal's starting amount had more than two decimal places.</summary>
        public const string StartingAmountWholeCents = "SavingsGoals.Validation.StartingAmountWholeCents";

        /// <summary>A goal's description was longer than the <c>max</c> argument.</summary>
        public const string DescriptionTooLong = "SavingsGoals.Validation.DescriptionTooLong";

        /// <summary>A Contribution's amount was zero.</summary>
        public const string ContributionAmountZero = "SavingsGoals.Validation.ContributionAmountZero";

        /// <summary>A Contribution's amount was over the <c>max</c> amount argument, either way.</summary>
        public const string ContributionAmountTooLarge = "SavingsGoals.Validation.ContributionAmountTooLarge";

        /// <summary>A Contribution's amount had more than two decimal places.</summary>
        public const string ContributionAmountWholeCents = "SavingsGoals.Validation.ContributionAmountWholeCents";

        /// <summary>A Contribution was dated in the future.</summary>
        public const string ContributionDateFuture = "SavingsGoals.Validation.ContributionDateFuture";

        /// <summary>A Contribution was dated before the year 2000.</summary>
        public const string ContributionDateTooEarly = "SavingsGoals.Validation.ContributionDateTooEarly";

        /// <summary>A Contribution's note was longer than the <c>max</c> argument.</summary>
        public const string NoteTooLong = "SavingsGoals.Validation.NoteTooLong";

        /// <summary>A balance correction named a negative amount.</summary>
        public const string BalanceNegative = "SavingsGoals.Validation.BalanceNegative";

        /// <summary>A balance correction named an amount over the <c>max</c> amount argument.</summary>
        public const string BalanceTooLarge = "SavingsGoals.Validation.BalanceTooLarge";

        /// <summary>A balance correction named an amount with more than two decimal places.</summary>
        public const string BalanceWholeCents = "SavingsGoals.Validation.BalanceWholeCents";

        /// <summary>A Contribution change would leave the goal's saved amount below zero.</summary>
        public const string BelowZero = "SavingsGoals.Error.BelowZero";

        /// <summary>An edit was sent for a balance Adjustment, which can only be deleted.</summary>
        public const string AdjustmentNotEditable = "SavingsGoals.Error.AdjustmentNotEditable";

        /// <summary>A savings goal named a currency that is not supported.</summary>
        public const string CurrencyUnsupported = "SavingsGoals.Validation.CurrencyUnsupported";
    }

    /// <summary>The keys of the Watchlist Feature's rejections.</summary>
    public static class Watchlist
    {
        /// <summary>A saved article had no title.</summary>
        public const string TitleRequired = "Watchlist.Validation.TitleRequired";

        /// <summary>A saved article's title was longer than the <c>max</c> argument.</summary>
        public const string TitleTooLong = "Watchlist.Validation.TitleTooLong";

        /// <summary>A saved article had no address.</summary>
        public const string UrlRequired = "Watchlist.Validation.UrlRequired";

        /// <summary>A saved article's address was longer than the <c>max</c> argument.</summary>
        public const string UrlTooLong = "Watchlist.Validation.UrlTooLong";

        /// <summary>A saved article's address was not an absolute http or https link.</summary>
        public const string UrlNotHttp = "Watchlist.Validation.UrlNotHttp";

        /// <summary>A saved article had no source.</summary>
        public const string SourceRequired = "Watchlist.Validation.SourceRequired";

        /// <summary>A saved article's source was longer than the <c>max</c> argument.</summary>
        public const string SourceTooLong = "Watchlist.Validation.SourceTooLong";

        /// <summary>A saved article had no topic.</summary>
        public const string CategoryRequired = "Watchlist.Validation.CategoryRequired";

        /// <summary>A saved article's topic was longer than the <c>max</c> argument.</summary>
        public const string CategoryTooLong = "Watchlist.Validation.CategoryTooLong";

        /// <summary>A saved article's notes were longer than the <c>max</c> argument.</summary>
        public const string NotesTooLong = "Watchlist.Validation.NotesTooLong";

        /// <summary>A saved article's image address was longer than the <c>max</c> argument.</summary>
        public const string ImageUrlTooLong = "Watchlist.Validation.ImageUrlTooLong";
    }

    /// <summary>The keys of the Net Worth Feature's rejections. The range keys are also the Web form's own messages.</summary>
    public static class NetWorth
    {
        /// <summary>The starting balance was outside 0 to 1 billion.</summary>
        public const string StartingBalanceRange = "NetWorth.Validation.StartingBalanceRange";

        /// <summary>The annual savings were outside 0 to 10 million.</summary>
        public const string AnnualSavingsRange = "NetWorth.Validation.AnnualSavingsRange";

        /// <summary>The annual income was outside 0 to 100 million.</summary>
        public const string AnnualIncomeRange = "NetWorth.Validation.AnnualIncomeRange";

        /// <summary>The annual spending was outside 0 to 10 million.</summary>
        public const string AnnualSpendingRange = "NetWorth.Validation.AnnualSpendingRange";

        /// <summary>The aggressive savings were outside 0 to 10 million.</summary>
        public const string AggressiveSavingsRange = "NetWorth.Validation.AggressiveSavingsRange";

        /// <summary>The current age was outside 18 to 100.</summary>
        public const string CurrentAgeRange = "NetWorth.Validation.CurrentAgeRange";

        /// <summary>The retirement age was outside 18 to 120.</summary>
        public const string RetirementAgeRange = "NetWorth.Validation.RetirementAgeRange";

        /// <summary>The retirement age was not greater than the current age.</summary>
        public const string RetirementAfterCurrentAge = "NetWorth.Validation.RetirementAfterCurrentAge";

        /// <summary>The base ROI was outside 0% to 50%.</summary>
        public const string BaseRoiRange = "NetWorth.Validation.BaseRoiRange";

        /// <summary>The conservative ROI was outside 0% to 50%.</summary>
        public const string ConservativeRoiRange = "NetWorth.Validation.ConservativeRoiRange";

        /// <summary>The aggressive ROI was outside 0% to 50%.</summary>
        public const string AggressiveRoiRange = "NetWorth.Validation.AggressiveRoiRange";

        /// <summary>The inflation was outside 0% to 20%.</summary>
        public const string InflationRange = "NetWorth.Validation.InflationRange";

        /// <summary>The Net Worth parameters named a currency that is not supported.</summary>
        public const string CurrencyUnsupported = "NetWorth.Validation.CurrencyUnsupported";
    }

    /// <summary>The keys of the AI Assistant Feature's rejections.</summary>
    public static class AiAssistant
    {
        /// <summary>A chat message was empty.</summary>
        public const string MessageRequired = "AiAssistant.Validation.MessageRequired";

        /// <summary>A chat message was longer than the <c>max</c> argument.</summary>
        public const string MessageTooLong = "AiAssistant.Validation.MessageTooLong";

        /// <summary>A summary request named no article title.</summary>
        public const string TitleRequired = "AiAssistant.Validation.TitleRequired";

        /// <summary>The request came from no signed-in Account.</summary>
        public const string SignInRequired = "AiAssistant.Error.SignInRequired";

        /// <summary>The Account used its <c>max</c> AI requests for today.</summary>
        public const string DailyLimitReached = "AiAssistant.Error.DailyLimitReached";

        /// <summary>The AI Assistant's monthly budget is spent.</summary>
        public const string MonthlyBudgetReached = "AiAssistant.Error.MonthlyBudgetReached";

        /// <summary>The Account sent too many summary requests in a short time.</summary>
        public const string RateLimited = "AiAssistant.Error.RateLimited";
    }

    /// <summary>The keys of the Research Feature's rejections.</summary>
    public static class Research
    {
        /// <summary>A company search was outside the <c>min</c> to <c>max</c> character arguments.</summary>
        public const string QueryInvalid = "Research.Validation.QueryInvalid";

        /// <summary>A ticker was longer than the <c>max</c> argument or had other characters than letters, digits, dots or dashes.</summary>
        public const string TickerInvalid = "Research.Validation.TickerInvalid";

        /// <summary>The Account looked up too many companies in a short time.</summary>
        public const string TooManyLookups = "Research.Error.TooManyLookups";
    }

    /// <summary>The keys of the Analysis Feature's rejections.</summary>
    public static class Analysis
    {
        /// <summary>A spreadsheet request named neither a file id nor a file name.</summary>
        public const string FileRequired = "Analysis.Validation.FileRequired";

        /// <summary>A spreadsheet request named no file id.</summary>
        public const string FileIdRequired = "Analysis.Validation.FileIdRequired";
    }

    /// <summary>The keys of the Google Drive Feature's rejections.</summary>
    public static class GoogleDrive
    {
        /// <summary>A request named no Google Drive folder.</summary>
        public const string FolderUrlRequired = "GoogleDrive.Validation.FolderUrlRequired";

        /// <summary>The folder link or id could not be read as a Google Drive folder.</summary>
        public const string FolderInvalid = "GoogleDrive.Validation.FolderInvalid";

        /// <summary>A spreadsheet request named neither a file id nor a file name.</summary>
        public const string FileRequired = "GoogleDrive.Validation.FileRequired";

        /// <summary>A spreadsheet request named no file id.</summary>
        public const string FileIdRequired = "GoogleDrive.Validation.FileIdRequired";

        /// <summary>The connection does not exist or belongs to another user.</summary>
        public const string ConnectionNotFound = "GoogleDrive.Error.ConnectionNotFound";

        /// <summary>A Google Drive connection named a currency that is not supported.</summary>
        public const string CurrencyUnsupported = "GoogleDrive.Validation.CurrencyUnsupported";
    }

    /// <summary>The keys of the Currency Converter Feature's rejections.</summary>
    public static class CurrencyConverter
    {
        /// <summary>A conversion named no amount.</summary>
        public const string AmountRequired = "CurrencyConverter.Validation.AmountRequired";

        /// <summary>A conversion's amount was below zero.</summary>
        public const string AmountNegative = "CurrencyConverter.Validation.AmountNegative";

        /// <summary>A conversion's From code is not a convertible currency.</summary>
        public const string FromUnsupported = "CurrencyConverter.Validation.FromUnsupported";

        /// <summary>A conversion's To code is not a convertible currency.</summary>
        public const string ToUnsupported = "CurrencyConverter.Validation.ToUnsupported";

        /// <summary>A conversion's date was before 2015-01-01.</summary>
        public const string DateTooEarly = "CurrencyConverter.Validation.DateTooEarly";

        /// <summary>A conversion's date was after today (UTC).</summary>
        public const string DateInFuture = "CurrencyConverter.Validation.DateInFuture";
    }
}
