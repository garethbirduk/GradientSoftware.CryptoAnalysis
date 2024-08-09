namespace Gradient.CryptoAnalysis.Tests
{
    [TestClass]
    public class IsConfirmedBreakOfStructureConditionTests
    {
        [TestMethod]
        public void TestIsConfirmedBreakOfStructureCondition()
        {
            var data = TestData.SwingsPrices;
            var swings = data.ToSwings();

            var condition = new Condition();
            condition.AndConditions.Add(new IsBreakOfStructureCondition(data.Count()));

            foreach (var item in data.Where(x => x.Close != 20))
            {
                Assert.AreEqual(false, condition.IsMet(data, item.DateTime));
            }

            Assert.AreEqual(false, condition.IsMet(data, data.Where(x => x.Close == 20).ToList()[0].DateTime));
            Assert.AreEqual(false, condition.IsMet(data, data.Where(x => x.Close == 20).ToList()[1].DateTime));
            Assert.AreEqual(true, condition.IsMet(data, data.Where(x => x.Close == 20).ToList()[2].DateTime));
        }
    }
}