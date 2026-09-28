using System.Globalization;
using FluentAssertions;
using WorkshopOS.Contracts.Common;

namespace WorkshopOS.Api.Tests;

public sealed class MoneyDisplayTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AUD")]
    [InlineData("USD")]
    [InlineData("CAD")]
    [InlineData("NZD")]
    [InlineData("aud")]
    [InlineData("XYZ")]
    public void Format_Dollar_Currencies_Use_Dollar_Sign_Never_Generic(string? code)
    {
        var text = MoneyDisplay.Format(139.20m, code);
        text.Should().Be("$139.20");
        text.Should().NotContain("¤");
        text.Should().StartWith("$");
    }

    [Fact]
    public void Format_Never_Uses_Culture_Currency_Symbol()
    {
        // Culture ToString("C") under InvariantCulture is the bug the print path must avoid.
        var cultureBug = 50m.ToString("C", CultureInfo.InvariantCulture);
        cultureBug.Should().Contain("¤");

        MoneyDisplay.Format(50m, "AUD").Should().Be("$50.00");
        MoneyDisplay.FormatWithSymbol(50m, "¤").Should().Be("$50.00");
        MoneyDisplay.FormatWithSymbol(50m, null).Should().Be("$50.00");
        MoneyDisplay.FormatWithSymbol(50m, "").Should().Be("$50.00");
    }

    [Fact]
    public void SymbolFromCurrencyCode_Maps_Common_Codes()
    {
        MoneyDisplay.SymbolFromCurrencyCode("EUR").Should().Be("€");
        MoneyDisplay.SymbolFromCurrencyCode("GBP").Should().Be("£");
        MoneyDisplay.SymbolFromCurrencyCode("AUD").Should().Be("$");
        MoneyDisplay.SymbolFromCurrencyCode(null).Should().Be("$");
    }
}
