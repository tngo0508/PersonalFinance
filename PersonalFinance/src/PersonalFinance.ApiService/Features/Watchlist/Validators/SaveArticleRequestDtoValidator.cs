using FluentValidation;
using PersonalFinance.Shared.DTOs.Watchlist;

namespace PersonalFinance.ApiService.Features.Watchlist.Validators;

/// <summary>
/// Validator for an article saved to the Watchlist: title, source and category are required, the URL must be an
/// absolute http(s) address, and each field fits its <c>SavedArticles</c> column.
/// </summary>
internal sealed class SaveArticleRequestDtoValidator : AbstractValidator<SaveArticleRequestDto>
{
    // The SavedArticles column lengths in AppDbContext; WatchlistSaveValidationTests keeps them in step.
    public const int TitleMaxLength = 500;
    public const int UrlMaxLength = 2000;
    public const int SourceMaxLength = 200;
    public const int CategoryMaxLength = 100;
    public const int NotesMaxLength = 2000;
    public const int ImageUrlMaxLength = 2000;

    public SaveArticleRequestDtoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(TitleMaxLength);
        RuleFor(x => x.Url)
            .NotEmpty()
            .MaximumLength(UrlMaxLength)
            .Must(BeAnAbsoluteHttpUrl)
            .WithMessage("The article's address must be a full http or https link.");
        RuleFor(x => x.Source).NotEmpty().MaximumLength(SourceMaxLength);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(CategoryMaxLength);
        RuleFor(x => x.Notes).MaximumLength(NotesMaxLength);
        RuleFor(x => x.ImageUrl).MaximumLength(ImageUrlMaxLength);
    }

    private static bool BeAnAbsoluteHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
