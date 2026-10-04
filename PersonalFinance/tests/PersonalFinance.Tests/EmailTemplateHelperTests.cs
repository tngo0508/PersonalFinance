using PersonalFinance.Web.Services;
using Xunit;

namespace PersonalFinance.Tests;

public class EmailTemplateHelperTests
{
    [Fact]
    public void BuildConfirmationEmail_ContainsBrandedCtaButtonAndConfirmationUrl()
    {
        var testUrl = "https://localhost:7100/Identity/Account/ConfirmEmail?userId=123&code=abc%3D%3D";
        var appName = "PersonalFinance";

        var html = EmailTemplateHelper.BuildConfirmationEmail(testUrl, appName);

        Assert.NotNull(html);
        Assert.Contains("PersonalFinance", html);
        Assert.Contains("Confirm your email address", html);
        Assert.Contains("Confirm Email Address", html);
        Assert.Contains("background-color: #0d6efd", html);
        Assert.Contains("https://localhost:7100/Identity/Account/ConfirmEmail?userId=123&amp;code=abc%3D%3D", html);
    }

    [Fact]
    public void BuildConfirmationEmail_WhenAppNameIsNull_DefaultsToPersonalFinance()
    {
        var testUrl = "https://example.com/confirm";

        var html = EmailTemplateHelper.BuildConfirmationEmail(testUrl, null);

        Assert.Contains("PersonalFinance", html);
        Assert.Contains("https://example.com/confirm", html);
    }

    [Fact]
    public void BuildConfirmationEmail_EscapesHtmlCharactersInParameters()
    {
        var maliciousAppName = "<script>alert('xss')</script>App";
        var testUrl = "https://example.com/confirm?token=<test>";

        var html = EmailTemplateHelper.BuildConfirmationEmail(testUrl, maliciousAppName);

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt;App", html);
        Assert.Contains("https://example.com/confirm?token=&lt;test&gt;", html);
    }
}
