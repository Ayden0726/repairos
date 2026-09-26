using FluentAssertions;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Infrastructure.Services;

namespace WorkshopOS.Api.Tests;

public sealed class PricingCalculatorTests
{
    [Fact]
    public void Example_Landed156_Markup20_Labour50_End9()
    {
        var settings = PricingCalculator.DefaultSettings() with
        {
            Parts = new PartsPricingDto("FlatPercent", 20m, 0m, 0m),
            Labour = PricingCalculator.DefaultSettings().Labour with { DefaultLabourFee = 50m, DifficultyPricingEnabled = false },
            Rounding = new RoundingPricingDto("End9", null),
            Tax = new TaxPricingDto(true, 0.10m, true)
        };

        var preview = PricingCalculator.Calculate(
            new PricingPreviewRequest(
            [
                new QuoteLineCalcInput(
                    "PART", "OLED", null, "OLED", null, null, null, null, null,
                    1m, 156m, 0m, 0m, null, null, null, 50m, 0m, 0m, null)
            ], null, null, null, null),
            settings);

        preview.Lines.Should().HaveCount(1);
        preview.Lines[0].LandedCost.Should().Be(156m);
        preview.Lines[0].PartSell.Should().Be(187.20m);
        preview.Lines[0].LabourAmount.Should().Be(50m);
        preview.PreRoundTotal.Should().Be(237.20m);
        preview.Total.Should().Be(239m);
        preview.ProfitTotal.Should().Be(83m);
        preview.MarginPercent.Should().Be(34.73m);
    }

    [Fact]
    public void RoundEndIn9_From_237_20()
    {
        PricingCalculator.RoundEndIn9(237.20m).Should().Be(239m);
    }
}
