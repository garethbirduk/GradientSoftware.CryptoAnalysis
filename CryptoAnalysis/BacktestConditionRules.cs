using Gradient.CryptoAnalysis.Conditions;

namespace Gradient.CryptoAnalysis
{
    /// <summary>
    /// Sets a trade's take-profit and stop-loss prices as it opens, from the prices up to the opening candle. Null leaves the
    /// trade at its default targets, 10% either side of the open.
    /// </summary>
    public delegate (double TakeProfit, double StopLoss)? TargetRule(List<Price> prices, DateTime dateTime);

    /// <summary>
    /// The price a trade fills at on the candle that called for it, from the prices up to that candle: a limit order resting
    /// at a level the candle reached, say, or the candle's close. Null when the candle gives no fill, and no trade is opened.
    /// </summary>
    public delegate double? EntryRule(List<Price> prices, DateTime dateTime);

    public class BacktestConditionRules
    {
        public ConditionSet ConfirmationConditions { get; set; } = new ConditionSet();

        /// <summary>
        /// With this set, a trade opens on the candle that called for it, at the price this gives, with no confirmation step;
        /// its targets and exits apply from the next candle. Without it, a trade waits for its confirmation and opens at the
        /// open of the candle after that.
        /// </summary>
        public EntryRule? Entry { get; set; }

        public ConditionSet ExpireConditions { get; set; } = new ConditionSet();

        /// <summary>
        /// With this set, a new trade is only looked for while no trade is waiting, confirmed or open.
        /// </summary>
        public bool OnePositionAtATime { get; set; }

        public ConditionSet PreConditions { get; set; } = new ConditionSet();
        public ConditionSet StopLossConditions { get; set; } = new ConditionSet();
        public ConditionSet TakeProfitConditions { get; set; } = new ConditionSet();

        /// <summary>
        /// Where a trade's targets come from when it opens; null for the default 10% either side.
        /// </summary>
        public TargetRule? Targets { get; set; }
    }
}
