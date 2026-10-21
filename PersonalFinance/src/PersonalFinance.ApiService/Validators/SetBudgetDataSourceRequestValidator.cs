using FluentValidation;
using PersonalFinance.Shared.DTOs.BudgetTracker;

namespace PersonalFinance.ApiService.Validators;

/// <summary>
/// Validator for budget data source selection requests.
/// </summary>
public class SetBudgetDataSourceRequestValidator : AbstractValidator<SetBudgetDataSourceRequest>
{
    public SetBudgetDataSourceRequestValidator()
    {
        RuleFor(x => x.Source)
            .IsInEnum()
            .WithMessage("Valid budget data source is required.");
    }
}
