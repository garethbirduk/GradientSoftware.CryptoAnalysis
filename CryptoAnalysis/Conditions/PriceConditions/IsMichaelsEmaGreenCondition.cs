namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public class IsMichaelsEmaGreenCondition : PriceCondition
    {
        protected override bool IsMet()
        {
            if (Price.Indicators.MichaelsEma == null)
                return false;

            var big = Price.Indicators.MichaelsEma.EMABig;
            var small = Price.Indicators.MichaelsEma.EMASmall;
            return small > big;
        }

        public IsMichaelsEmaGreenCondition() : base()
        {
        }
    }
}