using Gradient.CryptoAnalysis.Conditions;

namespace Gradient.CryptoAnalysis
{
    public class Trade
    {
        private void TransitionAwaitingConfirmationToClosed(DateTime dateTime)
        {
            TradeStatus = EnumTradeStatus.Expired;
            DateTimeClose = dateTime;
            PriceClose = PriceOpen;
        }

        private void TransitionAwaitingConfirmationToConfirmed()
        {
            TradeStatus = EnumTradeStatus.Confirmed;
        }

        private void TransitionConfirmedToOpen(DateTime dateTime)
        {
            TradeStatus = EnumTradeStatus.Open;
            DateTimeOpen = dateTime;
            PriceOpen = Prices.First(x => x.DateTime == dateTime).Open;

            var tp = 1.1 * PriceOpen;
            var sl = 0.9 * PriceOpen;

            ((IsPriceHighGreaterThanOrEqualCondition)TakeProfitCondition.AndConditions.Single()).SetTargetPrice(tp);
            ((IsPriceLowLessThanOrEqualCondition)StopLossCondition.AndConditions.Single()).SetTargetPrice(sl);
        }

        private void TransitionOpenToClosed(DateTime dateTime, double price)
        {
            TradeStatus = EnumTradeStatus.Completed;
            DateTimeClose = dateTime;
            PriceClose = price;
        }

        public Trade(List<Price> prices, Condition confirmationCondition,
            Condition takeProfitCondition, Condition stopLossCondition, Condition expireCondition)
        {
            TradeStatus = EnumTradeStatus.AwaitingConfirmation;
            Prices = prices;
            ConfirmationCondition = confirmationCondition;
            TakeProfitCondition = takeProfitCondition;
            StopLossCondition = stopLossCondition;
            ExpireCondition = expireCondition;
        }

        public Condition ConfirmationCondition { get; }
        public DateTime DateTimeClose { get; private set; }
        public DateTime DateTimeOpen { get; set; }
        public Condition ExpireCondition { get; }
        public Guid Id { get; } = Guid.NewGuid();
        public double PriceClose { get; private set; }

        public double PriceOpen { get; private set; }

        public List<Price> Prices { get; }

        public Condition StopLossCondition { get; }

        public double StopLossTarget
        {
            get
            {
                return ((IsPriceLowLessThanOrEqualCondition)StopLossCondition.AndConditions.Single()).TargetPrice;
            }
        }

        public Condition TakeProfitCondition { get; }

        public double TakeProfitTarget
        {
            get
            {
                return ((IsPriceHighGreaterThanOrEqualCondition)TakeProfitCondition.AndConditions.Single()).TargetPrice;
            }
        }

        public EnumTradeStatus TradeStatus { get; private set; } = EnumTradeStatus.None;

        public void Update(DateTime dateTime)
        {
            switch (TradeStatus)
            {
                case EnumTradeStatus.AwaitingConfirmation:
                    if (ConfirmationCondition.IsMet(Prices, dateTime))
                        TransitionAwaitingConfirmationToConfirmed();
                    else if (ExpireCondition.IsMet(Prices, dateTime))
                        TransitionAwaitingConfirmationToClosed(dateTime);
                    break;

                case EnumTradeStatus.Confirmed:
                    TransitionConfirmedToOpen(dateTime);
                    break;

                case EnumTradeStatus.Open:
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