using CryptoAnalysis.Csv.ClassMaps;
using Gradient.CryptoAnalysis.Csv;
using Plotly.NET;

namespace Gradient.CryptoAnalysis.Test.PriceExtensions
{
    [TestClass]
    public class ToHighSegmentsTests
    {
        private List<Price> _prices;

        //public static readonly string _cryptoDataFilePath = Path.Combine("TestData", "PricesExtensionsData", "ToHighSegmentsTests", "TestData.csv");
        public static readonly string _cryptoDataFilePath = Path.Combine("TestData", "COINBASE_BTCUSD, 60.csv");

        [TestInitialize]
        public void TestInitialize()
        {
            _prices = new CsvReaderHelper().ReadData<Price, PriceClassMap>(_cryptoDataFilePath).ToList();
        }

        [TestMethod]
        public void TestToHighSegments_Ok()
        {
            var chart = ChartGenerator.CreatePriceChart(_prices, lineCloses: true);

            var segments = _prices.ToHighSegments();
            //foreach (var segment in segments)
            //{
            //    chart = chart.AddLayers(new Layer
            //    {
            //        Name = "base",
            //        ChartFactory = () => ChartGenerator.GenerateLineChart(
            //            segment,
            //            p => (decimal)p.Close,
            //            "Close Prices"
            //        ),
            //        Color = Color.fromString("red"),
            //        LineWidth = 3,
            //    });
            //}

            //var annotatedHigherHighs = _prices.ToHigherHighs().ToAnnotatedPrices(EnumAnnotationType.HigherHigh);
            //chart = chart.AddLayers(ChartGenerator.CreateAnnotationLayer(annotatedHigherHighs));

            //var annotatedHigherLows = _prices.ToHigherLows().ToAnnotatedPrices(EnumAnnotationType.HigherLow);
            //chart = chart.AddLayers(ChartGenerator.CreateAnnotationLayer(annotatedHigherLows));

            //var sawtooth = _prices.ToUptrendSawtooth();
            //chart = chart.AddLayers(new Layer
            //{
            //    Name = "base",
            //    ChartFactory = () => ChartGenerator.GenerateLineChart(
            //        sawtooth,
            //        p => (decimal)p.Close,
            //        "Close Prices"
            //    ),
            //    Color = Color.fromString("green"),
            //    LineWidth = 3,
            //});

            var breakOfStructures = _prices.ToUptrendBreakOfStructures();
            foreach (var bos in breakOfStructures)
            {
                var start = new Price()
                {
                    Close = bos.Item2.Close,
                    DateTime = bos.Item2.DateTime
                };
                var end = new Price()
                {
                    Close = bos.Item2.Close, // yes item2
                    DateTime = bos.Item1.DateTime
                };

                var bosPrices = new List<Price>() { start, end };
                //var annotatedUptrendBreakOfStructures = bosPrices.ToAnnotatedPrices(EnumAnnotationType.BreakOfStructure);
                chart = chart.AddLayers(ChartGenerator.PriceClosesLineLayer(bosPrices, Color.fromString("Cyan")));
            }

            chart.SaveHtml(Path.Combine("c:\\", "temp", "toHighSegments"));

            Assert.AreEqual(10, segments.Count);
            Assert.AreEqual(9, segments[0].Count);
            Assert.AreEqual(4, segments[1].Count);
            Assert.AreEqual(3, segments[2].Count);
            Assert.AreEqual(2, segments[3].Count);
            Assert.AreEqual(2, segments[4].Count);
            Assert.AreEqual(8, segments[5].Count);
            Assert.AreEqual(2, segments[6].Count);
            Assert.AreEqual(3, segments[7].Count);
            Assert.AreEqual(5, segments[8].Count);
            Assert.AreEqual(4, segments[9].Count);
        }

        [TestMethod]
        public void TestToSegments_Empty()
        {
            CollectionAssert.AreEqual(new List<List<Price>>(), new List<Price>().ToHighSegments());
        }
    }
}