using FluentValidation;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Community;

namespace PersonalFinance.ApiService.Features.Community.Validators;

/// <summary>
/// Validator for an Admin's new Poll: a question, an optional description, one of the board's categories, and two to
/// six distinct, non-empty options. Every rule carries its <see cref="ApiErrorKeys.Community"/> key (ADR 0017).
/// </summary>
internal sealed class CommunityPollRequestValidator : AbstractValidator<CommunityPollRequestDto>
{
    public CommunityPollRequestValidator()
    {
        string questionMessage = $"A question is required, up to {CommunityPollLimits.QuestionMaxLength} characters.";
        RuleFor(x => x.Question)
            .NotEmpty()
                .WithErrorCode(ApiErrorKeys.Community.PollQuestion)
                .WithMessage(questionMessage)
                .WithState(_ => ErrorArgs.Number("max", CommunityPollLimits.QuestionMaxLength))
            .MaximumLength(CommunityPollLimits.QuestionMaxLength)
                .WithErrorCode(ApiErrorKeys.Community.PollQuestion)
                .WithMessage(questionMessage)
                .WithState(_ => ErrorArgs.Number("max", CommunityPollLimits.QuestionMaxLength));

        RuleFor(x => x.Description)
            .MaximumLength(CommunityPollLimits.DescriptionMaxLength)
                .WithErrorCode(ApiErrorKeys.Community.PollDescription)
                .WithMessage($"A description is up to {CommunityPollLimits.DescriptionMaxLength} characters.")
                .WithState(_ => ErrorArgs.Number("max", CommunityPollLimits.DescriptionMaxLength));

        RuleFor(x => x.Category)
            .Must(category => CommunityStrategyLimits.Categories.Contains(category?.Trim(), StringComparer.Ordinal))
                .WithErrorCode(ApiErrorKeys.Community.PollCategory)
                .WithMessage("Choose one of the board's categories.");

        RuleFor(x => x.Options)
            .Must(options => options is not null
                             && options.Select(o => o?.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                             is >= CommunityPollLimits.MinOptions and <= CommunityPollLimits.MaxOptions
                             && options.Count <= CommunityPollLimits.MaxOptions)
                .WithErrorCode(ApiErrorKeys.Community.PollOptions)
                .WithMessage($"A poll has {CommunityPollLimits.MinOptions} to {CommunityPollLimits.MaxOptions} different options.")
                .WithState(_ => ErrorArgs.NumberRange(CommunityPollLimits.MinOptions, CommunityPollLimits.MaxOptions));

        string optionMessage = $"Each option is required, up to {CommunityPollLimits.OptionTextMaxLength} characters.";
        RuleForEach(x => x.Options)
            .NotEmpty()
                .WithErrorCode(ApiErrorKeys.Community.PollOptionText)
                .WithMessage(optionMessage)
                .WithState(_ => ErrorArgs.Number("max", CommunityPollLimits.OptionTextMaxLength))
            .MaximumLength(CommunityPollLimits.OptionTextMaxLength)
                .WithErrorCode(ApiErrorKeys.Community.PollOptionText)
                .WithMessage(optionMessage)
                .WithState(_ => ErrorArgs.Number("max", CommunityPollLimits.OptionTextMaxLength));
    }
}
