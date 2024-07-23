namespace Gradient.CryptoAnalysis.Tests
{
    [TestClass]
    public class TradeConditionTests
    {
        [TestMethod]
        public void GreenCandlesCondition_ReturnsFalse_ForEqualClosePrices()
        {
            var condition = new IsSuccessiveGreenCandlesCondition(2);
            condition.SetPrices(new List<Price>
                {
                    new Price { Close = 0.005 },
                    new Price { Close = 0.006 },
                    new Price { Close = 0.006 }
                });

            Assert.IsFalse(condition.IsMet());
        }

        [TestMethod]
        public void GreenCandlesCondition_ReturnsFalse_ForNonIncreasingClosePrices()
        {
            var condition = new IsSuccessiveGreenCandlesCondition(2);
            condition.SetPrices(new List<Price>
                {
                    new Price { Close = 0.006 },
                    new Price { Close = 0.005 }
                });

            Assert.IsFalse(condition.IsMet());
        }

        [TestMethod]
        public void GreenCandlesCondition_ReturnsFalse_ForUpDownUpPrices()
        {
            var condition = new IsSuccessiveGreenCandlesCondition(3);
            condition.SetPrices(new List<Price>
                {
                    new Price { Close = 0.005 },
                    new Price { Close = 0.004 },
                    new Price { Close = 0.006 }
                });

            Assert.IsFalse(condition.IsMet());
        }

        [TestMethod]
        public void GreenCandlesCondition_ReturnsTrue_ForIncreasingClosePrices()
        {
            var condition = new IsSuccessiveGreenCandlesCondition(2);
            condition.SetPrices(new List<Price>
                {
                    new Price { Close = 0.005 },
                    new Price { Close = 0.006 }
                });

            Assert.IsTrue(condition.IsMet());
        }

        [TestMethod]
        [ExpectedException(typeof(NotSupportedException))]
        public void GreenCandlesCondition_ThrowsException_WhenDataSizeIsLessThanMinDataSize()
        {
            var condition = new IsSuccessiveGreenCandlesCondition(1);
            condition.SetPrices(new List<Price>
                {
                    new Price { Close = 0.005 }
                });

            condition.IsMet();
        }

        [TestMethod]
        public void RedCandlesCondition_ReturnsFalse_ForDownUpDownPrices()
        {
            var condition = new IsSuccessiveRedCandlesCondition(3);
            condition.SetPrices(new List<Price>
                {
                    new Price { Close = 0.006 },
                    new Price { Close = 0.007 },
                    new Price { Close = 0.005 }
                });

            Assert.IsFalse(condition.IsMet());
        }

        [TestMethod]
        public void RedCandlesCondition_ReturnsFalse_ForEqualClosePrices()
        {
            var condition = new IsSuccessiveRedCandlesCondition(3);
            condition.SetPrices(new List<Price>
                {
                    new Price { Close = 0.006 },
                    new Price { Close = 0.005 },
                    new Price { Close = 0.005 }
                });

            Assert.IsFalse(condition.IsMet());
        }

        [TestMethod]
        public void RedCandlesCondition_ReturnsFalse_ForNonDecreasingClosePrices()
        {
            var condition = new IsSuccessiveRedCandlesCondition(2);
            condition.SetPrices(new List<Price>
                {
                    new Price { Close = 0.005 },
                    new Price { Close = 0.006 }
                });

            Assert.IsFalse(condition.IsMet());
        }

        [TestMethod]
        public void RedCandlesCondition_ReturnsTrue_ForDecreasingClosePrices()
        {
            var condition = new IsSuccessiveRedCandlesCondition(3);
            condition.SetPrices(new List<Price>
                {
                    new Price { Close = 0.005 },
                    new Price { Close = 0.006 }
                });

            Assert.IsTrue(condition.IsMet());
        }

        [TestMethod]
        [ExpectedException(typeof(NotSupportedException))]
        public void RedCandlesCondition_ThrowsException_WhenDataSizeIsLessThanMinDataSize()
        {
            var condition = new IsSuccessiveRedCandlesCondition(3);
            condition.SetPrices(new List<Price>
                {
                    new Price { Close = 0.005 }
                });

            condition.IsMet();
        }
    }
}