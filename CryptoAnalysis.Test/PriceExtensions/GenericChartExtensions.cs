using Plotly.NET;

namespace Gradient.CryptoAnalysis.Test.PriceExtensions;

public static class GenericChartExtensions
{
    private static GenericChart WithBreakOfStructure(this GenericChart chart, Upswing upswing, string color = "green", int lineWidth = 1)
    {
        var p1 = upswing.Prices.First();
        var p2 = upswing.NextPrice;

        if (p2 == null)
            return chart;

        var p = new List<Price>
            {
                p1,
                new Price
                {
                    Close = p1.Close,
                    DateTime = p2.DateTime
                }
            };

        chart = chart.AddLayers(
            ChartGenerator.PriceClosesLineLayer(p, lineWidth: 1, color: Color.fromString(color))
            );
        return chart;
    }

    private static GenericChart WithBreakOfStructure(this GenericChart chart, Downswing downswing, string color = "red", int lineWidth = 1)
    {
        var p1 = downswing.Prices.First();
        var p2 = downswing.NextPrice;

        if (p2 == null)
            return chart;

        var p = new List<Price>
            {
                p1,
                new Price
                {
                    Close = p1.Close,
                    DateTime = p2.DateTime
                }
            };

        chart = chart.AddLayers(
            ChartGenerator.PriceClosesLineLayer(p, lineWidth: 1, color: Color.fromString(color))
            );
        return chart;
    }

    public static GenericChart DownUpswings(this GenericChart chart, List<Downswing> downswings, EnumCloseType closeType, string color = "cyan", int markerSize = 6, int lineWidth = 1)
    {
        foreach (var downswing in downswings.Where(x => x.BreakOfStructure != null))
        {
            chart = WithDownswing(chart, downswing, color, lineWidth);
        }

        return chart;
    }

    public static GenericChart WithBreaksOfStructureMarkers(this GenericChart chart, List<Downswing> downswings, EnumCloseType closeType,
        string color = "cyan", int markerSize = 12, int lineWidth = 1, BreakOfStructureFormat breakOfStructureFormat = BreakOfStructureFormat.Box)
    {
        chart = chart.AddLayers(new Layer
        {
            Name = "BOS",
            ChartFactory = () => ChartGenerator.GenerateScatterChart(
                downswings.Where(x => x.BreakOfStructure != null).Select(x => x.BreakOfStructure).ToList(),
                p => (decimal)p.Close,
                color: Color.fromString(color),
                markerSize: markerSize
            ),
        });
        return chart;
    }

    public static GenericChart WithBreaksOfStructureMarkers(this GenericChart chart, List<Upswing> upswings, EnumCloseType closeType,
            string color = "cyan", int markerSize = 12, int lineWidth = 1, BreakOfStructureFormat breakOfStructureFormat = BreakOfStructureFormat.Box)
    {
        chart = chart.AddLayers(new Layer
        {
            Name = "BOS",
            ChartFactory = () => ChartGenerator.GenerateScatterChart(
                upswings.Where(x => x.BreakOfStructure != null).Select(x => x.BreakOfStructure).ToList(),
                p => (decimal)p.Close,
                color: Color.fromString(color),
                markerSize: markerSize
            ),
        });
        return chart;
    }

    public static GenericChart WithBreaksOfStructureReferences(this GenericChart chart, List<Downswing> downswings, EnumCloseType closeType,
        string color = "cyan", int markerSize = 12, int lineWidth = 1, BreakOfStructureFormat breakOfStructureFormat = BreakOfStructureFormat.Box)
    {
        foreach (var downswing in downswings.Where(x => x.BreakOfStructure != null))
        {
            chart = chart.WithBreakOfStructure(downswing, color, lineWidth);
        }

        return chart;
    }

    public static GenericChart WithBreaksOfStructureReferences(this GenericChart chart, List<Upswing> upswings, EnumCloseType closeType,
        string color = "cyan", int markerSize = 12, int lineWidth = 1, BreakOfStructureFormat breakOfStructureFormat = BreakOfStructureFormat.Box)
    {
        foreach (var upswing in upswings.Where(x => x.BreakOfStructure != null))
        {
            chart = chart.WithBreakOfStructure(upswing, color, lineWidth);
        }

        return chart;
    }

    public static GenericChart WithDownswing(this GenericChart chart, Downswing downswing, string color = "cyan", int lineWidth = 1)
    {
        if (downswing.SwingType() == EnumSwingType.DownlegOnly)
            return chart;

        var p1 = downswing.Prices.First();
        var p2 = downswing.NextPrice;

        if (p1 == null || p2 == null)
            return chart;

        var swingHigh = downswing.SwingHigh(EnumCloseType.Close);

        if (swingHigh == null) return chart;

        var topY = p1.Close;
        var bottomY = swingHigh.Close;
        var leftX = p1.DateTime;
        var rightX = p2.DateTime;

        var topLine = new List<Price>
        {
            new Price { Close = topY, DateTime = leftX },
            new Price { Close = topY, DateTime = rightX }
        };

        var bottomLine = new List<Price>
        {
            new Price { Close = bottomY, DateTime = leftX },
            new Price { Close = bottomY, DateTime = rightX }
        };

        var leftLine = new List<Price>
        {
            new Price { Close = topY, DateTime = leftX },
            new Price { Close = bottomY, DateTime = leftX }
        };

        var rightLine = new List<Price>
        {
            new Price { Close = topY, DateTime = rightX },
            new Price { Close = bottomY, DateTime = rightX }
        };

        chart = chart.AddLayers(
            ChartGenerator.PriceClosesLineLayer(topLine, lineWidth: lineWidth, color: Color.fromString(color)),
            ChartGenerator.PriceClosesLineLayer(bottomLine, lineWidth: lineWidth, color: Color.fromString(color)),
            ChartGenerator.PriceClosesLineLayer(leftLine, lineWidth: lineWidth, color: Color.fromString(color)),
            ChartGenerator.PriceClosesLineLayer(rightLine, lineWidth: lineWidth, color: Color.fromString(color))
        );

        return chart;
    }

    public static GenericChart WithDownswings(this GenericChart chart, IEnumerable<Downswing> downswings, EnumCloseType closeType, string color = "cyan", int markerSize = 6, int lineWidth = 1)
    {
        foreach (var downswing in downswings.Where(x => x.BreakOfStructure != null))
        {
            chart = WithDownswing(chart, downswing, color, lineWidth);
        }

        return chart;
    }

    //public static GenericChart WithDownswingSawtooths(this GenericChart chart, Downswing downswing, EnumCloseType closeType,
    //    string color = "cyan", int markerSize = 6, int lineWidth = 1)
    //{
    //    var downSawtooth = downswing.DownlegPrices(closeType, true).ToDownwardSawtooth(closeType, true);
    //    chart = chart.WithSawtooth(downSawtooth, closeType, color: "red");
    //    var upwardSawtooth = downswing.UplegPrices(closeType, true).ToUpwardSawtooth(closeType, true);
    //    chart = chart.WithSawtooth(upwardSawtooth, closeType, color: "green");

    //    return chart;
    //}

    //public static GenericChart WithDownswingsSawtooths(this GenericChart chart, List<Downswing> downswings, EnumCloseType closeType,
    //    string color = "cyan", int markerSize = 6, int lineWidth = 1)
    //{
    //    var prices = new List<Price>();
    //    foreach (var downswing in downswings)
    //    {
    //        prices.Add(downswing.Prices.First());
    //        if (downswing.SwingHigh(closeType) != null)
    //            prices.Add(downswing.SwingHigh(closeType));
    //    }
    //    var sawtooth = prices.ToDownwardSawtooth(closeType);
    //    chart = chart.WithSawtooth(sawtooth, closeType, "red");
    //    return chart;
    //}

    public static GenericChart WithHigherHighs(this GenericChart chart, IEnumerable<Upswing> upswings, EnumCloseType closeType, string color = "green", int markerSize = 12)
    {
        return chart.AddLayers(new Layer
        {
            Name = "Higher highs",
            ChartFactory = () => ChartGenerator.GenerateScatterChart(
                upswings.Select(x => x.Prices.First()).ToList(),
                p => (decimal)p.CloseValue(closeType),
                color: Color.fromString(color),
                markerSize: markerSize,
                text: "HH"
            ),
        });
    }

    public static GenericChart WithHigherHighs(this GenericChart chart, IEnumerable<Price> higherHighs, EnumCloseType closeType, string color = "green", int markerSize = 12)
    {
        return chart.AddLayers(new Layer
        {
            Name = "Higher highs",
            ChartFactory = () => ChartGenerator.GenerateScatterChart(
                higherHighs,
                p => (decimal)p.CloseValue(closeType),
                color: Color.fromString(color),
                markerSize: markerSize,
                text: "HH"
            ),
        });
    }

    public static GenericChart WithHigherLows(this GenericChart chart, IEnumerable<Upswing> upswings, EnumCloseType closeType, string color = "red", int markerSize = 12)
    {
        var s = upswings.Select(x => x.SwingLow(EnumCloseType.Close)).ToList();
        return chart.AddLayers(new Layer
        {
            Name = "Higher lows",
            ChartFactory = () => ChartGenerator.GenerateScatterChart(
                s,
                p => (decimal)p.CloseValue(closeType),
                color: Color.fromString(color),
                markerSize: markerSize,
                text: "HL"
            )
        });
    }

    public static GenericChart WithHigherLows(this GenericChart chart, IEnumerable<Price> higherLows, EnumCloseType closeType, string color = "red", int markerSize = 12)
    {
        {
            return chart.AddLayers(new Layer
            {
                Name = "Higher lows",
                ChartFactory = () => ChartGenerator.GenerateScatterChart(
                    higherLows,
                    p => (decimal)p.CloseValue(closeType),
                    color: Color.fromString(color),
                    markerSize: markerSize,
                    text: "HL"
                ),
            });
        }
    }

    public static GenericChart WithHorizontalLineByGain(this GenericChart chart, Price low, Price high, DateTime extent, double gain, EnumCloseType lowCloseType, EnumCloseType highCloseType,
        string color = "orange", int markerSize = 6, int lineWidth = 1)
    {
        var highLowDelta = high.CloseValue(highCloseType) - low.CloseValue(lowCloseType);
        var retracementDelta = highLowDelta * gain;

        var topY = low.CloseValue(lowCloseType) + retracementDelta;
        var leftX = low.DateTime;
        var rightX = extent;

        var line = new List<Price>
        {
            new Price { Close = topY, DateTime = high.DateTime },
            new Price { Close = topY, DateTime = extent }
        };

        chart = chart.AddLayers(
            ChartGenerator.PriceClosesLineLayer(line, lineWidth: lineWidth, color: Color.fromString(color))
            );

        return chart;
    }

    public static GenericChart WithHorizontalLinesByGain(this GenericChart chart, Price low, Price high, DateTime extent, EnumCloseType lowCloseType, EnumCloseType highCloseType,
        string color = "orange", int markerSize = 6, int lineWidth = 1, params double[] gains)
    {
        foreach (var gain in gains)
            chart = chart.WithHorizontalLineByGain(low, high, extent, gain, lowCloseType, highCloseType, color, markerSize, lineWidth);
        return chart;
    }

    public static GenericChart WithHorizontalLinesByGain_GreyscalePrefix(this GenericChart chart, Upswing upswing, Upswing nextUpswing, DateTime extent, EnumCloseType lowCloseType, EnumCloseType highCloseType, int markerSize = 6, int lineWidth = 1)
    {
        var low = upswing.SwingLow(lowCloseType);
        if (low == null)
            return chart;

        var high = nextUpswing.Prices.First();
        var last = nextUpswing.Prices.Last();

        return chart.WithHorizontalLinesByGain_GreyscalePrefix(low, high, nextUpswing.Prices.Last().DateTime, lowCloseType, highCloseType, markerSize, lineWidth);
    }

    public static GenericChart WithHorizontalLinesByGain_GreyscalePrefix(this GenericChart chart, Price low, Price high, DateTime extent, EnumCloseType lowCloseType, EnumCloseType highCloseType,
        int markerSize = 6, int lineWidth = 1, params double[] gains)
    {
        chart = chart.WithHorizontalLineByGain(low, high, extent, 1.25, lowCloseType, highCloseType, color: "gray", markerSize, lineWidth);
        chart = chart.WithHorizontalLineByGain(low, high, extent, 1.0, lowCloseType, highCloseType, color: "black", markerSize, lineWidth);
        chart = chart.WithHorizontalLineByGain(low, high, extent, 0.75, lowCloseType, highCloseType, color: "gray", markerSize, lineWidth);
        chart = chart.WithHorizontalLineByGain(low, high, extent, 0.5, lowCloseType, highCloseType, color: "gray", markerSize, lineWidth);
        chart = chart.WithHorizontalLineByGain(low, high, extent, 0.25, lowCloseType, highCloseType, color: "gray", markerSize, lineWidth);
        chart = chart.WithHorizontalLineByGain(low, high, extent, 0.0, lowCloseType, highCloseType, color: "black", markerSize, lineWidth);
        chart = chart.WithHorizontalLineByGain(low, high, extent, -0.25, lowCloseType, highCloseType, color: "gray", markerSize, lineWidth);
        return chart;
    }

    public static GenericChart WithHorizontalLinesByGain_RainbowPrefix(this GenericChart chart, Upswing upswing, Upswing nextUpswing, DateTime extent, EnumCloseType lowCloseType, EnumCloseType highCloseType, int markerSize = 6, int lineWidth = 1)
    {
        var low = upswing.SwingLow(lowCloseType);
        if (low == null)
            return chart;

        var high = nextUpswing.Prices.First();
        var last = nextUpswing.Prices.Last();

        return chart.WithHorizontalLinesByGain_RainbowPrefix(low, high, nextUpswing.Prices.Last().DateTime, lowCloseType, highCloseType, markerSize, lineWidth);
    }

    public static GenericChart WithHorizontalLinesByGain_RainbowPrefix(this GenericChart chart, Price low, Price high, DateTime extent, EnumCloseType lowCloseType, EnumCloseType highCloseType,
        int markerSize = 6, int lineWidth = 1, params double[] gains)
    {
        chart = chart.WithHorizontalLineByGain(low, high, extent, 1.25, lowCloseType, highCloseType, color: "purple", markerSize, lineWidth);
        chart = chart.WithHorizontalLineByGain(low, high, extent, 1.0, lowCloseType, highCloseType, color: "blue", markerSize, lineWidth);
        chart = chart.WithHorizontalLineByGain(low, high, extent, 0.75, lowCloseType, highCloseType, color: "cyan", markerSize, lineWidth);
        chart = chart.WithHorizontalLineByGain(low, high, extent, 0.5, lowCloseType, highCloseType, color: "green", markerSize, lineWidth);
        chart = chart.WithHorizontalLineByGain(low, high, extent, 0.25, lowCloseType, highCloseType, color: "yellow", markerSize, lineWidth);
        chart = chart.WithHorizontalLineByGain(low, high, extent, 0.0, lowCloseType, highCloseType, color: "orange", markerSize, lineWidth);
        chart = chart.WithHorizontalLineByGain(low, high, extent, -0.25, lowCloseType, highCloseType, color: "red", markerSize, lineWidth);
        return chart;
    }

    public static GenericChart WithInterimSwings(this GenericChart chart, Downswing downswing, EnumCloseType closeType = EnumCloseType.Close,
        int maxDepth = 0, int depth = 0)
    {
        var interimUpswings = downswing.InterimUpswings(closeType, true, true);
        var interimDownswings = downswing.InterimDownswings(closeType, true, true);

        chart = chart
            .WithLowerHighs(interimDownswings, EnumCloseType.Close, markerSize: 6, color: "green")
            .WithLowerLows(interimDownswings, EnumCloseType.Close, markerSize: 6, color: "red")

            .WithHigherHighs(interimUpswings, EnumCloseType.Close, markerSize: 6, color: "green")
            .WithHigherLows(interimUpswings, EnumCloseType.Close, markerSize: 6, color: "red")

            .WithUpswings(interimUpswings, EnumCloseType.Close, markerSize: 6, color: "green")
            .WithDownswings(interimDownswings, EnumCloseType.Close, markerSize: 6, color: "red")

            .WithBreaksOfStructureMarkers(interimUpswings, closeType, lineWidth: 3, color: "cyan", markerSize: 6)
            .WithBreaksOfStructureMarkers(interimDownswings, closeType, lineWidth: 3, color: "cyan", markerSize: 6)

            .WithMarketStructureBreaksMarkers(interimUpswings, closeType, lineWidth: 3, color: "orange", markerSize: 6)
            .WithMarketStructureBreaksMarkers(interimDownswings, closeType, lineWidth: 3, color: "orange", markerSize: 6)
        ;

        //var uplegSawtoothPrices = new List<Price>()
        //{
        //    downswing.Prices.First()
        //};
        //foreach (var interimUpswing in interimUpswings)
        //{
        //    uplegSawtoothPrices.Add(interimUpswing.Prices.First());
        //    var interimSwinglow = interimUpswing.SwingLow(closeType);
        //    if (interimSwinglow != null)
        //        uplegSawtoothPrices.Add(interimSwinglow);
        //}
        //var swinghigh = downswing.SwingHigh(closeType);
        //if (swinghigh != null)
        //    uplegSawtoothPrices.Add(swinghigh);
        //chart = chart.WithSawtooth(uplegSawtoothPrices, closeType, "green");

        //var downlegSawtoothPrices = new List<Price>();
        //if (swinghigh != null)
        //    downlegSawtoothPrices.Add(swinghigh);
        //foreach (var interimDownswing in interimDownswings)
        //{
        //    downlegSawtoothPrices.Add(interimDownswing.Prices.First());
        //    var interimSwingHigh = interimDownswing.SwingHigh(closeType);
        //    if (interimSwingHigh != null)
        //        downlegSawtoothPrices.Add(interimSwingHigh);
        //}
        //var nextPrice = downswing.NextPrice;
        //if (nextPrice != null)
        //    downlegSawtoothPrices.Add(nextPrice);
        //chart = chart.WithSawtooth(downlegSawtoothPrices, closeType, "red");

        while (depth < maxDepth)
        {
            if (interimUpswings.Count + interimDownswings.Count == 0)
                depth = maxDepth;
            foreach (var interimUpswing in interimUpswings)
                chart = chart.WithInterimSwings(interimUpswing, EnumCloseType.Close, maxDepth: maxDepth, depth: depth + 1);
            foreach (var interimDownswing in interimDownswings)
                chart = chart.WithInterimSwings(interimDownswing, EnumCloseType.Close, maxDepth: maxDepth, depth: depth + 1);
            depth++;
        }
        return chart;
    }

    public static GenericChart WithInterimSwings(this GenericChart chart, Upswing upswing, EnumCloseType closeType = EnumCloseType.Close,
        int maxDepth = 0, int depth = 0)
    {
        var interimDownswings = upswing.InterimDownswings(closeType, true, true);
        var interimUpswings = upswing.InterimUpswings(closeType, true, true);

        chart = chart
            .WithLowerHighs(interimDownswings, closeType, markerSize: 6, color: "green")
            .WithLowerLows(interimDownswings, closeType, markerSize: 6, color: "red")

            .WithHigherHighs(interimUpswings, closeType, markerSize: 6, color: "green")
            .WithHigherLows(interimUpswings, closeType, markerSize: 6, color: "red")

            .WithDownswings(interimDownswings, closeType, markerSize: 6, color: "red")
            .WithUpswings(interimUpswings, closeType, markerSize: 6, color: "green")

            .WithBreaksOfStructureMarkers(interimUpswings, closeType, lineWidth: 3, color: "cyan", markerSize: 6)
            .WithBreaksOfStructureMarkers(interimDownswings, closeType, lineWidth: 3, color: "cyan", markerSize: 6)

            .WithMarketStructureBreaksMarkers(interimUpswings, closeType, lineWidth: 3, color: "orange", markerSize: 6)
            .WithMarketStructureBreaksMarkers(interimDownswings, closeType, lineWidth: 3, color: "orange", markerSize: 6)

            //.WithUpswingsSawtooths(interimUpswings, closeType)
            //.WithDownswingsSawtooths(interimDownswings, closeType)
            ;

        //var downlegSawtoothPrices = new List<Price>()
        //{
        //    upswing.Prices.First()
        //};
        //foreach (var interimDownswing in interimDownswings)
        //{
        //    downlegSawtoothPrices.Add(interimDownswing.Prices.First());
        //    var interimSwingHigh = interimDownswing.SwingHigh(closeType);
        //    if (interimSwingHigh != null)
        //        downlegSawtoothPrices.Add(interimSwingHigh);
        //}
        //var swinglow = upswing.SwingLow(closeType);
        //if (swinglow != null)
        //    downlegSawtoothPrices.Add(swinglow);
        //chart = chart.WithSawtooth(downlegSawtoothPrices, closeType, "red");

        //var uplegSawtoothPrices = new List<Price>();
        //if (swinglow != null)
        //    uplegSawtoothPrices.Add(swinglow);
        //foreach (var interimUpswing in interimUpswings)
        //{
        //    uplegSawtoothPrices.Add(interimUpswing.Prices.First());
        //    var interimSwinglow = interimUpswing.SwingLow(closeType);
        //    if (interimSwinglow != null)
        //        uplegSawtoothPrices.Add(interimSwinglow);
        //}
        //var nextPrice = upswing.NextPrice;
        //if (nextPrice != null)
        //    uplegSawtoothPrices.Add(nextPrice);
        //chart = chart.WithSawtooth(uplegSawtoothPrices, closeType, "green");

        while (depth < maxDepth)
        {
            if (interimUpswings.Count + interimDownswings.Count == 0)
                depth = maxDepth;
            foreach (var interimUpswing in interimUpswings)
                chart = chart.WithInterimSwings(interimUpswing, maxDepth: maxDepth, depth: depth + 1);
            foreach (var interimDownswing in interimDownswings)
                chart = chart.WithInterimSwings(interimDownswing, maxDepth: maxDepth, depth: depth + 1);
            depth++;
        }
        return chart;
    }

    public static GenericChart WithInterimUpwardBreakouts(this GenericChart chart, Upswing upswing, int maxDepth = 0, int depth = 0)
    {
        var closeType = EnumCloseType.Close;
        var swingLow = upswing.SwingLow(closeType);

        var prices = upswing.Prices.Where(x => x.DateTime > swingLow.DateTime).ToList();

        chart = chart
            .WithUpwardBreakouts(prices.ToUpwardBreakouts(closeType), EnumCloseType.Close);

        var interminUpswings = upswing.InterimUpswings(EnumCloseType.Close, false, false);
        while (depth < maxDepth)
        {
            if (interminUpswings.Count == 0)
                depth = maxDepth;
            foreach (var interimUpswing in interminUpswings)
                chart = chart.WithInterimUpwardBreakouts(interimUpswing, maxDepth, depth + 1);
            depth++;
        }
        return chart;
    }

    public static GenericChart WithLowerHighs(this GenericChart chart, IEnumerable<Downswing> downswings, EnumCloseType closeType, string color = "green", int markerSize = 12)
    {
        return chart.AddLayers(new Layer
        {
            Name = "Lower highs",
            ChartFactory = () => ChartGenerator.GenerateScatterChart(
                downswings.Select(x => x.SwingHigh(EnumCloseType.Close)).ToList(),
                p => (decimal)p.CloseValue(closeType),
                color: Color.fromString(color),
                markerSize: markerSize,
                text: "LH"
            )
        });
    }

    public static GenericChart WithLowerLows(this GenericChart chart, IEnumerable<Downswing> downswings, EnumCloseType closeType, string color = "red", int markerSize = 12)
    {
        return chart.AddLayers(new Layer
        {
            Name = "Lower lows",
            ChartFactory = () => ChartGenerator.GenerateScatterChart(
                downswings.Select(x => x.SwingLow(EnumCloseType.Close)).ToList(),
                p => (decimal)p.CloseValue(closeType),
                color: Color.fromString(color),
                markerSize: markerSize,
                text: "LL"
            )
        });
    }

    public static GenericChart WithMarketStructureBreakReferences(this GenericChart chart, List<Downswing> downswings, EnumCloseType closeType, string color = "orange", int markerSize = 12, int lineWidth = 1)
    {
        foreach (var downswing in downswings.Where(x => x.MarketStructureBreak != null))
        {
            var p1 = downswing.PreviousDownswing.SwingHigh(EnumCloseType.Close);
            var p2 = downswing.MarketStructureBreak;
            var p = new List<Price>
            {
                p1,
                new Price
                {
                    Close = p1.Close,
                    DateTime = p2.DateTime
                }
            };

            chart = chart.AddLayers(
                ChartGenerator.PriceClosesLineLayer(p, lineWidth: 1, color: Color.fromString(color))
                );
        }

        return chart;
    }

    public static GenericChart WithMarketStructureBreaksMarkers(this GenericChart chart, List<Downswing> downswings, EnumCloseType closeType, string color = "orange", int markerSize = 12, int lineWidth = 1)
    {
        chart = chart.AddLayers(new Layer
        {
            Name = "Market Structure Breaks",
            ChartFactory = () => ChartGenerator.GenerateScatterChart(
                downswings.Where(x => x.MarketStructureBreak != null).Select(x => x.MarketStructureBreak).ToList(),
                p => (decimal)p.Close,
                color: Color.fromString(color),
                markerSize: markerSize,
                lineWidth: lineWidth
            ),
        });
        return chart;
    }

    public static GenericChart WithMarketStructureBreaksMarkers(this GenericChart chart, List<Upswing> upswings, EnumCloseType closeType, string color = "orange", int markerSize = 12, int lineWidth = 1)
    {
        chart = chart.AddLayers(new Layer
        {
            Name = "Market Structure Breaks",
            ChartFactory = () => ChartGenerator.GenerateScatterChart(
                upswings.Where(x => x.MarketStructureBreak != null).Select(x => x.MarketStructureBreak).ToList(),
                p => (decimal)p.Close,
                color: Color.fromString(color),
                markerSize: markerSize,
                lineWidth: lineWidth
            ),
        });
        return chart;
    }

    public static GenericChart WithMarketStructureBreaksReferences(this GenericChart chart, List<Upswing> upswings, EnumCloseType closeType, string color = "orange", int markerSize = 12, int lineWidth = 1)
    {
        foreach (var upswing in upswings.Where(x => x.MarketStructureBreak != null))
        {
            var p1 = upswing.PreviousUpswing.SwingLow(EnumCloseType.Close);
            var p2 = upswing.MarketStructureBreak;
            var p = new List<Price>
            {
                p1,
                new Price
                {
                    Close = p1.Close,
                    DateTime = p2.DateTime
                }
            };

            chart = chart.AddLayers(
                ChartGenerator.PriceClosesLineLayer(p, lineWidth: 1, color: Color.fromString(color))
                );
        }

        return chart;
    }

    public static GenericChart WithPartialDownswings(this GenericChart chart, List<Downswing> downswings, EnumCloseType closeType, string color = "cyan", int markerSize = 6, int lineWidth = 1)
    {
        foreach (var downswing in downswings.Where(x => x.BreakOfStructure == null))
        {
            if (downswing.NextPrice == null)
                downswing.NextPrice = downswing.Prices.LastOrDefault();

            chart = WithDownswing(chart, downswing, color, lineWidth);
        }

        return chart;
    }

    public static GenericChart WithPreviousSwingLowMarker(this GenericChart chart, Price low, DateTime extent, EnumCloseType enumCloseType,
        int markerSize = 6, int lineWidth = 1, string color = "red")
    {
        var line = new List<Price>
        {
            new Price { Close = low.CloseValue(enumCloseType), DateTime = low.DateTime },
            new Price { Close = low.CloseValue(enumCloseType), DateTime = extent }
        };

        chart = chart.AddLayers(ChartGenerator.PriceClosesLineLayer(line, lineWidth: lineWidth, color: Color.fromString(color))
            );

        return chart;
    }

    public static GenericChart WithRangeIndicators(this GenericChart chart, List<Downswing> downswings, EnumCloseType closeType, string color = "blue", int markerSize = 12, int lineWidth = 1)
    {
        //var
        //chart = chart.AddLayers(
        //    ChartGenerator.PriceClosesLineLayer(p, lineWidth: 1, color: Color.fromString(color))
        //    );

        return chart;
    }

    public static GenericChart WithRetracementLinesFromPreviousSwingLow(this GenericChart chart, List<Upswing> upswings, EnumCloseType lowCloseType, EnumCloseType highCloseType, int interimDepth = 0,
            int markerSize = 6, int lineWidth = 1)
    {
        foreach (var upswing in upswings.Where(x => x != upswings.Last()))
        {
            var nextUpswing = upswings[upswings.IndexOf(upswing) + 1];

            chart = chart.WithHorizontalLinesByGain_RainbowPrefix(upswing, nextUpswing, nextUpswing.Prices.Last().DateTime, lowCloseType, highCloseType);
        }
        return chart;
    }

    public static GenericChart WithRetracementMarker(this GenericChart chart, Upswing upswing, Upswing nextSwing, double retracement, EnumCloseType enumCloseType,
        string color = "orange", int markerSize = 6, int lineWidth = 1)
    {
        var low = upswing.SwingLow(enumCloseType);
        if (low == null)
            return chart;

        var high = nextSwing.Prices.First();
        var last = nextSwing.Prices.Last();

        var highLowDelta = high.CloseValue(enumCloseType) - low.CloseValue(enumCloseType);
        var retracementDelta = highLowDelta * retracement;

        var bottomY = low.CloseValue(enumCloseType);
        var topY = high.CloseValue(enumCloseType) - retracementDelta;
        var leftX = low.DateTime;
        var rightX = last.DateTime;

        var topLine = new List<Price>
        {
            new Price { Close = topY, DateTime = high.DateTime },
            new Price { Close = topY, DateTime = rightX }
        };

        var bottomLine = new List<Price>
        {
            new Price { Close = bottomY, DateTime = leftX },
            new Price { Close = bottomY, DateTime = high.DateTime }
        };

        var verticalLine = new List<Price>
        {
            new Price { Close = bottomY, DateTime = high.DateTime },
            new Price { Close = topY, DateTime = high.DateTime }
        };

        chart = chart.AddLayers(
            ChartGenerator.PriceClosesLineLayer(topLine, lineWidth: lineWidth, color: Color.fromString(color)),
            ChartGenerator.PriceClosesLineLayer(bottomLine, lineWidth: lineWidth, color: Color.fromString(color)),
            ChartGenerator.PriceClosesLineLayer(verticalLine, lineWidth: lineWidth, color: Color.fromString(color))
        );

        return chart;
    }

    public static GenericChart WithRetracementMarkers(this GenericChart chart, List<Upswing> upswings, double retracement, EnumCloseType enumCloseType,
            string color = "orange", int markerSize = 6, int lineWidth = 1)
    {
        foreach (var upswing in upswings.Where(x => x != upswings.Last()))
        {
            var low = upswing.SwingLow(enumCloseType);
            if (low == null)
                continue;

            var nextUpswing = upswings[upswings.IndexOf(upswing) + 1];
            var high = nextUpswing.Prices.First();
            var gains = new double[] { -0.25, 0.0, 0.25, 0.5, 0.75, 1.0, 1.25 };

            chart = chart.WithHorizontalLinesByGain(low, high, nextUpswing.Prices.Last().DateTime, EnumCloseType.Low, EnumCloseType.High, gains: gains);
        }
        return chart;
    }

    public static GenericChart WithSawtooth(this GenericChart chart, List<Price> sawtooth, EnumCloseType closeType, string color = "black", int markerSize = 12, int lineWidth = 1)
    {
        chart = chart.AddLayers(
            ChartGenerator.PriceClosesLineLayer(sawtooth, lineWidth: lineWidth, color: Color.fromString(color))
            );
        return chart;
    }

    public static GenericChart WithUnfinishedUpswings(this GenericChart chart, List<Upswing> upswings, EnumCloseType closeType, string color = "cyan", int markerSize = 6, int lineWidth = 1)
    {
        foreach (var upswing in upswings.Where(x => x.BreakOfStructure == null))
        {
            if (upswing.NextPrice == null)
                upswing.NextPrice = upswing.Prices.LastOrDefault();

            chart = WithUpswing(chart, upswing, color, lineWidth);
        }

        return chart;
    }

    public static GenericChart WithUpswing(this GenericChart chart, Upswing upswing, string color = "cyan", int lineWidth = 1)
    {
        var p1 = upswing.Prices.First();
        var p2 = upswing.NextPrice;

        if (p1 == null || p2 == null)
            return chart;

        var swingLow = upswing.SwingLow(EnumCloseType.Close);

        if (swingLow == null) return chart;

        var topY = p1.Close;
        var bottomY = swingLow.Close;
        var leftX = p1.DateTime;
        var rightX = p2.DateTime;

        var topLine = new List<Price>
        {
            new Price { Close = topY, DateTime = leftX },
            new Price { Close = topY, DateTime = rightX }
        };

        var bottomLine = new List<Price>
        {
            new Price { Close = bottomY, DateTime = leftX },
            new Price { Close = bottomY, DateTime = rightX }
        };

        var leftLine = new List<Price>
        {
            new Price { Close = topY, DateTime = leftX },
            new Price { Close = bottomY, DateTime = leftX }
        };

        var rightLine = new List<Price>
        {
            new Price { Close = topY, DateTime = rightX },
            new Price { Close = bottomY, DateTime = rightX }
        };

        chart = chart.AddLayers(
            ChartGenerator.PriceClosesLineLayer(topLine, lineWidth: lineWidth, color: Color.fromString(color)),
            ChartGenerator.PriceClosesLineLayer(bottomLine, lineWidth: lineWidth, color: Color.fromString(color)),
            ChartGenerator.PriceClosesLineLayer(leftLine, lineWidth: lineWidth, color: Color.fromString(color)),
            ChartGenerator.PriceClosesLineLayer(rightLine, lineWidth: lineWidth, color: Color.fromString(color))
        );

        return chart;
    }

    public static GenericChart WithUpswings(this GenericChart chart, List<Upswing> upswings, EnumCloseType closeType,
        string color = "cyan", int markerSize = 6, int lineWidth = 1)
    {
        foreach (var upswing in upswings)
        {
            chart = WithUpswing(chart, upswing, color, lineWidth);
        }

        return chart;
    }

    public static GenericChart WithUpswingSawtooths(this GenericChart chart, Upswing upswing, EnumCloseType closeType,
        string color = "cyan", int markerSize = 6, int lineWidth = 1)
    {
        var downSawtooth = upswing.DownlegPrices(closeType, true, true).ToDownwardSawtooth(closeType, true);
        chart = chart.WithSawtooth(downSawtooth, closeType, color: "red");
        var upwardSawtooth = upswing.UplegPrices(closeType, true, true).ToUpwardSawtooth(closeType, true);
        chart = chart.WithSawtooth(upwardSawtooth, closeType, color: "green");

        return chart;
    }

    public static GenericChart WithUpswingsSawtooths(this GenericChart chart, List<Upswing> upswings, EnumCloseType closeType,
        string color = "cyan", int markerSize = 6, int lineWidth = 1)
    {
        foreach (var upswing in upswings)
            chart = chart.WithUpswingSawtooths(upswing, closeType, color, markerSize, lineWidth);
        return chart;
    }

    public static GenericChart WithUpwardBreakout(this GenericChart chart, UpwardBreakout upwardBreakout,
        string confirmationColor = "yellow", string successfulBreakout = "green", string failedBreakout = "red", int lineWidth = 3
        )
    {
        if (upwardBreakout.Breakout == null)
            return chart;

        if (upwardBreakout.Confirmation == null)
            return chart;

        var confirmation = new List<Price> { upwardBreakout.Breakout, upwardBreakout.Confirmation };

        chart = chart.AddLayers(
            ChartGenerator.PriceClosesLineLayer(confirmation, lineWidth: lineWidth, color: Color.fromString(confirmationColor)),
            ChartGenerator.PriceClosesLineLayer(upwardBreakout.SuccessfulBreakout, lineWidth: lineWidth, color: Color.fromString(successfulBreakout)),
            ChartGenerator.PriceClosesLineLayer(upwardBreakout.FailedBreakout, lineWidth: lineWidth, color: Color.fromString(failedBreakout))
            );
        return chart;
    }

    public static GenericChart WithUpwardBreakouts(this GenericChart chart, List<UpwardBreakout> upwardBreakouts, EnumCloseType closeType,
        string confirmationColor = "yellow", string successfulBreakout = "green", string failedBreakout = "red", int lineWidth = 3)
    {
        foreach (var upwardBreakout in upwardBreakouts)
        {
            chart = chart.WithUpwardBreakout(upwardBreakout, confirmationColor, successfulBreakout, failedBreakout, lineWidth);
        }

        return chart;
    }
}