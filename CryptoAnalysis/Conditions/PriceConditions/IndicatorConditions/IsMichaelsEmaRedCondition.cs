namespace Gradient.CryptoAnalysis.Conditions.PriceConditions.IndicatorConditions
{
    public class IsMichaelsEmaRedCondition : IndicatorCondition
    {
        public IsMichaelsEmaRedCondition() : base()
        {
        }

        public override bool IsMet()
        {
            if (Price.Indicators.MichaelsEma == null)
                return false;

            var big = Price.Indicators.MichaelsEma.EMABig;
            var small = Price.Indicators.MichaelsEma.EMASmall;
            return small < big;
        }
    }
}