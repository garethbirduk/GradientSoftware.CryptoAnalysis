using CryptoAnalysis.Csv.ClassMaps;
using Gradient.CryptoAnalysis;
using Gradient.CryptoAnalysis.Csv;

namespace CryptoAnalysis.Test.Csv
{
    [TestClass]
    public class CsvReaderHelperTests
    {
        [TestMethod]
        public void CsvReaderHelper_ShouldReadBacktestResultsCorrectly()
        {
            // Arrange
            var csvHelper = new CsvReaderHelper();

            // Act
            var data = csvHelper.ReadData<BacktestResult, BacktestResultClassMap>(Path.Combine("TestData", "ReadBacktestResults", "BacktestResults.csv")).ToList();

            // Assert
            Assert.IsNotNull(data);
            Assert.IsTrue(data.Count > 0);
        }

        [TestMethod]
        public void CsvReaderHelper_ShouldReadCryptoCoinDataCorrectly()
        {
            // Arrange
            var csvHelper = new CsvReaderHelper();

            // Act
            var data = csvHelper.ReadData<Price, PriceClassMap>(Path.Combine("TestData", "ReadPriceData", "PriceData.csv")).ToList();

            // Assert
            Assert.IsNotNull(data);
            Assert.IsTrue(data.Count > 0);
            var expectedTime = DateTime.SpecifyKind(new DateTime(2023, 11, 12, 01, 0, 0), DateTimeKind.Utc);
            Assert.AreEqual(expectedTime, data[0].DateTime.ToUniversalTime());
            Assert.AreEqual(0.005192, data[0].Open);
            Assert.AreEqual(0.00537, data[0].High);
            Assert.AreEqual(0.005172, data[0].Low);
            Assert.AreEqual(0.005196, data[0].Close);
        }
    }
}