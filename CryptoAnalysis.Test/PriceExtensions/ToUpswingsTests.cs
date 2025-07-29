using Plotly.NET;

namespace Gradient.CryptoAnalysis.Test.PriceExtensions;

[TestClass]
public class ToUpswingsTests : PricesTests
{
    public override string TestDirectory => Path.Combine("PricesExtensionsData", "ToUpswingsTests");

    [TestMethod]
    public void ToUpswingTests_1()
    {
        var name = "ToUpswingTests_1";

        var chart = ChartGenerator.CreatePriceChart(_prices, candlestick: true, lineWidth: 3);

        var upswings = _prices.ToUpswings(EnumCloseType.Close);

        chart = chart.AddLayers(new Layer
        {
            Name = "base",
            ChartFactory = () => ChartGenerator.GenerateScatterChart(
                upswings.Select(x => x.Prices.First()).ToList(),
                p => (decimal)p.Close,
                name
            ),
            Color = Color.fromString("green"),
            LineWidth = 1,
        });

        chart = chart.AddLayers(new Layer
        {
            Name = "base",
            ChartFactory = () => ChartGenerator.GenerateScatterChart(
                upswings.Select(x => x.Prices.AllTimeLows(EnumCloseType.Close).MinBy(x => x.Close)).ToList(),
                p => (decimal)p.Close,
                name
            ),
            Color = Color.fromString("orange"),
            LineWidth = 1,
        });

        chart = chart.AddLayers(new Layer
        {
            Name = "base",
            ChartFactory = () => ChartGenerator.GenerateScatterChart(
                upswings.Where(x => x.MarketStructureBreak != null).Select(x => x.MarketStructureBreak).ToList(),
                p => (decimal)p.Close,
                name
            ),
            Color = Color.fromString("red"),
            LineWidth = 1,
        });

        foreach (var upswing in upswings.Where(x => x.NextPrice != null))
        {
            var p1 = upswing.Prices.First();
            var p2 = upswing.NextPrice;
            var p = new List<Price>()
            {
                p1,
                new Price()
                {
                    Close = p1.Close,
                    DateTime = p2.DateTime
                }
            };

            chart = chart.AddLayers(new Layer
            {
                Name = "base",
                ChartFactory = () => ChartGenerator.GenerateLineChart(
                    p,
                    p => (decimal)p.Close,
                    name,
                    color: Color.fromString("cyan"))
            });
        }

        AssertChart(name, chart);
    }
}