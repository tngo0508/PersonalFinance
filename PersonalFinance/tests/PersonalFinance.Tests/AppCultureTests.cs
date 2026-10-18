using System.Globalization;
using PersonalFinance.Shared.Constants;

namespace PersonalFinance.Tests;

/// <summary>
/// Regression tests for the "¤1,234.56" currency bug: chiseled containers start in the invariant culture,
/// so currency formatting must be pinned to en-US at startup.
/// </summary>
[Collection("Culture")]
public class AppCultureTests : IDisposable
{
    private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;
    private readonly CultureInfo? _originalDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;
    private readonly CultureInfo? _originalDefaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;

    public void Dispose()
    {
        CultureInfo.DefaultThreadCurrentCulture = _originalDefaultCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _originalDefaultUiCulture;
        CultureInfo.CurrentCulture = _originalCulture;
        CultureInfo.CurrentUICulture = _originalUiCulture;
    }

    [Fact]
    public void ApplyDefault_FromInvariantCulture_FormatsCurrencyWithDollarSign()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        Assert.StartsWith("¤", 1234.56m.ToString("C"));

        AppCulture.ApplyDefault();

        Assert.Equal("$1,234.56", 1234.56m.ToString("C"));
        Assert.Equal("en-US", CultureInfo.CurrentCulture.Name);
        Assert.Equal("en-US", CultureInfo.CurrentUICulture.Name);
    }

    [Fact]
    public void ApplyDefault_FromInvariantCulture_AppliesToThreadsThatDoNotInheritCulture()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        AppCulture.ApplyDefault();

        string? formatted = null;
        string? cultureName = null;
        var thread = new Thread(() =>
        {
            formatted = 1234.56m.ToString("C");
            cultureName = CultureInfo.CurrentCulture.Name;
        });

        // A new thread must pick up DefaultThreadCurrentCulture, not the async-local culture of this test.
        using (ExecutionContext.SuppressFlow())
        {
            thread.Start();
        }

        thread.Join();

        Assert.Equal("$1,234.56", formatted);
        Assert.Equal("en-US", cultureName);
    }

    [Fact]
    public void Default_UsesDollarCurrencySymbol()
    {
        Assert.Equal("en-US", AppCulture.DefaultCultureName);
        Assert.Equal("$", AppCulture.Default.NumberFormat.CurrencySymbol);
    }
}
