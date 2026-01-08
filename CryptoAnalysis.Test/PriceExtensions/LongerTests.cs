using Plotly.NET;

namespace Gradient.CryptoAnalysis.Test.PriceExtensions;

[TestClass]
public class LongerTests : PricesTests
{
    public override string TestDirectory => Path.Combine("PricesExtensionsData", "LongerTests");

    [DataTestMethod]
    [DataRow("ToDownswingTests_LineCloses", true, true, -1)]
    public void Test1(string name, bool candlestick, bool lineCloses, int interims)
    {
        var closeType = EnumCloseType.Close;
        var (upleg, downleg) = _prices.ToGlobalLegs(closeType);

        var upswings = upleg.Prices.ToUpswings(closeType, true);
        var downswings = downleg.Prices.ToDownswings(closeType, true);
        var chart = ChartGenerator.CreatePriceChart(_prices, candlestick: candlestick, lineCloses: lineCloses, lineWidth: 1);

        chart = chart
            //.WithHigherHighs(upswings, closeType)
            //.WithHigherLows(upswings, closeType)
            //.WithLowerHighs(downswings, closeType)
            //.WithLowerLows(downswings, closeType)
            .WithDownswings(downswings, closeType, lineWidth: 1, color: "cyan")
            //.WithUpswings(upswings, closeType, lineWidth: 1, color: "green")
            //.WithBreaksOfStructureMarkers(upswings, closeType, lineWidth: 3, color: "cyan", markerSize: 6)
            //.WithBreaksOfStructureMarkers(downswings, closeType, lineWidth: 3, color: "cyan", markerSize: 6)
            //.WithMarketStructureBreaksMarkers(upswings, closeType, lineWidth: 3, color: "orange", markerSize: 6)
            //.WithMarketStructureBreaksMarkers(downswings, closeType, lineWidth: 3, color: "orange", markerSize: 6)
            ;

        var lastDownswing = downswings.Last();

        foreach (var downswing in downswings.Skip(1))
        {
            var potential = downswing.Prices.First();
            Price? retracement = null;
            Upswing? msbForConfirmation = null;
            Price? msbForConfirmationSwingLow = null;
            Price? msbConfirmation = null;
            Price? liquidityLow = null;
            Price? liquidityHigh = null;

            var previousDownswing = downswing.PreviousDownswing;
            Assert.IsNotNull(previousDownswing);

            var previousSwingHigh = previousDownswing.SwingHigh(closeType);
            Assert.IsNotNull(previousSwingHigh);

            var high = previousSwingHigh;
            var low = downswing.Prices.First();
            var maxDelta = high.CloseValue(closeType) - low.CloseValue(closeType);
            var last = downswing.Prices.Last();
            foreach (var price in downswing.Prices.Skip(1))
            {
                if (retracement == null)
                {
                    var currentDelta = price.CloseValue(closeType) - low.CloseValue(closeType);

                    var ratio = currentDelta / maxDelta;
                    if (ratio < 0.75)
                        continue;
                    if (ratio > 1.0)
                        break;
                    retracement = price;
                }

                if (retracement != null && msbForConfirmation == null)
                {
                    var take = downswing.Prices.IndexOf(price);
                    var prices = downswing.Prices.Take(take).ToList();

                    var upswing = prices.ToUpswings(closeType).FirstOrDefault();

                    if (upswing == null)
                        continue;

                    msbForConfirmation = upswing;
                    msbForConfirmationSwingLow = msbForConfirmation.SwingLow(closeType);
                    if (msbForConfirmationSwingLow == null)
                        break;
                }

                if (retracement != null && msbForConfirmation != null && msbForConfirmationSwingLow != null && msbConfirmation == null)
                {
                    if (msbForConfirmationSwingLow.CloseValue(closeType) < price.CloseValue(closeType))
                        continue;

                    msbConfirmation = price;
                    liquidityLow = potential;
                    liquidityHigh = retracement;
                }
            }

            if (retracement != null)
            {
                chart = chart.WithFib(high, low, closeType, potential.DateTime, "black");
                var rangeLow = potential;
                var rangeHigh = retracement;

                chart = chart
                    .WithFib(rangeHigh, rangeLow, closeType, last.DateTime, "blue")
                    .WithDiscountZone(rangeHigh, rangeLow, closeType, last.DateTime)
                    .WithPremiumZone(rangeHigh, rangeLow, closeType, last.DateTime)
                    ;

                if (msbForConfirmation != null)
                {
                    chart = chart
                        .WithUpswing(msbForConfirmation)
                        .WithHigherHighs([msbForConfirmation], closeType)
                        .WithHigherLows([msbForConfirmation], closeType)
                        ;
                }

                if (msbConfirmation != null && msbForConfirmationSwingLow != null)
                {
                    var confirmation = new List<Price>
                    {
                        msbForConfirmationSwingLow,
                        new Price()
                        {
                            DateTime = msbConfirmation.DateTime,
                            Close = msbForConfirmationSwingLow.Close,
                        }
                    };

                    chart = chart.AddLayers(
                        ChartGenerator.PriceClosesLineLayer(confirmation, color: Color.fromString("yellow"))
                        );
                }

                if (liquidityLow != null && liquidityHigh != null)
                {
                    var lowLiquidityLine = new List<Price>
                    {
                        liquidityLow,
                        new Price()
                        {
                            DateTime = msbConfirmation.DateTime.AddDays(1),
                            Low = liquidityLow.Low,
                        }
                    };

                    var highLiquidityLine = new List<Price>
                    {
                        new Price()
                        {
                            DateTime = liquidityLow.DateTime,
                            High = liquidityHigh.High
                        },
                        new Price()
                        {
                            DateTime = msbConfirmation.DateTime.AddDays(1),
                            High = liquidityHigh.High,
                        }
                    };

                    chart = chart.AddLayers(
                        ChartGenerator.CreatePriceLineLayer(lowLiquidityLine, p => (decimal)p.Low, name, Color.fromString("red"), 1),
                        ChartGenerator.CreatePriceLineLayer(highLiquidityLine, p => (decimal)p.High, name, Color.fromString("red"), 1)
                        );
                }
            }
        }

        if (interims > -1)
        {
            name = $"{name}_Interims_{interims}";
            foreach (var upswing in upswings)
            {
                var interimDownswings = upswing.InterimDownswings(closeType, true, true);
                var interimUpswings = upswing.InterimUpswings(closeType, true, true);

                chart = chart.WithInterimSwings(upswing, EnumCloseType.Close, interims)
                    .WithHigherHighs(interimUpswings, closeType)
                    .WithHigherLows(interimUpswings, closeType)
                    .WithLowerHighs(interimDownswings, closeType)
                    .WithLowerLows(interimDownswings, closeType)
                    ;
            }

            foreach (var downswing in downswings)
            {
                var interimDownswings = downswing.InterimDownswings(closeType, true, true);
                var interimUpswings = downswing.InterimUpswings(closeType, true, true);

                chart = chart.WithInterimSwings(downswing, EnumCloseType.Close, interims)
                    .WithHigherHighs(interimUpswings, closeType)
                    .WithHigherLows(interimUpswings, closeType)
                    .WithLowerHighs(interimDownswings, closeType)
                    .WithLowerLows(interimDownswings, closeType)
                    ;
            }
        }

        AssertChart(name, chart);
    }

    [DataTestMethod]
    [DataRow("ToDownswingTests_LineCloses", true, true, 0)]
    public void Test2(string name, bool candlestick, bool lineCloses, int interims)
    {
        var closeType = EnumCloseType.Close;
        var (upleg, downleg) = _prices.ToGlobalLegs(closeType);

        var upswings = upleg.Prices.ToUpswings(closeType, true);
        var downswings = downleg.Prices.ToDownswings(closeType, true);
        var chart = ChartGenerator.CreatePriceChart(_prices, candlestick: candlestick, lineCloses: lineCloses, lineWidth: 1);

        chart = chart
            //.WithHigherHighs(upswings, closeType)
            //.WithHigherLows(upswings, closeType)
            //.WithLowerHighs(downswings, closeType)
            //.WithLowerLows(downswings, closeType)
            .WithDownswings(downswings, closeType, lineWidth: 1, color: "red")
            .WithUpswings(upswings, closeType, lineWidth: 1, color: "green")
            //.WithBreaksOfStructureMarkers(upswings, closeType, lineWidth: 3, color: "cyan", markerSize: 6)
            //.WithBreaksOfStructureMarkers(downswings, closeType, lineWidth: 3, color: "cyan", markerSize: 6)
            //.WithMarketStructureBreaksMarkers(upswings, closeType, lineWidth: 3, color: "orange", markerSize: 6)
            //.WithMarketStructureBreaksMarkers(downswings, closeType, lineWidth: 3, color: "orange", markerSize: 6)
            ;

        if (interims > -1)
        {
            name = $"{name}_Interims_{interims}";
            foreach (var upswing in upswings)
            {
                var interimDownswings = upswing.InterimDownswings(closeType, true, true);
                var interimUpswings = upswing.InterimUpswings(closeType, true, true);

                chart = chart.WithInterimSwings(upswing, EnumCloseType.Close, interims)
                    //.WithHigherHighs(interimUpswings, closeType)
                    //.WithHigherLows(interimUpswings, closeType)
                    //.WithLowerHighs(interimDownswings, closeType)
                    //.WithLowerLows(interimDownswings, closeType)
                    ;
            }

            foreach (var downswing in downswings)
            {
                var interimDownswings = downswing.InterimDownswings(closeType, true, true);
                var interimUpswings = downswing.InterimUpswings(closeType, true, true);

                chart = chart.WithInterimSwings(downswing, EnumCloseType.Close, interims)
                    //.WithHigherHighs(interimUpswings, closeType)
                    //.WithHigherLows(interimUpswings, closeType)
                    //.WithLowerHighs(interimDownswings, closeType)
                    //.WithLowerLows(interimDownswings, closeType)
                    ;
            }
        }

        AssertChart(name, chart);
    }
}