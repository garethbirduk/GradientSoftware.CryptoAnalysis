namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public class IsMichaelsEmaChangedGreenToRedCondition : PriceCondition
    {
        protected override bool IsMet()
        {
            IsMichaelsEmaGreenCondition.SetPrices(Prices, Cursor.Last);
            IsMichaelsEmaRedCondition.SetPrices(Prices, Cursor.Last);

            var currentIndex = CurrentIndex;
            if (currentIndex == 0)
            {
                return false;
            }

            var previousIndex = currentIndex - 1;

            IsMichaelsEmaGreenCondition.SetPrice(previousIndex);
            IsMichaelsEmaRedCondition.SetPrice(currentIndex);

            return IsMichaelsEmaGreenCondition.IsMet(false) && IsMichaelsEmaRedCondition.IsMet(false);
        }

        public IsMichaelsEmaChangedGreenToRedCondition()
        {
        }

        public IsMichaelsEmaGreenCondition IsMichaelsEmaGreenCondition { get; set; } = new();
        public IsMichaelsEmaRedCondition IsMichaelsEmaRedCondition { get; set; } = new();
    }
}