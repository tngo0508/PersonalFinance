using FluentValidation;
using PersonalFinance.Shared.Constants;

namespace PersonalFinance.ApiService.Validators;

/// <summary>
/// The To-do title rule shared by adding and editing a To-do.
/// </summary>
public static class TodoTitleRules
{
    /// <summary>
    /// Checks a title as it will be stored, trimmed: a whitespace-only title is missing, and surrounding spaces do
    /// not count toward <see cref="TodoLimits.TitleMaxLength"/>. Errors are reported against <c>Title</c>.
    /// </summary>
    public static void RuleForTodoTitle<T>(this AbstractValidator<T> validator, Func<T, string?> title)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(title);

        validator.RuleFor(x => (title(x) ?? string.Empty).Trim())
            .NotEmpty()
            .WithMessage(TodoMessages.TitleRequired)
            .MaximumLength(TodoLimits.TitleMaxLength)
            .WithMessage(TodoMessages.TitleTooLong)
            .OverridePropertyName("Title");
    }
}
