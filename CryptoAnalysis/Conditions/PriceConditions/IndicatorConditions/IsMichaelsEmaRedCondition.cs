using Gradient.CryptoAnalysis;

namespace CryptoAnalysis.Conditions.PriceConditions.IndicatorConditions
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

            return Price.Indicators.MichaelsEma.EMABig < Price.Indicators.MichaelsEma.EMASmall;
        }
    }
}