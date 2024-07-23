using CryptoAnalysis.Conditions.DateConditions;

namespace Gradient.CryptoAnalysis.Tests
{
    [TestClass]
    public class IsWeekendConditionTests
    {
        [DataTestMethod]
        [DataRow(1, false)]
        [DataRow(2, false)]
        [DataRow(3, false)]
        [DataRow(4, false)]
        [DataRow(5, false)]
        [DataRow(6, true)]
        [DataRow(7, true)]
        public void TestIsWeekendCondition(int day, bool expected)
        {
            var c1 = new Condition();
            c1.AndConditions.Add(new IsWeekendCondition());

            Assert.AreEqual(expected, c1.IsMet(null, new DateTime(2024, 01, day)));
        }
    }
}