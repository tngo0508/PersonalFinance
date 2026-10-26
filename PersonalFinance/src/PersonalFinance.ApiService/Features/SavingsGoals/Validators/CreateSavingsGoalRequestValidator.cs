using FluentValidation;
using PersonalFinance.Shared.DTOs.SavingsGoals;

namespace PersonalFinance.ApiService.Features.SavingsGoals.Validators;

/// <summary>
/// Validator for creating a goal: a name, a positive target, and a starting amount that is not negative (it becomes
/// the goal's Opening Contribution).
/// </summary>
internal sealed class CreateSavingsGoalRequestValidator : AbstractValidator<CreateSavingsGoalRequestDto>
{
    public CreateSavingsGoalRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Goal name is required.")
            .MaximumLength(200)
            .WithMessage("Goal name must not exceed 200 characters.");

        RuleFor(x => x.TargetAmount)
            .GreaterThan(0m)
            .WithMessage("Target amount must be greater than 0.")
            .LessThanOrEqualTo(ContributionRules.MaxAmount)
            .WithMessage("Target amount cannot exceed 9,999,999,999,999,999.99.");

        RuleFor(x => x.CurrentAmount)
            .GreaterThanOrEqualTo(0m)
            .When(x => x.CurrentAmount.HasValue)
            .WithMessage("Starting amount cannot be negative.")
            .LessThanOrEqualTo(ContributionRules.MaxAmount)
            .When(x => x.CurrentAmount.HasValue)
            .WithMessage("Starting amount cannot exceed 9,999,999,999,999,999.99.")
            .Must(amount => amount is null || decimal.Round(amount.Value, 2) == amount.Value)
            .WithMessage("Starting amount cannot have more than 2 decimal places.");

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .WithMessage("Description must not exceed 1000 characters.");
    }
}
