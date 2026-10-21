using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Tests.Security;

public class UserIdForwardingHandlerTests
{
    [Fact]
    public async Task SignedInAccount_IsForwardedWithInternalKey()
    {
        var sent = await SendAsync(new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "account-1") }, "Cookies")));

        Assert.Equal("account-1", sent.Headers.GetValues("X-User-Id").Single());
        Assert.Equal("shared-secret", sent.Headers.GetValues("X-Internal-Key").Single());
    }

    [Fact]
    public async Task SignedOutVisitor_IsNotForwardedAsAnAccount()
    {
        var sent = await SendAsync(new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.False(sent.Headers.Contains("X-User-Id"));
    }

    private static async Task<HttpRequestMessage> SendAsync(ClaimsPrincipal user)
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = user } };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Internal:ApiKey"] = "shared-secret" })
            .Build();
        HttpRequestMessage? captured = null;
        var handler = new UserIdForwardingHandler(accessor, configuration)
        {
            InnerHandler = new DelegatingTestHandler((request, _) =>
            {
                captured = request;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            })
        };

        using var invoker = new HttpMessageInvoker(handler);
        await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://apiservice/api/test"), CancellationToken.None);
        return captured!;
    }
}
