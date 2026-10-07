namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    /// <summary>
    /// Met on a candle that makes a market structure break at a sawtooth level, as seen from the prices up to that candle
    /// (see <see cref="MarketStructure"/>). Direction is the break's direction: Down breaks an upswing's protective low
    /// (bearish), Up a downswing's protective high (bullish).
    /// </summary>
    public class IsMarketStructureBreakCondition : PriceCondition, IAdjustableCandles
    {
        protected override bool IsMet()
        {
            var type = Direction == EnumSwingDirection.Up ? EnumAnnotationType.BullishMarketStructureBreak : EnumAnnotationType.BearishMarketStructureBreak;
            return MarketStructure.EventsOnLastCandle(PricesToCurrent(), Basis, Level).Any(x => x.Type == type);
        }

        public IsMarketStructureBreakCondition(int successiveCandles = DefaultAdditionalCandles, EnumSwingDirection direction = EnumSwingDirection.Down,
            int level = 1, EnumPriceBasis basis = EnumPriceBasis.Close) : base(successiveCandles)
        {
            Direction = direction;
            Level = level;
            Basis = basis;
        }

        public EnumPriceBasis Basis { get; }

        public EnumSwingDirection Direction { get; }

        public int Level { get; }

        /// <inheritdoc/>
        public void SetAdditionalCandles(int successiveCandles)
        {
            AdditionalCandles = successiveCandles;
        }
    }
}
