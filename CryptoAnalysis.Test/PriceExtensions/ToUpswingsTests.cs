namespace Gradient.CryptoAnalysis.Test.PriceExtensions;

[TestClass]
public class ToUpswingsTests : PricesTests
{
    public override string TestDirectory => Path.Combine("PricesExtensionsData", "ToUpswingsTests");

    [TestMethod]
    public void ToUpswingTests_Candlestick()
    {
        var name = "ToUpswingTests_Candlestick";

        var upswings = _prices.ToUpswings(EnumCloseType.Close);
        var chart = ChartGenerator.CreatePriceChart(_prices, candlestick: true, lineWidth: 1);
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

        var chart = ChartGenerator.CreatePriceChart(_prices, lineCloses: true, lineWidth: 1);
        var upswings = _prices.ToUpswings(EnumCloseType.Close);

        foreach (var upswing in upswings)
        {
            var interimUpswings = upswing.InterimUpswings(EnumCloseType.Close);
            chart = chart
                .WithHigherHighs(interimUpswings, EnumCloseType.Close, color: "lightgreen")
                .WithHigherLows(interimUpswings, EnumCloseType.Close, color: "pink")
                .WithMarketStructureBreaks(interimUpswings, EnumCloseType.Close, color: "yellow")
                .WithBreaksOfStructure(interimUpswings, EnumCloseType.Close, color: "teal");
        }

        chart = chart
            .WithHigherHighs(upswings, EnumCloseType.Close)
            .WithHigherLows(upswings, EnumCloseType.Close)
            .WithMarketStructureBreaks(upswings, EnumCloseType.Close)
            .WithBreaksOfStructure(upswings, EnumCloseType.Close);

        AssertChart(name, chart);
    }

    [TestMethod]
    public void ToUpswingTests_LineCloses()
    {
        var name = "ToUpswingTests_LineCloses";

        var upswings = _prices.ToUpswings(EnumCloseType.Close);
        var chart = ChartGenerator.CreatePriceChart(_prices, lineCloses: true, lineWidth: 1);
        chart = chart
            .WithHigherHighs(upswings, EnumCloseType.Close)
            .WithHigherLows(upswings, EnumCloseType.Close)
            .WithUpswings(upswings, EnumCloseType.Close, lineWidth: 2, color: "cyan")
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