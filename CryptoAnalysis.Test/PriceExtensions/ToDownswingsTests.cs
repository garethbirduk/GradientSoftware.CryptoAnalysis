namespace Gradient.CryptoAnalysis.Test.PriceExtensions;

[TestClass]
public class ToDownswingsTests : PricesTests
{
    public override string TestDirectory => Path.Combine("PricesExtensionsData", "ToDownswingsTests");

    [DataTestMethod]
    [DataRow("ToDownswingTests_Candlestick", true, false)]
    [DataRow("ToDownswingTests_LineCloses", false, true)]
    public void ToDownswingTests_1(string name, bool candlestick, bool lineCloses)
    {
        var downswings = _prices.ToDownswings(EnumCloseType.Close, true);
        var chart = ChartGenerator.CreatePriceChart(_prices, candlestick: candlestick, lineCloses: lineCloses, lineWidth: 1);
        chart = chart
            .WithLowerHighs(downswings, EnumCloseType.Close)
            .WithLowerLows(downswings, EnumCloseType.Close)
            .WithDownswings(downswings, EnumCloseType.Close, lineWidth: 2, color: "cyan")
            .WithBreaksOfStructure(downswings, EnumCloseType.Close, lineWidth: 3, color: "blue", markerSize: 6)
            .WithMarketStructureBreaks(downswings, EnumCloseType.Close, lineWidth: 3, color: "orange", markerSize: 6)
            ;

        AssertChart(name, chart);
    }
}