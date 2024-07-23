namespace Gradient.CryptoAnalysis.Tests
{
    [TestClass]
    public class IsAfterDateConditionTests
    {
        [DataTestMethod]
        [DataRow(1, false)]
        [DataRow(2, false)]
        [DataRow(3, false)]
        [DataRow(4, true)]
        [DataRow(5, true)]
        [DataRow(6, true)]
        [DataRow(7, true)]
        public void TestIsAfterDateCondition(int day, bool expected)
        {
            var c1 = new Condition();
            c1.AndConditions.Add(new IsAfterDateCondition(new DateTime(2024, 01, 03)));

            Assert.AreEqual(expected, c1.IsMet(null, new DateTime(2024, 01, day)));
        }
    }
}