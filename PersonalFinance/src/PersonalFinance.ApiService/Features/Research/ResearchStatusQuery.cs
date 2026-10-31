using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// The dataset status query: a query of its own so tests can check it translates to Azure SQL and never selects the
/// large payload column.
/// </summary>
internal static class ResearchStatusQuery
{
    /// <summary>Each stored dataset's SourceAsOf and BuiltAt, ordered by dataset name, without its payload.</summary>
    internal static IQueryable<ResearchDatasetStatusDto> Build(AppDbContext db) =>
        db.ResearchSnapshots.AsNoTracking()
            .OrderBy(s => s.Dataset)
            .Select(s => new ResearchDatasetStatusDto
            {
                Dataset = s.Dataset,
                SourceAsOf = s.SourceAsOf,
                BuiltAt = s.BuiltAt,
            });
}
