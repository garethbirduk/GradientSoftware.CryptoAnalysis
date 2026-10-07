using CsvHelper.Configuration;
using Gradient.CryptoAnalysis;

namespace CryptoAnalysis.Csv.ClassMaps
{
    public class BacktestResultClassMap : ClassMap<BacktestResult>
    {
        public BacktestResultClassMap()
        {
            Map(m => m.Coin).Name("Coin");
            Map(m => m.EntryDateTime).Name("Entry Date & Time");
            Map(m => m.ExitDateTime).Name("Exit Date & Time");
            Map(m => m.Direction).Name("Direction");
            Map(m => m.Entry).Name("Entry");
            Map(m => m.StopLoss).Name("Stop Loss");
            Map(m => m.Exit).Name("Exit");
            Map(m => m.Returns).Name("Returns");
            Map(m => m.WinLoss).Name("Win / Loss");
        }
    }
}