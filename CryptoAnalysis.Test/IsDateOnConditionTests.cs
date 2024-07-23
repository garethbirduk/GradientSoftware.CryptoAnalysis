using CryptoAnalysis.Conditions.DateConditions;

namespace Gradient.CryptoAnalysis.Tests
{
    [TestClass]
    public class IsDateOnConditionTests
    {
        [DataTestMethod]
        [DataRow(1, false)]
        [DataRow(2, true)]
        [DataRow(3, false)]
        [DataRow(4, false)]
        [DataRow(5, false)]
        [DataRow(6, false)]
        [DataRow(7, false)]
        public void TestIsDateOnAndBeforeConditions(int day, bool expected)
        {
            var c1 = new Condition();
            c1.AndConditions.Add(new IsDateOnCondition(new DateTime(2024, 01, 02), new DateTime(2024, 01, 05)));
            c1.AndConditions.Add(new IsBeforeDateCondition(new DateTime(2024, 01, 03)));

            Assert.AreEqual(expected, c1.IsMet(null, new DateTime(2024, 01, day)));
        }

        [DataTestMethod]
        [DataRow(1, false)]
        [DataRow(2, true)]
        [DataRow(3, false)]
        [DataRow(4, false)]
        [DataRow(5, true)]
        [DataRow(6, false)]
        [DataRow(7, false)]
        public void TestIsDateOnCondition_And(int day, bool expected)
        {
            var c1 = new Condition();
            c1.AndConditions.Add(new IsDateOnCondition(new DateTime(2024, 01, 02), new DateTime(2024, 01, 05)));

            Assert.AreEqual(expected, c1.IsMet(null, new DateTime(2024, 01, day)));
        }

        [DataTestMethod]
        [DataRow(1, false)]
        [DataRow(2, true)]
        [DataRow(3, false)]
        [DataRow(4, false)]
        [DataRow(5, true)]
        [DataRow(6, false)]
        [DataRow(7, false)]
        public void TestIsDateOnCondition_Or(int day, bool expected)
        {
            var c1 = new Condition();
            c1.OrConditions.Add(new IsDateOnCondition(new DateTime(2024, 01, 02), new DateTime(2024, 01, 05)));

            Assert.AreEqual(expected, c1.IsMet(null, new DateTime(2024, 01, day)));
        }

        [DataTestMethod]
        [DataRow(1, false)]
        [DataRow(2, true)]
        [DataRow(3, false)]
        [DataRow(4, false)]
        [DataRow(5, true)]
        [DataRow(6, false)]
        [DataRow(7, true)]
        public void TestIsDateOnOrAfterConditions(int day, bool expected)
        {
            var c1 = new Condition();
            c1.OrConditions.Add(new IsDateOnCondition(new DateTime(2024, 01, 02), new DateTime(2024, 01, 05)));
            c1.OrConditions.Add(new IsAfterDateCondition(new DateTime(2024, 01, 06)));

            Assert.AreEqual(expected, c1.IsMet(null, new DateTime(2024, 01, day)));
        }
    }
}