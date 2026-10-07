namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public class IsPriceIncreaseRateCondition : PriceCondition
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
                case SubsetType.LowestToLast:
                    {
                        from = prices.Where(x => x.Close == prices.Select(x => x.Close).Min()).First();
                        break;
                    }
                case SubsetType.FirstToHighest:
                    {
                        to = prices.Where(x => x.Close == prices.Select(x => x.Close).Max()).First();
                        break;
                    }
                case SubsetType.LowestToHighest:
                    {
                        from = prices.Where(x => x.Close == prices.Select(x => x.Close).Min()).First();
                        to = prices.Where(x => x.Close == prices.Select(x => x.Close).Max()).First();
                        break;
                    }
            }

            var data = Prices.CreateSubset(from, to, true);
            return data.HasIncreasedByPercentage(PercentageIncrease);
        }

        public IsPriceIncreaseRateCondition(double percentageIncrease, int additionalCandles, SubsetType subsetType) : base(additionalCandles)
        {
            PercentageIncrease = percentageIncrease;
            SubsetType = subsetType;
        }

        public double PercentageIncrease { get; set; }
        public SubsetType SubsetType { get; set; }
    }
}