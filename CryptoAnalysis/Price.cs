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
        /// A high above the previous high. Price has pushed past its last peak, the upward half of structure.
        /// </summary>
        /// <remarks>
        /// Detected on the sawtooth: a high pivot above the previous high pivot at the same level (see <see cref="Sawtooth"/>).
        /// </remarks>
        [Term("HH", "Higher high", TermCategories.StructurePoints, EnumPosition.Above, "green", "triangle-down")]
        HigherHigh,

        /// <summary>
        /// A low above the previous low. The pullback held above the last trough.
        /// </summary>
        /// <remarks>
        /// Detected on the sawtooth: a low pivot above the previous low pivot at the same level (see <see cref="Sawtooth"/>).
        /// </remarks>
        [Term("HL", "Higher low", TermCategories.StructurePoints, EnumPosition.Below, "green", "triangle-up")]
        HigherLow,

        /// <summary>
        /// A high below the previous high. The bounce failed to reach the last peak.
        /// </summary>
        /// <remarks>
        /// Detected on the sawtooth: a high pivot below the previous high pivot at the same level (see <see cref="Sawtooth"/>).
        /// </remarks>
        [Term("LH", "Lower high", TermCategories.StructurePoints, EnumPosition.Above, "red", "triangle-down")]
        LowerHigh,

        /// <summary>
        /// A low below the previous low. Price has broken beneath its last trough, the downward half of structure.
        /// </summary>
        /// <remarks>
        /// Detected on the sawtooth: a low pivot below the previous low pivot at the same level (see <see cref="Sawtooth"/>).
        /// </remarks>
        [Term("LL", "Lower low", TermCategories.StructurePoints, EnumPosition.Below, "red", "triangle-up")]
        LowerLow,

        /// <summary>
        /// A high, the pullback low after it, and the first close back above the high. The break of structure confirms it:
        /// until then the high may be the top. The pullback low is the swing's protective level.
        /// </summary>
        /// <remarks>
        /// Detected on the sawtooth (see <see cref="Sawtooth.Swings"/>): only inside an upleg of the level above, so a lower
        /// low inside an upleg is not a downswing. Asserted at its start, the high it pulls back from. The swings one level
        /// finer that start inside it are its interim swings (see <see cref="Sawtooth.Interims"/>).
        /// </remarks>
        [Term("S↑", "Upswing", TermCategories.Swings, EnumPosition.Above, "#2563eb", "square")]
        Upswing,

        /// <summary>
        /// A low, the bounce high after it, and the first close back below the low. The break of structure confirms it:
        /// until then the low may be the bottom. The bounce high is the swing's protective level.
        /// </summary>
        /// <remarks>
        /// Detected on the sawtooth (see <see cref="Sawtooth.Swings"/>): only inside a downleg of the level above, so a higher
        /// high inside a downleg is not an upswing. Asserted at its start, the low it bounces from. The swings one level
        /// finer that start inside it are its interim swings (see <see cref="Sawtooth.Interims"/>).
        /// </remarks>
        [Term("S↓", "Downswing", TermCategories.Swings, EnumPosition.Below, "#9333ea", "square")]
        Downswing,

        /// <summary>
        /// The first close above an upswing's high after the pullback from it. It confirms the upswing and continues the move up.
        /// </summary>
        /// <remarks>
        /// Detected on the sawtooth (see <see cref="Sawtooth.Swings"/>): inside an upleg of the level above, a high, the low
        /// after it, then the first close above that high.
        /// </remarks>
        [Term("BoS↑", "Bullish break of structure", TermCategories.StructureBreaks, EnumPosition.Above, "#3b82f6", "arrow-up")]
        BullishBreakOfStructure,

        /// <summary>
        /// The first close below a downswing's low after the bounce from it. It confirms the downswing and continues the move down.
        /// </summary>
        /// <remarks>
        /// Detected on the sawtooth (see <see cref="Sawtooth.Swings"/>): inside a downleg of the level above, a low, the high
        /// after it, then the first close below that low.
        /// </remarks>
        [Term("BoS↓", "Bearish break of structure", TermCategories.StructureBreaks, EnumPosition.Below, "#a855f7", "arrow-down")]
        BearishBreakOfStructure,

        /// <summary>
        /// Upswings following one another at the same level: higher highs broken again and again, with no downswing between them.
        /// It is confirmed by the second upswing's break of structure and lasts until a downswing breaks structure.
        /// </summary>
        /// <remarks>
        /// Detected on the sawtooth (see <see cref="Sawtooth.Trends"/>): a run of at least two consecutive upswings at a level,
        /// in the order they broke structure. Market structure breaks against it are counted, but do not end it. Asserted at
        /// the confirming break of structure.
        /// </remarks>
        [Term("T↑", "Uptrend", TermCategories.Trends, EnumPosition.Above, "#0d9488", "diamond")]
        Uptrend,

        /// <summary>
        /// Downswings following one another at the same level: lower lows broken again and again, with no upswing between them.
        /// It is confirmed by the second downswing's break of structure and lasts until an upswing breaks structure.
        /// </summary>
        /// <remarks>
        /// Detected on the sawtooth (see <see cref="Sawtooth.Trends"/>): a run of at least two consecutive downswings at a
        /// level, in the order they broke structure. Market structure breaks against it are counted, but do not end it.
        /// Asserted at the confirming break of structure.
        /// </remarks>
        [Term("T↓", "Downtrend", TermCategories.Trends, EnumPosition.Below, "#e11d48", "diamond")]
        Downtrend,

        /// <summary>
        /// The first close above a downswing's protective high (the bounce high before its break of structure): a warning that
        /// the fall may be ending. It is a signal, not structure: the downswing can still continue.
        /// </summary>
        /// <remarks>
        /// Detected on the sawtooth (see <see cref="Sawtooth.MarketStructureBreaks"/>): from a downswing's break of structure
        /// until the next downswing at the same level breaks structure, the first close above its bounce high. Each protective
        /// high breaks at most once.
        /// </remarks>
        [Term("MSB↑", "Bullish market structure break", TermCategories.Indicators, EnumPosition.Above, "orange", "triangle-up-open")]
        BullishMarketStructureBreak,

        /// <summary>
        /// The first close below an upswing's protective low (the pullback low before its break of structure): a warning that
        /// the rise may be ending. It is a signal, not structure: the upswing can still continue.
        /// </summary>
        /// <remarks>
        /// Detected on the sawtooth (see <see cref="Sawtooth.MarketStructureBreaks"/>): from an upswing's break of structure
        /// until the next upswing at the same level breaks structure, the first close below its pullback low. Each protective
        /// low breaks at most once.
        /// </remarks>
        [Term("MSB↓", "Bearish market structure break", TermCategories.Indicators, EnumPosition.Below, "orange", "triangle-down-open")]
        BearishMarketStructureBreak,

        /// <summary>
        /// A run of at least three consecutive green candles (close above open). A red candle, or one that closes where it
        /// opened, ends the run. It is a candle pattern, not structure.
        /// </summary>
        /// <remarks>
        /// Detected by <see cref="CandleRuns.Runs"/>; each run is one maximal stretch, drawn from the first candle's open to the
        /// last candle's close, and asserted at its last candle.
        /// </remarks>
        [Term("G", "Successive green candles", TermCategories.CandlePatterns, EnumPosition.Above, "#16a34a", "arrow-up")]
        SuccessiveGreenCandles,

        /// <summary>
        /// A run of at least three consecutive red candles (close below open). A green candle, or one that closes where it
        /// opened, ends the run. It is a candle pattern, not structure.
        /// </summary>
        /// <remarks>
        /// Detected by <see cref="CandleRuns.Runs"/>; each run is one maximal stretch, drawn from the first candle's open to the
        /// last candle's close, and asserted at its last candle.
        /// </remarks>
        [Term("R", "Successive red candles", TermCategories.CandlePatterns, EnumPosition.Below, "#dc2626", "arrow-down")]
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