using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Features.BudgetPlanner;
using PersonalFinance.ApiService.Features.BudgetTracker;
using PersonalFinance.ApiService.Features.Dashboard;
using PersonalFinance.ApiService.Features.GoogleDrive;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Models.Feedback;
using BudgetController = PersonalFinance.Web.Controllers.BudgetController;
using BudgetTrackerController = PersonalFinance.Web.Controllers.BudgetTrackerController;
using DashboardController = PersonalFinance.Web.Controllers.DashboardController;
using GoogleDriveController = PersonalFinance.Web.Controllers.GoogleDriveController;

namespace PersonalFinance.Tests.Web;

public class NavigationLayoutTests
{
    private static readonly string SolutionRoot = RepositoryRoot.Find();

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
            typeof(ResearchController),
            typeof(MoneyPrioritiesController)
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
        // Resources is the last sidebar group, so it runs to the end of the nav.
        string resources = Between(sidebar, "@L[\"Shell.Nav.Resources\"]</div>", "</nav>");

        Assert.Contains("asp-controller=\"Research\" asp-action=\"Index\"", resources);
        Assert.Contains("currentController == \"Research\" ? \"active\"", resources);
        Assert.Contains("<span class=\"sidebar-link-text\">@L[\"Shell.Nav.Research\"]</span>", resources);
    }

    [Fact]
    public void Sidebar_LinksToMoneyPriorities_InMyFinances_AfterSavingsGoals()
    {
        string sidebar = Between(ReadView("Shared", "_Layout.cshtml"), "<aside", "</aside>");
        string myFinances = Between(sidebar, "@L[\"Shell.Nav.MyFinances\"]</div>", "<div class=\"sidebar-group\">");

        int savingsGoals = myFinances.IndexOf("asp-controller=\"SavingsGoals\"", StringComparison.Ordinal);
        int moneyPriorities = myFinances.IndexOf("asp-controller=\"MoneyPriorities\" asp-action=\"Index\"",
            StringComparison.Ordinal);
        int analysis = myFinances.IndexOf("asp-controller=\"Analysis\"", StringComparison.Ordinal);
        Assert.True(savingsGoals >= 0 && moneyPriorities > savingsGoals && analysis > moneyPriorities,
            "Money Priorities should sit in My Finances between Savings Goals and Analysis.");
        Assert.Contains("currentController == \"MoneyPriorities\" ? \"active\"", myFinances);
        Assert.Contains("<i data-lucide=\"signpost\"></i>", myFinances);
        Assert.Contains("<span class=\"sidebar-link-text\">@L[\"Shell.Nav.MoneyPriorities\"]</span>", myFinances);
    }

    [Fact]
    public void SidebarLinkLabels_AreShellNavResources()
    {
        // Only English is required: a missing translation falls back to English and never fails a build (ADR 0015).
        string sidebar = Between(ReadView("Shared", "_Layout.cshtml"), "<aside", "</aside>");
        MatchCollection labels = Regex.Matches(sidebar, "<span class=\"sidebar-link-text\">(.*?)</span>");
        Assert.NotEmpty(labels);

        foreach (Match label in labels)
        {
            Match key = Regex.Match(label.Groups[1].Value, "^@L\\[\"(Shell\\.Nav\\.[A-Za-z0-9]+)\"\\]$");
            Assert.True(key.Success, $"Sidebar label {label.Groups[1].Value} should be a Shell.Nav.* resource.");
            Assert.True(TestLocalization.Catalog.TryGet("en", key.Groups[1].Value, out _),
                $"en.json has no {key.Groups[1].Value}.");
        }
    }

    [Fact]
    public void BottomNav_LightsFinancesOnMoneyPriorities()
    {
        string bottomNav = Between(ReadView("Shared", "_Layout.cshtml"), "<nav class=\"bottom-nav\"", "</nav>");

        string finances = bottomNav[..bottomNav.IndexOf("<span>@L[\"Shell.BottomNav.Finances\"]</span>", StringComparison.Ordinal)];
        finances = finances[finances.LastIndexOf("<a ", StringComparison.Ordinal)..];
        Assert.Contains("currentController == \"MoneyPriorities\"", finances);
    }

    [Fact]
    public void BottomNav_KeepsFiveItems_AndLightsToolsOnResearch()
    {
        string bottomNav = Between(ReadView("Shared", "_Layout.cshtml"), "<nav class=\"bottom-nav\"", "</nav>");

        Assert.Equal(5, Regex.Count(bottomNav, "class=\"bottom-nav-item"));
        string tools = bottomNav[..bottomNav.IndexOf("<span>@L[\"Shell.BottomNav.Tools\"]</span>", StringComparison.Ordinal)];
        tools = tools[tools.LastIndexOf("<a ", StringComparison.Ordinal)..];
        Assert.Contains("currentController == \"Research\"", tools);
    }

    [Fact]
    public void SidebarHoldingResearch_OpensFromAMenuToggleOnPhones()
    {
        // Research is not in the five-item bottom nav: on phones it is reached through the off-canvas sidebar, which
        // the top bar's menu button opens below the md breakpoint (css/sidebar.css slides it in on .mobile-open).
        string layout = ReadView("Shared", "_Layout.cshtml");
        string topbar = Between(layout, "<header class=\"topbar\"", "</header>");

        Match toggle = Regex.Match(topbar, "<button id=\"mobileMenuToggle\"[^>]*>");
        Assert.True(toggle.Success, "The top bar should hold the mobile menu toggle.");
        Assert.Contains("d-md-none", toggle.Value);
        Assert.Contains("aria-controls=\"sidebar\"", toggle.Value);
        Assert.Contains("asp-controller=\"Research\"", Between(layout, "<aside id=\"sidebar\"", "</aside>"));
        string sidebarScript = File.ReadAllText(Path.Combine(SolutionRoot, "PersonalFinance", "src", "PersonalFinance.Web",
            "wwwroot", "js", "core", "sidebar.js"));
        Assert.Contains("getElementById('mobileMenuToggle')", sidebarScript);
        Assert.Contains("classList.toggle('mobile-open')", sidebarScript);
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
    public void LayoutView_LinksSignedInAccountsToFeedback_FromTheFooterOnly()
    {
        string content = ReadView("Shared", "_Layout.cshtml");

        string sidebar = content[content.IndexOf("<aside", StringComparison.Ordinal)..content.IndexOf("</aside>", StringComparison.Ordinal)];
        Assert.DoesNotContain("asp-controller=\"Feedback\"", sidebar);

        string footer = content[content.IndexOf("<footer", StringComparison.Ordinal)..content.IndexOf("</footer>", StringComparison.Ordinal)];
        int footerLink = footer.IndexOf("asp-controller=\"Feedback\"", StringComparison.Ordinal);
        Assert.True(footerLink >= 0, "The footer should link to Feedback.");
        Assert.True(footer.LastIndexOf("@if (isSignedIn)", footerLink, StringComparison.Ordinal) >= 0,
            "The footer Feedback link should only be shown to signed-in Accounts.");
    }

    [Fact]
    public void AdminDashboard_IsLinkedFromTheAccountMenuForAdmins_NotTheSidebar()
    {
        string sidebar = Between(ReadView("Shared", "_Layout.cshtml"), "<aside", "</aside>");
        Assert.DoesNotContain("asp-controller=\"Admin\"", sidebar);

        string accountMenu = ReadView("Shared", "_LoginPartial.cshtml");
        int adminLink = accountMenu.IndexOf("asp-controller=\"Admin\" asp-action=\"Index\"", StringComparison.Ordinal);
        Assert.True(adminLink >= 0, "The account menu should link to the Admin dashboard.");
        Assert.True(accountMenu.LastIndexOf("@if (isAdmin)", adminLink, StringComparison.Ordinal) >= 0,
            "The account menu Admin link should only be shown to admins.");
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
        Assert.Contains("L[\"About.Features.NetWorth.Text\"]", about);

        Assert.True(TestLocalization.Catalog.TryGet("en", "About.Features.NetWorth.Text", out string netWorth));
        Assert.DoesNotContain("assets and liabilities", netWorth);
        Assert.Contains("financial independence", netWorth);
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
