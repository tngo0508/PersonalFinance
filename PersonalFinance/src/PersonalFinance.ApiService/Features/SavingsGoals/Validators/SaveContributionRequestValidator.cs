using FluentValidation;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.SavingsGoals;

namespace PersonalFinance.ApiService.Features.SavingsGoals.Validators;

/// <summary>
/// Validator for adding or editing a Contribution: a non-zero amount, a date between 2000 and today, a short note.
/// Whether the change would leave the saved amount below zero needs the goal, so the service checks that. Every rule
/// carries its <see cref="ApiErrorKeys.SavingsGoals"/> key (ADR 0017).
/// </summary>
internal sealed class SaveContributionRequestValidator : AbstractValidator<SaveContributionRequestDto>
{
    public SaveContributionRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.Amount)
            .NotEqual(0m)
                .WithErrorCode(ApiErrorKeys.SavingsGoals.ContributionAmountZero)
                .WithMessage("Contribution amount must not be zero.")
            .Must(amount => Math.Abs(amount) <= ContributionRules.MaxAmount)
                .WithErrorCode(ApiErrorKeys.SavingsGoals.ContributionAmountTooLarge)
                .WithMessage("Contribution amount cannot exceed 9,999,999,999,999,999.99.")
                .WithState(_ => ContributionRules.MaxAmountArgs)
            .Must(amount => decimal.Round(amount, 2) == amount)
                .WithErrorCode(ApiErrorKeys.SavingsGoals.ContributionAmountWholeCents)
                .WithMessage("Contribution amount cannot have more than 2 decimal places.");

        RuleFor(x => x.Date)
            .Must(date => date is null || date.Value <= ContributionRules.Today(timeProvider))
                .WithErrorCode(ApiErrorKeys.SavingsGoals.ContributionDateFuture)
                .WithMessage("Contribution date cannot be in the future.")
            .Must(date => date is null || date.Value >= ContributionRules.EarliestDate)
                .WithErrorCode(ApiErrorKeys.SavingsGoals.ContributionDateTooEarly)
                .WithMessage("Contribution date cannot be before the year 2000.");

        RuleFor(x => x.Note)
            .MaximumLength(ContributionRules.MaxNoteLength)
                .WithErrorCode(ApiErrorKeys.SavingsGoals.NoteTooLong)
                .WithMessage("Note must not exceed 200 characters.")
                .WithState(_ => ErrorArgs.Number("max", ContributionRules.MaxNoteLength));
    }
}
