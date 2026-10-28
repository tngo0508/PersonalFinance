using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersonalFinance.Data;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.DTOs.GoogleDrive;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Infrastructure.Localization;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Tests.GoogleDrive;

/// <summary>
/// The connected Google Drive page rendered through the real compiled Razor view (Reduce Clutter, #475): the
/// Spreadsheets cards are the one home for sheet actions, the All files list and grid are content, and the drive
/// header offers Refresh plus a ⋯ menu.
/// </summary>
public sealed partial class GoogleDrivePageRenderingTests : IAsyncLifetime
{
    private const int ConnectionId = 7;
    private const string BudgetSheetId = "sheet-budget";
    private const string PlainSheetId = "sheet-plain";

    private static readonly GoogleDriveFileDto[] SampleFiles =
    [
        Sheet(BudgetSheetId, "Monthly budget 2026.xlsx", isBudget: true, hoursAgo: 1),
        Sheet("sheet-budget-sep", "Monthly budget - September 2026.xlsx", isBudget: true, hoursAgo: 2),
        Sheet(PlainSheetId, "Household_Budget_Model.xlsx", isBudget: false, hoursAgo: 3),
        new()
        {
            Id = "doc-1", Name = "Tax_Return_2025.pdf", FileType = "PDF Document", Size = 2_450_000, SizeFormatted = "2.3 MB",
            ModifiedTime = DateTime.UtcNow.AddDays(-2), WebViewLink = "https://drive.google.com/file/d/doc-1/view"
        },
        new()
        {
            Id = "folder-1", Name = "Tax_Receipts_2025", FileType = "Folder", IsFolder = true,
            ModifiedTime = DateTime.UtcNow.AddDays(-5), WebViewLink = "https://drive.google.com/drive/folders/folder-1"
        }
    ];

    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private ApiProxy<IGoogleDriveApi> _driveProxy = null!;

    public async Task InitializeAsync()
    {
        (IGoogleDriveApi driveApi, ApiProxy<IGoogleDriveApi> proxy) = ApiProxy.Create<IGoogleDriveApi>();
        _driveProxy = proxy;
        GivenTheDrive(hasApiKey: false, isDemoData: true);

        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = RepositoryRoot.SourceProject("PersonalFinance.Web"),
            EnvironmentName = "Testing"
        });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(driveApi);
        builder.Services.AddSingleton<IUserPreferenceService, InMemoryUserPreferenceService>();
        builder.Services.AddSingleton<IFeedbackApi, NoFeedbackApi>();
        builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        builder.Services.AddIdentityCore<IdentityUser>()
            .AddSignInManager()
            .AddEntityFrameworkStores<AppDbContext>();
        builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddScheme<AuthenticationSchemeOptions, SignedInHandler>(IdentityConstants.ApplicationScheme, null);
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(AppPolicies.RequireAdmin, policy => policy.RequireAuthenticatedUser().RequireRole(AppRoles.Admin));
        builder.Services.AddAppLocalization(builder.Environment);
        builder.Services.AddControllersWithViews()
            .AddViewLocalization()
            .AddDataAnnotationsLocalization()
            .AddApplicationPart(typeof(GoogleDriveController).Assembly);

        _app = builder.Build();
        _app.UseRequestLocalization();
        _app.UseRouting();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.UseAntiforgery();
        _app.MapControllerRoute("default", "{controller}/{action=Index}");
        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task BudgetSheetCard_ShowsBudgetReport_AndKeepsTheRestInItsMoreMenu()
    {
        string html = await GetPageAsync();

        string card = SheetCard(html, BudgetSheetId);
        string menu = SheetMenu(card, BudgetSheetId);
        string primary = card.Replace(menu, string.Empty, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(primary, "btn-budget-report"));
        Assert.DoesNotContain("btn-sheet-transactions", primary, StringComparison.Ordinal);
        Assert.DoesNotContain("Add to Analysis", primary, StringComparison.Ordinal);
        Assert.DoesNotContain("https://drive.google.com/file/d/", primary, StringComparison.Ordinal);
        Assert.Matches(SheetMoreToggle(BudgetSheetId), card);
        Assert.Contains("btn-sheet-transactions", menu, StringComparison.Ordinal);
        Assert.Contains("Add to Analysis", menu, StringComparison.Ordinal);
        Assert.Contains($"https://drive.google.com/file/d/{BudgetSheetId}/view", menu, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PlainSheetCard_ShowsTransactions_AndKeepsTheRestInItsMoreMenu()
    {
        string html = await GetPageAsync();

        string card = SheetCard(html, PlainSheetId);
        string menu = SheetMenu(card, PlainSheetId);
        string primary = card.Replace(menu, string.Empty, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(primary, "btn-sheet-transactions"));
        Assert.DoesNotContain("btn-budget-report", card, StringComparison.Ordinal);
        Assert.DoesNotContain("btn-sheet-transactions", menu, StringComparison.Ordinal);
        Assert.Contains("Add to Analysis", menu, StringComparison.Ordinal);
        Assert.Contains($"https://drive.google.com/file/d/{PlainSheetId}/view", menu, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SheetActions_AppearOnlyOnTheSpreadsheetsCards()
    {
        string html = await GetPageAsync();

        string files = AllFiles(html);
        Assert.DoesNotContain("btn-budget-report", files, StringComparison.Ordinal);
        Assert.DoesNotContain("btn-sheet-transactions", files, StringComparison.Ordinal);
        Assert.DoesNotContain("/Analysis/Sync", files, StringComparison.Ordinal);
        Assert.DoesNotContain("dropdown", files, StringComparison.Ordinal);
        Assert.DoesNotContain("Sync for analysis", html, StringComparison.Ordinal);
        // One Add to Analysis per featured card, and nowhere else.
        Assert.Equal(3, Regex.Matches(Page(html), "action=\"/Analysis/Sync\"").Count);
    }

    [Fact]
    public async Task AllFiles_ListAndGrid_CarryOnlyTheOpenLink_ForEveryFile()
    {
        string html = await GetPageAsync();

        string files = AllFiles(html);
        Assert.All(SampleFiles, file =>
            Assert.Equal(2, Regex.Matches(files, $"href=\"{Regex.Escape(file.WebViewLink!)}\"").Count));
        Assert.Equal(2 * SampleFiles.Length, Regex.Matches(files, "<a ").Count);
        Assert.Equal(2, Regex.Matches(files, "id=\"btnView(Table|Grid)\"").Count);
    }

    [Fact]
    public async Task DriveHeader_OffersRefresh_AndAMoreMenu_WithoutNewBudget()
    {
        string html = await GetPageAsync();

        string header = DriveHeader(html);
        string menu = DriveMenu(html);
        Assert.Matches(RefreshButton(), header);
        Assert.Matches(DriveMoreToggle(), header);
        Assert.DoesNotContain("Open in Drive", header.Replace(menu, string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("href=\"https://drive.google.com/drive/folders/folder-abc\"", menu, StringComparison.Ordinal);
        Assert.Contains("Open in Drive", menu, StringComparison.Ordinal);
        Assert.Contains("Copy folder ID", menu, StringComparison.Ordinal);
        Assert.Contains("data-bs-target=\"#deleteDriveModal\"", menu, StringComparison.Ordinal);
        Assert.DoesNotContain("New budget", html, StringComparison.Ordinal);
        Assert.DoesNotContain("newBudgetSheetModal", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Sync<", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SampleData_WithoutAKey_TheNoticeIsTheOneApiKeyEntryPoint()
    {
        string html = await GetPageAsync();

        Assert.DoesNotContain("#reauthModal", DriveMenu(html), StringComparison.Ordinal);
        Assert.Single(Regex.Matches(Page(html), "data-bs-target=\"#reauthModal\""));
        Assert.Contains("Add API key", DriveHeader(html), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DriveWithAKey_OffersChangeApiKey_InTheMoreMenu()
    {
        GivenTheDrive(hasApiKey: true, isDemoData: false);

        string html = await GetPageAsync();

        string menu = DriveMenu(html);
        Assert.Contains("data-bs-target=\"#reauthModal\"", menu, StringComparison.Ordinal);
        Assert.Contains("Change API key", menu, StringComparison.Ordinal);
        Assert.DoesNotContain("Add API key", Page(html), StringComparison.Ordinal);
    }

    private void GivenTheDrive(bool hasApiKey, bool isDemoData)
    {
        _driveProxy.Returns(nameof(IGoogleDriveApi.GetUserConnectionsAsync), new List<GoogleDriveConnectionDto>
        {
            new()
            {
                Id = ConnectionId, Name = "Household", FolderId = "folder-abc",
                FolderUrl = "https://drive.google.com/drive/folders/folder-abc",
                MaskedApiKey = hasApiKey ? "AIza…1234" : null, SyncStatus = "Synced",
                LastSyncedAtUtc = DateTime.UtcNow.AddMinutes(-5), CachedFilesCount = SampleFiles.Length
            }
        });
        _driveProxy.Returns(nameof(IGoogleDriveApi.GetConnectionFilesAsync), new GoogleDriveFolderResponseDto
        {
            Success = true, FolderId = "folder-abc", FolderName = "Household", Files = [.. SampleFiles],
            TotalSizeFormatted = "6 MB", IsDemoData = isDemoData
        });
    }

    private static GoogleDriveFileDto Sheet(string id, string name, bool isBudget, int hoursAgo) => new()
    {
        Id = id, Name = name, FileType = "Spreadsheet", IsMonthlyBudgetSpreadsheet = isBudget, Size = 1_000_000,
        SizeFormatted = "1 MB", ModifiedTime = DateTime.UtcNow.AddHours(-hoursAgo),
        WebViewLink = $"https://drive.google.com/file/d/{id}/view"
    };

    private async Task<string> GetPageAsync()
    {
        HttpResponseMessage response = await _client.GetAsync($"/GoogleDrive?connectionId={ConnectionId}");
        string html = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"/GoogleDrive returned {response.StatusCode}: {html}");
        return WebUtility.HtmlDecode(html);
    }

    /// <summary>The page itself, without the modals that follow it.</summary>
    private static string Page(string html) => Between(html, "class=\"gd-page\"", "<!-- Budget Report Modal -->");

    private static string DriveHeader(string html) => Between(html, "<!-- Drive header -->", "<!-- Featured spreadsheets");

    private static string AllFiles(string html) => Between(html, "<!-- All files -->", "<!-- Budget Report Modal -->");

    private static string SheetCard(string html, string fileId) =>
        Between(html, $"data-sheet-card=\"{fileId}\"", "</article>");

    private static string DriveMenu(string html) => Menu(html, "gdDriveMoreBtn");

    private static string SheetMenu(string card, string fileId) => Menu(card, $"gdSheetMore_{fileId}");

    private static string Menu(string html, string toggleId)
    {
        Match match = Regex.Match(html, $"""<ul class="dropdown-menu[^"]*" aria-labelledby="{Regex.Escape(toggleId)}">(.*?)</ul>""",
            RegexOptions.Singleline);
        Assert.True(match.Success, $"No ⋯ menu labelled by {toggleId}.");
        return match.Value;
    }

    private static string Between(string html, string start, string end)
    {
        int from = html.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"The page has no {start}.");
        int to = html.IndexOf(end, from, StringComparison.Ordinal);
        Assert.True(to > from, $"The page has no {end} after {start}.");
        return html[from..to];
    }

    // A keyboard-reachable Bootstrap dropdown toggle: Bootstrap keeps aria-expanded current and closes on Escape.
    private static Regex SheetMoreToggle(string fileId) => new(
        $"""<button type="button"[^>]*id="gdSheetMore_{Regex.Escape(fileId)}"[^>]*data-bs-toggle="dropdown"[^>]*aria-expanded="false"[^>]*aria-label="[^"]+"[^>]*>""");

    [GeneratedRegex("""<button type="button"[^>]*id="gdDriveMoreBtn"[^>]*data-bs-toggle="dropdown"[^>]*aria-expanded="false"[^>]*aria-label="[^"]+"[^>]*>""")]
    private static partial Regex DriveMoreToggle();

    [GeneratedRegex("""id="btnSyncDrive"[^>]*title="Get the latest files from Google Drive"[^>]*>.*?<span>Refresh</span>""", RegexOptions.Singleline)]
    private static partial Regex RefreshButton();

    /// <summary>Signs every request in as an Account, as Identity's cookie would.</summary>
    private sealed class SignedInHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "drive-user"),
                new Claim(ClaimTypes.Email, "drive@example.com"),
                new Claim(ClaimTypes.GivenName, "Linh")
            ], IdentityConstants.ApplicationScheme);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), IdentityConstants.ApplicationScheme)));
        }
    }

    private sealed class NoFeedbackApi : IFeedbackApi
    {
        public Task SubmitAsync(SubmitFeedbackRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<List<FeedbackAdminItemDto>> ListAsync(FeedbackStatus? status = null,
            CancellationToken cancellationToken = default) => Task.FromResult(new List<FeedbackAdminItemDto>());

        public Task ChangeStatusAsync(int id, ChangeFeedbackStatusRequest request,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
