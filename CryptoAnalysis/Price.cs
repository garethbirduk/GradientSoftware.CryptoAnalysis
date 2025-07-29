using CsvHelper.Configuration.Attributes;

namespace Gradient.CryptoAnalysis
{
    public enum EnumAnnotationType
    {
        None,

        [Name("HH")]
        [Position(EnumPosition.Above)]
        HigherHigh,

        [Name("HL")]
        [Position(EnumPosition.Below)]
        HigherLow,

        [Name("LH")]
        [Position(EnumPosition.Above)]
        LowerHigh,

        [Name("LL")]
        [Position(EnumPosition.Below)]
        LowerLow,

        [Name("BoS")]
        [Position(EnumPosition.Left)]
        BreakOfStructure,

        [Name("MSB")]
        [Position(EnumPosition.Left)]
        MarketStructureBreak,

        [Name("G")]
        [Position(EnumPosition.Above)]
        SuccessiveGreenCandles,

        [Name("R")]
        [Position(EnumPosition.Above)]
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

        public static Annotation Create(EnumAnnotationType annotationType, string? note = null, EnumPosition? position = null)
        {
            var finalNote = note ?? annotationType
                .GetType()
                .GetField(annotationType.ToString())?
                .GetCustomAttributes(typeof(NameAttribute), false)
                .Cast<NameAttribute>()
                .FirstOrDefault()?.Names?.FirstOrDefault() ?? annotationType.ToString();

            var finalPosition = position ?? annotationType
                .GetType()
                .GetField(annotationType.ToString())?
                .GetCustomAttributes(typeof(PositionAttribute), false)
                .Cast<PositionAttribute>()
                .FirstOrDefault()?.Position ?? EnumPosition.None;

            return new Annotation
            {
                AnnotationType = annotationType,
                Note = finalNote,
                Position = finalPosition
            };
        }
    }

    public sealed class ColorAttribute : Attribute
    {
        public ColorAttribute(string color)
        {
            Color = color;
        }

        public string Color { get; }
    }

    public sealed class PositionAttribute : Attribute
    {
        public PositionAttribute(EnumPosition position)
        {
            Position = position;
        }

        public EnumPosition Position { get; }
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