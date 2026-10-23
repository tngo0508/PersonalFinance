using FluentValidation;
using PersonalFinance.Shared.DTOs.Todos;

namespace PersonalFinance.ApiService.Validators;

/// <summary>
/// Validator for a request to add a To-do. The title is checked as it will be stored, trimmed: a whitespace-only
/// title is missing, and surrounding spaces do not count toward the length limit (see <see cref="TodoTitleRules"/>).
/// </summary>
public class CreateTodoRequestValidator : AbstractValidator<CreateTodoRequest>
{
    public CreateTodoRequestValidator()
    {
        this.RuleForTodoTitle(x => x.Title);
    }
}
