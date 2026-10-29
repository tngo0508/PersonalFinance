using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using PersonalFinance.Shared.DTOs.Learning;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Tests.Learning;

/// <summary>
/// Terms named in a Term Entry's text link to their entries: whole words, ignoring case, the longest mention winning,
/// once per entry, and with the rest of the text encoded.
/// </summary>
public class TermTextLinkerTests
{
    private static readonly List<TermMentionDto> Mentions =
    [
        new() { Text = "capital gains tax", Slug = "capital-gains-tax" },
        new() { Text = "APR", Slug = "apr" },
        new() { Text = "tax", Slug = "tax" },
    ];

    private static string Link(string text) =>
        Render(TermTextLinker.Link(text, Mentions, slug => "/Learning/Term/" + slug));

    [Fact]
    public void LinksAMention_IgnoringCase()
    {
        Assert.Equal("A low <a class=\"atelier-term-link\" href=\"/Learning/Term/apr\">apr</a> helps.",
            Link("A low apr helps."));
    }

    [Fact]
    public void PrefersTheLongerMention_AndLinksEachEntryOnce()
    {
        Assert.Equal(
            "Pay <a class=\"atelier-term-link\" href=\"/Learning/Term/capital-gains-tax\">capital gains tax</a> or "
            + "<a class=\"atelier-term-link\" href=\"/Learning/Term/tax\">tax</a>; tax again.",
            Link("Pay capital gains tax or tax; tax again."));
    }

    [Fact]
    public void MatchesWholeWordsOnly()
    {
        Assert.Equal("Taxes and APRs.", Link("Taxes and APRs."));
    }

    [Fact]
    public void EncodesTheText()
    {
        Assert.Equal("&lt;b&gt; <a class=\"atelier-term-link\" href=\"/Learning/Term/apr\">APR</a>",
            Link("<b> APR"));
    }

    private static string Render(IHtmlContent content)
    {
        using var writer = new StringWriter();
        content.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }
}
