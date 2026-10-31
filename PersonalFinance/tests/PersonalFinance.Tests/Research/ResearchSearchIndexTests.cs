using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.Research;
using PersonalFinance.Data;
using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// The search index the Research page's search box loads once: funds, industry offices, industries and countries,
/// shaped from the funds, Sectors and World snapshots.
/// </summary>
public class ResearchSearchIndexTests
{
    private static readonly FundsSnapshotDto Funds = new()
    {
        Funds =
        [
            new TrackedFundDto { Ticker = "VTI", Name = "Vanguard Total Stock Market ETF", Role = "US broad" },
            new TrackedFundDto { Ticker = "BND", Name = "Vanguard Total Bond Market ETF" },
        ],
    };

    private static readonly SectorsSnapshotDto Sectors = new()
    {
        Offices = ["Technology", "Energy & Transportation"],
        Industries =
        [
            new SectorsIndustryDto { Name = "Semiconductors & Related Devices", Office = 0 },
            new SectorsIndustryDto { Name = "Crude Petroleum & Natural Gas", Office = 1 },
        ],
    };

    private static readonly WorldSnapshotDto World = new()
    {
        Countries =
        [
            new WorldCountryDto { Id = "076", Iso3 = "BRA", Name = "Brazil" },
            new WorldCountryDto { Id = "840", Iso3 = "USA", Name = "United States" },
        ],
    };

    [Fact]
    public void Build_TakesFundsInSnapshotOrder_IndustriesWithTheirOfficeName_AndCountriesByName()
    {
        ResearchSearchIndexDto index = ResearchSearchIndexBuilder.Build(Funds, Sectors, World);

        Assert.Equal(["VTI", "BND"], index.Funds.Select(f => f.Ticker));
        Assert.Equal("Vanguard Total Stock Market ETF", index.Funds[0].Name);
        Assert.Equal("US broad", index.Funds[0].Role);
        Assert.Null(index.Funds[1].Role);
        Assert.Equal(["Technology", "Energy & Transportation"], index.Offices);
        Assert.Collection(index.Industries,
            first =>
            {
                Assert.Equal("Semiconductors & Related Devices", first.Name);
                Assert.Equal("Technology", first.Office);
            },
            second =>
            {
                Assert.Equal("Crude Petroleum & Natural Gas", second.Name);
                Assert.Equal("Energy & Transportation", second.Office);
            });
        Assert.Equal(["Brazil", "United States"], index.Countries);
    }

    [Fact]
    public void Build_WithAMissingSnapshot_LeavesOnlyThatPartEmpty()
    {
        ResearchSearchIndexDto index = ResearchSearchIndexBuilder.Build(Funds, null, World);

        Assert.Empty(index.Offices);
        Assert.Empty(index.Industries);
        Assert.Equal(2, index.Funds.Count);
        Assert.Equal(2, index.Countries.Count);
    }

    [Fact]
    public void Build_WithNoSnapshots_IsEmpty()
    {
        ResearchSearchIndexDto index = ResearchSearchIndexBuilder.Build(null, null, null);

        Assert.Empty(index.Funds);
        Assert.Empty(index.Offices);
        Assert.Empty(index.Industries);
        Assert.Empty(index.Countries);
    }

    [Fact]
    public void Build_DropsDuplicateIndustryNamesWithinAnOffice()
    {
        var sectors = new SectorsSnapshotDto
        {
            Offices = ["Technology", "Manufacturing"],
            Industries =
            [
                new SectorsIndustryDto { SicCode = "3570", Name = "Computer & Office Equipment", Office = 0 },
                new SectorsIndustryDto { SicCode = "3571", Name = "computer & office equipment", Office = 0 },
                new SectorsIndustryDto { SicCode = "3572", Name = "Computer & Office Equipment", Office = 1 },
            ],
        };

        ResearchSearchIndexDto index = ResearchSearchIndexBuilder.Build(null, sectors, null);

        Assert.Equal(["Technology", "Manufacturing"], index.Industries.Select(i => i.Office));
    }

    [Fact]
    public async Task GetSearchIndexAsync_ReadsTheStoredSnapshots_AndLeavesAnUnloadedPartEmpty()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ResearchSearchIndex_{Guid.NewGuid():N}").Options);
        var store = new ResearchSnapshotStore(db, new FixedTimeProvider(new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero)));
        await store.StoreAsync(ResearchDatasets.Funds, "2026 Q1", "2026 Q2", new DateOnly(2026, 6, 30), Funds,
            CancellationToken.None);
        await store.StoreAsync(ResearchDatasets.Sectors, "2025", "2025", new DateOnly(2026, 6, 30), Sectors,
            CancellationToken.None);
        var service = new ResearchService(store, [], new ResearchSettings(), NullLogger<ResearchService>.Instance);

        ResearchSearchIndexDto index = await service.GetSearchIndexAsync();

        Assert.Equal(["VTI", "BND"], index.Funds.Select(f => f.Ticker));
        Assert.Equal(2, index.Industries.Count);
        Assert.Empty(index.Countries);
    }
}
