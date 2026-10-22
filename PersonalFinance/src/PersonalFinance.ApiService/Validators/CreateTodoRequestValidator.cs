using FluentValidation;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Todos;

namespace PersonalFinance.ApiService.Validators;

/// <summary>
/// Validator for a request to add a To-do. The title is checked as it will be stored, trimmed: a whitespace-only
/// title is missing, and surrounding spaces do not count toward the length limit.
/// </summary>
public class CreateTodoRequestValidator : AbstractValidator<CreateTodoRequest>
{
    public CreateTodoRequestValidator()
    {
        RuleFor(x => (x.Title ?? string.Empty).Trim())
            .NotEmpty()
            .WithMessage(TodoMessages.TitleRequired)
            .MaximumLength(TodoLimits.TitleMaxLength)
            .WithMessage(TodoMessages.TitleTooLong)
            .OverridePropertyName(nameof(CreateTodoRequest.Title));
    }
}
