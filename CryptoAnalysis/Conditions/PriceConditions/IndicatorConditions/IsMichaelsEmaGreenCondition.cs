using Gradient.CryptoAnalysis;

namespace Gradient.CryptoAnalysis.Conditions.PriceConditions.IndicatorConditions
{
    public class IsMichaelsEmaGreenCondition : IndicatorCondition
    {
        public IsMichaelsEmaGreenCondition() : base()
        {
        }

        public override bool IsMet()
        {
            if (Price.Indicators.MichaelsEma == null)
                return false;

            return Price.Indicators.MichaelsEma.EMABig > Price.Indicators.MichaelsEma.EMASmall;
        }
    }
}