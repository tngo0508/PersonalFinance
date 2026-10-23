using FluentValidation;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.DTOs.BudgetTracker;

namespace PersonalFinance.ApiService.Features.BudgetTracker.Validators;

/// <summary>
/// Validator for transaction creation and update requests.
/// </summary>
internal sealed class SaveTransactionRequestValidator : AbstractValidator<SaveTransactionRequest>
{
    public const decimal MaxAmount = 9_999_999_999_999_999.99m;

    public SaveTransactionRequestValidator()
    {
        RuleFor(x => x.Date)
            .NotEmpty()
            .WithMessage("Transaction date is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Transaction amount must be greater than 0.")
            .LessThanOrEqualTo(MaxAmount)
            .WithMessage("Transaction amount cannot exceed 9,999,999,999,999,999.99.")
            .Must(amount => decimal.Round(amount, 2) == amount)
            .WithMessage("Transaction amount cannot have more than 2 decimal places.");

        RuleFor(x => x.Description)
            .MaximumLength(200)
            .WithMessage("Description must not exceed 200 characters.");

        RuleFor(x => x.CategoryId)
            .GreaterThan(0)
            .WithMessage("A valid category is required.");
    }
}
