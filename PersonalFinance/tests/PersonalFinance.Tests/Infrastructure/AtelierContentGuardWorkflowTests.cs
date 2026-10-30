using PersonalFinance.Tests.TestSupport;
using Xunit;

namespace PersonalFinance.Tests.Infrastructure;

/// <summary>
/// The Atelier content guard (#515, ADR 0019): Agent Auto-Merge never merges a PR that touches the content folder,
/// and the ticket agent never picks an <c>atelier:content</c> issue. These pin the workflow lines that keep the owner
/// as the only one who merges content.
/// </summary>
public class AtelierContentGuardWorkflowTests
{
    private const string ContentDir = "PersonalFinance/src/PersonalFinance.ApiService/Features/Learning/Content";

    // The working copy may be CRLF; the assertions below are written for LF.
    private static string ReadWorkflow(string file) =>
        File.ReadAllText(Path.Combine(RepositoryRoot.Find(), ".github", "workflows", file)).Replace("\r\n", "\n");

    [Fact]
    public void AutoMerge_ListsEveryChangedPath_AndStopsBeforeMergingAContentPr()
    {
        string workflow = ReadWorkflow("agent-auto-merge.yml");

        Assert.Contains($"CONTENT_DIR: {ContentDir}", workflow);
        Assert.True(Directory.Exists(Path.Combine([RepositoryRoot.Find(), .. ContentDir.Split('/')])));
        // Paginated, so a PR over 100 files is still read whole; a rename out of the folder counts by its old path.
        Assert.Contains("gh api --paginate \"repos/$GH_REPO/pulls/$pr/files?per_page=100\"", workflow);
        Assert.Contains(".filename, (.previous_filename // empty)", workflow);

        int guard = workflow.IndexOf("grep -c \"^$CONTENT_DIR/\"", StringComparison.Ordinal);
        int merge = workflow.IndexOf("gh pr merge", StringComparison.Ordinal);
        Assert.InRange(guard, 0, merge);
    }

    [Fact]
    public void AutoMerge_CommentsOnceForTheOwner_AndNeverFailsThePr()
    {
        string workflow = ReadWorkflow("agent-auto-merge.yml");
        int start = workflow.IndexOf("if [ \"$content\" != \"0\" ]", StringComparison.Ordinal);
        int end = workflow.IndexOf("runs=$(", StringComparison.Ordinal);
        Assert.InRange(start, 0, end);
        string guard = workflow[start..end];

        Assert.Contains("marker='<!-- atelier-content-guard -->'", guard);
        Assert.Contains("grep -qF \"$marker\"", guard);
        Assert.Contains("gh pr comment \"$pr\"", guard);
        Assert.Contains("The owner reviews and merges it", guard);
        Assert.Contains("exit 0", guard);
        Assert.DoesNotContain("exit 1", guard);
    }

    [Fact]
    public void TicketAgent_NeverPicksAnAtelierContentIssue()
    {
        string workflow = ReadWorkflow("claude-ticket-agent.yml");

        Assert.Contains(
            "(index(\"agent:in-progress\") or index(\"wayfinder:map\") or index(\"atelier:content\")) | not", workflow);
    }
}
