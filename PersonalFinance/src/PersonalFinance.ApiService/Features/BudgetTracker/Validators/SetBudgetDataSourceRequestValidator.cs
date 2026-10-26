using FluentValidation;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.BudgetTracker;

namespace PersonalFinance.ApiService.Features.BudgetTracker.Validators;

/// <summary>
/// Validator for budget data source selection requests. The rule carries its <see cref="ApiErrorKeys.BudgetTracker"/>
/// key (ADR 0017).
/// </summary>
internal sealed class SetBudgetDataSourceRequestValidator : AbstractValidator<SetBudgetDataSourceRequest>
{
    public SetBudgetDataSourceRequestValidator()
    {
        RuleFor(x => x.Source)
            .IsInEnum()
                .WithErrorCode(ApiErrorKeys.BudgetTracker.DataSourceInvalid)
                .WithMessage("Valid budget data source is required.");
    }
}
