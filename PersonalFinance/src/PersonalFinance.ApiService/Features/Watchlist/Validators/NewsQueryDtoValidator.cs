using FluentValidation;
using PersonalFinance.Shared.DTOs.Watchlist;

namespace PersonalFinance.ApiService.Features.Watchlist.Validators;

/// <summary>
/// Validator for the news feed's filters: each is optional and at most <see cref="MaxFilterLength"/> characters.
/// </summary>
internal sealed class NewsQueryDtoValidator : AbstractValidator<NewsQueryDto>
{
    public const int MaxFilterLength = 100;

    public NewsQueryDtoValidator()
    {
        RuleFor(x => x.Category).MaximumLength(MaxFilterLength);
        RuleFor(x => x.Source).MaximumLength(MaxFilterLength);
        RuleFor(x => x.Search).MaximumLength(MaxFilterLength);
    }
}
