namespace Gradient.CryptoAnalysis
{
    public class IsSuccessiveRedCandlesCondition : PriceCondition
    {
        public IsSuccessiveRedCandlesCondition(int successiveCandles) : base(successiveCandles)
        {
        }

        public override bool IsMet()
        {
            if (IsExpired)
                return false;

            var data = Prices.CreateSubsetByCount(SuccessiveCandles - 1, Price, true);

            if (data.Count() < MinDataSize)
                return false;

            if (data.Count() < SuccessiveCandles)
                return false;

            return data.All(x => x.Close < x.Open);
        }
    }
}