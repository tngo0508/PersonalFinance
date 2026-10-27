using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.DTOs.AiAssistant;
using PersonalFinance.Shared.DTOs.News;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Web.Models.AiAssistant;

/// <summary>
/// The AI Assistant's fixed text in the request's Language (#381): a fixed reply or summary by its keyed entry, and the
/// disclaimer and budget warning, which the ApiService sends in English. Model-written text is already in the user's
/// Language and is passed through as sent.
/// </summary>
public static class AiAssistantText
{
    /// <summary>Translates a chat response's fixed reply, disclaimer and budget warning in place.</summary>
    public static AiChatResponseDto Localize(IStringLocalizer localizer, AiChatResponseDto response)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(response);
        if (response.FixedText is not null)
        {
            response.Response = ApiErrorMessages.Resolve(localizer, response.FixedText);
        }

        response.Disclaimer = Disclaimer(localizer, response.Disclaimer);
        response.BudgetWarning = BudgetWarning(localizer, response.BudgetWarning);
        return response;
    }

    /// <summary>Translates a News summary's fixed message, disclaimer and budget warning in place.</summary>
    public static NewsArticleSummaryResponseDto Localize(IStringLocalizer localizer,
        NewsArticleSummaryResponseDto response)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(response);
        if (response.FixedText is not null)
        {
            response.Summary = ApiErrorMessages.Resolve(localizer, response.FixedText);
        }

        response.Disclaimer = Disclaimer(localizer, response.Disclaimer);
        response.BudgetWarning = BudgetWarning(localizer, response.BudgetWarning);
        return response;
    }

    private static string? Disclaimer(IStringLocalizer localizer, string? sent) =>
        sent is null ? null : localizer["AiAssistant.Disclaimer"];

    private static string? BudgetWarning(IStringLocalizer localizer, string? sent) =>
        sent is null ? null : localizer["AiAssistant.BudgetWarning"];
}
