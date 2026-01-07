namespace Gradient.CryptoAnalysis.Test.PriceExtensions;

[TestClass]
public class ToRangesTests : PricesTests
{
    public override string TestDirectory => Path.Combine("PricesExtensionsData", "ToRangesTests");

    [DataTestMethod]
    [DataRow("ToRangesTests_Downswings", false, true)]
    public void ToRangesTests_Downswings(string name, bool candlestick, bool lineCloses)
    {
        var upswings = _prices.ToUpswings(EnumCloseType.Close, true);
        var downswings = _prices.ToDownswings(EnumCloseType.Close, true);
        var chart = ChartGenerator.CreatePriceChart(_prices, candlestick: candlestick, lineCloses: lineCloses, lineWidth: 1);
        chart = chart
            .WithLowerHighs(downswings, EnumCloseType.Close)
            .WithLowerLows(downswings, EnumCloseType.Close)
            .WithDownswings(downswings, EnumCloseType.Close, lineWidth: 2, color: "cyan")
            .WithBreaksOfStructure(downswings, EnumCloseType.Close, lineWidth: 3, color: "yellow", markerSize: 6)
            .WithMarketStructureBreaks(downswings, EnumCloseType.Close, lineWidth: 3, color: "orange", markerSize: 6)
            .WithRangeIndicators(downswings, EnumCloseType.Close, lineWidth: 3, color: "blue", markerSize: 6)
            ;

        foreach (var upswing in upswings)
        {
            chart = chart.WithInterimUpswings(upswing, EnumCloseType.Close, 3);
        }

        foreach (var downswing in downswings)
        {
            chart = chart.WithInterimDownswings(downswing, 3);
        }

        //AssertChart(name, chart);
    }

    [DataTestMethod]
    [DataRow("ToRangesTests_Upswings", false, true)]
    public void ToRangesTests_Upswings(string name, bool candlestick, bool lineCloses)
    {
        var upswings = _prices.ToUpswings(EnumCloseType.Close, true);
        var downswings = _prices.ToDownswings(EnumCloseType.Close, true);
        var chart = ChartGenerator.CreatePriceChart(_prices, candlestick: candlestick, lineCloses: lineCloses, lineWidth: 1);
        chart = chart
            .WithHigherHighs(upswings, EnumCloseType.Close)
            .WithHigherLows(upswings, EnumCloseType.Close)
            .WithUpswings(upswings, EnumCloseType.Close, lineWidth: 2, color: "cyan")
            .WithBreaksOfStructure(upswings, EnumCloseType.Close, lineWidth: 3, color: "yellow", markerSize: 6)
            .WithMarketStructureBreaks(upswings, EnumCloseType.Close, lineWidth: 3, color: "orange", markerSize: 6)
            ;

        foreach (var upswing in upswings)
        {
            chart = chart.WithInterimUpswings(upswing, EnumCloseType.Close, 3);
        }

        foreach (var downswing in downswings)
        {
            chart = chart.WithInterimDownswings(downswing, 3);
        }

        AssertChart(name, chart);
    }
}