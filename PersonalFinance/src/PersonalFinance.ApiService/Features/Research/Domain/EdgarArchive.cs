using System.Globalization;

namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// Where SEC EDGAR serves a filing's files: <c>https://www.sec.gov/Archives/edgar/data/{cik}/{accession without
/// dashes}/{file}</c>.
/// </summary>
internal static class EdgarArchive
{
    private const string ArchivesUrl = "https://www.sec.gov/Archives/edgar/data/";

    /// <summary>The URL of <paramref name="fileName"/> in filing <paramref name="accessionNumber"/> of <paramref name="cik"/>.</summary>
    public static string FileUrl(long cik, string accessionNumber, string fileName) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{ArchivesUrl}{cik}/{accessionNumber.Replace("-", string.Empty, StringComparison.Ordinal)}/{Uri.EscapeDataString(fileName)}");
}
