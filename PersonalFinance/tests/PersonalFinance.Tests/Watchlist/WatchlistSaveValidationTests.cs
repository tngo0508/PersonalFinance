using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PersonalFinance.ApiService.Features.Watchlist;
using PersonalFinance.ApiService.Features.Watchlist.Validators;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.Watchlist;
using WatchlistApiController = PersonalFinance.ApiService.Features.Watchlist.WatchlistController;

namespace PersonalFinance.Tests.Watchlist;

/// <summary>
/// <c>POST /api/Watchlist</c> validates the article with <see cref="SaveArticleRequestDtoValidator"/> before saving
/// it: a missing or overlong field, or a URL that is not http(s), answers 400 with the reasons and stores nothing,
/// rather than reaching the database. The limits are the <c>SavedArticles</c> column lengths.
/// </summary>
public class WatchlistSaveValidationTests
{
    private sealed class RecordingWatchlistService : IWatchlistService
    {
        public int Saves { get; private set; }

        public Task<SaveArticleResultDto> SaveArticleAsync(string userId, SaveArticleRequestDto request,
            CancellationToken cancellationToken = default)
        {
            Saves++;
            return Task.FromResult(new SaveArticleResultDto { Success = true, Id = 1 });
        }

        public Task<List<SavedArticleResponseDto>> GetWatchlistAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<SavedArticleResponseDto>());

        public Task<bool> RemoveArticleAsync(string userId, int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> IsArticleSavedAsync(string userId, string url, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private static WatchlistApiController CreateController(IWatchlistService service)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "watchlist-user")], "TestAuthType");
        return new WatchlistApiController(service, new SaveArticleRequestDtoValidator())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    private static SaveArticleRequestDto ValidArticle() => new()
    {
        Title = "Rates fall",
        Url = "https://example.test/rates-fall",
        Source = "BBC Business",
        Category = "General",
        Notes = "Read later",
        ImageUrl = "https://example.test/rates.jpg",
    };

    public static TheoryData<string, Action<SaveArticleRequestDto>> InvalidArticles => new()
    {
        { "no title", a => a.Title = "" },
        { "blank title", a => a.Title = "   " },
        { "title over 500", a => a.Title = new string('t', SaveArticleRequestDtoValidator.TitleMaxLength + 1) },
        { "no url", a => a.Url = "" },
        { "relative url", a => a.Url = "/news/rates-fall" },
        { "javascript url", a => a.Url = "javascript:alert(1)" },
        { "ftp url", a => a.Url = "ftp://example.test/rates" },
        { "url over 2000", a => a.Url = "https://example.test/" + new string('u', SaveArticleRequestDtoValidator.UrlMaxLength) },
        { "no source", a => a.Source = "" },
        { "source over 200", a => a.Source = new string('s', SaveArticleRequestDtoValidator.SourceMaxLength + 1) },
        { "no category", a => a.Category = "" },
        { "category over 100", a => a.Category = new string('c', SaveArticleRequestDtoValidator.CategoryMaxLength + 1) },
        { "notes over 2000", a => a.Notes = new string('n', SaveArticleRequestDtoValidator.NotesMaxLength + 1) },
        { "image url over 2000", a => a.ImageUrl = "https://example.test/" + new string('i', SaveArticleRequestDtoValidator.ImageUrlMaxLength) },
    };

    [Theory]
    [MemberData(nameof(InvalidArticles))]
    public async Task Save_AnInvalidArticle_ReturnsBadRequestWithTheReason_AndSavesNothing(string invalid, Action<SaveArticleRequestDto> breakIt)
    {
        var service = new RecordingWatchlistService();
        SaveArticleRequestDto article = ValidArticle();
        breakIt(article);

        IActionResult result = await CreateController(service).SaveArticle(article);

        object body = Assert.IsType<BadRequestObjectResult>(result).Value!;
        IEnumerable<string> errors = Assert.IsAssignableFrom<IEnumerable<string>>(body.GetType().GetProperty("errors")!.GetValue(body));
        Assert.True(errors.Any(), $"No reason given for {invalid}.");
        Assert.Equal(0, service.Saves);
    }

    [Theory]
    [InlineData("https://example.test/rates-fall")]
    [InlineData("http://feeds.bbci.co.uk/news/business-123")]
    public async Task Save_AValidArticle_IsSaved(string url)
    {
        var service = new RecordingWatchlistService();
        SaveArticleRequestDto article = ValidArticle();
        article.Url = url;
        article.Notes = null;
        article.ImageUrl = null;

        IActionResult result = await CreateController(service).SaveArticle(article);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, service.Saves);
    }

    [Fact]
    public void ValidatorLimits_AreTheSavedArticlesColumnLengths()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"WatchlistLimits_{Guid.NewGuid():N}").Options);
        IEntityType entity = db.Model.FindEntityType(typeof(SavedArticle))!;
        int? MaxLength(string property) => entity.FindProperty(property)!.GetMaxLength();

        Assert.Equal(SaveArticleRequestDtoValidator.TitleMaxLength, MaxLength(nameof(SavedArticle.Title)));
        Assert.Equal(SaveArticleRequestDtoValidator.UrlMaxLength, MaxLength(nameof(SavedArticle.Url)));
        Assert.Equal(SaveArticleRequestDtoValidator.SourceMaxLength, MaxLength(nameof(SavedArticle.Source)));
        Assert.Equal(SaveArticleRequestDtoValidator.CategoryMaxLength, MaxLength(nameof(SavedArticle.Category)));
        Assert.Equal(SaveArticleRequestDtoValidator.NotesMaxLength, MaxLength(nameof(SavedArticle.Notes)));
        Assert.Equal(SaveArticleRequestDtoValidator.ImageUrlMaxLength, MaxLength(nameof(SavedArticle.ImageUrl)));
    }
}
