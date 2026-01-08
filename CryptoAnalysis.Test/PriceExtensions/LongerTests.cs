using CryptoAnalysis.Csv.ClassMaps;
using Gradient.CryptoAnalysis.Csv;

namespace Gradient.CryptoAnalysis.Test.PriceExtensions;

[TestClass]
public class LongerTests : PricesTests
{
    public override string TestDirectory => Path.Combine("PricesExtensionsData", "LongerTests");

    [DataTestMethod]
    [DataRow("ToDownswingTests_LineCloses", false, true, 0)]
    public void Test1(string name, bool candlestick, bool lineCloses, int interims)
    {
        _prices = new CsvReaderHelper().ReadData<Price, PriceClassMap>(Path.Combine(DirectoryPath(), "TestData -- full.csv")).ToList() ?? new();

        var closeType = EnumCloseType.Close;
        var upswings = _prices.ToUpswings(closeType, true);
        var downswings = _prices.ToDownswings(closeType, true);
        var chart = ChartGenerator.CreatePriceChart(_prices, candlestick: candlestick, lineCloses: lineCloses, lineWidth: 1);

        chart = chart
            .WithHigherHighs(upswings, closeType)
            .WithHigherLows(upswings, closeType)
            .WithLowerHighs(downswings, closeType)
            .WithLowerLows(downswings, closeType)
            //.WithDownswings(downswings, closeType, lineWidth: 1, color: "red")
            //.WithUpswings(upswings, closeType, lineWidth: 1, color: "green")
            .WithBreaksOfStructureMarkers(upswings, closeType, lineWidth: 3, color: "cyan", markerSize: 6)
            .WithBreaksOfStructureMarkers(downswings, closeType, lineWidth: 3, color: "cyan", markerSize: 6)
            .WithMarketStructureBreaksMarkers(upswings, closeType, lineWidth: 3, color: "orange", markerSize: 6)
            .WithMarketStructureBreaksMarkers(downswings, closeType, lineWidth: 3, color: "orange", markerSize: 6)
            ;

        if (interims > -1)
        {
            name = $"{name}_Interims_{interims}";
            foreach (var upswing in upswings)
            {
                chart = chart.WithInterimSwings(upswing, EnumCloseType.Close, interims);
            }

            foreach (var downswing in downswings)
            {
                chart = chart.WithInterimSwings(downswing, EnumCloseType.Close, interims);
            }
        }

        AssertChart(name, chart);
    }
}