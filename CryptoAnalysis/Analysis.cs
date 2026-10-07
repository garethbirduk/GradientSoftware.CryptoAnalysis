namespace Gradient.CryptoAnalysis
{
    public class Analysis
    {
        internal void Analyse()
        {
            var xxxxx = Results.Select(x => x.Value).OrderBy(x => x.Select(y => y.Profit)).ToList();
        }

        public Dictionary<string, List<TradeResult>> Results { get; } = new();
    }
}