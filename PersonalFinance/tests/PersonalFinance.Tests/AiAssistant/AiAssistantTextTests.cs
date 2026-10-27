using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.AiAssistant;
using PersonalFinance.Shared.DTOs.News;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Models.AiAssistant;

namespace PersonalFinance.Tests.AiAssistant;

/// <summary>
/// The Web app shows the AI Assistant's fixed text in the reader's Language (#381): a fixed reply or summary by its
/// keyed entry, and the disclaimer and budget warning by their own keys. Model-written text passes through as sent.
/// </summary>
public class AiAssistantTextTests
{
    [Fact]
    public void Chat_FixedReply_DisclaimerAndWarning_AreShownInVietnamese()
    {
        using var culture = new RequestCultureScope("vi-VN");
        var response = new AiChatResponseDto
        {
            Response = AiAssistantMessages.GreetingAcknowledgement,
            FixedText = new ApiErrorEntryDto
            {
                Key = ResultMessageKeys.AiAssistant.Greeting, Message = AiAssistantMessages.GreetingAcknowledgement
            },
            Disclaimer = AiAssistantMessages.Disclaimer,
            BudgetWarning = AiAssistantMessages.BudgetWarning
        };

        AiAssistantText.Localize(TestLocalization.Localizer, response);

        Assert.Equal(TestLocalization.Vietnamese(ResultMessageKeys.AiAssistant.Greeting), response.Response);
        Assert.Equal(TestLocalization.Vietnamese("AiAssistant.Disclaimer"), response.Disclaimer);
        Assert.Equal(TestLocalization.Vietnamese("AiAssistant.BudgetWarning"), response.BudgetWarning);
    }

    [Fact]
    public void Chat_ModelReply_IsPassedThrough_AndAbsentNoticesStayAbsent()
    {
        using var culture = new RequestCultureScope("vi-VN");
        var response = new AiChatResponseDto { Response = "Ngân sách của bạn ổn." };

        AiAssistantText.Localize(TestLocalization.Localizer, response);

        Assert.Equal("Ngân sách của bạn ổn.", response.Response);
        Assert.Null(response.Disclaimer);
        Assert.Null(response.BudgetWarning);
    }

    [Fact]
    public void Summary_FixedMessage_IsShownInVietnamese_AndAModelSummaryIsNot()
    {
        using var culture = new RequestCultureScope("vi-VN");
        var unavailable = new NewsArticleSummaryResponseDto
        {
            Summary = "AI summarization is currently unavailable.",
            FixedText = new ApiErrorEntryDto
            {
                Key = ResultMessageKeys.AiAssistant.SummaryUnavailable, Message = "AI summarization is currently unavailable."
            },
            Disclaimer = AiAssistantMessages.Disclaimer
        };
        var written = new NewsArticleSummaryResponseDto { Summary = "Fed giữ nguyên lãi suất." };

        AiAssistantText.Localize(TestLocalization.Localizer, unavailable);
        AiAssistantText.Localize(TestLocalization.Localizer, written);

        Assert.Equal(TestLocalization.Vietnamese(ResultMessageKeys.AiAssistant.SummaryUnavailable), unavailable.Summary);
        Assert.Equal(TestLocalization.Vietnamese("AiAssistant.Disclaimer"), unavailable.Disclaimer);
        Assert.Equal("Fed giữ nguyên lãi suất.", written.Summary);
    }

    [Fact]
    public void TooLong_FormatsTheLimitInThePageCulture()
    {
        using var culture = new RequestCultureScope("vi-VN");
        var response = new AiChatResponseDto
        {
            FixedText = new ApiErrorEntryDto
            {
                Key = ResultMessageKeys.AiAssistant.TooLong,
                Message = "Your message is too long. Keep it under {max} characters.",
                Args = new Dictionary<string, ApiErrorArgDto>
                {
                    ["max"] = new() { Kind = ApiErrorArgKind.Number, Value = "2000" }
                }
            }
        };

        AiAssistantText.Localize(TestLocalization.Localizer, response);

        Assert.Contains("2.000", response.Response, StringComparison.Ordinal);
        Assert.DoesNotContain("{max}", response.Response, StringComparison.Ordinal);
    }
}
