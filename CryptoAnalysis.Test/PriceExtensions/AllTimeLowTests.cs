using Plotly.NET;

namespace Gradient.CryptoAnalysis.Test.PriceExtensions;

[TestClass]
public class AllTimeLowTests : PricesTests
{
    public override string TestDirectory => Path.Combine("PricesExtensionsData", "AllTimeLowTests");

    [TestMethod]
    public void TestAllTimeLows_LowestCloses()
    {
        var name = "TestAllTimeLows_LowestCloses";

        var chart = ChartGenerator.CreatePriceChart(_prices, lineCloses: true, lineWidth: 1);

        var segment = _prices.AllTimeLows(EnumCloseType.Close);
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
    public void TestAllTimeLows_LowestsLows()
    {
        var name = "TestAllTimeLows_LowestsLows";
        var chart = ChartGenerator.CreatePriceChart(_prices, lineCloses: true, lineWidth: 1);

        var segment = _prices.AllTimeLows(EnumCloseType.Low);
        chart = chart.AddLayers(new Layer
        {
            Name = "base",
            ChartFactory = () => ChartGenerator.GenerateLineChart(
                segment,
                p => (decimal)p.Low,
                name
            ),
            Color = Color.fromString("red"),
            LineWidth = 1,
        });

        AssertChart(name, chart);
    }
}