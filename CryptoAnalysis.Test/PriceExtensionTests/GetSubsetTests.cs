using Gradient.CryptoAnalysis;

namespace CryptoAnalysis.Test.PriceExtensionTests
{
    [TestClass]
    public class GetSubsetTests
    {
        private List<Price> _data = new List<Price>
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

        [TestMethod]
        public void TestGetSubsetByCountFromExclusive()
        {
            var result = _data.CreateSubsetByCount(_data[1], 5);
            Assert.AreEqual(5, result.Count());
            Assert.AreEqual(_data[1], result[0]);
            Assert.AreEqual(_data[2], result[1]);
            Assert.AreEqual(_data[3], result[2]);
            Assert.AreEqual(_data[4], result[3]);
            Assert.AreEqual(_data[5], result[4]);
        }

        [TestMethod]
        public void TestGetSubsetByCountFromInclusive()
        {
            var result = _data.CreateSubsetByCount(_data[1], 5, true);
            Assert.AreEqual(6, result.Count());
            Assert.AreEqual(_data[1], result[0]);
            Assert.AreEqual(_data[2], result[1]);
            Assert.AreEqual(_data[3], result[2]);
            Assert.AreEqual(_data[4], result[3]);
            Assert.AreEqual(_data[5], result[4]);
            Assert.AreEqual(_data[6], result[5]);
        }

        [TestMethod]
        public void TestGetSubsetByCountFromInclusive_NotEnough()
        {
            var result = _data.CreateSubsetByCount(_data[1], 100, true);
            Assert.AreEqual(9, result.Count());
            Assert.AreEqual(_data[1], result[0]);
            Assert.AreEqual(_data[2], result[1]);
            Assert.AreEqual(_data[3], result[2]);
            Assert.AreEqual(_data[4], result[3]);
            Assert.AreEqual(_data[5], result[4]);
            Assert.AreEqual(_data[6], result[5]);
            Assert.AreEqual(_data[7], result[6]);
            Assert.AreEqual(_data[8], result[7]);
            Assert.AreEqual(_data[9], result[8]);
        }

        [TestMethod]
        public void TestGetSubsetByIndexFromExclusive()
        {
            var result = _data.CreateSubsetByIndex(_data[1], 5);
            Assert.AreEqual(4, result.Count());
            Assert.AreEqual(_data[1], result[0]);
            Assert.AreEqual(_data[2], result[1]);
            Assert.AreEqual(_data[3], result[2]);
            Assert.AreEqual(_data[4], result[3]);
        }

        [TestMethod]
        public void TestGetSubsetByIndexFromInclusive()
        {
            var result = _data.CreateSubsetByIndex(_data[1], 5, true);
            Assert.AreEqual(5, result.Count());
            Assert.AreEqual(_data[1], result[0]);
            Assert.AreEqual(_data[2], result[1]);
            Assert.AreEqual(_data[3], result[2]);
            Assert.AreEqual(_data[4], result[3]);
            Assert.AreEqual(_data[5], result[4]);
        }

        [TestMethod]
        public void TestGetSubsetByIndexToExclusive()
        {
            var result = _data.CreateSubsetByIndex(0, _data[4]);
            Assert.AreEqual(4, result.Count());
            Assert.AreEqual(_data[0], result[0]);
            Assert.AreEqual(_data[1], result[1]);
            Assert.AreEqual(_data[2], result[2]);
            Assert.AreEqual(_data[3], result[3]);
        }

        [TestMethod]
        public void TestGetSubsetByIndexToInclusive()
        {
            var result = _data.CreateSubsetByIndex(0, _data[4], true);
            Assert.AreEqual(5, result.Count());
            Assert.AreEqual(_data[0], result[0]);
            Assert.AreEqual(_data[1], result[1]);
            Assert.AreEqual(_data[2], result[2]);
            Assert.AreEqual(_data[3], result[3]);
            Assert.AreEqual(_data[4], result[4]);
        }

        [TestMethod]
        public void TestGetSubsetExclusive()
        {
            var result = _data.CreateSubset(_data[1], _data[5]);
            Assert.AreEqual(4, result.Count());
            Assert.AreEqual(_data[1], result[0]);
            Assert.AreEqual(_data[2], result[1]);
            Assert.AreEqual(_data[3], result[2]);
            Assert.AreEqual(_data[4], result[3]);
        }

        [TestMethod]
        public void TestGetSubsetInclusive()
        {
            var result = _data.CreateSubset(_data[1], _data[5], true);
            Assert.AreEqual(5, result.Count());
            Assert.AreEqual(_data[1], result[0]);
            Assert.AreEqual(_data[2], result[1]);
            Assert.AreEqual(_data[3], result[2]);
            Assert.AreEqual(_data[4], result[3]);
            Assert.AreEqual(_data[5], result[4]);
        }
    }
}