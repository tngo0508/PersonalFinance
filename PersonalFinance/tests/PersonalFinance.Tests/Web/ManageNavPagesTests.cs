using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using PersonalFinance.Web.Areas.Identity.Pages.Account.Manage;
using Xunit;

namespace PersonalFinance.Tests.Web;

public class ManageNavPagesTests
{
    [Fact]
    public void IsActive_UsesActivePageFromViewData()
    {
        var viewContext = CreateViewContext("/Account/Manage/SetPassword", activePage: ManageNavPages.ChangePassword);

        Assert.True(ManageNavPages.IsActive(viewContext, ManageNavPages.ChangePassword));
        Assert.False(ManageNavPages.IsActive(viewContext, ManageNavPages.Index));
    }

    [Fact]
    public void IsActive_WithoutActivePage_FallsBackToPageFileName()
    {
        var viewContext = CreateViewContext("/Account/Manage/Email", activePage: null);

        Assert.True(ManageNavPages.IsActive(viewContext, ManageNavPages.Email));
        Assert.False(ManageNavPages.IsActive(viewContext, ManageNavPages.PersonalData));
    }

    private static ViewContext CreateViewContext(string pageDisplayName, string? activePage)
    {
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new ActionDescriptor { DisplayName = pageDisplayName });
        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());
        if (activePage is not null)
        {
            viewData["ActivePage"] = activePage;
        }

        return new ViewContext
        {
            ActionDescriptor = actionContext.ActionDescriptor,
            HttpContext = actionContext.HttpContext,
            RouteData = actionContext.RouteData,
            ViewData = viewData
        };
    }
}
