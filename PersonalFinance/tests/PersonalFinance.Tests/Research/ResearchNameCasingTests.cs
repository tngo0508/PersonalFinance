using PersonalFinance.ApiService.Features.Research.Domain;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// How Research shows SEC's all-capitals company names (the Sectors and Funds tabs, and the Companies tab's
/// suggestions and Company Profile): title-cased, except for the brand spellings, initialisms, company forms, short
/// <c>&amp;</c> names and <c>Mc</c> names that title-casing would get wrong.
/// </summary>
public class ResearchNameCasingTests
{
    [Theory]
    [InlineData("JPMORGAN CHASE & CO", "JPMorgan Chase & Co")]
    [InlineData("NVIDIA CORP", "NVIDIA Corp")]
    [InlineData("BLACKROCK, INC.", "BlackRock, Inc.")]
    [InlineData("AT&T INC.", "AT&T Inc.")]
    [InlineData("S&P GLOBAL INC.", "S&P Global Inc.")]
    [InlineData("PROCTER & GAMBLE CO", "Procter & Gamble Co")]
    [InlineData("CVS HEALTH CORP", "CVS Health Corp")]
    [InlineData("HP INC", "HP Inc")]
    [InlineData("MCKESSON CORP", "McKesson Corp")]
    [InlineData("MCDONALDS CORP", "McDonalds Corp")]
    [InlineData("ACCENTURE PLC", "Accenture PLC")]
    [InlineData("SPDR S&P 500 ETF TRUST", "SPDR S&P 500 ETF Trust")]
    [InlineData("ISHARES CORE S&P TOTAL U.S. STOCK MARKET ETF", "iShares Core S&P Total U.S. Stock Market ETF")]
    [InlineData("MICROSOFT CORP", "Microsoft Corp")]
    [InlineData("BANK OF THE OZARKS", "Bank of the Ozarks")]
    public void TitleCase_KeepsTheSpellingsTitleCasingWouldGetWrong(string secName, string expected)
    {
        Assert.Equal(expected, SectorsClassifier.TitleCase(secName));
    }

    [Theory]
    [InlineData("Apple Inc.")]
    [InlineData("Meta Platforms, Inc.")]
    public void TitleCase_LeavesANameSecAlreadyCases(string secName)
    {
        Assert.Equal(secName, SectorsClassifier.TitleCase(secName));
    }

    [Fact]
    public void CompanyName_DropsTheStateSuffix_AndKeepsTheBrandSpelling()
    {
        Assert.Equal("JPMorgan Chase & Co", SectorsClassifier.CompanyName("JPMORGAN CHASE & CO /DE/"));
    }
}
