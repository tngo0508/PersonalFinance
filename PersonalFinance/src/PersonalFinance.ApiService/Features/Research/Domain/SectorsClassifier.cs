using System.Globalization;
using System.Text;
using PersonalFinance.ApiService.Features.Research.Adapters;

namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// Places each filer in the Sectors sunburst's SEC industry office and SIC industry, from its SEC submissions. A filer
/// without an office (<c>ownerOrg</c>) takes the most common office of the filers with the same SIC code, then of the
/// same two-digit SIC major group, else <see cref="UnassignedOffice"/>.
/// </summary>
internal sealed class SectorsClassifier
{
    /// <summary>The office of a filer whose office cannot be told.</summary>
    public const string UnassignedOffice = "Unassigned";

    /// <summary>The industry of a filer SEC has no SIC code for.</summary>
    public const string UnclassifiedIndustry = "Unclassified";

    private static readonly HashSet<string> SmallWords = new(StringComparer.Ordinal) { "of", "and", "the", "for" };

    private static readonly SectorsOffice Unassigned = new(int.MaxValue, UnassignedOffice);

    private readonly IReadOnlyDictionary<long, SecFiler> _filers;
    private readonly Dictionary<string, SectorsOffice> _officeBySic;
    private readonly Dictionary<string, SectorsOffice> _officeByMajorGroup;

    /// <param name="filers">The filers' submissions by CIK; the fallback offices are counted over all of them.</param>
    public SectorsClassifier(IReadOnlyDictionary<long, SecFiler> filers)
    {
        _filers = filers;
        List<(string Sic, SectorsOffice Office)> withOffice = filers.Values
            .Where(f => f.SicCode is not null && f.OwnerOrg is not null)
            .Select(f => (f.SicCode!, ParseOffice(f.OwnerOrg!)))
            .ToList();
        _officeBySic = MostCommonOffice(withOffice, sic => sic);
        _officeByMajorGroup = MostCommonOffice(withOffice, MajorGroup);
    }

    /// <summary>
    /// The filer's office and industry. A filer with no submissions is Unassigned and Unclassified.
    /// </summary>
    public SectorsPlacement Place(long cik)
    {
        if (!_filers.TryGetValue(cik, out SecFiler? filer))
        {
            return new SectorsPlacement(Unassigned, null, UnclassifiedIndustry);
        }

        SectorsOffice office = filer.OwnerOrg is { } ownerOrg
            ? ParseOffice(ownerOrg)
            : filer.SicCode is not { } sic
                ? Unassigned
                : _officeBySic.GetValueOrDefault(sic) ?? _officeByMajorGroup.GetValueOrDefault(MajorGroup(sic))
                ?? Unassigned;
        string industry = filer.SicCode is null
            ? UnclassifiedIndustry
            : TitleCase(filer.SicDescription ?? filer.SicCode);
        return new SectorsPlacement(office, filer.SicCode, industry);
    }

    /// <summary>
    /// A name for display: title-cased when SEC has it all in capitals (<c>BANK OF THE OZARKS</c> becomes
    /// <c>Bank of the Ozarks</c>), otherwise as SEC has it.
    /// </summary>
    public static string TitleCase(string name)
    {
        if (name.Any(char.IsLower))
        {
            return name;
        }

        var cased = new StringBuilder(name.Length);
        char previous = ' ';
        foreach (char c in name)
        {
            bool startsWord = !char.IsLetter(previous) && previous != '\'';
            cased.Append(char.IsLetter(c) && !startsWord ? char.ToLowerInvariant(c) : c);
            previous = c;
        }

        string[] words = cased.ToString().Split(' ');
        for (int i = 1; i < words.Length; i++)
        {
            string lower = words[i].ToLowerInvariant();
            if (SmallWords.Contains(lower))
            {
                words[i] = lower;
            }
        }

        return string.Join(' ', words);
    }

    /// <summary>
    /// SEC's <c>ownerOrg</c>, e.g. <c>06 Technology</c>: the office number orders the offices, the rest names it.
    /// </summary>
    private static SectorsOffice ParseOffice(string ownerOrg)
    {
        string[] parts = ownerOrg.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int number)
            ? new SectorsOffice(number, parts[1].Trim())
            : new SectorsOffice(int.MaxValue - 1, ownerOrg.Trim());
    }

    private static string MajorGroup(string sic) => sic.Length >= 2 ? sic[..2] : sic;

    /// <summary>Per key, the office most filers have; a tie goes to the lower office number, so it is deterministic.</summary>
    private static Dictionary<string, SectorsOffice> MostCommonOffice(List<(string Sic, SectorsOffice Office)> filers,
        Func<string, string> key) =>
        filers.GroupBy(f => key(f.Sic), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g
                .GroupBy(f => f.Office)
                .OrderByDescending(o => o.Count())
                .ThenBy(o => o.Key.Order)
                .ThenBy(o => o.Key.Name, StringComparer.Ordinal)
                .First().Key, StringComparer.Ordinal);
}

/// <summary>
/// An SEC industry office: <see cref="Order"/> is SEC's office number, which orders the offices on the page.
/// </summary>
internal sealed record SectorsOffice(int Order, string Name);

/// <summary>
/// Where a filer sits in the sunburst: its office, and its SIC industry within that office.
/// </summary>
/// <param name="Office">The filer's SEC industry office.</param>
/// <param name="SicCode">The filer's SIC code, or null when it has none.</param>
/// <param name="Industry">The SIC industry's name for display.</param>
internal sealed record SectorsPlacement(SectorsOffice Office, string? SicCode, string Industry);
