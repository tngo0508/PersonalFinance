using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Models.Community;

namespace PersonalFinance.Tests.Community;

/// <summary>
/// The Guild Hall page's own wording around user content: how long ago something was posted, the label of a Strategy's
/// category, and who a post is shown as written by. The content itself (titles, bodies, comments, Pseudonyms) is never
/// translated (spec §1).
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
    public void Category_KnownCategory_IsTranslated_UnknownIsShownAsStored()
    {
        using var culture = new UiCultureScope("en");

        Assert.Equal("Groceries", CommunityText.Category(TestLocalization.Localizer, "Groceries"));
        Assert.Equal("Side hustles", CommunityText.Category(TestLocalization.Localizer, "Side hustles"));
    }

    [Fact]
    public void Category_SeededPollCategory_IsTranslated()
    {
        using var culture = new UiCultureScope("vi");

        Assert.NotEqual("Goals", CommunityText.Category(TestLocalization.Localizer, "Goals"));
        Assert.NotEqual("Groceries", CommunityText.Category(TestLocalization.Localizer, "Groceries"));
    }

    [Fact]
    public void StrategyCategories_AreTheValuesTheBoardOffers_EachWithAnEnglishAndVietnameseLabel()
    {
        Assert.Equal(["Spending", "Groceries", "Saving", "Debt", "Investing", "Income", "Habits", "Other"],
            CommunityText.StrategyCategories);
        IReadOnlyCollection<string> english = TestLocalization.Catalog.Keys("en");
        IReadOnlyCollection<string> vietnamese = TestLocalization.Catalog.Keys("vi");
        Assert.All(CommunityText.StrategyCategories, category =>
        {
            Assert.Contains($"Community.Category.{category}", english);
            Assert.Contains($"Community.Category.{category}", vietnamese);
        });
    }

    [Fact]
    public void Author_IsThePseudonymAsStored_OrFormerMemberInTheRequestLanguage()
    {
        using (new UiCultureScope("vi"))
        {
            Assert.Equal("Steady Heron", CommunityText.Author(TestLocalization.Localizer, "Steady Heron"));
            Assert.Equal(TestLocalization.Vietnamese("Community.FormerMember"),
                CommunityText.Author(TestLocalization.Localizer, null));
        }

        using (new UiCultureScope("en"))
        {
            Assert.Equal("Former member", CommunityText.Author(TestLocalization.Localizer, null));
            Assert.Equal("Former member", CommunityText.Author(TestLocalization.Localizer, " "));
        }
    }

    [Theory]
    [InlineData("Steady Heron", 2, "SH")]
    [InlineData("Steady Heron", 1, "S")]
    [InlineData("Wren", 2, "W")]
    [InlineData("former member", 2, "FM")]
    public void Initials_TakeTheFirstLetterOfEachWord(string author, int count, string expected)
    {
        Assert.Equal(expected, CommunityText.Initials(author, count));
    }
}
