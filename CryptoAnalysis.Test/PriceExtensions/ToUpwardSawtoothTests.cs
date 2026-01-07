namespace Gradient.CryptoAnalysis.Test.PriceExtensions;

[TestClass]
public class ToUpwardSawtoothTests : PricesTests
{
    public override string TestDirectory => Path.Combine("PricesExtensionsData", "ToUpwardSawtoothTests");

    [TestMethod]
    public void ToUpwardSawtoothTests_1()
    {
        var name = "ToUpwardSawtoothTests_1";

        var sawtooth = _prices.ToUpwardSawtooth(EnumCloseType.Close);

        var chart = ChartGenerator.CreatePriceChart(_prices, lineCloses: true, lineWidth: 1);
        chart = chart
            .WithSawtooth(sawtooth, EnumCloseType.Close, "green", lineWidth: 3)
            ;

        AssertChart(name, chart);
    }
}