namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    /// <summary>
    /// Met on a candle that breaks structure in the given direction at a sawtooth level, as seen from the prices up to that
    /// candle (see <see cref="MarketStructure"/>).
    /// </summary>
    public class IsBreakOfStructureCondition : PriceCondition, IAdjustableCandles
    {
        protected override bool IsMet()
        {
            var type = Direction == EnumSwingDirection.Up ? EnumAnnotationType.BullishBreakOfStructure : EnumAnnotationType.BearishBreakOfStructure;
            return MarketStructure.EventsOnLastCandle(PricesToCurrent(), Basis, Level).Any(x => x.Type == type);
        }

        public IsBreakOfStructureCondition(int additionalCandles = DefaultAdditionalCandles, EnumSwingDirection direction = EnumSwingDirection.Up,
            int level = 1, EnumPriceBasis basis = EnumPriceBasis.Close) : base(additionalCandles)
        {
            Direction = direction;
            Level = level;
            Basis = basis;
        }

        public EnumPriceBasis Basis { get; }

        public EnumSwingDirection Direction { get; }

        public int Level { get; }

        /// <inheritdoc/>
        public void SetAdditionalCandles(int additionalCandles)
        {
            AdditionalCandles = additionalCandles;
        }
    }
}
