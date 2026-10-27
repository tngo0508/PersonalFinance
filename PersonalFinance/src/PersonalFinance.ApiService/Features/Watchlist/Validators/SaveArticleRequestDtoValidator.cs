using FluentValidation;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Watchlist;

namespace PersonalFinance.ApiService.Features.Watchlist.Validators;

/// <summary>
/// Validator for an article saved to the Watchlist: title, source and category are required, the URL must be an
/// absolute http(s) address, and each field fits its <c>SavedArticles</c> column. Every rule carries its
/// <see cref="ApiErrorKeys.Watchlist"/> key (ADR 0017).
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
        RuleFor(x => x.Title)
            .NotEmpty()
                .WithErrorCode(ApiErrorKeys.Watchlist.TitleRequired)
                .WithMessage("The article needs a title.")
            .MaximumLength(TitleMaxLength)
                .WithErrorCode(ApiErrorKeys.Watchlist.TitleTooLong)
                .WithMessage("The article's title must be 500 characters or fewer.")
                .WithState(_ => ErrorArgs.Number("max", TitleMaxLength));
        RuleFor(x => x.Url)
            .NotEmpty()
                .WithErrorCode(ApiErrorKeys.Watchlist.UrlRequired)
                .WithMessage("The article needs an address.")
            .MaximumLength(UrlMaxLength)
                .WithErrorCode(ApiErrorKeys.Watchlist.UrlTooLong)
                .WithMessage("The article's address must be 2,000 characters or fewer.")
                .WithState(_ => ErrorArgs.Number("max", UrlMaxLength))
            .Must(BeAnAbsoluteHttpUrl)
                .WithErrorCode(ApiErrorKeys.Watchlist.UrlNotHttp)
                .WithMessage("The article's address must be a full http or https link.");
        RuleFor(x => x.Source)
            .NotEmpty()
                .WithErrorCode(ApiErrorKeys.Watchlist.SourceRequired)
                .WithMessage("The article needs a source.")
            .MaximumLength(SourceMaxLength)
                .WithErrorCode(ApiErrorKeys.Watchlist.SourceTooLong)
                .WithMessage("The article's source must be 200 characters or fewer.")
                .WithState(_ => ErrorArgs.Number("max", SourceMaxLength));
        RuleFor(x => x.Category)
            .NotEmpty()
                .WithErrorCode(ApiErrorKeys.Watchlist.CategoryRequired)
                .WithMessage("The article needs a topic.")
            .MaximumLength(CategoryMaxLength)
                .WithErrorCode(ApiErrorKeys.Watchlist.CategoryTooLong)
                .WithMessage("The article's topic must be 100 characters or fewer.")
                .WithState(_ => ErrorArgs.Number("max", CategoryMaxLength));
        RuleFor(x => x.Notes)
            .MaximumLength(NotesMaxLength)
                .WithErrorCode(ApiErrorKeys.Watchlist.NotesTooLong)
                .WithMessage("Notes must be 2,000 characters or fewer.")
                .WithState(_ => ErrorArgs.Number("max", NotesMaxLength));
        RuleFor(x => x.ImageUrl)
            .MaximumLength(ImageUrlMaxLength)
                .WithErrorCode(ApiErrorKeys.Watchlist.ImageUrlTooLong)
                .WithMessage("The article's image address must be 2,000 characters or fewer.")
                .WithState(_ => ErrorArgs.Number("max", ImageUrlMaxLength));
    }

    private static bool BeAnAbsoluteHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
