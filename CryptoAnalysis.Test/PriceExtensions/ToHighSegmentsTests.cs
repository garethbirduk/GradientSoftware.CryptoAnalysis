using Plotly.NET;

namespace Gradient.CryptoAnalysis.Test.PriceExtensions;

[TestClass]
public class ToHighSegmentsTests : PricesTests
{
    public override string TestDirectory => Path.Combine("PricesExtensionsData", "ToHighSegmentsTests");

    [TestMethod]
    public void ToHighSegmentsTests_Segments()
    {
        var name = "ToHighSegmentsTests_Segments";

        var chart = ChartGenerator.CreatePriceChart(_prices, lineCloses: true, lineWidth: 3);

        var segments = _prices.ToHighSegments(EnumCloseType.Close, true);
        foreach (var segment in segments)
        {
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
        }

        AssertChart(name, chart);
    }
}