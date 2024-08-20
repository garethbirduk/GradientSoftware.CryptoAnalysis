using CryptoAnalysis.Csv.ClassMaps;
using Gradient.CryptoAnalysis.Csv;

namespace Gradient.CryptoAnalysis.Test.PriceExtensions
{
    [TestClass]
    public class ToHighSegmentsTests
    {
        private List<Price> _prices;
        public static readonly string _cryptoDataFilePath = Path.Combine("TestData", "PricesExtensionsData", "ToHighSegmentsTests", "TestData.csv");

        [TestInitialize]
        public void TestInitialize()
        {
            _prices = new CsvReaderHelper().ReadData<Price, PriceClassMap>(_cryptoDataFilePath).ToList();
        }

        [TestMethod]
        public void TestToHighSegments_Ok()
        {
            var segments = _prices.ToHighSegments();

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