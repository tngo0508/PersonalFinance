using PersonalFinance.Web.Services;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// The Research Home page's starting mixes: each one is a Fund Basket written as the address writes it.
/// </summary>
public class ResearchBasketPresetsTests
{
    [Fact]
    public void All_HoldsTheFourMixes_InTheAddressFormat()
    {
        Assert.Equal(
            [
                ("Research.Home.Preset.ThreeFund", "VTI:60,VXUS:30,BND:10"),
                ("Research.Home.Preset.SP500Only", "VOO:100"),
                ("Research.Home.Preset.GrowthTilt", "QQQ:50,VTI:50"),
                ("Research.Home.Preset.Income", "SCHD:50,BND:30,VNQ:20"),
            ],
            ResearchBasketPresets.All.Select(p => (p.LabelKey, p.Basket)));
        Assert.All(ResearchBasketPresets.All, p => Assert.Equal(100, p.Funds.Sum(f => f.Weight)));
    }
}
