namespace Gradient.CryptoAnalysis.Test.PriceExtensions;

[TestClass]
public class ToUpswingsTests : PricesTests
{
    public override string TestDirectory => Path.Combine("PricesExtensionsData", "ToUpswingsTests");

    [DataTestMethod]
    [DataRow("ToUpswingTests_Candlestick", true, false)]
    [DataRow("ToUpswingTests_LineCloses", false, true)]
    public void ToUpswingTests_1(string name, bool candlestick, bool lineCloses)
    {
        var upswings = _prices.ToUpswings(EnumCloseType.Close);
        var chart = ChartGenerator.CreatePriceChart(_prices, candlestick: candlestick, lineCloses: lineCloses, lineWidth: 1);
        chart = chart
            .WithHigherHighs(upswings, EnumCloseType.Close)
            .WithHigherLows(upswings, EnumCloseType.Close)
            .WithUpswings(upswings, EnumCloseType.Close, lineWidth: 2, color: "cyan")
            .WithBreaksOfStructure(upswings, EnumCloseType.Close, lineWidth: 3, color: "blue", markerSize: 6)
            .WithMarketStructureBreaks(upswings, EnumCloseType.Close, lineWidth: 3, color: "orange", markerSize: 6)
            ;

        AssertChart(name, chart);
    }

    [TestMethod]
    public void ToUpswingTests_Interims()
    {
        var name = "ToUpswingTests_Interims";

        var upswings = _prices.ToUpswings(EnumCloseType.Close);
        var chart = ChartGenerator.CreatePriceChart(_prices, candlestick: true, lineWidth: 1);
        chart = chart
            .WithHigherHighs(upswings, EnumCloseType.Close)
            .WithHigherLows(upswings, EnumCloseType.Close)
            .WithUpswings(upswings, EnumCloseType.Close, lineWidth: 2, color: "cyan");

        foreach (var upswing in upswings)
        {
            var interimDownswings = upswing.InterimDownswings(EnumCloseType.Close);
            var interminUpswings = upswing.InterimUpswings(EnumCloseType.Close);

            chart = chart
                .WithLowerHighs(interimDownswings, EnumCloseType.Close, markerSize: 6)
                .WithLowerLows(interimDownswings, EnumCloseType.Close, markerSize: 6)
                .WithHigherHighs(interminUpswings, EnumCloseType.Close, markerSize: 6)
                .WithHigherLows(interminUpswings, EnumCloseType.Close, markerSize: 6)
                .WithDownswings(interimDownswings, EnumCloseType.Close, lineWidth: 2, color: "pink")
                .WithUpswings(interminUpswings, EnumCloseType.Close, lineWidth: 2, color: "lightgreen")
                ;
        }

        chart = chart
            .WithBreaksOfStructure(upswings, EnumCloseType.Close, lineWidth: 3, color: "blue", markerSize: 6)
            .WithMarketStructureBreaks(upswings, EnumCloseType.Close, lineWidth: 3, color: "orange", markerSize: 6)
            ;

        AssertChart(name, chart);
    }
}

public enum BreakOfStructureFormat
{
    Line,
    Box
}