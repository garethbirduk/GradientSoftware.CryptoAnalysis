using Microsoft.FSharp.Core;
using Plotly.NET;
using Plotly.NET.TraceObjects;

namespace Gradient.CryptoAnalysis.Test.PriceExtensions;

[TestClass]
public class IsRetracementConditionTests : PricesTests
{
    public override string TestDirectory => Path.Combine("Conditions", "PriceConditions", "IsRetracementConditionTests");

    [TestMethod]
    public void TestIsMet()
    {
        var upswings = _prices.ToUpswings(EnumCloseType.Close, true, false);
        var chart = ChartGenerator.CreatePriceChart(_prices, candlestick: true, lineWidth: 1)
            .WithUpswings(upswings, EnumCloseType.Close, "cyan", lineWidth: 3)
            .WithBreaksOfStructure(upswings, EnumCloseType.Close)
            .WithMarketStructureBreaks(upswings, EnumCloseType.Close)
            .WithHigherHighs(upswings, EnumCloseType.Close)
            .WithHigherLows(upswings, EnumCloseType.Close)
            ;

        foreach (var upswing in upswings)
        {
            chart = chart.WithInterimUpswings(upswing, EnumCloseType.Close, 3);
        }

        foreach (var upswing in upswings.Where(x => x != upswings.First()))
        {
            var high = upswing.SwingHigh(EnumCloseType.High);
            if (high == null)
                continue;
            var previousUpswing = upswings[upswings.IndexOf(upswing) - 1];

            Price? low = null;

            var maxDepth = 3;
            var previousInterimUpswings = previousUpswing.FinalInterimUpswings(EnumCloseType.Low, maxDepth);

            if (previousInterimUpswings.Any())
                low = previousInterimUpswings.Last().SwingLow(EnumCloseType.Low);
            else
                low = previousUpswing.SwingLow(EnumCloseType.Low);
            if (low == null)
                continue;

            chart = chart
                .WithHorizontalLinesByGain_GreyscalePrefix(low, high, upswing.Prices.Last().DateTime, EnumCloseType.Low, EnumCloseType.High, lineWidth: 3)
                .WithPreviousSwingLowMarker(low, upswing.Prices.Last().DateTime, EnumCloseType.Low, lineWidth: 1)
                ;

            //var finalInterimUpswings = upswing.FinalInterimUpswings(EnumCloseType.Close, maxDepth);
            //if (finalInterimUpswings.Any())
            //{
            //    var nextUpswing = upswings[upswings.IndexOf(upswing) + 1];
            //    var previousUpswing = finalInterimUpswings.Last();
            //}

            //chart = chart.WithInterimUpswings(upswing, maxDepth);
        }

        for (int i = 1; i < _prices.Count; i++)
        {
            var conditionPrices = _prices.Take(i + 1).ToList();

            var price = _prices[i];

            var condition = new IsRetracementCondition(conditionPrices, 0.05, EnumCloseType.Low, EnumCloseType.High, EnumCloseType.Low);
            try
            {
                condition.SetPrice(price.DateTime);
            }
            catch (Exception ex)
            {
                continue;
            }

            if (condition.IsMet(false))
            {
                var point = Chart2D.Chart.Point<DateTime, double, string>(
                    x: new[] { price.DateTime },
                    y: new[] { price.Low }
                )
                .WithMarker(Marker.init(
                    Color: Color.fromString("black"),
                    Size: FSharpOption<int>.Some(12)
                    )
                );

                chart = Chart.Combine(new[] { chart, point });
            }
        }

        var name = "IsRetracementConditionTests";
        SaveChart(chart, $"actual_{name}.html");
    }
}