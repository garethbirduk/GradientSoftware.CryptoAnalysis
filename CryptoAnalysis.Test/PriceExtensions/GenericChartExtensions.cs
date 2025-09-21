using Plotly.NET;

namespace Gradient.CryptoAnalysis.Test.PriceExtensions;

public static class GenericChartExtensions
{
    private static GenericChart WithBreakOfStructure(this GenericChart chart, Upswing upswing, string color = "green", int lineWidth = 1)
    {
        var p1 = upswing.Prices.First();
        var p2 = upswing.NextPrice;
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

    public static GenericChart WithUpswings(this GenericChart chart, List<Upswing> upswings, EnumCloseType closeType, string color = "cyan", int markerSize = 6, int lineWidth = 1)
    {
        foreach (var upswing in upswings)
        {
            chart = WithUpswing(chart, upswing, color, lineWidth);
        }

        return chart;
    }
}