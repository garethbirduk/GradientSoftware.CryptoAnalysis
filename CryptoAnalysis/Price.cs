using CsvHelper.Configuration.Attributes;

namespace Gradient.CryptoAnalysis
{
    /// <summary>
    /// The vocabulary of market-structure terms. Each member's <see cref="TermAttribute"/> sets how it is drawn;
    /// its summary is the definition shown in the term library, and its remarks describe how the code detects it.
    /// </summary>
    public enum EnumAnnotationType
    {
        None,

        /// <summary>
        /// A close that exceeds every close before it in the current run. It is the high an upswing starts from.
        /// </summary>
        /// <remarks>
        /// Detected as the first price of each <see cref="Upswing"/>.
        /// </remarks>
        [Term("HH", "Higher high", TermCategories.StructurePoints, EnumPosition.Above, "green", "triangle-down")]
        HigherHigh,

        /// <summary>
        /// The lowest close in the pullback that follows a higher high, before price breaks above that high again.
        /// </summary>
        /// <remarks>
        /// Detected as <see cref="Upswing.SwingLow"/> of each <see cref="Upswing"/>.
        /// </remarks>
        [Term("HL", "Higher low", TermCategories.StructurePoints, EnumPosition.Below, "red", "triangle-up")]
        HigherLow,

        /// <summary>
        /// The highest close in the bounce that follows a lower low, before price breaks below that low again.
        /// </summary>
        /// <remarks>
        /// Detected as <see cref="Downswing.SwingHigh"/> of each <see cref="Downswing"/>.
        /// </remarks>
        [Term("LH", "Lower high", TermCategories.StructurePoints, EnumPosition.Above, "green", "triangle-down")]
        LowerHigh,

        /// <summary>
        /// A close that is below every close before it in the current run. It is the low a downswing starts from.
        /// </summary>
        /// <remarks>
        /// Detected as <see cref="Downswing.SwingLow"/> of each <see cref="Downswing"/>.
        /// </remarks>
        [Term("LL", "Lower low", TermCategories.StructurePoints, EnumPosition.Below, "red", "triangle-up")]
        LowerLow,

        /// <summary>
        /// The first close beyond the swing's starting point, continuing the trend: above the higher high in an upswing,
        /// below the lower low in a downswing.
        /// </summary>
        /// <remarks>
        /// Detected by <see cref="Upswing.BreakOfStructure"/> and <see cref="Downswing.BreakOfStructure"/>.
        /// </remarks>
        [Term("BoS", "Break of structure", TermCategories.StructureBreaks, EnumPosition.Left, "cyan", "diamond")]
        BreakOfStructure,

        /// <summary>
        /// The first close that breaks the previous swing's protective point, signalling a possible trend change:
        /// below the previous higher low in an upswing, above the previous lower high in a downswing.
        /// </summary>
        /// <remarks>
        /// Detected by <see cref="Upswing.MarketStructureBreak"/> and <see cref="Downswing.MarketStructureBreak"/>.
        /// </remarks>
        [Term("MSB", "Market structure break", TermCategories.StructureBreaks, EnumPosition.Left, "orange", "x")]
        MarketStructureBreak,

        /// <summary>
        /// A run of consecutive green candles (close above open).
        /// </summary>
        [Term("G", "Successive green candles", TermCategories.CandlePatterns, EnumPosition.Above, "green", "circle")]
        SuccessiveGreenCandles,

        /// <summary>
        /// A run of consecutive red candles (close below open).
        /// </summary>
        [Term("R", "Successive red candles", TermCategories.CandlePatterns, EnumPosition.Above, "red", "circle")]
        SuccessiveRedCandles,
    }

    public enum EnumCloseType
    {
        None = 0,

        Close,

        High,
        Low
    }

    public enum EnumPosition
    {
        None,

        Precise,

        Above,

        Below,

        Left,

        Right,
    }

    public static class PriceExtensions
    {
        public static bool IsGlobalEnd(this Price price)
        {
            return price.DateTime == DateTime.MaxValue;
        }

        public static bool IsGlobalStart(this Price price)
        {
            return price.DateTime == DateTime.MinValue;
        }

        public static bool IsGreen(this Price price)
        {
            return price.Close > price.Open;
        }

        public static bool IsRed(this Price price)
        {
            return price.Close < price.Open;
        }
    }

    public class AnnotatedPrice : Price
    {
        public List<Annotation> Annotations { get; set; } = new();
    }

    public class Annotation
    {
        public EnumAnnotationType AnnotationType { get; set; }
        public string Note { get; set; } = "";

        public EnumPosition Position { get; set; } = EnumPosition.None;

        /// <summary>
        /// Creates an annotation, defaulting the note and position from the term registry.
        /// </summary>
        public static Annotation Create(EnumAnnotationType annotationType, string? note = null, EnumPosition? position = null)
        {
            var term = annotationType == EnumAnnotationType.None ? null : Terms.Get(annotationType);

            return new Annotation
            {
                AnnotationType = annotationType,
                Note = note ?? term?.Label ?? annotationType.ToString(),
                Position = position ?? term?.Position ?? EnumPosition.None
            };
        }
    }

    public class Price
    {
        [Name("close")]
        public double Close { get; set; }

        [Name("time")]
        public DateTime DateTime { get; set; }

        [Name("high")]
        public double High { get; set; }

        public Gradient.CryptoAnalysis.OtherData.Indicators Indicators { get; set; } = new();

        [Name("low")]
        public double Low { get; set; }

        [Name("open")]
        public double Open { get; set; }

        public double CloseValue(EnumCloseType closeType)
        {
            switch (closeType)
            {
                case EnumCloseType.Close:
                    {
                        return Close;
                    }
                case EnumCloseType.High:
                    {
                        return High;
                    }
                case EnumCloseType.Low:
                    {
                        return Low;
                    }
                default:
                    throw new NotSupportedException("EnumCloseType must be specified");
            }
        }

        public override string ToString()
        {
            return $"{DateTime} : {Close}";
        }
    }
}