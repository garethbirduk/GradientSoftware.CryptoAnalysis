using Gradient.CryptoAnalysis;
using Microsoft.FSharp.Core;
using Plotly.NET;
using Plotly.NET.TraceObjects;

public static class ChartGenerator
{
    private static GenericChart ApplyStyle(GenericChart chart, Color? color, double? lineWidth)
    {
        if (color == null && !lineWidth.HasValue) return chart;

        var line = Line.init(
            Color: color,
            Width: lineWidth.HasValue ? FSharpOption<double>.Some(lineWidth.Value) : FSharpOption<double>.None
        );
        return chart.WithLine(line);
    }

    private static GenericChart CreateAnnotationPoint(DateTime x, double y, string note, Color? color = null, double? size = null)
    {
        var point = Chart2D.Chart.Point<DateTime, double, string>(
            x: new[] { x },
            y: new[] { y },
            Text: FSharpOption<string>.Some(note)
        );

        return ApplyStyle(point, color, size);
    }

    private static Layer CreatePriceLineLayer(
        List<Price> prices,
        Func<Price, decimal> selector,
        string name,
        Color? color,
        int lineWidth)
    {
        return new Layer
        {
            Name = name,
            ChartFactory = () => GenerateLineChart(
                prices,
                xSelector: p => p.DateTime,
                ySelector: selector,
                lineWidth: lineWidth,
                title: name,
                color: color
            ),
            Color = color,
            LineWidth = lineWidth
        };
    }

    public static GenericChart AddLayers(this GenericChart baseChart, params Layer[] layers)
    {
        var layerCharts = layers.Select(l => ApplyStyle(l.ChartFactory(), l.Color, l.LineWidth));
        return Chart.Combine(new[] { baseChart }.Concat(layerCharts));
    }

    public static Layer CreateAnnotationLayer(
        List<AnnotatedPrice> prices,
        StyleParam.MarkerSymbol symbol,
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
                    Symbol: symbol
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

        var charts = layers.Select(l => ApplyStyle(l.ChartFactory(), l.Color, l.LineWidth));
        return Chart.Combine(charts);
    }

    public static GenericChart CreatePriceChart(List<Price> prices,
        bool candlestick = false,
        bool lineCloses = false,
        bool lineHighs = false,
        bool lineLows = false,
        int lineWidth = 1,
        string name = "prices")
    {
        var layers = new List<Layer>();
        if (candlestick)
            layers.Add(GenerateCandlestickLayer(prices, lineWidth: lineWidth, name: name));
        if (lineCloses)
            layers.Add(PriceClosesLineLayer(prices, lineWidth: lineWidth, name: name));
        if (lineHighs)
            layers.Add(PriceHighsLineLayer(prices, lineWidth: lineWidth, name: name));
        if (lineLows)
            layers.Add(PriceLowsLineLayer(prices, lineWidth: lineWidth, name: name));

        var layout = Layout.init<string>(
            Width: FSharpOption<int>.Some(1900),
            Height: FSharpOption<int>.Some(1168),
            AutoSize: FSharpOption<bool>.Some(false)
        );

        var config = Config.init(Responsive: false);

        var baseChart = CreateChart();
        baseChart = ApplyStyle(baseChart, null, null);

        return baseChart
            .WithLayout(layout)
            .WithConfig(config)
            .AddLayers(layers.ToArray());
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

                annotationCharts.Add(CreateAnnotationPoint(p.DateTime, y, a.Note));
            }
        }

        var combined = new[] { chart }.Concat(annotationCharts).ToArray();
        var fullChart = Chart.Combine(combined);

        return fullChart
            .WithTitle("Crypto Candlestick with Annotations")
            .WithXAxisStyle(title: Title.init("Date"))
            .WithYAxisStyle(title: Title.init("Price"));
    }

    public static GenericChart GenerateCandlestickChart(
        List<Price> prices,
        string title = "Crypto Candlestick",
        Color? color = null,
        double? lineWidth = null)
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

        chart = ApplyStyle(chart, color, lineWidth);

        return chart
            .WithTitle(title)
            .WithXAxisStyle(title: Title.init("Date"))
            .WithYAxisStyle(title: Title.init("Price"));
    }

    public static Layer GenerateCandlestickLayer(
        List<Price> prices,
        string name = "prices",
        Color? color = null,
        double? lineWidth = null)
    {
        return new Layer
        {
            Name = name,
            ChartFactory = () => GenerateCandlestickChart(prices, name, color, lineWidth),
            Color = color,
            LineWidth = lineWidth
        };
    }

    public static GenericChart GenerateLineChart<T>(
        IEnumerable<T> data,
        Func<T, DateTime> xSelector,
        Func<T, decimal> ySelector,
        int lineWidth = 1,
        string title = "Line Chart",
        Color? color = null
    )
    {
        var xData = data.Select(xSelector);
        var yData = data.Select(ySelector);

        var chart = Chart2D.Chart.Line<DateTime, decimal, string>(
            x: xData,
            y: yData
        );

        chart = ApplyStyle(chart, color, lineWidth);

        return chart
            .WithTitle(title)
            .WithXAxisStyle(title: Title.init("Date"))
            .WithYAxisStyle(title: Title.init("Value"));
    }

    public static GenericChart GenerateLineChart<T>(
        List<T> prices,
        Func<T, decimal> ySelector,
        string title = "Line Chart",
        Color? color = null,
        int? size = null
        ) where T : Price
    {
        var xData = prices.Select(x => x.DateTime);
        var yData = prices.Select(ySelector);

        var chart = Chart2D.Chart.Line<DateTime, decimal, string>(
            x: xData,
            y: yData,
            LineColor: color
        );

        return chart
            .WithTitle(title)
            .WithXAxisStyle(title: Title.init("Date"))
            .WithYAxisStyle(title: Title.init("Value"));
    }

    public static GenericChart GenerateScatterChart<T>(
        IEnumerable<T> prices,
        Func<T, decimal> ySelector,
        string title = "Scatter Chart",
        Color? color = null,
        double? lineWidth = null
    ) where T : Price
    {
        var xData = prices.Select(x => x.DateTime);
        var yData = prices.Select(ySelector);

        var chart = Chart2D.Chart.Point<DateTime, decimal, string>(
            x: xData,
            y: yData
        );

        chart = ApplyStyle(chart, color, lineWidth);

        return chart
            .WithTitle(title)
            .WithXAxisStyle(title: Title.init("Date"))
            .WithYAxisStyle(title: Title.init("Value"));
    }

    public static Layer PriceClosesLineLayer(List<Price> prices, Color? color = null, int lineWidth = 1, string name = "Close Prices") =>
        CreatePriceLineLayer(prices, p => (decimal)p.Close, name, color ?? Color.fromString("Black"), lineWidth);

    public static Layer PriceHighsLineLayer(List<Price> prices, Color? color = null, int lineWidth = 1, string name = "High Prices") =>
        CreatePriceLineLayer(prices, p => (decimal)p.High, name, color ?? Color.fromString("Black"), lineWidth);

    public static Layer PriceLowsLineLayer(List<Price> prices, Color? color = null, int lineWidth = 1, string name = "Low Prices") =>
            CreatePriceLineLayer(prices, p => (decimal)p.Low, name, color ?? Color.fromString("Black"), lineWidth);

    public static Layer PriceOpenLine(List<Price> prices) =>
        CreatePriceLineLayer(prices, p => (decimal)p.Open, "Open Prices", Color.fromString("blue"), 3);

    public static void Save(this GenericChart chart, string outputPath, string format = "html")
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        switch (format.ToLowerInvariant())
        {
            case "html":
                chart.SaveHtml(outputPath);
                break;
            // case "png":
            //     chart.SaveImage(outputPath); // Uncomment if Plotly.NET supports it
            //     break;
            default:
                throw new NotSupportedException($"Format '{format}' is not supported.");
        }
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