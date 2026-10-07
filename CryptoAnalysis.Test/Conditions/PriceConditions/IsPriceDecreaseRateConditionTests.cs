using Gradient.CryptoAnalysis.Tests;

namespace Gradient.CryptoAnalysis.Test.Conditions.PriceConditions
{
    [TestClass]
    public class IsPriceDecreaseRateConditionTests
    {
        [TestMethod]
        public void TestIsPriceDecreaseRateCondition_10pc_FirstToLast()
        {
            var data = TestHelper.CreatePriceData(new DateTime(2024, 01, 01), 60, 7);

            data[0].Close = 110.0;
            data[1].Close = 102.0;
            data[2].Close = 100.0;
            data[3].Close = 104.0;
            data[4].Close = 107.0;
            data[5].Close = 105.0;
            data[6].Close = 90.0;

            var Decrease10pc = new ConditionSet();
            Decrease10pc.AndConditions.Add(new IsPriceDecreaseRateCondition(10, 4, SubsetType.FirstToLast));

            Assert.AreEqual(false, Decrease10pc.IsMet(data, new DateTime(2024, 01, 01, 00, 00, 00)));
            Assert.AreEqual(false, Decrease10pc.IsMet(data, new DateTime(2024, 01, 01, 01, 00, 00)));
            Assert.AreEqual(false, Decrease10pc.IsMet(data, new DateTime(2024, 01, 01, 02, 00, 00)));
            Assert.AreEqual(false, Decrease10pc.IsMet(data, new DateTime(2024, 01, 01, 03, 00, 00)));
            Assert.AreEqual(false, Decrease10pc.IsMet(data, new DateTime(2024, 01, 01, 04, 00, 00)));
            Assert.AreEqual(false, Decrease10pc.IsMet(data, new DateTime(2024, 01, 01, 05, 00, 00)));
            Assert.AreEqual(true, Decrease10pc.IsMet(data, new DateTime(2024, 01, 01, 06, 00, 00)));
        }

        [TestMethod]
        public void TestIsPriceDecreaseRateCondition_4pc_FirstToLast()
        {
            var data = TestHelper.CreatePriceData(new DateTime(2024, 01, 01), 60, 7);

            data[0].Close = 110.0;
            data[1].Close = 102.0;
            data[2].Close = 100.0;
            data[3].Close = 104.0;
            data[4].Close = 107.0;
            data[5].Close = 105.0;
            data[6].Close = 90.0;

            var Decrease4pc = new ConditionSet();
            Decrease4pc.AndConditions.Add(new IsPriceDecreaseRateCondition(4.5, 2, SubsetType.FirstToLast));

            Assert.AreEqual(false, Decrease4pc.IsMet(data, new DateTime(2024, 01, 01, 00, 00, 00)));
            Assert.AreEqual(false, Decrease4pc.IsMet(data, new DateTime(2024, 01, 01, 01, 00, 00)));
            Assert.AreEqual(true, Decrease4pc.IsMet(data, new DateTime(2024, 01, 01, 02, 00, 00)));
            Assert.AreEqual(false, Decrease4pc.IsMet(data, new DateTime(2024, 01, 01, 03, 00, 00)));
            Assert.AreEqual(false, Decrease4pc.IsMet(data, new DateTime(2024, 01, 01, 04, 00, 00)));
            Assert.AreEqual(false, Decrease4pc.IsMet(data, new DateTime(2024, 01, 01, 05, 00, 00)));
            Assert.AreEqual(true, Decrease4pc.IsMet(data, new DateTime(2024, 01, 01, 06, 00, 00)));
        }

        [TestMethod]
        public void TestIsPriceDecreaseRateCondition_4pc_LowestToLast()
        {
            var data = TestHelper.CreatePriceData(new DateTime(2024, 01, 01), 60, 7);

            data[0].Close = 110.0;
            data[1].Close = 102.0;
            data[2].Close = 100.0;
            data[3].Close = 104.0;
            data[4].Close = 107.0;
            data[5].Close = 105.0;
            data[6].Close = 90.0;

            var Decrease4pc = new ConditionSet();
            Decrease4pc.AndConditions.Add(new IsPriceDecreaseRateCondition(4.5, 2, SubsetType.HighestToLast));

            Assert.AreEqual(false, Decrease4pc.IsMet(data, new DateTime(2024, 01, 01, 00, 00, 00)));
            Assert.AreEqual(true, Decrease4pc.IsMet(data, new DateTime(2024, 01, 01, 01, 00, 00)));
            Assert.AreEqual(true, Decrease4pc.IsMet(data, new DateTime(2024, 01, 01, 02, 00, 00)));
            Assert.AreEqual(false, Decrease4pc.IsMet(data, new DateTime(2024, 01, 01, 03, 00, 00)));
            Assert.AreEqual(false, Decrease4pc.IsMet(data, new DateTime(2024, 01, 01, 04, 00, 00)));
            Assert.AreEqual(false, Decrease4pc.IsMet(data, new DateTime(2024, 01, 01, 05, 00, 00)));
            Assert.AreEqual(true, Decrease4pc.IsMet(data, new DateTime(2024, 01, 01, 06, 00, 00)));
        }
    }
}