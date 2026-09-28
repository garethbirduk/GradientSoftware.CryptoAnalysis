using Plotly.NET;

namespace Gradient.CryptoAnalysis.Test.PriceExtensions;

// Real Coinbase BTC/USD hourly candles, 2020-11-12T10:00Z to 2020-11-13T01:00Z (bugs.md).
// Two candles in this window (16:00 and 21:00) close at a genuine new high while
// closing red -- HighClosesIsGreen (PricesExtensions/Closes.cs) skips both, purely
// for their own body colour, and ToHighSegments then anchors the resulting swings
// to the wrong candle. Both tests here fail against the current code and should
// pass once HighClosesIsGreen stops gating on candle colour.
[TestClass]
public class HighClosesIsGreenBugTests : PricesTests
{
    public override string TestDirectory => Path.Combine("PricesExtensionsData", "HighClosesIsGreenBugTests");

    private GenericChart Chart(List<Upswing> upswings, List<Downswing> downswings, EnumCloseType closeType)
    {
        return ChartGenerator.CreatePriceChart(_prices, candlestick: true, lineCloses: false, lineWidth: 1)
            .WithHigherHighs(upswings, closeType)
            .WithHigherLows(upswings, closeType)
            .WithUpswings(upswings, closeType, lineWidth: 2, color: "green")
            .WithBreaksOfStructureMarkers(upswings, closeType, lineWidth: 3, color: "cyan", markerSize: 8, text: "BOS")
            .WithMarketStructureBreaksMarkers(upswings, closeType, lineWidth: 3, color: "orange", markerSize: 8, text: "MSB");
    }

    [TestMethod]
    public void HighClosesIsGreen_InventsAPhantomSwingWhenTheColourGateSkipsABackToBackNewHigh()
    {
        var closeType = EnumCloseType.Close;
        var upswings = _prices.ToUpswings(closeType, true);
        var downswings = _prices.ToDownswings(closeType, true);

        // always write the chart, pass or fail, so the current structure can be opened and inspected
        SaveChart(Chart(upswings, downswings, closeType), "actual_HighClosesIsGreenBug.html");

        // 20:00 (16163.19) and 21:00 (16166.99) are two separate, back-to-back new highs with no pullback
        // between them -- there is no swing to find there. 21:00 is red (close 16166.99 < open 16172.47), so
        // HighClosesIsGreen skips it, and ToHighSegments folds it into a segment starting at 20:00 instead,
        // reporting a phantom Upswing whose "break of structure" is really just 21:00 being its own new high.
        var phantomSwing = upswings.SingleOrDefault(x => x.InitialPrice.DateTime == new DateTime(2020, 11, 12, 20, 0, 0, DateTimeKind.Utc));

        Assert.IsNull(phantomSwing,
            "Expected no swing anchored at 2020-11-12T20:00Z -- 21:00 is itself a new high, not a break " +
            "above one, so there is nothing for a swing to break out of here.");
    }

    [TestMethod]
    public void HighClosesIsGreen_AnchorsTheSwingToTheWrongCandle()
    {
        var closeType = EnumCloseType.Close;
        var upswings = _prices.ToUpswings(closeType, true);
        var downswings = _prices.ToDownswings(closeType, true);

        SaveChart(Chart(upswings, downswings, closeType), "actual_HighClosesIsGreenBug.html");

        // 16:00 closes at 16155.78, a genuine new high over everything before it (prior high 16155.71 at
        // 15:00) -- but it's red (close 16155.78 < open 16158.26), so HighClosesIsGreen skips it too. The
        // resulting swing gets anchored two candles early, at 15:00, instead of at 16:00 where the true new
        // high actually is.
        var wronglyAnchored = upswings.SingleOrDefault(x => x.InitialPrice.DateTime == new DateTime(2020, 11, 12, 15, 0, 0, DateTimeKind.Utc));
        var correctlyAnchored = upswings.SingleOrDefault(x => x.InitialPrice.DateTime == new DateTime(2020, 11, 12, 16, 0, 0, DateTimeKind.Utc));

        Assert.IsNull(wronglyAnchored,
            "Expected no swing anchored at 2020-11-12T15:00Z -- 16:00 was itself the new high, not a break above one.");
        Assert.IsNotNull(correctlyAnchored,
            "Expected a swing anchored at 2020-11-12T16:00Z, the candle that was actually the new high.");
    }
}
