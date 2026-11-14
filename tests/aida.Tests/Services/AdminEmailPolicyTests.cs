using aida.Extensions;
using Microsoft.Extensions.Configuration;

namespace aida.Tests.Services;

public class AdminEmailPolicyTests
{
    private static IConfiguration Config(params string[] emails)
    {
        var values = new Dictionary<string, string?>();
        for (var i = 0; i < emails.Length; i++)
            values[$"Admin:Emails:{i}"] = emails[i];

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Theory]
    [InlineData("admin@example.com")]
    [InlineData("ADMIN@example.com")]   // case-insensitive
    public void Listed_email_is_admin(string email)
    {
        Assert.True(AuthEndpointExtensions.IsAdminEmail(Config("admin@example.com"), email));
    }

    [Fact]
    public void Unlisted_email_is_not_admin()
    {
        Assert.False(AuthEndpointExtensions.IsAdminEmail(Config("admin@example.com"), "someone else@example.com"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_email_is_never_admin(string? email)
    {
        Assert.False(AuthEndpointExtensions.IsAdminEmail(Config("admin@example.com"), email));
    }

    [Fact]
    public void Empty_admin_list_makes_nobody_admin()
    {
        Assert.False(AuthEndpointExtensions.IsAdminEmail(Config(), "admin@example.com"));
    }

    [Fact]
    public void Blank_config_entries_are_ignored()
    {
        Assert.False(AuthEndpointExtensions.IsAdminEmail(Config("", "  "), "admin@example.com"));
    }
}
