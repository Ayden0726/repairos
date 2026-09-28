namespace WorkshopOS.Contracts.Common;

/// <summary>
/// Display money with a currency symbol. Uses business profile currency code when available;
/// defaults to <c>$</c> (AUD/USD/NZD/CAD and unknown codes).
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
            _ => "$"
        };
    }

    public static string Format(decimal amount, string? currencyCode = null) =>
        $"{SymbolFromCurrencyCode(currencyCode)}{amount:0.00}";

    public static string Format(double amount, string? currencyCode = null) =>
        $"{SymbolFromCurrencyCode(currencyCode)}{amount:0.00}";

    public static string FormatWithSymbol(decimal amount, string? symbol) =>
        $"{(string.IsNullOrEmpty(symbol) ? "$" : symbol)}{amount:0.00}";

    public static string FormatWithSymbol(double amount, string? symbol) =>
        $"{(string.IsNullOrEmpty(symbol) ? "$" : symbol)}{amount:0.00}";
}
