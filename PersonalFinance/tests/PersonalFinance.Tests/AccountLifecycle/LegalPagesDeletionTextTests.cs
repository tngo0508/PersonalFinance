using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.AccountLifecycle;

/// <summary>
/// The Privacy Policy and Terms must describe account deletion as it ships (ADR 0005): hidden at once, 30 days to
/// cancel, erased within 31 days, Community content kept as "Deleted User". They must not promise the old behaviour.
/// </summary>
public class LegalPagesDeletionTextTests
{
    private static string ReadLegalView(string name) =>
        File.ReadAllText(Path.Combine(RepositoryRoot.SourceProject("PersonalFinance.Web"), "Views", "Legal",
            $"{name}.cshtml"));

    [Theory]
    [InlineData("Privacy")]
    [InlineData("Terms")]
    public void LegalPages_NoLongerSayDeletingAnAccountOnlySoftDeletesIt(string page)
    {
        string view = ReadLegalView(page);

        Assert.DoesNotContain("soft-delete your data", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("within 30 days", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("31 days", view);
        Assert.Contains("asp-page=\"/Account/Manage/PersonalData\"", view);
    }

    [Fact]
    public void PrivacyPolicy_DescribesTheDeletionRequest_TheGracePeriod_AndThePurge()
    {
        string privacy = ReadLegalView("Privacy");

        Assert.Contains("30 days", privacy);
        Assert.Contains("cancel", privacy, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Deleted User", privacy);
        Assert.Contains("backups", privacy, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Download my data", privacy);
    }
}
