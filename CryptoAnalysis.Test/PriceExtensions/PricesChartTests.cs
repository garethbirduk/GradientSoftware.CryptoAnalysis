namespace Gradient.CryptoAnalysis.Test.PriceExtensions;

[TestClass]
public class PricesChartTests : PricesTests
{
    public override string TestDirectory => Path.Combine("PricesExtensionsData", "PricesChartTests");

    [TestMethod]
    public void PricesTests_CreatePriceChart()
    {
        var name = "TestAllTimeHighs_HighestCloses";

        var chart = ChartGenerator.CreatePriceChart(_prices, lineCloses: true, lineWidth: 1);

        var segment = _prices.AllTimeHighs(EnumCloseType.Close);
        chart = ChartGenerator.CreatePriceChart(_prices, lineCloses: true);

        AssertChart(name, chart);
    }
}