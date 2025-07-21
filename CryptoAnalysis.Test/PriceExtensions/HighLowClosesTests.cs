using CryptoAnalysis.Csv.ClassMaps;
using Gradient.CryptoAnalysis.Csv;
using Plotly.NET;

namespace Gradient.CryptoAnalysis.Test.PriceExtensions
{
    [TestClass]
    public class HighLowClosesTests
    {
        private List<Price> _prices;
        public static readonly string _cryptoDataFilePath = Path.Combine("TestData", "PricesExtensionsData", "HighLowClosesTests", "TestData.csv");

        [TestMethod]
        public void TestGetLows()
        {
            CollectionAssert.AreEqual(new List<Price>(), new List<Price>().LowCloses());

            var lows = _prices.LowCloses();
            Assert.AreEqual(4, lows.Count);
            var i = 0;
            Assert.AreEqual(new DateTime(2024, 2, 7), lows[i++].DateTime);
            Assert.AreEqual(new DateTime(2024, 2, 7, 1, 0, 0), lows[i++].DateTime);
            Assert.AreEqual(new DateTime(2024, 2, 7, 2, 0, 0), lows[i++].DateTime);
            Assert.AreEqual(new DateTime(2024, 2, 7, 3, 0, 0), lows[i++].DateTime);
        }

        [TestInitialize]
        public void TestInitialize()
        {
            _prices = new CsvReaderHelper().ReadData<Price, PriceClassMap>(_cryptoDataFilePath).ToList();
        }

        [TestMethod]
        public void TestsToHighHighsUsingCloses()
        {
            CollectionAssert.AreEqual(new List<Price>(), new List<Price>().HighCloses());
            var chart = ChartGenerator.CreatePriceChart(_prices, lineCloses: true);

            var highHighs = _prices.ToHighHighsUsingCloses();
            var annotated = highHighs.ToAnnotatedPrices(EnumAnnotationType.HigherHigh);
            chart = chart.AddLayers(ChartGenerator.CreateAnnotationLayer(annotated, StyleParam.MarkerSymbol.TriangleUp, Color.fromString("Green")));
            chart.SaveHtml(Path.Combine("c:\\", "temp", "ToHighHighsUsingCloses"));
        }

        [TestMethod]
        public void TestsToHighHighsUsingHighs()
        {
            CollectionAssert.AreEqual(new List<Price>(), new List<Price>().HighCloses());
            var chart = ChartGenerator.CreatePriceChart(_prices, lineHighs: true);

            var highHighs = _prices.ToHighHighsUsingHighs();
            var annotated = highHighs.ToAnnotatedPrices(EnumAnnotationType.HigherHigh);
            chart = chart.AddLayers(ChartGenerator.CreateAnnotationLayer(annotated, StyleParam.MarkerSymbol.TriangleUp, Color.fromString("Green")));
            chart.SaveHtml(Path.Combine("c:\\", "temp", "ToHighHighsUsingHighs"));
        }
    }
}