namespace Gradient.CryptoAnalysis.Test.PriceExtensions;

[TestClass]
public class ToRangesTests : PricesTests
{
    public override string TestDirectory => Path.Combine("PricesExtensionsData", "ToRangesTests");

    [DataTestMethod]
    //[DataRow("ToRangesTest_Candlestick", true, false, -1)]
    //[DataRow("ToRangesTest_Candlestick", true, false, 0)]
    //[DataRow("ToRangesTest_LineCloses", false, true, -1)]
    [DataRow("ToRangesTest_LineCloses", false, true, 1)]
    public void ToRangesTest1(string name, bool candlestick, bool lineCloses, int interims)
    {
        var closeType = EnumCloseType.Close;
        var downtrends = _prices.ToDowntrends();

        //    var upswings = _prices.ToUpswings(closeType, true);
        //    var downswings = _prices.ToDownswings(closeType, true);
        //    var chart = ChartGenerator.CreatePriceChart(_prices, candlestick: candlestick, lineCloses: lineCloses, lineWidth: 1);

        //    chart = chart
        //        .WithLowerHighs(downswings, closeType)
        //        .WithLowerLows(downswings, closeType)
        //        .WithDownswings(downswings, closeType, lineWidth: 1, color: "red")
        //        //.WithBreaksOfStructureMarkers(upswings, closeType, lineWidth: 3, color: "cyan", markerSize: 6)
        //        .WithBreaksOfStructureMarkers(downswings, closeType, lineWidth: 3, color: "cyan", markerSize: 6)
        //        //.WithBreaksOfStructureReferences(downswings, closeType, lineWidth: 3, color: "cyan", markerSize: 6)
        //        //.WithMarketStructureBreaksMarkers(upswings, closeType, lineWidth: 3, color: "orange", markerSize: 6)
        //        .WithMarketStructureBreaksMarkers(downswings, closeType, lineWidth: 3, color: "orange", markerSize: 6)
        //        .WithMarketStructureBreakReferences(downswings, closeType, lineWidth: 3, color: "orange", markerSize: 6)
        //        ;

        //    if (interims > -1)
        //    {
        //        name = $"{name}_Interims_{interims}";
        //        foreach (var upswing in upswings)
        //        {
        //            chart = chart.WithInterimSwings(upswing, EnumCloseType.Close, interims);
        //        }

        //        foreach (var downswing in downswings)
        //        {
        //            var interimDownswings = downswing.InterimDownswings(closeType, true, true);
        //            var interimUpswings = downswing.InterimUpswings(closeType, true, true);
        //            chart = chart.WithInterimSwings(downswing, EnumCloseType.Close, interims);
        //        }
        //    }

        //    AssertChart(name, chart);
    }
}