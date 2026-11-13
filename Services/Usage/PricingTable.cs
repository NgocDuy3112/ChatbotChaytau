using System.Globalization;

namespace chatbotchaytau.Services.Usage;

/// <summary>
/// USD per 1M token prices from <c>OpenRouter:Pricing</c> (seeded from the live
/// OpenRouter catalog). Models without an entry return null cost — the admin
/// page reports those as unpriced instead of inventing numbers.
/// </summary>
public sealed class PricingTable
{
    private readonly Dictionary<string, (decimal In, decimal Out)> _prices;

    public PricingTable(IConfiguration configuration)
    {
        _prices = new Dictionary<string, (decimal, decimal)>(StringComparer.Ordinal);

        foreach (var model in configuration.GetSection("OpenRouter:Pricing").GetChildren())
        {
            var name = model.Key;
            if (string.IsNullOrWhiteSpace(name)) continue;

            if (TryDecimal(model["In"], out var priceIn) && TryDecimal(model["Out"], out var priceOut))
                _prices[name] = (priceIn, priceOut);
        }
    }

    internal int Count => _prices.Count;

    /// <summary>Cost of one call, or null when pricing/tokens are unavailable.</summary>
    public decimal? CostUsd(string model, long? promptTokens, long? completionTokens)
    {
        if (promptTokens is null && completionTokens is null) return null;
        if (!_prices.TryGetValue(model, out var price)) return null;

        var cost = ((promptTokens ?? 0) * price.In
                  + (completionTokens ?? 0) * price.Out) / 1_000_000m;
        return cost;
    }

    public bool IsPriced(string model) => _prices.ContainsKey(model);

    private static bool TryDecimal(string? raw, out decimal value) =>
        decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
