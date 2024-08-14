using Gradient.CryptoAnalysis.Csv;

namespace Gradient.CryptoAnalysis
{
    public class CheckIndicators
    {
        public string OutputFilepath { get; set; }
        public ConditionRules PositionRules { get; set; } = new();
        public List<Price> Prices { get; set; } = new();
        public List<Price> PricesWhereConditionIsMet { get; set; } = new();

        public DateTime StartDateTime { get; set; }

        public List<Trade> Trades { get; set; } = new();

        public List<Trade> Execute()
        {
            var met = new List<DateTime>();
            var index = Prices.FindIndex(x => x.DateTime >= StartDateTime);

            while (index > -1 && index < Prices.Count())
            {
                var p = Prices[index];
                var dateTime = p.DateTime;

                if (PositionRules.PreConditions.IsMet(Prices, dateTime))
                {
                    PricesWhereConditionIsMet.Add(p);
                }
                index++;
            }

            var helper = new CsvReaderHelper();
            helper.WriteData<Price, PriceClassMap>(OutputFilepath, PricesWhereConditionIsMet);

            return Trades;
        }
    }
}