using CryptoAnalysis.Csv.ClassMaps;
using Gradient.CryptoAnalysis.Csv;
using Plotly.NET;

namespace Gradient.CryptoAnalysis.Test.PriceExtensions;

[TestClass]
public class ClosesTests
{
    private List<Price> _prices;

    public static readonly string _cryptoDataFilePath = Path.Combine("TestData", "PricesExtensionsData", "AllTimeHighTests", "TestData.csv");
    //public static readonly string _cryptoDataFilePath = Path.Combine("TestData", "COINBASE_BTCUSD, 60.csv");

    [TestMethod]
    public void TestAllTimeHighs_HighestCloses()
    {
        var chart = ChartGenerator.CreatePriceChart(_prices, lineCloses: true, lineWidth: 1);

        var segment = _prices.AllTimeHighs(EnumCloseType.Close);
        chart = chart.AddLayers(new Layer
        {
            Name = "base",
            ChartFactory = () => ChartGenerator.GenerateLineChart(
                segment,
                p => (decimal)p.Close,
                "Close Prices"
            ),
            Color = Color.fromString("red"),
            LineWidth = 1,
        });
        chart.SaveHtml(TestHelper.HtmlPath("TestAllTimeHighs_HighestCloses"));
    }

    [TestMethod]
    public void TestAllTimeHighs_HighestsHighs()
    {
        var chart = ChartGenerator.CreatePriceChart(_prices, lineCloses: true, lineWidth: 1);

        var segment = _prices.AllTimeHighs(EnumCloseType.High);
        chart = chart.AddLayers(new Layer
        {
            Name = "base",
            ChartFactory = () => ChartGenerator.GenerateLineChart(
                segment,
                p => (decimal)p.High,
                "Close Prices"
            ),
            Color = Color.fromString("red"),
            LineWidth = 1,
        });

        chart.SaveHtml(TestHelper.HtmlPath("TestAllTimeHighs_HighestsHighs"));
    }

    [TestMethod]
    public void TestAllTimeHighs_HighestsHighs2_Multisegments()
    {
        var chart = ChartGenerator.CreatePriceChart(_prices, lineCloses: true, lineWidth: 1);

        var p1 = _prices.Take(10000);
        var p2 = _prices.Skip(15000).Take(10000);

        var s1 = p1.AllTimeHighs(EnumCloseType.High);
        var s2 = p2.AllTimeHighs(EnumCloseType.High);

        chart = chart.AddLayers(
            new Layer
            {
                Name = "base",
                ChartFactory = () => ChartGenerator.GenerateScatterChart(
                    s1,
                    p => (decimal)p.High,
                    "Close Prices"
                ),
                Color = Color.fromString("red"),
                LineWidth = 1,
            },
            new Layer
            {
                Name = "base",
                ChartFactory = () => ChartGenerator.GenerateScatterChart(
                    s2,
                    p => (decimal)p.High,
                    "Close Prices"
                ),
                Color = Color.fromString("blue"),
                LineWidth = 1,
            }
            );

        chart.SaveHtml(TestHelper.HtmlPath("TestAllTimeHighs_HighestsHighs2_Multisegments"));
    }

    [TestInitialize]
    public void TestInitialize()
    {
        _prices = new CsvReaderHelper().ReadData<Price, PriceClassMap>(_cryptoDataFilePath)
            .Skip(20000)
            .Take(20000)
            .ToList();
    }
}

//[TestMethod]
//public void TestToHighSegments_Ok()
//{
//    var chart = ChartGenerator.CreatePriceChart(_prices, lineCloses: true, lineWidth: 5);

//    var segments = _prices.ToHighSegments();
//    foreach (var segment in segments)
//    {
//        chart = chart.AddLayers(new Layer
//        {
//            Name = "base",
//            ChartFactory = () => ChartGenerator.GenerateLineChart(
//                segment,
//                p => (decimal)p.Close,
//                "Close Prices"
//            ),
//            Color = Color.fromString("red"),
//            LineWidth = 3,
//        });
//    }

//    //var annotatedHigherHighs = _prices.HigherHighs().ToAnnotatedPrices(EnumAnnotationType.HigherHigh);
//    //chart = chart.AddLayers(ChartGenerator.CreateAnnotationLayer(annotatedHigherHighs, StyleParam.MarkerSymbol.ArrowUp));

//    //var annotatedHigherLows = _prices.ToHigherLows().ToAnnotatedPrices(EnumAnnotationType.HigherLow);
//    //chart = chart.AddLayers(ChartGenerator.CreateAnnotationLayer(annotatedHigherLows, StyleParam.MarkerSymbol.ArrowDown));

//    var upswings = _prices.ToUpswings();
//    foreach (var upswing in upswings)
//    {
//        chart = chart.AddLayers(ChartGenerator.PriceClosesLineLayer(upswing.Prices, Color.fromString("Green"), lineWidth: 2));
//    }

//    //var sawtooth = _prices.ToUptrendSawtooth();
//    //chart = chart.AddLayers(new Layer
//    //{
//    //    Name = "base",
//    //    ChartFactory = () => ChartGenerator.GenerateLineChart(
//    //        sawtooth,
//    //        p => (decimal)p.Close,
//    //        "Close Prices"
//    //    ),
//    //    Color = Color.fromString("pink"),
//    //    LineWidth = 1,
//    //});

//    //(var breakOfStructures, var marketStructureBreaks) = _prices.ToStructures();
//    //foreach (var bos in breakOfStructures)
//    //{
//    //    var start = new Price()
//    //    {
//    //        Close = bos.Item2.Close,
//    //        DateTime = bos.Item2.DateTime
//    //    };
//    //    var end = new Price()
//    //    {
//    //        Close = bos.Item2.Close, // yes item2
//    //        DateTime = bos.Item1.DateTime
//    //    };

//    //    var bosPrices = new List<Price>() { start, end };
//    //    chart = chart.AddLayers(ChartGenerator.PriceClosesLineLayer(bosPrices, Color.fromString("Cyan")));
//    //}
//    //foreach (var msb in marketStructureBreaks)
//    //{
//    //    var start = new Price()
//    //    {
//    //        Close = msb.Item2.Close,
//    //        DateTime = msb.Item2.DateTime
//    //    };
//    //    var end = new Price()
//    //    {
//    //        Close = msb.Item2.Close, // yes item2
//    //        DateTime = msb.Item1.DateTime
//    //    };

//    //    var msbPrices = new List<Price>() { start, end };
//    //    chart = chart.AddLayers(ChartGenerator.PriceClosesLineLayer(msbPrices, Color.fromString("Red")));
//    //}
//    chart.SaveHtml(TestHelper.HtmlPath("TestToHighSegments_Ok"));

//    //Assert.AreEqual(10, segment.Count);
//    //Assert.AreEqual(9, segment[0].Count);
//    //Assert.AreEqual(4, segment[1].Count);
//    //Assert.AreEqual(3, segment[2].Count);
//    //Assert.AreEqual(2, segment[3].Count);
//    //Assert.AreEqual(2, segment[4].Count);
//    //Assert.AreEqual(8, segment[5].Count);
//    //Assert.AreEqual(2, segment[6].Count);
//    //Assert.AreEqual(3, segment[7].Count);
//    //Assert.AreEqual(5, segment[8].Count);
//    //Assert.AreEqual(4, segment[9].Count);
//}

//[TestMethod]
//public void TestToSegments_Empty()
//{
//    CollectionAssert.AreEqual(new List<List<Price>>(), new List<Price>().ToHighSegments());
//}