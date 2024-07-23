using Gradient.CryptoAnalysis.Conditions;

namespace Gradient.CryptoAnalysis
{
    public enum EnumTradeStatus
    {
        None,
        PreConditioned,
        AwaitingConfirmation,
        Confirmed,
        Open,
        Completed,
        Cancelled,
        Expired
    }

    public class Trade
    {
        private void TransitionAwaitingConfirmationToConfirmed()
        {
            TradeStatus = EnumTradeStatus.Confirmed;
        }

        private void TransitionConfirmedToOpen(DateTime dateTime)
        {
            TradeStatus = EnumTradeStatus.Open;
            DateTimeOpen = dateTime;
            PriceOpen = Prices.Single(x => x.Time == dateTime).Open;
        }

        private void TransitionOpenToClosed(DateTime dateTime, double price)
        {
            TradeStatus = EnumTradeStatus.Completed;
            DateTimeClose = dateTime;
            PriceClose = price;
        }

        private void TransitionOpenToExpired(DateTime dateTime)
        {
            TradeStatus = EnumTradeStatus.Expired;
            DateTimeClose = dateTime;
            PriceClose = PriceOpen;
        }

        private void TransitionPreconditionedToAwaitingConfirmation()
        {
            TradeStatus = EnumTradeStatus.AwaitingConfirmation;
        }

        public Trade(List<Price> prices, Condition confirmationCondition,
            Condition takeProfitCondition, Condition stopLossCondition, Condition expireCondition)
        {
            TradeStatus = EnumTradeStatus.PreConditioned;
            Prices = prices;
            ConfirmationCondition = confirmationCondition;
            TakeProfitCondition = takeProfitCondition;
            StopLossCondition = stopLossCondition;
            ExpireCondition = expireCondition;
        }

        public Condition ConfirmationCondition { get; }
        public DateTime DateTimeClose { get; private set; }
        public DateTime DateTimeOpen { get; private set; }
        public Condition ExpireCondition { get; }
        public double PriceClose { get; private set; }
        public double PriceOpen { get; private set; }
        public List<Price> Prices { get; }
        public Condition StopLossCondition { get; }
        public Condition TakeProfitCondition { get; }
        public EnumTradeStatus TradeStatus { get; private set; } = EnumTradeStatus.None;

        public void Update(DateTime dateTime)
        {
            switch (TradeStatus)
            {
                case EnumTradeStatus.PreConditioned:
                    TransitionPreconditionedToAwaitingConfirmation();
                    break;

                case EnumTradeStatus.AwaitingConfirmation:
                    if (ConfirmationCondition.IsMet(Prices, dateTime))
                        TransitionAwaitingConfirmationToConfirmed();
                    break;

                case EnumTradeStatus.Confirmed:
                    TransitionConfirmedToOpen(dateTime);
                    break;

                case EnumTradeStatus.Open:
                    if (TakeProfitCondition.IsMet(Prices, dateTime))
                        TransitionOpenToClosed(dateTime, ((IsPriceHighGreaterThanOrEqualCondition)TakeProfitCondition.AndConditions.Single()).TargetPrice);
                    else if (StopLossCondition.IsMet(Prices, dateTime))
                        TransitionOpenToClosed(dateTime, ((IsPriceLowLessThanOrEqualCondition)StopLossCondition.AndConditions.Single()).TargetPrice);
                    else if (ExpireCondition.IsMet(Prices, dateTime))
                        TransitionOpenToExpired(dateTime);
                    break;

                default:
                    break;
            }
        }
    }
}