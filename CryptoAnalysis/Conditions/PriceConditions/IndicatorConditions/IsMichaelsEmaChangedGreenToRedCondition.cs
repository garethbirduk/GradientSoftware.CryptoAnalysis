using Gradient.CryptoAnalysis;

namespace CryptoAnalysis.Conditions.PriceConditions.IndicatorConditions
{
    public class IsMichaelsEmaChangedGreenToRedCondition : IndicatorCondition
    {
        public IsMichaelsEmaChangedGreenToRedCondition()
        {
        }

        public IsMichaelsEmaGreenCondition IsMichaelsEmaGreenCondition { get; set; } = new();
        public IsMichaelsEmaRedCondition IsMichaelsEmaRedCondition { get; set; } = new();

        public override bool IsMet()
        {
            IsMichaelsEmaGreenCondition.SetPrices(Prices);
            IsMichaelsEmaRedCondition.SetPrices(Prices);

            var currentIndex = CurrentIndex;
            if (currentIndex == 0)
            {
                return false;
            }

            var previousIndex = currentIndex - 1;

            IsMichaelsEmaGreenCondition.SetPrice(previousIndex);
            IsMichaelsEmaRedCondition.SetPrice(currentIndex);

            return IsMichaelsEmaGreenCondition.IsMet() && IsMichaelsEmaRedCondition.IsMet();
        }
    }

    public class IsMichaelsEmaChangedRedToGreenCondition : IndicatorCondition
    {
        public IsMichaelsEmaChangedRedToGreenCondition()
        {
        }

        public IsMichaelsEmaGreenCondition IsMichaelsEmaGreenCondition { get; set; } = new();
        public IsMichaelsEmaRedCondition IsMichaelsEmaRedCondition { get; set; } = new();

        public override bool IsMet()
        {
            IsMichaelsEmaGreenCondition.SetPrices(Prices);
            IsMichaelsEmaRedCondition.SetPrices(Prices);

            var currentIndex = CurrentIndex;
            if (currentIndex == 0)
            {
                return false;
            }

            var previousIndex = currentIndex - 1;

            IsMichaelsEmaRedCondition.SetPrice(previousIndex);
            IsMichaelsEmaGreenCondition.SetPrice(currentIndex);

            return IsMichaelsEmaGreenCondition.IsMet() && IsMichaelsEmaRedCondition.IsMet();
        }
    }
}