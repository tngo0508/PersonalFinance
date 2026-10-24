using PersonalFinance.ApiService.Features.Research.Domain;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// The press-release headline rule on exhibit markup as sec.gov serves it: a paragraph that also carries the exhibit
/// label (<c>Exhibit 99.1</c>) loses the source lines that hold the label, so a slide title is not run together with
/// its footer, while a headline wrapped across source lines still reads whole.
/// </summary>
public class PressReleaseHeadlineTests
{
    private static readonly SecFilingEntry Filing =
        new("0001234567-26-000099", "8-K", new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 2), ["7.01", "9.01"]);

    private static string HeadlineOf(string head) =>
        PressReleaseBuilder.BuildRelease(1234567, Filing, "d291965dex991.htm", head).Headline;

    [Fact]
    public void ASlideTitle_IsNotRunTogetherWithItsDateAndExhibitLabel()
    {
        // Microsoft's investor-metrics deck (8-K of 2 September 2026): each slide is an image followed by its text in
        // one hidden paragraph, the title and the "September 2026 Exhibit 99.1" footer on separate source lines.
        const string head = """
                            <HTML><HEAD>
                            <TITLE>EX-99.1</TITLE>
                            </HEAD>
                            <body> <p style="margin-bottom:0pt" align="center">
                            <img src="g291965ex99_1s1g1.jpg" alt="Slide 1" title="Slide 1" ></p> <p style="font-size:1pt;color:#FFFFFF;"><font style="font-size:1pt;"> FY27 Segments and Investor Metrics
                            September 2026 Exhibit 99.1</font></p>
                            <p style="page-break-before:always"> </p>
                            """;

        Assert.Equal("FY27 Segments and Investor Metrics", HeadlineOf(head));
    }

    [Fact]
    public void AWrappedHeadline_SharingAParagraphWithTheExhibitLabel_StillReadsWhole()
    {
        const string head = """
                            <html><body>
                            <p>Exhibit 99.1
                            Sample Widgets Reports Record
                            Fiscal 2026 Results</p>
                            <p>SPRINGFIELD, Ill. — Sample Widgets Inc. today announced ...</p>
                            </body></html>
                            """;

        Assert.Equal("Sample Widgets Reports Record Fiscal 2026 Results", HeadlineOf(head));
    }

    [Fact]
    public void AWrappedHeadline_WithNoExhibitLabel_StillReadsWhole()
    {
        const string head = """
                            <html><body>
                            <p>Microsoft Cloud and AI Strength Fuels
                            Fourth Quarter Results</p>
                            </body></html>
                            """;

        Assert.Equal("Microsoft Cloud and AI Strength Fuels Fourth Quarter Results", HeadlineOf(head));
    }
}
