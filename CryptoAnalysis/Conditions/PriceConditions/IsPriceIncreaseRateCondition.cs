namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public enum SubsetType
    {
        FirstToLast,
        FirstToHighest,
        LowestToHighest,
        LowestToLast,
    }

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
                        break;
                    }
                case SubsetType.LowestToLast:
                    {
                        var min = prices.Select(x => x.Close).Min();
                        from = prices.Where(x => x.Close == min).First();
                        break;
                    }
                case SubsetType.FirstToHighest:
                    {
                        var max = prices.Select(x => x.Close).Max();
                        to = prices.Where(x => x.Close == max).First();
                        break;
                    }
                case SubsetType.LowestToHighest:
                    {
                        from = prices.Where(x => x.Low == prices.Select(x => x.Low).First()).First();
                        to = prices.Where(x => x.High == prices.Select(x => x.High).First()).First();
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