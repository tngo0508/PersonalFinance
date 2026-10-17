using FluentValidation;
using PersonalFinance.Shared.DTOs.BudgetTracker;

namespace PersonalFinance.ApiService.Validators;

/// <summary>
/// Validator for category creation and update requests.
/// </summary>
public class SaveCategoryRequestValidator : AbstractValidator<SaveCategoryRequest>
{
    public SaveCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Category name is required.")
            .MaximumLength(100)
            .WithMessage("Category name must not exceed 100 characters.");

        RuleFor(x => x.Kind)
            .IsInEnum()
            .WithMessage("Valid category kind (Expense or Income) is required.");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Sort order must be greater than or equal to 0.");
    }
}