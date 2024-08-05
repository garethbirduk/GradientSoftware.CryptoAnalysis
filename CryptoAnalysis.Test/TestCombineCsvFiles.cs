namespace Gradient.CryptoAnalysis.Tests
{
    [TestClass]
    public class CsvCombinerTests
    {
        [TestMethod]
        public void TestCombineCsvFiles()
        {
            // Arrange
            string inputFilePath = @"C:\dev\GitHub\Gradient\CryptoAnalysis\Data\COINBASE_BTCUSD, 60.csv";
            string outputFilePath = @"C:\dev\GitHub\Gradient\CryptoAnalysis\Data\Combined.csv";
            var combiner = new CsvCombiner();

            // Act
            combiner.CombineCsvFiles(inputFilePath, outputFilePath, x => x.DateTime, x => x.DateTime);

            // Assert
            Assert.IsTrue(File.Exists(outputFilePath));

            var lines = File.ReadAllLines(outputFilePath);
            Assert.IsTrue(lines.Length > 0); // Ensure the file is not empty
            Assert.AreEqual(lines.Count(), lines.Distinct().Count()); // Ensure no duplicate rows
        }
    }
}