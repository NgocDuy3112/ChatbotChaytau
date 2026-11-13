using chatbotchaytau.Extensions;

namespace chatbotchaytau.Tests.Extensions;

public class SafeReturnUrlTests
{
    [Theory]
    // Legitimate local targets pass through unchanged.
    [InlineData("/", "/")]
    [InlineData("/chat", "/chat")]
    [InlineData("/chat?tab=1", "/chat?tab=1")]
    [InlineData("/a/b/c", "/a/b/c")]
    // Missing/empty values fall back to the site root.
    [InlineData(null, "/")]
    [InlineData("", "/")]
    // Open-redirect attempts are rejected.
    [InlineData("//evil.com", "/")]
    [InlineData("/\\evil.com", "/")]
    [InlineData("https://evil.com", "/")]
    [InlineData("http://evil.com/phish", "/")]
    [InlineData("evil.com", "/")]
    [InlineData("\\\\evil.com", "/")]
    public void SafeReturnUrl(string? input, string expected)
    {
        var actual = AuthEndpointExtensions.SafeReturnUrl(input);
        Assert.Equal(expected, actual);
    }
}
