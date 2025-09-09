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

    private static GenericChart WithDownswing(this GenericChart chart, Downswing downswing, string color = "cyan", int lineWidth = 1)
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

    private static GenericChart WithUpswing(this GenericChart chart, Upswing upswing, string color = "cyan", int lineWidth = 1)
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

    public static GenericChart WithDownswings(this GenericChart chart, List<Downswing> downswings, EnumCloseType closeType, string color = "cyan", int markerSize = 6, int lineWidth = 1)
    {
        foreach (var downswing in downswings.Where(x => x.BreakOfStructure != null))
        {
            chart = WithDownswing(chart, downswing, color, lineWidth);
        }

        return chart;
    }

    public static GenericChart WithHigherHighs(this GenericChart chart, List<Upswing> upswings, EnumCloseType closeType, string color = "green", int markerSize = 12)
    {
        return chart.AddLayers(new Layer
        {
            Name = "Higher highs",
            ChartFactory = () => ChartGenerator.GenerateScatterChart(
                upswings.Select(x => x.Prices.First()).ToList(),
                p => (decimal)p.CloseValue(closeType),
                color: Color.fromString(color),
                markerSize: markerSize
            ),
        });
    }

    public static GenericChart WithHigherLows(this GenericChart chart, List<Upswing> upswings, EnumCloseType closeType, string color = "red", int markerSize = 12)
    {
        return chart.AddLayers(new Layer
        {
            Name = "Higher lows",
            ChartFactory = () => ChartGenerator.GenerateScatterChart(
                upswings.Select(x => x.SwingLow(EnumCloseType.Close)).ToList(),
                p => (decimal)p.CloseValue(closeType),
                color: Color.fromString(color),
                markerSize: markerSize
            )
        });
    }

    public static GenericChart WithLowerHighs(this GenericChart chart, List<Downswing> upswings, EnumCloseType closeType, string color = "green", int markerSize = 12)
    {
        return chart.AddLayers(new Layer
        {
            Name = "Lower highs",
            ChartFactory = () => ChartGenerator.GenerateScatterChart(
                upswings.Select(x => x.SwingHigh(EnumCloseType.Close)).ToList(),
                p => (decimal)p.CloseValue(closeType),
                color: Color.fromString(color),
                markerSize: markerSize
            )
        });
    }

    public static GenericChart WithLowerLows(this GenericChart chart, List<Downswing> downswings, EnumCloseType closeType, string color = "red", int markerSize = 12)
    {
        return chart.AddLayers(new Layer
        {
            Name = "Lower lows",
            ChartFactory = () => ChartGenerator.GenerateScatterChart(
                downswings.Select(x => x.Prices.First()).ToList(),
                p => (decimal)p.CloseValue(closeType),
                color: Color.fromString(color),
                markerSize: markerSize
            ),
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

    public static GenericChart WithUpswings(this GenericChart chart, List<Upswing> upswings, EnumCloseType closeType, string color = "cyan", int markerSize = 6, int lineWidth = 1)
    {
        foreach (var upswing in upswings.Where(x => x.BreakOfStructure != null))
        {
            chart = WithUpswing(chart, upswing, color, lineWidth);
        }

        return chart;
    }
}