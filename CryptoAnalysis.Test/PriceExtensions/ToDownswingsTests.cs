using Plotly.NET;

namespace Gradient.CryptoAnalysis.Test.PriceExtensions;

[TestClass]
public class ToDownswingsTests : PricesTests
{
    public override string TestDirectory => Path.Combine("PricesExtensionsData", "ToDownswingsTests");

    [TestMethod]
    public void ToDownswingTests_1()
    {
        var name = "ToDownswingTests_1";

        var chart = ChartGenerator.CreatePriceChart(_prices, lineCloses: true, lineWidth: 1);

        var downswings = _prices.ToDownswings(EnumCloseType.Close);

        chart = chart.AddLayers(new Layer
        {
            Name = "Lower lows",
            ChartFactory = () => ChartGenerator.GenerateScatterChart(
                downswings.Select(x => x.Prices.First()).ToList(),
                p => (decimal)p.Close,
                title: name,
                color: Color.fromString("red"),
                markerSize: 12
            ),
        });

        chart = chart.AddLayers(new Layer
        {
            Name = "base",
            ChartFactory = () => ChartGenerator.GenerateScatterChart(
                downswings.Select(x => x.SwingHigh(EnumCloseType.Close)).ToList(),
                p => (decimal)p.Close,
                title: name,
                color: Color.fromString("green"),
                markerSize: 12
            )
        });

        chart = chart.AddLayers(new Layer
        {
            Name = "Market Structure Breaks",
            ChartFactory = () => ChartGenerator.GenerateScatterChart(
                downswings.Where(x => x.MarketStructureBreak != null).Select(x => x.MarketStructureBreak).ToList(),
                p => (decimal)p.Close,
                title: name,
                color: Color.fromString("orange"),
                markerSize: 6
            ),
        });

        foreach (var downswing in downswings.Where(x => x.MarketStructureBreak != null))
        {
            var p1 = downswing.PreviousDownswing.SwingHigh(EnumCloseType.Close);
            var p2 = downswing.MarketStructureBreak;
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
                downswings.Where(x => x.BreakOfStructure != null).Select(x => x.BreakOfStructure).ToList(),
                p => (decimal)p.Close,
                title: name,
                color: Color.fromString("cyan"),
                markerSize: 6
            ),
        });

        foreach (var Downswing in downswings.Where(x => x.BreakOfStructure != null))
        {
            var p1 = Downswing.Prices.First();
            var p2 = Downswing.NextPrice;
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