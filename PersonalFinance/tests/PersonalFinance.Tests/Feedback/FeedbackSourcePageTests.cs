using Microsoft.AspNetCore.Http;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models.Feedback;

namespace PersonalFinance.Tests.Feedback;

/// <summary>
/// <see cref="FeedbackSourcePage"/> picks the page that "Send feedback" links record as the page the Account came
/// from: the current path and query, app-relative and within <see cref="FeedbackLimits.PageUrlMaxLength"/>.
/// </summary>
public class FeedbackSourcePageTests
{
    private static HttpRequest Request(string path, string query = "")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        return context.Request;
    }

    [Theory]
    [InlineData("/Budget", "", "/Budget")]
    [InlineData("/BudgetTracker", "?year=2026&month=10", "/BudgetTracker?year=2026&month=10")]
    [InlineData("/", "", "/")]
    public void From_IsThePathAndQueryOfTheCurrentPage(string path, string query, string expected)
    {
        Assert.Equal(expected, FeedbackSourcePage.From(Request(path, query)));
    }

    [Fact]
    public void From_DropsAQueryThatWouldMakeItTooLong()
    {
        string query = "?q=" + new string('a', FeedbackLimits.PageUrlMaxLength);

        Assert.Equal("/Search", FeedbackSourcePage.From(Request("/Search", query)));
    }

    [Fact]
    public void From_IsNothingWhenEvenThePathIsTooLong()
    {
        Assert.Null(FeedbackSourcePage.From(Request("/" + new string('a', FeedbackLimits.PageUrlMaxLength))));
    }

    [Fact]
    public void From_IsNothingForAPathThatIsNotAppRelative()
    {
        Assert.Null(FeedbackSourcePage.From(Request("//evil.example")));
    }

    [Theory]
    [InlineData("/Feedback", "")]
    [InlineData("/Feedback/Send", "")]
    [InlineData("/feedback/index", "")]
    [InlineData("/Feedback", "?from=https://evil.example")]
    public void From_OnTheFeedbackPage_IsNotTheFeedbackPageItself(string path, string query)
    {
        Assert.Null(FeedbackSourcePage.From(Request(path, query)));
    }

    [Theory]
    [InlineData("/Feedback")]
    [InlineData("/Feedback/Send?x=1")]
    [InlineData("/feedback")]
    public void Accept_RejectsTheFeedbackPageAsTheSourcePage(string page)
    {
        Assert.Null(FeedbackSourcePage.Accept(page));
    }

    [Fact]
    public void Accept_KeepsAnotherPageWithItsQuery()
    {
        Assert.Equal("/FeedbackArchive?x=1", FeedbackSourcePage.Accept("/FeedbackArchive?x=1"));
    }

    [Fact]
    public void From_OnTheFeedbackPage_KeepsThePageTheAccountCameFrom()
    {
        Assert.Equal("/Budget?month=10",
            FeedbackSourcePage.From(Request("/Feedback", "?from=%2FBudget%3Fmonth%3D10")));
    }
}
