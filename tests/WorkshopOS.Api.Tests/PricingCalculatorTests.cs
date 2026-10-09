using FluentAssertions;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Infrastructure.Services;

namespace WorkshopOS.Api.Tests;

public sealed class PricingCalculatorTests
{
    [Fact]
    public void Example_TwoParts_SumLanded156_Markup20_Labour50_End9()
    {
        // $100 + $56 = $156 landed → 20% markup once → $187.20 parts sell + $50 labour = $237.20 → End9 $239
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
                    "PART", "Panel A", null, "Panel A", null, null, null, null, null,
                    1m, 100m, 0m, 0m, null, null, null, null, 0m, 0m, null),
                new QuoteLineCalcInput(
                    "PART", "Panel B", null, "Panel B", null, null, null, null, null,
                    1m, 56m, 0m, 0m, null, null, null, null, 0m, 0m, null)
            ], null, null, null, null),
            settings);

        preview.Lines.Should().HaveCount(2);
        preview.PartsCostTotal.Should().Be(156m);
        preview.CostTotal.Should().Be(156m);
        preview.MarkupPercent.Should().Be(20m);
        preview.MarkupAmount.Should().Be(31.20m);
        preview.PartsSellTotal.Should().Be(187.20m);
        preview.PartsSubtotal.Should().Be(187.20m);
        preview.LabourFee.Should().Be(50m);
        preview.LabourSubtotal.Should().Be(50m);
        preview.Lines.Should().OnlyContain(l => l.LabourAmount == 0m);
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

    [Fact]
    public void Uses_Custom_Settings_Defaults_When_No_Overrides()
    {
        var settings = PricingCalculator.DefaultSettings() with
        {
            Parts = new PartsPricingDto("FlatPercent", 40m, 0m, 0m),
            Labour = PricingCalculator.DefaultSettings().Labour with { DefaultLabourFee = 75m, DifficultyPricingEnabled = false },
            Rounding = new RoundingPricingDto("None", null),
            Tax = new TaxPricingDto(true, 0.10m, true)
        };

        var preview = PricingCalculator.Calculate(
            new PricingPreviewRequest(
            [
                new QuoteLineCalcInput(
                    "PART", "Battery", null, "Battery", null, null, null, null, null,
                    1m, 100m, 0m, 0m, null, null, null, null, 0m, 0m, null)
            ], null, null, null, null),
            settings);

        preview.MarkupPercent.Should().Be(40m);
        preview.PartsSellTotal.Should().Be(140m);
        preview.LabourFee.Should().Be(75m);
        preview.Lines[0].LabourAmount.Should().Be(0m);
        preview.PreRoundTotal.Should().Be(215m);
        preview.Total.Should().Be(215m);
    }

    [Fact]
    public void Job_Labour_And_Markup_Overrides_Apply_Once()
    {
        var settings = PricingCalculator.DefaultSettings() with
        {
            Parts = new PartsPricingDto("FlatPercent", 20m, 0m, 0m),
            Labour = PricingCalculator.DefaultSettings().Labour with { DefaultLabourFee = 50m, DifficultyPricingEnabled = false },
            Rounding = new RoundingPricingDto("None", null),
            Tax = new TaxPricingDto(false, 0.10m, true)
        };

        var preview = PricingCalculator.Calculate(
            new PricingPreviewRequest(
            [
                new QuoteLineCalcInput("PART", "A", null, null, null, null, null, null, null,
                    1m, 50m, 0m, 0m, null, null, null, null, 0m, 0m, null),
                new QuoteLineCalcInput("PART", "B", null, null, null, null, null, null, null,
                    1m, 50m, 0m, 0m, null, null, null, null, 0m, 0m, null)
            ], null, null, null, null, LabourFee: 40m, MarkupPercent: 10m),
            settings);

        preview.PartsCostTotal.Should().Be(100m);
        preview.MarkupPercent.Should().Be(10m);
        preview.PartsSellTotal.Should().Be(110m);
        preview.LabourFee.Should().Be(40m);
        preview.PreRoundTotal.Should().Be(150m);
        preview.Total.Should().Be(150m);
    }
}
