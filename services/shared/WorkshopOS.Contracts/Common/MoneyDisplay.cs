using System.Globalization;

namespace WorkshopOS.Contracts.Common;

/// <summary>
/// Display money with an explicit currency symbol. Never uses CultureInfo currency formatting
/// (InvariantCulture <c>ToString("C")</c> yields the generic <c>¤</c> glyph).
/// AUD/USD/NZD/CAD and unknown codes → <c>$</c>.
/// </summary>
public static class MoneyDisplay
{
    public static string SymbolFromCurrencyCode(string? currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
            return "$";

        return currencyCode.Trim().ToUpperInvariant() switch
        {
            "EUR" => "€",
            "GBP" => "£",
            "JPY" or "CNY" or "RMB" => "¥",
            "INR" => "₹",
            "KRW" => "₩",
            "CHF" => "CHF ",
            // AUD, USD, NZD, CAD, SGD, HKD, MXN, etc. — dollar sign
            "AUD" or "USD" or "NZD" or "CAD" or "SGD" or "HKD" or "MXN" => "$",
            _ => "$"
        };
    }

    public static string Format(decimal amount, string? currencyCode = null) =>
        SymbolFromCurrencyCode(currencyCode) + amount.ToString("0.00", CultureInfo.InvariantCulture);

    public static string Format(double amount, string? currencyCode = null) =>
        SymbolFromCurrencyCode(currencyCode) + amount.ToString("0.00", CultureInfo.InvariantCulture);

    public static string FormatWithSymbol(decimal amount, string? symbol)
    {
        var s = string.IsNullOrWhiteSpace(symbol) || symbol == "¤" ? "$" : symbol;
        return s + amount.ToString("0.00", CultureInfo.InvariantCulture);
    }

    public static string FormatWithSymbol(double amount, string? symbol)
    {
        var s = string.IsNullOrWhiteSpace(symbol) || symbol == "¤" ? "$" : symbol;
        return s + amount.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
