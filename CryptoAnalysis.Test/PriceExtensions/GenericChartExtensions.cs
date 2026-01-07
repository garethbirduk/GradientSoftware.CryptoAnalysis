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

    public static GenericChart WithBreaksOfStructure(this GenericChart chart, List<Upswing> upswings, EnumCloseType closeType, string color = "cyan", int markerSize = 12, int lineWidth = 1, BreakOfStructureFormat breakOfStructureFormat = BreakOfStructureFormat.Box)
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

        foreach (var upswing in upswings.Where(x => x.BreakOfStructure != null))
        {
            chart = WithBreakOfStructure(chart, upswing, color, lineWidth);
        }

        return chart;
    }

    public static GenericChart WithBreaksOfStructure(this GenericChart chart, List<Downswing> downswings, EnumCloseType closeType, string color = "cyan", int markerSize = 12, int lineWidth = 1, BreakOfStructureFormat breakOfStructureFormat = BreakOfStructureFormat.Box)
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

        foreach (var downswing in downswings.Where(x => x.BreakOfStructure != null))
        {
            chart = WithBreakOfStructure(chart, downswing, color, lineWidth);
        }

        return chart;
    }

    public static GenericChart WithDownswing(this GenericChart chart, Downswing? downswing, string color = "cyan", int lineWidth = 1)
    {
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

    public static GenericChart WithInterimDownswings(this GenericChart chart, Downswing downswings, int maxDepth = 0, int depth = 0)
    {
        var interimDownswings = downswings.InterimDownswings(EnumCloseType.Close, skip: 0);
        var interminUpswings = downswings.InterimUpswings(EnumCloseType.Close, skip: 0);

        chart = chart
            .WithLowerHighs(interimDownswings, EnumCloseType.Close, markerSize: 6, color: "green")
            .WithLowerLows(interimDownswings, EnumCloseType.Close, markerSize: 6, color: "red")

            .WithHigherHighs(interminUpswings, EnumCloseType.Close, markerSize: 6, color: "green")
            .WithHigherLows(interminUpswings, EnumCloseType.Close, markerSize: 6, color: "red")

            .WithUpswings(interminUpswings, EnumCloseType.Close, markerSize: 6, color: "green")
            .WithDownswings(interimDownswings, EnumCloseType.Close, markerSize: 6, color: "red")

            .WithBreaksOfStructure(interimDownswings, EnumCloseType.Close, lineWidth: 3, color: "cyan", markerSize: 6)
            .WithMarketStructureBreaks(interimDownswings, EnumCloseType.Close, lineWidth: 3, color: "orange", markerSize: 6)
            ;

        while (depth < maxDepth)
        {
            if (interminUpswings.Count + interimDownswings.Count == 0)
                depth = maxDepth;
            foreach (var interimUpswing in interminUpswings)
                chart = chart.WithInterimUpswings(interimUpswing, maxDepth, depth + 1);
            foreach (var interimDownswing in interimDownswings)
                chart = chart.WithInterimDownswings(interimDownswing, maxDepth, depth + 1);
            depth++;
        }
        return chart;
    }

    public static GenericChart WithInterimUpswings(this GenericChart chart, Upswing upswing, int maxDepth = 0, int depth = 0)
    {
        var interimDownswings = upswing.InterimDownswings(EnumCloseType.Close, skip: 0);
        var interminUpswings = upswing.InterimUpswings(EnumCloseType.Close, skip: 0);

        chart = chart
            .WithLowerHighs(interimDownswings, EnumCloseType.Close, markerSize: 6, color: "green")
            .WithLowerLows(interimDownswings, EnumCloseType.Close, markerSize: 6, color: "red")

            .WithHigherHighs(interminUpswings, EnumCloseType.Close, markerSize: 6, color: "green")
            .WithHigherLows(interminUpswings, EnumCloseType.Close, markerSize: 6, color: "red")

            .WithDownswings(interimDownswings, EnumCloseType.Close, markerSize: 6, color: "red")
            .WithUpswings(interminUpswings, EnumCloseType.Close, markerSize: 6, color: "green")

            .WithBreaksOfStructure(interminUpswings, EnumCloseType.Close, lineWidth: 3, color: "cyan", markerSize: 6)
            .WithMarketStructureBreaks(interminUpswings, EnumCloseType.Close, lineWidth: 3, color: "orange", markerSize: 6)

            ;

        while (depth < maxDepth)
        {
            if (interminUpswings.Count + interimDownswings.Count == 0)
                depth = maxDepth;
            foreach (var interimUpswing in interminUpswings)
                chart = chart.WithInterimUpswings(interimUpswing, maxDepth, depth + 1);
            foreach (var interimDownswing in interimDownswings)
                chart = chart.WithInterimDownswings(interimDownswing, maxDepth, depth + 1);
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

        var interminUpswings = upswing.InterimUpswings(EnumCloseType.Close, skip: 0);
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

    public static GenericChart WithMarketStructureBreaks(this GenericChart chart, List<Upswing> upswings, EnumCloseType closeType, string color = "orange", int markerSize = 12, int lineWidth = 1)
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

    public static GenericChart WithMarketStructureBreaks(this GenericChart chart, List<Downswing> downswings, EnumCloseType closeType, string color = "orange", int markerSize = 12, int lineWidth = 1)
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