using Plotly.NET;
using System.ComponentModel.DataAnnotations;

namespace Gradient.CryptoAnalysis.Test.PriceExtensions;

public class DownlegToRange
{
    public DownlegToRange([Required] Downleg downleg, EnumCloseType closeType = EnumCloseType.Close)
    {
        Downleg = downleg;
        CloseType = closeType;

        Downswings = Downleg.Prices.ToDownswings(CloseType, true);
    }

    public EnumCloseType CloseType { get; } = EnumCloseType.Close;
    public Downleg Downleg { get; }

    public List<Downswing> Downswings { get; } = new List<Downswing>();

    public GenericChart Analyse(GenericChart chart)
    {
        foreach (var downswing in Downswings.Skip(1))
        {
            var downswingToRange = new DownswingToRange(downswing);
            chart = downswingToRange.Analyse(chart);
        }
        return chart;
    }
}

public class DownswingToRange
{
    private Price? LiquidityHigh = null;

    private Price? LiquidityLow = null;

    private Price? MsbConfirmation = null;

    private Upswing? msbForConfirmation = null;

    private Price? MsbForConfirmationSwingLow = null;

    private Price? Retracement = null;

    public DownswingToRange([Required] Downswing downswing, EnumCloseType closeType = EnumCloseType.Close)
    {
        Downswing = downswing;
        CloseType = closeType;
    }

    public EnumCloseType CloseType { get; } = EnumCloseType.Close;

    public Downswing Downswing { get; }

    public GenericChart Analyse(GenericChart chart)
    {
        var previousDownswing = Downswing.PreviousDownswing;
        if (previousDownswing == null)
            return chart;

        var previousSwingHigh = previousDownswing.SwingHigh(CloseType);
        if (previousSwingHigh == null)
            return chart;

        var swingEntry = Downswing.Prices.First();
        var maxDelta = previousSwingHigh.CloseValue(CloseType) - swingEntry.CloseValue(CloseType);

        var potential = Downswing.Prices.First();
        foreach (var price in Downswing.Prices.Skip(1))
        {
            if (Retracement == null)
            {
                var currentDelta = price.CloseValue(CloseType) - swingEntry.CloseValue(CloseType);

                var ratio = currentDelta / maxDelta;
                if (ratio < 0.75)
                    continue;
                if (ratio > 1.0)
                    break;
                Retracement = price;
            }

            if (Retracement != null && msbForConfirmation == null)
            {
                var take = Downswing.Prices.IndexOf(price);
                var prices = Downswing.Prices.Take(take).ToList();

                var upswing = prices.ToUpswings(CloseType).FirstOrDefault();

                if (upswing == null)
                    continue;

                msbForConfirmation = upswing;
                MsbForConfirmationSwingLow = msbForConfirmation.SwingLow(CloseType);
                if (MsbForConfirmationSwingLow == null)
                    break;
            }

            if (Retracement != null && msbForConfirmation != null && MsbForConfirmationSwingLow != null && MsbConfirmation == null)
            {
                if (MsbForConfirmationSwingLow.CloseValue(CloseType) < price.CloseValue(CloseType))
                    continue;

                MsbConfirmation = price;
                LiquidityLow = potential;
                LiquidityHigh = Retracement;
            }
        }

        if (Retracement != null)
        {
            var last = Downswing.Prices.Last();

            chart = chart.WithFib(previousSwingHigh, swingEntry, CloseType, potential.DateTime, "black");
            var rangeLow = potential;
            var rangeHigh = Retracement;

            chart = chart
                .WithFib(rangeHigh, rangeLow, CloseType, last.DateTime, "blue")
                .WithDiscountZone(rangeHigh, rangeLow, CloseType, last.DateTime)
                .WithPremiumZone(rangeHigh, rangeLow, CloseType, last.DateTime)
                ;

            if (msbForConfirmation != null)
            {
                chart = chart
                    .WithUpswing(msbForConfirmation)
                    .WithHigherHighs([msbForConfirmation], CloseType)
                    .WithHigherLows([msbForConfirmation], CloseType)
                    ;
            }

            if (MsbConfirmation != null && MsbForConfirmationSwingLow != null)
            {
                var confirmation = new List<Price>
                    {
                        MsbForConfirmationSwingLow,
                        new Price()
                        {
                            DateTime = MsbConfirmation.DateTime,
                            Close = MsbForConfirmationSwingLow.Close,
                        }
                    };

                chart = chart.AddLayers(
                    ChartGenerator.PriceClosesLineLayer(confirmation, color: Color.fromString("yellow"))
                    );
            }

            if (MsbConfirmation != null && LiquidityLow != null && LiquidityHigh != null)
            {
                var lowLiquidityLine = new List<Price>
                    {
                        LiquidityLow,
                        new Price()
                        {
                            DateTime = MsbConfirmation.DateTime.AddDays(1),
                            Low = LiquidityLow.Low,
                        }
                    };

                var highLiquidityLine = new List<Price>
                    {
                        new Price()
                        {
                            DateTime = LiquidityLow.DateTime,
                            High = LiquidityHigh.High
                        },
                        new Price()
                        {
                            DateTime = MsbConfirmation.DateTime.AddDays(1),
                            High = LiquidityHigh.High,
                        }
                    };

                chart = chart.AddLayers(
                    ChartGenerator.CreatePriceLineLayer(lowLiquidityLine, p => (decimal)p.Low, "", Color.fromString("red"), 1),
                    ChartGenerator.CreatePriceLineLayer(highLiquidityLine, p => (decimal)p.High, "", Color.fromString("red"), 1)
                    );
            }
        }
        return chart;
    }
}

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
            //.WithHigherHighs(upswings, CloseType)
            //.WithHigherLows(upswings, CloseType)
            //.WithLowerHighs(downswings, CloseType)
            //.WithLowerLows(downswings, CloseType)
            .WithDownswings(downswings, closeType, lineWidth: 1, color: "cyan")
            //.WithUpswings(upswings, CloseType, lineWidth: 1, color: "green")
            //.WithBreaksOfStructureMarkers(upswings, CloseType, lineWidth: 3, color: "cyan", markerSize: 6)
            //.WithBreaksOfStructureMarkers(downswings, CloseType, lineWidth: 3, color: "cyan", markerSize: 6)
            //.WithMarketStructureBreaksMarkers(upswings, CloseType, lineWidth: 3, color: "orange", markerSize: 6)
            //.WithMarketStructureBreaksMarkers(downswings, CloseType, lineWidth: 3, color: "orange", markerSize: 6)
            ;

        var downlegToRange = new DownlegToRange(downleg, closeType);
        chart = downlegToRange.Analyse(chart);

        var uplegToRange = new UplegToRange(upleg, closeType);
        chart = uplegToRange.Analyse(chart);

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
            //.WithHigherHighs(upswings, CloseType)
            //.WithHigherLows(upswings, CloseType)
            //.WithLowerHighs(downswings, CloseType)
            //.WithLowerLows(downswings, CloseType)
            .WithDownswings(downswings, closeType, lineWidth: 1, color: "red")
            .WithUpswings(upswings, closeType, lineWidth: 1, color: "green")
            //.WithBreaksOfStructureMarkers(upswings, CloseType, lineWidth: 3, color: "cyan", markerSize: 6)
            //.WithBreaksOfStructureMarkers(downswings, CloseType, lineWidth: 3, color: "cyan", markerSize: 6)
            //.WithMarketStructureBreaksMarkers(upswings, CloseType, lineWidth: 3, color: "orange", markerSize: 6)
            //.WithMarketStructureBreaksMarkers(downswings, CloseType, lineWidth: 3, color: "orange", markerSize: 6)
            ;

        var downlegToRange = new DownlegToRange(downleg, closeType);
        chart = downlegToRange.Analyse(chart);

        var uplegToRange = new UplegToRange(upleg, closeType);
        chart = uplegToRange.Analyse(chart);

        if (interims > -1)
        {
            name = $"{name}_Interims_{interims}";
            foreach (var upswing in upswings)
            {
                var interimDownswings = upswing.InterimDownswings(closeType, true, true);
                var interimUpswings = upswing.InterimUpswings(closeType, true, true);

                chart = chart.WithInterimSwings(upswing, EnumCloseType.Close, interims)
                    //.WithHigherHighs(interimUpswings, CloseType)
                    //.WithHigherLows(interimUpswings, CloseType)
                    //.WithLowerHighs(interimDownswings, CloseType)
                    //.WithLowerLows(interimDownswings, CloseType)
                    ;
            }

            foreach (var downswing in downswings)
            {
                var interimDownswings = downswing.InterimDownswings(closeType, true, true);
                var interimUpswings = downswing.InterimUpswings(closeType, true, true);

                chart = chart.WithInterimSwings(downswing, EnumCloseType.Close, interims)
                    //.WithHigherHighs(interimUpswings, CloseType)
                    //.WithHigherLows(interimUpswings, CloseType)
                    //.WithLowerHighs(interimDownswings, CloseType)
                    //.WithLowerLows(interimDownswings, CloseType)
                    ;
            }
        }

        AssertChart(name, chart);
    }
}

public class UplegToRange
{
    public UplegToRange([Required] Upleg upleg, EnumCloseType closeType = EnumCloseType.Close)
    {
        Upleg = upleg;
        CloseType = closeType;

        Upswings = Upleg.Prices.ToUpswings(CloseType, true);
    }

    public EnumCloseType CloseType { get; } = EnumCloseType.Close;
    public Upleg Upleg { get; }

    public List<Upswing> Upswings { get; } = new List<Upswing>();

    public GenericChart Analyse(GenericChart chart)
    {
        foreach (var upswing in Upswings.Skip(1))
        {
            var upswingToRange = new UpswingToRange(upswing);
            chart = upswingToRange.Analyse(chart);
        }
        return chart;
    }
}

public class UpswingToRange
{
    private Price? LiquidityHigh = null;

    private Price? LiquidityLow = null;

    private Price? MsbConfirmation = null;

    private Downswing? msbForConfirmation = null;

    private Price? MsbForConfirmationSwingLow = null;

    private Price? Retracement = null;

    public UpswingToRange([Required] Upswing upswing, EnumCloseType closeType = EnumCloseType.Close)
    {
        Upswing = upswing;
        CloseType = closeType;
    }

    public EnumCloseType CloseType { get; } = EnumCloseType.Close;

    public Upswing Upswing { get; }

    public GenericChart Analyse(GenericChart chart)
    {
        var previousUpswing = Upswing.PreviousUpswing;
        if (previousUpswing == null)
            return chart;

        var previousSwingHigh = previousUpswing.SwingHigh(CloseType);
        if (previousSwingHigh == null)
            return chart;

        var swingEntry = Upswing.Prices.First();
        var maxDelta = previousSwingHigh.CloseValue(CloseType) - swingEntry.CloseValue(CloseType);

        var potential = Upswing.Prices.First();
        foreach (var price in Upswing.Prices.Skip(1))
        {
            if (Retracement == null)
            {
                var currentDelta = price.CloseValue(CloseType) - swingEntry.CloseValue(CloseType);

                var ratio = currentDelta / maxDelta;
                if (ratio < 0.75)
                    continue;
                if (ratio > 1.0)
                    break;
                Retracement = price;
            }

            if (Retracement != null && msbForConfirmation == null)
            {
                var take = Upswing.Prices.IndexOf(price);
                var prices = Upswing.Prices.Take(take).ToList();

                var WILLBEupswing = prices.ToDownswings(CloseType).FirstOrDefault();

                if (WILLBEupswing == null)
                    continue;

                msbForConfirmation = WILLBEupswing;
                MsbForConfirmationSwingLow = msbForConfirmation.SwingLow(CloseType);
                if (MsbForConfirmationSwingLow == null)
                    break;
            }

            if (Retracement != null && msbForConfirmation != null && MsbForConfirmationSwingLow != null && MsbConfirmation == null)
            {
                if (MsbForConfirmationSwingLow.CloseValue(CloseType) > price.CloseValue(CloseType))
                    continue;

                MsbConfirmation = price;
                LiquidityLow = Retracement;
                LiquidityHigh = potential;
            }
        }

        if (Retracement != null)
        {
            var last = Upswing.Prices.Last();

            chart = chart.WithFib(previousSwingHigh, swingEntry, CloseType, potential.DateTime, "black");
            var rangeLow = Retracement;
            var rangeHigh = potential;

            chart = chart
                .WithFib(rangeHigh, rangeLow, CloseType, last.DateTime, "blue")
                .WithDiscountZone(rangeHigh, rangeLow, CloseType, last.DateTime)
                .WithPremiumZone(rangeHigh, rangeLow, CloseType, last.DateTime)
                ;

            if (msbForConfirmation != null)
            {
                chart = chart
                    .WithDownswing(msbForConfirmation)
                    .WithLowerHighs([msbForConfirmation], CloseType)
                    .WithLowerLows([msbForConfirmation], CloseType)
                    ;
            }

            if (MsbConfirmation != null && MsbForConfirmationSwingLow != null)
            {
                var confirmation = new List<Price>
                    {
                        MsbForConfirmationSwingLow,
                        new Price()
                        {
                            DateTime = MsbConfirmation.DateTime,
                            Close = MsbForConfirmationSwingLow.Close,
                        }
                    };

                chart = chart.AddLayers(
                    ChartGenerator.PriceClosesLineLayer(confirmation, color: Color.fromString("yellow"))
                    );
            }

            if (MsbConfirmation != null && LiquidityLow != null && LiquidityHigh != null)
            {
                var lowLiquidityLine = new List<Price>
                    {
                        LiquidityLow,
                        new Price()
                        {
                            DateTime = MsbConfirmation.DateTime.AddDays(1),
                            Low = LiquidityLow.Low,
                        }
                    };

                var highLiquidityLine = new List<Price>
                    {
                        new Price()
                        {
                            DateTime = LiquidityLow.DateTime,
                            High = LiquidityHigh.High
                        },
                        new Price()
                        {
                            DateTime = MsbConfirmation.DateTime.AddDays(1),
                            High = LiquidityHigh.High,
                        }
                    };

                chart = chart.AddLayers(
                    ChartGenerator.CreatePriceLineLayer(lowLiquidityLine, p => (decimal)p.Low, "", Color.fromString("red"), 1),
                    ChartGenerator.CreatePriceLineLayer(highLiquidityLine, p => (decimal)p.High, "", Color.fromString("red"), 1)
                    );
            }
        }
        return chart;
    }
}