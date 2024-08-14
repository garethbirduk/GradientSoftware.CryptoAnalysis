using Gradient.CryptoAnalysis;
using Gradient.CryptoAnalysis.Tests;

namespace Gradient.CryptoAnalysis.Test.Conditions.PriceConditions
{
    [TestClass]
    public class IsPriceHighGreaterThanOrEqualConditionTests
    {
        [TestMethod]
        public void TestIsPriceHighGreaterThanOrEqualCondition()
        {
            var data = TestHelper.CreatePriceData(new DateTime(2024, 01, 01), 60, 10);

            data[0].High = 100.0;
            data[1].High = 102.0;
            data[2].High = 100.0;
            data[3].High = 104.0;
            data[4].High = 107.0;
            data[5].High = 105.0;
            data[6].High = 110.0;
            data[7].High = 100.0;
            data[8].High = 101.0;
            data[9].High = 105.0;

            var condition = new Condition();
            condition.AndConditions.Add(new IsPriceHighGreaterThanOrEqualCondition(105.0));

            Assert.AreEqual(false, condition.IsMet(data, new DateTime(2024, 01, 01, 00, 00, 00)));
            Assert.AreEqual(false, condition.IsMet(data, new DateTime(2024, 01, 01, 01, 00, 00)));
            Assert.AreEqual(false, condition.IsMet(data, new DateTime(2024, 01, 01, 02, 00, 00)));
            Assert.AreEqual(false, condition.IsMet(data, new DateTime(2024, 01, 01, 03, 00, 00)));
            Assert.AreEqual(true, condition.IsMet(data, new DateTime(2024, 01, 01, 04, 00, 00)));
            Assert.AreEqual(true, condition.IsMet(data, new DateTime(2024, 01, 01, 05, 00, 00)));
            Assert.AreEqual(true, condition.IsMet(data, new DateTime(2024, 01, 01, 06, 00, 00)));
            Assert.AreEqual(false, condition.IsMet(data, new DateTime(2024, 01, 01, 07, 00, 00)));
            Assert.AreEqual(false, condition.IsMet(data, new DateTime(2024, 01, 01, 08, 00, 00)));
            Assert.AreEqual(true, condition.IsMet(data, new DateTime(2024, 01, 01, 09, 00, 00)));
        }
    }

    [TestClass]
    public class IsPriceLowLessThanOrEqualConditionTests
    {
        [TestMethod]
        public void TestIsPriceLowLessThanOrEqualCondition()
        {
            var data = TestHelper.CreatePriceData(new DateTime(2024, 01, 01), 60, 10);

            data[0].Low = 100.0;
            data[1].Low = 102.0;
            data[2].Low = 100.0;
            data[3].Low = 104.0;
            data[4].Low = 107.0;
            data[5].Low = 105.0;
            data[6].Low = 110.0;
            data[7].Low = 100.0;
            data[8].Low = 101.0;
            data[9].Low = 105.0;

            var condition = new Condition();
            condition.AndConditions.Add(new IsPriceLowLessThanOrEqualCondition(105.0));

            Assert.AreEqual(true, condition.IsMet(data, new DateTime(2024, 01, 01, 00, 00, 00)));
            Assert.AreEqual(true, condition.IsMet(data, new DateTime(2024, 01, 01, 01, 00, 00)));
            Assert.AreEqual(true, condition.IsMet(data, new DateTime(2024, 01, 01, 02, 00, 00)));
            Assert.AreEqual(true, condition.IsMet(data, new DateTime(2024, 01, 01, 03, 00, 00)));
            Assert.AreEqual(false, condition.IsMet(data, new DateTime(2024, 01, 01, 04, 00, 00)));
            Assert.AreEqual(true, condition.IsMet(data, new DateTime(2024, 01, 01, 05, 00, 00)));
            Assert.AreEqual(false, condition.IsMet(data, new DateTime(2024, 01, 01, 06, 00, 00)));
            Assert.AreEqual(true, condition.IsMet(data, new DateTime(2024, 01, 01, 07, 00, 00)));
            Assert.AreEqual(true, condition.IsMet(data, new DateTime(2024, 01, 01, 08, 00, 00)));
            Assert.AreEqual(true, condition.IsMet(data, new DateTime(2024, 01, 01, 09, 00, 00)));
        }
    }
}