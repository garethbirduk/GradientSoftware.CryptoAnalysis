using Gradient.CryptoAnalysis;
using Gradient.CryptoAnalysis.Conditions.PriceConditions;

namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public class IsMichaelsEmaChangedRedToGreenCondition : PriceCondition
    {
        public IsMichaelsEmaChangedRedToGreenCondition()
        {
        }

        public IsMichaelsEmaGreenCondition IsMichaelsEmaGreenCondition { get; set; } = new();
        public IsMichaelsEmaRedCondition IsMichaelsEmaRedCondition { get; set; } = new();

        public override bool IsMet()
        {
            IsMichaelsEmaGreenCondition.SetPrices(Prices, Cursor.Last);
            IsMichaelsEmaRedCondition.SetPrices(Prices, Cursor.Last);

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