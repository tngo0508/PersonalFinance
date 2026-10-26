using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.NetWorth;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models.Networth;

namespace PersonalFinance.Tests.NetWorth;

/// <summary>
/// The messages the Net Worth controller shows (save result, Unavailable State) follow the UI language (slice L3, #363).
/// </summary>
public class NetworthControllerLanguageTests
{
    [Fact]
    public async Task SaveParameters_InVietnamese_ConfirmsInVietnamese()
    {
        (INetWorthApi api, ApiProxy<INetWorthApi> proxy) = ApiProxy.Create<INetWorthApi>();
        proxy.Returns(nameof(INetWorthApi.SaveParametersAsync), new NetWorthParametersDto());
        NetworthController controller = WithContext(api);

        using (new UiCultureScope("vi"))
        {
            await controller.SaveParameters(new NetWorthViewModel());
        }

        Assert.Equal(TestLocalization.Vietnamese("NetWorth.Message.Saved"), controller.TempData["SaveSuccess"]);
    }

    [Fact]
    public async Task DeleteParameters_WhenApiFails_InVietnamese_ExplainsInVietnamese()
    {
        (INetWorthApi api, ApiProxy<INetWorthApi> proxy) = ApiProxy.Create<INetWorthApi>();
        // Not a connection failure (that one has its own shared message): the page's own fallback applies.
        proxy.Throws(nameof(INetWorthApi.DeleteParametersAsync), new InvalidOperationException("boom"));
        NetworthController controller = WithContext(api);

        using (new UiCultureScope("vi"))
        {
            await controller.DeleteParameters();
        }

        Assert.Equal(TestLocalization.Vietnamese("NetWorth.Message.ClearFailed"), controller.TempData["SaveError"]);
    }

    [Fact]
    public async Task Index_WhenApiFails_InVietnamese_ShowsTheUnavailableStateInVietnamese()
    {
        (INetWorthApi api, ApiProxy<INetWorthApi> _) = ApiProxy.Create<INetWorthApi>();
        NetworthController controller = WithContext(api);

        IActionResult result;
        using (new UiCultureScope("vi"))
        {
            result = await controller.Index();
        }

        UnavailableStateViewModel model = Assert.IsType<UnavailableStateViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(TestLocalization.Vietnamese("NetWorth.Title"), model.PageTitle);
        Assert.Equal(TestLocalization.Vietnamese("NetWorth.Message.Unavailable"), model.Heading);
    }

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
