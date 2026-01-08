using Gradient.CryptoAnalysis;
using Microsoft.FSharp.Core;
using Plotly.NET;
using Plotly.NET.LayoutObjects;
using Plotly.NET.TraceObjects;
using static Plotly.NET.StyleParam;

public static class ChartGenerator
{
    private static GenericChart ApplyStyle(this GenericChart chart, Color? color, int? lineWidth)
    {
        return chart.ApplyStyle(color, lineWidth.HasValue ? (double?)lineWidth.Value : null);
    }

    private static GenericChart ApplyStyle(this GenericChart chart, Color? color, double? lineWidth)
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

    public static GenericChart AddLayers(this GenericChart baseChart, params Layer[] layers)
    {
        var layerCharts = layers.Select(l => ApplyStyle(l.ChartFactory(), l.Color, l.LineWidth));
        return Chart.Combine(new[] { baseChart }.Concat(layerCharts));
    }

    public static Layer CreateAnnotationLayer(
        List<AnnotatedPrice> prices,
        StyleParam.MarkerSymbol? symbol = null,
        Color? color = null,
        int? size = null
    )
    {
        if (!prices.Any() || prices.All(p => p.Annotations == null || p.Annotations.Count == 0))
            return new Layer();

        var first = prices.First(p => p.Annotations != null && p.Annotations.Count > 0);
        var type = first.Annotations[0].AnnotationType;
        var markerSymbol = symbol ?? StyleParam.MarkerSymbol.Circle;
        var s = size ?? 10;
        var markerOffset = s * 1.0;
        var labelOffset = s * 2.0;

        var points = prices
            .Where(p => p.Annotations != null && p.Annotations.Count > 0)
            .Select(p =>
            {
                var a = p.Annotations[0];
                var baseY = a.Position switch
                {
                    EnumPosition.Above => p.Close,
                    EnumPosition.Below => p.Close,
                    EnumPosition.Precise => p.Close,
                    _ => p.Close
                };

                return (x: p.DateTime, y: baseY + markerOffset, labelY: baseY + labelOffset, text: a.Note);
            })
            .ToList();

        return new Layer
        {
            Name = type.ToString(),
            ChartFactory = () =>
                Chart2D.Chart.Point<DateTime, double, string>(
                    x: points.Select(p => p.x),
                    y: points.Select(p => p.labelY),
                    MultiText: FSharpOption<IEnumerable<string>>.Some(points.Select(p => p.text)),
                    MultiTextPosition: FSharpOption<IEnumerable<StyleParam.TextPosition>>.Some(
                        Enumerable.Repeat(StyleParam.TextPosition.TopCenter, points.Count)
                    )
                )
                .WithMarker(Marker.init(
                    Color: color,
                    Size: FSharpOption<int>.Some(s),
                    Symbol: markerSymbol
                )),
            Color = color,
            LineWidth = size
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

    public static GenericChart CreatePriceChart(
        List<Price> prices,
        bool candlestick = false,
        bool lineCloses = false,
        bool lineHighs = false,
        bool lineLows = false,
        int lineWidth = 1,
        string name = "prices",
        Color? color = null
    )
    {
        var layers = new List<Layer>();
        if (candlestick)
            layers.Add(PricesCandlestickLayer(prices, lineWidth: lineWidth, name: $"{name} - Candlestick"));
        if (lineCloses)
            layers.Add(PriceClosesLineLayer(prices, lineWidth: lineWidth, name: $"{name} - Close", color: color));
        if (lineHighs)
            layers.Add(PriceHighsLineLayer(prices, lineWidth: lineWidth, name: $"{name} - High", color: color));
        if (lineLows)
            layers.Add(PriceLowsLineLayer(prices, lineWidth: lineWidth, name: $"{name} - Low", color: color));

        var baseChart = CreateChart()
            .AddLayers(layers.ToArray());

        return baseChart;
    }

    public static Layer CreatePriceLineLayer(
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

        foreach (var p in prices.Where(p => p.Annotations != null && p.Annotations.Count > 0))
        {
            foreach (var a in p.Annotations!)
            {
                double y = a.Position switch
                {
                    EnumPosition.Above => p.High + 0.5,
                    EnumPosition.Below => p.Low - 0.5,
                    EnumPosition.Precise => p.Close,
                    _ => p.Close
                };

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

        var yAxis = new LinearAxis();
        yAxis.SetValue("autorange", AutoRange.True);
        yAxis.SetValue("fixedrange", false);

        var chart = Chart2D.Chart.Candlestick<decimal, decimal, decimal, decimal, DateTime, string>(
            open: openData,
            high: highData,
            low: lowData,
            close: closeData,
            X: FSharpOption<IEnumerable<DateTime>>.Some(dateData),
            ShowXAxisRangeSlider: FSharpOption<bool>.Some(true),
            UseDefaults: FSharpOption<bool>.Some(true)
        ).WithYAxis(yAxis);

        chart = ApplyStyle(chart, color, lineWidth);

        return chart
            .WithTitle(title)
            .WithXAxisStyle(title: Title.init("Date"))
            .WithYAxisStyle(title: Title.init("Price"));
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
        int? lineWidth = null
    ) where T : Price
    {
        return GenerateLineChart(
            prices,
            xSelector: x => x.DateTime,
            ySelector: ySelector,
            lineWidth: lineWidth ?? 1,
            title: title,
            color: color
        );
    }

    public static GenericChart GenerateScatterChart<T>(
        IEnumerable<T> prices,
        Func<T, decimal> ySelector,
        string title = "Scatter Chart",
        Color? color = null,
        double? lineWidth = null,
        int? markerSize = null,
        string? text = null
    ) where T : Price
    {
        var xData = prices.Select(x => x.DateTime);
        var yData = prices.Select(ySelector);

        GenericChart chart;

        if (!string.IsNullOrEmpty(text))
        {
            chart = Chart2D.Chart.Point<DateTime, decimal, string>(
                x: xData,
                y: yData,
                Text: FSharpOption<string>.Some(text)
            );
        }
        else
        {
            chart = Chart2D.Chart.Point<DateTime, decimal, string>(
                x: xData,
                y: yData
            );
        }

        chart = chart.WithMarker(Marker.init(
            Color: color,
            Size: markerSize.HasValue ? FSharpOption<int>.Some(markerSize.Value) : FSharpOption<int>.None
        ));

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

    public static Layer PricesCandlestickLayer(List<Price> prices, string name = "prices", Color? color = null, int? lineWidth = null)
    {
        return new Layer
        {
            Name = name,
            ChartFactory = () => GenerateCandlestickChart(prices, name, color, lineWidth),
            Color = color,
            LineWidth = lineWidth
        };
    }

    public static void Save(this GenericChart chart, string outputPath, string format = "html")
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var layout = Layout.init<string>(
            DragMode: StyleParam.DragMode.Zoom,
            Width: FSharpOption<int>.Some(1800),
            Height: FSharpOption<int>.Some(900),
            AutoSize: FSharpOption<bool>.Some(false)
        );

        var config = Config.init(
            ScrollZoom: StyleParam.ScrollZoom.NoZoom,
            Responsive: true,
            DisplayModeBar: true,
            Displaylogo: false
        );

        chart = chart
            .WithLayout(layout)
            .WithConfig(config);

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

    public int? LineWidth { get; init; } = 1;
    public string Name { get; init; } = "";
}