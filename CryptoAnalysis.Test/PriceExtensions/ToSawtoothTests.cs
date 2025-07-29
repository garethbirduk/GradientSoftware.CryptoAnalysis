using Plotly.NET;

namespace Gradient.CryptoAnalysis.Test.PriceExtensions;

[TestClass]
public class ToSawtoothTests : PricesTests
{
    public override string TestDirectory => Path.Combine("PricesExtensionsData", "ToSawtoothTests");

    [TestMethod]
    public void ToSawtoothTests_1()
    {
        var name = "ToSawtoothTests_1";

        var chart = ChartGenerator.CreatePriceChart(_prices, lineCloses: true, lineWidth: 3);
        var sawtooth = _prices.ToSawtooth(EnumCloseType.Close);

        chart = chart.AddLayers(new Layer
        {
            Name = "base",
            ChartFactory = () => ChartGenerator.GenerateLineChart(
                sawtooth,
                p => (decimal)p.Close,
                name
            ),
            Color = Color.fromString("green"),
            LineWidth = 1,
        });

        AssertChart(name, chart);
    }
}