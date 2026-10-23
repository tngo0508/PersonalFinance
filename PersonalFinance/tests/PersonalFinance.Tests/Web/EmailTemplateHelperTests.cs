using PersonalFinance.Web.Services;
using Xunit;

namespace PersonalFinance.Tests.Web;

public class EmailTemplateHelperTests
{
    [Fact]
    public void BuildConfirmationEmail_ContainsBrandedCtaButtonAndConfirmationUrl()
    {
        string testUrl = "https://localhost:7100/Identity/Account/ConfirmEmail?userId=123&code=abc%3D%3D";
        string appName = "PersonalFinance";

        string html = EmailTemplateHelper.BuildConfirmationEmail(testUrl, appName);

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
        string testUrl = "https://example.com/confirm";

        string html = EmailTemplateHelper.BuildConfirmationEmail(testUrl, null);

        Assert.Contains("PersonalFinance", html);
        Assert.Contains("https://example.com/confirm", html);
    }

    [Fact]
    public void BuildConfirmationEmail_EscapesHtmlCharactersInParameters()
    {
        string maliciousAppName = "<script>alert('xss')</script>App";
        string testUrl = "https://example.com/confirm?token=<test>";

        string html = EmailTemplateHelper.BuildConfirmationEmail(testUrl, maliciousAppName);

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt;App", html);
        Assert.Contains("https://example.com/confirm?token=&lt;test&gt;", html);
    }
}
