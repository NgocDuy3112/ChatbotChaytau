using aida.Services.Usage;
using Microsoft.Extensions.Configuration;

namespace aida.Tests.Services;

public class PricingTableTests
{
    private static PricingTable Table(params (string Key, string Value)[] settings) =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(
                s => s.Key, s => (string?)s.Value))
            .Build());

    private static readonly (string Key, string Value)[] Sample =
    [
        ("OpenRouter:Pricing:openai/gpt-4o:In", "2.5"),
        ("OpenRouter:Pricing:openai/gpt-4o:Out", "10"),
        ("OpenRouter:Pricing:openai/gpt-4o-mini:In", "0.15"),
        ("OpenRouter:Pricing:openai/gpt-4o-mini:Out", "0.6"),
        ("OpenRouter:Pricing:broken/model:In", "not-a-number"),
        ("OpenRouter:Pricing:broken/model:Out", "1"),
    ];

    [Fact]
    public void Known_model_costs_tokens_at_configured_rates()
    {
        var table = Table(Sample);

        // 1000 prompt × $2.5/M + 500 completion × $10/M = $0.0075
        Assert.Equal(0.0075m, table.CostUsd("openai/gpt-4o", 1000, 500));
    }

    [Fact]
    public void Unknown_model_returns_null_never_zero()
    {
        var table = Table(Sample);

        Assert.Null(table.CostUsd("mystery/model", 1000, 500));
        Assert.False(table.IsPriced("mystery/model"));
    }

    [Fact]
    public void Missing_usage_returns_null_even_for_priced_models()
    {
        var table = Table(Sample);

        Assert.Null(table.CostUsd("openai/gpt-4o", null, null));
    }

    [Fact]
    public void One_sided_usage_still_costs()
    {
        var table = Table(Sample);

        // prompt-only: 1000 × $0.15/M = $0.00015
        Assert.Equal(0.00015m, table.CostUsd("openai/gpt-4o-mini", 1000, null));
    }

    [Fact]
    public void Malformed_price_entry_is_skipped_not_fatal()
    {
        var table = Table(Sample);

        Assert.False(table.IsPriced("broken/model"));
        Assert.Null(table.CostUsd("broken/model", 1000, 500));
    }

    [Fact]
    public void Priced_models_count_excludes_broken_entries()
    {
        var table = Table(Sample);

        Assert.Equal(2, table.Count);
    }
}
