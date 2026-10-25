using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Infrastructure.Localization;

namespace PersonalFinance.Tests.Web.Localization;

/// <summary>
/// The model-binding messages and Identity errors go through the localizer, and in English read exactly as the
/// framework's own, so switching them over changed nothing for English users.
/// </summary>
public class FrameworkMessagesTests
{
    private static readonly MethodInfo[] ModelBindingAccessors = typeof(DefaultModelBindingMessageProvider)
        .GetProperties()
        .Where(p => p.Name.EndsWith("Accessor", StringComparison.Ordinal))
        .Select(p => p.GetMethod!)
        .ToArray();

    [Fact]
    public void ModelBindingMessages_AllElevenAreReplaced_AndMatchTheFrameworkInEnglish()
    {
        var framework = new DefaultModelBindingMessageProvider();
        var options = new MvcOptions();
        new ModelBindingMessagesSetup(TestLocalization.Localizer).Configure(options);
        using var culture = new UiCultureScope("en");

        Assert.Equal(11, ModelBindingAccessors.Length);
        foreach (MethodInfo accessor in ModelBindingAccessors)
        {
            Assert.Equal(Invoke(accessor, framework), Invoke(accessor, options.ModelBindingMessageProvider));
        }
    }

    [Fact]
    public void ModelBindingMessages_FollowTheUiCulture()
    {
        var options = new MvcOptions();
        new ModelBindingMessagesSetup(TestLocalization.Localizer).Configure(options);

        string english;
        using (new UiCultureScope("en"))
        {
            english = options.ModelBindingMessageProvider.ValueMustBeANumberAccessor("Amount");
        }

        using var culture = new UiCultureScope("vi");
        string vietnamese = options.ModelBindingMessageProvider.ValueMustBeANumberAccessor("Amount");
        Assert.Contains("Amount", vietnamese);
        Assert.NotEqual(english, vietnamese);
    }

    [Fact]
    public void IdentityErrorDescriber_OverridesEveryMessage()
    {
        string[] virtuals = typeof(IdentityErrorDescriber)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.IsVirtual && m.ReturnType == typeof(IdentityError))
            .Select(m => m.Name)
            .ToArray();
        string[] overridden = typeof(LocalizedIdentityErrorDescriber)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.ReturnType == typeof(IdentityError))
            .Select(m => m.Name)
            .ToArray();

        Assert.Empty(virtuals.Except(overridden));
    }

    [Fact]
    public void IdentityErrorDescriber_InEnglish_MatchesTheFrameworkTextAndCode()
    {
        var framework = new IdentityErrorDescriber();
        var localized = new LocalizedIdentityErrorDescriber(TestLocalization.Localizer);
        using var culture = new UiCultureScope("en");

        foreach (MethodInfo method in typeof(IdentityErrorDescriber).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                     .Where(m => m.IsVirtual && m.ReturnType == typeof(IdentityError)))
        {
            object?[] args = method.GetParameters()
                .Select(p => p.ParameterType == typeof(int) ? (object)8 : "someone@example.com")
                .ToArray();
            var expected = (IdentityError)method.Invoke(framework, args)!;
            var actual = (IdentityError)method.Invoke(localized, args)!;

            Assert.Equal(expected.Code, actual.Code);
            Assert.Equal(expected.Description, actual.Description);
        }
    }

    private static string Invoke(MethodInfo accessor, DefaultModelBindingMessageProvider provider)
    {
        Delegate func = (Delegate)accessor.Invoke(provider, null)!;
        object[] args = func.GetType().GetMethod("Invoke")!.GetParameters().Select((_, i) => (object)$"arg{i}").ToArray();
        return (string)func.DynamicInvoke(args)!;
    }
}
