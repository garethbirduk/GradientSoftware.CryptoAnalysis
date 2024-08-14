using Gradient.CryptoAnalysis.Tests;

namespace Gradient.CryptoAnalysis.Test.Conditions.PriceConditions
{
    [TestClass]
    public class IsSuccessiveGreenCandlesConditionTests
    {
        private List<Price> _prices = TestHelper.CreatePriceData(new DateTime(2024, 01, 01), 60, 11);

        [TestInitialize]
        public void TestInitialize()
        {
            _prices[0].Open = 100.0; _prices[0].Close = _prices[0].Open - 10.0;
            _prices[1].Open = 50.0; _prices[1].Close = _prices[1].Open + 10.0; // 1
            _prices[2].Open = 100.0; _prices[2].Close = _prices[2].Open - 10.0;
            _prices[3].Open = 50.0; _prices[3].Close = _prices[3].Open + 10.0; // 1
            _prices[4].Open = 100.0; _prices[4].Close = _prices[4].Open + 10.0; // 2
            _prices[5].Open = 200.0; _prices[5].Close = _prices[5].Open + 10.0; // 3
            _prices[6].Open = 400.0; _prices[6].Close = _prices[6].Open + 10.0; // 4
            _prices[7].Open = 100.0; _prices[7].Close = _prices[7].Open - 10.0;
            _prices[8].Open = 200.0; _prices[8].Close = _prices[8].Open + 10.0; // 1
            _prices[9].Open = 300.0; _prices[9].Close = _prices[9].Open + 10.0; // 2
            _prices[10].Open = 300.0; _prices[10].Close = _prices[10].Open + 10.0; // 3
        }

        [TestMethod]
        public void TestIsSuccessiveGreenCandlesCondition_0()
        {
            var conditionSet = new ConditionSet();
            conditionSet.AndConditions.Add(new IsSuccessiveGreenCandlesCondition(0));

            Assert.AreEqual(true, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 00, 00, 00)));
            Assert.AreEqual(true, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 01, 00, 00)));
            Assert.AreEqual(true, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 02, 00, 00)));
            Assert.AreEqual(true, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 03, 00, 00)));
        }

        [TestMethod]
        public void TestIsSuccessiveGreenCandlesCondition_1()
        {
            var conditionSet = new ConditionSet();
            conditionSet.AndConditions.Add(new IsSuccessiveGreenCandlesCondition(1));

            Assert.AreEqual(false, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 00, 00, 00)));
            Assert.AreEqual(true, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 01, 00, 00)));
            Assert.AreEqual(false, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 02, 00, 00)));
            Assert.AreEqual(true, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 03, 00, 00)));
        }

        [TestMethod]
        public void TestIsSuccessiveGreenCandlesCondition_2()
        {
            var conditionSet = new ConditionSet();
            conditionSet.AndConditions.Add(new IsSuccessiveGreenCandlesCondition(2));

            Assert.AreEqual(false, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 00, 00, 00)));
            Assert.AreEqual(false, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 01, 00, 00)));
            Assert.AreEqual(false, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 02, 00, 00)));
            Assert.AreEqual(false, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 03, 00, 00)));
            Assert.AreEqual(true, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 04, 00, 00)));
        }

        [TestMethod]
        public void TestIsSuccessiveGreenCandlesCondition_3()
        {
            var conditionSet = new ConditionSet();
            conditionSet.AndConditions.Add(new IsSuccessiveGreenCandlesCondition(3));

            Assert.AreEqual(false, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 00, 00, 00)));
            Assert.AreEqual(false, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 01, 00, 00)));
            Assert.AreEqual(false, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 02, 00, 00)));
            Assert.AreEqual(false, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 03, 00, 00)));
            Assert.AreEqual(false, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 04, 00, 00)));
            Assert.AreEqual(true, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 05, 00, 00)));
            Assert.AreEqual(true, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 06, 00, 00)));
            Assert.AreEqual(false, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 07, 00, 00)));
            Assert.AreEqual(false, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 08, 00, 00)));
            Assert.AreEqual(false, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 09, 00, 00)));
            Assert.AreEqual(true, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 10, 00, 00)));
        }

        [TestMethod]
        public void TestIsSuccessiveGreenCandlesCondition_4()
        {
            var conditionSet = new ConditionSet();
            conditionSet.AndConditions.Add(new IsSuccessiveGreenCandlesCondition(4));

            Assert.AreEqual(false, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 00, 00, 00)));
            Assert.AreEqual(false, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 01, 00, 00)));
            Assert.AreEqual(false, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 02, 00, 00)));
            Assert.AreEqual(false, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 03, 00, 00)));
            Assert.AreEqual(false, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 04, 00, 00)));
            Assert.AreEqual(false, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 05, 00, 00)));
            Assert.AreEqual(true, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 06, 00, 00)));
            Assert.AreEqual(false, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 07, 00, 00)));
            Assert.AreEqual(false, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 08, 00, 00)));
            Assert.AreEqual(false, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 09, 00, 00)));
            Assert.AreEqual(false, conditionSet.IsMet(_prices, new DateTime(2024, 01, 01, 10, 00, 00)));
        }
    }
}