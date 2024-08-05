namespace Gradient.CryptoAnalysis.Tests
{
    [TestClass]
    public class IsSuccessiveGreenCandlesConditionTests
    {
        [TestMethod]
        public void TestIsSuccessiveGreenCandlesCondition()
        {
            var data = TestHelper.CreatePriceData(new DateTime(2024, 01, 01), 60, 11);

            data[0].Open = 100.0;
            data[1].Open = 50.0;
            data[2].Open = 100.0;
            data[3].Open = 50.0;
            data[4].Open = 100.0;
            data[5].Open = 200.0; // 3 in a row
            data[6].Open = 400.0; // 4 in a row
            data[7].Open = 100.0;
            data[8].Open = 200.0;
            data[9].Open = 300.0;
            data[10].Open = 300.0; // 3 in a row

            data[0].Close = data[0].Open - 10.0;
            data[1].Close = data[1].Open + 10.0;
            data[2].Close = data[2].Open - 10.0;
            data[3].Close = data[3].Open + 10.0;
            data[4].Close = data[4].Open + 10.0;
            data[5].Close = data[5].Open + 10.0; // 3 in a row
            data[6].Close = data[6].Open + 10.0; // 4 in a row
            data[7].Close = data[7].Open - 10.0;
            data[8].Close = data[8].Open + 10.0;
            data[9].Close = data[9].Open + 10.0;
            data[10].Close = data[10].Open + 10.0; // 3 in a row

            var c3InARow = new Condition();
            c3InARow.AndConditions.Add(new IsSuccessiveGreenCandlesCondition(3));

            Assert.AreEqual(false, c3InARow.IsMet(data, new DateTime(2024, 01, 01, 00, 00, 00)));
            Assert.AreEqual(false, c3InARow.IsMet(data, new DateTime(2024, 01, 01, 01, 00, 00)));
            Assert.AreEqual(false, c3InARow.IsMet(data, new DateTime(2024, 01, 01, 02, 00, 00)));
            Assert.AreEqual(false, c3InARow.IsMet(data, new DateTime(2024, 01, 01, 03, 00, 00)));
            Assert.AreEqual(false, c3InARow.IsMet(data, new DateTime(2024, 01, 01, 04, 00, 00)));
            Assert.AreEqual(true, c3InARow.IsMet(data, new DateTime(2024, 01, 01, 05, 00, 00)));
            Assert.AreEqual(true, c3InARow.IsMet(data, new DateTime(2024, 01, 01, 06, 00, 00)));
            Assert.AreEqual(false, c3InARow.IsMet(data, new DateTime(2024, 01, 01, 07, 00, 00)));
            Assert.AreEqual(false, c3InARow.IsMet(data, new DateTime(2024, 01, 01, 08, 00, 00)));
            Assert.AreEqual(false, c3InARow.IsMet(data, new DateTime(2024, 01, 01, 09, 00, 00)));
            Assert.AreEqual(true, c3InARow.IsMet(data, new DateTime(2024, 01, 01, 10, 00, 00)));

            var c4InARow = new Condition();
            c4InARow.AndConditions.Add(new IsSuccessiveGreenCandlesCondition(4));

            Assert.AreEqual(false, c4InARow.IsMet(data, new DateTime(2024, 01, 01, 00, 00, 00)));
            Assert.AreEqual(false, c4InARow.IsMet(data, new DateTime(2024, 01, 01, 01, 00, 00)));
            Assert.AreEqual(false, c4InARow.IsMet(data, new DateTime(2024, 01, 01, 02, 00, 00)));
            Assert.AreEqual(false, c4InARow.IsMet(data, new DateTime(2024, 01, 01, 03, 00, 00)));
            Assert.AreEqual(false, c4InARow.IsMet(data, new DateTime(2024, 01, 01, 04, 00, 00)));
            Assert.AreEqual(false, c4InARow.IsMet(data, new DateTime(2024, 01, 01, 05, 00, 00)));
            Assert.AreEqual(true, c4InARow.IsMet(data, new DateTime(2024, 01, 01, 06, 00, 00)));
            Assert.AreEqual(false, c4InARow.IsMet(data, new DateTime(2024, 01, 01, 07, 00, 00)));
            Assert.AreEqual(false, c4InARow.IsMet(data, new DateTime(2024, 01, 01, 08, 00, 00)));
            Assert.AreEqual(false, c4InARow.IsMet(data, new DateTime(2024, 01, 01, 09, 00, 00)));
            Assert.AreEqual(false, c4InARow.IsMet(data, new DateTime(2024, 01, 01, 10, 00, 00)));
        }
    }
}