using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.MoneyPriorities.Domain;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.MoneyPriorities;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Tests.Web.Localization;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models.MoneyPriorities;
using Refit;

namespace PersonalFinance.Tests.MoneyPriorities;

/// <summary>
/// The Money Priorities page answers in the request's Language: the Web controller's own wording, leaf labels by id,
/// the disclaimers and Purpose Note messages by their fixed keys and the note's state, and validation messages by the
/// keys of the ApiService's <see cref="ApiErrorDto"/>. A leaf label it has no key for passes through as written.
/// </summary>
public class MoneyPrioritiesLocalizationTests
{
    private const string English = """
        {
          "MoneyPriorities.Title": "Money Priorities",
          "MoneyPriorities.Message.Unavailable": "We couldn't load Money Priorities",
          "MoneyPriorities.Note.Disclaimer": "Not advice.",
          "MoneyPriorities.Note.Unavailable": "The note is not available right now.",
          "MoneyPriorities.Note.QuotaReached": "You've used today's notes.",
          "MoneyPriorities.Leaf.L1": "Pay down the high-interest debt first",
          "MoneyPriorities.Result.HeadlineLeaf": "{leaf}.",
          "MoneyPriorities.Result.EstimateDisclaimer": "Estimates only.",
          "MoneyPriorities.Note.Invite": "Add a purpose.",
          "MoneyPriorities.Note.Refused": "We couldn't write a note.",
          "MoneyPriorities.Message.NoAnswers": "Answer the questions, then try again.",
          "MoneyPriorities.Message.CheckAnswers": "Check your answers, then try again.",
          "MoneyPriorities.Validation.DebtBalanceRequired": "Enter the balance of that debt.",
          "MoneyPriorities.Validation.MonthlyAmountRange": "Enter a monthly amount over {min} and at most {max}."
        }
        """;

    private const string Vietnamese = """
        {
          "MoneyPriorities.Title": "Ưu tiên tiền bạc",
          "MoneyPriorities.Message.Unavailable": "Không thể tải Ưu tiên tiền bạc",
          "MoneyPriorities.Note.Disclaimer": "Không phải lời khuyên.",
          "MoneyPriorities.Note.Unavailable": "Ghi chú hiện chưa có.",
          "MoneyPriorities.Note.QuotaReached": "Bạn đã dùng hết ghi chú hôm nay.",
          "AiAssistant.Error.DailyLimitReached": "Bạn đã dùng hết {max} yêu cầu AI hôm nay.",
          "MoneyPriorities.Leaf.L1": "Trả nợ lãi cao trước",
          "MoneyPriorities.Result.HeadlineLeaf": "{leaf}.",
          "MoneyPriorities.Result.EstimateDisclaimer": "Chỉ là ước tính.",
          "MoneyPriorities.Note.Invite": "Thêm mục đích.",
          "MoneyPriorities.Note.Refused": "Không thể viết ghi chú.",
          "MoneyPriorities.Message.NoAnswers": "Hãy trả lời các câu hỏi rồi thử lại.",
          "MoneyPriorities.Message.CheckAnswers": "Hãy kiểm tra câu trả lời rồi thử lại.",
          "MoneyPriorities.Validation.DebtBalanceRequired": "Nhập số dư của khoản nợ đó.",
          "MoneyPriorities.Validation.MonthlyAmountRange": "Nhập số tiền hằng tháng trên {min} và tối đa {max}."
        }
        """;

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Index_WhenTheApiFails_ShowsTheUnavailableStateInTheRequestLanguage()
    {
        using var culture = new UiCultureScope("vi");
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> _) = ApiProxy.Create<IMoneyPrioritiesApi>();
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await controller.Index();

        UnavailableStateViewModel model =
            Assert.IsType<UnavailableStateViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("Ưu tiên tiền bạc", model.PageTitle);
        Assert.Equal("Không thể tải Ưu tiên tiền bạc", model.Heading);
    }

    [Fact]
    public async Task Index_CarriesThePurposeNoteDisclaimerAndUnavailableMessage_InTheRequestLanguage()
    {
        using var culture = new UiCultureScope("vi");
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Returns(nameof(IMoneyPrioritiesApi.GetTreeAsync), new PriorityTreeDiagramDto());
        proxy.Returns(nameof(IMoneyPrioritiesApi.GetRecentWalksAsync), new List<PriorityWalkSummaryDto>());
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await controller.Index();

        MoneyPrioritiesViewModel model = Assert.IsType<MoneyPrioritiesViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("Không phải lời khuyên.", model.PurposeNoteDisclaimer);
        Assert.Equal("Ghi chú hiện chưa có.", model.PurposeNoteUnavailable);
    }

    [Fact]
    public async Task Submit_ReturnsTheLeafLabelsAndTheEstimateDisclaimer_InTheRequestLanguage()
    {
        using var culture = new UiCultureScope("vi");
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Returns(nameof(IMoneyPrioritiesApi.SubmitWalkAsync), EnglishWalk("L1"));
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await controller.Submit(new SubmitPriorityWalkRequest { MonthlyAmount = 500m, DebtApr = 22m });

        PriorityWalkResultDto walk =
            Assert.IsType<MoneyPrioritiesResultViewModel>(Assert.IsType<JsonResult>(result).Value).Walk;
        Assert.Equal("Trả nợ lãi cao trước", walk.LeafLabel);
        Assert.Equal("Trả nợ lãi cao trước.", walk.HeadlineLeafLabel);
        Assert.Equal("Chỉ là ước tính.", walk.Disclaimer);
    }

    [Fact]
    public async Task Walk_WithALeafThePageHasNoKeyFor_KeepsTheApiServiceLabel()
    {
        using var culture = new UiCultureScope("vi");
        PriorityWalkResultDto english = EnglishWalk("L9");
        english.Disclaimer = "A disclaimer the page does not know.";
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Returns(nameof(IMoneyPrioritiesApi.GetWalkAsync), english);
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await controller.Walk(3);

        PriorityWalkResultDto walk =
            Assert.IsType<MoneyPrioritiesResultViewModel>(Assert.IsType<JsonResult>(result).Value).Walk;
        Assert.Equal("A leaf from a later tree", walk.LeafLabel);
        Assert.Equal("A leaf from a later tree.", walk.HeadlineLeafLabel);
        Assert.Equal("Chỉ là ước tính.", walk.Disclaimer);
    }

    [Fact]
    public async Task Recent_ReturnsEachLeafLabelInTheRequestLanguage()
    {
        using var culture = new UiCultureScope("vi");
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Returns(nameof(IMoneyPrioritiesApi.GetRecentWalksAsync), new List<PriorityWalkSummaryDto>
        {
            new() { WalkId = 2, LeafId = "L1", LeafLabel = "Pay down the high-interest debt first" },
            new() { WalkId = 1, LeafId = "L9", LeafLabel = "A leaf from a later tree" }
        });
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await controller.Recent();

        List<PriorityWalkSummaryDto> walks =
            Assert.IsType<List<PriorityWalkSummaryDto>>(Assert.IsType<JsonResult>(result).Value);
        Assert.Equal(["Trả nợ lãi cao trước", "A leaf from a later tree"], walks.Select(w => w.LeafLabel));
    }

    [Theory]
    [InlineData(PurposeNoteState.NoPurpose, "Thêm mục đích.")]
    [InlineData(PurposeNoteState.Unavailable, "Ghi chú hiện chưa có.")]
    [InlineData(PurposeNoteState.QuotaReached, "Bạn đã dùng hết ghi chú hôm nay.")]
    [InlineData(PurposeNoteState.Refused, "Không thể viết ghi chú.")]
    public async Task Note_ReturnsTheStateMessageAndDisclaimer_InTheRequestLanguage(PurposeNoteState state,
        string expected)
    {
        using var culture = new UiCultureScope("vi");
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Returns(nameof(IMoneyPrioritiesApi.WritePurposeNoteAsync), new PriorityWalkPurposeNoteDto
        {
            WalkId = 7, State = state, Message = "whatever", Disclaimer = "whatever"
        });
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await controller.Note(7);

        PriorityWalkPurposeNoteDto note =
            Assert.IsType<PriorityWalkPurposeNoteDto>(Assert.IsType<JsonResult>(result).Value);
        Assert.Equal(expected, note.Message);
        Assert.Equal("Không phải lời khuyên.", note.Disclaimer);
    }

    [Fact]
    public async Task Note_QuotaReached_SaysWhichLimit_InTheRequestLanguage()
    {
        using var culture = new UiCultureScope("vi");
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Returns(nameof(IMoneyPrioritiesApi.WritePurposeNoteAsync), new PriorityWalkPurposeNoteDto
        {
            WalkId = 7,
            State = PurposeNoteState.QuotaReached,
            Message = "You have reached the daily limit of 30 messages. Please try again tomorrow.",
            Reason = new ApiErrorEntryDto
            {
                Key = "AiAssistant.Error.DailyLimitReached",
                Message = "You have reached the daily limit of 30 messages. Please try again tomorrow.",
                Args = new Dictionary<string, ApiErrorArgDto>
                    { ["max"] = new() { Kind = ApiErrorArgKind.Number, Value = "30" } }
            }
        });
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await controller.Note(7);

        PriorityWalkPurposeNoteDto note =
            Assert.IsType<PriorityWalkPurposeNoteDto>(Assert.IsType<JsonResult>(result).Value);
        Assert.Equal("Bạn đã dùng hết 30 yêu cầu AI hôm nay.", note.Message);
    }

    [Fact]
    public async Task Note_Written_KeepsTheAiWrittenNoteAsWritten()
    {
        using var culture = new UiCultureScope("vi");
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Returns(nameof(IMoneyPrioritiesApi.WritePurposeNoteAsync), new PriorityWalkPurposeNoteDto
        {
            WalkId = 7, State = PurposeNoteState.Written, Note = "It keeps your trip safe.",
            Disclaimer = AiAssistantMessages.Disclaimer
        });
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await controller.Note(7);

        PriorityWalkPurposeNoteDto note =
            Assert.IsType<PriorityWalkPurposeNoteDto>(Assert.IsType<JsonResult>(result).Value);
        Assert.Equal("It keeps your trip safe.", note.Note);
        Assert.Null(note.Message);
        Assert.Equal([nameof(IMoneyPrioritiesApi.WritePurposeNoteAsync)], proxy.Calls); // written once, not retried
    }

    [Fact]
    public async Task Submit_WhenTheApiRejectsTheAnswers_ReturnsEachMessageInTheRequestLanguage()
    {
        using var culture = new UiCultureScope("vi");
        string body = KeyedBody(
            Entry(ApiErrorKeys.MoneyPriorities.DebtBalanceRequired, MoneyPrioritiesMessages.DebtBalanceRequired,
                "DebtBalance"),
            Entry("MoneyPriorities.Validation.ALaterRule", "A rule the page does not know.", "DebtApr"));
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Throws(nameof(IMoneyPrioritiesApi.SubmitWalkAsync), await ApiExceptionAsync(HttpStatusCode.BadRequest, body));
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await controller.Submit(new SubmitPriorityWalkRequest { MonthlyAmount = 500m, DebtApr = 22m });

        MoneyPrioritiesErrorsViewModel errors = ErrorsOf(result);
        Assert.Empty(errors.Errors);
        Assert.Equal(["Nhập số dư của khoản nợ đó."], errors.FieldErrors["DebtBalance"]);
        Assert.Equal(["A rule the page does not know."], errors.FieldErrors["DebtApr"]);
        Assert.Equal(2, errors.FieldErrors.Count);
    }

    [Fact]
    public async Task Submit_RangeMessage_FormatsItsAmountsWithTheMoneyFormatter()
    {
        using var culture = new UiCultureScope("en");
        CultureInfo formatting = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            string body = KeyedBody(Entry(ApiErrorKeys.MoneyPriorities.MonthlyAmountRange,
                MoneyPrioritiesMessages.MonthlyAmountRange, "MonthlyAmount", MoneyRangeArgs(1_000_000m)));
            (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
            proxy.Throws(nameof(IMoneyPrioritiesApi.SubmitWalkAsync),
                await ApiExceptionAsync(HttpStatusCode.BadRequest, body));
            MoneyPrioritiesController controller = CreateController(api);

            IActionResult result = await controller.Submit(new SubmitPriorityWalkRequest { MonthlyAmount = 0m, DebtApr = 0m });

            Assert.Equal(["Enter a monthly amount over $0 and at most $1,000,000."],
                ErrorsOf(result).FieldErrors["MonthlyAmount"]);
        }
        finally
        {
            CultureInfo.CurrentCulture = formatting;
        }
    }

    [Fact]
    public async Task Fields_KeepsEveryMessageOfAField_InOrder()
    {
        using var culture = new UiCultureScope("vi");
        CultureInfo formatting = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            string body = KeyedBody(
                Entry(ApiErrorKeys.MoneyPriorities.MonthlyAmountRange, MoneyPrioritiesMessages.MonthlyAmountRange,
                    "MonthlyAmount", MoneyRangeArgs(1_000_000m)),
                Entry(ApiErrorKeys.MoneyPriorities.WholeCents, MoneyPrioritiesMessages.WholeCents, "MonthlyAmount"));
            (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
            proxy.Throws(nameof(IMoneyPrioritiesApi.SubmitWalkAsync),
                await ApiExceptionAsync(HttpStatusCode.BadRequest, body));
            MoneyPrioritiesController controller = CreateController(api);

            IActionResult result = await controller.Submit(new SubmitPriorityWalkRequest { MonthlyAmount = 0m, DebtApr = 0m });

            // WholeCents has no key in this test catalog, so it keeps the ApiService's English message.
            Assert.Equal(["Nhập số tiền hằng tháng trên $0 và tối đa $1,000,000.", MoneyPrioritiesMessages.WholeCents],
                ErrorsOf(result).FieldErrors["MonthlyAmount"]);
        }
        finally
        {
            CultureInfo.CurrentCulture = formatting;
        }
    }

    [Fact]
    public async Task Submit_WithNoBody_AnswersInTheRequestLanguage()
    {
        using var culture = new UiCultureScope("vi");
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await controller.Submit(null);

        Assert.Equal(["Hãy trả lời các câu hỏi rồi thử lại."], ErrorsOf(result).Errors);
        Assert.Empty(proxy.Calls);
    }

    [Fact]
    public async Task Submit_WhenThe400IsNotTheValidationShape_AnswersInTheRequestLanguage()
    {
        using var culture = new UiCultureScope("vi");
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Throws(nameof(IMoneyPrioritiesApi.SubmitWalkAsync),
            await ApiExceptionAsync(HttpStatusCode.BadRequest, """{"title":"bad","errors":{"$.ageBand":["bad"]}}"""));
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await controller.Submit(new SubmitPriorityWalkRequest { MonthlyAmount = 500m, DebtApr = 0m });

        Assert.Equal(["Hãy kiểm tra câu trả lời rồi thử lại."], ErrorsOf(result).Errors);
    }

    [Fact]
    public void EveryLeafOfTheTree_HasAnEnglishLabelMatchingTheApiService()
    {
        using var culture = new UiCultureScope("en");
        foreach ((string leafId, string english) in TreeLeaves())
        {
            Assert.Equal(english, MoneyPrioritiesText.LeafLabel(TestLocalization.Localizer, leafId, "missing"));
        }
    }

    private static IEnumerable<(string LeafId, string Label)> TreeLeaves() =>
        PriorityTreeV1.Tree.Nodes.Where(n => n.Kind == PriorityNodeKind.Leaf).Select(n => (n.Id, n.Label));

    private static MoneyPrioritiesErrorsViewModel ErrorsOf(IActionResult result) =>
        Assert.IsType<MoneyPrioritiesErrorsViewModel>(Assert.IsType<BadRequestObjectResult>(result).Value);

    private static ApiErrorEntryDto Entry(string key, string message, string? field,
        Dictionary<string, ApiErrorArgDto>? args = null) =>
        new() { Key = key, Message = message, Field = field, Args = args };

    private static string KeyedBody(params ApiErrorEntryDto[] entries) =>
        JsonSerializer.Serialize(new ApiErrorDto { Errors = [.. entries] }, WebJson);

    private static Dictionary<string, ApiErrorArgDto> MoneyRangeArgs(decimal max) => new()
    {
        ["min"] = new ApiErrorArgDto { Kind = ApiErrorArgKind.Money, Value = "0", Currency = "USD" },
        ["max"] = new ApiErrorArgDto
        {
            Kind = ApiErrorArgKind.Money, Value = max.ToString(CultureInfo.InvariantCulture), Currency = "USD"
        }
    };

    private static async Task<ApiException> ApiExceptionAsync(HttpStatusCode statusCode, string content)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.local/api/MoneyPriorities/walks");
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

        return await ApiException.Create(request, HttpMethod.Post, response, new RefitSettings());
    }

    private static PriorityWalkResultDto EnglishWalk(string leafId)
    {
        string label = leafId == "L1" ? "Pay down the high-interest debt first" : "A leaf from a later tree";
        return new PriorityWalkResultDto
        {
            WalkId = 3,
            LeafId = leafId,
            LeafLabel = label,
            HeadlineLeafLabel = label + ".",
            Disclaimer = MoneyPrioritiesMessages.EstimateDisclaimer,
            PathNodeIds = ["S", "Q1", leafId],
            Tree = new PriorityTreeDto
            {
                Version = 1,
                Nodes =
                [
                    new PriorityTreeNodeDto { Id = "S", Kind = PriorityNodeKind.Start, Label = "Extra money each month" },
                    new PriorityTreeNodeDto { Id = "Q1", Kind = PriorityNodeKind.Question, Label = "Debt with APR of 7% or more?" },
                    new PriorityTreeNodeDto { Id = leafId, Kind = PriorityNodeKind.Leaf, Label = label }
                ],
                Edges =
                [
                    new PriorityTreeEdgeDto { From = "S", To = "Q1" },
                    new PriorityTreeEdgeDto { From = "Q1", To = leafId, Label = "Yes" }
                ]
            }
        };
    }

    private static MoneyPrioritiesController CreateController(IMoneyPrioritiesApi api)
    {
        var controller = new MoneyPrioritiesController(api, NullLogger<MoneyPrioritiesController>.Instance,
            LocalizationTestCatalog.Localizer(English, Vietnamese), new InMemoryUserPreferenceService());
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, "priorities-user") }, "TestAuthType"))
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
        return controller;
    }
}
