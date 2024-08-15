using CryptoAnalysis.Conditions;

namespace Gradient.CryptoAnalysis.Conditions
{
    public class ConditionSet
    {
        public List<ICondition> AndConditions { get; set; } = [];
        public List<ConditionSet> AndSubConditions { get; set; } = [];
        public List<ICondition> OrConditions { get; set; } = [];
        public List<ConditionSet> OrSubConditions { get; set; } = [];

        public bool IsMet(List<Price> prices, DateTime dateTime)
        {
            foreach (var condition in AndConditions.Union(OrConditions))
            {
                if (condition.GetType().GetInterfaces().Contains(typeof(IDateCondition)))
                {
                    ((IDateCondition)condition).SetDateTimeCandidate(dateTime);
                }

                if (condition.GetType().GetInterfaces().Contains(typeof(IPriceCondition)))
                {
                    ((IPriceCondition)condition).SetPrices(prices, Cursor.None);
                    ((IPriceCondition)condition).SetPrice(dateTime);
                }

                if (condition.GetType().GetInterfaces().Contains(typeof(IAdjustableCandles)))
                {
                    ((IAdjustableCandles)condition).SetSuccessiveCandles(200);
                }
            }

            var m1 = AndConditions.All(x => x.IsMet(false));
            var m2 = OrConditions.Any(x => x.IsMet(false)) || OrConditions.Count() == 0;
            var m3 = AndSubConditions.All(x => x.IsMet(prices, dateTime));
            var m4 = OrSubConditions.Any(x => x.IsMet(prices, dateTime)) || OrSubConditions.Count() == 0;

            return m1 && m2 && m3 && m4;
        }
    }
}