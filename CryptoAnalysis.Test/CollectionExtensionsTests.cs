namespace Gradient.CryptoAnalysis.Tests
{
    [TestClass]
    public class CollectionExtensionsTest
    {
        private List<int> _list = new List<int>()
        {
            1, 2, 3, 4, 5, 6,
        };

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(1, 6)]
        [DataRow(2, 5, 6)]
        [DataRow(3, 4, 5, 6)]
        [DataRow(4, 3, 4, 5, 6)]
        [DataRow(5, 2, 3, 4, 5, 6)]
        [DataRow(6, 1, 2, 3, 4, 5, 6)]
        [DataRow(7, 1, 2, 3, 4, 5, 6)]
        public void TestTakeLast(int takeLast, params int[] expected)
        {
            CollectionAssert.AreEqual(expected, _list.TakeLast(takeLast).ToList());
        }
    }
}