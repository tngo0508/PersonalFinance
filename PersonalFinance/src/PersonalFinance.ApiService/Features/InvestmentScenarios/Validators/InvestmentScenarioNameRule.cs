using FluentValidation;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;

namespace PersonalFinance.ApiService.Features.InvestmentScenarios.Validators;

/// <summary>The name rule both Investment Scenario validators share.</summary>
internal static class InvestmentScenarioNameRule
{
    /// <summary>Not blank, and at most <see cref="InvestmentCalculatorLimits.ScenarioNameMaxLength"/> characters.</summary>
    public static IRuleBuilderOptions<T, string> ScenarioName<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .Must(name => !string.IsNullOrWhiteSpace(name))
                .WithErrorCode(ApiErrorKeys.InvestmentScenarios.NameRequired)
                .WithMessage("Give the scenario a name.")
            .MaximumLength(InvestmentCalculatorLimits.ScenarioNameMaxLength)
                .WithErrorCode(ApiErrorKeys.InvestmentScenarios.NameTooLong)
                .WithMessage($"The name can contain up to {InvestmentCalculatorLimits.ScenarioNameMaxLength} characters.")
                .WithState(_ => ErrorArgs.Number("max", InvestmentCalculatorLimits.ScenarioNameMaxLength));
}
