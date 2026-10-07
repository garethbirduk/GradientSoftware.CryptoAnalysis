namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public class IsRetracementCondition : PriceCondition
    {
        protected override bool IsMet()
        {
            if (PreviousUpswing == null)
                return false;
            if (CurrentUpswing == null)
                return false;

            Price? low = null;

            var maxDepth = 3;
            var previousInterimUpswings = PreviousUpswing.FinalInterimUpswings(LowCloseType, maxDepth);

            if (previousInterimUpswings.Any())
                low = previousInterimUpswings.Last().SwingLow(LowCloseType);
            else
                low = PreviousUpswing.SwingLow(LowCloseType);

            if (low == null)
                return false;

            var previousSwingLowValue = low.CloseValue(LowCloseType);
            var currentSwingHighValue = CurrentUpswing.SwingHigh(HighCloseType)?.CloseValue(HighCloseType);
            if (currentSwingHighValue == null)
                return false;
            var currentPrice = Price.CloseValue(CandidateCloseType);

            var numerator = currentSwingHighValue - currentPrice;
            var demoninator = currentSwingHighValue - previousSwingLowValue;
            if (demoninator == 0.0)
                return false;
            var result = (numerator / demoninator) > RequiredRetracement;
            return result;
        }

        public List<Upswing> Upswings = new List<Upswing>();

        public IsRetracementCondition(List<Price> prices, double requiredRetracement,
            EnumCloseType lowCloseType, EnumCloseType highCloseType, EnumCloseType candidateCloseType) : base()
        {
            Prices = prices;
            RequiredRetracement = requiredRetracement;
            LowCloseType = lowCloseType;
            HighCloseType = highCloseType;
            CandidateCloseType = candidateCloseType;

            Upswings = prices.ToUpswings(EnumCloseType.Close, true, false);

            if (Upswings.Count < 2)
                return;

            CurrentUpswing = Upswings.Last();
            PreviousUpswing = Upswings[^2];
        }

        public IsRetracementCondition(Upswing previousUpswing, Upswing currentUpswing, double requiredRetracement,
            EnumCloseType lowCloseType, EnumCloseType highCloseType, EnumCloseType candidateCloseType) : base()
        {
            PreviousUpswing = previousUpswing;
            CurrentUpswing = currentUpswing;
            RequiredRetracement = requiredRetracement;
            LowCloseType = lowCloseType;
            HighCloseType = highCloseType;
            CandidateCloseType = candidateCloseType;
        }

        public EnumCloseType CandidateCloseType { get; }
        public Upswing? CurrentUpswing { get; }
        public EnumCloseType HighCloseType { get; }
        public EnumCloseType LowCloseType { get; }
        public Upswing? PreviousUpswing { get; set; }
        public double RequiredRetracement { get; }
    }
}