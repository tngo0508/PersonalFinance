using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Models.Community;

namespace PersonalFinance.Tests.Community;

/// <summary>
/// The Community page's own wording around user content: how long ago something was posted, and the label of a Story's
/// category. The content itself (titles, bodies, comments, author names) is never translated (spec §1).
/// </summary>
public class CommunityTextTests
{
    private static readonly DateTime Now = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(0, "just now")]
    [InlineData(5, "5m ago")]
    [InlineData(180, "3h ago")]
    [InlineData(60 * 24 * 2, "2d ago")]
    public void TimeAgo_InEnglish(int minutesAgo, string expected)
    {
        using var culture = new UiCultureScope("en");

        Assert.Equal(expected, CommunityText.TimeAgo(TestLocalization.Localizer, Now.AddMinutes(-minutesAgo), Now));
    }

    [Fact]
    public void TimeAgo_InVietnamese_IsNotEnglish()
    {
        using var culture = new UiCultureScope("vi");

        string text = CommunityText.TimeAgo(TestLocalization.Localizer, Now.AddMinutes(-5), Now);

        Assert.Contains("5", text, StringComparison.Ordinal);
        Assert.NotEqual("5m ago", text);
    }

    [Fact]
    public void StoryCategory_KnownCategory_IsTranslated_UnknownIsShownAsStored()
    {
        using var culture = new UiCultureScope("en");

        Assert.Equal("Budgeting", CommunityText.StoryCategory(TestLocalization.Localizer, "Budgeting"));
        Assert.Equal("Side hustles", CommunityText.StoryCategory(TestLocalization.Localizer, "Side hustles"));
    }

    [Fact]
    public void StoryCategories_AreTheValuesTheSubmitFormOffers_EachWithAnEnglishLabel()
    {
        Assert.Equal(["Budgeting", "Debt", "Investing", "Savings", "Experiments", "Career", "Other"],
            CommunityText.StoryCategories);
        IReadOnlyCollection<string> english = TestLocalization.Catalog.Keys("en");
        Assert.All(CommunityText.StoryCategories,
            category => Assert.Contains($"Community.Category.{category}", english));
    }
}
