using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.DataExport;
using PersonalFinance.Web.Areas.Identity.Pages.Account.Manage;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Tests.DataExport;

/// <summary>
/// The Personal data page downloads the Data Export, and the Identity UI default download only redirects to it.
/// </summary>
public class PersonalDataPageTests
{
    private static PersonalDataModel CreatePage(IDataExportApi api)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "export-user")], "TestAuthType");
        return new PersonalDataModel(api, NullLogger<PersonalDataModel>.Instance)
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } }
        };
    }

    [Theory]
    [InlineData(typeof(PersonalDataModel))]
    [InlineData(typeof(DeletePersonalDataModel))]
    [InlineData(typeof(DownloadPersonalDataModel))]
    public void Page_RequiresASignedInAccount(Type pageModel)
    {
        Assert.NotNull(pageModel.GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public async Task Download_ReturnsTheExportAsADatedJsonFile()
    {
        var api = new FakeDataExportApi(new DataExportDto
        {
            ExportedAtUtc = new DateTime(2026, 10, 22, 23, 30, 0, DateTimeKind.Utc),
            Account = new DataExportAccountDto { Id = "export-user", Email = "me@example.com" },
            SavingsGoals = [new DataExportSavingsGoalDto { Name = "Emergency fund", TargetAmount = 1000.50m }]
        });
        var page = CreatePage(api);

        var result = await page.OnPostDownloadAsync();

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/json", file.ContentType);
        Assert.Equal("personal-finance-export-2026-10-22.json", file.FileDownloadName);
        Assert.Equal("no-store", page.Response.Headers.CacheControl.ToString());

        using var json = JsonDocument.Parse(file.FileContents);
        Assert.Equal("me@example.com", json.RootElement.GetProperty("account").GetProperty("email").GetString());
        var goal = json.RootElement.GetProperty("savingsGoals")[0];
        Assert.Equal(1000.50m, goal.GetProperty("targetAmount").GetDecimal());
    }

    [Fact]
    public async Task Download_WhenTheApiIsUnreachable_ShowsTheUnavailableMessage_AndNoFile()
    {
        var page = CreatePage(new FakeDataExportApi(new HttpRequestException("connection refused")));

        var result = await page.OnPostDownloadAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal(JsonApiErrorsFilter.UnavailableMessage, page.ErrorMessage);
        Assert.DoesNotContain("connection refused", page.ErrorMessage);
    }

    [Fact]
    public void DownloadPersonalData_SendsTheUserToTheDataExport_InsteadOfTheIdentityDefaultDownload()
    {
        Assert.Empty(typeof(DownloadPersonalDataModel).GetConstructors().Single().GetParameters());
        var page = new DownloadPersonalDataModel();

        foreach (var result in new[] { page.OnGet(), page.OnPost() })
        {
            Assert.Equal("./PersonalData", Assert.IsType<RedirectToPageResult>(result).PageName);
        }
    }

    private sealed class FakeDataExportApi : IDataExportApi
    {
        private readonly DataExportDto? _export;
        private readonly Exception? _failure;

        public FakeDataExportApi(DataExportDto export) => _export = export;

        public FakeDataExportApi(Exception failure) => _failure = failure;

        public Task<DataExportDto> ExportAsync(CancellationToken cancellationToken = default) =>
            _failure is null ? Task.FromResult(_export!) : Task.FromException<DataExportDto>(_failure);
    }
}
