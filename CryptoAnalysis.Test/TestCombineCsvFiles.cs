using Gradient.CryptoAnalysis.Csv;

namespace Gradient.CryptoAnalysis.Tests
{
    [TestClass]
    public class CsvCombinerTests
    {
        [DataTestMethod]
        [DataRow("TestData", "CombineCsv", "COINBASE_SPAUSD, 60.csv")]
        public void TestCombineCsvFiles(params string[] path)
        {
            // Arrange
            string inputFilePath = Path.Combine(path);
            string outputFilePath = Path.Combine("c:\\", "temp", string.Join("\\", path));
            var combiner = new CsvCombiner();

            // Act
            combiner.CombineCsvFiles(inputFilePath, outputFilePath, x => x.DateTime, x => x.DateTime);

            // Assert
            Assert.IsTrue(File.Exists(outputFilePath));

            var lines = File.ReadAllLines(outputFilePath);
            Assert.IsTrue(lines.Length > 0); // Ensure the file is not empty
            Assert.AreEqual(lines.Count(), lines.Distinct().Count()); // Ensure no duplicate rows

            var reader = new CsvReaderHelper();
            var data = reader.ReadData<Price, PriceClassMap>(outputFilePath);
            Assert.AreEqual(lines.Count() - 1, data.Count()); // Ensure output can be read
        }
    }
}