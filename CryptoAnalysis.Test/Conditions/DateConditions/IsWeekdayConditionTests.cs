namespace Gradient.CryptoAnalysis.Tests
{
    [TestClass]
    public class IsWeekdayConditionTests
    {
        [DataTestMethod]
        [DataRow(1, true)]
        [DataRow(2, true)]
        [DataRow(3, true)]
        [DataRow(4, true)]
        [DataRow(5, true)]
        [DataRow(6, false)]
        [DataRow(7, false)]
        public void TestIsWeekdayCondition(int day, bool expected)
        {
            var c1 = new ConditionSet();
            c1.AndConditions.Add(new IsWeekdayCondition());

            Assert.AreEqual(expected, c1.IsMet(null, new DateTime(2024, 01, day)));
        }
    }
}