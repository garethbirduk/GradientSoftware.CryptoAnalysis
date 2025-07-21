using Gradient.CryptoAnalysis;
using Microsoft.FSharp.Core;
using Plotly.NET;
using Plotly.NET.TraceObjects;

public static class ChartGenerator
{
    public static GenericChart AddLayers(this GenericChart baseChart, params Layer[] layers)
    {
        var layerCharts = layers.Select(l =>
        {
            var chart = l.ChartFactory();
            if (l.Color != null || l.LineWidth != null)
            {
                var line = Line.init(
                    Color: l.Color,
                    Width: l.LineWidth.HasValue ? FSharpOption<double>.Some(l.LineWidth.Value) : FSharpOption<double>.None
                );
                chart = chart.WithLine(line);
            }
            return chart;
        });

        return Chart.Combine(new[] { baseChart }.Concat(layerCharts));
    }

    public static Layer CreateAnnotationLayer(
    List<AnnotatedPrice> prices,
    Color? color = null,
    int? size = null
)
    {
        if (!prices.Any()) return new Layer();

        var annotation = prices.First().Annotations.First();
        var type = annotation.AnnotationType;

        var points = prices.Select(p =>
        {
            var a = p.Annotations.First();
            double y = a.Position switch
            {
                EnumPosition.Above => p.Close + 0.5,
                EnumPosition.Below => p.Close - 0.5,
                EnumPosition.Precise => p.Close,
                _ => p.Close
            };
            return (x: p.DateTime, y, text: a.Note);
        }).ToList();

        return new Layer
        {
            Name = type.ToString(),
            ChartFactory = () =>
                Chart2D.Chart.Point<DateTime, double, string>(
                    x: points.Select(p => p.x),
                    y: points.Select(p => p.y),
                    MultiText: FSharpOption<IEnumerable<string>>.Some(points.Select(p => p.text)),
                    MultiTextPosition: FSharpOption<IEnumerable<StyleParam.TextPosition>>.Some(
                        Enumerable.Repeat(StyleParam.TextPosition.TopCenter, points.Count)
                    )
                )
                .WithMarker(Marker.init(
                    Color: color,
                    Size: size.HasValue ? FSharpOption<int>.Some(size.Value) : FSharpOption<int>.None,
                    Symbol: StyleParam.MarkerSymbol.ArrowUp
                )),
            Color = color
        };
    }

    public static GenericChart CreateChart(params Layer[] layers)
    {
        if (layers == null || layers.Length == 0)
        {
            return Chart2D.Chart.Point<double, double, string>(
                x: new double[] { },
                y: new double[] { }
            );
        }

        var charts = layers.Select(l =>
        {
            var chart = l.ChartFactory();
            if (l.Color != null || l.LineWidth != null)
            {
                var line = Line.init(
                    Color: l.Color,
                    Width: l.LineWidth.HasValue ? FSharpOption<double>.Some(l.LineWidth.Value) : FSharpOption<double>.None
                );
                chart = chart.WithLine(line);
            }
            return chart;
        });

        return Chart.Combine(charts);
    }

    public static GenericChart CreatePriceChart(List<Price> prices)
    {
        return ChartGenerator.CreateChart()
            .AddLayers(new Layer
            {
                Name = "base",
                ChartFactory = () => ChartGenerator.GenerateLineChart(
                    prices,
                    p => (decimal)p.Close,
                    "Close Prices"
                ),
                Color = Color.fromString("blue"),
                LineWidth = 5,
            });
    }

    public static GenericChart GenerateAnnotatedCandlestickChart(List<AnnotatedPrice> prices)
    {
        var openData = prices.Select(p => (decimal)p.Open);
        var highData = prices.Select(p => (decimal)p.High);
        var lowData = prices.Select(p => (decimal)p.Low);
        var closeData = prices.Select(p => (decimal)p.Close);
        var dateData = prices.Select(p => p.DateTime);

        var chart = Chart2D.Chart.Candlestick<decimal, decimal, decimal, decimal, DateTime, string>(
            open: openData,
            high: highData,
            low: lowData,
            close: closeData,
            X: FSharpOption<IEnumerable<DateTime>>.Some(dateData)
        );

        var annotationCharts = new List<GenericChart>();

        foreach (var p in prices)
        {
            if (p.Annotations == null || p.Annotations.Count == 0) continue;

            foreach (var a in p.Annotations)
            {
                double y = a.Position switch
                {
                    EnumPosition.Above => p.High + 0.5,
                    EnumPosition.Below => p.Low - 0.5,
                    EnumPosition.Precise => p.Close,
                    _ => p.Close
                };

                var point = Chart2D.Chart.Point<DateTime, double, string>(
                    x: new[] { p.DateTime },
                    y: new[] { y },
                    Text: FSharpOption<string>.Some(a.Note)
                );

                annotationCharts.Add(point);
            }
        }

        var combined = new[] { chart }.Concat(annotationCharts).ToArray();
        var fullChart = Chart.Combine(combined);

        return fullChart
            .WithTitle("Crypto Candlestick with Annotations")
            .WithXAxisStyle(title: Title.init("Date"))
            .WithYAxisStyle(title: Title.init("Price"));
    }

    public static GenericChart GenerateCandlestickChart(List<Price> prices)
    {
        var openData = prices.Select(p => (decimal)p.Open);
        var highData = prices.Select(p => (decimal)p.High);
        var lowData = prices.Select(p => (decimal)p.Low);
        var closeData = prices.Select(p => (decimal)p.Close);
        var dateData = prices.Select(p => p.DateTime);

        var chart = Chart2D.Chart.Candlestick<decimal, decimal, decimal, decimal, DateTime, string>(
            open: openData,
            high: highData,
            low: lowData,
            close: closeData,
            X: FSharpOption<IEnumerable<DateTime>>.Some(dateData)
        );

        return chart
            .WithTitle("Crypto Candlestick")
            .WithXAxisStyle(title: Title.init("Date"))
            .WithYAxisStyle(title: Title.init("Price"));
    }

    public static GenericChart GenerateLineChart<T>(
        List<T> data,
        Func<T, DateTime> xSelector,
        Func<T, decimal> ySelector,
        string title = "Line Chart"
    )
    {
        var xData = data.Select(xSelector);
        var yData = data.Select(ySelector);

        var chart = Chart2D.Chart.Line<DateTime, decimal, string>(
            x: xData,
            y: yData
        );

        return chart
            .WithTitle(title)
            .WithXAxisStyle(title: Title.init("Date"))
            .WithYAxisStyle(title: Title.init("Value"));
    }

    public static GenericChart GenerateLineChart<T>(
        List<T> prices,
        Func<T, decimal> ySelector,
        string title = "Line Chart"
    ) where T : Price
    {
        var xData = prices.Select(x => x.DateTime);
        var yData = prices.Select(ySelector);

        var chart = Chart2D.Chart.Line<DateTime, decimal, string>(
            x: xData,
            y: yData
        );

        return chart
            .WithTitle(title)
            .WithXAxisStyle(title: Title.init("Date"))
            .WithYAxisStyle(title: Title.init("Value"));
    }

    public static void Save(this GenericChart chart, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        chart.SaveHtml(outputPath);
    }
}

public class Layer
{
    public Func<GenericChart> ChartFactory { get; init; } = () => Chart2D.Chart.Point<double, double, string>(
        x: new double[] { },
        y: new double[] { }
    );

    public Color? Color { get; init; }

    public double? LineWidth { get; init; } = 1;
    public string Name { get; init; } = "";
}