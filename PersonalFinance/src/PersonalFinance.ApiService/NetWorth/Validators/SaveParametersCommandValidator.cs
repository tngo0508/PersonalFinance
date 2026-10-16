using FluentValidation;
using PersonalFinance.ApiService.NetWorth.Commands;

namespace PersonalFinance.ApiService.NetWorth.Validators;

/// <summary>
/// Validator for SaveParametersCommand.
/// </summary>
public sealed class SaveParametersCommandValidator : AbstractValidator<SaveParametersCommand>
{
    public SaveParametersCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.Parameters.StartingBalance)
            .GreaterThanOrEqualTo(0).WithMessage("Starting balance must be 0 or greater.");

        RuleFor(x => x.Parameters.AnnualSavings)
            .GreaterThanOrEqualTo(0).WithMessage("Annual savings must be 0 or greater.");

        RuleFor(x => x.Parameters.AnnualIncome)
            .GreaterThanOrEqualTo(0).WithMessage("Annual income must be 0 or greater.");

        RuleFor(x => x.Parameters.AnnualSpending)
            .GreaterThanOrEqualTo(0).WithMessage("Annual spending must be 0 or greater.");

        RuleFor(x => x.Parameters.BaseRoi)
            .GreaterThanOrEqualTo(-50).LessThanOrEqualTo(100)
            .WithMessage("Base ROI must be between -50% and 100%.");

        RuleFor(x => x.Parameters.ConservativeRoi)
            .GreaterThanOrEqualTo(-50).LessThanOrEqualTo(100)
            .WithMessage("Conservative ROI must be between -50% and 100%.");

        RuleFor(x => x.Parameters.AggressiveRoi)
            .GreaterThanOrEqualTo(-50).LessThanOrEqualTo(100)
            .WithMessage("Aggressive ROI must be between -50% and 100%.");

        RuleFor(x => x.Parameters.AggressiveSavings)
            .GreaterThanOrEqualTo(0).WithMessage("Aggressive savings must be 0 or greater.");
    }
}
