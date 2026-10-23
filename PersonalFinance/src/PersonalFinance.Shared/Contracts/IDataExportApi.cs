using PersonalFinance.Shared.DTOs.DataExport;
using Refit;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for the Data Export.
/// </summary>
public interface IDataExportApi
{
    /// <summary>
    /// Returns the signed-in Account's Data Export: every live record it holds, never another Account's.
    /// </summary>
    [Get("/api/data-export")]
    Task<DataExportDto> ExportAsync(CancellationToken cancellationToken = default);
}
