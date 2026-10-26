using System.Reflection;
using System.Text.RegularExpressions;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Architecture;

/// <summary>
/// Every key an API rejection can carry (<see cref="ApiErrorKeys"/>, localized API errors spec §2.4) resolves to an
/// English message in the Web app's <c>en.json</c>, so a reader never sees a raw key, and follows the naming rule
/// <c>&lt;Feature&gt;.Validation|Error.&lt;Rule&gt;</c> under the nested class of its Feature, so two Features cannot
/// share or collide on a key.
/// </summary>
public class ApiErrorKeysTests
{
    private static readonly Regex KeyName = new(
        @"^[A-Z][A-Za-z]+\.(Validation|Error)\.[A-Z][A-Za-z]+$",
        RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    [Fact]
    public void EveryApiErrorKey_HasAnEnglishEntry()
    {
        List<string> missing = AllKeys()
            .Where(entry => !TestLocalization.Catalog.TryGet("en", entry.Value, out string _))
            .Select(entry => entry.Value)
            .ToList();

        Assert.True(missing.Count == 0, "en.json has no entry for these ApiErrorKeys:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void EveryApiErrorKey_FollowsTheNamingRule()
    {
        List<string> offenders = AllKeys()
            .Where(entry => !KeyName.IsMatch(entry.Value) || !entry.Value.StartsWith(entry.Feature + ".", StringComparison.Ordinal))
            .Select(entry => $"ApiErrorKeys.{entry.Feature}: {entry.Value}")
            .ToList();

        Assert.True(offenders.Count == 0,
            "Name a key <Feature>.Validation.<Rule> or <Feature>.Error.<Rule> under its Feature's nested class:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void ApiErrorKeys_HasNoDuplicateValues()
    {
        List<string> duplicates = AllKeys()
            .GroupBy(entry => entry.Value, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.True(duplicates.Count == 0, "These ApiErrorKeys values are declared more than once:\n" + string.Join("\n", duplicates));
    }

    private static IEnumerable<(string Feature, string Value)> AllKeys() =>
        typeof(ApiErrorKeys).GetNestedTypes()
            .SelectMany(feature => feature.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .Select(field => (feature.Name, (string)field.GetRawConstantValue()!)));
}
