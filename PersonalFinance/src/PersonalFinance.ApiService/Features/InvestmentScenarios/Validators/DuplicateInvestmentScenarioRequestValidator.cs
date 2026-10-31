using FluentValidation;
using PersonalFinance.Shared.DTOs.InvestmentScenarios;

namespace PersonalFinance.ApiService.Features.InvestmentScenarios.Validators;

/// <summary>
/// Validator for duplicating an Investment Scenario: the copy's name follows the same rules as a saved one's.
/// </summary>
internal sealed class DuplicateInvestmentScenarioRequestValidator : AbstractValidator<DuplicateInvestmentScenarioRequestDto>
{
    public DuplicateInvestmentScenarioRequestValidator()
    {
        RuleFor(x => x.Name).ScenarioName();
    }
}
