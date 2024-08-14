using CryptoAnalysis.Conditions;
using Gradient.CryptoAnalysis.Conditions;

namespace Gradient.CryptoAnalysis
{
    public class Trade
    {
        private void TransitionAwaitingConfirmationToClosed(DateTime dateTime)
        {
            TradeStatus = EnumConditionStatus.Expired;
            DateTimeClose = dateTime;
            PriceClose = PriceOpen;
        }

        private void TransitionAwaitingConfirmationToConfirmed()
        {
            TradeStatus = EnumConditionStatus.Confirmed;
        }

        private void TransitionConfirmedToOpen(DateTime dateTime)
        {
            TradeStatus = EnumConditionStatus.Open;
            DateTimeOpen = dateTime;
            PriceOpen = Prices.First(x => x.DateTime == dateTime).Open;

            var tp = 1.1 * PriceOpen;
            var sl = 0.9 * PriceOpen;

            ((IsPriceHighGreaterThanOrEqualCondition)TakeProfitCondition.AndConditions.Single()).SetTargetPrice(tp);
            ((IsPriceLowLessThanOrEqualCondition)StopLossCondition.AndConditions.Single()).SetTargetPrice(sl);
        }

        private void TransitionOpenToClosed(DateTime dateTime, double price)
        {
            TradeStatus = EnumConditionStatus.Completed;
            DateTimeClose = dateTime;
            PriceClose = price;
        }

        public Trade(List<Price> prices, ConditionSet confirmationCondition,
            ConditionSet takeProfitCondition, ConditionSet stopLossCondition, ConditionSet expireCondition)
        {
            TradeStatus = EnumConditionStatus.AwaitingConfirmation;
            Prices = prices;
            ConfirmationCondition = confirmationCondition;
            TakeProfitCondition = takeProfitCondition;
            StopLossCondition = stopLossCondition;
            ExpireCondition = expireCondition;
        }

        public ConditionSet ConfirmationCondition { get; }
        public DateTime DateTimeClose { get; private set; }
        public DateTime DateTimeOpen { get; set; }
        public ConditionSet ExpireCondition { get; }
        public Guid Id { get; } = Guid.NewGuid();
        public double PriceClose { get; private set; }

        public double PriceOpen { get; private set; }

        public List<Price> Prices { get; }

        public ConditionSet StopLossCondition { get; }

        public double StopLossTarget
        {
            get
            {
                return ((IsPriceLowLessThanOrEqualCondition)StopLossCondition.AndConditions.Single()).TargetPrice;
            }
        }

        public ConditionSet TakeProfitCondition { get; }

        public double TakeProfitTarget
        {
            get
            {
                return ((IsPriceHighGreaterThanOrEqualCondition)TakeProfitCondition.AndConditions.Single()).TargetPrice;
            }
        }

        public EnumConditionStatus TradeStatus { get; private set; } = EnumConditionStatus.None;

        public void Update(DateTime dateTime)
        {
            switch (TradeStatus)
            {
                case EnumConditionStatus.AwaitingConfirmation:
                    if (ConfirmationCondition.IsMet(Prices, dateTime))
                        TransitionAwaitingConfirmationToConfirmed();
                    else if (ExpireCondition.IsMet(Prices, dateTime))
                        TransitionAwaitingConfirmationToClosed(dateTime);
                    break;

                case EnumConditionStatus.Confirmed:
                    TransitionConfirmedToOpen(dateTime);
                    break;

                case EnumConditionStatus.Open:
                    if (TakeProfitCondition.IsMet(Prices, dateTime))
                        TransitionOpenToClosed(dateTime, ((IsPriceHighGreaterThanOrEqualCondition)TakeProfitCondition.AndConditions.Single()).TargetPrice);
                    else if (StopLossCondition.IsMet(Prices, dateTime))
                        TransitionOpenToClosed(dateTime, ((IsPriceLowLessThanOrEqualCondition)StopLossCondition.AndConditions.Single()).TargetPrice);
                    break;

                default:
                    break;
            }
        }
    }
}