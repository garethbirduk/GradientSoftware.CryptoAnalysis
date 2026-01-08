//using CryptoAnalysis.Csv.ClassMaps;
//using Gradient.CryptoAnalysis.Csv;

//namespace Gradient.CryptoAnalysis.Test.PriceExtensions;

//[TestClass]
//public class ToUpwardBreakoutsTests : PricesTests
//{
//    public override string TestDirectory => Path.Combine("PricesExtensionsData", "ToUpwardBreakoutsTests");

//    [DataTestMethod]
//    [DataRow("ToUpwardBreakoutsTests_LineCloses", false, true)]
//    public void ToUpwardBreakoutsTests_1(string name, bool candlestick, bool lineCloses)
//    {
//        var upswings = Prices.ToUpswings(EnumCloseType.Close, true);
//        var upwardBreakouts = Prices.ToUpwardBreakouts(EnumCloseType.Close, true);
//        var chart = ChartGenerator.CreatePriceChart(Prices, candlestick: candlestick, lineCloses: lineCloses, lineWidth: 1);
//        chart = chart
//            .WithHigherHighs(upswings, EnumCloseType.Close)
//            .WithHigherLows(upswings, EnumCloseType.Close)
//            .WithUpswings(upswings, EnumCloseType.Close, lineWidth: 2, color: "cyan")
//            .WithBreaksOfStructureMarkers(upswings, EnumCloseType.Close, lineWidth: 3, color: "yellow", markerSize: 6)
//            .WithMarketStructureBreaksMarkers(upswings, EnumCloseType.Close, lineWidth: 3, color: "orange", markerSize: 6)
//            .WithUpwardBreakouts(upwardBreakouts, EnumCloseType.Close)
//            ;

//        AssertChart(name, chart);
//    }

//    [DataTestMethod]
//    [DataRow("ToUpwardBreakoutsTests_LineCloses_Full", false, true)]
//    public void ToUpwardBreakoutsTests_1_Full(string name, bool candlestick, bool lineCloses)
//    {
//        _cryptoDataFilePath = Path.Combine(_cryptoDataDirectory, "TestData -- full.csv");
//        Prices = new CsvReaderHelper().ReadData<Price, PriceClassMap>(Path.Combine(DirectoryPath(), "TestData -- full.csv")).ToList() ?? new();

//        var upswings = Prices.ToUpswings(EnumCloseType.Close, true);
//        var upwardBreakouts = Prices.ToUpwardBreakouts(EnumCloseType.Close, true);
//        var chart = ChartGenerator.CreatePriceChart(Prices, candlestick: candlestick, lineCloses: lineCloses, lineWidth: 1);
//        chart = chart
//            .WithHigherHighs(upswings, EnumCloseType.Close)
//            .WithHigherLows(upswings, EnumCloseType.Close)
//            .WithUpswings(upswings, EnumCloseType.Close, lineWidth: 2, color: "cyan")
//            .WithBreaksOfStructureMarkers(upswings, EnumCloseType.Close, lineWidth: 3, color: "yellow", markerSize: 6)
//            .WithMarketStructureBreaksMarkers(upswings, EnumCloseType.Close, lineWidth: 3, color: "orange", markerSize: 6)
//            .WithUpwardBreakouts(upwardBreakouts, EnumCloseType.Close)
//            ;

//        AssertChart(name, chart);
//    }

//    [DataTestMethod]
//    [DataRow("ToUpwardBreakoutsTests_LineCloses_WithInterims", false, true)]
//    public void ToUpwardBreakoutsTests_WithInterims(string name, bool candlestick, bool lineCloses)
//    {
//        _cryptoDataFilePath = Path.Combine(_cryptoDataDirectory, "TestData -- full.csv");
//        Prices = new CsvReaderHelper().ReadData<Price, PriceClassMap>(Path.Combine(DirectoryPath(), "TestData -- full.csv")).ToList() ?? new();

//        var upswings = Prices.ToUpswings(EnumCloseType.Close, true);
//        var upwardBreakouts = Prices.ToUpwardBreakouts(EnumCloseType.Close, true);
//        var chart = ChartGenerator.CreatePriceChart(Prices, candlestick: candlestick, lineCloses: lineCloses, lineWidth: 1);
//        chart = chart
//            //.WithHigherHighs(upswings, EnumCloseType.Close)
//            //.WithHigherLows(upswings, EnumCloseType.Close)
//            .WithUpswings(upswings, EnumCloseType.Close, lineWidth: 2, color: "cyan")
//            .WithBreaksOfStructureMarkers(upswings, EnumCloseType.Close, lineWidth: 3, color: "yellow", markerSize: 6)
//            .WithMarketStructureBreaksMarkers(upswings, EnumCloseType.Close, lineWidth: 3, color: "orange", markerSize: 6)
//            .WithUpwardBreakouts(upwardBreakouts, EnumCloseType.Close)
//            ;

//        var depth = 0;
//        foreach (var upswing in upswings)
//        {
//            chart = chart.WithInterimSwings(upswing, EnumCloseType.Close, depth);
//            chart = chart.WithInterimUpwardBreakouts(upswing, depth);
//        }

//        AssertChart(name, chart);
//    }

//    [DataTestMethod]
//    [DataRow("ToUpwardBreakoutsTests_LineCloses_WithInterims_Large", false, true)]
//    public void ToUpwardBreakoutsTests_WithInterims_Large(string name, bool candlestick, bool lineCloses)
//    {
//        _cryptoDataFilePath = Path.Combine(_cryptoDataDirectory, "TestData -- large.csv");
//        Prices = new CsvReaderHelper().ReadData<Price, PriceClassMap>(Path.Combine(DirectoryPath(), "TestData -- large.csv")).ToList() ?? new();

//        var upswings = Prices.ToUpswings(EnumCloseType.Close, true);
//        var upwardBreakouts = Prices.ToUpwardBreakouts(EnumCloseType.Close, true);
//        var chart = ChartGenerator.CreatePriceChart(Prices, candlestick: candlestick, lineCloses: lineCloses, lineWidth: 1);
//        chart = chart
//            .WithHigherHighs(upswings, EnumCloseType.Close)
//            .WithHigherLows(upswings, EnumCloseType.Close)
//            //.WithUpswings(upswings, EnumCloseType.Close, lineWidth: 2, color: "cyan")
//            //.WithBreaksOfStructureMarkers(upswings, EnumCloseType.Close, lineWidth: 3, color: "yellow", markerSize: 6)
//            //.WithMarketStructureBreaksMarkers(upswings, EnumCloseType.Close, lineWidth: 3, color: "orange", markerSize: 6)
//            .WithUpwardBreakouts(upwardBreakouts, EnumCloseType.Close)
//            ;

//        foreach (var upswing in upswings)
//        {
//            //chart = chart.WithInterimSwings(upswing);
//            chart = chart.WithInterimUpwardBreakouts(upswing, 1);
//        }

//        AssertChart(name, chart);
//    }
//}