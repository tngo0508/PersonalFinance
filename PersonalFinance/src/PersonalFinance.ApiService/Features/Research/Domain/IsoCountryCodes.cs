using System.Collections.Frozen;

namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// ISO 3166-1 alpha-3 and alpha-2 to numeric codes, so World Bank rows (keyed by alpha-3) and N-PORT holdings (keyed by
/// alpha-2) match the world-atlas map shapes (keyed by three-digit numeric code, e.g. <c>840</c>, <c>076</c>).
/// </summary>
/// <remarks>
/// The table is copied from <c>codes.json</c> of the npm package <c>i18n-iso-countries@7.11.0</c> (MIT,
/// https://cdn.jsdelivr.net/npm/i18n-iso-countries@7.11.0/codes.json), the table the Research prototype used: its
/// alpha-2, alpha-3 and numeric columns, 250 rows, unchanged. It is data, not a dependency, so no package is added. World Bank
/// codes outside ISO 3166-1 (e.g. <c>XKX</c> Kosovo, <c>CHI</c> Channel Islands) and the World Bank's aggregates have
/// no numeric code here, so they have no map shape and are left out of the World snapshot.
/// </remarks>
internal static class IsoCountryCodes
{
    // "ALPHA2:ALPHA3:NUMERIC" triples separated by white space, in alpha-3 order.
    private const string Table = """
        AW:ABW:533 AF:AFG:004 AO:AGO:024 AI:AIA:660 AX:ALA:248 AL:ALB:008 AD:AND:020 AE:ARE:784 AR:ARG:032
        AM:ARM:051 AS:ASM:016 AQ:ATA:010 TF:ATF:260 AG:ATG:028 AU:AUS:036 AT:AUT:040 AZ:AZE:031 BI:BDI:108
        BE:BEL:056 BJ:BEN:204 BQ:BES:535 BF:BFA:854 BD:BGD:050 BG:BGR:100 BH:BHR:048 BS:BHS:044 BA:BIH:070
        BL:BLM:652 BY:BLR:112 BZ:BLZ:084 BM:BMU:060 BO:BOL:068 BR:BRA:076 BB:BRB:052 BN:BRN:096 BT:BTN:064
        BV:BVT:074 BW:BWA:072 CF:CAF:140 CA:CAN:124 CC:CCK:166 CH:CHE:756 CL:CHL:152 CN:CHN:156 CI:CIV:384
        CM:CMR:120 CD:COD:180 CG:COG:178 CK:COK:184 CO:COL:170 KM:COM:174 CV:CPV:132 CR:CRI:188 CU:CUB:192
        CW:CUW:531 CX:CXR:162 KY:CYM:136 CY:CYP:196 CZ:CZE:203 DE:DEU:276 DJ:DJI:262 DM:DMA:212 DK:DNK:208
        DO:DOM:214 DZ:DZA:012 EC:ECU:218 EG:EGY:818 ER:ERI:232 EH:ESH:732 ES:ESP:724 EE:EST:233 ET:ETH:231
        FI:FIN:246 FJ:FJI:242 FK:FLK:238 FR:FRA:250 FO:FRO:234 FM:FSM:583 GA:GAB:266 GB:GBR:826 GE:GEO:268
        GG:GGY:831 GH:GHA:288 GI:GIB:292 GN:GIN:324 GP:GLP:312 GM:GMB:270 GW:GNB:624 GQ:GNQ:226 GR:GRC:300
        GD:GRD:308 GL:GRL:304 GT:GTM:320 GF:GUF:254 GU:GUM:316 GY:GUY:328 HK:HKG:344 HM:HMD:334 HN:HND:340
        HR:HRV:191 HT:HTI:332 HU:HUN:348 ID:IDN:360 IM:IMN:833 IN:IND:356 IO:IOT:086 IE:IRL:372 IR:IRN:364
        IQ:IRQ:368 IS:ISL:352 IL:ISR:376 IT:ITA:380 JM:JAM:388 JE:JEY:832 JO:JOR:400 JP:JPN:392 KZ:KAZ:398
        KE:KEN:404 KG:KGZ:417 KH:KHM:116 KI:KIR:296 KN:KNA:659 KR:KOR:410 KW:KWT:414 LA:LAO:418 LB:LBN:422
        LR:LBR:430 LY:LBY:434 LC:LCA:662 LI:LIE:438 LK:LKA:144 LS:LSO:426 LT:LTU:440 LU:LUX:442 LV:LVA:428
        MO:MAC:446 MF:MAF:663 MA:MAR:504 MC:MCO:492 MD:MDA:498 MG:MDG:450 MV:MDV:462 MX:MEX:484 MH:MHL:584
        MK:MKD:807 ML:MLI:466 MT:MLT:470 MM:MMR:104 ME:MNE:499 MN:MNG:496 MP:MNP:580 MZ:MOZ:508 MR:MRT:478
        MS:MSR:500 MQ:MTQ:474 MU:MUS:480 MW:MWI:454 MY:MYS:458 YT:MYT:175 NA:NAM:516 NC:NCL:540 NE:NER:562
        NF:NFK:574 NG:NGA:566 NI:NIC:558 NU:NIU:570 NL:NLD:528 NO:NOR:578 NP:NPL:524 NR:NRU:520 NZ:NZL:554
        OM:OMN:512 PK:PAK:586 PA:PAN:591 PN:PCN:612 PE:PER:604 PH:PHL:608 PW:PLW:585 PG:PNG:598 PL:POL:616
        PR:PRI:630 KP:PRK:408 PT:PRT:620 PY:PRY:600 PS:PSE:275 PF:PYF:258 QA:QAT:634 RE:REU:638 RO:ROU:642
        RU:RUS:643 RW:RWA:646 SA:SAU:682 SD:SDN:729 SN:SEN:686 SG:SGP:702 GS:SGS:239 SH:SHN:654 SJ:SJM:744
        SB:SLB:090 SL:SLE:694 SV:SLV:222 SM:SMR:674 SO:SOM:706 PM:SPM:666 RS:SRB:688 SS:SSD:728 ST:STP:678
        SR:SUR:740 SK:SVK:703 SI:SVN:705 SE:SWE:752 SZ:SWZ:748 SX:SXM:534 SC:SYC:690 SY:SYR:760 TC:TCA:796
        TD:TCD:148 TG:TGO:768 TH:THA:764 TJ:TJK:762 TK:TKL:772 TM:TKM:795 TL:TLS:626 TO:TON:776 TT:TTO:780
        TN:TUN:788 TR:TUR:792 TV:TUV:798 TW:TWN:158 TZ:TZA:834 UG:UGA:800 UA:UKR:804 UM:UMI:581 UY:URY:858
        US:USA:840 UZ:UZB:860 VA:VAT:336 VC:VCT:670 VE:VEN:862 VG:VGB:092 VI:VIR:850 VN:VNM:704 VU:VUT:548
        WF:WLF:876 WS:WSM:882 XK:XXK:983 YE:YEM:887 ZA:ZAF:710 ZM:ZMB:894 ZW:ZWE:716
        """;

    private static readonly string[][] Rows = Table
        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
        .Select(triple => triple.Split(':'))
        .ToArray();

    private static readonly FrozenDictionary<string, string> NumericByAlpha3 =
        Rows.ToFrozenDictionary(row => row[1], row => row[2], StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> NumericByAlpha2 =
        Rows.ToFrozenDictionary(row => row[0], row => row[2], StringComparer.Ordinal);

    /// <summary>How many countries the table holds.</summary>
    public static int Count => NumericByAlpha3.Count;

    /// <summary>
    /// The three-digit ISO numeric code of an ISO alpha-3 code (upper case, as the World Bank sends it), or null when
    /// the code is not in the table.
    /// </summary>
    public static string? NumericOf(string alpha3) =>
        NumericByAlpha3.TryGetValue(alpha3, out string? numeric) ? numeric : null;

    /// <summary>
    /// The three-digit ISO numeric code of an ISO alpha-2 code (as N-PORT gives a holding's country, e.g. <c>HK</c>), or
    /// null when the code is not in the table (N-PORT's <c>XX</c>, <c>N/A</c> and historical codes).
    /// </summary>
    public static string? NumericOfAlpha2(string alpha2) =>
        NumericByAlpha2.TryGetValue(alpha2.Trim().ToUpperInvariant(), out string? numeric) ? numeric : null;
}
