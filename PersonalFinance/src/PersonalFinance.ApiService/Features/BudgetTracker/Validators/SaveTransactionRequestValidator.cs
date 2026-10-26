using FluentValidation;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.BudgetTracker;

namespace PersonalFinance.ApiService.Features.BudgetTracker.Validators;

/// <summary>
/// Validator for transaction creation and update requests. Every rule carries its
/// <see cref="ApiErrorKeys.BudgetTracker"/> key and the amount bounds as arguments (ADR 0017).
/// </summary>
internal sealed class SaveTransactionRequestValidator : AbstractValidator<SaveTransactionRequest>
{
    public const decimal MaxAmount = BudgetTrackerLimits.MaxAmount;

    private static readonly IReadOnlyDictionary<string, ApiErrorArgDto> DescriptionLengthArgs =
        ErrorArgs.Number("max", BudgetTrackerLimits.DescriptionMaxLength);

    public SaveTransactionRequestValidator()
    {
        RuleFor(x => x.Date)
            .NotEmpty()
                .WithErrorCode(ApiErrorKeys.BudgetTracker.TransactionDateRequired)
                .WithMessage("Transaction date is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
                .WithErrorCode(ApiErrorKeys.BudgetTracker.TransactionAmountRange)
                .WithMessage("Transaction amount must be greater than 0.")
                .WithState(_ => BudgetTrackerLimits.AmountRangeArgs)
            .LessThanOrEqualTo(MaxAmount)
                .WithErrorCode(ApiErrorKeys.BudgetTracker.TransactionAmountRange)
                .WithMessage("Transaction amount cannot exceed 9,999,999,999,999,999.99.")
                .WithState(_ => BudgetTrackerLimits.AmountRangeArgs)
            .Must(amount => decimal.Round(amount, 2) == amount)
                .WithErrorCode(ApiErrorKeys.BudgetTracker.TransactionAmountWholeCents)
                .WithMessage("Transaction amount cannot have more than 2 decimal places.");

        RuleFor(x => x.Description)
            .MaximumLength(BudgetTrackerLimits.DescriptionMaxLength)
                .WithErrorCode(ApiErrorKeys.BudgetTracker.DescriptionTooLong)
                .WithMessage("Description must not exceed 200 characters.")
                .WithState(_ => DescriptionLengthArgs);

        RuleFor(x => x.CategoryId)
            .GreaterThan(0)
                .WithErrorCode(ApiErrorKeys.BudgetTracker.CategoryRequired)
                .WithMessage("A valid category is required.");
    }
}
