using FluentValidation;
using PersonalFinance.Shared.DTOs.SavingsGoals;

namespace PersonalFinance.ApiService.Features.SavingsGoals.Validators;

/// <summary>
/// Validator for adding or editing a Contribution: a non-zero amount, a date between 2000 and today, a short note.
/// Whether the change would leave the saved amount below zero needs the goal, so the service checks that.
/// </summary>
internal sealed class SaveContributionRequestValidator : AbstractValidator<SaveContributionRequestDto>
{
    public SaveContributionRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.Amount)
            .NotEqual(0m)
            .WithMessage("Contribution amount must not be zero.")
            .Must(amount => Math.Abs(amount) <= ContributionRules.MaxAmount)
            .WithMessage("Contribution amount cannot exceed 9,999,999,999,999,999.99.")
            .Must(amount => decimal.Round(amount, 2) == amount)
            .WithMessage("Contribution amount cannot have more than 2 decimal places.");

        RuleFor(x => x.Date)
            .Must(date => date is null || date.Value <= ContributionRules.Today(timeProvider))
            .WithMessage("Contribution date cannot be in the future.")
            .Must(date => date is null || date.Value >= ContributionRules.EarliestDate)
            .WithMessage("Contribution date cannot be before the year 2000.");

        RuleFor(x => x.Note)
            .MaximumLength(ContributionRules.MaxNoteLength)
            .WithMessage("Note must not exceed 200 characters.");
    }
}
