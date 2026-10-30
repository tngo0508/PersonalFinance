using FluentValidation;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Community;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Features.Community.Validators;

/// <summary>
/// Validator for an Admin's new Challenge: a title and a habit with no money amount in either (a Challenge is about
/// behavior, never money, ADR 0018; unlike a Strategy's flag, this is a rejection), Daily or Weekly, and an end date
/// on or after the start, at most <see cref="CommunityChallengeLimits.MaxDays"/> days in all. Every rule carries its
/// <see cref="ApiErrorKeys.Community"/> key (ADR 0017).
/// </summary>
internal sealed class CommunityChallengeRequestValidator : AbstractValidator<CommunityChallengeRequestDto>
{
    public CommunityChallengeRequestValidator()
    {
        string titleMessage = $"A title is required, up to {CommunityChallengeLimits.TitleMaxLength} characters.";
        RuleFor(x => x.Title)
            .NotEmpty()
                .WithErrorCode(ApiErrorKeys.Community.ChallengeTitle)
                .WithMessage(titleMessage)
                .WithState(_ => ErrorArgs.Number("max", CommunityChallengeLimits.TitleMaxLength))
            .MaximumLength(CommunityChallengeLimits.TitleMaxLength)
                .WithErrorCode(ApiErrorKeys.Community.ChallengeTitle)
                .WithMessage(titleMessage)
                .WithState(_ => ErrorArgs.Number("max", CommunityChallengeLimits.TitleMaxLength));

        string habitMessage = $"A habit is required, up to {CommunityChallengeLimits.HabitMaxLength} characters.";
        RuleFor(x => x.Habit)
            .NotEmpty()
                .WithErrorCode(ApiErrorKeys.Community.ChallengeHabit)
                .WithMessage(habitMessage)
                .WithState(_ => ErrorArgs.Number("max", CommunityChallengeLimits.HabitMaxLength))
            .MaximumLength(CommunityChallengeLimits.HabitMaxLength)
                .WithErrorCode(ApiErrorKeys.Community.ChallengeHabit)
                .WithMessage(habitMessage)
                .WithState(_ => ErrorArgs.Number("max", CommunityChallengeLimits.HabitMaxLength));

        RuleFor(x => x)
            .Must(x => !MoneyAmountRule.ContainsAmount(x.Title, x.Habit))
                .WithName(nameof(CommunityChallengeRequestDto.Habit))
                .WithErrorCode(ApiErrorKeys.Community.ChallengeAmount)
                .WithMessage("A Challenge is about a habit, never an amount of money.");

        RuleFor(x => x.Period)
            .IsInEnum()
                .WithErrorCode(ApiErrorKeys.Community.ChallengePeriod)
                .WithMessage("A Challenge is Daily or Weekly.");

        RuleFor(x => x.EndDate)
            .Must((x, end) => end >= x.StartDate
                              && end.DayNumber - x.StartDate.DayNumber < CommunityChallengeLimits.MaxDays)
                .WithErrorCode(ApiErrorKeys.Community.ChallengeDates)
                .WithMessage($"A Challenge ends on or after its start, and runs at most {CommunityChallengeLimits.MaxDays} days.")
                .WithState(_ => ErrorArgs.Number("max", CommunityChallengeLimits.MaxDays));
    }
}
