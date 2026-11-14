using aida.Extensions;
using Microsoft.Extensions.Configuration;

namespace aida.Tests.Extensions;

public class GoogleConfigTests
{
    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value))
            .Build();

    [Fact]
    public void Both_values_present_is_configured()
    {
        var config = Config(
            ("Authentication:Google:ClientId", "id"),
            ("Authentication:Google:ClientSecret", "secret"));

        Assert.True(IdentityExtensions.IsGoogleConfigured(config));
    }

    [Fact]
    public void Missing_secret_is_not_configured()
    {
        var config = Config(("Authentication:Google:ClientId", "id"));

        Assert.False(IdentityExtensions.IsGoogleConfigured(config));
    }

    [Fact]
    public void Empty_values_are_not_configured()
    {
        var config = Config(
            ("Authentication:Google:ClientId", ""),
            ("Authentication:Google:ClientSecret", ""));

        Assert.False(IdentityExtensions.IsGoogleConfigured(config));
    }

    [Fact]
    public void No_values_at_all_is_not_configured()
    {
        Assert.False(IdentityExtensions.IsGoogleConfigured(Config()));
    }
}
