using FluentValidation;
using PersonalFinance.Shared.DTOs.Todos;

namespace PersonalFinance.ApiService.Validators;

/// <summary>
/// Validator for a request to edit a To-do: the same title rule as adding (see <see cref="TodoTitleRules"/>).
/// </summary>
public class UpdateTodoRequestValidator : AbstractValidator<UpdateTodoRequest>
{
    public UpdateTodoRequestValidator()
    {
        this.RuleForTodoTitle(x => x.Title);
    }
}
