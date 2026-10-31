using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.Research;
using PersonalFinance.Data;
using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// The dataset status: each stored dataset's SourceAsOf and BuiltAt, read without its large payload.
/// </summary>
public class ResearchStatusTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    private static AppDbContext CreateContext(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);

    private static ResearchService CreateService(AppDbContext db) =>
        new(new ResearchSnapshotStore(db, new FixedTimeProvider(Now)), [], new ResearchSettings(),
            NullLogger<ResearchService>.Instance);

    [Fact]
    public async Task GetStatusAsync_ListsEachStoredDataset_OrderedByName_WithItsSourceAsOfAndBuiltAt()
    {
        await using AppDbContext db = CreateContext($"ResearchStatus_{Guid.NewGuid():N}");
        var store = new ResearchSnapshotStore(db, new FixedTimeProvider(Now));
        await store.StoreAsync(ResearchDatasets.World, "2024", "2024", new DateOnly(2026, 10, 8), new List<int> { 1 },
            CancellationToken.None);
        await store.StoreAsync(ResearchDatasets.Macro, "2026", "2026", new DateOnly(2026, 9, 30), new List<int> { 2 },
            CancellationToken.None);

        ResearchStatusDto status = await CreateService(db).GetStatusAsync();

        Assert.Collection(status.Datasets,
            first => Assert.Equal(ResearchDatasets.Macro, first.Dataset),
            second => Assert.Equal(ResearchDatasets.World, second.Dataset));
        ResearchDatasetStatusDto macro = status.Datasets.Single(d => d.Dataset == ResearchDatasets.Macro);
        Assert.Equal(new DateOnly(2026, 9, 30), macro.SourceAsOf);
        Assert.Equal(Now.UtcDateTime, macro.BuiltAt);
        Assert.Equal(new DateOnly(2026, 10, 8), status.Datasets.Single(d => d.Dataset == ResearchDatasets.World).SourceAsOf);
    }

    [Fact]
    public async Task GetStatusAsync_WithNoSnapshots_ReturnsAnEmptyList()
    {
        await using AppDbContext db = CreateContext($"ResearchStatus_{Guid.NewGuid():N}");

        ResearchStatusDto status = await CreateService(db).GetStatusAsync();

        Assert.Empty(status.Datasets);
    }

    [Fact]
    public void StatusQuery_DoesNotSelectThePayload_AndTranslatesToAzureSql()
    {
        using AzureSqlAppDbContext db = AzureSqlTranslation.CreateContext();

        string sql = AzureSqlTranslation.AssertTranslates(ResearchStatusQuery.Build(db));

        Assert.Contains("ResearchSnapshots", sql);
        Assert.DoesNotContain("Payload", sql);
    }
}
