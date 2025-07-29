//using CryptoAnalysis.Csv.ClassMaps;
//using Gradient.CryptoAnalysis.Csv;
//using Plotly.NET;

//namespace Gradient.CryptoAnalysis.Test.PriceExtensions;

//[TestClass]
//public class ToHighHighsTests
//{
//    private List<Price> _prices;
//    public static readonly string _cryptoDataFilePath = Path.Combine("TestData", "PricesExtensionsData", "ToHighHighsTests", "TestData.csv");

//    [TestInitialize]
//    public void TestInitialize()
//    {
//        _prices = new CsvReaderHelper().ReadData<Price, PriceClassMap>(_cryptoDataFilePath).ToList();
//    }

//    [TestMethod]
//    public void TestToHighHighs_Ok()
//    {
//        var Highs = _prices.HigherHighs();

//        ChartGenerator.CreateChart()
//            .AddLayers(new Layer
//            {
//                Name = "base",
//                ChartFactory = () => ChartGenerator.GenerateLineChart(
//                    _prices,
//                    p => (decimal)p.Close,
//                    "Close Prices"
//                ),
//                Color = Color.fromString("blue"),
//                LineWidth = 5,
//            })
//            .SaveHtml(Path.Combine("c:\\", "temp", "prices"));

//        Assert.AreEqual(10, Highs.Count);

//        Assert.AreEqual(9, Highs[0].Count);
//        Assert.AreEqual(4, Highs[1].Count);
//        Assert.AreEqual(3, Highs[2].Count);

//        Assert.AreEqual(2, Highs[3].Count);

//        Assert.AreEqual(2, Highs[4].Count);
//        Assert.AreEqual(8, Highs[5].Count);

//        Assert.AreEqual(2, Highs[6].Count);
//        Assert.AreEqual(3, Highs[7].Count);
//        Assert.AreEqual(5, Highs[8].Count);
//        Assert.AreEqual(4, Highs[9].Count);
//    }

//    [TestMethod]
//    public void TestToHighs_Empty()
//    {
//        CollectionAssert.AreEqual(new List<List<Price>>(), new List<Price>().ToHighHighsUsingHighs());
//    }
//}