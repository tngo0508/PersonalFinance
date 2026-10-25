using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.DataExport;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Web.Areas.Identity.Pages.Account.Manage;

/// <summary>
/// Personal data: downloads the signed-in Account's Data Export as a JSON file. Replaces the Identity UI default
/// page, whose download held only the sign-in fields.
/// </summary>
[Authorize]
public class PersonalDataModel : PageModel
{
    private static readonly JsonSerializerOptions FileJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly IDataExportApi _dataExportApi;
    private readonly ILogger<PersonalDataModel> _logger;
    private readonly IStringLocalizer _localizer;

    public PersonalDataModel(IDataExportApi dataExportApi, ILogger<PersonalDataModel> logger,
        IStringLocalizer localizer)
    {
        _dataExportApi = dataExportApi;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>
    /// Why the last download failed, safe to show to the user; null when it did not fail.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    public void OnGet()
    {
    }

    /// <summary>
    /// Fetches the Data Export and returns it as <c>personal-finance-export-YYYY-MM-DD.json</c>, never cached.
    /// When ApiService fails, shows the page again with a message instead.
    /// </summary>
    public async Task<IActionResult> OnPostDownloadAsync()
    {
        try
        {
            DataExportDto export = await _dataExportApi.ExportAsync(HttpContext.RequestAborted);
            string fileName = string.Create(CultureInfo.InvariantCulture,
                $"personal-finance-export-{export.ExportedAtUtc:yyyy-MM-dd}.json");

            Response.Headers.CacheControl = "no-store";
            return File(JsonSerializer.SerializeToUtf8Bytes(export, FileJsonOptions), "application/json", fileName);
        }
        catch (Exception ex) when ((ex is Refit.ApiException or HttpRequestException or TaskCanceledException)
                                   && !HttpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Exporting personal data failed.");
            ErrorMessage = ApiErrorMessages.ForUser(ex, _localizer["Account.PersonalData.ExportFailed"]);
            return Page();
        }
    }
}
