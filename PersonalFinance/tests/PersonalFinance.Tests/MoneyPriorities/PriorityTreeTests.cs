using PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

namespace PersonalFinance.Tests.MoneyPriorities;

/// <summary>
/// Priority Tree version 1 as authored on the spec (#315) and the tree draft (#300). The diagram texts (#322) depend on
/// the node and edge order, so it is pinned here.
/// </summary>
public class PriorityTreeTests
{
    private static readonly PriorityTree Tree = PriorityTreeV1.Tree;

    [Fact]
    public void Tree_IsVersion1()
    {
        Assert.Equal(1, Tree.Version);
    }

    [Fact]
    public void Nodes_AreTheStartFiveQuestionsAndSevenLeaves_InAuthoredOrder()
    {
        Assert.Equal(
            ["S", "Q1", "Q2", "Q3", "Q4", "Q5", "L1", "L2", "L3", "L4", "L5", "L6", "L7"],
            Tree.Nodes.Select(node => node.Id));
        Assert.Equal(PriorityNodeKind.Start, Tree.Node("S").Kind);
        Assert.All(Tree.Nodes.Where(node => node.Id.StartsWith('Q')), node => Assert.Equal(PriorityNodeKind.Question, node.Kind));
        Assert.All(Tree.Nodes.Where(node => node.Id.StartsWith('L')), node => Assert.Equal(PriorityNodeKind.Leaf, node.Kind));
    }

    [Theory]
    [InlineData("S", "Extra money each month")]
    [InlineData("Q1", "Debt with APR of 7% or more?")]
    [InlineData("Q2", "Emergency fund covers 3 months of expenses?")]
    [InlineData("Q3", "Getting the full employer match?")]
    [InlineData("Q4", "When will you need this money?")]
    [InlineData("Q5", "Already at the IRS limit for your age?")]
    [InlineData("L1", "Pay down the high-interest debt first")]
    [InlineData("L2", "Build the emergency fund to 3 months")]
    [InlineData("L3", "Raise 401(k) contributions to capture the full match")]
    [InlineData("L4", "High-yield savings or short-term Treasuries")]
    [InlineData("L5", "Taxable brokerage: balanced index funds")]
    [InlineData("L6", "Max the IRA, then the 401(k)")]
    [InlineData("L7", "Taxable brokerage: stock index funds")]
    public void Node_HasItsAuthoredLabel(string id, string label)
    {
        Assert.Equal(label, Tree.Node(id).Label);
    }

    [Fact]
    public void Edges_AreLabelledAndInAuthoredOrder()
    {
        Assert.Equal(
            [
                "S->Q1:", "Q1->L1:Yes", "Q1->Q2:No", "Q2->L2:No", "Q2->Q3:Yes", "Q3->L3:No", "Q3->Q4:Yes",
                "Q4->L4:Under 3 years", "Q4->L5:3 to 10 years", "Q4->Q5:Over 10 years", "Q5->L6:No", "Q5->L7:Yes"
            ],
            Tree.Edges.Select(edge => $"{edge.From}->{edge.To}:{edge.Label}"));
    }

    [Fact]
    public void Node_WithAnUnknownId_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() => Tree.Node("Q9"));
    }
}
