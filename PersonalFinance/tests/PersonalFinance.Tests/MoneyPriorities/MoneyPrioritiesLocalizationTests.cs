using System.Globalization;
using System.Net;
using System.Reflection;
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
/// The Money Priorities page answers in the request's Language: the Web controller's own wording, and the ApiService's
/// fixed wording it recognizes (leaf labels by id, the disclaimers, Purpose Note messages, validation messages), which
/// it swaps for the translation. Text it does not recognize passes through as the ApiService wrote it.
/// </summary>
public class MoneyPrioritiesLocalizationTests
{
    private const string English = """
        {
          "MoneyPriorities.Title": "Money Priorities",
          "MoneyPriorities.Message.Unavailable": "We couldn't load Money Priorities",
          "MoneyPriorities.Note.Disclaimer": "Not advice.",
          "MoneyPriorities.Note.Unavailable": "The note is not available right now.",
          "MoneyPriorities.Leaf.L1": "Pay down the high-interest debt first",
          "MoneyPriorities.Leaf.Headline": "{leaf}.",
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
          "MoneyPriorities.Leaf.L1": "Trả nợ lãi cao trước",
          "MoneyPriorities.Leaf.Headline": "{leaf}.",
          "MoneyPriorities.Result.EstimateDisclaimer": "Chỉ là ước tính.",
          "MoneyPriorities.Note.Invite": "Thêm mục đích.",
          "MoneyPriorities.Note.Refused": "Không thể viết ghi chú.",
          "MoneyPriorities.Message.NoAnswers": "Hãy trả lời các câu hỏi rồi thử lại.",
          "MoneyPriorities.Message.CheckAnswers": "Hãy kiểm tra câu trả lời rồi thử lại.",
          "MoneyPriorities.Validation.DebtBalanceRequired": "Nhập số dư của khoản nợ đó.",
          "MoneyPriorities.Validation.MonthlyAmountRange": "Nhập số tiền hằng tháng trên {min} và tối đa {max}."
        }
        """;

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

        PriorityWalkResultDto walk = Assert.IsType<PriorityWalkResultDto>(Assert.IsType<JsonResult>(result).Value);
        Assert.Equal("Trả nợ lãi cao trước", walk.LeafLabel);
        Assert.Equal("Trả nợ lãi cao trước.", walk.HeadlineLeafLabel);
        Assert.Equal("Chỉ là ước tính.", walk.Disclaimer);
    }

    [Fact]
    public async Task Walk_WithALeafOrDisclaimerThePageHasNoKeyFor_KeepsTheApiServiceText()
    {
        using var culture = new UiCultureScope("vi");
        PriorityWalkResultDto english = EnglishWalk("L9");
        english.Disclaimer = "A disclaimer the page does not know.";
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Returns(nameof(IMoneyPrioritiesApi.GetWalkAsync), english);
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await controller.Walk(3);

        PriorityWalkResultDto walk = Assert.IsType<PriorityWalkResultDto>(Assert.IsType<JsonResult>(result).Value);
        Assert.Equal("A leaf from a later tree", walk.LeafLabel);
        Assert.Equal("A leaf from a later tree.", walk.HeadlineLeafLabel);
        Assert.Equal("A disclaimer the page does not know.", walk.Disclaimer);
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
    [InlineData(PurposeNoteState.NoPurpose, MoneyPrioritiesMessages.PurposeNoteInvite, "Thêm mục đích.")]
    [InlineData(PurposeNoteState.Unavailable, AiAssistantMessages.PurposeNoteUnavailable, "Ghi chú hiện chưa có.")]
    [InlineData(PurposeNoteState.Refused, AiAssistantMessages.PurposeNoteRefused, "Không thể viết ghi chú.")]
    [InlineData(PurposeNoteState.QuotaReached, "The quota's own message.", "The quota's own message.")]
    public async Task Note_ReturnsTheStateMessageAndDisclaimer_InTheRequestLanguage(PurposeNoteState state,
        string english, string expected)
    {
        using var culture = new UiCultureScope("vi");
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Returns(nameof(IMoneyPrioritiesApi.WritePurposeNoteAsync), new PriorityWalkPurposeNoteDto
        {
            WalkId = 7, State = state, Message = english, Disclaimer = AiAssistantMessages.Disclaimer
        });
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await controller.Note(7);

        PriorityWalkPurposeNoteDto note =
            Assert.IsType<PriorityWalkPurposeNoteDto>(Assert.IsType<JsonResult>(result).Value);
        Assert.Equal(expected, note.Message);
        Assert.Equal("Không phải lời khuyên.", note.Disclaimer);
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
    }

    [Fact]
    public async Task Submit_WhenTheApiRejectsTheAnswers_ReturnsEachMessageInTheRequestLanguage()
    {
        using var culture = new UiCultureScope("vi");
        string body = JsonSerializer.Serialize(new MoneyPrioritiesValidationErrorsDto
        {
            Errors = [MoneyPrioritiesMessages.DebtBalanceRequired, "A rule the page does not know."],
            FieldErrors = new Dictionary<string, List<string>>
            {
                ["DebtBalance"] = [MoneyPrioritiesMessages.DebtBalanceRequired],
                ["DebtApr"] = ["A rule the page does not know."]
            }
        });
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Throws(nameof(IMoneyPrioritiesApi.SubmitWalkAsync), await ApiExceptionAsync(HttpStatusCode.BadRequest, body));
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await controller.Submit(new SubmitPriorityWalkRequest { MonthlyAmount = 500m, DebtApr = 22m });

        MoneyPrioritiesValidationErrorsDto errors = ErrorsOf(result);
        Assert.Equal(["Nhập số dư của khoản nợ đó.", "A rule the page does not know."], errors.Errors);
        Assert.Equal(["Nhập số dư của khoản nợ đó."], errors.FieldErrors["DebtBalance"]);
        Assert.Equal(["A rule the page does not know."], errors.FieldErrors["DebtApr"]);
    }

    [Fact]
    public async Task Submit_RangeMessage_FormatsItsAmountsWithTheMoneyFormatter()
    {
        using var culture = new UiCultureScope("en");
        CultureInfo formatting = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            string body = JsonSerializer.Serialize(new MoneyPrioritiesValidationErrorsDto
            {
                Errors = [MoneyPrioritiesMessages.MonthlyAmountRange],
                FieldErrors = new Dictionary<string, List<string>> { ["MonthlyAmount"] = [MoneyPrioritiesMessages.MonthlyAmountRange] }
            });
            (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
            proxy.Throws(nameof(IMoneyPrioritiesApi.SubmitWalkAsync),
                await ApiExceptionAsync(HttpStatusCode.BadRequest, body));
            MoneyPrioritiesController controller = CreateController(api);

            IActionResult result = await controller.Submit(new SubmitPriorityWalkRequest { MonthlyAmount = 0m, DebtApr = 0m });

            Assert.Equal(["Enter a monthly amount over $0 and at most $1,000,000."], ErrorsOf(result).Errors);
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
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> _) = ApiProxy.Create<IMoneyPrioritiesApi>();
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await controller.Submit(null);

        Assert.Equal(["Hãy trả lời các câu hỏi rồi thử lại."], ErrorsOf(result).Errors);
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

    /// <summary>
    /// Every fixed ApiService message the page translates reads in en.json exactly as the ApiService writes it, so
    /// translating an English reply into English changes nothing.
    /// </summary>
    [Fact]
    public void EveryFixedMessage_ReadsInEnglishExactlyAsTheApiServiceWritesIt()
    {
        using var culture = new UiCultureScope("en");
        CultureInfo formatting = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            IEnumerable<string> messages = typeof(MoneyPrioritiesMessages)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue()!)
                .Concat([AiAssistantMessages.Disclaimer, AiAssistantMessages.PurposeNoteUnavailable,
                    AiAssistantMessages.PurposeNoteRefused]);

            foreach (string message in messages)
            {
                Assert.Equal(message, MoneyPrioritiesText.Message(TestLocalization.Localizer, message));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = formatting;
        }
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

    private static MoneyPrioritiesValidationErrorsDto ErrorsOf(IActionResult result) =>
        Assert.IsType<MoneyPrioritiesValidationErrorsDto>(Assert.IsType<BadRequestObjectResult>(result).Value);

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
            Disclaimer = MoneyPrioritiesMessages.EstimateDisclaimer
        };
    }

    private static MoneyPrioritiesController CreateController(IMoneyPrioritiesApi api)
    {
        var controller = new MoneyPrioritiesController(api, NullLogger<MoneyPrioritiesController>.Instance,
            LocalizationTestCatalog.Localizer(English, Vietnamese));
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
