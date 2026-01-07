namespace Gradient.CryptoAnalysis.Test.PriceExtensions;

[TestClass]
public class ToUpswingsTests : PricesTests
{
    public override string TestDirectory => Path.Combine("PricesExtensionsData", "ToUpswingsTests");

    [DataTestMethod]
    [DataRow("ToUpswingTests_Candlestick", true, false, -1)]
    [DataRow("ToUpswingTests_LineCloses", false, true, -1)]
    [DataRow("ToUpswingTests_Candlestick", true, false, 0)]
    [DataRow("ToUpswingTests_LineCloses", false, true, 0)]
    public void ToUpswingTests(string name, bool candlestick, bool lineCloses, int interims)
    {
        var closeType = EnumCloseType.Close;
        var upswings = _prices.ToUpswings(closeType, true);
        var downswings = _prices.ToDownswings(closeType, true);
        var chart = ChartGenerator.CreatePriceChart(_prices, candlestick: candlestick, lineCloses: lineCloses, lineWidth: 1);

        chart = chart
            .WithHigherHighs(upswings, closeType)
            .WithHigherLows(upswings, closeType)
            .WithUpswings(upswings, closeType, lineWidth: 2, color: "green")
            .WithBreaksOfStructureMarkers(upswings, closeType, lineWidth: 3, color: "cyan", markerSize: 6)
            .WithBreaksOfStructureMarkers(downswings, closeType, lineWidth: 3, color: "cyan", markerSize: 6)
            .WithMarketStructureBreaksMarkers(upswings, closeType, lineWidth: 3, color: "orange", markerSize: 6)
            .WithMarketStructureBreaksMarkers(downswings, closeType, lineWidth: 3, color: "orange", markerSize: 6)
            ;

        if (interims > -1)
        {
            name = $"{name}_Interims_{interims}";
            foreach (var upswing in upswings)
            {
                chart = chart.WithInterimSwings(upswing, EnumCloseType.Close, interims);
            }

            foreach (var downswing in downswings)
            {
                //chart = chart.WithInterimSwings(downswing, EnumCloseType.Close, interims);
            }
        }

        AssertChart(name, chart);
    }
}