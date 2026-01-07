using CryptoAnalysis.Csv.ClassMaps;
using Gradient.CryptoAnalysis.Csv;

namespace Gradient.CryptoAnalysis.Test.PriceExtensions;

[TestClass]
public class ToUpswingsTests : PricesTests
{
    public override string TestDirectory => Path.Combine("PricesExtensionsData", "ToUpswingsTests");

    [DataTestMethod]
    [DataRow("ToUpswingTests_Candlestick", true, false)]
    [DataRow("ToUpswingTests_LineCloses", false, true)]
    public void ToUpswingTests_1(string name, bool candlestick, bool lineCloses)
    {
        var upswings = _prices.ToUpswings(EnumCloseType.Close, true);
        var chart = ChartGenerator.CreatePriceChart(_prices, candlestick: candlestick, lineCloses: lineCloses, lineWidth: 1);
        chart = chart
            .WithHigherHighs(upswings, EnumCloseType.Close)
            .WithHigherLows(upswings, EnumCloseType.Close)
            .WithUpswings(upswings, EnumCloseType.Close, lineWidth: 2, color: "cyan")
            .WithBreaksOfStructure(upswings, EnumCloseType.Close, lineWidth: 3, color: "yellow", markerSize: 6)
            .WithMarketStructureBreaks(upswings, EnumCloseType.Close, lineWidth: 3, color: "orange", markerSize: 6)
            ;

        AssertChart(name, chart);
    }

    [DataTestMethod]
    [DataRow("ToUpswingTests_Full", false, true)]
    public void ToUpswingTests_Full(string name, bool candlestick, bool lineCloses)
    {
        _cryptoDataFilePath = Path.Combine(_cryptoDataDirectory, "TestData -- Full.csv");
        _prices = new CsvReaderHelper().ReadData<Price, PriceClassMap>(Path.Combine(DirectoryPath(), "TestData -- Full.csv")).ToList() ?? new();

        var upswings = _prices.ToUpswings(EnumCloseType.Close, true);
        var chart = ChartGenerator.CreatePriceChart(_prices, candlestick: candlestick, lineCloses: lineCloses, lineWidth: 1);
        chart = chart
            .WithHigherHighs(upswings, EnumCloseType.Close)
            .WithHigherLows(upswings, EnumCloseType.Close)
            .WithUpswings(upswings, EnumCloseType.Close, lineWidth: 2, color: "cyan")
            .WithBreaksOfStructure(upswings, EnumCloseType.Close, lineWidth: 3, color: "yellow", markerSize: 6)
            .WithMarketStructureBreaks(upswings, EnumCloseType.Close, lineWidth: 3, color: "orange", markerSize: 6)
            ;

        AssertChart(name, chart);
    }

    //[DataTestMethod]
    //[DataRow("ToUpswingTests_Candlestick_Full_WithInterims", true, false)]
    //[DataRow("ToUpswingTests_LineCloses_Full_WithInterims", false, true)]
    public void ToUpswingTests_Full_WithInterims(string name, bool candlestick, bool lineCloses)
    {
        _cryptoDataFilePath = Path.Combine(_cryptoDataDirectory, "TestData -- Full.csv");
        _prices = new CsvReaderHelper().ReadData<Price, PriceClassMap>(Path.Combine(DirectoryPath(), "TestData -- Full.csv")).ToList() ?? new();

        var upswings = _prices.ToUpswings(EnumCloseType.Close, true, false);
        var downswings = _prices.ToDownswings(EnumCloseType.Close, true);

        var chart = ChartGenerator.CreatePriceChart(_prices, lineCloses: true, lineWidth: 1);
        chart = chart
            .WithHigherHighs(upswings, EnumCloseType.Close)
            .WithHigherLows(upswings, EnumCloseType.Close)
            .WithUpswings(upswings, EnumCloseType.Close, lineWidth: 2, color: "cyan")
            .WithBreaksOfStructure(upswings, EnumCloseType.Close, lineWidth: 3, color: "yellow", markerSize: 6)
            .WithMarketStructureBreaks(upswings, EnumCloseType.Close, lineWidth: 3, color: "orange", markerSize: 6)
            ;

        foreach (var upswing in upswings)
        {
            chart = chart.WithInterimUpswings(upswing, 3);
        }

        foreach (var downswing in downswings)
        {
            chart = chart.WithInterimDownswings(downswing, 3);
        }

        chart = chart
            .WithBreaksOfStructure(upswings, EnumCloseType.Close, lineWidth: 3, color: "cyan", markerSize: 6)
            .WithMarketStructureBreaks(upswings, EnumCloseType.Close, lineWidth: 3, color: "orange", markerSize: 6)
            ;

        AssertChart(name, chart);
    }

    [DataTestMethod]
    [DataRow("ToUpswingTests_Candlestick_Large_WithInterims", true, false)]
    [DataRow("ToUpswingTests_LineCloses_Large_WithInterims", false, true)]
    public void ToUpswingTests_Large_WithInterims(string name, bool candlestick, bool lineCloses)
    {
        _cryptoDataFilePath = Path.Combine(_cryptoDataDirectory, "TestData -- large.csv");
        _prices = new CsvReaderHelper().ReadData<Price, PriceClassMap>(Path.Combine(DirectoryPath(), "TestData -- large.csv")).ToList() ?? new();

        var upswings = _prices.ToUpswings(EnumCloseType.Close, true, false);
        var downswings = _prices.ToDownswings(EnumCloseType.Close, true);

        var chart = ChartGenerator.CreatePriceChart(_prices, candlestick: candlestick, lineCloses: lineCloses, lineWidth: 1);
        chart = chart
            .WithHigherHighs(upswings, EnumCloseType.Close)
            .WithHigherLows(upswings, EnumCloseType.Close)
            .WithUpswings(upswings, EnumCloseType.Close, lineWidth: 2, color: "cyan")
            .WithBreaksOfStructure(upswings, EnumCloseType.Close, lineWidth: 3, color: "yellow", markerSize: 6)
            .WithMarketStructureBreaks(upswings, EnumCloseType.Close, lineWidth: 3, color: "orange", markerSize: 6)
            ;

        foreach (var upswing in upswings)
        {
            chart = chart.WithInterimUpswings(upswing, 3);
        }

        foreach (var downswing in downswings)
        {
            chart = chart.WithInterimDownswings(downswing, 3);
        }

        chart = chart
            .WithBreaksOfStructure(upswings, EnumCloseType.Close, lineWidth: 3, color: "cyan", markerSize: 6)
            .WithMarketStructureBreaks(upswings, EnumCloseType.Close, lineWidth: 3, color: "orange", markerSize: 6)
            ;

        AssertChart(name, chart);
    }

    [TestMethod]
    public void ToUpswingTests_WithInterims()
    {
        var name = "ToUpswingTests_LineCloses_WithInterims";

        var upswings = _prices.ToUpswings(EnumCloseType.Close, true, false);
        var downswings = _prices.ToDownswings(EnumCloseType.Close, true);

        var chart = ChartGenerator.CreatePriceChart(_prices, lineCloses: true, lineWidth: 1);
        chart = chart
            .WithHigherHighs(upswings, EnumCloseType.Close)
            .WithHigherLows(upswings, EnumCloseType.Close)
            .WithUpswings(upswings, EnumCloseType.Close, lineWidth: 2, color: "cyan")
            .WithBreaksOfStructure(upswings, EnumCloseType.Close, lineWidth: 3, color: "yellow", markerSize: 6)
            .WithMarketStructureBreaks(upswings, EnumCloseType.Close, lineWidth: 3, color: "orange", markerSize: 6)
            ;

        foreach (var upswing in upswings)
        {
            chart = chart.WithInterimUpswings(upswing, 3);
        }

        foreach (var downswing in downswings)
        {
            chart = chart.WithInterimDownswings(downswing, 3);
        }

        chart = chart
            .WithBreaksOfStructure(upswings, EnumCloseType.Close, lineWidth: 3, color: "cyan", markerSize: 6)
            .WithMarketStructureBreaks(upswings, EnumCloseType.Close, lineWidth: 3, color: "orange", markerSize: 6)
            ;

        AssertChart(name, chart);
    }
}

public enum BreakOfStructureFormat
{
    Line,
    Box
}