namespace CryptoAnalysis.Tests
{
    [TestClass]
    public class MathsTests
    {
        [DataTestMethod]
        [DataRow(100, 150, -50)] // Final > Initial
        [DataRow(200, 150, 25)] // Final < Initial
        [DataRow(100, 100, 0)]  // Final == Initial
        [DataRow(-100, -150, -50)] // Negative Initial and Final
        [DataRow(0, 100, -100)] // Initial is zero
        [DataRow(0, -100, 100)] // Initial is zero
        public void PercentageDecrease_ShouldReturnExpectedValue(double initial, double final, double expected)
        {
            // Act
            double result = Maths.PercentageDecrease(initial, final);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [DataTestMethod]
        [DataRow(100, 150, 50)] // Final > Initial
        [DataRow(200, 150, -25)] // Final < Initial
        [DataRow(100, 100, 0)]  // Final == Initial
        [DataRow(-100, -150, 50)] // Negative Initial and Final
        [DataRow(0, 100, 100)] // Initial is zero
        [DataRow(0, -100, -100)] // Initial is zero
        public void PercentageIncrease_ShouldReturnExpectedValue(double initial, double final, double expected)
        {
            // Act
            double result = Maths.PercentageIncrease(initial, final);

            // Assert
            Assert.AreEqual(expected, result);
        }
    }
}