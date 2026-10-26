using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Infrastructure.Localization;
using PersonalFinance.Web.Models.Budget;

namespace PersonalFinance.Tests.BudgetPlanner;

/// <summary>
/// The Budget Planner page in every supported language (spec §2.1, §3 L3): its view, its script and its form's field
/// names and validation messages are localizer keys that English and Vietnamese both define, and no money on the page
/// is written with a hard-coded dollar sign.
/// </summary>
public partial class BudgetPlannerLanguageTests
{
    private static readonly string WebRoot = RepositoryRoot.SourceProject("PersonalFinance.Web");
    private static readonly string ViewPath = Path.Combine(WebRoot, "Views", "Budget", "Index.cshtml");
    private static readonly string ScriptPath = Path.Combine(WebRoot, "wwwroot", "js", "budget", "budget.js");

    [Fact]
    public void EveryKeyTheViewUses_HasEnglishAndVietnameseText()
    {
        List<string> keys = RazorKey().Matches(File.ReadAllText(ViewPath)).Select(m => m.Groups["key"].Value)
            .Distinct().ToList();

        Assert.Contains("BudgetPlanner.Title", keys);
        AssertEnglishAndVietnamese(keys);
    }

    [Fact]
    public void EveryKeyTheScriptUses_IsOnThePage_AndHasEnglishAndVietnameseText()
    {
        List<string> keys = ScriptKey().Matches(File.ReadAllText(ScriptPath)).Select(m => m.Groups["key"].Value)
            .Distinct().ToList();

        Assert.NotEmpty(keys);
        Assert.All(keys, key => Assert.StartsWith("BudgetPlanner.Js.", key, StringComparison.Ordinal));
        Assert.Contains("<js-strings prefix=\"BudgetPlanner.Js.\"", File.ReadAllText(ViewPath), StringComparison.Ordinal);
        AssertEnglishAndVietnamese(keys);
    }

    [Fact]
    public void FormFieldNamesAndValidationMessages_AreKeys_WithEnglishAndVietnameseText()
    {
        List<PropertyInfo> fields = typeof(BudgetViewModel).GetProperties()
            .Where(p => p.PropertyType == typeof(decimal) && p.CanWrite)
            .ToList();
        List<string> keys = fields
            .SelectMany(p => p.GetCustomAttributes<ValidationAttribute>().Select(a => a.ErrorMessage)
                .Concat(p.GetCustomAttributes<DisplayAttribute>().Select(a => a.Name)))
            .Select(text => text ?? string.Empty)
            .ToList();

        Assert.Equal(11, fields.Count);
        Assert.All(keys, key => Assert.True(JsonStringLocalizer.IsKey(key), $"\"{key}\" is English text, not a key."));
        AssertEnglishAndVietnamese(keys.Distinct().ToList());
    }

    [Fact]
    public void ThePage_WritesNoDollarSignOfItsOwn()
    {
        // Symbols come from the money formatter (PFI18n.formatMoney / MoneyFormatter) or SupportedCurrencies.
        Assert.DoesNotContain("$", DollarFree(File.ReadAllText(ViewPath)), StringComparison.Ordinal);
        Assert.DoesNotContain("$", DollarFree(File.ReadAllText(ScriptPath)), StringComparison.Ordinal);
    }

    private static void AssertEnglishAndVietnamese(List<string> keys)
    {
        Assert.All(keys, key =>
        {
            Assert.True(TestLocalization.Catalog.TryGet("en", key, out _), $"en.json has no {key}.");
            Assert.True(TestLocalization.Catalog.TryGet("vi", key, out _), $"vi.json has no {key}.");
        });
    }

    // Template literals (`${x}`) and regex anchors are code, not text.
    private static string DollarFree(string source) => CodeDollar().Replace(source, string.Empty);

    // L["Key"], L.Named("Key", ...) and L.NamedHtml("Key", ...).
    [GeneratedRegex(@"\bL(?:\[|\.Named\(|\.NamedHtml\()\s*""(?<key>[A-Z][A-Za-z0-9]*(?:\.[A-Za-z0-9]+)+)""")]
    private static partial Regex RazorKey();

    // PFI18n.t('Key') and PFI18n.t('Key', { ... }).
    [GeneratedRegex(@"PFI18n\.t\(\s*'(?<key>[A-Z][A-Za-z0-9]*(?:\.[A-Za-z0-9]+)+)'")]
    private static partial Regex ScriptKey();

    [GeneratedRegex(@"\$\{|\$'|\$/")]
    private static partial Regex CodeDollar();
}
