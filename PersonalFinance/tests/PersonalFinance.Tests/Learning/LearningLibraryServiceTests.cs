using PersonalFinance.ApiService.Features.Learning;
using PersonalFinance.Shared.DTOs.Learning;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Learning;

/// <summary>
/// What the public Library and Term Entry pages are served: Topics in shelf order, related Terms and mentions named in
/// the reader's Language, and country notes marked overdue once their rules may have changed.
/// </summary>
public class LearningLibraryServiceTests
{
    // The fixture's apr entry was last reviewed on 2026-10-27; both its notes have rules as of 2026.
    private static readonly DateTimeOffset SoonAfterReview = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static LearningLibraryService Service(DateTimeOffset now, string? folder = null) =>
        new(LearningContent.Load(folder ?? LearningContentFolders.Fixture), new FixedTimeProvider(now));

    [Fact]
    public void GetLibrary_ListsEveryTopicInShelfOrder_IncludingEmptyOnes()
    {
        LearningLibraryDto library = Service(SoonAfterReview).GetLibrary("en");

        Assert.Equal(6, library.Topics.Count);
        Assert.Empty(library.Topics.Single(t => t.Slug == "tax").Terms);
        TermSummaryDto apr = library.Topics.Single(t => t.Slug == "borrowing-credit").Terms[0];
        Assert.Equal(("apr", "Annual percentage rate", "lãi suất phần trăm hằng năm"),
            (apr.Slug, apr.Term, apr.VietnameseName));
        Assert.Equal(["APR"], apr.AlsoKnownAs);
    }

    [Fact]
    public void GetLibrary_InVietnamese_KeepsTheEnglishTermForTheFilter()
    {
        TermSummaryDto apr = Service(SoonAfterReview).GetLibrary("vi").Topics
            .SelectMany(t => t.Terms).Single(t => t.Slug == "apr");

        Assert.Equal(("Lãi suất phần trăm hằng năm", "Annual percentage rate"), (apr.Term, apr.EnglishTerm));
    }

    [Fact]
    public void FindTerm_NamesRelatedTerms_AndMentionsEveryOtherEntryLongestFirst()
    {
        TermEntryDto apr = Service(SoonAfterReview).FindTerm("apr", null)!;

        Assert.Equal(["credit-score", "compound-interest"], apr.RelatedTerms.Select(r => r.Slug));
        Assert.Equal("Credit score", apr.RelatedTerms[0].Term);
        Assert.DoesNotContain(apr.Mentions, m => m.Slug == "apr");
        Assert.Contains(apr.Mentions, m => m is { Text: "Credit score", Slug: "credit-score" });
        Assert.Equal(apr.Mentions.Select(m => m.Text.Length).OrderDescending(), apr.Mentions.Select(m => m.Text.Length));
    }

    [Fact]
    public void FindTerm_NamesTheLessonThatTeachesIt()
    {
        TermEntryDto apr = Service(SoonAfterReview).FindTerm("apr", "vi")!;

        Assert.Equal(("borrow-wisely", 2, "Vay tiền khôn ngoan"), (apr.Lesson!.Slug, apr.Lesson.Day, apr.Lesson.Title));
    }

    [Fact]
    public void GetLessons_IsTheOutlineByDay()
    {
        List<LessonSummaryDto> lessons = Service(SoonAfterReview).GetLessons("en");

        Assert.Equal([("build-a-cushion", 1, "Build a cushion"), ("borrow-wisely", 2, "Borrow wisely")],
            lessons.Select(l => (l.Slug, l.Day, l.Title)));
        Assert.Equal("Coin jar", lessons[0].Keepsake.Name);
    }

    [Fact]
    public void FindLesson_ServesItsTermEntriesInOrder_AndItsCheck()
    {
        LessonDto lesson = Service(SoonAfterReview).FindLesson("borrow-wisely", "vi")!;

        Assert.Equal(["apr", "credit-score"], lesson.Terms.Select(t => t.Slug));
        Assert.Equal("Lãi suất phần trăm hằng năm", lesson.Terms[0].Term);
        Assert.Equal(3, lesson.Check.Count);
        Assert.Equal(("myth-or-fact", 0), (lesson.Check[0].Format, lesson.Check[0].CorrectOption));
        Assert.Equal(["Lầm tưởng", "Sự thật"], lesson.Check[0].Options);
        Assert.Equal("/Budget", lesson.TryIt.Url);
        Assert.Equal("Sổ cái", lesson.Keepsake.Name);
    }

    [Fact]
    public void FindLesson_UnknownSlug_IsNull()
    {
        Assert.Null(Service(SoonAfterReview).FindLesson("no-such-lesson", "en"));
    }

    [Fact]
    public void FindTerm_UnknownSlug_IsNull()
    {
        Assert.Null(Service(SoonAfterReview).FindTerm("mortgage", "en"));
    }

    [Fact]
    public void Notes_AreCurrent_WithinTwelveMonthsOfTheReview()
    {
        TermEntryDto apr = Service(new DateTimeOffset(2027, 10, 27, 0, 0, 0, TimeSpan.Zero)).FindTerm("apr", "en")!;

        Assert.False(apr.InTheUs!.IsOverdue);
        Assert.False(apr.InVietnam!.IsOverdue);
    }

    [Fact]
    public void Notes_AreOverdue_MoreThanTwelveMonthsAfterTheReview()
    {
        TermEntryDto apr = Service(new DateTimeOffset(2027, 10, 28, 0, 0, 0, TimeSpan.Zero)).FindTerm("apr", "en")!;

        Assert.True(apr.InTheUs!.IsOverdue);
        Assert.True(apr.InVietnam!.IsOverdue);
    }

    [Fact]
    public void TaxNotes_AreOverdue_OnceTheirRulesYearIsPast()
    {
        using LearningContentFolders.TempFolder folder = LearningContentFolders.CopyOfFixture();
        string file = Path.Combine(folder.Path, "terms", "apr.json");
        File.WriteAllText(file, File.ReadAllText(file).Replace("\"borrowing-credit\"", "\"tax\""));

        TermEntryDto sameYear = Service(new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero), folder.Path)
            .FindTerm("apr", "en")!;
        TermEntryDto nextYear = Service(new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero), folder.Path)
            .FindTerm("apr", "en")!;

        Assert.False(sameYear.InTheUs!.IsOverdue);
        Assert.True(nextYear.InTheUs!.IsOverdue);
        Assert.True(nextYear.InVietnam!.IsOverdue);
    }
}
