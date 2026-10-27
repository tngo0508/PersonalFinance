using FluentValidation;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Community;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Features.Community.Validators;

/// <summary>
/// Validator for an Admin's decision on a Strategy: it can be approved or rejected, never set back to Pending.
/// </summary>
internal sealed class CommunityStrategyReviewRequestValidator : AbstractValidator<CommunityStrategyReviewRequestDto>
{
    public CommunityStrategyReviewRequestValidator()
    {
        RuleFor(x => x.Status)
            .Must(status => status is CommunityStrategyStatus.Approved or CommunityStrategyStatus.Rejected)
                .WithErrorCode(ApiErrorKeys.Community.StrategyReviewStatus)
                .WithMessage("A Strategy can only be approved or rejected.");
    }
}
