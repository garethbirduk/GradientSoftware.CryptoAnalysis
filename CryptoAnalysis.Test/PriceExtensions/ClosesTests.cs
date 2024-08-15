namespace Gradient.CryptoAnalysis.Test.PriceExtensions
{
    [TestClass]
    public class ClosesTests
    {
        [DataTestMethod]
        [DataRow(100, 100, 95, 95, 70, 70, 20, true)]
        [DataRow(100, 100, 95, 95, 80, 80, 20, true)]
        [DataRow(100, 100, 95, 95, 90, 90, 20, false)]
        [DataRow(100, 100, 200, 200, 70, 70, 20, true)]
        [DataRow(100, 100, 110, 110, 70, 70, -20, false)]
        [DataRow(100, 100, 110, 110, 80, 80, -20, false)]
        [DataRow(100, 100, 110, 110, 90, 90, -20, false)]
        [DataRow(100, 100, 110, 110, 120, 120, -20, true)]
        public void HasDecreasedByPercentage_ValidData_CalculatesCorrectly(double open1, double close1, double open2, double close2, double open3, double close3, double percentageIncrease, bool expectedResult)
        {
            // Arrange
            var data = new List<Price>
            {
                new Price { DateTime = DateTime.Parse("2023-11-12T01:00:00Z"), Open = open1, Close = close1 },
                new Price { DateTime = DateTime.Parse("2023-11-12T02:00:00Z"), Open = open2, Close = close2 },
                new Price { DateTime = DateTime.Parse("2023-11-12T03:00:00Z"), Open = open3, Close = close3 }
            };

            // Act
            bool result = data.HasDecreasedByPercentage(percentageIncrease);

            // Assert
            Assert.AreEqual(expectedResult, result);
        }

        [DataTestMethod]
        [DataRow(100, 100, 110, 110, 118, 118, 20, false)]
        [DataRow(100, 100, 110, 110, 120, 120, 20, true)]
        [DataRow(100, 100, 110, 110, 122, 122, 20, true)]
        [DataRow(100, 100, 200, 200, 122, 122, 20, true)]
        [DataRow(100, 100, 50, 50, 122, 122, 20, true)]
        [DataRow(100, 100, 110, 110, 70, 70, -20, true)]
        [DataRow(100, 100, 110, 110, 80, 80, -20, true)]
        [DataRow(100, 100, 110, 110, 90, 90, -20, false)]
        public void HasIncreasedByPercentage_ValidData_CalculatesCorrectly(double open1, double close1, double open2, double close2, double open3, double close3, double percentageIncrease, bool expectedResult)
        {
            // Arrange
            var data = new List<Price>
            {
                new Price { DateTime = DateTime.Parse("2023-11-12T01:00:00Z"), Open = open1, Close = close1 },
                new Price { DateTime = DateTime.Parse("2023-11-12T02:00:00Z"), Open = open2, Close = close2 },
                new Price { DateTime = DateTime.Parse("2023-11-12T03:00:00Z"), Open = open3, Close = close3 }
            };

            // Act
            bool result = data.HasIncreasedByPercentage(percentageIncrease);

            // Assert
            Assert.AreEqual(expectedResult, result);
        }
    }
}