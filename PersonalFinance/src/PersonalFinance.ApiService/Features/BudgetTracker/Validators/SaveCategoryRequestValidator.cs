using FluentValidation;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.BudgetTracker;

namespace PersonalFinance.ApiService.Features.BudgetTracker.Validators;

/// <summary>
/// Validator for category creation and update requests. Every rule carries its
/// <see cref="ApiErrorKeys.BudgetTracker"/> key (ADR 0017).
/// </summary>
internal sealed class SaveCategoryRequestValidator : AbstractValidator<SaveCategoryRequest>
{
    private static readonly IReadOnlyDictionary<string, ApiErrorArgDto> NameLengthArgs =
        ErrorArgs.Number("max", BudgetTrackerLimits.CategoryNameMaxLength);

    public SaveCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
                .WithErrorCode(ApiErrorKeys.BudgetTracker.CategoryNameRequired)
                .WithMessage("Category name is required.")
            .MaximumLength(BudgetTrackerLimits.CategoryNameMaxLength)
                .WithErrorCode(ApiErrorKeys.BudgetTracker.CategoryNameTooLong)
                .WithMessage("Category name must not exceed 100 characters.")
                .WithState(_ => NameLengthArgs);

        RuleFor(x => x.Kind)
            .IsInEnum()
                .WithErrorCode(ApiErrorKeys.BudgetTracker.CategoryKindInvalid)
                .WithMessage("Valid category kind (Expense or Income) is required.");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0)
                .WithErrorCode(ApiErrorKeys.BudgetTracker.SortOrderNegative)
                .WithMessage("Sort order must be greater than or equal to 0.");
    }
}
