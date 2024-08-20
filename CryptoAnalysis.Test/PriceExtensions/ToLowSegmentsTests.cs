using CryptoAnalysis.Csv.ClassMaps;
using Gradient.CryptoAnalysis.Csv;

namespace Gradient.CryptoAnalysis.Test.PriceExtensions
{
    [TestClass]
    public class ToLowSegmentsTests
    {
        private List<Price> _prices;
        public static readonly string _cryptoDataFilePath = Path.Combine("TestData", "PricesExtensionsData", "ToLowSegmentsTests", "TestData.csv");

        [TestInitialize]
        public void TestInitialize()
        {
            _prices = new CsvReaderHelper().ReadData<Price, PriceClassMap>(_cryptoDataFilePath).ToList();
        }

        [TestMethod]
        public void TestToLowSegments_Empty()
        {
            CollectionAssert.AreEqual(new List<List<Price>>(), new List<Price>().ToLowSegments());
        }

        [TestMethod]
        public void TestToLowSegments_Ok()
        {
            var segments = _prices.ToLowSegments();

            Assert.AreEqual(7, segments.Count);
            Assert.AreEqual(8, segments[0].Count);
            Assert.AreEqual(3, segments[1].Count);
            Assert.AreEqual(4, segments[2].Count);
            Assert.AreEqual(6, segments[3].Count);
            Assert.AreEqual(2, segments[4].Count);
            Assert.AreEqual(2, segments[5].Count);
            Assert.AreEqual(3, segments[6].Count);
        }
    }
}