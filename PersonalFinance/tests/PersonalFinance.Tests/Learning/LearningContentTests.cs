using PersonalFinance.ApiService.Features.Learning;
using PersonalFinance.ApiService.Features.Learning.Domain;

namespace PersonalFinance.Tests.Learning;

/// <summary>
/// The content loader (ADR 0019): Term Entries and Lessons by slug and by Topic, each Language falling back to English
/// field by field.
/// </summary>
public class LearningContentTests
{
    private static readonly LearningContent Content = LearningContent.Load(LearningContentFolders.Fixture);

    [Fact]
    public void FindTerm_ServesTheEntryBySlug()
    {
        TermEntry? apr = Content.FindTerm("apr", "en");

        Assert.NotNull(apr);
        Assert.Equal("Annual percentage rate", apr.Term);
        Assert.Equal(["APR"], apr.AlsoKnownAs);
        Assert.Equal(LearningTopics.BorrowingCredit, apr.Topic);
        Assert.Equal(2026, apr.InVietnam?.RulesAsOf);
        Assert.StartsWith("https://vbpl.vn/", apr.InVietnam?.Sources[0].Url);
        Assert.Equal(new DateOnly(2026, 10, 27), apr.LastReviewed);
        Assert.Null(Content.FindTerm("mortgage", "en"));
    }

    [Fact]
    public void GetTermsInTopic_ServesTheTopicsEntries_ByTerm()
    {
        Assert.Equal(["Annual percentage rate", "Credit score"],
            Content.GetTermsInTopic(LearningTopics.BorrowingCredit, "en").Select(t => t.Term));
        Assert.Empty(Content.GetTermsInTopic(LearningTopics.Tax, "en"));
        Assert.Equal(4, Content.GetTerms("en").Count);
    }

    [Fact]
    public void Vietnamese_ReplacesTheFieldsItTranslates_AndFallsBackToEnglishForTheRest()
    {
        TermEntry apr = Content.FindTerm("apr", "vi")!;
        TermEntry english = Content.FindTerm("apr", "en")!;

        Assert.Equal("Lãi suất phần trăm hằng năm", apr.Term);
        Assert.StartsWith("Chi phí vay", apr.OneLine);
        Assert.StartsWith("APR là", apr.Explanation[0]);
        Assert.Equal(english.Explanation[1], apr.Explanation[1]); // a blank paragraph falls back
        Assert.Equal(english.WhyItMatters, apr.WhyItMatters); // left out
        Assert.StartsWith("Ngân hàng", apr.InVietnam!.Text);
        Assert.Equivalent(english.InTheUs, apr.InTheUs, strict: true);
        Assert.Equivalent(english.Sources, apr.Sources, strict: true);
        Assert.Equal(english.Topic, apr.Topic);
    }

    [Fact]
    public void Vietnamese_ForAnEntryWithNoTranslation_IsTheEnglish()
    {
        Assert.Equivalent(Content.FindTerm("credit-score", "en"), Content.FindTerm("credit-score", "vi"), strict: true);
    }

    [Fact]
    public void Lessons_AreServedByDay_AndBySlug()
    {
        Assert.Equal(["build-a-cushion", "borrow-wisely"], Content.GetLessons("en").Select(l => l.Slug));

        Lesson? lesson = Content.FindLesson("borrow-wisely", "en");
        Assert.NotNull(lesson);
        Assert.Equal(2, lesson.Day);
        Assert.Equal(["apr", "credit-score"], lesson.Terms);
        Assert.Equal(3, lesson.Check.Count);
        Assert.Equal(CheckFormats.MythOrFact, lesson.Check[0].Format);
        Assert.Equal("/Budget", lesson.TryIt.Url);
        Assert.Equal("Ledger", lesson.Keepsake.Name);
        Assert.Null(Content.FindLesson("own-a-piece", "en"));
    }

    [Fact]
    public void VietnameseLesson_FallsBackPerField_AndKeepsEnglishOptionsWhenTheCountDiffers()
    {
        Lesson lesson = Content.FindLesson("borrow-wisely", "vi")!;
        Lesson english = Content.FindLesson("borrow-wisely", "en")!;

        Assert.Equal("Vay tiền khôn ngoan", lesson.Title);
        Assert.Equal(english.Intro, lesson.Intro);
        Assert.Equal(["Lầm tưởng", "Sự thật"], lesson.Check[0].Options);
        Assert.Equal(english.Check[0].Explanation, lesson.Check[0].Explanation);
        // Two options for a three-option question would move the correct answer, so the English ones stay.
        Assert.Equal(english.Check[1].Options, lesson.Check[1].Options);
        Assert.Equivalent(english.Check[2], lesson.Check[2], strict: true);
        Assert.Equal("Sổ cái", lesson.Keepsake.Name);
        Assert.Equal(english.Keepsake.Symbol, lesson.Keepsake.Symbol);
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("")]
    [InlineData(" vi ")]
    public void AnUnknownLanguage_IsEnglish_AndTheCodeIsTrimmed(string language)
    {
        string expected = language.Trim() == "vi" ? "Vay tiền khôn ngoan" : "Borrow wisely";

        Assert.Equal(expected, Content.FindLesson("borrow-wisely", language)?.Title);
    }

    [Fact]
    public void AnEmptyFolder_ServesNothing()
    {
        using LearningContentFolders.TempFolder folder = LearningContentFolders.Empty();

        LearningContent content = LearningContent.Load(folder.Path);

        Assert.Empty(content.GetTerms("en"));
        Assert.Empty(content.GetLessons("vi"));
        Assert.Null(content.FindTerm("apr", "en"));
    }

    [Fact]
    public void TheRealFolder_Loads()
    {
        LearningContent content = LearningContent.Load(LearningContentFolders.Real);

        Assert.NotNull(content.GetTerms("en"));
    }

    [Fact]
    public void AMissingFolder_Throws()
    {
        Assert.Throws<DirectoryNotFoundException>(() =>
            LearningContent.Load(Path.Combine(Path.GetTempPath(), "pf-learning-missing-" + Guid.NewGuid())));
    }

    [Theory]
    [InlineData("{ \"slug\": \"apr\", ")]
    [InlineData("{ \"slug\": \"apr\", \"oneline\": \"A misspelt field\", \"whyItMaters\": \"\" }")]
    public void AMalformedFile_FailsTheLoad_NamingTheFile(string json)
    {
        using LearningContentFolders.TempFolder folder = LearningContentFolders.CopyOfFixture();
        File.WriteAllText(Path.Combine(folder.Path, "terms", "apr.json"), json);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() => LearningContent.Load(folder.Path));

        Assert.StartsWith("terms/apr.json: ", error.Message);
    }
}
