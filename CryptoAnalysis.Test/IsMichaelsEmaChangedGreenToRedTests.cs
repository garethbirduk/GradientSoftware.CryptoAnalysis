﻿namespace Gradient.CryptoAnalysis.Tests
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
            data[3].Indicators.MichaelsEma = new MichaelsEma { EMABig = 100, EMASmall = 50 };

            var condition = new Condition();
            condition.AndConditions.Add(new IsMichaelsEmaChangedGreenToRedCondition());

            Assert.IsFalse(condition.IsMet(data, new DateTime(2024, 05, 05, 00, 00, 00))); // r to r
            Assert.IsFalse(condition.IsMet(data, new DateTime(2024, 05, 05, 01, 00, 00))); // r to r
            Assert.IsFalse(condition.IsMet(data, new DateTime(2024, 05, 05, 02, 00, 00))); // r to g
            Assert.IsTrue(condition.IsMet(data, new DateTime(2024, 05, 05, 03, 00, 00))); // g to r
        }
    }
}