namespace Gradient.CryptoAnalysis.Test.PriceExtensions;

[TestClass]
public class ToDownwardSawtoothTests : PricesTests
{
    public override string TestDirectory => Path.Combine("PricesExtensionsData", "ToDownwardSawtoothTests");

    [TestMethod]
    public void ToDownwardSawtoothTests_1()
    {
        var name = "ToDownwardSawtoothTests_1";

        var sawtooth = _prices.ToDownwardSawtooth(EnumCloseType.Close);

        var chart = ChartGenerator.CreatePriceChart(_prices, lineCloses: true, lineWidth: 1);
        chart = chart
            .WithSawtooth(sawtooth, EnumCloseType.Close, "red", lineWidth: 3)
            ;

        AssertChart(name, chart);
    }
}