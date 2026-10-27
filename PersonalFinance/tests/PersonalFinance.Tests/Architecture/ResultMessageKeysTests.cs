using System.Reflection;
using System.Text.RegularExpressions;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Architecture;

/// <summary>
/// Every key a successful result's error or warning can carry (<see cref="ResultMessageKeys"/>, #405) resolves to an
/// English message in the Web app's <c>en.json</c> and to a Vietnamese one in <c>vi.json</c>, so a reader never sees a
/// raw key or English under <c>vi</c>. Each follows the naming rule <c>&lt;Feature&gt;.Result.&lt;Rule&gt;</c> under
/// the nested class of its Feature and never repeats an <see cref="ApiErrorKeys"/> value.
/// </summary>
public class ResultMessageKeysTests
{
    private static readonly Regex KeyName = new(
        @"^[A-Z][A-Za-z]+\.Result\.[A-Z][A-Za-z]+$",
        RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    public static TheoryData<string> Keys => new(AllKeys().Select(entry => entry.Value));

    [Theory]
    [MemberData(nameof(Keys))]
    public void EveryKey_HasAnEnglishAndAVietnameseEntry(string key)
    {
        Assert.True(TestLocalization.Catalog.TryGet("en", key, out string _), $"en.json has no {key}.");
        Assert.True(TestLocalization.Catalog.TryGet("vi", key, out string _), $"vi.json has no {key}.");
    }

    [Fact]
    public void EveryKey_FollowsTheNamingRule()
    {
        List<string> offenders = AllKeys()
            .Where(entry => !KeyName.IsMatch(entry.Value) || !entry.Value.StartsWith(entry.Feature + ".", StringComparison.Ordinal))
            .Select(entry => $"ResultMessageKeys.{entry.Feature}: {entry.Value}")
            .ToList();

        Assert.True(offenders.Count == 0,
            "Name a key <Feature>.Result.<Rule> under its Feature's nested class:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void NoKey_IsDeclaredTwice()
    {
        List<string> duplicates = AllKeys()
            .GroupBy(entry => entry.Value, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.True(duplicates.Count == 0, "These ResultMessageKeys values are declared more than once:\n" + string.Join("\n", duplicates));
    }

    private static List<(string Feature, string Value)> AllKeys()
    {
        List<(string Feature, string Value)> keys = typeof(ResultMessageKeys).GetNestedTypes()
            .SelectMany(feature => feature.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .Select(field => (feature.Name, (string)field.GetRawConstantValue()!)))
            .ToList();

        // Guards against a vacuous pass if reflection stops finding the nested Feature classes.
        Assert.NotEmpty(keys);
        return keys;
    }
}
