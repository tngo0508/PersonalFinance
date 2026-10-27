using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Infrastructure;
using Refit;

namespace PersonalFinance.Tests.Web;

/// <summary>
/// The Admin area is not localized (ADR 0017), so its English rejection reason (an <c>AdminErrorDto</c>) is read by
/// the Admin controller itself and shown as sent; <see cref="ApiErrorMessages"/> no longer reads unkeyed bodies.
/// </summary>
public class AdminRejectionMessageTests
{
    [Fact]
    public async Task ARejectedRoleOperation_ShowsTheAdminReasonAsSent()
    {
        AdminController controller = CreateController(await FailureAsync(HttpStatusCode.BadRequest,
            """{"error":"Built-in roles cannot be deleted."}"""));

        await controller.DeleteRole("Admin");

        Assert.Equal("Built-in roles cannot be deleted.", controller.TempData["AdminError"]);
    }

    [Fact]
    public async Task ARejectionWithoutAReason_ShowsTheStatusMessage()
    {
        AdminController controller = CreateController(await FailureAsync(HttpStatusCode.NotFound, string.Empty));

        await controller.DeleteRole("Ghost");

        Assert.Equal(TestLocalization.Localizer["Errors.NotFound"].Value, controller.TempData["AdminError"]);
    }

    [Fact]
    public async Task AServerError_NeverShowsItsBody()
    {
        AdminController controller = CreateController(await FailureAsync(HttpStatusCode.InternalServerError,
            """{"error":"NullReferenceException at AdminService.cs:42"}"""));

        await controller.DeleteRole("Admin");

        Assert.Equal("The operation failed. Try again later.", controller.TempData["AdminError"]);
    }

    private static AdminController CreateController(ApiException failure)
    {
        (IAdminApi adminApi, ApiProxy<IAdminApi> proxy) = ApiProxy.Create<IAdminApi>();
        proxy.Throws(nameof(IAdminApi.DeleteRoleAsync), failure);
        (IFeedbackApi feedbackApi, ApiProxy<IFeedbackApi> _) = ApiProxy.Create<IFeedbackApi>();
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "admin-user")], "TestAuthType");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        return new AdminController(adminApi, feedbackApi, NullLogger<AdminController>.Instance, TestLocalization.Localizer)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
        };
    }

    private static async Task<ApiException> FailureAsync(HttpStatusCode status, string content)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, "http://localhost/api/admin/roles/x");
        using var response = new HttpResponseMessage(status)
        {
            RequestMessage = request,
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };
        return await ApiException.Create(request, HttpMethod.Delete, response, new RefitSettings());
    }
}
