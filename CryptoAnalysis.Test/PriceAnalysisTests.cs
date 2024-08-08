namespace Gradient.CryptoAnalysis.Tests
{
    [TestClass]
    public class PriceAnalysisTests
    {
        private readonly List<Price> _prices = new List<Price>
        {
            new Price { Close = 10, DateTime = DateTime.Now.AddDays(-62) },
            new Price { Close = 14, DateTime = DateTime.Now.AddDays(-61) },
            new Price { Close = 18, DateTime = DateTime.Now.AddDays(-60) },
            new Price { Close = 20, DateTime = DateTime.Now.AddDays(-59) },
            new Price { Close = 16, DateTime = DateTime.Now.AddDays(-58) },
            new Price { Close = 12, DateTime = DateTime.Now.AddDays(-57) },
            new Price { Close = 15, DateTime = DateTime.Now.AddDays(-56) },
            new Price { Close = 12, DateTime = DateTime.Now.AddDays(-55) },
            new Price { Close = 17, DateTime = DateTime.Now.AddDays(-54) },
            new Price { Close = 18, DateTime = DateTime.Now.AddDays(-53) },
            new Price { Close = 21, DateTime = DateTime.Now.AddDays(-52) },
            new Price { Close = 22, DateTime = DateTime.Now.AddDays(-51) },
            new Price { Close = 26, DateTime = DateTime.Now.AddDays(-50) },
            new Price { Close = 21, DateTime = DateTime.Now.AddDays(-49) },
            new Price { Close = 19, DateTime = DateTime.Now.AddDays(-48) },
            new Price { Close = 15, DateTime = DateTime.Now.AddDays(-47) },
            new Price { Close = 30, DateTime = DateTime.Now.AddDays(-46) },
            new Price { Close = 25, DateTime = DateTime.Now.AddDays(-45) },
            new Price { Close = 19, DateTime = DateTime.Now.AddDays(-44) },
            new Price { Close = 24, DateTime = DateTime.Now.AddDays(-43) },
            new Price { Close = 26, DateTime = DateTime.Now.AddDays(-42) },
            new Price { Close = 22, DateTime = DateTime.Now.AddDays(-41) },
            new Price { Close = 29, DateTime = DateTime.Now.AddDays(-40) },
            new Price { Close = 27, DateTime = DateTime.Now.AddDays(-39) },
            new Price { Close = 28, DateTime = DateTime.Now.AddDays(-38) },
            new Price { Close = 30, DateTime = DateTime.Now.AddDays(-37) },
            new Price { Close = 29, DateTime = DateTime.Now.AddDays(-36) },
            new Price { Close = 28, DateTime = DateTime.Now.AddDays(-35) },
            new Price { Close = 16, DateTime = DateTime.Now.AddDays(-34) },
            new Price { Close = 19, DateTime = DateTime.Now.AddDays(-33) },
            new Price { Close = 17, DateTime = DateTime.Now.AddDays(-32) },
            new Price { Close = 21, DateTime = DateTime.Now.AddDays(-31) },
            new Price { Close = 19, DateTime = DateTime.Now.AddDays(-30) },
            new Price { Close = 18, DateTime = DateTime.Now.AddDays(-29) },
            new Price { Close = 17, DateTime = DateTime.Now.AddDays(-28) },
            new Price { Close = 22, DateTime = DateTime.Now.AddDays(-27) },
            new Price { Close = 21, DateTime = DateTime.Now.AddDays(-26) },
            new Price { Close = 19, DateTime = DateTime.Now.AddDays(-25) },
            new Price { Close = 20, DateTime = DateTime.Now.AddDays(-24) },
            new Price { Close = 21, DateTime = DateTime.Now.AddDays(-23) },
            new Price { Close = 19, DateTime = DateTime.Now.AddDays(-22) },
            new Price { Close = 24, DateTime = DateTime.Now.AddDays(-21) },
            new Price { Close = 31, DateTime = DateTime.Now.AddDays(-20) },
            new Price { Close = 33, DateTime = DateTime.Now.AddDays(-19) },
            new Price { Close = 34, DateTime = DateTime.Now.AddDays(-18) },
            new Price { Close = 30, DateTime = DateTime.Now.AddDays(-17) },
            new Price { Close = 31, DateTime = DateTime.Now.AddDays(-16) },
            new Price { Close = 29, DateTime = DateTime.Now.AddDays(-15) },
            new Price { Close = 30, DateTime = DateTime.Now.AddDays(-14) },
            new Price { Close = 28, DateTime = DateTime.Now.AddDays(-13) },
            new Price { Close = 26, DateTime = DateTime.Now.AddDays(-12) },
            new Price { Close = 28, DateTime = DateTime.Now.AddDays(-11) },
            new Price { Close = 29, DateTime = DateTime.Now.AddDays(-10) },
            new Price { Close = 30, DateTime = DateTime.Now.AddDays(-9) },
            new Price { Close = 31, DateTime = DateTime.Now.AddDays(-8) },
            new Price { Close = 30, DateTime = DateTime.Now.AddDays(-7) },
            new Price { Close = 35, DateTime = DateTime.Now.AddDays(-6) },
            new Price { Close = 40, DateTime = DateTime.Now.AddDays(-5) },
            new Price { Close = 38, DateTime = DateTime.Now.AddDays(-4) },
            new Price { Close = 30, DateTime = DateTime.Now.AddDays(-3) },
            new Price { Close = 20, DateTime = DateTime.Now.AddDays(-2) },
            new Price { Close = 36, DateTime = DateTime.Now.AddDays(-1) },
            new Price { Close = 50, DateTime = DateTime.Now }
        };

        [TestMethod]
        public void TestFindBreaksOfStructures()
        {
            // Act
            var actual = _prices.ToSwings();

            // Assert
            Assert.AreEqual(6, actual.Count);

            var subBos = actual[2];
            Assert.AreEqual(3, subBos.Swings.Count);
        }
    }
}