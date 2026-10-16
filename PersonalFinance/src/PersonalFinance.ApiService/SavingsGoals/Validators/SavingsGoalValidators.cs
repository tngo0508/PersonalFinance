using FluentValidation;
using PersonalFinance.ApiService.SavingsGoals;

namespace PersonalFinance.ApiService.SavingsGoals.Validators;

/// <summary>
/// Validator for CreateSavingsGoalCommand.
/// </summary>
public sealed class CreateSavingsGoalCommandValidator : AbstractValidator<CreateSavingsGoalCommand>
{
    public CreateSavingsGoalCommandValidator()
    {
        RuleFor(x => x.Request.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.Request.TargetAmount)
            .GreaterThan(0).WithMessage("Target amount must be greater than 0.");

        RuleFor(x => x.Request.CurrentAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Current amount must be 0 or greater.")
            .When(x => x.Request.CurrentAmount.HasValue);
    }
}

/// <summary>
/// Validator for UpdateSavingsGoalCommand.
/// </summary>
public sealed class UpdateSavingsGoalCommandValidator : AbstractValidator<UpdateSavingsGoalCommand>
{
    public UpdateSavingsGoalCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Invalid goal ID.");

        RuleFor(x => x.Request.Name)
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.")
            .When(x => !string.IsNullOrEmpty(x.Request.Name));

        RuleFor(x => x.Request.TargetAmount)
            .GreaterThan(0).WithMessage("Target amount must be greater than 0.")
            .When(x => x.Request.TargetAmount.HasValue);
    }
}
