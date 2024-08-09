﻿namespace Gradient.CryptoAnalysis.Tests
{
    public static class TestData
    {
        public static readonly List<Price> SwingsPrices = new List<Price>
        {
            new Price { Close = 10, DateTime = new DateTime(2010, 01, 01).AddDays(-62) },
            new Price { Close = 14, DateTime = new DateTime(2010, 01, 01).AddDays(-61) },
            new Price { Close = 18, DateTime = new DateTime(2010, 01, 01).AddDays(-60) },
            new Price { Close = 20, DateTime = new DateTime(2010, 01, 01).AddDays(-59) },
            new Price { Close = 16, DateTime = new DateTime(2010, 01, 01).AddDays(-58) },
            new Price { Close = 12, DateTime = new DateTime(2010, 01, 01).AddDays(-57) },
            new Price { Close = 15, DateTime = new DateTime(2010, 01, 01).AddDays(-56) },
            new Price { Close = 12, DateTime = new DateTime(2010, 01, 01).AddDays(-55) },
            new Price { Close = 17, DateTime = new DateTime(2010, 01, 01).AddDays(-54) },
            new Price { Close = 18, DateTime = new DateTime(2010, 01, 01).AddDays(-53) },
            new Price { Close = 21, DateTime = new DateTime(2010, 01, 01).AddDays(-52) },
            new Price { Close = 22, DateTime = new DateTime(2010, 01, 01).AddDays(-51) },
            new Price { Close = 26, DateTime = new DateTime(2010, 01, 01).AddDays(-50) },
            new Price { Close = 21, DateTime = new DateTime(2010, 01, 01).AddDays(-49) },
            new Price { Close = 19, DateTime = new DateTime(2010, 01, 01).AddDays(-48) },
            new Price { Close = 15, DateTime = new DateTime(2010, 01, 01).AddDays(-47) },
            new Price { Close = 30, DateTime = new DateTime(2010, 01, 01).AddDays(-46) },
            new Price { Close = 25, DateTime = new DateTime(2010, 01, 01).AddDays(-45) },
            new Price { Close = 19, DateTime = new DateTime(2010, 01, 01).AddDays(-44) },
            new Price { Close = 24, DateTime = new DateTime(2010, 01, 01).AddDays(-43) },
            new Price { Close = 26, DateTime = new DateTime(2010, 01, 01).AddDays(-42) },
            new Price { Close = 22, DateTime = new DateTime(2010, 01, 01).AddDays(-41) },
            new Price { Close = 29, DateTime = new DateTime(2010, 01, 01).AddDays(-40) },
            new Price { Close = 27, DateTime = new DateTime(2010, 01, 01).AddDays(-39) },
            new Price { Close = 28, DateTime = new DateTime(2010, 01, 01).AddDays(-38) },
            new Price { Close = 30, DateTime = new DateTime(2010, 01, 01).AddDays(-37) },
            new Price { Close = 29, DateTime = new DateTime(2010, 01, 01).AddDays(-36) },
            new Price { Close = 28, DateTime = new DateTime(2010, 01, 01).AddDays(-35) },
            new Price { Close = 16, DateTime = new DateTime(2010, 01, 01).AddDays(-34) },
            new Price { Close = 19, DateTime = new DateTime(2010, 01, 01).AddDays(-33) },
            new Price { Close = 17, DateTime = new DateTime(2010, 01, 01).AddDays(-32) },
            new Price { Close = 21, DateTime = new DateTime(2010, 01, 01).AddDays(-31) },
            new Price { Close = 19, DateTime = new DateTime(2010, 01, 01).AddDays(-30) },
            new Price { Close = 18, DateTime = new DateTime(2010, 01, 01).AddDays(-29) },
            new Price { Close = 17, DateTime = new DateTime(2010, 01, 01).AddDays(-28) },
            new Price { Close = 22, DateTime = new DateTime(2010, 01, 01).AddDays(-27) },
            new Price { Close = 21, DateTime = new DateTime(2010, 01, 01).AddDays(-26) },
            new Price { Close = 19, DateTime = new DateTime(2010, 01, 01).AddDays(-25) },
            new Price { Close = 20, DateTime = new DateTime(2010, 01, 01).AddDays(-24) },
            new Price { Close = 21, DateTime = new DateTime(2010, 01, 01).AddDays(-23) },
            new Price { Close = 19, DateTime = new DateTime(2010, 01, 01).AddDays(-22) },
            new Price { Close = 24, DateTime = new DateTime(2010, 01, 01).AddDays(-21) },
            new Price { Close = 31, DateTime = new DateTime(2010, 01, 01).AddDays(-20) },
            new Price { Close = 33, DateTime = new DateTime(2010, 01, 01).AddDays(-19) },
            new Price { Close = 34, DateTime = new DateTime(2010, 01, 01).AddDays(-18) },
            new Price { Close = 30, DateTime = new DateTime(2010, 01, 01).AddDays(-17) },
            new Price { Close = 31, DateTime = new DateTime(2010, 01, 01).AddDays(-16) },
            new Price { Close = 29, DateTime = new DateTime(2010, 01, 01).AddDays(-15) },
            new Price { Close = 30, DateTime = new DateTime(2010, 01, 01).AddDays(-14) },
            new Price { Close = 28, DateTime = new DateTime(2010, 01, 01).AddDays(-13) },
            new Price { Close = 26, DateTime = new DateTime(2010, 01, 01).AddDays(-12) },
            new Price { Close = 28, DateTime = new DateTime(2010, 01, 01).AddDays(-11) },
            new Price { Close = 29, DateTime = new DateTime(2010, 01, 01).AddDays(-10) },
            new Price { Close = 30, DateTime = new DateTime(2010, 01, 01).AddDays(-9) },
            new Price { Close = 31, DateTime = new DateTime(2010, 01, 01).AddDays(-8) },
            new Price { Close = 30, DateTime = new DateTime(2010, 01, 01).AddDays(-7) },
            new Price { Close = 35, DateTime = new DateTime(2010, 01, 01).AddDays(-6) },
            new Price { Close = 40, DateTime = new DateTime(2010, 01, 01).AddDays(-5) },
            new Price { Close = 38, DateTime = new DateTime(2010, 01, 01).AddDays(-4) },
            new Price { Close = 30, DateTime = new DateTime(2010, 01, 01).AddDays(-3) },
            new Price { Close = 20, DateTime = new DateTime(2010, 01, 01).AddDays(-2) },
            new Price { Close = 36, DateTime = new DateTime(2010, 01, 01).AddDays(-1) },
            new Price { Close = 50, DateTime = new DateTime(2010, 01, 01) }
        };
    }

    [TestClass]
    public class SwingsTests
    {
        [TestMethod]
        public void TestFindBreaksOfStructures()
        {
            // Act
            var actual = TestData.SwingsPrices.ToSwings();

            // Assert
            Assert.AreEqual(6, actual.Count);

            Assert.AreEqual(14, actual[0].ConfirmedBreakOfStructure.Close);
            Assert.IsNull(actual[0].ConfirmedMarketStructureBreak);

            Assert.AreEqual(21, actual[1].ConfirmedBreakOfStructure.Close);
            Assert.IsNull(actual[1].ConfirmedMarketStructureBreak);

            Assert.AreEqual(30, actual[2].ConfirmedBreakOfStructure.Close);
            Assert.IsNull(actual[2].ConfirmedMarketStructureBreak);

            Assert.AreEqual(31, actual[3].ConfirmedBreakOfStructure.Close);
            Assert.IsNull(actual[3].ConfirmedMarketStructureBreak);

            Assert.AreEqual(35, actual[4].ConfirmedBreakOfStructure.Close);
            Assert.IsNull(actual[4].ConfirmedMarketStructureBreak);

            Assert.IsNull(actual[5].ConfirmedBreakOfStructure);
            Assert.AreEqual(20, actual[5].ConfirmedMarketStructureBreak.Close);

            var swing2 = actual[2];
            Assert.AreEqual(2, swing2.InterimSwings.Count);
        }
    }
}