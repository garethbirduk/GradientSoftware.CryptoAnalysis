using Plotly.NET;

namespace Gradient.CryptoAnalysis.Test.PriceExtensions;

// Mirror of HighClosesIsGreenBugTests, for the low side. Real Bitstamp BTC/USD hourly candles,
// 2024-08-03T07:00Z to 22:00Z. 16:00 closes at a genuine new low (60,865, below 15:00's 60,884)
// while closing GREEN -- the pre-fix LowClosesIsRed skipped it for exactly the mirror reason
// HighClosesIsGreen skipped a red new high. This verifies the AllTimeLows fix covers this side too.
[TestClass]
public class LowClosesIsRedBugTests : PricesTests
{
    public override string TestDirectory => Path.Combine("PricesExtensionsData", "LowClosesIsRedBugTests");

    [TestMethod]
    public void LowClosesIsRed_NoLongerInventsAPhantomSwingWhenTheColourGateSkippedABackToBackNewLow()
    {
        var closeType = EnumCloseType.Close;
        var upswings = _prices.ToUpswings(closeType, true);
        var downswings = _prices.ToDownswings(closeType, true);

        var chart = ChartGenerator.CreatePriceChart(_prices, candlestick: true, lineCloses: false, lineWidth: 1)
            .WithLowerHighs(downswings, closeType)
            .WithLowerLows(downswings, closeType)
            .WithDownswings(downswings, closeType, lineWidth: 2, color: "red")
            .WithBreaksOfStructureMarkers(downswings, closeType, lineWidth: 3, color: "cyan", markerSize: 8, text: "BOS")
            .WithMarketStructureBreaksMarkers(downswings, closeType, lineWidth: 3, color: "orange", markerSize: 8, text: "MSB");
        SaveChart(chart, "actual_LowClosesIsRedBug.html");

        // 15:00 (60,884) and 16:00 (60,865) are two back-to-back new lows with nothing between
        // them to make a swing out of. 16:00 is green, so pre-fix this got folded into a segment
        // starting at 15:00 -- a phantom downswing, mirroring the 20:00/21:00 case on the high side.
        var phantomSwing = downswings.SingleOrDefault(x => x.InitialPrice.DateTime == new DateTime(2024, 8, 3, 15, 0, 0, DateTimeKind.Utc));

        Assert.IsNull(phantomSwing,
            "Expected no downswing anchored at 2024-08-03T15:00Z -- 16:00 is itself a new low, not a break " +
            "below one, so there is nothing for a swing to break out of here.");
    }
}
