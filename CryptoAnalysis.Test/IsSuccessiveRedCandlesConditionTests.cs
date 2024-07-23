namespace Gradient.CryptoAnalysis.Tests
{
    [TestClass]
    public class IsPriceIncreaseRateConditionTests
    {
        [TestMethod]
        public void TestIsPriceIncreaseRateCondition()
        {
            var data = TestHelper.CreatePriceData(new DateTime(2024, 01, 01), 60, 10);

            data[0].Close = 100.0;
            data[1].Close = 102.0;
            data[2].Close = 100.0;
            data[3].Close = 104.0;
            data[4].Close = 107.0; // 100 -> 102 = 7% in 3
            data[5].Close = 105.0;
            data[6].Close = 110.0; // 100 -> 110 = 10% in 5
            data[7].Close = 100.0;
            data[8].Close = 101.0;
            data[9].Close = 105.0; // 100 -> 102 = 5% in 3

            var increase10pc = new Condition();
            increase10pc.AndConditions.Add(new IsPriceIncreaseRateCondition(10, 5));

            Assert.ThrowsException<ArgumentException>(() => increase10pc.IsMet(data, new DateTime(2024, 01, 01, 00, 00, 00)));
            Assert.ThrowsException<ArgumentException>(() => increase10pc.IsMet(data, new DateTime(2024, 01, 01, 01, 00, 00)));
            Assert.ThrowsException<ArgumentException>(() => increase10pc.IsMet(data, new DateTime(2024, 01, 01, 02, 00, 00)));
            Assert.ThrowsException<ArgumentException>(() => increase10pc.IsMet(data, new DateTime(2024, 01, 01, 03, 00, 00)));
            Assert.AreEqual(false, increase10pc.IsMet(data, new DateTime(2024, 01, 01, 04, 00, 00)));
            Assert.AreEqual(false, increase10pc.IsMet(data, new DateTime(2024, 01, 01, 05, 00, 00)));
            Assert.AreEqual(true, increase10pc.IsMet(data, new DateTime(2024, 01, 01, 06, 00, 00)));
            Assert.AreEqual(false, increase10pc.IsMet(data, new DateTime(2024, 01, 01, 07, 00, 00)));
            Assert.AreEqual(false, increase10pc.IsMet(data, new DateTime(2024, 01, 01, 08, 00, 00)));
            Assert.AreEqual(false, increase10pc.IsMet(data, new DateTime(2024, 01, 01, 09, 00, 00)));

            var increase4pc = new Condition();
            increase4pc.AndConditions.Add(new IsPriceIncreaseRateCondition(4.5, 3));

            Assert.ThrowsException<ArgumentException>(() => increase4pc.IsMet(data, new DateTime(2024, 01, 01, 00, 00, 00)));
            Assert.ThrowsException<ArgumentException>(() => increase4pc.IsMet(data, new DateTime(2024, 01, 01, 01, 00, 00)));
            Assert.AreEqual(false, increase4pc.IsMet(data, new DateTime(2024, 01, 01, 02, 00, 00)));
            Assert.AreEqual(false, increase4pc.IsMet(data, new DateTime(2024, 01, 01, 03, 00, 00)));
            Assert.AreEqual(true, increase4pc.IsMet(data, new DateTime(2024, 01, 01, 04, 00, 00)));
            Assert.AreEqual(false, increase4pc.IsMet(data, new DateTime(2024, 01, 01, 05, 00, 00)));
            Assert.AreEqual(false, increase4pc.IsMet(data, new DateTime(2024, 01, 01, 06, 00, 00)));
            Assert.AreEqual(false, increase4pc.IsMet(data, new DateTime(2024, 01, 01, 07, 00, 00)));
            Assert.AreEqual(false, increase4pc.IsMet(data, new DateTime(2024, 01, 01, 08, 00, 00)));
            Assert.AreEqual(true, increase4pc.IsMet(data, new DateTime(2024, 01, 01, 09, 00, 00)));
        }
    }
}