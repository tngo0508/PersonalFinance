using PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

namespace PersonalFinance.Tests.MoneyPriorities;

/// <summary>The "Your path" sentences: one per question answered on the path, then the suggested priority.</summary>
public class PriorityPathSentencesTests
{
    [Fact]
    public void For_TheDebtPath_StatesTheAnswerThenThePriority()
    {
        IReadOnlyList<string> sentences = PriorityPathSentences.For(PriorityTreeV1.Tree, ["S", "Q1", "L1"]);

        Assert.Equal(
            [
                "Debt with APR of 7% or more? Yes.",
                "Suggested priority: Pay down the high-interest debt first."
            ],
            sentences);
    }

    [Fact]
    public void For_TheLongestPath_StatesEveryQuestionInOrder()
    {
        IReadOnlyList<string> sentences =
            PriorityPathSentences.For(PriorityTreeV1.Tree, ["S", "Q1", "Q2", "Q3", "Q4", "Q5", "L7"]);

        Assert.Equal(
            [
                "Debt with APR of 7% or more? No.",
                "Emergency fund covers 3 months of expenses? Yes.",
                "Getting the full employer match? Yes.",
                "When will you need this money? Over 10 years.",
                "Already at the IRS limit for your age? Yes.",
                "Suggested priority: Taxable brokerage: stock index funds."
            ],
            sentences);
    }

    [Theory]
    [InlineData("S,Q1,Q2,L2", "Emergency fund covers 3 months of expenses? No.", "Suggested priority: Build the emergency fund to 3 months.")]
    [InlineData("S,Q1,Q2,Q3,L3", "Getting the full employer match? No.", "Suggested priority: Raise 401(k) contributions to capture the full match.")]
    [InlineData("S,Q1,Q2,Q3,Q4,L4", "When will you need this money? Under 3 years.", "Suggested priority: High-yield savings or short-term Treasuries.")]
    [InlineData("S,Q1,Q2,Q3,Q4,L5", "When will you need this money? 3 to 10 years.", "Suggested priority: Taxable brokerage: balanced index funds.")]
    [InlineData("S,Q1,Q2,Q3,Q4,Q5,L6", "Already at the IRS limit for your age? No.", "Suggested priority: Max the IRA, then the 401(k).")]
    public void For_EveryOtherLeaf_EndsWithTheDecidingAnswerAndThePriority(string path, string lastAnswer, string priority)
    {
        string[] pathIds = path.Split(',');

        IReadOnlyList<string> sentences = PriorityPathSentences.For(PriorityTreeV1.Tree, pathIds);

        Assert.Equal(pathIds.Length - 1, sentences.Count);
        Assert.Equal(lastAnswer, sentences[^2]);
        Assert.Equal(priority, sentences[^1]);
    }
}
