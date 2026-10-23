using FluentValidation;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;
using PersonalFinance.Shared.DTOs.BudgetTracker;

namespace PersonalFinance.ApiService.Features.BudgetTracker.Validators;

/// <summary>
/// Validator for budget data source selection requests.
/// </summary>
internal sealed class SetBudgetDataSourceRequestValidator : AbstractValidator<SetBudgetDataSourceRequest>
{
    public SetBudgetDataSourceRequestValidator()
    {
        RuleFor(x => x.Source)
            .IsInEnum()
            .WithMessage("Valid budget data source is required.");
    }
}
