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
                upswings.Select(x => x.SwingLow(EnumCloseType.Close)).ToList(),
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
                markerSize: 6
            ),
        });

        foreach (var upswing in upswings.Where(x => x.MarketStructureBreak != null))
        {
            var p1 = upswing.PreviousUpswing.SwingLow(EnumCloseType.Close);
            var p2 = upswing.MarketStructureBreak;
            var p = new List<Price>
            {
                p1,
                new Price
                {
                    Close = p1.Close,
                    DateTime = p2.DateTime
                }
            };

            chart = chart.AddLayers(
                ChartGenerator.PriceClosesLineLayer(p, lineWidth: 1, color: Color.fromString("orange"))
                );
        }

        chart = chart.AddLayers(new Layer
        {
            Name = "BOS",
            ChartFactory = () => ChartGenerator.GenerateScatterChart(
                upswings.Where(x => x.BreakOfStructure != null).Select(x => x.BreakOfStructure).ToList(),
                p => (decimal)p.Close,
                title: name,
                color: Color.fromString("cyan"),
                markerSize: 6
            ),
        });

        foreach (var upswing in upswings.Where(x => x.BreakOfStructure != null))
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

            chart = chart.AddLayers(
                ChartGenerator.PriceClosesLineLayer(p, lineWidth: 1, color: Color.fromString("cyan"))
                );
        }

        AssertChart(name, chart);
    }
}