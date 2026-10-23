using PersonalFinance.Shared.DTOs.DataExport;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Builds an Account's Data Export.
/// </summary>
public interface IDataExportService
{
    /// <summary>
    /// Collects every live (not soft-deleted) record owned by <paramref name="userId"/>. Never changes data and
    /// never includes secrets. An unknown Account gets an export with empty sections.
    /// </summary>
    Task<DataExportDto> ExportAsync(string userId, CancellationToken cancellationToken = default);
}
