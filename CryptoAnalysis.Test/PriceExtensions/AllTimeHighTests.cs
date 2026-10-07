using Plotly.NET;

namespace Gradient.CryptoAnalysis.Test.PriceExtensions;

[TestClass]
public class AllTimeHighTests : PricesTests
{
    public override string TestDirectory => Path.Combine("PricesExtensionsData", "AllTimeHighTests");

    [TestMethod]
    public void TestAllTimeHighs_HighestCloses()
    {
        var name = "TestAllTimeHighs_HighestCloses";

        var chart = ChartGenerator.CreatePriceChart(_prices, lineCloses: true, lineWidth: 1);

        var segment = _prices.AllTimeHighs(EnumCloseType.Close);
        chart = chart.AddLayers(new Layer
        {
            Name = "base",
            ChartFactory = () => ChartGenerator.GenerateLineChart(
                segment,
                p => (decimal)p.Close,
                name
            ),
            Color = Color.fromString("red"),
            LineWidth = 1,
        });

        AssertChart(name, chart);
    }

    [TestMethod]
    public void TestAllTimeHighs_HighestsHighs()
    {
        var name = "TestAllTimeHighs_HighestsHighs";
        var chart = ChartGenerator.CreatePriceChart(_prices, lineCloses: true, lineWidth: 1);

        var segment = _prices.AllTimeHighs(EnumCloseType.High);
        chart = chart.AddLayers(new Layer
        {
            Name = "base",
            ChartFactory = () => ChartGenerator.GenerateLineChart(
                segment,
                p => (decimal)p.High,
                name
            ),
            Color = Color.fromString("red"),
            LineWidth = 1,
        });

        AssertChart(name, chart);
    }
}