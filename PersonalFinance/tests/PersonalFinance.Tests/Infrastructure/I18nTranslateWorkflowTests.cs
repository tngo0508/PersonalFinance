using System.Text.RegularExpressions;
using PersonalFinance.Tests.TestSupport;
using Xunit;

namespace PersonalFinance.Tests.Infrastructure;

/// <summary>
/// The i18n Translate workflow drafts the Vietnamese on every same-repository PR (ADR 0015), so the ticket agent
/// never needs the Translator key and Agent Auto-Merge never merges an untranslated PR. The repository is public:
/// these tests pin the choices that keep the key away from forks and from every Claude step.
/// </summary>
public class I18nTranslateWorkflowTests
{
    // The working copy may be CRLF; the line-anchored assertions below are written for LF.
    private static string ReadRepoFile(params string[] path) =>
        File.ReadAllText(Path.Combine([RepositoryRoot.Find(), .. path])).Replace("\r\n", "\n");

    private static string ReadWorkflow(string file) => ReadRepoFile(".github", "workflows", file);

    private static string TranslateWorkflow() => ReadWorkflow("i18n-translate.yml");

    [Fact]
    public void Workflow_RunsOnEveryPullRequest_NeverOnPullRequestTarget()
    {
        string workflow = TranslateWorkflow();

        // pull_request_target would hand secrets to a fork's code; pull_request gives a fork none.
        Assert.Contains("\non:\n  pull_request:\n", workflow);
        Assert.DoesNotContain("pull_request_target", workflow);
        // Agent Auto-Merge requires this check, and a path-filtered workflow reports none on a docs-only PR.
        Assert.DoesNotContain("paths:", workflow);
        Assert.DoesNotContain("paths-ignore:", workflow);
        Assert.Contains("types: [opened, synchronize, reopened, ready_for_review]", workflow);
    }

    [Fact]
    public void Workflow_SkipsForks_AndPushesWithThePatSoTheOtherChecksReRun()
    {
        string workflow = TranslateWorkflow();

        Assert.Contains("SAME_REPO: ${{ github.event.pull_request.head.repo.full_name == github.repository }}", workflow);
        Assert.Contains("if: env.SAME_REPO != 'true'", workflow);
        // A push made with GITHUB_TOKEN triggers no workflow, so the translated commit would never be checked or merged.
        Assert.Contains("token: ${{ secrets.AGENT_GH_TOKEN }}", workflow);
        Assert.Contains("git push origin \"HEAD:$HEAD_REF\"", workflow);
        Assert.Contains("ref: ${{ github.event.pull_request.head.ref }}", workflow);
        // The pushed commit keeps the name the docs and the script's own "Commit as:" line use.
        Assert.Contains("i18n: translate [0-9]+ (keys|content fields)", workflow);
    }

    [Fact]
    public void Workflow_CommitsTheAtelierContentCounterparts_IncludingNewFiles()
    {
        string workflow = TranslateWorkflow();

        Assert.Contains("CONTENT_DIR: PersonalFinance/src/PersonalFinance.ApiService/Features/Learning/Content", workflow);
        // A new {slug}.vi.json is untracked, so "git diff" alone would miss it and the PR would ship no Vietnamese.
        Assert.Contains("git add -A -- \"$I18N_DIR\" \"$CONTENT_DIR\"", workflow);
        Assert.Contains("git diff --cached --quiet", workflow);
        Assert.True(Directory.Exists(Path.Combine(
            RepositoryRoot.Find(), "PersonalFinance", "src", "PersonalFinance.ApiService", "Features", "Learning", "Content")));
    }

    [Fact]
    public void Workflow_GivesTheTranslatorKeyToOneRunStep_AndToNoClaudeStep()
    {
        string workflow = TranslateWorkflow();

        Assert.Equal(1, Regex.Count(workflow, Regex.Escape("secrets.AZURE_TRANSLATOR_KEY")));
        Assert.DoesNotContain("claude-code-action", workflow);
        Assert.Contains("npm run i18n:translate", workflow);

        foreach (string claudeWorkflow in new[] { "claude-ticket-agent.yml", "claude.yml", "claude-code-review.yml" })
        {
            Assert.DoesNotContain("AZURE_TRANSLATOR", ReadWorkflow(claudeWorkflow));
        }
    }

    [Fact]
    public void AgentAutoMerge_WaitsForTheTranslateCheck()
    {
        string workflow = ReadWorkflow("agent-auto-merge.yml");

        Assert.Contains("      - i18n Translate\n", workflow);
        Assert.Contains("\"Translate (i18n)\"", workflow);
        Assert.Contains("name: Translate (i18n)", TranslateWorkflow());
    }

    [Fact]
    public void TicketAgent_AddsEnglishKeysOnly_AndNeverWaitsForTranslation()
    {
        string prompt = ReadWorkflow("claude-ticket-agent.yml");

        Assert.Contains("Never edit `vi.json` or `i18n.lock.json`", prompt);
        Assert.Contains("never run the translator (`i18n:translate`", prompt);
        Assert.Contains("so never open a draft", prompt);
    }
}
