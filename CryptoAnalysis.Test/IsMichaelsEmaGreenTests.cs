using CryptoAnalysis.Conditions.PriceConditions.IndicatorConditions;

namespace Gradient.CryptoAnalysis.Tests
{
    [TestClass]
    public class IsMichaelsEmaChangedGreenToRedTests
    {
        [TestMethod]
        public void TestIsMichaelsEmaChangedGreenToRed()
        {
            var data = TestHelper.CreatePriceData(new DateTime(2024, 05, 05), 60, 4);

            data[0].Indicators.MichaelsEma = new MichaelsEma { EMABig = 100, EMASmall = 50 };
            data[1].Indicators.MichaelsEma = new MichaelsEma { EMABig = 150, EMASmall = 120 };
            data[2].Indicators.MichaelsEma = new MichaelsEma { EMABig = 120, EMASmall = 150 };
            data[3].Indicators.MichaelsEma = new MichaelsEma { EMABig = 100, EMASmall = 150 };

            var condition = new Condition();
            condition.AndConditions.Add(new IsMichaelsEmaChangedGreenToRedCondition());

            Assert.IsFalse(condition.IsMet(data, new DateTime(2024, 05, 05, 00, 00, 00)));
            Assert.IsFalse(condition.IsMet(data, new DateTime(2024, 05, 05, 01, 00, 00)));
            Assert.IsTrue(condition.IsMet(data, new DateTime(2024, 05, 05, 02, 00, 00)));
            Assert.IsFalse(condition.IsMet(data, new DateTime(2024, 05, 05, 03, 00, 00)));
        }
    }

    [TestClass]
    public class IsMichaelsEmaChangedRedToGreenTests
    {
        [TestMethod]
        public void TestIsMichaelsEmaChangedRedToGreen()
        {
            var data = TestHelper.CreatePriceData(new DateTime(2024, 05, 05), 60, 5);

            data[0].Indicators.MichaelsEma = new MichaelsEma { EMABig = 100, EMASmall = 50 };
            data[1].Indicators.MichaelsEma = new MichaelsEma { EMABig = 150, EMASmall = 120 };
            data[2].Indicators.MichaelsEma = new MichaelsEma { EMABig = 120, EMASmall = 150 };
            data[3].Indicators.MichaelsEma = new MichaelsEma { EMABig = 100, EMASmall = 150 };
            data[4].Indicators.MichaelsEma = new MichaelsEma { EMABig = 200, EMASmall = 150 };

            var condition = new Condition();
            condition.AndConditions.Add(new IsMichaelsEmaChangedRedToGreenCondition());

            Assert.IsFalse(condition.IsMet(data, new DateTime(2024, 05, 05, 00, 00, 00)));
            Assert.IsFalse(condition.IsMet(data, new DateTime(2024, 05, 05, 01, 00, 00)));
            Assert.IsFalse(condition.IsMet(data, new DateTime(2024, 05, 05, 02, 00, 00)));
            Assert.IsFalse(condition.IsMet(data, new DateTime(2024, 05, 05, 03, 00, 00)));
            Assert.IsTrue(condition.IsMet(data, new DateTime(2024, 05, 05, 04, 00, 00)));
        }
    }

    [TestClass]
    public class IsMichaelsEmaGreenTests
    {
        [TestMethod]
        public void TestIsMichaelsEmaGreen()
        {
            var data = TestHelper.CreatePriceData(new DateTime(2024, 05, 05), 60, 4);

            data[0].Indicators.MichaelsEma = new MichaelsEma { EMABig = 100, EMASmall = 50 };
            data[1].Indicators.MichaelsEma = new MichaelsEma { EMABig = 150, EMASmall = 120 };
            data[2].Indicators.MichaelsEma = new MichaelsEma { EMABig = 120, EMASmall = 150 };
            data[3].Indicators.MichaelsEma = new MichaelsEma { EMABig = 100, EMASmall = 150 };

            var condition = new Condition();
            condition.AndConditions.Add(new IsMichaelsEmaGreenCondition());

            Assert.IsTrue(condition.IsMet(data, new DateTime(2024, 05, 05, 00, 00, 00)));
            Assert.IsTrue(condition.IsMet(data, new DateTime(2024, 05, 05, 01, 00, 00)));
            Assert.IsFalse(condition.IsMet(data, new DateTime(2024, 05, 05, 02, 00, 00)));
            Assert.IsFalse(condition.IsMet(data, new DateTime(2024, 05, 05, 03, 00, 00)));
        }
    }

    [TestClass]
    public class IsMichaelsEmaRedTests
    {
        [TestMethod]
        public void TestIsMichaelsEmaRed()
        {
            var data = TestHelper.CreatePriceData(new DateTime(2024, 05, 05), 60, 4);

            data[0].Indicators.MichaelsEma = new MichaelsEma { EMABig = 100, EMASmall = 50 };
            data[1].Indicators.MichaelsEma = new MichaelsEma { EMABig = 150, EMASmall = 120 };
            data[2].Indicators.MichaelsEma = new MichaelsEma { EMABig = 120, EMASmall = 150 };
            data[3].Indicators.MichaelsEma = new MichaelsEma { EMABig = 100, EMASmall = 150 };

            var condition = new Condition();
            condition.AndConditions.Add(new IsMichaelsEmaRedCondition());

            Assert.IsFalse(condition.IsMet(data, new DateTime(2024, 05, 05, 00, 00, 00)));
            Assert.IsFalse(condition.IsMet(data, new DateTime(2024, 05, 05, 01, 00, 00)));
            Assert.IsTrue(condition.IsMet(data, new DateTime(2024, 05, 05, 02, 00, 00)));
            Assert.IsTrue(condition.IsMet(data, new DateTime(2024, 05, 05, 03, 00, 00)));
        }
    }
}