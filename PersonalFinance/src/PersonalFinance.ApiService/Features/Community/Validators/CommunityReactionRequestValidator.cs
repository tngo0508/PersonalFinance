using FluentValidation;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Community;

namespace PersonalFinance.ApiService.Features.Community.Validators;

/// <summary>
/// Validator for a Reaction toggle: a known kind, given to exactly one Strategy or one Comment. Whether the target
/// exists and can be seen is the Service's to check.
/// </summary>
internal sealed class CommunityReactionRequestValidator : AbstractValidator<CommunityReactionRequestDto>
{
    public CommunityReactionRequestValidator()
    {
        RuleFor(x => x.Kind)
            .IsInEnum()
                .WithErrorCode(ApiErrorKeys.Community.ReactionKind)
                .WithMessage("A Reaction is Helpful or Cheering you on.");

        RuleFor(x => x)
            .Must(r => (r.StrategyId is > 0) != (r.CommentId is > 0))
                .WithErrorCode(ApiErrorKeys.Community.ReactionTarget)
                .WithMessage("React to one Strategy or one Comment.")
                .OverridePropertyName("Target");
    }
}
