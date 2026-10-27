using System.Globalization;
using System.Reflection;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.AccountLifecycle.Validators;
using PersonalFinance.ApiService.Features.AiAssistant;
using PersonalFinance.ApiService.Features.Analysis;
using PersonalFinance.ApiService.Features.Feedback.Validators;
using PersonalFinance.ApiService.Features.GoogleDrive;
using PersonalFinance.ApiService.Features.NetWorth.Validators;
using PersonalFinance.ApiService.Features.Research;
using PersonalFinance.ApiService.Features.SavingsGoals.Validators;
using PersonalFinance.ApiService.Features.Watchlist.Validators;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.AccountDeletion;
using PersonalFinance.Shared.DTOs.AiAssistant;
using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.DTOs.GoogleDrive;
using PersonalFinance.Shared.DTOs.NetWorth;
using PersonalFinance.Shared.DTOs.News;
using PersonalFinance.Shared.DTOs.SavingsGoals;
using PersonalFinance.Shared.DTOs.Watchlist;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Tests.Infrastructure;

/// <summary>
/// For the Features converted in slices E3a and E3b of the localized API errors spec: every <see cref="ApiErrorKeys"/>
/// key of the Feature is produced by some real rejection, and every rejection resolves in English with each placeholder
/// filled by its arguments (spec §2.4).
/// </summary>
public class KeyedRejectionCoverageTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    public static TheoryData<string> Features => new()
    {
        nameof(ApiErrorKeys.Feedback),
        nameof(ApiErrorKeys.AccountLifecycle),
        nameof(ApiErrorKeys.SavingsGoals),
        nameof(ApiErrorKeys.Watchlist),
        nameof(ApiErrorKeys.NetWorth),
        nameof(ApiErrorKeys.AiAssistant),
        nameof(ApiErrorKeys.Research),
        nameof(ApiErrorKeys.Analysis),
        nameof(ApiErrorKeys.GoogleDrive)
    };

    [Theory]
    [MemberData(nameof(Features))]
    public async Task EveryKeyOfTheFeature_IsProducedByARealRejection(string feature)
    {
        HashSet<string> produced = (await RejectionsAsync(feature)).Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);

        List<string> unreached = KeysOf(feature).Where(key => !produced.Contains(key)).ToList();
        List<string> foreign = produced.Where(key => !KeysOf(feature).Contains(key)).ToList();

        Assert.True(unreached.Count == 0, "No rejection produces:\n" + string.Join("\n", unreached));
        Assert.True(foreign.Count == 0, "Not a key of " + feature + ":\n" + string.Join("\n", foreign));
    }

    [Theory]
    [MemberData(nameof(Features))]
    public async Task ResolvingEveryRejectionInEnglish_LeavesNoPlaceholder(string feature)
    {
        List<ApiErrorEntryDto> rejections = await RejectionsAsync(feature);
        using var uiCulture = new UiCultureScope("en");
        CultureInfo formatting = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            foreach (ApiErrorEntryDto entry in rejections)
            {
                Assert.False(TestLocalization.Localizer[entry.Key].ResourceNotFound, entry.Key);
                string text = ApiErrorMessages.Resolve(TestLocalization.Localizer, entry);
                Assert.DoesNotContain("{", text);
                Assert.DoesNotContain("}", text);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = formatting;
        }
    }

    [Fact]
    public void ASavingsGoalAmountLimit_IsAMoneyArgument()
    {
        ApiErrorEntryDto entry = Assert.Single(Failures(new CorrectBalanceRequestValidator(),
            new CorrectBalanceRequestDto { Balance = 10_000_000_000_000_000m }));

        Assert.Equal(ApiErrorKeys.SavingsGoals.BalanceTooLarge, entry.Key);
        ApiErrorArgDto max = entry.Args!["max"];
        Assert.Equal(ApiErrorArgKind.Money, max.Kind);
        Assert.Equal("9999999999999999.99", max.Value);
        Assert.Equal(SupportedCurrencies.Default.Code, max.Currency);
    }

    [Fact]
    public void AMalformedCancelToken_IsOneInvalidLinkError()
    {
        ApiErrorEntryDto entry = Assert.Single(Failures(new CancelDeletionRequestValidator(),
            new CancelDeletionRequest { Token = "" }));

        Assert.Equal(ApiErrorKeys.AccountLifecycle.InvalidCancelLink, entry.Key);
    }

    [Fact]
    public async Task TheDailyAiLimit_NamesItsLimitAsANumberArgument()
    {
        ApiErrorEntryDto entry = await QuotaDenialAsync("user-1", dailyLimit: 0, monthlyBudgetUsd: 10m);

        Assert.Equal(ApiErrorKeys.AiAssistant.DailyLimitReached, entry.Key);
        Assert.Equal(ApiErrorArgKind.Number, entry.Args!["max"].Kind);
        Assert.Equal("0", entry.Args["max"].Value);
    }

    /// <summary>Every rejection the Feature can return: each validator failure plus each service or controller rejection.</summary>
    private static async Task<List<ApiErrorEntryDto>> RejectionsAsync(string feature) => feature switch
    {
        nameof(ApiErrorKeys.AiAssistant) =>
        [
            await Caught(() => AiController().Chat(new AiChatRequestDto { Message = " " })),
            await Caught(() => AiController().Chat(new AiChatRequestDto
                { Message = new string('m', AiAssistantMessages.MaxMessageLength + 1) })),
            await Caught(() => AiController().Summarize(new NewsArticleSummaryRequestDto { Title = "" })),
            await QuotaDenialAsync("anonymous", dailyLimit: 10, monthlyBudgetUsd: 10m),
            await QuotaDenialAsync("user-1", dailyLimit: 0, monthlyBudgetUsd: 10m),
            await QuotaDenialAsync("user-1", dailyLimit: 10, monthlyBudgetUsd: 0m),
            Thrown(ApiErrorKeys.AiAssistant.RateLimited)
        ],
        nameof(ApiErrorKeys.Research) =>
        [
            await Caught(() => new CompaniesController(null!).Suggest("")),
            await Caught(() => new CompaniesController(null!).Get("TOOLONGTICKER")),
            Thrown(ApiErrorKeys.Research.TooManyLookups)
        ],
        nameof(ApiErrorKeys.Analysis) =>
        [
            await Caught(() => AnalysisController().GetSpreadsheetBudgetReport("", fileName: null)),
            await Caught(() => AnalysisController().GetSpreadsheetTransactions(""))
        ],
        nameof(ApiErrorKeys.GoogleDrive) =>
        [
            await Caught(() => DriveController().Explore(new GoogleDriveRequestDto { FolderUrl = "" })),
            await Caught(() => DriveController().Connect(new ConnectGoogleDriveRequestDto { FolderUrl = "" })),
            await Caught(() => DriveController().GetSpreadsheetBudgetReport("", fileName: null)),
            await Caught(() => DriveController().GetSpreadsheetTransactions("")),
            await Caught(() => new DriveConnectionService(new HttpClient(), new ConfigurationBuilder().Build(), null!,
                NullLogger<DriveConnectionService>.Instance).GetFolderFilesAsync(
                new GoogleDriveRequestDto { FolderUrl = "invalid-link" })),
            DriveConnectionService.ConnectionNotFound().ToDto().Errors[0]
        ],
        _ => Rejections(feature)
    };

    private static List<ApiErrorEntryDto> Rejections(string feature) => feature switch
    {
        nameof(ApiErrorKeys.Feedback) =>
        [
            .. Failures(new SubmitFeedbackRequestValidator(),
                new SubmitFeedbackRequest { Category = (FeedbackCategory)99, Message = "", PageUrl = "https://evil.example" },
                new SubmitFeedbackRequest
                {
                    Category = FeedbackCategory.Bug,
                    Message = new string('m', FeedbackLimits.MessageMaxLength + 1),
                    PageUrl = "/" + new string('p', FeedbackLimits.PageUrlMaxLength)
                }),
            .. Failures(new ChangeFeedbackStatusRequestValidator(),
                new ChangeFeedbackStatusRequest { Status = (FeedbackStatus)99 }),
            Thrown(ApiErrorKeys.Feedback.RateLimited)
        ],
        nameof(ApiErrorKeys.AccountLifecycle) =>
        [
            .. Failures(new CancelDeletionRequestValidator(), new CancelDeletionRequest { Token = "<script>" }),
            Thrown(ApiErrorKeys.AccountLifecycle.LastAdmin),
            Thrown(ApiErrorKeys.AccountLifecycle.AlreadyPending)
        ],
        nameof(ApiErrorKeys.SavingsGoals) =>
        [
            .. Failures(new CreateSavingsGoalRequestValidator(),
                new CreateSavingsGoalRequestDto { Name = "", TargetAmount = 0m, CurrentAmount = -1m },
                new CreateSavingsGoalRequestDto
                {
                    Name = new string('n', 201), TargetAmount = 10_000_000_000_000_000m,
                    CurrentAmount = 10_000_000_000_000_000m, Description = new string('d', 1001)
                },
                new CreateSavingsGoalRequestDto { Name = "Trip", TargetAmount = 1m, CurrentAmount = 1.005m }),
            .. Failures(new SaveContributionRequestValidator(new FixedTimeProvider(Now)),
                new SaveContributionRequestDto { Amount = 0m, Date = new DateOnly(2026, 10, 26), Note = new string('n', 201) },
                new SaveContributionRequestDto { Amount = -10_000_000_000_000_000m, Date = new DateOnly(1999, 12, 31) },
                new SaveContributionRequestDto { Amount = 1.005m }),
            .. Failures(new CorrectBalanceRequestValidator(),
                new CorrectBalanceRequestDto { Balance = -1m },
                new CorrectBalanceRequestDto { Balance = 10_000_000_000_000_000m },
                new CorrectBalanceRequestDto { Balance = 1.005m }),
            Thrown(ApiErrorKeys.SavingsGoals.BelowZero),
            Thrown(ApiErrorKeys.SavingsGoals.AdjustmentNotEditable)
        ],
        nameof(ApiErrorKeys.Watchlist) =>
        [
            .. Failures(new SaveArticleRequestDtoValidator(),
                new SaveArticleRequestDto { Title = "", Url = "", Source = "", Category = "" },
                new SaveArticleRequestDto
                {
                    Title = new string('t', SaveArticleRequestDtoValidator.TitleMaxLength + 1),
                    Url = "https://example.test/" + new string('u', SaveArticleRequestDtoValidator.UrlMaxLength),
                    Source = new string('s', SaveArticleRequestDtoValidator.SourceMaxLength + 1),
                    Category = new string('c', SaveArticleRequestDtoValidator.CategoryMaxLength + 1),
                    Notes = new string('n', SaveArticleRequestDtoValidator.NotesMaxLength + 1),
                    ImageUrl = new string('i', SaveArticleRequestDtoValidator.ImageUrlMaxLength + 1)
                })
        ],
        nameof(ApiErrorKeys.NetWorth) => NetWorthRejections(),
        _ => throw new ArgumentOutOfRangeException(nameof(feature), feature, null)
    };

    /// <summary>The Net Worth validator stops at the first broken rule, so each rule is broken on its own.</summary>
    private static List<ApiErrorEntryDto> NetWorthRejections()
    {
        Action<NetWorthProjectionRequestDto>[] breaks =
        [
            r => r.StartingBalance = -1m, r => r.AnnualSavings = -1m, r => r.AnnualIncome = -1m,
            r => r.AnnualSpending = -1m, r => r.AggressiveSavings = -1m, r => r.CurrentAge = 17,
            r => r.RetirementAge = 121, r => r.RetirementAge = 30, r => r.BaseRoi = 51m,
            r => r.ConservativeRoi = 51m, r => r.AggressiveRoi = 51m
        ];

        return breaks.Select(breakIt =>
        {
            var request = new NetWorthProjectionRequestDto
            {
                StartingBalance = 1000m, AnnualSavings = 1000m, AnnualIncome = 50_000m, AnnualSpending = 30_000m,
                AggressiveSavings = 2000m, CurrentAge = 30, RetirementAge = 65, BaseRoi = 7m, ConservativeRoi = 4m,
                AggressiveRoi = 10m
            };
            Assert.Null(NetWorthProjectionValidator.Validate(request));
            breakIt(request);
            return NetWorthProjectionValidator.Validate(request)!;
        }).ToList();
    }

    private static List<ApiErrorEntryDto> Failures<T>(IValidator<T> validator, params T[] requests) =>
        new ValidationResult(requests.SelectMany(request => validator.Validate(request).Errors)).ToApiError().Errors;

    /// <summary>A rejection thrown deep in a service or controller; none of these carries arguments.</summary>
    private static ApiErrorEntryDto Thrown(string key) => new ApiErrorException(key, "English").ToDto().Errors[0];

    private static HashSet<string> KeysOf(string feature) =>
        typeof(ApiErrorKeys).GetNestedType(feature)!
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);

    private static async Task<ApiErrorEntryDto> Caught(Func<Task> call) =>
        (await Assert.ThrowsAsync<ApiErrorException>(call)).ToDto().Errors[0];

    /// <summary>The rejection the AI quota gives <paramref name="userId"/> under the given limits, on an empty usage log.</summary>
    private static async Task<ApiErrorEntryDto> QuotaDenialAsync(string userId, int dailyLimit, decimal monthlyBudgetUsd)
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"KeyedRejectionQuota_{Guid.NewGuid():N}").Options);
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:DailyMessageLimit"] = dailyLimit.ToString(CultureInfo.InvariantCulture),
                ["OpenAI:MonthlyBudgetUsd"] = monthlyBudgetUsd.ToString(CultureInfo.InvariantCulture)
            })
            .Build();

        QuotaCheckResult quota = await new AiQuotaService(db, configuration).CheckQuotaAsync(userId, "chat");

        Assert.False(quota.Allowed);
        return quota.ToRejection(StatusCodes.Status429TooManyRequests).ToDto().Errors[0];
    }

    // The checks under test run before any dependency is used, so none is given.
    private static AiAssistantController AiController() => new(null!, null!, null!);

    private static SpreadsheetAnalysisController AnalysisController() =>
        new(null!, null!, NullLogger<SpreadsheetAnalysisController>.Instance, null!, null!);

    private static GoogleDriveController DriveController() =>
        new(null!, null!, NullLogger<GoogleDriveController>.Instance);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
