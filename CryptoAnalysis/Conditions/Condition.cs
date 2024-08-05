using Gradient.CryptoAnalysis.Conditions.DateConditions;
using Gradient.CryptoAnalysis.Conditions.PriceConditions;

namespace Gradient.CryptoAnalysis.Conditions
{
    public class Condition
    {
        public List<ICondition> AndConditions { get; set; } = [];
        public List<Condition> AndSubConditions { get; set; } = [];
        public List<ICondition> OrConditions { get; set; } = [];
        public List<Condition> OrSubConditions { get; set; } = [];

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
                    ((IPriceCondition)condition).SetPrices(prices);
                    ((IPriceCondition)condition).SetPrice(dateTime);
                }
            }

            var m1 = AndConditions.All(x => x.IsMet());
            var m2 = OrConditions.Any(x => x.IsMet()) || OrConditions.Count() == 0;
            var m3 = AndSubConditions.All(x => x.IsMet(prices, dateTime));
            var m4 = OrSubConditions.Any(x => x.IsMet(prices, dateTime)) || OrSubConditions.Count() == 0;

            return m1 && m2 && m3 && m4;
        }
    }
}