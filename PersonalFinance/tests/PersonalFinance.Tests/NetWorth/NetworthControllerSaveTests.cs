using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.NetWorth;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Models.Networth;

namespace PersonalFinance.Tests.NetWorth;

/// <summary>
/// A failed save shows the page again with the user's input. Where the presets go (inline, or the More menu) still
/// follows whether the Account has saved parameters (Reduce Clutter 5/7, #453).
/// </summary>
public class NetworthControllerSaveTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveParameters_WhenTheApiFails_KnowsWhetherTheAccountHasSavedParameters(bool hasSaved)
    {
        (INetWorthApi api, ApiProxy<INetWorthApi> proxy) = ApiProxy.Create<INetWorthApi>();
        proxy.Throws(nameof(INetWorthApi.SaveParametersAsync), new InvalidOperationException("boom"));
        proxy.Returns(nameof(INetWorthApi.LoadParametersAsync), hasSaved ? Saved() : null);
        proxy.Returns(nameof(INetWorthApi.CalculateProjectionAsync), new NetWorthProjectionDto());

        IActionResult result = await WithContext(api).SaveParameters(new NetWorthViewModel { StartingBalance = 1m });

        NetWorthViewModel model = Assert.IsType<NetWorthViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(hasSaved, model.HasSavedParameters);
        Assert.Equal(1m, model.StartingBalance);
    }

    [Fact]
    public async Task SaveParameters_WhenInvalid_KnowsTheAccountHasSavedParameters()
    {
        (INetWorthApi api, ApiProxy<INetWorthApi> proxy) = ApiProxy.Create<INetWorthApi>();
        proxy.Returns(nameof(INetWorthApi.LoadParametersAsync), Saved());
        proxy.Returns(nameof(INetWorthApi.CalculateProjectionAsync), new NetWorthProjectionDto());
        NetworthController controller = WithContext(api);
        controller.ModelState.AddModelError(nameof(NetWorthViewModel.CurrentAge), "invalid");

        IActionResult result = await controller.SaveParameters(new NetWorthViewModel());

        NetWorthViewModel model = Assert.IsType<NetWorthViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.HasSavedParameters);
        Assert.DoesNotContain(nameof(INetWorthApi.SaveParametersAsync), proxy.Calls);
    }

    [Fact]
    public async Task SaveParameters_WhenTheSavedStateCannotLoad_ShowsThePageWithTheInlinePresets()
    {
        (INetWorthApi api, ApiProxy<INetWorthApi> proxy) = ApiProxy.Create<INetWorthApi>();
        proxy.Throws(nameof(INetWorthApi.SaveParametersAsync), new InvalidOperationException("boom"));
        proxy.Throws(nameof(INetWorthApi.LoadParametersAsync), new InvalidOperationException("boom"));
        proxy.Returns(nameof(INetWorthApi.CalculateProjectionAsync), new NetWorthProjectionDto());

        IActionResult result = await WithContext(api).SaveParameters(new NetWorthViewModel());

        NetWorthViewModel model = Assert.IsType<NetWorthViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.False(model.HasSavedParameters);
    }

    private static NetWorthParametersDto Saved() => new() { CurrencyCode = "USD", CurrentAge = 35, RetirementAge = 60 };

    private static NetworthController WithContext(INetWorthApi api)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "nw-user")], "Test"))
        };
        return new NetworthController(api, NullLogger<NetworthController>.Instance, TestLocalization.Localizer)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
        };
    }
}
