using System.Collections.Frozen;

namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// The five regions the World tab groups issuer countries into, by ISO alpha-2 code, following geography (UN M49
/// subregions): North America is the US, Canada, Bermuda, Greenland and Saint Pierre and Miquelon; Mexico, Central
/// America, the Caribbean and South America are Latin America; the Middle East and Africa are one region; Asia and
/// Oceania are Asia Pacific. A code not listed (N-PORT's historical codes, Antarctica) has no region.
/// </summary>
internal static class CountryRegions
{
    public const string NorthAmerica = "North America";
    public const string LatinAmerica = "Latin America";
    public const string Europe = "Europe";
    public const string AsiaPacific = "Asia Pacific";
    public const string MiddleEastAfrica = "Middle East & Africa";
    public const string Other = "Other";

    private static readonly (string Region, string Codes)[] Table =
    [
        (NorthAmerica, "US CA BM GL PM"),
        (LatinAmerica, "MX GT BZ SV HN NI CR PA CU JM HT DO PR BS BB TT AG DM GD KN LC VC KY VG VI AI MS TC AW CW SX BQ GP MQ BL MF " +
            "CO VE GY SR GF EC PE BO BR PY UY AR CL FK GS"),
        (Europe, "GB IE FR DE NL BE LU CH AT IT ES PT GR DK SE NO FI IS EE LV LT PL CZ SK HU SI HR BA RS ME MK AL XK BG RO MD " +
            "UA BY RU MT CY MC SM VA AD LI GI JE GG IM FO AX SJ"),
        (MiddleEastAfrica, "TR IL PS LB SY JO IQ IR SA KW BH QA AE OM YE " +
            "EG LY TN DZ MA EH SD SS ET ER DJ SO KE UG TZ RW BI CD CG CF CM GA GQ ST TD NE NG BJ TG GH CI LR SL GN GW SN GM ML " +
            "BF MR CV AO ZM ZW MW MZ MG MU SC KM YT RE NA BW ZA LS SZ SH IO"),
        (AsiaPacific, "CN HK MO TW JP KR KP MN IN PK BD LK NP BT MV AF KZ UZ TM KG TJ GE AM AZ " +
            "SG MY TH VN PH ID BN KH LA MM TL AU NZ PG FJ SB VU NC PF WS TO KI TV NR MH FM PW GU MP AS CK NU TK WF PN NF CX CC " +
            "HM UM"),
    ];

    private static readonly FrozenDictionary<string, string> RegionByCode = Table
        .SelectMany(row => row.Codes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(code => (code, row.Region)))
        .ToFrozenDictionary(pair => pair.code, pair => pair.Region, StringComparer.Ordinal);

    /// <summary>How many codes have a region.</summary>
    public static int Count => RegionByCode.Count;

    /// <summary>The region of an alpha-2 code, or <see cref="Other"/> when it has none.</summary>
    public static string Of(string alpha2) =>
        RegionByCode.TryGetValue(alpha2.Trim().ToUpperInvariant(), out string? region) ? region : Other;
}
