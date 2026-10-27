using FluentValidation;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.SavingsGoals;

namespace PersonalFinance.ApiService.Features.SavingsGoals.Validators;

/// <summary>
/// Validator for creating a goal: a name, a positive target, and a starting amount that is not negative (it becomes
/// the goal's Opening Contribution). Every rule carries its <see cref="ApiErrorKeys.SavingsGoals"/> key (ADR 0017).
/// </summary>
internal sealed class CreateSavingsGoalRequestValidator : AbstractValidator<CreateSavingsGoalRequestDto>
{
    public CreateSavingsGoalRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
                .WithErrorCode(ApiErrorKeys.SavingsGoals.NameRequired)
                .WithMessage("Goal name is required.")
            .MaximumLength(ContributionRules.MaxNameLength)
                .WithErrorCode(ApiErrorKeys.SavingsGoals.NameTooLong)
                .WithMessage("Goal name must not exceed 200 characters.")
                .WithState(_ => ErrorArgs.Number("max", ContributionRules.MaxNameLength));

        RuleFor(x => x.TargetAmount)
            .GreaterThan(0m)
                .WithErrorCode(ApiErrorKeys.SavingsGoals.TargetAmountPositive)
                .WithMessage("Target amount must be greater than 0.")
            .LessThanOrEqualTo(ContributionRules.MaxAmount)
                .WithErrorCode(ApiErrorKeys.SavingsGoals.TargetAmountTooLarge)
                .WithMessage("Target amount cannot exceed 9,999,999,999,999,999.99.")
                .WithState(_ => ContributionRules.MaxAmountArgs);

        RuleFor(x => x.CurrentAmount)
            .GreaterThanOrEqualTo(0m)
                .When(x => x.CurrentAmount.HasValue)
                .WithErrorCode(ApiErrorKeys.SavingsGoals.StartingAmountNegative)
                .WithMessage("Starting amount cannot be negative.")
            .LessThanOrEqualTo(ContributionRules.MaxAmount)
                .When(x => x.CurrentAmount.HasValue)
                .WithErrorCode(ApiErrorKeys.SavingsGoals.StartingAmountTooLarge)
                .WithMessage("Starting amount cannot exceed 9,999,999,999,999,999.99.")
                .WithState(_ => ContributionRules.MaxAmountArgs)
            .Must(amount => amount is null || decimal.Round(amount.Value, 2) == amount.Value)
                .WithErrorCode(ApiErrorKeys.SavingsGoals.StartingAmountWholeCents)
                .WithMessage("Starting amount cannot have more than 2 decimal places.");

        RuleFor(x => x.Description)
            .MaximumLength(ContributionRules.MaxDescriptionLength)
                .WithErrorCode(ApiErrorKeys.SavingsGoals.DescriptionTooLong)
                .WithMessage("Description must not exceed 1000 characters.")
                .WithState(_ => ErrorArgs.Number("max", ContributionRules.MaxDescriptionLength));
    }
}
