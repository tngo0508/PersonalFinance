using System.Linq.Expressions;
using FluentValidation;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Community;

namespace PersonalFinance.ApiService.Features.Community.Validators;

/// <summary>
/// Validator for a request to pin a Strategy. <c>NotEmpty</c> also rejects a whitespace-only text, which would be
/// stored empty once trimmed. A money amount is not a rejection: it is flagged for the Admin instead (ADR 0018).
/// Every rule carries its <see cref="ApiErrorKeys.Community"/> key (ADR 0017).
/// </summary>
internal sealed class CommunityStrategyRequestValidator : AbstractValidator<CommunityStrategyRequestDto>
{
    public CommunityStrategyRequestValidator()
    {
        RuleFor(x => x.Category)
            .Must(category => CommunityStrategyLimits.Categories.Contains(category?.Trim(), StringComparer.Ordinal))
                .WithErrorCode(ApiErrorKeys.Community.StrategyCategory)
                .WithMessage("Choose one of the board's categories.");

        Text(x => x.Title, ApiErrorKeys.Community.StrategyTitle, "A title", CommunityStrategyLimits.TitleMaxLength);
        Text(x => x.Tried, ApiErrorKeys.Community.StrategyTried, "What you tried", CommunityStrategyLimits.TextMaxLength);
        Text(x => x.Happened, ApiErrorKeys.Community.StrategyHappened, "What happened",
            CommunityStrategyLimits.TextMaxLength);
        Text(x => x.Keeping, ApiErrorKeys.Community.StrategyKeeping, "What you'd keep doing",
            CommunityStrategyLimits.TextMaxLength);
    }

    /// <summary>A required text of at most <paramref name="max"/> characters, both rules under one key.</summary>
    private void Text(Expression<Func<CommunityStrategyRequestDto, string>> field, string key, string label, int max)
    {
        string message = $"{label} is required, up to {max} characters.";
        RuleFor(field)
            .NotEmpty()
                .WithErrorCode(key)
                .WithMessage(message)
                .WithState(_ => ErrorArgs.Number("max", max))
            .MaximumLength(max)
                .WithErrorCode(key)
                .WithMessage(message)
                .WithState(_ => ErrorArgs.Number("max", max));
    }
}
