namespace Gradient.CryptoAnalysis.Test.PriceExtensions;

[TestClass]
public class ToHigherHighsAndLowsTests : PricesTests
{
    public override string TestDirectory => Path.Combine("PricesExtensionsData", "ToHigherHighsAndLowsTests");

    [DataTestMethod]
    [DataRow("ToHigherHighsAndLowsTests", false, true)]
    public void ToHigherHighsAndLowsTests1(string name, bool candlestick, bool lineCloses)
    {
        var closeType = EnumCloseType.Close;
        var chart = ChartGenerator.CreatePriceChart(_prices, candlestick: candlestick, lineCloses: lineCloses, lineWidth: 1);
        chart = chart
            .WithHigherHighs(_prices.ToHighHighs(closeType), EnumCloseType.Close)
            .WithHigherLows(_prices.ToHigherLows(closeType), EnumCloseType.Close)
            ;

        AssertChart(name, chart);
    }
}