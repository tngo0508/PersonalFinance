using System.Globalization;
using System.Text;

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

    /// <summary>The name of SEC's Technology office (<c>06 Technology</c>).</summary>
    private const string TechnologyOffice = "Technology";

    private static readonly HashSet<string> SmallWords = new(StringComparer.Ordinal) { "of", "and", "the", "for" };

    // Companies whose own spelling title-casing cannot guess, keyed by SEC's all-capitals word.
    private static readonly Dictionary<string, string> BrandSpellings = new(StringComparer.Ordinal)
    {
        ["JPMORGAN"] = "JPMorgan",
        ["NVIDIA"] = "NVIDIA",
        ["BLACKROCK"] = "BlackRock",
        ["PAYPAL"] = "PayPal",
        ["FEDEX"] = "FedEx",
        ["EBAY"] = "eBay",
        ["IQVIA"] = "IQVIA",
        ["NEXTERA"] = "NextEra",
        ["ONEOK"] = "ONEOK",
        ["SALESFORCE"] = "Salesforce",
        ["SERVICENOW"] = "ServiceNow",
        ["CROWDSTRIKE"] = "CrowdStrike",
        ["PEPSICO"] = "PepsiCo",
        ["GLAXOSMITHKLINE"] = "GlaxoSmithKline",
        ["ASTRAZENECA"] = "AstraZeneca",
        ["ABBVIE"] = "AbbVie",
        ["DUPONT"] = "DuPont",
        ["SPDR"] = "SPDR",
        ["ISHARES"] = "iShares",
    };

    // Initialisms and company-form abbreviations that stay in capitals.
    private static readonly HashSet<string> Capitalised = new(StringComparer.Ordinal)
    {
        "AES", "AMN", "BNY", "CME", "CSX", "CVS", "DTE", "HP", "KLA", "MSCI", "NRG", "PNC", "PPG", "PPL", "RTX", "TJX",
        "UPS", "USA", "ETF", "REIT", "LLC", "LLP", "LP", "PLC", "NV", "SA", "AG", "SE", "II", "III", "IV",
    };

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
    /// Whether filers of this office and SIC code are lit by the "Technology (incl. chip makers)" highlight: the SEC
    /// Technology office, plus SIC 3570-3579 (computer and office equipment) and 3670-3679 (electronic components and
    /// semiconductors), because SEC files chip makers such as NVIDIA and Broadcom under Manufacturing.
    /// </summary>
    public static bool IsTechnology(string office, string? sicCode) =>
        office == TechnologyOffice
        || (int.TryParse(sicCode, NumberStyles.None, CultureInfo.InvariantCulture, out int sic)
            && sic is (>= 3570 and <= 3579) or (>= 3670 and <= 3679));

    /// <summary>
    /// A company name for display: SEC's name without its state-of-incorporation suffix (<c>APPLE INC /CA/</c>,
    /// <c>UNIVERSAL DISPLAY CORP \PA\</c>), title-cased by <see cref="TitleCase"/>.
    /// </summary>
    public static string CompanyName(string name)
    {
        string trimmed = name.Trim();
        int end = trimmed.Length - 1;
        if (end >= 3 && trimmed[end] is '/' or '\\')
        {
            int start = trimmed.LastIndexOfAny(['/', '\\'], end - 1);
            int length = end - start - 1;
            if (start > 0 && trimmed[start - 1] == ' ' && length is >= 2 and <= 3
                && trimmed.AsSpan(start + 1, length).ToString().All(char.IsLetter))
            {
                trimmed = trimmed[..(start - 1)].TrimEnd();
            }
        }

        return TitleCase(trimmed);
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
        string[] original = name.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            string lower = words[i].ToLowerInvariant();
            if (i > 0 && SmallWords.Contains(lower))
            {
                words[i] = lower;
            }
            else
            {
                words[i] = CompanySpelling(original[i]) ?? words[i];
            }
        }

        return string.Join(' ', words);
    }

    /// <summary>
    /// The spelling of one all-capitals word that title-casing would get wrong, keeping any punctuation around it, or
    /// null to keep the title-cased word: a brand's own spelling (<c>JPMORGAN</c> is <c>JPMorgan</c>), an initialism
    /// or company form (<c>CVS</c>, <c>PLC</c>), a short name joined by <c>&amp;</c> (<c>AT&amp;T</c>, <c>S&amp;P</c>),
    /// or a <c>Mc</c> name (<c>MCKESSON</c> is <c>McKesson</c>).
    /// </summary>
    private static string? CompanySpelling(string word)
    {
        int start = 0;
        int end = word.Length;
        while (start < end && !char.IsLetterOrDigit(word[start]) && word[start] != '&')
        {
            start++;
        }

        while (end > start && !char.IsLetterOrDigit(word[end - 1]) && word[end - 1] != '&')
        {
            end--;
        }

        string core = word[start..end];
        string? spelled = core switch
        {
            _ when BrandSpellings.TryGetValue(core, out string? brand) => brand,
            _ when Capitalised.Contains(core) => core,
            { Length: >= 3 and <= 5 } when core.Contains('&') && core.Replace("&", string.Empty).All(char.IsLetter) =>
                core,
            { Length: >= 5 } when core.StartsWith("MC", StringComparison.Ordinal) && core.All(char.IsLetter) =>
                string.Concat("Mc", core[2..3], core[3..].ToLowerInvariant()),
            _ => null,
        };
        return spelled is null ? null : string.Concat(word[..start], spelled, word[end..]);
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
