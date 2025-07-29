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

        var chart = ChartGenerator.CreatePriceChart(_prices, lineCloses: true, lineWidth: 1);

        var upswings = _prices.ToUpswings(EnumCloseType.Close);

        chart = chart.AddLayers(new Layer
        {
            Name = "Higher highs",
            ChartFactory = () => ChartGenerator.GenerateScatterChart(
                upswings.Select(x => x.Prices.First()).ToList(),
                p => (decimal)p.Close,
                title: name,
                color: Color.fromString("green"),
                markerSize: 12
            ),
        });

        chart = chart.AddLayers(new Layer
        {
            Name = "base",
            ChartFactory = () => ChartGenerator.GenerateScatterChart(
                upswings.Select(x => x.Prices.AllTimeLows(EnumCloseType.Close).MinBy(x => x.Close)).ToList(),
                p => (decimal)p.Close,
                title: name,
                color: Color.fromString("red"),
                markerSize: 12
            )
        });

        chart = chart.AddLayers(new Layer
        {
            Name = "Market Structure Breaks",
            ChartFactory = () => ChartGenerator.GenerateScatterChart(
                upswings.Where(x => x.MarketStructureBreak != null).Select(x => x.MarketStructureBreak).ToList(),
                p => (decimal)p.Close,
                title: name,
                color: Color.fromString("orange"),
                markerSize: 12
            ),
        });

        foreach (var upswing in upswings.Where(x => x.NextPrice != null))
        {
            var p1 = upswing.Prices.First();
            var p2 = upswing.NextPrice;
            var p = new List<Price>
            {
                p1,
                new Price
                {
                    Close = p1.Close,
                    DateTime = p2.DateTime
                }
            };

            var layer = ChartGenerator.PriceClosesLineLayer(p, lineWidth: 1, color: Color.fromString("cyan"));

            chart = chart.AddLayers(layer);
        }

        AssertChart(name, chart);
    }
}