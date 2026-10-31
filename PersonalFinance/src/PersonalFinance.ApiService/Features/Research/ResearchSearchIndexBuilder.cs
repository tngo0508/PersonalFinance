using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// Shapes the Research page's search index from the funds, Sectors and World snapshots. Pure: a snapshot that has never
/// loaded (null) leaves only its own part of the index empty.
/// </summary>
internal static class ResearchSearchIndexBuilder
{
    /// <summary>Builds the index from the snapshots, any of which may be null.</summary>
    public static ResearchSearchIndexDto Build(FundsSnapshotDto? funds, SectorsSnapshotDto? sectors, WorldSnapshotDto? world)
    {
        var index = new ResearchSearchIndexDto();
        if (funds is not null)
        {
            index.Funds = funds.Funds
                .Select(f => new TrackedFundDto { Ticker = f.Ticker, Name = f.Name, Role = f.Role })
                .ToList();
        }

        if (sectors is not null)
        {
            index.Offices = [.. sectors.Offices];
            index.Industries = sectors.Industries
                .Where(i => i.Office >= 0 && i.Office < sectors.Offices.Count)
                .Select(i => new ResearchSearchIndustryDto { Name = i.Name, Office = sectors.Offices[i.Office] })
                .DistinctBy(i => (i.Office, i.Name), StringTupleComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (world is not null)
        {
            index.Countries = world.Countries.Select(c => c.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        }

        return index;
    }

    /// <summary>Compares (office, name) pairs without regard to case.</summary>
    private sealed class StringTupleComparer : IEqualityComparer<(string, string)>
    {
        public static readonly StringTupleComparer OrdinalIgnoreCase = new();

        public bool Equals((string, string) x, (string, string) y) =>
            string.Equals(x.Item1, y.Item1, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Item2, y.Item2, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string, string) obj) =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item1),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item2));
    }
}
