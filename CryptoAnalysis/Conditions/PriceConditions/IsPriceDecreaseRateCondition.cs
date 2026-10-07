namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public class IsPriceDecreaseRateCondition : PriceCondition
    {
        protected override bool IsMet()
        {
            var prices = Prices.CreateSubsetByCount(AdditionalCandles, Price, true);

            var from = prices.First();
            var to = prices.Last();

            switch (SubsetType)
            {
                case SubsetType.FirstToLast:
                default:
                    {
                        if (prices.Count() < AdditionalCandles + 1)
                            return false;
                        break;
                    }
                case SubsetType.HighestToLast:
                    {
                        from = prices.Where(x => x.Close == prices.Select(x => x.Close).Max()).First();
                        break;
                    }
                case SubsetType.FirstToLowest:
                    {
                        to = prices.Where(x => x.Close == prices.Select(x => x.Close).Min()).First();
                        break;
                    }
                case SubsetType.HighestToLowest:
                    {
                        from = prices.Where(x => x.Close == prices.Select(x => x.Close).Max()).First();
                        to = prices.Where(x => x.Close == prices.Select(x => x.Close).Min()).First();
                        break;
                    }
            }

            var data = Prices.CreateSubset(from, to, true);
            return data.HasDecreasedByPercentage(PercentageDecrease);
        }

        public IsPriceDecreaseRateCondition(double percentageDecrease, int additionalCandles, SubsetType subsetType) : base(additionalCandles)
        {
            PercentageDecrease = percentageDecrease;
            SubsetType = subsetType;
        }

        public double PercentageDecrease { get; set; }
        public SubsetType SubsetType { get; set; }
    }
}