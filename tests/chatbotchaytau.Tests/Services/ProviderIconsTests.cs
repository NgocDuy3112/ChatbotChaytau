using chatbotchaytau.Services;

namespace chatbotchaytau.Tests.Services;

public class ProviderIconsTests
{
    // The providers currently configured in appsettings ChatModels — if this
    // test fails after a config edit, the new provider just gets a monogram
    // fallback (which is fine); it fails only if an embedded icon breaks.
    [Theory]
    [InlineData("google")]
    [InlineData("openai")]
    [InlineData("anthropic")]
    [InlineData("qwen")]
    [InlineData("deepseek")]
    public void Known_provider_has_brand_icon(string provider)
    {
        var icon = ProviderIcons.TryGet(provider);

        Assert.True(icon is not null, $"Expected an icon for provider '{provider}'.");
        Assert.Matches("^#[0-9A-Fa-f]{6}$", icon!.Value.BrandColor);
        Assert.StartsWith("M", icon.Value.SvgPath, StringComparison.Ordinal);
        Assert.True(icon.Value.SvgPath.Length > 50, "Path data looks truncated.");
    }

    [Theory]
    [InlineData("openai/gpt-6-luna", "openai")]
    [InlineData("google/gemini-3.8-flash", "google")]
    [InlineData("anthropic/claude-sonnet-5.5", "anthropic")]
    [InlineData("a/b/c", "a")]              // only the first segment
    [InlineData("no-slash", "")]            // bare id → no provider
    [InlineData("/leading-slash", "")]      // slash at position 0 is not a provider
    public void ProviderOf_extracts_prefix(string modelId, string expected)
    {
        Assert.Equal(expected, ProviderIcons.ProviderOf(modelId));
    }

    [Fact]
    public void Lookup_is_case_insensitive()
    {
        Assert.True(ProviderIcons.TryGet("OpenAI") is not null);
        Assert.True(ProviderIcons.TryGet(ProviderIcons.ProviderOf("Google/gemini")) is not null);
    }

    [Theory]
    [InlineData("")]
    [InlineData("meta")]          // known-unlisted provider → monogram fallback
    [InlineData("mystery-labs")]
    public void Unknown_provider_falls_back_to_null(string provider)
    {
        Assert.Null(ProviderIcons.TryGet(provider));
    }
}
