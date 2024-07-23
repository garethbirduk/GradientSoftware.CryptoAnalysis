namespace Gradient.CryptoAnalysis.Tests
{
    [TestClass]
    public class CsvCombinerTests
    {
        [TestMethod]
        public void TestCombineCsvFiles()
        {
            // Arrange
            string inputFilePath = @"C:\dev\GitHub\Gradient\CryptoAnalysis\Prices\COINBASE_SPAUSD, 60.csv";
            string outputFilePath = @"C:\dev\GitHub\Gradient\CryptoAnalysis\Prices\Combined.csv";
            var combiner = new CsvCombiner();

            // Act
            combiner.CombineCsvFiles(inputFilePath, outputFilePath, x => x.Time, x => x.Time);

            // Assert
            Assert.IsTrue(File.Exists(outputFilePath));

            var lines = File.ReadAllLines(outputFilePath);
            Assert.IsTrue(lines.Length > 0); // Ensure the file is not empty
            Assert.AreEqual(lines.Count(), lines.Distinct().Count()); // Ensure no duplicate rows
        }
    }
}