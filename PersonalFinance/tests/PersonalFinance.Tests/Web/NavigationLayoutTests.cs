using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Features.BudgetPlanner;
using PersonalFinance.ApiService.Features.BudgetTracker;
using PersonalFinance.ApiService.Features.Dashboard;
using PersonalFinance.ApiService.Features.GoogleDrive;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Models.Feedback;
using BudgetController = PersonalFinance.Web.Controllers.BudgetController;
using BudgetTrackerController = PersonalFinance.Web.Controllers.BudgetTrackerController;
using DashboardController = PersonalFinance.Web.Controllers.DashboardController;
using GoogleDriveController = PersonalFinance.Web.Controllers.GoogleDriveController;

namespace PersonalFinance.Tests.Web;

public class NavigationLayoutTests
{
    private static readonly string SolutionRoot = FindSolutionRoot();

    private static string FindSolutionRoot()
    {
        string current = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "PersonalFinance.sln")) ||
                File.Exists(Path.Combine(current, "CHANGELOG.md")))
            {
                return current;
            }

            DirectoryInfo? parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }

        return AppDomain.CurrentDomain.BaseDirectory;
    }

    [Fact]
    public void AllRequiredControllers_ExistAndInheritFromController()
    {
        Type[] controllerTypes = new[]
        {
            typeof(DashboardController),
            typeof(NetworthController),
            typeof(BudgetTrackerController),
            typeof(BudgetController),
            typeof(SavingsGoalsController),
            typeof(AnalysisController),
            typeof(InvestmentCalculatorController),
            typeof(NewsController),
            typeof(CommunityController),
            typeof(GoogleDriveController),
            typeof(ResearchController)
        };

        foreach (Type? type in controllerTypes)
        {
            Assert.True(typeof(Controller).IsAssignableFrom(type), $"{type.Name} should inherit from Controller.");
            var indexMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == "Index").ToList();
            Assert.NotEmpty(indexMethods);
        }
    }

    [Fact]
    public void Sidebar_LinksToResearch_InTheResourcesGroup()
    {
        string sidebar = Between(ReadView("Shared", "_Layout.cshtml"), "<aside", "</aside>");
        string resources = Between(sidebar, "Resources</div>", "<div class=\"sidebar-group\">");

        Assert.Contains("asp-controller=\"Research\" asp-action=\"Index\"", resources);
        Assert.Contains("currentController == \"Research\" ? \"active\"", resources);
        Assert.Contains("<span class=\"sidebar-link-text\">Research</span>", resources);
    }

    [Fact]
    public void BottomNav_KeepsFiveItems_AndLightsToolsOnResearch()
    {
        string bottomNav = Between(ReadView("Shared", "_Layout.cshtml"), "<nav class=\"bottom-nav\"", "</nav>");

        Assert.Equal(5, Regex.Matches(bottomNav, "class=\"bottom-nav-item").Count);
        string tools = bottomNav[..bottomNav.IndexOf("<span>Tools</span>", StringComparison.Ordinal)];
        tools = tools[tools.LastIndexOf("<a ", StringComparison.Ordinal)..];
        Assert.Contains("currentController == \"Research\"", tools);
    }

    [Fact]
    public void NetWorthPage_LinksToResearch_AndTheDashboardDoesNot()
    {
        Assert.Contains("asp-controller=\"Research\" asp-action=\"Index\"", ReadView("Networth", "_NetWorthContent.cshtml"));
        Assert.DoesNotContain("asp-controller=\"Research\"", ReadView("Dashboard", "Index.cshtml"));
    }

    private static string Between(string content, string start, string end)
    {
        int from = content.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"{start} not found.");
        return content[from..content.IndexOf(end, from, StringComparison.Ordinal)];
    }

    [Fact]
    public void LayoutView_ContainsThreeNavigationTiers()
    {
        string layoutPath = Path.Combine(SolutionRoot, "PersonalFinance", "src", "PersonalFinance.Web", "Views", "Shared",
            "_Layout.cshtml");
        Assert.True(File.Exists(layoutPath), $"_Layout.cshtml not found at {layoutPath}");

        string content = File.ReadAllText(layoutPath);

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
        Assert.Contains("asp-controller=\"Research\"", content);

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
        string content = ReadView("Shared", "_Layout.cshtml");

        string sidebar = content[content.IndexOf("<aside", StringComparison.Ordinal)..content.IndexOf("</aside>", StringComparison.Ordinal)];
        Assert.Contains("asp-controller=\"Feedback\"", sidebar);

        string footer = content[content.IndexOf("<footer", StringComparison.Ordinal)..content.IndexOf("</footer>", StringComparison.Ordinal)];
        int footerLink = footer.IndexOf("asp-controller=\"Feedback\"", StringComparison.Ordinal);
        Assert.True(footerLink >= 0, "The footer should link to Feedback.");
        Assert.True(footer.LastIndexOf("@if (isSignedIn)", footerLink, StringComparison.Ordinal) >= 0,
            "The footer Feedback link should only be shown to signed-in Accounts.");
    }

    [Fact]
    public void AboutPage_LinksSignedInAccountsToFeedback()
    {
        string about = ReadView("Home", "About.cshtml");
        int feedbackLink = about.IndexOf("asp-controller=\"Feedback\"", StringComparison.Ordinal);

        Assert.True(feedbackLink >= 0, "The About page should link to Feedback.");
        Assert.True(about.LastIndexOf("IsAuthenticated", feedbackLink, StringComparison.Ordinal) >= 0,
            "The About page should only show the Feedback link to signed-in Accounts.");
    }

    [Theory]
    [InlineData("Shared", "_Layout.cshtml")]
    [InlineData("Home", "About.cshtml")]
    public void FeedbackLinks_CaptureTheSourcePageThroughFeedbackSourcePage(string folder, string fileName)
    {
        string view = ReadView(folder, fileName);

        Assert.DoesNotContain("asp-route-from=\"@Context.Request.Path\"", view);
        Assert.Contains("asp-route-from=\"@FeedbackSourcePage.From(Context.Request)\"", view);
    }

    [Fact]
    public void AboutPage_DescribesNetWorthAsTheGlossaryDefinesIt()
    {
        string about = ReadView("Home", "About.cshtml");

        Assert.DoesNotContain("assets and liabilities", about);
        Assert.Contains("financial independence", about[about.IndexOf("<strong>Net Worth</strong>", StringComparison.Ordinal)..]);
    }

    [Fact]
    public void FeedbackForm_HelpTextUsesTheSharedMessageLimit()
    {
        string form = ReadView("Feedback", "Index.cshtml");

        Assert.DoesNotContain("2,000", form);
        Assert.Contains("FeedbackLimits.MessageMaxLength", form[form.IndexOf("id=\"messageHelp\"", StringComparison.Ordinal)..]);
    }

    [Fact]
    public void LayoutFooter_LinksToTheAboutPage()
    {
        string footer = ReadView("Shared", "_Layout.cshtml");
        int footerStart = footer.IndexOf("<footer", StringComparison.Ordinal);
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
        string path = Path.Combine(SolutionRoot, "PersonalFinance", "src", "PersonalFinance.Web", "Views", folder,
            fileName);
        Assert.True(File.Exists(path), $"{fileName} not found at {path}");
        return File.ReadAllText(path);
    }

    [Fact]
    public void SidebarCss_DefinesRequiredResponsiveAndCollapsedDimensions()
    {
        string cssPath = Path.Combine(SolutionRoot, "PersonalFinance", "src", "PersonalFinance.Web", "wwwroot", "css",
            "sidebar.css");
        Assert.True(File.Exists(cssPath), $"sidebar.css not found at {cssPath}");

        string content = File.ReadAllText(cssPath);

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
        string jsPath = Path.Combine(SolutionRoot, "PersonalFinance", "src", "PersonalFinance.Web", "wwwroot", "js",
            "core", "sidebar.js");
        Assert.True(File.Exists(jsPath), $"sidebar.js not found at {jsPath}");

        string content = File.ReadAllText(jsPath);

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
