using FluentValidation;
using PersonalFinance.ApiService.Features.Community;

namespace PersonalFinance.ApiService.Validators;

/// <summary>
/// Validator for VoteOnPollCommand.
/// </summary>
public sealed class VoteOnPollCommandValidator : AbstractValidator<VoteOnPollCommand>
{
    public VoteOnPollCommandValidator()
    {
        RuleFor(x => x.Request.PollCode)
            .NotEmpty().WithMessage("Poll code is required.");

        RuleFor(x => x.Request.OptionId)
            .GreaterThan(0).WithMessage("Option ID is required.");
    }
}

/// <summary>
/// Validator for AddCommentCommand.
/// </summary>
public sealed class AddCommentCommandValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentCommandValidator()
    {
        RuleFor(x => x.Request.Text)
            .NotEmpty().WithMessage("Comment text cannot be empty.")
            .MaximumLength(2000).WithMessage("Comment text is too long (max 2000 characters).");

        RuleFor(x => x.AuthorName)
            .NotEmpty().WithMessage("Author name is required.");
    }
}

/// <summary>
/// Validator for SubmitStoryCommand.
/// </summary>
public sealed class SubmitStoryCommandValidator : AbstractValidator<SubmitStoryCommand>
{
    public SubmitStoryCommandValidator()
    {
        RuleFor(x => x.Request.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(500).WithMessage("Title must be under 500 characters.");

        RuleFor(x => x.Request.Summary)
            .NotEmpty().WithMessage("Summary is required.")
            .MaximumLength(2000).WithMessage("Summary must be under 2000 characters.");

        RuleFor(x => x.Request.Body)
            .NotEmpty().WithMessage("Story body is required.")
            .MinimumLength(100).WithMessage("Story body must be at least 100 characters.")
            .MaximumLength(10000).WithMessage("Story body must be under 10000 characters.");

        RuleFor(x => x.Request.Category)
            .NotEmpty().WithMessage("Category is required.")
            .MaximumLength(50).WithMessage("Category must be under 50 characters.");

        RuleFor(x => x.AuthorName)
            .NotEmpty().WithMessage("Author name is required.");
    }
}
