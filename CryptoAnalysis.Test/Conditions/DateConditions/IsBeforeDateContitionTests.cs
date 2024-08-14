﻿namespace Gradient.CryptoAnalysis.Test.Conditions.DateConditions
{
    [TestClass]
    public class IsBeforeDateContitionTests
    {
        [DataTestMethod]
        [DataRow(1, true)]
        [DataRow(2, true)]
        [DataRow(3, false)]
        [DataRow(4, false)]
        [DataRow(5, false)]
        [DataRow(6, false)]
        [DataRow(7, false)]
        public void TestIsAfterDateCondition(int day, bool expected)
        {
            var c1 = new ConditionSet();
            c1.AndConditions.Add(new IsBeforeDateCondition(new DateTime(2024, 01, 03)));

            Assert.AreEqual(expected, c1.IsMet(null, new DateTime(2024, 01, day)));
        }
    }
}