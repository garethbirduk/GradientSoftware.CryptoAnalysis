namespace Gradient.CryptoAnalysis.Tests
{
    [TestClass]
    public class CryptoCoinDataExtensionsTest
    {
        public static List<Price> CreateData(DateTime startDateTime, int interval, int count)
        {
            var random = new Random();
            var data = new List<Price>();

            double lastClose = random.Next(100, 200); // Initial close price

            for (int i = 0; i < count; i++)
            {
                double open = lastClose;
                double close = open + random.Next(-10, 11); // Random variation
                double high = Math.Max(open, close) + random.Next(1, 5);
                double low = Math.Min(open, close) - random.Next(1, 5);

                var cryptoData = new Price
                {
                    DateTime = startDateTime.AddMinutes(i * interval),
                    Open = open,
                    Close = close,
                    High = high,
                    Low = low
                };

                data.Add(cryptoData);
                lastClose = close; // Set lastClose for the next iteration
            }

            return data;
        }

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

        [TestMethod]
        public void TakePreviousPeriods_ReturnsCorrectSubset_WhenIndexIsInRange()
        {
            // Arrange
            var data = new List<Price>
            {
                new Price { DateTime = DateTime.Parse("2023-11-12T01:00:00Z"), Open = 0.005192, High = 0.00537, Low = 0.005172, Close = 0.005196 },
                new Price { DateTime = DateTime.Parse("2023-11-12T02:00:00Z"), Open = 0.005193, High = 0.005353, Low = 0.005133, Close = 0.005133 },
                new Price { DateTime = DateTime.Parse("2023-11-12T03:00:00Z"), Open = 0.00513, High = 0.005178, Low = 0.005077, Close = 0.005114 },
                new Price { DateTime = DateTime.Parse("2023-11-12T04:00:00Z"), Open = 0.005114, High = 0.005273, Low = 0.005114, Close = 0.00527 },
                new Price { DateTime = DateTime.Parse("2023-11-12T05:00:00Z"), Open = 0.005267, High = 0.005335, Low = 0.005211, Close = 0.005315 },
                new Price { DateTime = DateTime.Parse("2023-11-12T06:00:00Z"), Open = 0.005276, High = 0.005432, Low = 0.005266, Close = 0.005432 },
                new Price { DateTime = DateTime.Parse("2023-11-12T07:00:00Z"), Open = 0.005438, High = 0.005541, Low = 0.005392, Close = 0.005538 },
                new Price { DateTime = DateTime.Parse("2023-11-12T08:00:00Z"), Open = 0.005539, High = 0.005553, Low = 0.005405, Close = 0.005459 },
                new Price { DateTime = DateTime.Parse("2023-11-12T09:00:00Z"), Open = 0.005459, High = 0.00552, Low = 0.005426, Close = 0.00552 },
                new Price { DateTime = DateTime.Parse("2023-11-12T10:00:00Z"), Open = 0.00552, High = 0.005847, Low = 0.00552, Close = 0.005847 },
            };

            // Act
            var result = data.TakePreviousPrices(3, 7).ToList();

            // Assert
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(data[5], result[0]);
            Assert.AreEqual(data[6], result[1]);
            Assert.AreEqual(data[7], result[2]);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void TakePreviousPeriods_ThrowsException_WhenIndexIsOutOfRange()
        {
            // Arrange
            var data = new List<Price>
            {
                new Price { DateTime = DateTime.Parse("2023-11-12T01:00:00Z"), Open = 0.005192, High = 0.00537, Low = 0.005172, Close = 0.005196 }
            };

            // Act
            var result = data.TakePreviousPrices(1, 1).ToList();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void TakePreviousPeriods_ThrowsException_WhenNotEnoughPeriods()
        {
            // Arrange
            var data = new List<Price>
            {
                new Price { DateTime = DateTime.Parse("2023-11-12T01:00:00Z"), Open = 0.005192, High = 0.00537, Low = 0.005172, Close = 0.005196 },
                new Price { DateTime = DateTime.Parse("2023-11-12T02:00:00Z"), Open = 0.005193, High = 0.005353, Low = 0.005133, Close = 0.005133 },
                new Price { DateTime = DateTime.Parse("2023-11-12T03:00:00Z"), Open = 0.00513, High = 0.005178, Low = 0.005077, Close = 0.005114 }
            };

            // Act
            var result = data.TakePreviousPrices(4, 2).ToList();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void TakePreviousPeriods_ThrowsException_WhenPeriodsIsNonPositive()
        {
            // Arrange
            var data = new List<Price>
            {
                new Price { DateTime = DateTime.Parse("2023-11-12T01:00:00Z"), Open = 0.005192, High = 0.00537, Low = 0.005172, Close = 0.005196 }
            };

            // Act
            var result = data.TakePreviousPrices(0, 0).ToList();
        }
    }
}