using PersonalFinance.Web.Models.Research;

namespace PersonalFinance.Web.Services;

/// <summary>
/// The starting mixes the Research Home page offers for the Fund Basket. The page hides a mix whose funds are not all
/// Tracked Funds.
/// </summary>
public static class ResearchBasketPresets
{
    /// <summary>Every mix, in the order the page shows them.</summary>
    public static IReadOnlyList<ResearchBasketPreset> All { get; } =
    [
        new("Research.Home.Preset.ThreeFund", [("VTI", 60), ("VXUS", 30), ("BND", 10)]),
        new("Research.Home.Preset.SP500Only", [("SPY", 100)]),
        new("Research.Home.Preset.GrowthTilt", [("QQQ", 50), ("VTI", 50)]),
        new("Research.Home.Preset.Income", [("SCHD", 50), ("BND", 30), ("VNQ", 20)]),
    ];
}
