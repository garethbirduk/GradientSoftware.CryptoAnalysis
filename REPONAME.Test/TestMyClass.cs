namespace REPONAME.Test
{
    [TestClass]
    public class TestMyClass
    {
        [TestMethod]
        public void TestAdd()
        {
            Assert.AreEqual(2, MyClass.Add(1, 1));
        }
    }
}