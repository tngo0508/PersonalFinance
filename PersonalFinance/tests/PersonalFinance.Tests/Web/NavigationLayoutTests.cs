using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Web.Controllers;

namespace PersonalFinance.Tests.Web;

public class NavigationLayoutTests
{
    private static readonly string SolutionRoot = FindSolutionRoot();

    private static string FindSolutionRoot()
    {
        var current = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "PersonalFinance.sln")) ||
                File.Exists(Path.Combine(current, "CHANGELOG.md")))
            {
                return current;
            }

            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }

        return AppDomain.CurrentDomain.BaseDirectory;
    }

    [Fact]
    public void AllRequiredControllers_ExistAndInheritFromController()
    {
        var controllerTypes = new[]
        {
            typeof(DashboardController),
            typeof(NetworthController),
            typeof(BudgetTrackerController),
            typeof(BudgetController),
            typeof(SavingsGoalsController),
            typeof(TodosController),
            typeof(AnalysisController),
            typeof(InvestmentCalculatorController),
            typeof(NewsController),
            typeof(CommunityController),
            typeof(GoogleDriveController)
        };

        foreach (var type in controllerTypes)
        {
            Assert.True(typeof(Controller).IsAssignableFrom(type), $"{type.Name} should inherit from Controller.");
            var indexMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == "Index").ToList();
            Assert.NotEmpty(indexMethods);
        }
    }

    [Fact]
    public void LayoutView_ContainsThreeNavigationTiers()
    {
        var layoutPath = Path.Combine(SolutionRoot, "PersonalFinance", "src", "PersonalFinance.Web", "Views", "Shared",
            "_Layout.cshtml");
        Assert.True(File.Exists(layoutPath), $"_Layout.cshtml not found at {layoutPath}");

        var content = File.ReadAllText(layoutPath);

        // Verify the 3 navigation tiers
        Assert.Contains("Overview", content);
        Assert.Contains("My Finances", content);
        Assert.Contains("Resources", content);

        // Verify required controller links
        Assert.Contains("asp-controller=\"Dashboard\"", content);
        Assert.Contains("asp-controller=\"Networth\"", content);
        Assert.Contains("asp-controller=\"BudgetTracker\"", content);
        Assert.Contains("asp-controller=\"Budget\"", content);
        Assert.Contains("asp-controller=\"SavingsGoals\"", content);
        Assert.Contains("asp-controller=\"Analysis\"", content);
        Assert.Contains("asp-controller=\"InvestmentCalculator\"", content);
        Assert.Contains("asp-controller=\"News\"", content);
        Assert.Contains("asp-controller=\"Community\"", content);
        Assert.Contains("asp-controller=\"GoogleDrive\"", content);

        // Verify mobile bottom navigation bar exists with 5 items
        Assert.Contains("bottom-nav", content);
        Assert.Contains("Home", content);
        Assert.Contains("Finances", content);
        Assert.Contains("Tools", content);
        Assert.Contains("Community", content);
        Assert.Contains("Profile", content);
    }

    [Fact]
    public void LayoutView_LinksSignedInAccountsToFeedback_FromTheSidebarAndTheFooter()
    {
        var content = ReadView("Shared", "_Layout.cshtml");

        var sidebar = content[content.IndexOf("<aside", StringComparison.Ordinal)..content.IndexOf("</aside>", StringComparison.Ordinal)];
        Assert.Contains("asp-controller=\"Feedback\"", sidebar);

        var footer = content[content.IndexOf("<footer", StringComparison.Ordinal)..content.IndexOf("</footer>", StringComparison.Ordinal)];
        var footerLink = footer.IndexOf("asp-controller=\"Feedback\"", StringComparison.Ordinal);
        Assert.True(footerLink >= 0, "The footer should link to Feedback.");
        Assert.True(footer.LastIndexOf("@if (isSignedIn)", footerLink, StringComparison.Ordinal) >= 0,
            "The footer Feedback link should only be shown to signed-in Accounts.");
    }

    [Fact]
    public void LayoutSidebar_LinksToTheTodoList()
    {
        var content = ReadView("Shared", "_Layout.cshtml");
        var sidebar = content[content.IndexOf("<aside", StringComparison.Ordinal)..content.IndexOf("</aside>", StringComparison.Ordinal)];

        var link = sidebar.IndexOf("asp-controller=\"Todos\" asp-action=\"Index\"", StringComparison.Ordinal);
        Assert.True(link >= 0, "The sidebar should link to the To-do List.");
        Assert.Contains("To-do List", sidebar[link..]);
    }

    [Fact]
    public void AboutPage_LinksSignedInAccountsToFeedback()
    {
        var about = ReadView("Home", "About.cshtml");
        var feedbackLink = about.IndexOf("asp-controller=\"Feedback\"", StringComparison.Ordinal);

        Assert.True(feedbackLink >= 0, "The About page should link to Feedback.");
        Assert.True(about.LastIndexOf("IsAuthenticated", feedbackLink, StringComparison.Ordinal) >= 0,
            "The About page should only show the Feedback link to signed-in Accounts.");
    }

    [Theory]
    [InlineData("Shared", "_Layout.cshtml")]
    [InlineData("Home", "About.cshtml")]
    public void FeedbackLinks_CaptureTheSourcePageThroughFeedbackSourcePage(string folder, string fileName)
    {
        var view = ReadView(folder, fileName);

        Assert.DoesNotContain("asp-route-from=\"@Context.Request.Path\"", view);
        Assert.Contains("asp-route-from=\"@FeedbackSourcePage.From(Context.Request)\"", view);
    }

    [Fact]
    public void AboutPage_DescribesNetWorthAsTheGlossaryDefinesIt()
    {
        var about = ReadView("Home", "About.cshtml");

        Assert.DoesNotContain("assets and liabilities", about);
        Assert.Contains("financial independence", about[about.IndexOf("<strong>Net Worth</strong>", StringComparison.Ordinal)..]);
    }

    [Fact]
    public void FeedbackForm_HelpTextUsesTheSharedMessageLimit()
    {
        var form = ReadView("Feedback", "Index.cshtml");

        Assert.DoesNotContain("2,000", form);
        Assert.Contains("FeedbackLimits.MessageMaxLength", form[form.IndexOf("id=\"messageHelp\"", StringComparison.Ordinal)..]);
    }

    [Fact]
    public void LayoutFooter_LinksToTheAboutPage()
    {
        var footer = ReadView("Shared", "_Layout.cshtml");
        var footerStart = footer.IndexOf("<footer", StringComparison.Ordinal);
        Assert.True(footerStart >= 0, "_Layout.cshtml should contain a <footer>.");

        Assert.Contains("asp-controller=\"Home\" asp-action=\"About\"", footer[footerStart..]);
    }

    [Fact]
    public void LandingPage_LinksToTheAboutPage()
    {
        Assert.Contains("asp-controller=\"Home\" asp-action=\"About\"", ReadView("Home", "Index.cshtml"));
    }

    [Fact]
    public void AboutPage_LinksToTheDisclaimer()
    {
        Assert.Contains("asp-controller=\"Legal\" asp-action=\"Disclaimer\"", ReadView("Home", "About.cshtml"));
    }

    private static string ReadView(string folder, string fileName)
    {
        var path = Path.Combine(SolutionRoot, "PersonalFinance", "src", "PersonalFinance.Web", "Views", folder,
            fileName);
        Assert.True(File.Exists(path), $"{fileName} not found at {path}");
        return File.ReadAllText(path);
    }

    [Fact]
    public void SidebarCss_DefinesRequiredResponsiveAndCollapsedDimensions()
    {
        var cssPath = Path.Combine(SolutionRoot, "PersonalFinance", "src", "PersonalFinance.Web", "wwwroot", "css",
            "sidebar.css");
        Assert.True(File.Exists(cssPath), $"sidebar.css not found at {cssPath}");

        var content = File.ReadAllText(cssPath);

        // 240px expanded desktop width
        Assert.Contains("240px", content);

        // 60px collapsed desktop width
        Assert.Contains("60px", content);

        // Responsive mobile bottom nav rules
        Assert.Contains("bottom-nav", content);
        Assert.Contains("@media (max-width: 768px)", content);

        // Focus visibility styling for keyboard accessibility
        Assert.Contains(":focus-visible", content);
    }

    [Fact]
    public void SidebarJs_ImplementsLocalStoragePersistenceAndAccessibility()
    {
        var jsPath = Path.Combine(SolutionRoot, "PersonalFinance", "src", "PersonalFinance.Web", "wwwroot", "js",
            "core", "sidebar.js");
        Assert.True(File.Exists(jsPath), $"sidebar.js not found at {jsPath}");

        var content = File.ReadAllText(jsPath);

        // LocalStorage state persistence
        Assert.Contains("sidebar-collapsed", content);
        Assert.Contains("localStorage", content);

        // Keyboard accessibility
        Assert.Contains("Escape", content);
        Assert.Contains("aria-expanded", content);

        // Active link highlighting
        Assert.Contains("highlightActive", content);
    }
}
