using PersonalFinance.Tests.TestSupport;
using Xunit;

namespace PersonalFinance.Tests.Infrastructure;

/// <summary>
/// The owner guard: anyone can open an issue or a fork PR on a public repository, so the ticket agent works only on
/// issues the owner assigned to themself, Agent Auto-Merge merges only the agent's own PRs for such issues, and only
/// the owner can summon Claude with a mention. These pin the workflow lines that keep outsiders from steering either.
/// </summary>
public class AgentOwnerGuardWorkflowTests
{
    private const string OwnerOnly = "jq -e --arg owner \"$OWNER\" '[.assignees[].login] == [$owner]'";

    // The working copy may be CRLF; the assertions below are written for LF.
    private static string ReadWorkflow(string file) =>
        File.ReadAllText(Path.Combine(RepositoryRoot.Find(), ".github", "workflows", file)).Replace("\r\n", "\n");

    [Fact]
    public void TicketAgent_PicksOnlyIssuesAssignedToTheOwnerAlone()
    {
        string workflow = ReadWorkflow("claude-ticket-agent.yml");

        Assert.Contains("OWNER: ${{ github.repository_owner }}", workflow);
        Assert.Contains(OwnerOnly, workflow);
        Assert.DoesNotContain("'.assignees | length'", workflow);
    }

    [Fact]
    public void TicketAgent_NeverRebasesAForkPr()
    {
        string workflow = ReadWorkflow("claude-ticket-agent.yml");
        int start = workflow.IndexOf("stale=$(gh pr list", StringComparison.Ordinal);
        int end = workflow.IndexOf("if [ -n \"$stale\" ]", StringComparison.Ordinal);
        Assert.InRange(start, 0, end);

        Assert.Contains("and (.isCrossRepository | not)", workflow[start..end]);
    }

    [Fact]
    public void AutoMerge_ChecksRepositoryAuthorAndAssigneeBeforeMerging()
    {
        string workflow = ReadWorkflow("agent-auto-merge.yml");
        int merge = workflow.IndexOf("gh pr merge", StringComparison.Ordinal);

        Assert.Contains("OWNER: ${{ github.repository_owner }}", workflow);
        Assert.Contains("AGENT_LOGIN: app/claude", workflow);
        Assert.InRange(workflow.IndexOf("select(.isCrossRepository | not)", StringComparison.Ordinal), 0, merge);
        Assert.InRange(workflow.IndexOf("if [ \"$author\" != \"$AGENT_LOGIN\" ]", StringComparison.Ordinal), 0, merge);
        Assert.InRange(workflow.IndexOf(OwnerOnly.Replace("jq -e", "| jq -e"), StringComparison.Ordinal), 0, merge);
    }

    [Fact]
    public void ClaudeMention_RunsOnlyForTheOwner()
    {
        string workflow = ReadWorkflow("claude.yml");

        Assert.Contains("github.actor == github.repository_owner && (", workflow);
    }
}
